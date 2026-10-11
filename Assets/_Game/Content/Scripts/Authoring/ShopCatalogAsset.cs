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

        /// <summary>T47: one compatible rig family and its fitting profile (neutral rig space).</summary>
        [Serializable]
        public sealed class FamilyFit
        {
            public RigFamily family;
            public Vector3 localPosition;
            public Vector3 localEuler;
            public Vector3 localScale = Vector3.one;
        }

        /// <summary>T47: a measured fit for one species (written by the fitting pipeline); wins over the family profile.</summary>
        [Serializable]
        public sealed class SpeciesFit
        {
            public string speciesId = "species.new";
            public Vector3 localPosition;
            public Vector3 localEuler;
            public Vector3 localScale = Vector3.one;
        }

        /// <summary>T47: the wearable data of a Cosmetic item (same source id as the item).</summary>
        [Serializable]
        public sealed class Accessory
        {
            public string sourceId = "cosmetic.new";
            public string displayKey = "cosmetic.new.name";
            public AccessorySocket socket;
            public List<FamilyFit> families = new List<FamilyFit>();
            public List<SpeciesFit> speciesFits = new List<SpeciesFit>();
            [Tooltip("Species that never wear it (clipping that no fit solves, or a hidden socket).")]
            public List<string> excludedSpecies = new List<string>();
            [Tooltip("Palette names of the shared accessory material. Append only: saves keep the index.")]
            public List<string> palettes = new List<string>();
            public int trisBudget = 250;
            public string iconKey = string.Empty;
            public string visualKey = string.Empty;
            [Tooltip("Editable source (.blend and script) listed in SourceArt/Handmade_Sources.json.")]
            public string sourceKey = string.Empty;
        }

        /// <summary>T47: 1 to 3 accessories on different sockets with one shared palette.</summary>
        [Serializable]
        public sealed class Outfit
        {
            public string sourceId = "outfit.new";
            public string displayKey = "outfit.new.name";
            public List<string> accessories = new List<string>();
            public string palette = string.Empty;
            public string iconKey = string.Empty;
            public string sourceKey = string.Empty;
        }

        [Tooltip("The shop shortcut appears once the player owns this many animals (doc 06 UI: after onboarding).")]
        public int unlockMinAnimals = 3;

        public List<Product> products = new List<Product>();
        public List<Offer> offers = new List<Offer>();
        public List<Entitlement> entitlements = new List<Entitlement>();
        public List<Accessory> accessories = new List<Accessory>();
        public List<Outfit> outfits = new List<Outfit>();

        public void AddTo(ContentRegistryBuilder builder)
        {
            builder.SetShopUnlock(unlockMinAnimals);
            foreach (var e in entitlements)
                builder.AddEntitlement(new EntitlementDefinition(ContentId.FromSource(e.sourceId), e.kind, e.displayKey, e.iconKey), e.sourceId);
            foreach (var a in accessories)
            {
                var families = new AccessoryFamilyFit[a.families.Count];
                for (int i = 0; i < families.Length; i++)
                    families[i] = new AccessoryFamilyFit(a.families[i].family, new AccessoryFit(a.families[i].localPosition, a.families[i].localEuler, a.families[i].localScale));
                var species = new AccessorySpeciesFit[a.speciesFits.Count];
                for (int i = 0; i < species.Length; i++)
                    species[i] = new AccessorySpeciesFit(ContentId.FromSource(a.speciesFits[i].speciesId),
                        new AccessoryFit(a.speciesFits[i].localPosition, a.speciesFits[i].localEuler, a.speciesFits[i].localScale));
                builder.AddAccessory(new AccessoryDefinition(ContentId.FromSource(a.sourceId), a.displayKey, a.socket, families, species,
                    Ids(a.excludedSpecies), a.palettes.ToArray(), a.trisBudget, a.iconKey, a.visualKey, a.sourceKey), a.sourceId);
            }
            foreach (var o in outfits)
                builder.AddOutfit(new OutfitDefinition(ContentId.FromSource(o.sourceId), o.displayKey, Ids(o.accessories), o.palette, o.iconKey, o.sourceKey), o.sourceId);
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
