Vessels covers everything about player-built craft: the parts library, the craft assembly interface (how players design rockets by combining parts), design validation (delta-v calculation, mass distribution, fuel routing, staging order), and structural simulation (joints, breakage, kraken-prevention at high velocity).

This piece is scoped and substantively designed at the architectural level. No implementation has started. The vessel container schema lives in engine and is complete; that's the runtime data shape that vessels-as-built will populate.

## Architectural decisions locked

**Procedural parts for all categories, with constrained parameter ranges.** Every part is a procedural generator (similar to Juno's approach) rather than fixed pre-built parts. Player configures size, shape, subtype, material via UI sliders/options; the generator produces the actual mesh and stats. Parameter ranges are constrained so generated parts stay reasonable.

**Juno-style attachment.** Surface attach + node attach hybrid. Precise rotator for orientations. Mirror symmetry and radial symmetry (2x-10x) supported.

**No progression gating on parts.** All parts available from start. Logistics, skill, and resource availability gate gameplay — not artificial unlocks.

**Five fuels in v1: methalox, kerolox, hydrolox, ion (xenon), solid.** Nuclear deferred to post-launch. Engines are fuel-agnostic but performance varies by fuel choice. Different fuels create different production/logistics challenges, mapping to which bodies have which resources.

**Life support is both consumable and recyclable.** Crew need food, water, oxygen as separate daily streams. Player can pack supplies (mass cost scales with duration) or install recyclers/scrubbers (fixed mass cost regardless of duration). Long missions become engineering tradeoffs.

**Rovers in v1.** Wheels, hub motors, surface mobility. Surface exploration of procgen planets is a core gameplay loop.

**Some robotic parts in v1.** Hinges and basic actuators yes; complex articulated systems post-launch.

**Two-stage parachutes** (drogue then main).

**Heat shields are directional.** Player orients them to protect a specific side. Wrong orientation results in damage.

**Solar panels toggleable** between auto-track and manual orientation.

**Relay-based comms with line-of-sight modeling.** Player must build relay networks to maintain communications. Blind-spot visualizer helps players plan relay placement. Affects what missions are possible (no comms = no remote control).

**Telescopes and sensor arrays as discovery tools.** Telescopes scan distant bodies (haven't visited yet). Sensors scan nearby bodies (composition, atmosphere, magnetic field, planet type). Feeds the logbook and procgen metadata reveal system.

**Build UI: Juno-style.** Draggable parts, modal build screen (separate from gameplay), 3D rotation, save/load craft designs. Build can happen at ground VAB (launch pad + visible robot arm/gantry) or orbital VAB (dockable station segment with robotic arm part).

**Manufacturing model.** Craft saved as blueprints (cost materials to build, instant build time) or as instances (already-built craft you reuse). Editing blueprints costs proportional to upgrade size, with refund for downgrades. No build-time delays — manufacturing instant if materials available.

**Crew can ride chip-controlled craft.** Command chip ≠ command capsule. Chips pilot; crew are passengers/scientists/specialists. Not mutually exclusive.

**Decouplers.** Stack decoupling happens automatically between engines and tanks (like Juno). Radial decouplers are a separate part for side stages. Decoupling produces persistent debris (the discarded stage continues as a separate vessel).

**Structural failure exists.** Parts break from over-stress; joints can fail during flight. Build-time stability warnings are minimal — player learns by failing.

**No part wear.** Permanent parts. No maintenance, no degradation tracking. May add as toggle post-launch.

**Validation is informational, not blocking.** Build UI shows warnings but doesn't prevent launch of flawed craft. Player has all information available; they choose whether to launch.

## Working part list

Initial part categories identified (procedural unless noted):

- Fuel tank (contains methalox / kerolox / hydrolox / xenon / supplies / battery; settings: size, shape, contents, texture)
- Strut (size, shape)
- Fairing system (size, shape)
- Detacher / decoupler (radial side-stage decoupler)
- Cargo bay (door type, size, shape; animated doors, no interior in v1)
- Command capsule (size = capacity; contains crew)
- Command chip (size; uncrewed control)
- Engine (size, type, fuel, nozzle; covers solid, liquid, ion, nuclear-deferred)
- Fin (size, shape)
- Wing (size, shape, fuel — wings can hold fuel)
- Parachute (size, shape; two-stage)
- Landing legs (size, config)
- Docking port (size; enables transfer of cargo and crew)
- Heat shield (size, shape; directional)
- Hab (size = capacity, shape, rotates?; contains crew supplies in tons and days)
- VAB robot arm (for building vehicles in space or on ground; converts a landing pad to ground VAB or a docking port to orbital VAB)
- Comms (relay, receive signals; for objectives, updates, telemetry)
- Telescope (size = resolution, wavelength; for discovery of distant bodies)
- Solar panels (size = resolution, wavelength; power generation)
- Generators (size, fuel type; burn fuel for power)
- Radiators (heat dissipation for space and some bases)
- Sensor array (size = resolution; mass spec, distance, planetary scanning)
- Lights
- RCS nozzle (fixed, multi-directional)
- Gyroscope / reaction wheel (size, shape; stabilizes craft at electricity cost)
- Wheels (hub motors for rovers)

Some parts will defer to post-launch: complex robotic actuators, articulated systems, inflatable habitats (the inflatable part exists but special deployment animations may defer), specialized scientific equipment beyond core sensors.

## Open questions

- Specific parameter ranges for each procedural part (what's the smallest engine, largest tank?). Implementation-detail design that happens during implementation.
- Specific stat formulas (how mass scales with size, how Isp varies with cycle type). Implementation detail.
- Specific subtype options per category (which engine cycles? which tank shapes?). Implementation detail.
- UI specifics for procedural parameter controls (sliders, dropdowns, numeric inputs).

## Status

Largest unbuilt piece of the project alongside World. Probably months of focused work when implementation begins. Design is substantive but stops at architectural decisions; detailed per-part design happens during implementation phases.
