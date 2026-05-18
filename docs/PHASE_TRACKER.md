# Phase Tracker

**Read this first at session start.** It tells you where the project is, what's currently being worked on, and what's blocking forward progress.

This document is operational state, not narrative history. It updates with every meaningful commit. The detailed per-commit narrative lives in `commits/`.

---

## Current phase

**Phase 0 — Design crystallization and prototype foundation.**

Phase 0 has two halves per `docs/CONSTRAINTS.md` §9:

1. **Design content** — locking the design decisions that gate Phase 1+ implementation. **Status: complete** (commits 001-025).
2. **Prototype foundation** — written netcode contract plus prototype implementation validating it. **Status: in progress** (commits 026-040 landed; mode-transition test still to come).

## Current milestone

**Kepler-rails propagator landed.** Commit 040 wires `KeplerPropagator` into `Vessel.GetWorldPosition` and `Vessel.TransitionToPhysXActive`, removing the Phase 0 "rewind on re-activation" limitation. Step 4 of the sim-tick cycle stays a stub; propagation is on-demand at query time.

After 040, Phase 0's prototype implementation has both halves of the mode boundary working: PhysX-active simulation (via Unity's rigidbody) and Kepler-rails analytic propagation (via the propagator), with verified transitions between them. The one remaining Phase 0 item is a comprehensive mode-transition test exercising the §3.1 trigger conditions end-to-end.

## Active blockers

None.

## Recently landed

| Commit | What | Date |
|---|---|---|
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

**Tests:** 154 EditMode + 6 PlayMode = 160 total, all green (commit 040 baseline). Commit 040 adds 17 new EditMode tests: 15 in `KeplerPropagatorTests` covering propagator math (circular / elliptical / hyperbolic / retrograde orbits, high eccentricity, very-high-eccentricity no-throw, long-interval numerical stability, dt=0 short-circuit, negative-dt backward propagation, elliptical and hyperbolic round-trip preservation, mean-motion period verification, small-dt linearization sanity), plus 2 in `VesselTests` covering propagator integration (`KeplerRails_GetWorldPosition_AdvancesWithSimTick`, `KeplerRails_TransitionToPhysXActive_UsesPropagatedPosition`). Counts cleanly: 137 (commit 038 baseline) + 17 = 154. The earlier "151 projected after Stage 1" reading from the surfacing message was a mis-count of 14 propagator tests when the file actually contained 15; the discrepancy is reconciled in the commit 040 artifact's Lessons section.

**End-to-end Play verification:** TestCoordinates.unity exercises the foundational spine (coordinate system + floating origin + sim-tick controller + deferred listener registration + rigidbody shift) — verified working at commit 034. TestVessels.unity exercises the full vessel-container stack (Vessel + ReferenceBody + TestVesselDriver + mode transitions) — floating-origin shifts verified working in commit 038 end-to-end Play; Space-key mode transitions verified working after commit 039's Input System migration. Commit 040's propagator integration is covered by EditMode tests (the 16 above); end-to-end Play verification of long Kepler-rails sits with visible orbital motion in TestVessels.unity is deferred to a future verification commit. The math is exercised by 14 unit tests and the wiring by 2 integration tests, which is sufficient for the propagator to claim Phase-0 coverage.

**Git state:** Three commits on `main`, pushed to https://github.com/gmkarleski/SPACESIM.

## Phase progression

- **Phase 0** (Design crystallization + prototype) — in progress
- **Phase 1** (Foundations: coordinates, time, modes) — not started; gated by remaining Phase 0 work
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
- [ ] Mode transition test (PhysX-active ↔ Kepler-rails per netcode contract §3.1) — the comprehensive trigger-conditions test is still owed; commit 038's `RoundTrip_PhysXKeplerPhysX_PreservesPositionAndVelocity` and commit 040's two integration tests cover specific paths but not the full §3.1 trigger matrix

Once those land, Phase 0's prototype implementation validates the netcode contract end-to-end and Phase 1 can begin.

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
