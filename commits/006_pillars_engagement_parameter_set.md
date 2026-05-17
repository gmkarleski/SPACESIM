# 006: Two new design pillars, layered engagement framework, per-body physics parameter set

Land the foundational additions that everything in commits 007-012 builds on. Four edits across sections 1, 5, 7, and 8 add two new design pillars (discovery as gameplay grounded in real astrophysics; physics-grounded substrate), a four-layer engagement framework operationalizing the existing "layered depth" pillar, a per-body physics parameter set as the locked Phase 2 architectural commitment, and a Phase 2 deliverable clarification reinforcing that the parameter set is the data contract (not just visible rendering).

This commit is the first in the seven-commit series (006-012) reconciling material from the post-005 conversation arcs. The reconciled source document lives at `commits/RECONCILED_NEW_MATERIAL.md` in the repo. The two new pillars are the vision-level direction commitment; the framework and parameter set are the load-bearing architectural commitments that subsequent commits read from. Commit 007 builds the detection ecosystem on the parameter set; commit 008 builds the 14-stage procgen pipeline that produces it; commit 009 builds physics-driven gameplay that reads from it; commit 011 adds crater primitives that extend it.

The two new pillars are complementary rather than redundant with the existing "Discovery as the meta-game" pillar. The existing pillar names what discovery does at the structural level (it is the meta-game; information is the primary reward). The new "Discovery as gameplay" pillar names how discovery works mechanically (multiple detection methods with different blind spots; players build models from incomplete data). The "Physics-grounded substrate" pillar then commits the design discipline: every feature must connect to physics.

The layered engagement framework lands in section 7 as a refinement of the "layered depth" pillar rather than as a parallel concept. It is not a difficulty setting; it describes how the same systems serve different player depths simultaneously. The framework becomes the design test for every system added from this point forward: does it work at Layer 1 (beautiful, comprehensible) and remain interesting at Layer 4 (rewarding for deep engagement)?

The per-body physics parameter set is the architectural anchor for commits 007-012. Defining the parameter set upfront in Phase 2, even though most parameters will not be used until Phase 5-7, means subsequent systems read from existing data rather than retrofitting bodies. Adding parameters retroactively to every existing body in every save would be expensive; defining the full set upfront makes the seed function design once.

## Scope

- `docs/CONSTRAINTS.md` — modified. Four edits across sections 1, 5, 7, 8:
  - Section 1 design pillars list: two new bullets inserted after the existing "Discovery as the meta-game" bullet (Discovery as gameplay grounded in real astrophysics; Physics-grounded substrate). The list grows from 7 pillars to 9.
  - Section 5: new `### Per-body physics parameter set` subsection appended after `### Tuning is the hard part`.
  - Section 7: new `### Layered engagement framework` subsection appended after the existing Dwarf Fortress / RimWorld closing paragraph.
  - Section 8 Phase 2 description: one new paragraph appended after the existing parallel-development paragraph, naming the parameter set as the data contract.

## Rationale

The two new pillars commit the discovery-as-gameplay and physics-grounded-substrate direction at the vision level. Both are load-bearing for the subsequent commits: commit 007's detection ecosystem operationalizes the first pillar; commit 008's procgen pipeline, commit 009's physics-driven gameplay, and commit 011's dual-use technology framework all enact the second. Without these pillars in section 1, the subsequent commits would be adding mechanics whose vision-level rationale is implicit; with them, every later system has an explicit anchor.

The "Discovery as gameplay" pillar is positioned immediately after the existing "Discovery as the meta-game" pillar because the two are paired. The existing pillar gives the structural intent ("information is the primary reward"); the new pillar gives the mechanism ("multiple detection methods each reveal different aspects; depth comes from combination"). Keeping them adjacent in the list makes the pairing visible to any future reader scanning the pillars.

The "Physics-grounded substrate" pillar names a design discipline rather than a feature. The discipline is: when designing any new feature, the first question is "what physics does this connect to?" If the answer is nothing, the feature is probably wrong for this game. This forecloses certain temptations that would otherwise creep in (magic resources, arbitrary mystery stats, gameplay mechanics with no physical grounding) and aligns subsequent design decisions with a single clear filter.

The layered engagement framework operationalizes the "layered depth" pillar at the system-design level. The pillar said the same systems should serve curious newcomers and engineering nerds; the framework specifies four engagement layers (surface experience, visible information, deductive gameplay, expert play) and names the design test (every system must work at Layer 1 and remain interesting at Layer 4). Lands in section 7 because section 7 is where player-facing experience configuration lives; the framework is adjacent to the difficulty toggle list but distinct from it (it is not a setting; it is a description of how systems behave across player depths).

The per-body physics parameter set is the architectural commitment that the rest of the procgen and discovery architecture depends on. Stars get a parameter set (stellar type, mass, age, magnetic activity, etc.); planets and moons get a parameter set (mass, density, atmosphere, magnetic field, hydrosphere, resources, special features, etc.); asteroids and small bodies get a subset. The set is extensible — later systems can add parameters — but the architectural commitment is that bodies generate their full parameter set at first generation, deterministically from seed, with parameter correlations enforced by the seed function (mass and radius produce density; stellar type plus orbital distance plus atmospheric composition produces surface temperature). The placement at the end of section 5 reads cleanly as the architectural anchor for the rest of the procgen mechanics. Commit 008 will significantly restructure section 5 (replacing `### Generation layers` with `### Generation architecture` containing the six-layer hierarchy and 14-stage pipeline), and the per-body parameter set will end up adjacent to those structural pieces. For commit 006 in isolation, end-of-section placement is correct.

The Phase 2 deliverable clarification in section 8 reinforces the data-contract commitment at the build-order level. Commit 004c amended Phase 2's heading and body to include per-planet procgen as a parallel deliverable to vessel construction. This commit extends that body with one paragraph naming that per-planet generation must produce the full parameter set (not just the visible-rendering subset), because detection signatures, anomaly compatibility, resource distribution, and physics-driven gameplay all read from the same parameters that produce terrain, atmosphere, ocean, and biomes.

## Changes

### Edit 1: Section 1, design pillars list — insert two new bullets

Insert immediately after the existing `- Discovery as the meta-game. Hand-crafted home system teaches the game; procedural galaxy beyond provides endless variety. Information is the primary reward.` bullet and immediately before the `- Time as a real dimension. ...` bullet. Two new bullets:

> - Discovery as gameplay, grounded in real astrophysics. The universe is generated by physics and discovered through physics. Multiple detection methods (optical, infrared, radio, X-ray, gravitational, particle, direct sampling) each reveal different aspects of objects. Mystery is preserved by the limits of each method; depth is created by their combination. Players build models of the universe from incomplete data, refining as new instruments reveal new dimensions. This is the central exploration loop and the most differentiated feature of the game.
>
> - Physics-grounded substrate. The universe operates by real (simplified) physics. Gameplay emerges from physical principles, not arbitrary mechanics. When designing any new feature, the question is "what physics does this connect to?" — if the answer is "nothing," the feature is probably wrong for this game.

The seven existing pillars are preserved verbatim. The list grows from 7 to 9 bullets.

### Edit 2: Section 7, append new `### Layered engagement framework` subsection

Appended at the end of section 7, after the existing closing paragraph (`Each setting is independent... This is the Dwarf Fortress / RimWorld pattern.`). New subsection content:

> ### Layered engagement framework
>
> The "layered depth" pillar (section 1) operationalizes as four layers of engagement, each available to any player at any time. The framework is not a difficulty setting — it describes how the same systems serve different player depths simultaneously.
>
> **Layer 1 — Surface experience.** ...
> **Layer 2 — Visible information.** ...
> **Layer 3 — Deductive gameplay.** ...
> **Layer 4 — Expert play.** ...
>
> [Plus closing paragraph: "A given player might be Layer 1 for stellar phenomena, Layer 3 for resource extraction..."]

Full content in the file after this commit lands; the four layer descriptions plus the closing paragraph specify the framework.

### Edit 3: Section 5, append new `### Per-body physics parameter set` subsection

Appended at the end of section 5, after the existing `### Tuning is the hard part` subsection. New subsection contains:

- LOCKED commitment: every celestial body has a full physics parameter set defined at first generation; subsequent systems read from it rather than retrofitting.
- Parameter set at minimum (extensible): stars get stellar type, mass, radius, age, luminosity, surface temperature, spectral peak, magnetic activity index, flare frequency, rotation rate, metallicity, system position; planets and moons get mass through resource distribution plus an extensible "special features" dictionary; asteroids and small bodies get a subset plus orbital family membership.
- Phase 2 commitment with reasoning: parameter set defined upfront because adding parameters retroactively is expensive.
- Deterministic seed function with enforced parameter correlations (mass and radius produce density; density plus composition produces internal structure; stellar type plus orbital distance plus atmospheric composition produces surface temperature).

### Edit 4: Section 8, Phase 2 description — append parameter-set clarification paragraph

Appended after the existing closing sentence of the Phase 2 description (`Vessel construction and per-planet procgen have no hard dependencies on each other and can develop in parallel. Both meet in Phase 3 when flight gameplay requires real vessels operating against real planets rather than placeholder geometry.`):

> Per-planet generation in Phase 2 produces the full physics parameter set for each body, not just the visible-rendering subset. Detection signatures, anomaly compatibility, resource distribution, and physics-driven gameplay are all gameplay expressions of the same parameters. Terrain, atmosphere rendering, ocean, and biomes are visible expressions. Both lands in Phase 2; the parameter set is the data contract.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### New-content checks

1. Section 1's design pillars list contains the literal bullet starting `- Discovery as gameplay, grounded in real astrophysics.`
2. Section 1's design pillars list contains the literal bullet starting `- Physics-grounded substrate. The universe operates by real (simplified) physics.`
3. Section 7 contains exactly one `### Layered engagement framework` heading anchored to start-of-line.
4. The layered engagement subsection contains all four layer lead-ins: `**Layer 1 — Surface experience.**`, `**Layer 2 — Visible information.**`, `**Layer 3 — Deductive gameplay.**`, `**Layer 4 — Expert play.**`.
5. The layered engagement subsection contains the design-test phrase `Systems that only serve one layer are wrong for this game`.
6. Section 5 contains exactly one `### Per-body physics parameter set` heading anchored to start-of-line.
7. The parameter-set subsection contains the literal LOCKED commitment `**LOCKED:** Every celestial body in the universe — home system, procgen system, every planet, every moon, every asteroid, every star — has a full physics parameter set defined at the time of its first generation`.
8. The parameter-set subsection contains the literal phrase `Parameters are computed from the body's seed deterministically`.
9. Section 8's Phase 2 description contains the literal phrase `Per-planet generation in Phase 2 produces the full physics parameter set for each body, not just the visible-rendering subset`.
10. The section 1 design pillars list contains exactly 9 bullets (up from 7).

### Preserved-content anchors

11. Section 1's existing seven pillars are all preserved (each present): `- Engineering as the verb.`, `- Discovery as the meta-game.`, `- Time as a real dimension.`, `- Automation as a love letter to engineers.`, `- Selective realism.`, `Asynchronous progression. Things happen while the player is doing other things — within bounded limits`, `- Layered depth.`
12. Section 1's commit-004b and commit-005 subsections preserved: anchored heading counts of 1 for `### Tonal framing for game modes` and `### Minimal-tycoon, rich-progression positioning`. First-hour caveat phrase `once the player has accumulated agency state — bases, missions, research, probes — to advance` still present.
13. Section 2 preserved: anchored heading count of 1 for `### Foundational architectural principles`; literal phrases `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `sharp and symmetric`, `Authoritative state replication is the multiplayer model for PhysX-active vessels`, `Save files are mode-locked at creation` each present exactly once. Five numbered principles in FAP.
14. Section 3 preserved: anchored heading counts of 1 each for `### Director perspective`, `### Crew and characters`, `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts`, `### Home system evolves autonomously`, `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Goal structure`. Literal phrases `Pixar register, not Goat Simulator register`, `this is how Jeb became a legend`, `**Agency-based observation sharing.**`, `**Mobile shipping note.**` each present.
15. Section 4 preserved: literal phrases `**Network-capacity rule.**`, `Research advances only when scientists are assigned to the project` each present exactly once.
16. Section 5 existing content preserved (not in the new subsection): literal phrases `Procgen produces all planets in the universe, including the home system`, `Code is 20% of the work`, `Hill sphere spacing, frost line` each present exactly once.
17. Section 6 preserved: literal phrase `KSP's first hour was bad` present. `warp to next event` present (occurs in both section 2 and section 6).
18. Section 7 toggle list preserved: literal phrases `Physics fidelity (pragmatic / strict)` and `- Solar system scale (Casual 1/10 / Standard 1/8 / Realistic 1/1, with custom intermediate scales available)` each present. Closing line `This is the Dwarf Fortress / RimWorld pattern` preserved.
19. Section 8 preserved: anchored heading counts of 1 each for `### Phase 2 — Vessel construction and per-planet procgen (weight 2)`, `### Phase 7 — Galaxy-level procgen + interstellar (weight 3+)`, `### Phase exit criteria`. Literal phrase `**Phase 0 deliverable: netcode contract.**` present. Validation milestone phrases `This is the vertical slice MVP` and `placeholder cube launches from a planet surface` preserved.
20. Section 9 final-three-bullets adjacency: last three bullets in order `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX`. Section 9 bullet count: 13 (after commit 005's RESOLVED-crew-abstraction deletion).
21. Sections 10-14 preserved: literal phrases `siren song of pretty screenshots` (S10), `Suggested repo layout` (S10), `project's institutional memory` (S11), `Critical practice: doc-driven development` (S11), `FoundationPrimitives` companion doc (S11), `this is the kraken returning` (S12), `Pre-flight checklist before generating code` (S12), `KSP 2's path` (S13), `Stale doc syndrome` (S13), `Living document` at start of paragraph (S14), `Last comprehensive update: Phase 0 design crystallization` (S14). Each present exactly once.
22. All 14 numbered `## N.` section headings still present.
23. File line count in range 895-925 (post-005 baseline was 875 lines; this commit added approximately 34 net lines).

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/006_pillars_engagement_parameter_set.md
```
