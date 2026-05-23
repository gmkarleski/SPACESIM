using SpaceSim.Foundation.SimTick;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Result of <see cref="Vessel.EvaluateTransitionTriggers"/>. Bundles the suggested
    /// transition (if any) with the reason that fired.
    ///
    /// Pairing the decision with the firing condition lets the caller log diagnostics
    /// without having to reverse-engineer which §3.1 condition the evaluator must have
    /// matched. The driver (<c>VesselTransitionDriver</c>) logs the reason directly from
    /// the struct; tests assert against both fields to verify the evaluator picked the
    /// right condition, not just the right decision.
    ///
    /// Value semantics — no heap allocation per evaluation.
    /// </summary>
    public struct TransitionEvaluation
    {
        /// <summary>
        /// The mode the evaluator suggests transitioning to, or null to stay in the
        /// current mode. Null is the common case (no condition fired this tick); the
        /// driver should treat null as "do nothing."
        /// </summary>
        public PhysicsMode? SuggestedMode;

        /// <summary>
        /// Which §3.1 condition fired (or <see cref="TransitionTriggerReason.None"/>
        /// when no transition is suggested).
        /// </summary>
        public TransitionTriggerReason Reason;

        /// <summary>
        /// Constructs a "stay in current mode" result. Convenience for evaluator
        /// no-transition branches.
        /// </summary>
        public static TransitionEvaluation Stay()
        {
            return new TransitionEvaluation
            {
                SuggestedMode = null,
                Reason = TransitionTriggerReason.None,
            };
        }

        /// <summary>
        /// Constructs a "transition to <paramref name="mode"/> because <paramref name="reason"/>"
        /// result.
        /// </summary>
        public static TransitionEvaluation Transition(PhysicsMode mode, TransitionTriggerReason reason)
        {
            return new TransitionEvaluation
            {
                SuggestedMode = mode,
                Reason = reason,
            };
        }
    }

    /// <summary>
    /// Names which §3.1 trigger condition fired (or <see cref="None"/> when no
    /// transition is suggested). Each enum value maps to one bullet in
    /// <c>docs/NETCODE_CONTRACT.md</c> §3.1's trigger-conditions list.
    /// </summary>
    public enum TransitionTriggerReason
    {
        /// <summary>No transition suggested this evaluation.</summary>
        None,

        // ----- PhysX-active → Kepler-rails (conjunction; all five conditions must hold) -----

        /// <summary>
        /// All five PhysX-active → Kepler-rails conditions held simultaneously: beyond
        /// 50 km of any active vessel AND no thrust AND no significant atmospheric drag
        /// AND no contact forces AND well-defined trajectory. §3.1's conjunctive trigger
        /// is reported as a single "BeyondProximityWithCleanState" reason because no
        /// finer attribution is possible — the conjunction fired only when every part
        /// was true; naming one would be misleading.
        /// </summary>
        BeyondProximityWithCleanState,

        // ----- Kepler-rails → PhysX-active (disjunction; first matching condition wins) -----

        /// <summary>K→P trigger: vessel entered within 50 km of any active vessel.</summary>
        ProximityToActiveVessel,

        /// <summary>K→P trigger: predicted atmospheric entry within the next sim-tick.
        /// Reads <see cref="KeplerState.NextAtmosphericEntryTick"/>; fires only when
        /// the vessel's trajectory crosses the atmospheric boundary
        /// (<c>SurfaceRadiusMeters + AtmosphericTopAltitudeMeters</c>) of the current
        /// reference body.
        ///
        /// <para>
        /// AS OF COMMIT 048: this enum value is atmospheric-only. Surface impact gets
        /// its own distinct value (<see cref="SurfaceImpactPredicted"/>), and
        /// <c>NextModeTransitionTick</c> was split into
        /// <see cref="KeplerState.NextAtmosphericEntryTick"/> and
        /// <see cref="KeplerState.NextSurfaceImpactTick"/> so the trigger evaluator
        /// can distinguish the two events at runtime. The label imprecision that
        /// existed from commit 047 onward is now resolved.
        /// </para></summary>
        AtmosphericEntryPredicted,

        /// <summary>K→P trigger: predicted surface impact within the next sim-tick.
        /// Reads <see cref="KeplerState.NextSurfaceImpactTick"/>; fires only when the
        /// vessel's trajectory intersects the surface radius
        /// (<c>SurfaceRadiusMeters</c>) of the current reference body.
        ///
        /// <para>
        /// ADDED COMMIT 048: split from the previously-aggregated
        /// <c>NextModeTransitionTick</c> field. Surface impact is a distinct event
        /// from atmospheric entry (mass loss vs aerodynamic engagement) and warrants
        /// a separate trigger reason for diagnostic clarity and future
        /// terminal-event handling (warp halts for surface impact even on routine
        /// supply vessels per the upcoming time-warp policy).
        /// </para></summary>
        SurfaceImpactPredicted,

        /// <summary>K→P trigger: player switched focus to this vessel. Phase 0 stub (no focus subsystem).</summary>
        PlayerFocusSwitch,

        /// <summary>K→P trigger: scripted mode change (Vizzy thrust, etc.). Phase 0 stub (Vizzy is Phase 5).</summary>
        ScriptedThrust,

        /// <summary>K→P trigger: multi-vessel proximity cluster. Phase 0 stub (no clustering logic).</summary>
        MultiVesselProximityCluster,

        /// <summary>K→P trigger: warp rate exceeds the 5x PhysX threshold while the vessel
        /// is in PhysX-active mode. The time-warp controller (lands in commit 048 Stage 2)
        /// fires a forced transition to KeplerRails so high-warp advancement stays valid.
        ///
        /// <para>
        /// ADDED COMMIT 048 (Stage 1 enum value; wired up in Stage 3): preserves the
        /// architectural pattern that all mode changes flow through the driver. The
        /// time-warp controller cannot bypass <see cref="VesselTransitionDriver"/>
        /// for forced transitions; it relies on this trigger reason being fired by
        /// the driver's per-tick evaluator.
        /// </para></summary>
        WarpRateForcedRails,
    }
}
