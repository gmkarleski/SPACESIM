# 011b: Resource list, supply lines, life support model, base storage amendment, section 7 toggle

Land the surgical content-replacement piece of the original commit 011. Five edits in section 4 plus one edit in section 7: the `### Resource set` subsection is fully replaced (eight-resource DEFAULT list becomes eleven-resource LOCKED list); the `### Supply lines` subsection is fully replaced (graph-of-nodes model becomes vessels-on-schedules model); a new `### Life support model` subsection is appended at end of section 4; the `### Base structure` module list bullet for storage is amended (Depot module → VAB-resident storage); section 7's Life support requirements toggle is amended (off/abstract/full → off/abstract/full/brutal with cross-reference to section 4's new model).

This commit is the more sensitive piece of the original 011 because it replaces locked content that has accumulated amendments. The Supply lines subsection had been amended by commit 004c's Network-capacity rule paragraph; that paragraph is removed in this commit's replacement (its semantic intent — that network capacity is bounded by storage rather than unbounded — is preserved within the new "Network capacity emerges from VAB storage plus active routes" subsection header). The Resource set subsection had been DEFAULT since commit 001 and is now LOCKED with a specific eleven-resource list.

The depot terminology cascade is the cross-cutting consistency concern this commit addresses. Five locations across sections 1, 2, 3, 4 reference "depot" or "depot capacity" from commit 004c's bounded-evolution content. The new Supply lines subsection establishes that storage exists in VAB facilities (not separate Depot modules) but explicitly authorizes colloquial usage of "depot" to mean "the storage at a facility." Under this clarifying sentence, all five cross-references remain consistent without requiring edits. The Base structure subsection's "Depot" module bullet is amended explicitly to bring it into alignment with the new model.

The life support model is the new substantive addition. Three-category architecture (consumption resources, exposure tracking, failure modes) plus four difficulty levels (off/abstract/full/brutal). Architecture lands in Phase 2 with the body parameter set; full implementation lands in Phase 6 with the resource system. The Brutal level is a new addition beyond the locked off/abstract/full toggle from commit 001; section 7's toggle list is amended in this commit's Edit 5 to add Brutal and cross-reference the section 4 model.

## Scope

- `docs/CONSTRAINTS.md` — modified. Five logical edits applied as one atomic write:
  - Section 4 `### Resource set`: full replacement. Old eight-resource DEFAULT list (hydrocarbons, hydrogen, water ice, common metals, rare metals, fissile material, research samples, exotic matter OPEN) → new eleven-resource LOCKED list organized into four categories (Fuel and propellant: hydrogen, oxygen, methane, kerosene, xenon; Life support: water, food; Power: fissile material; Materials: structural metals, rare metals, rare earths) plus Special (research samples) plus Phase 7+ additions (helium-3, antimatter). Multi-use principle and Direction B chemistry paragraphs added.
  - Section 4 `### Base structure`: amend the Depot module bullet. Old `- Depot (storage; resources buffer here)` → new `- Storage (VAB-resident; resources buffer in VAB capacity per `### Supply lines`)`. Other base modules in the list preserved verbatim.
  - Section 4 `### Supply lines`: full replacement. Old graph-of-nodes model (with commit 004c's appended Network-capacity rule paragraph) → new vessels-on-schedules model with route components, default-route convenience, failure-cascade behavior, network-capacity emergence from VAB storage, map visualization, cost accounting, route progression. Clarifying sentence on colloquial "depot" usage included.
  - Section 4: new `### Life support model` subsection appended at end of section (after `### Mass and delta-v as central currencies`). Three-category architecture plus four-level difficulty toggle.
  - Section 7: amend `Life support requirements (off / abstract / full)` → `Life support requirements (off / abstract / full / brutal — see section 4's `### Life support model` for the categorical model)`.

## Rationale

The resource set replacement matures DEFAULT content to LOCKED with a specific list. The old eight-resource list was always provisional ("Final resource list to be locked before Phase 6. Names and exact set may shift"); the new list is the lock. The reorganization into categories (Fuel and propellant / Life support / Power / Materials / Special / Phase 7+ additions) makes the resource economy structure visible. Helium-3 and antimatter as Phase 7+ additions replace the previous "Exotic matter or equivalent OPEN" placeholder with specific named resources tied to specific propulsion tiers (fusion via helium-3; highest-energy-density via antimatter).

The supply lines replacement is the larger reframe in this commit. The old "Graph of nodes with transit-time edges. Fallout 4 style. Set up once, runs automatically" model was an abstraction: routes had transit time and throughput parameters; the simulation handled the rest as a graph computation; "This is NOT a real spatial simulation of transport vessels." The new model commits the opposite: supply lines ARE a real spatial simulation. Each route is a launch site, a destination, a configured supply craft design, and an automated schedule. Routes execute autonomously; the supply craft are vessel designs like any others, with full engineering trade-offs.

The motivation for the reframe: under commit 010's logistics-not-tech progression gating, the rocket equation is the gate. Supply line efficiency is therefore real engineering — a supply craft that uses 80% of its payload mass on fuel is mostly burning fuel rather than moving it. The graph-computation model abstracted this away. The vessels-on-schedules model makes supply line design a genuine engineering activity that connects to the broader physics-grounded substrate. Players who design efficient supply craft have efficient logistics.

The clarifying sentence about colloquial "depot" usage handles a real cross-reference issue. Commit 004c's bounded-evolution content uses "depot" / "depot capacity" in five locations across sections 1, 2, 3, 4. The new supply lines subsection says depots don't exist as separate modules. Without the clarifying sentence, the cross-references become inconsistent. With it, the colloquial usage is explicitly authorized: "depot" continues to mean "the storage at a facility," and under the new model the storage at a facility lives in its VAB.

The Base structure amendment is the only existing-locked-content amendment outside the two replacement subsections. Commit 001's base structure list had "Depot (storage; resources buffer here)" as one of eight module types. Under the new supply lines model, depots aren't a separate module type; storage is VAB-resident. The bullet is amended to reflect this. The other seven module types (Habitat, Miner, Refinery, Power, Research station, Laser array, Shipyard) are preserved verbatim.

The life support model addition operationalizes life support requirements at the categorical level. Three categories: continuous-consumption resources (food, water, oxygen consumed per crew per day); cumulative exposure tracking (radiation dose, zero-G exposure, mission stress); discrete failure modes (decompression, thermal failure, G-force injury, radiation event, supply depletion). Four difficulty levels make the categorical model configurable: off (no life support requirements), abstract (single resource per crew per day, the default), full (all three categories tracked separately, realistic tolerances), brutal (adds psychological stress, individual crew health tracking). The Brutal level is new beyond commit 001's three-level toggle.

The section 7 toggle amendment brings the difficulty toggle list current with the new categorical model. Same paired-edit pattern as commit 003 used for the physics fidelity toggle: when a new mechanic is added that exposes a new difficulty level, section 7's toggle list is amended in the same commit to maintain consistency.

The placement of the new Life support model subsection at the end of section 4 (after `### Mass and delta-v as central currencies`) is intentional. Mass and delta-v as the closing thought of section 4 reads as a meta-commitment about resource economics; life support model adds a specific operational system on top of that meta-commitment. The reading flow through section 4 becomes: resources (what exists) → bases (where they're stored and produced) → supply lines (how they move) → research (how they're consumed for progression) → mass and delta-v (the central economic currency) → life support (the operational consumption pattern for crew).

## Changes

### Edit 1: Section 4 — replace `### Resource set` subsection

Full replacement of the existing subsection. The DEFAULT 6-8 resources list becomes a LOCKED eleven-resource list organized into categories. Old resource names that change:

- "Hydrocarbons (kerosene, methane)" → split into separate "Methane" and "Kerosene" entries
- "Water ice" → "Water" (ubiquitous as ice; recyclable)
- "Common metals" → "Structural metals" (iron, aluminum, titanium aggregated)
- "Exotic matter" → split into Phase 7+ "Helium-3" and "Antimatter"

New resources added: Oxygen (explicit, was implicit), Xenon (for ion drives), Food (life support), Rare earths (lanthanides tier).

Resources preserved: Hydrogen, Rare metals, Fissile material, Research samples.

Two new closing paragraphs: Multi-use principle (every resource has multiple sources and multiple uses); Direction B chemistry (discrete gameplay-meaningful units with realistic uses, not simulated element-by-element chemistry).

### Edit 2: Section 4 `### Base structure` — amend Depot module bullet

The existing eight-bullet module list is preserved. The bullet `- Depot (storage; resources buffer here)` is amended to `- Storage (VAB-resident; resources buffer in VAB capacity per `### Supply lines`)`. The shift reflects that under the new supply lines model, storage is VAB-resident and there is no separate Depot module type. The cross-reference to `### Supply lines` resolves to the same-commit replacement.

### Edit 3: Section 4 — replace `### Supply lines` subsection (including commit 004c Network-capacity rule paragraph)

Full replacement of the existing subsection, including the appended Network-capacity rule paragraph from commit 004c. The new subsection contains:

- LOCKED commitment that supply lines operate as scheduled real vessels rather than abstract throughput parameters.
- Storage-in-VAB-facilities paragraph plus the clarifying sentence on colloquial "depot" usage.
- Five route component bullets: Launch site, Destination, Supply craft design, Schedule (time-based/threshold-based/event-based/conditional), Active/paused state.
- `**Default routes are easy to set up.**` paragraph: game suggests basic craft design, basic schedule, basic trajectory; player can accept or customize.
- `**Routes can fail.**` paragraph: failure modes at launch / in transit / at destination; failures cascade as transmissions; route pauses pending attention.
- `**Network capacity emerges from VAB storage plus active routes.**` paragraph: total storage is sum of VAB capacities; routes redistribute but don't create storage; producing facility whose VAB is full idles per bounded-evolution rules.
- `**Visualization on the map.**` paragraph: active routes visualized at appropriate scale; sort and manage from map view.
- `**Cost accounting is real engineering.**` paragraph: supply craft consume fuel to deliver fuel; route efficiency matters; rocket equation discipline applies.
- `**Route progression:**` paragraph: first-time players get defaults; engaged players optimize; mastery players automate with Vizzy.

The semantic intent of commit 004c's Network-capacity rule paragraph is preserved within the new "Network capacity emerges from VAB storage plus active routes" subsection header: network total storage is bounded; routes don't create storage; bounded-evolution rules apply.

### Edit 4: Section 4 — append new `### Life support model` subsection at end of section

Appended after `### Mass and delta-v as central currencies`. The new subsection contains:

- LOCKED commitment: three-category architecture for crew sustenance and exposure.
- `**Consumption resources (continuous).**` paragraph: food, water, oxygen consumed per crew per day at fixed rates; production via hydroponics, recyclers, generators; storage as consumables.
- `**Exposure tracking (cumulative).**` paragraph: radiation dose, zero-G exposure, mission stress; accumulate with thresholds; mitigated by shielding, spin gravity, mission rotation.
- `**Failure modes (discrete).**` paragraph: decompression, thermal failure, G-force injury, radiation event, supply depletion.
- Four difficulty toggle bullets: Off (no life support requirements), Abstract default (single life support resource per crew per day), Full (all three categories tracked separately, realistic tolerances), Brutal (adds psychological stress, individual crew health tracking).
- Phasing closing: architecture lands in Phase 2 (body parameter set supports it); full implementation lands in Phase 6 with the resource system.

### Edit 5: Section 7 — amend Life support requirements toggle

The existing toggle bullet `- Life support requirements (off / abstract / full)` is amended to `- Life support requirements (off / abstract / full / brutal — see section 4's `### Life support model` for the categorical model)`. Adds the Brutal level and cross-references the new section 4 model.

## Verification

A future session can confirm this commit landed correctly by running the following checks.

### Resource set replacement

1. Literal LOCKED phrase `Eleven physical resources at v1 plus categorical samples. Helium-3 and antimatter are Phase 7+ additions` is present.
2. All six category headers present: `**Fuel and propellant:**`, `**Life support:**`, `**Power:**`, `**Materials:**`, `**Special:**`, `**Phase 7+ additions:**`.
3. All eleven physical resources present as bolded leads: `- **Hydrogen**`, `- **Oxygen**`, `- **Methane**`, `- **Kerosene**`, `- **Xenon**`, `- **Water**`, `- **Food**`, `- **Fissile material**`, `- **Structural metals**`, `- **Rare metals**`, `- **Rare earths**`.
4. `- **Research samples**` present under Special.
5. Phase 7+ additions present: `- **Helium-3**`, `- **Antimatter**`.
6. Paragraph headers present: `**Multi-use principle.**`, `**Direction B chemistry (not full simulation).**`.

### Old resource content absent

7. Old DEFAULT phrasing absent: `**DEFAULT:** 6–8 resources, each with clear sources and uses` is ABSENT.
8. Old `**Hydrocarbons** (kerosene, methane)` resource bullet is ABSENT.
9. Old `- **Water ice** — life support` resource bullet is ABSENT.
10. Old `**Common metals** — structure, construction` resource bullet is ABSENT.
11. Old `**Exotic matter** or equivalent (**OPEN**` resource bullet is ABSENT.

### Base structure amendment

12. New storage bullet present: `- Storage (VAB-resident; resources buffer in VAB capacity per `### Supply lines`)`.
13. Old Depot bullet absent: `- Depot (storage; resources buffer here)` is ABSENT.

### Supply lines replacement

14. Literal LOCKED phrase `Supply lines operate as scheduled real vessels rather than abstract throughput parameters` is present.
15. Literal phrase `Storage exists in VAB facilities; there are no separate depot modules` is present.
16. Clarifying sentence on colloquial "depot" usage present: literal phrase `The word "depot" is sometimes used colloquially in this doc and in player-facing text`.
17. All five Route component bullets present: `- **Launch site:**`, `- **Destination:**`, `- **Supply craft design:**`, `- **Schedule:**`, `- **Active/paused state:**`.
18. All six paragraph headers present: `**Default routes are easy to set up.**`, `**Routes can fail.**`, `**Network capacity emerges from VAB storage plus active routes.**`, `**Visualization on the map.**`, `**Cost accounting is real engineering.**`, `**Route progression:**`.
19. Bounded-evolution cross-reference preserved within new content: literal phrase `per the bounded-autonomous-evolution rules in section 3` present.

### Old supply lines content absent

20. Old graph-of-nodes phrasing absent: `Graph of nodes with transit-time edges. Fallout 4 style.` is ABSENT.
21. Old "NOT a real spatial simulation" phrasing absent: `This is NOT a real spatial simulation of transport vessels. It's a graph computation.` is ABSENT.
22. Commit 004c Network-capacity rule paragraph absent: `**Network-capacity rule.**` is ABSENT.

### Life support model

23. `### Life support model` anchored heading count is 1.
24. Literal phrase `Three-category architecture for crew sustenance and exposure` is present.
25. All three category headers present: `**Consumption resources (continuous).**`, `**Exposure tracking (cumulative).**`, `**Failure modes (discrete).**`.
26. All four difficulty toggle bullets present: `- **Off:**`, `- **Abstract (default):**`, `- **Full:**`, `- **Brutal:**`.
27. Phasing closing phrase present: `Architecture lands in Phase 2 (body parameter set supports it). Full implementation in Phase 6 with the resource system`.

### Section 7 toggle amendment

28. New amended toggle present: `- Life support requirements (off / abstract / full / brutal — see section 4's `### Life support model` for the categorical model)`.
29. Old toggle absent: `- Life support requirements (off / abstract / full)` (with terminating newline) is ABSENT.

### Section 4 subsection ordering

30. Section 4's complete subsection list, in document order, is exactly: `### Resource set`, `### Base structure`, `### Supply lines`, `### Research as asynchronous progression`, `### Mass and delta-v as central currencies`, `### Life support model`. Total: 6 subsections.

### Cross-references preserved as colloquial usage

31. Commit 004c "depot" cross-references remain intact (preserved as colloquial usage per the new Supply lines clarifying sentence):
    - Section 1 design pillar: `bases produce until depots fill, then idle` still present.
    - Section 2 Save format implications: `production stops when depot capacity is reached (the base idles until capacity is freed)` still present.
    - Section 3 `### Home system evolves autonomously`: `Bases don't accumulate resources passively beyond their depot capacity` still present.
    - Section 3 `### Home system evolves autonomously`: `**Supply line refinement.**` paragraph still present.

### Preserved-content anchors

32. Commit 011a content preserved: anchored heading counts of 1 each for `### Scaling discipline`, `### Crater primitives and bounded terrain modification`, `### Movie-moment mechanics`, `### Dual-use technology framework`. Literal `**Part categories at v1 (revised from ~30 to ~35-40):**` present.
33. Commit 010 content preserved: anchored heading counts of 1 each for `### In-media-res starting state`, `### Logistics-not-tech as primary progression gating`, `### Configurable starting conditions`. Literal `The rocket equation is the gate`, `**What the tech tree still gates.**` each present.
34. Commit 009 content preserved: anchored heading counts of 1 each for `### Physics-driven gameplay`, `### Home observatory`, `### Catalog as long-term meta-game`.
35. Commit 008 content preserved: anchored heading counts of 1 each for `### Generation architecture`, `### The 14-stage pipeline (Layer 5)`, `### Hybrid pipeline-plus-grammar`, `### Planet variety tiers`, `### Feature-layer architecture`, `### The 90/9/1 anomaly distribution`, `### Galaxy scope`, `### Map view hierarchy`. Literal `90% resolve to interesting-but-understandable phenomena` present.
36. Commit 007 content preserved: anchored heading counts of 1 each for `### Detection ecosystem`, `### Platforms for detection`, `### Discovery progression — eight stages`, `### Detection mechanics`, `### Anomaly types worth seeding`.
37. Commit 006 content preserved: `### Per-body physics parameter set` and `### Layered engagement framework` each anchored count 1. Section 1 design pillars list contains `- Discovery as gameplay, grounded in real astrophysics.` and `- Physics-grounded substrate.`
38. Commit 004a/b/c content preserved: anchored heading counts of 1 each for `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Tonal framing for game modes`, `### Channel 16 broadcasts`, `### Home system evolves autonomously`. Literal `Save files are mode-locked at creation`, `1/8 real scale`, `within bounded limits that prevent passive accumulation`, `**Phase 0 deliverable: netcode contract.**` each present. Anchored headings for Phase 2 and Phase 7 amended titles each count 1.
39. Commit 005 content preserved: `### Phase exit criteria`, `**Mobile shipping note.**`, `FoundationPrimitives` companion doc reference.
40. Commit 003 content preserved: `**Agency-based observation sharing.**`, `detection-aggressiveness parameter`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.`
41. Commit 002 content preserved: `### Foundational architectural principles` anchored heading count 1; literal `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `sharp and symmetric`, `Authoritative state replication is the multiplayer model for PhysX-active vessels` each present. Five numbered principles in FAP.
42. Commit 001 content preserved across untouched sections: distinctive phrases per section (Pixar register, Jeb legend, Hill sphere, 20%/80%, KSP's first hour, Dwarf Fortress / RimWorld, physics fidelity toggle, vertical slice MVP, placeholder cube, siren song, suggested repo layout, institutional memory, doc-driven dev, kraken returning, pre-flight checklist, KSP 2's path, stale doc syndrome, Living document, Last comprehensive update). Each present exactly once.
43. Section 9 final-three-bullets adjacency: `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX` in order. Section 9 bullet count: 13.
44. All 14 numbered `## N.` section headings still present.
45. File line count is 1370 (post-011a was 1318 lines; this commit added 52 net lines).

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/011b_resources_supply_lines_life_support.md
```
