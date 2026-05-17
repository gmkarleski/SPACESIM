using System.Collections.Generic;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Static registry of all <see cref="Vessel"/> components currently active in the scene.
    ///
    /// Vessels register themselves on <c>OnEnable</c> via <see cref="RegisterVesselSafe"/>
    /// and unregister on <c>OnDisable</c> via <see cref="UnregisterVesselSafe"/>. The
    /// registry is the canonical "list of vessels the sim-tick controller iterates" — Step 4
    /// (analytic advancement) of the 10-step cycle reads from <see cref="Vessels"/>.
    ///
    /// WHY THIS IS A STATIC CLASS, NOT A SINGLETON MONOBEHAVIOUR:
    /// The deferred-registration pattern from commit 034's
    /// <c>FloatingOriginManager</c> exists to solve a specific problem: a singleton
    /// MonoBehaviour's <c>Instance</c> field is null until its <c>Awake</c> fires, so
    /// listeners that register via OnEnable could lose the race. A static class has no
    /// such race — the CLR's static-initializer machinery guarantees the type's static
    /// fields are initialized before any static member is accessed. The first call to
    /// <see cref="RegisterVesselSafe"/> triggers the static constructor synchronously and
    /// the call proceeds against an initialized list.
    ///
    /// Tests use <see cref="ClearForTesting"/> in <c>[SetUp]</c> / <c>[TearDown]</c> to
    /// reset the static state between test methods. Same discipline as
    /// <c>FloatingOriginManager.ClearInstanceForTesting</c> from commit 029.
    ///
    /// The <c>Safe</c> suffix on <see cref="RegisterVesselSafe"/> /
    /// <see cref="UnregisterVesselSafe"/> is preserved for cross-codebase naming consistency
    /// with <see cref="Coordinates.FloatingOriginManager.RegisterListenerSafe"/>. The methods
    /// are "safe" in the sense of null-safe and dedup-safe (idempotent calls).
    /// </summary>
    public static class VesselRegistry
    {
        private static readonly List<Vessel> _vessels = new List<Vessel>();

        /// <summary>Read-only view of currently-registered vessels.</summary>
        public static IReadOnlyList<Vessel> Vessels => _vessels;

        /// <summary>Number of currently-registered vessels.</summary>
        public static int VesselCount => _vessels.Count;

        /// <summary>
        /// Register a vessel with the registry. Null is silently ignored. Duplicate
        /// registrations are deduplicated (the vessel is added only once).
        /// </summary>
        public static void RegisterVesselSafe(Vessel vessel)
        {
            if (vessel == null) return;
            if (!_vessels.Contains(vessel)) _vessels.Add(vessel);
        }

        /// <summary>
        /// Unregister a vessel from the registry. Safe to call with a vessel that was never
        /// registered (no-op). Null is silently ignored.
        /// </summary>
        public static void UnregisterVesselSafe(Vessel vessel)
        {
            if (vessel == null) return;
            _vessels.Remove(vessel);
        }

        /// <summary>
        /// Reset the registry's static state. Used by tests to ensure clean state between
        /// runs. Not part of the production API.
        /// </summary>
        public static void ClearForTesting()
        {
            _vessels.Clear();
        }
    }
}
