# Session Protocol

**Purpose:** the standard way to start and structure a Cowork session on this project. Every implementation session follows this protocol to maintain quality and verification discipline.

**Read this:** at the start of every new Cowork session, before sending any prompts.

---

## Session start

### Step 1: Read the current state

Read these three files in order to ground yourself before doing anything:

1. **`docs/PHASE_TRACKER.md`** — current phase, milestone, recently landed commits, what's still open. Tells you where the project is.
2. **`docs/DECISIONS.md`** — recently resolved decisions. Tells you what's already settled so you don't re-ask.
3. **`commits/README.md`** — workflow rules. Tells you the discipline that's been learned through prior commits.

If something in any of these files contradicts what you remember, trust the files. They're the canonical record.

### Step 2: Decide session scope

Based on the current state, pick what this session will produce:

- **Implementation commit** — substantive code work. Use the implementation commit protocol below.
- **Operational commit** — workflow rule formalization, scaffolding doc update, etc. Use the operational commit protocol below.
- **Diagnostic session** — investigating a bug or unexpected behavior. Use the diagnostic protocol below.
- **Multiple commits** — chain of small commits. Follow the appropriate protocol for each.

### Step 3: Prepare the work environment

If implementation work:
- Confirm Unity Hub has the project open or available to open
- Confirm git is clean (`git status` shows no uncommitted changes you didn't intend)
- Confirm `git pull` succeeds (no remote changes you need to integrate first)

If documentation-only work:
- Confirm relevant docs are accessible

---

## Implementation commit protocol

The protocol that produced commits 027-034.

### Phase 1: Cowork proposes

Send Cowork a prompt describing the substantive change. Include:
- What's being built or changed
- Locked design decisions relevant to the work
- Flags for design choices that need user judgment
- Verification battery additions specific to this commit
- Where the commit artifact lives

Cowork responds with a proposal: file structure, type signatures, flags resolved or surfaced for confirmation.

### Phase 2: User confirms or pushes back

Read the proposal. Confirm each flag explicitly. If anything needs adjustment, say so before Cowork writes.

If the proposal surfaces a flag I missed, treat it as a real question. Cowork's diagnostic instincts have caught real issues before (commits 028, 033, the scene-state mismatches).

### Phase 3: Cowork writes

Cowork writes the files. Includes propose-before-act discipline within the writing (surface concerns before making changes that affect existing code).

### Phase 4: Verification battery

Cowork runs the standard verification battery plus commit-specific checks. Includes:
- File-level correctness
- Compilation (where applicable; verification is file-level since Cowork can't run Unity directly)
- Preserved-content anchors per workflow rules 1, 5, 6
- Commit-specific verifications surfaced in the proposal

If any check fails, Cowork stops and reports rather than retrying silently.

### Phase 5: Commit artifact

Cowork writes the commit artifact at `commits/NNN_description.md` following the established format. Records design decisions, verification results, user-side verification steps.

### Phase 6: User-side verification

If the commit affects code that runs in Unity:
1. Open Unity, wait for recompile
2. Run Test Runner (EditMode + PlayMode tabs as relevant)
3. For architectural commits with test scenes: end-to-end Play verification per the commit's user-side steps
4. Report any unexpected behavior back to Cowork for diagnosis

### Phase 7: Git commit and push

Once verification passes:
```
git add .
git commit -m "commit NNN: <short title>"
git push
```

One git commit per Cowork commit. Captures the state in version control with rollback granularity.

---

## Operational commit protocol

The protocol that produced commits 014b and 035 (workflow rule formalizations) and 036 (operational scaffolding).

### Phase 1: User proposes scope

Send Cowork the substantive content. For workflow rules, the rule text plus data points justifying it. For scaffolding docs, the reconciled-source draft I (Claude) produce in conversation.

### Phase 2: Cowork surfaces design questions

Cowork reads the proposal and surfaces flags for design choices (naming, placement, wording refinements).

### Phase 3: User confirms

Confirm flags. Iterate if needed.

### Phase 4: Cowork writes and verifies

Same as implementation protocol phases 3-5, scaled to operational work.

### Phase 5: Git commit and push

Same as implementation protocol phase 7.

No user-side Unity verification is needed for operational commits since they don't affect runnable code.

---

## Diagnostic session protocol

When something has gone wrong and needs investigation before forward progress.

### Step 1: Capture the symptom

Document what's broken. Console errors, test failures, unexpected behavior. Be specific.

### Step 2: Don't fix unilaterally

Even in GUI mode with computer-use access, Cowork should surface the diagnosis rather than make code changes without user confirmation. Diagnostic findings → user decision → fix → verification.

### Step 3: Diagnose with byte-level discipline

When sandbox view conflicts with expected state, verify with byte-level reads per workflow rule 6. The host filesystem is canonical.

### Step 4: Propose the fix

Once the root cause is identified, propose the fix as a regular commit (implementation or operational depending on scope).

### Step 5: Land the fix and re-verify

The fix lands as a normal commit through the implementation protocol. Re-verify that the symptom is gone.

---

## What to do when something feels off

If Cowork is doing something that feels wrong — moving too fast, skipping verification, making changes without proposing — stop and verify. The discipline is the asset.

Signs to watch for:
- Cowork output that claims operations completed without showing the operations
- Test counts or verification results that look too clean
- File changes outside the announced scope
- Computer-use sessions without your initiation

If any of these happen, stop and ask for ground truth before proceeding. The friction is worth it.

---

## Pacing

Sessions can be productive in 30-minute bursts or in multi-hour arcs. Both are fine. What matters:

- Don't make architectural decisions tired. Subtle bugs that surface months later trace back to fatigued decisions.
- Stop at natural milestones (commit verified, git pushed, tests green) rather than mid-commit when possible.
- If a session has accumulated more open threads than is comfortable, close out the in-flight work before starting new threads.

The project is a multi-year arc. Sustainable pacing matters more than any single session's progress.

---

## How this document maintains itself

Updates when the protocol changes (new verification category, new commit type, workflow rule promotion to standing discipline). Discovered workflow improvements that survive multiple sessions get added; one-off observations don't.
