#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The adaptive difficulty of an activity (doc 01 Contenu, section 12.0 « Difficulté qui s'adapte »; doc Mini-jeux,
    /// section 2): d from 1 to 10, per player and per activity, starting at 3. After 2 free-mode games in a row at 0 stars
    /// the next is a little easier (d − 1, never under 1); after 2 in a row at 3 stars a little harder (d + 1, never over
    /// 10); otherwise d stays. Practice games count; challenges never adapt. The counts and the step are Remote Config
    /// (activities.adaptive.*). Parameters are given at d = 1, 5 and 10 and joined by straight lines, rounded. Pure
    /// functions, no allocation.
    /// </summary>
    public static class AdaptiveDifficulty
    {
        public const int Min = 1;
        public const int Max = 10;
        public const int Start = 3;

        public static int Clamp(int d) => d < Min ? Min : d > Max ? Max : d;

        /// <summary>The record's d (a never-adapted record is at <see cref="Start"/>).</summary>
        public static int Level(in ActivityRecord record) => record.Difficulty == 0 ? Start : Clamp(record.Difficulty);

        /// <summary>
        /// Counts one finished free-mode game (rewarded or practice) and moves d when a streak is reached; the streak then
        /// starts again. Returns the change (−levels, 0 or +levels).
        /// </summary>
        public static int Apply(ref ActivityRecord record, int stars, ActivityConfig config)
        {
            int before = Level(in record);
            int d = before;
            if (stars <= 0)
            {
                record.ThreeStarStreak = 0;
                record.ZeroStarStreak = Increment(record.ZeroStarStreak);
                if (record.ZeroStarStreak >= Math.Max(1, config.EasierAfterFails))
                {
                    d = Clamp(d - config.AdaptiveLevels);
                    record.ZeroStarStreak = 0;
                }
            }
            else if (stars >= 3)
            {
                record.ZeroStarStreak = 0;
                record.ThreeStarStreak = Increment(record.ThreeStarStreak);
                if (record.ThreeStarStreak >= Math.Max(1, config.HarderAfterPerfects))
                {
                    d = Clamp(d + config.AdaptiveLevels);
                    record.ThreeStarStreak = 0;
                }
            }
            else
            {
                record.ZeroStarStreak = 0;
                record.ThreeStarStreak = 0;
            }
            record.Difficulty = (byte)d;
            return d - before;
        }

        /// <summary>
        /// A parameter at d: straight lines from d1 to d5 and from d5 to d10, rounded to <paramref name="decimals"/> digits
        /// (half away from zero).
        /// </summary>
        public static double Param(double at1, double at5, double at10, int d, int decimals)
        {
            d = Clamp(d);
            double value = d <= 5 ? at1 + (at5 - at1) * (d - 1) / 4.0 : at5 + (at10 - at5) * (d - 5) / 5.0;
            return Math.Round(value, Math.Max(0, Math.Min(15, decimals)), MidpointRounding.AwayFromZero);
        }

        /// <summary>A whole-number parameter at d (zones, debris, cards).</summary>
        public static int Param(int at1, int at5, int at10, int d) => (int)Param(at1, at5, (double)at10, d, 0);

        private static byte Increment(byte value) => value < byte.MaxValue ? (byte)(value + 1) : value;
    }
}
