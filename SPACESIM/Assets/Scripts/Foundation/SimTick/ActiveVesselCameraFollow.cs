using SpaceSim.Foundation.Coordinates;
using UnityEngine;

namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Tracks the active vessel during Play for visual validation. Per-frame, reads
    /// <see cref="SimTickController.Instance"/>'s
    /// <see cref="SimTickController.ActiveVessel"/>'s world position, converts it to
    /// local-space via <see cref="FloatingOriginManager.WorldToLocal"/>, and snaps this
    /// camera's <see cref="Transform.position"/> to that local position plus a
    /// configurable Inspector offset. Rotation is left alone — the scene author sets
    /// the camera's look direction once and the follow logic preserves it across
    /// frames (a debug camera that snaps rotation can disorient).
    ///
    /// <para>
    /// <strong>DEBUG / VALIDATION UTILITY — NOT A GAMEPLAY CAMERA.</strong> This
    /// MonoBehaviour exists to make Phase 1 foundations end-to-end visually
    /// observable in Play mode. Without it the active vessel leaves the camera
    /// frustum within seconds at the test driver's default 10 km/s initial velocity,
    /// and all Play feedback comes from the on-screen Canvas diagnostic. With it
    /// attached to the Main Camera, the vessel stays centered in view across
    /// floating-origin shifts. The gameplay camera (orbit / chase / cinematic modes,
    /// mouse / scroll input, FOV control, lerp damping) lands in Phase 3 alongside
    /// the rest of the flight-integration stack and replaces this utility.
    /// </para>
    ///
    /// <para>
    /// <strong>WORLD→LOCAL CONVERSION PATH.</strong>
    /// <see cref="FloatingOriginManager.WorldToLocal"/> is the same delegated math
    /// (<see cref="CoordinateMath.WorldToLocal"/>) that
    /// <see cref="FloatingOriginAnchor"/> implicitly relies on across origin shifts.
    /// One conversion source-of-truth means an origin shift mid-frame is handled
    /// correctly: <see cref="FloatingOriginManager.MaybeShiftOrigin"/> updates
    /// <see cref="FloatingOriginManager.CurrentOrigin"/> synchronously inside Step 6
    /// of the sim-tick cycle (before the camera's <see cref="LateUpdate"/> sees the
    /// world position again next frame), so the camera converts against the new
    /// origin and remains tracking the vessel without any jitter at the shift
    /// boundary. The alternative — registering the camera as a
    /// <see cref="FloatingOriginAnchor"/> and following the vessel's
    /// <c>Transform</c> directly — would require extending
    /// <see cref="IActiveVessel"/> with a <c>Transform</c> accessor, which is a
    /// cross-asmdef API change unjustified for a debug utility.
    /// </para>
    ///
    /// <para>
    /// <strong>RUN ORDER.</strong> The follow logic is in <see cref="LateUpdate"/> so
    /// it runs after all <see cref="Update"/> and physics integration in the same
    /// frame, capturing the vessel's final per-frame world position before render.
    /// <see cref="SimTickController.FixedUpdate"/> may have shifted the origin
    /// earlier in the same frame; that shift is fully visible by the time
    /// <see cref="LateUpdate"/> runs.
    /// </para>
    ///
    /// <para>
    /// <strong>SCENE WIRING.</strong> Attach to the Main Camera GameObject in any
    /// scene that needs visual active-vessel tracking. The default
    /// <see cref="_offset"/> frames the vessel from behind-and-above for the
    /// <c>TestVessels.unity</c> coast-along-+X test configuration; adjust per scene.
    /// </para>
    ///
    /// <para>
    /// See <c>docs/phase1_validation_readiness.md</c> Section C, commit candidate 1
    /// for the rationale behind landing this utility now: it's the cheapest single
    /// change that unblocks visual validation of the Phase 1 foundations.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActiveVesselCameraFollow : MonoBehaviour
    {
        [SerializeField]
        [Tooltip(
            "Local-space offset from the active vessel's position to this camera's " +
            "position. Default (0, 5, -20) frames the vessel from 20 units behind " +
            "(-Z, the default Unity camera look direction is +Z) and 5 units above " +
            "(+Y). Adjust to suit the scene's vessel scale and desired framing. " +
            "Setting Z to zero or positive will place the camera at or past the " +
            "vessel — the vessel will fall outside the default Unity camera frustum.")]
        private Vector3 _offset = new Vector3(0f, 5f, -20f);

        private bool _warnedAboutMissingCamera;
        private bool _warnedAboutMissingSimTickController;
        private bool _warnedAboutMissingActiveVessel;
        private bool _warnedAboutMissingOriginManager;

        // ----- Lifecycle -----

        private void Awake()
        {
            // Validation matches the WarpUIController discipline: log a single warning
            // for misconfiguration rather than disabling the component outright. A
            // warning is the right severity here — the follow logic short-circuits
            // null-safely each frame, so an attached-to-the-wrong-GameObject mistake
            // is observable in the Console but doesn't otherwise destabilise the scene.
            if (GetComponent<Camera>() == null)
            {
                Debug.LogWarning(
                    $"ActiveVesselCameraFollow on '{gameObject.name}': no Camera " +
                    "component on this GameObject. The follow logic will still run " +
                    "(it operates on this Transform regardless), but you probably " +
                    "want this on the Main Camera. See class doc SCENE WIRING.");
            }
        }

        private void LateUpdate()
        {
            // Null-safety at every level. The SimTickController singleton may not exist
            // yet on scene load (Awake order across managers is not guaranteed without
            // Script Execution Order configuration); ActiveVessel may not yet be set
            // (TestVesselDriver.Start calls SetActiveVessel, but its Start may run
            // after the camera's LateUpdate on the first frame); FloatingOriginManager
            // may not exist in a scene without origin-shift support. Each missing
            // dependency logs a once-per-lifetime warning so misconfiguration is
            // visible in the Console without spamming the log file.
            if (SimTickController.Instance == null)
            {
                WarnOnce(
                    ref _warnedAboutMissingSimTickController,
                    "SimTickController.Instance is null. The camera will not follow " +
                    "until a SimTickController GameObject is present in the scene.");
                return;
            }

            IActiveVessel activeVessel = SimTickController.Instance.ActiveVessel;
            if (activeVessel == null)
            {
                WarnOnce(
                    ref _warnedAboutMissingActiveVessel,
                    "SimTickController.Instance.ActiveVessel is null. The camera will " +
                    "not follow until SetActiveVessel(...) has been called with a " +
                    "non-null vessel. In TestVessels.unity, TestVesselDriver.Start " +
                    "performs this wiring once at scene load.");
                return;
            }

            if (FloatingOriginManager.Instance == null)
            {
                WarnOnce(
                    ref _warnedAboutMissingOriginManager,
                    "FloatingOriginManager.Instance is null. The camera will not " +
                    "follow until a FloatingOriginManager GameObject is present in " +
                    "the scene.");
                return;
            }

            // Convert the active vessel's double-precision world position into the
            // current single-precision local frame, then offset and assign. This is
            // the same WorldToLocal path that FloatingOriginAnchor would compute
            // implicitly across origin shifts (CoordinateMath.WorldToLocal). The
            // origin may have shifted earlier this frame inside Step 6; the WorldToLocal
            // call reads the now-current CurrentOrigin so we land correctly post-shift.
            WorldPosition worldPos = activeVessel.GetWorldPosition();
            LocalPosition localPos = FloatingOriginManager.Instance.WorldToLocal(worldPos);
            transform.position = localPos.Value + _offset;
        }

        // ----- Helpers -----

        /// <summary>
        /// Log the given message once per controller lifetime if the flag isn't set.
        /// Matches the once-per-lifetime warning pattern used by
        /// <see cref="SimTickController.Step6_DetectModeTransitions"/>.
        /// </summary>
        private void WarnOnce(ref bool flag, string message)
        {
            if (flag) return;
            Debug.LogWarning($"ActiveVesselCameraFollow: {message}");
            flag = true;
        }
    }
}
