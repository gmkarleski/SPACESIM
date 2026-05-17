# 002: Phase 0 foundation resolutions

Land the four Phase 0 foundation-cascading decisions resolved at the start of this session as edits to the constraints document. The resolutions address: floating origin threshold and shift discipline (Issue #1); determinism boundary between authoritative state and PhysX-active simulation, including the multiplayer state-replication model (Issue #2); mode transition and event scheduling architecture, replacing the doc's previously underspecified "Kepler-rails exit conditions" with an analytic event-prediction priority queue (Issue #4); interstellar-cruise arrival semantics, replacing the doc's incorrect "transitions to Kepler-rails on arrival" language with analytic arrival events that branch by vessel type (Issue #5). A new "Foundational architectural principles" subsection is added at the end of section 2, naming the five cross-resolution properties that hold across these four resolutions and apply across the project as load-bearing architectural commitments.

All edits land within section 2 (Foundation). Each existing subsection is extended in place; one new subsection (Mode transitions and event scheduling) is inserted in dependency order; one new subsection (Foundational architectural principles) is appended at the end of section 2. No content elsewhere in the doc is touched by this commit.

## Scope

- `docs/CONSTRAINTS.md` — modified. Section 2 receives edits and additions across three existing subsections (Coordinate system, Physics architecture, Multiplayer architecture preparation), one new inserted subsection (Mode transitions and event scheduling), and one new appended subsection (Foundational architectural principles).

## Rationale

The four resolutions in this commit were derived through structured option analysis at the start of the Phase 0 session. Each issue was identified during initial reading of the source constraints document as a foundation-cascading underspecification that needed to land before any Phase 1 work could begin. Each was resolved by selecting one option from a small set of analyzed alternatives, with the deliberate-non-choice cases (rejected alternatives) recorded for future reference in the project's decisions log.

The resolutions are landed together because they are mutually consistent and were resolved as a connected set. Each refers to invariants in the others: Issue #4's analytic event scheduling is owned by the same sim-tick controller that owns Issue #1's floating-origin shifts; Issue #5's interstellar arrival events register into Issue #4's priority queue; Issue #2's authoritative-state sampling boundary is what makes Issue #4's predictors operate on deterministic state. The Foundational architectural principles subsection captures the cross-cutting structural properties that emerge from these resolutions and that apply across the project, not only to these four issues.

The choice to land all four plus the principles in a single commit reflects their interdependence. Landing them piecemeal would create intermediate states in which the doc described inconsistent foundations.

## Changes

### Edit 1: Section 2, `### Coordinate system` subsection

The existing paragraph ending "Floating origin shift trigger: when camera distance from current origin exceeds a threshold (start with ~10 km, tune later)." is replaced with the following expanded language:

> Floating origin shift threshold: 50 km default. The threshold is data, not a constant — globally per save, with single-value scope at any moment, tunable later by body parameters or difficulty settings.
>
> Shifts only occur at sim-tick boundaries, never mid-tick. The sim-tick controller owns the shift operation; nothing else may shift the origin. When a shift is pending, no system may mutate PhysX state between the end of one tick and the application of the shift — the shift is atomic from PhysX's perspective. All Transforms in the scene translate atomically; PhysX sees a clean teleport of the whole world.
>
> When a shift occurs, floating-origin-aware systems receive a callback or event with the shift vector. Float-precision-cached positions (rendering, particle systems, decals) update in response. Double-precision authoritative state is unaffected by definition — double-precision world coordinates don't move when the rendering origin moves.
>
> Rendering precision at distances of 10–50 km from origin is solved in the rendering layer (camera-relative rendering or logarithmic depth), not in the physics layer. This is Phase 4 work, not Phase 1.
>
> A known refactor path exists if this approach reveals problems we can't solve: vessel-local PhysX scenes, where each PhysX-active vessel simulates in a coordinate space anchored at the vessel itself, with world position stored in the authoritative layer. The interface to the rest of the game — vessels at world coordinates, forces applied, results produced — is invariant across the current approach and the refactor, so the choice is reversible.

### Edit 2: Section 2, `### Physics architecture` subsection

The existing paragraph describing the three modes is preserved verbatim. The paragraph beginning "Transitions between modes are the highest-bug-density boundary..." is preserved verbatim through its end. After that paragraph, the following new paragraphs are inserted:

> Interstellar-cruise arrival semantics are event-driven and branch by vessel type. Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event: the resolution computes synthetic flyby geometry from arrival velocity, aim point, and destination star's gravitational parameter; derives which bodies the probe got close enough to for useful observation and at what observation tier; schedules the science-data-return comms message (delivered after light-speed comms delay); marks the vessel as lost. The vessel never enters Kepler-rails or PhysX-active in the destination system. It is never propagated. It is a math event producing an information payload. Tier 3 (laser sail two-way, decelerated by destination array) arrival inserts the vessel into Kepler-rails in the destination system, with orbital elements determined by arrival geometry — typically a capture orbit around the destination star. Tier 3 arrival also advances proper-time and home-time clocks for the dilation payoff and triggers narrative/notification events. After insertion, a tier 3 vessel behaves as any Kepler-rails vessel.
>
> Architectural principle this clarifies, applying game-wide: the simulation resolves events; the presentation layer visualizes them; they are separable concerns. Tier 2 arrival has a Phase 5/6 visualization (a "ghost flyby" trace through the destination system with closest-approach markers) that is a UI deliverable, not a simulation deliverable. The simulation's correctness does not depend on whether the visualization exists.
>
> Save-load handles interstellar arrivals identically to any other event in the priority queue (see Mode transitions and event scheduling below). Time advances on load; arrival events whose times fall within the gap fire in time order; their resolutions run. The asynchronous-progression machinery handles interstellar arrivals through the same path as research completions, supply line deliveries, and base resource ticks.

### Edit 3: Section 2, new subsection `### Mode transitions and event scheduling` inserted immediately after `### Physics architecture`

> **LOCKED:** Mode transitions and time-driven events are scheduled through a single analytic event-prediction priority queue, owned by the sim-tick controller.
>
> Each Kepler-rails vessel maintains an analytically-computed next-event time. Time-warp advances by `min(tick × warp_rate, time_to_next_event_across_all_vessels)`. The warp controller maintains a sorted priority queue of vessel next-event times. Authority over the queue lives in the sim-tick controller — the same controller that owns floating-origin shifts and authoritative-state sampling from PhysX.
>
> Event types predicted in Phase 1: atmospheric entry, surface impact, SOI boundary crossing (entering and exiting), scheduled-burn arrival, interstellar-cruise arrival. Orbital intersection with other tracked vessels is deferred to Phase 5/6 when collision/rendezvous gameplay arrives.
>
> Event predictors are pure functions of orbital and trajectory state. Given an orbit and current time, a predictor returns the next event time or +∞ if no event in the foreseeable future. No hidden state, no caches that can drift. Cache the result against vessel state; invalidate when state changes.
>
> When a vessel's state changes — new maneuver node, Vizzy command affecting orbit at PhysX-active mode exit, mode transition, SOI re-rooting — its next-event time is recomputed and its queue entry is updated.
>
> When a warp step lands at an event time, the event resolves at exactly the event time, then warp continues or halts according to player preferences. Halt-on-event-type is a player-facing setting: auto-drop warp on atmospheric entry by default; SOI crossings resolved silently by default.
>
> The "warp to next event" UI feature is a view onto the same priority queue. One system, one queue, multiple consumers.
>
> Adding a new event type later means writing its predictor function and adding it to the event-type enum. The priority queue and dispatch are invariant. This is the extensibility hook that lets orbital-intersection arrive in Phase 5/6 without architectural change.

### Edit 4: Section 2, `### Multiplayer architecture preparation` subsection

The existing four-bullet list under "Implementation requirements that apply from Phase 1" is preserved verbatim. After the existing list and before the final paragraph ("The gameplay-design problems of multiplayer..."), the following new paragraphs are inserted:

> The determinism requirement applies to authoritative state, not to the PhysX-active envelope. Determinism is required for all authoritative state outside PhysX-active mode: Kepler-rails propagation, interstellar-cruise, Vizzy VMs, resource/research/supply-line simulation, RNG-driven failure modes, all save/load state. Inside PhysX-active mode, simulation is non-deterministic by design. Future multiplayer for nearby-physics scenes uses host-authoritative state replication rather than lockstep input replication.
>
> The boundary between authoritative state and PhysX-active simulation is the architectural seam. PhysX-active vessel state is sampled into the authoritative double-precision state representation at the sim-tick rate. The reverse direction (forces applied to PhysX-active vessels, mass updates, command injection) goes through defined sim-tick-controlled paths, not via direct PhysX mutation from arbitrary code. PhysX-active mode is a black box that the authoritative layer reads from and writes to through defined channels, never via direct access from other systems.
>
> Vizzy scripts running on PhysX-active vessels read from the sim-tick-sampled authoritative state, not directly from PhysX. This keeps Vizzy behavior reproducible from a save — replay a save, run the same Vizzy script against the same sampled state stream, get the same script behavior. The sim tick is fixed-timestep and decoupled from rendering; PhysX FixedUpdate may run at a different rate than the sim tick; if it does, the sampling boundary is where the rates reconcile.
>
> Authoritative state replication is the multiplayer model for PhysX-active vessels — the authoritative state sampled at each sim-tick is what the host broadcasts, and followers interpolate received state rather than running their own PhysX. Deterministic state outside the PhysX-active envelope can be replicated via lockstep input or state sync, with the specific choice deferred to multiplayer implementation. The architectural commitment is the asymmetry: PhysX-active is host-authoritative; everything outside it is deterministic and can use either model.

### Edit 5: Section 2, new subsection `### Foundational architectural principles` appended at the end of section 2

> Five structural properties hold across the foundation resolutions above and apply project-wide as load-bearing architectural commitments. They are not optional refinements; they are the shape the foundation has, and code should be written to respect them.
>
> **1. Single sim-tick controller as authority.** Floating-origin shifts, the analytic event-prediction priority queue, the next-event scheduling for arrivals, and the authoritative-state sampling from PhysX all live under one controller. That controller is the project's "authority attribution" scaffolding from the multiplayer-preparation requirement, instantiated for Phase 1. Every cross-cutting timing decision passes through it.
>
> **2. Analytic events as the universal pattern for time-driven transitions.** Mode transitions, SOI re-rootings, scheduled burns, interstellar arrivals, and (eventually) orbital intersections all fit one shape: predict a time, queue it, resolve it. This is the temporal analog of the save format's "state at time T, rate function" commitment — instead of "state advances analytically," it is "transitions occur at analytically known times."
>
> **3. Determinism boundary is the PhysX-active envelope, not the whole simulation.** Inside PhysX-active mode, simulation is non-deterministic; outside it, fully deterministic. The boundary is sharp and symmetric: PhysX-active vessel state crosses into the authoritative layer through sim-tick sampling (read side); forces, mass updates, and commands cross from the authoritative layer into PhysX-active through defined sim-tick-controlled channels (write side). These are the only legal channels across the boundary. Direct PhysX mutation from arbitrary code is not permitted. Vizzy reads authoritative state, not PhysX. Save/load reads authoritative state. Eventual multiplayer replication operates on authoritative state.
>
> **4. Separation of simulation from presentation.** Floating-origin shifts are owned by the simulation; rendering precision is solved separately in the rendering layer. Tier 2 interstellar arrival is resolved analytically by simulation; the flyby trace is a presentation deliverable. PhysX feedback is presentation-grade physics; authoritative state is sim-grade. The simulation must be correct without any presentation layer; the presentation layer can be added or improved later without touching simulation.
>
> **5. Reversibility named where it exists.** The floating-origin approach explicitly names vessel-local PhysX scenes as a viable refactor path with a stable external interface. The authoritative-state-sampling boundary means PhysX could in principle be replaced (with Unity Physics ECS, a custom integrator, or anything else) without disturbing the rest of the simulation. The event-type enum in the priority queue is explicitly extensible. Tier 2 arrival's stubbed Phase 1 resolution body is explicitly forward-compatible. The architecture is deliberately structured so that foundation decisions don't paint Phase 2+ into a corner.

## Verification

A future session can confirm this commit landed correctly by checking:

1. **Section 2's `### Coordinate system` subsection** contains the threshold value `50 km` and the explicit statement that shifts occur at sim-tick boundaries. The earlier `~10 km` threshold language no longer appears in this subsection.
2. **The Coordinate system subsection** names vessel-local PhysX scenes as a known refactor path with a stable external interface.
3. **Section 2's `### Physics architecture` subsection** contains the language "Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event" and "The vessel never enters Kepler-rails or PhysX-active in the destination system." The earlier vague "transitions to Kepler-rails on arrival" framing has been replaced by the type-branched arrival semantics for tier 2 and tier 3.
4. **Section 2 contains a new subsection `### Mode transitions and event scheduling`** positioned immediately after `### Physics architecture`. The subsection includes the LOCKED tag, the priority queue commitment, the list of Phase 1 event types (atmospheric entry, surface impact, SOI boundary crossing, scheduled-burn arrival, interstellar-cruise arrival), and the statement that orbital intersection is deferred to Phase 5/6.
5. **The Mode transitions subsection** specifies time-warp advances by `min(tick × warp_rate, time_to_next_event_across_all_vessels)` and names the "warp to next event" UI feature as a view onto the same priority queue.
6. **Section 2's `### Multiplayer architecture preparation` subsection** contains the language "Determinism is required for all authoritative state outside PhysX-active mode" and "Inside PhysX-active mode, simulation is non-deterministic by design. Future multiplayer for nearby-physics scenes uses host-authoritative state replication rather than lockstep input replication."
7. **The Multiplayer architecture preparation subsection** names the boundary direction symmetry: reads from PhysX into authoritative state via sim-tick sampling, writes from authoritative state into PhysX via defined sim-tick-controlled channels.
8. **The Multiplayer architecture preparation subsection** states that Vizzy scripts on PhysX-active vessels read from sim-tick-sampled authoritative state, not directly from PhysX.
9. **Section 2 ends with a new subsection `### Foundational architectural principles`** that enumerates exactly five numbered principles in this order: single sim-tick controller as authority; analytic events as universal pattern for time-driven transitions; determinism boundary is the PhysX-active envelope; separation of simulation from presentation; reversibility named where it exists.
10. **Principle 3 in the Foundational architectural principles subsection** explicitly describes the boundary as "sharp and symmetric" with both the read side (sampling) and the write side (defined channels) named.
11. **No content has been removed from sections 1, 3 through 14.** The changes in this commit are scoped entirely to section 2.
12. **Section 9's resolved crew-abstraction entry** still appears under section 9 with the RESOLVED tag. Its cleanup is deferred to a later commit per the Phase 0 plan.
13. **The Multiplayer architecture preparation subsection** contains the explicit summary paragraph beginning "Authoritative state replication is the multiplayer model for PhysX-active vessels" with the concrete host-broadcasts/followers-interpolate pattern and the closing architectural-asymmetry statement.
14. **Sections untouched by this commit still contain all original content from commit `001`.** Verify the following distinctive phrases are each present exactly once in the file: (S3) `Pixar register, not Goat Simulator register` and `this is how Jeb became a legend`; (S5) `Code is 20% of the work` and `Hill sphere spacing, frost line`; (S8) `This is the vertical slice MVP` and `placeholder cube launches from a planet surface`; (S10) `siren song of pretty screenshots` and `Suggested repo layout`; (S11) `project's institutional memory` and `Critical practice: doc-driven development`; (S12) `this is the kraken returning` and `Pre-flight checklist before generating code`; (S13) `KSP 2's path` and `Stale doc syndrome`; (S14) `Living document` (at start of paragraph) and `Last comprehensive update: Phase 0 design crystallization`. If any anchor is missing, the file has been truncated or corrupted in a section this commit did not intend to modify; the commit has not landed correctly and the file must be repaired before subsequent commits proceed.

If any of these checks fail, the commit has not been correctly applied and should be reviewed against the source artifact before subsequent commits proceed.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/002_phase0_foundation_resolutions.md
```
