using System;

namespace SpaceSim.Foundation.Bodies
{
    /// <summary>
    /// A body's orbital state relative to its parent body, per commit
    /// 061 D2 / D3.
    ///
    /// Six classical orbital elements (a, e, i, Ω, ω, ν₀) plus epoch
    /// tick plus reference body identifier, plus TidalLockState as a
    /// rotation-orbit relational property. Matches Foundation.Vessels'
    /// KeplerState orbital-identity field structure but is a distinct
    /// type — body orbital state is stable (no event-prediction caches
    /// at this layer); vessel KeplerState carries vessel-runtime caches
    /// that don't fit body schema.
    ///
    /// Populated for orbiting bodies (planets around stars, moons around
    /// planets, asteroids in orbits). Null for top-level bodies (the
    /// star at the root of a star system, whose ParentBodyId is
    /// Guid.Empty).
    ///
    /// Per commit 061 D3, body event-prediction caches (when does this
    /// body next eclipse another body, etc.) are NOT included on this
    /// type. They're runtime / driver concerns, not body-state. If body
    /// event prediction becomes useful in a future phase, a separate
    /// sub-object (OrbitalEventCache or similar) gets designed when
    /// the use case is concrete.
    ///
    /// Phase 7 binary-system handling: ReferenceBodyId points at a
    /// single parent body for Phase 2. Binary stars / binary-asteroid
    /// systems where bodies orbit a barycenter are deferred to Phase 7
    /// per commit 061 alternatives-rejected.
    /// </summary>
    public sealed class OrbitalDynamicsState
    {
        /// <summary>Semi-major axis a, in meters. Negative for
        /// hyperbolic trajectories (rare for bodies but possible for
        /// captured/escaping cases).</summary>
        public double SemiMajorAxis;

        /// <summary>Eccentricity e, dimensionless. 0 = circular,
        /// 0 &lt; e &lt; 1 = elliptical, e ≥ 1 = hyperbolic.</summary>
        public double Eccentricity;

        /// <summary>Inclination i, in radians [0, π]. Relative to the
        /// parent body's reference plane.</summary>
        public double Inclination;

        /// <summary>Longitude of ascending node Ω, in radians [0, 2π).
        /// Per OrbitalElements.cs convention: Ω = 0 for equatorial
        /// orbits.</summary>
        public double LongitudeOfAscendingNode;

        /// <summary>Argument of periapsis ω, in radians [0, 2π).
        /// Per OrbitalElements.cs convention: ω = 0 for circular
        /// orbits.</summary>
        public double ArgumentOfPeriapsis;

        /// <summary>True anomaly at epoch ν₀, in radians [0, 2π).
        /// Position of the body along its orbit at EpochTick.</summary>
        public double TrueAnomalyAtEpoch;

        /// <summary>Sim-tick at which these orbital elements were
        /// captured. Subsequent positions computed by propagating
        /// from this tick.</summary>
        public long EpochTick;

        /// <summary>UUID of the parent body whose gravity defines this
        /// orbit. Same field semantics as
        /// SpaceSim.Foundation.Vessels.KeplerState.ReferenceBodyId.
        /// Guid.Empty is invalid for OrbitalDynamicsState (top-level
        /// bodies don't have orbits and their BodyState.OrbitalDynamics
        /// field is null instead). Validation of this invariant lives
        /// in pipeline code, not in this schema type, per the
        /// 059-locked schema-vs-code principle: schema documents the
        /// contract; enforcement lives in the code that produces /
        /// consumes schema instances.</summary>
        public Guid ReferenceBodyId;

        /// <summary>True if the body is tidally locked to its parent
        /// (rotation period equals orbital period; same face permanently
        /// toward parent). False otherwise. Binary representation per
        /// commit 061 D2 — Phase 2 home-system tidal cases are all 1:1
        /// or none. Richer representations (resonance ratio, libration)
        /// migrate when Phase 7 procgen demands them.</summary>
        public bool TidalLockState;
    }
}
