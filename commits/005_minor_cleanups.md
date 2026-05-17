# 005: Minor cleanups — first-hour caveat, RESOLVED bullet removal, companion-doc grouping, Vizzy mobile note, phase exit criteria

Land the five mechanical cleanups identified during initial reading of the constraints document. Each is small, independent, and clarifies an existing commitment rather than introducing a new design decision. The cleanups are grouped as one commit because each is too small to merit its own artifact and they share the "tidy up loose ends" character.

The five items: #9 first-hour caveat on the asynchronous-progression pillar (which had been partially addressed by commit `004c`'s bounded-evolution refinement but still needed an explicit timescale qualifier); #11 remove the RESOLVED crew-abstraction bullet from section 9 per the doc's own status discipline; #12 refine the companion-doc strategy to allow small utilities to be grouped under a single doc rather than each having their own; #13 add a mobile-shipping note to the Vizzy subsection acknowledging that node graph editors are notoriously painful on small touchscreens; #14 add a new Phase exit criteria subsection at the end of section 8 making validation milestones the explicit phase-completion bar.

None of the five edits supersede previously locked content in a way that requires special handling. The first-hour caveat embeds inside an existing pillar bullet that was already refined by commit `004c` (the parenthetical sits inside the third sentence of that bullet). The crew-abstraction removal is a bullet deletion with no replacement; the canonical resolution remains in section 3's `### Crew and characters` subsection and the historical record will live in the DECISIONS log when that file is created in a later session. The companion-doc grouping refinement appends a paragraph after the existing list of categories; the list is preserved verbatim. The Vizzy mobile note appends to the existing `### Automation and scripting` subsection. The Phase exit criteria subsection is net-new content positioned at the end of section 8 before section 9.

## Scope

- `docs/CONSTRAINTS.md` — modified. Five edits:
  - Section 1: asynchronous-progression design pillar bullet has a parenthetical embedded inside its third sentence naming the first-hour caveat and cross-referencing section 6's `### First-hour experience` subsection.
  - Section 9: the RESOLVED crew-abstraction bullet is deleted entirely. Section 9's bullet count goes from 14 to 13. The final-three-bullets adjacency (`Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX`) is preserved because the removed bullet was a middle bullet.
  - Section 11: `### What gets a companion doc` subsection appended with a follow-up paragraph after the existing bullet list, naming the grouping principle (small utilities grouped under a single doc; major systems get their own; grouping decided when each doc is started).
  - Section 3: `### Automation and scripting` subsection appended with a `**Mobile shipping note.**` paragraph naming the node-graph-editor-on-touchscreen difficulty and the read-only-viewer-plus-desktop-authoring pattern.
  - Section 8: new `### Phase exit criteria` subsection appended at the end (after Phase 8's description, before `## 9. Open questions to resolve`). The subsection names validation milestones as the phase-completion bar, lists the phases with explicit binary milestones (Phase 1, Phase 3), describes multi-deliverable exit conditions for Phase 0 / Phase 2 / Phase 4 / Phases 5-8, and closes with the don't-press-into-the-next-phase-if-blocked rule cross-referencing section 13's KSP 2-style failure mode.

## Rationale

The five cleanups were each identified during the initial reading at the start of the Phase 0 session but deferred so that the foundation-cascading decisions (commits `002` through `004c`) could land first. Each is too small to merit its own commit and too independent to fit naturally into any of the four foundation-cascading commits.

The first-hour caveat is the most consequential of the five. The asynchronous-progression pillar's claim that "every session produces visible forward motion" is true after the player has accumulated some agency state (bases producing, research running, supply lines delivering, probes in transit), but it is not true in the first hour, when no asynchronous state exists yet to advance. The pillar was at risk of reading as a universal claim that misleads any reader who notices the contradiction with section 6's locked first-hour design targets. The parenthetical embedded in the pillar makes the timescale explicit and points the reader at section 6 for the first-hour-specific design.

The RESOLVED crew-abstraction bullet was inherited from the source `.docx` and preserved through commit `001`'s faithful conversion. The doc's own discipline ("when an OPEN item is resolved, move it to DEFAULT or LOCKED" in `### Document status`) says resolved items leave the open-questions list. The bullet was drift; deletion is the right correction. The canonical resolution lives in section 3's `### Crew and characters` subsection and is unaffected by the deletion. A future DECISIONS log entry will capture the resolution chain historically.

The companion-doc grouping refinement names a practical reality that the original strategy did not. Treating DeterministicRandom (a single utility class), Authority (a cross-cutting pattern), and VesselConstruction (a major gameplay system) as peers in the companion-doc list would produce wildly varying doc shapes. The refinement is that the principle is "one doc per coherent unit of design," not "one doc per item in the list." Small utilities can be grouped under a `FoundationPrimitives` container or similar; major systems always get their own. The grouping decision is made when each doc is started, not in advance, because the right grouping depends on how the code actually shapes up.

The Vizzy mobile note acknowledges a specific friction the doc had not previously named. Vizzy is a node graph editor, and node graph editors are notoriously painful on small touchscreens. Mobile-capable architecture is a Phase 1 commitment (commit `001` preserved), but mobile-shipping is a Phase 8 open question. Naming the Vizzy-on-mobile concern now is architecturally cheap; retrofitting a mobile authoring path for Vizzy later would be expensive. The architectural commitment that emerges from this note: Vizzy scripts persist with vessel designs and can be loaded and executed on any platform; only the authoring UI is platform-restricted. Mobile users can run desktop-authored scripts; they cannot author new ones from mobile.

The Phase exit criteria subsection makes explicit what was previously implicit: validation milestones are the bar for phase completion, not weight units. The weights are effort estimates for planning; they tell you how much work each phase is expected to involve, but they do not tell you when a phase is done. The subsection lists the phases with single-milestone binary exit criteria (Phase 1 and Phase 3, both of which have explicit validation milestones in their subsection descriptions), describes multi-deliverable exit conditions for the other phases, and closes with the don't-press-into-the-next-phase-if-blocked rule. The rule is grounded in the existing section 13 failure-modes content, which already names the "beautiful screenshots, broken physics" KSP 2 trajectory as a project-shape failure to avoid. The Phase exit criteria subsection turns that failure-mode into a positive process commitment.

## Changes

### Edit 1: Section 1, asynchronous-progression design pillar — embed first-hour caveat parenthetical

Current text (post-`004c`) — third sentence in the four-sentence pillar:

> Every session produces visible forward motion.

Replacement (third sentence becomes):

> Every session produces visible forward motion (once the player has accumulated agency state — bases, missions, research, probes — to advance; the first hour has its own design, see section 6's `### First-hour experience`).

The other three sentences of the pillar bullet (bounded-limits clause, per-system bullets, journey-gating principle) are preserved verbatim.

### Edit 2: Section 9 — delete the RESOLVED crew-abstraction bullet

The bullet `- **Crew abstraction** (Phase 6): RESOLVED — headcount for base mechanics, named individuals for important missions. See section 3, Crew and characters.` is removed entirely from section 9. Section 9's bullet count goes from 14 to 13. No replacement bullet; no cross-reference back to section 3 (the canonical resolution is already in section 3's `### Crew and characters` subsection, and a reader of section 9 does not need a pointer back to a resolved question).

### Edit 3: Section 11, `### What gets a companion doc` — append grouping paragraph

After the existing four-bullet list (`Each foundation system`, `Each gameplay system`, `Each cross-cutting concern`, `Each procgen layer`), append a single paragraph:

> Some entries in this list are major systems with substantial implementation; others are single utilities or thin patterns. The intent is "one doc per coherent unit of design," not "one doc per item in this list." When utilities are small enough that a per-utility doc would be a few paragraphs and would mostly duplicate context, group them under a `FoundationPrimitives` companion doc (or similar named container per cluster). Cross-cutting concerns at the smaller end (e.g., DeterministicRandom as a utility, the EventBus as a thin pattern) are good candidates for grouping. Major systems (VesselConstruction, OrbitalMechanics, Vizzy) always get their own doc. The grouping decision is made when each doc is started, not in advance; the principle is that the doc should be substantial enough to be worth maintaining and small enough to read in one sitting.

The bullet list above it is preserved verbatim.

### Edit 4: Section 3, `### Automation and scripting` — append Vizzy mobile note

After the existing closing paragraph of `### Automation and scripting` (the "alert system and analytic event-prediction priority queue ... may share infrastructure when both are built in Phase 5; the specific architectural decision (shared or separate) defers to that phase. The systems should be designed with awareness of each other." paragraph from commit `003`), append a new paragraph:

> **Mobile shipping note.** Vizzy is the part of the game least suited to mobile. Node graph editors are notoriously painful on small touchscreens — dense graphs, fat-finger imprecision, small affordances. If mobile shipping happens (see section 9's `Mobile shipping` open question), the expected pattern is a read-only Vizzy viewer on mobile plus full desktop authoring rather than a full mobile authoring experience. Architecturally cheap if known now; expensive to retrofit later. The architecture commitment is that Vizzy scripts persist with vessel designs (already locked) and can be loaded and executed on any platform; only the authoring UI is platform-restricted. Mobile users can run desktop-authored scripts; they cannot author new ones from mobile.

### Edit 5: Section 8 — new `### Phase exit criteria` subsection appended at end

After Phase 8's description and before `## 9. Open questions to resolve`, append a new subsection:

> ### Phase exit criteria
>
> Each phase exits when its validation milestone passes, not when its weight units are exhausted. The weights are effort estimates for planning; they do not determine when a phase is done.
>
> Phases with explicit validation milestones (named in their subsections above): Phase 1 (placeholder cube reaches orbit, transfers to a moon, captures, lands; time-warp works); Phase 3 (full launch-to-Mun-landing in a built rocket — vertical slice MVP). These phases have binary pass/fail criteria; do not advance until the milestone passes.
>
> Phases without single-milestone exit criteria (Phase 0, Phase 2, Phase 4-8) have multi-deliverable exit conditions. Phase 0 exits when LOCKED items are locked, blocking OPEN items are resolved, and the netcode contract deliverable (written contract + prototype) is in place. Phase 2 exits when both deliverables (procedural part system + per-planet procgen pipeline) meet their respective sub-milestones, leaving Phase 3 unblocked. Phase 4 exits when the visual layer is at minimum-viable for the vertical slice. Phases 5-8 have iteration loops rather than single milestones; their exit conditions are listed deliverables in their subsection descriptions.
>
> If a phase's validation milestone or deliverables are blocked, do not press into the next phase. Resolve the block first, either by completing the work or by explicitly retracting the deliverable from the phase scope (which requires a doc update and a DECISIONS log entry per the project's discipline). Pressing into a later phase while leaving foundation work incomplete is exactly the pattern that creates the KSP 2-style "beautiful screenshots, broken physics" failure named in section 13.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### #9 first-hour caveat

1. Section 1 contains the literal phrase `once the player has accumulated agency state — bases, missions, research, probes — to advance`.
2. Section 1 contains the literal phrase `the first hour has its own design, see section 6's `+'`### First-hour experience`'+`.
3. The pillar still contains `Every session produces visible forward motion` (preserved within the larger sentence).
4. The pillar still contains `long absences change who and what, not how much` (from commit `004c`).

### #11 RESOLVED crew-abstraction removal

5. The bullet `- **Crew abstraction** (Phase 6): RESOLVED` is ABSENT from the doc.
6. The literal phrase `Crew abstraction` is ABSENT from the doc (the only previous occurrence was the section 9 bullet; the canonical content uses `Crew and characters` as the section 3 heading, which is different).
7. Section 3's `### Crew and characters` subsection is preserved (anchored heading count: 1).
8. Section 9 final-three-bullets adjacency is preserved: in order `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX`.
9. Section 9 contains exactly 13 `- **<bullet>**` entries after this commit (previously 14).

### #12 companion doc grouping refinement

10. Section 11 contains the new paragraph lead-in `Some entries in this list are major systems with substantial implementation`.
11. Section 11 contains the literal phrase ``FoundationPrimitives` companion doc``.
12. Section 11 contains the literal phrase `The grouping decision is made when each doc is started`.
13. Section 11's existing bullet list is preserved (literal phrases `Each foundation system (CoordinateSystem, ReferenceFrames, OrbitalMechanics, TimeManager, PhysicsModeManager, SaveSystem)` and `Each procgen layer (GalaxyGen, SystemGen, PlanetGen, AnomalyGen)` each present exactly once).

### #13 Vizzy mobile note

14. Section 3's `### Automation and scripting` subsection contains the literal paragraph lead-in `**Mobile shipping note.**`.
15. The same subsection contains the literal phrase `Node graph editors are notoriously painful on small touchscreens`.
16. The same subsection contains the literal phrase `a read-only Vizzy viewer on mobile plus full desktop authoring`.
17. The same subsection references section 9's mobile shipping open question via the literal phrase ``see section 9's `Mobile shipping` open question``.
18. The same subsection contains the cross-platform-execution architectural commitment: `Vizzy scripts persist with vessel designs (already locked) and can be loaded and executed on any platform`.

### #14 Phase exit criteria

19. Section 8 contains exactly one `### Phase exit criteria` heading anchored to start-of-line. The heading is positioned after the existing Phase 8 description and immediately before `## 9. Open questions to resolve`.
20. The subsection contains the weights-vs-milestones distinction: `Each phase exits when its validation milestone passes, not when its weight units are exhausted`.
21. The subsection contains the binary-pass-fail naming: `These phases have binary pass/fail criteria; do not advance until the milestone passes`.
22. The subsection contains the Phase 0 multi-deliverable exit description: `Phase 0 exits when LOCKED items are locked, blocking OPEN items are resolved, and the netcode contract deliverable (written contract + prototype) is in place`.
23. The subsection contains the Phase 2 exit description: `Phase 2 exits when both deliverables (procedural part system + per-planet procgen pipeline) meet their respective sub-milestones, leaving Phase 3 unblocked`.
24. The subsection contains the cross-reference to section 13's KSP 2-style failure mode: `the KSP 2-style "beautiful screenshots, broken physics" failure named in section 13`.

### Preserved-content anchors (per workflow rule)

25. Commit `001` content preserved across untouched section anchors: literal phrases per section (Pixar register, Jeb legend, 20%/80%, Hill sphere, vertical slice MVP, placeholder cube, siren song, suggested repo layout, institutional memory, doc-driven dev, kraken returning, pre-flight checklist, KSP 2's path, stale doc syndrome, Last comprehensive update Phase 0). Each present exactly once.
26. Commit `002` content preserved: anchored heading `### Foundational architectural principles` count is 1; literal phrases `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `Authoritative state replication is the multiplayer model for PhysX-active vessels`, `sharp and symmetric` each present exactly once. Five numbered principles in FAP.
27. Commit `003` content preserved: `detection-aggressiveness parameter`, `**Agency-based observation sharing.**`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.` each present exactly once.
28. Commit `004a` content preserved: anchored heading counts of exactly one each for `### Multiplayer as shared universe` and `### Mode-portable designs and templates`; phrases `Save files are mode-locked at creation` and `parameterized by game mode and propulsion tier` each present exactly once.
29. Commit `004b` content preserved: anchored heading counts of exactly one each for `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### Interstellar distances`, `### Time-warp in single-player`, `### Director perspective`, `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts`. Phrases `1/8 real scale` and section 7 toggle `- Solar system scale (Casual 1/10 / Standard 1/8 / Realistic 1/1, with custom intermediate scales available)` each present exactly once.
30. Commit `004c` content preserved: anchored heading counts of exactly one each for `### Home system evolves autonomously`, `### Phase 2 — Vessel construction and per-planet procgen (weight 2)`, `### Phase 7 — Galaxy-level procgen + interstellar (weight 3+)`. Literal phrases `within bounded limits that prevent passive accumulation`, `**Network-capacity rule.**`, `Research advances only when scientists are assigned to the project`, `Procgen produces all planets in the universe, including the home system`, `**Phase 0 deliverable: netcode contract.**`, `This is the *Interstellar* emotional structure made structural rather than aspirational` each present exactly once.
31. Section 3 subsection order preserved (in document order): Parts → Failure modes → Automation → Information asymmetry → Anomalies → Interstellar tiers → Director perspective → Crew and characters → EVA → Transmissions → Channel 16 → Home system evolves autonomously → Multiplayer as shared universe → Mode-portable designs → Goal structure.
32. All 14 numbered `## N.` section headings still present.
33. File line count in range 870–890. The post-`004c` baseline was 862 lines; this commit's net delta is approximately 13 lines (additions minus the one deleted bullet).

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/005_minor_cleanups.md
```
