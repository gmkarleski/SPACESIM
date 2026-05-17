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
    /// In commit 029, this manager stands alone. Commit 030 will wire it into the sim-tick
    /// controller; until then, test scenes drive <see cref="MaybeShiftOrigin"/> directly from
    /// MonoBehaviour Update.
    ///
    /// The class is a singleton enforced via <see cref="Instance"/>. A second instance in the
    /// scene self-destructs and logs an error. Tests that need to reset the singleton use
    /// the <see cref="ClearInstanceForTesting"/> method.
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
        /// Reset the singleton's static reference. Used by tests to ensure a clean state
        /// between test runs. Not part of the production API.
        /// </summary>
        public static void ClearInstanceForTesting()
        {
            Instance = null;
        }
    }
}
