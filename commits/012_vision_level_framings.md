# 012: Vision-level framings — play styles, design strategy, dramatic shapes, world-is-alive, home as load-bearing, investment depth

Land the closing commit of the seven-commit reconciled-source series. Six new subsections commit vision-level framings that organize how the systems built across commits 001-011b work together. Five subsections land at the end of section 1 (six play styles supported by the design, design strategy commitment, dramatic shapes the game produces, world-is-alive-via-player-decisions principle, home system as load-bearing design). One subsection lands at the end of section 3 (investment depth as graduated game response).

These commitments tie together the earlier commits and make explicit the philosophical commitments that have been implicit. The six play styles name who the game serves and how those player types map to system depth. The design strategy commitment names the two strategies in combination (Strategy 5 refused breadth + Strategy 1 deep vocabulary) and what's rejected. The dramatic shapes commitment names the eleven story shapes the game produces naturally without authoring plots. The world-is-alive principle names the design discipline that the world's autonomous behavior is mostly the consequence of player decisions, not random events. The home system as load-bearing commitment binds 30-60 hours of pre-interstellar engagement to home-system design effort. The investment depth subsection commits the game's graduated response to player engagement signals.

This commit is purely additive — no existing locked content is replaced. All cross-references resolve to existing content from commits 001-011b. There are no forward references because this is the closing commit of the series.

After this commit lands, the constraints document fully represents the Phase 0 design state at 1458 lines / 156KB. Section 1 has 13 subsections (vision); section 3 has 25 subsections (gameplay systems); the doc covers fourteen numbered sections from Vision through Document status. Phase 1 implementation can begin with confidence after this lands. The only remaining Phase 0 work is the operational scaffolding (PHASE_TRACKER.md, DECISIONS.md, ARCHITECTURE.md, SESSION_PROTOCOL.md, companion doc template, systems-by-phase list) and the netcode contract deliverable.

## Scope

- `docs/CONSTRAINTS.md` — modified. Six logical edits applied as one atomic write:
  - Section 1: five new subsections appended at end of section 1 (after `### Configurable starting conditions` from commit 010): `### Six play styles supported by the design`, `### Design strategy commitment`, `### Dramatic shapes the game produces`, `### World-is-alive-via-player-decisions principle`, `### Home system as load-bearing design`. Section 1 grows from 8 to 13 subsections.
  - Section 3: one new subsection appended at end of section 3 (after `### Dual-use technology framework` from commit 011a): `### Investment depth as graduated game response`. Section 3 grows from 24 to 25 subsections.

## Rationale

The six new section 1 subsections are vision-level commitments that organize how earlier-commit systems work together. None of them introduce new mechanics; each commits a vision-level discipline that the existing systems implement.

The six play styles subsection answers "who is this game for?" by naming six distinct play styles (engineer, explorer, colonist, strategist, narrator, completionist) and how each maps to existing system depth. The engineer plays the procedural parts system and Vizzy scripting; the explorer plays the detection ecosystem and 8-stage discovery progression; the colonist plays base modules and supply lines under bounded autonomous evolution; the strategist plays tech progression through engineering gates; the narrator plays director perspective and named-crew time-dilation; the completionist plays the persistent catalog and bounded procgen variety. Players mix freely. Most engage with several. Variety is emergent from system depth, not added as separate features.

The design strategy commitment answers "what kind of game is this?" by naming the two strategies in combination: Strategy 5 (refused breadth, KSP-shaped — explicit non-goals protect focused depth) plus Strategy 1 (deep vocabulary producing emergent variety, Minecraft-shaped — one rich system produces many activities). The subsection also explicitly rejects three alternative strategies (Fallout-shaped, EVE-shaped, GTA-shaped) and seven specific design patterns (combat as gameplay, economy as meta-game, quest systems, RPG mechanics on crew, social mechanics in single-player, discrete activity types). The closing discipline names the implementation rule: when implementation reveals "missing" capability, the first instinct is to deepen existing systems.

The dramatic shapes commitment names eleven story shapes that the game produces when players engage with it: the rescue, the arrival, the discovery, the reach, the disaster, the pivot, the long arc, the cost, the legacy, the encounter with prior humanity, the reach that creates the problem. The game does not author plots; it designs systems honest enough to produce these shapes naturally. The closing implementation-supports paragraph names what each shape needs in implementation: telemetry legibility, time visibility, persistent history, real consequences from real choices, arrival events as moments, catalog and discovery surfacing, anomaly resolution tiered by significance, prior-humanity artifacts that pay off.

The world-is-alive-via-player-decisions principle is the design discipline that ties bounded autonomous evolution to player agency. The world's autonomous behavior during player absence is predominantly the consequence of player decisions, not random events. A supply line failure fails because of how the player designed the route. A colony that grows beyond expectations grows because of how the player configured its automation. The texture of the world while the player is away is the player's own creation. This principle applies across five surfaces (supply line outcomes, base evolution, research progress, crew lives, anomaly investigations) — all of which have existing locked-content support from commits 001 through 011b. Random events still exist (procedural failure modes, stellar weather, mechanical wear) but they operate within the system the player designed.

The home system as load-bearing commitment binds 30-60 hours of pre-interstellar engagement to home-system design effort. The home system must be rich enough to carry that time before interstellar capability arrives. This makes home-system design effort load-bearing rather than decorative. The requirements list names six concrete deliverables: all home system bodies hand-tuned through the full 14-stage pipeline (commit 008); hand-placed Tier A artifacts (commit 010); hand-placed landmarks on home bodies; visual care commensurate with time spent; starting moon base with character and history; deep-space anomalies known but unreachable with starting tech. The closing paragraph names why this matters: the dramatic weight of time-dilation depends on home being a place the player has invested in.

The investment depth subsection in section 3 commits the game's graduated response to player engagement signals. The game can detect player investment depth through observable signals (named crew, bases built at home, time in home system, transmissions monitored vs accumulated, crew biographies read or skipped) and modulate the texture of dilation experiences accordingly without changing gameplay mechanics. A player with high investment signals gets more cinematic arrival moments — more transmissions, more crew interaction, more home colony state changes surfaced. A player with low investment signals gets the same mechanical outcome (mission returns, time has advanced) without the emotional production. This is honest service of different play styles: the engineer gets clean mechanics; the narrator gets full dramatic payoff. Same game, different surfacing.

The placement of all six new subsections is end-of-section to preserve existing subsection ordering: section 1's five additions follow the configurable-starting-conditions subsection (commit 010); section 3's one addition follows the dual-use-technology-framework subsection (commit 011a). Section 1 grows from 8 to 13 subsections (the reconciled source projected ~12); the larger size is acceptable because each subsection is a distinct vision-level commitment that earns its place. Section 3 grows from 24 to 25 subsections, within the reconciled-source projection of 22-24 plus this expected addition.

All cross-references in the new content resolve to existing locked content. The six play styles reference systems from commits 001 (procedural parts, named crew), 003 (Vizzy details), 004b (director perspective), 004c (bounded autonomous evolution), 007 (detection ecosystem, 8-stage discovery), 008 (90/9/1 anomaly distribution), 009 (persistent catalog), 010 (starting artifacts), 011b (supply line systems). The design strategy commitment references external design patterns (KSP, Minecraft, Fallout, EVE, GTA) without doc cross-references. The dramatic shapes commitment references catalog and discovery surfacing (commit 009), anomaly resolution tiered by significance (commit 008), prior-humanity artifacts (commit 010), arrival events as moments (commit 002), telemetry legibility (commit 011a movie-moment mechanics), and persistent history (commit 010 in-media-res). The world-is-alive principle references supply lines (commit 011b), base evolution (commit 004c), research (commit 004c), crew (commit 001 + 004b), anomaly investigations (commits 007 + 008). The home system as load-bearing references the 14-stage pipeline (commit 008), Tier A artifacts (commit 010), and the time-dilation pillar (commit 001 design pillars). The investment depth subsection references named crew (commit 001), bases (commit 010), time in home system, transmissions (commit 004b), crew biographies (commit 001 + 004b).

## Changes

### Edit 1-5: Section 1 — append five new subsections at end of section

Inserted in order after the existing `### Configurable starting conditions` subsection (the closing subsection of section 1 from commit 010) and immediately before `## 2. Foundation`:

1. **`### Six play styles supported by the design`** — six bolded play-style leads (engineer, explorer, colonist, strategist, narrator, completionist) with closing paragraph on emergent variety from system depth.
2. **`### Design strategy commitment`** — LOCKED commitment to Strategy 5 + Strategy 1 combination; three rejected alternatives (Fallout, EVE, GTA); specifically-rejected design patterns; closing implementation discipline.
3. **`### Dramatic shapes the game produces`** — LOCKED commitment that the game designs systems producing eleven dramatic shapes (rescue, arrival, discovery, reach, disaster, pivot, long arc, cost, legacy, encounter with prior humanity, reach that creates the problem); closing implementation supports list.
4. **`### World-is-alive-via-player-decisions principle`** — LOCKED commitment that autonomous behavior during player absence is predominantly the consequence of player decisions; why this matters (decision consequences feel earned, random events feel arbitrary); five application surfaces; random-events-still-exist clarification.
5. **`### Home system as load-bearing design`** — LOCKED commitment to 30-60 hours of pre-interstellar engagement; six concrete requirements; closing paragraph on dramatic weight of time-dilation.

After this edit lands, section 1's complete subsection order is: `### Design pillars`, `### Target player types — all served simultaneously`, `### Things we are explicitly NOT doing`, `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### In-media-res starting state`, `### Logistics-not-tech as primary progression gating`, `### Configurable starting conditions`, `### Six play styles supported by the design`, `### Design strategy commitment`, `### Dramatic shapes the game produces`, `### World-is-alive-via-player-decisions principle`, `### Home system as load-bearing design`. Total: 13 subsections.

### Edit 6: Section 3 — append `### Investment depth as graduated game response` at end of section

Inserted after the existing `### Dual-use technology framework` subsection (commit 011a, the closing subsection of section 3) and immediately before `## 4. Resources, bases, and logistics`. The new subsection contains:

- LOCKED commitment that the game can detect player investment depth through observable signals (named crew, bases built at home, time in home system, transmissions monitored vs accumulated, crew biographies read or skipped) and modulate dilation experience texture without changing gameplay mechanics.
- High-investment-vs-low-investment paragraph: cinematic arrival moments for high-investment players; same mechanical outcomes without emotional production for low-investment players.
- Closing paragraph: this is not manipulation; it is honest service of different play styles; same game, different surfacing.

After this edit lands, section 3's last subsection is `### Investment depth as graduated game response`. Total: 25 subsections.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### New-content anchored heading checks

1. `### Six play styles supported by the design` anchored heading count is 1.
2. `### Design strategy commitment` anchored heading count is 1.
3. `### Dramatic shapes the game produces` anchored heading count is 1.
4. `### World-is-alive-via-player-decisions principle` anchored heading count is 1.
5. `### Home system as load-bearing design` anchored heading count is 1.
6. `### Investment depth as graduated game response` anchored heading count is 1.

### Six play styles content

7. All six play style leads present, each exactly once: `**The engineer.**`, `**The explorer.**`, `**The colonist.**`, `**The strategist.**`, `**The narrator.**`, `**The completionist.**`.
8. Closing phrase `Variety is emergent from system depth, not added as separate features` is present.

### Design strategy commitment content

9. Literal LOCKED phrase `The design follows two strategies in combination: refused breadth (Strategy 5: explicit non-goals protect focused depth, KSP-shaped) plus deep vocabulary producing emergent variety (Strategy 1: one rich system produces many activities, Minecraft-shaped)` is present.
10. All three rejected-strategy literal phrases present: `(Fallout-shaped)`, `(EVE-shaped)`, `(GTA-shaped)`.
11. Literal `**Specifically rejected from this design:**` is present.

### Dramatic shapes content

12. LOCKED phrase `The game does not author plots. It designs systems that produce certain dramatic shapes naturally` is present.
13. All eleven dramatic-shape lead-ins present, each exactly once: `**The rescue.**`, `**The arrival.**`, `**The discovery.**`, `**The reach.**`, `**The disaster.**`, `**The pivot.**`, `**The long arc.**`, `**The cost.**`, `**The legacy.**`, `**The encounter with prior humanity.**`, `**The reach that creates the problem.**`.
14. Literal `**Specific supports needed in implementation:**` is present.

### World-is-alive content

15. LOCKED phrase `The world's autonomous behavior during player absence is predominantly the consequence of player decisions, not random events` is present.
16. All five application surface bullets present: `Supply line outcomes (route failures trace to craft design choices)`, `Base evolution (autonomous expansion within bounded rules the player configured)`, `Research progress (advances when scientists were assigned, paused otherwise)`, `Crew lives (relationships and aging follow from the missions and assignments the player chose)`, `Anomaly investigations (auto-completing during absence based on the resources the player committed)`.
17. Closing phrase `Random events still exist (procedural failure modes, stellar weather, mechanical wear)` is present.

### Home system as load-bearing content

18. LOCKED phrase `The home system must be rich enough to carry 30-60 hours of player engagement before interstellar capability arrives` is present.
19. 14-stage pipeline reference present: `All home system bodies hand-tuned through the full 14-stage pipeline`. Tier A artifact reference present: `Hand-placed Tier A artifacts (Voyager-equivalent, ISS-equivalent, Apollo sites, Mars-equivalent landers, the deep-space anomaly)`.
20. Closing phrase `The dramatic weight of time-dilation depends on home being a place the player has invested in` is present.

### Investment depth content

21. LOCKED phrase `The game can detect player investment depth through observable signals` is present.
22. Closing phrase `Same game, different surfacing based on what the player has signaled they care about` is present.

### Section ordering checks

23. Section 1's complete subsection list, in document order, is exactly: `### Design pillars`, `### Target player types — all served simultaneously`, `### Things we are explicitly NOT doing`, `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### In-media-res starting state`, `### Logistics-not-tech as primary progression gating`, `### Configurable starting conditions`, `### Six play styles supported by the design`, `### Design strategy commitment`, `### Dramatic shapes the game produces`, `### World-is-alive-via-player-decisions principle`, `### Home system as load-bearing design`. Total: 13 subsections.
24. Section 3's last subsection is `### Investment depth as graduated game response`. Total: 25 subsections.

### Preserved-content anchors

25. Commit 011b content preserved: anchored heading count of 1 for `### Life support model`. Literal `Eleven physical resources at v1 plus categorical samples`, `Supply lines operate as scheduled real vessels rather than abstract throughput parameters`, `- Storage (VAB-resident; resources buffer in VAB capacity per `+'`### Supply lines`'+`)` each present.
26. Commit 011a content preserved: anchored heading counts of 1 each for `### Scaling discipline`, `### Crater primitives and bounded terrain modification`, `### Movie-moment mechanics`, `### Dual-use technology framework`.
27. Commit 010 content preserved: anchored heading counts of 1 each for `### In-media-res starting state`, `### Logistics-not-tech as primary progression gating`, `### Configurable starting conditions`. Literal `The rocket equation is the gate` present.
28. Commit 009 content preserved: anchored heading counts of 1 each for `### Physics-driven gameplay`, `### Home observatory`, `### Catalog as long-term meta-game`.
29. Commit 008 content preserved: anchored heading counts of 1 each for `### Generation architecture`, `### The 14-stage pipeline (Layer 5)`, `### Hybrid pipeline-plus-grammar`, `### Planet variety tiers`, `### Feature-layer architecture`, `### The 90/9/1 anomaly distribution`, `### Galaxy scope`, `### Map view hierarchy`.
30. Commit 007 content preserved: anchored heading counts of 1 each for `### Detection ecosystem`, `### Platforms for detection`, `### Discovery progression — eight stages`, `### Detection mechanics`, `### Anomaly types worth seeding`.
31. Commit 006 content preserved: anchored heading counts of 1 each for `### Per-body physics parameter set`, `### Layered engagement framework`. Section 1 design pillars list contains `- Discovery as gameplay, grounded in real astrophysics.` and `- Physics-grounded substrate.`
32. Commits 004a/b/c content preserved: anchored heading counts of 1 each for `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### Channel 16 broadcasts`, `### Home system evolves autonomously`. Literal `Save files are mode-locked at creation`, `1/8 real scale`, `within bounded limits that prevent passive accumulation`, `**Phase 0 deliverable: netcode contract.**` each present. Anchored headings for Phase 2 and Phase 7 amended titles each count 1.
33. Commit 005 content preserved: `### Phase exit criteria`, `**Mobile shipping note.**`, `FoundationPrimitives` companion doc reference.
34. Commit 003 content preserved: `**Agency-based observation sharing.**`, `detection-aggressiveness parameter`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.`
35. Commit 002 content preserved: `### Foundational architectural principles` anchored heading count 1; literal `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `sharp and symmetric` each present. Five numbered principles in FAP.
36. Commit 001 content preserved across untouched sections: distinctive phrases per section (Pixar register, Jeb legend, Hill sphere, 20%/80%, KSP's first hour, Dwarf Fortress / RimWorld, vertical slice MVP, placeholder cube, siren song, suggested repo layout, institutional memory, doc-driven dev, kraken returning, pre-flight checklist, KSP 2's path, stale doc syndrome, Living document, Last comprehensive update). Each present exactly once.
37. Section 9 final-three-bullets adjacency: `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX` in order. Section 9 bullet count: 13.
38. All 14 numbered `## N.` section headings still present.
39. File line count is 1458 (post-011b was 1370 lines; this commit added 88 net lines).

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/012_vision_level_framings.md
```
