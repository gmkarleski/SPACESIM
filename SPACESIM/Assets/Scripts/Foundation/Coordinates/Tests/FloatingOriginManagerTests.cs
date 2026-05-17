using System.Collections;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpaceSim.Foundation.Coordinates.Tests
{
    /// <summary>
    /// PlayMode tests for <see cref="FloatingOriginManager"/>. The manager is a MonoBehaviour
    /// and requires a runtime scene context. Each test creates a fresh GameObject + manager,
    /// drives <see cref="FloatingOriginManager.MaybeShiftOrigin"/>, and verifies the resulting
    /// state and listener notifications.
    /// </summary>
    public class FloatingOriginManagerTests
    {
        private GameObject _managerGo;
        private FloatingOriginManager _manager;

        [SetUp]
        public void SetUp()
        {
            FloatingOriginManager.ClearInstanceForTesting();
            _managerGo = new GameObject("TestFloatingOriginManager");
            _manager = _managerGo.AddComponent<FloatingOriginManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerGo != null) Object.DestroyImmediate(_managerGo);
            FloatingOriginManager.ClearInstanceForTesting();
        }

        // ----- Singleton -----

        [Test]
        public void Singleton_FirstInstance_BecomesInstance()
        {
            Assert.AreEqual(_manager, FloatingOriginManager.Instance);
        }

        [Test]
        public void Singleton_DuplicateInstance_LogsErrorAndDestroys()
        {
            // Adding a second manager component on a new GameObject should produce an error
            // log and the duplicate component should be destroyed.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Duplicate FloatingOriginManager.*"));
            var dupGo = new GameObject("DuplicateManagerGo");
            var dup = dupGo.AddComponent<FloatingOriginManager>();
            // The duplicate self-destructs in Awake; its reference may persist briefly until
            // the GameObject is destroyed. Singleton stays as the original.
            Assert.AreEqual(_manager, FloatingOriginManager.Instance);
            if (dupGo != null) Object.DestroyImmediate(dupGo);
        }

        // ----- Initial state -----

        [Test]
        public void InitialOrigin_IsZero()
        {
            Assert.AreEqual(WorldPosition.Zero, _manager.CurrentOrigin);
        }

        [Test]
        public void InitialShiftCount_IsZero()
        {
            Assert.AreEqual(0, _manager.ShiftCount);
        }

        [Test]
        public void InitialThreshold_Is50Km()
        {
            // Default serialized value should be 50 km.
            Assert.AreEqual(50.0, _manager.ShiftThresholdKm);
            Assert.AreEqual(50_000.0, _manager.ShiftThresholdMeters);
        }

        // ----- Listener registration -----

        [Test]
        public void RegisterListener_AddsToList()
        {
            var listener = new CountingListener();
            _manager.RegisterListener(listener);
            Assert.AreEqual(1, _manager.ListenerCount);
        }

        [Test]
        public void RegisterListener_Duplicate_DoesNotDouble()
        {
            var listener = new CountingListener();
            _manager.RegisterListener(listener);
            _manager.RegisterListener(listener);
            Assert.AreEqual(1, _manager.ListenerCount);
        }

        [Test]
        public void RegisterListener_Null_IsIgnored()
        {
            _manager.RegisterListener(null);
            Assert.AreEqual(0, _manager.ListenerCount);
        }

        [Test]
        public void UnregisterListener_RemovesFromList()
        {
            var listener = new CountingListener();
            _manager.RegisterListener(listener);
            _manager.UnregisterListener(listener);
            Assert.AreEqual(0, _manager.ListenerCount);
        }

        // ----- Shift logic -----

        [Test]
        public void MaybeShiftOrigin_BelowThreshold_DoesNotShift()
        {
            bool shifted = _manager.MaybeShiftOrigin(new WorldPosition(40_000.0, 0.0, 0.0));
            Assert.IsFalse(shifted);
            Assert.AreEqual(WorldPosition.Zero, _manager.CurrentOrigin);
            Assert.AreEqual(0, _manager.ShiftCount);
        }

        [Test]
        public void MaybeShiftOrigin_AboveThreshold_DoesShift()
        {
            var newPos = new WorldPosition(60_000.0, 0.0, 0.0);
            bool shifted = _manager.MaybeShiftOrigin(newPos);
            Assert.IsTrue(shifted);
            Assert.AreEqual(newPos, _manager.CurrentOrigin);
            Assert.AreEqual(1, _manager.ShiftCount);
        }

        [Test]
        public void MaybeShiftOrigin_NotifiesListener()
        {
            var listener = new CountingListener();
            _manager.RegisterListener(listener);
            var newPos = new WorldPosition(60_000.0, 0.0, 0.0);
            _manager.MaybeShiftOrigin(newPos);

            Assert.AreEqual(1, listener.ShiftCount);
            Assert.AreEqual(60_000.0, listener.LastDelta.x, 1e-6);
            Assert.AreEqual(0.0, listener.LastDelta.y, 1e-6);
            Assert.AreEqual(0.0, listener.LastDelta.z, 1e-6);
        }

        [Test]
        public void MaybeShiftOrigin_NotifiesEventSubscriber()
        {
            int eventCount = 0;
            double3 capturedDelta = default;
            _manager.OriginShifted += d => { eventCount++; capturedDelta = d; };

            var newPos = new WorldPosition(60_000.0, 0.0, 0.0);
            _manager.MaybeShiftOrigin(newPos);

            Assert.AreEqual(1, eventCount);
            Assert.AreEqual(60_000.0, capturedDelta.x, 1e-6);
        }

        [Test]
        public void MaybeShiftOrigin_BelowThreshold_DoesNotNotify()
        {
            var listener = new CountingListener();
            _manager.RegisterListener(listener);
            int eventCount = 0;
            _manager.OriginShifted += _ => eventCount++;

            _manager.MaybeShiftOrigin(new WorldPosition(40_000.0, 0.0, 0.0));

            Assert.AreEqual(0, listener.ShiftCount);
            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void MaybeShiftOrigin_UpdatesOriginBeforeNotifying()
        {
            // The contract states: CurrentOrigin updates BEFORE listeners run. A listener that
            // reads CurrentOrigin in its callback should see the new origin.
            var newPos = new WorldPosition(60_000.0, 0.0, 0.0);
            WorldPosition observedDuringCallback = WorldPosition.Zero;
            _manager.OriginShifted += _ => { observedDuringCallback = _manager.CurrentOrigin; };
            _manager.MaybeShiftOrigin(newPos);
            Assert.AreEqual(newPos, observedDuringCallback);
        }

        [Test]
        public void MaybeShiftOrigin_SequentialShifts_AccumulateCount()
        {
            _manager.MaybeShiftOrigin(new WorldPosition(60_000.0, 0.0, 0.0));
            _manager.MaybeShiftOrigin(new WorldPosition(60_000.0 + 60_000.0, 0.0, 0.0));
            _manager.MaybeShiftOrigin(new WorldPosition(60_000.0 + 60_000.0 + 60_000.0, 0.0, 0.0));
            Assert.AreEqual(3, _manager.ShiftCount);
        }

        [Test]
        public void MaybeShiftOrigin_ListenerThrows_OtherListenersStillNotified()
        {
            // A throwing listener should not block other listeners from being notified.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*listener.*threw during shift.*"));
            var throwing = new ThrowingListener();
            var counting = new CountingListener();
            _manager.RegisterListener(throwing);
            _manager.RegisterListener(counting);
            _manager.MaybeShiftOrigin(new WorldPosition(60_000.0, 0.0, 0.0));
            Assert.AreEqual(1, counting.ShiftCount, "Second listener should have been notified despite first listener throwing.");
        }

        // ----- Conversion convenience -----

        [Test]
        public void WorldToLocal_UsesCurrentOrigin()
        {
            _manager.MaybeShiftOrigin(new WorldPosition(60_000.0, 0.0, 0.0));
            // Now origin is at 60_000. World position 60_010 should be local position 10.
            var local = _manager.WorldToLocal(new WorldPosition(60_010.0, 0.0, 0.0));
            Assert.AreEqual(10.0f, local.Value.x, 1e-3f);
        }

        [Test]
        public void LocalToWorld_UsesCurrentOrigin()
        {
            _manager.MaybeShiftOrigin(new WorldPosition(60_000.0, 0.0, 0.0));
            // Origin is at 60_000. Local position 10 should be world position 60_010.
            var world = _manager.LocalToWorld(new LocalPosition(10.0f, 0.0f, 0.0f));
            Assert.AreEqual(60_010.0, world.Value.x, 1e-3);
        }

        // ----- Test helpers -----

        private class CountingListener : IFloatingOriginListener
        {
            public int ShiftCount;
            public double3 LastDelta;
            public void OnFloatingOriginShifted(double3 shiftDelta)
            {
                ShiftCount++;
                LastDelta = shiftDelta;
            }
        }

        private class ThrowingListener : IFloatingOriginListener
        {
            public void OnFloatingOriginShifted(double3 shiftDelta)
            {
                throw new System.InvalidOperationException("Test exception from ThrowingListener");
            }
        }
    }
}
