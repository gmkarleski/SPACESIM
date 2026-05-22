using System;
using SpaceSim.Foundation.Vessels;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// Shared <see cref="KeplerState"/> builder for predictor tests. Consolidates
    /// the three previously-duplicated <c>BuildState</c> helpers that lived
    /// privately inside <c>AtmosphericEntryPredictorTests</c>,
    /// <c>SoiCrossingPredictorTests</c>, and <c>SurfaceImpactPredictorTests</c>.
    /// The three helpers had near-identical bodies (i=0, Ω=0, ω=0, epochTick=0)
    /// with only minor variation in <see cref="KeplerState.ReferenceBodyId"/>
    /// handling (one supported an explicit override, the other two used
    /// <c>Guid.NewGuid()</c> per-call). The consolidated signature subsumes
    /// both patterns via the optional <paramref name="referenceBodyId"/>
    /// parameter.
    ///
    /// <para>
    /// <strong>USAGE — `using static`:</strong> consumer test files import
    /// this class via <c>using static SpaceSim.Foundation.Vessels.Tests.PredictorTestState;</c>
    /// at the top of the file. Existing call sites
    /// (<c>BuildState(a, e, nu)</c> / <c>BuildState(a, e, nu, bodyId)</c>) stay
    /// unchanged after migration — `using static` resolves the unqualified
    /// names directly. The qualifier-free pattern keeps the diff small at
    /// each call site (the helper consolidation should not noise up tests
    /// that aren't conceptually changing).
    /// </para>
    ///
    /// <para>
    /// <strong>SCOPE — PREDICTOR TESTS ONLY:</strong> the helper name and
    /// XML doc deliberately scope to predictor tests. A fourth
    /// <c>BuildState</c> helper exists in
    /// <c>OrbitalElementsTests.cs</c> (which exercises the shared math
    /// helper that predictors call into); that helper uses a class-level
    /// <c>TestBodyId</c> constant for body-id stability across calls within
    /// a single test class, not the per-call <c>Guid.NewGuid()</c> pattern
    /// the predictor tests use. Consolidating <c>OrbitalElementsTests</c>
    /// would require either renaming this helper (e.g., to a broader
    /// <c>KeplerStateTestBuilder</c>) or accepting a name-vs-scope mismatch.
    /// Deferred to a separate small follow-on commit if/when the rename
    /// decision is made.
    /// </para>
    ///
    /// <para>
    /// <strong>PARAMETER NAMES:</strong> <c>semiMajorAxis</c>,
    /// <c>eccentricity</c>, <c>trueAnomalyAtEpoch</c> match the convention
    /// established in <see cref="OrbitalElements"/> public API and used
    /// throughout <c>OrbitalElementsTests</c>. Named-argument call sites
    /// (e.g., <c>BuildState(rPeri, eccentricity: 0.5, trueAnomalyAtEpoch: 0.0)</c>)
    /// continue to work unchanged across the three migrated files.
    /// </para>
    /// </summary>
    internal static class PredictorTestState
    {
        /// <summary>
        /// Build a default equatorial <see cref="KeplerState"/> at the given
        /// orbital parameters. Inclination, longitude-of-ascending-node, and
        /// argument-of-periapsis are all zero (the orbit lies in the
        /// equatorial plane with periapsis at +X). Epoch tick is zero
        /// (caller-supplied current-tick values in predictor invocations are
        /// relative to this baseline).
        ///
        /// <para>
        /// <paramref name="referenceBodyId"/>: if provided, used directly;
        /// if null, a fresh <see cref="Guid.NewGuid"/> is generated per
        /// call. The optional override matches the
        /// <c>SoiCrossingPredictorTests</c> helper's original signature
        /// where some tests need to wire a specific body's id into the
        /// state.
        /// </para>
        /// </summary>
        internal static KeplerState BuildState(
            double semiMajorAxis,
            double eccentricity,
            double trueAnomalyAtEpoch,
            Guid? referenceBodyId = null)
        {
            return new KeplerState
            {
                SemiMajorAxis = semiMajorAxis,
                Eccentricity = eccentricity,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = trueAnomalyAtEpoch,
                EpochTick = 0,
                ReferenceBodyId = referenceBodyId ?? Guid.NewGuid(),
            };
        }
    }
}
