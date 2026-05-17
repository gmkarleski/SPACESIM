using System.Collections;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpaceSim.Foundation.Coordinates.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="FloatingOriginManager"/>. Each test creates a fresh
    /// GameObject + manager component via <c>AddComponent</c>, drives
    /// <see cref="FloatingOriginManager.MaybeShiftOrigin"/>, and verifies the resulting state
    /// and listener notifications.
    ///
    /// These tests use the local <c>_manager</c> reference rather than reading
    /// <see cref="FloatingOriginManager.Instance"/> directly. This is deliberate: in EditMode
    /// tests, MonoBehaviour lifecycle hooks (<c>Awake</c>, <c>Start</c>, <c>OnEnable</c>) do
    /// NOT fire on <c>AddComponent</c> — the play loop is not running. Reading
    /// <see cref="FloatingOriginManager.Instance"/> in EditMode would return <c>null</c>
    /// because the singleton-claim in <c>Awake</c> hasn't executed.
    ///
    /// Tests that specifically verify the singleton-Awake lifecycle live in the sibling
    /// PlayMode asmdef at <c>PlayModeTests/FloatingOriginManagerPlayModeTests.cs</c> using
    /// <c>[UnityTest] IEnumerator</c> with <c>yield return null</c> to let <c>Awake</c> fire.
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

        // ----- Singleton lifecycle tests moved to PlayMode -----
        //
        // The two singleton-lifecycle tests (Singleton_FirstInstance_BecomesInstance and
        // Singleton_DuplicateInstance_LogsErrorAndDestroys) require Awake to fire on the
        // manager component, which only happens in PlayMode. See the sibling PlayMode test
        // file at PlayModeTests/FloatingOriginManagerPlayModeTests.cs.

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

        // ----- Deferred listener registration (commit 034) -----
        //
        // These tests exercise the static RegisterListenerSafe / UnregisterListenerSafe
        // facade and the pending-queue lifecycle. Note that the SetUp's AddComponent already
        // sets _manager but does NOT fire Awake (EditMode constraint), and the singleton
        // claim (Instance = this) is inside Awake. So at test-body entry, Instance is null
        // even though _manager exists. Tests that need Instance set explicitly use the
        // ClearInstanceForTesting + reflection pattern; tests that exercise the queue-while-
        // null path benefit from the natural EditMode null state.

        [Test]
        public void RegisterListenerSafe_WhenInstanceNull_QueuesPending()
        {
            // Instance is null in EditMode (Awake didn't fire). Safe-register should queue.
            Assert.IsNull(FloatingOriginManager.Instance,
                "Sanity: EditMode SetUp does not fire Awake, so Instance should be null.");
            var listener = new CountingListener();
            FloatingOriginManager.RegisterListenerSafe(listener);
            Assert.AreEqual(1, FloatingOriginManager.PendingListenerCount,
                "Listener should be queued in the static pending list.");
            Assert.AreEqual(0, _manager.ListenerCount,
                "Manager's active list should not have the listener yet (Awake didn't fire).");
        }

        [Test]
        public void RegisterListenerSafe_WhenInstanceSet_RegistersDirectly()
        {
            // Set Instance via reflection (Awake didn't fire in EditMode).
            SetInstance(_manager);
            var listener = new CountingListener();
            FloatingOriginManager.RegisterListenerSafe(listener);
            Assert.AreEqual(0, FloatingOriginManager.PendingListenerCount,
                "With Instance set, listener should bypass the queue.");
            Assert.AreEqual(1, _manager.ListenerCount,
                "Listener should be in the active list, not the pending queue.");
        }

        [Test]
        public void RegisterListenerSafe_Null_IsIgnored()
        {
            FloatingOriginManager.RegisterListenerSafe(null);
            Assert.AreEqual(0, FloatingOriginManager.PendingListenerCount);
            Assert.AreEqual(0, _manager.ListenerCount);
        }

        [Test]
        public void RegisterListenerSafe_DuplicateWhileQueued_DoesNotDouble()
        {
            var listener = new CountingListener();
            FloatingOriginManager.RegisterListenerSafe(listener);
            FloatingOriginManager.RegisterListenerSafe(listener);
            Assert.AreEqual(1, FloatingOriginManager.PendingListenerCount,
                "Duplicate enqueue should be deduplicated at the queue level.");
        }

        [Test]
        public void UnregisterListenerSafe_WhenInPendingQueue_RemovesFromQueue()
        {
            var listener = new CountingListener();
            FloatingOriginManager.RegisterListenerSafe(listener);
            Assert.AreEqual(1, FloatingOriginManager.PendingListenerCount);
            FloatingOriginManager.UnregisterListenerSafe(listener);
            Assert.AreEqual(0, FloatingOriginManager.PendingListenerCount,
                "Listener should be removed from the pending queue.");
        }

        [Test]
        public void UnregisterListenerSafe_WhenInActiveList_RemovesFromList()
        {
            SetInstance(_manager);
            var listener = new CountingListener();
            FloatingOriginManager.RegisterListenerSafe(listener);
            Assert.AreEqual(1, _manager.ListenerCount);
            FloatingOriginManager.UnregisterListenerSafe(listener);
            Assert.AreEqual(0, _manager.ListenerCount,
                "Listener should be removed from the active list.");
        }

        [Test]
        public void UnregisterListenerSafe_Null_IsIgnored()
        {
            // Smoke: should not throw.
            FloatingOriginManager.UnregisterListenerSafe(null);
            Assert.AreEqual(0, FloatingOriginManager.PendingListenerCount);
            Assert.AreEqual(0, _manager.ListenerCount);
        }

        [Test]
        public void DrainPendingForTesting_MovesQueuedListenersToActive()
        {
            // This is the test-only simulation of what Awake does at the end of its body.
            // Queue two listeners while Instance is null.
            var listenerA = new CountingListener();
            var listenerB = new CountingListener();
            FloatingOriginManager.RegisterListenerSafe(listenerA);
            FloatingOriginManager.RegisterListenerSafe(listenerB);
            Assert.AreEqual(2, FloatingOriginManager.PendingListenerCount);
            Assert.AreEqual(0, _manager.ListenerCount);

            // Set Instance to _manager (simulating Awake claim) and then drain.
            SetInstance(_manager);
            FloatingOriginManager_DrainPendingForTesting(_manager);

            Assert.AreEqual(0, FloatingOriginManager.PendingListenerCount,
                "Pending queue should be empty after drain.");
            Assert.AreEqual(2, _manager.ListenerCount,
                "Both listeners should now be in the active list.");
        }

        [Test]
        public void DrainPendingForTesting_NullInstance_IsNoOp()
        {
            // Smoke: passing null should not throw.
            FloatingOriginManager_DrainPendingForTesting(null);
            // No state-change assertions needed; if it didn't throw the test passes.
        }

        [Test]
        public void ClearInstanceForTesting_AlsoClearsPendingQueue()
        {
            var listener = new CountingListener();
            FloatingOriginManager.RegisterListenerSafe(listener);
            Assert.AreEqual(1, FloatingOriginManager.PendingListenerCount,
                "Setup: listener should be queued.");
            FloatingOriginManager.ClearInstanceForTesting();
            Assert.AreEqual(0, FloatingOriginManager.PendingListenerCount,
                "Pending queue should be cleared by ClearInstanceForTesting.");
            Assert.IsNull(FloatingOriginManager.Instance);
        }

        [Test]
        public void DrainedListener_ReceivesShifts()
        {
            // End-to-end EditMode test: queue listener while null, simulate Awake-drain,
            // shift the origin, verify the drained listener received the shift.
            var listener = new CountingListener();
            FloatingOriginManager.RegisterListenerSafe(listener);
            SetInstance(_manager);
            FloatingOriginManager_DrainPendingForTesting(_manager);

            _manager.MaybeShiftOrigin(new WorldPosition(60_000.0, 0.0, 0.0));
            Assert.AreEqual(1, listener.ShiftCount,
                "Drained listener should have received the shift notification.");
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

        /// <summary>
        /// Set FloatingOriginManager.Instance via reflection. Awake doesn't fire in EditMode
        /// so the singleton claim doesn't happen automatically; tests that need Instance set
        /// use this. The setter is private, so reflection is required.
        /// </summary>
        private static void SetInstance(FloatingOriginManager m)
        {
            var prop = typeof(FloatingOriginManager).GetProperty(
                "Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var setter = prop.GetSetMethod(nonPublic: true);
            setter.Invoke(null, new object[] { m });
        }

        /// <summary>
        /// Reflection wrapper for the internal <c>DrainPendingForTesting</c> hook. The hook
        /// is internal so non-test code can't accidentally call it; tests in this assembly
        /// could access it directly via InternalsVisibleTo, but using reflection here keeps
        /// the test self-contained and decoupled from assembly-attribute configuration.
        /// </summary>
        private static void FloatingOriginManager_DrainPendingForTesting(FloatingOriginManager instance)
        {
            var method = typeof(FloatingOriginManager).GetMethod(
                "DrainPendingForTesting",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method.Invoke(null, new object[] { instance });
        }

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
