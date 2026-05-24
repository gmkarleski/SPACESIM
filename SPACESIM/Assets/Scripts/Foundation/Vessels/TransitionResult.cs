namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Result code returned by <see cref="Vessel.TransitionToKeplerRails"/>
    /// (and reserved for future use by <see cref="Vessel.TransitionToPhysXActive"/>
    /// if needed). Introduced in commit 053-stage2; replaces the prior
    /// <c>void</c> return type that left callers unable to distinguish
    /// success from the various failure paths in the transition logic.
    ///
    /// <para>
    /// <strong>WHY ENUM RETURN INSTEAD OF EXCEPTION-OR-VOID:</strong>
    /// failures of <c>TransitionToKeplerRails</c> are expected operational
    /// conditions (vessel not initialized yet, vessel already on rails,
    /// vessel in unsupported mode, vessel state inconsistent, vessel
    /// trajectory degenerate) — not exceptional errors. The void return
    /// + Console-log pattern lost information downstream of the call site;
    /// callers like the Space-key handler had no way to surface "transition
    /// silently refused" to the user without parsing log strings. An enum
    /// return gives every caller an explicit, checkable outcome.
    /// </para>
    ///
    /// <para>
    /// <strong>API STABILITY:</strong> the three values defined here cover
    /// the current and immediately-anticipated failure modes. The
    /// <see cref="FailedOther"/> value exists explicitly to keep the API
    /// open for future failure modes that would otherwise require a
    /// breaking change to add (e.g., a future radial-Kepler closed-form
    /// implementation might add <c>FailedRadialMathNotImplemented</c>,
    /// which today's callers would treat as <see cref="FailedOther"/>
    /// equivalent without code changes).
    /// </para>
    /// </summary>
    public enum TransitionResult
    {
        /// <summary>
        /// The transition completed and the vessel is now in the requested
        /// mode. Also returned when the vessel was already in the requested
        /// mode at call time (idempotent — desired end-state was already
        /// satisfied).
        /// </summary>
        Success,

        /// <summary>
        /// The transition was refused because the vessel's current state
        /// vector (position + velocity) produces a degenerate orbit —
        /// specifically, the angular momentum magnitude squared falls below
        /// <c>PhysicsConstants.DegenerateOrbitAngularMomentumSquaredScale · μ · |r|</c>,
        /// indicating a nearly purely-radial trajectory that the current
        /// Keplerian rails implementation cannot resolve. The vessel
        /// remains in its prior mode (typically PhysX-active).
        ///
        /// <para>
        /// Radial closed-form Kepler math is Phase 3+ scope; today's
        /// implementation rejects the case rather than producing the
        /// NaN cascade documented in commit 052
        /// (<c>docs/phase1_validation_incomplete.md</c>).
        /// </para>
        /// </summary>
        FailedDegenerateOrbit,

        /// <summary>
        /// The transition was refused for a reason other than orbit
        /// degeneracy. Includes: vessel not initialized, vessel in an
        /// unsupported source mode (e.g., InterstellarCruise → KeplerRails
        /// not implemented in Phase 0), vessel state inconsistent (e.g.,
        /// PhysXActive mode with null Rigidbody). Reserved for future
        /// failure modes that aren't worth their own enum value.
        /// </summary>
        FailedOther,
    }
}
