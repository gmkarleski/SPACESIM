# 043: Per-sim-tick mode transition trigger evaluator — disabled-by-default infrastructure

Land the per-sim-tick §3.1 mode transition trigger evaluator as disabled-by-default infrastructure. Closes the remaining Phase 1 carryover item from commit 041's close-out narrative — but partially, not fully, because the evaluator's behavior under stub-state conditions (Phase 0 always-pass for thrust, atmosphere, contact, focus, scripted thrust, multi-vessel proximity) would cause unintended automatic transitions if enabled. The `VesselTransitionDriver.Enabled` flag defaults false; the infrastructure lands and is tested, but TestVessels.unity Play behavior is unchanged. Future commits flip the flag on once upstream Phase 3+ state systems wire real values into the schema fields the evaluator reads.

The commit is structured around an asmdef-discipline trade-off (per the §3.1 contract "trigger evaluation runs at every sim-tick" satisfied at the `TickAdvanced` event boundary rather than literally inside `SimTickController.Step6_DetectModeTransitions`) and a Phase-0-stub-state pragmatism trade-off (disabled-by-default rather than enabled-with-stub-conditions-that-always-pass). Both trade-offs are documented in DECISIONS.md and below.

## Scope

- `SPACESIM/Assets/Scripts/Foundation/Vessels/TransitionEvaluation.cs` — new file. Contains the `TransitionEvaluation` struct (`SuggestedMode` nullable `PhysicsMode` + `Reason` enum) with `Stay()` and `Transition(mode, reason)` factory methods, plus the `TransitionTriggerReason` enum with 7 values: `None`, `BeyondProximityWithCleanState` (the only PhysX→Kepler reason — conjunction reports as a single value because no condition fired in isolation can claim authorship), `ProximityToActiveVessel`, `AtmosphericEntryPredicted`, `PlayerFocusSwitch`, `ScriptedThrust`, `MultiVesselProximityCluster`.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs` — modified. New public method `EvaluateTransitionTriggers(IActiveVessel activeVesselForProximity)` returning `TransitionEvaluation`. Dispatches by mode: pre-Initialize and null activeVessel return Stay; `PhysXActive` evaluates the 5-condition AND conjunction; `KeplerRails` evaluates the 5-condition OR disjunction in declared order; `InterstellarCruise` returns Stay (Phase 6 scope). New private condition helpers (one per §3.1 condition):
  - `IsBeyondProximityThreshold` / `IsWithinProximityThreshold` — real implementations using `DistanceTo()`. Strict inequalities on both sides (hysteresis-free; vessel exactly at 50 km doesn't transition either direction).
  - `DistanceTo` — double-precision Euclidean distance helper.
  - `HasNoThrust` — reads `State.PhysXState.ActiveThrustN`. PHASE 0 NOTE: stub field always-zero in Phase 0.
  - `HasNoSignificantAtmosphericDrag` — reads `State.PhysXState.AtmosphericDensity`. PHASE 0 NOTE: stub field always-zero in Phase 0; threshold is 1e-6 kg/m³.
  - `HasContactForces` — PHASE 0 STUB always-false. Doc explains why always-false beats the IsSleeping proxy.
  - `HasWellDefinedTrajectory` — real check (`_referenceBody != null`); Phase 0 invariant always passes.
  - `IsAtmosphericEntryPredicted` — reads `State.KeplerState.NextModeTransitionTick`. PHASE 0 NOTE: prediction system doesn't populate the field yet.
  - `HasPlayerFocusSwitch` — PHASE 0 STUB always-false. Focus subsystem is Phase 5+.
  - `HasScriptedThrust` — PHASE 0 STUB always-false. Vizzy is Phase 5.
  - `HasMultiVesselProximityCluster` — PHASE 0 STUB always-false. Multi-vessel sim is Phase 5+.

  Two private constants: `ProximityThresholdMeters = 50_000.0` (matches floating-origin shift threshold from commit 002/029) and `AtmosphericDensityThreshold = 1e-6` (kg/m³; corresponds to roughly 100 km altitude on Earth).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/VesselTransitionDriver.cs` — new file. Static class with public state (`Enabled`, `EvaluationCount`, `TransitionCount`) and lifecycle methods (`Initialize`, `Shutdown`, `ResetForTesting`, `OnTickAdvanced`). `Initialize` subscribes `OnTickAdvanced` to `SimTickController.Instance.TickAdvanced` (deferred-attach pattern: logs warning and returns if Instance is null, caller can retry later). `Shutdown` unsubscribes and resets all state. `ResetForTesting` clears counters and warn-once flag without touching the subscription. `OnTickAdvanced` early-returns on `!Enabled`, gets the active vessel via `SimTickController.Instance?.ActiveVessel` (warn-once on null), snapshots `VesselRegistry.Vessels` via manual array copy, iterates with per-vessel try/catch around both the evaluation call and the dispatch call, increments `EvaluationCount` BEFORE the try-block, increments `TransitionCount` only AFTER successful dispatch.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs` — modified. After the existing `SimTickController.Instance.SetActiveVessel(_vessel)` call at the end of `Start()`, adds a `VesselTransitionDriver.Initialize()` call with an inline comment block explaining the subscription is in place from Start onward but the master switch (`VesselTransitionDriver.Enabled`) defaults false; automatic transitions don't fire in Play unless code elsewhere flips the flag.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs` — modified. `SetUp` and `TearDown` extended with `VesselTransitionDriver.Shutdown()` calls (clean state between tests, parallel to existing `ClearForTesting` / `ClearInstanceForTesting` calls). Two private nested test stub classes added: `StubActiveVessel` (implements `IActiveVessel`, returns constructor-supplied position and mode) and `ThrowingActiveVessel` (throws `InvalidOperationException` on `GetWorldPosition`). Private helper `SetUpDriverWithStubActiveVessel(IActiveVessel)` consolidates the SimTickController + SetActiveVessel + VesselTransitionDriver.Initialize setup pattern used across multiple driver tests. **16 new EditMode tests** appended at the end of the file — see "Tests" section below for the per-test breakdown.
- `docs/DECISIONS.md` — modified. New entry "Per-sim-tick mode transition trigger evaluator (commit 043)" inserted after the commit 042 entry, before the `---` separator. Records the multi-part design (evaluator on Vessel, driver in Vessels module, TickAdvanced timing, disabled-by-default flag, struct return type), the six alternatives rejected, the asmdef-direction rationale, and the implication for future commits.
- `docs/PHASE_TRACKER.md` — modified. Five edits: (1) prototype-foundation line extended to "commits 026-043"; (2) "Current milestone" rewritten to describe Phase 1 carryover items now addressed (one fully closed, one partially via disabled-by-default infrastructure); (3) commit 043 row added to Recently landed; (4) Verification state Tests paragraph rewritten to 194/200 baseline with the 16-test breakdown; (5) Phase 1 remaining work section: trigger evaluator item changed from `[ ]` to `[~]` (partial), full prose updated to describe what landed at commit 043 and what remains (full activation deferred until upstream state systems exist).
- `commits/043_trigger_evaluator.md` — created (this artifact).

No netcode contract changes. No CONSTRAINTS.md changes. No ARCHITECTURE.md changes. No new asmdef references (VesselTransitionDriver lives in the Vessels asmdef, which already references SimTick from commit 038). No workflow rules added.

## Three-stage decomposition

The commit was decomposed into three stages with verification gates between them, matching the commits 040/042 pattern.

### Stage 1 — Evaluator on Vessel

Created `TransitionEvaluation.cs` (struct + enum) and added `EvaluateTransitionTriggers` plus 10 private condition helpers to `Vessel.cs`. Verification: 178 EditMode green (unchanged from commit 042 baseline; the new method is reachable but has no test coverage at this stage).

Tested in isolation by virtue of the next-stage tests, but Stage 1 itself adds zero test coverage. The structural change is contained to two files and one new file; the verification gate is "compile cleanly + existing tests still pass."

### Stage 2 — Driver + TickAdvanced subscription + TestVesselDriver wiring

Created `VesselTransitionDriver.cs` (~170 LOC static class) and added one `Initialize()` call to `TestVesselDriver.Start()`. Verification: 184 total green (still no new tests; Stage 2 establishes the dispatch path that Stage 3 tests will exercise). TestVessels.unity Play behavior unchanged (driver subscribes to TickAdvanced but `Enabled = false` returns immediately on every fire).

### Stage 3 — Tests + docs + artifact

16 new EditMode tests (10 for `EvaluateTransitionTriggers`, 6 for `VesselTransitionDriver`). PHASE_TRACKER.md updates. DECISIONS.md entry. This artifact. Verification: 194 EditMode + 6 PlayMode = 200 total green.

## Architecture

**Evaluator placement.** `EvaluateTransitionTriggers` is a public method on `Vessel`. The vessel knows its own state, reference body, rigidbody, and mode — it is the natural owner of the question "should I transition?" The method is pure read-only (does not invoke any transition; that's the driver's job).

**Driver placement.** `VesselTransitionDriver` is a static class in the Vessels asmdef. It subscribes to `SimTickController.TickAdvanced` from outside the controller (the Vessels module knows about SimTick; SimTick does not know about Vessels — same asmdef direction commit 038 established).

**The Step 6 vs TickAdvanced trade-off.** §3.1 of the netcode contract says "the trigger evaluation runs at every sim-tick" and the natural slot is `SimTickController.Step6_DetectModeTransitions`. Commit 043 satisfies "at every sim-tick" via `TickAdvanced` (one fire per tick, after Step 10's tick increment), not literally inside Step 6. Transitions complete between tick N's Step 10 and tick N+1's Step 1 — well within the "sharp transition at sim-tick boundary" semantics §3.1 specifies. The literal "inside Step 6" placement would require SimTickController to iterate `VesselRegistry`, closing the asmdef cycle commit 038 broke for IActiveVessel. The trade-off is the asmdef discipline (preserved) for the cycle-location precision (slightly off — TickAdvanced rather than Step 6). DECISIONS.md records the rationale; PHASE_TRACKER's "trigger evaluator" line notes the deferred-activation status.

**Return type — `TransitionEvaluation` struct.** Pairs the decision (`SuggestedMode` nullable) with the firing condition (`Reason` enum). The driver logs the reason directly; tests assert against both fields to verify the evaluator picked the right condition, not just the right decision. A simpler `PhysicsMode?` return would force the driver to reverse-engineer the reason from the vessel's mode, which is fragile.

**Condition implementation pattern.** Each §3.1 condition has its own private method on `Vessel` (`IsBeyondProximityThreshold`, `HasNoThrust`, `HasNoSignificantAtmosphericDrag`, `HasContactForces`, `HasWellDefinedTrajectory`, `IsWithinProximityThreshold`, `IsAtmosphericEntryPredicted`, `HasPlayerFocusSwitch`, `HasScriptedThrust`, `HasMultiVesselProximityCluster`). Each method documents whether it's real or stub via PHASE 0 NOTE / PHASE 0 STUB comments in its XML doc. The pattern makes it easy to find which conditions are wired vs deferred when reviewing.

**Driver dispatch pattern.** `OnTickAdvanced` iterates a snapshot of `VesselRegistry.Vessels` (manual `Vessel[]` copy rather than `ToArray()` because `IReadOnlyList<Vessel>` doesn't expose `.ToArray()` without LINQ; the manual copy matches the per-vessel iteration pattern from `SimTickController.Step10_AdvanceCounter`). Per-vessel try/catch isolates failures: a throwing vessel does not abort the loop or block other vessels' evaluations. `EvaluationCount` increments BEFORE the try-block so the counter reflects attempted evaluations regardless of throw outcomes; `TransitionCount` increments only AFTER successful dispatch.

## Disabled-by-default rationale (explicit)

The `VesselTransitionDriver.Enabled` flag defaults to false. This is the central operational property of commit 043 — without it, the commit could not land in Phase 0 without changing TestVessels.unity Play behavior.

**Why the flag exists:** Phase 0 has stub-state values for thrust (`PhysXState.ActiveThrustN`), atmospheric density, contact forces, player focus, scripted thrust, and multi-vessel proximity. In stub state, the corresponding condition checks always return their "no obstruction to transition" value: no thrust applied, no atmospheric drag significant, no contact forces, no focus switch, no scripted thrust, no clustering. The PhysX-active → Kepler-rails conjunction with stub state reduces to a single check: "vessel beyond 50 km from active vessel." If any Phase 0 scene placed a vessel beyond 50 km with the driver enabled, the vessel would automatically transition to Kepler-rails on the next tick — surprising behavior for any test that doesn't explicitly want it.

**Why disabled-by-default rather than implement-when-state-ready:** the evaluator's structure and tests should land before upstream state systems exist, because (a) the evaluator's design depends on upstream state being read but not produced — clarifying the contract early prevents drift; (b) the evaluator's tests provide the regression check for when the state systems wire real values; (c) the infrastructure cost of landing now is low (no behavior change in Play, low test count overhead). Disabled-by-default lets the work happen on its own timeline rather than blocking on the state systems' own timelines.

**Future activation:** when enough upstream state systems wire real values into `PhysXState.ActiveThrustN`, `AtmosphericDensity`, etc., a single future commit can flip `VesselTransitionDriver.Enabled = true` (probably from `TestVesselDriver.Start()` or a similar production-wiring point) and run the same 16 commit-043 tests to verify the behavior. The flag is a single bool; the activation commit is small.

## The self-proximity edge case

When `EvaluateTransitionTriggers` is called with `activeVesselForProximity` referencing the same vessel that's being evaluated (the active vessel evaluates its own triggers), `DistanceTo(this)` returns 0. The math works correctly without special-case logic:

- **PhysXActive mode:** `IsBeyondProximityThreshold` checks `distance > 50_000.0`; distance-to-self is 0; check returns false; conjunction fails; result is Stay. The active vessel does not suggest Kepler-rails for itself. **Correct** per the contract's "any active vessel" framing (which includes the vessel itself in the universe of active vessels).
- **KeplerRails mode:** `IsWithinProximityThreshold` checks `distance < 50_000.0`; distance-to-self is 0; check returns true; fires `ProximityToActiveVessel`; result is Transition-to-PhysXActive. If the active vessel is on Kepler-rails (e.g., after a long timewarp), the proximity-to-self check correctly pulls it back to PhysXActive on the next tick. **Correct** per the same framing.

No special-case branch needed. The contract's "any active vessel" wording is the right framing; the math reflects it naturally.

## All 16 new tests

### EvaluateTransitionTriggers tests (10)

| Test | Tests | Maps to §3.1 |
|---|---|---|
| `EvaluateTransitionTriggers_BeforeInitialize_ReturnsStay` | Pre-Initialize defensive return | none (lifecycle) |
| `EvaluateTransitionTriggers_PhysXActive_BeyondProximity_AllConditionsPass_SuggestsKeplerRails` | Happy path conjunction | P→K all five conditions |
| `EvaluateTransitionTriggers_PhysXActive_WithinProximity_SuggestsStay` | Proximity condition false → conjunction false | P→K condition 1 (negated) |
| `EvaluateTransitionTriggers_PhysXActive_WithThrust_SuggestsStay` | Thrust condition false (manually populated PhysXState.ActiveThrustN) | P→K condition 2 (negated) |
| `EvaluateTransitionTriggers_PhysXActive_InAtmosphere_SuggestsStay` | Atmospheric density condition false | P→K condition 3 (negated) |
| `EvaluateTransitionTriggers_KeplerRails_WithinProximity_SuggestsPhysXActive_WithProximityReason` | K→P first disjunctive condition | K→P trigger 1 |
| `EvaluateTransitionTriggers_KeplerRails_BeyondProximity_SuggestsStay` | All K→P conditions false → Stay | K→P all five (negated) |
| `EvaluateTransitionTriggers_KeplerRails_AtmosphericEntryPredicted_SuggestsPhysXActive` | K→P second disjunctive condition (NextModeTransitionTick within 1 tick) | K→P trigger 2 |
| `EvaluateTransitionTriggers_InterstellarCruise_ReturnsStay` | Phase 6 mode returns Stay | (Phase 6 scope) |
| `EvaluateTransitionTriggers_NullActiveVessel_ReturnsStay` | Defensive null check | (defensive) |

### VesselTransitionDriver tests (6)

| Test | Tests |
|---|---|
| `TransitionDriver_DisabledByDefault_DoesNothing` | `Enabled = false` → OnTickAdvanced is no-op, counters stay 0 |
| `TransitionDriver_WhenEnabled_EvaluatesVesselsOnTickAdvanced` | `Enabled = true` → EvaluationCount increments |
| `TransitionDriver_WhenEnabled_FiresTransitionForVesselBeyondProximity` | End-to-end: enabled driver actually fires `TransitionToKeplerRails` |
| `TransitionDriver_DiagnosticCountersIncrementCorrectly` | Two ticks; counters track across multiple invocations correctly |
| `TransitionDriver_VesselThatThrowsDuringEvaluation_LoopContinues` | Per-vessel try/catch isolation; loop continues past throwing vessel |
| `TransitionDriver_ActiveVesselNull_SkipsEvaluation` | warn-once + skip; EvaluationCount stays 0 |

## Phase 1 carryover status

Commit 041's close-out logged two Phase 1 carryover items. Both are now addressed:

1. **Fix `Vessel.Initialize`-in-KeplerRails state inconsistency** — closed at commit 042 (overload-based API).
2. **Implement per-sim-tick mode transition trigger evaluator** — **partially closed at commit 043.** Infrastructure landed: `Vessel.EvaluateTransitionTriggers`, `VesselTransitionDriver`, 16 tests. Full activation deferred: the `Enabled` flag stays false until upstream state systems populate the schema fields with real values.

PHASE_TRACKER.md marks the trigger evaluator item with `[~]` (partial) rather than `[x]` (full) or `[ ]` (untouched). The item is genuinely partial — the infrastructure is real and tested, but the contract's "trigger evaluation runs at every sim-tick" isn't operationally fulfilled until the flag flips on. The honest framing of partial-but-progressing is better than overclaiming closure.

**Phase 1 system-list work** (per the "Systems by phase → Phase 1 — Foundations" section) can now proceed without the carryover overhead — the remaining Phase 1 work is the design-content systems list (reference frame hierarchy, patched conics, time architecture and time-warp, vessel container data structure, save/load format). The trigger-evaluator activation commit will land somewhere in that Phase 3+ work when enough state is real to flip the flag.

## Lessons

### Disabled-by-default flag as Phase 0 / Phase 1 bridge

When implementing infrastructure that depends on upstream systems not yet built, a disabled-by-default flag lets the infrastructure land cleanly without affecting current-phase behavior. The flag becomes the activation point that future commits flip when enough upstream state exists. Three concrete benefits surfaced during commit 043:

1. **The infrastructure lands on its own timeline**, not blocking on the upstream systems' timelines. Commit 043 can land in Phase 0 / early Phase 1; the upstream state systems will land progressively across Phase 3+. Without the flag, commit 043 would either ship with broken stub-state behavior or be blocked until the upstream work is done.
2. **Test coverage exists for the activation path.** The 16 commit-043 tests exercise the enabled-driver path explicitly (`VesselTransitionDriver.Enabled = true` in test SetUp). When future commits flip the production flag, these tests are the regression check.
3. **The honest partial-completion status is preservable.** PHASE_TRACKER's `[~]` (partial) marker accurately reflects that the item is real but not fully operational. Without the flag, the choice would be `[ ]` (untouched, dishonest) or `[x]` (complete, also dishonest). Partial is the honest framing.

The pattern generalizes: any commit that lands infrastructure ahead of its activation conditions should use a disabled-by-default flag with a documented activation criterion (here: "upstream state systems wire real values into the schema fields the evaluator reads").

### Asmdef discipline trade-offs are worth documenting

Commit 043's "TickAdvanced rather than Step 6" location decision is a deviation from the contract's natural reading. The deviation is justified (preserves the asmdef direction commit 038 established), but it's worth documenting explicitly in the DECISIONS entry rather than hiding it inside the implementation. Future readers reviewing the trigger evaluator will see the decision documented; reviewers who think "shouldn't this be inside Step 6?" find the rationale already laid out.

The pattern generalizes: when implementation diverges from a natural reading of a contract, document the divergence as a trade-off with explicit alternatives-considered, rather than treating the implementation as obviously correct.

### Self-proximity math works without special-case

The active vessel evaluating its own triggers is a corner case that could have warranted special-case logic ("if this == activeVesselForProximity, skip proximity checks"). The math turns out to work correctly without it — distance-to-self is zero, which produces the right answer in both modes (PhysXActive: no transition; KeplerRails: transition to PhysXActive). The "any active vessel" framing in the contract handles the self case naturally because the vessel itself is one of the "any active vessels."

The lesson: before adding a special-case branch, verify the general math actually fails the corner case. Often it doesn't.

### Static state crossing test boundaries: Shutdown semantics must include all reset-worthy fields

Stage 3's first verification pass surfaced a test-pollution bug. `TransitionDriver_DisabledByDefault_DoesNothing` failed with "Expected Enabled = false (default), got true." The diagnosis chain:

1. Test runner ordering ran a test that set `VesselTransitionDriver.Enabled = true` (e.g., `TransitionDriver_WhenEnabled_EvaluatesVesselsOnTickAdvanced`) before the disabled-by-default test.
2. The earlier test's TearDown called `VesselTransitionDriver.Shutdown()`, which the original implementation defined as "unsubscribe + reset counters + reset warn-once flag." Notably absent: resetting `Enabled` to false.
3. SetUp for the next test called `VesselTransitionDriver.Shutdown()` again, same incomplete reset.
4. `TransitionDriver_DisabledByDefault_DoesNothing` started with `Enabled` still true from the earlier test's setup. The test asserted "Enabled defaults to false at the start" — it didn't, because the static `Enabled` field is process-global and the previous test had set it.

The fix: `Shutdown()` now also resets `Enabled = false`. The XML doc expanded to call out "Resets Enabled to false in addition to unsubscribing and clearing counters" so future readers see the full reset surface.

**The resolution principle is worth articulating because it generalizes:** static-state cleanup hooks should have clearly-documented semantics about what each one covers. For `VesselTransitionDriver` the two reset hooks now carry distinct contracts:

- **`Shutdown()` — the "fully off" semantic.** Unsubscribes from `TickAdvanced`, resets `_subscribed` to false, resets `Enabled` to false, clears `EvaluationCount` and `TransitionCount`, resets the warn-once flag. After Shutdown, the driver is in the exact state it would be in if it had never been used since process start. Suitable for TearDown blocks, scene unload, and any other "guarantee a clean slate" call site.

- **`ResetForTesting()` — the "between-scenario, preserve setup" semantic.** Clears counters and warn-once flag only. Does NOT touch `_subscribed`, does NOT touch `Enabled`. Suitable for tests that subscribe + enable once in a setup block and want to re-run scenarios against the same configured driver, asserting per-scenario counter values from a clean baseline. Touching `Enabled` here would blow away the caller's deliberate setup state.

The bug surfaced because the two hooks had overlapping-but-not-identical reset surfaces and the difference wasn't documented. The fix is small (one assignment line + a doc update) but the lesson generalizes: **any static-state class with multiple reset hooks should explicitly document which fields each hook covers, including a comparative note ("Shutdown resets X, Y, Z; ResetForTesting resets only X, Y")**. The comparative framing makes the choice between hooks obvious at the call site and surfaces gaps in the reset surface during code review.

A second-order lesson: NUnit doesn't guarantee test ordering, so any static-state contamination that depends on test order produces nondeterministic failures across test-runner versions / parallel-test configurations. Treating every static-state cleanup hook as "must restore all process-global state to a known baseline" is the safer default. The exception (`ResetForTesting`'s deliberate preserve-Enabled semantics) needs to be explicit and documented, not implicit.

## User-side replay procedure

1. **Open the project in Unity.** Let it recompile. The modified files are `Vessel.cs`, `TestVesselDriver.cs`, `VesselTests.cs`. The new files are `TransitionEvaluation.cs` and `VesselTransitionDriver.cs`. No new asmdef references needed.
2. **Run EditMode tests.** Test Runner → EditMode → Run All. Expect **194 green** (178 commit-042 baseline + 16 new).
3. **Run PlayMode tests.** Test Runner → PlayMode → Run All. Expect **6 green** (unchanged from commit 042; no PlayMode tests added or modified).
4. **Spot check TestVessels.unity Play behavior.** Press Play. Console should show normal startup with no new warnings from the trigger driver (the Initialize call logs nothing on success). Space-key transitions should work as before — they're imperative calls that bypass the (disabled) driver entirely. No automatic transitions should fire.
5. **Optional: verify the new tests in Test Runner.** The 16 new tests appear under `SpaceSim.Foundation.Vessels.Tests.VesselTests` with names beginning `EvaluateTransitionTriggers_*` and `TransitionDriver_*`.
6. **Git commit and push:**

   ```
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/TransitionEvaluation.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/VesselTransitionDriver.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs
   git add SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs
   git add docs/PHASE_TRACKER.md
   git add docs/DECISIONS.md
   git add commits/043_trigger_evaluator.md
   git commit -m "commit 043: per-sim-tick mode transition trigger evaluator — disabled-by-default infrastructure"
   git push origin main
   ```

   `git status` before commit should show exactly eight modified/new files (two new code files, three modified code files, two modified doc files, one new commit artifact). If other files appear, investigate via `git diff` before committing.

## Notes for future commits

- **Trigger evaluator activation commit.** When upstream state systems wire real values into `PhysXState.ActiveThrustN`, `PhysXState.AtmosphericDensity`, and contact-detection state, a small commit (estimated <50 LOC) can flip `VesselTransitionDriver.Enabled = true` from production wiring (probably `TestVesselDriver.Start()` or a sibling location). The same 16 commit-043 tests are the regression check; no new test infrastructure required for the activation.
- **Multi-vessel proximity clustering.** The §3.1 "multi-vessel proximity cluster" trigger is currently the `HasMultiVesselProximityCluster` always-false stub. When multi-vessel sim lands (Phase 5+), the implementation iterates `VesselRegistry.Vessels` for nearby rails-mode peers, applies clustering logic, and returns true when the current vessel is part of a cluster that should activate. The asmdef direction is already correct (the method lives on Vessel; iterates VesselRegistry which is in the same module).
- **NextModeTransitionTick population.** The `AtmosphericEntryPredicted` condition reads `KeplerState.NextModeTransitionTick`. No system currently populates this field (Phase 0 scope: stub null). When Phase 1+ event prediction lands (per `docs/NETCODE_CONTRACT.md` §3.1 procedure step 4 — "Compute pre-computed event predictions"), this field gets populated, and the atmospheric-entry trigger becomes real without code changes in commit 043's evaluator.
- **Player focus subsystem.** `HasPlayerFocusSwitch` is a Phase 5+ stub. The Phase 5 Mission Control UI work will wire camera / input routing such that "the focused vessel" becomes a meaningful concept. The evaluator method gets a real body at that point.
