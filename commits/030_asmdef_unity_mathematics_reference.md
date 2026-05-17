# 030: Fix asmdef references to include Unity.Mathematics

Two-file asmdef fix-up correcting commit 029's misunderstanding of the `autoReferenced` semantic. Adds `"Unity.Mathematics"` to the `references` array in both `SpaceSim.Foundation.Coordinates.asmdef` (runtime module) and `SpaceSim.Foundation.Coordinates.Tests.asmdef` (test module). No C# source changes. No CONSTRAINTS update. No scene changes. Minimal scope: two JSON edits that resolve Unity's CS0234 / CS0246 compile errors for `Unity.Mathematics` namespace and `double3` type across all seven coordinate-system source files.

The substantive coordinate-system code from commit 029 — `WorldPosition`, `LocalPosition`, `CoordinateMath`, `FloatingOriginManager`, `FloatingOriginAnchor`, `IFloatingOriginListener`, `TestShiftDriver`, plus four test files — is unchanged. Only the asmdef references are corrected. Unity's compile errors are downstream of the asmdef misconfiguration; once Mathematics is properly referenced, the runtime assembly compiles, the test assembly compiles, and the Burst entry-point scan completes against the now-compiled assemblies. (The Burst error in the first compile attempt was downstream of the assembly compile failure; once compilation succeeds, Burst's error resolves automatically. If Burst still errors after this fix, that's a separate issue diagnosed at that point — do not preemptively add Burst configuration.)

## Scope

- `SPACESIM/Assets/Scripts/Foundation/Coordinates/SpaceSim.Foundation.Coordinates.asmdef` — modified. Added `"Unity.Mathematics"` to `references` array (was empty pre-fix; one element post-fix). All other fields preserved exactly.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/SpaceSim.Foundation.Coordinates.Tests.asmdef` — modified. Added `"Unity.Mathematics"` to `references` array (now four elements: `SpaceSim.Foundation.Coordinates`, `Unity.Mathematics`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`). All other fields preserved exactly: `overrideReferences: true`, `autoReferenced: false`, `UNITY_INCLUDE_TESTS` defineConstraint, `nunit.framework.dll` precompiled reference.

No other files in the project are touched. The seven C# source files from commit 029 are unchanged. The four test files from commit 029 are unchanged. `TestCoordinates.unity` is unchanged. CONSTRAINTS.md is unchanged (Phase 0 §9 was closed at commit 028; implementation fix-ups do not append to §9).

## Rationale

Commit 029 wrote both asmdef files with empty `references` arrays and `autoReferenced: true`. The bet was that `autoReferenced: true` would cause Unity to automatically link the asmdef to first-party packages like `Unity.Mathematics`. That bet was wrong.

The Unity asmdef semantics are:

- **`autoReferenced: true`** means *other asmdefs* automatically reference this asmdef when their own `autoReferenced` is enabled and they don't have `overrideReferences: true`. It is an **inbound** setting controlling who can implicitly link to this module.
- **`references: [...]`** is the **outbound** setting controlling which other asmdefs this module links to. Without an explicit entry, the linked assembly is not available, and the C# compiler emits CS0234 ("namespace not found") and CS0246 ("type not found") errors.
- **`noEngineReferences: false`** provides `UnityEngine.dll` and `UnityEditor.dll` automatically (the "engine references"). It does NOT provide the wider package ecosystem — `Unity.Mathematics`, `Unity.Burst`, etc., each require an explicit `references` entry.

The commit 029 runtime asmdef had `references: []` plus `autoReferenced: true` plus `noEngineReferences: false`. This made the assembly available to consumers (`autoReferenced: true`) and gave it `UnityEngine` access (`noEngineReferences: false`), but did NOT give it `Unity.Mathematics`. All six runtime source files that use `double3` failed to compile.

The commit 029 test asmdef had `references: ["SpaceSim.Foundation.Coordinates", "UnityEngine.TestRunner", "UnityEditor.TestRunner"]` plus `overrideReferences: true`. `overrideReferences: true` means the asmdef ignores `autoReferenced` and uses *only* the explicit `references` list (plus `precompiledReferences`). The test asmdef therefore had no path to `Unity.Mathematics` at all — not even via transitive reference from the runtime module, because Unity asmdef references are not transitive.

The fix is two minimal JSON edits: add `"Unity.Mathematics"` to the runtime asmdef's `references` array, and add it to the test asmdef's `references` array. Both edits use the **name-reference form** (`"Unity.Mathematics"`) rather than the **GUID-reference form** (`"GUID:d8b63aba1907145bea998dd612889d6b"`). Name form is more readable, supported in Unity 6's modern asmdef format, and stable for first-party Unity packages (Unity has not renamed `Unity.Mathematics` and is committed to API stability for first-party math types). GUID form is more robust against package renames but the readability win matters more here.

The decision to keep the C# code unchanged is deliberate. The compile errors were not bugs in the C# code; they were the C# code being correctly written against types the build system had been (mis)configured to deny it. Once the asmdef gives the compiler access to `Unity.Mathematics`, every existing `using Unity.Mathematics;` directive and every `double3` reference resolves. No source edits required.

This is the third "Unity semantic that bit me" pattern in the prototype work, after commit 023's substring-find-against-wrong-file ordering check and commit 028's sandbox-mount staleness. Two data points are below the threshold for formalizing a new workflow rule in `commits/README.md`. If commit 031 (sim-tick controller) needs to add `Unity.Burst` to its asmdef references and hits a similar trap, that's the third instance and the candidate rule formalizes: "Unity asmdef references are not transitive. Every asmdef must explicitly list every assembly it uses, including Unity first-party packages. `autoReferenced` controls inbound references; `references` controls outbound. `noEngineReferences` provides only the engine assemblies, not the wider package ecosystem."

## Changes

Two atomic file edits, each via the standard Python json-load-modify-dump-write-then-rename pattern:

### Edit 1: Runtime asmdef

Before:
```json
"references": [],
```

After:
```json
"references": [
    "Unity.Mathematics"
],
```

All other fields preserved verbatim. Sanity-checked via JSON round-trip: parse pre-edit, assert `Unity.Mathematics` not present (avoid no-op), modify `references` field, serialize with 4-space indent, atomic write via `.recovery` + `os.replace`, re-parse to confirm validity and presence of the new reference.

### Edit 2: Test asmdef

Before:
```json
"references": [
    "SpaceSim.Foundation.Coordinates",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
],
```

After:
```json
"references": [
    "SpaceSim.Foundation.Coordinates",
    "Unity.Mathematics",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
],
```

`Unity.Mathematics` inserted after `SpaceSim.Foundation.Coordinates` (the local-module reference) and before the TestRunner references. Position groups the references by purpose: own module first, framework dependencies next (Unity.Mathematics), test infrastructure last (TestRunner pair). Same atomic-write pattern.

The two asmdef files retain all their other configuration unchanged:

- Runtime: `autoReferenced: true`, `overrideReferences: false`, `noEngineReferences: false`, empty `precompiledReferences`, `allowUnsafeCode: false`, both platform lists empty.
- Test: `autoReferenced: false`, `overrideReferences: true`, `noEngineReferences: false`, `precompiledReferences: ["nunit.framework.dll"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, `allowUnsafeCode: false`, both platform lists empty.

## Verification

133 checks, all passing on first run. Includes 16 NEW asmdef-content checks specific to this commit's fix, plus the full commit-029 verification battery (file existence, C# file shape, asmdef JSON validity, scene shape, content-specific code sanity, test file content, .gitkeep state, CONSTRAINTS unchanged, prior-commit anchors).

### A. NEW asmdef-content checks (16)

The substantive validation for this commit's correctness:

- Runtime asmdef `references` contains `Unity.Mathematics`
- Test asmdef `references` contains `Unity.Mathematics`
- Test asmdef `references` still contains `SpaceSim.Foundation.Coordinates` (regression check)
- Test asmdef `references` still contains `UnityEngine.TestRunner` (regression check)
- Test asmdef `references` still contains `UnityEditor.TestRunner` (regression check)
- Runtime asmdef parses as valid JSON post-edit
- Test asmdef parses as valid JSON post-edit
- Runtime asmdef `name` unchanged at `SpaceSim.Foundation.Coordinates`
- Test asmdef `name` unchanged at `SpaceSim.Foundation.Coordinates.Tests`
- Test asmdef `overrideReferences: true` preserved
- Test asmdef `autoReferenced: false` preserved
- Test asmdef `UNITY_INCLUDE_TESTS` defineConstraint preserved
- Test asmdef `nunit.framework.dll` precompiled reference preserved
- Runtime asmdef `autoReferenced: true` preserved
- Runtime asmdef `overrideReferences: false` preserved
- Runtime asmdef `noEngineReferences: false` preserved

### B. Commit-029 carry-forward checks (117)

- File existence (14): all 7 C# source files, both asmdef files, all 4 test files, and the test scene present
- C# file shape (28): each file has correct namespace, balanced braces, NUnit using directive in test files, size in plausible range
- asmdef JSON validity (8): both asmdef files parse, have name and references fields
- TestCoordinates.unity scene shape (5): YAML 1.1 header, four scene singletons, exactly 4 doc markers, zero GameObject declarations
- Source-file content sanity (38): WorldPosition readonly struct with double3 + no implicit/explicit casts + arithmetic restrictions; LocalPosition readonly struct with Vector3 + arithmetic restrictions; CoordinateMath signatures + strict-greater-than threshold; FloatingOriginManager singleton + SerializeField threshold + MaybeShiftOrigin + OriginShifted event + origin-before-notify ordering + ClearInstanceForTesting + try/catch; FloatingOriginAnchor IFloatingOriginListener + PROTOTYPE-QUALITY CAVEAT comment + rigidbody/transform paths; IFloatingOriginListener interface signature; TestShiftDriver default speed and TEST-HARNESS labeling
- Test file content (10): SetUp/TearDown; origin-before-notify test; throwing-listener resilience; duplicate-singleton handling; below/above threshold tests; round-trip stability; ExactlyAtThreshold_IsFalse; interstellar and planetary precision
- .gitkeep state (1): still 0-byte; replay via host-side git rm
- CONSTRAINTS.md unchanged (2): line count = 1960; `**Prototype scaffolding: verified.**` paragraph still present exactly once
- Prior-commit anchors (9): commits 014, 015, 017, 019, 025, 026, 027, 028 anchors preserved

## User-side verification

The commit-029 user-side verification path still applies, with the asmdef fix removing the previously-blocking compile errors:

1. Open Unity Hub. Open the SPACESIM project.
2. Unity recompiles the assemblies. Console should report no CS0234 / CS0246 errors. The Burst error from the first compile attempt should also clear (it was downstream of the assembly compile failures; with `Unity.Mathematics` properly referenced, both assemblies compile and Burst's entry-point scan completes).
3. Run the EditMode test runner. Window → General → Test Runner → EditMode. Four test classes from `SpaceSim.Foundation.Coordinates.Tests` should be discovered. Run all tests; confirm all pass.
4. Open `TestCoordinates.unity`. Populate per the commit 029 instructions (FloatingOriginRoot with FloatingOriginManager; TestSubject sphere with FloatingOriginAnchor + Rigidbody + TestShiftDriver; WorldOriginMarker cube with FloatingOriginAnchor). Press Play and observe shift behavior.
5. If Burst still errors after the asmdef fix, that's a separate issue to diagnose then.

## Replay

```
cd C:\Users\gmkar\space_sim
git add SPACESIM/Assets/Scripts/Foundation/Coordinates/SpaceSim.Foundation.Coordinates.asmdef ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/SpaceSim.Foundation.Coordinates.Tests.asmdef ^
        commits/030_asmdef_unity_mathematics_reference.md
git commit -F commits/030_asmdef_unity_mathematics_reference.md
```

Single atomic git commit. Sim-tick controller work moves to commit 031.
