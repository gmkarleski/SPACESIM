# Decisions Log

**Purpose:** record questions that have been answered so they don't get re-asked. Resolved questions migrate here from `docs/CONSTRAINTS.md` §10 (open questions). Architectural decisions made during implementation also land here so future sessions know what's locked.

**Read this before:** asking "have we decided X?" The answer is here or it's still open.

**Update discipline:** when a decision lands (whether in a constraints commit, an implementation commit, or a workflow discussion), add an entry here. Reference the commit that locked it. Keep entries dated.

---

## How entries are structured

Each decision gets a heading, a brief question/answer summary, the alternatives that were considered (and why they were rejected), and a reference to the commit or conversation that locked it.

The format is intentionally informal — this is internal project memory, not a publication. Brevity over ceremony.

---

## Resolved decisions

### Aerodynamic model fidelity

**Date:** 2026-05-17 (commit 025)
**Question:** What aerodynamic model fidelity for atmospheric flight — drag-cube approximation, raycast-based, or FAR-style procedural?
**Decision:** Juno-fidelity procedural aerodynamics with calculated lift/drag from procedural aero surfaces.
**Alternatives considered:**
- Drag-cube approximation (KSP stock style) — rejected for being too low fidelity for the spaceplane/supersonic/hypersonic gameplay commitments.
- Raycast-based middle ground — was the original §10 lean; rejected because the atmospheric flight pillar commitment requires real procedural aero surfaces.
**Implication:** procedural fuselage builder and procedural wing builder become v1 (Phase 5) features. Phase 5 weight increases from 3 to 4.
**Locked in:** commit 025 (`docs/CONSTRAINTS.md` §3 atmospheric flight subsection + §9 Phase 5).

### Multiplayer scale for v1.1

**Date:** 2026-05-17 (commit 026)
**Question:** What multiplayer player count should the architecture support?
**Decision:** 2-4 players for v1.1 and beyond. Architecture supports scaling to ~16 if it becomes interesting. No multiplayer in v1 itself.
**Alternatives considered:**
- 5-16 player medium-scale — rejected for current scope; architecture doesn't preclude later.
- 16+ large-scale (distributed authority, server-authoritative) — rejected as substantially harder, not aligned with shared-universe-not-MMO design framing.
**Implication:** multiplayer-shaped architecture from day 1; single-player implemented as one machine with zero peers in that architecture.
**Locked in:** commit 026 (`docs/NETCODE_CONTRACT.md`).

### Sim-tick rate

**Date:** 2026-05-17 (commit 026)
**Question:** What rate should the deterministic sim-tick run at?
**Decision:** 30 Hz fixed.
**Alternatives considered:**
- 60 Hz — more bandwidth + CPU overhead, no clear benefit for orbital mechanics.
- 20 Hz — too coarse for landing physics.
- Variable rate — sophisticated but complicates determinism guarantees significantly.
**Implication:** authoritative state advances at 30 Hz; display interpolates between sim-tick boundaries for smooth rendering.
**Locked in:** commit 026 (`docs/NETCODE_CONTRACT.md` §1.2).

### Determinism scope

**Date:** 2026-05-17 (commit 026)
**Question:** How strict is the determinism guarantee across machines?
**Decision:** Authoritative-state only. No lockstep. Host's state is canonical; clients can show different transient detail.
**Alternatives considered:**
- Full bit-identical determinism — required for lockstep multiplayer; rejected as substantially harder.
- Functional determinism — same inputs produce same outcomes but bit-level state may differ; rejected for being a middle ground without clear benefits.
**Implication:** clients don't need to run identical simulations; they render authoritative state plus client-side prediction.
**Locked in:** commit 026 (`docs/NETCODE_CONTRACT.md` §1.4).

### Single-player as multiplayer degenerate case

**Date:** 2026-05-17 (commit 026)
**Question:** Should single-player and multiplayer share architecture, or be separate code paths?
**Decision:** Single-player is multiplayer with one machine and zero peers. Same authority model, same replication infrastructure (no-op when no peers), same state representation.
**Alternatives considered:**
- Separate single-player code path with multiplayer added later — rejected as the path that produces brittle multiplayer or never-shipping multiplayer.
**Implication:** all single-player code paths use the authority-attribution model and replication infrastructure from day 1; multiplayer in v1.1 is a feature addition, not a rewrite.
**Locked in:** commit 026 (`docs/NETCODE_CONTRACT.md` §8).

### Floating origin threshold

**Date:** 2026-05-17 (commit 029)
**Question:** What distance threshold triggers a floating-origin shift?
**Decision:** Strict greater-than 50 km. At exactly 50.0 km, no shift fires; precision is still adequate. Above 50.0 km, precision starts degrading and shift fires.
**Alternatives considered:**
- Less-than-or-equal 50 km — rejected for causing unnecessary churn at the boundary.
- Different threshold value — 50 km was committed in CONSTRAINTS §2 commit 002 and matches the netcode contract.
**Implication:** active vessel must move past 50 km of current origin to trigger shift; new origin snaps to active vessel position so local coords stay near zero.
**Locked in:** commit 029 (`SPACESIM/Assets/Scripts/Foundation/Coordinates/CoordinateMath.cs`).

### No implicit casts WorldPosition ↔ LocalPosition

**Date:** 2026-05-17 (commit 029)
**Question:** Should the coordinate types allow implicit conversion between WorldPosition and LocalPosition?
**Decision:** No implicit casts. Named methods only (`WorldToLocal`, `LocalToWorld`).
**Alternatives considered:**
- Explicit cast operators — rejected as still encouraging mix-up at call sites.
- Implicit casts — rejected as the classic coordinate-space-type footgun.
**Implication:** every conversion is a named call at the call site; mixed-up usage fails to compile.
**Locked in:** commit 029.

### Dual listener model (interface + event)

**Date:** 2026-05-17 (commit 029, reaffirmed commit 033)
**Question:** How should code subscribe to events like origin shifts and sim-tick advancement?
**Decision:** Both interface and event. Interface for performance-critical / frequently-iterated listeners (avoid per-event delegate allocation); event for ad-hoc subscribers (convenient default).
**Alternatives considered:**
- Interface only — verbose for one-off subscribers.
- Event only — performance overhead for hot paths.
**Implication:** every event-emitting subsystem exposes both. FloatingOriginManager and SimTickController both follow this pattern.
**Locked in:** commits 029 + 033.

### Deferred listener registration architecture

**Date:** 2026-05-17 (commit 034)
**Question:** How should listeners that register before their manager's Awake is handled?
**Decision:** Static pending-listeners queue on the manager class. Listeners that try to register before Instance is set get queued; manager's Awake drains the queue. Static facade methods (`RegisterListenerSafe` / `UnregisterListenerSafe`) hide the queue/direct routing from callers.
**Alternatives considered:**
- Script Execution Order project setting — rejected because it doesn't generalize to runtime-spawned vessels.
- Hard-requiring manager-before-listener in scene setup — rejected as fragile to scene editing and prefab instantiation.
**Implication:** scene authoring is order-independent; runtime spawning will Just Work; the architecture is robust to any spawn pattern.
**Locked in:** commit 034.

### Three physics modes

**Date:** 2026-05-17 (commit 002, reaffirmed commit 026)
**Question:** How is physics simulation organized?
**Decision:** Three modes — PhysX-active (Unity rigidbody simulation), Kepler-rails (analytic orbit propagation), interstellar-cruise (analytic galactic-scale propagation with relativistic time-dilation). Sharp transitions at well-defined boundaries.
**Alternatives considered:**
- Unified physics (always PhysX) — rejected as performance-prohibitive at orbital and interstellar scales.
- Two modes (PhysX + analytic) — rejected as not distinguishing well between in-system and interstellar.
**Implication:** mode transition protocol per netcode contract §3; per-mode time-warp ceilings (1× / 10,000× / 100,000×).
**Locked in:** commits 002 + 026.

### Atmospheric flight as design pillar

**Date:** 2026-05-17 (commit 025)
**Question:** Is atmospheric vehicle gameplay (planes, spaceplanes, supersonic, hypersonic) a real design pillar or stretch content?
**Decision:** Real pillar. Supersonic + hypersonic in scope. Re-entry heating in scope. Spaceplane gameplay in v1.
**Alternatives considered:**
- Defer atmospheric vehicles to v1.1 — rejected; user explicitly said KSP-level mechanical depth is non-negotiable.
- Subsonic-only atmospheric — rejected for same reason.
**Implication:** procedural fuselage + wing builders are v1; Phase 5 weight increased to 4.
**Locked in:** commit 025.

### Four intensive-craft bodies in home system

**Date:** 2026-05-17 (commit 021)
**Question:** Which bodies in the home system get detailed hand-tuning vs procgen treatment?
**Decision:** Four bodies: home planet, home moon, Mars-equivalent, Saturn-equivalent system (with rings, 5-10 hand-tuned moons, Enceladus-equivalent with infinite extractable water, Saturn-orbit satellite anomaly trail, inherited rover on one moon).
**Alternatives considered:**
- More bodies (full solar system) — rejected; design intent is intensive craft over breadth for home-system content.
- Fewer bodies — rejected; the four cover the dramatic-shape variety needed.
**Locked in:** commit 021.

### Mission Control as primary UI

**Date:** 2026-05-17 (commit 023)
**Question:** What's the primary game UI shape?
**Decision:** Mission Control. TV-screen layout with 6-9 vessels visible (scroll/page for more). Channel-changing via keys. Real-time render. Opens on game start. Pauses game when open.
**Alternatives considered:**
- Map view first — rejected as too abstract for opening experience.
- Single active vessel cockpit first (KSP style) — rejected as not capturing the program-of-craft framing.
**Locked in:** commit 023.

### Sandbox simulator runs from last save

**Date:** 2026-05-17 (commit 023)
**Question:** How does the in-game simulator (sandbox) for testing missions relate to the real campaign?
**Decision:** Runs from last save. Real physics. Reverts on exit. Only sees catalog-known data; uses parameter-derived defaults at scanned-but-unvisited bodies. Stylized monitor-bezel UI with SIMULATOR watermark.
**Alternatives considered:**
- Pure sandbox decoupled from save state — rejected as not actually useful for mission rehearsal.
- Sandbox modifies save — rejected as eliminating its value as a safe test environment.
**Locked in:** commit 023.

### Workflow rule 6 (sandbox mount staleness)

**Date:** 2026-05-17 (commit 035)
**Question:** When sandbox mount view diverges from host filesystem state, which is authoritative?
**Decision:** Host filesystem is canonical. For files Unity has written or Cowork's Edit tool has modified mid-session, verify with byte-level reads (`wc -c`, `stat`, `xxd | tail`) when verification produces unexpected results, and defer git operations to host-side replay.
**Alternatives considered:**
- Trust sandbox view always — rejected; three data points show it can lie.
- Always require byte-level cross-check — rejected as overhead on every verification.
**Locked in:** commit 035.

### VesselRegistry as plain static class (no deferred-registration)

**Date:** 2026-05-17 (commit 038)
**Question:** Should `VesselRegistry` use the same deferred-registration pattern as `FloatingOriginManager` from commit 034 (pending-listeners queue, `RegisterListenerSafe` facade, `DrainPendingForTesting` test hook)?
**Decision:** No. `VesselRegistry` is a plain static class with a single `_vessels` list. No pending queue, no drain step. The `Safe` suffix on `RegisterVesselSafe` / `UnregisterVesselSafe` is preserved for cross-codebase naming consistency, but the methods only do null-check and dedup.
**Alternatives considered:**
- Reflexive copy of commit 034's pattern (pending queue + drain) — rejected as over-engineering for a static class.
- Singleton MonoBehaviour with deferred-registration — rejected; a registry of vessels has no need for an Instance-claim step and the MonoBehaviour overhead buys nothing.
**Implication:** ~50 LOC saved in VesselRegistry.cs vs the pattern-matched version, ~4 tests saved that would have exercised pending-queue lifecycle.
**Reusable pattern-selection criterion:** future static-class registries follow VesselRegistry's simple pattern, not FloatingOriginManager's. The discriminator is "does the class have a non-null window?" If yes — singleton MonoBehaviour whose `Instance` is null between scene load and Awake — use the pending queue. If no — static class with always-available members — direct registration is sufficient. This criterion separates the two patterns cleanly and gives future readers a one-question test for which pattern applies.
**Locked in:** commit 038.

### TestVesselDriver uses new Input System API (not legacy UnityEngine.Input)

**Date:** 2026-05-17 (commit 039)
**Question:** Which input API does Phase 0 test-scene code use — legacy `UnityEngine.Input` or the new `UnityEngine.InputSystem` package?
**Decision:** New Input System API. `Keyboard.current.spaceKey.wasPressedThisFrame` instead of `Input.GetKeyDown(KeyCode.Space)`. Vessels asmdef gains a `Unity.InputSystem` reference.
**Alternatives considered:**
- Legacy `UnityEngine.Input` — rejected; the project's `activeInputHandler: 1` setting (Input System Package only) makes legacy API throw `InvalidOperationException` at runtime.
- Switch project setting to "Both" so legacy works alongside new — rejected; aligning with the Unity 6 default reduces drift risk and the migration cost is small (one file, one asmdef reference).
- Dual-API approach with `#if ENABLE_LEGACY_INPUT_MANAGER` / `#if ENABLE_INPUT_SYSTEM` preprocessor branches — rejected as over-engineering for one Phase 0 test driver.
**Implication:** All future MonoBehaviour input handling in this project uses the new Input System API. The Vessels asmdef now references `Unity.InputSystem`; other modules that need input similarly add the reference.
**Locked in:** commit 039.

### Kepler-rails propagator: on-demand stateless helper with schema fidelity

**Date:** 2026-05-18 (commit 040)
**Question:** When the Kepler-rails propagator lands, where does it live, what does it cache, and does its math force a schema change to `KeplerState`?
**Decision:**
- **Shape:** stateless static helper `KeplerPropagator.PropagateState(KeplerState, long currentTick, double mu, double tickIntervalSeconds)`. No instance state, no caching. One static class per `OrbitalElements` / `KeplerPropagator` separation of concerns: elements vs evolution.
- **Invocation:** on-demand from `Vessel.GetWorldPosition` and `Vessel.TransitionToPhysXActive`. NOT driven from `SimTickController.Step4`. Step 4 stays a stub for this commit; it gains work when the event queue and multi-vessel state-update needs land.
- **Math:** Newton-Raphson solving Kepler's equation. Elliptic branch (e<1) uses Conway's 1986 starter `E₀ = M + e·sin(M)/(1 - sin(M+e) + sin(M))` with defensive denominator guard. Hyperbolic branch (e>1) uses Conway's hyperbolic starter (`H/(e-1)` for small `|M|`, `sign(M)·ln(2|M|/e + 1.8)` otherwise) on `e·sinh(H) - H = M`. Tolerance 1e-10, max 15 iterations. No explicit parabolic (e≈1) branch — accept numerical instability in the ~1e-8 band around e=1.
- **Schema fidelity:** `KeplerState.TrueAnomalyAtEpoch` stays as the canonical epoch value per netcode contract §2.3. The propagator converts ν₀ → M₀ → M(t) → E(t)/H(t) → ν(t) internally on every call. The schema is unchanged; what's stored is one true anomaly value and an epoch tick.
**Alternatives considered:**
- **Cache mean anomaly at epoch as a derived field on `KeplerState`** — rejected; this would conflate stored state (the contract specifies `TrueAnomalyAtEpoch`) with derived state (an implementation choice of which anomaly the math wants). Caching it also raises invalidation questions if any other element ever changes.
- **Change `KeplerState.TrueAnomalyAtEpoch` to `MeanAnomalyAtEpoch`** — rejected; this is a schema change to the netcode contract §2.3 and deserves its own commit with rationale, not as a side-effect of implementation work. True anomaly is also the more natural value for rendering, debugging, and orbit-visualization tooling.
- **Stateful propagator with cached last-tick state** — rejected; doesn't compose well across multiple vessels and introduces invalidation surface area. The recompute cost (1 Newton-Raphson solve per query) is sub-microsecond at the iteration counts we see.
- **Drive propagation from `SimTickController.Step4` per FixedUpdate** — rejected for this commit; would require multi-vessel iteration infrastructure that doesn't exist yet, and on-demand computation is correct by construction for the current scope (single active vessel querying GetWorldPosition + transition).
- **Explicit Barker's equation parabolic branch** — rejected; numerical instability in a band of e ≈ 1 ± 1e-8 is acceptable for this prototype phase. Real orbits don't sit precisely at e=1; the instability band is narrow enough that physical scenarios (planetary capture, escape) sample either side without touching the singular region.
**Rationale (the schema-vs-implementation distinction):** the netcode contract specifies *what's stored*, not *how the math uses it*. `TrueAnomalyAtEpoch` is a stored field; the propagator's choice to convert to mean anomaly internally is an implementation detail that shouldn't surface in the schema. This distinction matters because schema changes ripple — they affect serialization, replication, save-load, future renderers, and any tooling that reads `KeplerState`. Implementation choices stay local to the implementation. When considering "should the schema change to make this implementation cleaner?" the default answer is no; schema changes deserve their own commit, their own rationale, and explicit acknowledgement of which downstream contracts they affect.
**Implication:** future propagator extensions (e.g., explicit parabolic branch, J2 perturbations, atmospheric drag) layer on top of `KeplerPropagator.PropagateState` without schema changes. The propagator is the single point of evolution; `KeplerState` is the single point of element storage.
**Locked in:** commit 040.

---

## Pending decisions (open questions still in `docs/CONSTRAINTS.md` §10)

This section mirrors §10's open questions so a reader can find both resolved and pending decisions in one place. When an entry here lands a decision, it moves to "Resolved decisions" above and gets removed from this section.

Each question is flagged with the phase by which it must be resolved. Bullets mirror §10 verbatim; when a question resolves it gets a full "Resolved decisions" entry above and the bullet drops from here.

- **Vizzy implementation foundation** (Phase 5): xNode (open source, mature) vs Unity Visual Scripting (heavier, integrated) vs custom. Investigate before locking.
- **Final resource set** (Phase 6): 6-8 listed above is the working set; final names and exotic-matter inclusion not locked.
- **Anomaly authoring system** (Phase 7): fully procedural vs hand-authored seeds vs hybrid. Likely hybrid; specifics undecided.
- **Mobile shipping** (Phase 8): do we actually ship a mobile build? Decision deferred until vertical slice exists.
- **Post-tier-3 propulsion** (Phase 7+): do we add a fourth FTL tier with exotic physics, or stop at tier 3? Affects whether 'exotic matter' is in the resource set.
- **Multiplayer feature scope** (post-v1): co-op only? Competitive? Persistent shared universe? Architectural foundation supports any; gameplay design deferred.
- **Tutorial structure** (Phase 8): linear scripted vs contextual / discovered. Both have merit. Decide late, after the rest of the game shape is concrete.
- **Character visual design language** (Phase 4 or 8): specific species look, proportions, color palette, animation style. Commit to mid-stylization humanoid; specifics decided when art direction matures.
- **Character expressiveness depth** (Phase 4): do characters have animated facial reactions (Kerbal-style screams during stress)? Could be a major emotional asset or scope creep. Decide alongside art direction.
- **Colony autonomy depth** (Phase 7): do colonies just produce resources, or do they grow / change / develop their own state independently? Time-dilation is more interesting if colonies actually evolve in player's absence.
- **Save format technology** (Phase 1): JSON (readable, debuggable, larger) vs binary (compact, faster, opaque) vs hybrid. Lean JSON for early development, optional binary later.
- **Anomaly resolution UX** (Phase 7): how does the player learn what an anomaly's true cause is? Pop-up reveal vs in-world investigation vs progressive observation tier upgrade. Defer.

---

## Decisions that don't fit elsewhere

Architectural decisions made during implementation that weren't preceded by an explicit open question in §10. These still need recording so future sessions don't re-derive them from scratch.

### Singleton MonoBehaviour pattern for managers

**Date:** 2026-05-17 (commit 029, reaffirmed commit 033)
**Decision:** Project-wide manager classes (FloatingOriginManager, SimTickController) use the singleton MonoBehaviour pattern: static `Instance` property, Awake claims Instance, OnDestroy clears, duplicates log error and destroy themselves.
**Why:** consistent with Unity's standard pattern, Inspector-tunable, lifecycle hooks fire correctly, scene-relative reset semantics.
**Alternatives:** static class (no lifecycle), ScriptableObject (state leaks across save/load).

### Form 1 (canonical, optionalUnityReferences) for PlayMode test asmdefs

**Date:** 2026-05-17 (commit 032)
**Decision:** PlayMode test asmdefs use the canonical Unity form with `optionalUnityReferences: ["TestAssemblies"]` rather than the modern verbose form with `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.
**Why:** Unity's own sample asmdefs use Form 1 for PlayMode; minimum surface area is the right move after two prior asmdef-trap sessions (commits 030-031).
**Locked in:** commit 032.

### Form 2 (modern verbose) for EditMode test asmdefs

**Date:** 2026-05-17 (commit 030)
**Decision:** EditMode test asmdefs use the modern verbose form with explicit `references`, `defineConstraints: []`, `includePlatforms: ["Editor"]`, `overrideReferences: true`, and `precompiledReferences: ["nunit.framework.dll"]`.
**Why:** what we converged on through commits 029-031 diagnosis. The asymmetry with PlayMode (Form 1 canonical) is intentional — different test modes have different optimal asmdef shapes.
**Locked in:** commits 030-031.

### Explicit asmdef references rather than autoReferenced bet

**Date:** 2026-05-17 (commit 030)
**Decision:** Asmdef `references` field explicitly lists all dependencies. Don't rely on `autoReferenced: true` to include Unity first-party packages.
**Why:** `autoReferenced: true` means "other asmdefs auto-link to this one," not "this asmdef auto-links to everything." Commit 030 fixed the consequence of the inverted semantic understanding.
**Locked in:** commit 030.

### End-to-end Play verification as standing category

**Date:** 2026-05-17 (commit 034)
**Decision:** Architectural commits with test scenes get end-to-end Play verification as a standard verification category, alongside file-level checks, compilation, and unit tests. Not just commits with obvious end-to-end concerns.
**Why:** commit 033's file-level checks passed but the actual scene behavior was broken (anchor-before-manager registration bug). The verification gap was real.
**Status:** observation recorded in commit 034 artifact, not yet a formal workflow rule. May formalize when more data points accumulate.

---

## How this document maintains itself

When a decision lands:
1. Add an entry to "Resolved decisions" with date, question, decision, alternatives, and commit reference.
2. If the decision resolved an item in `docs/CONSTRAINTS.md` §10, remove that item from §10 in the same commit.
3. If the decision was made during implementation without an explicit §10 entry, add it to "Decisions that don't fit elsewhere."
4. Pending decisions section mirrors §10's current content.

Entries don't get edited after landing unless a decision is reversed (in which case the entry gets struck through with a note pointing to the reversal commit; the new decision gets its own entry).
