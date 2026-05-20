using Unity.Mathematics;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Per-orbit predictor for the next periapsis and apoapsis passages. Pure
    /// function of <see cref="KeplerState"/>, current tick, μ, and tick interval.
    /// No state, no caching, no side effects — matches CONSTRAINTS §2's locked
    /// design ("Event predictors are pure functions of orbital and trajectory
    /// state").
    ///
    /// <para>ALGORITHM:</para>
    /// <list type="number">
    ///   <item>Compute mean motion n = sqrt(μ / |a|³) via
    ///   <see cref="OrbitalElements.MeanMotion"/>.</item>
    ///   <item>Advance the epoch mean anomaly forward by n·dt where
    ///   dt = (currentTick − epochTick) · tickIntervalSeconds.</item>
    ///   <item>For elliptical orbits (e &lt; 1): periapsis is at M=0 (equivalently
    ///   M=2π wrapped). Time to next periapsis = (2π − M_now mod 2π) / n.
    ///   Apoapsis is at M=π. Time to next apoapsis = ((π − M_now mod 2π) + 2π) mod 2π) / n.</item>
    ///   <item>For hyperbolic orbits (e &gt; 1): periapsis is at M=0, reached at most
    ///   once. If M_now &lt;= 0 (vessel hasn't reached periapsis yet), periapsis time
    ///   = (−M_now) / n. If M_now &gt; 0 (already past periapsis), return null.
    ///   Apoapsis doesn't exist on hyperbolic orbits — always null.</item>
    ///   <item>Convert time-to-event seconds to ticks (Δticks = ⌈Δseconds /
    ///   tickIntervalSeconds⌉) and add to currentTick.</item>
    /// </list>
    ///
    /// <para>OVERFLOW DEFENSE:</para>
    /// If a predicted time-to-event exceeds <c>long.MaxValue / 2</c> ticks (effectively
    /// "no event in any reasonable timeframe" — happens for near-parabolic orbits
    /// where the period stretches toward infinity), the predictor returns null for
    /// that event type rather than overflowing the tick arithmetic. The /2 leaves
    /// headroom for currentTick + Δticks addition without wrapping.
    ///
    /// <para>PARABOLIC EDGE CASE:</para>
    /// Eccentricity in <c>[1 - 1e-8, 1 + 1e-8]</c> (the parabolic-instability band per
    /// <see cref="OrbitalElements.ParabolicInstabilityBand"/>) is routed through the
    /// hyperbolic branch (e &gt;= 1 comparison). The predictor doesn't throw or
    /// produce NaN in this band; it produces approximate answers per the
    /// documented numerical-instability behavior of
    /// <see cref="OrbitalElements.TrueToMeanAnomaly"/>. The overflow defense above
    /// catches the "period approaches infinity" cases by returning null.
    ///
    /// <para>DETECTION-AGGRESSIVENESS:</para>
    /// Phase 1 ships only the pragmatic detection-aggressiveness per CONSTRAINTS §2
    /// ("Phase 1 ships the pragmatic version of each predictor. The difficulty
    /// toggle that exposes the strict-mode value becomes available in Phase 5/6").
    /// No parameter is exposed at this stage; the predictor returns the pragmatic
    /// (always-detect-when-possible) result. When the strict-mode toggle lands,
    /// this signature extends with a <c>DetectionAggressiveness</c> enum parameter.
    /// </summary>
    public static class PeriapsisApoapsisPredictor
    {
        /// <summary>
        /// Compute the sim-tick of the next periapsis and next apoapsis passage for
        /// the given orbit. See class XML doc for algorithm + edge cases.
        /// </summary>
        /// <param name="state">Vessel's current Kepler state (orbital elements + epoch).</param>
        /// <param name="currentTick">Current sim-tick; predictions are returned in
        /// absolute tick coordinates.</param>
        /// <param name="mu">Reference body's gravitational parameter μ = G·M in m³/s².</param>
        /// <param name="tickIntervalSeconds">Seconds per sim-tick (1/30 ≈ 0.0333s
        /// in Phase 1; pass <c>SimTickController.SimTickIntervalSeconds</c>).</param>
        /// <returns>
        /// Tuple of (next periapsis tick, next apoapsis tick). Either or both may
        /// be null per the algorithm rules: apoapsis is always null for hyperbolic;
        /// periapsis is null for hyperbolic-past-periapsis; both are null if the
        /// predicted tick would overflow.
        /// </returns>
        public static (long? periapsisTick, long? apoapsisTick) Predict(
            KeplerState state,
            long currentTick,
            double mu,
            double tickIntervalSeconds)
        {
            double e = state.Eccentricity;
            double n = OrbitalElements.MeanMotion(state.SemiMajorAxis, mu);

            // Advance epoch mean anomaly to current tick.
            double dtSeconds = (currentTick - state.EpochTick) * tickIntervalSeconds;
            double meanAnomalyAtEpoch = OrbitalElements.TrueToMeanAnomaly(
                state.TrueAnomalyAtEpoch, e);
            double meanAnomalyNow = meanAnomalyAtEpoch + n * dtSeconds;

            if (e < 1.0)
            {
                return PredictElliptical(meanAnomalyNow, n, currentTick, tickIntervalSeconds);
            }
            else
            {
                return PredictHyperbolic(meanAnomalyNow, n, currentTick, tickIntervalSeconds);
            }
        }

        /// <summary>
        /// Elliptical branch: both periapsis and apoapsis are reached every period.
        /// Wrap mean anomaly into [0, 2π) and compute time-to-next-{0, π}.
        /// </summary>
        private static (long? periapsisTick, long? apoapsisTick) PredictElliptical(
            double meanAnomalyNow,
            double n,
            long currentTick,
            double tickIntervalSeconds)
        {
            double twoPi = 2.0 * math.PI_DBL;
            double mWrapped = meanAnomalyNow % twoPi;
            if (mWrapped < 0.0) mWrapped += twoPi;

            // Time to next periapsis (next M=0, equivalently M=2π from current wrapped).
            // If mWrapped == 0 exactly, we're AT periapsis — return one full period
            // for "the next one" semantics (not "this one"). The (2π - 0) = 2π
            // expression naturally produces this. If mWrapped is just past 0 (e.g.,
            // 0.0001), the next periapsis is nearly a full period away — also correct.
            double secondsToPeriapsis = (twoPi - mWrapped) / n;

            // Time to next apoapsis (next M=π).
            // If mWrapped < π: next apoapsis at M=π, time = (π - mWrapped) / n.
            // If mWrapped >= π: next apoapsis wraps around, time = (π - mWrapped + 2π) / n.
            double mToApoapsis = math.PI_DBL - mWrapped;
            if (mToApoapsis <= 0.0) mToApoapsis += twoPi;
            double secondsToApoapsis = mToApoapsis / n;

            long? periapsisTick = SecondsToAbsoluteTick(secondsToPeriapsis, currentTick, tickIntervalSeconds);
            long? apoapsisTick = SecondsToAbsoluteTick(secondsToApoapsis, currentTick, tickIntervalSeconds);
            return (periapsisTick, apoapsisTick);
        }

        /// <summary>
        /// Hyperbolic branch: periapsis is reached at most once; apoapsis doesn't
        /// exist. Sign of mean anomaly tells past-vs-future.
        /// </summary>
        private static (long? periapsisTick, long? apoapsisTick) PredictHyperbolic(
            double meanAnomalyNow,
            double n,
            long currentTick,
            double tickIntervalSeconds)
        {
            // Apoapsis is always null on hyperbolic trajectories.
            long? apoapsisTick = null;

            // Periapsis (M=0): if meanAnomalyNow > 0, vessel is past periapsis on
            // its outbound leg — never comes back. Return null. If meanAnomalyNow <=
            // 0, vessel is approaching periapsis; time = (0 - meanAnomalyNow) / n =
            // (-meanAnomalyNow) / n.
            long? periapsisTick = null;
            if (meanAnomalyNow <= 0.0)
            {
                double secondsToPeriapsis = -meanAnomalyNow / n;
                periapsisTick = SecondsToAbsoluteTick(secondsToPeriapsis, currentTick, tickIntervalSeconds);
            }

            return (periapsisTick, apoapsisTick);
        }

        /// <summary>
        /// Convert a positive seconds-until-event value to an absolute sim-tick by
        /// adding to <paramref name="currentTick"/>. Returns null if the result
        /// would overflow (predicted ticks-until-event &gt; long.MaxValue/2),
        /// which happens for near-parabolic orbits where the period stretches
        /// toward infinity. The /2 leaves headroom for currentTick + Δticks
        /// addition without wrapping.
        /// </summary>
        private static long? SecondsToAbsoluteTick(
            double secondsUntilEvent,
            long currentTick,
            double tickIntervalSeconds)
        {
            // Safety: NaN/infinity → null (parabolic-instability-band degenerate cases).
            if (double.IsNaN(secondsUntilEvent) || double.IsInfinity(secondsUntilEvent))
            {
                return null;
            }

            // Compute ticks-until-event. Round up because warp lands ON event ticks
            // exactly (per netcode contract §4.2); rounding down would miss the event.
            double ticksUntilEventD = math.ceil(secondsUntilEvent / tickIntervalSeconds);

            // Overflow defense: if the result would push absolute tick past
            // long.MaxValue/2, return null. Real-orbit periods stay well within
            // this bound; near-parabolic orbits with stretched periods hit it.
            if (ticksUntilEventD > (double)(long.MaxValue / 2))
            {
                return null;
            }
            if (ticksUntilEventD < 0.0)
            {
                // Defensive: caller passed a negative seconds value somehow. Return
                // null rather than producing a negative absolute tick (which would
                // be a past event, not a future event).
                return null;
            }

            return currentTick + (long)ticksUntilEventD;
        }
    }
}
