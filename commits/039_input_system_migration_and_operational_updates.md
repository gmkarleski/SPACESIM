# 039: Input System migration for TestVesselDriver + operational doc updates

Resolve the runtime input-API mismatch surfaced during commit 038's end-to-end Play verification of TestVessels.unity, plus two operational doc updates that were pending after commit 038. Three small concerns landing as one logical scope because they're all the commit-038-aftermath cleanup.

## Scope

- `SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs` — modified. Replace `Input.GetKeyDown(KeyCode.Space)` with `Keyboard.current.spaceKey.wasPressedThisFrame` from the new Input System package. Add `using UnityEngine.InputSystem;`. Guard against a null `Keyboard.current` (edge case: headless test runs, gamepad-only devices) with a non-null check before the key read. Replace the brief inline comment with a 5-line explanation of why the migration was needed and how the new API maps to the old one.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/SpaceSim.Foundation.Vessels.asmdef` — modified. Add `Unity.InputSystem` to the `references` array. The package's asmdef name is `Unity.InputSystem` (verified from `Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Unity.InputSystem.asmdef`). The Unity Input System package was already installed in the project (per `Packages/manifest.json`: `"com.unity.inputsystem": "1.19.0"`); only the Vessels asmdef needed to opt in to the reference.
- `docs/DECISIONS.md` — modified. Added two new entries to the "Resolved decisions" section after the workflow rule 6 entry:
  1. **VesselRegistry as plain static class (no deferred-registration)** — locked-in commit 038 decision that was pending DECISIONS.md migration. Includes the reusable pattern-selection criterion ("does the class have a non-null window? If yes, pending queue. If no, direct registration") as cross-codebase insight.
  2. **TestVesselDriver uses new Input System API** — locked-in this commit's decision, with alternatives considered (legacy `UnityEngine.Input`, dual-API preprocessor approach, project-setting flip) and why each was rejected.
- `docs/PHASE_TRACKER.md` — modified. Three small edits:
  1. Added commit 039 row to Recently landed table
  2. Added commit 038 row to Recently landed table (had been pending since commit 038's own update didn't include itself)
  3. Toggled "Vessel containers per netcode contract §2" checkbox to checked, with reference to both commits 038 and 039 in the line
  4. Updated Verification state test counts to reflect commit 038's contributions (146 EditMode + 6 PlayMode = 152)
  5. Updated end-to-end Play verification status to mention both TestCoordinates and TestVessels scenes
- `commits/039_input_system_migration_and_operational_updates.md` — created (this artifact)

No CONSTRAINTS.md changes. No NETCODE_CONTRACT.md changes. No ARCHITECTURE.md changes. No SESSION_PROTOCOL.md changes. No code changes outside the two Vessels-module files. No tests added or modified. The 152-test total from commit 038 remains green; no new tests are needed because the input migration is end-to-end Play behavior (no EditMode test path exercises Input).

## Rationale

### Why the input migration is needed

Commit 038's end-to-end Play verification of TestVessels.unity surfaced this exact error in the Unity Console:

```
InvalidOperationException: You are trying to read input using the UnityEngine.Input
class, but you have switched active input handling to Input System package in
Player Settings.
```

The project's `ProjectSettings/ProjectSettings.asset` has `activeInputHandler: 1`, which is Unity's "Input System Package only" setting (value 0 = legacy, 1 = new, 2 = both). The commit-038 `TestVesselDriver.cs` used `Input.GetKeyDown(KeyCode.Space)` from the legacy `UnityEngine.Input` namespace, which throws this exception at runtime when the project is set exclusively to the new system.

Three architectural payoffs from commit 038 still demonstrated successfully during the broken Play session — TestVessel's X position dropped from 7,000,000 down to 29,000 over a few seconds, indicating multiple floating-origin shifts fired correctly, confirming that:
- Vessel.Initialize correctly added Rigidbody and FloatingOriginAnchor at runtime
- SimTickController.SetActiveVessel propagated the active vessel to step 6
- Step 6 read ActiveVessel.GetWorldPosition() each FixedUpdate
- FloatingOriginManager.MaybeShiftOrigin fired when the 50 km threshold was crossed
- FloatingOriginAnchor moved the rigidbody via the deferred-registration plumbing from commit 034

The only broken piece was the Space-key mode-transition trigger. The architecture works end-to-end; the input API was the only thing in the wrong place.

### Why the new Input System API rather than alternatives

Three alternatives considered, each rejected:

1. **Switch project setting from "Input System Package" to "Both"** — would make legacy `Input.*` work alongside the new system. Rejected because aligning with Unity 6 default reduces drift risk; the migration cost is small (one file, one asmdef reference); and a future project-wide migration to "Input System only" would re-create the problem.

2. **Dual-API approach with `#if ENABLE_LEGACY_INPUT_MANAGER` / `#if ENABLE_INPUT_SYSTEM` preprocessor directives** — would work regardless of which Active Input Handling is set. Rejected as over-engineering for one Phase 0 test driver. The pattern has value in libraries that need to support both modes, but TestVesselDriver is internal test-scene scaffolding, not a redistributable component.

3. **Just migrate this file to the new API** (chosen) — single-file scope, no preprocessor complexity, aligns with project defaults, no test coverage impact. The new API is the standard for Unity 6 going forward.

### Why `Keyboard.current.spaceKey.wasPressedThisFrame` specifically

Two equivalent forms exist in the new Input System:
- `Keyboard.current.spaceKey.wasPressedThisFrame` — typed property access (per-key, named accessors)
- `Keyboard.current[Key.Space].wasPressedThisFrame` — indexer access with Key enum

Both compile to equivalent IL. The typed-property form is the recommended canonical style per Unity's Input System documentation for named keys. The InputSystem package's official samples (`Library/PackageCache/com.unity.inputsystem@.../Samples~/SimpleDemo/`) use the same `<device>.<key>.wasPressedThisFrame` pattern for `gamepad.buttonSouth.wasPressedThisFrame`.

`wasPressedThisFrame` is the new-Input-System equivalent of legacy `Input.GetKeyDown` — fires `true` exactly once on key-down transition, returns `false` for held keys after the first frame. The semantic mapping is one-to-one.

### Why guard against null `Keyboard.current`

The new Input System exposes `Keyboard.current` as a static accessor that returns the most-recently-used keyboard device, or `null` if no keyboard is currently attached. In normal desktop play, `Keyboard.current` is non-null. The edge cases that produce `null`:

- Headless test runs (PlayMode tests with no input devices configured)
- Gamepad-only sessions where the keyboard hasn't been touched yet
- VR or mobile device profiles where keyboards aren't part of the device list

Adding the null check makes TestVesselDriver robust against these without affecting normal play. The cost is one line; the benefit is that the script doesn't throw NullReferenceException in CI-style runs.

### Why the two operational doc updates land here

Both updates were pending after commit 038 wrote — the commit 038 artifact's "Notes for future commits" section explicitly listed them as small follow-on edits. Rolling them into commit 039 keeps the operational state synchronized with the Vessels-module work without requiring a separate operational commit.

**DECISIONS.md additions:**
- The VesselRegistry-as-static-class decision was discovered during commit 038's design discussion (Stage 1 surfacing the reflexive pattern copy, then the simplification analysis). The reusable insight — the pattern-selection criterion based on "does the class have a non-null window?" — is the genuinely valuable part of the entry, providing future maintainers a one-question test for which registry pattern to use.
- The TestVesselDriver Input System decision is locked here in commit 039 itself. Documenting alternatives (legacy, dual-API, project-flip) makes the rejection rationale durable.

**PHASE_TRACKER.md additions:**
- Commit 038 missed adding its own row to the Recently landed table (same pattern as commit 036 missing its own row, fixed retroactively in commit 037). Adding commit 038's row here makes the table accurate.
- Toggling the "Vessel containers" checkbox to checked acknowledges that commit 038 lands the §2 schema and §3.1 transition mechanism. The remaining two checkboxes (Kepler-rails mode test + comprehensive mode transition test) stay unchecked because this commit doesn't address them.
- Test count update from 106 to 152 reflects the +46 tests commit 038 added (+11 VesselTests + +8 VesselRegistryTests + +10 OrbitalElementsTests + +4 SimTickController step-6 refactor tests + +3 VesselPlayModeTests).

## Verification

All checks below must pass.

### TestVesselDriver.cs migration landed correctly

- File contains the line `using UnityEngine.InputSystem;` near the top of the file.
- File contains the phrase `Keyboard.current` exactly twice (once in the local variable assignment, once nowhere else — the local is then used for the key read).
- File contains the phrase `spaceKey.wasPressedThisFrame` exactly once.
- File contains the null-guard expression `keyboard != null &&`.
- File does NOT contain `Input.GetKeyDown` anywhere (grep returns 0).
- File does NOT contain `KeyCode.Space` anywhere (grep returns 0).
- The Space-key handling block still calls `TransitionToKeplerRails()` for the PhysX-active → Kepler-rails branch and `TransitionToPhysXActive()` for the reverse branch, unchanged.

### Vessels asmdef references updated

- `SpaceSim.Foundation.Vessels.asmdef` `references` array contains `Unity.InputSystem` (exact spelling).
- The existing references (`SpaceSim.Foundation.Coordinates`, `SpaceSim.Foundation.SimTick`, `Unity.Mathematics`) are preserved verbatim.

### DECISIONS.md updates

- Two new entries appear in the "Resolved decisions" section after the "Workflow rule 6" entry and before the `---` section separator.
- Entry 1 heading: `### VesselRegistry as plain static class (no deferred-registration)` (line-anchored `^### `).
- Entry 1 contains the reusable pattern-selection criterion phrase `does the class have a non-null window?` exactly once.
- Entry 1 contains the locked-in reference `**Locked in:** commit 038.`.
- Entry 2 heading: `### TestVesselDriver uses new Input System API (not legacy UnityEngine.Input)` (line-anchored `^### `).
- Entry 2 contains the locked-in reference `**Locked in:** commit 039.`.
- Pre-existing decisions preserved verbatim (workflow rule 6 entry distinctive content `For files Unity has written or Cowork's Edit tool has modified mid-session` still present).

### PHASE_TRACKER.md updates

- Recently landed table contains rows for commits 039 and 038 in newest-first order: row order at top is 039, 038, 037, 036, ..., 026.
- Phase 0 remaining work checklist has the line `- [x] Vessel containers per netcode contract §2 (commit 038, with Input System migration in commit 039)` (checked, both commits referenced).
- Two unchecked items remain: Kepler-rails mode test and mode transition test.
- Verification state line reads `146 EditMode + 6 PlayMode = 152 total, all green` (with commit-038 noted as the baseline).
- End-to-end Play verification line mentions both TestCoordinates and TestVessels scenes.

### Workflow rules preserved

- `commits/README.md` contains exactly six `^### ` headings under the "Workflow rules learned from experience" section.
- All six rules' distinctive phrases present (per commit 038's verification battery, which spot-checked them).

### CONSTRAINTS.md untouched

- `docs/CONSTRAINTS.md` not modified; byte count unchanged from post-commit-036 state (230,948 bytes).

## Replay

```
cd C:\Users\gmkar\space_sim

git add SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/SpaceSim.Foundation.Vessels.asmdef
git add docs/DECISIONS.md
git add docs/PHASE_TRACKER.md
git add commits/039_input_system_migration_and_operational_updates.md

git commit -m "commit 039: Input System migration + operational updates"
git push
```

After git replay, the user-side actions in Unity:

1. Let Unity recompile. The Vessels asmdef now references `Unity.InputSystem`, so the package's types resolve. Console should show no compile errors and no new warnings related to TestVesselDriver.
2. Test Runner → EditMode → Run All. Expected: **146 green** (unchanged from commit 038). The input migration doesn't add or remove tests.
3. Test Runner → PlayMode → Run All. Expected: **6 green** (unchanged).
4. Open `Assets/Scenes/TestVessels.unity` (the scene authored after commit 038 lands).
5. Press Play. Run for ~15-20 seconds. Verify:
   - Floating-origin shifts still fire correctly (same behavior verified end-to-end in commit 038): vessel moves at 10 km/s, shifts at 50 km threshold every ~5 s, diagnostic UI updates each frame.
   - **No `InvalidOperationException` about UnityEngine.Input in Console.** This was the failing condition in commit 038's end-to-end verification.
   - Press Space: vessel transitions to Kepler-rails mode. Diagnostic UI shows `Mode: Kepler-rails (epoch tick: N)`. One-time `PHASE 0 LIMITATION` message appears in Console.
   - Press Space again: vessel transitions back to PhysX-active. Rigidbody resumes motion from the position the vessel had at the moment of the first Space press (Phase 0 propagator-not-yet-built limitation; expected behavior, not a bug).
6. Stop Play mode. Console should be clean of unexpected errors.

If any of these checks fail, surface for diagnosis before considering commit 039 complete.

## Lesson recorded

**Verify Unity project's Active Input Handling at code-write time when writing scripts that read input.** Unity 6 defaults to the new Input System package (`activeInputHandler: 1`), where the legacy `UnityEngine.Input` API throws `InvalidOperationException` at runtime. The compile step does NOT catch this — `Input.GetKeyDown` compiles cleanly in both modes; the failure only surfaces at Play-mode runtime.

The check that catches this at write-time: look at `Packages/manifest.json` for `"com.unity.inputsystem"` (if present, the new system is available) and `ProjectSettings/ProjectSettings.asset` for `activeInputHandler:` value. Match the input API used in the script to the project's setting:

| activeInputHandler | Legacy `Input.*` | New `Keyboard.current.*` |
|---|---|---|
| 0 (Input Manager Old) | ✓ works | ✗ Keyboard.current is always null |
| 1 (Input System Package) | ✗ throws at runtime | ✓ works |
| 2 (Both) | ✓ works | ✓ works |

The recorded lesson goes into the commit 038 lessons section as the sixth entry (or as a new operational rule if the pattern recurs). For now, single data point — not yet a workflow rule.

This is the architectural-level pattern: **input handling is one of several Unity systems where the project's settings determine which API works at runtime, but the compiler doesn't enforce the match**. Other systems with similar runtime-vs-compile mismatch:
- Render pipelines (URP vs HDRP vs built-in: scripts referencing pipeline-specific types compile but fail in another pipeline)
- Networking transport (different transport implementations expose similar APIs that work in different runtime configs)
- Physics (2D vs 3D: scripts using `Rigidbody2D` types compile in 3D-only projects but fail at runtime)

The general principle: when writing Unity scripts that depend on a project-level Setting, document the assumption explicitly in the script's class-level XML doc, and verify the project setting matches at write-time rather than relying on the compiler.

## Notes for future commits

- **Phase 0 is now 10 of 12 items complete.** Two remaining before Phase 1 implementation can honestly begin: Kepler-rails mode test, comprehensive mode transition test (per §3.1's full trigger conditions). Both are single-session implementations.
- **Kepler-rails propagator** (Step 4 actual implementation) is a separate prior dependency for the comprehensive mode-transition test. It may land as its own commit (probably 040) before the test commits.
- **Companion docs** (`docs/code/<system>.md`) haven't started landing yet. The first one is likely `docs/code/coordinate_system.md` for the Coordinates module, but it lands when the coordinate system gets a non-trivial extension or refactor, not preemptively. The Vessels module is also a candidate (it's the biggest single module so far), but per the template's own guidance, companion docs are written when there's something substantive to document beyond what the class XML docs already capture.
- **PHASE_TRACKER discipline** continues per the simplification from commit 037: checklist items use `(commit NNN)` references with no "this commit" annotations.
- **DECISIONS.md migration pattern** continues per the precedent established in commit 036: resolved decisions move from CONSTRAINTS.md §10 (or surface during implementation) to DECISIONS.md with full Date/Question/Decision/Alternatives/Implication/Locked-in formatting. Pending decisions stay as verbatim bullets in DECISIONS.md's "Pending decisions" section, mirroring §10.
