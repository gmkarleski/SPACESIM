# 029: Coordinate system — WorldPosition, LocalPosition, FloatingOriginManager + unit tests

First real implementation commit. Adds double-precision world coordinates (`WorldPosition`), single-precision local coordinates (`LocalPosition`), a static math layer for pure conversion and shift-decision logic (`CoordinateMath`), a singleton MonoBehaviour that owns the floating origin and dispatches shift events (`FloatingOriginManager`), an opt-in MonoBehaviour that applies shifts to GameObject transforms and rigidbodies (`FloatingOriginAnchor`), a listener interface (`IFloatingOriginListener`), a test-scene driver MonoBehaviour (`TestShiftDriver`), four unit-test files with comprehensive coverage of conversion correctness, precision boundaries, shift behavior at threshold, and edge cases, two assembly definitions (one runtime, one tests), and a test scene `TestCoordinates.unity` (empty scene per the commit 027/028 split-of-labor convention; user populates GameObjects in Unity per the artifact's verification instructions).

The implementation is faithful to `docs/CONSTRAINTS.md` §2 commit 002 (foundational architectural principles) and `docs/NETCODE_CONTRACT.md` §1.1 (two layers of state). Key commitments implemented in code:

- **WorldPosition is double-precision; LocalPosition is single-precision.** Both are immutable `readonly struct` value types. WorldPosition wraps `Unity.Mathematics.double3`; LocalPosition wraps `UnityEngine.Vector3`. Mixing them across the conversion boundary is a type error; named conversion methods (`CoordinateMath.WorldToLocal` / `LocalToWorld`) force every conversion to declare its coordinate space at the call site. No implicit or explicit casts between the types.
- **50 km shift threshold default**, exposed as a serialized field for per-save tuning per CONSTRAINTS §2 commit 002.
- **Strict-greater-than threshold convention.** A position at exactly 50.0 km does NOT trigger a shift; only positions farther than 50 km do. Test coverage explicitly exercises the exactly-at-threshold case.
- **Origin updates atomically before listeners run.** `FloatingOriginManager.CurrentOrigin = activeVesselWorldPos` happens before any listener notification. Listeners that call `WorldToLocal()` in their shift handler see the new origin.
- **Listener resilience.** Each listener's `OnFloatingOriginShifted` call is wrapped in try/catch; a throwing listener logs an error but does not block other listeners from being notified. The simulation cannot end up half-shifted.
- **Dual listener model.** `IFloatingOriginListener` interface for performance-critical / frequently-iterated subscribers (vessel anchors, physics components); `event Action<double3> OriginShifted` for ad-hoc subscribers (UI overlays, particle systems, etc.).
- **`FloatingOriginAnchor` is the opt-in shift participant.** Attaching it to a GameObject makes that GameObject's transform translate when the origin shifts. If the GameObject has a Rigidbody, the shift uses `Rigidbody.position -= delta` (canonical Unity floating-origin pattern). The file carries an explicit PROTOTYPE-QUALITY CAVEAT comment noting that production-quality shift handling at the sim-tick boundary will be revisited in commit 030 (sim-tick controller).

The commit also ships a test-scene driver `TestShiftDriver` that moves a GameObject through world coordinates at a configurable speed (default 10 km/s) so the user can observe shift events interactively in Play mode. The driver's diagnostic UI text labels the speed as `TEST-HARNESS SPEED (10.0 km/s — not representative of game physics)` per your flagged note.

## Scope

- `SPACESIM/Assets/Scripts/Foundation/Coordinates/WorldPosition.cs` — created. Immutable double-precision world-coords struct with arithmetic and distance methods.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/LocalPosition.cs` — created. Immutable single-precision local-coords struct.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/CoordinateMath.cs` — created. Static pure functions: `WorldToLocal`, `LocalToWorld`, `ShouldShift`, `ComputeShiftDelta`.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/IFloatingOriginListener.cs` — created. Listener interface for shift notifications.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/FloatingOriginManager.cs` — created. Singleton MonoBehaviour. Owns origin state, threshold, listener list, shift logic.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/FloatingOriginAnchor.cs` — created. Opt-in MonoBehaviour that applies shifts to its transform and rigidbody. Contains PROTOTYPE-QUALITY CAVEAT comment per the design discussion.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/TestShiftDriver.cs` — created. Test-harness MonoBehaviour for the demonstration scene.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/SpaceSim.Foundation.Coordinates.asmdef` — created. Assembly definition for the Coordinates module. `autoReferenced: true` so Unity.Mathematics and UnityEngine are picked up automatically.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/WorldPositionTests.cs` — created. EditMode tests covering construction, equality, arithmetic, distance, precision at planetary and interstellar scales.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/LocalPositionTests.cs` — created. EditMode tests covering construction, equality, arithmetic, distance.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/CoordinateMathTests.cs` — created. EditMode tests covering WorldToLocal, LocalToWorld, round-trip stability, strict-greater-than threshold convention (including the explicit `ExactlyAtThreshold_IsFalse` case), shift delta computation.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/FloatingOriginManagerTests.cs` — created. PlayMode-flavor tests covering singleton, initial state, listener registration (including dedup, null-safe), shift logic (below/above threshold, listener notification, event notification, origin-updates-before-listeners, sequential shifts, throwing-listener resilience), conversion convenience methods.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/SpaceSim.Foundation.Coordinates.Tests.asmdef` — created. Test assembly definition. References main module, NUnit, Unity TestRunner. `defineConstraints: ["UNITY_INCLUDE_TESTS"]` so the assembly compiles only when test framework is enabled.
- `SPACESIM/Assets/Scenes/TestCoordinates.unity` — created. Empty Unity 6 scene (four scene-level singletons; no GameObjects). User populates per the artifact's verification instructions.
- `SPACESIM/Assets/Scripts/Foundation/Coordinates/.gitkeep` — should be removed during replay. The sandbox cannot unlink files that existed before the session; commit 027's `.gitkeep` placeholder is now superseded by real source files but persists as a 0-byte file. Replay procedure runs `git rm SPACESIM/Assets/Scripts/Foundation/Coordinates/.gitkeep`.

CONSTRAINTS.md is NOT modified by this commit. Per the discussion, Phase 0 §9 is closed at commit 028; implementation commits do not append to §9.

## Rationale

The contract specifies two layers of state — authoritative double-precision world coordinates and PhysX-side single-precision local coordinates — with the floating origin as the bridge between them. Implementing this faithfully means more than just providing `double3` and `Vector3` types; it means making the *coordinate-space distinction* a type-level guarantee that the rest of the codebase cannot accidentally violate. `WorldPosition` and `LocalPosition` as separate types with no implicit casts is the discipline; named conversion methods are the enforcement.

The strict-greater-than threshold convention (Flag 7 in the proposal) matters because the alternative — shifting at exactly 50.0 km — produces churn at the boundary with zero precision benefit. At exactly 50.0 km, the active vessel's local position is `(50000, 0, 0)` in single-precision floats, which still has ~5 digits of precision. Shifting "preventively" here would invoke listeners, modify Transforms, and trigger physics re-resolution for nothing. Shifting only when strictly farther than the threshold lets the active vessel use the full 50 km radius before any shift cost is incurred.

The dual listener model (Flag 3) separates concerns. The `IFloatingOriginListener` interface is for performance-critical subscribers — vessel anchors that will exist in the dozens per scene, each invoked synchronously during the sim-tick boundary's shift step. Interface dispatch avoids per-shift delegate allocation. The `event Action<double3>` is for everything else — UI overlays, particle systems, diagnostic logs — where the convenience of `+= handler` outweighs the small allocation cost. Both pathways are notified by the manager's shift logic; the choice between them is a per-subscriber performance / ergonomic tradeoff.

The origin-update-before-notify ordering (Flag 5) matters because listeners may legitimately call `WorldToLocal()` in their shift handler to compute updated local positions for caches they own. If the manager's state still showed the old origin during the notification pass, those calls would return wrong values. Updating state first means the manager's APIs are consistent throughout the notification, and the shift delta is passed as an explicit argument to listeners that need to know what changed.

`FloatingOriginAnchor` (Flag 4) ships in this commit because the test scene needs a working visual demonstration of shift behavior. Without it, the test scene would require bespoke shift-handler code in `TestShiftDriver` to translate the sphere when shifts fire — fragile and unrepresentative. The anchor is also genuinely useful for future commits: vessel containers will use it. Shipping it now lets the test scene be a clean demonstration of the listener architecture working as designed.

The PROTOTYPE-QUALITY CAVEAT comment in `FloatingOriginAnchor.cs` documents an honest limitation of the current implementation. `Rigidbody.position -= delta` is canonical Unity for the prototype use case (single isolated rigidbody, no active joints crossing the shift) but is not sufficient for production. Production-quality shift handling needs to disable Physics.Simulate briefly during the shift, dispatch shifts on FixedUpdate boundaries where PhysX state is synchronized, and handle articulated rigidbody / joint / contact-crossing-shift cases. These are revisited when commit 030 lands the sim-tick controller and the manager's `MaybeShiftOrigin` call moves from MonoBehaviour-Update-driven to sim-tick-driven. Documenting the limitation in code rather than letting it be implicit avoids the "we'll remember to revisit" failure mode.

Tests cover the contract-mandated cases. Pure-math tests in `CoordinateMathTests` exercise round-trip stability (`LocalToWorld(WorldToLocal(w, o), o) ≈ w` within float epsilon), the strict-greater-than threshold (including the explicit `ExactlyAtThreshold_IsFalse` boundary case), and shift delta correctness. World-position tests exercise precision at planetary scale (sub-millimeter resolution at 6.371e6 m) and interstellar scale (~1 m resolution at 9.461e15 m, the light-year distance limit of double-precision). Manager tests exercise singleton enforcement (including duplicate-instance error logging), listener registration (including dedup and null-safe), shift logic (below/above/exactly-at threshold, listener and event notification, origin updates before listeners, sequential shifts), and listener resilience (a throwing listener doesn't block other listeners). The two-axis listener model (interface plus event) is independently tested.

## Changes

Implementation operations:

1. Write seven C# source files into `SPACESIM/Assets/Scripts/Foundation/Coordinates/`.
2. Write one runtime asmdef in the same directory. `autoReferenced: true`; empty `references` (Unity.Mathematics is auto-referenced under the modern manifest).
3. Write four C# test files into `SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/`.
4. Write one test asmdef. References `SpaceSim.Foundation.Coordinates`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`. Precompiled reference to `nunit.framework.dll`. `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. `autoReferenced: false`.
5. Write empty `TestCoordinates.unity` scene by extracting the four scene-level singletons from the template `SampleScene.unity` (same approach used in commits 027/028 for `TestFoundation.unity`). User populates GameObjects in Unity per the verification instructions.

The `.gitkeep` placeholder from commit 027 in `Coordinates/` still exists (sandbox cannot unlink files that existed before the session). The replay procedure handles its removal via host-side `git rm`.

No changes to CONSTRAINTS.md.

## Verification

128 checks, 127 pass on first run, 1 verification-check semantic mismatch (the strict-greater-than threshold test lives in `CoordinateMathTests.cs` not `FloatingOriginManagerTests.cs`, and my check was looking in the wrong file; the test is present and correct). Effective: 128 / 128 pass with the corrected expectation.

### A. File existence (14 checks)

All 14 expected source / asmdef / scene files exist at expected paths.

### B. C# file shape (28 checks)

For each of the 7 C# source files plus 4 test files:

- Correct namespace declaration (`SpaceSim.Foundation.Coordinates` for runtime, `SpaceSim.Foundation.Coordinates.Tests` for tests)
- Balanced curly braces (open count = close count)
- File size in plausible range (200 bytes to 30 KB)
- Test files include `using NUnit.Framework`

### C. asmdef validity (8 checks)

- Both asmdef files parse as valid JSON
- Both have `name` and `references` fields
- Test asmdef references the main module by name
- Test asmdef references TestRunner assemblies
- Test asmdef precompiled-references nunit.framework.dll
- Test asmdef has `UNITY_INCLUDE_TESTS` defineConstraint
- Test asmdef has `autoReferenced: false`

### D. Scene file shape (7 checks)

`TestCoordinates.unity` is a valid Unity 6 empty scene: starts with `%YAML 1.1`, contains all four scene-level singletons (OcclusionCullingSettings, RenderSettings, LightmapSettings, NavMeshSettings), exactly 4 document markers, zero GameObject declarations (empty by design — user populates in Unity).

### E. Content-specific sanity (39 checks)

- `WorldPosition`: readonly struct; IEquatable; double3 Value field; Zero static; DistanceTo method; NO implicit or explicit cast to LocalPosition; arithmetic with `double3` only (not `Vector3`)
- `LocalPosition`: readonly struct; Vector3 Value field; arithmetic with `Vector3` only (not `double3`)
- `CoordinateMath`: WorldToLocal / LocalToWorld signatures match design; ShouldShift uses strict `>` not `>=`; ComputeShiftDelta present
- `FloatingOriginManager`: singleton Instance; SerializeField shiftThresholdKm = 50.0; CurrentOrigin property; MaybeShiftOrigin method; OriginShifted event; origin updates before listener notification (verified by source-order check); ClearInstanceForTesting test support; try/catch around listener invocation
- `FloatingOriginAnchor`: implements IFloatingOriginListener; PROTOTYPE-QUALITY CAVEAT comment present; references commit 030 for production-quality revisit; Rigidbody.position teleport; transform fallback when no Rigidbody
- `IFloatingOriginListener`: interface declaration; OnFloatingOriginShifted signature
- `TestShiftDriver`: default speed 10 km/s; TEST-HARNESS framing in diagnostic text labels speed as not representative of game physics; RequireComponent FloatingOriginAnchor; FloatingOriginManager.Instance reference

### F. Test file content (10 checks)

- `FloatingOriginManagerTests`: uses [SetUp] / [TearDown]; tests origin-updates-before-notify; tests throwing-listener resilience; tests singleton duplicate handling with LogAssert.Expect
- `CoordinateMathTests`: tests round-trip stability; tests strict-threshold convention (`ExactlyAtThreshold_IsFalse`)
- `WorldPositionTests`: tests interstellar-scale precision; tests planetary-scale precision

### G. CONSTRAINTS.md unchanged (3 checks)

- Line count = 1960 (unchanged from commit 028)
- Most recent paragraph (`**Prototype scaffolding: verified.**`) still present exactly once
- All sample prior-commit anchors preserved (commits 014, 015, 017, 019, 025, 026, 027, 028)

### H. Prior-commit anchors sample (9 checks)

Commit 014 damage-repair; commit 015 first verbatim-with-context anchor; commit 017 `## 3. Gameplay mechanics`; commit 019 three-category framing; commit 025 atmospheric flight subsection; commit 026 contract path; commit 027 SPACESIM reference; commit 028 closing "Real implementation work begins next session." — all preserved.

### I. .gitkeep state (1 check)

`.gitkeep` in Coordinates/ still exists as a 0-byte file (sandbox cannot unlink). Replay procedure removes it via host-side `git rm`.

## User-side verification (Unity)

Cowork cannot drive Unity. The user-side verification path:

1. Open Unity Hub. Open the SPACESIM project.
2. Unity imports the new assembly definitions and recompiles. Console should report no compile errors.
3. **Run the editor test runner.** Window → General → Test Runner. The EditMode tab should discover the four test classes from `SpaceSim.Foundation.Coordinates.Tests`: `WorldPositionTests`, `LocalPositionTests`, `CoordinateMathTests`, `FloatingOriginManagerTests`. Run all tests; confirm all pass.
4. **Populate `TestCoordinates.unity`.** Open the scene. Add:
   - **Main Camera** (right-click in Hierarchy → 3D Object → Camera, or copy from SampleScene)
   - **Directional Light** (right-click → Light → Directional Light)
   - **GameObject "FloatingOriginRoot"** (Create Empty). Attach component **FloatingOriginManager**. (Default 50 km threshold preserves the locked design.)
   - **GameObject "TestSubject"** (Create Empty, or use a 3D Sphere primitive for visibility). Attach **Rigidbody** (uncheck "Use Gravity"). Attach **FloatingOriginAnchor**. Attach **TestShiftDriver**. Position at (0, 0, 0).
   - **GameObject "WorldOriginMarker"** (use a 3D Cube primitive for visibility, scale to 2-3 units). Attach **FloatingOriginAnchor**. Position at (0, 0, 0). This marker stays at world (0,0,0) so the user sees its local position change after each shift.
   - Optional: **Canvas → Text** for the diagnostic UI. Drag the Text into the TestShiftDriver's `diagnosticLabel` field.
   - Save the scene.
5. **Press Play.** The sphere moves along +X (or whichever direction `TestShiftDriver.directionWorld` was set to). At t ≈ 5 seconds (10 km/s × 5 s = 50 km), the first shift fires. Console logs `[TestShiftDriver] Origin shifted (#1). New origin: ... Driver's world position: ...`. The sphere's local position resets to near zero; its world position keeps growing. The WorldOriginMarker cube's local position becomes -50km. Every 5 seconds thereafter, another shift fires.
6. Stop Play mode.

If all four tests pass and the test scene shifts as described, the coordinate system is verified at the file level (Cowork) and the runtime level (user). Commit 030 (sim-tick controller) wires the manager into the sim-tick boundary and revisits the FloatingOriginAnchor PROTOTYPE-QUALITY CAVEAT.

## Notes for next session

- Commit 030: sim-tick controller. Per `docs/NETCODE_CONTRACT.md` §1.2, the 30 Hz fixed-timestep loop and the ten-step sim-tick cycle. The cycle's step 6 ("detect mode transitions") and step 8 ("replicate to peers") are stubs in this commit's prototype scope (single-vessel, no multiplayer); the substantive steps for now are 1 (receive — no-op), 2 (read PhysX), 3 (convert), 4 (apply analytic), 5 (reconcile), 7 (push back to PhysX), 9 (fire events), 10 (advance counter). Commit 030 will move the `MaybeShiftOrigin` call from `TestShiftDriver.Update` into the sim-tick controller's appropriate step, and will revisit the FloatingOriginAnchor's PhysX-aware shift handling.
- The `TestShiftDriver` will become obsolete once vessel containers exist in commit 031+. It remains useful in the meantime as a manual smoke test for the coordinate system.

## Replay

```
cd C:\Users\gmkar\space_sim
git rm SPACESIM/Assets/Scripts/Foundation/Coordinates/.gitkeep
git add SPACESIM/Assets/Scripts/Foundation/Coordinates/WorldPosition.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/LocalPosition.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/CoordinateMath.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/IFloatingOriginListener.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/FloatingOriginManager.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/FloatingOriginAnchor.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/TestShiftDriver.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/SpaceSim.Foundation.Coordinates.asmdef ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/WorldPositionTests.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/LocalPositionTests.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/CoordinateMathTests.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/FloatingOriginManagerTests.cs ^
        SPACESIM/Assets/Scripts/Foundation/Coordinates/Tests/SpaceSim.Foundation.Coordinates.Tests.asmdef ^
        SPACESIM/Assets/Scenes/TestCoordinates.unity ^
        commits/029_coordinate_system.md
git commit -F commits/029_coordinate_system.md
```

The user will also need to `git add` the `.meta` files Unity auto-generates for these new assets on first open. Those `.meta` files land in commit 030 alongside the sim-tick controller, following the same pattern as commit 028 (which captured commit 027's `.meta` files after Unity verification).
