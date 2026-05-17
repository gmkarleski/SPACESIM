# 004b: Vision and texture layers — tonal framing, scale, time-warp, perspective, transmissions

Land the vision-and-texture cluster of refinements from Phase 0's second design pass. Ten edits across sections 1, 2, 3, and 7 establish the player-facing character of the game: what the modes feel like (tonal framing for single-player vs. multiplayer; minimal-tycoon positioning), the scale at which gameplay operates (1/8 in-system default; real-scale interstellar), the player's relationship to time (continuous time-warp with 100,000× uniform max; "warp to next event" as a navigation feature reading the priority queue from commit `002`), the player's perspective (director, not captain-as-avatar; EVA as a brief and explicit exception), and the channel through which the world communicates with the player (diegetic radio transmissions following the Marine Channel 16 reference model, with multiplayer Channel 16 broadcasts as the inter-agency texture layer).

The scale change (KSP-style 1/10 → Standard 1/8) is the only edit that supersedes previously-locked numerical values. The architectural commitment that body parameters are data, not hardcoded constants, is preserved verbatim as the closing sentence of the rewritten subsection, so the change is to the default parameter set rather than to the architecture. Old values (`~600 km radius`, `~3,400 m/s` to orbit) are removed from the doc.

Two new subsections in section 1 grow the Vision section by approximately thirty lines. Section 1 is the appropriate home for tonal framing and minimal-tycoon positioning because both are commitments about the character of the game itself, not about systems within it. Six new subsections in section 3 add the player-facing texture layer: Director perspective (before Crew and characters, establishing the player's viewpoint before describing whom they direct), EVA (after Crew and characters, the explicit exception to vessel-level direction), Transmissions and world communication (the densest single piece, ten paragraphs), Channel 16 broadcasts (the multiplayer inter-agency surface), all inserted between Crew-and-characters and the existing commit-`004a` subsections (Multiplayer as shared universe and Mode-portable designs and templates). The reading order through section 3 is preserved as a coherent flow: parts → failure modes → automation → information asymmetry → anomalies → interstellar tiers → director perspective → crew → EVA → transmissions → Channel 16 → multiplayer-as-shared-universe → mode-portable designs → goal structure.

Section 7's difficulty toggle list is updated to match the new scale presets (Casual 1/10, Standard 1/8, Realistic 1/1). One bullet replacement; no other section 7 changes.

## Scope

- `docs/CONSTRAINTS.md` — modified. Ten edits across sections 1, 2, 3, 7:
  - Section 1: two new subsections appended after `### Things we are explicitly NOT doing` (`### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`).
  - Section 2: `### Solar system scale` subsection's DEFAULT block fully rewritten for 1/8 scale with three preset toggles; new `### Interstellar distances` subsection inserted immediately after. New `### Time-warp in single-player` subsection inserted between `### Mode transitions and event scheduling` (from commit `002`) and `### Time representation`.
  - Section 3: four new subsections inserted (`### Director perspective` before `### Crew and characters`; `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts` after `### Crew and characters` and before the commit-`004a` subsection `### Multiplayer as shared universe`).
  - Section 7: difficulty toggle list bullet for solar system scale replaced with the new preset names.

## Rationale

The previous commit cluster (`002`, `003`, `004a`) established the architectural foundation. This commit establishes the experiential character that runs on that foundation.

Tonal framing for game modes is the vision-level commitment that prevents the natural complaint that multiplayer is a diminished version of single-player. Single-player is contemplative and time-dilated by design intent; multiplayer is coordinated and shared by design intent; neither is the other's lesser cousin. Naming this in section 1 means future design work cannot drift into treating multiplayer as a feature-deficit version of single-player, and gives the doc a citation for any future discussion of "why doesn't multiplayer have X."

Minimal-tycoon positioning carves out what the game is not. Resources serve gameplay decisions, not economic optimization. No money, no contracts, upgrades as capabilities rather than numerical tweaks. The position is paired with the bounded-autonomous-evolution rules that will land in commit `004c` — automation is rich within bounded limits, not unbounded accumulation. The game is positioned as an expedition/exploration game where running the agency's space program is in service of finding out what is there.

The 1/8 scale change tightens the engineering envelope without breaching first-hour design targets. KSP-tuned intuitions remain almost right at 1/8 (slightly off, giving experienced players something new to learn) while the more demanding delta-v budget gives mass margins enough weight to create real engineering decisions. The casual/standard/realistic preset structure makes the choice a difficulty surface rather than a forced choice. The architectural commitment that body parameters are data is preserved.

Interstellar real-scale distances are the structural answer to the "warp defeats FTL" problem. With real-scale distances between stars and real relativistic transit velocities, a tier 2 probe to a Proxima-equivalent star takes real-time hours to days at max warp, gated by physics rather than by an imposed warp cap. In-system distances remain compressed at the chosen solar system scale because in-system gameplay requires tractable distances for active mission play; the two scales differ because the gameplay contexts differ, and real space exhibits exactly this ratio (solar systems are tiny compared to interstellar gaps), so the design is more realistic than uniform scaling, not less.

Continuous time-warp with a uniform 100,000× maximum and "warp to next event" as a navigation feature on top of the priority queue establishes single-player time advancement as a player-driven dial rather than a contextual cap. The priority queue from commit `002` already exists; this subsection names the UI feature that reads it and clarifies the architectural seam (one queue, multiple consumers).

Director perspective makes explicit what was previously only implied by the doc's lack of captain-as-avatar language. The player operates at vessel/agency level, witnesses time-dilation rather than experiencing it first-person, and feels its weight through investment in their crew. The Apollo 13 reference grounds the perspective in real history.

EVA is the one explicit exception to vessel-level direction. PhysX-active simulation, first or third-person view, non-deterministic outcomes sampled into authoritative state at sim-tick boundaries per the commit `002` determinism boundary. EVA is per-player exploration content; it does not produce authoritative shared state beyond the sample-collected / flag-planted / data-recorded boundary.

Transmissions are the diegetic mechanism for the world to communicate with the player. The Marine Channel 16 reference model — a shared frequency monitored by everyone, used sparingly, with brevity and recognizable signal types — gives the design a specific shape. Persistence in a log, differentiated audio/visual signatures, configurable attention filtering, and game-clock density (one per 5–15 game-minutes, regardless of warp rate) make the system make the world feel alive without making it feel demanding. The unifying principle: the world has texture and voice; the player chooses what to attend to.

Channel 16 broadcasts are the multiplayer inter-agency surface of the transmission system. Cross-agency observation-sharing rules from commit `003` govern who hears what; discovery announcements are a natural broadcast type; broadcast scope is configurable; density and warp-handling follow the same rules as other transmissions.

Two parenthetical cross-references to commit `004c` (home-system evolution) were considered for the minimal-tycoon and transmissions subsections but removed during proposal review. The relevant sentences read as self-contained claims; the cross-references will be implicit once commit `004c` lands and a reader of section 1 or section 3 can find the home-system evolution material in section 3 by reading order.

## Changes

### Edit 1: Section 1, new subsection `### Tonal framing for game modes`

Inserted at the end of section 1, after `### Things we are explicitly NOT doing` and before `## 2. Foundation`. Three paragraphs:

- Single-player and multiplayer are tonally different games sharing the same systems and universe. Single-player is the contemplative, time-dilated, asynchronous narrative; multiplayer is the coordinated, shared, immediate cooperative or competitive experience.
- Both are valuable; neither is a diminished version of the other. Players choosing a mode are choosing a kind of story.
- The multiplayer architecture in section 3 makes the mechanical commitment that supports this tonal commitment. The tonal commitment in the Vision section is the reason; the architecture is the mechanism.

### Edit 2: Section 1, new subsection `### Minimal-tycoon, rich-progression positioning`

Inserted immediately after `### Tonal framing for game modes`. Lead-in plus five-bullet list plus closing paragraph. The list specifies: no money or universal currency; no contracts or quests; upgrades are capabilities not optimizations; automation is rich within bounded autonomous evolution; difficulty toggles available for tycoon-curious players (resource scarcity, mass margin requirements, life support, crew salaries or training costs). Closing paragraph positions the game as expedition/exploration where the journey is the point.

### Edit 3: Section 2, `### Solar system scale` subsection — full rewrite of DEFAULT block

The existing DEFAULT block is replaced:

- Old: `KSP-style scale (~1/10 real). Home Kerbin-equivalent ~600 km radius with ~9.8 m/s² gravity. ~3,400 m/s to low orbit.` followed by the KSP-style-scale rationale.
- New: `1/8 real scale. Home Kerbin-equivalent ~800 km radius, surface gravity ~9.8 m/s², delta-v to orbit ~4,000 m/s.` followed by the new rationale (slightly more demanding than KSP but reachable for new players given information legibility; the goal is meaningful mass margins, not difficulty) and a three-preset toggle list (Casual 1/10, Standard 1/8, Realistic 1/1), plus a closing sentence preserving the locked architectural commitment that body parameters are data.

Old numerical values (`~600 km radius`, `~3,400 m/s` to orbit) are removed from the doc.

### Edit 4: Section 2, new subsection `### Interstellar distances`

Inserted immediately after `### Solar system scale`. Four paragraphs: the DEFAULT commitment (1× to 5× real-world distances between stars, not scaled down to match in-system); rationale (real relativistic velocities + real-scale distances produce realistic journey durations naturally); the contextual-scale distinction (in-system compressed, interstellar real-scale, because the two contexts have different gameplay needs and real space exhibits the same ratio); the multiplayer note (spatial warp operates over the same distances with journey duration determined by warp velocity).

### Edit 5: Section 2, new subsection `### Time-warp in single-player`

Inserted between `### Mode transitions and event scheduling` (from commit `002`) and `### Time representation`. Four paragraphs:

- LOCKED commitment: continuous time-warp is the primary single-player time advancement mechanic.
- Maximum time-warp rate target 100,000× uniformly. The warp-defeats-FTL problem is solved by real-scale interstellar distances, not by capping warp rates.
- "Warp to next event" as a navigation feature reading the same priority queue used by the simulation. One queue, multiple consumers.
- Free time-warp is single-player only; multiplayer uses a coordinated time multiplier.

### Edit 6: Section 3, new subsection `### Director perspective`

Inserted before `### Crew and characters`. Four paragraphs:

- LOCKED commitment: the player operates at vessel/agency level. The player exists outside the game world as the director of the agency.
- Crew are named characters with their own proper-time clocks; the player witnesses time-dilation rather than experiencing it first-person.
- This is the perspective of mission control, not the astronaut — the perspective of every space program in human history. Apollo 13's drama was experienced by mission control as much as by the astronauts.
- The reading order through the next two subsections: director establishes the viewpoint, crew describes who the director directs, EVA describes the explicit exception.

### Edit 7: Section 3, new subsection `### EVA as temporary character control`

Inserted after `### Crew and characters` (and before the new `### Transmissions and world communication` subsection). Three paragraphs:

- LOCKED commitment: EVA is a contextual control mode for flag-planting, surface exploration, Armstrong-moment scenarios. The director temporarily takes direct control of a named crew member. PhysX-active simulation, first or third-person view.
- EVA outcomes are non-deterministic in the same way other PhysX-active outcomes are non-deterministic per the commit `002` foundational architectural principles. EVA state changes that matter are sampled into authoritative state at sim-tick boundaries.
- Returns to vessel-level director control when EVA ends.

### Edit 8: Section 3, new subsection `### Transmissions and world communication`

Inserted after `### EVA as temporary character control`. Ten paragraphs:

1. LOCKED headline: the world communicates with the player through diegetic radio transmissions, not modal alerts.
2. Reference model: Marine Channel 16. Short, recognizable, only sometimes important.
3. Transmissions during active flight: ambient radio chatter, periodic notable transmissions, audio plus brief visual indicator, player can attend / ignore / review later.
4. Persistence: all transmissions persist in a searchable comms log regardless of attention.
5. Differentiated audio/visual signatures: quiet ambient for routine, present for notable, insistent for crises. Players learn the texture of "something important happened."
6. Configurable attention filtering: player settings control which transmission types reach notice and which trigger automatic warp-drop.
7. Density and warp handling: game-clock density (one per 5–15 game-minutes), accumulate silently at high warp, only player-configured "important" types interrupt warp.
8. Unifying principle: the world has texture and voice; the player chooses what to attend to. The world does not demand engagement, it offers it.
9. This replaces any earlier events-at-calibrated-rates framing as a gating mechanism. Journey-gating is now structurally handled by real-scale interstellar distances.
10. Multiplayer note: same role, with cross-agency Channel 16 broadcasts adding inter-agency texture.

### Edit 9: Section 3, new subsection `### Channel 16 broadcasts`

Inserted after `### Transmissions and world communication` and before `### Multiplayer as shared universe`. LOCKED commitment plus four-bullet mechanics list plus single-player note. The mechanics list specifies: each agency can broadcast; cross-agency sharing rules from commit `003` govern who receives; discovery announcements as a natural broadcast type with third-party catalog entries; configurable broadcast scope; same density and warp-handling as other transmissions. Single-player note: Channel 16 carries the agency's own internal comms; mechanism same, audience is the agency itself.

### Edit 10: Section 7, difficulty toggle list — replace solar system scale bullet

The bullet `- Solar system scale (KSP-scale / intermediate / realistic)` is replaced with `- Solar system scale (Casual 1/10 / Standard 1/8 / Realistic 1/1, with custom intermediate scales available)`. No other section 7 changes.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### Headings anchored to start-of-line (avoid cross-reference false positives)

1. Section 1 contains exactly one `### Tonal framing for game modes` heading.
2. Section 1 contains exactly one `### Minimal-tycoon, rich-progression positioning` heading.
3. Section 2 contains exactly one `### Interstellar distances` heading.
4. Section 2 contains exactly one `### Time-warp in single-player` heading.
5. Section 3 contains exactly one `### Director perspective` heading.
6. Section 3 contains exactly one `### EVA as temporary character control` heading.
7. Section 3 contains exactly one `### Transmissions and world communication` heading.
8. Section 3 contains exactly one `### Channel 16 broadcasts` heading.

Use anchored regex `^### <heading name>$` for these checks. Substring grep will match cross-references in body prose and produce false-positive counts.

### Tonal framing and minimal-tycoon content

9. The phrase `Both are valuable; neither is a diminished version of the other` is present.
10. The phrase `multiplayer is a different experience by design intent, not by technical limitation` is present.
11. The phrase `The game is not a tycoon` is present.
12. The phrase `No money or universal currency` is present.
13. The phrase `The progression is real but it's not the point. The point is the journey.` is present.

### Scale change content

14. The phrase `1/8 real scale` is present.
15. The phrase `~800 km radius` is present.
16. The phrase `~4,000 m/s` is present.
17. The phrase `~600 km radius` is ABSENT from the doc.
18. The phrase `~3,400 m/s` is ABSENT from the doc.
19. The preset labels `**Casual (1/10):**`, `**Standard (1/8):**`, `**Realistic (1/1):**` are each present.
20. The phrase `All body parameters remain data (not hardcoded constants)` is present (architectural commitment preserved).

### Interstellar distances content

21. The phrase `1× to 5× real-world distances between stars` is present.
22. The phrase `Real space exhibits exactly this ratio — solar systems are tiny compared to interstellar gaps` is present.

### Time-warp single-player content

23. The phrase `Maximum time-warp rate in single-player is uniformly high (target: 100,000×)` is present.
24. The phrase `Free time-warp is single-player only` is present.
25. The phrase `The UI feature reads from the same priority queue used by the simulation; both consume one source` is present.

### Director perspective and EVA content

26. The phrase `The player exists outside the game world as the director of the agency` is present.
27. The phrase `Apollo 13's drama was experienced by mission control as much as by the astronauts` is present.
28. The phrase `EVA outcomes are non-deterministic in the same way other PhysX-active outcomes are non-deterministic` is present.

### Transmissions content

29. The phrase `The world communicates with the player through diegetic radio transmissions, not through modal alert notifications` is present.
30. The phrase `Marine Channel 16 — a shared frequency monitored by everyone, used sparingly` is present.
31. The phrase `**Transmissions persist in a log.**` is present.
32. The phrase `Transmissions fire at game-clock density (one per 5–15 game-minutes of routine play, regardless of warp rate)` is present.
33. The phrase `the world has texture and voice; the player chooses what to attend to` is present.

### Channel 16 broadcasts content

34. The phrase `Channel 16 broadcasts are a shared communication channel in multiplayer` is present.

### Section 7 toggle update

35. The bullet `- Solar system scale (Casual 1/10 / Standard 1/8 / Realistic 1/1, with custom intermediate scales available)` is present exactly once.
36. The bullet `- Solar system scale (KSP-scale / intermediate / realistic)` is ABSENT.

### Section 3 subsection order (the most important structural check)

37. The complete sequence of `### ` headings within section 3, in document order, is exactly: `### Parts and vessel construction`, `### Failure modes`, `### Automation and scripting`, `### Information asymmetry / progressive discovery`, `### Anomalies and mysteries`, `### Interstellar travel: tiered tech progression`, `### Director perspective`, `### Crew and characters`, `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts`, `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Goal structure`. Verify by extracting all `^### .+$` lines from the section 3 range and comparing as a list.

### Preserved-content anchors (per workflow rule)

38. Commit `001` content preserved across untouched section anchors: `Pixar register, not Goat Simulator register` (S3), `this is how Jeb became a legend` (S3), `Code is 20% of the work` (S5), `Hill sphere spacing, frost line` (S5), `This is the vertical slice MVP` (S8), `placeholder cube launches from a planet surface` (S8), `siren song of pretty screenshots` (S10), `Suggested repo layout` (S10), `project's institutional memory` (S11), `Critical practice: doc-driven development` (S11), `this is the kraken returning` (S12), `Pre-flight checklist before generating code` (S12), `KSP 2's path` (S13), `Stale doc syndrome` (S13), `Last comprehensive update: Phase 0 design crystallization` (S14). Each present exactly once.
39. Commit `002` content preserved: `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `Authoritative state replication is the multiplayer model for PhysX-active vessels`, `sharp and symmetric`. Anchored heading count: exactly one `### Foundational architectural principles`. Five numbered principles in the FAP subsection.
40. Commit `003` content preserved: `detection-aggressiveness parameter`, `**Agency-based observation sharing.**`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.` Each present exactly once.
41. Commit `004a` content preserved: anchored heading counts of exactly one each for `### Multiplayer as shared universe` and `### Mode-portable designs and templates`; phrases `Save files are mode-locked at creation` and `parameterized by game mode and propulsion tier` each present exactly once.
42. Section 9 final-three-bullets adjacency check: the last three bullets in section 9 must be, in this order, `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX`.
43. All 14 numbered `## N.` section headings still present.
44. File line count in the range 815–845. The post-`004a` baseline was 726 lines; this commit added approximately 100 lines.

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/004b_vision_and_texture_layers.md
```
