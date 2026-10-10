#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Every number of the garden (doc 01 Contenu, section 8.1; doc 08 Remote Config: domain garden, gems.price.garden_*,
    /// gems.garden_speedup_daily_cap, events.weekend.garden_multiplier, flags.garden). Defaults are the docs'; <see cref="TryOverride"/>
    /// applies a Remote Config value inside its bounds and ignores anything else (a remote change never edits a save: a plot
    /// once owned stays owned, a sprout keeps the times and draws of its planting).
    /// </summary>
    public sealed class GardenConfig
    {
        public static GardenConfig Default { get; } = new GardenConfig();

        /// <summary>The free plots (doc 01: 3), never lowered by a remote value.</summary>
        public const int DefaultPlotsBase = 3;

        // ---- Plots (garden.plots_base, garden.plots_club_bonus, gems.price.garden_plot) ----
        public int PlotsBase = DefaultPlotsBase;
        public int PlotsClubBonus = 1;
        public long PlotPrice = 400;

        // ---- Growth (garden.grow_minutes_min, garden.grow_minutes_max, garden.water_reduction) ----
        /// <summary>The shortest and longest growth (30 min and 8 h): the plants' times are spread between the two.</summary>
        public double GrowMinutesMin = 30.0;

        public double GrowMinutesMax = 480.0;

        /// <summary>Watering takes this share of the total growth off (10 %), once per sprout.</summary>
        public double WaterReduction = 0.10;

        // ---- Finish now (doc Gemmes: 1 Gem per started 10 minutes left, 3 at least, 3 a day) ----
        public int SpeedUpGemsPerStep = 1;
        public double SpeedUpStepMinutes = 10.0;
        public int SpeedUpMinGems = 3;
        public int SpeedUpsPerDay = 3;

        // ---- Seeds back and « Pousse heureuse » (section 8.1, table 8.3) ----
        public int SeedBackPercent = 60;
        public int SeedBackPercentLevel5 = 75;
        public int SeedBackPercentLevel9 = 90;
        public int HappyPercent = 10;

        // ---- Events (flags set by the event systems, T48+; section 8.1) ----
        /// <summary>Floraison (April, featured activity): growth twice as fast, a rare Floraison flower on every harvest.</summary>
        public bool FloraisonActive;

        public double FloraisonGrowthMultiplier = 2.0;

        /// <summary>Week-end du Jardin: growth × <see cref="WeekendGrowthMultiplier"/> and seeds given back 100 %.</summary>
        public bool WeekendActive;

        /// <summary>events.weekend.garden_multiplier: the growth speed during the Garden weekend.</summary>
        public double WeekendGrowthMultiplier = 2.0;

        /// <summary>flags.garden: the activity is shown (false hides it, nothing is erased; sprouts keep growing).</summary>
        public bool Enabled = true;

        /// <summary>
        /// A plant's growth in seconds with the remote spread: the doc's 30 min to 8 h mapped linearly onto
        /// [<see cref="GrowMinutesMin"/>, <see cref="GrowMinutesMax"/>] (the identity with the defaults).
        /// </summary>
        public double SpreadSeconds(double docSeconds)
        {
            const double docMin = 30.0 * 60.0, docMax = 480.0 * 60.0;
            double min = Math.Max(60.0, GrowMinutesMin * 60.0), max = Math.Max(min, GrowMinutesMax * 60.0);
            if (Math.Abs(min - docMin) < 1e-6 && Math.Abs(max - docMax) < 1e-6)
                return docSeconds;
            double t = (docSeconds - docMin) / (docMax - docMin);
            return min + Math.Max(0.0, Math.Min(1.0, t)) * (max - min);
        }

        // ---- Remote Config ----

        private readonly struct Bound
        {
            public readonly double Min;
            public readonly double Max;
            public readonly bool Integer;
            public readonly Action<GardenConfig, double> Set;

            public Bound(double min, double max, Action<GardenConfig, double> set, bool integer = true)
            {
                Min = min;
                Max = max;
                Integer = integer;
                Set = set;
            }
        }

        private static readonly Dictionary<string, Bound> Bounds = new Dictionary<string, Bound>
        {
            // « jamais en baisse »: never under the embedded value; the set has 5 beds.
            ["garden.plots_base"] = new Bound(DefaultPlotsBase, GardenState.MaxPlots - 1, (c, v) => c.PlotsBase = (int)v),
            ["garden.plots_club_bonus"] = new Bound(0, 2, (c, v) => c.PlotsClubBonus = (int)v),
            ["garden.grow_minutes_min"] = new Bound(10, 120, (c, v) => c.GrowMinutesMin = v),
            ["garden.grow_minutes_max"] = new Bound(120, 720, (c, v) => c.GrowMinutesMax = v),
            ["garden.water_reduction"] = new Bound(0, 0.3, (c, v) => c.WaterReduction = v, false),
            ["gems.price.garden_speedup_per_10min"] = new Bound(1, 5, (c, v) => c.SpeedUpGemsPerStep = (int)v),
            ["gems.price.garden_speedup_min"] = new Bound(1, 50, (c, v) => c.SpeedUpMinGems = (int)v),
            ["gems.garden_speedup_daily_cap"] = new Bound(0, 10, (c, v) => c.SpeedUpsPerDay = (int)v),
            ["gems.price.garden_plot"] = new Bound(200, 1000, (c, v) => c.PlotPrice = (long)v),
            ["events.weekend.garden_multiplier"] = new Bound(1, 3, (c, v) => c.WeekendGrowthMultiplier = v, false),
        };

        public const string EnabledFlag = "flags.garden";

        public static IEnumerable<string> RemoteKeys => Bounds.Keys;

        /// <summary>One Remote Config number: false (nothing changes) when unknown, out of bounds or not an integer where one is needed.</summary>
        public bool TryOverride(string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || !Bounds.TryGetValue(key, out var bound))
                return false;
            if (value < bound.Min || value > bound.Max || (bound.Integer && value != Math.Floor(value)))
                return false;
            bound.Set(this, value);
            return true;
        }

        public bool TryOverrideFlag(string key, bool value)
        {
            if (key != EnabledFlag)
                return false;
            Enabled = value;
            return true;
        }
    }
}
