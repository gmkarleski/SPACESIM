# 013: Deepen section 3 with mission/campaign/engineering structure and sensors

Add seven pieces of design content to section 3 (Gameplay systems) that take existing system commitments and make them concrete in ways that produce engineering decisions. All additive. Two extensions to existing locked subsections (Crew physical-location, Ground-truth verification); five new subsections (Sensors as vessel components, Mission planning, Campaigns, Engineering documentation, Failure forensics).

The thread running through this commit is the operational layer that organizes existing systems for late-game play. Missions are the persistent unit of player work. Campaigns are the multi-mission containers above missions. Engineering documentation accumulates across vessel-design iterations. Failure forensics turns telemetry-on-failure into a player-facing investigation loop. Sensors-as-components connects vessel design to mission outcomes. Crew-physical-location makes crew rotation an engineering decision rather than a UI abstraction. Ground-truth verification surfaces the differences between remote observation and direct measurement.

Each addition deepens what's already there. No replacements. No retractions. Section 3 grows from 25 to 30 subsections (five new) plus two paragraph-level extensions to existing subsections.

## Scope

- `docs/CONSTRAINTS.md` — seven additions to section 3 (Gameplay systems):
  - Extend `### Crew and characters` with the "Crew are physically located on vessels" commitment paragraph
  - Extend `### Information asymmetry / progressive discovery` with the "Ground-truth verification" paragraph
  - Insert new `### Sensors as vessel components` after `### Parts and vessel construction`
  - Insert new `### Mission planning as structured persistent activity` after `### Goal structure`
  - Insert new `### Campaigns as multi-mission programs` after Mission planning
  - Insert new `### Engineering documentation produced by play` after Campaigns
  - Insert new `### Failure forensics` after Engineering documentation

## Rationale

The commits 006-012 series established the system commitments: layered engagement, agency-sharing on observation, dramatic shapes, sensor parts in the vocabulary, telemetry-on-failure, anomaly distributions, the home observatory, transmissions and world communication. What was missing was the operational layer that sits above these systems in late-game play.

Late-game players have many concurrent activities. Without structure, the player loses orientation: which vessel is doing what; what's the current state of the Mars program; which design has the track record I should trust; what went wrong with that mission. The five new subsections in commit 013 supply that operational layer. Missions are the persistent unit. Campaigns are the multi-mission containers. Engineering documentation is design-attached history. Failure forensics is the post-incident review verb. Sensors-as-components is what connects vessel design to mission outcomes.

These are deliberately structural overlays rather than new mechanics. Missions organize existing systems. Campaigns organize missions. Engineering documentation tracks designs that already existed. Failure forensics uses telemetry that's already being recorded. The work in this commit is naming structures the player needs at scale, not adding fundamentally new gameplay.

The two paragraph-level extensions are smaller. Crew-physical-location was implicit but unstated — it gets stated as a locked commitment so the engineering consequences (life support, crew rotation logistics, stranded-crew failure modes) are unambiguous. Ground-truth verification was implicit in the detection ecosystem but unstated as a player-visible reward for direct investigation — it gets stated so the gameplay loop "observe remotely → travel there → confirm or revise" has explicit doc backing.

The new-subsection cluster ordering (missions → campaigns → engineering documentation → failure forensics) was an explicit placement decision per the reconciled source material. Campaigns sit immediately after Mission planning because campaigns are containers for missions. Engineering documentation and Failure forensics sit after the mission/campaign pair because they are the design-attached and telemetry-attached records that play produces — both feeding into the same operational-layer loop.

Sensors as vessel components is placed after Parts and vessel construction (rather than after the failure-forensics cluster) because sensors are a category of parts and the adjacency makes the connection immediate. Section 3 is long, but local clustering keeps it navigable.

## Changes

Seven edits applied in a single atomic write. The file grew from 1458 lines to 1532 lines (+74 net lines; +7,795 bytes).

### Edit 1: Extend `### Crew and characters`

Append after the existing commit-002 / commit-006 / commit-012 content:

```markdown
**Crew are physically located on vessels.** Each crew member is at a specific location at any given time — aboard a specific vessel, at a specific base, or in transit. To move a crew member between locations, you launch a vessel that carries them. Crew rotation is a real logistics problem: shifting a crew member from the home base to Mars requires a vessel going there with capacity for them; returning them requires a return vessel; rotating long-duration crews requires scheduled crew-transport routes.

This makes crew rotation an engineering decision rather than a UI abstraction. A crewed mission that fails strands the crew. A crewed vessel needs life support for everyone aboard. Crew assignment to a long mission means those individuals are unavailable for other missions until they return. The dramatic shapes (commit 012) that involve crew — the rescue, the disaster, the cost — emerge from the physicality.
```

### Edit 2: Extend `### Information asymmetry / progressive discovery`

Append after the existing commit-001 base and commit-003 agency-sharing block:

```markdown
**Ground-truth verification.** When a vessel reaches a body that's been observed remotely, the differences between remote observation and direct measurement are surfaced. Remote spectroscopy estimated 80% nitrogen / 20% oxygen; direct atmospheric sampling shows 78% nitrogen / 19% oxygen / 3% argon. Most differences are small (remote observation is mostly accurate). Some are significant (a biosignature confirmed or disconfirmed; a resource estimate revised by 30%; an anomaly resolved). The differences are gameplay — they reveal the limits of remote observation and reward direct investigation.
```

### Edit 3: Insert `### Sensors as vessel components` after `### Parts and vessel construction`

New subsection. Full content (LOCKED): vessels carry sensors that affect what they can detect during flyby, orbital, surface operations; sensor categories include cameras (visible, multi-spectral, hyperspectral), spectrometers (UV, visible, IR), magnetometers, radar, mass spectrometers, particle detectors, seismometers, sample-return systems; each sensor adds mass and complexity; each unlocks specific observation capabilities at the target; mission planning becomes "what do I need to learn at this target, and what sensors do I need to learn it?"; sensors are part categories in the parts vocabulary (commit 011a); procedural part variation produces specific sensor instances from each category.

### Edit 4: Insert `### Mission planning as structured persistent activity` after `### Goal structure`

New subsection. Full content (LOCKED): missions are the persistent unit of player work; a mission is a named, planned, multi-stage activity with objectives, vessels assigned, crew assigned, supply requirements identified, success criteria defined, and a transmission log; missions persist across game-time on the scale of weeks to decades; mission lifecycle states are planning / launched / in transit / on station / returning / complete / failed; missions provide structural organization — vessels are mission-assigned, crew are mission-assigned, supplies feed missions, observations are mission outputs, anomalies trigger new missions, transmissions are mission-tagged; the mission UI is where the player tracks ongoing work; this is structural overlay, not new mechanics.

### Edit 5: Insert `### Campaigns as multi-mission programs` after Mission planning

New subsection. Full content (LOCKED): a campaign is a named long-running goal with multiple missions feeding it; campaigns can span decades of game-time and dozens of missions; campaign state includes name, declared goal, missions belonging to the campaign (planned / in progress / complete / failed), accumulated progress, current bottleneck, key metrics; the campaign UI sits one level above the mission UI; without campaigns, dozens of concurrent missions become unmanageable; campaigns are structural overlay over missions, no new mechanics required.

### Edit 6: Insert `### Engineering documentation produced by play` after Campaigns

New subsection. Full content (LOCKED): every vessel design accumulates a history (total launches, success rate, failure modes encountered, modifications made, missions served on, performance metrics across launches); a successful design with a track record becomes a tool the player trusts; a new design feels new because there's no track record; data is attached to the design (not the individual vessel instance); the design is mode-portable (commit 004a); the history is portable with it; players sharing designs can include or strip the history; connects to dramatic shapes (the long-trusted design that finally fails), engineering as the verb, and the catalog (designs searchable alongside discoveries).

### Edit 7: Insert `### Failure forensics` after Engineering documentation

New subsection. Full content (LOCKED): when a vessel fails, the player can investigate; every vessel records telemetry continuously throughout its operation; failure produces a post-incident review showing telemetry leading up to the event, identification of the proximate cause, and where applicable the design or operational decisions that contributed; the investigation is a player verb, not an automatic notification; the player can review carefully or accept the summary; Layer 4 players get raw telemetry, Layer 1 players get the summary; connects real telemetry and procedural failure modes (commit 011a) into a player-facing loop; failures become learning opportunities; the dramatic shape "the disaster" gains forensic depth.

## Verification

All checks below must pass. They verify both the added content and the preservation of unrelated content.

### New content present

- Section 3's `### Crew and characters` contains the string `**Crew are physically located on vessels.**`
- Section 3's `### Crew and characters` contains the phrase `Crew rotation is a real logistics problem`
- Section 3's `### Information asymmetry / progressive discovery` contains the string `**Ground-truth verification.**`
- Section 3's `### Information asymmetry / progressive discovery` contains the phrase `Remote spectroscopy estimated 80% nitrogen`
- File contains exactly one anchored heading `^### Sensors as vessel components$`
- The `### Sensors as vessel components` subsection lists all eight sensor categories: Cameras, Spectrometers, Magnetometers, Radar, Mass spectrometers, Particle detectors, Seismometers, Sample-return systems
- File contains exactly one anchored heading `^### Mission planning as structured persistent activity$`
- The Mission planning subsection contains the lifecycle states list: `planning, launched, in transit, on station, returning, complete, failed`
- File contains exactly one anchored heading `^### Campaigns as multi-mission programs$`
- The Campaigns subsection contains the phrase `Mars colonization campaign`
- File contains exactly one anchored heading `^### Engineering documentation produced by play$`
- The Engineering documentation subsection contains the phrase `Heavy Lifter Mk IV, 47 successful launches`
- File contains exactly one anchored heading `^### Failure forensics$`
- The Failure forensics subsection contains the phrase `post-incident review`

### Structural counts

- Section 3 has exactly 30 `### ` subsections (was 25 before commit 013; five new added: Sensors, Mission planning, Campaigns, Engineering documentation, Failure forensics)
- Section 1 has exactly 13 `### ` subsections (unchanged)
- Section 2 has exactly 15 `### ` subsections (unchanged)
- Section 4 has exactly 6 `### ` subsections (unchanged)
- Section 5 has exactly 12 `### ` subsections (unchanged)
- Section 6 has exactly 5 `### ` subsections (unchanged)
- Total file is approximately 1530 ± 5 lines

### Cluster ordering

In section 3, the following four subsections appear in this exact order (Mission planning → Campaigns → Engineering documentation → Failure forensics), with no other `### ` subsection between them:

```
### Mission planning as structured persistent activity
### Campaigns as multi-mission programs
### Engineering documentation produced by play
### Failure forensics
```

The `### Sensors as vessel components` subsection appears immediately after `### Parts and vessel construction` (no other `### ` subsection between them).

### Preserved content from prior commits (cross-section anchors)

Pick distinctive phrases from sections this commit did not modify. All must still be present.

- Section 1, `### Things we are explicitly NOT doing`, still contains the phrase `realistic life-support failure modeling`
- Section 2, `### Coordinate system`, still contains the phrase `50 km from the origin`
- Section 2, `### Reference frame hierarchy`, still contains the phrase `Galactic-rest`
- Section 2, `### Foundational architectural principles`, still contains the phrase `Determinism is a property of the simulation outside PhysX-active mode`
- Section 2, `### Multiplayer architecture preparation`, still contains the phrase `deterministic simulation outside PhysX-active mode`
- Section 4, `### Resource set`, still contains the phrase `Mass and delta-v as central currencies`
- Section 4, `### Research as asynchronous progression`, still contains the phrase `asynchronous progression` (this section is replaced by commit 015 — verify still present before commit 015 lands)
- Section 5, `### Tuning is the hard part`, still contains the phrase `90/9/1` (from commit 008)
- Section 6, `### First-hour experience`, still contains the phrase `start small, build up`
- Section 8 (Build order), still contains exactly eight phase descriptions `#### Phase 0` through `#### Phase 8`
- Section 9, still contains the line `Crew abstraction (Phase 6): RESOLVED` (will be removed in a later cleanup commit; at this commit it must still be present)
- Section 10, `### Suggested repo layout`, still contains the phrase `commits/`
- Section 11, `### Strategy: docs per system, not per file`, still contains the phrase `companion doc`
- Section 12, `### Mandatory reading order`, still contains the phrase `Standard session start prompt template`
- Section 13, `### Project-shape failures`, still contains the phrase `simulation that is unfun to play`
- Section 14, still contains the phrase `Last comprehensive update`

### Preserved content from prior commits in section 3 (the section this commit modifies)

These anchors verify section 3 content from earlier commits was not damaged by this commit's additions.

- Section 3, `### Parts and vessel construction`, still contains the commit-011a phrase `procedural part variation`
- Section 3, `### Crew and characters`, still contains the original commit-001 LOCKED tag for the three character functions (Stakes / Scale / Emotional payload)
- Section 3, `### Crew and characters`, still contains the commit-012 phrase about dramatic shapes
- Section 3, `### Information asymmetry / progressive discovery`, still contains the commit-003 phrase about agency-sharing
- Section 3, `### Goal structure`, still contains its original content from commit 001
- Section 3, `### Anomalies and mysteries`, still contains the commit-008 phrase `90/9/1`
- Section 3, `### Physics-driven gameplay`, still contains the commit-006 phrase about Layer 1 through Layer 4
- Section 3, `### Detection mechanics`, still contains the commit-007 phrase about wavelength categories
- Section 3, `### Transmissions and world communication`, still contains the commit-004b phrase about warp handling (will be extended by commit 014 — verify still present before commit 014 lands)

If any of the new-content checks fail, the commit did not land. If any structural-count or cluster-ordering check fails, an edit was misplaced or duplicated. If any preserved-content check fails, the commit damaged earlier content and must be reverted.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/013_section_3_deepening.md
git commit -F commits/013_section_3_deepening.md
```
