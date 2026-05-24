# 049: Active-vessel camera-follow

Add a minimal `MonoBehaviour` to the Main Camera in `TestVessels.unity` so the active vessel stays in view during Play. Single-commit scope, ~150 LOC of new code in one file plus the scene change and a DECISIONS entry. No new tests (visual-only behavior; no automated verification possible without a play-mode test harness — overkill for a debug utility this size).

This is the **first of three commits** identified in `docs/phase1_validation_readiness.md` Section C as the work that unblocks end-to-end Phase 1 validation. The audit's verdict on this gap: *"the most significant visual-validation usability gap in the audit"* — without it, the active vessel leaves the Main Camera's frustum within ~5 seconds at `TestVesselDriver`'s default 10 km/s initial velocity, and all Play feedback funnels through the on-screen Canvas diagnostic. With it attached, the vessel stays centered across floating-origin shifts.

## Scope

- **New file** `SPACESIM/Assets/Scripts/Foundation/SimTick/ActiveVesselCameraFollow.cs` — sealed `MonoBehaviour` with a single Inspector field (`_offset`, default `(0, 5, -20)`), `Awake` validation that warns if the component is attached to a GameObject without a `Camera`, and `LateUpdate` that reads `SimTickController.Instance?.ActiveVessel?.GetWorldPosition()`, converts to local via `FloatingOriginManager.Instance.WorldToLocal(WorldPosition)`, and assigns `transform.position = localPos.Value + _offset`. Null-safe at every level with once-per-lifetime warnings matching `SimTickController.Step6_DetectModeTransitions` discipline (one warning per missing dependency, suppressed thereafter so the Console isn't spammed each frame in a misconfigured scene). Rotation is left alone — scene author sets look direction once.
- **Scene change** `SPACESIM/Assets/Scenes/TestVessels.unity` — `ActiveVesselCameraFollow` component attached to the existing Main Camera GameObject (which previously had only the default Unity components: Transform, Camera, AudioListener, UniversalAdditionalCameraData). `_offset` left at default `(0, 5, -20)`.
- **DECISIONS amendment** `docs/DECISIONS.md` — new entry "Active-vessel camera-follow (commit 049)" capturing Path A vs Path B decision (Path A chosen: explicit world→local conversion via the public `FloatingOriginManager.WorldToLocal` API; Path B's `FloatingOriginAnchor`-based approach rejected because it requires extending `IActiveVessel` with a `Transform` accessor, a cross-asmdef API change unjustified for a debug utility).
- **Artifact** `commits/049_active_vessel_camera_follow.md` — this file.

**Test count delta:** 0. Pre-commit 339 EditMode + 6 PlayMode = 345 green. Post-commit 339 EditMode + 6 PlayMode = 345 green (unchanged). Visual-only behavior; no automated verification.

## Architecture

Three things kept this commit small:

**1. World→local conversion path is already public.** `FloatingOriginManager.WorldToLocal(WorldPosition)` (line 224 of `FloatingOriginManager.cs`) is a public convenience method delegating to `CoordinateMath.WorldToLocal(w, CurrentOrigin)`. The spec hinted at a `OriginShift` field; reconnaissance found that the actual API is `WorldToLocal` (with `CurrentOrigin` exposed read-only as a `WorldPosition`). One existing public method means no new API surface for this commit.

**2. The asmdef direction stays clean.** The camera-follow MonoBehaviour lives in `SpaceSim.Foundation.SimTick` rather than `SpaceSim.Foundation.Vessels`. SimTick.asmdef already references Coordinates, so `WorldPosition` / `LocalPosition` / `FloatingOriginManager` are accessible. The narrow `IActiveVessel` interface (only `GetWorldPosition()` + `Mode`, by design — see the doc on `IActiveVessel.cs:18-27`) gives the follow logic exactly what it needs from the active vessel without coupling to concrete `Vessel`. No new asmdef references needed.

**3. Mid-frame origin shifts are handled correctly without special-casing.** `SimTickController.FixedUpdate` calls `FloatingOriginManager.MaybeShiftOrigin` synchronously inside Step 6 (line 507). `MaybeShiftOrigin` updates `CurrentOrigin` BEFORE notifying listeners (line 258 of `FloatingOriginManager.cs`, deliberately ordered so listeners can call `WorldToLocal` mid-handler and get correct local coords). By the time `LateUpdate` runs later in the same frame, `CurrentOrigin` is the post-shift value; the camera's `WorldToLocal` call converts against the new origin and lands smoothly. No anchor-registration needed for the camera itself — the world-position-then-convert path is shift-invariant.

## Design decision

See `docs/DECISIONS.md` "Active-vessel camera-follow (commit 049)" for the Path A vs Path B decision and rejected alternatives (camera rotation tracking, smoothing, vessel-switch input — all deferred). Headline:

- **Path A chosen (read world position, convert in `LateUpdate`).** SimTick asmdef self-sufficient; no extension of `IActiveVessel`; single conversion source-of-truth via `FloatingOriginManager.WorldToLocal`.
- **Path B rejected (camera as `FloatingOriginAnchor`).** Would have required a `Transform` accessor on `IActiveVessel` — cross-asmdef interface extension unjustified for a debug-only utility.

## Lessons

**Spec-vs-impl details land cleanly when recon catches them.** The spec hinted at a `FloatingOriginManager.OriginShift` field that doesn't exist literally — the actual public API is `WorldToLocal(WorldPosition)` returning `LocalPosition`. Reconnaissance caught the difference before any code was written; the implementation uses the real API and the DECISIONS entry documents the (more accurate) name. Spec hints are leads, not invariants.

**Pre-existing Console warnings are cheap to mis-read as breakage.** The pre-existing-and-benign CS0414/CS0219 unused-variable warnings (`WarpController._pendingNumerator` and `AtmosphericEntryPredictorTests.threshold`) were already in the Console at session start; an earlier-session Test Runner had left 15 stale errors visible. Surface-back-to-Gray rather than guessing was the right move — the user host-side ran the Test Runner and confirmed 345 green, ruling out real breakage in two minutes. Workflow rule 6 (host is canonical) earns its weight again.

**Visual validation requires checking Game view (Main Camera output) not Scene view (editor free camera).** "I don't see the vessel" during Play almost always means looking at the wrong view or reading Inspector outside Play mode, not a render bug. Diagnostic logging confirmed in 3 seconds what 30 minutes of speculation didn't.

## What's next

Two more commits are queued in `docs/phase1_validation_readiness.md` Section C:

- **Commit candidate 2 — multi-body test scene.** Extend `TestVessels.unity` with a Moon child body OR build a separate `TestSoi.unity`. Unblocks the three deferred end-to-end Play verifications PHASE_TRACKER explicitly flags at line 63: SOI re-rooting (commit 044), SOI-crossing halt (commit 046), atmospheric-entry halt (commit 047). The math is complete and EditMode-tested; only the Play-mode demonstration is missing. Single-session work assuming computer-use drives the scene editing.
- **Commit candidate 3 — Phase 1 validation milestone artifact.** Run the validation scenarios with commit 049 + commit-candidate-2 in place. Record results (Console output, tick-counter readings, screenshots) into `docs/phase1_validation_results.md`. Closes the Phase 1 foundations layer formally and gives Phase 2+ work a clean handoff point.

Independent of the validation track: save/load remains the next major architectural commit (the Phase 1 system-list item PHASE_TRACKER flags as "parallel track"). The schema hooks added in commit 048 Stage 1 (per-predictor `KeplerState` fields, `IsRoutineSupply` flag, rational `WarpRate`) are in place ready for that work.
