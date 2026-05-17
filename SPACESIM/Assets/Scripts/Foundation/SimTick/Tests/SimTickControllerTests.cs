using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;
using UnityEngine;
using UnityEngine.TestTools;
// Alias UnityEngine.Object to disambiguate from System.Object. Tests use
// UnityObject.DestroyImmediate for GameObject cleanup; bare 'Object' is ambiguous when
// both 'using System' (transitive via test framework usings) and 'using UnityEngine'
// could be in scope.
using UnityObject = UnityEngine.Object;

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
        public void Step6_WithoutActiveVessel_LogsWarningOnce()
        {
            // FloatingOriginManager present so that branch passes; ActiveVessel left null.
            var managerGo = new GameObject("TestFloatingOriginManager");
            var manager = managerGo.AddComponent<FloatingOriginManager>();
            SetFloatingOriginManagerInstance(manager);

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*ActiveVessel is null.*"));
            _controller.RunFixedUpdateCycle(1);
            // Second cycle: warning should NOT re-fire (once-per-controller-lifetime).
            _controller.RunFixedUpdateCycle(1);

            Assert.AreEqual(0, manager.ShiftCount, "No shift expected when ActiveVessel is null.");
            UnityObject.DestroyImmediate(managerGo);
        }

        [Test]
        public void Step6_WithActiveVessel_BelowThreshold_DoesNotShift()
        {
            var managerGo = new GameObject("TestFloatingOriginManager");
            var manager = managerGo.AddComponent<FloatingOriginManager>();
            SetFloatingOriginManagerInstance(manager);

            // POCO stub implementing IActiveVessel. No GameObject / Rigidbody scaffolding
            // needed because the interface contract is narrow (position + mode).
            var vessel = new ActiveVesselStub
            {
                Position = new WorldPosition(40_000.0, 0.0, 0.0),
                ModeValue = PhysicsMode.PhysXActive,
            };
            _controller.SetActiveVessel(vessel);
            _controller.RunFixedUpdateCycle(1);

            Assert.AreEqual(0, manager.ShiftCount, "Below threshold; no shift expected.");
            UnityObject.DestroyImmediate(managerGo);
        }

        [Test]
        public void Step6_WithActiveVessel_AboveThreshold_DoesShift()
        {
            var managerGo = new GameObject("TestFloatingOriginManager");
            var manager = managerGo.AddComponent<FloatingOriginManager>();
            SetFloatingOriginManagerInstance(manager);

            var vessel = new ActiveVesselStub
            {
                Position = new WorldPosition(60_000.0, 0.0, 0.0),
                ModeValue = PhysicsMode.PhysXActive,
            };
            _controller.SetActiveVessel(vessel);
            _controller.RunFixedUpdateCycle(1);

            Assert.AreEqual(1, manager.ShiftCount, "Above 50km threshold; one shift expected.");
            Assert.AreEqual(new WorldPosition(60_000.0, 0.0, 0.0), manager.CurrentOrigin);
            UnityObject.DestroyImmediate(managerGo);
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

            var vessel = new ActiveVesselStub
            {
                Position = new WorldPosition(60_000.0, 0.0, 0.0),
                ModeValue = PhysicsMode.PhysXActive,
            };
            _controller.SetActiveVessel(vessel);
            _controller.RunFixedUpdateCycle(100);

            Assert.AreEqual(1, manager.ShiftCount,
                "Step 6 runs once per FixedUpdate (gated by i == 0), not per analytic iteration.");
            Assert.AreEqual(100L, _controller.TickNumber,
                "Tick counter still advances by analyticIterations (100), separate from step 6's cadence.");
            UnityObject.DestroyImmediate(managerGo);
        }

        // ----- SetActiveVessel: warp-mode tracking -----

        [Test]
        public void SetActiveVessel_WithPhysXActive_SetsWarpModeToPhysXActive()
        {
            // Pre-set warp to KeplerRails so we can detect the assignment back to PhysXActive.
            _controller.Warp.SetActiveVesselMode(PhysicsMode.KeplerRails);
            Assert.AreEqual(PhysicsMode.KeplerRails, _controller.Warp.ActiveVesselMode);

            var vessel = new ActiveVesselStub { ModeValue = PhysicsMode.PhysXActive };
            _controller.SetActiveVessel(vessel);

            Assert.AreEqual(PhysicsMode.PhysXActive, _controller.Warp.ActiveVesselMode,
                "SetActiveVessel should propagate vessel.Mode to Warp.SetActiveVesselMode.");
        }

        [Test]
        public void SetActiveVessel_WithKeplerRails_SetsWarpModeToKeplerRails()
        {
            var vessel = new ActiveVesselStub { ModeValue = PhysicsMode.KeplerRails };
            _controller.SetActiveVessel(vessel);

            Assert.AreEqual(PhysicsMode.KeplerRails, _controller.Warp.ActiveVesselMode);
        }

        [Test]
        public void SetActiveVessel_Null_ResetsWarpModeToPhysXActive()
        {
            // First put warp in KeplerRails via a vessel.
            _controller.SetActiveVessel(new ActiveVesselStub { ModeValue = PhysicsMode.KeplerRails });
            Assert.AreEqual(PhysicsMode.KeplerRails, _controller.Warp.ActiveVesselMode);

            // Now clear ActiveVessel. Warp should reset to PhysXActive (most-restrictive default).
            _controller.SetActiveVessel(null);

            Assert.IsNull(_controller.ActiveVessel);
            Assert.AreEqual(PhysicsMode.PhysXActive, _controller.Warp.ActiveVesselMode,
                "SetActiveVessel(null) should reset warp to PhysXActive (most-restrictive default).");
        }

        [Test]
        public void Step6_WhenVesselModeChanges_WarpModeTracksOnNextTick()
        {
            // Set up active vessel in PhysXActive mode.
            var managerGo = new GameObject("TestFloatingOriginManager");
            var manager = managerGo.AddComponent<FloatingOriginManager>();
            SetFloatingOriginManagerInstance(manager);
            var vessel = new ActiveVesselStub
            {
                Position = new WorldPosition(10_000.0, 0.0, 0.0),
                ModeValue = PhysicsMode.PhysXActive,
            };
            _controller.SetActiveVessel(vessel);
            Assert.AreEqual(PhysicsMode.PhysXActive, _controller.Warp.ActiveVesselMode);

            // Vessel's mode changes (simulating an in-play transition). Without calling
            // SetActiveVessel again, the warp controller's mode should still track on the
            // next FixedUpdate via step 6's per-tick assignment.
            vessel.ModeValue = PhysicsMode.KeplerRails;
            _controller.RunFixedUpdateCycle(1);

            Assert.AreEqual(PhysicsMode.KeplerRails, _controller.Warp.ActiveVesselMode,
                "Step 6 should pick up vessel.Mode changes on each FixedUpdate without an explicit SetActiveVessel re-call.");
            UnityObject.DestroyImmediate(managerGo);
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

        /// <summary>
        /// POCO test double implementing <see cref="IActiveVessel"/>. Step-6 tests use this
        /// instead of constructing a real <c>Vessel</c> with GameObject + Rigidbody +
        /// ReferenceBody scaffolding. The interface contract is narrow (position + mode),
        /// so a two-field stub satisfies it.
        ///
        /// This is exactly the testability payoff of the IActiveVessel interface (per
        /// commit 038 design discussion): tests can stub the contract without the full
        /// component-chain assembly.
        /// </summary>
        private class ActiveVesselStub : IActiveVessel
        {
            public WorldPosition Position;
            public PhysicsMode ModeValue;
            public WorldPosition GetWorldPosition() => Position;
            public PhysicsMode Mode => ModeValue;
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
