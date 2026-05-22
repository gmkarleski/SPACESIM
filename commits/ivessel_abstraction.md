# IVessel abstraction (operational commit)

## Headline

`IVessel` interface lifts `VesselEventPredictionDriver` off concrete `Vessel` coupling, enabling POCO-fake testing for predictor driver behavior. `VesselSoiRerootingDriver` excluded from migration due to incompatible mutation surface (its `vessel.ReRootToBody(parentBody)` call is a mutating lifecycle operation that doesn't fit the read-only interface contract). `VesselTransitionDriver` also excluded for the same reason (its `TransitionToKeplerRails` / `TransitionToPhysXActive` calls are even more mutating, destroying and adding Unity components). Scope locked at "one driver migrated, interface defined, two driver exclusions documented with deferred-candidate refactors named."

## Why this is operational, not numbered

This commit closes technical debt flagged by the producer + tech director daily audits: the per-predictor exception isolation pattern in `VesselEventPredictionDriver.PredictAndUpdate` was past the inflection point (four predictor call-sites coupled to concrete `Vessel`). It is not a feature commit and does not advance the Phase 1 critical path. It lands as `commits/ivessel_abstraction.md` (lowercase, no number) rather than `NNN_topic.md`.

This is the **first operational commit to ship with an artifact**. The two preceding operational commits — `293d2b4` (ReferenceBody test seam + Physics/ placeholder resolution + SimTickController TODO sweep, May 20) and `e27adac` (scene cleanup removing SPACESIM/SampleScene/TestFoundation/_Recovery scenes + codifying TestVessels canonical, May 21) — both landed without artifacts despite touching canonical files. By the letter of `commits/README.md` rule 1 ("Every meaningful change to a canonical project file gets an artifact"), all three should have had artifacts. The inconsistency is itself input to the convention discussion (see "What's next" below).

## What landed

Four files, all under `SPACESIM/Assets/Scripts/Foundation/Vessels/`:

- **`IVessel.cs` (new)** — read-only interface with five members (`Mode`, `State`, `ReferenceBody`, `GetWorldPosition()`, `DiagnosticName`). Full XML documentation explaining the scope decision (read-only, no mutations), the why-Vessels-module-not-SimTick placement rationale, and why it doesn't extend or get extended by `IActiveVessel`.
- **`Vessel.cs` (modified)** — class signature gained `, IVessel`; new `DiagnosticName` property added (`gameObject != null ? gameObject.name : "(null)"`). Four other interface members (`Mode`, `State`, `ReferenceBody`, `GetWorldPosition`) are already-present public members that satisfy the contract without further changes. Pre-existing `IActiveVessel` implementation untouched; both interfaces coexist cleanly on the concrete type.
- **`VesselEventPredictionDriver.cs` (modified)** — `PredictAndUpdate` signature changed from `(Vessel vessel, ...)` to `(IVessel vessel, ...)`. Four inner per-predictor catch blocks simplified from `vessel.gameObject != null ? vessel.gameObject.name : "(null)"` ternary to `vessel.DiagnosticName` (net ~12 line reduction). Outer `OnTickAdvanced` iteration stays Vessel-typed; cast happens implicitly at the inner method call. Inline comment added at outer catch documenting the deliberate Vessel/IVessel scoping asymmetry. XML doc on `PredictAndUpdate` expanded with the IVessel-migration rationale.
- **`Tests/VesselEventPredictionDriverTests.cs` (new)** — first piece of the audit-flagged `VesselTests.cs` split. Self-contained SetUp/TearDown (matches `VesselTests.cs` pattern). One test `PredictAndUpdate_AcceptsIVesselFake_WritesPredictedTicks` that constructs a POCO `FakeVessel : IVessel` (nested private class, matches existing `n=3` stub pattern), invokes `PredictAndUpdate` via reflection (`NonPublic | Static`), and asserts predictor writes landed on the fake's `KeplerState` and the controller's `EventQueue`.

Plus three documentation files:

- **`docs/ARCHITECTURE.md` (modified)** — small touch to §1.3 (added one paragraph mentioning IVessel alongside IActiveVessel in the asmdef-dependency discussion). New §3.4 "Interface abstractions for drivers" covering both IActiveVessel (cycle-breaker) and IVessel (test-decoupler), the why-not-inheritance argument, the scope discipline on IVessel (read-only), and the inner-method-only migration pattern.
- **`docs/DECISIONS.md` (modified)** — new entry "IVessel abstraction for driver-test decoupling" after the commit 047 entry. Records the eight sub-decisions (interface scope, predictor-driver migration, two driver exclusions, interface independence, POCO-fake pattern, inner-method-only migration) and the seven alternatives rejected (full migration, both inheritance directions, shared FakeVessel helper, transition-driver migration, SOI-rerooter migration, adding ReRootToBody to IVessel, splitting EvaluateAndReroot).
- **`commits/ivessel_abstraction.md` (new)** — this file.

## Test count delta

- Before: 310 EditMode + 6 PlayMode = 316 green
- After: 311 EditMode + 6 PlayMode = 317 green
- Delta: +1 EditMode test (`VesselEventPredictionDriverTests.PredictAndUpdate_AcceptsIVesselFake_WritesPredictedTicks`)

Existing tests (predictor unit tests, VesselTests.cs driver-integration tests, registry tests, lifecycle tests) all continue to pass against real `Vessel` components. The IVessel migration is signature-only at the production code level; nothing in the implementation logic of `PredictAndUpdate` changed.

## Three-stage decomposition

**Stage 1 — Interface definition + concrete implementation.** Created `IVessel.cs`. Added `, IVessel` to `Vessel.cs` class signature and added `DiagnosticName` property. No driver changes, no test changes. Verification: 316 green (unchanged), no compile errors, `IActiveVessel` + `IVessel` coexist cleanly on `Vessel` with no analyzer warnings.

**Stage 2 — Predictor driver migration + POCO-fake test.** Migrated `VesselEventPredictionDriver.PredictAndUpdate` to take `IVessel`. Simplified four inner catch blocks to use `vessel.DiagnosticName`. Updated XML doc on PredictAndUpdate and on the class-level "PER-PREDICTOR EXCEPTION ISOLATION" block. Added explanatory comment at the outer-vs-inner catch asymmetry. Created `VesselEventPredictionDriverTests.cs` with one POCO-fake test that exercises the IVessel seam end-to-end (FakeVessel + real predictor static methods + real `SimTickController.EventQueue`). Verification: 317 green (+1), no compile errors, POCO-fake test demonstrates the IVessel seam works without real Vessel scaffolding.

**Stage 3 — Scope-lock-with-docs.** Reconnaissance for SOI re-rooter migration surfaced an architectural reality not visible at Stage 1 reconnaissance: `VesselSoiRerootingDriver.EvaluateAndReroot` calls `vessel.ReRootToBody(parentBody)`, a mutating lifecycle method incompatible with IVessel's read-only contract. Three resolution paths surfaced — extend IVessel to include mutation methods (rejects the read-only design), split `EvaluateAndReroot` into detect/dispatch (architecturally honest but unplanned scope expansion), or cast IVessel→Vessel inside the method (defeats the testability goal). Locked scope at "not in this commit." Stage 3 reduced to documentation: ARCHITECTURE.md §3.4, DECISIONS.md entry, this artifact. Verification: 317 green (unchanged), no compile impact (zero code changes in Stage 3).

## Design decisions

See `docs/DECISIONS.md` entry "IVessel abstraction for driver-test decoupling" for the full record. Summary of the eight sub-decisions and seven alternatives-rejected:

**Sub-decisions:** (a) new `IVessel` interface in Vessels asmdef; (b) read-only scope; (c) `VesselEventPredictionDriver` migrates; (d) `VesselSoiRerootingDriver` does not migrate; (e) `VesselTransitionDriver` does not migrate; (f) IVessel and IActiveVessel are independent interfaces with no inheritance; (g) POCO test fakes are nested-private-class per driver; (h) inner-method-only migration pattern with outer iteration staying Vessel-typed.

**Alternatives rejected:** full migration including outer iteration; IVessel : IActiveVessel inheritance; IActiveVessel : IVessel reverse inheritance; shared FakeVessel helper file; migrate VesselTransitionDriver; migrate VesselSoiRerootingDriver (this commit); adding ReRootToBody to IVessel; splitting EvaluateAndReroot into detect/dispatch (this commit).

## Lessons

**Recon the caller's verbs, not just its read surface, before locking interface scope.** The IVessel scope decision (read-only contract, no mutations) was made at Stage 1 based on Stage-1 reconnaissance of `VesselEventPredictionDriver`. The decision interacted with `VesselSoiRerootingDriver`'s `vessel.ReRootToBody(parentBody)` call in a way that wasn't visible at Stage 1 — the SOI re-rooter's mutating verb only surfaced when Stage 3 reconnaissance enumerated its internals. The architectural shape difference between drivers (read+write schema fields vs read+mutate referenced bodies) is the kind of thing recon should explicitly check for. When extending an abstraction to a new caller, recon the caller's specific verbs (not just its read surface) before locking interface scope.

**Scope discipline mid-commit is the senior move.** Three options surfaced at Stage 3 for the SOI re-rooter mismatch (extend interface, split inner method, lock scope). Locking scope and capturing the alternatives as named deferred candidates is cleaner than expanding the commit with unplanned refactor work. The deferred candidates are tracked in DECISIONS.md and in this artifact's "What's next" section; they survive as known future work rather than disappearing into "should we have done this?" ambiguity.

**Outer-vs-inner catch asymmetry generalizes to architectural pattern.** Stage 2's deliberate decision to keep the outer iteration concrete-Vessel-typed (Unity-null semantics from `VesselRegistry.Vessels` matter at iteration) while migrating only the inner per-vessel method to `IVessel` is documented inline in the driver and in ARCHITECTURE.md §3.4. Future driver refactors that want IVessel-style testability should default to this inner-method-only pattern. Catch-block diagnostics follow the same split — outer uses `vessel.gameObject.name`, inner uses `vessel.DiagnosticName`.

**`DiagnosticName` property is a small but real net-line win.** Internalizing the `gameObject != null ? gameObject.name : "(null)"` ternary into a property on the interface saved ~12 lines net across the four inner catch blocks in `VesselEventPredictionDriver`. The lesson generalizes: when an abstraction is being introduced for testability, look for repeated null-defensive idioms in the consumer that the abstraction can absorb. The property internalization makes both the production code and the test fakes simpler.

**Cowork sandbox-vs-host divergence is a recurring workflow pattern.** This commit surfaced it three times: (1) during the scene-cleanup detour mid-recon, where Linux-sandbox `git status` reported the index as `bh��` corrupt while Windows-side git was fine; (2) during Stage 2 file deletions where toolkit-side deletions confirmed successful but sandbox `[ -e file ]` stat checks reported the files still present; (3) during Stage 3 doc edits where sandbox reads of `docs/DECISIONS.md` showed the file truncated mid-sentence while host-side verification confirmed both Edit tool calls landed cleanly with the full intended content. The pattern: when Cowork's sandbox view of a file looks corrupted, host-side verification via PowerShell (`Get-Content`, `wc -l`, `Test-Path`) is the authoritative check. Trust git status and host-side file reads over sandbox-cached views. This is workflow-rule-6 territory (per `commits/035`); each instance reinforces the rule.

## What's next

Listed roughly in priority order, not all of which block commit 048:

**(1) Test infrastructure sweep commit.** Audit-flagged. Three threads merge cleanly into one commit:
- Migrate 16 reflection-based test setup sites on `ReferenceBody` to the parameterized `InitializeBodyForTesting` overload that landed in operational commit `293d2b4`. The seam exists; existing sites haven't migrated.
- Consolidate four `BuildState` helpers across predictor test files (closest-approach, SOI-crossing, atmospheric-entry, surface-impact) into a shared `PredictorTestState` helper. Tech-director audit recommends this before predictor #5 lands.
- Split `VesselTests.cs` (currently 2,625 lines) at the natural seam (orbital-element-projection tests vs registry/lifecycle/transition tests). This commit incidentally started the split by establishing `VesselEventPredictionDriverTests.cs` as a sibling; the bulk migration is still pending.

**(2) Operational commit convention decision.** Audit-flagged process question. Three operational commits in the past three days (`293d2b4` cleanup, `e27adac` scene cleanup, and this IVessel commit) — only one (this one) shipped with an artifact. By the letter of `commits/README.md` rule 1 ("Every meaningful change to a canonical project file gets an artifact"), all three should have. Two resolution options: (a) amend rule 1 to explicitly carve out operational commits (artifacts not required for operational); (b) keep rule 1 as written and require artifacts for operational commits going forward. The inconsistency surfaced across the three commits is itself the data the convention discussion will use.

**(3) `docs/engine/STATUS.md` update.** Audit-flagged. File is stale across the three predictor commits (045, 046, 047) plus the IVessel commit. Bulk refresh worth bundling with the test infrastructure sweep or landing as its own small operational commit.

**(4) `TransitionTriggerReason.AtmosphericEntryPredicted` rename.** Audit-flagged. Label drift since commit 047 — the enum value fires for surface impact too (because `NextModeTransitionTick` is N-way aggregated), so the "AtmosphericEntryPredicted" name is imprecise. Mechanical rename touching enum + callsite + tests. Separable cleanup commit.

**(5) `parent_body_id` sentinel convention documentation.** Audit-flagged. NETCODE_CONTRACT §2.7 uses `Option<BodyID>` in the contract while the code uses `Guid.Empty` as the sentinel. Documentation hygiene; small doc commit.

**(6) SOI re-rooter detect/dispatch split (conditional).** If SOI-rerooter test needs justify it (e.g., when multi-body Phase 1+ scenes need POCO-fake testing of the crossing-detection logic), split `EvaluateAndReroot` into `DetectCrossing` (returns target body + reason, takes IVessel) and `DispatchReroot` (performs `ReRootToBody`, takes concrete Vessel). Tests of detection logic use IVessel fakes; tests of dispatch are integration tests on real Vessel. Track as candidate refactor; do not pre-emptively land.

**(7) Commit 048: time-warp rate machinery.** Phase 1 next critical-path commit. Continuous-warp UI + warp-rate scaling controls + warp-respects-event-tick gating per netcode contract §4.2. End-to-end Play verification of warp-drops-on-event-tick is the validation gate.
