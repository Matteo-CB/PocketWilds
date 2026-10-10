#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Every number of the activity framework (doc « Mini-jeux &amp; activités » section 2, doc 01 Contenu sections 0 and
    /// 12.0, doc 02 Gemmes, doc 08 Remote Config: activities.*, econ.light_minutes.activity_*, econ.mastery_xp.activity_*,
    /// econ.bond.activity_*, gems.activity_*, gems.price.tickets_pack, gems.tickets_pack_*, albums.chance.activity_*,
    /// events.weekend.activity_multiplier, flags.activities, flags.activity.*). Defaults are the docs';
    /// <see cref="TryOverride"/> applies a Remote Config value inside its bounds and ignores anything else (a remote change
    /// never edits a save). Readers go through the accessors, which apply the cross rules (club reserve at least the
    /// normal one, Light growing with the stars).
    /// </summary>
    public sealed class ActivityConfig
    {
        public static ActivityConfig Default { get; } = new ActivityConfig();

        // ---- Tickets (activities.tickets_*) ----
        public int TicketsMax = 5;
        public int TicketsMaxClub = 7;
        public int TicketsRefillMinutes = 120;

        /// <summary>A ticket received as a gift may pass the reserve, up to this many (doc 01, section 0; no Remote Config key).</summary>
        public int TicketsGiftCap = 10;

        // ---- Ticket packs (gems.price.tickets_pack, gems.tickets_pack_size, gems.tickets_pack_daily_cap) ----
        public int PackPrice = 30;
        public int PackSize = 3;
        public int PacksPerDay = 2;

        // ---- Adaptive difficulty (activities.adaptive.*, doc 01 section 12.0) ----
        public int EasierAfterFails = 2;
        public int HarderAfterPerfects = 2;

        /// <summary>
        /// activities.adaptive.step, a share of the whole range: d moves by round(step × 10) levels, at least 1 (0.10 = one
        /// level, the doc 01 rule).
        /// </summary>
        public double AdaptiveStep = 0.10;

        // ---- Rewards of a rewarded game (doc Mini-jeux section 2, doc 01 section 12.0) ----
        public int LightMinutes1 = 5;
        public int LightMinutes2 = 10;
        public int LightMinutes3 = 20;
        public int SpeciesXp1 = 1;
        public int SpeciesXp2 = 2;
        public int SpeciesXp3 = 3;
        public int Bond1 = 2;
        public int Bond2 = 4;
        public int Bond3 = 6;
        public int EventCurrency1 = 20;
        public int EventCurrency2 = 30;
        public int EventCurrency3 = 40;
        public double FeaturedMultiplier = 1.5;

        /// <summary>« Week-end des Activités »: rewards × this (never Gems, never stickers; doc 08 events.weekend.activity_multiplier).</summary>
        public double WeekendMultiplier = 2.0;

        /// <summary>Sticker chance of a game played with a free ticket (albums.chance.activity_*star).</summary>
        public double StickerChance1 = 0.10;
        public double StickerChance2 = 0.20;
        public double StickerChance3 = 0.35;

        /// <summary>Points of the fixed sticker counter of paid games: a sticker at this many (doc 01 Albums, section 1.7).</summary>
        public const int PaidStickerThreshold = 100;

        // ---- Gems (doc 02 Gemmes) ----
        public long GemsFirstThreeStars = 2;
        public long GemsDailyCap = 6;
        public long GemsChallenge = 5;

        /// <summary>A challenge decoration every this many challenges at 3 stars (activities.challenge_decoration_every).</summary>
        public int ChallengeDecorationEvery = 5;

        // ---- Session ----
        /// <summary>A game quit within this many seconds gives its ticket back (a kind default, no doc number).</summary>
        public double QuitRefundSeconds = 3.0;

        // ---- Accessibility « Activités plus lentes » (doc 01 section 12.0; doc 06 settings) ----
        public const float SlowerSpeedMultiplier = 0.8f;
        public const float SlowerTimeMultiplier = 1.25f;

        // ---- Flags (doc 08, section 5: a false flag hides, never erases) ----
        public bool ActivitiesEnabled = true;

        /// <summary>flags.activity.&lt;name&gt; per slot (activities without a flag stay true).</summary>
        public readonly bool[] ActivityEnabled = AllTrue();

        // ---- Build gates and states owned by later tasks ----
        /// <summary>The Wild Club is active (T54): the reserve is <see cref="TicketsMaxClub"/>.</summary>
        public bool ClubActive;

        /// <summary>Albums and stickers exist (T44); before, a sticker becomes 3 treats of the newest biome (as T36).</summary>
        public bool AlbumsAvailable;

        /// <summary>The Pâtisserie exists and is open (T63); before, Nut Drop's Strawberries are not given (T40).</summary>
        public bool BakeryAvailable;

        // ---- Accessors (cross rules of doc 08, section 5) ----

        public int Reserve(bool club) => Math.Max(1, club ? Math.Max(TicketsMax, TicketsMaxClub) : TicketsMax);

        public long RefillMilliseconds => Math.Max(1, TicketsRefillMinutes) * 60_000L;

        /// <summary>Levels d moves by per adaptation step.</summary>
        public int AdaptiveLevels => Math.Max(1, (int)Math.Round(AdaptiveStep * 10.0, MidpointRounding.AwayFromZero));

        /// <summary>Light minutes of a game by its stars (0 stars: 0), never lower than the star below.</summary>
        public double LightMinutes(int stars)
        {
            int one = Math.Max(0, LightMinutes1);
            int two = Math.Max(one, LightMinutes2);
            int three = Math.Max(two, LightMinutes3);
            return stars >= 3 ? three : stars == 2 ? two : stars == 1 ? one : 0;
        }

        public int SpeciesXp(int stars) => Math.Max(0, stars >= 3 ? SpeciesXp3 : stars == 2 ? SpeciesXp2 : stars == 1 ? SpeciesXp1 : 0);

        public int Bond(int stars) => Math.Max(0, stars >= 3 ? Bond3 : stars == 2 ? Bond2 : stars == 1 ? Bond1 : 0);

        public int EventCurrency(int stars) =>
            Math.Max(0, stars >= 3 ? EventCurrency3 : stars == 2 ? EventCurrency2 : stars == 1 ? EventCurrency1 : 0);

        public double StickerChance(int stars) =>
            Math.Max(0.0, stars >= 3 ? StickerChance3 : stars == 2 ? StickerChance2 : stars == 1 ? StickerChance1 : 0.0);

        public bool IsEnabled(ActivityId activity)
        {
            if (!ActivitiesEnabled)
                return false;
            int slot = ActivityCatalog.SlotOf(activity);
            return slot >= 0 && ActivityEnabled[slot];
        }

        // ---- Remote Config ----

        private readonly struct Bound
        {
            public readonly double Min;
            public readonly double Max;
            public readonly bool Integer;
            public readonly Action<ActivityConfig, double> Set;

            public Bound(double min, double max, Action<ActivityConfig, double> set, bool integer = true)
            {
                Min = min;
                Max = max;
                Integer = integer;
                Set = set;
            }
        }

        private static readonly Dictionary<string, Bound> Bounds = new Dictionary<string, Bound>
        {
            ["activities.tickets_max"] = new Bound(3, 10, (c, v) => c.TicketsMax = (int)v),
            ["activities.tickets_max_club"] = new Bound(3, 12, (c, v) => c.TicketsMaxClub = (int)v),
            ["activities.tickets_refill_minutes"] = new Bound(30, 240, (c, v) => c.TicketsRefillMinutes = (int)v),
            ["activities.reward.event_currency_1star"] = new Bound(0, 100, (c, v) => c.EventCurrency1 = (int)v),
            ["activities.reward.event_currency_2star"] = new Bound(0, 150, (c, v) => c.EventCurrency2 = (int)v),
            ["activities.reward.event_currency_3star"] = new Bound(0, 200, (c, v) => c.EventCurrency3 = (int)v),
            ["activities.reward.featured_multiplier"] = new Bound(1, 3, (c, v) => c.FeaturedMultiplier = v, false),
            ["activities.challenge_decoration_every"] = new Bound(1, 10, (c, v) => c.ChallengeDecorationEvery = (int)v),
            ["activities.adaptive.easier_after_fails"] = new Bound(1, 5, (c, v) => c.EasierAfterFails = (int)v),
            ["activities.adaptive.harder_after_perfects"] = new Bound(1, 5, (c, v) => c.HarderAfterPerfects = (int)v),
            ["activities.adaptive.step"] = new Bound(0.02, 0.30, (c, v) => c.AdaptiveStep = v, false),
            ["econ.light_minutes.activity_1star"] = new Bound(1, 30, (c, v) => c.LightMinutes1 = (int)v),
            ["econ.light_minutes.activity_2star"] = new Bound(2, 60, (c, v) => c.LightMinutes2 = (int)v),
            ["econ.light_minutes.activity_3star"] = new Bound(5, 120, (c, v) => c.LightMinutes3 = (int)v),
            ["econ.mastery_xp.activity_1star"] = new Bound(0, 10, (c, v) => c.SpeciesXp1 = (int)v),
            ["econ.mastery_xp.activity_2star"] = new Bound(0, 20, (c, v) => c.SpeciesXp2 = (int)v),
            ["econ.mastery_xp.activity_3star"] = new Bound(0, 30, (c, v) => c.SpeciesXp3 = (int)v),
            ["econ.bond.activity_1star"] = new Bound(0, 20, (c, v) => c.Bond1 = (int)v),
            ["econ.bond.activity_2star"] = new Bound(0, 30, (c, v) => c.Bond2 = (int)v),
            ["econ.bond.activity_3star"] = new Bound(0, 40, (c, v) => c.Bond3 = (int)v),
            ["gems.activity_first_3stars"] = new Bound(0, 10, (c, v) => c.GemsFirstThreeStars = (long)v),
            ["gems.activity_daily_cap"] = new Bound(0, 20, (c, v) => c.GemsDailyCap = (long)v),
            ["gems.activity_challenge"] = new Bound(0, 20, (c, v) => c.GemsChallenge = (long)v),
            ["gems.price.tickets_pack"] = new Bound(10, 100, (c, v) => c.PackPrice = (int)v),
            ["gems.tickets_pack_size"] = new Bound(1, 5, (c, v) => c.PackSize = (int)v),
            ["gems.tickets_pack_daily_cap"] = new Bound(0, 5, (c, v) => c.PacksPerDay = (int)v),
            ["albums.chance.activity_1star"] = new Bound(0, 1, (c, v) => c.StickerChance1 = v, false),
            ["albums.chance.activity_2star"] = new Bound(0, 1, (c, v) => c.StickerChance2 = v, false),
            ["albums.chance.activity_3star"] = new Bound(0, 1, (c, v) => c.StickerChance3 = v, false),
            ["events.weekend.activity_multiplier"] = new Bound(1, 3, (c, v) => c.WeekendMultiplier = v, false),
        };

        /// <summary>doc 08 flags of the activities (Expeditions, Garden and the Grand Contest have none here).</summary>
        private static readonly Dictionary<string, ActivityId> ActivityFlags = new Dictionary<string, ActivityId>
        {
            ["flags.activity.grooming"] = ActivityId.Grooming,
            ["flags.activity.hide_seek"] = ActivityId.HideAndSeek,
            ["flags.activity.nut_rain"] = ActivityId.NutRain,
            ["flags.activity.memory"] = ActivityId.PawMemory,
            ["flags.activity.treasure_fishing"] = ActivityId.TreasureFishing,
            ["flags.activity.lily_hop"] = ActivityId.LilyHop,
            ["flags.activity.patisserie"] = ActivityId.Bakery,
            ["flags.activity.photo_safari"] = ActivityId.PhotoSafari,
            ["flags.activity.cuddle_match"] = ActivityId.CuddleMatch,
            ["flags.activity.slide"] = ActivityId.Slide,
            ["flags.activity.constellations"] = ActivityId.Constellations,
            ["flags.activity.firefly_rhythm"] = ActivityId.FireflyRhythm,
            ["flags.activity.bubbles"] = ActivityId.Bubbles,
            ["flags.activity.puzzle"] = ActivityId.TilePuzzle,
            ["flags.activity.burrow_maze"] = ActivityId.BurrowMaze,
        };

        public const string ActivitiesFlag = "flags.activities";

        public static IEnumerable<string> RemoteKeys => Bounds.Keys;
        public static IEnumerable<string> RemoteFlags
        {
            get
            {
                yield return ActivitiesFlag;
                foreach (var key in ActivityFlags.Keys)
                    yield return key;
            }
        }

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
            if (key == ActivitiesFlag)
            {
                ActivitiesEnabled = value;
                return true;
            }
            if (!ActivityFlags.TryGetValue(key, out var activity))
                return false;
            ActivityEnabled[ActivityCatalog.SlotOf(activity)] = value;
            return true;
        }

        private static bool[] AllTrue()
        {
            var flags = new bool[ActivityCatalog.SlotCount];
            for (int i = 0; i < flags.Length; i++)
                flags[i] = true;
            return flags;
        }
    }
}
