using System;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Mode-portable vessel design, per <c>docs/NETCODE_CONTRACT.md</c> §2.5.
    ///
    /// PHASE 0 STUB: only the <see cref="DesignId"/> field is implemented. The full
    /// VesselDesign (part tree, mass calculations, design history, vizzy scripts, flight
    /// computer routes) lands in Phase 2 when vessel construction work begins.
    ///
    /// The stub exists so <see cref="VesselAuthoritativeState.DesignId"/> has a real type to
    /// reference. Phase 0 vessels share a default design ID; Phase 2 will introduce real
    /// distinct designs and the design-store mechanism.
    ///
    /// Per §2.5, designs are SEPARATE from authoritative state — saves reference designs by
    /// ID and the design file loads separately. This stub establishes the design-as-its-own-
    /// concept architecture even though the design store doesn't exist yet.
    /// </summary>
    public sealed class VesselDesign
    {
        /// <summary>Permanent unique identifier for this design.</summary>
        public Guid DesignId;

        // PHASE 2 FIELDS (deferred):
        //   public string DesignName;
        //   public DateTime CreatedAtRealTime;
        //   public DateTime ModifiedAtRealTime;
        //   public List<PartInstance> Parts;
        //   public Guid RootPartId;
        //   public double MassDryKg;
        //   public double DeltaVEstimate;
        //   public ulong LaunchCount;
        //   public ulong SuccessCount;
        //   public List<FailureMode> FailureModesEncountered;
        //   public List<ModificationEntry> Modifications;
        //   public List<VizzyScript> VizzyScripts;
        //   public List<FlightRoute> FlightComputerRoutes;
        //
        // Each of these requires either Phase 2 vessel-construction work or later-phase
        // module integration (Vizzy in Phase 5, flight computers in Phase 5).
    }
}
