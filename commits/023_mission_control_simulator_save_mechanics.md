# 023: Mission Control as primary UI, integrated simulator, forgiving restart philosophy, save mechanics

Establish Mission Control as the primary functional UI for the game and add three closely-related commitments: a TV-screens visualization of the player's fleet with overflow-via-scroll/page handled at scale; a sandbox-as-simulator integrated into Mission Control that forks the current game state for testing and rehearsal without committing real resources; a forgiving-restart philosophy at vision level committing to abundant saves; and save mechanics implementation specifics extending commit 002's save format implications.

Mission Control is the wrapper around all game activities. It opens on game start after loading screens. Each TV-style screen shows a real-time render of one of the player's vessels at its last save-point position. Default screen count is 6-9 visible at once; late-game players with dozens of vessels access overflow via scroll/page controls. The player navigates between screens by direct selection or "change channel" navigation. Mission Control pauses the game; the player can spend as long as needed planning without time pressure.

The simulator is a feature of Mission Control, not a separate mode. Entering the simulator opens a fork of the current game state. The simulator starts from the last save and runs forward with real physics and real craft data. Exiting reverts. The simulator has access only to what the catalog knows — for remotely-scanned bodies, the simulator renders parameter-derived defaults rather than actual procgen output. The simulator viewport is framed as a monitor-within-the-game-view with "SIMULATOR" on the bezel and distinct HUD styling so sim mode is recognizable at a glance.

The forgiving-restart philosophy commits to abundant saves: quicksave (player-initiated, single overwriting slot), autosave (game-initiated, multiple rotating slots, triggered on mission launch/arrival/landing/EVA-completion/stage-transition/research-completion/catastrophic-failure), named saves (player-titled, manually managed). Reloading fully reverts state to the save's canonical state. The Fallout 4 reference is intentional: abundant-save culture supports deep mechanical engagement without punishing experimentation.

Simulator-state saves are sim-branch only. Quicksave / autosave / named saves all work inside the simulator, but the saves live in a separate sim-branch save space discarded on simulator exit. This lets players experiment in the simulator with full save/reload discipline without contaminating real game state.

The §2 save format implications extension specifies the file-level implementation: complete state snapshots (not deltas); quicksave as single dedicated path; autosave as rotating slots (default 5, player-configurable); named saves as player-titled with no automatic overwriting; mode-locked at creation; sim-branch using separate file path namespace cleared on simulator exit; quicksave performance budget of 2 seconds, autosave 5-10 seconds acceptable.

§7 grows from 5 to 7 subsections (Mission Control + Simulator). §1 grows from 14 to 15 subsections (Forgiving restart philosophy). §2 stays at 15 (Save format implications extended only).

## Scope

- `docs/CONSTRAINTS.md` — four edits:
  - §7: insert new `### Mission Control as primary UI` at end of §7
  - §7: insert new `### Sandbox-as-simulator integrated in Mission Control` immediately after Mission Control
  - §1: insert new `### Forgiving restart philosophy` at end of §1
  - §2: append `**Save mechanics implementation:**` block (with sim-branch save-space sub-bullet) to end of `### Save format implications`

## Rationale

The four edits commit to a coherent set: Mission Control as the player's primary UI, the simulator as a fork-and-revert feature of Mission Control, the abundant-save philosophy that supports both real and simulator play, and the save-format implementation that makes all of it work at the file level.

**Mission Control matters** because the design's commitment to thoughtful play (commit 004b transmissions framing; commit 012 dramatic shapes; commit 013 missions and campaigns) needs a UI that supports thoughtful play. A fleet of dozens of vessels in various states (orbiting, transiting, landed, mission-pending) needs a single coherent entry point. The TV-screens-room metaphor is a working visualization: each screen shows a vessel at its last save-point position; the player navigates between screens to enter contexts. The metaphor scales (default 6-9 screens; scroll/page for overflow) and supports the design's structural commitment that Mission Control pauses the game — the player can spend as long as needed without time pressure.

**The simulator matters** because the design's commitment to honest simulation (commit 002 deterministic physics; commit 011a real telemetry; commit 014 observation as structured activity) requires that players can rehearse without committing real resources. The simulator forks the current game state, runs forward with real physics and real craft data, then reverts on exit. Players use it for trajectory planning, docking practice, craft testing, mission rehearsal. The simulator is honest — it has access only to what the catalog knows; for remotely-scanned bodies it renders parameter-derived defaults that match what would be predictable from the available data; for unscanned bodies it shows nothing. The stylized monitor-within-the-game-view framing with "SIMULATOR" on the bezel and distinct HUD styling prevents the player from mistaking a sim session for the real game.

**The forgiving-restart philosophy matters** because the design's commitment to consequence-bearing decisions (commit 019 crew critical condition; commit 021 opening rescue directive; the entire mission lifecycle) needs to be a *choice* the player makes, not a punishment imposed on them. Abundant saves let players experiment, learn the difficult thing, then succeed the difficult thing. The decisions that carry forward are decisions the player chose to let carry forward. The Fallout 4 reference cites a working precedent for this model: deep mechanical engagement without punishing experimentation.

**The save mechanics implementation matters** because the simulator-sim-branch commitment has Phase 1 implications. The sim-branch save space is a separate file-path namespace, cleared on simulator exit. This is an architectural decision that the save-format subsystem must support from Phase 1. The save format implementation extension makes this explicit alongside the quicksave / autosave / named-save / mode-lock / performance-budget specifications.

The four edits all land in the same atomic write so the cross-references between them resolve: the simulator subsection cross-references Mission Control (same-batch sibling); the Forgiving-restart subsection cross-references the simulator subsection (same-batch sibling); the save format extension cross-references the simulator subsection (same-batch sibling). This is the same forward-reference pattern that commit 016 used to cross-reference commit 015's research subsection from within the same batch.

## Changes

A single atomic write via bash-via-Python with four edits applied in sequence to a single in-memory text buffer, then written atomically:

1. Read the file. Capture seven boundary anchors (B1 §7 catalog tail; B2 §1 off-world mining tail; B3 commit 002 save-format LOCKED opener; B4 commit 002+017 save-load-advance paragraph; B5 commit 002 architectural-backbone paragraph; B6 commit 004a mode-lock paragraph; B7 commit 004a Explicit non-goal closing paragraph). Verify each appears exactly once pre-write.
2. **Edit 1 + 2:** Insert `### Mission Control as primary UI` and `### Sandbox-as-simulator integrated in Mission Control` at the end of §7 (between the existing `### Catalog as long-term meta-game` body and the `## 8. Difficulty and accessibility` heading). Both new subsections added in sequence, separated by `\n\n`.
3. **Edit 3:** Insert `### Forgiving restart philosophy` at the end of §1 (between `### Off-world mining as interstellar gating` body from commit 021 and the `## 2. Foundation` heading).
4. **Edit 4:** Append `**Save mechanics implementation:**` block to end of `### Save format implications` body, after commit 004a's `**Explicit non-goal:**` closing paragraph.
5. Verify all seven boundary anchors still present exactly once. Verify three new subsection headings line-anchored exactly once each. Verify distinctive new-content phrases. Verify ordering using line-anchored regex (rather than substring `find()`) so cross-references inside Edit 4 don't confuse the position checks. Atomic write via `.recovery` + `os.replace`.

File grew from 1739 lines / 197,381 bytes to 1840 lines / 208,353 bytes (+101 lines, +10,972 bytes).

### Note on a verification false-positive

The post-write verification battery flagged one apparent ordering failure: the substring `find()` for `### Sandbox-as-simulator integrated in Mission Control` returned a position in §2 (byte 46378) rather than §7 (byte 180482) because the cross-reference inside Edit 4's save-format extension comes textually first in the file. The data is correct (both occurrences exist, in the right places). The check was wrong: substring `find()` returned the first occurrence, not the heading occurrence. Repaired by using line-anchored regex (`^### Sandbox-as-simulator integrated in Mission Control$`) to find the actual heading position. Effective post-repair verification: 83/83 pass.

This is the structural-vs-substring distinction the fifth workflow rule names. Substring checks treat content textually; line-anchored regex checks treat content structurally. The two semantics differ when a string appears both as a heading and as a cross-reference. The fifth-rule discipline applied: use line-anchored regex for heading checks; use substring for prose anchors.

## Verification

83 checks, all passing after the verification false-positive correction. Six groups:

### A. New content present (22 checks)

- Three new subsection headings line-anchored exactly once: `^### Mission Control as primary UI$`, `^### Sandbox-as-simulator integrated in Mission Control$`, `^### Forgiving restart philosophy$`
- Mission Control content: LOCKED opener (`**LOCKED:** Mission Control is the primary functional UI for the game.`); real-time-render commitment; default 6-9 screen count; scroll/page-controls (intentionally appears twice — once in visual design, once in panel-structure); Mission-Control-pauses-the-game commitment; "change channel" navigation; map-view-integration paragraph
- Simulator content: LOCKED opener; `**Stylized simulator UI.**`; monitor-within-the-game-view framing; "SIMULATOR" on the bezel; "the simulator is honest" commitment
- Forgiving restart content: LOCKED opener; `**Saves in simulator state are sim-state only.**`; Fallout 4 reference
- Save format extension: `**Save mechanics implementation:**` exactly once; `**Simulator-state saves use a separate sim-branch save space.**` exactly once; performance budget 2 seconds; cross-reference to §7 simulator subsection; autosave 5-slot-rotating default

### B. Boundary verbatim-with-context anchors preserved (7 checks)

All seven boundary anchors (B1 through B7) still present exactly once post-write. The save format anchors (B3 through B7) are the most consequential — they protect commit 002 and commit 004a content that Edit 4 appends after.

### C. Ordering using line-anchored regex (3 checks)

- §7 ordering: catalog-as-Channel-16 tail (byte 177259) < Mission Control heading (byte 177511) < Sandbox-as-simulator heading (byte 180482, found via line-anchored regex not substring find) < `## 8. Difficulty and accessibility` heading (byte 183495)
- §1 ordering: Saturn-system off-world-mining tail < Forgiving restart heading < `## 2. Foundation` heading
- §2 save format ordering: commit 004a mode-lock paragraph < commit 004a Explicit non-goal closing paragraph < new save mechanics block

### D. Structural counts (15 checks)

- §1 h3 count = 15 (was 14; +1 from Forgiving restart philosophy)
- §7 h3 count = 7 (was 5; +2 from Mission Control + Simulator)
- §2 h3 count = 15 (unchanged; extension to existing subsection only)
- §3=16, §4=17, §5=6, §6=13, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0 (all unchanged from commit 022)

### E. Section headings (6 checks)

- `## 1. Vision`, `## 2. Foundation (LOCKED unless noted)`, `## 3. Gameplay mechanics`, `## 4. World behavior and discovery`, `## 7. UI and information density`, `## 15. Document status` each line-anchored exactly once

### F. Prior-commit anchors preserved (30 checks)

- Commit 014 damage-repair: no malformed line; restored inline reference present exactly once
- Commit 015 five verbatim-with-context anchors all preserved
- Commit 017 cross-reference sample (six representative cross-references) all preserved
- Commits 018-022 anchors all preserved: commit 018 wavelength-limits, commit 019 crew-tolerances + three-category framing + `Radiation dose, zero-G exposure, mission stress` line, commit 020 storage modules universal, commit 021 four-bodies-intensive-hand-craft + Saturn rings galaxy-wide parenthetical + `^### Off-world mining as interstellar gating$`, commit 022 `^### Asteroid clusters as system composition variant$` + strip-mining + extreme-weather + volumetric nebulae
- Cross-section preserved-content battery: `Engineering as the verb` (§1), `Floating origin shift threshold: 50 km default` (§2), `**Crew are physically located on vessels.**`, `**Crew as a finite resource.**`

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/023_mission_control_simulator_save_mechanics.md
git commit -F commits/023_mission_control_simulator_save_mechanics.md
```
