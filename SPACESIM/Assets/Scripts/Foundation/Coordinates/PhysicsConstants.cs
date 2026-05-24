namespace SpaceSim.Foundation.Coordinates
{
    /// <summary>
    /// Central definitions for physics-scale constants used across the project.
    /// Replaces inline declarations that previously lived in 11 sites
    /// (1 production default in <c>ReferenceBody</c> + 10 test files each with
    /// their own <c>private const double EarthMassKg = 5.972e24;</c>) plus one
    /// inline <c>5.972e24</c> literal in <c>BodyRegistryTests</c>.
    ///
    /// <para>
    /// <strong>FILE HOME:</strong> lives in <c>SpaceSim.Foundation.Coordinates</c>
    /// alongside <see cref="CoordinateMath"/> rather than in
    /// <c>SpaceSim.Foundation.Vessels</c>. Reason: physical constants are
    /// substrate-level — they predate the Vessels asmdef in the dependency
    /// graph (Vessels references Coordinates, not the reverse). Test files in
    /// other asmdefs can reach Coordinates from anywhere; the Vessels namespace
    /// already imports <c>SpaceSim.Foundation.Coordinates</c> for
    /// <see cref="WorldPosition"/> and friends, so no new <c>using</c>
    /// directives are needed in production code that consumes these constants.
    /// </para>
    ///
    /// <para>
    /// <strong>RELATIONSHIP TO <see cref="CoordinateMath"/>.G:</strong> the
    /// gravitational constant G stays in <see cref="CoordinateMath"/> (its
    /// existing home), not duplicated here. <see cref="EarthMu"/> is computed
    /// as <c>CoordinateMath.G · EarthMassKg</c> matching the existing test
    /// pattern, so the project has one source-of-truth for G even though the
    /// derived μ-values live in <c>PhysicsConstants</c>.
    /// </para>
    ///
    /// <para>
    /// <strong>SCOPE OF THIS FILE:</strong> contains exactly the constants
    /// that have been duplicated across the codebase plus the
    /// degenerate-orbit threshold introduced in commit 053. Three additional
    /// constants in <c>VesselTestHelpers</c> (<c>EarthMoonDistanceMeters</c>,
    /// <c>MoonMassKg</c>, <c>MoonSoiRadiusMeters</c>) are deferred to fixer-bot
    /// migration — the helper file's pattern of test-side constants doesn't
    /// need to move now; that migration is small and adjacent rather than
    /// on the commit 053 critical path.
    /// </para>
    ///
    /// <para>
    /// <strong>FUTURE EXPANSION:</strong> as additional physics constants
    /// accumulate (Sun mass, planet masses for the home system's other
    /// bodies, atmospheric scale-height constants), they land here.
    /// </para>
    /// </summary>
    public static class PhysicsConstants
    {
        // ----- Body masses -----

        /// <summary>
        /// Earth mass in kilograms (5.972 × 10²⁴ kg). Project-wide
        /// canonical value used by <see cref="ReferenceBody"/>'s default
        /// and by every test file that previously declared this constant
        /// locally.
        /// </summary>
        public const double EarthMassKg = 5.972e24;

        /// <summary>
        /// Moon mass in kilograms (7.342 × 10²² kg). Used by the
        /// <c>TestVessels.unity</c> Moon ReferenceBody (per commit 051) and
        /// by any test that constructs a Moon-equivalent body.
        /// </summary>
        public const double MoonMassKg = 7.342e22;

        // ----- Sphere-of-influence radii -----

        /// <summary>
        /// Moon sphere-of-influence radius in meters (66,183 km — the real
        /// Moon's SOI relative to Earth per patched-conics convention).
        /// Used by the <c>TestVessels.unity</c> Moon ReferenceBody (per
        /// commit 051) and by any test that constructs a Moon-equivalent
        /// body with finite SOI.
        /// </summary>
        public const double MoonSoiRadiusMeters = 6.6183e7;

        // ----- Derived gravitational parameters -----

        /// <summary>
        /// Earth's standard gravitational parameter μ = G · M, in m³/s².
        /// Computed once at type initialization from <see cref="CoordinateMath.G"/>
        /// and <see cref="EarthMassKg"/>. Used by orbital-element computations
        /// against Earth-equivalent bodies in test code.
        ///
        /// <para>
        /// Declared <c>static readonly</c> rather than <c>const</c> because
        /// the product of two <c>double</c> consts is not a compile-time
        /// constant expression in C# (language quirk — the product is
        /// computed at runtime even though both operands are consts). For
        /// callers this is invisible; the value resolves once on first
        /// access and stays fixed.
        /// </para>
        /// </summary>
        public static readonly double EarthMu = CoordinateMath.G * EarthMassKg;

        // ----- Solver-stability thresholds -----

        /// <summary>
        /// Relative-form threshold for detecting degenerate-orbit
        /// (nearly purely-radial) trajectories during
        /// <c>Vessel.TransitionToKeplerRails</c>. If
        /// |h|² &lt; <c>DegenerateOrbitAngularMomentumSquaredScale · μ · |r|</c>,
        /// the transition refuses with
        /// <c>TransitionResult.FailedDegenerateOrbit</c>.
        ///
        /// <para>
        /// <strong>WHY RELATIVE FORM:</strong> a single absolute threshold
        /// (say, |h|² &lt; 1e-6) would be unusable across reference bodies
        /// of differing μ and orbits at differing radial distance — a
        /// circular LEO around Earth has |h|² ≈ 2.79 × 10²¹, while a
        /// circular orbit around a small body has dramatically smaller |h|².
        /// The relative form <c>scale · μ · |r|</c> stays well-behaved
        /// across these scales: it captures "much smaller than what a
        /// reasonable orbit at this scale would produce" rather than
        /// "smaller than some fixed number."
        /// </para>
        ///
        /// <para>
        /// <strong>VALUE CHOSEN: 1e-10.</strong> Matches the existing
        /// project convention in <c>OrbitalElements</c>
        /// (<c>CircularThreshold</c>, <c>EquatorialThreshold</c>,
        /// <c>KeplerConvergenceTolerance</c> all use 1e-10). The semantic
        /// is slightly different from those (they're absolute thresholds
        /// while this is a scale factor) but the underlying question
        /// ("what's the project's tolerance for floating-point error?")
        /// gets the same answer. For a guard like this, false negatives
        /// (accepting a near-radial orbit as non-degenerate) are worse
        /// than false positives (rejecting a slightly-eccentric orbit
        /// that the propagator might actually handle), so 1e-10 is the
        /// conservative choice over a tighter 1e-12.
        /// </para>
        ///
        /// <para>
        /// See <c>docs/phase1_validation_incomplete.md</c> (commit 052)
        /// for the bug shape this threshold prevents, and commit 053
        /// for the production guard that consumes it.
        /// </para>
        /// </summary>
        public const double DegenerateOrbitAngularMomentumSquaredScale = 1e-10;
    }
}
