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
    /// PROTOTYPE-QUALITY CAVEAT (revisit in commit 030):
    /// The rigidbody shift path here uses <c>Rigidbody.position -= delta</c>, which is the
    /// canonical Unity floating-origin pattern. It produces a clean teleport from PhysX's
    /// perspective for the prototype test scene's use case (single isolated rigidbody, no
    /// active joints or contacts crossing the shift). Production-quality shift handling at
    /// the sim-tick boundary may need more PhysX-aware care:
    ///   - Disabling Physics.Simulate briefly during the shift to guarantee a single
    ///     atomic teleport visible to no PhysX step (Unity's auto-sim mode normally runs
    ///     physics on FixedUpdate; we may need to call Physics.Simulate manually so we
    ///     control when PhysX sees the shifted state).
    ///   - Ensuring the shift is dispatched during a FixedUpdate boundary where PhysX
    ///     state is synchronized with Unity Transforms (not mid-frame where PhysX has
    ///     internal state not yet reflected to Transforms).
    ///   - Handling articulated rigidbodies, joints, and active contacts that cross the
    ///     shift boundary (typical scenario: a docked vessel pair, an in-flight vessel
    ///     with internal hinges).
    /// These concerns are listed in `docs/NETCODE_CONTRACT.md` §1.1 / §1.2 (PhysX as derived
    /// view, sim-tick atomicity). They are revisited when commit 030 lands the sim-tick
    /// controller and the manager's <see cref="FloatingOriginManager.MaybeShiftOrigin"/>
    /// call moves from MonoBehaviour-Update-driven to sim-tick-driven.
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
            // Register late in lifecycle (OnEnable rather than Awake) so that the manager,
            // which may be on a different GameObject with later Awake order, is available.
            // If the manager truly isn't present, the registration is silently skipped and a
            // warning logged; the anchor remains inert. This is acceptable for the prototype.
            if (FloatingOriginManager.Instance != null)
            {
                FloatingOriginManager.Instance.RegisterListener(this);
            }
            else
            {
                Debug.LogWarning(
                    $"FloatingOriginAnchor on '{gameObject.name}' enabled before " +
                    $"FloatingOriginManager singleton was available. Anchor will not receive " +
                    $"shifts. Ensure the FloatingOriginManager GameObject's script execution " +
                    $"order runs before anchored GameObjects, or instantiate the anchor in a " +
                    $"later phase.");
            }
        }

        private void OnDisable()
        {
            if (FloatingOriginManager.Instance != null)
            {
                FloatingOriginManager.Instance.UnregisterListener(this);
            }
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
                // See the PROTOTYPE-QUALITY CAVEAT above for production-quality considerations.
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
