using System;
using System.Collections;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;
using SpaceSim.Foundation.Vessels;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
// Alias UnityEngine.Object to disambiguate from System.Object. Tests need
// UnityObject.DestroyImmediate (UnityEngine.Object) for GameObject cleanup; bare 'Object'
// is ambiguous when both 'using System' and 'using UnityEngine' are present.
using UnityObject = UnityEngine.Object;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// PlayMode tests for <see cref="Vessel"/>. Cover the cases that require Unity's
    /// MonoBehaviour lifecycle (Awake / OnEnable / Update) to fire — specifically:
    ///
    ///   1. Vessel.OnEnable registers the vessel with <see cref="VesselRegistry"/>. In
    ///      EditMode, OnEnable doesn't fire on AddComponent, so the registration path is
    ///      untested. PlayMode lets the lifecycle run normally.
    ///   2. Vessel.OnDisable unregisters (symmetric to OnEnable).
    ///   3. After TransitionToPhysXActive, the newly-added FloatingOriginAnchor's
    ///      OnEnable fires and the anchor registers with FloatingOriginManager. A
    ///      subsequent shift is dispatched to the anchor (the anchor responds to the
    ///      shift by moving the GameObject). This is the test that verifies the
    ///      "anchor added at runtime via AddComponent registers correctly" claim from
    ///      Stage 3's Q4 surface-before-writing discussion.
    ///
    /// EachPlayMode test uses [UnityTest] IEnumerator and yield return null between
    /// component creation and assertions to let Unity advance one frame (which fires
    /// Awake / OnEnable). Same discipline as commit 032's
    /// FloatingOriginManagerPlayModeTests.
    /// </summary>
    public class VesselPlayModeTests
    {
        private const double LeoRadius = 7_000_000.0;

        private GameObject _vesselGo;
        private GameObject _bodyGo;
        private GameObject _managerGo;

        [SetUp]
        public void SetUp()
        {
            VesselRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            if (_vesselGo != null) UnityObject.DestroyImmediate(_vesselGo);
            if (_bodyGo != null) UnityObject.DestroyImmediate(_bodyGo);
            if (_managerGo != null) UnityObject.DestroyImmediate(_managerGo);
            VesselRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();
        }

        // ----- Helpers -----

        private VesselAuthoritativeState NewState()
        {
            return new VesselAuthoritativeState
            {
                VesselId = Guid.NewGuid(),
                DesignId = Guid.NewGuid(),
                Name = "TestVessel",
                TotalMassKg = 1000.0,
            };
        }

        // ----- OnEnable / OnDisable registration -----

        [UnityTest]
        public IEnumerator OnEnable_RegistersVesselWithRegistry()
        {
            _bodyGo = new GameObject("TestReferenceBody");
            ReferenceBody body = _bodyGo.AddComponent<ReferenceBody>();

            _vesselGo = new GameObject("TestVessel");
            Vessel vessel = _vesselGo.AddComponent<Vessel>();

            // Awake/OnEnable haven't fired yet. After one yield, they fire and OnEnable
            // tries to register. Initialize hasn't run, so OnEnable bails early
            // (_initialized is false). The vessel is NOT yet in the registry.
            yield return null;
            Assert.AreEqual(0, VesselRegistry.VesselCount,
                "OnEnable should bail when _initialized is false; vessel not in registry yet.");

            // Now Initialize. Initialize itself calls RegisterVesselSafe if isActiveAndEnabled.
            // Using PhysXActive: the test asserts registry registration only, not anything
            // mode-specific. PhysXActive avoids the 4-arg overload (which requires a real
            // KeplerState the test doesn't care about). Previously used KeplerRails; updated
            // in commit 042 since the 3-arg overload no longer accepts KeplerRails.
            vessel.Initialize(NewState(), body, PhysicsMode.PhysXActive);
            Assert.AreEqual(1, VesselRegistry.VesselCount,
                "Initialize should register the vessel via its own RegisterVesselSafe call.");
            Assert.AreSame(vessel, VesselRegistry.Vessels[0]);
        }

        [UnityTest]
        public IEnumerator OnDisable_UnregistersVesselFromRegistry()
        {
            _bodyGo = new GameObject("TestReferenceBody");
            ReferenceBody body = _bodyGo.AddComponent<ReferenceBody>();

            _vesselGo = new GameObject("TestVessel");
            Vessel vessel = _vesselGo.AddComponent<Vessel>();
            yield return null;
            // PhysXActive — symmetric to OnEnable_RegistersVesselWithRegistry; the test
            // asserts unregister-on-disable, not anything mode-specific. Updated in commit
            // 042 from KeplerRails.
            vessel.Initialize(NewState(), body, PhysicsMode.PhysXActive);
            Assert.AreEqual(1, VesselRegistry.VesselCount,
                "Setup: vessel should be registered after Initialize.");

            // Disabling the GameObject fires OnDisable on the Vessel component.
            _vesselGo.SetActive(false);
            yield return null;
            Assert.AreEqual(0, VesselRegistry.VesselCount,
                "OnDisable should unregister the vessel.");
        }

        // ----- Runtime AddComponent of FloatingOriginAnchor -----

        [UnityTest]
        public IEnumerator TransitionToPhysXActive_RuntimeAddedAnchor_ReceivesShifts()
        {
            // Build a manager first so the anchor has someone to register with.
            _managerGo = new GameObject("FloatingOriginRoot");
            FloatingOriginManager manager = _managerGo.AddComponent<FloatingOriginManager>();
            yield return null;  // Let manager's Awake fire and claim Instance.
            Assert.AreSame(manager, FloatingOriginManager.Instance,
                "Setup: FloatingOriginManager should have claimed Instance.");

            // Build the vessel in Kepler-rails mode (no rigidbody, no anchor).
            _bodyGo = new GameObject("TestReferenceBody");
            ReferenceBody body = _bodyGo.AddComponent<ReferenceBody>();
            yield return null;  // Let body's Awake fire and capture PositionWorld.

            _vesselGo = new GameObject("TestVessel");
            Vessel vessel = _vesselGo.AddComponent<Vessel>();
            yield return null;
            // Construct the KeplerState that TransitionToPhysXActive will transition
            // from. Pre-commit-042 this was a manual assignment to state.KeplerState
            // before calling the 3-arg Initialize (the workaround for the
            // Initialize-in-KeplerRails state inconsistency). Post-commit-042 the
            // 4-arg overload populates State.KeplerState directly from the parameter.
            var keplerState = new KeplerState
            {
                SemiMajorAxis = LeoRadius,
                Eccentricity = 0.0,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = 0.0,
                EpochTick = 0,
                ReferenceBodyId = body.BodyId,
            };
            vessel.Initialize(NewState(), body, PhysicsMode.KeplerRails, keplerState);

            // Transition to PhysX-active. This calls AddComponent<Rigidbody> followed by
            // AddComponent<FloatingOriginAnchor>. The anchor's OnEnable should fire
            // immediately (synchronously inside AddComponent) and register with the
            // manager. After this call, the manager's listener list should include the
            // newly-created anchor.
            int beforeShiftCount = manager.ShiftCount;
            int beforeListenerCount = manager.ListenerCount;
            vessel.TransitionToPhysXActive();

            Assert.AreEqual(beforeListenerCount + 1, manager.ListenerCount,
                "FloatingOriginAnchor added at runtime should register with the manager immediately.");

            // Trigger a shift well past the threshold (default 50 km).
            manager.MaybeShiftOrigin(new WorldPosition(100_000.0, 0.0, 0.0));

            Assert.AreEqual(beforeShiftCount + 1, manager.ShiftCount,
                "Shift should fire (active vessel position is past 50 km threshold).");

            // The anchor's OnFloatingOriginShifted should have moved the rigidbody by -delta.
            // The shift moved the origin from (0,0,0) to (100000,0,0), so anchored
            // rigidbodies move by -100000 in X. The vessel's rigidbody starts at the
            // position corresponding to its KeplerState (LeoRadius, 0, 0), so after the
            // shift the rigidbody's local position is (LeoRadius - 100000, 0, 0).
            Vector3 expectedRbPos = new Vector3((float)(LeoRadius - 100_000.0), 0f, 0f);
            Assert.AreEqual(expectedRbPos.x, vessel.Rigidbody.position.x, 1f,
                "Rigidbody position should reflect the shift; anchor add-via-AddComponent path works.");
        }
    }
}
