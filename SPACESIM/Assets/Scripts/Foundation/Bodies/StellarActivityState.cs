namespace SpaceSim.Foundation.Bodies
{
    /// <summary>
    /// Magnetic and dynamical activity of a stellar / sub-stellar body,
    /// per commit 060 D2.
    ///
    /// Populated for bodies with magnetic / dynamical activity (typically
    /// the same bodies that populate StellarEmission). Null for inactive
    /// bodies.
    /// </summary>
    public sealed class StellarActivityState
    {
        /// <summary>Magnetic activity index, dimensionless on a [0, 1]
        /// scale. Real Sun varies 0.1-0.5 across its 11-year cycle;
        /// quiescent stars approach 0; flare stars approach 1.</summary>
        public double MagneticActivityIndex;

        /// <summary>Average flare frequency, flares per unit time
        /// (units left implicit at Phase 2; per-year conventional for
        /// stars).</summary>
        public double FlareFrequency;
    }
}
