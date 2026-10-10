#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Every number of the expeditions (doc 01 Contenu, sections 7.1 to 7.3; doc 08 Remote Config: expeditions.*,
    /// gems.price.expedition_*, gems.expedition_speedup_daily_cap, econ.light_minutes.expedition_*, events.currency.expedition_*,
    /// events.weekend.expedition_duration_factor, flags.expeditions). Defaults are the docs'; <see cref="TryOverride"/> applies a
    /// Remote Config value inside its bounds and ignores anything else (a remote change never edits a save, a slot once owned
    /// stays owned). Tables are indexed by <see cref="ExpeditionDuration"/> (1 h, 4 h, 8 h, 12 h).
    /// </summary>
    public sealed class ExpeditionConfig
    {
        public static ExpeditionConfig Default { get; } = new ExpeditionConfig();

        /// <summary>The free slots the expeditions open with (doc 01: 2), never lowered by a remote value.</summary>
        public const int DefaultSlotsBase = 2;

        // ---- Slots (expeditions.slots_base, expeditions.slots_club_bonus, gems.price.expedition_slot) ----
        public int SlotsBase = DefaultSlotsBase;
        public int SlotsClubBonus = 1;
        public long SlotPrice = 800;

        // ---- Durations (expeditions.duration_hours.*; the first stroll of A18; the Expeditions weekend) ----
        public double[] DurationHours = { 1.0, 4.0, 8.0, 12.0 };

        /// <summary>« Première balade : 3 min » (doc 01, A18 and section 7.1): the account's first expedition, with the 1 h loot.</summary>
        public double FirstStrollSeconds = 180.0;

        /// <summary>The Expeditions weekend is running (an event flag: T48+ sets it; durations × <see cref="WeekendFactor"/>).</summary>
        public bool WeekendActive;

        /// <summary>events.weekend.expedition_duration_factor: −25 % (1 h becomes 45 min); the loot does not change.</summary>
        public double WeekendFactor = 0.75;

        // ---- Finish now (doc Gemmes: 1 Gem per started 6 minutes left, 5 at least, 3 a day) ----
        public int SpeedUpGemsPerStep = 1;
        public double SpeedUpStepMinutes = 6.0;
        public int SpeedUpMinGems = 5;
        public int SpeedUpsPerDay = 3;

        // ---- The Discovery egg guarantee (expeditions.discovery_egg_guarantee, expeditions.discovery_guarantee_min_hours) ----
        public int DiscoveryGuarantee = 12;
        public double GuaranteeMinHours = 8.0;

        // ---- The common loot of a 1-star team (section 7.3) ----
        public int[] LightMinutes = { 15, 50, 90, 120 };
        public int[] Treats = { 1, 2, 3, 4 };
        public int[] Seeds = { 1, 2, 3, 4 };
        public int[] StickerChance = { 10, 25, 45, 60 };
        public int[] DiscoveryChance = { 0, 2, 6, 9 };
        public int[] Tickets = { 0, 0, 1, 1 };
        public int[] Ingredients = { 0, 1, 1, 2 };
        public int[] EventCurrency = { 30, 50, 70, 90 };

        /// <summary>Bond points for each animal of the team (doc 02 Renouveau; doc 08 econ.bond.expedition).</summary>
        public int BondPoints = 5;

        /// <summary>The destination biome's mastery XP (doc 02 Améliorations: 15; the progression config's XpExpedition when it exists).</summary>
        public int MasteryXp = 15;

        /// <summary>Species mastery XP for each species of the team, once per species (doc 08 econ.mastery_xp.expedition).</summary>
        public int SpeciesXp = 2;

        // ---- The team rating's loot bonus (section 7.2) ----
        public int TwoStarLightPercent = 10;
        public int ThreeStarLightPercent = 20;
        public int TwoStarTreats = 1;
        public int ThreeStarTreats = 1;
        public int ThreeStarSeeds = 1;
        public int ThreeStarStickerPoints = 10;

        /// <summary>flags.expeditions: the activity is shown (false hides it, nothing is erased; teams away still come back).</summary>
        public bool Enabled = true;

        public double Hours(ExpeditionDuration duration) => At(DurationHours, duration, 1.0);

        public int Light(ExpeditionDuration duration) => Math.Max(0, At(LightMinutes, duration));

        public static int At(int[] table, ExpeditionDuration duration)
        {
            int i = (int)duration;
            return table != null && (uint)i < (uint)table.Length ? table[i] : 0;
        }

        public static double At(double[] table, ExpeditionDuration duration, double fallback)
        {
            int i = (int)duration;
            return table != null && (uint)i < (uint)table.Length ? table[i] : fallback;
        }

        // ---- Remote Config ----

        private readonly struct Bound
        {
            public readonly double Min;
            public readonly double Max;
            public readonly bool Integer;
            public readonly Action<ExpeditionConfig, double> Set;

            public Bound(double min, double max, Action<ExpeditionConfig, double> set, bool integer = true)
            {
                Min = min;
                Max = max;
                Integer = integer;
                Set = set;
            }
        }

        private static readonly Dictionary<string, Bound> Bounds = new Dictionary<string, Bound>
        {
            // « jamais en baisse »: never under the embedded value.
            ["expeditions.slots_base"] = new Bound(DefaultSlotsBase, 4, (c, v) => c.SlotsBase = (int)v),
            ["expeditions.slots_club_bonus"] = new Bound(0, 2, (c, v) => c.SlotsClubBonus = (int)v),
            ["expeditions.duration_hours.short"] = new Bound(0.5, 2, (c, v) => c.DurationHours[0] = v, false),
            ["expeditions.duration_hours.medium"] = new Bound(2, 6, (c, v) => c.DurationHours[1] = v, false),
            ["expeditions.duration_hours.long"] = new Bound(6, 10, (c, v) => c.DurationHours[2] = v, false),
            ["expeditions.duration_hours.very_long"] = new Bound(10, 16, (c, v) => c.DurationHours[3] = v, false),
            ["expeditions.discovery_egg_guarantee"] = new Bound(4, 20, (c, v) => c.DiscoveryGuarantee = (int)v),
            ["expeditions.discovery_guarantee_min_hours"] = new Bound(4, 12, (c, v) => c.GuaranteeMinHours = v, false),
            ["gems.price.expedition_speedup_per_6min"] = new Bound(1, 5, (c, v) => c.SpeedUpGemsPerStep = (int)v),
            ["gems.price.expedition_speedup_min"] = new Bound(1, 50, (c, v) => c.SpeedUpMinGems = (int)v),
            ["gems.expedition_speedup_daily_cap"] = new Bound(0, 10, (c, v) => c.SpeedUpsPerDay = (int)v),
            ["gems.price.expedition_slot"] = new Bound(400, 2000, (c, v) => c.SlotPrice = (long)v),
            ["econ.mastery_xp.expedition"] = new Bound(0, 20, (c, v) => c.SpeciesXp = (int)v),
            ["econ.light_minutes.expedition_1h"] = new Bound(5, 60, (c, v) => c.LightMinutes[0] = (int)v),
            ["econ.light_minutes.expedition_4h"] = new Bound(15, 180, (c, v) => c.LightMinutes[1] = (int)v),
            ["econ.light_minutes.expedition_8h"] = new Bound(30, 300, (c, v) => c.LightMinutes[2] = (int)v),
            ["econ.light_minutes.expedition_12h"] = new Bound(45, 420, (c, v) => c.LightMinutes[3] = (int)v),
            ["events.currency.expedition_1h"] = new Bound(0, 100, (c, v) => c.EventCurrency[0] = (int)v),
            ["events.currency.expedition_4h"] = new Bound(0, 150, (c, v) => c.EventCurrency[1] = (int)v),
            ["events.currency.expedition_8h"] = new Bound(0, 200, (c, v) => c.EventCurrency[2] = (int)v),
            ["events.currency.expedition_12h"] = new Bound(0, 250, (c, v) => c.EventCurrency[3] = (int)v),
            ["events.weekend.expedition_duration_factor"] = new Bound(0.5, 1, (c, v) => c.WeekendFactor = v, false),
            ["econ.bond.expedition"] = new Bound(0, 30, (c, v) => c.BondPoints = (int)v),
        };

        public const string EnabledFlag = "flags.expeditions";

        public static IEnumerable<string> RemoteKeys => Bounds.Keys;

        /// <summary>One Remote Config number: false (nothing changes) when unknown, out of bounds or not an integer where one is needed.</summary>
        public bool TryOverride(string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || !Bounds.TryGetValue(key, out var bound))
                return false;
            if (value < bound.Min || value > bound.Max || (bound.Integer && value != Math.Floor(value)))
                return false;
            var before = (double[])DurationHours.Clone();
            var light = (int[])LightMinutes.Clone();
            bound.Set(this, value);
            // « Les quatre valeurs doivent croître »: a remote value that breaks the order is refused.
            if (!Increasing(DurationHours) || !Increasing(LightMinutes))
            {
                DurationHours = before;
                LightMinutes = light;
                return false;
            }
            return true;
        }

        public bool TryOverrideFlag(string key, bool value)
        {
            if (key != EnabledFlag)
                return false;
            Enabled = value;
            return true;
        }

        private static bool Increasing(double[] values)
        {
            for (int i = 1; i < values.Length; i++)
                if (values[i] <= values[i - 1])
                    return false;
            return true;
        }

        private static bool Increasing(int[] values)
        {
            for (int i = 1; i < values.Length; i++)
                if (values[i] <= values[i - 1])
                    return false;
            return true;
        }
    }
}
