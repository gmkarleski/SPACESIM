using System;

namespace SpaceSim.Foundation.Bodies
{
    /// <summary>
    /// Canonical body-state schema, per docs/world/PROCGEN_DESIGN.md
    /// (commits 059 / 060 / 061).
    ///
    /// Sealed POCO with composition-all-the-way-down shape: nine
    /// physics-universal flat fields plus nullable domain sub-object
    /// references. Body-type asymmetry (star vs planet vs moon vs
    /// asteroid) is expressed by which sub-objects are populated,
    /// not by class hierarchy.
    ///
    /// PHASE 2 SCOPE (commit 062): four sub-objects (StellarEmission,
    /// StellarActivity, StellarComposition, OrbitalDynamics)
    /// implemented per 060 / 061 designs. Additional sub-objects
    /// (planetary bulk, atmosphere, hydrosphere, magnetosphere,
    /// geology, terrain, biomes, resources, feature subsystems,
    /// detection signatures) land in subsequent commits when their
    /// per-sub-object designs lock.
    /// </summary>
    public sealed class BodyState
    {
        // ----- Shared flat fields (locked at 059, revised at 060) -----

        /// <summary>Stable across save/load. Used for cross-references
        /// (ParentBodyId, OrbitalDynamicsState.ReferenceBodyId).</summary>
        public Guid BodyId;

        /// <summary>Per-body seed for deterministic regeneration.
        /// Semantics per commit 058 SEED_VERSIONING.md. Type locked at
        /// commit 062 as ulong (standard procgen seed type; 64-bit
        /// space comfortable for hierarchical derivation per Phase 7
        /// Layers 1-4).</summary>
        public ulong Seed;

        /// <summary>Player-facing name. Hand-tuned for home-system
        /// bodies; procgen-generated for galaxy-beyond bodies.</summary>
        public string Name = string.Empty;

        /// <summary>Parent body in the SOI hierarchy. Guid.Empty for
        /// top-level bodies (the star at the root of a star system).
        /// Per commit 057a sentinel-convention semantics (in-code Guid
        /// + Guid.Empty rather than Option&lt;BodyID&gt;).</summary>
        public Guid ParentBodyId;

        /// <summary>Body mass in kilograms.</summary>
        public double Mass;

        /// <summary>Body radius in meters. For stars, the photospheric
        /// radius.</summary>
        public double Radius;

        /// <summary>Sphere-of-influence radius in meters.
        /// double.PositiveInfinity for top-level bodies (no parent;
        /// no SOI boundary within the system).</summary>
        public double SoiRadius;

        /// <summary>Rotation rate in radians per second. Intrinsic;
        /// applies to every body including top-level stars.</summary>
        public double RotationRate;

        /// <summary>Axial tilt in radians. Intrinsic.</summary>
        public double AxialTilt;

        // ----- Nullable domain sub-objects (commits 060 / 061) -----

        /// <summary>Stellar emission state. Populated for emitting
        /// bodies (stars, brown dwarfs, neutron stars, pulsars).
        /// Null for non-emitting bodies. Per commit 060 D2.</summary>
        public StellarEmissionState StellarEmission;

        /// <summary>Stellar activity state. Populated for bodies with
        /// magnetic / dynamical activity (typically the same bodies
        /// that populate StellarEmission). Null for inactive bodies.
        /// Per commit 060 D2.</summary>
        public StellarActivityState StellarActivity;

        /// <summary>Stellar composition state. Populated for stellar
        /// and sub-stellar bodies. Null for non-stellar bodies.
        /// Per commit 060 D2.</summary>
        public StellarCompositionState StellarComposition;

        /// <summary>Orbital dynamics state. Populated for orbiting
        /// bodies; null for top-level bodies (the star at the root
        /// of a star system). Per commit 061 D2.</summary>
        public OrbitalDynamicsState OrbitalDynamics;

        // PHASE 2+ DEFERRED SUB-OBJECTS (commit 063+):
        //   - PlanetaryBulk (commit 063 when Earth-equivalent enters)
        //   - Atmosphere / Hydrosphere / Magnetosphere
        //   - Geology / Terrain / Biomes / Resources
        //   - Feature subsystems (Rings, Auroras, Volcanism, etc.)
        //   - DetectionSignatures
        // Each lands as its own per-sub-object design + implementation
        // commit per the established pattern.
    }
}
