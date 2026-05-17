# <System Name>

**Purpose:** one-sentence description of what this system does for the player or for other systems.

**Status:** Designed / In progress / Implemented / Shipped / Deferred

**Module:** `SpaceSim.Foundation.<Module>` (or `SpaceSim.Gameplay.<Module>`, etc.)

**Constraints reference:** §X `### <subsection>` in `docs/CONSTRAINTS.md` plus relevant commit numbers.

**Netcode contract reference:** §X.Y in `docs/NETCODE_CONTRACT.md` (if applicable).

---

## Player-facing description

What the player does or experiences. Keep this section to a few paragraphs. The player doesn't read code; this section describes the system as game content.

## Mechanical specification

The rules of the system. What inputs produce what outputs. Edge cases. Boundary conditions. Failure modes.

## Implementation overview

How the system is implemented in code. Module structure, key types, dispatch patterns. Cross-references to other modules this depends on or that depend on this.

## API surface

Public types and methods other code calls. Each entry briefly explains the contract.

```csharp
// Type signatures and brief contracts
```

## Tests

What's tested and at what level. EditMode coverage, PlayMode coverage, end-to-end coverage. Known gaps in test coverage.

## Open questions

Design or implementation questions specific to this system that aren't yet resolved.

## Decision log

Decisions specific to this system that were made during implementation. References to `docs/DECISIONS.md` for cross-cutting decisions; system-specific decisions documented inline here.

| Date | Decision | Why | Locked in |
|---|---|---|---|

## Future work

What's planned but not yet built. References to phase planning in `docs/PHASE_TRACKER.md` where applicable.

---

## Template usage notes (delete this section when creating from template)

This template lands at `docs/code/<system_name>.md`. Replace `<System Name>`, `<Module>`, and the section content with system-specific material. Delete sections that don't apply (e.g., "Netcode contract reference" if the system isn't covered by the contract).

Keep companion docs short and focused. If a section grows past a page, consider splitting it into its own document and linking from here.

Companion docs are internal documentation. They're not user-facing manuals. Write in plain language, not marketing copy.
