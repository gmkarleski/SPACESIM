using System;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.Vessels;
using Unity.Mathematics;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="PeriapsisApoapsisPredictor"/>. Pure-math tests
    /// at LEO scale. Tolerance conventions match
    /// <see cref="OrbitalElementsTests"/>: 1e-9 absolute on angles, ~1e-6 relative
    /// on distances. Tick tolerances are ±2 ticks (one tick of rounding plus one
    /// for any compound numerical noise across the predict + ceiling-conversion
    /// path).
    /// </summary>
    public class PeriapsisApoapsisPredictorTests
    {
        private const double EarthMassKg = 5.972e24;
        private static readonly double EarthMu = CoordinateMath.G * EarthMassKg;
        private const double LeoRadius = 7_000_000.0;
        private const double TickIntervalSeconds = 1.0 / 30.0;  // 30 Hz sim-tick

        // Computed period for the LEO reference orbit (a = LeoRadius, μ = EarthMu):
        // T = 2π · sqrt(a³/μ). At LEO this is ~5828 seconds ≈ 174,840 ticks.
        private static long LeoOrbitPeriodTicks
        {
            get
            {
                double T = 2.0 * math.PI_DBL * math.sqrt((LeoRadius * LeoRadius * LeoRadius) / EarthMu);
                return (long)math.ceil(T / TickIntervalSeconds);
            }
        }

        [Test]
        public void Predict_CircularOrbitAtEpoch_PeriapsisAtZeroAnomaly()
        {
            // Circular orbit (e=0), vessel at ν=0 (periapsis by convention) at epoch
            // tick 0. Predict at tick 0: vessel IS at periapsis right now, so the
            // "next" periapsis is one full period from now. Apoapsis is half a period
            // from now.
            var state = new KeplerState
            {
                SemiMajorAxis = LeoRadius,
                Eccentricity = 0.0,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = 0.0,
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            var (periapsisTick, apoapsisTick) = PeriapsisApoapsisPredictor.Predict(
                state, currentTick: 0, mu: EarthMu, tickIntervalSeconds: TickIntervalSeconds);

            Assert.IsTrue(periapsisTick.HasValue, "Elliptical orbit should always have a future periapsis");
            Assert.IsTrue(apoapsisTick.HasValue, "Elliptical orbit should always have a future apoapsis");
            // Expected periapsis: one full period from tick 0.
            long expectedPeriapsisTick = LeoOrbitPeriodTicks;
            Assert.AreEqual(expectedPeriapsisTick, periapsisTick.Value, 2,
                $"Circular orbit at periapsis: next periapsis should be ~{expectedPeriapsisTick} ticks away");
            // Expected apoapsis: half period from tick 0.
            long expectedApoapsisTick = LeoOrbitPeriodTicks / 2;
            Assert.AreEqual(expectedApoapsisTick, apoapsisTick.Value, 2,
                $"Circular orbit at periapsis: next apoapsis should be at ~{expectedApoapsisTick}");
        }

        [Test]
        public void Predict_EllipticalOrbitAtPeriapsis_ApoapsisAtHalfPeriod()
        {
            // Elliptical orbit, vessel at ν=0 at epoch. Apoapsis is half a period
            // from now regardless of eccentricity.
            double a = 1.0e7;
            double e = 0.3;
            var state = new KeplerState
            {
                SemiMajorAxis = a,
                Eccentricity = e,
                TrueAnomalyAtEpoch = 0.0,
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            double T = 2.0 * math.PI_DBL * math.sqrt((a * a * a) / EarthMu);
            long expectedApoapsisTick = (long)math.ceil((T / 2.0) / TickIntervalSeconds);

            var (_, apoapsisTick) = PeriapsisApoapsisPredictor.Predict(
                state, currentTick: 0, mu: EarthMu, tickIntervalSeconds: TickIntervalSeconds);

            Assert.IsTrue(apoapsisTick.HasValue);
            Assert.AreEqual(expectedApoapsisTick, apoapsisTick.Value, 2,
                "Elliptical at ν=0: next apoapsis at T/2 ticks from epoch");
        }

        [Test]
        public void Predict_EllipticalOrbitAtApoapsis_NextPeriapsisIsAfterApoapsis()
        {
            // Vessel currently at apoapsis (ν=π) at epoch. Next periapsis is T/2 ticks
            // away. Next apoapsis is one full period away (we're AT apoapsis now).
            double a = 1.0e7;
            double e = 0.3;
            var state = new KeplerState
            {
                SemiMajorAxis = a,
                Eccentricity = e,
                TrueAnomalyAtEpoch = math.PI_DBL,
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            double T = 2.0 * math.PI_DBL * math.sqrt((a * a * a) / EarthMu);
            long expectedPeriapsisTick = (long)math.ceil((T / 2.0) / TickIntervalSeconds);
            long expectedApoapsisTick = (long)math.ceil(T / TickIntervalSeconds);

            var (periapsisTick, apoapsisTick) = PeriapsisApoapsisPredictor.Predict(
                state, currentTick: 0, mu: EarthMu, tickIntervalSeconds: TickIntervalSeconds);

            Assert.IsTrue(periapsisTick.HasValue);
            Assert.IsTrue(apoapsisTick.HasValue);
            Assert.AreEqual(expectedPeriapsisTick, periapsisTick.Value, 2,
                "Elliptical at ν=π: next periapsis at T/2 from epoch");
            Assert.AreEqual(expectedApoapsisTick, apoapsisTick.Value, 2,
                "Elliptical at ν=π: next apoapsis is one period away (current apoapsis doesn't count)");
        }

        [Test]
        public void Predict_EllipticalAfterAdvancedTime_RecomputesCorrectly()
        {
            // Vessel at periapsis (ν=0) at epoch tick 0. Predict at tick 100 — the
            // vessel has propagated forward 100 ticks (~3.3 seconds at 30 Hz), so the
            // next periapsis is ~(T - 100·dt) ticks away from tick 100, i.e., at
            // absolute tick ~T (same absolute event tick, just predicted from a later
            // vantage point).
            var state = new KeplerState
            {
                SemiMajorAxis = LeoRadius,
                Eccentricity = 0.0,  // circular for arithmetic clarity
                TrueAnomalyAtEpoch = 0.0,
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            var predictAt0 = PeriapsisApoapsisPredictor.Predict(state, 0, EarthMu, TickIntervalSeconds);
            var predictAt100 = PeriapsisApoapsisPredictor.Predict(state, 100, EarthMu, TickIntervalSeconds);

            // The absolute event tick should be the same regardless of when we
            // predicted from (up to one tick of rounding noise).
            Assert.IsTrue(predictAt0.periapsisTick.HasValue);
            Assert.IsTrue(predictAt100.periapsisTick.HasValue);
            Assert.AreEqual(predictAt0.periapsisTick.Value, predictAt100.periapsisTick.Value, 2,
                "Same physical event predicted from different vantage points should give the same absolute tick");
        }

        [Test]
        public void Predict_HyperbolicOrbitPreperiapsis_PeriapsisInFuture()
        {
            // Hyperbolic orbit, vessel inbound (ν < 0 means before periapsis). At
            // ν=-π/4 with e=1.5, the vessel will reach periapsis at some future tick.
            // Apoapsis is null (no apoapsis on hyperbolic trajectories).
            var state = new KeplerState
            {
                SemiMajorAxis = -1.0e7,  // negative = hyperbolic
                Eccentricity = 1.5,
                TrueAnomalyAtEpoch = -math.PI_DBL * 0.25,
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            var (periapsisTick, apoapsisTick) = PeriapsisApoapsisPredictor.Predict(
                state, currentTick: 0, mu: EarthMu, tickIntervalSeconds: TickIntervalSeconds);

            Assert.IsTrue(periapsisTick.HasValue, "Pre-periapsis hyperbolic: periapsis should be predictable");
            Assert.Greater(periapsisTick.Value, 0, "Periapsis should be in the future");
            Assert.IsNull(apoapsisTick, "Hyperbolic orbits have no apoapsis");
        }

        [Test]
        public void Predict_HyperbolicOrbitPostperiapsis_BothReturnNull()
        {
            // Hyperbolic orbit, vessel already past periapsis (ν > 0, outbound). Both
            // predictions return null.
            var state = new KeplerState
            {
                SemiMajorAxis = -1.0e7,
                Eccentricity = 1.5,
                TrueAnomalyAtEpoch = math.PI_DBL * 0.25,  // past periapsis
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            var (periapsisTick, apoapsisTick) = PeriapsisApoapsisPredictor.Predict(
                state, currentTick: 0, mu: EarthMu, tickIntervalSeconds: TickIntervalSeconds);

            Assert.IsNull(periapsisTick, "Post-periapsis hyperbolic: periapsis is in the past");
            Assert.IsNull(apoapsisTick, "Hyperbolic orbits have no apoapsis");
        }

        [Test]
        public void Predict_HyperbolicOrbitWithApoapsis_ReturnsNull()
        {
            // Apoapsis on a hyperbolic trajectory is mathematically undefined (the
            // orbit is unbound). Test at multiple true-anomaly values to confirm
            // apoapsis stays null regardless of where the vessel is on the trajectory.
            double[] testAnomalies = { -math.PI_DBL * 0.4, -0.1, 0.0, 0.1, math.PI_DBL * 0.3 };

            foreach (double nu in testAnomalies)
            {
                var state = new KeplerState
                {
                    SemiMajorAxis = -1.0e7,
                    Eccentricity = 1.8,
                    TrueAnomalyAtEpoch = nu,
                    EpochTick = 0,
                    ReferenceBodyId = Guid.NewGuid(),
                };

                var (_, apoapsisTick) = PeriapsisApoapsisPredictor.Predict(
                    state, currentTick: 0, mu: EarthMu, tickIntervalSeconds: TickIntervalSeconds);

                Assert.IsNull(apoapsisTick, $"Hyperbolic at ν={nu:G6}: apoapsis must be null");
            }
        }

        [Test]
        public void Predict_NearParabolic_DoesNotThrow()
        {
            // e = 1 + 1e-10 (just barely hyperbolic). Routes through the hyperbolic
            // branch. The math is numerically unstable per the documented
            // parabolic-instability band but should not throw or produce NaN.
            var state = new KeplerState
            {
                SemiMajorAxis = -1.0e10,
                Eccentricity = 1.0 + 1e-10,
                TrueAnomalyAtEpoch = -math.PI_DBL * 0.25,
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            // Should not throw.
            var (periapsisTick, apoapsisTick) = PeriapsisApoapsisPredictor.Predict(
                state, currentTick: 0, mu: EarthMu, tickIntervalSeconds: TickIntervalSeconds);

            // Apoapsis still null (hyperbolic-branch behavior).
            Assert.IsNull(apoapsisTick);
            // Periapsis may be finite or null (overflow-defended); both acceptable.
            // The test asserts no exception thrown and no NaN/infinity sneaks through
            // — if periapsisTick has a value, it must be finite.
            if (periapsisTick.HasValue)
            {
                Assert.GreaterOrEqual(periapsisTick.Value, 0L,
                    "Near-parabolic finite result should be non-negative");
            }
        }

        [Test]
        public void Predict_EllipticalRoundTrip_ConsistentAcrossTicks()
        {
            // Predict at ticks 0, 50, 100, 200. The "next periapsis" should reference
            // the same physical event (same absolute tick) across all four
            // predictions until we cross that event.
            var state = new KeplerState
            {
                SemiMajorAxis = LeoRadius,
                Eccentricity = 0.1,
                TrueAnomalyAtEpoch = math.PI_DBL * 0.5,  // arbitrary mid-orbit position
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            var p0 = PeriapsisApoapsisPredictor.Predict(state, 0, EarthMu, TickIntervalSeconds);
            var p50 = PeriapsisApoapsisPredictor.Predict(state, 50, EarthMu, TickIntervalSeconds);
            var p100 = PeriapsisApoapsisPredictor.Predict(state, 100, EarthMu, TickIntervalSeconds);

            Assert.IsTrue(p0.periapsisTick.HasValue);
            Assert.IsTrue(p50.periapsisTick.HasValue);
            Assert.IsTrue(p100.periapsisTick.HasValue);

            // All three predictions should point at the same physical event.
            // Tolerance ±3 ticks for accumulated rounding across the three predict
            // calls (each ceilings the result, adding up to one tick of bias).
            Assert.AreEqual(p0.periapsisTick.Value, p50.periapsisTick.Value, 3,
                "Same physical event predicted from tick 0 vs tick 50 should match");
            Assert.AreEqual(p0.periapsisTick.Value, p100.periapsisTick.Value, 3,
                "Same physical event predicted from tick 0 vs tick 100 should match");
        }

        [Test]
        public void Predict_SemiMajorAxisVerification_OrbitPeriodMatchesKepler()
        {
            // Kepler's third law: T² = 4π²a³/μ, so T = 2π·sqrt(a³/μ).
            // Construct a vessel at periapsis, predict next periapsis, verify the
            // delta ticks corresponds to one period.
            double a = 1.5e7;  // 15,000 km
            var state = new KeplerState
            {
                SemiMajorAxis = a,
                Eccentricity = 0.4,
                TrueAnomalyAtEpoch = 0.0,  // at periapsis
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            var (periapsisTick, _) = PeriapsisApoapsisPredictor.Predict(
                state, currentTick: 0, mu: EarthMu, tickIntervalSeconds: TickIntervalSeconds);

            Assert.IsTrue(periapsisTick.HasValue);

            // Expected: T = 2π·sqrt(a³/μ) seconds → T / tickIntervalSeconds ticks.
            double T = 2.0 * math.PI_DBL * math.sqrt((a * a * a) / EarthMu);
            long expectedTicks = (long)math.ceil(T / TickIntervalSeconds);
            Assert.AreEqual(expectedTicks, periapsisTick.Value, 2,
                $"Period from Kepler's third law: expected ~{expectedTicks} ticks");
        }

        [Test]
        public void Predict_VeryHighEccentricity_PeriapsisInDistantFuture_ReturnsNullOrFinite()
        {
            // Eccentricity 1 - 1e-12 (extreme near-parabolic ellipse). Period
            // stretches toward infinity. Predictor's overflow defense should return
            // null rather than overflowing long arithmetic.
            //
            // To put the vessel "in the past" of periapsis with a still-future
            // periapsis prediction, set ν just before 2π (i.e., nearly back to
            // periapsis after going around). With very high e, the time between
            // M=mWrapped and M=2π stretches dramatically.
            var state = new KeplerState
            {
                SemiMajorAxis = 1.0e10,  // very large semi-major axis (long period)
                Eccentricity = 1.0 - 1e-12,
                TrueAnomalyAtEpoch = 0.001,  // just barely past periapsis
                EpochTick = 0,
                ReferenceBodyId = Guid.NewGuid(),
            };

            // Should not throw, regardless of whether the result is finite or null.
            var result = PeriapsisApoapsisPredictor.Predict(
                state, currentTick: 0, mu: EarthMu, tickIntervalSeconds: TickIntervalSeconds);

            // If periapsis is non-null, the value must be a finite positive tick
            // (not negative, not absurd). Either null OR finite-positive is acceptable.
            if (result.periapsisTick.HasValue)
            {
                Assert.Greater(result.periapsisTick.Value, 0L,
                    "If periapsis prediction is finite, it must be in the future");
            }
            // Apoapsis on near-parabolic ellipse: technically exists at M=π but the
            // period is so long it likely overflows. Allow null or finite-positive.
            if (result.apoapsisTick.HasValue)
            {
                Assert.Greater(result.apoapsisTick.Value, 0L);
            }

            // The acceptance criterion: no exception, no NaN smuggled into the long
            // arithmetic. Both fields are nullable longs; if they're non-null they're
            // finite positive. The test passes as long as we got here without an
            // exception.
        }
    }
}
