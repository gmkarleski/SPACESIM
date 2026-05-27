namespace SpaceSim.Foundation.Bodies
{
    /// <summary>
    /// What a stellar / sub-stellar body emits and how, per commit 060 D2.
    ///
    /// Populated for stars (main-sequence, giant, white dwarf, neutron
    /// star), brown dwarfs, pulsars, and other emitting / sub-stellar
    /// objects. Non-emitting bodies (planets, moons, asteroids) have
    /// BodyState.StellarEmission = null.
    /// </summary>
    public sealed class StellarEmissionState
    {
        /// <summary>Total radiative power output in watts.</summary>
        public double Luminosity;

        /// <summary>Effective surface temperature in kelvin.</summary>
        public double SurfaceTemperature;

        /// <summary>Wavelength of peak emission in meters
        /// (Wien's displacement law).</summary>
        public double SpectralPeak;

        /// <summary>Specific stellar subclass (e.g., "G2V" for Sol).
        /// Derived from mass + age + spectral_class at Stage 1.</summary>
        public string StellarType = string.Empty;

        /// <summary>Broad spectral class letter (O / B / A / F / G / K
        /// / M / brown dwarf / white dwarf / neutron star). Layer 3
        /// input per commit 058's Stage 1 contract.</summary>
        public string SpectralClass = string.Empty;
    }
}
