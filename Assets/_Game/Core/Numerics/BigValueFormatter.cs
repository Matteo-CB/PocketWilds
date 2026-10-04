#nullable enable
using System;

namespace PocketWilds.Core
{
    /// <summary>Locale separators for player-facing numbers. Suffixes stay universal (K, M, B, T, aa...).</summary>
    public readonly struct NumberFormatSettings
    {
        public static readonly NumberFormatSettings Invariant = new NumberFormatSettings('.', ',');

        public readonly char DecimalSeparator;

        /// <summary>'\0' disables grouping.</summary>
        public readonly char GroupSeparator;

        public NumberFormatSettings(char decimalSeparator, char groupSeparator)
        {
            DecimalSeparator = decimalSeparator;
            GroupSeparator = groupSeparator;
        }
    }

    /// <summary>
    /// Allocation-free player-facing formatting, following the Writing Bible:
    /// "0", "4.5", "999", "1,250", "9,999", "12.4K", "124K", "2.1M", "3.75B", "1T", "1aa"... "9.99zz", then "1.23e2048".
    /// Values are floored at the shown precision so the counter never shows more than the player owns.
    /// </summary>
    public static class BigValueFormatter
    {
        /// <summary>Destination buffers must hold at least this many chars.</summary>
        public const int MaxLength = 32;

        private const int FirstLetterTier = 5;
        private const int LetterTierCount = 26 * 26;
        private static readonly char[] NamedSuffixes = { '\0', 'K', 'M', 'B', 'T' };

        public static int Format(BigValue value, Span<char> destination, in NumberFormatSettings settings)
        {
            if (destination.Length < MaxLength)
                throw new ArgumentException("Destination too small for BigValueFormatter.", nameof(destination));

            // Anything below 0.1 in magnitude displays as "0" (one decimal max), never "-0".
            if (value.IsZero || value.Exponent10 < -1)
            {
                destination[0] = '0';
                return 1;
            }

            int pos = 0;
            double mantissa = value.Mantissa;
            int exponent = value.Exponent10;
            if (mantissa < 0.0)
            {
                destination[pos++] = '-';
                mantissa = -mantissa;
            }

            if (exponent < 4)
                return FormatSmall(mantissa, exponent, destination, pos, settings);

            int tier = exponent / 3;
            if (tier >= FirstLetterTier + LetterTierCount)
                return FormatScientific(mantissa, exponent, destination, pos, settings);

            // Three significant digits, floored. Epsilon absorbs binary representation error (1.24 -> 123.999...).
            long significant = (long)Math.Floor(mantissa * 100.0 + 1e-7);
            if (significant > 999)
                significant = 999;
            if (significant < 100)
                significant = 100;

            int integerDigits = exponent - tier * 3 + 1;
            pos = WriteSignificant(significant, integerDigits, destination, pos, settings.DecimalSeparator);

            if (tier < FirstLetterTier)
            {
                destination[pos++] = NamedSuffixes[tier];
            }
            else
            {
                int letterIndex = tier - FirstLetterTier;
                destination[pos++] = (char)('a' + letterIndex / 26);
                destination[pos++] = (char)('a' + letterIndex % 26);
            }

            return pos;
        }

        /// <summary>Convenience overload for UI text buffers (TMP SetText(char[], start, length)).</summary>
        public static int Format(BigValue value, char[] buffer, int start, in NumberFormatSettings settings)
        {
            return Format(value, new Span<char>(buffer, start, buffer.Length - start), settings);
        }

        /// <summary>Debug and log helper. Allocates a string.</summary>
        public static string ToDebugString(BigValue value)
        {
            Span<char> buffer = stackalloc char[MaxLength];
            int length = Format(value, buffer, NumberFormatSettings.Invariant);
            return new string(buffer.Slice(0, length));
        }

        private static int FormatSmall(double mantissa, int exponent, Span<char> destination, int pos, in NumberFormatSettings settings)
        {
            double value = BigValue.ScaleByPowerOf10(mantissa, exponent);

            if (value < 100.0)
            {
                long tenths = (long)Math.Floor(value * 10.0 + 1e-7);
                if (tenths == 0)
                {
                    destination[0] = '0';
                    return 1;
                }
                long whole = tenths / 10;
                int fraction = (int)(tenths % 10);
                pos = WriteInteger(whole, destination, pos, settings.GroupSeparator);
                if (fraction != 0)
                {
                    destination[pos++] = settings.DecimalSeparator;
                    destination[pos++] = (char)('0' + fraction);
                }
                return pos;
            }

            long integer = (long)Math.Floor(value + 1e-7);
            return WriteInteger(integer, destination, pos, settings.GroupSeparator);
        }

        private static int FormatScientific(double mantissa, int exponent, Span<char> destination, int pos, in NumberFormatSettings settings)
        {
            long significant = (long)Math.Floor(mantissa * 100.0 + 1e-7);
            if (significant > 999)
                significant = 999;
            pos = WriteSignificant(significant, 1, destination, pos, settings.DecimalSeparator);
            destination[pos++] = 'e';
            return WriteInteger(exponent, destination, pos, '\0');
        }

        /// <summary>Writes a 3-digit significand with the decimal point after <paramref name="integerDigits"/>, trimming trailing zeros.</summary>
        private static int WriteSignificant(long significant, int integerDigits, Span<char> destination, int pos, char decimalSeparator)
        {
            int d0 = (int)(significant / 100);
            int d1 = (int)(significant / 10 % 10);
            int d2 = (int)(significant % 10);

            destination[pos++] = (char)('0' + d0);
            if (integerDigits >= 2)
                destination[pos++] = (char)('0' + d1);
            if (integerDigits >= 3)
                destination[pos++] = (char)('0' + d2);

            if (integerDigits == 1)
            {
                if (d1 != 0 || d2 != 0)
                {
                    destination[pos++] = decimalSeparator;
                    destination[pos++] = (char)('0' + d1);
                    if (d2 != 0)
                        destination[pos++] = (char)('0' + d2);
                }
            }
            else if (integerDigits == 2 && d2 != 0)
            {
                destination[pos++] = decimalSeparator;
                destination[pos++] = (char)('0' + d2);
            }

            return pos;
        }

        private static int WriteInteger(long value, Span<char> destination, int pos, char groupSeparator)
        {
            if (value < 0)
            {
                destination[pos++] = '-';
                value = -value;
            }

            if (value == 0)
            {
                destination[pos++] = '0';
                return pos;
            }

            Span<char> reversed = stackalloc char[28];
            int count = 0;
            int digits = 0;
            while (value > 0)
            {
                if (groupSeparator != '\0' && digits > 0 && digits % 3 == 0)
                    reversed[count++] = groupSeparator;
                reversed[count++] = (char)('0' + (int)(value % 10));
                value /= 10;
                digits++;
            }

            for (int i = count - 1; i >= 0; i--)
                destination[pos++] = reversed[i];
            return pos;
        }
    }
}
