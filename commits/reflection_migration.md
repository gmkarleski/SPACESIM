# Reflection migration (operational commit, 2 of 3 in test-infra sweep)

## Headline

14 of 15 `typeof(ReferenceBody).GetField` reflection call sites migrate to the parameterized `InitializeBodyForTesting(massKg, soiRadiusMeters, parentBody, surfaceRadiusMeters, atmosphericTopAltitudeMeters)` overload that landed in operational commit `293d2b4` (May 20). One site stays as reflection — VesselTests.cs SetUp's `surfaceRadiusMeters = 1.0` baseline, which deliberately sets a field WITHOUT calling Init afterward (the parameterized overload always invokes Init; many downstream tests need to call Init themselves at test time after additional configuration). The carve-out is documented inline.

Four dead helpers removed as part of the migration:
- `SoiCrossingPredictorTests.SetBodyMass / SetBodySoiRadius / SetBodyParent` (3 helpers, all callers folded into direct `InitializeBodyForTesting` calls)
- `AtmosphericEntryPredictorTests.SetField` (generic reflection helper, both callers folded)

Three named wrappers preserved as semantic anchors (`SetEarthFiniteSoiAndInitialize`, `SetEarthSurfaceAtmosphereAndInitialize`, `BuildMoonAsChildOfEarth`): their value is the intent-documenting name, not the reflection ceremony they wrapped; bodies shrunk to a single `InitializeBodyForTesting` call.

## Why this is operational, not numbered

Commit 2 of 3 in the audit-flagged test-infrastructure sweep series:

- **Commit 1 (landed):** BuildState consolidation — three of four duplicate helpers consolidated into `PredictorTestState.BuildState` via `using static` import pattern (`predictor_test_state_consolidation.md`)
- **Commit 2 (this one):** Reflection migration — 14 of 15 sites migrate to parameterized overload
- **Commit 3 (next):** VesselTests.cs split — driver tests for Transition + SoiReroot move to dedicated files; hybrid helper extraction

## What landed

Five files (four modified, one new artifact). All test files; no production code changes.

- **`SPACESIM/Assets/Scripts/Foundation/Vessels/Tests/VesselTests.cs` (modified)** — 9 of 10 reflection sites migrated; 1 (the SetUp baseline `surfaceRadiusMeters = 1.0`) stays as reflection with expanded carve-out comment explaining why. Three named wrapper helpers (`SetEarthFiniteSoiAndInitialize`, `SetEarthSurfaceAtmosphereAndInitialize`, `BuildMoonAsChildOfEarth`) have their bodies shrunk to a single `InitializeBodyForTesting` call but keep their intent-documenting names. Two inline test sites (`ReferenceBody_SoiRadiusMeters_ReadsInspectorValue`, `ReferenceBody_Awake_WithSelfAsParent_LogsErrorAndTreatsAsTopLevel`) and one inline parent-child test (`ReferenceBody_Awake_PopulatesParentBodyId_WhenParentSet`) all use the parameterized overload directly.
- **`SoiCrossingPredictorTests.cs` (modified)** — three `SetBody*` helpers removed (~21 lines). Four call clusters migrated: SetUp + BuildChildBody + the two inline FarBody / SmallBody tests. All four now call `InitializeBodyForTesting(massKg, soiRadiusMeters, parentBody)` directly.
- **`AtmosphericEntryPredictorTests.cs` (modified)** — `SetField` generic helper removed (~6 lines). Two callers (`SetEarthAtmosphereAndInitialize`, `SetVacuumAndInitialize`) migrated; both retain their named-wrapper status with shrunk bodies that pass surface + atmosphere via the parameterized overload's optional params.
- **`BodyRegistryTests.cs` (modified)** — one inline reflection site migrated. `Assert.IsNotNull(parentBodyField, "Sanity: parentBody field should exist via reflection")` defensive sanity assertion dropped — the compiler now serves the same function via the typed overload's signature (any future ReferenceBody refactor that removes/renames the parameter will break at build time, not at test-run time).
- **`commits/reflection_migration.md` (new)** — this artifact.

## Test count delta

- Before: 311 EditMode + 6 PlayMode = 317 green
- After: 311 EditMode + 6 PlayMode = 317 green
- Delta: **+0** (behavior-preserving refactor; no test additions, removals, or renames)

The dropped `Assert.IsNotNull` sanity in BodyRegistryTests is inside the existing `BodyRegistry_GetChildrenOf_FindsChildren` test method, not its own test — net test count unchanged.

## Design decisions (brief)

- **Carve-out rationale (SetUp site).** VesselTests.cs SetUp deliberately sets `surfaceRadiusMeters = 1.0` without calling `InitializeBodyForTesting`. The parameterized overload always invokes Init; many downstream tests rely on calling Init themselves at test time after additional per-test configuration (e.g., the wrapper helpers SetEarthFiniteSoiAndInitialize and SetEarthSurfaceAtmosphereAndInitialize, plus BuildMoonAsChildOfEarth). Pre-init field-setting without Init is exactly what reflection provides cleanly; the parameterized overload cannot replicate it without adding a new "skip Init" parameter that would clutter the production API. Keeping one reflection site is the small cost of keeping the production API clean.
- **Dead-helper removal.** Three `SetBody*` helpers in SoiCrossingPredictorTests and one `SetField` in AtmosphericEntryPredictorTests become dead code after the migration. Removing them in the same commit is the right move — leaving dead helpers around invites future drift (someone might call them, perpetuating the reflection pattern). The Edit chunks per-file already touched the helper locations; removing them adds zero new file touches.
- **Named-wrapper preservation.** Three wrapper helpers (SetEarthFiniteSoiAndInitialize, SetEarthSurfaceAtmosphereAndInitialize, BuildMoonAsChildOfEarth) survive the migration with shrunk bodies. Their value is the documenting name — "this test wants Earth with a finite SOI" reads better than an inline `InitializeBodyForTesting(massKg: EarthMassKg, soiRadiusMeters: 9.24e8)` call at every consumer site. Helper as semantic anchor, not as implementation ceremony.
- **Dropped sanity assertion.** `Assert.IsNotNull(parentBodyField, "Sanity: parentBody field should exist via reflection")` in BodyRegistryTests dropped. The defensive purpose (catch a refactor that renames/removes `parentBody`) is now served by the compiler — the parameterized overload's signature is the type-system witness for the field's existence and accessibility.

## Lessons

**Audit recon-count discrepancies are routine.** This commit landed 15 sites against an audit-claimed 16. Two days ago, the BuildState consolidation landed 3 (initially) then 4 (corrected via OrbitalElementsTests discovery) against an audit-claimed 4. The audit's count framing has been approximately right both times. Reconnaissance scoped to actually-touched files is the authoritative count; treat audit numbers as +/-1 approximations for planning purposes. The cleanest framing: **audit identifies the pattern and rough scope; recon establishes precise counts at planning time**.

**Behavior-preserving refactors that touch many sites benefit from file-at-a-time discipline.** This commit touched four files with 15 site migrations. Worked file-at-a-time, small Edit chunks per file (10 separate edits across VesselTests.cs alone), no mega-edit per file. Reduces blast radius: if a single Edit truncates (per IVessel Stage 3) or hits an unanticipated AST snag, the damage is one Edit's scope, not the entire file. The verification gates "all four modified test files pass independently" naturally maps to file-at-a-time work.

**Named wrappers survive the migration; their bodies become trivial.** Three wrappers (SetEarthFiniteSoiAndInitialize, SetEarthSurfaceAtmosphereAndInitialize, BuildMoonAsChildOfEarth) saw their bodies shrink from 6-25 lines to 3-7 lines, but the wrappers themselves stayed. Helper names that document intent are independent of how the helper implements that intent. A future helper-cleanup pass might consolidate the two SetEarth* wrappers (they're variants of the same pattern) but that's a different refactor.

**Reflection-based sanity assertions become unnecessary post-migration.** `Assert.IsNotNull(parentBodyField, ...)` was a defensive runtime check that the private field still existed. The typed overload makes this a build-time check (compiler errors if `parentBody` rename breaks the call). Migration trades runtime sanity assertion for compile-time type checking — generally a strict improvement, but worth flagging in artifacts so the lost assertion isn't perceived as a coverage drop.

**The EarthMassKg constant duplication is bigger than the audit captured.** Recon for this commit surfaced that 8 test files independently declare `private const double EarthMassKg = 5.972e24` (plus 7 of them declare `EarthMu = CoordinateMath.G * EarthMassKg`). The same numeric value lives as a magic-number field initializer in ReferenceBody.cs's production code. The audit framed "reflection migration" and "BuildState consolidation"; the constant duplication was a separate concern the audit didn't name. This is calibration data for the audit-bot — pattern-matching on similar test ceremonies (helpers, reflection) catches some duplication; pattern-matching on identical literals across files catches a different class of duplication. Worth surfacing both.

## What's next

Listed in priority order:

**(1) Commit 3 of 3: VesselTests.cs split.** Three driver-test sections (VesselTransitionDriver, VesselSoiRerootingDriver, VesselEventPredictionDriver) move to dedicated files. VesselEventPredictionDriverTests.cs already exists from the IVessel commit; receives its sibling tests from VesselTests.cs. Hybrid helper strategy: four cross-cutting helpers (NewState, NewKeplerState, NewCrossingKeplerState, BuildMoonAsChildOfEarth) extract to a shared `VesselTestHelpers.cs`; single-use helpers stay local. VesselTests.cs shrinks from ~2625 lines toward ~1500-1700.

**(2) EarthMassKg constant consolidation.** Eight test files declare `private const double EarthMassKg = 5.972e24` independently. ReferenceBody.cs's `massKg` field initializer uses the same magic number. Two-step cleanup:
- **(2a)** Extract `EarthMassKgDefault` as `public const double` on ReferenceBody (or as a new `PhysicsConstants` static class — surface decision when commit lands). Update ReferenceBody's `massKg` field initializer to reference the new constant.
- **(2b)** Migrate the 8 test files to reference the production constant. Remove local `EarthMassKg` const declarations; replace usage with `ReferenceBody.EarthMassKgDefault` (or `PhysicsConstants.EarthMassKg`, per (2a)). Same migration applies to `EarthMu = CoordinateMath.G * EarthMassKg` (used in 7 files).

Estimated one small commit. Worth landing **before commit 048** to avoid additional duplication during time-warp work, and to give the audit-bot calibration data on whether constant duplication shows up in tomorrow's audit.

**(3) SurfaceImpactPredictorTests has dead `using System.Reflection;`** (pre-existing, not introduced by this commit). Now that the file has no reflection (didn't in this commit either — it's been clean), the using is unambiguously dead. Opportunistic cleanup; could fold into Commit 3 of 3 or stand alone. ~1 line removal.

**(4) Audit calibration data feeding the bot tomorrow:**
- **15 vs 16 reflection sites** — audit count off by 1; recon is authoritative.
- **3 vs 4 BuildState helpers** — yesterday's recon got this right (audit said 4, my Stage A miscounted as 3, OrbitalElementsTests discovery corrected to 4).
- **EarthMassKg 8-file duplication NOT in audit** — the constant-duplication pattern is a separate class from helper/reflection patterns. Worth adding to the audit-bot's pattern set if it isn't already.

**(5) Operational commit convention decision** — still pending from the IVessel commit. Three operational commits now ship with artifacts (IVessel `81d1d60`, BuildState `7a...` [pending push], reflection `[this one, pending push]`); two without (cleanup `293d2b4`, scene cleanup `e27adac`). The convention discussion has more data to work with.

**(6) Audit-flagged items still pending after the three-commit sweep:**
- `docs/engine/STATUS.md` refresh (stale across three predictor commits + IVessel + the test-infra sweep)
- `TransitionTriggerReason.AtmosphericEntryPredicted` rename (label drift since commit 047)
- `parent_body_id` sentinel convention documentation in NETCODE_CONTRACT §2.7
- Commit 048: time-warp rate machinery (Phase 1 next critical-path commit)
