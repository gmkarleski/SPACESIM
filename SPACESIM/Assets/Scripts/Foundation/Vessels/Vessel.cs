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
    /// KEPLER-RAILS PROPAGATION:
    /// On <see cref="TransitionToKeplerRails"/>, the vessel's state vector is converted
    /// to classical orbital elements (a, e, i, Ω, ω, ν₀) at epoch tick. Subsequent
    /// position queries via <see cref="GetWorldPosition"/> and re-activation via
    /// <see cref="TransitionToPhysXActive"/> use <see cref="KeplerPropagator"/> to
    /// advance the true anomaly from ν₀ through Kepler's equation, so the vessel's
    /// position on rails advances correctly with sim time. The sim-tick cycle's
    /// step 4 stub does not drive this propagation; computation is on-demand at
    /// query time. Orientation handling remains a Phase 0 simplification — see the
    /// inline comment in <see cref="TransitionToPhysXActive"/> for the deferred work.
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
        ///
        /// <para>
        /// <paramref name="state"/>'s <c>Mode</c> field is overwritten to match
        /// <paramref name="initialMode"/>. The initial mode parameter is the source of
        /// truth so test sites don't need to remember to set state.Mode separately.
        /// </para>
        ///
        /// STATE INVARIANT (since commit 042):
        /// After Initialize completes, the schema invariant from
        /// <c>docs/NETCODE_CONTRACT.md</c> §2.1 holds: <c>Mode == X</c> implies
        /// <c>XState != null</c> for X in {PhysXActive, KeplerRails, InterstellarCruise}.
        /// This 3-arg overload handles only <see cref="PhysicsMode.PhysXActive"/>
        /// cleanly — it constructs a fresh <see cref="PhysXState"/> implicitly when
        /// the rigidbody is added.
        ///
        /// This 3-arg overload REJECTS <see cref="PhysicsMode.KeplerRails"/>:
        /// constructing a Kepler-rails vessel requires caller-provided orbital elements,
        /// because the math has no way to invent them from nothing. Callers that need a
        /// rails-mode vessel use the 4-arg overload
        /// <see cref="Initialize(VesselAuthoritativeState, ReferenceBody, PhysicsMode, KeplerState)"/>.
        /// A 3-arg call with KeplerRails logs an error and falls back to PhysXActive,
        /// parallel to the existing InterstellarCruise rejection.
        ///
        /// <see cref="PhysicsMode.InterstellarCruise"/> is also rejected (Phase 6 scope);
        /// when interstellar mode ships, an analogous overload accepting
        /// <c>initialCruiseState</c> will land then.
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
            if (initialMode == PhysicsMode.KeplerRails)
            {
                Debug.LogError(
                    $"Vessel.Initialize on '{gameObject.name}' requested KeplerRails mode " +
                    $"via the 3-arg overload, but KeplerRails requires caller-provided " +
                    $"orbital elements. Use the 4-arg overload " +
                    $"Initialize(state, body, KeplerRails, initialKeplerState). " +
                    $"Falling back to PhysX-active.");
                initialMode = PhysicsMode.PhysXActive;
            }

            InitializeCore(state, referenceBody, initialMode, initialKeplerState: null);
        }

        /// <summary>
        /// 4-arg overload for Kepler-rails initialization with caller-provided orbital
        /// elements. Use this when constructing a vessel directly in Kepler-rails mode
        /// (e.g., save-load that restores a vessel from stored orbital elements, or
        /// integration tests that need a vessel on rails without going through
        /// <see cref="TransitionToKeplerRails"/>).
        ///
        /// STATE INVARIANT: this overload guarantees the §2.1 schema invariant holds
        /// after return — if <paramref name="initialMode"/> is
        /// <see cref="PhysicsMode.KeplerRails"/>, <c>State.KeplerState</c> is populated
        /// from <paramref name="initialKeplerState"/>.
        ///
        /// Per-mode behavior:
        /// <list type="bullet">
        ///   <item><see cref="PhysicsMode.KeplerRails"/>: requires
        ///   <paramref name="initialKeplerState"/> non-null. If null, logs error and
        ///   falls back to PhysX-active.</item>
        ///   <item><see cref="PhysicsMode.PhysXActive"/>: this overload is the wrong
        ///   tool — <paramref name="initialKeplerState"/> is ignored, an error is
        ///   logged, and initialization proceeds in PhysX-active mode (so callers don't
        ///   end up with a partially-initialized vessel). PhysX-active callers should
        ///   use the 3-arg overload.</item>
        ///   <item><see cref="PhysicsMode.InterstellarCruise"/>: existing Phase 6
        ///   rejection; <paramref name="initialKeplerState"/> is ignored.</item>
        /// </list>
        /// </summary>
        public void Initialize(
            VesselAuthoritativeState state,
            ReferenceBody referenceBody,
            PhysicsMode initialMode,
            KeplerState initialKeplerState)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (referenceBody == null) throw new ArgumentNullException(nameof(referenceBody));
            if (initialMode == PhysicsMode.InterstellarCruise)
            {
                Debug.LogError(
                    $"Vessel.Initialize on '{gameObject.name}' requested InterstellarCruise " +
                    $"mode; that mode is deferred to Phase 6 per the Phase 0 artifact list. " +
                    $"Starting in PhysX-active instead. (initialKeplerState ignored.)");
                initialMode = PhysicsMode.PhysXActive;
                initialKeplerState = null;
            }
            if (initialMode == PhysicsMode.PhysXActive)
            {
                Debug.LogError(
                    $"Vessel.Initialize on '{gameObject.name}' used the 4-arg overload with " +
                    $"PhysicsMode.PhysXActive; this overload is for KeplerRails initialization. " +
                    $"PhysXActive does not accept initialKeplerState — use the 3-arg overload. " +
                    $"Proceeding in PhysX-active mode with initialKeplerState ignored.");
                initialKeplerState = null;
            }
            else if (initialMode == PhysicsMode.KeplerRails && initialKeplerState == null)
            {
                Debug.LogError(
                    $"Vessel.Initialize on '{gameObject.name}' requested KeplerRails mode " +
                    $"with null initialKeplerState; orbital elements are required. " +
                    $"Falling back to PhysX-active.");
                initialMode = PhysicsMode.PhysXActive;
            }

            InitializeCore(state, referenceBody, initialMode, initialKeplerState);
        }

        /// <summary>
        /// Shared body of both Initialize overloads. Stores state, sets mode and tick
        /// bookkeeping, populates <see cref="VesselAuthoritativeState.KeplerState"/>
        /// (when applicable), forces the GameObject's component shape, and registers
        /// with <see cref="VesselRegistry"/>.
        ///
        /// Precondition: the caller has already validated <paramref name="initialMode"/>
        /// and mode-specific state parameters. <see cref="InitializeCore"/> trusts its
        /// inputs and does no further validation — the per-overload public methods do
        /// the mode-rejection / fallback work.
        /// </summary>
        private void InitializeCore(
            VesselAuthoritativeState state,
            ReferenceBody referenceBody,
            PhysicsMode initialMode,
            KeplerState initialKeplerState)
        {
            State = state;
            _referenceBody = referenceBody;
            State.Mode = initialMode;

            long tick = SimTickController.Instance != null
                ? SimTickController.Instance.TickNumber
                : 0L;
            State.ModeEnteredAtTick = tick;
            State.LastAdvancedTick = tick;

            // Populate mode-specific state per the §2.1 schema invariant. Only
            // KeplerRails uses the caller-provided initialKeplerState here; PhysXActive's
            // PhysXState gets constructed implicitly inside ConfigureForPhysXActive +
            // the post-Initialize lifecycle (the rigidbody add happens here; PhysXState
            // population happens at the next mode-transition or sim-tick read).
            if (initialMode == PhysicsMode.KeplerRails)
            {
                State.KeplerState = initialKeplerState;
                State.PhysXState = null;
            }
            else  // PhysXActive (InterstellarCruise was rewritten in the public overloads)
            {
                State.KeplerState = null;
            }

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

            // Step 9 (commit 045): invalidate any stale event-queue entries from a
            // prior orbit. The VesselEventPredictionDriver will repopulate on the
            // next TickAdvanced now that this vessel is in KeplerRails. Defensive
            // null checks: SimTickController.Instance may be absent in EditMode
            // tests that don't construct the controller.
            SimTickController.Instance?.EventQueue?.RemoveVesselEntries(State.VesselId);
        }

        /// <summary>
        /// Transition from Kepler-rails to PhysX-active mode per §3.1.
        ///
        /// Procedure (locked in commit 038, propagator wired in commit 040):
        ///   1. From <see cref="VesselAuthoritativeState.KeplerState"/>, propagate
        ///      position + velocity from epoch tick to current tick via
        ///      <see cref="KeplerPropagator.PropagateState"/>.
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

            // Step 1: Propagate position + velocity from epoch tick to current tick.
            //
            // KeplerPropagator.PropagateState advances the true anomaly from
            // KeplerState.TrueAnomalyAtEpoch (the ν₀ captured at TransitionToKeplerRails)
            // through Kepler's equation by (currentTick - EpochTick) * tickIntervalSeconds.
            // The propagator handles dt == 0 as a short-circuit, so a round-trip-
            // immediately transition reproduces the entry state exactly.
            //
            // The SimTickController instance may be absent in EditMode tests that don't
            // construct the controller. In that case we fall back to the epoch tick,
            // which makes the propagator a no-op (dt == 0) and preserves the legacy
            // behavior for those tests.
            long propagationTick = SimTickController.Instance != null
                ? SimTickController.Instance.TickNumber
                : State.KeplerState.EpochTick;
            (double3 relPosition, double3 relVelocity) = KeplerPropagator.PropagateState(
                State.KeplerState, propagationTick, _referenceBody.Mu, SimTickController.SimTickIntervalSeconds);

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

            // Step 7 (commit 045): vessel is leaving KeplerRails; remove any
            // event-queue entries. The driver skips non-KeplerRails vessels so no
            // repopulation happens until/unless the vessel transitions back.
            SimTickController.Instance?.EventQueue?.RemoveVesselEntries(State.VesselId);
        }

        // ----- SOI re-rooting (commit 044) -----

        /// <summary>
        /// Re-root this vessel's orbital state from its current reference body to
        /// <paramref name="newBody"/>. Intra-mode operation: vessel stays on
        /// Kepler-rails before and after; only the reference body and the orbital
        /// elements change. Operational analog to <see cref="TransitionToKeplerRails"/>
        /// but no mode transition fires.
        ///
        /// PROCEDURE:
        /// <list type="number">
        ///   <item>Propagate current orbital state to the current sim-tick to obtain
        ///   position and velocity relative to the current reference body.</item>
        ///   <item>Delegate to <see cref="OrbitalElements.ReRootStateVector"/> to
        ///   transform the state vector into the new body's frame and produce a fresh
        ///   <see cref="KeplerState"/>.</item>
        ///   <item>Update <see cref="VesselAuthoritativeState.KeplerState"/>, the
        ///   cached reference-body field, and <c>LastAdvancedTick</c>.</item>
        /// </list>
        ///
        /// CALLED BY: <see cref="VesselSoiRerootingDriver.OnTickAdvanced"/> when the
        /// per-tick SOI check detects the vessel has crossed an SOI boundary (outward
        /// to the parent, or inward into a child body's SOI).
        ///
        /// PHASE 4+ VELOCITY HAZARD: the propagated velocity is relative-to-current-body.
        /// In Phase 1 (stationary bodies), this is identical to absolute world velocity
        /// and to velocity-relative-to-any-other-body. When bodies orbit (Phase 4+),
        /// the <see cref="OrbitalElements.ReRootStateVector"/> signature will extend to
        /// take per-body velocity parameters; this method will need to pass them through.
        /// See the helper's XML doc for the full migration plan.
        ///
        /// PUBLIC for parity with the existing <c>TransitionTo*</c> methods (same
        /// asmdef-visibility-via-public convention; the production driver lives in the
        /// Vessels asmdef alongside Vessel). The only intended invokers are
        /// <see cref="VesselSoiRerootingDriver"/> and EditMode tests; player code and
        /// other systems should not call this directly.
        /// </summary>
        public void ReRootToBody(ReferenceBody newBody)
        {
            if (!_initialized)
            {
                Debug.LogWarning(
                    $"Vessel.ReRootToBody on '{gameObject.name}' before Initialize; ignored.");
                return;
            }
            if (newBody == null)
            {
                Debug.LogError(
                    $"Vessel.ReRootToBody on '{gameObject.name}': newBody is null. " +
                    $"Cannot re-root without a destination body.");
                return;
            }
            if (State.Mode != PhysicsMode.KeplerRails)
            {
                Debug.LogError(
                    $"Vessel.ReRootToBody on '{gameObject.name}': vessel is in {State.Mode} " +
                    $"mode, not KeplerRails. SOI re-rooting is intra-Kepler-rails only.");
                return;
            }
            if (State.KeplerState == null)
            {
                Debug.LogError(
                    $"Vessel.ReRootToBody on '{gameObject.name}': KeplerState is null. " +
                    $"State is inconsistent (Mode == KeplerRails should imply KeplerState != null).");
                return;
            }

            long currentTick = SimTickController.Instance != null
                ? SimTickController.Instance.TickNumber
                : State.KeplerState.EpochTick;

            // Propagate current orbital state forward to obtain (position, velocity)
            // relative to the current reference body at the current tick. Mirrors the
            // pattern in TransitionToPhysXActive (commit 040).
            (double3 relPosition, double3 relVelocity) = KeplerPropagator.PropagateState(
                State.KeplerState, currentTick, _referenceBody.Mu,
                SimTickController.SimTickIntervalSeconds);

            // Delegate to the orbital-math helper. Phase 1: velocity passes through
            // unchanged because both bodies are stationary in this prototype.
            KeplerState newKeplerState = OrbitalElements.ReRootStateVector(
                currentPositionRelativeToCurrentBody: relPosition,
                currentVelocity: relVelocity,
                currentBodyPositionWorld: _referenceBody.PositionWorld,
                newBodyPositionWorld: newBody.PositionWorld,
                newBodyMu: newBody.Mu,
                epochTick: currentTick,
                newBodyId: newBody.BodyId);

            // Commit the new state. Cached _referenceBody updates so subsequent calls
            // (GetWorldPosition, EvaluateTransitionTriggers, propagator queries) use the
            // new body's μ and position automatically.
            State.KeplerState = newKeplerState;
            _referenceBody = newBody;
            State.LastAdvancedTick = currentTick;

            // commit 045: orbital elements changed; invalidate predictions in the
            // event queue. The driver recomputes on the next TickAdvanced now that
            // the new KeplerState (with different μ, different orbit shape) is
            // committed.
            SimTickController.Instance?.EventQueue?.RemoveVesselEntries(State.VesselId);
        }

        // ----- Position accessor -----

        /// <summary>
        /// Return the vessel's current world position regardless of mode.
        ///
        /// In PhysX-active mode: reads from the rigidbody position and converts through
        /// the floating-origin manager.
        /// In Kepler-rails mode: propagates orbital elements from epoch tick to current
        /// tick via <see cref="KeplerPropagator.PropagateState"/> and adds the reference
        /// body's world position. The propagator handles dt == 0 as a short-circuit, so
        /// querying immediately after <see cref="TransitionToKeplerRails"/> returns the
        /// entry position exactly. Falls back to epoch tick (dt == 0) if no
        /// <see cref="SimTickController"/> instance exists (EditMode tests).
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
                    long queryTick = SimTickController.Instance != null
                        ? SimTickController.Instance.TickNumber
                        : State.KeplerState.EpochTick;
                    (double3 relPosition, _) = KeplerPropagator.PropagateState(
                        State.KeplerState, queryTick, _referenceBody.Mu, SimTickController.SimTickIntervalSeconds);
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

        // ----- Mode transition trigger evaluation (commit 043) -----

        /// <summary>
        /// Threshold for atmospheric density below which atmospheric drag is considered
        /// insignificant (kg/m³). §3.1's "atmospheric_density &lt; threshold" condition.
        /// The value 1e-6 corresponds to roughly 100 km altitude on Earth — well above
        /// the Karman line where orbital trajectories become well-defined. The vessel's
        /// rigidbody could in principle still be subject to drag at densities slightly
        /// below this but the operational regime where Kepler-rails is appropriate begins
        /// here.
        /// </summary>
        private const double AtmosphericDensityThreshold = 1e-6;

        /// <summary>
        /// Proximity threshold for §3.1 transition triggers, in meters. 50 km matches
        /// the floating-origin shift threshold from <c>commit 002 / 029</c> and the
        /// netcode contract §3.1. The two thresholds are intentionally aligned: a vessel
        /// that moves beyond 50 km of the active vessel is the same vessel that the
        /// floating-origin system would no longer track precisely.
        /// </summary>
        private const double ProximityThresholdMeters = 50_000.0;

        /// <summary>
        /// Evaluate the §3.1 mode transition trigger conditions for this vessel and
        /// return a suggestion to transition (or stay). Pure read-only — does not
        /// invoke any transition; that's the driver's job.
        ///
        /// <para>
        /// <see cref="PhysicsMode.PhysXActive"/> mode evaluates the 5-condition AND
        /// conjunction (proximity, thrust, atmosphere, contact, well-defined-trajectory).
        /// If ALL pass, suggests <see cref="PhysicsMode.KeplerRails"/> with reason
        /// <see cref="TransitionTriggerReason.BeyondProximityWithCleanState"/>.
        /// </para>
        ///
        /// <para>
        /// <see cref="PhysicsMode.KeplerRails"/> mode evaluates the 5-condition OR
        /// disjunction in declared order (first match wins): proximity, predicted
        /// mode transition (atmospheric entry / surface impact / future scheduled-burn
        /// / future interstellar-arrival — aggregated into
        /// <see cref="KeplerState.NextModeTransitionTick"/> via the event-prediction
        /// driver), player focus, scripted thrust, multi-vessel cluster. If any
        /// fires, suggests <see cref="PhysicsMode.PhysXActive"/> with the matching
        /// reason. As of commit 047, the trigger reason
        /// <see cref="TransitionTriggerReason.AtmosphericEntryPredicted"/> fires for
        /// any populated mode-transition tick — atmospheric entry OR surface impact
        /// — because the underlying field is N-way aggregated; the trigger reason
        /// label keeps its historical name and rename is deferred to a separate
        /// cleanup commit.
        /// </para>
        ///
        /// <para>
        /// <see cref="PhysicsMode.InterstellarCruise"/> mode and pre-Initialize state
        /// both return Stay — interstellar transitions are Phase 6, and evaluating
        /// before Initialize would dereference null State. Defensive null check on
        /// <paramref name="activeVesselForProximity"/> also returns Stay (no proximity
        /// reference → cannot fire proximity-dependent conditions; safest to stay).
        /// </para>
        ///
        /// PHASE 0 / PHASE 1 NOTE: many condition implementations are stubs whose
        /// behavior always passes (no thrust, no atmospheric drag from PhysX-state,
        /// no contact, no focus, no clustering). Proximity has a real implementation
        /// since Phase 0. As of commit 047, mode-transition prediction
        /// (<c>IsAtmosphericEntryPredicted</c>) also has a real implementation —
        /// <see cref="KeplerState.NextModeTransitionTick"/> is populated by
        /// <see cref="VesselEventPredictionDriver"/>. See each <c>Has*</c> /
        /// <c>Is*</c> helper for its individual stub status.
        /// </summary>
        /// <param name="activeVesselForProximity">
        /// The active vessel whose position serves as the proximity reference point.
        /// Typically <c>SimTickController.Instance.ActiveVessel</c>. Null returns Stay.
        /// </param>
        public TransitionEvaluation EvaluateTransitionTriggers(IActiveVessel activeVesselForProximity)
        {
            if (!_initialized || State == null)
            {
                return TransitionEvaluation.Stay();
            }
            if (activeVesselForProximity == null)
            {
                return TransitionEvaluation.Stay();
            }

            switch (State.Mode)
            {
                case PhysicsMode.PhysXActive:
                    return EvaluatePhysXActiveTriggers(activeVesselForProximity);

                case PhysicsMode.KeplerRails:
                    return EvaluateKeplerRailsTriggers(activeVesselForProximity);

                case PhysicsMode.InterstellarCruise:
                    // Phase 6 scope — no transitions in/out of cruise mode in Phase 0.
                    return TransitionEvaluation.Stay();

                default:
                    return TransitionEvaluation.Stay();
            }
        }

        /// <summary>
        /// Evaluate the PhysX-active → Kepler-rails 5-condition conjunction. All five
        /// must hold simultaneously.
        /// </summary>
        private TransitionEvaluation EvaluatePhysXActiveTriggers(IActiveVessel activeVesselForProximity)
        {
            bool beyondProximity = IsBeyondProximityThreshold(activeVesselForProximity);
            bool noThrust = HasNoThrust();
            bool noAtmosphere = HasNoSignificantAtmosphericDrag();
            bool noContact = !HasContactForces();
            bool wellDefinedTrajectory = HasWellDefinedTrajectory();

            if (beyondProximity && noThrust && noAtmosphere && noContact && wellDefinedTrajectory)
            {
                return TransitionEvaluation.Transition(
                    PhysicsMode.KeplerRails,
                    TransitionTriggerReason.BeyondProximityWithCleanState);
            }
            return TransitionEvaluation.Stay();
        }

        /// <summary>
        /// Evaluate the Kepler-rails → PhysX-active 5-condition disjunction in declared
        /// order. First match wins; remaining conditions are not evaluated.
        /// </summary>
        private TransitionEvaluation EvaluateKeplerRailsTriggers(IActiveVessel activeVesselForProximity)
        {
            // §3.1 trigger 1: within 50 km of any active vessel.
            if (IsWithinProximityThreshold(activeVesselForProximity))
            {
                return TransitionEvaluation.Transition(
                    PhysicsMode.PhysXActive,
                    TransitionTriggerReason.ProximityToActiveVessel);
            }

            // §3.1 trigger 2: predicted atmospheric entry within next sim-tick.
            if (IsAtmosphericEntryPredicted())
            {
                return TransitionEvaluation.Transition(
                    PhysicsMode.PhysXActive,
                    TransitionTriggerReason.AtmosphericEntryPredicted);
            }

            // §3.1 trigger 3: player focus switch.
            if (HasPlayerFocusSwitch())
            {
                return TransitionEvaluation.Transition(
                    PhysicsMode.PhysXActive,
                    TransitionTriggerReason.PlayerFocusSwitch);
            }

            // §3.1 trigger 4: scripted mode change (Vizzy).
            if (HasScriptedThrust())
            {
                return TransitionEvaluation.Transition(
                    PhysicsMode.PhysXActive,
                    TransitionTriggerReason.ScriptedThrust);
            }

            // §3.1 trigger 5: multi-vessel proximity cluster.
            if (HasMultiVesselProximityCluster())
            {
                return TransitionEvaluation.Transition(
                    PhysicsMode.PhysXActive,
                    TransitionTriggerReason.MultiVesselProximityCluster);
            }

            return TransitionEvaluation.Stay();
        }

        // ----- §3.1 condition helpers (one per condition; PHASE 0 NOTE per stub) -----

        /// <summary>
        /// §3.1 condition: distance from this vessel to <paramref name="activeVessel"/>
        /// is greater than <see cref="ProximityThresholdMeters"/>.
        ///
        /// Real implementation: computes Euclidean distance in double-precision world
        /// coordinates. Works correctly for both PhysX-active mode (rigidbody position)
        /// and Kepler-rails mode (propagated orbital position) because the
        /// <see cref="GetWorldPosition"/> method handles both.
        /// </summary>
        private bool IsBeyondProximityThreshold(IActiveVessel activeVessel)
        {
            return DistanceTo(activeVessel) > ProximityThresholdMeters;
        }

        /// <summary>
        /// §3.1 condition (inverse): distance from this vessel to
        /// <paramref name="activeVessel"/> is less than
        /// <see cref="ProximityThresholdMeters"/>.
        ///
        /// Used by the Kepler-rails → PhysX-active disjunction. Note the asymmetry: the
        /// PhysX-active → Kepler-rails conjunction uses strict greater-than (the vessel
        /// must be clearly outside the threshold), while the Kepler-rails →
        /// PhysX-active disjunction uses strict less-than (the vessel must be clearly
        /// inside). The strict inequality on both sides means a vessel sitting exactly
        /// at 50 km will not transition in either direction — appropriate for an
        /// hysteresis-free threshold.
        /// </summary>
        private bool IsWithinProximityThreshold(IActiveVessel activeVessel)
        {
            return DistanceTo(activeVessel) < ProximityThresholdMeters;
        }

        /// <summary>
        /// Euclidean distance from this vessel's world position to
        /// <paramref name="activeVessel"/>'s world position, in meters. Pure
        /// double-precision math; no float conversions in the result path.
        /// </summary>
        private double DistanceTo(IActiveVessel activeVessel)
        {
            WorldPosition myPos = GetWorldPosition();
            WorldPosition otherPos = activeVessel.GetWorldPosition();
            double dx = myPos.Value.x - otherPos.Value.x;
            double dy = myPos.Value.y - otherPos.Value.y;
            double dz = myPos.Value.z - otherPos.Value.z;
            return math.sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// §3.1 condition: no thrust is being applied.
        ///
        /// Reads <see cref="PhysXState.ActiveThrustN"/>. The field exists in the schema
        /// (per netcode contract §2.2) but no system writes to it yet — engine
        /// simulation is Phase 3+ (flight integration). Phase 0 vessels always have
        /// <c>ActiveThrustN == 0.0</c>, so this check always passes; the wiring is
        /// correct for when engines actually fire in a later phase.
        ///
        /// PHASE 0 NOTE: §3.1 condition for thrust is wired to the schema field but
        /// the field is always stub-zero in Phase 0. When engine simulation lands
        /// (Phase 3), this method's behavior changes automatically without
        /// re-touching the evaluator code.
        /// </summary>
        private bool HasNoThrust()
        {
            if (State.PhysXState == null) return true;  // No PhysX state → no thrust by construction.
            return State.PhysXState.ActiveThrustN == 0.0;
        }

        /// <summary>
        /// §3.1 condition: no atmospheric drag is significant (altitude above
        /// atmospheric boundary OR atmospheric_density &lt; threshold).
        ///
        /// Reads <see cref="PhysXState.AtmosphericDensity"/>. Same Phase 0 stub-state
        /// pattern as <see cref="HasNoThrust"/>: the field exists, no system writes
        /// to it yet (atmospheric flight model is Phase 5), so Phase 0 vessels always
        /// have density 0 and this check always passes.
        ///
        /// PHASE 0 NOTE: §3.1 condition for atmospheric drag is wired to the schema
        /// field; atmospheric model that populates the field is Phase 5.
        /// </summary>
        private bool HasNoSignificantAtmosphericDrag()
        {
            if (State.PhysXState == null) return true;  // No PhysX state → no atmosphere by construction.
            return State.PhysXState.AtmosphericDensity < AtmosphericDensityThreshold;
        }

        /// <summary>
        /// §3.1 condition: contact forces are active (landed, docked to PhysX-active vessel).
        ///
        /// PHASE 0 NOTE: NO contact-force detection system exists. This stub always
        /// returns false (no contact). Always-false was chosen over an IsSleeping
        /// proxy because the proxy would produce false-positives — a vessel sitting
        /// on a planet with rigidbody-asleep would incorrectly report no-contact and
        /// become eligible for Kepler-rails transition while physically touching a
        /// body. Always-false simply means contact-force-checking is a no-op in Phase
        /// 0; the conjunction relies on the proximity check to keep landed vessels
        /// from transitioning (a landed vessel is by definition close to its body,
        /// which means close to the active vessel if the active vessel is also at
        /// that body). When real contact detection lands (Phase 3+), this method
        /// gets a real body.
        /// </summary>
        private bool HasContactForces()
        {
            // PHASE 0 STUB: always-false. Real contact detection is Phase 3+.
            return false;
        }

        /// <summary>
        /// §3.1 condition: the vessel's trajectory is well-defined by patched conics
        /// around a single dominant body.
        ///
        /// Phase 0 invariant: every vessel has a single non-null <see cref="ReferenceBody"/>
        /// assigned at Initialize. Patched-conics multi-body handling (SOI transitions,
        /// Lagrange-point regions) is Phase 1+ work; Phase 0 vessels always have a
        /// well-defined trajectory by virtue of having one and only one dominant body.
        /// </summary>
        private bool HasWellDefinedTrajectory()
        {
            return _referenceBody != null;
        }

        /// <summary>
        /// §3.1 trigger: predicted imminent mode transition within the next sim-tick.
        /// Reads <see cref="KeplerState.NextModeTransitionTick"/>; if set and within
        /// one tick of the current sim-tick, a mode transition is imminent.
        ///
        /// As of commit 047, <see cref="KeplerState.NextModeTransitionTick"/> is
        /// populated by <see cref="VesselEventPredictionDriver"/> as the earliest of
        /// atmospheric-entry and surface-impact predictions. This method name
        /// retains the "AtmosphericEntry" framing for historical / API-stability
        /// reasons, but the underlying predicate fires for any populated
        /// mode-transition tick — including surface impact on a vacuum body. The
        /// label imprecision (and the matching
        /// <see cref="TransitionTriggerReason.AtmosphericEntryPredicted"/> enum
        /// value) is a known cosmetic concern deferred to a separate cleanup commit.
        /// </summary>
        private bool IsAtmosphericEntryPredicted()
        {
            if (State.KeplerState == null) return false;
            long? predicted = State.KeplerState.NextModeTransitionTick;
            if (!predicted.HasValue) return false;

            long currentTick = SimTickController.Instance != null
                ? SimTickController.Instance.TickNumber
                : State.KeplerState.EpochTick;
            return predicted.Value <= currentTick + 1;
        }

        /// <summary>
        /// §3.1 trigger: player switched focus to this vessel.
        ///
        /// PHASE 0 NOTE: no player-focus subsystem exists. The TestVesselDriver's
        /// Space-key handler invokes transitions directly via the imperative API,
        /// not via this evaluator path. Real focus-tracking is Phase 5+ (camera /
        /// input routing tied to the Mission Control UI).
        /// </summary>
        private bool HasPlayerFocusSwitch()
        {
            // PHASE 0 STUB: no focus subsystem. Always-false.
            return false;
        }

        /// <summary>
        /// §3.1 trigger: scripted mode change (Vizzy script triggering thrust, etc.).
        ///
        /// PHASE 0 NOTE: Vizzy ships in Phase 5. No scripting subsystem exists in
        /// Phase 0; scripted thrust is unreachable.
        /// </summary>
        private bool HasScriptedThrust()
        {
            // PHASE 0 STUB: no scripting subsystem. Always-false.
            return false;
        }

        /// <summary>
        /// §3.1 trigger: multi-vessel proximity events (multiple Kepler-rails vessels
        /// in a 50 km cluster).
        ///
        /// PHASE 0 NOTE: no multi-vessel proximity-clustering logic exists. Phase 0
        /// scenes have one vessel; the cluster trigger has no operational meaning.
        /// When multi-vessel simulation lands (Phase 5+), this method iterates
        /// <see cref="VesselRegistry.Vessels"/> for nearby rails-mode peers.
        /// </summary>
        private bool HasMultiVesselProximityCluster()
        {
            // PHASE 0 STUB: no multi-vessel logic. Always-false.
            return false;
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
