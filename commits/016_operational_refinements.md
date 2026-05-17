# 016: Three operational refinements connecting crew, artifacts, and anomalies to the research reframe

Three small additive paragraphs across sections 1 and 3. All light-ceremony pure appends to clean parts of the doc. Two of the three introduce forward-references to commit 015's new `### Research as logistics-driven question-answering` heading, which is the natural cross-reference now that 015 has landed.

The three refinements: (1) crew-as-a-finite-resource paragraph appended to `### Crew and characters` (section 3), deepening the crew-as-real-people thread from commit 013's physical-location commitment; (2) prior-era-artifacts-as-research-opportunities paragraph appended to `### In-media-res starting state` (section 1), connecting the Tier A handcrafted artifacts to the new research framing as concrete first questions the player can investigate; (3) anomalies-as-research-questions paragraph appended to `### Anomalies and mysteries` (section 3), making explicit that anomaly investigation IS research under the new model — same activities, same logistics, same outcomes.

Together these refinements connect three previously-named gameplay elements (crew, prior-era artifacts, anomalies) to the new research framing from commit 015. None of the additions change locked commitments; they make explicit connections that the new model implies but had not yet stated.

## Scope

- `docs/CONSTRAINTS.md` — three pure appends:
  - Append `**Crew as a finite resource.**` paragraph to `### Crew and characters` (section 3), after commit 013's physical-location paragraph
  - Append `**Prior-era artifacts as research opportunities.**` paragraph to `### In-media-res starting state` (section 1), after the existing closing paragraph
  - Append `**Anomalies as research questions.**` paragraph to `### Anomalies and mysteries` (section 3), at end of existing subsection

## Rationale

Commit 015's research reframe made research and discovery one activity rather than two. That reframe has implicit consequences for several other gameplay elements that had been described in their own terms but should now be understood as connected to research. Commit 016 makes three of those connections explicit.

**Crew as finite resource** extends commit 013's physical-location commitment. Commit 013 stated that crew are physically located on vessels and that crew rotation is real logistics. The commitment that commit 013 left implicit was: crew are also a *finite* resource, not interchangeable, with their own training and history. The new paragraph makes that explicit. The structural payoff is that crew assignment becomes a real decision under the research framing: sending the crew with the right skills on a long mission means those individuals are unavailable for other research projects until they return. Interstellar missions amplify this — the right crew might be unavailable for decades or never return.

**Prior-era artifacts as research opportunities** connects commit 010's in-media-res starting state (Tier A handcrafted artifacts: Voyager-equivalent, ISS-equivalent, Apollo sites, deep-space anomaly) to commit 015's research framing. The original commit 010 framing treated the Tier A artifacts as narrative weight — markers that establish the player's program continues an earlier era's space program. The new paragraph adds that they are also concrete first research questions. Each artifact opens specific investigations: retrieving the Voyager-equivalent's recorded data, investigating why the generation ship lost contact, sampling the deep-space anomaly. This seeds the early research-as-logistics gameplay with questions that exist before the player has detected any procedurally-generated anomalies.

**Anomalies as research questions** is the most direct cross-reference. Commit 008 established the 90/9/1 anomaly distribution rule and commit 007 named the detection ecosystem that produces anomalies. Under commit 015's research framing, every detected anomaly opens a discovery question; investigation is the research activity that resolves the question; the catalog (commit 009) tracks the question's status from detection through resolution. The new paragraph states this connection explicitly so future readers do not have to derive it.

The forward-reference pattern matters here. Both edit 2 (in-media-res) and edit 3 (anomalies) cross-reference `### Research as logistics-driven question-answering` by name — using backtick-wrapped inline heading references in the same shape that caused commit 014's truncation failure. Commit 014b's fifth workflow rule explicitly addressed this hazard. The verbatim-with-context anchors at the three append boundaries (defined below in the Verification section) catch any regression where the rstrip-and-append pattern damages the preserved-content boundary.

## Changes

Three atomic appends executed via bash-via-Python in a single operation with pre-write boundary-anchor capture, post-construct boundary-anchor re-verification, and atomic write. The operation:

1. Read the file.
2. Capture three boundary-anchor strings as Python constants and verify each appears exactly once pre-write.
3. For each of the three subsections, locate the body (heading line through start of next `### ` heading), strip trailing whitespace from the body, append the new paragraph with proper spacing, and substitute back.
4. Verify all three boundary anchors still appear exactly once in the constructed text.
5. Verify the three new-content phrase anchors appear exactly once.
6. Atomic write via `.recovery` + `os.replace`.

File grew from 1610 lines / 173,851 bytes to 1616 lines / 175,378 bytes (+6 lines, +1,527 bytes).

### Edit 1: Append `**Crew as a finite resource.**` to `### Crew and characters`

Appended after commit 013's `**Crew are physically located on vessels.**` paragraph. The new paragraph: the player's program has finite crew; each crew member is one person with their own training, history, and skills; crew are not interchangeable; sending crew on a long mission means those specific individuals are unavailable for other missions until they return — or in the case of interstellar missions, possibly never; the player's choices about who goes where shape the program's character.

### Edit 2: Append `**Prior-era artifacts as research opportunities.**` to `### In-media-res starting state`

Appended after the existing closing paragraph (`The player's agency is the continuation of the previous era's space program. ...`). The new paragraph: the Tier A handcrafted artifacts are not just narrative weight, they are investigation opportunities; each Tier A artifact opens specific research questions when encountered; retrieving the Voyager-equivalent's recorded data is a research project; investigating why the generation ship lost contact is a research project; the artifacts seed the early research-as-logistics gameplay (cross-reference to `### Research as logistics-driven question-answering`) by providing concrete first questions the player can investigate.

### Edit 3: Append `**Anomalies as research questions.**` to `### Anomalies and mysteries`

Appended at the end of the existing subsection (which already contained commit 001 base content plus the 90/9/1 distribution from commit 008). The new paragraph: each detected anomaly opens a research question (cross-reference to `### Research as logistics-driven question-answering`); the 90/9/1 distribution (commit 008) governs how often investigation resolves to interesting outcomes; the catalog (commit 009) tracks the question's status from detection through investigation to resolution; anomaly investigation IS research — same activities, same logistics, same outcomes.

## Verification

56 checks, all passing. Organized in five groups.

### 1. New content present (9 checks)

- `**Crew as a finite resource.**` present exactly once
- Crew-as-finite-resource paragraph contains `finite crew` and `specific individuals are unavailable`
- `**Prior-era artifacts as research opportunities.**` present exactly once
- Prior-era-artifacts paragraph contains the full Tier A list `Tier A handcrafted artifacts (Voyager-equivalent, ISS-equivalent, Apollo sites, the deep-space anomaly)`
- `**Anomalies as research questions.**` present exactly once
- Anomalies-as-research-questions paragraph contains `Anomaly investigation IS research`
- The inline cross-reference `(see section 4's \`### Research as logistics-driven question-answering\`)` appears exactly twice (once in edit 2, once in edit 3)

### 2. Structural counts via line-anchored regex (7 checks)

No h3 count changes anywhere — this commit is pure appends.

- Section 1: 13 (unchanged)
- Section 2: 15 (unchanged)
- Section 3: 33 (unchanged from commit 014)
- Section 4: 6 (unchanged from commit 015)
- Section 5: 12 (unchanged)
- Section 6: 5 (unchanged)
- File total: 1611–1620 lines (actual 1616)

### 3. Three boundary verbatim-with-context anchors (6 checks)

The three append boundaries each carry a verbatim-with-context anchor on the pre-existing content immediately before the new paragraph, plus an ordering check confirming the new paragraph lands after the preserved content and before the next subsection. The anchors:

- **Boundary 1 (commit 013 physical-location paragraph must survive at end of `### Crew and characters`):** `**Crew are physically located on vessels.** Each crew member is at a specific location at any given time — aboard a specific vessel, at a specific base, or in transit.`
- **Boundary 2 (commit 010 in-media-res closing paragraph must survive at end of `### In-media-res starting state`):** `The player's agency is the continuation of the previous era's space program. The first transmissions can acknowledge this: review meetings reference Voyager-2 telemetry, ISS retirement decisions, the laser propulsion research program. The world has history; the player is its current author.`
- **Boundary 3 (commit 001 `### Anomalies and mysteries` existing tail must survive):** `A few hand-authored mysteries can be seeded into the procedural generation for cross-system narrative threads`

Each anchor verified to appear exactly once verbatim. Ordering checks confirm: heading < boundary anchor < new paragraph < next subsection, for each of the three edits.

### 4. Prior-commit content still present (24 checks)

- **Commit 013 anchors:** `### Sensors as vessel components`, `### Mission planning as structured persistent activity`, `### Campaigns as multi-mission programs`, `### Engineering documentation produced by play`, `### Failure forensics` (each exactly once, line-anchored); `**Ground-truth verification.**` present
- **Commit 014 anchors:** `### Observation as structured activity`, `### Wavelengths and filters as concrete gameplay`, `### Observation results as interpretable data` (each exactly once, line-anchored); `**Operational chatter.**` present
- **Commit 014 damage-repair anchors:** no line matches `^### Channel 16 broadcasts\` below\)\.$`; restored inline reference `(see \`### Channel 16 broadcasts\` below).` present exactly once
- **Commit 015 anchors:** `### Research as logistics-driven question-answering` present exactly once line-anchored; old `### Research as asynchronous progression` has zero line-anchored matches; `**Bounded autonomous progression preserved.**` present; `**Research stations have location bonuses (preserved from prior locked content).**` present
- **Commit 015 five verbatim-with-context anchors** all still present exactly once (the section 1 design pillar, section 2 save format implications, section 3 home system evolves autonomously, section 3 home observatory, section 8 Phase 6 deliverable list)

### 5. Cross-section preserved-content anchors (13 checks)

Standard distinctive-phrase battery from sections this commit did not touch:

- Section 1: `Engineering as the verb`; `Combat. Different game, different audience.`
- Section 2: `Floating origin shift threshold: 50 km default`; `Three-mode system`
- Section 4: heading-level only — `### Resource set`, `### Mass and delta-v as central currencies`, `### Life support model`
- Section 5: `90/9/1`
- Section 6: `first hour of play matters more than any other hour`
- Section 8: `### Phase 0 — Decisions`; `netcode contract`
- Section 10: `docs/CONSTRAINTS.md`
- Section 13: `Engine rewrite temptation at month six`

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/016_operational_refinements.md
git commit -F commits/016_operational_refinements.md
```
