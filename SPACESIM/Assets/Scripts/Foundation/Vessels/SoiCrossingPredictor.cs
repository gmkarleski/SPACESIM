using System.Collections.Generic;
using Unity.Mathematics;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Per-orbit predictor for the next SOI (sphere-of-influence) crossing event.
    /// Pure function of <see cref="KeplerState"/>, the vessel's current
    /// <see cref="ReferenceBody"/>, the current sim-tick, and the tick interval.
    /// No state, no caching, no side effects — matches CONSTRAINTS §2's locked
    /// design ("Event predictors are pure functions of orbital and trajectory
    /// state").
    ///
    /// <para>TWO MATH PATHS:</para>
    /// <list type="number">
    ///   <item><strong>Outward (closed-form):</strong> the vessel exits the current
    ///   body's SOI. Solve r(ν) = SoiRadiusMeters for true anomaly ν using the
    ///   conic equation r(ν) = p / (1 + e·cos(ν)) where p = a·(1 − e²). The two
    ///   ν solutions (+ν, −ν) correspond to outbound and inbound radial crossings;
    ///   convert each to mean anomaly via <see cref="OrbitalElements.TrueToMeanAnomaly"/>,
    ///   then to a tick offset from currentTick, and take the smallest positive
    ///   offset. No iteration — single conic-equation solve.</item>
    ///   <item><strong>Inward (sampled-and-refined):</strong> the vessel enters any
    ///   child body's SOI. For each child of the current body, sample the
    ///   discriminator d(T) = |r_vessel_world(T) − r_child_world|² −
    ///   ChildSoiRadius² at <see cref="PredictorCoarseSampleTicks"/> intervals
    ///   across the lookahead horizon. Find the first positive-to-negative sign
    ///   flip (vessel approaches the child's SOI from outside), then bisect the
    ///   bracket to within one-tick tolerance. The first positive-to-negative
    ///   flip wins per child (a vessel that enters a child SOI cannot re-enter
    ///   without first exiting, which is the driver's job at evaluation time —
    ///   not the predictor's). Across multiple children, the predictor returns
    ///   the EARLIEST crossing — not first-match in registry order. This
    ///   divergence from <see cref="VesselSoiRerootingDriver"/>'s first-match-wins
    ///   semantics is deliberate: the driver runs at evaluation time when the
    ///   discriminator has already flipped (any child currently containing the
    ///   vessel is a correct re-root target); the predictor asks "which event
    ///   happens first?" and requires comparing all candidates by tick.</item>
    /// </list>
    ///
    /// <para>LOOKAHEAD HORIZON:</para>
    /// <list type="bullet">
    ///   <item>Elliptical (e &lt; 1): horizon = ⌈one orbital period in ticks⌉.
    ///   T = 2π / n where n = <see cref="OrbitalElements.MeanMotion"/>. Beyond
    ///   one period the orbit repeats; sampling further would only re-detect
    ///   the same crossings.</item>
    ///   <item>Hyperbolic (e ≥ 1): horizon = <see cref="PredictorMaxLookaheadTicks"/>
    ///   (~1 game year at 30 Hz). Hyperbolic orbits never repeat; the horizon
    ///   is a practical predict-ahead bound, not a mathematical one. Predictions
    ///   beyond one game year are unactionable (the player will maneuver, warp
    ///   elsewhere, or the scenario will shift).</item>
    /// </list>
    ///
    /// <para>DETECTION-AGGRESSIVENESS (PRAGMATIC vs STRICT):</para>
    /// The signature accepts a <see cref="DetectionAggressiveness"/> enum parameter
    /// per CONSTRAINTS §1701's pragmatic/strict difficulty lever. Commit 046 ships
    /// Pragmatic at all call sites; the Strict-mode branch is architecture
    /// scaffolding only and behaves identically to Pragmatic in this commit. Strict
    /// mode (Phase 5/6 work) will adopt an adaptive sample interval that shrinks
    /// at high vessel speed to catch near-tangent grazes the coarse sampler misses.
    ///
    /// <para>PRAGMATIC DETECTION TRADES:</para>
    /// <list type="bullet">
    ///   <item><em>Near-tangent grazes inside a single coarse sample interval are
    ///   missed.</em> If the discriminator dips below zero and returns positive
    ///   within <see cref="PredictorCoarseSampleTicks"/> ticks, the two bracket
    ///   samples both read positive and bisection sees no sign change. This is
    ///   the documented behavior — the driver's at-evaluation-time check
    ///   (<see cref="VesselSoiRerootingDriver"/>) catches the actual crossing if
    ///   it happens, even if the predictor missed it; warp loses its
    ///   pre-stop-on-event opportunity, but the re-root still fires correctly.</item>
    ///   <item><em>Non-monotone brackets (entry+exit within one coarse sample)
    ///   missed for the same reason.</em></item>
    ///   <item><em>Vessel currently inside a child SOI: predictor skips that
    ///   child.</em> The driver should have re-rooted the vessel on the previous
    ///   tick if it was inside a child SOI; defensive skip rather than producing
    ///   a false positive at currentTick.</item>
    /// </list>
    ///
    /// <para>CONSTANT-BODY-POSITION ASSUMPTION (PHASE 0/1):</para>
    /// The predictor assumes the current body's position AND each child body's
    /// position are constant across the prediction horizon. Phase 0 and Phase 1
    /// honor this: <see cref="ReferenceBody.PositionWorld"/> is captured once at
    /// Awake and never updated. Phase 4+ (when bodies orbit) MUST revisit both
    /// math paths: outward becomes a vessel-conic-vs-moving-body-sphere
    /// intersection (no closed form in general; sampled+refined throughout);
    /// inward becomes a two-orbit relative distance function. The predictor
    /// signature does NOT change at that point — the math paths internally
    /// upgrade to handle body motion. Callers don't need to know.
    ///
    /// <para>BOUNDARY-HUG NUMERICAL TOLERANCE:</para>
    /// <see cref="BoundaryHugTolerance"/> = 1.0 meter. If both periapsis and
    /// apoapsis distances are within this tolerance of the current body's SOI
    /// radius, the orbit is treated as numerically hugging the boundary and no
    /// outward crossing is reported. Without this guard, a circular orbit at
    /// exactly the SOI radius would falsely report a crossing every period due
    /// to floating-point rounding. The 1 meter scale is comfortable below the
    /// 100s-of-km scale of typical body SOIs.
    ///
    /// <para>SIGNATURE DIVERGENCE FROM PeriapsisApoapsisPredictor:</para>
    /// This predictor takes <see cref="ReferenceBody"/> rather than a raw
    /// <c>mu</c> parameter, because the inward search requires the body
    /// reference for <see cref="BodyRegistry.GetChildrenOf"/> lookup and for
    /// child-position math anyway. Single source of truth for μ
    /// (<see cref="ReferenceBody.Mu"/>) — no redundant parameter. The signature
    /// shape is math-driven, not a stylistic inconsistency with
    /// <see cref="PeriapsisApoapsisPredictor"/>.
    /// </summary>
    public static class SoiCrossingPredictor
    {
        /// <summary>
        /// Coarse-grid sample interval for inward SOI search, in sim-ticks. At 30 Hz
        /// (Phase 1 default) this is 3.33 seconds. Tuned for typical orbits: low
        /// enough to detect most child-SOI entries, high enough that the inner
        /// per-child loop stays cheap for vessels with single-digit children to
        /// scan. Near-tangent grazes inside one sample interval are intentionally
        /// missed per the pragmatic-detection design.
        /// </summary>
        public const int PredictorCoarseSampleTicks = 100;

        /// <summary>
        /// Maximum lookahead horizon for hyperbolic trajectories, in sim-ticks.
        /// 9.46e8 ticks ≈ 1 game year at 30 Hz (30 ticks/s × 86,400 s/day × 365 days/yr).
        /// Predictions beyond one year are unactionable; the player will maneuver,
        /// warp elsewhere, or the scenario will shift before the predicted event
        /// fires. Fits comfortably under <c>int.MaxValue</c> (2.15e9) so arithmetic
        /// in the horizon does not approach the <c>long</c> overflow band that
        /// <see cref="PeriapsisApoapsisPredictor"/>'s <c>long.MaxValue/2</c> defense
        /// guards against at the absolute-tick level.
        /// </summary>
        public const long PredictorMaxLookaheadTicks = 946_080_000L;

        /// <summary>
        /// Detection-aggressiveness lever per CONSTRAINTS §1701. Pragmatic ships in
        /// Phase 1; Strict is architecture-only in commit 046 (behaves identically
        /// to Pragmatic until Phase 5/6 lands the adaptive sampling implementation).
        /// </summary>
        public enum DetectionAggressiveness
        {
            /// <summary>
            /// Phase 1 default. Coarse fixed-interval sampling; misses near-tangent
            /// grazes inside one sample interval; driver-at-evaluation-time check
            /// is the safety net.
            /// </summary>
            Pragmatic,

            /// <summary>
            /// Architecture placeholder for Phase 5/6. Adaptive sampling that
            /// shrinks the interval at high vessel speed to catch near-tangent
            /// grazes. Commit 046 behaves identically to Pragmatic — no
            /// implementation difference yet.
            /// </summary>
            Strict
        }

        /// <summary>
        /// Numerical tolerance for the boundary-hug check (test 4: circular orbit
        /// at exactly SOI radius). If both periapsis and apoapsis distances are
        /// within this many meters of the current body's SOI radius, no outward
        /// crossing is reported.
        /// </summary>
        private const double BoundaryHugTolerance = 1.0;

        /// <summary>
        /// Compute the absolute sim-tick of the next SOI crossing event for the
        /// vessel. Returns the earliest crossing across (outward exit from
        /// <paramref name="currentBody"/>, inward entry into any of
        /// <paramref name="currentBody"/>'s children). Returns null if no crossing
        /// is predicted within the lookahead horizon.
        /// </summary>
        /// <param name="state">Vessel's current Kepler state (orbital elements + epoch).</param>
        /// <param name="currentBody">Reference body the vessel is currently parented to.
        /// μ, SOI radius, world position, and child-list lookup all derive from this.</param>
        /// <param name="currentTick">Current sim-tick; predictions are returned in
        /// absolute tick coordinates.</param>
        /// <param name="tickIntervalSeconds">Seconds per sim-tick (1/30 ≈ 0.0333s
        /// in Phase 1; pass <c>SimTickController.SimTickIntervalSeconds</c>).</param>
        /// <param name="aggressiveness">Detection-aggressiveness lever. Pragmatic in
        /// commit 046 at all call sites; Strict is architecture-only and behaves
        /// identically.</param>
        /// <returns>The absolute sim-tick of the earliest predicted SOI crossing,
        /// or null if no crossing is found within the lookahead horizon.</returns>
        public static long? PredictNextCrossing(
            KeplerState state,
            ReferenceBody currentBody,
            long currentTick,
            double tickIntervalSeconds,
            DetectionAggressiveness aggressiveness)
        {
            // Defensive: null inputs produce null. The predictor is a pure function;
            // there's no useful behavior on null arguments, but a defensive return is
            // cheap and matches the driver's per-vessel skip semantics.
            if (state == null) return null;
            if (currentBody == null) return null;

            double mu = currentBody.Mu;

            // Strict mode currently behaves identically to Pragmatic. Phase 5/6 will
            // diverge here; commit 046 keeps the parameter in the signature so call
            // sites don't churn when Strict ships.
            // (Suppress unused-variable warning by reading the value into a local.)
            _ = aggressiveness;

            // --- Outward closed-form ---
            long? outwardTick = TryPredictOutwardCrossing(
                state, currentBody, currentTick, tickIntervalSeconds, mu);

            // --- Inward sampled-and-refined ---
            long? earliestInwardTick = TryPredictEarliestInwardCrossing(
                state, currentBody, currentTick, tickIntervalSeconds, mu);

            // --- Combine: return earliest ---
            return MinNullable(outwardTick, earliestInwardTick);
        }

        // ----- Outward closed-form -----

        /// <summary>
        /// Solve r(ν) = SoiRadius for true anomaly ν on the vessel's orbit, convert to
        /// tick offset, and return the absolute tick of the next outward crossing.
        /// Returns null if the orbit never reaches the SOI radius (apoapsis &lt; SOI),
        /// if the current body has infinite SOI (top-level body convention), or if
        /// the orbit hugs the SOI boundary numerically.
        /// </summary>
        private static long? TryPredictOutwardCrossing(
            KeplerState state,
            ReferenceBody currentBody,
            long currentTick,
            double tickIntervalSeconds,
            double mu)
        {
            double soiRadius = currentBody.SoiRadiusMeters;

            // Top-level body convention: PositiveInfinity SOI never crosses.
            if (double.IsInfinity(soiRadius)) return null;
            if (soiRadius <= 0.0) return null;  // defensive

            double a = state.SemiMajorAxis;
            double e = state.Eccentricity;

            // Periapsis and apoapsis distances. Apoapsis is +infinity for hyperbolic.
            double rPeri = OrbitalElements.PeriapsisDistance(a, e);
            double rApo = OrbitalElements.ApoapsisDistance(a, e);

            // Boundary-hug numerical guard (test 4 — circular orbit at exactly SOI radius).
            // For ellipses, both periapsis and apoapsis are finite; if both within
            // tolerance of SoiRadius, treat as hugging.
            if (e < 1.0)
            {
                if (math.abs(rPeri - soiRadius) < BoundaryHugTolerance &&
                    math.abs(rApo - soiRadius) < BoundaryHugTolerance)
                {
                    return null;
                }
            }

            // No outward crossing if apoapsis < SoiRadius (orbit fully contained inside
            // SOI). For hyperbolic orbits rApo is +infinity so this branch is skipped
            // and outward crossing exists if periapsis < SoiRadius < +infinity.
            if (rApo < soiRadius) return null;

            // No outward crossing if periapsis > SoiRadius (orbit fully outside SOI —
            // shouldn't happen if vessel is currently parented to this body; defensive).
            if (rPeri > soiRadius) return null;

            // Conic equation: r(ν) = p / (1 + e·cos(ν))  ⇒  cos(ν) = (p/r − 1) / e.
            // For circular orbits (e≈0) the equation degenerates; boundary-hug above
            // catches the only meaningful circular-at-SOI case.
            double p = a * (1.0 - e * e);  // semi-latus rectum (positive for both elliptic + hyperbolic via a<0 for hyp)
            if (math.abs(e) < 1e-12)
            {
                // Pure circular orbit: r is constant. If r != soiRadius it never crosses;
                // if r == soiRadius the boundary-hug guard already returned null.
                return null;
            }

            double cosNuCrossing = (p / soiRadius - 1.0) / e;

            // cosNu out of range means no solution (defensive — shouldn't happen given
            // the periapsis/apoapsis bracket checks above).
            if (cosNuCrossing < -1.0 || cosNuCrossing > 1.0)
            {
                return null;
            }

            // Two ν solutions: ±acos(cosNu). +ν corresponds to outbound radial crossing
            // (after periapsis on its way to apoapsis); −ν corresponds to inbound radial
            // crossing (before periapsis on its way back from apoapsis). For elliptical
            // orbits both are reachable in finite time; for hyperbolic orbits only the
            // +ν branch is reachable post-periapsis (the −ν branch was the past inbound
            // crossing).
            double nuCrossing = math.acos(cosNuCrossing);  // in [0, π]

            // Convert both ν candidates to mean anomaly, then to tick offsets from
            // currentTick, and pick the smallest positive offset.
            double n = OrbitalElements.MeanMotion(state.SemiMajorAxis, mu);
            double dtSeconds = (currentTick - state.EpochTick) * tickIntervalSeconds;
            double meanAnomalyAtEpoch = OrbitalElements.TrueToMeanAnomaly(
                state.TrueAnomalyAtEpoch, e);
            double meanAnomalyNow = meanAnomalyAtEpoch + n * dtSeconds;

            double mCandidatePos = OrbitalElements.TrueToMeanAnomaly(nuCrossing, e);
            double mCandidateNeg = OrbitalElements.TrueToMeanAnomaly(-nuCrossing, e);

            double? secondsToCrossing;
            if (e < 1.0)
            {
                secondsToCrossing = SmallestPositiveDeltaElliptical(
                    meanAnomalyNow, mCandidatePos, mCandidateNeg, n);
            }
            else
            {
                secondsToCrossing = SmallestPositiveDeltaHyperbolic(
                    meanAnomalyNow, mCandidatePos, mCandidateNeg, n);
            }

            if (!secondsToCrossing.HasValue) return null;

            return SecondsToAbsoluteTick(
                secondsToCrossing.Value, currentTick, tickIntervalSeconds);
        }

        /// <summary>
        /// Compute the smallest positive seconds-until-crossing across the two ν
        /// candidates, accounting for the periodic wrap of elliptical mean anomaly.
        /// </summary>
        private static double? SmallestPositiveDeltaElliptical(
            double meanAnomalyNow, double mPositive, double mNegative, double n)
        {
            double twoPi = 2.0 * math.PI_DBL;
            double mNowWrapped = meanAnomalyNow % twoPi;
            if (mNowWrapped < 0.0) mNowWrapped += twoPi;

            double mPosWrapped = mPositive % twoPi;
            if (mPosWrapped < 0.0) mPosWrapped += twoPi;
            double mNegWrapped = mNegative % twoPi;
            if (mNegWrapped < 0.0) mNegWrapped += twoPi;

            double deltaPos = mPosWrapped - mNowWrapped;
            if (deltaPos <= 0.0) deltaPos += twoPi;
            double deltaNeg = mNegWrapped - mNowWrapped;
            if (deltaNeg <= 0.0) deltaNeg += twoPi;

            double bestDelta = math.min(deltaPos, deltaNeg);
            return bestDelta / n;
        }

        /// <summary>
        /// Hyperbolic-orbit version: mean anomaly is monotone (no wrap). Each
        /// candidate corresponds to a single ν on the orbit; the one with positive
        /// delta from current is the future crossing, the other is in the past.
        /// </summary>
        private static double? SmallestPositiveDeltaHyperbolic(
            double meanAnomalyNow, double mPositive, double mNegative, double n)
        {
            double deltaPos = mPositive - meanAnomalyNow;
            double deltaNeg = mNegative - meanAnomalyNow;

            double? best = null;
            if (deltaPos > 0.0) best = deltaPos;
            if (deltaNeg > 0.0)
            {
                best = best.HasValue ? math.min(best.Value, deltaNeg) : deltaNeg;
            }
            return best.HasValue ? best.Value / n : (double?)null;
        }

        // ----- Inward sampled-and-refined -----

        /// <summary>
        /// For each child of <paramref name="currentBody"/>, sample the
        /// vessel-to-child distance discriminator at coarse intervals across the
        /// lookahead horizon. On the first positive-to-negative sign flip, bisect to
        /// one-tick tolerance. Return the earliest crossing across all children, or
        /// null if no child SOI is entered within horizon.
        /// </summary>
        private static long? TryPredictEarliestInwardCrossing(
            KeplerState state,
            ReferenceBody currentBody,
            long currentTick,
            double tickIntervalSeconds,
            double mu)
        {
            List<ReferenceBody> children = BodyRegistry.GetChildrenOf(currentBody);
            if (children == null || children.Count == 0) return null;

            // Lookahead horizon depends on orbit type.
            long horizonTicks = ComputeLookaheadHorizon(state, mu, tickIntervalSeconds);
            if (horizonTicks <= 0) return null;

            long? earliestCrossing = null;
            double3 currentBodyPosWorld = currentBody.PositionWorld.Value;

            for (int i = 0; i < children.Count; i++)
            {
                ReferenceBody child = children[i];
                if (child == null) continue;

                double childSoiRadius = child.SoiRadiusMeters;
                if (double.IsInfinity(childSoiRadius)) continue;  // child has no finite SOI
                if (childSoiRadius <= 0.0) continue;

                double3 childPosWorld = child.PositionWorld.Value;
                double childSoiRadiusSq = childSoiRadius * childSoiRadius;

                long? crossing = FindFirstInwardCrossingForChild(
                    state, currentBody, currentBodyPosWorld,
                    childPosWorld, childSoiRadiusSq,
                    currentTick, horizonTicks, tickIntervalSeconds, mu);

                if (crossing.HasValue)
                {
                    earliestCrossing = MinNullable(earliestCrossing, crossing);
                }
            }

            return earliestCrossing;
        }

        /// <summary>
        /// Coarse-sample then bisect for one child. Returns the absolute tick of the
        /// first positive-to-negative sign flip refined to one-tick tolerance, or
        /// null if no flip is found within the horizon.
        ///
        /// If the discriminator at currentTick is already negative (vessel currently
        /// inside the child's SOI), skip — the driver should have re-rooted on the
        /// previous tick; predictor declines to produce a false positive at currentTick.
        /// </summary>
        private static long? FindFirstInwardCrossingForChild(
            KeplerState state,
            ReferenceBody currentBody,
            double3 currentBodyPosWorld,
            double3 childPosWorld,
            double childSoiRadiusSq,
            long currentTick,
            long horizonTicks,
            double tickIntervalSeconds,
            double mu)
        {
            // Discriminator at currentTick.
            double dPrev = ComputeDiscriminator(
                state, currentTick, mu, tickIntervalSeconds,
                currentBodyPosWorld, childPosWorld, childSoiRadiusSq);

            // Vessel already inside child SOI: skip this child (driver handles re-root).
            if (dPrev < 0.0) return null;

            long tPrev = currentTick;
            for (long offset = PredictorCoarseSampleTicks;
                 offset <= horizonTicks;
                 offset += PredictorCoarseSampleTicks)
            {
                long tCurr = currentTick + offset;
                double dCurr = ComputeDiscriminator(
                    state, tCurr, mu, tickIntervalSeconds,
                    currentBodyPosWorld, childPosWorld, childSoiRadiusSq);

                // Positive-to-negative flip = vessel entered SOI between tPrev and tCurr.
                if (dPrev > 0.0 && dCurr < 0.0)
                {
                    return BisectToCrossing(
                        state, currentBody, currentBodyPosWorld,
                        childPosWorld, childSoiRadiusSq,
                        tPrev, tCurr, mu, tickIntervalSeconds);
                }

                dPrev = dCurr;
                tPrev = tCurr;
            }

            return null;
        }

        /// <summary>
        /// Bisect on a known positive-to-negative bracket to within one-tick tolerance.
        /// Returns the LAST tick where the discriminator is still positive + 1 (i.e.,
        /// the FIRST tick where the discriminator goes non-positive — the entry tick).
        /// </summary>
        private static long BisectToCrossing(
            KeplerState state,
            ReferenceBody currentBody,
            double3 currentBodyPosWorld,
            double3 childPosWorld,
            double childSoiRadiusSq,
            long tickLow,    // discriminator > 0 (outside child SOI)
            long tickHigh,   // discriminator < 0 (inside child SOI)
            double mu,
            double tickIntervalSeconds)
        {
            while (tickHigh - tickLow > 1)
            {
                long tickMid = tickLow + (tickHigh - tickLow) / 2;
                double dMid = ComputeDiscriminator(
                    state, tickMid, mu, tickIntervalSeconds,
                    currentBodyPosWorld, childPosWorld, childSoiRadiusSq);

                if (dMid > 0.0)
                {
                    tickLow = tickMid;
                }
                else
                {
                    tickHigh = tickMid;
                }
            }
            // tickHigh is the first tick where the vessel is inside the SOI.
            return tickHigh;
        }

        /// <summary>
        /// d(T) = |r_vessel_world(T) − r_child_world|² − r_SOI². Squared distance
        /// avoids a sqrt per sample; sign is preserved. Vessel position from Kepler
        /// propagation is body-relative; add current body's world position to get
        /// world coordinates (constant-body-position assumption — child position is
        /// constant too in Phase 0/1).
        /// </summary>
        private static double ComputeDiscriminator(
            KeplerState state,
            long tick,
            double mu,
            double tickIntervalSeconds,
            double3 currentBodyPosWorld,
            double3 childPosWorld,
            double childSoiRadiusSq)
        {
            var (rRelative, _) = KeplerPropagator.PropagateState(
                state, tick, mu, tickIntervalSeconds);
            double3 vesselWorld = currentBodyPosWorld + rRelative;
            double3 delta = vesselWorld - childPosWorld;
            double distanceSq = math.dot(delta, delta);
            return distanceSq - childSoiRadiusSq;
        }

        /// <summary>
        /// Lookahead horizon in ticks. Elliptical orbits: one period. Hyperbolic
        /// orbits: <see cref="PredictorMaxLookaheadTicks"/>.
        /// </summary>
        private static long ComputeLookaheadHorizon(
            KeplerState state, double mu, double tickIntervalSeconds)
        {
            double e = state.Eccentricity;
            if (e < 1.0)
            {
                double n = OrbitalElements.MeanMotion(state.SemiMajorAxis, mu);
                if (n <= 0.0 || double.IsNaN(n) || double.IsInfinity(n))
                {
                    return PredictorMaxLookaheadTicks;
                }
                double periodSeconds = 2.0 * math.PI_DBL / n;
                double periodTicksD = math.ceil(periodSeconds / tickIntervalSeconds);
                if (periodTicksD > (double)PredictorMaxLookaheadTicks)
                {
                    return PredictorMaxLookaheadTicks;
                }
                return (long)periodTicksD;
            }
            return PredictorMaxLookaheadTicks;
        }

        // ----- Tick-arithmetic helpers -----

        /// <summary>
        /// Convert seconds-until-event to an absolute sim-tick. Mirrors
        /// <see cref="PeriapsisApoapsisPredictor"/>'s overflow defense: null if the
        /// result would exceed <c>long.MaxValue/2</c>.
        /// </summary>
        private static long? SecondsToAbsoluteTick(
            double secondsUntilEvent, long currentTick, double tickIntervalSeconds)
        {
            if (double.IsNaN(secondsUntilEvent) || double.IsInfinity(secondsUntilEvent))
            {
                return null;
            }
            if (secondsUntilEvent < 0.0) return null;

            double ticksUntilEventD = math.ceil(secondsUntilEvent / tickIntervalSeconds);
            if (ticksUntilEventD > (double)(long.MaxValue / 2)) return null;

            return currentTick + (long)ticksUntilEventD;
        }

        /// <summary>
        /// Return the smaller of two nullable longs. Null is treated as "no value";
        /// if both null, returns null; if one null, returns the other.
        ///
        /// Uses <see cref="System.Math.Min(long, long)"/> rather than
        /// <c>Unity.Mathematics.math.min</c> because the latter has no <c>long</c>
        /// overload — only int, uint, float, and double — and would silently coerce
        /// to double, losing precision near <c>long.MaxValue</c>.
        /// </summary>
        private static long? MinNullable(long? a, long? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return System.Math.Min(a.Value, b.Value);
        }
    }
}
