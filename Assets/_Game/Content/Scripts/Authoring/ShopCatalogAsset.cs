using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// Authoring asset for the shop (DEF_ShopCatalog, built by Pocket Wilds/Shop/Build Sandbox Catalog): store
    /// products, offers and the items they grant. Commercial values are data: prices, amounts, eligibility and
    /// limits change here (or by remote config later), never in code. Runtime: <see cref="ProductDefinition"/>,
    /// <see cref="OfferDefinition"/>, <see cref="EntitlementDefinition"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Shop Catalog", fileName = "DEF_ShopCatalog")]
    public sealed class ShopCatalogAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Product
        {
            public string sourceId = "product.new";
            public string storeSku = "pw_new";
            public ProductKind kind;
            [Tooltip("Baseline price in euro cents (doc 09). Sandbox store and analytics only: the UI shows the store's price.")]
            public int referencePriceCents = 99;
            public long gems;
            public List<string> entitlements = new List<string>();
        }

        [Serializable]
        public sealed class Offer
        {
            public string sourceId = "offer.new";
            public string displayKey = string.Empty;
            public OfferPlacement placement;
            public int priority;
            [Tooltip("Store product source id. Empty for a Gem-priced offer.")]
            public string product = string.Empty;
            public long gemCost;
            public List<string> gemCostEntitlements = new List<string>();
            [Tooltip("0 = no limit.")]
            public int purchaseLimit;
            public int cooldownSeconds;
            [Tooltip("Unix seconds, 0 = open.")]
            public long startUnixSeconds;
            [Tooltip("Unix seconds, 0 = open. Only a real end date may show a timer.")]
            public long endUnixSeconds;
            public int minAnimals;
            public int minDiscoveredSpecies;
            public OfferSegment segment;
            public OfferBadge badge;
            public string artKey = string.Empty;
            public string experimentId = string.Empty;
        }

        [Serializable]
        public sealed class Entitlement
        {
            public string sourceId = "entitlement.new";
            public EntitlementKind kind;
            public string displayKey = "entitlement.new.name";
            public string iconKey = string.Empty;
        }

        [Tooltip("The shop shortcut appears once the player owns this many animals (doc 06 UI: after onboarding).")]
        public int unlockMinAnimals = 3;

        public List<Product> products = new List<Product>();
        public List<Offer> offers = new List<Offer>();
        public List<Entitlement> entitlements = new List<Entitlement>();

        public void AddTo(ContentRegistryBuilder builder)
        {
            builder.SetShopUnlock(unlockMinAnimals);
            foreach (var e in entitlements)
                builder.AddEntitlement(new EntitlementDefinition(ContentId.FromSource(e.sourceId), e.kind, e.displayKey, e.iconKey), e.sourceId);
            foreach (var p in products)
                builder.AddProduct(new ProductDefinition(ContentId.FromSource(p.sourceId), p.storeSku, p.kind, p.referencePriceCents, p.gems, Ids(p.entitlements)), p.sourceId);
            foreach (var o in offers)
            {
                var product = string.IsNullOrEmpty(o.product) ? default : ContentId.FromSource(o.product);
                builder.AddOffer(new OfferDefinition(ContentId.FromSource(o.sourceId), o.displayKey, o.placement, o.priority, product, o.gemCost,
                    Ids(o.gemCostEntitlements), o.purchaseLimit, o.cooldownSeconds, o.startUnixSeconds, o.endUnixSeconds, o.minAnimals,
                    o.minDiscoveredSpecies, o.segment, o.badge, o.artKey, o.experimentId), o.sourceId);
            }
        }

        private static ContentId[] Ids(List<string> sources)
        {
            var ids = new ContentId[sources.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = ContentId.FromSource(sources[i]);
            return ids;
        }
    }
}
