# 008: Procgen architecture — six-layer hierarchy, 14-stage pipeline, planet variety, anomaly distribution, galaxy scope, map view hierarchy

Land the full procgen architecture. Section 5's existing four-bullet `### Generation layers` subsection is replaced with seven new subsections describing the architecture in depth: the six-layer hierarchy (galactic structure → star sampling → stellar parameters → system composition → body parameters and features → surface detail), the 14-stage per-body pipeline that produces Layer 5 output, the hybrid pipeline-plus-grammar architectural pattern, planet variety tiers with explicit phase placement, the feature-layer architecture for Stage 11 special features, the 90/9/1 anomaly distribution rule for Stage 12 anomaly resolution weights, and the galaxy scope commitment (10,000-50,000 stars within 100-200 light-years of home).

The existing `### Per-body physics parameter set` subsection (from commit 006) is moved from the end of section 5 to between `### Galaxy scope` and `### Resource distribution`, putting the data-contract commitment immediately after the architectural commitments that produce it and before the resource-output and tuning subsections. Section 5 grows from 5 subsections (Scope, Generation layers, Resource distribution, Tuning, Per-body parameter set) to 11 subsections.

Section 6 gains a new `### Map view hierarchy` subsection at the end, naming the four primary view scales (galactic / system / body / construction) and the transitions between them.

Section 8's Phase 2 description is expanded with a new paragraph naming the specific procgen deliverables for Phase 2: Layer 5 (the 14-stage pipeline) implemented, feature layer architecture with 2-3 cheap-tier feature layers, hand-tuned home system bodies with all 14 stages applied including Stage 13 hand-placed features, seed function backward-compatibility versioning. Layers 1-3 (galactic structure, star sampling, stellar parameters) are explicitly named as Phase 7 work.

This commit resolves commit 007's forward reference to the 90/9/1 distribution rule. After this commit lands, section 5 contains the named `### The 90/9/1 anomaly distribution` subsection with the literal `90%`, `9%`, and `1%` resolution phrases.

## Scope

- `docs/CONSTRAINTS.md` — modified. Three logical edits applied as one atomic write:
  - Section 5: `### Generation layers` subsection replaced with a chained block of seven new subsections (`### Generation architecture` through `### Galaxy scope`); existing `### Per-body physics parameter set` subsection moved from end of section to between `### Galaxy scope` and `### Resource distribution`.
  - Section 6: new `### Map view hierarchy` subsection appended at the end, after `### Information legibility`.
  - Section 8: Phase 2 description expanded with one new paragraph naming specific procgen deliverables.

## Rationale

The four-bullet `### Generation layers` subsection from commit 001 captured the conceptual progression of procgen (galaxy → system → planet → anomaly) but was not architecturally specific. The new six-layer hierarchy makes the seed derivation, caching strategy, and per-layer responsibilities explicit. Each layer is deterministic from its inputs and seed; each layer's output is cached when computed; the whole universe exists in the master seed; everything else is computed on demand. This is the architectural anchor that the lazy-generation commitment requires and that commit 008's other subsections build on.

The 14-stage pipeline operationalizes Layer 5 in detail. The 14 stages flow unidirectionally from stellar context through detection signatures, with each stage taking inputs from earlier stages and producing inputs for later ones. The stages can be developed incrementally; the architecture is fixed; contents can be filled in over time. Stage 14 (detection signatures) is the critical connection point with commit 007's detection ecosystem: detection instruments built in later phases read from cached signatures computed at body generation time rather than retrofitting bodies.

The hybrid pipeline-plus-grammar subsection names an architectural pattern that applies throughout the 14-stage pipeline. Pipeline establishes the big picture (stages 1-14 produce the body's full parameter set and detection signatures); grammar handles local variation (within terrain, biomes, and feature placement, rules determine what goes where based on local conditions). Grammar is bounded by pipeline; local rules cannot override pipeline-level constraints. A planet whose Stage 2 parameters say "low geological activity" cannot have grammar rules that produce active volcanism — the volcanism feature layer would not fire.

The planet variety tiers subsection commits an explicit cost-vs-benefit framing for procgen content. Cheap tier (free from the pipeline, available in Phase 5-7) covers parameter combinations that emerge naturally — atmospheric coloration, extreme temperature worlds, tidal locking, etc. Medium tier (dedicated systems, 1-3 weeks each, Phases 5-8) covers binary/multiple-star systems, active volcanism, auroras, pulsars, etc., with a ship target of 5-10 medium-tier phenomena by Phase 7 end. Hard/hero tier (significant engineering, 1+ month each, Phase 7-8) covers frozen-moment planetary collisions, neutron stars with accretion disks, megastructures, with a ship target at v1 of 2-3 hard-tier features. The tier framing makes the procgen ambition trade-offs explicit.

The feature-layer architecture subsection specifies how Stage 11 special features compose. Each layer (rings, auroras, volcanism, impact craters, frozen-moment collisions, biosignatures, etc.) is independent with its own activation rule, placement logic, and visual/gameplay effects. Layers compose freely except where anti-correlation rules prevent specific combinations (volcanism plus heavy rings is dampened because rings would be disrupted; recent major impact dampens stable atmosphere; frozen-moment collision overrides most other features). Anti-correlation rules are tunable parameters, not hard constraints — edge cases are anomalies worth investigating.

The 90/9/1 anomaly distribution subsection commits a tuning rule for Stage 12 anomaly resolution weights. Of all anomalies that warrant player investigation, 90% resolve to interesting-but-understandable phenomena, 9% to genuinely surprising results, 1% to once-per-playthrough hero moments. The ratio is what keeps exploration satisfying for hundreds of hours. The 1% bucket includes both procedural rare phenomena and hand-authored hero moments. Target: 10-20 hand-authored mysteries at v1, scattered through procgen seeds, each findable once per save. This subsection's named `### The 90/9/1 anomaly distribution` heading plus the literal `90%`, `9%`, `1%` phrases in the body resolve commit 007's forward reference from `### Anomaly types worth seeding`.

The galaxy scope subsection commits the playable region size. A local stellar neighborhood of approximately 10,000-50,000 stars within roughly 100-200 light-years of the home system. Large enough to never be exhausted by any individual player; structured enough to feel approachable rather than infinite. The galaxy at large is visible through telescopes but unreachable — framed as the region currently accessible to humanity given propulsion technology.

The per-body parameter set move (commit 006 placed it at the end of section 5; commit 008 moves it between Galaxy scope and Resource distribution) makes the section reading flow: what gets generated → architecture that produces it → the 14-stage pipeline within → architectural patterns within stages → cost-and-content tiers → specific subsystem details → scope of the playable region → data contract for pipeline output → specific resource output → meta-commitment on tuning. The data contract sits adjacent to the architectural commitments that produce it rather than at the end of the section after unrelated content.

The map view hierarchy commits the four primary view scales. Galactic / stellar neighborhood view operates at light-year scale and is where long-arc decisions get made (which star to send the next interstellar probe to, where the unexplored regions are). System view operates at interplanetary scale for mission planning within a system. Body view operates at orbital or surface scale for tactical decisions about a specific body. Construction view is local to a specific facility. Transitions between views are zoom-and-select operations; data is consistent across views.

The Phase 2 procgen-deliverables paragraph in section 8 names what specifically lands in Phase 2: Layer 5 (the 14-stage pipeline) implemented with full parameter set including Stage 14 detection signatures; feature layer architecture with 2-3 cheap-tier feature layers as proof of architecture; hand-tuned home system bodies with all 14 stages applied; seed function backward-compatibility versioning in place from day one. Layers 1-3 (galactic structure, star sampling, stellar parameters) explicitly named as Phase 7 work.

## Changes

### Edit A: Section 5 — replace `### Generation layers` with chained block of seven new subsections, plus move `### Per-body physics parameter set`

Two logical changes performed as one atomic operation:

1. The existing `### Generation layers` subsection (four bullets: Galaxy, System, Planet, Anomaly) is fully replaced with a chained block of seven new subsections in order: `### Generation architecture` (six-layer hierarchy + lazy-generation paragraph), `### The 14-stage pipeline (Layer 5)` (14 numbered stages), `### Hybrid pipeline-plus-grammar`, `### Planet variety tiers` (three tiers with phase placement), `### Feature-layer architecture` (anti-correlation rules), `### The 90/9/1 anomaly distribution` (resolution weights), `### Galaxy scope` (10,000-50,000 stars).

2. The existing `### Per-body physics parameter set` subsection (commit 006) is removed from its position at the end of section 5 (between `### Tuning is the hard part` and `## 6.`) and re-inserted between `### Galaxy scope` and `### Resource distribution`.

The information content of the old four-bullet model is preserved within the new structure: old "Galaxy" maps to new Layers 1+2 (Galactic structure + Star sampling); old "System" maps to new Layer 4 (System composition) with the Hill sphere / frost line / mass distributions phrase preserved verbatim; old "Planet" maps to new Layer 5 (Body parameters and features) and the 14-stage pipeline with the multi-octave noise phrase preserved verbatim; old "Anomaly" maps to new Stage 12 (Anomalies) with the anomaly density tunable property captured by the 90/9/1 distribution subsection.

After this edit lands, section 5's complete subsection order is: `### Scope`, `### Generation architecture`, `### The 14-stage pipeline (Layer 5)`, `### Hybrid pipeline-plus-grammar`, `### Planet variety tiers`, `### Feature-layer architecture`, `### The 90/9/1 anomaly distribution`, `### Galaxy scope`, `### Per-body physics parameter set`, `### Resource distribution`, `### Tuning is the hard part`. Total: 11 subsections.

### Edit B: Section 6 — append `### Map view hierarchy` subsection at end of section

Appended at the end of section 6, after the existing `### Information legibility` subsection and immediately before `## 7. Difficulty and accessibility`. New subsection content:

- LOCKED commitment that the game has four primary view scales, each operating at the scale appropriate to player decisions at that scale.
- Four bolded view paragraphs: Galactic / stellar neighborhood view, System view, Body view, Construction view.
- Transitions paragraph: galactic-to-system is "select a star, zoom in"; system-to-body is "select a body, zoom in"; body-to-construction is "enter the facility"; data is consistent across views.

After this edit lands, section 6's complete subsection order is: `### Patterns to use`, `### First-hour experience`, `### Information legibility`, `### Map view hierarchy`. Total: 4 subsections.

### Edit C: Section 8 Phase 2 description — append procgen-deliverables paragraph

Appended after the existing closing paragraph of Phase 2's description (the commit 006 paragraph naming the parameter set as the data contract):

> Phase 2 procgen deliverables specifically: Layer 5 (the 14-stage pipeline) implemented, producing the full parameter set including Stage 14 detection signatures for any body given a seed; feature layer architecture with 2-3 cheap-tier feature layers implemented (rings, basic auroras, basic volcanism) as proof of architecture; hand-tuned home system bodies with all 14 stages applied including Stage 13 hand-placed features; seed function backward-compatibility versioning in place from day one. Layers 1-2 (galactic structure and star sampling) and Layer 3 (stellar parameters) are Phase 7 work.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### New-content anchored heading checks

1. `### Generation architecture` anchored heading count is 1.
2. `### The 14-stage pipeline (Layer 5)` anchored heading count is 1.
3. `### Hybrid pipeline-plus-grammar` anchored heading count is 1.
4. `### Planet variety tiers` anchored heading count is 1.
5. `### Feature-layer architecture` anchored heading count is 1.
6. `### The 90/9/1 anomaly distribution` anchored heading count is 1.
7. `### Galaxy scope` anchored heading count is 1.
8. `### Map view hierarchy` anchored heading count is 1.
9. `### Generation layers` anchored heading count is 0 (replaced).

### Six-layer hierarchy content

10. All six layer lead-ins present, each exactly once: `**Layer 1 — Galactic structure.**`, `**Layer 2 — Star sampling.**`, `**Layer 3 — Stellar parameters.**`, `**Layer 4 — System composition.**`, `**Layer 5 — Body parameters and features.**`, `**Layer 6 — Surface detail.**`.
11. The literal phrase `Each layer's output is cached when computed` is present (in the lazy-generation paragraph).

### 14-stage pipeline content

12. All 14 numbered stages present, each in the form `N. **Stage name.**`: `1. **Stellar context.**`, `2. **Planetary core parameters.**`, ..., `14. **Detection signatures.**`. Spot-check key stages: Stage 5 Atmosphere, Stage 8 Terrain generation, Stage 11 Special feature layers, Stage 12 Anomalies, Stage 13 Hand-placed features (home system only), Stage 14 Detection signatures.

### Planet variety tiers content

13. All three tier lead-ins present: `**Cheap tier (free from the pipeline) — Phase 5-7:**`, `**Medium tier (dedicated systems, 1-3 weeks each) — Phases 5-8:**`, `**Hard/hero tier (significant engineering, 1+ month each) — Phase 7-8:**`.

### 90/9/1 anomaly distribution content (resolves commit 007 forward reference)

14. The literal phrase `90% resolve to interesting-but-understandable phenomena` is present.
15. The literal phrase `9% resolve to genuinely surprising results` is present.
16. The literal phrase `1% resolve to once-per-playthrough hero moments` is present.
17. Commit 007's forward reference `The 90/9/1 distribution rule (section 5)` (in `### Anomaly types worth seeding`) is still present and now resolves to existing content.

### Galaxy scope content

18. The literal phrase `10,000-50,000 stars within roughly 100-200 light-years of the home system` is present.

### Map view hierarchy content

19. All four view lead-ins present: `**Galactic / stellar neighborhood view.**`, `**System view.**`, `**Body view.**`, `**Construction view.**`.

### Section 8 Phase 2 procgen deliverables

20. The literal phrase `Layer 5 (the 14-stage pipeline) implemented` is present.
21. The literal phrase `feature layer architecture with 2-3 cheap-tier feature layers` is present.
22. The literal phrase `seed function backward-compatibility versioning in place from day one` is present.
23. The literal phrase `Layers 1-2 (galactic structure and star sampling) and Layer 3 (stellar parameters) are Phase 7 work` is present.

### Section 5 subsection ordering

24. Section 5's complete subsection list, in document order, is exactly: `### Scope`, `### Generation architecture`, `### The 14-stage pipeline (Layer 5)`, `### Hybrid pipeline-plus-grammar`, `### Planet variety tiers`, `### Feature-layer architecture`, `### The 90/9/1 anomaly distribution`, `### Galaxy scope`, `### Per-body physics parameter set`, `### Resource distribution`, `### Tuning is the hard part`. Total: 11 subsections.

### Section 6 subsection ordering

25. Section 6's complete subsection list, in document order, is exactly: `### Patterns to use`, `### First-hour experience`, `### Information legibility`, `### Map view hierarchy`. Total: 4 subsections.

### Preserved-content anchors

26. Commit 006 content preserved: anchored heading count of 1 for `### Per-body physics parameter set` (note: moved within section 5; heading and content preserved verbatim). Literal LOCKED commitment `**LOCKED:** Every celestial body in the universe — home system, procgen system, every planet, every moon, every asteroid, every star — has a full physics parameter set defined at the time of its first generation` still present. Anchored heading `### Layered engagement framework` still count 1. Section 1 design pillars still contain `- Discovery as gameplay, grounded in real astrophysics.` and `- Physics-grounded substrate.`
27. Commit 007 content preserved: anchored heading counts of 1 each for `### Detection ecosystem`, `### Platforms for detection`, `### Discovery progression — eight stages`, `### Detection mechanics`, `### Anomaly types worth seeding`. The forward reference `The 90/9/1 distribution rule (section 5)` is still in `### Anomaly types worth seeding`.
28. Commit 004a/b/c content preserved: anchored heading counts of 1 each for `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### Interstellar distances`, `### Time-warp in single-player`, `### Director perspective`, `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts`, `### Home system evolves autonomously`. Literal phrases: `Save files are mode-locked at creation`, `1/8 real scale`, `within bounded limits that prevent passive accumulation`, `**Network-capacity rule.**`, `Research advances only when scientists are assigned to the project`, `**Phase 0 deliverable: netcode contract.**`, `This is the *Interstellar* emotional structure made structural rather than aspirational` each present. Anchored headings `### Phase 2 — Vessel construction and per-planet procgen (weight 2)` and `### Phase 7 — Galaxy-level procgen + interstellar (weight 3+)` each count 1.
29. Commit 005 content preserved: `### Phase exit criteria` anchored count 1; `**Mobile shipping note.**` present; `FoundationPrimitives` companion doc reference present.
30. Commit 003 content preserved: `**Agency-based observation sharing.**`, `detection-aggressiveness parameter`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.` each present.
31. Commit 002 content preserved: anchored heading count of 1 for `### Foundational architectural principles`; literal `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `sharp and symmetric`, `Authoritative state replication is the multiplayer model for PhysX-active vessels` each present. Five numbered principles in FAP.
32. Commit 001 content preserved across untouched sections: distinctive phrases per section (Pixar register, Jeb legend, Hill sphere, 20%/80%, Procgen all planets, KSP's first hour, Dwarf Fortress / RimWorld, physics fidelity toggle, vertical slice MVP, placeholder cube, siren song, suggested repo layout, institutional memory, doc-driven dev, kraken returning, pre-flight checklist, KSP 2's path, stale doc syndrome, Living document, Last comprehensive update). Each present exactly once.
33. Section 9 final-three-bullets adjacency: `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX` in order. Section 9 bullet count: 13.
34. All 14 numbered `## N.` section headings still present.
35. File line count is 1095 (post-007 was 1017 lines; this commit added 78 net lines — note that 78 is below the originally projected 150-180 range because the per-body parameter set move is content-neutral, not additive).

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/008_procgen_architecture.md
```
