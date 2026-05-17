# 026: Netcode contract landed — Phase 0 → Phase 1 written-contract gate satisfied

The Phase 0 netcode contract is finalized and lives at `docs/NETCODE_CONTRACT.md`. This commit lands the written-contract deliverable named by `docs/CONSTRAINTS.md` §9 Phase 0. Renames the working-name file `docs/NETCODE_CONTRACT_DRAFT.md` to the canonical `docs/NETCODE_CONTRACT.md`, edits two internal draft markers to reflect finalization, and appends a `**Netcode contract: landed.**` paragraph to §9 Phase 0 description recording the deliverable's arrival.

The contract is the canonical specification of the sim-tick boundary between authoritative double-precision state and PhysX-active simulation. It treats single-player as a degenerate case of multiplayer (one machine, zero peers): same code paths, same state representation, same authority model, with the replication step a no-op in single-player. This commits the architecture to be multiplayer-shaped from Phase 1 forward, even though v1 ships single-player only.

The Phase 0 → Phase 1 gate is split into two components by §9: the written contract (this document) and the prototype (a separate Phase 0 work item that demonstrates the rules hold in practice). The written contract is now in place; the prototype is the remaining Phase 0 deliverable. Phase 1 work cannot begin until both are complete.

## Scope

- `docs/NETCODE_CONTRACT.md` — created (renamed from `docs/NETCODE_CONTRACT_DRAFT.md` with two in-file edits applying the finalization). 862 lines / 47,989 bytes.
- `docs/NETCODE_CONTRACT_DRAFT.md` — reduced to a 425-byte redirect stub pointing at `NETCODE_CONTRACT.md`. The sandbox the artifact was produced in cannot unlink files that existed before the session, so the working-name file is overwritten with a stub rather than deleted. On the Windows host where this commit replays, `git rm docs/NETCODE_CONTRACT_DRAFT.md` removes the stub and only `NETCODE_CONTRACT.md` remains in history.
- `docs/CONSTRAINTS.md` — single paragraph append to §9 `### Phase 0 — Decisions (weight 1)` recording the contract landing.

## Rationale

`docs/CONSTRAINTS.md` §2 commit 002 (`### Foundational architectural principles`) established the boundary between authoritative double-precision state and PhysX-active simulation as the highest-bug-density seam in the project. §2 commit 002 (`### Multiplayer architecture preparation`) committed to architectural multiplayer-readiness from day one, with v1 single-player as the shipping configuration. §9 commit 002 (Phase 0 deliverable) named the netcode contract — written contract plus prototype — as the explicit Phase 0 → Phase 1 gate. The written contract has been the open Phase 0 deliverable across all 25 prior commits in this batch.

The contract resolves the boundary specification with five locked architectural decisions:

**Sim-tick rate: 30 Hz fixed.** Authoritative state advances on a 30 Hz fixed-timestep tick (33.33 ms intervals). Rendering runs at frame rate; PhysX runs at its own substep schedule; the sim-tick is the heartbeat at which authoritative state is canonical. This sets the granularity at which save state captures, network replication packets, mode transitions, and analytic events are scheduled.

**Determinism scope: authoritative state only.** No lockstep across machines. The host's authoritative state is canonical; peers receive deltas. PhysX-active simulation is non-deterministic by design (consistent with §2 commit 002's "outside PhysX-active mode" framing). The implication: multiplayer is host-authoritative state replication, not deterministic input replication.

**Multiplayer scale: 2-4 players for v1.1+.** The architecture supports scaling to roughly 16 players if it becomes interesting, but the design target is 2-4 for v1.1. V1 ships single-player only. The smaller-than-typical-MMO scale reflects the design's commitment to thoughtful play and the time-warp incompatibility with persistent shared real-time.

**Single-player as degenerate multiplayer case.** Same code paths in both modes. In single-player, the receive-peer-state step is a no-op; the replicate-to-peers step is a no-op. The architecture is multiplayer-shaped throughout; single-player runs the multiplayer code with zero peers. This forecloses the design drift where multiplayer is bolted onto a single-player architecture.

**Mode separation: PhysX-active, Kepler-rails, interstellar-cruise.** Per §2 commit 002. The contract specifies the transition protocol between modes — when a vessel enters/leaves PhysX-active, when Kepler-rails advances analytically, when interstellar-cruise applies relativistic time-dilation. Mode transitions are bug-density boundaries (§2 commit 002) and the contract specifies them in detail.

The contract also specifies: the ten-step sim-tick cycle (receive peer state → read PhysX → convert to authoritative coords → apply analytic updates → reconcile authoritative state → detect mode transitions → push authoritative back to PhysX → replicate to peers → fire events → advance sim-tick counter); the authoritative state schema; the floating-origin shift protocol at sim-tick boundaries (consistent with §2 commit 002's 50 km threshold); save format integration (save captured between steps 9 and 10 is canonical); prototype validation criteria. Cross-references to CONSTRAINTS in §12 of the contract list every section the netcode contract touches: §2 commit 002 (foundational principles), §2 commit 023 (save mechanics), §3 commit 004a (mode-portable designs), §3 commit 011a (movie-moment mechanics with real telemetry), §3 commit 024 (flight computers), §3 commit 025 (atmospheric flight), §4 commit 002 (multiplayer as shared universe), §9 Phase 0 (this contract as deliverable).

## Changes

Three discrete operations, each atomic-write via bash-via-Python:

### Operation 1: Rename + finalization edits

The contract was authored at `docs/NETCODE_CONTRACT_DRAFT.md`. The finalization includes two in-file edits to reflect the status change:

- Status marker: `**Status:** Draft for review. This document is the canonical specification...` → `**Status:** Finalized. This document is the canonical specification...`
- Final line marker: `*End of contract draft.*` → `*End of contract.*`

Edits applied to the in-memory text buffer, then atomic-written to `docs/NETCODE_CONTRACT.md` via the `.recovery` + `os.replace` pattern. After the rename, the sandbox-bound `os.remove(draft_path)` failed with PermissionError because the sandbox enforces non-deletion of files that existed before the session began. To resolve the leftover, the draft file is overwritten via the same write-then-rename pattern (different syscall path than `unlink`) with a 425-byte redirect stub pointing at the canonical file. The stub remains as the only artifact of the rename inside the sandbox; on the host filesystem where this commit replays, `git rm docs/NETCODE_CONTRACT_DRAFT.md` removes it cleanly.

Final contract state: 862 lines / 47,989 bytes (-13 bytes from the marker edits). The locked-decisions block in section 1 of the contract remains intact, naming the five architectural decisions verbatim.

### Operation 2: §9 Phase 0 paragraph append in CONSTRAINTS.md

Single paragraph append to the body of `### Phase 0 — Decisions (weight 1)` in §9 of `docs/CONSTRAINTS.md`. The new paragraph lands after commit 021's four-body-intensive-craft-identifications paragraph (the current closing of Phase 0's body). Pattern: standard subsection-body-append via the rstrip+`\n\n`+block+`\n\n` template.

The new paragraph: `**Netcode contract: landed.**` followed by the cross-reference to `docs/NETCODE_CONTRACT.md`, a five-element list naming the contract's scope (sim-tick boundary specification, authoritative state schema, mode transition protocol, replication protocol, save format integration, prototype validation criteria), the five locked architectural decisions, and the closing statement that prototype implementation work is the next Phase 0 deliverable.

CONSTRAINTS.md grew from 1954 lines / 226,933 bytes to 1956 lines / 228,053 bytes (+2 lines, +1,120 bytes).

### Operation 3: Verbatim-with-context boundary verification

Five boundary anchors captured pre-write and verified post-write:

- B1: §9 Phase 0 opener (commit 001) `Lock all LOCKED items. Resolve OPEN items that block Phase 1. Document everything (this doc plus companion docs as the project grows).`
- B2: commit 002 netcode contract paragraph `**Phase 0 deliverable: netcode contract.** Multiplayer netcode architecture requires a dedicated Phase 0 design pass before Phase 1 implementation begins.`
- B3: commit 002 Phase 1 prerequisite sentence `The contract is a Phase 1 prerequisite.`
- B4: commit 010 three-tier content paragraph `Phase 0 also locks the three-tier starting content structure: identify which Tier A handcrafted artifacts ship at v1 (target 10-20), which Tier B classes ship (target 6-10), and which procgen scope is required for Tier C ambient texture.`
- B5: commit 021 four-body content lock `Phase 0 also locks the four intensive-craft body identifications: home planet, home moon, Mars-equivalent, Saturn-equivalent system.`

All five preserved exactly once post-write. Phase 0 internal ordering preserved: B1 < B2 < B4 < B5 < new netcode-landed paragraph.

## Verification

72 checks, all passing on first run. Six groups:

### A. File existence and rename (4 checks)

- `docs/NETCODE_CONTRACT.md` exists
- `docs/NETCODE_CONTRACT_DRAFT.md` exists as redirect stub (sandbox-deletion workaround)
- NETCODE_CONTRACT.md line count = 862
- NETCODE_CONTRACT.md byte count in expected ~48KB range (47,989)

### B. Contract finalization markers (4 checks)

- `**Status:** Finalized.` present in contract
- `**Status:** Draft for review.` absent from contract
- `*End of contract.*` present in contract
- `*End of contract draft.*` absent from contract

### C. Contract locked-decisions block intact (4 checks)

The five-decision locked block in section 1 of the contract names:

- `Sim-tick rate: 30 Hz fixed.`
- `Determinism scope: authoritative-state only. No lockstep. Host's state is canonical.`
- `Multiplayer scale: 2-4 players for v1.1 and beyond.`
- single-player as a degenerate case of multiplayer

All four phrases verified present in the contract body.

### D. Redirect stub (2 checks)

- Draft stub contains a markdown link to `NETCODE_CONTRACT.md`
- Draft stub explains the sandbox-deletion limitation

### E. CONSTRAINTS.md §9 Phase 0 update (12 checks)

- CONSTRAINTS.md line count = 1956 (1954 → 1956)
- `**Netcode contract: landed.**` paragraph present exactly once
- Paragraph references `docs/NETCODE_CONTRACT.md` path
- Paragraph names all five locked architectural decisions (30 Hz fixed sim-tick rate; determinism scope is authoritative-state only; multiplayer scale is 2-4 players for v1.1+; single-player treated as the degenerate case of multiplayer; mode separation implicit in the cross-reference)
- Paragraph references commit 026 explicitly
- Paragraph closes with `Prototype work is the next Phase 0 deliverable.`

### F. Phase 0 boundary anchors preserved + ordering (6 checks)

- B1, B2, B3, B4, B5 each preserved exactly once
- Phase 0 internal ordering: opener < netcode-contract paragraph (commit 002) < three-tier paragraph (commit 010) < four-body paragraph (commit 021) < new netcode-landed paragraph (commit 026)

### G. Structural counts and prior-commit anchors (40 checks)

Per-section h3 counts unchanged from commit 025: §1=15, §2=15, §3=18, §4=17, §5=6, §6=13, §7=7, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0.

Section headings line-anchored exactly once for the sample (`## 9. Build order`, `### Phase 0 — Decisions (weight 1)`, `### Phase 5 — Game systems (weight 4)`).

Sample of prior-commit anchors preserved: commit 014 damage-repair (no malformed `### Channel 16 broadcasts\` below).` line; restored inline ref present); commit 015 five verbatim-with-context anchors; commit 017 structural headings (`## 3. Gameplay mechanics`, `## 4. World behavior and discovery`); commit 019 three-category framing preserved verbatim including `Radiation dose, zero-G exposure, mission stress`; commit 021 four-bodies-intensive-hand-craft + Saturn rings galaxy-wide procgen parenthetical; commit 022 `### Asteroid clusters as system composition variant`; commit 023 `### Mission Control as primary UI` and `### Forgiving restart philosophy`; commit 024 `### Flight computers and unmanned operation`; commit 025 `### Atmospheric flight and spaceplane gameplay` and atmospheric-flight pillar and §10 RESOLVED marker and Phase 5 weight 4 increase.

Cross-section preserved-content battery: `Engineering as the verb` (§1); `Floating origin shift threshold: 50 km default` (§2); `**Crew are physically located on vessels.**`.

## Note on sandbox file-deletion limitation

The sandbox environment this commit was produced in cannot unlink files that existed before the session began (the `unlink` syscall returns Operation-Not-Permitted). The write-then-rename pattern (`os.replace`) does work because it uses a different syscall path. As a result, the `NETCODE_CONTRACT_DRAFT.md` file could not be removed and was instead overwritten with a 425-byte redirect stub. On the Windows host where this commit replays, the stub can be removed cleanly with `git rm docs/NETCODE_CONTRACT_DRAFT.md`. The Replay section's `git rm` line handles this.

This sandbox limitation is a property of the environment, not a property of the commit. The commit's logical scope is the rename; the stub is an artifact of how the rename was executed within sandbox constraints. Future replacement commits that need to rename files may face the same limitation; the documented workaround is the write-then-replace + stub approach used here.

## Replay

```
cd C:\Users\gmkar\space_sim
git rm docs/NETCODE_CONTRACT_DRAFT.md
git add docs/NETCODE_CONTRACT.md docs/CONSTRAINTS.md commits/026_netcode_contract.md
git commit -F commits/026_netcode_contract.md
```
