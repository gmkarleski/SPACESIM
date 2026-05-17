# 037: Phase 0 artifact list for v1 — Tier A/B/C content decisions

Land `docs/PHASE_0_ARTIFACT_LIST.md` cataloging the specific named content and design decisions that ship in v1. The document classifies v1 content as Tier A (must lock for v1 implementation — affects code architecture, scene authoring, or other systems' design), Tier B (lock for v1 with flexible specifics during content authoring), and Tier C (defer to content authoring). It locks the Saturn astronaut rescue as v1's central narrative arc, the two-eras framing (prior wave 2020s-2040s; current era 2050-2100), the Tier A artifact set (Voyager-equivalent, ISS-equivalent, Apollo-equivalent sites, Mars landers, inherited rover, Saturn station with two astronauts, distant interstellar-medium destination), the starting state (3 named crew + 3 pre-built vessels + home base + paced crew hiring), and the v1 physics scope (PhysX-active + Kepler-rails only; interstellar-cruise mode deferred to post-launch).

This commit closes the "Phase 0 artifact list" item from the Phase 0 remaining work checklist. Three items remain before Phase 1 implementation can honestly begin: vessel containers per netcode contract §2, a Kepler-rails mode test, and a PhysX-active ↔ Kepler-rails mode transition test.

This commit also performs two PHASE_TRACKER.md cleanup-as-you-pass items: backfilling the missing commit 036 row in the Recently landed table (commit 036's own update didn't include itself in the table), and stripping the "this commit" annotation from the operational scaffolding checklist item (the marker was correct at commit 036 landing but created drift the moment a subsequent commit ran). Going forward, the PHASE_TRACKER discipline drops the "this commit" annotation entirely — commit references alone are unambiguous given the Recently landed table at the top, and removing the marker eliminates per-commit drift maintenance overhead.

## Scope

- `docs/PHASE_0_ARTIFACT_LIST.md` — created. 258 lines / 15,028 bytes. Six main sections: v1 scope summary, Tier A (must lock), Tier B (lock with flexible specifics), Tier C (defer to content authoring), cross-references, update history. The Tier A section is the architectural heart of the document — it names the content whose decisions affect code structure, including the Saturn rescue arc with its system implications (mission planning, life support, discovery progression, crew tracking), the seven Tier A artifacts with their locations/eras/reveals, the starting state with its save-format and tutorial implications, and the v1 physics scope with its prototype-validation implications.
- `docs/PHASE_TRACKER.md` — modified, three edits:
  1. Recently landed table: added rows for commits 037 (this commit) and 036 (backfilled from previous commit's missing self-reference).
  2. Phase 0 remaining work checklist: stripped "this commit" annotation from the operational scaffolding line (was `(commit 036, this commit)`, now `(commit 036)`).
  3. Phase 0 remaining work checklist: toggled the Phase 0 artifact list item from unchecked to checked, with `(commit 037)` reference appended.
- `commits/037_phase_0_artifact_list.md` — created (this artifact).

No CONSTRAINTS.md changes. No code changes. No tests. No DECISIONS.md changes (the artifact list operates within already-locked design decisions; the document references them via the Cross-references section but doesn't change them). No NETCODE_CONTRACT.md changes. No ARCHITECTURE.md changes. No SESSION_PROTOCOL.md changes. No `commits/README.md` changes (workflow rules 1-6 preserved).

## Rationale

### Why this document needs to exist

CONSTRAINTS.md catalogs design constraints. DECISIONS.md records resolved questions. PHASE_TRACKER.md tracks operational state. NETCODE_CONTRACT.md specifies the architectural contract. ARCHITECTURE.md describes implementation patterns. None of these documents directly answers the question "which specific named content ships in v1?"

The Phase 0 artifact list is the missing piece. It bridges from the abstract design commitments (atmospheric flight pillar from commit 025, Mission Control UI from commit 023, four intensive-craft bodies from commit 021, discovery-based research from commit 015) to the concrete content decisions that drive Phase 1+ implementation: how many starting crew? how many vessels at home base? which artifacts are pre-populated in the catalog? what's the v1 endgame target?

Without this list, implementation work in Phase 1+ would constantly hit "wait, what's the design intent here" friction. With this list, implementation reads the relevant Tier A section and proceeds.

### Why three tiers

Not all v1 content has the same urgency for design lock-in. Three distinct categories emerged from the design discussion:

**Tier A** — content where the design decision affects code structure or system architecture. Example: "three pre-built vessels at home base, one of which is a deployed rover on the home moon, one of which is in orbit, one of which is on the home planet surface" determines save-format requirements (must serialize 3 vessels in 3 distinct starting states), tutorial flow (first mission can use any of the three), and UI requirements (vessel-list display must handle this mix). These can't move during content authoring without re-doing architectural work.

**Tier B** — content where the design intent is locked but specifics resolve during content authoring. Example: "what the Saturn icy moon contains beyond water" — the structure (multi-stage investigation thread with partial-information catalog states) is locked because it affects research mechanics; whether the specific reveal is a geological anomaly, a biosignature, or a built structure can resolve during content production without code rework.

**Tier C** — content that doesn't affect code architecture and can resolve any time during the v1 production cycle. Example: the specific names of the two starting colleague crew members. Code doesn't care about the names; UI displays whatever names get authored.

The tiering is what makes the document actionable. Tier A items must lock before implementation depends on them. Tier B items get tracked but don't block. Tier C items are explicitly deferred so implementation doesn't waste cycles trying to author them prematurely.

### Why Saturn astronaut rescue as central v1 arc

The design discussion converged on a central narrative arc for v1 to give the player a coherent story spine through what would otherwise be a sandbox-of-discoveries game. Saturn astronaut rescue fits several criteria:

- **Achievable within home system scope** (per commit 021's four intensive-craft bodies, Saturn-equivalent is one of them).
- **Exercises the full systems stack** — mission planning (multi-stage rescue), life support (critical crew recovery per commit 019), discovery progression (transmission → trail → station → astronauts), catalog (pre-populated artifact for the Saturn station), crew tracking (rescued astronauts become veterans).
- **Has lasting consequences** — veteran crew unlock difficult missions later, the icy moon investigation continues post-rescue.
- **Doesn't require Tier C content authoring up front** — the names of the two astronauts can resolve during production, and the dialogue is Tier C.
- **Forward-compatible with v1.1 expansion** — the icy moon investigation can be left open at v1 launch and resolved in v1.1+ content.

The rescue serves as the spine; the Tier A artifacts (Voyager-equivalent, ISS-equivalent, Apollo sites, etc.) serve as side-content that fills out the world without competing for narrative focus.

### Why the two-eras framing

The world has two visible technological eras: a prior wave (2020s-2040s) that built the inherited hardware then ended, and the current era (2050-2100) where the player's program is restarting. This framing was implicit in earlier design discussion (commit 021's "inherited rover," commit 012's Voyager-equivalent / ISS-equivalent / Apollo sites all being from before the current era) but never explicitly locked.

Locking it as Tier A enables:

- **Visual design language consistency** — Tier C visual specifics can be developed knowing which era they belong to.
- **Catalog organization** — entries can be tagged by era, helping the player understand the world's history.
- **Narrative coherence** — the Saturn rescue specifically involves prior-wave astronauts; the "something happened" framing creates dramatic stakes without requiring v1 to resolve what specifically happened to the prior wave (Tier C — deliberately unresolved).

### Why v1 ships without interstellar-cruise mode

The netcode contract §10.1 already specifies that interstellar-cruise mode code stays as stubs through prototype scope. This artifact list reaffirms that decision at v1 launch scope — interstellar-cruise mode is deferred to post-launch (v1.1+). The implication is that v1's "interstellar destination" reaches via long Kepler-rails journey rather than via the interstellar-cruise mode itself; the destination is still gravitationally bound to the home star (Voyager-equivalent in the Oort cloud, or generation-ship-equivalent beyond Pluto orbit). This is reachable analytically via Kepler-rails — the Phase 6 work on interstellar-cruise mode for cross-stellar journeys isn't needed for v1.

The implication for the prototype implementation work in commits 027-036 is that the Phase 0 mode transition test (still unchecked in PHASE_TRACKER.md) only needs to validate PhysX-active ↔ Kepler-rails. The §3.2 (Kepler-rails ↔ interstellar-cruise) and §3.3 (PhysX-active ↔ interstellar-cruise) mode transitions stay deferred to Phase 6.

### Why this is its own commit

The artifact list could in principle have been combined with the operational scaffolding commit 036 (both are documentation commits, both land docs that subsequent work depends on). Two reasons to keep them separate:

1. **The operational scaffolding is generic infrastructure** (PHASE_TRACKER, DECISIONS, ARCHITECTURE, SESSION_PROTOCOL templates) that any project at this scope would need; this commit's artifact list is **project-specific content scoping** that depends on the design content from commits 001-025. The two have different audiences (process tooling vs game-scope decisions) and different update cadences (process tooling rarely; game-scope content authoring will revise this document frequently).

2. **The artifact list is a discrete logical scope** per `commits/README.md`'s "logical scope, not file scope" rule. Bundling it with operational scaffolding would have made commit 036 a mixed-scope commit and made future "show me when v1 scope was first locked" queries point at a commit with unrelated content alongside the answer.

The single new file plus the PHASE_TRACKER.md update is the minimum coherent scope for landing this content.

### Why drop the "this commit" annotation discipline

PHASE_TRACKER.md's checklist items previously included annotations like `(commit 036, this commit)` on the most recently landed work. The annotation was correct at the moment of landing but became drift the moment a subsequent commit ran. Commit 036 itself demonstrated the failure mode: its own update added the "this commit" marker to the operational scaffolding line, but commit 036 didn't run any post-landing cleanup, so commit 037 inherits the stale "this commit" annotation pointing at commit 036.

The simpler discipline: drop the annotation entirely. The Recently landed table at the top of PHASE_TRACKER.md provides recency information; the checklist items name the commit number directly. The annotation was redundant with the table and created per-commit maintenance overhead.

Going forward, checklist items get the commit reference but no "this commit" marker. Commit 038+ doesn't need to remove anything from prior commits' checklist entries — they're already in their final form. This is a one-time cleanup paid in commit 037 with no ongoing maintenance cost.

## Verification

All checks below must pass.

### PHASE_0_ARTIFACT_LIST.md present with expected content

- `docs/PHASE_0_ARTIFACT_LIST.md` exists.
- First line is `# Phase 0 Artifact List for v1`.
- Line count is 258 (per `wc -l` and the file's actual content).
- Byte count is 15,028 (per `wc -c`; matches the `ls -la` size).
- Contains the section heading `## Tier A — must lock for v1`.
- Contains the section heading `## Tier B — lock for v1, specifics flexible`.
- Contains the section heading `## Tier C — defer to v1 content authoring`.
- Contains the section heading `### Story arc: Saturn astronaut rescue`.
- Contains the section heading `### Starting state`.
- Contains the section heading `### Physics scope`.
- Contains the Tier A artifacts table with seven artifact rows (Voyager-equivalent, ISS-equivalent, Apollo-equivalent sites, Mars landers, Inherited rover, Saturn station + 2 astronauts, Distant interstellar-medium destination).
- Contains the phrase `Saturn astronaut rescue` (the central narrative arc).
- Contains the two-eras framing phrases `Prior wave (2020s-2040s tech)` and `Current era (2050-2100 tech)`.
- Contains the starting state spec `3 named starting crew` and `3 pre-built vessels`.
- Contains the physics scope lock `Interstellar-cruise (Phase 6 / v1.1+)` as deferred.
- Contains the research scope reaffirmation `research is entirely discovery-based in v1`.
- Final line is the update history entry for commit 037.

### PHASE_TRACKER.md updates landed correctly

- Recently landed table contains a row for commit 037: `| 037 | Phase 0 artifact list for v1 | 2026-05-17 |`.
- Recently landed table contains a row for commit 036: `| 036 | Operational scaffolding (PHASE_TRACKER, DECISIONS, ARCHITECTURE, SESSION_PROTOCOL, companion-doc template) | 2026-05-17 |`.
- The two new rows appear in 037-then-036 order (newest first), at the top of the table after the header rows.
- Recently landed table still contains the 10 prior rows (026 through 035) below the new entries; total table rows count is 12 commit entries (was 10).
- Phase 0 remaining work checklist contains the line `- [x] Operational scaffolding (commit 036)` (no "this commit" annotation).
- Phase 0 remaining work checklist contains the line `- [x] Phase 0 artifact list (Tier A/B/C content decisions for v1, commit 037)` (checked, with commit reference, no "this commit" annotation).
- Phase 0 remaining work checklist contains exactly three unchecked items remaining:
  - `- [ ] Vessel containers per netcode contract §2 (next substantial implementation)`
  - `- [ ] At least one Kepler-rails mode test (validates the rails side of the mode boundary)`
  - `- [ ] Mode transition test (PhysX-active ↔ Kepler-rails per netcode contract §3.1)`
- The string "this commit" does not appear anywhere in PHASE_TRACKER.md after the edits.
- All other sections of PHASE_TRACKER.md (Current phase, Current milestone, Active blockers, Verification state, Phase progression, Systems by phase, How this document maintains itself) are unchanged.

### Five operational scaffolding docs from commit 036 still present and unchanged

- `docs/PHASE_TRACKER.md` exists (and is the file being modified in this commit; only the changes listed above).
- `docs/DECISIONS.md` exists at unchanged size (15,703 bytes per the host filesystem; sandbox-side `wc -c` may report stale value per rule 6).
- `docs/ARCHITECTURE.md` exists at unchanged size (post-commit-036 size; sandbox-side may report stale).
- `docs/SESSION_PROTOCOL.md` exists at 7,317 bytes (unchanged).
- `docs/code/_TEMPLATE.md` exists at 2,364 bytes (unchanged).

### Workflow rules preserved verbatim in commits/README.md

- `commits/README.md` contains exactly six `^### ` headings under the "Workflow rules learned from experience" section.
- Distinctive phrases for each of the six rules present (per commit 036's verification battery, which spot-checked all six).
- Rule 6's example phrases `_rb.position -= delta`, `5,109 bytes`, and the trigger condition `when a verification check produces an unexpected result or when sandbox view appears stale` all still present.

### CONSTRAINTS.md untouched

- `docs/CONSTRAINTS.md` not modified in this commit; line count and byte count unchanged from post-commit-036 state.
- Specifically: §10 still contains 12 pending open-question bullets (no aero marker; matches post-commit-036 state).

### NETCODE_CONTRACT.md untouched

- `docs/NETCODE_CONTRACT.md` size unchanged from prior commits' state (48,014 bytes per session-start `ls -la`).

## Replay

```
cd C:\Users\gmkar\space_sim

git add docs/PHASE_0_ARTIFACT_LIST.md
git add docs/PHASE_TRACKER.md
git add commits/037_phase_0_artifact_list.md

git commit -m "commit 037: Phase 0 artifact list"
git push
```

Three files staged: the new artifact list, the PHASE_TRACKER update, and this commit artifact. No `git rm` needed for this commit (unlike commit 036's `docs/files.zip` removal). No CONSTRAINTS.md / NETCODE_CONTRACT.md / DECISIONS.md / ARCHITECTURE.md / SESSION_PROTOCOL.md / `commits/README.md` changes — those files are not touched.

## Notes for future commits

- **Tier B items resolve during content authoring.** When a Tier B specific lands (e.g., "what the Saturn icy moon contains beyond water" gets a final decision during content production), the artifact list updates with the resolved specific, the Tier B section narrows accordingly, and a DECISIONS.md entry may be appropriate if the resolution involved rejecting alternatives worth documenting. Tier B items don't always need DECISIONS.md entries — only if alternatives were considered and rejected with reasoning worth preserving.
- **Tier C items don't need commits when they resolve.** Tier C content is content authoring; specific names, dialogue text, and visual specifics get added during normal production work. No commit-per-Tier-C-item ceremony.
- **PHASE_TRACKER.md cleanup discipline going forward.** No "this commit" annotations on checklist items. Commit references in parentheses are sufficient. When a checklist item completes, toggle to `[x]`, add `(commit NNN)` reference, no other annotation.
- **Three Phase 0 items remain.** Commits 038-040 (or thereabouts) will close out vessel containers, Kepler-rails mode, and the mode transition test, completing Phase 0's prototype-implementation half. Phase 1 implementation can then honestly begin.
- **Companion doc creation, when it begins.** The companion-doc template at `docs/code/_TEMPLATE.md` is the starting structure. The first companion doc is likely `docs/code/coordinate_system.md` covering the Coordinates module; it lands when the coordinate system gets a non-trivial extension or refactor, not preemptively.
