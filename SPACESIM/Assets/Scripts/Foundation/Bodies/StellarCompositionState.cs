namespace SpaceSim.Foundation.Bodies
{
    /// <summary>
    /// Fundamental composition and age of a stellar / sub-stellar body,
    /// per commit 060 D2.
    ///
    /// Per commit 060 D3, `system_position` is NOT placed here — it's
    /// substantively a system-level coordinate that lives on a future
    /// system-level data structure (Phase 7). Only fields intrinsic to
    /// the body itself belong on StellarCompositionState.
    /// </summary>
    public sealed class StellarCompositionState
    {
        /// <summary>Body age in years since formation.</summary>
        public double Age;

        /// <summary>Metallicity in dimensionless [Fe/H] log scale
        /// relative to Sol. Sol-equivalent reference = 0.0; metal-rich
        /// stars positive; metal-poor stars negative.</summary>
        public double Metallicity;
    }
}
