# 024: Flight computers, advanced maneuvering primitives, incubation modules, satellite repair, supply line depth

Add the flight computer as a discrete part category enabling unmanned vessel operation. Insert new subsection `### Flight computers and unmanned operation` in §3 (Gameplay mechanics) after `### Automation and scripting`. The new subsection commits to the manned-vs-unmanned trade-off framework, nine advanced maneuvering primitives that Vizzy can invoke, incubation modules as the cheapest interstellar-tier mission type, and a two-layer incubation success model (engineering-affected failure probabilities plus fixed per-design success multiplier). Extend `### Mission planning as structured persistent activity` (§3) with satellite-and-infrastructure repair missions as a frequent mission type. Extend `### Supply lines` (§5) with strategic-depth-in-supply-route-design (six emergent strategy patterns), supply-unit-measurement per resource type, and auto-route-complexity acknowledgment.

The flight computer commitment matters because the design has been operating with an implicit assumption that unmanned operation works. This commit makes the part discrete and the trade-offs explicit. A flight computer is a real part with mass, cost, and design implications — not an invisible universal capability. Vessels without one require crew to operate beyond minimum stability (basic auto-hold-attitude is always available; advanced maneuvers require either the part or direct crew control). This produces meaningful build decisions: a small crewed probe might omit the flight computer to save mass; a large station's flight computer is essential; an unmanned probe requires it absolutely.

The flight computer is not artificial intelligence in any deep sense. It is a guidance and automation system roughly equivalent to the autopilot computer in current spacecraft — capable of executing pre-planned procedures and pre-flown routes but not improvising novel solutions. This framing matters because it forecloses the design drift toward AI-as-magic-capability while preserving the practical capability players need.

The manned-vs-unmanned table commits to seven specific trade-offs: G-force tolerance (crew-tolerance varies per person; unmanned 2-3× crew); life support (required with mass cost for manned, none for unmanned); return vehicle (usually required for manned, optional one-way feasible for unmanned); adaptability (improvisation possible for manned, pre-planned procedures only for unmanned); mission duration (bounded by life support and crew endurance for manned, bounded by hardware degradation only for unmanned); cost (higher for manned; lower for unmanned); risk weight (crew lives vs hardware cost). Players choose per mission based on what's needed. Discovery missions to nearby targets often manned (improvisation valuable). Supply routes often unmanned (pre-planned, repetitive). Long-duration interstellar missions usually unmanned (life support over decades is intractable).

The nine advanced maneuvering primitives — match velocity with target vessel, match orbit with target body or vessel, approach to specified range, station-keeping at specified relative position, match rotation with target, dock at specified port, hold attitude, burn to target orbital parameters, hold altitude — are the vocabulary Vizzy scripts compose into complex behavior. The primitives are real player-facing operations, not internal flight-computer abstractions.

Incubation modules are the cheapest interstellar-tier mission. A colony seed package combined with flight computer guidance, accelerated by laser sail, cruises for decades or centuries to a target star system. On arrival, the incubation module attempts to deploy colony infrastructure autonomously. The two-layer success model is the design's commitment that mission success is partly engineering and partly fixed: specific failure modes (signal loss during cruise; navigation drift; deceleration system failure; autonomous deployment errors) scale with specific engineering decisions the player makes (redundant comms; mid-course correction reliability; fuel reserve margin; deployment system complexity and pre-flight testing); a fixed per-design success multiplier modifies all probabilities and varies modestly across incubator design generations as the player's research program improves the technology. The model gives engineering-attentive players significantly better odds than minimum-mass-builders while preserving baseline difficulty.

Satellite repair as a mission type matters because infrastructure failures over game-years are part of operational reality, not special events. A satellite's instrument module degrades. A base's life support module malfunctions. A deep-space probe loses comms. Repair missions dispatch crew or flight-computer-equipped repair craft to the failure location with replacement modules or supplies. Successful repair restores the asset; failed repair (insufficient supplies, inappropriate replacement parts, crew loss en route) leaves the asset offline pending another attempt. Players who maintain a substantial orbital infrastructure manage an ongoing repair schedule.

The supply lines extensions name design patterns that emerge under commit 011b's vessels-on-schedules model. Strategic depth — return-trip cargo, multi-stop routes, gravity-assist trajectories, refueling cascades, synchronized launches, specialized cargo handlers — are emergent strategies, not designer-authored mission types. Supply unit measurement per resource type commits to tons for bulk materials, kg for high-value or low-mass cargo, categorical inventory for research samples, and kg/volume for atmospheric gases. Auto-route complexity acknowledgment is the honest hedge: some auto-supply routes are very difficult to set up because planetary motion makes optimization hard, but easier-but-slower alternatives always exist so the player can always find a workable route.

§3 grows from 16 to 17 subsections (flight computer added).

## Scope

- `docs/CONSTRAINTS.md` — three edits:
  - §3: insert new `### Flight computers and unmanned operation` after `### Automation and scripting` and before `### Crew and characters`
  - §3: append `**Satellite and infrastructure repair missions.**` paragraph block to `### Mission planning as structured persistent activity`
  - §5: append three paragraph blocks to `### Supply lines` — strategic depth in supply route design (six bullets), supply unit measurement per resource type (four bullets), auto-route complexity acknowledgment

## Rationale

Commits 002 and 011a established the parts vocabulary and the basic vessel-construction commitment. Commit 011b established supply lines as scheduled real vessels. Commit 013 established mission planning as the persistent unit of player work. Commit 019 established crew tolerances. Commit 023 established Mission Control with the simulator. The implicit assumption running through all of these was that unmanned operation works: supply routes run on autopilot, probes scan and report autonomously, incubation missions cruise for decades. Commit 024 makes that assumption explicit and gives it engineering substance.

The flight computer is the discrete part that enables it. The commitment that this is a real part with mass and cost — not an invisible universal capability — produces real build decisions. A small probe might omit the flight computer to save mass if it has crew; a large station's flight computer is essential because hand-flying a large vessel is impractical; an unmanned probe requires it absolutely. This is the same engineering-decisions-from-parts pattern that commit 011a established for the parts system generally.

The framing that flight computers are not AI matters for design discipline. AI-as-capability invites design drift toward magical autonomy. The autopilot framing — capable of executing pre-planned procedures and pre-flown routes but not improvising novel solutions — is bounded. Players who want a vessel to do something the flight computer can't (improvise a novel solution to an unexpected situation) need crew. This produces the meaningful difference between manned and unmanned missions that the design has been operating with.

The nine maneuvering primitives are the vocabulary Vizzy composes against. Match velocity, match orbit, approach to specified range, station-keeping, match rotation, dock, hold attitude, burn to orbital parameters, hold altitude. Each is a player-facing operation, not an internal abstraction. Vizzy scripts (`### Automation and scripting` in §3) invoke these primitives as building blocks. The primitive vocabulary is finite and discoverable; the compositions are infinite and player-authored.

Incubation modules deserve their own block because they are the cheapest interstellar-tier mission. No crew, no life support during cruise, no return vehicle. But they commit the player to the destination for decades before knowing if it worked. The two-layer success model — failure-mode probabilities the player affects through engineering plus a fixed per-design multiplier — gives engineering decisions real weight without making mission success a quality-dial gameplay element (the game has no monetary system to make "quality" a meaningful axis). The multiplier varies modestly across incubator design generations as the player's research program develops better incubator technology, connecting incubation success to the research framing from commit 015.

The satellite repair mission type matters because it is frequent rather than special. Satellites accumulate failures over game-years. Players who maintain substantial orbital infrastructure manage an ongoing repair schedule. This is operational reality, not a designed event. The repair-mission paragraph in §3 mission planning establishes that repair is a player verb at the mission level — dispatching crew or a flight-computer-equipped repair craft, success or failure with consequences — with the same structure as other mission types.

The supply lines extensions extend commit 011b's vessels-on-schedules model without changing it. Strategic depth names six emergent strategies that the existing model already supports: return-trip cargo (supply ships return loaded), multi-stop routes (single craft visits multiple bases), gravity-assist trajectories (longer transit for fuel efficiency), refueling cascades (intermediate stations enable longer-range routes), synchronized launches (split at staging points), specialized cargo handlers (parts vocabulary optimized for specific cargo). These are emergent, not designer-authored — the vessels-on-schedules model supports them; players who optimize their logistics discover them. Supply unit measurement makes the measurement framework concrete: tons for bulk materials, kg for high-value or low-mass, categorical inventory for research samples, kg/volume for atmospheric gases. Auto-route complexity acknowledgment is the honest hedge about the model's hard edge: planetary motion makes some auto-routes very difficult to optimize, but alternative paths always exist so players are not blocked.

## Changes

A single atomic write via bash-via-Python with three edits applied in sequence to a single in-memory text buffer:

1. Read file. Capture six boundary anchors and verify each appears exactly once pre-write.
2. **Edit 1:** Locate the line-anchored `### Crew and characters` heading position. Insert the new flight-computer subsection plus `\n\n` immediately before it. The previous subsection (`### Automation and scripting`) and the boundary anchor B1 (Mobile shipping note paragraph) sit immediately before the insertion point; the placement target B2 (Crew and characters LOCKED opener) sits immediately after.
3. **Edit 2:** Append the satellite-repair-missions paragraph block to the end of `### Mission planning as structured persistent activity` body via the standard subsection-body-append pattern.
4. **Edit 3:** Append the three supply-line extension paragraph blocks (strategic depth + unit measurement + auto-route complexity) to the end of `### Supply lines` body. The append lands after commit 011b's Route-progression closing paragraph (boundary anchor B6).
5. Verify all six boundary anchors still present exactly once. Verify line-anchored heading count for new flight-computer subsection. Verify all distinctive new-content phrases. Verify ordering using line-anchored regex for h3 positions. Atomic write via `.recovery` + `os.replace`.

File grew from 1840 lines / 208,353 bytes to 1927 lines / 218,123 bytes (+87 lines, +9,770 bytes).

## Verification

105 checks, all passing on first run. Six groups:

### A. New content present (40 checks)

- New subsection heading `^### Flight computers and unmanned operation$` line-anchored exactly once
- Flight-computer subsection content: LOCKED opener; `**Flight computer as a discrete part.**`; `**Manned vessels without flight computer.**`; `Basic stability (auto-hold-attitude) is always available`; `**What flight computers enable:**`; `**Manned vs unmanned trade-offs:**`; the G-force tolerance table row (the most distinctive table row content); `**Advanced maneuvering primitives.**`; all nine maneuvering primitive bullets present exactly once each (match velocity, match orbit, approach to specified range, station-keeping, match rotation, dock at specified port, hold attitude, burn to target orbital parameters, hold altitude); `**Incubation modules.**`; `**Success model.**`; `**Failure-mode-tied probabilities the player affects through engineering.**`; `**Fixed per-design success multiplier.**`; closing sentence `Incubation is the cheapest interstellar-tier mission: no crew, no life support during cruise, no return vehicle.`
- Mission planning extension: `**Satellite and infrastructure repair missions.**` exactly once; closing sentence about ongoing repair schedules
- Supply lines extension: `**Strategic depth in supply route design.**`; `**Supply unit measurement per resource type:**`; `**Auto-route complexity acknowledgment.**`; six strategy bullets (return-trip cargo, multi-stop routes, gravity-assist trajectories, refueling cascades, synchronized launches, specialized cargo handlers); two distinctive unit-measurement entries (`**Tons** for bulk materials`, `**Categorical inventory** for research samples`)
- Four cross-references inside the new flight-computer content: to `### Supply lines` in §5; to `### Sandbox-as-simulator integrated in Mission Control` in §7 (commit 023 sibling); to `### Automation and scripting` in §3; to `### Interstellar travel: tiered tech progression` in §4

### B. Boundary verbatim-with-context anchors preserved (6 checks)

- B1: `**Mobile shipping note.** Vizzy is the part of the game least suited to mobile.` (commit 001 + commit 017 L499-update closing of Automation and scripting; Edit 1 lands after this)
- B2: `**LOCKED:** A single stylized humanoid species. Crew tracked as headcount for base mechanics, as named individuals for important missions.` (commit 001 Crew and characters LOCKED opener; Edit 1 lands before this)
- B3: `This is structural overlay, not new mechanics. Missions organize existing systems so the player stays oriented in late-game complexity.` (commit 013 Mission planning closing; Edit 2 appends after this)
- B4: `**LOCKED:** Supply lines operate as scheduled real vessels rather than abstract throughput parameters.` (commit 011b Supply lines LOCKED opener)
- B5: `Storage exists in VAB facilities; there are no separate depot modules. The home VAB is starting storage at the home planet; orbital VABs and surface VABs at other bodies provide storage at their locations.` (commit 011b depot-terminology-cascade paragraph)
- B6: `**Route progression:** first-time players get working routes from defaults. Engaged players optimize craft designs. Mastery players automate with Vizzy and build sophisticated multi-route logistics networks. Same system, different depths.` (commit 011b Supply lines closing; Edit 3 appends after this)

### C. Ordering (4 checks via line-anchored regex)

- §3 Edit 1 placement: `### Automation and scripting` < `### Flight computers and unmanned operation` < `### Crew and characters`
- §3 Edit 2 placement: `### Mission planning as structured persistent activity` heading < new satellite-repair paragraph < `### Campaigns as multi-mission programs` heading
- §5 Edit 3 placement: `### Supply lines` heading < new strategic-depth paragraph < `### Research as logistics-driven question-answering` heading
- §5 Edit 3 boundary ordering: commit 011b Route-progression closing paragraph < new strategic-depth paragraph

### D. Structural counts (15 checks)

- §3 h3 count = 17 (was 16; +1 from Flight computers and unmanned operation)
- §1=15, §2=15, §4=17, §5=6, §6=13, §7=7, §8=1, §9=10, §10=0, §11=4, §12=6, §13=4, §14=2, §15=0 (all unchanged from commit 023)

### E. Section headings (8 checks)

Each of the 15 `## N. Title` headings line-anchored exactly once (sample of 8 verified): `## 1. Vision`, `## 2. Foundation (LOCKED unless noted)`, `## 3. Gameplay mechanics`, `## 4. World behavior and discovery`, `## 5. Resources, bases, and logistics`, `## 6. Procedural generation`, `## 7. UI and information density`, `## 15. Document status`.

### F. Prior-commit anchors preserved (32 checks)

- Commit 014 damage-repair: no malformed line; restored inline reference present exactly once
- Commit 015 five verbatim-with-context anchors all preserved
- Commit 017 cross-reference sample (six representative cross-references) all preserved
- Commits 018-023 anchors: commit 018 wavelength-limits + first-image moments; commit 019 crew-tolerances + three-category framing preserved verbatim + `Radiation dose, zero-G exposure, mission stress` line preserved; commit 020 storage modules universal; commit 021 four-bodies-intensive-hand-craft + Saturn rings galaxy-wide parenthetical + `^### Off-world mining as interstellar gating$`; commit 022 `^### Asteroid clusters as system composition variant$` + strip mining; commit 023 `^### Mission Control as primary UI$` + `^### Sandbox-as-simulator integrated in Mission Control$` + `^### Forgiving restart philosophy$` + `**Save mechanics implementation:**`
- Cross-section preserved-content battery: `Engineering as the verb` (§1); `Floating origin shift threshold: 50 km default` (§2); `**Crew are physically located on vessels.**`; `**Crew as a finite resource.**`

## Replay

```
cd C:\Users\gmkar\space_sim
git add docs/CONSTRAINTS.md commits/024_flight_computers_automation_supply.md
git commit -F commits/024_flight_computers_automation_supply.md
```
