using SpaceSim.Foundation.Coordinates;
using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Test-scene driver MonoBehaviour. Moves a GameObject along a configurable axis at a
    /// configurable speed, expressed in <see cref="WorldPosition"/>, and pushes the
    /// resulting world position to <see cref="SimTickController"/> each frame via
    /// <see cref="SimTickController.SetActiveVesselWorldPosition"/>. The controller's
    /// step 6 (mode-transition / floating-origin shift) then drives
    /// <see cref="FloatingOriginManager.MaybeShiftOrigin"/> at the FixedUpdate cadence.
    ///
    /// COMMIT 033 NOTE: this MonoBehaviour was previously in
    /// <c>SpaceSim.Foundation.Coordinates</c> (commit 029) and called
    /// <c>FloatingOriginManager.MaybeShiftOrigin</c> directly from Update. As of commit 033,
    /// the driver pushes the position to the sim-tick controller and the controller is the
    /// single authority on when shift detection runs. The file moved to the SimTick assembly
    /// because the bridging code naturally depends on both modules; SimTick already
    /// references Coordinates, so the dependency arrow is clean.
    ///
    /// This is a prototype-grade harness for the coordinate system + sim-tick controller,
    /// not a representative game-physics object. The default 10 km/s travel speed is chosen
    /// so the user can observe a shift event roughly every 5 seconds in Play mode at the
    /// 50 km default threshold — fast enough to verify the behavior interactively, not
    /// representative of real vessel velocities. Real vessels integrate forces and
    /// accelerations through the PhysX-active mode; this driver bypasses that for
    /// diagnostic purposes only.
    ///
    /// Attach to a GameObject with a <see cref="FloatingOriginAnchor"/> (the anchor handles
    /// applying the shift to the transform when the manager fires). The scene also needs a
    /// <see cref="SimTickController"/> on a separate GameObject (typically named
    /// "SimTickRoot") and a <see cref="FloatingOriginManager"/> on yet another (typically
    /// "FloatingOriginRoot"). All three managers must be present for the driver to function.
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

        private bool _warnedAboutMissingControllers;

        private void Start()
        {
            if (FloatingOriginManager.Instance == null)
            {
                Debug.LogError(
                    $"TestShiftDriver on '{gameObject.name}' requires FloatingOriginManager " +
                    $"in the scene. Driver disabled.");
                enabled = false;
                return;
            }
            if (SimTickController.Instance == null)
            {
                Debug.LogError(
                    $"TestShiftDriver on '{gameObject.name}' requires SimTickController " +
                    $"in the scene (commit 033 routes shifts through the sim-tick boundary). " +
                    $"Add a GameObject 'SimTickRoot' with the SimTickController component. " +
                    $"Driver disabled.");
                enabled = false;
                return;
            }

            // Initialize from the GameObject's current transform position. The transform
            // position is in local-space; convert to world-space using the current origin.
            var initialLocal = new LocalPosition(transform.position);
            CurrentWorldPosition = FloatingOriginManager.Instance.LocalToWorld(initialLocal);
            SimTickController.Instance.SetActiveVesselWorldPosition(CurrentWorldPosition);
        }

        private void Update()
        {
            // Guard against either manager being destroyed during play. The Start-time check
            // disables the driver if either is missing at scene load; this Update check
            // catches the case where they get destroyed mid-play.
            if (FloatingOriginManager.Instance == null || SimTickController.Instance == null)
            {
                if (!_warnedAboutMissingControllers)
                {
                    Debug.LogWarning(
                        $"TestShiftDriver on '{gameObject.name}': required manager (FloatingOriginManager " +
                        $"or SimTickController) is null mid-play. Driver continues to compute world position " +
                        $"but cannot push to the controller or render the transform. " +
                        $"This warning logs once per driver lifetime.");
                    _warnedAboutMissingControllers = true;
                }
                return;
            }

            // Advance world position by speed * direction * dt.
            double dt = Time.deltaTime;
            double3 deltaWorld = new double3(directionWorld.normalized.x, directionWorld.normalized.y, directionWorld.normalized.z)
                                 * (speedKmPerSec * 1000.0) * dt;
            CurrentWorldPosition = CurrentWorldPosition + deltaWorld;

            // Push to SimTickController; step 6 will drive the shift check at the next
            // FixedUpdate cadence.
            int beforeShifts = FloatingOriginManager.Instance.ShiftCount;
            SimTickController.Instance.SetActiveVesselWorldPosition(CurrentWorldPosition);

            // Shifts may have happened on prior FixedUpdate cycles; log when ShiftCount
            // changes between frames so the user sees a clear shift event in the console.
            // Note: shift detection happens in FixedUpdate (step 6), not in this Update,
            // so a shift can appear "delayed" relative to where the driver currently is.
            // At 30 Hz FixedUpdate and the default 10 km/s speed, the delay is < 33 ms,
            // which is imperceptible.
            int currentShifts = FloatingOriginManager.Instance.ShiftCount;
            if (currentShifts > beforeShifts)
            {
                Debug.Log(
                    $"[TestShiftDriver] Origin shifted (#{currentShifts}). " +
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
                long tickNumber = SimTickController.Instance.TickNumber;
                diagnosticLabel.text =
                    $"TEST-HARNESS SPEED ({speedKmPerSec:F1} km/s — not representative of game physics)\n" +
                    $"World: ({CurrentWorldPosition.Value.x:F1}, {CurrentWorldPosition.Value.y:F1}, {CurrentWorldPosition.Value.z:F1})\n" +
                    $"Local: ({local.Value.x:F1}, {local.Value.y:F1}, {local.Value.z:F1})\n" +
                    $"Origin: ({origin.Value.x:F1}, {origin.Value.y:F1}, {origin.Value.z:F1})\n" +
                    $"Dist from origin: {distFromOrigin:F1} m / Threshold: {FloatingOriginManager.Instance.ShiftThresholdMeters:F1} m\n" +
                    $"Shift count: {FloatingOriginManager.Instance.ShiftCount}\n" +
                    $"Sim-tick #: {tickNumber} (advanced via SimTickController.FixedUpdate)";
            }
        }
    }
}
