# 020: Parts compatibility, bases-ships interchange, expandable habitats

Four additive paragraphs appended to §3 `### Parts and vessel construction` (Gameplay mechanics). Make the parts vocabulary discipline concrete by committing to four specific consequences: storage modules are universal across contexts; bases and ships share docking standard; some habitat modules deploy after arrival for expanded capacity; cargo modules survive rougher impact than crew. All four follow naturally from commit 011a's parts system; this commit makes the implications explicit.

The unifying claim is that the parts vocabulary is a single vocabulary, not parallel context-specific vocabularies. A hydrogen tank is a hydrogen tank whether it's on a launch vehicle, a supply tanker, a base, or an orbital station. The same docking nodes work everywhere. The structural payoff is that players who learn the parts system once know it for all contexts; cargo can be physically transferred between docked structures; mission planning treats base modules and ship modules as the same kind of object.

Expandable habitat modules add a deployment trade-off that produces real engineering decisions. Inflatable structures, telescoping volumes, and deployed-only-after-arrival assemblies have deployed capacity 3-5× their transit capacity, but deployment is a one-way operation in most cases. Players choose: transit at small footprint and deploy on arrival (more habitat for the launch mass), or accept the deployment-irreversibility cost.

Cargo-survives-rougher-impact-than-crew is the gameplay justification for unmanned supply runs. Cargo modules have G-force tolerance roughly 2-3× crew tolerance, no life support needs during transit, no return vehicle requirements. Unmanned supply runs can use more aggressive trajectories, simpler landers, cheaper missions. This connects directly to commit 019's crew tolerance commitments and to commit 024's coming flight-computer commitments (the trade-off table for manned vs unmanned).

## Scope

- `docs/CONSTRAINTS.md` — single append to §3 `### Parts and vessel construction`:
  - `**Storage modules are universal across contexts.**` paragraph
  - `**Bases and ships use the same docking standard.**` paragraph
  - `**Expandable habitat modules.**` paragraph
  - `**Supplies survive rougher impact than crew.**` paragraph

## Rationale

Commit 011a established the procedural parts system: 35-40 part categories producing potentially hundreds of distinct variants, organized by category and procedurally varied within each. The system was named; its consequences were left implicit. Commit 020 names four specific consequences that the design has been operating under but had not yet committed to in the doc:

**Storage modules are universal.** Implicit in commit 011a — the parts vocabulary was always single, not parallel per context — but stating it explicitly forecloses the design drift where ship parts and base parts develop separate vocabularies. The architectural payoff is one parts UI for everything; the gameplay payoff is players who learn parts once know them for all contexts.

**Bases and ships share docking standard.** Module transfer between docked structures is implicit in shared docking but worth naming because it produces gameplay (moving a habitat module from a ship to a base; moving a fuel tank from a base to a ship for relaunch). Context-specific modules (planetary launch pad, surface VAB) are explicitly excluded.

**Expandable habitat modules.** New commitment, not implicit in commit 011a. Establishes deployment as a part category property: some habitats deploy after arrival for 3-5× capacity. The trade-off (one-way deployment in most cases) makes deployment a player decision rather than a free win.

**Cargo modules survive rougher impact than crew.** Establishes the cargo-vs-crew distinction at the part-tolerance level: cargo modules have G-force tolerance 2-3× crew tolerance, no life support needs, no return requirements. This is the gameplay justification for unmanned supply runs being a distinct mission class with different engineering trade-offs.

All four are light-ceremony additions. They commit to specifics that the design had been operating under without yet stating. None replaces existing content. None changes mechanics; all clarify the consequences of existing commitments.

## Changes

A single atomic append executed via bash-via-Python:

1. Read the file. Capture one boundary anchor at the existing closing of `### Parts and vessel construction` (commit 011a's "35-40 categories produces potentially hundreds of distinct part variants" sentence). Verify it appears exactly once pre-write.
2. Append the four-paragraph block to the end of the subsection body.
3. Verify the boundary anchor still appears exactly once post-write. Verify the four new bold-header phrases each appear exactly once.
4. Atomic write via `.recovery` + `os.replace`.

File grew from 1682 lines / 186,026 bytes to 1690 lines / 187,827 bytes (+8 lines, +1,801 bytes).

### Edit: Append four paragraphs to `### Parts and vessel construction`

Appended after commit 011a's closing paragraph about procedural variation producing hundreds of distinct part variants. The four new paragraphs in order:

1. **Storage modules are universal across contexts.** A hydrogen tank is a hydrogen tank whether on a launch vehicle, supply tanker, base, or orbital station. Same module categories (water tanks, food storage, fissile fuel containers, battery banks) across all contexts. Module capacity scales with size (small/medium/large variants per category). Same docking nodes regardless of context.

2. **Bases and ships use the same docking standard.** Bases can dock with ships and vice versa. Module transfer between docked structures is possible (habitat module from ship to base; fuel tank from base to ship for relaunch). Context-specific modules exist (planetary launch pad, surface VAB) and are obviously not portable, but the general parts vocabulary is interchangeable.

3. **Expandable habitat modules.** Some habitat modules can deploy after arrival — inflatable structures, telescoping volumes, deployed-only-after-arrival assemblies. Deployed capacity is significantly larger than transit capacity (3-5x typical). Trade-off: deployment is a one-way operation in most cases.

4. **Supplies survive rougher impact than crew.** Cargo modules have G-force tolerance roughly 2-3x crew tolerance. No life support during transit. No return vehicle requirements. The gameplay justification for unmanned supply runs: more aggressive trajectories, simpler landers, cheaper missions than crewed equivalents.

## Verification

46 checks, all passing on first run.

### A. New content present (7 checks)

- Each of the four bold-header phrases (`**Storage modules are universal across contexts.**`, `**Bases and ships use the same docking standard.**`, `**Expandable habitat modules.**`, `**Supplies survive rougher impact than crew.**`) present exactly once
- Distinctive phrases: `A hydrogen tank is a hydrogen tank`; `Deployed capacity is significantly larger than transit capacity (3-5x typical)`; `cargo modules have G-force tolerance roughly 2-3x crew tolerance`

### B. Boundary verbatim-with-context anchor (2 checks)

- Commit 011a closing paragraph (`Procedural parts produce many variants from few categories. A habitat module is not one part — it is a category with variation in capacity, duration, gravity context, life support level, specialization. 35-40 categories produces potentially hundreds of distinct part variants.`) present exactly once
- Ordering: boundary anchor appears before new content

### C. Structural counts (15 checks)

H3 subsection counts per section: §1=13, §2=15, §3=16, §4=17, §5=6, §6=12, §7=5, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0. All unchanged from commit 019 (this commit adds no new subsections, only paragraph-level extension).

### D. Prior-commit anchors preserved (22 checks)

- Commit 017 section headings: `## 3. Gameplay mechanics`, `## 4. World behavior and discovery`, `## 15. Document status` each exactly once line-anchored
- Commit 014 damage-repair: no malformed line, restored inline reference present exactly once
- Commit 015 five verbatim-with-context anchors all preserved
- Commit 018 anchors: three new-paragraph headers (`**Wavelength-specific atmospheric limits for ground observatories.**`, `**Resource detection via spectroscopy.**`, `**First-image moments are designed memorable events.**`) all present
- Commit 019 anchors: `**Crew tolerances and endurance growth.**`, `**Cause-dependent recovery thresholds.**`, `**Abandoned craft and bases decay.**` all present; three-category framing protected (`Three-category architecture for crew sustenance and exposure` exactly once; `Radiation dose, zero-G exposure, mission stress` still present verbatim)
- Cross-section preserved: `Engineering as the verb` (§1); `Floating origin shift threshold: 50 km default` (§2); `**Crew are physically located on vessels.**`; `**Crew as a finite resource.**`

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/020_parts_compatibility.md
git commit -F commits/020_parts_compatibility.md
```
