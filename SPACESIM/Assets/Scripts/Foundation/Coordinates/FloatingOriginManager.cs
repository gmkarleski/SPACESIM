using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.Coordinates
{
    /// <summary>
    /// Singleton MonoBehaviour that owns the floating origin and dispatches origin-shift events.
    ///
    /// Per `docs/CONSTRAINTS.md` §2 commit 002 and `docs/NETCODE_CONTRACT.md` §1.1:
    ///   - Shifts happen only at sim-tick boundaries; the sim-tick controller (commit 030)
    ///     drives <see cref="MaybeShiftOrigin"/> once per tick with the active-vessel position.
    ///   - The shift threshold is 50 km default, exposed as a serialized field for tuning.
    ///   - Shifts are atomic from PhysX's perspective: all listeners apply the shift inside
    ///     a single notification pass, with no PhysX state mutation between the old origin
    ///     and the new origin.
    ///   - Shift trigger: <c>distance(active_vessel, current_origin) &gt; threshold</c>
    ///     (strict; see <see cref="CoordinateMath.ShouldShift"/>).
    ///   - New origin: the active-vessel position itself. After the shift, the active vessel
    ///     is at local-position-zero.
    ///
    /// In commit 029, this manager stands alone. Commit 033 wired it into the sim-tick
    /// controller's step 6, which now drives <see cref="MaybeShiftOrigin"/> at the FixedUpdate
    /// cadence.
    ///
    /// The class is a singleton enforced via <see cref="Instance"/>. A second instance in the
    /// scene self-destructs and logs an error. Tests that need to reset the singleton use
    /// the <see cref="ClearInstanceForTesting"/> method.
    ///
    /// DEFERRED LISTENER REGISTRATION (commit 034):
    /// MonoBehaviours that want to receive origin-shift notifications cannot generally rely
    /// on <see cref="Instance"/> being non-null at the moment their own <c>OnEnable</c> or
    /// <c>Awake</c> runs. Unity does not guarantee Awake-order across distinct GameObjects
    /// without explicit Script Execution Order configuration. To avoid the user-side trap of
    /// "your anchor was enabled before the manager existed, so it never received shifts,"
    /// listeners use the static <see cref="RegisterListenerSafe"/> / <see cref="UnregisterListenerSafe"/>
    /// facade. When the manager's <c>Awake</c> later claims <see cref="Instance"/>, any
    /// listeners that registered "early" are drained from the static pending queue into the
    /// active-listener list automatically. This makes the registration path order-independent,
    /// which matters for runtime-spawned vessels in later commits where scene-load ordering
    /// is not available.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingOriginManager : MonoBehaviour
    {
        // ----- Singleton -----

        public static FloatingOriginManager Instance { get; private set; }

        // ----- Inspector-tunable configuration -----

        [SerializeField, Tooltip("Shift threshold in kilometers. Default 50 km per CONSTRAINTS §2 commit 002.")]
        private double shiftThresholdKm = 50.0;

        /// <summary>Shift threshold in meters. Derived from <see cref="shiftThresholdKm"/>.</summary>
        public double ShiftThresholdMeters => shiftThresholdKm * 1000.0;

        /// <summary>Shift threshold in kilometers (read-only public access).</summary>
        public double ShiftThresholdKm => shiftThresholdKm;

        // ----- State -----

        /// <summary>Current floating origin in world coordinates. Updated atomically on shift.</summary>
        public WorldPosition CurrentOrigin { get; private set; } = WorldPosition.Zero;

        /// <summary>Total shift count since startup, exposed for diagnostics and tests.</summary>
        public int ShiftCount { get; private set; }

        // ----- Listeners -----

        private readonly List<IFloatingOriginListener> _listeners = new List<IFloatingOriginListener>();

        /// <summary>
        /// Static queue of listeners that attempted to register via
        /// <see cref="RegisterListenerSafe"/> before <see cref="Instance"/> was available.
        /// Drained into <see cref="_listeners"/> by <see cref="Awake"/> once the singleton
        /// claims itself. Static (not instance) because by definition no instance exists at
        /// the time these are enqueued.
        /// </summary>
        private static readonly List<IFloatingOriginListener> _pendingListeners
            = new List<IFloatingOriginListener>();

        /// <summary>
        /// Event raised after the origin updates and listeners have been notified.
        ///
        /// Subscribers receive the world-space shift delta (new origin minus old origin). To
        /// preserve a world-space position, subtract the delta from the cached local position.
        ///
        /// For performance-critical or frequently-iterated listeners, prefer the
        /// <see cref="IFloatingOriginListener"/> interface and <see cref="RegisterListener"/>:
        /// interface dispatch avoids per-shift delegate allocation.
        /// </summary>
        public event Action<double3> OriginShifted;

        // ----- Lifecycle -----

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"Duplicate FloatingOriginManager in scene. Existing on '{Instance.gameObject.name}', " +
                    $"duplicate on '{gameObject.name}'. Destroying the duplicate.");
                Destroy(this);
                return;
            }
            Instance = this;

            // Drain any listeners that registered "early" via RegisterListenerSafe while
            // Instance was null. The list is processed in registration order (preserves
            // user-visible behavior) and goes through RegisterListener for deduplication.
            // Snapshot via ToArray to tolerate the unlikely case of a listener registering
            // a sibling during its own initialization (which would mutate _pendingListeners
            // mid-iteration).
            DrainPendingListeners();
        }

        /// <summary>
        /// Move all listeners from the static pending queue into the active listener list.
        /// Called by <see cref="Awake"/> immediately after the singleton claims itself, and
        /// also exposed to tests via <see cref="DrainPendingForTesting"/>. The active
        /// instance's <see cref="RegisterListener"/> is used so deduplication semantics apply
        /// uniformly across deferred and direct registration paths.
        /// </summary>
        private void DrainPendingListeners()
        {
            if (_pendingListeners.Count == 0) return;
            var pending = _pendingListeners.ToArray();
            _pendingListeners.Clear();
            for (int i = 0; i < pending.Length; i++)
            {
                RegisterListener(pending[i]);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ----- Listener registration -----

        /// <summary>
        /// Register a listener to receive origin-shift notifications. Duplicate registrations
        /// are deduplicated (the listener is added only once).
        /// </summary>
        public void RegisterListener(IFloatingOriginListener listener)
        {
            if (listener == null) return;
            if (!_listeners.Contains(listener)) _listeners.Add(listener);
        }

        /// <summary>
        /// Unregister a listener. Safe to call with a listener that was never registered.
        /// </summary>
        public void UnregisterListener(IFloatingOriginListener listener)
        {
            if (listener == null) return;
            _listeners.Remove(listener);
        }

        /// <summary>
        /// Number of currently-registered listeners. Exposed for diagnostics and tests.
        /// </summary>
        public int ListenerCount => _listeners.Count;

        /// <summary>
        /// Number of listeners in the static pending queue (registered "early" via
        /// <see cref="RegisterListenerSafe"/> while <see cref="Instance"/> was null).
        /// Exposed for diagnostics and tests. Drained to <see cref="ListenerCount"/> on
        /// <see cref="Awake"/>.
        /// </summary>
        public static int PendingListenerCount => _pendingListeners.Count;

        /// <summary>
        /// Register a listener safely regardless of whether <see cref="Instance"/> exists yet.
        /// If the singleton is available, the listener is added directly via
        /// <see cref="RegisterListener"/>. Otherwise the listener is queued in a static
        /// pending list and will be drained into the active listener list when the manager's
        /// <see cref="Awake"/> claims the singleton.
        ///
        /// This is the canonical registration path for MonoBehaviour listeners that cannot
        /// guarantee they wake up after the manager (the common case). Pass <c>null</c> is
        /// silently ignored.
        /// </summary>
        public static void RegisterListenerSafe(IFloatingOriginListener listener)
        {
            if (listener == null) return;
            if (Instance != null)
            {
                Instance.RegisterListener(listener);
                return;
            }
            // Deduplicate at the queue level too: if the same listener calls Safe twice
            // before Awake fires, it should still drain once.
            if (!_pendingListeners.Contains(listener)) _pendingListeners.Add(listener);
        }

        /// <summary>
        /// Unregister a listener safely regardless of whether it currently lives in the
        /// active list or the pending queue. If <see cref="Instance"/> exists, the listener
        /// is removed from the active list via <see cref="UnregisterListener"/>. The pending
        /// queue is also scanned and the listener removed from there if present (covers the
        /// case where a listener registered via <see cref="RegisterListenerSafe"/> and was
        /// then destroyed before the manager's <see cref="Awake"/> drained it). Both checks
        /// run because a listener can be in only the queue, only the active list, or
        /// neither — but never both simultaneously (drain transfers between them atomically).
        /// </summary>
        public static void UnregisterListenerSafe(IFloatingOriginListener listener)
        {
            if (listener == null) return;
            if (Instance != null)
            {
                Instance.UnregisterListener(listener);
            }
            // Always check pending too; harmless if the listener isn't there.
            _pendingListeners.Remove(listener);
        }

        // ----- Conversion convenience -----

        /// <summary>Convert a world position to local using the current origin.</summary>
        public LocalPosition WorldToLocal(WorldPosition w)
            => CoordinateMath.WorldToLocal(w, CurrentOrigin);

        /// <summary>Convert a local position to world using the current origin.</summary>
        public WorldPosition LocalToWorld(LocalPosition l)
            => CoordinateMath.LocalToWorld(l, CurrentOrigin);

        // ----- Shift logic -----

        /// <summary>
        /// Check whether the given active-vessel world position has exceeded the shift threshold
        /// from the current origin, and if so, perform the shift.
        ///
        /// Sim-tick controller calls this once per tick with the active vessel's position. In
        /// commit 029 (no sim-tick controller yet), test scenes call it from MonoBehaviour
        /// Update with a test position.
        ///
        /// Shift sequence:
        ///   1. Compute shift delta from old origin to new origin (the active-vessel position).
        ///   2. Update <see cref="CurrentOrigin"/> (state reflects new origin BEFORE listeners run).
        ///   3. Increment <see cref="ShiftCount"/>.
        ///   4. Notify all <see cref="IFloatingOriginListener"/>s synchronously.
        ///   5. Raise <see cref="OriginShifted"/> event.
        /// </summary>
        /// <returns>True if a shift occurred this call; false otherwise.</returns>
        public bool MaybeShiftOrigin(WorldPosition activeVesselWorldPos)
        {
            if (!CoordinateMath.ShouldShift(activeVesselWorldPos, CurrentOrigin, ShiftThresholdMeters))
                return false;

            double3 shiftDelta = CoordinateMath.ComputeShiftDelta(CurrentOrigin, activeVesselWorldPos);

            // Step 2: update state BEFORE notifying. Listeners may want to call WorldToLocal()
            // mid-handler and need the new origin to compute correct local coords.
            CurrentOrigin = activeVesselWorldPos;
            ShiftCount++;

            // Step 4: synchronous notification of interface listeners. Iterate over a copy to
            // tolerate listeners that unregister themselves in their callback (rare but legal).
            // Snapshot via ToArray() rather than a `for` loop to avoid in-place mutation hazards.
            var snapshot = _listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].OnFloatingOriginShifted(shiftDelta);
                }
                catch (Exception e)
                {
                    Debug.LogError($"FloatingOriginManager: listener {snapshot[i]} threw during shift: {e}");
                    // Continue notifying remaining listeners; a single buggy listener should not
                    // leave the simulation half-shifted.
                }
            }

            // Step 5: raise event. Event-only subscribers see the delta after listeners are done.
            OriginShifted?.Invoke(shiftDelta);

            return true;
        }

        // ----- Test support -----

        /// <summary>
        /// Reset the singleton's static reference AND clear the static pending-listeners
        /// queue. Used by tests to ensure a clean state between test runs. Not part of the
        /// production API.
        ///
        /// Both pieces of static state are cleared because the pending queue is part of the
        /// manager's externally-observable state: a test that wants a "fresh manager" expects
        /// no leftover queued listeners from a prior test. Tests that specifically exercise
        /// the deferred-registration path call this first, then explicitly enqueue listeners
        /// via <see cref="RegisterListenerSafe"/> within the test body.
        /// </summary>
        public static void ClearInstanceForTesting()
        {
            Instance = null;
            _pendingListeners.Clear();
        }

        /// <summary>
        /// Simulate the pending-queue drain that <see cref="Awake"/> performs, without
        /// requiring PlayMode. EditMode tests can construct a manager via
        /// <c>AddComponent&lt;FloatingOriginManager&gt;</c> (which does NOT fire Awake), set
        /// the singleton via reflection or via <see cref="ClearInstanceForTesting"/>-then-
        /// register-this-as-Instance, then call this method to exercise the drain logic.
        ///
        /// Deliberately narrow in scope: this method drains the pending queue and does
        /// nothing else. Do NOT extend it to cover other future Awake responsibilities — if
        /// Awake grows to do more, those responsibilities get their own test-only hooks. The
        /// specific name is the deterrent against misuse as a general "fake Awake" method.
        /// </summary>
        internal static void DrainPendingForTesting(FloatingOriginManager instance)
        {
            if (instance == null) return;
            instance.DrainPendingListeners();
        }
    }
}
