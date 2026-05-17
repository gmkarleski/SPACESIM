using System;

namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Pure-logic controller for time-warp state and per-mode warp ceilings, per
    /// <c>docs/NETCODE_CONTRACT.md</c> §1.4 (time-warp mechanics).
    ///
    /// Separated from <see cref="SimTickController"/> so the warp math is EditMode-testable
    /// without instantiating any MonoBehaviour. The MonoBehaviour holds an instance of this
    /// class and delegates warp-rate decisions to it.
    ///
    /// Per the contract §1.4: time-warp scales sim-tick advancement, not sim-tick rate.
    /// At 100× warp, the controller advances 100 analytic sim-ticks per real-time FixedUpdate
    /// (every 33.33 ms of real time advances 3.33 s of game time). The 30 Hz rate stays
    /// constant; the work-per-FixedUpdate scales.
    ///
    /// Per-mode warp ceilings:
    ///   <list type="bullet">
    ///     <item><see cref="PhysicsMode.PhysXActive"/>: <see cref="PhysXActiveCeiling"/> = 1×.
    ///       PhysX cannot run faster than real time without artifacts.</item>
    ///     <item><see cref="PhysicsMode.KeplerRails"/>: <see cref="KeplerRailsCeiling"/> = 10,000×.
    ///       Orbit propagation is analytic; arbitrary acceleration is safe.</item>
    ///     <item><see cref="PhysicsMode.InterstellarCruise"/>:
    ///       <see cref="InterstellarCruiseCeiling"/> = 100,000×.
    ///       Cruise is analytic and largely featureless; high warp is the design intent.</item>
    ///   </list>
    ///
    /// The <see cref="EffectiveWarpRate"/> is always <c>min(RequestedWarpRate, CeilingFor(ActiveVesselMode))</c>.
    /// Time-warp advancement is further capped by the analytic event queue: the cycle never
    /// advances past a scheduled event. <see cref="ComputeAnalyticIterations(int)"/> returns
    /// the per-FixedUpdate iteration count, clamped to the event distance.
    /// </summary>
    public sealed class SimTickWarpController
    {
        // ----- Ceilings (constants from the contract §1.4) -----

        /// <summary>Warp ceiling for <see cref="PhysicsMode.PhysXActive"/>: 1×.</summary>
        public const double PhysXActiveCeiling = 1.0;

        /// <summary>Warp ceiling for <see cref="PhysicsMode.KeplerRails"/>: 10,000×.</summary>
        public const double KeplerRailsCeiling = 10_000.0;

        /// <summary>Warp ceiling for <see cref="PhysicsMode.InterstellarCruise"/>: 100,000×.</summary>
        public const double InterstellarCruiseCeiling = 100_000.0;

        // ----- State -----

        /// <summary>
        /// Player-requested warp rate (always ≥ 1.0). The effective rate is this clamped
        /// to the active vessel's mode-specific ceiling.
        /// </summary>
        public double RequestedWarpRate { get; private set; } = 1.0;

        /// <summary>
        /// The current active-vessel physics mode. Determines the warp ceiling. Defaults
        /// to <see cref="PhysicsMode.PhysXActive"/>, the most restrictive mode.
        /// </summary>
        public PhysicsMode ActiveVesselMode { get; private set; } = PhysicsMode.PhysXActive;

        /// <summary>
        /// Effective warp rate after applying the per-mode ceiling. Recomputed on
        /// <see cref="SetRequestedWarp"/> or <see cref="SetActiveVesselMode"/>.
        /// </summary>
        public double EffectiveWarpRate { get; private set; } = 1.0;

        // ----- API -----

        /// <summary>
        /// Set the requested warp rate. Values less than 1.0 are clamped to 1.0 (the
        /// minimum permitted warp; 1× is "real-time"). Recomputes <see cref="EffectiveWarpRate"/>.
        /// </summary>
        public void SetRequestedWarp(double rate)
        {
            if (rate < 1.0) rate = 1.0;
            RequestedWarpRate = rate;
            RecomputeEffective();
        }

        /// <summary>
        /// Set the active vessel's physics mode. Used to determine the warp ceiling.
        /// Recomputes <see cref="EffectiveWarpRate"/> against the new ceiling.
        /// </summary>
        public void SetActiveVesselMode(PhysicsMode mode)
        {
            ActiveVesselMode = mode;
            RecomputeEffective();
        }

        /// <summary>
        /// Recompute <see cref="EffectiveWarpRate"/> from current
        /// <see cref="RequestedWarpRate"/> and <see cref="ActiveVesselMode"/>.
        /// Result is <c>min(RequestedWarpRate, CeilingFor(ActiveVesselMode))</c>.
        /// </summary>
        public void RecomputeEffective()
        {
            double ceiling = CeilingFor(ActiveVesselMode);
            EffectiveWarpRate = Math.Min(RequestedWarpRate, ceiling);
        }

        /// <summary>
        /// Return the warp ceiling for the given physics mode, per the contract §1.4 values.
        /// Throws <see cref="ArgumentOutOfRangeException"/> for unrecognized enum values
        /// (defensive against future enum additions that fall through unhandled).
        /// </summary>
        public static double CeilingFor(PhysicsMode mode)
        {
            switch (mode)
            {
                case PhysicsMode.PhysXActive: return PhysXActiveCeiling;
                case PhysicsMode.KeplerRails: return KeplerRailsCeiling;
                case PhysicsMode.InterstellarCruise: return InterstellarCruiseCeiling;
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        /// <summary>
        /// Compute the analytic-step iteration count for one FixedUpdate, clamped to the
        /// distance to the next scheduled event. Per <c>docs/NETCODE_CONTRACT.md</c> §1.4:
        /// <c>min(floor(EffectiveWarpRate), ticksUntilNextEvent)</c>. Always returns at
        /// least 1.
        /// </summary>
        /// <param name="ticksUntilNextEvent">
        /// Number of sim-ticks until the next scheduled analytic event. Pass
        /// <see cref="int.MaxValue"/> when the event queue is empty (no upcoming event).
        /// </param>
        /// <returns>
        /// The iteration count for this FixedUpdate's analytic-step loop. Clamped to
        /// <c>[1, int.MaxValue]</c> on the low end and to the smaller of the effective
        /// warp rate and the event distance on the high end.
        /// </returns>
        public int ComputeAnalyticIterations(int ticksUntilNextEvent)
        {
            if (ticksUntilNextEvent < 1) ticksUntilNextEvent = 1;

            // Floor the effective warp rate to an integer iteration count.
            long warpAsInt;
            if (EffectiveWarpRate >= int.MaxValue) warpAsInt = int.MaxValue;
            else if (EffectiveWarpRate < 1.0) warpAsInt = 1;
            else warpAsInt = (long)Math.Floor(EffectiveWarpRate);

            long min = Math.Min(warpAsInt, ticksUntilNextEvent);
            if (min < 1) min = 1;
            if (min > int.MaxValue) min = int.MaxValue;
            return (int)min;
        }
    }
}
