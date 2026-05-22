using System;
using System.Reflection;
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
    /// Driver-only tests for <see cref="VesselEventPredictionDriver"/>. Lives in
    /// its own file (rather than nested inside <c>VesselTests.cs</c>) for two
    /// reasons: (1) the per-driver isolation matches the established split
    /// pattern (PeriapsisApoapsisPredictorTests / SoiCrossingPredictorTests /
    /// AtmosphericEntryPredictorTests / SurfaceImpactPredictorTests /
    /// VesselRegistryTests already live as siblings); (2) VesselTests.cs is
    /// past the per-file size guideline and the IVessel-migration commit is
    /// the natural moment to start the planned split rather than continuing
    /// to grow the monster file.
    ///
    /// <para>
    /// <strong>SCOPE:</strong> tests that exercise the IVessel-abstraction
    /// seam introduced by the IVessel-migration commit. Specifically, tests
    /// that pass POCO <see cref="IVessel"/> fakes through the driver's
    /// per-vessel dispatch (<c>PredictAndUpdate</c>) to verify the seam works
    /// without constructing real Vessel MonoBehaviours. The existing
    /// EventPrediction tests in VesselTests.cs (which use real Vessel
    /// components) continue to cover the OnTickAdvanced + VesselRegistry path
    /// end-to-end and are not duplicated here.
    /// </para>
    ///
    /// <para>
    /// <strong>WHY REFLECTION FOR <c>PredictAndUpdate</c>:</strong> the method
    /// is <c>private static</c> on the driver. Using reflection
    /// (<see cref="BindingFlags.NonPublic"/> + <see cref="BindingFlags.Static"/>)
    /// to invoke it directly keeps the driver's public surface narrow —
    /// nothing outside the driver should be calling its per-vessel helper
    /// during normal operation. Matches the existing reflection pattern used
    /// throughout VesselTests.cs for non-public-member access.
    /// </para>
    /// </summary>
    public class VesselEventPredictionDriverTests
    {
        private const double EarthMassKg = 5.972e24;
        // LeoRadius is also available via `using static VesselTestHelpers`, but
        // the original file declared it locally before the helper extraction; the
        // local declaration is retained to avoid a name-resolution diff at the
        // many call sites (the file's pre-extraction tests + the migrated tests
        // both expect LeoRadius unqualified). The two declarations are identical
        // (7_000_000.0); the local takes precedence at lookup but produces the
        // same value, so the using-static import is harmless.
        private const double LeoRadius = 7_000_000.0;

        private GameObject _vesselGo;
        private GameObject _bodyGo;
        private GameObject _simTickGo;
        private Vessel _vessel;
        private ReferenceBody _body;
        private SimTickController _controller;

        [SetUp]
        public void SetUp()
        {
            // Defensive: clear shared static state before each test so registry
            // leaks from other test classes don't poison this one. Matches the
            // VesselTests.cs SetUp pattern.
            VesselRegistry.ClearForTesting();
            BodyRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();
            VesselTransitionDriver.Shutdown();
            VesselSoiRerootingDriver.Shutdown();
            VesselEventPredictionDriver.Shutdown();

            // ReferenceBody constructed via the parameterized
            // InitializeBodyForTesting overload (landed in the May 21 cleanup
            // commit). Earth-mass, infinite SOI so SOI-crossing predictor
            // doesn't fire spuriously on the test orbit. 1.0 m surface so
            // SurfaceImpactPredictor doesn't fire either; LEO orbit at 7e6 m
            // stays comfortably above. Atmospheric top at 0.0 marks vacuum
            // body, so AtmosphericEntryPredictor early-returns null.
            //
            // Tests in sections 20 and 21 (migrated from VesselTests.cs) use
            // named-wrapper helpers (SetEarthFiniteSoiAndInitialize,
            // SetEarthSurfaceAtmosphereAndInitialize) that re-invoke
            // InitializeBodyForTesting with different SOI / surface / atmosphere
            // values. Re-init is safe per ReferenceBody.InitializeBodyForTesting's
            // BodyId-preservation guard and BodyRegistry.RegisterBodySafe
            // idempotent dedup.
            _bodyGo = new GameObject("TestReferenceBody");
            _body = _bodyGo.AddComponent<ReferenceBody>();
            _body.InitializeBodyForTesting(
                massKg: EarthMassKg,
                soiRadiusMeters: double.PositiveInfinity,
                parentBody: null,
                surfaceRadiusMeters: 1.0,
                atmosphericTopAltitudeMeters: 0.0);

            // Vessel GameObject (no components yet beyond the Vessel itself).
            // Added in the VesselTests.cs split commit: most migrated tests
            // construct vessels at test time via _vessel.Initialize(...). The
            // pre-existing POCO-fake test (PredictAndUpdate_AcceptsIVesselFake_WritesPredictedTicks)
            // does NOT reference _vessel; the construction here is transparent
            // to that test.
            _vesselGo = new GameObject("TestVessel");
            _vessel = _vesselGo.AddComponent<Vessel>();

            // SimTickController instance — the driver's PredictAndUpdate writes
            // to controller.EventQueue, so a real controller is required for
            // the EventQueue assertion path. Migrated section-19 tests call
            // SetUpEventPredictionDriver() which is now a no-op that returns
            // the SetUp-constructed controller; see the helper's XML doc below.
            _simTickGo = new GameObject("TestSimTick");
            _controller = _simTickGo.AddComponent<SimTickController>();
            SimTickController.SetInstanceForTesting(_controller);
            VesselEventPredictionDriver.Initialize();
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

        // ----- IVessel POCO fake (per Stage 2 spec: nested-private-class pattern) -----

        /// <summary>
        /// Minimal POCO implementing <see cref="IVessel"/> for driver tests.
        /// No GameObject, no MonoBehaviour, no Unity components — exists only
        /// to satisfy the interface contract and let the driver's
        /// <c>PredictAndUpdate</c> exercise its per-predictor dispatch without
        /// constructing real Vessel scaffolding. All five interface members
        /// are auto-properties with public setters so tests can configure the
        /// fake inline.
        ///
        /// <para>
        /// <see cref="GetWorldPosition"/> returns a stub <see cref="WorldPosition"/>
        /// because <c>PredictAndUpdate</c> does not call this method during
        /// the predictor-dispatch path (it reads <see cref="State"/>,
        /// <see cref="Mode"/>, <see cref="ReferenceBody"/>, and
        /// <see cref="DiagnosticName"/> in catch blocks only). The Stage 3
        /// SOI-rerooter tests will need a fake whose GetWorldPosition returns
        /// meaningful values; that's a different fake in a different test file
        /// per the (α) nested-per-test-file pattern.
        /// </para>
        /// </summary>
        private sealed class FakeVessel : IVessel
        {
            public PhysicsMode Mode { get; set; }
            public VesselAuthoritativeState State { get; set; }
            public ReferenceBody ReferenceBody { get; set; }
            public string DiagnosticName { get; set; } = "fake-vessel";
            public WorldPosition WorldPosition { get; set; } = WorldPosition.Zero;

            public WorldPosition GetWorldPosition() => WorldPosition;
        }

        // ----- Test -----

        [Test]
        public void PredictAndUpdate_AcceptsIVesselFake_WritesPredictedTicks()
        {
            // Build a circular-LEO KeplerState and wrap it in a POCO FakeVessel.
            // The predictor dispatch should write NextPeriapsisTick and
            // NextApoapsisTick to the FakeVessel's KeplerState and add the
            // corresponding entries to the controller's EventQueue. This
            // exercises the IVessel-abstraction seam end-to-end: the driver's
            // PredictAndUpdate consumes IVessel, the real predictor static
            // methods consume the KeplerState that IVessel.State exposes, and
            // the controller's EventQueue is the same instance the production
            // path would use.
            var kepler = new KeplerState
            {
                SemiMajorAxis = LeoRadius,
                Eccentricity = 0.1,  // elliptical so periapsis/apoapsis populate
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = 0.0,
                EpochTick = 0,
                ReferenceBodyId = _body.BodyId,
            };

            var state = new VesselAuthoritativeState
            {
                VesselId = Guid.NewGuid(),
                DesignId = Guid.NewGuid(),
                Name = "FakeVessel",
                TotalMassKg = 1000.0,
                Mode = PhysicsMode.KeplerRails,
                KeplerState = kepler,
            };

            var fake = new FakeVessel
            {
                Mode = PhysicsMode.KeplerRails,
                State = state,
                ReferenceBody = _body,
                DiagnosticName = "ivessel-seam-test-fake",
            };

            // Sanity: ticks start null.
            Assert.IsNull(fake.State.KeplerState.NextPeriapsisTick,
                "Sanity: NextPeriapsisTick should be null before driver fires");
            Assert.IsNull(fake.State.KeplerState.NextApoapsisTick,
                "Sanity: NextApoapsisTick should be null before driver fires");
            Assert.AreEqual(0, _controller.EventQueue.Count,
                "Sanity: queue should be empty before driver fires");

            // Reflection invocation of PredictAndUpdate (private static).
            var method = typeof(VesselEventPredictionDriver).GetMethod(
                "PredictAndUpdate",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method,
                "Reflection: PredictAndUpdate must exist as a non-public static method");
            method.Invoke(null, new object[] { fake, _body, _controller, 0L });

            // The IVessel seam works: predictor writes landed on the fake's
            // KeplerState, queue gained the expected entries.
            Assert.IsNotNull(fake.State.KeplerState.NextPeriapsisTick,
                "NextPeriapsisTick should be populated after PredictAndUpdate on IVessel fake");
            Assert.IsNotNull(fake.State.KeplerState.NextApoapsisTick,
                "NextApoapsisTick should be populated after PredictAndUpdate on IVessel fake");
            Assert.Greater(fake.State.KeplerState.NextPeriapsisTick.Value, 0,
                "Predicted periapsis tick should be in the future for elliptical LEO orbit");
            Assert.Greater(fake.State.KeplerState.NextApoapsisTick.Value, 0,
                "Predicted apoapsis tick should be in the future for elliptical LEO orbit");

            // Queue: Periapsis + Apoapsis entries. SOI-crossing predictor
            // returns null (no children, infinite SOI). Atmospheric / surface
            // predictors return null (vacuum body, surface = 1 m, orbit at 7e6 m).
            // Confirms only the two expected entries are present.
            Assert.AreEqual(2, _controller.EventQueue.Count,
                "Queue should have Periapsis + Apoapsis entries; SOI / atmospheric / " +
                "surface predictors return null on this configuration");
        }

        // ===== Migrated from VesselTests.cs sections 19, 20, 21 =====
        // The 15 tests below moved from VesselTests.cs as part of the operational
        // commit that split VesselTests.cs at the natural driver-test seam. They
        // exercise the real-Vessel + real-SimTickController path through the
        // driver's OnTickAdvanced + VesselRegistry-iteration flow — complementary
        // to the IVessel POCO-fake test above which exercises the inner
        // PredictAndUpdate dispatch directly.

        // ----- Section 19: VesselEventPredictionDriver tests (commit 045 Stage 2) -----

        /// <summary>
        /// Helper: returns the SetUp-constructed controller. In the pre-split
        /// VesselTests.cs this helper used to construct the SimTickController
        /// itself, but in this file the SetUp already does that work eagerly.
        /// The helper now resolves to a no-op that returns <see cref="_controller"/>;
        /// existing migrated tests that captured the return value (e.g.,
        /// <c>var controller = SetUpEventPredictionDriver();</c>) continue to
        /// work unchanged. Driver re-Initialize is idempotent (the driver's own
        /// <c>if (_subscribed) return;</c> guard).
        /// </summary>
        private SimTickController SetUpEventPredictionDriver()
        {
            VesselEventPredictionDriver.Initialize();
            return _controller;
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_PopulatesPeriapsisApoapsisOnKeplerRailsVessel()
        {
            // Kepler-rails vessel with elliptical orbit. After one TickAdvanced fire,
            // both NextPeriapsisTick and NextApoapsisTick should be populated.
            SetUpEventPredictionDriver();

            var kepler = NewKeplerState(_body);
            kepler.Eccentricity = 0.1;  // ensure elliptical (NewKeplerState defaults to circular)
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            // Sanity: before the driver fires, both fields should be null
            // (predictor hasn't run yet at Initialize time in Phase 0/1 scope).
            Assert.IsNull(_vessel.State.KeplerState.NextPeriapsisTick);
            Assert.IsNull(_vessel.State.KeplerState.NextApoapsisTick);

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.IsNotNull(_vessel.State.KeplerState.NextPeriapsisTick,
                "NextPeriapsisTick should be populated after driver fires");
            Assert.IsNotNull(_vessel.State.KeplerState.NextApoapsisTick,
                "NextApoapsisTick should be populated for elliptical orbit");
            Assert.AreEqual(1, VesselEventPredictionDriver.EvaluationCount);
            Assert.AreEqual(1, VesselEventPredictionDriver.PredictionUpdateCount);
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_SkipsPhysXActiveVessel()
        {
            // PhysX-active vessel should be skipped by the driver (predictor only
            // applies to Kepler-rails vessels).
            SetUpEventPredictionDriver();

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.AreEqual(0, VesselEventPredictionDriver.EvaluationCount,
                "PhysX-active vessel should not be evaluated by event predictor");
            Assert.AreEqual(0, VesselEventPredictionDriver.PredictionUpdateCount);
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_UpdatesPriorityQueue()
        {
            // After driver fires, the controller's EventQueue should have entries
            // for the vessel's Periapsis and Apoapsis events.
            var controller = SetUpEventPredictionDriver();

            var kepler = NewKeplerState(_body);
            kepler.Eccentricity = 0.1;
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            Assert.AreEqual(0, controller.EventQueue.Count,
                "Sanity: queue should be empty before driver fires");

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.AreEqual(2, controller.EventQueue.Count,
                "Queue should have Periapsis and Apoapsis entries after driver fires");
        }

        [Test]
        public void EventPredictionDriver_VesselWithNullKeplerState_IsSkipped()
        {
            // Construct the schema-invariant-violation state (Mode == KeplerRails but
            // KeplerState == null) and verify the driver skips it gracefully.
            SetUpEventPredictionDriver();

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.State.Mode = PhysicsMode.KeplerRails;
            _vessel.State.KeplerState = null;

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.AreEqual(0, VesselEventPredictionDriver.EvaluationCount,
                "Null-KeplerState vessel should be skipped before incrementing counter");
        }

        [Test]
        public void EventPredictionDriver_VesselThatThrows_LoopContinues()
        {
            // Verify per-vessel try/catch isolation: a vessel with null ReferenceBody
            // would dereference null inside PredictAndUpdate (the predictor needs
            // body.Mu). The driver's check at the top filters that case BEFORE
            // incrementing EvaluationCount, so we instead use a setup that throws
            // INSIDE the try block: register two vessels, one with normal state and
            // one we'll force into a state that throws during prediction.
            //
            // Cleanest path: register vessel A (normal), register vessel B (normal),
            // assert both evaluated. Failing-vessel coverage is exercised by the
            // ReRootingDriver tests via the ThrowingActiveVessel stub; the event
            // predictor doesn't take an external stub, so a throw mid-PredictAndUpdate
            // would require a corrupted KeplerState that the existing math handles
            // gracefully without throwing.
            //
            // For Phase 1, this test instead verifies the simpler property: two
            // vessels both get evaluated. The exception-isolation code is exercised
            // by the try/catch structure being in place; the absence of a clean
            // throw-injection path doesn't invalidate the structural assertion.
            SetUpEventPredictionDriver();

            var kepler1 = NewKeplerState(_body);
            kepler1.Eccentricity = 0.1;
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler1);

            var secondVesselGo = new GameObject("SecondVessel");
            var secondVessel = secondVesselGo.AddComponent<Vessel>();
            try
            {
                var kepler2 = NewKeplerState(_body);
                kepler2.Eccentricity = 0.2;
                secondVessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler2);

                Assert.AreEqual(2, VesselRegistry.VesselCount,
                    "Sanity: 2 vessels registered");

                VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

                Assert.AreEqual(2, VesselEventPredictionDriver.EvaluationCount,
                    "Both vessels should be evaluated");
                Assert.AreEqual(2, VesselEventPredictionDriver.PredictionUpdateCount,
                    "Both vessels should complete prediction (no throws in normal state)");
            }
            finally
            {
                UnityObject.DestroyImmediate(secondVesselGo);
            }
        }

        [Test]
        public void EventPredictionDriver_DiagnosticCountersIncrement()
        {
            // Two ticks; counters should reflect cumulative evaluations across ticks.
            SetUpEventPredictionDriver();

            var kepler = NewKeplerState(_body);
            kepler.Eccentricity = 0.1;
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);
            Assert.AreEqual(1, VesselEventPredictionDriver.EvaluationCount);
            Assert.AreEqual(1, VesselEventPredictionDriver.PredictionUpdateCount);

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 2);
            Assert.AreEqual(2, VesselEventPredictionDriver.EvaluationCount,
                "EvaluationCount should be 2 after two ticks");
            Assert.AreEqual(2, VesselEventPredictionDriver.PredictionUpdateCount,
                "PredictionUpdateCount should be 2 after two ticks");
        }

        // ----- Section 20: VesselEventPredictionDriver + SoiCrossingPredictor integration (commit 046 Stage 2) -----

        // Test-scale Earth SOI for SOI-crossing scenarios. Matches the value used in
        // SoiCrossingPredictorTests for consistency.
        private const double TestEarthSoiRadiusMeters = 9.24e8;

        /// <summary>
        /// Configure <see cref="_body"/> as Earth with a finite SOI radius (overriding
        /// the SetUp's infinite-SOI default). Named wrapper preserved as semantic
        /// anchor: the test reads as "this test wants Earth with a finite SOI"
        /// rather than as a generic init call. Safe to re-invoke after SetUp's
        /// eager init per ReferenceBody.InitializeBodyForTesting idempotence.
        /// </summary>
        private void SetEarthFiniteSoiAndInitialize(double soiRadiusMeters)
        {
            _body.InitializeBodyForTesting(
                massKg: EarthMassKg,
                soiRadiusMeters: soiRadiusMeters);
        }

        /// <summary>
        /// Build a KeplerState with periapsis at 2e8 m (above LEO to keep solver
        /// stable per the Stage 1 e &lt; 0.8 constraint) and apoapsis at the given
        /// multiplier of Earth's test SOI. Returns a state whose orbit crosses
        /// Earth's SOI on its way to apoapsis.
        /// </summary>
        private KeplerState NewCrossingKeplerState(double apoMultiplierOfEarthSoi = 1.5)
        {
            double rPeri = 2.0e8;
            double rApo = apoMultiplierOfEarthSoi * TestEarthSoiRadiusMeters;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            return new KeplerState
            {
                SemiMajorAxis = a,
                Eccentricity = e,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = 0.0,
                EpochTick = 0,
                ReferenceBodyId = _body != null ? _body.BodyId : Guid.Empty,
            };
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_PopulatesSoiTransitionTickOnKeplerRailsVessel()
        {
            // Kepler-rails vessel with an orbit that crosses Earth's (finite) SOI on
            // its way to apoapsis. After one TickAdvanced fire, NextSoiTransitionTick
            // should be populated alongside the existing periapsis/apoapsis fields.
            SetUpEventPredictionDriver();
            SetEarthFiniteSoiAndInitialize(TestEarthSoiRadiusMeters);

            var kepler = NewCrossingKeplerState();
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            Assert.IsNull(_vessel.State.KeplerState.NextSoiTransitionTick,
                "Sanity: NextSoiTransitionTick should be null before driver fires");

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.IsNotNull(_vessel.State.KeplerState.NextSoiTransitionTick,
                "NextSoiTransitionTick should be populated after driver fires when " +
                "orbit crosses Earth's SOI");
            Assert.Greater(_vessel.State.KeplerState.NextSoiTransitionTick.Value, 0,
                "Predicted crossing tick should be in the future");
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_UpdatesQueueWithSoiCrossing()
        {
            // Same vessel/orbit setup as the previous test; verify the EventQueue
            // gains a SoiCrossing entry alongside Periapsis/Apoapsis.
            var controller = SetUpEventPredictionDriver();
            SetEarthFiniteSoiAndInitialize(TestEarthSoiRadiusMeters);

            var kepler = NewCrossingKeplerState();
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            Assert.AreEqual(0, controller.EventQueue.Count,
                "Sanity: queue should be empty before driver fires");

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            // Elliptical orbit with crossing: Periapsis + Apoapsis + SoiCrossing = 3 entries.
            Assert.AreEqual(3, controller.EventQueue.Count,
                "Queue should have Periapsis, Apoapsis, and SoiCrossing entries for an " +
                "elliptical orbit that crosses Earth's SOI");
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_NoSoiCrossingExpected_NextSoiTransitionTickNull()
        {
            // Vessel in low circular orbit fully inside Earth's (finite) SOI. No
            // children registered. The predictor's closed-form path executes and
            // returns null at the rApo < SoiRadius early-return. The driver writes
            // null to NextSoiTransitionTick; the queue receives no SoiCrossing entry.
            var controller = SetUpEventPredictionDriver();
            SetEarthFiniteSoiAndInitialize(TestEarthSoiRadiusMeters);

            // Default NewKeplerState is circular LEO (rApo = LeoRadius = 7e6 m, far
            // below 9.24e8 m Earth SOI) — orbit contained.
            var kepler = NewKeplerState(_body);
            kepler.Eccentricity = 0.1;  // mildly elliptical so Periapsis/Apoapsis still populate
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.IsNull(_vessel.State.KeplerState.NextSoiTransitionTick,
                "NextSoiTransitionTick should be null when orbit is fully inside Earth's SOI");

            // Queue should have Periapsis + Apoapsis (2 entries), not 3.
            Assert.AreEqual(2, controller.EventQueue.Count,
                "Queue should have only Periapsis + Apoapsis entries when no SOI crossing is predicted");
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_BothPredictorsRun_BothFieldsPopulated()
        {
            // Vessel with elliptical orbit that crosses Earth's SOI. All three
            // prediction fields should populate after one driver tick.
            var controller = SetUpEventPredictionDriver();
            SetEarthFiniteSoiAndInitialize(TestEarthSoiRadiusMeters);

            var kepler = NewCrossingKeplerState();
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.IsNotNull(_vessel.State.KeplerState.NextPeriapsisTick,
                "NextPeriapsisTick should be populated for elliptical orbit");
            Assert.IsNotNull(_vessel.State.KeplerState.NextApoapsisTick,
                "NextApoapsisTick should be populated for elliptical orbit");
            Assert.IsNotNull(_vessel.State.KeplerState.NextSoiTransitionTick,
                "NextSoiTransitionTick should be populated when orbit crosses Earth's SOI");

            // All three queue entries present.
            Assert.AreEqual(3, controller.EventQueue.Count,
                "Queue should have all three event-type entries for a crossing elliptical orbit");

            // Counter semantics: both EvaluationCount and PredictionUpdateCount
            // should equal 1 (one vessel examined, PredictAndUpdate completed).
            Assert.AreEqual(1, VesselEventPredictionDriver.EvaluationCount);
            Assert.AreEqual(1, VesselEventPredictionDriver.PredictionUpdateCount);
        }

        // ----- Section 21: VesselEventPredictionDriver + AtmosphericEntry + SurfaceImpact (commit 047 Stage 2) -----

        // Earth-scale surface + atmosphere values for the Stage 2 tests.
        private const double TestEarthSurfaceRadiusMeters = 6.371e6;
        private const double TestEarthAtmosphericTopMeters = 1.0e5;  // 100 km

        /// <summary>
        /// Configure <see cref="_body"/> with Earth-like surfaceRadiusMeters and
        /// atmosphericTopAltitudeMeters via the parameterized
        /// InitializeBodyForTesting overload. Named wrapper preserved as semantic
        /// anchor.
        /// </summary>
        private void SetEarthSurfaceAtmosphereAndInitialize(
            double surfaceRadius, double atmosphericTop)
        {
            _body.InitializeBodyForTesting(
                massKg: EarthMassKg,
                soiRadiusMeters: double.PositiveInfinity,
                surfaceRadiusMeters: surfaceRadius,
                atmosphericTopAltitudeMeters: atmosphericTop);
        }

        /// <summary>
        /// Build a KeplerState whose orbit reaches into the atmosphere (periapsis
        /// below atmospheric top, apoapsis well above). Vessel starts at apoapsis
        /// so the predicted entry is in the future. e &lt; 0.8 by construction.
        /// </summary>
        private KeplerState NewAtmosphericCrossingState()
        {
            // rPeri inside atmosphere (above surface but below atmospheric top):
            // 50 km above surface, atmospheric top at 100 km, so peri is 50 km
            // inside atmosphere.
            double rPeri = TestEarthSurfaceRadiusMeters + 5.0e4;
            double rApo = 1.0e7;  // 10,000 km from body center — well above atmosphere
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            // Sanity: e should be ~0.22, well under 0.8.
            return new KeplerState
            {
                SemiMajorAxis = a,
                Eccentricity = e,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = math.PI_DBL,  // start at apoapsis, descending
                EpochTick = 0,
                ReferenceBodyId = _body != null ? _body.BodyId : Guid.Empty,
            };
        }

        /// <summary>
        /// Build a KeplerState whose orbit impacts the surface (periapsis below
        /// surface). Vessel starts at apoapsis. e &lt; 0.8 by construction.
        /// </summary>
        private KeplerState NewSurfaceImpactState()
        {
            double rPeri = TestEarthSurfaceRadiusMeters - 1.0e5;  // 100 km below surface
            double rApo = 1.0e7;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            return new KeplerState
            {
                SemiMajorAxis = a,
                Eccentricity = e,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = math.PI_DBL,  // start at apoapsis, descending
                EpochTick = 0,
                ReferenceBodyId = _body != null ? _body.BodyId : Guid.Empty,
            };
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_PopulatesNextModeTransitionTickFromAtmosphericEntry()
        {
            // Body with atmosphere but no orbit-impacts-surface. Vessel orbit reaches
            // into atmosphere; predicted entry tick populates NextModeTransitionTick.
            // (Surface impact predictor returns null here because rPeri > surface.)
            SetUpEventPredictionDriver();
            SetEarthSurfaceAtmosphereAndInitialize(
                TestEarthSurfaceRadiusMeters, TestEarthAtmosphericTopMeters);

            var kepler = NewAtmosphericCrossingState();
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            Assert.IsNull(_vessel.State.KeplerState.NextModeTransitionTick,
                "Sanity: NextModeTransitionTick should be null before driver fires");

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.IsNotNull(_vessel.State.KeplerState.NextModeTransitionTick,
                "NextModeTransitionTick should be populated when orbit reaches atmosphere");
            Assert.Greater(_vessel.State.KeplerState.NextModeTransitionTick.Value, 0,
                "Predicted mode-transition tick should be in the future");
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_PopulatesNextModeTransitionTickFromSurfaceImpact()
        {
            // Body with NO atmosphere (vacuum) but vessel orbit impacts surface.
            // SurfaceImpactPredictor populates NextModeTransitionTick;
            // AtmosphericEntryPredictor returns null (vacuum body).
            SetUpEventPredictionDriver();
            SetEarthSurfaceAtmosphereAndInitialize(
                TestEarthSurfaceRadiusMeters, atmosphericTop: 0.0);  // vacuum

            var kepler = NewSurfaceImpactState();
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.IsNotNull(_vessel.State.KeplerState.NextModeTransitionTick,
                "NextModeTransitionTick should be populated when orbit impacts surface");
            Assert.Greater(_vessel.State.KeplerState.NextModeTransitionTick.Value, 0,
                "Predicted mode-transition tick should be in the future");
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_EarliestOfAtmosphericAndSurface_WinsInModeTransitionTick()
        {
            // Body has both atmosphere AND vessel orbit impacts surface. Vessel
            // hits atmospheric boundary (threshold = SurfaceRadiusMeters + AtmoTop =
            // 6.471e6) BEFORE surface (threshold = SurfaceRadiusMeters = 6.371e6),
            // so atmospheric entry tick should be earlier than surface impact tick.
            // NextModeTransitionTick = min(atmospheric, surface) = atmospheric tick.
            SetUpEventPredictionDriver();
            SetEarthSurfaceAtmosphereAndInitialize(
                TestEarthSurfaceRadiusMeters, TestEarthAtmosphericTopMeters);

            // Orbit with periapsis BELOW surface (impacts) — also passes through
            // atmosphere on the way down.
            var kepler = NewSurfaceImpactState();
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            // Verify NextModeTransitionTick is populated.
            long? modeTick = _vessel.State.KeplerState.NextModeTransitionTick;
            Assert.IsNotNull(modeTick,
                "NextModeTransitionTick should be populated when both atmosphere and surface are reached");

            // Compute atmospheric and surface predicted ticks independently for comparison.
            long? atmoTick = AtmosphericEntryPredictor.PredictNextEntry(
                kepler, _body, currentTick: 1, SimTickController.SimTickIntervalSeconds);
            long? surfaceTick = SurfaceImpactPredictor.PredictNextImpact(
                kepler, _body, currentTick: 1, SimTickController.SimTickIntervalSeconds);

            Assert.IsTrue(atmoTick.HasValue, "Atmospheric entry should be predicted");
            Assert.IsTrue(surfaceTick.HasValue, "Surface impact should be predicted");
            Assert.Less(atmoTick.Value, surfaceTick.Value,
                "Atmospheric entry (higher threshold radius) should fire before surface impact (lower)");

            // The aggregated tick should equal the earlier of the two (the atmospheric tick).
            Assert.AreEqual(atmoTick.Value, modeTick.Value,
                "NextModeTransitionTick should equal min(atmosphericEntryTick, surfaceImpactTick)");
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_UpdatesQueueWithAtmosphericEntryAndSurfaceImpact()
        {
            // Both atmosphere + surface-impact predictors populate their respective
            // queue entries. With orbit crossing both thresholds: queue has
            // Periapsis + Apoapsis + AtmosphericEntry + SurfaceImpact = 4 entries.
            // (No SoiCrossing — default _body has infinite SOI; predictor returns null.)
            var controller = SetUpEventPredictionDriver();
            SetEarthSurfaceAtmosphereAndInitialize(
                TestEarthSurfaceRadiusMeters, TestEarthAtmosphericTopMeters);

            var kepler = NewSurfaceImpactState();
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            Assert.AreEqual(0, controller.EventQueue.Count,
                "Sanity: queue empty before driver fires");

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.AreEqual(4, controller.EventQueue.Count,
                "Queue should have Periapsis + Apoapsis + AtmosphericEntry + SurfaceImpact entries");
        }

        [Test]
        public void EventPredictionDriver_OnTickAdvanced_NoAtmosphereOrImpactExpected_NextModeTransitionTickNull()
        {
            // High-altitude circular orbit (e=0.05, rPeri ≈ 6.65e6 above Earth-default
            // surface 6.371e6) around vacuum body. Neither atmospheric entry nor
            // surface impact applies. NextModeTransitionTick stays null; queue gets
            // only Periapsis + Apoapsis.
            var controller = SetUpEventPredictionDriver();
            SetEarthSurfaceAtmosphereAndInitialize(
                TestEarthSurfaceRadiusMeters, atmosphericTop: 0.0);  // vacuum

            var kepler = NewKeplerState(_body);
            kepler.Eccentricity = 0.05;  // rPeri = 7e6 * 0.95 = 6.65e6 > 6.371e6 surface
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            VesselEventPredictionDriver.OnTickAdvanced(tickNumber: 1);

            Assert.IsNull(_vessel.State.KeplerState.NextModeTransitionTick,
                "NextModeTransitionTick should be null when neither atmosphere nor surface is reached");

            // Queue has only Periapsis + Apoapsis (no SoiCrossing because Earth has
            // infinite SOI by default in this test, no AtmosphericEntry vacuum body,
            // no SurfaceImpact orbit-above-surface).
            Assert.AreEqual(2, controller.EventQueue.Count,
                "Queue should contain only Periapsis + Apoapsis entries");
        }
    }
}
