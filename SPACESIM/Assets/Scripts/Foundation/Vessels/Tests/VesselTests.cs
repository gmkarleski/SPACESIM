using System;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;
using SpaceSim.Foundation.Vessels;
using Unity.Mathematics;
using UnityEngine;
// Alias UnityEngine.Object to disambiguate from System.Object. Tests need
// UnityObject.DestroyImmediate (UnityEngine.Object) for GameObject cleanup; bare 'Object'
// is ambiguous when both 'using System' and 'using UnityEngine' are present.
using UnityObject = UnityEngine.Object;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="Vessel"/>. Tests cover Initialize behavior,
    /// transition logic (both directions), transition guards (no-op on wrong-mode
    /// invocations), GetWorldPosition correctness in both modes, and the
    /// PhysX → Kepler → PhysX round-trip preservation.
    ///
    /// EditMode constraint: Awake doesn't fire on AddComponent, and Destroy is deferred to
    /// next-frame (which never arrives). Vessel.cs handles this with Application.isPlaying
    /// check in DestroyComponentSafe (uses DestroyImmediate in EditMode). Tests can
    /// therefore exercise the full transition logic without entering Play mode.
    ///
    /// What these tests do NOT cover (deferred to PlayMode tests):
    ///   - Vessel.OnEnable registration with VesselRegistry (Awake/OnEnable don't fire in
    ///     EditMode). Tests can verify Initialize registers via the
    ///     `if (isActiveAndEnabled) RegisterVesselSafe` path; PlayMode tests verify the
    ///     OnEnable path independently.
    ///   - FloatingOriginAnchor receiving shifts after being added at runtime (anchor's
    ///     own OnEnable doesn't fire in EditMode either).
    /// </summary>
    public class VesselTests
    {
        private const double EarthMassKg = 5.972e24;
        private static readonly double EarthMu = CoordinateMath.G * EarthMassKg;
        private const double LeoRadius = 7_000_000.0;

        private GameObject _vesselGo;
        private GameObject _bodyGo;
        private GameObject _managerGo;
        private GameObject _simTickGo;

        private Vessel _vessel;
        private ReferenceBody _body;

        [SetUp]
        public void SetUp()
        {
            VesselRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();

            // ReferenceBody MonoBehaviour. Awake doesn't fire in EditMode, so the body's
            // BodyId stays Guid.Empty and PositionWorld stays default(WorldPosition).
            // For most tests this is fine because the math doesn't care; tests that need
            // a real BodyId assign one manually after AddComponent.
            _bodyGo = new GameObject("TestReferenceBody");
            _body = _bodyGo.AddComponent<ReferenceBody>();

            // Vessel GameObject (no components yet beyond the Vessel itself).
            _vesselGo = new GameObject("TestVessel");
            _vessel = _vesselGo.AddComponent<Vessel>();

            // Optional: FloatingOriginManager + SimTickController if specific tests want
            // them. Most tests don't because the Vessel handles Instance-null gracefully.
            _managerGo = null;
            _simTickGo = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_vesselGo != null) UnityObject.DestroyImmediate(_vesselGo);
            if (_bodyGo != null) UnityObject.DestroyImmediate(_bodyGo);
            if (_managerGo != null) UnityObject.DestroyImmediate(_managerGo);
            if (_simTickGo != null) UnityObject.DestroyImmediate(_simTickGo);

            VesselRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();
        }

        // ----- Helpers -----

        private VesselAuthoritativeState NewState(PhysicsMode mode = PhysicsMode.PhysXActive)
        {
            return new VesselAuthoritativeState
            {
                VesselId = Guid.NewGuid(),
                DesignId = Guid.NewGuid(),
                Name = "TestVessel",
                TotalMassKg = 1000.0,
                Mode = mode,
            };
        }

        // ----- Initialize -----

        [Test]
        public void Initialize_StoresStateAndReferenceBody()
        {
            var state = NewState();
            _vessel.Initialize(state, _body, PhysicsMode.PhysXActive);

            Assert.AreSame(state, _vessel.State);
            Assert.AreSame(_body, _vessel.ReferenceBody);
            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
        }

        [Test]
        public void Initialize_InPhysXActiveMode_AddsRigidbodyAndAnchor()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);

            Assert.IsNotNull(_vessel.Rigidbody, "Rigidbody should be added in PhysX-active mode");
            Assert.IsNotNull(_vesselGo.GetComponent<FloatingOriginAnchor>(),
                "FloatingOriginAnchor should be added in PhysX-active mode");
            Assert.IsFalse(_vessel.Rigidbody.useGravity,
                "Rigidbody should have useGravity = false by default");
        }

        [Test]
        public void Initialize_InKeplerRailsMode_DoesNotAddRigidbodyOrAnchor()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails);

            Assert.IsNull(_vessel.Rigidbody, "Rigidbody should not be present in Kepler-rails mode");
            Assert.IsNull(_vesselGo.GetComponent<FloatingOriginAnchor>(),
                "FloatingOriginAnchor should not be present in Kepler-rails mode");
        }

        // ----- TransitionToKeplerRails -----

        [Test]
        public void TransitionToKeplerRails_PopulatesKeplerStateWithRealOrbitalElements()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);

            // Position the rigidbody at LEO with circular orbit velocity. Body is at origin
            // (default in EditMode where ReferenceBody.Awake didn't fire).
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);

            // Set body's μ-equivalent: BodyId and mass affect _body.Mu through the
            // SerializeField, but Awake hasn't fired so we patch BodyId via reflection-free
            // path. The default massKg = 5.972e24 (Earth) from the SerializeField default
            // is what we computed EarthMu from, so Mu = EarthMu already.
            _vessel.TransitionToKeplerRails();

            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode);
            Assert.IsNotNull(_vessel.State.KeplerState, "KeplerState should be populated");

            // Semi-major axis should match LeoRadius (circular orbit). Tolerance 1.0m at
            // LEO scale (~7e6 m) is ~1.4e-7 relative — comfortably above single-precision
            // float epsilon at this magnitude (~1.2e-7).
            Assert.AreEqual(LeoRadius, _vessel.State.KeplerState.SemiMajorAxis, 1.0,
                "Semi-major axis should equal LEO radius for circular orbit");

            // Eccentricity tolerance: 1e-6, NOT 1e-9.
            //
            // The pure-double OrbitalElements math reliably hits ~1e-12 eccentricity for a
            // mathematically-circular orbit. The looser tolerance here accommodates the
            // float-precision noise injected by passing the state through Unity's Rigidbody:
            //
            //   - Vector3 is float (~7 decimal digits of precision)
            //   - At LEO radius (~7e6 m), float epsilon is ~0.84 m
            //   - Velocity (~7546 m/s circular) has float epsilon ~9.0e-4 m/s
            //   - Eccentricity computed from (r, v) via the standard formula
            //         e = (1/μ) · [(v² - μ/r) · r - (r · v) · v]
            //     amplifies these float quantization artifacts through the v² and (r · v)
            //     terms by approximately the relative-error magnitude (~1e-7 to ~1e-8)
            //
            // Empirically the noise lands around 1.66e-8 for this specific configuration.
            // 1e-6 (~60× margin over observed noise) is loose enough to be robust against
            // future float-conversion-path changes (e.g., Unity rigidbody precision updates)
            // and tight enough to meaningfully detect any real eccentricity above ~10^-6
            // (which is below the meaningful-orbit threshold for any gameplay or test
            // discrimination purpose).
            //
            // DO NOT tighten this back to 1e-9 thinking the noise is a bug. The bug would
            // be passing pure-double values through float Rigidbody storage and expecting
            // double precision back. Pure-double OrbitalElements tests (in OrbitalElementsTests)
            // can and do use 1e-9 because they never touch a Rigidbody.
            Assert.AreEqual(0.0, _vessel.State.KeplerState.Eccentricity, 1e-6,
                "Eccentricity should be ~0 (within float-precision-via-Rigidbody noise) for circular orbit");
        }

        [Test]
        public void TransitionToKeplerRails_ClearsPhysXStateAndRemovesComponents()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, (float)math.sqrt(EarthMu / LeoRadius), 0f);

            _vessel.TransitionToKeplerRails();

            Assert.IsNull(_vessel.State.PhysXState, "PhysXState should be cleared on transition to Kepler-rails");
            Assert.IsNull(_vessel.Rigidbody, "Rigidbody component should be removed");
            Assert.IsNull(_vesselGo.GetComponent<Rigidbody>(), "Rigidbody component should be absent from GameObject");
            Assert.IsNull(_vesselGo.GetComponent<FloatingOriginAnchor>(),
                "FloatingOriginAnchor component should be removed");
        }

        // ----- TransitionToPhysXActive -----

        [Test]
        public void TransitionToPhysXActive_PopulatesPhysXStateAndAddsComponents()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);

            // First go to Kepler-rails so we have a KeplerState to transition back from.
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, (float)math.sqrt(EarthMu / LeoRadius), 0f);
            _vessel.TransitionToKeplerRails();

            // Now back to PhysX-active.
            _vessel.TransitionToPhysXActive();

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
            Assert.IsNotNull(_vessel.State.PhysXState, "PhysXState should be populated on transition to PhysX-active");
            Assert.IsNull(_vessel.State.KeplerState, "KeplerState should be cleared on transition to PhysX-active");

            Assert.IsNotNull(_vessel.Rigidbody, "Rigidbody component should be added");
            Assert.IsNotNull(_vesselGo.GetComponent<FloatingOriginAnchor>(),
                "FloatingOriginAnchor component should be added");
            Assert.IsFalse(_vessel.Rigidbody.useGravity, "Rigidbody useGravity should be false after re-activation");
        }

        // ----- Transition guards (no-op on wrong-mode invocations) -----

        [Test]
        public void TransitionToKeplerRails_WhenAlreadyKeplerRails_LogsWarningAndNoOps()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails);
            // Manually populate KeplerState (Initialize-with-KeplerRails leaves it null since
            // we don't have a PhysX state to compute from). For this guard test the actual
            // contents of KeplerState don't matter.
            _vessel.State.KeplerState = new KeplerState { SemiMajorAxis = LeoRadius, Eccentricity = 0.0 };

            // Capture the expected warning.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*already on Kepler-rails.*"));

            _vessel.TransitionToKeplerRails();

            // State should be unchanged.
            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode);
            Assert.IsNotNull(_vessel.State.KeplerState);
        }

        [Test]
        public void TransitionToPhysXActive_WhenAlreadyPhysXActive_LogsWarningAndNoOps()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            var originalRb = _vessel.Rigidbody;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*already PhysX-active.*"));

            _vessel.TransitionToPhysXActive();

            // State and rigidbody should be unchanged.
            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
            Assert.AreSame(originalRb, _vessel.Rigidbody, "Rigidbody reference should be unchanged on no-op transition");
        }

        // ----- Round-trip preservation -----

        [Test]
        public void RoundTrip_PhysXKeplerPhysX_PreservesPositionAndVelocity()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);

            // Set a non-trivial initial state.
            Vector3 startPos = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            Vector3 startVel = new Vector3(0f, vCircular, 0f);
            _vessel.Rigidbody.position = startPos;
            _vessel.Rigidbody.linearVelocity = startVel;

            // Transition to Kepler-rails and immediately back. Since no propagation
            // occurs and we evaluate at TrueAnomalyAtEpoch (the angle captured at
            // transition), the round-trip should recover position and velocity.
            _vessel.TransitionToKeplerRails();
            _vessel.TransitionToPhysXActive();

            // Position should be preserved to numerical tolerance. Velocity should be
            // preserved similarly. The exact tolerance depends on float-vs-double
            // conversions in the conversion pipeline (Unity rigidbodies are floats,
            // orbital elements are doubles).
            Vector3 endPos = _vessel.Rigidbody.position;
            Vector3 endVel = _vessel.Rigidbody.linearVelocity;

            Assert.AreEqual(startPos.x, endPos.x, math.abs(startPos.x) * 1e-5f, "Round-trip position.x");
            Assert.AreEqual(startPos.y, endPos.y, 1e-3f, "Round-trip position.y (was 0)");
            Assert.AreEqual(startPos.z, endPos.z, 1e-3f, "Round-trip position.z (was 0)");
            Assert.AreEqual(startVel.x, endVel.x, 1e-3f, "Round-trip velocity.x (was 0)");
            Assert.AreEqual(startVel.y, endVel.y, math.abs(startVel.y) * 1e-5f, "Round-trip velocity.y");
            Assert.AreEqual(startVel.z, endVel.z, 1e-3f, "Round-trip velocity.z (was 0)");
        }

        // ----- GetWorldPosition -----

        [Test]
        public void GetWorldPosition_InPhysXActiveMode_ReturnsRigidbodyPosition()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);

            WorldPosition pos = _vessel.GetWorldPosition();
            // FloatingOriginManager.Instance is null in this test, so LocalToWorld returns
            // the local position as-is in world coordinates.
            //
            // X tolerance 1.0m: rigidbody position is Vector3 (float). At LeoRadius ~7e6,
            // float epsilon is ~0.84m, so anything tighter than ~1m is fragile against
            // future float-conversion-path changes. The specific test value 7e6 happens
            // to be exactly representable as float, but tolerance should not depend on
            // that incidental property.
            //
            // Y and Z tolerance 1e-3m: these compare against exactly-representable 0.0f,
            // so float-precision-via-Rigidbody adds no error. 1e-3 is a generous
            // round-off-noise tolerance.
            Assert.AreEqual(LeoRadius, pos.Value.x, 1.0, "GetWorldPosition.x in PhysX-active mode");
            Assert.AreEqual(0.0, pos.Value.y, 1e-3);
            Assert.AreEqual(0.0, pos.Value.z, 1e-3);
        }

        [Test]
        public void GetWorldPosition_InKeplerRailsMode_ComputedFromOrbitalElements()
        {
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, (float)math.sqrt(EarthMu / LeoRadius), 0f);

            _vessel.TransitionToKeplerRails();

            // Now in Kepler-rails. GetWorldPosition propagates from epoch tick to current
            // tick via KeplerPropagator. With no SimTickController instance present
            // (this test doesn't construct one), the propagation tick falls back to
            // EpochTick, making dt == 0 — the propagator short-circuits to the epoch
            // state vector, identical to evaluating at TrueAnomalyAtEpoch.
            // Body's position is (0,0,0) in EditMode (Awake didn't fire), so the returned
            // position equals the orbital position relative to the body.
            WorldPosition pos = _vessel.GetWorldPosition();
            Assert.AreEqual(LeoRadius, pos.Value.x, 1.0, "GetWorldPosition.x in Kepler-rails mode");
        }

        // ----- Propagator integration (commit 040) -----

        [Test]
        public void KeplerRails_GetWorldPosition_AdvancesWithSimTick()
        {
            // Integration test: with a SimTickController present and TickNumber advanced
            // past the vessel's KeplerState.EpochTick, GetWorldPosition should return a
            // *propagated* position — not the entry position. This is the behavior that
            // KeplerPropagator (commit 040) wires up; previously GetWorldPosition always
            // returned the entry-state position regardless of elapsed sim time.

            // Construct a SimTickController so the Vessel's propagation tick lookup hits
            // a real instance instead of falling back to EpochTick. Awake doesn't fire
            // on AddComponent in EditMode, so claim the singleton via the test-only
            // SetInstanceForTesting hook.
            _simTickGo = new GameObject("TestSimTick");
            var controller = _simTickGo.AddComponent<SimTickController>();
            SimTickController.SetInstanceForTesting(controller);
            Assert.AreSame(controller, SimTickController.Instance,
                "SimTickController.Instance must be set for this integration test to be meaningful");

            // Position the vessel at LEO and transition to Kepler-rails. The EpochTick
            // captured at transition is controller.TickNumber, currently 0.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            Vector3 startPos = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.position = startPos;
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);
            _vessel.TransitionToKeplerRails();

            Assert.AreEqual(0L, _vessel.State.KeplerState.EpochTick, "EpochTick should be 0 at transition");

            // Query at tick 0: dt == 0, propagator short-circuits, position == startPos.
            WorldPosition posAtEntry = _vessel.GetWorldPosition();
            Assert.AreEqual(LeoRadius, posAtEntry.Value.x, 1.0,
                "GetWorldPosition at EpochTick should equal entry position");

            // Advance the tick clock by 100 ticks via the test-only setter.
            // At 30 Hz, 100 ticks = 3.333s. Orbital period at LEO ≈ 5828s, so 100 ticks
            // is ~0.057% of one period — well below a full revolution.
            //
            // Tangential velocity is ~7546 m/s; arc-length advance is ~25,150 m
            // (about 25 km). Position should differ from entry by at least 10 km in
            // the y direction (most of the tangential velocity was along y at entry).
            controller.SetTickNumberForTesting(100);
            WorldPosition posAdvanced = _vessel.GetWorldPosition();

            double dx = posAdvanced.Value.x - posAtEntry.Value.x;
            double dy = posAdvanced.Value.y - posAtEntry.Value.y;
            double displacement = math.sqrt(dx * dx + dy * dy);

            // Lower bound: 10 km confirms the vessel has visibly moved.
            // Upper bound: 1,000 km confirms it hasn't accidentally advanced
            // a full revolution (which would put it back near the entry position
            // with a near-zero displacement — a false-positive trap if the upper
            // bound were absent).
            Assert.Greater(displacement, 10_000.0,
                $"Vessel should have advanced >10 km after 100 ticks at LEO; got {displacement:F1} m");
            Assert.Less(displacement, 1_000_000.0,
                $"Vessel advance >1000 km after 100 ticks is implausible at LEO; got {displacement:F1} m");
        }

        [Test]
        public void KeplerRails_TransitionToPhysXActive_UsesPropagatedPosition()
        {
            // Integration test: after sitting on Kepler-rails for some elapsed ticks,
            // the rigidbody position on re-activation should reflect propagated position,
            // NOT the entry position. This is the behavior that KeplerPropagator removes
            // the "rewind on re-activation" Phase 0 limitation.

            _simTickGo = new GameObject("TestSimTick");
            var controller = _simTickGo.AddComponent<SimTickController>();
            SimTickController.SetInstanceForTesting(controller);
            Assert.AreSame(controller, SimTickController.Instance);

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            Vector3 startPos = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.position = startPos;
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);
            _vessel.TransitionToKeplerRails();

            // Advance 100 ticks while on rails.
            controller.SetTickNumberForTesting(100);

            // Transition back. TransitionToPhysXActive should propagate position to
            // tick 100 (3.333s after entry) rather than reading state at TrueAnomalyAtEpoch.
            _vessel.TransitionToPhysXActive();

            Vector3 reactivatedPos = _vessel.Rigidbody.position;

            // Verify the rigidbody position is NOT at the entry position — that would
            // be the old (commit 038 era) "rewind on re-activation" behavior.
            double dx = reactivatedPos.x - startPos.x;
            double dy = reactivatedPos.y - startPos.y;
            double displacement = math.sqrt(dx * dx + dy * dy);

            Assert.Greater(displacement, 10_000.0,
                $"Re-activated rigidbody should have advanced >10 km from entry; got {displacement:F1} m (rewind regression?)");
            Assert.Less(displacement, 1_000_000.0,
                $"Re-activated rigidbody advance >1000 km after 100 ticks is implausible; got {displacement:F1} m");
        }
    }
}
