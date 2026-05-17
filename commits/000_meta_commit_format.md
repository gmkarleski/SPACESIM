# 000: Establish commit artifact workflow

Establish the `commits/` directory and the artifact-based commit workflow that this project will use for the foreseeable future. Every meaningful change to canonical project files (anything under `docs/`, `src/`, `tests/`, and repo-level files like `.gitignore`) produces a numbered, self-contained commit artifact documenting what changed, where, why, and how to verify it landed correctly.

The artifact format exists because the project's AI-assisted development sessions operate in a sandbox that can write plain files to the host filesystem but cannot reliably run git operations against the host's `.git` directory. The commit artifacts bridge that gap: the AI writes the artifact and the file changes in one logical step, and the human replays artifacts into git from a Windows shell at their convenience. The artifacts also serve as a durable human-readable history that survives independently of git, which is valuable for a long-lived project developed across many AI sessions.

This is the first artifact in the series and documents the format itself. All subsequent artifacts (`001` onward) follow the structure defined here.

## Scope

- `commits/README.md` — created. Full specification of the commit artifact format, naming convention, replay procedure, scope rules, and lifecycle. Authoritative reference for what an artifact must contain.
- `commits/000_meta_commit_format.md` — created (this file). First artifact in the series; records the decision to use this workflow and serves as a worked example of the format.

## Rationale

Three workflow alternatives were considered: (a) human runs git init and commit commands directly, with the AI doing all file writes and the human committing after each logical step; (b) skip git entirely for the Phase 0 session and capture everything in one initial commit later; (c) AI writes numbered commit artifacts that the human replays into git at their convenience, with each artifact being a self-contained durable record. Option (c) was chosen because it preserves the "isolated commits with clear scope" discipline named at the start of Phase 0, produces an artifact trail that future AI sessions can read to understand project history, and decouples the AI's file-writing work from the timing of the human's git operations.

The decision to make artifacts self-contained — verification sections written for future-Claude rather than relying on conversation context — comes from the same principle that makes companion docs valuable in this project: the artifact must stand on its own, readable by any session that has access to the repo and the constraints document, without needing to reconstruct the conversation that produced it.

## Changes

Two new files created. Both are net-new content; no diff against a prior state.

`commits/README.md` defines:

- The naming convention (`NNN_short_kebab_subject.md`, zero-padded three digits, strictly monotonic).
- The artifact template (subject line, body, Scope, Rationale, Changes, Verification, Replay sections).
- The replay procedure (`git commit -F commits/NNN_subject.md`).
- The rule for when an artifact is required (any meaningful change to a canonical project file).
- The "logical scope, not file scope" rule (one artifact per logical change, which may span multiple files).
- The append-only convention (superseded artifacts get a new artifact that revises them; existing artifacts are not edited after writing).
- The lifecycle (artifacts live in the repo indefinitely; archive under `commits/archive/` if volume becomes a problem).

`commits/000_meta_commit_format.md` (this file) records the workflow decision itself and serves as a worked example of the format that future artifacts will follow.

## Verification

A future session can confirm this commit landed correctly by checking:

1. **File `commits/README.md` exists** at the repository root's `commits/` subdirectory.
2. **File `commits/000_meta_commit_format.md` exists** in the same directory.
3. **`commits/README.md` contains** the section headings: `## Why this exists`, `## Naming convention`, `## Artifact structure`, `## Replay procedure`, `## When to write an artifact`, `## Logical scope, not file scope`, `## Status tracking`, `## Cleanup`.
4. **`commits/README.md` specifies the naming convention** `NNN_short_kebab_subject.md` with zero-padded three-digit sequencing.
5. **`commits/README.md` specifies the artifact template sections** in this order: subject line, body, Scope, Rationale, Changes, Verification, Replay.
6. **No other files exist** under `commits/` at the time of this artifact (only `README.md` and `000_meta_commit_format.md`).
7. **The repository's `docs/` directory** exists but is empty at this point (no `CONSTRAINTS.md` yet — that lands in commit `001`).
8. **No `.git` directory exists** at the repository root at the time this artifact was written. The repository has not yet been initialized; this artifact and all subsequent ones up to the point of first replay are pre-git. Once the human replays artifact `000` (initializing git as part of the replay), this verification check inverts: `.git` should exist for all post-replay verifications.

If any of checks 1–7 fail, the artifact has not been correctly applied and should be regenerated or repaired before subsequent artifacts are processed.

## Replay

This is the first artifact and includes the git initialization that all subsequent artifacts assume has been done.

```
cd C:\Users\gmkar\space_sim
git init -b main
git config user.email "gmkarleski@gmail.com"
git config user.name "Gray"
git add commits/README.md commits/000_meta_commit_format.md
git commit -F commits/000_meta_commit_format.md
```

After this replay, the repository exists, has `main` as the default branch, has a single commit containing both files in this artifact's Scope, and is ready for artifact `001` to be replayed against it.
