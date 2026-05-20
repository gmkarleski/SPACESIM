using System;
using SpaceSim.Foundation.SimTick;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Per-sim-tick event prediction driver. Subscribes to
    /// <see cref="SimTickController.TickAdvanced"/> and runs predictors on every
    /// Kepler-rails vessel. Populates <see cref="KeplerState.NextPeriapsisTick"/>
    /// and <see cref="KeplerState.NextApoapsisTick"/> from the predictor output and
    /// updates the corresponding entries in
    /// <see cref="SimTickController.EventQueue"/>.
    ///
    /// <para>
    /// <strong>NOT DISABLED-BY-DEFAULT.</strong> Unlike
    /// <see cref="VesselTransitionDriver"/> (which gates on Phase 0 stub state),
    /// the event predictor has real implementation: the periapsis/apoapsis math is
    /// closed-form and produces correct results for any Kepler-rails vessel.
    /// Always-on is the right default.
    /// </para>
    ///
    /// <para>
    /// <strong>PATTERN PARALLEL:</strong> mirrors <see cref="VesselTransitionDriver"/>
    /// and <see cref="VesselSoiRerootingDriver"/> in shape — static class with
    /// counters, Initialize/Shutdown/ResetForTesting lifecycle, snapshot-iterate
    /// pattern with per-vessel try/catch. Three drivers now subscribe to
    /// TickAdvanced independently (transition triggers, SOI re-rooting, event
    /// prediction); each handles its own concern.
    /// </para>
    ///
    /// <para>
    /// <strong>COMMIT 045 SCOPE:</strong> only the periapsis and apoapsis events
    /// get populated in this commit. SOI-crossing, atmospheric-entry,
    /// surface-impact, scheduled-burn, and interstellar-arrival predictors land
    /// in subsequent commits (the <see cref="SimEventType"/> enum values exist
    /// already per CONSTRAINTS §2's extensibility hook).
    /// </para>
    /// </summary>
    public static class VesselEventPredictionDriver
    {
        /// <summary>
        /// Diagnostic counter: vessels examined by the predictor this session.
        /// Incremented once per Kepler-rails vessel per tick. Non-Kepler-rails and
        /// invalid-state vessels are skipped without incrementing.
        /// </summary>
        public static int EvaluationCount;

        /// <summary>
        /// Diagnostic counter: vessels whose KeplerState event-prediction fields
        /// were updated AND whose EventQueue entries were touched this session.
        /// Incremented once per vessel update (not once per event type).
        /// </summary>
        public static int PredictionUpdateCount;

        private static bool _subscribed = false;

        /// <summary>
        /// Subscribe to <see cref="SimTickController.TickAdvanced"/>. Idempotent.
        /// Deferred-attach pattern: logs warning + returns if
        /// <see cref="SimTickController.Instance"/> is null at call time.
        /// </summary>
        public static void Initialize()
        {
            if (_subscribed) return;
            if (SimTickController.Instance == null)
            {
                Debug.LogWarning(
                    "VesselEventPredictionDriver.Initialize: SimTickController.Instance " +
                    "is null; deferring subscription. Call Initialize again after the " +
                    "controller exists.");
                return;
            }
            SimTickController.Instance.TickAdvanced += OnTickAdvanced;
            _subscribed = true;
        }

        /// <summary>
        /// Unsubscribe from TickAdvanced and reset diagnostic state. Safe to call
        /// without a prior <see cref="Initialize"/>.
        /// </summary>
        public static void Shutdown()
        {
            if (_subscribed && SimTickController.Instance != null)
            {
                SimTickController.Instance.TickAdvanced -= OnTickAdvanced;
            }
            _subscribed = false;
            EvaluationCount = 0;
            PredictionUpdateCount = 0;
        }

        /// <summary>
        /// Test-only reset: clears counters without unsubscribing.
        /// </summary>
        public static void ResetForTesting()
        {
            EvaluationCount = 0;
            PredictionUpdateCount = 0;
        }

        /// <summary>
        /// TickAdvanced event handler. Iterates Kepler-rails vessels, runs the
        /// periapsis/apoapsis predictor, writes results to KeplerState fields and
        /// EventQueue entries.
        ///
        /// Per-vessel try/catch isolates failures (a throwing vessel logs an error
        /// and the loop continues to the next vessel). Snapshot of
        /// <see cref="VesselRegistry.Vessels"/> taken via manual array copy so
        /// re-rooting or destruction mid-loop doesn't invalidate iteration.
        /// </summary>
        public static void OnTickAdvanced(long tickNumber)
        {
            SimTickController controller = SimTickController.Instance;
            if (controller == null)
            {
                // Transient: scene unload, test cleanup. Silent return.
                return;
            }

            // Snapshot vessels for safe iteration.
            Vessel[] snapshot;
            {
                var live = VesselRegistry.Vessels;
                snapshot = new Vessel[live.Count];
                for (int i = 0; i < snapshot.Length; i++) snapshot[i] = live[i];
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                Vessel vessel = snapshot[i];
                if (vessel == null) continue;

                // Skip non-Kepler-rails vessels — predictors only apply to vessels
                // on analytic propagation. PhysX-active vessels handle position via
                // Unity physics; InterstellarCruise is Phase 6+.
                if (vessel.Mode != PhysicsMode.KeplerRails) continue;

                // Defensive: schema invariant says Mode == KeplerRails implies
                // KeplerState != null, but defend.
                if (vessel.State?.KeplerState == null) continue;

                // Predictor needs μ from the reference body.
                ReferenceBody currentBody = vessel.ReferenceBody;
                if (currentBody == null) continue;

                try
                {
                    EvaluationCount++;
                    PredictAndUpdate(vessel, currentBody, controller, tickNumber);
                    PredictionUpdateCount++;
                }
                catch (Exception ex)
                {
                    string name = vessel != null && vessel.gameObject != null
                        ? vessel.gameObject.name : "(null)";
                    Debug.LogError(
                        $"VesselEventPredictionDriver: vessel '{name}' threw during " +
                        $"event prediction at tick {tickNumber}: {ex}");
                }
            }
        }

        /// <summary>
        /// Per-vessel predict + update: extracted from <see cref="OnTickAdvanced"/>
        /// for readability. Throws on internal failure; the outer loop's try/catch
        /// handles isolation.
        /// </summary>
        private static void PredictAndUpdate(
            Vessel vessel,
            ReferenceBody currentBody,
            SimTickController controller,
            long tickNumber)
        {
            KeplerState kepler = vessel.State.KeplerState;
            Guid vesselId = vessel.State.VesselId;

            (long? periapsisTick, long? apoapsisTick) = PeriapsisApoapsisPredictor.Predict(
                kepler,
                tickNumber,
                currentBody.Mu,
                SimTickController.SimTickIntervalSeconds);

            // Write schema fields per netcode contract §2.3.
            kepler.NextPeriapsisTick = periapsisTick;
            kepler.NextApoapsisTick = apoapsisTick;

            // Update priority queue entries. UpdateVesselEntry handles add/update/
            // remove uniformly (null tick removes the entry).
            controller.EventQueue.UpdateVesselEntry(vesselId, SimEventType.Periapsis, periapsisTick);
            controller.EventQueue.UpdateVesselEntry(vesselId, SimEventType.Apoapsis, apoapsisTick);
        }
    }
}
