# 031: Test asmdef Editor-only platform fix

Single-file asmdef edit correcting Test Runner's failure to discover the coordinate-system tests. Changes the test asmdef from `defineConstraints: ["UNITY_INCLUDE_TESTS"]` to `includePlatforms: ["Editor"]`. The change is on disk and verified working in Unity through a Cowork computer-use diagnostic session: Test Runner now discovers all 54 tests in `SpaceSim.Foundation.Coordinates.Tests.dll`, of which 52 pass and 2 fail. The 2 remaining failures are a separate test-design issue (commit 029 tests trying to exercise the singleton-Awake lifecycle from EditMode tests where Awake doesn't fire on `AddComponent`); they are deferred to commit 032.

## Scope

- `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/SpaceSim.Foundation.Coordinates.Tests.asmdef` — modified. `includePlatforms: []` → `includePlatforms: ["Editor"]`; `defineConstraints: ["UNITY_INCLUDE_TESTS"]` → `defineConstraints: []`. All other fields preserved verbatim.

No C# source changes. No runtime asmdef changes. No scene changes. No CONSTRAINTS update.

## Rationale

After commit 030 fixed the `Unity.Mathematics` reference, the test assembly compiled cleanly (zero errors, zero warnings in Console) but Test Runner showed the message:

> No tests to show.
> Test scripts can be added to assemblies referencing the "nunit.framework.dll" library or folders with Assembly Definition References targeting "UnityEngine.TestRunner" or "UnityEditor.TestRunner".

The `SpaceSim.Foundation.Coordinates.Tests.dll` was not listed in Test Runner's tree at all — neither populated nor empty. The Inspector view of the asmdef rendered without a "Define Constraints" subsection, indicating that the importer either dropped the `defineConstraints` field during parse or rejected the unmet constraint silently. With `UNITY_INCLUDE_TESTS` unsatisfied, Test Runner's discovery scan excluded the assembly.

The fix replaces the `UNITY_INCLUDE_TESTS` define-constraint mechanism with the more reliable `includePlatforms: ["Editor"]` mechanism. Editor-only inclusion at the platform level is the modern Unity 6 idiom for test asmdefs:

- It explicitly marks the assembly as Editor-only, which prevents it from shipping in runtime builds (the same protection `UNITY_INCLUDE_TESTS` was providing).
- It triggers Test Runner's discovery scan correctly: Test Runner looks for Editor-platform-only assemblies that reference `nunit.framework.dll`.
- The Inspector renders the "Include Platforms" subsection with Editor checked, providing a visible UI for the configuration.

After the change, Test Runner discovered `SPACESIM (54 tests) → SpaceSim.Foundation.Coordinates.Tests.dll (54)` and Run All produced 52 passed / 2 failed within 0.051 seconds.

## Diagnostic session note

This diagnosis was performed via Cowork's computer-use MCP rather than the typical Cowork-file-edits-then-user-verifies loop. The computer-use session opened Unity, navigated to Window → General → Test Runner, observed the empty-Test-Runner state, navigated to the asmdef Inspector to confirm the configuration parse, applied the fix via bash-via-Python edit to the underlying JSON, triggered Unity's asset refresh via Ctrl+R, re-opened Test Runner, observed 54 tests discovered, ran the tests, and observed 52 pass / 2 fail.

The underlying file change is identical to what the standard Cowork workflow would produce, and the verification result (52/54 passing) is equivalent to what user-side verification would have reported. The GUI-mode diagnostic loop was used because the failure mode required Unity Inspector inspection to discriminate between the candidate causes (CS error, Console warning, compile success but discovery failure, missing reference, etc.), which is faster end-to-end via computer-use than by round-tripping each hypothesis to the user.

This is the first commit in the project to use computer-use for diagnosis. The substantive deliverable (the asmdef edit + the verification result) is no different from what a user-side workflow would produce; only the diagnostic loop was accelerated.

## Changes

Single JSON edit applied via bash-via-Python with atomic write:

Before:
```json
{
    "includePlatforms": [],
    ...
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    ...
}
```

After:
```json
{
    "includePlatforms": [
        "Editor"
    ],
    ...
    "defineConstraints": [],
    ...
}
```

All other fields preserved verbatim: `name`, `rootNamespace`, `references` (the four entries from commit 030: `SpaceSim.Foundation.Coordinates`, `Unity.Mathematics`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`), `excludePlatforms`, `allowUnsafeCode`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false`, `versionDefines`, `noEngineReferences: false`.

## Verification

56 checks, all passing on first run. Five groups:

### A. Substantive asmdef change confirmed (3 checks)

- Test asmdef `includePlatforms == ["Editor"]`
- Test asmdef `excludePlatforms == []`
- Test asmdef `defineConstraints == []` (UNITY_INCLUDE_TESTS removed)

### B. Test asmdef regression preservation (11 checks)

- Name unchanged: `SpaceSim.Foundation.Coordinates.Tests`
- rootNamespace unchanged
- All four `references` entries preserved: `SpaceSim.Foundation.Coordinates`, `Unity.Mathematics`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`
- `overrideReferences: true` preserved
- `autoReferenced: false` preserved
- `nunit.framework.dll` precompiled reference preserved
- `noEngineReferences: false` preserved
- `allowUnsafeCode: false` preserved
- `versionDefines: []` preserved

### C. Runtime asmdef unchanged from commit 030 (4 checks)

- Name still `SpaceSim.Foundation.Coordinates`
- `references == ["Unity.Mathematics"]` (commit 030's change preserved)
- `includePlatforms == []` (any platform; runtime module is not Editor-only)
- `autoReferenced: true` preserved

### D. C# source files and scene unchanged (24 checks)

All 11 C# source files (7 runtime + 4 test) exist with correct namespace declarations. Specific commit-029 invariants spot-checked: FloatingOriginManager singleton pattern preserved, 50.0 km threshold default preserved, ClearInstanceForTesting preserved, FloatingOriginAnchor PROTOTYPE-QUALITY CAVEAT comment preserved. TestCoordinates.unity scene starts with `%YAML 1.1` and contains the four scene singletons.

### E. CONSTRAINTS.md unchanged + prior-commit anchors (8 checks)

CONSTRAINTS.md line count = 1960 (unchanged from commit 028 forward). `**Prototype scaffolding: verified.**` paragraph present exactly once. Anchors from commits 014, 015, 025, 026, 027, 028 all preserved.

## Test Runner output post-fix (verified via computer-use)

```
SPACESIM (54 tests)  2 tests failed (0.051s)
└─ SpaceSim.Foundation.Coordinates.Tests.dll (54)
   └─ SpaceSim (54 tests)
      └─ Foundation (54 tests)
         └─ Coordinates (54 tests)
            └─ Tests (54 tests)
               ├─ ✓ CoordinateMathTests (14 tests)
               ├─ ✗ FloatingOriginManagerTests (19 tests) — 2 failed
               ├─ ✓ LocalPositionTests (8 tests)
               └─ ✓ WorldPositionTests (13 tests)
```

Top-right Test Runner counters: ✓52 ✗2 ⚪0.

## Remaining failures (deferred to commit 032)

Two tests in `FloatingOriginManagerTests` fail with identical error pattern:

```
Singleton_FirstInstance_BecomesInstance (0.001s)
Expected: <TestFloatingOriginManager (SpaceSim.Foundation.Coordinates.FloatingOriginManager)>
But was: null

Singleton_DuplicateInstance_LogsErrorAndDestroys (0.006s)
Expected: <TestFloatingOriginManager (SpaceSim.Foundation.Coordinates.FloatingOriginManager)>
But was: null
```

Both tests read `FloatingOriginManager.Instance` and expect it to equal the manager instance created in `[SetUp]`. The expectation depends on `Awake` having run on the manager component to execute `Instance = this`. In EditMode tests, `AddComponent<>()` does NOT invoke `Awake` on the new component — Unity's component lifecycle hooks (`Awake`, `Start`, `OnEnable`) require the play loop. So `Instance` remains null, the assertion fails.

This is a test-design issue in commit 029's test code, not a problem with the production `FloatingOriginManager`. The 17 other manager tests pass because they use the local `_manager` reference rather than reading `Instance` directly; only the 2 singleton-lifecycle-specific tests are affected.

Path forward for commit 032: move the 2 affected tests into a new `FloatingOriginManagerPlayModeTests.cs` file using `[UnityTest] IEnumerator` test methods with `yield return null` to allow Unity's lifecycle to run. This is the canonical Unity test pattern for component-lifecycle tests. Production code (`FloatingOriginManager.cs`) stays clean — no `Initialize()` helper, no test-API mutation paths. The PlayMode asmdef can either be a new asmdef (`SpaceSim.Foundation.Coordinates.PlayModeTests.asmdef`) or the existing test asmdef can have `Tests/PlayMode/` as a subfolder with a separate asmdef.

The two singleton tests are the only ones in the project that need PlayMode; commit 032 is small.

## Numbering

Commit 031 is the asmdef Editor-only platform fix (this commit).

Commit 032 will be the PlayMode lifecycle tests fix (move 2 singleton tests to PlayMode, get to 54/54 pass).

Commit 033 will be the sim-tick controller per `docs/NETCODE_CONTRACT.md` §1.2, replacing the earlier expectation that the sim-tick controller would be commit 032. Number trail: 027 init → 028 verify → 029 coordinates → 030 mathematics reference fix → 031 test platform fix → 032 PlayMode lifecycle tests fix → 033 sim-tick controller.

## Replay

```
cd C:\Users\gmkar\space_sim
git add SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/SpaceSim.Foundation.Coordinates.Tests.asmdef ^
        commits/031_test_asmdef_editor_platform.md
git commit -F commits/031_test_asmdef_editor_platform.md
```
