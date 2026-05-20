# Phase Tracker

**Read this first at session start.** It tells you where the project is, what's currently being worked on, and what's blocking forward progress.

This document is operational state, not narrative history. It updates with every meaningful commit. The detailed per-commit narrative lives in `commits/`.

---

## Current phase

**Phase 0 — Design crystallization and prototype foundation.**

Phase 0 has two halves per `docs/CONSTRAINTS.md` §9:

1. **Design content** — locking the design decisions that gate Phase 1+ implementation. **Status: complete** (commits 001-025).
2. **Prototype foundation** — written netcode contract plus prototype implementation validating it. **Status: complete** (commits 026-043 landed; all 12 of 12 Phase 0 remaining-work items checked; both Phase 1 carryover items addressed — one fully closed at commit 042, one partially closed at commit 043 with infrastructure landed and full activation deferred until upstream state systems exist).

## Current milestone

**Phase 1 underway — both carryover items addressed; first system-list item closed.** Commit 041 closed Phase 0 with the comprehensive §3.1 mode transition procedure test. Commit 042 fixed the `Vessel.Initialize`-in-KeplerRails state inconsistency via an overload-based API. Commit 043 lands the per-sim-tick mode transition trigger evaluator as disabled-by-default infrastructure: `Vessel.EvaluateTransitionTriggers` implements the §3.1 conditions, `VesselTransitionDriver` subscribes to `SimTickController.TickAdvanced` and dispatches transitions, but the driver's `Enabled` flag defaults false. Trigger evaluation activates when upstream state systems (thrust simulation, atmospheric model, contact detection, player focus, scripted thrust, multi-vessel proximity) populate the schema fields the evaluator reads — those systems land progressively across Phase 3+ work. **Commit 044 lands SOI re-rooting** (first item closed from the Phase 1 system-list under "Systems by phase → Phase 1 — Foundations"): `ReferenceBody` schema extended with `SoiRadiusMeters` and parent-body wiring, new `BodyRegistry` for Guid-keyed lookup + parent→children enumeration, `OrbitalElements.ReRootStateVector` math helper, `Vessel.ReRootToBody` intra-mode operation, `VesselSoiRerootingDriver` per-tick check (no Enabled flag — real implementation, always-on), NETCODE_CONTRACT §2.7 BodyState added.

**The prototype implementation validates the netcode contract end-to-end**: coordinate system, floating origin, sim-tick controller, deferred listener registration, vessel containers, mode transitions both directions, Kepler-rails propagation, mode transition procedure under each §3.1 condition, schema-invariant-maintaining Initialize, trigger evaluator infrastructure ready to activate.

**Phase 1 system-list work (per the "Systems by phase → Phase 1 — Foundations" section) can now proceed cleanly.** The two Phase 0 carryover items are resolved; reference frame hierarchy + patched conics landed at commit 044; event prediction priority queue (first item that unblocks time-warp gating) landed at commit 045. The remaining Phase 1 work is additional event predictors (atmospheric entry + surface impact at commits 046-047, SOI crossing at commit 048+), continuous time-warp UI and rate-scaling controls (the queue lookup is wired but the player-facing warp controls aren't), save/load format, vessel container data structure refinements as gameplay systems land.

**Commit 045 lands the event predictor + priority queue infrastructure.** `EventPriorityQueue` lives on `SimTickController` per CONSTRAINTS §2 ("Authority over the queue lives in the sim-tick controller"). `PeriapsisApoapsisPredictor` is the first concrete predictor — pure function, populates `KeplerState.NextPeriapsisTick` / `NextApoapsisTick` for Kepler-rails vessels. `VesselEventPredictionDriver` subscribes to `TickAdvanced` and runs the predictor each tick. `RunFixedUpdateCycle` now reads `EventQueue.PeekTopTick()` to bound time-warp advancement per netcode contract §4.2. The `SimEventType` enum names all seven Phase 1+ event categories even though only Periapsis and Apoapsis are populated in commit 045 — the extensibility hook from CONSTRAINTS §2 is in place. NETCODE_CONTRACT §2.3 amended at commit 045: `next_periapsis_tick` and `next_apoapsis_tick` are now `Option<SimTickCount>` (hyperbolic post-periapsis has no future periapsis; hyperbolic orbits have no apoapsis at all).

## Active blockers

None.

## Recently landed

| Commit | What | Date |
|---|---|---|
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

**Tests:** 268 EditMode + 6 PlayMode = 274 total, all green (commit 045 baseline). Commit 045 adds 41 new EditMode tests across three stages: Stage 1 (12 `OrbitalElements` anomaly-conversion tests for math extracted from `KeplerPropagator` — `TrueToMeanAnomaly`, `MeanToTrueAnomaly`, `MeanMotion`), Stage 2 (12 `EventPriorityQueue` tests + 11 `PeriapsisApoapsisPredictor` tests + 6 `VesselEventPredictionDriver` tests = 29), Stage 3 (1 test renamed: `ComputeFromStateVector_EventPredictionFields_AreNull` → `..._AreNullAtConstruction` for timing clarity; no assertion change). Counts cleanly: 227 (commit 044 baseline) + 41 = 268. PlayMode test count unchanged at 6.

**End-to-end Play verification:** TestCoordinates.unity exercises the foundational spine (coordinate system + floating origin + sim-tick controller + deferred listener registration + rigidbody shift) — verified working at commit 034. TestVessels.unity exercises the full vessel-container stack (Vessel + ReferenceBody + TestVesselDriver + mode transitions) — floating-origin shifts verified working in commit 038 end-to-end Play; Space-key mode transitions verified working after commit 039's Input System migration. Commit 040's propagator integration is covered by EditMode tests (15 propagator + 2 integration); end-to-end Play verification of long Kepler-rails sits with visible orbital motion in TestVessels.unity is deferred to a future verification commit. Commit 041's 18 transition-procedure tests are EditMode-only by construction. Commit 042's 6 Initialize-overload tests are EditMode-only. Commit 043's 16 trigger-evaluator tests are EditMode-only. Commit 044's 33 SOI re-rooting tests are EditMode-only; the `VesselSoiRerootingDriver` runs in Play (no Enabled flag) but TestVessels.unity is single-body so no actual re-rooting fires. End-to-end Play verification of multi-body re-rooting (constructing an Earth-Moon scene in TestVessels.unity or a new TestSoi.unity scene) is deferred to a future verification commit. Commit 045's 41 event-predictor tests are EditMode-only; the `VesselEventPredictionDriver` populates `KeplerState.NextPeriapsisTick` / `NextApoapsisTick` in Play and updates the `SimTickController.EventQueue` each tick. `RunFixedUpdateCycle` now reads `EventQueue.PeekTopTick()` to bound time-warp advancement (per netcode contract §4.2). End-to-end Play behavior change at commit 045: warp will slow as a Kepler-rails vessel approaches periapsis or apoapsis events, landing on the event tick exactly before continuing.

**Git state:** Three commits on `main`, pushed to https://github.com/gmkarleski/SPACESIM.

## Phase progression

- **Phase 0** (Design crystallization + prototype) — substantively complete (12 of 12 remaining-work items checked at commit 041)
- **Phase 1** (Foundations: coordinates, time, modes) — underway; 1 carryover item remaining (Initialize-in-KeplerRails fix closed at commit 042; trigger evaluator still open)
- **Phase 2** (Vessel construction) — not started
- **Phase 3** (Flight integration) — not started
- **Phase 4** (Procgen: bodies, parts variation) — not started
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

Coordinate system (complete, commit 029). Floating origin (complete, commit 029). Sim-tick controller (complete, commit 033). Reference frame hierarchy (complete, commit 044 — `ReferenceBody` hierarchy with parent body wiring + `BodyRegistry` for Guid-keyed lookup). Patched conics (complete, commit 044 — SOI re-rooting via `OrbitalElements.ReRootStateVector` + `VesselSoiRerootingDriver` per-tick detection). Event prediction priority queue (complete, commit 045 — `EventPriorityQueue` owned by `SimTickController`, `PeriapsisApoapsisPredictor` populates orbit-shape events, `VesselEventPredictionDriver` runs per-tick, `RunFixedUpdateCycle` reads queue to bound warp advancement). Three-mode physics architecture (commits 038-043). Mode transitions (commits 038-043). Time architecture and time-warp (partial — queue lookup wired at commit 045; full continuous-warp UI + warp-rate scaling controls land later). Vessel container data structure (commits 038, 042). Save/load format. Additional predictors (atmospheric entry + surface impact at commits 046-047, SOI crossing at commit 048 or later, scheduled-burn and interstellar-arrival at later phases per CONSTRAINTS §2 extensibility hook).

### Phase 2 — Vessel construction

Parts library with procgen variation per part category. Vessel assembly. Symmetric editing. Sub-assemblies. Multi-stage configuration. Fuel routing. Per-part configuration. Center-of-mass / thrust / lift display. Crew slot positioning. Action group bindings.

### Phase 3 — Flight integration

Vessel spawn at launch sites. Atmospheric flight model (Juno-fidelity per commit 025). Engine thrust. Control inputs and SAS. Camera systems. PhysX-active simulation of vessels.

### Phase 4 — Procgen

Procgen bodies (planets, moons, asteroids). Per-body parameter set. 14-stage generation pipeline. Six-layer hierarchy. Catalog state. Procgen variation per part category (visual polish on parts library).

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
