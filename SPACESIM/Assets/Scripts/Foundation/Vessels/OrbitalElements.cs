using System;
using SpaceSim.Foundation.Coordinates;
using Unity.Mathematics;

namespace SpaceSim.Foundation.Vessels
{
    /// <summary>
    /// State-vector ↔ Keplerian orbital-element conversion math.
    ///
    /// All math is double-precision. Inputs are position and velocity relative to the
    /// reference body (not absolute world position — caller subtracts body world position
    /// first). Output is the six classical orbital elements (a, e, i, Ω, ω, ν₀).
    ///
    /// References:
    ///  - Bate, Mueller, White, "Fundamentals of Astrodynamics" §2.4 - §2.5
    ///  - Curtis, "Orbital Mechanics for Engineering Students" §4.4
    ///  - Vallado, "Fundamentals of Astrodynamics and Applications" §2.5
    ///
    /// EDGE CASES handled explicitly:
    ///   (a) Circular orbit (e ≈ 0): argument of periapsis ω is undefined. Convention here:
    ///       ω = 0, and ν₀ measured from the ascending node instead of periapsis (this is
    ///       the "argument of latitude" u = ω + ν, with ω = 0 by convention).
    ///   (b) Equatorial orbit (i ≈ 0 or i ≈ π): longitude of ascending node Ω is undefined.
    ///       Convention here: Ω = 0, and ω measured from the reference axis (the inertial
    ///       +X direction) instead of from the ascending node.
    ///   (c) Equatorial circular orbit (e ≈ 0 AND i ≈ 0): both Ω and ω undefined. Convention:
    ///       Ω = 0, ω = 0, ν₀ = the angle from +X to the position vector ("true longitude").
    ///   (d) Hyperbolic trajectory (e > 1): semi-major axis is negative; true anomaly is
    ///       constrained to the open interval (-acos(-1/e), +acos(-1/e)). The math handles
    ///       this without special-casing (a falls out negative; ν₀ stays in the valid range).
    ///   (e) Parabolic trajectory (e == 1 exactly): semi-major axis is infinite. This is a
    ///       measure-zero set; in floating point it never appears exactly. The math returns
    ///       a very large (but finite) negative a in that limit, which is the right thing
    ///       for downstream code (treat it as a high-energy hyperbolic trajectory).
    ///
    /// TOLERANCES used for edge-case detection:
    ///   - Eccentricity-zero threshold: <see cref="CircularThreshold"/> = 1e-10
    ///   - Inclination-zero threshold: <see cref="EquatorialThreshold"/> = 1e-10 radians
    ///
    /// These are dimensionless angles / dimensionless eccentricities, so the same constants
    /// apply across all body scales (planet around star, vessel around moon, etc.).
    /// </summary>
    public static class OrbitalElements
    {
        /// <summary>
        /// Eccentricities below this magnitude are treated as zero (circular orbit).
        /// Below this, the eccentricity vector's direction is numerically unreliable, so ω
        /// gets set to zero by convention.
        /// </summary>
        public const double CircularThreshold = 1e-10;

        /// <summary>
        /// Inclinations below this magnitude (radians) are treated as zero (equatorial
        /// orbit). Below this, the node vector's direction is numerically unreliable, so Ω
        /// gets set to zero by convention.
        /// </summary>
        public const double EquatorialThreshold = 1e-10;

        /// <summary>
        /// Compute the six classical orbital elements from position and velocity relative
        /// to a reference body of standard gravitational parameter μ.
        ///
        /// Reference frame: the position and velocity are in an inertial frame centered on
        /// the body. The "reference plane" is the XY plane (Z is the polar axis). The
        /// "reference direction" for measuring longitude of ascending node is +X. This
        /// matches Unity's default world-frame convention.
        /// </summary>
        /// <param name="positionRelativeToBody">
        /// Position of the orbiting object relative to the gravity body, in meters.
        /// </param>
        /// <param name="velocity">Velocity of the orbiting object in the body's inertial
        /// frame, in m/s.</param>
        /// <param name="mu">Standard gravitational parameter μ = G · M, in m³/s².</param>
        /// <param name="epochTick">Sim-tick at which these elements are captured.</param>
        /// <param name="referenceBodyId">UUID of the reference body, copied into the output.</param>
        /// <returns>A fully-populated <see cref="KeplerState"/> (event-prediction fields left null).</returns>
        public static KeplerState ComputeFromStateVector(
            double3 positionRelativeToBody,
            double3 velocity,
            double mu,
            long epochTick,
            Guid referenceBodyId)
        {
            // Step 1: angular momentum vector h = r × v. The magnitude of h is the
            // specific angular momentum; the direction is the orbit's normal.
            double3 h = math.cross(positionRelativeToBody, velocity);
            double hMagnitude = math.length(h);

            // Step 2: node vector n = k̂ × h (where k̂ = (0, 0, 1)). The node vector points
            // from the body toward the ascending node. For equatorial orbits this vector
            // is zero (numerically very small).
            double3 kHat = new double3(0.0, 0.0, 1.0);
            double3 n = math.cross(kHat, h);
            double nMagnitude = math.length(n);

            // Step 3: eccentricity vector e = (1/μ) · [(v² - μ/r) · r - (r · v) · v]. The
            // magnitude is the eccentricity; the direction points from the body toward
            // periapsis.
            double r = math.length(positionRelativeToBody);
            double vSquared = math.lengthsq(velocity);
            double rDotV = math.dot(positionRelativeToBody, velocity);

            double3 eccentricityVector = (1.0 / mu) * (
                (vSquared - mu / r) * positionRelativeToBody - rDotV * velocity);
            double eccentricity = math.length(eccentricityVector);

            // Step 4: specific orbital energy ε = v²/2 - μ/r. Bound orbits have ε < 0;
            // unbound have ε > 0. Semi-major axis a = -μ / (2ε), which is negative for
            // hyperbolic trajectories (consistent with the standard convention).
            double specificEnergy = vSquared / 2.0 - mu / r;
            double semiMajorAxis = -mu / (2.0 * specificEnergy);

            // Step 5: inclination i = acos(h_z / |h|). Always in [0, π].
            double inclination = math.acos(math.clamp(h.z / hMagnitude, -1.0, 1.0));

            // Step 6: longitude of ascending node Ω. Undefined for equatorial orbits.
            double longitudeOfAscendingNode;
            if (nMagnitude < EquatorialThreshold)
            {
                longitudeOfAscendingNode = 0.0;
            }
            else
            {
                longitudeOfAscendingNode = math.acos(math.clamp(n.x / nMagnitude, -1.0, 1.0));
                // Quadrant correction: if n_y < 0, Ω is in (π, 2π).
                if (n.y < 0.0) longitudeOfAscendingNode = 2.0 * math.PI_DBL - longitudeOfAscendingNode;
            }

            // Step 7: argument of periapsis ω. Undefined for circular orbits.
            double argumentOfPeriapsis;
            if (eccentricity < CircularThreshold)
            {
                argumentOfPeriapsis = 0.0;
            }
            else if (nMagnitude < EquatorialThreshold)
            {
                // Equatorial non-circular: ω measured from +X to the eccentricity vector.
                argumentOfPeriapsis = math.acos(math.clamp(eccentricityVector.x / eccentricity, -1.0, 1.0));
                if (eccentricityVector.y < 0.0)
                {
                    argumentOfPeriapsis = 2.0 * math.PI_DBL - argumentOfPeriapsis;
                }
                // Retrograde equatorial: convention flips sign.
                if (h.z < 0.0)
                {
                    argumentOfPeriapsis = 2.0 * math.PI_DBL - argumentOfPeriapsis;
                }
            }
            else
            {
                // Standard case: ω measured from the ascending node to the eccentricity vector.
                double cosArgPeriapsis = math.dot(n, eccentricityVector) / (nMagnitude * eccentricity);
                argumentOfPeriapsis = math.acos(math.clamp(cosArgPeriapsis, -1.0, 1.0));
                // Quadrant correction: if the eccentricity vector's z component is negative,
                // the periapsis is south of the reference plane and ω is in (π, 2π).
                if (eccentricityVector.z < 0.0)
                {
                    argumentOfPeriapsis = 2.0 * math.PI_DBL - argumentOfPeriapsis;
                }
            }

            // Step 8: true anomaly at epoch ν₀.
            double trueAnomalyAtEpoch;
            if (eccentricity < CircularThreshold)
            {
                // Circular orbit: ν measured from the ascending node to the position vector
                // (this is the argument of latitude with ω = 0 by convention).
                if (nMagnitude < EquatorialThreshold)
                {
                    // Circular AND equatorial: ν₀ = true longitude (angle from +X to r̂).
                    trueAnomalyAtEpoch = math.acos(math.clamp(positionRelativeToBody.x / r, -1.0, 1.0));
                    if (positionRelativeToBody.y < 0.0)
                    {
                        trueAnomalyAtEpoch = 2.0 * math.PI_DBL - trueAnomalyAtEpoch;
                    }
                }
                else
                {
                    // Circular non-equatorial: ν₀ = argument of latitude u.
                    double cosU = math.dot(n, positionRelativeToBody) / (nMagnitude * r);
                    trueAnomalyAtEpoch = math.acos(math.clamp(cosU, -1.0, 1.0));
                    if (positionRelativeToBody.z < 0.0)
                    {
                        trueAnomalyAtEpoch = 2.0 * math.PI_DBL - trueAnomalyAtEpoch;
                    }
                }
            }
            else
            {
                // Standard case: ν measured from periapsis (eccentricity vector) to position.
                double cosNu = math.dot(eccentricityVector, positionRelativeToBody) / (eccentricity * r);
                trueAnomalyAtEpoch = math.acos(math.clamp(cosNu, -1.0, 1.0));
                // Quadrant correction: r · v > 0 means moving away from periapsis
                // (ν in (0, π)); r · v < 0 means moving toward periapsis (ν in (π, 2π)).
                if (rDotV < 0.0)
                {
                    trueAnomalyAtEpoch = 2.0 * math.PI_DBL - trueAnomalyAtEpoch;
                }
            }

            return new KeplerState
            {
                SemiMajorAxis = semiMajorAxis,
                Eccentricity = eccentricity,
                Inclination = inclination,
                LongitudeOfAscendingNode = longitudeOfAscendingNode,
                ArgumentOfPeriapsis = argumentOfPeriapsis,
                TrueAnomalyAtEpoch = trueAnomalyAtEpoch,
                EpochTick = epochTick,
                ReferenceBodyId = referenceBodyId,
                // Event-prediction fields left null; computed when the propagator lands.
                NextPeriapsisTick = null,
                NextApoapsisTick = null,
                NextSoiTransitionTick = null,
                NextModeTransitionTick = null,
            };
        }

        /// <summary>
        /// Compute position and velocity (relative to the reference body) at a specified
        /// true anomaly, given Keplerian orbital elements and the body's μ.
        ///
        /// This is the inverse of <see cref="ComputeFromStateVector"/>: feeding the output
        /// of one into the other (with <paramref name="trueAnomaly"/> set to ν₀) should
        /// return position and velocity equal to the original inputs (round-trip property).
        ///
        /// The math: compute (r, v) in the "perifocal" frame (orbit plane, X̂ = periapsis,
        /// Ẑ = orbit normal), then rotate into the body's inertial frame using the three
        /// classical rotations: -ω around Ẑ_orbit (move periapsis to ascending node), -i
        /// around X̂_intermediate (level the orbit plane), -Ω around k̂_inertial (move the
        /// ascending node to +X). The negative rotation angles are deliberate; we're going
        /// FROM perifocal TO inertial.
        /// </summary>
        /// <param name="state">Keplerian orbital elements.</param>
        /// <param name="trueAnomaly">True anomaly ν at which to evaluate position and velocity (radians).</param>
        /// <param name="mu">Standard gravitational parameter μ = G · M.</param>
        /// <returns>(position relative to body in meters, velocity in m/s).</returns>
        public static (double3 position, double3 velocity) ComputeStateVector(
            KeplerState state, double trueAnomaly, double mu)
        {
            double a = state.SemiMajorAxis;
            double e = state.Eccentricity;
            double i = state.Inclination;
            double bigOmega = state.LongitudeOfAscendingNode;
            double smallOmega = state.ArgumentOfPeriapsis;

            // Semi-latus rectum p = a · (1 - e²). For hyperbolic orbits a < 0 and 1 - e² < 0,
            // so the product is positive (p > 0). For parabolic orbits this expression
            // breaks down (a = ∞, e = 1); guard against e == 1 exactly being passed in.
            double p = a * (1.0 - e * e);

            // Position in perifocal frame: r_pqw = (p / (1 + e cos ν)) · (cos ν, sin ν, 0).
            // For hyperbolic orbits, 1 + e cos ν must stay positive (this constrains the
            // valid range of ν to (-acos(-1/e), +acos(-1/e))); the caller is responsible
            // for passing a valid ν.
            double cosNu = math.cos(trueAnomaly);
            double sinNu = math.sin(trueAnomaly);
            double rMagnitude = p / (1.0 + e * cosNu);

            double3 rPerifocal = new double3(rMagnitude * cosNu, rMagnitude * sinNu, 0.0);

            // Velocity in perifocal frame: v_pqw = sqrt(μ/p) · (-sin ν, e + cos ν, 0).
            // This expression is well-behaved for all eccentricities (it doesn't have the
            // 1 + e cos ν denominator that the position has).
            double vScale = math.sqrt(mu / math.abs(p));  // |p| guards against negative-a numerical artifacts
            double3 vPerifocal = vScale * new double3(-sinNu, e + cosNu, 0.0);

            // Rotation matrix from perifocal to inertial: R = R_z(-Ω) · R_x(-i) · R_z(-ω).
            // Compose the three rotations algebraically into a single 3×3 matrix; this is
            // the standard form found in any orbital-mechanics textbook (e.g., Curtis §4.4).
            double cosOmegaBig = math.cos(bigOmega);
            double sinOmegaBig = math.sin(bigOmega);
            double cosI = math.cos(i);
            double sinI = math.sin(i);
            double cosOmegaSmall = math.cos(smallOmega);
            double sinOmegaSmall = math.sin(smallOmega);

            double r11 = cosOmegaBig * cosOmegaSmall - sinOmegaBig * sinOmegaSmall * cosI;
            double r12 = -cosOmegaBig * sinOmegaSmall - sinOmegaBig * cosOmegaSmall * cosI;
            double r13 = sinOmegaBig * sinI;
            double r21 = sinOmegaBig * cosOmegaSmall + cosOmegaBig * sinOmegaSmall * cosI;
            double r22 = -sinOmegaBig * sinOmegaSmall + cosOmegaBig * cosOmegaSmall * cosI;
            double r23 = -cosOmegaBig * sinI;
            double r31 = sinOmegaSmall * sinI;
            double r32 = cosOmegaSmall * sinI;
            double r33 = cosI;

            double3 positionInertial = new double3(
                r11 * rPerifocal.x + r12 * rPerifocal.y + r13 * rPerifocal.z,
                r21 * rPerifocal.x + r22 * rPerifocal.y + r23 * rPerifocal.z,
                r31 * rPerifocal.x + r32 * rPerifocal.y + r33 * rPerifocal.z);

            double3 velocityInertial = new double3(
                r11 * vPerifocal.x + r12 * vPerifocal.y + r13 * vPerifocal.z,
                r21 * vPerifocal.x + r22 * vPerifocal.y + r23 * vPerifocal.z,
                r31 * vPerifocal.x + r32 * vPerifocal.y + r33 * vPerifocal.z);

            return (positionInertial, velocityInertial);
        }

        /// <summary>
        /// Re-root a vessel's state vector from one reference body's frame to another's,
        /// computing the new <see cref="KeplerState"/> in the destination body's frame.
        /// Used by SOI re-rooting (commit 044) when a vessel crosses an SOI boundary
        /// inward (into a child body) or outward (back to a parent body).
        ///
        /// <para>ALGORITHM:</para>
        /// <list type="number">
        ///   <item>Compute the vessel's absolute world position by adding its current
        ///   relative position to the current body's world position.</item>
        ///   <item>Subtract the new body's world position from the absolute world
        ///   position to obtain the vessel's position relative to the new body.</item>
        ///   <item>Velocity passes through unchanged (see Phase 1 limitation below).</item>
        ///   <item>Call <see cref="ComputeFromStateVector"/> with the new relative
        ///   position, the unchanged velocity, the new body's μ, the supplied epoch
        ///   tick, and the new body's UUID to produce the new <see cref="KeplerState"/>.</item>
        /// </list>
        ///
        /// <para>WHY POSITION TRANSFORMS BUT VELOCITY DOES NOT (PHASE 1):</para>
        /// A vessel's position is defined relative to a body's origin; changing the
        /// reference body changes the origin and so the position vector changes. A
        /// vessel's velocity in an inertial frame is unaffected by the choice of origin
        /// <em>so long as the new origin is itself stationary in that inertial frame</em>.
        /// In Phase 1, all bodies are stationary (positions captured once at Awake;
        /// never updated). So the velocity vector relative to body A equals the
        /// velocity vector relative to body B, regardless of A and B's positions. The
        /// math reflects this by passing <paramref name="currentVelocity"/> through
        /// unchanged.
        ///
        /// <para>⚠ PHASE 4+ HAZARD — POSITION CACHING:</para>
        /// This helper reads <paramref name="currentBodyPositionWorld"/> and
        /// <paramref name="newBodyPositionWorld"/> as instantaneous values. In Phase 4+
        /// when bodies orbit, this method must be called fresh each evaluation, never
        /// with cached body positions. Caching either body position across ticks will
        /// produce drifting orbital elements as the body moves. Each call must read
        /// body positions fresh from <see cref="ReferenceBody.PositionWorld"/> (or
        /// equivalent) at the moment of the call. Holding a <see cref="WorldPosition"/>
        /// value across ticks and passing it in to a later re-rooting call will
        /// produce orbital elements that drift away from the body's true position by
        /// the body's traveled distance over that interval.
        ///
        /// <para>⚠ PHASE 4+ HAZARD — VELOCITY:</para>
        /// The Phase 1 implementation treats both body velocities as zero. In Phase 4+
        /// when bodies orbit, the caller must pass <c>newBodyVelocityWorld</c> and
        /// <c>currentBodyVelocityWorld</c> parameters so the vessel's velocity can be
        /// re-expressed in the new body's reference frame as
        /// <c>vesselVelocityAbsolute - newBodyVelocityWorld</c>, where
        /// <c>vesselVelocityAbsolute = currentVelocity + currentBodyVelocityWorld</c>.
        /// The current signature does not accept these parameters and silently treats
        /// them as zero. Extending the signature when bodies orbit is the correct fix;
        /// callers should NOT compensate manually because that distributes the
        /// correction across every callsite.
        ///
        /// <para>ROUND-TRIP PROPERTY:</para>
        /// Re-rooting from body A to body B, then back to body A (with the same body
        /// positions and the velocity unchanged), should produce orbital elements
        /// equivalent to the original within numerical precision. The position
        /// arithmetic is two double-precision additions and two subtractions; the
        /// dominant error source is the round-trip through
        /// <see cref="ComputeFromStateVector"/> and back, which at LEO+ scales has
        /// shown stable behavior to ~1e-6 m position and ~1e-6 m/s velocity in the
        /// existing commit 040 round-trip tests.
        /// </summary>
        /// <param name="currentPositionRelativeToCurrentBody">
        /// Vessel's position relative to the current body, in meters (from the
        /// vessel's current <see cref="KeplerState"/> propagation, or equivalent).
        /// </param>
        /// <param name="currentVelocity">
        /// Vessel's velocity in the inertial frame, in m/s. In Phase 1 this is identical
        /// to the velocity relative to either body (bodies are stationary).
        /// </param>
        /// <param name="currentBodyPositionWorld">
        /// Current body's position in world coordinates. Must be read fresh per call
        /// in Phase 4+ — see hazard note above.
        /// </param>
        /// <param name="newBodyPositionWorld">
        /// New (destination) body's position in world coordinates. Same caching hazard
        /// in Phase 4+.
        /// </param>
        /// <param name="newBodyMu">
        /// New body's standard gravitational parameter μ = G · M, in m³/s².
        /// </param>
        /// <param name="epochTick">
        /// Sim-tick at which the re-rooted orbital elements are anchored. Typically the
        /// current sim-tick when SOI re-rooting fires.
        /// </param>
        /// <param name="newBodyId">
        /// UUID of the new body, copied into the output <see cref="KeplerState.ReferenceBodyId"/>.
        /// </param>
        /// <returns>
        /// A new <see cref="KeplerState"/> describing the vessel's orbit in the new
        /// body's frame. Event-prediction fields (NextPeriapsisTick etc.) left null per
        /// Phase 0 / Phase 1 scope.
        /// </returns>
        public static KeplerState ReRootStateVector(
            double3 currentPositionRelativeToCurrentBody,
            double3 currentVelocity,
            WorldPosition currentBodyPositionWorld,
            WorldPosition newBodyPositionWorld,
            double newBodyMu,
            long epochTick,
            Guid newBodyId)
        {
            // Step 1: vessel's absolute world position. The current body's world
            // position plus the vessel's offset from it.
            double3 absoluteWorldPos = currentBodyPositionWorld.Value
                + currentPositionRelativeToCurrentBody;

            // Step 2: vessel's position relative to the new body. Subtract the new
            // body's world position from the absolute. Phase 1: this is the only
            // coordinate-frame transform needed (positions only).
            double3 newRelativePos = absoluteWorldPos - newBodyPositionWorld.Value;

            // Step 3: velocity passes through unchanged. See "WHY POSITION TRANSFORMS
            // BUT VELOCITY DOES NOT" in the XML doc above for the Phase 1 reasoning,
            // and the "PHASE 4+ HAZARD — VELOCITY" note for what changes when bodies
            // orbit.

            // Step 4: compute orbital elements in the new frame.
            return ComputeFromStateVector(
                newRelativePos,
                currentVelocity,
                newBodyMu,
                epochTick,
                newBodyId);
        }

        /// <summary>
        /// Compute periapsis distance from semi-major axis and eccentricity.
        /// Periapsis = a · (1 - e). For circular orbits, periapsis = a. For hyperbolic
        /// orbits, this still returns a positive number (a is negative; 1 - e is negative;
        /// product is positive).
        /// </summary>
        public static double PeriapsisDistance(double semiMajorAxis, double eccentricity)
            => semiMajorAxis * (1.0 - eccentricity);

        /// <summary>
        /// Compute apoapsis distance from semi-major axis and eccentricity. For elliptical
        /// orbits, apoapsis = a · (1 + e). For hyperbolic / parabolic orbits, there is no
        /// apoapsis (the orbit is unbound); this method returns
        /// <see cref="double.PositiveInfinity"/> in that case.
        /// </summary>
        public static double ApoapsisDistance(double semiMajorAxis, double eccentricity)
        {
            if (eccentricity >= 1.0) return double.PositiveInfinity;
            return semiMajorAxis * (1.0 + eccentricity);
        }
    }
}
