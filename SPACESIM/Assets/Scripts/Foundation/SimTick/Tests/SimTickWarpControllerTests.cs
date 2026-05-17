using NUnit.Framework;
using SpaceSim.Foundation.SimTick;

namespace SpaceSim.Foundation.SimTick.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SimTickWarpController"/>. This is the pure-logic
    /// warp-rate state class; no Unity MonoBehaviour lifecycle is involved, so all
    /// behavior is exercisable in EditMode.
    /// </summary>
    public class SimTickWarpControllerTests
    {
        // ----- Defaults -----

        [Test]
        public void Defaults_AreSafe()
        {
            var w = new SimTickWarpController();
            Assert.AreEqual(1.0, w.RequestedWarpRate);
            Assert.AreEqual(PhysicsMode.PhysXActive, w.ActiveVesselMode);
            Assert.AreEqual(1.0, w.EffectiveWarpRate);
        }

        // ----- CeilingFor -----

        [Test]
        public void CeilingFor_PhysXActive_Is1x()
        {
            Assert.AreEqual(1.0, SimTickWarpController.CeilingFor(PhysicsMode.PhysXActive));
        }

        [Test]
        public void CeilingFor_KeplerRails_Is10000x()
        {
            Assert.AreEqual(10_000.0, SimTickWarpController.CeilingFor(PhysicsMode.KeplerRails));
        }

        [Test]
        public void CeilingFor_InterstellarCruise_Is100000x()
        {
            Assert.AreEqual(100_000.0, SimTickWarpController.CeilingFor(PhysicsMode.InterstellarCruise));
        }

        // ----- Requested warp clamping -----

        [Test]
        public void SetRequestedWarp_BelowOne_ClampsToOne()
        {
            var w = new SimTickWarpController();
            w.SetRequestedWarp(0.0);
            Assert.AreEqual(1.0, w.RequestedWarpRate);
            w.SetRequestedWarp(-5.0);
            Assert.AreEqual(1.0, w.RequestedWarpRate);
        }

        [Test]
        public void SetRequestedWarp_AtAndAboveOne_Preserved()
        {
            var w = new SimTickWarpController();
            w.SetRequestedWarp(1.0);
            Assert.AreEqual(1.0, w.RequestedWarpRate);
            w.SetRequestedWarp(50.0);
            Assert.AreEqual(50.0, w.RequestedWarpRate);
            w.SetRequestedWarp(50_000.0);
            Assert.AreEqual(50_000.0, w.RequestedWarpRate);
        }

        // ----- Effective warp = min(requested, ceiling) -----

        [Test]
        public void EffectiveWarp_PhysXActive_CappedAt1x()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.PhysXActive);
            w.SetRequestedWarp(1_000.0);
            Assert.AreEqual(1.0, w.EffectiveWarpRate,
                "PhysX-active mode caps effective warp at 1× even if requested is high.");
        }

        [Test]
        public void EffectiveWarp_KeplerRails_CappedAt10000x()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.KeplerRails);
            w.SetRequestedWarp(50_000.0);
            Assert.AreEqual(10_000.0, w.EffectiveWarpRate,
                "Kepler-rails mode caps effective warp at 10,000×.");
        }

        [Test]
        public void EffectiveWarp_InterstellarCruise_CappedAt100000x()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.InterstellarCruise);
            w.SetRequestedWarp(1_000_000.0);
            Assert.AreEqual(100_000.0, w.EffectiveWarpRate,
                "Interstellar-cruise mode caps effective warp at 100,000×.");
        }

        [Test]
        public void EffectiveWarp_BelowCeiling_PreservesRequested()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.KeplerRails);
            w.SetRequestedWarp(100.0);
            Assert.AreEqual(100.0, w.EffectiveWarpRate,
                "Below the mode ceiling, effective warp equals requested.");
        }

        // ----- Mode change recomputes effective -----

        [Test]
        public void SetActiveVesselMode_RecomputesEffective()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.KeplerRails);
            w.SetRequestedWarp(5_000.0);
            Assert.AreEqual(5_000.0, w.EffectiveWarpRate);

            // Switching to PhysX-active drops effective to 1.0 even with requested still at 5000.
            w.SetActiveVesselMode(PhysicsMode.PhysXActive);
            Assert.AreEqual(1.0, w.EffectiveWarpRate);
            Assert.AreEqual(5_000.0, w.RequestedWarpRate,
                "RequestedWarpRate is unchanged by mode switch; only EffectiveWarpRate recomputes.");

            // Switch back: effective recomputes to requested (5000), still under the 10000 ceiling.
            w.SetActiveVesselMode(PhysicsMode.KeplerRails);
            Assert.AreEqual(5_000.0, w.EffectiveWarpRate);
        }

        // ----- ComputeAnalyticIterations -----

        [Test]
        public void ComputeAnalyticIterations_ClampsToAtLeastOne()
        {
            var w = new SimTickWarpController();
            // EffectiveWarpRate is 1.0 by default; floor is 1.
            Assert.AreEqual(1, w.ComputeAnalyticIterations(int.MaxValue));
            // ticksUntilNextEvent < 1 is clamped to 1.
            Assert.AreEqual(1, w.ComputeAnalyticIterations(0));
            Assert.AreEqual(1, w.ComputeAnalyticIterations(-100));
        }

        [Test]
        public void ComputeAnalyticIterations_WarpRateDeterminesCountWhenEventQueueEmpty()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.KeplerRails);
            w.SetRequestedWarp(100.0);
            Assert.AreEqual(100, w.ComputeAnalyticIterations(int.MaxValue));
        }

        [Test]
        public void ComputeAnalyticIterations_EventDistanceCapsIterations()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.KeplerRails);
            w.SetRequestedWarp(1_000.0);
            // Event in 50 ticks: cycle should advance 50, not 1000.
            Assert.AreEqual(50, w.ComputeAnalyticIterations(50));
        }

        [Test]
        public void ComputeAnalyticIterations_FractionalWarpFloors()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.KeplerRails);
            w.SetRequestedWarp(100.7);
            Assert.AreEqual(100, w.ComputeAnalyticIterations(int.MaxValue),
                "Fractional warp is floored to integer iteration count.");
        }

        [Test]
        public void ComputeAnalyticIterations_PhysXActiveAlwaysReturnsOne()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.PhysXActive);
            // Even with very high requested warp, PhysX-active ceiling is 1.0 so iterations = 1.
            w.SetRequestedWarp(10_000_000.0);
            Assert.AreEqual(1, w.ComputeAnalyticIterations(int.MaxValue));
        }

        [Test]
        public void ComputeAnalyticIterations_InterstellarCruise_HighWarp_HighIterations()
        {
            var w = new SimTickWarpController();
            w.SetActiveVesselMode(PhysicsMode.InterstellarCruise);
            w.SetRequestedWarp(100_000.0);
            Assert.AreEqual(100_000, w.ComputeAnalyticIterations(int.MaxValue));
        }
    }
}
