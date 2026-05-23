namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Names why time-warp stopped advancing. Carried by <see cref="WarpHaltInfo"/>
    /// and surfaced via <see cref="WarpController.OnWarpHalted"/>.
    ///
    /// <para>
    /// <strong>Parallel to but distinct from <c>TransitionTriggerReason</c></strong>
    /// (which lives in the Vessels asmdef and names §3.1 mode-transition triggers).
    /// The two enums name related but distinct concepts:
    /// <list type="bullet">
    ///   <item>Some <c>TransitionTriggerReason</c> values don't halt warp —
    ///   proximity-to-active-vessel and player-focus-switch are mode-transition
    ///   triggers that don't stop time-warp because they don't represent
    ///   player-visible events the player needs to react to.</item>
    ///   <item>Some <see cref="WarpHaltReason"/> values aren't trigger reasons —
    ///   <see cref="TargetTickReached"/> and <see cref="Manual"/> are halt
    ///   conditions internal to the warp controller; no vessel-level
    ///   mode-transition fires.</item>
    /// </list>
    /// The two taxonomies overlap on the predictor-driven events
    /// (atmospheric entry, surface impact, SOI crossing, scheduled-burn arrival)
    /// and evolve independently otherwise.
    /// </para>
    ///
    /// <para>
    /// <strong>STAGE 3 MAPPING:</strong> the Vessels asmdef will introduce a small
    /// translation function that maps <c>TransitionTriggerReason</c> values to
    /// <see cref="WarpHaltReason"/> when constructing
    /// <see cref="WarpHaltInfo"/> from predictor output. The mapping lives in
    /// Vessels (the side that has access to both enums) and keeps the SimTick
    /// asmdef free of any reference to <c>TransitionTriggerReason</c>.
    /// </para>
    ///
    /// <para>
    /// <strong>STAGE 2 FIRING SUBSET:</strong> only <see cref="TargetTickReached"/>
    /// and <see cref="Manual"/> actually fire from Stage 2's own behavior — the
    /// rest are infrastructure that Stage 3 wires up when vessel-level halt
    /// registration lands.
    /// </para>
    /// </summary>
    public enum WarpHaltReason
    {
        /// <summary>The warp controller advanced to its <see cref="WarpController.TargetTick"/>
        /// value and halted exactly at that tick. Stage 2 firing path.</summary>
        TargetTickReached,

        /// <summary>The player (or automation) explicitly halted warp via
        /// <see cref="WarpController.RegisterHaltEvent"/> with no event-driven
        /// reason. Stage 2 firing path; expected use is the Mission Control "halt
        /// warp" button before the predictor-driven halts wire up.</summary>
        Manual,

        /// <summary>A vessel's atmospheric-entry predictor flagged an imminent entry.
        /// Stage 3 wires this — the Vessels asmdef registers the halt event in
        /// response to a <see cref="WarpController.OnRateChanged"/>-mediated check
        /// or via direct registration from the predictor driver.</summary>
        AtmosphericEntryPredicted,

        /// <summary>A vessel's surface-impact predictor flagged an imminent impact.
        /// Stage 3 firing path (parallel to <see cref="AtmosphericEntryPredicted"/>).</summary>
        SurfaceImpactPredicted,

        /// <summary>A vessel's SOI-crossing predictor flagged an imminent crossing.
        /// Stage 3 firing path.</summary>
        SoiCrossingPredicted,

        /// <summary>A vessel's scheduled-burn (maneuver-execution) tick is imminent.
        /// Stage 3+ firing path (the maneuver predictor lands after the atmospheric /
        /// surface / SOI predictors).</summary>
        ManeuverExecutionImminent,
    }
}
