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
    /// got populated in that commit. <strong>COMMIT 046 SCOPE:</strong>
    /// SOI-crossing events also populated via
    /// <see cref="SoiCrossingPredictor.PredictNextCrossing"/>.
    /// <strong>COMMIT 047 SCOPE (Stage 2):</strong> atmospheric-entry events via
    /// <see cref="AtmosphericEntryPredictor.PredictNextEntry"/> and surface-impact
    /// events via <see cref="SurfaceImpactPredictor.PredictNextImpact"/>. Both
    /// populate <see cref="KeplerState.NextModeTransitionTick"/> via min-of-both
    /// aggregation (the field holds the EARLIEST of the two predicted ticks).
    /// Scheduled-burn and interstellar-arrival predictors land in subsequent
    /// commits.
    /// </para>
    ///
    /// <para>
    /// <strong>NextModeTransitionTick AGGREGATION (commit 047):</strong>
    /// Multiple predictors can populate
    /// <see cref="KeplerState.NextModeTransitionTick"/> — atmospheric entry,
    /// surface impact, and future scheduled-burn / interstellar-arrival
    /// predictors. The driver aggregates these via min-of-all (using
    /// <see cref="MinNullable"/>) so the field stores the earliest predicted
    /// mode-transition event. The commit-043 trigger evaluator
    /// (<c>Vessel.IsAtmosphericEntryPredicted</c>) reads this field and fires the
    /// K→P mode transition when the tick is within one of the current sim-tick.
    /// One imprecision worth noting: the trigger reason label
    /// (<c>TransitionTriggerReason.AtmosphericEntryPredicted</c>) is generic
    /// "mode transition imminent" semantics — it fires for surface impact too if
    /// that's the earliest event. Renaming the trigger reason to something
    /// neutral is deferred to a separate cleanup commit; commit 047 accepts the
    /// label imprecision for now.
    /// </para>
    ///
    /// <para>
    /// <strong>PER-PREDICTOR EXCEPTION ISOLATION (commit 046):</strong>
    /// Inside <see cref="PredictAndUpdate"/>, each predictor (PeriapsisApoapsis,
    /// SoiCrossing, AtmosphericEntry, SurfaceImpact) runs inside its own
    /// try/catch. A failure in one predictor logs an error and the remaining
    /// predictors still execute. This is per-predictor isolation rather than
    /// per-vessel-all-or-nothing — the netcode contract's commitment to keep
    /// event predictions current is honored on a best-effort basis even when
    /// one predictor degrades. The outer try/catch in
    /// <see cref="OnTickAdvanced"/> remains as a safety net for schema-invariant
    /// violations or unforeseen failures that propagate past the inner catches.
    /// As of the IVessel-migration commit, <see cref="PredictAndUpdate"/>
    /// takes <see cref="IVessel"/> rather than concrete <see cref="Vessel"/>
    /// so the per-predictor isolation logic is unit-testable against POCO
    /// fakes; see <see cref="PredictAndUpdate"/> for the rationale.
    /// </para>
    ///
    /// <para>
    /// <strong>COUNTER SEMANTICS:</strong>
    /// <see cref="EvaluationCount"/> increments for every Kepler-rails vessel
    /// examined. <see cref="PredictionUpdateCount"/> increments for every vessel
    /// for which <see cref="PredictAndUpdate"/> executed to completion (i.e., no
    /// unhandled exception reached the outer catch). Individual predictor
    /// failures inside <see cref="PredictAndUpdate"/> are logged via inner
    /// try/catch and do not affect either counter beyond the log message. This
    /// means PredictionUpdateCount can equal EvaluationCount even when one of the
    /// predictors threw — the structural completion of PredictAndUpdate is what
    /// the counter tracks.
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
                    // Outer catch stays in concrete-Vessel context (vessel is
                    // typed Vessel at the iteration boundary, not IVessel),
                    // so the gameObject null-ternary is the natural pattern
                    // here. Inner per-predictor catches inside PredictAndUpdate
                    // use vessel.DiagnosticName because their vessel reference
                    // is IVessel-typed. The asymmetry is deliberate: outer
                    // iteration uses Unity-null semantics from VesselRegistry,
                    // inner predictor dispatch is interface-typed for testability.
                    string name = vessel != null && vessel.gameObject != null
                        ? vessel.gameObject.name : "(null)";
                    Debug.LogError(
                        $"VesselEventPredictionDriver: vessel '{name}' threw during " +
                        $"event prediction at tick {tickNumber}: {ex}");
                }
            }
        }

        /// <summary>
        /// Per-vessel predict + update. Each predictor (PeriapsisApoapsis,
        /// SoiCrossing, AtmosphericEntry, SurfaceImpact) runs inside its own
        /// try/catch for per-predictor isolation. A failure in one predictor
        /// logs an error and the remaining predictors still execute. The outer
        /// try/catch in <see cref="OnTickAdvanced"/> handles schema-invariant
        /// violations or unforeseen failures that escape the inner catches.
        ///
        /// Predictor order: PeriapsisApoapsisPredictor first, then
        /// SoiCrossingPredictor, then AtmosphericEntryPredictor + SurfaceImpact
        /// (whose results combine via min-of-both into NextModeTransitionTick).
        /// Stable iteration order keeps the diagnostic logs and queue update
        /// ordering deterministic across ticks.
        ///
        /// <para>
        /// <strong>IVessel PARAMETER (commit landing this stage):</strong>
        /// takes <see cref="IVessel"/> rather than concrete
        /// <see cref="Vessel"/> to decouple the per-predictor invocation from
        /// Unity's <see cref="UnityEngine.GameObject"/> type. This enables
        /// POCO test fakes (see
        /// <c>VesselEventPredictionDriverTests.PredictAndUpdate_AcceptsIVesselFake_WritesPredictedTicks</c>)
        /// to exercise the predictor-dispatch logic without constructing real
        /// Vessel MonoBehaviours. The outer <see cref="OnTickAdvanced"/> loop
        /// stays on concrete <see cref="Vessel"/> because
        /// <see cref="VesselRegistry.Vessels"/> returns concrete Vessel and
        /// Unity-null semantics still apply at the registry-iteration
        /// boundary; the cast Vessel → IVessel happens implicitly at the call
        /// to <see cref="PredictAndUpdate"/>.
        /// </para>
        /// </summary>
        private static void PredictAndUpdate(
            IVessel vessel,
            ReferenceBody currentBody,
            SimTickController controller,
            long tickNumber)
        {
            KeplerState kepler = vessel.State.KeplerState;
            Guid vesselId = vessel.State.VesselId;

            // ----- Periapsis / Apoapsis predictor (per-predictor isolation) -----
            try
            {
                (long? periapsisTick, long? apoapsisTick) =
                    PeriapsisApoapsisPredictor.Predict(
                        kepler,
                        tickNumber,
                        currentBody.Mu,
                        SimTickController.SimTickIntervalSeconds);

                // Write schema fields per netcode contract §2.3. Null returns
                // (hyperbolic-post-periapsis, hyperbolic apoapsis, overflow defense)
                // are written through directly — the field type is long? and the
                // queue's UpdateVesselEntry handles null cleanly (removes any
                // existing entry).
                kepler.NextPeriapsisTick = periapsisTick;
                kepler.NextApoapsisTick = apoapsisTick;

                controller.EventQueue.UpdateVesselEntry(
                    vesselId, SimEventType.Periapsis, periapsisTick);
                controller.EventQueue.UpdateVesselEntry(
                    vesselId, SimEventType.Apoapsis, apoapsisTick);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"VesselEventPredictionDriver: PeriapsisApoapsisPredictor failed " +
                    $"on vessel '{vessel.DiagnosticName}' at tick {tickNumber}: {ex}");
                // Continue to SoiCrossingPredictor — per-predictor isolation.
            }

            // ----- SOI crossing predictor (per-predictor isolation, commit 046) -----
            try
            {
                long? soiCrossingTick = SoiCrossingPredictor.PredictNextCrossing(
                    kepler,
                    currentBody,
                    tickNumber,
                    SimTickController.SimTickIntervalSeconds,
                    SoiCrossingPredictor.DetectionAggressiveness.Pragmatic);

                // Write schema field per netcode contract §2.3 (amendment lands in
                // commit 046 Stage 3). Null on no crossing within horizon — cleanup
                // of any stale value is automatic via the field assignment.
                kepler.NextSoiTransitionTick = soiCrossingTick;

                controller.EventQueue.UpdateVesselEntry(
                    vesselId, SimEventType.SoiCrossing, soiCrossingTick);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"VesselEventPredictionDriver: SoiCrossingPredictor failed " +
                    $"on vessel '{vessel.DiagnosticName}' at tick {tickNumber}: {ex}");
                // PeriapsisApoapsis writes already landed above — that's per-predictor
                // isolation working as designed.
            }

            // ----- Atmospheric entry + surface impact predictors (commit 047) -----
            //
            // Both predictors populate KeplerState.NextModeTransitionTick via
            // min-of-both aggregation AFTER both have run. The per-predictor
            // try/catch blocks isolate failures; an exception in either predictor
            // leaves that local tick variable null, and the aggregation step still
            // writes whatever the other predictor produced (or null if both threw).
            long? atmosphericEntryTick = null;
            long? surfaceImpactTick = null;

            try
            {
                atmosphericEntryTick = AtmosphericEntryPredictor.PredictNextEntry(
                    kepler,
                    currentBody,
                    tickNumber,
                    SimTickController.SimTickIntervalSeconds);

                controller.EventQueue.UpdateVesselEntry(
                    vesselId, SimEventType.AtmosphericEntry, atmosphericEntryTick);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"VesselEventPredictionDriver: AtmosphericEntryPredictor failed " +
                    $"on vessel '{vessel.DiagnosticName}' at tick {tickNumber}: {ex}");
                // atmosphericEntryTick stays null; surface-impact predictor still runs.
            }

            try
            {
                surfaceImpactTick = SurfaceImpactPredictor.PredictNextImpact(
                    kepler,
                    currentBody,
                    tickNumber,
                    SimTickController.SimTickIntervalSeconds);

                controller.EventQueue.UpdateVesselEntry(
                    vesselId, SimEventType.SurfaceImpact, surfaceImpactTick);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"VesselEventPredictionDriver: SurfaceImpactPredictor failed " +
                    $"on vessel '{vessel.DiagnosticName}' at tick {tickNumber}: {ex}");
                // surfaceImpactTick stays null.
            }

            // Aggregate atmospheric + surface ticks into NextModeTransitionTick.
            // Earliest of the two (or null if both null). This aggregation step
            // lives OUTSIDE both per-predictor try/catch blocks so it always runs;
            // if both predictors threw, both locals are null and MinNullable
            // produces null (clean stale-value cleanup on the field).
            kepler.NextModeTransitionTick = MinNullable(
                atmosphericEntryTick, surfaceImpactTick);
        }

        /// <summary>
        /// Return the smaller of two nullable longs. Null is treated as "no value";
        /// if both null, returns null; if one null, returns the other.
        ///
        /// Uses <see cref="System.Math.Min(long, long)"/> rather than
        /// <c>Unity.Mathematics.math.min</c> because the latter has no <c>long</c>
        /// overload — only int, uint, float, and double — and would silently
        /// coerce to double, losing precision near <c>long.MaxValue</c>. Same
        /// pattern as <c>SoiCrossingPredictor.MinNullable</c> (intentional small
        /// duplication: each module's nullable-long min is a 5-line utility, and
        /// extracting to a shared math library would be over-engineering).
        /// </summary>
        private static long? MinNullable(long? a, long? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return System.Math.Min(a.Value, b.Value);
        }
    }
}
