using System;
using System.Collections.Generic;
using SpaceSim.Foundation.SimTick;
using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Static driver that subscribes to <see cref="SimTickController.TickAdvanced"/> and
    /// performs SOI re-rooting on every registered Kepler-rails vessel once per sim-tick.
    /// When a vessel crosses the SOI boundary outward (beyond its current body's
    /// <see cref="ReferenceBody.SoiRadiusMeters"/>), the driver re-roots to the parent
    /// body. When a vessel crosses inward (into a child body's SOI), the driver
    /// re-roots to that child.
    ///
    /// <para>
    /// <strong>NOT DISABLED-BY-DEFAULT.</strong> Unlike <see cref="VesselTransitionDriver"/>
    /// (which gates on stub-state conditions that would always pass in Phase 0), SOI
    /// re-rooting has real implementation: <see cref="ReferenceBody.SoiRadiusMeters"/>
    /// is hand-set per body in Phase 1 with the top-level convention
    /// (<see cref="double.PositiveInfinity"/> for bodies with no parent). In a
    /// single-body Phase 0 test scene, the re-rooting check runs every tick and
    /// correctly finds no crossings (vessel inside infinite SOI; no children to enter).
    /// In multi-body Phase 1+ scenes, the check fires actual re-rooting at the right
    /// moments. Always-on is the right default.
    /// </para>
    ///
    /// <para>
    /// <strong>WHY NOT FOLDED INTO VesselTransitionDriver?</strong> SOI re-rooting is
    /// intra-mode (vessel stays on Kepler-rails before and after; only the reference
    /// body changes). Mode transitions (handled by <see cref="VesselTransitionDriver"/>)
    /// are inter-mode (PhysX-active ↔ Kepler-rails). The two concerns are
    /// architecturally distinct; separating them keeps each driver focused. Both
    /// subscribe to <see cref="SimTickController.TickAdvanced"/> independently.
    /// </para>
    ///
    /// <para>
    /// <strong>FIRST-MATCH-WINS for child SOI entry.</strong> If a vessel is somehow
    /// inside multiple overlapping child SOIs simultaneously (which shouldn't happen
    /// in a well-designed body hierarchy but is mathematically possible if SOI radii
    /// are set inconsistently), the driver re-roots to the first child found in
    /// <see cref="BodyRegistry.GetChildrenOf"/>'s enumeration order. This is a Phase 1
    /// simplification — the correct behavior would be to re-root to the child whose
    /// SOI center is closest, or whose gravitational dominance is highest. Worth
    /// revisiting when multi-body scenes get rich enough for overlap to be plausible.
    /// </para>
    ///
    /// Subscription lifecycle parallels <see cref="VesselTransitionDriver"/>:
    /// <see cref="Initialize"/> attaches the event handler when
    /// <see cref="SimTickController.Instance"/> exists; <see cref="Shutdown"/> detaches
    /// and resets state. Tests use <see cref="ResetForTesting"/> to clear counters
    /// without churning the subscription.
    /// </summary>
    public static class VesselSoiRerootingDriver
    {
        /// <summary>
        /// Diagnostic counter: number of vessels the driver has examined for SOI
        /// crossings since the last <see cref="Initialize"/> / <see cref="ResetForTesting"/>.
        /// Incremented once per Kepler-rails vessel per tick. PhysX-active and
        /// InterstellarCruise vessels are skipped without incrementing the counter
        /// (the counter tracks "vessels evaluated," not "vessels iterated").
        /// </summary>
        public static int EvaluationCount;

        /// <summary>
        /// Diagnostic counter: number of re-rooting operations the driver has
        /// completed (i.e., evaluations where an SOI crossing was detected AND
        /// <see cref="Vessel.ReRootToBody"/> was invoked without throwing).
        /// </summary>
        public static int RerootingCount;

        private static bool _subscribed = false;

        /// <summary>
        /// Subscribe to <see cref="SimTickController.Instance"/>'s
        /// <see cref="SimTickController.TickAdvanced"/> event. Idempotent — repeated
        /// calls after a successful subscription are no-ops. Logs warning + returns
        /// if Instance is null at call time (deferred-attach pattern matching
        /// <see cref="VesselTransitionDriver.Initialize"/>).
        /// </summary>
        public static void Initialize()
        {
            if (_subscribed) return;
            if (SimTickController.Instance == null)
            {
                Debug.LogWarning(
                    "VesselSoiRerootingDriver.Initialize: SimTickController.Instance is null; " +
                    "deferring subscription. Call Initialize again after the controller exists.");
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
            RerootingCount = 0;
        }

        /// <summary>
        /// Test-only reset: clears counters without unsubscribing.
        /// </summary>
        public static void ResetForTesting()
        {
            EvaluationCount = 0;
            RerootingCount = 0;
        }

        /// <summary>
        /// TickAdvanced event handler. Iterates registered vessels, checks each
        /// Kepler-rails vessel for SOI crossings, dispatches re-rooting when needed.
        ///
        /// Exception safety: per-vessel try/catch isolates failures. A vessel that
        /// throws does not abort the loop. Snapshot of <see cref="VesselRegistry.Vessels"/>
        /// is taken via manual array copy (matches the
        /// <see cref="VesselTransitionDriver.OnTickAdvanced"/> pattern) so re-rooting
        /// that mutates the registry mid-loop doesn't invalidate iteration. (In
        /// practice ReRootToBody does not touch the registry, but the discipline is
        /// the same.)
        /// </summary>
        public static void OnTickAdvanced(long tickNumber)
        {
            SimTickController controller = SimTickController.Instance;
            if (controller == null)
            {
                // Controller transiently null (scene unload, test cleanup); silent return.
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

                // Skip non-Kepler-rails vessels. PhysX-active vessels handle position
                // via Unity physics; InterstellarCruise is Phase 6+.
                if (vessel.Mode != PhysicsMode.KeplerRails) continue;

                // Defensive: schema invariant says Mode == KeplerRails implies
                // KeplerState != null, but defend against the invalid state.
                if (vessel.State?.KeplerState == null) continue;

                // Need a current reference body to compute distances against.
                ReferenceBody currentBody = vessel.ReferenceBody;
                if (currentBody == null) continue;

                try
                {
                    EvaluationCount++;
                    EvaluateAndReroot(vessel, currentBody, tickNumber);
                }
                catch (Exception ex)
                {
                    string name = vessel != null && vessel.gameObject != null
                        ? vessel.gameObject.name : "(null)";
                    Debug.LogError(
                        $"VesselSoiRerootingDriver: vessel '{name}' threw during SOI " +
                        $"evaluation at tick {tickNumber}: {ex}");
                }
            }
        }

        /// <summary>
        /// Per-vessel SOI check + re-root dispatch. Extracted from
        /// <see cref="OnTickAdvanced"/> for readability. Throws on any internal
        /// failure; <see cref="OnTickAdvanced"/>'s try/catch handles isolation.
        ///
        /// Check order:
        /// <list type="number">
        ///   <item>Outward: distance to current body > current body's SOI radius
        ///   AND current body has a parent. If yes, re-root to parent.</item>
        ///   <item>Inward: iterate current body's children; if vessel is inside any
        ///   child's SOI, re-root to that child. First match wins.</item>
        /// </list>
        /// </summary>
        private static void EvaluateAndReroot(Vessel vessel, ReferenceBody currentBody, long tickNumber)
        {
            // Vessel's world position. GetWorldPosition handles the Kepler-rails
            // propagation internally (commit 040 wiring).
            var vesselPosWorld = vessel.GetWorldPosition().Value;
            var currentBodyPosWorld = currentBody.PositionWorld.Value;
            double distanceToCurrent = math.length(vesselPosWorld - currentBodyPosWorld);

            // Outward check: beyond current SOI?
            if (distanceToCurrent > currentBody.SoiRadiusMeters && currentBody.ParentBody != null)
            {
                ReferenceBody parentBody = currentBody.ParentBody;
                Debug.Log(
                    $"VesselSoiRerootingDriver: vessel '{vessel.gameObject.name}' exited SOI of " +
                    $"'{currentBody.gameObject.name}' at tick {tickNumber} (distance " +
                    $"{distanceToCurrent:E3} m > SOI {currentBody.SoiRadiusMeters:E3} m); " +
                    $"re-rooting to parent '{parentBody.gameObject.name}'");
                vessel.ReRootToBody(parentBody);
                RerootingCount++;
                return;  // One re-root per vessel per tick.
            }

            // Inward check: inside any child's SOI?
            List<ReferenceBody> children = BodyRegistry.GetChildrenOf(currentBody);
            for (int i = 0; i < children.Count; i++)
            {
                ReferenceBody child = children[i];
                if (child == null) continue;

                double distanceToChild = math.length(vesselPosWorld - child.PositionWorld.Value);
                if (distanceToChild < child.SoiRadiusMeters)
                {
                    Debug.Log(
                        $"VesselSoiRerootingDriver: vessel '{vessel.gameObject.name}' entered SOI " +
                        $"of '{child.gameObject.name}' at tick {tickNumber} (distance " +
                        $"{distanceToChild:E3} m < SOI {child.SoiRadiusMeters:E3} m); " +
                        $"re-rooting from '{currentBody.gameObject.name}'");
                    vessel.ReRootToBody(child);
                    RerootingCount++;
                    return;  // First match wins; one re-root per vessel per tick.
                }
            }
        }
    }
}
