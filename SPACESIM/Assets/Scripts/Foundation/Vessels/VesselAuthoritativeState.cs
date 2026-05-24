using System;
using System.Collections.Generic;
using SpaceSim.Foundation.SimTick;
using Unity.Mathematics;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Canonical authoritative state for a single vessel, per
    /// <c>docs/NETCODE_CONTRACT.md</c> §2.1.
    ///
    /// This is the data shape that serves as save-file representation, network replication
    /// payload, and runtime in-memory state. One canonical representation across all three
    /// purposes, per §2's framing.
    ///
    /// Common fields (identity, ownership, mass properties, resources, crew, telemetry)
    /// apply across all modes. Exactly one of <see cref="PhysXState"/>,
    /// <see cref="KeplerState"/>, <see cref="CruiseState"/> is non-null at any time,
    /// determined by <see cref="Mode"/>.
    ///
    /// PHASE 0 SCOPE: identity, mode, mass properties, and the three mode-specific state
    /// pointers are fully wired. Owner/authority fields, resource inventory, crew, and
    /// telemetry are stubs (defaulted to empty / sentinel values) until their owning
    /// systems land in later phases. The stub fields are present so the type's structure
    /// matches the contract; downstream code can already read them without crashing, even
    /// though Phase 0 always sees defaults.
    /// </summary>
    public sealed class VesselAuthoritativeState
    {
        // ----- Identity -----

        /// <summary>Permanent unique identifier for this vessel. Stable across save/load.</summary>
        public Guid VesselId;

        /// <summary>
        /// Reference to the mode-portable <see cref="VesselDesign"/> (separate from
        /// authoritative state per §2.5). Saves reference designs by ID; designs load
        /// separately.
        /// </summary>
        public Guid DesignId;

        /// <summary>Player-facing name. Defaults to empty string until set.</summary>
        public string Name = string.Empty;

        // ----- Authority and ownership -----

        /// <summary>
        /// UUID of the owning agency (multiplayer concept; single-player has exactly one
        /// agency). PHASE 0 STUB: defaults to <see cref="Guid.Empty"/>; agency system lands
        /// in Phase 2+ alongside organization / multi-program work.
        /// </summary>
        public Guid OwnerAgencyId;

        /// <summary>
        /// Network ID of the machine currently holding authority for this vessel.
        /// PHASE 0 STUB: typed as string for now; becomes <c>NetworkID</c> when the
        /// multiplayer authority system lands (per netcode contract §5, post-v1).
        /// Single-player Phase 0 leaves this as empty string.
        /// </summary>
        public string AuthorityHolderId = string.Empty;

        // ----- Current physics mode -----

        /// <summary>Which physics mode this vessel is currently in.</summary>
        public PhysicsMode Mode;

        /// <summary>Sim-tick at which the current <see cref="Mode"/> was entered.</summary>
        public long ModeEnteredAtTick;

        // ----- Mass properties (computed from parts, cached) -----

        /// <summary>
        /// Total vessel mass in kilograms. Computed from part inventory when a vessel
        /// design is built; PHASE 0 vessels default to 1000 kg for orbital math.
        /// </summary>
        public double TotalMassKg = 1000.0;

        /// <summary>
        /// Center of mass relative to vessel origin, in vessel-local coordinates.
        /// PHASE 0: defaults to zero (treat the vessel as a point mass at its transform).
        /// </summary>
        public double3 CenterOfMassLocal;

        /// <summary>
        /// Principal axis moments of inertia, kg·m². PHASE 0: defaults to zero (no
        /// rotational dynamics in Phase 0 test cases).
        /// </summary>
        public double3 MomentsOfInertia;

        // ----- Resources (per-part inventory aggregated) -----

        /// <summary>
        /// Per-resource current amount in vessel's tanks. PHASE 0 STUB: empty dictionary.
        /// Real resource accounting lands in Phase 5+ alongside the supply-line system.
        /// Keys will be resource type names; values will become a richer struct (amount,
        /// capacity, storage-location) when the supply system needs them.
        /// </summary>
        public Dictionary<string, double> ResourceInventory = new Dictionary<string, double>();

        // ----- Crew assignment (vessel-physical, per commit 013) -----

        /// <summary>
        /// UUIDs of named crew currently aboard this vessel. PHASE 0 STUB: empty list.
        /// Real crew tracking lands in Phase 5 alongside mission / crew systems.
        /// </summary>
        public List<Guid> CrewAboard = new List<Guid>();

        // ----- Routine-supply classification (commit 048 Stage 1 framing; field migrated from Vessel MonoBehaviour at commit 057a per DECISIONS D1) -----

        /// <summary>
        /// Whether this vessel is classified as routine supply. Routine vessels
        /// skip warp halts on SOI crossings (expected, repetitive, non-terminal
        /// for supply-run profiles); they still halt on terminal / mass-affecting
        /// events (surface impact, maneuver execution) and on atmospheric entry
        /// (aerodynamic engagement matters regardless of classification).
        /// Default false. Per NETCODE_CONTRACT §2.1.
        ///
        /// <para>
        /// Persistent state — the player's marking of a vessel as routine supply
        /// survives save/load. Originally placed on the
        /// <see cref="SpaceSim.Foundation.Vessels.Vessel"/> MonoBehaviour at
        /// commit 048 Stage 1; migrated here at commit 057a per DECISIONS.md
        /// commit 057 entry D1, which supersedes the commit 048 entry (e)
        /// field-location decision now that save/load is architecturally
        /// load-bearing per commit 055 D3.
        /// </para>
        /// </summary>
        public bool IsRoutineSupply;

        // ----- Mode-specific state (exactly one of) -----

        /// <summary>Non-null when <see cref="Mode"/> is <see cref="PhysicsMode.PhysXActive"/>.</summary>
        public PhysXState PhysXState;

        /// <summary>Non-null when <see cref="Mode"/> is <see cref="PhysicsMode.KeplerRails"/>.</summary>
        public KeplerState KeplerState;

        /// <summary>
        /// Non-null when <see cref="Mode"/> is <see cref="PhysicsMode.InterstellarCruise"/>.
        /// PHASE 0 STUB: always null. Interstellar-cruise mode is deferred to Phase 6 per
        /// the Phase 0 artifact list (commit 037).
        /// </summary>
        public CruiseState CruiseState;

        // ----- Telemetry buffer (per commit 013 failure forensics) -----

        /// <summary>
        /// Circular buffer of recent telemetry samples for failure-forensics replay.
        /// PHASE 0 STUB: typed as <c>List&lt;object&gt;</c>; becomes
        /// <c>CircularBuffer&lt;TelemetrySample&gt;</c> when the telemetry module lands.
        /// </summary>
        public List<object> TelemetryBuffer = new List<object>();

        // ----- Last sim-tick this state was advanced -----

        /// <summary>
        /// Sim-tick at which this vessel's state was last advanced (PhysX integration,
        /// Kepler propagation, etc.). Used to detect skipped ticks and to validate
        /// save/load consistency.
        /// </summary>
        public long LastAdvancedTick;
    }
}
