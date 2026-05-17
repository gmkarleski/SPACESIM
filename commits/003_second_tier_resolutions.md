# 003: Second-tier issue resolutions and agency terminology

Land three of the four second-tier issue resolutions identified at the start of the Phase 0 session, plus a small terminology cleanup that pairs naturally with Issue #6. The resolutions address: SOI transition detection aggressiveness as a difficulty-toggleable parameter (Issue #3); agency-based observation sharing as the data model for who has knowledge about a celestial body (Issue #6); and Vizzy script sleep semantics during Kepler-rails time-warp, paired with the alert system as the foundational monitoring channel (Issue #8). Issue #10's time-warp authority question is deferred to commit `004`, where it will be resolved as part of the multiplayer architecture rework.

The terminology cleanup is included here because Issue #6 introduces "agency" as a load-bearing noun in the doc, and the only existing phrase using "space program" to refer to the organization (section 1's curious-newcomer bullet) reads inconsistently with the new term. One mechanical replacement removes the inconsistency at the moment the new term is introduced. A user-facing entry for the physics-fidelity difficulty toggle is also added to section 7 so the toggle list stays self-consistent with the Issue #3 mechanic introduced in section 2.

## Scope

- `docs/CONSTRAINTS.md` — modified. Five edits across four sections: section 1 (terminology cleanup, one phrase), section 2 (Issue #3 — two paragraphs added to the existing Mode transitions and event scheduling subsection), section 3 (Issue #6 — touch-up to one sentence in Information asymmetry plus new agency-based-sharing block; Issue #8 — new paragraphs at end of Automation and scripting), section 7 (one bullet added to the difficulty toggle list).

## Rationale

These three resolutions were derived through the same option-analysis pattern as the four foundation resolutions in commit `002`. They are smaller in cascading impact and are grouped together because they are mutually independent — none of the three depends on either of the others — and small enough that landing them as one logical change costs less than splitting them.

The agency-based observation model from Issue #6 is the load-bearing piece architecturally. It introduces "agency" as the entity that has memberships, owns observations, and broadcasts discoveries. The agency exists in both single-player (one member: the player) and multiplayer (multiple members possible across multiple agencies), making the data model uniform across modes. Crew on missions are explicitly named as employees of the agency, not members of it — a distinction that matters when multiplayer rules later govern who has authority over what.

Issue #3's tiered SOI detection is structured as a parameter on the predictor architecture from commit `002`, with a difficulty toggle exposing the strict-mode value in Phase 5/6 alongside the broader difficulty toggle system. Phase 1 ships the pragmatic value, which deliberately accepts that some near-tangent SOI grazes will be missed in exchange for predictor performance. This is consistent with the doc's selective-realism pillar: physics realism that creates engineering decisions is good; realism that requires the predictor to chase every grazing edge case adds cost without adding decisions for most players.

Issue #8's resolution keeps Vizzy operating cleanly within the determinism boundary from commit `002`: scripts sleep when their vessel enters Kepler-rails, because there is no well-defined sim-tick-sampled state to read during analytic propagation at warp. Monitoring needs are served by the alert system, which is named here as a first-class foundational system rather than a UI element. The alert system and the analytic event-prediction priority queue from section 2 both predict when conditions become true; the resolution flags this for Phase 5 architectural consideration without committing to whether they share infrastructure or remain separate.

The terminology cleanup is mechanical and small. Section 1's "curious newcomer" bullet previously read "Wants to feel like a space program director." After this commit it reads "Wants to feel like an agency director." The doc's other uses of "space program" refer to the activities the agency runs (as in "your home colony's situation changes"), which is correct under the canonical-noun decision and stays unchanged.

Issue #10 (time-warp authority in multiplayer) is deferred to commit `004` because the resolution depends on the multiplayer architecture rework, which introduces shared game-time with a player-coordinated time multiplier. The "whose clock wins" problem dissolves under that model. Landing #10 here would force a forward reference to language that does not yet exist; landing it in `004` keeps the dependency order clean.

## Changes

### Edit A: Section 1 terminology cleanup

`Wants to feel like a space program director.` → `Wants to feel like an agency director.`

Single phrase, single occurrence, in the "curious newcomer" bullet under Target player types.

### Edit B: Section 2, `### Mode transitions and event scheduling` — predictor aggressiveness

Two new paragraphs added immediately after the existing "Event predictors are pure functions..." paragraph, before the "When a vessel\'s state changes..." paragraph:

> Event predictors have a detection-aggressiveness parameter. Phase 1 ships with two values, exposed later via the difficulty toggle system: a pragmatic default that aggressively detects atmospheric entry and surface impact (missing these is catastrophic for the player) and applies standard root-finding to clear SOI crossings; and a strict mode that applies aggressive detection to all event types including SOI grazing (near-tangent cases). Under default settings, some SOI grazes will be missed — this is a deliberate trade in favor of predictor performance. Players who want full physics accuracy can enable the physics fidelity toggle to receive strict-mode behavior across all predictors.
>
> Phase 1 ships the pragmatic version of each predictor. The difficulty toggle that exposes the strict-mode value becomes available in Phase 5/6 alongside the broader difficulty toggle system from section 7. The predictor architecture supports the toggle from Phase 1; only the user-facing setting is deferred.

### Edit C: Section 3, `### Information asymmetry / progressive discovery` — agency model

Two parts. First, the existing implication sentence is touched up to re-root the observation model on the agency:

`Implication for data model: each body has \'true\' state plus per-player \'observed\' state. UI shows observed state until upgraded by missions.` → `Implication for data model: each body has \'true\' state plus per-agency \'observed\' state, accumulated by the agency\'s instruments and missions. UI shows the active agency\'s observed state until upgraded.`

Second, a new block is appended to the end of the subsection (before `### Anomalies and mysteries`):

> **Agency-based observation sharing.** Observation state is per-agency, not per-player. An "agency" is the player-facing organization the player is running — what does the work and accumulates the knowledge. Each agency has its own observation tier per body and its own catalog of discoveries.
>
> Agency membership refers to human players. Crew on missions are employed by the agency, not members of it: a crew member is a named individual the agency hires and assigns; an agency member is a human player who participates in directing the agency. In single-player, the agency has exactly one human member (the player) plus however many crew the player has assigned. In multiplayer, an agency may have multiple human members; multiple agencies may exist in one multiplayer session with their own memberships.
>
> Cross-agency observation sharing has three modes the multiplayer architecture supports, with the default for any given mode decided when multiplayer ships post-v1:
>
> - **Within an agency, sharing is instant.** All human members of one agency see the same observations as soon as they are made.
> - **Across agencies, sharing routes through comms.** Agency A broadcasts a discovery; agency B receives it after the appropriate comms delay (light-speed for interstellar broadcasts, scaled or instant per difficulty settings for in-system).
> - **In competitive games, cross-agency data may be hidden entirely.** Agency A\'s observations are private to agency A; other agencies must discover independently.
>
> This architecturally supports: shared knowledge (everyone in one agency), asymmetric with instant sharing (each player their own agency with permissive rules), asymmetric with comms delay (each player their own agency with realistic comms), and hybrid team-based play (multiple multi-player agencies). The data model has agencies even when there is one human player; single-player is the trivial reduction where the agency has one member.

### Edit D: Section 3, `### Automation and scripting` — Vizzy sleep + alert system

Three new paragraphs appended to the end of the subsection (before `### Information asymmetry / progressive discovery`):

> **Vizzy scripts do not run during time-warp on Kepler-rails vessels.** Scripts sleep when their vessel enters Kepler-rails mode; they wake when the vessel returns to PhysX-active. This is what makes Vizzy compatible with the determinism boundary from section 2: scripts read sim-tick-sampled authoritative state during PhysX-active flight, which is well-defined; they would have no well-defined read source during high-warp Kepler-rails propagation, where there is no sim-tick sampling of vessel state because the vessel state is being advanced analytically. Vizzy is a flight automation tool, not a monitoring tool.
>
> **Monitoring needs are served by the alert system.** The alert system is a first-class foundational system, not a UI element. Alerts watch for conditions ("fuel below 10%," "periapsis below 70 km," "vessel within 1000 km of target," "communication lost for more than an hour") and notify the player when triggered. Alerts run at warp scale and operate against the same analytic state the priority queue from section 2 operates on. Vizzy scripts can configure alerts as part of their setup — a script runs once during launch to register the alerts it needs, then sleeps when the vessel enters Kepler-rails; the alert system continues watching at warp scale and surfaces notifications when triggered.
>
> The alert system and the analytic event-prediction priority queue from section 2 both predict when conditions become true and fire events. They may share infrastructure when both are built in Phase 5; the specific architectural decision (shared or separate) defers to that phase. The systems should be designed with awareness of each other.

### Edit E: Section 7 — Physics fidelity toggle entry

New bullet added to the end of the existing difficulty toggle list:

> - Physics fidelity (pragmatic / strict): pragmatic skips some near-tangent SOI grazes for predictor performance; strict applies aggressive detection across all event types. See section 2\'s Mode transitions and event scheduling for the underlying mechanic.

## Verification

A future session can confirm this commit landed correctly by checking:

### Issue #3 (predictor aggressiveness)

1. **Section 2\'s `### Mode transitions and event scheduling` subsection** contains the phrase `detection-aggressiveness parameter`.
2. **The same subsection** contains the phrase `physics fidelity toggle` and the statement that Phase 1 ships the pragmatic version with the strict-mode toggle becoming available in Phase 5/6.

### Issue #6 (agency-based observation sharing)

3. **Section 3\'s `### Information asymmetry / progressive discovery` subsection** contains the lead-in `**Agency-based observation sharing.**` as a bolded paragraph opener.
4. **The same subsection** contains the phrase `Agency membership refers to human players` and the phrase `Crew on missions are employed by the agency, not members of it`.
5. **The same subsection** lists three cross-agency sharing modes with the literal lead-ins `Within an agency, sharing is instant`, `Across agencies, sharing routes through comms`, and `In competitive games, cross-agency data may be hidden entirely`.
6. **The same subsection\'s implication sentence has been touched up:** the file contains the phrase `per-agency \'observed\' state` and does NOT contain the phrase `per-player \'observed\' state`.

### Issue #8 (Vizzy + alert system bridge)

7. **Section 3\'s `### Automation and scripting` subsection** contains the phrase `Vizzy scripts do not run during time-warp on Kepler-rails vessels`.
8. **The same subsection** contains the phrase `The alert system is a first-class foundational system, not a UI element`.
9. **The same subsection** contains the phrase `may share infrastructure when both are built in Phase 5`.

### Terminology cleanup

10. **Section 1** contains the phrase `Wants to feel like an agency director.` and does NOT contain `Wants to feel like a space program director.`

### Section 7 toggle entry

11. **Section 7** contains the phrase `Physics fidelity (pragmatic / strict)` as a bullet in the difficulty toggle list.

### Preserved-content anchors

12. **Commit `002` content from section 2 is preserved.** File contains, as literal matches: `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `### Foundational architectural principles`, `sharp and symmetric`. The `### Coordinate system` subsection still does not contain `~10 km`. The `### Foundational architectural principles` subsection still enumerates exactly five numbered principles.
13. **Section 3 retains its commit-`001` content:** the literal phrases `Pixar register, not Goat Simulator register` and `this is how Jeb became a legend` are each present exactly once.
14. **Section 5 retains its commit-`001` content:** literal phrases `Code is 20% of the work` and `Hill sphere spacing, frost line` each present exactly once.
15. **Section 8 retains its commit-`001` content:** literal phrases `This is the vertical slice MVP` and `placeholder cube launches from a planet surface` each present exactly once.
16. **Section 9 retains its commit-`001` content with adjacency check.** The final three bullets in section 9 (after the last `## 9.` heading and before the `## 10.` heading) must be, in this exact order: `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX`. The bullets `Character visual design language` and `Character expressiveness depth` must also be present in section 9.
17. **Section 10 retains its commit-`001` content:** literal phrases `siren song of pretty screenshots` and `Suggested repo layout` each present exactly once.
18. **Section 11 retains its commit-`001` content:** literal phrases `project\'s institutional memory` and `Critical practice: doc-driven development` each present exactly once.
19. **Section 12 retains its commit-`001` content:** literal phrases `this is the kraken returning` and `Pre-flight checklist before generating code` each present exactly once.
20. **Section 13 retains its commit-`001` content:** literal phrases `KSP 2\'s path` and `Stale doc syndrome` each present exactly once.
21. **Section 14 retains its commit-`001` content:** the line beginning `Living document` (at start of paragraph) is present, and the phrase `Last comprehensive update: Phase 0 design crystallization` is present exactly once.
22. **All 14 numbered `## N.` section headings are present.**
23. **File size is in the range 670-720 lines.** A measurement substantially outside this range indicates the commit either added much more content than intended or has corrupted unrelated sections.

If any of these checks fail, the commit has not been correctly applied. The file should be repaired against the source artifact before subsequent commits proceed. Note: the bash-via-Python escape hatch from `commits/README.md` may be required for any repair, because the Write and Edit tools have observed silent-failure modes on this workspace mount; the repair pattern is `read existing → modify in memory → atomic write-then-rename via Python`.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/003_second_tier_resolutions.md
```
