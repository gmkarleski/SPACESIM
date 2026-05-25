using System;
using UnityEngine;

namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Singleton <see cref="MonoBehaviour"/> that owns time-warp state per
    /// <c>docs/NETCODE_CONTRACT.md</c> §1.4 and <c>docs/DECISIONS.md</c>
    /// "Time-warp controller architecture (commit 048)".
    ///
    /// <para>
    /// <strong>RATIONAL RATE REPRESENTATION.</strong> The current warp rate is a
    /// <see cref="WarpRate"/> rational (numerator / denominator) — not a
    /// floating-point multiplier. Rational representation prevents precision drift
    /// over long warp sessions: a 10,000× rate held for hours of real time
    /// produces sim-tick advancement that's exact under integer arithmetic but
    /// accumulates error under repeated float multiplication.
    /// </para>
    ///
    /// <para>
    /// <strong>MODE-AWARE CEILINGS.</strong> The effective rate is always
    /// <c>min(currentRate, CeilingFor(ActiveVesselMode))</c>. As of commit 048
    /// Stage 2 the PhysX-active ceiling is 5× (up from the pre-commit-048 1× hard
    /// cap). Force-transition (auto-promoting the active vessel to KeplerRails
    /// when the player requests warp above the PhysX cap) was originally sketched
    /// for commit 048 but deferred to a future commit per DECISIONS entry
    /// 'Time-warp controller architecture (commit 048)' item (d) — current
    /// behavior is cap-and-stay (silent clamp to 5×, vessel does not
    /// auto-transition). The <see cref="TransitionTriggerReason"/>
    /// <c>WarpRateForcedRails</c> enum value remains as held infrastructure for
    /// the deferred work. Kepler-rails and interstellar-cruise ceilings (10,000×
    /// and 100,000×) are unchanged from the pre-commit-048 system.
    /// </para>
    ///
    /// <para>
    /// <strong>EVENT-BUS HALT SURFACING.</strong> Halt events fire via
    /// <see cref="OnWarpHalted"/>; multiple UI elements subscribe independently
    /// (Mission Control, warp-rate HUD, audio cues). Matches the existing
    /// <see cref="SimTickController.TickAdvanced"/> pattern: loose coupling
    /// between the controller and its consumers, multiple subscribers supported,
    /// no controller knowledge of how halt is surfaced.
    /// </para>
    ///
    /// <para>
    /// <strong>TARGET-TICK MODE.</strong> When <see cref="TargetTick"/> is set
    /// (via <see cref="SetTargetTick"/>), the controller advances at high warp
    /// and rounds the final advancement DOWN so it lands exactly on the target.
    /// At that point a <see cref="WarpHaltReason.TargetTickReached"/> halt fires
    /// and the controller becomes <see cref="IsHalting"/>; calling
    /// <see cref="ClearHalt"/> (Stage 4 UI affordance) resumes normal advancement.
    /// </para>
    ///
    /// <para>
    /// <strong>SINGLETON LIFECYCLE.</strong> Pattern matches
    /// <see cref="SimTickController"/>: <see cref="Awake"/> claims
    /// <see cref="Instance"/>; duplicates log an error and destroy themselves;
    /// <see cref="OnDestroy"/> clears <see cref="Instance"/>;
    /// <see cref="SetInstanceForTesting"/> and
    /// <see cref="ClearInstanceForTesting"/> exist for EditMode tests that
    /// can't rely on Awake firing.
    /// </para>
    ///
    /// <para>
    /// <strong>SCENE WIRING.</strong> Scene wiring landed at commit 048
    /// Stage 4: <c>TestVessels.unity</c> instantiates <see cref="WarpController"/>
    /// alongside <see cref="SpaceSim.Foundation.SimTick.UI.WarpUIController"/>
    /// (pause/resume, seven discrete-rate buttons, continuous slider,
    /// clear-halt). When <see cref="Instance"/> is null (non-Unity test
    /// contexts or scenes without the WarpController GameObject),
    /// <see cref="SimTickController"/> falls back to single-tick
    /// advancement; production scenes run under the rational-rate
    /// machinery throughout.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarpController : MonoBehaviour
    {
        // ----- Singleton -----

        public static WarpController Instance { get; private set; }

        // ----- Per-mode ceilings (long, matching the rational rate representation) -----

        /// <summary>
        /// Warp ceiling for <see cref="PhysicsMode.PhysXActive"/>: 5× (commit 048
        /// Stage 2 raised this from the pre-commit-048 hard cap of 1×). Stage 3
        /// will turn this from a passive cap into the trigger point for a forced
        /// KeplerRails transition.
        /// </summary>
        public const long PhysXActiveCeiling = 5L;

        /// <summary>Warp ceiling for <see cref="PhysicsMode.KeplerRails"/>: 10,000×.</summary>
        public const long KeplerRailsCeiling = 10_000L;

        /// <summary>Warp ceiling for <see cref="PhysicsMode.InterstellarCruise"/>: 100,000×.</summary>
        public const long InterstellarCruiseCeiling = 100_000L;

        // ----- State -----

        private WarpRate _currentRate = WarpRate.OneX;
        private WarpRate _previousRate = WarpRate.OneX;
        private long _pendingNumerator;
        private long? _targetTick;
        private PhysicsMode _activeVesselMode = PhysicsMode.PhysXActive;
        private bool _isHalting;
        private WarpHaltInfo? _lastHaltInfo;

        // ----- Public properties -----

        /// <summary>The current warp rate as set by the player or
        /// scheduled mode. May exceed the per-mode ceiling; the effective rate
        /// after ceiling clamping is reported via <see cref="EffectiveRate"/>.</summary>
        public WarpRate CurrentRate => _currentRate;

        /// <summary>The current active-vessel mode. Determines which per-mode ceiling
        /// applies. Updated by <see cref="SetActiveVesselMode"/> (called by
        /// <see cref="SimTickController.SetActiveVessel"/> and step 6's per-tick
        /// idempotent sync).</summary>
        public PhysicsMode ActiveVesselMode => _activeVesselMode;

        /// <summary>The effective warp rate after applying the per-mode ceiling. Equals
        /// <c>min(CurrentRate.Numerator, CeilingFor(ActiveVesselMode))</c> in v1
        /// (denominator is always 1 in the integer-only mode). Returns
        /// <see cref="WarpRate.Paused"/> when <see cref="CurrentRate"/> is paused.</summary>
        public WarpRate EffectiveRate
        {
            get
            {
                if (_currentRate.IsPaused) return WarpRate.Paused;
                long ceiling = CeilingFor(_activeVesselMode);
                // v1 denominator is always 1; effective is just min on the numerator.
                long effective = _currentRate.Numerator < ceiling ? _currentRate.Numerator : ceiling;
                return new WarpRate(effective, _currentRate.Denominator);
            }
        }

        /// <summary>The target sim-tick the controller is warping toward, or null if
        /// no target is active. Set via <see cref="SetTargetTick"/>; cleared
        /// automatically when reached (a halt event fires at the same instant).</summary>
        public long? TargetTick => _targetTick;

        /// <summary>True when the controller is in a halted state — most recently
        /// because a target tick was reached, a registered halt event fired, or
        /// the player explicitly paused via <see cref="RegisterHaltEvent"/>. In
        /// halted state <see cref="ComputeAnalyticIterations"/> returns 1
        /// (the existing "always at least 1" floor; sim-time effectively pauses
        /// from the cycle's perspective). Cleared by <see cref="ClearHalt"/>.</summary>
        public bool IsHalting => _isHalting;

        /// <summary>The most recent halt event's context. Null until the first halt
        /// fires; cleared NEVER (kept for UI's "last halt was…" display even after
        /// the player resumes via <see cref="ClearHalt"/>).</summary>
        public WarpHaltInfo? LastHaltInfo => _lastHaltInfo;

        // ----- Events -----

        /// <summary>Raised when <see cref="CurrentRate"/> changes via any of the
        /// rate-set methods (<see cref="SetDiscreteLevel"/>,
        /// <see cref="SetContinuousRate"/>, <see cref="Pause"/>,
        /// <see cref="Resume"/>). Subscribers receive the new
        /// <see cref="CurrentRate"/>.</summary>
        public event Action<WarpRate> OnRateChanged;

        /// <summary>Raised when a halt event fires (target tick reached, manual halt,
        /// or — in Stage 3+ — a vessel-level predictor halt). Subscribers receive
        /// the <see cref="WarpHaltInfo"/> describing the halt; the same value is
        /// cached on <see cref="LastHaltInfo"/>.</summary>
        public event Action<WarpHaltInfo> OnWarpHalted;

        // ----- Lifecycle -----

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"Duplicate WarpController in scene. Existing on '{Instance.gameObject.name}', " +
                    $"duplicate on '{gameObject.name}'. Destroying the duplicate.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ----- Test API -----

        /// <summary>Reset the singleton's static reference. Used by tests to ensure
        /// clean state between runs.</summary>
        public static void ClearInstanceForTesting()
        {
            Instance = null;
        }

        /// <summary>TEST-ONLY. Claim the singleton slot with the provided controller
        /// without going through <see cref="Awake"/>. Matches the
        /// <see cref="SimTickController.SetInstanceForTesting"/> pattern: in
        /// EditMode <see cref="Awake"/> doesn't fire on <c>AddComponent</c>, so
        /// tests need this to point <see cref="Instance"/> at a specific
        /// controller. Pair every call with a matching
        /// <see cref="ClearInstanceForTesting"/> in TearDown to avoid bleeding
        /// state across test cases.</summary>
        public static void SetInstanceForTesting(WarpController controller)
        {
            Instance = controller;
        }

        // ----- Mode-aware ceiling -----

        /// <summary>Return the warp ceiling for the given physics mode, per
        /// <c>docs/NETCODE_CONTRACT.md</c> §1.4 (with the commit-048 Stage 2
        /// PhysX-active value raised from 1× to 5×). Throws
        /// <see cref="ArgumentOutOfRangeException"/> for unrecognized enum values
        /// (defensive against future enum additions that fall through unhandled).</summary>
        public static long CeilingFor(PhysicsMode mode)
        {
            switch (mode)
            {
                case PhysicsMode.PhysXActive: return PhysXActiveCeiling;
                case PhysicsMode.KeplerRails: return KeplerRailsCeiling;
                case PhysicsMode.InterstellarCruise: return InterstellarCruiseCeiling;
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        /// <summary>Set the active vessel's physics mode. Used to determine the warp
        /// ceiling. Called by <see cref="SimTickController.SetActiveVessel"/>
        /// (initial set-up) and <see cref="SimTickController.Step6_DetectModeTransitions"/>
        /// (idempotent per-FixedUpdate sync so in-play mode changes propagate
        /// without an explicit SetActiveVessel re-call).</summary>
        public void SetActiveVesselMode(PhysicsMode mode)
        {
            _activeVesselMode = mode;
        }

        // ----- Rate setters -----

        /// <summary>Set the current warp rate to a discrete level.
        /// <paramref name="level"/> must be one of <c>{1, 5, 10, 100, 1000, 10000,
        /// 100000}</c>; any other value throws
        /// <see cref="ArgumentException"/> (delegated to
        /// <see cref="WarpRate.Discrete(long)"/>). Resets the rational accumulator
        /// (<see cref="WarpRate.AdvanceTicks(long, long)"/>'s pending-numerator
        /// state) so the new rate starts fresh. Fires <see cref="OnRateChanged"/>.</summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="level"/>
        /// is not one of the supported discrete levels.</exception>
        public void SetDiscreteLevel(long level)
        {
            WarpRate newRate = WarpRate.Discrete(level);
            _currentRate = newRate;
            _pendingNumerator = 0;
            OnRateChanged?.Invoke(newRate);
        }

        /// <summary>Set the current warp rate to a continuous integer value in
        /// <c>[1, 1000]</c>. Resets the rational accumulator. Fires
        /// <see cref="OnRateChanged"/>.</summary>
        /// <exception cref="ArgumentException">Thrown when
        /// <paramref name="integerRate"/> is outside <c>[1, 1000]</c>.</exception>
        public void SetContinuousRate(long integerRate)
        {
            WarpRate newRate = WarpRate.Continuous(integerRate);
            _currentRate = newRate;
            _pendingNumerator = 0;
            OnRateChanged?.Invoke(newRate);
        }

        /// <summary>Set a target sim-tick to warp toward. <paramref name="tick"/>
        /// must be greater than <paramref name="currentTickFromController"/>;
        /// otherwise <see cref="ArgumentException"/> is thrown. For v1 this also
        /// sets the rate to a high discrete level (10,000×) so the warp closes
        /// the distance quickly; the per-frame advance math in
        /// <see cref="ComputeAnalyticIterations"/> rounds the final frame down so
        /// the controller lands EXACTLY on <paramref name="tick"/> rather than
        /// overshooting. When the target is reached, a
        /// <see cref="WarpHaltReason.TargetTickReached"/> halt fires and
        /// <see cref="TargetTick"/> clears to null.
        ///
        /// <para>Returns the rate the controller set (currently always
        /// <c>WarpRate.Discrete(10000)</c>); the return value exists so future
        /// modes can choose adaptive rates without callers needing a separate
        /// query.</para></summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tick"/>
        /// is not strictly greater than
        /// <paramref name="currentTickFromController"/>.</exception>
        public WarpRate SetTargetTick(long tick, long currentTickFromController)
        {
            if (tick <= currentTickFromController)
            {
                throw new ArgumentException(
                    $"Target tick {tick} must be strictly greater than current tick {currentTickFromController}.",
                    nameof(tick));
            }
            _targetTick = tick;
            // v1: kick the rate to 10,000× so the warp closes quickly. The per-frame
            // clamp in ComputeAnalyticIterations stops exactly on the target.
            WarpRate newRate = WarpRate.Discrete(10_000);
            _currentRate = newRate;
            _pendingNumerator = 0;
            OnRateChanged?.Invoke(newRate);
            return newRate;
        }

        /// <summary>Pause time-warp. Caches the current rate so a subsequent
        /// <see cref="Resume"/> can restore it. Fires <see cref="OnRateChanged"/>
        /// with <see cref="WarpRate.Paused"/>. Idempotent — calling Pause when
        /// already paused is a no-op (and does not overwrite the cached previous
        /// rate, which would lose the pre-Pause value).</summary>
        public void Pause()
        {
            if (_currentRate.IsPaused) return;
            _previousRate = _currentRate;
            _currentRate = WarpRate.Paused;
            _pendingNumerator = 0;
            OnRateChanged?.Invoke(_currentRate);
        }

        /// <summary>Resume time-warp from a prior <see cref="Pause"/>. Restores
        /// the cached previous rate. If Resume is called without a prior Pause
        /// (the cached rate is the construction-default OneX or has been
        /// otherwise non-Paused), this is still safe: the rate stays unchanged
        /// and no event fires.</summary>
        public void Resume()
        {
            if (!_currentRate.IsPaused) return;
            if (_previousRate.IsPaused)
            {
                // No prior Pause to restore from; the cached previous rate is itself
                // Paused. Fall back to OneX.
                _previousRate = WarpRate.OneX;
            }
            _currentRate = _previousRate;
            _pendingNumerator = 0;
            OnRateChanged?.Invoke(_currentRate);
        }

        // ----- Halt event registration -----

        /// <summary>Register a halt event. Sets <see cref="IsHalting"/> to true,
        /// caches <paramref name="info"/> in <see cref="LastHaltInfo"/>, and
        /// fires <see cref="OnWarpHalted"/> with the same payload.
        ///
        /// <para>Callers (all landed across commit 048 stages):
        /// target-tick-reached (internally fired by
        /// <see cref="ComputeAnalyticIterations"/>, Stage 2);
        /// manual pause from Mission Control UI (Stage 4); vessel-level
        /// predictor-driven callers from the Vessels asmdef using the
        /// <see cref="WarpHaltReason"/> enum's predictor-mapped values
        /// (Stage 3).</para></summary>
        public void RegisterHaltEvent(WarpHaltInfo info)
        {
            _isHalting = true;
            _lastHaltInfo = info;
            OnWarpHalted?.Invoke(info);
        }

        /// <summary>Clear the halting state. Called by UI after the player
        /// acknowledges the halt; does NOT clear <see cref="LastHaltInfo"/>
        /// (which the UI may still want to display).</summary>
        public void ClearHalt()
        {
            _isHalting = false;
        }

        // ----- Per-FixedUpdate advancement -----

        /// <summary>Compute the analytic-step iteration count for one FixedUpdate.
        /// Matches the contract <c>min(effectiveRate, ticksUntilNextEvent)</c>
        /// semantics from §1.4, with an additional target-tick clamp.
        ///
        /// <list type="number">
        ///   <item>If <see cref="IsHalting"/>, return 1 (the existing "always at
        ///   least 1" floor; sim-tick advancement is effectively paused from the
        ///   cycle's perspective during a halt).</item>
        ///   <item>If <see cref="EffectiveRate"/> is paused, return 1 (same
        ///   floor).</item>
        ///   <item>Otherwise compute <c>min(effectiveRate.Numerator,
        ///   ticksUntilNextEvent)</c>.</item>
        ///   <item>If <see cref="TargetTick"/> is set and the advancement would
        ///   reach or exceed it, reduce the result so the controller lands
        ///   exactly at the target. Then clear <see cref="TargetTick"/> and fire
        ///   a <see cref="WarpHaltReason.TargetTickReached"/> halt event.</item>
        /// </list>
        ///
        /// <para>The return value is clamped to <c>[1, int.MaxValue]</c>: at least
        /// one iteration always runs so the FixedUpdate cycle isn't a no-op (even
        /// during halts the cycle still completes its non-iterated steps), and the
        /// upper bound matches the existing <c>RunFixedUpdateCycle(int)</c>
        /// signature.</para>
        ///
        /// <para>The rational accumulator
        /// (<see cref="WarpRate.AdvanceTicks(long, long)"/>'s
        /// <c>pendingNumerator</c>) is NOT consulted in v1 because the
        /// denominator is always 1. Future fractional modes can call
        /// <see cref="WarpRate.AdvanceTicks"/> directly with this controller's
        /// stored pendingNumerator; the current ComputeAnalyticIterations stays
        /// integer-fast.</para></summary>
        /// <param name="ticksUntilNextEvent">Number of sim-ticks until the next
        /// scheduled analytic event. Pass <see cref="int.MaxValue"/> when the
        /// event queue is empty.</param>
        /// <param name="currentTickFromController">The sim-tick controller's
        /// current <see cref="SimTickController.TickNumber"/>. Used for the
        /// target-tick clamp.</param>
        /// <returns>Iteration count for this FixedUpdate's analytic-step loop,
        /// clamped to <c>[1, int.MaxValue]</c>.</returns>
        public int ComputeAnalyticIterations(int ticksUntilNextEvent, long currentTickFromController)
        {
            if (_isHalting) return 1;
            if (_currentRate.IsPaused) return 1;
            if (ticksUntilNextEvent < 1) ticksUntilNextEvent = 1;

            // v1: denominator is always 1, so the effective per-frame advancement is
            // just the (clamped) numerator. Future fractional modes would call
            // _currentRate.AdvanceTicks(realTimeTicks=1, _pendingNumerator) and update
            // _pendingNumerator from the return value.
            long effectiveRate = _currentRate.Numerator;
            long ceiling = CeilingFor(_activeVesselMode);
            if (effectiveRate > ceiling) effectiveRate = ceiling;

            long iterations = effectiveRate < ticksUntilNextEvent ? effectiveRate : ticksUntilNextEvent;

            // Target-tick clamp: reduce so we land EXACTLY at the target.
            if (_targetTick.HasValue)
            {
                long ticksToTarget = _targetTick.Value - currentTickFromController;
                if (ticksToTarget <= 0)
                {
                    // Already at or past target (defensive). Fire the halt and clear
                    // the target so we don't loop on stale state.
                    FireTargetTickReachedHalt(currentTickFromController);
                    return 1;
                }
                if (iterations >= ticksToTarget)
                {
                    iterations = ticksToTarget;
                    // After this frame's advancement the controller will be at the target.
                    // Fire the halt event now so subscribers see the halt at the same tick
                    // the advancement lands; the cycle still runs the iterations to advance
                    // the counter to the target.
                    FireTargetTickReachedHalt(_targetTick.Value);
                }
            }

            if (iterations < 1) iterations = 1;
            if (iterations > int.MaxValue) iterations = int.MaxValue;
            return (int)iterations;
        }

        private void FireTargetTickReachedHalt(long landingTick)
        {
            long target = _targetTick.GetValueOrDefault(landingTick);
            _targetTick = null;
            var info = new WarpHaltInfo(
                haltingVesselId: null,
                haltReason: WarpHaltReason.TargetTickReached,
                haltTick: target,
                diagnosticMessage: $"Target tick {target} reached");
            RegisterHaltEvent(info);
        }
    }
}
