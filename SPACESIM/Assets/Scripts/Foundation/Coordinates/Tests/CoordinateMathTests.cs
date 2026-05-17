using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using Unity.Mathematics;
using UnityEngine;

namespace SpaceSim.Foundation.Coordinates.Tests
{
    public class CoordinateMathTests
    {
        // ----- WorldToLocal -----

        [Test]
        public void WorldToLocal_AtOrigin_IsZero()
        {
            var w = new WorldPosition(5.0, 10.0, -15.0);
            var origin = w;  // origin = same as the point
            var local = CoordinateMath.WorldToLocal(w, origin);
            Assert.AreEqual(0.0f, local.Value.x);
            Assert.AreEqual(0.0f, local.Value.y);
            Assert.AreEqual(0.0f, local.Value.z);
        }

        [Test]
        public void WorldToLocal_SmallDistance_PreservesPrecision()
        {
            var origin = new WorldPosition(1000.0, 2000.0, 3000.0);
            var w = new WorldPosition(1001.5, 2002.5, 3003.5);
            var local = CoordinateMath.WorldToLocal(w, origin);
            Assert.AreEqual(1.5f, local.Value.x, 1e-6f);
            Assert.AreEqual(2.5f, local.Value.y, 1e-6f);
            Assert.AreEqual(3.5f, local.Value.z, 1e-6f);
        }

        [Test]
        public void WorldToLocal_AtFarOrigin_StillProducesSmallLocal()
        {
            // Origin at 10 million meters; point at 10 million + 100 m. Local should be ~100m
            // with sub-meter precision retained after the double→float cast.
            var origin = new WorldPosition(10_000_000.0, 0.0, 0.0);
            var w = new WorldPosition(10_000_100.0, 0.0, 0.0);
            var local = CoordinateMath.WorldToLocal(w, origin);
            Assert.AreEqual(100.0f, local.Value.x, 1.0f);
        }

        // ----- LocalToWorld -----

        [Test]
        public void LocalToWorld_AtOrigin_ReturnsOrigin()
        {
            var origin = new WorldPosition(5.0, 10.0, -15.0);
            var l = LocalPosition.Zero;
            var w = CoordinateMath.LocalToWorld(l, origin);
            Assert.AreEqual(origin, w);
        }

        [Test]
        public void LocalToWorld_PreservesWorldValue()
        {
            var origin = new WorldPosition(1000.0, 2000.0, 3000.0);
            var l = new LocalPosition(1.5f, 2.5f, 3.5f);
            var w = CoordinateMath.LocalToWorld(l, origin);
            Assert.AreEqual(1001.5, w.Value.x, 1e-5);
            Assert.AreEqual(2002.5, w.Value.y, 1e-5);
            Assert.AreEqual(3003.5, w.Value.z, 1e-5);
        }

        // ----- Round-trip stability -----

        [Test]
        public void RoundTrip_LocalToWorldToLocal_Stable()
        {
            var origin = new WorldPosition(1000.0, 2000.0, 3000.0);
            var lOriginal = new LocalPosition(10.5f, 20.5f, 30.5f);
            var w = CoordinateMath.LocalToWorld(lOriginal, origin);
            var lRoundTrip = CoordinateMath.WorldToLocal(w, origin);
            Assert.AreEqual(lOriginal.Value.x, lRoundTrip.Value.x, 1e-5f);
            Assert.AreEqual(lOriginal.Value.y, lRoundTrip.Value.y, 1e-5f);
            Assert.AreEqual(lOriginal.Value.z, lRoundTrip.Value.z, 1e-5f);
        }

        [Test]
        public void RoundTrip_WorldToLocalToWorld_Stable_AtSmallScale()
        {
            var origin = new WorldPosition(1000.0, 2000.0, 3000.0);
            var wOriginal = new WorldPosition(1001.5, 2002.5, 3003.5);
            var l = CoordinateMath.WorldToLocal(wOriginal, origin);
            var wRoundTrip = CoordinateMath.LocalToWorld(l, origin);
            Assert.AreEqual(wOriginal.Value.x, wRoundTrip.Value.x, 1e-5);
            Assert.AreEqual(wOriginal.Value.y, wRoundTrip.Value.y, 1e-5);
            Assert.AreEqual(wOriginal.Value.z, wRoundTrip.Value.z, 1e-5);
        }

        // ----- ShouldShift -----

        [Test]
        public void ShouldShift_BelowThreshold_IsFalse()
        {
            var origin = WorldPosition.Zero;
            var pos = new WorldPosition(40_000.0, 0.0, 0.0);  // 40 km
            double threshold = 50_000.0;  // 50 km
            Assert.IsFalse(CoordinateMath.ShouldShift(pos, origin, threshold));
        }

        [Test]
        public void ShouldShift_ExactlyAtThreshold_IsFalse()
        {
            // Strict-greater-than convention: at exactly threshold, no shift.
            var origin = WorldPosition.Zero;
            var pos = new WorldPosition(50_000.0, 0.0, 0.0);  // exactly 50 km
            double threshold = 50_000.0;
            Assert.IsFalse(CoordinateMath.ShouldShift(pos, origin, threshold),
                "Strict-greater-than: at exactly threshold, no shift should occur.");
        }

        [Test]
        public void ShouldShift_AboveThreshold_IsTrue()
        {
            var origin = WorldPosition.Zero;
            var pos = new WorldPosition(60_000.0, 0.0, 0.0);  // 60 km
            double threshold = 50_000.0;
            Assert.IsTrue(CoordinateMath.ShouldShift(pos, origin, threshold));
        }

        [Test]
        public void ShouldShift_NegativeDirection_IsTrue()
        {
            // Distance is signed-magnitude; -60 km is still 60 km away.
            var origin = WorldPosition.Zero;
            var pos = new WorldPosition(-60_000.0, 0.0, 0.0);
            double threshold = 50_000.0;
            Assert.IsTrue(CoordinateMath.ShouldShift(pos, origin, threshold));
        }

        [Test]
        public void ShouldShift_3DDistance_IsTrue()
        {
            // 30km on x, 40km on y → 50km Euclidean. Strict-greater-than: not shifted.
            // Bump to 31km on x → distance > 50km → shifted.
            var origin = WorldPosition.Zero;
            var pos1 = new WorldPosition(30_000.0, 40_000.0, 0.0);
            var pos2 = new WorldPosition(31_000.0, 40_000.0, 0.0);
            double threshold = 50_000.0;
            Assert.IsFalse(CoordinateMath.ShouldShift(pos1, origin, threshold));
            Assert.IsTrue(CoordinateMath.ShouldShift(pos2, origin, threshold));
        }

        // ----- ComputeShiftDelta -----

        [Test]
        public void ComputeShiftDelta_FromZero_EqualsNewOrigin()
        {
            var oldOrigin = WorldPosition.Zero;
            var newOrigin = new WorldPosition(60_000.0, 0.0, 0.0);
            double3 delta = CoordinateMath.ComputeShiftDelta(oldOrigin, newOrigin);
            Assert.AreEqual(60_000.0, delta.x, 1e-9);
            Assert.AreEqual(0.0, delta.y, 1e-9);
            Assert.AreEqual(0.0, delta.z, 1e-9);
        }

        [Test]
        public void ComputeShiftDelta_BetweenNonzeroOrigins_IsCorrect()
        {
            var oldOrigin = new WorldPosition(10.0, 20.0, 30.0);
            var newOrigin = new WorldPosition(15.0, 25.0, 35.0);
            double3 delta = CoordinateMath.ComputeShiftDelta(oldOrigin, newOrigin);
            Assert.AreEqual(5.0, delta.x, 1e-9);
            Assert.AreEqual(5.0, delta.y, 1e-9);
            Assert.AreEqual(5.0, delta.z, 1e-9);
        }
    }
}
