# Phase 1 Validation Readiness — audit report

**Audit date:** 2026-05-23
**HEAD audited:** `77dc8a0` (commit 048 Stage 5 closed; 339 EditMode + 6 PlayMode = 345 green)
**Audit scope:** read-only systematic survey of Phase 1 engine systems to identify what's implemented vs documented vs absent, so Grayson can targeted-play-test the working subset rather than discover gaps through manual play.

---

## Executive summary

Phase 1 (per `docs/PHASE_TRACKER.md`) is **foundations only**: coordinates, floating origin, sim-tick spine, reference frame hierarchy, predictors, event queue, three-mode physics architecture, mode transitions, time-warp, vessel containers, save/load. Engine thrust, atmospheric flight model, collision detection, and camera systems are explicitly Phase 3 / Phase 5 work and out of Phase 1 scope.

**Within Phase 1 scope, the foundations work.** Coordinates + floating origin shift at 50 km threshold (commit 029). Kepler-rails propagation is mathematically exact and drift-free by construction (commit 040). The five predictors populate `KeplerState` fields each tick (commits 045–047). Time-warp controller + UI is end-to-end functional with the per-mode 5x/10000x/100000x ceilings, rational rate machinery, and event-bus halt surfacing (commit 048).

**Three things block end-to-end Phase 1 validation in `TestVessels.unity`:**

1. **No camera-follow.** The vessel leaves the camera frustum within ~5 seconds at the test driver's default 10 km/s initial velocity. All visual feedback during Play comes from the on-screen Canvas diagnostic UI; the Scene view shows an empty starfield. This is a usability gap, not a code gap — building a 30-line camera-follow MonoBehaviour unlocks visual validation cheaply.

2. **`TestVessels.unity` is single-body with vacuum-atmosphere Earth.** SOI re-rooting (commit 044), SOI-crossing halt (commit 046), and atmospheric-entry halt (commit 047) are all *unobservable* in this scene because there's no second body and `AtmosphericTopAltitudeMeters = 0`. The code for all three is implemented and EditMode-tested; only the Play-mode end-to-end demonstration is missing. Either extending the scene to Earth+Moon + finite atmosphere, or building a separate multi-body test scene, unblocks the three deferred end-to-end Play verifications PHASE_TRACKER explicitly flags (lines 63 — "deferred to a future verification commit").

3. **`VesselTransitionDriver.Enabled = false` by default.** Per commit 043's design, automatic mode transitions are gated until upstream state systems (thrust simulation, atmospheric density, contact forces, player focus) populate the schema fields with real values. The imperative Space-key path through `TestVesselDriver` works fine. End-to-end automatic transition is intentionally deferred to Phase 3+ — not a Phase 1 gap.

**Can the full "launch → orbit → warp → reentry → land" flight be validated end-to-end right now? NO** — but that's because four of those five legs are Phase 3+ scope (launch needs thrust, reentry needs drag, landing needs collision). The Phase 1 foundations subset that DOES work end-to-end is: **Kepler-rails orbital motion with time-warp + floating-origin shifts + surface-impact halt + manual mode toggle.** That's the validation envelope right now.

---

## System-by-system findings

### System 1 — Launch and thrust capability

**Status:** ABSENT

**Implementation summary:** No code path applies thrust to a vessel anywhere in the Foundation codebase. The thrust fields exist as schema only:
- `PhysXState.ActiveThrustN` (`PhysXState.cs:61`) and `ActiveThrustDirection` (`:67`) — declared fields with no writers other than initialization to zero.
- `Vessel.cs:554` initializes `ActiveThrustN = 0.0` on transition to PhysX-active; this is the *only* write site in the codebase, and it writes zero.
- `Vessel.cs:975-979` reads `ActiveThrustN` solely to evaluate the §3.1 `HasNoThrust()` transition condition; comment explicitly states "no system writes to it yet — engine simulation is Phase 3+".
- Searches for `AddForce` / `ApplyForce` across `Foundation/` return **zero hits**. No `FixedUpdate` integration of thrust into rigidbody velocity anywhere.
- `TestVesselDriver.cs:101` sets `_vessel.Rigidbody.linearVelocity = _initialVelocity` once at `Start()` — a one-shot initial velocity, not continuous thrust.
- `Vessel.cs:1105-1109` `HasScriptedThrust()` is a Phase 0 stub returning `false`.

**Observable behavior:** In Play mode, a vessel coasts ballistically at its `_initialVelocity` (default `(10000, 0, 0)` m/s per `TestVesselDriver.cs:57`). Gravity is disabled on the rigidbody, so the vessel drifts in a straight line until 50 km from origin, when floating-origin shifts kick in. No keyboard or in-game input changes velocity, orientation, or applies any force.

**Gaps:** Everything for an end-to-end "launch" test. No throttle, no thrust direction control, no fuel, no mass change, no force integration. Consistent with Phase 3 scope per `PHASE_TRACKER.md`.

**Test recommendation:** Do not attempt to play-test launch/thrust. Confirm absence by observing the rigidbody coast at the initial velocity with no input affecting it. The "launch" leg of the flight scenario is fundamentally out of Phase 1 scope.

---

### System 2 — Orbital flight (KeplerRails physics mode)

**Status:** WORKING (with notable architectural caveat — propagation is lazy/pull-based, not per-tick).

**Implementation summary:**
- `KeplerPropagator.PropagateState(state, currentTick, mu, tickIntervalSeconds)` is stateless pure math: dt → mean motion `n` → ν₀→M₀ → advance → solve Kepler → ν → `OrbitalElements.ComputeStateVector` (`KeplerPropagator.cs:103-151`). Handles elliptical, hyperbolic, retrograde implicitly; parabolic measure-zero band noted.
- `KeplerState` is a sealed class with the six classical elements + epoch tick + reference body GUID + predictor fields `NextPeriapsisTick / NextApoapsisTick / NextSoiTransitionTick / NextAtmosphericEntryTick / NextSurfaceImpactTick` (`KeplerState.cs:16-124`).
- Propagator is invoked at THREE call sites in `Vessel.cs`: `TransitionToPhysXActive` (`:503`), `RerootToNewSoi` (`:648`), and `GetWorldPosition` (`:715`). There is **NO per-tick analytic advancement** — `SimTickController.Step4_ApplyAnalyticUpdates` is deliberately empty (`SimTickController.cs:438`). Propagation is computed *on demand* whenever `GetWorldPosition()` is called, with `dt = (currentTick − EpochTick) * tickInterval`. The epoch never advances during rail flight.
- Tests (`KeplerPropagatorTests.cs`): includes `CircularOrbit_FullPeriod_ReturnsToStart` (line 98) and `EllipticalOrbit_FullPeriod_ReturnsToStart` (line 155) — full-period round-trips functionally exercising long-baseline drift. The architecture is intrinsically drift-free: a single closed-form jump from epoch to current tick, no accumulating integration error.

**Observable behavior:** In `TestVessels.unity`, once the test vessel is on Kepler rails, its visual position updates every time something calls `GetWorldPosition()` (diagnostic UI, transition-trigger evaluator, etc.). It orbits Earth exactly per closed-form propagation; period closure is exact within solver tolerance (~7e-4 m at LEO).

**Gaps:**
- No render-loop driver guarantees `GetWorldPosition` is called every frame for the visual transform — combined with the absent camera-follow (System 12), even a correctly-propagating vessel is invisible after the first few seconds.
- No explicit long-duration drift test (e.g., 100-period propagation), though the math forbids drift.

**Test recommendation:** Open `TestVessels.unity`, transition the vessel to KeplerRails (Space key), run at 10000× warp for ~5 minutes of real time (~50,000 sim-ticks). Verify via diagnostic UI that (a) position updates trace a closed ellipse, (b) `KeplerState.EpochTick` does NOT advance (rail-flight invariant), (c) `NextPeriapsisTick` / `NextApoapsisTick` get repopulated as previous ones pass. Periapsis recurrence is the cleanest one-eye test.

---

### System 3 — Time-warp (commit 048)

**Status:** WORKING (controller + UI + predictor halt wiring all present; one architectural caveat).

**Implementation summary:**
- `WarpController` — singleton MonoBehaviour. Rational `WarpRate` (num/den), per-mode ceilings via `CeilingFor(PhysicsMode)`: PhysX=5, Kepler=10000, Interstellar=100000 (`WarpController.cs:81-87, 210-219`). Public API: `SetDiscreteLevel`, `SetContinuousRate`, `SetTargetTick`, `Pause`, `Resume`, `RegisterHaltEvent`, `ClearHalt`, `SetActiveVesselMode`. Events: `OnRateChanged`, `OnWarpHalted`.
- `WarpRate` — readonly struct, validated `Discrete` levels `{1,5,10,100,1000,10000,100000}` (`WarpRate.cs:81-99`) and `Continuous` `[1,1000]` (`:109-118`); `AdvanceTicks` supports future fractional but v1 denominator is always 1.
- `SimTickController.FixedUpdate` (`SimTickController.cs:262-307`): peeks `EventQueue.PeekTopTick()`, computes `ticksUntilNextEvent`, then calls `WarpController.Instance.ComputeAnalyticIterations(ticksUntilNextEvent, TickNumber)` — null-safe fallback to 1 (`:302`). The returned count drives the analytic-step loop in `RunFixedUpdateCycle` (`:330-344`).
- `ComputeAnalyticIterations` (`WarpController.cs:399-440`) returns `min(effectiveRate.Numerator, ticksUntilNextEvent)` after per-mode ceiling clamp, with target-tick exact-landing clamp and always-at-least-1 floor on halt/pause.
- `WarpUIController.cs` — validates `WarpController.Instance` in `Start`, wires button click handlers, subscribes to events. Degrades gracefully when references or singleton missing.
- `VesselEventPredictionDriver.PredictAndUpdate` has THREE halt registration sites — SOI (`:321`, gated by `!vessel.IsRoutineSupply`), atmospheric entry (`:374`, unconditional), surface impact (`:410`, unconditional). All three use imminent-tick gate `tick.Value <= tickNumber + 1`. `RegisterHalt` helper (`:443-456`) builds `WarpHaltInfo` and calls `WarpController.Instance?.RegisterHaltEvent` — null-safe.
- Mode-aware ceiling sync: `SimTickController.SetActiveVessel` and `Step6_DetectModeTransitions` both call `WarpController.Instance?.SetActiveVesselMode(...)` (`:206, :505`).

**Observable behavior:** In `TestVessels.unity` with the Stage-4 Canvas wired, pressing discrete-rate buttons changes rate; the per-mode ceiling clamps it; pause/resume works; target-tick lands exactly. With test vessel on Kepler rails in vacuum-atmosphere Earth, the SurfaceImpactPredictor will register a halt when the orbit's surface intersection becomes imminent; atmospheric entry never fires (atmosphere top = 0 → predictor returns null); SOI crossing never fires (single-body scene). PhysX cap at 5× shows up as the slider/buttons being clamped (controller accepts higher rates but `EffectiveRate` returns the ceiling).

**Gaps:**
- **PhysX 5× is a passive cap, not a forced transition.** Controller doc (`WarpController.cs:78-80`) acknowledges Stage 3 was originally intended to turn the 5× into a *trigger point for a forced KeplerRails transition*. Stage 5's DECISIONS amendment formally documents force-transition as deferred to a future commit; `TransitionTriggerReason.WarpRateForcedRails` enum value is held as infrastructure. **A PhysX vessel at 5×-clamped warp will simply run slow — no forced transition.**
- **Atmospheric-entry halt is dead code in TestVessels.unity** because the scene uses vacuum atmosphere (top = 0). Cannot be smoke-tested without scene modification.
- **SOI-crossing halt is dead code** in single-body TestVessels — only one body, no children, infinite SOI by convention.
- Per-Kepler-rails analytic iteration: Step 4 / Step 5 do nothing per iteration. At 10000× Kepler warp the only thing advancing is `TickNumber`; vessel positions update lazily on `GetWorldPosition` query. This is correct (Kepler is closed-form) but easy to mistake for "broken" if you watch the vessel between queries.

**Test recommendation:** In `TestVessels.unity` (with Canvas + WarpController wired per `docs/stage4_setup_guide.md`):
1. Press 5× — PhysX vessel runs at 5×, slider/higher buttons can't push past it. Confirm `EffectiveRate` displays `5x`.
2. Put vessel on Kepler rails (Space), press 10000× — confirm rate display, `TickNumber` advances ~10000× faster.
3. Set up a sub-orbital Kepler trajectory (periapsis below 6371 km) and warp at 10000× — `SurfaceImpactPredicted` halt should fire when the predicted impact tick comes within +1 of current; rate freezes, halt-info text shows `SurfaceImpactPredicted` with vessel ID.
4. Target-tick: programmatically call `SetTargetTick(currentTick + 50000, currentTick)` — confirm exact-landing with `TargetTickReached` halt.

Atmospheric-entry and SOI halts cannot be smoke-tested in TestVessels.unity — they need a different scene.

---

### System 4 — Atmospheric entry

**Status:** ABSENT (atmospheric physics) / PARTIAL (entry-prediction-only)

**Implementation summary:** Only the predictor exists. `AtmosphericEntryPredictor.cs:83-103` is a static pure function: given a `KeplerState`, body, current tick, tick interval, it solves the conic equation `r(ν) = SurfaceRadiusMeters + AtmosphericTopAltitudeMeters` and returns the absolute sim-tick of the next inbound boundary crossing — or null on vacuum bodies. It computes a TICK; applies no force, models no density profile, computes no heating. `ReferenceBody.cs:141-148` carries one scalar `atmosphericTopAltitudeMeters` (default 0.0 = vacuum). No density-altitude profile, no scale height, no temperature. The class doc explicitly defers `atmospheric_profile` to "Phase 4+ / Phase 5 atmospheric flight model". `PhysXState.AtmosphericDensity` (`PhysXState.cs:72`) is a schema field, but the only writer in production code is `Vessel.cs:556` which hardcodes `AtmosphericDensity = 0.0`. Grep for `heating|thermal|temperature` across Foundation returned zero files.

The atmospheric-entry trigger fires (`Vessel.cs:1047-1057` `IsAtmosphericEntryPredicted` reads `KeplerState.NextAtmosphericEntryTick`) and would suggest `TransitionTriggerReason.AtmosphericEntryPredicted` — but `VesselTransitionDriver.Enabled = false` (`VesselTransitionDriver.cs:59`) so the trigger never dispatches in production.

**Observable behavior:** In Play, vessels on Kepler-rails with an orbit intersecting the atmospheric boundary will have `NextAtmosphericEntryTick` populated each tick. The event enqueues into `EventPriorityQueue`, and per PHASE_TRACKER line 63 warp will halt at the predicted tick. On reaching the predicted tick, however, NOTHING force-physical happens — the vessel keeps Kepler-propagating right through the atmosphere as if it weren't there. In TestVessels.unity the body's `atmosphericTopAltitudeMeters = 0` so the predictor returns null and no event is enqueued at all.

**Gaps:** No drag force. No density profile. No heating/destruction. No mode-transition dispatch (driver disabled). Atmospheric flight is Phase 3 + Phase 5 work per docs.

**Test recommendation:** None for the *physics* of atmospheric entry. To validate the predictor's tick computation in Play, set `atmosphericTopAltitudeMeters > 0` on the body in TestVessels.unity, give the vessel a Kepler orbit that dips through that boundary, and verify warp halts at the predicted tick. Do not expect any aero behavior.

---

### System 5 — Surface contact and landing

**Status:** ABSENT (collision/landing/destruction) / PARTIAL (surface-impact-prediction-only)

**Implementation summary:** `SurfaceImpactPredictor.cs:79-92` is a static pure function: solves `r(ν) = SurfaceRadiusMeters` via the same shared `OrbitalElements.SolveConicAtRadius` helper as atmospheric/SOI predictors. Returns null if the orbit's periapsis is above the surface. Computes a TICK; does nothing physical. `ReferenceBody.cs:105-109` has scalar `surfaceRadiusMeters` (default 6.371e6).

Grep for `Collider | OnCollisionEnter | OnTriggerEnter | Raycast` across Foundation returned exactly ONE file — `VesselTests.cs` — and that's the test substrate, not production code. Vessels have no Collider component (`Vessel.ConfigureForPhysXActive` at `Vessel.cs:1132-1148` only adds Rigidbody + FloatingOriginAnchor). Bodies have no collider geometry. There is no physical surface to land on. Grep for `(ground | landed | destroy | damage | explosion | crash)` returned only mode-transition rigidbody cleanup or future-work comments — no destruction-on-impact logic exists.

The `SurfaceImpactPredicted` trigger (`Vessel.cs:1073-1083`) would suggest `PhysXActive` mode if the driver were enabled — but landing back in PhysXActive at the predicted impact tick puts you in a colliderless rigidbody flying through a colliderless sphere.

**Observable behavior:** In Play, a Kepler-rails vessel whose orbit intersects the body surface gets `NextSurfaceImpactTick` populated and enqueued. Warp halts on that tick. At that tick, no transition fires (driver disabled), no force happens, no destruction happens. The vessel continues propagating its Kepler orbit through the inside of the planet.

**Gaps:** No Collider/MeshCollider on bodies. No Collider on vessels. No OnCollisionEnter handler. No landed-state representation in schema. No rest-on-surface logic. No destruction. No damage model. The Phase 1 surface-impact predictor exists solely to fire warp halt + (when enabled) a K→P transition for *future* contact handling.

**Test recommendation:** None for the *physics* of landing. To validate the predictor's tick computation, set up a Kepler orbit with periapsis below 6371 km and verify warp halts at the impact tick. Do not expect a landing.

---

### System 6 — SOI transitions

**Status:** WORKING (within its constraints)

**Implementation summary:** `VesselSoiRerootingDriver.cs` is a real-implementation always-on driver. No `Enabled` flag — class doc (`:18-27`) is explicit that this is "NOT DISABLED-BY-DEFAULT" because SOI data uses `PositiveInfinity` convention for top-level bodies so single-body scenes correctly find no crossings. `OnTickAdvanced` (`:132-180`) snapshots `VesselRegistry.Vessels`, skips non-KeplerRails (`:156`), and calls `EvaluateAndReroot`. `EvaluateAndReroot` (`:195-237`) does the math: outward check (`distanceToCurrent > currentBody.SoiRadiusMeters && currentBody.ParentBody != null` → `vessel.ReRootToBody(parentBody)`), inward check (iterate `BodyRegistry.GetChildrenOf(currentBody)`, first child with `distanceToChild < child.SoiRadiusMeters` wins). `BodyRegistry.cs` provides Guid-keyed `TryGetBodyById` and `GetChildrenOf` over a static `List<ReferenceBody>` with self-registration.

`Vessel.ReRootToBody` (`Vessel.cs:611-675`) re-propagates the current `KeplerState` to current tick (`:648`), then calls `OrbitalElements.ReRootStateVector` (`:654`) which transforms the state vector into the new body's frame and **produces a fresh `KeplerState`**. Orbital elements ARE recomputed for the new parent. Cached `_referenceBody` updates (`:667`) and event queue entries are invalidated (`:674`). EditMode tests at `VesselSoiRerootingDriverTests.cs` confirm the driver works with an Earth-Moon helper (`BuildMoonAsChildOfEarth`).

**Observable behavior:** In TestVessels.unity (single-body, infinite-SOI Earth), the driver iterates each tick, finds no children, no crossings, and does nothing. `EvaluationCount` ticks up; `RerootingCount` stays at 0. No re-rooting is observable in Play without a multi-body scene.

**Gaps:** No multi-body Play scene exists. End-to-end Play verification deferred per PHASE_TRACKER line 63 — "constructing an Earth-Moon scene in TestVessels.unity or a new TestSoi.unity scene is deferred to a future verification commit". Phase 4+ velocity-frame hazard documented at `Vessel.cs:598-603` (re-rooting math assumes stationary bodies — fine for Phase 1).

**Test recommendation:** Build a TestSoi.unity (or extend TestVessels) with Earth + Moon hierarchy (Earth finite `SoiRadiusMeters`, Moon child with finite SOI, Moon parented to Earth in Inspector). Put a vessel on a Hohmann transfer crossing into Moon SOI, watch the console for the SoiRerootingDriver log at the inward-crossing tick. This is the only Phase 1 system with fully working code that lacks Play verification.

---

### System 7 — Physics mode transitions (PhysXActive / KeplerRails / InterstellarCruise)

**Status:** PARTIAL for PhysX↔Kepler (infrastructure complete, automatic dispatch gated). ABSENT for InterstellarCruise direction.

**Implementation summary:** `VesselTransitionDriver.cs:59` — `public static bool Enabled = false;`. The early return at `:166` makes the entire dispatch loop a no-op in production. The driver IS subscribed to `TickAdvanced` (`:104`) regardless of the flag — infrastructure path is live, only dispatch is gated.

`TransitionEvaluation.cs:64-139` defines 9 trigger reasons. Per-direction:

- **PhysXActive → KeplerRails:** ONE conjunctive condition `BeyondProximityWithCleanState` (`Vessel.cs:835-850`). All 5 must hold: proximity (real, `:923-926`), no thrust (reads stub `ActiveThrustN`, always 0, `:975-979`), no atmospheric drag (reads stub `AtmosphericDensity`, always 0, `:993-997`), no contact (always false stub, `:1014-1018`), well-defined trajectory (real: `_referenceBody != null`, `:1029-1032`). Since 4 of 5 are always-pass stubs, the only meaningful gate is proximity — which is exactly why `Enabled = false`.

- **KeplerRails → PhysXActive:** SIX disjunctive triggers, first-match-wins (`Vessel.cs:856-910`). `ProximityToActiveVessel` (real), `AtmosphericEntryPredicted` (real predictor wiring, `:867 + :1047-1057`), `SurfaceImpactPredicted` (real predictor wiring, `:878 + :1073-1083`), `PlayerFocusSwitch` (stub always false, `:1093-1097`), `ScriptedThrust` (stub, `:1105-1109`), `MultiVesselProximityCluster` (stub, `:1120-1124`). `WarpRateForcedRails` is enum-only (commit 048 Stage 1; "wired up in Stage 3" per `TransitionEvaluation.cs:138` — Stage 5 amendment formally defers it to a future commit).

- **InterstellarCruise (any direction):** Phase 6 scope. ALL operations rejected with error logs (`Vessel.cs:179-186, :360-365, :469-474, :822-824`). `KeplerPropagator.cs` doesn't handle cruise. `VesselSoiRerootingDriver.cs:155` and `VesselEventPredictionDriver.cs:187` skip cruise vessels. No trigger condition exists anywhere for entering cruise mode. The only non-rejecting usage is `WarpController.cs:87, :216` defining the 100,000× warp ceiling constant — a pure lookup.

Dispatch at `VesselTransitionDriver.cs:236-254` calls `TransitionToKeplerRails()` (`Vessel.cs:348-438`) or `TransitionToPhysXActive()` (`:457-572`). Both transition methods are real, fully implemented, and exercise the full §3.1 procedure (Kepler-elements computation, rigidbody add/remove, anchor add/remove, event-queue invalidation).

**Observable behavior:** In Play with `Enabled = false` (default), no automatic transitions ever fire. `TestVesselDriver`'s Space-key handler invokes `TransitionToKeplerRails` / `TransitionToPhysXActive` directly via the imperative API, bypassing the evaluator entirely. PHASE_TRACKER confirms Space-key transitions verified working in Play after commit 039.

**Gaps:**
- Disabled-by-default dispatch — full activation deferred until thrust simulation (Phase 3), atmospheric density (Phase 5), contact detection (Phase 3+), focus subsystem (Phase 5), and scripting (Phase 5) populate the schema fields with real values.
- `WarpRateForcedRails` enum value exists with no firing condition.
- `InterstellarCruise` is entirely Phase 6 scope.

**Test recommendation:** Play-test the imperative path in TestVessels.unity: press Space to transition PhysXActive → KeplerRails, observe rigidbody removal and orbital motion, press Space again for KeplerRails → PhysXActive with correctly propagated position. This is the validated path. **Do NOT enable `VesselTransitionDriver.Enabled = true` for Play verification** — the always-pass stubs (`ActiveThrustN == 0`, `AtmosphericDensity == 0`, `HasContactForces() == false`) plus the proximity check would produce immediate unwanted transitions on any non-active vessel >50km away. Driver-on testing belongs in EditMode where states are controlled.

---

### System 8 — Reference body system

**Status:** WORKING (single-body scope only)

**Implementation summary:**
- `ReferenceBody.cs`: MonoBehaviour with full Phase 1 schema — `BodyId` (Guid, auto-assigned, `:283-286`), `MassKg` (default 5.972e24, `:60`), `SoiRadiusMeters` (default `double.PositiveInfinity`, `:81`), `SurfaceRadiusMeters` (default 6.371e6, `:106`), `AtmosphericTopAltitudeMeters` (default 0.0 = vacuum, `:142`), `ParentBody` Inspector wiring + `ParentBodyId` Guid (`:165, :172, :180`), `PositionWorld` captured once at `Awake` via `FloatingOriginManager.LocalToWorld` (`:332`), and `Mu = G·M` (`:211`). Self-cycle parent wiring rejected with `Debug.LogError` (`:292-301`). Self-registers with `BodyRegistry` on Awake (`:346`).
- `BodyRegistry.cs`: static class (rationale at `:14-26`). API: `RegisterBodySafe` (`:45`), `UnregisterBodySafe` (`:55`), `TryGetBodyById(Guid)` with Guid.Empty sentinel (`:70-87`), `GetChildrenOf(parent)` O(N) iteration (`:105`), `ClearForTesting` (`:123`).
- Positions are statically captured at `Awake` — explicit Phase 1 limitation. `ReferenceBody.cs:185-198` calls out bodies "do not move during Phase 0 / Phase 1"; orbital motion is Phase 4+.

**Scene content — TestVessels.unity has exactly ONE ReferenceBody:**
- Single GameObject `m_Name: ReferenceBody` (`TestVessels.unity:3372`)
- Inspector: `massKg: 5.972e+24` (Earth), `soiRadiusMeters: Infinity` (top-level), `surfaceRadiusMeters: 6371000`, `atmosphericTopAltitudeMeters: 0` (vacuum), `parentBody: {fileID: 0}` (null).

**No Earth+Moon scene exists.** Only two `.unity` files in `Assets/`: `Scenes/TestVessels.unity` and `_Recovery/0.unity`. SOI re-rooting (commit 044) is implemented but never exercised in the only Play scene.

**Observable behavior:** A vessel sits forever inside the single body's infinite SOI. The SOI re-rooting driver runs every tick and silently finds no crossings. Atmospheric and surface-impact predictors are wired but the body is vacuum + has no atmosphere top, so atmospheric entry never fires. Surface impact CAN fire (radius 6371 km) if the vessel's orbit intersects the surface.

**Gaps:** Multi-body validation impossible end-to-end without a second body. Bodies don't orbit.

**Test recommendation:** For TestVessels.unity, only surface-impact prediction is observable (set initial velocity low enough that orbit periapsis < 6371 km). For multi-body validation, build a manual test scene (e.g., Earth + Moon: child body with finite SOI, parented to Earth in Inspector).

---

### System 9 — Coordinate system and floating origin

**Status:** WORKING

**Implementation summary:**
- `WorldPosition.cs`: readonly struct wrapping `double3` (`:28`), explicit conversions only, +/- operators take `double3` deltas (`:36-45`), `Zero = default` (`:33`), distance + equality (`:51-57`).
- `LocalPosition.cs`: readonly struct wrapping `Vector3` single-precision (`:25`), +/- accept `Vector3` deltas only — mixing world/local is a type error by design.
- `FloatingOriginManager.cs`: singleton MonoBehaviour. **Threshold field at `:53-54`** — `[SerializeField] private double shiftThresholdKm = 50.0`. Exposed as meters via `ShiftThresholdMeters => shiftThresholdKm * 1000.0` (`:57`). Scene value confirmed: `TestVessels.unity:2680` reads `shiftThresholdKm: 50`.
- **Rebase logic in `MaybeShiftOrigin` (`:249-283`)**: calls `CoordinateMath.ShouldShift` first (`:251-252`), then computes delta, updates `CurrentOrigin` BEFORE notifying listeners (`:258`, deliberate so listeners can use new origin), increments `ShiftCount`, synchronously calls each `IFloatingOriginListener.OnFloatingOriginShifted(shiftDelta)` over a snapshot copy (`:264-277`), then raises the `OriginShifted` Action event (`:280`). Each listener wrapped in try/catch (`:267-276`).
- **Shift trigger formula** in `CoordinateMath.cs:75-79`: `distSq > thresholdMeters * thresholdMeters` (strict >; at exactly 50km no shift).
- `FloatingOriginAnchor.cs`: per-vessel `IFloatingOriginListener`. Registers in `OnEnable` via deferred-registration path (`:75`). `OnFloatingOriginShifted` (`:88-111`) does `_rb.position -= delta` if Rigidbody present (PhysX teleport), else `transform.position -= delta`.
- Dispatch driven from `SimTickController.Step6_DetectModeTransitions` (`SimTickController.cs:472-508`), calling `FloatingOriginManager.Instance.MaybeShiftOrigin(ActiveVessel.GetWorldPosition())` once per FixedUpdate (gated `i == 0` per `:339-343`).

**Observable behavior:** In TestVessels.unity, the vessel launches at 10 km/s +X. It crosses the 50 km threshold in ~5 seconds (sim time). Diagnostic UI ticks `Shift count` up by one, vessel visibly snaps back to local origin while Rigidbody velocity persists. World position diverges from local in the diagnostic readout.

**Gaps:** None for the foundation feature itself. PhysX joint/articulation behavior across shifts is explicitly deferred (`FloatingOriginAnchor.cs:40-56`) but irrelevant for single-vessel Phase 1.

**Test recommendation:** Press Play, confirm `Shift count` increments roughly every ~5s; confirm vessel `transform.position` cycles near zero after each shift; confirm `World:` line in diagnostic UI keeps growing monotonically (double-precision).

---

### System 10 — Save/load

**Status:** ABSENT

**Implementation summary:** None. Exhaustive grep across `Foundation/` for `JsonUtility`, `JsonSerializer`, `BinaryFormatter`, `System.Runtime.Serialization`, file-write-to-.json, etc. returns **zero hits**. Grep for bare tokens `Serialize` / `Deserialize` / `SaveGame` / `LoadGame` / `SaveLoad` / `ISavable` / `IPersist` returns **zero matches**. The 10 files matching the broader regex all hit it via `[SerializeField]` (Unity Inspector attribute, not save/load) or via tooltip text mentioning "save-load" as future work.

The only `Application.persistentDataPath` reference is one `Debug.Log` in `PrototypeStartupTest.cs:26` — a startup smoke print, no actual file I/O.

**PHASE_TRACKER status:** Line 24 calls save/load "the parallel track"; line 116 lists it bare as `Save/load format.` with no completion annotation (every other Phase 1 item is annotated with `(complete, commit NNN — ...)`). PHASE_TRACKER confirms ABSENT.

**Note on existing infrastructure that save/load WILL consume:** `ReferenceBody.ParentBodyId` (Guid) is explicitly designed for save-load reconstruction (`:175-180`), and `BodyRegistry.TryGetBodyById` is intended to re-resolve cached references on load. The hooks are in place; no writer/reader exists.

**Observable behavior:** Nothing — no UI, no menu, no hotkey, no automatic write. Quitting Play discards all sim state.

**Gaps:** Entire feature. No format spec, no writer, no reader, no version field, no migration story, no quicksave hotkey.

**Test recommendation:** Skip from Phase 1 validation. Confirm with Grayson whether save/load belongs in Phase 1 validation scope at all — it's listed in Phase 1 system list per PHASE_TRACKER but flagged as "parallel track". No play-test is possible.

---

### System 11 — Input system

**Status:** PARTIAL (one input: Space-bar mode toggle; nothing else)

**Implementation summary:**
- `TestVesselDriver.cs:6` imports `UnityEngine.InputSystem`. The only input handler in the entire Foundation codebase is at `:162-174`: `Keyboard.current.spaceKey.wasPressedThisFrame` toggles between PhysXActive and KeplerRails by calling `_vessel.TransitionToKeplerRails()` or `_vessel.TransitionToPhysXActive()`.
- Grep across Foundation for `UnityEngine.InputSystem | InputAction | Input.GetKey | Keyboard.current | Mouse.current` returns hits only inside TestVesselDriver.cs.
- `TestShiftDriver.cs` at `Coordinates/` is an empty stub file (file moved to SimTick/ in commit 033); the replacement in SimTick/ has no input bindings either.
- No `InputAction` assets, no `PlayerInput` components, no input action maps anywhere under Foundation/.
- Active vessel selection: `SimTickController.SetActiveVessel(IActiveVessel)` (`:202`) called exactly once at startup by `TestVesselDriver.Start` (`:108`). No input path or runtime UI to switch between vessels.
- Vessel controls (thrust direction, orientation, throttle): none wired anywhere.

**The WarpUIController IS button-based**, not keybind-based — see System 3. Mouse-click on the on-screen Canvas buttons / slider is the warp input path.

**Observable behavior:** In TestVessels.unity, pressing Space toggles the vessel's mode. The diagnostic Text label updates each frame. Mouse-clicking the WarpUI buttons changes warp rate. No other input does anything.

**Gaps:** Throttle, orientation, multi-vessel selection, camera control, pause keybind, time-warp keybinds. All flight controls.

**Test recommendation:** Verify Space-bar mode toggle works once (Console will print the "PHASE 0 LIMITATION" log on first PhysX→Kepler transition per `TestVesselDriver.cs:195`); diagnostic label reflects toggled mode; no other input does anything. WarpUI buttons handled by mouse-click on the Canvas.

---

### System 12 — Active vessel concept

**Status:** PARTIAL (operationally narrow; severe visual gap)

**Implementation summary:**
- `IActiveVessel.cs:35-49`: two members only — `WorldPosition GetWorldPosition()` and `PhysicsMode Mode { get; }`. Doc explicitly states the interface is narrow to break a circular asmdef dependency.
- `SimTickController.ActiveVessel` property (`:105`) holds the current `IActiveVessel`. `SetActiveVessel(IActiveVessel vessel)` (`:202-207`) assigns it and propagates the vessel's `Mode` to `WarpController.Instance?.SetActiveVesselMode` (null-safe). `Step6_DetectModeTransitions` (`:472-508`) reads `ActiveVessel.GetWorldPosition()` for the floating-origin shift check and re-syncs warp-controller mode every tick (`:505-507`).
- `VesselRegistry.cs`: static registry of ALL `Vessel`s, separate from the active-vessel slot.
- `TestVesselDriver.cs:106-116`: in `Start`, calls `SimTickController.Instance.SetActiveVessel(_vessel)` exactly once. No mid-Play re-call, no switching.

**Operationally, "active vessel" means TWO things and only two things:**
1. Its `GetWorldPosition()` is the input to the floating-origin shift threshold check.
2. Its `Mode` drives the warp-controller's ceiling.

**Active vessel does NOT route any input.** `TestVesselDriver.Update` (`:147-181`) reads `Keyboard.current.spaceKey.wasPressedThisFrame` and acts directly on `_vessel` (Inspector-wired reference, not `SimTickController.ActiveVessel`). The Space-key toggle would still work on a non-active vessel.

**No camera-follow code exists anywhere.** Glob `**/*Camera*.cs` under `Assets/Scripts/` returns no files. Grep for `Camera` / `LookAt` / `FindObjectOfType<Camera` across Foundation returns no matches. The Main Camera in TestVessels.unity is the default Unity scene camera — stationary at the editor-placed position. **Once the vessel translates out of view (~seconds at 10 km/s, well before the first origin shift) it leaves the camera frustum and is invisible.**

**No runtime switch mechanism.** `SimTickController.SetActiveVessel` is public, but no UI/input/code path calls it after `TestVesselDriver.Start`.

**Observable behavior:** In Play, the diagnostic UI shows the active vessel's `World` and `Local` coords; floating origin tracks that vessel. Space toggles that vessel's mode. When Kepler-rails, position freezes visually (Phase 0 limitation log per `TestVesselDriver.cs:192-202`). Camera doesn't move; vessel leaves view almost immediately.

**Gaps:**
- **No camera-follow** — the most significant visual-validation usability gap in the audit. Vessel disappears off-screen within seconds.
- No active-vessel-switch UI/input.
- Input is keybind-direct to driver, not routed through the active-vessel concept.

**Test recommendation:** Rely on the Canvas diagnostic UI for ALL feedback during Play (the visible sphere will leave the camera frustum quickly). Confirm diagnostic shows shifts firing, tick counter advancing, Space toggling mode. Do NOT expect to see the vessel after the first few seconds. **A simple camera-follow MonoBehaviour reading `SimTickController.Instance.ActiveVessel.GetWorldPosition()` would be ~30 lines and would dramatically improve Play-mode validation.**

---

## Section A — End-to-end Phase 1 flight feasibility

**Can Grayson fly launch → orbit → warp → reentry → land in `TestVessels.unity` right now? No.**

But the answer maps to design intent, not a bug:

| Leg | Feasibility | Reason |
|---|---|---|
| **Launch (thrust)** | Phase 3 | No thrust application code (System 1 ABSENT). Engine simulation is Phase 3 per PHASE_TRACKER. |
| **Orbit (Kepler-rails)** | Working | KeplerPropagator + transition (commits 040, 038–043). System 2 WORKING. |
| **Warp** | Working | WarpController + UI complete (commit 048). System 3 WORKING. |
| **Reentry (atmospheric drag)** | Phase 3 + Phase 5 | No drag, no density, no heating. Only entry-tick prediction. System 4 PARTIAL (predictor-only). |
| **Land (collision/destruction)** | Phase 3+ | No Colliders, no OnCollisionEnter, no rest-on-surface logic. Only impact-tick prediction. System 5 PARTIAL (predictor-only). |

**The Phase 1 validation envelope that DOES work end-to-end** in `TestVessels.unity`:

1. Vessel coasts ballistically at initial velocity (no thrust needed).
2. Press Space → vessel transitions to Kepler-rails, propagates exactly via closed-form math.
3. Time-warp at 5x (PhysX) or 10000x (Kepler) — full UI, slider, discrete buttons all working.
4. Floating-origin shifts every ~5s at 10 km/s — observable via `Shift count` field in diagnostic UI.
5. If vessel orbit has periapsis below 6371 km, the SurfaceImpactPredictor will register a halt and warp will stop at the imminent impact tick. (Vessel then keeps propagating through the surface — landing is Phase 3+.)
6. Press Space again → vessel transitions back to PhysX-active with propagated position (commit 040 round-trip).

**Minimum subset of gaps to fill** for full Phase 1 validation as PHASE_TRACKER understands it (i.e., validating the foundations, NOT a full flight):

1. **Camera-follow MonoBehaviour** (~30 lines, no design needed). Reads `SimTickController.Instance.ActiveVessel.GetWorldPosition()` each LateUpdate, sets Camera transform to follow. Without it, all visual validation depends on the diagnostic UI. (Single-session cost, large UX payoff.)

2. **Multi-body Play scene** (Earth + Moon) — either extend `TestVessels.unity` or create `TestSoi.unity`. Enables Play-mode validation of three deferred-end-to-end Phase 1 items per PHASE_TRACKER line 63: SOI re-rooting, SOI-crossing halt, multi-body event prediction. EditMode tests already cover the math. (Single-session cost.)

3. **Finite-atmosphere body config** (set `AtmosphericTopAltitudeMeters > 0` on the Earth in the multi-body scene). Enables Play-mode validation of the atmospheric-entry halt. (Zero-line cost if applied to the multi-body scene above.)

After those three gaps fill, the full Phase 1 foundations layer can be end-to-end Play-validated. The full launch→land flight scenario remains gated by Phase 3 thrust + atmospheric flight + collision work, which is correct per the project's phase plan.

---

## Section B — Phase 1 validation work breakdown

### Critical (blocks any Phase 1 validation)

None. The Phase 1 foundations validate end-to-end within the constrained envelope listed in Section A.

### Important (blocks specific Phase 1 scenarios)

| Gap | Affected scenarios | Magnitude |
|---|---|---|
| **No camera-follow** | All visual play-mode validation (the vessel disappears from view in seconds). | **Small** — single MonoBehaviour, ~30 lines, reads `ActiveVessel.GetWorldPosition()` in LateUpdate. |
| **Single-body scene** | SOI re-rooting Play verification (commit 044 deferred end-to-end); SOI-crossing halt observation (commit 046); atmospheric-entry halt observation (commit 047). | **Small/medium** — extend `TestVessels.unity` with Moon child + finite atmosphere top OR build dedicated `TestSoi.unity`. The math is complete; just needs scene construction. |
| **Atmospheric-top scalar = 0** | Atmospheric-entry predictor returns null; halt never fires in default scene. | **Trivial** — Inspector value change once a multi-body scene exists. |
| **VesselTransitionDriver.Enabled default = false** | Trigger evaluator never dispatches automatically. | **Out of Phase 1 scope** — gate flips when Phase 3+ upstream state systems land. |

### Polish (works but feels wrong)

| Gap | Affected | Magnitude |
|---|---|---|
| **PhysX 5x is a passive cap, not a forced transition** | User experience: at 5x cap, vessel runs slow until manual mode toggle. Original Stage 1 spec called for force-transition. Stage 5 amendment formally defers. | **Medium** — separate future commit; enum value already held as infrastructure. |
| **No active-vessel switching mechanism** | Multi-vessel scenarios cannot switch which vessel is "active". | **Small** — runtime API exists (`SetActiveVessel`); just no UI/input path. |
| **Default vessel velocity (10 km/s) flies off-camera before first origin shift** | Visual validation muddled by combination of fast velocity + no camera follow. | **Trivial** — Inspector value change OR fixed by camera-follow. |

### Out of Phase 1 scope (defer to Phase 2+)

| System | Correct phase | Notes |
|---|---|---|
| Engine thrust | Phase 3 | System 1 ABSENT, intentionally. |
| Atmospheric drag + heating | Phase 3 + Phase 5 | System 4 PARTIAL (predictor-only), intentionally. |
| Collision detection + destruction | Phase 3 | System 5 PARTIAL (predictor-only), intentionally. |
| InterstellarCruise mode | Phase 6 | System 7 partial-direction ABSENT, intentionally. |
| Save/load | Phase 1 nominal but lives as "parallel track" per PHASE_TRACKER | System 10 ABSENT. Whether to bring forward depends on Grayson's call. |
| Player controls (throttle, attitude) | Phase 3 | System 11 PARTIAL, intentionally. |

---

## Section C — Recommended next-commit sequence

Three commit candidates, ordered by validation payoff per cost:

### Commit candidate 1 — Camera-follow MonoBehaviour + visual-validation pass

**Scope:** Single small MonoBehaviour (`Foundation/SimTick/ActiveVesselCameraFollow.cs` or similar) that reads `SimTickController.Instance.ActiveVessel.GetWorldPosition()` in `LateUpdate` and updates the Main Camera's local position to track the active vessel (with optional offset). No new tests needed (visual-only).

**Why first:** Single-session work; immediate UX payoff; unblocks visual play-mode validation of every other Phase 1 system. The diagnostic UI tells you the numbers; the camera follow tells you it *looks* right.

**Magnitude:** Small (~50 LOC including XML doc + Inspector field for offset).

### Commit candidate 2 — Multi-body test scene + Earth+Moon body wiring

**Scope:** Either extend `TestVessels.unity` with a Moon child body, OR create a parallel `TestMoon.unity` / `TestSoi.unity`. The body has finite SOI, parent wiring to Earth in Inspector, distinct Inspector-visible parameters. Optionally bump Earth's `AtmosphericTopAltitudeMeters` to a non-zero value so atmospheric-entry can be observed.

**Why second:** Unblocks the three deferred end-to-end Play verifications PHASE_TRACKER explicitly flags (SOI re-rooting, SOI crossing halt, atmospheric entry halt). Single-session work assuming Grayson can do the scene editing — computer-use can drive the Editor for the GameObject creation + Inspector wiring.

**Magnitude:** Small (~zero LOC; scene-edit + ReferenceBody Inspector wiring) to Medium (~150 LOC if a new TestVesselDriver-style harness is needed for the multi-body launch scenario).

### Commit candidate 3 — Phase 1 validation milestone artifact

**Scope:** Run the validation scenarios with the camera-follow + multi-body scene in place, record results (screenshots / Console output / tick-counter readings) into a `docs/phase1_validation_results.md` artifact. This is the documentation half of the milestone — proves the foundations work, formally closes the Phase 1 system list per the existing system-by-system completion annotations.

**Why third:** Captures the validation evidence durably; gives Phase 2+ work a clean handoff point. Cheap in code but valuable in project hygiene.

**Magnitude:** Small (documentation work; no code changes).

After these three commits land, the Phase 1 foundations layer is end-to-end Play-validated and durably documented. Save/load (the remaining Phase 1 system-list item) is the next major architectural commit, separately scoped.

---

## Audit methodology note

This audit is read-only. Four parallel general-purpose agents investigated focused subsets of the 12 systems via `Glob` / `Grep` / `Read` over the codebase, the planning docs (`CONSTRAINTS.md`, `NETCODE_CONTRACT.md`, `ARCHITECTURE.md`, `PHASE_TRACKER.md`), and the `TestVessels.unity` scene file. File:line citations are from HEAD `77dc8a0`. No code or scenes were modified.

The agents' assigned scopes were:
- Agent 1: Systems 1, 7-cruise-portion, 11 (thrust + cruise + input)
- Agent 2: Systems 2, 3 (orbital flight + time-warp)
- Agent 3: Systems 4, 5, 6, 7-transition-portion (atmospheric + surface + SOI + mode transitions)
- Agent 4: Systems 8, 9, 10, 12 (bodies + coordinates + save/load + active vessel)

Findings were synthesized into this report. Where agents reported conflicting framings, the more conservative (gap-flagging) interpretation was preserved.
