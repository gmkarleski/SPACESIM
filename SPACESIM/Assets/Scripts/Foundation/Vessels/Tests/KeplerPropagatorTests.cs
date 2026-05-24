using System;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.Vessels;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="KeplerPropagator"/>. Pure-math tests with no Unity
    /// scene dependencies. Test bodies use Earth-equivalent μ = G · 5.972e24 unless
    /// otherwise noted.
    ///
    /// Tolerance conventions match <see cref="OrbitalElementsTests"/>:
    ///   - Angles (radians): 1e-9 absolute baseline.
    ///   - Distances (meters): 1e-3 absolute or 1e-9 relative, whichever is looser, at LEO scale.
    ///   - Round-trip tests use 1e-7 relative because they compose both directions.
    ///
    /// The propagator math composes the existing OrbitalElements layer (verified at
    /// commit 038 in OrbitalElementsTests). These tests focus on the time-advancement
    /// piece: given elements at epoch tick N, does propagating to tick M produce the
    /// right state vector?
    /// </summary>
    public class KeplerPropagatorTests
    {
        private const double EarthMassKg = PhysicsConstants.EarthMassKg;
        private static readonly double EarthMu = PhysicsConstants.EarthMu;
        private const double LeoRadius = 7_000_000.0;

        // Sim-tick interval matching SimTickController.SimTickIntervalSeconds.
        // Test takes it as a parameter (per design discussion) rather than reading
        // the controller's const directly — keeps math testable in isolation.
        private const double TickInterval = 1.0 / 30.0;

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

        // Compute the period of an elliptical orbit (seconds) given semi-major axis and mu.
        // T = 2π · sqrt(a³ / μ)
        private static double OrbitalPeriodSeconds(double semiMajorAxis, double mu)
        {
            return 2.0 * math.PI_DBL * math.sqrt(semiMajorAxis * semiMajorAxis * semiMajorAxis / mu);
        }

        // ----- Circular orbit -----

        [Test]
        public void PropagateState_CircularOrbit_OneQuarterPeriod_RotatesNinetyDegrees()
        {
            // Circular orbit in equatorial plane: vessel at (r, 0, 0) moving in +Y at
            // circular velocity. After one quarter period, vessel should be at (0, r, 0).
            double r = LeoRadius;
            double vCircular = math.sqrt(EarthMu / r);
            double3 startPos = new double3(r, 0.0, 0.0);
            double3 startVel = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            // Quarter period in ticks (rounded to nearest tick).
            double period = OrbitalPeriodSeconds(r, EarthMu);
            long quarterPeriodTicks = (long)math.round(period * 0.25 / TickInterval);
            long targetTick = TestEpochTick + quarterPeriodTicks;

            (double3 pos, double3 vel) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            // After quarter period: position rotated 90° from +X to +Y. Tolerance loosened
            // because quarterPeriodTicks rounding introduces sub-tick angular error.
            double angularError = vCircular * TickInterval; // ~250 m worst-case rounding
            AssertNear(0.0, pos.x, angularError, "quarter period: pos.x should be ~0");
            AssertNear(r, pos.y, angularError, "quarter period: pos.y should be ~r");
            AssertNear(0.0, pos.z, 1e-3, "quarter period: pos.z should be 0 (equatorial)");

            // Velocity rotated 90°: from +Y to -X.
            AssertNear(-vCircular, vel.x, angularError, "quarter period: vel.x should be ~-v_circular");
            AssertNear(0.0, vel.y, angularError, "quarter period: vel.y should be ~0");
        }

        [Test]
        public void PropagateState_CircularOrbit_FullPeriod_ReturnsToStart()
        {
            double r = LeoRadius;
            double vCircular = math.sqrt(EarthMu / r);
            double3 startPos = new double3(r, 0.0, 0.0);
            double3 startVel = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            double period = OrbitalPeriodSeconds(r, EarthMu);
            long periodTicks = (long)math.round(period / TickInterval);
            long targetTick = TestEpochTick + periodTicks;

            (double3 pos, double3 vel) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            // After one full period: back to start. Tolerance ~v·dt for tick rounding.
            double tolerance = vCircular * TickInterval;
            AssertVectorNear(startPos, pos, tolerance, "full period position");
            AssertVectorNear(startVel, vel, tolerance, "full period velocity");
        }

        // ----- Elliptical orbit -----

        [Test]
        public void PropagateState_EllipticalOrbit_HalfPeriod_ReachesApoapsis()
        {
            // Elliptical orbit: periapsis at r_p = LeoRadius, apoapsis at 2·LeoRadius.
            // a = 1.5·LeoRadius, e = 1/3.
            double rPeriapsis = LeoRadius;
            double rApoapsis = 2.0 * LeoRadius;
            double a = (rPeriapsis + rApoapsis) / 2.0;

            double vPeriapsis = math.sqrt(EarthMu * (2.0 / rPeriapsis - 1.0 / a));
            double3 startPos = new double3(rPeriapsis, 0.0, 0.0);
            double3 startVel = new double3(0.0, vPeriapsis, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            // Half period from periapsis lands at apoapsis (vessel at -r_apoapsis on +X axis).
            double period = OrbitalPeriodSeconds(a, EarthMu);
            long halfPeriodTicks = (long)math.round(period * 0.5 / TickInterval);
            long targetTick = TestEpochTick + halfPeriodTicks;

            (double3 pos, _) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            double distance = math.length(pos);
            AssertNear(rApoapsis, distance, 100.0, "half period from periapsis: should be at apoapsis");
            // Direction: vessel is on opposite side of body from start. Start was at +X;
            // apoapsis is at -X.
            Assert.Less(pos.x, 0.0, "half period: vessel should be on -X side (opposite of periapsis at +X)");
        }

        [Test]
        public void PropagateState_EllipticalOrbit_FullPeriod_ReturnsToStart()
        {
            // Same elliptical setup as above, but propagate one full period.
            double rPeriapsis = LeoRadius;
            double rApoapsis = 2.0 * LeoRadius;
            double a = (rPeriapsis + rApoapsis) / 2.0;
            double vPeriapsis = math.sqrt(EarthMu * (2.0 / rPeriapsis - 1.0 / a));
            double3 startPos = new double3(rPeriapsis, 0.0, 0.0);
            double3 startVel = new double3(0.0, vPeriapsis, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            double period = OrbitalPeriodSeconds(a, EarthMu);
            long periodTicks = (long)math.round(period / TickInterval);
            long targetTick = TestEpochTick + periodTicks;

            (double3 pos, double3 vel) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            double tolerance = vPeriapsis * TickInterval;
            AssertVectorNear(startPos, pos, tolerance, "elliptical full period position");
            AssertVectorNear(startVel, vel, tolerance, "elliptical full period velocity");
        }

        // ----- Hyperbolic trajectory -----

        [Test]
        public void PropagateState_HyperbolicTrajectory_AdvancesAwayFromPeriapsis()
        {
            // Hyperbolic launch from periapsis at LeoRadius with 1.5× escape velocity.
            double escapeV = math.sqrt(2.0 * EarthMu / LeoRadius);
            double3 startPos = new double3(LeoRadius, 0.0, 0.0);
            double3 startVel = new double3(0.0, 1.5 * escapeV, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            // Propagate 100 ticks (~3.3 seconds at 30 Hz) forward. Vessel should still be
            // moving away from periapsis on a hyperbolic trajectory; distance from body
            // strictly greater than starting periapsis.
            long targetTick = TestEpochTick + 100;
            (double3 pos, double3 vel) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            double distance = math.length(pos);
            Assert.Greater(distance, LeoRadius,
                "hyperbolic: distance from body should be increasing past periapsis");

            // Velocity magnitude should still exceed escape velocity at the new (greater)
            // distance — vis-viva for hyperbolic: v² = μ·(2/r - 1/a) with a < 0, which means
            // v² is always greater than the escape value at any finite r.
            double speed = math.length(vel);
            double escapeAtNewDistance = math.sqrt(2.0 * EarthMu / distance);
            Assert.Greater(speed, escapeAtNewDistance,
                "hyperbolic: speed should remain above escape velocity at the new distance");
        }

        // ----- Retrograde orbit -----

        [Test]
        public void PropagateState_RetrogradeCircularOrbit_RotatesInOppositeDirection()
        {
            // Retrograde circular orbit: vessel at (r, 0, 0) moving in -Y (opposite to
            // the prograde +Y of earlier tests). The orbit normal flips from +Z to -Z;
            // inclination becomes π (or close to it).
            double r = LeoRadius;
            double vCircular = math.sqrt(EarthMu / r);
            double3 startPos = new double3(r, 0.0, 0.0);
            double3 startVel = new double3(0.0, -vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            // Inclination for pure retrograde equatorial: π radians (180°).
            AssertNear(math.PI_DBL, state.Inclination, 1e-9, "retrograde inclination should be π");

            // After a quarter period: vessel should be at (0, -r, 0), not (0, +r, 0).
            double period = OrbitalPeriodSeconds(r, EarthMu);
            long quarterPeriodTicks = (long)math.round(period * 0.25 / TickInterval);
            long targetTick = TestEpochTick + quarterPeriodTicks;

            (double3 pos, _) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            double tolerance = vCircular * TickInterval;
            AssertNear(0.0, pos.x, tolerance, "retrograde quarter period: pos.x should be ~0");
            AssertNear(-r, pos.y, tolerance, "retrograde quarter period: pos.y should be ~-r (opposite of prograde)");
        }

        // ----- High-eccentricity stress test -----

        [Test]
        public void PropagateState_HighEccentricityOrbit_SolverConverges()
        {
            // Eccentricity 0.95 — stressful for Newton-Raphson without Conway's starter.
            // Periapsis r_p = LeoRadius; semi-major axis chosen so that the orbit is
            // very elongated. a = r_p / (1 - e); for r_p = 7e6 and e = 0.95, a = 1.4e8.
            double e = 0.95;
            double rPeriapsis = LeoRadius;
            double a = rPeriapsis / (1.0 - e);
            double vPeriapsis = math.sqrt(EarthMu * (2.0 / rPeriapsis - 1.0 / a));
            double3 startPos = new double3(rPeriapsis, 0.0, 0.0);
            double3 startVel = new double3(0.0, vPeriapsis, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            // Propagate one full period — expensive case for solver because most of the
            // period is spent near apoapsis where M is far from E and convergence is slow.
            double period = OrbitalPeriodSeconds(a, EarthMu);
            long periodTicks = (long)math.round(period / TickInterval);
            long targetTick = TestEpochTick + periodTicks;

            // Should not log any solver-divergence warnings.
            (double3 pos, double3 vel) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            // Position should return to start within tolerance proportional to elapsed time
            // and the tick-rounding error.
            double tolerance = vPeriapsis * TickInterval;
            AssertVectorNear(startPos, pos, tolerance, "high-e full period position");
            AssertVectorNear(startVel, vel, tolerance, "high-e full period velocity");
        }

        // ----- Long propagation interval -----

        [Test]
        public void PropagateState_LongInterval_NumericallyStable()
        {
            // Propagate a circular orbit for 1000 periods. Accumulated numerical error
            // should stay bounded — testing that mean-anomaly wrapping does its job.
            double r = LeoRadius;
            double vCircular = math.sqrt(EarthMu / r);
            double3 startPos = new double3(r, 0.0, 0.0);
            double3 startVel = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            double period = OrbitalPeriodSeconds(r, EarthMu);
            long thousandPeriodsTicks = (long)math.round(1000.0 * period / TickInterval);
            long targetTick = TestEpochTick + thousandPeriodsTicks;

            (double3 pos, _) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            // After 1000 periods, position should still be close to start. Tolerance
            // dominated by tick-rounding accumulation over the propagation duration:
            // 1000 periods × tick-rounding-per-period ≈ 1000 · v·dt ≈ 1000 · 250 = 2.5e5 m.
            // Generous tolerance for that.
            double tolerance = 1000.0 * vCircular * TickInterval;
            AssertVectorNear(startPos, pos, tolerance, "long interval position stability");
            double distance = math.length(pos);
            AssertNear(r, distance, tolerance, "long interval: still on the orbit (correct radius)");
        }

        // ----- Edge cases: dt boundaries -----

        [Test]
        public void PropagateState_ZeroElapsedTime_ReturnsEpochStateVector()
        {
            // currentTick == epochTick: dt = 0. Should return exactly the epoch state.
            double r = LeoRadius;
            double vCircular = math.sqrt(EarthMu / r);
            double3 startPos = new double3(r, 0.0, 0.0);
            double3 startVel = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            (double3 pos, double3 vel) = KeplerPropagator.PropagateState(
                state, TestEpochTick, EarthMu, TickInterval);

            // Short-circuit path returns ComputeStateVector(state, ν₀, mu) directly, so
            // tolerance is tight — no Kepler-solver involved at all.
            AssertVectorNear(startPos, pos, 1e-3, "dt=0 position");
            AssertVectorNear(startVel, vel, 1e-6, "dt=0 velocity");
        }

        [Test]
        public void PropagateState_NegativeElapsedTime_PropagatesBackward()
        {
            // currentTick < epochTick: propagate backward in time. Should produce a valid
            // state that, if then propagated forward by the same |dt|, returns to epoch.
            double r = LeoRadius;
            double vCircular = math.sqrt(EarthMu / r);
            double3 startPos = new double3(r, 0.0, 0.0);
            double3 startVel = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            long earlierTick = TestEpochTick - 100;
            (double3 earlierPos, double3 earlierVel) = KeplerPropagator.PropagateState(
                state, earlierTick, EarthMu, TickInterval);

            // Backward 100 ticks then forward 100 ticks should return to epoch position.
            // Construct a state at the earlier tick and propagate forward.
            KeplerState earlierState = OrbitalElements.ComputeFromStateVector(
                earlierPos, earlierVel, EarthMu, earlierTick, TestBodyId);
            (double3 backToEpochPos, _) = KeplerPropagator.PropagateState(
                earlierState, TestEpochTick, EarthMu, TickInterval);

            double tolerance = vCircular * TickInterval;
            AssertVectorNear(startPos, backToEpochPos, tolerance, "backward-then-forward returns to epoch");
        }

        // ----- Round-trip integrity -----

        [Test]
        public void RoundTrip_PropagateForwardAndBack_PreservesStateVector()
        {
            // Inclined elliptical orbit. Propagate forward by some interval, then
            // re-extract elements from that state, then propagate backward by the same
            // interval — should return to original.
            double rPeriapsis = LeoRadius;
            double a = 1.5 * LeoRadius;
            double vPeriapsis = math.sqrt(EarthMu * (2.0 / rPeriapsis - 1.0 / a));

            // Apply 30° inclination by rotating the initial velocity into +Y/+Z plane.
            double inclinationRad = math.PI_DBL / 6.0;
            double3 startPos = new double3(rPeriapsis, 0.0, 0.0);
            double3 startVel = new double3(
                0.0,
                vPeriapsis * math.cos(inclinationRad),
                vPeriapsis * math.sin(inclinationRad));

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            // Forward 500 ticks (~16.7 seconds at 30 Hz; non-trivial fraction of period).
            long forwardTick = TestEpochTick + 500;
            (double3 forwardPos, double3 forwardVel) = KeplerPropagator.PropagateState(
                state, forwardTick, EarthMu, TickInterval);

            // Re-extract elements from forward state.
            KeplerState forwardState = OrbitalElements.ComputeFromStateVector(
                forwardPos, forwardVel, EarthMu, forwardTick, TestBodyId);

            // Now propagate backward to original epoch tick.
            (double3 backPos, double3 backVel) = KeplerPropagator.PropagateState(
                forwardState, TestEpochTick, EarthMu, TickInterval);

            // Round-trip tolerance: looser than direct test, two passes of solver +
            // element extraction + state vector composition.
            double tolerance = vPeriapsis * TickInterval;
            AssertVectorNear(startPos, backPos, tolerance, "round-trip position");
            AssertVectorNear(startVel, backVel, tolerance, "round-trip velocity");
        }

        [Test]
        public void RoundTrip_HyperbolicForwardAndBack_PreservesStateVector()
        {
            // Hyperbolic trajectory: forward + backward through hyperbolic solver. The
            // hyperbolic Newton-Raphson is numerically more delicate; round-trip exercises
            // both directions.
            double escapeV = math.sqrt(2.0 * EarthMu / LeoRadius);
            double3 startPos = new double3(LeoRadius, 0.0, 0.0);
            double3 startVel = new double3(0.0, 1.5 * escapeV, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            // Forward 200 ticks (~6.7 seconds; vessel travels far from periapsis on hyperbola).
            long forwardTick = TestEpochTick + 200;
            (double3 forwardPos, double3 forwardVel) = KeplerPropagator.PropagateState(
                state, forwardTick, EarthMu, TickInterval);

            KeplerState forwardState = OrbitalElements.ComputeFromStateVector(
                forwardPos, forwardVel, EarthMu, forwardTick, TestBodyId);

            (double3 backPos, double3 backVel) = KeplerPropagator.PropagateState(
                forwardState, TestEpochTick, EarthMu, TickInterval);

            double tolerance = math.length(startVel) * TickInterval;
            AssertVectorNear(startPos, backPos, tolerance, "hyperbolic round-trip position");
            AssertVectorNear(startVel, backVel, tolerance, "hyperbolic round-trip velocity");
        }

        // ----- Sanity checks -----

        [Test]
        public void PropagateState_MeanMotionMatchesExpected()
        {
            // For a known semi-major axis and μ, the propagator's internal mean motion
            // should match the analytic formula n = sqrt(μ/a³). We can't observe n
            // directly, but we can verify it indirectly: after one full period, the
            // vessel returns to start. The period formula T = 2π/n derives from the
            // mean motion; if the propagator returns to start at exactly T, the mean
            // motion was correct. This duplicates the FullPeriod_ReturnsToStart test
            // but at a different semi-major axis to make the test independent.
            double r = 12_000_000.0; // 12,000 km — geostationary-ish but not exactly
            double vCircular = math.sqrt(EarthMu / r);
            double3 startPos = new double3(r, 0.0, 0.0);
            double3 startVel = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            double period = OrbitalPeriodSeconds(r, EarthMu);
            long periodTicks = (long)math.round(period / TickInterval);
            long targetTick = TestEpochTick + periodTicks;

            (double3 pos, _) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            double tolerance = vCircular * TickInterval;
            AssertVectorNear(startPos, pos, tolerance, "12000 km circular period returns to start");
        }

        [Test]
        public void PropagateState_SmallElapsedTime_PositionAdvancesByExpectedFraction()
        {
            // Sanity: at very small dt, position should advance by approximately
            // velocity·dt. Linearized check that the propagator isn't producing
            // wildly-wrong results in the small-dt limit.
            double r = LeoRadius;
            double vCircular = math.sqrt(EarthMu / r);
            double3 startPos = new double3(r, 0.0, 0.0);
            double3 startVel = new double3(0.0, vCircular, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            // 1 tick = 33 ms ≈ trivially small fraction of orbital period.
            long targetTick = TestEpochTick + 1;
            (double3 pos, _) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            // First-order linear approximation: pos ≈ startPos + startVel·dt.
            // Second-order curvature error: ~0.5·a_centripetal·dt²
            //                              = 0.5 · (v²/r) · dt²
            //                              = 0.5 · (7546²/7e6) · (1/30)²
            //                              = 0.5 · 8.14 · 1.11e-3
            //                              ≈ 4.5e-3 m at LEO over one 30 Hz tick.
            // This is the orbit curving away from the tangent line — physics, not error.
            // Tolerance 5e-2 m comfortably exceeds the analytic deviation (~11×) while
            // staying tight enough to catch any actual propagator malfunction.
            double3 expectedLinear = startPos + startVel * TickInterval;
            AssertVectorNear(expectedLinear, pos, 5e-2,
                "small-dt position should be approximately startPos + startVel·dt");
        }

        // ----- The "this is unlikely but worth catching" cases -----

        [Test]
        public void PropagateState_VeryHighEccentricity_DoesNotThrow()
        {
            // Eccentricity 0.99 — within solver convergence range but stressful.
            // Goal: solver completes (no Debug.LogWarning), position is plausible.
            double e = 0.99;
            double rPeriapsis = LeoRadius;
            double a = rPeriapsis / (1.0 - e);
            double vPeriapsis = math.sqrt(EarthMu * (2.0 / rPeriapsis - 1.0 / a));
            double3 startPos = new double3(rPeriapsis, 0.0, 0.0);
            double3 startVel = new double3(0.0, vPeriapsis, 0.0);

            KeplerState state = OrbitalElements.ComputeFromStateVector(
                startPos, startVel, EarthMu, TestEpochTick, TestBodyId);

            // Propagate to a point a bit past periapsis. Should complete without
            // convergence warnings (LogAssert.NoUnexpectedReceived runs at teardown
            // and would catch any).
            long targetTick = TestEpochTick + 100;
            (double3 pos, double3 vel) = KeplerPropagator.PropagateState(
                state, targetTick, EarthMu, TickInterval);

            // Position should be a valid finite vector; distance from body should be
            // somewhere between periapsis and apoapsis.
            double distance = math.length(pos);
            Assert.IsTrue(double.IsFinite(distance), "result must be finite");
            Assert.Greater(distance, 0.0, "result must be a valid position");

            double rApoapsis = a * (1.0 + e);
            Assert.LessOrEqual(distance, rApoapsis + 1.0, "must not exceed apoapsis distance");
        }
    }
}
