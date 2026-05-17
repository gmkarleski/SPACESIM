using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.Coordinates
{
    /// <summary>
    /// Test-scene driver MonoBehaviour. Moves a GameObject along a configurable axis at a
    /// configurable speed, expressed in <see cref="WorldPosition"/>, and drives the
    /// <see cref="FloatingOriginManager"/>'s shift check every Update.
    ///
    /// This is a prototype-grade harness for the coordinate system, not a representative
    /// game-physics object. The default 10 km/s travel speed is chosen so the user can
    /// observe a shift event roughly every 5 seconds in Play mode at the 50 km default
    /// threshold — fast enough to verify the behavior interactively, not representative of
    /// real vessel velocities. Real vessels integrate forces and accelerations through the
    /// PhysX-active mode; this driver bypasses that for diagnostic purposes only.
    ///
    /// Attach to a GameObject with a <see cref="FloatingOriginAnchor"/> (the anchor handles
    /// applying the shift to the transform when the manager fires). This script reads its
    /// authoritative <see cref="WorldPosition"/> from <see cref="CurrentWorldPosition"/> and
    /// writes the corresponding <see cref="LocalPosition"/> to transform.position each frame.
    /// </summary>
    [RequireComponent(typeof(FloatingOriginAnchor))]
    public sealed class TestShiftDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Travel speed in kilometers per second. Test-harness value; not representative of game physics.")]
        private double speedKmPerSec = 10.0;

        [SerializeField, Tooltip("Travel direction (normalized at use-site). World-space axis.")]
        private Vector3 directionWorld = Vector3.right;

        [SerializeField, Tooltip("If non-null, render diagnostic text into this UI label. Optional.")]
        private UnityEngine.UI.Text diagnosticLabel;

        /// <summary>The driver's current authoritative world position.</summary>
        public WorldPosition CurrentWorldPosition { get; private set; }

        private void Start()
        {
            // Initialize from the GameObject's current transform position. The transform
            // position is in local-space; convert to world-space using the current origin.
            if (FloatingOriginManager.Instance == null)
            {
                Debug.LogError(
                    $"TestShiftDriver on '{gameObject.name}' requires FloatingOriginManager " +
                    $"in the scene. Driver disabled.");
                enabled = false;
                return;
            }
            var initialLocal = new LocalPosition(transform.position);
            CurrentWorldPosition = FloatingOriginManager.Instance.LocalToWorld(initialLocal);
        }

        private void Update()
        {
            if (FloatingOriginManager.Instance == null) return;

            // Advance world position by speed * direction * dt.
            double dt = Time.deltaTime;
            double3 deltaWorld = new double3(directionWorld.normalized.x, directionWorld.normalized.y, directionWorld.normalized.z)
                                 * (speedKmPerSec * 1000.0) * dt;
            CurrentWorldPosition = CurrentWorldPosition + deltaWorld;

            // Drive shift check on the manager.
            int beforeShifts = FloatingOriginManager.Instance.ShiftCount;
            bool shifted = FloatingOriginManager.Instance.MaybeShiftOrigin(CurrentWorldPosition);
            if (shifted)
            {
                Debug.Log(
                    $"[TestShiftDriver] Origin shifted (#{FloatingOriginManager.Instance.ShiftCount}). " +
                    $"New origin: {FloatingOriginManager.Instance.CurrentOrigin}. " +
                    $"Driver's world position: {CurrentWorldPosition}.");
            }

            // Update transform from authoritative world position via current origin.
            var local = FloatingOriginManager.Instance.WorldToLocal(CurrentWorldPosition);
            transform.position = local.Value;

            // Optional diagnostic label.
            if (diagnosticLabel != null)
            {
                var origin = FloatingOriginManager.Instance.CurrentOrigin;
                double distFromOrigin = CurrentWorldPosition.DistanceTo(origin);
                diagnosticLabel.text =
                    $"TEST-HARNESS SPEED ({speedKmPerSec:F1} km/s — not representative of game physics)\n" +
                    $"World: ({CurrentWorldPosition.Value.x:F1}, {CurrentWorldPosition.Value.y:F1}, {CurrentWorldPosition.Value.z:F1})\n" +
                    $"Local: ({local.Value.x:F1}, {local.Value.y:F1}, {local.Value.z:F1})\n" +
                    $"Origin: ({origin.Value.x:F1}, {origin.Value.y:F1}, {origin.Value.z:F1})\n" +
                    $"Dist from origin: {distFromOrigin:F1} m / Threshold: {FloatingOriginManager.Instance.ShiftThresholdMeters:F1} m\n" +
                    $"Shift count: {FloatingOriginManager.Instance.ShiftCount}";
            }
        }
    }
}
