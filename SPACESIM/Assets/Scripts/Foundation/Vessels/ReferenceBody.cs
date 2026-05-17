using System;
using SpaceSim.Foundation.Coordinates;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Phase 0 stub representing a single gravity-attracting body (planet, moon, or star)
    /// for orbital math purposes.
    ///
    /// PHASE 0 SCOPE: this class captures only the minimum a vessel's Kepler-rails state
    /// needs to compute orbital elements: the body's identity, mass, and world position.
    /// Full body state per <c>docs/NETCODE_CONTRACT.md</c> §2.6 (axial tilt, rotation rate,
    /// surface terrain seed, atmospheric profile, SOI radius, child bodies) is deferred to
    /// the procgen-bodies work in Phase 4.
    ///
    /// For the Phase 0 test scene, a single <see cref="ReferenceBody"/> represents the
    /// "home planet" at world-origin with Earth-like mass. Multi-body scenes arrive when
    /// the home system gets populated with its four intensive-craft bodies (per commit 021)
    /// during Phase 4 procgen work.
    ///
    /// The class is a <see cref="MonoBehaviour"/> rather than a plain data class so test
    /// scenes can drop a ReferenceBody GameObject into the Hierarchy and the Vessel
    /// component can reference it via Inspector wiring. When the real BodyState lands, the
    /// MonoBehaviour wrapper survives; the data fields gain depth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReferenceBody : MonoBehaviour
    {
        /// <summary>Permanent unique identifier for this body. Auto-assigned on Awake.</summary>
        public Guid BodyId { get; private set; }

        /// <summary>
        /// Body mass in kilograms. Default is Earth-like (5.972 × 10^24 kg) so the Phase 0
        /// test scene produces realistic orbital elements at LEO-scale altitudes.
        /// </summary>
        [SerializeField, Tooltip("Body mass in kilograms. Default 5.972e24 (Earth-equivalent).")]
        private double massKg = 5.972e24;

        /// <summary>Body mass in kilograms.</summary>
        public double MassKg => massKg;

        /// <summary>
        /// Body position in world coordinates.
        ///
        /// PHASE 0 LIMITATION: position is read once at <see cref="Awake"/> from the
        /// GameObject's <c>transform.position</c> via the floating-origin manager's current
        /// origin (treating the transform as a LocalPosition). The body does not move during
        /// Phase 0. When procgen bodies orbit (Phase 4+), this becomes a per-tick computed
        /// value from the body's own orbital state.
        /// </summary>
        public WorldPosition PositionWorld { get; private set; }

        /// <summary>
        /// Standard gravitational parameter μ = G · M, in m³/s².
        ///
        /// This is the value orbital mechanics actually consumes — most equations take μ,
        /// not M. The product is more precisely measured for real bodies (μ for Earth is
        /// known to about a part in 10^9, while G has only about 4 decimals of precision),
        /// but at Phase-0 fidelity computing it from <see cref="MassKg"/> and
        /// <see cref="CoordinateMath.G"/> is sufficient. Phase 4+ may switch to μ as the
        /// stored quantity.
        /// </summary>
        public double Mu => CoordinateMath.G * massKg;

        private void Awake()
        {
            if (BodyId == Guid.Empty)
            {
                BodyId = Guid.NewGuid();
            }

            // Phase 0: capture position once at Awake. Transform.position is a Unity float3;
            // in Phase 0 the test scene puts the ReferenceBody at the scene origin (or near
            // it), and the floating origin's initial position is also zero, so the world
            // position equals the transform position to single-precision. When the body
            // is far from origin, this conversion will need to go through
            // FloatingOriginManager.LocalToWorld with the current origin.
            var t = transform.position;
            if (FloatingOriginManager.Instance != null)
            {
                PositionWorld = FloatingOriginManager.Instance.LocalToWorld(
                    new LocalPosition(t));
            }
            else
            {
                // Manager not yet up: treat transform position as world position. The Phase 0
                // test scene places the body at origin so this branch produces (0, 0, 0).
                PositionWorld = new WorldPosition(t.x, t.y, t.z);
            }
        }
    }
}
