#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Content
{
    /// <summary>How the platform store treats a product. Stable byte values.</summary>
    public enum ProductKind : byte
    {
        /// <summary>Bought again and again (Gem packs).</summary>
        Consumable = 0,

        /// <summary>Bought once, restorable (Starter Pack, permanent cosmetics).</summary>
        NonConsumable = 1,
    }

    /// <summary>
    /// One store product (M-DEF-PRODUCT, doc 09 Catalogue IAP): the platform SKU and what a successful purchase
    /// grants. The price shown to the player always comes from the store provider, localized: the reference price
    /// here only feeds the sandbox store and analytics, never the UI.
    /// </summary>
    public sealed class ProductDefinition : IContentDefinition
    {
        public ProductDefinition(ContentId id, string storeSku, ProductKind kind, int referencePriceCents, long gems, ContentId[] entitlements)
        {
            Id = id;
            StoreSku = storeSku;
            Kind = kind;
            ReferencePriceCents = referencePriceCents;
            Gems = gems;
            Entitlements = entitlements ?? Array.Empty<ContentId>();
        }

        public ContentId Id { get; }

        /// <summary>Product id in the platform consoles (Google Play, App Store).</summary>
        public string StoreSku { get; }

        public ProductKind Kind { get; }

        /// <summary>Baseline price in euro cents (doc 09). Sandbox store and analytics only.</summary>
        public int ReferencePriceCents { get; }

        /// <summary>Gems granted per purchase.</summary>
        public long Gems { get; }

        /// <summary>Permanent items granted (cosmetics, decorations, perks).</summary>
        public ContentId[] Entitlements { get; }
    }
}
