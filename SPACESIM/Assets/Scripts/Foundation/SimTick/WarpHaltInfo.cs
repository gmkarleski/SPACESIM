using System;

namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Diagnostic payload describing a time-warp halt event. Carried by
    /// <see cref="WarpController.OnWarpHalted"/> and cached on
    /// <see cref="WarpController.LastHaltInfo"/>.
    ///
    /// <para>
    /// Halt events fire when the warp controller transitions from "advancing" to
    /// "not advancing" because of an external condition the player needs to see —
    /// reaching a target tick, a vessel-level predictor signalling an imminent
    /// event on a non-routine vessel, a transmission arrival, a mission event
    /// firing, etc.
    /// </para>
    ///
    /// <para>
    /// The struct is intentionally narrow:
    /// <list type="bullet">
    ///   <item><see cref="HaltingVesselId"/> identifies which vessel triggered the
    ///   halt (nullable for non-vessel halts like target-tick-reached or manual
    ///   pause).</item>
    ///   <item><see cref="HaltReason"/> names the halt taxonomy
    ///   (<see cref="WarpHaltReason"/>) — parallel to but distinct from
    ///   <c>TransitionTriggerReason</c> in the Vessels asmdef; see the
    ///   <see cref="WarpHaltReason"/> XML doc for the relationship.</item>
    ///   <item><see cref="HaltTick"/> records the sim-tick at which the halt
    ///   landed. For target-tick halts this equals the target.</item>
    ///   <item><see cref="DiagnosticMessage"/> is the human-readable context UI
    ///   surfaces verbatim (e.g., "Target tick 5000 reached", "Atmospheric entry
    ///   predicted on vessel TestProbe1 at tick 412").</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Value semantics — no heap allocation per halt event.
    /// </para>
    /// </summary>
    public readonly struct WarpHaltInfo : IEquatable<WarpHaltInfo>
    {
        /// <summary>UUID of the vessel that triggered the halt. Null for non-vessel halts (target-reached, manual pause).</summary>
        public readonly Guid? HaltingVesselId;

        /// <summary>Reason category for the halt. See <see cref="WarpHaltReason"/> for the taxonomy.</summary>
        public readonly WarpHaltReason HaltReason;

        /// <summary>The sim-tick at which the halt occurred.</summary>
        public readonly long HaltTick;

        /// <summary>Human-readable context for UI surfacing. Format and content are not stable across versions; intended for display, not parsing.</summary>
        public readonly string DiagnosticMessage;

        /// <summary>Construct a <see cref="WarpHaltInfo"/> with all four fields explicit.</summary>
        public WarpHaltInfo(
            Guid? haltingVesselId,
            WarpHaltReason haltReason,
            long haltTick,
            string diagnosticMessage)
        {
            HaltingVesselId = haltingVesselId;
            HaltReason = haltReason;
            HaltTick = haltTick;
            DiagnosticMessage = diagnosticMessage ?? string.Empty;
        }

        /// <inheritdoc />
        public bool Equals(WarpHaltInfo other)
        {
            return HaltingVesselId == other.HaltingVesselId
                && HaltReason == other.HaltReason
                && HaltTick == other.HaltTick
                && DiagnosticMessage == other.DiagnosticMessage;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is WarpHaltInfo other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HaltingVesselId.GetHashCode();
                hash = (hash * 397) ^ (int)HaltReason;
                hash = (hash * 397) ^ HaltTick.GetHashCode();
                hash = (hash * 397) ^ (DiagnosticMessage != null ? DiagnosticMessage.GetHashCode() : 0);
                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"WarpHaltInfo(vessel={HaltingVesselId}, reason={HaltReason}, tick={HaltTick}, msg=\"{DiagnosticMessage}\")";
        }

        public static bool operator ==(WarpHaltInfo left, WarpHaltInfo right) => left.Equals(right);
        public static bool operator !=(WarpHaltInfo left, WarpHaltInfo right) => !left.Equals(right);
    }
}
