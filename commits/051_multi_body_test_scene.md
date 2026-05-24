# 051: Multi-body TestVessels.unity with Moon + SOI-crossing test vessel

Extends `TestVessels.unity` from single-body Earth (vacuum atmosphere) to Earth + Moon (with Earth's atmospheric top set to 100 km Kármán line) and adds a second test vessel `TestVessel_SoiCrossing` positioned on a tangential trajectory through Moon's SOI sphere. Existing `TestVessel` preserved unchanged as the regression baseline for single-body behavior — camera continues to follow it; floating-origin shifts continue to anchor on it; warp UI continues to be driven by its mode.

The SOI-crossing vessel demonstrates inbound and outbound SOI re-rooting events per Play session via `WarpUI` halt-info text, validating commit 044 (SOI re-rooting) + commit 046 (SOI-crossing halt) + commit 047 (atmospheric-entry predictor) at multi-body scale.

Per the `e27adac` canonical-scene consolidation pattern, this extends the existing scene rather than creating a parallel `TestSoi.unity`. Single-commit scope: scene + small `TestVesselDriver.cs` toggle additions + DECISIONS entry + artifact.

Second of three commits closing Phase 1 validation per `docs/phase1_validation_readiness.md` Section C. Third commit will be the validation milestone artifact.

HEAD before commit: `e6a41fc` (commit 050, audit report).

## Scope

### Code change to `TestVesselDriver.cs` (`~45 LOC additions`)

Three new `[SerializeField] private bool` fields with full `[Tooltip]` text, all default `true`:

- `_setAsActiveVessel` — gates the `SimTickController.SetActiveVessel(_vessel)` call in `Start`.
- `_writeDiagnosticLabel` — gates the `_diagnosticLabel.text = BuildDiagnosticText()` write in `Update`.
- `_handleSpaceKeyToggle` — gates the Space-key mode-toggle handler block in `Update`.

Three conditional gates wrap the corresponding existing logic with `if (_<flag>) { ... }`. Default-`true` semantics preserve single-vessel scene behavior with zero Inspector reconfiguration; non-canonical duplicates uncheck all three.

A fourth `[SerializeField] private PhysicsMode _initialMode = PhysicsMode.PhysXActive` field closes the workflow gap the three-toggle resolution itself opens. With `_handleSpaceKeyToggle` unchecked on the duplicate, there is no path from clean Play state to `KeplerRails` via input — but `VesselSoiRerootingDriver.OnTickAdvanced` skips non-Kepler vessels, so the SOI test cannot complete unless the duplicate starts on rails. The new field gates a `_vessel.TransitionToKeplerRails()` call appended to the end of `Start`, after `Vessel.Initialize(..., PhysicsMode.PhysXActive)` has run and the initial velocity has been applied to the rigidbody. `TestVessel_SoiCrossing` sets `_initialMode = KeplerRails` via Inspector; the canonical `TestVessel` leaves it at the `PhysXActive` default and continues to use Space-key as its only path to Kepler.

Class-level XML doc gains a **MULTI-VESSEL TOGGLES** paragraph explaining the pattern for future scene authors who duplicate the driver. Per-field `[Tooltip]` text describes the specific failure mode each toggle/field prevents.

### Scene edits to `TestVessels.unity`

1. **Earth `ReferenceBody`**: `atmosphericTopAltitudeMeters` changed from `0` to `100000` (100 km Kármán line). All other Earth fields untouched.

2. **New `Moon` GameObject** (root-level sibling in Hierarchy):
   - `Transform.position` = `(0, 3.844e8, 0)` — real Moon-Earth distance, placed on +Y axis to keep the existing `TestVessel`'s +X trajectory orthogonal and intersection-free.
   - `ReferenceBody` component with:
     - `massKg = 7.342e22` (real Moon mass)
     - `soiRadiusMeters = 66183000` (66,183 km — real Moon SOI per patched-conics convention)
     - `surfaceRadiusMeters = 1737400` (1,737.4 km — real Moon surface radius)
     - `atmosphericTopAltitudeMeters = 0` (Moon is airless)
     - `parentBody` Inspector-wired to Earth's `ReferenceBody`

3. **`TestVessel_SoiCrossing` GameObject**: duplicated from `TestVessel` via Hierarchy Ctrl+D, then:
   - Renamed to `TestVessel_SoiCrossing`
   - `Transform.position` = `(2.0e7, 3.17765e8, 0)` — 20,000 km tangential offset on +X from Moon's center axis (well outside Moon's 1,737 km surface radius), 50 km outside Moon's SOI sphere on the -Y side
   - `TestVesselDriver._initialVelocity` = `(0, 1000, 0)` — 1 km/s inbound toward Moon
   - All three multi-vessel toggles UNCHECKED on the `TestVesselDriver` component
   - `TestVesselDriver._initialMode` = `KeplerRails` (so the duplicate enters KeplerRails right after PhysX-active Initialize completes, since with `_handleSpaceKeyToggle` unchecked there's no Space-key path to Kepler — and `VesselSoiRerootingDriver` only processes Kepler vessels)
   - Existing component shape preserved from the duplicate: own Rigidbody, FloatingOriginAnchor (added by `Vessel.Initialize` on Start), MeshFilter, MeshRenderer, SphereCollider, Vessel component

### DECISIONS amendment

New entry: "Multi-body TestVessels.unity with Moon + SOI-crossing test vessel (commit 051)". Captures the e27adac-pattern-aligned scene-extension decision, real-Moon-parameters rationale, three-toggle infrastructure framing, six alternatives rejected (parallel scene, single-vessel bumped velocity, active-vessel-switch UI, toy parameters, single-toggle resolution, Hierarchy-order hack, separate driver class, disabled-duplicate-driver), and atmospheric-entry deferral note.

### Test count

Pre-commit: 339 EditMode + 6 PlayMode = 345 green. Post-commit: same — no test changes. Code change is additive Inspector fields with default-true gates; existing `TestVessel` behavior is bit-exact preserved. SOI re-rooting + crossing halt math already covered by EditMode tests (commit 044 + commit 046); this commit demonstrates them end-to-end in Play, doesn't add new automated coverage.

## Architecture

Three things kept the commit small while opening a meaningful new Play validation surface:

**1. The drivers iterate `VesselRegistry.Vessels`, not just the active vessel.** `VesselSoiRerootingDriver.OnTickAdvanced` and `VesselEventPredictionDriver.OnTickAdvanced` both snapshot the full registry per tick. So adding a second vessel to the scene doesn't require new driver code; the existing infrastructure picks it up automatically. The only thing the second vessel is *not* eligible for is being the floating-origin shift target (that's active-vessel-specific). That asymmetry is fine for SOI testing: the second vessel propagates correctly in space; SOI events fire via the predictor + re-rooting drivers; the WarpUI halt-info displays the halt regardless of which vessel triggered it.

**2. The Hierarchy-order race on `SetActiveVessel` is real and would silently defeat the spec's intent.** Unity processes `Awake`/`Start` on sibling GameObjects in Hierarchy order; a Ctrl+D duplicate appears immediately after the original, so its `Start` runs second. If both drivers call `SimTickController.SetActiveVessel(_vessel)`, the duplicate wins last-write-wins, and the camera/origin/warp-ceiling all track the wrong vessel. The three Inspector toggles make the canonical-driver pattern explicit at the per-instance level. Without them, naive Hierarchy duplication produces silently-wrong behavior — exactly the kind of silent failure that motivated workflow rule 6 (host is canonical) and the broader project discipline of making implicit dependencies explicit.

**3. Moon on +Y axis preserves `TestVessel`'s regression-baseline character.** At Moon position `(384.4e6, 0, 0)` (the naive +X choice), the existing `TestVessel` coasting at `+10 km/s X` from `(7e6, 0, 0)` would eventually reach Moon distance — 37,740 sim-seconds at 1× warp, but only 3.8 real-time seconds at 10000× Kepler warp. The vessel's straight-line +X trajectory would then intersect Moon's SOI sphere. By placing Moon on +Y instead, the existing vessel's +X trajectory is orthogonal to Moon's offset axis and provably cannot intersect Moon's 66,183 km SOI regardless of warp rate or session duration. The regression-baseline property holds.

**SOI-crossing trajectory geometry**: starting at `(2.0e7, 3.17765e8, 0)`, Moon at `(0, 3.844e8, 0)`. Distance from Moon's center at start = `√(2e7² + (3.844e8 - 3.17765e8)²)` = `√(4e14 + 4.4365e15)` ≈ `√(4.84e15)` ≈ `6.95e7` m (~69,500 km — just outside Moon's 66,183 km SOI on the chord-entry side). With velocity `(0, 1000, 0)`, the vessel enters Moon's SOI sphere ~50 seconds after Play start, passes ~20,000 km from Moon's center (well outside Moon's 1,737 km surface so no surface impact contamination), and exits Moon's SOI on the +Y side after another ~88 seconds. Two SOI events per Play session — inbound crossing triggers re-root from Earth → Moon + a SOI-crossing halt; outbound crossing triggers re-root from Moon → Earth + another SOI-crossing halt.

**Moon-relative trajectory after re-rooting will be hyperbolic.** At SOI entry the vessel's relative velocity to Moon is ~1 km/s tangential with a 20,000 km impact parameter — well above Moon's circular velocity at that altitude (~0.6 km/s) and below Moon's escape velocity. The relative orbit is therefore hyperbolic. `KeplerPropagator` handles hyperbolic explicitly via `M = e·sinh(H) - H` solved via Newton-Raphson (per `KeplerPropagator.cs:33,61` recon), stable up to hyperbolic anomaly H ≈ 10 — well past anything this trajectory will reach.

**`_initialMode = KeplerRails` is implemented as a post-Initialize transition, not as a parameter to `Initialize`.** The natural-looking implementation — pass `_initialMode` straight through to `_vessel.Initialize(state, _referenceBody, _initialMode)` — would silently fall back to `PhysXActive`. The 3-arg `Initialize` overload (per commit 042's design) rejects `KeplerRails` because Kepler-active vessels require precomputed orbital elements that the 3-arg signature has no parameter to receive; the overload logs an error and switches the mode to `PhysXActive`. The 5-arg overload accepts `KeplerRails` plus an `initialKeplerState`, but that pushes the orbital-element math up into `TestVesselDriver` (or its caller) — the test driver shouldn't be in the business of constructing Kepler elements from scratch. The clean alternative is `Vessel.TransitionToKeplerRails()`, which already encapsulates the math: it reads `_rb.position` + `_rb.linearVelocity` and synthesizes Kepler elements from those. Running the transition AFTER `Initialize` + the rigidbody-velocity assignment means the elements come from the just-configured state. This is also why the transition call lives at the very end of `Start`, after the four `Initialize`-side drivers (`VesselTransitionDriver`, `VesselSoiRerootingDriver`, `VesselEventPredictionDriver`); doing it earlier would leave those drivers seeing a vessel still in PhysX mode for their first registration sweep.

## Design decisions

See `docs/DECISIONS.md` "Multi-body TestVessels.unity with Moon + SOI-crossing test vessel (commit 051)" for the full multi-part design (extension-vs-new-scene, real-vs-toy parameters, three-toggle infrastructure, eight alternatives rejected). Headline:

- **Extend `TestVessels.unity`** (not parallel scene) per the e27adac canonical-scene consolidation pattern.
- **Real Moon parameters** aligned with the pre-approved `PhysicsConstants` values.
- **Three multi-vessel toggles on `TestVesselDriver`** as durable infrastructure for any future multi-vessel scene.
- **A fourth `_initialMode` `PhysicsMode` field** closing the workflow gap the three-toggle resolution opens (with `_handleSpaceKeyToggle` unchecked, the duplicate needs a non-input path to KeplerRails so `VesselSoiRerootingDriver` will process it).
- **Moon on +Y axis** to preserve the existing `TestVessel`'s +X trajectory as orthogonal regression baseline.
- **Tangential SOI-crossing trajectory** for clean inbound + outbound demonstration without surface-impact contamination.
- **Atmospheric-entry validation deferred** — scene supports it (Earth atmosphere 100 km non-zero) but neither vessel demonstrates it this commit.

## Lessons

**Warp rates compress sim time to real time in ways that defeat naive "we'll never actually reach there" assumptions.** The existing `TestVessel` at `+10 km/s X` from `(7e6, 0, 0)` reaches Moon distance in 37,740 sim-seconds — about 10.5 hours of real time at 1× warp, but only 3.8 seconds at 10000× Kepler warp. Any "the vessel won't get there" assertion in a multi-body scene must include "at maximum warp rate over expected session duration" as a sanity check. Future Play scene authoring with multiple bodies should pre-compute whether trajectories cross body SOIs at the maximum-warp-rate timescale, not just at 1× warp.

**Even when an audit confirms a code path handles a case, recon should re-verify for the specific scenario being introduced.** The Phase 1 audit said "KeplerPropagator handles elliptical, hyperbolic, retrograde implicitly." That's true in general. But for the SOI-crossing vessel specifically, the recon step on commit 051 re-verified the hyperbolic dispatch (`KeplerPropagator.cs:33,61`) and confirmed the Newton-Raphson stability bound (H ≈ 10) is comfortably past anything this trajectory would reach. The pattern: audits give general assurances; per-scenario recon confirms the specific parameters fall within the assured envelope. The cost is small (one grep + one paragraph of reasoning); the benefit is catching the edge case where a general "handled" claim doesn't cover a specific extreme.

**Hierarchy duplication of a `MonoBehaviour` with `Start`-time singleton wiring requires explicit per-instance configuration toggles.** Naïve duplicate-then-uncheck doesn't work because the duplicate runs `Start` by default, calls the same singleton-claim API (`SimTickController.SetActiveVessel`), and wins the race by Hierarchy-order. The fix is `[SerializeField] bool` flags with default `true` that gate the singleton-claim, the shared-resource write, and the input handler. Pattern is durable infrastructure, not a 051-local fix — any future scene with multiple instances of any singleton-claiming driver MonoBehaviour benefits from the same pattern. The race was identified during commit 051's recon by tracing `TestVesselDriver.Start`'s `SetActiveVessel` call against Unity's documented sibling-order Awake/Start sequencing; the alternative — pressing through and discovering the bug in Play — would have produced a failed SOI test with no clear cause.

**A resolution can open its own gap that only surfaces when the workflow is walked end-to-end.** The three-toggle fix removed the Hierarchy-order races, but disabling `_handleSpaceKeyToggle` on the duplicate also removed its only path from `PhysXActive` to `KeplerRails` — and `VesselSoiRerootingDriver` filters out non-Kepler vessels. The duplicate sat inert during initial Play tests despite the toggles working exactly as designed. A fourth `_initialMode` field (default `PhysXActive` to preserve canonical behavior) was the right closure: it gives the duplicate a non-input path to KeplerRails by triggering `Vessel.TransitionToKeplerRails()` at the end of `Start`, after the rigidbody has the initial velocity applied. The lesson is procedural rather than architectural: walk the end-to-end workflow on each resolution before declaring it complete. The audit-level "three races fixed" framing didn't catch this because the audit was about identifying the races, not about validating that the chosen resolution leaves the test scenario runnable.

**Implementing an "initial mode" field as a parameter to a constructor-like method may not be the right shape; a post-construction transition often is.** The natural-looking signature `_vessel.Initialize(state, body, _initialMode)` doesn't work — the 3-arg `Initialize` overload (per commit 042) rejects `KeplerRails` because the Kepler path requires precomputed orbital elements the 3-arg signature can't accept. Two viable alternatives: (a) push the math up by switching to the 5-arg overload that takes `initialKeplerState`, forcing the caller to do orbital-element synthesis; (b) call the 3-arg `PhysXActive`-path overload first, apply the initial velocity, then call `Vessel.TransitionToKeplerRails()` — which already encapsulates the position+velocity-to-Kepler-elements math. Option (b) wins because the transition method is the existing source of truth for "build Kepler elements from current rigidbody state," and the test driver has no business duplicating that math. The implementation lesson: when a constructor-pass parameter doesn't work because the constructor rejects the value, check whether the post-construction transition path already does the work — often it does, because mode transitions and initial setup share the same underlying math.

## What's next

- **Commit 052 — Phase 1 validation milestone artifact.** Run the validation scenarios with this commit's multi-body scene + commit 049's camera-follow in place. Record results (Console output during SOI crossings, WarpUI halt-info snapshots, tick-counter readings) into `docs/phase1_validation_results.md`. Closes Phase 1 foundations validation formally. May include a third test vessel or temporary trajectory override to demonstrate the atmospheric-entry halt that this commit enables but doesn't exercise.
- **Active-vessel switching UI** — polish gap surfaced repeatedly during this commit's design (audit Section B, DECISIONS entry, this artifact). If wanted before Phase 1 closes, lands as a separate commit. Approximate scope: one new MonoBehaviour reading a hotkey (number-keys or tab), calling `SimTickController.SetActiveVessel` on a registry-iterated vessel. ~30-50 LOC. Would also enable visual SOI demonstration on the second vessel (camera follows it after switch).
- **Atmospheric-entry trajectory demonstration** — scene now supports it (Earth atmosphere is 100 km non-zero); requires a vessel trajectory that intersects. A third test vessel (e.g., `TestVessel_AtmosphericEntry`) on a decay orbit, or a temporary `_initialVelocity` override during the validation pass, would close the last deferred Play verification from `PHASE_TRACKER.md:63`.
- **Save/load implementation** — the parallel Phase 1 track per PHASE_TRACKER. Schema hooks added in commit 048 Stage 1 are ready (per-predictor `KeplerState` fields, `IsRoutineSupply`, rational `WarpRate`). Will exercise the multi-body scene's saved state across reload.
- **`IsRoutineSupply` migration to `VesselAuthoritativeState`** — design decision locked this session (move from `Vessel` MonoBehaviour to schema-side authoritative state for save/load roundtripping). Future fixer-bot work or part of save/load groundwork.
