# 004c: Asynchronous progression machinery — bounded autonomous evolution, supply lines, procgen phasing, netcode deliverable

Land the asynchronous-progression-machinery cluster of refinements from Phase 0's second design pass. Ten edits across sections 1, 2, 3, 4, 5, and 8 specify what happens while the player is not looking and constrain how much can happen passively, plus move per-planet procgen forward in the build order and add the Phase 0 netcode contract as a Phase 1 prerequisite.

The unifying principle of this commit is: **long absences change who and what, not how much.** The home system continues to exist while the player's attention is elsewhere, but the rules of that continuation are bounded — events fire, crew age, political dynamics shift; bases idle when depots fill, research pauses when scientists are unassigned, supply lines deliver within finite network capacity. The principle is named in the design pillar refinement (section 1), implemented in the new section 3 subsection, and enforced by the supply line and research subsection amendments (section 4).

Two edits supersede previously locked content for correctness. The asynchronous-progression pillar in section 1 was unbounded as originally phrased; the save-format implications paragraph in section 2 was correspondingly unbounded. Both are amended to bounded versions consistent with the new section 3 subsection. The structural commitments preserved verbatim: every system that produces or consumes anything over time is still representable as `(state at time T, rate function)`; the bounded-evolution rules constrain the rate function rather than the architecture.

The build order amendments move per-planet procgen from Phase 7 to Phase 2 (parallel with vessel construction) because per-planet generation must exist before flight gameplay in Phase 3 can target real planets. Galaxy-level procgen — multiple star systems, distribution, anomaly seeding — remains at Phase 7. Section 5's `### Scope` subsection is reframed to make the "one procgen pipeline; home is hand-tuned, galaxy is seeded" architecture explicit; the deeper procgen architecture (14-stage pipeline, detection signatures, planet variety tiers, 90/9/1 anomaly distribution) is deferred to the follow-on session per the earlier scope decision.

The Phase 0 netcode contract deliverable formalizes what was previously implicit: the sim-tick boundary contract between authoritative state and PhysX-active simulation from commit `002`'s `### Foundational architectural principles` is the architectural seam where multiplayer desync would hide, and it deserves a written contract plus a prototype implementation as Phase 0 work, not Phase 1 work. Phase 1 inherits the contract as a prerequisite.

## Scope

- `docs/CONSTRAINTS.md` — modified. Ten edits across sections 1, 2, 3, 4, 5, 8:
  - Section 1: asynchronous-progression design pillar bullet expanded with bounded-limits clause and journey-gating principle phrase.
  - Section 2: `### Save format implications` paragraph amended to bounded language and to cross-reference the new section 3 subsection.
  - Section 3: new subsection `### Home system evolves autonomously` inserted between `### Channel 16 broadcasts` and `### Multiplayer as shared universe`.
  - Section 4: `### Supply lines` subsection appended with `**Network-capacity rule.**` paragraph. `### Research as asynchronous progression` subsection's Mechanics list extended with new scientist-gating bullet (inserted before the location-bonuses bullet).
  - Section 5: `### Scope` subsection rewritten to make one-pipeline / home-tuned / galaxy-seeded architecture explicit.
  - Section 8: Phase 0 description extended with `**Phase 0 deliverable: netcode contract.**` paragraph. Phase 1 description extended with `**Prerequisite:**` paragraph naming the netcode contract. Phase 2 heading amended to `### Phase 2 — Vessel construction and per-planet procgen (weight 2)` with body extended for per-planet procgen content. Phase 7 heading amended to `### Phase 7 — Galaxy-level procgen + interstellar (weight 3+)` with body amended to reflect per-planet generation having moved to Phase 2.

## Rationale

The bounded-autonomous-evolution rules are the structural answer to a class of design failures that long-running simulation games tend to hit: passive accumulation creates optimization pressure (place a miner and leave for a decade; come back to infinite resources). The game's positioning as minimal-tycoon (from commit `004b`) requires that this failure mode be foreclosed by architecture, not by tuning. The home-system-evolution subsection in section 3 names the rules; the supply line and research subsection amendments in section 4 enforce them at the mechanic level; the save format amendment in section 2 makes save-load respect them.

The journey-gating principle — long absences change who and what, not how much — gives the design a memorable load-bearing phrase. A player returning from a 50-year journey finds the base they left (same modules, same depot levels, same physical footprint) but the people are different and the political situation has shifted. The material world is recognizable; the human world has moved on. The reference is to the *Interstellar* emotional structure, made structural rather than aspirational.

The supply line network-capacity rule prevents a specific exploit: a single tiny depot at the network edge functionally serving as infinite storage by being constantly drained. The rule is that total storage equals the sum of depot capacities, no more; supply lines redistribute within the total, they do not create additional storage. This is a real cap on accumulation rather than a soft suggestion.

The research scientist-gating bullet makes explicit what the existing locked research mechanic implied but did not state: research advances when scientists are assigned, not by passive clock-tick. Abandoned projects pause; reassigning scientists resumes progress. The mechanic is consistent with the existing "Multiple research streams run in parallel up to a capacity limit (upgradeable)" bullet — the capacity limit was always about assigned scientists, but the previous phrasing left the assignment-as-gate question ambiguous.

The procgen scope reframe in section 5 clarifies what "hand-crafted home system" means under the one-pipeline architecture: hand-tuned procgen parameters per home-system body, not a separate hand-built pipeline. The same pipeline produces all bodies; the difference is whether parameters are designer-chosen (home system) or seed-derived (galaxy beyond). The reframe matters for phasing: per-planet generation must exist in Phase 2 for the home system to render at all, and Phase 7's "procgen + interstellar" needs to be narrowed to galaxy-level work since per-planet work has moved earlier.

The Phase 0 netcode contract deliverable formalizes a design discipline that was implicit in commit `002`'s sim-tick boundary commitments but not named as a Phase 0 work item. The contract specifies the boundary; the prototype validates it. Without both, Phase 1 implements code against an under-specified contract, and desync hides in the seam between the written rules and the actual code behavior. Naming the deliverable as a Phase 0 work item makes it concrete and ensures Phase 1 starts with a validated contract in hand.

## Changes

### Edit 1: Section 1, asynchronous progression design pillar — expanded with bounded-limits clause

Original (commit `001`):

> - Asynchronous progression. Things happen while the player is doing other things. Research completes, supply lines deliver, missions proceed. Every session produces visible forward motion.

Replacement:

> - Asynchronous progression. Things happen while the player is doing other things — within bounded limits that prevent passive accumulation from creating optimization pressure. Research advances when scientists are assigned; supply lines deliver within network capacity; bases produce until depots fill, then idle. Every session produces visible forward motion. The journey-gating principle: long absences change who and what, not how much.

The pillar is now four sentences. The "long absences change who and what, not how much" phrase becomes the load-bearing principle that this commit's home-system-evolution and supply-line refinements implement.

### Edit 2: Section 2, `### Save format implications` paragraph — bounded amendment

Original (commit `001`):

> On save load: advance all asynchronous systems forward by (current time minus last save time). Interstellar-cruise vessels advance their proper-time clocks. Any vessels that arrived during the gap transition to Kepler-rails in their destination system. Bases produce and consume resources for the elapsed period. Research projects advance. Supply line shipments arrive on schedule.

Replacement:

> On save load: advance all asynchronous systems forward by (current time minus last save time), subject to the bounded-autonomous-evolution rules from section 3's `### Home system evolves autonomously` subsection. Interstellar-cruise vessels advance their proper-time clocks. Any vessels that arrived during the gap transition to Kepler-rails in their destination system per the rules in section 2's `### Physics architecture` subsection. Bases produce and consume resources for the elapsed period, but production stops when depot capacity is reached (the base idles until capacity is freed). Research projects advance only for time periods when scientists were assigned (abandoned projects pause). Supply line shipments arrive on schedule, within network throughput limits.

The three previously-unbounded sentences (bases produce; research advances; supply lines deliver) are replaced with their bounded versions. The interstellar-cruise sentence is preserved verbatim with one parenthetical added cross-referencing section 2's Physics architecture subsection.

### Edit 3: Section 3, new subsection `### Home system evolves autonomously`

Inserted between `### Channel 16 broadcasts` and `### Multiplayer as shared universe`. The subsection contains:

- LOCKED headline commitment that the home system continues to exist while the player's attention is elsewhere, with the journey-gating principle phrase as the closing sentence of the headline paragraph.
- A "What evolves autonomously (narrative texture)" bullet list with three bullets: events fire procedurally from a designed pool weighted by colony state (with cross-reference to the transmission system); crew age and live their lives; inter-agency political dynamics shift over time at a high level with mechanics deferred to Phase 7+.
- A "What does not evolve autonomously (would create optimization pressure)" bullet list with three bullets: bases idle when depots fill; research doesn't auto-progress without active prioritization; population growth is bounded by habitat capacity and life support.
- A `**Supply line refinement.**` paragraph: total network storage equals sum of depot capacities; supply lines redistribute but don't create additional storage; producing bases ship to consuming or available-depot bases; throughput is finite per route; cannot exploit single-small-depot constant-drain.
- A `**The principle: long absences change who and what, not how much.**` paragraph using the *Interstellar* emotional structure reference. The material world is recognizable; the human world has moved on.
- A bounded-depth paragraph: v1 ships constrained numerical evolution plus procedural events from a designed pool; richer versions (autonomous colony decisions, evolving politics, emergent factions) are Phase 7+ design questions.
- A multiplayer note paragraph: autonomous evolution is lighter in multiplayer because other players' presence does the work of making the world feel alive.

### Edit 4: Section 4, `### Supply lines` subsection — append Network-capacity rule

Appended after the existing "Route capacity and transit time are the key upgrade vectors..." paragraph:

> **Network-capacity rule.** The total storage capacity of a base network equals the sum of its depot capacities. Supply lines redistribute resources between nodes within that total; they do not create additional storage. A producing base whose depot is full and whose downstream consumers are also full will idle (per the bounded-autonomous-evolution rules in section 3's `### Home system evolves autonomously` subsection). Throughput is finite per route, so redistribution is rate-limited; a single small depot at the network edge cannot serve as functionally-infinite storage by being constantly drained. The network capacity is a real cap on accumulation, not a soft suggestion.

### Edit 5: Section 4, `### Research as asynchronous progression` — new scientist-gating bullet

Inserted between the existing "Multiple research streams run in parallel" bullet and the existing "Research stations have location bonuses" bullet:

> - Research advances only when scientists are assigned to the project. Abandoned projects pause; reassigning scientists resumes progress. This is the bounded-autonomous-evolution rule from section 3 applied to research: research advances over time when work is being done on it, not by passive clock-tick.

The existing bullets are preserved.

### Edit 6: Section 5, `### Scope` subsection — one-pipeline reframe

Original (commit `001`):

> **LOCKED:** Hand-crafted home system. Procedurally generated galaxy beyond it.
>
> Home system: tutorial, soul, screenshots. Hand-tuned. Every body designed deliberately. Learning curve choreographed.
>
> Galaxy beyond: sparse star distribution, each star seeds a system.

Replacement:

> **LOCKED:** Procgen produces all planets in the universe, including the home system. The home system is hand-tuned — its bodies are generated by the same procgen pipeline that produces galaxy-beyond bodies, but with parameters chosen deliberately rather than seeded randomly. "Hand-crafted home system" means hand-tuned procgen parameters per home-system body plus optionally hand-placed named landmarks (impact craters, mountain ranges, landing sites) on the procedural terrain. The procgen pipeline is one system; the difference between home-system and galaxy-beyond bodies is whether their parameter sets are designer-chosen or seed-derived.
>
> Home system: tutorial, soul, screenshots. Hand-tuned parameters per body. Every body designed deliberately. Learning curve choreographed.
>
> Galaxy beyond: sparse star distribution, each star seeds a system; system bodies are procgen-generated with seed-derived parameters.

The structural commitment (hand-crafted home, procgen galaxy) is preserved; the framing changes to "one pipeline; home-tuned vs. seed-derived." The deeper procgen architecture is deferred to the follow-on session.

### Edit 7: Section 8 — build order amendments

Three coordinated changes:

**Edit 7a (Phase 0):** Append `**Phase 0 deliverable: netcode contract.**` paragraph specifying the contract is a written document plus a prototype implementation; the sim-tick boundary from commit `002`'s `### Foundational architectural principles` is where the contract operates; the contract is a Phase 1 prerequisite.

**Edit 7b (Phase 2):** Heading amended from `### Phase 2 — Vessel construction (weight 2)` to `### Phase 2 — Vessel construction and per-planet procgen (weight 2)`. Body extended with two new paragraphs explaining per-planet generation (the procgen pipeline that produces a planet's parameter set, terrain, atmosphere, ocean, biomes, resource distribution; home-system bodies use designer-chosen parameters through the same pipeline; galaxy-level procgen stays at Phase 7) and the parallelism with vessel construction.

**Edit 7c (Phase 7):** Heading amended from `### Phase 7 — Procgen + interstellar (weight 3+)` to `### Phase 7 — Galaxy-level procgen + interstellar (weight 3+)`. Body amended to specify galaxy-level work (sparse star distribution, optional spiral-arm density variation, system-level generation from star seeds) and to note that per-planet generation is already in place from Phase 2.

### Edit 8: Section 8 — Phase 1 prerequisite note

Phase 1 description extended with a `**Prerequisite:**` paragraph naming the Phase 0 netcode contract (written contract + prototype) as required before Phase 1 begins.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### Pillar refinement

1. Section 1 contains the literal phrase `Asynchronous progression. Things happen while the player is doing other things — within bounded limits that prevent passive accumulation from creating optimization pressure`.
2. The phrase `long absences change who and what, not how much` is present (occurs at least once; once in the pillar refinement and may occur additional times in section 3 as the principle is restated).
3. The old unbounded pillar text `Research completes, supply lines deliver, missions proceed. Every session produces visible forward motion.` is ABSENT from the doc.

### Save format amendment

4. Section 2 contains `subject to the bounded-autonomous-evolution rules from section 3's`.
5. The amendment contains `production stops when depot capacity is reached (the base idles until capacity is freed)`.
6. The amendment contains `Research projects advance only for time periods when scientists were assigned (abandoned projects pause)`.
7. The amendment contains `Supply line shipments arrive on schedule, within network throughput limits`.
8. The old unbounded triple-sentence `Bases produce and consume resources for the elapsed period. Research projects advance. Supply line shipments arrive on schedule.` is ABSENT.

### Home system evolves autonomously (new subsection)

9. Section 3 contains exactly one `### Home system evolves autonomously` heading anchored to start-of-line.
10. The subsection contains `long absences change *who* and *what*, not *how much*` (with italics, as the headline emphasis).
11. The subsection contains `Events fire procedurally from a designed pool`.
12. The subsection contains `Crew age, relationships develop, characters live their lives`.
13. The subsection contains `Inter-agency political dynamics shift over time at a high level (mechanics deferred to Phase 7+)`.
14. The subsection contains `Bases don't accumulate resources passively beyond their depot capacity`.
15. The subsection contains `Research doesn't auto-progress without active prioritization`.
16. The subsection contains `Population growth is bounded by habitat capacity and life support, not arbitrary time`.
17. The subsection contains `Supply lines redistribute resources within the base network but don't create infinite total storage`.
18. The subsection contains `This is the *Interstellar* emotional structure made structural rather than aspirational`.
19. The subsection contains `The depth is bounded for v1: constrained numerical evolution plus procedural events from a designed pool`.
20. The subsection contains `In multiplayer, autonomous evolution is lighter because other players' presence does the work of making the world feel alive`.

### Supply line network-capacity rule (section 4)

21. Section 4 contains the bolded paragraph lead-in `**Network-capacity rule.**`.
22. Section 4 contains `The total storage capacity of a base network equals the sum of its depot capacities`.
23. Section 4's existing supply line content preserved: `Graph of nodes with transit-time edges. Fallout 4 style. Set up once, runs automatically`.

### Research scientist-gating (section 4)

24. Section 4 contains the new bullet `Research advances only when scientists are assigned to the project`.
25. Section 4's existing research content preserved: `Multiple research streams run in parallel up to a capacity limit (upgradeable)`.

### Procgen scope reframe (section 5)

26. Section 5 contains `Procgen produces all planets in the universe, including the home system`.
27. Section 5 contains `"Hand-crafted home system" means hand-tuned procgen parameters per home-system body`.
28. Section 5's old LOCKED line `**LOCKED:** Hand-crafted home system. Procedurally generated galaxy beyond it.` is ABSENT.

### Build order amendments (section 8)

29. Section 8 contains `**Phase 0 deliverable: netcode contract.**` (bolded paragraph lead-in).
30. Section 8 contains `a written contract document *plus* a prototype implementation`.
31. Section 8 contains `**Prerequisite:** the Phase 0 netcode contract (written contract + prototype) must be in place before Phase 1 begins`.
32. Section 8 contains exactly one heading `### Phase 2 — Vessel construction and per-planet procgen (weight 2)` anchored to start-of-line.
33. Section 8 contains `Per-planet generation: the procgen pipeline`.
34. Section 8 contains exactly one heading `### Phase 7 — Galaxy-level procgen + interstellar (weight 3+)` anchored to start-of-line.
35. Section 8 contains `Per-planet generation is already in place from Phase 2`.
36. The old Phase 2 heading `### Phase 2 — Vessel construction (weight 2)` is ABSENT.
37. The old Phase 7 heading `### Phase 7 — Procgen + interstellar (weight 3+)` is ABSENT.

### Section 3 subsection order

38. The complete sequence of `### ` headings within section 3, in document order, is exactly: `### Parts and vessel construction`, `### Failure modes`, `### Automation and scripting`, `### Information asymmetry / progressive discovery`, `### Anomalies and mysteries`, `### Interstellar travel: tiered tech progression`, `### Director perspective`, `### Crew and characters`, `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts`, `### Home system evolves autonomously`, `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Goal structure`.

### Preserved-content anchors (per workflow rule)

39. Commit `001` content preserved across untouched section anchors: `Pixar register, not Goat Simulator register` (S3), `this is how Jeb became a legend` (S3), `Code is 20% of the work` (S5), `Hill sphere spacing, frost line` (S5), `This is the vertical slice MVP` (S8), `placeholder cube launches from a planet surface` (S8), `siren song of pretty screenshots` (S10), `Suggested repo layout` (S10), `project's institutional memory` (S11), `Critical practice: doc-driven development` (S11), `this is the kraken returning` (S12), `Pre-flight checklist before generating code` (S12), `KSP 2's path` (S13), `Stale doc syndrome` (S13), `Last comprehensive update: Phase 0 design crystallization` (S14). Each present exactly once.
40. Commit `002` content preserved: anchored heading `### Foundational architectural principles` count is 1; literal phrases `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `Authoritative state replication is the multiplayer model for PhysX-active vessels`, `sharp and symmetric` each present exactly once. Five numbered principles in the FAP subsection.
41. Commit `003` content preserved: `detection-aggressiveness parameter`, `**Agency-based observation sharing.**`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.` each present exactly once.
42. Commit `004a` content preserved: anchored heading counts of exactly one each for `### Multiplayer as shared universe` and `### Mode-portable designs and templates`; phrases `Save files are mode-locked at creation` and `parameterized by game mode and propulsion tier` each present exactly once.
43. Commit `004b` content preserved: anchored heading counts of exactly one each for `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### Interstellar distances`, `### Time-warp in single-player`, `### Director perspective`, `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts`. Literal phrase `1/8 real scale` present exactly once. Section 7 bullet `- Solar system scale (Casual 1/10 / Standard 1/8 / Realistic 1/1, with custom intermediate scales available)` present exactly once.
44. Section 9 final-three-bullets adjacency: last three bullets in section 9 are, in this order, `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX`.
45. All 14 numbered `## N.` section headings still present.
46. File line count in range 850–880. The post-`004b` baseline was 829 lines; this commit's net delta is approximately 30–40 lines (additions plus replacements net out to ~33 lines).

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/004c_asynchronous_progression_machinery.md
```
