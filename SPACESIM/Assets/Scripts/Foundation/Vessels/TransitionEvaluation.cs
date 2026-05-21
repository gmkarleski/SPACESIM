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

        /// <summary>K→P trigger: predicted mode transition imminent within the next
        /// sim-tick. Reads <see cref="KeplerState.NextModeTransitionTick"/>.
        ///
        /// HISTORICAL NAMING / IMPRECISE AS OF COMMIT 047: this enum value retains
        /// its "AtmosphericEntryPredicted" name from commit 043 when atmospheric
        /// entry was the only contributor to <c>NextModeTransitionTick</c>. As of
        /// commit 047, the field is N-way aggregated (atmospheric entry + surface
        /// impact, with future scheduled-burn / interstellar-arrival), so this
        /// trigger reason fires for any populated mode-transition tick — not only
        /// atmospheric entry. The label rename / split is a known cosmetic concern
        /// deferred to a separate cleanup commit; see DECISIONS "Atmospheric entry
        /// + surface impact predictors (commit 047)" entry for the rationale.</summary>
        AtmosphericEntryPredicted,

        /// <summary>K→P trigger: player switched focus to this vessel. Phase 0 stub (no focus subsystem).</summary>
        PlayerFocusSwitch,

        /// <summary>K→P trigger: scripted mode change (Vizzy thrust, etc.). Phase 0 stub (Vizzy is Phase 5).</summary>
        ScriptedThrust,

        /// <summary>K→P trigger: multi-vessel proximity cluster. Phase 0 stub (no clustering logic).</summary>
        MultiVesselProximityCluster,
    }
}
