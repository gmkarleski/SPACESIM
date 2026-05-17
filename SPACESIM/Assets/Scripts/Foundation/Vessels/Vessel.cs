using System;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;
using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// MonoBehaviour wrapper around <see cref="VesselAuthoritativeState"/>. Owns the
    /// vessel's runtime Unity-side state (rigidbody, floating-origin anchor) and exposes
    /// the mode-transition operations per <c>docs/NETCODE_CONTRACT.md</c> §3.1.
    ///
    /// LIFECYCLE EXPECTATION:
    /// A Vessel GameObject in a scene needs <see cref="Initialize"/> called explicitly
    /// after component creation, before the vessel does anything useful. Initialize:
    ///   1. Stores the authoritative state and reference body.
    ///   2. Sets up the initial mode's component shape (adds Rigidbody +
    ///      FloatingOriginAnchor for PhysX-active; removes them for Kepler-rails).
    ///   3. Sets Mode + ModeEnteredAtTick + LastAdvancedTick.
    ///   4. Registers with <see cref="VesselRegistry"/>.
    ///
    /// <see cref="OnEnable"/> handles registration too (for the lifecycle case where the
    /// GameObject is enabled by Unity before Initialize fires). If Initialize hasn't run,
    /// OnEnable logs a warning and bails out — the vessel doesn't get registered until
    /// Initialize completes.
    ///
    /// MODE TRANSITION SCOPE (Phase 0):
    /// PhysX-active ↔ Kepler-rails per §3.1. Interstellar-cruise mode is deferred to
    /// Phase 6 per the Phase 0 artifact list (commit 037); transitions to/from
    /// <see cref="PhysicsMode.InterstellarCruise"/> log an error and no-op.
    ///
    /// PHASE 0 LIMITATION on Kepler-rails:
    /// The orbital state captured at <see cref="TransitionToKeplerRails"/> is frozen at
    /// the transition tick. No analytic propagation is yet wired in (Step 4 of the
    /// sim-tick cycle is still a stub). This means a vessel that transitions to
    /// Kepler-rails and immediately transitions back to PhysX-active will round-trip
    /// correctly (the state was just captured and is read back unchanged). A vessel that
    /// sits on Kepler-rails for any duration and then transitions back will reappear at
    /// the position it had at the moment of transition, not the propagated current
    /// position. This limitation is removed when the propagator commit lands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Vessel : MonoBehaviour, IActiveVessel
    {
        // ----- State -----

        /// <summary>Authoritative state of this vessel (the schema per §2.1).</summary>
        public VesselAuthoritativeState State { get; private set; }

        /// <summary>Current physics mode (convenience accessor; mirrors <c>State.Mode</c>).</summary>
        public PhysicsMode Mode => State?.Mode ?? PhysicsMode.PhysXActive;

        /// <summary>
        /// The Unity rigidbody currently attached to this GameObject. Non-null when in
        /// PhysX-active mode; null in Kepler-rails / Interstellar-cruise modes.
        /// </summary>
        public Rigidbody Rigidbody => _rb;

        /// <summary>Reference body whose gravity defines the vessel's orbital frame.</summary>
        public ReferenceBody ReferenceBody => _referenceBody;

        // ----- Internal state -----

        private Rigidbody _rb;
        private FloatingOriginAnchor _anchor;
        private ReferenceBody _referenceBody;
        private bool _initialized;

        // ----- Lifecycle -----

        private void OnEnable()
        {
            if (!_initialized)
            {
                // Vessel was enabled before Initialize completed. This is acceptable during
                // scene load (Initialize fires later and re-registers); but for production
                // scenes where Initialize never gets called, it's a configuration error.
                // The Initialize call will register with VesselRegistry itself if it's the
                // path that fires after OnEnable.
                return;
            }
            VesselRegistry.RegisterVesselSafe(this);
        }

        private void OnDisable()
        {
            VesselRegistry.UnregisterVesselSafe(this);
        }

        // ----- Initialization -----

        /// <summary>
        /// Set up the vessel's state, reference body, and initial mode. Must be called
        /// before the vessel does anything useful. Idempotent — calling twice with
        /// different parameters reconfigures the vessel (rare in practice; tests do this).
        ///
        /// COMPONENT SHAPE SET-UP:
        /// Initialize forces the GameObject into the correct component shape for the
        /// initial mode. If <paramref name="initialMode"/> is <see cref="PhysicsMode.PhysXActive"/>:
        ///   - Removes any pre-existing FloatingOriginAnchor (so the next add is clean)
        ///   - Adds a Rigidbody if one isn't already present
        ///   - Adds a FloatingOriginAnchor (after the Rigidbody, so the anchor's Awake
        ///     caches the rigidbody reference correctly)
        ///   - Disables gravity on the rigidbody by default (space sim convention)
        /// If <paramref name="initialMode"/> is <see cref="PhysicsMode.KeplerRails"/>:
        ///   - Removes any pre-existing Rigidbody
        ///   - Removes any pre-existing FloatingOriginAnchor
        ///
        /// <para>
        /// <paramref name="state"/>'s <c>Mode</c> field is overwritten to match
        /// <paramref name="initialMode"/>. The initial mode parameter is the source of
        /// truth so test sites don't need to remember to set state.Mode separately.
        /// </para>
        /// </summary>
        public void Initialize(
            VesselAuthoritativeState state,
            ReferenceBody referenceBody,
            PhysicsMode initialMode)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (referenceBody == null) throw new ArgumentNullException(nameof(referenceBody));
            if (initialMode == PhysicsMode.InterstellarCruise)
            {
                Debug.LogError(
                    $"Vessel.Initialize on '{gameObject.name}' requested InterstellarCruise " +
                    $"mode; that mode is deferred to Phase 6 per the Phase 0 artifact list. " +
                    $"Starting in PhysX-active instead.");
                initialMode = PhysicsMode.PhysXActive;
            }

            State = state;
            _referenceBody = referenceBody;
            State.Mode = initialMode;

            long tick = SimTickController.Instance != null
                ? SimTickController.Instance.TickNumber
                : 0L;
            State.ModeEnteredAtTick = tick;
            State.LastAdvancedTick = tick;

            // Force the GameObject into the right component shape for initialMode.
            if (initialMode == PhysicsMode.PhysXActive)
            {
                ConfigureForPhysXActive();
            }
            else
            {
                ConfigureForKeplerRails();
            }

            _initialized = true;

            // Register with the registry. If OnEnable already fired and bailed out due to
            // _initialized being false, this catches the case. RegisterVesselSafe dedups so
            // a double-register from OnEnable + Initialize is harmless.
            if (isActiveAndEnabled)
            {
                VesselRegistry.RegisterVesselSafe(this);
            }
        }

        // ----- Mode transitions -----

        /// <summary>
        /// Transition from PhysX-active to Kepler-rails mode per §3.1.
        ///
        /// Procedure (locked in commit 038):
        ///   1. Read current PhysX state (position, velocity, rotation, angular velocity).
        ///   2. Convert rigidbody position to <see cref="WorldPosition"/> and subtract the
        ///      reference body's world position to get relative position.
        ///   3. Compute orbital elements via <see cref="OrbitalElements.ComputeFromStateVector"/>.
        ///   4. Populate <see cref="VesselAuthoritativeState.KeplerState"/> with elements +
        ///      epoch tick.
        ///   5. Event predictions (NextPeriapsisTick etc.) left null — Phase 0 scope.
        ///   6. Set <see cref="VesselAuthoritativeState.Mode"/> = KeplerRails;
        ///      <c>ModeEnteredAtTick</c> = current tick.
        ///   7. Clear <see cref="VesselAuthoritativeState.PhysXState"/>.
        ///   8. Destroy FloatingOriginAnchor and Rigidbody components.
        ///
        /// No-op (logs warning, returns) if already in Kepler-rails mode.
        /// </summary>
        public void TransitionToKeplerRails()
        {
            if (!_initialized)
            {
                Debug.LogWarning($"Vessel.TransitionToKeplerRails on '{gameObject.name}' before Initialize; ignored.");
                return;
            }
            if (State.Mode == PhysicsMode.KeplerRails)
            {
                Debug.LogWarning($"Vessel '{gameObject.name}' already on Kepler-rails; TransitionToKeplerRails ignored.");
                return;
            }
            if (State.Mode == PhysicsMode.InterstellarCruise)
            {
                Debug.LogError(
                    $"Vessel '{gameObject.name}' is in InterstellarCruise mode (Phase 6 scope); " +
                    $"direct InterstellarCruise → KeplerRails not implemented in Phase 0.");
                return;
            }
            if (_rb == null)
            {
                Debug.LogError(
                    $"Vessel '{gameObject.name}' in PhysX-active mode but Rigidbody is null. " +
                    $"Cannot transition to Kepler-rails. State is inconsistent; check Initialize was called correctly.");
                return;
            }

            long tick = SimTickController.Instance != null
                ? SimTickController.Instance.TickNumber
                : 0L;

            // Step 1: Read PhysX state.
            Vector3 rbPosLocal = _rb.position;
            Vector3 rbVel = _rb.linearVelocity;
            Quaternion rbRot = _rb.rotation;
            Vector3 rbAngVel = _rb.angularVelocity;

            // Step 2: Convert rigidbody position to world coords, then subtract reference
            // body's world position to get position relative to the gravity body. The
            // rigidbody's position is in Unity world space (LocalPosition relative to the
            // current floating origin); convert through FloatingOriginManager to get
            // double-precision world coordinates.
            WorldPosition vesselWorldPos;
            if (FloatingOriginManager.Instance != null)
            {
                vesselWorldPos = FloatingOriginManager.Instance.LocalToWorld(new LocalPosition(rbPosLocal));
            }
            else
            {
                // Manager not present: treat rigidbody position as world position. This
                // path mostly fires in EditMode tests that don't set up a FloatingOriginManager.
                vesselWorldPos = new WorldPosition(rbPosLocal.x, rbPosLocal.y, rbPosLocal.z);
            }

            double3 relPosition = vesselWorldPos.Value - _referenceBody.PositionWorld.Value;
            double3 relVelocity = new double3(rbVel.x, rbVel.y, rbVel.z);

            // Step 3: Compute orbital elements.
            KeplerState keplerState = OrbitalElements.ComputeFromStateVector(
                relPosition, relVelocity, _referenceBody.Mu, tick, _referenceBody.BodyId);

            // Step 4: Populate state.
            State.KeplerState = keplerState;

            // Step 5: Event predictions left null (Phase 0 scope).
            // (KeplerState already has these as null by default.)

            // Step 6: Set mode.
            State.Mode = PhysicsMode.KeplerRails;
            State.ModeEnteredAtTick = tick;
            State.LastAdvancedTick = tick;

            // Step 7: Clear PhysX state.
            State.PhysXState = null;

            // Step 8: Destroy anchor and rigidbody components. Anchor first so its
            // OnDisable can unregister from FloatingOriginManager while the rigidbody
            // is still around (the anchor's destroy path doesn't actually depend on the
            // rigidbody, but ordering is conservative).
            DestroyComponentSafe(_anchor);
            _anchor = null;
            DestroyComponentSafe(_rb);
            _rb = null;
        }

        /// <summary>
        /// Transition from Kepler-rails to PhysX-active mode per §3.1.
        ///
        /// Procedure (locked in commit 038):
        ///   1. From <see cref="VesselAuthoritativeState.KeplerState"/>, compute position +
        ///      velocity. PHASE 0 LIMITATION: uses epoch-tick state (no propagator yet).
        ///   2. Add Rigidbody first, then FloatingOriginAnchor (the anchor's Awake caches
        ///      the rigidbody reference; must exist first).
        ///   3. Configure rigidbody: position, velocity, rotation, angular velocity, no gravity.
        ///   4. Populate <see cref="VesselAuthoritativeState.PhysXState"/>.
        ///   5. Set <see cref="VesselAuthoritativeState.Mode"/> = PhysXActive;
        ///      <c>ModeEnteredAtTick</c> = current tick.
        ///   6. Clear <see cref="VesselAuthoritativeState.KeplerState"/>.
        ///
        /// No-op (logs warning, returns) if already in PhysX-active mode.
        /// </summary>
        public void TransitionToPhysXActive()
        {
            if (!_initialized)
            {
                Debug.LogWarning($"Vessel.TransitionToPhysXActive on '{gameObject.name}' before Initialize; ignored.");
                return;
            }
            if (State.Mode == PhysicsMode.PhysXActive)
            {
                Debug.LogWarning($"Vessel '{gameObject.name}' already PhysX-active; TransitionToPhysXActive ignored.");
                return;
            }
            if (State.Mode == PhysicsMode.InterstellarCruise)
            {
                Debug.LogError(
                    $"Vessel '{gameObject.name}' is in InterstellarCruise mode (Phase 6 scope); " +
                    $"direct InterstellarCruise → PhysXActive not implemented in Phase 0.");
                return;
            }
            if (State.KeplerState == null)
            {
                Debug.LogError(
                    $"Vessel '{gameObject.name}' in Kepler-rails mode but KeplerState is null. " +
                    $"Cannot transition to PhysX-active. State is inconsistent.");
                return;
            }

            long tick = SimTickController.Instance != null
                ? SimTickController.Instance.TickNumber
                : 0L;

            // Step 1: Compute position + velocity from orbital elements.
            //
            // PHASE 0 SIMPLIFICATION: position computed at TrueAnomalyAtEpoch, not
            // propagated to current tick. Propagator commits later. Round-trip-immediately
            // works correctly; long Kepler-rails sits will rewind on re-activation. The
            // ν₀ stored in KeplerState is the value captured at TransitionToKeplerRails;
            // until a propagator advances it through Kepler's equation as time passes,
            // re-evaluating ComputeStateVector with the same ν₀ produces the same (r, v).
            (double3 relPosition, double3 relVelocity) = OrbitalElements.ComputeStateVector(
                State.KeplerState, State.KeplerState.TrueAnomalyAtEpoch, _referenceBody.Mu);

            // Convert relative position back to world position via the reference body.
            double3 absoluteWorld = _referenceBody.PositionWorld.Value + relPosition;
            WorldPosition vesselWorldPos = new WorldPosition(absoluteWorld.x, absoluteWorld.y, absoluteWorld.z);

            // Convert world position to local position via the current floating origin.
            Vector3 unityLocal;
            if (FloatingOriginManager.Instance != null)
            {
                LocalPosition local = FloatingOriginManager.Instance.WorldToLocal(vesselWorldPos);
                unityLocal = local.Value;
            }
            else
            {
                unityLocal = new Vector3(
                    (float)absoluteWorld.x, (float)absoluteWorld.y, (float)absoluteWorld.z);
            }

            // Step 2: Add rigidbody first, then anchor.
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.position = unityLocal;
            _rb.linearVelocity = new Vector3((float)relVelocity.x, (float)relVelocity.y, (float)relVelocity.z);
            // PHASE 0 SIMPLIFICATION: orientation reset to identity rather than preserved
            // per §3.1 step 7. The contract specifies that orientation captured at
            // TransitionToKeplerRails should be preserved across the rail period and
            // restored on re-activation; the Phase 0 implementation drops that history
            // and starts the rigidbody at identity rotation with zero angular velocity.
            // Future rotation-handling commit will preserve pre-transition orientation
            // by storing it alongside KeplerState (proposed: an OrientationOnRails field
            // outside the six classical elements, since orientation is independent of
            // orbital dynamics for a torque-free body on rails).
            _rb.rotation = Quaternion.identity;
            _rb.angularVelocity = Vector3.zero;

            _anchor = gameObject.AddComponent<FloatingOriginAnchor>();

            // Step 4: Populate PhysX state.
            State.PhysXState = new PhysXState
            {
                PositionWorld = vesselWorldPos,
                VelocityWorld = relVelocity,
                Orientation = quaternion.identity,
                AngularVelocity = double3.zero,
                ReferenceBodyId = _referenceBody.BodyId,
                FloatingOrigin = FloatingOriginManager.Instance != null
                    ? FloatingOriginManager.Instance.CurrentOrigin.Value
                    : double3.zero,
                RigidbodyHandle = _rb,
                ActiveThrustN = 0.0,
                ActiveThrustDirection = new double3(0, 0, 1),
                AtmosphericDensity = 0.0,
                AtmosphericVelocityRel = double3.zero,
            };

            // Step 5: Set mode.
            State.Mode = PhysicsMode.PhysXActive;
            State.ModeEnteredAtTick = tick;
            State.LastAdvancedTick = tick;

            // Step 6: Clear Kepler state.
            State.KeplerState = null;
        }

        // ----- Position accessor -----

        /// <summary>
        /// Return the vessel's current world position regardless of mode.
        ///
        /// In PhysX-active mode: reads from the rigidbody position and converts through
        /// the floating-origin manager.
        /// In Kepler-rails mode: computes position from orbital elements at the
        /// vessel's current true anomaly (PHASE 0: at TrueAnomalyAtEpoch — propagator
        /// not yet wired). Adds the reference body's world position.
        /// In InterstellarCruise mode (Phase 6+): would read CruiseState.PositionGalactic.
        /// PHASE 0: returns WorldPosition.Zero with an error log.
        /// </summary>
        public WorldPosition GetWorldPosition()
        {
            if (!_initialized || State == null)
            {
                return WorldPosition.Zero;
            }

            switch (State.Mode)
            {
                case PhysicsMode.PhysXActive:
                    if (_rb == null) return WorldPosition.Zero;
                    if (FloatingOriginManager.Instance != null)
                    {
                        return FloatingOriginManager.Instance.LocalToWorld(new LocalPosition(_rb.position));
                    }
                    return new WorldPosition(_rb.position.x, _rb.position.y, _rb.position.z);

                case PhysicsMode.KeplerRails:
                    if (State.KeplerState == null) return WorldPosition.Zero;
                    (double3 relPosition, _) = OrbitalElements.ComputeStateVector(
                        State.KeplerState, State.KeplerState.TrueAnomalyAtEpoch, _referenceBody.Mu);
                    double3 absoluteWorld = _referenceBody.PositionWorld.Value + relPosition;
                    return new WorldPosition(absoluteWorld.x, absoluteWorld.y, absoluteWorld.z);

                case PhysicsMode.InterstellarCruise:
                    Debug.LogError(
                        $"Vessel '{gameObject.name}' GetWorldPosition for InterstellarCruise mode " +
                        $"is Phase 6 scope; returning Zero.");
                    return WorldPosition.Zero;

                default:
                    return WorldPosition.Zero;
            }
        }

        // ----- Internal helpers -----

        /// <summary>
        /// Configure the GameObject for PhysX-active mode: ensure Rigidbody and
        /// FloatingOriginAnchor are present. Adds them if absent.
        /// </summary>
        private void ConfigureForPhysXActive()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
                _rb.useGravity = false;
            }

            _anchor = GetComponent<FloatingOriginAnchor>();
            if (_anchor == null)
            {
                // Rigidbody must already be on the GameObject so FloatingOriginAnchor.Awake
                // can cache its reference. The order above ensures that.
                _anchor = gameObject.AddComponent<FloatingOriginAnchor>();
            }
        }

        /// <summary>
        /// Configure the GameObject for Kepler-rails mode: ensure Rigidbody and
        /// FloatingOriginAnchor are absent. Removes them if present.
        /// </summary>
        private void ConfigureForKeplerRails()
        {
            var existingAnchor = GetComponent<FloatingOriginAnchor>();
            if (existingAnchor != null)
            {
                DestroyComponentSafe(existingAnchor);
            }
            _anchor = null;

            var existingRb = GetComponent<Rigidbody>();
            if (existingRb != null)
            {
                DestroyComponentSafe(existingRb);
            }
            _rb = null;
        }

        /// <summary>
        /// Destroy a Unity component, using <see cref="UnityEngine.Object.DestroyImmediate"/>
        /// in EditMode (when Application.isPlaying is false) and
        /// <see cref="UnityEngine.Object.Destroy"/> in Play mode.
        ///
        /// EditMode tests use DestroyImmediate because Destroy is deferred to the next
        /// frame, which never arrives in EditMode (no Update loop). Play mode prefers
        /// Destroy because DestroyImmediate during a frame's update phase can cause
        /// reentrancy issues with Unity's iteration over components.
        /// </summary>
        private static void DestroyComponentSafe(Component component)
        {
            if (component == null) return;
            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }
    }
}
