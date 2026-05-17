# 018: Remote sensing for resources and first-image observatory moments

Three additive paragraphs to §4 (World behavior and discovery): atmospheric wavelength limits for ground observatories appended to `### Platforms for detection`; resource detection via spectroscopy appended to `### Detection mechanics`; first-image moments as designed memorable events appended to `### Home observatory`. All three connect existing system commitments to specific gameplay consequences.

The atmospheric-wavelength-limits paragraph makes the ground-versus-space telescope decision concrete by naming which wavelengths each can access. UV is blocked by ozone; X-ray and gamma-ray are fully absorbed; visible passes with turbulence and weather caveats; near-IR works but degrades through water vapor; mid- and far-IR require above-atmosphere or dry high-altitude sites. The structural payoff is that space-based observatories become qualitatively more capable, not just larger — they open wavelengths the ground cannot reach at any size.

The resource-detection-via-spectroscopy paragraph commits to the design that detection methods reveal not just that a body exists but estimates of what it contains. Atmospheric and surface spectroscopy constrain likely resources; thermal emission constrains subsurface heat; radio emission constrains magnetic field and interior structure. Estimates are probability distributions that refine as observation tiers advance. The structural payoff is that home observatory infrastructure becomes mission-planning relevant beyond pure discovery — players identify likely resource-rich targets from remote sensing, then commit missions to confirm and exploit.

The first-image-moments paragraph commits to first-light events as designed memorable events. When a space-based observatory first comes online, the first image gets full-screen presentation with the catalog entry it updates. The transmission is high-priority. The commitment repeats for each major instrument unlock — first space telescope returning a high-resolution planet image, first infrared array finding a hidden cool object, first radio array picking up a previously-unknown signal. Each is "the reach" dramatic shape (`### Dramatic shapes the game produces` in §1) cashed out as a specific event.

## Scope

- `docs/CONSTRAINTS.md` — three appends to §4 subsections:
  - Append `**Wavelength-specific atmospheric limits for ground observatories.**` paragraph to `### Platforms for detection`
  - Append `**Resource detection via spectroscopy.**` paragraph to `### Detection mechanics`
  - Append `**First-image moments are designed memorable events.**` paragraph to `### Home observatory`

## Rationale

The §4 subsections this commit extends were established by commits 007 (detection ecosystem) and 009 (home observatory). Commits 013 and 014 added related content (sensors as vessel components, observation as structured activity, wavelengths and filters as concrete gameplay, observation results as interpretable data). What was missing was three specific commitments that the design implies but had not yet stated:

The atmospheric-limits commitment matters because the home planet's atmosphere is the boundary between ground and space observation. Without naming what each side can access, players cannot make informed decisions about observatory infrastructure. The commitment makes the space-based-observatory progression concrete: it is not "larger telescopes," it is "wavelengths the ground cannot access at all."

The resource-detection commitment matters because remote sensing is what bridges discovery and mission planning. The commits-013-and-014 observation content focused on detection of bodies and anomalies; this commit extends the same observation infrastructure to resource estimation, with explicit framing that estimates are probability distributions that refine through closer observation. The cross-reference to `### Home observatory` makes the observatory the locus of mission-relevant pre-mission information gathering.

The first-image-moments commitment matters because the design's emotional architecture depends on specific events carrying weight. Routine observation results accumulate into the catalog without ceremony; first-light events are different. This commit names them and commits to their full-screen presentation, dedicated transmission, and connection to the "the reach" dramatic shape from §1.

All three are light-ceremony additions. They do not replace locked content. They do not introduce new mechanics. They make existing system commitments concrete in ways that produce engineering decisions or designed emotional beats.

## Changes

Three atomic appends executed via bash-via-Python in a single operation:

1. Read the file. Capture three boundary anchors (one per target subsection) representing the existing closing content immediately before the new paragraph. Verify each appears exactly once pre-write.
2. Locate each target subsection by line-anchored heading regex. Identify the body span (heading line through start of next `### ` or `## ` heading). Strip trailing whitespace; append the new paragraph with `\n\n` separator; restore trailing `\n\n` before next subsection.
3. Verify all three boundary anchors still appear exactly once in the constructed text. Verify three new-content phrase anchors (`**Wavelength-specific atmospheric limits for ground observatories.**`, `**Resource detection via spectroscopy.**`, `**First-image moments are designed memorable events.**`) appear exactly once.
4. Atomic write via `.recovery` + `os.replace`.

File grew from 1618 lines / 175,418 bytes to 1632 lines / 178,466 bytes (+14 lines, +3,048 bytes).

### Edit 1: Append `**Wavelength-specific atmospheric limits for ground observatories.**` to `### Platforms for detection`

Appended after the existing `**Ground-based vehicles on other bodies.**` paragraph (the closing paragraph of the commit 007 subsection). Names six wavelength categories and their ground accessibility: visible (passes, atmospheric turbulence and weather limited), near-IR (passes with water-vapor degradation), mid- and far-IR (require above-atmosphere or dry high-altitude), UV (blocked by ozone, impossible from ground), X-ray and gamma-ray (fully absorbed, orbital required), radio (mostly passes, frequency-band-specific ionospheric and water-vapor effects). Closing commitment: space-based observatories are qualitatively more capable, not just larger.

### Edit 2: Append `**Resource detection via spectroscopy.**` to `### Detection mechanics`

Appended after the existing `**Old mysteries can reactivate.**` paragraph. Three claims: detection methods reveal body contents not just existence (atmospheric spectroscopy → atmospheric composition → resource constraints; surface spectroscopy → surface composition → accessible resources; thermal emission → subsurface heat → volcanic/tidal-heating activity; radio emission → magnetic field and interior structure). Estimates from remote sensing are probability distributions that refine as detection methods and closer observation tiers (Stage 5+, commit 007) accumulate. Cross-reference to `### Home observatory` as the infrastructure that makes remote-sensing-for-mission-planning a real player verb.

### Edit 3: Append `**First-image moments are designed memorable events.**` to `### Home observatory`

Appended after the existing closing paragraph of the home-observatory subsection. Commits to first-light events as designed memorable events: high-priority transmission, full-screen image presentation with catalog entry update, dedicated player beat. The commitment repeats across major instrument unlocks. Cross-reference to `### Dramatic shapes the game produces` in §1: each first-image moment is "the reach" dramatic shape cashed out as a specific event.

## Verification

64 checks, all passing on first run.

### New content present (9 checks)

- Each of the three new-paragraph headers `**Wavelength-specific atmospheric limits for ground observatories.**`, `**Resource detection via spectroscopy.**`, `**First-image moments are designed memorable events.**` present exactly once
- Edit 1 contains `Ultraviolet is largely absorbed by ozone` and `X-ray and gamma-ray are fully absorbed by the atmosphere`
- Edit 2 contains `Resource estimates from remote sensing are probability distributions` and the cross-reference `(Stage 5+, commit 007)`
- Edit 3 contains the transmission template `First-light from [observatory name]` and the dramatic-shape cross-reference `"the reach" dramatic shape`

### Boundary verbatim-with-context anchors preserved (6 checks)

- `**Ground-based vehicles on other bodies.** Rovers, drills, atmospheric processors. Site selection matters; mobility matters.` (Edit 1 boundary) present exactly once
- `**Old mysteries can reactivate.** A signal detected years ago and dismissed might be significant with new techniques. When a new instrument tier unlocks, the catalog can re-analyze old observations and surface new findings.` (Edit 2 boundary) present exactly once
- `The home observatory is the mid-to-late-game system. By Phase 7, when the galaxy is open to interstellar exploration, the observatory is what the player has been building for hours. Their catalog is what their observatory has produced.` (Edit 3 boundary) present exactly once
- Ordering check for each edit: existing tail appears before new paragraph (heading < existing tail < new paragraph order verified)

### Structural counts (15 checks)

H3 subsection counts per section: §1=13, §2=15, §3=16, §4=17, §5=6, §6=12, §7=5, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0. All unchanged from commit 017 (this commit adds no new subsections, only extends existing ones).

### Commit 017 structural anchors preserved (4 checks)

- `^## 3\. Gameplay mechanics$` exactly once
- `^## 4\. World behavior and discovery$` exactly once
- `^## 15\. Document status$` exactly once
- No `^## 3\. Gameplay systems$`

### Commit 014 damage-repair anchor preserved (2 checks)

- No line matches `^### Channel 16 broadcasts\` below\)\.$`
- Restored inline reference `(see \`### Channel 16 broadcasts\` below).` present exactly once

### Commit 015 verbatim-with-context anchors preserved (5 checks)

All five scientist-assignment cross-references from commit 015 remain verbatim with surrounding context.

### Commit 017 cross-reference updates preserved (17 checks)

All seventeen cross-references updated by commit 017 remain at their post-017 values:

- `see section 7's \`### First-hour experience\``
- `The multiplayer architecture in section 4 (\`### Multiplayer as shared universe\`)`
- `The artifacts seed the early research-as-logistics gameplay (see section 5's \`### Research as logistics-driven question-answering\`)`
- `the development build phases in section 9`
- `alongside the broader difficulty toggle system from section 8`
- `coordinated time multiplier (see section 4 \`### Multiplayer as shared universe\`)`
- `subject to the bounded-autonomous-evolution rules from section 4's \`### Home system evolves autonomously\` subsection`
- `see section 3's "Mode-portable designs and templates" subsection`
- `In multiplayer (see section 4 \`### Multiplayer as shared universe\`)`
- `see section 10's \`Mobile shipping\` open question`
- `The 90/9/1 distribution rule (section 6) governs how often each resolves to interesting outcomes.`
- `**Anomalies as research questions.** Each detected anomaly opens a research question (see section 5's \`### Research as logistics-driven question-answering\`)`
- `per the bounded-autonomous-evolution rules in section 4's \`### Home system evolves autonomously\` subsection`
- `observation results (commit 014's section 4 additions)`
- `(off / abstract / full / brutal — see section 5's \`### Life support model\` for the categorical model)`
- `"beautiful screenshots, broken physics" failure named in section 14.`
- `per-system companion docs (see section 12 below)`

### Cross-section preserved content (6 checks)

Standard battery: `Engineering as the verb` in §1; `Floating origin shift threshold: 50 km default` in §2; `### Research as logistics-driven question-answering` exactly once; `**Operational chatter.**`, `**Crew are physically located on vessels.**`, `**Crew as a finite resource.**` from commits 014, 013, 016 preserved.

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/018_remote_sensing_and_observatory_moments.md
git commit -F commits/018_remote_sensing_and_observatory_moments.md
```
