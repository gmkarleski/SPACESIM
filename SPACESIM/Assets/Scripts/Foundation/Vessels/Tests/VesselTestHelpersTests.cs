using NUnit.Framework;
using static SpaceSim.Foundation.Vessels.Tests.VesselTestHelpers;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// Validation tests for the eccentricity / orbit-shape assertion helpers
    /// added to <see cref="VesselTestHelpers"/> in commit 053-stage1.
    /// Confirms the helpers throw <see cref="AssertionException"/> on the
    /// boundary cases (eccentricity at threshold, NaN, Infinity, zero angular
    /// momentum) and pass on well-formed input. The helpers are themselves
    /// test infrastructure; the stage 3 regression test for the commit 052
    /// degenerate-orbit bug is the first consumer.
    ///
    /// <para>
    /// Per the file-naming convention established by the other
    /// <c>*Tests.cs</c> files in this directory, helper validation lives in
    /// a dedicated file rather than as a section of <see cref="VesselTests"/>
    /// (which is already at 86 KB).
    /// </para>
    /// </summary>
    public class VesselTestHelpersTests
    {
        // ----- AssertSolvableEccentricity -----

        [Test]
        public void AssertSolvableEccentricity_WithinThreshold_Passes()
        {
            // e = 0.5 is comfortably below the 0.8 default threshold.
            Assert.DoesNotThrow(() => AssertSolvableEccentricity(0.5));
        }

        [Test]
        public void AssertSolvableEccentricity_AboveThreshold_Throws()
        {
            // e = 0.85 exceeds the 0.8 default threshold.
            Assert.Throws<AssertionException>(() => AssertSolvableEccentricity(0.85));
        }

        [Test]
        public void AssertSolvableEccentricity_NaN_Throws()
        {
            // NaN-checked first; Kepler solvers cannot handle NaN input.
            Assert.Throws<AssertionException>(() => AssertSolvableEccentricity(double.NaN));
        }

        [Test]
        public void AssertSolvableEccentricity_PositiveInfinity_Throws()
        {
            // Infinity-checked second; Kepler solvers cannot handle Infinity input.
            Assert.Throws<AssertionException>(
                () => AssertSolvableEccentricity(double.PositiveInfinity));
        }

        // ----- AssertNonDegenerateOrbit -----

        [Test]
        public void AssertNonDegenerateOrbit_WellFormed_Passes()
        {
            // Circular LEO around Earth: r = 7,000 km, v_circ ≈ 7.55 km/s,
            // tangential velocity → |h| = r * v ≈ 5.28e10, |h|² ≈ 2.79e21.
            // Threshold at default scale 1e-12 with Earth's mu (3.986e14) and
            // r = 7e6 is 1e-12 * 3.986e14 * 7e6 = 2.79e9 — well below |h|².
            const double earthMu = 3.986e14;
            const double r = 7_000_000.0;
            double vCirc = System.Math.Sqrt(earthMu / r);
            double angularMomentumSquared = (r * vCirc) * (r * vCirc);

            Assert.DoesNotThrow(
                () => AssertNonDegenerateOrbit(angularMomentumSquared, earthMu, r));
        }

        [Test]
        public void AssertNonDegenerateOrbit_ZeroAngularMomentum_Throws()
        {
            // |h|² = 0 is the textbook degenerate case (purely-radial velocity).
            // This is the exact case that produced the commit 052 bug.
            const double earthMu = 3.986e14;
            const double r = 7_000_000.0;

            Assert.Throws<AssertionException>(
                () => AssertNonDegenerateOrbit(0.0, earthMu, r));
        }
    }
}
