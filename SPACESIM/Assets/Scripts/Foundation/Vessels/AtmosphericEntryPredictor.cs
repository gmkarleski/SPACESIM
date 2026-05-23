namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Per-orbit predictor for the next atmospheric-entry crossing — the sim-tick at
    /// which a vessel's Kepler-rails orbit reaches the atmospheric outer boundary
    /// (<c>SurfaceRadiusMeters + AtmosphericTopAltitudeMeters</c>). Pure function of
    /// the orbital state, the current body, the current sim-tick, and the tick
    /// interval. No state, no caching, no side effects — matches CONSTRAINTS §2's
    /// locked design ("Event predictors are pure functions of orbital and trajectory
    /// state").
    ///
    /// <para>MATH (delegated to <see cref="OrbitalElements.SolveConicAtRadius"/>):</para>
    /// Solves <c>r(ν) = threshold</c> on the orbit using the conic equation, where
    /// <c>threshold = SurfaceRadiusMeters + AtmosphericTopAltitudeMeters</c>. Returns
    /// the absolute sim-tick of the next crossing, or null if no crossing is
    /// reachable (orbit fully above atmosphere, orbit fully inside atmosphere — vessel
    /// already on a sub-orbital ballistic, or numerical degenerate cases).
    ///
    /// <para>VACUUM BODY:</para>
    /// If <see cref="ReferenceBody.AtmosphericTopAltitudeMeters"/> &lt;= 0 the body is
    /// considered a vacuum body (no atmosphere) and the predictor returns null. The
    /// &lt;= 0 check defends against both the 0.0 default (Phase 1 single-body test
    /// scenes intentionally start vacuum so atmospheric events don't fire by accident)
    /// and accidentally negative Inspector values.
    ///
    /// <para>ENTRY vs EXIT SEMANTICS:</para>
    /// The predictor returns the next time the orbit reaches the atmospheric boundary
    /// radius, regardless of direction. For vessels above the atmosphere — the
    /// gameplay-relevant case where K→P mode transition needs to fire — this is
    /// atmospheric ENTRY (inbound crossing on the descending leg toward periapsis).
    /// For vessels already inside the atmosphere (which shouldn't happen on Kepler-rails
    /// — those should be PhysX-active per CONSTRAINTS §2 mode transitions), the
    /// predictor would return the next exit-crossing tick. The K→P mode transition
    /// driver handles that case harmlessly: it forces PhysX-active at the predicted
    /// tick regardless of whether the crossing is entry or exit. v1 doesn't predict
    /// atmospheric exit as a distinct event type.
    ///
    /// <para>CONSTANT-BODY-POSITION ASSUMPTION (PHASE 0/1):</para>
    /// The predictor assumes the current body's position is constant across the
    /// prediction horizon. Phase 0/1 honors this; Phase 4+ (when bodies orbit) will
    /// require revisiting the underlying <see cref="OrbitalElements.SolveConicAtRadius"/>
    /// math. Same caveat as <see cref="SoiCrossingPredictor"/>'s outward path —
    /// shared because both use the same conic-equation closed-form via the helper.
    ///
    /// <para>NO <c>DetectionAggressiveness</c> PARAMETER:</para>
    /// Unlike <see cref="SoiCrossingPredictor"/> which has two math paths (outward
    /// closed-form + inward sampled-and-refined, where Strict-mode adaptive sampling
    /// would change behavior), the atmospheric-entry predictor has one math path —
    /// closed-form conic solve. There is no sampling granularity knob to turn, so no
    /// DetectionAggressiveness parameter is needed. The asymmetry with
    /// SoiCrossingPredictor's signature is math-driven (different algorithm structure),
    /// not stylistic. CONSTRAINTS §2's "aggressively detects atmospheric entry"
    /// commitment is satisfied automatically — the closed-form returns the exact tick
    /// when a crossing exists; there are no near-tangent grazes to miss.
    ///
    /// <para>POPULATES <see cref="KeplerState.NextAtmosphericEntryTick"/>:</para>
    /// As of commit 048 Stage 1, this predictor writes to its own dedicated field
    /// (<see cref="KeplerState.NextAtmosphericEntryTick"/>) rather than being
    /// aggregated with the surface-impact predictor's output into a shared
    /// <c>NextModeTransitionTick</c>. The trigger evaluator on the vessel side
    /// reads this field independently and fires
    /// <see cref="TransitionTriggerReason.AtmosphericEntryPredicted"/> when the
    /// predicted tick is within one of the current sim-tick. Surface impact has
    /// its own dedicated field and trigger reason.
    /// </summary>
    public static class AtmosphericEntryPredictor
    {
        /// <summary>
        /// Compute the absolute sim-tick of the next atmospheric-boundary crossing
        /// on the vessel's orbit. Returns null on vacuum bodies, on orbits that
        /// don't reach the atmosphere, or on numerical degenerate cases (see
        /// <see cref="OrbitalElements.SolveConicAtRadius"/> for the full null-case
        /// catalog).
        /// </summary>
        /// <param name="state">Vessel's current Kepler state (orbital elements + epoch).</param>
        /// <param name="currentBody">Reference body the vessel is currently parented
        /// to. Surface radius, atmospheric top altitude, and μ all derive from this.</param>
        /// <param name="currentTick">Current sim-tick; predictions are returned in
        /// absolute tick coordinates.</param>
        /// <param name="tickIntervalSeconds">Seconds per sim-tick (1/30 in Phase 1).</param>
        /// <returns>Absolute sim-tick of the next atmospheric-boundary crossing, or
        /// null if no crossing is predicted.</returns>
        public static long? PredictNextEntry(
            KeplerState state,
            ReferenceBody currentBody,
            long currentTick,
            double tickIntervalSeconds)
        {
            if (state == null) return null;
            if (currentBody == null) return null;

            // Vacuum body convention: no atmosphere → no atmospheric-entry event.
            // The <= 0 check covers both the 0.0 default (Phase 1 single-body test
            // scenes start vacuum) and accidentally-negative Inspector values.
            double atmosphericTop = currentBody.AtmosphericTopAltitudeMeters;
            if (atmosphericTop <= 0.0) return null;

            // Threshold radius is total distance from body center: surface + atmosphere.
            double threshold = currentBody.SurfaceRadiusMeters + atmosphericTop;

            return OrbitalElements.SolveConicAtRadius(
                state, threshold, currentTick, currentBody.Mu, tickIntervalSeconds);
        }
    }
}
