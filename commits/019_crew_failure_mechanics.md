# 019: Crew failure mechanics, G-force, self-repair, endurance growth

Three appends across §3 (Gameplay mechanics) and §5 (Resources, bases, and logistics) that make crew failure mechanics concrete. Extend `### Crew and characters` (§3) with per-individual tolerances and endurance growth governed by three specific commitments. Extend `### Failure modes` (§3) with shared ship/base failure vocabulary. Extend `### Life support model` (§5) with G-force as a fourth exposure category, failure-mode-specific log entries and alerts, crew critical condition mechanics, cause-dependent recovery thresholds, two-tier self-repair mechanics, and abandoned-craft decay.

The framing this commit commits to: crew are individuals with their own tolerances; tolerances grow through actual exposure during flight time on a logarithmic curve with a soft ceiling at 2-3× rookie baseline; specific crew matter because they are the only crew with growth in specific categories. Critical condition (not death) is the proximate outcome of exceeded tolerances; critical-condition crew cannot perform tasks but can be cared for and recovered through return to a medical-capable base. Recovery windows are cause-dependent — acute radiation is hours, decompression is minutes-to-hours, G-force injury is days, supply depletion is days-to-weeks. The architectural commitment is that windows are cause-dependent rather than uniform.

Two-tier self-repair: one healthy crew member plus correct supplies repairs a module with parts consumption; two healthy crew members at the failure location repair without supply consumption (they have the engineering knowledge between them to improvise). Routing constrains both paths — even with two crew available, getting them to the failure location takes effort if it's not a pre-flown path.

Abandoned craft and bases decay on a decade-scale timescale. A base abandoned for one year is mostly fine; a base abandoned for ten years has lost most non-critical instruments and has multiple module failures. Bringing two healthy crew restores full functionality per the auto-repair rule. This produces "the legacy" dramatic shape from §1 — abandoned outposts to rehabilitate, derelict ships to recover.

The commit preserves commit 011b's three-category architecture for life support (consumption resources, exposure tracking, discrete failure modes) verbatim. G-force enters as a fourth *exposure category* in a separately appended paragraph; the existing "Exposure tracking" line that names radiation, zero-G, and mission stress remains exactly as commit 011b wrote it. The reconciled source's explicit instruction: "the three-category text is preserved verbatim, and G-force is the appended fourth." That discipline applied.

G-force operates at two layers in the new model. The discrete-failure-modes line in commit 011b lists G-force injury as a one-event acute failure. The new exposure-category paragraph adds G-force tolerance as a cumulative quantity that can grow through endurance and produces critical condition when exceeded. These are two framings of the same physical phenomenon coexisting naturally — single-event injury versus cumulative tolerance — and both apply.

## Scope

- `docs/CONSTRAINTS.md` — three appends:
  - §3 `### Crew and characters`: append `**Crew tolerances and endurance growth.**` paragraph block after commit 016's `**Crew as a finite resource.**` paragraph
  - §3 `### Failure modes`: append `**Ship and base failure modes share vocabulary.**` paragraph
  - §5 `### Life support model`: append `**G-force as a fourth exposure category (alongside radiation, zero-G, mission stress).**` followed by `**Life support failure flavor.**` + 5 failure-mode bullets, `**Crew critical condition mechanics.**`, `**Cause-dependent recovery thresholds.**` + 6 cause-window bullets, `**Self-repair mechanics with two-tier difficulty.**`, and `**Abandoned craft and bases decay.**`

## Rationale

The locked content this commit refines was established by commit 011b (life support model) and commits 001 + 013 + 016 (crew and characters). What was missing was the operational layer: what happens when a tolerance is exceeded; how long before critical becomes fatal; how repair works; what happens to craft left unattended.

The reconciled source frames these as a coherent set: tolerance is per-crew and grows through exposure (Edit 1); failure modes are shared between bases and ships because they share parts (Edit 2); the life support model gains a fourth exposure category, specific failure-mode log entries, critical-condition mechanics, cause-dependent recovery thresholds, two-tier self-repair, and decade-scale abandoned-craft decay (Edit 3). Each piece on its own is a concrete commitment; together they produce the rescue-mission gameplay loop, the veteran-pilot-matters mechanic, the rehabilitation-of-derelicts dramatic shape, and the repair-as-real-engineering-with-real-time loop.

The tolerance-growth commitments are tight on purpose. Per-category growth is the architectural commitment that prevents tolerance scaling from making veterans universally capable — a high-G veteran has G-force tolerance, not radiation tolerance, unless they have actually flown radiation missions. Logarithmic curve with soft ceiling at 2-3× rookie baseline keeps the gap between rookie and veteran in a range where rookies are useful and veterans are valuable. Both commitments preserve crew variation as a player-decision space.

The two-layer G-force framing matters for protecting commit 011b's architecture. The existing `**Failure modes (discrete).**` line lists G-force injury as one of five discrete failure events (alongside decompression, thermal failure, radiation event, supply depletion). The new `**G-force as a fourth exposure category**` paragraph adds G-force as a cumulative tolerance quantity that grows through flight time and produces critical condition when exceeded. These are not in conflict — they describe the same physical phenomenon at different time-scales (acute injury vs cumulative tolerance). Both layers operate. The reconciled source explicitly preserves commit 011b's three-category language and adds G-force as the *fourth* category in a separate appended paragraph rather than restructuring the three-category architecture.

Cause-dependent recovery thresholds are the architectural commitment that gives rescue missions narrative weight at the right time-scale per failure type. Acute radiation = hours-to-days; the player has to drop everything and launch. Supply depletion = days-to-weeks; the player has time but it's finite. G-force injury = days; the patient stabilizes but needs medical attention. Each window produces a different gameplay tempo. Uniform recovery windows would flatten this; cause-dependent windows preserve the distinction.

The decade-scale abandoned-craft decay produces "the legacy" dramatic shape from §1. Bases left without crew accumulate failures over years. A ten-year-abandoned base has most non-critical instruments offline and multiple module failures. Bringing two healthy crew restores full functionality per the auto-repair rule. This is the rehabilitation-of-derelicts gameplay loop — players can find and recover what previous play-eras (or the prior era's program) left behind.

## Changes

Three atomic appends executed via bash-via-Python in a single operation:

1. Read the file. Capture seven boundary anchors representing the existing content the appends bracket against. Each anchor is verified pre-write at count exactly 1.
2. Apply Edit 1: append `**Crew tolerances and endurance growth.**` paragraph block (one opening paragraph + three bulleted commitments + one closing paragraph) to end of `### Crew and characters` body.
3. Apply Edit 2: append `**Ship and base failure modes share vocabulary.**` paragraph (one paragraph + closing sentence) to end of `### Failure modes` body.
4. Apply Edit 3: append the full life-support-model extension (six bold headers + bullet lists + commitments) to end of `### Life support model` body, after commit 011b's closing `Architecture lands in Phase 2 ... Phase 6` sentence.
5. Verify all seven boundary anchors still appear exactly once. Verify 22 new bold-header phrases appear exactly once each. Verify three-category framing from commit 011b survives verbatim. Verify no "Four-category architecture" rewrite leaked in.
6. Atomic write via `.recovery` + `os.replace`.

File grew from 1632 lines / 178,466 bytes to 1682 lines / 186,026 bytes (+50 lines, +7,560 bytes).

### Edit 1: Append `**Crew tolerances and endurance growth.**` to `### Crew and characters`

Appended after commit 016's `**Crew as a finite resource.**` paragraph (which is the current closing of the subsection per commits 001 base + 013 physical-location + 016 finite-resource). One opening paragraph naming the four tolerance categories (G-force, radiation, zero-G exposure, mission stress) with the commitment that tolerances vary across crew. Three bulleted commitments governing endurance growth: per-category growth (each category grows independently based on actual exposure), logarithmic curve (rapid improvement in early flight hours, slow improvement after thousands), soft ceiling at 2-3× rookie baseline (specific magnitudes are Phase 5 balance parameters). One closing paragraph framing the gameplay payoff: a veteran high-G pilot is the only crew member with grown G-force tolerance; losing them means losing capability that takes game-years to rebuild.

### Edit 2: Append `**Ship and base failure modes share vocabulary.**` to `### Failure modes`

Appended after commit 001's closing "All scaled by player difficulty settings (off / cosmetic / functional / brutal)" sentence. Two paragraphs. First commits to shared failure vocabulary across bases and ships because they share parts (commit 011a). Lists non-critical instrument failures (telescope module failed, mass spectrometer module failed, communications array degraded) that lose specific functionality without disabling the whole structure. Names life support failure modes on bases (radiation exposure, decompression, supply depletion, contamination) as manifesting identically to those on ships. Second paragraph: the shared vocabulary means players who learn ship failure handling already know base failure handling; the mental model transfers.

### Edit 3: Append life-support-model extension to `### Life support model`

Appended after commit 011b's closing `Architecture lands in Phase 2 (body parameter set supports it). Full implementation in Phase 6 with the resource system.` sentence. The closing sentence remains as the close of the original commit-011b architecture commitment; the new content adds the operational layer below it. The new content comprises:

- `**G-force as a fourth exposure category (alongside radiation, zero-G, mission stress).**` — names G-force as cumulative exposure (distinct from the discrete-injury framing in commit 011b's `**Failure modes (discrete).**` line, which remains untouched). Sustained or peak overlimits cause critical condition; brief overlimits cause transient stress. G-force is real telemetry per `### Movie-moment mechanics` in §3. Warnings issue as crew approach tolerance.
- `**Life support failure flavor.**` — five specific failure modes with framing (food contamination, water loss, atmospheric failure, radiation event, thermal failure). Each produces a transmission with specific framing and a log entry.
- `**Crew critical condition mechanics.**` — exceeding tolerance produces critical condition; critical crew cannot perform tasks; healthy crew on same vessel/base can care for them; recovery via return to medical-capable base within cause-specific window.
- `**Cause-dependent recovery thresholds.**` — six bullets naming windows per cause: acute radiation (hours), decompression (minutes-to-hours), G-force injury (days), thermal failure (hours-to-days), supply depletion (days-to-weeks), zero-G/mission stress (long, chronic). Architectural commitment: windows are cause-dependent, not uniform. Specific values are Phase 5 balance parameters.
- `**Self-repair mechanics with two-tier difficulty.**` — one healthy crew + correct supplies repairs with parts consumption; two healthy crew + presence at location repairs without supply consumption. Both take hours-to-days depending on module severity. Routing naturally constrains repair access.
- `**Abandoned craft and bases decay.**` — decade-scale timescale; one-year abandonment mostly fine; ten-year abandonment loses most non-critical instruments. Two healthy crew restores full functionality. Produces "the legacy" dramatic shape from §1.

## Verification

78 checks, all passing on first run. Five groups:

### A. New content present (26 checks)

All 22 new bold-header phrases verified at count exactly 1 (Crew tolerances and endurance growth; Per-category growth; Logarithmic curve; Soft ceiling at ~2-3x rookie tolerance; Ship and base failure modes share vocabulary; G-force as a fourth exposure category; Life support failure flavor; Food contamination; Water loss; Atmospheric failure; Radiation event; Thermal failure; Crew critical condition mechanics; Cause-dependent recovery thresholds; Acute radiation exposure; Decompression / atmospheric failure; G-force injury; Thermal failure (heat or cold injury); Supply depletion (food, water); Zero-G or mission stress exceedance; Self-repair mechanics with two-tier difficulty; Abandoned craft and bases decay). Plus specific worked-example anchors: `hours to days based on module severity`, `veteran ceiling sits at roughly 2-3x the rookie baseline`, `"the legacy" dramatic shape` cross-reference into §1, G-force telemetry cross-reference into `### Movie-moment mechanics` in §3.

### B. Seven boundary verbatim-with-context anchors preserved (7 checks)

- B1: `**LOCKED:** Three-category architecture for crew sustenance and exposure.` (commit 011b opener, three-category framing protected)
- B2: `**Exposure tracking (cumulative).** Radiation dose, zero-G exposure, mission stress. Accumulate over time with thresholds. Mitigated by shielding (mass cost), spin gravity (engineering cost), mission rotation (logistics cost).` (commit 011b three-category list, the key anchor per reconciled source instruction)
- B3: `**Consumption resources (continuous).** Food, water, oxygen consumed per crew per day at fixed rates.` (commit 011b consumption category)
- B4: `**Failure modes (discrete).** Decompression, thermal failure, G-force injury, radiation event, supply depletion. Fire when conditions met.` (commit 011b discrete-failure-modes line; note: G-force injury is here as a discrete event, distinct from the new exposure-category framing)
- B5: `Architecture lands in Phase 2 (body parameter set supports it). Full implementation in Phase 6 with the resource system.` (commit 011b closing implementation-phasing sentence; remains as logical close of original architecture, new content appended after)
- B6: `**Crew as a finite resource.** The player's program has finite crew. Each crew member is one person with their own training, history, and skills.` (commit 016 finite-resource paragraph, Edit 1 boundary)
- B7: `All scaled by player difficulty settings (off / cosmetic / functional / brutal). Defaults are friendly for new players.` (commit 001 failure-modes closing, Edit 2 boundary)

### C. Three-category framing protection (3 checks)

The reconciled source's most specific instruction is that commit 011b's three-category architecture remains verbatim and G-force is appended as the fourth in a separate paragraph. Three explicit checks:

- `Three-category architecture for crew sustenance and exposure` present exactly once
- `Radiation dose, zero-G exposure, mission stress` (the exposure-tracking three-category list) present exactly once
- `Four-category architecture` absent (no accidental rewrite of the original three-category framing into four)

### D. Ordering checks (3 checks)

Each edit's boundary anchor appears before its appended new content:

- Edit 1: commit 016 `**Crew as a finite resource.**` precedes new `**Crew tolerances and endurance growth.**`
- Edit 2: commit 001 `All scaled by player difficulty settings` precedes new `**Ship and base failure modes share vocabulary.**`
- Edit 3: commit 011b `Architecture lands in Phase 2` precedes new `**G-force as a fourth exposure category**` block

### E. Structural counts via line-anchored regex (15 checks)

H3 subsection counts per section: §1=13, §2=15, §3=16, §4=17, §5=6, §6=12, §7=5, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0. All unchanged from commit 018.

### F. Prior-commit anchors preserved (24 checks)

- Commit 017 section headings: `## 3. Gameplay mechanics`, `## 4. World behavior and discovery`, `## 5. Resources, bases, and logistics`, `## 15. Document status` each line-anchored exactly once
- Commit 014 damage-repair: no malformed `^### Channel 16 broadcasts\` below\)\.$` line; restored inline reference present exactly once
- Commit 015 five verbatim-with-context anchors all preserved
- Commit 017 cross-reference sample (six representative cross-references) all preserved at post-017 values
- Commit 018 anchors: three new-paragraph headers from yesterday's commit (`**Wavelength-specific atmospheric limits for ground observatories.**`, `**Resource detection via spectroscopy.**`, `**First-image moments are designed memorable events.**`) all present
- Cross-section preserved-content battery: `Engineering as the verb` in §1; `Floating origin shift threshold: 50 km default` in §2; `**Operational chatter.**`; `**Crew are physically located on vessels.**`

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/019_crew_failure_mechanics.md
git commit -F commits/019_crew_failure_mechanics.md
```
