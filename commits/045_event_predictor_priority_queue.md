# commit 045: Event predictor + priority queue infrastructure

First of multi-commit Phase 1 event-prediction work. Lands the architectural infrastructure for analytic event scheduling per CONSTRAINTS §2 ("Mode transitions and event scheduling") and netcode contract §4.1 ("Event prediction queue"): a single priority queue owned by `SimTickController`, a pure-function predictor for periapsis/apoapsis events, a per-tick driver that runs predictors on Kepler-rails vessels, and the warp-bounding consumer in `RunFixedUpdateCycle` that respects the queue. Subsequent commits (046-048+) layer additional predictors (atmospheric entry, surface impact, SOI crossing) on top of this infrastructure without changing the queue or the dispatch.

The commit also extracts shared anomaly-conversion math from `KeplerPropagator` into `OrbitalElements` (commit 045 Stage 1), giving both the propagator and the new predictors a single source of truth for true↔mean anomaly conversions. The propagator behavior is preserved bit-for-bit (verified by the 15 commit-040 propagator tests still passing).

## What landed

**New files (5):**
- `SPACESIM/Assets/Scripts/Foundation/SimTick/SimEventType.cs` — enum of analytic event categories. Seven values named upfront (Periapsis, Apoapsis, SoiCrossing, AtmosphericEntry, SurfaceImpact, ScheduledBurn, InterstellarArrival) per CONSTRAINTS §2's extensibility hook even though only Periapsis and Apoapsis get populated in this commit. Named `SimEventType` rather than `EventType` to avoid `UnityEngine.EventType` collision — see Lessons.
- `SPACESIM/Assets/Scripts/Foundation/SimTick/EventPriorityQueue.cs` — sorted queue of (tick, vesselId, eventType) entries. `SortedSet` + side `Dictionary<(Guid, SimEventType), long>` index for O(log n) update/peek operations and O(log n) effective remove-all-entries-for-vessel. Tie-breaking via ValueTuple.CompareTo (lexicographic by tick, Guid, enum ordinal). Public API: `UpdateVesselEntry`, `PeekTopTick`, `TryPeekTop`, `RemoveVesselEntries`, `Clear`.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/PeriapsisApoapsisPredictor.cs` — pure-function predictor. `Predict(KeplerState, currentTick, mu, tickIntervalSeconds)` returns `(long? periapsisTick, long? apoapsisTick)`. Elliptical branch computes both; hyperbolic branch computes periapsis if not yet passed, apoapsis always null. Overflow defense via `long.MaxValue / 2` threshold.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/VesselEventPredictionDriver.cs` — static class driver. Subscribes to `SimTickController.TickAdvanced`. Iterates Kepler-rails vessels, runs predictor, writes results to `KeplerState.NextPeriapsisTick` / `NextApoapsisTick` and `SimTickController.EventQueue`. No `Enabled` flag — predictors are real implementation, always-on.
- `SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/EventPriorityQueueTests.cs` — 12 EditMode tests covering add/update/remove, tie-breaking determinism, no-op defensive paths.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/PeriapsisApoapsisPredictorTests.cs` — 11 EditMode tests covering elliptical and hyperbolic branches, edge cases, round-trip consistency across ticks, near-parabolic stability, overflow defense.

**Modified files (5):**
- `SPACESIM/Assets/Scripts/Foundation/Vessels/OrbitalElements.cs` — added three public static helpers (`MeanMotion`, `TrueToMeanAnomaly`, `MeanToTrueAnomaly`), three public constants (`KeplerConvergenceTolerance`, `MaxKeplerIterations`, `ParabolicInstabilityBand`), and the private Newton-Raphson solvers + Atanh helper extracted from `KeplerPropagator`.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/KeplerPropagator.cs` — refactored `PropagateState` to call the new `OrbitalElements` helpers. Removed the duplicate private anomaly-conversion methods. Kept `WrapTwoPi` (propagation-specific). Cross-references in class XML doc updated.
- `SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickController.cs` — added `EventQueue` property. Refactored `FixedUpdate` to read `EventQueue.PeekTopTick()` and clamp to int range with two overflow defenses (delta ≤ 0 → 0; delta > int.MaxValue → int.MaxValue). Removed the "Empty event queue (commit 033 scope)" comment that's no longer true.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs` — added `EventQueue.RemoveVesselEntries(State.VesselId)` cleanup at the end of `TransitionToKeplerRails`, `TransitionToPhysXActive`, and `ReRootToBody`. Defensive null-coalescing through `SimTickController.Instance?.EventQueue?.`.
- `SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs` — added `VesselEventPredictionDriver.Initialize()` after the existing driver initializations in `Start()`.

**Modified tests (2):**
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/OrbitalElementsTests.cs` — added 12 anomaly-conversion tests (Stage 1). Renamed `ComputeFromStateVector_EventPredictionFields_AreNull` to `..._AreNullAtConstruction` with XML doc clarifying that fields are populated by `VesselEventPredictionDriver` on the next sim-tick (Stage 3).
- `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs` — added 6 `VesselEventPredictionDriver` integration tests. Updated SetUp/TearDown to call `VesselEventPredictionDriver.Shutdown()`.

**Modified docs (3):**
- `docs/NETCODE_CONTRACT.md` — §2.3 amended. `next_periapsis_tick` and `next_apoapsis_tick` now typed `Option<SimTickCount>`. Rationale sentence added explaining hyperbolic-orbit semantics.
- `docs/PHASE_TRACKER.md` — Recently landed row, Tests paragraph, milestone narrative, end-to-end Play verification paragraph, Systems-by-phase Phase 1 entry.
- `docs/DECISIONS.md` — new entry "Event predictor + priority queue infrastructure (commit 045)" with the 10 locked sub-decisions, 6 alternatives rejected, rationale, implication.

**Removed (1):**
- `SPACESIM/Assets/Scripts/Foundation/SimTick/EventType.cs` — renamed to `SimEventType.cs` (see Lessons). Cowork-side delete completed; user-side `git rm SPACESIM/Assets/Scripts/Foundation/SimTick/EventType.cs.meta` cleans up the orphaned .meta file.

**Test count delta:** 227 (commit 044 baseline) → 268 EditMode. +41 net (+12 anomaly-conversion in Stage 1, +29 in Stage 2 [12 queue + 11 predictor + 6 driver], 0 in Stage 3 — the single test rename is +0). PlayMode unchanged at 6. Total: 268 + 6 = 274 green.

## Three-stage decomposition

### Stage 1 — Math extraction (227 → 239 green)

Extracted shared anomaly-conversion math from `KeplerPropagator` (where it was duplicated as private helpers) into `OrbitalElements` (where it's public). Three new public statics: `MeanMotion`, `TrueToMeanAnomaly`, `MeanToTrueAnomaly`. Convergence-tolerance constants moved with them. The Newton-Raphson solvers and Conway's-starter implementations now live in `OrbitalElements` as private helpers; `KeplerPropagator.PropagateState` calls the new public surface directly. Single source of truth for the math.

Twelve new tests in `OrbitalElementsTests.cs` cover the new public helpers across circular, elliptical, hyperbolic, near-parabolic, and signed-anomaly cases. The pre-existing 15 propagator tests continue to pass — the refactor preserves propagator behavior bit-for-bit because the math is identical, just relocated.

One in-flight issue caught at verification time: missing `using UnityEngine;` for `Debug.LogWarning` in `OrbitalElements.cs`. Fixed by adding the import directive. Test count: 239.

### Stage 2 — Queue + predictor + driver (239 → 268 green)

Five new files. `SimEventType` enum, `EventPriorityQueue` class, `PeriapsisApoapsisPredictor` static class, `VesselEventPredictionDriver` static class, plus `EventPriorityQueueTests.cs` and `PeriapsisApoapsisPredictorTests.cs` for the new types. Six new driver tests appended to `VesselTests.cs`. `SimTickController` gains a public `EventQueue` property. `TestVesselDriver.Start()` calls `VesselEventPredictionDriver.Initialize()`.

The compile-error rename surfaced during Stage 2 verification (see Lessons). `EventType` collides with `UnityEngine.EventType`; renamed to `SimEventType` cleanly across 4 files. After rename, 268 green.

### Stage 3 — Integration + docs + artifact (268 → 268 green; tests unchanged)

`SimTickController.FixedUpdate` reads `EventQueue.PeekTopTick()` and clamps to int range before feeding `Warp.ComputeAnalyticIterations`. Warp now respects the queue per netcode contract §4.2. `Vessel.TransitionToKeplerRails`, `Vessel.TransitionToPhysXActive`, and `Vessel.ReRootToBody` call `EventQueue.RemoveVesselEntries` at the end of each method to invalidate stale predictions; the predictor driver repopulates on the next tick for vessels still in Kepler-rails. `OrbitalElementsTests` test renamed for timing clarity (assertion unchanged). NETCODE_CONTRACT §2.3 amended. PHASE_TRACKER updated. DECISIONS entry added. This artifact written.

## Design decisions (cross-reference)

Full design rationale lives in `docs/DECISIONS.md` under "Event predictor + priority queue infrastructure (commit 045)". Ten sub-decisions documented: nullable typing, queue ownership on SimTickController, SortedSet + side index internal structure, tie-breaking via ValueTuple.CompareTo, overflow defense thresholds, SimEventType naming, driver pattern parallel to existing drivers, pure-function predictors with schema-side caching and invalidation-on-state-change, no disabled-by-default flag, KeplerPropagator refactor to use shared OrbitalElements helpers.

## Lessons

### `EventType` collides with `UnityEngine.EventType` — name reserved-feeling types defensively

The first Stage 2 verification pass failed to compile with CS0104 ambiguous-reference errors. Root cause: `UnityEngine` defines `EventType` (the input-event enum used by the legacy GUI system). Any C# file in the project that imports both `UnityEngine` and the `SpaceSim.Foundation.SimTick` namespace would see two `EventType` symbols and refuse to compile until one was disambiguated.

`VesselEventPredictionDriver.cs` imports both — it lives in the Vessels module and uses `UnityEngine.Debug` for logging. The two `EventType.Periapsis` / `EventType.Apoapsis` lines in `PredictAndUpdate` triggered the ambiguity. Renamed the enum to `SimEventType` everywhere; clean compile.

**Generalizable:** any commonly-used identifier in an enum or class should be checked against the `UnityEngine` namespace before being defined. The pattern that catches this: type the name into a Unity project's autocomplete and see whether the engine offers a competing symbol. `EventType`, `Event`, `Object` (System.Object collision), `Random` (UnityEngine.Random vs System.Random), `Transform`, `Color`, `Time` are all names worth prefixing or namespacing defensively. Project convention going forward: prefix with `Sim` when the type is a project-side analog to a Unity-side concept.

### Compile errors visible in the Console must be checked before relying on Test Runner counts

This time the error was visible in Unity's Console (CS0104 with a clear "ambiguous reference between 'UnityEngine.EventType' and 'SpaceSim.Foundation.SimTick.EventType'" message). The Test Runner UI showed unchanged test counts because the compilation never completed. The user surfaced the issue directly: "Compile error: EventType ambiguous with UnityEngine.EventType."

This is the second instance of the pattern from commit 043's compile-error-silent-fallback lesson — if the Test Runner shows numbers matching what they were before the most recent code change, suspect compile failure in the production assembly or one of its dependencies. The Console pane is the diagnostic surface; Test Runner numbers alone don't surface compile failures.

**Operational rule:** always check the Unity Console first when Test Runner numbers look unchanged. Treat unchanged numbers as "tests didn't run" rather than "tests ran and all old ones passed."

### Exception-isolation tests for driver patterns benefit from an `IVessel` abstraction; deferred until a different need calls for it

`VesselTransitionDriver` and `VesselSoiRerootingDriver` test exception isolation via `ThrowingActiveVessel` stub injected as the active-vessel reference for proximity checks. `VesselEventPredictionDriver` doesn't take any external stub — it iterates `VesselRegistry.Vessels` directly, and the predictor handles all numerical edge cases internally (NaN/infinity → null; overflow → null; no division by zero possible at the API level). There's no clean way to make a real `Vessel` with a real `ReferenceBody` and a real `KeplerState` cause `PredictAndUpdate` to throw from outside.

The `EventPredictionDriver_VesselThatThrows_LoopContinues` test ended up verifying the simpler property "two vessels both get evaluated" with explanatory comments about why throw-injection isn't feasible here. The try/catch structure IS in place in `OnTickAdvanced`'s production code; the exception-isolation behavior is visible in code review but not directly test-exercised.

**The right long-term fix is an `IVessel` interface** that the driver iterates over (currently the driver iterates `Vessel` directly, since `VesselRegistry.Vessels` returns concrete `Vessel`). With an `IVessel` abstraction, tests could inject a throwing stub the same way the other drivers do. This refactor is deferred until another commit needs the abstraction for its own reasons — speculative-abstraction-for-tests would be premature.

### Two no-op-defensive queue tests document contracts the implementation supports

`EventPriorityQueue.UpdateVesselEntry(vesselId, eventType, null)` on an unknown vessel: no-op. `EventPriorityQueue.RemoveVesselEntries(unknownVessel)`: no-op. Both behaviors are real contracts the implementation supports (null-checks at the top, lookup-misses handled gracefully). The two corresponding tests document the contracts at +2 above the spec's queue-test target.

**Generalizable:** when a public API surface includes idempotent operations or null-safety behaviors, the corresponding "no-op when X is absent/null" tests are cheap to write and capture the contract explicitly. Future maintainers who refactor the implementation see the test fail if they accidentally make the operation throw — the test documents the API's expected behavior under boundary inputs.

## User-side replay procedure

```
cd C:\Users\gmkar\space_sim

# 1. Stage the orphaned .meta file removal (Cowork-side rename left it behind):
git rm SPACESIM/Assets/Scripts/Foundation/SimTick/EventType.cs.meta

# 2. Stage all the new and modified files:
git add SPACESIM/Assets/Scripts/Foundation/SimTick/SimEventType.cs
git add SPACESIM/Assets/Scripts/Foundation/SimTick/EventPriorityQueue.cs
git add SPACESIM/Assets/Scripts/Foundation/SimTick/SimTickController.cs
git add SPACESIM/Assets/Scripts/Foundation/SimTick/Tests/EventPriorityQueueTests.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/OrbitalElements.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/KeplerPropagator.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/PeriapsisApoapsisPredictor.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/VesselEventPredictionDriver.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/Vessel.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/TestVesselDriver.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/OrbitalElementsTests.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/PeriapsisApoapsisPredictorTests.cs
git add SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs
git add docs/NETCODE_CONTRACT.md
git add docs/PHASE_TRACKER.md
git add docs/DECISIONS.md
git add commits/045_event_predictor_priority_queue.md

# 3. Commit
git commit -m "commit 045: event predictor + priority queue infrastructure (periapsis/apoapsis predictors; warp respects queue; SimEventType renamed to avoid UnityEngine.EventType collision)"

# 4. Push
git push origin main
```

`git status` after step 1 should show 16 staged changes (1 deletion + 15 modifications/additions). After step 2, all 17 expected files staged. After step 3, working tree clean.

## Verification

1. Open Unity. Watch the Console — any red CS errors are blockers. After the `EventType` → `SimEventType` rename, the Console should be clean.
2. Test Runner → EditMode → expect 268 green.
3. Test Runner → PlayMode → expect 6 green.
4. Spot-check Play behavior in TestVessels.unity: the vessel enters Kepler-rails, the event-predictor driver runs each tick. `RunFixedUpdateCycle` now respects the event queue — at high warp rates, the cycle should clamp advancement to land on event ticks. With the single vessel at LEO (period ~5800 s), periapsis and apoapsis events fire periodically. Visible at high warp as warp "slowing" near event ticks.
5. Spot-check: KeplerPropagator-test behavior preserved. The 15 commit-040 propagator tests all still pass after the Stage 1 math extraction.

## What's next

Phase 1 system-list work continuing:
- **Commit 046:** atmospheric entry predictor. Reads `ReferenceBody`'s atmospheric profile (when that field lands) or hand-set atmospheric-boundary altitude. Populates `KeplerState.NextModeTransitionTick` (for vessels predicted to enter atmosphere; that triggers the §3.1 K→P transition).
- **Commit 047:** surface impact predictor. Reads `ReferenceBody`'s radius; predicts when a vessel's trajectory intersects the surface. Populates `KeplerState.NextModeTransitionTick`.
- **Commit 048+:** SOI crossing predictor. Reads parent and child body positions + radii; predicts when a vessel crosses an SOI boundary. Populates `KeplerState.NextSoiTransitionTick`.
- **Continuous time-warp UI + rate-scaling controls.** The queue lookup is wired into `RunFixedUpdateCycle` at commit 045; the player-facing warp controls (1×, 10×, 100×, etc. UI) are still ahead.
- **Save/load format.** Independent of the predictor work; CONSTRAINTS §10 flags JSON vs binary as an open question.

The `SimEventType` enum already names the remaining predictor categories (ScheduledBurn, InterstellarArrival) for Phase 5+ and Phase 6 work respectively. The extensibility hook from CONSTRAINTS §2 is in place: adding new event types is "write a predictor + populate the enum value." Architecture unchanged.
