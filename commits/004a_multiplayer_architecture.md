# 004a: Multiplayer architecture, Issue #10 resolution, save mode-locking, mode-portable designs

Land the multiplayer-architecture cluster of refinements from Phase 0's second design pass. Four interconnected commitments land together because they share a single architectural premise (multiplayer is a shared universe with shared game-time) and their consequences interleave: the shared-universe model makes Issue #10 (time-warp authority in multiplayer) dissolve rather than need a resolution; save mode-locking falls out of single-player retaining relativistic mechanics while multiplayer does not; mode-portable designs are the architectural counterpart to mode-locked runtime state, separating *what to construct* (portable) from *what currently exists* (locked).

Two amendments to previously-locked content are required for correctness, not style: the Interstellar-cruise mode description in section 2 and the proper-time statement in section 2's Time representation subsection were both single-player-correct but multiplayer-incorrect as originally written. The amendments preserve the single-player behavior verbatim as one branch of a now-mode-aware statement, and add the multiplayer branch alongside.

Three new commitments land as new subsections: section 3's `### Multiplayer as shared universe` (the full architecture rework including the Issue #10 resolution), section 3's `### Mode-portable designs and templates` (the file-portability architecture), and an extension to section 2's `### Save format implications` introducing save mode-locking with an explicit non-goal.

The shared-universe model operates on top of, not in place of, commit `002`'s determinism boundary. PhysX-active vessels remain host-authoritative; deterministic state outside the PhysX-active envelope remains deterministic and replicates cleanly. The shared-universe subsection's closing paragraph names this compatibility explicitly so the gameplay model and the architectural foundation cannot drift apart.

## Scope

- `docs/CONSTRAINTS.md` — modified. Five edits across sections 2 and 3: section 2's `### Physics architecture` Interstellar-cruise bullet (amendment), section 2's `### Time representation` proper-time statement (amendment), section 2's `### Save format implications` (appended save mode-locking block), section 3's new `### Multiplayer as shared universe` subsection (inserted before `### Goal structure`), section 3's new `### Mode-portable designs and templates` subsection (inserted between `### Multiplayer as shared universe` and `### Goal structure`).

## Rationale

The multiplayer model is the design choice that makes multiplayer actually multiplayer. Relativistic time-dilation creates frame-dependent time, which fundamentally conflicts with shared-time multiplayer; every workaround introduces structural complexity that hurts gameplay. The cleaner solution is to accept that time-dilation is a single-player design feature and that multiplayer interstellar uses spatial warp without invoking real relativistic physics. Both modes share the bulk of the design (physics, propulsion tiers as tech gates, base building, exploration, tech progression) and differ only in how interstellar transit operates.

This design choice has cascading consequences captured in this commit:

- Issue #10 dissolves. The "whose clock wins" problem assumes per-player clocks that need reconciliation; under shared game-time with a player-coordinated time multiplier, there is no clock to authoritatively own across players. The architecture changes the question.
- Save mode-locking is forced. A single-player save with relativistic-divergent proper-time clocks cannot be coherently loaded into multiplayer; a multiplayer save cannot be converted to single-player because the universe state has been advanced under spatial-warp semantics. The explicit non-goal makes this constraint visible to future design work.
- Mode-portable designs are the natural counterpart. Vessel designs and character templates describe how to construct, not what currently exists. They have no proper-time clock to diverge, no runtime state to break under mode change. They can and should travel between modes; runtime state cannot.

The amendments to locked content (Interstellar-cruise mode description and Time representation proper-time statement) are not soft revisions. The original locked statements were single-player-correct but ambiguous about multiplayer; the amendments preserve the single-player branch verbatim and add the multiplayer branch. Without these amendments the doc would describe inconsistent foundations after this commit lands.

The Channel 16 broadcast surface, agency-based observation sharing for multiplayer (which the multiplayer rework references), and transmission system are intentionally not in this commit. They belong in commits `004b` (vision and texture layers) and `004c` (asynchronous progression machinery). The architecture lands here; the texture layers reference back to it when they land.

## Changes

### Edit 1a: Section 2, `### Physics architecture` — Interstellar-cruise bullet mode-aware

The third bullet under "Three-mode system" is replaced. Original:

> - **Interstellar-cruise:** for vessels between stellar systems. Straight-line trajectory between origin and destination. Game time advances normally; vessel proper time advances at 1 divided by gamma.

Replacement:

> - **Interstellar-cruise:** for vessels between stellar systems. Straight-line trajectory between origin and destination. The mode\'s behavior is parameterized by game mode and propulsion tier: in single-player, vessel proper time advances at 1 divided by gamma (relativistic transit); in multiplayer, proper time always equals game time and the vessel traverses interstellar distance at a configured warp velocity for its propulsion tier (spatial warp, no relativistic effects). Per-vessel proper-time clocks exist in both modes as data; their behavior is determined by mode.

The amendment preserves the single-player behavior verbatim ("vessel proper time advances at 1 divided by gamma") as one branch of the new mode-aware statement and adds the multiplayer branch alongside. The three-mode physics architecture itself is unchanged; only the mode\'s behavioral description is parameterized.

### Edit 1b: Section 2, `### Time representation` — proper-time statement mode-aware

The sentence is amended. Original:

> Most of the time proper time equals game time; they diverge only during relativistic interstellar coast.

Replacement:

> Most of the time proper time equals game time; they diverge only during relativistic interstellar coast in single-player. In multiplayer, proper time always equals game time because multiplayer interstellar transit uses spatial warp, not relativistic transit.

Same amendment shape as 1a: the single-player branch is preserved and a multiplayer branch is added explicitly.

### Edit 2: Section 2, `### Save format implications` — save mode-locking

Appended at the end of the existing subsection, after the locked "every system that produces or consumes anything over time MUST be representable as (state at time T, rate function)" paragraph:

> **Save files are mode-locked at creation.** Saves are tied to their game mode at creation and cannot be transferred between single-player and multiplayer.
>
> - Single-player saves with relativistic-divergent proper-time clocks cannot be loaded as multiplayer sessions.
> - Multiplayer saves cannot be converted to single-player.
> - This is a hard architectural constraint that affects save format design.
>
> **Explicit non-goal:** there is no feature for importing single-player crew, vessels, or progress state into multiplayer (or vice versa). Saves are mode-locked at creation. Players who want to play with friends start a new multiplayer save together. Mode-portability applies to designs and templates only, not to runtime state — see section 3\'s "Mode-portable designs and templates" subsection.

### Edit 3: Section 3, new subsection `### Multiplayer as shared universe`

Inserted in section 3 immediately before the existing `### Goal structure` subsection. New subsection content (eleven paragraphs):

> **LOCKED:** Multiplayer is structurally a shared universe with shared game-time. All players exist in the same physical universe at the same moment. Players can physically interact — dock with each other\'s ships, meet at colonies, conduct joint missions — because they share both time and space.

The subsection continues with: shared time multiplier with player coordination; interstellar travel in multiplayer uses spatial warp; no time-dilation in multiplayer; the design-choice reasoning paragraph; same systems, different behavior by mode; cooperative play; competitive play; Issue #10 dissolution statement (literal phrase: "The 'whose clock wins' problem dissolves because all players share the same time at any moment"); architecture compatibility paragraph naming the commit `002` determinism boundary.

The full content is in `docs/CONSTRAINTS.md` section 3 after this commit lands; refer to the file rather than reproducing the eleven paragraphs verbatim here.

### Edit 4: Section 3, new subsection `### Mode-portable designs and templates`

Inserted in section 3 immediately after `### Multiplayer as shared universe` and immediately before `### Goal structure`. New subsection content (six paragraphs):

> **LOCKED:** Vessel designs (parts, configuration, Vizzy scripts) and character templates (names, traits, portraits) are mode-portable. Players can import designs and templates created in single-player into multiplayer and vice versa. Only runtime state (vessel instances in the simulation, character instances with their accumulated history, save state with diverged proper-time) is mode-locked.
>
> The distinction: designs and templates describe how to construct; runtime state describes what currently exists.

The subsection continues with: format requirements list (self-contained; explicit semantic versioning independent of game version with graceful degradation; human-readable for community tooling; exportable as files or codes); Vizzy scripts in shared designs use role-based references by default (parts referenced by role like "main engine," not by ID); community sharing infrastructure not v1 but format must support it; local-first, offline forever (no online requirement, no DRM, designs work offline indefinitely).

The full content is in `docs/CONSTRAINTS.md` section 3 after this commit lands; refer to the file rather than reproducing the six paragraphs verbatim here.

## Verification

A future session can confirm this commit landed correctly by checking:

### Multiplayer architecture (Issue #10 + multiplayer rework)

1. **Section 2\'s `### Physics architecture` subsection\'s Interstellar-cruise bullet** contains the phrase `parameterized by game mode and propulsion tier` and both branches: `in single-player, vessel proper time advances at 1 divided by gamma (relativistic transit)` and `in multiplayer, proper time always equals game time and the vessel traverses interstellar distance at a configured warp velocity`.
2. **Section 2\'s `### Time representation` subsection** contains the phrase `In multiplayer, proper time always equals game time because multiplayer interstellar transit uses spatial warp`.
3. **Section 3 contains a new subsection** `### Multiplayer as shared universe`.
4. **The new subsection contains** the literal phrases `Shared time multiplier with player coordination` and `Interstellar travel in multiplayer uses spatial warp`.
5. **The new subsection contains** the literal phrase `No time-dilation in multiplayer`.
6. **The new subsection contains** the Issue #10 dissolution statement (literal phrase: `The "whose clock wins" problem dissolves`).
7. **The new subsection contains** the architecture-compatibility note referencing commit `002`\'s determinism boundary (literal phrase: `the shared-universe model operates on top of the commit \`002\` determinism boundary`).

### Save mode-locking

8. **Section 2\'s `### Save format implications` subsection** contains the phrase `Save files are mode-locked at creation`.
9. **The same subsection** contains the three consequence bullets: `Single-player saves with relativistic-divergent proper-time clocks cannot be loaded as multiplayer sessions`; `Multiplayer saves cannot be converted to single-player`; `This is a hard architectural constraint that affects save format design`.
10. **The same subsection** names the explicit non-goal (literal phrase: `there is no feature for importing single-player crew, vessels, or progress state into multiplayer`) and references the section 3 Mode-portable designs and templates subsection.

### Mode-portable designs and templates

11. **Section 3 contains a new subsection** `### Mode-portable designs and templates`.
12. **The new subsection contains** the distinction statement: `designs and templates describe how to construct; runtime state describes what currently exists`.
13. **The new subsection contains** four format-requirement bullets: `Self-contained (no external references, no save-game dependencies)`, `Explicit semantic versioning of the format itself`, `Human-readable enough to enable community tooling`, `Exportable and importable as files or codes`.
14. **The new subsection contains** the Vizzy role-based references commitment: `Vizzy scripts in shared designs use role-based references by default`.
15. **The new subsection contains** the local-first commitment: `Local-first, offline forever`.
16. **The new subsection contains** the community-sharing-not-v1 commitment: `Community sharing infrastructure is not v1 but the format must support it`.

### Cross-commit consistency

17. **Section 2\'s `### Time representation` subsection** contains the mode-aware proper-time statement (literal phrase: `they diverge only during relativistic interstellar coast in single-player.`). The pre-amendment sentence ending `relativistic interstellar coast.` immediately followed by a newline (rather than the `in single-player` suffix) must NOT appear in the file.
18. **Section 3 subsection order is preserved.** In file order: `### Parts and vessel construction`, `### Failure modes`, `### Automation and scripting`, `### Information asymmetry / progressive discovery`, `### Anomalies and mysteries`, `### Interstellar travel: tiered tech progression`, `### Crew and characters`, `### Multiplayer as shared universe` (new), `### Mode-portable designs and templates` (new), `### Goal structure` (last). The new subsections are inserted between Crew-and-characters and Goal-structure; Goal-structure remains the last subsection of section 3.

### Preserved-content anchors (per workflow rule)

19. **Commit `002` content in section 2 is preserved.** Literal phrases present: `Authoritative state replication is the multiplayer model for PhysX-active vessels`, `host-authoritative state replication rather than lockstep input replication`, `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `### Foundational architectural principles` heading, `sharp and symmetric`. Five numbered principles in FAP. `~10 km` absent in Coordinate system subsection.
20. **Commit `003` content is preserved.** Literal phrases present: `detection-aggressiveness parameter`, `physics fidelity toggle`, `**Agency-based observation sharing.**`, `Agency membership refers to human players`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `The alert system is a first-class foundational system, not a UI element`, `Physics fidelity (pragmatic / strict)`, `Wants to feel like an agency director.`, `per-agency \'observed\' state`.
21. **Commit `001` content is preserved.** Literal phrases per untouched section: section 3 `Pixar register, not Goat Simulator register` and `this is how Jeb became a legend`; section 5 `Code is 20% of the work` and `Hill sphere spacing, frost line`; section 8 `This is the vertical slice MVP` and `placeholder cube launches from a planet surface`; section 10 `siren song of pretty screenshots` and `Suggested repo layout`; section 11 `project\'s institutional memory` and `Critical practice: doc-driven development`; section 12 `this is the kraken returning` and `Pre-flight checklist before generating code`; section 13 `KSP 2\'s path` and `Stale doc syndrome`; section 14 `Last comprehensive update: Phase 0 design crystallization`.
22. **Section 9 final-three-bullets adjacency check.** The last three bullets in section 9 must be, in this order: `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX`.
23. **All 14 numbered `## N.` section headings still present.**
24. **File line count after this commit is in the range 715–740.** The recovered post-commit-`003` baseline was 677 lines; this commit added approximately 49 lines.

If any of these checks fail, the commit has not been correctly applied and the file should be repaired against the source artifact before subsequent commits proceed. The bash-via-Python escape hatch from `commits/README.md` may be required for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/004a_multiplayer_architecture.md
```
