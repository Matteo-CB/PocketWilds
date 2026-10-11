using System;
using System.Collections.Generic;
using System.IO;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Events
{
    /// <summary>
    /// T49 « L'Éveil des Terriers » (event.burrows, P-EVT-103, album event 103) as data: DEF_Event_Burrows (the 30 tiers free and
    /// Carnet d'or, the returning-year bonus tiers, the egg, the shop, the Memory Shop, the challenges of Cache-cache, the
    /// Souvenir, the Box, the theme) and DEF_ShopCatalog_Burrows (the 8 accessories, the 7 outfits O-OUT-103-01..07, the 13
    /// decorations and the 4 gold ones, the monument, the hatch effect O-HATCH-103 and the gold frame, all owned entitlements).
    /// Both join the content database only when the registry builds with them (the mole's variants and the 4 event variants come
    /// from the animal pipeline: run it first). Models, prefabs, icons and sticker art come from the art pipelines. Idempotent.
    /// </summary>
    public static class BurrowsEventPipeline
    {
        public const string Key = DefaultEventCalendar.Burrows;
        public const int AlbumNumber = 103;
        public const string Folder = "Assets/_Game/Content/Events";
        public const string EventPath = Folder + "/DEF_Event_Burrows.asset";
        public const string CatalogPath = Folder + "/DEF_ShopCatalog_Burrows.asset";

        public const string NameKey = "event.burrows.name";
        public const string CurrencyKey = "event.burrows.currency";
        public const string CurrencyIcon = "ICO_Event_Pousses";
        public const string ThemeKey = "theme.burrows";

        /// <summary>Tender sprout green, cream, warm earth (0xRRGGBB).</summary>
        public static readonly uint[] ThemeColors = { 0xA8D17F, 0xFFF3DC, 0x9C6B45 };

        public const string FeaturedSpecies = "species.mole";
        public const string Velvet = "variant.mole.velvet";
        public const string Cocoa = "variant.mole.cocoa";
        public const string Sand = "variant.mole.sand";
        public const string Mocha = "variant.mole.mocha";
        public const string GoldenMoss = "variant.mole.goldenmoss";
        public const string StarNose = "variant.mole.starnose";
        public const string DewRabbit = "variant.rabbit.dew";
        public const string BudFieldMouse = "variant.fieldmouse.bud";
        public const string CloverHedgehog = "variant.hedgehog.clover";
        public const string MossHedgehog = "variant.hedgehog.softmoss";

        /// <summary>The egg's 7 items in order: the mole's v2, v3, v4, then the 4 event variants (palette variants, event source only).</summary>
        public static readonly string[] EggVariants = { Cocoa, Sand, Mocha, DewRabbit, BudFieldMouse, CloverHedgehog, MossHedgehog };

        /// <summary>Every variant the event names (the animal pipeline owns them).</summary>
        public static readonly string[] Variants = { Velvet, Cocoa, Sand, Mocha, GoldenMoss, StarNose, DewRabbit, BudFieldMouse, CloverHedgehog, MossHedgehog };

        public const string Frame = "frame.event_burrows";
        public const string GoldFrame = "frame.event_burrows_gold";
        public const string Pose = "pose.peek_out";
        public const string Title = "title.event_burrows";
        public const string HatchEffect = "effect.burrows.sprout_burst";
        public const string Souvenir = "decor.burrows.souvenir";
        public const string Monument = "decor.burrows.root_burrow";
        public const string SeedPlant = "plant.sunflower";

        // ---------------------------------------------------------------- cosmetics

        /// <summary>One accessory: id, socket, art name (ACC_&lt;Name&gt;, PF_Acc_&lt;Name&gt;, ICO_Accessory_&lt;Name&gt;), triangle budget, gold palette first.</summary>
        public readonly struct AccessoryRow
        {
            public readonly string Id;
            public readonly AccessorySocket Socket;
            public readonly string Name;
            public readonly int Tris;
            public readonly bool Gold;

            public AccessoryRow(string id, AccessorySocket socket, string name, int tris, bool gold)
            {
                Id = id;
                Socket = socket;
                Name = name;
                Tris = tris;
                Gold = gold;
            }
        }

        public static readonly AccessoryRow[] Accessories =
        {
            new AccessoryRow("accessory.burrows.sprout_hat", AccessorySocket.HeadTop, "SproutHat", 250, false),
            new AccessoryRow("accessory.burrows.seed_sack", AccessorySocket.Back, "SeedSack", 500, false),
            new AccessoryRow("accessory.burrows.burrow_goggles", AccessorySocket.HeadTop, "BurrowGoggles", 500, false),
            new AccessoryRow("accessory.burrows.gardener_scarf", AccessorySocket.Neck, "GardenerScarf", 500, false),
            new AccessoryRow("accessory.burrows.miner_helmet", AccessorySocket.HeadTop, "MinerHelmet", 500, true),
            new AccessoryRow("accessory.burrows.golden_shovel", AccessorySocket.Back, "GoldenShovel", 700, true),
            new AccessoryRow("accessory.burrows.overalls_bib", AccessorySocket.Neck, "OverallsBib", 500, false),
            new AccessoryRow("accessory.burrows.overalls_straps", AccessorySocket.Back, "OverallsStraps", 500, false),
        };

        public const string DefaultPalette = "default";
        public const string GoldPalette = "gold";
        public const string AccessorySource = "SourceArt/Blender/Accessories/BL_Accessories.blend";

        /// <summary>O-OUT-103-01..07: « outfit.burrows.01 »…</summary>
        public static string Outfit(int n) => "outfit.burrows." + n.ToString("00");

        /// <summary>The pieces (indexes in <see cref="Accessories"/>) and the palette of each outfit; 07 is the deluxe overalls (bib and straps).</summary>
        public static readonly (int[] pieces, string palette)[] Outfits =
        {
            (new[] { 0 }, DefaultPalette), (new[] { 1 }, DefaultPalette), (new[] { 2 }, DefaultPalette), (new[] { 3 }, DefaultPalette),
            (new[] { 4 }, GoldPalette), (new[] { 5 }, GoldPalette), (new[] { 6, 7 }, DefaultPalette),
        };

        // ---------------------------------------------------------------- decorations

        /// <summary>E-DEC-EVT-burrows-01..13: id and art name = the FBX object (Decor_Burrows_&lt;Name&gt;, PF_Decor_Burrows_&lt;Name&gt;, ICO_Decor_Burrows_&lt;Name&gt;; gold: ICO_Decor_Burrows_&lt;Name&gt;Gold). 1-4 track, 5-8 shop, 9-12 Box, 13 Souvenir.</summary>
        public static readonly (string id, string name)[] Decorations =
        {
            ("decor.burrows.flowery_entrance", "FlowerEntrance"),
            ("decor.burrows.moss_mound", "MossMound"),
            ("decor.burrows.dew_mushrooms", "DewMushrooms"),
            ("decor.burrows.watering_can", "WateringCan"),
            ("decor.burrows.mini_wheelbarrow", "Wheelbarrow"),
            ("decor.burrows.seedling_pots", "SeedlingPots"),
            ("decor.burrows.burrow_sign", "BurrowSign"),
            ("decor.burrows.earthworm_cushion", "WormCushion"),
            ("decor.burrows.garden_shed", "GardenShed"),
            ("decor.burrows.mini_greenhouse", "Greenhouse"),
            ("decor.burrows.wooden_bench", "WoodenBench"),
            ("decor.burrows.garden_fence", "GardenFence"),
            (Souvenir, "Souvenir"),
        };

        public static string Deco(int n) => Decorations[n - 1].id;

        /// <summary>The gold versions of the 4 track decorations (same models, gold palette).</summary>
        public static string GoldDeco(int n) => Decorations[n - 1].id + "_gold";

        public static string DecoIcon(string name) => "ICO_Decor_Burrows_" + name;

        // ---------------------------------------------------------------- menu

        [MenuItem("Pocket Wilds/Events/Build Burrow Awakening (T49)", priority = 196)]
        public static void BuildAllMenu() => Debug.Log("[BurrowsEventPipeline] " + BuildAll());

        [MenuItem("Pocket Wilds/Events/Build LiveOps_Burrows Group (T49)", priority = 197)]
        public static void BuildGroupMenu() => Debug.Log("[BurrowsEventPipeline] " + BurrowsContentGroup.Build());

        /// <summary>Writes both assets; adds them to the content database when the registry builds with them.</summary>
        public static string BuildAll()
        {
            Directory.CreateDirectory(Folder);
            var catalog = LoadOrCreate<ShopCatalogAsset>(CatalogPath);
            FillCatalog(catalog);
            EditorUtility.SetDirty(catalog);
            var definition = LoadOrCreate<EventDefinitionAsset>(EventPath);
            FillEvent(definition);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(PocketWilds.Editor.UI.UiPipeline.ContentDatabasePath);
            database.shopCatalogs.RemoveAll(c => c == null);
            database.events.RemoveAll(e => e == null);
            bool hadCatalog = database.shopCatalogs.Contains(catalog), hadEvent = database.events.Contains(definition);
            if (!hadCatalog)
                database.shopCatalogs.Add(catalog);
            if (!hadEvent)
                database.events.Add(definition);
            var builder = database.CreateBuilder();
            string issues;
            try
            {
                builder.Build();
                issues = builder.Issues.Count == 0 ? "none" : string.Join("; ", builder.Issues);
            }
            catch (InvalidOperationException e)
            {
                // The mole's variants are not in the database yet: keep the assets out until the animal pipeline ran.
                if (!hadCatalog)
                    database.shopCatalogs.Remove(catalog);
                if (!hadEvent)
                    database.events.Remove(definition);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                return "NOT ATTACHED (run the animal pipeline for the mole and the event variants, then again): " + e.Message;
            }
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return $"event.burrows: {definition.tiers.Count} tiers, {definition.eggItems.Count} egg items, {definition.shop.Count} shop lines, " +
                   $"{definition.memory.Count} memory lines; catalog: {catalog.entitlements.Count} items, {catalog.accessories.Count} accessories, " +
                   $"{catalog.outfits.Count} outfits; issues: {issues}";
        }

        /// <summary>The runtime definition built from a fresh in-memory asset (tests, the pacing simulator).</summary>
        public static EventDefinition Build()
        {
            var asset = ScriptableObject.CreateInstance<EventDefinitionAsset>();
            try
            {
                FillEvent(asset);
                return asset.ToRuntime();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        /// <summary>Adds the burrows catalog (items, accessories, outfits) to a builder (tests).</summary>
        public static void AddCatalog(ContentRegistryBuilder builder)
        {
            var catalog = ScriptableObject.CreateInstance<ShopCatalogAsset>();
            try
            {
                FillCatalog(catalog);
                catalog.AddTo(builder);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        // ---------------------------------------------------------------- the event

        public static void FillEvent(EventDefinitionAsset a)
        {
            a.sourceId = Key;
            a.version = 1;
            a.nameKey = NameKey;
            a.currencyKey = CurrencyKey;
            a.currencyIconKey = CurrencyIcon;
            a.featuredSpecies = FeaturedSpecies;
            a.featuredActivities = new List<int> { (int)ActivityId.HideAndSeek };
            // Cache-cache is also the fallback: it is open to every player who can play activities.
            a.fallbackActivity = (int)ActivityId.HideAndSeek;
            a.albumEventNumber = AlbumNumber;
            a.tiers = new List<EventDefinitionAsset.Tier>();
            for (int t = 1; t <= EventDefinition.TierCount; t++)
                a.tiers.Add(new EventDefinitionAsset.Tier { number = t, free = FreeLine(t), gold = GoldLine(t) });
            // Returning year: T31 to T36 give back the track's items (an item already owned becomes 100 Gems).
            a.bonusTiers = new List<EventDefinitionAsset.Tier>
            {
                Bonus(31, R(EventRewardKind.Decoration, Deco(1))), Bonus(32, R(EventRewardKind.Decoration, Deco(2))),
                Bonus(33, R(EventRewardKind.Outfit, Outfit(1))), Bonus(34, R(EventRewardKind.Decoration, Deco(3))),
                Bonus(35, R(EventRewardKind.Outfit, Outfit(2))), Bonus(36, R(EventRewardKind.Decoration, Deco(4))),
            };
            a.eggItems = new List<EventDefinitionAsset.EggItem>();
            for (int i = 0; i < EggVariants.Length; i++)
                a.eggItems.Add(new EventDefinitionAsset.EggItem { variant = EggVariants[i], featured = i < 3 });
            a.secretVariant = StarNose;
            a.shop = new List<EventDefinitionAsset.ShopLine>
            {
                Line("shop.egg", EventShopKind.Egg, EventPriceSlot.Egg, new EventDefinitionAsset.Reward { kind = EventRewardKind.None, amount = 0 }),
                Line("shop.outfit_4", EventShopKind.Item, EventPriceSlot.Outfit, R(EventRewardKind.Outfit, Outfit(4))),
                Line("shop.deco_5", EventShopKind.Item, EventPriceSlot.Decoration, R(EventRewardKind.Decoration, Deco(5))),
                Line("shop.deco_6", EventShopKind.Item, EventPriceSlot.Decoration, R(EventRewardKind.Decoration, Deco(6))),
                Line("shop.deco_7", EventShopKind.Item, EventPriceSlot.Decoration7, R(EventRewardKind.Decoration, Deco(7))),
                Line("shop.deco_8", EventShopKind.Item, EventPriceSlot.Decoration8, R(EventRewardKind.Decoration, Deco(8))),
                Line("shop.tickets", EventShopKind.Tickets, EventPriceSlot.Tickets, new EventDefinitionAsset.Reward { kind = EventRewardKind.Tickets, amount = 3 }),
                Line("shop.sticker", EventShopKind.Sticker, EventPriceSlot.Sticker, new EventDefinitionAsset.Reward { kind = EventRewardKind.None, amount = 0 }),
                Line("shop.seeds", EventShopKind.Seeds, EventPriceSlot.Seeds, new EventDefinitionAsset.Reward { kind = EventRewardKind.None, amount = 3, item = SeedPlant }),
            };
            a.memory = new List<EventDefinitionAsset.MemoryLine>
            {
                Memory("memory.variant_1", EventPriceSlot.MemoryVariant, R(EventRewardKind.Variant, Velvet)),
                Memory("memory.outfit_1", EventPriceSlot.Outfit, R(EventRewardKind.Outfit, Outfit(1))),
                Memory("memory.outfit_2", EventPriceSlot.Outfit, R(EventRewardKind.Outfit, Outfit(2))),
                Memory("memory.outfit_3", EventPriceSlot.Outfit, R(EventRewardKind.Outfit, Outfit(3))),
                Memory("memory.outfit_4", EventPriceSlot.Outfit, R(EventRewardKind.Outfit, Outfit(4))),
                Memory("memory.deco_1", EventPriceSlot.Decoration, R(EventRewardKind.Decoration, Deco(1))),
                Memory("memory.deco_2", EventPriceSlot.Decoration, R(EventRewardKind.Decoration, Deco(2))),
                Memory("memory.deco_3", EventPriceSlot.Decoration, R(EventRewardKind.Decoration, Deco(3))),
                Memory("memory.deco_4", EventPriceSlot.Decoration, R(EventRewardKind.Decoration, Deco(4))),
                Memory("memory.deco_5", EventPriceSlot.Decoration, R(EventRewardKind.Decoration, Deco(5))),
                Memory("memory.deco_6", EventPriceSlot.Decoration, R(EventRewardKind.Decoration, Deco(6))),
                Memory("memory.deco_7", EventPriceSlot.Decoration7, R(EventRewardKind.Decoration, Deco(7))),
                Memory("memory.deco_8", EventPriceSlot.Decoration8, R(EventRewardKind.Decoration, Deco(8))),
                Memory("memory.frame", EventPriceSlot.MemoryFrame, R(EventRewardKind.Frame, Frame, Frame)),
                Memory("memory.pose", EventPriceSlot.MemoryPose, R(EventRewardKind.Pose, Pose, Pose)),
            };
            // The 10 challenges are Cache-cache's own (challenge.hide_seek.01..10), at their fixed levels.
            var rules = HideSeekRules.Create();
            a.challengeLevels = new List<int>();
            for (int i = 0; i < EventDefinition.ChallengeCount; i++)
                a.challengeLevels.Add(rules.Challenges[i].Level);
            a.challengeOutfit = Outfit(3);
            a.souvenir = Souvenir;
            a.boxItems = new List<string> { Outfit(7), Deco(9), Deco(10), Deco(11), Deco(12) };
            a.headline = new List<EventDefinitionAsset.Reward>
            {
                R(EventRewardKind.Variant, Velvet), R(EventRewardKind.Outfit, Outfit(1)), R(EventRewardKind.Frame, Frame, Frame),
            };
            a.themeKeys = new List<string> { ThemeKey };
            a.themeColors = new List<Color32>();
            foreach (uint c in ThemeColors)
                a.themeColors.Add(new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, 255));
        }

        /// <summary>The free line (brief TRACK, left column).</summary>
        public static List<EventDefinitionAsset.Reward> FreeLine(int tier) => tier switch
        {
            1 => L(Sticker(1)),
            2 => L(Gems(25)),
            3 => L(R(EventRewardKind.Decoration, Deco(1))),
            4 => L(Tickets(3)),
            5 => L(R(EventRewardKind.Outfit, Outfit(1)), R(EventRewardKind.Souvenir, Souvenir)),
            6 => L(Sticker(6)),
            7 => L(Egg()),
            8 => L(R(EventRewardKind.Decoration, Deco(2))),
            9 => L(Gems(25)),
            10 => L(R(EventRewardKind.Variant, Velvet)),
            11 => L(Sticker(11)),
            12 => L(Tickets(3)),
            13 => L(R(EventRewardKind.Decoration, Deco(3))),
            14 => L(Gems(25)),
            15 => L(Treats(5)),
            16 => L(Tickets(3)),
            17 => L(Egg()),
            18 => L(R(EventRewardKind.Decoration, Deco(4))),
            19 => L(Sticker(19)),
            20 => L(R(EventRewardKind.Outfit, Outfit(2))),
            21 => L(Gems(25)),
            22 => L(new EventDefinitionAsset.Reward { kind = EventRewardKind.LightMinutes, amount = 60 }),
            23 => L(Sticker(23)),
            24 => L(Tickets(3)),
            25 => L(Gems(25)),
            26 => L(Sticker(26)),
            27 => L(Egg()),
            28 => L(Tickets(3)),
            29 => L(Gems(25)),
            _ => L(R(EventRewardKind.Frame, Frame, Frame)),
        };

        /// <summary>The Carnet d'or line (brief TRACK, right column); never Light, eggs, stickers or currency.</summary>
        public static List<EventDefinitionAsset.Reward> GoldLine(int tier) => tier switch
        {
            1 or 4 or 12 or 17 or 23 or 27 => L(Gems(50)),
            2 or 7 or 11 or 16 or 21 or 24 or 28 => L(Tickets(3)),
            3 => L(R(EventRewardKind.Decoration, GoldDeco(1))),
            5 => L(R(EventRewardKind.HatchEffect, HatchEffect)),
            6 or 9 or 14 or 19 or 22 or 26 => L(Treats(5)),
            8 => L(R(EventRewardKind.Decoration, GoldDeco(2))),
            10 => L(R(EventRewardKind.Outfit, Outfit(5))),
            13 => L(R(EventRewardKind.Decoration, GoldDeco(3))),
            15 => L(R(EventRewardKind.Variant, GoldenMoss)),
            18 => L(R(EventRewardKind.Decoration, GoldDeco(4))),
            20 => L(R(EventRewardKind.Frame, GoldFrame, GoldFrame)),
            25 => L(R(EventRewardKind.Outfit, Outfit(6))),
            29 => L(Treats(10)),
            _ => L(R(EventRewardKind.Monument, Monument)),
        };

        // ---------------------------------------------------------------- the catalog

        public static void FillCatalog(ShopCatalogAsset c)
        {
            c.unlockMinAnimals = 3;
            c.products = new List<ShopCatalogAsset.Product>();
            c.offers = new List<ShopCatalogAsset.Offer>();
            c.entitlements = new List<ShopCatalogAsset.Entitlement>();
            var fresh = new List<ShopCatalogAsset.Accessory>();
            foreach (var row in Accessories)
            {
                c.entitlements.Add(Item(row.Id, EntitlementKind.Cosmetic, "ICO_Accessory_" + row.Name));
                fresh.Add(new ShopCatalogAsset.Accessory
                {
                    sourceId = row.Id, displayKey = row.Id + ".name", socket = row.Socket,
                    families = new List<ShopCatalogAsset.FamilyFit>
                    {
                        new ShopCatalogAsset.FamilyFit { family = RigFamily.SmallQuadruped, localScale = Vector3.one },
                        new ShopCatalogAsset.FamilyFit { family = RigFamily.MediumQuadruped, localScale = Vector3.one * 1.3f },
                    },
                    // Gold pieces are gold by default (index 0); the plain look stays available. Append only.
                    palettes = row.Gold ? new List<string> { GoldPalette, DefaultPalette } : new List<string> { DefaultPalette },
                    trisBudget = row.Tris, iconKey = "ICO_Accessory_" + row.Name, visualKey = "ACC_" + row.Name, sourceKey = AccessorySource,
                });
            }
            // The fitting pipeline's measured fits and exclusions survive a rebuild.
            c.accessories = PocketWilds.Editor.Shop.ShopPipeline.KeepMeasuredFits(fresh, c.accessories);
            c.outfits = new List<ShopCatalogAsset.Outfit>();
            for (int i = 0; i < Outfits.Length; i++)
            {
                // An outfit is owned through its pieces (its id is claimed by the outfit table, never an item of its own).
                string id = Outfit(i + 1);
                var pieces = new List<string>();
                foreach (int p in Outfits[i].pieces)
                    pieces.Add(Accessories[p].Id);
                c.outfits.Add(new ShopCatalogAsset.Outfit
                {
                    sourceId = id, displayKey = id + ".name", accessories = pieces, palette = Outfits[i].palette,
                    iconKey = "ICO_Outfit_Burrows_" + (i + 1).ToString("00"), sourceKey = AccessorySource,
                });
            }
            foreach (var (id, name) in Decorations)
                c.entitlements.Add(Item(id, EntitlementKind.Decoration, DecoIcon(name)));
            for (int n = 1; n <= 4; n++)
                c.entitlements.Add(Item(GoldDeco(n), EntitlementKind.Decoration, DecoIcon(Decorations[n - 1].name + "Gold")));
            c.entitlements.Add(Item(Monument, EntitlementKind.Decoration, DecoIcon("RootBurrow")));
            c.entitlements.Add(Item(HatchEffect, EntitlementKind.Cosmetic, "ICO_Effect_SproutBurst"));
            // The profile knows frame.event_burrows (FrameArt, ProfileData); its gold version is an owned item until the profile lists it.
            c.entitlements.Add(Item(GoldFrame, EntitlementKind.Cosmetic, "ICO_Frame_EventBurrowsGold"));
        }

        /// <summary>Every item id the event can give (tests).</summary>
        public static IEnumerable<string> ItemIds()
        {
            foreach (var row in Accessories)
                yield return row.Id;
            for (int i = 1; i <= Outfits.Length; i++)
                yield return Outfit(i);
            foreach (var (id, _) in Decorations)
                yield return id;
            for (int n = 1; n <= 4; n++)
                yield return GoldDeco(n);
            yield return Monument;
            yield return HatchEffect;
            yield return GoldFrame;
        }

        // ---------------------------------------------------------------- helpers

        private static ShopCatalogAsset.Entitlement Item(string id, EntitlementKind kind, string icon) =>
            new ShopCatalogAsset.Entitlement { sourceId = id, kind = kind, displayKey = id + ".name", iconKey = icon };

        private static EventDefinitionAsset.Tier Bonus(int number, EventDefinitionAsset.Reward reward) =>
            new EventDefinitionAsset.Tier { number = number, free = L(reward), gold = new List<EventDefinitionAsset.Reward>() };

        private static EventDefinitionAsset.ShopLine Line(string key, EventShopKind kind, EventPriceSlot price, EventDefinitionAsset.Reward reward) =>
            new EventDefinitionAsset.ShopLine { key = key, kind = kind, price = price, reward = reward };

        private static EventDefinitionAsset.MemoryLine Memory(string key, EventPriceSlot price, EventDefinitionAsset.Reward reward) =>
            new EventDefinitionAsset.MemoryLine { key = key, price = price, reward = reward };

        private static List<EventDefinitionAsset.Reward> L(params EventDefinitionAsset.Reward[] rewards) => new List<EventDefinitionAsset.Reward>(rewards);

        private static EventDefinitionAsset.Reward R(EventRewardKind kind, string item, string key = "") =>
            new EventDefinitionAsset.Reward { kind = kind, amount = 1, item = item, key = key };

        private static EventDefinitionAsset.Reward Gems(int n) => new EventDefinitionAsset.Reward { kind = EventRewardKind.Gems, amount = n };
        private static EventDefinitionAsset.Reward Tickets(int n) => new EventDefinitionAsset.Reward { kind = EventRewardKind.Tickets, amount = n };

        /// <summary>Treats of the player's newest biome (the event's default treat).</summary>
        private static EventDefinitionAsset.Reward Treats(int n) => new EventDefinitionAsset.Reward { kind = EventRewardKind.Treats, amount = n };

        private static EventDefinitionAsset.Reward Egg() => new EventDefinitionAsset.Reward { kind = EventRewardKind.DiscoveryEgg, amount = 1 };
        private static EventDefinitionAsset.Reward Sticker(int tier) => new EventDefinitionAsset.Reward { kind = EventRewardKind.Sticker, amount = tier };

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
