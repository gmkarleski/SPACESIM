namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Implemented by components that need synchronous notification when the sim-tick
    /// counter advances. <see cref="SimTickController"/> invokes
    /// <see cref="OnSimTickAdvanced(long)"/> once per analytic iteration of step 10
    /// (counter advancement) of the cycle.
    ///
    /// The dual-listener model matches <c>SpaceSim.Foundation.Coordinates.IFloatingOriginListener</c>:
    ///   <list type="bullet">
    ///     <item>Implement this interface and register via
    ///       <see cref="SimTickController.RegisterListener"/> for performance-critical or
    ///       frequently-iterated listeners (vessel systems, physics components). Interface
    ///       dispatch avoids per-tick delegate allocation.</item>
    ///     <item>Subscribe to <see cref="SimTickController.TickAdvanced"/> for ad-hoc
    ///       subscribers (UI overlays, diagnostics, telemetry sinks) where the
    ///       <c>+= handler</c> convenience outweighs the small allocation cost.</item>
    ///   </list>
    ///
    /// Listeners receive the new <paramref name="tickNumber"/> as the callback argument.
    /// They must complete the callback synchronously; deferring work to a later frame
    /// would break the sim-tick boundary's atomicity (the cycle is meant to be visible to
    /// the rest of the system as a single advance from tick N to tick N+1).
    /// </summary>
    public interface ISimTickListener
    {
        /// <summary>
        /// Called by <see cref="SimTickController"/> when the sim-tick counter advances.
        /// Invoked from step 10 of the cycle, after authoritative state for this tick is
        /// canonical and before rendering interpolates to the next tick.
        /// </summary>
        /// <param name="tickNumber">The new sim-tick number after the advancement.</param>
        void OnSimTickAdvanced(long tickNumber);
    }
}
