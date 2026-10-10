#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Every number of the albums (doc 01 Albums, sections 1.2 to 1.8; Remote Config list of section 1.14: chances per source,
    /// page shares, rarity weights, the season draw's share, the guarantees' thresholds, the event shop price, the Albums
    /// Weekend multiplier). Defaults are the doc's; <see cref="TryOverride"/> applies a Remote Config value inside its bounds
    /// and ignores anything else (a remote change never edits a save). The activity games' chances stay in
    /// <see cref="ActivityConfig"/> (albums.chance.activity_*star) and the expeditions' in <see cref="ExpeditionConfig"/>.
    /// </summary>
    public sealed class AlbumConfig
    {
        // ---- Chances of the sources (section 1.4) ----
        public double GardenChance1 = 0.05;
        public double GardenChance2 = 0.10;
        public double GardenChance3 = 0.15;

        /// <summary>Wishes of the day: the 1st and 2nd 20 %, the 3rd 100 %, the next ones (wishes in reserve) 20 %.</summary>
        public double WishChanceFirst = 0.20;

        public double WishChanceThird = 1.0;
        public double WishChanceReserve = 0.20;

        /// <summary>Hug Match, a level cleared for the first time (same Essence bonus as the activities).</summary>
        public double CuddleChance1 = 0.10;

        public double CuddleChance2 = 0.20;
        public double CuddleChance3 = 0.35;

        /// <summary>Every 10th Hug Match level cleared gives one sure sticker of its page.</summary>
        public int CuddleGuaranteeEvery = 10;

        // ---- Page shares (section 1.4) ----
        public double ShareActivityPage = 0.50;
        public double ShareActiveBiomePage1 = 0.25;
        public double ShareActiveBiomePage2 = 0.25;
        public double ShareWishPage1 = 0.70;
        public double ShareWishPage2 = 0.30;
        public double ShareExpeditionPage1 = 0.40;
        public double ShareExpeditionPage2 = 0.35;
        public double ShareExpeditionActivity = 0.25;

        /// <summary>An event destination: the least complete open page 75 %, the Expeditions page 25 %.</summary>
        public double ShareExpeditionLeastComplete = 0.75;

        /// <summary>Daily gift and calendar: the newest biome's pages 40 % (20 % each), the rest shared by every other open page.</summary>
        public double ShareGiftNewestBiome = 0.40;

        public double ShareWorldPage2 = 0.60;
        public double ShareWorldPage1 = 0.40;

        /// <summary>The season draw takes this share of the activity, Hug Match, garden, wish and expedition draws.</summary>
        public double SeasonShare = 0.15;

        // ---- Rarity weights and preferred source (sections 1.2 and 1.6) ----
        public double WeightCommon = 70.0;
        public double WeightRare = 25.0;
        public double WeightShiny = 5.0;
        public double PreferredWeight = 3.0;

        // ---- Guarantees (section 1.6) ----
        /// <summary>After this many duplicates in a row, the next draw gives a missing sticker (the 4th after 3).</summary>
        public int NewnessAfter = 3;

        /// <summary>The page's n-th draw gives its Shiny when still missing.</summary>
        public int ShinyAfter = 30;

        // ---- Album stars (section 1.7) ----
        public int StarsCommon = 1;
        public int StarsRare = 2;
        public int StarsShiny = 3;
        public int ChoicePrice = 10;

        /// <summary>P-WKD-06 Week-end des Albums: duplicates' stars × this (set active by the event systems, Friday 0:00 to Sunday 23:59 local).</summary>
        public int WeekendMultiplier = 2;

        public bool WeekendActive;

        /// <summary>The event shop: a missing sticker of the running event's page, of choice, for this event currency.</summary>
        public int EventShopPrice = 250;

        // ---- Rewards (section 1.8) ----
        public int PageCompleteGems = 25;
        public int PageHalfTickets = 3;

        /// <summary>flags.albums: the albums are shown (false hides them, nothing is erased).</summary>
        public bool Enabled = true;

        public double GardenChance(int stars) =>
            Math.Max(0.0, stars >= 3 ? GardenChance3 : stars == 2 ? GardenChance2 : stars == 1 ? GardenChance1 : 0.0);

        public double CuddleChance(int stars) =>
            Math.Max(0.0, stars >= 3 ? CuddleChance3 : stars == 2 ? CuddleChance2 : stars == 1 ? CuddleChance1 : 0.0);

        public double WishChance(int nthOfDay) =>
            Math.Max(0.0, nthOfDay == 3 ? WishChanceThird : nthOfDay < 3 ? WishChanceFirst : WishChanceReserve);

        public double Weight(StickerRarity rarity) =>
            Math.Max(0.0, rarity == StickerRarity.Shiny ? WeightShiny : rarity == StickerRarity.Rare ? WeightRare : WeightCommon);

        /// <summary>The stars a duplicate gives (the Albums Weekend applied).</summary>
        public int DuplicateStars(StickerRarity rarity)
        {
            int stars = rarity == StickerRarity.Shiny ? StarsShiny : rarity == StickerRarity.Rare ? StarsRare : StarsCommon;
            return Math.Max(0, stars) * (WeekendActive ? Math.Max(1, WeekendMultiplier) : 1);
        }

        // ---- Remote Config ----

        private readonly struct Bound
        {
            public readonly double Min;
            public readonly double Max;
            public readonly bool Integer;
            public readonly Action<AlbumConfig, double> Set;

            public Bound(double min, double max, Action<AlbumConfig, double> set, bool integer = false)
            {
                Min = min;
                Max = max;
                Integer = integer;
                Set = set;
            }
        }

        private static readonly Dictionary<string, Bound> Bounds = new Dictionary<string, Bound>
        {
            ["albums.chance.garden_1star"] = new Bound(0, 1, (c, v) => c.GardenChance1 = v),
            ["albums.chance.garden_2star"] = new Bound(0, 1, (c, v) => c.GardenChance2 = v),
            ["albums.chance.garden_3star"] = new Bound(0, 1, (c, v) => c.GardenChance3 = v),
            ["albums.chance.wish_first"] = new Bound(0, 1, (c, v) => c.WishChanceFirst = v),
            ["albums.chance.wish_third"] = new Bound(0, 1, (c, v) => c.WishChanceThird = v),
            ["albums.chance.wish_reserve"] = new Bound(0, 1, (c, v) => c.WishChanceReserve = v),
            ["albums.chance.cuddle_1star"] = new Bound(0, 1, (c, v) => c.CuddleChance1 = v),
            ["albums.chance.cuddle_2star"] = new Bound(0, 1, (c, v) => c.CuddleChance2 = v),
            ["albums.chance.cuddle_3star"] = new Bound(0, 1, (c, v) => c.CuddleChance3 = v),
            ["albums.share.activity_page"] = new Bound(0, 1, (c, v) => c.ShareActivityPage = v),
            ["albums.share.active_biome_page1"] = new Bound(0, 1, (c, v) => c.ShareActiveBiomePage1 = v),
            ["albums.share.active_biome_page2"] = new Bound(0, 1, (c, v) => c.ShareActiveBiomePage2 = v),
            ["albums.share.wish_page1"] = new Bound(0, 1, (c, v) => c.ShareWishPage1 = v),
            ["albums.share.wish_page2"] = new Bound(0, 1, (c, v) => c.ShareWishPage2 = v),
            ["albums.share.expedition_page1"] = new Bound(0, 1, (c, v) => c.ShareExpeditionPage1 = v),
            ["albums.share.expedition_page2"] = new Bound(0, 1, (c, v) => c.ShareExpeditionPage2 = v),
            ["albums.share.expedition_activity"] = new Bound(0, 1, (c, v) => c.ShareExpeditionActivity = v),
            ["albums.share.expedition_least_complete"] = new Bound(0, 1, (c, v) => c.ShareExpeditionLeastComplete = v),
            ["albums.share.gift_newest_biome"] = new Bound(0, 1, (c, v) => c.ShareGiftNewestBiome = v),
            ["albums.share.world_page2"] = new Bound(0, 1, (c, v) => c.ShareWorldPage2 = v),
            ["albums.share.world_page1"] = new Bound(0, 1, (c, v) => c.ShareWorldPage1 = v),
            ["albums.season_share"] = new Bound(0, 0.5, (c, v) => c.SeasonShare = v),
            ["albums.weight.common"] = new Bound(0, 1000, (c, v) => c.WeightCommon = v),
            ["albums.weight.rare"] = new Bound(0, 1000, (c, v) => c.WeightRare = v),
            ["albums.weight.shiny"] = new Bound(0, 1000, (c, v) => c.WeightShiny = v),
            ["albums.weight.preferred"] = new Bound(1, 10, (c, v) => c.PreferredWeight = v),
            ["albums.guarantee.newness"] = new Bound(1, 10, (c, v) => c.NewnessAfter = (int)v, true),
            ["albums.guarantee.shiny"] = new Bound(5, 100, (c, v) => c.ShinyAfter = (int)v, true),
            ["albums.event_shop_price"] = new Bound(50, 1000, (c, v) => c.EventShopPrice = (int)v, true),
            ["events.weekend.albums_multiplier"] = new Bound(1, 3, (c, v) => c.WeekendMultiplier = (int)v, true),
        };

        public const string EnabledFlag = "flags.albums";

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
