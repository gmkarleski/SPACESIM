namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Per-orbit predictor for the next surface-impact crossing — the sim-tick at
    /// which a vessel's Kepler-rails orbit reaches the body's surface radius
    /// (<see cref="ReferenceBody.SurfaceRadiusMeters"/>). Pure function of the
    /// orbital state, the current body, the current sim-tick, and the tick interval.
    /// No state, no caching, no side effects — matches CONSTRAINTS §2's locked
    /// design ("Event predictors are pure functions of orbital and trajectory
    /// state").
    ///
    /// <para>MATH (delegated to <see cref="OrbitalElements.SolveConicAtRadius"/>):</para>
    /// Solves <c>r(ν) = SurfaceRadiusMeters</c> on the orbit using the conic
    /// equation. Returns the absolute sim-tick of the next crossing, or null if no
    /// crossing is reachable (orbit's periapsis above the surface — vessel never
    /// impacts — or numerical degenerate cases).
    ///
    /// <para>IMPACT vs LAUNCH SEMANTICS:</para>
    /// The predictor returns the next time the orbit reaches the surface radius,
    /// regardless of direction. For vessels with periapsis below the surface
    /// (vessel orbit intersects the planet), this is the impact tick — the sim-tick
    /// at which the vessel would lithobrake if propagated unchanged. The predictor
    /// does NOT predict surface departure (launch) as a separate event type; launch
    /// is a player-driven thrust event, not an orbit-intersects-surface event, and
    /// is outside the analytic event-prediction scope per CONSTRAINTS §2.
    ///
    /// <para>PHASE 1 INTERPRETATION:</para>
    /// In Phase 1 with stationary bodies, "the vessel's orbit intersects the surface"
    /// is a well-defined geometric statement about the conic-vs-sphere intersection.
    /// In Phase 4+ when bodies orbit, the predictor will need to account for the
    /// body's motion during the prediction horizon — surface-impact becomes a
    /// vessel-conic-vs-moving-sphere problem (no closed form in general). The
    /// signature stays stable across that migration; the math path inside
    /// <see cref="OrbitalElements.SolveConicAtRadius"/> upgrades.
    ///
    /// <para>CONSTANT-BODY-POSITION ASSUMPTION (PHASE 0/1):</para>
    /// The predictor assumes the current body's position is constant across the
    /// prediction horizon. Phase 0/1 honors this; Phase 4+ revisit required. Same
    /// caveat as <see cref="AtmosphericEntryPredictor"/> and
    /// <see cref="SoiCrossingPredictor"/>'s outward path — all three share the
    /// same conic-equation helper.
    ///
    /// <para>NO <c>DetectionAggressiveness</c> PARAMETER:</para>
    /// Same rationale as <see cref="AtmosphericEntryPredictor"/>: one math path
    /// (closed-form conic solve), no sampling granularity to tune, asymmetry with
    /// <see cref="SoiCrossingPredictor"/>'s signature is math-driven. CONSTRAINTS §2's
    /// "aggressively detects surface impact" commitment is satisfied automatically —
    /// the closed-form is exact for the constant-body case.
    ///
    /// <para>POPULATES <see cref="KeplerState.NextModeTransitionTick"/>:</para>
    /// The driver aggregates this predictor's output with the atmospheric-entry
    /// predictor's output via min-of-both. The combined earliest tick is written to
    /// <see cref="KeplerState.NextModeTransitionTick"/>; the trigger evaluator
    /// (commit 043) reads that field to detect imminent K→P transitions. Both
    /// predictors writing to the same field means the trigger fires for whichever
    /// event arrives first — usually atmospheric entry (higher threshold) before
    /// surface impact (lower threshold), but the predictor doesn't enforce ordering
    /// — both ticks are computed independently and the aggregation is purely on the
    /// values.
    /// </summary>
    public static class SurfaceImpactPredictor
    {
        /// <summary>
        /// Compute the absolute sim-tick of the next surface-radius crossing on the
        /// vessel's orbit. Returns null on orbits that don't reach the surface
        /// (periapsis above surface) or numerical degenerate cases (see
        /// <see cref="OrbitalElements.SolveConicAtRadius"/> for the full null-case
        /// catalog).
        /// </summary>
        /// <param name="state">Vessel's current Kepler state (orbital elements + epoch).</param>
        /// <param name="currentBody">Reference body the vessel is currently parented
        /// to. Surface radius and μ derive from this.</param>
        /// <param name="currentTick">Current sim-tick; predictions are returned in
        /// absolute tick coordinates.</param>
        /// <param name="tickIntervalSeconds">Seconds per sim-tick (1/30 in Phase 1).</param>
        /// <returns>Absolute sim-tick of the next surface-radius crossing, or null
        /// if no impact is predicted.</returns>
        public static long? PredictNextImpact(
            KeplerState state,
            ReferenceBody currentBody,
            long currentTick,
            double tickIntervalSeconds)
        {
            if (state == null) return null;
            if (currentBody == null) return null;

            double surfaceRadius = currentBody.SurfaceRadiusMeters;

            return OrbitalElements.SolveConicAtRadius(
                state, surfaceRadius, currentTick, currentBody.Mu, tickIntervalSeconds);
        }
    }
}
