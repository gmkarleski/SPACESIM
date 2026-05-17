# 025: Atmospheric flight pillar + Juno-fidelity aerodynamics + procedural fuselage/wing builders + Phase 5 weight increase

Lock atmospheric flight gameplay as a first-class design pillar joining the existing nine pillars in §1. Lock Juno-fidelity procedural aerodynamics (procedural aero surfaces with calculated lift/drag from surface geometry) with supersonic and hypersonic flight regimes in scope and re-entry heating modeled thermally. Insert new subsection `### Atmospheric flight and spaceplane gameplay` in §3 (Gameplay mechanics) after `### Flight computers and unmanned operation` and before `### Crew and characters`. Extend §3 `### Parts and vessel construction` with procedural fuselage and wing builders shipping at Phase 5, plus an explicit phase-progression-of-parts-quality commitment and an explicit v1.1-post-launch deferral of painting/texturing and ambitious mesh-composition features. Update §9 Phase 3, Phase 4, Phase 5, and Phase 8 deliverables to reflect the atmospheric-flight and parts-progression scope. Increase Phase 5 weight from 3 to 4. Resolve the §10 `Aerodynamic model (Phase 3)` open question as a RESOLVED marker referencing this commit, pending future migration to `docs/DECISIONS.md` once that operational scaffolding exists.

Atmospheric flight is now first-class gameplay. Spaceplanes are a real vessel category, not a token nod. The aerodynamic model is Juno-fidelity — procedural aero surfaces with lift/drag/moment calculated from surface geometry, angle of attack, and local airflow conditions. The flight regimes covered are subsonic, transonic, supersonic, and hypersonic, each with characteristic aerodynamic behavior (shock formation at transonic boundary, wave drag dominance in supersonic, thermal management dominance in hypersonic). Re-entry heating is in scope with thermal modeling of heat shields, re-entry corridor width depending on vehicle thermal budget, blunt-body versus sharp geometry trade-offs, and ablative failure modes. Spaceplane gameplay combines aerodynamic lift and rocket propulsion — takeoff from runways, climb as aircraft, transition to rocket at altitude, orbit via airbreathing-plus-rocket ascent profiles, glide re-entry, runway landing. Atmospheric science platforms cover the aircraft-on-other-bodies case where bodies with thick atmospheres support sustained-altitude science missions.

The procedural fuselage and wing builders ship at Phase 5. The fuselage builder lets the player specify cross-section, length, tapering, and connection points; the wing builder lets the player specify root chord, tip chord, sweep, dihedral, taper, and aerodynamic surface configuration. Mass, drag, lift, and structural properties calculate from the geometry. Together, the builders support serious spaceplane and atmospheric aircraft design where stock part libraries cannot capture the design space. The parts system develops in four progression stages: Phase 1-3 ships the simplest library-parts set sufficient for testing the flight model, vehicle assembly, and the Phase 1/3 validation milestones; Phase 4 adds procgen variation per part category (visual variants with consistent functional properties); Phase 5 adds the procedural fuselage and wing builders; v1.1 (post-launch) adds painting and texturing of parts and built vessels plus ambitious mesh-composition features. The phase progression keeps Phases 1-3 focused on functional correctness; defers visual richness to Phase 4 and procedural authoring to Phase 5; defers ambitious visual-polish features to post-launch.

The Phase 5 weight increase from 3 to 4 reflects the added builder scope. Phase 4's weight stays at 2-4 (variable) because the 2-4 range was already designed to absorb visual scope variation; procgen variation per part category fits within the visual scope already named for Phase 4.

The §10 `Aerodynamic model (Phase 3)` open question — previously listed as `drag-cube approximation vs raycast-based vs FAR-style. Lean raycast-based as middle ground.` — is resolved in favor of Juno-fidelity procedural aero surfaces. The middle-ground raycast-based lean was the previous default; the locked choice is the upper end of that bracket. The open-question bullet is replaced with a `RESOLVED — see commit 025` marker pending migration to `docs/DECISIONS.md` once operational scaffolding creates that file. The marker pattern matches the commit 003 / commit 005 RESOLVED-marker discipline; a future cleanup commit removes the marker when DECISIONS.md holds the formal entry.

§3 grows from 17 to 18 subsections (atmospheric flight added).

## Scope

- `docs/CONSTRAINTS.md` — eight edits across §1, §3, §9, §10:
  - §1 `### Design pillars`: insert new pillar bullet after the "Physics-grounded substrate" pillar
  - §3: insert new `### Atmospheric flight and spaceplane gameplay` after `### Flight computers and unmanned operation` and before `### Crew and characters`
  - §3 `### Parts and vessel construction`: append `**Procedural fuselage and wing builders.**` + `**Parts visual progression across phases.**` + `**Post-v1.1 part-system features.**` paragraph blocks
  - §9 `### Phase 3 — Flight integration (weight 2)`: replace deliverables paragraph to reference Juno-fidelity atmospheric flight
  - §9 `### Phase 4 — Visuals (weight 2-4)`: replace deliverables paragraph to add procgen variation per part category
  - §9 `### Phase 5 — Game systems`: heading weight `(weight 3)` → `(weight 4)`; deliverables paragraph adds procedural fuselage + wing builders
  - §9 `### Phase 8 — Polish, content, expansion (weight 4+)`: append v1.1 post-launch annotation
  - §10: replace `Aerodynamic model (Phase 3)` open-question bullet with RESOLVED marker

## Rationale

Atmospheric flight has been an implicit assumption across many commits: commit 002's three-mode physics names PhysX-active as the regime "within atmosphere, near surface, under thrust"; commit 011a's scaling discipline commits to atmospheres proportionally deeper than real to "make atmospheric flight gameplay-relevant"; commit 024's flight computers list "Hold altitude (atmospheric flight)" as a maneuvering primitive; commit 011a's parts list has wings/control surfaces. What was missing was an explicit pillar commitment naming atmospheric flight as first-class gameplay, plus the aerodynamic-fidelity commitment, plus the parts-system commitments that make spaceplane design tractable.

The pillar addition is small but consequential. The existing nine pillars (Engineering as the verb; Discovery as meta-game; Discovery as gameplay grounded in real astrophysics; Physics-grounded substrate; Time as a real dimension; Automation; Selective realism; Asynchronous progression; Layered depth) commit to the design's broad shape. Adding "Atmospheric flight as first-class gameplay" tells future readers — and future commits — that spaceplane gameplay is not a feature creep candidate but a load-bearing design dimension. The pillar lands after "Physics-grounded substrate" because atmospheric flight is the substrate principle applied: real (simplified) physics producing engineering decisions in a regime that many space sims skip past.

The Juno-fidelity aerodynamics commitment matters because the aerodynamic-model choice has been an open question since commit 002. Drag-cube approximation was the cheapest option; raycast-based was the middle-ground default; FAR-style was the upper-fidelity end. The commit-025 lock chooses Juno: New Origins-style procedural aero surfaces with calculated lift/drag from surface geometry. This is upper-end fidelity, not middle-ground — and it matters for the rest of the design because the parts-system commitments (procedural fuselage and wing builders shipping Phase 5) depend on having an aero model that calculates from geometry rather than from pre-tagged drag coefficients. Juno-fidelity is the only model that makes the procedural builders meaningful — a wing the player designs has the aerodynamics its geometry implies.

The supersonic and hypersonic scope matters because spaceplane ascent crosses both regimes. A vehicle that flies as a subsonic aircraft, accelerates through transonic, climbs in supersonic to altitude, and transitions to rocket power in hypersonic crosses every regime named. The model reproduces qualitative phenomena (shock formation, wave drag rise, sonic boom, plasma sheath at re-entry) rather than research-grade quantitative fidelity. The "qualitative reproduction" framing is the Selective Realism pillar applied — realism that creates decisions is good; realism that creates tedious calibration is not.

The re-entry heating scope matters because re-entry corridor design is a real engineering problem with real consequences. Too steep an angle and the craft burns; too shallow and it skips off the atmosphere. Heat shields are real parts with thermal capacity, ablation behavior, and failure modes. This connects directly to commit 019's crew critical condition mechanics (thermal failure was named there) and commit 023's forgiving restart philosophy (players retry mis-judged re-entry corridors via quicksave/reload). The Phase 4 re-entry plasma visuals already in the build order get their underlying model named here.

The procedural fuselage and wing builders are the parts-system commitment that makes the atmospheric flight pillar implementable. Stock libraries can't capture the spaceplane design space — every spaceplane is a different shape because every mission profile suggests a different aerodynamic configuration. The fuselage builder produces custom hulls from parameters; the wing builder produces custom wings from parameters. Mass, drag, lift, and structural properties calculate from geometry. This is the same procedural-parts principle commit 011a established for the parts vocabulary, extended to two specialized cases that need their own dedicated builders rather than the generic parametric variation that commit 011a's existing categories cover.

The parts visual progression across phases is a meta-commitment about how the parts system develops. Phase 1-3 ships the simplest library-parts set — sufficient for functional testing, sufficient for the Phase 1 validation milestone (placeholder cube to orbit) and the Phase 3 validation milestone (built rocket to Mun landing). Phase 4 adds procgen variation per part category — visual variants with consistent functional properties. Phase 5 adds the procedural fuselage and wing builders plus any remaining specialized builders. The phase progression is design strategy applied: functional correctness first, visual richness second, procedural authoring third. The commitment is structural — it says "don't try to ship the procedural builders before Phase 5" and "don't try to ship visual variants before Phase 4" — both of which are real anti-patterns that would compromise the validation milestones.

The Phase 5 weight increase from 3 to 4 reflects the added builder scope. The previously-named Phase 5 deliverables (map view with maneuver nodes, navball, tech tree, mission/observation/anomaly systems, tutorials, Vizzy scripting, difficulty toggle system, summary screens and alert system) plus the new procedural fuselage and wing builders is significantly more scope than Phase 5 carried before. The weight unit increase is the explicit budget update.

The v1.1-post-launch annotation matters for the same reason the parts visual progression matters — it names what's deferred and what's not. Painting and texturing of parts and built vessels are visual-polish features that the v1 parts system supports architecturally but does not ship at v1 launch. Ambitious mesh-composition features (constructing custom-shaped parts from primitive operations, more elaborate procedural builders for other categories beyond fuselage and wings) are also deferred. The deferral protects the v1 ship date. The architectural support keeps post-launch capability expansion possible. The Phase 8 v1.1 annotation makes the deferral explicit so future commits and future implementation work don't drift into shipping these at v1.

The §10 RESOLVED marker pattern follows the commit 003 / commit 005 precedent. Commit 003 introduced a RESOLVED marker on the crew abstraction open question; commit 005 cleaned up that marker as part of the minor-cleanups commit. The aerodynamic-model RESOLVED marker created here will follow the same lifecycle — a future cleanup commit removes it once `docs/DECISIONS.md` exists and holds the formal decision entry. The marker preserves the resolution in-doc until DECISIONS.md is ready to receive it.

## Changes

A single atomic write via bash-via-Python with eight edits applied in sequence to a single in-memory text buffer. Sequence:

1. Read file. Capture nine boundary anchors (B1-B9). Verify each appears exactly once pre-write. B7 and B8 are anchors-on-content-to-be-replaced; their presence confirms the replacement target exists exactly once. B9 is an anchor-on-content-not-touched (commit 011a scaling discipline atmospheric-depth line); verified preserved post-write.
2. **Edit 1:** Insert new pillar bullet `- Atmospheric flight as first-class gameplay...` into §1 design pillars list immediately after the "Physics-grounded substrate" bullet via exact-string replacement (replace `phys_pillar` with `phys_pillar + "\n" + new_pillar`).
3. **Edit 2:** Insert new `### Atmospheric flight and spaceplane gameplay` subsection (~3.2KB, 9 bold-headed paragraph blocks including cross-references to §1, §3, §6, §8) immediately before the line-anchored `### Crew and characters` heading.
4. **Edit 3:** Append three paragraph blocks (`**Procedural fuselage and wing builders.**`, `**Parts visual progression across phases.**`, `**Post-v1.1 part-system features.**`) to end of `### Parts and vessel construction` body via the standard subsection-body-append pattern.
5. **Edit 4:** Replace §9 Phase 3 deliverables paragraph to incorporate the Juno-fidelity reference into the existing "Atmospheric flight model" mention.
6. **Edit 5:** Replace §9 Phase 4 deliverables paragraph to add the procgen-variation-per-part-category sentence after the existing visual-deliverables list.
7. **Edit 6:** Replace §9 Phase 5 heading `(weight 3)` → `(weight 4)`. Replace §9 Phase 5 deliverables paragraph to add the procedural-fuselage-and-wing-builders sentence and the weight-increase explanation.
8. **Edit 7:** Replace §9 Phase 8 deliverables paragraph to append the v1.1-post-launch annotation paragraph.
9. **Edit 8:** Replace §10 `Aerodynamic model (Phase 3)` open-question bullet with the RESOLVED marker referencing this commit and naming the future DECISIONS.md migration plan.
10. Verify all preserved boundary anchors still present. Verify deliberately-changed boundaries (B6 old-form absent, new-form present; B7 weight-3 absent, weight-4 present; B8 old bullet absent, RESOLVED marker present). Verify all new bold-header phrases present exactly once. Verify line-anchored heading count for new §3 subsection. Atomic write via `.recovery` + `os.replace`.

File grew from 1927 lines / 218,123 bytes to 1954 lines / 226,933 bytes (+27 lines, +8,810 bytes).

## Verification

102 checks, all passing on first run. Seven groups:

### A. New content present (32 checks)

- New pillar: `- Atmospheric flight as first-class gameplay.` exactly once; the four key commitments (Juno-fidelity procedural aerodynamics with calculated lift/drag; supersonic and hypersonic regimes in scope; re-entry heating modeled thermally; spaceplane as first-class vessel category)
- New §3 subsection: line-anchored `^### Atmospheric flight and spaceplane gameplay$` exactly once; LOCKED opener; seven bold-header paragraphs (Aerodynamic fidelity; Flight regimes; Re-entry heating; Spaceplane gameplay; Atmospheric science platforms; Cross-references to vessel construction and parts; Atmospheric flight at non-home bodies); distinctive content (`Juno: New Origins fidelity`; `shock formation at transonic boundary`; `too steep and the craft burns; too shallow and it skips off the atmosphere`; `airbreathing-plus-rocket ascent profiles`)
- §3 Parts and vessel construction extension: three new bold-header paragraphs (`**Procedural fuselage and wing builders.**`, `**Parts visual progression across phases.**`, `**Post-v1.1 part-system features.**`); fuselage builder geometry commitment (cross-section, length, tapering, connection points); wing builder geometry commitment (root chord, tip chord, sweep, dihedral, taper); phase-progression-Phase-1-3 commitment; v1.1 painting/texturing deferral; v1.1 ambitious mesh-composition features deferral
- §9 Phase deliverables: Phase 3 references Juno-fidelity from §3; Phase 4 names procgen variation per part category; Phase 5 weight increased from 3 to 4 (heading line-anchored exactly once); Phase 5 deliverables include procedural fuselage and wing builders; Phase 8 v1.1 annotation present
- §10 resolution: RESOLVED marker present; references the new §3 subsection; names DECISIONS.md migration plan

### B. Boundary anchors (preserved or correctly replaced) (12 checks)

- B1 (Layered depth pillar), B2 (Mobile shipping), B3 (Flight computers closing), B4 (Crew and characters opener), B5 (Supplies-survive-rougher closing), B9 (§2 scaling discipline atmospheric-depth line) all preserved exactly once
- B6 (Phase 3 deliverables) deliberately changed: old `Spawning built vessels at launch sites. Atmospheric flight model. Engine thrust.` absent; new `Spawning built vessels at launch sites. Atmospheric flight model implemented at the resolution Phase 3 testing requires` present
- B7 (Phase 5 weight 3 heading) deliberately changed: old `### Phase 5 — Game systems (weight 3)` line-anchored count = 0; new `### Phase 5 — Game systems (weight 4)` line-anchored count = 1
- B8 (§10 aerodynamic-model bullet) deliberately changed: old `drag-cube approximation vs raycast-based vs FAR-style. Lean raycast-based as middle ground.` absent

### C. Structural counts (15 checks)

- §3 h3 count = 18 (was 17; +1 from Atmospheric flight subsection)
- §1=15, §2=15, §4=17, §5=6, §6=13, §7=7, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0 (all unchanged from commit 024)

### D. Ordering (3 checks)

- §3 ordering: `### Flight computers and unmanned operation` < `### Atmospheric flight and spaceplane gameplay` < `### Crew and characters`
- §1 pillar ordering: `- Physics-grounded substrate.` < `- Atmospheric flight as first-class gameplay.` < `- Time as a real dimension.`
- §3 Edit 3 ordering: commit 020 `**Supplies survive rougher impact than crew.**` < new `**Procedural fuselage and wing builders.**`

### E. Section headings unchanged (10 checks)

`## 1. Vision`, `## 2. Foundation (LOCKED unless noted)`, `## 3. Gameplay mechanics`, `## 4. World behavior and discovery`, `## 5. Resources, bases, and logistics`, `## 6. Procedural generation`, `## 7. UI and information density`, `## 9. Build order`, `## 10. Open questions to resolve`, `## 15. Document status` each line-anchored exactly once.

### F. Prior-commit anchors preserved (28 checks)

- Commit 014 damage-repair: no malformed line, restored inline reference present exactly once
- Commit 015 five verbatim-with-context anchors preserved
- Commit 017 cross-reference sample (six representative cross-references) preserved
- Commits 018-024 anchors: commit 018 first-image moments; commit 019 three-category framing preserved verbatim (`Three-category architecture for crew sustenance and exposure`; `Radiation dose, zero-G exposure, mission stress`); commit 020 storage modules universal; commit 021 four-bodies-intensive-hand-craft + Saturn rings galaxy-wide procgen parenthetical; commit 022 strip mining; commit 023 `^### Mission Control as primary UI$`, `^### Forgiving restart philosophy$`; commit 024 `^### Flight computers and unmanned operation$`, `**Strategic depth in supply route design.**`
- Cross-section preserved: `Engineering as the verb` (§1); `Floating origin shift threshold: 50 km default` (§2); `**Crew are physically located on vessels.**`; pre-existing Phase 4 `re-entry plasma` mention preserved alongside the new procgen-variation-per-part-category addition

### G. Resolution-marker validity (2 checks)

- §10 RESOLVED marker references commit 025 explicitly
- §10 RESOLVED marker names the future migration plan (`pending migration to docs/DECISIONS.md`)

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/025_atmospheric_flight_pillar.md
git commit -F commits/025_atmospheric_flight_pillar.md
```
