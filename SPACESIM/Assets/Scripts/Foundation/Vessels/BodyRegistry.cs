using System;
using System.Collections.Generic;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Static registry of all <see cref="ReferenceBody"/> components currently active in
    /// the scene. Bodies register on <see cref="ReferenceBody.Awake"/> and unregister on
    /// <see cref="ReferenceBody.OnDestroy"/>.
    ///
    /// PRIMARY USE CASES:
    /// <list type="bullet">
    ///   <item>SOI re-rooting math needs to look up parent and child bodies by ID
    ///   without traversing Inspector wiring chains. <see cref="TryGetBodyById"/>
    ///   provides parent lookup; <see cref="GetChildrenOf"/> provides child enumeration.</item>
    ///   <item>Save-load reconstruction reads <see cref="ReferenceBody.ParentBodyId"/>
    ///   from persisted state and resolves it back to a runtime
    ///   <see cref="ReferenceBody"/> reference via <see cref="TryGetBodyById"/>.</item>
    /// </list>
    ///
    /// PATTERN PARALLEL: this class is the body-side analog of <see cref="VesselRegistry"/>.
    /// Same shape: static class with a single backing list, idempotent register/unregister
    /// (null-safe + dedup), test-only clear hook. The static-class-not-singleton-MonoBehaviour
    /// choice has the same rationale: no null-Instance race (per commit 038's
    /// VesselRegistry design), no Inspector-tunable settings, lifecycle is purely
    /// self-registration from the participants.
    ///
    /// EditMode tests use <see cref="ClearForTesting"/> in SetUp/TearDown to reset the
    /// static state between methods.
    /// </summary>
    public static class BodyRegistry
    {
        private static readonly List<ReferenceBody> _bodies = new List<ReferenceBody>();

        /// <summary>Read-only view of currently-registered bodies.</summary>
        public static IReadOnlyList<ReferenceBody> Bodies => _bodies;

        /// <summary>Number of currently-registered bodies.</summary>
        public static int BodyCount => _bodies.Count;

        /// <summary>
        /// Register a body with the registry. Null is silently ignored. Duplicate
        /// registrations are deduplicated (the body is added only once).
        /// </summary>
        public static void RegisterBodySafe(ReferenceBody body)
        {
            if (body == null) return;
            if (!_bodies.Contains(body)) _bodies.Add(body);
        }

        /// <summary>
        /// Unregister a body from the registry. Safe to call with a body that was never
        /// registered (no-op). Null is silently ignored.
        /// </summary>
        public static void UnregisterBodySafe(ReferenceBody body)
        {
            if (body == null) return;
            _bodies.Remove(body);
        }

        /// <summary>
        /// Look up a registered body by its <see cref="ReferenceBody.BodyId"/>. Returns
        /// true and assigns <paramref name="body"/> on hit; returns false and assigns
        /// <c>null</c> on miss (no body with that Guid is registered).
        ///
        /// <see cref="Guid.Empty"/> always returns false — Empty is the sentinel for
        /// "no body assigned" (e.g., <see cref="ReferenceBody.ParentBodyId"/> on a
        /// top-level body) and should never be a successful lookup target.
        /// </summary>
        public static bool TryGetBodyById(Guid bodyId, out ReferenceBody body)
        {
            if (bodyId == Guid.Empty)
            {
                body = null;
                return false;
            }
            for (int i = 0; i < _bodies.Count; i++)
            {
                if (_bodies[i] != null && _bodies[i].BodyId == bodyId)
                {
                    body = _bodies[i];
                    return true;
                }
            }
            body = null;
            return false;
        }

        /// <summary>
        /// Enumerate all registered bodies whose <see cref="ReferenceBody.ParentBody"/>
        /// is the given <paramref name="parent"/>. Returns an empty list if no children
        /// are registered (or if <paramref name="parent"/> is null — null parent has no
        /// children by definition).
        ///
        /// Used by SOI re-rooting math (commit 044+) to determine which child bodies a
        /// vessel might be entering when crossing its current body's SOI inward, AND to
        /// determine which child bodies' SOIs the vessel might be exiting when moving
        /// outward.
        ///
        /// O(N) iteration where N is the total body count. In Phase 1 / early-Phase-4
        /// scenes N is small (single-digit); a reactively-maintained children-list cache
        /// on each ReferenceBody is the shape this would take when N grows but is
        /// premature optimization for now.
        /// </summary>
        public static List<ReferenceBody> GetChildrenOf(ReferenceBody parent)
        {
            var children = new List<ReferenceBody>();
            if (parent == null) return children;
            for (int i = 0; i < _bodies.Count; i++)
            {
                if (_bodies[i] != null && _bodies[i].ParentBody == parent)
                {
                    children.Add(_bodies[i]);
                }
            }
            return children;
        }

        /// <summary>
        /// Reset the registry's static state. Used by tests to ensure clean state between
        /// runs. Not part of the production API.
        /// </summary>
        public static void ClearForTesting()
        {
            _bodies.Clear();
        }
    }
}
