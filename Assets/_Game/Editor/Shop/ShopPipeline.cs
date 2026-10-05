using System.Collections.Generic;
using System.IO;
using PocketWilds.Content;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Shop
{
    /// <summary>
    /// ED-005 Offer editor, sandbox scope (T28): writes DEF_ShopCatalog with the doc 09 baseline: six Gem packs
    /// (0,99 to 99,99 euros) and the Starter Pack ("500 Gems + Flower Crown", Writing Guide example), plus the
    /// Starter Pack as a moment offer. Gem amounts and the Starter price are starting values to balance and
    /// approve: commercial values live here, as data, never in code. Idempotent.
    /// </summary>
    public static class ShopPipeline
    {
        public const string CatalogPath = "Assets/_Game/Content/Shop/DEF_ShopCatalog.asset";
        public const string StarterOffer = "offer.starter.001";
        public const string StarterMoment = "offer.starter.moment";
        public const string FlowerCrown = "cosmetic.flower_crown";

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
            };

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

            catalog.products.Add(new ShopCatalogAsset.Product
            {
                sourceId = "product.starter.001", storeSku = "pw_starter_001", kind = ProductKind.NonConsumable,
                referencePriceCents = StarterPriceCents, gems = StarterGems, entitlements = new List<string> { FlowerCrown },
            });
            // Doc 09: after several hatches and a first attachment, never at the raw start; new non-payers only.
            catalog.offers.Add(new ShopCatalogAsset.Offer
            {
                sourceId = StarterOffer, displayKey = "shop.starter_pack.title", placement = OfferPlacement.ShopFeatured, priority = 100,
                product = "product.starter.001", purchaseLimit = 1, minAnimals = 5, minDiscoveredSpecies = 2,
                segment = OfferSegment.NonPayer, artKey = "ICO_Cosmetic_FlowerCrown",
            });
            // Same product, offered once after a happy hatch; back at most every 3 days if closed. No timer: it
            // has no end date.
            catalog.offers.Add(new ShopCatalogAsset.Offer
            {
                sourceId = StarterMoment, displayKey = "shop.starter_pack.title", placement = OfferPlacement.Moment, priority = 100,
                product = "product.starter.001", purchaseLimit = 1, cooldownSeconds = 3 * 24 * 3600, minAnimals = 5, minDiscoveredSpecies = 2,
                segment = OfferSegment.NonPayer, artKey = "ICO_Cosmetic_FlowerCrown",
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
            return $"{registry.Shop.Products.Count} products, {registry.Shop.Offers.Count} offers, {registry.Shop.Entitlements.Count} items; " +
                   $"issues: {(builder.Issues.Count == 0 ? "none" : string.Join("; ", builder.Issues))}";
        }
    }
}
