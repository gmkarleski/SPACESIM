using System;
using NUnit.Framework;
using SpaceSim.Foundation.SimTick;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace SpaceSim.Foundation.SimTick.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="WarpController"/> (commit 048 Stage 2). Tests
    /// instantiate a <see cref="WarpController"/> GameObject in <see cref="SetUp"/>
    /// and claim the singleton slot via
    /// <see cref="WarpController.SetInstanceForTesting"/> — Awake doesn't fire on
    /// <c>AddComponent</c> in EditMode, so the SetInstanceForTesting path is the
    /// canonical EditMode-test setup. Pair every claim with a
    /// <see cref="WarpController.ClearInstanceForTesting"/> in <see cref="TearDown"/>
    /// to keep state from leaking between tests.
    /// </summary>
    public class WarpControllerTests
    {
        private GameObject _controllerGo;
        private WarpController _controller;

        [SetUp]
        public void SetUp()
        {
            WarpController.ClearInstanceForTesting();
            _controllerGo = new GameObject("TestWarpController");
            _controller = _controllerGo.AddComponent<WarpController>();
            WarpController.SetInstanceForTesting(_controller);
        }

        [TearDown]
        public void TearDown()
        {
            if (_controllerGo != null) UnityObject.DestroyImmediate(_controllerGo);
            WarpController.ClearInstanceForTesting();
        }

        // ----- Defaults -----

        [Test]
        public void Defaults_AreSafe()
        {
            Assert.AreEqual(WarpRate.OneX, _controller.CurrentRate);
            Assert.AreEqual(PhysicsMode.PhysXActive, _controller.ActiveVesselMode);
            Assert.IsFalse(_controller.IsHalting);
            Assert.IsNull(_controller.LastHaltInfo);
            Assert.IsNull(_controller.TargetTick);
        }

        // ----- CeilingFor -----

        [Test]
        public void CeilingFor_PhysX_Is5()
        {
            Assert.AreEqual(5L, WarpController.CeilingFor(PhysicsMode.PhysXActive));
        }

        [Test]
        public void CeilingFor_KeplerRails_Is10000()
        {
            Assert.AreEqual(10_000L, WarpController.CeilingFor(PhysicsMode.KeplerRails));
        }

        [Test]
        public void CeilingFor_InterstellarCruise_Is100000()
        {
            Assert.AreEqual(100_000L, WarpController.CeilingFor(PhysicsMode.InterstellarCruise));
        }

        // ----- Rate setters -----

        [Test]
        public void SetContinuousRate_BelowOne_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _controller.SetContinuousRate(0));
            Assert.Throws<ArgumentException>(() => _controller.SetContinuousRate(-5));
        }

        [Test]
        public void SetContinuousRate_At1000_Succeeds()
        {
            _controller.SetContinuousRate(1000);
            Assert.AreEqual(new WarpRate(1000, 1), _controller.CurrentRate);
        }

        [Test]
        public void SetContinuousRate_FiresOnRateChanged()
        {
            WarpRate? received = null;
            _controller.OnRateChanged += r => received = r;

            _controller.SetContinuousRate(250);

            Assert.IsTrue(received.HasValue, "OnRateChanged should fire on SetContinuousRate");
            Assert.AreEqual(new WarpRate(250, 1), received.Value);
        }

        [Test]
        public void SetDiscreteLevel_InvalidLevel_ThrowsArgumentException()
        {
            // 7 is not a valid discrete level (valid: 1, 5, 10, 100, 1000, 10000, 100000).
            Assert.Throws<ArgumentException>(() => _controller.SetDiscreteLevel(7));
        }

        [Test]
        public void SetDiscreteLevel_ValidLevel_Succeeds()
        {
            _controller.SetDiscreteLevel(100);
            Assert.AreEqual(new WarpRate(100, 1), _controller.CurrentRate);
        }

        // ----- Effective rate / mode-aware ceiling -----

        [Test]
        public void EffectiveRate_PhysX_CappedAt5x()
        {
            _controller.SetActiveVesselMode(PhysicsMode.PhysXActive);
            _controller.SetContinuousRate(1000);
            Assert.AreEqual(new WarpRate(5, 1), _controller.EffectiveRate,
                "PhysX-active mode caps effective rate at 5× even if requested is 1000×.");
        }

        [Test]
        public void EffectiveRate_Kepler_CappedAt10000()
        {
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetDiscreteLevel(100_000);
            Assert.AreEqual(new WarpRate(10_000, 1), _controller.EffectiveRate,
                "Kepler-rails mode caps effective rate at 10000×.");
        }

        [Test]
        public void SetActiveVesselMode_RecomputesEffective()
        {
            // Set rate first at Kepler-friendly value.
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetDiscreteLevel(1000);
            Assert.AreEqual(new WarpRate(1000, 1), _controller.EffectiveRate);

            // Switch to PhysX — effective should drop to 5x cap.
            _controller.SetActiveVesselMode(PhysicsMode.PhysXActive);
            Assert.AreEqual(new WarpRate(5, 1), _controller.EffectiveRate);

            // CurrentRate is unchanged by the mode switch — only EffectiveRate recomputes.
            Assert.AreEqual(new WarpRate(1000, 1), _controller.CurrentRate);

            // Switch back to Kepler — effective recomputes to requested 1000x.
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            Assert.AreEqual(new WarpRate(1000, 1), _controller.EffectiveRate);
        }

        // ----- ComputeAnalyticIterations -----

        [Test]
        public void ComputeAnalyticIterations_OneX_ReturnsOne()
        {
            // Default rate OneX, default mode PhysXActive (ceiling 5).
            // Empty event queue: ticksUntilNextEvent = int.MaxValue.
            Assert.AreEqual(1, _controller.ComputeAnalyticIterations(int.MaxValue, 100));
        }

        [Test]
        public void ComputeAnalyticIterations_KeplerAt100_ReturnsHundred()
        {
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetContinuousRate(100);
            Assert.AreEqual(100, _controller.ComputeAnalyticIterations(int.MaxValue, 100));
        }

        [Test]
        public void ComputeAnalyticIterations_RespectsEventDistance()
        {
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetDiscreteLevel(1000);
            // Event in 50 ticks; should advance 50 not 1000.
            Assert.AreEqual(50, _controller.ComputeAnalyticIterations(50, 100));
        }

        [Test]
        public void ComputeAnalyticIterations_PhysXActiveCappedAt5()
        {
            _controller.SetActiveVesselMode(PhysicsMode.PhysXActive);
            _controller.SetDiscreteLevel(100_000);
            // PhysX ceiling is 5; iterations cap at 5 regardless of requested rate.
            Assert.AreEqual(5, _controller.ComputeAnalyticIterations(int.MaxValue, 100));
        }

        // ----- Target tick -----

        [Test]
        public void SetTargetTick_AtOrBeforeCurrent_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _controller.SetTargetTick(100, 100));
            Assert.Throws<ArgumentException>(() => _controller.SetTargetTick(50, 100));
        }

        [Test]
        public void SetTargetTick_StopsExactlyAtTarget()
        {
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetTargetTick(tick: 5000, currentTickFromController: 100);
            Assert.AreEqual(5000L, _controller.TargetTick);

            // At currentTick=100 with TargetTick=5000, the warp rate is 10000x and
            // event queue is empty (int.MaxValue). Advancement should be capped to
            // ticks-to-target (4900) not the rate (10000).
            int advance = _controller.ComputeAnalyticIterations(int.MaxValue, 100);
            Assert.AreEqual(4900, advance,
                "ComputeAnalyticIterations should clamp to (target - current) when target would be reached.");

            // After computing the target-landing advancement, the target should be
            // cleared and the controller should be halting.
            Assert.IsNull(_controller.TargetTick, "TargetTick should clear after landing.");
            Assert.IsTrue(_controller.IsHalting, "IsHalting should be true after target reached.");
        }

        [Test]
        public void SetTargetTick_FiresOnWarpHaltedOnArrival()
        {
            WarpHaltInfo? received = null;
            _controller.OnWarpHalted += info => received = info;

            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetTargetTick(tick: 5000, currentTickFromController: 100);
            // Trigger the target-reached path.
            _controller.ComputeAnalyticIterations(int.MaxValue, 100);

            Assert.IsTrue(received.HasValue, "OnWarpHalted should fire when target reached.");
            Assert.AreEqual(WarpHaltReason.TargetTickReached, received.Value.HaltReason);
            Assert.AreEqual(5000L, received.Value.HaltTick);
            Assert.IsNull(received.Value.HaltingVesselId,
                "Target-reached halts are non-vessel; HaltingVesselId should be null.");
        }

        // ----- Pause / Resume -----

        [Test]
        public void Pause_SetsRateToPaused_FiresOnRateChanged()
        {
            WarpRate? received = null;
            _controller.OnRateChanged += r => received = r;

            _controller.Pause();

            Assert.AreEqual(WarpRate.Paused, _controller.CurrentRate);
            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(WarpRate.Paused, received.Value);
        }

        [Test]
        public void Pause_Resume_RoundTrip()
        {
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetContinuousRate(500);
            Assert.AreEqual(new WarpRate(500, 1), _controller.CurrentRate);

            _controller.Pause();
            Assert.AreEqual(WarpRate.Paused, _controller.CurrentRate);

            _controller.Resume();
            Assert.AreEqual(new WarpRate(500, 1), _controller.CurrentRate,
                "Resume should restore the rate cached at Pause.");
        }

        [Test]
        public void Pause_AlreadyPaused_IsNoOp()
        {
            // First Pause caches OneX as the previous rate.
            _controller.Pause();
            Assert.AreEqual(WarpRate.Paused, _controller.CurrentRate);

            // Second Pause should not overwrite the cached previous rate. Verify by
            // calling Resume and confirming we get back to OneX, not Paused.
            _controller.Pause();
            _controller.Resume();
            Assert.AreEqual(WarpRate.OneX, _controller.CurrentRate);
        }

        // ----- Halt registration -----

        [Test]
        public void RegisterHaltEvent_SetsIsHaltingAndStoresLastHaltInfo()
        {
            var info = new WarpHaltInfo(
                haltingVesselId: Guid.NewGuid(),
                haltReason: WarpHaltReason.Manual,
                haltTick: 1234L,
                diagnosticMessage: "test halt");

            _controller.RegisterHaltEvent(info);

            Assert.IsTrue(_controller.IsHalting);
            Assert.IsTrue(_controller.LastHaltInfo.HasValue);
            Assert.AreEqual(info, _controller.LastHaltInfo.Value);
        }

        [Test]
        public void RegisterHaltEvent_FiresOnWarpHalted()
        {
            WarpHaltInfo? received = null;
            _controller.OnWarpHalted += i => received = i;

            var info = new WarpHaltInfo(
                haltingVesselId: null,
                haltReason: WarpHaltReason.Manual,
                haltTick: 500L,
                diagnosticMessage: "test");
            _controller.RegisterHaltEvent(info);

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(info, received.Value);
        }

        [Test]
        public void ClearHalt_ClearsIsHalting_PreservesLastHaltInfo()
        {
            var info = new WarpHaltInfo(null, WarpHaltReason.Manual, 100, "test");
            _controller.RegisterHaltEvent(info);
            Assert.IsTrue(_controller.IsHalting);

            _controller.ClearHalt();

            Assert.IsFalse(_controller.IsHalting);
            Assert.IsTrue(_controller.LastHaltInfo.HasValue,
                "ClearHalt should preserve LastHaltInfo for UI display after acknowledgement.");
        }
    }
}
