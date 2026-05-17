# 015: Reframe research as logistics-driven question-answering

Replace section 4's `### Research as asynchronous progression` subsection with `### Research as logistics-driven question-answering`. The replacement changes research from abstract tech-tree progression to structured investigation of specific questions, with cascading-but-preserved consequences across five other locations in the doc that reference research.

The new model: research is the structured investigation of specific questions, not abstract progression through a tech tree. Questions arise from gameplay — discovery questions from anomalies, observation results, and unresolved catalog entries; engineering questions from design needs and infrastructure goals. Each question becomes a research project with required activities (specific missions, observations, sample returns, laboratory work), identified logistics costs (vessels, fuel, crew time, instrument time, sample requirements), and outcomes on completion (tech capabilities unlocked, catalog entry closed, follow-up questions opened). The tech tree is not abolished; it is reframed as the accumulated set of resolved engineering questions. "Nuclear thermal engines" is no longer an abstract node — it is the record of a development program the player ran.

The reframe is consequential because it dissolves the parallel between research and discovery. Under the old model, research was a separate progression track running alongside discovery. Under the new model, research IS discovery executed deliberately — the same activities (observations, missions, sample returns) that produce discovery also produce research outcomes. The progression feels meaningful because every advancement traces back to specific work the player did.

The five preserved-with-context cross-references — the section 1 scientist-assignment design pillar; section 2's save format implications; section 3's home-system evolves-autonomously rule; section 3's home-observatory research mechanic; section 8's Phase 6 deliverable list — all describe how research *progresses* (scientist-gated, time-bounded, paused when unassigned). The new model preserves every one of those mechanics. Only the *shape* of research projects changes — from abstract tech nodes to concrete question-answering campaigns. The depot-terminology-cascade pattern from commit 011b applies: preserve verbatim cross-references, let the new subsection authoritatively redefine the meaning underneath.

This commit is the first to use the verbatim-with-context anchor discipline introduced as the fifth workflow rule in `commits/README.md` (commit 014b). It establishes the template that future replacement commits will follow.

## Scope

- `docs/CONSTRAINTS.md` — single span replacement in section 4:
  - Replace the entire span from line `### Research as asynchronous progression` through the end of its body (up to but not including `### Mass and delta-v as central currencies`) with the new heading and body for `### Research as logistics-driven question-answering`.
  - No other edits anywhere in the file. Sections 1, 2, 3, 5, 6, 8, 10, 11, 12, 13, 14 untouched; sections 4's other five subsections untouched.

## Rationale

The original `### Research as asynchronous progression` subsection (commit 001 + commit 004c amendment) named the tech tree as the locked progression mechanism, with tech nodes carrying resource costs and research times and the scientist-assignment rule gating progression. The mechanic was sound; the framing was wrong.

The wrongness has two parts. First, "tech tree" inherits decades of game-industry framing in which technology unlocks are abstract — the player accumulates resources, picks a node, waits, and a capability appears. The framing doesn't naturally connect to discovery. A player who detects an anomaly does not by default see that detection as the start of a tech tree node; they see it as a thing in the universe to investigate. The two activities feel parallel even though they should be the same activity. Second, the old framing offers no answer to "what is research, concretely?" The answer "a node in the tech tree" begs the question: a node in a tech tree corresponds to *what real work*? Without an answer, research becomes abstract grinding.

The new framing dissolves both problems. Research is question-answering. Each project has a stated question (discovery or engineering), required activities (the specific work that resolves the question), logistics costs (the real costs of doing the work), and outcomes (what's learned, what's unlocked, what new questions arise). The activities are the *same activities the player does for discovery* — observations, missions, sample returns, equipment tests. The tech tree is preserved as a representation, but the representation now traces back to specific work.

This is also the framing that integrates research with the rest of the doc's commitments. The detection ecosystem (commit 007) produces anomalies; anomalies open discovery questions; discovery questions become research projects. Mission planning (commit 013) executes the activities required to answer research questions. The catalog (commit 009) becomes the research database — every body's catalog entry includes its open and resolved questions, searchable by status. The home observatory (commit 009) deterministically progresses observatory-related research projects toward instrument capability unlocks. The configurability rule from commit 010 still works — a player who wants everything unlocked from start sets `Progression source: neither` and skips research as a gating mechanism. None of these existing systems need to be rewritten; they all sit naturally on top of the new framing.

The five preserved cross-references warrant explicit treatment because the discipline that protects them is the new discipline (verbatim-with-context anchors) introduced in commit 014b. Each is a verbatim string whose meaning the new model preserves but whose framing implicitly depends on context that could be damaged by an upstream replacement. The anchors check that each cross-reference survives in its full original context, not just that the distinctive phrase still appears somewhere in the file.

## Changes

A single atomic replacement operation, executed via bash-via-Python with pre-write anchor capture, span replacement, and post-construct anchor re-verification before the atomic write. The operation:

1. Read the current file.
2. Capture the five preserved-with-context anchor strings as Python constants and verify each appears exactly once in the pre-write file (assertion failure stops the operation).
3. Locate the replacement span: from byte offset of `### Research as asynchronous progression` (the line) through the byte offset of the next `### Mass and delta-v as central currencies` line (exclusive). 1,195 bytes / 16 lines.
4. Construct the new subsection text (~5KB, 19 lines net growth after the replacement).
5. Build the new file as `text[:old_span_start] + new_subsection + text[next_heading_start:]`.
6. Verify the five anchors are still present exactly once in the constructed text (assertion failure stops the operation).
7. Verify the old heading line is absent and the new heading line is present exactly once in the constructed text (assertion failure stops the operation).
8. Atomic write via `.recovery` + `os.replace`.

File grew from 1591 lines / 169,754 bytes to 1610 lines / 173,851 bytes (+19 lines, +4,097 bytes).

### Replacement content

The new `### Research as logistics-driven question-answering` subsection contains, in order:

- **Opening LOCKED clause** stating the central claim: research is the structured investigation of specific questions, not abstract progression through a tech tree. Questions arise from gameplay; answering requires specific activities; activities have logistics costs; completion produces outcomes.
- **Two question categories** (Discovery questions, Engineering questions), each with worked examples and the sources from which they arise. Discovery questions emerge from anomalies (commit 007), observation results (commit 014), or unresolved catalog entries. Engineering questions emerge from design needs and infrastructure goals.
- **Research project anatomy** with three italic-labeled components: *Required activities* (specific gameplay work, with a worked example for "Investigate disequilibrium chemistry on body X"), *Logistics costs* (connecting research to vessels, supply lines, base infrastructure, and commit 011b's resource list), *Outcomes* (capabilities unlocked, catalog entries closed, follow-up questions opened).
- **The tech tree as questions and answers** — the explicit reframe of what was previously the tech tree, with a worked example for "Nuclear thermal engines" as the record of a real development program.
- **Bounded autonomous progression preserved** — explicit re-commitment to the scientist-assignment rule from commit 004c, with the framing that it applies identically to the question-answering model.
- **Research stations have location bonuses (preserved from prior locked content)** — explicit re-commitment to the location-bonus mechanic from the original subsection, with the parenthetical marking it as deliberately preserved.
- **The catalog (commit 009) becomes the research database** — names the new role of the catalog under the reframe.
- **Research configurable at game creation (commit 010)** — names the still-active configurability cross-reference.
- **Why this reframe matters** — closing paragraph that articulates the structural payoff: research is no longer parallel to discovery, it IS discovery executed deliberately.

The two locked-content commitments from the original subsection — scientist-assignment gating, location bonuses — are both explicitly re-stated in the new subsection with markers noting their preservation. The original subsection's other claims (parallel projects up to a capacity limit; research stations have location bonuses) are subsumed into the new framing without verbatim preservation: the parallel-projects-with-capacity-limit is implied by the question-answering model (each project occupies scientist capacity; multiple projects can run if capacity allows) but the specific "capacity limit (upgradeable)" phrasing does not survive.

## Verification

64 checks, all passing. Organized in seven groups:

### 1. New content present (16 checks)

- File contains exactly one line-anchored heading `^### Research as logistics-driven question-answering$`
- New subsection opens with `**LOCKED:** Research is the structured investigation of specific questions`
- New subsection contains both question categories: `**Discovery questions**` and `**Engineering questions**`
- New subsection contains the `**Research project anatomy:**` structural header
- New subsection contains all three italic component headers: `*Required activities*`, `*Logistics costs*`, `*Outcomes*`
- New subsection contains `**The tech tree as questions and answers.**`
- New subsection contains `**Bounded autonomous progression preserved.**` (the scientist-assignment re-commitment)
- New subsection contains `**Research stations have location bonuses (preserved from prior locked content).**` (the location-bonus re-commitment with explicit preservation marker)
- New subsection contains `**The catalog (commit 009) becomes the research database.**`
- New subsection contains `**Research configurable at game creation (commit 010).**`
- New subsection contains the closing `**Why this reframe matters.**` paragraph header
- New subsection contains the magnetar-shielding worked example
- New subsection contains the nuclear-thermal worked example phrase `tested fuel pellets, validated structural materials`

### 2. Old content absent (5 checks)

- Line-anchored old heading `^### Research as asynchronous progression$` has zero matches
- Old phrase `Tech tree where each node has a resource cost and a research time` is absent
- Old phrase `Multiple research streams run in parallel up to a capacity limit (upgradeable)` is absent
- Old phrase `Starting research consumes resources (samples, rare materials, sometimes crew time)` is absent
- Old phrase `Research completes after a time interval (varies by node complexity)` is absent

### 3. Structural counts via line-anchored regex (7 checks)

- Section 1: 13 `### ` subsections (unchanged)
- Section 2: 15 `### ` subsections (unchanged)
- Section 3: 33 `### ` subsections (unchanged from commit 014)
- Section 4: 6 `### ` subsections (unchanged — one heading replaced by one heading)
- Section 5: 12 `### ` subsections (unchanged)
- Section 6: 5 `### ` subsections (unchanged)
- File total: 1605–1615 lines (actual 1610)

### 4. Five verbatim-with-context anchors at preserved cross-references (5 checks)

Each anchor pairs a distinctive phrase with surrounding context that would be damaged by upstream corruption. All five must appear exactly once in the post-write file:

- **Anchor 1 (section 1 design pillar):** `Research advances when scientists are assigned; supply lines deliver within network capacity; bases produce until depots fill, then idle.`
- **Anchor 2 (section 2 save format implications):** `Research projects advance only for time periods when scientists were assigned (abandoned projects pause). Supply line shipments arrive on schedule, within network throughput limits.`
- **Anchor 3 (section 3 home system evolves autonomously):** `Research doesn't auto-progress without active prioritization. A research project advances when assigned scientists are working on it; abandoned projects pause.`
- **Anchor 4 (section 3 home observatory):** `Observatory research generates results over time, deterministically progressing toward instrument unlocks. Consistent with bounded autonomous evolution — research advances when scientists are assigned.`
- **Anchor 5 (section 8 Phase 6 deliverable list):** `Resource set finalized. Base module system. Supply line graph. Research as resource-and-time. Persistent light-tick simulation of off-screen bases and supply lines. Mass/delta-v integration with resource economics.`

The anchors are explicitly listed here rather than abbreviated because this commit establishes the verbatim-with-context anchor template that future replacement commits will follow. The pattern: capture each preserved cross-reference with at least one full sentence of surrounding context (typically the full sentence containing the distinctive phrase plus its immediate neighbor); verify exact-string count of 1 post-write; document each anchor explicitly in the artifact for future replication.

### 5. Commit 013 + 014 content still present (14 checks)

- Commit 013 anchors: `Crew are physically located on vessels`; exactly-once headings for Sensors as vessel components / Mission planning / Campaigns / Engineering documentation / Failure forensics
- Commit 014 anchors: exactly-once headings for Observation as structured activity / Wavelengths and filters / Observation results; `**Operational chatter.**`; `**Personal entities get chatter.**`
- Commit 014 damage-repair anchors (the malformed-heading hazard must not have regressed): no line matches `^### Channel 16 broadcasts\` below\)\.$`; the restored inline reference `(see \`### Channel 16 broadcasts\` below).` is present exactly once; `^### Channel 16 broadcasts$` is present exactly once

### 6. Cross-section preserved-content anchors (15 checks)

Standard distinctive-phrase battery from sections this commit did not touch:

- Section 1: `Engineering as the verb`; `Combat. Different game, different audience.`
- Section 2: `Floating origin shift threshold: 50 km default`; `Three-mode system`
- Section 4 (heading-level only — body of `Research as logistics-driven question-answering` deliberately changed): `### Resource set`, `### Base structure`, `### Supply lines`, `### Mass and delta-v as central currencies`, `### Life support model`
- Section 5: `90/9/1`
- Section 6: `first hour of play matters more than any other hour`
- Section 8: `### Phase 0 — Decisions`; `### Phase 8 — Polish, content, expansion`; `netcode contract`
- Section 10: `docs/CONSTRAINTS.md`
- Section 13: `Engine rewrite temptation at month six`

### 7. Section 4 internal ordering preserved (1 check)

Section 4's six subsections appear in this exact order (verified by ascending line numbers of the line-anchored headings):

1. `### Resource set`
2. `### Base structure`
3. `### Supply lines`
4. `### Research as logistics-driven question-answering` *(replaced)*
5. `### Mass and delta-v as central currencies`
6. `### Life support model`

The replacement preserves the slot position (4 of 6) and ordering relative to neighbors.

If any check fails, the commit did not land correctly. Particular attention to the five verbatim-with-context anchors: a failure on one of these indicates upstream damage to a section this commit was not supposed to touch, which would require diagnosis before continuing to commit 016.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/015_research_reframe.md
git commit -F commits/015_research_reframe.md
```
