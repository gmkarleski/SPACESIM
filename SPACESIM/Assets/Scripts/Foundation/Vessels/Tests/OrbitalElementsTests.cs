using System;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.Vessels;
using Unity.Mathematics;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="OrbitalElements"/>. Pure-math tests — no Unity
    /// dependencies beyond Unity.Mathematics types. Test bodies use Earth-equivalent
    /// μ = G · 5.972e24 unless otherwise noted.
    ///
    /// Tolerance conventions:
    ///   - Angles (radians): 1e-9 absolute. Tighter than this is unreliable on doubles
    ///     when the angle is computed via acos of a near-±1 cosine.
    ///   - Distances (meters): 1e-3 absolute, or 1e-9 relative. Whichever is looser.
    ///     LEO-scale radii are ~6.7e6 m; this gives ~7 significant figures.
    ///   - Velocities (m/s): 1e-6 absolute, or 1e-9 relative.
    ///
    /// The round-trip test uses tighter tolerances (1e-7 relative) because that path
    /// composes both directions and any numerical artifact compounds.
    /// </summary>
    public class OrbitalElementsTests
    {
        // Earth-equivalent gravitational parameter for repeatable orbital scales.
        private const double EarthMassKg = PhysicsConstants.EarthMassKg;
        private static readonly double EarthMu = PhysicsConstants.EarthMu;

        // Test radius: 7000 km, roughly LEO. Circular velocity at this radius is
        // sqrt(μ/r) ≈ 7546 m/s. Periapsis distance for elliptical tests uses this.
        private const double LeoRadius = 7_000_000.0;

        private static readonly Guid TestBodyId = Guid.NewGuid();
        private const long TestEpochTick = 100;

        // ----- Helpers -----

        private static void AssertNear(double expected, double actual, double tolerance, string label)
        {
            Assert.AreEqual(expected, actual, tolerance,
                $"{label}: expected {expected:G17}, got {actual:G17}, diff {math.abs(expected - actual):G17}");
        }

        private static void AssertVectorNear(double3 expected, double3 actual, double tolerance, string label)
        {
            AssertNear(expected.x, actual.x, tolerance, label + ".x");
            AssertNear(expected.y, actual.y, tolerance, label + ".y");
            AssertNear(expected.z, actual.z, tolerance, label + ".z");
        }

        // ----- Circular equatorial orbit -----

        [Test]
        public void ComputeFromStateVector_CircularEquatorialOrbit_HasZeroEAndZeroI()
        {
            // Vessel at (r, 0, 0), velocity tangent in +Y at sqrt(μ/r): perfect circle in
            // the equatorial plane.
            double3 r = new double3(LeoRadius, 0.0, 0.0);
            double vCircular = math.sqrt(EarthMu / LeoRadius);
            double3 v = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                r, v, EarthMu, TestEpochTick, TestBodyId);

            AssertNear(LeoRadius, state.SemiMajorAxis, 1e-3, "semi-major axis");
            AssertNear(0.0, state.Eccentricity, 1e-12, "eccentricity");
            AssertNear(0.0, state.Inclination, 1e-12, "inclination");
            // Circular equatorial: Ω = 0, ω = 0 by convention; ν₀ = true longitude.
            // Position is at +X, so true longitude = 0.
            AssertNear(0.0, state.LongitudeOfAscendingNode, 1e-12, "longitude of ascending node");
            AssertNear(0.0, state.ArgumentOfPeriapsis, 1e-12, "argument of periapsis");
            AssertNear(0.0, state.TrueAnomalyAtEpoch, 1e-9, "true anomaly at epoch");
            Assert.AreEqual(TestEpochTick, state.EpochTick);
            Assert.AreEqual(TestBodyId, state.ReferenceBodyId);
        }

        // ----- Elliptical equatorial orbit (apsides locked) -----

        [Test]
        public void ComputeFromStateVector_EllipticalOrbit_ApsidesMatchExpected()
        {
            // Construct an ellipse with periapsis at r_p = 7000 km and apoapsis at
            // r_a = 14000 km. Semi-major axis a = (r_p + r_a)/2 = 10500 km. Eccentricity
            // e = (r_a - r_p)/(r_a + r_p) = 1/3.
            //
            // At periapsis (perihelion-equivalent), position = (r_p, 0, 0), velocity is
            // purely tangential in +Y at v_p = sqrt(μ · (2/r_p - 1/a)) (vis-viva equation).
            double rPeriapsis = LeoRadius;             // 7e6 m
            double rApoapsis = 2.0 * LeoRadius;        // 14e6 m
            double aExpected = (rPeriapsis + rApoapsis) / 2.0;
            double eExpected = (rApoapsis - rPeriapsis) / (rApoapsis + rPeriapsis);

            double vPeriapsis = math.sqrt(EarthMu * (2.0 / rPeriapsis - 1.0 / aExpected));
            double3 r = new double3(rPeriapsis, 0.0, 0.0);
            double3 v = new double3(0.0, vPeriapsis, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                r, v, EarthMu, TestEpochTick, TestBodyId);

            AssertNear(aExpected, state.SemiMajorAxis, 1.0, "semi-major axis");
            AssertNear(eExpected, state.Eccentricity, 1e-12, "eccentricity");
            AssertNear(0.0, state.Inclination, 1e-12, "inclination (equatorial)");
            // At periapsis, ν = 0 by definition.
            AssertNear(0.0, state.TrueAnomalyAtEpoch, 1e-9, "true anomaly at epoch (at periapsis)");

            // Cross-check the periapsis/apoapsis helpers.
            AssertNear(rPeriapsis, OrbitalElements.PeriapsisDistance(state.SemiMajorAxis, state.Eccentricity),
                1.0, "periapsis distance helper");
            AssertNear(rApoapsis, OrbitalElements.ApoapsisDistance(state.SemiMajorAxis, state.Eccentricity),
                1.0, "apoapsis distance helper");
        }

        // ----- Inclined orbit -----

        [Test]
        public void ComputeFromStateVector_InclinedCircularOrbit_HasExpectedInclination()
        {
            // Vessel at (r, 0, 0), velocity tilted 45° toward +Z. This produces a circular
            // orbit inclined 45° to the equatorial plane.
            double inclinationExpected = math.PI_DBL / 4.0;  // 45°
            double vCircular = math.sqrt(EarthMu / LeoRadius);

            double3 r = new double3(LeoRadius, 0.0, 0.0);
            double3 v = new double3(0.0,
                vCircular * math.cos(inclinationExpected),
                vCircular * math.sin(inclinationExpected));

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                r, v, EarthMu, TestEpochTick, TestBodyId);

            AssertNear(LeoRadius, state.SemiMajorAxis, 1e-3, "semi-major axis");
            AssertNear(0.0, state.Eccentricity, 1e-12, "eccentricity");
            AssertNear(inclinationExpected, state.Inclination, 1e-9, "inclination");
        }

        // ----- Hyperbolic trajectory (a < 0) -----

        [Test]
        public void ComputeFromStateVector_HyperbolicTrajectory_HasNegativeAAndEGreaterThanOne()
        {
            // Launch from periapsis at r = 7000 km with velocity 1.5× escape velocity.
            // Escape from r is sqrt(2μ/r); 1.5× that puts e well above 1 (hyperbolic).
            double escapeV = math.sqrt(2.0 * EarthMu / LeoRadius);
            double3 r = new double3(LeoRadius, 0.0, 0.0);
            double3 v = new double3(0.0, 1.5 * escapeV, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                r, v, EarthMu, TestEpochTick, TestBodyId);

            Assert.Less(state.SemiMajorAxis, 0.0, "hyperbolic semi-major axis should be negative");
            Assert.Greater(state.Eccentricity, 1.0, "hyperbolic eccentricity should be > 1");
            // Periapsis distance should still be r since we launched from periapsis.
            AssertNear(LeoRadius, OrbitalElements.PeriapsisDistance(state.SemiMajorAxis, state.Eccentricity),
                1.0, "periapsis distance from hyperbolic elements");
            // Apoapsis is +infinity for hyperbolic (no upper bound; unbound trajectory).
            Assert.IsTrue(double.IsPositiveInfinity(
                OrbitalElements.ApoapsisDistance(state.SemiMajorAxis, state.Eccentricity)),
                "hyperbolic apoapsis should be +infinity");
        }

        // ----- Round-trip: state vector → elements → state vector -----

        [Test]
        public void RoundTrip_CircularEquatorial_PreservesStateVector()
        {
            double3 r = new double3(LeoRadius, 0.0, 0.0);
            double vCircular = math.sqrt(EarthMu / LeoRadius);
            double3 v = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                r, v, EarthMu, TestEpochTick, TestBodyId);

            (double3 rOut, double3 vOut) = OrbitalElements.ComputeStateVector(
                state, state.TrueAnomalyAtEpoch, EarthMu);

            AssertVectorNear(r, rOut, math.length(r) * 1e-9, "round-trip position (circular equatorial)");
            AssertVectorNear(v, vOut, math.length(v) * 1e-9, "round-trip velocity (circular equatorial)");
        }

        [Test]
        public void RoundTrip_EllipticalEquatorial_PreservesStateVector()
        {
            // Same elliptical setup as the apsides test, but evaluated at a non-periapsis
            // point along the orbit to exercise both r-magnitude formula and the angle.
            double rPeriapsis = LeoRadius;
            double rApoapsis = 2.0 * LeoRadius;
            double a = (rPeriapsis + rApoapsis) / 2.0;
            double e = (rApoapsis - rPeriapsis) / (rApoapsis + rPeriapsis);

            // Pick a ν = 1.0 radian (~57°). Compute (r, v) at that anomaly via forward
            // direction; the backward direction must recover them.
            KeplerState seedState = new KeplerState
            {
                SemiMajorAxis = a,
                Eccentricity = e,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = 1.0,
                EpochTick = TestEpochTick,
                ReferenceBodyId = TestBodyId,
            };
            (double3 r, double3 v) = OrbitalElements.ComputeStateVector(seedState, 1.0, EarthMu);

            // Re-extract elements from (r, v).
            KeplerState reExtracted = OrbitalElements.ComputeFromStateVector(
                r, v, EarthMu, TestEpochTick, TestBodyId);

            AssertNear(a, reExtracted.SemiMajorAxis, math.abs(a) * 1e-9, "round-trip semi-major axis");
            AssertNear(e, reExtracted.Eccentricity, 1e-9, "round-trip eccentricity");
            AssertNear(0.0, reExtracted.Inclination, 1e-9, "round-trip inclination");
            AssertNear(1.0, reExtracted.TrueAnomalyAtEpoch, 1e-9, "round-trip true anomaly");

            // And forward again should produce the same (r, v).
            (double3 rAgain, double3 vAgain) = OrbitalElements.ComputeStateVector(
                reExtracted, reExtracted.TrueAnomalyAtEpoch, EarthMu);
            AssertVectorNear(r, rAgain, math.length(r) * 1e-9, "round-trip position (elliptical)");
            AssertVectorNear(v, vAgain, math.length(v) * 1e-9, "round-trip velocity (elliptical)");
        }

        [Test]
        public void RoundTrip_Inclined_PreservesStateVector()
        {
            // Inclined elliptical orbit with non-zero Ω and ω.
            KeplerState seedState = new KeplerState
            {
                SemiMajorAxis = 10_500_000.0,
                Eccentricity = 0.3,
                Inclination = math.PI_DBL / 6.0,           // 30°
                LongitudeOfAscendingNode = math.PI_DBL / 4.0,  // 45°
                ArgumentOfPeriapsis = math.PI_DBL / 3.0,   // 60°
                TrueAnomalyAtEpoch = 1.5,                  // ~86°
                EpochTick = TestEpochTick,
                ReferenceBodyId = TestBodyId,
            };
            (double3 r, double3 v) = OrbitalElements.ComputeStateVector(seedState, 1.5, EarthMu);

            KeplerState reExtracted = OrbitalElements.ComputeFromStateVector(
                r, v, EarthMu, TestEpochTick, TestBodyId);

            AssertNear(seedState.SemiMajorAxis, reExtracted.SemiMajorAxis,
                math.abs(seedState.SemiMajorAxis) * 1e-9, "round-trip semi-major axis (inclined)");
            AssertNear(seedState.Eccentricity, reExtracted.Eccentricity, 1e-9, "round-trip eccentricity (inclined)");
            AssertNear(seedState.Inclination, reExtracted.Inclination, 1e-9, "round-trip inclination");
            AssertNear(seedState.LongitudeOfAscendingNode, reExtracted.LongitudeOfAscendingNode,
                1e-9, "round-trip Ω");
            AssertNear(seedState.ArgumentOfPeriapsis, reExtracted.ArgumentOfPeriapsis, 1e-9, "round-trip ω");
            AssertNear(seedState.TrueAnomalyAtEpoch, reExtracted.TrueAnomalyAtEpoch, 1e-9, "round-trip ν₀");
        }

        // ----- Periapsis/Apoapsis helpers -----

        [Test]
        public void PeriapsisDistance_CircularOrbit_EqualsRadius()
        {
            AssertNear(LeoRadius, OrbitalElements.PeriapsisDistance(LeoRadius, 0.0), 1e-9,
                "circular periapsis equals radius");
        }

        [Test]
        public void ApoapsisDistance_HyperbolicTrajectory_IsInfinity()
        {
            Assert.IsTrue(double.IsPositiveInfinity(OrbitalElements.ApoapsisDistance(-LeoRadius, 2.0)),
                "hyperbolic apoapsis should be +infinity");
            Assert.IsTrue(double.IsPositiveInfinity(OrbitalElements.ApoapsisDistance(LeoRadius, 1.0)),
                "parabolic apoapsis should be +infinity");
        }

        // ----- Event-prediction fields are null at construction -----

        /// <summary>
        /// <see cref="OrbitalElements.ComputeFromStateVector"/> returns a freshly-
        /// constructed <see cref="KeplerState"/> with all four event-prediction
        /// fields null. Fields are populated by
        /// <see cref="VesselEventPredictionDriver"/> on the next sim-tick after the
        /// vessel is in KeplerRails mode (NextPeriapsisTick + NextApoapsisTick land
        /// at commit 045; NextSoiTransitionTick lands at commit 046;
        /// NextAtmosphericEntryTick + NextSurfaceImpactTick land at commit 047 /
        /// split into distinct fields at commit 048 Stage 1). At construction
        /// time — the moment this test exercises — all five are null.
        ///
        /// Renamed at commit 045 Stage 3 from "AreNull" to
        /// "AreNullAtConstruction" to make the timing explicit.
        /// </summary>
        [Test]
        public void ComputeFromStateVector_EventPredictionFields_AreNullAtConstruction()
        {
            double3 r = new double3(LeoRadius, 0.0, 0.0);
            double vCircular = math.sqrt(EarthMu / LeoRadius);
            double3 v = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                r, v, EarthMu, TestEpochTick, TestBodyId);

            Assert.IsNull(state.NextPeriapsisTick,
                "Construction returns null; populated on next TickAdvanced by event predictor (commit 045+)");
            Assert.IsNull(state.NextApoapsisTick);
            Assert.IsNull(state.NextSoiTransitionTick);
            Assert.IsNull(state.NextAtmosphericEntryTick);
            Assert.IsNull(state.NextSurfaceImpactTick);
        }

        // ----- ReRootStateVector (commit 044 Stage 2) -----
        //
        // Test constants for an Earth-Moon two-body configuration. Both bodies are
        // stationary in Phase 1 (matches the helper's velocity-unchanged assumption).

        // Earth-Moon distance: 384,400 km = 3.844e8 m.
        private const double EarthMoonDistanceMeters = 3.844e8;

        // Moon mass and μ. Real Moon: 7.342e22 kg.
        private const double MoonMassKg = 7.342e22;
        private static readonly double MoonMu = CoordinateMath.G * MoonMassKg;

        // Moon SOI radius for reference (Laplace sphere ≈ 6.6e7 m); used in test 2 to
        // place the vessel just beyond Moon's SOI but well within Earth's frame of
        // gravitational dominance.
        private const double MoonSoiRadiusMeters = 6.6e7;

        // Body Guids for the test scene. Constants so tests can verify the helper
        // correctly assigns NewBodyId to the output KeplerState.
        private static readonly Guid EarthBodyId = Guid.NewGuid();
        private static readonly Guid MoonBodyId = Guid.NewGuid();

        [Test]
        public void ReRootStateVector_VesselAtNewBodyOrigin_PreservesIdentityFields()
        {
            // Sanity test: position the vessel exactly at the new body's world position.
            // After re-rooting, the relative position in the new frame should be zero.
            // This exercises only the position-arithmetic; ComputeFromStateVector with
            // a zero position vector won't produce well-defined orbital elements (the
            // r×v cross product is zero), but the position-arithmetic itself must
            // produce zero correctly.
            //
            // We set up vessel-at-Moon-origin (relative to Earth, this means the
            // vessel's relative position equals the Earth-Moon offset). Re-rooting to
            // the Moon frame should give a relative position of (0, 0, 0).
            //
            // Note: we don't assert on the returned KeplerState's orbital elements
            // because ComputeFromStateVector at r=0 is mathematically degenerate. This
            // test specifically validates the relative-position arithmetic.

            // Vessel at Moon position (relative to Earth at origin: r = Earth-Moon offset).
            double3 vesselPosRelativeToEarth = new double3(EarthMoonDistanceMeters, 0, 0);
            // Velocity doesn't matter for this position-arithmetic check; small nonzero
            // to avoid r-v degeneracy if ComputeFromStateVector cares.
            double3 vesselVel = new double3(0, 1000.0, 0);

            WorldPosition earthPosWorld = new WorldPosition(0, 0, 0);
            WorldPosition moonPosWorld = new WorldPosition(EarthMoonDistanceMeters, 0, 0);

            KeplerState result = OrbitalElements.ReRootStateVector(
                currentPositionRelativeToCurrentBody: vesselPosRelativeToEarth,
                currentVelocity: vesselVel,
                currentBodyPositionWorld: earthPosWorld,
                newBodyPositionWorld: moonPosWorld,
                newBodyMu: MoonMu,
                epochTick: TestEpochTick,
                newBodyId: MoonBodyId);

            // The relative-position arithmetic is what we're validating. We can't read
            // the relative position back from KeplerState directly, but we can
            // reconstruct it via ComputeStateVector(state, ν₀, μ) — that should give
            // r = 0 (or as close as the orbital-elements round-trip allows from a
            // degenerate input).
            //
            // Actually: ComputeFromStateVector at r=0 produces a near-degenerate
            // KeplerState; ComputeStateVector won't cleanly invert it. Instead we
            // assert the structural properties: ReferenceBodyId and EpochTick survived,
            // which proves the helper called through correctly.
            Assert.AreEqual(MoonBodyId, result.ReferenceBodyId,
                "Re-rooted state should have the new body's Guid");
            Assert.AreEqual(TestEpochTick, result.EpochTick,
                "Epoch tick should be preserved through re-rooting");
        }

        [Test]
        public void ReRootStateVector_VesselOutsideCurrentBodySoi_RerootsToParent()
        {
            // Vessel positioned beyond Moon's SOI but well inside the Earth-Moon system.
            // Vessel is at (Earth-Moon distance / 2, 0, 0) — halfway between Earth and
            // Moon along the x-axis. This is well outside the Moon's SOI (~6.6e7 m)
            // because the vessel is ~1.9e8 m from the Moon.
            //
            // Currently in Moon's frame: vessel position relative to Moon is
            // (Earth-Moon/2 - Earth-Moon, 0, 0) = (-Earth-Moon/2, 0, 0).
            // Re-root to Earth frame: vessel position should become
            // (Earth-Moon/2, 0, 0).
            double halfDistance = EarthMoonDistanceMeters / 2.0;

            double3 vesselPosRelativeToMoon = new double3(-halfDistance, 0, 0);
            // Vessel velocity ~circular at this Earth-frame radius.
            double vCircularEarthFrame = math.sqrt(EarthMu / halfDistance);
            double3 vesselVel = new double3(0, vCircularEarthFrame, 0);

            WorldPosition moonPosWorld = new WorldPosition(EarthMoonDistanceMeters, 0, 0);
            WorldPosition earthPosWorld = new WorldPosition(0, 0, 0);

            KeplerState earthFrameState = OrbitalElements.ReRootStateVector(
                currentPositionRelativeToCurrentBody: vesselPosRelativeToMoon,
                currentVelocity: vesselVel,
                currentBodyPositionWorld: moonPosWorld,
                newBodyPositionWorld: earthPosWorld,
                newBodyMu: EarthMu,
                epochTick: TestEpochTick,
                newBodyId: EarthBodyId);

            // Compute the Earth-frame KeplerState directly for comparison.
            double3 vesselPosRelativeToEarth = new double3(halfDistance, 0, 0);
            KeplerState directEarthFrameState = OrbitalElements.ComputeFromStateVector(
                vesselPosRelativeToEarth, vesselVel, EarthMu, TestEpochTick, EarthBodyId);

            Assert.AreEqual(EarthBodyId, earthFrameState.ReferenceBodyId);
            AssertNear(directEarthFrameState.SemiMajorAxis, earthFrameState.SemiMajorAxis, 1e-3,
                "Re-rooted SMA should match direct Earth-frame SMA");
            AssertNear(directEarthFrameState.Eccentricity, earthFrameState.Eccentricity, 1e-9,
                "Re-rooted eccentricity should match direct Earth-frame eccentricity");
        }

        [Test]
        public void ReRootStateVector_VesselEnteringChildSoi_RerootsToChild()
        {
            // Symmetric to the parent case: vessel in Earth frame approaching the Moon,
            // crossing into the Moon's SOI. The vessel is at (Earth-Moon distance -
            // 3e7, 0, 0) — about 30 Mm short of the Moon center, comfortably inside
            // the Moon's SOI (~6.6e7 m).
            //
            // After re-rooting to Moon frame, the vessel's relative position should be
            // (-3e7, 0, 0).
            double approachOffset = 3e7;  // 30 Mm inside the Moon's SOI from Earth side

            double3 vesselPosRelativeToEarth = new double3(
                EarthMoonDistanceMeters - approachOffset, 0, 0);
            // Velocity: vessel needs SOME velocity for ComputeFromStateVector to produce
            // well-defined elements (r×v must be non-zero). Use a value typical of an
            // Earth-frame trajectory at this radius.
            double3 vesselVel = new double3(0, 1500.0, 0);

            WorldPosition earthPosWorld = new WorldPosition(0, 0, 0);
            WorldPosition moonPosWorld = new WorldPosition(EarthMoonDistanceMeters, 0, 0);

            KeplerState moonFrameState = OrbitalElements.ReRootStateVector(
                currentPositionRelativeToCurrentBody: vesselPosRelativeToEarth,
                currentVelocity: vesselVel,
                currentBodyPositionWorld: earthPosWorld,
                newBodyPositionWorld: moonPosWorld,
                newBodyMu: MoonMu,
                epochTick: TestEpochTick,
                newBodyId: MoonBodyId);

            // Reconstruct expected via direct computation: vessel relative to Moon =
            // (-approachOffset, 0, 0).
            double3 expectedVesselPosRelativeToMoon = new double3(-approachOffset, 0, 0);
            KeplerState directMoonFrameState = OrbitalElements.ComputeFromStateVector(
                expectedVesselPosRelativeToMoon, vesselVel, MoonMu, TestEpochTick, MoonBodyId);

            Assert.AreEqual(MoonBodyId, moonFrameState.ReferenceBodyId);
            AssertNear(directMoonFrameState.SemiMajorAxis, moonFrameState.SemiMajorAxis, 1e-3,
                "Re-rooted SMA in Moon frame should match direct computation");
            AssertNear(directMoonFrameState.Eccentricity, moonFrameState.Eccentricity, 1e-9,
                "Re-rooted eccentricity in Moon frame should match direct computation");
        }

        [Test]
        public void ReRootStateVector_RoundTrip_PreservesStateVector()
        {
            // Re-root A→B, then B→A, and verify the resulting state vector matches
            // the original. Tolerances are relaxed beyond what double-precision
            // position arithmetic alone would give because the round-trip passes
            // through ComputeFromStateVector → ComputeStateVector internally (the
            // round-trip-through-orbital-elements adds numerical noise that dominates
            // the pure position-subtraction noise).

            // Original state: vessel orbiting Earth at LEO scale.
            double3 originalPosRelativeToEarth = new double3(LeoRadius, 0, 0);
            double vCircularLeo = math.sqrt(EarthMu / LeoRadius);
            double3 originalVel = new double3(0, vCircularLeo, 0);

            WorldPosition earthPosWorld = new WorldPosition(0, 0, 0);
            WorldPosition moonPosWorld = new WorldPosition(EarthMoonDistanceMeters, 0, 0);

            // A → B (Earth → Moon)
            KeplerState moonFrameState = OrbitalElements.ReRootStateVector(
                originalPosRelativeToEarth, originalVel,
                earthPosWorld, moonPosWorld, MoonMu, TestEpochTick, MoonBodyId);

            // Extract Moon-frame state vector via ComputeStateVector at the original
            // ν₀ (round-trip-through-elements).
            (double3 moonFramePos, double3 moonFrameVel) = OrbitalElements.ComputeStateVector(
                moonFrameState, moonFrameState.TrueAnomalyAtEpoch, MoonMu);

            // B → A (Moon → Earth)
            KeplerState earthFrameState = OrbitalElements.ReRootStateVector(
                moonFramePos, moonFrameVel,
                moonPosWorld, earthPosWorld, EarthMu, TestEpochTick, EarthBodyId);

            // Extract round-tripped state vector and compare to original.
            (double3 finalPos, double3 finalVel) = OrbitalElements.ComputeStateVector(
                earthFrameState, earthFrameState.TrueAnomalyAtEpoch, EarthMu);

            // Tolerance: 1e-3 m on position, 1e-6 m/s on velocity. Tighter would
            // require relaxing the contract on ComputeFromStateVector/ComputeStateVector
            // round-trips at Earth-Moon scales; 1e-3 m on a 7e6 m radius is ~1e-10
            // relative, comfortably within double-precision arithmetic but generous
            // enough to absorb the elements-round-trip noise.
            AssertVectorNear(originalPosRelativeToEarth, finalPos, 1e-3,
                "Round-trip position should match original");
            AssertVectorNear(originalVel, finalVel, 1e-6,
                "Round-trip velocity should match original");
        }

        [Test]
        public void ReRootStateVector_VelocityUnchanged_InPhase1()
        {
            // Explicit test of the Phase 1 velocity assumption: re-rooting changes the
            // reference body but leaves the vessel's velocity unchanged (because both
            // bodies are stationary in Phase 1; no relative motion to compensate for).
            // Documents the limitation that Phase 4+ will need to relax.
            double3 vesselPosRelativeToEarth = new double3(LeoRadius, 0, 0);
            double3 vesselVel = new double3(123.456, 789.012, -456.789);  // arbitrary

            WorldPosition earthPosWorld = new WorldPosition(0, 0, 0);
            WorldPosition moonPosWorld = new WorldPosition(EarthMoonDistanceMeters, 0, 0);

            KeplerState result = OrbitalElements.ReRootStateVector(
                vesselPosRelativeToEarth, vesselVel,
                earthPosWorld, moonPosWorld, MoonMu, TestEpochTick, MoonBodyId);

            // Extract the velocity from the resulting state vector at ν₀. It should
            // equal the input velocity exactly (no transformation applied in Phase 1).
            (double3 _, double3 reconstructedVel) = OrbitalElements.ComputeStateVector(
                result, result.TrueAnomalyAtEpoch, MoonMu);

            // Through ComputeFromStateVector → ComputeStateVector round-trip, exact
            // equality isn't guaranteed but 1e-6 m/s is well within the established
            // precision tolerance.
            AssertVectorNear(vesselVel, reconstructedVel, 1e-6,
                "Velocity should pass through unchanged (Phase 1 stationary-bodies assumption)");
        }

        [Test]
        public void ReRootStateVector_PreservesEpochTickAndBodyId()
        {
            // The helper's output KeplerState should have:
            //   - EpochTick == the input epochTick parameter
            //   - ReferenceBodyId == the input newBodyId parameter
            // These are direct pass-throughs but worth pinning down.
            long testTick = 12345;
            Guid testBodyId = Guid.NewGuid();

            double3 vesselPos = new double3(LeoRadius, 0, 0);
            double3 vesselVel = new double3(0, math.sqrt(EarthMu / LeoRadius), 0);

            WorldPosition body1 = new WorldPosition(0, 0, 0);
            WorldPosition body2 = new WorldPosition(EarthMoonDistanceMeters, 0, 0);

            KeplerState result = OrbitalElements.ReRootStateVector(
                vesselPos, vesselVel, body1, body2, EarthMu, testTick, testBodyId);

            Assert.AreEqual(testTick, result.EpochTick);
            Assert.AreEqual(testBodyId, result.ReferenceBodyId);
        }

        [Test]
        public void ReRootStateVector_HyperbolicTrajectory_ProducesHyperbolicElements()
        {
            // Vessel on a hyperbolic escape from Earth. After re-rooting to the Moon
            // frame at close range, the vessel's velocity in Moon frame is the same
            // as in Earth frame (Phase 1 assumption); the position changes by the
            // Earth-Moon offset.
            //
            // Construction: vessel at 10 Mm from Earth with speed > Earth's escape
            // velocity at that radius (sqrt(2μ/r) ≈ 8200 m/s); use 12000 m/s for
            // comfortable hyperbolic margin.
            double r = 1e7;
            double vEscape = math.sqrt(2.0 * EarthMu / r);
            Assert.Greater(12000.0, vEscape, "Sanity: 12000 m/s > Earth escape at 1e7 m");

            double3 vesselPosEarthFrame = new double3(r, 0, 0);
            double3 vesselVel = new double3(0, 12000.0, 0);  // perpendicular to position

            // Earth-frame check: confirm input is genuinely hyperbolic (e > 1) at Earth.
            KeplerState earthCheck = OrbitalElements.ComputeFromStateVector(
                vesselPosEarthFrame, vesselVel, EarthMu, TestEpochTick, EarthBodyId);
            Assert.Greater(earthCheck.Eccentricity, 1.0,
                "Sanity: input trajectory should be hyperbolic in Earth frame");

            // Re-root to Moon frame. Result depends on Moon-frame energy:
            //   v² > 2 · MoonMu / r_to_moon  → hyperbolic in Moon frame too
            // Moon's μ is much smaller than Earth's, and at the Moon-frame distance
            // the vessel is far from the Moon (~3.7e8 m), so Moon's gravity is
            // negligible — vessel is on a hyperbolic Moon-frame trajectory.
            WorldPosition earthPosWorld = new WorldPosition(0, 0, 0);
            WorldPosition moonPosWorld = new WorldPosition(EarthMoonDistanceMeters, 0, 0);

            KeplerState moonFrameResult = OrbitalElements.ReRootStateVector(
                vesselPosEarthFrame, vesselVel,
                earthPosWorld, moonPosWorld, MoonMu, TestEpochTick, MoonBodyId);

            Assert.Greater(moonFrameResult.Eccentricity, 1.0,
                "Re-rooted to Moon frame: trajectory should still be hyperbolic (Moon μ tiny, vessel far)");
            Assert.Less(moonFrameResult.SemiMajorAxis, 0.0,
                "Hyperbolic semi-major axis is negative");
        }

        [Test]
        public void ReRootStateVector_AcrossLargeDistances_NumericalStability()
        {
            // Stress test: vessel at 1e9 m from both bodies (~2.6× Earth-Moon distance).
            // Verify that double-precision arithmetic doesn't lose precision in the
            // position subtractions. Tolerance scales with magnitude — at 1e9 m,
            // double-precision gives ~16 significant digits, so absolute error from
            // arithmetic should be well below 1 m.
            double largeOffset = 1e9;

            double3 vesselPosRelativeToBody1 = new double3(largeOffset, 0, 0);
            double3 vesselVel = new double3(0, 1000.0, 0);

            // Body1 at origin, Body2 1e9 m away in y direction. Vessel is at (1e9, 0, 0)
            // absolute, which is (1e9, -1e9, 0) relative to Body2.
            WorldPosition body1PosWorld = new WorldPosition(0, 0, 0);
            WorldPosition body2PosWorld = new WorldPosition(0, largeOffset, 0);

            KeplerState result = OrbitalElements.ReRootStateVector(
                vesselPosRelativeToBody1, vesselVel,
                body1PosWorld, body2PosWorld, EarthMu, TestEpochTick, MoonBodyId);

            // Reconstruct via direct computation.
            double3 expectedRelativePosToBody2 = new double3(largeOffset, -largeOffset, 0);
            KeplerState directResult = OrbitalElements.ComputeFromStateVector(
                expectedRelativePosToBody2, vesselVel, EarthMu, TestEpochTick, MoonBodyId);

            // Relative SMA tolerance: 1e-6 relative. At SMA ~1e9 m scale this is ~1e3 m
            // absolute — generous but well within what double arithmetic guarantees.
            double smaTolerance = math.abs(directResult.SemiMajorAxis) * 1e-6;
            AssertNear(directResult.SemiMajorAxis, result.SemiMajorAxis, smaTolerance,
                "Re-rooted SMA across 1e9 m distances should match direct computation");
            AssertNear(directResult.Eccentricity, result.Eccentricity, 1e-9,
                "Re-rooted eccentricity should be precision-stable across large distances");
        }

        // ----- Anomaly conversions (commit 045 Stage 1) -----

        [Test]
        public void TrueToMeanAnomaly_CircularOrbit_ReturnsSameAngle()
        {
            // e = 0: Kepler's equation reduces to M = E, and E = ν (no eccentricity to
            // distort the anomaly). At every true anomaly, mean anomaly equals true
            // anomaly. Test a few points around the circle.
            AssertNear(0.0, OrbitalElements.TrueToMeanAnomaly(0.0, 0.0), 1e-12,
                "Circular orbit at ν=0: M=0");
            AssertNear(math.PI_DBL * 0.5, OrbitalElements.TrueToMeanAnomaly(math.PI_DBL * 0.5, 0.0), 1e-12,
                "Circular orbit at ν=π/2: M=π/2");
            AssertNear(math.PI_DBL, OrbitalElements.TrueToMeanAnomaly(math.PI_DBL, 0.0), 1e-12,
                "Circular orbit at ν=π: M=π");
        }

        [Test]
        public void TrueToMeanAnomaly_EllipticalAtPeriapsis_ReturnsZero()
        {
            // Periapsis is ν=0. At ν=0 the eccentric anomaly E=0 (by the formula), and
            // M = E - e·sin(E) = 0 - e·0 = 0. Holds for any e.
            AssertNear(0.0, OrbitalElements.TrueToMeanAnomaly(0.0, 0.5), 1e-12,
                "Elliptical at ν=0: M=0 (periapsis)");
            AssertNear(0.0, OrbitalElements.TrueToMeanAnomaly(0.0, 0.95), 1e-12,
                "High-eccentricity ellipse at ν=0: M=0");
        }

        [Test]
        public void TrueToMeanAnomaly_EllipticalAtApoapsis_ReturnsPi()
        {
            // Apoapsis is ν=π. E = 2·atan2(√(1-e)·sin(π/2), √(1+e)·cos(π/2)).
            // cos(π/2) = 0, so atan2 with second argument 0 gives π/2 for positive first
            // argument → E = π. M = π - e·sin(π) = π - 0 = π. Holds for any e<1.
            AssertNear(math.PI_DBL, OrbitalElements.TrueToMeanAnomaly(math.PI_DBL, 0.5), 1e-12,
                "Elliptical at ν=π: M=π (apoapsis)");
            AssertNear(math.PI_DBL, OrbitalElements.TrueToMeanAnomaly(math.PI_DBL, 0.95), 1e-12,
                "High-eccentricity ellipse at ν=π: M=π");
        }

        [Test]
        public void TrueToMeanAnomaly_EllipticalAtQuarter_HasCorrectValue()
        {
            // ν = π/2, e = 0.3. Compute expected analytically:
            //   tan(ν/2) = tan(π/4) = 1
            //   E = 2·atan2(√(1-0.3)·sin(π/4), √(1+0.3)·cos(π/4))
            //     = 2·atan2(√0.7 · √2/2, √1.3 · √2/2)
            //     = 2·atan2(√0.7, √1.3)
            //     = 2·atan(√(0.7/1.3))
            //     = 2·atan(√0.53846...)
            //     = 2·atan(0.73380...)
            //     = 2·0.63293...
            //     = 1.26586...
            //   M = E - 0.3·sin(E) = 1.26586 - 0.3·sin(1.26586) = 1.26586 - 0.3·0.95255
            //     = 1.26586 - 0.28577 = 0.98009...
            // Hand-computed via the same formula the helper implements; tolerance 1e-9.
            double e = 0.3;
            double nu = math.PI_DBL * 0.5;
            double sqrtOneMinusE = math.sqrt(1.0 - e);
            double sqrtOnePlusE = math.sqrt(1.0 + e);
            double E_expected = 2.0 * math.atan2(
                sqrtOneMinusE * math.sin(nu * 0.5),
                sqrtOnePlusE * math.cos(nu * 0.5));
            double M_expected = E_expected - e * math.sin(E_expected);

            double M_actual = OrbitalElements.TrueToMeanAnomaly(nu, e);
            AssertNear(M_expected, M_actual, 1e-12,
                "Elliptical at ν=π/2, e=0.3: M should match hand-derived formula");
        }

        [Test]
        public void TrueToMeanAnomaly_HyperbolicAtPeriapsis_ReturnsZero()
        {
            // Periapsis on hyperbolic trajectory is ν=0. By the same logic as the
            // elliptical case: ratio = 0, H = 0, M = e·sinh(0) - 0 = 0.
            AssertNear(0.0, OrbitalElements.TrueToMeanAnomaly(0.0, 1.5), 1e-12,
                "Hyperbolic at ν=0: M=0 (periapsis)");
            AssertNear(0.0, OrbitalElements.TrueToMeanAnomaly(0.0, 3.0), 1e-12,
                "High-energy hyperbolic at ν=0: M=0");
        }

        [Test]
        public void TrueToMeanAnomaly_HyperbolicPositiveAnomaly_HasCorrectSign()
        {
            // Positive ν on hyperbolic trajectory should give positive M (the math is
            // odd-symmetric around ν=0). Test ν=π/4 with e=1.5; expect positive M.
            double M = OrbitalElements.TrueToMeanAnomaly(math.PI_DBL * 0.25, 1.5);
            Assert.Greater(M, 0.0,
                "Hyperbolic at ν=π/4 should give positive M");
            // Also verify reasonable magnitude — not NaN, not absurd.
            Assert.Less(M, 10.0,
                "Hyperbolic M at ν=π/4 should be a reasonable magnitude (well below asymptote)");
        }

        [Test]
        public void MeanToTrueAnomaly_RoundTripPreservesValue()
        {
            // For an elliptical orbit, MeanToTrueAnomaly(TrueToMeanAnomaly(ν, e), e)
            // should return ν within solver tolerance. Test several ν values across
            // the orbit at e=0.4.
            double e = 0.4;
            double[] testAngles = {
                0.0,
                math.PI_DBL * 0.25,
                math.PI_DBL * 0.5,
                math.PI_DBL * 0.75,
                math.PI_DBL,
                math.PI_DBL * 1.25,
                math.PI_DBL * 1.75,
            };

            foreach (double nu in testAngles)
            {
                double M = OrbitalElements.TrueToMeanAnomaly(nu, e);
                double nu_recovered = OrbitalElements.MeanToTrueAnomaly(M, e);
                // Recovered ν may differ from input by a multiple of 2π depending on
                // which half of the orbit MeanToTrueAnomaly's atan2 picks. Normalize
                // the difference into (-π, π] for comparison.
                double diff = nu_recovered - nu;
                while (diff > math.PI_DBL) diff -= 2.0 * math.PI_DBL;
                while (diff < -math.PI_DBL) diff += 2.0 * math.PI_DBL;
                AssertNear(0.0, diff, 1e-10,
                    $"Round-trip ν={nu:G6} at e=0.4: expected recovery to within 1e-10 rad");
            }
        }

        [Test]
        public void MeanToTrueAnomaly_HyperbolicRoundTripPreservesValue()
        {
            // Hyperbolic round-trip. ν must stay within asymptote angle for the math
            // to be physically meaningful; test small to moderate ν at e=1.8.
            double e = 1.8;
            double[] testAngles = {
                0.0,
                math.PI_DBL * 0.1,
                math.PI_DBL * 0.25,
                math.PI_DBL * 0.4,
                -math.PI_DBL * 0.25,
            };

            foreach (double nu in testAngles)
            {
                double M = OrbitalElements.TrueToMeanAnomaly(nu, e);
                double nu_recovered = OrbitalElements.MeanToTrueAnomaly(M, e);
                AssertNear(nu, nu_recovered, 1e-10,
                    $"Hyperbolic round-trip ν={nu:G6} at e=1.8: expected exact recovery");
            }
        }

        [Test]
        public void MeanMotion_EarthOrbitMatchesExpected()
        {
            // Earth's μ = G · 5.972e24 = 3.986e14 m³/s² (approx).
            // For a = 7e6 m (LEO), n = √(μ/a³) = √(3.986e14 / 3.43e20) = √1.162e-6
            //   = 0.001078 rad/s (approximately).
            // Orbital period T = 2π/n ≈ 5828 seconds ≈ 97 minutes (LEO matches reality).
            double a = 7.0e6;
            double n = OrbitalElements.MeanMotion(a, EarthMu);
            double expectedN = math.sqrt(EarthMu / (a * a * a));
            AssertNear(expectedN, n, 1e-15,
                "Mean motion at LEO should match direct sqrt(μ/a³) formula");

            // Sanity: orbital period via T = 2π/n should be ~5828 seconds.
            double period = 2.0 * math.PI_DBL / n;
            Assert.Greater(period, 5800.0, "LEO period > 5800 s");
            Assert.Less(period, 5900.0, "LEO period < 5900 s (~97 minutes)");
        }

        [Test]
        public void MeanMotion_HyperbolicReturnsPositive()
        {
            // Hyperbolic orbit: a < 0. The formula uses |a| so n is positive regardless.
            double a = -1.0e7;
            double n = OrbitalElements.MeanMotion(a, EarthMu);
            Assert.Greater(n, 0.0, "Mean motion should always be positive (uses |a|)");
            // Magnitude check: should match |a|=1e7 case exactly.
            double nPositiveA = OrbitalElements.MeanMotion(1.0e7, EarthMu);
            AssertNear(nPositiveA, n, 1e-15,
                "Mean motion at a=-1e7 should equal mean motion at a=+1e7 (uses |a|)");
        }

        [Test]
        public void TrueToMeanAnomaly_NearParabolic_DoesNotThrow_AndReturnsFinite()
        {
            // At e = 1+1e-10, the function routes through the hyperbolic branch (e >= 1).
            // Numerical instability is acknowledged in the XML doc; this test asserts
            // the function doesn't throw and returns a finite value (no NaN, no infinity).
            // Position error in the result is allowed to be O(1e-3) per the documented
            // parabolic-instability-band tolerance.
            double e = 1.0 + 1e-10;
            double nu = math.PI_DBL * 0.25;

            double M = OrbitalElements.TrueToMeanAnomaly(nu, e);

            Assert.IsFalse(double.IsNaN(M),
                "Near-parabolic call should not return NaN");
            Assert.IsFalse(double.IsInfinity(M),
                "Near-parabolic call should not return infinity");
            // Magnitude should be reasonable (within an order of magnitude of ν).
            Assert.Less(math.abs(M), 100.0,
                $"Near-parabolic M magnitude should be reasonable; got {M:G6}");
        }

        [Test]
        public void TrueToMeanAnomaly_NegativeTrueAnomaly_ReturnsNegativeMean()
        {
            // Spec'd in the helper XML doc: mean anomaly is signed, so negative ν
            // produces negative M. Tests the signed-anomaly behavior the predictor work
            // relies on (Δt computations subtract two mean anomalies and expect a
            // signed result).
            double e = 0.3;
            double nu = -math.PI_DBL * 0.25;

            double M = OrbitalElements.TrueToMeanAnomaly(nu, e);

            Assert.Less(M, 0.0,
                "Negative true anomaly should produce negative mean anomaly");
            // Also verify magnitude is reasonable (between 0 and -π).
            Assert.Greater(M, -math.PI_DBL,
                "Negative M from ν=-π/4 should be > -π in magnitude");
        }

        // ----- SolveConicAtRadius (commit 047 Stage 1) -----

        private const double TickIntervalSeconds = 1.0 / 30.0;  // 30 Hz sim-tick

        /// <summary>
        /// Helper: build a default KeplerState at LeoRadius with the given eccentricity
        /// and initial true anomaly. ω=0, Ω=0, i=0 (equatorial, periapsis at +X).
        /// </summary>
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
                ReferenceBodyId = TestBodyId,
            };
        }

        [Test]
        public void SolveConicAtRadius_OrbitFullyInsideRadius_ReturnsNull()
        {
            // Circular LEO orbit at r=7e6 m, target radius 1e9 m (way outside LEO).
            // rApo < targetRadius → orbit fully inside → return null.
            var state = BuildState(LeoRadius, eccentricity: 0.0, trueAnomalyAtEpoch: 0.0);

            long? crossing = OrbitalElements.SolveConicAtRadius(
                state, targetRadius: 1.0e9, currentTick: 0, EarthMu, TickIntervalSeconds);

            Assert.IsNull(crossing,
                "Orbit with apoapsis < targetRadius should produce no crossing");
        }

        [Test]
        public void SolveConicAtRadius_OrbitFullyOutsideRadius_ReturnsNull()
        {
            // Circular orbit at r=1e9 m, target radius 1e6 m (way inside orbit).
            // rPeri > targetRadius → orbit fully outside → return null.
            var state = BuildState(semiMajorAxis: 1.0e9, eccentricity: 0.0, trueAnomalyAtEpoch: 0.0);

            long? crossing = OrbitalElements.SolveConicAtRadius(
                state, targetRadius: 1.0e6, currentTick: 0, EarthMu, TickIntervalSeconds);

            Assert.IsNull(crossing,
                "Orbit with periapsis > targetRadius should produce no crossing");
        }

        [Test]
        public void SolveConicAtRadius_EllipticalCrossing_ReturnsValidFutureTick()
        {
            // Elliptical orbit: rPeri = 2e8, rApo = 1.5e9 → e ≈ 0.764 (under 0.8 ceiling
            // for Newton-Raphson convergence stability). Target radius 9.24e8 m (Earth's
            // SOI). Vessel at periapsis (ν=0) at epoch 0. Outward crossing exists.
            double rPeri = 2.0e8;
            double rApo = 1.5e9;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            Assert.Less(e, 0.8, "Test orbit eccentricity must stay below 0.8 for solver stability");

            double targetRadius = 9.24e8;
            var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

            long? crossing = OrbitalElements.SolveConicAtRadius(
                state, targetRadius, currentTick: 0, EarthMu, TickIntervalSeconds);

            Assert.IsTrue(crossing.HasValue, "Elliptical orbit reaching targetRadius should return a crossing tick");
            Assert.Greater(crossing.Value, 0, "Crossing should be in the future");

            // Analytic expectation: cos(ν) = (p/r - 1)/e, +ν is outbound.
            double p = a * (1.0 - e * e);
            double cosNu = (p / targetRadius - 1.0) / e;
            double nu = math.acos(cosNu);
            double M = OrbitalElements.TrueToMeanAnomaly(nu, e);
            double n = OrbitalElements.MeanMotion(a, EarthMu);
            long expectedTick = (long)math.ceil((M / n) / TickIntervalSeconds);

            Assert.AreEqual(expectedTick, crossing.Value, 2,
                $"Elliptical crossing should land at ~{expectedTick} ticks");
        }

        [Test]
        public void SolveConicAtRadius_HyperbolicCrossingPreperiapsis_ReturnsFutureTick()
        {
            // Hyperbolic orbit, vessel BEFORE periapsis (ν₀ < 0). Vessel will reach
            // periapsis then continue outbound; the future crossing at target > rPeri
            // is on the outbound leg after periapsis. Return tick should be positive.
            double rPeri = LeoRadius;
            double e = 1.5;
            double a = rPeri / (1.0 - e);  // negative for hyperbolic
            // Vessel currently INBOUND at ν = -0.5 rad (pre-periapsis).
            var state = BuildState(a, e, trueAnomalyAtEpoch: -0.5);

            // Target radius beyond periapsis: vessel will cross it AFTER reaching periapsis.
            double targetRadius = 5.0e7;  // ~5x LEO radius — within reach of e=1.5 hyperbolic

            long? crossing = OrbitalElements.SolveConicAtRadius(
                state, targetRadius, currentTick: 0, EarthMu, TickIntervalSeconds);

            Assert.IsTrue(crossing.HasValue,
                "Pre-periapsis hyperbolic vessel should predict outbound crossing");
            Assert.Greater(crossing.Value, 0, "Crossing should be in the future");
        }

        [Test]
        public void SolveConicAtRadius_HyperbolicCrossingPostperiapsis_ReturnsFutureOnlyIfNotPassed()
        {
            // Hyperbolic orbit, vessel PAST periapsis on outbound leg.
            // Sub-case A: target > vessel's current r → vessel still approaching →
            //             future tick.
            // Sub-case B: target < vessel's current r → vessel already past target →
            //             null (the past crossing is not a future event).
            double rPeri = LeoRadius;
            double e = 1.5;
            double a = rPeri / (1.0 - e);

            // Vessel at ν = +1.0 rad (well past periapsis, on outbound leg).
            var state = BuildState(a, e, trueAnomalyAtEpoch: 1.0);

            // Vessel's current radius at ν=1.0:
            double p = a * (1.0 - e * e);
            double rNow = p / (1.0 + e * math.cos(1.0));

            // Sub-case A: target slightly beyond current radius — should return future tick.
            double targetAhead = rNow * 1.5;
            long? crossingAhead = OrbitalElements.SolveConicAtRadius(
                state, targetAhead, currentTick: 0, EarthMu, TickIntervalSeconds);
            Assert.IsTrue(crossingAhead.HasValue,
                $"Hyperbolic post-periapsis vessel at r={rNow:E3} should predict future crossing at r={targetAhead:E3}");
            Assert.Greater(crossingAhead.Value, 0, "Future crossing should be positive tick");

            // Sub-case B: target between rPeri and rNow — vessel already passed it.
            // Only the inbound branch's ν reaches this radius (negative ν, pre-periapsis);
            // post-periapsis vessel won't return. Should yield null.
            double targetBehind = (rPeri + rNow) * 0.5;
            long? crossingBehind = OrbitalElements.SolveConicAtRadius(
                state, targetBehind, currentTick: 0, EarthMu, TickIntervalSeconds);
            Assert.IsNull(crossingBehind,
                $"Hyperbolic post-periapsis vessel at r={rNow:E3} cannot reach past r={targetBehind:E3} again");
        }

        [Test]
        public void SolveConicAtRadius_BoundaryHugCircularAtRadius_ReturnsNull()
        {
            // Circular orbit at exactly target radius. Without the boundary-hug guard,
            // floating-point rounding in cos(ν) = (p/r - 1)/e would either error out
            // or report a spurious crossing every period. The 1.0m tolerance catches
            // this cleanly.
            double r = 1.0e8;
            var state = BuildState(semiMajorAxis: r, eccentricity: 0.0, trueAnomalyAtEpoch: 0.0);

            long? crossing = OrbitalElements.SolveConicAtRadius(
                state, targetRadius: r, currentTick: 0, EarthMu, TickIntervalSeconds);

            Assert.IsNull(crossing,
                "Circular orbit at exactly target radius should be caught by boundary-hug guard");
        }

        [Test]
        public void SolveConicAtRadius_OverflowDefense_ReturnsNull()
        {
            // Tiny gravitational parameter (1 kg body, μ = G ≈ 6.67e-11) combined with
            // a very large orbit produces astronomically large period. Time-to-crossing
            // exceeds long.MaxValue/2 ticks; overflow defense returns null.
            double tinyMu = CoordinateMath.G * 1.0;  // μ for 1 kg
            double rPeri = 1.0e11;
            double e = 0.5;
            double a = rPeri / (1.0 - e);  // a = 2e11
            var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

            // Target radius reachable on orbit (apoapsis = 3e11; choose 2e11 between
            // peri and apo). Crossing math would produce a real future event in seconds,
            // but seconds → ticks blows past long.MaxValue/2.
            double targetRadius = 2.0e11;

            long? crossing = OrbitalElements.SolveConicAtRadius(
                state, targetRadius, currentTick: 0, tinyMu, TickIntervalSeconds);

            Assert.IsNull(crossing,
                "Very long-period orbit with crossing tick beyond long.MaxValue/2 should return null via overflow defense");
        }

        [Test]
        public void SolveConicAtRadius_VeryNearParabolic_DoesNotThrow()
        {
            // Eccentricity just outside the parabolic-instability band on the hyperbolic
            // side. The helper should not throw — at worst it returns null or a
            // numerical-noise-affected result. This is a smoke test for stability
            // near e=1, not a correctness assertion.
            double rPeri = LeoRadius;
            double e = 1.0 + OrbitalElements.ParabolicInstabilityBand * 2.0;  // just outside the band
            double a = rPeri / (1.0 - e);  // very large negative number
            var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

            // Should not throw regardless of outcome.
            long? crossing = OrbitalElements.SolveConicAtRadius(
                state, targetRadius: 1.0e9, currentTick: 0, EarthMu, TickIntervalSeconds);

            // Output may be null or a finite future tick; both are acceptable.
            // The test exists to lock in "no exception thrown" for the near-parabolic edge.
            if (crossing.HasValue)
            {
                Assert.Greater(crossing.Value, 0,
                    "If a crossing is reported near parabolic, it must be in the future");
            }
        }
    }
}
