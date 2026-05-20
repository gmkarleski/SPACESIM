using System;
using NUnit.Framework;
using SpaceSim.Foundation.SimTick;

namespace SpaceSim.Foundation.SimTick.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="EventPriorityQueue"/>. Pure-data-structure tests
    /// — no Unity dependencies, no scene setup, no MonoBehaviour lifecycle. Each
    /// test constructs a fresh queue in SetUp to avoid cross-test state leakage.
    /// </summary>
    public class EventPriorityQueueTests
    {
        private EventPriorityQueue _queue;
        private Guid _vesselA;
        private Guid _vesselB;

        [SetUp]
        public void SetUp()
        {
            _queue = new EventPriorityQueue();
            _vesselA = Guid.NewGuid();
            _vesselB = Guid.NewGuid();
        }

        [Test]
        public void PriorityQueue_NewQueue_IsEmpty()
        {
            Assert.AreEqual(0, _queue.Count);
            Assert.IsNull(_queue.PeekTopTick());
            Assert.IsFalse(_queue.TryPeekTop(out _));
        }

        [Test]
        public void PriorityQueue_AddSingle_PeekReturnsIt()
        {
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);

            Assert.AreEqual(1, _queue.Count);
            Assert.AreEqual(100L, _queue.PeekTopTick());
            Assert.IsTrue(_queue.TryPeekTop(out var top));
            Assert.AreEqual(100L, top.tick);
            Assert.AreEqual(_vesselA, top.vesselId);
            Assert.AreEqual(SimEventType.Periapsis, top.eventType);
        }

        [Test]
        public void PriorityQueue_AddMultiple_PeekReturnsEarliest()
        {
            // Insert in non-sorted order. Peek should return tick=50 (the earliest).
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 200L);
            _queue.UpdateVesselEntry(_vesselB, SimEventType.Apoapsis, 50L);
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Apoapsis, 150L);

            Assert.AreEqual(3, _queue.Count);
            Assert.AreEqual(50L, _queue.PeekTopTick());
            Assert.IsTrue(_queue.TryPeekTop(out var top));
            Assert.AreEqual(_vesselB, top.vesselId);
            Assert.AreEqual(SimEventType.Apoapsis, top.eventType);
        }

        [Test]
        public void PriorityQueue_UpdateExisting_ReplacesTick()
        {
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);
            Assert.AreEqual(100L, _queue.PeekTopTick());

            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 250L);

            // Still one entry; tick is now 250 not 100.
            Assert.AreEqual(1, _queue.Count);
            Assert.AreEqual(250L, _queue.PeekTopTick());
        }

        [Test]
        public void PriorityQueue_UpdateToNull_RemovesEntry()
        {
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Apoapsis, 200L);
            Assert.AreEqual(2, _queue.Count);

            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, null);

            Assert.AreEqual(1, _queue.Count, "Null tick should remove the entry");
            Assert.AreEqual(200L, _queue.PeekTopTick(), "Apoapsis entry should remain");
        }

        [Test]
        public void PriorityQueue_UpdateToNullOnNonexistent_IsNoOp()
        {
            // Removing an entry that was never added should not throw.
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, null);

            Assert.AreEqual(0, _queue.Count);
        }

        [Test]
        public void PriorityQueue_MultipleVesselsSameSimEventType_KeepsBoth()
        {
            // Two vessels can both have Periapsis entries; they're keyed by
            // (vesselId, eventType) not just eventType.
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);
            _queue.UpdateVesselEntry(_vesselB, SimEventType.Periapsis, 200L);

            Assert.AreEqual(2, _queue.Count);
            Assert.AreEqual(100L, _queue.PeekTopTick());
        }

        [Test]
        public void PriorityQueue_SameVesselDifferentSimEventTypes_KeepsBoth()
        {
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Apoapsis, 50L);

            Assert.AreEqual(2, _queue.Count);
            Assert.AreEqual(50L, _queue.PeekTopTick());
            Assert.IsTrue(_queue.TryPeekTop(out var top));
            Assert.AreEqual(SimEventType.Apoapsis, top.eventType);
        }

        [Test]
        public void PriorityQueue_TieBreaker_SameTickOrderedDeterministically()
        {
            // Two events at exactly the same tick. Sort key tie-breaks by Guid then
            // SimEventType. The smaller Guid (lexicographic in byte order) sorts first.
            // We can't predict which random Guid is smaller, but we CAN verify that
            // back-to-back calls produce a deterministic ordering — same Guids in,
            // same top out.
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);
            _queue.UpdateVesselEntry(_vesselB, SimEventType.Periapsis, 100L);

            Assert.IsTrue(_queue.TryPeekTop(out var top1));

            // Re-build with the same vesselIds in opposite insertion order.
            _queue.Clear();
            _queue.UpdateVesselEntry(_vesselB, SimEventType.Periapsis, 100L);
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);

            Assert.IsTrue(_queue.TryPeekTop(out var top2));

            // The top should be the same regardless of insertion order — the sort
            // is by content (tick, vesselId, eventType), not by insertion order.
            Assert.AreEqual(top1.vesselId, top2.vesselId,
                "Tie-breaking should be deterministic across insertion orders");
            Assert.AreEqual(top1.eventType, top2.eventType);
            Assert.AreEqual(top1.tick, top2.tick);
        }

        [Test]
        public void PriorityQueue_RemoveVesselEntries_RemovesAll()
        {
            // Vessel A has multiple event types; vessel B has one.
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Apoapsis, 200L);
            _queue.UpdateVesselEntry(_vesselA, SimEventType.SoiCrossing, 300L);
            _queue.UpdateVesselEntry(_vesselB, SimEventType.Periapsis, 150L);
            Assert.AreEqual(4, _queue.Count);

            _queue.RemoveVesselEntries(_vesselA);

            Assert.AreEqual(1, _queue.Count, "Only vessel B's entry should remain");
            Assert.AreEqual(150L, _queue.PeekTopTick());
        }

        [Test]
        public void PriorityQueue_RemoveVesselEntries_UnknownVessel_IsNoOp()
        {
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);
            Guid unknownVessel = Guid.NewGuid();

            _queue.RemoveVesselEntries(unknownVessel);

            Assert.AreEqual(1, _queue.Count, "Removing unknown vessel should not affect existing entries");
        }

        [Test]
        public void PriorityQueue_Clear_EmptiesAllEntries()
        {
            _queue.UpdateVesselEntry(_vesselA, SimEventType.Periapsis, 100L);
            _queue.UpdateVesselEntry(_vesselB, SimEventType.Apoapsis, 200L);
            Assert.AreEqual(2, _queue.Count);

            _queue.Clear();

            Assert.AreEqual(0, _queue.Count);
            Assert.IsNull(_queue.PeekTopTick());
        }
    }
}
