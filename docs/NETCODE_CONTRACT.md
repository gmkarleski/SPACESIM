# Netcode Contract

## The sim-tick boundary specification for authoritative state and PhysX-active simulation

**Status:** Finalized. This document is the canonical specification of the boundary between authoritative double-precision state and local PhysX simulation. It is the explicit Phase 0 → Phase 1 gate per `docs/CONSTRAINTS.md` §9.

**Form of this deliverable.** Two components:

1. **The written contract** (this document). Specifies the rules.
2. **The prototype** (separate implementation work in Phase 0). Demonstrates the rules hold in practice.

Neither alone is sufficient. Written-only risks elegant theory that doesn't survive implementation; prototype-only risks ad-hoc code without articulated principles. Both are required to close Phase 0.

**Architecture summary.** The game's state model treats single-player as a degenerate case of multiplayer (one machine, zero peers). The same code, the same state representation, and the same authority model serve both cases. Multiplayer is not a parallel system layered on top; it is the architecture, with single-player as the trivial instance. This document specifies the multiplayer-shaped architecture even though v1 ships single-player only.

**Locked decisions from Phase 0 design:**

- Multiplayer scale: 2-4 players for v1.1 and beyond. No multiplayer in v1. Architecture supports scaling to ~16 if it becomes interesting.
- Sim-tick rate: 30 Hz fixed.
- Determinism scope: authoritative-state only. No lockstep. Host's state is canonical.
- Physics modes: PhysX-active, Kepler-rails, interstellar-cruise (per CONSTRAINTS §2 commit 002).
- Coordinate system: double-precision world coordinates with floating origin at 50km threshold (per CONSTRAINTS §2 commit 002).

---

## 1. Architectural foundations

### 1.1 Two layers of state

Every vessel has two layers of state:

**Authoritative state (sim-tick layer).** Double-precision world coordinates. Advanced at 30 Hz sim-tick rate. The source of truth. This is what gets saved, transmitted across the network (in multiplayer), and reconciled across machines. Authoritative state is mode-aware — different physics modes have different state shapes — but always lives in the same coordinate system.

**Display state (PhysX layer).** Single-precision local coordinates relative to floating origin. Advanced by Unity's PhysX at frame rate. Used for rendering and for fine-grained physics interactions (contacts, joints, atmospheric forces). The PhysX layer is **not** authoritative — it is a derived view that gets re-synced from authoritative state every sim-tick.

The relationship: **authoritative state → PhysX state** at each sim-tick boundary. PhysX simulates between sim-ticks. At the next sim-tick, the simulation results are reconciled back into authoritative state, and the cycle repeats.

This separation is the central architectural commitment. Most networked physics games conflate these layers and pay for it in desync issues, save-load divergence, and brittle multiplayer. By keeping authoritative state explicit and treating PhysX as a derived view, the game's state is always uniquely defined regardless of frame rate, machine performance, or network conditions.

### 1.2 The sim-tick boundary

The sim-tick is the heartbeat of authoritative state. Fires at fixed 30 Hz (33.33 ms intervals). At each sim-tick, in order:

1. **Receive peer state.** Apply any incoming authoritative state updates from peers (no-op in single-player). This step exists in single-player code but does nothing.
2. **Read PhysX state.** For every PhysX-active vessel this machine has authority over, read the current rigidbody state (position, velocity, orientation, angular velocity, accumulated contact information).
3. **Convert to authoritative coordinates.** Transform PhysX local coordinates back to double-precision world coordinates relative to current floating origin.
4. **Apply analytic updates.** Advance Kepler-rails vessels by 33.33 ms of orbit propagation. Advance interstellar-cruise vessels by 33.33 ms of cruise (with relativistic time-dilation applied). Apply fuel consumption, life support consumption, scheduled events that fire this tick.
5. **Reconcile authoritative state.** Merge the PhysX-derived updates with the analytic updates into the new authoritative state.
6. **Detect mode transitions.** For each vessel, check whether it should transition between physics modes this tick. If so, execute the mode transition protocol (§3 below).
7. **Push authoritative back to PhysX.** For vessels remaining PhysX-active, write the reconciled authoritative state back to PhysX rigidbodies. This corrects any drift from float precision or determinism issues.
8. **Replicate to peers.** Send authoritative state deltas to peers (no-op in single-player; this step exists in single-player code but does nothing).
9. **Fire events.** Process any analytic events whose scheduled time has been reached this tick (mission arrivals, transmissions, mode transition consequences, etc.).
10. **Advance sim-tick counter.** The sim-tick number increments; rendering interpolates between this tick's state and the next.

This ten-step cycle is the contract's heart. Every state change in the game flows through it. Save state captured between steps 9 and 10 is canonical; replication packets between machines carry slices of step 8's output.

### 1.3 Frame rate independence

Rendering runs at frame rate (60-144 Hz typically). Sim-tick runs at 30 Hz fixed. PhysX runs at its own substep schedule, typically frame-locked.

Display interpolation between sim-tick states produces smooth rendering. A frame rendered halfway between sim-tick N and sim-tick N+1 shows vessel positions interpolated 50% between those two ticks' authoritative positions. The interpolation is display-only; the authoritative state remains at sim-tick boundaries.

**Why fixed sim-tick rate matters.** Variable-rate simulation introduces nondeterminism (different machines tick at different rates and produce different state). Fixed rate is the foundation of cross-machine reproducibility within authoritative-state semantics. Even though we don't require lockstep determinism, fixed sim-tick rate ensures saves are reproducible, replays are possible, and multiplayer state transmission is well-defined.

### 1.4 Time-warp mechanics

Time-warp scales sim-tick advancement, not sim-tick rate. At 100x time-warp, the game advances 100 sim-ticks per real-time sim-tick interval (every 33.33 ms of real time advances 3.33 seconds of game time). The 30 Hz rate stays constant; the work per real-time interval scales.

The advancement is gated by analytic events. At each real-time tick, the game computes `min(ticks_per_real_tick × warp_rate, ticks_until_next_event)` and advances by the smaller number. This is the principle from CONSTRAINTS §2 commit 002. When a scheduled event approaches, time-warp slows to land exactly on the event's tick.

Time-warp ceiling per physics mode:

- **PhysX-active:** 1x maximum. PhysX cannot run faster than real time without producing physics artifacts.
- **Kepler-rails:** Up to 10,000x. Orbit propagation is analytic and can advance arbitrarily fast.
- **Interstellar-cruise:** Up to 100,000x. Cruise is analytic and largely featureless; high time-warp is the design intent.

Transitioning between modes during time-warp triggers automatic warp adjustment — if a vessel on Kepler-rails at 10,000x warp approaches a periapsis below atmospheric entry altitude, warp drops automatically as the vessel approaches PhysX-active threshold.

---

## 2. Authoritative state schema

The authoritative state for any entity in the game is mode-aware. The schema below specifies the fields per mode plus common fields.

This schema is also the save format's data shape. Save files are serialized snapshots of authoritative state at a sim-tick boundary. Network replication packets are slices of this same schema. One canonical state representation serves all three purposes.

### 2.1 Common vessel fields (all modes)

```
VesselAuthoritativeState {
    // Identity
    vessel_id: UUID                       // Permanent unique identifier
    design_id: UUID                       // Reference to mode-portable design
    name: String                          // Player-facing name
    
    // Authority and ownership
    owner_agency_id: UUID                 // For multiplayer; one agency in single-player
    authority_holder_id: NetworkID        // Which machine has authority this tick
    
    // Current physics mode
    mode: PhysicsMode                     // PHYSX_ACTIVE | KEPLER_RAILS | INTERSTELLAR_CRUISE
    mode_entered_at: SimTickCount         // When current mode was entered
    
    // Mass properties (computed from parts, cached)
    total_mass_kg: f64
    center_of_mass_local: f64x3           // Relative to vessel origin
    moments_of_inertia: f64x3             // Principal axes
    
    // Resources (per-part inventory aggregated)
    resource_inventory: ResourceMap       // Per-resource current amount, capacity, storage location
    
    // Crew assignment (vessel-physical, per commit 013)
    crew_aboard: List<CrewID>             // Named crew currently on this vessel
    
    // Mode-specific state (one of)
    physx_state: Option<PhysXState>       // Present when mode == PHYSX_ACTIVE
    kepler_state: Option<KeplerState>     // Present when mode == KEPLER_RAILS
    cruise_state: Option<CruiseState>     // Present when mode == INTERSTELLAR_CRUISE
    
    // Telemetry buffer (per commit 013 failure forensics)
    telemetry_buffer: CircularBuffer<TelemetrySample>
    
    // Last sim-tick this state was advanced
    last_advanced_tick: SimTickCount
}
```

### 2.2 PhysX-active mode state

```
PhysXState {
    // Position and motion in double-precision world coords
    position_world: f64x3                 // Position in galactic coordinates
    velocity_world: f64x3                 // Velocity in galactic coordinates
    orientation: Quaternion               // Orientation (quaternion, double-precision)
    angular_velocity: f64x3               // Angular velocity (rad/s)
    
    // Reference frame context
    reference_body_id: BodyID             // Which body's SOI this vessel is in
    floating_origin: f64x3                // Current floating origin offset
    
    // PhysX rigidbody reference (engine-side, not serialized)
    rigidbody_handle: Option<RigidbodyHandle>
    
    // Active forces (engines firing, RCS, etc.)
    active_thrust_n: f64                  // Current thrust magnitude
    active_thrust_direction: f64x3        // Thrust direction in local frame
    
    // Atmospheric context
    atmospheric_density: f64              // kg/m³ at current altitude
    atmospheric_velocity_rel: f64x3       // Velocity relative to local atmosphere
}
```

### 2.3 Kepler-rails mode state

```
KeplerState {
    // Orbital elements (around reference_body_id from common fields)
    semi_major_axis: f64                  // meters
    eccentricity: f64                     // dimensionless
    inclination: f64                      // radians
    longitude_of_ascending_node: f64      // radians
    argument_of_periapsis: f64            // radians
    true_anomaly_at_epoch: f64            // radians
    epoch_tick: SimTickCount              // sim-tick when these elements were captured
    
    // Reference frame context
    reference_body_id: BodyID             // Which body's gravity defines the orbit
    
    // Pre-computed event predictions for time-warp gating. All four are
    // Option<SimTickCount> (nullable) — see contract amendment at commit 045
    // for the rationale.
    next_periapsis_tick: Option<SimTickCount>     // When vessel reaches periapsis; None for hyperbolic post-periapsis
    next_apoapsis_tick: Option<SimTickCount>      // None for hyperbolic orbits (no apoapsis exists)
    next_soi_transition_tick: Option<SimTickCount>  // SOI exit, if applicable
    next_mode_transition_tick: Option<SimTickCount> // Will need PhysX activation when
}
```

**Nullable event-prediction fields (amended at commit 045):** All four `next_*_tick` fields are `Option<SimTickCount>`, not `SimTickCount`. Hyperbolic orbits genuinely have no future apoapsis, and post-periapsis hyperbolic trajectories have no future periapsis either. Nullable typing makes "no future event" explicit in the type rather than encoding "no event" via a sentinel value like `long.MaxValue`. The original `commit 026` contract had `next_periapsis_tick` and `next_apoapsis_tick` as non-nullable; the implementation has always used nullable (`long?` in C#). This amendment aligns the contract with the implementation's honest type.

### 2.4 Interstellar-cruise mode state

```
CruiseState {
    // Position and motion in galactic coordinates
    position_galactic: f64x3              // Light-years from galactic origin
    velocity_galactic: f64x3              // Velocity vector (proper motion)
    
    // Cruise trajectory
    departure_system_id: SystemID         // Origin star system
    arrival_system_id: SystemID           // Destination star system
    departure_tick: SimTickCount          // When cruise began
    arrival_tick: SimTickCount            // Predicted arrival sim-tick
    
    // Relativistic state
    cruise_velocity_c: f64                // Velocity as fraction of c
    time_dilation_factor: f64             // Ratio of crew time to galactic time
    crew_subjective_time_elapsed: f64     // Seconds of crew time elapsed since departure
    
    // Communication state (per commit 002 light-speed delay)
    last_transmission_received_tick: SimTickCount
    transmission_queue: List<DelayedTransmission>
}
```

### 2.5 Mode-portable design (separate from authoritative state)

Per CONSTRAINTS §3 commit 004a, vessel designs are first-class data portable across save files. The design is **separate from authoritative state** — saves don't store designs; they reference designs by `design_id` and the design file is loaded separately.

```
VesselDesign {
    design_id: UUID
    design_name: String
    created_at_real_time: Timestamp
    modified_at_real_time: Timestamp
    
    // Part tree (vessel construction)
    parts: List<PartInstance>             // Each part with parameters, attachments
    root_part_id: PartID
    
    // Computed properties (cached)
    mass_dry_kg: f64
    delta_v_estimate: f64                 // At current fuel load
    
    // Design history (per commit 013 engineering documentation)
    launch_count: u64
    success_count: u64
    failure_modes_encountered: List<FailureMode>
    modifications: List<ModificationEntry>
    
    // Scripts (per CONSTRAINTS §3 automation)
    vizzy_scripts: List<VizzyScript>
    flight_computer_routes: List<FlightRoute>
}
```

### 2.6 World state schema (beyond vessels)

The contract also covers world-level state:

```
WorldAuthoritativeState {
    // Time
    current_sim_tick: SimTickCount        // The canonical clock
    current_world_time_seconds: f64       // Seconds elapsed since campaign start
    
    // Galaxy and systems
    galaxy_seed: u64                      // Procgen seed (per commit 008)
    visited_systems: Map<SystemID, SystemState>
    
    // All celestial bodies known to the campaign (planets, moons, stars). Each body's
    // hierarchy position, mass, position, and SOI radius is per §2.7 BodyState. Added
    // at commit 044 as part of SOI re-rooting work; entries land progressively as
    // Phase 4 procgen runs.
    bodies: Map<BodyID, BodyState>
    
    // All vessels in the campaign (alive, lost, or in cruise)
    vessels: Map<UUID, VesselAuthoritativeState>
    
    // All bases
    bases: Map<UUID, BaseAuthoritativeState>
    
    // Active supply routes
    supply_routes: Map<UUID, SupplyRouteState>
    
    // Crew roster
    crew: Map<CrewID, CrewState>
    
    // Mission and campaign state
    active_missions: Map<UUID, MissionState>
    active_campaigns: Map<UUID, CampaignState>
    
    // Research state
    research_projects: Map<UUID, ResearchProjectState>
    
    // Catalog (per commit 009)
    catalog: CatalogState
    
    // Transmissions queue (per commit 004b)
    pending_transmissions: List<TransmissionState>
    
    // Agency state (per commit 004a)
    agencies: Map<UUID, AgencyState>
}
```

The world state is the complete authoritative state of a campaign. Save files contain a serialized world state. Network replication transmits world state deltas (changes since last sync).

### 2.7 BodyState (celestial body) schema

Per `### Reference frame hierarchy` in `docs/CONSTRAINTS.md` §2: hierarchical frames (Sun, planet, moon, vessel) with explicit transitions at sphere-of-influence boundaries. The `BodyState` schema captures the per-body data the patched-conics math needs to perform SOI re-rooting (commit 044) and the per-tick analytic propagation when bodies themselves orbit (Phase 4+).

```
BodyState {
    // Identity
    body_id: BodyID                       // UUID; stable across save/load
    name: String                          // Player-facing name (e.g., "Earth", "Mun")
    
    // Mass and gravity
    mass_kg: f64                          // Body mass in kilograms
    mu: f64                               // Standard gravitational parameter G * mass_kg, m^3/s^2
                                          // (derived; cached for math performance)
    
    // Position
    position_world: f64x3                 // Position in galactic (double-precision) coordinates
                                          // PHASE 0/1 SCOPE: fixed at scene load.
                                          // PHASE 4+: computed per-tick from this body's own orbital state.
    
    // SOI hierarchy
    soi_radius_meters: f64                // Sphere-of-influence radius
                                          // PositiveInfinity for top-level bodies (no parent).
                                          // PHASE 1 SCOPE (commit 044): hand-set per body.
                                          // PHASE 4+: computed via Laplace sphere formula
                                          //          r_SOI ≈ a * (m / M_parent)^(2/5)
                                          //          when this body orbits a parent.
    parent_body_id: Option<BodyID>        // Parent in the hierarchy; None for top-level bodies.
                                          // SOI re-rooting reads this to find the body to re-root TO
                                          // when crossing this body's SOI outward.
    
    // PHASE 4+ DEFERRED FIELDS (named here so the schema is complete; not populated in Phase 0/1):
    // orbital_state_around_parent: Option<KeplerState>   // When this body orbits a parent.
    // axial_tilt: f64                                    // Radians.
    // rotation_rate: f64                                 // Radians per second; for body-fixed frame.
    // surface_terrain_seed: u64                          // Phase 4 procgen.
    // atmospheric_profile: Option<AtmosphericProfile>    // Density-vs-altitude function.
    // children_body_ids: List<BodyID>                    // Reactively maintained; alternative to
    //                                                    //   iterating bodies and filtering by parent.
}
```

**Phase 0 / Phase 1 implementation scope:** the in-code `ReferenceBody` MonoBehaviour (in the Vessels asmdef) populates `body_id`, `mass_kg`, `mu`, `position_world`, `soi_radius_meters`, and `parent_body_id`. The remaining fields are scaffolded in the doc but their in-code representation lands with the procgen-bodies work in Phase 4+. The `BodyRegistry` static class (added at commit 044) provides Guid-keyed lookup and parent→children enumeration so save-load and re-rooting math don't need to traverse Inspector wiring chains.

**Save-load semantics:** `parent_body_id` is the persisted reference shape (Inspector references don't survive serialization). On load, `BodyRegistry.TryGetBodyById` resolves the Guid back to a runtime `ReferenceBody`. Self-cycle detection (a body wired as its own parent) is rejected at Awake with an error log; the body is treated as top-level (`parent_body_id = None`) so downstream math doesn't produce bogus orbital re-rooting.

**Top-level body convention:** the star at the root of a star system has `parent_body_id = None` and `soi_radius_meters = PositiveInfinity`. The re-rooting check on a top-level body always reports "still inside SOI" because no finite distance exceeds infinity — mathematically clean for patched conics (a body with no parent has no SOI boundary within its system). In Phase 0 / Phase 1 single-body scenes the lone `ReferenceBody` plays the top-level role; multi-body home-system scenes arrive in Phase 4.

---

## 3. Mode transition protocol

Mode transitions are sharp boundaries where a vessel changes from one physics mode to another. The contract specifies the exact procedure for each transition. Sharp and symmetric per CONSTRAINTS §2 commit 002.

### 3.1 PhysX-active ↔ Kepler-rails

**Trigger conditions for PhysX-active → Kepler-rails:**

A vessel transitions from PhysX-active to Kepler-rails when:

- The vessel is more than 50 km from any active vessel (the active-vessel threshold, per commit 002 floating origin)
- AND no thrust is being applied (engines off)
- AND no atmospheric drag is significant (altitude above atmospheric boundary OR atmospheric_density < threshold)
- AND no contact forces are active (not landed, not docked to a PhysX-active vessel)
- AND the vessel's trajectory is well-defined by patched conics around a single dominant body

The trigger evaluation runs at every sim-tick. The transition is sharp: at sim-tick N, the vessel is PhysX-active; at sim-tick N+1, after transition, the vessel is on Kepler-rails.

**Procedure for PhysX-active → Kepler-rails:**

1. Read current PhysX state (position, velocity, orientation).
2. Compute orbital elements from position and velocity relative to the dominant gravity body.
3. Store the orbital elements as the new `KeplerState`.
4. Compute pre-computed event predictions (next periapsis, apoapsis, SOI transitions, mode-transition triggers).
5. Set `mode = KEPLER_RAILS`, `mode_entered_at = current_sim_tick`.
6. Clear `physx_state`; release the PhysX rigidbody handle.
7. Vessel orientation: preserved as the orientation at transition moment, frozen until re-activation (vessels on rails don't rotate; rotation requires PhysX-active).
8. Angular velocity: preserved in storage but not acted on (no PhysX to apply it); re-applied on transition back.

**Trigger conditions for Kepler-rails → PhysX-active:**

A vessel transitions from Kepler-rails to PhysX-active when any of:

- The vessel enters within 50 km of any active vessel (the active-vessel proximity threshold)
- The vessel's trajectory predicts atmospheric entry within the next sim-tick (the pre-computed `next_mode_transition_tick` fires)
- The player switches focus to the vessel (player attention pulls vessels into PhysX-active)
- The vessel reaches a scripted mode change (Vizzy script triggering thrust, for example)
- Multi-vessel proximity events (multiple Kepler-rails vessels in a 50 km cluster; resolve by computing relative positions and activating the largest cluster)

**Procedure for Kepler-rails → PhysX-active:**

1. Compute the vessel's current position and velocity from orbital elements at the current sim-tick.
2. Apply floating-origin shift if needed (the vessel's position becomes the new origin if no other active vessel is closer).
3. Create a PhysX rigidbody with the computed position, velocity, orientation, and angular velocity.
4. Set `mode = PHYSX_ACTIVE`, `mode_entered_at = current_sim_tick`.
5. Populate `physx_state` with the active fields.
6. Clear `kepler_state`.
7. Atmospheric context updates based on current altitude.

### 3.2 Kepler-rails ↔ Interstellar-cruise

**Trigger conditions for Kepler-rails → Interstellar-cruise:**

A vessel transitions from Kepler-rails to Interstellar-cruise when:

- The vessel's trajectory escapes the home stellar system's gravitational influence (above stellar escape velocity at current distance)
- AND the vessel is configured for interstellar travel (has appropriate propulsion: laser sail engaged, or fusion/antimatter drive active)
- AND the destination is a star system, not a heliocentric orbit

**Procedure for Kepler-rails → Interstellar-cruise:**

1. Compute the vessel's current galactic position and velocity from orbital elements.
2. Resolve the destination star system from the vessel's flight plan or course parameters.
3. Compute predicted arrival sim-tick from velocity and distance.
4. Compute relativistic time-dilation factor from cruise velocity.
5. Initialize `CruiseState` with all fields.
6. Set `mode = INTERSTELLAR_CRUISE`, `mode_entered_at = current_sim_tick`.
7. Clear `kepler_state`.

**Trigger conditions for Interstellar-cruise → Kepler-rails:**

A vessel transitions from Interstellar-cruise to Kepler-rails when:

- The current sim-tick reaches the predicted `arrival_tick`
- AND the vessel can decelerate (has remaining fuel, has functioning deceleration system)

**Procedure for Interstellar-cruise → Kepler-rails:**

1. Position the vessel at the destination star's edge of stellar SOI (specifically: at a point inbound from the cruise trajectory).
2. Compute initial orbital elements from arrival velocity and position.
3. Populate `KeplerState`.
4. Set `mode = KEPLER_RAILS`, `mode_entered_at = current_sim_tick`.
5. Clear `cruise_state`.
6. Fire the arrival event (transmission to player; catalog entry update; mission state update).

If deceleration fails (insufficient fuel, system failure), the vessel continues past the destination as a flyby — `arrival_tick` was a predicted SOI capture point, missed by failed deceleration. The vessel's `cruise_state` updates to a new arrival prediction (the next reachable system, possibly never).

### 3.3 PhysX-active ↔ Interstellar-cruise (rare)

Direct transitions between PhysX-active and interstellar-cruise are not standard — interstellar cruise requires the vessel to first be on Kepler-rails (escape trajectory) before transitioning to cruise. If a vessel somehow needs direct PhysX-active → interstellar-cruise (e.g., a laser sail accelerated past escape velocity within PhysX-active range), the protocol is:

1. PhysX-active → Kepler-rails (using the standard procedure, with an escape trajectory)
2. Immediately Kepler-rails → Interstellar-cruise (using the standard procedure)

Same for the reverse. This is two transitions, not one. The architecture forbids skipping the intermediate Kepler-rails representation because mode transition logic depends on having well-defined trajectory state.

### 3.4 Docked vessels

When two vessels are docked, they share authoritative state for mass and motion. The docked pair operates as a single rigidbody from PhysX's perspective. Authoritative state per-vessel is preserved (each retains its identity, crew, resources, etc.), but motion fields (position, velocity, orientation) refer to the docked-pair's combined motion.

When undocking, the two vessels separate into independent authoritative state. The undocking procedure:

1. At the sim-tick when undocking initiates, compute relative separation velocity from the docking ports.
2. Split mass properties: each vessel's `total_mass_kg`, `center_of_mass_local`, etc. recalculate from its own parts only.
3. Each vessel re-initializes its PhysX rigidbody (PhysX-active mode) or its orbital elements (Kepler-rails mode) based on its independent motion state.
4. The two vessels are now independently tracked.

Docking and undocking happen exclusively in PhysX-active mode. Two vessels on Kepler-rails cannot dock until one or both transition to PhysX-active.

### 3.5 Landed vessels

A vessel landed on a body is in PhysX-active mode with contact forces holding it stationary relative to the body's surface. The body's rotation moves the vessel's position in world coordinates (the vessel rotates with the body).

Landed vessels do not transition to Kepler-rails. They remain PhysX-active because the contact forces require PhysX simulation. However, if no player is paying attention to a landed vessel (the vessel is on an inactive body, no active vessel within 50 km), the vessel uses **"sleeping" PhysX**: PhysX rigidbody is set to kinematic, no physics simulation occurs, position is locked to the body's surface position at the body's current rotation. Sleeping landed vessels consume minimal CPU.

A sleeping landed vessel wakes (returns to active PhysX simulation) when:

- A player switches focus to it
- An active vessel comes within 50 km
- A scripted action triggers it (Vizzy script firing thrust, for example)

The contract's authority model treats a sleeping landed vessel's authoritative state as unchanged between sleep and wake — its position relative to the surface is fixed, its velocity is zero, its orientation is fixed. Authoritative state is preserved without need for analytic advancement.

---

## 4. Time-warp protocol

Time-warp scales sim-tick advancement per real-time tick. The advancement is bounded by the next analytic event.

### 4.1 Event prediction queue

At each sim-tick, the game maintains a priority queue of upcoming events sorted by their scheduled sim-tick:

- **Mode transitions:** When a vessel will need to transition modes (atmospheric entry, SOI exit, etc.).
- **Mission events:** Mission arrivals, supply route deliveries, crew transfers.
- **Research events:** Project completions, observation results returning.
- **Resource events:** Tank reaching capacity (production should stop), supply running out.
- **Transmission events:** Scheduled transmissions firing, light-speed-delayed transmissions arriving.
- **Catalog events:** Discovery progression stage transitions.
- **Game-clock events:** Autosave triggers, real-time autosave intervals.

The queue is updated whenever an event is scheduled or rescheduled (a vessel changes course, a mission timeline shifts, etc.).

### 4.2 Per-tick time-warp advancement

At each real-time sim-tick (every 33.33 ms), the game computes:

```
target_advance_ticks = warp_rate
next_event_ticks = ticks_until_top_of_event_queue()
actual_advance_ticks = min(target_advance_ticks, next_event_ticks)
```

The game then performs `actual_advance_ticks` of sim-tick advancement in this real-time interval. Each sim-tick advancement runs the 10-step cycle from §1.2.

When `actual_advance_ticks < target_advance_ticks` (an event is approaching), warp effectively reduces. When the event fires, the game processes the event, then continues warping at the requested rate.

This produces smooth time-warp behavior: warp can be very high in featureless cruise periods, naturally slows as events approach, hits events exactly, and resumes warp afterward.

### 4.3 Time-warp ceilings

Hard ceilings per physics mode at the active vessel's perspective:

- **PhysX-active:** 1x (one sim-tick advancement per real-time tick)
- **Kepler-rails:** 10,000x (analytic propagation can advance arbitrarily, but UI/render performance benefit decreases sharply beyond 10,000x)
- **Interstellar-cruise:** 100,000x (cruise is largely featureless; very high warp is the design intent for long-duration crossings)

If multiple vessels are at different modes, the warp ceiling is determined by the active vessel (the one with player focus, or the one with the most player attention via Mission Control's TV-screen selection).

### 4.4 Time-warp pause conditions

Time-warp pauses (drops to 0x or 1x) when:

- A transmission arrives that's configured to interrupt warp (per CONSTRAINTS §4 transmissions)
- A catastrophic failure event fires (per CONSTRAINTS §3 failure forensics — failures interrupt warp so the player can investigate)
- Mission Control opens (entering Mission Control pauses the game per CONSTRAINTS §7)
- The player explicitly pauses

Pause is a discrete operation: warp_rate becomes 0 (or 1x for PhysX-active vessels that need physics to keep running for collision-completion purposes).

---

## 5. Authority attribution and transfer

Every entity in the game (vessel, base, supply route, etc.) has an authority holder — which machine is running its simulation and producing its authoritative state.

### 5.1 Authority model

**Single-player.** The player's machine holds authority over everything. Every authority_holder_id field references the player's machine.

**Multiplayer.** Authority is distributed:

- **Vessels in PhysX-active mode:** Authority belongs to the machine actively simulating the vessel (typically the host's machine for "background" vessels; the player's machine for vessels they're flying).
- **Vessels in Kepler-rails or Interstellar-cruise:** Authority belongs to the machine that initiated the mode transition (typically the host).
- **Bases:** Authority belongs to the host's machine.
- **Supply routes:** Authority belongs to the host's machine.
- **Crew:** Authority belongs to the host's machine, except for crew currently on a player-controlled vessel (in which case authority is delegated to that player's machine).
- **World state (time, catalog, missions, research):** Authority belongs to the host's machine.

### 5.2 Authority transfer

Authority transfers happen when:

- A player focuses on a vessel that was previously host-controlled (authority transfers to the player's machine)
- A player un-focuses from a vessel (authority returns to host)
- A player joins or leaves the session (authority redistributes among remaining machines)
- A machine disconnects (authority for everything that machine held transfers to the host; if the host disconnects, the session ends and players are prompted to migrate or reconnect)

Authority transfer protocol:

1. The current authority holder sends a transfer message containing the entity's complete authoritative state at the current sim-tick.
2. The new authority holder acknowledges receipt and starts running the entity's simulation from the received state.
3. The previous holder ceases simulating the entity; subsequent state updates from the previous holder are ignored.
4. Conflict resolution: if a state update arrives from the previous holder after transfer, it's discarded (the new holder is canonical).

### 5.3 Conflict resolution

When two machines disagree about an entity's state (rare, but can happen during transition):

- The authority holder is canonical. Their state wins.
- Non-authority machines' state estimates are replaced by the authority holder's state at the next sim-tick.
- If two machines both claim authority (shouldn't happen but is a possible bug), the host's claim wins.

### 5.4 Save-load and authority

When a save is loaded, all authority defaults to the player's machine (single-player) or to the host's machine (multiplayer, host loading the save). Subsequent gameplay can transfer authority normally. Save files don't record authority assignments — they record only the authoritative state, and authority is reassigned on load.

This means saves are portable across multiplayer sessions: a save made in single-player can be loaded into multiplayer (with the loading machine becoming the host), and vice versa.

---

## 6. Replication protocol

In multiplayer, machines exchange authoritative state via replication. The protocol specifies what's sent, when, and at what rate.

### 6.1 Replication rate

State updates are sent at sim-tick rate (30 Hz) for entities with active changes. Idle entities (sleeping landed vessels, vessels on Kepler-rails with no scheduled events soon) do not send updates every tick.

### 6.2 Delta encoding

Replication uses delta encoding: rather than sending the full authoritative state each tick, only changed fields are sent. The first transmission after a connection is the full state; subsequent transmissions are deltas.

A delta for a vessel includes:

- `vessel_id` (identification)
- `sim_tick` (the tick this delta applies to)
- Changed fields only (e.g., position changed, velocity changed; mass unchanged so not included)
- Mode change indicators (if mode transitioned this tick)

Receivers apply deltas to their cached authoritative state and update their PhysX views accordingly.

### 6.3 Bandwidth budget

Target per-player bandwidth budget for multiplayer:

- **Upstream from each peer:** 50-200 KB/s typical, 500 KB/s peak.
- **Downstream to each peer:** 100-400 KB/s typical, 1 MB/s peak.

These budgets assume 4-player sessions with moderate vessel counts. Higher counts scale roughly linearly.

The protocol prioritizes:

1. Player-focused vessels (high update rate, full fidelity)
2. Nearby vessels (medium update rate)
3. Distant vessels and base state (low update rate or event-driven only)
4. Cruise vessels (event-driven only, since state changes are rare)

### 6.4 Late join and reconnection

When a player joins an active session (or reconnects after disconnect):

1. The host sends a full world state snapshot to the joining machine.
2. The joining machine initializes its world state from the snapshot.
3. Normal delta replication begins from the next sim-tick.

The snapshot is large (potentially 10-50 MB for a developed campaign) but transferred once per join.

---

## 7. Save format integration

Save files are serialized world state at a sim-tick boundary. The save format and the authoritative state schema are the same shape.

### 7.1 Save file structure

A save file contains:

```
SaveFile {
    save_format_version: SemVer           // For compatibility / migration
    game_version: SemVer                  // Game version that created the save
    save_created_at_real_time: Timestamp
    save_created_at_sim_tick: SimTickCount
    save_kind: SaveKind                   // QUICKSAVE | AUTOSAVE | NAMED
    
    // The complete world authoritative state
    world: WorldAuthoritativeState
    
    // Per CONSTRAINTS §2 commit 002 mode-lock
    save_mode: SaveMode                   // SINGLEPLAYER | MULTIPLAYER_HOST | MULTIPLAYER_CLIENT
}
```

Designs are not stored in saves — they're referenced by `design_id` and stored separately in the designs library. This preserves the mode-portability commitment.

### 7.2 Save and load procedure

**Save procedure:**

1. At a sim-tick boundary (between steps 9 and 10 of the sim-tick cycle), serialize the complete world state.
2. Write to a `.recovery` file alongside the target save file.
3. Atomic rename `.recovery` → target save file path (prevents corruption from interrupted writes).

**Load procedure:**

1. Read the save file. Validate save_format_version (refuse to load incompatible versions).
2. Deserialize world state.
3. Verify referenced design_ids exist in the designs library; warn or fail on missing.
4. Set current_sim_tick to the save's sim_tick.
5. Initialize PhysX for vessels in PhysX-active mode (recreate rigidbodies from state).
6. Resume normal sim-tick processing.

### 7.3 Save mechanics

Per CONSTRAINTS §2 (commit 023 save format extension):

- **Quicksave:** Single overwritten slot.
- **Autosave:** 5 rotating slots (configurable), triggered by event list plus real-time interval.
- **Named saves:** Player-titled, no auto-overwrite.

**Simulator-state saves use a separate sim-branch save space** (per commit 023). When the player is in Mission Control's simulator, save operations write to the sim-branch namespace; main save space is untouched. On simulator exit, the sim-branch namespace is cleared.

Performance budgets:

- Quicksave: ≤ 2 seconds on target hardware.
- Autosave: ≤ 5-10 seconds on target hardware (autosaves don't interrupt gameplay).

### 7.4 Save format versioning and migration

Save format follows semver:

- **Major version increment:** Incompatible changes. Old saves cannot load in new game version without migration.
- **Minor version increment:** Backward-compatible additions. Old saves load with default values for new fields.
- **Patch version increment:** Bug fixes. Saves load identically.

When the game loads an older-format save, it runs migration to upgrade to the current format. Migration produces a new save file at the current version; the original is preserved with a `.legacy` suffix.

When the game encounters a newer-format save (saved by a future game version), it refuses to load and prompts the player to upgrade the game.

---

## 8. Single-player as multiplayer degenerate case

This is the architectural commitment that prevents single-player from accumulating shortcuts that break multiplayer.

### 8.1 The principle

The game's networking architecture runs in all cases — including single-player. In single-player:

- There is exactly one machine in the "session."
- Authority for every entity belongs to that machine.
- Replication functions are called every sim-tick but have zero peers to send to (no-op).
- The state representation, authority model, and sim-tick boundary are identical to multiplayer.

In multiplayer, the only difference is that the session contains multiple machines, replication functions send to peers, and authority is distributed among them.

### 8.2 Why this matters

A non-degenerate single-player would have direct global state modifications, no authority tracking, no replication infrastructure. Adding multiplayer to such an architecture requires rewriting every state-modifying code path to think about authority and replication. This is the source of most networked physics games' multiplayer pain.

The discipline of "multiplayer-shaped architecture from day 1" means:

- Every state change goes through the authority-attribution model.
- Every state change triggers replication (no-op in single-player; sends to peers in multiplayer).
- Saves and network packets share infrastructure (one canonical state representation).
- Debugging is easier (every state change has a traceable owner).
- Adding multiplayer in v1.1 is a feature addition, not a rewrite.

### 8.3 v1 vs v1.1 implementation

In v1 (single-player only):

- All authority is implicitly the player's machine. Authority fields exist in the data but always reference machine ID 1.
- Replication infrastructure is present in code (replicate_state(), receive_state(), etc.) but with no peers configured.
- Network transport layer is stubbed out — methods exist but are no-ops.
- All other contract components (sim-tick boundary, mode transitions, time-warp, save format) are fully implemented.

In v1.1 (multiplayer added):

- Authority becomes meaningful: authority transfers happen between machines.
- Replication infrastructure starts having real peers to send to.
- Network transport layer is implemented (UDP-based with reliability layer; protocol TBD).
- All other contract components are unchanged.

Adding multiplayer in v1.1 is filling in the stubbed transport layer and activating the replication infrastructure that was already running. The architecture is the same; the implementation gains peers.

---

## 9. Edge cases and special situations

The contract specifies behavior for several edge cases that would otherwise be ambiguous.

### 9.1 Docked vessel mode transitions

If two vessels are docked and the player initiates a maneuver that would put them in different modes (rare but possible — e.g., docked tanker burning fuel that takes the combined vessel above orbital velocity), the docked pair transitions as a unit. The Kepler-rails entry uses the combined mass's orbital elements.

If a docked pair on Kepler-rails undocks, both vessels transition to PhysX-active for the undocking maneuver. After separation, each vessel re-evaluates its individual mode based on the standard trigger conditions.

### 9.2 Landed vessel approaching active vessel

A sleeping landed vessel wakes when an active vessel approaches within 50 km. The wake procedure activates PhysX simulation for the landed vessel; it remains landed (contact forces hold it in place); the active vessel can now interact with it (dock, transfer crew, etc.).

When the active vessel leaves the 50 km range, the landed vessel returns to sleep.

### 9.3 Time-warp during mode transition

If time-warp is active when a mode transition fires:

- The transition completes within the current sim-tick (it's a discrete event).
- Time-warp may adjust based on the new mode's ceiling (e.g., a Kepler-rails → PhysX-active transition forces warp to 1x).
- The event queue updates with the new mode's predictions.

### 9.4 Multi-vessel cluster activation

When multiple Kepler-rails vessels come within 50 km of each other (e.g., a constellation of satellites in formation), they all transition to PhysX-active for the duration of proximity. The decision rule:

- Compute the centroid of the cluster.
- Set the floating origin to that centroid.
- Activate PhysX for all vessels within 50 km of the centroid.
- When the cluster disperses (no two vessels within 50 km), each transitions back to its appropriate mode based on individual trigger conditions.

### 9.5 Host migration in multiplayer

If the host disconnects in multiplayer:

- The session is paused while clients negotiate a new host.
- The client with the most recent authoritative state becomes the new host.
- All authority redistributes to the new host's machine.
- Replication resumes.

If no client has recent authoritative state (all disconnected near-simultaneously), the session ends and players are prompted to reload from a save.

### 9.6 Save during multiplayer

In multiplayer, only the host can initiate saves (the host has the canonical world state). Other players see "host is saving" indicators. The save file is stored on the host's machine. If the host shares the save out-of-band, other players can load it in their own sessions.

This means multiplayer saves are essentially the host's save files. Players who want to preserve their own copies of campaign progress need to be the host or get save files from the host.

### 9.7 Concurrent player input on shared vessels

In multiplayer, two players can be looking at the same vessel but only one can have authority over it. If both attempt to provide input simultaneously:

- The authority holder's input is processed.
- Non-authority input is ignored (with optional UI warning to the non-authority player).
- Authority transfer happens explicitly (via UI), not implicitly from input.

---

## 10. Prototype scope and validation

The Phase 0 prototype validates that the contract works in practice. The prototype is not the v1 game; it's a minimal implementation that proves the architecture is sound.

### 10.1 Prototype components

The prototype must implement:

- Double-precision world coordinates with floating origin at 50 km threshold
- 30 Hz fixed sim-tick with the 10-step cycle from §1.2
- PhysX-active mode (Unity physics wrapper at double precision)
- Kepler-rails mode (analytic orbit propagation)
- Mode transitions: PhysX-active ↔ Kepler-rails (both directions, both trigger sets)
- Time-warp with event-prediction queue and min(warp_rate, ticks_to_event) discipline
- Authoritative state schema for vessels (PhysX-active and Kepler-rails modes)
- Save and load with the canonical state schema
- Authority attribution scaffolding (all authority is single-player; multiplayer transport stubbed)

The prototype does not need to implement:

- Interstellar-cruise mode (Phase 6+ work)
- Full vessel construction (use placeholder cube vessels)
- Mission planning, supply routes, crew management (Phase 5+ work)
- Procedural body generation (use placeholder spheres)
- UI beyond minimal debug overlay

### 10.2 Validation milestone

The prototype passes validation when this scenario works end-to-end:

1. A placeholder cube vessel launches from a planet surface (PhysX-active mode).
2. Cube reaches orbit (PhysX-active during ascent, transitions to Kepler-rails at altitude).
3. Cube time-warps through orbit (Kepler-rails advances; time-warp scales).
4. Cube transfers to a moon (Hohmann transfer; transition to moon's SOI; mode transitions correctly).
5. Cube captures into moon orbit (Kepler-rails around moon).
6. Cube lands on moon (transitions to PhysX-active at descent; lands with contact forces).
7. Save state is captured at any point and reloaded; behavior continues identically.
8. The total scenario runs deterministically when replayed from a save (within authoritative-state semantics — i.e., the orbital path, the timing, the success of operations are identical, even if individual PhysX cycles differ at floating-point precision).

If this scenario works, the contract is validated. The remaining Phase 1+ work fills in details and adds features, but the architecture is proven sound.

### 10.3 Validation criteria

The prototype validates:

- **Determinism:** Multiple runs from the same save produce identical authoritative state outcomes (orbital path, arrival times, success of operations).
- **Performance:** 30 Hz sim-tick maintains 60+ fps rendering with at least 10 simultaneous vessels in PhysX-active mode.
- **Save fidelity:** Save and reload produce identical authoritative state.
- **Mode transitions:** All transitions execute correctly without state corruption.
- **Time-warp:** Event-driven warp adjustment lands exactly on events; high warp doesn't break orbits.

If any criterion fails, the contract is refined and the prototype is updated. The contract is not finalized until all criteria pass on the prototype.

### 10.4 Estimated prototype effort

For one person working with AI assistance:

- Coordinate system + floating origin: 1-2 weeks
- 30 Hz sim-tick + 10-step cycle: 2-3 weeks
- PhysX-active mode + double-precision wrapper: 2-3 weeks
- Kepler-rails mode + orbit propagation: 2-3 weeks
- Mode transitions: 1-2 weeks
- Time-warp with event queue: 1-2 weeks
- Save/load: 1-2 weeks
- Validation milestone: 1 week

Total: 11-18 weeks of focused work. Within the Phase 1 weight estimate (3, i.e., 40-80 hours; the prototype overlaps with Phase 1 work substantially).

---

## 11. Migration plan from contract to Phase 1

When the prototype validates, Phase 1 begins. Phase 1 work uses the contract as the canonical specification for:

- Coordinate system implementation
- Reference frame hierarchy
- Patched conics
- Time architecture and sim-tick controller
- Three-mode physics architecture
- Vessel container with mode transitions
- Authoritative state schema (vessel data model)
- Save format

The prototype's code becomes the starting point for Phase 1's foundation work. Phase 1 expands the prototype's scope to handle more complex vessels, more parts, more events, more state — but the architecture is the architecture established here.

Changes to the contract during Phase 1 implementation are possible but require justification. If implementation reveals a flaw in the contract (an edge case wasn't covered, a performance assumption was wrong), the contract is updated and the change is documented. The prototype is the validation; the contract is the specification; both evolve together.

---

## 12. Cross-references

This contract operationalizes commitments from:

- **CONSTRAINTS §2 commit 002** (Foundational architectural principles): coordinate system, mode separation, sim-tick determinism, time-warp discipline, multiplayer authority, save format
- **CONSTRAINTS §2 commit 023** (Save mechanics implementation): save format integration
- **CONSTRAINTS §3 commit 004a** (Mode-portable designs and templates): design separation from save state
- **CONSTRAINTS §3 commit 011a** (Movie-moment mechanics): real telemetry that requires PhysX-active simulation
- **CONSTRAINTS §3 commit 024** (Flight computers): the mode-portable design's flight-computer routes
- **CONSTRAINTS §3 commit 025** (Atmospheric flight): atmospheric mode in PhysX-active
- **CONSTRAINTS §4 commit 002** (Multiplayer as shared universe): the multiplayer architectural commitments
- **CONSTRAINTS §9 Phase 0** (Build order): this contract as the deliverable

When CONSTRAINTS changes in ways that affect netcode, this contract is updated. When this contract changes, the relevant CONSTRAINTS section reference is updated to reflect the new commitment.

---

## 13. Open questions

The contract leaves these questions open for resolution during prototype implementation:

- **Sim-tick rate variability:** Is 30 Hz fixed across all hardware, or should it adapt to low-end hardware? Current commitment: fixed.
- **Event queue performance:** With many events scheduled, the event-prediction queue could become a bottleneck. Current expectation: events are sparse enough (thousands at most) that priority queue performance is non-issue.
- **PhysX precision at high velocity:** Atmospheric reentry velocities (>11 km/s) may produce numerical issues in single-precision PhysX. Current expectation: floating-origin keeps PhysX-active scenarios within tolerable precision bounds; revisit if validation fails.
- **Network transport protocol (v1.1):** UDP with custom reliability layer vs. existing libraries (LiteNetLib, Mirror, FishNet, etc.). Deferred to v1.1.
- **Authority transfer latency:** How quickly can authority transfer happen without visible glitches? Deferred to v1.1 prototype.

These questions don't block the v1 prototype (most are multiplayer-specific). They will be resolved as v1.1 work begins.

---

*End of contract.*
