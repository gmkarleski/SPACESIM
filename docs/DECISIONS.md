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

### Test-only scope for §3.1 mode transition test (commit 041)

**Date:** 2026-05-18 (commit 041)
**Question:** Should commit 041 implement per-sim-tick trigger evaluation per netcode contract §3.1, or limit scope to testing the existing transition procedures?
**Decision:** Test-only scope. Commit 041 writes 18 tests against `Vessel.TransitionToKeplerRails` and `Vessel.TransitionToPhysXActive` exercising each §3.1 condition the test can construct in Phase 0. The trigger evaluator itself (per-sim-tick code that decides when transitions should fire) is deferred to Phase 1 and recorded as a Phase 1 remaining-work item.
**Alternatives rejected:**
- **Option B from the design conversation: implement `Vessel.EvaluateTriggers()` or `SimTickController.Step6_DetectModeTransitions` extension performing §3.1 condition checks per sim-tick.** Rejected because most §3.1 conditions depend on authoritative state fields (thrust state, atmospheric drag state, contact forces, player focus, proximity clustering) that don't exist in Phase 0. Implementing trigger evaluation would require either stub state fields that always pass checks (no real validation) or real state field implementation (Phase 1 scope creep into Phase 0).
**Rationale (reusable pattern):** When implementing a contract item that depends on systems not yet built, test what's testable now and explicitly defer the rest to the phase where the dependencies exist. This preserves bounded phase scope while honestly documenting which contract items remain partially implemented. The commit artifact must explicitly distinguish what's tested (the procedure) from what isn't (the trigger evaluator).
**Implication:** Phase 1 inherits the trigger evaluator as a carryover item. When implementing in Phase 1, the test infrastructure from commit 041 can be extended to validate the evaluator (the same test setups become evaluator-invocation tests rather than direct-method-call tests).
**Locked in:** commit 041.

### Vessel.Initialize signature for state-mode consistency (commit 042)

**Date:** 2026-05-18 (commit 042)
**Question:** How does Initialize maintain the schema invariant that Mode == KeplerRails implies KeplerState != null?
**Decision:** Overload-based API. The 3-arg overload (state, body, mode) supports only `PhysXActive` cleanly; it rejects `KeplerRails` with an error and falls back to `PhysXActive`, parallel to the existing `InterstellarCruise` rejection. A new 4-arg overload (state, body, mode, initialKeplerState) supports explicit Kepler-rails construction with caller-provided orbital elements.
**Alternatives rejected:**
- **Single signature with optional/nullable parameter:** ambiguous semantics, harder to enforce caller intent at compile time.
- **Reject KeplerRails entirely in Initialize** (force all rails entries through `TransitionToKeplerRails`): would prevent legitimate save-load use cases where a vessel needs to be reconstructed directly in Kepler-rails mode with stored orbital elements.
- **Modify the existing 3-arg signature to add a `KeplerState` parameter:** would break all existing PhysXActive callers, requiring sweeping updates.
**Rationale:** Overload-based API preserves backward compatibility for the dominant case (PhysXActive callers, all unchanged) while making the KeplerRails construction path explicit and self-documenting. The compile-time signature differences make caller mistakes visible (PhysXActive callers don't accidentally pass orbital elements; KeplerRails callers can't accidentally forget them). Shared private `InitializeCore` method de-duplicates the body between overloads.
**Implication:** Future Initialize variants for new modes (`InterstellarCruise` when Phase 6 ships, any others) follow the same pattern: per-mode overloads with mode-specific state parameters.
**Locked in:** commit 042.

### Per-sim-tick mode transition trigger evaluator (commit 043)

**Date:** 2026-05-18 (commit 043)
**Question:** Where does §3.1's per-sim-tick trigger evaluation live in the codebase, given the asmdef direction established in commit 038 (Vessels → SimTick, not the reverse)?
**Decision:** Multi-part design:
- **(a) Evaluation logic lives on `Vessel`** as `EvaluateTransitionTriggers(IActiveVessel)`. The vessel knows its own state, reference body, rigidbody, and mode — it is the natural owner of the question "should I transition?" Return type is a `TransitionEvaluation` struct (`SuggestedMode` nullable + `Reason` enum).
- **(b) Driver lives in Vessels module** as `VesselTransitionDriver` static class. Subscribes to `SimTickController.TickAdvanced` (the cross-module event hook), iterates `VesselRegistry.Vessels`, calls `EvaluateTransitionTriggers` on each, dispatches the corresponding `TransitionTo*` method.
- **(c) Driver fires from `TickAdvanced` (after Step 10), not literally inside Step 6.** §3.1 says "trigger evaluation runs at every sim-tick" — satisfied by TickAdvanced (one fire per tick, transitions complete between tick N's Step 10 and tick N+1's Step 1). Literal "inside Step 6" would require SimTickController to iterate `VesselRegistry`, which would close the asmdef cycle commit 038 deliberately broke.
- **(d) Driver is disabled by default** (`Enabled` flag, defaults false). Stub-state conditions (thrust, atmosphere, contact, focus, scripted, multi-vessel) all always-pass in Phase 0; enabling the driver would cause vessels beyond 50 km to automatically transition to Kepler-rails during Phase 0 Play. Flag stays false until upstream state systems populate the schema fields with real values.
- **(e) Return type is `TransitionEvaluation` struct, not nullable `PhysicsMode?`.** The struct pairs the decision (`SuggestedMode`) with the firing condition (`Reason`); the driver logs the reason directly. A nullable would force the driver to reverse-engineer the reason from the vessel's current mode — fragile and gets harder as conditions accumulate.

**Alternatives rejected:**
- **`SimTickController.Step6_DetectModeTransitions` directly iterating `VesselRegistry`:** closes the asmdef cycle (SimTick → Vessels), which commit 038 broke for the IActiveVessel interface reason.
- **`IActiveVessel` interface extension with an `EvaluateTriggers` method:** would only handle the single active vessel (the interface's existing scope); the §3.1 contract specifies trigger evaluation on EVERY vessel, including Kepler-rails vessels needing proximity-to-active-vessel checks.
- **Enabled-by-default with stub-state conditions:** would cause vessels beyond 50 km to automatically transition to Kepler-rails during Phase 0 Play (the conjunction with no thrust, no atmosphere, no contact, well-defined trajectory always passes in Phase 0 because the first three are stubs). Surprising behavior for any Phase 0 test scene with vessels beyond proximity.
- **`Rigidbody.IsSleeping()` as a contact-force proxy:** would produce false-positives — a vessel sitting on a planet with rigidbody-asleep would incorrectly report no-contact and become eligible for Kepler-rails transition while physically touching a body. Always-false stub is the safer Phase 0 placeholder.
- **Simpler nullable `PhysicsMode?` return type:** loses the reason information; diagnostic logging would have to reverse-engineer which condition fired from the vessel's mode, which is fragile and worsens as conditions accumulate.
- **Internal driver state on `SimTickController` (a `_triggerEvaluatorEnabled` flag on the controller):** would require SimTickController to know about the trigger evaluator's existence, inverting the asmdef ownership direction. The driver's `Enabled` flag lives on the driver itself, where the "is the evaluator running?" question naturally belongs.

**Rationale (preserved asmdef direction + Phase 0 Play stability):** This multi-part split preserves the asmdef direction established in commit 038, keeps Phase 0 Play behavior identical to commit 042, and lays the infrastructure for upstream state systems to flip the `Enabled` flag when they're ready. The disabled-by-default flag is the key — it lets the evaluator land, be tested, and be reviewed in isolation without affecting any current Phase 0 scene's behavior. The Step-6-vs-TickAdvanced timing distinction is a deliberate trade-off, documented rather than hidden: the §3.1 contract's "trigger evaluation runs at every sim-tick" is satisfied at the TickAdvanced event boundary; the literal location in the cycle is the event handler, not step 6. Self-proximity edge case (vessel evaluates against itself) works correctly without special-case math: distance to self is zero, never exceeds the threshold, so the active vessel never suggests Kepler-rails for itself (correct per the contract's "any active vessel" framing).

**Implication:** When Phase 5+ engine systems wire real thrust into `PhysXState.ActiveThrustN`, the trigger evaluator immediately becomes meaningful without any code changes to `Vessel.EvaluateTransitionTriggers` or the driver. Same for atmospheric drag (Phase 5 atmospheric model), contact forces (Phase 3+ collision system), player focus (Phase 5 Mission Control UI), Vizzy scripted thrust (Phase 5), and multi-vessel proximity clustering (Phase 5+ multi-vessel sim). Each upstream system that lands moves more conditions from stub-state to real-state. A single future commit can flip `VesselTransitionDriver.Enabled = true` once enough upstream state exists that automatic transitions are meaningful.

**Locked in:** commit 043.

### SOI re-rooting design (commit 044)

**Date:** 2026-05-18 (commit 044)
**Question:** How should the codebase implement §2 reference-frame-hierarchy + patched-conics SOI handling? Specifically: where do per-body SOI radii live; how is the body hierarchy represented; where does the per-tick re-rooting check fire; what coordinate-frame math does the helper need to perform; and how does the design extend to Phase 4+ when bodies orbit?
**Decision:** Multi-part design:
- **(a) SOI radius hand-set per body in Phase 1.** `ReferenceBody.soiRadiusMeters` is a `[SerializeField]` Inspector field with default `double.PositiveInfinity` (top-level body convention). Bodies with a parent have a finite value set in the Inspector. Documented intent to compute via Laplace sphere formula `r_SOI ≈ a · (m/M_parent)^(2/5)` when bodies orbit in Phase 4+; the formula needs the body's semi-major axis around its parent, which doesn't exist in Phase 1 where bodies are stationary.
- **(b) Parent body referenced both ways.** Inspector `[SerializeField] ReferenceBody parentBody` for human-convenience wiring, plus `Guid ParentBodyId` for save-load. On `Awake`, the Inspector reference resolves into both the cached `ParentBody` reference (runtime) and `ParentBodyId` (persisted). Save-load reads/writes the Guid; `BodyRegistry.TryGetBodyById` resolves Guids back to runtime references at load time.
- **(c) NETCODE_CONTRACT.md amended with new §2.7 BodyState** ahead of full implementation. Contract specifies: body_id, name, mass_kg, mu (derived), position_world, soi_radius_meters, parent_body_id, plus Phase 4+ deferred fields (orbital_state_around_parent, axial_tilt, rotation_rate, surface_terrain_seed, atmospheric_profile, children_body_ids). Existing `bodies: Map<BodyID, BodyState>` added to §2.6 WorldAuthoritativeState. Contract is now ahead of implementation rather than behind — matches the "contract first" discipline.
- **(d) New `VesselSoiRerootingDriver` static class, NOT folded into `VesselTransitionDriver`.** Architectural distinction: SOI re-rooting is intra-mode (vessel stays on Kepler-rails before and after; only reference body changes). Mode transitions are inter-mode (PhysX-active ↔ Kepler-rails). Separating the two drivers keeps each focused. Both subscribe to `SimTickController.TickAdvanced` independently.
- **(e) No `Enabled` flag on `VesselSoiRerootingDriver`.** Unlike `VesselTransitionDriver` (which gates on stub-state conditions that always pass in Phase 0), SOI re-rooting has real implementation in Phase 1. Always-on is the right default; in single-body scenes the check correctly finds no crossings, in multi-body scenes it fires at the right moments.
- **(f) Re-rooting math lives in `OrbitalElements.ReRootStateVector` as static helper.** Pattern parallels existing `ComputeFromStateVector` and `ComputeStateVector` — stateless, testable, takes raw positions + μ rather than `ReferenceBody` references. Phase 4+ velocity-extension documented in the helper's XML doc with explicit caching-hazard warning.
- **(g) Top-level body convention: `SoiRadiusMeters = PositiveInfinity`.** Mathematically clean for patched conics — no finite distance exceeds infinity, so the outward-re-rooting check never fires for a top-level body. A body with no parent has no SOI boundary within its system.
- **(h) First-match-wins for overlapping child SOIs.** Phase 1 simplification: if a vessel is somehow inside multiple child SOIs simultaneously, the driver re-roots to the first child found in `BodyRegistry.GetChildrenOf` enumeration order. Correct behavior would be "closest SOI center" or "highest gravitational dominance"; revisit when multi-body scenes get rich enough for overlap to be plausible.
- **(i) Self-cycle defensive check at `ReferenceBody.Awake`.** If a body is Inspector-wired as its own parent, Awake logs an error and treats the body as top-level (`ParentBody = null`, `ParentBodyId = Empty`). Cheap protection against a footgun.

**Alternatives rejected:**
- **`Vessel.ReRootToBody` accepting an updated `KeplerState` directly:** would move re-rooting math out of `OrbitalElements`. Rejected; pattern is consistent with `TransitionToKeplerRails` which also delegates math to `OrbitalElements.ComputeFromStateVector`.
- **Re-rooting math taking `ReferenceBody` references:** would couple the math to scene infrastructure and prevent testability without `AddComponent<ReferenceBody>` setup. Existing `OrbitalElements` helpers take raw `double mu` parameters; consistency wins.
- **`Rigidbody.IsSleeping()`-style proxy for "vessel near a body":** Phase 0 / Phase 1 has no actual contact-force or proximity-detection state beyond geometric distance, which is what `IsBeyondProximityThreshold` already uses for the trigger evaluator. The SOI-radius geometric distance check is the right tool here.
- **Reactive children-list maintenance on `ReferenceBody`:** would have child bodies notify their parent at register-time and unregister-time. Rejected as premature optimization — `BodyRegistry.GetChildrenOf` iterates the registry's body list and filters by parent reference. O(N) per call where N is the body count; single-digit N in Phase 1 / early Phase 4. Caching is a Phase-5+ concern when N grows.
- **Closing the asmdef cycle for the driver:** `VesselSoiRerootingDriver` lives in the Vessels asmdef and subscribes to `SimTickController.TickAdvanced` from outside the controller. Putting it inside SimTickController.Step6 would close the asmdef cycle (matches the same trade-off documented in commit 043's DECISIONS entry).

**Rationale (asmdef discipline + Phase 1 scope + Phase 4+ migration path):** This design preserves the established asmdef direction (Vessels → SimTick), keeps the Phase 1 implementation honest about what's stub vs real (SOI radii hand-set; Laplace formula deferred), documents the velocity-frame-transform hazard explicitly so Phase 4+ work doesn't silently break (the XML hazard note on `ReRootStateVector` names the failure mode in terms of body-velocity-not-yet-subtracted), and follows the commit 040-043 pattern of math-helper + driver + integration tests. The first-match-wins child selection is a deliberate simplification with a named upgrade path; the top-level infinite-SOI convention is mathematically clean for patched conics. Self-cycle defensive check at Awake is cheap insurance against Inspector footguns.

**Implication:** When Phase 4+ procgen-bodies work lands, the Laplace sphere formula replaces the hand-set values; bodies gain their own `orbital_state_around_parent` field; and `ReRootStateVector`'s signature extends to take `currentBodyVelocityWorld` and `newBodyVelocityWorld` so the velocity-frame transform becomes meaningful. The driver requires no changes at that point — the per-tick check, the re-rooting dispatch, and the diagnostic logging all work unchanged. The locked Phase 1 design is forward-compatible with Phase 4+ multi-body work.

**Locked in:** commit 044.

### Event predictor + priority queue infrastructure (commit 045)

**Date:** 2026-05-19 (commit 045)
**Question:** How does the analytic event-prediction priority queue locked in CONSTRAINTS §2 ("Mode transitions and event scheduling") and netcode contract §4.1 ("Event prediction queue") get built? Specifically: where does the queue live; what internal data structure backs it; how do predictors interact with it; how does time-warp consume it; how is the queue invalidated when vessel state changes; and how does naming avoid `UnityEngine` collisions?
**Decision:** Multi-part design landed across three stages of commit 045:
- **(a) Nullable typing for event-prediction fields.** All four `next_*_tick` fields on `KeplerState` are `long?` (nullable). NETCODE_CONTRACT §2.3 amended at commit 045 from non-nullable `SimTickCount` to `Option<SimTickCount>` for `next_periapsis_tick` and `next_apoapsis_tick`. Hyperbolic orbits genuinely have no future apoapsis; hyperbolic-post-periapsis trajectories have no future periapsis either. Nullable typing makes "no future event" explicit in the type rather than encoding it via a sentinel value.
- **(b) Queue lives on `SimTickController`** per CONSTRAINTS §2 ("Authority over the queue lives in the sim-tick controller"). Exposed as `public EventQueue { get; }`. Vessels in the Vessels asmdef access it through `SimTickController.Instance.EventQueue`, preserving the asmdef direction commit 038 established.
- **(c) Internal data structure: `SortedSet<(long tick, Guid vesselId, SimEventType type)>` + `Dictionary<(Guid, SimEventType), long>` side index.** The SortedSet provides the sorted view; the dictionary provides O(1) lookup for update/remove operations. All public operations are O(log n) or better. Alternatives considered and rejected: `SortedList<long, ...>` (unique-key constraint breaks tie cases), `SortedDictionary` (same shape as SortedSet but less idiomatic for value-typed entries), custom binary heap (more complex with no clear win at the small N expected in Phase 1).
- **(d) Tie-breaking via `ValueTuple.CompareTo`.** Two events at the same tick order by Guid then EventType (enum ordinal). Deterministic across runs because Guid ordering is value-based.
- **(e) Overflow defense via `long.MaxValue / 2` threshold in the predictor.** A predicted absolute tick that would exceed half the long range gets returned as null (treat as "no event in any reasonable timeframe"). Real orbit periods stay well within this bound; near-parabolic orbits with stretched periods hit it. The /2 leaves headroom for currentTick + Δticks addition without wrapping. A separate `int.MaxValue` clamp at the SimTickController boundary handles the long → int truncation when feeding `ComputeAnalyticIterations`.
- **(f) `SimEventType` naming to avoid `UnityEngine.EventType` collision.** The Unity name is reserved by the engine and any file that imports both `UnityEngine` and `SpaceSim.Foundation.SimTick` raises CS0104 ambiguous-reference errors. Prefixing with "Sim" eliminates the ambiguity everywhere. Discovered during the first Stage 2 verification pass (the compile error was visible in Unity Console; renamed cleanly).
- **(g) Driver pattern parallel to `VesselTransitionDriver` and `VesselSoiRerootingDriver`.** New `VesselEventPredictionDriver` static class in Vessels module subscribes to `SimTickController.TickAdvanced`. Iterates Kepler-rails vessels each tick, runs predictors, writes results to KeplerState and queue.
- **(h) Predictors are pure functions; cache lives on the schema; invalidation on state change.** Per CONSTRAINTS §2 ("Event predictors are pure functions of orbital and trajectory state. No hidden state, no caches that can drift. Cache the result against vessel state; invalidate when state changes"). `Vessel.TransitionToKeplerRails`, `Vessel.TransitionToPhysXActive`, and `Vessel.ReRootToBody` all call `EventQueue.RemoveVesselEntries` after the state mutation. The driver repopulates on the next tick for vessels still in Kepler-rails.
- **(i) No disabled-by-default flag on `VesselEventPredictionDriver`.** Unlike `VesselTransitionDriver` (which gates on Phase 0 stub state), the periapsis/apoapsis math is closed-form and produces correct results for any Kepler-rails vessel. Always-on is the right default.
- **(j) `KeplerPropagator` refactored to use `OrbitalElements` math helpers.** Stage 1 extracted `TrueToMeanAnomaly`, `MeanToTrueAnomaly`, `MeanMotion` from KeplerPropagator (where they were private) to OrbitalElements (where they're public). The Newton-Raphson solvers and Conway's-starter implementations moved with them. KeplerPropagator's `PropagateState` now calls the OrbitalElements helpers; single source of truth for the anomaly conversions.

**Alternatives rejected:**
- **Queue ownership inside Vessels module:** would invert the established asmdef direction. CONSTRAINTS §2 explicitly says the queue lives in SimTickController.
- **Sentinel values (`long.MaxValue`) for "no event" instead of nullable:** less honest typing; downstream code would need to check for the sentinel everywhere rather than getting null-check semantics from the type. Nullable is the correct shape.
- **In-place mutation of queue entries when vessel state changes:** would couple the predictor driver to the transition methods (driver would need to recompute from inside transition methods). Cleaner separation: transitions remove queue entries; driver repopulates next tick.
- **Single shared driver for SOI re-rooting + event prediction (folding into `VesselSoiRerootingDriver`):** the two concerns are operationally distinct (re-rooting changes the reference body; event prediction populates orbital event predictions). Separate drivers keep each focused.
- **Per-vessel mini-queues instead of a single global queue:** would not give `RunFixedUpdateCycle` a single "next event across all vessels" tick to read. Global queue is the right shape per the contract.
- **Two no-op-defensive tests (`UpdateToNullOnNonexistent`, `RemoveVesselEntries_UnknownVessel`):** kept in the test suite at +2 beyond the spec target because they document real contracts the implementation supports and are cheap to maintain.

**Rationale (preserves asmdef direction + extensibility hook + correctness):** the design respects CONSTRAINTS §2's locked architecture (queue owned by SimTickController, predictors as pure functions, extensibility via enum + driver), preserves the asmdef discipline (Vessels → SimTick), introduces the cleanest first predictor (periapsis/apoapsis is closed-form; no root-finding), and documents the `UnityEngine.EventType` collision so future contributors don't repeat the mistake. The nullable contract amendment aligns the documentation with what the implementation always did.

**Implication:** Future predictor commits (atmospheric entry, surface impact, SOI crossing, scheduled burn, interstellar arrival) follow the established pattern: write a `*Predictor` static class, add an enum value to `SimEventType` (already there), populate the corresponding `KeplerState` field (the `Next*Tick` fields are scaffolded), call `EventQueue.UpdateVesselEntry`. The architecture supports adding event types without architectural change. Time-warp UI and rate-scaling controls become buildable now that the queue lookup is wired into `RunFixedUpdateCycle`; the "warp to next event" UI feature from §4.1 reads from the same queue.

**Locked in:** commit 045.

### SOI crossing predictor (commit 046)

**Date:** 2026-05-19 (commit 046)
**Question:** How is the SOI crossing event predicted analytically each sim-tick so that time-warp can respect it? Specifically: what math handles outward exits vs inward child entries; how is the lookahead bounded; what's the granularity/accuracy tradeoff; how does the predictor interact with the established `VesselEventPredictionDriver` infrastructure from commit 045; and what's the failure-isolation discipline when one predictor throws?
**Decision:** Multi-part design landed across three stages of commit 046:
- **(a) Two math paths in the predictor.** Outward closed-form: solve `r(ν) = SoiRadius` using the conic equation `r(ν) = p / (1 + e·cos(ν))`. Convert the two ν solutions to mean anomaly via `OrbitalElements.TrueToMeanAnomaly`, then to tick offsets via `MeanMotion`, and take the smallest positive offset. No iteration. Inward sampled-and-refined: for each child of the current body, sample the discriminator `d(T) = |r_vessel_world(T) − r_child_world|² − r_SOI²` at coarse intervals across the lookahead horizon; on the first positive-to-negative sign flip, bisect to one-tick tolerance.
- **(b) Constant-body-position assumption acceptable for Phase 0/1.** Both math paths assume the current body's position AND each child body's position are constant across the prediction horizon. Phase 0/1 honors this (`ReferenceBody.PositionWorld` is captured once at Awake and never updated). Phase 4+ work (when bodies orbit) will require revisiting both paths to account for body orbital motion — outward becomes a vessel-conic-vs-moving-body-sphere intersection (no closed form in general); inward becomes a two-orbit relative distance function. The predictor signature does NOT change at that point; the math paths internally upgrade.
- **(c) Lookahead horizon depends on orbit type.** Elliptical (e < 1): one orbital period in ticks. Hyperbolic (e ≥ 1): `PredictorMaxLookaheadTicks` = 946,080,000 ticks (~1 game year at 30 Hz). Predictions beyond one game year are unactionable — the player will maneuver, warp elsewhere, or the scenario will shift.
- **(d) Coarse sample interval = 100 ticks (3.33 sec at 30 Hz).** Tuned for typical orbits: low enough to detect most child-SOI entries, high enough that the inner per-child loop stays cheap. Near-tangent grazes inside one sample interval are intentionally missed per the pragmatic-detection design — the driver's at-evaluation-time check (`VesselSoiRerootingDriver`) catches the actual crossing if it happens, and warp loses its pre-stop-on-event opportunity but the re-root still fires correctly.
- **(e) Earliest-crossing-wins across all children in the predictor.** Differs from `VesselSoiRerootingDriver`'s first-match-wins semantics. Both are correct in their respective contexts: the driver runs at evaluation time when the discriminator has already flipped (any child currently containing the vessel is a correct re-root target); the predictor asks "which event happens first?" and requires comparing all candidates by tick. Documented in the predictor's XML doc to prevent confusion.
- **(f) `DetectionAggressiveness` enum in the signature; Pragmatic hardcoded at commit 046 call sites.** Architecture scaffolding for CONSTRAINTS §1701's pragmatic/strict difficulty lever. Strict mode is Phase 5/6 work (adaptive sampling that shrinks the interval at high vessel speed); commit 046 behaves identically to Pragmatic. Keeping the parameter in the signature now means call sites don't churn when Strict ships.
- **(g) `BoundaryHugTolerance` = 1.0 meter** for the numerical near-circular-at-SOI edge case. If both periapsis and apoapsis distances are within this tolerance of the current body's SOI radius, the orbit is treated as numerically hugging the boundary and no outward crossing is reported. Without this guard, a circular orbit at exactly the SOI radius would falsely report a crossing every period due to floating-point rounding.
- **(h) Signature divergence from `PeriapsisApoapsisPredictor`** (takes `ReferenceBody`, not raw `mu`) is math-driven, not stylistic. The inward search requires the body reference for `BodyRegistry.GetChildrenOf` lookup and for child-position math anyway; passing `currentBody` and reading `currentBody.Mu` internally is the cleaner shape with single source of truth for μ.
- **(i) Per-predictor try/catch isolation in `VesselEventPredictionDriver.PredictAndUpdate`.** Each predictor runs inside its own try/catch. A failure in `SoiCrossingPredictor` logs an error and PeriapsisApoapsis updates still land; vice versa. The outer try/catch in `OnTickAdvanced` remains as a safety net for schema-invariant violations. Counter semantics: `EvaluationCount` counts vessels examined; `PredictionUpdateCount` counts vessels for which `PredictAndUpdate` executed to completion (i.e., no unhandled exception reached the outer catch). Individual predictor failures don't affect either counter beyond the log message.

**Alternatives rejected:**
- **Caching the children list per vessel:** premature optimization. `BodyRegistry.GetChildrenOf` is O(N) iteration with N small (single-digit in Phase 1 scenes); a reactively-maintained children-list cache on each ReferenceBody is the shape this would take when N grows, but commit 046 keeps the simpler call-it-each-time pattern.
- **Full numerical root-finding for outward:** the closed-form is exact for the constant-body-position case (which Phase 0/1 honors). Numerical root-finding would add complexity without benefit. Phase 4+ work will revisit this when bodies orbit and the closed-form no longer applies.
- **Adaptive sample interval scaling with vessel speed:** deferred to Strict mode (Phase 5/6). Adds branching to what should be a simple knob; the pragmatic constant + driver safety net is the right shape for Phase 1.
- **Raising `MaxKeplerIterations` to support high-e orbits (e > 0.95):** better solvers are future work. Phase 1 stays in the Newton-Raphson stable region (e < ~0.9 in practice). If Phase 5/6 gameplay demands higher-e transfer orbits, swap Newton-Raphson for Laguerre's method (cubic convergence; same iteration-loop structure; Conway 1986 covers it as a Newton-Raphson alternative). Markley 1995 ("Kepler equation solver" in Celestial Mechanics & Dynamical Astronomy) is a separate speed optimization — closed-form initial guess + one Newton iteration — that doesn't fix high-e but speeds up the common case.
- **Folding SOI prediction into `VesselSoiRerootingDriver`:** would conflate at-evaluation-time detection with predict-ahead lookup. Separate concerns; same data shape but different timing. The driver runs the actual re-root; the predictor populates the event queue.
- **Computing `mu` from `currentBody.MassKg * G` at the predictor call site:** the `ReferenceBody.Mu` property already does this. Using the property avoids redundant computation and keeps the single source of truth on the body.

**Rationale (preserves architecture + extensibility + Phase 0/1 scope):** the design extends the commit-045 predictor infrastructure (driver + queue + KeplerState schema fields) without architectural change — the extensibility hook is in place, commit 046 just adds the second concrete predictor. Two math paths reflect the genuinely different geometric problems (vessel-exits-sphere-around-focus vs vessel-enters-arbitrary-sphere). Per-predictor try/catch isolation honors the netcode contract's "keep event predictions current on a best-effort basis" intent — one degraded predictor doesn't poison the others' updates. The constant-body-position assumption is the largest piece of Phase 0/1 scope debt; documented loudly in XML doc, the signature stays stable across the Phase 4+ migration.

**Implication:** Commit 047 will add `AtmosphericEntryPredictor` and `SurfaceImpactPredictor` following the established pattern — pure-function predictor + per-predictor try/catch in `PredictAndUpdate` + KeplerState field population + queue entry update. The remaining `SimEventType` enum values (`ScheduledBurn`, `InterstellarArrival`) land at later phases per CONSTRAINTS §2's extensibility hook. The Phase 4+ migration of the constant-body-position assumption is a contained change to `SoiCrossingPredictor`'s internals; no callers or schema fields change. The Newton-Raphson solver-stability constraint (e < ~0.9 in practice) becomes a documented Phase 1 boundary — tests across the codebase must keep input eccentricities below this ceiling to avoid solver instability propagating into predictor results.

**Locked in:** commit 046.

### Atmospheric entry + surface impact predictors (commit 047)

**Date:** 2026-05-20 (commit 047)
**Question:** How do the atmospheric entry and surface impact event predictors get built? Specifically: what `ReferenceBody` schema additions do they need; how do they share math with the existing `SoiCrossingPredictor` to avoid duplication; how does the `NextModeTransitionTick` field aggregate multiple mode-transition event types; how does the trigger evaluator interact with the aggregated field; and what's the discipline around the existing `TransitionTriggerReason` enum value becoming imprecise post-aggregation?
**Decision:** Multi-part design landed across three stages of commit 047:
- **(a) Two new `ReferenceBody` schema fields.** `SurfaceRadiusMeters` defaults to `6.371e6` (Earth-like at 1/1 scale); `AtmosphericTopAltitudeMeters` defaults to `0.0` (vacuum body). Both are `[SerializeField]` private with public read-only properties, paralleling the `SoiRadiusMeters` pattern from commit 044. NETCODE_CONTRACT §2.7 BodyState extended with the matching schema entries.
- **(b) `AtmosphericTopAltitudeMeters <= 0` treated as "no atmosphere" (vacuum body).** The `<= 0` check defends against both the 0.0 default (Phase 1 single-body test scenes start vacuum) and accidentally negative Inspector values. `AtmosphericEntryPredictor` returns null on vacuum bodies regardless of orbit geometry.
- **(c) Threshold radii.** Atmospheric entry threshold = `SurfaceRadiusMeters + AtmosphericTopAltitudeMeters`. Surface impact threshold = `SurfaceRadiusMeters`. Both predictors delegate to the shared math helper with their specific threshold.
- **(d) Shared math helper `OrbitalElements.SolveConicAtRadius` extracted from commit 046's outward closed-form path.** Three callers now use this helper (SOI outward crossing, atmospheric entry, surface impact). Extraction motivated by the second + third callers; the first caller (commit 046) inlined the math correctly because there was no shared usage yet — premature extraction on the first caller would have been speculation. The helper's signature `(state, targetRadius, currentTick, mu, tickIntervalSeconds)` matches `PeriapsisApoapsisPredictor.Predict`'s parameter convention.
- **(e) `BoundaryHugTolerance` migrated** from `SoiCrossingPredictor`'s private constant to `OrbitalElements`'s public const so all three radius-crossing predictors share one definition (1.0 m for elliptical near-circular-at-threshold edge cases). Hyperbolic orbits don't hit boundary-hug (infinite apoapsis); the check is elliptical-only inside the helper.
- **(f) `NextModeTransitionTick` aggregates via min(atmospheric, surface).** Multiple predictors write to the same `KeplerState` field; the driver picks the earliest tick across both. Future scheduled-burn / interstellar-arrival predictors extend the aggregation to N-way (still via min) without schema change. Null when both predictors return null. The aggregation step lives outside both per-predictor try/catches so it runs even if both predictors threw (writes null cleanly, stale-value cleanup automatic).
- **(g) New predictors omit the `DetectionAggressiveness` enum parameter.** Closed-form math has no sampling granularity to tune — pragmatic and strict modes would produce identical output. The signature asymmetry with `SoiCrossingPredictor` (which has the parameter because of its inward sampled-and-refined path) is math-driven, not stylistic. CONSTRAINTS §2's "aggressively detects atmospheric entry and surface impact" commitment is satisfied automatically: closed-form returns the exact tick when a crossing exists.
- **(h) Atmospheric entry semantics: predictor returns next threshold crossing.** For vessels above the atmosphere — the gameplay-relevant case — this is entry (inbound). For vessels already inside the atmosphere (shouldn't happen on Kepler-rails; they should be PhysX-active), it would be the exit crossing. The K→P mode transition driver handles either case harmlessly: forces PhysX-active at the predicted tick regardless of crossing direction. v1 doesn't predict atmospheric exit as a distinct event type.
- **(i) `VesselTests.cs` SetUp reflection-sets `_body.surfaceRadiusMeters = 1.0` (point-mass default).** Without this, existing tests with `Eccentricity = 0.1` on LEO orbits (rPeri = 6.3e6 < 6.371e6 default surface) would trigger surface-impact predictions and break existing assertions. The 1.0 m point-mass convention is documented in the SetUp comment so future test writers see immediately that tests exercising surface-impact behavior must reflection-set finite values per-test. New schema additions on `ReferenceBody` should follow this pattern.
- **(j) `TransitionTriggerReason.AtmosphericEntryPredicted` label is now imprecise.** Post-commit-047, the trigger fires for surface impact too (because `NextModeTransitionTick` is aggregated). Rename or split is acknowledged as a cosmetic concern and deferred to a separate cleanup commit. XML doc updates on the enum value, `Vessel.IsAtmosphericEntryPredicted`, and `Vessel.EvaluateTransitionTriggers` flag the imprecision pointing at this DECISIONS entry.

**Alternatives rejected:**
- **Separate per-event-type fields on `KeplerState` (`NextAtmosphericEntryTick`, `NextSurfaceImpactTick`) instead of aggregated `NextModeTransitionTick`:** would force the trigger evaluator to compute the min itself across N nullable fields. The single aggregated field matches the field's purpose (single "next imminent mode transition" boolean signal); per-event-type fields would only matter if the trigger evaluator needed to distinguish the cause, which it currently doesn't.
- **`DetectionAggressiveness` on new predictors for API parity with `SoiCrossingPredictor`:** rejected because closed-form math has no aggressiveness behavior to differ. Signature asymmetry is math-driven (different algorithm structures), not stylistic. Future Strict-mode work on these predictors (e.g., handling moving bodies in Phase 4+) would add the parameter then.
- **`AtmosphericTopAltitudeMeters = 0` as "zero-thickness atmosphere" (predictor solves at exactly surface radius):** rejected because the vacuum interpretation is semantically clearer. Zero-thickness would make every vacuum body fire AtmosphericEntry events at the surface, which is wrong — vacuum bodies shouldn't produce atmospheric entry events at all. The `<= 0` defensive check also catches accidentally-negative Inspector values.
- **Splitting `TransitionTriggerReason` into per-event-type values now:** deferred to a separate cleanup commit. The rename mechanically touches `TransitionEvaluation.cs` enum + `Vessel.cs` callsite + tests, but conflates with the commit-047 scope (predictor implementations + schema). Cosmetic-only; the underlying mechanism works correctly.
- **Per-predictor exception-isolation throw-injection tests:** same precedent as commit 045 Stage 2 and commit 046 Stage 2 — no clean throw path exists for these predictors (null inputs return null gracefully; closed-form math doesn't throw on valid orbits). The per-predictor try/catch isolation is structural in the driver code + documentary in the XML doc, not test-covered.

**Rationale (preserves architecture + completes Phase 1 predictor set + minimal scope creep):** the design extends the commit-046 driver and schema-aggregation patterns without architectural change — `VesselEventPredictionDriver.PredictAndUpdate` gains two more per-predictor try/catches following the established pattern. Math reuse via `SolveConicAtRadius` avoids duplicating the conic-equation solve across three predictors; helper extraction post-second-caller is the right time (premature on first caller, overdue on fourth). `BoundaryHugTolerance` migration is symmetric — when math moves to a shared location, its shared constants follow. The `TransitionTriggerReason` rename is a separable concern with its own scope; deferring keeps commit 047 focused on what's actually new. The test SetUp point-mass default establishes a forward-looking convention for future `ReferenceBody` schema additions.

**Implication:** Commit 048 will land the time-warp rate machinery (continuous-warp UI + warp-rate scaling controls + warp-respects-event-tick gating per netcode contract §4.2) — with three predictor types populating the event queue, commit 048 has a complete event-prediction substrate to gate warp against. The deferred `TransitionTriggerReason` rename is a follow-on cleanup commit that mechanically updates the enum + callsite + tests; not blocked by Phase 1 milestone work. Phase 4+ migration of the constant-body-position assumption inside `OrbitalElements.SolveConicAtRadius` is a contained change affecting all three radius-crossing predictors simultaneously (since they share the helper); no caller signature changes.

**Locked in:** commit 047.

---

### IVessel abstraction for driver-test decoupling

**Date:** 2026-05-21 (operational commit, post-047)
**Question:** Should the per-tick driver→vessel coupling be lifted off concrete `Vessel` for testability? If so, which drivers; with what interface scope; and how does the abstraction interact with the existing `IActiveVessel` interface that already lives in SimTick?
**Decision:** Multi-part design landing as an operational commit:
- **(a) New `IVessel` interface in the `Vessels` asmdef.** Read-only contract with five members: `Mode`, `State`, `ReferenceBody`, `GetWorldPosition()`, `DiagnosticName`. The first four were already present on concrete `Vessel`; `DiagnosticName` is the one new member, returning `gameObject.name` (or `"(null)"` if the GameObject is missing). The property internalizes the Unity-null check so consumers don't have to repeat the ternary.
- **(b) Scope: read-only.** No mode-transition methods, no SOI re-root, no schema mutations. Mutating lifecycle operations stay on concrete `Vessel`.
- **(c) `VesselEventPredictionDriver` migrates.** `PredictAndUpdate` takes `IVessel` rather than `Vessel`; the four inner per-predictor catch blocks log via `vessel.DiagnosticName` instead of `vessel.gameObject.name` ternary (net ~12 line reduction). The outer `OnTickAdvanced` iteration stays typed against concrete `Vessel`; cast `Vessel → IVessel` happens implicitly at the `PredictAndUpdate` call site. Outer catch stays in concrete-Vessel context.
- **(d) `VesselSoiRerootingDriver` does NOT migrate.** Its inner `EvaluateAndReroot` calls `vessel.ReRootToBody(parentBody)` — a mutating method incompatible with the read-only contract. The driver stays on concrete `Vessel`. Deferred candidate: split detect/dispatch if SOI-rerooter test needs justify it.
- **(e) `VesselTransitionDriver` does NOT migrate.** Its `TransitionToKeplerRails` / `TransitionToPhysXActive` calls are even more mutating (they add and destroy Unity components). Stays on concrete `Vessel`.
- **(f) `IVessel` and `IActiveVessel` are independent interfaces.** Both declare `Mode` and `GetWorldPosition()`; concrete `Vessel` satisfies both with a single implementation each. No inheritance — `IActiveVessel` lives in SimTick (cycle-breaker), `IVessel` lives in Vessels (test-decoupler), and inheriting one from the other would force the SimTick asmdef onto the declaration site of the other interface for no operational gain.
- **(g) POCO test fakes per driver, nested-private-class.** Matches the existing `n=3` pattern (`ActiveVesselStub` in SimTickControllerTests, `StubActiveVessel` + `ThrowingActiveVessel` in VesselTests). Allows Stage 2's `FakeVessel` (stubs `GetWorldPosition` to zero — predictor driver doesn't call it) to diverge from a hypothetical Stage 3 fake (would need meaningful `GetWorldPosition` — the SOI re-rooter actually calls it). One fake per driver keeps the implementation honest about per-driver needs.
- **(h) Inner-method-only migration pattern.** Where `IVessel` is used, the outer iteration boundary stays concrete-Vessel-typed (Unity-null semantics from `VesselRegistry.Vessels` matter at iteration); only the inner per-vessel method takes `IVessel`. Catch-block diagnostics follow the same split — outer uses `vessel.gameObject.name`, inner uses `vessel.DiagnosticName`. The asymmetry is documented inline in `VesselEventPredictionDriver.OnTickAdvanced` so future readers don't try to "fix" it.

**Alternatives rejected:**
- **Full migration including outer iteration:** rejected because Unity-null semantics on the `Vessel[] snapshot` array matter at the iteration boundary (destroyed-but-not-collected MonoBehaviours register as Unity-null but not C#-null). Pushing the IVessel cast outward would either lose that semantic or duplicate the check. Inner-method-only keeps each concern in its natural typing.
- **`IVessel : IActiveVessel` inheritance:** rejected because it would force every IVessel consumer to add a reference to the SimTick asmdef (where IActiveVessel lives). The two interfaces happen to share two members but live in different asmdefs for different architectural reasons; merging them via inheritance conflates the cycle-breaking concern with the test-decoupling concern.
- **`IActiveVessel : IVessel` reverse inheritance:** rejected for the symmetric reason — SimTick's IActiveVessel cannot reference IVessel without pulling Vessels into SimTick's dependency surface, which inverts the asmdef direction and re-introduces the cycle that IActiveVessel was created to break.
- **Shared `FakeVessel` helper file in `Tests/`:** rejected because no shared test-helpers file exists in the Vessels module (n=3 existing stub patterns are all nested private classes); introducing a new pattern here for DRY value that may or may not materialize is premature. If Stage 3 ever lands a SOI-rerooter test, its FakeVessel will differ from Stage 2's (needs meaningful GetWorldPosition) and the divergence argues for nested-per-driver-test-file rather than shared.
- **Migrate `VesselTransitionDriver` to IVessel:** rejected because its `TransitionToKeplerRails` / `TransitionToPhysXActive` calls are mutating lifecycle operations that destroy and re-add Unity components. The interface either bloats to expose these (and POCO fake implementations become semantically meaningless no-ops) or stays clean and the driver stays coupled. Stays coupled.
- **Migrate `VesselSoiRerootingDriver` to IVessel:** rejected for this commit because its `vessel.ReRootToBody(parentBody)` call is incompatible with the read-only contract. Three resolution options surfaced during reconnaissance: (i) extend IVessel to include `ReRootToBody` (rejects the read-only design, makes POCO fake implementations of ReRootToBody semantically meaningless); (ii) split `EvaluateAndReroot` into detect/dispatch where detect takes IVessel and dispatch takes concrete Vessel (architecturally honest but unplanned scope expansion mid-commit); (iii) cast IVessel→Vessel inside the method (works but defeats the testability goal). Scope-locked to "not in this commit" — the SOI re-rooter detect/dispatch split is a deferred candidate refactor if test needs justify it.
- **Adding `ReRootToBody` to IVessel:** rejected because POCO test fakes implementing it would be semantically meaningless (`ReRootToBody` performs a real Unity component shape change on real Vessels; a fake's "no-op ReRootToBody" doesn't help any test). Same logic applies to `TransitionToKeplerRails` / `TransitionToPhysXActive`.
- **Splitting `EvaluateAndReroot` into detect/dispatch (for this commit):** deferred. Architecturally honest but unplanned scope expansion. Tracked as a candidate future refactor; if SOI-rerooter behavior diverges enough that POCO-fake testing becomes the right path, the split lands then with its own design conversation.

**Rationale (deliver primary value, defer compatible-but-larger scope):** the four-predictor-callsite concern that motivated this commit is addressed by Stage 2 (predictor driver migration). The interface is defined for future use; Stage 3 surfaced an architectural reality (read-only contract incompatible with SOI-rerooter mutation) that wasn't visible at Stage 1 reconnaissance. The two resolution paths for the SOI re-rooter (extend interface or split inner method) are both real design work, and bundling either into this commit would be unplanned scope expansion. Locking scope and capturing the alternatives as deferred candidates is the cleaner ship. The two driver migrations that didn't happen (transition driver, SOI re-rooter) each have their own rationale — neither is forgotten; both are documented above.

**Implication:** future driver refactors that want IVessel-style testability should default to the inner-method-only migration pattern (outer iteration stays Vessel-typed; inner method takes IVessel). The pattern generalizes cleanly to read-mostly drivers; mutation-heavy drivers need their detect-vs-dispatch concerns separated before the lift is possible, which is a separate architectural conversation per driver.

**Locked in:** operational commit (post-047).

---

### Time-warp controller architecture (commit 048)

**Date:** 2026-05-22 (commit 048 Stage 1 schema + infrastructure; Stages 2-5 wire behavior)
**Question:** How is the time-warp controller built? Specifically: rate representation (float vs rational); halt surfacing (direct controller→UI vs event bus); PhysX threshold for forced KeplerRails transition; routing for forced transitions (driver vs direct call); routine-supply classification representation; and field schema for atmospheric-entry / surface-impact distinct trigger reasons.
**Decision:** Multi-part design landing in Stage 1 (schema + infrastructure) with behavior in Stages 2-5:
- **(a) Rational warp-rate representation.** `WarpRate` is a `readonly struct` with `(long Numerator, long Denominator)` and helper factories `Paused` / `OneX` / `Discrete(level)` / `Continuous(integerRate)`. Discrete levels are {1, 5, 10, 100, 1000, 10000, 100000}; continuous integer rates are [1, 1000]. Denominator validated positive at construction. Internal `AdvanceTicks(realTimeTicks, pendingNumerator)` helper carries fractional-tick remainder forward for future fractional-rate modes; in v1 denominator is always 1, so `pendingNumerator` stays zero.
- **(b) Event-bus halt surfacing.** `WarpController` exposes `Action<WarpHaltInfo>` for halt events. UI elements subscribe independently (Mission Control, warp-rate HUD, audio cues). Matches the existing `SimTickController.TickAdvanced` pattern.
- **(c) PhysX cap at 5x — cap-and-stay, not force-transition (amended at Stage 5).** Warp rates above 5x while a vessel is `PhysXActive` are silently clamped to 5x in `WarpController.EffectiveRate`. The vessel does NOT auto-transition to `KeplerRails`; the player must transition manually, or wait for a Vessel-side transition trigger to fire (exit-atmosphere, mode-toggle command, etc.). The 5x number matches KSP-tested behavior — empirically the point where PhysX accuracy degrades enough to produce unphysical results. Pre-commit-048 the cap was 1x (via the now-replaced `SimTickWarpController`); Stage 2 raised the cap to 5x while preserving the cap-and-stay policy. Original Stage 1 sketch described force-transition; see (d) for why it was deferred.
- **(d) Force-transition deferred to a future commit; `TransitionTriggerReason.WarpRateForcedRails` lands as held infrastructure (amended at Stage 5).** Original Stage 1 sketch had `WarpController` triggering a forced `KeplerRails` transition when the player requested warp above the PhysX cap. Stage 2 reconnaissance discovered an existing `SimTickWarpController` with cap-and-stay policy already in place, and absorbing cap-and-stay was the cheaper move for commit 048: it preserves the existing UX (player chooses when to transition), keeps Stage 2's scope to "replace the float-multiplier rate machinery with a rational one," and lets force-transition land later as a discrete behavior change with its own design review. The `TransitionTriggerReason.WarpRateForcedRails` enum value remains in `TransitionEvaluation.cs` as deliberate infrastructure — it never fires in commit 048 but the wiring is in place for the future force-transition commit. When that commit lands the cap-at-5x becomes a transition trigger that flips the active vessel to `KeplerRails` (raising the effective ceiling to 10,000x) and the enum value starts firing. Per the architectural pattern preserved here, force-transition will flow through `VesselTransitionDriver` rather than calling `Vessel.TransitionToKeplerRails` directly from `WarpController` — the rejected alternative below still applies to the deferred work.
- **(e) `IsRoutineSupply` as a single boolean on `Vessel`.** Routine vessels skip warp halts on SOI crossings and atmospheric entry events (expected, repetitive, non-terminal); they still halt on surface impact (mass loss), maneuver execution, and other terminal events. Field default false. Inspector-visible.
- **(f) `NextModeTransitionTick` split into per-event-type fields.** Atmospheric entry and surface impact each get a dedicated `KeplerState` field (`NextAtmosphericEntryTick` and `NextSurfaceImpactTick`). The trigger evaluator reads both fields independently and fires distinct `TransitionTriggerReason` values (`AtmosphericEntryPredicted` for atmospheric, `SurfaceImpactPredicted` for surface). Resolves the label imprecision that existed from commit 047 onward.

**Alternatives rejected:**
- **Float warp multipliers:** precision drift over long warp sessions. A 10,000x rate held for hours accumulates sim-tick advancement errors that compound.
- **Per-vessel warp rates:** scope explosion (every vessel needs its own warp state); unclear UX (what does "warp on this vessel" mean when the player isn't looking at it?). v1 has one global warp rate.
- **Aggressive floating-origin rebasing during warp:** no precision benefit at double-precision authoritative state. The current lazy rebasing remains correct under any warp rate.
- **Tenths or finer continuous modes:** deferred to v2 if players ask. The rational `WarpRate` infrastructure supports them without schema change; v1 ships integer-only.
- **Direct `Vessel.TransitionToKeplerRails` calls from `WarpController`:** would bypass the driver pattern. Rejected to preserve architectural consistency — the driver is the single dispatch point for all mode changes.
- **Keeping `NextModeTransitionTick` as a computed aggregate property after the field split:** rejected because the prior aggregation was the source of the label imprecision being fixed. A computed-aggregate property would re-introduce the question "which event does this tick refer to?" at every reader. The single production reader split cleanly into two readers (`IsAtmosphericEntryPredicted` and `IsSurfaceImpactPredicted`); no remaining reader benefits from the aggregated view.
- **Splitting `AtmosphericEntryPredicted` into two enum values with the field still aggregated:** rejected because the trigger evaluator can't determine which event the aggregated tick referred to. Half-resolving the label imprecision would leave the structural problem in place.

**Rationale (single coherent schema-version bump + clean separation of concerns):** Stage 1 lands all the schema and infrastructure changes as one schema-version-incompatible bump, so the save format gets one migration step rather than a chain. Rational rate representation isolates the precision concern at the type level; event-bus halt surfacing isolates the UI-coupling concern; forced transitions via driver preserve the architectural mode-change discipline; the field split eliminates the label imprecision at the source rather than papering over it. Each piece composes cleanly with the others — none of them constrains the design of any other.

**Implication:** Stage 2 wires `WarpController` into `SimTickController.RunFixedUpdateCycle` for actual time-warp advancement gating, and replaces the pre-existing `SimTickWarpController` with the rational-rate equivalent (the pre-existing controller was discovered during Stage 2 reconnaissance, see (d) above). Stage 3 wires the routine-supply gating on predictor-driven halt registration: atmospheric entry and surface impact halt always; SOI crossing halts only for non-routine vessels (per `IsRoutineSupply`). Force-transition is NOT wired in Stage 3 — it is deferred to a separate future commit, see (d). Stage 4 adds the UI scaffolding (`WarpUIController` + the `TestVessels.unity` Canvas wiring: pause/resume, seven discrete rate buttons, continuous slider, clear-halt). Stage 5 adds the gap-fill test suite (determinism + forbidden-state coverage on top of the Stage 2 / Stage 3 tests), amends this DECISIONS entry to reflect the Stage 2 reshape, and lands the commit 048 artifact. Save-file migration logic for the field split lands when save/load module begins (Phase 1 deferred per `Pending decisions` below); pre-commit-048 save files using `next_mode_transition_tick` will be migrated by dropping the value (the predictors re-populate on the next tick post-load).

**Locked in:** commit 048 Stage 1 (schema + infrastructure); Stages 2-5 wire behavior in subsequent commits.

---

### Active-vessel camera-follow (commit 049)

**Date:** 2026-05-23 (commit 049)
**Question:** How should the Phase-1-validation camera in `TestVessels.unity` track the active vessel without extending the `IActiveVessel` interface or coupling the camera utility into the Vessels asmdef?
**Decision:** Debug-utility MonoBehaviour (`ActiveVesselCameraFollow`) lives in `SpaceSim.Foundation.SimTick`. In `LateUpdate` it reads `SimTickController.Instance.ActiveVessel.GetWorldPosition()`, converts to local via `FloatingOriginManager.Instance.WorldToLocal(WorldPosition)`, and assigns `transform.position = localPos.Value + _offset`. Rotation untouched. Inspector `_offset` Vector3 with default `(0, 5, -20)`. Null-safe at every level (controller, active vessel, origin manager) with once-per-lifetime warnings matching `SimTickController.Step6_DetectModeTransitions` discipline.
**Alternatives rejected:**
- **Path B — register the camera as a `FloatingOriginAnchor` and follow the active vessel's `Transform` directly.** Would require extending `IActiveVessel` with a `Transform` accessor (cross-asmdef API change) or casting to concrete `Vessel` (defeats the interface). Unjustified cost for a debug-only camera; defeats the narrow-interface design rationale already documented on `IActiveVessel`.
- **Camera rotation tracking / `LookAt`** — a debug camera that snaps rotation can disorient. Position-only follow is the conservative default; the scene author sets the look direction once at Inspector time and the follow logic preserves it.
- **Smoothing / damping / lerp** — snap-follow is fine for visual validation; smoothing introduces visible lag and complicates the "did the origin shift mid-frame?" diagnostic by hiding the discontinuity test wants to see.
- **Vessel-switch input / hotkey** — out of scope (no active-vessel-switch UI exists yet per `docs/phase1_validation_readiness.md` System 12). The camera follows whichever vessel `SimTickController.SetActiveVessel` was last called with; switching is a separate future commit.
**Rationale (narrow utility, single conversion path):** `FloatingOriginManager.WorldToLocal` is the same `CoordinateMath.WorldToLocal` math that `FloatingOriginAnchor` implicitly relies on across shifts. One conversion source-of-truth means mid-frame origin shifts (which fire synchronously inside Step 6 before `LateUpdate` runs the same frame) are handled correctly without the camera needing to register as an anchor itself. No architectural pillar moves: `IActiveVessel` interface unchanged; asmdef direction `Coordinates ← SimTick ← Vessels` preserved; production gameplay camera (orbit / chase / cinematic modes, mouse + scroll input, FOV control, damping) is Phase 3 work and replaces this utility when it lands.
**Implication:** Phase 1 visual validation unblocked. The vessel stays centered in view across floating-origin shifts during Play, removing the dependency on Canvas diagnostic UI for visual feedback. First of three commits identified in `docs/phase1_validation_readiness.md` Section C; the remaining two (multi-body Play scene, Phase 1 validation milestone artifact) are independent commits.
**Locked in:** commit 049.

---

### Multi-body TestVessels.unity with Moon + SOI-crossing test vessel (commit 051)

**Date:** 2026-05-23 (commit 051)
**Question:** How to enable Play-mode validation of SOI re-rooting (commit 044), SOI-crossing halt (commit 046), and atmospheric-entry halt (commit 047) without violating the single-canonical-scene project pattern established by `e27adac operational: scene cleanup - codify TestVessels canonical`.
**Decision:** Extend `TestVessels.unity` with Moon as Earth's child reference body at world position `(0, 3.844e8, 0)` (real Moon-Earth distance on +Y axis), bump Earth's `atmosphericTopAltitudeMeters` from 0 to 100,000 (100 km Kármán line), and duplicate `TestVessel` to add `TestVessel_SoiCrossing` positioned at `(2.0e7, 3.17765e8, 0)` with `_initialVelocity = (0, 1000, 0)` — a tangential chord trajectory through Moon's SOI sphere. Real Moon parameters used throughout (mass 7.342e22 kg, SOI radius 66,183 km, surface radius 1,737.4 km). Camera continues to follow the original `TestVessel`; SOI events are validated via `WarpUI` halt-info text rather than visual observation of the second vessel.
**Multi-vessel toggle infrastructure added to `TestVesselDriver`:** three new `[SerializeField] bool` flags plus one `[SerializeField] PhysicsMode` field — `_setAsActiveVessel`, `_writeDiagnosticLabel`, `_handleSpaceKeyToggle` (all default `true`), plus `_initialMode` (default `PhysicsMode.PhysXActive`). The three booleans preserve single-vessel scene behavior with zero reconfiguration and are unchecked on `TestVessel_SoiCrossing` so it doesn't race the canonical driver. Without these, the duplicate's Hierarchy-order-later position causes its `Start` to fire after the original's, winning the `SimTickController.SetActiveVessel` race (camera tracks the wrong vessel; floating-origin chases the wrong vessel; warp ceiling driven by the wrong vessel's mode). Similar races on the shared diagnostic-label `Text` reference and the Space-key handler. The toggles also defend the specific failure mode where a Space-key press would toggle BOTH vessels' modes, dropping `TestVessel_SoiCrossing` from `KeplerRails` to `PhysXActive` (which caps warp at 5×) and stalling the SOI test in mid-flight. **`_initialMode` closes the workflow gap the three-toggle resolution itself opens:** with `_handleSpaceKeyToggle` unchecked, the duplicate has no path to `KeplerRails` from clean Play state — but `VesselSoiRerootingDriver.OnTickAdvanced` skips non-Kepler vessels, so the SOI test cannot complete unless the duplicate starts on rails. `_initialMode = KeplerRails` on the duplicate triggers a `Vessel.TransitionToKeplerRails()` call immediately after `Vessel.Initialize(..., PhysicsMode.PhysXActive)` completes — the transition reads the rigidbody's just-applied initial velocity and synthesizes the orbital elements from current state, which is the correct primer for a tangential SOI-crossing trajectory.
**Alternatives rejected:**
- **Parallel `TestSoi.unity` scene:** violates the e27adac canonical-scene consolidation pattern; doubles scene-maintenance overhead (duplicate Canvas + WarpController + camera-follow wiring).
- **Single test vessel with bumped velocity reaching Moon:** defeats existing `TestVessel`'s role as regression baseline for single-body behavior (camera-follow, floating-origin, time-warp UI). Also requires precomputing the Hohmann-transfer velocity, more involved than the spec's purposes warrant.
- **Active-vessel switching UI to enable visual SOI observation:** scope creep relative to Phase 1 validation; the WarpUI halt-info text is sufficient observation channel. Deferred to a later polish commit if visual SOI demonstration is wanted.
- **Toy Moon parameters for simpler trajectory math:** creates inconsistency with future `PhysicsConstants` extraction (real values are pre-approved for that work). Phase 1 is foundations validation; "the math works at real scale" is the higher-confidence assertion.
- **`Set as Active Vessel` single-toggle resolution (without `Write Diagnostic Label` or `Handle Space Key Toggle`):** Race 2 (shared diagnostic Text) and Race 3 (shared Space-key handler) are bugs, not features. Race 3 specifically: Space-key cross-toggling un-modes the SOI-crossing vessel via the PhysX 5× warp cap; the SOI test scenario becomes non-completable without isolated Space-key control. The three-toggle resolution is required infrastructure, not optional polish.
- **Hierarchy-order hack to preserve race 1's correct outcome:** fragile (any Hierarchy reorder breaks it); not self-documenting; relies on Unity's deterministic-but-not-guaranteed sibling-order execution. Inspector toggles make the pattern explicit and durable.
- **Separate `TestSoiVesselDriver.cs` for the duplicate:** ~50 LOC of parallel driver code creating a maintenance burden parallel to what the audit's `VesselSnapshot` consolidation is trying to solve elsewhere.
- **Disabling `TestVesselDriver` on the duplicate:** would prevent `Vessel.Initialize()` from being called; the duplicate would sit inert as an unconfigured `Vessel`.
**Rationale (pattern-aligned + durable + race-free):** Real Moon parameters align with pre-approved `PhysicsConstants` values. Tangential trajectory through Moon SOI demonstrates two re-rooting events per Play session (inbound + outbound) without surface impact contamination — the SOI test vessel passes ~20,000 km from Moon's center, well outside Moon's 1,737 km surface. Moon on +Y axis preserves the existing `TestVessel`'s +X trajectory as clean regression baseline (vessel coasts in +X at 10 km/s, never intersects Moon SOI regardless of warp rate or session duration; the existing trajectory math holds at all real-time scales because the +X line and +Y Moon are orthogonal). The three multi-vessel toggles become durable infrastructure for any future scene that introduces multiple `TestVessel` instances — naive Hierarchy-duplication produces silently-wrong behavior; explicit toggles make the canonical-driver pattern self-documenting.
**Implication:** Phase 1 validation of SOI re-rooting + SOI-crossing halt is now end-to-end demonstrable in Play. `TestVessel` continues to validate single-body behavior. SOI events surface in `WarpUI` halt-info text. **Atmospheric-entry halt validation deferred:** the scene now supports it (Earth atmosphere is 100 km non-zero), but neither this commit's vessels has a trajectory that intersects the atmospheric boundary. A future trajectory-authoring commit (likely 052 milestone artifact, or a third test vessel) will demonstrate the atmospheric-entry path. Second of three commits closing Phase 1 validation per `docs/phase1_validation_readiness.md` Section C.
**Locked in:** commit 051.

---

### Phase 1 closure — doc reconciliation + Phase 2 scope + save format technology (commit 055)

**Date:** 2026-05-24 (commit 055)
**Question:** Three reconciliations gating Phase 1 closure + Phase 2 entry: (D1) which doc is canonical for Phase 2 scope when PHASE_TRACKER and CONSTRAINTS disagree; (D2) how CONSTRAINTS §9 Phase 1 milestone language should read given the validation arc closed at commit 054; (D3) is save/load sim-state implementation a Phase 1 closure prerequisite or a parallel track to Phase 2.
**Decision:** Three locked decisions.

**D1 — CONSTRAINTS.md is canonical for Phase 2 scope; PHASE_TRACKER.md reconciled to match.** CONSTRAINTS §9 puts the per-planet procgen 14-stage pipeline (Layer 5) at Phase 2 as Track B alongside vessel construction Track A; PHASE_TRACKER had drifted to put the same pipeline at Phase 4. CONSTRAINTS was settled through commit 025 (per PHASE_TRACKER's Phase 0 remaining-work checklist) — design lock. PHASE_TRACKER is the operational tracker that drifted post-commit-025 without an intentional refactor. Reconciliation lands at this commit: PHASE_TRACKER Phase 2 section restructured to two-track form (Track A vessel construction + Track B per-planet procgen with explicit 14-stage Layer 5 reference + feature layer architecture + hand-tuned home system bodies + seed function backward-compatibility versioning); PHASE_TRACKER Phase 4 section rewritten to visuals-only scope per CONSTRAINTS (PBR, atmospheric scattering, volumetric clouds, terrain rendering, engine effects, re-entry plasma + procgen variation per part category). Phase progression list updated to match.

**D2 — CONSTRAINTS §9 Phase 1 milestone language amended to middle reading.** The original strict reading ("placeholder cube launches from a planet surface, reaches orbit, transfers to a moon, captures into orbit, lands") would have required Phase 3 work to close Phase 1 — thrust, atmospheric ascent, capture burn, landing are all flight-integration scope. The validation arc at commit 054 demonstrates the engine substrate Phase 1 actually delivers: vessel placed on Kepler-rails orbit propagates through multi-body SOI transitions under time-warp, returns to PhysX-active mode over surface; no thrust, no atmospheric flight, no controls. The integrated cube-to-moon flight scenario is Phase 3 work by design. Amended CONSTRAINTS §9 language reflects this scope decision; the strict-reading language is preserved in the amendment text as historical reference.

**D3 — Save/load sim-state implementation is parallel track, not Phase 1 closure prerequisite.** CONSTRAINTS §10 flagged save format technology as a Phase 1-by-deadline open question; lean was JSON for early development. Decision locked at this commit: **JSON for early development** (readable, debuggable, larger; binary alternative deferred to v2 if performance argues for it). Sim-state save/load IMPLEMENTATION is parallel-track per the framing PHASE_TRACKER has used for save/load throughout — develops alongside Phase 2 Track A (vessel construction) work without blocking Phase 2 entry. Format technology decision is the Phase 1 closure item; implementation is parallel work. Schema hooks added at commit 048 stage 1 (per-predictor `KeplerState` fields, `IsRoutineSupply` flag, rational `WarpRate`) are in place ready for the format work.

**Alternatives rejected:**
- **PHASE_TRACKER wins on D1 (procgen at Phase 4):** would have implied an intentional refactor post-commit-025 that has no DECISIONS entry capturing the move. CONSTRAINTS is the design lock; drift gets reconciled toward the canonical doc, not away from it.
- **Strict reading on D2:** would have effectively merged late-Phase-1 with early-Phase-3, making the Phase 1 → Phase 2 transition ambiguous. The middle reading scopes Phase 1's validation to the engine substrate the foundation phase actually delivers; the integrated flight scenario gets its proper home in Phase 3.
- **Loose reading on D2 (components-validated):** would have left Phase 1's milestone language too soft to gate progression. The middle reading is specific enough to verify (vessel-on-rails through multi-body SOI under warp + return to PhysX) while still scoping to engine substrate.
- **Strict reading on D3 (Phase 1 needs save/load implementation):** would have blocked Phase 2 entry on multi-session save/load implementation work that PHASE_TRACKER's existing "parallel track" framing explicitly does not require. The format technology decision is the substantive Phase 1 closure item; implementation can develop in parallel.
- **Binary save format from day one:** premature optimization; JSON for early development gives readable/debuggable save files at the cost of larger file size, which is the right tradeoff at this stage. Binary remains a future option without architectural change.

**Rationale (reconcile to design lock + scope honestly + parallel-track explicitly):** D1 makes the canonical doc the source of truth; PHASE_TRACKER is operational, CONSTRAINTS is design — reconciliation flows from operational toward design. D2's middle reading honors what Phase 1 actually validates without overclaiming flight integration that's Phase 3's job. D3 locks the cheapest Phase 1 closure item (format decision) and explicitly frames the parallel track to prevent the Phase 1 → Phase 2 transition from blocking on multi-session save/load implementation work.

**Implication:** Phase 1 closes substantively at commit 055 (this commit). The 049-055 closing block (camera-follow utility, validation readiness audit, multi-body scene + infrastructure, validation-incomplete artifact, three-stage bug fix arc, validation milestone artifact, this doc reconciliation + push) is complete and pushed to origin/main. Phase 2 (Vessel construction Track A + per-planet procgen Track B per CONSTRAINTS §9) is unblocked; entry-point decision (Track A first, Track B first, or mixed) is a fresh-session question. Save/load sim-state implementation can land any time as parallel work; not blocking. Trigger evaluator full activation remains deferred per commit 043's `Enabled = false` design until upstream Phase 3+ state systems exist. Scene drift on `TestVessels.unity` (TestVessel_SoiCrossing accidentally deleted mid-session during the 053 bug-fix work) stays as uncommitted working-tree state per option γ from the validation-pivot decision; restoration is queued for a future hygiene commit and doesn't block Phase 2 entry because the scenarios that depend on the duplicate vessel are validated via component-level EditMode coverage per commit 054's methodology.

**Locked in:** commit 055.

### Phase 1 closing audit response — IsRoutineSupply location + audit persistence policy + Newton-Raphson helper resolution (commit 057)

**Date:** 2026-05-25
**Question:** Three items surfaced by the 2026-05-24 producer audit (and audit-feed cross-references to audits 004 and 006) needing locked decisions before Phase 2 entry: (D1) `IsRoutineSupply` field location given save/load implementation is parallel-tracked per commit 055 D3; (D2) producer-audit output persistence + the audit-folder convention inconsistency between tech-director audit-file placements; (D3) Newton-Raphson eccentricity helper outstanding-item status given commits 053-stage1 and 056-item-5 both landed.
**Decision:** Three locked decisions.

**D1 — `IsRoutineSupply` moves to `VesselAuthoritativeState`.** This entry SUPERSEDES the field-location decision in DECISIONS.md commit 048 entry (e), which originally placed `IsRoutineSupply` as a single boolean on `Vessel` MonoBehaviour. That decision predated the explicit save/load schema work in commit 055 (D3), which made the question "is this save/load-persistent state?" architecturally load-bearing. With the answer "yes, it persists," the original placement is wrong by current architectural standards. The decisive frame is "does the player's marking of a vessel as a routine supply run persist across save/load?" and the gameplay answer is yes — a player who marks an Earth↔Moon supply lane as routine expects that designation to survive reload; making them re-mark every load would be hostile UX and would defeat the gating-the-SOI-crossing-halt purpose for any long-running supply operation. Persistent state belongs on `VesselAuthoritativeState`, not on the `Vessel` MonoBehaviour. The migration landed at commit 057a (`adbcb96`): field declaration moves from `Vessel.cs` to `VesselAuthoritativeState.cs`, `VesselEventPredictionDriver`'s per-tick reader updates to `vessel.State.IsRoutineSupply`, `IVessel.IsRoutineSupply` interface declaration removed (passthrough property on `Vessel` rejected at the architectural-principle level — boundary enforcement at API surface, not just at storage), POCO fake in `VesselEventPredictionDriverTests.cs` updated, 4 test sites updated.

**D2 — Producer-audit output persists to `docs/automation/producer_audits/`.** Symmetric to tech-director audit persistence. Audit-folder convention standardized in the same commit: subdir-with-date-only-filenames is canonical (e.g. `docs/automation/producer_audits/2026-05-25.md` and `docs/automation/tech_director_audits/2026-05-21.md`). The existing loose `docs/automation/tech_director_audit_2026-05-21.md` at the automation root is moved into `docs/automation/tech_director_audits/2026-05-21.md` to match. The producer-audit scheduled-task file (`SKILL.md` shipped to the task) gets a one-line amendment naming the canonical write path so future runs don't re-deliberate. Existing audit-pipeline outputs (previously untracked on disk) are added to git at this commit, closing the producer-audit-005 finding that audit-folder infrastructure was uncommitted.

**D3 — Newton-Raphson eccentricity helper marked resolved.** `AssertSolvableEccentricity` landed at commit 053-stage1 in `VesselTestHelpers.cs`; eleven inline `Assert.Less(e, 0.8, ...)` call-site migrations landed at commit 056-item-5 across four test files. Both halves of the originally-flagged outstanding item are done. The audit feed's continued surfacing of this as outstanding is stale tracking input, not a real outstanding item. Future helper-related items (additional call sites in newly-added test files, different helpers for different convergence properties) get tracked separately under their own names when they surface.

**Alternatives rejected:**

- **Passthrough property on `Vessel` MonoBehaviour with field actually on State (D1).** Defensible at the storage level but hides the architectural boundary at the API surface. Callers can still write `vessel.IsRoutineSupply` and the field appears to belong to Vessel. The whole point of D1 was the architectural correction; the passthrough does half the work. Rejected in favor of removing `IsRoutineSupply` from `IVessel` entirely and routing callers through `vessel.State.IsRoutineSupply`.
- **Ratify commit 048 entry (e) rather than supersede (D1).** Defensible only if the gameplay answer to "does this persist?" is no. The answer is yes. Rejected.
- **Defer `IsRoutineSupply` migration to the save/load implementation session (D1).** Save/load is parallel-tracked with no committed schedule per commit 055 D3 — it can land any time from "next week" to "after Phase 2 Track A ships." Pinning the schema location to a wandering schedule risks Phase 2 work building on top of `IsRoutineSupply`-on-`Vessel`-MonoBehaviour, then needing a mid-Phase-2 migration when save/load eventually lands. The clean break between Phase 1 closure and Phase 2 entry is the right window: engine code is paused, no callers are mid-flight, the migration is single-architectural-concern. Doing it now costs one focused commit; doing it later costs a migration plus rework of whatever Phase 2 code accumulated against the wrong location.
- **Producer-audit output stays chat-only (D2).** Chat-only loses the audit text on chat-context turnover and prevents week-over-week comparisons against durable history. No advantage over file persistence.
- **Accept the existing tech-director audit-folder inconsistency rather than standardizing while touching the policy (D2).** Standardizing costs one file move plus the producer-audit folder creation. Encoding the inconsistency means every future audit-related decision starts by re-asking "which placement do we use?" Cheap to fix now; expensive to fix later.
- **Continue tracking the Newton-Raphson helper as outstanding (D3).** Both halves of the originally-flagged item are done. Continued tracking is noise that drowns out real outstanding items in audit output.

**Rationale (lock decisions while reasoning is fresh; eliminate downstream re-deliberation cost):** D1 locks schema before Phase 2 builds against it, eliminating mid-Phase-2 migration risk; explicit supersession of commit 048 (e) preserves the historical context (the original decision wasn't wrong-for-its-time but predates the architectural framework that makes its location wrong now). D2 makes audit infrastructure discoverable + persistent + conventionally consistent in one pass — the asymmetry between producer-audit and tech-director persistence becomes a symmetric convention; the existing tech-director placement inconsistency resolves at the same time; existing untracked audit outputs land in git so the convention is actually realized in the repo state, not just declared. D3 closes a tracking item that's already actually done so future audits don't keep surfacing it.

**Implication:** Commit 057a (`adbcb96`) implements D1 as a focused schema migration. Commit 057b (this commit) implements D2 audit-folder consolidation (`tech_director_audit_2026-05-21.md` moved into `tech_director_audits/`, `producer_audits/` subdir created, README.md placed, existing audit outputs added to git), D3 resolution captured here, plus the five fixer-bot prompts from audits 004 and 006: `WarpController.cs` STAGE 2 SCOPE block refresh (scene wiring landed at commit 048 Stage 4), `CoordinateMath.cs` G doc-comment refresh acknowledging `PhysicsConstants` exists, `MoonSoiRadiusMeters` mixed-precision migration in `OrbitalElementsTests.cs` and `SoiCrossingPredictorTests.cs` plus matching cleanup of the now-resolved "pending" comment in `PhysicsConstants.cs:49`, `engine/STATUS.md` rewrite to pointer-to-PHASE_TRACKER form, `NETCODE_CONTRACT.md` §2.7 sentinel-convention sentence (documentation-as-acceptance, not contract-implementation alignment — the contract specifies `Option<BodyID>` semantics; the implementation uses `Guid` with `Guid.Empty` sentinel; both are correct in their respective domains; documented explicitly so future readers see the choice was deliberate, not overlooked). `.gitignore` extended with `diff_*.txt` and `*_draft.md` patterns. Test count unchanged at 346 EditMode + 6 PlayMode = 352 green throughout the 057a/057b arc. Producer-audit `SKILL.md` amendment is out-of-band work (applied to scheduled-task spec outside the repo).

**Locked in:** commit 057b.

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
