namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// The current phase of the sim-tick cycle, per <c>docs/NETCODE_CONTRACT.md</c> §1.2's
    /// ten-step sequence. Updated by <see cref="SimTickController"/> as it enters each
    /// step so debuggers, logs, and future telemetry can observe which step is executing.
    /// </summary>
    public enum SimTickPhase
    {
        /// <summary>The controller is not currently executing a cycle.</summary>
        Idle,

        /// <summary>Step 1: receive peer state (no-op in single-player).</summary>
        ReceivePeerState,

        /// <summary>Step 2: read PhysX state for PhysX-active vessels with local authority.</summary>
        ReadPhysX,

        /// <summary>Step 3: convert PhysX local coords to authoritative double-precision world coords.</summary>
        ConvertToAuthoritative,

        /// <summary>Step 4: apply analytic updates (Kepler/cruise propagation, fuel, life support).</summary>
        ApplyAnalyticUpdates,

        /// <summary>Step 5: reconcile PhysX-derived updates with analytic updates into new authoritative state.</summary>
        ReconcileAuthoritative,

        /// <summary>Step 6: detect mode transitions; dispatch floating-origin shift if needed.</summary>
        DetectModeTransitions,

        /// <summary>Step 7: push authoritative state back to PhysX rigidbodies.</summary>
        PushAuthoritativeToPhysX,

        /// <summary>Step 8: replicate authoritative state deltas to peers (no-op in single-player).</summary>
        ReplicateToPeers,

        /// <summary>Step 9: fire analytic events whose scheduled tick has been reached.</summary>
        FireEvents,

        /// <summary>Step 10: increment the sim-tick counter; rendering interpolates between this tick's state and the next.</summary>
        AdvanceCounter
    }
}
