using SpaceSim.Foundation.Coordinates;

namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Minimal contract that the sim-tick controller's step 6 needs from "the active
    /// vessel" — the one whose world position drives floating-origin shift detection and
    /// whose mode drives the warp-ceiling selection.
    ///
    /// WHY AN INTERFACE INSTEAD OF A DIRECT VESSEL REFERENCE:
    /// The concrete <c>Vessel</c> type lives in the <c>SpaceSim.Foundation.Vessels</c>
    /// asmdef, which references <c>SpaceSim.Foundation.SimTick</c> (so vessels can read
    /// <see cref="SimTickController.Instance.TickNumber"/> and use the <see cref="PhysicsMode"/>
    /// enum). SimTickController referencing <c>Vessel</c> directly would create a
    /// circular asmdef dependency — Unity rejects circular references at compile time.
    /// <see cref="IActiveVessel"/> breaks the cycle by living in the SimTick module:
    /// SimTickController consumes the interface; Vessel implements it. The dependency
    /// graph stays acyclic: Vessels → SimTick → Coordinates.
    ///
    /// WHY THIS INTERFACE IS NARROW:
    /// Phase 0 step 6 needs exactly two pieces of state from the active vessel: where it
    /// is (for the shift threshold check) and what mode it's in (for the warp ceiling).
    /// Keeping the interface minimal makes test doubles trivial — a POCO with two
    /// auto-properties satisfies the contract. Future SimTickController steps that gain
    /// real implementations (step 2 PhysX read, step 4 analytic propagation, step 7
    /// PhysX push-back) will either extend this interface or define their own sibling
    /// interfaces in this module.
    ///
    /// EXISTING IMPLEMENTERS (commit 038):
    ///   - <c>SpaceSim.Foundation.Vessels.Vessel</c> — the production vessel class
    ///   - Test POCOs in <c>SpaceSim.Foundation.SimTick.Tests</c> — minimal stand-ins for
    ///     step-6 unit tests that don't want the full Vessel + ReferenceBody + GameObject
    ///     scaffolding
    /// </summary>
    public interface IActiveVessel
    {
        /// <summary>
        /// World position of this vessel in galactic (double-precision) coordinates,
        /// regardless of the vessel's current physics mode.
        /// </summary>
        WorldPosition GetWorldPosition();

        /// <summary>
        /// Current physics mode of this vessel. Determines the warp ceiling that applies
        /// when this vessel is the active vessel.
        /// </summary>
        PhysicsMode Mode { get; }
    }
}
