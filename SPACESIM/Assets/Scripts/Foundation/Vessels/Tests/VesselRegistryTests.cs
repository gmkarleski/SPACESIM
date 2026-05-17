using System.Collections.Generic;
using NUnit.Framework;
using SpaceSim.Foundation.Vessels;
using UnityEngine;
// Alias UnityEngine.Object to disambiguate from System.Object. This file doesn't
// currently import System.Object directly (only System.Collections.Generic), so bare
// 'Object' resolves unambiguously here — but the alias matches the pattern used in
// the other two test files and guards against future drift if 'using System;' gets
// added for any reason.
using UnityObject = UnityEngine.Object;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="VesselRegistry"/>. The registry is a static class with
    /// no MonoBehaviour lifecycle, so all tests are EditMode (no Awake-firing required).
    ///
    /// Tests use throwaway <see cref="Vessel"/> components created via
    /// <c>AddComponent&lt;Vessel&gt;()</c>. These vessels are NOT Initialize'd — the
    /// registry doesn't care about a vessel's internal state, only its identity. The
    /// registry stores Vessel references; the tests just need distinct references.
    ///
    /// <c>ClearForTesting</c> in SetUp/TearDown ensures static state doesn't leak between
    /// test methods (same discipline as <c>FloatingOriginManager.ClearInstanceForTesting</c>
    /// from commit 029).
    /// </summary>
    public class VesselRegistryTests
    {
        private GameObject _vesselGo1;
        private GameObject _vesselGo2;
        private Vessel _vessel1;
        private Vessel _vessel2;

        [SetUp]
        public void SetUp()
        {
            VesselRegistry.ClearForTesting();

            _vesselGo1 = new GameObject("TestVessel1");
            _vessel1 = _vesselGo1.AddComponent<Vessel>();

            _vesselGo2 = new GameObject("TestVessel2");
            _vessel2 = _vesselGo2.AddComponent<Vessel>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_vesselGo1 != null) UnityObject.DestroyImmediate(_vesselGo1);
            if (_vesselGo2 != null) UnityObject.DestroyImmediate(_vesselGo2);
            VesselRegistry.ClearForTesting();
        }

        [Test]
        public void RegisterVesselSafe_AddsToList()
        {
            VesselRegistry.RegisterVesselSafe(_vessel1);
            Assert.AreEqual(1, VesselRegistry.VesselCount);
            Assert.AreSame(_vessel1, VesselRegistry.Vessels[0]);
        }

        [Test]
        public void RegisterVesselSafe_Duplicate_DoesNotDouble()
        {
            VesselRegistry.RegisterVesselSafe(_vessel1);
            VesselRegistry.RegisterVesselSafe(_vessel1);
            Assert.AreEqual(1, VesselRegistry.VesselCount,
                "Registering the same vessel twice should leave the list at count 1.");
        }

        [Test]
        public void RegisterVesselSafe_Null_IsIgnored()
        {
            VesselRegistry.RegisterVesselSafe(null);
            Assert.AreEqual(0, VesselRegistry.VesselCount);
        }

        [Test]
        public void UnregisterVesselSafe_RemovesFromList()
        {
            VesselRegistry.RegisterVesselSafe(_vessel1);
            VesselRegistry.RegisterVesselSafe(_vessel2);
            Assert.AreEqual(2, VesselRegistry.VesselCount);

            VesselRegistry.UnregisterVesselSafe(_vessel1);
            Assert.AreEqual(1, VesselRegistry.VesselCount);
            Assert.AreSame(_vessel2, VesselRegistry.Vessels[0]);
        }

        [Test]
        public void UnregisterVesselSafe_Null_IsIgnored()
        {
            VesselRegistry.RegisterVesselSafe(_vessel1);
            VesselRegistry.UnregisterVesselSafe(null);
            Assert.AreEqual(1, VesselRegistry.VesselCount,
                "Unregistering null should be a silent no-op, not affect the list.");
        }

        [Test]
        public void UnregisterVesselSafe_NotInList_IsNoOp()
        {
            VesselRegistry.RegisterVesselSafe(_vessel1);
            VesselRegistry.UnregisterVesselSafe(_vessel2);  // vessel2 was never registered
            Assert.AreEqual(1, VesselRegistry.VesselCount,
                "Unregistering an unregistered vessel should be a silent no-op.");
        }

        [Test]
        public void ClearForTesting_EmptiesTheList()
        {
            VesselRegistry.RegisterVesselSafe(_vessel1);
            VesselRegistry.RegisterVesselSafe(_vessel2);
            Assert.AreEqual(2, VesselRegistry.VesselCount);

            VesselRegistry.ClearForTesting();
            Assert.AreEqual(0, VesselRegistry.VesselCount);
        }

        [Test]
        public void Vessels_PropertyReturnsReadOnlyList()
        {
            VesselRegistry.RegisterVesselSafe(_vessel1);
            IReadOnlyList<Vessel> vessels = VesselRegistry.Vessels;
            Assert.AreEqual(1, vessels.Count);

            // The runtime type is List<Vessel> under the hood, but the public surface
            // is IReadOnlyList. Tests verifying the read-only contract should rely on the
            // interface, not the underlying type. Casting to List<Vessel> isn't expected
            // to succeed across versions; we don't test that escape hatch.
            Assert.IsInstanceOf<IReadOnlyList<Vessel>>(vessels,
                "Vessels property contract is IReadOnlyList<Vessel>.");
        }
    }
}
