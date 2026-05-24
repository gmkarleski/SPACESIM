using System;
using System.Reflection;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.Vessels;
using Unity.Mathematics;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using static SpaceSim.Foundation.Vessels.Tests.PredictorTestState;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SoiCrossingPredictor"/>. Pure-math + body-registry
    /// integration tests at Earth-Moon scale. Tolerance conventions match
    /// <see cref="PeriapsisApoapsisPredictorTests"/>: ±2 ticks on direct closed-form
    /// (outward) predictions, ±<see cref="SoiCrossingPredictor.PredictorCoarseSampleTicks"/>
    /// on bisected (inward) predictions reflecting the coarse-sample-then-bisect
    /// algorithm's inherent granularity.
    ///
    /// Body construction pattern: <c>AddComponent&lt;ReferenceBody&gt;()</c> on a
    /// fresh GameObject, set <c>massKg</c> / <c>soiRadiusMeters</c> / <c>parentBody</c>
    /// via reflection on the private [SerializeField] fields, set transform.position
    /// for the body's world coordinates, then <c>InitializeBodyForTesting()</c>.
    /// This matches the helper in <c>VesselTests.BuildMoonAsChildOfEarth</c>.
    /// </summary>
    public class SoiCrossingPredictorTests
    {
        // Body parameters at Earth-Moon scale.
        private const double EarthMassKg = PhysicsConstants.EarthMassKg;
        private static readonly double EarthMu = PhysicsConstants.EarthMu;
        private const double EarthSoiRadiusMeters = 9.24e8;  // Real Earth heliocentric SOI ~924,000 km
        private const double MoonMassKg = 7.342e22;
        private const double MoonSoiRadiusMeters = 6.6e7;    // Real Moon SOI ~66,100 km
        private const double EarthMoonDistanceMeters = 3.844e8;
        private const double TickIntervalSeconds = 1.0 / 30.0;  // 30 Hz sim-tick

        // LEO scale for the orbit-fully-contained tests.
        private const double LeoRadius = 7_000_000.0;

        // High-periapsis scale (~200,000 km geocentric) used by tests whose orbits
        // reach far enough to engage SOI crossings. Higher periapsis keeps orbital
        // eccentricities below 0.8 even when apoapsis is at multi-million-km scale,
        // which keeps Newton-Raphson on Kepler's equation comfortably within its
        // convergence range (e > ~0.95 starts to challenge the 15-iteration limit
        // at the 1e-10 tolerance). See OrbitalElements.MaxKeplerIterations XML doc.
        private const double HighPeriapsisRadius = 2.0e8;

        private GameObject _earthGo;
        private ReferenceBody _earth;

        [SetUp]
        public void SetUp()
        {
            BodyRegistry.ClearForTesting();

            _earthGo = new GameObject("Earth");
            _earth = _earthGo.AddComponent<ReferenceBody>();
            // No parent — Earth is the local top-level body for these tests, but with
            // finite SOI so outward crossings can be tested.
            _earth.InitializeBodyForTesting(
                massKg: EarthMassKg,
                soiRadiusMeters: EarthSoiRadiusMeters);
        }

        [TearDown]
        public void TearDown()
        {
            if (_earthGo != null) UnityObject.DestroyImmediate(_earthGo);
            BodyRegistry.ClearForTesting();
        }

        // ----- Helpers -----

        // SetBodyMass / SetBodySoiRadius / SetBodyParent helpers removed in the
        // reflection-migration sweep. All four prior call clusters (SetUp,
        // BuildChildBody, two inline test sites) now invoke the parameterized
        // InitializeBodyForTesting(massKg, soiRadiusMeters, parentBody, ...)
        // overload directly.

        /// <summary>
        /// Create a child body of <see cref="_earth"/> at the given world offset, with
        /// the given mass and SOI radius. Registers automatically via
        /// InitializeBodyForTesting.
        /// </summary>
        private ReferenceBody BuildChildBody(
            string name, double3 worldOffsetMeters, double massKg, double soiRadius)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(
                (float)worldOffsetMeters.x,
                (float)worldOffsetMeters.y,
                (float)worldOffsetMeters.z);
            var body = go.AddComponent<ReferenceBody>();
            body.InitializeBodyForTesting(
                massKg: massKg,
                soiRadiusMeters: soiRadius,
                parentBody: _earth);
            return body;
        }

        // BuildState helper consolidated into PredictorTestState (imported via
        // `using static` at top of file). The optional referenceBodyId param —
        // which this file's original helper introduced for tests that wire a
        // specific body's id — is preserved by the consolidated helper.

        // ----- Test 1: orbit contained, no children -----

        [Test]
        public void PredictNextCrossing_VesselWellWithinSoi_OrbitContained_ReturnsNull()
        {
            // LEO orbit around Earth: r = 7,000 km, SOI = 924,000 km. Orbit is fully
            // inside Earth's SOI; no children registered; should return null.
            var state = BuildState(LeoRadius, eccentricity: 0.0, trueAnomalyAtEpoch: 0.0);

            long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                state, _earth, currentTick: 0, TickIntervalSeconds,
                SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

            Assert.IsNull(crossing,
                "LEO orbit fully inside Earth SOI with no children should produce no crossing");
        }

        // ----- Test 2: hyperbolic escape -----

        [Test]
        public void PredictNextCrossing_HyperbolicEscape_OutwardCrossingPredicted()
        {
            // Hyperbolic trajectory: a < 0, e > 1. Vessel currently INSIDE Earth's SOI
            // (well inside, at periapsis at ν=0), traveling outward. Should predict
            // the outward crossing at some finite future tick.
            // Periapsis at LEO scale; e = 1.5 (escape orbit).
            double rPeri = LeoRadius;
            double e = 1.5;
            // a = rPeri / (1 - e)  →  negative for hyperbolic.
            double a = rPeri / (1.0 - e);  // = 7e6 / -0.5 = -1.4e7
            var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

            long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                state, _earth, currentTick: 0, TickIntervalSeconds,
                SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

            Assert.IsTrue(crossing.HasValue,
                "Hyperbolic escape from inside Earth SOI should predict an outward crossing");
            Assert.Greater(crossing.Value, 0,
                "Crossing tick should be in the future");

            // Sanity: the crossing time should match the analytic expectation. Compute
            // ν at r = SOI: cos(ν) = (p/r - 1)/e, then convert ν → M → t.
            double p = a * (1.0 - e * e);
            double cosNu = (p / EarthSoiRadiusMeters - 1.0) / e;
            double nu = math.acos(cosNu);  // outbound branch
            double M = OrbitalElements.TrueToMeanAnomaly(nu, e);
            double n = OrbitalElements.MeanMotion(a, EarthMu);
            double tSeconds = M / n;
            long expectedTick = (long)math.ceil(tSeconds / TickIntervalSeconds);

            Assert.AreEqual(expectedTick, crossing.Value, 2,
                $"Hyperbolic outward crossing should land at ~{expectedTick} ticks");
        }

        // ----- Test 3: elliptical with apoapsis beyond SOI -----

        [Test]
        public void PredictNextCrossing_EllipticalApoapsisBeyondSoi_OutwardCrossingPredicted()
        {
            // Elliptical orbit: periapsis at HighPeriapsisRadius (~200,000 km),
            // apoapsis beyond Earth's SOI. Eccentricity ≈ 0.748 — comfortably under
            // the 0.8 ceiling we hold for Newton-Raphson convergence headroom on
            // the Kepler solver. Vessel at periapsis at epoch.
            double rPeri = HighPeriapsisRadius;
            double rApo = 1.5 * EarthSoiRadiusMeters;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            // Sanity guard (matches the e < 0.8 design constraint).
            VesselTestHelpers.AssertSolvableEccentricity(e);

            var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

            long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                state, _earth, currentTick: 0, TickIntervalSeconds,
                SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

            Assert.IsTrue(crossing.HasValue,
                "Elliptical orbit with apoapsis > SOI should predict outward crossing");

            // Expected: solve cos(ν) = (p/r - 1)/e at r = SoiRadius, take +ν (outbound)
            // since vessel starts at ν=0 (periapsis).
            double p = a * (1.0 - e * e);
            double cosNu = (p / EarthSoiRadiusMeters - 1.0) / e;
            double nu = math.acos(cosNu);
            double M = OrbitalElements.TrueToMeanAnomaly(nu, e);
            double n = OrbitalElements.MeanMotion(a, EarthMu);
            double tSeconds = M / n;
            long expectedTick = (long)math.ceil(tSeconds / TickIntervalSeconds);

            Assert.AreEqual(expectedTick, crossing.Value, 2,
                $"Elliptical outward crossing should land at ~{expectedTick} ticks");
        }

        // ----- Test 4: circular orbit at exactly SOI radius -----

        [Test]
        public void PredictNextCrossing_EllipticalCircularAtSoiRadius_NoCrossing()
        {
            // Circular orbit at exactly SOI radius. Without the boundary-hug tolerance,
            // floating-point rounding could falsely report a crossing every period.
            // BoundaryHugTolerance (1.0 m) catches this case.
            var state = BuildState(EarthSoiRadiusMeters, eccentricity: 0.0, trueAnomalyAtEpoch: 0.0);

            long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                state, _earth, currentTick: 0, TickIntervalSeconds,
                SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

            Assert.IsNull(crossing,
                "Circular orbit at exactly SOI radius should not produce a false-positive crossing");
        }

        // ----- Test 5: single child SOI, vessel orbit passes through it -----

        [Test]
        public void PredictNextCrossing_VesselApproachingChildSoi_InwardCrossingPredicted()
        {
            // Single child body (Moon) at APOAPSIS location of the vessel's orbit.
            // With ω=0 and ν₀=0, vessel periapsis = +X, vessel apoapsis = -X. So Moon
            // is placed at -X (at apoapsis distance) to sit on the vessel's orbital path.
            // The orbit passes through the Moon's SOI as it approaches apoapsis.
            // Periapsis: HighPeriapsisRadius (~200,000 km). Apoapsis: EarthMoonDistance.
            // Eccentricity ≈ 0.316 — well within solver-comfortable range.
            double rPeri = HighPeriapsisRadius;
            double rApo = EarthMoonDistanceMeters;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            VesselTestHelpers.AssertSolvableEccentricity(e);

            // Moon placed at (-rApo, 0, 0) — vessel's apoapsis location.
            var moon = BuildChildBody(
                "Moon",
                new double3(-EarthMoonDistanceMeters, 0, 0),
                MoonMassKg, MoonSoiRadiusMeters);
            try
            {
                var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

                long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                    state, _earth, currentTick: 0, TickIntervalSeconds,
                    SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

                Assert.IsTrue(crossing.HasValue,
                    "Vessel orbit reaching Moon's SOI at apoapsis should predict an inward crossing");
                Assert.Greater(crossing.Value, 0, "Crossing tick should be in the future");

                // Sanity: the crossing should land somewhere between zero and one full
                // orbital period. (Specifically, near half-period since Moon sits at
                // apoapsis; vessel reaches Moon's SOI somewhat before apoapsis.)
                double T = 2.0 * math.PI_DBL * math.sqrt((a * a * a) / EarthMu);
                long periodTicks = (long)math.ceil(T / TickIntervalSeconds);
                Assert.LessOrEqual(crossing.Value, periodTicks,
                    "Inward crossing should fire within one orbital period");
            }
            finally
            {
                if (moon != null && moon.gameObject != null)
                    UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        // ----- Test 6: child body present, vessel never approaches -----

        [Test]
        public void PredictNextCrossing_VesselNeverEntersChildSoi_ReturnsNullFromInward()
        {
            // Moon at +X offset. Vessel in low circular orbit around Earth — orbit
            // never reaches Moon's distance. Should return null (no outward, no inward).
            var moon = BuildChildBody(
                "Moon",
                new double3(EarthMoonDistanceMeters, 0, 0),
                MoonMassKg, MoonSoiRadiusMeters);
            try
            {
                var state = BuildState(LeoRadius, eccentricity: 0.0, trueAnomalyAtEpoch: 0.0);

                long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                    state, _earth, currentTick: 0, TickIntervalSeconds,
                    SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

                Assert.IsNull(crossing,
                    "LEO orbit can't reach Moon's SOI; no outward + no inward = null");
            }
            finally
            {
                if (moon != null && moon.gameObject != null)
                    UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        // ----- Test 7: multiple child SOIs, earliest wins -----

        [Test]
        public void PredictNextCrossing_MultipleChildSois_ReturnsEarliest()
        {
            // Two child bodies along the vessel's orbital sweep, parameterized by
            // true anomaly ν rather than by raw +X/-X axis placement. The vessel
            // (ω=0, ν₀=0) sweeps from periapsis at +X (ν=0) through +Y (ν=π/2)
            // toward apoapsis at -X (ν=π). Children placed at the orbit positions
            // for two distinct ν values; the vessel reaches the smaller-ν child
            // first in time.
            //
            // To discriminate earliest-by-tick (correct predictor semantics) from
            // first-match-by-registry-order (driver semantics, would be a bug
            // here): register FarChild (larger ν) FIRST, then NearChild (smaller ν).
            // If the predictor returns NearChild's crossing tick, it correctly
            // chose earliest-by-tick over registry order.
            //
            // Orbit: rPeri = HighPeriapsisRadius, rApo = 3·rPeri (e = 0.5 — well
            // under 0.8). Earth SOI 9.24e8 > rApo so no outward crossing; only
            // inward candidates can fire.
            double rPeri = HighPeriapsisRadius;
            double rApo = 3.0 * HighPeriapsisRadius;  // 6e8 m
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            VesselTestHelpers.AssertSolvableEccentricity(e);
            double p = a * (1.0 - e * e);

            // Compute orbit positions at two ν values along the sweep.
            double nuNear = 0.5 * math.PI_DBL;          // π/2 — quarter-way around
            double nuFar  = 2.0 * math.PI_DBL / 3.0;    // 2π/3 — further along

            double rAtNear = p / (1.0 + e * math.cos(nuNear));
            double rAtFar  = p / (1.0 + e * math.cos(nuFar));
            double3 nearPos = new double3(rAtNear * math.cos(nuNear), rAtNear * math.sin(nuNear), 0.0);
            double3 farPos  = new double3(rAtFar  * math.cos(nuFar),  rAtFar  * math.sin(nuFar),  0.0);

            double childSoiR = 5.0e7;  // 50,000 km SOI — small enough that vessel must pass through location

            // Register FarChild FIRST so registry order ≠ earliest-by-tick order.
            var farChild = BuildChildBody("FarChild", farPos, MoonMassKg, childSoiR);
            ReferenceBody nearChild = null;
            try
            {
                nearChild = BuildChildBody("NearChild", nearPos, MoonMassKg, childSoiR);

                var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

                long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                    state, _earth, currentTick: 0, TickIntervalSeconds,
                    SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

                Assert.IsTrue(crossing.HasValue,
                    "Vessel reaching either child should produce a crossing");

                // Vessel-at-child-center ticks (when vessel reaches each child's exact
                // position along the orbit). These are NOT what the predictor returns —
                // the predictor returns the SOI-entry tick, which fires EARLIER (vessel
                // crosses the SOI sphere before reaching the center). We use these as
                // reference points for relative comparison only.
                double nMM = OrbitalElements.MeanMotion(a, EarthMu);
                double mNear = OrbitalElements.TrueToMeanAnomaly(nuNear, e);
                double mFar  = OrbitalElements.TrueToMeanAnomaly(nuFar,  e);
                long nearCenterArrivalTick = (long)math.ceil((mNear / nMM) / TickIntervalSeconds);
                long farCenterArrivalTick  = (long)math.ceil((mFar  / nMM) / TickIntervalSeconds);

                // Assertion 1: crossing fires BEFORE vessel reaches NearChild center.
                // SOI-entry happens when vessel crosses the 5e7 m sphere boundary,
                // which is upstream of the center along the trajectory.
                Assert.Less(crossing.Value, nearCenterArrivalTick,
                    $"Crossing should fire BEFORE vessel reaches NearChild center " +
                    $"({nearCenterArrivalTick}); SOI entry precedes center arrival. " +
                    $"Got {crossing.Value}.");

                // Assertion 2: crossing is closer in absolute distance to NearChild's
                // center-arrival than to FarChild's. This is the earliest-wins
                // discriminator — if the predictor had returned FarChild's crossing
                // (first-match-by-registry-order bug, since FarChild was registered
                // first), distToFar would be smaller.
                long distToNear = System.Math.Abs(crossing.Value - nearCenterArrivalTick);
                long distToFar  = System.Math.Abs(crossing.Value - farCenterArrivalTick);
                Assert.Less(distToNear, distToFar,
                    $"Crossing tick {crossing.Value} should be closer to NearChild's " +
                    $"center-arrival ({nearCenterArrivalTick}) than to FarChild's " +
                    $"({farCenterArrivalTick}), confirming earliest-by-tick semantics " +
                    $"over first-match-by-registry-order. distToNear={distToNear}, " +
                    $"distToFar={distToFar}.");
            }
            finally
            {
                if (nearChild != null && nearChild.gameObject != null)
                    UnityObject.DestroyImmediate(nearChild.gameObject);
                if (farChild != null && farChild.gameObject != null)
                    UnityObject.DestroyImmediate(farChild.gameObject);
            }
        }

        // ----- Test 8: outward and inward both apply, earliest wins -----

        [Test]
        public void PredictNextCrossing_OutwardAndInwardBothApply_ReturnsEarliest()
        {
            // Vessel on elliptical orbit whose apoapsis exceeds Earth's SOI radius
            // (outward crossing exists), AND a child body sits along the orbital path
            // BEFORE the outward crossing point. Both candidates are valid; predictor
            // must return the EARLIEST.
            //
            // Orbit geometry: periapsis at +X axis (ν=0), apoapsis at -X axis (ν=π).
            // The orbit sweeps through +Y midway. Place the child along the orbital
            // path at a point reached BEFORE the vessel crosses Earth's SOI outward.
            //
            // For an orbit with rPeri = LeoRadius, rApo = 1.5 × EarthSoi: the vessel
            // reaches r = EarthSoi at ν = acos((p/EarthSoi - 1) / e). Place the child
            // at an interior r-value reached EARLIER in ν (smaller ν → reached sooner).
            // rPeri = HighPeriapsisRadius (200,000 km) + rApo = 1.5 × EarthSOI gives
            // eccentricity ≈ 0.748 — under the 0.8 ceiling for solver stability.
            double rPeri = HighPeriapsisRadius;
            double rApo = 1.5 * EarthSoiRadiusMeters;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            VesselTestHelpers.AssertSolvableEccentricity(e);
            double p = a * (1.0 - e * e);

            // Earth-SOI-exit ν:
            double cosNuExit = (p / EarthSoiRadiusMeters - 1.0) / e;
            double nuExit = math.acos(cosNuExit);

            // Pick a child ν earlier in the sweep (smaller ν → reached before nuExit).
            double nuChild = 0.5 * nuExit;
            double rAtNuChild = p / (1.0 + e * math.cos(nuChild));
            double3 childPos = new double3(
                rAtNuChild * math.cos(nuChild),
                rAtNuChild * math.sin(nuChild),
                0.0);

            double childSoi = 5.0e7;  // 50,000 km SOI for the child
            var child = BuildChildBody(
                "InterceptingChild", childPos, MoonMassKg, childSoi);
            try
            {
                var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

                long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                    state, _earth, currentTick: 0, TickIntervalSeconds,
                    SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

                Assert.IsTrue(crossing.HasValue, "Both outward and inward should produce candidates");

                // Compute outward crossing tick analytically (vessel exits Earth SOI at nuExit).
                double mOut = OrbitalElements.TrueToMeanAnomaly(nuExit, e);
                double nMM = OrbitalElements.MeanMotion(a, EarthMu);
                long expectedOutwardTick = (long)math.ceil((mOut / nMM) / TickIntervalSeconds);

                // The returned crossing should be EARLIER than the outward tick
                // (inward intercepts before vessel reaches Earth-SOI exit).
                Assert.Less(crossing.Value, expectedOutwardTick,
                    $"Inward intercept should fire before outward at tick {expectedOutwardTick}, " +
                    $"got {crossing.Value}");
            }
            finally
            {
                if (child != null && child.gameObject != null)
                    UnityObject.DestroyImmediate(child.gameObject);
            }
        }

        // ----- Test 9: vessel already at boundary, no false positive -----

        [Test]
        public void PredictNextCrossing_VesselAlreadyAtCrossingBoundary_NoFalsePositive()
        {
            // Vessel currently at exactly SOI radius (ν=0 at periapsis, rPeri = SoiRadius
            // exactly). Boundary-hug tolerance + the periapsis/apoapsis bracket logic
            // should prevent a false positive at currentTick. The orbit dips slightly
            // inside the SOI on its way to apoapsis (apoapsis < SOI), so no outward
            // crossing exists.
            double rPeri = EarthSoiRadiusMeters;
            double rApo = 0.999 * EarthSoiRadiusMeters;  // apoapsis JUST below SOI
            // Note: rApo < rPeri is invalid for an orbit; flip so rPeri < rApo.
            // Reframe: orbit with rPeri = 0.999 SOI, rApo = 1.0 SOI exactly.
            rPeri = 0.999 * EarthSoiRadiusMeters;
            rApo = EarthSoiRadiusMeters;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);

            // Vessel at apoapsis (ν=π) where r = rApo = SOI exactly.
            var state = BuildState(a, e, trueAnomalyAtEpoch: math.PI_DBL);

            long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                state, _earth, currentTick: 0, TickIntervalSeconds,
                SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

            // The orbit goes from apoapsis=SOI back down to periapsis=0.999·SOI,
            // never strictly crossing outward (it just touches at apoapsis).
            // If a crossing IS reported, it must be in the FUTURE, not at currentTick=0.
            if (crossing.HasValue)
            {
                Assert.Greater(crossing.Value, 0,
                    "Any reported crossing must be strictly in the future, not at currentTick");
            }
            // Either null (boundary-hug guard caught it) or future-tick — both acceptable.
        }

        // ----- Test 10: near-tangent graze missed under pragmatic -----

        [Test]
        public void PredictNextCrossing_NearTangentGraze_MissesUnderPragmatic()
        {
            // Construct a child body such that the vessel's orbit grazes its SOI
            // boundary very briefly — entry and exit within one coarse sample interval.
            // Under Pragmatic detection, the predictor should miss this (the two
            // bracket samples both read positive). This locks in the documented
            // trade per CONSTRAINTS §1701.
            //
            // Approach: place a child near the vessel's orbital path with a very small
            // SOI radius such that the vessel sweeps past it within < CoarseSampleTicks
            // ticks of physical time. At LEO speed ~7,500 m/s and a 100-tick sample
            // interval (3.33 s), a child SOI of ~3-5 km would be traversed in well
            // under one sample.
            //
            // Place child at (LeoRadius, 0, 0) with tiny SOI; vessel in low circular
            // orbit at LeoRadius — the vessel passes through the child's center every
            // period, so the SOI is grazed every period. Tiny SOI means the traversal
            // time is much less than one coarse sample.
            //
            // Note: at LeoRadius the vessel's position oscillates between exactly +X
            // and elsewhere. The graze occurs at ν=0 (periapsis position) once per
            // period.
            double tinySoi = 5000.0;  // 5 km — much smaller than 7500 m/s × 3.33 s = 25 km
            var child = BuildChildBody(
                "TinyGrazerChild",
                new double3(LeoRadius, 0, 0),
                100.0,  // negligible mass — doesn't matter for the predictor
                tinySoi);
            try
            {
                // Vessel at ν=π (opposite side of orbit from child) at epoch — so the
                // first graze happens half a period later. This gives a clean
                // "negative-positive-...-positive" sampling pattern: vessel starts far
                // from child, swings around, grazes briefly, swings away.
                //
                // At ν=π in a circular orbit, vessel position is at (-LeoRadius, 0, 0).
                // Distance to child = 2 × LeoRadius = 1.4e7 m. Well outside 5 km SOI.
                // Discriminator at currentTick = positive.
                var state = BuildState(LeoRadius, eccentricity: 0.0, trueAnomalyAtEpoch: math.PI_DBL);

                long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                    state, _earth, currentTick: 0, TickIntervalSeconds,
                    SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

                // Under Pragmatic: the graze should be missed. The acceptance criterion
                // is that no crossing is reported (or any reported crossing is the
                // outward Earth-SOI exit, which doesn't apply here since rApo = LEO <
                // EarthSOI — so null is the only valid result).
                Assert.IsNull(crossing,
                    "Pragmatic detection should miss near-tangent grazes inside one coarse sample. " +
                    "Driver at-evaluation-time check is the safety net.");
            }
            finally
            {
                if (child != null && child.gameObject != null)
                    UnityObject.DestroyImmediate(child.gameObject);
            }
        }

        // ----- Test 11: hyperbolic that escapes after horizon -----

        [Test]
        public void PredictNextCrossing_HyperbolicNoCrossingWithinHorizon_ReturnsNull()
        {
            // Hyperbolic orbit with such a far periapsis that the outward SOI crossing
            // happens after the lookahead horizon. Build a hyperbolic orbit around a
            // body with a SOI so vast that the crossing tick exceeds
            // PredictorMaxLookaheadTicks. Use the outward-overflow-defense path —
            // SecondsToAbsoluteTick returns null when ticks > long.MaxValue/2.
            //
            // Tactic: very small μ (so n is tiny → seconds-per-mean-anomaly-unit huge),
            // and finite SOI that requires a large ν change to reach. Use a custom
            // "FarBody" with tiny mass and small finite SOI but very high a.
            var farBodyGo = new GameObject("FarBody");
            var farBody = farBodyGo.AddComponent<ReferenceBody>();
            farBody.InitializeBodyForTesting(
                massKg: 1.0,            // 1 kg — μ ~ 6.67e-11
                soiRadiusMeters: 1.0e12);  // 1 trillion meters — vast SOI
            try
            {
                // Hyperbolic at huge scale. Periapsis at 1e11 m (10× Earth-Sun distance).
                double rPeri = 1.0e11;
                double e = 1.5;
                double a = rPeri / (1.0 - e);  // negative
                var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

                long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                    state, farBody, currentTick: 0, TickIntervalSeconds,
                    SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

                // With μ ~ 6.67e-11 and a ~ 2e11, mean motion n is astronomically small.
                // Time to reach SOI from periapsis is far longer than long.MaxValue/2 ticks.
                // The overflow-defense path returns null.
                Assert.IsNull(crossing,
                    "Hyperbolic with crossing far past the lookahead horizon should return null");
            }
            finally
            {
                if (farBodyGo != null) UnityObject.DestroyImmediate(farBodyGo);
            }
        }

        // ----- Test 12: very-elliptical long period -----

        [Test]
        public void PredictNextCrossing_VeryEllipticalLongPeriod_ReturnsCrossingOrNull()
        {
            // Long-period orbit around a small body (tiny μ) — the period exceeds
            // PredictorMaxLookaheadTicks so the inward-search horizon clamp engages.
            // The orbit's eccentricity stays under 0.8 for Kepler-solver stability;
            // "long period" comes from tiny μ, not from extreme eccentricity.
            //
            // Body: 1 kg mass (μ ~ 6.67e-11), SOI = 1e12 m. No children registered.
            // Orbit: rPeri = 2e8, rApo = 5e8 → e ≈ 0.375. Orbit is fully inside the
            // body's vast SOI (no outward crossing); no children (no inward). Result
            // should be null — but the horizon-computation path still exercises the
            // clamp logic without crashing.
            var smallBodyGo = new GameObject("SmallBody");
            var smallBody = smallBodyGo.AddComponent<ReferenceBody>();
            smallBody.InitializeBodyForTesting(
                massKg: 1.0,                // 1 kg — μ ~ 6.67e-11
                soiRadiusMeters: 1.0e12);   // 1 trillion meters
            try
            {
                double rPeri = 2.0e8;
                double rApo = 5.0e8;
                double a = 0.5 * (rPeri + rApo);
                double e = (rApo - rPeri) / (rApo + rPeri);
                VesselTestHelpers.AssertSolvableEccentricity(e);
                var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

                long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                    state, smallBody, currentTick: 0, TickIntervalSeconds,
                    SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

                // Orbit fully inside SOI + no children + horizon clamp = null result.
                // The point of this test is "no crash on very-long-period configurations";
                // any reported crossing would have to respect the overflow defense.
                if (crossing.HasValue)
                {
                    Assert.Greater(crossing.Value, 0,
                        "Any reported crossing must be in the future");
                    Assert.LessOrEqual(crossing.Value, long.MaxValue / 2,
                        "Reported crossing must respect the overflow defense");
                }
            }
            finally
            {
                if (smallBodyGo != null) UnityObject.DestroyImmediate(smallBodyGo);
            }
        }

        // ----- Test 13: defensive null kepler state -----

        [Test]
        public void PredictNextCrossing_NullKeplerState_ReturnsNull()
        {
            long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                state: null, _earth, currentTick: 0, TickIntervalSeconds,
                SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

            Assert.IsNull(crossing, "Null KeplerState should return null defensively");
        }

        // ----- Test 14: defensive null current body -----

        [Test]
        public void PredictNextCrossing_NullCurrentBody_ReturnsNull()
        {
            var state = BuildState(LeoRadius, eccentricity: 0.0, trueAnomalyAtEpoch: 0.0);

            long? crossing = SoiCrossingPredictor.PredictNextCrossing(
                state, currentBody: null, currentTick: 0, TickIntervalSeconds,
                SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

            Assert.IsNull(crossing, "Null current body should return null defensively");
        }
    }
}
