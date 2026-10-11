using System.Collections.Generic;
using System.IO;
using PocketWilds.Content;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Shop
{
    /// <summary>
    /// ED-005 Offer editor, sandbox scope (T28): writes DEF_ShopCatalog with the doc 09 baseline: six Gem packs
    /// (0,99 to 99,99 euros) and the Meadow Pack (T47, « Pack de la Prairie »: Field Hamster + 500 Gems + Cozy Burrow + Flower
    /// Crown, Light +5 % for the Meadow while owned; it keeps the Starter Pack's product id product.starter.001), featured and as
    /// a moment offer, plus the accessories (Flower Crown, Seed Basket). Gem amounts and the pack price are starting values to
    /// balance and approve: commercial values live here, as data, never in code. Idempotent.
    /// </summary>
    public static class ShopPipeline
    {
        public const string CatalogPath = "Assets/_Game/Content/Shop/DEF_ShopCatalog.asset";
        public const string StarterOffer = "offer.starter.001";
        public const string StarterMoment = "offer.starter.moment";
        public const string FlowerCrown = "cosmetic.flower_crown";

        /// <summary>T47: the Meadow Pack keeps the Starter Pack's content id; its store SKU and contents changed.</summary>
        public const string MeadowPackProduct = "product.starter.001";

        /// <summary>T48: the Carnet d'or (4.99 €) and the Box (9.99 €, 800 Gems), one purchase per event instance.</summary>
        public const string EventGoldBookProduct = "product.event.goldbook";
        public const string EventChestProduct = "product.event.chest";
        public const string MeadowPackSku = "pw.pack.grassland";
        public const string MeadowPackTitle = "shop.pack.grassland.title";
        public const string CompanionFieldHamster = "companion.fieldhamster";
        public const string CozyBurrow = "decor.cozy_burrow";
        public const string SeedBasket = "accessory.fieldhamster.seedbasket";

        /// <summary>Palettes of the shared accessory material (append only: saves keep the index).</summary>
        public static readonly string[] AccessoryPalettes = { "meadow", "blossom", "frost" };

        /// <summary>Gem packs: reference price (cents), Gems. Bonus grows with the pack (doc 09); values to approve.</summary>
        public static readonly (int cents, long gems)[] GemPacks =
        {
            (99, 100), (499, 550), (999, 1150), (1999, 2400), (4999, 6250), (9999, 13000),
        };

        public const int StarterPriceCents = 199;
        public const long StarterGems = 500;

        [MenuItem("Pocket Wilds/Shop/Build Sandbox Catalog (T28)", priority = 190)]
        public static void BuildMenu() => Debug.Log("[ShopPipeline] " + Build());

        public static string Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ShopCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.unlockMinAnimals = 3;
            catalog.entitlements = new List<ShopCatalogAsset.Entitlement>
            {
                new ShopCatalogAsset.Entitlement
                {
                    sourceId = FlowerCrown, kind = EntitlementKind.Cosmetic,
                    displayKey = "entitlement.cosmetic.flower_crown.name", iconKey = "ICO_Cosmetic_FlowerCrown",
                },
                // T47: the Meadow Pack's companion and decoration, and the hamster's bond 15 accessory (never sold).
                new ShopCatalogAsset.Entitlement
                {
                    sourceId = CompanionFieldHamster, kind = EntitlementKind.Companion,
                    displayKey = "species.fieldhamster.name", iconKey = "ICO_Companion_FieldHamster",
                },
                new ShopCatalogAsset.Entitlement
                {
                    sourceId = CozyBurrow, kind = EntitlementKind.Decoration, displayKey = CozyBurrow + ".name", iconKey = "ICO_Decor_CozyBurrow",
                },
                new ShopCatalogAsset.Entitlement
                {
                    sourceId = SeedBasket, kind = EntitlementKind.Cosmetic, displayKey = SeedBasket + ".name", iconKey = "ICO_Accessory_SeedBasket",
                },
            };
            // The fitting pipeline's measured fits and exclusions survive a catalog rebuild (AccessoryFitPipeline writes them).
            catalog.accessories = KeepMeasuredFits(BuildAccessories(), catalog.accessories);
            catalog.outfits = new List<ShopCatalogAsset.Outfit>();

            catalog.products = new List<ShopCatalogAsset.Product>();
            catalog.offers = new List<ShopCatalogAsset.Offer>();
            for (int i = 0; i < GemPacks.Length; i++)
            {
                string tier = (i + 1).ToString("00");
                catalog.products.Add(new ShopCatalogAsset.Product
                {
                    sourceId = "product.gems.tier" + tier, storeSku = "pw_gems_tier" + tier, kind = ProductKind.Consumable,
                    referencePriceCents = GemPacks[i].cents, gems = GemPacks[i].gems,
                });
                catalog.offers.Add(new ShopCatalogAsset.Offer
                {
                    sourceId = "offer.gems.tier" + tier, placement = OfferPlacement.ShopGems, priority = 60 - i * 10,
                    product = "product.gems.tier" + tier, artKey = "ICO_GemPack_" + tier,
                    // The biggest pack has the biggest bonus: the badge tells the truth.
                    badge = i == GemPacks.Length - 1 ? OfferBadge.BestValue : OfferBadge.None,
                });
            }

            // T47 Meadow Pack: one purchase per account, restorable (the companion, the burrow and the crown come back, never the Gems).
            catalog.products.Add(new ShopCatalogAsset.Product
            {
                sourceId = MeadowPackProduct, storeSku = MeadowPackSku, kind = ProductKind.NonConsumable,
                referencePriceCents = StarterPriceCents, gems = StarterGems,
                entitlements = new List<string> { CompanionFieldHamster, CozyBurrow, FlowerCrown },
            });
            // Doc 09: after several hatches and a first attachment, never at the raw start; new non-payers only.
            catalog.offers.Add(new ShopCatalogAsset.Offer
            {
                sourceId = StarterOffer, displayKey = MeadowPackTitle, placement = OfferPlacement.ShopFeatured, priority = 100,
                product = MeadowPackProduct, purchaseLimit = 1, minAnimals = 5, minDiscoveredSpecies = 2,
                segment = OfferSegment.NonPayer, artKey = "ICO_Cosmetic_FlowerCrown",
            });
            // Same product, offered once after a happy hatch; back at most every 3 days if closed. No timer: it
            // has no end date.
            catalog.offers.Add(new ShopCatalogAsset.Offer
            {
                sourceId = StarterMoment, displayKey = MeadowPackTitle, placement = OfferPlacement.Moment, priority = 100,
                product = MeadowPackProduct, purchaseLimit = 1, cooldownSeconds = 3 * 24 * 3600, minAnimals = 5, minDiscoveredSpecies = 2,
                segment = OfferSegment.NonPayer, artKey = "ICO_Cosmetic_FlowerCrown",
            });
            // T48: the Carnet d'or and the Box, one purchase per event instance (items « <instance>.gold » and « .box » resolved by
            // the event service; the Box adds outfit 7, 4 luxury decorations and its 800 Gems), sold in the shop's « Événement »
            // section while an event runs, never as a popup.
            catalog.products.RemoveAll(p => p.sourceId == EventGoldBookProduct || p.sourceId == EventChestProduct);
            catalog.offers.RemoveAll(o => o.sourceId == "offer.event.goldbook" || o.sourceId == "offer.event.chest");
            catalog.products.Add(new ShopCatalogAsset.Product
            {
                sourceId = EventGoldBookProduct, storeSku = "pw.event.goldbook", kind = ProductKind.Consumable, referencePriceCents = 499,
                gems = 0, eventScope = EventProductScope.GoldBook,
            });
            catalog.products.Add(new ShopCatalogAsset.Product
            {
                sourceId = EventChestProduct, storeSku = "pw.event.chest", kind = ProductKind.Consumable, referencePriceCents = 999,
                gems = 800, eventScope = EventProductScope.Box,
            });
            catalog.offers.Add(new ShopCatalogAsset.Offer
            {
                sourceId = "offer.event.goldbook", displayKey = "event.gold.title", placement = OfferPlacement.ShopEvent, priority = 90,
                product = EventGoldBookProduct, artKey = "ICO_Event_GoldBook",
            });
            catalog.offers.Add(new ShopCatalogAsset.Offer
            {
                sourceId = "offer.event.chest", displayKey = "event.box.title", placement = OfferPlacement.ShopEvent, priority = 80,
                product = EventChestProduct, artKey = "ICO_Event_Box",
            });
            EditorUtility.SetDirty(catalog);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(PocketWilds.Editor.UI.UiPipeline.ContentDatabasePath);
            database.shopCatalogs.RemoveAll(c => c == null);
            if (!database.shopCatalogs.Contains(catalog))
                database.shopCatalogs.Add(catalog);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            var builder = database.CreateBuilder();
            var registry = builder.Build();
            return $"{registry.Shop.Products.Count} products, {registry.Shop.Offers.Count} offers, {registry.Shop.Entitlements.Count} items, " +
                   $"{registry.Shop.Accessories.Count} accessories; " +
                   $"issues: {(builder.Issues.Count == 0 ? "none" : string.Join("; ", builder.Issues))}";
        }

        /// <summary>
        /// T47 accessories (doc Accessoires): one mesh in neutral rig space per accessory, a fitting profile per rig family (starting
        /// values; the fitting pipeline writes the measured per-species fits into speciesFits), one shared accessory material.
        /// </summary>
        public static List<ShopCatalogAsset.Accessory> KeepMeasuredFits(List<ShopCatalogAsset.Accessory> fresh, List<ShopCatalogAsset.Accessory> previous)
        {
            if (previous == null)
                return fresh;
            foreach (var accessory in fresh)
            {
                var old = previous.Find(a => a != null && a.sourceId == accessory.sourceId);
                if (old == null)
                    continue;
                accessory.speciesFits = old.speciesFits ?? new List<ShopCatalogAsset.SpeciesFit>();
                accessory.excludedSpecies = old.excludedSpecies ?? new List<string>();
            }
            return fresh;
        }

        public static List<ShopCatalogAsset.Accessory> BuildAccessories()
        {
            return new List<ShopCatalogAsset.Accessory>
            {
                // Flower Crown: HeadTop, under 350 triangles, every small and medium quadruped.
                new ShopCatalogAsset.Accessory
                {
                    sourceId = FlowerCrown, displayKey = "entitlement.cosmetic.flower_crown.name", socket = AccessorySocket.HeadTop,
                    families = new List<ShopCatalogAsset.FamilyFit>
                    {
                        new ShopCatalogAsset.FamilyFit { family = RigFamily.SmallQuadruped, localScale = Vector3.one },
                        new ShopCatalogAsset.FamilyFit { family = RigFamily.MediumQuadruped, localScale = Vector3.one * 1.3f },
                    },
                    palettes = new List<string>(AccessoryPalettes), trisBudget = 350,
                    iconKey = "ICO_Cosmetic_FlowerCrown", visualKey = "ACC_FlowerCrown", sourceKey = "SourceArt/Blender/Accessories/BL_Accessories.blend",
                },
                // Seed Basket: Back, about 180 triangles, every small quadruped (the Field Hamster's bond 15 reward).
                new ShopCatalogAsset.Accessory
                {
                    sourceId = SeedBasket, displayKey = SeedBasket + ".name", socket = AccessorySocket.Back,
                    families = new List<ShopCatalogAsset.FamilyFit>
                    {
                        new ShopCatalogAsset.FamilyFit { family = RigFamily.SmallQuadruped, localScale = Vector3.one },
                    },
                    palettes = new List<string>(AccessoryPalettes), trisBudget = 200,
                    iconKey = "ICO_Accessory_SeedBasket", visualKey = "ACC_SeedBasket", sourceKey = "SourceArt/Blender/Accessories/BL_Accessories.blend",
                },
            };
        }
    }
}
