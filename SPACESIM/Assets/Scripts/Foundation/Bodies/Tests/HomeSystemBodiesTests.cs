using NUnit.Framework;
using System;

namespace SpaceSim.Foundation.Bodies.Tests
{
    /// <summary>
    /// Verify HomeSystemBodies.Sun() produces a Sol-equivalent BodyState
    /// matching commit 058 D3's hand-tuned input values and informational
    /// derived values.
    ///
    /// Per commit 058 D3, derived values are informational expectations,
    /// not regression targets — Stage 1's eventual derivation formulas
    /// will produce values close-to-but-not-necessarily-exactly matching
    /// the listed numbers. For commit 062, the values are hand-tuned to
    /// match exactly (no derivation formulas exist yet); tests verify
    /// the hand-tuning produces the documented Sol-equivalent reference.
    /// </summary>
    [TestFixture]
    public class HomeSystemBodiesTests
    {
        // Standard relative tolerance for hand-tuned values that should
        // match real solar parameters exactly. Used for fields stored at
        // their canonical literal values (e.g., 1.989e30 kg for Sun mass).
        private const double Tolerance = 1e-9;

        [Test]
        public void Sun_UniversalFlatFields_MatchSolEquivalent()
        {
            var sun = HomeSystemBodies.Sun();

            Assert.AreNotEqual(Guid.Empty, sun.BodyId,
                "Sun should have a non-empty BodyId.");
            Assert.AreEqual(0UL, sun.Seed,
                "Sun-equivalent uses fixed seed 0UL as system anchor.");
            Assert.AreEqual("Sun", sun.Name);
            Assert.AreEqual(Guid.Empty, sun.ParentBodyId,
                "Sun is top-level; no parent.");
            Assert.AreEqual(1.989e30, sun.Mass, 1.989e30 * Tolerance,
                "Sun mass = 1.989e30 kg.");
            Assert.AreEqual(6.96e8, sun.Radius, 6.96e8 * Tolerance,
                "Sun radius = 6.96e8 m.");
            Assert.AreEqual(double.PositiveInfinity, sun.SoiRadius,
                "Sun is top-level; SOI radius is infinity.");

            // RotationRate and AxialTilt use WIDER tolerances than other
            // fields because their stored values are hand-converted from
            // human-readable forms (25.4 days for rotation period; 7.25°
            // for axial tilt) and the conversion introduces rounding.
            // True value of rotation rate: 2π / (25.4 × 86400) ≈ 2.86346e-6;
            // stored as 2.864e-6 (rounded). True value of axial tilt:
            // 7.25 × π/180 ≈ 0.12653; stored as 0.1265 (rounded). Tightening
            // these tolerances would force re-deriving from days / degrees
            // at every read, which is the wrong layer for the conversion.
            Assert.AreEqual(2.864e-6, sun.RotationRate, 2.864e-6 * 1e-3,
                "Sun rotation rate ≈ 2.864e-6 rad/s (25.4-day period).");
            Assert.AreEqual(0.1265, sun.AxialTilt, 1e-4,
                "Sun axial tilt ≈ 0.1265 rad (7.25°).");
        }

        [Test]
        public void Sun_StellarEmission_Populated_MatchesSolEquivalent()
        {
            var sun = HomeSystemBodies.Sun();

            Assert.IsNotNull(sun.StellarEmission,
                "Sun is an emitter; StellarEmission must be populated.");
            Assert.AreEqual(3.828e26, sun.StellarEmission.Luminosity,
                3.828e26 * Tolerance, "Sun luminosity = 3.828e26 W.");
            Assert.AreEqual(5778.0, sun.StellarEmission.SurfaceTemperature,
                5778.0 * Tolerance, "Sun surface temperature = 5778 K.");
            Assert.AreEqual(5.02e-7, sun.StellarEmission.SpectralPeak,
                5.02e-7 * Tolerance,
                "Sun spectral peak ≈ 5.02e-7 m (visible peak ~502 nm).");
            Assert.AreEqual("G2V", sun.StellarEmission.StellarType,
                "Sun stellar type = G2V.");
            Assert.AreEqual("G", sun.StellarEmission.SpectralClass,
                "Sun broad spectral class = G.");
        }

        [Test]
        public void Sun_StellarActivity_Populated_MatchesSolEquivalent()
        {
            var sun = HomeSystemBodies.Sun();

            Assert.IsNotNull(sun.StellarActivity,
                "Sun has magnetic / dynamical activity; StellarActivity "
                + "must be populated.");
            Assert.AreEqual(0.3, sun.StellarActivity.MagneticActivityIndex,
                Tolerance,
                "Sun magnetic activity index = 0.3 (mid-band 11-year cycle).");
            Assert.AreEqual(0.0, sun.StellarActivity.FlareFrequency,
                Tolerance,
                "Sun-equivalent is quiescent at v1; flare frequency = 0.");
        }

        [Test]
        public void Sun_StellarComposition_Populated_MatchesSolEquivalent()
        {
            var sun = HomeSystemBodies.Sun();

            Assert.IsNotNull(sun.StellarComposition,
                "Sun is stellar; StellarComposition must be populated.");
            Assert.AreEqual(4.6e9, sun.StellarComposition.Age,
                4.6e9 * Tolerance, "Sun age = 4.6e9 years.");
            Assert.AreEqual(0.0, sun.StellarComposition.Metallicity,
                Tolerance,
                "Sun is the metallicity reference (0.0 [Fe/H] log scale).");
        }

        [Test]
        public void Sun_OrbitalDynamics_Null_BecauseTopLevel()
        {
            var sun = HomeSystemBodies.Sun();

            Assert.IsNull(sun.OrbitalDynamics,
                "Sun is top-level; OrbitalDynamics must be null "
                + "(no parent body to orbit around).");
        }

        [Test]
        public void Sun_AcceptsExplicitBodyId_ForDeterministicScenarios()
        {
            var fixedId = new Guid("12345678-1234-1234-1234-123456789012");
            var sun = HomeSystemBodies.Sun(fixedId);

            Assert.AreEqual(fixedId, sun.BodyId,
                "Sun() should accept explicit BodyId parameter for "
                + "deterministic test scenarios.");
        }

        [Test]
        public void Sun_GeneratesDistinctBodyIds_WhenCalledWithoutArgs()
        {
            var sun1 = HomeSystemBodies.Sun();
            var sun2 = HomeSystemBodies.Sun();

            Assert.AreNotEqual(sun1.BodyId, sun2.BodyId,
                "Each Sun() call without explicit BodyId should generate "
                + "a unique Guid.");
        }
    }
}
