#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Content
{
    /// <summary>Where an offer can show. Stable byte values.</summary>
    public enum OfferPlacement : byte
    {
        /// <summary>Top of the shop, large card.</summary>
        ShopFeatured = 0,

        /// <summary>Gem pack grid.</summary>
        ShopGems = 1,

        /// <summary>Offered once after a positive moment (a hatch reveal), never after a failure.</summary>
        Moment = 2,

        /// <summary>T48: the shop's « Événement » section (Carnet d'or, Box) while an event runs; never a popup.</summary>
        ShopEvent = 3,
    }

    /// <summary>Doc 09 segments, reduced to what the sandbox can tell from the save.</summary>
    public enum OfferSegment : byte
    {
        Any = 0,
        NonPayer = 1,
        Payer = 2,
    }

    public enum OfferBadge : byte
    {
        None = 0,
        BestValue = 1,
    }

    /// <summary>
    /// One sellable offer (doc 06 OfferDefinition, doc 09 offer engine): what it sells (a store product or a
    /// Gem price with granted items), who can see it, where, how often and how many times. No text or price is
    /// hard-coded: titles are localization keys, real-money prices come from the store provider.
    /// </summary>
    public sealed class OfferDefinition : IContentDefinition
    {
        public OfferDefinition(ContentId id, string displayKey, OfferPlacement placement, int priority, ContentId product, long gemCost,
            ContentId[] gemCostEntitlements, int purchaseLimit, int cooldownSeconds, long startUnixSeconds, long endUnixSeconds,
            int minAnimals, int minDiscoveredSpecies, OfferSegment segment, OfferBadge badge, string artKey, string experimentId)
        {
            Id = id;
            DisplayKey = displayKey;
            Placement = placement;
            Priority = priority;
            Product = product;
            GemCost = gemCost;
            GemCostEntitlements = gemCostEntitlements ?? Array.Empty<ContentId>();
            PurchaseLimit = purchaseLimit;
            CooldownSeconds = cooldownSeconds;
            StartUnixSeconds = startUnixSeconds;
            EndUnixSeconds = endUnixSeconds;
            MinAnimals = minAnimals;
            MinDiscoveredSpecies = minDiscoveredSpecies;
            Segment = segment;
            Badge = badge;
            ArtKey = artKey;
            ExperimentId = experimentId;
        }

        public ContentId Id { get; }

        /// <summary>Title key. Empty for Gem packs: the card shows the amount instead.</summary>
        public string DisplayKey { get; }

        public OfferPlacement Placement { get; }

        /// <summary>Higher first within a placement.</summary>
        public int Priority { get; }

        /// <summary>Store product bought with real money. Invalid for Gem-priced offers.</summary>
        public ContentId Product { get; }

        /// <summary>Gem price (premium currency offers). 0 for store offers.</summary>
        public long GemCost { get; }

        /// <summary>Items granted by a Gem-priced offer.</summary>
        public ContentId[] GemCostEntitlements { get; }

        /// <summary>How many times one player can buy it. 0 = no limit.</summary>
        public int PurchaseLimit { get; }

        /// <summary>Moment offers: delay before showing again after the player closed it.</summary>
        public int CooldownSeconds { get; }

        /// <summary>Real availability window (Unix seconds, 0 = open). Only a real end date may show a timer.</summary>
        public long StartUnixSeconds { get; }
        public long EndUnixSeconds { get; }

        /// <summary>Eligibility: the player has enjoyed the loop first (doc 09: never at the raw start).</summary>
        public int MinAnimals { get; }
        public int MinDiscoveredSpecies { get; }
        public OfferSegment Segment { get; }

        public OfferBadge Badge { get; }

        /// <summary>Card art: an icon key (Gem cluster tier, item icon).</summary>
        public string ArtKey { get; }

        /// <summary>A/B experiment this offer belongs to (analytics metadata). Empty when none.</summary>
        public string ExperimentId { get; }

        public bool IsGemPriced => GemCost > 0;
        public bool HasEndDate => EndUnixSeconds > 0;
    }
}
