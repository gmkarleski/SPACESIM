using System;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Mode-specific state for a vessel in <c>KeplerRails</c> mode, per
    /// <c>docs/NETCODE_CONTRACT.md</c> §2.3.
    ///
    /// Populated when a vessel is on analytic orbit-propagation rails around a single
    /// dominant gravity body. Cleared on transition to any other mode.
    ///
    /// The six classical orbital elements (a, e, i, Ω, ω, ν₀) plus the epoch tick define
    /// the orbit uniquely. Future propagation work computes position at an arbitrary
    /// sim-tick by advancing the true anomaly from ν₀ using Kepler's equation.
    /// </summary>
    public sealed class KeplerState
    {
        /// <summary>Semi-major axis a, in meters. Negative for hyperbolic trajectories.</summary>
        public double SemiMajorAxis;

        /// <summary>Eccentricity e (dimensionless). 0 = circular, 0&lt;e&lt;1 = elliptical, e=1 = parabolic, e&gt;1 = hyperbolic.</summary>
        public double Eccentricity;

        /// <summary>Inclination i relative to the reference plane, in radians [0, π].</summary>
        public double Inclination;

        /// <summary>
        /// Longitude of ascending node Ω, in radians [0, 2π). Undefined for equatorial orbits
        /// (i = 0 or i = π); convention used is Ω = 0 in those cases.
        /// </summary>
        public double LongitudeOfAscendingNode;

        /// <summary>
        /// Argument of periapsis ω, in radians [0, 2π). Undefined for circular orbits (e = 0);
        /// convention used is ω = 0 in those cases.
        /// </summary>
        public double ArgumentOfPeriapsis;

        /// <summary>
        /// True anomaly at epoch ν₀, in radians [0, 2π). Position of the vessel along the
        /// orbit at <see cref="EpochTick"/>.
        /// </summary>
        public double TrueAnomalyAtEpoch;

        /// <summary>Sim-tick at which the orbital elements were captured (epoch).</summary>
        public long EpochTick;

        /// <summary>
        /// UUID of the body whose gravity defines this orbit. Used when the vessel
        /// re-activates to PhysX-active mode to determine the reference frame.
        /// </summary>
        public Guid ReferenceBodyId;

        /// <summary>
        /// Pre-computed sim-tick of next periapsis passage. Null if not yet computed.
        ///
        /// PHASE 0 SCOPE: left null. Event predictions land alongside the analytic
        /// propagator in a future commit. The contract field is preserved here so the
        /// schema is complete and downstream code can read it (defensively handling null).
        /// </summary>
        public long? NextPeriapsisTick;

        /// <summary>
        /// Pre-computed sim-tick of next apoapsis passage. Null if not yet computed.
        /// PHASE 0 SCOPE: left null (see <see cref="NextPeriapsisTick"/>).
        /// </summary>
        public long? NextApoapsisTick;

        /// <summary>
        /// Pre-computed sim-tick of next SOI transition — earliest of outward exit
        /// from the current body's SOI or inward entry into one of its children's
        /// SOIs. Populated by <see cref="VesselEventPredictionDriver"/> via
        /// <see cref="SoiCrossingPredictor"/> (commit 046). Predicted at each
        /// sim-tick for KeplerRails vessels with non-null KeplerState. Null when no
        /// crossing is predicted in the lookahead horizon (one orbital period for
        /// elliptical, ~1 game year for hyperbolic), when the current body has
        /// infinite SOI (top-level convention), or when the orbit is fully
        /// contained within the SOI.
        /// </summary>
        public long? NextSoiTransitionTick;

        /// <summary>
        /// Pre-computed sim-tick at which the vessel's trajectory enters the
        /// atmospheric boundary of the current reference body. Populated by
        /// <see cref="VesselEventPredictionDriver"/> via
        /// <see cref="AtmosphericEntryPredictor"/> (commit 048 field split). Predicted
        /// at each sim-tick for KeplerRails vessels with non-null KeplerState.
        ///
        /// Null when the body is a vacuum body (atmospheric-top altitude = 0), when
        /// the orbit is fully above the atmospheric boundary, or when the overflow
        /// defense kicked in for very-long-period configurations.
        ///
        /// <para>
        /// As of commit 048, atmospheric entry and surface impact each get their own
        /// dedicated field rather than being aggregated into a single
        /// <c>NextModeTransitionTick</c>. This lets the trigger evaluator distinguish
        /// the two events at runtime and fire the correct
        /// <see cref="TransitionTriggerReason"/> value, eliminating the label
        /// imprecision that commit 047's aggregation introduced.
        /// </para>
        /// </summary>
        public long? NextAtmosphericEntryTick;

        /// <summary>
        /// Pre-computed sim-tick at which the vessel's trajectory intersects the
        /// surface of the current reference body. Populated by
        /// <see cref="VesselEventPredictionDriver"/> via
        /// <see cref="SurfaceImpactPredictor"/> (commit 048 field split). Predicted at
        /// each sim-tick for KeplerRails vessels with non-null KeplerState.
        ///
        /// Null when the orbit does not intersect the body's surface (periapsis above
        /// surface radius), or when the overflow defense kicked in for very-long-period
        /// configurations.
        ///
        /// <para>
        /// Companion field to <see cref="NextAtmosphericEntryTick"/>. The two were
        /// previously aggregated into a single <c>NextModeTransitionTick</c> field;
        /// the split lets the trigger evaluator fire
        /// <see cref="TransitionTriggerReason.SurfaceImpactPredicted"/> distinctly
        /// from <see cref="TransitionTriggerReason.AtmosphericEntryPredicted"/>.
        /// </para>
        /// </summary>
        public long? NextSurfaceImpactTick;
    }
}
