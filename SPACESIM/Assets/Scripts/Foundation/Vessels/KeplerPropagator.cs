using System;
using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Time-advancement of Keplerian orbital state. Given a <see cref="KeplerState"/>
    /// captured at epoch tick N and the current sim-tick M, returns the state vector
    /// (position, velocity) at M.
    ///
    /// Stateless. Pure math, no Unity scene dependencies beyond Unity.Mathematics types
    /// and <c>Debug.LogWarning</c> for the (rare) solver-convergence-failure path.
    ///
    /// USAGE:
    /// <code>
    /// (double3 pos, double3 vel) = KeplerPropagator.PropagateState(
    ///     vesselState.KeplerState, simTick.TickNumber, body.Mu,
    ///     SimTickController.SimTickIntervalSeconds);
    /// </code>
    ///
    /// ALGORITHM (per Bate-Mueller-White §4.2, Curtis §3.3, Vallado §2.2):
    ///   1. dt = (currentTick - epochTick) * tickIntervalSeconds                  [seconds]
    ///   2. n = sqrt(mu / |a|^3)                                                  [rad/s]
    ///   3. Convert epoch true anomaly ν₀ → mean anomaly M₀ (ellipse or hyperbola)
    ///   4. M = M₀ + n * dt                                                       [rad]
    ///   5. Solve Kepler's equation for E (ellipse) or H (hyperbola) via Newton-Raphson
    ///   6. Convert E or H back to true anomaly ν
    ///   7. Call <see cref="OrbitalElements.ComputeStateVector"/> with ν for (position, velocity)
    ///
    /// BRANCHES (per <see cref="KeplerState.Eccentricity"/>):
    ///   - e &lt; 1.0   → elliptical (Kepler's equation: M = E - e·sin(E))
    ///   - e &gt; 1.0   → hyperbolic (Kepler's equation: M = e·sinh(H) - H)
    ///
    /// PARABOLIC ORBITS (e == 1.0 exactly) are NOT handled. Justification per Phase 0
    /// design discussion: parabolic is a measure-zero set; vessels in gameplay always
    /// have bound (e&lt;1) or unbound (e&gt;1) trajectories. The numerical instability band
    /// around e=1 is narrow (~1e-8 wide in eccentricity) and producing approximate
    /// answers there is acceptable when the alternative is an explicit Barker's-equation
    /// branch for a case that never appears. If e is within
    /// <see cref="OrbitalElements.ParabolicInstabilityBand"/> of 1.0, the propagator
    /// routes through whichever branch the comparison `e &lt; 1.0` happens to pick;
    /// expect O(1e-3) position error in that band. Behavior outside the band is the
    /// canonical algorithm.
    ///
    /// RETROGRADE ORBITS: handled implicitly. The orbit's geometric encoding (inclination
    /// &gt; π/2, plus argument-of-periapsis and longitude-of-ascending-node) carries the
    /// motion direction. Mean motion n = sqrt(mu / |a|^3) is always positive (a magnitude,
    /// not signed); mean anomaly advances positively in time regardless of orbit
    /// direction; the rotation matrix in <see cref="OrbitalElements.ComputeStateVector"/>
    /// flips sign for retrograde orbits because cos(i) goes negative. No special case
    /// needed in the propagator.
    ///
    /// NUMERICAL STABILITY:
    ///   - Elliptic Newton-Raphson with Conway's starter (Conway 1986) converges in 3-5
    ///     iterations for e &lt; 0.99. Hard limit at
    ///     <see cref="OrbitalElements.MaxKeplerIterations"/>.
    ///   - For e &gt; 0.99, convergence can require more iterations; the solver still
    ///     terminates within the iteration limit but precision may degrade slightly.
    ///   - Hyperbolic Newton-Raphson with Conway's logarithmic starter for large M.
    ///     Stable up to hyperbolic anomaly H ~ 10 (extremely high-energy flyby);
    ///     beyond that, sinh(H) loses precision via floating-point overflow approach.
    ///   - Tolerance <see cref="OrbitalElements.KeplerConvergenceTolerance"/> = 1e-10
    ///     radians. At LEO scale, this corresponds to ~7e-4 m of position error from
    ///     solver convergence — below the 1e-3 m round-trip tolerance used in commit 038 tests.
    ///   - Anomaly conversions (<see cref="OrbitalElements.TrueToMeanAnomaly"/> and
    ///     <see cref="OrbitalElements.MeanToTrueAnomaly"/>) extracted to OrbitalElements
    ///     at commit 045 (Stage 1) so the same math is reusable by predictor work.
    ///
    /// REFERENCES:
    ///  - Conway, B.A. (1986), "An improved algorithm due to Laguerre for the solution of
    ///    Kepler's equation", Celestial Mechanics 39:199–211.
    ///  - Markley, F.L. (1995), "Kepler equation solver", Celestial Mechanics 63:101–111.
    ///  - Bate, Mueller, White, "Fundamentals of Astrodynamics" §4.2.
    ///  - Curtis, "Orbital Mechanics for Engineering Students" §3.3 (elliptic), §3.4 (hyperbolic).
    ///  - Vallado, "Fundamentals of Astrodynamics and Applications" §2.2.
    /// </summary>
    public static class KeplerPropagator
    {
        // Convergence tolerance, max iterations, and parabolic-instability-band constants
        // moved to OrbitalElements at commit 045 (Stage 1) so anomaly conversions can be
        // reused by predictor work. Reference the constants there:
        //   - OrbitalElements.KeplerConvergenceTolerance = 1e-10
        //   - OrbitalElements.MaxKeplerIterations = 15
        //   - OrbitalElements.ParabolicInstabilityBand = 1e-8

        /// <summary>
        /// Propagate a <see cref="KeplerState"/> from its epoch tick to the specified
        /// current tick. Returns position and velocity relative to the reference body
        /// (not absolute world coordinates — caller adds body position).
        /// </summary>
        /// <param name="state">Orbital elements captured at <see cref="KeplerState.EpochTick"/>.</param>
        /// <param name="currentTick">Sim-tick to propagate to.</param>
        /// <param name="mu">Standard gravitational parameter μ = G · M of the reference body, m³/s².</param>
        /// <param name="tickIntervalSeconds">
        /// Seconds per sim-tick. Pass <c>SimTickController.SimTickIntervalSeconds</c>
        /// (1/30 ≈ 0.0333...). Taken as a parameter rather than a global so the math is
        /// testable independently of the controller's const value.
        /// </param>
        /// <returns>
        /// (position relative to body in meters, velocity in m/s) at the specified tick.
        /// </returns>
        public static (double3 position, double3 velocity) PropagateState(
            KeplerState state,
            long currentTick,
            double mu,
            double tickIntervalSeconds)
        {
            // Step 1: elapsed time in seconds. Can be negative; the math handles backward
            // propagation cleanly (mean anomaly just advances negatively).
            double dt = (currentTick - state.EpochTick) * tickIntervalSeconds;

            // Step 1.5: short-circuit for zero elapsed time. Avoid all subsequent math
            // and float artifacts when dt is exactly zero (common in test cases and the
            // immediate-round-trip path in TransitionToPhysXActive after TransitionToKeplerRails).
            if (dt == 0.0)
            {
                return OrbitalElements.ComputeStateVector(state, state.TrueAnomalyAtEpoch, mu);
            }

            double e = state.Eccentricity;

            // Step 2: mean motion. Always positive magnitude; orbit direction is encoded
            // geometrically (inclination, RAAN, arg-of-periapsis), not in the sign of n.
            // Uses |a| so the formula works for both elliptical (a>0) and hyperbolic
            // (a<0) orbits with the same expression. Delegated to OrbitalElements at
            // commit 045 — single source of truth for the mean-motion formula.
            double n = OrbitalElements.MeanMotion(state.SemiMajorAxis, mu);

            // Step 3, 4, 5, 6: dispatch on orbit type. Note that e exactly equal to 1.0 is
            // not handled specially; the comparison routes such cases through whichever
            // branch the e < 1.0 test happens to pick. See class XML doc for the
            // parabolic-omission rationale.
            //
            // Convert epoch true anomaly → epoch mean anomaly, advance by n·dt, convert
            // the advanced mean anomaly back to true anomaly. For elliptical orbits we
            // wrap the advanced mean anomaly into [0, 2π) before the inverse conversion
            // to keep Newton-Raphson roundoff from compounding over long propagation
            // intervals. Hyperbolic mean anomaly is NOT wrapped (no period; H unbounded).
            double meanAnomalyAtEpoch = OrbitalElements.TrueToMeanAnomaly(
                state.TrueAnomalyAtEpoch, e);
            double meanAnomaly = meanAnomalyAtEpoch + n * dt;
            if (e < 1.0)
            {
                meanAnomaly = WrapTwoPi(meanAnomaly);
            }
            double currentTrueAnomaly = OrbitalElements.MeanToTrueAnomaly(meanAnomaly, e);

            // Step 7: get state vector from elements + propagated ν.
            return OrbitalElements.ComputeStateVector(state, currentTrueAnomaly, mu);
        }

        /// <summary>
        /// Wrap a value into the range [0, 2π). Used to keep elliptical mean anomaly
        /// bounded after long propagation intervals so Newton-Raphson roundoff doesn't
        /// compound. Hyperbolic mean anomaly is NOT wrapped (no period; H is unbounded).
        ///
        /// Propagation-specific utility; lives on <see cref="KeplerPropagator"/> rather
        /// than <see cref="OrbitalElements"/> because the wrapping is a propagation
        /// concern (stability across long dt), not an anomaly-conversion concern.
        /// </summary>
        private static double WrapTwoPi(double angle)
        {
            double twoPi = 2.0 * math.PI_DBL;
            double wrapped = angle % twoPi;
            if (wrapped < 0.0) wrapped += twoPi;
            return wrapped;
        }
    }
}
