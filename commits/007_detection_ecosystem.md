# 007: Detection ecosystem, platforms, eight-stage discovery progression, mechanics, anomaly types

Land the mechanism of discovery. Commit 006 added the "Discovery as gameplay, grounded in real astrophysics" pillar to section 1; this commit specifies how that pillar actually works through five new section 3 subsections inserted as a contiguous block between the existing `### Information asymmetry / progressive discovery` and `### Anomalies and mysteries` subsections.

The five subsections build incrementally: detection methods (the seven remote-detection categories plus direct sampling, what each sees and misses) → platforms (where instruments live: ground, space, lunar, interferometric arrays, mobile, surface vehicles) → discovery progression (eight fine-grained stages from total ignorance to continued discovery) → detection mechanics (operational gameplay: filter switching, "what is this" puzzles, distance and time effects, multi-investigator pooling, catalog growth, old-data re-analysis) → anomaly types (nine specific categories the procgen seeds, each with multiple plausible resolutions whose weights the 90/9/1 distribution from section 5 governs).

This is a substantial extension of the existing `### Information asymmetry / progressive discovery` subsection rather than a replacement. The four observation tiers (home / flyby / orbit / surface) from commit 001 remain valid as coarse categories. The detection methods and eight stages are the multi-instrument and finer-grained mechanics within. The existing subsection's per-agency observation model (from commit 003) is also preserved verbatim and is cross-referenced from the new content.

Section 3 grows from 15 subsections to 20 after this commit. The reconciled source's cross-commit consistency note projected 22-24 subsections after all commits land; this is on track for that projection. A future split decision (potentially separating "Gameplay mechanics" from "World behavior and discovery") is rightly deferred until all commits have landed and the actual reading flow can be evaluated.

Three forward references are accepted in this commit: the 90/9/1 distribution rule cross-reference in `### Anomaly types worth seeding` points to content that lands in commit 008 (one-commit inconsistency window); catalog references in `### Detection mechanics` point to commit 009's `### Catalog as long-term meta-game` (the references are intuitive enough that they read as "the place where observations go" without needing the dedicated subsection to exist yet); cross-references to detection mechanics from elsewhere are deliberately omitted until those subsections land in commits 008-012.

## Scope

- `docs/CONSTRAINTS.md` — modified. One edit replacing the boundary between `### Information asymmetry / progressive discovery` and `### Anomalies and mysteries` with the existing closing content plus five new subsections plus the existing Anomalies-and-mysteries heading. Net effect: five subsections inserted as a contiguous block in section 3.

## Rationale

Discovery is the most differentiated feature of the game per commit 006's new pillar. Discovery operationalizes as a specific mechanism: detection methods reveal different aspects of objects, each with its own physical character and characteristic blind spots; depth comes from combining methods rather than from any single one. The reconciled source structures this as a five-subsection block to keep the mechanic readable.

The Detection ecosystem subsection commits the seven detection method categories plus direct sampling. The grouping reflects how real astronomy works: remote detection methods divided by wavelength (visible / IR / UV / X-ray / gamma-ray / radio) plus orthogonal methods (particle / gravitational wave) plus the conceptually-distinct in-situ approach (direct sampling — going there). The heading "Seven detection method categories" with eight bolded paragraphs is intentional: Particle detection and gravitational wave detection share a category (specialized detectors revealing processes invisible to electromagnetic radiation), and Direct sampling is in a different conceptual class than the seven remote methods.

The Platforms subsection commits where instruments live. Each platform has its own gameplay role: ground observatories are cheap but atmosphere-limited; space-based observatories access all wavelengths but cost launch mass; lunar far-side observatories are radio-quiet but require infrastructure to build; interferometric arrays improve resolution with baseline distance, rewarding long-term investment in multi-site infrastructure; mobile platforms (probes, satellites, rovers) carry instruments to specific contexts; ground vehicles on other bodies give site-and-mobility-selection gameplay.

The Discovery progression subsection commits eight stages from total ignorance to continued discovery. The stages are fine-grained refinement of the four observation tiers (a body at Stage 3 — spectroscopic atmosphere — might still be in the "home" tier of investigation; the tier categorizes how the player accessed the body; the stage categorizes what they now know). The stages aren't strictly linear: a body might stop at Stage 1, a flyby might skip lower stages, higher-stage observations might re-open lower stages when anomalies appear. The progression terminates at Stage 8 (continued discovery) rather than Stage 7 (surface investigation) because the doc explicitly commits to discovery that continues after a body is "known" — old data can be re-analyzed with new instruments; new mission types reveal new things; time changes the planet.

The Detection mechanics subsection commits the operational gameplay: filter switching as a core verb (instruments have wavelength selectors; switching reveals different aspects of the same object); the "what is this" puzzle as gameplay (anomalous signatures with multiple plausible explanations; follow-up measurements disambiguate); distance and time effects (measurements at 100 ly are less informative than at 10 ly; variable stars only reveal their period over time; patience is part of the gameplay); multi-investigator pooling in multiplayer; persistent discovery names; the catalog as the record of accumulated observation; old mysteries that can reactivate when new instruments unlock. These are the operational primitives that turn the methods, platforms, and stages into actual gameplay.

The Anomaly types subsection commits nine specific categories the procgen seeds: the transit that shouldn't be, the chemistry that shouldn't persist, the signal with structure, the object that moves wrong, the gravitational anomaly, the variable that varies wrong, the temperature mismatch, the repeating burst, the directional signal. Each has multiple plausible resolutions with weighted probabilities; the player cannot tell from initial detection which category an anomaly falls into; investigation reveals it. The categories make the existing `### Anomalies and mysteries` subsection (from commit 001) concrete by specifying what gets seeded.

The placement of all five subsections as a contiguous block between `### Information asymmetry / progressive discovery` and `### Anomalies and mysteries` reads cleanly: existing observation-tier framework → seven detection methods → platforms that carry them → eight stages of progression → operational mechanics → specific anomaly categories → existing anomaly meta-content. Intent → mechanism → instances. The new content extends the existing locked content rather than replacing it.

## Changes

### Edit 1: Section 3 — insert five new subsections as a contiguous block

A single replacement inserts five subsections between the existing closing paragraph of `### Information asymmetry / progressive discovery` (the agency-based observation sharing block from commit 003 ending with "...single-player is the trivial reduction where the agency has one member.") and the existing `### Anomalies and mysteries` heading.

The five new subsections, in order:

1. `### Detection ecosystem` — LOCKED commitment that discovery operates through distinct methods with characteristic blind spots, plus the eight bolded paragraphs covering Visible light astronomy, Infrared astronomy, Ultraviolet astronomy, X-ray astronomy, Gamma-ray astronomy, Radio astronomy, Particle detection and gravitational wave detection, Direct sampling.
2. `### Platforms for detection` — six bolded paragraphs covering Ground-based observatories, Space-based observatories, Lunar / moon-surface observatories, Interferometric arrays, Mobile platforms, Ground-based vehicles on other bodies.
3. `### Discovery progression — eight stages` — nine bolded paragraphs covering Stages 0 through 8 (the stage numbering is 0-8, totaling nine stages; "eight stages" refers to the eight named progression states from Stage 1 onward, with Stage 0 as the implicit pre-detection state).
4. `### Detection mechanics` — eight bolded operational primitives: filter switching, "what is this" puzzle, distance effects, time effects, multi-investigator pooling, discovery names, catalog growth, old-mystery reactivation.
5. `### Anomaly types worth seeding` — nine bolded anomaly categories, opening with the cross-reference to section 5's 90/9/1 distribution rule.

After this edit lands, section 3's complete subsection order is: Parts and vessel construction → Failure modes → Automation and scripting → Information asymmetry / progressive discovery → **Detection ecosystem** → **Platforms for detection** → **Discovery progression — eight stages** → **Detection mechanics** → **Anomaly types worth seeding** → Anomalies and mysteries → Interstellar travel: tiered tech progression → Director perspective → Crew and characters → EVA as temporary character control → Transmissions and world communication → Channel 16 broadcasts → Home system evolves autonomously → Multiplayer as shared universe → Mode-portable designs and templates → Goal structure. Total: 20 subsections.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### New-content anchored heading checks

1. `### Detection ecosystem` anchored heading count is 1.
2. `### Platforms for detection` anchored heading count is 1.
3. `### Discovery progression — eight stages` anchored heading count is 1.
4. `### Detection mechanics` anchored heading count is 1.
5. `### Anomaly types worth seeding` anchored heading count is 1.

### Detection ecosystem content

6. The subsection contains the LOCKED commitment phrase `**LOCKED:** Discovery operates through a suite of distinct detection methods`.
7. All eight detection-method bolded paragraph leads are present, each exactly once: `**Visible light astronomy.**`, `**Infrared astronomy.**`, `**Ultraviolet astronomy.**`, `**X-ray astronomy.**`, `**Gamma-ray astronomy.**`, `**Radio astronomy.**`, `**Particle detection and gravitational wave detection.**`, `**Direct sampling.**`.

### Discovery progression content

8. All nine stage lead-ins are present, each exactly once: `**Stage 0: Total ignorance.**`, `**Stage 1: Coarse detection.**`, `**Stage 2: Confirmation and basic characterization.**`, `**Stage 3: Spectroscopic atmosphere.**`, `**Stage 4: High-resolution imaging.**`, `**Stage 5: Flyby investigation.**`, `**Stage 6: Orbital observation.**`, `**Stage 7: Surface investigation.**`, `**Stage 8: Continued discovery.**`.

### Anomaly types content

9. All nine anomaly-type lead-ins are present, each exactly once: `**The transit that shouldn't be.**`, `**The chemistry that shouldn't persist.**`, `**The signal with structure.**`, `**The object that moves wrong.**`, `**The gravitational anomaly.**`, `**The variable that varies wrong.**`, `**The temperature mismatch.**`, `**The repeating burst.**`, `**The directional signal.**`.

### Section 3 subsection ordering

10. Section 3's complete subsection list, in document order, is exactly: `### Parts and vessel construction`, `### Failure modes`, `### Automation and scripting`, `### Information asymmetry / progressive discovery`, `### Detection ecosystem`, `### Platforms for detection`, `### Discovery progression — eight stages`, `### Detection mechanics`, `### Anomaly types worth seeding`, `### Anomalies and mysteries`, `### Interstellar travel: tiered tech progression`, `### Director perspective`, `### Crew and characters`, `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts`, `### Home system evolves autonomously`, `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Goal structure`. Total: 20 subsections.

### Preserved-content anchors

11. Commit 006 content preserved: anchored heading counts of 1 each for `### Per-body physics parameter set` (section 5) and `### Layered engagement framework` (section 7). Section 1 design pillars list contains `- Discovery as gameplay, grounded in real astrophysics.` and `- Physics-grounded substrate.` each present.
12. Commit 004a content preserved: anchored heading counts of 1 each for `### Multiplayer as shared universe`, `### Mode-portable designs and templates`. Literal phrases `Save files are mode-locked at creation`, `parameterized by game mode and propulsion tier` each present exactly once.
13. Commit 004b content preserved: anchored heading counts of 1 each for `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### Interstellar distances`, `### Time-warp in single-player`, `### Director perspective`, `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts`. Literal `1/8 real scale` and section 7 toggle bullet present.
14. Commit 004c content preserved: anchored heading count of 1 for `### Home system evolves autonomously`. Literal phrases `within bounded limits that prevent passive accumulation`, `**Network-capacity rule.**`, `Research advances only when scientists are assigned to the project`, `Procgen produces all planets in the universe, including the home system`, `**Phase 0 deliverable: netcode contract.**`, `This is the *Interstellar* emotional structure made structural rather than aspirational` each present exactly once. Anchored headings `### Phase 2 — Vessel construction and per-planet procgen (weight 2)` and `### Phase 7 — Galaxy-level procgen + interstellar (weight 3+)` each count 1.
15. Commit 005 content preserved: anchored heading count of 1 for `### Phase exit criteria`. Literal `**Mobile shipping note.**` and `FoundationPrimitives` companion doc references present.
16. Commit 003 content preserved: literal `**Agency-based observation sharing.**`, `detection-aggressiveness parameter`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.` each present exactly once.
17. Commit 002 content preserved: anchored heading count of 1 for `### Foundational architectural principles`. Literal `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `sharp and symmetric`, `Authoritative state replication is the multiplayer model for PhysX-active vessels` each present exactly once. Five numbered principles in FAP.
18. Commit 001 content preserved across untouched sections: `Pixar register, not Goat Simulator register` (S3), `this is how Jeb became a legend` (S3), `Code is 20% of the work` (S5), `Hill sphere spacing, frost line` (S5), `KSP's first hour was bad` (S6), `Physics fidelity (pragmatic / strict)` (S7), `This is the Dwarf Fortress / RimWorld pattern` (S7), `This is the vertical slice MVP` (S8), `placeholder cube launches from a planet surface` (S8), `siren song of pretty screenshots` (S10), `Suggested repo layout` (S10), `project's institutional memory` (S11), `Critical practice: doc-driven development` (S11), `this is the kraken returning` (S12), `Pre-flight checklist before generating code` (S12), `KSP 2's path` (S13), `Stale doc syndrome` (S13), `Living document` at start of paragraph (S14), `Last comprehensive update: Phase 0 design crystallization` (S14). Each present exactly once.
19. Section 9 final-three-bullets adjacency: `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX` in order. Section 9 bullet count: 13 (after commit 005's RESOLVED-crew-abstraction deletion).
20. All 14 numbered `## N.` section headings still present.
21. File line count in range 1005-1035 (post-006 was 909 lines; this commit added approximately 108 net lines).

### Cross-commit consistency notes

22. The cross-reference in `### Anomaly types worth seeding` to section 5's 90/9/1 distribution rule is a forward reference to content landing in commit 008. The between-commits inconsistency window (commit 007 landed, commit 008 not yet landed) is expected; once commit 008 lands, section 5 will contain the named `### The 90/9/1 anomaly distribution` subsection and the cross-reference resolves.
23. Cross-references in `### Detection mechanics` and `### Discovery progression — eight stages` to "the catalog" are forward references to content landing in commit 009. The references are intuitive enough that they read as "the place where observations go" without needing the dedicated `### Catalog as long-term meta-game` subsection to exist yet.

If any of checks 1-21 fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/007_detection_ecosystem.md
```
