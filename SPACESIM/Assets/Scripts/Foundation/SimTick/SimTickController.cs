using System;
using System.Collections.Generic;
using SpaceSim.Foundation.Coordinates;
using UnityEngine;

namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Singleton MonoBehaviour that owns the 30 Hz fixed-timestep sim-tick loop per
    /// <c>docs/NETCODE_CONTRACT.md</c> §1.2 (the sim-tick boundary) and §1.4 (time-warp
    /// mechanics).
    ///
    /// The controller drives the 10-step cycle once per Unity FixedUpdate. PhysX-touching
    /// steps (1, 2, 3, 7, 8, 9) execute once per FixedUpdate; analytic-propagation steps
    /// (4, 5, 10) execute N times per FixedUpdate where N is the warp-effective iteration
    /// count computed by <see cref="SimTickWarpController"/>. Step 6 (mode-transition
    /// detection / floating-origin shift) runs once per FixedUpdate inside the iteration
    /// loop (gated by <c>if (i == 0)</c>) because the active-vessel position doesn't change
    /// within a single FixedUpdate at warp.
    ///
    /// Commit 033 (this commit) ships the cycle spine and timing. The individual step
    /// methods are stubs except step 6, which wires through to
    /// <see cref="FloatingOriginManager.MaybeShiftOrigin"/>. Future commits flesh out the
    /// remaining steps as their collaborators (vessels, peers, event queue, Kepler-rails
    /// propagator) are built.
    ///
    /// Singleton pattern matches <see cref="FloatingOriginManager"/>:
    ///   <list type="bullet">
    ///     <item><see cref="Instance"/> is claimed in <see cref="Awake"/>.</item>
    ///     <item>Duplicate instances log an error and destroy the duplicate component.</item>
    ///     <item><see cref="OnDestroy"/> clears <see cref="Instance"/> when the original is destroyed.</item>
    ///     <item><see cref="ClearInstanceForTesting"/> is the test-API mutation path.</item>
    ///   </list>
    ///
    /// PHASE 0 PROTOTYPE NOTE: a separate FloatingOriginManager is no longer the sole driver
    /// of <see cref="FloatingOriginManager.MaybeShiftOrigin"/>. This controller is. As of
    /// commit 033, the floating-origin shift dispatches synchronously from step 6 of the
    /// sim-tick cycle at the FixedUpdate cadence. See
    /// <c>SpaceSim.Foundation.Coordinates.FloatingOriginAnchor</c>'s class-level
    /// "SHIFT DISPATCH FROM SIM-TICK BOUNDARY" doc block for the architectural payoff.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimTickController : MonoBehaviour
    {
        // ----- Singleton -----

        public static SimTickController Instance { get; private set; }

        // ----- Constants -----

        /// <summary>Sim-tick rate in ticks per second. Fixed at 30 Hz per the contract §1.2.</summary>
        public const float SimTickRate = 30f;

        /// <summary>Sim-tick interval in seconds. <c>1f / SimTickRate</c> ≈ 33.333 ms.</summary>
        public const float SimTickIntervalSeconds = 1f / SimTickRate;

        // ----- State -----

        /// <summary>Total sim-ticks advanced since startup. Increments in step 10 of each cycle iteration.</summary>
        public long TickNumber { get; private set; }

        /// <summary>The phase the controller is currently executing. <see cref="SimTickPhase.Idle"/> between FixedUpdate calls.</summary>
        public SimTickPhase CurrentPhase { get; private set; } = SimTickPhase.Idle;

        /// <summary>The warp controller. Owns warp-rate state and per-mode ceilings.</summary>
        public SimTickWarpController Warp { get; } = new SimTickWarpController();

        /// <summary>Total FixedUpdate cycles processed since startup. Distinct from <see cref="TickNumber"/>: 1 FixedUpdate may advance N ticks at warp.</summary>
        public long FixedUpdateCount { get; private set; }

        // ----- Step 6 (mode transitions / floating-origin shift) state -----

        /// <summary>
        /// The active vessel — the one whose position drives floating-origin shift
        /// detection and whose mode drives warp-ceiling selection. Step 6 reads
        /// <c>ActiveVessel.GetWorldPosition()</c> and <c>ActiveVessel.Mode</c> on every
        /// FixedUpdate.
        ///
        /// Null is valid and means "no active vessel" — step 6 logs a warning once per
        /// controller lifetime and skips shift detection. Tests set this via
        /// <see cref="SetActiveVessel"/> with either a real <c>Vessel</c> or an
        /// <see cref="IActiveVessel"/> POCO test double.
        /// </summary>
        public IActiveVessel ActiveVessel { get; private set; }

        private bool _warnedAboutMissingOriginManager;
        private bool _warnedAboutMissingActiveVessel;

        // ----- Listeners -----

        private readonly List<ISimTickListener> _listeners = new List<ISimTickListener>();

        /// <summary>
        /// Event raised at the end of step 10 each iteration. Subscribers receive the new
        /// tick number after the advancement.
        ///
        /// Prefer the <see cref="ISimTickListener"/> interface and
        /// <see cref="RegisterListener"/> for performance-critical or
        /// frequently-iterated listeners: interface dispatch avoids per-tick delegate
        /// allocation.
        /// </summary>
        public event Action<long> TickAdvanced;

        // ----- Lifecycle -----

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"Duplicate SimTickController in scene. Existing on '{Instance.gameObject.name}', " +
                    $"duplicate on '{gameObject.name}'. Destroying the duplicate.");
                Destroy(this);
                return;
            }
            Instance = this;

            // Set the fixed-timestep rate to match the contract's 30 Hz commitment. PhysX
            // and the sim-tick share the same timing source for the prototype; this is
            // documented as acceptable in the commit 033 artifact.
            Time.fixedDeltaTime = SimTickIntervalSeconds;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ----- Listener registration -----

        /// <summary>Register an interface-based sim-tick listener. Duplicate registrations are dedup'd.</summary>
        public void RegisterListener(ISimTickListener listener)
        {
            if (listener == null) return;
            if (!_listeners.Contains(listener)) _listeners.Add(listener);
        }

        /// <summary>Unregister a listener. Safe to call with an unregistered listener.</summary>
        public void UnregisterListener(ISimTickListener listener)
        {
            if (listener == null) return;
            _listeners.Remove(listener);
        }

        /// <summary>Number of currently-registered interface listeners. Exposed for diagnostics and tests.</summary>
        public int ListenerCount => _listeners.Count;

        // ----- Active-vessel registration -----

        /// <summary>
        /// Set the active vessel — the one driving step 6's floating-origin shift check
        /// and the warp-controller's mode tracking.
        ///
        /// Accepts null, which means "no active vessel" — step 6 will warn-once and skip
        /// shift detection until a non-null vessel is assigned. When called with null,
        /// the warp controller's active-vessel mode resets to
        /// <see cref="PhysicsMode.PhysXActive"/> (the most-restrictive default, matching
        /// <see cref="SimTickWarpController"/>'s construction state).
        ///
        /// When called with a non-null vessel, the warp controller's mode is updated to
        /// match the vessel's <see cref="IActiveVessel.Mode"/>. Step 6 also refreshes the
        /// warp mode on every FixedUpdate (cheap idempotent assignment), so transitions
        /// during play propagate without an explicit SetActiveVessel re-call.
        ///
        /// This method replaces the commit-033 <c>SetActiveVesselWorldPosition</c>
        /// prototype-bridge API. The new design takes a vessel reference rather than a
        /// raw position because the controller now needs both position and mode, and
        /// vessel containers (per commit 038) own both pieces of state.
        ///
        /// The parameter type is <see cref="IActiveVessel"/>, not the concrete
        /// <c>Vessel</c> class, to avoid a circular asmdef dependency
        /// (Vessels → SimTick is the canonical direction; SimTick → Vessels would
        /// close the cycle). The interface contract is narrow on purpose; see
        /// <see cref="IActiveVessel"/> for the design rationale.
        /// </summary>
        public void SetActiveVessel(IActiveVessel vessel)
        {
            ActiveVessel = vessel;
            if (vessel == null)
            {
                Warp.SetActiveVesselMode(PhysicsMode.PhysXActive);
            }
            else
            {
                Warp.SetActiveVesselMode(vessel.Mode);
            }
        }

        // ----- Test API -----

        /// <summary>Reset the singleton's static reference. Used by tests to ensure clean state between runs.</summary>
        public static void ClearInstanceForTesting()
        {
            Instance = null;
        }

        /// <summary>
        /// TEST-ONLY. Claim the singleton slot with the provided controller without
        /// going through <see cref="Awake"/>.
        ///
        /// In EditMode, <see cref="Awake"/> does not fire on <c>AddComponent</c>, so
        /// <see cref="Instance"/> is never claimed automatically. Tests that need
        /// <see cref="Instance"/> to point at a specific controller (e.g.,
        /// propagator-integration tests that need <see cref="TickNumber"/> visible
        /// to <c>Vessel.GetWorldPosition</c>) call this directly.
        ///
        /// Production code MUST NOT call this. The intended path is Awake-driven
        /// claim plus duplicate-detection logic; this setter skips both. Pair every
        /// call with a matching <see cref="ClearInstanceForTesting"/> in TearDown to
        /// avoid bleeding state across test cases.
        /// </summary>
        public static void SetInstanceForTesting(SimTickController controller)
        {
            Instance = controller;
        }

        /// <summary>
        /// TEST-ONLY. Directly set <see cref="TickNumber"/> without running the cycle.
        ///
        /// Used by EditMode tests that need to advance the sim clock without the
        /// side effects of <see cref="RunFixedUpdateCycle"/> (listener callbacks,
        /// floating-origin shifts, PhysX state reads). The propagator-integration
        /// tests are the canonical caller: they need a vessel to observe a non-zero
        /// elapsed-tick delta when calling <see cref="IActiveVessel.GetWorldPosition"/>
        /// or transitioning back to PhysX-active, and the cleanest path is a direct
        /// tick-number write rather than driving the cycle in EditMode (where Time
        /// is paused and the cycle's auxiliary state machinery isn't being exercised).
        ///
        /// Production code MUST NOT call this. <see cref="TickNumber"/> is otherwise
        /// advanced only by <see cref="Step10_AdvanceCounter"/>, which is invoked
        /// exclusively from <see cref="RunFixedUpdateCycle"/>. A test-only setter
        /// is the sibling of <see cref="ClearInstanceForTesting"/>: both exist to
        /// let tests reach internal state that the production lifecycle owns.
        /// </summary>
        public void SetTickNumberForTesting(long tick)
        {
            TickNumber = tick;
        }

        // ----- Main cycle driver -----

        private void FixedUpdate()
        {
            // Empty event queue (commit 033 scope: no event queue exists yet).
            // ticksUntilNextEvent = int.MaxValue means warp is capped by mode ceiling only.
            int analyticIterations = Warp.ComputeAnalyticIterations(int.MaxValue);
            RunFixedUpdateCycle(analyticIterations);
            FixedUpdateCount++;
        }

        /// <summary>
        /// Run one FixedUpdate's worth of the 10-step cycle. Public for testability: tests
        /// invoke this directly with a controlled iteration count to verify cycle behavior
        /// without running the FixedUpdate loop.
        ///
        /// PhysX-touching steps (1, 2, 3, 7, 8, 9) execute once per FixedUpdate regardless
        /// of warp. Analytic-propagation steps (4, 5, 10) execute <paramref name="analyticIterations"/>
        /// times. Step 6 executes once per FixedUpdate (gated by <c>i == 0</c>); see the
        /// class-level doc for rationale.
        /// </summary>
        /// <param name="analyticIterations">Iteration count for analytic steps. Clamped to ≥ 1.</param>
        public void RunFixedUpdateCycle(int analyticIterations)
        {
            if (analyticIterations < 1) analyticIterations = 1;

            // PhysX-touching steps run ONCE per FixedUpdate regardless of warp.
            CurrentPhase = SimTickPhase.ReceivePeerState;     Step1_ReceivePeerState();
            CurrentPhase = SimTickPhase.ReadPhysX;            Step2_ReadPhysX();
            CurrentPhase = SimTickPhase.ConvertToAuthoritative; Step3_ConvertToAuthoritative();

            // Analytic-propagation iteration loop.
            for (int i = 0; i < analyticIterations; i++)
            {
                CurrentPhase = SimTickPhase.ApplyAnalyticUpdates;    Step4_ApplyAnalyticUpdates();
                CurrentPhase = SimTickPhase.ReconcileAuthoritative;  Step5_ReconcileAuthoritative();

                // Step 6 runs ONCE per FixedUpdate (Flag 4 resolution). Active-vessel
                // position doesn't change within a single FixedUpdate at warp because the
                // active vessel is PhysX-active and PhysX-active ceiling is 1×; analytic
                // steps don't move the active vessel.
                if (i == 0)
                {
                    CurrentPhase = SimTickPhase.DetectModeTransitions;
                    Step6_DetectModeTransitions();
                }
            }

            // PhysX-touching push back: ONCE per FixedUpdate.
            CurrentPhase = SimTickPhase.PushAuthoritativeToPhysX; Step7_PushAuthoritativeToPhysX();

            // Replication: ONCE per FixedUpdate (single-player no-op for v1).
            CurrentPhase = SimTickPhase.ReplicateToPeers; Step8_ReplicateToPeers();

            // Events: placeholder at FixedUpdate cadence for commit 033 (empty queue, no-op).
            // When the event queue exists, this will move inside the iteration loop and fire
            // events whose scheduled tick has been reached.
            CurrentPhase = SimTickPhase.FireEvents; Step9_FireEvents();

            // Tick counter: advances by analyticIterations per FixedUpdate.
            CurrentPhase = SimTickPhase.AdvanceCounter;
            for (int i = 0; i < analyticIterations; i++)
            {
                Step10_AdvanceCounter();
            }

            CurrentPhase = SimTickPhase.Idle;
        }

        // ----- The 10 step methods -----
        //
        // Each step corresponds to a numbered step in docs/NETCODE_CONTRACT.md §1.2.
        // Commit 033 ships the cycle spine and timing; most steps are STUBS that will be
        // fleshed out in future commits as their collaborators exist. Step 6 is wired to
        // FloatingOriginManager.MaybeShiftOrigin and is functional now.
        //
        // The stubs are intentionally empty rather than throwing NotImplementedException:
        // the cycle must run end-to-end during the prototype (the test scene's origin shift
        // depends on the cycle completing each FixedUpdate). Throwing would break the prototype.

        /// <summary>
        /// Step 1: receive peer state.
        /// STUB (commit 033). Single-player no-op. Fleshed out when multiplayer lands (post-v1).
        /// </summary>
        private void Step1_ReceivePeerState() { /* TODO: peer state ingestion (post-v1 multiplayer) */ }

        /// <summary>
        /// Step 2: read PhysX state for vessels with local PhysX-active authority.
        /// STUB (commit 033). Fleshed out when vessel containers exist (commit 035+).
        /// </summary>
        private void Step2_ReadPhysX() { /* TODO: per-vessel PhysX rigidbody state read (commit 035+) */ }

        /// <summary>
        /// Step 3: convert PhysX local coords to authoritative double-precision world coords.
        /// STUB (commit 033). Uses <see cref="CoordinateMath.LocalToWorld"/> per-vessel once
        /// vessel containers exist (commit 035+).
        /// </summary>
        private void Step3_ConvertToAuthoritative() { /* TODO: per-vessel local-to-world conversion (commit 035+) */ }

        /// <summary>
        /// Step 4: apply analytic updates (Kepler-rails propagation, interstellar-cruise
        /// with relativistic time-dilation, fuel/life-support consumption).
        /// STUB (commit 033). As of commit 040, the Kepler-rails propagator
        /// (<see cref="SpaceSim.Foundation.Vessels.KeplerPropagator"/>) is wired in but
        /// invoked on-demand by <c>Vessel.GetWorldPosition</c> and
        /// <c>Vessel.TransitionToPhysXActive</c>, not from step 4. Step 4 gains work
        /// when the event queue and multi-vessel state-update needs land (future commit).
        /// </summary>
        private void Step4_ApplyAnalyticUpdates() { /* TODO: analytic propagation driver — event queue + multi-vessel updates (future commit). Per-vessel Kepler-rails position queries already work via on-demand propagator. */ }

        /// <summary>
        /// Step 5: reconcile PhysX-derived updates and analytic updates into the new
        /// authoritative state.
        /// STUB (commit 033). Fleshed out alongside step 2 and step 4 (commit 036+).
        /// </summary>
        private void Step5_ReconcileAuthoritative() { /* TODO: reconciliation logic (commit 036+) */ }

        /// <summary>
        /// Step 6: detect mode transitions for each vessel. In commit 038, two
        /// responsibilities:
        ///   1. Dispatch floating-origin shift check via
        ///      <see cref="FloatingOriginManager.MaybeShiftOrigin"/> using
        ///      <see cref="ActiveVessel"/>.<see cref="IActiveVessel.GetWorldPosition"/>.
        ///   2. Keep the warp controller's mode in sync with the active vessel's mode
        ///      by calling <see cref="SimTickWarpController.SetActiveVesselMode"/> on
        ///      every FixedUpdate. This is a cheap idempotent assignment that picks up
        ///      mode transitions during play (PhysX-active ↔ Kepler-rails) without
        ///      requiring an explicit <see cref="SetActiveVessel"/> re-call from the
        ///      vessel each time it transitions.
        ///
        /// Two skip conditions, each producing a one-time warning per controller
        /// lifetime:
        ///   - <see cref="FloatingOriginManager.Instance"/> is null
        ///   - <see cref="ActiveVessel"/> is null
        ///
        /// Each condition's warning fires once and is suppressed thereafter; the cycle
        /// continues to run, just without origin-shift detection.
        /// </summary>
        private void Step6_DetectModeTransitions()
        {
            if (FloatingOriginManager.Instance == null)
            {
                if (!_warnedAboutMissingOriginManager)
                {
                    Debug.LogWarning(
                        "SimTickController.Step6: FloatingOriginManager.Instance is null. " +
                        "Origin-shift detection skipped. This warning logs once per controller lifetime; " +
                        "ensure a FloatingOriginManager GameObject is present in the scene if origin shifts are needed.");
                    _warnedAboutMissingOriginManager = true;
                }
                return;
            }

            if (ActiveVessel == null)
            {
                if (!_warnedAboutMissingActiveVessel)
                {
                    Debug.LogWarning(
                        "SimTickController.Step6: ActiveVessel is null. " +
                        "Origin-shift detection skipped. This warning logs once per controller lifetime; " +
                        "call SetActiveVessel(vessel) to assign an active vessel before expecting origin shifts.");
                    _warnedAboutMissingActiveVessel = true;
                }
                return;
            }

            // Keep warp mode in sync with active vessel's mode every tick. Idempotent;
            // assignment is cheap. Picks up mode transitions during play without
            // requiring an explicit SetActiveVessel re-call from the vessel.
            Warp.SetActiveVesselMode(ActiveVessel.Mode);

            FloatingOriginManager.Instance.MaybeShiftOrigin(ActiveVessel.GetWorldPosition());
        }

        /// <summary>
        /// Step 7: push authoritative state back to PhysX rigidbodies.
        /// STUB (commit 033). Per-vessel write once vessel containers exist (commit 035+).
        /// </summary>
        private void Step7_PushAuthoritativeToPhysX() { /* TODO: per-vessel authoritative-to-PhysX write (commit 035+) */ }

        /// <summary>
        /// Step 8: replicate authoritative state deltas to peers.
        /// STUB (commit 033). Single-player no-op. Fleshed out when multiplayer lands (post-v1).
        /// </summary>
        private void Step8_ReplicateToPeers() { /* TODO: replication delta send (post-v1 multiplayer) */ }

        /// <summary>
        /// Step 9: fire analytic events whose scheduled tick has been reached.
        /// STUB (commit 033). Empty event queue; no-op. Fleshed out when the analytic event
        /// queue lands (commit 036+ with Kepler-rails).
        /// </summary>
        private void Step9_FireEvents() { /* TODO: event-queue processing (commit 036+) */ }

        /// <summary>
        /// Step 10: advance the sim-tick counter and notify listeners.
        ///
        /// Increments <see cref="TickNumber"/>, raises the <see cref="TickAdvanced"/> event,
        /// and invokes <see cref="ISimTickListener.OnSimTickAdvanced"/> on each registered
        /// listener. Listener exceptions are caught and logged; a throwing listener does
        /// not block the cycle or other listeners.
        /// </summary>
        private void Step10_AdvanceCounter()
        {
            TickNumber++;

            // Interface listeners first (performance-critical path). Iterate over a copy
            // to tolerate listeners that unregister themselves in their callback.
            var snapshot = _listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].OnSimTickAdvanced(TickNumber);
                }
                catch (Exception e)
                {
                    Debug.LogError($"SimTickController: listener {snapshot[i]} threw during tick advance: {e}");
                }
            }

            // Event subscribers second. Single try/catch around the multicast; a throw
            // here aborts remaining subscribers but the controller state is already advanced.
            try
            {
                TickAdvanced?.Invoke(TickNumber);
            }
            catch (Exception e)
            {
                Debug.LogError($"SimTickController: TickAdvanced event subscriber threw: {e}");
            }
        }
    }
}
