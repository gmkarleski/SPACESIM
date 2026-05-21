using System;
using System.Reflection;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.Vessels;
using Unity.Mathematics;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SurfaceImpactPredictor"/>. The predictor is a thin
    /// wrapper around <see cref="OrbitalElements.SolveConicAtRadius"/> with the
    /// threshold radius set to <c>SurfaceRadiusMeters</c>. Tests verify the wrapper
    /// logic: orbit-vs-surface geometry, hyperbolic trajectories, defensive null
    /// handling.
    ///
    /// Test geometry pattern: Earth-like body with <c>SurfaceRadiusMeters = 6.371e6</c>.
    /// Orbits with periapsis below 6.371e6 m intersect the surface; orbits with
    /// periapsis above don't impact.
    /// </summary>
    public class SurfaceImpactPredictorTests
    {
        private const double EarthMassKg = 5.972e24;
        private static readonly double EarthMu = CoordinateMath.G * EarthMassKg;
        private const double EarthSurfaceRadiusMeters = 6.371e6;
        private const double TickIntervalSeconds = 1.0 / 30.0;

        private GameObject _bodyGo;
        private ReferenceBody _body;

        [SetUp]
        public void SetUp()
        {
            BodyRegistry.ClearForTesting();

            _bodyGo = new GameObject("Earth");
            _body = _bodyGo.AddComponent<ReferenceBody>();
            // Surface radius is Earth-default 6.371e6 from the SerializeField default
            // (no need to set unless a test wants a different value). atmospheric top
            // doesn't matter for surface impact.
            _body.InitializeBodyForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bodyGo != null) UnityObject.DestroyImmediate(_bodyGo);
            BodyRegistry.ClearForTesting();
        }

        // ----- Helpers -----

        // Parameter names (semiMajorAxis, eccentricity, trueAnomalyAtEpoch) match the
        // convention established in SoiCrossingPredictorTests and OrbitalElementsTests
        // so cross-file named-argument call sites are consistent. Each test file owns
        // its own BuildState helper (matching their local SetUp body wiring); the
        // signature shape is shared across files.
        private static KeplerState BuildState(
            double semiMajorAxis, double eccentricity, double trueAnomalyAtEpoch)
        {
            return new KeplerState
            {
                SemiMajorAxis = semiMajorAxis,
                Eccentricity = eccentricity,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = trueAnomalyAtEpoch,
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };
        }

        // ----- Tests -----

        [Test]
        public void PredictNextImpact_OrbitAboveSurface_ReturnsNull()
        {
            // Orbit's periapsis is above the surface — vessel never impacts.
            double rPeri = EarthSurfaceRadiusMeters + 1.0e5;  // 100 km altitude
            double rApo = 1.0e7;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            Assert.Less(e, 0.8, "Test orbit eccentricity must stay below 0.8");

            var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

            long? impact = SurfaceImpactPredictor.PredictNextImpact(
                state, _body, currentTick: 0, TickIntervalSeconds);

            Assert.IsNull(impact,
                "Orbit with periapsis above surface should produce no impact event");
        }

        [Test]
        public void PredictNextImpact_OrbitImpactsSurface_ReturnsValidFutureTick()
        {
            // Orbit's periapsis is 100 km BELOW the surface. Vessel starts at apoapsis
            // (high above surface); the predicted impact tick is when the descending
            // orbit reaches r = SurfaceRadiusMeters.
            double rPeri = EarthSurfaceRadiusMeters - 1.0e5;  // 100 km below surface
            double rApo = 1.0e7;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            Assert.Less(e, 0.8, "Test orbit eccentricity must stay below 0.8");

            // Vessel at apoapsis — far above surface, descending toward periapsis.
            var state = BuildState(a, e, trueAnomalyAtEpoch: math.PI_DBL);

            long? impact = SurfaceImpactPredictor.PredictNextImpact(
                state, _body, currentTick: 0, TickIntervalSeconds);

            Assert.IsTrue(impact.HasValue,
                "Orbit intersecting surface should predict an impact event");
            Assert.Greater(impact.Value, 0,
                "Impact tick should be in the future");

            // Sanity: impact should happen before vessel reaches periapsis (vessel
            // hits the surface on the descending leg, before reaching the lowest point
            // of its underground theoretical periapsis).
            double T = 2.0 * math.PI_DBL * math.sqrt((a * a * a) / EarthMu);
            long halfPeriodTicks = (long)math.ceil((T / 2.0) / TickIntervalSeconds);
            Assert.Less(impact.Value, halfPeriodTicks,
                "Impact should fire before vessel reaches theoretical periapsis");
        }

        [Test]
        public void PredictNextImpact_HyperbolicImpact_ReturnsFutureTick()
        {
            // Hyperbolic trajectory with periapsis BELOW the surface — vessel on an
            // impact-bound flyby. Vessel currently above surface at ν=-0.8 (inbound,
            // pre-periapsis). Expected: future impact tick before vessel reaches
            // periapsis.
            double rPeri = EarthSurfaceRadiusMeters - 1.0e5;  // 100 km below surface
            double e = 1.3;
            double a = rPeri / (1.0 - e);  // negative for hyperbolic

            // Verify vessel at ν=-0.8 is currently above surface.
            double p = a * (1.0 - e * e);
            double rNow = p / (1.0 + e * math.cos(-0.8));
            Assert.Greater(rNow, EarthSurfaceRadiusMeters,
                $"Vessel at ν=-0.8 should be above surface; rNow={rNow:E3}");

            var state = BuildState(a, e, trueAnomalyAtEpoch: -0.8);

            long? impact = SurfaceImpactPredictor.PredictNextImpact(
                state, _body, currentTick: 0, TickIntervalSeconds);

            Assert.IsTrue(impact.HasValue,
                "Pre-periapsis hyperbolic vessel above surface (impact-bound) should predict impact");
            Assert.Greater(impact.Value, 0, "Impact tick should be in the future");
        }

        [Test]
        public void PredictNextImpact_NullState_ReturnsNull()
        {
            long? impact = SurfaceImpactPredictor.PredictNextImpact(
                state: null, _body, currentTick: 0, TickIntervalSeconds);

            Assert.IsNull(impact, "Null KeplerState should return null defensively");
        }

        [Test]
        public void PredictNextImpact_NullBody_ReturnsNull()
        {
            var state = BuildState(7.0e6, eccentricity: 0.0, trueAnomalyAtEpoch: 0.0);

            long? impact = SurfaceImpactPredictor.PredictNextImpact(
                state, currentBody: null, currentTick: 0, TickIntervalSeconds);

            Assert.IsNull(impact, "Null currentBody should return null defensively");
        }
    }
}
