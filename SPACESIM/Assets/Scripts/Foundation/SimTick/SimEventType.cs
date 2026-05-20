namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Categories of analytic events tracked by the
    /// <see cref="EventPriorityQueue"/>. Each value names a class of predictable
    /// future event that warp must respect (warp lands on event times exactly per
    /// netcode contract §4.2).
    ///
    /// <para>
    /// <strong>NAMING:</strong> "SimEventType" rather than "EventType" to avoid
    /// ambiguity with <c>UnityEngine.EventType</c>. The Unity name is reserved by
    /// the engine and the C# compiler raises CS0104 ambiguous-reference errors at
    /// any callsite that imports both namespaces. Prefixing with "Sim" makes the
    /// project-side type unambiguous everywhere.
    /// </para>
    ///
    /// The full enum exists from commit 045 (Stage 2) even though only
    /// <see cref="Periapsis"/> and <see cref="Apoapsis"/> get populated in that
    /// commit. The remaining five values are scaffolded so future predictor commits
    /// add their event type without architectural change — per CONSTRAINTS §2's
    /// extensibility hook ("Adding a new event type later means writing its
    /// predictor function and adding it to the event-type enum. The priority queue
    /// and dispatch are invariant").
    ///
    /// Enum order is significant: it serves as the secondary tie-breaker in the
    /// <see cref="EventPriorityQueue"/>'s sort key when multiple events share the
    /// same tick and vessel. Lower enum values sort earlier. Periapsis before
    /// Apoapsis (numeric ordering matches periapsis-before-apoapsis sequence in a
    /// fresh elliptical orbit). Mode-transition events come after orbit-shape events.
    /// </summary>
    public enum SimEventType
    {
        /// <summary>Vessel reaches periapsis (closest approach to reference body).</summary>
        Periapsis,

        /// <summary>Vessel reaches apoapsis (farthest distance from reference body).</summary>
        Apoapsis,

        /// <summary>
        /// Vessel crosses an SOI boundary (outward to parent, or inward into a
        /// child). Predictor implemented in commit 046 — see
        /// <see cref="SpaceSim.Foundation.Vessels.SoiCrossingPredictor"/>.
        /// Populated each sim-tick by
        /// <see cref="SpaceSim.Foundation.Vessels.VesselEventPredictionDriver"/>
        /// alongside Periapsis and Apoapsis events.
        /// </summary>
        SoiCrossing,

        /// <summary>
        /// Vessel's trajectory predicts atmospheric-entry altitude crossing.
        /// Predictor lands in a future commit (commit 046+); reserved.
        /// </summary>
        AtmosphericEntry,

        /// <summary>
        /// Vessel's trajectory intersects a body's surface (lithobraking inbound).
        /// Predictor lands in a future commit (commit 046+); reserved.
        /// </summary>
        SurfaceImpact,

        /// <summary>
        /// Scheduled engine burn from a maneuver node or autopilot script. Predictor
        /// lands when the maneuver-node system arrives (Phase 5+); reserved.
        /// </summary>
        ScheduledBurn,

        /// <summary>
        /// Interstellar-cruise vessel reaches destination star SOI. Predictor lands
        /// when interstellar mode arrives (Phase 6); reserved.
        /// </summary>
        InterstellarArrival,
    }
}
