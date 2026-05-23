namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// The three physics modes a vessel can be in, per
    /// <c>docs/CONSTRAINTS.md</c> §2 commit 002 (Physics architecture) and
    /// <c>docs/NETCODE_CONTRACT.md</c> §1.4 / §3 (mode separation, transition protocol).
    ///
    /// Each mode has a different authoritative-state shape and a different time-warp
    /// ceiling:
    ///   <list type="bullet">
    ///     <item><see cref="PhysXActive"/>: Unity PhysX simulation. 1× warp ceiling.</item>
    ///     <item><see cref="KeplerRails"/>: analytic orbit propagation. 10,000× warp ceiling.</item>
    ///     <item><see cref="InterstellarCruise"/>: analytic cruise with relativistic
    ///         time-dilation. 100,000× warp ceiling.</item>
    ///   </list>
    ///
    /// Mode transitions happen at sim-tick boundaries (step 6 of the cycle, per
    /// <c>docs/NETCODE_CONTRACT.md</c> §1.2). Commit 033 ships the mode enum and the
    /// per-mode warp ceilings; transition detection-and-execution lands in a later commit
    /// when vessel containers exist (the active vessel's mode is currently a configuration
    /// parameter on <see cref="WarpController"/> for warp-ceiling computation only).
    /// </summary>
    public enum PhysicsMode
    {
        /// <summary>Unity PhysX-based simulation. Used in atmosphere, near surfaces, under thrust.</summary>
        PhysXActive,

        /// <summary>Analytic orbit propagation via Kepler's equation. Used for stable on-rails vessels.</summary>
        KeplerRails,

        /// <summary>Analytic cruise with relativistic time-dilation. Used between stellar systems.</summary>
        InterstellarCruise
    }
}
