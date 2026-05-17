using System.Collections;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpaceSim.Foundation.Coordinates.Tests
{
    /// <summary>
    /// PlayMode tests for <see cref="FloatingOriginManager"/>'s singleton lifecycle.
    ///
    /// The singleton claim happens in <c>Awake</c>, which only fires when Unity's play loop
    /// is running. EditMode tests do not invoke <c>Awake</c> on <c>AddComponent&lt;&gt;()</c>
    /// — MonoBehaviour lifecycle hooks (<c>Awake</c>, <c>Start</c>, <c>OnEnable</c>) require
    /// the play loop. The two tests here use <c>[UnityTest] IEnumerator</c> with
    /// <c>yield return null</c> to let Unity advance one frame between component creation and
    /// the assertion, giving <c>Awake</c> a chance to run and claim <c>Instance</c>.
    ///
    /// The 17 other manager tests live in <c>Tests/FloatingOriginManagerTests.cs</c> as
    /// regular EditMode tests because they use the local <c>_manager</c> reference rather
    /// than reading <c>Instance</c> — they don't depend on <c>Awake</c> having fired.
    ///
    /// This file lives in a sibling <c>PlayModeTests/</c> folder with its own assembly
    /// definition (<c>SpaceSim.Foundation.Coordinates.PlayModeTests.asmdef</c>) that uses
    /// <c>optionalUnityReferences: ["TestAssemblies"]</c> and an empty <c>includePlatforms</c>
    /// list. The PlayMode asmdef must NOT be Editor-only because PlayMode tests run in the
    /// player by definition during a test run; restricting to the Editor platform would
    /// prevent Test Runner from discovering them in the PlayMode tab.
    /// </summary>
    public class FloatingOriginManagerPlayModeTests
    {
        private GameObject _managerGo;
        private FloatingOriginManager _manager;

        [SetUp]
        public void SetUp()
        {
            // Clear any leftover Instance from a prior test (defensive — TearDown should have
            // handled this, but if a previous test crashed mid-flight the static ref can leak).
            FloatingOriginManager.ClearInstanceForTesting();
            _managerGo = new GameObject("TestFloatingOriginManager");
            _manager = _managerGo.AddComponent<FloatingOriginManager>();
            // Note: Awake has NOT fired yet on _manager. Each [UnityTest] yields once after
            // SetUp completes, allowing Awake to fire before the test body asserts.
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerGo != null) Object.DestroyImmediate(_managerGo);
            FloatingOriginManager.ClearInstanceForTesting();
        }

        [UnityTest]
        public IEnumerator Singleton_FirstInstance_BecomesInstance()
        {
            // Let Awake fire on _manager. After the yield, the first-instance branch of
            // FloatingOriginManager.Awake should have executed, claiming the singleton.
            yield return null;
            Assert.AreEqual(_manager, FloatingOriginManager.Instance);
        }

        [UnityTest]
        public IEnumerator Singleton_DuplicateInstance_LogsErrorAndDestroys()
        {
            // First yield: let Awake fire on _manager so it claims Instance.
            yield return null;

            // Now adding a second manager component on a new GameObject should produce an
            // error log on the duplicate's Awake and the duplicate's component should
            // self-destruct. The Instance should remain the original _manager.
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*Duplicate FloatingOriginManager.*"));
            var dupGo = new GameObject("DuplicateManagerGo");
            var dup = dupGo.AddComponent<FloatingOriginManager>();

            // Second yield: let Awake fire on the duplicate so the duplicate-detection
            // branch of FloatingOriginManager.Awake runs.
            yield return null;

            Assert.AreEqual(_manager, FloatingOriginManager.Instance,
                "Singleton Instance should remain the original after a duplicate is rejected.");

            if (dupGo != null) Object.DestroyImmediate(dupGo);
        }
    }
}
