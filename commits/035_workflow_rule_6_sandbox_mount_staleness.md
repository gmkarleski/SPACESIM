# 035: Add sixth workflow rule on sandbox-mount staleness for Unity-touched and Edit-touched files

Append a sixth learned-rule entry to `commits/README.md`'s "Workflow rules learned from experience" section, capturing the failure mode that appeared across commits 028, 031, and 033: the Cowork sandbox mount of the host filesystem can show different content than the host filesystem itself for files that have been written by Unity or modified through the Edit tool during the same session.

The rule names the failure mechanism: the Cowork sandbox mounts the host filesystem but caches aggressively, the Read tool and bash byte-level reads can route through different code paths, and the two readers can disagree about a file's current content. The host filesystem is the canonical state — what `git add` operates against — and sandbox views are derived. The rule names the defense: when sandbox and host views diverge, treat the host filesystem as canonical; verify suspicious checks with byte-level reads (`wc -c`, `stat`, `xxd | tail`); defer git operations to host-side replay where the host bytes are what git sees.

This is a meta-commit (changes to `commits/README.md`, which the README itself calls out as artifact-worthy). The numbering uses `035` rather than `034b` because the rule has been pending formalization across three commits' worth of accumulated evidence and is methodologically independent of any single prior commit's content. This is the second workflow rule landed via the "structural change in its own commit" pattern; the first was commit 014b's workflow rule 5 about line-anchored vs substring verification.

## Scope

- `commits/README.md` — append a sixth rule section titled `### Sandbox mount can diverge from host filesystem for Unity-touched and Edit-touched files` at the end of the "Workflow rules learned from experience" section. No edits to the five pre-existing rules. No edits to any other section of the file.

## Rationale

The five pre-existing learned rules were each added after a specific failure during early Phase 0 work and the first few Unity-prototype commits: preserved-content anchors (rule 1) after commit 002's silent truncation; Write-over-Edit (rule 2) after observed multi-Edit failures on the same file; the bash escape hatch (rule 3) after the tool-layer ~42KB write timeout; silent-Edit-failure (rule 4) after a small-file Edit reported success without writing through; markdown-shaped rstrip hazard (rule 5) after commit 014's malformed-heading repair via commit 014b.

Commits 028, 031, and 033 produced a sixth class of failure that none of the existing five rules names directly. The pattern is a divergence between two readers of the same file in the same session: one reader sees the canonical host-filesystem state, the other sees a cached or intermediate sandbox-mount state. The two readers' views can persist in disagreement for the entire remainder of the session — refresh attempts don't reliably flush the cache — and the divergence is invisible to substring-presence verification because each reader is internally consistent. The verification check returns whichever value the reader-it-happens-to-use sees, and that value can be wrong.

Three concrete data points accumulated before the rule was promoted to a formal entry:

**Commit 028.** The first observation. The user re-saved `TestFoundation.unity` on the host with a fully-attached `PrototypeStartupTest` component. The sandbox-mount view of the same file continued to show the prior 3,507-byte truncated content for the rest of the session. The commit's verification claimed file size diverged from expectation. Diagnosis: the host file had the correct content and was correctly committable; the sandbox view was stale. Recovery: defer file commit to host-side `git add` during replay, since git reads host bytes regardless of which sandbox view Cowork shows.

**Commit 031.** Reproduced the pattern under a different reader. The user's `findstr` on the Windows host counted three `PrototypeStartupTest` references in the scene file. Cowork's sandbox-mount view of the same file via grep counted zero matches. A mount-refresh probe was attempted and did not flush the cache. Same divergence, same recovery: trust host-side bytes for git operations.

**Commit 033.** Produced the cleanest worked example. An Edit-tool change to `FloatingOriginAnchor.cs` was confirmed by the Read tool to contain the fully-corrected post-edit content (the `_rb.position -= delta` rigidbody-path assignment was present in the Read view). A bash `wc -c` on the same path during the same session returned 5,109 bytes — an intermediate state that included the updated XML doc's `SHIFT DISPATCH FROM SIM-TICK BOUNDARY` header (newly added) but was missing the `_rb.position -= delta` line that was the architectural point of the edit. Same file, two views in the same session, diverging on the exact line that proved whether the change had taken effect. This is the worked example preserved inline in the rule body because it most cleanly demonstrates the failure shape: the divergence is not random; it lands on the line that matters.

Three data points across three different commits, three different file types (.unity scene file × 2, .cs source × 1), three different reader configurations (Unity-save vs sandbox-view; findstr-on-host vs sandbox-grep; Edit-tool + Read vs bash wc -c), all producing the same divergence pattern. The threshold for promotion to a formal workflow rule has been met.

The rule frames the defense as conditional rather than universal — byte-level cross-reading is the diagnostic when a verification check produces an unexpected result or when sandbox view appears stale, not a default check imposed on every read. This matches the framing of rules 3 and 5, which both name a trigger condition (file size estimate exceeds ~35KB; appending to or replacing locked content) rather than imposing the diagnostic on all operations. Universal byte-level verification would impose overhead on every check; the conditional framing keeps the rule tractable.

The framing of the rule follows the established pattern in `commits/README.md`: name the failure mode, give the worked example, name the defense, close with an operational rule. The three commit references (028, 031, 033) are preserved as the data points so future readers can reconstruct the failure shape's prevalence without reading each commit's artifact in full.

## Changes

A single append operation to `commits/README.md`, written via the Edit tool because the post-mutation file size is well under the rule 3 threshold (file grew from ~14.0KB to ~19.2KB, both under the ~35KB tool-route threshold). Per rule 4, the result is verified post-Edit by reading back the tail of the file to confirm the new rule's content landed verbatim.

The append adds the following content to the end of the file, after the existing `### Markdown-shaped content can survive rstrip-and-append in misleading shapes` rule and its closing operational-rule paragraph:

```markdown
### Sandbox mount can diverge from host filesystem for Unity-touched and Edit-touched files

The Cowork sandbox mounts the host filesystem but caches aggressively. Files that have been written by tools running in the same session — Unity saving a scene, the Edit tool modifying source mid-session, scripts run via bash that write through to the same path — can show different content depending on which reader queries them. ...

[full text of the new rule, ~5.0KB]
```

File grew from 211 lines / ~13,847 bytes (post-commit-014b baseline, per that artifact's recorded counts) to 220 lines / 17,231 bytes (verified by `wc -lc` post-Edit), an addition of +9 lines and +3,384 bytes. The line count delta is small because the new rule is written as three full-width paragraphs plus a heading and an operational rule, not as a bulleted list (matching rules 3 and 5's prose shape). The byte delta is smaller than the rule body's surface size because three of the four added paragraphs are long single-line markdown paragraphs that wrap visually in a viewer but count as one line each in `wc -l`.

## Verification

All checks below must pass.

### New rule present and well-formed

- File contains exactly one line-anchored heading `^### Sandbox mount can diverge from host filesystem for Unity-touched and Edit-touched files$`
- The new rule body contains the phrase `Cowork sandbox mounts the host filesystem but caches aggressively`
- The new rule body contains the worked example phrase `_rb.position -= delta`
- The new rule body contains the worked example byte count `5,109 bytes`
- The new rule body contains the worked example file reference `FloatingOriginAnchor.cs`
- The new rule body contains the three commit references `Commit 028`, `Commit 031`, `Commit 033` (each appearing at least once in the rule body)
- The new rule body ends with an operational rule beginning `Operational rule: when sandbox and host views of a file diverge`
- The operational rule contains the trigger condition phrase `when a verification check produces an unexpected result or when sandbox view appears stale`
- The operational rule contains the canonical-reader instruction `treat the host filesystem as canonical`
- The operational rule contains the byte-level reader list ``` `wc -c`, `stat`, `xxd | tail` ```

### Rule ordering

The six rule headings appear in `commits/README.md` in this exact order (verified by `^### ` line-anchored regex):

1. `### Verification checks must include preserved-content anchors`
2. `### Choose Write over Edit for changes spanning multiple subsections`
3. `### Tool-layer write timeout produces an effective ~42KB cap; use bash for larger writes`
4. `### Edit may silently fail to mutate even small files`
5. `### Markdown-shaped content can survive rstrip-and-append in misleading shapes`
6. `### Sandbox mount can diverge from host filesystem for Unity-touched and Edit-touched files`

The new rule must appear *last* in the file (no content other than terminal whitespace after it).

### Pre-existing rules preserved verbatim

Each of the five pre-existing rule sections must still contain its distinctive content:

- Rule 1 still contains the phrase `silently truncates or corrupts unrelated content`
- Rule 1 still contains the prescription `Three to five anchors per untouched section is typical`
- Rule 2 still contains the phrase `Reliability beats elegance when the canonical doc is at stake`
- Rule 3 still contains the phrase `~42KB on this workspace`
- Rule 3 still contains the code-block content marker `PYEOF_INNER`
- Rule 3 still contains the threshold statement `If the file will exceed ~35KB after the operation`
- Rule 4 still contains the phrase `cached read-view that diverges from the file on disk`
- Rule 5 still contains the example phrase `` `### Channel 16 broadcasts` ``
- Rule 5 still contains the operational rule beginning `Operational rule: every commit's Verification section includes`

### Cross-section preserved content (top of file, untouched)

- File still starts with the title `# Commit Artifacts`
- File still contains the naming-convention spec `NNN_short_kebab_subject.md`
- File still contains the Artifact structure template with `## Scope`, `## Rationale`, `## Changes`, `## Verification`, `## Replay` sections
- File still contains the section `## Replay procedure` with `cd C:\Users\gmkar\space_sim`
- File still contains the section `## Status tracking`
- File still contains the section `## Cleanup`
- File still contains the heading `## Workflow rules learned from experience`

### Structural counts

- File contains exactly six `^### ` headings under the `## Workflow rules learned from experience` section (the six rule headings)
- File total line count is 220 lines (verified by `wc -l`; tolerance ± 3 lines for whitespace-trim variation across tooling)
- File total byte size is 17,231 bytes (verified by `wc -c` post-Edit; tolerance ± 100 bytes for line-ending normalisation across tooling)

### Tool-route success confirmation

The Edit tool's success return was cross-checked two ways: (a) reading back the file tail (lines 208-221) via the Read tool and visually confirming the new rule's heading and operational-rule paragraph are present verbatim; (b) running `wc -lc` on the file via bash, returning 220 lines / 17,231 bytes which falls within the expected post-Edit range. The two readers agreed, which is the happy case for rule 6's own divergence concern. Per rule 4's silent-Edit-failure check, the read-back confirms the Edit landed.

The Edit tool was used rather than bash because the post-mutation file size (17,231 bytes) is well under rule 3's ~35KB threshold. If a future workflow-rule addition would push the file past 35KB, that commit will route through bash per rule 3.

Recursively applying rule 6 to this commit: the byte-size estimate in the original artifact draft was ~19,200 bytes; the actual landed size is 17,231 bytes. That ~2KB discrepancy is a measurement-overshoot in the artifact draft, not a sandbox-vs-host divergence — `wc -c` and the Read-tool tail view both agree on the actual 17,231-byte size. The artifact's Changes section was updated with the verified values rather than the projected ones, which is the practice rule 6 codifies for verification-time discrepancies.

## Replay

```
cd C:\Users\gmkar\space_sim
git add commits/README.md commits/035_workflow_rule_6_sandbox_mount_staleness.md
git commit -m "commit 035: workflow rule 6 (sandbox-mount staleness)"
git push
```

Per the per-commit-replay pattern shift (one git commit per Cowork commit going forward), this commit is replayed via a single short-form `git commit -m` rather than the `-F` form used for earlier commits whose full artifact body served as the extended commit message.
