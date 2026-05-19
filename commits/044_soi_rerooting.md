# 044: SOI re-rooting (first Phase 1 system-list item closed)

Land the patched-conics reference-frame-hierarchy implementation locked in CONSTRAINTS §2 ("Reference frame hierarchy" + "Orbital mechanics"). Closes the first Phase 1 system-list item: a vessel that crosses a sphere-of-influence boundary outward re-roots to the parent body; a vessel that enters a child body's SOI re-roots to that child. Stays intra-Kepler-rails throughout (mode is unchanged; only the reference body and orbital elements change).

The commit lands in three stages with verification gates between them:

- **Stage 1:** `ReferenceBody` schema extension (`SoiRadiusMeters` + parent body wiring), new `BodyRegistry` static class (Guid-keyed lookup + parent→children enumeration), NETCODE_CONTRACT.md amendment (§2.7 BodyState added; §2.6 WorldAuthoritativeState gains `bodies` field), 15 EditMode tests.
- **Stage 2:** `OrbitalElements.ReRootStateVector` math helper with explicit Phase 4+ caching-hazard XML doc, 8 EditMode tests on the Earth-Moon substrate.
- **Stage 3:** `Vessel.ReRootToBody` intra-mode operation, `VesselSoiRerootingDriver` static class subscribing to TickAdvanced, `TestVesselDriver` wiring, 10 EditMode tests + this artifact + DECISIONS + PHASE_TRACKER updates.

NETCODE_CONTRACT §2.7 BodyState is the first contract amendment of the Phase 1 system-list work — the contract gets ahead of implementation rather than behind, matching the project's "contract first" discipline. The `VesselSoiRerootingDriver` has no `Enabled` flag (unlike `VesselTransitionDriver` from commit 043) because SOI re-rooting has real implementation in Phase 1, not stubs that would always pass.

## Scope

### Stage 1 — Schema + registry + contract

- `SPACESIM/Assets/Scripts/Foundation/Vessels/ReferenceBody.cs` — modified. Added `SoiRadiusMeters` (Inspector field, default `double.PositiveInfinity` for top-level body convention), `parentBody` (Inspector reference, may be null), `ParentBody` (cached runtime reference), `ParentBodyId` (Guid for save-load). `Awake` extended with parent-body resolution (cycle-detection rejects self-parent with error log; parent's BodyId is auto-populated if the parent's own Awake hasn't fired yet — handles ordering races between sibling GameObjects). Self-registration with `BodyRegistry` on Awake; unregister on OnDestroy. New `InitializeBodyForTesting()` public method extracts Awake's body so EditMode tests can exercise the lifecycle logic (Unity's Awake doesn't fire on `AddComponent` in EditMode).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/BodyRegistry.cs` — new file (~120 LOC). Static class mirroring `VesselRegistry`'s shape. Public surface: `Bodies` (read-only list), `BodyCount`, `RegisterBodySafe`, `UnregisterBodySafe`, `TryGetBodyById` (Empty-Guid rejected as a sentinel for "no body assigned"), `GetChildrenOf` (O(N) filter; reactive maintenance is premature for Phase 1's single-digit body counts), `ClearForTesting`.
- `docs/NETCODE_CONTRACT.md` — modified. §2.6 WorldAuthoritativeState gains `bodies: Map<BodyID, BodyState>`. New §2.7 BodyState section defines the schema (body_id, name, mass_kg, mu, position_world, soi_radius_meters, parent_body_id, plus Phase 4+ deferred fields named for completeness). Three paragraphs of prose covering Phase 0/1 implementation scope, save-load semantics, and top-level body convention.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs` — modified. SetUp/TearDown extended with `BodyRegistry.ClearForTesting()`. 5 new ReferenceBody tests appended.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/BodyRegistryTests.cs` — new file. 10 tests covering register/unregister, lookup, children-enumeration, the test-only clear hook, plus three defensive checks (null register ignored, Guid.Empty lookup always false, null parent in GetChildrenOf returns empty list).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/PlayModeTests/VesselPlayModeTests.cs` — modified. SetUp/TearDown extended with `BodyRegistry.ClearForTesting()`. Operational cleanup ahead of any future PlayMode test that reads the registry.

**Stage 1 test count delta:** 15 new (5 ReferenceBody + 10 BodyRegistry). 194 (commit 043 baseline) + 15 = 209 EditMode after Stage 1.

### Stage 2 — Re-rooting math helper

- `SPACESIM/Assets/Scripts/Foundation/Vessels/OrbitalElements.cs` — modified. Added `using SpaceSim.Foundation.Coordinates;` directive (the new helper references `WorldPosition`). New `ReRootStateVector` static helper (~120 LOC including the comprehensive XML doc). Takes raw positions + μ + epoch tick + new body Guid (pattern parallels existing `ComputeFromStateVector` and `ComputeStateVector`); returns a `KeplerState` in the new body's frame. The XML doc has four sections: ALGORITHM (4 numbered steps), WHY POSITION TRANSFORMS BUT VELOCITY DOES NOT (Phase 1 stationary-bodies reasoning), ⚠ PHASE 4+ HAZARD — POSITION CACHING (the drift-by-body-travel warning), ⚠ PHASE 4+ HAZARD — VELOCITY (signature-must-extend-when-bodies-orbit warning), ROUND-TRIP PROPERTY.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/OrbitalElementsTests.cs` — modified. Added Earth-Moon test constants (Earth-Moon distance 3.844e8 m, Moon mass 7.342e22 kg, Moon SOI radius 6.6e7 m for reference, Earth + Moon body Guids). 8 new ReRoot tests covering: identity-fields preservation (originally named "ReturnsZeroPositionElements" — renamed in Stage 3 to "PreservesIdentityFields" to match what the test actually validates), re-root from Moon frame to Earth frame, re-root from Earth frame to Moon frame, round-trip A→B→A position+velocity preservation, velocity-unchanged Phase 1 limitation, epoch-tick + body-Guid pass-through, hyperbolic-trajectory preservation across re-root, numerical stability at 1e9 m distances.

**Stage 2 test count delta:** 8 new. 209 + 8 = 217 EditMode after Stage 2.

### Stage 3 — Driver + integration + docs + artifact

- `SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs` — modified. New `public void ReRootToBody(ReferenceBody newBody)` method (~70 LOC). Propagates current orbital state via `KeplerPropagator.PropagateState` to obtain position+velocity at current tick, delegates to `OrbitalElements.ReRootStateVector` for the frame transform, updates `State.KeplerState`, the cached `_referenceBody` field, and `State.LastAdvancedTick`. Four defensive guards: not-initialized, null newBody, wrong-mode (not KeplerRails), null-KeplerState. Public (not internal) for parity with the existing TransitionTo* methods and to allow tests in the separate `Vessels.Tests` asmdef to invoke it.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/VesselSoiRerootingDriver.cs` — new file (~190 LOC). Static class parallels `VesselTransitionDriver`'s shape but with no `Enabled` flag (SOI re-rooting has real implementation; always-on is the right default). Public surface: `EvaluationCount`, `RerootingCount`, `Initialize` (subscribe to TickAdvanced, idempotent, deferred-attach if Instance is null), `Shutdown` (unsubscribe + reset counters), `ResetForTesting` (clear counters without unsubscribing), `OnTickAdvanced`. The per-tick loop iterates a snapshot of `VesselRegistry.Vessels`, skips non-Kepler-rails vessels, defends against the schema-invariant-violation states (null State, null KeplerState, null ReferenceBody), checks outward crossing first (vessel beyond current SOI + parent exists → re-root to parent), then iterates `BodyRegistry.GetChildrenOf(currentBody)` to detect inward crossing (vessel inside child SOI → re-root to child, first match wins). Per-vessel try/catch isolates failures; throwing vessels log error and don't abort the loop. Diagnostic log on every re-rooting names the from-body, to-body, distance, and tick.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs` — modified. Added `VesselSoiRerootingDriver.Initialize()` call after the existing `VesselTransitionDriver.Initialize()` call in `Start()`. Inline comment block explains: the driver is always-on (no Enabled flag); in the single-body TestVessels.unity scene the per-tick check correctly finds no SOI crossings and produces no log spam.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs` — modified. SetUp/TearDown extended with `VesselSoiRerootingDriver.Shutdown()`. New `BuildMoonAsChildOfEarth()` test helper constructs an Earth-Moon multi-body substrate via reflection on `ReferenceBody`'s private `[SerializeField]` fields (mass, soiRadiusMeters, parentBody) — same reflection pattern established in Stage 1. 10 new tests:
  - **Vessel.ReRootToBody direct** (6): basic happy path (updates ReferenceBody), position-continuity (GetWorldPosition continuous across re-root within 1 m), before-Initialize warn, wrong-mode (PhysXActive) error, null-KeplerState error, null-newBody error.
  - **VesselSoiRerootingDriver integration** (4): vessel within SOI does not re-root (Moon-rooted at LMO altitude in Earth-Moon substrate), vessel beyond SOI re-roots to parent (Moon-rooted at SMA > Moon SOI re-roots to Earth), vessel enters child SOI re-roots to child (Earth-rooted at Moon's position re-roots to Moon), vessel with no parent does not re-root outward (top-level Earth, infinite SOI, no parent — even at SMA 1e10 m, no re-root fires).
- `docs/PHASE_TRACKER.md` — modified. Four edits: (1) commit 044 row added to Recently landed; (2) "Current milestone" extended with the SOI re-rooting summary; (3) Verification state Tests paragraph rewritten to 227/233 baseline with the 33-test breakdown; (4) Systems-by-phase Phase 1 — Foundations entry updated to mark "Reference frame hierarchy" and "Patched conics" as complete with commit-044 reference.
- `docs/DECISIONS.md` — modified. New entry "SOI re-rooting design (commit 044)" inserted after the commit 043 entry. Records the multi-part design (9 sub-decisions a-i), 5 alternatives rejected, the asmdef-discipline + Phase 1 scope + Phase 4+ migration-path rationale, and the implication for Phase 4+ work.
- `commits/044_soi_rerooting.md` — created (this artifact).

**Stage 3 test count delta:** 10 new + 1 renamed (no count change for rename). 217 + 10 = **227 EditMode** total after commit 044. PlayMode unchanged at 6. Total: **233 green**.

## Architecture overview

The SOI re-rooting feature breaks into four cooperating pieces, each living at the right layer:

1. **`ReferenceBody` (data layer)** — owns the SOI radius and parent-body wiring. Self-registers with `BodyRegistry` on Awake. Pure data class plus lifecycle hooks; no math.
2. **`BodyRegistry` (lookup layer)** — Guid-keyed lookup + parent→children enumeration. Pattern parallels `VesselRegistry`. Used by save-load and by the driver to find child bodies during the inward-crossing check.
3. **`OrbitalElements.ReRootStateVector` (math layer)** — pure math, stateless, takes raw positions + μ. Computes the vessel's state vector in the new body's frame and produces a fresh `KeplerState`. The Phase 4+ velocity-frame-transform hazard is documented in the XML doc; the signature will extend (not break) when bodies orbit.
4. **`Vessel.ReRootToBody` + `VesselSoiRerootingDriver` (operation layer)** — the per-tick check + dispatch. Driver subscribes to `TickAdvanced`, iterates the vessel registry, computes distances, calls `ReRootToBody` when an SOI crossing is detected. Re-rooting is intra-mode (vessel stays on Kepler-rails); only the reference body and orbital elements change.

The four layers compose cleanly. Math doesn't know about scene infrastructure (testable without `AddComponent`). Data doesn't know about math (Inspector-wirable without code paths). Lookup is its own concern (mirrors VesselRegistry). The driver is the only piece that touches all four — and lives in the Vessels asmdef so the asmdef direction (Vessels → SimTick) is preserved.

## The Phase 4+ caching hazard (preserved from Stage 2 XML doc)

The `OrbitalElements.ReRootStateVector` helper reads `currentBodyPositionWorld` and `newBodyPositionWorld` as instantaneous values. In Phase 1 (stationary bodies), the values are constant and reads are idempotent. In Phase 4+ when bodies orbit, **the helper must be called fresh each evaluation, never with cached body positions**. Caching either body position across ticks will produce orbital elements that drift away from the body's true position by the body's traveled distance over that interval.

The XML doc on `ReRootStateVector` names this explicitly with the operational consequence visible at a glance:

> "Each call must read body positions fresh from `ReferenceBody.PositionWorld` (or equivalent) at the moment of the call. Holding a `WorldPosition` value across ticks and passing it in to a later re-rooting call will produce orbital elements that drift away from the body's true position by the body's traveled distance over that interval."

The Phase 4+ velocity hazard is documented in parallel: the current signature treats body velocities as zero (correct for Phase 1 stationary bodies); when bodies orbit, the signature extends to take `newBodyVelocityWorld` and `currentBodyVelocityWorld` so the vessel's velocity can be re-expressed in the new body's reference frame. The signature extension is the correct fix (not callsite compensation, which would distribute the correction across every callsite).

## Lessons

### Compile errors silently fall back to the previous test-runner state

Surfaced during Stage 2 verification. After writing 8 new ReRoot tests, the Test Runner reported "209 EditMode green / 0 red / 0 new" — exactly the count from Stage 1, despite the new test file having 18 `[Test]` attributes per filesystem grep. The diagnosis: `OrbitalElements.cs` referenced `WorldPosition` (Coordinates namespace) in the new `ReRootStateVector` parameter list without a corresponding `using SpaceSim.Foundation.Coordinates;` directive. Unity's compiler failed the Vessels assembly; the Vessels.Tests assembly couldn't link against the broken Vessels assembly; **Unity fell back to the previous successfully-compiled assemblies and re-ran the prior test set.**

The failure mode is silent at the Test Runner UI: 209 green, 0 red, 0 new. The compile error surfaces only in Unity's Console pane (red lines). The Test Runner has no UI affordance saying "test discovery failed because the assembly didn't compile."

**Lesson:** when Unity Test Runner shows "unchanged green count after adding tests, no red, no new," the failure mode is almost certainly a compile error in the production assembly the tests depend on, not a test-discovery issue. The diagnostic check: open the Console pane, look for red-line compile errors against any file in the modified asmdef. Fix compile errors first; the test count should jump to the expected value on the next run.

The pattern generalizes: any time the Test Runner shows numbers that match what they were BEFORE the most recent code change, suspect compile failure in the assembly under test or one of its dependencies. The lack-of-failure-feedback is itself the failure signal.

### First-match-wins simplification for overlapping child SOIs

The `VesselSoiRerootingDriver` checks each child body's SOI via `BodyRegistry.GetChildrenOf` iteration order and re-roots to the first child whose SOI the vessel is inside. **For Phase 1, this is a deliberate simplification.** Mathematically a vessel could be inside multiple overlapping child SOIs at once (e.g., a planet's primary moon SOI overlapping a captured-asteroid SOI). The correct behavior would be one of:

- Re-root to the child whose SOI center is closest to the vessel.
- Re-root to the child with highest local gravitational dominance (the body whose μ/r² is largest at the vessel's position).
- Refuse to re-root and log a warning until SOIs are reconfigured to not overlap.

Phase 1 doesn't choose among these — it picks the first child the registry enumerates. The DECISIONS entry names this as a Phase-5+ revisit item; the artifact records it here so future maintainers see the design space.

**The reason this is safe for Phase 1:** the home system as designed in CONSTRAINTS §1 (four intensive-craft bodies plus the rest tuned for the 14-stage pipeline) doesn't have overlapping child SOIs. Real planet-moon configurations don't either — the Hill sphere arithmetic precludes meaningful overlap when bodies orbit stably. The Phase 1 simplification is a deferred-correctness choice, not a known-broken behavior.

### Position-continuity verification pattern for re-rooting tests

`ReRootToBody_FromKeplerRails_PreservesPositionContinuity` uses the pattern: call `GetWorldPosition()` before re-rooting, call `ReRootToBody(newBody)`, call `GetWorldPosition()` after, assert the two world positions match within tolerance. This is the right shape for any operation that changes the reference frame but should not teleport the vessel.

The tolerance choice (1 m absolute at LEO scale) accommodates the orbital-elements round-trip noise inside the implementation: `ReRootToBody` propagates current state via `KeplerPropagator.PropagateState` (which round-trips through orbital-elements internally), passes through `ReRootStateVector` (which calls `ComputeFromStateVector`), and subsequent `GetWorldPosition` calls propagate again from the new elements. Two full elements↔state-vector round-trips at Earth-Moon scales accumulate microsecond-velocity noise that translates to sub-meter position drift over the propagation time involved.

**Lesson:** when validating an operation as "doesn't change observable behavior X," express the assertion as before/after equality on X rather than asserting some intermediate property. The before/after equality is robust to implementation changes that re-compose the internal math.

### NUnit test discovery confirms only public methods

A near-miss caught before submission: `ReRootToBody` was initially marked `internal` to match the "only the driver calls it" framing. EditMode tests live in a separate test assembly (`SpaceSim.Foundation.Vessels.Tests`), which means `internal` would have made the method invisible to tests without an `[InternalsVisibleTo]` attribute on the Vessels assembly. Changed to `public` for parity with the existing `TransitionToKeplerRails` / `TransitionToPhysXActive` methods (same kind-of-private framing; same `public` accessibility).

**Lesson:** in Unity projects with separate test asmdefs, methods that need test coverage must be `public` OR the production assembly must use `[InternalsVisibleTo("...Tests")]`. The codebase convention here is `public` with documentation framing for "internal" callers (the XML doc names the intended caller); future similar additions should follow the same pattern unless a `[InternalsVisibleTo]` AssemblyInfo.cs file is added to the Vessels asmdef.

## Phase 1 status — first system-list item closed

CONSTRAINTS §9 Phase 1 "Foundation" subsection lists: coordinate system, floating origin, reference frames, patched conics, time architecture, three-mode physics, vessel container, deterministic simulation tick, authority attribution scaffolding. Commits 029-043 closed the foundational pieces (coordinate system, floating origin, sim-tick, three-mode physics, vessel container, mode transition procedures + trigger evaluator). Commit 044 closes **reference frame hierarchy + patched conics** — the first multi-body Phase 1 system-list item.

Remaining Phase 1 work, per the PHASE_TRACKER "Systems by phase → Phase 1 — Foundations" section:

- **Time architecture and time-warp** — `SimTickController.SimTickIntervalSeconds` is the 30 Hz constant; `SimTickWarpController` exists with warp-ceiling scaffolding from commit 033 but no actual warp-rate machinery. The full system (continuous time-warp 1× through 100,000×, "warp to next event" UI feature) needs the event-prediction priority queue, which is the next big Phase 1 system.
- **Save/load format** — the schema's serializable per the contract; no implementation yet. CONSTRAINTS §10 flags an open design question: JSON vs binary vs hybrid. A small DECISIONS.md entry would resolve the technology choice before implementation begins.
- **Event prediction priority queue** — CONSTRAINTS §2 "Mode transitions and event scheduling" specifies a single analytic event-prediction priority queue owned by the sim-tick controller. Phase 1 ships pragmatic predictors for: atmospheric entry, surface impact, SOI boundary crossing, scheduled-burn arrival, interstellar-cruise arrival. The KeplerState schema already has nullable fields (`NextPeriapsisTick`, `NextApoapsisTick`, `NextSoiTransitionTick`, `NextModeTransitionTick`) — populating them is the work.
- **Authority attribution scaffolding** — multiplayer-prep, low immediate priority.

The Phase 1 validation milestone from CONSTRAINTS §9 is: "placeholder cube launches from a planet surface, reaches orbit, transfers to a moon, captures into orbit, lands. Time-warp works." With SOI re-rooting in place, the "transfers to a moon" / "captures into orbit" parts of the milestone are mathematically possible; time-warp + event-queue work is what's needed to close the rest.

## User-side replay procedure

1. **Open the project in Unity.** Let it recompile. New files: `BodyRegistry.cs`, `BodyRegistryTests.cs`, `VesselSoiRerootingDriver.cs`. Modified code: `ReferenceBody.cs`, `OrbitalElements.cs`, `Vessel.cs`, `TestVesselDriver.cs`, `Tests/VesselTests.cs`, `Tests/OrbitalElementsTests.cs`, `PlayModeTests/VesselPlayModeTests.cs`. Modified docs: `NETCODE_CONTRACT.md`, `PHASE_TRACKER.md`, `DECISIONS.md`. No new asmdef references needed.
2. **Watch the Console first.** Per the Stage 2 lesson — if any red compile errors appear, surface them before running tests. Test Runner will silently fall back to the previous green count if compilation fails. After this commit's `using SpaceSim.Foundation.Coordinates;` fix the Console should be clean.
3. **Run EditMode tests.** Expect **227 green** (194 commit-043 baseline + 33 new commit-044 tests).
4. **Run PlayMode tests.** Expect **6 green** (unchanged from commit 043; no PlayMode tests added).
5. **Spot check TestVessels.unity Play behavior.** Press Play. The single-body scene + the new `VesselSoiRerootingDriver` should produce no log spam — the driver evaluates the vessel each tick, finds Earth has infinite SOI and no parent, and finds no children of Earth registered. The Console should be clean apart from any pre-existing diagnostic logs.
6. **Git commit and push:**

   ```
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/ReferenceBody.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/BodyRegistry.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/OrbitalElements.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/VesselSoiRerootingDriver.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/OrbitalElementsTests.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/BodyRegistryTests.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/PlayModeTests/VesselPlayModeTests.cs
   git add docs/NETCODE_CONTRACT.md
   git add docs/PHASE_TRACKER.md
   git add docs/DECISIONS.md
   git add commits/044_soi_rerooting.md
   git commit -m "commit 044: SOI re-rooting (first Phase 1 system-list item) — ReferenceBody hierarchy + BodyRegistry + ReRootStateVector + VesselSoiRerootingDriver + NETCODE_CONTRACT §2.7 BodyState"
   git push origin main
   ```

## Notes for future commits

- **Multi-body Play scene.** TestVessels.unity is single-body; the new driver runs but never re-roots. A future commit could extend it (or add TestSoi.unity) with an Earth-Moon body hierarchy to exercise the re-rooting visually during Play. End-to-end visualization is the natural complement to the 33 EditMode tests.
- **Laplace sphere SOI computation.** When Phase 4+ procgen-bodies lands, `ReferenceBody.SoiRadiusMeters` migrates from hand-set Inspector value to computed-from-`a · (m/M_parent)^(2/5)`. The Inspector field stays as override; the procgen pipeline writes the computed value into it. The migration touches `ReferenceBody.cs` only — no driver or math changes.
- **Velocity-frame extension to `ReRootStateVector`.** When bodies orbit, the helper's signature extends to take `currentBodyVelocityWorld` and `newBodyVelocityWorld`. The XML hazard doc names this explicitly; the migration plan is "extend the signature, never compensate at callsites."
- **First-match-wins child selection.** Revisit when multi-body scenes have plausible SOI overlap (Phase 5+). The DECISIONS entry names the three candidate replacement strategies (closest SOI center, highest local gravitational dominance, refuse-and-warn).
- **Next Phase 1 system-list item.** Event prediction priority queue is the natural next system per CONSTRAINTS §2's "Mode transitions and event scheduling" subsection. The KeplerState event-prediction fields already exist as nullable scaffolds; populating them is the work. Time-warp rate machinery depends on the event queue for the "warp to next event" feature.
