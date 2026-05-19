using System;
using SpaceSim.Foundation.SimTick;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Static driver that subscribes to <see cref="SimTickController.TickAdvanced"/> and
    /// invokes <see cref="Vessel.EvaluateTransitionTriggers"/> on every registered vessel
    /// once per sim-tick. When <see cref="Vessel.EvaluateTransitionTriggers"/> returns a
    /// non-null suggestion, the driver dispatches the corresponding transition
    /// (<see cref="Vessel.TransitionToKeplerRails"/> or <see cref="Vessel.TransitionToPhysXActive"/>).
    ///
    /// <para>
    /// <strong>DISABLED BY DEFAULT.</strong> <see cref="Enabled"/> defaults to false; when
    /// false, <see cref="OnTickAdvanced"/> is a no-op (one bool check + return per tick;
    /// sub-nanosecond cost). The driver subscribes to the TickAdvanced event regardless
    /// of <see cref="Enabled"/> — so production scenes wire up the subscription at
    /// startup and flip the flag on whenever upstream state systems (thrust simulation,
    /// atmospheric model, contact detection) become populated enough that automatic
    /// transitions are meaningful. Phase 0 / early Phase 1 ships with the flag off;
    /// transitions remain imperative (driven by player input or test code calling
    /// <see cref="Vessel.TransitionToKeplerRails"/> / <see cref="Vessel.TransitionToPhysXActive"/>
    /// directly).
    /// </para>
    ///
    /// <para>
    /// <strong>WHY NOT IN SIMTICK STEP 6?</strong> The netcode contract §3.1 says trigger
    /// evaluation runs at every sim-tick, which TickAdvanced satisfies (one fire per
    /// tick). The location is the TickAdvanced event handler rather than literally
    /// inside <c>SimTickController.Step6_DetectModeTransitions</c> to preserve the
    /// asmdef dependency direction established in commit 038: Vessels references
    /// SimTick (so Vessel can read <see cref="SimTickController.Instance.TickNumber"/>
    /// and the <see cref="PhysicsMode"/> enum); SimTick does NOT reference Vessels.
    /// Putting the per-vessel iteration inside SimTickController would close the
    /// dependency cycle. Subscribing from outside the controller (Vessels-module
    /// driver listening to the cross-module event) keeps the graph acyclic. Transitions
    /// complete between tick N's Step 10 and tick N+1's Step 1 — within "every sim-tick"
    /// per the contract, just not literally inside step 6.
    /// </para>
    ///
    /// <para>
    /// Subscription lifecycle: <see cref="Initialize"/> attaches the event handler when
    /// <see cref="SimTickController.Instance"/> exists; calling <see cref="Initialize"/>
    /// before the controller's Awake fires returns with a warning so the caller can
    /// retry later. <see cref="Shutdown"/> detaches and resets all state. Tests use
    /// <see cref="ResetForTesting"/> to clear counters and warn-once flags between
    /// methods without churning the subscription.
    /// </para>
    /// </summary>
    public static class VesselTransitionDriver
    {
        /// <summary>
        /// Master switch. When false, <see cref="OnTickAdvanced"/> returns immediately
        /// without evaluating or transitioning any vessel. Defaults to false (Phase 0 /
        /// early Phase 1: trigger evaluation is implemented and tested but not running
        /// in Play scenes). Tests set this to true to exercise the dispatch path.
        /// </summary>
        public static bool Enabled = false;

        /// <summary>
        /// Diagnostic counter: number of <see cref="Vessel.EvaluateTransitionTriggers"/>
        /// invocations the driver has made since the last <see cref="Initialize"/> /
        /// <see cref="ResetForTesting"/>. Incremented once per vessel per tick when
        /// <see cref="Enabled"/>. Stays at zero when the driver is disabled.
        /// </summary>
        public static int EvaluationCount;

        /// <summary>
        /// Diagnostic counter: number of transitions the driver has actually invoked
        /// (i.e., evaluations that returned non-null SuggestedMode AND the subsequent
        /// transition call completed without throwing). Stays at zero when the driver
        /// is disabled.
        /// </summary>
        public static int TransitionCount;

        private static bool _subscribed = false;
        private static bool _warnedAboutMissingActiveVessel = false;

        /// <summary>
        /// Subscribe to <see cref="SimTickController.Instance"/>'s
        /// <see cref="SimTickController.TickAdvanced"/> event. Idempotent — repeated
        /// calls after a successful subscription are no-ops.
        ///
        /// If <see cref="SimTickController.Instance"/> is null when Initialize is
        /// called (Awake hasn't fired yet, or no controller in scene), logs a warning
        /// and returns without subscribing. The caller can call Initialize again
        /// later when the controller exists. This deferred-attach semantics matches
        /// commit 034's pending-listener pattern but is simpler — the driver is
        /// idempotent on repeated calls.
        /// </summary>
        public static void Initialize()
        {
            if (_subscribed) return;

            if (SimTickController.Instance == null)
            {
                Debug.LogWarning(
                    "VesselTransitionDriver.Initialize: SimTickController.Instance is null; " +
                    "deferring subscription. Call Initialize again after the controller exists.");
                return;
            }

            SimTickController.Instance.TickAdvanced += OnTickAdvanced;
            _subscribed = true;
        }

        /// <summary>
        /// Unsubscribe from TickAdvanced and reset all driver state to defaults. Safe
        /// to call without a prior <see cref="Initialize"/>. Tests typically call this
        /// in TearDown; production code calls it on scene unload or shutdown.
        ///
        /// Resets <see cref="Enabled"/> to false in addition to unsubscribing and
        /// clearing counters. The "fully off" semantics of Shutdown includes disabling
        /// the master switch, so a subsequent <see cref="Initialize"/> + tick fire is
        /// guaranteed to no-op until the caller explicitly sets <see cref="Enabled"/>
        /// to true again. This is the difference from <see cref="ResetForTesting"/>,
        /// which preserves <see cref="Enabled"/> as part of the caller's test setup
        /// state and only clears counters + warn-once flag.
        /// </summary>
        public static void Shutdown()
        {
            if (_subscribed && SimTickController.Instance != null)
            {
                SimTickController.Instance.TickAdvanced -= OnTickAdvanced;
            }
            _subscribed = false;
            Enabled = false;
            EvaluationCount = 0;
            TransitionCount = 0;
            _warnedAboutMissingActiveVessel = false;
        }

        /// <summary>
        /// Test-only reset: clears counters and warn-once flag without unsubscribing.
        /// Tests that need to re-exercise the same subscribed driver across multiple
        /// scenarios call this between scenarios to start from a clean diagnostic
        /// baseline.
        /// </summary>
        public static void ResetForTesting()
        {
            EvaluationCount = 0;
            TransitionCount = 0;
            _warnedAboutMissingActiveVessel = false;
            // Intentionally does not touch _subscribed.
        }

        /// <summary>
        /// TickAdvanced event handler. Iterates registered vessels, calls
        /// <see cref="Vessel.EvaluateTransitionTriggers"/> on each, and dispatches the
        /// suggested transition when non-null.
        ///
        /// Exception safety: per-vessel try/catch wraps both the evaluation call and
        /// the transition call. A vessel that throws does not abort the loop or block
        /// other vessels' evaluations. Errors are logged with the offending vessel's
        /// name and the exception detail.
        ///
        /// Snapshot semantics: iterates a <c>ToArray()</c> snapshot of
        /// <see cref="VesselRegistry.Vessels"/> so transitions that destroy
        /// rigidbodies / anchors / register or unregister vessels mid-loop don't
        /// invalidate the iteration (same pattern as
        /// <see cref="SimTickController.Step10_AdvanceCounter"/>'s listener loop).
        /// </summary>
        public static void OnTickAdvanced(long tickNumber)
        {
            if (!Enabled) return;

            SimTickController controller = SimTickController.Instance;
            if (controller == null)
            {
                // Controller can disappear mid-frame (scene unload, test cleanup);
                // this is a transient condition. No warning — the symmetric path on
                // Initialize handles the diagnostic on the way in.
                return;
            }

            IActiveVessel activeVessel = controller.ActiveVessel;
            if (activeVessel == null)
            {
                if (!_warnedAboutMissingActiveVessel)
                {
                    Debug.LogWarning(
                        "VesselTransitionDriver.OnTickAdvanced: ActiveVessel is null. " +
                        "Trigger evaluation skipped. This warning logs once per Initialize lifetime; " +
                        "call SimTickController.SetActiveVessel(vessel) before enabling the driver.");
                    _warnedAboutMissingActiveVessel = true;
                }
                return;
            }

            // Snapshot so transitions mid-loop (which may unregister anchors / change
            // component shapes) don't invalidate the iteration.
            Vessel[] snapshot;
            {
                var live = VesselRegistry.Vessels;
                snapshot = new Vessel[live.Count];
                for (int i = 0; i < snapshot.Length; i++) snapshot[i] = live[i];
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                Vessel vessel = snapshot[i];
                if (vessel == null) continue;  // Vessel destroyed since snapshot.

                TransitionEvaluation evaluation;
                try
                {
                    EvaluationCount++;
                    evaluation = vessel.EvaluateTransitionTriggers(activeVessel);
                }
                catch (Exception ex)
                {
                    string name = vessel != null && vessel.gameObject != null
                        ? vessel.gameObject.name : "(null)";
                    Debug.LogError(
                        $"VesselTransitionDriver: vessel '{name}' threw during " +
                        $"EvaluateTransitionTriggers: {ex}");
                    continue;
                }

                if (!evaluation.SuggestedMode.HasValue)
                {
                    continue;
                }

                PhysicsMode suggested = evaluation.SuggestedMode.Value;
                PhysicsMode current = vessel.Mode;
                string vesselName = vessel.gameObject != null
                    ? vessel.gameObject.name : "(null)";

                Debug.Log(
                    $"VesselTransitionDriver: vessel '{vesselName}' transitioning " +
                    $"{current} → {suggested} (reason: {evaluation.Reason}, tick {tickNumber})");

                try
                {
                    if (suggested == PhysicsMode.KeplerRails)
                    {
                        vessel.TransitionToKeplerRails();
                    }
                    else if (suggested == PhysicsMode.PhysXActive)
                    {
                        vessel.TransitionToPhysXActive();
                    }
                    else
                    {
                        // InterstellarCruise would land here in Phase 6+; not reachable
                        // in Phase 0 because EvaluateTransitionTriggers returns Stay for
                        // InterstellarCruise mode. Defensive log if it ever fires.
                        Debug.LogError(
                            $"VesselTransitionDriver: vessel '{vesselName}' suggested unhandled " +
                            $"mode {suggested}; no transition method invoked.");
                        continue;
                    }
                    TransitionCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        $"VesselTransitionDriver: vessel '{vesselName}' threw during " +
                        $"transition to {suggested}: {ex}");
                }
            }
        }
    }
}
