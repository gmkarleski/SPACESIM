# Phase Tracker

**Read this first at session start.** It tells you where the project is, what's currently being worked on, and what's blocking forward progress.

This document is operational state, not narrative history. It updates with every meaningful commit. The detailed per-commit narrative lives in `commits/`.

---

## Current phase

**Phase 0 — Design crystallization and prototype foundation.**

Phase 0 has two halves per `docs/CONSTRAINTS.md` §9:

1. **Design content** — locking the design decisions that gate Phase 1+ implementation. **Status: complete** (commits 001-025).
2. **Prototype foundation** — written netcode contract plus prototype implementation validating it. **Status: complete** (commits 026-042 landed; all 12 of 12 Phase 0 remaining-work items checked; one of two Phase 1 carryover items closed).

## Current milestone

**Phase 1 underway — early Phase 0 carryover work closing out.** Commit 041 closed Phase 0 with the comprehensive §3.1 mode transition procedure test. Commit 042 closes one of the two Phase 1 carryover items from that close-out: the `Vessel.Initialize`-in-KeplerRails state inconsistency is fixed via an overload-based API (3-arg overload now rejects KeplerRails parallel to InterstellarCruise; new 4-arg overload accepts caller-provided `KeplerState`). One Phase 1 carryover remains: the per-sim-tick trigger evaluator.

**The prototype implementation validates the netcode contract end-to-end**: coordinate system, floating origin, sim-tick controller, deferred listener registration, vessel containers, mode transitions both directions, Kepler-rails propagation, mode transition procedure under each §3.1 condition, schema-invariant-maintaining Initialize.

**Trigger evaluation per §3.1 remains the open Phase 1 carryover.** No code evaluates whether a vessel SHOULD transition modes per sim-tick — transitions are invoked imperatively (via `Vessel.TransitionTo*` calls). §3.1 specifies trigger evaluation runs at every sim-tick; commit 041 tests the procedures executed when transitions are invoked, not the evaluation logic that would decide when to invoke them.

## Active blockers

None.

## Recently landed

| Commit | What | Date |
|---|---|---|
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

**Tests:** 178 EditMode + 6 PlayMode = 184 total, all green (commit 042 baseline). Commit 042 adds 6 new EditMode tests covering the `Vessel.Initialize` 4-arg overload (happy path with KeplerRails + KeplerState, null-KeplerState fallback, PhysXActive-overload-misuse fallback, 3-arg-KeplerRails rejection, component-shape under 4-arg KeplerRails, field-by-field element preservation). Counts cleanly: 172 (commit 041 baseline) + 6 = 178. Three existing EditMode tests modified in place (lines 127, 237, 838 of `VesselTests.cs`): switched to the new 4-arg overload or rewritten to construct the state-inconsistency directly. Three PlayMode tests modified in place (`VesselPlayModeTests.cs` lines 96, 111, 155): two switched to PhysXActive (test intent didn't depend on KeplerRails), one switched to the new 4-arg overload. PlayMode test count unchanged at 6.

**End-to-end Play verification:** TestCoordinates.unity exercises the foundational spine (coordinate system + floating origin + sim-tick controller + deferred listener registration + rigidbody shift) — verified working at commit 034. TestVessels.unity exercises the full vessel-container stack (Vessel + ReferenceBody + TestVesselDriver + mode transitions) — floating-origin shifts verified working in commit 038 end-to-end Play; Space-key mode transitions verified working after commit 039's Input System migration. Commit 040's propagator integration is covered by EditMode tests (15 propagator + 2 integration); end-to-end Play verification of long Kepler-rails sits with visible orbital motion in TestVessels.unity is deferred to a future verification commit. Commit 041's 18 transition-procedure tests are EditMode-only by construction. Commit 042's 6 new Initialize-overload tests are EditMode-only (the API surface is deterministic; PlayMode adds nothing).

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

- [ ] **Implement per-sim-tick mode transition trigger evaluation per netcode contract §3.1.** Currently no code evaluates whether a vessel should transition modes; transitions only happen when explicitly invoked. §3.1 specifies trigger evaluation runs every sim-tick. This requires: (a) authoritative state fields for thrust, atmospheric context, contact forces, player focus, and proximity that don't yet exist; (b) evaluation logic in `SimTickController.Step6_DetectModeTransitions` or a per-vessel method; (c) tests that verify the evaluator fires transitions when conditions are met. **Decision needed before implementation:** where the evaluation lives (SimTickController vs Vessel method) and how partial state availability is handled in early Phase 1.

- [x] ~~Fix `Vessel.Initialize`-in-KeplerRails state inconsistency.~~ **Closed at commit 042** via overload-based API. Option (a) from the two carryover options was chosen: a new 4-arg `Initialize` overload accepts caller-provided `KeplerState`; the 3-arg overload now rejects `PhysicsMode.KeplerRails` (parallel to the existing `InterstellarCruise` rejection) and falls back to `PhysXActive`. The schema invariant (Mode == X ⟹ XState != null) is enforced at Initialize return for all callers. See DECISIONS.md "Vessel.Initialize signature for state-mode consistency (commit 042)" for the design rationale.

---

## Systems by phase

This section maps which game systems land in which phase. The list is derived from CONSTRAINTS.md §9 build order plus accumulated design content. Use it to answer "when does X get built?"

### Phase 1 — Foundations

Coordinate system (complete, commit 029). Floating origin (complete, commit 029). Sim-tick controller (complete, commit 033). Reference frame hierarchy. Patched conics. Three-mode physics architecture. Mode transitions. Time architecture and time-warp. Vessel container data structure. Save/load format.

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
