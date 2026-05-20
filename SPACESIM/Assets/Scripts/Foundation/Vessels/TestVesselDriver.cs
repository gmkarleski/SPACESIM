using System;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Test-scene driver MonoBehaviour. Consolidates the responsibilities that were
    /// split across <c>TestShiftDriver</c> (motion + diagnostic UI) and a planned
    /// <c>TestVesselDriver</c> (initialization + input) in earlier design iterations.
    /// One script per vessel, attached to the vessel GameObject alongside the Vessel
    /// component itself.
    ///
    /// COMMIT 038 NOTE: this replaces the old
    /// <c>SpaceSim.Foundation.SimTick.TestShiftDriver</c> which pushed raw world
    /// positions to <c>SimTickController.SetActiveVesselWorldPosition</c>. The new
    /// driver takes a <see cref="Vessel"/> reference and lets the vessel own the
    /// position; <see cref="SimTickController.SetActiveVessel"/> takes the vessel
    /// directly. See commit 038 artifact for the architectural migration.
    ///
    /// LIFECYCLE:
    /// On <c>Start</c>:
    ///   1. Construct a fresh <see cref="VesselAuthoritativeState"/> (default values,
    ///      fresh GUID for VesselId)
    ///   2. Call <see cref="Vessel.Initialize"/> with the constructed state, the
    ///      <see cref="ReferenceBody"/> from Inspector, and PhysX-active mode
    ///   3. Register the vessel with <see cref="SimTickController"/> as the active vessel
    ///   4. Apply the initial velocity to the vessel's rigidbody
    ///
    /// On <c>Update</c>:
    ///   - Refresh the diagnostic UI label with current state (mode, world position,
    ///     local position, origin, distance, shift count, tick number)
    ///   - Listen for Space key: toggle vessel mode (PhysX-active ↔ Kepler-rails) by
    ///     calling the appropriate Vessel transition method
    ///
    /// INSPECTOR WIRING:
    /// User attaches this component to a GameObject that also has a <see cref="Vessel"/>
    /// component (the Vessel handles its own rigidbody and floating-origin anchor; this
    /// driver doesn't add them). User wires the four serialized fields via Inspector:
    /// the Vessel reference (typically self-reference), the ReferenceBody reference,
    /// the initial velocity, and the diagnostic Text label.
    /// </summary>
    [RequireComponent(typeof(Vessel))]
    public sealed class TestVesselDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("The Vessel component this driver controls. Typically the Vessel on the same GameObject.")]
        private Vessel _vessel;

        [SerializeField, Tooltip("The reference body whose gravity defines the vessel's orbital frame.")]
        private ReferenceBody _referenceBody;

        [SerializeField, Tooltip("Initial velocity applied to the vessel's rigidbody at Start, in m/s. Default 10 km/s in +X for visible shift-threshold crossings.")]
        private Vector3 _initialVelocity = new Vector3(10_000f, 0f, 0f);

        [SerializeField, Tooltip("Optional UI Text label for diagnostic output. Null = no label updates.")]
        private Text _diagnosticLabel;

        private bool _phase0LimitationLogged;

        // ----- Lifecycle -----

        private void Start()
        {
            if (_vessel == null)
            {
                Debug.LogError(
                    $"TestVesselDriver on '{gameObject.name}': Vessel reference is null. " +
                    $"Wire the Vessel component via Inspector. Disabling driver.");
                enabled = false;
                return;
            }
            if (_referenceBody == null)
            {
                Debug.LogError(
                    $"TestVesselDriver on '{gameObject.name}': ReferenceBody reference is null. " +
                    $"Wire a ReferenceBody GameObject via Inspector. Disabling driver.");
                enabled = false;
                return;
            }

            // Construct fresh state for this test vessel.
            var state = new VesselAuthoritativeState
            {
                VesselId = Guid.NewGuid(),
                DesignId = Guid.NewGuid(),
                Name = gameObject.name,
            };

            // Initialize the vessel in PhysX-active mode. Initialize adds Rigidbody and
            // FloatingOriginAnchor automatically; the user does NOT manually attach them.
            _vessel.Initialize(state, _referenceBody, PhysicsMode.PhysXActive);

            // Apply initial velocity to the rigidbody. After Initialize, _vessel.Rigidbody
            // is non-null because we asked for PhysX-active mode.
            if (_vessel.Rigidbody != null)
            {
                _vessel.Rigidbody.linearVelocity = _initialVelocity;
            }

            // Register the vessel with the sim-tick controller as the active vessel. Step 6
            // will pull position + mode from the vessel each tick.
            if (SimTickController.Instance != null)
            {
                SimTickController.Instance.SetActiveVessel(_vessel);
            }
            else
            {
                Debug.LogWarning(
                    $"TestVesselDriver on '{gameObject.name}': SimTickController.Instance is null at Start. " +
                    $"No active vessel will be registered. Floating-origin shifts will not fire until the " +
                    $"controller exists and SetActiveVessel is called.");
            }

            // Wire up the per-sim-tick mode transition trigger evaluator (commit 043).
            // The driver subscribes to SimTickController.TickAdvanced; the subscription
            // is in place from this point forward. However, the driver's master switch
            // (VesselTransitionDriver.Enabled) defaults to false — automatic mode
            // transitions do not fire in Play unless code elsewhere explicitly sets the
            // flag to true. Phase 0 / early Phase 1 expects the flag to stay off; Space-key
            // transitions and other imperative calls continue to drive mode changes.
            VesselTransitionDriver.Initialize();

            // Wire up the per-sim-tick SOI re-rooting driver (commit 044). Unlike
            // VesselTransitionDriver, this driver has no Enabled flag — SOI re-rooting
            // has real implementation (not stubs), so always-on is the right default.
            // In the single-body TestVessels.unity scene, the re-rooting check runs
            // every tick and correctly finds no crossings (vessel inside top-level
            // body's infinite SOI; no children to enter). The Console will show no
            // re-rooting log messages during Play with this scene.
            VesselSoiRerootingDriver.Initialize();

            // Wire up the per-sim-tick event prediction driver (commit 045). Always-on:
            // periapsis/apoapsis prediction is real math, populated for every
            // Kepler-rails vessel each tick. In Play, the predictor writes to
            // KeplerState.NextPeriapsisTick / NextApoapsisTick and updates the
            // SimTickController.EventQueue. The integration with
            // RunFixedUpdateCycle (warp respecting the queue) lands in commit 045
            // Stage 3 — until then the queue is populated but not consulted, so
            // observable Play behavior is unchanged.
            VesselEventPredictionDriver.Initialize();
        }

        private void Update()
        {
            if (_vessel == null) return;

            // Keypress: toggle mode on Space, via the new Input System.
            //
            // This project's Active Input Handling (Project Settings → Player) is set to
            // Input System Package, so the legacy UnityEngine.Input.GetKeyDown API throws
            // an InvalidOperationException at runtime. The new API reads keyboard state
            // through Keyboard.current; the null check guards the rare case where no
            // keyboard device is present (e.g., headless test runs, gamepad-only devices).
            //
            // The wasPressedThisFrame property is the new-Input-System equivalent of
            // legacy Input.GetKeyDown — fires once per key-down transition, false for held
            // keys after the first frame.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                if (_vessel.Mode == PhysicsMode.PhysXActive)
                {
                    _vessel.TransitionToKeplerRails();
                    LogPhase0LimitationOnce();
                }
                else if (_vessel.Mode == PhysicsMode.KeplerRails)
                {
                    _vessel.TransitionToPhysXActive();
                }
            }

            // Refresh diagnostic UI.
            if (_diagnosticLabel != null)
            {
                _diagnosticLabel.text = BuildDiagnosticText();
            }
        }

        // ----- Internals -----

        /// <summary>
        /// On the first PhysX-active → Kepler-rails transition, log a one-time reminder
        /// of the Phase 0 epoch-tick limitation so user-side observers aren't surprised
        /// when the vessel rewinds-on-return after sitting on Kepler-rails for any
        /// duration. The Vessel class's TransitionToPhysXActive comment block has the
        /// full explanation; this is just the runtime nudge.
        /// </summary>
        private void LogPhase0LimitationOnce()
        {
            if (_phase0LimitationLogged) return;
            Debug.Log(
                "PHASE 0 LIMITATION (commit 038): vessel is now on Kepler-rails. Position is " +
                "frozen at the transition tick — no propagator is wired yet. If you transition " +
                "back to PhysX-active immediately, the vessel round-trips correctly. If you wait " +
                "and then transition back, the vessel reappears at the transition position, " +
                "NOT at a propagated current position. This message logs once per driver lifetime.");
            _phase0LimitationLogged = true;
        }

        /// <summary>
        /// Build the multi-line diagnostic label content. Captures the test-harness
        /// disclaimer plus current mode-aware state plus floating-origin context plus
        /// sim-tick context.
        /// </summary>
        private string BuildDiagnosticText()
        {
            WorldPosition worldPos = _vessel.GetWorldPosition();
            Vector3 localPos = _vessel.transform.position;
            string modeLine;
            string positionContext;

            if (_vessel.Mode == PhysicsMode.PhysXActive)
            {
                modeLine = "Mode: PhysX-active";
                positionContext = "(rigidbody-driven; floating-origin shifts apply at 50 km threshold)";
            }
            else if (_vessel.Mode == PhysicsMode.KeplerRails)
            {
                long epochTick = _vessel.State?.KeplerState?.EpochTick ?? -1;
                modeLine = $"Mode: Kepler-rails (epoch tick: {epochTick})";
                positionContext = "(PHASE 0: position frozen at epoch; propagator not yet wired — see commit 038)";
            }
            else
            {
                modeLine = $"Mode: {_vessel.Mode}";
                positionContext = "(Phase 6+ mode; not exercised in commit 038)";
            }

            // Floating origin + sim tick context. Tolerate null managers gracefully so the
            // diagnostic stays informative even if the scene is mis-configured.
            string originLine;
            string distanceLine;
            string shiftCountLine;
            if (FloatingOriginManager.Instance != null)
            {
                var origin = FloatingOriginManager.Instance.CurrentOrigin;
                double distFromOrigin = worldPos.DistanceTo(origin);
                originLine = $"Origin: ({origin.Value.x:F1}, {origin.Value.y:F1}, {origin.Value.z:F1})";
                distanceLine = $"Dist from origin: {distFromOrigin:F1} m / Threshold: {FloatingOriginManager.Instance.ShiftThresholdMeters:F1} m";
                shiftCountLine = $"Shift count: {FloatingOriginManager.Instance.ShiftCount}";
            }
            else
            {
                originLine = "Origin: (FloatingOriginManager.Instance is null)";
                distanceLine = "Dist from origin: n/a";
                shiftCountLine = "Shift count: n/a";
            }

            string tickLine = SimTickController.Instance != null
                ? $"Sim-tick #: {SimTickController.Instance.TickNumber}"
                : "Sim-tick #: (SimTickController.Instance is null)";

            return
                $"TEST-HARNESS VESSEL ({_initialVelocity.magnitude / 1000.0:F1} km/s — not representative of game physics)\n" +
                $"World: ({worldPos.Value.x:F1}, {worldPos.Value.y:F1}, {worldPos.Value.z:F1})\n" +
                $"Local: ({localPos.x:F1}, {localPos.y:F1}, {localPos.z:F1})\n" +
                $"{originLine}\n" +
                $"{distanceLine}\n" +
                $"{shiftCountLine}\n" +
                $"{tickLine}\n" +
                $"{modeLine}\n" +
                $"{positionContext}\n" +
                $"Press Space to toggle mode (PhysX-active ↔ Kepler-rails).";
        }
    }
}
