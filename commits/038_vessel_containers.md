# 038: Vessel containers — §2 authoritative state schema in code, mode transition implementation, TestVessels test scene

Phase 0's largest implementation commit. Lands the netcode contract §2.1-§2.5 authoritative state schema as C# data classes, the §3.1 PhysX-active ↔ Kepler-rails mode transition mechanism, a static `VesselRegistry`, real state-vector-to-Keplerian orbital element math, a Phase-0 `ReferenceBody` stub for orbital frame, an `IActiveVessel` contract that SimTickController consumes, and a new `TestVessels.unity` scene with `TestVesselDriver` script demonstrating mode transitions end-to-end. Also refactors `SimTickController` to use the new `IActiveVessel` interface instead of the commit-033 prototype `SetActiveVesselWorldPosition` API. Deletes the old `TestShiftDriver.cs` (host-side `git rm`).

This is the first time the architectural state schema gets implemented in code. Future commits build on the schema: Kepler-rails propagator (Step 4 actual implementation), comprehensive mode transition triggers per §3.1, vessel construction in Phase 2, save/load serialization. The schema landing here is the contract every downstream commit honors.

Four substantive design decisions locked in this commit's design discussion:

1. **Three nullable mode-state fields on VesselAuthoritativeState** (`PhysXState`, `KeplerState`, `CruiseState`). Direct schema match; one is non-null at any time per `Mode`.
2. **VesselRegistry as plain static class without deferred-registration**, distinct from commit 034's `FloatingOriginManager` pattern. The deferred-registration pattern solves a specific race (singleton MonoBehaviour `Instance` is null until Awake fires); static classes have no equivalent race because their static initializer runs synchronously before first member access. Reflexive pattern-copying from commit 034 would have been over-engineering.
3. **IActiveVessel interface introduced** to break what would have been a circular asmdef dependency. Vessels references SimTick (for `PhysicsMode` and `SimTickController.Instance`); SimTickController needs to reference vessel state for step 6's shift-detection and warp-mode-tracking. The interface lives in the SimTick module, Vessel implements it, the dependency graph stays acyclic.
4. **Phase 0 simplifications on Kepler-rails preservation**, explicitly documented:
   - Orientation reset to identity on re-activation rather than preserved per §3.1 step 7
   - Position computed at `TrueAnomalyAtEpoch` rather than propagated to current tick

Each simplification has a forward-pointer to the commit that will resolve it.

## Scope

### New files (Vessels module)

- `SPACESIM/Assets/Scripts/Foundation/Vessels/SpaceSim.Foundation.Vessels.asmdef` — runtime asmdef. References Coordinates, SimTick, Unity.Mathematics. Standard runtime pattern matching the Coordinates and SimTick asmdefs (autoReferenced: true, no overrideReferences).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/VesselAuthoritativeState.cs` — §2.1 schema. All common fields wired; stubs (with explicit `// PHASE 0 STUB` comments) for fields not yet exercised: `OwnerAgencyId` (agency system in Phase 2+), `AuthorityHolderId` (string for now; becomes `NetworkID` when multiplayer lands), `ResourceInventory` (Phase 5+ supply system), `CrewAboard` (Phase 5 crew tracking), `TelemetryBuffer` (telemetry module). Three nullable mode-state references (`PhysXState`, `KeplerState`, `CruiseState`).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/PhysXState.cs` — §2.2 schema. All fields wired (position, velocity, orientation, angular velocity, reference body ID, floating origin, rigidbody handle, thrust, atmospheric context).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/KeplerState.cs` — §2.3 schema. Six classical orbital elements (a, e, i, Ω, ω, ν₀) + epoch tick + reference body ID + four nullable event-prediction fields (left null in Phase 0; propagator commits will populate).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/CruiseState.cs` — §2.4 schema as stub. Field shape complete; no Phase 0 code constructs it (interstellar-cruise deferred to Phase 6 per the artifact list in commit 037).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/VesselDesign.cs` — §2.5 minimal stub. Only `DesignId` field; Phase 2 vessel-construction work fleshes out the part tree, mass, design history, scripts.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/ReferenceBody.cs` — Phase 0 stub MonoBehaviour. Captures the minimum a vessel's Kepler-rails state needs: `BodyId`, `MassKg`, `PositionWorld`, derived `Mu = G · M`. Full §2.6 body state (axial tilt, rotation, atmospheric profile, SOI, child bodies) is Phase 4 procgen work.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/OrbitalElements.cs` — real state-vector-to-Keplerian math. `ComputeFromStateVector(position, velocity, μ, tick, bodyId)` returns a KeplerState. Inverse `ComputeStateVector(state, ν, μ)` returns (position, velocity) — used for both the propagator-stub round-trip in mode transitions and the EditMode round-trip tests. Edge cases explicitly handled: circular orbits (e=0, ω undefined), equatorial orbits (i=0, Ω undefined), equatorial circular (both undefined), hyperbolic trajectories (a<0, e>1), parabolic (e=1 measure-zero). References Bate-Mueller-White §2.4-§2.5, Curtis §4.4, Vallado §2.5.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs` — MonoBehaviour wrapping `VesselAuthoritativeState`. Implements `IActiveVessel` (`GetWorldPosition()` and `Mode` property). Owns the runtime Rigidbody and FloatingOriginAnchor when in PhysX-active mode; both components removed in Kepler-rails mode. `Initialize(state, body, mode)` sets up the GameObject's component shape; `TransitionToKeplerRails()` and `TransitionToPhysXActive()` execute the §3.1 procedure steps. Component lifecycle: in PhysX-active mode, Rigidbody is added FIRST then FloatingOriginAnchor (so the anchor's Awake caches the rigidbody reference correctly via the deferred-registration plumbing from commit 034).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/VesselRegistry.cs` — static class. `_vessels` list, `RegisterVesselSafe(Vessel)`, `UnregisterVesselSafe(Vessel)`, `ClearForTesting()`, `Vessels` read-only property, `VesselCount` property. ~30 LOC. No pending queue; no `DrainPendingForTesting`. The `Safe` suffix preserved for cross-codebase naming consistency with `FloatingOriginManager.RegisterListenerSafe` from commit 034.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs` — test-scene driver MonoBehaviour. Consolidates initialization (Vessel.Initialize) + active-vessel registration (SimTickController.SetActiveVessel) + initial velocity application + keypress handling for mode transitions + diagnostic UI updates. Replaces the old `SimTick/TestShiftDriver.cs` entirely (which gets host-side `git rm`'d).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/SpaceSim.Foundation.Vessels.Tests.asmdef` — EditMode test asmdef. Form 2 modern verbose pattern (autoReferenced: false, overrideReferences: true, precompiledReferences: ["nunit.framework.dll"], includePlatforms: ["Editor"]).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/OrbitalElementsTests.cs` — 10 EditMode tests covering: circular equatorial orbit (a, e, i, Ω, ω, ν₀ match conventions), elliptical orbit (apsides at predicted radii), inclined circular orbit (45° inclination), hyperbolic trajectory (a<0, e>1, apoapsis is +∞), three round-trip preservation tests (circular equatorial, elliptical equatorial, inclined with all six elements non-zero), periapsis/apoapsis helper sanity checks, event-prediction-fields-are-null Phase-0-stub verification.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs` — 11 EditMode tests covering: Initialize stores state and reference body, Initialize in PhysX-active adds Rigidbody and FloatingOriginAnchor, Initialize in Kepler-rails doesn't add them, TransitionToKeplerRails populates KeplerState with real elements, TransitionToKeplerRails clears PhysXState and removes components, TransitionToPhysXActive populates PhysXState and adds components, both transitions log warning and no-op when called from wrong mode, round-trip PhysX → Kepler → PhysX preserves position+velocity (with float-precision-via-Rigidbody tolerances documented), GetWorldPosition correctness in both modes.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselRegistryTests.cs` — 8 EditMode tests covering: RegisterVesselSafe adds to list, duplicate doesn't double, null is ignored, UnregisterVesselSafe removes from list / null is ignored / not-in-list is no-op, ClearForTesting empties the list, Vessels property returns IReadOnlyList.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/PlayModeTests/SpaceSim.Foundation.Vessels.PlayModeTests.asmdef` — PlayMode test asmdef. Form 1 canonical pattern (optionalUnityReferences: ["TestAssemblies"]).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/PlayModeTests/VesselPlayModeTests.cs` — 3 PlayMode tests covering: OnEnable registers vessel with registry after Initialize, OnDisable unregisters, TransitionToPhysXActive at runtime adds a FloatingOriginAnchor that correctly registers with FloatingOriginManager and receives subsequent shifts.

### New files (SimTick module)

- `SPACESIM/Assets/Scripts/Foundation/SimTick/IActiveVessel.cs` — interface in the SimTick namespace. Two members: `WorldPosition GetWorldPosition()` and `PhysicsMode Mode { get; }`. Comprehensive class-level XML doc explaining the asmdef-cycle-avoidance rationale, the narrow-interface design principle, and future evolution path.

### Modified files

- `SPACESIM/Assets/Scripts/Foundation/Coordinates/CoordinateMath.cs` — added `public const double G = 6.67430e-11` (Newton's gravitational constant) with XML doc noting the future move to a Physics module when one exists. Single-constant addition; no existing code touched.
- `SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickController.cs` — refactored to use `IActiveVessel`. Deleted: `_activeVesselWorldPosition` field, `_activeVesselPositionSet` flag, `SetActiveVesselWorldPosition(WorldPosition)` method, the "Active-vessel position injection (PROTOTYPE BRIDGE)" section. Added: `IActiveVessel ActiveVessel { get; private set; }` property, `SetActiveVessel(IActiveVessel)` method, `_warnedAboutMissingActiveVessel` flag, two-step warp-mode-tracking (Set call + every-tick refresh in Step 6). Step 6 also gets an "ActiveVessel is null" branch with warn-once-per-controller-lifetime.
- `SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/SimTickControllerTests.cs` — refactored. The three step-6 tests that previously called `SetActiveVesselWorldPosition(WorldPosition)` now construct an `ActiveVesselStub` POCO implementing `IActiveVessel`, call `SetActiveVessel(stub)`. Added a new test `Step6_WithoutActiveVessel_LogsWarningOnce` for the null-active-vessel warn-once path. Added four new tests covering `SetActiveVessel` warp-mode tracking: SetActiveVessel(PhysXActive vessel) sets warp to PhysXActive, SetActiveVessel(KeplerRails vessel) sets warp to KeplerRails, SetActiveVessel(null) resets warp to PhysXActive, Step 6 picks up vessel.Mode changes on next FixedUpdate without explicit re-call. Added `UnityObject` alias for the namespace-ambiguity prevention pattern from earlier compile-cycles this commit. Added private `ActiveVesselStub` POCO class implementing IActiveVessel.

### Files marked for host-side deletion

- `SPACESIM/Assets/Scripts/Foundation/SimTick/TestShiftDriver.cs` — replaced by `Vessels/TestVesselDriver.cs`. The Cowork sandbox cannot unlink pre-session files; the file's body is rewritten as a stub comment block flagging it for host-side `git rm` during replay. Same pattern as commit 028's `PrototypeStartupTest.cs.bak` and commit 033's old `Coordinates/TestShiftDriver.cs`. The user's host-side replay procedure must include:
  ```
  git rm SPACESIM/Assets/Scripts/Foundation/SimTick/TestShiftDriver.cs
  git rm SPACESIM/Assets/Scripts/Foundation/SimTick/TestShiftDriver.cs.meta
  ```
  This is the **third** scene-rewire-required commit in the prototype implementation arc (after 028 and 033). Worth flagging the cumulative workflow tax; not worth solving with tooling in this commit (any automated-scene-rewire infrastructure would be its own large commit).

### Files not modified

- `docs/CONSTRAINTS.md` — no design changes
- `docs/NETCODE_CONTRACT.md` — no contract changes (the implementation conforms to §2.1-§2.5 and §3.1 as written)
- `docs/DECISIONS.md` — will be updated by a small follow-on edit if this commit's design decisions need a formal entry. Surface: yes, add an entry for the VesselRegistry-as-static-class architectural choice (see lessons section for the reusable insight)
- `docs/PHASE_TRACKER.md` — will be updated by a small follow-on edit: toggle vessel-containers checkbox checked, add commit 038 row, leave Kepler-rails-test and mode-transition-test checkboxes unchecked (this commit lands the *mechanism*; comprehensive testing of Kepler-rails propagation and mode-transition triggers comes later)
- `commits/README.md` — workflow rules 1-6 preserved

## Rationale

### Why this commit is large

The §2 schema implementation is the single largest piece of code architecture in Phase 0. Splitting it across multiple commits would have produced false-binary boundaries: "schema without behavior" is incomplete because the schema doesn't justify itself without a consumer; "behavior without schema" is impossible because the behavior IS the schema's lifecycle. Committing the schema, the mode transition mechanism, the test infrastructure, and the SimTickController integration as one logical scope mirrors the netcode contract's framing: §2 (schema) and §3 (transitions) are two halves of one specification.

The four-stage decomposition that landed the work (data classes → orbital math + tests → Vessel + Registry + transitions → SimTickController integration + TestVesselDriver + artifact) produced four natural verification boundaries. Each stage was independently testable; failures at each boundary would have surfaced quickly. Two compile cycles surfaced naming/namespace issues that the staging caught immediately (PhysX vs Physx; UnityEngine.Object vs System.Object). One mid-stage test failure (eccentricity precision) surfaced and got fixed at its natural boundary.

### Why three nullable mode-state fields rather than a discriminated union or polymorphism

The §2 spec writes the schema with Option-typed fields: `physx_state: Option<PhysXState>`, `kepler_state: Option<KeplerState>`, `cruise_state: Option<CruiseState>`. The C# direct translation is three nullable references on `VesselAuthoritativeState`. Alternatives considered:

- **Polymorphic class hierarchy** (`VesselAuthoritativeState<PhysXState> : VesselAuthoritativeStateBase`, etc.) — heavier, requires casting at call sites that know the mode, fights the "common fields are common" structure that the contract specifies.
- **Discriminated union via inheritance** (`abstract class ModeState; class PhysXMode : ModeState; ...`) — solves the "exactly one is set" invariant via type, but the contract's `mode_entered_at` and the conversion between modes both need explicit branching anyway, so the type-level enforcement buys little.
- **Three nullable fields** (chosen) — direct schema match, supports the contract's framing of "mode-specific state" without inventing new abstractions, supports save/load round-trip naturally (deserialize all three fields; null serializes as null).

The cost is that the mode-state-mismatch invariant ("the field corresponding to `Mode` is the non-null one") is enforced by convention rather than by the type system. The transition methods enforce it programmatically; tests verify the invariant after each transition. Acceptable Phase 0 trade-off.

### Why VesselRegistry is a static class (not a singleton MonoBehaviour with deferred-registration)

The Stage 1 design discussion proposed reflexively copying commit 034's `FloatingOriginManager` deferred-registration pattern (`RegisterListenerSafe`, pending queue, `DrainPendingForTesting`, etc.) for `VesselRegistry`. On closer examination, the pattern doesn't apply.

Commit 034's pattern solves a specific Unity lifecycle race: a singleton MonoBehaviour's static `Instance` reference is null until its `Awake` fires; other components' `OnEnable` could fire before that and would see `Instance == null`. The pending queue + drain-on-Awake fixed that race.

`VesselRegistry` is a plain static class with a static `_vessels` list. The list is initialized to `new List<Vessel>()` at the type's static-constructor time, which the CLR runs synchronously before the first member access. The first call to `RegisterVesselSafe` triggers the static constructor, the list initializes, and the call proceeds against an initialized list. There is no null-window race. The deferred-registration pattern was reflexive copying, not a response to a real problem.

The simplification saved ~50 LOC of registry code and ~4 tests of pending-queue lifecycle tests. The `Safe` suffix is preserved on the public methods (`RegisterVesselSafe`, `UnregisterVesselSafe`) for cross-codebase naming consistency with the FloatingOriginManager methods.

**Reusable insight** (for DECISIONS.md): future static-class registries should follow VesselRegistry's simple pattern, not FloatingOriginManager's pattern. The pattern selection criterion: **does the class have a non-null window?** If yes, use the pending queue. If no, direct registration is sufficient. The criterion separates "singleton MonoBehaviour" patterns (which have a null window between scene load and Awake) from "static class" patterns (which don't).

### Why IActiveVessel interface rather than direct Vessel reference

The Stage 4 plan assumed SimTickController could reference `Vessel` directly. Compile-time check during Stage 4 writing surfaced the circular asmdef dependency: Vessels references SimTick (for `PhysicsMode` and `SimTickController.Instance`); SimTick referencing Vessels closes the cycle.

Four options were considered:
- **Interface in SimTick that Vessel implements** (chosen) — small, focused, breaks the cycle cleanly
- **Bridge class in Vessels** that holds the active vessel and registers as a SimTick listener — heavier, hides the active-vessel concept from the controller
- **Move Vessel out of Vessels asmdef** — heaviest refactor, wrong architecturally
- **Move PhysicsMode into Coordinates** — would not actually break the cycle since Vessels still needs `SimTickController.Instance.TickNumber`

The interface is two members. Vessel was already implementing `GetWorldPosition()` and `Mode` with the right signatures; adding `: IActiveVessel` to the base list cost nothing. Tests benefit from the abstraction — the three step-6 tests in `SimTickControllerTests.cs` now construct a simple `ActiveVesselStub` POCO implementing `IActiveVessel` instead of the full GameObject + Vessel + ReferenceBody scaffolding they would have needed under the direct-reference approach.

The interface is narrow on purpose: it captures only what Step 6 needs from the active vessel (position and mode). Future SimTickController steps that gain real implementations — Step 2 (PhysX read), Step 4 (analytic propagation), Step 7 (PhysX push-back) — will either extend `IActiveVessel` or define new sibling interfaces in the SimTick module. The dependency graph stays acyclic regardless of how the vessel module evolves.

### Why Phase 0 simplifications on Kepler-rails state

Two simplifications, both made explicit in the code via `// PHASE 0 SIMPLIFICATION:` comment blocks in `Vessel.TransitionToPhysXActive`:

1. **Orientation reset to identity** rather than preserved per §3.1 step 7. The contract specifies that the orientation captured at TransitionToKeplerRails should be preserved across the rail period and restored on re-activation. Phase 0 drops that history. The rotation-handling commit will preserve pre-transition orientation, likely via an `OrientationOnRails` field stored alongside the six classical elements (orientation is independent of orbital dynamics for a torque-free body on rails, so it doesn't belong inside KeplerState proper).

2. **Position computed at `TrueAnomalyAtEpoch`** rather than propagated to the current tick. Phase 0 has no Kepler-rails propagator (Step 4 is still a stub). The `ν` stored in KeplerState is the value captured at TransitionToKeplerRails; until a propagator advances it through Kepler's equation as time passes, re-evaluating `ComputeStateVector` with the same `ν₀` produces the same `(r, v)`. Consequence: a vessel that transitions to Kepler-rails and immediately back to PhysX-active round-trips correctly. A vessel that sits on Kepler-rails for any duration and then transitions back will reappear at the position it had at the moment of transition, not the propagated current position. The TestVesselDriver logs a one-time `PHASE 0 LIMITATION` message on the first Space-key transition so user-side observers aren't surprised by the rewind-on-return behavior.

Both simplifications have forward-pointers in code comments to the commit(s) that will resolve them.

### Why SimTickController state simplifies

The commit-033 prototype bridge had two state fields plus one method:
- `_activeVesselWorldPosition` (WorldPosition)
- `_activeVesselPositionSet` (bool)
- `SetActiveVesselWorldPosition(WorldPosition)` method

Commit 038 replaces all three with:
- `ActiveVessel` (IActiveVessel property)
- `SetActiveVessel(IActiveVessel)` method

State count drops from 2 fields + 1 method to 1 property + 1 method. The reduction reflects the controller now consuming a single object that owns its own position and mode, rather than tracking position separately and modes via guesses. Step 6's per-tick logic also simplifies — the `_activeVesselPositionSet` guard goes away (replaced by `ActiveVessel != null`), and warp mode tracking happens via a single line at the start of step 6.

### Why TestVesselDriver consolidates initialization + input + UI into one script

Stage 4's original plan split responsibilities across two MonoBehaviours: `TestShiftDriver` refactored for velocity/UI, plus a new `TestVesselDriver` for initialization/input. On closer examination, the split produced moderate duplication (both scripts attached to the vessel GameObject, both held a `Vessel` reference, both had matching `Start`/`Update` lifecycle). The natural seam — "TestShiftDriver does motion + UI; TestVesselDriver does initialization + input" — divided one coherent responsibility (drive the test vessel) into two.

The consolidation: one script (`TestVesselDriver`) that constructs the state at Start, registers with the SimTickController, applies the initial velocity, handles keypress for mode transitions, and refreshes the diagnostic UI each Update. The old `TestShiftDriver.cs` retires (host-side `git rm`).

The cost: deleting `TestShiftDriver.cs` requires the standard host-side `git rm` (commits 028 and 033 used the same pattern for files Cowork's sandbox couldn't unlink). The benefit: one script per vessel, no duplication, naming aligns with the new architecture.

### Why mark deletion via stub-file pattern instead of waiting for `git rm`

The Cowork sandbox cannot delete files that existed before the session. Marking the file for deletion by rewriting its body as a stub comment block (referencing the replacement file and the `git rm` command) is the established pattern from commits 028 and 033. The stub:
- Is valid C# (compiles to a comment-only file with no top-level declarations)
- Documents the deletion intent so a future reader sees what happened
- Includes the exact `git rm` command for the host-side replay step

After `git rm`, the file disappears from the working tree and from git. The `cs.meta` file also needs removal — Unity's per-asset metadata file. The replay procedure handles both.

## Test coverage

EditMode tests added (29 new EditMode tests, plus 3 new PlayMode tests):

| File | New tests | Total in file after commit |
|---|---|---|
| `OrbitalElementsTests.cs` | +10 (new file) | 10 |
| `VesselTests.cs` | +11 (new file) | 11 |
| `VesselRegistryTests.cs` | +8 (new file) | 8 |
| `SimTickControllerTests.cs` | +4 (3 step-6 refactored; 1 new ActiveVessel-null warn; 4 new SetActiveVessel warp-mode tests; net +4 over the previous count of 24) | 28 |
| `VesselPlayModeTests.cs` (PlayMode) | +3 (new file) | 3 |

**Expected test count after commit 038:**
- EditMode: 113 (commit 037 baseline) + 11 (VesselTests) + 8 (VesselRegistryTests) + 10 (OrbitalElementsTests) + 4 (SimTickController net additions) = **146 EditMode**
- PlayMode: 3 (commit 037 baseline) + 3 (VesselPlayModeTests) = **6 PlayMode**
- Total: **152 tests green**

(Note: the running tally during Stage 3 verification was 132 EditMode + 6 PlayMode = 138; that did NOT include the 4 new SimTickController tests added in Stage 4 to cover the new SetActiveVessel API.)

## End-to-end Play verification

Per the standing category established in commit 034, architectural commits with test scenes get end-to-end Play verification.

**TestVessels.unity** (new scene, user-side authoring required):

User-side scene authoring steps (similar to commit 033's TestCoordinates authoring guide):

1. File → New Scene → Save As `Assets/Scenes/TestVessels.unity`
2. Hierarchy → Right-click → Camera (verify tag is MainCamera)
3. Hierarchy → Right-click → Light → Directional Light
4. Hierarchy → Right-click → Create Empty → rename "FloatingOriginRoot" → Inspector → Add Component → Floating Origin Manager
5. Hierarchy → Right-click → Create Empty → rename "SimTickRoot" → Inspector → Add Component → Sim Tick Controller
6. Hierarchy → Right-click → Create Empty → rename "ReferenceBody" → Inspector → Add Component → Reference Body. Position stays at (0, 0, 0). Mass Kg default is 5.972e24 (Earth-equivalent) — leave as default for the test.
7. Hierarchy → Right-click → 3D Object → Sphere → rename "TestVessel". Set Position to (7000000, 0, 0) — LEO altitude relative to the body at origin.
8. With TestVessel selected → Inspector → Add Component → Vessel. (Do NOT manually attach Rigidbody or FloatingOriginAnchor — the Vessel.Initialize call wires those automatically.)
9. With TestVessel selected → Inspector → Add Component → Test Vessel Driver. The component's `[RequireComponent(typeof(Vessel))]` will fail to attach if Vessel isn't present, so attach in this order.
10. Test Vessel Driver Inspector:
    - Vessel: drag TestVessel onto the field (or auto-resolves to self-reference via [RequireComponent])
    - Reference Body: drag ReferenceBody from Hierarchy
    - Initial Velocity: leave default (10000, 0, 0) for visible shifts
    - Diagnostic Label: wire later, after creating the UI Canvas
11. Hierarchy → Right-click → UI → Canvas → its default settings are fine (Screen Space - Overlay)
12. With Canvas selected → Right-click → UI → Legacy → Text. Position the text in the upper-left of the canvas via the Rect Transform anchors. Set Font Size to 14, color white. Set Horizontal Overflow to "Overflow" and Vertical Overflow to "Overflow" (same as TestCoordinates from commit 034 verification).
13. With TestVessel selected → Inspector → Test Vessel Driver → Diagnostic Label: drag the UI Text onto the field.
14. File → Save Scene (Ctrl+S).

**End-to-end behavior to verify in Play mode:**

15. Press Play. Run for ~15-20 seconds.
16. Verify in Game view:
    - Diagnostic text renders, updates each frame.
    - `Mode: PhysX-active` displayed initially.
    - World position X advances at the configured velocity (10 km/s → +10,000 m/s).
    - Distance from origin grows; at 50 km the floating origin shifts. Verify "Shift count: N" advances by 1 every ~5 seconds.
    - "Sim-tick #" advances continuously.
17. Press Space during play (after a few seconds have passed):
    - One-time PHASE 0 LIMITATION log message appears in Console.
    - Diagnostic text changes to `Mode: Kepler-rails (epoch tick: N)`.
    - "Mode: Kepler-rails" line includes the epoch tick number for reference.
    - The position-context line displays the Phase 0 propagator caveat.
18. Press Space again:
    - Diagnostic text returns to `Mode: PhysX-active`.
    - Position roughly equal to where the vessel was at the moment of the first Space press (round-trip-immediate behavior — Phase 0 doesn't propagate the orbit, so the rewind-on-return is expected behavior, not a bug).
19. Stop Play mode. No unexpected warnings in Console (the warn-once messages for `ActiveVessel is null` would only fire if the active vessel registration failed — they should not appear in normal Play).

**TestCoordinates.unity** (existing scene, user-side rewire required):

The existing TestCoordinates scene from commit 034 has a TestSubject GameObject with a `TestShiftDriver` component attached. After commit 038 lands, `TestShiftDriver.cs` is gone (replaced by the stub-then-`git rm`), so Unity will show the component as "Missing (Mono Script)" on TestSubject.

User-side rewire procedure:
1. Open TestCoordinates.unity in Unity
2. Select TestSubject in Hierarchy. Inspector shows "Missing (Mono Script)" on the TestSubject's old TestShiftDriver component.
3. Remove the missing component (right-click → Remove Component, or click the gear → Remove Component)
4. Add Component → Test Vessel Driver
5. Wire the Inspector fields the same way as TestVessels (Vessel reference, ReferenceBody reference, Initial Velocity, Diagnostic Label)
6. The TestSubject GameObject still has a Vessel component from before? Check: it likely doesn't, because the original TestSubject had a Rigidbody + FloatingOriginAnchor (commit 034 authoring) but no Vessel.
   - If TestSubject has no Vessel component: Add Component → Vessel (the `[RequireComponent(typeof(Vessel))]` on TestVesselDriver enforces this).
   - If TestSubject has an existing Rigidbody and/or FloatingOriginAnchor from commit 034: Vessel.Initialize will gracefully accept the pre-existing components rather than adding duplicates (per the `ConfigureForPhysXActive` `GetComponent` checks). However, the cleanest re-author is to remove the old Rigidbody and FloatingOriginAnchor first, let Vessel.Initialize add fresh ones.
7. Optionally create a ReferenceBody GameObject if TestCoordinates doesn't have one yet. (TestCoordinates wasn't authored with one in commit 034.)
8. Save scene (Ctrl+S)
9. Re-run end-to-end Play verification in TestCoordinates — same observable behavior as TestVessels but without the keypress-mode-transition demonstration (TestCoordinates can stay focused on the floating-origin behavior).

Two scenes end up using the same TestVesselDriver pattern. The architecture is consistent across both.

## Lessons

Five lessons surface from the commit 038 work. None yet a formal workflow rule (each is a single data point or two), but worth recording so future commits benefit:

**Lesson 1 — PhysX spelling.** When referencing an existing type from another module, verify the actual spelling against the source. Stage 3 wrote `PhysicsMode.PhysxActive` (lowercase x); the enum from commit 033 spells it `PhysXActive` (capital X, matching the proper noun). The compile error caught this immediately, but a few minutes of verification at prompt-write time would have caught it sooner. The principle: cross-module identifiers come from source files, not from prompt text or memory.

**Lesson 2 — Unity-vs-System namespace ambiguities.** Test files that mix Unity types and System types need explicit qualification for ambiguous names. `Object` is the most common offender — `System.Object` collides with `UnityEngine.Object` when both namespaces are imported. The pattern that worked: a `using UnityObject = UnityEngine.Object;` alias at the top of each test file, then `UnityObject.DestroyImmediate(...)` at call sites. The alias is cheap, reads cleanly, and is drift-resistant if a future edit adds `using System;` for unrelated reasons.

**Lesson 3 — Float-precision tolerances in Rigidbody-routed tests.** Tests that route values through Unity's float types (Vector3, Quaternion, Rigidbody) need tolerances proportional to float precision at the test's value magnitude. Pure-double math tests can use much tighter tolerances; mixed pipelines compound float error through any subsequent math. At LEO scale (7e6 m), the float epsilon is ~0.84 m, so any assertion in absolute meters should use ≥1 m tolerance; any relative-error assertion should use ≥1e-7. Commit 038's eccentricity test loosened from 1e-9 to 1e-6 with an explanatory comment to prevent future tightening; the GetWorldPosition test was hardened from 1e-3 m (which passed by luck — 7e6 happens to be exactly representable as float) to 1.0 m for consistency.

**Lesson 4 — Cross-asmdef dependency directions compound across stages.** Stage 1 locked Vessels → SimTick (so Vessels could use `PhysicsMode` and `SimTickController.Instance`). Stage 4 planned SimTick → Vessels (so the controller could hold a `Vessel ActiveVessel`). Neither stage flagged the circular implication; both were stated in separate sessions. The compile-time check would have caught it eventually; the architectural-level check should have caught it earlier. **Principle**: when a stage's design references types from a sibling module, verify the dependency direction is consistent with prior stages' design. Caught at design time rather than compile time in commit 038 because the question was asked explicitly during Stage 4 setup — small workflow improvement.

**Lesson 5 — Testability cost of concrete-type coupling, and the interface-decoupling payoff.** The original Stage 4 plan to use `Vessel` directly in SimTickController would have required real-Vessel test doubles in the three step-6 SimTickController tests (GameObject + Vessel component + ReferenceBody MonoBehaviour + Initialize call). Significant scaffolding for tests that only need a position-and-mode value. The IActiveVessel interface inverts this cost: tests now use a 6-line POCO stub implementing the interface. Architectural integration via interface decoupling makes unit tests simpler, not fiddlier, in the cases where it matters. The principle: **when designing cross-module coupling, the interface footprint determines the test-double footprint**. Narrow interfaces → tiny mocks. Wide interfaces or concrete-type references → heavy mocks.

## Workflow rule 6 self-applications during this commit

Workflow rule 6 (sandbox-mount-staleness, formalized in commit 035) fired multiple times during commit 038's writing:
- Bash `wc -l` on `Vessel.cs` reported stale line counts after Edit-tool mutations; Read-tool view was canonical (matched what git would see)
- Bash `wc -c` on three docs during commit 036 reported stale byte counts after the §10 edit; same pattern
- Bash `cat | wc -l` mid-Stage-3 returned a truncated view of `Vessel.cs` (~30 lines short of the actual file)

Each instance was resolved by trusting the Read-tool / Grep-tool views (which read host-canonical bytes) over the bash view. The rule's discipline — host filesystem is canonical, verify byte-level views are independent — continues to do load-bearing work. Pattern stable now across commits 028 / 031 / 033 / 035 / 036 / 037 / 038.

## Replay

```
cd C:\Users\gmkar\space_sim

# Stage all new/modified files
git add SPACESIM/Assets/Scripts/Foundation/Vessels/
git add SPACESIM/Assets/Scripts/Foundation/SimTick/IActiveVessel.cs
git add SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickController.cs
git add SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/SimTickControllerTests.cs
git add SPACESIM/Assets/Scripts/Foundation/Coordinates/CoordinateMath.cs

# Remove the retired TestShiftDriver.cs and its meta file
git rm SPACESIM/Assets/Scripts/Foundation/SimTick/TestShiftDriver.cs
git rm SPACESIM/Assets/Scripts/Foundation/SimTick/TestShiftDriver.cs.meta

# Stage the commit artifact
git add commits/038_vessel_containers.md

git commit -m "commit 038: vessel containers + mode transition + TestVessels scene"
git push
```

After git replay, the user-side actions in Unity:
1. Let Unity recompile. Console should show no compile errors. The Vessels asmdef + the new SimTick types should resolve cleanly.
2. Test Runner → EditMode → Run All. Expected: **146 green** (113 prior + 11 VesselTests + 8 VesselRegistryTests + 10 OrbitalElementsTests + 4 SimTickController additions).
3. Test Runner → PlayMode → Run All. Expected: **6 green** (3 prior + 3 VesselPlayModeTests).
4. Author TestVessels.unity per the user-side scene authoring guide above.
5. Rewire TestCoordinates.unity per the user-side rewire guide above.
6. End-to-end Play verification in both scenes per the steps in this artifact.

If any test fails or any end-to-end behavior diverges from the documented expectation, surface for diagnosis before considering commit 038 complete.

## Notes for future commits

- **Kepler-rails propagator (next likely commit, 039 or thereabouts):** the analytic propagation that Step 4 currently stubs. Solves Kepler's equation each tick to advance true anomaly from the epoch value. Eliminates the Phase 0 simplification on `TransitionToPhysXActive` (positions become propagated, not frozen).
- **Mode transition test comprehensive coverage** (subsequent commit): exercise all §3.1 trigger conditions, including atmospheric entry prediction, player-focus changes, scripted mode changes, multi-vessel cluster activation.
- **Orientation preservation on rails** (rotation-handling commit): add an `OrientationOnRails` field alongside KeplerState (or inside it; the design decision is open). Resolves Phase 0 SIMPLIFICATION block 2.
- **PHASE_TRACKER.md** and **DECISIONS.md updates** land as small follow-on edits after commit 038 verification completes (or rolled into the next operational commit). Two specific entries surface from this commit:
  - PHASE_TRACKER: toggle "Vessel containers" checkbox to checked (commit 038), add commit 038 row to Recently landed table
  - DECISIONS: new entry for "VesselRegistry as static class (no deferred-registration pattern needed)" with the reusable pattern-selection criterion ("does the class have a non-null window?") as the cross-codebase insight
- **The scene-rewire workflow tax** (three commits in a row now: 028, 033, 038) is real but not yet worth solving with tooling. Worth tracking — if a fourth scene-rewire-required commit lands, consider designing a "scene migration" tool or pattern.
