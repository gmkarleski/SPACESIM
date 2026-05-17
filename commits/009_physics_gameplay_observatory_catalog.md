# 009: Physics-driven gameplay, home observatory, catalog as meta-game, Channel 16 discovery extension

Land the four refinements that close Session B's procgen-and-discovery cluster. Two new section 3 subsections (Physics-driven gameplay with six categories; Home observatory as the player's primary discovery infrastructure) appended after `### Goal structure`. One new section 6 subsection (Catalog as long-term meta-game) appended after `### Map view hierarchy`. One paragraph appended to the existing `### Channel 16 broadcasts` subsection in section 3 (Discovery announcement transmissions).

This commit resolves commit 007's forward references to the catalog. After this commit lands, the three forward references from `### Detection mechanics` ("Body enters the catalog as a candidate", "The catalog grows", "Old mysteries can reactivate") are backed by the named `### Catalog as long-term meta-game` subsection in section 6, which commits the catalog contents (every observed object with current observation stage 0-8, discovery history, anomaly status, player tags, discovery names, cross-references), the catalog's gameplay functions (sorting and filtering as exploration planning, old-data re-analysis when new instruments unlock, catalog as save identity, catalog export and sharing, catalog as Channel 16 broadcast content), and the catalog's role as the meta-game of the long playthrough.

The Channel 16 discovery announcement paragraph extends an existing bullet rather than replacing it. The existing second bullet in `### Channel 16 broadcasts` (from commit 004b) already summarizes the discovery-announcement mechanic; the new paragraph adds detail (explicit broadcast format including discovery name + observation stage + headline findings, explicit broadcast scope distinction across open-science vs competitive, explicit density and warp-handling rules). Mild overlap is accepted because the existing locked content is preserved verbatim; the new paragraph is a detailed expansion of the summary bullet rather than a replacement.

The physics-driven gameplay subsection commits six categories of physics as substrate rather than as separate features. Each category produces gameplay consequences across procgen, observation tiers, mission planning, tech tree progression, and emergent narrative. Stellar physics, magnetic phenomena, radio and electromagnetic phenomena, gravitational phenomena, atmospheric and chemical phenomena, time and history phenomena — each is paragraph-length with specific gameplay handles. The closing "How these integrate" paragraph names the integration surfaces: procgen (the 14-stage pipeline produces physics-coherent output), observation (each tier reveals additional physics-relevant information), tech tree (better magnetometers detect weaker fields), transmissions (physics-driven content), mission planning (physics-driven hazards become real constraints).

The home observatory subsection commits the player's primary discovery infrastructure. Capability optimization is genuinely on-theme here as the explicit exception to the minimal-tycoon positioning. Player choices are named: what to build and where (ground / space / lunar), which instruments to build (optical / infrared / radio / X-ray / gamma-ray / gravitational wave / particle), whether to build interferometers, maintenance and servicing. Integration with existing systems is named: tech tree, resource system, base system, research (consistent with bounded autonomous evolution), mission planning. The home observatory is the mid-to-late-game system the player has been building toward across Phase 7.

Section 3 grows from 20 subsections to 22, ending with Physics-driven gameplay → Home observatory rather than with Goal structure. The motivation → universe → tool reading flow is coherent at the section close. Section 6 grows from 4 subsections to 5, ending with Catalog as long-term meta-game.

## Scope

- `docs/CONSTRAINTS.md` — modified. Four logical edits applied as one atomic write:
  - Section 3: new `### Physics-driven gameplay` subsection appended after `### Goal structure`. Six bolded physics category paragraphs plus closing integration paragraph.
  - Section 3: new `### Home observatory` subsection appended immediately after `### Physics-driven gameplay`. LOCKED commitment, four player-choice areas, five integration points with existing systems.
  - Section 3: `### Channel 16 broadcasts` subsection extended with one new `**Discovery announcement transmissions.**` paragraph at the end (after the existing single-player paragraph). Existing content preserved verbatim.
  - Section 6: new `### Catalog as long-term meta-game` subsection appended after `### Map view hierarchy`. LOCKED commitment, catalog-contents bullet list, five italicized gameplay-function paragraphs.

## Rationale

Section 1's "Discovery as gameplay, grounded in real astrophysics" pillar (commit 006) commits the direction; commit 007 specifies how discovery operates as a mechanism; commit 008 lands the procgen architecture that produces what is discovered; commit 009 closes the loop by specifying what threads through the discovered universe (physics-driven gameplay), what tool the player uses to discover it (home observatory), and what becomes of the accumulated discoveries (catalog as meta-game).

The physics-driven gameplay subsection makes explicit a discipline that has been implicit throughout commits 006-008: the universe is physics, and gameplay emerges from physical principles. The six categories cover the major axes (stellar, magnetic, radio/EM, gravitational, atmospheric/chemical, temporal). Each category produces a real gameplay consequence rather than a flavor note: red dwarf flare activity sterilizes surfaces; magnetar fields create no-fly zones; gravitational wave events are detectable transients; methane-oxygen disequilibrium is a stronger biosignature than either alone; light from distant stars is old. The closing integration paragraph names where this substrate surfaces in player experience (procgen, observation, tech tree, transmissions, mission planning).

The home observatory subsection makes the player's primary discovery infrastructure a substantial system rather than a side mechanic. Capability optimization is genuinely on-theme here — the explicit exception to the minimal-tycoon positioning from commit 004b — because optimizing the observatory directly serves the discovery loop. The player is not making more money; they are seeing more of the universe. The player choices (what to build, which instruments, interferometric arrays, maintenance) and integration points (tech tree, resources, base system, research, mission planning) make the system concrete rather than aspirational. The home observatory is the mid-to-late-game system that the catalog produces.

The catalog as long-term meta-game subsection commits the catalog as the player's accumulated record. Late-game catalogs have thousands of entries. The catalog functions as gameplay through sorting and filtering (exploration planning), old data re-analysis (new instruments process old observations at improved sensitivity), catalog as save identity (two players with the same seed have different catalogs based on what they investigated), catalog export and sharing (human-readable format, community tooling, no first-party online sharing at v1 but format supports it), catalog as Channel 16 broadcast content (multiplayer discoveries flow into other agencies' catalogs as third-party data). The catalog is what makes a long-playthrough save uniquely the player's.

The Channel 16 discovery announcement paragraph extends an existing bullet with format detail. The existing second bullet in `### Channel 16 broadcasts` (from commit 004b) summarized: agencies broadcast when reaching new observation tier or resolving anomaly; other agencies add entries to their catalog as third-party data. The new paragraph adds: explicit broadcast format (discovery name + observation stage + headline findings), explicit broadcast scope (open-science broadcasts everything; competitive broadcasts only strategic findings), explicit density and warp-handling (broadcasts accumulate in the log at high warp; only configured "important" types interrupt warp). The paragraph reads as expansion of the summary bullet rather than replacement; mild overlap is accepted to preserve the existing locked content verbatim.

The placement of physics-driven gameplay and home observatory at the end of section 3 (after `### Goal structure`) is deliberate. Goal structure describes the player's motivations; physics-driven gameplay describes what the universe presents to those motivations; home observatory describes the player's primary tool for engaging with what the universe presents. The motivation → universe → tool progression closes section 3 coherently. The alternative (placing physics-driven gameplay and home observatory before goal structure to keep goal structure as the closing thought) would put the universe's substrate after its motivation, which reads less well.

The catalog placement at the end of section 6 (after `### Map view hierarchy` from commit 008) is also deliberate. Map view hierarchy describes navigation across spatial scales (galactic / system / body / construction); catalog describes navigation across the body of accumulated knowledge. The two are paired as navigation infrastructure; reading them adjacent makes the pairing visible.

## Changes

### Edit 1: Section 3, append new `### Physics-driven gameplay` subsection after `### Goal structure`

Inserted after the closing sentence of `### Goal structure` (which ends with the colony-pilot-and-her-sister hypothetical) and before `## 4. Resources, bases, and logistics`. The new subsection contains:

- LOCKED commitment that six categories of physics-driven gameplay are substrate, not features.
- Six bolded category paragraphs: Stellar physics, Magnetic phenomena, Radio and electromagnetic phenomena, Gravitational phenomena, Atmospheric and chemical phenomena, Time and history phenomena.
- Closing "How these integrate" paragraph naming integration surfaces (procgen, observation, tech tree, transmissions, mission planning).

### Edit 2: Section 3, append new `### Home observatory` subsection after `### Physics-driven gameplay`

Inserted immediately after `### Physics-driven gameplay` and before `## 4.`. The new subsection contains:

- LOCKED commitment that the home observatory is the player's primary discovery infrastructure.
- Minimal-tycoon exception paragraph (capability optimization is genuinely on-theme here).
- Four player-choice italicized headers: What to build and where, Which instruments to build, Whether to build interferometers, Maintenance and servicing.
- Five integration-with-existing-systems italicized bullets: Tech tree, Resource system, Base system, Research, Mission planning.
- Closing paragraph naming the home observatory as the mid-to-late-game system.

### Edit 3: Section 6, append new `### Catalog as long-term meta-game` subsection after `### Map view hierarchy`

Inserted at the end of section 6, after the existing `### Map view hierarchy` subsection (from commit 008) and before `## 7. Difficulty and accessibility`. The new subsection contains:

- LOCKED commitment that the catalog is the player's accumulated record of engagement.
- Catalog contents bullet list (six bullets): observation stage 0-8, discovery history, anomaly status, player tags, discovery names, cross-references.
- Five italicized gameplay-function paragraphs: Sorting and filtering as exploration planning, Old data re-analysis, Catalog as save identity, Catalog export and sharing, Catalog as Channel 16 broadcast content.

### Edit 4: Section 3, `### Channel 16 broadcasts` — append `**Discovery announcement transmissions.**` paragraph

Appended after the existing single-player paragraph (which closes the subsection from commit 004b) and before the next subsection `### Home system evolves autonomously` (from commit 004c). The new paragraph adds explicit broadcast format detail, scope distinction (open-science vs competitive), and density/warp-handling rules. The existing two bullets and the single-player closing paragraph are preserved verbatim.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### New-content anchored heading checks

1. `### Physics-driven gameplay` anchored heading count is 1.
2. `### Home observatory` anchored heading count is 1.
3. `### Catalog as long-term meta-game` anchored heading count is 1.

### Physics-driven gameplay content

4. All six category lead-ins present, each exactly once: `**Stellar physics.**`, `**Magnetic phenomena.**`, `**Radio and electromagnetic phenomena.**`, `**Gravitational phenomena.**`, `**Atmospheric and chemical phenomena.**`, `**Time and history phenomena.**`.

### Home observatory content

5. Literal phrase `The home observatory is the player's primary discovery infrastructure` is present.
6. Literal phrase `capability optimization, not resource optimization` is present.
7. Literal phrase `The home observatory is the mid-to-late-game system` is present.

### Catalog content (resolves commit 007 forward references)

8. Literal phrase `The catalog of discovered objects is the player's accumulated record of engagement with the universe` is present.
9. Literal phrase `When a new instrument tier unlocks, the catalog can re-analyze old observations at the new resolution` is present (resolves commit 007's "Old mysteries can reactivate" forward reference).
10. Literal phrase `Every observed object with its current observation stage (0-8)` is present (resolves commit 007's catalog-grows and stage-1-enters-catalog forward references).
11. Italicized sub-headers `*Catalog as save identity.*` and `*Catalog as Channel 16 broadcast content.*` each present.

### Channel 16 discovery announcement extension

12. Literal `**Discovery announcement transmissions.**` paragraph lead-in is present.
13. The new paragraph contains the literal phrase `The broadcast includes the discovery name (if named), the body's current observation stage, headline findings`.
14. The existing closing paragraph `In single-player, the agency has no peers to broadcast to and Channel 16 carries only the agency's own internal comms` is still present (preserved verbatim).
15. The existing second bullet `Discovery announcements are a natural transmission type — when an agency reaches a new observation tier on a body, or resolves an anomaly, the announcement may be broadcast on Channel 16` is still present (preserved verbatim, despite overlap with the new paragraph).

### Section ordering

16. Section 3's complete subsection list, in document order, is exactly: Parts and vessel construction, Failure modes, Automation and scripting, Information asymmetry / progressive discovery, Detection ecosystem, Platforms for detection, Discovery progression — eight stages, Detection mechanics, Anomaly types worth seeding, Anomalies and mysteries, Interstellar travel: tiered tech progression, Director perspective, Crew and characters, EVA as temporary character control, Transmissions and world communication, Channel 16 broadcasts, Home system evolves autonomously, Multiplayer as shared universe, Mode-portable designs and templates, Goal structure, Physics-driven gameplay, Home observatory. Total: 22 subsections.
17. Section 6's complete subsection list, in document order, is exactly: Patterns to use, First-hour experience, Information legibility, Map view hierarchy, Catalog as long-term meta-game. Total: 5 subsections.

### Preserved-content anchors

18. Commit 008 content preserved: anchored heading counts of 1 each for `### Generation architecture`, `### The 14-stage pipeline (Layer 5)`, `### Hybrid pipeline-plus-grammar`, `### Planet variety tiers`, `### Feature-layer architecture`, `### The 90/9/1 anomaly distribution`, `### Galaxy scope`, `### Map view hierarchy`. Literal `90% resolve to interesting-but-understandable phenomena` and `1% resolve to once-per-playthrough hero moments` present.
19. Commit 007 content preserved: anchored heading counts of 1 each for `### Detection ecosystem`, `### Platforms for detection`, `### Discovery progression — eight stages`, `### Detection mechanics`, `### Anomaly types worth seeding`. Forward reference `The 90/9/1 distribution rule (section 5)` still present (now resolves through commit 008).
20. Commit 006 content preserved: anchored heading counts of 1 each for `### Per-body physics parameter set` (in its commit-008 location between Galaxy scope and Resource distribution) and `### Layered engagement framework`. Design pillars list contains `- Discovery as gameplay, grounded in real astrophysics.` and `- Physics-grounded substrate.`
21. Commit 004a/b/c content preserved: anchored heading counts of 1 each for `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Tonal framing for game modes`, `### Channel 16 broadcasts`, `### Home system evolves autonomously`. Literal `Save files are mode-locked at creation`, `1/8 real scale`, `within bounded limits that prevent passive accumulation`, `**Network-capacity rule.**`, `**Phase 0 deliverable: netcode contract.**` each present. Anchored headings for Phase 2 and Phase 7 amended titles each count 1.
22. Commit 005 content preserved: `### Phase exit criteria`, `**Mobile shipping note.**`, `FoundationPrimitives` companion doc reference.
23. Commit 003 content preserved: `**Agency-based observation sharing.**`, `detection-aggressiveness parameter`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.`
24. Commit 002 content preserved: `### Foundational architectural principles` anchored heading count 1; literal `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `sharp and symmetric`, `Authoritative state replication is the multiplayer model for PhysX-active vessels` each present. Five numbered principles in FAP.
25. Commit 001 content preserved across untouched sections: distinctive phrases per section (Pixar register, Jeb legend, Hill sphere, 20%/80%, KSP's first hour, Dwarf Fortress / RimWorld, physics fidelity toggle, vertical slice MVP, placeholder cube, siren song, suggested repo layout, institutional memory, doc-driven dev, kraken returning, pre-flight checklist, KSP 2's path, stale doc syndrome, Living document, Last comprehensive update). Each present exactly once.
26. Section 9 final-three-bullets adjacency: `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX` in order. Section 9 bullet count: 13.
27. All 14 numbered `## N.` section headings still present.
28. File line count is 1166 (post-008 was 1095 lines; this commit added 71 net lines).

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/009_physics_gameplay_observatory_catalog.md
```
