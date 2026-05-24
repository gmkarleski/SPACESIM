using System;
using SpaceSim.Foundation.Coordinates;
using UnityEngine;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// Single gravity-attracting body (planet, moon, or star) for orbital math purposes.
    ///
    /// PHASE 0 / EARLY PHASE 1 SCOPE: this class captures the identity, mass, position, and
    /// SOI structure a vessel's Kepler-rails state needs to compute orbital elements AND
    /// perform SOI re-rooting at boundary crossings (commit 044).
    ///
    /// SOI STRUCTURE (added at commit 044):
    /// Each body has a sphere-of-influence radius (<see cref="SoiRadiusMeters"/>) and an
    /// optional parent body (<see cref="ParentBody"/> / <see cref="ParentBodyId"/>).
    /// Top-level bodies (e.g., the star at the root of the home system) have no parent;
    /// their SOI radius is <see cref="double.PositiveInfinity"/> by convention — the
    /// re-rooting check on a top-level body always says "still inside SOI" because no
    /// finite distance exceeds infinity. Mathematically clean for patched conics: a body
    /// with no parent has no SOI boundary within its system.
    ///
    /// PHASE 4+ DEFERRED: full body state (axial tilt, rotation rate, surface terrain
    /// seed, atmospheric profile, child bodies list reactively-maintained, orbital state
    /// for bodies that themselves orbit) lands with the procgen-bodies work. The
    /// SoiRadiusMeters value is hand-set per body in Phase 1 (Inspector field); in
    /// Phase 4+ when bodies orbit, the value will be computed via the Laplace sphere
    /// formula <c>r_SOI ≈ a · (m/M)^(2/5)</c> where <c>a</c> is the body's semi-major
    /// axis around its parent and <c>m</c>/<c>M</c> are the body and parent masses.
    /// Phase 0 bodies don't orbit, so <c>a</c> doesn't exist and computed values aren't
    /// possible yet.
    ///
    /// For the Phase 0 test scene, a single <see cref="ReferenceBody"/> represents the
    /// "home planet" at world-origin with Earth-like mass and infinite SOI (no parent).
    /// Multi-body scenes arrive when the home system gets populated with its four
    /// intensive-craft bodies (per commit 021) during Phase 4 procgen work.
    ///
    /// The class is a <see cref="MonoBehaviour"/> rather than a plain data class so test
    /// scenes can drop a ReferenceBody GameObject into the Hierarchy and the Vessel
    /// component can reference it via Inspector wiring. When the real BodyState lands
    /// (per <c>docs/NETCODE_CONTRACT.md</c> §2.7 as of commit 044), the MonoBehaviour
    /// wrapper survives; the data fields gain depth.
    ///
    /// REGISTRY: on <see cref="Awake"/>, the body registers with <see cref="BodyRegistry"/>.
    /// On <see cref="OnDestroy"/>, it unregisters. Self-registration matches the pattern
    /// in <c>VesselRegistry</c>. The registry lets re-rooting math look up parent and
    /// child bodies by ID without traversing Inspector wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReferenceBody : MonoBehaviour
    {
        /// <summary>Permanent unique identifier for this body. Auto-assigned on Awake.</summary>
        public Guid BodyId { get; private set; }

        /// <summary>
        /// Body mass in kilograms. Default is Earth-like
        /// (<see cref="PhysicsConstants.EarthMassKg"/>) so the Phase 0 test scene
        /// produces realistic orbital elements at LEO-scale altitudes. The literal value
        /// is centralized in <see cref="PhysicsConstants"/> per commit 053-stage2.
        /// </summary>
        [SerializeField, Tooltip("Body mass in kilograms. Default PhysicsConstants.EarthMassKg (5.972e24, Earth-equivalent).")]
        private double massKg = PhysicsConstants.EarthMassKg;

        /// <summary>Body mass in kilograms.</summary>
        public double MassKg => massKg;

        /// <summary>
        /// Sphere-of-influence radius in meters. The distance from this body's center
        /// beyond which a vessel is considered to have escaped this body's gravitational
        /// dominance and re-rooted to the parent body's frame.
        ///
        /// Top-level bodies (no parent) have <see cref="double.PositiveInfinity"/> —
        /// no finite distance exceeds infinity, so re-rooting never fires.
        ///
        /// PHASE 1 SCOPE (commit 044): hand-set per body via the Inspector. Default
        /// <see cref="double.PositiveInfinity"/> covers the top-level case; bodies with
        /// a parent must have a finite value set in the Inspector.
        ///
        /// PHASE 4+ DEFERRED: computed automatically via the Laplace sphere formula
        /// when bodies orbit.
        /// </summary>
        [SerializeField, Tooltip("SOI radius in meters. Default infinity (top-level body, never re-roots). Bodies with a parent should set a finite value.")]
        private double soiRadiusMeters = double.PositiveInfinity;

        /// <summary>Sphere-of-influence radius in meters. See backing field doc for semantics.</summary>
        public double SoiRadiusMeters => soiRadiusMeters;

        /// <summary>
        /// Distance from this body's center to its surface, in meters. The
        /// <see cref="SpaceSim.Foundation.Vessels.SurfaceImpactPredictor"/> uses this
        /// (commit 047) to predict the sim-tick at which a vessel's orbit would
        /// intersect the body's surface — i.e., lithobraking — and populate the
        /// vessel's <see cref="KeplerState.NextSurfaceImpactTick"/> (commit 048
        /// Stage 1; previously the predictor populated the now-removed aggregated
        /// <c>NextModeTransitionTick</c> field).
        ///
        /// PHASE 1 SCOPE (commit 047): hand-set per body via the Inspector. Default
        /// 6.371e6 (Earth-equivalent at 1/1 scale; at the CONSTRAINTS §2 default 1/8
        /// solar-system scale, real-world bodies translate to ~800 km surface radius
        /// for an Earth-equivalent, so test scenes will typically override this).
        ///
        /// PHASE 4+ DEFERRED: procgen bodies will compute this from per-body
        /// parameters at scene-build time and write the value through to the
        /// Inspector-equivalent field at runtime. The runtime field stays the
        /// canonical storage; only the population path differs.
        /// </summary>
        [SerializeField, Tooltip("Distance from body center to surface, in meters. Default Earth-like 6,371 km. Override per body in Phase 1 scenes; procgen-populated in Phase 4+.")]
        private double surfaceRadiusMeters = 6.371e6;

        /// <summary>Surface radius in meters. See backing field doc for semantics.</summary>
        public double SurfaceRadiusMeters => surfaceRadiusMeters;

        /// <summary>
        /// Height above the surface at which the atmosphere effectively ends, in
        /// meters. The <see cref="SpaceSim.Foundation.Vessels.AtmosphericEntryPredictor"/>
        /// uses this (commit 047) to predict the sim-tick at which a vessel's orbit
        /// crosses the atmospheric boundary inbound — i.e., re-entry — and populate
        /// the vessel's <see cref="KeplerState.NextAtmosphericEntryTick"/> (commit
        /// 048 Stage 1; previously the predictor populated the now-removed
        /// aggregated <c>NextModeTransitionTick</c> field).
        ///
        /// A value of 0.0 indicates a vacuum body (no atmosphere). The atmospheric
        /// predictor returns null on vacuum bodies; no atmospheric-entry event is
        /// ever predicted for orbits around airless worlds (Moon, asteroid, etc.).
        ///
        /// The atmospheric outer boundary in absolute distance from body center is
        /// <c>SurfaceRadiusMeters + AtmosphericTopAltitudeMeters</c> — that is the
        /// threshold radius the atmospheric-entry predictor solves for.
        ///
        /// PHASE 1 SCOPE (commit 047): hand-set per body via the Inspector. Default
        /// 0.0 (vacuum) so single-body test scenes don't accidentally fire atmospheric
        /// entry events. Atmospheric bodies (the home planet, Mars-equivalent) need a
        /// finite value set in the Inspector — CONSTRAINTS §2 "Scaling discipline"
        /// commits to KSP-shaped atmospheric depth, so an Earth-equivalent uses
        /// ~70,000 m at the 1/8 default solar-system scale.
        ///
        /// This field stores ONLY the scalar atmospheric-top altitude — the full
        /// density-vs-altitude profile that the Phase 5 atmospheric flight model
        /// needs is separate work (NETCODE_CONTRACT §2.7 lists
        /// <c>atmospheric_profile</c> as Phase 4+ deferred). Commit 047 needs only
        /// the boundary altitude, not the density curve, to fire the entry event.
        /// </summary>
        [SerializeField, Tooltip("Height above surface where atmosphere ends, in meters. Zero indicates vacuum body. Default 0. Atmospheric bodies set ~70,000 m for Earth-equivalent at 1/8 scale per CONSTRAINTS §2 KSP-shaped depth.")]
        private double atmosphericTopAltitudeMeters = 0.0;

        /// <summary>
        /// Atmospheric top altitude (height above surface) in meters. See backing
        /// field doc for semantics. Zero = vacuum body.
        /// </summary>
        public double AtmosphericTopAltitudeMeters => atmosphericTopAltitudeMeters;

        /// <summary>
        /// Parent body in the hierarchy. Null for top-level bodies (the star at the root
        /// of a star system; in Phase 0 / Phase 1 single-body scenes, the lone body).
        ///
        /// Inspector-wired for human convenience: drag a parent body's GameObject onto
        /// this field. On <see cref="Awake"/> the reference is resolved into
        /// <see cref="ParentBody"/> (cached runtime reference) and
        /// <see cref="ParentBodyId"/> (Guid for save-load).
        ///
        /// DEFENSIVE CHECK: if the Inspector wires this body as its own parent (self-cycle),
        /// Awake logs an error and treats the field as null (top-level body). The math
        /// downstream would otherwise produce bogus orbital re-rooting computations
        /// because a body cannot be its own SOI's parent.
        /// </summary>
        [SerializeField, Tooltip("Parent body in the hierarchy. Null/unset for top-level bodies. Inspector self-references are rejected with an error log at Awake.")]
        private ReferenceBody parentBody;

        /// <summary>
        /// Cached parent body reference resolved at Awake. Null for top-level bodies and
        /// for self-cycle Inspector configurations (those are rejected with an error log).
        /// Runtime code uses this property for parent lookup.
        /// </summary>
        public ReferenceBody ParentBody { get; private set; }

        /// <summary>
        /// UUID of the parent body. <see cref="Guid.Empty"/> for top-level bodies (and for
        /// self-cycle configurations rejected at Awake). Save-load reads/writes this Guid;
        /// reconstruction-from-save reassigns the cached <see cref="ParentBody"/> reference
        /// via <see cref="BodyRegistry"/> lookup.
        /// </summary>
        public Guid ParentBodyId { get; private set; }

        /// <summary>
        /// Body position in world coordinates.
        ///
        /// PHASE 0 / PHASE 1 LIMITATION: position is read once at <see cref="Awake"/> from
        /// the GameObject's <c>transform.position</c> via the floating-origin manager's
        /// current origin (treating the transform as a LocalPosition). The body does not
        /// move during Phase 0 / Phase 1. When procgen bodies orbit (Phase 4+), this
        /// becomes a per-tick computed value from the body's own orbital state.
        ///
        /// SOI RE-ROOTING IMPLICATION: commit 044's re-rooting math reads this property
        /// as an instantaneous value when computing distances between bodies and vessels.
        /// Until Phase 4+ when bodies orbit, the value is constant and reads are
        /// equivalent. Once bodies orbit, the re-rooting math must call this fresh on
        /// every evaluation, never cache it across ticks — see the
        /// <c>OrbitalElements.ReRootStateVector</c> XML doc (Stage 2) for the full
        /// caching hazard.
        /// </summary>
        public WorldPosition PositionWorld { get; private set; }

        /// <summary>
        /// Standard gravitational parameter μ = G · M, in m³/s².
        ///
        /// This is the value orbital mechanics actually consumes — most equations take μ,
        /// not M. The product is more precisely measured for real bodies (μ for Earth is
        /// known to about a part in 10^9, while G has only about 4 decimals of precision),
        /// but at Phase-0/Phase-1 fidelity computing it from <see cref="MassKg"/> and
        /// <see cref="CoordinateMath.G"/> is sufficient. Phase 4+ may switch to μ as the
        /// stored quantity.
        /// </summary>
        public double Mu => CoordinateMath.G * massKg;

        private void Awake()
        {
            InitializeBodyForTesting();
        }

        /// <summary>
        /// TEST-ONLY initialization hook. Encapsulates the Awake-time logic (Guid
        /// assignment, parent-body resolution, world-position capture, registry
        /// registration) so EditMode tests can exercise it without Unity's Awake
        /// firing (which doesn't run on AddComponent in EditMode).
        ///
        /// Production code path: Unity calls <see cref="Awake"/>, which calls this
        /// method. Tests call this method directly to simulate Awake.
        ///
        /// Idempotent on BodyId (the <c>if (BodyId == Guid.Empty)</c> guard preserves
        /// any previously-assigned Guid); idempotent on registry (RegisterBodySafe
        /// dedups).
        /// </summary>
        /// <summary>
        /// TEST-ONLY initialization overload that sets the body's serialized fields
        /// before running the standard Awake-equivalent logic. Production callers
        /// configure these via the Unity Inspector; this overload provides a test
        /// seam so EditMode tests can populate fields without reflection-based
        /// private mutation.
        ///
        /// <para>NULLABLE OPTIONAL PARAMETERS:</para>
        /// <paramref name="surfaceRadiusMeters"/> and
        /// <paramref name="atmosphericTopAltitudeMeters"/> are nullable so callers
        /// that don't care about surface/atmosphere semantics (most tests, which
        /// only need a body with mass and SOI for orbital math) can omit them and
        /// pick up the SerializeField defaults (6.371e6 surface, 0 atmosphere).
        /// Passing null = use field default; passing a value = override.
        ///
        /// <para>EXISTING TESTS:</para>
        /// Pre-existing tests using reflection-based field-mutation patterns are
        /// not migrated to this overload as part of the commit that introduced it
        /// — they migrate organically as tests get touched. The reflection pattern
        /// stays valid; this overload is an additional option, not a replacement.
        /// </summary>
        /// <param name="massKg">Body mass in kilograms.</param>
        /// <param name="soiRadiusMeters">SOI radius in meters
        /// (<see cref="double.PositiveInfinity"/> for top-level bodies).</param>
        /// <param name="parentBody">Parent body reference; null for top-level bodies.</param>
        /// <param name="surfaceRadiusMeters">Surface radius in meters. Null = use
        /// SerializeField default (6.371e6, Earth-like).</param>
        /// <param name="atmosphericTopAltitudeMeters">Atmosphere top altitude above
        /// surface in meters. Null = use SerializeField default (0.0, vacuum body).</param>
        public void InitializeBodyForTesting(
            double massKg,
            double soiRadiusMeters,
            ReferenceBody parentBody = null,
            double? surfaceRadiusMeters = null,
            double? atmosphericTopAltitudeMeters = null)
        {
            this.massKg = massKg;
            this.soiRadiusMeters = soiRadiusMeters;
            this.parentBody = parentBody;
            if (surfaceRadiusMeters.HasValue)
            {
                this.surfaceRadiusMeters = surfaceRadiusMeters.Value;
            }
            if (atmosphericTopAltitudeMeters.HasValue)
            {
                this.atmosphericTopAltitudeMeters = atmosphericTopAltitudeMeters.Value;
            }
            InitializeBodyForTesting();
        }

        public void InitializeBodyForTesting()
        {
            if (BodyId == Guid.Empty)
            {
                BodyId = Guid.NewGuid();
            }

            // Resolve parent body wiring. Three cases:
            //   1. parentBody == null → top-level body. ParentBody=null, ParentBodyId=Empty.
            //   2. parentBody == this → self-cycle. Reject with error; treat as top-level.
            //   3. parentBody is a real other body → resolve cached reference + Guid.
            if (parentBody == this)
            {
                Debug.LogError(
                    $"ReferenceBody '{gameObject.name}': Inspector wires this body as its " +
                    $"own parent. A body cannot be its own SOI's parent. Treating as " +
                    $"top-level body (ParentBody = null). Fix the Inspector wiring; the " +
                    $"self-cycle would produce bogus orbital re-rooting computations.");
                ParentBody = null;
                ParentBodyId = Guid.Empty;
            }
            else if (parentBody != null)
            {
                ParentBody = parentBody;
                // parentBody.BodyId may not be populated yet if parentBody.Awake hasn't
                // fired (Unity's MonoBehaviour Awake ordering is not deterministic
                // across sibling GameObjects). If empty, give it a fresh Guid here so
                // the parent body itself will adopt the same Guid when its own Awake
                // runs (the Awake's `if (BodyId == Guid.Empty)` guard preserves any
                // pre-assigned Guid).
                if (parentBody.BodyId == Guid.Empty)
                {
                    parentBody.BodyId = Guid.NewGuid();
                }
                ParentBodyId = parentBody.BodyId;
            }
            else
            {
                ParentBody = null;
                ParentBodyId = Guid.Empty;
            }

            // Phase 0/1: capture position once at Awake. Transform.position is a Unity float3;
            // in the test scene the body sits at the scene origin (or near it), and the
            // floating origin's initial position is also zero, so the world position equals
            // the transform position to single-precision. When the body is far from origin,
            // this conversion will need to go through FloatingOriginManager.LocalToWorld
            // with the current origin.
            var t = transform.position;
            if (FloatingOriginManager.Instance != null)
            {
                PositionWorld = FloatingOriginManager.Instance.LocalToWorld(
                    new LocalPosition(t));
            }
            else
            {
                // Manager not yet up: treat transform position as world position. The
                // Phase 0 test scene places the body at origin so this branch produces
                // (0, 0, 0).
                PositionWorld = new WorldPosition(t.x, t.y, t.z);
            }

            // Register with the body registry. Self-registration pattern matches
            // VesselRegistry. The registry exists so re-rooting math can look up bodies
            // by ID without traversing Inspector wiring chains.
            BodyRegistry.RegisterBodySafe(this);
        }

        private void OnDestroy()
        {
            BodyRegistry.UnregisterBodySafe(this);
        }
    }
}
