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
        private const double EarthMassKg = 5.972e24;
        private static readonly double EarthMu = CoordinateMath.G * EarthMassKg;

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

        // ----- Event-prediction fields are null at construction (Phase 0 stub) -----

        [Test]
        public void ComputeFromStateVector_EventPredictionFields_AreNull()
        {
            double3 r = new double3(LeoRadius, 0.0, 0.0);
            double vCircular = math.sqrt(EarthMu / LeoRadius);
            double3 v = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                r, v, EarthMu, TestEpochTick, TestBodyId);

            Assert.IsNull(state.NextPeriapsisTick, "event prediction stubs left null in Phase 0");
            Assert.IsNull(state.NextApoapsisTick);
            Assert.IsNull(state.NextSoiTransitionTick);
            Assert.IsNull(state.NextModeTransitionTick);
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
    }
}
