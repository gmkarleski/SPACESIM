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
using static SpaceSim.Foundation.Vessels.Tests.VesselTestHelpers;

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
        // LeoRadius moved to VesselTestHelpers (imported via `using static`).

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
            BodyRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();
            VesselTransitionDriver.Shutdown();
            VesselSoiRerootingDriver.Shutdown();
            VesselEventPredictionDriver.Shutdown();

            // ReferenceBody MonoBehaviour. Awake doesn't fire in EditMode, so the body's
            // BodyId stays Guid.Empty and PositionWorld stays default(WorldPosition).
            // For most tests this is fine because the math doesn't care; tests that need
            // a real BodyId assign one manually after AddComponent.
            _bodyGo = new GameObject("TestReferenceBody");
            _body = _bodyGo.AddComponent<ReferenceBody>();

            // Default test body is a POINT MASS for tests that don't care about surface
            // or atmosphere. Reflection-set surfaceRadiusMeters = 1.0 so the
            // SurfaceImpactPredictor (commit 047) doesn't fire on standard test orbits
            // (LEO scale 7e6 m never reaches a 1 m surface). atmosphericTopAltitudeMeters
            // stays at the field default 0.0 (vacuum body) which the
            // AtmosphericEntryPredictor already handles correctly (returns null).
            //
            // CARVE-OUT (reflection migration sweep, operational commit): SetUp
            // deliberately sets surfaceRadiusMeters via reflection WITHOUT calling
            // InitializeBodyForTesting afterward. The parameterized
            // InitializeBodyForTesting(massKg, soiRadiusMeters, parentBody,
            // surfaceRadiusMeters, atmosphericTopAltitudeMeters) overload always
            // invokes the init path; many tests downstream rely on calling Init
            // themselves at test time after additional per-test configuration (e.g.,
            // SetEarthFiniteSoiAndInitialize / SetEarthSurfaceAtmosphereAndInitialize
            // / BuildMoonAsChildOfEarth elsewhere in this file). Pre-init
            // field-setting without init is the reason this site stays as reflection
            // while the file's other 9 reflection sites migrated to the parameterized
            // overload.
            {
                var surfaceField = typeof(ReferenceBody).GetField(
                    "surfaceRadiusMeters",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                surfaceField.SetValue(_body, 1.0);
            }

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
            BodyRegistry.ClearForTesting();
            FloatingOriginManager.ClearInstanceForTesting();
            SimTickController.ClearInstanceForTesting();
            VesselTransitionDriver.Shutdown();
            VesselSoiRerootingDriver.Shutdown();
            VesselEventPredictionDriver.Shutdown();
        }

        // ----- Helpers -----

        // NewState / NewKeplerState / BuildMoonAsChildOfEarth helpers consolidated
        // into VesselTestHelpers (imported via `using static` at top of file).
        // Earth-Moon constants (EarthMoonDistanceMeters / MoonMassKg / MoonSoiRadiusMeters)
        // and the LeoRadius constant also moved there. Local EarthMassKg + EarthMu
        // declarations stay pending a separate 8-file constant consolidation commit
        // flagged in the test infrastructure sweep's "What's next" follow-ons.

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
            // Updated in commit 042: uses the 4-arg overload with a default KeplerState.
            // The 3-arg overload now rejects KeplerRails (would fall back to PhysXActive),
            // which would invert this test's assertions.
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, NewKeplerState(_body));

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
            // Updated in commit 042: uses the 4-arg overload, which populates State.KeplerState
            // from the parameter. Pre-commit-042 this was Initialize-in-KeplerRails (3-arg)
            // followed by a manual State.KeplerState assignment workaround for the now-fixed
            // state-inconsistency bug. For this guard test the actual contents of KeplerState
            // don't matter beyond non-null.
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, NewKeplerState(_body));

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

        // ----- §3.1 mode transition procedure tests (commit 041) -----
        //
        // These tests exercise the PhysX-active ↔ Kepler-rails transition PROCEDURES
        // under each §3.1 condition that can be constructed in Phase 0. They do NOT
        // test trigger evaluation — no code in this prototype evaluates whether a
        // vessel should transition modes; transitions only happen when explicitly
        // invoked. Per-sim-tick trigger evaluation per §3.1 lands as Phase 1 work
        // (see PHASE_TRACKER.md Phase 1 section after commit 041).
        //
        // Several conditions in §3.1 cannot be fully exercised in Phase 0 because
        // the underlying authoritative-state fields are not yet implemented:
        // thrust state, atmospheric context, contact forces, player focus,
        // multi-vessel proximity. Tests covering those conditions are
        // documentation-shaped: identical setup to the geometric-condition tests,
        // distinct §3.1 condition mapping in their names and the PHASE 0 NOTE
        // comment. They become substantive tests in Phase 1 when the underlying
        // state fields exist.

        // ----- PhysX-active → Kepler-rails procedure tests (one per §3.1 condition) -----

        [Test]
        public void TransitionToKeplerRails_AtBeyond50kmFromOrigin_Succeeds()
        {
            // §3.1 condition: "The vessel is more than 50 km from any active vessel
            // (the active-vessel threshold, per commit 002 floating origin)."
            //
            // In Phase 0, there is no active-vessel registry beyond the single
            // TestVessel; the 50 km proxy is geometric distance from the reference
            // body's world position. LeoRadius (7,000,000 m) is ~140× the 50 km
            // threshold — well into the "Kepler-rails-eligible" regime.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);

            _vessel.TransitionToKeplerRails();

            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode,
                "Vessel >50km from origin should transition successfully");
            Assert.IsNotNull(_vessel.State.KeplerState, "KeplerState should be populated");
            Assert.AreEqual(LeoRadius, _vessel.State.KeplerState.SemiMajorAxis, 1.0,
                "Semi-major axis should equal LEO radius");
        }

        [Test]
        public void TransitionToKeplerRails_WithZeroThrust_Succeeds()
        {
            // §3.1 condition: "AND no thrust is being applied (engines off)."
            //
            // PHASE 0 NOTE: §3.1 condition for thrust cannot be fully exercised
            // in Phase 0 because the underlying state field is not yet implemented.
            // This test verifies the transition procedure succeeds under the
            // geometric/kinematic conditions we CAN construct. Trigger evaluation
            // per §3.1 lands in Phase 1.
            //
            // PhysXState.ActiveThrustN exists as a field in the schema but is
            // populated to 0.0 on transition and never read by the transition
            // procedure itself — there is no "is thrust active" check in
            // TransitionToKeplerRails. A future Phase 1 trigger evaluator will
            // read this field; for now we just confirm the no-thrust setup
            // succeeds the procedure.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);

            _vessel.TransitionToKeplerRails();

            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode);
            Assert.IsNotNull(_vessel.State.KeplerState);
        }

        [Test]
        public void TransitionToKeplerRails_AboveAtmosphericBoundary_Succeeds()
        {
            // §3.1 condition: "AND no atmospheric drag is significant (altitude
            // above atmospheric boundary OR atmospheric_density < threshold)."
            //
            // PHASE 0 NOTE: §3.1 condition for atmospheric drag cannot be fully
            // exercised in Phase 0 because the underlying state field is not yet
            // implemented. This test verifies the transition procedure succeeds
            // under the geometric/kinematic conditions we CAN construct. Trigger
            // evaluation per §3.1 lands in Phase 1.
            //
            // LEO radius (7,000,000 m) is ~600 km above Earth's surface, well above
            // the Karman line (~100 km). PhysXState.AtmosphericDensity and
            // AtmosphericVelocityRel exist as schema fields but are populated to
            // zero on transition and never read by the procedure.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);

            _vessel.TransitionToKeplerRails();

            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode);
            Assert.IsNotNull(_vessel.State.KeplerState);
        }

        [Test]
        public void TransitionToKeplerRails_NoContactForces_Succeeds()
        {
            // §3.1 condition: "AND no contact forces are active (not landed, not
            // docked to a PhysX-active vessel)."
            //
            // PHASE 0 NOTE: §3.1 condition for contact forces cannot be fully
            // exercised in Phase 0 because the underlying state field is not yet
            // implemented. This test verifies the transition procedure succeeds
            // under the geometric/kinematic conditions we CAN construct. Trigger
            // evaluation per §3.1 lands in Phase 1.
            //
            // No docking or landing state exists in the Phase 0 schema. A vessel
            // at LeoRadius with no Unity Collider-based contacts is implicitly
            // "in free space" by virtue of its construction.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);

            _vessel.TransitionToKeplerRails();

            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode);
            Assert.IsNotNull(_vessel.State.KeplerState);
        }

        [Test]
        public void TransitionToKeplerRails_WellDefinedTrajectory_Succeeds()
        {
            // §3.1 condition: "AND the vessel's trajectory is well-defined by
            // patched conics around a single dominant body."
            //
            // Unlike conditions 2-4, this one CAN be exercised in Phase 0: the
            // OrbitalElements.ComputeFromStateVector call produces a valid
            // elliptical orbit when state-vector inputs are well-defined. This
            // test uses a non-circular elliptical orbit (perigee 7,000 km, apogee
            // 8,000 km) to confirm the procedure handles e>0 correctly, not just
            // the e=0 special case covered by the other tests.
            //
            // For an ellipse with rp=7e6, ra=8e6: a = (rp+ra)/2 = 7.5e6;
            // e = (ra-rp)/(ra+rp) = 1/15 ≈ 0.0667; v at perigee from vis-viva:
            // v = sqrt(μ · (2/rp - 1/a)) ≈ sqrt(3.987e14 · (2.857e-7 - 1.333e-7))
            //   ≈ sqrt(6.08e7) ≈ 7798 m/s — slightly faster than circular at rp.
            double a = 7.5e6;
            double rp = 7.0e6;
            double vPerigee = math.sqrt(EarthMu * (2.0 / rp - 1.0 / a));

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)rp, 0f, 0f);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, (float)vPerigee, 0f);

            _vessel.TransitionToKeplerRails();

            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode);
            Assert.IsNotNull(_vessel.State.KeplerState);
            // Semi-major axis tolerance: 1 km absolute (~1e-4 relative at 7.5e6).
            // Eccentricity tolerance per the established 1e-6 pattern (float-
            // precision-through-Rigidbody noise; see TransitionToKeplerRails_
            // PopulatesKeplerStateWithRealOrbitalElements for the full derivation).
            Assert.AreEqual(a, _vessel.State.KeplerState.SemiMajorAxis, 1000.0,
                "Semi-major axis should equal 7.5e6 m");
            Assert.AreEqual(1.0 / 15.0, _vessel.State.KeplerState.Eccentricity, 1e-4,
                "Eccentricity should equal 1/15 ≈ 0.0667");
        }

        // ----- Kepler-rails → PhysX-active procedure tests (one per §3.1 trigger) -----

        [Test]
        public void TransitionToPhysXActive_Within50kmOfOrigin_Succeeds()
        {
            // §3.1 trigger: "The vessel enters within 50 km of any active vessel
            // (the active-vessel proximity threshold)."
            //
            // PHASE 0 NOTE: the active-vessel proximity check requires a
            // multi-vessel registry that doesn't yet exist; this test exercises
            // the procedure with a single vessel transitioning back to PhysX-active
            // after a brief Kepler-rails period. The "proximity" semantics are
            // proxied by the procedure succeeding: the rigidbody is recreated with
            // propagated state. Trigger evaluation per §3.1 lands in Phase 1.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);
            _vessel.TransitionToKeplerRails();

            _vessel.TransitionToPhysXActive();

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
            Assert.IsNotNull(_vessel.Rigidbody, "Rigidbody should be recreated on re-activation");
            Assert.IsNotNull(_vessel.State.PhysXState, "PhysXState should be populated");
            Assert.IsNull(_vessel.State.KeplerState, "KeplerState should be cleared");
        }

        [Test]
        public void TransitionToPhysXActive_AtmosphericEntryPredicted_Succeeds()
        {
            // §3.1 trigger: "The vessel's trajectory predicts atmospheric entry
            // within the next sim-tick (the pre-computed next_atmospheric_entry_tick
            // fires)."
            //
            // PHASE 0 / TEST-CONSTRUCTION NOTE: §3.1 condition for atmospheric-entry
            // prediction cannot be fully exercised by this test because the body in
            // SetUp uses the surfaceRadiusMeters=1.0 point-mass convention with no
            // atmosphere configured; the atmospheric-entry predictor returns null on
            // this body. This test verifies the transition procedure succeeds under
            // the geometric/kinematic conditions we CAN construct. Real driver-fired
            // trigger evaluation is covered separately by
            // EvaluateTransitionTriggers_KeplerRails_AtmosphericEntryPredicted_SuggestsPhysXActive
            // (which writes NextAtmosphericEntryTick directly).
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);
            _vessel.TransitionToKeplerRails();

            // Confirm the test-construction invariant — both new mode-transition
            // fields stay null (commit 048 Stage 1 field-split) — as a sanity check
            // that this test's "trigger condition" cannot actually fire in the
            // current body setup.
            Assert.IsNull(_vessel.State.KeplerState.NextAtmosphericEntryTick,
                "Test setup: NextAtmosphericEntryTick should be null on point-mass body");
            Assert.IsNull(_vessel.State.KeplerState.NextSurfaceImpactTick,
                "Test setup: NextSurfaceImpactTick should be null on surfaceRadius=1.0 body");

            _vessel.TransitionToPhysXActive();

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
            Assert.IsNotNull(_vessel.Rigidbody);
        }

        [Test]
        public void TransitionToPhysXActive_PlayerFocusSwitch_Succeeds()
        {
            // §3.1 trigger: "The player switches focus to the vessel (player
            // attention pulls vessels into PhysX-active)."
            //
            // PHASE 0 NOTE: §3.1 condition for player focus cannot be fully
            // exercised in Phase 0 because the underlying mechanism (camera /
            // input / focus subsystem) is not yet implemented. This test verifies
            // the transition procedure succeeds under the geometric/kinematic
            // conditions we CAN construct. Trigger evaluation per §3.1 lands in
            // Phase 1.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);
            _vessel.TransitionToKeplerRails();

            _vessel.TransitionToPhysXActive();

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
            Assert.IsNotNull(_vessel.Rigidbody);
        }

        [Test]
        public void TransitionToPhysXActive_ScriptedThrust_Succeeds()
        {
            // §3.1 trigger: "The vessel reaches a scripted mode change (Vizzy
            // script triggering thrust, for example)."
            //
            // PHASE 0 NOTE: §3.1 condition for scripted mode change cannot be
            // fully exercised in Phase 0 because the underlying mechanism (Vizzy
            // / scripting subsystem) is not yet implemented; Vizzy ships in Phase
            // 5. This test verifies the transition procedure succeeds under the
            // geometric/kinematic conditions we CAN construct. Trigger evaluation
            // per §3.1 lands in Phase 1; Vizzy integration lands in Phase 5.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);
            _vessel.TransitionToKeplerRails();

            _vessel.TransitionToPhysXActive();

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
            Assert.IsNotNull(_vessel.Rigidbody);
        }

        [Test]
        public void TransitionToPhysXActive_MultiVesselProximityCluster_Succeeds()
        {
            // §3.1 trigger: "Multi-vessel proximity events (multiple Kepler-rails
            // vessels in a 50 km cluster; resolve by computing relative positions
            // and activating the largest cluster)."
            //
            // PHASE 0 NOTE: §3.1 condition for multi-vessel proximity cannot be
            // fully exercised in Phase 0 because the underlying detection
            // (VesselRegistry exists but no proximity-clustering logic does).
            // This test verifies the transition procedure succeeds under the
            // geometric/kinematic conditions we CAN construct. Trigger evaluation
            // per §3.1 lands in Phase 1.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);
            _vessel.TransitionToKeplerRails();

            _vessel.TransitionToPhysXActive();

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
            Assert.IsNotNull(_vessel.Rigidbody);
        }

        // ----- Edge case and error path tests -----

        [Test]
        public void TransitionToKeplerRails_WhenInterstellarCruise_LogsErrorAndNoOps()
        {
            // §3.1 / §3.3 implication: direct InterstellarCruise → KeplerRails
            // transition is rejected with an error log. Phase 6 scope per the
            // implementation comment in Vessel.cs:196-202.
            //
            // Construction note: Vessel.Initialize rewrites initialMode ==
            // InterstellarCruise to PhysXActive with an error log (Vessel.cs:
            // 123-130). To exercise the transition's rejection path, we Initialize
            // in PhysXActive (normal path) then directly assign State.Mode =
            // InterstellarCruise to force the rejection state. This bypasses
            // Initialize's mode-rewrite, which is the correct test scaffolding
            // for the procedure-rejection path (we are not testing Initialize
            // here, we are testing TransitionToKeplerRails's response to
            // InterstellarCruise mode).
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.State.Mode = PhysicsMode.InterstellarCruise;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*InterstellarCruise → KeplerRails.*"));

            _vessel.TransitionToKeplerRails();

            Assert.AreEqual(PhysicsMode.InterstellarCruise, _vessel.Mode,
                "Mode should remain InterstellarCruise after rejected transition");
        }

        [Test]
        public void TransitionToPhysXActive_WhenInterstellarCruise_LogsErrorAndNoOps()
        {
            // §3.1 / §3.3 implication: direct InterstellarCruise → PhysXActive
            // transition is rejected with an error log. Phase 6 scope per the
            // implementation comment in Vessel.cs:298-302.
            //
            // Construction note (same as the symmetric test above): Initialize in
            // PhysXActive then directly assign State.Mode = InterstellarCruise to
            // force the rejection state, bypassing Initialize's mode-rewrite. We
            // are testing the transition's rejection, not Initialize's behavior.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.State.Mode = PhysicsMode.InterstellarCruise;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*InterstellarCruise → PhysXActive.*"));

            _vessel.TransitionToPhysXActive();

            Assert.AreEqual(PhysicsMode.InterstellarCruise, _vessel.Mode);
        }

        [Test]
        public void TransitionToKeplerRails_WhenRigidbodyNull_LogsErrorAndNoOps()
        {
            // State-inconsistency case: vessel in PhysXActive mode but rigidbody
            // missing. Initialize forces the component shape to match the mode,
            // so we have to construct this state by destroying the rigidbody
            // post-Initialize. The cached _rb field in Vessel becomes a destroyed
            // UnityObject reference; Unity's overloaded == operator treats this as
            // equal to null, so the guard at Vessel.cs:203 fires correctly.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            Assert.IsNotNull(_vessel.Rigidbody, "Sanity: rigidbody present after Initialize");

            // Destroy the rigidbody. Vessel's _rb field is now a destroyed
            // UnityObject reference (==null per Unity's overload).
            UnityObject.DestroyImmediate(_vessel.Rigidbody);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*Rigidbody is null.*State is inconsistent.*"));

            _vessel.TransitionToKeplerRails();

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode,
                "Mode should remain PhysXActive after rejected transition");
            Assert.IsNull(_vessel.State.KeplerState,
                "KeplerState should NOT be populated after rejected transition");
        }

        [Test]
        public void TransitionToPhysXActive_WhenKeplerStateNull_LogsErrorAndNoOps()
        {
            // State-inconsistency case: vessel in KeplerRails mode but KeplerState
            // is null. This state is intentionally hard to construct — commit 042's
            // Initialize fix ensures that the normal Initialize paths cannot produce
            // it (Mode == KeplerRails ⟹ KeplerState != null after Initialize).
            //
            // CONSTRUCTION (post-commit-042): the test directly builds the inconsistent
            // state by:
            //   1. Initialize in PhysXActive (the only path that doesn't require
            //      mode-specific state).
            //   2. DestroyImmediate the Rigidbody and FloatingOriginAnchor that
            //      ConfigureForPhysXActive added (otherwise the test's "Rigidbody
            //      should NOT be added after rejected transition" assertion would
            //      conflate "didn't add a new one" with "no rigidbody exists").
            //   3. Set State.Mode = KeplerRails and State.KeplerState = null
            //      directly, bypassing Initialize's invariant-maintenance.
            //   4. Attempt the transition; verify the guard at Vessel.cs:305 fires.
            //
            // This is what "constructing the state-inconsistency directly" looks like
            // now that Initialize-in-KeplerRails no longer produces it. The guard
            // itself stays as defense-in-depth even though the production code can
            // no longer reach the inconsistency through normal API calls.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);

            // Clean up the components that PhysXActive Initialize added; otherwise the
            // post-transition rigidbody-null assertion would fail trivially.
            UnityObject.DestroyImmediate(_vesselGo.GetComponent<FloatingOriginAnchor>());
            UnityObject.DestroyImmediate(_vesselGo.GetComponent<Rigidbody>());

            // Force the inconsistent state.
            _vessel.State.Mode = PhysicsMode.KeplerRails;
            _vessel.State.KeplerState = null;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*KeplerState is null.*State is inconsistent.*"));

            _vessel.TransitionToPhysXActive();

            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode,
                "Mode should remain KeplerRails after rejected transition");
            Assert.IsNull(_vesselGo.GetComponent<Rigidbody>(),
                "Rigidbody should NOT be added after rejected transition");
        }

        [Test]
        public void TransitionToKeplerRails_BeforeInitialize_LogsWarningAndNoOps()
        {
            // Lifecycle case: TransitionToKeplerRails called on a vessel that
            // hasn't been Initialized. Vessel.cs:186-190 short-circuits with a
            // warning log.
            //
            // SetUp constructed _vessel via AddComponent<Vessel>(); Initialize has
            // not been called. _initialized is false.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*before Initialize; ignored.*"));

            _vessel.TransitionToKeplerRails();

            // No state to verify (State is null pre-Initialize); the test passes
            // by reaching this assertion without exceptions and with the expected
            // log fired.
            Assert.IsNull(_vessel.State, "State should remain null after rejected pre-Initialize transition");
        }

        [Test]
        public void TransitionToPhysXActive_BeforeInitialize_LogsWarningAndNoOps()
        {
            // Symmetric lifecycle case: TransitionToPhysXActive called on a
            // vessel that hasn't been Initialized. Vessel.cs:288-292 short-circuits
            // with a warning log.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*before Initialize; ignored.*"));

            _vessel.TransitionToPhysXActive();

            Assert.IsNull(_vessel.State, "State should remain null after rejected pre-Initialize transition");
        }

        // ----- Stability tests -----

        [Test]
        public void MultipleRoundTrips_PhysXKeplerPhysXKepler_PreservesPosition()
        {
            // Stress test: three full PhysX → Kepler → PhysX round trips (six
            // transitions total) should preserve position and velocity through
            // float-precision-through-Rigidbody noise accumulation.
            //
            // Each transition routes through Vector3 (float, ~7 digits at 7e6 m
            // → ~0.84 m float epsilon). Six conversions accumulate worst-case
            // ~6 m position error and ~5e-3 m/s velocity error.
            //
            // Tolerances chosen above worst-case accumulation:
            //   - Position: 10 m absolute (~1.4e-6 relative at LeoRadius)
            //   - Velocity: 1e-2 m/s absolute (~1.3e-6 relative at vCircular)
            // Eccentricity not checked — float-precision-through-Rigidbody noise
            // can push e from 0 toward ~1e-5 cumulatively across multiple
            // transitions; the position/velocity bounds are the operationally
            // meaningful check for stability across mode flips.
            //
            // SimTickController NOT constructed → propagator falls back to
            // EpochTick → dt=0 every transition → no propagation drift, only
            // float-conversion drift.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            Vector3 startPos = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            Vector3 startVel = new Vector3(0f, vCircular, 0f);
            _vessel.Rigidbody.position = startPos;
            _vessel.Rigidbody.linearVelocity = startVel;

            for (int i = 0; i < 3; i++)
            {
                _vessel.TransitionToKeplerRails();
                _vessel.TransitionToPhysXActive();
            }

            Vector3 endPos = _vessel.Rigidbody.position;
            Vector3 endVel = _vessel.Rigidbody.linearVelocity;

            Assert.AreEqual(startPos.x, endPos.x, 10.0f,
                $"Position.x drift after 3 round trips: expected ~{startPos.x}, got {endPos.x}");
            Assert.AreEqual(startPos.y, endPos.y, 10.0f, "Position.y drift after 3 round trips");
            Assert.AreEqual(startPos.z, endPos.z, 10.0f, "Position.z drift after 3 round trips");
            Assert.AreEqual(startVel.x, endVel.x, 1e-2f, "Velocity.x drift after 3 round trips");
            Assert.AreEqual(startVel.y, endVel.y, 1e-2f, "Velocity.y drift after 3 round trips");
            Assert.AreEqual(startVel.z, endVel.z, 1e-2f, "Velocity.z drift after 3 round trips");

            // Verify the vessel is in PhysXActive mode at the end (odd number
            // of pairs ending on TransitionToPhysXActive → final mode is
            // PhysXActive).
            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode,
                "After three (Kepler, PhysX) pairs, final mode should be PhysXActive");
        }

        [Test]
        public void Transitions_SetLastAdvancedTickToCurrentTick()
        {
            // Bookkeeping test: every transition sets both ModeEnteredAtTick and
            // LastAdvancedTick to the current sim-tick. With a SimTickController
            // present and TickNumber advanced between transitions, we verify
            // both fields track correctly.
            //
            // (Renamed from the spec's "PreservesLastAdvancedTick" because the
            // value is SET to current tick on every transition, not preserved
            // across transitions — clearer test name matches the behavior.)
            _simTickGo = new GameObject("TestSimTick");
            var controller = _simTickGo.AddComponent<SimTickController>();
            SimTickController.SetInstanceForTesting(controller);

            // Tick 0: Initialize in PhysXActive.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            Assert.AreEqual(0L, _vessel.State.ModeEnteredAtTick,
                "ModeEnteredAtTick should be 0 at Initialize");
            Assert.AreEqual(0L, _vessel.State.LastAdvancedTick,
                "LastAdvancedTick should be 0 at Initialize");

            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            float vCircular = (float)math.sqrt(EarthMu / LeoRadius);
            _vessel.Rigidbody.linearVelocity = new Vector3(0f, vCircular, 0f);

            // Advance to tick 50, transition to Kepler-rails.
            controller.SetTickNumberForTesting(50);
            _vessel.TransitionToKeplerRails();
            Assert.AreEqual(50L, _vessel.State.ModeEnteredAtTick,
                "ModeEnteredAtTick should be 50 after transition at tick 50");
            Assert.AreEqual(50L, _vessel.State.LastAdvancedTick,
                "LastAdvancedTick should be 50 after transition at tick 50");

            // Advance to tick 150, transition back to PhysX-active.
            controller.SetTickNumberForTesting(150);
            _vessel.TransitionToPhysXActive();
            Assert.AreEqual(150L, _vessel.State.ModeEnteredAtTick,
                "ModeEnteredAtTick should be 150 after transition at tick 150");
            Assert.AreEqual(150L, _vessel.State.LastAdvancedTick,
                "LastAdvancedTick should be 150 after transition at tick 150");
        }

        // ----- Initialize 4-arg overload (commit 042) -----

        [Test]
        public void Initialize_With4ArgOverload_KeplerRails_PopulatesKeplerState()
        {
            // Happy path: 4-arg Initialize with KeplerRails + a real KeplerState
            // populates State.KeplerState and sets Mode == KeplerRails.
            var inputKepler = NewKeplerState(_body);
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, inputKepler);

            Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode);
            Assert.IsNotNull(_vessel.State.KeplerState,
                "State.KeplerState should be non-null after Initialize(KeplerRails, NewKeplerState())");
            Assert.AreSame(inputKepler, _vessel.State.KeplerState,
                "State.KeplerState should reference the caller-provided KeplerState instance");
        }

        [Test]
        public void Initialize_With4ArgOverload_KeplerRails_NullKeplerState_LogsErrorAndFallsBack()
        {
            // Error path: 4-arg Initialize with KeplerRails + null KeplerState logs
            // error and falls back to PhysXActive (preserves schema invariant — vessel
            // ends up in PhysXActive mode with no Kepler state).
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*KeplerRails mode with null initialKeplerState.*"));

            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, initialKeplerState: null);

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode,
                "Mode should fall back to PhysXActive on null initialKeplerState");
            Assert.IsNull(_vessel.State.KeplerState,
                "State.KeplerState should be null in the fallback PhysXActive state");
            Assert.IsNotNull(_vessel.Rigidbody,
                "Rigidbody should be added per the PhysXActive fallback component shape");
        }

        [Test]
        public void Initialize_With4ArgOverload_PhysXActive_LogsErrorAndIgnoresKeplerState()
        {
            // Overload misuse: 4-arg Initialize with PhysXActive logs error and
            // proceeds in PhysXActive mode with initialKeplerState ignored. The error
            // exists so the caller knows they used the wrong overload; the proceed
            // semantics exist so the vessel ends up in a valid state regardless.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*4-arg overload with PhysicsMode.PhysXActive.*"));

            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive, NewKeplerState(_body));

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
            Assert.IsNull(_vessel.State.KeplerState,
                "initialKeplerState should be ignored when initialMode is PhysXActive");
            Assert.IsNotNull(_vessel.Rigidbody, "Rigidbody should be added");
        }

        [Test]
        public void Initialize_With3ArgOverload_KeplerRails_LogsErrorAndFallsBack()
        {
            // Post-commit-042 behavior: the 3-arg overload now rejects KeplerRails
            // because constructing a Kepler-rails vessel requires caller-provided
            // orbital elements. Falls back to PhysXActive, parallel to the existing
            // InterstellarCruise rejection.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*KeplerRails mode via the 3-arg overload.*"));

            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails);

            Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode,
                "Mode should fall back to PhysXActive (3-arg overload doesn't accept KeplerRails)");
            Assert.IsNull(_vessel.State.KeplerState);
            Assert.IsNotNull(_vessel.Rigidbody);
        }

        [Test]
        public void Initialize_With4ArgOverload_KeplerRails_DoesNotAddRigidbodyOrAnchor()
        {
            // Component-shape invariant: 4-arg Initialize with KeplerRails does NOT
            // add a Rigidbody or FloatingOriginAnchor (those are PhysXActive's shape).
            // Symmetric to Initialize_InKeplerRailsMode_DoesNotAddRigidbodyOrAnchor
            // which covers the same property under a different focus (that test
            // asserts component shape; this one asserts component shape under the
            // 4-arg overload specifically).
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, NewKeplerState(_body));

            Assert.IsNull(_vessel.Rigidbody, "Rigidbody should not be present in Kepler-rails mode");
            Assert.IsNull(_vesselGo.GetComponent<FloatingOriginAnchor>(),
                "FloatingOriginAnchor should not be present in Kepler-rails mode");
        }

        [Test]
        public void Initialize_With4ArgOverload_KeplerRails_PreservesProvidedElements()
        {
            // Field-by-field check: every orbital element the caller passes in
            // appears unchanged on State.KeplerState. Catches any future regression
            // where InitializeCore inadvertently overwrites a field.
            var inputKepler = new KeplerState
            {
                SemiMajorAxis = 8_500_000.0,
                Eccentricity = 0.15,
                Inclination = 0.5,
                LongitudeOfAscendingNode = 1.2,
                ArgumentOfPeriapsis = 0.7,
                TrueAnomalyAtEpoch = 1.5,
                EpochTick = 42,
                ReferenceBodyId = Guid.NewGuid(),
            };

            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, inputKepler);

            Assert.AreEqual(inputKepler.SemiMajorAxis, _vessel.State.KeplerState.SemiMajorAxis);
            Assert.AreEqual(inputKepler.Eccentricity, _vessel.State.KeplerState.Eccentricity);
            Assert.AreEqual(inputKepler.Inclination, _vessel.State.KeplerState.Inclination);
            Assert.AreEqual(inputKepler.LongitudeOfAscendingNode, _vessel.State.KeplerState.LongitudeOfAscendingNode);
            Assert.AreEqual(inputKepler.ArgumentOfPeriapsis, _vessel.State.KeplerState.ArgumentOfPeriapsis);
            Assert.AreEqual(inputKepler.TrueAnomalyAtEpoch, _vessel.State.KeplerState.TrueAnomalyAtEpoch);
            Assert.AreEqual(inputKepler.EpochTick, _vessel.State.KeplerState.EpochTick);
            Assert.AreEqual(inputKepler.ReferenceBodyId, _vessel.State.KeplerState.ReferenceBodyId);
        }

        // ----- Trigger evaluation: EvaluateTransitionTriggers (commit 043) -----

        // StubActiveVessel + ThrowingActiveVessel stub types extracted to
        // VesselTestHelpers (resolved via the namespace's regular `using`).
        // Used here by the EvaluateTransitionTriggers tests and previously also
        // by the VesselTransitionDriver tests (migrated to
        // VesselTransitionDriverTests.cs in the test infrastructure sweep).

        [Test]
        public void EvaluateTransitionTriggers_BeforeInitialize_ReturnsStay()
        {
            // _vessel has been AddComponent'd in SetUp but Initialize has NOT been called.
            // The evaluator must short-circuit on !_initialized before touching State,
            // which is null at this point.
            var activeRef = new StubActiveVessel(new WorldPosition(0, 0, 0));
            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeRef);

            Assert.IsFalse(result.SuggestedMode.HasValue,
                "Pre-Initialize evaluation should return Stay (no suggestion)");
            Assert.AreEqual(TransitionTriggerReason.None, result.Reason);
        }

        [Test]
        public void EvaluateTransitionTriggers_PhysXActive_BeyondProximity_AllConditionsPass_SuggestsKeplerRails()
        {
            // The happy path: PhysXActive vessel at LEO, active reference at origin
            // (LeoRadius = 7,000,000 m, well past 50 km proximity threshold), default
            // stub thrust/atmosphere fields (zero), valid ReferenceBody.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);

            // Active vessel reference at (0,0,0) — far from our LEO vessel.
            var activeRef = new StubActiveVessel(new WorldPosition(0, 0, 0));
            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeRef);

            Assert.IsTrue(result.SuggestedMode.HasValue,
                "All five §3.1 conditions hold → suggestion should be non-null");
            Assert.AreEqual(PhysicsMode.KeplerRails, result.SuggestedMode.Value);
            Assert.AreEqual(TransitionTriggerReason.BeyondProximityWithCleanState, result.Reason);
        }

        [Test]
        public void EvaluateTransitionTriggers_PhysXActive_WithinProximity_SuggestsStay()
        {
            // PhysXActive vessel within 50 km of the active reference. Proximity fails,
            // so the conjunction fails (one falsy condition is enough). Result: Stay.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3(10_000f, 0f, 0f);  // 10 km from origin

            var activeRef = new StubActiveVessel(new WorldPosition(0, 0, 0));
            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeRef);

            Assert.IsFalse(result.SuggestedMode.HasValue, "10 km < 50 km threshold → Stay");
            Assert.AreEqual(TransitionTriggerReason.None, result.Reason);
        }

        [Test]
        public void EvaluateTransitionTriggers_PhysXActive_WithThrust_SuggestsStay()
        {
            // Vessel beyond proximity threshold, but with thrust applied (we manually
            // populate the stub field). Conjunction fails on thrust; result: Stay.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            // After Initialize, PhysXState is null until TransitionToPhysXActive
            // populates it. For this test we populate State.PhysXState manually with
            // non-zero thrust.
            _vessel.State.PhysXState = new PhysXState { ActiveThrustN = 1000.0 };

            var activeRef = new StubActiveVessel(new WorldPosition(0, 0, 0));
            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeRef);

            Assert.IsFalse(result.SuggestedMode.HasValue,
                "Thrust > 0 → HasNoThrust returns false → conjunction fails → Stay");
            Assert.AreEqual(TransitionTriggerReason.None, result.Reason);
        }

        [Test]
        public void EvaluateTransitionTriggers_PhysXActive_InAtmosphere_SuggestsStay()
        {
            // Vessel beyond proximity, no thrust, but in atmosphere (manually populated
            // AtmosphericDensity above the 1e-6 threshold). Conjunction fails.
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);
            _vessel.State.PhysXState = new PhysXState
            {
                ActiveThrustN = 0.0,
                AtmosphericDensity = 1.0,  // way above the 1e-6 threshold
            };

            var activeRef = new StubActiveVessel(new WorldPosition(0, 0, 0));
            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeRef);

            Assert.IsFalse(result.SuggestedMode.HasValue,
                "Atmospheric density above threshold → HasNoSignificantAtmosphericDrag false → Stay");
            Assert.AreEqual(TransitionTriggerReason.None, result.Reason);
        }

        [Test]
        public void EvaluateTransitionTriggers_KeplerRails_WithinProximity_SuggestsPhysXActive_WithProximityReason()
        {
            // KeplerRails vessel within 50 km of active reference. The first
            // disjunctive condition fires.
            // EpochTick = 0; with no SimTickController, propagation returns epoch state
            // (dt = 0); position of the orbit at TrueAnomaly = 0 is (a, 0, 0) = LeoRadius.
            // We place the active reference at LeoRadius too (proximity 0).
            var kepler = NewKeplerState(_body);
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            var activeRef = new StubActiveVessel(new WorldPosition(LeoRadius, 0, 0));
            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeRef);

            Assert.IsTrue(result.SuggestedMode.HasValue,
                "Proximity 0 < 50 km → fires ProximityToActiveVessel");
            Assert.AreEqual(PhysicsMode.PhysXActive, result.SuggestedMode.Value);
            Assert.AreEqual(TransitionTriggerReason.ProximityToActiveVessel, result.Reason);
        }

        [Test]
        public void EvaluateTransitionTriggers_KeplerRails_BeyondProximity_SuggestsStay()
        {
            // KeplerRails vessel beyond 50 km of active reference, and none of the
            // stub disjunctive conditions fire (all stub-false in Phase 0). Result: Stay.
            var kepler = NewKeplerState(_body);
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            // Active reference at origin; vessel propagated position at (LeoRadius, 0, 0).
            // Distance = LeoRadius = 7,000,000 m >> 50,000 m threshold.
            var activeRef = new StubActiveVessel(new WorldPosition(0, 0, 0));
            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeRef);

            Assert.IsFalse(result.SuggestedMode.HasValue,
                "Beyond proximity, no other K→P trigger fires → Stay");
            Assert.AreEqual(TransitionTriggerReason.None, result.Reason);
        }

        [Test]
        public void EvaluateTransitionTriggers_KeplerRails_AtmosphericEntryPredicted_SuggestsPhysXActive()
        {
            // KeplerRails vessel beyond proximity but with NextAtmosphericEntryTick set
            // to current+1 (atmospheric entry imminent). Fires AtmosphericEntryPredicted.
            //
            // Uses SimTickController.SetInstanceForTesting + SetTickNumberForTesting from
            // commit 040 to control "current tick" deterministically.
            //
            // Field write target updated commit 048 Stage 1 from the (now-removed)
            // aggregated NextModeTransitionTick to the dedicated
            // NextAtmosphericEntryTick — test name and asserted trigger reason stay
            // atmospheric-specific, matching the new per-field firing distinction.
            _simTickGo = new GameObject("TestSimTick");
            var controller = _simTickGo.AddComponent<SimTickController>();
            SimTickController.SetInstanceForTesting(controller);
            controller.SetTickNumberForTesting(100);

            var kepler = NewKeplerState(_body);
            kepler.NextAtmosphericEntryTick = 101;  // current (100) + 1
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            // Active reference at origin → vessel propagated position at (LeoRadius, 0, 0)
            // → proximity = LeoRadius >> 50 km → proximity check passes (doesn't fire).
            // Next condition (atmospheric entry predicted) should fire.
            var activeRef = new StubActiveVessel(new WorldPosition(0, 0, 0));
            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeRef);

            Assert.IsTrue(result.SuggestedMode.HasValue);
            Assert.AreEqual(PhysicsMode.PhysXActive, result.SuggestedMode.Value);
            Assert.AreEqual(TransitionTriggerReason.AtmosphericEntryPredicted, result.Reason);
        }

        [Test]
        public void EvaluateTransitionTriggers_InterstellarCruise_ReturnsStay()
        {
            // InterstellarCruise mode is Phase 6 scope; the evaluator must return Stay.
            // Initialize forces InterstellarCruise to fall back to PhysXActive, so we
            // bypass Initialize's mode-rewrite by direct State.Mode assignment (same
            // pattern as commit 041 tests 11/12).
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.State.Mode = PhysicsMode.InterstellarCruise;

            var activeRef = new StubActiveVessel(new WorldPosition(0, 0, 0));
            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeRef);

            Assert.IsFalse(result.SuggestedMode.HasValue,
                "InterstellarCruise is Phase 6 scope → evaluator returns Stay");
            Assert.AreEqual(TransitionTriggerReason.None, result.Reason);
        }

        [Test]
        public void EvaluateTransitionTriggers_NullActiveVessel_ReturnsStay()
        {
            // Defensive null check on activeVesselForProximity. Passing null must not
            // throw; result must be Stay (cannot evaluate proximity without a reference).
            _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
            _vessel.Rigidbody.position = new Vector3((float)LeoRadius, 0f, 0f);

            TransitionEvaluation result = _vessel.EvaluateTransitionTriggers(activeVesselForProximity: null);

            Assert.IsFalse(result.SuggestedMode.HasValue,
                "Null active reference → Stay (cannot evaluate proximity)");
            Assert.AreEqual(TransitionTriggerReason.None, result.Reason);
        }

        // ----- ReferenceBody SOI schema (commit 044 Stage 1) -----

        [Test]
        public void ReferenceBody_Awake_PopulatesParentBodyId_WhenParentSet()
        {
            // Set up a parent body (separate GameObject from the SetUp's _body) and wire
            // it into a child body via the parameterized InitializeBodyForTesting
            // overload's parentBody parameter. EditMode tests can't go through the
            // Inspector; the test seam stays close to that.
            var parentGo = new GameObject("ParentBody");
            var parentBody = parentGo.AddComponent<ReferenceBody>();
            try
            {
                var childGo = new GameObject("ChildBody");
                var childBody = childGo.AddComponent<ReferenceBody>();
                try
                {
                    // Initialize parent first so its BodyId is populated before child
                    // reads it. Child is initialized with parentBody wired in via the
                    // parameterized overload (no reflection on private fields needed).
                    parentBody.InitializeBodyForTesting();
                    childBody.InitializeBodyForTesting(
                        massKg: EarthMassKg,
                        soiRadiusMeters: double.PositiveInfinity,
                        parentBody: parentBody);

                    Assert.AreNotEqual(Guid.Empty, parentBody.BodyId,
                        "Sanity: parent's BodyId should be populated");
                    Assert.AreEqual(parentBody.BodyId, childBody.ParentBodyId,
                        "Child's ParentBodyId should match parent's BodyId");
                    Assert.AreSame(parentBody, childBody.ParentBody,
                        "Child's ParentBody reference should point at the parent");
                }
                finally
                {
                    UnityObject.DestroyImmediate(childGo);
                }
            }
            finally
            {
                UnityObject.DestroyImmediate(parentGo);
            }
        }

        [Test]
        public void ReferenceBody_Awake_LeavesParentBodyIdEmpty_WhenNoParent()
        {
            // SetUp's _body has no parent set (parentBody Inspector field defaults null).
            // Invoke InitializeBodyForTesting and verify the top-level body convention:
            // ParentBody == null, ParentBodyId == Guid.Empty.
            _body.InitializeBodyForTesting();

            Assert.IsNull(_body.ParentBody,
                "Body with no parent wired should have ParentBody == null (top-level)");
            Assert.AreEqual(Guid.Empty, _body.ParentBodyId,
                "Body with no parent wired should have ParentBodyId == Guid.Empty");
        }

        [Test]
        public void ReferenceBody_SoiRadiusMeters_DefaultsToInfinity()
        {
            // Default Inspector value for soiRadiusMeters is double.PositiveInfinity
            // (top-level body convention). InitializeBodyForTesting doesn't touch the
            // field; the default applies.
            _body.InitializeBodyForTesting();

            Assert.AreEqual(double.PositiveInfinity, _body.SoiRadiusMeters,
                "Default SoiRadiusMeters should be PositiveInfinity (top-level body)");
        }

        [Test]
        public void ReferenceBody_SoiRadiusMeters_ReadsInspectorValue()
        {
            // Wire a finite SOI radius via the parameterized InitializeBodyForTesting
            // overload (the Inspector-substitute test seam). Verify the property
            // reads it back.
            _body.InitializeBodyForTesting(
                massKg: EarthMassKg,
                soiRadiusMeters: 6.6e8);  // ~660,000 km, Moon-like SOI

            Assert.AreEqual(6.6e8, _body.SoiRadiusMeters,
                "Property should return the Inspector-set finite value");
        }

        [Test]
        public void ReferenceBody_Awake_WithSelfAsParent_LogsErrorAndTreatsAsTopLevel()
        {
            // Defensive check: if the Inspector wires a body as its own parent,
            // Awake/InitializeBodyForTesting should log an error and treat the body
            // as top-level (ParentBody = null, ParentBodyId = Empty). The cycle
            // would otherwise produce bogus orbital re-rooting computations.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*Inspector wires this body as its own parent.*"));

            // Wire the self-cycle via the parameterized InitializeBodyForTesting
            // overload's parentBody parameter. The overload stores the reference and
            // the runtime self-cycle check fires inside InitializeBodyForTesting.
            _body.InitializeBodyForTesting(
                massKg: EarthMassKg,
                soiRadiusMeters: double.PositiveInfinity,
                parentBody: _body);  // self-cycle — triggers the defensive check

            Assert.IsNull(_body.ParentBody,
                "Self-cycle should be rejected; ParentBody should be null (top-level fallback)");
            Assert.AreEqual(Guid.Empty, _body.ParentBodyId,
                "Self-cycle should be rejected; ParentBodyId should be Empty");
        }

        // ----- SOI re-rooting (commit 044 Stage 3) -----
        //
        // Earth-Moon test substrate. Constructed per-test via a helper to avoid
        // bleeding into the SetUp's single-body baseline.
        //
        // Helper + constants extracted to VesselTestHelpers (test infrastructure
        // sweep, Commit 3 of 3). The 5 callers in the Vessel.ReRootToBody-direct
        // tests below invoke `BuildMoonAsChildOfEarth(_body)` via `using static`.

        // ----- Vessel.ReRootToBody direct tests -----

        [Test]
        public void ReRootToBody_FromKeplerRails_UpdatesReferenceBody()
        {
            // Set up Earth-rooted vessel, then re-root to Moon. Verify the cached
            // _referenceBody and the schema's ReferenceBodyId both update.
            var moon = BuildMoonAsChildOfEarth(_body);
            try
            {
                var kepler = NewKeplerState(_body);
                _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

                Assert.AreSame(_body, _vessel.ReferenceBody, "Sanity: vessel starts Earth-rooted");

                _vessel.ReRootToBody(moon);

                Assert.AreSame(moon, _vessel.ReferenceBody,
                    "Cached reference body should update to Moon");
                Assert.AreEqual(moon.BodyId, _vessel.State.KeplerState.ReferenceBodyId,
                    "Schema ReferenceBodyId should match Moon's BodyId");
            }
            finally
            {
                UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        [Test]
        public void ReRootToBody_FromKeplerRails_PreservesPositionContinuity()
        {
            // The vessel's world position should be continuous across re-rooting.
            // Re-rooting just changes the reference frame; the vessel doesn't teleport.
            var moon = BuildMoonAsChildOfEarth(_body);
            try
            {
                var kepler = NewKeplerState(_body);  // LEO around Earth
                _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

                WorldPosition posBefore = _vessel.GetWorldPosition();
                _vessel.ReRootToBody(moon);
                WorldPosition posAfter = _vessel.GetWorldPosition();

                // Tolerance: 1 m absolute at LEO scale (~7e6 m magnitude). Re-rooting
                // composes one position subtraction + orbital-elements round-trip.
                double dx = posBefore.Value.x - posAfter.Value.x;
                double dy = posBefore.Value.y - posAfter.Value.y;
                double dz = posBefore.Value.z - posAfter.Value.z;
                double diff = math.sqrt(dx * dx + dy * dy + dz * dz);

                Assert.Less(diff, 1.0,
                    $"Position should be continuous across re-rooting; diff = {diff:F3} m");
            }
            finally
            {
                UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        [Test]
        public void ReRootToBody_BeforeInitialize_LogsWarningAndNoOps()
        {
            // Pre-Initialize vessel. The guard at the top of ReRootToBody should fire.
            var moon = BuildMoonAsChildOfEarth(_body);
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex(".*before Initialize; ignored.*"));

                _vessel.ReRootToBody(moon);

                // No state assertions — State is null pre-Initialize, can't dereference.
                // The expected log firing is the test assertion.
            }
            finally
            {
                UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        [Test]
        public void ReRootToBody_WhenPhysXActive_LogsErrorAndNoOps()
        {
            // PhysXActive vessel. ReRootToBody is intra-Kepler-rails only.
            var moon = BuildMoonAsChildOfEarth(_body);
            try
            {
                _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);

                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex(".*not KeplerRails. SOI re-rooting is intra-Kepler-rails only.*"));

                _vessel.ReRootToBody(moon);

                // Mode unchanged; cached reference unchanged.
                Assert.AreEqual(PhysicsMode.PhysXActive, _vessel.Mode);
                Assert.AreSame(_body, _vessel.ReferenceBody);
            }
            finally
            {
                UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        [Test]
        public void ReRootToBody_WhenKeplerStateNull_LogsErrorAndNoOps()
        {
            // Construct the inconsistent state directly: Initialize in PhysXActive
            // (KeplerState stays null), force Mode = KeplerRails. Then ReRootToBody
            // should hit the KeplerState-null guard.
            var moon = BuildMoonAsChildOfEarth(_body);
            try
            {
                _vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);
                _vessel.State.Mode = PhysicsMode.KeplerRails;
                _vessel.State.KeplerState = null;

                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex(".*KeplerState is null.*State is inconsistent.*"));

                _vessel.ReRootToBody(moon);

                Assert.AreSame(_body, _vessel.ReferenceBody,
                    "Reference body should be unchanged after rejected re-root");
            }
            finally
            {
                UnityObject.DestroyImmediate(moon.gameObject);
            }
        }

        [Test]
        public void ReRootToBody_WhenNewBodyNull_LogsErrorAndNoOps()
        {
            var kepler = NewKeplerState(_body);
            _vessel.Initialize(NewState(), _body, PhysicsMode.KeplerRails, kepler);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*newBody is null.*"));

            _vessel.ReRootToBody(null);

            Assert.AreSame(_body, _vessel.ReferenceBody,
                "Reference body should be unchanged after rejected re-root");
        }

    }
}
