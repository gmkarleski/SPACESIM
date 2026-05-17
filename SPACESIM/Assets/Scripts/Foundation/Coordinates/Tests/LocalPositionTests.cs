using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using UnityEngine;

namespace SpaceSim.Foundation.Coordinates.Tests
{
    public class LocalPositionTests
    {
        [Test]
        public void DefaultConstruction_IsZero()
        {
            LocalPosition l = default;
            Assert.AreEqual(Vector3.zero, l.Value);
            Assert.AreEqual(LocalPosition.Zero, l);
        }

        [Test]
        public void Construction_FromVector3_PreservesValue()
        {
            var l = new LocalPosition(new Vector3(1.5f, -2.5f, 3.5f));
            Assert.AreEqual(1.5f, l.Value.x);
            Assert.AreEqual(-2.5f, l.Value.y);
            Assert.AreEqual(3.5f, l.Value.z);
        }

        [Test]
        public void Construction_FromThreeFloats_PreservesValue()
        {
            var l = new LocalPosition(1.5f, -2.5f, 3.5f);
            Assert.AreEqual(1.5f, l.Value.x);
            Assert.AreEqual(-2.5f, l.Value.y);
            Assert.AreEqual(3.5f, l.Value.z);
        }

        [Test]
        public void Equality_SameValue_IsEqual()
        {
            var a = new LocalPosition(1.0f, 2.0f, 3.0f);
            var b = new LocalPosition(1.0f, 2.0f, 3.0f);
            Assert.IsTrue(a == b);
            Assert.IsTrue(a.Equals(b));
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Addition_PreservesLocalCoords()
        {
            var l = new LocalPosition(10.0f, 20.0f, 30.0f);
            var delta = new Vector3(1.0f, 2.0f, 3.0f);
            var result = l + delta;
            Assert.AreEqual(11.0f, result.Value.x);
            Assert.AreEqual(22.0f, result.Value.y);
            Assert.AreEqual(33.0f, result.Value.z);
        }

        [Test]
        public void Subtraction_PreservesLocalCoords()
        {
            var l = new LocalPosition(10.0f, 20.0f, 30.0f);
            var delta = new Vector3(1.0f, 2.0f, 3.0f);
            var result = l - delta;
            Assert.AreEqual(9.0f, result.Value.x);
            Assert.AreEqual(18.0f, result.Value.y);
            Assert.AreEqual(27.0f, result.Value.z);
        }

        [Test]
        public void Difference_OfTwoLocalPositions_IsDisplacement()
        {
            var a = new LocalPosition(10.0f, 20.0f, 30.0f);
            var b = new LocalPosition(1.0f, 2.0f, 3.0f);
            Vector3 displacement = a - b;
            Assert.AreEqual(9.0f, displacement.x);
            Assert.AreEqual(18.0f, displacement.y);
            Assert.AreEqual(27.0f, displacement.z);
        }

        [Test]
        public void DistanceTo_OriginToUnit_IsCorrect()
        {
            var origin = LocalPosition.Zero;
            var unit = new LocalPosition(3.0f, 4.0f, 0.0f);
            Assert.AreEqual(5.0f, origin.DistanceTo(unit), 1e-5f);
        }
    }
}
