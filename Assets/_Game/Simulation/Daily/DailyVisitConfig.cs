#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Every number of the daily rendez-vous (doc 01 Contenu, sections 1 to 6; doc 08 Remote Config, domains daily, gems,
    /// econ.bond, flags). Defaults are the docs'; <see cref="TryOverride"/> applies a Remote Config value inside its bounds
    /// and ignores anything else (a remote change never edits a save). Build gates say which later systems exist yet.
    /// </summary>
    public sealed class DailyVisitConfig
    {
        public static DailyVisitConfig Default { get; } = new DailyVisitConfig();

        // ---- Days ----
        /// <summary>Local hour the daily items renew at (doc 08 daily.reset_hour_local, 0 to 6).</summary>
        public int ResetHour;

        // ---- Wishes (section 2) ----
        public int WishesPerDay = 3;
        public int WishStockDays = 3;
        public int FreeSwaps = 1;
        public long WishGems = 3;

        /// <summary>Wish Gems are capped in all at this many per day since install (doc 01, section 2.6).</summary>
        public long WishGemCapPerDay = 9;

        public int WishBond = 15;
        public int WishSpeciesXp = 5;

        /// <summary>Wishes kept at most (stock): WishesPerDay × WishStockDays.</summary>
        public int WishStock => Math.Min(DailyVisitState.MaxWishStock, WishesPerDay * WishStockDays);

        // ---- Daily gift (section 3) ----
        /// <summary>At least one big gift every this many gifts (the guarantee).</summary>
        public int GiftBigEvery = 15;

        // ---- Calendar (section 4) ----
        public long CalendarGems7 = 20;
        public long CalendarGems14 = 20;
        public long CalendarGems21 = 20;
        public long CalendarGems28 = 40;

        // ---- Welcome (section 1.4) ----
        public long WelcomeTotalGems = 300;

        // ---- Weekly journal (section 5) ----
        public int JournalGoals = 7;
        public int JournalPrefilled = 2;
        public long JournalGoalGems = 5;
        public int JournalGoalTickets = 2;
        public long JournalCompleteGems = 15;

        // ---- Return gifts (section 6) ----
        public int ReturnGiftMinDays = 3;
        public int ReturnBigDays = 14;
        public int ReturnRepeatDays = 7;
        public int ReturnBigRepeatDays = 30;

        // ---- Substitutions while a system is closed (doc 01's own exchange rate: 1 ticket = 15 min of Light) ----
        public double TicketLightMinutes = 15.0;

        // ---- Bond (doc 08 econ.bond.*) ----
        public int BondTouch = 1;
        public int BondTouchCapAnimal = 5;
        public int BondTouchCapTotal = 30;
        public int BondMax = 1000;

        // ---- Flags (doc 08, section 5: a false flag hides, never erases) ----
        public bool GiftEnabled = true;
        public bool WishesEnabled = true;
        public bool CalendarEnabled = true;
        public bool JournalEnabled = true;
        public bool ReturnGiftEnabled = true;

        // ---- Build gates: the later systems these rewards belong to (substitutions apply while false) ----
        /// <summary>Activities and their tickets (T37).</summary>
        public bool ActivitiesAvailable;

        /// <summary>The Garden and its seeds (T43).</summary>
        public bool GardenAvailable;

        /// <summary>Albums and stickers (T44).</summary>
        public bool AlbumsAvailable;

        /// <summary>The Pâtisserie and its flower sugar (T63).</summary>
        public bool BakeryAvailable;

        /// <summary>The bonus gift (rewarded video or the Club, T54/T55).</summary>
        public bool BonusGiftAvailable;

        private readonly struct Bound
        {
            public readonly double Min;
            public readonly double Max;
            public readonly Action<DailyVisitConfig, double> Set;

            public Bound(double min, double max, Action<DailyVisitConfig, double> set)
            {
                Min = min;
                Max = max;
                Set = set;
            }
        }

        private static readonly Dictionary<string, Bound> Bounds = new Dictionary<string, Bound>
        {
            ["daily.reset_hour_local"] = new Bound(0, 6, (c, v) => c.ResetHour = (int)v),
            ["daily.wishes_per_day"] = new Bound(3, 4, (c, v) => c.WishesPerDay = (int)v),
            ["daily.wishes_stock_days"] = new Bound(1, 7, (c, v) => c.WishStockDays = (int)v),
            ["daily.wish_free_rerolls"] = new Bound(0, 3, (c, v) => c.FreeSwaps = (int)v),
            ["daily.journal_goals"] = new Bound(5, 10, (c, v) => c.JournalGoals = (int)v),
            ["daily.journal_prefilled_goals"] = new Bound(0, 3, (c, v) => c.JournalPrefilled = (int)v),
            ["daily.return_gift_min_days"] = new Bound(2, 14, (c, v) => c.ReturnGiftMinDays = (int)v),
            ["daily.gift_big_every"] = new Bound(5, 30, (c, v) => c.GiftBigEvery = (int)v),
            ["gems.wish"] = new Bound(0, 10, (c, v) => c.WishGems = (long)v),
            ["gems.wish_daily_cap"] = new Bound(0, 30, (c, v) => c.WishGemCapPerDay = (long)v),
            ["gems.journal_goal"] = new Bound(0, 20, (c, v) => c.JournalGoalGems = (long)v),
            ["gems.journal_complete"] = new Bound(0, 60, (c, v) => c.JournalCompleteGems = (long)v),
            ["gems.calendar_day7"] = new Bound(0, 60, (c, v) => c.CalendarGems7 = (long)v),
            ["gems.calendar_day14"] = new Bound(0, 60, (c, v) => c.CalendarGems14 = (long)v),
            ["gems.calendar_day21"] = new Bound(0, 60, (c, v) => c.CalendarGems21 = (long)v),
            ["gems.calendar_day28"] = new Bound(0, 120, (c, v) => c.CalendarGems28 = (long)v),
            ["gems.welcome_total"] = new Bound(0, 600, (c, v) => c.WelcomeTotalGems = (long)v),
            ["econ.bond.touch"] = new Bound(0, 10, (c, v) => c.BondTouch = (int)v),
            ["econ.bond.touch_daily_cap_animal"] = new Bound(0, 20, (c, v) => c.BondTouchCapAnimal = (int)v),
            ["econ.bond.touch_daily_cap_total"] = new Bound(0, 100, (c, v) => c.BondTouchCapTotal = (int)v),
            ["econ.bond.wish"] = new Bound(0, 50, (c, v) => c.WishBond = (int)v),
            ["econ.mastery_xp.wish"] = new Bound(0, 50, (c, v) => c.WishSpeciesXp = (int)v),
        };

        private static readonly Dictionary<string, Action<DailyVisitConfig, bool>> Flags = new Dictionary<string, Action<DailyVisitConfig, bool>>
        {
            ["flags.daily_gift"] = (c, v) => c.GiftEnabled = v,
            ["flags.wishes"] = (c, v) => c.WishesEnabled = v,
            ["flags.calendar"] = (c, v) => c.CalendarEnabled = v,
            ["flags.weekly_journal"] = (c, v) => c.JournalEnabled = v,
            ["flags.return_gift"] = (c, v) => c.ReturnGiftEnabled = v,
        };

        public static IEnumerable<string> RemoteKeys => Bounds.Keys;
        public static IEnumerable<string> RemoteFlags => Flags.Keys;

        /// <summary>One Remote Config number: false (nothing changes) when unknown, out of bounds or not an integer where one is needed.</summary>
        public bool TryOverride(string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || !Bounds.TryGetValue(key, out var bound))
                return false;
            if (value < bound.Min || value > bound.Max || value != Math.Floor(value))
                return false;
            bound.Set(this, value);
            return true;
        }

        public bool TryOverrideFlag(string key, bool value)
        {
            if (!Flags.TryGetValue(key, out var set))
                return false;
            set(this, value);
            return true;
        }
    }
}
