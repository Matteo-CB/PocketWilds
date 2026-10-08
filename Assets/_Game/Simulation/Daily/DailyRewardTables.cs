#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>The 12 lots of the daily gift (doc 01 Contenu, section 3.2). Stable values (save, analytics).</summary>
    public enum GiftLot : byte
    {
        /// <summary>3 Gems, small.</summary>
        G3 = 0,

        /// <summary>5 Gems, nice.</summary>
        G5 = 1,

        /// <summary>10 Gems, big.</summary>
        G10 = 2,

        /// <summary>15 Gems, big.</summary>
        G15 = 3,

        /// <summary>Light 15 min, 1 ticket and 1 Gem, nice.</summary>
        Mix = 4,

        /// <summary>Light 30 min, small.</summary>
        L30 = 5,

        /// <summary>Light 60 min, nice.</summary>
        L60 = 6,

        /// <summary>2 tickets, small.</summary>
        Tix = 7,

        /// <summary>3 treats of the most recent biome, small.</summary>
        Trt = 8,

        /// <summary>4 seeds of the known plants, small.</summary>
        Seed = 9,

        /// <summary>1 missing sticker, nice.</summary>
        Stk = 10,

        /// <summary>1 Discovery egg, big.</summary>
        Egg = 11,
    }

    /// <summary>
    /// The reward tables of the daily rendez-vous, shared by the services and the economy simulator (doc 01 Contenu):
    /// <list type="bullet">
    /// <item>the daily gift's 12 lots, their weights and sizes (section 3.2) and the guarantee (section 3.3);</item>
    /// <item>the 28 calendar boxes and the 13 cycle decorations, then their « Crépuscule » versions (sections 4.2, 4.3);</item>
    /// <item>the 3 welcome gifts (section 1.4);</item>
    /// <item>the return gifts after 3 to 13 days and after 14 days or more (sections 6.2, 6.3).</item>
    /// </list>
    /// A lot whose system is not open yet is replaced as the doc says, and keeps its original size:
    /// <list type="bullet">
    /// <item>tickets before the Play button: Light at doc 01's own rate of 15 min a ticket (TIX 2 → 30 min, box 23 3 → 45 min);</item>
    /// <item>seeds before the Garden: Light 7.5 min a seed (4 → 30 min, 6 → 45 min);</item>
    /// <item>a sticker before the albums: 3 treats of the most recent biome.</item>
    /// </list>
    /// Owner decisions of T36: tickets of the welcome and return gifts follow the same Light rate; the welcome's seeds are
    /// stored in the inventory, never replaced; the sticker of the 14-day return gift becomes 3 treats before the albums.
    /// Pure functions on static tables: nothing allocates.
    /// </summary>
    public static class DailyRewardTables
    {
        public const int GiftLotCount = 12;
        public const int CalendarBoxCount = 28;
        public const int WelcomeCount = 3;

        /// <summary>Decorations of cycles 1 to 13; cycles 14 to 26 give the same in their dusk tint.</summary>
        public const int CycleDecorationCount = 13;

        /// <summary>From this cycle on, box 28 gives a Discovery egg instead of a decoration.</summary>
        public const int FirstCycleWithoutDecoration = 2 * CycleDecorationCount + 1;

        /// <summary>Light a seed is worth before the Garden (doc 01: 4 seeds → Light 30 min, 6 seeds → Light 45 min).</summary>
        public const double SeedLightMinutes = 7.5;

        /// <summary>Treats a sticker becomes before the albums (doc 01, lot STK and boxes 6, 13, 24).</summary>
        public const int StickerTreats = 3;

        /// <summary>Trèfle doux, the only plant known before the Garden (welcome day 3, seed lots).</summary>
        public static readonly ContentId SweetClover = ContentId.FromSource("plant.sweet_clover");

        /// <summary>The plant of seed lots: the known plants with equal weights, Trèfle doux alone until T43.</summary>
        public static ContentId SeedPlant => SweetClover;

        // ---------------- Daily gift (section 3.2) ----------------

        private static readonly int[] GiftWeights = { 21, 19, 6, 4, 5, 13, 6, 9, 7, 5, 3, 2 };

        private static readonly GiftSize[] GiftSizes =
        {
            GiftSize.Small, GiftSize.Nice, GiftSize.Big, GiftSize.Big, GiftSize.Nice, GiftSize.Small, GiftSize.Nice, GiftSize.Small,
            GiftSize.Small, GiftSize.Small, GiftSize.Nice, GiftSize.Big,
        };

        private static readonly int WeightSum = Sum(false);
        private static readonly int ForcedSum = Sum(true);

        /// <summary>Sum of the gift weights (100: the weights are the doc's percentages).</summary>
        public static int GiftWeightTotal => WeightSum;

        /// <summary>Sum of the big lots' weights (G10 6, G15 4, EGG 2: 12), the guaranteed draw.</summary>
        public static int ForcedWeightTotal => ForcedSum;

        /// <summary>The lot's weight in the normal draw (its percentage).</summary>
        public static int Weight(GiftLot lot) => (uint)lot < GiftLotCount ? GiftWeights[(int)lot] : 0;

        /// <summary>The lot's weight in the guaranteed draw: its own weight for a big lot, 0 otherwise.</summary>
        public static int ForcedWeight(GiftLot lot) => SizeOf(lot) == GiftSize.Big ? Weight(lot) : 0;

        public static GiftSize SizeOf(GiftLot lot) => (uint)lot < GiftLotCount ? GiftSizes[(int)lot] : GiftSize.Small;

        public static bool IsBig(GiftLot lot) => SizeOf(lot) == GiftSize.Big;

        /// <summary>
        /// The guarantee (section 3.3): after <paramref name="bigEvery"/> − 1 gifts in a row without a big lot, the next one
        /// is drawn among the big lots only (15 by default, Remote Config daily.gift_big_every).
        /// </summary>
        public static bool MustForceBig(int giftsSinceBig, int bigEvery) => giftsSinceBig >= Math.Max(1, bigEvery) - 1;

        /// <summary>The Gems of a lot (substitutions never touch Gems).</summary>
        public static long GiftGems(GiftLot lot)
        {
            switch (lot)
            {
                case GiftLot.G3: return 3;
                case GiftLot.G5: return 5;
                case GiftLot.G10: return 10;
                case GiftLot.G15: return 15;
                case GiftLot.Mix: return 1;
                default: return 0;
            }
        }

        /// <summary>A daily gift's content (section 3.2), with the substitutions of the systems still closed.</summary>
        public static RewardBundle Gift(GiftLot lot, in RewardContext ctx)
        {
            var bundle = new RewardBundle { Size = SizeOf(lot), Gems = GiftGems(lot) };
            switch (lot)
            {
                case GiftLot.Mix:
                    bundle.LightMinutes = 15.0;
                    AddTickets(ref bundle, 1, in ctx);
                    break;
                case GiftLot.L30:
                    bundle.LightMinutes = 30.0;
                    break;
                case GiftLot.L60:
                    bundle.LightMinutes = 60.0;
                    break;
                case GiftLot.Tix:
                    AddTickets(ref bundle, 2, in ctx);
                    break;
                case GiftLot.Trt:
                    AddTreats(ref bundle, 3, in ctx);
                    break;
                case GiftLot.Seed:
                    AddSeeds(ref bundle, 4, in ctx);
                    break;
                case GiftLot.Stk:
                    AddSticker(ref bundle, in ctx);
                    break;
                case GiftLot.Egg:
                    bundle.DiscoveryEggs = 1;
                    break;
            }
            return bundle;
        }

        // ---------------- Calendar (sections 4.2 and 4.3) ----------------

        private enum BoxKind : byte
        {
            Light,
            Treats,
            TreatsAndSugar,
            Tickets,
            Seeds,
            Sticker,
            Gems,
            Egg,
            Final,
        }

        private readonly struct Box
        {
            public readonly BoxKind Kind;

            /// <summary>Treats, tickets or seeds.</summary>
            public readonly int Amount;

            /// <summary>Light boxes: minutes of production.</summary>
            public readonly double Minutes;

            public Box(BoxKind kind, int amount, double minutes)
            {
                Kind = kind;
                Amount = amount;
                Minutes = minutes;
            }
        }

        private static readonly Box[] Boxes =
        {
            new Box(BoxKind.Light, 0, 30.0), // 1
            new Box(BoxKind.TreatsAndSugar, 3, 0.0), // 2
            new Box(BoxKind.Tickets, 2, 0.0), // 3
            new Box(BoxKind.Seeds, 4, 0.0), // 4
            new Box(BoxKind.Light, 0, 45.0), // 5
            new Box(BoxKind.Sticker, 1, 0.0), // 6
            new Box(BoxKind.Gems, 0, 0.0), // 7
            new Box(BoxKind.Light, 0, 30.0), // 8
            new Box(BoxKind.TreatsAndSugar, 3, 0.0), // 9
            new Box(BoxKind.Tickets, 2, 0.0), // 10
            new Box(BoxKind.Light, 0, 60.0), // 11
            new Box(BoxKind.Seeds, 4, 0.0), // 12
            new Box(BoxKind.Sticker, 1, 0.0), // 13
            new Box(BoxKind.Gems, 0, 0.0), // 14
            new Box(BoxKind.Light, 0, 45.0), // 15
            new Box(BoxKind.Egg, 1, 0.0), // 16
            new Box(BoxKind.Tickets, 2, 0.0), // 17
            new Box(BoxKind.Treats, 5, 0.0), // 18
            new Box(BoxKind.Seeds, 6, 0.0), // 19
            new Box(BoxKind.Light, 0, 60.0), // 20
            new Box(BoxKind.Gems, 0, 0.0), // 21
            new Box(BoxKind.Light, 0, 45.0), // 22
            new Box(BoxKind.Tickets, 3, 0.0), // 23
            new Box(BoxKind.Sticker, 1, 0.0), // 24
            new Box(BoxKind.Treats, 5, 0.0), // 25
            new Box(BoxKind.Light, 0, 90.0), // 26
            new Box(BoxKind.Egg, 1, 0.0), // 27
            new Box(BoxKind.Final, 0, 0.0), // 28
        };

        private static readonly string[] CycleDecorationSources = BuildCycleSources();
        private static readonly ContentId[] CycleDecorationIds = BuildCycleIds(CycleDecorationSources);

        /// <summary>Boxes 7, 14, 21 and 28 (index 6, 13, 20, 27): the Gem boxes, stamped with the longer feel.</summary>
        public static bool IsBigBox(int box) => box >= 0 && box < CalendarBoxCount && box % 7 == 6;

        /// <summary>
        /// The decoration box 28 gives in a cycle (section 4.3): cycles 1 to 13 the 13 decorations in order, 14 to 26 the
        /// same with the « Crépuscule » tint (decor.&lt;name&gt;_dusk), none from cycle 27 on.
        /// </summary>
        public static ContentId CycleDecoration(int cycle) =>
            cycle >= 1 && cycle < FirstCycleWithoutDecoration ? CycleDecorationIds[cycle - 1] : ContentId.None;

        /// <summary>The source id of the cycle's decoration (localization keys), null from cycle 27 on.</summary>
        public static string? CycleDecorationSource(int cycle) =>
            cycle >= 1 && cycle < FirstCycleWithoutDecoration ? CycleDecorationSources[cycle - 1] : null;

        /// <summary>
        /// One calendar box (<paramref name="box"/> 0 to 27 for boxes 1 to 28) of a cycle (from 1), with the substitutions
        /// of the systems still closed. Gems come from the config (Remote Config gems.calendar_day7/14/21/28). Box 28 gives
        /// the cycle's decoration, or a Discovery egg from cycle 27 on. Big boxes are <see cref="GiftSize.Big"/>, the others
        /// <see cref="GiftSize.Small"/>.
        /// </summary>
        public static RewardBundle CalendarBox(int box, int cycle, in RewardContext ctx, DailyVisitConfig config)
        {
            box = ((box % CalendarBoxCount) + CalendarBoxCount) % CalendarBoxCount;
            if (cycle < 1)
                cycle = 1;
            var entry = Boxes[box];
            var bundle = new RewardBundle { Size = IsBigBox(box) ? GiftSize.Big : GiftSize.Small };
            switch (entry.Kind)
            {
                case BoxKind.Light:
                    bundle.LightMinutes = entry.Minutes;
                    break;
                case BoxKind.Treats:
                    AddTreats(ref bundle, entry.Amount, in ctx);
                    break;
                case BoxKind.TreatsAndSugar:
                    AddTreats(ref bundle, entry.Amount, in ctx);
                    if (ctx.BakeryOpen)
                        bundle.FlowerSugar = 1;
                    break;
                case BoxKind.Tickets:
                    AddTickets(ref bundle, entry.Amount, in ctx);
                    break;
                case BoxKind.Seeds:
                    AddSeeds(ref bundle, entry.Amount, in ctx);
                    break;
                case BoxKind.Sticker:
                    AddSticker(ref bundle, in ctx);
                    break;
                case BoxKind.Gems:
                    bundle.Gems = Math.Max(0, box == 6 ? config.CalendarGems7 : box == 13 ? config.CalendarGems14 : config.CalendarGems21);
                    break;
                case BoxKind.Egg:
                    bundle.DiscoveryEggs = entry.Amount;
                    break;
                case BoxKind.Final:
                    bundle.Gems = Math.Max(0, config.CalendarGems28);
                    bundle.Decoration = CycleDecoration(cycle);
                    if (bundle.Decoration == ContentId.None)
                        bundle.DiscoveryEggs = 1;
                    break;
            }
            return bundle;
        }

        // ---------------- Welcome gifts (section 1.4) ----------------

        /// <summary>
        /// The welcome gift of login day <paramref name="index"/> + 1: a third of gems.welcome_total each (100 Gems by
        /// default, the parts always adding up to the total), plus 2 tickets on day 2 (Light 15 min each before the Play
        /// button) and 5 seeds of Trèfle doux on day 3 (stored until the Garden, never replaced). Empty outside 0 to 2.
        /// </summary>
        public static RewardBundle Welcome(int index, in RewardContext ctx, DailyVisitConfig config)
        {
            if (index < 0 || index >= WelcomeCount)
                return default;
            long total = Math.Max(0, config.WelcomeTotalGems);
            var bundle = new RewardBundle
            {
                Size = GiftSize.Nice,
                Gems = total * (index + 1) / WelcomeCount - total * index / WelcomeCount,
            };
            if (index == 1)
            {
                AddTickets(ref bundle, 2, in ctx);
            }
            else if (index == 2)
            {
                bundle.SeedPlant = SweetClover;
                bundle.Seeds = 5;
            }
            return bundle;
        }

        // ---------------- Return gifts (sections 6.2 and 6.3) ----------------

        /// <summary>
        /// The return gift, on top of the normal offline Light. After 3 to 13 days: Light 120 min, 3 tickets, 1 Discovery
        /// egg, 3 treats of the most recent biome. After 14 days or more (<paramref name="big"/>): Light 240 min, 5 tickets,
        /// 1 Discovery egg, 5 treats and 1 missing sticker of the player's choice (3 treats before the albums). The Essence
        /// node Warm Return (<paramref name="warm"/>) adds one Discovery egg. Tickets become Light 15 min each before the
        /// Play button.
        /// </summary>
        public static RewardBundle Return(bool big, bool warm, in RewardContext ctx)
        {
            var bundle = new RewardBundle
            {
                Size = big ? GiftSize.Big : GiftSize.Nice,
                LightMinutes = big ? 240.0 : 120.0,
                DiscoveryEggs = warm ? 2 : 1,
            };
            AddTickets(ref bundle, big ? 5 : 3, in ctx);
            AddTreats(ref bundle, big ? 5 : 3, in ctx);
            if (big)
                AddSticker(ref bundle, in ctx);
            return bundle;
        }

        // ---------------- Substitutions ----------------

        private static void AddTickets(ref RewardBundle bundle, int tickets, in RewardContext ctx)
        {
            if (ctx.PlayOpen)
            {
                bundle.Tickets += tickets;
                return;
            }
            bundle.LightMinutes += tickets * ctx.TicketLightMinutes;
            bundle.Substituted = true;
        }

        private static void AddSeeds(ref RewardBundle bundle, int seeds, in RewardContext ctx)
        {
            if (ctx.GardenOpen)
            {
                bundle.SeedPlant = SeedPlant;
                bundle.Seeds += seeds;
                return;
            }
            bundle.LightMinutes += seeds * SeedLightMinutes;
            bundle.Substituted = true;
        }

        private static void AddSticker(ref RewardBundle bundle, in RewardContext ctx)
        {
            if (ctx.AlbumsOpen)
            {
                bundle.Stickers += 1;
                return;
            }
            AddTreats(ref bundle, StickerTreats, in ctx);
            bundle.Substituted = true;
        }

        private static void AddTreats(ref RewardBundle bundle, int treats, in RewardContext ctx)
        {
            bundle.Treat = ctx.TreatOfNewestBiome;
            bundle.Treats += treats;
        }

        // ---------------- Static tables ----------------

        private static int Sum(bool forced)
        {
            int sum = 0;
            for (int i = 0; i < GiftLotCount; i++)
                if (!forced || GiftSizes[i] == GiftSize.Big)
                    sum += GiftWeights[i];
            return sum;
        }

        private static string[] BuildCycleSources()
        {
            string[] names =
            {
                "moss_armchair", "pebble_fountain", "branch_swing", "round_garden_lantern", "woven_linen_hammock",
                "giant_watering_can", "birdhouse", "log_bridge", "stump_shelf", "daisy_carousel", "cloud_mobile",
                "sunflower_clock", "round_greenhouse",
            };
            var sources = new string[2 * CycleDecorationCount];
            for (int i = 0; i < CycleDecorationCount; i++)
            {
                sources[i] = "decor." + names[i];
                sources[CycleDecorationCount + i] = "decor." + names[i] + "_dusk";
            }
            return sources;
        }

        private static ContentId[] BuildCycleIds(string[] sources)
        {
            var ids = new ContentId[sources.Length];
            for (int i = 0; i < sources.Length; i++)
                ids[i] = ContentId.FromSource(sources[i]);
            return ids;
        }
    }
}
