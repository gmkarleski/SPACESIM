# 011a: Scaling discipline, crater primitives, parts extension, movie-moment mechanics, dual-use technology

Land the additive cross-cutting refinements from the larger commit 011 — the half of 011 that adds new content without replacing existing locked content. Five edits across sections 2, 3, 5: section 2 gains `### Scaling discipline` between `### Interstellar distances` and `### Target platforms`; section 5 gains `### Crater primitives and bounded terrain modification` at end of section; section 3's existing `### Parts and vessel construction` subsection is extended with revised category list at end; section 3 gains `### Movie-moment mechanics` and `### Dual-use technology framework` at end (after `### Home observatory` from commit 009).

The two replacements (resource list, supply lines) plus the life support model are deferred to commit 011b in a later session. Splitting 011 into 011a/011b keeps cognitive load manageable for the resource-list replacement (which removes commit 001's locked Resource set and replaces with an eleven-resource list) and the supply-lines replacement (which removes commit 001's "Graph of nodes with transit-time edges. Fallout 4 style" framing and replaces with scheduled real-vessels-on-schedules).

Five additive subsections in this commit, no existing content replaced. The parts-and-vessel-construction extension preserves the existing locked subsection verbatim and appends to it; the crater primitives subsection adds a Stage 11 feature-layer specification consistent with commit 008's feature-layer architecture; the scaling discipline subsection crystallizes the principle behind commit 004b's solar system scale and interstellar distances commitments; the movie-moment mechanics subsection commits six engineering-improvisation mechanics with discipline that the physics is the gameplay; the dual-use technology framework subsection commits destruction-as-physics-consequence (compatible with the existing combat-as-non-goal) plus the endgame propulsion tiers extension (chemical / nuclear thermal / laser sail tiers / fusion / antimatter, no FTL beyond physics-grounded options).

The dual-use technology framework is the load-bearing piece in this commit because it resolves a tension the doc has carried since commit 001's combat-as-non-goal commitment. Combat as a gameplay mode (weapons tab, targeting reticles, click-to-damage interactions) remains forbidden. Destruction as a physics consequence of legitimate engineering tools (nuclear devices used as terraforming or Project Orion propulsion; high-power lasers used as sail propulsion or sensor disruption; mass drivers used as surface-to-orbit launchers or kinetic impactors) is the design pattern. The distinction is made explicit: "tech tree has terraforming tools, propulsion infrastructure, energy systems. No 'weapons' tab. Mission planning has 'deliver payload to body' with payload type 'nuclear device,' not 'target enemy ship' with targeting reticle."

## Scope

- `docs/CONSTRAINTS.md` — modified. Five logical edits applied as one atomic write:
  - Section 2: new `### Scaling discipline` subsection inserted between `### Interstellar distances` and `### Target platforms`. Section 2 grows from 14 to 15 subsections.
  - Section 5: new `### Crater primitives and bounded terrain modification` subsection appended at end of section, after `### Tuning is the hard part`. Section 5 grows from 11 to 12 subsections.
  - Section 3: existing `### Parts and vessel construction` subsection extended with revised category list (eight category bullets plus closing paragraphs) appended after the existing closing sentence (`Vessel structure: tree (one parent per part)...`).
  - Section 3: new `### Movie-moment mechanics` subsection appended at end of section, after `### Home observatory` (from commit 009). Six bolded mechanic paragraphs plus closing discipline paragraph.
  - Section 3: new `### Dual-use technology framework` subsection appended immediately after `### Movie-moment mechanics`. Five dual-use technology bullets plus server-rules / friction / endgame-propulsion-tiers paragraphs. Section 3 grows from 22 to 24 subsections.

## Rationale

The scaling discipline subsection crystallizes a principle that has been implicit since commit 004b's solar system scale and interstellar distances commitments. Some things scale (bodies and inter-body distances; atmospheric depth; star gravitational parameters for orbital mechanics); some things don't (craft and craft components; interstellar distances; stellar luminosity and temperature; communication delay). Implementation seams are named explicitly (SOI boundaries between in-system and interstellar; atmospheric physics within tuned atmospheres; detection signature computation from real luminosities). Without explicit scaling discipline, implementation can subtly fudge in ways that compound. With it, every implementation decision can be checked against the principle.

The crater primitives subsection commits a specific Stage 11 feature layer with runtime mutability. Initial body generation places craters appropriate to age and bombardment history. The crater system supports runtime additions for impact events, nuclear detonations on surfaces, mining operations, and construction site preparation. Crater geometry uses standard impact scaling (energy maps to crater diameter via established impact physics relationships). Each body has a crater list initially populated by procgen but mutable at runtime; rendering at any LOD reads from the current list; save state stores the full list including runtime additions. Body fragmentation as event-driven state change is committed: impacts exceeding the body's gravitational binding energy by sufficient margin trigger fragmentation, which removes the body from procgen and places a procedural asteroid field at its orbital position. Small bodies are fragmentable by realistic gameplay forces; large bodies require energies beyond normal gameplay range. Other terrain modification (trenches, roads, biome changes, water displacement, atmospheric effects from impacts) is explicitly deferred to post-v1.

The parts-and-vessel-construction extension revises the part categories from ~30 to ~35-40 with explicit per-category counts. Parts are the universal construction vocabulary: rockets, stations, bases, infrastructure, observatories, and propulsion systems are all built from the same parts system. Eight category groups are named with counts. The "construction contexts themselves are built" paragraph commits that home ground VAB is starting infrastructure (consistent with commit 010's in-media-res starting state), and that orbital VABs and surface VABs at other bodies are built by the player using parts they can launch or transport. The closing paragraph names that procedural parts produce many variants from few categories — 35-40 categories produces potentially hundreds of distinct part variants.

The movie-moment mechanics subsection commits six mechanics that produce engineering-improvisation moments characteristic of the genre's most beloved scenes. The shared property: multi-purpose physical systems that can be used in ways the designer did not anticipate. Differential thrust from multi-engine clusters allows asymmetric thrust for attitude or counter-force; thrust vectoring beyond designed range lets the player push engines past rated parameters in emergencies with stress accumulating; spin gravity through actual rotation gives crew on outer ring artificial gravity with real Coriolis force; communication delay with real consequences makes Vizzy scripting essential beyond reasonable command-and-control delay; procedural failure modes produce varied probabilistic mechanical failures; real telemetry reveals problems before they fail. The implementation discipline closing the subsection is the load-bearing principle: "make the physics real and legible. Do not add gameplay layers on top of physics. No 'docking skill check,' no 'engineering bonus.' The physics is the gameplay. Players who understand physics can do interesting things."

The dual-use technology framework subsection commits destruction-as-physics-consequence rather than combat-as-gameplay-mode. Combat as a gameplay mode remains the locked non-goal from commit 001. Dual-use technologies (nuclear devices, high-power lasers, mass drivers, reactor technology, plasma and ion technologies) have predictable destructive consequences when applied to fragile targets, but the destructive use is consequence-of-physics, not weapons-category. The tech tree has terraforming tools, propulsion infrastructure, energy systems — no "weapons" tab. Mission planning has "deliver payload to body" with payload type "nuclear device," not "target enemy ship" with targeting reticle. Server rules govern destructive use against other agencies: cooperative servers protect other agencies' assets; competitive servers permit physical-consequence engagement; self-destruction is unrestricted. Friction comes from physics: nuking a base requires getting the device there; frying electronics with a laser requires the target in range with sufficient power. The endgame propulsion tiers paragraph extends the locked tier 1/2/3 interstellar progression with additional propulsion tiers (chemical starting, nuclear thermal intermediate, fusion advanced via helium-3, antimatter highest and post-v1) and explicitly rejects FTL beyond physics-grounded options: "Star Trek warp would undermine the time-dilation pillar and is rejected."

The placement of all four new subsections (scaling discipline in section 2; crater primitives in section 5; movie-moment mechanics and dual-use technology in section 3) plus the parts extension was determined by the reconciled source. Scaling discipline lands adjacent to the other scale commitments. Crater primitives lands at end of section 5 to keep the procgen architecture chain (Generation architecture through Galaxy scope plus Per-body parameter set) intact without interruption — the projected post-all-commits ordering (with crater primitives between Per-body and Resource distribution) can be achieved by a future reorder if needed. Movie-moment mechanics and dual-use technology land at end of section 3 to keep the existing subsection ordering (parts/failure-modes/automation/info-asymmetry/detection-ecosystem/anomalies/interstellar-tiers/director/crew/EVA/transmissions/Channel-16/home-evolves/multiplayer/mode-portable/goal-structure/physics-driven/home-observatory) intact.

Within-commit forward reference: crater primitives' closing paragraph references "the dual-use technology framework's visible consequences" — dual-use technology is added in this same commit (Edit 5). After 011a lands, both subsections exist; the cross-reference resolves.

Between-commits forward reference: the parts extension's "Habitats and life support (~2)" bullet says "Separate life support modules" without defining the model. The life support model lands in commit 011b. After 011a lands and before 011b lands, this is an acceptable between-commits inconsistency (same pattern as commit 007's 90/9/1 forward reference to commit 008).

## Changes

### Edit 1: Section 2, insert new `### Scaling discipline` subsection between `### Interstellar distances` and `### Target platforms`

Inserted at the boundary between the existing `### Interstellar distances` subsection (commit 004b) and the existing `### Target platforms` subsection (commit 001). The new subsection contains:

- LOCKED commitment phrase ("Scale what is necessary for gameplay tractability. Keep real what is necessary for physics consistency. Document where the seams are.").
- Three bolded structural lead-ins (`**What scales:**`, `**What does not scale:**`, `**Implementation seams:**`) with bullet lists.
- Closing principle paragraph ("Without explicit scaling discipline, implementation can subtly fudge in ways that compound. With it, every implementation decision can be checked against the principle.").

### Edit 2: Section 5, append new `### Crater primitives and bounded terrain modification` subsection at end of section

Inserted at the end of section 5 after `### Tuning is the hard part` and immediately before `## 6. UI and information density`. The new subsection contains:

- LOCKED commitment that crater placement is a Stage 11 feature layer applicable to all rocky bodies, with runtime mutability for impact events, nuclear detonations, mining, and construction site preparation.
- Paragraph on crater geometry using standard impact scaling (energy maps to crater diameter; surface burst vs subsurface burst; overlapping craters use standard composition rules).
- Paragraph on crater list mutability at runtime, LOD rendering, and save state inclusion.
- Body-fragmentation paragraph (`**Body fragmentation as event-driven state change.**`) committing that impacts exceeding gravitational binding energy trigger fragmentation, removing the body from procgen and placing a procedural asteroid field.
- Closing paragraph: other terrain modification (trenches, roads, biome changes, water displacement, atmospheric effects from impacts) is deferred to post-v1; the crater and fragmentation primitives cover the dual-use technology framework's visible consequences (within-commit forward reference to Edit 5).

### Edit 3: Section 3, extend `### Parts and vessel construction` subsection with revised category list

Appended at the end of the existing subsection, after the existing closing sentence (`Vessel structure: tree (one parent per part). Symmetry tools (mirror, radial). Surface-attach plus predefined attachment nodes.`) and immediately before the next subsection `### Failure modes`. The extension contains:

- `**Part categories at v1 (revised from ~30 to ~35-40):**` bolded lead-in.
- Statement that parts are the universal construction vocabulary across rockets, stations, bases, infrastructure, observatories, propulsion systems; different construction contexts determine what is buildable but use the same parts vocabulary.
- Eight category bullets with counts: Standard rocket parts (~13), Habitats and life support (~2), Surface base modules (~6), Orbital station modules (~5), Telescopes (~1), Industrial modules (~4), Communication relays (~2), Specialty propulsion infrastructure (~4).
- `**Construction contexts themselves are built.**` paragraph: home ground VAB is starting infrastructure; orbital VABs and surface VABs at other bodies are built by the player; each context has scope limits.
- Closing paragraph: procedural parts produce many variants from few categories; 35-40 categories produces potentially hundreds of distinct part variants.

The existing locked content (the ~30 base part categories list, the surface-attach commitment, etc.) is preserved verbatim.

### Edit 4: Section 3, append new `### Movie-moment mechanics` subsection at end of section

Inserted at the end of section 3 after `### Home observatory` (commit 009) and immediately before `## 4. Resources, bases, and logistics`. The new subsection contains:

- LOCKED commitment that six mechanics produce engineering-improvisation moments characteristic of the genre's most beloved scenes (Interstellar docking with spinning Endurance, Gravity using landing fuel for attitude, Martian counter-thrust against storm).
- Six bolded mechanic paragraphs: Differential thrust from multi-engine clusters, Thrust vectoring beyond designed range, Spin gravity through actual rotation, Communication delay with real consequences, Procedural failure modes, Real telemetry revealing problems.
- Closing discipline paragraph: make the physics real and legible; do not add gameplay layers on top of physics; no "docking skill check," no "engineering bonus." The physics is the gameplay.

### Edit 5: Section 3, append new `### Dual-use technology framework` subsection immediately after `### Movie-moment mechanics`

Inserted at the new end of section 3, after `### Movie-moment mechanics`. The new subsection contains:

- LOCKED commitment that destruction emerges from physics, not from dedicated combat systems.
- Discipline paragraph: tech tree has terraforming tools, propulsion infrastructure, energy systems; no "weapons" tab; mission planning has "deliver payload to body" with payload type "nuclear device," not "target enemy ship" with targeting reticle.
- Five dual-use technology bullets: Nuclear devices, High-power lasers, Mass drivers, Reactor technology, Plasma and ion technologies.
- `**Server rules govern destructive use against other agencies.**` paragraph: cooperative servers protect other agencies' assets; competitive servers permit physical-consequence engagement; self-destruction is unrestricted.
- `**Friction comes from physics.**` paragraph: nuking a base requires getting the device there; frying electronics with a laser requires the target in range with sufficient power; real engineering problems, not click-to-damage interactions.
- `**Endgame propulsion tiers:**` paragraph: chemical (starting), nuclear thermal, laser sail one-way (tier 2 interstellar), laser sail two-way (tier 3 interstellar, requires destination infrastructure), fusion (helium-3 fueled, advanced), antimatter (highest, post-v1 or very late). No FTL beyond physics-grounded options. Star Trek warp would undermine the time-dilation pillar and is rejected.

## Verification

### New-content anchored heading checks

1. `### Scaling discipline` anchored heading count is 1.
2. `### Crater primitives and bounded terrain modification` anchored heading count is 1.
3. `### Movie-moment mechanics` anchored heading count is 1.
4. `### Dual-use technology framework` anchored heading count is 1.

### Scaling discipline content

5. LOCKED commitment phrase `Scale what is necessary for gameplay tractability. Keep real what is necessary for physics consistency. Document where the seams are.` is present.
6. All three structural lead-ins present: `**What scales:**`, `**What does not scale:**`, `**Implementation seams:**`.
7. Literal phrase `1/8 default. Body parameters (radius, mass, gravity)` is present.

### Crater primitives content

8. LOCKED commitment phrase `The procgen pipeline includes crater placement as a Stage 11 feature layer applicable to all rocky bodies` is present.
9. Phrase `**Body fragmentation as event-driven state change.**` is present.
10. Phrase `Other terrain modification (trenches, roads, biome changes, water displacement, atmospheric effects from impacts) is deferred to post-v1` is present.
11. Within-commit forward reference to dual-use technology resolves: the phrase `dual-use technology framework's visible consequences` is present.

### Parts system extension content

12. Phrase `**Part categories at v1 (revised from ~30 to ~35-40):**` is present.
13. All eight category bullets present: `- Standard rocket parts (~13):`, `- Habitats and life support (~2):`, `- Surface base modules (~6):`, `- Orbital station modules (~5):`, `- Telescopes (~1):`, `- Industrial modules (~4):`, `- Communication relays (~2):`, `- Specialty propulsion infrastructure (~4):`.
14. Phrase `**Construction contexts themselves are built.**` is present.
15. Closing phrase `35-40 categories produces potentially hundreds of distinct part variants` is present.

### Movie-moment mechanics content

16. All six mechanic lead-ins present: `**Differential thrust from multi-engine clusters.**`, `**Thrust vectoring beyond designed range.**`, `**Spin gravity through actual rotation.**`, `**Communication delay with real consequences.**`, `**Procedural failure modes.**`, `**Real telemetry revealing problems.**`.
17. Discipline phrase `make the physics real and legible. Do not add gameplay layers on top of physics` is present.
18. Phrase `Interstellar docking with spinning Endurance, Gravity using landing fuel for attitude, Martian counter-thrust against storm` is present.

### Dual-use technology content

19. LOCKED commitment phrase `Destruction emerges from physics, not from dedicated combat systems` is present.
20. Phrase `No "weapons" tab` is present.
21. All five dual-use technology bullets present: `- **Nuclear devices.**`, `- **High-power lasers.**`, `- **Mass drivers.**`, `- **Reactor technology.**`, `- **Plasma and ion technologies.**`.
22. Phrase `**Server rules govern destructive use against other agencies.**` is present.
23. Phrase `**Friction comes from physics.**` is present.
24. Phrase `**Endgame propulsion tiers:**` is present.
25. Phrase `No FTL beyond physics-grounded options. Star Trek warp would undermine the time-dilation pillar and is rejected.` is present.

### Section ordering checks

26. Section 2 contains `### Scaling discipline` between `### Interstellar distances` and `### Target platforms`. Section 2 has 15 subsections total.
27. Section 3's last two subsections are `### Movie-moment mechanics` and `### Dual-use technology framework`, in that order, after `### Home observatory`. Section 3 has 24 subsections total.
28. Section 5's last subsection is `### Crater primitives and bounded terrain modification`, after `### Tuning is the hard part`. Section 5 has 12 subsections total.
29. Section 1 unchanged at 8 subsections. Section 6 unchanged at 5 subsections.

### Preserved-content anchors

30. Commit 010 content preserved: anchored heading counts of 1 each for `### In-media-res starting state`, `### Logistics-not-tech as primary progression gating`, `### Configurable starting conditions`. Literal `The game begins with humanity already 50-60 years into its space age`, `The rocket equation is the gate`, `**What the tech tree still gates.**` each present.
31. Commit 009 content preserved: anchored heading counts of 1 each for `### Physics-driven gameplay`, `### Home observatory`, `### Catalog as long-term meta-game`. Literal `**Discovery announcement transmissions.**` present.
32. Commit 008 content preserved: anchored heading counts of 1 each for `### Generation architecture`, `### The 14-stage pipeline (Layer 5)`, `### Hybrid pipeline-plus-grammar`, `### Planet variety tiers`, `### Feature-layer architecture`, `### The 90/9/1 anomaly distribution`, `### Galaxy scope`, `### Map view hierarchy`. Literal `90% resolve to interesting-but-understandable phenomena` present.
33. Commit 007 content preserved: anchored heading counts of 1 each for `### Detection ecosystem`, `### Platforms for detection`, `### Discovery progression — eight stages`, `### Detection mechanics`, `### Anomaly types worth seeding`.
34. Commit 006 content preserved: anchored heading counts of 1 each for `### Per-body physics parameter set`, `### Layered engagement framework`. Section 1 design pillars list still contains `- Discovery as gameplay, grounded in real astrophysics.` and `- Physics-grounded substrate.`
35. Commit 004a/b/c content preserved: anchored heading counts of 1 each for `### Multiplayer as shared universe`, `### Mode-portable designs and templates`, `### Tonal framing for game modes`, `### Minimal-tycoon, rich-progression positioning`, `### Interstellar distances`, `### Time-warp in single-player`, `### Director perspective`, `### EVA as temporary character control`, `### Transmissions and world communication`, `### Channel 16 broadcasts`, `### Home system evolves autonomously`. Literal `Save files are mode-locked at creation`, `1/8 real scale`, `within bounded limits that prevent passive accumulation`, `**Network-capacity rule.**`, `**Phase 0 deliverable: netcode contract.**` each present. Anchored headings for Phase 2 and Phase 7 amended titles each count 1.
36. Commit 005 content preserved: `### Phase exit criteria`, `**Mobile shipping note.**`, `FoundationPrimitives`.
37. Commit 003 content preserved: `**Agency-based observation sharing.**`, `detection-aggressiveness parameter`, `Vizzy scripts do not run during time-warp on Kepler-rails vessels`, `Wants to feel like an agency director.`
38. Commit 002 content preserved: `### Foundational architectural principles` anchored heading count 1; literal `50 km default`, `Tier 2 (laser sail one-way, flyby-only) arrival is a single analytic event`, `min(tick × warp_rate`, `sharp and symmetric`, `Authoritative state replication is the multiplayer model for PhysX-active vessels` each present. Five numbered principles in FAP.
39. Commit 001 content preserved across untouched sections: distinctive phrases per section (Pixar register, Jeb legend, Hill sphere, 20%/80%, KSP's first hour, Dwarf Fortress / RimWorld, physics fidelity toggle, vertical slice MVP, placeholder cube, siren song, suggested repo layout, institutional memory, doc-driven dev, kraken returning, pre-flight checklist, KSP 2's path, stale doc syndrome, Living document, Last comprehensive update). Each present exactly once.
40. Section 9 final-three-bullets adjacency: `Colony autonomy depth`, `Save format technology`, `Anomaly resolution UX` in order. Section 9 bullet count: 13.
41. All 14 numbered `## N.` section headings still present.
42. File line count is 1318 (post-010 was 1226 lines; this commit added 92 net lines).

If any of these checks fail, the commit has not landed correctly. Use the bash-via-Python escape hatch from `commits/README.md` for any repair.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md
git commit -F commits/011a_scaling_crater_parts_movie_dual_use.md
```
