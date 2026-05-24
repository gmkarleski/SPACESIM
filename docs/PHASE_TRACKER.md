# Phase Tracker

**Read this first at session start.** It tells you where the project is, what's currently being worked on, and what's blocking forward progress.

This document is operational state, not narrative history. It updates with every meaningful commit. The detailed per-commit narrative lives in `commits/`.

---

## Current phase

**Phase 1 — Foundations: coordinates, time, modes.** Engine substrate validated at commit 054; doc reconciliation + push lands at commit 055.

Phase 0 (Design crystallization + prototype foundation) closed at commit 041 — all 12 of 12 Phase 0 remaining-work items checked, both Phase 1 carryover items addressed (Initialize-in-KeplerRails fix closed at 042; trigger evaluator infrastructure landed at 043 with full activation deferred until upstream Phase 3+ state systems exist).

Phase 1 systems landed: coordinate system (029), floating origin (029), sim-tick controller (033), three-mode physics + mode transitions (038-043), reference frame hierarchy + patched conics + SOI re-rooting (044), event prediction priority queue + periapsis/apoapsis predictor (045), SOI crossing predictor (046), atmospheric entry + surface impact predictors (047), time-warp controller (048), validation infrastructure (049-051), bug fix + regression coverage (052-053), validation milestone (054), doc reconciliation (055). Save/load sim-state implementation is parallel track per commit 055 D3 — can develop alongside Phase 2 Track A work without blocking Phase 2 entry.

Phase 2 (Vessel construction Track A + per-planet procgen Track B per CONSTRAINTS §9) is unblocked once commit 055 pushes.

## Current milestone

**Phase 1 closing arc landed.** The 049-055 closing block validated Phase 1's engine substrate end-to-end via component-level Play demonstration + integrated EditMode coverage:

- **049** — Active-vessel camera-follow utility (visual-validation infrastructure for the Play session)
- **050** — Phase 1 validation readiness audit committed (`docs/phase1_validation_readiness.md`)
- **051** — Multi-body TestVessels.unity with Moon + SOI-crossing test vessel; three-toggle + `_initialMode` multi-vessel infrastructure on `TestVesselDriver`
- **052** — Phase 1 validation incomplete artifact (`docs/phase1_validation_incomplete.md`) — bug discovered during scenario 2 validation: purely-radial velocity → e=1 → `SolveKeplerHyperbolic` divide-by-zero → NaN cascade
- **053** (three stages) — Eccentricity helper landing (closes 5-cycle audit-pipeline carryover); `PhysicsConstants` extraction + `Vessel.TransitionToKeplerRails` degenerate-orbit guard + `TransitionResult` enum; regression test
- **054** — Phase 1 validation milestone (`docs/phase1_validation_results.md`) — 6 scenarios validated (4 qualitative-in-Play, 2 via EditMode coverage), 2 deferred (5, 9), 1 amended-to-Phase-3 (integrated cube-to-moon flight per CONSTRAINTS §9 amendment at commit 055 D2)
- **055** — Doc reconciliation (PHASE_TRACKER ↔ CONSTRAINTS on Phase 2 scope per D1) + CONSTRAINTS §9 Phase 1 milestone amendment (D2) + save format technology locked at JSON for early development (D3) + push 049-055 closing block to remote

**Phase 1 engine substrate fully validated.** The integrated cube-to-moon flight scenario that the original CONSTRAINTS §9 Phase 1 milestone language anticipated is Phase 3 work by design (requires thrust + atmospheric flight + control inputs — all flight-integration scope). Save/load sim-state implementation is parallel-track per commit 055 D3 — develops alongside Phase 2 Track A work without blocking Phase 2 entry. Trigger evaluator full activation remains deferred per commit 043's `Enabled = false` design until upstream Phase 3+ state systems exist.

**Phase 2 entry is unblocked.** Two parallel tracks per CONSTRAINTS §9: Track A vessel construction (procedural part system, VAB assembly UI, save/load vessel designs, symmetry/attachment/fuel/mass-properties) + Track B per-planet procgen (14-stage Layer 5 pipeline, feature layer architecture, hand-tuned home system bodies, seed function backward-compatibility versioning). Phase 2 entry decision (Track A first, Track B first, or mixed) is a fresh-session question.

## Active blockers

None.

## Recently landed

| Commit | What | Date |
|---|---|---|
| 055 | Phase 1 closure — doc reconciliation (PHASE_TRACKER ↔ CONSTRAINTS) + CONSTRAINTS §9 Phase 1 milestone amendment + save format technology lock + push 049-055 to remote | 2026-05-24 |
| 054 | Phase 1 validation milestone — engine substrate validated (`docs/phase1_validation_results.md`); 6 scenarios validated (4 qualitative-in-Play + 2 via EditMode coverage), 2 deferred (5, 9), 1 amended-to-Phase-3 (integrated cube-to-moon flight) | 2026-05-24 |
| 053-stage3 | Regression test for KeplerPropagator degenerate-orbit handling (`TransitionToKeplerRails_WhenPurelyRadialVelocity_LogsErrorAndReturnsFailedDegenerateOrbit`) closing the test-coverage gap surfaced at commit 052 | 2026-05-24 |
| 053-stage2 | PhysicsConstants extraction (Foundation/Coordinates) + Vessel.TransitionToKeplerRails degenerate-orbit guard + TransitionResult enum (Success / FailedDegenerateOrbit / FailedOther); 12 inline EarthMassKg/5.972e24 sites migrated; 3 production call sites updated for return-value checking | 2026-05-23 |
| 053-stage1 | Eccentricity helper landing (`AssertSolvableEccentricity` + `AssertNonDegenerateOrbit` in `VesselTestHelpers`); closes the 5-cycle audit-pipeline carryover on the Newton-Raphson eccentricity helper; 6 helper-validation tests added | 2026-05-23 |
| 052 | Phase 1 validation incomplete — `docs/phase1_validation_incomplete.md` failure-mode artifact documenting purely-radial-velocity → e=1 → SolveKeplerHyperbolic divide-by-zero NaN cascade bug found during scenario 2 validation pass | 2026-05-23 |
| 051 | Multi-body TestVessels.unity with Moon + SOI-crossing test vessel; three-toggle + `_initialMode` multi-vessel infrastructure on TestVesselDriver | 2026-05-23 |
| 050 | Phase 1 validation readiness audit report committed (`docs/phase1_validation_readiness.md`) | 2026-05-23 |
| 049 | Active-vessel camera-follow utility for Play-mode visual validation (`ActiveVesselCameraFollow` MonoBehaviour in SimTick asmdef) | 2026-05-23 |
| 048 | Time-warp controller (5 stages: rational rate representation, RunFixedUpdateCycle integration, routine-supply gating, WarpUIController + TestVessels Canvas, gap-fill tests + comprehensive artifact); pre-existing SimTickWarpController replaced; routine-supply gating wires atmospheric entry + surface impact halt unconditionally and SOI crossing halt only for non-routine vessels | 2026-05-22 |
| 047 | Atmospheric entry + surface impact predictors (closed-form via shared `OrbitalElements.SolveConicAtRadius`; populate `next_mode_transition_tick` via min-aggregation) | 2026-05-20 |
| 046 | SOI crossing predictor (outward closed-form + inward sampled-and-refined; populates `next_soi_transition_tick`) | 2026-05-19 |
| 045 | Event predictor + priority queue infrastructure (periapsis/apoapsis predictors, warp respects queue) | 2026-05-19 |
| 044 | SOI re-rooting + ReferenceBody hierarchy + NETCODE_CONTRACT §2.7 BodyState | 2026-05-18 |
| 043 | Per-sim-tick mode transition trigger evaluator — disabled-by-default infrastructure | 2026-05-18 |
| 042 | Fix Initialize-in-KeplerRails state inconsistency — overload-based API | 2026-05-18 |
| 041 | Comprehensive mode transition test (Phase 0 close) | 2026-05-18 |
| 040 | Kepler-rails propagator + Phase 0 rewind limitation removed | 2026-05-18 |
| 039 | Input System migration + operational doc updates | 2026-05-17 |
| 038 | Vessel containers + mode transition + TestVessels scene | 2026-05-17 |
| 037 | Phase 0 artifact list for v1 | 2026-05-17 |
| 036 | Operational scaffolding (PHASE_TRACKER, DECISIONS, ARCHITECTURE, SESSION_PROTOCOL, companion-doc template) | 2026-05-17 |
| 035 | Workflow rule 6 formalization (sandbox-mount staleness) | 2026-05-17 |
| 034 | Deferred listener registration architectural fix | 2026-05-17 |
| 033 | Sim-tick controller with 10-step cycle spine | 2026-05-17 |
| 032 | PlayMode lifecycle tests | 2026-05-17 |
| 031 | Test asmdef Editor platform fix | 2026-05-17 |
| 030 | Asmdef Unity.Mathematics reference fix | 2026-05-17 |
| 029 | Coordinate system implementation | 2026-05-17 |
| 028 | Prototype verification pass | 2026-05-17 |
| 027 | Prototype initialization (Unity project) | 2026-05-17 |
| 026 | Netcode contract landed | 2026-05-17 |

## Verification state

**Tests:** 346 EditMode + 6 PlayMode = 352 total, all green (at commit 054 close, no test changes in 055). Counts decompose: 310 EditMode at commit 047 close → 339 EditMode at commit 048-stage5 close (commit 048 staged work added warp-controller test coverage + gap-fill suite; per the 053-stage1 commit message the 048-stage5 close baseline is 339) → 345 at commit 053-stage1 close (+6 helper-validation tests in `VesselTestHelpersTests.cs`) → 345 at commit 053-stage2 close (production fix + PhysicsConstants migrations, no test additions per spec) → 346 at commit 053-stage3 close (+1 purely-radial Kepler-rails transition regression test in `VesselTests.cs`). PlayMode unchanged at 6 throughout the 048-055 closing arc.

**End-to-end Play verification:** TestCoordinates.unity exercises the foundational spine (coordinate system + floating origin + sim-tick controller + deferred listener registration + rigidbody shift) — verified working at commit 034. TestVessels.unity exercises the full vessel-container stack (Vessel + ReferenceBody + TestVesselDriver + mode transitions) — floating-origin shifts verified working in commit 038 end-to-end Play; Space-key mode transitions verified working after commit 039's Input System migration. Commit 040's propagator integration is covered by EditMode tests (15 propagator + 2 integration); end-to-end Play verification of long Kepler-rails sits with visible orbital motion in TestVessels.unity is deferred to a future verification commit. Commit 041's 18 transition-procedure tests are EditMode-only by construction. Commit 042's 6 Initialize-overload tests are EditMode-only. Commit 043's 16 trigger-evaluator tests are EditMode-only. Commit 044's 33 SOI re-rooting tests are EditMode-only; the `VesselSoiRerootingDriver` runs in Play (no Enabled flag) but TestVessels.unity is single-body so no actual re-rooting fires. End-to-end Play verification of multi-body re-rooting (constructing an Earth-Moon scene in TestVessels.unity or a new TestSoi.unity scene) is deferred to a future verification commit. Commit 045's 41 event-predictor tests are EditMode-only; the `VesselEventPredictionDriver` populates `KeplerState.NextPeriapsisTick` / `NextApoapsisTick` in Play and updates the `SimTickController.EventQueue` each tick. `RunFixedUpdateCycle` now reads `EventQueue.PeekTopTick()` to bound time-warp advancement (per netcode contract §4.2). End-to-end Play behavior change at commit 045: warp will slow as a Kepler-rails vessel approaches periapsis or apoapsis events, landing on the event tick exactly before continuing. Commit 046's 18 SOI-crossing-predictor tests are EditMode-only; the `VesselEventPredictionDriver` now additionally populates `KeplerState.NextSoiTransitionTick` and enqueues `SimEventType.SoiCrossing` entries each tick. TestVessels.unity is single-body with infinite-SOI Earth so `SoiCrossingPredictor` returns null and no SoiCrossing entries land in the queue — the Play-behavior change is observable only in a multi-body scene with finite SOIs. End-to-end Play verification of warp-respects-SOI-crossing in such a scene is deferred to a future verification commit. Commit 047's 24 atmospheric/surface predictor tests are EditMode-only; the `VesselEventPredictionDriver` now additionally populates `KeplerState.NextModeTransitionTick` (min-aggregated from atmospheric entry + surface impact) and enqueues `SimEventType.AtmosphericEntry` / `SimEventType.SurfaceImpact` entries each tick. TestVessels.unity's `_body` defaults (vacuum atmospheric-top = 0 and Inspector-default surface radius) make the AtmosphericEntryPredictor return null and the SurfaceImpactPredictor return non-null only for orbits intersecting Earth's surface; the Play-behavior change for atmospheric entry is observable only with an Inspector-set finite atmospheric-top. End-to-end Play verification of warp-drops-on-atmospheric-entry per CONSTRAINTS §2 ("auto-drop warp on atmospheric entry by default") is deferred to a future verification commit alongside commit 048's time-warp UI.

**Git state:** Three commits on `main`, pushed to https://github.com/gmkarleski/SPACESIM.

## Phase progression

- **Phase 0** (Design crystallization + prototype) — substantively complete (12 of 12 remaining-work items checked at commit 041)
- **Phase 1** (Foundations: coordinates, time, modes) — engine substrate validated at commit 054; save/load sim-state implementation is parallel track per commit 055 D3; trigger evaluator full activation deferred until Phase 3+ upstream systems exist
- **Phase 2** (Vessel construction Track A + per-planet procgen Track B per CONSTRAINTS §9) — not started; two parallel tracks per CONSTRAINTS §9
- **Phase 3** (Flight integration) — not started
- **Phase 4** (Visuals: PBR, atmospheric scattering, terrain rendering, engine effects, re-entry plasma + procgen variation per part category) — not started
- **Phase 5** (Game systems: missions, supply, crew, builder, atmospheric flight) — not started; weight increased to 4 per commit 025
- **Phase 6** (Interstellar) — not started
- **Phase 7** (Procgen extensions: nebulae, extreme weather) — not started
- **Phase 8** (Polish, accessibility, post-launch) — not started

## Phase 0 remaining work

Items that need to land before Phase 1 implementation can honestly begin:

- [x] Constraints doc settled through commit 025
- [x] Netcode contract written (commit 026)
- [x] Prototype scaffolding verified (commits 027-028)
- [x] Coordinate system implemented and verified (commits 029-032)
- [x] Sim-tick controller implemented and verified (commit 033)
- [x] Deferred listener registration (commit 034)
- [x] Workflow rule 6 formalized (commit 035)
- [x] Operational scaffolding (commit 036)
- [x] Phase 0 artifact list (Tier A/B/C content decisions for v1, commit 037)
- [x] Vessel containers per netcode contract §2 (commit 038, with Input System migration in commit 039)
- [x] At least one Kepler-rails mode test (commit 040: 15 `KeplerPropagatorTests` covering elliptic / hyperbolic / retrograde / high-eccentricity / round-trip / numerical stability, plus 2 `VesselTests` integration tests verifying GetWorldPosition advances with sim time and TransitionToPhysXActive uses propagated position)
- [x] Mode transition test (PhysX-active ↔ Kepler-rails per netcode contract §3.1) — procedure tested in commit 041; trigger evaluation deferred to Phase 1 (see Phase 1 remaining work below). 18 new `VesselTests` covering each §3.1 condition that can be constructed in Phase 0, plus error paths and stability.

**Phase 0 status: 12 of 12 items checked. Phase 0 substantively closed.** Phase 1 implementation can now honestly begin.

---

## Phase 1 remaining work

Items that need to land during Phase 1, in addition to the design content already mapped under "Systems by phase → Phase 1 — Foundations":

- [~] **Implement per-sim-tick mode transition trigger evaluation per netcode contract §3.1.** **Partially closed at commit 043.** Infrastructure landed: (a) `Vessel.EvaluateTransitionTriggers(IActiveVessel)` returns a `TransitionEvaluation` struct describing the suggested transition and the firing §3.1 condition; (b) `VesselTransitionDriver` static class subscribes to `SimTickController.TickAdvanced` and dispatches transitions on a snapshot of `VesselRegistry.Vessels`; (c) 16 EditMode tests cover the evaluator's per-condition behavior and the driver's evaluate/dispatch loop including exception isolation. **Full activation deferred** by the disabled-by-default `VesselTransitionDriver.Enabled` flag: Phase 0 / early Phase 1 has stub-state values for thrust (`PhysXState.ActiveThrustN`), atmospheric density, contact forces, player focus, scripted thrust, and multi-vessel proximity — flipping the driver on with stub state would cause unintended automatic transitions. The flag flips to true progressively as upstream Phase 3+ state systems wire real values into the schema fields the evaluator reads. See DECISIONS.md "Per-sim-tick mode transition trigger evaluator (commit 043)" for the multi-part design rationale.

- [x] ~~Fix `Vessel.Initialize`-in-KeplerRails state inconsistency.~~ **Closed at commit 042** via overload-based API. Option (a) from the two carryover options was chosen: a new 4-arg `Initialize` overload accepts caller-provided `KeplerState`; the 3-arg overload now rejects `PhysicsMode.KeplerRails` (parallel to the existing `InterstellarCruise` rejection) and falls back to `PhysXActive`. The schema invariant (Mode == X ⟹ XState != null) is enforced at Initialize return for all callers. See DECISIONS.md "Vessel.Initialize signature for state-mode consistency (commit 042)" for the design rationale.

---

## Systems by phase

This section maps which game systems land in which phase. The list is derived from CONSTRAINTS.md §9 build order plus accumulated design content. Use it to answer "when does X get built?"

### Phase 1 — Foundations

Coordinate system (complete, commit 029). Floating origin (complete, commit 029). Sim-tick controller (complete, commit 033). Reference frame hierarchy (complete, commit 044 — `ReferenceBody` hierarchy with parent body wiring + `BodyRegistry` for Guid-keyed lookup). Patched conics (complete, commit 044 — SOI re-rooting via `OrbitalElements.ReRootStateVector` + `VesselSoiRerootingDriver` per-tick detection). Event prediction priority queue (complete, commit 045 — `EventPriorityQueue` owned by `SimTickController`, `PeriapsisApoapsisPredictor` populates orbit-shape events, `VesselEventPredictionDriver` runs per-tick, `RunFixedUpdateCycle` reads queue to bound warp advancement). SOI crossing predictor (complete, commit 046 — `SoiCrossingPredictor` two-path math, integrated into `VesselEventPredictionDriver` with per-predictor try/catch isolation; populates `NextSoiTransitionTick` and enqueues `SimEventType.SoiCrossing` events). Atmospheric entry predictor + surface impact predictor (complete, commit 047 — `AtmosphericEntryPredictor` and `SurfaceImpactPredictor` as thin wrappers around shared `OrbitalElements.SolveConicAtRadius`; `ReferenceBody` schema extended with `SurfaceRadiusMeters` + `AtmosphericTopAltitudeMeters`; populate `NextModeTransitionTick` via min-aggregation). Three-mode physics architecture (commits 038-043). Mode transitions (commits 038-043). Time architecture and time-warp (partial — queue lookup wired at commit 045; full continuous-warp UI + warp-rate scaling controls land at commit 048). Vessel container data structure (commits 038, 042). Save/load format technology locked at commit 055 (JSON for early development per CONSTRAINTS §10 → DECISIONS commit 055); sim-state save/load implementation is parallel-track per commit 055 D3 — develops alongside Phase 2 Track A work without blocking Phase 2 entry. Engine substrate validated end-to-end at commit 054 (`docs/phase1_validation_results.md`). Scheduled-burn and interstellar-arrival predictors at later phases per CONSTRAINTS §2 extensibility hook.

### Phase 2 — Vessel construction Track A + per-planet procgen Track B

Per CONSTRAINTS §9: two parallel tracks with no hard dependencies on each other. Both must close to exit Phase 2 (multi-deliverable exit per CONSTRAINTS §9 phase exit criteria); they meet in Phase 3 when flight gameplay requires real vessels against real planets.

**Track A — Vessel construction.** Procedural part system. VAB assembly UI. Save/load vessel designs (built on top of the Phase 1 sim-state save/load format locked at commit 055). Symmetry. Attachment nodes. Fuel flow graph. Mass properties (center-of-mass, thrust, lift display). Sub-assemblies. Multi-stage configuration. Per-part configuration. Crew slot positioning. Action group bindings. (Parts library procgen variation per part category lands in Phase 4 per CONSTRAINTS §9; Phase 2 ships the library-parts set sufficient for assembly + Phase 3 flight testing.)

**Track B — Per-planet procgen.** Layer 5 (the 14-stage pipeline) implemented, producing the full physics parameter set for any body given a seed including Stage 14 detection signatures. Feature layer architecture with 2-3 cheap-tier feature layers implemented (rings, basic auroras, basic volcanism) as proof of architecture. Hand-tuned home system bodies with all 14 stages applied including Stage 13 hand-placed features. Seed function backward-compatibility versioning in place from day one. Per-planet generation produces both the physics parameter set (detection signatures, anomaly compatibility, resource distribution, physics-driven gameplay) AND visible expressions (terrain, atmosphere rendering, ocean, biomes). Layers 1-2 (galactic structure and star sampling) and Layer 3 (stellar parameters) are Phase 7 work.

### Phase 3 — Flight integration

Vessel spawn at launch sites. Atmospheric flight model (Juno-fidelity per commit 025). Engine thrust. Control inputs and SAS. Camera systems. PhysX-active simulation of vessels.

### Phase 4 — Visuals

Per CONSTRAINTS §9: PBR materials, atmospheric scattering (Hillaire), volumetric clouds (asset-store), terrain rendering with biomes, engine effects, re-entry plasma. Procgen variation per part category — each part type gains visual variants with consistent functional properties, giving vessel designs visual identity without changing the parts vocabulary (see CONSTRAINTS §3 "Parts and vessel construction" for the phase progression commitment).

Per-planet procgen pipeline (the 14-stage Layer 5 work) is Phase 2 Track B per CONSTRAINTS §9, NOT Phase 4. PHASE_TRACKER was reconciled to CONSTRAINTS at commit 055 (D1).

### Phase 5 — Game systems

Missions and campaigns. Supply lines. Crew management. Life support. Research projects. Mission Control UI. Sandbox simulator. Procedural fuselage builder (per commit 025). Procedural wing builder (per commit 025). Atmospheric vehicle gameplay (spaceplanes, supersonic, hypersonic, re-entry heating). Phase 5 weight is 4.

### Phase 6 — Interstellar

Interstellar-cruise mode. Tiered tech progression for interstellar travel. Laser sail, fusion drive, antimatter drive content. Generation ship encounter. Long-duration mission planning. Relativistic time-dilation in cruise.

### Phase 7 — Procgen extensions

Volumetric nebulae. Extreme weather feature layer. Asteroid clusters as system composition variant. Late-game procgen content.

### Phase 8 — Polish + accessibility + post-launch

Accessibility passes. Tutorial content. v1.1 features deferred per commit 025: painting/texturing, ambitious mesh composition features. Multiplayer (2-4 players) per commit 026 netcode contract.

---

## How this document maintains itself

- After every meaningful commit, the "Recently landed" table updates and the "Phase 0 remaining work" checkboxes update.
- "Current milestone" updates when a milestone changes (not every commit).
- "Active blockers" updates when blockers are discovered or resolved.
- "Phase progression" updates only when a phase changes status.
- "Systems by phase" updates when systems are added/removed/moved between phases via design decisions in CONSTRAINTS.md.

Updates land as part of the same Cowork commit that produced the underlying change, when applicable. Cowork-side maintenance discipline: every commit prompt should ask "does this require a PHASE_TRACKER update?"
