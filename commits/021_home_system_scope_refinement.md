# 021: Home system scope refinement — four-body intensive-craft + Saturn anchor + opening rescue + off-world mining

Refine `### Home system as load-bearing design` (§1) by replacing the abstract "all home system bodies hand-tuned" implementation scope with a concrete four-body intensive-craft scope: home planet, home moon, Mars-equivalent, Saturn-equivalent system. Extend `### In-media-res starting state` (§1) with the rescue-mission opening directive as the engaged-narrative default game-start. Append new subsection `### Off-world mining as interstellar gating` to §1 establishing the resource progression cascade from home-sourced through asteroid mining to outer-system extraction. Extend §9 Phase 0 deliverables list with the four-body content lock.

The home-system scope refinement is the central change. The previous "all home system bodies hand-tuned" commitment from commit 012 was load-bearing in principle (home system carries 30-60 hours of player engagement) but implementation-unbounded — "all bodies" is open-ended. Four-body intensive-craft scope is tractable: home planet (first-hour gameplay), home moon (first major mission target), Mars-equivalent (second major mission target), Saturn-equivalent system (outer-system anchor with multiple hand-tuned moons including an Enceladus-equivalent with subsurface water ocean). All other home-system bodies receive hand-tuned 14-stage parameters (deliberate parameter choices, no random seed) without intensive artifact placement. They feel like specific places without consuming designer time per body.

The Saturn anchor carries specific design content: 5-10 hand-tuned moons, an Enceladus-equivalent with subsurface water ocean (effectively infinite for gameplay purposes — extraction does not deplete meaningfully across a normal playthrough), an existing Saturn-orbit satellite from the prior era carrying sensor data showing the Enceladus water ocean (clue-trail for early investigation), a moon with an inherited rover still transmitting limited telemetry, and the deep-space anomaly's outer-system narrative weight. Rings render as visual structure plus low-detail particle field; mineable as a distributed resource zone without terrain modification. The rings commitment generalizes — "rings remain a galaxy-wide procgen feature emerging from parameter combinations at other gas giants too" — connecting the Saturn anchor to the procgen ring systems §6 already supports per commit 008.

The opening rescue directive establishes the default game-start as an immediate operational situation. One inherited asset has critical life support failure with crew aboard; the opening directive is mount a rescue mission. This demonstrates the game's core systems (mission planning, crew management, supply logistics, real consequences) and reveals the world as one where things happen regardless of player attention. Configurable starting conditions allow alternative openings: pure sandbox, ongoing observatory operations, engineering challenge.

The off-world mining commitment establishes natural infrastructure gating for interstellar capability. Helium-3 for fusion propulsion requires lunar regolith or gas giant atmospheres. Bulk volatiles for laser-sail propellant require outer-system ice bodies or Enceladus-equivalent extraction. Rare earths in mission-grade quantities require specific asteroid bodies. Fissile material in expansion quantities requires asteroid-belt or outer-system sourcing. The progression cascade — Stage 1-2 logistics use home resources entirely; Stage 3 introduces asteroid mining and outer-system ice extraction; Stage 4 interstellar propulsion requires laser arrays requiring fissile expansion requiring asteroid mining; Stages 5-6 interstellar capability requires bulk volatiles — is real engineering, not abstract tech unlocks. The Saturn-equivalent system becomes the natural late-game industrial expansion target.

The §9 Phase 0 extension adds the four-body intensive-craft body identifications to the Phase 0 lock list alongside the existing netcode contract and three-tier content structure deliverables. Specific artifact placements per body remain content-design decisions within the existing artifact-list deliverable; the architectural commitment is the four-body scope itself.

## Scope

- `docs/CONSTRAINTS.md` — four edits:
  - §1 `### Home system as load-bearing design`: replace `**What this requires:**` bullet list AND the closing `**Why this matters.**` paragraph with the new four-body intensive-craft scope (the `**LOCKED:**` opening clause stays verbatim)
  - §1 `### In-media-res starting state`: append `**The opening directive: rescue mission.**` paragraph block
  - §1: insert new subsection `### Off-world mining as interstellar gating` at end of §1 (after `### Home system as load-bearing design`)
  - §9 `### Phase 0 — Decisions (weight 1)`: append four-body content-list paragraph after the existing three-tier content paragraph

## Rationale

The previous "all home system bodies hand-tuned" scope from commit 012 was right in principle (home system load-bearing requires real designer investment) and wrong in implementation bound (open-ended). Four-body intensive-craft scope is the depot-terminology-cascade pattern from commit 011b applied to the home-system commitment: preserve the principle and its load-bearing role; change the implementation underneath.

The four bodies are chosen for their distinct roles in the home-system gameplay arc:

- **Home planet:** first hour of gameplay; fully designed surface, hand-placed landmarks, multiple Tier A artifacts including ISS-equivalent in orbit and active home base infrastructure.
- **Home moon:** first major mission target; inherited base from prior era, Apollo-equivalent landing sites with prior-era hardware.
- **Mars-equivalent:** second major mission target; minimal inherited outpost, prior-era lander remains scattered across the surface, atmospheric characterization that rewards investigation.
- **Saturn-equivalent system:** outer-system anchor carrying the deep-space anomaly narrative weight; gas giant with rings, 5-10 hand-tuned moons, Enceladus-equivalent with subsurface water ocean, prior-era satellite with Enceladus sensor data as clue trail, inherited rover on one moon.

Other bodies in the home system receive hand-tuned 14-stage parameters (no random seed) so they feel like specific places, but no intensive artifact placement. This makes the implementation tractable: designer time scales with four bodies of intensive work, not n bodies of intensive work where n is open-ended.

The Saturn rings galaxy-wide procgen-feature framing makes explicit that rings are not a Saturn-specific designed feature but a procgen feature that emerges at other gas giants too. Commit 008 already lists rings as cheap-tier procgen content (`### Planet variety tiers`), so this is making explicit what commit 008 supported rather than introducing new procgen architecture. The implementation depth statement is concrete: visual ring structure plus low-detail particle field; light particle interactions for passing vessels but no granular per-particle physics; mineable as a distributed resource zone with mining mechanics similar to asteroid mining; no terrain modification (rings are spatial volumes, not surfaces).

The Enceladus-equivalent "effectively infinite for gameplay purposes" framing is the gameplay justification for the player committing to large-scale Enceladus extraction operations without an exhaustion risk that would invalidate the infrastructure investment. Extraction via nuclear excavation or laser-array vaporization produces crater-primitive surface features per the crater system from commit 011a, so the operations have visible long-term consequences without depleting the source.

The opening rescue directive addresses a missing piece in the design: how does play begin? Commit 010 established the in-media-res starting state and configurable starting conditions. This commit names the engaged-narrative default. The rescue framing establishes immediate stakes and demonstrates the core systems (mission planning, crew management, supply logistics, real consequences) in the first hour of play. The configurable alternatives (pure sandbox, ongoing observatory operations, engineering challenge) preserve commit 010's flexibility for players who prefer non-narrative entry.

The off-world mining commitment names natural infrastructure gating from physics. The home planet does not produce enough resources to build interstellar infrastructure. Specific resources — helium-3, bulk volatiles, rare earths in mission-grade quantities, fissile material in expansion quantities — require off-world sources. Players who want interstellar capability must develop off-world mining operations. This is consistent with commit 010's logistics-not-tech progression gating and with commit 015's research framing (research is logistics-driven; the activities have logistics costs). The off-world mining commitment cross-references commit 010's six-stage logistics progression and commit 015's research subsection by name.

The §9 Phase 0 extension is a single paragraph that locks the four-body identification at the architectural level. Specific artifact placements remain content-design decisions within the existing Phase 0 artifact-list deliverable.

## Changes

A single atomic write via bash-via-Python with four discrete edits applied in sequence to a single in-memory text buffer, then written atomically:

1. Read file. Capture six boundary anchors (one per locked-content boundary the edits bracket). Verify each appears exactly once pre-write.
2. **Edit 1 (replacement):** Replace the entire span from `**What this requires:**` through end of `**Why this matters.**` paragraph in `### Home system as load-bearing design` with the new four-body intensive-craft body (six paragraph blocks: the four-body opener, the other-bodies framing, the intensive-craft scope per body with four nested bullets, the Saturn rings implementation depth paragraph, the new Why-this-matters paragraph). The `**LOCKED:** ... 30-60 hours ...` opening clause stays exactly as written.
3. **Edit 2 (append):** Append `**The opening directive: rescue mission.**` paragraph block (three paragraphs: opening situation + crew at risk + rescue mission framing; configurable alternative openings; exploration-objectives-surface-throughout-play closing) to end of `### In-media-res starting state` body. The append lands after commit 016's `**Prior-era artifacts as research opportunities.**` paragraph, which is the current closing of that subsection.
4. **Edit 3 (new subsection):** Insert new `### Off-world mining as interstellar gating` at end of §1 (between `### Home system as load-bearing design` body and `## 2. Foundation` heading). Section 1 grows from 13 to 14 h3 subsections.
5. **Edit 4 (append):** Append the four-body content-lock paragraph to end of §9 `### Phase 0 — Decisions (weight 1)` body after the existing three-tier-starting-content paragraph from commit 010.
6. Verify six boundary anchors still present exactly once. Verify seven absent-phrase checks (every removed bullet from the old "What this requires" list verifiably absent; old closing sentence verifiably absent). Verify new-content phrase anchors present exactly once. Atomic write via `.recovery` + `os.replace`.

File grew from 1690 lines / 187,827 bytes to 1721 lines / 194,238 bytes (+31 lines, +6,411 bytes).

## Verification

85 checks, all passing on first run. Seven groups:

### A. New content present (20 checks)

- Edit 1 anchors: `Four bodies receive intensive hand-craft`, the four body-specific bullets (home planet, home moon, Mars-equivalent, Saturn-equivalent system), `**Saturn rings implementation depth.**`, `rings remain a galaxy-wide procgen feature emerging from parameter combinations at other gas giants too`, `effectively infinite for gameplay purposes`, `visual ring structure plus a low-detail particle field`, `mineable as a distributed resource zone`, the Saturn-orbit-satellite-with-Enceladus-data clue trail, and the new Why-this-matters paragraph (`The four intensive bodies carry the time-investment weight`)
- Edit 2 anchors: `**The opening directive: rescue mission.**`, the configurable-starting-conditions cross-reference, `rescue directive is the engaged-narrative default`
- Edit 3 anchors: `^### Off-world mining as interstellar gating$` line-anchored exactly once, the new subsection's LOCKED opener, the Helium-3 bullet, the Saturn-system-as-natural-late-game-industrial-expansion-target closing
- Edit 4 anchor: the four-body Phase 0 content-lock paragraph

### B. Old content absent (7 checks)

Each removed bullet from the previous "What this requires" list verifiably absent from the new file:

- `All home system bodies hand-tuned through the full 14-stage pipeline.`
- `Hand-placed Tier A artifacts (Voyager-equivalent, ISS-equivalent, Apollo sites, Mars-equivalent landers, the deep-space anomaly).` (note the explicit `Mars-equivalent landers` phrasing — the new Tier A list in §1 In-media-res still has Tier A artifacts but with different phrasing)
- `Hand-placed landmarks on home bodies.`
- `Visual care commensurate with how much time players spend there.`
- `The starting moon base with character and history, not just function.`
- `Deep-space anomalies in the outer system that are known but unreachable with starting tech.`
- `The home frame must be appealing enough that 30-60 hours of engagement produces real investment as a side effect.` (old Why-this-matters closing)

### C. Six boundary verbatim-with-context anchors preserved (6 checks)

- B1: `**LOCKED:** The home system must be rich enough to carry 30-60 hours of player engagement before interstellar capability arrives. This makes home-system design effort load-bearing rather than decorative.` (Home system LOCKED opener preserved; principle intact, implementation underneath updates)
- B2: `**Prior-era artifacts as research opportunities.** The Tier A handcrafted artifacts (Voyager-equivalent, ISS-equivalent, Apollo sites, the deep-space anomaly) are not just narrative weight — they are investigation opportunities.` (commit 016 paragraph survives at Edit 2 boundary)
- B3: `The player's agency is the continuation of the previous era's space program. The first transmissions can acknowledge this:` (commit 010 in-media-res closing paragraph)
- B4: `**Phase 0 deliverable: netcode contract.** Multiplayer netcode architecture requires a dedicated Phase 0 design pass before Phase 1 implementation begins.` (commit 002 netcode contract paragraph)
- B5: `Phase 0 also locks the three-tier starting content structure: identify which Tier A handcrafted artifacts ship at v1 (target 10-20), which Tier B classes ship (target 6-10), and which procgen scope is required for Tier C ambient texture.` (commit 010 three-tier paragraph at Edit 4 boundary)
- B6: `Lock all LOCKED items. Resolve OPEN items that block Phase 1. Document everything (this doc plus companion docs as the project grows).` (§9 Phase 0 opener from commit 001)

### D. Ordering checks (3 checks)

- Edit 4 ordering inside Phase 0: opener < netcode contract paragraph < three-tier paragraph < new four-body paragraph
- Edit 2 ordering inside In-media-res: commit 016 prior-era-artifacts paragraph < new opening-directive paragraph
- Edit 3 placement: `### Home system as load-bearing design` < `### Off-world mining as interstellar gating` < `## 2. Foundation` (new subsection lands at end of §1 with no intervening h3)

### E. Structural counts via line-anchored regex (20 checks)

- §1 h3 count = 14 (was 13; +1 from Off-world mining subsection)
- §2-§15 h3 counts unchanged from commit 020: §2=15, §3=16, §4=17, §5=6, §6=12, §7=5, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0
- Each `## N. Title` heading exactly once line-anchored for §1 through §15

### F. Prior-commit anchors preserved (24 checks)

- Commit 014 damage-repair: no malformed line, restored inline reference present exactly once
- Commit 015 five verbatim-with-context anchors all preserved
- Commit 017 cross-reference sample (six representative cross-references) all preserved
- Commit 018 anchors: three new-paragraph headers all present
- Commit 019 anchors: `**Crew tolerances and endurance growth.**`, `**Cause-dependent recovery thresholds.**`, `**Abandoned craft and bases decay.**`; three-category framing (`Three-category architecture for crew sustenance and exposure`) preserved; `Radiation dose, zero-G exposure, mission stress` preserved
- Commit 020 anchors: four new bold-header paragraphs all present

### G. Cross-section preserved-content battery (5 checks)

`Engineering as the verb` (§1); `Floating origin shift threshold: 50 km default` (§2); `**Crew are physically located on vessels.**`; `**Crew as a finite resource.**`; cross-section anchors from earlier commits.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/021_home_system_scope_refinement.md
git commit -F commits/021_home_system_scope_refinement.md
```
