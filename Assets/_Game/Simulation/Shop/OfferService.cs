#nullable enable
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public enum OfferAvailability : byte
    {
        Available = 0,

        /// <summary>The player has not reached it yet (animals, species, segment): hidden.</summary>
        NotEligible = 1,

        /// <summary>Outside its real availability window: hidden.</summary>
        OutOfWindow = 2,

        /// <summary>Purchase limit reached or every item already owned: shown as owned where useful.</summary>
        Owned = 3,
    }

    public enum GemPurchaseStatus : byte
    {
        Purchased = 0,
        NotAvailable = 1,
        NotEnoughGems = 2,
        NotGemPriced = 3,
    }

    /// <summary>
    /// Offer engine (doc 09): which offers a player sees per placement, when a moment offer may show, and
    /// purchases made with Gems. Everything comes from the catalog data and the save; nothing is random and
    /// nothing allocates per query (callers pass the result buffer). Real-money purchases go through the store
    /// service, which reports them back with <see cref="RecordPurchase"/>.
    /// </summary>
    public sealed class OfferService
    {
        private readonly ContentRegistry _content;
        private readonly PlayerState _state;
        private readonly GrantService _grants;
        private readonly ISaveScheduler? _save;

        public OfferService(ContentRegistry content, PlayerState state, GrantService grants, ISaveScheduler? save)
        {
            _content = content;
            _state = state;
            _grants = grants;
            _save = save;
        }

        public ShopContent Catalog => _content.Shop;
        public GrantService Grants => _grants;

        /// <summary>The shop opens after onboarding (doc 06 UI): enough animals in the refuge.</summary>
        public bool ShopUnlocked => _content.Shop.Offers.Count > 0 && _state.Collection.Count >= _content.Shop.UnlockMinAnimals;

        public long Gems => _state.Wallet.Gems;

        public OfferAvailability Check(OfferDefinition offer, long nowUnix)
        {
            if (offer.StartUnixSeconds > 0 && nowUnix < offer.StartUnixSeconds)
                return OfferAvailability.OutOfWindow;
            if (offer.HasEndDate && nowUnix >= offer.EndUnixSeconds)
                return OfferAvailability.OutOfWindow;
            var record = _state.Shop.Get(offer.Id);
            if (offer.PurchaseLimit > 0 && record.Purchases >= offer.PurchaseLimit)
                return OfferAvailability.Owned;
            if (offer.Product.IsValid && _content.Shop.Products.TryGet(offer.Product, out var product)
                && product.Kind == ProductKind.NonConsumable && _grants.OwnsAll(product))
                return OfferAvailability.Owned;
            if (offer.IsGemPriced && AllOwned(offer.GemCostEntitlements))
                return OfferAvailability.Owned;
            if (_state.Collection.Count < offer.MinAnimals || _state.Collection.DiscoveredSpecies.Count < offer.MinDiscoveredSpecies)
                return OfferAvailability.NotEligible;
            bool payer = _state.Shop.PaidPurchases > 0;
            if ((offer.Segment == OfferSegment.NonPayer && payer) || (offer.Segment == OfferSegment.Payer && !payer))
                return OfferAvailability.NotEligible;
            return OfferAvailability.Available;
        }

        /// <summary>
        /// Offers of a placement in catalog order (priority first). Owned one-time offers are included when asked
        /// (the shop can show them as owned); hidden ones never are. Returns the count written to the buffer.
        /// </summary>
        public int GetOffers(OfferPlacement placement, long nowUnix, OfferDefinition[] buffer, bool includeOwned)
        {
            int count = 0;
            var offers = _content.Shop.Offers;
            for (int i = 0; i < offers.Count && count < buffer.Length; i++)
            {
                var offer = offers[i];
                if (offer.Placement != placement)
                    continue;
                var availability = Check(offer, nowUnix);
                if (availability == OfferAvailability.Available || (includeOwned && availability == OfferAvailability.Owned))
                    buffer[count++] = offer;
            }
            return count;
        }

        /// <summary>
        /// A moment offer to show now, if any: available and not shown within its cooldown. Doc 09: only after a
        /// positive moment (the caller decides when), never twice in a row.
        /// </summary>
        public bool TryGetMomentOffer(long nowUnix, out OfferDefinition offer)
        {
            var offers = _content.Shop.Offers;
            for (int i = 0; i < offers.Count; i++)
            {
                var candidate = offers[i];
                if (candidate.Placement != OfferPlacement.Moment || Check(candidate, nowUnix) != OfferAvailability.Available)
                    continue;
                var record = _state.Shop.Get(candidate.Id);
                if (record.Impressions > 0 && nowUnix < record.LastShownUnix + candidate.CooldownSeconds)
                    continue;
                offer = candidate;
                return true;
            }
            offer = null!;
            return false;
        }

        public void RecordShown(ContentId offer, long nowUnix)
        {
            var record = _state.Shop.Get(offer);
            record.Impressions++;
            record.LastShownUnix = nowUnix;
            _state.Shop.Offers[offer] = record;
            _save?.RequestSave(SaveUrgency.Normal);
        }

        /// <summary>Counts a completed purchase against the offer limit (store purchases and Gem purchases).</summary>
        public void RecordPurchase(ContentId offer)
        {
            var record = _state.Shop.Get(offer);
            record.Purchases++;
            _state.Shop.Offers[offer] = record;
            _save?.RequestSave(SaveUrgency.Priority);
        }

        public GemPurchaseStatus TryBuyWithGems(ContentId offerId, long nowUnix, out GrantResult result)
        {
            result = default;
            if (!_content.Shop.Offers.TryGet(offerId, out var offer))
                return GemPurchaseStatus.NotAvailable;
            if (!offer.IsGemPriced)
                return GemPurchaseStatus.NotGemPriced;
            if (Check(offer, nowUnix) != OfferAvailability.Available)
                return GemPurchaseStatus.NotAvailable;
            if (_state.Wallet.Gems < offer.GemCost)
                return GemPurchaseStatus.NotEnoughGems;
            _state.Wallet.Gems -= offer.GemCost;
            RecordPurchase(offer.Id);
            result = _grants.GrantItems(offer.Id, offer.GemCostEntitlements);
            return GemPurchaseStatus.Purchased;
        }

        private bool AllOwned(ContentId[] items)
        {
            if (items.Length == 0)
                return false;
            foreach (var item in items)
            {
                if (!_grants.Owns(item))
                    return false;
            }
            return true;
        }
    }
}
