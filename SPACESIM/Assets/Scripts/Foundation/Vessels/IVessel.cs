using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Read-only contract that per-tick driver code uses when it needs to query
    /// vessel state without taking a hard dependency on the concrete
    /// <see cref="Vessel"/> MonoBehaviour. The two consumers as of commit
    /// landing are <see cref="VesselEventPredictionDriver"/> (event prediction:
    /// periapsis / apoapsis / SOI crossing / atmospheric entry / surface
    /// impact) and <see cref="VesselSoiRerootingDriver"/> (per-tick SOI-cross
    /// check + re-root dispatch). Both iterate <see cref="VesselRegistry.Vessels"/>
    /// and call read-only members in tight per-tick loops; lifting them off the
    /// concrete <c>Vessel</c> type makes the test surface clean (POCO fakes
    /// satisfy the contract without constructing Unity GameObjects) and narrows
    /// what each driver is allowed to do to a vessel during its per-tick pass.
    ///
    /// <para>
    /// <strong>WHY IN THE VESSELS MODULE, NOT SIMTICK:</strong> the existing
    /// <see cref="IActiveVessel"/> interface lives in the SimTick asmdef
    /// specifically to break what would otherwise be a Vessels↔SimTick
    /// circular asmdef reference — SimTickController needs to query a vessel
    /// (Mode + position) for floating-origin shift detection, and SimTick
    /// cannot reference Vessels. <see cref="IVessel"/> is a different problem:
    /// it decouples the per-tick driver loop from the concrete vessel type for
    /// testability and contract narrowing, not to break a cycle. Both the
    /// interface and its consumers live in the Vessels asmdef; the dependency
    /// direction stays Vessels → SimTick → Coordinates as documented in
    /// <c>docs/ARCHITECTURE.md</c> §1.3.
    /// </para>
    ///
    /// <para>
    /// <strong>WHY NOT IActiveVessel : IVessel OR THE REVERSE:</strong>
    /// <see cref="IActiveVessel"/> declares <see cref="Mode"/> and
    /// <see cref="GetWorldPosition"/>; <see cref="IVessel"/> declares both of
    /// those plus three more (<see cref="State"/>, <see cref="ReferenceBody"/>,
    /// <see cref="DiagnosticName"/>). Making <see cref="IVessel"/> extend
    /// <see cref="IActiveVessel"/> would pull the SimTick asmdef into the
    /// declaration site of <see cref="IVessel"/>, structurally splitting one
    /// interface across two asmdefs for no operational gain. The concrete
    /// <see cref="Vessel"/> class implements both interfaces independently;
    /// the shared members (<see cref="Mode"/> and <see cref="GetWorldPosition"/>)
    /// are satisfied by a single concrete implementation each.
    /// </para>
    ///
    /// <para>
    /// <strong>SCOPE — READ-ONLY ONLY:</strong> the interface deliberately
    /// excludes mode-transition methods (<c>TransitionToKeplerRails</c>,
    /// <c>TransitionToPhysXActive</c>) and the SOI re-root operation
    /// (<c>ReRootToBody</c>). Those are mutating lifecycle operations that
    /// destroy and re-add Unity components; they belong on the concrete
    /// <see cref="Vessel"/> type, not on a contract that POCO test fakes are
    /// expected to satisfy. <see cref="VesselTransitionDriver"/> consequently
    /// stays coupled to concrete <see cref="Vessel"/> — the driver is the
    /// natural owner of the mutating-lifecycle concerns, and pretending its
    /// dependency is interfaceable would either bloat the interface with
    /// no-op fake implementations or require a parallel mutating interface
    /// (<c>IMutableVessel</c>) that nobody needs yet.
    /// </para>
    ///
    /// <para>
    /// <strong>IMPLEMENTERS:</strong>
    /// <list type="bullet">
    ///   <item><see cref="Vessel"/> — the production MonoBehaviour. Already
    ///   implemented <see cref="IActiveVessel"/>; commit landing this interface
    ///   adds <see cref="IVessel"/> to the class signature and adds the one
    ///   new member (<see cref="DiagnosticName"/>) that wasn't already on the
    ///   concrete type.</item>
    ///   <item>POCO test fakes — minimal POCOs that satisfy the contract via
    ///   auto-properties and a trivial <see cref="GetWorldPosition"/>
    ///   implementation. No <c>GameObject</c>, no <c>MonoBehaviour</c>, no
    ///   reflection. Useful for testing per-tick driver logic in isolation
    ///   from Unity's lifecycle.</item>
    /// </list>
    /// </para>
    /// </summary>
    public interface IVessel
    {
        /// <summary>
        /// Current physics mode of this vessel. Drivers filter to KeplerRails
        /// vessels via this property; non-rails vessels are skipped without
        /// further inspection.
        /// </summary>
        PhysicsMode Mode { get; }

        /// <summary>
        /// Authoritative state of this vessel per netcode contract §2.1.
        /// Drivers read <see cref="VesselAuthoritativeState.KeplerState"/>
        /// (orbital elements + event-prediction fields) and
        /// <see cref="VesselAuthoritativeState.VesselId"/> (for event-queue
        /// entry keys). The §2.1 schema invariant holds (Mode == KeplerRails ⇒
        /// KeplerState != null), but drivers defend against the inconsistent
        /// state regardless.
        /// </summary>
        VesselAuthoritativeState State { get; }

        /// <summary>
        /// Reference body whose gravity defines this vessel's orbital frame.
        /// Drivers pass this to predictor static methods that need
        /// <see cref="ReferenceBody.Mu"/> and per-body parameters
        /// (<see cref="ReferenceBody.SoiRadiusMeters"/>,
        /// <see cref="ReferenceBody.SurfaceRadiusMeters"/>,
        /// <see cref="ReferenceBody.AtmosphericTopAltitudeMeters"/>).
        /// </summary>
        ReferenceBody ReferenceBody { get; }

        /// <summary>
        /// World position of this vessel in galactic (double-precision)
        /// coordinates, regardless of current physics mode. Concrete
        /// <see cref="Vessel"/> implements this via rigidbody-position
        /// conversion (PhysX-active mode) or Kepler propagation (rails mode).
        /// POCO test fakes return a pre-computed <see cref="WorldPosition"/>
        /// from an auto-property.
        ///
        /// <para>
        /// This member is also declared on <see cref="IActiveVessel"/>; the
        /// concrete <see cref="Vessel"/> implementation satisfies both
        /// interfaces with a single method body.
        /// </para>
        /// </summary>
        WorldPosition GetWorldPosition();

        /// <summary>
        /// Unity-free diagnostic identifier for this vessel. Concrete
        /// <see cref="Vessel"/> returns the GameObject's name (or "(null)"
        /// when the GameObject reference is unset, e.g. during destruction).
        /// POCO test fakes return their own string. Used by per-tick driver
        /// catch-block log messages to identify which vessel threw without
        /// coupling the driver to Unity's <c>GameObject</c> type.
        /// </summary>
        string DiagnosticName { get; }

        /// <summary>
        /// Whether this vessel is classified as routine supply. Routine vessels
        /// skip warp-halt registration for predictor events that are expected and
        /// repetitive in their supply-run profile (SOI crossings as of commit 048
        /// Stage 3). Surface impact and atmospheric entry still register halts on
        /// routine vessels — mass-loss and aerodynamic engagement matter
        /// regardless of vessel classification.
        ///
        /// <para>
        /// Added at commit 048 Stage 3 so <see cref="VesselEventPredictionDriver"/>'s
        /// per-predictor halt-registration logic can gate on the flag without
        /// downcasting from <see cref="IVessel"/> to concrete <see cref="Vessel"/>.
        /// The interface stays read-only; the flag itself is mutable on the
        /// concrete <see cref="Vessel"/> via the public-setter property there.
        /// </para>
        /// </summary>
        bool IsRoutineSupply { get; }
    }
}
