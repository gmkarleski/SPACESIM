# 022: Procgen extensions — nebulae, extreme weather, asteroid clusters, strip mining

Four additive extensions to §6 (Procedural generation): volumetric nebulae appended to `### Planet variety tiers` (hard-tier addition); `**Extreme weather as feature layer.**` appended to `### Feature-layer architecture`; new `### Asteroid clusters as system composition variant` subsection inserted after `### Feature-layer architecture`; `**Strip mining as crater primitive subtype.**` appended to `### Crater primitives and bounded terrain modification`. All four follow naturally from commits 008 (procgen architecture) and 011a (crater primitives), making explicit what those systems were always designed to support.

The four additions together commit to: nebulae as flyable volumetric regions at hard tier (Phase 7+) with visibility-limited navigation and particle-density gating sensor effectiveness; extreme weather emerging from parameter combinations (high pressure + high axial tilt + fast rotation) producing planet-spanning storm systems that affect atmospheric flight, EVA, and supply route trajectories; asteroid clusters as a system composition variant distinct from broad ring-shaped belts, with concentrated resource access and density-based anomaly hiding; strip mining as a crater primitive subtype distinguishing player-mining-produced linear excavations from impact-produced bowl-with-rim craters, with the explicit commitment that strip mines emerge only from player operations (not seeded as starting procgen content).

§6 grows from 12 to 13 h3 subsections.

## Scope

- `docs/CONSTRAINTS.md` — four edits to §6:
  - Append volumetric-nebulae paragraph to `### Planet variety tiers`
  - Append `**Extreme weather as feature layer.**` paragraph to `### Feature-layer architecture`
  - Insert new `### Asteroid clusters as system composition variant` between `### Feature-layer architecture` and `### The 90/9/1 anomaly distribution`
  - Append `**Strip mining as crater primitive subtype.**` paragraph block to `### Crater primitives and bounded terrain modification`

## Rationale

Commit 008 established the procgen architecture: planet variety tiers (cheap / medium / hard), feature-layer architecture (composable features that combine via tunable parameter thresholds), galaxy scope, per-body physics parameter set. Commit 011a established crater primitives as runtime-mutable terrain modification. What was missing was four specific additions that the existing architecture supported but had not yet committed to:

**Volumetric nebulae as flyable regions.** Commit 008's hard-tier list already names "nebulae as flyable regions" alongside frozen-moment planetary collisions and other Phase 7-8 hero features. This commit adds the implementation specifics: volumetric rendering, navigation hazards (visibility-limited, particle-density gating sensor effectiveness), aesthetic priority for screenshots and atmosphere. Ship target of 1-2 named nebulae at v1 if Phase 7 scope permits; otherwise post-launch.

**Extreme weather as feature layer.** Commit 008's feature-layer architecture already supports parameter-threshold-gated feature activation. This commit names extreme weather as one such feature: high atmospheric pressure plus high axial tilt plus fast rotation activates planet-spanning storm systems (Jupiter-style banding, super-rotating atmospheric jets, season-long hurricanes). Visual: storm systems visible from orbit. Gameplay: atmospheric flight is hazardous in these zones; surface EVA during storms is dangerous; supply route trajectories must account for weather windows.

**Asteroid clusters as system composition variant.** Commit 008 Layer 4 establishes system composition (planets, moons, asteroid belts) emerging from procgen at scale. Asteroid clusters are a variant: spatially compact groupings (dozens to hundreds of bodies in a small volume) rather than broad ring-shaped belts. Often dynamically related (recent disruption event, Lagrange-point captures, fragmented-larger-body remnants). Gameplay distinction: navigating a cluster requires close-quarters trajectory planning; mining yields concentrated resource access; clusters can hide anomalies in their density. Medium-tier per `### Planet variety tiers` — dedicated systems work but not as expensive as hard-tier hero features.

**Strip mining as crater primitive subtype.** Commit 011a established craters as runtime-mutable terrain modification with a per-body crater list. Mining operations conducted over sustained game-time produce a distinct geometric variant — linear/excavated rather than circular bowl-with-rim. Same crater-list-mutable-at-runtime architecture; different geometric primitive in the placement system. Both impact craters and strip mines populate the same crater list per body; rendering distinguishes them by their stored geometry parameters. The architectural commitment that matters for procgen content design: strip mines emerge only from player mining operations. They do not appear as starting procgen content — prior-era industrial activity is not seeded as visible strip-mining terrain modification. (Prior-era artifacts and infrastructure are seeded per `### In-media-res starting state` in §1, but those are Tier A/B/C placed-content items, not procedural terrain modification.) This produces visible long-term consequences of player operations: a heavily-mined body looks visibly transformed across decades of game-time; modification persists across saves.

All four are light-ceremony additions. They make existing procgen architecture commitments concrete in ways that produce engineering decisions and visible long-term consequences.

## Changes

Four atomic edits executed via bash-via-Python in a single operation:

1. Read file. Capture three boundary anchors (one per append target's closing content) plus one placement anchor (`### The 90/9/1 anomaly distribution` heading that new asteroid-clusters subsection lands before). Verify each appears exactly once pre-write.
2. Append volumetric-nebulae paragraph to `### Planet variety tiers` body.
3. Append `**Extreme weather as feature layer.**` paragraph to `### Feature-layer architecture` body.
4. Insert new `### Asteroid clusters as system composition variant` subsection immediately before `### The 90/9/1 anomaly distribution` via exact-string replacement (`### The 90/9/1 anomaly distribution` → new subsection content + `\n\n` + `### The 90/9/1 anomaly distribution`).
5. Append `**Strip mining as crater primitive subtype.**` paragraph block to `### Crater primitives and bounded terrain modification` body.
6. Verify all four boundary anchors still present exactly once. Verify eight new-content phrase anchors. Atomic write via `.recovery` + `os.replace`.

File grew from 1721 lines / 194,238 bytes to 1739 lines / 197,381 bytes (+18 lines, +3,143 bytes).

## Verification

55 checks, all passing on first run. Five groups:

### A. New content present (11 checks)

- Edit 1: `Volumetric nebulae as flyable regions are also hard-tier`; `Ship target: 1-2 named nebulae`
- Edit 2: `**Extreme weather as feature layer.**` exactly once; `Jupiter-style banding, super-rotating atmospheric jets, season-long hurricanes`
- Edit 3: `^### Asteroid clusters as system composition variant$` line-anchored exactly once; LOCKED opener for the new subsection; cross-reference to `### Planet variety tiers` confirming medium-tier classification; explicit framing that clusters are distinct from broad ring-shaped asteroid belts (commit 008 Layer 4 cross-reference)
- Edit 4: `**Strip mining as crater primitive subtype.**` exactly once; `Strip mines emerge only from player mining operations.`; cross-reference to `### In-media-res starting state` in §1 (the Tier A/B/C placement note)

### B. Boundary verbatim-with-context anchors preserved (3 checks)

- B1: `Ship target at v1: 2-3 hard-tier features.` (commit 008 Planet variety tiers closing)
- B2: `Rules are tunable parameters, not hard constraints — edge cases are anomalies worth investigating.` (commit 008 Feature-layer architecture closing)
- B3: `Other terrain modification (trenches, roads, biome changes, water displacement, atmospheric effects from impacts) is deferred to post-v1. The crater and fragmentation primitives cover the dual-use technology framework's visible consequences and the dramatic moments produced by engineering gameplay.` (commit 011a Crater primitives closing)

### C. Ordering checks (4 checks)

- Edit 1: commit-008 tier-list-tail < new nebulae paragraph
- Edit 2: commit-008 feature-layer-tail < new extreme-weather paragraph
- Edit 3 placement: extreme-weather paragraph < new asteroid-clusters subsection < `### The 90/9/1 anomaly distribution` heading
- Edit 4: commit-011a crater-primitives-tail < new strip-mining paragraph

### D. Structural counts (15 checks)

- §6 h3 count = 13 (was 12; +1 from Asteroid clusters subsection)
- §1=14, §2=15, §3=16, §4=17, §5=6, §7=5, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0 (all unchanged from commit 021)

### E. Prior-commit anchors preserved (22 checks)

- Commit 017 section headings (§3 Gameplay mechanics, §4 World behavior and discovery, §15 Document status) each line-anchored exactly once
- Commit 014 damage-repair: no malformed line; restored inline reference present exactly once
- Commit 015 five verbatim-with-context anchors
- Commits 018, 019, 020 anchors all preserved
- Commit 021 anchors: four-bodies-receive-intensive-hand-craft; Saturn rings galaxy-wide procgen-feature parenthetical; `**The opening directive: rescue mission.**`; `### Off-world mining as interstellar gating` exactly once; Phase 0 four-body content lock
- Cross-section preserved: `Engineering as the verb` (§1), `Floating origin shift threshold: 50 km default` (§2), `**Crew are physically located on vessels.**`, three-category framing intact, `Radiation dose, zero-G exposure, mission stress` preserved

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/022_procgen_extensions.md
git commit -F commits/022_procgen_extensions.md
```
