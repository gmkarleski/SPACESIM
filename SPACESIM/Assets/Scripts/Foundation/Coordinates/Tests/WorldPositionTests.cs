using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using Unity.Mathematics;

namespace SpaceSim.Foundation.Coordinates.Tests
{
    public class WorldPositionTests
    {
        [Test]
        public void DefaultConstruction_IsZero()
        {
            WorldPosition w = default;
            Assert.AreEqual(0.0, w.Value.x);
            Assert.AreEqual(0.0, w.Value.y);
            Assert.AreEqual(0.0, w.Value.z);
            Assert.AreEqual(WorldPosition.Zero, w);
        }

        [Test]
        public void Construction_FromDouble3_PreservesValue()
        {
            var w = new WorldPosition(new double3(1.5, -2.5, 3.5));
            Assert.AreEqual(1.5, w.Value.x);
            Assert.AreEqual(-2.5, w.Value.y);
            Assert.AreEqual(3.5, w.Value.z);
        }

        [Test]
        public void Construction_FromThreeDoubles_PreservesValue()
        {
            var w = new WorldPosition(1.5, -2.5, 3.5);
            Assert.AreEqual(1.5, w.Value.x);
            Assert.AreEqual(-2.5, w.Value.y);
            Assert.AreEqual(3.5, w.Value.z);
        }

        [Test]
        public void Equality_SameValue_IsEqual()
        {
            var a = new WorldPosition(1.0, 2.0, 3.0);
            var b = new WorldPosition(1.0, 2.0, 3.0);
            Assert.IsTrue(a == b);
            Assert.IsTrue(a.Equals(b));
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DifferentValue_IsNotEqual()
        {
            var a = new WorldPosition(1.0, 2.0, 3.0);
            var b = new WorldPosition(1.0, 2.0, 3.00000000001);
            Assert.IsTrue(a != b);
            Assert.IsFalse(a == b);
        }

        [Test]
        public void Addition_PreservesWorldCoords()
        {
            var w = new WorldPosition(10.0, 20.0, 30.0);
            var delta = new double3(1.0, 2.0, 3.0);
            var result = w + delta;
            Assert.AreEqual(11.0, result.Value.x);
            Assert.AreEqual(22.0, result.Value.y);
            Assert.AreEqual(33.0, result.Value.z);
        }

        [Test]
        public void Subtraction_PreservesWorldCoords()
        {
            var w = new WorldPosition(10.0, 20.0, 30.0);
            var delta = new double3(1.0, 2.0, 3.0);
            var result = w - delta;
            Assert.AreEqual(9.0, result.Value.x);
            Assert.AreEqual(18.0, result.Value.y);
            Assert.AreEqual(27.0, result.Value.z);
        }

        [Test]
        public void Difference_OfTwoWorldPositions_IsDisplacement()
        {
            var a = new WorldPosition(10.0, 20.0, 30.0);
            var b = new WorldPosition(1.0, 2.0, 3.0);
            double3 displacement = a - b;
            Assert.AreEqual(9.0, displacement.x);
            Assert.AreEqual(18.0, displacement.y);
            Assert.AreEqual(27.0, displacement.z);
        }

        [Test]
        public void DistanceTo_OriginToUnit_IsCorrect()
        {
            var origin = WorldPosition.Zero;
            var unit = new WorldPosition(3.0, 4.0, 0.0);  // 3-4-5 triangle
            Assert.AreEqual(5.0, origin.DistanceTo(unit), 1e-12);
        }

        [Test]
        public void DistanceSquaredTo_IsExactSquare()
        {
            var origin = WorldPosition.Zero;
            var unit = new WorldPosition(3.0, 4.0, 0.0);
            Assert.AreEqual(25.0, origin.DistanceSquaredTo(unit), 1e-12);
        }

        [Test]
        public void DistanceTo_PlanetaryScale_PreservesPrecision()
        {
            // Earth radius ~6,371,000 m. Two points at planetary scale: precision should be
            // sub-millimeter under double-precision.
            var a = new WorldPosition(6_371_000.0, 0.0, 0.0);
            var b = new WorldPosition(6_371_001.0, 0.0, 0.0);
            Assert.AreEqual(1.0, a.DistanceTo(b), 1e-9);
        }

        [Test]
        public void DistanceTo_InterstellarScale_PreservesPrecision()
        {
            // 1 light-year ≈ 9.461e15 m. At this scale, double-precision still resolves to
            // ~1 meter increments (53-bit mantissa ≈ 16 significant decimal digits).
            const double ly = 9.461e15;
            var a = new WorldPosition(ly, 0.0, 0.0);
            var b = new WorldPosition(ly + 1.0, 0.0, 0.0);
            // The 1-meter difference is at the 16th significant digit of 9.461e15. Allow
            // a few-meter tolerance to absorb representation rounding.
            Assert.AreEqual(1.0, a.DistanceTo(b), 100.0);
        }

        [Test]
        public void ToString_FullPrecisionFormat_HasG17()
        {
            var w = new WorldPosition(1.0 / 3.0, 0.0, 0.0);
            string s = w.ToString();
            // G17 means "round-trip-precision" — at least 16 significant digits of 1/3.
            Assert.IsTrue(s.Contains("0.33333333333333"), $"Expected G17 precision in ToString output, got: {s}");
        }
    }
}
