# 001: Convert constraints document to canonical markdown

Convert the project's constraints document from its authored `.docx` form into canonical markdown at `docs/CONSTRAINTS.md`. This is the project's ground-truth specification; all subsequent design, architectural, and implementation decisions reference this file as canonical.

The conversion is structure-preserving. All section numbering (1–14), subsection structure, status tags (LOCKED / DEFAULT / OPEN), nested bullet lists, and inline emphasis are carried over from the source `.docx` with no content changes. Markdown heading levels map as follows: `#` for the title, `##` for the fourteen numbered sections, `###` for subsections, `####` for sub-subsections (used in section 3's tiered FTL discussion and section 6's First-hour experience). Bullet lists remain bullet lists. Inline status tags (LOCKED / DEFAULT / OPEN) are rendered as `**LOCKED**`, `**DEFAULT**`, `**OPEN**` to make them scannable. Suggested code paths, identifiers, and bullet labels with technical meaning are wrapped in backticks where appropriate (e.g., `docs/CONSTRAINTS.md`, `Unity.Mathematics double3`).

This commit creates the canonical document but does not modify it. Phase 0 foundation resolutions (resolutions to issues #1, #2, #4, #5 identified during initial reading), second-tier doc updates (#3, #6, #7, #8, #10), and minor cleanups (#9, #11, #12, #13, #14) land in subsequent commits as separate logical scopes.

## Scope

- `docs/CONSTRAINTS.md` — created. Faithful markdown conversion of the source `space_sim2_constraints.docx`. Section structure, status tagging, and content preserved verbatim.

## Rationale

The source document is the senior architect's spec. It represents many hours of deliberate design thinking and contains decisions whose weirdness is the point (double-precision world coordinates, three physics modes instead of two, characters as emotional anchors for time-dilation, etc.). Converting it faithfully — without "improvements," reorganizations, or AI-pattern-matched cleanups — is the structural prerequisite for everything else. Any change to the spec lands as a separate commit with its own rationale.

Markdown is the canonical format because it is plain text (versionable, diffable, mergeable), renders cleanly on every platform, and is what AI sessions are expected to read at session start per the document's own Section 12. The `.docx` source remains as a historical artifact but is no longer authoritative — once this commit lands, `docs/CONSTRAINTS.md` is the only file the project references for constraint information.

## Changes

This is a net-new file creation. There is no prior `docs/CONSTRAINTS.md` state to diff against.

Structural elements preserved from the source:

- Document title (`# Project Constraints: Space Sim`) and lead paragraphs establishing the document as ground truth.
- Status-tag legend ("How to use this document") with LOCKED / DEFAULT / OPEN definitions.
- All fourteen numbered sections in original order: 1. Vision; 2. Foundation; 3. Gameplay systems; 4. Resources, bases, and logistics; 5. Procedural generation; 6. UI and information density; 7. Difficulty and accessibility; 8. Build order; 9. Open questions to resolve; 10. Working with Cowork; 11. Code companion documentation; 12. What every session needs to know; 13. Failure modes specific to this project; 14. Document status.
- Section 1's three subsections: Design pillars; Target player types — all served simultaneously; Things we are explicitly NOT doing.
- Section 2's subsections: Engine; Coordinate system; Reference frame hierarchy; Orbital mechanics; Physics architecture; Time representation; Save format implications; Solar system scale; Target platforms; Multiplayer architecture preparation.
- Section 3's subsections: Parts and vessel construction; Failure modes; Automation and scripting; Information asymmetry / progressive discovery; Anomalies and mysteries; Interstellar travel: tiered tech progression (with sub-subsections Tier 1 / Tier 2 / Tier 3 / Time dilation effects); Crew and characters; Goal structure.
- Section 4's subsections: Resource set; Base structure; Supply lines; Research as asynchronous progression; Mass and delta-v as central currencies.
- Section 5's subsections: Scope; Generation layers; Resource distribution; Tuning is the hard part.
- Section 6's subsections: Patterns to use; First-hour experience; Information legibility.
- Section 8's eight phase descriptions (Phase 0 through Phase 8).
- Section 9's fourteen open-questions bullets including the "RESOLVED" inline tag on Crew abstraction (cleanup deferred to a later commit per Phase 0 plan).
- Section 10's subsections: What to put in Cowork's context up front; How to structure sessions; What to be wary of; Suggested repo layout.
- Section 11's subsections: Strategy: docs per system, not per file; What gets a companion doc; What a companion doc contains; Critical practice: doc-driven development; Maintenance discipline; Template for a companion doc.
- Section 12's subsections: Mandatory reading order; Standard session start prompt template (rendered as a markdown blockquote); Red flags to watch for; Pre-flight checklist before generating code.
- Section 13's two subsections: Project-shape failures; AI-assistance-specific failures.

## Verification

A future session can confirm this commit landed correctly by checking:

1. **File `docs/CONSTRAINTS.md` exists** in the repository.
2. **The file begins with** the line `# Project Constraints: Space Sim`.
3. **The file contains exactly fourteen second-level headings** (`## 1.` through `## 14.`) with the section titles listed in the Changes section above.
4. **Section 2 contains the subsection `### Coordinate system`** with the text `LOCKED: 64-bit double-precision world coordinates (Unity.Mathematics double3). Floating origin for rendering.` (Note: this commit does NOT yet contain the 50 km threshold update from Phase 0 foundation resolutions — that lands in commit `002`. At this commit, the threshold reads `start with ~10 km, tune later`.)
5. **Section 2 contains the subsection `### Physics architecture`** describing the three modes: PhysX-active, Kepler-rails, Interstellar-cruise. (Note: at this commit, the doc does NOT yet contain a `### Mode transitions and event scheduling` subsection — that is added in commit `002`.)
6. **Section 2 contains the subsection `### Multiplayer architecture preparation`** with the deterministic-simulation requirement listed. (Note: this commit does NOT yet contain the Issue #2 resolution clarifying that determinism applies outside PhysX-active mode — that lands in commit `002`.)
7. **Section 3 contains the subsection `### Crew and characters`** with the LOCKED tag and the three character functions (Stakes / Scale / Emotional payload for time-dilation) listed in that order.
8. **Section 3 contains the four sub-subsections under `### Interstellar travel: tiered tech progression`**: `#### Tier 1: Sublight rockets`, `#### Tier 2: Laser sail propulsion (one-way)`, `#### Tier 3: Laser sail propulsion (two-way)`, `#### Time dilation effects`.
9. **Section 9 contains the line** `Crew abstraction (Phase 6): RESOLVED — headcount for base mechanics, named individuals for important missions. See section 3, Crew and characters.` (This will be removed in commit `004` as part of the minor cleanups; at this commit it must still be present.)
10. **Section 14 ends with** the paragraph beginning "Last comprehensive update: Phase 0 design crystallization..." with the document's pre-Phase-0-resolution state described. (After commit `002`, this paragraph will be revised to reflect the resolutions; at this commit it reads as in the source `.docx`.)
11. **No section contains a `### Foundational architectural principles` subsection.** This subsection is added by commit `002`.
12. **Total file length is approximately 350-420 lines of markdown** depending on formatting. (Sanity check; not a strict invariant.)

If any of checks 1–11 fail, the conversion is incomplete or has been corrupted; the file should be regenerated from the `.docx` source before subsequent commits are applied. Check 12 is a soft sanity bound — small variations are fine, but a result more than 30% off in either direction suggests something is wrong.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/001_initial_conversion.md
```
