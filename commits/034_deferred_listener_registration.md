# 034: Deferred listener registration — FloatingOriginAnchor wakes before FloatingOriginManager without losing shifts

Resolve the architectural bug surfaced by commit 033's end-to-end Play verification: `FloatingOriginAnchor.OnEnable` could fire before `FloatingOriginManager.Instance` was set, causing the anchor to silently fail to register as a listener. After this commit, listeners use a static `RegisterListenerSafe` / `UnregisterListenerSafe` facade that queues registrations into a pending list when the manager doesn't exist yet, and the manager's `Awake` drains the queue when it claims the singleton. Order between `FloatingOriginAnchor.Awake` and `FloatingOriginManager.Awake` no longer matters, which is the architecturally correct posture for runtime-spawned vessels in later commits.

This commit also establishes **end-to-end Play verification** as a new standing category in the verification battery for architectural commits with test scenes, alongside file-level, compilation, and unit-test checks. The category is established by this commit because commit 033's verification gap — code-level checks all passed, but the first time anyone pressed Play the architecture didn't work — is the empirical evidence that file-level + unit tests are necessary but not sufficient for architectural correctness.

## Scope

- `SPACESIM/Assets/Scripts/Foundation/Coordinates/FloatingOriginManager.cs` — modified. Adds a static `List<IFloatingOriginListener> _pendingListeners`. Adds static `RegisterListenerSafe` / `UnregisterListenerSafe` public methods. Adds an internal `DrainPendingForTesting(FloatingOriginManager)` hook for EditMode-test drain simulation. `Awake` now calls `DrainPendingListeners()` after claiming `Instance`. `ClearInstanceForTesting` now also clears the pending queue. Class-level XML doc updated with a `DEFERRED LISTENER REGISTRATION (commit 034)` section. Existing instance `RegisterListener` / `UnregisterListener` methods untouched — tests and direct-access call sites continue to use them.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/FloatingOriginAnchor.cs` — modified. `OnEnable` replaced with a single call to `FloatingOriginManager.RegisterListenerSafe(this)`. `OnDisable` replaced with a single call to `FloatingOriginManager.UnregisterListenerSafe(this)`. The previous `Debug.LogWarning` for "manager not available" is removed entirely (the deferred-registration pattern makes that case a normal expected code path, not an error). Class-level XML doc updated with a `DEFERRED REGISTRATION (commit 034)` section.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/FloatingOriginManagerTests.cs` — extended. Adds 11 EditMode tests in a new `// ----- Deferred listener registration (commit 034) -----` section covering queue-while-null, register-while-set, null-ignore, dedup, unregister-pending, unregister-active, drain semantics, clear-clears-queue, and end-to-end drained-listener-receives-shift. Adds two private test helpers (`SetInstance` via reflection, `FloatingOriginManager_DrainPendingForTesting` via reflection).
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/PlayModeTests/FloatingOriginManagerPlayModeTests.cs` — extended. Adds 1 PlayMode regression test (`DeferredRegistration_ListenerRegisteredBeforeAwake_ReceivesShift`) that exercises the exact ordering failure mode commit 033 surfaced: queue listener while no manager exists, create manager, yield to let Awake fire and drain, trigger shift, verify listener got notified. Adds a private `PlayModeCountingListener` test helper.

No CONSTRAINTS update. No scene changes. No asmdef changes. No `IFloatingOriginListener` interface changes. No `SimTickController` changes.

## Rationale

### The bug commit 033 verification surfaced

Commit 033's verification battery passed 117 / 117 file-level checks and the Test Runner showed 92 EditMode + 2 PlayMode tests green. All of this was correct. None of it caught the bug.

When the test scene was finally built and Play pressed, the diagnostic UI rendered, the TestShiftDriver moved the sphere at the configured 10 km/s, the world position advanced cleanly, the SimTickController advanced its tick counter — and zero shifts ever fired. The sphere's world position climbed past the 50 km threshold to 500+ km without the floating origin ever updating.

The Console showed exactly one warning:

```
FloatingOriginAnchor on 'TestSubject' enabled before
FloatingOriginManager singleton was available. Anchor will not
receive shifts. Ensure the FloatingOriginManager GameObject's
script execution order runs before anchored GameObjects...
```

This warning was logged by the anchor itself in its old `OnEnable` body. The pre-034 code did:

```csharp
private void OnEnable()
{
    if (FloatingOriginManager.Instance != null)
    {
        FloatingOriginManager.Instance.RegisterListener(this);
    }
    else
    {
        Debug.LogWarning("...enabled before FloatingOriginManager singleton was available...");
    }
}
```

Unity's MonoBehaviour lifecycle runs `Awake` and `OnEnable` for components in an undefined order across distinct GameObjects unless explicit Script Execution Order is configured in Project Settings. In the TestCoordinates scene, the Hierarchy order is FloatingOriginRoot (index 2) → SimTickRoot (index 3) → TestSubject (index 4), but Unity does not guarantee that Hierarchy order produces Awake order. The anchor's `OnEnable` fired before the manager's `Awake`, so `Instance` was `null`, the warning fired, and the anchor never registered. Later when the manager's `Awake` ran and claimed `Instance`, it had no way to discover the anchor that had given up earlier in the frame.

The warning was diagnostically perfect — it named the exact failure mode and even suggested a Script Execution Order fix. The behavior the warning produced (silently no shifts) was architecturally wrong. The system depended on a global ordering invariant that was not enforced and not documented at the scene-configuration level.

### Why Script Execution Order is the wrong fix

A reader of the warning might naturally reach for Project Settings → Script Execution Order and set `FloatingOriginManager` to run before `FloatingOriginAnchor`. This works for the test scene. It does not work for the production architecture commit 034 is building toward.

Future commits will spawn vessel containers at runtime via prefab instantiation, asset bundle loading, and object pooling. Runtime-instantiated GameObjects have no relationship to Project Settings → Script Execution Order — that configuration applies to scene-loaded components, not to `Object.Instantiate(prefab)` results. A vessel that pools a new anchor instance partway through gameplay cannot rely on Script Execution Order having anything to say about it.

The architecturally correct fix decouples listener registration from manager-lifecycle ordering entirely. Registration becomes a request that succeeds in both orderings: if the manager exists, register directly; if not, queue and drain later. This pattern works for scene-loaded components, runtime-instantiated components, pooled components, and any other future ordering scenario without further work.

### The pending-queue design

The pending queue is a `static` field on `FloatingOriginManager`, not an instance field. This is structurally necessary: at the moment a listener wants to register before any manager exists, there is no instance to hold the queue on. The queue lives at the class level, indexed only by the (singleton) class identity.

```csharp
private static readonly List<IFloatingOriginListener> _pendingListeners
    = new List<IFloatingOriginListener>();
```

`RegisterListenerSafe` is the canonical registration path:

```csharp
public static void RegisterListenerSafe(IFloatingOriginListener listener)
{
    if (listener == null) return;
    if (Instance != null)
    {
        Instance.RegisterListener(listener);  // direct path: register on active manager
        return;
    }
    // deferred path: queue for later drain
    if (!_pendingListeners.Contains(listener)) _pendingListeners.Add(listener);
}
```

The direct path delegates to the existing instance `RegisterListener`, which already deduplicates and is exercised by all the existing pre-034 tests. The deferred path is new and is deduplicated at the queue level as well — a listener that calls `RegisterListenerSafe` twice before `Awake` fires gets enqueued once, not twice.

`Awake` drains the queue immediately after claiming `Instance`:

```csharp
private void Awake()
{
    if (Instance != null && Instance != this) { /* duplicate path, unchanged */ return; }
    Instance = this;
    DrainPendingListeners();
}

private void DrainPendingListeners()
{
    if (_pendingListeners.Count == 0) return;
    var pending = _pendingListeners.ToArray();
    _pendingListeners.Clear();
    for (int i = 0; i < pending.Length; i++)
    {
        RegisterListener(pending[i]);  // dedup via the same instance path
    }
}
```

The `ToArray` snapshot + `Clear` + reassign-via-instance pattern handles the unlikely-but-legal case of a listener registering a sibling listener during its own initialization. Reassigning via `RegisterListener` (the instance method) preserves deduplication semantics uniformly across deferred and direct paths — if for any reason a listener appears in both the pending queue and the active list, it ends up exactly once in the active list after drain.

### Unregister handles both states

`UnregisterListenerSafe` symmetrically handles three cases — listener in active list, listener in pending queue, listener in neither:

```csharp
public static void UnregisterListenerSafe(IFloatingOriginListener listener)
{
    if (listener == null) return;
    if (Instance != null)
    {
        Instance.UnregisterListener(listener);
    }
    _pendingListeners.Remove(listener);  // always check pending too; harmless if absent
}
```

The unconditional pending check handles the case where an anchor was queued before the manager's `Awake` and then destroyed (e.g., the GameObject loaded with a queued anchor gets scene-unloaded before Awake fires). Without this, a queued listener with no live GameObject could survive across `ClearInstanceForTesting` calls between tests — but `ClearInstanceForTesting` also clears the queue (see below), so the only window where this matters is "anchor queued and destroyed within the same Unity frame, no test boundary." That window is real for runtime-spawned vessels and the unconditional pending check is the cheap correct behavior.

### Removed the OnEnable warning

The pre-034 warning ("anchor enabled before manager singleton was available") was load-bearing diagnostic when the failure mode existed. With deferred registration, the case the warning flagged is now a normal expected path — anchors that register before the manager just go through the queue. Keeping the warning would make every scene load that exercises the deferred path emit log noise; removing it leaves the Console clean for actual problems. The architectural fact is documented in the class XML doc instead, where it belongs.

### Test-only DrainPendingForTesting hook

EditMode tests cannot fire `Awake` on `AddComponent` results. To exercise the drain logic without entering PlayMode, the test infrastructure needs a way to invoke the drain explicitly. The hook is `internal` rather than `public` so non-test code can't accidentally call it. The name is deliberately narrow — `DrainPendingForTesting` is obviously about pending-listeners drain only; nothing about it suggests "simulate Awake's other responsibilities." If Awake grows additional responsibilities in later commits, those responsibilities get their own test-only hooks; this hook stays focused.

```csharp
internal static void DrainPendingForTesting(FloatingOriginManager instance)
{
    if (instance == null) return;
    instance.DrainPendingListeners();
}
```

EditMode tests reach the hook via reflection (Type.GetMethod with `BindingFlags.NonPublic | Static`) rather than `InternalsVisibleTo`, which would couple the assembly metadata to test-only access. Reflection is locally awkward but globally cleaner — the test self-contains its access pattern.

### ClearInstanceForTesting clears the queue too

`ClearInstanceForTesting` was added in commit 029 to reset `Instance` between EditMode tests. With commit 034 adding the pending queue as additional static state, `ClearInstanceForTesting` is extended to clear that too. A test that wants a fresh manager state expects no leftover queued listeners from prior tests. Tests that specifically exercise the deferred-registration path call `ClearInstanceForTesting` first, then explicitly enqueue listeners via `RegisterListenerSafe` within the test body. The documented contract is "clear all of FloatingOriginManager's static observable state."

## Test coverage

EditMode tests added (11 new tests, in addition to the 92 from commit 033):

1. `RegisterListenerSafe_WhenInstanceNull_QueuesPending` — listener goes into queue, not active list, when Instance is null.
2. `RegisterListenerSafe_WhenInstanceSet_RegistersDirectly` — listener goes into active list, not queue, when Instance is set.
3. `RegisterListenerSafe_Null_IsIgnored` — null argument doesn't crash and doesn't add anything.
4. `RegisterListenerSafe_DuplicateWhileQueued_DoesNotDouble` — same listener enqueued twice ends up once.
5. `UnregisterListenerSafe_WhenInPendingQueue_RemovesFromQueue` — unregister while pending removes from queue.
6. `UnregisterListenerSafe_WhenInActiveList_RemovesFromList` — unregister while active removes from active list.
7. `UnregisterListenerSafe_Null_IsIgnored` — null argument doesn't crash.
8. `DrainPendingForTesting_MovesQueuedListenersToActive` — drain transfers queue → active.
9. `DrainPendingForTesting_NullInstance_IsNoOp` — null argument doesn't crash.
10. `ClearInstanceForTesting_AlsoClearsPendingQueue` — clear-test clears both pieces of static state.
11. `DrainedListener_ReceivesShifts` — end-to-end: queue listener, drain, shift, listener notified.

PlayMode test added (1 new test, in addition to the 2 from commit 032):

- `DeferredRegistration_ListenerRegisteredBeforeAwake_ReceivesShift` — the bug-regression test. Clears Instance, enqueues a listener while no manager exists, creates a manager component, yields one frame to let Awake fire and drain, asserts listener moved out of queue into active list, triggers a shift, asserts listener received it. This is the test that would have caught the commit-033 bug at PlayMode-test time if it had existed.

Expected test counts after this commit: 92 + 11 = **103 EditMode tests**, 2 + 1 = **3 PlayMode tests**, total **106 tests** all green.

## End-to-end verification observations

This section is new with commit 034 and is intended as a standing category for future architectural commits with test scenes.

**The verification-gap principle.** Architectural commits that include test scenes have a class of bugs that cannot be caught by file-level checks (do the expected files exist?), compilation checks (does it build clean?), or unit-test checks (do isolated component behaviors work?). These bugs live in the interaction between components at scene-load time, in MonoBehaviour lifecycle ordering, in how components find each other, and in how cross-component contracts hold up under Unity's actual play loop. The only way to find these bugs is to open the test scene and press Play.

Commit 033's verification battery passed cleanly at the file and unit level. The architectural bug (anchor wakes before manager, registration silently fails) was only exposed by the first end-to-end Play verification when the test scene was built up and run for real. Before that, the architecture *looked correct in isolation* — every component had passing tests, every interface was symmetric, every contract was documented. The test that was missing was "do these components find each other under Unity's actual scene-load ordering."

**The new standing category.** From commit 034 forward, architectural commits with test scenes get end-to-end Play verification as a standard category in their verification battery, alongside:

1. **File-level verification** — expected files exist with expected content/structure.
2. **Compilation verification** — Unity compiles the project with zero errors and zero new warnings.
3. **Unit test verification** — Test Runner shows all expected EditMode and PlayMode tests green.
4. **End-to-end Play verification (new)** — the test scene loads, Play runs for an interval long enough to exercise the architecture's main behaviors, and the observed behavior matches the documented expectations. Specific checks vary by commit; for floating-origin commits the checks are "no missing-component warnings in Console, shifts fire at expected intervals at the configured speed, diagnostic UI shows expected counter advances."

The verification is interactive (a human presses Play and observes), not automated. Automating it would require Unity batch mode + scene-test scaffolding that doesn't exist yet. The standard for "architectural commit with test scene" is narrow enough that the manual cost is acceptable — coordinate-system, sim-tick, physics-mode-transition, vessel-spawning, networking are the commits where this category applies. Pure-data commits (CONSTRAINTS edits, design documents, asmdef fixes) don't need it.

**Discipline.** A commit isn't truly verified until someone has pressed Play and watched the expected behavior happen. The file-level and unit-test checks are necessary but not sufficient. This is the recorded lesson from commit 033's verification gap. It's not yet a formal workflow rule (one data point), but the principle goes into the commit-034 artifact for future reference. If a subsequent commit's end-to-end verification surfaces a similar gap, the third data point will warrant promoting the principle to workflow rule 7 (after the still-pending sandbox-mount-staleness rule 6 from commits 028/031/033 lands in commit 035).

## User-side verification

After files land:

1. Open Unity (or refocus if already open) and let it recompile. Console should show 0 errors and 0 warnings related to commit-034 changes.
2. Open Test Runner via Window → General → Test Runner.
3. EditMode tab → Run All. Expected: **103 tests green** (92 from prior commits + 11 new commit-034 tests). 0 failed, 0 ignored.
4. PlayMode tab → Run All. Expected: **3 tests green** (2 from commit 032 + 1 new commit-034 regression test). 0 failed, 0 ignored.
5. Open `Assets/Scenes/TestCoordinates.unity`. The scene should already contain Camera, Directional Light, FloatingOriginRoot, SimTickRoot, TestSubject, Canvas → Text (Legacy), EventSystem from the prior verification session. No new scene setup required.
6. Press Play. Run for 15-20 seconds. Verify:
   - **No "anchor enabled before manager" warning in Console.** The pre-034 warning is removed entirely; the Console should be clean (or have only unrelated informational logs).
   - **Shift events fire at ~5 second intervals.** TestShiftDriver logs `[TestShiftDriver] Origin shifted (#N)` each time the manager's `ShiftCount` advances. At the default 10 km/s travel speed and 50 km shift threshold, this is approximately one shift per 5 seconds. After 15 seconds, expect ~3 shifts logged.
   - **Diagnostic UI shows non-zero "Shift count" by 15 seconds.** The UI label written by TestShiftDriver includes a "Shift count: N" line. By the 15-second mark, N should be ≥ 2.
   - **TestSubject stays near origin visually.** Without commit 034 (or with commit 034 broken), the sphere drifts off-screen at 10 km/s and never returns; with commit 034 working, the sphere's local position resets every shift, so the rendered position oscillates within ±50 km rather than growing unbounded. Visually this means the sphere stays roughly visible in the Game view (with the default camera position), though it does drift gradually until the next shift snaps it back.
7. Stop Play mode.

If any of these checks fail, surface before considering commit 034 complete. The end-to-end verification step is the new standing category established by this commit; skipping it would defeat the purpose.

## Notes for future commits

- Workflow rule 6 (sandbox-mount-staleness from commits 028/031/033) is still pending formalization. The pattern has three data points and is ready for promotion to a formal rule, but commit 034 stays focused on the deferred-registration fix. Workflow rule 6 lands in commit 035, where the cumulative discipline can be documented cleanly without competing for space with this commit's architectural narrative.
- The end-to-end Play verification standing category is established by this commit but is not yet workflow rule 7. One data point (commit 033's verification gap → commit 034's fix) is not enough to formalize a rule. A second data point from a future architectural commit's verification surfacing a similar gap would justify promotion. Until then, the category is documented here as a recorded principle that informs future commits' verification batteries without being enforced.
- Commit 035 is currently planned to formalize workflow rule 6 (sandbox-mount-staleness). Commit 036+ continues toward vessel containers and PhysicsModeTransitionService per the previously-tracked roadmap.
