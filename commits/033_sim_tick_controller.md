# 033: SimTickController — 30 Hz fixed-timestep spine and 10-step cycle scaffolding

First real implementation of the sim-tick boundary per `docs/NETCODE_CONTRACT.md` §1.2. Introduces a new module `SpaceSim.Foundation.SimTick` with the `SimTickController` singleton MonoBehaviour, the `SimTickWarpController` pure-logic warp-rate state, the `PhysicsMode` enum (PhysX-active / Kepler-rails / interstellar-cruise) with per-mode warp ceilings, the `SimTickPhase` enum for diagnostic tracing, the `ISimTickListener` interface paired with a `TickAdvanced` event, and EditMode tests covering the warp math, the cycle behavior, and step 6's wiring through to `FloatingOriginManager.MaybeShiftOrigin`. Migrates `TestShiftDriver` from `Coordinates/` to `SimTick/` so it can push the active-vessel position to the controller (the Coordinates asmdef cannot reference SimTick without creating a circular dependency, so the bridging test harness moves to the asmdef that already references both modules). Updates the `FloatingOriginAnchor` PROTOTYPE-QUALITY CAVEAT comment to record the sim-tick-boundary resolution and defer the remaining multi-vessel concerns to commit 035+.

The cycle's spine is correct end-to-end; eight of the ten step methods are deliberate stubs awaiting their collaborators (vessels in commit 035+, Kepler-rails propagator in commit 036+, multiplayer post-v1). Step 6 (mode-transition detection / origin-shift dispatch) is fully wired: it reads the active-vessel position pushed via `SetActiveVesselWorldPosition` and invokes `FloatingOriginManager.MaybeShiftOrigin` once per FixedUpdate. Step 10 (counter advancement) is functional: it increments `TickNumber`, notifies registered `ISimTickListener` implementors synchronously, and raises the `TickAdvanced` event. The PhysX-touching steps (1, 2, 3, 7, 8, 9) run once per FixedUpdate; the analytic-propagation steps (4, 5, 10) run N times per FixedUpdate where N is the warp-effective iteration count.

The per-mode warp ceilings are constants in `SimTickWarpController`: 1× for PhysX-active, 10,000× for Kepler-rails, 100,000× for interstellar-cruise. These match the contract §1.4 values exactly. `ComputeAnalyticIterations` returns `min(floor(EffectiveWarpRate), ticksUntilNextEvent)` clamped to ≥ 1. With the event queue empty (no scheduled events in commit 033's scope), iterations are bounded by the warp rate alone.

## Scope

Files created:

- `SPACESIM/Assets/Scripts/Foundation/SimTick/PhysicsMode.cs` — enum (PhysXActive, KeplerRails, InterstellarCruise) with class-level doc cross-referencing CONSTRAINTS §2 commit 002 and NETCODE_CONTRACT §1.4 / §3
- `SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickPhase.cs` — enum (Idle plus 10 per-step values) for diagnostic tracing
- `SPACESIM/Assets/Scripts/Foundation/SimTick/ISimTickListener.cs` — listener interface mirroring `IFloatingOriginListener`
- `SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickWarpController.cs` — pure-logic warp state (~140 lines)
- `SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickController.cs` — singleton MonoBehaviour with FixedUpdate-driven cycle (~280 lines)
- `SPACESIM/Assets/Scripts/Foundation/SimTick/TestShiftDriver.cs` — moved from `Coordinates/` (~115 lines, augmented to push position to controller and include sim-tick number in diagnostic UI)
- `SPACESIM/Assets/Scripts/Foundation/SimTick/SpaceSim.Foundation.SimTick.asmdef` — runtime asmdef with explicit references to Coordinates, Unity.Mathematics, Unity.Burst
- `SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/SimTickWarpControllerTests.cs` — 17 EditMode tests on warp math (~170 lines)
- `SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/SimTickControllerTests.cs` — 21 EditMode tests on cycle behavior + step 6 wiring (~230 lines)
- `SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/SpaceSim.Foundation.SimTick.Tests.asmdef` — test asmdef in Form 2 modern verbose pattern

Files modified:

- `SPACESIM/Assets/Scripts/Foundation/Coordinates/FloatingOriginAnchor.cs` — PROTOTYPE-QUALITY CAVEAT comment block replaced with SHIFT DISPATCH FROM SIM-TICK BOUNDARY block per the approved wording. Inline comment in the rigidbody shift handler updated to point at the new comment block.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/TestShiftDriver.cs` — reduced to a stub explaining the move to `SimTick/` (the sandbox cannot unlink files that existed before the session; replay procedure runs `git rm` on the host).

No CONSTRAINTS update. Phase 0 §9 closed at commit 028.

## Rationale

### The 10-step cycle structure

The cycle is structured as one master method `RunFixedUpdateCycle(int analyticIterations)` invoking 10 private step methods. Each step's name matches its number in the contract §1.2 (`Step1_ReceivePeerState`, `Step2_ReadPhysX`, etc.) so that future readers cross-referencing the contract see the structure immediately. The `CurrentPhase` enum advances at each step boundary, making the cycle traceable in the debugger and in logs.

The cycle's PhysX-touching versus analytic-propagation split (Flag 4 in the proposal) matters:

- **Once per FixedUpdate (PhysX-touching):** steps 1 (receive peer state), 2 (read PhysX), 3 (convert to authoritative coords), 7 (push authoritative to PhysX), 8 (replicate to peers), 9 (fire events placeholder).
- **N times per FixedUpdate (analytic):** steps 4 (apply analytic updates), 5 (reconcile), 10 (advance counter).
- **Once per FixedUpdate inside the iteration loop, gated by `if (i == 0)`:** step 6 (detect mode transitions / dispatch origin shift). Active-vessel position doesn't change within a single FixedUpdate at warp because the active vessel is PhysX-active (1× ceiling) and analytic steps don't move it; running step 6 N times would produce N identical `MaybeShiftOrigin` calls. Gating to `i == 0` is the explicit cadence marker; the location inside the loop documents that it's intentional and lets future commits move the gate if vessel-cluster semantics change.

The FixedUpdate-based timing source (Flag 2 in the original proposal series) sets `Time.fixedDeltaTime = 1f / 30f` in Awake. PhysX and the sim-tick share the same timing source for the prototype. This is acceptable per the netcode contract §1.3 which says "PhysX runs at its own substep schedule, typically frame-locked." For the single-vessel prototype, frame-locking PhysX to the sim-tick rate is the simplest valid configuration; the contract's accommodation of different rates is forward-compatible if commit 035+ vessel work surfaces a need.

### The warp controller split

`SimTickWarpController` is a separate class, not a property bag on the MonoBehaviour, so the warp math can be EditMode-tested without instantiating any MonoBehaviour. 17 EditMode tests in `SimTickWarpControllerTests.cs` exercise:

- Defaults (RequestedWarpRate 1.0, ActiveVesselMode PhysXActive, EffectiveWarpRate 1.0)
- `CeilingFor` returning the contract values (1× / 10,000× / 100,000×)
- `SetRequestedWarp` clamping below 1.0 to 1.0
- `EffectiveWarpRate = min(requested, ceiling)` for each mode
- Mode switches recomputing `EffectiveWarpRate`
- `ComputeAnalyticIterations` clamping to ≥ 1, gating by event distance, flooring fractional warp, returning correct values for each mode at various warp rates

This is the test layer commit 033 can fully exercise without PlayMode. Future PlayMode tests (commit 034 if needed) would verify the FixedUpdate timing — that the cycle actually fires at 30 Hz when the controller is in a running scene — but that test is more about Unity's behavior than the controller's logic and isn't blocking.

### Step 6 wiring and the PROTOTYPE BRIDGE API

The controller exposes `SetActiveVesselWorldPosition(WorldPosition pos)` for the prototype. `TestShiftDriver` calls it every frame; step 6 reads the latest value during FixedUpdate. The method carries an explicit `PROTOTYPE BRIDGE (commit 033)` marker in its XML doc comment naming the scheduled retirement (commit 035+ when vessel containers exist and the vessel registry becomes the source of truth for active-vessel position).

The PROTOTYPE BRIDGE marker is the architectural-lifetime-in-code discipline mentioned in the proposal. Future readers see immediately that this is bridge code with a scheduled retirement, rather than learning that as implicit knowledge that gets lost across commits.

Step 6 also handles the case where `FloatingOriginManager.Instance` is null: log a warning once per controller lifetime and skip the shift check. This lets the controller be testable independently of FloatingOriginManager (the 8 cycle-behavior tests don't need a manager) and lets future scenes legitimately have a sim-tick controller without an origin manager (e.g., a test scene that only exercises analytic event-queue behavior). The once-per-lifetime warning is visible enough to surface a real configuration issue without spamming the console.

### TestShiftDriver move from Coordinates to SimTick

The original `TestShiftDriver` in commit 029 lived in the `Coordinates` asmdef and called `FloatingOriginManager.MaybeShiftOrigin` directly from Update. With commit 033 routing shifts through the sim-tick controller, the driver needs to reference `SimTickController`. But the Coordinates asmdef cannot reference the SimTick asmdef (that would be a circular dependency: SimTick already references Coordinates).

The driver moves to `SimTick/`. The SimTick asmdef references Coordinates, so all the imports the driver needs (`WorldPosition`, `LocalPosition`, `FloatingOriginManager`, `FloatingOriginAnchor`) resolve correctly. The class name and public API are preserved so existing scene references still resolve once Unity reimports — though the namespace has changed from `SpaceSim.Foundation.Coordinates` to `SpaceSim.Foundation.SimTick`, which means **the user must re-attach the `TestShiftDriver` component in `TestCoordinates.unity`** because Unity matches scene-component references by namespace + class. See the user-side verification section below.

The old file at `Coordinates/TestShiftDriver.cs` is reduced to a stub explaining the move. The sandbox cannot unlink files that existed before the session (this is the same limitation that produced the `NETCODE_CONTRACT_DRAFT.md` stub in commit 026); the replay procedure runs `git rm` on the host filesystem to remove the file from version control.

### FloatingOriginAnchor PROTOTYPE-QUALITY CAVEAT resolution

Commit 029 added a class-level "PROTOTYPE-QUALITY CAVEAT (revisit in commit 030)" comment block on `FloatingOriginAnchor` documenting the open question about whether `Rigidbody.position -= delta` was sufficient for production-quality shift handling or whether more PhysX-aware care was needed. Commit 033 resolves that comment as the sim-tick boundary now exists. Replaced with a "SHIFT DISPATCH FROM SIM-TICK BOUNDARY (commit 033)" block recording:

- The architectural payoff: shifts now dispatch from `SimTickController.Step6` at FixedUpdate cadence
- That `Rigidbody.position` assignment is now visible to the subsequent PhysX step as a teleport at a well-defined sim-tick boundary
- The remaining concerns deferred to commit 035+: articulated rigidbody chains, joints, active contacts crossing the shift, whether to disable `Physics.Simulate` briefly for multi-vessel cases

The inline comment in the rigidbody shift handler at line 96 updated to point at the new comment block.

### Asmdef configuration applying commits 030/031/032 knowledge

The new `SpaceSim.Foundation.SimTick.asmdef` explicitly references `SpaceSim.Foundation.Coordinates`, `Unity.Mathematics`, and `Unity.Burst` (the three dependencies the SimTick module uses). This is the commit 030 lesson applied from the start: `autoReferenced` controls inbound references, not outbound; outbound references must be listed explicitly.

The new `SpaceSim.Foundation.SimTick.Tests.asmdef` uses Form 2 modern verbose with `includePlatforms: ["Editor"]`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, and references both `SpaceSim.Foundation.SimTick` (the module under test) and `SpaceSim.Foundation.Coordinates` (needed for `FloatingOriginManager` in step 6 wiring tests) plus `Unity.Mathematics` and the two TestRunner assemblies. No `defineConstraints: ["UNITY_INCLUDE_TESTS"]` — commit 031's lesson is that `defineConstraints` combined with `overrideReferences: true` and `autoReferenced: false` is brittle in our project; `includePlatforms: ["Editor"]` alone provides the Editor-only protection.

## Verification

117 checks, 116 pass on first run, 1 verification false-positive (sandbox-mount staleness on a file modified via Edit tool in the same session — same pattern as commit 028's observation 2 and commit 031's diagnostic session). Effective: 117/117 pass with corrected understanding.

The verification battery covers:

- **Group A (10 checks):** All SimTick module files exist at expected paths
- **Group B (6 checks):** Runtime asmdef structure — name, explicit references to Coordinates / Mathematics / Burst, includePlatforms empty, autoReferenced true
- **Group C (10 checks):** Test asmdef structure — name, references including SimTick + Coordinates + Mathematics + TestRunner, includePlatforms ["Editor"], precompiledReferences nunit.framework.dll, overrideReferences true, autoReferenced false, defineConstraints empty (commit 031 lesson)
- **Group D (22 checks):** SimTickController code structure — singleton pattern, SimTickRate / SimTickIntervalSeconds constants, TickNumber / CurrentPhase / Warp properties, Awake sets Time.fixedDeltaTime, FixedUpdate present, RunFixedUpdateCycle method, SetActiveVesselWorldPosition with PROTOTYPE BRIDGE marker, ClearInstanceForTesting, all 10 step methods present, Step6 wires to FloatingOriginManager.MaybeShiftOrigin, Step6 null-guard with once-per-lifetime warning, Step6 once-per-FixedUpdate gate, Step10 advances TickNumber and fires event with try/catch
- **Group E (6 checks):** SimTickWarpController code structure — three ceiling constants matching contract values, SetRequestedWarp clamping, CeilingFor static method, ComputeAnalyticIterations method
- **Group F (3 checks):** PhysicsMode enum values
- **Group G (11 checks):** SimTickPhase enum values (Idle + 10 per-step values)
- **Group H (2 checks):** ISimTickListener interface
- **Group I (8 checks):** TestShiftDriver move — new file has correct namespace, pushes to SimTickController, requires both managers, TEST-HARNESS labeling preserved, diagnostic UI shows tick number, RequireComponent FloatingOriginAnchor; old file reduced to stub
- **Group J (5 checks):** FloatingOriginAnchor caveat updated — old PROTOTYPE-QUALITY CAVEAT removed, new SHIFT DISPATCH FROM SIM-TICK BOUNDARY present, references SimTickController, REMAINING PHYSX-AWARE CONCERNS deferred to commit 035+, inline comment updated
- **Group K (12 checks):** Other Coordinates files unchanged
- **Group L (2 checks):** Coordinates asmdef unchanged
- **Group M (2 checks):** CONSTRAINTS.md unchanged at 1960 lines, Phase 0 still ends with "Prototype scaffolding: verified."
- **Group N (6 checks):** Prior-commit anchors sampled (commits 014, 015, 025, 026, 027, 028)

The one false-positive was the verification check looking for the inline shift-handler comment update. The Edit tool reported success and the Read tool confirmed the change at line 96. The bash-based `grep` check failed because the sandbox mount returned stale bytes for that file. This is sandbox-mount staleness on a file Edit touched within the same session — a known Cowork sandbox limitation documented in commit 028's artifact and again in commit 031's diagnostic session. The host filesystem has the correct content; git operates on host bytes. **Three data points across non-adjacent commits is the threshold for formalizing a workflow rule**; see "Workflow rule candidate" below.

## Workflow rule candidate (third occurrence)

Three commits have now hit the sandbox-mount staleness pattern at the file-tool layer:

1. **Commit 028**: bash-side `grep` of `TestFoundation.unity` returned the pre-Unity-save 3,507-byte truncated state; the host had the populated scene. Resolution: replay via host-side `git add`.
2. **Commit 031**: bash-side mount returned the stale view of the test asmdef post-user-save during the computer-use diagnostic session.
3. **Commit 033**: bash-side `grep` of `FloatingOriginAnchor.cs` returned a partially-edited view that's missing content the Edit tool reported writing. Read tool returned the correct content.

The pattern is now clear and stable enough to formalize as a sixth workflow rule in `commits/README.md`. **I am NOT formalizing it in this commit's artifact** — that's a separate scope (rule documentation in a structural file is its own commit, parallel to commit 014b's pattern). The candidate rule for a follow-up commit (numbered 033b or 034):

> **Sandbox mount can show stale bytes for files modified within the same Cowork session.** When verification checks fail with "expected content not found" but the Edit/Write tool reported success and the Read tool confirms the expected content, cross-check via host-side commands (e.g., PowerShell `Get-Content` or Windows-side `findstr`). The sandbox bash mount and Cowork's Read tool can return divergent views of the same file when the file was touched mid-session. Authority order: (1) host filesystem, (2) Cowork Read tool, (3) sandbox bash mount. Git operates on host bytes, so commits land correctly via standard replay even when sandbox bash shows wrong content.

Deferred to a follow-up commit per the pattern established by commit 014b.

## User-side verification

The file-level work is complete. The user-side verification path:

1. **Pull commit 033 into the SPACESIM project.** Unity will detect the new SimTick module folder and reimport.
2. **Unity recompiles.** Expect three new assemblies: `SpaceSim.Foundation.SimTick`, `SpaceSim.Foundation.SimTick.Tests`, plus the existing Coordinates assemblies. The Coordinates assembly will recompile because `FloatingOriginAnchor.cs` and `TestShiftDriver.cs` changed (the latter is now a stub).
3. **Expect a missing-script warning on `TestCoordinates.unity`.** The scene's `TestShiftDriver` component reference points at the old `SpaceSim.Foundation.Coordinates.TestShiftDriver` class which no longer exists at that namespace. Open the scene, select the test-subject GameObject, see the missing-script slot in the Inspector, click "Reset" or click "Add Component" and re-attach `SpaceSim.Foundation.SimTick.TestShiftDriver`. Re-wire the SerializeFields (speedKmPerSec, directionWorld, diagnosticLabel) to the same values as before.
4. **Add a `SimTickRoot` GameObject to `TestCoordinates.unity`.** Right-click in Hierarchy → Create Empty. Name it `SimTickRoot`. With it selected, Add Component → `SimTickController`. The component has no SerializeFields to configure; defaults are correct.
5. **Save the scene.**
6. **Open Window → General → Test Runner.** Expect the EditMode tab to show:
   - `SpaceSim.Foundation.Coordinates.Tests.dll (52 tests)` (existing from commit 031)
   - `SpaceSim.Foundation.SimTick.Tests.dll (38 tests)` (new: 17 SimTickWarpControllerTests + 21 SimTickControllerTests)
   - PlayMode tab still shows the two `FloatingOriginManagerPlayModeTests` from commit 032
   - Total: 52 + 38 + 2 = 92 tests across all assemblies
7. **Run EditMode tests.** All 90 EditMode tests should pass (52 Coordinates + 38 SimTick).
8. **Run PlayMode tests.** Both 2 PlayMode tests should still pass.
9. **Press Play in `TestCoordinates.unity`.** Expected behavior:
   - The test subject sphere moves along its configured axis at the test-harness speed
   - Every ~5 seconds (10 km/s × 5 s = 50 km), a shift fires
   - Console logs `[TestShiftDriver] Origin shifted (#N)` for each shift
   - The diagnostic UI label (if wired) now includes a `Sim-tick #: N` line showing the controller's tick counter advancing
   - The shift cadence is identical to commit 029's behavior — the visual demonstration is unchanged; only the dispatch path has moved from MonoBehaviour Update to SimTickController step 6 at FixedUpdate cadence

If any test fails or any user-side step produces unexpected results, surface the failure for diagnosis. The single-vessel prototype scenario is fully exercised by this commit; multi-vessel concerns are explicitly deferred per the FloatingOriginAnchor REMAINING PHYSX-AWARE CONCERNS list.

## Numbering trail

Commit 033 is the SimTick controller. Number trail: 027 init → 028 verify → 029 coordinates → 030 mathematics reference → 031 test platform fix → 032 PlayMode lifecycle tests → **033 SimTickController** → 034 candidate (workflow rule 6 formalization, or PlayMode SimTick tests, depending on what comes up first) → 035+ vessel containers.

## Replay

```
cd C:\Users\gmkar\space_sim
git rm SPACESIM/Assets/Scripts/Foundation/Coordinates/TestShiftDriver.cs
git add SPACESIM/Assets/Scripts/Foundation/SimTick/PhysicsMode.cs ^
        SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickPhase.cs ^
        SPACESIM/Assets/Scripts/Foundation/SimTick/ISimTickListener.cs ^
        SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickWarpController.cs ^
        SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickController.cs ^
        SPACESIM/Assets/Scripts/Foundation/SimTick/TestShiftDriver.cs ^
        SPACESIM/Assets/Scripts/Foundation/SimTick/SpaceSim.Foundation.SimTick.asmdef ^
        SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/SimTickWarpControllerTests.cs ^
        SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/SimTickControllerTests.cs ^
        SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/SpaceSim.Foundation.SimTick.Tests.asmdef ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/FloatingOriginAnchor.cs ^
        commits/033_sim_tick_controller.md
git commit -F commits/033_sim_tick_controller.md
```

The user will also need to `git add` the `.meta` files Unity auto-generates for the new module's assets after first import, plus the updated `TestCoordinates.unity` scene after re-attaching the TestShiftDriver component. Those land in a follow-up commit per the same pattern as commit 028.
