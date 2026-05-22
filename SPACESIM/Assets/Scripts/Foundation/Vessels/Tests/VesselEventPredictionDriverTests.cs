using System;
using System.Reflection;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;
using SpaceSim.Foundation.Vessels;
using UnityEngine;
using UnityObject = UnityEngine.Object;

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
        private const double LeoRadius = 7_000_000.0;

        private GameObject _bodyGo;
        private GameObject _simTickGo;
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
            _bodyGo = new GameObject("TestReferenceBody");
            _body = _bodyGo.AddComponent<ReferenceBody>();
            _body.InitializeBodyForTesting(
                massKg: EarthMassKg,
                soiRadiusMeters: double.PositiveInfinity,
                parentBody: null,
                surfaceRadiusMeters: 1.0,
                atmosphericTopAltitudeMeters: 0.0);

            // SimTickController instance — the driver's PredictAndUpdate writes
            // to controller.EventQueue, so a real controller is required for
            // the EventQueue assertion path.
            _simTickGo = new GameObject("TestSimTick");
            _controller = _simTickGo.AddComponent<SimTickController>();
            SimTickController.SetInstanceForTesting(_controller);
            VesselEventPredictionDriver.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
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
    }
}
