#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The garden's fixed rules (doc 01 Contenu, sections 8.1 and 8.3): stages, watering, the draws made at planting, the
    /// harvest stars and products, the seeds given back, the garden points and levels, the speed-up cost. Pure functions, no
    /// allocation.
    /// </summary>
    public static class GardenRules
    {
        /// <summary>Stage 2 from 40 % of the growth; stage 3 (ripe) at 100 %.</summary>
        public const double StageTwoAt = 0.4;

        public const int MaxLevel = GardenCatalog.LevelCount;

        /// <summary>The dyes open at garden level 4.</summary>
        public const int DyesLevel = 4;

        public const int SeedBack75Level = 5;
        public const int HappyLevel = 7;
        public const int SeedBack90Level = 9;
        public const int ThreeStarSeedLevel = 10;

        /// <summary>Points: the growth in minutes / 10 rounded, at least 3, + 2 per star above the first.</summary>
        public const int MinPoints = 3;

        public const int PointsPerExtraStar = 2;

        private const uint SeedBackSalt = 0x53454544u;
        private const uint HappySalt = 0x48415059u;

        /// <summary>1 (0 to 40 %), 2 (40 to 100 %), 3 (ripe).</summary>
        public static int Stage(double growth) => growth >= 1.0 ? 3 : growth >= StageTwoAt ? 2 : 1;

        /// <summary>
        /// The growth of a sprout planted now, in ms: the plant's time (spread by Remote Config), twice as fast during the
        /// Floraison or the Garden weekend, × (1 − the Essence tree's « green thumb »), never under a quarter, at least 1 s.
        /// </summary>
        public static long GrowMs(in GardenPlant plant, GardenConfig config, double essenceGrowth)
        {
            double seconds = config.SpreadSeconds(plant.GrowSeconds);
            if (config.FloraisonActive)
                seconds /= Math.Max(1.0, config.FloraisonGrowthMultiplier);
            if (config.WeekendActive)
                seconds /= Math.Max(1.0, config.WeekendGrowthMultiplier);
            seconds *= Math.Max(0.25, 1.0 - Math.Max(0.0, essenceGrowth));
            return Math.Max(1000L, (long)Math.Round(seconds * 1000.0));
        }

        /// <summary>The ripening time once watered: 10 % of the total growth off, never before now nor before the planting.</summary>
        public static long WaterRipe(long plantedMs, long ripeMs, long growMs, double reduction, long nowMs)
        {
            long off = (long)Math.Round(Math.Max(0L, growMs) * Math.Max(0.0, Math.Min(1.0, reduction)));
            return Math.Max(plantedMs, Math.Max(nowMs, ripeMs - off));
        }

        /// <summary>Finish now: 1 Gem per started 10 minutes left, 3 at least (doc Gemmes); 0 when nothing is left.</summary>
        public static int SpeedUpCost(double secondsLeft, GardenConfig config)
        {
            if (!(secondsLeft > 0.0))
                return 0;
            double step = Math.Max(1.0, config.SpeedUpStepMinutes * 60.0);
            long steps = (long)Math.Ceiling(secondsLeft / step - 1e-9);
            long cost = Math.Max(1L, steps) * Math.Max(1, config.SpeedUpGemsPerStep);
            return (int)Math.Min(int.MaxValue, Math.Max(config.SpeedUpMinGems, cost));
        }

        /// <summary>A seed given back: 60 %, 75 % from garden level 5, 90 % from level 9; 100 % during the Garden weekend.</summary>
        public static int SeedBackPercent(int level, GardenConfig config)
        {
            if (config.WeekendActive)
                return 100;
            if (level >= SeedBack90Level)
                return config.SeedBackPercentLevel9;
            if (level >= SeedBack75Level)
                return config.SeedBackPercentLevel5;
            return config.SeedBackPercent;
        }

        /// <summary>The seed given back, drawn at planting with the sprout's seed (same seed, same result).</summary>
        public static byte DrawSeedsBack(uint seed, int percent) =>
            (byte)(percent >= 100 || SeedHash.Unit(seed, SeedBackSalt) * 100f < percent ? 1 : 0);

        /// <summary>« Pousse heureuse »: from garden level 7, 10 % of the sprouts (drawn at planting).</summary>
        public static bool DrawHappy(uint seed, int level, GardenConfig config) =>
            level >= HappyLevel && SeedHash.Unit(seed, HappySalt) * 100f < config.HappyPercent;

        /// <summary>1 picked; 2 watered; 3 watered and a neighbour of another species grew beside it half the time.</summary>
        public static byte Stars(bool watered, bool helped) => (byte)(watered ? (helped ? 3 : 2) : 1);

        /// <summary>The main products: the plant's count, +1 at 3 stars, doubled by « Pousse heureuse ».</summary>
        public static int MainCount(in GardenPlant plant, byte stars, bool happy)
        {
            int count = Math.Max(0, plant.MainCount) + (stars >= 3 ? 1 : 0);
            return happy ? count * 2 : count;
        }

        public static int SideCount(in GardenPlant plant, bool happy)
        {
            int count = plant.Side.IsValid ? Math.Max(0, plant.SideCount) : 0;
            return happy ? count * 2 : count;
        }

        /// <summary>Seeds back at the harvest: the one drawn at planting, +1 at 3 stars from garden level 10.</summary>
        public static int SeedsBack(byte drawn, byte stars, int level) => drawn + (stars >= 3 && level >= ThreeStarSeedLevel ? 1 : 0);

        /// <summary>Garden points of a harvest: the plant's growth in minutes / 10 rounded (at least 3), + 2 per star above the first.</summary>
        public static int Points(double growSeconds, byte stars)
        {
            int baseline = (int)Math.Round(growSeconds / 60.0 / 10.0, MidpointRounding.AwayFromZero);
            return Math.Max(MinPoints, baseline) + PointsPerExtraStar * Math.Max(0, stars - 1);
        }

        /// <summary>The garden level of cumulated points (table 8.3), 1 to 10.</summary>
        public static int LevelOf(int points)
        {
            var levels = GardenCatalog.Levels;
            int level = 1;
            for (int i = 0; i < levels.Length; i++)
                if (points >= levels[i].Points)
                    level = levels[i].Level;
            return level;
        }

        /// <summary>Points inside the current level, and the points its next level needs (0 at level 10).</summary>
        public static int PointsInLevel(int points)
        {
            int level = LevelOf(points);
            return Math.Max(0, points - GardenCatalog.Levels[level - 1].Points);
        }

        public static int PointsForNext(int points)
        {
            int level = LevelOf(points);
            if (level >= MaxLevel)
                return 0;
            return GardenCatalog.Levels[level].Points - GardenCatalog.Levels[level - 1].Points;
        }

        /// <summary>The time two intervals [a0, a1] and [b0, b1] share, in ms (0 when apart).</summary>
        public static long Overlap(long a0, long a1, long b0, long b1)
        {
            long start = Math.Max(a0, b0), end = Math.Min(a1, b1);
            return end > start ? end - start : 0L;
        }

        /// <summary>« Les voisines s'entraident »: a neighbour's help covers at least half of the growth.</summary>
        public static bool HelpEnough(long helpMs, long growthMs) => growthMs > 0L ? 2L * helpMs >= growthMs : helpMs > 0L;
    }
}
