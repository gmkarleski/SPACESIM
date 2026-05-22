using System;
using System.Reflection;
using NUnit.Framework;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.Vessels;
using Unity.Mathematics;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using static SpaceSim.Foundation.Vessels.Tests.PredictorTestState;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="AtmosphericEntryPredictor"/>. The predictor is a
    /// thin wrapper around <see cref="OrbitalElements.SolveConicAtRadius"/> with the
    /// threshold radius computed from <c>SurfaceRadiusMeters + AtmosphericTopAltitudeMeters</c>.
    /// Tests verify the wrapper logic: vacuum-body handling, threshold computation,
    /// orbit-vs-threshold geometry, defensive null handling.
    ///
    /// Test geometry pattern: Earth-like body with <c>SurfaceRadiusMeters = 6.371e6</c>
    /// and <c>AtmosphericTopAltitudeMeters = 1.0e5</c> (100 km), giving an atmospheric
    /// outer boundary of 6.471e6 m from body center. Orbits are tuned so periapsis
    /// is either above or below this boundary depending on the test.
    /// </summary>
    public class AtmosphericEntryPredictorTests
    {
        private const double EarthMassKg = 5.972e24;
        private static readonly double EarthMu = CoordinateMath.G * EarthMassKg;
        private const double EarthSurfaceRadiusMeters = 6.371e6;
        private const double EarthAtmosphericTopAltitudeMeters = 1.0e5;  // 100 km
        private const double TickIntervalSeconds = 1.0 / 30.0;

        private GameObject _bodyGo;
        private ReferenceBody _body;

        [SetUp]
        public void SetUp()
        {
            BodyRegistry.ClearForTesting();

            _bodyGo = new GameObject("Earth");
            _body = _bodyGo.AddComponent<ReferenceBody>();
            // Mass defaults to Earth-equivalent already. Set surface + atmosphere
            // explicitly per test scenario via SetAtmosphere / SetVacuum helpers.
            // _body.InitializeBodyForTesting() called by helpers after field setup.
        }

        [TearDown]
        public void TearDown()
        {
            if (_bodyGo != null) UnityObject.DestroyImmediate(_bodyGo);
            BodyRegistry.ClearForTesting();
        }

        // ----- Helpers -----

        /// <summary>
        /// Configure the body with Earth-like surface radius and a non-zero atmospheric
        /// top altitude (atmosphere present). Named wrapper preserved as semantic
        /// anchor: the test reads as "this test wants Earth with atmosphere" rather
        /// than as a generic init call.
        /// </summary>
        private void SetEarthAtmosphereAndInitialize()
        {
            _body.InitializeBodyForTesting(
                massKg: EarthMassKg,
                soiRadiusMeters: double.PositiveInfinity,
                surfaceRadiusMeters: EarthSurfaceRadiusMeters,
                atmosphericTopAltitudeMeters: EarthAtmosphericTopAltitudeMeters);
        }

        /// <summary>
        /// Configure the body as a vacuum body (atmospheric top altitude = 0). Named
        /// wrapper preserved as semantic anchor.
        /// </summary>
        private void SetVacuumAndInitialize()
        {
            _body.InitializeBodyForTesting(
                massKg: EarthMassKg,
                soiRadiusMeters: double.PositiveInfinity,
                surfaceRadiusMeters: EarthSurfaceRadiusMeters,
                atmosphericTopAltitudeMeters: 0.0);
        }

        // SetField generic reflection helper removed in the reflection-migration sweep.
        // Both callers (SetEarthAtmosphereAndInitialize, SetVacuumAndInitialize) now
        // invoke the parameterized InitializeBodyForTesting overload directly.

        // BuildState helper consolidated into PredictorTestState (imported via
        // `using static` at top of file). Call sites use the unqualified
        // BuildState(...) name unchanged from the previous local-helper era.

        // ----- Tests -----

        [Test]
        public void PredictNextEntry_VacuumBody_ReturnsNull()
        {
            // Vacuum body (AtmosphericTopAltitudeMeters = 0). The predictor's defensive
            // check returns null regardless of orbit geometry — no atmosphere = no
            // entry event possible.
            SetVacuumAndInitialize();

            // Orbit that WOULD pass through the (nonexistent) atmosphere if one existed.
            double rPeri = EarthSurfaceRadiusMeters + 5.0e4;  // 50 km above surface
            double rApo = 1.0e7;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            Assert.Less(e, 0.8, "Test orbit eccentricity must stay below 0.8");

            var state = BuildState(a, e, trueAnomalyAtEpoch: math.PI_DBL);  // apoapsis

            long? entry = AtmosphericEntryPredictor.PredictNextEntry(
                state, _body, currentTick: 0, TickIntervalSeconds);

            Assert.IsNull(entry,
                "Vacuum body should produce no atmospheric-entry event");
        }

        [Test]
        public void PredictNextEntry_OrbitAboveAtmosphere_ReturnsNull()
        {
            // Atmosphere present, but orbit's periapsis is above the atmospheric top.
            // SolveConicAtRadius returns null (rPeri > threshold → fully outside).
            SetEarthAtmosphereAndInitialize();

            double threshold = EarthSurfaceRadiusMeters + EarthAtmosphericTopAltitudeMeters;
            double rPeri = threshold + 1.0e5;  // 100 km above atmospheric top
            double rApo = 1.0e7;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            Assert.Less(e, 0.8, "Test orbit eccentricity must stay below 0.8");

            var state = BuildState(a, e, trueAnomalyAtEpoch: 0.0);

            long? entry = AtmosphericEntryPredictor.PredictNextEntry(
                state, _body, currentTick: 0, TickIntervalSeconds);

            Assert.IsNull(entry,
                "Orbit with periapsis above atmospheric top should produce no entry event");
        }

        [Test]
        public void PredictNextEntry_OrbitCrossesAtmosphere_ReturnsValidFutureTick()
        {
            // Orbit's periapsis is inside the atmosphere; apoapsis is well above.
            // Vessel starts at apoapsis (ν=π), so the future entry crossing is
            // before it reaches periapsis.
            SetEarthAtmosphereAndInitialize();

            double threshold = EarthSurfaceRadiusMeters + EarthAtmosphericTopAltitudeMeters;
            // Periapsis 50 km above surface = INSIDE atmosphere (50 km < 100 km top).
            double rPeri = EarthSurfaceRadiusMeters + 5.0e4;
            double rApo = 1.0e7;
            double a = 0.5 * (rPeri + rApo);
            double e = (rApo - rPeri) / (rApo + rPeri);
            Assert.Less(e, 0.8, "Test orbit eccentricity must stay below 0.8");

            // Start vessel at apoapsis — far above the atmosphere, descending.
            var state = BuildState(a, e, trueAnomalyAtEpoch: math.PI_DBL);

            long? entry = AtmosphericEntryPredictor.PredictNextEntry(
                state, _body, currentTick: 0, TickIntervalSeconds);

            Assert.IsTrue(entry.HasValue,
                "Orbit reaching into atmosphere should predict an entry event");
            Assert.Greater(entry.Value, 0,
                "Entry tick should be in the future");

            // Sanity: entry should happen before vessel reaches periapsis.
            // Periapsis is half a period from apoapsis.
            double T = 2.0 * math.PI_DBL * math.sqrt((a * a * a) / EarthMu);
            long halfPeriodTicks = (long)math.ceil((T / 2.0) / TickIntervalSeconds);
            Assert.Less(entry.Value, halfPeriodTicks,
                "Entry should fire before vessel reaches periapsis");
        }

        [Test]
        public void PredictNextEntry_HyperbolicEntry_ReturnsFutureTick()
        {
            // Hyperbolic trajectory — periapsis is INSIDE the atmosphere (vessel will
            // make a hot pass, then continue outbound). Vessel currently approaching
            // periapsis from far away (ν=-1.0, pre-periapsis on inbound leg).
            SetEarthAtmosphereAndInitialize();

            double rPeri = EarthSurfaceRadiusMeters + 2.0e4;  // 20 km above surface
            double e = 1.3;
            double a = rPeri / (1.0 - e);  // negative for hyperbolic

            // Vessel inbound at ν=-1.0 (before periapsis). Position r at this ν:
            double p = a * (1.0 - e * e);
            double rNow = p / (1.0 + e * math.cos(-1.0));
            // Sanity: vessel should currently be above the atmosphere (rNow > threshold).
            double threshold = EarthSurfaceRadiusMeters + EarthAtmosphericTopAltitudeMeters;
            Assert.Greater(rNow, threshold,
                $"Vessel at ν=-1.0 should be above atmosphere; rNow={rNow:E3}, threshold={threshold:E3}");

            var state = BuildState(a, e, trueAnomalyAtEpoch: -1.0);

            long? entry = AtmosphericEntryPredictor.PredictNextEntry(
                state, _body, currentTick: 0, TickIntervalSeconds);

            Assert.IsTrue(entry.HasValue,
                "Pre-periapsis hyperbolic vessel above atmosphere should predict entry");
            Assert.Greater(entry.Value, 0,
                "Entry tick should be in the future");
        }

        [Test]
        public void PredictNextEntry_NullState_ReturnsNull()
        {
            SetEarthAtmosphereAndInitialize();

            long? entry = AtmosphericEntryPredictor.PredictNextEntry(
                state: null, _body, currentTick: 0, TickIntervalSeconds);

            Assert.IsNull(entry, "Null KeplerState should return null defensively");
        }

        [Test]
        public void PredictNextEntry_NullBody_ReturnsNull()
        {
            var state = BuildState(7.0e6, eccentricity: 0.0, trueAnomalyAtEpoch: 0.0);

            long? entry = AtmosphericEntryPredictor.PredictNextEntry(
                state, currentBody: null, currentTick: 0, TickIntervalSeconds);

            Assert.IsNull(entry, "Null currentBody should return null defensively");
        }
    }
}
