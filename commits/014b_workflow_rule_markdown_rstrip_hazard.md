# 014b: Add fifth workflow rule on markdown-shape rstrip-and-append hazard

Append a fifth learned-rule entry to `commits/README.md`'s "Workflow rules learned from experience" section, capturing the failure mode that surfaced and was repaired during commit 014.

The rule names the failure: when appending to or replacing a subsection whose existing content contains markdown that looks like other structural markdown (backtick-wrapped inline heading references, fenced-code-block markers, inline bold-as-heading-substitutes), the naive `text.rstrip() + new_content` pattern can split or re-space the existing content in ways that turn previously-inline markdown into what looks like a top-level structural element. Commit 014 produced a concrete instance: the existing Transmissions subsection ended with `(see \`### Channel 16 broadcasts\` below).`. The append operation truncated the paragraph at the opening backtick, leaving a dangling fragment at the original location and an orphan `### Channel 16 broadcasts\` below).` line beneath the appended content — which, because it began with `###`, parsed as a malformed h3 heading. The failure is invisible to substring-presence verification because every distinctive phrase the commit checks for is still present; only the structural role of the text changes.

The rule names the two defenses: (1) use line-anchored regex for heading detection in verification, not substring presence; (2) when appending to or replacing locked content, capture the exact final paragraph or full subsection text pre-write and verify it survives verbatim post-write, anchored on the distinctive phrase plus its surrounding context. The rule closes with an operational requirement: every commit's Verification section includes a line-anchored `^### ` count check for each section the commit modifies, and for any commit that appends to or replaces locked content, at least one verbatim-with-context anchor on the boundary where new content meets preserved content.

This is a meta-commit (changes to `commits/README.md`, which the README itself calls out as artifact-worthy). The numbering uses `014b` rather than `015` because the content is methodologically immediately downstream of commit 014's failure-and-repair experience, not a new logical scope; it documents what 014 taught the process. The next research-content commit will be `015`.

## Scope

- `commits/README.md` — append a fifth rule section titled `### Markdown-shaped content can survive rstrip-and-append in misleading shapes` at the end of the "Workflow rules learned from experience" section. No edits to the four pre-existing rules. No edits to any other section of the file.

## Rationale

The four pre-existing learned rules were each added after a specific failure during early Phase 0 work: preserved-content anchors after commit 002's silent truncation; Write-over-Edit after observed multi-Edit failures on the same file; the bash escape hatch after the tool-layer ~42KB write timeout; and the silent-Edit-failure rule after a small-file Edit reported success without writing through.

Commit 014 produced a fifth class of failure that none of the existing four rules would catch by itself. The atomic write went through correctly (Rule 3 satisfied: bash route, no timeout). The verification battery included preserved-content anchors as Rule 1 requires (the phrase `Channel 16 broadcasts` was checked for presence and was found). What surfaced the bug was a *structural* check — section 3's h3 subsection count came out one higher than projected — and the diagnosis required inspecting the actual heading list. Substring-presence verification could not have caught the malformed heading because the substring was still present; only its structural role had changed.

This is the failure mode worth naming. The two defenses are independently load-bearing:

The structural-count check catches the *category* of damage where markdown surface-shape is preserved but structural role changes. It is the only reliable signal that a commit's edits disturbed the markdown's structural skeleton, not just its prose.

The context-anchored verbatim check catches the specific case where existing markdown contains structurally-significant characters (backticks, hashes, asterisks) in inline positions that an rstrip-and-append operation can re-space into structural positions. The context anchor matters because the distinctive phrase alone is insufficient: in commit 014's case, the phrase `Channel 16 broadcasts` was *legitimately* present in three places (one inline reference, one actual heading, one corrupted orphan) and substring-presence could not distinguish them.

The rule frames both defenses as operational requirements rather than optional best practices, because commit 015 is full-ceremony replacement of locked content (`### Research as asynchronous progression` → research-as-logistics-driven question-answering) with cascading cross-references to anomalies, missions, tech tree, observatory, and catalog. The replacement target's content includes inline backtick-wrapped heading references in the same shape that caused commit 014's failure. The rule must be in place before that work begins, with the verification template explicitly carrying both defenses, so the same failure mode cannot recur in a more consequential commit.

The framing of the rule follows the established pattern in `commits/README.md`: name the failure mode, give the defense, close with an operational rule. The example (commit 014's truncated paragraph at the inline backtick reference) is preserved as the worked instance so future readers can reconstruct the failure shape without reading commit 014's artifact.

## Changes

A single append operation to `commits/README.md`, written through the bash escape hatch even though the file is small (well under the ~42KB tool-layer cap) for unambiguous atomicity and to avoid the Rule 4 silent-Edit-failure risk on small files. The append adds the following content to the very end of the file, after the existing `### Edit may silently fail to mutate even small files` rule and its closing paragraph:

```markdown
### Markdown-shaped content can survive rstrip-and-append in misleading shapes

When appending to or replacing a subsection whose existing content contains markdown that looks like other structural markdown — backtick-wrapped inline heading references like `` `### Channel 16 broadcasts` ``, fenced-code-block markers, inline bold-as-heading-substitutes — the naive `text.rstrip() + new_content` pattern can split or re-space the existing content in ways that turn previously-inline markdown into what looks like a top-level structural element. Commit 014 produced a concrete instance: ...

[full text of the new rule, ~3.1KB]
```

File grew from 196 lines / 10,685 bytes to 210 lines / 13,847 bytes (+14 lines, +3,162 bytes). The append is atomic (write-to-`.recovery` then `os.replace`).

## Verification

All checks below must pass.

### New rule present and well-formed

- File contains exactly one line-anchored heading `^### Markdown-shaped content can survive rstrip-and-append in misleading shapes$`
- The new rule body contains the example phrase `` `### Channel 16 broadcasts` `` (backtick-wrapped inline reference, as the example of structurally-significant inline markdown)
- The new rule body contains the sentence fragment `Commit 014 produced a concrete instance`
- The new rule body contains the defense statement `line-anchored regex for heading detection`
- The new rule body contains the defense statement `verbatim post-write` and the phrase `surrounding context`
- The new rule body ends with an operational rule beginning `Operational rule: every commit`

### Rule ordering

The five rule headings appear in `commits/README.md` in this exact order (verified by `^### ` line-anchored regex):

1. `### Verification checks must include preserved-content anchors`
2. `### Choose Write over Edit for changes spanning multiple subsections`
3. `### Tool-layer write timeout produces an effective ~42KB cap; use bash for larger writes`
4. `### Edit may silently fail to mutate even small files`
5. `### Markdown-shaped content can survive rstrip-and-append in misleading shapes`

The new rule must appear *last* in the file (no content other than terminal whitespace after it).

### Pre-existing rules preserved verbatim

Each of the four pre-existing rule sections must still contain its distinctive content:

- Rule 1 still contains the phrase `silently truncates or corrupts unrelated content`
- Rule 1 still contains the prescription `Three to five anchors per untouched section is typical`
- Rule 2 still contains the phrase `Reliability beats elegance when the canonical doc is at stake`
- Rule 3 still contains the phrase `~42KB on this workspace`
- Rule 3 still contains the code-block content marker `PYEOF_INNER`
- Rule 3 still contains the threshold statement `If the file will exceed ~35KB after the operation`
- Rule 4 still contains the phrase `cached read-view that diverges from the file on disk`

### Cross-section preserved content (top of file, untouched)

- File still starts with the title `# Commit Artifacts`
- File still contains the naming-convention spec `NNN_short_kebab_subject.md`
- File still contains the Artifact structure template with `## Scope`, `## Rationale`, `## Changes`, `## Verification`, `## Replay` sections
- File still contains the section `## Replay procedure` with `cd C:\Users\gmkar\space_sim`
- File still contains the section `## Status tracking`
- File still contains the section `## Cleanup`
- File still contains the heading `## Workflow rules learned from experience`

### Structural counts

- File contains exactly five `^### ` headings under the `## Workflow rules learned from experience` section (the five rule headings)
- File total line count is 210 ± 3 lines
- File total byte size is 13,800 ± 100 bytes

## Replay

```
cd C:\Users\gmkar\space_sim
git add commits/README.md commits/014b_workflow_rule_markdown_rstrip_hazard.md
git commit -F commits/014b_workflow_rule_markdown_rstrip_hazard.md
```
