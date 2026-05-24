# Phase 1 validation milestone — engine substrate validated

**Date:** 2026-05-24
**HEAD at validation:** `3b4b040` (053-stage3, KeplerPropagator degenerate-orbit guard + regression test landed)
**Validation arc:** per `docs/phase1_validation_readiness.md` Section C, commit candidate 3
**Methodology:** Phase 1 validation milestone closed via component-level EditMode coverage + integrated Play-mode qualitative pass. The original strict reading of CONSTRAINTS §9 ("placeholder cube reaches orbit, transfers to a moon, captures, lands") is amended to the middle reading at commit 055: "engine substrate validated end-to-end via component-level Play demonstration + integrated multi-body coverage at the EditMode level." The cube-to-moon flight integration scenario (thrust, atmospheric ascent, capture burn, landing) is Phase 3 work by scope, not Phase 1.

## Executive summary

Six scenarios from the Phase 1 validation envelope are validated against this commit:

| # | Scenario | Method | Status |
|---|---|---|---|
| 1 | Ballistic-ish coast (elliptical orbit under gravity) | Qualitative in-Play | ✓ |
| 2 | PhysX → KeplerRails transition | Qualitative in-Play | ✓ |
| 3 | Time-warp at PhysX 5x / Kepler 10000x | EditMode coverage + qualitative | ✓ |
| 4 | Floating-origin shifts at 50km threshold | Qualitative in-Play (inferred via Local-stays-bounded-while-World-grows) | ✓ |
| 6 | KeplerRails → PhysX with propagated position | Qualitative in-Play | ✓ |
| 7 | SOI re-rooting on body crossing | EditMode coverage | ✓ |
| 8 | SOI-crossing halt firing | EditMode coverage | ✓ |
| 5 | Surface-impact halt firing in Play | Deferred (math EditMode-tested; Play demo requires sub-orbital trajectory authoring) | ⏸ |
| 9 | Atmospheric-entry halt firing in Play | Deferred (math EditMode-tested; Play demo requires Kármán-boundary-crossing trajectory) | ⏸ |

**Test suite at validation:** 346 EditMode + 6 PlayMode = **352 green**.

Phase 1 engine substrate is validated. Phase 1 → Phase 2 transition is unblocked once commit 055's PHASE_TRACKER ↔ CONSTRAINTS reconciliation lands.

## Scenarios demonstrated qualitatively in Play (1, 2, 4, 6)

In-Play validation pass on canonical `TestVessel` in `TestVessels.unity`, post commit 053 with the degenerate-orbit guard in place. Initial conditions: Position (7,000,000, 0, 0) m, Initial Velocity (3000, 7000, 0) m/s — elliptical orbit (e ≈ 0.66) safely clear of solver-stability boundaries. Initial Mode = PhysXActive.

**Scenario 1 — Ballistic-ish coast.** TestVessel coasts under PhysX gravity. Diagnostic UI shows World position advancing per tick, Local position bounded by floating-origin shifts, Mode = PhysX-active. Console clean during coast. Vessel motion is elliptical rather than purely ballistic (the β velocity revision was specifically chosen to break the e≈1 boundary that an earlier (10000, 100, 0) attempt landed on). Position rate ~250 m/tick observed; matches order-of-magnitude expectation for the elliptical orbit produced by the (3000, 7000, 0) initial velocity at 7,000 km altitude.

**Scenario 2 — PhysX → KeplerRails transition.** Pressing Space triggers the Space-key handler, which calls `Vessel.TransitionToKeplerRails()`. Transition succeeds; `TestVesselDriver.LogPhase0LimitationOnce()` fires once as expected, producing one INFO entry: `[hh:mm:ss] PHASE 0 LIMITATION (commit 038): vessel is now on Kepler-rails. Position is frozen at the transition tick — no propagator is wired yet...`. This INFO entry is the success indicator. No errors, no warnings, no NaN cascade — the commit 053 guard correctly didn't fire (orbit non-degenerate). The diagnostic UI mode line updates to "Mode: Kepler-rails (epoch tick: N)".

**Scenario 4 — Floating-origin shifts at 50km threshold.** Diagnostic UI shows `Shift count` incrementing during sustained coast (multiple shifts observed across the in-Play session). The Local-bounded-while-World-grows relationship corroborates: vessel's Local position stays small (tens of km) while World position advances monotonically into the millions of meters. Together, the direct `Shift count` reading + the Local-vs-World relationship confirm floating-origin manager is shifting per the 50km threshold and per-vessel anchors are tracking the shifts.

**Scenario 6 — KeplerRails → PhysX with propagated position.** Pressing Space again on a Kepler-rails vessel triggers `Vessel.TransitionToPhysXActive()`. Vessel transitions back to PhysX-active mode; position is propagated from the Kepler epoch tick to the current tick via `KeplerPropagator.PropagateState`, then assigned to the newly-constructed Rigidbody. No errors observed during round-trip transitions.

## Scenarios validated via EditMode coverage (3, 7, 8)

The validation arc's audit (commit 050) framed scenarios 3, 7, 8 as Play-demonstrable. The Phase 1 closing-arc validation session surfaced that these scenarios are already covered at the component level via existing EditMode tests. Per the validation discipline ("math + driver behavior verified at component level is sufficient evidence for milestone closure"), the EditMode coverage is the evidence; integrated Play demonstration is deferred to a future visualization commit (Phase 3 scope) when body visualization makes such demonstration meaningful.

**Scenario 3 — Time-warp at PhysX 5x / Kepler 10000x.** Covered by `SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/WarpControllerTests.cs`:
- `CeilingFor_PhysX_Is5` (line 55) — verifies the PhysX-mode ceiling is 5x
- `CeilingFor_KeplerRails_Is10000` (line 61) — verifies the Kepler-mode ceiling is 10000x
- `CeilingFor_InterstellarCruise_Is100000` (line 67) — verifies the Cruise-mode ceiling for Phase 6 readiness
- `EffectiveRate_PhysX_CappedAt5x` (line 117) — verifies effective rate is clamped to ceiling regardless of requested rate
- `EffectiveRate_Kepler_CappedAt10000` (line 126) — same for Kepler mode

These tests exercise the mode-aware ceiling logic that scenario 3's Play demonstration would observe. Test count: 5 directly relevant, more in the broader 39+ WarpController test suite covering halt/resume/pause/clearhalt/discrete-level/continuous-rate dispatch.

**Scenario 7 — SOI re-rooting on body crossing.** Covered by `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselSoiRerootingDriverTests.cs`:
- `SoiRerootingDriver_VesselWithinSoi_DoesNotReroot` (line 109) — negative case: vessel inside current body's SOI stays
- `SoiRerootingDriver_VesselBeyondSoi_RerootsToParent` (line 154) — outward re-root: vessel exits SOI → re-rooted to parent body
- `SoiRerootingDriver_VesselEntersChildSoi_RerootsToChild` (line 201) — **inward re-root: vessel enters child body's SOI → re-rooted to child** (the scenario 7 case)
- `SoiRerootingDriver_VesselWithNoParent_DoesNotRerootOutward` (line 247) — edge case: top-level body has no parent to re-root to

Test setup uses `VesselTestHelpers.BuildMoonAsChildOfEarth(_body)` to construct an Earth + Moon multi-body world programmatically, exercises `VesselSoiRerootingDriver` per-tick dispatch via `SimTickController.TickAdvanced`, asserts on vessel's `State.KeplerState.ReferenceBodyId` after re-root. The component behavior scenario 7's Play demonstration would observe is verified directly.

**Scenario 8 — SOI-crossing halt firing.** Covered by two test classes:

`SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/SoiCrossingPredictorTests.cs`:
- `PredictNextCrossing_VesselApproachingChildSoi_InwardCrossingPredicted` (line 222) — inward crossing prediction populates the event queue
- `PredictNextCrossing_EllipticalApoapsisBeyondSoi_OutwardCrossingPredicted` (line 165) — outward crossing prediction
- 16 additional tests cover edge cases, multi-body SOI hierarchies, sample-and-refine convergence

`SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/WarpControllerTests.cs`:
- `RegisterHaltEvent_SetsIsHaltingAndStoresLastHaltInfo` (line 423) — verifies halt-event registration sets `IsHalting` and `LastHaltInfo`
- `ClearHalt_ClearsIsHalting_PreservesLastHaltInfo` (line 456) — verifies post-halt resumption path

Together: the predictor populates `EventPriorityQueue` with imminent SOI crossings; `WarpController` reads the queue via `EventQueue.PeekTopTick()` (per `SimTickController.RunFixedUpdateCycle` per netcode contract §4.2) and registers a halt event when an imminent tick approaches. The Play demonstration's "warp rate freezes, halt-info shows SoiCrossingPredicted" is the integration outcome of these tested components. **Caveat:** no single test asserts the full predictor→queue→WarpController→halt integration in one execution; the integration is inferred from the components' tested behavior plus the netcode contract §4.2 specification of the integration path. A future end-to-end PlayMode test could close this inference gap if regression confidence calls for it.

## Scenarios deferred (5, 9)

Both deferred per the amended commit 052 spec — math EditMode-tested at commit 047 (`SurfaceImpactPredictorTests.cs`, `AtmosphericEntryPredictorTests.cs`); Play demonstration requires trajectory authoring not in scope for this commit.

**Scenario 5 — Surface-impact halt firing in Play.** Requires a sub-orbital trajectory. Could be demonstrated via a third test vessel with low initial velocity or a non-committed Inspector tweak during a future Play session. Deferred to a future polish commit.

**Scenario 9 — Atmospheric-entry halt firing in Play.** Requires a trajectory crossing the 100 km Kármán boundary. Earth's `AtmosphericTopAltitudeMeters = 100000` is set per commit 051 scene change; no current vessel has a Karman-crossing trajectory. Deferred to a future polish commit (likely paired with scenario 5's trajectory work).

## Test suite

Captured host-side at session time, post-commit-053-stage3:

| Suite | Count | Result |
|---|---|---|
| EditMode | 346 | All green |
| PlayMode | 6 | All green |
| **Total** | **352** | **All green** |

Counts decompose: 339 EditMode baseline at commit 048-stage5 close + 6 from commit 053-stage1 helper-validation + 0 from stage 2 (PhysicsConstants extraction + production guard + TransitionResult enum + migrations; no test additions per spec) + 1 from stage 3 regression test = 346 EditMode. PlayMode unchanged at 6.

## What this commit is NOT validating

- **Integrated cube-to-moon flight scenario** (the original CONSTRAINTS §9 Phase 1 strict-reading milestone). Requires thrust, atmospheric flight model, control inputs, capture burn, landing — all Phase 3 flight-integration scope. CONSTRAINTS §9 Phase 1 milestone language is amended at commit 055 to the middle reading: engine substrate validated via component-level Play demonstration + integrated EditMode coverage.
- **Body visualization** (Phase 3 scope per the amended commit 052 spec). Earth and Moon ReferenceBodies remain data-only transforms with no mesh components. Phase 3-aligned visualization (proxy meshes + anchor wiring + camera far-clip handling + distance-based LOD) is queued as future polish.
- **Save/load sim-state implementation** (Phase 1 parallel track per commit 055 D3). Format technology decision is locked at commit 055 (lean JSON); implementation can develop alongside Phase 2 Track A work without blocking Phase 2 entry.
- **Scenarios 5 and 9** (deferred per scope; math is EditMode-validated).

## Cross-references

- `docs/phase1_validation_readiness.md` — audit driving this arc (commit 050)
- `docs/phase1_validation_incomplete.md` — failure-mode artifact from the bug discovery (commit 052)
- `commits/049_active_vessel_camera_follow.md` — visual-validation infrastructure
- `commits/051_multi_body_test_scene.md` — multi-body scene + multi-vessel toggle infrastructure
- `SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/WarpControllerTests.cs` — scenario 3 + scenario 8 halt coverage
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselSoiRerootingDriverTests.cs` — scenario 7 coverage
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/SoiCrossingPredictorTests.cs` — scenario 8 prediction coverage
- `docs/CONSTRAINTS.md` §9 — Phase 1 scope and milestone definition (amended at commit 055)
- `docs/PHASE_TRACKER.md` — operational tracker (updated at commit 055)

## Session findings (non-blocking)

**TestVessel_SoiCrossing missing from canonical scene file at validation time.** The in-Editor validation pass surfaced that `TestVessels.unity`'s Hierarchy did not contain `TestVessel_SoiCrossing` despite the commit 051 scene file landing it. Most likely an unsaved-deletion artifact from earlier in the session's scene-edit cycles (multiple velocity edits during the bug-discovery work made it easy to accidentally remove the duplicate vessel). Sandbox copy of `TestVessels.unity` confirms the vessel data is present on disk via grep; the deletion (if any) is uncommitted. Restoration is queued for a future hygiene commit; sequencing TBD. Does NOT block Phase 1 closure because scenario 7 (SOI re-rooting) and scenario 8 (SOI-crossing halt) — the only scenarios that depend on `TestVessel_SoiCrossing` for in-Editor Play demonstration — are validated via component-level EditMode coverage per the methodology in this commit.

## Closing

Phase 1 engine substrate is validated. The Phase 1 closing arc (commits 049, 050, 051, 052, 053-stage1, 053-stage2, 053-stage3, 054) is complete; commit 055 closes the doc reconciliation + push 049-055 to remote. Phase 2 (vessel construction Track A + per-planet procgen Track B per the amended PHASE_TRACKER post-055) is unblocked once 055 lands.
