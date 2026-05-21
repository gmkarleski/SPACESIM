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

**Phase 1 underway — both carryover items addressed; SOI re-rooting + five predictors landed.** Commit 041 closed Phase 0 with the comprehensive §3.1 mode transition procedure test. Commit 042 fixed the `Vessel.Initialize`-in-KeplerRails state inconsistency via an overload-based API. Commit 043 lands the per-sim-tick mode transition trigger evaluator as disabled-by-default infrastructure: `Vessel.EvaluateTransitionTriggers` implements the §3.1 conditions, `VesselTransitionDriver` subscribes to `SimTickController.TickAdvanced` and dispatches transitions, but the driver's `Enabled` flag defaults false. Trigger evaluation activates when upstream state systems (thrust simulation, atmospheric model, contact detection, player focus, scripted thrust, multi-vessel proximity) populate the schema fields the evaluator reads — those systems land progressively across Phase 3+ work. **Commit 044 lands SOI re-rooting** (first item closed from the Phase 1 system-list under "Systems by phase → Phase 1 — Foundations"): `ReferenceBody` schema extended with `SoiRadiusMeters` and parent-body wiring, new `BodyRegistry` for Guid-keyed lookup + parent→children enumeration, `OrbitalElements.ReRootStateVector` math helper, `Vessel.ReRootToBody` intra-mode operation, `VesselSoiRerootingDriver` per-tick check (no Enabled flag — real implementation, always-on), NETCODE_CONTRACT §2.7 BodyState added. **Commit 046 lands the SOI crossing predictor** (second event-prediction item from the system-list, after commit 045's periapsis/apoapsis infrastructure): `SoiCrossingPredictor` static class with outward closed-form (conic equation) + inward sampled-and-refined (coarse-sample + bisection) math paths, integrated into `VesselEventPredictionDriver` with per-predictor try/catch isolation, populates `KeplerState.NextSoiTransitionTick` and enqueues `SimEventType.SoiCrossing` events. NETCODE_CONTRACT §2.3 amended: `next_soi_transition_tick` semantics clarified as bidirectional (outward exit or inward child entry). **Commit 047 lands the atmospheric entry and surface impact predictors** (third and fourth predictor system-list items): `ReferenceBody` schema extended with `SurfaceRadiusMeters` and `AtmosphericTopAltitudeMeters` (commit-047 Stage 1); shared math helper `OrbitalElements.SolveConicAtRadius` extracted from `SoiCrossingPredictor`'s outward closed-form path so all three radius-crossing predictors (SOI outward, atmospheric entry, surface impact) share one conic-equation solve; `AtmosphericEntryPredictor` and `SurfaceImpactPredictor` static classes are thin wrappers around the shared helper with body-specific thresholds; integrated into `VesselEventPredictionDriver` with per-predictor try/catch isolation; populate `KeplerState.NextModeTransitionTick` via min-aggregation of the two ticks and enqueue `SimEventType.AtmosphericEntry` / `SimEventType.SurfaceImpact` events. NETCODE_CONTRACT §2.3 amended: `next_mode_transition_tick` semantics clarified as min-aggregated across atmospheric entry + surface impact (extensible to N-way for future predictors); §2.7 BodyState extended with surface/atmosphere fields.

**The prototype implementation validates the netcode contract end-to-end**: coordinate system, floating origin, sim-tick controller, deferred listener registration, vessel containers, mode transitions both directions, Kepler-rails propagation, mode transition procedure under each §3.1 condition, schema-invariant-maintaining Initialize, trigger evaluator infrastructure ready to activate.

**Phase 1 system-list work (per the "Systems by phase → Phase 1 — Foundations" section) is now most of the way through.** The two Phase 0 carryover items are resolved; reference frame hierarchy + patched conics landed at commit 044; event prediction priority queue (the item that unblocks time-warp gating) landed at commit 045; SOI crossing predictor landed at commit 046; atmospheric entry + surface impact predictors landed at commit 047. Three predictors now populate the event queue. **The remaining Phase 1 engine work narrows to time-warp rate machinery (commit 048)** — continuous time-warp UI and warp-rate scaling controls (the queue lookup is wired but the player-facing warp controls aren't, and the warp-respects-event-tick gating per netcode contract §4.2 needs end-to-end Play verification). Save/load format is the parallel track. Scheduled-burn and interstellar-arrival predictors are later-phase work per CONSTRAINTS §2 extensibility hook (the enum values are scaffolded).

**Commit 045 lands the event predictor + priority queue infrastructure.** `EventPriorityQueue` lives on `SimTickController` per CONSTRAINTS §2 ("Authority over the queue lives in the sim-tick controller"). `PeriapsisApoapsisPredictor` is the first concrete predictor — pure function, populates `KeplerState.NextPeriapsisTick` / `NextApoapsisTick` for Kepler-rails vessels. `VesselEventPredictionDriver` subscribes to `TickAdvanced` and runs the predictor each tick. `RunFixedUpdateCycle` now reads `EventQueue.PeekTopTick()` to bound time-warp advancement per netcode contract §4.2. The `SimEventType` enum names all seven Phase 1+ event categories even though only Periapsis and Apoapsis are populated in commit 045 — the extensibility hook from CONSTRAINTS §2 is in place. NETCODE_CONTRACT §2.3 amended at commit 045: `next_periapsis_tick` and `next_apoapsis_tick` are now `Option<SimTickCount>` (hyperbolic post-periapsis has no future periapsis; hyperbolic orbits have no apoapsis at all).

## Active blockers

None.

## Recently landed

| Commit | What | Date |
|---|---|---|
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

**Tests:** 310 EditMode + 6 PlayMode = 316 total, all green (commit 047 baseline). Commit 047 adds 24 new EditMode tests across two test-bearing stages: Stage 1 (8 `OrbitalElements.SolveConicAtRadius` tests covering elliptical and hyperbolic radius crossings, fully-inside / fully-outside / boundary-hug / overflow / near-parabolic edge cases), Stage 2 (6 `AtmosphericEntryPredictor` tests + 5 `SurfaceImpactPredictor` tests + 5 `VesselEventPredictionDriver` integration tests for the new predictors and the `NextModeTransitionTick` min-aggregation). Counts cleanly: 286 (commit 046 baseline) + 8 + 6 + 5 + 5 = 310. PlayMode test count unchanged at 6.

**End-to-end Play verification:** TestCoordinates.unity exercises the foundational spine (coordinate system + floating origin + sim-tick controller + deferred listener registration + rigidbody shift) — verified working at commit 034. TestVessels.unity exercises the full vessel-container stack (Vessel + ReferenceBody + TestVesselDriver + mode transitions) — floating-origin shifts verified working in commit 038 end-to-end Play; Space-key mode transitions verified working after commit 039's Input System migration. Commit 040's propagator integration is covered by EditMode tests (15 propagator + 2 integration); end-to-end Play verification of long Kepler-rails sits with visible orbital motion in TestVessels.unity is deferred to a future verification commit. Commit 041's 18 transition-procedure tests are EditMode-only by construction. Commit 042's 6 Initialize-overload tests are EditMode-only. Commit 043's 16 trigger-evaluator tests are EditMode-only. Commit 044's 33 SOI re-rooting tests are EditMode-only; the `VesselSoiRerootingDriver` runs in Play (no Enabled flag) but TestVessels.unity is single-body so no actual re-rooting fires. End-to-end Play verification of multi-body re-rooting (constructing an Earth-Moon scene in TestVessels.unity or a new TestSoi.unity scene) is deferred to a future verification commit. Commit 045's 41 event-predictor tests are EditMode-only; the `VesselEventPredictionDriver` populates `KeplerState.NextPeriapsisTick` / `NextApoapsisTick` in Play and updates the `SimTickController.EventQueue` each tick. `RunFixedUpdateCycle` now reads `EventQueue.PeekTopTick()` to bound time-warp advancement (per netcode contract §4.2). End-to-end Play behavior change at commit 045: warp will slow as a Kepler-rails vessel approaches periapsis or apoapsis events, landing on the event tick exactly before continuing. Commit 046's 18 SOI-crossing-predictor tests are EditMode-only; the `VesselEventPredictionDriver` now additionally populates `KeplerState.NextSoiTransitionTick` and enqueues `SimEventType.SoiCrossing` entries each tick. TestVessels.unity is single-body with infinite-SOI Earth so `SoiCrossingPredictor` returns null and no SoiCrossing entries land in the queue — the Play-behavior change is observable only in a multi-body scene with finite SOIs. End-to-end Play verification of warp-respects-SOI-crossing in such a scene is deferred to a future verification commit. Commit 047's 24 atmospheric/surface predictor tests are EditMode-only; the `VesselEventPredictionDriver` now additionally populates `KeplerState.NextModeTransitionTick` (min-aggregated from atmospheric entry + surface impact) and enqueues `SimEventType.AtmosphericEntry` / `SimEventType.SurfaceImpact` entries each tick. TestVessels.unity's `_body` defaults (vacuum atmospheric-top = 0 and Inspector-default surface radius) make the AtmosphericEntryPredictor return null and the SurfaceImpactPredictor return non-null only for orbits intersecting Earth's surface; the Play-behavior change for atmospheric entry is observable only with an Inspector-set finite atmospheric-top. End-to-end Play verification of warp-drops-on-atmospheric-entry per CONSTRAINTS §2 ("auto-drop warp on atmospheric entry by default") is deferred to a future verification commit alongside commit 048's time-warp UI.

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

Coordinate system (complete, commit 029). Floating origin (complete, commit 029). Sim-tick controller (complete, commit 033). Reference frame hierarchy (complete, commit 044 — `ReferenceBody` hierarchy with parent body wiring + `BodyRegistry` for Guid-keyed lookup). Patched conics (complete, commit 044 — SOI re-rooting via `OrbitalElements.ReRootStateVector` + `VesselSoiRerootingDriver` per-tick detection). Event prediction priority queue (complete, commit 045 — `EventPriorityQueue` owned by `SimTickController`, `PeriapsisApoapsisPredictor` populates orbit-shape events, `VesselEventPredictionDriver` runs per-tick, `RunFixedUpdateCycle` reads queue to bound warp advancement). SOI crossing predictor (complete, commit 046 — `SoiCrossingPredictor` two-path math, integrated into `VesselEventPredictionDriver` with per-predictor try/catch isolation; populates `NextSoiTransitionTick` and enqueues `SimEventType.SoiCrossing` events). Atmospheric entry predictor + surface impact predictor (complete, commit 047 — `AtmosphericEntryPredictor` and `SurfaceImpactPredictor` as thin wrappers around shared `OrbitalElements.SolveConicAtRadius`; `ReferenceBody` schema extended with `SurfaceRadiusMeters` + `AtmosphericTopAltitudeMeters`; populate `NextModeTransitionTick` via min-aggregation). Three-mode physics architecture (commits 038-043). Mode transitions (commits 038-043). Time architecture and time-warp (partial — queue lookup wired at commit 045; full continuous-warp UI + warp-rate scaling controls land at commit 048). Vessel container data structure (commits 038, 042). Save/load format. Scheduled-burn and interstellar-arrival predictors at later phases per CONSTRAINTS §2 extensibility hook.

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
