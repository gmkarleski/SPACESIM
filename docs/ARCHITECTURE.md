# Architecture

**Purpose:** implementation-level details that flow from the design commitments in `docs/CONSTRAINTS.md` and the contract in `docs/NETCODE_CONTRACT.md`. This document is the bridge between "what the game is" (constraints) and "how it's built" (code).

**Read this when:** implementing a new module, modifying an existing module's contracts, or trying to understand why a piece of code is shaped the way it is.

**Update discipline:** when implementation surfaces a real architectural decision (not just a coding choice), document it here. When the decision affects how other modules interact, also add it to `docs/DECISIONS.md`.

This document is intentionally a skeleton at commit 036 landing. It fills in as Phase 1+ implementation work surfaces real architecture worth documenting.

---

## 1. Module layout

The Unity project is organized into modules under `SPACESIM/Assets/Scripts/Foundation/`. Each module is its own assembly definition (.asmdef) so changes recompile only the affected module.

### 1.1 Current modules

| Module | Status | Purpose | Asmdef |
|---|---|---|---|
| Coordinates | Complete (commit 029) | Double-precision world coordinates, floating origin, shift dispatch | `SpaceSim.Foundation.Coordinates` |
| SimTick | Complete (commit 033); event priority queue added (commit 045) | 30 Hz sim-tick controller, 10-step cycle, warp controller, event priority queue | `SpaceSim.Foundation.SimTick` |
| Vessels | In progress (commits 038-047) | Vessel containers + ReferenceBody hierarchy + Kepler propagator + mode transitions + SOI re-rooting + five predictors (periapsis/apoapsis, SOI crossing, atmospheric entry, surface impact) | `SpaceSim.Foundation.Vessels` |

### 1.2 Planned modules

Per the netcode contract and CONSTRAINTS §9 build order, the remaining foundation modules expected during Phase 1+:

- `Save` — save/load format implementing the authoritative state schema

The original "Planned modules" list also included separate `Physics` and `Time` modules. Both have been absorbed into existing modules rather than landing as standalone folders:

- **Three-mode physics architecture** (PhysX-active / Kepler-rails / interstellar-cruise) lives in `Vessels/` (the vessel container holds the mode state and `KeplerState`; mode transition triggers and procedures are vessel methods) and `SimTick/` (the warp controller respects mode-specific warp ceilings). No standalone `Physics/` module exists. An earlier `Physics/` folder placeholder with only `.gitkeep` was removed in the cleanup commit following commit 047.
- **Time-warp controller integration** lives in `SimTick/` (`SimTickWarpController`, `EventPriorityQueue`). Event-prediction queue is owned by `SimTickController` per CONSTRAINTS §2 ("Authority over the queue lives in the sim-tick controller"). No standalone `Time/` module exists.

### 1.3 Module dependency direction

Dependencies flow strictly downward through the foundation:

```
Vessels → SimTick → Coordinates
                        ↑
   (Save) refers to all module states for serialization
```

The `IActiveVessel` interface (in `SimTick`) breaks what would otherwise be a Vessels↔SimTick cycle: SimTick code that needs to query vessel state takes `IActiveVessel`, and `Vessel` implements it. Drivers in Vessels (`VesselTransitionDriver`, `VesselSoiRerootingDriver`, `VesselEventPredictionDriver`) subscribe to `SimTickController.TickAdvanced` from outside the controller, preserving the asmdef direction.

A second interface, `IVessel` (in `Vessels`), narrows the per-tick driver→vessel contract for testability rather than for cycle-breaking. `VesselEventPredictionDriver.PredictAndUpdate` consumes `IVessel` so POCO test fakes can exercise its per-predictor dispatch without constructing real Vessel MonoBehaviours. See §3.4 for the contrast between the two interface abstractions.

Lower modules never reference higher modules. Tests that need to cross module boundaries (e.g., SimTick tests verifying integration with Coordinates) live in the higher module's Tests folder and reference both.

---

## 2. Asmdef configuration patterns

Two distinct asmdef forms in use, each with a specific role:

### 2.1 Runtime asmdef (modern verbose)

For runtime code (Coordinates, SimTick). Explicit dependencies, no `autoReferenced` bet (see DECISIONS.md):

```json
{
    "name": "SpaceSim.Foundation.<Module>",
    "rootNamespace": "SpaceSim.Foundation.<Module>",
    "references": ["<explicit dependencies>"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### 2.2 EditMode test asmdef (modern verbose, Editor-only)

For pure-logic tests. `includePlatforms: ["Editor"]` is what makes them EditMode-only.

```json
{
    "name": "SpaceSim.Foundation.<Module>.Tests",
    "references": ["<module>", "Unity.Mathematics", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": [],
    "noEngineReferences": false
}
```

### 2.3 PlayMode test asmdef (Form 1 canonical)

For tests that need Unity's MonoBehaviour lifecycle. Uses `optionalUnityReferences: ["TestAssemblies"]` (canonical Unity pattern from sample asmdefs):

```json
{
    "name": "SpaceSim.Foundation.<Module>.PlayModeTests",
    "references": ["<module>", "Unity.Mathematics"],
    "optionalUnityReferences": ["TestAssemblies"],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

The asymmetry between EditMode (Form 2 verbose) and PlayMode (Form 1 canonical) is intentional. Each form is what works reliably for its test mode. See DECISIONS.md entries on Form 1/Form 2 for rationale.

---

## 3. State management patterns

### 3.1 Singleton MonoBehaviour managers

Project-wide managers (FloatingOriginManager, SimTickController) use the singleton MonoBehaviour pattern with deferred-registration support for listeners. Per DECISIONS.md.

Common structure:
- `public static <Manager> Instance { get; private set; }`
- `Awake` claims Instance; logs error and destroys self if duplicate
- `OnDestroy` clears Instance if this was the active one
- Static pending-listeners queue for deferred registration
- `Awake` drains the pending queue after claiming Instance
- Static `RegisterListenerSafe` / `UnregisterListenerSafe` facade methods
- Test-only `ClearInstanceForTesting()` and `DrainPendingForTesting(instance)` static methods

### 3.2 Dual listener dispatch

Event-emitting subsystems expose both an interface listener model and a plain C# event. Listeners that need hot-path performance implement the interface; ad-hoc subscribers use the event.

### 3.3 Coordinate space types

Two coordinate spaces with non-interchangeable types:
- `WorldPosition` wraps `double3`; lives in galactic coordinates
- `LocalPosition` wraps `Vector3`; lives in floating-origin-relative coordinates

No implicit conversions. Named methods (`WorldToLocal`, `LocalToWorld`) require the current origin as an explicit parameter, so the math is testable without instantiating a manager.

### 3.4 Interface abstractions for drivers

Two distinct interfaces narrow what driver code knows about vessels. They look superficially similar (both have `Mode` and `GetWorldPosition()`) but serve different architectural purposes.

`IActiveVessel` (in `SimTick`) breaks an asmdef cycle. SimTickController's step 6 needs to query the active vessel's position (for floating-origin shift detection) and mode (for warp-ceiling selection), but `Vessel` lives in the higher Vessels asmdef. The interface lets SimTick consume the contract without referencing Vessels. Members: `Mode`, `GetWorldPosition()`. Implementers: production `Vessel`, plus POCO stubs in SimTick's own tests.

`IVessel` (in `Vessels`) narrows the per-tick driver→vessel contract for testability. Inside `VesselEventPredictionDriver.PredictAndUpdate`, the driver iterates over Kepler-rails vessels and dispatches predictors; the contract it actually needs is read-only and small (Mode + State + ReferenceBody + GetWorldPosition + DiagnosticName). Typing the inner method against `IVessel` enables POCO test fakes that exercise the predictor-dispatch logic without constructing real Vessel MonoBehaviours.

The two interfaces are independent (no inheritance). Inheriting one from the other would force SimTick's asmdef into the declaration site of the other interface for no operational gain. `Vessel` implements both interfaces directly; the shared members (`Mode`, `GetWorldPosition()`) are satisfied by a single concrete implementation each.

**Scope discipline (`IVessel`).** The interface is deliberately read-only — no mode-transition methods, no SOI re-root, no schema mutations. Mutating lifecycle operations belong on the concrete `Vessel` type, where Unity-component-shape changes can actually happen. `VesselEventPredictionDriver` lifts cleanly onto `IVessel` because its mutations write to schema fields that the interface exposes (`State.KeplerState.NextPeriapsisTick`, etc.) and to `SimTickController.EventQueue` (not on the vessel itself). `VesselSoiRerootingDriver` does not lift, because its inner method calls `vessel.ReRootToBody(parentBody)` — a mutating lifecycle method that's incompatible with the read-only contract. The SOI re-rooter stays on concrete `Vessel`; a future commit may split its detect-vs-dispatch concerns if SOI-rerooter test needs justify it. `VesselTransitionDriver` similarly stays on concrete `Vessel` because `TransitionToKeplerRails` / `TransitionToPhysXActive` are even more mutating (they add and destroy Unity components).

**Inner-method-only migration pattern.** Where `IVessel` is used, the outer `OnTickAdvanced` iteration loop stays typed against concrete `Vessel` (the iteration boundary uses Unity-null semantics from `VesselRegistry.Vessels`), and the inner per-vessel method takes `IVessel`. The implicit upcast happens at the call site. This asymmetry is deliberate: outer iteration is production-Unity territory; inner per-vessel logic is the surface that wants test fakes. Catch-block diagnostics follow the same split — outer catch uses `vessel.gameObject.name` (concrete-Vessel context), inner catches use `vessel.DiagnosticName` (IVessel context).

---

## 4. The sim-tick boundary

The architectural heart of the netcode contract. Implemented in commit 033.

### 4.1 10-step cycle

Per `docs/NETCODE_CONTRACT.md` §1.2. The SimTickController.RunFixedUpdateCycle method orchestrates the cycle. Each step is a private instance method named `Step1_...` through `Step10_...` for direct cross-reference to the contract.

PhysX-touching steps (1, 2, 3, 7, 8) run once per FixedUpdate regardless of time-warp. Analytic steps (4, 5) loop N times per FixedUpdate where N is the time-warp-derived iteration count. Step 6 (mode transitions) runs once per FixedUpdate gated by `if (i == 0)` inside the loop. Step 10 (counter advance) runs N times to match analytic iterations.

### 4.2 Time-warp

Hybrid approach. PhysX-active steps stay at 1x; analytic steps scale with warp rate. Per-mode ceilings: PhysX-active 1x, Kepler-rails 10,000x, interstellar-cruise 100,000x. Iteration count gated by `min(warp_rate, ticks_until_next_event)`; event queue is currently empty so the cap is effectively just the warp rate.

### 4.3 Frame rate independence

Sim-tick is fixed 30 Hz. Rendering runs at frame rate. Display interpolation between sim-tick states produces smooth visuals; authoritative state remains at sim-tick boundaries only.

### 4.5 Time-warp controller pattern

Time-warp infrastructure (lands in commit 048, scaffolding in Stage 1) follows a singleton-MonoBehaviour-plus-event-bus pattern parallel to `FloatingOriginManager` and `SimTickController`.

**Singleton ownership.** `WarpController` is a singleton MonoBehaviour in `Foundation/SimTick/` that owns the current warp rate. The rate is represented as a rational number — `WarpRate` is a `readonly struct` carrying `(Numerator, Denominator)`. Rational representation is chosen over float multipliers because float multipliers accumulate precision error over long warp sessions (a 10,000x rate held for hours of real time produces sim-tick advancement errors that compound); rational arithmetic keeps the rate exact for deterministic sim-tick advancement. In v1 the denominator is always 1 (integer-only continuous mode, discrete steps {1, 5, 10, 100, 1000, 10000, 100000} plus continuous integers in [1, 1000]); the rational infrastructure is wired up now so future fractional modes (tenths, quarter-steps) can use it without schema or type churn.

**Event-bus halt surfacing.** When the controller halts warp due to an upcoming event (mode transition predicted within the next tick, scheduled burn, atmospheric entry on a non-routine vessel, etc.), it raises an `Action<WarpHaltInfo>` event. UI elements that need to know about halts subscribe to this event independently — Mission Control, the warp-rate HUD, audio cues. This matches the existing `SimTickController.TickAdvanced` pattern: loose coupling between the controller and its consumers, multiple subscribers supported, no controller knowledge of how the halt is surfaced to the player.

**PhysX threshold at 5x via forced KeplerRails transitions.** When warp rate exceeds 5x while a vessel is in `PhysicsMode.PhysXActive`, the controller signals the vessel transition driver to force-transition the vessel to `KeplerRails` mode. The 5x threshold matches a KSP-tested number — empirically the point at which PhysX simulation accuracy degrades enough that continuing produces unphysical behavior. Forced transitions flow through `VesselTransitionDriver` via the new `TransitionTriggerReason.WarpRateForcedRails` enum value (added Stage 1, wired Stage 3); the time-warp controller does NOT call `Vessel.TransitionToKeplerRails` directly. This preserves the architectural pattern that all mode changes flow through the driver — no shortcut bypasses, no second mode-change code path to keep in sync.

**Atmospheric/surface trigger field split (commit 048 Stage 1).** Per §2.3 of the netcode contract, atmospheric entry and surface impact each get their own `KeplerState` field (`NextAtmosphericEntryTick` and `NextSurfaceImpactTick`). The trigger evaluator reads both fields independently and fires distinct `TransitionTriggerReason` values. This is a precondition for routine-supply policy: routine-supply vessels (the new `Vessel.IsRoutineSupply` flag) skip warp halts on atmospheric entry — atmospheric re-entry is expected, repetitive, non-terminal for supply runs — but still halt on surface impact (mass loss is terminal). Without the field split, the controller couldn't distinguish the two events at halt-decision time.

**Asmdef placement.** `WarpController`, `WarpRate`, and `WarpHaltInfo` all live in the `SimTick` asmdef. No new cross-module dependencies; the controller subscribes to its own `SimTickController.TickAdvanced` event, the field-split fields live on `KeplerState` in `Vessels`, and the forced-transition dispatch goes through the existing `VesselTransitionDriver` driver pattern. The diagram from §1.3 (`Vessels → SimTick → Coordinates`) stays unchanged.

---

## 5. Coordinate system and floating origin

Implemented in commit 029.

### 5.1 Floating origin shift

Triggered when active-vessel world position exceeds 50 km from current origin (strict greater-than). Origin snaps to active vessel; all anchored listeners receive shift delta and adjust their local positions.

For rigidbodies, the shift path uses `Rigidbody.position -= delta` to ensure PhysX sees the teleport at a known sim-tick boundary. The shift currently dispatches from Step 6 of the sim-tick cycle (FixedUpdate cadence).

### 5.2 Test scene infrastructure

`TestCoordinates.unity` is the end-to-end test scene exercising:
- Coordinate system (WorldPosition arithmetic, conversion)
- Floating origin shifts at 50 km threshold
- Sim-tick boundary dispatching shifts
- Deferred listener registration (anchor wakes before manager, still gets shifts)
- Rigidbody-path teleport at sim-tick boundary

The scene must be manually populated on first project clone (see commit 033 artifact for steps). Empty scene files are committed but GameObject contents are user-authored.

---

## 6. Verification patterns

### 6.1 Verification battery per commit

Every implementation commit includes a verification battery checking:
- File-level correctness (files exist with expected content/structure)
- Compilation cleanly (no new errors)
- Unit tests (EditMode + PlayMode as applicable)
- Preserved-content anchors (rules 1, 5, 6 from `commits/README.md`)

### 6.2 End-to-end Play verification

Per commit 034, architectural commits with test scenes also get end-to-end Play verification. Open the test scene, press Play, observe expected behavior, confirm no unexpected warnings/errors in Console.

This is a standing verification category, not a one-off check.

### 6.3 Cross-tool divergence checks

Per workflow rule 6 (commit 035), when verification produces unexpected results or sandbox view appears stale, verify with byte-level reads (`wc -c`, `stat`, `xxd | tail`) before reporting verification success.

---

## 7. Open architecture questions

Items where implementation will need to make architectural decisions, surfaced for visibility:

### 7.1 Multi-vessel cluster activation

The netcode contract specifies cluster activation when multiple Kepler-rails vessels come within 50 km. Implementation decisions deferred to when vessels exist:
- Cluster centroid as floating origin vs single dominant vessel
- Activation/deactivation hysteresis to prevent thrashing at cluster boundaries
- Authority handoff between machines when multiplayer-active

### 7.2 Save format implementation

The schema is defined in netcode contract §2. The actual serializer choice (Unity JsonUtility, Newtonsoft, custom binary) is open. Will surface when save module begins.

### 7.3 Event queue performance

The contract specifies an event-prediction queue. Performance with thousands of events scheduled is an open implementation question. Profile when content density grows enough to test it.

### 7.4 Physics.Simulate disable during floating-origin shifts

The current FloatingOriginAnchor implementation trusts Unity's FixedUpdate ordering to make the rigidbody teleport atomic from PhysX's perspective. Multi-vessel scenes with active joints may surface edge cases requiring `Physics.Simulate(0)` or similar during the shift. Open question until multi-vessel scenes exist.

---

## 8. How this document maintains itself

This document is intentionally a skeleton at commit 036 landing. As Phase 1+ implementation work surfaces real architectural decisions, sections fill in.

Updates land in the same commit that produces the underlying change. When a section grows substantially (multiple-page level), consider splitting it into its own document under `docs/architecture/` and linking from here.

When implementation reveals that a section's content has become stale (e.g., the asmdef patterns from commits 030-032 get superseded by a Unity update changing best practices), update the section in place rather than leaving stale guidance.
