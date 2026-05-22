# PredictorTestState consolidation (operational commit, 1 of 3 in test-infra sweep)

## Headline

Three of four duplicate `BuildState` helpers consolidated into a shared `PredictorTestState.BuildState` in a new `Tests/PredictorTestState.cs`. Call sites stay unchanged via `using static` imports — the consolidation introduces zero per-call-site noise on top of the helper removal. Fourth `BuildState` helper (in `OrbitalElementsTests.cs`, which uses class-constant `TestBodyId` semantics rather than per-call `Guid.NewGuid()`) deferred to a separate small follow-on commit, where the rename-or-keep-scope decision will be made.

## Why this is operational, not numbered

Audit-flagged test-infrastructure improvement: the same `BuildState` helper pattern existed in three predictor test files with near-identical bodies (and a fourth in `OrbitalElementsTests` with a small semantic divergence). Helper consolidation is mechanical and doesn't advance the Phase 1 critical path. Lands as `commits/predictor_test_state_consolidation.md` (lowercase, no number) following the operational-commit convention established by `commits/ivessel_abstraction.md`.

This is commit **1 of 3** in the test-infrastructure sweep series:
- **Commit 1 (this one):** BuildState consolidation
- **Commit 2 (next):** Reflection migration — 15 `typeof(ReferenceBody).GetField` call sites move to the parameterized `InitializeBodyForTesting` overload that landed in operational commit `293d2b4`
- **Commit 3 (last):** `VesselTests.cs` split — driver tests for Transition + SoiReroot + EventPrediction move to dedicated files; hybrid helper extraction

## What landed

Five files, all under `SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/` (plus the artifact):

- **`PredictorTestState.cs` (new)** — `internal static class PredictorTestState` with single method `internal static KeplerState BuildState(double semiMajorAxis, double eccentricity, double trueAnomalyAtEpoch, Guid? referenceBodyId = null)`. Returns equatorial KeplerState (i=0, Ω=0, ω=0, epochTick=0). `referenceBodyId` defaults to `Guid.NewGuid()` per call when not provided; explicit override available for SoiCrossing tests that need a specific body id. Full XML doc explains the consolidation history, the `using static` usage pattern, and the OrbitalElementsTests scope decision.
- **`AtmosphericEntryPredictorTests.cs` (modified)** — added `using static SpaceSim.Foundation.Vessels.Tests.PredictorTestState;`. Removed local `private static BuildState` (~20 lines including comment block). Replaced with a 3-line note pointing at the consolidated helper. Six call sites unchanged (`BuildState(a, e, nu)` resolves via `using static`).
- **`SoiCrossingPredictorTests.cs` (modified)** — same pattern as Atmospheric. Removed local 4-param `BuildState` (the one with optional `referenceBodyId`). Note in the replacement comment that the optional override is preserved in the consolidated signature. Fourteen call sites unchanged.
- **`SurfaceImpactPredictorTests.cs` (modified)** — same pattern as Atmospheric. Five call sites unchanged.
- **`commits/predictor_test_state_consolidation.md` (new)** — this artifact.

## Test count delta

- Before: 311 EditMode + 6 PlayMode = 317 green
- After: 311 EditMode + 6 PlayMode = 317 green
- Delta: **+0** (pure refactor, no test additions/removals/renames)

## Design decisions (brief)

Three small choices worth recording. None warranted a full `docs/DECISIONS.md` entry — the consolidation is mechanical, not architectural.

- **Internal-static placement.** `PredictorTestState` is `internal static class` in the `SpaceSim.Foundation.Vessels.Tests` namespace, callable from all sibling test files in the same asmdef. No need for `public` (production code never calls test helpers); no need for `private` (we want cross-file visibility).
- **`using static` call pattern.** Test files import via `using static SpaceSim.Foundation.Vessels.Tests.PredictorTestState;`. Call sites stay as `BuildState(a, e, nu)` unchanged from the local-helper era. This keeps the per-call-site diff at zero — verification reduces to "no test broke," not "verify 25 call sites still produce expected behavior with new qualifier."
- **Single signature, no overload anticipation.** YAGNI on speculative overloads for future predictors. The single 4-param signature (3 required + 1 optional `referenceBodyId`) satisfies all three current callers. Future predictors that need different orbital-element defaults (e.g., non-zero inclination) can extend the signature at that time.

## Lessons

**The audit was correct about four `BuildState` helpers.** My initial reconnaissance scoped narrowly to predictor test files and miscounted as three. `OrbitalElementsTests.cs` (line 870) has the fourth helper, which I missed because I assumed "predictor test files" meant the four files literally named `*PredictorTests.cs`. The audit was actually framing the consolidation opportunity correctly across `OrbitalElementsTests` + the three predictor test files. **Reconnaissance scope must match audit scope to verify counts accurately** — when an audit says "four of X," check all places X might live, not just the obvious ones.

**`PeriapsisApoapsisPredictorTests` inlines `new KeplerState` 11 times without a helper.** The audit's "four BuildState helpers" framing was about *consolidation opportunity given the pattern*, not literal helper count. PeriapsisApoapsis predicts inline because it predates the helper convention; migrating its 11 inline literals to `PredictorTestState.BuildState` is a clean follow-on commit (see "What's next"). The audit framing was right; the literal-count framing was incomplete.

**`using static` keeps call sites unchanged when consolidating helpers across files.** Trades one `using static` line per consumer file for zero changes at potentially 25+ call sites. Per-file diff: one new using-static line + a small comment block where the local helper used to be. Easy to verify ("no call sites changed" is a strong invariant). Recommend `using static` for any cross-file helper extraction where the helper name is unique enough not to collide with consumer-local symbols.

**Helper-pattern consolidation is the right move at the 3rd or 4th concrete example.** Earlier consolidation risks premature abstraction; later consolidation costs accumulate (more files to migrate, more drift to reconcile). Three identical helpers across three files plus a fourth with minor divergence is exactly the threshold where consolidation pays off. Worth flagging when a new helper enters the codebase: "is this the third instance of a pattern? If yes, propose consolidation."

**Cowork sandbox stayed available throughout this commit.** Unlike the IVessel commit (where sandbox-vs-host divergence surfaced three times), this commit's smaller scope meant fewer cross-tool operations and no divergence events to report. The pattern from `commits/035` workflow rule 6 still applies; absence of incidents in one commit doesn't mean the rule is no longer relevant.

## What's next

Listed in priority order:

**(1) Commit 2 of 3: Reflection migration.** Fifteen `typeof(ReferenceBody).GetField` call sites across `AtmosphericEntryPredictorTests`, `SoiCrossingPredictorTests`, `VesselTests`, and `BodyRegistryTests` migrate to the parameterized `InitializeBodyForTesting(massKg, soiRadiusMeters, parentBody, surfaceRadiusMeters, atmosphericTopAltitudeMeters)` overload that landed in operational commit `293d2b4`. Per the reconnaissance, the SetUp baseline site in `VesselTests.cs` stays as reflection (carve-out for "field-set without init") with an explanatory comment. `EarthMassKg` constant passed explicitly in helpers that previously left massKg at field default.

**(2) Commit 3 of 3: VesselTests.cs split.** Three driver-test sections (VesselTransitionDriver, VesselSoiRerootingDriver, VesselEventPredictionDriver) move to dedicated files. VesselEventPredictionDriverTests.cs already exists from the IVessel commit; receives its sibling tests from VesselTests.cs. Hybrid helper strategy: four cross-cutting helpers (NewState, NewKeplerState, NewCrossingKeplerState, BuildMoonAsChildOfEarth) extract to a shared `VesselTestHelpers.cs`; single-use helpers stay local to their consumers. VesselTests.cs shrinks from ~2625 lines toward ~1500-1700.

**(3) Follow-on: OrbitalElementsTests.cs BuildState migration.** The fourth `BuildState` helper has a semantic difference (class-constant `TestBodyId` for body-id stability vs per-call `Guid.NewGuid()`). Migrating it requires either (a) renaming `PredictorTestState` to something broader like `KeplerStateTestBuilder`, or (b) accepting a name-vs-scope mismatch where OrbitalElementsTests imports a predictor-named helper. Small commit; rename decision deserves its own surfacing. Audit was right; my recon was wrong; this is the residual consolidation work the audit was originally pointing at.

**(4) Follow-on: PeriapsisApoapsisPredictorTests inline-literal migration.** Eleven inline `new KeplerState { ... }` literals across the file's tests could migrate to `PredictorTestState.BuildState`. Smaller follow-on commit; ~30-line diff total once call sites resolve via `using static`.

**(5) Follow-on: Remove unused `using System.Reflection;` in SurfaceImpactPredictorTests.cs.** Pre-existing dead using surfaced during recon for this commit; cosmetic, can land in any subsequent test-file commit or its own micro-commit.

**(6) Audit-flagged items still pending after the three-commit sweep:**
- Operational commit convention decision (whether `commits/README.md` rule 1 carves out operational commits)
- `docs/engine/STATUS.md` refresh (stale across three predictor commits plus IVessel plus this one)
- `TransitionTriggerReason.AtmosphericEntryPredicted` rename (label drift since commit 047)
- `parent_body_id` sentinel convention documentation in NETCODE_CONTRACT §2.7
- Commit 048: time-warp rate machinery (Phase 1 next critical-path commit)
