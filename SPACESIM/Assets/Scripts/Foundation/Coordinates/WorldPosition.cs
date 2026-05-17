using System;
using Unity.Mathematics;

namespace SpaceSim.Foundation.Coordinates
{
    /// <summary>
    /// A position in double-precision world coordinates.
    ///
    /// This is the authoritative-state position type per `docs/NETCODE_CONTRACT.md` §1.1
    /// (two layers of state). Authoritative state lives in double-precision world coordinates;
    /// the floating origin and Unity Transforms live in single-precision local coordinates
    /// (see <see cref="LocalPosition"/>).
    ///
    /// Conversion between <see cref="WorldPosition"/> and <see cref="LocalPosition"/> is
    /// explicit and requires the current floating origin. There are no implicit casts;
    /// callers must name the conversion via <see cref="CoordinateMath"/> or via
    /// <see cref="FloatingOriginManager"/>'s convenience wrappers. This is deliberate: the
    /// most common bug class in floating-origin systems is silently mixing world-space and
    /// local-space values. Named conversions force the call site to declare which space it
    /// is operating in.
    ///
    /// Arithmetic with <see cref="WorldPosition"/> accepts only <see cref="double3"/> deltas.
    /// Mixing a <see cref="LocalPosition"/> or a single-precision <see cref="UnityEngine.Vector3"/>
    /// delta into a <see cref="WorldPosition"/> is a type error by design.
    /// </summary>
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public readonly double3 Value;

        public WorldPosition(double3 value) { Value = value; }
        public WorldPosition(double x, double y, double z) { Value = new double3(x, y, z); }

        public static readonly WorldPosition Zero = default;

        /// <summary>Translate this world position by a world-space delta.</summary>
        public static WorldPosition operator +(WorldPosition a, double3 worldDelta)
            => new WorldPosition(a.Value + worldDelta);

        /// <summary>Translate this world position by a world-space delta in the negative direction.</summary>
        public static WorldPosition operator -(WorldPosition a, double3 worldDelta)
            => new WorldPosition(a.Value - worldDelta);

        /// <summary>Displacement from <paramref name="b"/> to <paramref name="a"/> in world-space.</summary>
        public static double3 operator -(WorldPosition a, WorldPosition b)
            => a.Value - b.Value;

        public static bool operator ==(WorldPosition a, WorldPosition b) => a.Equals(b);
        public static bool operator !=(WorldPosition a, WorldPosition b) => !a.Equals(b);

        /// <summary>Distance to another world position, in meters.</summary>
        public double DistanceTo(WorldPosition other) => math.length(other.Value - Value);

        /// <summary>Squared distance to another world position. Cheaper than <see cref="DistanceTo"/>; use when comparing distances.</summary>
        public double DistanceSquaredTo(WorldPosition other) => math.lengthsq(other.Value - Value);

        public bool Equals(WorldPosition other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is WorldPosition o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString()
            => $"WorldPosition({Value.x:G17}, {Value.y:G17}, {Value.z:G17})";
    }
}
