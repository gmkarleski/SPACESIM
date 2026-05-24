# Phase 1 validation incomplete — KeplerPropagator degenerate-orbit bug found during scenario 2

**Date:** 2026-05-24
**HEAD at session:** `16cc1d5` (commit 051, multi-body TestVessels.unity with Moon + SOI-crossing test vessel)
**Validation arc:** per `docs/phase1_validation_readiness.md` Section C, commit candidate 3
**Methodology:** Hybrid Option C (human-driven Play with structured Cowork capture) per amended commit 052 spec

## Session conditions

The validation session was conducted in Unity 6.4 (6000.4.7f1) against the post-commit-051 scene state. Two operational disturbances are noted for accuracy, neither affecting the validity of the central finding:

1. **Recurring window overlay friction** from Phone Link and Microsoft Store windows masking Unity's left half during early scenario captures. Closing Phone Link mid-session removed most of the friction; Microsoft Store remained but Unity rendered cleanly afterward.
2. **One Unity Editor freeze** during scenario 2 setup. Force-recovery via Task Manager produced the in-Editor crashed state in which the bug's evidence first became visible: 5 errors and 999+ warnings in the Console (the Console's warning counter caps display at 999).

After Test Runner pass + clean Unity exit at session end, the scene title bar showed "TestVessels" with no asterisk and Unity exited via normal close with no save prompt. Working tree remains clean against `16cc1d5` per host-side `git status`.

Neither operational disturbance is the central finding; both are noted here once and not elaborated further. The central finding is a real production bug in `KeplerPropagator`'s degenerate-orbit handling, discovered when the Editor's NaN cascade made the bug's signature unmistakable.

## Executive summary

Phase 1 validation setup completed cleanly. Scenario 1 (ballistic coast) was demonstrated and signed off at sim-tick #416 with all evidence locked. Scenario 2 (PhysX → KeplerRails transition) revealed a production bug in `KeplerPropagator`: a purely-radial initial velocity produces a degenerate orbit (eccentricity = 1 with zero angular momentum) that the Newton-Raphson hyperbolic Kepler solver cannot resolve, returning NaN. The NaN propagates through `Vessel.GetWorldPosition` into the diagnostic UI and `ActiveVesselCameraFollow`, producing a per-frame `transform.position` rejection error from Unity.

The validation arc halted at scenario 2. Scenarios 3 through 8 cannot proceed without the fix. Per the spec's failure-mode discipline, this commit lands as the failure-mode artifact; the fix lands in a follow-up commit (provisionally 053); re-validation follows (provisionally 054).

Test Runner at session time: 339 EditMode + 6 PlayMode = **345 green**. The bug is uncaught by current test coverage. This is a test-coverage gap finding alongside the production bug.

The validation arc found a real production bug before completing its happy-path demonstration. That is the validation arc working as designed: the failure-mode framing existed in the spec precisely for this contingency.

## Bug diagnosis

### Initial conditions of TestVessel

Per `TestVessels.unity` post-commit-051 state:

| Field | Value |
|---|---|
| Position | (7,000,000, 0, 0) m |
| Velocity | (10,000, 0, 0) m/s |
| Reference body | Earth (ReferenceBody with mass 5.972E+24 kg) |
| Mode after `Initialize` | PhysXActive |

Both position and velocity vectors point purely along the +X axis. They are exactly parallel.

### Mathematical chain to the bug

**Angular momentum vector.** `h = r × v`. For parallel `r` and `v`, the cross product is zero. Therefore `h = 0` exactly.

**Eccentricity formula.** `e = sqrt(1 + 2εh²/μ²)` where `ε` is specific orbital energy and `μ` is the body's gravitational parameter. With `h = 0`, the term `2εh²/μ²` collapses to zero regardless of energy, and the formula reduces to `e = sqrt(1) = 1` exactly.

A purely-radial trajectory is therefore a degenerate-orbit case: not truly elliptical (which uses `M = E − e·sin(E)`), not truly hyperbolic (which uses `M = e·sinh(H) − H`), but a 1D collapse-and-rebound that requires its own rectilinear-orbit math (universal-variable formulation or time-dependent radial equation).

**Dispatcher behavior.** `KeplerPropagator.PropagateState` routes orbits with `e ≥ 1` to `OrbitalElements.SolveKeplerHyperbolic`. The path was designed for genuine hyperbolic orbits (`e > 1`); `e = 1` is the parabolic boundary, and `e = 1` with `h = 0` is the radial degenerate case the dispatcher conflates with hyperbolic.

**Newton-Raphson at e = 1.** The hyperbolic Kepler equation `M = e·sinh(H) − H` has a Newton-Raphson iteration whose derivative term involves `e·cosh(H) − 1`. At `e = 1`, the iteration's correction step `(M − (e·sinh(H) − H))/(e·cosh(H) − 1)` has a near-singular denominator for small `H`, and the iteration fails to converge in 15 steps. Per `OrbitalElements.SolveKeplerHyperbolic` (line 962), the method returns `NaN` with a warning rather than throwing.

**Observed Console output during scenario 2:**

The warning fires per frame at warp rates, generating 999+ entries with `e = 1` and various `M` values:

```
OrbitalElements.SolveKeplerHyperbolic: Newton-Raphson did not converge
within 15 iterations for M = 3.9989655550764898, e = 1. Returning best
estimate NaN; position error may exceed tolerance.
```

### NaN cascade downstream of the failed solve

The NaN propagates through the per-frame Vessel-position read chain:

1. `OrbitalElements.SolveKeplerHyperbolic` returns NaN (`OrbitalElements.cs:962`).
2. `OrbitalElements.MeanToTrueAnomaly` propagates NaN (`OrbitalElements.cs:645`).
3. `KeplerPropagator.PropagateState` propagates NaN (`KeplerPropagator.cs:147`).
4. `Vessel.GetWorldPosition` returns a `WorldPosition` whose `.Value` is `(NaN, NaN, NaN)` (`Vessel.cs:715`).
5. `TestVesselDriver.BuildDiagnosticText` reads this position and renders `World: (NaN, NaN, NaN)` into the diagnostic UI (`TestVesselDriver.cs:278`).
6. `ActiveVesselCameraFollow.LateUpdate` calls `FloatingOriginManager.WorldToLocal(world)` on the NaN; the local position is also NaN; the assignment `transform.position = localPos.Value + _offset` produces NaN.
7. Unity rejects the NaN assignment with a hard error visible in the Console:

```
transform.position assign attempt for 'Main Camera' is not valid.
Input position is { NaN, NaN, NaN }.
```

The error fires every frame at warp rates, producing the 5 distinct error entries observed in the Console (Unity de-duplicates identical errors past a certain count). The error is logged as `UnityEngine.Transform:set_position`.

### Editor freeze hypothesis

The Editor freeze observed earlier in the session is consistent with the NaN cascade overwhelming a renderer or physics fallback path: the per-frame Unity error handler does work that becomes expensive when called every frame at high warp rates, and the cascade through camera-follow may have starved the main thread. The freeze is not the bug; it is a downstream consequence of the bug under sustained NaN load. The bug itself is the degenerate-orbit handling failure that produced the NaN in the first place.

### Diagnostic UI capture at session time

Per the off-screen photograph captured during the session, post-recovery:

| Field | Value at capture |
|---|---|
| Mode | Kepler-rails (epoch tick: 99) |
| World position | (NaN, NaN, NaN) |
| Local position | (33000.0, 0.0, 0.0) |
| Origin | (7000000.0, 0.0, 0.0) |
| Dist from origin | NaN m / Threshold: 50000.0 m |
| Shift count | 1 |
| Sim-tick # | 120 |

The mode line correctly shows the transition completed; the propagator was called per frame after epoch tick 99 and returned NaN starting at tick 100, accumulating ~20 ticks of NaN-propagated position by the capture.

## Bug scope and architectural shape

The fix is larger than a single patch to `KeplerPropagator`. Three coordinated work items emerge from the finding:

### Work item 1: Production fix in `KeplerPropagator` (the blocker)

Guard the degenerate-orbit case where `h ≈ 0`. Two viable approaches, decision deferred to the fix commit's spec:

- **(a) Radial closed-form math.** Add explicit rectilinear-orbit handling for purely-radial trajectories: 1D collapse-and-rebound via universal-variable formulation or the time-dependent radial equation. This honors the trajectory's geometry. Real but non-trivial mathematical work.
- **(b) Guard-and-reject.** In `Vessel.TransitionToKeplerRails`, detect `|h| < epsilon` before allowing the transition; refuse with a clear error log ("vessel velocity is purely radial; KeplerRails transition not supported for degenerate orbits at this revision"). Cheaper near-term; doesn't pretend to handle radial orbits, only prevents the NaN cascade. Acceptable as a stopgap if (a) is multi-session work.

The fix commit's spec will resolve which approach. Both leave production code in a defensible state; the choice depends on whether radial Kepler orbits are a real Phase 1+ scenario the game needs to support (e.g., for vertical launches from a body surface, sub-orbital ballistic trajectories) or whether they're an edge case that the test harness alone produces.

### Work item 2: Newton-Raphson eccentricity helper landing

The producer-bot audit pipeline has flagged a missing test-side helper for Newton-Raphson convergence assertions for **five audit cycles**. Each prior cycle marked it as outstanding-but-not-urgent because the active commit was in territory unrelated to predictor work (warp controller, UI, scene construction).

The deferred helper would let test code write assertions like `AssertSolvableEccentricity(orbit)` instead of inlining `e < 0.8` guards that mask convergence failures. A test exercising purely-radial Kepler transition would naturally use the helper.

This finding closes the "not urgent" framing on the helper. The bug it would have caught is real, in production, and uncaught by existing tests. The helper landing pairs naturally with the production fix in a coordinated work block.

### Work item 3: New test covering degenerate-orbit transition

The test coverage gap is real (345 green at session time with the bug present and reachable through normal vessel transition). A new EditMode test covering purely-radial Kepler transition should land alongside the fix, using the helper from item 2 and asserting either (a) the closed-form math handles the radial case correctly, or (b) the guard rejects with the expected diagnostic, depending on which fix approach is chosen.

Test name suggestion: `KeplerPropagatorTests.PurelyRadialTransition_DegenerateOrbit_HandledCorrectly`.

### Items 1-3 coordinate but do not require simultaneous landing

The production fix is the blocker for re-validation (scenarios 2-8 cannot proceed without it). Items 2 and 3 are independent infrastructure work that could land in the same commit as item 1, or separately. The fix commit's spec will resolve the scheduling.

## Test Runner data point

Captured host-side during the session, after the bug had become visible in the Editor:

| Suite | Count | Result |
|---|---|---|
| EditMode | 339 | All green |
| PlayMode | 6 | All green |
| **Total** | **345** | **All green** |

The bug exists in production code reachable through the canonical `TestVessel`'s normal mode transition; no test in the current suite exercises this path. **The test suite did not catch this bug.** Three-work-items framing in the preceding section captures the architectural response.

## Scenario 1 evidence (preserved)

The validation arc produced clean evidence for scenario 1 (ballistic coast) before halting at scenario 2. Captured at the toolbar-Paused state during 1× warp:

| Field | Value |
|---|---|
| Sim-tick | #416 |
| Mode | PhysX-active |
| World position | (7138666.6, 0.0, 0.0) m |
| Local position | (38000.0, 0.0, 0.0) m |
| Origin | (7100000.0, 0.0, 0.0) m |
| Distance from origin | 38000.0 m (threshold: 50000.0 m) |
| Shift count | 3 |
| Console | 0 errors / 0 warnings / 0 info |
| Pause state | Confirmed (toolbar Play and Pause buttons both highlighted) |

**Rate validation arithmetic:**

`(7138666.6 − 7000000) / 416 = 333.333 m/tick`

This matches the predicted figure exactly: 10000 m/s × (1/30 s per FixedUpdate) = 333.33 m/tick. Velocity is unchanged frame to frame, as expected for ballistic coast in a vacuum.

**Floating-origin shift accounting:**

- Shift 1 fired at initialization (TestVessel started 7,000,000 m from world origin, well past the 50 km threshold; origin shifted immediately to (7,000,000, 0, 0))
- Shifts 2 and 3 fired at 50 km vessel-motion intervals, putting origin at (7,050,000, 0, 0) then (7,100,000, 0, 0)
- Current Local (38,000, 0, 0) confirms vessel is 38 km past current origin, tracking toward the next shift threshold

**Camera-follow status:** the vessel sphere primitive was occluded in scenario 1 captures by the screen-space-overlay WarpUI Canvas (Panel + DiagnosticText + buttons covering the central rendered region). Camera-follow behavior was verified structurally rather than visually: Local position stayed bounded at 38 km while World position grew monotonically to 7.1M+ m. This is mathematically inconsistent with a static camera; only camera-follow-through-floating-origin-shifts produces this relationship. Per the amended spec, scenario 1's "vessel primitive remains centered" claim is satisfied structurally even when the sphere is UI-occluded.

**Scenario 1 sign-off: complete.** Re-validation in the future post-fix commit may re-capture scenario 1 to refresh the data point, or may carry this evidence forward; the math and methodology are unchanged.

## Connection to audit pipeline carryover

The producer-bot audit pipeline has been pointing at the Newton-Raphson / `OrbitalElements` neighborhood for 5 audit cycles. The latest audit (run before commit 049) framed the eccentricity helper as not-urgent because the active commit work was in warp-controller territory; if commit 049+ returned to predictor work, the helper would be the first thing to land.

Commit 049 (camera-follow) didn't touch the predictor. Commit 050 (audit report) didn't touch the predictor. Commit 051 (multi-body scene) didn't touch the predictor. Commit 052 (this artifact) didn't touch the predictor.

Each prior cycle, deferring the helper was reasonable: the active work didn't expose the gap the helper would close. This session's validation pass is the first end-to-end exercise that drives the predictor through real per-frame use under sustained warp. It exposed the gap, in production, in a way the helper would have caught earlier (had the helper been in place during the unit tests that should have existed for purely-radial transition).

The audit pipeline's persistence in flagging this area is now corroborated by direct evidence. The fix commit's spec should treat the helper landing as paired with the production fix, not as a separable concern.

## What this commit is NOT validating

The following items are explicitly deferred to a future re-validation commit (provisionally 054) following the fix commit (provisionally 053):

- **Scenarios 2-8 of Phase 1 validation.** PhysX → KeplerRails transition (scenario 2), time-warp at PhysX 5× and Kepler 10000× (scenario 3), floating-origin shifts at 50 km threshold under warp (scenario 4), KeplerRails → PhysX with propagated position (scenario 6), SOI re-rooting on inbound (scenario 7), SOI-crossing halt firing (scenario 8). All cannot proceed without the production fix.
- **Body visualization (Phase 3 scope).** Earth and Moon ReferenceBodies in `TestVessels.unity` are data-only transforms with no MeshFilter/MeshRenderer; validation evidence is structural rather than cinematic by deliberate scope decision. Carried over from the amended commit 052 spec's "What this commit is NOT validating" section.
- **Scenario 5: surface-impact halt firing in Play.** Math is EditMode-covered by commit 047 tests; Play demonstration requires a sub-orbital trajectory not in the current scene. Carried over.
- **Scenario 9: atmospheric-entry halt firing in Play.** Math is EditMode-covered by commit 047 tests; requires a trajectory crossing the 100 km Kármán-line boundary, which no vessel in the current scene has. Carried over.

## Cross-references

- `docs/phase1_validation_readiness.md` — the audit that drove the validation arc and identified commits 049/050/051 plus the planned 052 milestone artifact
- `commits/049_active_vessel_camera_follow.md` — visual-validation infrastructure (active-vessel camera-follow MonoBehaviour)
- Commit `e6a41fc` (no artifact in `commits/`) — audit report commit (050); the audit document itself was the commit's content
- `commits/051_multi_body_test_scene.md` — multi-body scene + multi-vessel toggle infrastructure that completed the validation-arc setup
- `SPACESIM/Assets/Scripts/Foundation/Vessels/KeplerPropagator.cs:147` — `PropagateState` entry point; routes to `MeanToTrueAnomaly`
- `SPACESIM/Assets/Scripts/Foundation/Vessels/OrbitalElements.cs:645` — `MeanToTrueAnomaly` dispatcher; routes `e ≥ 1` to `SolveKeplerHyperbolic`
- `SPACESIM/Assets/Scripts/Foundation/Vessels/OrbitalElements.cs:962` — `SolveKeplerHyperbolic` Newton-Raphson iteration that fails to converge for `e = 1`
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs:715` — `GetWorldPosition` that returns NaN downstream of the failed solve
- `SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs:278` — `BuildDiagnosticText` that surfaces the NaN to the UI
- `SPACESIM/Assets/Scripts/Foundation/SimTick/ActiveVesselCameraFollow.cs` — `LateUpdate` that triggers the `transform.position` rejection on NaN input
- Producer-bot audit pipeline outputs in `docs/automation/` — Newton-Raphson eccentricity helper outstanding 5 cycles (untracked artifacts)

## Closing

This commit closes the Phase 1 validation session at the point of the finding. The Phase 1 closing arc continues with commit 053 (the fix) and commit 054 (the re-validation). Push to remote is deferred until 054 actually closes the validation arc clean; landing this incomplete artifact remotely without the follow-up commits would publish a partially-resolved finding, which would be confusing to anyone reading the remote in isolation.

The fix commit's spec will be drafted fresh, with the architectural decisions (radial closed-form vs guard-and-reject, helper landing pairing, test coverage scope) made with rest rather than under the immediate pressure of the session's bug discovery. Carrying the finding overnight has no cost; the math will be the same tomorrow as today.
