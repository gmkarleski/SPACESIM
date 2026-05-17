using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Mode-specific state for a vessel in <c>InterstellarCruise</c> mode, per
    /// <c>docs/NETCODE_CONTRACT.md</c> §2.4.
    ///
    /// PHASE 0 STUB: the field shape is locked here so the schema is complete and the type
    /// compiles, but no Phase 0 code path constructs or reads <see cref="CruiseState"/>.
    /// Interstellar-cruise mode is post-launch per the Phase 0 artifact list (commit 037);
    /// the actual implementation arrives in Phase 6.
    ///
    /// All fields default to zero / empty. The <see cref="TransmissionQueue"/> field is typed
    /// as <c>List&lt;object&gt;</c> as a placeholder; the real type becomes
    /// <c>List&lt;DelayedTransmission&gt;</c> when the transmissions module lands (per
    /// commit 004b transmission framework).
    /// </summary>
    public sealed class CruiseState
    {
        /// <summary>Position in galactic coordinates (light-years from galactic origin).</summary>
        public double3 PositionGalactic;

        /// <summary>Velocity vector in galactic coordinates (proper motion).</summary>
        public double3 VelocityGalactic;

        /// <summary>UUID of the origin star system for the current cruise leg.</summary>
        public Guid DepartureSystemId;

        /// <summary>UUID of the destination star system for the current cruise leg.</summary>
        public Guid ArrivalSystemId;

        /// <summary>Sim-tick at which the current cruise leg began.</summary>
        public long DepartureTick;

        /// <summary>Predicted sim-tick of SOI capture at the destination star.</summary>
        public long ArrivalTick;

        /// <summary>Cruise velocity as a fraction of the speed of light.</summary>
        public double CruiseVelocityC;

        /// <summary>Ratio of crew (proper) time to galactic (coordinate) time.</summary>
        public double TimeDilationFactor;

        /// <summary>Seconds of crew subjective time elapsed since departure.</summary>
        public double CrewSubjectiveTimeElapsed;

        /// <summary>Sim-tick when the most recent transmission was received from the vessel.</summary>
        public long LastTransmissionReceivedTick;

        /// <summary>
        /// Queued transmissions awaiting light-speed-delayed delivery to/from the vessel.
        /// Stub type; becomes <c>List&lt;DelayedTransmission&gt;</c> when the transmissions
        /// module lands (per commit 004b transmission framework).
        /// </summary>
        public List<object> TransmissionQueue = new List<object>();
    }
}
