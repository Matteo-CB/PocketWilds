#nullable enable
using System;

namespace PocketWilds.Core
{
    /// <summary>
    /// Compact economic number: Mantissa x 10^Exponent10, with |Mantissa| in [1, 10) or exactly 0.
    /// Immutable struct, no heap allocation in any operation. Used for Light and every value
    /// that can grow without practical limit. Not for content constants that fit a double.
    /// </summary>
    public readonly struct BigValue : IEquatable<BigValue>, IComparable<BigValue>
    {
        public const int MaxExponent = 1_000_000_000;
        public const int MinExponent = -1_000_000_000;

        /// <summary>Beyond this exponent gap the smaller operand cannot change a double mantissa.</summary>
        private const int AddPrecisionDigits = 17;

        public static readonly BigValue Zero = default;
        public static readonly BigValue One = new BigValue(1.0, 0);
        public static readonly BigValue MaxValue = new BigValue(9.999999999999998, MaxExponent);

        public readonly double Mantissa;
        public readonly int Exponent10;

        private BigValue(double mantissa, int exponent10)
        {
            Mantissa = mantissa;
            Exponent10 = exponent10;
        }

        public bool IsZero => Mantissa == 0.0;
        public bool IsNegative => Mantissa < 0.0;
        public int Sign => Mantissa > 0.0 ? 1 : (Mantissa < 0.0 ? -1 : 0);

        /// <summary>Always true for values built through the public API. Kept for defensive checks on loaded data.</summary>
        public bool IsFinite => !double.IsNaN(Mantissa) && !double.IsInfinity(Mantissa);

        // ---------- Construction ----------

        public static BigValue FromDouble(double value) => Normalize(value, 0);

        public static BigValue FromMantissaExponent(double mantissa, long exponent10) => Normalize(mantissa, exponent10);

        /// <summary>
        /// Brings any (mantissa, exponent) pair into canonical form. NaN becomes zero,
        /// infinities and exponent overflow saturate to +/- MaxValue, underflow becomes zero.
        /// </summary>
        public static BigValue Normalize(double mantissa, long exponent10)
        {
            if (mantissa == 0.0 || double.IsNaN(mantissa))
                return Zero;
            if (double.IsInfinity(mantissa))
                return mantissa > 0.0 ? MaxValue : Negate(MaxValue);

            double abs = mantissa < 0.0 ? -mantissa : mantissa;

            // Fast paths cover almost every result of add/multiply.
            if (abs >= 1.0 && abs < 10.0)
                return Clamp(mantissa, exponent10);
            if (abs >= 10.0 && abs < 100.0)
                return Clamp(mantissa / 10.0, exponent10 + 1);
            if (abs >= 0.1 && abs < 1.0)
                return Clamp(mantissa * 10.0, exponent10 - 1);

            int shift = (int)Math.Floor(Math.Log10(abs));
            mantissa = ScaleByPowerOf10(mantissa, -shift);
            exponent10 += shift;

            abs = mantissa < 0.0 ? -mantissa : mantissa;
            if (abs >= 10.0)
            {
                mantissa /= 10.0;
                exponent10++;
            }
            else if (abs < 1.0)
            {
                mantissa *= 10.0;
                exponent10--;
            }

            return Clamp(mantissa, exponent10);
        }

        private static BigValue Clamp(double mantissa, long exponent10)
        {
            if (exponent10 > MaxExponent)
                return mantissa > 0.0 ? MaxValue : Negate(MaxValue);
            if (exponent10 < MinExponent)
                return Zero;
            return new BigValue(mantissa, (int)exponent10);
        }

        // ---------- Arithmetic ----------

        public static BigValue Negate(BigValue value) => new BigValue(-value.Mantissa, value.Exponent10);

        public static BigValue Abs(BigValue value) =>
            value.Mantissa < 0.0 ? new BigValue(-value.Mantissa, value.Exponent10) : value;

        public static BigValue Add(BigValue a, BigValue b)
        {
            if (a.Mantissa == 0.0)
                return b;
            if (b.Mantissa == 0.0)
                return a;

            long diff = (long)a.Exponent10 - b.Exponent10;
            if (diff > AddPrecisionDigits)
                return a;
            if (diff < -AddPrecisionDigits)
                return b;

            if (diff >= 0)
                return Normalize(a.Mantissa + b.Mantissa / PowersOf10[diff], a.Exponent10);
            return Normalize(b.Mantissa + a.Mantissa / PowersOf10[-diff], b.Exponent10);
        }

        public static BigValue Subtract(BigValue a, BigValue b) => Add(a, Negate(b));

        public static BigValue Multiply(BigValue a, BigValue b)
        {
            if (a.Mantissa == 0.0 || b.Mantissa == 0.0)
                return Zero;
            return Normalize(a.Mantissa * b.Mantissa, (long)a.Exponent10 + b.Exponent10);
        }

        public static BigValue Multiply(BigValue a, double scalar) => Multiply(a, FromDouble(scalar));

        public static BigValue Divide(BigValue a, BigValue b)
        {
            if (b.Mantissa == 0.0)
                throw new DivideByZeroException("BigValue division by zero.");
            if (a.Mantissa == 0.0)
                return Zero;
            return Normalize(a.Mantissa / b.Mantissa, (long)a.Exponent10 - b.Exponent10);
        }

        public static BigValue Divide(BigValue a, double scalar) => Divide(a, FromDouble(scalar));

        /// <summary>
        /// value^exponent for value >= 0 and exponent >= 0. Used by cost curves (e.g. 10 x 1.18^n).
        /// </summary>
        public static BigValue PowPositive(BigValue value, double exponent)
        {
            if (double.IsNaN(exponent) || exponent < 0.0)
                throw new ArgumentOutOfRangeException(nameof(exponent), "PowPositive needs a non-negative exponent.");
            if (value.Mantissa < 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "PowPositive needs a non-negative base.");
            if (exponent == 0.0)
                return One;
            if (value.Mantissa == 0.0)
                return Zero;

            double log = (Math.Log10(value.Mantissa) + value.Exponent10) * exponent;
            if (log > MaxExponent)
                return MaxValue;
            if (log < MinExponent)
                return Zero;

            double whole = Math.Floor(log);
            return Normalize(Math.Pow(10.0, log - whole), (long)whole);
        }

        /// <summary>log10(value). Zero returns negative infinity, negative values return NaN.</summary>
        public static double Log10(BigValue value)
        {
            if (value.Mantissa == 0.0)
                return double.NegativeInfinity;
            if (value.Mantissa < 0.0)
                return double.NaN;
            return Math.Log10(value.Mantissa) + value.Exponent10;
        }

        public static BigValue ClampMin(BigValue value, BigValue min) => value.CompareTo(min) < 0 ? min : value;

        public static BigValue Max(BigValue a, BigValue b) => a.CompareTo(b) >= 0 ? a : b;

        public static BigValue Min(BigValue a, BigValue b) => a.CompareTo(b) <= 0 ? a : b;

        /// <summary>Converts to double, saturating to +/- double.MaxValue and flushing tiny values to 0.</summary>
        public double ToDoubleClamped()
        {
            if (Mantissa == 0.0)
                return 0.0;
            if (Exponent10 > 308)
                return Mantissa > 0.0 ? double.MaxValue : -double.MaxValue;
            if (Exponent10 < -323)
                return 0.0;

            double result = ScaleByPowerOf10(Mantissa, Exponent10);
            if (double.IsInfinity(result))
                return result > 0.0 ? double.MaxValue : -double.MaxValue;
            return result;
        }

        /// <summary>Relative comparison for tests and tolerance checks.</summary>
        public static bool ApproximatelyEqual(BigValue a, BigValue b, double relativeTolerance)
        {
            if (a.Mantissa == 0.0 || b.Mantissa == 0.0)
                return a.Mantissa == b.Mantissa;
            var diff = Abs(Subtract(a, b));
            var scale = Max(Abs(a), Abs(b));
            return Divide(diff, scale).ToDoubleClamped() <= relativeTolerance;
        }

        // ---------- Comparison ----------

        public int CompareTo(BigValue other)
        {
            int sa = Sign;
            int sb = other.Sign;
            if (sa != sb)
                return sa < sb ? -1 : 1;
            if (sa == 0)
                return 0;

            int magnitude;
            if (Exponent10 != other.Exponent10)
                magnitude = Exponent10 < other.Exponent10 ? -1 : 1;
            else
            {
                double am = Mantissa < 0.0 ? -Mantissa : Mantissa;
                double bm = other.Mantissa < 0.0 ? -other.Mantissa : other.Mantissa;
                magnitude = am < bm ? -1 : (am > bm ? 1 : 0);
            }

            return sa > 0 ? magnitude : -magnitude;
        }

        public bool Equals(BigValue other) => Mantissa.Equals(other.Mantissa) && Exponent10 == other.Exponent10;
        public override bool Equals(object? obj) => obj is BigValue other && Equals(other);
        public override int GetHashCode() => unchecked((Mantissa.GetHashCode() * 397) ^ Exponent10);

        // ---------- Operators ----------

        public static BigValue operator +(BigValue a, BigValue b) => Add(a, b);
        public static BigValue operator -(BigValue a, BigValue b) => Subtract(a, b);
        public static BigValue operator -(BigValue a) => Negate(a);
        public static BigValue operator *(BigValue a, BigValue b) => Multiply(a, b);
        public static BigValue operator *(BigValue a, double b) => Multiply(a, b);
        public static BigValue operator /(BigValue a, BigValue b) => Divide(a, b);
        public static BigValue operator /(BigValue a, double b) => Divide(a, b);
        public static bool operator ==(BigValue a, BigValue b) => a.Equals(b);
        public static bool operator !=(BigValue a, BigValue b) => !a.Equals(b);
        public static bool operator <(BigValue a, BigValue b) => a.CompareTo(b) < 0;
        public static bool operator >(BigValue a, BigValue b) => a.CompareTo(b) > 0;
        public static bool operator <=(BigValue a, BigValue b) => a.CompareTo(b) <= 0;
        public static bool operator >=(BigValue a, BigValue b) => a.CompareTo(b) >= 0;

        /// <summary>Debug only. Allocates.</summary>
        public override string ToString() => BigValueFormatter.ToDebugString(this);

        // ---------- Powers of ten ----------

        internal static readonly double[] PowersOf10 = BuildPowersOf10();

        private static double[] BuildPowersOf10()
        {
            var table = new double[309];
            for (int i = 0; i < table.Length; i++)
                table[i] = double.Parse("1e" + i, System.Globalization.CultureInfo.InvariantCulture);
            return table;
        }

        internal static double ScaleByPowerOf10(double value, long power)
        {
            while (power > 308)
            {
                value *= 1e308;
                power -= 308;
            }
            while (power < -308)
            {
                value /= 1e308;
                power += 308;
            }
            return power >= 0 ? value * PowersOf10[power] : value / PowersOf10[-power];
        }
    }
}
