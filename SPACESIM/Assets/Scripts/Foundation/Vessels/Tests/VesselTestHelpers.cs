using System;
using SpaceSim.Foundation.Coordinates;
using SpaceSim.Foundation.SimTick;
using SpaceSim.Foundation.Vessels;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels.Tests
{
    /// <summary>
    /// Shared helpers for vessel test files. Holds constants, state builders, and
    /// IActiveVessel stub types used across multiple test classes in the Vessels
    /// asmdef. Consolidated here as part of the operational commit that split
    /// <c>VesselTests.cs</c> at the natural driver-test seam — helpers that the
    /// audit identified as cross-cutting (used by sections that stay in
    /// <c>VesselTests.cs</c> AND by sections that moved to dedicated driver test
    /// files) live here; single-use helpers stay local to their consumer file.
    ///
    /// <para>
    /// <strong>USAGE — `using static`:</strong> consumer test files import this
    /// class via <c>using static SpaceSim.Foundation.Vessels.Tests.VesselTestHelpers;</c>
    /// at the top of the file. Constants and static methods become available
    /// unqualified — <c>LeoRadius</c>, <c>NewState()</c>, <c>NewKeplerState(_body)</c>,
    /// <c>BuildMoonAsChildOfEarth(_body)</c>. Matches the pattern established by
    /// <see cref="PredictorTestState"/> in the BuildState-consolidation commit.
    /// The stub types (<see cref="StubActiveVessel"/> /
    /// <see cref="ThrowingActiveVessel"/>) need explicit type names since C#'s
    /// <c>using static</c> only imports members, not nested types — consumers
    /// reference them as <c>new StubActiveVessel(pos)</c> through the regular
    /// namespace <c>using</c>.
    /// </para>
    ///
    /// <para>
    /// <strong>CROSS-CUTTING CRITERION:</strong> a helper landed here only if it
    /// was used by at least two test files. Specifically: <see cref="NewState"/>
    /// (76 call sites across VesselTests.cs + the three driver test files),
    /// <see cref="NewKeplerState"/> (19 sites), <see cref="BuildMoonAsChildOfEarth"/>
    /// (8 sites — 5 in VesselTests's ReRootToBody-direct section that stays,
    /// 3 in the SOI re-rooter driver section that moved out), plus the two
    /// IActiveVessel stub types (used by the EvaluateTransitionTriggers section
    /// that stays AND by the VesselTransitionDriver section that moved).
    /// Helpers used by a single consumer file (e.g.,
    /// <c>SetUpEventPredictionDriver</c>, <c>NewCrossingKeplerState</c>,
    /// <c>NewAtmosphericCrossingState</c>) stay local to their file.
    /// </para>
    ///
    /// <para>
    /// <strong>INSTANCE-FIELD DEPENDENCY HANDLING:</strong> the original
    /// instance-method versions of <see cref="NewKeplerState"/> and
    /// <see cref="BuildMoonAsChildOfEarth"/> referenced <c>_body</c> directly.
    /// The static extraction accepts a <see cref="ReferenceBody"/> parameter
    /// instead, so consumers pass their own <c>_body</c> field at the call
    /// site: <c>NewKeplerState(_body)</c>, <c>BuildMoonAsChildOfEarth(_body)</c>.
    /// 27 call sites were updated to pass the argument.
    /// </para>
    /// </summary>
    internal static class VesselTestHelpers
    {
        // ----- Constants -----

        /// <summary>
        /// Low-Earth-orbit radius in meters. Used as the default
        /// <see cref="KeplerState.SemiMajorAxis"/> in <see cref="NewKeplerState"/>
        /// and as the standard "near Earth" position offset in 50+ call sites
        /// across the vessel test files.
        /// </summary>
        internal const double LeoRadius = 7_000_000.0;

        /// <summary>
        /// Earth-Moon mean distance in meters (3.844 × 10⁸ m). Used to position
        /// the Moon body in the Earth-Moon test substrate and as a synthetic
        /// "vessel co-located with Moon" semi-major axis in SOI-entry tests.
        /// </summary>
        internal const double EarthMoonDistanceMeters = 3.844e8;

        /// <summary>
        /// Moon mass in kilograms (7.342 × 10²² kg). Used by
        /// <see cref="BuildMoonAsChildOfEarth"/> when constructing the Moon body.
        /// </summary>
        internal const double MoonMassKg = 7.342e22;

        /// <summary>
        /// Moon SOI radius in meters (~66,100 km — the real Moon's sphere of
        /// influence relative to Earth). Used by
        /// <see cref="BuildMoonAsChildOfEarth"/> for the Moon body's SOI radius.
        /// Finite (unlike the SetUp _body's default <see cref="double.PositiveInfinity"/>)
        /// so SOI-crossing logic can be exercised.
        /// </summary>
        internal const double MoonSoiRadiusMeters = 6.6e7;

        // ----- State builders -----

        /// <summary>
        /// Construct a default <see cref="VesselAuthoritativeState"/> with fresh
        /// random GUIDs, a "TestVessel" name, 1000 kg mass, and the supplied
        /// physics mode. The specific identifiers don't matter for most tests;
        /// what matters is the state is non-null and internally consistent.
        /// </summary>
        internal static VesselAuthoritativeState NewState(
            PhysicsMode mode = PhysicsMode.PhysXActive)
        {
            return new VesselAuthoritativeState
            {
                VesselId = Guid.NewGuid(),
                DesignId = Guid.NewGuid(),
                Name = "TestVessel",
                TotalMassKg = 1000.0,
                Mode = mode,
            };
        }

        /// <summary>
        /// Construct a default circular-LEO <see cref="KeplerState"/> referencing
        /// the supplied <paramref name="body"/>'s id. Inclination, longitude of
        /// ascending node, argument of periapsis, and true anomaly at epoch are
        /// all zero (equatorial, periapsis at +X, vessel at periapsis at epoch);
        /// semi-major axis is <see cref="LeoRadius"/>; eccentricity is zero
        /// (circular).
        ///
        /// <para>
        /// <paramref name="body"/> may be null in edge cases where a test wants
        /// the empty-body fallback; in that case <see cref="KeplerState.ReferenceBodyId"/>
        /// is set to <see cref="Guid.Empty"/>. Most callers pass a real body
        /// (typically <c>_body</c> from their own SetUp).
        /// </para>
        /// </summary>
        internal static KeplerState NewKeplerState(ReferenceBody body)
        {
            return new KeplerState
            {
                SemiMajorAxis = LeoRadius,
                Eccentricity = 0.0,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                TrueAnomalyAtEpoch = 0.0,
                EpochTick = 0,
                ReferenceBodyId = body != null ? body.BodyId : Guid.Empty,
            };
        }

        /// <summary>
        /// Build an Earth-Moon test substrate. The caller's <paramref name="earth"/>
        /// stays as the top-level body (typically the test class's <c>_body</c>);
        /// this helper adds a "Moon" <see cref="ReferenceBody"/> at the Earth-Moon
        /// offset with finite SOI and the supplied earth as parent. Returns the
        /// Moon body so the test can wire it as a re-root target.
        ///
        /// <para>
        /// Both bodies are initialized via the parameterized
        /// <see cref="ReferenceBody.InitializeBodyForTesting(double, double, ReferenceBody, double?, double?)"/>
        /// overload — Earth first (so its BodyId is populated before Moon resolves
        /// its parent), then Moon with the parameterized overload's
        /// <c>parentBody</c> parameter wiring the relationship cleanly.
        /// </para>
        /// </summary>
        internal static ReferenceBody BuildMoonAsChildOfEarth(ReferenceBody earth)
        {
            var moonGo = new GameObject("Moon");
            moonGo.transform.position = new Vector3((float)EarthMoonDistanceMeters, 0, 0);
            var moon = moonGo.AddComponent<ReferenceBody>();

            // Make sure Earth's BodyId is populated before Moon resolves its parent.
            earth.InitializeBodyForTesting();

            // Configure Moon with mass + finite SOI + Earth as parent via the
            // parameterized InitializeBodyForTesting overload.
            moon.InitializeBodyForTesting(
                massKg: MoonMassKg,
                soiRadiusMeters: MoonSoiRadiusMeters,
                parentBody: earth);

            return moon;
        }
    }

    // ----- IActiveVessel stub types -----
    //
    // Top-level types in the namespace (not nested inside VesselTestHelpers) so
    // consumers can reference them by short name after a regular `using
    // SpaceSim.Foundation.Vessels.Tests;` — `using static VesselTestHelpers;`
    // only imports members of the static class, not nested types. Both stubs
    // qualify as cross-cutting per the criterion in the class doc above
    // (StubActiveVessel used by the EvaluateTransitionTriggers section that
    // stays in VesselTests.cs AND the VesselTransitionDriver section that
    // moved out; ThrowingActiveVessel used by both for exception-handling tests).

    /// <summary>
    /// Stub <see cref="IActiveVessel"/> used as the proximity reference in
    /// trigger-evaluation tests and driver tests. Returns the constructor-supplied
    /// world position and mode. Useful for controlling distance-to-active-vessel
    /// independently of any real Vessel instance.
    /// </summary>
    internal sealed class StubActiveVessel : IActiveVessel
    {
        private readonly WorldPosition _pos;
        private readonly PhysicsMode _mode;

        public StubActiveVessel(WorldPosition pos, PhysicsMode mode = PhysicsMode.PhysXActive)
        {
            _pos = pos;
            _mode = mode;
        }

        public WorldPosition GetWorldPosition() => _pos;
        public PhysicsMode Mode => _mode;
    }

    /// <summary>
    /// Stub <see cref="IActiveVessel"/> that throws on <see cref="GetWorldPosition"/>.
    /// Used by driver tests to verify per-vessel exception isolation around the
    /// proximity-distance check.
    /// </summary>
    internal sealed class ThrowingActiveVessel : IActiveVessel
    {
        public WorldPosition GetWorldPosition()
        {
            throw new InvalidOperationException("Stub throws by design");
        }

        public PhysicsMode Mode => PhysicsMode.PhysXActive;
    }
}
