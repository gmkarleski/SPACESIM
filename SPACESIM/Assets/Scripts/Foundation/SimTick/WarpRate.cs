using System;

namespace SpaceSim.Foundation.SimTick
{
    /// <summary>
    /// Time-warp rate represented as a rational number (numerator / denominator).
    /// Used by the time-warp controller (lands in commit 048 Stage 2) to scale
    /// sim-tick advancement per real-time tick.
    ///
    /// <para>
    /// <strong>RATIONAL REPRESENTATION (not float).</strong> Float warp multipliers
    /// accumulate error over long warp sessions; a rational representation keeps the
    /// warp rate exact for deterministic sim-tick advancement. The advancement math
    /// (see <see cref="AdvanceTicks"/>) uses integer arithmetic on
    /// numerator/denominator, with a <c>pendingNumerator</c> accumulator that
    /// carries fractional-tick remainder across calls for rates whose denominator
    /// is greater than 1.
    /// </para>
    ///
    /// <para>
    /// <strong>V1 IS INTEGER-ONLY CONTINUOUS MODE.</strong> The denominator is
    /// always 1 in v1: discrete-step warp rates (1, 5, 10, 100, 1000, 10000,
    /// 100000) and continuous integer warp rates (1..1000) all have denominator
    /// = 1. The rational infrastructure is wired up now so future continuous
    /// fractional modes (tenths, quarter-steps, etc.) can use it without
    /// schema or type churn.
    /// </para>
    ///
    /// <para>
    /// <strong>MODE INFORMATION LIVES ELSEWHERE.</strong> Whether a particular
    /// rate is "discrete level 100" or "continuous 100x" or "target-tick scheduled
    /// 100x" is UI/controller state, not a property of the rate itself. This type
    /// only carries the scalar ratio that the sim-tick math needs.
    /// </para>
    /// </summary>
    public readonly struct WarpRate : IEquatable<WarpRate>
    {
        /// <summary>Numerator of the rational rate. May be zero (paused).</summary>
        public readonly long Numerator;

        /// <summary>Denominator of the rational rate. Always positive (validated at construction).</summary>
        public readonly long Denominator;

        /// <summary>
        /// Construct a <see cref="WarpRate"/> from an explicit numerator and denominator.
        /// Validates that the denominator is positive (zero or negative throws
        /// <see cref="ArgumentException"/>). Does not reduce the fraction — callers
        /// pass already-reduced values, and the helper factories
        /// (<see cref="Paused"/>, <see cref="OneX"/>, <see cref="Discrete"/>,
        /// <see cref="Continuous"/>) produce reduced values by construction.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="denominator"/>
        /// is zero or negative.</exception>
        public WarpRate(long numerator, long denominator)
        {
            if (denominator <= 0)
            {
                throw new ArgumentException(
                    $"WarpRate denominator must be positive; got {denominator}. " +
                    $"Use WarpRate.Paused for the zero rate.",
                    nameof(denominator));
            }
            Numerator = numerator;
            Denominator = denominator;
        }

        /// <summary>The paused rate (0/1). No sim-tick advancement occurs at this rate.</summary>
        public static WarpRate Paused => new WarpRate(0, 1);

        /// <summary>The 1x rate (1/1). One sim-tick advance per real-time tick.</summary>
        public static WarpRate OneX => new WarpRate(1, 1);

        /// <summary>
        /// Construct a discrete-step warp rate. Valid levels are
        /// {1, 5, 10, 100, 1000, 10000, 100000}; any other value throws
        /// <see cref="ArgumentException"/>. Denominator is always 1
        /// (integer-only in v1).
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="level"/>
        /// is not one of the supported discrete levels.</exception>
        public static WarpRate Discrete(long level)
        {
            switch (level)
            {
                case 1:
                case 5:
                case 10:
                case 100:
                case 1000:
                case 10000:
                case 100000:
                    return new WarpRate(level, 1);
                default:
                    throw new ArgumentException(
                        $"Discrete warp level must be one of {{1, 5, 10, 100, 1000, 10000, 100000}}; " +
                        $"got {level}.",
                        nameof(level));
            }
        }

        /// <summary>
        /// Construct a continuous integer warp rate in the range [1, 1000]; any
        /// value outside this range throws <see cref="ArgumentException"/>.
        /// Denominator is always 1 (integer-only in v1). The 1000 ceiling matches
        /// the practical UI-rate cap before discrete levels (10000, 100000) take over.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="integerRate"/>
        /// is outside [1, 1000].</exception>
        public static WarpRate Continuous(long integerRate)
        {
            if (integerRate < 1 || integerRate > 1000)
            {
                throw new ArgumentException(
                    $"Continuous warp rate must be in [1, 1000]; got {integerRate}.",
                    nameof(integerRate));
            }
            return new WarpRate(integerRate, 1);
        }

        /// <summary>True when the rate is the paused rate (numerator zero).</summary>
        public bool IsPaused => Numerator == 0;

        /// <summary>
        /// Advance the sim-tick counter by <paramref name="realTimeTicks"/> real-time
        /// ticks at this warp rate, carrying any fractional remainder forward via
        /// <paramref name="pendingNumerator"/>.
        ///
        /// <para>
        /// Returns a tuple <c>(simTicksAdvance, newPendingNumerator)</c> where
        /// <c>simTicksAdvance</c> is the integer number of sim-ticks to advance this
        /// call, and <c>newPendingNumerator</c> is the carried-forward remainder
        /// for the next call. The caller is responsible for storing
        /// <c>newPendingNumerator</c> across calls.
        /// </para>
        ///
        /// <para>
        /// Math: <c>total = realTimeTicks * Numerator + pendingNumerator</c>;
        /// <c>simTicksAdvance = total / Denominator</c> (integer division);
        /// <c>newPendingNumerator = total % Denominator</c>.
        /// </para>
        ///
        /// <para>
        /// In v1 (integer-only continuous mode) <c>Denominator</c> is always 1, so
        /// <c>pendingNumerator</c> stays zero and <c>simTicksAdvance</c> reduces to
        /// <c>realTimeTicks * Numerator</c>. The pending-numerator accumulator is
        /// kept in the signature for future fractional-rate modes.
        /// </para>
        /// </summary>
        internal (long simTicksAdvance, long newPendingNumerator) AdvanceTicks(
            long realTimeTicks, long pendingNumerator)
        {
            long total = realTimeTicks * Numerator + pendingNumerator;
            long simTicksAdvance = total / Denominator;
            long newPendingNumerator = total % Denominator;
            return (simTicksAdvance, newPendingNumerator);
        }

        /// <inheritdoc />
        public bool Equals(WarpRate other)
        {
            return Numerator == other.Numerator && Denominator == other.Denominator;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is WarpRate other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Numerator.GetHashCode() * 397) ^ Denominator.GetHashCode();
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Denominator == 1
                ? $"{Numerator}x"
                : $"{Numerator}/{Denominator}x";
        }

        public static bool operator ==(WarpRate left, WarpRate right) => left.Equals(right);
        public static bool operator !=(WarpRate left, WarpRate right) => !left.Equals(right);
    }
}
