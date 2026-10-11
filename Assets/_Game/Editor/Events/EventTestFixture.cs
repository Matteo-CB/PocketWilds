#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Editor.Events
{
    /// <summary>
    /// T48: the test event « event.test », built in code (never shipped: the default calendar stays empty). The doc's full track
    /// (free and Carnet d'or lines), the 7 egg items and the secret, the shop, the Memory Shop, the shared quest pools, 10 challenges,
    /// album page 112 and the Souvenir. Tests, the pacing simulator and editor captures share it; its content is a featured species
    /// with 6 variants and two existing species with 2 event variants each.
    /// </summary>
    public static class EventTestFixture
    {
        public const string Key = "event.test";
        public const int Year = 2026;
        public const int Month = 11;
        public const int AlbumNumber = 112;

        public const string FeaturedSpecies = "species.testfawn";
        public const string RabbitSpecies = "species.testrabbit";
        public const string SquirrelSpecies = "species.testsquirrel";
        public const string Treat = "treat.test_clover";
        public const string Seeds = "seed.event_test";

        public static string Variant(int n) => "variant.testfawn.v" + n;
        public static string Deco(int n) => "decor.event_test.deco_" + n;
        public static string GoldDeco(int n) => "decor.event_test.gold_deco_" + n;
        public static string Outfit(int n) => "outfit.event_test.outfit_" + n;
        public static string Luxury(int n) => "decor.event_test.luxury_" + n;
        public const string Souvenir = "decor.event_test.souvenir";
        public const string Frame = "frame.event_test";
        public const string GoldFrame = "frame.event_test_gold";
        public const string Pose = "pose.event_test";
        public const string HatchEffect = "effect.event_test.hatch";
        public const string Monument = "decor.event_test.monument";

        public static readonly string[] EventVariants =
        {
            "variant.testrabbit.event_a", "variant.testrabbit.event_b", "variant.testsquirrel.event_c", "variant.testsquirrel.event_d",
        };

        /// <summary>The run of a year: 2nd Friday of November 10:00 UTC, 14 days (announce 48 h before).</summary>
        public static EventCalendarEntry Entry(int year = Year, int version = 1) => EventSchedule.Entry(Key, year, Month, version);

        public static EventCalendar Calendar(params EventCalendarEntry[] entries)
        {
            var calendar = new EventCalendar();
            if (!calendar.TrySet(entries.Length > 0 ? entries : new[] { Entry() }))
                throw new InvalidOperationException("Invalid test calendar.");
            return calendar;
        }

        public static ContentRegistry BuildContent(int version = 1)
        {
            var builder = new ContentRegistryBuilder();
            AddContent(builder, version);
            return builder.Build();
        }

        /// <summary>The species, variants and the event itself (a returning edition: version 2 or more).</summary>
        public static void AddContent(ContentRegistryBuilder builder, int version = 1)
        {
            AddSpecies(builder, FeaturedSpecies, 2.0);
            AddSpecies(builder, RabbitSpecies, 1.0);
            AddSpecies(builder, SquirrelSpecies, 1.5);
            AddVariant(builder, "variant.testfawn.plain", FeaturedSpecies, RarityTier.Common, false);
            AddVariant(builder, "variant.testrabbit.plain", RabbitSpecies, RarityTier.Common, false);
            AddVariant(builder, "variant.testsquirrel.plain", SquirrelSpecies, RarityTier.Common, false);
            for (int n = 1; n <= 6; n++)
                AddVariant(builder, Variant(n), FeaturedSpecies, n == 6 ? RarityTier.Legendary : n >= 4 ? RarityTier.Epic : RarityTier.Rare, n == 6);
            for (int i = 0; i < EventVariants.Length; i++)
                AddVariant(builder, EventVariants[i], i < 2 ? RabbitSpecies : SquirrelSpecies, RarityTier.Rare, false);
            builder.AddEvent(Build(version), Key);
        }

        public static EventDefinition Build(int version = 1)
        {
            var tiers = new EventTier[EventDefinition.TierCount];
            for (int t = 1; t <= EventDefinition.TierCount; t++)
                tiers[t - 1] = new EventTier(t, FreeLine(t), GoldLine(t));
            var bonus = new[]
            {
                new EventTier(31, new[] { Item(EventRewardKind.Decoration, Deco(1)) }, Array.Empty<EventReward>()),
                new EventTier(32, new[] { Item(EventRewardKind.Decoration, Deco(2)) }, Array.Empty<EventReward>()),
                new EventTier(33, new[] { Item(EventRewardKind.Outfit, Outfit(1)) }, Array.Empty<EventReward>()),
                new EventTier(34, new[] { Item(EventRewardKind.Decoration, Deco(3)) }, Array.Empty<EventReward>()),
                new EventTier(35, new[] { Item(EventRewardKind.Outfit, Outfit(2)) }, Array.Empty<EventReward>()),
                new EventTier(36, new[] { Item(EventRewardKind.Decoration, Deco(4)) }, Array.Empty<EventReward>()),
            };
            var egg = new EventEggItem[EventDefinition.EggItemCount];
            egg[0] = new EventEggItem(ContentId.FromSource(Variant(2)), true);
            egg[1] = new EventEggItem(ContentId.FromSource(Variant(3)), true);
            egg[2] = new EventEggItem(ContentId.FromSource(Variant(4)), true);
            for (int i = 0; i < EventVariants.Length; i++)
                egg[3 + i] = new EventEggItem(ContentId.FromSource(EventVariants[i]), false);
            var shop = new[]
            {
                new EventShopItem("shop.egg", EventShopKind.Egg, EventPriceSlot.Egg, default),
                new EventShopItem("shop.outfit_4", EventShopKind.Item, EventPriceSlot.Outfit, Item(EventRewardKind.Outfit, Outfit(4))),
                new EventShopItem("shop.deco_5", EventShopKind.Item, EventPriceSlot.Decoration, Item(EventRewardKind.Decoration, Deco(5))),
                new EventShopItem("shop.deco_6", EventShopKind.Item, EventPriceSlot.Decoration, Item(EventRewardKind.Decoration, Deco(6))),
                new EventShopItem("shop.deco_7", EventShopKind.Item, EventPriceSlot.Decoration7, Item(EventRewardKind.Decoration, Deco(7))),
                new EventShopItem("shop.deco_8", EventShopKind.Item, EventPriceSlot.Decoration8, Item(EventRewardKind.Decoration, Deco(8))),
                new EventShopItem("shop.tickets", EventShopKind.Tickets, EventPriceSlot.Tickets, EventReward.Tickets(3)),
                new EventShopItem("shop.sticker", EventShopKind.Sticker, EventPriceSlot.Sticker, default),
                new EventShopItem("shop.seeds", EventShopKind.Seeds, EventPriceSlot.Seeds, new EventReward(EventRewardKind.None, 3, ContentId.FromSource(Seeds))),
            };
            var memory = new[]
            {
                new EventMemoryItem("memory.variant_1", EventPriceSlot.MemoryVariant, Item(EventRewardKind.Variant, Variant(1))),
                new EventMemoryItem("memory.outfit_1", EventPriceSlot.Outfit, Item(EventRewardKind.Outfit, Outfit(1))),
                new EventMemoryItem("memory.outfit_2", EventPriceSlot.Outfit, Item(EventRewardKind.Outfit, Outfit(2))),
                new EventMemoryItem("memory.outfit_3", EventPriceSlot.Outfit, Item(EventRewardKind.Outfit, Outfit(3))),
                new EventMemoryItem("memory.outfit_4", EventPriceSlot.Outfit, Item(EventRewardKind.Outfit, Outfit(4))),
                new EventMemoryItem("memory.deco_1", EventPriceSlot.Decoration, Item(EventRewardKind.Decoration, Deco(1))),
                new EventMemoryItem("memory.deco_2", EventPriceSlot.Decoration, Item(EventRewardKind.Decoration, Deco(2))),
                new EventMemoryItem("memory.deco_3", EventPriceSlot.Decoration, Item(EventRewardKind.Decoration, Deco(3))),
                new EventMemoryItem("memory.deco_4", EventPriceSlot.Decoration, Item(EventRewardKind.Decoration, Deco(4))),
                new EventMemoryItem("memory.deco_5", EventPriceSlot.Decoration, Item(EventRewardKind.Decoration, Deco(5))),
                new EventMemoryItem("memory.deco_6", EventPriceSlot.Decoration, Item(EventRewardKind.Decoration, Deco(6))),
                new EventMemoryItem("memory.deco_7", EventPriceSlot.Decoration7, Item(EventRewardKind.Decoration, Deco(7))),
                new EventMemoryItem("memory.deco_8", EventPriceSlot.Decoration8, Item(EventRewardKind.Decoration, Deco(8))),
                new EventMemoryItem("memory.frame", EventPriceSlot.MemoryFrame, new EventReward(EventRewardKind.Frame, 1, ContentId.FromSource(Frame), Frame)),
                new EventMemoryItem("memory.pose", EventPriceSlot.MemoryPose, new EventReward(EventRewardKind.Pose, 1, ContentId.FromSource(Pose), Pose)),
            };
            var luxury = new[]
            {
                ContentId.FromSource(Outfit(7)), ContentId.FromSource(Luxury(1)), ContentId.FromSource(Luxury(2)), ContentId.FromSource(Luxury(3)),
                ContentId.FromSource(Luxury(4)),
            };
            var levels = new int[EventDefinition.ChallengeCount];
            for (int i = 0; i < levels.Length; i++)
                levels[i] = i + 1;
            var headline = new[]
            {
                Item(EventRewardKind.Variant, Variant(1)), Item(EventRewardKind.Outfit, Outfit(1)),
                new EventReward(EventRewardKind.Frame, 1, ContentId.FromSource(Frame), Frame),
            };
            return new EventDefinition(Key, version, "event.test.name", "event.test.currency", ContentId.FromSource(FeaturedSpecies),
                new[] { (int)ActivityId.HideAndSeek }, (int)ActivityId.Grooming, AlbumNumber, tiers, bonus, egg, ContentId.FromSource(Variant(6)), shop,
                memory, EventQuestCatalog.Daily, EventQuestCatalog.Long, levels, ContentId.FromSource(Outfit(3)), ContentId.FromSource(Souvenir), luxury,
                headline, new[] { "theme.event_test" });
        }

        /// <summary>The free line (doc: T1 sticker 1, T2 25 Gems … T30 the profile frame).</summary>
        private static EventReward[] FreeLine(int tier) => tier switch
        {
            1 => new[] { EventReward.Sticker(1) },
            2 => new[] { EventReward.Gems(25) },
            3 => new[] { Item(EventRewardKind.Decoration, Deco(1)) },
            4 => new[] { EventReward.Tickets(3) },
            5 => new[] { Item(EventRewardKind.Outfit, Outfit(1)), new EventReward(EventRewardKind.Souvenir, 1, ContentId.FromSource(Souvenir)) },
            6 => new[] { EventReward.Sticker(6) },
            7 => new[] { EventReward.DiscoveryEgg() },
            8 => new[] { Item(EventRewardKind.Decoration, Deco(2)) },
            9 => new[] { EventReward.Gems(25) },
            10 => new[] { Item(EventRewardKind.Variant, Variant(1)) },
            11 => new[] { EventReward.Sticker(11) },
            12 => new[] { EventReward.Tickets(3) },
            13 => new[] { Item(EventRewardKind.Decoration, Deco(3)) },
            14 => new[] { EventReward.Gems(25) },
            15 => new[] { EventReward.Treats(5, ContentId.FromSource(Treat)) },
            16 => new[] { EventReward.Tickets(3) },
            17 => new[] { EventReward.DiscoveryEgg() },
            18 => new[] { Item(EventRewardKind.Decoration, Deco(4)) },
            19 => new[] { EventReward.Sticker(19) },
            20 => new[] { Item(EventRewardKind.Outfit, Outfit(2)) },
            21 => new[] { EventReward.Gems(25) },
            22 => new[] { EventReward.LightMinutes(60) },
            23 => new[] { EventReward.Sticker(23) },
            24 => new[] { EventReward.Tickets(3) },
            25 => new[] { EventReward.Gems(25) },
            26 => new[] { EventReward.Sticker(26) },
            27 => new[] { EventReward.DiscoveryEgg() },
            28 => new[] { EventReward.Tickets(3) },
            29 => new[] { EventReward.Gems(25) },
            _ => new[] { new EventReward(EventRewardKind.Frame, 1, ContentId.FromSource(Frame), Frame) },
        };

        /// <summary>The Carnet d'or line (doc: T1 50 Gems … T30 the monument); never Light, eggs, stickers or currency.</summary>
        private static EventReward[] GoldLine(int tier) => tier switch
        {
            1 or 4 or 12 or 17 or 23 or 27 => new[] { EventReward.Gems(50) },
            2 or 7 or 11 or 16 or 21 or 24 or 28 => new[] { EventReward.Tickets(3) },
            3 => new[] { Item(EventRewardKind.Decoration, GoldDeco(1)) },
            5 => new[] { Item(EventRewardKind.HatchEffect, HatchEffect) },
            6 or 9 or 14 or 19 or 22 or 26 => new[] { EventReward.Treats(5, ContentId.FromSource(Treat)) },
            8 => new[] { Item(EventRewardKind.Decoration, GoldDeco(2)) },
            10 => new[] { Item(EventRewardKind.Outfit, Outfit(5)) },
            13 => new[] { Item(EventRewardKind.Decoration, GoldDeco(3)) },
            15 => new[] { Item(EventRewardKind.Variant, Variant(5)) },
            18 => new[] { Item(EventRewardKind.Decoration, GoldDeco(4)) },
            20 => new[] { new EventReward(EventRewardKind.Frame, 1, ContentId.FromSource(GoldFrame), GoldFrame) },
            25 => new[] { Item(EventRewardKind.Outfit, Outfit(6)) },
            29 => new[] { EventReward.Treats(10, ContentId.FromSource(Treat)) },
            _ => new[] { Item(EventRewardKind.Monument, Monument) },
        };

        private static EventReward Item(EventRewardKind kind, string id) => EventReward.Of(kind, id);

        private static void AddSpecies(ContentRegistryBuilder builder, string id, double production) =>
            builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource(id), id + ".name", RigFamily.SmallQuadruped, ContentId.FromSource("biome.grassland"),
                production, "PF_Animal_Test"), id);

        private static void AddVariant(ContentRegistryBuilder builder, string id, string species, RarityTier rarity, bool secret) =>
            builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), ContentId.FromSource(species), id + ".name", rarity,
                new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0, isSecret: secret), id);
    }
}
