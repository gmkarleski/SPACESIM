# Phase 0 Artifact List for v1

**Purpose:** the specific named content and design decisions that ship in v1. This list locks the artifacts that affect code architecture, scene authoring, asset pipelines, or other systems' design — not exhaustive content authoring.

**Scope:** v1 release content only. Post-launch content (interstellar-cruise mode destinations, v1.1 multiplayer additions, post-launch named characters and missions) is out of scope here.

**Update discipline:** when v1 content authoring resolves a Tier C placeholder (e.g., specific names, dialogue text), update this document. When v1 scope changes (a Tier A artifact moves to v1.1, or vice versa), update this document.

**Read this when:** scoping implementation work, authoring content for v1, or asking "is X in v1?"

---

## v1 scope summary

v1 ships with the home system as the primary playable space. Interstellar-cruise mode (Phase 6) is post-launch; v1 only implements PhysX-active and Kepler-rails physics modes. The "interstellar destination" in v1 is a distant heliocentric or near-interstellar-medium target (Voyager-equivalent in the Oort cloud, generation-ship-equivalent beyond Pluto orbit), reachable via long Kepler-rails journey rather than via interstellar-cruise.

The central narrative arc of v1 is the Saturn astronaut rescue — a recurring story thread from opening tutorial through mid-game payoff, with lasting consequences (rescued astronauts become veteran crew available for difficult missions).

The world has two visible eras:
- **Prior wave (2020s-2040s tech):** earlier space program era that built inherited hardware, then ended (unresolved why in v1)
- **Current era (2050-2100 tech):** player's era, program restarting

The player operates with inherited prior-wave vessels at start; modern current-era tech becomes available through gameplay progression.

---

## Tier A — must lock for v1

Content that gates v1 implementation. These items affect code architecture, scene authoring, or other systems' design.

### Story arc: Saturn astronaut rescue

**Premise:** two named astronauts from the prior-wave program (2020s-2040s era) were sent to Saturn to scope an icy moon for human habitability. They reached the destination, set up a station in Saturn orbit, and began surveying. Something happened. Communication was lost.

**Player encounter:** v1 opens with the player receiving a distress signal trail — fragments of transmission, satellite breadcrumbs, Channel 16 broadcast remnants — leading back to the Saturn system. The opening rescue directive is to find and recover the astronauts.

**The astronauts:**
- Two named individuals (names: Tier C, deferred to content authoring)
- Both in critical condition when found
- Both rescuable with reasonable player effort (no harsh time limit, no impossible delta-v requirements)
- Once rescued, both become veteran-grade crew available for difficult missions
- Their station contains data about the icy moon they were surveying

**The icy moon (Enceladus-equivalent):**
- Effectively-infinite extractable water (locked in commit 021)
- Habitability survey was the astronauts' mission purpose
- Data recovered from their station unlocks an investigation thread for the player to continue
- What the astronauts found: deferred (Tier B — see below)

**Implications for systems:**
- Mission planning must support multi-stage long-duration rescue missions
- Life support mechanics must handle critical-condition crew recovery (per commit 019)
- Discovery progression must support a guided lead-trail (transmission → trail → station → astronauts)
- Crew tracking must support named individuals with "veteran" status differentiation

### Tier A artifacts present in v1

All of these exist in v1's home system and can be encountered through normal play:

| Artifact | Location | Era | Reveal on encounter |
|---|---|---|---|
| Voyager-equivalent | Outer system / Oort cloud (long Kepler-rails journey) | Real Voyager-era (1977+) | Confirms specific historical achievements of the prior era; baseline catalog entry pre-populated |
| ISS-equivalent | Home planet orbit | Prior wave | Documents the prior wave's crewed-orbital capability; recoverable resources/parts |
| Apollo-equivalent sites | Home moon surface (multiple sites) | Prior wave's lunar program | Documents the prior wave's lunar capability; recoverable instruments |
| Mars landers | Mars-equivalent surface (multiple) | Prior wave | Documents the prior wave's robotic surface exploration; some still transmitting telemetry |
| Inherited rover | Home moon (one specific site) | Prior wave | Functional rover the player can operate from day 1; contains baseline surface telemetry |
| Saturn station + 2 astronauts | Saturn orbit | Prior wave (most recent) | The central v1 narrative payoff (see story arc above) |
| Distant interstellar-medium destination | Beyond Pluto-equivalent orbit | Prior wave (sent decades earlier) | Long-form payoff for v1 endgame; specific reveal: Tier B |

**Implications for systems:**
- Catalog must support pre-populated entries for known-but-not-yet-visited artifacts
- Some artifacts are interactive (recoverable, dockable, operable); others are observation-only
- The "distant interstellar-medium destination" is reachable via Kepler-rails, not interstellar-cruise — Phase 6 stays post-launch

### Starting state

**Crew:**
- 3 named starting crew (player character + 2 named colleagues)
- All are current-era (2050-2100), not prior-wave veterans
- Names: Tier C, deferred
- Personalities: minimal v1 specification; named individuals with role assignment but no deep characterization in v1

**Vessels at home base:**
- 3 pre-built vessels representing early-to-mid 2000s tech (within the prior-wave era)
- Specific vessel types (strawman, refinable in v1 content authoring):
  - **Surface rover** (already deployed on home moon — the "inherited rover" from above; Tier A artifact and starting asset in one)
  - **Orbital probe** (small uncrewed satellite in home planet orbit; reusable for missions)
  - **Launch-ready rocket** (on home planet surface; suborbital-capable, basic crew-rating)

**Home base:**
- Home VAB on home planet surface
- Starting resources sufficient for first 2-3 missions without resource gates
- Specific starting inventory: Tier C

**Crew hiring system:**
- Available after first mission completes (gating tutorial flow)
- Paced unlock: 1-2 new hires per game-year
- No hard cap on total crew count — bounded by pacing only
- Cost: resources + time (not free, not strictly gated)

**Implications for systems:**
- Save format must serialize 3 named crew, 3 vessels, home base inventory at game start
- Mission planning must support a guided first-mission tutorial flow
- Crew hiring requires a recruitment mechanic (UI + cost system + pacing controller)
- Vessels must support both "deployed on surface" and "in orbit" starting states

### Physics scope

**Modes implemented in v1:**
- PhysX-active (Unity rigidbody simulation)
- Kepler-rails (analytic orbit propagation)

**Modes deferred to post-launch:**
- Interstellar-cruise (Phase 6 / v1.1+)

**Mode transitions in v1:**
- PhysX-active ↔ Kepler-rails (both directions, per netcode contract §3.1)

**Mode transitions deferred:**
- Kepler-rails ↔ Interstellar-cruise (§3.2) — Phase 6+
- PhysX-active ↔ Interstellar-cruise (§3.3, rare combined transition) — Phase 6+

**Implications for systems:**
- Phase 0 prototype must validate PhysX-active ↔ Kepler-rails transitions
- Interstellar-cruise mode code can stay as stubs through v1 (per netcode contract §10.1 prototype scope)
- The "distant interstellar-medium destination" reaches Kepler-rails-only because it's still bound to the home star's gravity

---

## Tier B — lock for v1, specifics flexible

Content that v1 needs to have, but where exact specifics can resolve during content authoring without affecting code architecture.

### Home system anomaly count

**Target range:** 3-8 anomalies across the four intensive-craft bodies plus general system space.

**Distribution principle:**
- 1 anomaly tied to the Saturn icy moon (what the astronauts found — see below)
- 1-2 anomalies on the home moon (small-scale, tutorial-graceful)
- 1-2 anomalies on Mars-equivalent (medium-scale)
- 1-2 anomalies elsewhere (asteroid clusters, outer system, Voyager-equivalent investigations)
- Final count determined during content authoring once gameplay pacing is testable

**Implications for systems:**
- Discovery progression must support a sparse anomaly distribution (not anomaly-on-every-body density)
- Anomaly authoring system (per CONSTRAINTS §10 open question) must support both hand-authored and procgen-distributed anomalies; v1 uses hand-authored for the home system

### What the Saturn icy moon contains (beyond water)

**The astronauts found something.** Their station's data recovery reveals it. What "it" is should resolve during content authoring, but the structure is locked:

- Resource discovery (subterranean composition, exotic isotopes, etc.) — mundane but plausible
- Geological anomaly (unexpected geyser patterns, subsurface ocean structure) — scientifically interesting
- Biosignature (organic chemistry, possible life precursors) — high-stakes scientifically
- Structure (something built, ambiguously natural or artificial) — speculative, high narrative weight

**Default lean for content authoring:** geological anomaly with biosignature edge — the data reveals unexpected subsurface ocean structure plus organic chemistry traces, leaving "is there life here" as an open investigation thread. Forward-compatible with later content (v1.1 could resolve the question, or leave it open through multiple expansions).

**Implications for systems:**
- Research mechanics must support multi-stage investigation threads (initial discovery → follow-up missions → eventual resolution or open continuation)
- Catalog entries must support "partial information" states (recovered data fragments without complete picture)

### Distant interstellar-medium destination — what it reveals

**Structure locked:** one destination at distant heliocentric range, prior-wave-era artifact, reachable via long Kepler-rails journey.

**Content (Tier B, deferred):** what the player finds when reaching it. Options aligned with the world's framing:

- A prior-wave deep-space probe that has been transmitting long-form data
- A generation-ship-equivalent (slower-than-light interstellar attempt, prior wave) that's been in cruise for decades
- A signal source / artifact that the prior wave detected and never reached
- An archive / time capsule from the prior wave (deliberate long-term communication attempt)

**Default lean:** the prior wave's deep-space probe with long-form data — most consistent with the inherited-hardware framing, gives the player meaningful astrophysics data, doesn't require a generation-ship encounter mechanic that's bigger than v1 scope. The probe's data contains observations of stellar phenomena that seed future research.

**Implications for systems:**
- Catalog must support a single distant artifact with rich data payload
- The artifact's discoverable content drives v1 endgame research threads
- No interstellar-cruise mode needed; Kepler-rails handles the long journey

### Research scope

**Locked:** research is entirely discovery-based in v1. No abstract research projects with "spend resources, wait X game-time, get research credits" mechanics.

**What research means in v1:**
- Player makes a discovery (anomaly, artifact, observation)
- Discovery generates a research question
- Player designs and executes missions to answer the question
- Mission results either resolve the question, partially resolve it, or open new questions
- No tech-tree, no point-spending, no abstracted progression

**Implications for systems:**
- Per CONSTRAINTS §3 commit 015, research-as-logistics-driven-question-answering — this is now reaffirmed for v1
- No research project list to author at game start; questions emerge from gameplay
- Player capabilities expand through equipment/parts unlocked by discoveries, not via research points

---

## Tier C — defer to v1 content authoring

Content that doesn't affect code architecture and can resolve during the v1 production cycle.

### Names

- Home planet, home moon, Mars-equivalent, Saturn-equivalent, the icy moon, the distant interstellar-medium destination
- The two starting colleague crew members
- The two Saturn astronauts
- The player character (if named; could remain unnamed)
- The player's organization / agency
- The prior-wave organization (whether same as current or different)

### Dialogue and transmission content

- Opening rescue directive transmission text
- Channel 16 broadcasts (mysterious transmissions per commit 014b)
- Operational chatter from program operations
- Astronaut log entries from the Saturn station
- Voyager-equivalent / ISS-equivalent / Apollo-site / Mars-lander text content

### Visual specifics

- Character visual design language (per CONSTRAINTS §10 open question)
- Vessel visual designs (interior, exterior, branding)
- Body visuals (terrain, atmosphere, sky)
- UI visual polish (Mission Control monitor style, simulator bezel design, etc.)

### Procgen content beyond the home system

- All star systems beyond the home system (per CONSTRAINTS commit 008 procgen architecture)
- Distant body content not tied to v1 narrative
- Anomaly distribution in procgen-generated systems (procgen-flagged anomaly types only; specific instances generated at play time)

### Prior wave history

- What specifically happened to the prior-wave program (deliberately unresolved in v1 per design discussion)
- Specific founding dates, key figures, organizational structure
- Whether the player's program is a literal continuation or a successor under different management

These specifics can be alluded to in v1 content without being explicitly resolved. Implementation should support either resolution path being added later without rework.

---

## Cross-references

- `docs/CONSTRAINTS.md` §1, §3, §4 — design pillars, gameplay mechanics, world behavior framings this artifact list operates within
- `docs/CONSTRAINTS.md` §9 — build order; v1 ships at end of Phase 5
- `docs/CONSTRAINTS.md` §10 — open design questions, several of which this list addresses
- `docs/NETCODE_CONTRACT.md` §3 — mode transition protocols; v1 scope clarified above
- `docs/DECISIONS.md` — resolved decisions including aerodynamic model, multiplayer scale, atmospheric flight pillar
- `commits/021_home_system_scope_refinement.md` — original framing of intensive-craft bodies and inherited rover
- `commits/012_vision_level_framings.md` — original Tier A artifact framings

---

## Update history

- **2026-05-17 (commit 037):** initial landing. Locks Tier A scope based on design discussion; Tier B and Tier C structures defined for content-authoring phase resolution.
