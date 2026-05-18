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
    /// <see cref="ParabolicInstabilityBand"/> of 1.0, the propagator routes through whichever
    /// branch the comparison `e &lt; 1.0` happens to pick; expect O(1e-3) position error in
    /// that band. Behavior outside the band is the canonical algorithm.
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
    ///     iterations for e &lt; 0.99. Hard limit at <see cref="MaxKeplerIterations"/>.
    ///   - For e &gt; 0.99, convergence can require more iterations; the solver still
    ///     terminates within the iteration limit but precision may degrade slightly.
    ///   - Hyperbolic Newton-Raphson with Conway's logarithmic starter for large M.
    ///     Stable up to hyperbolic anomaly H ~ 10 (extremely high-energy flyby);
    ///     beyond that, sinh(H) loses precision via floating-point overflow approach.
    ///   - Tolerance <see cref="KeplerConvergenceTolerance"/> = 1e-10 radians. At LEO
    ///     scale, this corresponds to ~7e-4 m of position error from solver
    ///     convergence — below the 1e-3 m round-trip tolerance used in commit 038 tests.
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
        /// <summary>
        /// Convergence tolerance for Newton-Raphson on Kepler's equation, in radians.
        /// At LEO scale (~7e6 m), 1e-10 rad ≈ 7e-4 m position error from solver
        /// convergence — comfortably below downstream tolerances.
        /// </summary>
        public const double KeplerConvergenceTolerance = 1e-10;

        /// <summary>
        /// Maximum Newton-Raphson iterations before declaring non-convergence. Typical
        /// convergence is 3-5 iterations with Conway's starter; 15 is a generous upper
        /// bound that catches pathological cases (e &gt; 0.99 with adversarial M).
        /// </summary>
        public const int MaxKeplerIterations = 15;

        /// <summary>
        /// Eccentricity band around 1.0 where the elliptic-vs-hyperbolic dispatch produces
        /// O(1e-3 m) position error due to numerical instability near the parabolic limit.
        /// See class XML doc for justification of not adding an explicit Barker's-equation
        /// branch. Width is asymmetric: 1e-8 on both sides, total band [1 - 1e-8, 1 + 1e-8].
        /// </summary>
        public const double ParabolicInstabilityBand = 1e-8;

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

            double a = state.SemiMajorAxis;
            double e = state.Eccentricity;

            // Step 2: mean motion. Always positive magnitude; orbit direction is encoded
            // geometrically (inclination, RAAN, arg-of-periapsis), not in the sign of n.
            // Uses |a| so the formula works for both elliptical (a>0) and hyperbolic
            // (a<0) orbits with the same expression.
            double absA = math.abs(a);
            double n = math.sqrt(mu / (absA * absA * absA));

            // Step 3, 4, 5, 6: dispatch on orbit type. Note that e exactly equal to 1.0 is
            // not handled specially; the comparison routes such cases through whichever
            // branch the e < 1.0 test happens to pick. See class XML doc for the
            // parabolic-omission rationale.
            double currentTrueAnomaly;
            if (e < 1.0)
            {
                // Elliptical branch.
                double meanAnomalyAtEpoch = TrueToMeanAnomalyElliptic(state.TrueAnomalyAtEpoch, e);
                double meanAnomaly = meanAnomalyAtEpoch + n * dt;
                // Wrap mean anomaly into [0, 2π) for solver stability; long propagation
                // intervals (years of game time at high warp) can accumulate M into the
                // tens of thousands of radians, which compounds Newton-Raphson roundoff.
                meanAnomaly = WrapTwoPi(meanAnomaly);
                double eccentricAnomaly = SolveKeplerElliptic(meanAnomaly, e);
                currentTrueAnomaly = EccentricToTrueAnomalyElliptic(eccentricAnomaly, e);
            }
            else
            {
                // Hyperbolic branch (and e ≈ 1 numerical-instability band).
                double meanAnomalyAtEpoch = TrueToMeanAnomalyHyperbolic(state.TrueAnomalyAtEpoch, e);
                double meanAnomaly = meanAnomalyAtEpoch + n * dt;
                // Hyperbolic mean anomaly is NOT wrapped — there's no period on an
                // unbound trajectory, and hyperbolic anomaly H is unbounded on (-∞, +∞).
                double hyperbolicAnomaly = SolveKeplerHyperbolic(meanAnomaly, e);
                currentTrueAnomaly = HyperbolicToTrueAnomalyHyperbolic(hyperbolicAnomaly, e);
            }

            // Step 7: get state vector from elements + propagated ν.
            return OrbitalElements.ComputeStateVector(state, currentTrueAnomaly, mu);
        }

        // ----- Elliptic-branch math -----

        /// <summary>
        /// Convert true anomaly ν to mean anomaly M for an elliptical orbit (e &lt; 1).
        /// Two-step: ν → E (eccentric anomaly) → M (mean anomaly).
        ///   E = 2 · atan2(sqrt(1-e) · sin(ν/2), sqrt(1+e) · cos(ν/2))
        ///   M = E - e · sin(E)
        /// </summary>
        private static double TrueToMeanAnomalyElliptic(double trueAnomaly, double e)
        {
            double halfNu = trueAnomaly * 0.5;
            double sqrtOneMinusE = math.sqrt(1.0 - e);
            double sqrtOnePlusE = math.sqrt(1.0 + e);
            double eccentricAnomaly = 2.0 * math.atan2(
                sqrtOneMinusE * math.sin(halfNu),
                sqrtOnePlusE * math.cos(halfNu));
            return eccentricAnomaly - e * math.sin(eccentricAnomaly);
        }

        /// <summary>
        /// Convert eccentric anomaly E to true anomaly ν for an elliptical orbit.
        ///   ν = 2 · atan2(sqrt(1+e) · sin(E/2), sqrt(1-e) · cos(E/2))
        /// Inverse of the E-from-ν half of <see cref="TrueToMeanAnomalyElliptic"/>.
        /// </summary>
        private static double EccentricToTrueAnomalyElliptic(double eccentricAnomaly, double e)
        {
            double halfE = eccentricAnomaly * 0.5;
            double sqrtOneMinusE = math.sqrt(1.0 - e);
            double sqrtOnePlusE = math.sqrt(1.0 + e);
            return 2.0 * math.atan2(
                sqrtOnePlusE * math.sin(halfE),
                sqrtOneMinusE * math.cos(halfE));
        }

        /// <summary>
        /// Solve Kepler's equation M = E - e·sin(E) for the eccentric anomaly E given
        /// mean anomaly M and eccentricity e (0 ≤ e &lt; 1). Newton-Raphson with Conway's
        /// 1986 starter for fast convergence.
        ///
        /// Conway's starter:
        ///   E_0 = M + e · sin(M) / (1 - sin(M + e) + sin(M))
        /// Approximately one Halley iteration's worth of work from M; typically gives
        /// Newton-Raphson convergence in 3-5 iterations vs 8-12 for E_0 = M.
        /// </summary>
        private static double SolveKeplerElliptic(double meanAnomaly, double e)
        {
            // Conway's starter. Special-case e == 0 (circular orbit): Kepler's equation
            // is trivially M = E, no iteration needed. Saves a few cycles and avoids
            // any chance of zero-denominator surprise in the starter formula.
            if (e == 0.0)
            {
                return meanAnomaly;
            }

            double sinM = math.sin(meanAnomaly);
            double denominator = 1.0 - math.sin(meanAnomaly + e) + sinM;
            // Conway's starter denominator is bounded away from zero for all
            // (M, e) with e in [0, 1) by the structure of the trig identities involved;
            // but defensively guard against the edge case e ≈ 1 by falling back to
            // E_0 = π when the denominator gets pathologically small.
            double eccentricAnomaly = math.abs(denominator) > 1e-12
                ? meanAnomaly + e * sinM / denominator
                : (meanAnomaly > 0.0 ? math.PI_DBL : -math.PI_DBL);

            // Newton-Raphson: E_{k+1} = E_k - f(E_k) / f'(E_k)
            //   f(E)  = E - e · sin(E) - M
            //   f'(E) = 1 - e · cos(E)
            for (int i = 0; i < MaxKeplerIterations; i++)
            {
                double f = eccentricAnomaly - e * math.sin(eccentricAnomaly) - meanAnomaly;
                if (math.abs(f) < KeplerConvergenceTolerance)
                {
                    return eccentricAnomaly;
                }
                double fPrime = 1.0 - e * math.cos(eccentricAnomaly);
                eccentricAnomaly -= f / fPrime;
            }

            Debug.LogWarning(
                $"KeplerPropagator.SolveKeplerElliptic: Newton-Raphson did not converge " +
                $"within {MaxKeplerIterations} iterations for M = {meanAnomaly:G17}, e = {e:G17}. " +
                $"Returning best estimate {eccentricAnomaly:G17}; position error may exceed tolerance.");
            return eccentricAnomaly;
        }

        // ----- Hyperbolic-branch math -----

        /// <summary>
        /// Convert true anomaly ν to hyperbolic mean anomaly M for a hyperbolic
        /// trajectory (e &gt; 1). Two-step: ν → H (hyperbolic anomaly) → M.
        ///   H = 2 · atanh( sqrt((e-1)/(e+1)) · tan(ν/2) )
        ///   M = e · sinh(H) - H
        /// The hyperbolic anomaly H is the analog of eccentric anomaly E on a hyperbola.
        /// </summary>
        private static double TrueToMeanAnomalyHyperbolic(double trueAnomaly, double e)
        {
            double tanHalfNu = math.tan(trueAnomaly * 0.5);
            double ratio = math.sqrt((e - 1.0) / (e + 1.0)) * tanHalfNu;
            // atanh is defined for |ratio| < 1, which corresponds to ν within the
            // hyperbolic asymptote angle ±acos(-1/e). Outside this range, the trajectory
            // is past the asymptote — physically meaningless. Clamp defensively.
            ratio = math.clamp(ratio, -0.9999999999, 0.9999999999);
            double hyperbolicAnomaly = 2.0 * Atanh(ratio);
            return e * math.sinh(hyperbolicAnomaly) - hyperbolicAnomaly;
        }

        /// <summary>
        /// Convert hyperbolic anomaly H to true anomaly ν for a hyperbolic trajectory.
        ///   tan(ν/2) = sqrt((e+1)/(e-1)) · tanh(H/2)
        /// </summary>
        private static double HyperbolicToTrueAnomalyHyperbolic(double hyperbolicAnomaly, double e)
        {
            double tanHalfNu = math.sqrt((e + 1.0) / (e - 1.0)) * math.tanh(hyperbolicAnomaly * 0.5);
            return 2.0 * math.atan(tanHalfNu);
        }

        /// <summary>
        /// Solve hyperbolic Kepler equation M = e·sinh(H) - H for H given mean anomaly M
        /// and eccentricity e (e &gt; 1). Newton-Raphson with Conway's logarithmic starter
        /// for large |M|.
        ///
        /// Initial guess (Conway 1986):
        ///   - For small |M| relative to e:  H_0 ≈ M / (e - 1)
        ///   - For large |M|:                H_0 ≈ sign(M) · ln(2·|M|/e + 1.8)
        /// </summary>
        private static double SolveKeplerHyperbolic(double meanAnomaly, double e)
        {
            // Conway's starter: switch criterion at |M| > 4·e (heuristic from Markley).
            double hyperbolicAnomaly;
            double absM = math.abs(meanAnomaly);
            if (absM < 4.0 * e)
            {
                hyperbolicAnomaly = meanAnomaly / (e - 1.0);
            }
            else
            {
                hyperbolicAnomaly = math.sign(meanAnomaly) * math.log(2.0 * absM / e + 1.8);
            }

            // Newton-Raphson: H_{k+1} = H_k - f(H_k) / f'(H_k)
            //   f(H)  = e · sinh(H) - H - M
            //   f'(H) = e · cosh(H) - 1
            for (int i = 0; i < MaxKeplerIterations; i++)
            {
                double f = e * math.sinh(hyperbolicAnomaly) - hyperbolicAnomaly - meanAnomaly;
                if (math.abs(f) < KeplerConvergenceTolerance)
                {
                    return hyperbolicAnomaly;
                }
                double fPrime = e * math.cosh(hyperbolicAnomaly) - 1.0;
                hyperbolicAnomaly -= f / fPrime;
            }

            Debug.LogWarning(
                $"KeplerPropagator.SolveKeplerHyperbolic: Newton-Raphson did not converge " +
                $"within {MaxKeplerIterations} iterations for M = {meanAnomaly:G17}, e = {e:G17}. " +
                $"Returning best estimate {hyperbolicAnomaly:G17}; position error may exceed tolerance.");
            return hyperbolicAnomaly;
        }

        // ----- Utility -----

        /// <summary>
        /// Inverse hyperbolic tangent. Unity.Mathematics does not expose atanh directly,
        /// so this is the standard identity: atanh(x) = 0.5 · ln((1+x)/(1-x)) for |x| &lt; 1.
        /// </summary>
        private static double Atanh(double x)
        {
            return 0.5 * math.log((1.0 + x) / (1.0 - x));
        }

        /// <summary>
        /// Wrap a value into the range [0, 2π). Used to keep elliptical mean anomaly
        /// bounded after long propagation intervals so Newton-Raphson roundoff doesn't
        /// compound. Hyperbolic mean anomaly is NOT wrapped (no period; H is unbounded).
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
