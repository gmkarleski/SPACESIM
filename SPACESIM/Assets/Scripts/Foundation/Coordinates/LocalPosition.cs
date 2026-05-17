using System;
using UnityEngine;

namespace SpaceSim.Foundation.Coordinates
{
    /// <summary>
    /// A position in single-precision local coordinates, relative to the current floating
    /// origin.
    ///
    /// This is the rendering-and-PhysX-side position type per `docs/NETCODE_CONTRACT.md` §1.1
    /// (two layers of state). Local positions are what Unity's <see cref="Transform"/> API
    /// expects; they are bounded in magnitude by the floating origin shift threshold from
    /// `docs/CONSTRAINTS.md` §2 commit 002 (50 km default). Beyond the threshold, the origin
    /// shifts so local coordinates stay near zero.
    ///
    /// Conversion between <see cref="LocalPosition"/> and <see cref="WorldPosition"/> is
    /// explicit and requires the current floating origin. See <see cref="CoordinateMath"/>.
    ///
    /// Arithmetic with <see cref="LocalPosition"/> accepts only <see cref="UnityEngine.Vector3"/>
    /// deltas. Mixing a <see cref="WorldPosition"/> or a double-precision delta into a
    /// <see cref="LocalPosition"/> is a type error by design.
    /// </summary>
    public readonly struct LocalPosition : IEquatable<LocalPosition>
    {
        public readonly Vector3 Value;

        public LocalPosition(Vector3 value) { Value = value; }
        public LocalPosition(float x, float y, float z) { Value = new Vector3(x, y, z); }

        public static readonly LocalPosition Zero = default;

        /// <summary>Translate this local position by a local-space delta.</summary>
        public static LocalPosition operator +(LocalPosition a, Vector3 localDelta)
            => new LocalPosition(a.Value + localDelta);

        /// <summary>Translate this local position by a local-space delta in the negative direction.</summary>
        public static LocalPosition operator -(LocalPosition a, Vector3 localDelta)
            => new LocalPosition(a.Value - localDelta);

        /// <summary>Displacement from <paramref name="b"/> to <paramref name="a"/> in local-space.</summary>
        public static Vector3 operator -(LocalPosition a, LocalPosition b)
            => a.Value - b.Value;

        public static bool operator ==(LocalPosition a, LocalPosition b) => a.Equals(b);
        public static bool operator !=(LocalPosition a, LocalPosition b) => !a.Equals(b);

        /// <summary>Distance to another local position, in meters (single-precision).</summary>
        public float DistanceTo(LocalPosition other) => Vector3.Distance(Value, other.Value);

        public bool Equals(LocalPosition other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is LocalPosition o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString()
            => $"LocalPosition({Value.x}, {Value.y}, {Value.z})";
    }
}
