#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Pure formulas of doc 02 Améliorations: prices, the Max purchase, level curves, calendar days.</summary>
    public static class ProgressionMath
    {
        /// <summary>base x growth^n, rounded to whole Light while that matters (below 10^15).</summary>
        public static BigValue Price(double baseValue, double growth, double n)
        {
            if (baseValue <= 0.0)
                return BigValue.Zero;
            double log10 = Math.Log10(baseValue) + n * Math.Log10(growth);
            return FromLog10Rounded(log10);
        }

        /// <summary>Total of <paramref name="count"/> successive levels from a first price: first x (r^n - 1) / (r - 1).</summary>
        public static BigValue GeometricTotal(BigValue first, double growth, int count)
        {
            if (count <= 0 || first.IsZero)
                return BigValue.Zero;
            if (count == 1)
                return first;
            double growthPow = count * Math.Log10(growth);
            // (r^n - 1) / (r - 1), kept in log space when r^n is beyond a double.
            BigValue factor = growthPow < 300.0
                ? BigValue.FromDouble((Math.Pow(growth, count) - 1.0) / (growth - 1.0))
                : BigValue.FromMantissaExponent(Math.Pow(10.0, growthPow - Math.Floor(growthPow)) / (growth - 1.0), (long)Math.Floor(growthPow));
            return Round(BigValue.Multiply(first, factor));
        }

        /// <summary>
        /// How many successive levels a budget pays for, by the closed geometric sum (no loop over levels):
        /// n = floor(log_r(1 + budget x (r - 1) / first)), corrected by one step either way and capped at the limit.
        /// </summary>
        public static int MaxAffordable(BigValue first, double growth, BigValue budget, int limit)
        {
            if (limit <= 0 || first.IsZero || budget < first)
                return 0;
            double ratio = BigValue.Log10(budget) - BigValue.Log10(first) + Math.Log10(growth - 1.0);
            // log10(1 + x) without overflow: x is 10^ratio.
            double log1p = ratio > 15.0 ? ratio : Math.Log10(1.0 + Math.Pow(10.0, ratio));
            double estimate = Math.Floor(log1p / Math.Log10(growth));
            int n = estimate >= limit ? limit : (int)Math.Max(0.0, estimate);
            // Rounding of the logs can be one off at an exact boundary: settle it with the closed total.
            while (n > 0 && GeometricTotal(first, growth, n) > budget)
                n--;
            while (n < limit && GeometricTotal(first, growth, n + 1) <= budget)
                n++;
            return n;
        }

        /// <summary>Refuge score needed for a level: round(mult x (L - 1)^exp). Level 1 needs 0.</summary>
        public static long RefugeScoreForLevel(ProgressionConfig config, int level)
        {
            if (level <= 1)
                return 0;
            return (long)Math.Round(config.RefugeCurveMult * Math.Pow(level - 1, config.RefugeCurveExp), MidpointRounding.AwayFromZero);
        }

        /// <summary>Refuge level a score reaches (no maximum).</summary>
        public static int RefugeLevelForScore(ProgressionConfig config, long score)
        {
            if (score <= 0)
                return 1;
            // Inverse of the curve, then corrected for the rounding of each threshold.
            int level = 1 + (int)Math.Floor(Math.Pow(score / config.RefugeCurveMult, 1.0 / config.RefugeCurveExp));
            while (level > 1 && RefugeScoreForLevel(config, level) > score)
                level--;
            while (RefugeScoreForLevel(config, level + 1) <= score)
                level++;
            return level;
        }

        /// <summary>Biome mastery XP to go from level L - 1 to L: round(mult x L^exp).</summary>
        public static long MasteryXpForLevel(ProgressionConfig config, int level)
        {
            if (level <= 0)
                return 0;
            return (long)Math.Round(config.MasteryCurveMult * Math.Pow(level, config.MasteryCurveExp), MidpointRounding.AwayFromZero);
        }

        /// <summary>Biome mastery level reached with this much XP in total, and the XP already inside the next level.</summary>
        public static int MasteryLevelForXp(ProgressionConfig config, long totalXp, out long xpInLevel)
        {
            int level = 0;
            long left = Math.Max(0L, totalXp);
            while (true)
            {
                long need = MasteryXpForLevel(config, level + 1);
                if (need <= 0 || left < need)
                    break;
                left -= need;
                level++;
            }
            xpInLevel = left;
            return level;
        }

        /// <summary>Local calendar day as yyyymmdd.</summary>
        public static int LocalDayKey(DateTimeOffset utc, TimeZoneInfo zone)
        {
            var local = TimeZoneInfo.ConvertTime(utc, zone).DateTime;
            return local.Year * 10000 + local.Month * 100 + local.Day;
        }

        /// <summary>
        /// Calendar day since install, the install day being day 1 (doc 02, section 5.1). A clock moved backwards
        /// counts from the latest time the profile has seen.
        /// </summary>
        public static int DayNumber(PlayerState state, DateTimeOffset utcNow, TimeZoneInfo zone)
        {
            var now = utcNow > state.Offline.HighWaterUtc ? utcNow : state.Offline.HighWaterUtc;
            var installed = TimeZoneInfo.ConvertTime(state.Meta.CreatedUtc, zone).Date;
            var today = TimeZoneInfo.ConvertTime(now, zone).Date;
            return Math.Max(1, (int)(today - installed).TotalDays + 1);
        }

        private static BigValue FromLog10Rounded(double log10)
        {
            if (log10 < 15.0)
                return BigValue.FromDouble(Math.Round(Math.Pow(10.0, log10), MidpointRounding.AwayFromZero));
            long exponent = (long)Math.Floor(log10);
            return BigValue.FromMantissaExponent(Math.Pow(10.0, log10 - exponent), exponent);
        }

        private static BigValue Round(BigValue value) =>
            value.Exponent10 >= 15 ? value : BigValue.FromDouble(Math.Round(value.ToDoubleClamped(), MidpointRounding.AwayFromZero));
    }
}
