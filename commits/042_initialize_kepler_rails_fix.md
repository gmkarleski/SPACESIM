# 042: Fix Initialize-in-KeplerRails state inconsistency — overload-based API

Close the `Vessel.Initialize`-in-KeplerRails state inconsistency surfaced in commit 041 test 14 (`TransitionToPhysXActive_WhenKeplerStateNull_LogsErrorAndNoOps`). Before this commit, calling `Initialize` with `mode == PhysicsMode.KeplerRails` left `State.KeplerState` null, violating the netcode contract §2.1 schema invariant that `Mode == KeplerRails` implies `KeplerState != null`. The `TransitionToPhysXActive` guard caught the resulting bad state cleanly, but the inconsistent state itself shouldn't be reachable through the normal API.

Commit 042 fixes this with an overload-based API. The 3-arg `Initialize` overload now rejects `KeplerRails` (parallel to the existing `InterstellarCruise` rejection) and falls back to `PhysXActive`. A new 4-arg overload accepts an `initialKeplerState` parameter and populates `State.KeplerState` from it at Initialize return. The schema invariant is now enforced at Initialize boundary; the inconsistent state is no longer reachable through public API.

This closes one of the two Phase 1 carryover items logged at the commit 041 close-out. The other carryover (per-sim-tick trigger evaluator implementation) remains open.

## Scope

- `SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs` — modified. Three structural changes:
  1. The existing 3-arg `Initialize` overload XML doc is rewritten to include a new "STATE INVARIANT" section documenting which modes the overload handles and what the post-Initialize schema state guarantees. The method body adds a `KeplerRails` rejection branch (parallel to the existing `InterstellarCruise` rejection) that logs an error and falls back to `PhysXActive`.
  2. New 4-arg `Initialize(VesselAuthoritativeState, ReferenceBody, PhysicsMode, KeplerState)` overload. Per-mode validation: rejects `InterstellarCruise` with error and falls back to `PhysXActive` (initialKeplerState ignored); flags `PhysXActive` as overload-misuse with error but proceeds in `PhysXActive` (initialKeplerState ignored); rejects `KeplerRails` with null initialKeplerState (falls back to `PhysXActive`); on valid `KeplerRails` + non-null initialKeplerState, populates `State.KeplerState` from the parameter.
  3. New private `InitializeCore(state, body, initialMode, initialKeplerState)` method holds the shared body of both overloads. Receives already-validated inputs; sets `State`, `_referenceBody`, `State.Mode`, the tick bookkeeping fields, the mode-specific state field (`State.KeplerState = initialKeplerState` when KeplerRails, `null` otherwise; `State.PhysXState = null` when KeplerRails), runs `Configure*` for the component shape, sets `_initialized`, and registers with `VesselRegistry`.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs` — modified. `NewKeplerState()` helper added alongside the existing `NewState()` helper. Three existing tests updated:
  1. Line 127 (`Initialize_InKeplerRailsMode_DoesNotAddRigidbodyOrAnchor`): switched to the new 4-arg overload with `NewKeplerState()`. Without this change, the 3-arg overload's new KeplerRails rejection would invert this test's component-shape assertions.
  2. Line 237 (`TransitionToKeplerRails_WhenAlreadyKeplerRails_LogsWarningAndNoOps`): switched to the new 4-arg overload; the manual `State.KeplerState = ...` workaround line is removed.
  3. Line 838 (`TransitionToPhysXActive_WhenKeplerStateNull_LogsErrorAndNoOps`): rewritten to construct the state-inconsistency directly. The new construction Initialize-s in `PhysXActive`, `DestroyImmediate`s the Rigidbody and Anchor, then manually sets `State.Mode = KeplerRails` and `State.KeplerState = null`. This is what "construct the bug state directly" looks like once Initialize no longer produces the bug state. The guard at `Vessel.cs:305` still rejects the state cleanly; the test still passes; only the construction path changes.

  6 new EditMode tests appended at the end of `VesselTests.cs`:
  - `Initialize_With4ArgOverload_KeplerRails_PopulatesKeplerState` — happy path (4-arg + KeplerRails + valid KeplerState → State.KeplerState populated, Mode == KeplerRails).
  - `Initialize_With4ArgOverload_KeplerRails_NullKeplerState_LogsErrorAndFallsBack` — null-initialKeplerState fallback to PhysXActive.
  - `Initialize_With4ArgOverload_PhysXActive_LogsErrorAndIgnoresKeplerState` — 4-arg-with-PhysXActive overload misuse (error logged, initialKeplerState ignored, PhysXActive proceeds normally).
  - `Initialize_With3ArgOverload_KeplerRails_LogsErrorAndFallsBack` — 3-arg-with-KeplerRails rejection (the new behavior this commit adds).
  - `Initialize_With4ArgOverload_KeplerRails_DoesNotAddRigidbodyOrAnchor` — component shape correct under the 4-arg overload.
  - `Initialize_With4ArgOverload_KeplerRails_PreservesProvidedElements` — field-by-field check that all eight `KeplerState` fields the caller passes are present unchanged on `State.KeplerState`.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/PlayModeTests/VesselPlayModeTests.cs` — modified. Three tests updated:
  1. Line 96 (`OnEnable_RegistersVesselWithRegistry`): switched to `PhysicsMode.PhysXActive`. Test asserts registry registration only, doesn't reference KeplerRails specifics.
  2. Line 111 (`OnDisable_UnregistersVesselFromRegistry`): switched to `PhysicsMode.PhysXActive`. Symmetric to the above.
  3. Line 155 (`TransitionToPhysXActive_RuntimeAddedAnchor_ReceivesShifts`): switched to the new 4-arg overload, using the same `KeplerState` the test was already manually populating. This is the cleanest beneficiary of the fix — the manual workaround line goes away.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs` — UNCHANGED. The production caller passes `PhysXActive` (line 95); the 3-arg overload behavior for `PhysXActive` is unchanged.
- `docs/DECISIONS.md` — modified. New entry "Vessel.Initialize signature for state-mode consistency (commit 042)" inserted after the commit 041 entry, before the `---` separator. Records the overload-based decision, the three alternatives rejected, and the future-modes implication.
- `docs/PHASE_TRACKER.md` — modified. Five edits: (1) prototype-foundation line extended to "commits 026-042"; (2) "Current milestone" rewritten to "Phase 1 underway" describing the Phase 1 carryover-closing pattern; (3) commit 042 row added to Recently landed; (4) Verification state Tests paragraph rewritten to 178/184 baseline with the 6-test breakdown; (5) Phase 1 remaining work section: Initialize-in-KeplerRails item struck through with "Closed at commit 042" note; only the trigger evaluator item remains open. Also: Phase progression bullet for Phase 1 updated from "ready to begin; 2 carryover items" to "underway; 1 carryover item remaining."
- `commits/042_initialize_kepler_rails_fix.md` — created (this artifact).

No netcode contract changes. No new asmdef references. No CONSTRAINTS.md changes. No ARCHITECTURE.md changes. The fix is entirely internal to `Vessel.cs` plus the call-site updates the new API requires.

## API design — the behavior matrix

The two `Initialize` overloads handle the three `PhysicsMode` values across two callers (the dominant `PhysXActive` path and the explicit `KeplerRails` path) with consistent fallback semantics. The full matrix:

| Caller intent | Overload | Behavior |
|---|---|---|
| `PhysXActive` (production / typical test) | 3-arg | Proceed normally. `ConfigureForPhysXActive` adds Rigidbody + Anchor. State.KeplerState stays null. |
| `KeplerRails` (caller forgot the 4-arg overload) | 3-arg | Log error. Rewrite `initialMode = PhysXActive`. Fall through to the PhysXActive path. (Parallel to InterstellarCruise rejection.) |
| `InterstellarCruise` (Phase 6 scope) | 3-arg | Log error. Rewrite `initialMode = PhysXActive`. Fall through to the PhysXActive path. (Pre-existing behavior, unchanged.) |
| `KeplerRails` + valid `initialKeplerState` | 4-arg | Populate `State.KeplerState = initialKeplerState`. `ConfigureForKeplerRails` removes any pre-existing Rigidbody + Anchor. |
| `KeplerRails` + null `initialKeplerState` | 4-arg | Log error. Rewrite `initialMode = PhysXActive`. Fall through to the PhysXActive path. |
| `PhysXActive` (4-arg overload misuse) | 4-arg | Log error. Set `initialKeplerState = null`. Proceed as PhysXActive — vessel ends up in a valid state, but the caller is informed they used the wrong overload. |
| `InterstellarCruise` (4-arg, Phase 6 scope) | 4-arg | Log error. Set `initialKeplerState = null`. Rewrite `initialMode = PhysXActive`. Proceed as PhysXActive. |

**Invariant after Initialize returns (across both overloads, all cases):** `State.Mode == PhysicsMode.X` implies `State.XState != null` for X ∈ {PhysXActive, KeplerRails}. `InterstellarCruise` mode is unreachable through Initialize in Phase 0; the schema invariant for that mode lands with Phase 6's interstellar work.

**Design discipline:** every error path rewrites `initialMode` and (where applicable) `initialKeplerState` to known-good values BEFORE calling `InitializeCore`. The `InitializeCore` method trusts its inputs and contains no further validation. This separates the validation/fallback logic (per-overload public methods) from the state-mutation logic (private shared body), making both easier to reason about independently.

## The shared `InitializeCore` refactor

Before commit 042, the 3-arg `Initialize` method held all the validation and all the state-mutation in one method body. After commit 042, the two public overloads each do their own per-mode validation + fallback rewriting, then delegate to a single private `InitializeCore(state, body, initialMode, initialKeplerState)` that does the actual state mutation.

The refactor pays off three ways:
1. **No duplication.** The state-mutation logic (set `State.Mode`, set tick fields, populate mode-specific state, run Configure, set `_initialized`, register) appears once. A future Phase 6 overload accepting `initialCruiseState` wires into the same core; no copy-paste hazard.
2. **Clear validation contract.** `InitializeCore` documents its precondition explicitly ("the caller has already validated `initialMode` and mode-specific state parameters"). The public overloads are the contract surface; the core is the implementation.
3. **Easier to test the invariant.** Future tests asserting "after Initialize returns, the schema invariant holds" can target `InitializeCore`'s output regardless of which public overload was called. The 6 new tests in this commit exercise the public overloads (caller-facing surface), but a future maintenance commit could add invariant-property tests that exercise the core directly through reflection or `[InternalsVisibleTo]` if desired.

## The test rewrite for the inconsistency test

`TransitionToPhysXActive_WhenKeplerStateNull_LogsErrorAndNoOps` (originally commit 041 test 14) was the test that surfaced the bug commit 042 fixes. Its purpose stays the same — verify the `TransitionToPhysXActive` guard rejects the `Mode == KeplerRails && KeplerState == null` inconsistency cleanly — but its construction path changes because Initialize-in-KeplerRails no longer produces the inconsistency.

The new construction:

```csharp
_vessel.Initialize(NewState(), _body, PhysicsMode.PhysXActive);

// Clean up the components that PhysXActive Initialize added; otherwise the
// post-transition rigidbody-null assertion would fail trivially.
UnityObject.DestroyImmediate(_vesselGo.GetComponent<FloatingOriginAnchor>());
UnityObject.DestroyImmediate(_vesselGo.GetComponent<Rigidbody>());

// Force the inconsistent state.
_vessel.State.Mode = PhysicsMode.KeplerRails;
_vessel.State.KeplerState = null;

UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
    new System.Text.RegularExpressions.Regex(".*KeplerState is null.*State is inconsistent.*"));

_vessel.TransitionToPhysXActive();

Assert.AreEqual(PhysicsMode.KeplerRails, _vessel.Mode, ...);
Assert.IsNull(_vesselGo.GetComponent<Rigidbody>(), ...);
```

The test now exercises the guard as defense-in-depth: even though no production code path can reach the inconsistent state through Initialize, the guard remains because future code (other construction paths, save-load, manual State manipulation in tests) could still produce it. The guard is the last-line check; the Initialize fix is the first-line check.

The post-transition assertion changed from `_vessel.Rigidbody` (the cached field) to `_vesselGo.GetComponent<Rigidbody>()` (a fresh GetComponent query). The cached field is bound at Initialize time; after the `DestroyImmediate` call the cached reference becomes a destroyed-UnityObject (`== null` per Unity's overloaded operator but not `null` per `ReferenceEquals`). Reading via `GetComponent<Rigidbody>()` queries the GameObject's current component list directly, which is the more honest check for "is there a Rigidbody on this GameObject right now."

## Caller updates summary

- **1 production caller modified: 0.** `TestVesselDriver.cs:95` passes `PhysXActive` and is unchanged. The dominant-case API surface is backward-compatible by design.
- **3 PlayMode test callsites modified.** Two switched to `PhysXActive` (the tests didn't depend on KeplerRails specifics); one switched to the new 4-arg overload (was already manually populating a KeplerState — the new API absorbs that workaround cleanly).
- **3 EditMode test callsites modified.** All three were previously using Initialize-in-KeplerRails for various purposes: two switched to the 4-arg overload (preserve test intent); one (the inconsistency test) was rewritten to construct the bug state directly.
- **6 new EditMode tests added.** Cover the 4-arg overload's behavior matrix plus the 3-arg overload's new KeplerRails rejection.

Net: 1 production callsite untouched, 6 test callsites modified (3 PlayMode + 3 EditMode), 6 tests added. Test count: 172 EditMode (commit 041 baseline) + 6 = **178 EditMode**, PlayMode unchanged at **6**. Total **184 green**.

## Phase 1 carryover resolution

Commit 041's close-out added two items to a new "Phase 1 remaining work" section of PHASE_TRACKER.md:

1. **Trigger evaluator implementation** — the per-sim-tick §3.1 condition checks that commit 041 explicitly deferred.
2. **Initialize-in-KeplerRails state inconsistency fix** — surfaced during commit 041 test 14 writing.

Commit 042 closes item (2). Option (a) from the two recorded options was chosen: "Initialize-in-KeplerRails populates a default/parameterized KeplerState, requiring an Initialize signature with orbital element inputs." The decision rationale and rejected alternatives are recorded in DECISIONS.md.

Item (1) remains open as the sole Phase 1 carryover. Implementation will require Phase 1's broader work on authoritative state fields (thrust state, atmospheric context, contact forces, player focus, multi-vessel proximity) — none of which exist in Phase 0. The trigger evaluator's implementation is fundamentally Phase 1 work, not a quick close-out.

## Lessons

### The InitializeCore refactor as a shared-body pattern

Multi-overload APIs that share most of their state-mutation logic benefit from a shared private method that holds the body. The pattern:

1. **Public overloads do per-shape validation, fallback rewriting, and parameter normalization.** Each overload knows its own contract: which modes it handles cleanly, what mode-specific state parameters it requires, what to do on error or misuse.
2. **Public overloads end by delegating to a single private core method.** The core's preconditions are "inputs already validated, fallbacks already applied"; the core trusts its inputs.
3. **The private core method does the actual state mutation.** Sets the various state fields, runs any side effects (component shape, registry registration), establishes invariants.

This separates "validation/fallback decisions" (per-overload, lives in public methods) from "state mutation" (single source of truth, lives in `InitializeCore`). Adding a future overload (e.g., a Phase 6 `Initialize` accepting `initialCruiseState`) requires writing the new overload's validation/fallback logic but no new state-mutation logic.

The pattern is reusable across the project for any multi-overload API where the shared body is non-trivial. Future commits introducing new public APIs with mode-specific or shape-specific parameter sets should consider this pattern.

### Test-rewrite-as-construction-path-change

Commit 041 test 14 (`TransitionToPhysXActive_WhenKeplerStateNull_LogsErrorAndNoOps`) was structured around constructing the inconsistent state via Initialize-in-KeplerRails. When the underlying bug was fixed, the test's purpose (verify guard rejection) stayed unchanged but its construction path had to change. The new construction directly builds the inconsistent state via `DestroyImmediate` + manual `State` manipulation, bypassing the now-invariant-respecting Initialize.

This is a small instance of a larger pattern: **fixing a bug often changes the construction path for tests that target the bug's downstream defense-in-depth.** When the fix removes the bug from one construction path (the natural API), the defense-in-depth check (the guard) is now harder to test because it's only reachable through unnatural construction. The test stays as documentation that the guard still works; the test's setup elaborates to construct the no-longer-reachable state directly.

The discipline: when a bug fix removes a production-reachable inconsistency, write the defense-in-depth test using an unnatural construction path (direct field manipulation, reflection, `[InternalsVisibleTo]` test API), and document the unnaturalness in the test's comment. Future readers should know that the construction path looks weird precisely because the bug is fixed.

### Backward compatibility through overload addition

Adding a new overload that doesn't change the existing signature is the lowest-disruption way to extend an API. The 1-of-1 production callsite stayed untouched; the existing `PhysXActive` callers (the dominant case) saw zero changes. The cost was paid in the explicit mode-rejection branch of the 3-arg overload — KeplerRails callers of the 3-arg API get a clear error message pointing them at the 4-arg overload, rather than silently producing inconsistent state.

Future API extensions to `Vessel` (or any multi-mode component) should prefer overload addition over signature changes when backward compatibility matters and the new parameters are mode-specific.

## User-side replay procedure

1. **Open the project in Unity.** Let it recompile. The modified files are `Vessel.cs`, `VesselTests.cs`, `VesselPlayModeTests.cs`. No new asmdef references needed (all imports already in place).
2. **Run EditMode tests.** Test Runner → EditMode → Run All. Expect **178 green** (172 commit-041 baseline + 6 new). The 6 new tests appear under `SpaceSim.Foundation.Vessels.Tests.VesselTests` with names beginning `Initialize_With*` and `Initialize_With3ArgOverload_KeplerRails_LogsErrorAndFallsBack`.
3. **Run PlayMode tests.** Test Runner → PlayMode → Run All. Expect **6 green** (unchanged count; the 3 modified tests pass under their new construction).
4. **Spot check the rewritten test.** `TransitionToPhysXActive_WhenKeplerStateNull_LogsErrorAndNoOps` should pass under its new construction path (Initialize-in-PhysXActive + DestroyImmediate + manual State manipulation). If it fails with "expected log not received," the production error message has drifted from the regex pattern.
5. **Git commit and push:**

   ```
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/PlayModeTests/VesselPlayModeTests.cs
   git add docs/PHASE_TRACKER.md
   git add docs/DECISIONS.md
   git add commits/042_initialize_kepler_rails_fix.md
   git commit -m "commit 042: Initialize overload for KeplerRails — fixes Phase 1 carryover state inconsistency"
   git push origin main
   ```

   `git status` before commit should show exactly six modified/new files. If other files appear, investigate via `git diff` before committing.

## Notes for future commits

- **Trigger evaluator (the remaining Phase 1 carryover).** Now the sole open item in "Phase 1 remaining work." Implementation will require authoritative state fields for thrust, atmospheric context, contact forces, player focus, and multi-vessel proximity — none of which exist yet. The trigger evaluator commit (commit 043+) is a substantively larger piece of work than 042 was.
- **Phase 6 InterstellarCruise overload.** When Phase 6's interstellar work begins, the third `Initialize` overload — `Initialize(state, body, PhysicsMode.InterstellarCruise, CruiseState initialCruiseState)` — will follow the same pattern as commit 042's 4-arg overload. The existing 3-arg `InterstellarCruise` rejection becomes "use the 5-arg overload" or wherever the pattern lands. The shared `InitializeCore` will need a third branch for populating `State.CruiseState`. This is documented in commit 042's `Initialize` XML doc as "When interstellar mode ships, an analogous overload accepting `initialCruiseState` will land then."
- **Save-load construction.** Save-load is in the Phase 1 system list ("Save/load format"). The 4-arg overload is the intended construction path for save-load restoring a vessel to Kepler-rails: load the persisted `KeplerState`, pass it to `Initialize(state, body, KeplerRails, savedKeplerState)`. The overload's existence makes save-load's vessel-reconstruction path well-defined; the previous Phase 0 state (Initialize-in-KeplerRails leaves KeplerState null) would have forced save-load to do a manual `State.KeplerState =` assignment as a workaround, repeating the same anti-pattern the commit 041 tests had to use.
