# 014: Add observation mechanics cluster and operational chatter framework

Add three new subsections to section 3 (Gameplay systems) that make observation a concrete gameplay loop, and extend `### Transmissions and world communication` with an operational-chatter framework that gives the world ambient texture without demanding action.

The three new subsections form a cluster inside the detection ecosystem block: `### Observation as structured activity` (observation is a scheduled activity with target / instrument / configuration / duration choices, queued and executed over game-time), `### Wavelengths and filters as concrete gameplay` (each wavelength category from the commit 007 detection ecosystem has concrete filter and configuration choices that produce different data), and `### Observation results as interpretable data` (observations produce raw data the player interprets through layered-engagement analysis tools per commit 006). The cluster lands immediately after `### Detection mechanics` and immediately before `### Anomaly types worth seeding`, sitting inside the detection ecosystem cluster where it belongs.

The transmissions extension appends an operational-chatter framework to the existing subsection. The framework names a class of routine radio that fills the world with activity without demanding action (supply craft entering orbit, mission milestones, science updates, engineering updates, crew personal touches, weather, status changes). The chatter system uses the same underlying transmission infrastructure (density rules, warp handling, Channel 16 routing) but defaults differently: chatter logs without interrupting warp; transmissions can interrupt. Personal entities (named crew, player-named vessels, player-built infrastructure) get chatter using their names — a small detail with disproportionate impact on player attachment. Configurable by category so players who want quiet can have quiet.

This commit's logical scope is operationalizing observation as a player verb and giving the world ambient voice. Both are refinements that take existing system commitments (the detection ecosystem from commit 007, transmissions from commit 004b) and make them concrete in gameplay terms.

## Scope

- `docs/CONSTRAINTS.md` — four edits to section 3:
  - Insert new `### Observation as structured activity` immediately after `### Detection mechanics`
  - Insert new `### Wavelengths and filters as concrete gameplay` immediately after `### Observation as structured activity`
  - Insert new `### Observation results as interpretable data` immediately after `### Wavelengths and filters as concrete gameplay`
  - Extend `### Transmissions and world communication` with the operational-chatter framework (operational chatter, configurable by category, personal entities, chatter system uses transmission infrastructure) appended at the end of the existing subsection

## Rationale

Detection mechanics from commit 007 named the detection ecosystem (wavelength categories, platforms, eight-stage discovery progression) and the player-facing verbs (filter switching, "what is this" puzzle, distance and time effects on measurements). What was missing was the *activity* — how does the player actually take an observation? What are the components of an observation? What does it look like to run multiple observations in parallel during a long mission?

The three-subsection observation-mechanics cluster supplies that activity layer. Observation is a scheduled activity: pick target, pick instrument, pick configuration, pick duration, queue it, work on something else until it completes. Each wavelength has concrete filter and configuration choices (hydrogen alpha, sodium D, near-IR, gamma-ray transient detection) that produce different data and reward different play styles. Results arrive as raw data — spectra, images, light curves — that the player interprets through layered analysis tools per commit 006. Layer 1 players get the interpretation; Layer 4 players get the raw data and can catch what the default analysis missed.

The cluster lands inside the detection ecosystem block (after Detection mechanics, before Anomaly types worth seeding) because observation is what the player does with the platforms, stages, and mechanics already established. The natural read order through section 3's detection block is now: Detection ecosystem → Platforms for detection → Discovery progression → Detection mechanics → Observation as structured activity → Wavelengths and filters → Observation results → Anomaly types. The cluster slots in cleanly between the mechanics that govern detection and the anomaly categories that detection produces.

The transmissions extension addresses a different gap. Commit 004b's transmissions subsection named transmissions as the world's voice and specified warp-handling behavior (routine transmissions accumulate during warp without interrupting; player-configured "important" types drop warp). But it left open what *fills* the transmission log between crises. Operational chatter is the answer: routine radio that gives the world ambient texture without demanding action. Supply craft entering orbit, mission milestones, science session updates, engineering status, crew personal touches. The chatter system uses the same underlying transmission infrastructure (same density rules, same warp handling, same Channel 16 routing) but defaults to "logs without interrupting" rather than "interrupts warp on crisis." Players who want quiet can configure categories off; players who want immersion get the world filled with activity.

The "personal entities get chatter" commitment matters more than its size suggests. Named crew get personal touches in the chatter. Player-named vessels get chatter using their names — "Lucky Strike entering Mars LEO" feels different than "Vessel 34567." This is how player attachment to named entities forms: through hearing about them in routine traffic.

## Changes

Four edits applied in a single atomic write. The file grew from 1532 lines to 1591 lines after the write plus a one-paragraph repair (described under "Note on repair" below); net +59 lines, +6,027 bytes.

### Edit 1: Insert `### Observation as structured activity` after `### Detection mechanics`

New subsection. LOCKED. Observation is a scheduled activity with components: target selection (body, region, coordinates, or specific phenomenon), instrument selection (which observatory instrument or vessel sensor), wavelength / mode selection, duration / integration time (longer integration = lower signal threshold = fainter detections; shorter = faster results), priority / queue position (limited instrument time forces scheduling decisions). Observations execute over game-time. Results arrive as transmissions or accumulate in the catalog. The home observatory (commit 009) provides the player's primary observation infrastructure; vessel sensors (commit 013) provide observation during missions; both use the same observation-as-scheduled-activity model.

### Edit 2: Insert `### Wavelengths and filters as concrete gameplay` after Observation as structured activity

New subsection. LOCKED. Each wavelength category from the detection ecosystem (commit 007) has concrete filter and configuration choices that produce different data. Visible light: broadband filters, narrow filters, specific line filters (hydrogen alpha, sodium D, oxygen, methane). Infrared: mid-IR (heat from cool objects), near-IR (cooler stars, dust-penetrating), cold-detector requirement for space-based. Ultraviolet: hot phenomena, atmospheric absorption above water vapor / oxygen / ozone — requires above-atmosphere observation. X-ray: hot gas, accretion, stellar coronae, energy resolution as key parameter. Gamma-ray: highest-energy phenomena, transient detection critical. Radio: frequency selection across MHz to THz, lunar far-side advantage, interferometric baselines. Particle / gravitational wave: specialized detectors with less filter-switching, more on/off. Layer 1 players (commit 006) accept default filters and get useful results; Layer 4 players configure specific filters and extract more from the same instruments.

### Edit 3: Insert `### Observation results as interpretable data` after Wavelengths and filters

New subsection. LOCKED. Observations produce raw data that the player interprets through analysis tools. A spectrum is a graph of intensity vs wavelength. An image is a 2D array of pixels with metadata. A light curve is brightness over time. The player's role in interpreting data is real, not abstracted away. Layered engagement (commit 006) applies directly: Layer 1 sees the interpretation; Layer 2 sees data alongside the interpretation; Layer 3 uses data to make predictions; Layer 4 works with raw data directly and builds Vizzy scripts to automate analysis pipelines. Some observations produce data the analysis tools cannot fully interpret — that's where anomalies emerge. The 90/9/1 distribution rule (commit 008) governs how often unresolved data points resolve to interesting outcomes.

### Edit 4: Extend `### Transmissions and world communication` with operational chatter framework

Append five paragraphs at the end of the existing subsection (preserving all prior content from commits 004b and 009 verbatim, including the warp-handling rules paragraph and the inline reference to `### Channel 16 broadcasts`). The appended content names operational chatter (routine radio fills the world with activity without demanding action; categories include supply craft, mission milestones, science updates, engineering updates, crew personal touches, weather and conditions, status changes); states the configurability rule (configurable by category — vessel operations, base operations, mission progress, crew chatter, observatory sessions, external chatter; defaults: chatter logs but does not interrupt warp); commits to personal entities getting chatter (named crew get personal touches, player-named vessels get chatter using their names, player-built infrastructure transmits status — this is how attachment forms); and frames the chatter system as using the same underlying transmission infrastructure with different defaults.

### Note on repair

The initial atomic write applied the four edits but introduced one corruption to existing locked content. The existing transmissions subsection ended with a paragraph containing the inline reference `(see \`### Channel 16 broadcasts\` below).`. The naive append-after-rstrip pattern truncated this paragraph at the opening backtick and orphaned the remainder onto its own line below the new chatter content, where it appeared (because it began with `###`) as a malformed h3 heading. A follow-up repair restored the original paragraph verbatim and removed the orphan stub. The repair was the only corrective operation needed before verification passed; both the pre-repair damage and the post-repair restoration are documented here so the failure mode is recorded.

The corruption is a worked example of why the verification battery must include line-anchored heading-count checks (not just substring presence checks): a malformed heading that began with `###` and looked like a valid h3 in plain-text reading was caught only by counting actual section-3 h3s (which came out one higher than expected) and by inspecting the heading list directly.

## Verification

All checks below must pass. They verify added content, cluster ordering, preserved content from this commit's predecessors, and the structural integrity of section 3 after the edits and the repair.

### New content present

- File contains exactly one anchored heading `^### Observation as structured activity$`
- File contains exactly one anchored heading `^### Wavelengths and filters as concrete gameplay$`
- File contains exactly one anchored heading `^### Observation results as interpretable data$`
- `### Observation as structured activity` contains the phrase `scheduled activity, not a passive readout`
- `### Observation as structured activity` contains the bullet label `Priority / queue position`
- `### Wavelengths and filters as concrete gameplay` contains the phrase `hydrogen alpha, sodium D, oxygen, methane`
- `### Wavelengths and filters as concrete gameplay` contains the phrase `Frequency selection across MHz to THz`
- `### Wavelengths and filters as concrete gameplay` contains the wavelength category `Particle / gravitational wave`
- `### Observation results as interpretable data` contains the example `78% N2, 19% O2, 3% Ar`
- `### Observation results as interpretable data` contains the phrase `Vizzy scripts`
- `### Observation results as interpretable data` contains the cross-reference `90/9/1 distribution rule (commit 008)`
- `### Transmissions and world communication` contains the new heading text `**Operational chatter.**`
- `### Transmissions and world communication` contains the new heading text `**Personal entities get chatter.**`
- `### Transmissions and world communication` contains the example `Tanker-34567 entering Mars LEO`
- `### Transmissions and world communication` contains the example `Officer Reyes celebrated 50th birthday`
- `### Transmissions and world communication` contains the heading text `**Configurable by category:**`
- `### Transmissions and world communication` contains the example `Lucky Strike entering Mars LEO`

### Damage-repair anchors

- No line in the file matches the regex `^### Channel 16 broadcasts\` below\)\.$` (the malformed-heading shape that the initial write produced, removed by repair)
- The file contains exactly one occurrence of the restored inline reference `(see \`### Channel 16 broadcasts\` below).`
- The file contains exactly one line-anchored heading `^### Channel 16 broadcasts$`

### Cluster ordering

In section 3, the four headings appear in this exact order with no other `### ` subsection between them:

```
### Detection mechanics
### Observation as structured activity
### Wavelengths and filters as concrete gameplay
### Observation results as interpretable data
### Anomaly types worth seeding
```

### Transmissions: existing content preserved verbatim

The transmissions subsection still contains (verbatim from commits 004b and 009):

- The paragraph beginning `**Differentiated audio/visual signatures by type.**`
- The paragraph beginning `**Configurable attention filtering.**`
- The paragraph beginning `**Density and warp handling.**`
- The verbatim sentence: `Only player-configured "important" transmission types (crises by default; player-tunable) automatically drop warp`

And operational chatter appears strictly AFTER the `**Density and warp handling.**` paragraph.

### Structural counts

- Section 3 has exactly 33 `### ` subsections (was 30 after commit 013; +3 from this commit: Observation as structured activity, Wavelengths and filters, Observation results)
- Section 1 has exactly 13 `### ` subsections (unchanged)
- Section 2 has exactly 15 `### ` subsections (unchanged)
- Section 4 has exactly 6 `### ` subsections (unchanged)
- Section 5 has exactly 12 `### ` subsections (unchanged)
- Section 6 has exactly 5 `### ` subsections (unchanged)
- Total file length: 1585–1605 lines

### Commit 013 content preserved

This commit must not damage commit 013's additions. All of the following must still be present:

- The string `**Crew are physically located on vessels.**`
- The phrase `Crew rotation is a real logistics problem`
- The string `**Ground-truth verification.**`
- The phrase `Remote spectroscopy estimated 80% nitrogen`
- Exactly one anchored heading `^### Sensors as vessel components$`
- The phrase `Sample-return systems`
- Exactly one anchored heading `^### Mission planning as structured persistent activity$`
- The lifecycle states list `planning, launched, in transit, on station, returning, complete, failed`
- Exactly one anchored heading `^### Campaigns as multi-mission programs$`
- The phrase `Mars colonization campaign`
- Exactly one anchored heading `^### Engineering documentation produced by play$`
- The phrase `Heavy Lifter Mk IV, 47 successful launches`
- Exactly one anchored heading `^### Failure forensics$`
- The phrase `post-incident review`
- Cluster ordering: Mission planning → Campaigns → Engineering documentation → Failure forensics
- Adjacency: Parts and vessel construction immediately precedes Sensors as vessel components

### Cross-section preserved-content anchors

The following distinctive phrases from earlier commits must still be present in their original sections:

- Section 1 contains the design pillar phrase `Engineering as the verb`
- Section 1 contains the explicit-exclusion phrase `Combat. Different game, different audience.`
- Section 1 contains the phrase `Mining is a consequence of placing a module on a body`
- Section 2 contains the phrase `Floating origin shift threshold: 50 km default`
- Section 2 contains the phrase `Hierarchical frames — Sun, planet, moon, vessel`
- Section 2 contains the phrase `Patched conics within stellar systems`
- Section 2 contains the phrase `Three-mode system`
- Section 4 contains the phrase `Mass and delta-v as central currencies`
- Section 4 contains the phrase `asynchronous progression` (the Research subsection — to be replaced by commit 015)
- Section 5 contains the phrase `90/9/1`
- Section 6 contains the phrase `first hour of play matters more than any other hour`
- Section 8 contains the headings `### Phase 0 — Decisions` and `### Phase 8 — Polish, content, expansion`
- Section 8 contains the phrase `netcode contract`
- Section 9 contains the open question `Aerodynamic model`
- Section 10 contains the path `docs/CONSTRAINTS.md`
- Section 11 contains the phrase `companion doc`
- Section 12 contains the phrase `Standard session start prompt template`
- Section 13 contains the failure-mode phrase `Engine rewrite temptation at month six`
- Section 13 contains the AI-failure-mode phrase `Drift across sessions`

### Section 3 prior-commit content preserved

These section-3 anchors verify nothing in section 3 was damaged by this commit's additions plus repair:

- Section 3 references `procedural` and `parts` (commit 011a vocabulary)
- Section 3 contains the three crew functions `Stakes` / `Scale` / `Emotional payload` (commit 001 base)
- Section 3 references `90/9/1` in the anomalies context (commit 008)
- Section 3 contains both `Layer 1` and `Layer 4` (commit 006 layered engagement)
- Section 3 contains the phrase `Filter switching as a core verb` (commit 007 detection mechanics)
- Section 3 contains the heading `### Goal structure`

If any check fails, the commit did not land correctly. If the damage-repair anchors fail specifically, the repair regressed and the malformed-heading bug returned; this would block commit 015 work because section 3 would be in a non-canonical state.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/014_observation_mechanics_and_chatter.md
git commit -F commits/014_observation_mechanics_and_chatter.md
```
