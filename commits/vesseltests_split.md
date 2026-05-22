# VesselTests.cs split (operational commit, 3 of 3 in test-infra sweep)

## Headline

`VesselTests.cs` split at the natural driver-test seam. Three sections (sections 14, 18, 19+20+21 from the pre-split numbering) moved into dedicated driver test files. Hybrid helper extraction to a new shared `VesselTestHelpers.cs`: cross-cutting helpers (used by 2+ files) extracted; single-use helpers stay local to their consumer file. File reduced from 2,625 lines to ~1,620 lines — the audit's overdue-split flag is now closed.

Test count: unchanged at **311 EditMode + 6 PlayMode = 317 green**. Tests redistributed across files, not added or removed.

## Why this is operational, not numbered

Final commit in the audit-flagged test-infrastructure sweep series. Closes the bigger-than-guideline `VesselTests.cs` size concern by extracting driver-specific test concerns to per-driver files, matching the established pattern (`PeriapsisApoapsisPredictorTests`, `SoiCrossingPredictorTests`, `AtmosphericEntryPredictorTests`, `SurfaceImpactPredictorTests`, `VesselRegistryTests`, `BodyRegistryTests`, `VesselEventPredictionDriverTests` from the IVessel commit). Lands as `commits/vesseltests_split.md` (lowercase, no number) following the operational-commit convention established by `commits/ivessel_abstraction.md`.

The three-commit test-infrastructure sweep is now complete:

- **Commit 1 (landed):** BuildState consolidation — `predictor_test_state_consolidation.md`
- **Commit 2 (landed):** Reflection migration — `reflection_migration.md`
- **Commit 3 (this one):** VesselTests.cs split

## What landed

Five files (one new helpers file, two new driver test files, two modified test files, plus the artifact).

- **`SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTestHelpers.cs` (new, ~210 lines)** — `internal static class VesselTestHelpers` with:
  - 4 constants: `LeoRadius` (7e6), `EarthMoonDistanceMeters` (3.844e8), `MoonMassKg` (7.342e22), `MoonSoiRadiusMeters` (6.6e7).
  - 3 static helper methods: `NewState(PhysicsMode = PhysXActive)`, `NewKeplerState(ReferenceBody body)`, `BuildMoonAsChildOfEarth(ReferenceBody earth)`. The two instance-field-dependent helpers (`NewKeplerState`, `BuildMoonAsChildOfEarth`) take their dependency as an explicit `ReferenceBody` parameter rather than relying on the consumer's `_body` field — enables shared usage across different test class instances.
  - 2 top-level `internal sealed` types in the namespace: `StubActiveVessel : IActiveVessel`, `ThrowingActiveVessel : IActiveVessel`. Top-level (not nested inside `VesselTestHelpers`) so consumers can reference them by short name via the regular namespace `using` — `using static` only imports members of a static class, not nested types.
  - Full XML documentation explaining the cross-cutting criterion, the `using static` usage pattern, the instance-field-to-parameter migration, and why the stubs are top-level rather than nested.

- **`SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTransitionDriverTests.cs` (new, ~290 lines)** — six tests migrated verbatim from `VesselTests.cs` section 14. Self-contained SetUp/TearDown matching the `VesselEventPredictionDriverTests.cs` pattern from the IVessel commit. Local helper `SetUpDriverWithStubActiveVessel(IActiveVessel)` stays local to this file (single-use, per the hybrid extraction strategy). Local `EarthMassKg` and `EarthMu` declarations (per the deferred 8-file constant consolidation).

- **`SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselSoiRerootingDriverTests.cs` (new, ~280 lines)** — four tests migrated verbatim from `VesselTests.cs` section 18. Self-contained SetUp/TearDown. No local helpers (all dependencies via `VesselTestHelpers`). Local `EarthMassKg` const. Migrated tests use `BuildMoonAsChildOfEarth(_body)` with explicit body argument.

- **`SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselEventPredictionDriverTests.cs` (modified, ~870 lines)** — pre-existing 1-test file from the IVessel commit grew by 15 tests + section-local helpers (4 section-19-21 helpers + 3 section-21 consts + 1 section-20 const). SetUp expanded to construct `_vesselGo` + `_vessel` always (the pre-existing POCO-fake test ignores them; the construction is transparent). `SetUpEventPredictionDriver()` helper rewritten as a no-op that returns the SetUp-constructed `_controller` (the original VesselTests.cs version constructed the controller; in this file SetUp already does that work eagerly). Now contains 16 tests total: 1 IVessel POCO-fake + 15 migrated real-Vessel tests.

- **`SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs` (modified, 2,625 → 1,620 lines)** — added `using static SpaceSim.Foundation.Vessels.Tests.VesselTestHelpers;` import. Deleted 5 sections in reverse order (21 → 20 → 19 → 18 → 14) for line-number stability during destructive edits. Updated 16 stay-section call sites for the new helper signatures (11 `NewKeplerState()` → `NewKeplerState(_body)`, 5 `BuildMoonAsChildOfEarth()` → `BuildMoonAsChildOfEarth(_body)`). Removed dead-code local declarations: `NewState` method, `NewKeplerState` method, `BuildMoonAsChildOfEarth` method, 3 Moon constants, `LeoRadius` const, `StubActiveVessel` + `ThrowingActiveVessel` nested classes. Kept `EarthMassKg` + `EarthMu` (deferred per the audit-flagged 8-file constant consolidation). Replaced removed content with explanatory inline comments pointing at `VesselTestHelpers`.

- **`commits/vesseltests_split.md` (new)** — this artifact.

## Test count delta

- Pre-commit: 311 EditMode + 6 PlayMode = 317 green
- Post-commit: 311 EditMode + 6 PlayMode = 317 green
- Delta: **+0** (behavior-preserving refactor; tests redistributed across files, not added or removed)

Final distribution across the Vessels/Tests folder (EditMode):

| File | Tests | Note |
|---|---|---|
| VesselTests.cs | 57 | down from 82 |
| VesselTransitionDriverTests.cs | 6 | new |
| VesselSoiRerootingDriverTests.cs | 4 | new |
| VesselEventPredictionDriverTests.cs | 16 | 1 (existing IVessel) + 15 migrated |
| Other test files (unchanged) | 228 | predictor tests, registry tests, propagator tests, orbital elements tests |
| **Total EditMode** | **311** | unchanged |

## Five-phase decomposition

The refactor used **phased file-at-a-time execution with verification gates between phases**. Each phase had a clean stopping point where compilation and tests could be verified host-side before proceeding. Intermediate states were compilable throughout.

**Phase 1 — Helpers extraction.** Created `VesselTestHelpers.cs` with the four cross-cutting helpers, four constants, and two stub types. File compiled standalone as dead code (nothing imported it yet). Verification: 317 tests still green; the new file imported nothing back.

**Phase 2 — New driver test files.** Created `VesselTransitionDriverTests.cs` and `VesselSoiRerootingDriverTests.cs` with their 6 + 4 tests respectively. Both files imported `using static VesselTestHelpers`. Tests temporarily existed in both old (`VesselTests.cs`) and new locations. Test count inflated to **321 EditMode + 6 PlayMode = 327 green**. Both old and new locations passed identically, confirming behavior preservation.

**Phase 3 — Modify VesselEventPredictionDriverTests.cs.** Expanded SetUp to construct `_vesselGo` + `_vessel` always (transparent to the pre-existing POCO-fake test which doesn't reference `_vessel`). Added the 15 migrated tests + section-local helpers (SetUpEventPredictionDriver as a no-op returning `_controller`, plus the 4 section helpers and 3 section consts). Test count inflated to **336 EditMode + 6 PlayMode = 342 green**.

**Phase 4 — Destructive cleanup of VesselTests.cs.** Eight sub-steps:
1. Added `using static VesselTestHelpers` import.
2-6. Deleted sections 21, 20, 19, 18, 14 in reverse order (line-number stability).
7. Updated 16 stay-section call sites for new helper signatures (`replace_all` on two distinct patterns to handle both `_vessel.Initialize(..., NewKeplerState());` and `var kepler = NewKeplerState();` callers without touching documentation strings).
8. Removed dead-code local declarations (helpers, consts, stub classes) now that no callers reference them.

Test count returned to baseline: **311 EditMode + 6 PlayMode = 317 green**.

**Phase 5 — Artifact (this file).**

## Design decisions

**Hybrid helper extraction strategy.** Cross-cutting helpers (used by 2+ files) extract to `VesselTestHelpers`; single-use helpers stay local to their consumer file. Cross-cutting members extracted: `NewState`, `NewKeplerState`, `BuildMoonAsChildOfEarth`, four Earth-Moon constants, `LeoRadius`, `StubActiveVessel`, `ThrowingActiveVessel`. Single-use stayed local: `SetUpDriverWithStubActiveVessel` (VesselTransitionDriverTests only), `SetUpEventPredictionDriver` / `SetEarthFiniteSoiAndInitialize` / `NewCrossingKeplerState` / `SetEarthSurfaceAtmosphereAndInitialize` / `NewAtmosphericCrossingState` / `NewSurfaceImpactState` (VesselEventPredictionDriverTests only). Avoids both over-extraction (every helper to shared) and under-extraction (every file duplicates everything).

**`using static SpaceSim.Foundation.Vessels.Tests.VesselTestHelpers` import pattern.** All four consumer files (VesselTests + the three driver test files) import the helpers via `using static`. Call sites stay unqualified: `NewState()`, `NewKeplerState(_body)`, `BuildMoonAsChildOfEarth(_body)`. Matches the precedent set by `PredictorTestState` in Commit 1 of the sweep.

**Static helpers with explicit body/earth parameters.** The original `NewKeplerState()` and `BuildMoonAsChildOfEarth()` instance helpers referenced `_body` directly. The static extraction takes a `ReferenceBody` parameter instead, so consumers pass their own `_body` field at the call site. This makes the helpers reusable across test class instances and explicit about their dependencies. 27 call sites (across both staying and migrating sections during the migration window) gained the explicit argument; the final state has 16 stay-section sites + 12 in the new driver test files all passing the body explicitly.

**`StubActiveVessel` and `ThrowingActiveVessel` extracted as top-level internal sealed classes.** Not nested inside `VesselTestHelpers` — C#'s `using static` only imports members of the static class, not nested types. Top-level placement in the same namespace lets consumers reference them by short name through the regular namespace `using`, without per-call-site qualification.

**`LeoRadius` local declaration retained in `VesselEventPredictionDriverTests.cs`.** Deliberate shadow over the imported version (same value 7_000_000.0). Avoids a diff at the IVessel POCO-fake test's pre-existing call sites. Local-scope wins on name resolution in C#, so the shadow is unambiguous; both declarations resolve to identical values, so behavior is unchanged. Documented inline in the file.

**`EarthMassKg` and `EarthMu` kept local in each test file.** Not extracted. Deferred per the audit-flagged 8-file constant duplication finding from Commit 2's "What's next" section. The two new driver test files raise the duplication count from 8 to 10; the planned follow-on consolidation commit will collapse all 10.

**Phase 4 reorder (Steps 3-7 before Step 2 in the original spec).** The original spec ordering would have removed local helpers in Step 2 before deleting the migrating sections in Steps 3-7. But migrating sections still in `VesselTests.cs` at Step 2 also called those helpers — removing them mid-Phase-4 would have broken compilation until Step 7 completed. The mid-Phase reorder kept the file compilable at every intermediate state. Surfaced and discussed before proceeding; the resulting sequence (Step 1 import → Steps 2-6 section deletions → Step 7 call-site updates → Step 8 dead-code removal) is the safe order.

**Reverse-section deletion order (21 → 20 → 19 → 18 → 14).** Preserves line-number stability of earlier sections during destructive Edits. Deleting section 21 first leaves sections 14-20's line numbers untouched; deleting section 20 next leaves 14-19 untouched; and so on. This made each Edit's `old_string` anchor independently of the previous deletion's line-shift effects.

## Lessons

**Phased file-at-a-time approach with verification gates manages blast radius for large refactors.** Each phase had a clean stopping point where compilation and tests could be verified host-side before proceeding. When something looks suspect at a phase boundary, the recovery scope is bounded to that phase, not to the whole commit. The five phases here ranged from "trivial dead-code file creation" (Phase 1) to "delete 1,000 lines from a 2,625-line file" (Phase 4); only the latter carried significant risk, and the prior phases established that the migration logic was sound before any destructive step.

**Compilable intermediate states are the right discipline for multi-step destructive refactors.** The mid-Phase reorder during Phase 4 demonstrated this principle: when reading the spec carefully surfaced an ordering problem (helper removal before section deletion would leave migrating sections without their helper definitions), surfacing the issue and reordering kept the file compilable throughout. The alternative — proceed with the original spec ordering and accept a broken intermediate state — would have made a Phase 4 interruption catastrophic. Always design destructive multi-step refactors so the file compiles after each individual step.

**Tests duplicated in old and new locations during the migration window provide behavior-preservation signal.** Phases 2 and 3 deliberately inflated the test count by duplicating 25 tests in both `VesselTests.cs` and the new dedicated files. Both passing identically is the empirical confirmation that the migration preserves behavior. Phase 4 deletes the originals only after this signal is observed. The discipline costs ~10 minutes of inflated test counts but eliminates a major risk path.

**Hybrid extraction (cross-cutting to shared, single-use staying local) is the right default for test infrastructure refactors.** The natural seam emerges from usage patterns: extract what's used by multiple files, leave what's used by one. Over-extraction creates abstraction debt (every helper change touches a shared file; every test file's intent is opaque to readers because all the test-setup specifics are elsewhere). Under-extraction creates duplication debt (the same helper exists in N files; future changes require N-way coordination). The hybrid balance was clear in this commit: `NewState` / `NewKeplerState` / `BuildMoonAsChildOfEarth` were used in 4 files; `SetEarthFiniteSoiAndInitialize` was used in one. The criterion is the cardinality of consumers, not abstract notions of "general-purpose-ness."

**`using static` is a powerful tool for shared helper consolidation.** Call sites stay unchanged when the local helper is removed and the imported static version takes over (with possible signature changes via explicit parameters). Combined with the explicit-parameter approach (`NewKeplerState(body)` rather than instance-method `NewKeplerState()` that reads `_body`), it produces clean composable test code. The pattern works for any cross-cutting helper extraction; this commit applied it the same way `PredictorTestState` did in Commit 1.

**`replace_all=true` with carefully crafted patterns can do many site updates in one Edit.** Phase 4 Step 7 used `replace_all` to update 16 call sites across the file (11 `NewKeplerState()` → `NewKeplerState(_body)` + 5 `BuildMoonAsChildOfEarth()` → `BuildMoonAsChildOfEarth(_body)`). Two distinct patterns covered the call-site variants while leaving documentation strings intact. The risk is matching strings or comments unintentionally; mitigation is to pick patterns with C#-code-only terminators (e.g., semicolon, assignment operator) that don't appear in surrounding documentation prose. The alternative — anchored per-site Edits — would have required 16 separate Edit calls with unique context, multiplying both effort and risk.

**Sandbox-vs-host divergence pattern surfaced again during Phase 4.** Stale reminders in the assistant context, sandbox bash unavailability, and the workflow-rule-6 pattern from `commits/035` all continued to be relevant. Host-side verification at each phase boundary was the authoritative signal; sandbox-cached reads would have been unreliable. Same pattern as the three prior operational commits (IVessel, BuildState consolidation, reflection migration); the workflow is now well-rehearsed.

## What's next

**Test infrastructure sweep is now COMPLETE.** All three audit-flagged items addressed:
- ✅ BuildState consolidation (Commit 1, `predictor_test_state_consolidation.md`)
- ✅ Reflection migration (Commit 2, `reflection_migration.md`)
- ✅ VesselTests.cs split (this commit, `vesseltests_split.md`)

Pending follow-on items (in priority order):

**(1) EarthMassKg constant consolidation.** Audit-flagged via Commit 2's artifact. Eight test files declared `private const double EarthMassKg = 5.972e24` independently; this commit's two new driver test files raised the count from 8 to 10. ReferenceBody.cs has the same numeric value as a magic-number field initializer in production. Two-step cleanup: (1a) extract `EarthMassKgDefault` as `public const double` on `ReferenceBody` (or as new `PhysicsConstants` static class — surface decision when commit lands); (1b) migrate the 10 test files to reference the production constant. Estimated one small commit. Worth landing **before commit 048** to avoid further duplication during time-warp work.

**(2) `docs/engine/STATUS.md` update.** Audit-flagged. Stale across three predictor commits + IVessel + the three test-infra sweep commits. Bulk refresh worth bundling with whatever lands next, or its own small operational commit.

**(3) `TransitionTriggerReason.AtmosphericEntryPredicted` rename.** Audit-flagged. Label drift since commit 047 — the enum value fires for surface impact too (because `NextModeTransitionTick` is N-way aggregated). Mechanical rename touching enum + callsite + tests. Separable cleanup commit.

**(4) `parent_body_id` sentinel convention documentation.** Audit-flagged. NETCODE_CONTRACT §2.7 uses `Option<BodyID>` in the contract while the code uses `Guid.Empty` as the sentinel. Documentation hygiene; small doc commit.

**(5) Pre-existing dead `using System.Reflection;` in SurfaceImpactPredictorTests.cs.** Flagged during Commit 1 + Commit 2 recon but not addressed by any sweep commit. Now safe to remove (the file no longer uses reflection after Commit 2). Cosmetic; could be folded into any subsequent test-file touch.

**(6) Operational commit convention amendment to `commits/README.md`.** Audit-flagged process question. The three test-infra sweep commits all produced artifacts, continuing the precedent set by the IVessel commit. Five operational commits to date: three with artifacts (IVessel, BuildState, reflection migration, VesselTests split), two without (`293d2b4` ReferenceBody test seam cleanup, `e27adac` scene cleanup). The convention discussion now has ample data: either amend rule 1 of `commits/README.md` to explicitly carve out operational commits, or keep rule 1 as written and acknowledge that the early operational commits should retroactively get artifacts (or simply that the convention has shifted). The recent pattern is "operational commits get artifacts"; codifying or contradicting this is the open question.

**(7) Commit 048: time-warp rate machinery.** Phase 1 next critical-path commit. Continuous-warp UI + warp-rate scaling controls + warp-respects-event-tick gating per netcode contract §4.2. End-to-end Play verification of warp-drops-on-event-tick is the validation gate. With the three predictor types populating the event queue (commits 045/046/047) and the test infrastructure now stable, commit 048 has a clean substrate to build on.
