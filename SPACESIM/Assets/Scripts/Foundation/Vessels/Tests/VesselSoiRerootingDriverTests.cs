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
    /// EditMode tests for <see cref="VesselSoiRerootingDriver"/>. Driver subscribes
    /// to <see cref="SimTickController.TickAdvanced"/> and re-roots Kepler-rails
    /// vessels across SOI boundaries (outward to parent when distance to current
    /// body exceeds its SOI radius; inward to a child body when the vessel enters
    /// the child's SOI).
    ///
    /// <para>
    /// Migrated from <c>VesselTests.cs</c> as part of the operational commit that
    /// split <c>VesselTests.cs</c> at the natural driver-test seam. The four tests
    /// in this file were section 18 of the pre-split <c>VesselTests.cs</c>;
    /// content is verbatim (no semantic changes), but the Earth-Moon test
    /// substrate helper <see cref="BuildMoonAsChildOfEarth"/> now resolves via
    /// <c>using static VesselTestHelpers;</c> with an explicit <c>_body</c>
    /// argument instead of the private instance helper's implicit <c>_body</c>
    /// reference.
    /// </para>
    ///
    /// <para>
    /// <strong>WHY NOT IVessel-MIGRATED:</strong> the IVessel commit
    /// (<c>81d1d60</c>) deliberately excluded <see cref="VesselSoiRerootingDriver"/>
    /// from the IVessel abstraction because its inner <c>EvaluateAndReroot</c>
    /// method calls <c>vessel.ReRootToBody(parentBody)</c> — a mutating lifecycle
    /// operation incompatible with the IVessel read-only contract. These tests
    /// stay coupled to concrete <see cref="Vessel"/> components for now. A
    /// future commit may split detect/dispatch concerns if SOI-rerooter test
    /// needs justify it.
    /// </para>
    /// </summary>
    public class VesselSoiRerootingDriverTests
    {
        private const double EarthMassKg = 5.972e24;

        private GameObject _vesselGo;
        private GameObject _bodyGo;
        private GameObject _simTickGo;

        private Vessel _vessel;
        private ReferenceBody _body;

        [SetUp]
        public void SetUp()
        {
            // Defensive: clear shared static state before each test so registry
            // leaks from other test classes don't poison this one.
            VesselRegistry.ClearForTesting();
            BodyRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();
            VesselTransitionDriver.Shutdown();
            VesselSoiRerootingDriver.Shutdown();
            VesselEventPredictionDriver.Shutdown();

            // ReferenceBody — the SetUp baseline body (Earth in these tests).
            // Point-mass default (surfaceRadiusMeters = 1.0 via reflection
            // carve-out) so SurfaceImpactPredictor doesn't fire on LEO orbits.
            // SetUp does NOT call InitializeBodyForTesting; tests opt in via
            // BuildMoonAsChildOfEarth (which initializes both Earth and Moon)
            // or call _body.InitializeBodyForTesting() themselves.
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

            // _simTickGo is null at SetUp; each test constructs the controller
            // it needs (or doesn't, if the test exercises a no-controller path).
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

        // ----- Tests (4, migrated verbatim from VesselTests.cs section 18) -----

        [Test]
        public void SoiRerootingDriver_VesselWithinSoi_DoesNotReroot()
        {
            // Earth-Moon multi-body world. Vessel deep inside Moon's SOI (close to
            // Moon center). The driver should evaluate the vessel and find no
            // crossings: distance to Moon < Moon SOI (so no outward re-root); no
            // children of Moon to enter inward.
            var moon = BuildMoonAsChildOfEarth(_body);
            try
            {
                // SimTickController needed for the driver. Construct + claim Instance.
                _simTickGo = new GameObject("TestSimTick");
                var controller = _simTickGo.AddComponent<SimTickController>();
                SimTickController.SetInstanceForTesting(controller);

                // Vessel orbiting Moon at 1000 km altitude. Position relative to Moon
                // (in Moon's frame, since vessel will be Moon-rooted).
                var moonOrbit = new KeplerState
                {
                    SemiMajorAxis = 2_737_000.0,  // Moon radius (1737km) + 1000 km altitude
                    Eccentricity = 0.0,
                    Inclination = 0.0,
                    LongitudeOfAscendingNode = 0.0,
                    ArgumentOfPeriapsis = 0.0,
                    TrueAnomalyAtEpoch = 0.0,
                    EpochTick = 0,
                    ReferenceBodyId = moon.BodyId,
                };
                _vessel.Initialize(NewState(), moon, PhysicsMode.KeplerRails, moonOrbit);

                VesselSoiRerootingDriver.OnTickAdvanced(tickNumber: 1);

                Assert.AreEqual(1, VesselSoiRerootingDriver.EvaluationCount,
                    "Driver should have evaluated 1 vessel");
                Assert.AreEqual(0, VesselSoiRerootingDriver.RerootingCount,
                    "Vessel inside SOI with no child crossings should NOT re-root");
                Assert.AreSame(moon, _vessel.ReferenceBody,
                    "Vessel should remain Moon-rooted");
            }
            finally
            {
                UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        [Test]
        public void SoiRerootingDriver_VesselBeyondSoi_RerootsToParent()
        {
            // Vessel currently Moon-rooted but at a distance from Moon that exceeds
            // Moon's SOI. The driver should detect the outward crossing and re-root
            // to Earth (Moon's parent).
            var moon = BuildMoonAsChildOfEarth(_body);
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex(".*exited SOI of 'Moon'.*re-rooting to parent 'TestReferenceBody'.*"));

                _simTickGo = new GameObject("TestSimTick");
                var controller = _simTickGo.AddComponent<SimTickController>();
                SimTickController.SetInstanceForTesting(controller);

                // Vessel orbit around Moon with SMA > Moon SOI. The propagator at
                // ν₀ = 0 puts the vessel at periapsis = a(1-e); with e=0 and a=1e8 m,
                // periapsis = 1e8 m, well beyond Moon SOI (6.6e7 m).
                var moonEscapeOrbit = new KeplerState
                {
                    SemiMajorAxis = 1.0e8,
                    Eccentricity = 0.0,
                    Inclination = 0.0,
                    LongitudeOfAscendingNode = 0.0,
                    ArgumentOfPeriapsis = 0.0,
                    TrueAnomalyAtEpoch = 0.0,
                    EpochTick = 0,
                    ReferenceBodyId = moon.BodyId,
                };
                _vessel.Initialize(NewState(), moon, PhysicsMode.KeplerRails, moonEscapeOrbit);

                VesselSoiRerootingDriver.OnTickAdvanced(tickNumber: 1);

                Assert.AreEqual(1, VesselSoiRerootingDriver.EvaluationCount);
                Assert.AreEqual(1, VesselSoiRerootingDriver.RerootingCount,
                    "Vessel beyond Moon SOI should re-root to parent (Earth)");
                Assert.AreSame(_body, _vessel.ReferenceBody,
                    "Vessel should now be Earth-rooted");
                Assert.AreEqual(_body.BodyId, _vessel.State.KeplerState.ReferenceBodyId);
            }
            finally
            {
                UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        [Test]
        public void SoiRerootingDriver_VesselEntersChildSoi_RerootsToChild()
        {
            // Vessel currently Earth-rooted with an orbit that passes near the Moon
            // (within Moon's SOI). The driver should detect the inward crossing and
            // re-root to Moon.
            var moon = BuildMoonAsChildOfEarth(_body);
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex(".*entered SOI of 'Moon'.*"));

                _simTickGo = new GameObject("TestSimTick");
                var controller = _simTickGo.AddComponent<SimTickController>();
                SimTickController.SetInstanceForTesting(controller);

                // Construct a circular Earth orbit at Earth-Moon distance, with ν₀
                // such that the vessel is at the Moon's exact angular position. The
                // vessel will be at (EarthMoonDistance, 0, 0) in Earth frame —
                // co-located with the Moon — comfortably inside Moon's SOI.
                var earthOrbitAtMoonPosition = new KeplerState
                {
                    SemiMajorAxis = EarthMoonDistanceMeters,
                    Eccentricity = 0.0,
                    Inclination = 0.0,
                    LongitudeOfAscendingNode = 0.0,
                    ArgumentOfPeriapsis = 0.0,
                    TrueAnomalyAtEpoch = 0.0,
                    EpochTick = 0,
                    ReferenceBodyId = _body.BodyId,
                };
                _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, earthOrbitAtMoonPosition);

                VesselSoiRerootingDriver.OnTickAdvanced(tickNumber: 1);

                Assert.AreEqual(1, VesselSoiRerootingDriver.EvaluationCount);
                Assert.AreEqual(1, VesselSoiRerootingDriver.RerootingCount,
                    "Vessel inside Moon's SOI should re-root to Moon");
                Assert.AreSame(moon, _vessel.ReferenceBody);
            }
            finally
            {
                UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        [Test]
        public void SoiRerootingDriver_VesselWithNoParent_DoesNotRerootOutward()
        {
            // Top-level body (Earth in SetUp) has SoiRadiusMeters = PositiveInfinity
            // and ParentBody == null. A vessel orbiting Earth — no matter how far —
            // should never trigger outward re-rooting because there's no parent to
            // re-root to AND the infinite SOI guarantees distance < SOI for any
            // finite distance.
            _simTickGo = new GameObject("TestSimTick");
            var controller = _simTickGo.AddComponent<SimTickController>();
            SimTickController.SetInstanceForTesting(controller);

            // Initialize Earth (the SetUp _body) so its top-level state is in place.
            _body.InitializeBodyForTesting();

            // Vessel on an extremely wide orbit around Earth — SMA 1e10 m, far beyond
            // any plausible SOI of a real planet. Should not trigger any re-rooting
            // because Earth has infinite SOI.
            var wideOrbit = new KeplerState
            {
                SemiMajorAxis = 1.0e10,
                Eccentricity = 0.0,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = 0.0,
                EpochTick = 0,
                ReferenceBodyId = _body.BodyId,
            };
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, wideOrbit);

            VesselSoiRerootingDriver.OnTickAdvanced(tickNumber: 1);

            Assert.AreEqual(1, VesselSoiRerootingDriver.EvaluationCount);
            Assert.AreEqual(0, VesselSoiRerootingDriver.RerootingCount,
                "Vessel orbiting a top-level body (infinite SOI, no parent) should never re-root outward");
            Assert.AreSame(_body, _vessel.ReferenceBody,
                "Vessel should remain top-level-rooted");
        }
    }
}
