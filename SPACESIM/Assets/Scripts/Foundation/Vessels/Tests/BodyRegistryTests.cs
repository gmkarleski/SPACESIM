using System;
using NUnit.Framework;
using SpaceSim.Foundation.Vessels;
using UnityEngine;
// Alias UnityEngine.Object to disambiguate from System.Object. Matches the pattern
// established in VesselRegistryTests / VesselTests.
using UnityObject = UnityEngine.Object;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="BodyRegistry"/>. The registry is a static class with
    /// no MonoBehaviour lifecycle, so all tests are EditMode (no Awake-firing required).
    /// Pattern parallels <see cref="VesselRegistryTests"/> exactly.
    ///
    /// Tests construct throwaway <see cref="ReferenceBody"/> components via
    /// <c>AddComponent&lt;ReferenceBody&gt;()</c>. These bodies have <c>Awake</c>-time
    /// state UNPOPULATED unless the test explicitly calls
    /// <see cref="ReferenceBody.InitializeBodyForTesting"/> — in EditMode, Unity's Awake
    /// does not fire on AddComponent. Tests that need a body's BodyId or registration
    /// to be in place either call <c>InitializeBodyForTesting</c> directly or assign the
    /// fields via the registry's public API.
    ///
    /// <c>ClearForTesting</c> in SetUp/TearDown ensures static state doesn't leak between
    /// test methods.
    /// </summary>
    public class BodyRegistryTests
    {
        private GameObject _bodyGo1;
        private GameObject _bodyGo2;
        private ReferenceBody _body1;
        private ReferenceBody _body2;

        [SetUp]
        public void SetUp()
        {
            BodyRegistry.ClearForTesting();

            _bodyGo1 = new GameObject("TestBody1");
            _body1 = _bodyGo1.AddComponent<ReferenceBody>();
            // Initialize to populate BodyId (Awake doesn't fire in EditMode).
            _body1.InitializeBodyForTesting();

            _bodyGo2 = new GameObject("TestBody2");
            _body2 = _bodyGo2.AddComponent<ReferenceBody>();
            _body2.InitializeBodyForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bodyGo1 != null) UnityObject.DestroyImmediate(_bodyGo1);
            if (_bodyGo2 != null) UnityObject.DestroyImmediate(_bodyGo2);
            BodyRegistry.ClearForTesting();
        }

        // ----- RegisterBodySafe -----

        [Test]
        public void BodyRegistry_RegisterBodySafe_AddsBody()
        {
            // SetUp's InitializeBodyForTesting already calls RegisterBodySafe via
            // the body's own Awake-equivalent path. Clear and re-register to test
            // the API directly.
            BodyRegistry.ClearForTesting();
            Assert.AreEqual(0, BodyRegistry.BodyCount, "Sanity: registry should be empty after Clear");

            BodyRegistry.RegisterBodySafe(_body1);

            Assert.AreEqual(1, BodyRegistry.BodyCount);
            Assert.AreSame(_body1, BodyRegistry.Bodies[0]);
        }

        [Test]
        public void BodyRegistry_RegisterBodySafe_DeDupsExistingBody()
        {
            BodyRegistry.ClearForTesting();
            BodyRegistry.RegisterBodySafe(_body1);
            BodyRegistry.RegisterBodySafe(_body1);  // duplicate
            BodyRegistry.RegisterBodySafe(_body1);  // duplicate

            Assert.AreEqual(1, BodyRegistry.BodyCount,
                "Duplicate register calls should not add the same body multiple times");
        }

        [Test]
        public void BodyRegistry_RegisterBodySafe_NullIsIgnored()
        {
            BodyRegistry.ClearForTesting();
            BodyRegistry.RegisterBodySafe(null);

            Assert.AreEqual(0, BodyRegistry.BodyCount, "Null register should be silently ignored");
        }

        // ----- UnregisterBodySafe -----

        [Test]
        public void BodyRegistry_UnregisterBodySafe_RemovesBody()
        {
            // SetUp's InitializeBodyForTesting registered both bodies. Unregister one
            // and verify the count drops.
            Assert.AreEqual(2, BodyRegistry.BodyCount, "Sanity: SetUp registered 2 bodies");

            BodyRegistry.UnregisterBodySafe(_body1);

            Assert.AreEqual(1, BodyRegistry.BodyCount);
            Assert.AreSame(_body2, BodyRegistry.Bodies[0]);
        }

        // ----- TryGetBodyById -----

        [Test]
        public void BodyRegistry_TryGetBodyById_ReturnsRegisteredBody()
        {
            // _body1.BodyId was assigned in SetUp's InitializeBodyForTesting.
            bool found = BodyRegistry.TryGetBodyById(_body1.BodyId, out ReferenceBody result);

            Assert.IsTrue(found, "Registered body should be findable by its BodyId");
            Assert.AreSame(_body1, result);
        }

        [Test]
        public void BodyRegistry_TryGetBodyById_ReturnsFalseForMissingBody()
        {
            Guid unregisteredGuid = Guid.NewGuid();

            bool found = BodyRegistry.TryGetBodyById(unregisteredGuid, out ReferenceBody result);

            Assert.IsFalse(found, "Lookup for an unregistered Guid should return false");
            Assert.IsNull(result, "Out parameter should be null on miss");
        }

        [Test]
        public void BodyRegistry_TryGetBodyById_GuidEmpty_AlwaysReturnsFalse()
        {
            // Guid.Empty is the sentinel for "no body assigned" (e.g., ParentBodyId on
            // a top-level body). Lookup with Empty should always return false even if
            // some body's BodyId happened to be uninitialized (which shouldn't happen
            // in production but would be a real corner case).
            bool found = BodyRegistry.TryGetBodyById(Guid.Empty, out ReferenceBody result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        // ----- GetChildrenOf -----

        [Test]
        public void BodyRegistry_GetChildrenOf_FindsChildren()
        {
            // Wire _body1 as _body2's parent via the parameterized
            // InitializeBodyForTesting overload's parentBody parameter. The
            // reflection-on-private-fields path that previously set up this
            // relationship has been replaced by the typed overload (introduced in
            // the ReferenceBody test-seam cleanup commit 293d2b4). The compiler now
            // serves the "parentBody field exists" sanity-check function that the
            // prior Assert.IsNotNull on parentBodyField performed — if the overload's
            // signature ever changes, the migration breaks at build time. The mass /
            // SOI values match the field defaults the prior no-arg
            // InitializeBodyForTesting calls preserved; the test asserts only on
            // registry behavior (Children count + identity), not on mass / SOI, so
            // the explicit pass is documentation, not a semantic change.
            BodyRegistry.ClearForTesting();
            _body1.InitializeBodyForTesting();
            _body2.InitializeBodyForTesting(
                massKg: 5.972e24,
                soiRadiusMeters: double.PositiveInfinity,
                parentBody: _body1);

            // _body1 has no parent (top-level); _body2 has _body1 as parent.
            var childrenOfBody1 = BodyRegistry.GetChildrenOf(_body1);
            var childrenOfBody2 = BodyRegistry.GetChildrenOf(_body2);

            Assert.AreEqual(1, childrenOfBody1.Count, "_body1 should have exactly one child (_body2)");
            Assert.AreSame(_body2, childrenOfBody1[0]);
            Assert.AreEqual(0, childrenOfBody2.Count, "_body2 has no children");
        }

        [Test]
        public void BodyRegistry_GetChildrenOf_NullParent_ReturnsEmptyList()
        {
            var children = BodyRegistry.GetChildrenOf(null);

            Assert.IsNotNull(children, "GetChildrenOf(null) should return an empty list, not null");
            Assert.AreEqual(0, children.Count);
        }

        // ----- ClearForTesting -----

        [Test]
        public void BodyRegistry_ClearForTesting_EmptiesRegistry()
        {
            // SetUp registered two bodies.
            Assert.AreEqual(2, BodyRegistry.BodyCount, "Sanity: SetUp registered 2 bodies");

            BodyRegistry.ClearForTesting();

            Assert.AreEqual(0, BodyRegistry.BodyCount, "ClearForTesting should empty the registry");
        }
    }
}
