using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.Coordinates
{
    /// <summary>
    /// Pure static functions for coordinate-system math.
    ///
    /// These functions are EditMode-testable without instantiating <see cref="FloatingOriginManager"/>.
    /// The manager wraps these with the current origin; tests call them directly with explicit
    /// origin parameters.
    ///
    /// Sign convention for floating-origin shift:
    ///   - World coords are absolute; they don't change when the origin moves.
    ///   - Local coords are relative to the current origin.
    ///   - When the origin moves by +shiftDelta (in world-space), an object that stayed at the
    ///     same world position sees its local position decrease by shiftDelta.
    ///   - Equivalently: <c>local = world - origin</c>.
    /// </summary>
    public static class CoordinateMath
    {
        /// <summary>
        /// Newton's gravitational constant, m³ / (kg · s²).
        ///
        /// Lives in <c>CoordinateMath</c> as the project's single source-of-truth
        /// for G. <see cref="SpaceSim.Foundation.Coordinates.PhysicsConstants"/>
        /// (added at commit 053-stage2) holds body-specific physical constants
        /// (masses, SOI radii, surface radii, inter-body distances) and
        /// references <see cref="G"/> when deriving μ-values
        /// (e.g. <c>PhysicsConstants.EarthMu = G · EarthMassKg</c>). The
        /// import direction is <c>PhysicsConstants</c> → <c>CoordinateMath</c>;
        /// G is not duplicated in <c>PhysicsConstants</c>. See the
        /// <see cref="PhysicsConstants"/> preamble for the canonical
        /// articulation of this split.
        ///
        /// Used by <c>SpaceSim.Foundation.Vessels.ReferenceBody.Mu</c> to compute the standard
        /// gravitational parameter μ = G · M for orbital-element calculations.
        /// </summary>
        public const double G = 6.67430e-11;

        /// <summary>
        /// Convert a world-space position to a local-space position, given the current origin.
        ///
        /// The local position may exceed single-precision representable range when the world
        /// position is far from the origin. The shift system is responsible for ensuring the
        /// active vessel stays within the threshold, but other listeners (e.g., distant
        /// landmarks) may have local positions outside single-precision tolerance. Such
        /// listeners are expected to either snap to the floating-origin event (acceptable
        /// precision loss) or use distance-fade culling to render them as billboards.
        /// </summary>
        public static LocalPosition WorldToLocal(WorldPosition w, WorldPosition origin)
        {
            double3 delta = w.Value - origin.Value;
            return new LocalPosition(new Vector3((float)delta.x, (float)delta.y, (float)delta.z));
        }

        /// <summary>
        /// Convert a local-space position to a world-space position, given the current origin.
        ///
        /// Round-trip stability: <c>LocalToWorld(WorldToLocal(w, o), o)</c> equals <c>w</c>
        /// within single-precision representational tolerance (~1e-5 for local coords below the
        /// shift threshold). Authoritative state should always be stored as
        /// <see cref="WorldPosition"/>; <see cref="LocalPosition"/> is a derived view.
        /// </summary>
        public static WorldPosition LocalToWorld(LocalPosition l, WorldPosition origin)
        {
            double3 localD = new double3(l.Value.x, l.Value.y, l.Value.z);
            return new WorldPosition(origin.Value + localD);
        }

        /// <summary>
        /// Return true if the given world position is strictly farther from the origin than
        /// the threshold. Threshold and distance are both in meters.
        ///
        /// Strict-greater-than convention: a position exactly at the threshold does NOT
        /// trigger a shift. Single-precision local coords are still well-behaved at the
        /// threshold; shifting introduces churn for zero precision benefit.
        /// </summary>
        public static bool ShouldShift(WorldPosition position, WorldPosition origin, double thresholdMeters)
        {
            double distSq = math.lengthsq(position.Value - origin.Value);
            return distSq > thresholdMeters * thresholdMeters;
        }

        /// <summary>
        /// Compute the world-space delta from <paramref name="oldOrigin"/> to
        /// <paramref name="newOrigin"/>. This is the value listeners receive in their shift
        /// callback; they subtract it from their local position to preserve their world position.
        /// </summary>
        public static double3 ComputeShiftDelta(WorldPosition oldOrigin, WorldPosition newOrigin)
        {
            return newOrigin.Value - oldOrigin.Value;
        }
    }
}
