# Procgen design

**Purpose:** lock the design philosophy and body-state schema shape for Phase 2 Track B (per-planet procgen) before stage-specific algorithm specifications begin. This document establishes the structural foundations that subsequent stage specs build against.

**Locked by:** commit 059.

## Scope

059 locks three things:

1. **Design philosophy.** What kind of procgen system this is at the conceptual level (physics-not-gating, coherence-not-content).
2. **BodyState schema SHAPE.** The structural pattern that holds a body's full state — composition-all-the-way-down, sealed POCO, named-nullable sub-objects, no inheritance hierarchy, no feature dictionary. The shared flat fields are enumerated; the specific sub-object decomposition (names, granularity, what's-one-subsystem-vs-two) is NOT locked.
3. **Schema-vs-code principle.** What the schema is responsible for (state) and what code is responsible for (logic, activation rules, cascade order).

059 does NOT lock:

- Specific sub-object decomposition — what sub-objects exist, their names, their granularity. Subsequent commits (060+) design each sub-object individually, including whether two candidate subsystems should be one sub-object or two.
- Sub-object internal contents (what fields live inside each sub-object).
- Pipeline cascade structure (which stages run in what order; how each stage's outputs feed the next).
- Reconciliation of the parallel pipeline designs in `CONSTRAINTS.md §6` and `docs/world/STATUS.md` (see "Existing pipeline language" below).
- Galaxy-level Layers 1-4 (Phase 7 scope).
- Surface-detail Layer 6 (procgen runtime concern).
- Rendering, terrain LOD, atmospheric scattering shaders (Phase 4 visuals).

## Design philosophy

### Physics-not-gating

Procgen simulates physical processes; physics simulation drives outcomes. Gameplay progression isn't gated by narrative unlocks or arbitrary level-design barriers. A player who builds a rocket capable of reaching a destination CAN reach that destination; the destination's properties are whatever physics says they are.

This is opposed to gating-driven design where bodies' accessibility, difficulty, or properties are tuned to fit the player's progression curve. A new player encountering an extreme-pressure Venus-equivalent finds it hostile because Venus-equivalent atmospheres are hostile (8 MPa surface pressure, sulfuric acid clouds, 460°C surface temperature) — not because the procgen tuned it for difficulty appropriate to early-game skill levels.

The home-system bodies (home planet, home moon, Mars-equivalent, Saturn-equivalent) are hand-tuned PARAMETERS through the same pipeline that produces galaxy-beyond bodies. The hand-tuning chooses parameters that produce specific desired outcomes — an Earth-equivalent home planet, a Mun-equivalent moon — but the bodies still obey the same physical-coherence constraints as procgen-generated bodies.

### Coherence-not-content

Bodies are coherent because their parameters obey physical relationships, not because they're hand-authored. Atmospheric composition follows from mass, temperature, and stellar flux. Geological activity follows from age, composition, internal heat, and tidal heating. Biomes follow from atmosphere, temperature, water, and stellar UV. Each property has a physical reason for existing in its specific form.

This is opposed to content-driven design where each body is hand-authored with arbitrary parameter choices and visual aesthetics. Content-driven procgen produces bodies that look interesting individually but feel arbitrary in aggregate (a player notices that planets don't actually relate to their stars or to each other).

Coherence emerges from constraint propagation through the pipeline. A planet whose core parameters say "low geological activity" can't have its grammar rules produce active volcanism — the volcanism subsystem wouldn't populate. Constraint propagation IS the coherence mechanism; the schema doesn't enforce constraints, the pipeline does.

### Why these two principles together

Physics-not-gating is about WHAT gates gameplay (physics, not authored unlocks). Coherence-not-content is about HOW bodies get their parameters (constraint propagation, not authored values). Together they describe a procgen system where the player's engineering work meets a physically-real universe; bodies are interesting because they're physically interesting, not because a designer tagged some as interesting.

The home system is a hand-tuned instance of this — the same coherence machinery, with parameter values chosen rather than seeded.

## Existing pipeline language

Two documents in the current repo enumerate "14 stages" of per-body procgen, with substantively DIFFERENT stage breakdowns:

- `CONSTRAINTS.md §6` "The 14-stage pipeline (Layer 5)" enumerates parameter-focused stages (stellar context → planetary core → orbital/rotational → thermal profile → atmosphere → hydrosphere → magnetosphere/geology → terrain → biomes → resources → special features → anomalies → hand-placed features → detection signatures).

- `docs/world/STATUS.md` "Generation stages (working order)" enumerates rendering-focused stages (system layout → per-body classification → base geometry → coarse elevation → ocean masking → climate computation → biome assignment → fine terrain detail → craters → surface texturing → cloud layer → atmospheric scattering → surface feature placement → metadata generation).

The two pipelines are not manifestly the same pipeline at different levels of abstraction; they enumerate substantively different stage progressions. They might be (a) two views of one pipeline with parameter-generation upstream and rendering-side downstream, (b) two independent pipelines for different concerns, or (c) two designs that drifted apart and need to be reconciled into one canonical pipeline.

**059 does not pick.** The body-state schema this document locks is upstream of the pipeline choice — schema describes what a body IS, pipeline describes how it gets there. The schema works whichever pipeline reading turns out canonical. Pipeline reconciliation is deferred to a future commit. Both §6 and STATUS.md remain in the repo unchanged at 059; future reconciliation will retire or unify them.

## BodyState schema shape

**LOCKED:** `BodyState` is a single sealed C# class containing flat shared fields plus nullable domain sub-objects plus named-nullable feature subsystems. No inheritance hierarchy. No polymorphic feature dictionary. Per-body type asymmetries (star vs planet vs moon vs asteroid) are expressed by WHICH sub-objects are populated, not by class type.

### Shared flat fields — locked at 059

Ten physics-universal fields, present on every body regardless of type. These are the universal physical properties any body in space has; their inclusion isn't a design decomposition decision.

```
BodyState (sealed, POCO):

  BodyId         : Guid           // Stable across save/load.
  Seed           : <seed type>    // Per-body seed for deterministic regeneration.
                                  // Concrete seed type locked alongside Stage 1
                                  // implementation; semantics per 058 seed
                                  // versioning mechanism.
  Name           : string         // Player-facing name; hand-tuned for home system,
                                  // procgen for galaxy-beyond.
  Position       : WorldPosition  // Galactic double-precision coordinates.
  ParentBodyId   : Guid           // Empty for top-level body (star at root of its system).
  Mass           : double         // kg.
  Radius         : double         // m. For stars, the photospheric radius.
  SoiRadius      : double         // m. PositiveInfinity for top-level bodies.
  RotationRate   : double         // Radians per second. Intrinsic; applies to every
                                  // body including top-level stars.
  AxialTilt      : double         // Radians. Intrinsic.

  // ... domain sub-objects + feature subsystems + signatures (structure below)
```

### Schema structure — locked at 059, specific decomposition NOT locked

The schema's structure beyond the shared flat fields is locked at 059. The decomposition (specific sub-object names, granularity, what's-one-subsystem-vs-two) is NOT locked — it's per-commit design work for subsequent commits.

```
BodyState (continued from above):

  // Domain sub-objects: nullable references to per-subsystem state types.
  // Sub-object presence/absence expresses body-type asymmetry (stars don't have
  // atmospheres; gas giants don't have rocky-surface state; airless bodies don't
  // have hydrosphere; etc.).
  //
  // Specific sub-objects (their names, their granularity, whether two candidate
  // subsystems are one sub-object or two) are designed in subsequent commits
  // (060+). Examples of design questions deferred to those commits:
  //
  //   - Stellar state: one sub-object covering luminosity + spectrum + magnetic
  //     activity together, or separated by physical process?
  //   - Surface state: composition/activity vs elevation/features as one
  //     sub-object or two? (Different physical concerns but tightly coupled.)
  //   - Atmospheric subsystems: pressure/composition as one sub-object, or split
  //     by static profile vs dynamic weather?
  //   - Orbital state: separate from the body's intrinsic rotation (locked above
  //     as flat fields) but containing tidal-lock-state which is a rotation-orbit
  //     relational property.
  //
  // The schema structure is: named-nullable sub-object references on BodyState.
  // The list of which sub-objects exist is filled in iteratively.

  [Domain sub-objects — list designed per-commit going forward, not at 059]

  // Feature subsystems: named-nullable references to per-feature state types.
  // Same structural shape as domain sub-objects, distinguished primarily by
  // which subsystems are "always-applicable-in-principle but null-when-absent"
  // (domain) vs "applicable only under specific physical conditions"
  // (feature). The line between domain and feature isn't sharp; some sub-objects
  // may move category as design progresses.
  //
  // Specific feature list is designed in subsequent commits. Design questions
  // deferred include:
  //
  //   - Is extreme weather a feature subsystem or part of the atmospheric
  //     subsystem?
  //   - Are volcanism and cryovolcanism two distinct feature subsystems, or
  //     one subsystem with a working-fluid parameter?
  //   - Are debris rings a feature subsystem (per CONSTRAINTS §6's framing) or
  //     part of an orbital-debris-field subsystem that applies to both bodies-
  //     with-rings and asteroid clusters?

  [Feature subsystems — list designed per-commit going forward, not at 059]

  // Detection signatures: a sub-object representing what detection instruments
  // would observe (per-wavelength signatures). Likely its own sub-object since
  // signature data is structurally distinct from physical state. Exact shape
  // designed in a subsequent commit.

  [Detection signatures sub-object — structure designed per-commit going forward,
   not at 059]
```

### Why this shape

1. **Composition all the way down.** No inheritance hierarchy. Body-type asymmetries are expressed by sub-object presence/absence, not by class hierarchy. This matches the existing Foundation/ codebase precedent — zero inheritance hierarchies in current production code; composition + nullable references for variants.

2. **No feature dictionary.** Each feature subsystem is a named nullable field rather than a `Dictionary<FeatureKind, FeatureState>` entry. Polymorphic feature state would require an inheritance hierarchy for FeatureState subtypes, which we reject for the same reasons we reject body-level inheritance. Named-nullable gives strong typing per feature and additive growth (new field per new feature, no central dictionary to maintain).

3. **Sub-objects represent real physical subsystems, not pipeline-stage outputs.** A subsystem is a subsystem because bodies have it as a physical reality; the procgen pipeline that produces a subsystem's state is upstream of the schema and doesn't dictate schema shape. If the pipeline reorganizes (e.g., the §6 vs STATUS.md reconciliation), the schema is unaffected.

4. **Schema captures STATE, not PRODUCTION.** See "Schema-vs-code principle" below.

### Asymmetry by sub-object presence — illustrative

The shape's value comes from how it handles body-type variation through sub-object presence/absence. Once the specific sub-objects are designed (in 060+), the structure for each body type looks like:

- A star: emission-related sub-object(s) populated; orbital sub-object populated only if in a binary system (top-level stars have it null); rocky-surface and atmosphere/hydrosphere/biomes sub-objects null.
- A rocky planet: orbital sub-object populated; surface-related sub-object(s) populated; atmosphere sub-object populated or null (airless bodies); hydrosphere/biomes/magnetosphere populated per body's parameters; relevant feature subsystems populated per body's parameters; emission sub-object null.
- A gas giant: orbital sub-object populated; atmosphere sub-object populated; relevant feature subsystems populated per body's parameters (rings if applicable); no rocky-surface state, no hydrosphere, no biomes; emission sub-object null.
- An asteroid: orbital sub-object populated; minimal rocky-surface state; everything else null.
- A brown dwarf: emission sub-object(s) populated (low luminosity); orbital sub-object populated or null; possibly magnetosphere-shaped state; possibly atmospheric-shaped state. Hybrid object that composition handles gracefully — a strict star-vs-planet class hierarchy would struggle to classify it; composition just populates whatever sub-objects apply.

The brown dwarf case is the strongest illustration of why composition over inheritance: real astrophysics has body-type edge cases that don't fit a clean binary. Composition treats every body as "whatever sub-objects are populated" without forcing a type choice.

## Schema-vs-code principle

**LOCKED:** The schema describes what a body IS. Code describes how a body gets its state, when activation rules fire, what anti-correlation rules apply, what cascade order produces what.

Specifically:

- **Activation rules** (when does a feature subsystem populate? when is a sub-object null vs non-null?) live in the procgen pipeline code. The schema only captures the result: a null sub-object means the body lacks that subsystem; the rule that decided absence is procgen logic.

- **Anti-correlation rules** (volcanism + heavy rings dampened; recent impact dampens stable atmosphere) live in the procgen pipeline code. The schema can hold any combination of sub-objects the pipeline produces; rules that prevent bad combinations are pipeline-side validation.

- **Cascade order** (which sub-objects depend on which prior sub-objects) lives in the procgen pipeline code. The schema doesn't structurally encode that atmosphere depends on mass + temperature; that dependency is enforced by the pipeline computing atmospheric state AFTER mass and temperature are determined.

- **Validation** (a planet whose core parameters say "low geological activity" can't have grammar rules that produce active volcanism) lives in the procgen pipeline code. The schema doesn't refuse to hold a volcanism sub-object on a low-geological-activity body; the pipeline doesn't produce one.

### Why this split matters

Schema lives in the type system; logic lives in implementation. The split lets:

- Save-load read/write schema without knowing pipeline rules (a save file is data, not process).
- Network replication transmit schema without dragging activation logic along.
- The schema be inspected from outside the procgen module (Mission Control UI, debug tooling, save-file editors) without depending on procgen pipeline code.
- Multiple pipelines (the §6/STATUS.md parallel-pipeline question, future replacements) all produce the same schema — schema is the stable interface, pipelines come and go.

This is the same principle that drives VesselAuthoritativeState's separation from VesselDesign (per NETCODE_CONTRACT §2.1 / §2.5): state is one thing, the rules that produce state are another.

## Forward links

- Stage 1 (stellar context) input contract locked at commit 058 — the 8 inputs + 5 derived = 13 fields that Stage 1 produces will map to whatever sub-object(s) end up representing stellar state in the per-sub-object design pass that lands stellar-state contents.
- Sol-equivalent stellar parameters (hand-tuned static values) locked at commit 058 — the home system's star will be the first concrete instance of the stellar-state sub-object(s) populated.
- Subsequent commits (060+) design individual sub-objects. Natural next: the sub-object(s) that will hold stellar state (continuing 058's Stage 1 thread). After that, orbital state (most bodies have it; orbital mechanics well-understood from existing Vessels work). Then iteratively per sub-object. Each per-sub-object commit decides naming, granularity, and internal contents for that one sub-object.
- Pipeline cascade structure reconciliation (the §6 vs STATUS.md question) is its own future commit when enough sub-objects are designed to make the cascade tractable.

## When this design retires

The composition-all-the-way-down shape is locked. Specific sub-object decomposition, naming, and internal contents will evolve as individual sub-object designs land. If a future design pass surfaces that the shape itself is wrong (e.g., we need inheritance after all, or features should be a dictionary after all), that's a major DECISIONS-level reversal with its own commit and rationale.
