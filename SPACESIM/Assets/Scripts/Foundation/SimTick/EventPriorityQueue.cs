using System;
using System.Collections.Generic;

namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Priority queue of upcoming analytic events sorted by sim-tick. Owned by
    /// <see cref="SimTickController"/> per netcode contract §4.1 ("the game maintains
    /// a priority queue of upcoming events sorted by their scheduled sim-tick") and
    /// CONSTRAINTS §2 ("Authority over the queue lives in the sim-tick controller").
    ///
    /// <para>USAGE:</para>
    /// Predictors call <see cref="UpdateVesselEntry"/> each tick to record (or refresh)
    /// a vessel's upcoming event. The warp controller reads <see cref="PeekTopTick"/>
    /// or <see cref="TryPeekTop"/> to determine how many ticks of advancement are
    /// safe before the next event fires. Vessels that get destroyed or transition
    /// out of Kepler-rails call <see cref="RemoveVesselEntries"/> to clean up.
    ///
    /// <para>INTERNAL STRUCTURE:</para>
    /// Two collections maintained in lockstep:
    /// <list type="bullet">
    ///   <item><c>SortedSet&lt;(long tick, Guid vesselId, SimEventType type)&gt;</c> — the
    ///   sorted view. Peek operations read the minimum in O(log n).</item>
    ///   <item><c>Dictionary&lt;(Guid, SimEventType), long&gt;</c> — side index mapping
    ///   each vessel/event-type pair to its current tick. Used to find the existing
    ///   entry for update or removal in O(1) before the O(log n) SortedSet operation.</item>
    /// </list>
    /// All public operations are O(log n) or better at the small N expected in
    /// Phase 1 (1-2 vessels × ≤7 event types). Performance is not the primary
    /// concern; correctness and clarity are.
    ///
    /// <para>TIE-BREAKING:</para>
    /// When multiple events share the same tick, the sort key
    /// <c>(tick, vesselId, eventType)</c> compares lexicographically:
    /// <list type="number">
    ///   <item>By tick first (the primary ordering).</item>
    ///   <item>Then by Guid (arbitrary but deterministic across runs because Guid
    ///   ordering is value-based, not address-based).</item>
    ///   <item>Finally by enum ordinal (Periapsis=0 &lt; Apoapsis=1 &lt; ... per
    ///   <see cref="SimEventType"/>).</item>
    /// </list>
    /// This determinism matters for replay and save/load: the same world state with
    /// the same events at the same ticks produces the same priority queue ordering
    /// across machines and across runs.
    /// </summary>
    public class EventPriorityQueue
    {
        private readonly SortedSet<(long tick, Guid vesselId, SimEventType type)> _sorted
            = new SortedSet<(long, Guid, SimEventType)>();

        private readonly Dictionary<(Guid vesselId, SimEventType type), long> _index
            = new Dictionary<(Guid, SimEventType), long>();

        /// <summary>Total number of entries currently in the queue.</summary>
        public int Count => _sorted.Count;

        /// <summary>
        /// Add, update, or remove a vessel's entry for a given event type.
        /// Idempotent.
        /// <list type="bullet">
        ///   <item>If <paramref name="eventTick"/> is non-null and no entry exists
        ///   for this (vessel, eventType) pair: add a new entry.</item>
        ///   <item>If <paramref name="eventTick"/> is non-null and an entry already
        ///   exists: replace the existing entry's tick.</item>
        ///   <item>If <paramref name="eventTick"/> is null: remove the entry if
        ///   present (no-op if absent).</item>
        /// </list>
        /// O(log n) in all cases.
        /// </summary>
        /// <param name="vesselId">UUID of the vessel the event belongs to.</param>
        /// <param name="eventType">Which kind of event (periapsis, apoapsis, etc.).</param>
        /// <param name="eventTick">When the event fires, or null to remove the entry.</param>
        public void UpdateVesselEntry(Guid vesselId, SimEventType eventType, long? eventTick)
        {
            var key = (vesselId, eventType);

            // Remove existing entry from the sorted view if present, regardless of
            // whether we're updating or removing.
            if (_index.TryGetValue(key, out long oldTick))
            {
                _sorted.Remove((oldTick, vesselId, eventType));
                _index.Remove(key);
            }

            // If a new tick was supplied, add the updated entry.
            if (eventTick.HasValue)
            {
                _sorted.Add((eventTick.Value, vesselId, eventType));
                _index[key] = eventTick.Value;
            }
        }

        /// <summary>
        /// Return the sim-tick of the earliest event in the queue, or null if empty.
        /// O(log n) for SortedSet.Min in the .NET implementation.
        /// </summary>
        public long? PeekTopTick()
        {
            if (_sorted.Count == 0) return null;
            return _sorted.Min.tick;
        }

        /// <summary>
        /// Try to read the earliest entry without removing it. Returns false if the
        /// queue is empty (in which case <paramref name="top"/> is the default
        /// tuple). Returns true with the full (tick, vesselId, eventType) tuple if
        /// non-empty.
        /// </summary>
        public bool TryPeekTop(out (long tick, Guid vesselId, SimEventType eventType) top)
        {
            if (_sorted.Count == 0)
            {
                top = default;
                return false;
            }
            top = _sorted.Min;
            return true;
        }

        /// <summary>
        /// Remove all entries belonging to <paramref name="vesselId"/> regardless of
        /// event type. Called when a vessel is destroyed, unregistered, or
        /// transitions out of Kepler-rails (the only mode that populates events in
        /// commit 045 scope).
        ///
        /// Iterates the seven possible event types and checks the side index for
        /// each — effectively O(1) per vessel (constant number of event types) with
        /// O(log n) per actual removal. Total O(log n) regardless of vessel count.
        /// </summary>
        public void RemoveVesselEntries(Guid vesselId)
        {
            // Collect the keys to remove first (can't modify _index during iteration).
            // Bounded to the cardinality of SimEventType (7); no heap allocation concern.
            List<(Guid, SimEventType)> toRemove = null;
            foreach (var kvp in _index)
            {
                if (kvp.Key.vesselId == vesselId)
                {
                    toRemove ??= new List<(Guid, SimEventType)>(capacity: 7);
                    toRemove.Add(kvp.Key);
                }
            }
            if (toRemove == null) return;
            foreach (var key in toRemove)
            {
                long tick = _index[key];
                _sorted.Remove((tick, key.Item1, key.Item2));
                _index.Remove(key);
            }
        }

        /// <summary>
        /// Reset the queue to empty. Test hook (also useful for scene unload /
        /// shutdown paths where the controller is being torn down).
        /// </summary>
        public void Clear()
        {
            _sorted.Clear();
            _index.Clear();
        }
    }
}
