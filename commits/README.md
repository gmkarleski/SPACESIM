# Commit Artifacts

This directory contains numbered commit artifacts that document every meaningful change to the project's canonical files. Each artifact is a self-contained record of one logical scope of change: what changed, where, why, and how to verify it landed correctly.

## Why this exists

The project is being built with significant AI assistance across many sessions. Atomic file operations from the AI's sandbox environment can't reliably write to a `.git` directory on the host filesystem, so commits can't be made directly from the AI session. The commit artifacts bridge that gap: the AI writes the artifact and updates the affected files in one logical step; you replay artifacts into git at your convenience.

The artifacts also serve a second purpose. They are durable, human-readable records of every meaningful decision and change in the project's history. A future session — AI or human — can read `commits/` in order and understand how the project arrived at its current state without needing to reconstruct that history from the file contents alone.

## Naming convention

```
NNN_short_kebab_subject.md
```

Where:

- `NNN` is a zero-padded three-digit sequence number. Strictly monotonic. No gaps, no re-use.
- `short_kebab_subject` is a brief description in lowercase with underscores. Should be readable as a commit subject line.
- `000` is reserved for this meta-commit (the format definition itself). `001` onward are project commits.

Examples:

- `001_initial_conversion.md`
- `002_phase0_foundation_resolutions.md`
- `015_coordinate_system_implementation.md`

## Artifact structure

Every artifact follows this template:

```markdown
# NNN: <subject line, imperative form, ~50 chars>

<blank line>

<extended commit message body — what changed and why, in prose. This is what
`git log` would show. Multiple paragraphs are fine. No markdown headings within
this section; it ends at the first `## ` heading below.>

## Scope

- `path/to/file1.ext` — <one-line summary of what changed in this file>
- `path/to/file2.ext` — <one-line summary>
- ...

## Rationale

<Why this change is being made now, in this shape. Cross-references to
constraints document sections, prior commits, or open decisions as needed.
This is where the "why" lives that doesn't fit in the commit message body.>

## Changes

<For small changes: full unified diff in ```diff fenced blocks.
For large changes (e.g., initial file creation, full rewrites): a structural
summary of the change instead, with key sections quoted. The standard is
"enough that a future session can understand the change without reading the
file at the post-change state".>

## Verification

<Specific, mechanical checks a future session can perform to confirm the
change landed correctly. Not "looks right" — concrete tests:

- Specific strings or sections that must be present in named files
- Specific strings or sections that must be absent
- File counts, directory structure assertions
- Cross-file consistency checks where relevant

The verification section is documentation for future-Claude, not just for the
human reviewer. It should be self-contained: no references to "the
conversation that produced this commit" or "what we discussed". The artifact
must stand on its own.>

## Replay

```
git add <files listed in Scope>
git commit -F commits/NNN_subject.md
```

(The `git commit -F` form uses the file as the commit message. Git reads the
subject line and body; the `## Scope` and following sections are ignored by
git but preserved in this file as the durable record.)
```

## Replay procedure

When you have a Windows shell available:

```
cd C:\Users\gmkar\space_sim

# First-time setup only:
git init -b main
git config user.email "gmkarleski@gmail.com"
git config user.name "Gray"

# For each unreplayed commit, in numbered order:
git add <files listed in commit's Scope section>
git commit -F commits/NNN_subject.md
```

You can replay artifacts mechanically (trusting the artifact's Scope list) or read each one and stage manually. The artifact is a proposal; git is the executor.

## When to write an artifact

Every meaningful change to a canonical project file gets an artifact. "Canonical" means:

- Anything under `docs/` (CONSTRAINTS, ARCHITECTURE, PHASE_TRACKER, DECISIONS, companion docs, etc.)
- Anything under `src/` once code exists
- Anything under `tests/` once tests exist
- This directory's `README.md` (changes to the commit format itself)
- The repo root `.gitignore` and similar repo-level files

What does *not* require an artifact:

- Scratch work in the AI session that doesn't get written to a canonical file
- Failed attempts that get reverted within the same session before any file write
- Files added to `commits/` itself (each artifact IS its own record)

## Logical scope, not file scope

A single artifact may touch multiple files when they change together as one logical unit (e.g., a system implementation plus its companion doc, or a constraints update plus the decisions log entry that records it). The rule is one *logical scope* per artifact, not one file per artifact.

What constitutes a single logical scope:

- A single decision and the files that record/implement it
- A single feature increment and the files that compose it
- A single refactor and the files it touches

What is two logical scopes (and therefore two artifacts):

- "Convert constraints to markdown" and "apply foundation resolutions to constraints" — these are sequential decisions, not one change
- "Add coordinate system" and "fix a typo in constraints" — unrelated work, even if done in the same session

## Status tracking

Artifacts have no explicit status field — the existence of the artifact means it has been written by the AI session and is ready for replay. Once replayed to git, the commit hash becomes the canonical record of the change; the artifact remains as the human-readable companion.

If an artifact is superseded before replay (e.g., we realize a foundation decision needs revision after writing it but before the human replays it), the convention is to write a new artifact that revises the prior one, not to edit the existing artifact. Artifacts are append-only once written, the same way git commits are.

## Cleanup

The `commits/` directory is intended to live in the repository indefinitely. It is a permanent record. If at some point it becomes large enough to be inconvenient, the convention will be to archive older artifacts under `commits/archive/NNN-MMM/` rather than delete them.

## Workflow rules learned from experience

The following rules were added after specific failures during early Phase 0 work. Each rule names the failure mode it prevents.

### Verification checks must include preserved-content anchors

Every artifact's Verification section must include grep-able checks not only for *added* content but for *previously existing* content in sections this commit did not intend to touch. The check format is "section N still contains [specific distinctive phrase]" for each section the commit claims not to have modified.

This is the discipline that catches the failure mode where a file mutation succeeds in writing the new content but silently truncates or corrupts unrelated content. Without preserved-content anchors, a commit can verify its own additions and still leave the canonical doc broken downstream.

Pick phrases that are distinctive (a memorable noun phrase or specific quoted text), not generic words that might appear elsewhere. Three to five anchors per untouched section is typical. For commits touching only one or two sections, anchor every other major section.

### Choose Write over Edit for changes spanning multiple subsections

Commits that touch more than two subsections, or add more than ~20 lines of content, use Write (full-file replacement) rather than multiple Edit operations. Single-paragraph fixes, small targeted edits, and renames can still use Edit.

The threshold is conservative on purpose. Multi-Edit sequences against the same file have failed in observed ways on this project's filesystem mount; full-file Write is more reliable for foundation work. Reliability beats elegance when the canonical doc is at stake.

### Tool-layer write timeout produces an effective ~42KB cap; use bash for larger writes

The Write and Edit tools route through a layer that has a timeout on the post-write verification step. Writes that take longer than the timeout — empirically ~42KB on this workspace — report success at the API level but do not commit through to storage, or commit only the bytes that landed before the timeout fired. The failure is silent: tool reports success, file is truncated or unchanged.

The diagnostic test that distinguishes timeout-shaped failure from a true storage cap: try a small `cat <<EOF`-style write of >50KB through bash to the same path. If bash succeeds where Write fails, the constraint is in the tool layer, not the filesystem.

Workaround when the tool layer fails: use bash (typically Python invoked from bash) to do the mutation directly. Bash writes go through fine even for files much larger than 42KB. Pattern that works:

```
python3 << 'PYEOF_INNER'
import os
with open(path, 'r', encoding='utf-8') as f:
    existing = f.read()
new_content = construct_new_content(existing)
tmp = path + '.recovery'
with open(tmp, 'w', encoding='utf-8') as f:
    f.write(new_content)
os.replace(tmp, path)
PYEOF_INNER
```

The write-then-rename pattern preserves atomicity. The bash route is the escape hatch when the tool route fails; it is not the preferred default (the tool route's error reporting and integration with the editor session is generally better), but it is reliable when the tool route times out.

Operational rule: before any large Write or Edit against a canonical doc, estimate the post-mutation byte size. If the file will exceed ~35KB after the operation, route through bash from the start rather than waiting for the tool route to fail.

### Edit may silently fail to mutate even small files

A documented failure mode beyond the timeout: the Edit tool has been observed to report success on a small file (~5KB target, small replacement string) without actually writing through. The cause is unclear; possibly a cached read-view that diverges from the file on disk, possibly a different timeout failure shape. When this happens, repeating the Edit does not help — same silent failure recurs.

Workaround: after any Edit, run a quick verification check (line count or distinctive-phrase grep) confirming the change landed. If the Edit silently failed, fall through to the bash escape hatch above. Don't rely on the tool's success return alone.

### Markdown-shaped content can survive rstrip-and-append in misleading shapes

When appending to or replacing a subsection whose existing content contains markdown that looks like other structural markdown — backtick-wrapped inline heading references like `` `### Channel 16 broadcasts` ``, fenced-code-block markers, inline bold-as-heading-substitutes — the naive `text.rstrip() + new_content` pattern can split or re-space the existing content in ways that turn previously-inline markdown into what looks like a top-level structural element. Commit 014 produced a concrete instance: the existing Transmissions subsection ended with `(see \`### Channel 16 broadcasts\` below).`. The append operation truncated the paragraph at the opening backtick, leaving a dangling `(see \`` at the original location and an orphan `### Channel 16 broadcasts\` below).` line beneath the appended content — which, because it began with `###`, parsed as a malformed h3 heading. Section 3's h3 count came out one higher than expected.

The failure is invisible to substring-presence verification. Every distinctive phrase the commit checks for is still present somewhere in the file — only the *structural role* of that text has changed. The defense is two-part:

1. **Use line-anchored regex for heading detection, not substring presence.** Verification must count headings via `^### ` at start of line (and similarly for `^## `, `^#### `). Substring-presence checks like `'### Foo' in text` will not catch a heading that became malformed by acquiring trailing text, because the substring is still there. Every commit's Verification section must include a structural-count check per major section touched: "section N has exactly K `### ` subsections (was K-prior before; +delta from this commit)." A mismatch is the only reliable signal that markdown-shape damage occurred.

2. **When appending to or replacing locked content, capture the exact final paragraph (for appends) or full subsection text (for replacements) pre-write, and verify it survives verbatim post-write.** Anchor the verification to a distinctive phrase *plus its surrounding context*, not just the distinctive phrase alone. For commit 014, the right check was not "does the file still contain the phrase `Channel 16 broadcasts`" (it did, multiple times) but "does the file still contain the verbatim string `` (see `### Channel 16 broadcasts` below). `` exactly once" — anchored on context that would be destroyed by the truncation.

The remediation when this failure is caught: diagnose precisely where the existing markdown was split, restore the truncated paragraph verbatim, remove the orphan stub, and re-run the full verification battery. The repair is mechanical once the diagnosis is in hand; the discipline that catches it is structural-count plus context-anchored verification.

Operational rule: every commit's Verification section includes (a) a line-anchored `^### ` count check for each section the commit modifies, and (b) for any commit that appends to or replaces locked content, at least one verbatim-with-context anchor on the boundary where the new content meets the preserved content.

### Sandbox mount can diverge from host filesystem for Unity-touched and Edit-touched files

The Cowork sandbox mounts the host filesystem but caches aggressively. Files that have been written by tools running in the same session — Unity saving a scene, the Edit tool modifying source mid-session, scripts run via bash that write through to the same path — can show different content depending on which reader queries them. The Read tool sometimes routes through one code path while bash byte-level reads (`wc -c`, `cat`, `xxd`) route through a different one, and the two views can disagree about what the file currently contains. The host filesystem — what `git add` and `git commit` operate against — is the canonical state; sandbox views are derived and can lag.

Three concrete data points produced this rule. Commit 028 ran into the failure first: after the user re-saved `TestFoundation.unity` on the host with a fully-attached `PrototypeStartupTest` component, the sandbox-mount view continued to show the prior 3,507-byte truncated content for the rest of the session. Commit 031 reproduced the pattern under a different reader: the user's `findstr` on the Windows host counted three `PrototypeStartupTest` references in the scene file, while Cowork's sandbox view of the same file counted zero matches and a mount-refresh probe failed to flush the cache. Commit 033 produced the cleanest worked example: an Edit-tool change to `FloatingOriginAnchor.cs` was confirmed by the Read tool to contain the fully-corrected post-edit content, but a bash `wc -c` on the same path during the same session returned 5,109 bytes — an intermediate state that included the updated XML doc's `SHIFT DISPATCH FROM SIM-TICK BOUNDARY` header but was missing the `_rb.position -= delta` line that was the architectural point of the edit. Same file, two views in the same session, diverging on the exact line that proved whether the change had taken effect.

The defense is not to distrust either reader unconditionally, but to recognize the divergence and route to the canonical reader when it matters. Verification checks that count substrings, sum line counts, or assert byte sizes against an Edit-touched file can return values for the sandbox's stale view rather than for the bytes that git will see on commit. When a verification check produces an unexpected result on a file that has been Unity-saved or Edit-touched during the same session, byte-level cross-reading (`wc -c`, `stat`, and `xxd | tail` spot-checks) is the diagnostic that distinguishes "the edit didn't land" from "the sandbox is showing me yesterday's content." Where the divergence matters most is at the commit boundary: `git add` reads host bytes regardless of which sandbox view Cowork is showing, so a commit whose Cowork-side verification claimed success can be safely replayed against the host filesystem and the host bytes are what land in git.

Operational rule: when sandbox and host views of a file diverge, treat the host filesystem as canonical. For files Unity has written or that Cowork's Edit tool has modified mid-session, verify with byte-level reads (`wc -c`, `stat`, `xxd | tail`) when a verification check produces an unexpected result or when sandbox view appears stale, and defer git operations to host-side replay where the host bytes are what git sees.
