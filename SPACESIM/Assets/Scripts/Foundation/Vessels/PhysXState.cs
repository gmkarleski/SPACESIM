using System;
using SpaceSim.Foundation.Coordinates;
using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Mode-specific state for a vessel in <c>PhysXActive</c> mode, per
    /// <c>docs/NETCODE_CONTRACT.md</c> §2.2.
    ///
    /// Populated when a vessel is being simulated by Unity's PhysX rigidbody system (and
    /// thus subject to forces, contacts, and atmospheric drag). Cleared on transition to
    /// any other mode.
    ///
    /// The <see cref="RigidbodyHandle"/> field is engine-side state, not part of the
    /// serialized save format — when this state is persisted to disk, the rigidbody handle
    /// is null. On scene load, a new rigidbody is constructed from the other fields and the
    /// handle is reassigned.
    /// </summary>
    public sealed class PhysXState
    {
        /// <summary>Position in galactic (double-precision world) coordinates.</summary>
        public WorldPosition PositionWorld;

        /// <summary>Velocity in galactic (double-precision world) coordinates, m/s.</summary>
        public double3 VelocityWorld;

        /// <summary>Vessel orientation (double-precision quaternion).</summary>
        public quaternion Orientation;

        /// <summary>Angular velocity in vessel body frame, rad/s.</summary>
        public double3 AngularVelocity;

        /// <summary>
        /// UUID of the body whose SOI this vessel is currently in. Determines which body's
        /// gravity dominates and is used as the reference body for any subsequent
        /// transition to Kepler-rails mode.
        /// </summary>
        public Guid ReferenceBodyId;

        /// <summary>
        /// Floating-origin offset at the moment this state was last updated. Set by the
        /// FloatingOriginManager on every shift. Used during save/load to reconstruct the
        /// scene's floating origin at the same offset.
        /// </summary>
        public double3 FloatingOrigin;

        /// <summary>
        /// Reference to the Unity rigidbody currently simulating this vessel. Engine-side
        /// state; not serialized. Null when the vessel is in PhysXActive mode but the
        /// rigidbody hasn't been created yet (e.g., during scene load before the
        /// post-deserialization rigidbody-spawn step runs).
        /// </summary>
        public Rigidbody RigidbodyHandle;

        /// <summary>
        /// Current total thrust magnitude across all engines, in Newtons. Zero when no
        /// engines are firing.
        /// </summary>
        public double ActiveThrustN;

        /// <summary>
        /// Thrust direction in vessel local frame (unit vector). Default forward = +Z per
        /// Unity convention.
        /// </summary>
        public double3 ActiveThrustDirection;

        /// <summary>
        /// Atmospheric density at vessel altitude, kg/m³. Zero outside any atmosphere.
        /// </summary>
        public double AtmosphericDensity;

        /// <summary>
        /// Vessel velocity relative to the local atmosphere (accounts for wind, body
        /// rotation). Used for atmospheric drag and atmospheric flight model.
        /// </summary>
        public double3 AtmosphericVelocityRel;
    }
}
