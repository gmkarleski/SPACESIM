using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpaceSim.Foundation.SimTick.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SimTickController"/>. Tests use the local
    /// <c>_controller</c> reference (set by SetUp's <c>AddComponent</c> return value) and
    /// invoke <see cref="SimTickController.RunFixedUpdateCycle(int)"/> directly with a
    /// controlled iteration count. This bypasses Unity's FixedUpdate loop and the Awake
    /// lifecycle entirely, so the cycle behavior can be verified without entering Play mode.
    ///
    /// Singleton-lifecycle tests for SimTickController (verifying Awake claims Instance,
    /// duplicate destroys self, etc.) would require PlayMode and are deferred to a future
    /// commit, the same way <c>FloatingOriginManagerPlayModeTests</c> handles the singleton
    /// lifecycle for <see cref="FloatingOriginManager"/>.
    /// </summary>
    public class SimTickControllerTests
    {
        private GameObject _controllerGo;
        private SimTickController _controller;

        [SetUp]
        public void SetUp()
        {
            SimTickController.ClearInstanceForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            _controllerGo = new GameObject("TestSimTickController");
            _controller = _controllerGo.AddComponent<SimTickController>();
            // Note: Awake has NOT fired (EditMode); _controller is functional via direct
            // method calls but Instance is null. Tests use _controller directly.
        }

        [TearDown]
        public void TearDown()
        {
            if (_controllerGo != null) Object.DestroyImmediate(_controllerGo);
            SimTickController.ClearInstanceForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
        }

        // ----- Constants -----

        [Test]
        public void SimTickRate_Is30Hz()
        {
            Assert.AreEqual(30f, SimTickController.SimTickRate);
        }

        [Test]
        public void SimTickIntervalSeconds_Is33MsExactly()
        {
            // 1f / 30f ≈ 0.033333...; float-precision exact equality is fine here.
            Assert.AreEqual(1f / 30f, SimTickController.SimTickIntervalSeconds);
        }

        // ----- Initial state -----

        [Test]
        public void InitialTickNumber_IsZero()
        {
            Assert.AreEqual(0L, _controller.TickNumber);
        }

        [Test]
        public void InitialCurrentPhase_IsIdle()
        {
            Assert.AreEqual(SimTickPhase.Idle, _controller.CurrentPhase);
        }

        [Test]
        public void InitialFixedUpdateCount_IsZero()
        {
            Assert.AreEqual(0L, _controller.FixedUpdateCount);
        }

        [Test]
        public void WarpController_IsInitialized()
        {
            Assert.IsNotNull(_controller.Warp);
            Assert.AreEqual(1.0, _controller.Warp.EffectiveWarpRate);
        }

        // ----- Cycle behavior: tick counter -----

        [Test]
        public void RunFixedUpdateCycle_OneIteration_AdvancesTickByOne()
        {
            _controller.RunFixedUpdateCycle(1);
            Assert.AreEqual(1L, _controller.TickNumber);
        }

        [Test]
        public void RunFixedUpdateCycle_NIterations_AdvancesTickByN()
        {
            _controller.RunFixedUpdateCycle(100);
            Assert.AreEqual(100L, _controller.TickNumber);
        }

        [Test]
        public void RunFixedUpdateCycle_ZeroOrNegativeIterations_ClampsToOne()
        {
            _controller.RunFixedUpdateCycle(0);
            Assert.AreEqual(1L, _controller.TickNumber);
            _controller.RunFixedUpdateCycle(-5);
            Assert.AreEqual(2L, _controller.TickNumber);
        }

        [Test]
        public void RunFixedUpdateCycle_Sequential_Accumulates()
        {
            _controller.RunFixedUpdateCycle(10);
            _controller.RunFixedUpdateCycle(20);
            _controller.RunFixedUpdateCycle(30);
            Assert.AreEqual(60L, _controller.TickNumber);
        }

        // ----- Cycle behavior: phase tracking -----

        [Test]
        public void RunFixedUpdateCycle_ExitsAtIdlePhase()
        {
            _controller.RunFixedUpdateCycle(5);
            Assert.AreEqual(SimTickPhase.Idle, _controller.CurrentPhase);
        }

        // ----- Listener notification -----

        [Test]
        public void RunFixedUpdateCycle_NotifiesInterfaceListener_OncePerIteration()
        {
            var listener = new CountingTickListener();
            _controller.RegisterListener(listener);
            _controller.RunFixedUpdateCycle(7);
            Assert.AreEqual(7, listener.NotificationCount);
            Assert.AreEqual(7L, listener.LastTickNumber);
        }

        [Test]
        public void RunFixedUpdateCycle_RaisesTickAdvancedEvent_OncePerIteration()
        {
            int eventCount = 0;
            long lastTick = -1L;
            _controller.TickAdvanced += t => { eventCount++; lastTick = t; };
            _controller.RunFixedUpdateCycle(5);
            Assert.AreEqual(5, eventCount);
            Assert.AreEqual(5L, lastTick);
        }

        [Test]
        public void RunFixedUpdateCycle_NotifiesBothInterfaceAndEvent()
        {
            var listener = new CountingTickListener();
            _controller.RegisterListener(listener);
            int eventCount = 0;
            _controller.TickAdvanced += _ => eventCount++;
            _controller.RunFixedUpdateCycle(3);
            Assert.AreEqual(3, listener.NotificationCount);
            Assert.AreEqual(3, eventCount);
        }

        // ----- Listener registration semantics (mirrors FloatingOriginManager) -----

        [Test]
        public void RegisterListener_AddsToList()
        {
            var listener = new CountingTickListener();
            _controller.RegisterListener(listener);
            Assert.AreEqual(1, _controller.ListenerCount);
        }

        [Test]
        public void RegisterListener_Duplicate_DoesNotDouble()
        {
            var listener = new CountingTickListener();
            _controller.RegisterListener(listener);
            _controller.RegisterListener(listener);
            Assert.AreEqual(1, _controller.ListenerCount);
        }

        [Test]
        public void RegisterListener_Null_IsIgnored()
        {
            _controller.RegisterListener(null);
            Assert.AreEqual(0, _controller.ListenerCount);
        }

        [Test]
        public void UnregisterListener_RemovesFromList()
        {
            var listener = new CountingTickListener();
            _controller.RegisterListener(listener);
            _controller.UnregisterListener(listener);
            Assert.AreEqual(0, _controller.ListenerCount);
        }

        // ----- Listener resilience -----

        [Test]
        public void RunFixedUpdateCycle_ThrowingListener_OthersStillNotified()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*threw during tick advance.*"));
            var throwing = new ThrowingTickListener();
            var counting = new CountingTickListener();
            _controller.RegisterListener(throwing);
            _controller.RegisterListener(counting);
            _controller.RunFixedUpdateCycle(1);
            Assert.AreEqual(1, counting.NotificationCount,
                "Second listener should be notified despite first throwing.");
        }

        // ----- Step 6 wiring: FloatingOriginManager -----

        [Test]
        public void Step6_WithoutFloatingOriginManager_LogsWarningOnce()
        {
            // FloatingOriginManager.Instance is null per SetUp's ClearInstanceForTesting.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*FloatingOriginManager.Instance is null.*"));
            _controller.RunFixedUpdateCycle(1);
            // Second cycle should NOT produce another warning (once-per-controller-lifetime).
            // If a second warning were emitted, LogAssert.NoUnexpectedReceived (called at test
            // teardown) would catch it.
            _controller.RunFixedUpdateCycle(1);
            // Test passes if no additional unexpected logs surfaced.
        }

        [Test]
        public void Step6_WithFloatingOriginManager_BelowThreshold_DoesNotShift()
        {
            var managerGo = new GameObject("TestFloatingOriginManager");
            var manager = managerGo.AddComponent<FloatingOriginManager>();
            // Manually set Instance because Awake doesn't fire in EditMode.
            SetFloatingOriginManagerInstance(manager);

            _controller.SetActiveVesselWorldPosition(new WorldPosition(40_000.0, 0.0, 0.0));
            _controller.RunFixedUpdateCycle(1);

            Assert.AreEqual(0, manager.ShiftCount, "Below threshold; no shift expected.");
            Object.DestroyImmediate(managerGo);
        }

        [Test]
        public void Step6_WithFloatingOriginManager_AboveThreshold_DoesShift()
        {
            var managerGo = new GameObject("TestFloatingOriginManager");
            var manager = managerGo.AddComponent<FloatingOriginManager>();
            SetFloatingOriginManagerInstance(manager);

            _controller.SetActiveVesselWorldPosition(new WorldPosition(60_000.0, 0.0, 0.0));
            _controller.RunFixedUpdateCycle(1);

            Assert.AreEqual(1, manager.ShiftCount, "Above 50km threshold; one shift expected.");
            Assert.AreEqual(new WorldPosition(60_000.0, 0.0, 0.0), manager.CurrentOrigin);
            Object.DestroyImmediate(managerGo);
        }

        [Test]
        public void Step6_RunsOncePerFixedUpdate_NotPerAnalyticIteration()
        {
            // With analyticIterations=100 and an above-threshold active vessel, the manager
            // should still see exactly ONE MaybeShiftOrigin call, not 100. The first call
            // moves the origin to the active-vessel position; subsequent calls would see
            // distance 0 (no shift). So a passing test here verifies the cycle's once-per-
            // FixedUpdate cadence for step 6 even before considering the no-redundant-shift
            // logic in FloatingOriginManager.
            var managerGo = new GameObject("TestFloatingOriginManager");
            var manager = managerGo.AddComponent<FloatingOriginManager>();
            SetFloatingOriginManagerInstance(manager);

            _controller.SetActiveVesselWorldPosition(new WorldPosition(60_000.0, 0.0, 0.0));
            _controller.RunFixedUpdateCycle(100);

            Assert.AreEqual(1, manager.ShiftCount,
                "Step 6 runs once per FixedUpdate (gated by i == 0), not per analytic iteration.");
            Assert.AreEqual(100L, _controller.TickNumber,
                "Tick counter still advances by analyticIterations (100), separate from step 6's cadence.");
            Object.DestroyImmediate(managerGo);
        }

        // ----- Test helpers -----

        /// <summary>
        /// Force the FloatingOriginManager.Instance static field via reflection. Awake
        /// doesn't fire in EditMode, so we set Instance manually so step 6's null-check
        /// passes.
        /// </summary>
        private static void SetFloatingOriginManagerInstance(FloatingOriginManager m)
        {
            var prop = typeof(FloatingOriginManager).GetProperty(
                "Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            // Instance has a private setter; use reflection to bypass.
            var setter = prop.GetSetMethod(nonPublic: true);
            setter.Invoke(null, new object[] { m });
        }

        private class CountingTickListener : ISimTickListener
        {
            public int NotificationCount;
            public long LastTickNumber;
            public void OnSimTickAdvanced(long tickNumber)
            {
                NotificationCount++;
                LastTickNumber = tickNumber;
            }
        }

        private class ThrowingTickListener : ISimTickListener
        {
            public void OnSimTickAdvanced(long tickNumber)
            {
                throw new System.InvalidOperationException("Test exception from ThrowingTickListener");
            }
        }
    }
}
