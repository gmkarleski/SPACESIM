using System;
using SpaceSim.Foundation.Coordinates;
using Unity.Mathematics;
using UnityEngine;  // For Debug.LogWarning in Newton-Raphson non-convergence path.

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

        // ----- Anomaly conversions (extracted from KeplerPropagator in commit 045) -----

        /// <summary>
        /// Convergence tolerance for Newton-Raphson on Kepler's equation, in radians.
        /// At LEO scale (~7e6 m), 1e-10 rad ≈ 7e-4 m position error from solver
        /// convergence — comfortably below downstream tolerances.
        /// </summary>
        public const double KeplerConvergenceTolerance = 1e-10;

        /// <summary>
        /// Maximum Newton-Raphson iterations before declaring non-convergence. Typical
        /// convergence is 3-5 iterations with Conway's starter; 15 is a generous upper
        /// bound that catches pathological cases (e &gt; 0.99 with adversarial M).
        /// </summary>
        public const int MaxKeplerIterations = 15;

        /// <summary>
        /// Eccentricity band around 1.0 where the elliptic-vs-hyperbolic dispatch produces
        /// O(1e-3 m) position error due to numerical instability near the parabolic limit.
        /// See <see cref="TrueToMeanAnomaly"/> XML doc for the parabolic-omission rationale.
        /// Width is asymmetric: 1e-8 on both sides, total band [1 - 1e-8, 1 + 1e-8].
        /// </summary>
        public const double ParabolicInstabilityBand = 1e-8;

        /// <summary>
        /// Numerical tolerance for the "orbit hugs the threshold radius" edge case in
        /// <see cref="SolveConicAtRadius"/>. If an elliptical orbit's periapsis AND
        /// apoapsis are both within this many meters of the target radius, the orbit
        /// is treated as numerically tangent to the boundary and no crossing is
        /// reported. Without this guard, a circular orbit at exactly the target
        /// radius would falsely report a crossing every period due to floating-point
        /// rounding in <c>cos(ν) = (p/r − 1) / e</c> evaluating to values outside
        /// <c>[-1, 1]</c> by small numerical noise.
        ///
        /// The 1.0 m scale is comfortable below the 100s-of-km scale of typical body
        /// SOIs and surface radii; orbits genuinely passing within 1 m of a boundary
        /// are below the precision the predictors operate at anyway.
        ///
        /// Migrated from <c>SoiCrossingPredictor</c>'s private constant at commit 047
        /// Stage 1 so the value is shared by SOI-crossing outward, atmospheric-entry,
        /// and surface-impact predictors via <see cref="SolveConicAtRadius"/>.
        /// </summary>
        public const double BoundaryHugTolerance = 1.0;

        /// <summary>
        /// Mean motion n = sqrt(μ / |a|³), in radians per second. The angular rate at
        /// which mean anomaly advances along the orbit.
        ///
        /// Always returns a positive value. Orbit direction is encoded geometrically
        /// (inclination, RAAN, argument-of-periapsis) — never in the sign of n. The
        /// |a| inside the formula handles both elliptical (a > 0) and hyperbolic
        /// (a &lt; 0) orbits with the same expression.
        ///
        /// UNITS: <paramref name="mu"/> in m³/s², <paramref name="semiMajorAxis"/> in
        /// meters. Returns rad/sec. Callers converting to ticks-of-mean-anomaly-advance
        /// multiply by <c>SimTickController.SimTickIntervalSeconds</c> per tick.
        ///
        /// Mean motion is independent of true/eccentric/hyperbolic anomaly — the rate
        /// is constant along the orbit; only the geometric advance per radian of mean
        /// anomaly varies with position (faster at periapsis, slower at apoapsis for
        /// ellipses).
        /// </summary>
        /// <param name="semiMajorAxis">Semi-major axis a in meters. Sign carries orbit type
        /// (positive ellipse, negative hyperbola); the function uses |a|.</param>
        /// <param name="mu">Standard gravitational parameter μ = G · M of the reference
        /// body, in m³/s².</param>
        /// <returns>Mean motion n in radians per second (always positive).</returns>
        public static double MeanMotion(double semiMajorAxis, double mu)
        {
            double absA = math.abs(semiMajorAxis);
            return math.sqrt(mu / (absA * absA * absA));
        }

        /// <summary>
        /// Convert true anomaly ν to mean anomaly M. Dispatches on eccentricity:
        /// elliptical (e &lt; 1) goes through eccentric anomaly E; hyperbolic (e &gt; 1)
        /// goes through hyperbolic anomaly H.
        ///
        /// <para>ELLIPTICAL BRANCH (e &lt; 1):</para>
        /// <list type="number">
        ///   <item>E = 2 · atan2(√(1-e) · sin(ν/2), √(1+e) · cos(ν/2))</item>
        ///   <item>M = E - e · sin(E)  (Kepler's equation forward)</item>
        /// </list>
        ///
        /// <para>HYPERBOLIC BRANCH (e &gt; 1):</para>
        /// <list type="number">
        ///   <item>H = 2 · atanh(√((e-1)/(e+1)) · tan(ν/2))</item>
        ///   <item>M = e · sinh(H) - H  (hyperbolic Kepler's equation forward)</item>
        /// </list>
        ///
        /// <para>SIGN CONVENTION:</para>
        /// Mean anomaly is signed. A negative true anomaly produces a negative mean
        /// anomaly (the math is odd-symmetric around ν=0). Predictor work that takes
        /// the difference of two mean anomalies (current vs. target) gets a signed Δt,
        /// which is the correct shape for "ticks until target, possibly in the past."
        ///
        /// <para>RANGE:</para>
        /// Elliptical: caller may pass any ν; output M is in the same half-period as the
        /// input (e.g., ν ∈ [0, π] → M ∈ [0, π]; ν ∈ [-π, 0] → M ∈ [-π, 0]). The
        /// function does not wrap. Wrapping into [0, 2π) is the caller's responsibility
        /// if needed for downstream stability.
        /// Hyperbolic: ν must be within the asymptote angle ±acos(-1/e). Outside this
        /// range, the trajectory is past the asymptote — physically meaningless. The
        /// function clamps the internal atanh argument to (-1, 1) to avoid singularities;
        /// callers passing ν beyond the asymptote get an approximation, not an error.
        ///
        /// <para>NO PARABOLIC HANDLING:</para>
        /// The function does not have an explicit e = 1 branch. Within the
        /// <see cref="ParabolicInstabilityBand"/> (e ∈ [1 - 1e-8, 1 + 1e-8]), the
        /// elliptic-vs-hyperbolic dispatch is numerically unstable but does not throw.
        /// Real orbits never sit at e = 1 exactly; the instability band is narrow enough
        /// that physical scenarios (planetary capture, escape) sample either side without
        /// touching the singular region. See <see cref="KeplerPropagator"/>'s class-level
        /// XML doc for the full parabolic-omission rationale.
        /// </summary>
        /// <param name="trueAnomaly">True anomaly ν in radians. Signed; may be negative.</param>
        /// <param name="eccentricity">Eccentricity e. 0 ≤ e &lt; 1 for ellipse, e &gt; 1
        /// for hyperbola.</param>
        /// <returns>Mean anomaly M in radians (signed).</returns>
        public static double TrueToMeanAnomaly(double trueAnomaly, double eccentricity)
        {
            if (eccentricity < 1.0)
            {
                // Elliptical: ν → E → M.
                double halfNu = trueAnomaly * 0.5;
                double sqrtOneMinusE = math.sqrt(1.0 - eccentricity);
                double sqrtOnePlusE = math.sqrt(1.0 + eccentricity);
                double eccentricAnomaly = 2.0 * math.atan2(
                    sqrtOneMinusE * math.sin(halfNu),
                    sqrtOnePlusE * math.cos(halfNu));
                return eccentricAnomaly - eccentricity * math.sin(eccentricAnomaly);
            }
            else
            {
                // Hyperbolic: ν → H → M.
                double tanHalfNu = math.tan(trueAnomaly * 0.5);
                double ratio = math.sqrt((eccentricity - 1.0) / (eccentricity + 1.0)) * tanHalfNu;
                // atanh is defined for |ratio| < 1 (corresponds to ν within asymptote angle).
                // Clamp defensively against floating-point precision near the asymptote.
                ratio = math.clamp(ratio, -0.9999999999, 0.9999999999);
                double hyperbolicAnomaly = 2.0 * Atanh(ratio);
                return eccentricity * math.sinh(hyperbolicAnomaly) - hyperbolicAnomaly;
            }
        }

        /// <summary>
        /// Convert mean anomaly M to true anomaly ν. Inverse of
        /// <see cref="TrueToMeanAnomaly"/>. Dispatches on eccentricity: elliptical
        /// solves Kepler's equation iteratively; hyperbolic solves the hyperbolic
        /// Kepler equation iteratively.
        ///
        /// <para>ELLIPTICAL BRANCH (e &lt; 1):</para>
        /// <list type="number">
        ///   <item>Solve M = E - e · sin(E) for E via Newton-Raphson with Conway's 1986
        ///   starter (typical convergence 3-5 iterations).</item>
        ///   <item>ν = 2 · atan2(√(1+e) · sin(E/2), √(1-e) · cos(E/2))</item>
        /// </list>
        /// On non-convergence within <see cref="MaxKeplerIterations"/>, logs a warning
        /// via <c>Debug.LogWarning</c> and returns the best estimate (position error
        /// may exceed <see cref="KeplerConvergenceTolerance"/>).
        ///
        /// <para>HYPERBOLIC BRANCH (e &gt; 1):</para>
        /// <list type="number">
        ///   <item>Solve M = e · sinh(H) - H for H via Newton-Raphson with Conway's
        ///   logarithmic starter for large |M|.</item>
        ///   <item>tan(ν/2) = √((e+1)/(e-1)) · tanh(H/2)</item>
        /// </list>
        /// On non-convergence: same fallback as elliptical.
        ///
        /// <para>ROUND-TRIP PROPERTY:</para>
        /// <c>MeanToTrueAnomaly(TrueToMeanAnomaly(ν, e), e)</c> returns ν to within
        /// <see cref="KeplerConvergenceTolerance"/> for all valid (ν, e) pairs outside
        /// the parabolic-instability band.
        ///
        /// <para>NO PARABOLIC HANDLING:</para>
        /// Same as <see cref="TrueToMeanAnomaly"/>.
        /// </summary>
        /// <param name="meanAnomaly">Mean anomaly M in radians (signed).</param>
        /// <param name="eccentricity">Eccentricity e.</param>
        /// <returns>True anomaly ν in radians (signed).</returns>
        public static double MeanToTrueAnomaly(double meanAnomaly, double eccentricity)
        {
            if (eccentricity < 1.0)
            {
                double eccentricAnomaly = SolveKeplerElliptic(meanAnomaly, eccentricity);
                double halfE = eccentricAnomaly * 0.5;
                double sqrtOneMinusE = math.sqrt(1.0 - eccentricity);
                double sqrtOnePlusE = math.sqrt(1.0 + eccentricity);
                return 2.0 * math.atan2(
                    sqrtOnePlusE * math.sin(halfE),
                    sqrtOneMinusE * math.cos(halfE));
            }
            else
            {
                double hyperbolicAnomaly = SolveKeplerHyperbolic(meanAnomaly, eccentricity);
                double tanHalfNu = math.sqrt((eccentricity + 1.0) / (eccentricity - 1.0))
                    * math.tanh(hyperbolicAnomaly * 0.5);
                return 2.0 * math.atan(tanHalfNu);
            }
        }

        // ----- Conic-equation radius solve (commit 047 Stage 1) -----

        /// <summary>
        /// Predict the next sim-tick at which a vessel on the given Kepler orbit will
        /// reach a specified radial distance from the body center — i.e., the next
        /// solution to <c>r(ν) = targetRadius</c> on the orbit, expressed as an
        /// absolute future sim-tick.
        ///
        /// <para>USAGE:</para>
        /// Shared math for "find when the vessel reaches radius R from the focus":
        /// <list type="bullet">
        ///   <item>SOI outward crossing (commit 046): targetRadius =
        ///   <c>currentBody.SoiRadiusMeters</c>.</item>
        ///   <item>Atmospheric entry (commit 047): targetRadius =
        ///   <c>currentBody.SurfaceRadiusMeters + currentBody.AtmosphericTopAltitudeMeters</c>.</item>
        ///   <item>Surface impact (commit 047): targetRadius =
        ///   <c>currentBody.SurfaceRadiusMeters</c>.</item>
        /// </list>
        /// The helper is body-agnostic — it doesn't take a <see cref="ReferenceBody"/>
        /// parameter, only the target radius scalar. Callers that need body-specific
        /// behavior (e.g., infinite-SOI early returns, child SOI enumeration) handle
        /// that one layer up.
        ///
        /// <para>ALGORITHM:</para>
        /// <list type="number">
        ///   <item>Compute periapsis and apoapsis distances via
        ///   <see cref="PeriapsisDistance"/> and <see cref="ApoapsisDistance"/>.</item>
        ///   <item>Early return null if the orbit is fully inside the target radius
        ///   (<c>rApo &lt; targetRadius</c>) or fully outside
        ///   (<c>rPeri &gt; targetRadius</c>). For hyperbolic orbits
        ///   <c>rApo = +∞</c>, so the fully-inside branch never fires.</item>
        ///   <item>Boundary-hug guard for elliptical orbits only: if both
        ///   <c>|rPeri − targetRadius|</c> and <c>|rApo − targetRadius|</c> are within
        ///   <see cref="BoundaryHugTolerance"/>, return null. Prevents false-positive
        ///   crossings on orbits that numerically hug the target radius (e.g., a
        ///   circular orbit at exactly target). Hyperbolic orbits have no finite
        ///   apoapsis so this check is vacuous and skipped.</item>
        ///   <item>Conic equation: <c>cos(ν) = (p/r − 1) / e</c> where
        ///   <c>p = a(1 − e²)</c>. Defensive range check returns null if
        ///   <c>|cos(ν)| &gt; 1</c> (shouldn't happen given the rPeri/rApo bracket,
        ///   but guards against numerical noise).</item>
        ///   <item>Two ν solutions: <c>+acos(cosNu)</c> (outbound radial crossing,
        ///   between periapsis and apoapsis on increasing-r leg) and
        ///   <c>-acos(cosNu)</c> (inbound radial crossing, between apoapsis and
        ///   periapsis on decreasing-r leg, or pre-periapsis for hyperbolic).
        ///   Convert each to mean anomaly via <see cref="TrueToMeanAnomaly"/>, then
        ///   to seconds-until-event via mean motion. For elliptical orbits, wrap
        ///   mean anomaly into <c>[0, 2π)</c> and take the smallest positive delta.
        ///   For hyperbolic orbits, mean anomaly is monotone and only the candidate
        ///   with a positive delta-from-current is the future crossing.</item>
        ///   <item>Convert seconds-until-event to an absolute tick via
        ///   <c>ceil(seconds / tickIntervalSeconds)</c>. Round-up because warp lands
        ///   ON event ticks exactly (per netcode contract §4.2); rounding down would
        ///   miss the event by a tick.</item>
        /// </list>
        ///
        /// <para>OVERFLOW DEFENSE:</para>
        /// If the predicted absolute tick would exceed <c>long.MaxValue / 2</c>,
        /// returns null. Near-parabolic orbits with stretched periods hit this; the
        /// /2 leaves headroom for <c>currentTick + Δticks</c> addition without
        /// wrapping. Mirrors <see cref="PeriapsisApoapsisPredictor"/>'s defense.
        ///
        /// <para>PARAMETER ORDER:</para>
        /// Matches <see cref="PeriapsisApoapsisPredictor.Predict"/>'s parameter
        /// convention for the four shared parameters (state, currentTick, mu,
        /// tickIntervalSeconds). targetRadius is the unique extra parameter and
        /// sits second after state.
        ///
        /// <para>BEHAVIOR PRESERVATION FROM COMMIT 046:</para>
        /// The math in this helper is the outward closed-form previously inlined in
        /// <c>SoiCrossingPredictor.TryPredictOutwardCrossing</c>. Extracted at
        /// commit 047 Stage 1 so atmospheric-entry and surface-impact predictors
        /// can reuse it. The refactor is bit-exact: same arithmetic in same order,
        /// same boundary-hug tolerance value, same overflow defense.
        /// </summary>
        /// <param name="state">Vessel's current Kepler state (orbital elements + epoch).</param>
        /// <param name="targetRadius">Threshold radius from body center, in meters.
        /// Must be finite and positive; callers handle the infinite-threshold case
        /// (e.g., SOI on top-level body) one layer up.</param>
        /// <param name="currentTick">Current sim-tick; predictions are returned in
        /// absolute tick coordinates.</param>
        /// <param name="mu">Reference body's gravitational parameter μ = G·M in m³/s².</param>
        /// <param name="tickIntervalSeconds">Seconds per sim-tick (1/30 in Phase 1).</param>
        /// <returns>Absolute sim-tick of the next radius crossing, or null if no
        /// crossing is reachable on this orbit (fully inside, fully outside,
        /// boundary-hug, or overflow).</returns>
        public static long? SolveConicAtRadius(
            KeplerState state,
            double targetRadius,
            long currentTick,
            double mu,
            double tickIntervalSeconds)
        {
            if (targetRadius <= 0.0) return null;
            if (double.IsNaN(targetRadius) || double.IsInfinity(targetRadius)) return null;

            double a = state.SemiMajorAxis;
            double e = state.Eccentricity;

            double rPeri = PeriapsisDistance(a, e);
            double rApo = ApoapsisDistance(a, e);

            // Boundary-hug guard for elliptical orbits only. Hyperbolic orbits have
            // rApo = +infinity so the |rApo - target| < tolerance check is vacuously
            // false; skip the branch entirely.
            if (e < 1.0)
            {
                if (math.abs(rPeri - targetRadius) < BoundaryHugTolerance &&
                    math.abs(rApo - targetRadius) < BoundaryHugTolerance)
                {
                    return null;
                }
            }

            // Early returns: orbit fully inside target → no crossing; orbit fully
            // outside target → no crossing. For hyperbolic rApo = +infinity the
            // fully-inside branch is automatically false.
            if (rApo < targetRadius) return null;
            if (rPeri > targetRadius) return null;

            // Circular orbits (e ≈ 0): r is constant. If r != target the orbit never
            // crosses; if r == target the boundary-hug guard above already returned
            // null. Defensive null avoids divide-by-near-zero in cos(ν).
            if (math.abs(e) < 1e-12) return null;

            // Conic equation: r(ν) = p / (1 + e·cos(ν))  ⇒  cos(ν) = (p/r − 1) / e.
            double p = a * (1.0 - e * e);
            double cosNuCrossing = (p / targetRadius - 1.0) / e;

            // Defensive range check (numerical noise can push cosNu just outside
            // [-1, 1] even when the rPeri/rApo bracket says a solution exists).
            if (cosNuCrossing < -1.0 || cosNuCrossing > 1.0) return null;

            double nuCrossing = math.acos(cosNuCrossing);  // in [0, π]

            // Convert both ν candidates (±nuCrossing) to mean anomaly, then compute
            // smallest positive seconds-until-event from current mean anomaly.
            double n = MeanMotion(state.SemiMajorAxis, mu);
            double dtSeconds = (currentTick - state.EpochTick) * tickIntervalSeconds;
            double meanAnomalyAtEpoch = TrueToMeanAnomaly(state.TrueAnomalyAtEpoch, e);
            double meanAnomalyNow = meanAnomalyAtEpoch + n * dtSeconds;

            double mCandidatePos = TrueToMeanAnomaly(nuCrossing, e);
            double mCandidateNeg = TrueToMeanAnomaly(-nuCrossing, e);

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
        /// Smallest positive seconds-until-crossing across two ν candidates,
        /// accounting for the periodic wrap of elliptical mean anomaly.
        /// Internal helper for <see cref="SolveConicAtRadius"/>.
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
        /// Internal helper for <see cref="SolveConicAtRadius"/>.
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

        /// <summary>
        /// Convert seconds-until-event to an absolute sim-tick. Returns null if the
        /// result would exceed <c>long.MaxValue / 2</c> (overflow defense matching
        /// <see cref="PeriapsisApoapsisPredictor"/>'s convention).
        /// Internal helper for <see cref="SolveConicAtRadius"/>.
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

        // ----- Private helpers for anomaly conversions -----

        /// <summary>
        /// Solve Kepler's equation M = E - e·sin(E) for the eccentric anomaly E given
        /// mean anomaly M and eccentricity e (0 ≤ e &lt; 1). Newton-Raphson with Conway's
        /// 1986 starter:
        ///   E_0 = M + e · sin(M) / (1 - sin(M + e) + sin(M))
        ///
        /// Special-cases e == 0 (circular orbit): M = E trivially, no iteration needed.
        /// Defensive denominator guard falls back to E_0 = ±π when Conway's starter
        /// denominator gets pathologically small (e ≈ 1 edge case).
        ///
        /// Extracted from <see cref="KeplerPropagator"/> at commit 045 so the math is
        /// reusable by predictor work. Internal helper for <see cref="MeanToTrueAnomaly"/>.
        /// </summary>
        private static double SolveKeplerElliptic(double meanAnomaly, double e)
        {
            if (e == 0.0)
            {
                return meanAnomaly;
            }

            double sinM = math.sin(meanAnomaly);
            double denominator = 1.0 - math.sin(meanAnomaly + e) + sinM;
            double eccentricAnomaly = math.abs(denominator) > 1e-12
                ? meanAnomaly + e * sinM / denominator
                : (meanAnomaly > 0.0 ? math.PI_DBL : -math.PI_DBL);

            for (int i = 0; i < MaxKeplerIterations; i++)
            {
                double f = eccentricAnomaly - e * math.sin(eccentricAnomaly) - meanAnomaly;
                if (math.abs(f) < KeplerConvergenceTolerance)
                {
                    return eccentricAnomaly;
                }
                double fPrime = 1.0 - e * math.cos(eccentricAnomaly);
                eccentricAnomaly -= f / fPrime;
            }

            Debug.LogWarning(
                $"OrbitalElements.SolveKeplerElliptic: Newton-Raphson did not converge " +
                $"within {MaxKeplerIterations} iterations for M = {meanAnomaly:G17}, e = {e:G17}. " +
                $"Returning best estimate {eccentricAnomaly:G17}; position error may exceed tolerance.");
            return eccentricAnomaly;
        }

        /// <summary>
        /// Solve hyperbolic Kepler equation M = e·sinh(H) - H for H given mean anomaly M
        /// and eccentricity e (e &gt; 1). Newton-Raphson with Conway's logarithmic starter:
        ///   H_0 ≈ M / (e - 1)              for |M| &lt; 4·e
        ///   H_0 ≈ sign(M) · ln(2·|M|/e + 1.8)  for large |M|
        ///
        /// Internal helper for <see cref="MeanToTrueAnomaly"/>. Extracted from
        /// <see cref="KeplerPropagator"/> at commit 045.
        /// </summary>
        private static double SolveKeplerHyperbolic(double meanAnomaly, double e)
        {
            double hyperbolicAnomaly;
            double absM = math.abs(meanAnomaly);
            if (absM < 4.0 * e)
            {
                hyperbolicAnomaly = meanAnomaly / (e - 1.0);
            }
            else
            {
                hyperbolicAnomaly = math.sign(meanAnomaly) * math.log(2.0 * absM / e + 1.8);
            }

            for (int i = 0; i < MaxKeplerIterations; i++)
            {
                double f = e * math.sinh(hyperbolicAnomaly) - hyperbolicAnomaly - meanAnomaly;
                if (math.abs(f) < KeplerConvergenceTolerance)
                {
                    return hyperbolicAnomaly;
                }
                double fPrime = e * math.cosh(hyperbolicAnomaly) - 1.0;
                hyperbolicAnomaly -= f / fPrime;
            }

            Debug.LogWarning(
                $"OrbitalElements.SolveKeplerHyperbolic: Newton-Raphson did not converge " +
                $"within {MaxKeplerIterations} iterations for M = {meanAnomaly:G17}, e = {e:G17}. " +
                $"Returning best estimate {hyperbolicAnomaly:G17}; position error may exceed tolerance.");
            return hyperbolicAnomaly;
        }

        /// <summary>
        /// Inverse hyperbolic tangent. Unity.Mathematics does not expose atanh directly,
        /// so this is the standard identity: atanh(x) = 0.5 · ln((1+x)/(1-x)) for |x| &lt; 1.
        /// </summary>
        private static double Atanh(double x)
        {
            return 0.5 * math.log((1.0 + x) / (1.0 - x));
        }
    }
}
