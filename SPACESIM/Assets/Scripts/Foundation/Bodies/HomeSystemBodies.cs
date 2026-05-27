using System;

namespace SpaceSim.Foundation.Bodies
{
    /// <summary>
    /// Hand-tuned BodyState factory for home-system bodies.
    ///
    /// Per commit 058 D3, the home system's bodies are produced with
    /// hand-tuned PARAMETERS through the same machinery as procgen-
    /// generated bodies. This class encapsulates those hand-tuned
    /// values. Sibling factories for home planet, home moon, Mars-
    /// equivalent, Saturn-equivalent land in subsequent commits per
    /// the four-intensive-bodies scope from CONSTRAINTS §3 commit 021.
    ///
    /// COMMIT 062 SCOPE: Sol-equivalent only. Other home-system body
    /// factories follow when their per-body designs lock and the
    /// pipeline cascade infrastructure supports cross-body dependencies
    /// (e.g., Earth-equivalent's thermal profile depends on Sol-
    /// equivalent's luminosity, so it can't be cleanly hand-tuned in
    /// isolation; lands with cascade infrastructure).
    ///
    /// Per commit 058 D3 alternatives-rejected: derived stellar values
    /// (luminosity, surface_temperature, spectral_peak, stellar_type,
    /// radius) are INFORMATIONAL — they describe what Sol-equivalent
    /// should look like, not regression targets. Stage 1 implementation
    /// (a later commit) will choose derivation formulas. For commit 062,
    /// these values are hand-tuned to match real solar parameters; the
    /// formulas don't exist yet.
    /// </summary>
    public static class HomeSystemBodies
    {
        /// <summary>
        /// Construct a Sol-equivalent BodyState — the home star of the
        /// Phase 2 home system. Top-level body (no parent;
        /// SoiRadius = PositiveInfinity; OrbitalDynamics = null).
        ///
        /// Values per commit 058 D3 hand-tuned input + informational
        /// derived. Real solar parameters because the home system is
        /// Earth-equivalent per CONSTRAINTS §3 line 435.
        /// </summary>
        /// <param name="bodyId">UUID for this body instance. Defaults
        /// to Guid.NewGuid() if not specified. For deterministic test
        /// scenarios, pass a fixed value.</param>
        /// <returns>A fully-populated BodyState representing
        /// Sol-equivalent.</returns>
        public static BodyState Sun(Guid bodyId = default)
        {
            if (bodyId == default)
            {
                bodyId = Guid.NewGuid();
            }

            return new BodyState
            {
                // ----- Universal flat fields -----

                BodyId = bodyId,
                Seed = 0UL,                  // Hand-tuned: Sol-equivalent
                                             // uses fixed seed 0UL as system
                                             // anchor. 0UL is a DELIBERATE
                                             // FIXED VALUE here, NOT an
                                             // absence-of-seed sentinel —
                                             // procgen bodies use derived
                                             // non-zero ulong seeds when
                                             // Phase 7 Layers 1-4 land
                                             // hierarchical seed derivation.
                Name = "Sun",
                ParentBodyId = Guid.Empty,   // Top-level body
                Mass = 1.989e30,             // kg — real Sun mass
                Radius = 6.96e8,             // m — real Sun radius
                SoiRadius = double.PositiveInfinity,  // Top-level convention
                RotationRate = 2.864e-6,     // rad/s — converted from real
                                             // Sun's 25.4-day equatorial
                                             // rotation period per Finding A
                                             // (2π / (25.4 × 86400))
                AxialTilt = 0.1265,          // rad — real Sun axial tilt
                                             // ≈ 7.25° (per Finding B,
                                             // hand-tuned for Phase 2 since
                                             // 058's contract doesn't
                                             // enumerate axial_tilt as a
                                             // Layer 3 input)

                // ----- Stellar sub-objects (Sol-equivalent: all populated) -----

                StellarEmission = new StellarEmissionState
                {
                    Luminosity = 3.828e26,         // W — real Sun
                    SurfaceTemperature = 5778.0,   // K — real Sun
                    SpectralPeak = 5.02e-7,        // m — real Sun
                                                   // (~502 nm visible peak)
                    StellarType = "G2V",           // Specific subclass
                    SpectralClass = "G",           // Broad letter
                },

                StellarActivity = new StellarActivityState
                {
                    MagneticActivityIndex = 0.3,   // Mid-band of real Sun's
                                                   // 0.1-0.5 cycle variation
                    FlareFrequency = 0.0,          // Quiescent at v1
                                                   // per 058 D3 framing
                },

                StellarComposition = new StellarCompositionState
                {
                    Age = 4.6e9,        // years — real Sun age
                    Metallicity = 0.0,  // Sol-equivalent is the [Fe/H]
                                        // reference
                },

                // ----- Orbital dynamics: null (top-level body) -----

                OrbitalDynamics = null,
            };
        }
    }
}
