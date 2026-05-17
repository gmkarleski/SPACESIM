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

        /// <summary>
        /// Bug-regression test for commit 034. The end-to-end Play verification of commit 033
        /// surfaced this exact ordering failure: an anchor's <c>OnEnable</c> ran before the
        /// manager's <c>Awake</c>, so the anchor's old (pre-034) registration check found
        /// <c>Instance == null</c>, logged a warning, and silently did not register. The
        /// commit-034 fix routes registration through the static pending queue, which
        /// <c>Awake</c> drains. This test exercises that path at PlayMode-level so the bug
        /// can't regress.
        ///
        /// The scenario is: a listener calls <see cref="FloatingOriginManager.RegisterListenerSafe"/>
        /// before any <see cref="FloatingOriginManager"/> exists in the scene. We then create
        /// the manager (via the SetUp's earlier AddComponent flow — actually, SetUp already
        /// did that, but Awake fires on the next yield not on AddComponent itself). After
        /// yielding to let Awake run, the listener should be registered as an active listener
        /// and should receive a triggered shift. Failure mode if the fix regresses: the
        /// listener is never moved out of the pending queue and never receives the shift.
        /// </summary>
        [UnityTest]
        public IEnumerator DeferredRegistration_ListenerRegisteredBeforeAwake_ReceivesShift()
        {
            // Pre-condition: SetUp created _manager via AddComponent but Awake hasn't fired
            // yet on this frame. We need a clean slate: destroy SetUp's manager GameObject
            // immediately so we can stage the "listener registers BEFORE any manager exists"
            // scenario without the SetUp manager interfering.
            if (_managerGo != null) Object.DestroyImmediate(_managerGo);
            _managerGo = null;
            _manager = null;
            FloatingOriginManager.ClearInstanceForTesting();
            Assert.IsNull(FloatingOriginManager.Instance,
                "Sanity: Instance should be null at start of test (no manager in scene).");

            // Queue a listener while no manager exists in the scene.
            var listener = new PlayModeCountingListener();
            FloatingOriginManager.RegisterListenerSafe(listener);
            Assert.AreEqual(1, FloatingOriginManager.PendingListenerCount,
                "Listener should be in the pending queue.");

            // Now create the manager fresh. AddComponent does NOT fire Awake immediately —
            // Awake will fire on the next yield. This is the canonical "anchor was already
            // queued before any manager existed" scenario.
            var lateGo = new GameObject("LateManagerGo");
            var lateMgr = lateGo.AddComponent<FloatingOriginManager>();

            // Yield to let Awake fire on the new manager. Awake should drain the pending
            // queue into its active listener list.
            yield return null;

            Assert.AreEqual(lateMgr, FloatingOriginManager.Instance,
                "After yield, the new manager should be Instance.");
            Assert.AreEqual(0, FloatingOriginManager.PendingListenerCount,
                "Pending queue should be drained.");
            Assert.AreEqual(1, lateMgr.ListenerCount,
                "Active listener list should contain the previously-pending listener.");

            // Trigger a shift; the drained listener should be notified.
            lateMgr.MaybeShiftOrigin(new WorldPosition(60_000.0, 0.0, 0.0));
            Assert.AreEqual(1, listener.ShiftCount,
                "Drained listener should have received the shift notification end-to-end.");

            // Cleanup.
            if (lateGo != null) Object.DestroyImmediate(lateGo);
        }

        private class PlayModeCountingListener : IFloatingOriginListener
        {
            public int ShiftCount;
            public void OnFloatingOriginShifted(Unity.Mathematics.double3 shiftDelta)
            {
                ShiftCount++;
            }
        }
    }
}
