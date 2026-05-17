# 010: In-media-res starting state, logistics-not-tech progression, configurable starting conditions

Land the most consequential single commit in the reconciled-source series. Three new subsections in section 1 commit a fundamental change to how progression works in the game: progression is gated by the physics of logistics (the rocket equation, infrastructure that must be built before further infrastructure can be built, real engineering with honest engine numbers) rather than by tech-tree unlocks of parts. The game begins in-media-res with humanity already 50-60 years into its space age, with inherited infrastructure (a partly-operational moon base, a Mars-equivalent research outpost) and a three-tier content structure for placed artifacts (Tier A handcrafted unique, Tier B handcrafted classes filled procedurally, Tier C ambient procgen texture). The game has one mode with configurable starting conditions rather than separate sandbox and career modes. Section 8's Phase 0 description is expanded to include the artifact list as a Phase 0 deliverable.

The logistics-not-tech reframe is the single largest design change in this commit series. The existing tech tree (section 4's `### Research as asynchronous progression`) is not eliminated; it is reframed. Under logistics-not-tech, parts are universal from game start; the tech tree continues to gate capabilities and infrastructure that aren't parts: observatory instrument tiers, specialty propulsion infrastructure (laser sail arrays and beyond), advanced manufacturing facilities (orbital VAB, surface VABs at other bodies), and research samples as categorical inventory. The new `### Logistics-not-tech as primary progression gating` subsection makes this gating relationship explicit so the question "what does the tech tree gate under logistics-not-tech?" is answered within the subsection where it is introduced.

The in-media-res starting state changes what the first hour of the game involves. The home moon already has a partly-operational base inherited from the previous era; the Mars-equivalent already has a minimal research outpost. Section 6's `### First-hour experience` subsection is preserved verbatim — its target arc (successful launch within 10 minutes, view from orbit within 20, reach the home moon within the first hour, reveal of the broader game) still holds. The in-media-res context re-frames the "accomplishment moment" of reaching the moon: the player is reaching an inherited base, not virgin territory. The accomplishment is the player's first solo reach, not humanity's first reach; the inherited base reinforces the first-hour reveal-of-broader-game moment.

The six progression stages (Stage 1 through Stage 6, spanning hours 1-10 through 200+) describe player experience over hours of play, distinct from section 8's Phase 0-8 development build phases. The subsection text makes this distinction explicit in the lead-in to the stage list to prevent confusion. Both ordered sequences exist in the doc; both use the word "stage" / "phase" in different domains; the disambiguation is one phrase but prevents a confusion that any careful reader would otherwise have to resolve from context.

The configurable starting conditions subsection commits the game-has-one-mode discipline. Difficulty has been configurable settings since commit 001's section 7 lock; tech availability and progression follow the same pattern. A player who wants full sandbox sets everything unlocked, no progression; a player who wants progression sets minimal start, research gated; players in the middle configure however they want.

## Scope

- `docs/CONSTRAINTS.md` — modified. Four logical edits applied as one atomic write:
  - Section 1: three new subsections appended at the end of section 1 (after `### Minimal-tycoon, rich-progression positioning`): `### In-media-res starting state`, `### Logistics-not-tech as primary progression gating`, `### Configurable starting conditions`.
  - Section 8 Phase 0 description: one new paragraph appended after the existing `**Phase 0 deliverable: netcode contract.**` paragraph, naming the three-tier starting content structure and the artifact list as Phase 0 deliverables.

Section 1 grows from 5 subsections to 8.

## Rationale

The logistics-not-tech reframe solves four design tensions simultaneously: the tech-tree-vs-sandbox tension (no separate modes, configurable settings instead); the pacing problem (players progress naturally through physical infrastructure rather than via designer-set tech timing); the veteran-vs-new-player problem (same physical constraints apply to both, with KSP veterans completing Stage 1 faster but unable to skip Stage 2 because orbital VAB construction is physical work that requires many launches and assembly time); the home-investment problem (players who want to push outward must develop home infrastructure first, which builds investment in the home system naturally as a side effect). Each of these is a real tension in space sim design that this commit addresses with one structural commitment.

The progression-gates-are-physics framing demands that engine numbers be honest. Tank mass fractions must be realistic. Structural masses must match real engineering. The temptation to fudge for gameplay must be resisted because the gating mechanism depends on the physics being real. This is a design discipline commitment that propagates into Phase 2 (procedural parts must have honest physics) and Phase 3 (flight integration must respect the rocket equation).

The in-media-res starting state changes the emotional valence of the game's opening. Rather than building humanity's first space program, the player continues an existing tradition. The previous era reached significant accomplishments — a moon base, a Mars-equivalent outpost, a Voyager-equivalent probe, possibly a generation ship in transit — but ran out of momentum. The player's era is the current revival, pushing further than the previous era did. This creates an immediate sense of history without requiring backstory: the artifacts of prior space activity carry the history, and the player's first transmissions can acknowledge it explicitly (review meetings reference Voyager-2 telemetry, ISS retirement decisions, the laser propulsion research program).

The three-tier content structure (Tier A handcrafted unique / Tier B handcrafted classes filled procedurally / Tier C ambient procgen texture) commits an explicit content budget. 10-20 Tier A artifacts ship at v1, each designer-authored with specific intent. 6-10 Tier B classes ship, each with rich variation parameters producing many instances. Tier C emerges from the procgen pipeline at no specific design cost. The three-tier framing makes content trade-offs explicit and ties the artifact list to Phase 0 as a deliverable.

The KSP veteran case paragraph addresses a specific player concern: skilled players want to skip the early-game friction that newcomers benefit from. Under logistics-not-tech, veterans move through Stage 1 faster (maybe 3-5 hours instead of 10) but cannot skip Stage 2 because orbital VAB construction is physical work, not a tutorial barrier. By Stage 4, veterans and new players are at comparable pacing. Same game, different speeds. This is a different shape than KSP's tech tree (which veterans treat as a checklist to complete) or sandbox (which abandons progression entirely).

The configurable starting conditions subsection extends the existing locked "difficulty as independent toggles" pillar (section 7) to tech availability and progression. The game has one mode; players configure their starting conditions and progression rules at game creation. Starting tech level options: everything unlocked / standard starting set / minimal starting set / custom. Progression source options: research from missions / research from observatory / both / neither (everything unlocks immediately). Whether anomalies and discoveries gate specific advanced tech: yes / no. This is the same pattern as difficulty toggles: the rules are configurable per-save rather than mode-gated.

The Phase 0 expansion commits the artifact list as a Phase 0 deliverable. Phase 0 now has two named deliverables: the netcode contract (commit 004c) and the artifact list (this commit). Both are Phase 1 prerequisites in their respective domains — the netcode contract for the multiplayer-architecture-preparation work in Phase 1; the artifact list for the procgen and hand-tuned-home-system work in Phase 2 (per-planet generation needs to know which Tier A artifacts to place; Tier B classes need to know which classes are in scope; Tier C texture needs to know which procgen scope is required).

## Changes

### Edit 1: Section 1, append new `### In-media-res starting state` subsection

Appended at the end of section 1, after the existing `### Minimal-tycoon, rich-progression positioning` subsection (the last subsection of section 1 per commit 004b). The new subsection contains:

- LOCKED commitment: the game begins with humanity 50-60 years into its space age; the player's agency continues an existing tradition; the previous era reached accomplishments but ran out of momentum; the player's era is the current revival.
- Starting infrastructure (three bolded leads): Home planet (fully-equipped ground capability), Home moon (partly-operational inherited base), Next planet out / Mars-equivalent (minimal research outpost).
- Statement that nothing exists in orbit at game start and nothing exists at outer system bodies.
- Three-tier content structure (three bolded leads): Tier A (10-20 handcrafted unique artifacts), Tier B (6-10 handcrafted classes filled procedurally), Tier C (ambient procgen texture).
- Tier A example list: Voyager-equivalent at heliopause, ISS-equivalent in orbit, Apollo-equivalent landing sites, Mars-equivalent landers, generation ship en route, wormhole or deep-space anomaly, unexplained signal source.
- Closing paragraph: the player's agency continues the previous era's space program; first transmissions can acknowledge this (Voyager-2 telemetry, ISS retirement, laser propulsion research).

### Edit 2: Section 1, append new `### Logistics-not-tech as primary progression gating` subsection

Appended immediately after `### In-media-res starting state`. The new subsection contains:

- LOCKED commitment: all parts available from game start; progression is physics of payload delivery, not tech unlocks.
- Rocket-equation-is-the-gate paragraph: real engines, real Isp, real tank mass fractions; the math determines what works.
- Infrastructure progression list with six stages, lead-in including the stage-vs-phase disambiguation ("six player-experience stages over hours of play — these are distinct from the development build phases in section 8"): Stage 1 (hours 1-10, home system development), Stage 2 (hours 10-30, orbital VAB), Stage 3 (hours 30-60, interplanetary infrastructure), Stage 4 (hours 60-100, Tier 1 interstellar), Stage 5 (hours 100-200, Tier 2 interstellar), Stage 6 (hours 200+, Tier 3 interstellar).
- KSP veteran case paragraph.
- Discipline paragraph (engine numbers must be honest).
- Why this matters paragraph (solves four tensions: tech-tree-vs-sandbox, pacing, veteran-vs-new-player, home-investment).
- `**What the tech tree still gates.**` paragraph: parts are universal from start, tech tree continues to gate capabilities and infrastructure (observatory instrument tiers, specialty propulsion infrastructure, advanced manufacturing facilities, research samples as categorical inventory), tech tree behavior configurable per `### Configurable starting conditions`.

### Edit 3: Section 1, append new `### Configurable starting conditions` subsection

Appended immediately after `### Logistics-not-tech as primary progression gating`. The new subsection contains:

- LOCKED commitment: the game has one mode; players configure starting conditions and progression rules at game creation.
- Configuration options list (four items): starting tech level, progression source, anomaly/discovery gating, difficulty toggles on existing axes.
- Same-systems-different-starting-points paragraph: full sandbox sets "everything unlocked, no progression"; progression sets "minimal start, research gated"; players in the middle set whatever they want.
- Closing paragraph: same pattern as difficulty toggles; difficulty is not a mode, it is configurable settings; tech availability and progression follow the same pattern.

### Edit 4: Section 8 Phase 0 description — append three-tier artifact list deliverable paragraph

Appended after the existing `**Phase 0 deliverable: netcode contract.**` paragraph (from commit 004c). The new paragraph:

> Phase 0 also locks the three-tier starting content structure: identify which Tier A handcrafted artifacts ship at v1 (target 10-20), which Tier B classes ship (target 6-10), and which procgen scope is required for Tier C ambient texture. The specific artifact list is a Phase 0 deliverable.

Phase 0 now has two named deliverables: the netcode contract (from commit 004c) and the artifact list (this commit). Both are Phase 1 / Phase 2 prerequisites in their respective domains.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### New-content anchored heading checks

1. `### In-media-res starting state` anchored heading count is 1.
2. `### Logistics-not-tech as primary progression gating` anchored heading count is 1.
3. `### Configurable starting conditions` anchored heading count is 1.

### In-media-res content

4. Literal phrase `The game begins with humanity already 50-60 years into its space age` is present.
5. All three starting infrastructure leads present, each exactly once: `**Home planet:** fully-equipped ground capability`, `**Home moon:** partly-operational base inherited from the previous era`, `**Next planet out (Mars-equivalent):**`.
6. All three content tier leads present, each exactly once: `**Tier A (handcrafted unique artifacts):**`, `**Tier B (handcrafted classes filled procedurally):**`, `**Tier C (ambient procgen texture):**`.
7. Tier A example phrases present: `Voyager-equivalent at the heliopause`, `ISS-equivalent in orbit`, `Apollo-equivalent landing sites on the home moon`.

### Logistics-not-tech content

8. Literal phrase `All parts are available from game start` is present.
9. Literal phrase `The rocket equation is the gate` is present.
10. All six progression stage leads present, each exactly once: `**Stage 1 (hours 1-10):**`, `**Stage 2 (hours 10-30):**`, `**Stage 3 (hours 30-60):**`, `**Stage 4 (hours 60-100):**`, `**Stage 5 (hours 100-200):**`, `**Stage 6 (hours 200+):**`.
11. Literal phrase `**The KSP veteran case:**` is present.
12. Literal phrase `**The discipline this requires:**` is present.
13. Stage-vs-phase disambiguation present: literal phrase `these are distinct from the development build phases in section 8`.
14. Tech-tree-still-gates clarifying paragraph present: literal phrase `**What the tech tree still gates.**` and literal phrase `observatory instrument tiers, specialty propulsion infrastructure`.

### Configurable starting conditions content

15. Literal phrase `The game has one mode. Players configure their starting conditions` is present.
16. Literal phrase `Difficulty is not a mode; it is configurable settings` is present.
17. Starting tech level option literal `Starting tech level (everything unlocked / standard starting set / minimal starting set / custom)` is present.
18. Progression source option literal `Progression source (research from missions / research from observatory / both / neither — everything unlocks immediately)` is present.

### Phase 0 expansion

19. Literal phrase `Phase 0 also locks the three-tier starting content structure` is present.
20. Literal phrase `The specific artifact list is a Phase 0 deliverable` is present.

### Section 1 subsection ordering

21. Section 1's complete subsection list, in document order, is exactly: `### Design pillars`, `### Target player types — all served simultaneously`, `### Things we are explicitly NOT doing`, `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### In-media-res starting state`, `### Logistics-not-tech as primary progression gating`, `### Configurable starting conditions`. Total: 8 subsections.

### Preserved-content anchors

22. Commit 009 content preserved: anchored heading counts of 1 each for `### Physics-driven gameplay`, `### Home observatory`, `### Catalog as long-term meta-game`. Literal `**Discovery announcement transmissions.**` and `**Stellar physics.**` present.
23. Commit 008 content preserved: anchored heading counts of 1 each for `### Generation architecture`, `### The 14-stage pipeline (Layer 5)`, `### Hybrid pipeline-plus-grammar`, `### Planet variety tiers`, `### Feature-layer architecture`, `### The 90/9/1 anomaly distribution`, `### Galaxy scope`, `### Map view hierarchy`. Literal `90% resolve to interesting-but-understandable phenomena` and `1% resolve to once-per-playthrough hero moments` present.
24. Commit 007 content preserved: anchored heading counts of 1 each for `### Detection ecosystem`, `### Platforms for detection`, `### Discovery progression — eight stages`, `### Detection mechanics`, `### Anomaly types worth seeding`. Forward reference `The 90/9/1 distribution rule (section 5)` still present.
25. Commit 006 content preserved: anchored heading counts of 1 each for `### Per-body physics parameter set` and `### Layered engagement framework`. Section 1 design pillars list still contains `- Discovery as gameplay, grounded in real astrophysics.` and `- Physics-grounded substrate.`
26. Commit 004a/b/c content preserved: anchored heading counts of 1 each for `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### Channel 16 broadcasts`, `### Home system evolves autonomously`. Literal `Save files are mode-locked at creation`, `1/8 real scale`, `within bounded limits that prevent passive accumulation`, `**Network-capacity rule.**`, `**Phase 0 deliverable: netcode contract.**` each present. Anchored headings for Phase 2 and Phase 7 amended titles each count 1.
27. Commit 005 content preserved: `### Phase exit criteria`, `**Mobile shipping note.**`, `FoundationPrimitives` companion doc reference.
28. Commit 003 content preserved: `**Agency-based observation sharing.**`, `detection-aggressiveness parameter`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.`
29. Commit 002 content preserved: `### Foundational architectural principles` anchored heading count 1; literal `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `sharp and symmetric`, `Authoritative state replication is the multiplayer model for PhysX-active vessels` each present. Five numbered principles in FAP.
30. Commit 001 content preserved across untouched sections: distinctive phrases per section (Pixar register, Jeb legend, Hill sphere, 20%/80%, KSP's first hour, Dwarf Fortress / RimWorld, physics fidelity toggle, vertical slice MVP, placeholder cube, siren song, suggested repo layout, institutional memory, doc-driven dev, kraken returning, pre-flight checklist, KSP 2's path, stale doc syndrome, Living document, Last comprehensive update). Each present exactly once.
31. Section 9 final-three-bullets adjacency: `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX` in order. Section 9 bullet count: 13.
32. All 14 numbered `## N.` section headings still present.
33. File line count is 1226 (post-009 was 1166 lines; this commit added 60 net lines).

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/010_in_media_res_logistics_progression.md
```
