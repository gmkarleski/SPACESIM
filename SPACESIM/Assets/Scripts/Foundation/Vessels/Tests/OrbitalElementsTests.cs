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
    }
}
