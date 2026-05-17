using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.Coordinates
{
    /// <summary>
    /// Opt-in MonoBehaviour that translates its GameObject's transform when the floating
    /// origin shifts. Attach to any GameObject whose world-space position should be preserved
    /// across origin shifts.
    ///
    /// In commit 029 (no sim-tick controller yet), test scenes use this as the canonical
    /// shift-participation marker. From commit 030 onward, the sim-tick controller will
    /// attach <see cref="FloatingOriginAnchor"/> to vessel containers automatically; user-
    /// authored anchors continue to work alongside the controller-driven ones.
    ///
    /// SHIFT DISPATCH FROM SIM-TICK BOUNDARY (commit 033):
    /// As of commit 033, <see cref="FloatingOriginManager.MaybeShiftOrigin"/> is invoked by
    /// <c>SpaceSim.Foundation.SimTick.SimTickController</c>'s step 6 at the FixedUpdate
    /// cadence (once per FixedUpdate, not per analytic iteration). The shift is now anchored
    /// at a well-defined sim-tick boundary, matching the contract's §1.2 sequence (step 6:
    /// detect mode transitions). The rigidbody shift path here
    /// (<c>Rigidbody.position -= delta</c>) runs synchronously from the controller's step 6
    /// dispatch, so PhysX sees the teleport at a known sim-tick boundary rather than at an
    /// arbitrary point in the frame.
    ///
    /// DEFERRED REGISTRATION (commit 034):
    /// <see cref="OnEnable"/> uses <see cref="FloatingOriginManager.RegisterListenerSafe"/>
    /// rather than checking <see cref="FloatingOriginManager.Instance"/> directly. This
    /// eliminates the failure mode where the anchor's <c>OnEnable</c> fired before the
    /// manager's <c>Awake</c> and the anchor silently failed to receive shifts. With the
    /// safe path, the anchor registers in a static pending queue if the manager doesn't
    /// exist yet; the manager drains the queue when its own Awake runs. Order between
    /// anchor-Awake and manager-Awake no longer matters, which is the architecturally
    /// correct posture for runtime-spawned vessels in later commits (no scene-load ordering
    /// available). <see cref="OnDisable"/> uses the matching
    /// <see cref="FloatingOriginManager.UnregisterListenerSafe"/> so anchors that are
    /// destroyed while their registration is still pending get cleanly removed from the
    /// queue.
    ///
    /// REMAINING PHYSX-AWARE CONCERNS (deferred to vessel-cluster work, commit 035+):
    /// The prototype test scene uses a single isolated rigidbody. Production-quality shift
    /// handling for multi-vessel scenes still needs to address:
    ///   - Articulated rigidbody chains, joints, and active contacts that cross the shift
    ///     boundary (typical scenario: a docked vessel pair with internal joints; an
    ///     in-flight vessel with articulated parts).
    ///   - Whether to disable <c>Physics.Simulate</c> briefly during the shift to guarantee
    ///     a single atomic teleport from PhysX's perspective. The current implementation
    ///     trusts Unity's FixedUpdate ordering: <c>SimTickController.FixedUpdate</c> runs
    ///     before PhysX's internal step on the same FixedUpdate frame (per Unity's
    ///     standard execution order), so <c>Rigidbody.position</c> assignment is visible to
    ///     the subsequent PhysX step as a teleport. This is sufficient for the
    ///     single-vessel prototype; multi-vessel cases with active joints may surface edge
    ///     cases.
    /// These concerns are revisited in commit 035+ when vessel containers and joint
    /// management land. The single-vessel prototype test scene exercises the architecture
    /// successfully at the current scope.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingOriginAnchor : MonoBehaviour, IFloatingOriginListener
    {
        private Rigidbody _rb;

        private void Awake()
        {
            // Cache the rigidbody reference once; null is acceptable (transform-only anchor).
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            // Deferred-registration path: if the manager exists, registers directly; if not,
            // enqueues in a static pending list that the manager's Awake will drain. Either
            // way the anchor is wired up correctly without needing Script Execution Order
            // configuration. See class XML doc DEFERRED REGISTRATION section for rationale.
            FloatingOriginManager.RegisterListenerSafe(this);
        }

        private void OnDisable()
        {
            // Symmetric safe-unregister. Handles three cases uniformly:
            //   (a) listener is in the manager's active list — removed from active list.
            //   (b) listener is in the static pending queue (registered before manager's
            //       Awake fired and is being destroyed before drain) — removed from queue.
            //   (c) listener is in neither — no-op.
            FloatingOriginManager.UnregisterListenerSafe(this);
        }

        public void OnFloatingOriginShifted(double3 shiftDelta)
        {
            // The world-space delta is double-precision, but at the threshold (50 km default)
            // the magnitude is well within single-precision range. The float cast loses no
            // meaningful precision at this scale.
            Vector3 delta = new Vector3((float)shiftDelta.x, (float)shiftDelta.y, (float)shiftDelta.z);

            if (_rb != null)
            {
                // Canonical Unity floating-origin pattern for rigidbodies: assign directly to
                // Rigidbody.position so PhysX treats the shift as a single teleport rather
                // than an integrated movement. Velocity and angular velocity are unchanged.
                // See the SHIFT DISPATCH FROM SIM-TICK BOUNDARY note above for the
                // current architecture and the REMAINING PHYSX-AWARE CONCERNS list for
                // deferred multi-vessel concerns.
                _rb.position -= delta;
            }
            else
            {
                // Non-rigidbody case: shift the transform directly. Applies to static markers,
                // landmarks, UI overlays anchored to world positions, etc.
                transform.position -= delta;
            }
        }
    }
}
