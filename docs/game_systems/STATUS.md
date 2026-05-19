Game systems covers the rules that turn physics and worlds into a game: resource economy, supply lines, crew, agency management, mission objectives, research, progression.

This piece has substantial design captured for some sub-areas (supply lines, resource economy, crew). Other areas (agency management, mission objectives, research, progression) are not yet designed.

## Supply lines (v1 design locked)

**Abstracted lines model.** V1 supply lines are abstracted as lines on the map with cargo flowing along them, not as physically simulated vessels. The autopilot system (which would handle physically-simulated supply vessels) is decoupled and lands as its own work stream.

**Route declaration:** Player declares a supply route if current fuel production supports its demand. Resources are specific materials. Lines transport actual cargo with specific quantities.

**Launch windows respected.** Lines only launch during orbital transfer windows between origin and destination. Between windows, demand accumulates as backlog. At window time, the line ships everything queued. Launch windows are a real gating mechanism, not just timing flavor.

**Player visibility:** lines visible on map. Progress visible (cargo moving along the line). Player can pause, modify, or cancel routes.

**Failures don't happen in v1.** Once a route is set up and supplied, it runs reliably. No "ship lost en route" mechanics. Routes can stop because of fuel exhaustion at supply location, but missions themselves don't fail.

**Player can fly cargo manually.** Player-flown craft can be flagged as supply craft, showing on the map alongside abstracted lines. Manual supply is an opt-in for routes the player cares about.

**Window-based throughput.** Throughput per route is determined by frequency of launch windows (e.g., Earth-Mars window every ~26 months). High-priority missions can specify higher fuel consumption to fly faster trajectories during windows.

**Multi-stop routes deferred post-launch.** V1 supports direct flights only. Gravity assists and intermediate refueling stops are post-launch.

## Resource economy (v1 design locked)

**Resource list (working draft):**

Fluids/gases:

- Water (H2O) — electrolysable to H2 + O2
- Liquid hydrogen (LH2)
- Liquid oxygen (LOX) — also serves as crew oxygen
- Methane (CH4)
- Kerosene (RP-1 equivalent)
- Xenon (rare)

Solids:

- Food (crew consumable)
- Solid propellant (pre-manufactured)
- Alloy/metals (general structural)
- Polymers (inflatable habs, soft goods)
- Silicon/electronics (advanced parts)

Approximately 11 distinct resources. Resource interconversion is real (electrolysis, combination reactions). LOX serves dual purpose (engine oxidizer + crew oxygen).

**Production model.** Continuous over time. Bases can have local infinite supply of resources native to their body. Earth has plenty of common resources; specialty resources exist on specific bodies. Production requires power and either crew or a computer chip.

**No money/budget.** Pure physical accounting. No currency, no trading. May add for multiplayer post-launch.

**Storage built by player.** Costs materials. Visually distinct per resource type. Bases can grow to accommodate (not pure tycoon micromanagement).

**ISRU is core gameplay.** Every body with a resource has infinite supply. Players establish ISRU operations to become self-sufficient. Water on icy bodies → hydrolox. Hydrocarbons → methalox. CO2 atmospheric mining → methane via Sabatier process.

**Bases need power and assignment.** Computers can run small operations; larger operations benefit from crew (better production rates due to experience).

**Consumption is event-based + crew-driven.** Idle bases consume nothing. Active operations consume during their work. Crew consume food/water/oxygen daily.

**Catastrophic failures recoverable.** Resource mismanagement kills missions, not the game. Player learns and improves. No game-over state.

**In-transit tracking.** Resources on supply lines count as "in transit" separately from source or destination totals.

**Manual movement allowed.** Player can load cargo on a personal craft, fly it somewhere, and unload outside the abstracted supply line system.

**Resources are planet-wide pools.** Once on a planet, location is abstracted (no "this fuel is at Cape Canaveral specifically"). Planet-scale resource pools.

**No deposits.** All planets with a given resource have infinite amount. Mining can extract anywhere. Small asteroids may be exception (mineable to deletion). No prospecting, no rare deposit hunting.

**Player starts with infrastructure and resources.** No build-from-scratch start. Earth begins as friendly developed launch site.

## Crew system (v1 design locked)

**4 named crew + generic pool.** Player starts with 2 named crew. 2 more named crew are rescuable from Saturn orbit (the prior-wave astronauts at the icy moon). Beyond these 4 fixed names, crew come from a generic randomized pool with random names.

**Hiring rate.** Up to ~5 per game-year. No financial cost; time gates hiring.

**Identity in v1.** Names only, possibly portraits. No backstories, personalities, or character stories. Post-launch can add depth.

**All crew are generalists.** No specialization roles. No skill trees. Single linear progression: endurance.

**Endurance accumulation.** Crew accumulate endurance over time (0.01-0.1% per game-day cumulative space time). Higher endurance reduces resource consumption and reduces radiation/g-force damage susceptibility. Caps at maximum.

**Critical condition recoverable.** Crew enter critical condition from radiation events, g-force events, or catastrophic vessel failure. Critical condition is rescuable. Crew don't die from critical condition itself — only from total capsule destruction.

**MIA mechanic.** Stranded crew get marked MIA on the roster. Their craft shows a red alert icon on the map. Rescue missions are explicit gameplay content — finding ways to reach stranded crew is a major mission type that emerges from this system.

**Crew vs computer pros/cons:**

Crew:

- Endurance progression (better at risky missions over time)
- Adaptive problem-solving (better outcomes in anomalous events)
- EVA repair and reconfiguration
- Production bonus at bases
- Required for some narrative beats (Saturn rescue, anomaly investigation)
- Can self-replicate via incubators (creates more crew at remote colonies)
- Direct/instant control (no light-delay)

Computers:

- No life support cost
- No critical condition risk
- No emotional cost on failure
- Can be launched from anywhere (no physical-presence requirement)
- Parallel operations scale
- Cannot self-replicate (must be manufactured)
- Speed-of-light delays for control requests at distance (the exact mechanic of this is open — full simulation vs flavor)

**Self-replication via incubators.** Crew can produce more crew through incubator parts. Computers cannot self-replicate (must be manufactured at facility with appropriate resources).

**Assignment model.** Crew assigned at vessel build/launch time. Cannot swap during flight. Can be moved between locations via supply ships (no teleporting). Stationed at specific locations, take up housing capacity.

**Aging: none.** Crew last forever (simplicity).

**Life support: separate streams.** Food, water, oxygen tracked separately. Experienced crew need less. Multiple supply paths (consumable + recyclable).

**Environmental hazards.** Radiation in specific high-risk areas. G-force during high-acceleration maneuvers. Endurance reduces susceptibility. Player doesn't actively manage atmosphere/temperature in v1.

**Comfort: complaints via radio only.** Long missions in small capsules cause crew complaints. Not mechanical penalty. Habs needed for genuine long-duration missions.

**No morale system in v1.** Crew don't quit, don't have conflicts, don't need rotation.

**Crew or computer for base operations.** Both can run operations. Crew get the experience benefit and production bonus; chips don't. Crew valuable for things you want to scale up.

## Not yet designed

- Agency management (the player's space program as an organization at the high level)
- Mission objectives (what is the player trying to accomplish at any moment)
- Research and progression (technology unlocks through gameplay — likely small given "no progression gating" decisions)

## Open questions

- Speed-of-light delay mechanic for computer-controlled distant craft: full simulation (realistic but punishing) vs flavor (visual indicator without mechanical impact)
- Incubator production rate (months, years, decades game-time per new crew)
- Whether incubator-grown crew start at zero endurance or inherit baseline (raised in space, naturally adapted)
- Population cap per location based on hab capacity and food production
- Whether computers can be manufactured at remote bases with appropriate resources, or only at Earth-based facilities
- What materials chip manufacturing specifically requires beyond silicon

## Status

Substantive design captured for supply lines, resource economy, and crew. Other sub-areas not yet designed. Most v1 critical-path game systems work is the abstracted supply line system implementation and the resource economy integration with vessels.
