# 032: PlayMode lifecycle tests fix — singleton tests moved to PlayMode

Move the two singleton-lifecycle tests from EditMode to a new PlayMode test assembly so Unity's `Awake` fires before assertions read `FloatingOriginManager.Instance`. The two tests previously failed in commit 031's verification (52 of 54 passing) because EditMode tests do not invoke MonoBehaviour lifecycle hooks on `AddComponent`. With this commit they run in PlayMode, where `[UnityTest] IEnumerator` + `yield return null` lets Unity advance one frame between component creation and the assertion. After this commit lands and the user opens Unity, Test Runner should show 17 EditMode tests + 2 PlayMode tests under the Coordinates subtree, with all 54 tests across the four test classes passing.

The fix uses a second assembly definition (`SpaceSim.Foundation.Coordinates.PlayModeTests.asmdef`) in a sibling folder (`PlayModeTests/`) to the existing `Tests/` folder. The two folders correspond to the two test modes: EditMode tests for component logic that doesn't depend on lifecycle hooks, PlayMode tests for the few cases that specifically exercise `Awake`/`Start`/`OnEnable`/etc.

This is the third asmdef commit in three working sessions, after commit 030 (added `Unity.Mathematics` to runtime asmdef references) and commit 031 (replaced `defineConstraints: ["UNITY_INCLUDE_TESTS"]` with `includePlatforms: ["Editor"]` on the test asmdef). The cumulative Unity-semantic learning across the three commits is captured in this artifact's "Diagnostic context from commit 031" section.

## Scope

- `SPACESIM/Assets/Scripts/Foundation/Coordinates/PlayModeTests/SpaceSim.Foundation.Coordinates.PlayModeTests.asmdef` — created. Canonical Unity 6 PlayMode form using `optionalUnityReferences: ["TestAssemblies"]` and `includePlatforms: []`. Matches Unity's official sample asmdefs for PlayMode (samples 10, 12 in `com.unity.test-framework`).
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/PlayModeTests/FloatingOriginManagerPlayModeTests.cs` — created. Two `[UnityTest] IEnumerator` test methods, `[SetUp]`, `[TearDown]`. Namespace `SpaceSim.Foundation.Coordinates.Tests` matching the EditMode test file's namespace so the two singleton-related test files share a coherent doc layout in Test Runner.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/FloatingOriginManagerTests.cs` — modified. Two `[Test]` methods removed (`Singleton_FirstInstance_BecomesInstance`, `Singleton_DuplicateInstance_LogsErrorAndDestroys`). `// ----- Singleton -----` header comment replaced with a `// ----- Singleton lifecycle tests moved to PlayMode -----` pointer comment naming the new file. Class-level XML docstring updated to describe the EditMode-only nature of the remaining tests and to point at the PlayMode file.

No other files touched. No runtime source changes. No production code modifications. No `Tests/SpaceSim.Foundation.Coordinates.Tests.asmdef` changes. No runtime asmdef changes. No CONSTRAINTS update. No scene changes.

## Rationale

Commit 031's diagnostic session via computer-use revealed that the two singleton tests failed not because of any production bug but because of an EditMode/PlayMode test-mode mismatch. The error pattern for both was identical:

```
Singleton_FirstInstance_BecomesInstance (0.001s)
Expected: <TestFloatingOriginManager (SpaceSim.Foundation.Coordinates.FloatingOriginManager)>
But was: null
```

The expected value is the manager component created in `[SetUp]`; the actual value (`null`) is what `FloatingOriginManager.Instance` returned at the time of the assertion. `Instance` is set in the manager's `Awake` method via `Instance = this`. In Unity's EditMode tests, `Awake` does NOT fire when `AddComponent<>()` is called — MonoBehaviour lifecycle hooks (`Awake`, `Start`, `OnEnable`) require the play loop, and EditMode tests run without it. So `Instance` stayed null and the assertions failed.

The 17 other manager tests in `FloatingOriginManagerTests.cs` all pass in EditMode because they use the local `_manager` reference (set by SetUp's return value of `AddComponent`) rather than reading `Instance`. The local reference doesn't depend on `Awake` having fired — it's just the component handle that `AddComponent` returned. Production code paths the EditMode tests exercise (`MaybeShiftOrigin`, `RegisterListener`, `WorldToLocal`, `LocalToWorld`) operate on fields and properties that are initialized at the field-declaration site, not in `Awake`.

The fix moves only the two affected tests into PlayMode. Production `FloatingOriginManager` stays clean — no `Initialize()` helper, no test-only initialization mutation. The Awake-based singleton-claim is the production behavior; the PlayMode tests run that production behavior end-to-end and verify it correctly. This is the canonical Unity testing pattern for component-lifecycle-dependent behavior.

**Why Pattern B (two asmdefs) over Pattern A (single asmdef, mix `[Test]` and `[UnityTest]`):**

Initial design exploration considered putting the `[UnityTest]` PlayMode tests in the existing EditMode asmdef (Pattern A) and trusting Unity 6 Test Runner to route them by attribute. Unity's official documentation contradicts that approach: a test assembly with `includePlatforms: ["Editor"]` runs only in EditMode regardless of which attributes its methods carry. The asmdef-level platform restriction filters out the assembly from PlayMode test runs entirely.

The correct pattern (Pattern B, which this commit implements) is two separate asmdefs:
- EditMode asmdef: `includePlatforms: ["Editor"]`. Contains `[Test]` methods.
- PlayMode asmdef: `includePlatforms: []` (any platform). Contains `[UnityTest] IEnumerator` methods.

The two asmdefs sit in sibling folders (`Tests/` and `PlayModeTests/`) for clear separation. Both reference the same runtime module (`SpaceSim.Foundation.Coordinates`) and Unity.Mathematics, so they share access to the types under test. Each asmdef carries its own test-framework wiring; they don't depend on each other.

**Asmdef form choice (Form 1 vs Form 2):**

Unity 6 supports two valid forms for test asmdefs:
- **Form 1 (canonical, minimal):** `optionalUnityReferences: ["TestAssemblies"]` plus minimum surface area. This is what Unity's own sample asmdefs use for PlayMode (samples 10 and 12 in `com.unity.test-framework@76560ee600cb/Samples~/`).
- **Form 2 (modern verbose):** explicit `references: ["UnityEngine.TestRunner", "UnityEditor.TestRunner"]`, `precompiledReferences: ["nunit.framework.dll"]`, `overrideReferences: true`, plus optional `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. This matches our existing EditMode asmdef post-commit-031.

This commit uses Form 1 for the PlayMode asmdef. Reasoning: (a) Unity's own samples for PlayMode use Form 1, suggesting it's the recommended canonical form; (b) Form 1 has a smaller surface area — fewer fields means fewer opportunities to trigger the kind of brittle behavior we hit in commits 030 and 031; (c) `optionalUnityReferences: ["TestAssemblies"]` is Unity's "tag this as a test assembly" shortcut that handles the test-framework linkage automatically; (d) the canonical form is more stable across Unity versions than the verbose form.

The two asmdefs in this project now use slightly different forms — EditMode uses Form 2 (verbose, with `references`/`precompiledReferences`/`overrideReferences`), PlayMode uses Form 1. This asymmetry is fine. The EditMode asmdef is post-commit-031 working and we don't need to change it; the PlayMode asmdef gets the minimum-surface-area form because we're writing it from scratch.

## Diagnostic context from commit 031

Commit 031 documented that the test asmdef was being filtered out by Test Runner despite the assembly compiling cleanly. The fix was to replace `defineConstraints: ["UNITY_INCLUDE_TESTS"]` with `includePlatforms: ["Editor"]`. This commit's research into Unity's sample asmdefs refines that lesson:

The trap in commit 031 was not that `defineConstraints: ["UNITY_INCLUDE_TESTS"]` is inherently broken — Unity's official sample 13 uses exactly that field and it works there. The trap was the specific *combination* of fields in our commit 029 asmdef: `autoReferenced: false` + `overrideReferences: true` + `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. Unity's sample 13 has `autoReferenced: true` and the constraint resolves correctly; our commit 029 had `autoReferenced: false` and the constraint failed to satisfy. The interaction between these three fields under the modern Unity 6 asmdef parser is what produced the silent-rejection behavior we observed.

This is preserved here as debugging context rather than promoted to a formal workflow rule. The pattern is too narrow to lock in as a discipline — `autoReferenced: false` plus `overrideReferences: true` plus `defineConstraints` is a specific combination unlikely to appear outside test asmdef configurations. If future Unity work hits a similar three-field-interaction trap, the diagnostic step is the same: compare against Unity's official sample asmdefs for the relevant test-mode and feature configuration. The samples ship at `Library/PackageCache/com.unity.test-framework@<hash>/Samples~/` and cover EditMode, PlayMode, scene-based, build-setup, domain-reload, test-cases, and custom-attribute scenarios.

The commit 030 + 031 + 032 sequence is the project's first sustained engagement with the Unity 6 asmdef system. Three Unity-semantic surprises arose:

1. **Commit 030**: `autoReferenced: true` controls inbound references (others linking to this asmdef), not outbound (this asmdef linking to others). `Unity.Mathematics` needed explicit `references` entry.
2. **Commit 031**: `defineConstraints: ["UNITY_INCLUDE_TESTS"]` combined with `overrideReferences: true` + `autoReferenced: false` silently filters the assembly out of Test Runner. Fix was to use `includePlatforms: ["Editor"]` instead for EditMode tests.
3. **Commit 032**: PlayMode tests cannot live in an Editor-only-platform asmdef; they require a separate asmdef with `includePlatforms: []`. Unity 6's official samples use the legacy `optionalUnityReferences: ["TestAssemblies"]` form for PlayMode, which remains the canonical minimal form.

Together these three commits give the project a working understanding of the asmdef system sufficient for future foundation work. No formal workflow rule lands yet (the trip-rate has been once per commit and each surprise has resolved cleanly with a single follow-up). The pattern to watch for in future commits: any new asmdef that uses non-default values for `autoReferenced`, `overrideReferences`, `defineConstraints`, or `includePlatforms` should be cross-checked against the closest matching Unity sample asmdef before assuming it works.

## Changes

Three discrete file operations:

### Operation 1: Create PlayMode asmdef

`SPACESIM/Assets/Scripts/Foundation/Coordinates/PlayModeTests/SpaceSim.Foundation.Coordinates.PlayModeTests.asmdef`. Form 1 (canonical Unity 6 PlayMode):

```json
{
    "name": "SpaceSim.Foundation.Coordinates.PlayModeTests",
    "rootNamespace": "SpaceSim.Foundation.Coordinates.Tests",
    "references": [
        "SpaceSim.Foundation.Coordinates",
        "Unity.Mathematics"
    ],
    "optionalUnityReferences": [
        "TestAssemblies"
    ],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

The `rootNamespace` matches the EditMode test file's namespace (`SpaceSim.Foundation.Coordinates.Tests`) so the two test files share the same Test Runner tree branch. `optionalUnityReferences: ["TestAssemblies"]` is Unity's canonical PlayMode marker that handles nunit + TestRunner linkage automatically. `includePlatforms: []` means "any platform" — required for PlayMode tests to run in the player during a test run. No `defineConstraints`; no explicit `precompiledReferences`; no `overrideReferences`. Minimal surface area.

### Operation 2: Create PlayMode test file

`SPACESIM/Assets/Scripts/Foundation/Coordinates/PlayModeTests/FloatingOriginManagerPlayModeTests.cs`. Same namespace as the EditMode test file. New class `FloatingOriginManagerPlayModeTests`. `[SetUp]` and `[TearDown]` mirror the EditMode pattern. Two `[UnityTest] IEnumerator` methods:

- `Singleton_FirstInstance_BecomesInstance`: one `yield return null` after SetUp so the manager's `Awake` fires, then `Assert.AreEqual(_manager, FloatingOriginManager.Instance)`.
- `Singleton_DuplicateInstance_LogsErrorAndDestroys`: one `yield return null` after SetUp (lets the original's `Awake` claim Instance), `LogAssert.Expect` for the duplicate-detection error log, create a second manager component, second `yield return null` (lets the duplicate's `Awake` detect the existing Instance and self-destruct), then `Assert.AreEqual(_manager, FloatingOriginManager.Instance)` confirming the original survived.

### Operation 3: Edit EditMode test file

`SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/FloatingOriginManagerTests.cs`. Three changes:

1. Class XML docstring updated. Old text said "PlayMode tests for FloatingOriginManager." (which had always been mis-labeled — the file was always EditMode but the docstring was wrong). New text describes the file as EditMode tests and explains why `_manager` is used over `Instance` (the EditMode-Awake-doesn't-fire issue) and points readers at the new PlayMode file for the singleton-lifecycle cases.

2. Two `[Test]` methods removed: `Singleton_FirstInstance_BecomesInstance` and `Singleton_DuplicateInstance_LogsErrorAndDestroys`. The total `[Test]` count drops from 19 to 17.

3. `// ----- Singleton -----` section-header comment replaced with `// ----- Singleton lifecycle tests moved to PlayMode -----` plus a pointer to the new PlayMode test file path.

## Verification

83 checks, 82 pass on first run, 1 verification-check semantic mismatch (substring-counting `[UnityTest]` mis-counted documentation mentions as attribute applications). Re-verified with line-anchored regex: 2 actual `[UnityTest]` attributes on the two test methods exactly as designed. Effective: 83/83 pass.

### A. New PlayMode files present (3 checks)

`PlayModeTests/` folder exists. PlayMode asmdef exists at the expected path. PlayMode test file exists at the expected path.

### B. PlayMode asmdef structure (9 checks)

Name: `SpaceSim.Foundation.Coordinates.PlayModeTests`. rootNamespace: `SpaceSim.Foundation.Coordinates.Tests`. References both `SpaceSim.Foundation.Coordinates` and `Unity.Mathematics`. `optionalUnityReferences` contains `TestAssemblies`. `includePlatforms: []`. `excludePlatforms: []`. Explicitly does NOT have `defineConstraints` (Form 1 minimal). Explicitly does NOT have explicit `nunit.framework.dll` precompiled reference (handled via `TestAssemblies`).

### C. PlayMode test file content (12 checks)

`using NUnit.Framework`, `using UnityEngine.TestTools` (for `[UnityTest]` attribute). Namespace `SpaceSim.Foundation.Coordinates.Tests`. Class `FloatingOriginManagerPlayModeTests`. `[SetUp]` and `[TearDown]` attributes present. `ClearInstanceForTesting()` called in SetUp. **Two actual `[UnityTest]` attributes** (verified via line-anchored regex; substring count is higher due to comment references). Both `IEnumerator` test method signatures present. At least three `yield return null` statements (one in first test, two in second). `LogAssert.Expect` for the duplicate-detection error log. Zero `[Test]` attribute applications (this file is PlayMode only; `[Test]` would put methods in the EditMode tab).

### D. EditMode test file updates (29 checks)

Two methods removed: `Singleton_FirstInstance_BecomesInstance` and `Singleton_DuplicateInstance_LogsErrorAndDestroys` (their full method bodies are gone). The bare `// ----- Singleton -----` header comment is gone, replaced with the pointer-to-PlayMode explanatory comment. The pointer comment names the new PlayMode test file path. The class docstring no longer says "PlayMode tests for FloatingOriginManager" (the always-mis-labeled phrase). The new docstring says "EditMode tests for FloatingOriginManager" and explains the `_manager`-over-Instance pattern. All 17 preserved tests verified by name: `InitialOrigin_IsZero`, `InitialShiftCount_IsZero`, `InitialThreshold_Is50Km`, `RegisterListener_AddsToList`, `RegisterListener_Duplicate_DoesNotDouble`, `RegisterListener_Null_IsIgnored`, `UnregisterListener_RemovesFromList`, `MaybeShiftOrigin_BelowThreshold_DoesNotShift`, `MaybeShiftOrigin_AboveThreshold_DoesShift`, `MaybeShiftOrigin_NotifiesListener`, `MaybeShiftOrigin_NotifiesEventSubscriber`, `MaybeShiftOrigin_BelowThreshold_DoesNotNotify`, `MaybeShiftOrigin_UpdatesOriginBeforeNotifying`, `MaybeShiftOrigin_SequentialShifts_AccumulateCount`, `MaybeShiftOrigin_ListenerThrows_OtherListenersStillNotified`, `WorldToLocal_UsesCurrentOrigin`, `LocalToWorld_UsesCurrentOrigin`. Total `[Test]` count = 17 (was 19; -2 lifecycle). `[SetUp]` and `[TearDown]` preserved.

### E. EditMode test asmdef unchanged (5 checks)

Name unchanged. `includePlatforms: ["Editor"]` unchanged. `defineConstraints: []` unchanged (post-commit-031 fix). References still include `Unity.Mathematics`. `nunit.framework.dll` precompiled still present.

### F. Runtime asmdef unchanged (3 checks)

Name unchanged. `references: ["Unity.Mathematics"]` unchanged. `autoReferenced: true` unchanged.

### G. Other source files unchanged (10 + 4 spot checks)

All 7 runtime source files (`WorldPosition.cs`, `LocalPosition.cs`, `CoordinateMath.cs`, `IFloatingOriginListener.cs`, `FloatingOriginManager.cs`, `FloatingOriginAnchor.cs`, `TestShiftDriver.cs`) exist. The 3 unchanged test files (`WorldPositionTests.cs`, `LocalPositionTests.cs`, `CoordinateMathTests.cs`) exist. Spot-checked `FloatingOriginManager.cs` for invariants: singleton Instance pattern preserved, 50.0 threshold default preserved, ClearInstanceForTesting preserved, try/catch around listener loop preserved.

### H. CONSTRAINTS.md unchanged + prior-commit anchors (8 checks)

CONSTRAINTS.md line count = 1960 (unchanged). `**Prototype scaffolding: verified.**` paragraph present exactly once. Sampled anchors from commits 014, 015, 025, 026, 027, 028 all preserved.

## User-side verification

Cowork's file work is complete. The user-side verification path:

1. Open Unity Hub. Open the SPACESIM project.
2. Wait for Unity to recompile both test assemblies. Console should be free of errors.
3. Open Window → General → Test Runner.
4. **EditMode tab** should show `SpaceSim.Foundation.Coordinates.Tests.dll (52 tests)` containing 4 test classes:
   - CoordinateMathTests (14 tests)
   - FloatingOriginManagerTests (17 tests) ← down from 19
   - LocalPositionTests (8 tests)
   - WorldPositionTests (13 tests)
5. **PlayMode tab** should show `SpaceSim.Foundation.Coordinates.PlayModeTests.dll (2 tests)` containing 1 test class:
   - FloatingOriginManagerPlayModeTests (2 tests)
6. Right-click the SPACESIM root in EditMode and choose Run. All 52 should pass.
7. Switch to the PlayMode tab. Right-click SPACESIM and choose Run. Unity enters Play mode briefly (this is normal for PlayMode tests). Both tests should pass.
8. Total result: 54 of 54 tests passing.

If the PlayMode tab doesn't show the new tests, the most likely cause is that Unity's package cache hasn't picked up the new asmdef. Try Assets → Reimport All (the heavy hammer; takes minutes). If that doesn't work, the PlayMode asmdef may need to use Form 2 instead of Form 1 — surface the failure and we land a follow-up commit.

After verification passes, version-control the state:

```
cd C:\Users\gmkar\space_sim
git add .
git commit -m "commit 032: PlayMode lifecycle tests"
git push
```

## Numbering trail

The sim-tick controller moves to commit 033 next session. Number trail: 027 init → 028 verify → 029 coordinates → 030 mathematics reference → 031 test platform fix → 032 PlayMode lifecycle tests fix → **033 sim-tick controller (next)**. The three asmdef commits (030, 031, 032) cluster as the Unity Test Framework learning curve; the project now has a stable asmdef-system understanding to build on.

## Replay

```
cd C:\Users\gmkar\space_sim
git add SPACESIM/Assets/Scripts/Foundation/Coordinates/PlayModeTests/SpaceSim.Foundation.Coordinates.PlayModeTests.asmdef ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/PlayModeTests/FloatingOriginManagerPlayModeTests.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/FloatingOriginManagerTests.cs ^
        commits/032_playmode_lifecycle_tests.md
git commit -F commits/032_playmode_lifecycle_tests.md
```

The user will also need to `git add` the `.meta` files Unity auto-generates for the new asmdef and test file on first open. Those land in a future commit per the same pattern as commit 028.
