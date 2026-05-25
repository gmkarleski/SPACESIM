# Seed function versioning

**Purpose:** lock backward-compatibility guarantees for procgen seed functions before the first stage spec lands, per CONSTRAINTS.md §9 line 1759 ("seed function backward-compatibility versioning in place from day one").

**Locked by:** commit 058 (DECISIONS entry "Phase 2 Track B entry — seed versioning + Stage 1 input contract + Sol-equivalent stellar parameters").

## Why this matters

A player who runs the game at v1.0 builds a save. They install v1.1 which has updated procgen (an extra feature layer, a tuning pass, a bug fix). Their existing save must keep producing the same world — the moon they discovered must still be in the same orbit, the anomaly they catalogued must still resolve to the same finding, the supply lane they established must still pass through the same SOIs. New saves under v1.1 produce the v1.1 world.

Without versioning, ANY change to ANY seed function silently changes every existing save's universe. The patch that adds a new feature layer also moves players' familiar landmarks. That's hostile to long-running playthroughs and an unacceptable user-experience regression.

Versioning solves this by tagging each save with the seed-function version it was generated under, and dispatching to the matching seed function at load time. Old seed functions remain in the codebase (or in a versioned archive) so old saves keep producing the same world they always did.

## Granularity — monolithic version

**LOCKED:** A single monolithic version covers the whole seed function. Any change to any layer, any stage, or any sub-routine within the procgen pipeline bumps the version.

**Alternatives considered:**

- **Per-layer version** (Layer 1 vs Layer 5 etc. versioned separately): more flexible — a tuning pass on Layer 5 doesn't require bumping Layer 1's version. Rejected as premature complexity. Phase 2 has one layer in scope (Layer 5); per-layer versioning is over-engineering for one layer. When Phase 7 adds Layers 1-3, per-layer versioning can be added then if its benefits justify the cost.
- **Per-stage version** (each of 14 stages versioned separately): most flexible, most overhead. Rejected for the same reason — premature. If Stage 11 feature-layer activation rules change but Stages 1-10 don't, monolithic versioning forces a bump even though most of the universe is unchanged. Acceptable cost.

**Rationale:** the §9 mandate is "in place from day one," not "maximally granular from day one." Monolithic is the simplest version that satisfies the mandate. Granularity can expand later if experience shows it needed; collapsing granularity is harder than expanding it.

## Format — monotonic integer

**LOCKED:** Version is a monotonic integer starting at 1. Each version bump increments by 1. No semver, no date-tagging, no compound versioning.

**Alternatives considered:**

- **Semver (1.0.0, 1.0.1, ...):** overkill for the granularity we need. Major / minor / patch distinctions don't apply to seed functions.
- **Date-tagged (2026.05.25):** readable but loose. Two version bumps on the same calendar day collide. Encodes calendar information into a version space that doesn't need it.

## Storage — save metadata + code constant

**LOCKED:** Two storage sites:

1. **Save metadata.** Every save file carries a `procgen_seed_version: int` field at top-level metadata. Saves persist the version they were generated under.
2. **Code constant.** The procgen module declares `CurrentSeedVersion: int = N`. This is the source-of-truth for "what version does new-save generation use."

At load time: read save's `procgen_seed_version`; compare to `CurrentSeedVersion`; if equal, use current seed function; if less, dispatch to versioned-historical seed function for that version; if greater, fail with a "save was generated under a newer game version" error.

**Save format dependency:** the save format implementation (parallel-tracked per commit 055 D3) must include the `procgen_seed_version` field. 058 locks the field name and type so the save format work can include it when it lands.

## Backward-compatibility semantics

**LOCKED:** Existing saves continue producing the same world across version bumps. The seed function the save was generated under must remain accessible.

**Implementation discipline:** when bumping `CurrentSeedVersion`:

1. Capture the previous seed function as a versioned-historical snapshot. The codebase retains both the old and new functions; the old one is referenced only for loading old saves.
2. Test that an existing save under the old version still produces identical world parameters under the new code (regression test against representative seed values).
3. The version bump itself is a discrete commit with a DECISIONS entry naming what changed and why a bump was required.

**Cost acknowledgment:** versioning carries an ongoing cost — every seed-function change requires retaining the previous version's code. Over many bumps, the codebase carries a tail of historical seed functions. The cost is accepted because the alternative (saves silently change worlds on patches) is unacceptable.

## Hierarchical seed derivation

CONSTRAINTS §6 line 1447 specifies the hierarchical derivation: `master seed → galactic region → star → system → body → surface`.

Each level derives its seed from the parent's seed plus level-specific coordinates (region coordinates, star position within region, body identity within system, surface coordinates within body).

The specific hash function used for derivation is **not locked at 058** because Phase 2 Track B's scope is Layer 5 (body-level) and the body-seed input is provided by the (eventually) Phase 7 Layer 4. Hash-function lock is a Phase 7 decision when Layers 1-4 land. 058 locks only that derivation IS hierarchical; HOW each level derives from its parent's seed is Phase 7 work.

In Phase 2, home-system bodies receive their body-seeds as hand-tuned constants (mirrors the Sol-equivalent stellar parameters precedent in commit 058's DECISIONS entry).

## What 058 does NOT lock

- Hash function for seed derivation (Phase 7 / Layer 1-2 / Layer 4 concern)
- Per-stage RNG within Layer 5 (Stage 1 spec discusses this when Stage 1 algorithm specifics land in a later commit)
- Save-format implementation of the `procgen_seed_version` field (parallel-tracked per commit 055 D3)
- Pruning policy for historical seed functions (Phase 8 release-management concern)
- Migration tooling for cross-version save loads (Phase 8 or post-launch)

## When the first version bump fires

`CurrentSeedVersion = 1` from the moment any seed-using code lands. The first version bump (1 → 2) fires when any change to the procgen pipeline alters output for an existing seed.

For commit 058 itself (DECISIONS only, no seed-using code yet), no version constant exists in code. When Stage 1 implementation lands (a later commit), `CurrentSeedVersion = 1` lands with it.
