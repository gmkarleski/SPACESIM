using System;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;
using SpaceSim.Foundation.Vessels;
using Unity.Mathematics;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using static SpaceSim.Foundation.Vessels.Tests.VesselTestHelpers;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="VesselTransitionDriver"/>. Driver subscribes to
    /// <see cref="SimTickController.TickAdvanced"/> and dispatches PhysX-active ↔
    /// Kepler-rails mode transitions per the §3.1 trigger conditions evaluated by
    /// <see cref="Vessel.EvaluateTransitionTriggers"/>.
    ///
    /// <para>
    /// Migrated from <c>VesselTests.cs</c> as part of the operational commit that
    /// split <c>VesselTests.cs</c> at the natural driver-test seam. The six tests
    /// in this file were section 14 of the pre-split <c>VesselTests.cs</c>;
    /// content is verbatim (no semantic changes), but helper calls now resolve via
    /// <c>using static VesselTestHelpers;</c> rather than against the private
    /// instance helpers of the pre-split file. Stub <see cref="IActiveVessel"/>
    /// implementations (<see cref="StubActiveVessel"/>, <see cref="ThrowingActiveVessel"/>)
    /// promoted from <c>VesselTests</c>'s private nested classes to top-level
    /// internal types in <see cref="VesselTestHelpers"/> alongside the other
    /// cross-cutting helpers.
    /// </para>
    ///
    /// <para>
    /// <strong>WHY ITS OWN FILE:</strong> matches the per-driver test-file
    /// pattern established by <see cref="VesselEventPredictionDriverTests"/>
    /// (IVessel commit) and <see cref="VesselSoiRerootingDriverTests"/> (this
    /// commit). VesselTests.cs is the bigger-than-guideline file the audit
    /// flagged for splitting; driver tests are the natural seam.
    /// </para>
    /// </summary>
    public class VesselTransitionDriverTests
    {
        private const double EarthMassKg = 5.972e24;
        private static readonly double EarthMu = CoordinateMath.G * EarthMassKg;

        private GameObject _vesselGo;
        private GameObject _bodyGo;
        private GameObject _simTickGo;

        private Vessel _vessel;
        private ReferenceBody _body;

        [SetUp]
        public void SetUp()
        {
            // Defensive: clear shared static state before each test so registry
            // leaks from other test classes don't poison this one. Matches the
            // pattern from VesselTests.cs SetUp.
            VesselRegistry.ClearForTesting();
            BodyRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();
            VesselTransitionDriver.Shutdown();
            VesselSoiRerootingDriver.Shutdown();
            VesselEventPredictionDriver.Shutdown();

            // ReferenceBody — the SetUp baseline body. Point-mass default
            // (surfaceRadiusMeters = 1.0 via reflection carve-out) so the
            // SurfaceImpactPredictor doesn't fire on standard LEO test orbits.
            // Mirrors VesselTests.cs SetUp's site-1 carve-out reasoning.
            _bodyGo = new GameObject("TestReferenceBody");
            _body = _bodyGo.AddComponent<ReferenceBody>();
            {
                var surfaceField = typeof(ReferenceBody).GetField(
                    "surfaceRadiusMeters",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                surfaceField.SetValue(_body, 1.0);
            }

            // Vessel GameObject (no components yet beyond the Vessel itself).
            _vesselGo = new GameObject("TestVessel");
            _vessel = _vesselGo.AddComponent<Vessel>();

            // _simTickGo is null at SetUp time; each test that needs the controller
            // constructs it via SetUpDriverWithStubActiveVessel below.
            _simTickGo = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_vesselGo != null) UnityObject.DestroyImmediate(_vesselGo);
            if (_bodyGo != null) UnityObject.DestroyImmediate(_bodyGo);
            if (_simTickGo != null) UnityObject.DestroyImmediate(_simTickGo);

            VesselRegistry.ClearForTesting();
            BodyRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();
            VesselTransitionDriver.Shutdown();
            VesselSoiRerootingDriver.Shutdown();
            VesselEventPredictionDriver.Shutdown();
        }

        // ----- Local helper (single-use, stays local per the hybrid extraction strategy) -----

        /// <summary>
        /// Helper: construct a SimTickController, register an active vessel stub, and
        /// initialize the driver. Returns the controller for tests that need direct
        /// access (e.g., to fire TickAdvanced manually).
        /// </summary>
        private SimTickController SetUpDriverWithStubActiveVessel(IActiveVessel activeVessel)
        {
            _simTickGo = new GameObject("TestSimTick");
            var controller = _simTickGo.AddComponent<SimTickController>();
            SimTickController.SetInstanceForTesting(controller);
            controller.SetActiveVessel(activeVessel);
            VesselTransitionDriver.Initialize();
            return controller;
        }

        // ----- Tests (6, migrated verbatim from VesselTests.cs section 14) -----

        [Test]
        public void TransitionDriver_DisabledByDefault_DoesNothing()
        {
            // Default state: Enabled = false. Firing TickAdvanced (via direct
            // OnTickAdvanced invocation, since EditMode has no FixedUpdate cycle)
            // should be a complete no-op — no evaluation, no transition, no counter increment.
            var stubActive = new StubActiveVessel(new WorldPosition(0, 0, 0));
            SetUpDriverWithStubActiveVessel(stubActive);

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            // In EditMode, OnEnable doesn't fire, so the vessel isn't auto-registered.
            // Initialize itself calls VesselRegistry.RegisterVesselSafe inside the
            // isActiveAndEnabled guard which IS true in EditMode for newly added
            // components, so the registration does happen — verify and proceed.
            Assert.AreEqual(1, VesselRegistry.VesselCount,
                "Sanity: vessel should be registered after Initialize");

            Assert.IsFalse(VesselTransitionDriver.Enabled,
                "Sanity: driver should be disabled by default");

            VesselTransitionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.AreEqual(0, VesselTransitionDriver.EvaluationCount,
                "Disabled driver should not evaluate vessels");
            Assert.AreEqual(0, VesselTransitionDriver.TransitionCount,
                "Disabled driver should not transition vessels");
            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode,
                "Disabled driver should not change vessel mode");
        }

        [Test]
        public void TransitionDriver_WhenEnabled_EvaluatesVesselsOnTickAdvanced()
        {
            // With Enabled = true, OnTickAdvanced iterates registered vessels and calls
            // EvaluateTransitionTriggers on each. We register one vessel within proximity
            // (so the evaluation returns Stay rather than firing a transition); the
            // diagnostic counter should still increment to confirm the call happened.
            var stubActive = new StubActiveVessel(new WorldPosition(0, 0, 0));
            SetUpDriverWithStubActiveVessel(stubActive);

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3(10_000f, 0f, 0f);  // within proximity

            VesselTransitionDriver.Enabled = true;
            VesselTransitionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.AreEqual(1, VesselTransitionDriver.EvaluationCount,
                "Enabled driver should evaluate 1 vessel (the one we registered)");
            Assert.AreEqual(0, VesselTransitionDriver.TransitionCount,
                "Within-proximity vessel should not transition");
            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
        }

        [Test]
        public void TransitionDriver_WhenEnabled_FiresTransitionForVesselBeyondProximity()
        {
            // The end-to-end happy path: enabled driver iterates vessels, finds one
            // beyond proximity with clean state, fires TransitionToKeplerRails.
            // Suppress the expected diagnostic log so the test runner doesn't choke.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log,
                new System.Text.RegularExpressions.Regex(".*transitioning PhysXActive → KeplerRails.*"));

            var stubActive = new StubActiveVessel(new WorldPosition(0, 0, 0));
            SetUpDriverWithStubActiveVessel(stubActive);

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);

            VesselTransitionDriver.Enabled = true;
            VesselTransitionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.AreEqual(1, VesselTransitionDriver.EvaluationCount);
            Assert.AreEqual(1, VesselTransitionDriver.TransitionCount,
                "Beyond-proximity clean-state vessel should fire a transition");
            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode,
                "Vessel mode should be KeplerRails after driver-invoked transition");
            Assert.IsNotNull(_vessel.State.KeplerState,
                "KeplerState should be populated after transition");
        }

        [Test]
        public void TransitionDriver_DiagnosticCountersIncrementCorrectly()
        {
            // Two-tick scenario: vessel is within proximity → evaluates but doesn't
            // transition (counter: 1, 0). On second tick, vessel teleported beyond
            // proximity → evaluates AND transitions (counter: 2, 1).
            UnityEngine.TestTools.LogAssert.Expect(LogType.Log,
                new System.Text.RegularExpressions.Regex(".*transitioning PhysXActive → KeplerRails.*"));

            var stubActive = new StubActiveVessel(new WorldPosition(0, 0, 0));
            SetUpDriverWithStubActiveVessel(stubActive);

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3(10_000f, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);

            VesselTransitionDriver.Enabled = true;

            // Tick 1: within proximity → eval but no transition.
            VesselTransitionDriver.OnTickAdvanced(tickNumber: 1);
            Assert.AreEqual(1, VesselTransitionDriver.EvaluationCount);
            Assert.AreEqual(0, VesselTransitionDriver.TransitionCount);

            // Teleport to LEO (beyond proximity) and apply circular velocity so the
            // resulting Kepler-rails state is well-defined.
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);

            // Tick 2: beyond proximity → eval AND transition.
            VesselTransitionDriver.OnTickAdvanced(tickNumber: 2);
            Assert.AreEqual(2, VesselTransitionDriver.EvaluationCount,
                "EvaluationCount should be 2 after two ticks");
            Assert.AreEqual(1, VesselTransitionDriver.TransitionCount,
                "TransitionCount should be 1 (one transition fired on tick 2)");
        }

        [Test]
        public void TransitionDriver_VesselThatThrowsDuringEvaluation_LoopContinues()
        {
            // Verify the driver's per-vessel try/catch isolates failures: a throwing
            // ActiveVessel (whose GetWorldPosition raises an exception) causes every
            // vessel's evaluation to throw during the proximity-distance check. The
            // driver should log errors for each but continue iterating. EvaluationCount
            // increments BEFORE the try-block per the driver implementation, so both
            // vessels should contribute to the counter even though both throw.
            //
            // Two vessels registered: _vessel from SetUp, plus a second one we
            // construct here. Both should be evaluated; both should throw; both error
            // logs should fire.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*threw during EvaluateTransitionTriggers.*"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*threw during EvaluateTransitionTriggers.*"));

            var throwingActive = new ThrowingActiveVessel();
            SetUpDriverWithStubActiveVessel(throwingActive);

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);

            // Add a second vessel (will be auto-registered via Initialize).
            var secondVesselGo = new GameObject("SecondVessel");
            var secondVessel = secondVesselGo.AddComponent<Vessel>();
            secondVessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            try
            {
                Assert.AreEqual(2, VesselRegistry.VesselCount, "Sanity: 2 vessels registered");

                VesselTransitionDriver.Enabled = true;
                VesselTransitionDriver.OnTickAdvanced(tickNumber: 1);

                Assert.AreEqual(2, VesselTransitionDriver.EvaluationCount,
                    "Both vessels should be attempted (counter increments before try-block)");
                Assert.AreEqual(0, VesselTransitionDriver.TransitionCount,
                    "Both vessels threw → no transitions");
                Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode,
                    "First vessel mode unchanged after throw");
                Assert.AreEqual(PhysicsMode.PhysXActive, secondVessel.Mode,
                    "Second vessel mode unchanged after throw");
            }
            finally
            {
                UnityObject.DestroyImmediate(secondVesselGo);
            }
        }

        [Test]
        public void TransitionDriver_ActiveVesselNull_SkipsEvaluation()
        {
            // No SetActiveVessel call; the controller's ActiveVessel stays null. The
            // driver should warn-once and skip without evaluating any vessel.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*ActiveVessel is null.*"));

            _simTickGo = new GameObject("TestSimTick");
            var controller = _simTickGo.AddComponent<SimTickController>();
            SimTickController.SetInstanceForTesting(controller);
            // Intentionally NOT calling controller.SetActiveVessel(...)
            VesselTransitionDriver.Initialize();

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);

            VesselTransitionDriver.Enabled = true;
            VesselTransitionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.AreEqual(0, VesselTransitionDriver.EvaluationCount,
                "Null ActiveVessel → skip evaluation entirely");
            Assert.AreEqual(0, VesselTransitionDriver.TransitionCount);
            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
        }
    }
}
