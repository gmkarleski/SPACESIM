# 017: Split section 3 into Gameplay mechanics + World behavior and discovery

Restructure the canonical document by splitting section 3 (Gameplay systems, 33 subsections after the commits 001–016 series) into two new sections — section 3 "Gameplay mechanics" (16 subsections) and a new section 4 "World behavior and discovery" (17 subsections). All sections numbered 4 through 14 in the pre-split doc renumber to 5 through 15. Every cross-reference to a renumbered section throughout the doc updates to the new numbering. This is the largest structural change to the doc since the initial commit 001 conversion; it touches the entire cross-reference graph but adds no new design content.

The split separates player-engagement systems from universe-behavior systems. New section 3 contains the vessel-construction cluster (parts, failure modes, sensors, automation), the crew cluster (crew, EVA, director), the mission cluster (mission planning, campaigns, engineering documentation, failure forensics from commit 013), design portability (mode-portable designs), physics-as-mechanics (movie-moment mechanics, dual-use technology), and player motivation (goal structure, investment depth). New section 4 contains the discovery flow (information asymmetry → detection ecosystem → platforms → stages → mechanics → observation activity → wavelengths → results → anomaly types → anomalies), interstellar travel, world communication (transmissions, Channel 16), world autonomous behavior (home system, physics-driven gameplay, home observatory), and multiplayer.

The rationale for the split is navigability. Section 3 had accumulated 33 subsections across sixteen prior commits. Reading flow had degraded: the player-engagement-mechanics content and the universe-behavior content interleaved without grouping, and the section was long enough that finding a specific subsection required scrolling or grep. The split also creates clean homes for the design content arriving in commits 018–024 (remote sensing extends section 4; flight computers extend section 3; observatory moments extend section 4; etc.). The cost of the split is one structurally consequential commit; the benefit is doc shape that scales to the design's full surface.

The cross-reference sweep was conducted systematically before any text was written, using six search patterns covering both verbose (`section N`) and shorthand (`§N`) forms, both lowercase and capitalized variants, for both single-digit (3-9) and double-digit (10-14) section references. Seventeen unique matches were found across the renumbered range. Each was classified by reading its context, identifying the subsection it points at, and determining whether that subsection survives in section 3 (no number change) or moves to section 4 or another renumbered section (number changes per the renumbering map). All seventeen are legitimate cross-references; none are spurious incidental occurrences. Sixteen update digits; one (the only reference to "Mode-portable designs and templates," which stays in section 3) remains literally unchanged but is verified present in the post-write file by exact-string match.

The discipline learned from the commits 013–016 series applies in full. The bash-via-Python escape hatch handles the byte size (the file grew only slightly but the operation rewrites the entire file). The verbatim-with-context anchor pattern from commit 014b / commit 015 protects every preserved subsection plus the existing commit 014 damage-repair anchor and the five commit 015 cross-reference anchors. Line-anchored heading-count checks confirm the section structure (one of each `## N. Title` heading; correct h3 count per section). The operation builds the new file by parsing the old file into sections and subsections, applying the seventeen cross-reference updates to their specific chunks (with surgical exact-string replacement that asserts a count of 1 per replacement), reordering subsections in the new section 3 and section 4 per the specified order, renumbering the section-4-through-section-14 headings to section-5-through-section-15, and concatenating with consistent spacing.

## Scope

- `docs/CONSTRAINTS.md` — single structural rewrite:
  - Original `## 3. Gameplay systems` heading replaced with `## 3. Gameplay mechanics`
  - New `## 4. World behavior and discovery` heading inserted, containing 17 subsections moved from the original section 3
  - Original section 3's 33 subsections redistributed between new section 3 (16 subsections) and new section 4 (17 subsections) per the assignment specified in the reconciled source
  - Subsections within each new section reordered per the specified reading-flow order
  - Original `## 4.` through `## 14.` headings renumbered to `## 5.` through `## 15.`
  - Seventeen cross-references to renumbered sections updated in the doc body (sixteen with digit changes, one verified to stay unchanged)

## Rationale

Section 3 had grown to 33 subsections through the commits 001–016 work. The growth was organic and intentional — each addition added necessary content to a section whose scope was "Gameplay systems" interpreted broadly. The cost of the growth was reading flow: by commit 016, finding a specific subsection in section 3 required scrolling through unrelated content, and the section read as a heap rather than a structured argument. The split into two sections with sixteen and seventeen subsections each is the minimum-disruption restructure that recovers reading flow without changing the design content.

The choice of split axis — player-engagement-mechanics versus universe-behavior — emerges from observing how the 33 subsections cluster. The vessel construction subsections (parts, failure modes, sensors, automation) describe what the player builds. The crew subsections describe who the player has. The mission subsections describe what the player does. The detection-ecosystem and observation subsections describe how the universe reveals itself. The home-system-evolves and multiplayer subsections describe how the universe behaves while the player is or isn't watching. The first set is "what the player engages with directly"; the second set is "what the universe does." This is a clean cleavage; the post-split sections have coherent internal arguments.

Two subsection placements deserve explicit note. `### Multiplayer as shared universe` lands in section 4 (World behavior and discovery) rather than section 3 (Gameplay mechanics). Multiplayer is in one sense a property of the player's interaction with the system, which would argue for section 3, but the reconciled source frames it as a property of the shared universe — the multiplayer commitment is that the universe is shared, with all players inhabiting the same physical space and time. This framing makes section 4 the right home: multiplayer is a structural property of the world, not a mechanic the player engages with. Three cross-references to multiplayer that previously said "section 3" now say "section 4." `### Home system evolves autonomously` lands in section 4 for the same reason: home-system autonomous evolution is a world-behavior commitment, not a player-engagement mechanic. References to it from sections 2 and inside the former section 4 (now section 5) update to "section 4."

The reordering within each new section follows the reading-flow priority. In section 3, vessel construction comes first (the player builds before doing anything else), then crew (who's in the vessels), then missions (what crew do in vessels), then portability (designs persist), then physics-as-mechanics, then motivation (goal structure, investment depth). In section 4, discovery flow runs in pipeline order (information asymmetry as the framing principle, then how detection works, then how observation results are interpreted, then anomalies), then interstellar travel (extends the world's reach), then communication (transmissions and Channel 16), then world autonomous behavior (home system evolves, physics-driven gameplay, home observatory), then multiplayer (the structural commitment about shared universe).

The seventeen cross-reference updates are the depot-terminology-cascade discipline applied at maximum scope. Each cross-reference was located by systematic grep before any text was written. Each was classified by identifying the subsection it points at and determining the new numbering. The full table was surfaced in the proposal for human review before the operation began. The discipline is what protects against the failure mode named in the reconciled source: a missed cross-reference that points at the wrong section silently after the split.

## Changes

A single atomic file rewrite via bash-via-Python with multi-stage validation. Sequence:

1. Read the current file (1617 lines / 175,378 bytes).
2. Capture 48 pre-write anchors: 33 verbatim-with-context anchors for old-section-3 subsections (each anchor is the subsection's distinctive opening sentence or two), 5 commit-015 verbatim-with-context anchors for the scientist-assignment cross-references, 1 commit-014 damage-repair anchor for the malformed-heading fix, and 9 misc anchors for distinctive phrases from commits 013–016. Each anchor verified pre-write at exact-count 1.
3. Parse the file into structural chunks: prologue (everything before `## 1. Vision`), section blocks (each `## N. Title` through start of next `## ` heading), and within old section 3, the 33 subsection blocks keyed by title.
4. Apply the seventeen cross-reference updates surgically per the proposal table, using exact-string replacement with assert-count-1 on each. Two replacements (L303 and L349 in section 2) required surrounding-context disambiguation because the literal "see section 3 \`### Multiplayer as shared universe\`" string appeared twice in section 2 and needed unique leading context (the "coordinated time multiplier" and "In multiplayer" prefixes respectively).
5. Construct new section 3 by emitting the new `## 3. Gameplay mechanics` heading followed by the 16 subsection blocks in the specified order. Construct new section 4 by emitting the new `## 4. World behavior and discovery` heading followed by the 17 subsection blocks in the specified order. Each block is the original subsection's heading-plus-body run from the parse step, with whitespace normalized.
6. Renumber the original section-4-through-section-14 headings to section-5-through-section-15 using a regex on the `## N. ` heading line in each block. Each renumber asserts exactly one match.
7. Concatenate prologue, section 1, section 2, new section 3, new section 4, and renumbered sections 5–15 with consistent two-newline separation between blocks.
8. Post-construct validation in the same script: all 48 anchors still present exactly once; all 17 cross-reference updates present with their new text and absent in their old form (or unchanged for L325); all 25 heading checks correct (one new heading per section number, zero old headings); h3 counts per section match the projected values (§1=13, §2=15, §3=16, §4=17, §5=6, §6=12, §7=5, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0).
9. Atomic write via `.recovery` + `os.replace`.

The file grew from 1617 lines / 175,378 bytes to 1618 lines / 175,418 bytes (+1 line, +40 bytes). The minimal byte delta reflects that the operation is structurally consequential but textually local: one new `## 4. World behavior and discovery` heading, the original `## 3. Gameplay systems` heading replaced with `## 3. Gameplay mechanics`, and seventeen surgical character-level digit changes plus three identical-width digit changes. No subsection bodies changed.

## Verification

163 checks, all passing on first run. Five groups:

### A. Section structure (40 checks)

For each section number 3 through 15: line-anchored heading count = 1 with the new title, line-anchored heading count = 0 for the old title (where there was a swap). For section 3 the swap is `Gameplay systems` → `Gameplay mechanics`. For section 4 the swap is `Resources, bases, and logistics` → `World behavior and discovery`. For sections 5–15, the swap is the digit increment of the original section's title (e.g., `## 4. Resources, bases, and logistics` → `## 5. Resources, bases, and logistics`).

Per-section h3 subsection counts: §1=13, §2=15, §3=16, §4=17, §5=6, §6=12, §7=5, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0. Each verified by counting `^### ` lines between the section's `## N. ` heading and the next `## N+1. ` heading.

### B. All 33 old-§3 subsections preserved and uniquely placed (37 checks)

For each of the 33 old-§3 subsection titles: exactly one `^### Title$` line in the file. The new section 3 subsection list matches the specified order (16 titles in the specified sequence). The new section 4 subsection list matches the specified order (17 titles in the specified sequence). The union of new §3 and §4 subsections covers all 33 originals. The intersection is empty (no subsection appears in both new sections).

### C. Verbatim-with-context anchors preserved (48 checks)

- **33 anchors** for the old-§3 subsection openings, each a 1–2 sentence opener that identifies the subsection by distinctive content. Each anchor verified at exact-count 1 post-write.
- **5 commit-015 anchors** at scientist-assignment cross-references (section 1 design pillar, section 2 save format implications, section 4 home system evolves autonomously, section 4 home observatory, section 9 Phase 6 deliverable list — note: anchor texts themselves don't contain section numbers, so the renumbering operation doesn't affect anchor content, only the section where the anchor sits).
- **1 commit-014 damage-repair anchor**: the restored inline reference `(see \`### Channel 16 broadcasts\` below).` is present exactly once. Companion check: no line in the file matches the malformed-heading regex `^### Channel 16 broadcasts\` below\)\.$` that the original commit 014 corruption produced.
- **9 misc anchors** for distinctive phrases from commits 013–016: `**Crew as a finite resource.**`, `**Prior-era artifacts as research opportunities.**`, `**Anomalies as research questions.**`, `**Operational chatter.**`, `**Personal entities get chatter.**`, `**Crew are physically located on vessels.**`, `**Ground-truth verification.**`, `**Bounded autonomous progression preserved.**`, `**Research stations have location bonuses (preserved from prior locked content).**`.

### D. Cross-reference updates (33 checks)

For each of the sixteen digit-changing updates: new content present exactly once, old content absent. The seventeenth (L325, "see section 3's \"Mode-portable designs and templates\" subsection") verified to remain unchanged. Each update's verification anchor uses surrounding context where the cross-reference text alone would be ambiguous; specifically L97 and L691 both produce the same parenthetical `(see section 5's \`### Research as logistics-driven question-answering\`)` and are disambiguated by their leading sentence ("The artifacts seed the early research-as-logistics gameplay" for L97; "Each detected anomaly opens a research question" for L691).

The seventeen updates by source location and target section:

| Source | Old reference | New reference | Reason |
|---|---|---|---|
| §1 L30 | `section 6's \`### First-hour experience\`` | `section 7's \`### First-hour experience\`` | §6→§7 renumber |
| §1 L61 | `section 3 (\`### Multiplayer as shared universe\`)` | `section 4 (\`### Multiplayer as shared universe\`)` | Multiplayer moves to new §4 |
| §1 L97 | `section 4's \`### Research as logistics-driven question-answering\`` | `section 5's \`### Research as logistics-driven question-answering\`` | §4→§5 renumber |
| §1 L105 | `the development build phases in section 8` | `the development build phases in section 9` | §8→§9 renumber |
| §2 L285 | `the broader difficulty toggle system from section 7` | `the broader difficulty toggle system from section 8` | §7→§8 renumber |
| §2 L303 | `coordinated time multiplier (see section 3 \`### Multiplayer as shared universe\`)` | `coordinated time multiplier (see section 4 \`### Multiplayer as shared universe\`)` | Multiplayer moves to new §4 |
| §2 L315 | `bounded-autonomous-evolution rules from section 3's \`### Home system evolves autonomously\`` | `bounded-autonomous-evolution rules from section 4's \`### Home system evolves autonomously\`` | Home system evolves moves to new §4 |
| §2 L325 | `see section 3's "Mode-portable designs and templates" subsection` | (unchanged) | Mode-portable designs stays in new §3 |
| §2 L349 | `In multiplayer (see section 3 \`### Multiplayer as shared universe\`)` | `In multiplayer (see section 4 \`### Multiplayer as shared universe\`)` | Multiplayer moves to new §4 |
| §3 L499 (Automation) | `section 9's \`Mobile shipping\` open question` | `section 10's \`Mobile shipping\` open question` | §9→§10 renumber |
| §3 L661 (Anomaly types) | `(section 5)` | `(section 6)` | §5→§6 renumber |
| §3 L691 (Anomalies) | `(see section 4's \`### Research as logistics-driven question-answering\`)` | `(see section 5's \`### Research as logistics-driven question-answering\`)` | §4→§5 renumber |
| §4 L1104 (Supply lines) | `section 3's \`### Home system evolves autonomously\`` | `section 4's \`### Home system evolves autonomously\`` | Home system evolves moves to new §4 |
| §4 L1118 (Research) | `commit 014's section 3 additions` | `commit 014's section 4 additions` | Commit 014's additions live in new §4 |
| §7 L1366 (Difficulty toggles) | `section 4's \`### Life support model\`` | `section 5's \`### Life support model\`` | §4→§5 renumber |
| §8 L1451 (Phase progression rule) | `section 13` | `section 14` | §13→§14 renumber |
| §10 L1502 (Repo layout) | `section 11 below` | `section 12 below` | §11→§12 renumber |

### E. File-level sanity (4 checks)

- File starts with `# Project Constraints: Space Sim`
- Total line count in 1615–1625 range (actual 1618)
- Exactly 15 `## N. ` second-level headings (one per top-level section, no orphans)
- No `## 16.` or higher heading (no orphan from the renumbering)

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/017_section_3_split.md commits/RECONCILED_NEW_MATERIAL_017-024.md
git commit -F commits/017_section_3_split.md
```

The replay stages the reconciled source for the 017–024 batch alongside the artifact, since the reconciled source is the canonical input for the next three sessions of work and belongs in the repo's commit history alongside its first applied commit.
