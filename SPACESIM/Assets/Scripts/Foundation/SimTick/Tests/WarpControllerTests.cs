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

        // ----- Determinism: multi-call accumulation -----

        [Test]
        public void ComputeAnalyticIterations_AccumulatesDeterministically_AcrossManyCalls()
        {
            // 100 sequential calls at 100,000× (Cruise mode allows the full rate
            // without ceiling clamping). The rational-rate / integer-arithmetic
            // guarantee means total advancement is EXACTLY 100 × 100,000 = 10,000,000
            // with no drift accumulated across calls.
            _controller.SetActiveVesselMode(PhysicsMode.InterstellarCruise);
            _controller.SetDiscreteLevel(100_000);

            long total = 0;
            for (int i = 0; i < 100; i++)
            {
                total += _controller.ComputeAnalyticIterations(int.MaxValue, total);
            }

            Assert.AreEqual(10_000_000L, total,
                "100 calls at 100,000× should advance exactly 10,000,000 ticks with no drift.");
        }

        [Test]
        public void ComputeAnalyticIterations_ModeSwitch_BetweenCalls_NoDrift()
        {
            // Three phases. Mode changes between phases don't reset the
            // _pendingNumerator (SetActiveVesselMode doesn't touch the rational
            // accumulator state) so this also verifies mode-switch atomicity.
            //
            // Phase 1: 10 calls at KeplerRails / 1000× → +10,000.
            // Phase 2: 10 calls at PhysXActive  (effective drops to 5×) → +50.
            // Phase 3: 10 calls at KeplerRails / 1000× → +10,000.
            // Cumulative expected: 20,050.
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetDiscreteLevel(1000);

            long total = 0;
            for (int i = 0; i < 10; i++)
                total += _controller.ComputeAnalyticIterations(int.MaxValue, total);
            Assert.AreEqual(10_000L, total,
                "After 10 KeplerRails/1000× calls total should be 10,000.");

            _controller.SetActiveVesselMode(PhysicsMode.PhysXActive);
            for (int i = 0; i < 10; i++)
                total += _controller.ComputeAnalyticIterations(int.MaxValue, total);
            Assert.AreEqual(10_050L, total,
                "After 10 PhysXActive-capped (5×) calls total should be 10,050.");

            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            for (int i = 0; i < 10; i++)
                total += _controller.ComputeAnalyticIterations(int.MaxValue, total);
            Assert.AreEqual(20_050L, total,
                "After 10 more KeplerRails/1000× calls total should be 20,050.");
        }

        [Test]
        public void ComputeAnalyticIterations_WhileHalting_ReturnsOne()
        {
            // Pre-halt sanity: KeplerRails @ 10000× returns the full 10,000.
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetDiscreteLevel(10_000);
            Assert.AreEqual(10_000, _controller.ComputeAnalyticIterations(int.MaxValue, 100),
                "Pre-halt sanity: KeplerRails @ 10000× should return 10,000 iterations.");

            // Register a halt; the "always at least 1" floor kicks in regardless
            // of rate. Per WarpController.cs line 401 the impl returns 1, not 0;
            // the user-facing "Paused" signal lives in the RateDisplay text, not
            // in zero advancement (the cycle still runs its non-iterated steps).
            _controller.RegisterHaltEvent(new WarpHaltInfo(
                haltingVesselId: null,
                haltReason: WarpHaltReason.Manual,
                haltTick: 100L,
                diagnosticMessage: "halt test"));

            Assert.AreEqual(1, _controller.ComputeAnalyticIterations(int.MaxValue, 100),
                "During halt the always-at-least-1 floor returns 1 regardless of rate.");
        }

        [Test]
        public void ComputeAnalyticIterations_WhilePaused_ReturnsOne()
        {
            // Paused state hits the same "always at least 1" floor as halting
            // (per WarpController.cs line 402). Dual-coded behavior; dual-tested.
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetDiscreteLevel(10_000);
            Assert.AreEqual(10_000, _controller.ComputeAnalyticIterations(int.MaxValue, 100),
                "Pre-pause sanity: KeplerRails @ 10000× should return 10,000 iterations.");

            _controller.Pause();
            Assert.AreEqual(1, _controller.ComputeAnalyticIterations(int.MaxValue, 100),
                "While paused the always-at-least-1 floor returns 1 regardless of rate.");
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

        [Test]
        public void SetTargetTick_AfterReached_NewTargetWorks()
        {
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);

            // First target: 100 → 5000. Reaching it should clear TargetTick and
            // set IsHalting (verified by the StopsExactlyAtTarget test); we
            // reproduce the arrival here to set up the post-arrival state.
            _controller.SetTargetTick(tick: 5000, currentTickFromController: 100);
            _controller.ComputeAnalyticIterations(int.MaxValue, 100);
            Assert.IsNull(_controller.TargetTick, "First target should clear after landing.");
            Assert.IsTrue(_controller.IsHalting, "First target arrival should set IsHalting.");

            // Player acknowledges via ClearHalt; controller is ready for a new target.
            _controller.ClearHalt();
            Assert.IsFalse(_controller.IsHalting, "ClearHalt should reset halting.");

            // Second target latches and reaches just like the first.
            _controller.SetTargetTick(tick: 8000, currentTickFromController: 5000);
            Assert.AreEqual(8000L, _controller.TargetTick,
                "Second target should latch after the first cleared.");

            _controller.ComputeAnalyticIterations(int.MaxValue, 5000);
            Assert.IsNull(_controller.TargetTick, "Second target should clear after landing.");
            Assert.IsTrue(_controller.IsHalting, "Second target arrival should set IsHalting.");
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

        [Test]
        public void Resume_WithoutPriorPause_IsNoOp()
        {
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetContinuousRate(500);
            int eventFireCount = 0;
            _controller.OnRateChanged += _ => eventFireCount++;

            // Resume without ever calling Pause. The impl early-returns when
            // _currentRate.IsPaused is false (lines 318-330 of WarpController.cs),
            // so this is a defensive no-op: rate unchanged, no event fires.
            _controller.Resume();

            Assert.AreEqual(new WarpRate(500, 1), _controller.CurrentRate,
                "Resume without prior Pause should leave CurrentRate untouched.");
            Assert.AreEqual(0, eventFireCount,
                "Resume without prior Pause should not fire OnRateChanged.");
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

        [Test]
        public void ClearHalt_ResumesAdvancement_AtCurrentRate()
        {
            // Pre-halt sanity: KeplerRails @ 100× returns 100 iterations.
            _controller.SetActiveVesselMode(PhysicsMode.KeplerRails);
            _controller.SetDiscreteLevel(100);
            Assert.AreEqual(100, _controller.ComputeAnalyticIterations(int.MaxValue, 100),
                "Pre-halt sanity: KeplerRails @ 100× should return 100 iterations.");

            // Halt: the always-at-least-1 floor reduces advancement to 1
            // (covered in detail by _WhileHalting_ReturnsOne).
            _controller.RegisterHaltEvent(new WarpHaltInfo(
                haltingVesselId: null,
                haltReason: WarpHaltReason.Manual,
                haltTick: 100L,
                diagnosticMessage: "halt test"));
            Assert.AreEqual(1, _controller.ComputeAnalyticIterations(int.MaxValue, 100),
                "Sanity: halting yields floor of 1.");

            // ClearHalt should restore normal advancement to the current effective rate.
            _controller.ClearHalt();
            Assert.AreEqual(100, _controller.ComputeAnalyticIterations(int.MaxValue, 100),
                "After ClearHalt advancement should return to the current effective rate (100×).");
        }
    }
}
