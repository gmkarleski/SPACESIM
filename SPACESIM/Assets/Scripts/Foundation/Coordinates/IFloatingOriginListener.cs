using Unity.Mathematics;

namespace SpaceSim.Foundation.Coordinates
{
    /// <summary>
    /// Implemented by components that need to participate in floating-origin shifts.
    ///
    /// When the active vessel exceeds the shift threshold from current origin, the
    /// <see cref="FloatingOriginManager"/> updates its origin and notifies every registered
    /// listener via this interface. Listeners apply the shift to their local-space cached
    /// positions: <c>localPosition -= shiftDelta</c>, where shiftDelta is the world-space
    /// movement of the origin.
    ///
    /// The interface dispatch is preferred for performance-critical or frequently-iterated
    /// listeners such as PhysX rigidbody anchors. For ad-hoc subscribers (particle systems,
    /// UI overlays, etc.), <see cref="FloatingOriginManager.OriginShifted"/> event is
    /// equivalent and avoids the implementation requirement.
    /// </summary>
    public interface IFloatingOriginListener
    {
        /// <summary>
        /// Called by the <see cref="FloatingOriginManager"/> on each origin shift.
        ///
        /// <paramref name="shiftDelta"/> is the world-space delta from the old origin to the
        /// new origin. To preserve the listener's world-space position, subtract the delta
        /// from the listener's local-space cached position.
        ///
        /// This callback is invoked synchronously from the manager's
        /// <see cref="FloatingOriginManager.MaybeShiftOrigin"/> call, which the sim-tick
        /// controller dispatches at the sim-tick boundary. Listeners must complete the
        /// shift application synchronously; deferring is not supported because PhysX state
        /// would be momentarily inconsistent with authoritative state.
        /// </summary>
        void OnFloatingOriginShifted(double3 shiftDelta);
    }
}
