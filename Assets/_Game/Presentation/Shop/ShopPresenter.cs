using System;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Feedback;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Shop presenter (T28): turns offers, grants and store prices into cards for PF_UI_Shop and PF_UI_OfferModal,
    /// and turns their intents (offer ids) into StoreService calls. Screens only see <see cref="IShopModel"/> and
    /// <see cref="IShopActions"/>: no store provider, product or SKU reaches the UI. Cards are rebuilt on events
    /// (open, price arrival, purchase), never per frame.
    /// </summary>
    public sealed class ShopPresenter : IShopModel, IShopActions, IDisposable
    {
        private const float ModalCloseDelay = 1.4f;

        private readonly StoreService _store;
        private readonly OfferService _offers;
        private readonly ShopContent _catalog;
        private readonly ILocalization _text;
        private readonly IShopView _shopView;
        private readonly IOfferModalView _offerView;
        private readonly IAudioService _audio;
        private readonly IHapticService _haptics;
        private readonly IAnalytics _analytics;
        private readonly Action<bool> _setWorldVisible;
        private readonly CancellationToken _lifetime;
        private readonly Action<PurchaseOutcome> _onFinished;
        private readonly OfferDefinition[] _buffer = new OfferDefinition[16];
        private readonly ShopCardModel[] _featured = new ShopCardModel[4];
        private readonly ShopCardModel[] _gemPacks = new ShopCardModel[12];
        private readonly double _baseGemsPerEuro;
        private int _featuredCount;
        private int _gemCount;
        private bool _wasReady;
        private bool _restoring;
        private ContentId _busyOffer;
        private ContentId _modalOffer;
        private float _modalCloseIn = -1f;

        public ShopPresenter(StoreService store, ILocalization text, IShopView shopView, IOfferModalView offerView, IAudioService audio,
            IHapticService haptics, Action<bool> setWorldVisible, CancellationToken lifetime, IAnalytics analytics = null)
        {
            _store = store;
            _offers = store.Offers;
            _catalog = _offers.Catalog;
            _text = text;
            _shopView = shopView;
            _offerView = offerView;
            _audio = audio;
            _haptics = haptics;
            _analytics = analytics ?? NullAnalytics.Instance;
            _setWorldVisible = setWorldVisible;
            _lifetime = lifetime;
            _baseGemsPerEuro = SmallestPackRate();
            _onFinished = OnPurchaseFinished;
            _store.PurchaseFinished += _onFinished;
        }

        public bool Unlocked => _offers.ShopUnlocked;
        public bool ShopOpen => _shopView != null && _shopView.IsOpen;
        public bool OfferOpen => _offerView != null && _offerView.IsOpen;

        // ---------------- IShopModel ----------------

        public long Gems => _offers.Gems;
        public bool Busy => _busyOffer.IsValid || _restoring;
        public ContentId BusyOffer => _busyOffer;
        public int FeaturedCount => _featuredCount;
        public ShopCardModel GetFeatured(int index) => _featured[index];
        public int GemPackCount => _gemCount;
        public ShopCardModel GetGemPack(int index) => _gemPacks[index];

        // ---------------- Entry points ----------------

        /// <summary>Scene entry: both screens build their layout and text once, while the loading screen is up.</summary>
        public void Prewarm()
        {
            Rebuild();
            _shopView?.Prewarm(this, _text, this);
            if (_offerView == null)
                return;
            var offers = _catalog.Offers;
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Placement == OfferPlacement.Moment)
                {
                    _offerView.Prewarm(BuildCard(offers[i], _store.NowUnix), this, _text, this);
                    return;
                }
            }
        }

        public void OpenShop()
        {
            if (_shopView == null || !Unlocked || ShopOpen)
                return;
            Rebuild();
            _shopView.Open(this, _text, this);
            _audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _setWorldVisible(false);
            _analytics.ScreenView(ScreenId.Shop);
            for (int i = 0; i < _featuredCount; i++)
                _analytics.OfferImpression(_featured[i].Offer, _featured[i].Placement);
            for (int i = 0; i < _gemCount; i++)
                _analytics.OfferImpression(_gemPacks[i].Offer, _gemPacks[i].Placement);
        }

        /// <summary>
        /// After a positive moment (a hatch reveal): the first moment offer whose cooldown is over, once its price
        /// is known. Returns true when the modal opened.
        /// </summary>
        public bool TryShowMomentOffer()
        {
            if (_offerView == null || !Unlocked || !_store.IsReady || OfferOpen || ShopOpen || Busy)
                return false;
            long now = _store.NowUnix;
            if (!_offers.TryGetMomentOffer(now, out var offer))
                return false;
            var card = BuildCard(offer, now);
            if (card.Price == null && card.GemCost == 0)
                return false;
            _offers.RecordShown(offer.Id, now);
            _modalOffer = offer.Id;
            _offerView.Open(card, this, _text, this);
            _audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _analytics.ScreenView(ScreenId.OfferModal);
            _analytics.OfferImpression(offer.Id, offer.Placement);
            return true;
        }

        /// <summary>T47: whether a featured or moment offer grants this item (the Meadow Pack grants the crown and the hamster).</summary>
        public bool HasOfferGranting(ContentId item) => FindOfferGranting(item, out _);

        /// <summary>
        /// T47: the pack's offer modal opened from the outfit picker or a Codex silhouette: the offer whose product grants this
        /// item, featured first. Opens even while owned (the modal then says « Acheté »). False when the shop is locked or none.
        /// </summary>
        public bool TryOpenOfferGranting(ContentId item)
        {
            if (_offerView == null || !Unlocked || OfferOpen || ShopOpen || !FindOfferGranting(item, out var offer))
                return false;
            _modalCloseIn = -1f;
            _modalOffer = offer.Id;
            _offerView.Open(BuildCard(offer, _store.NowUnix), this, _text, this);
            _audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _analytics.ScreenView(ScreenId.OfferModal);
            _analytics.OfferImpression(offer.Id, offer.Placement);
            return true;
        }

        private bool FindOfferGranting(ContentId item, out OfferDefinition found)
        {
            found = null;
            var offers = _catalog.Offers;
            for (int pass = 0; pass < 2 && found == null; pass++)
            {
                for (int i = 0; i < offers.Count; i++)
                {
                    var offer = offers[i];
                    var placement = pass == 0 ? OfferPlacement.ShopFeatured : OfferPlacement.Moment;
                    if (offer.Placement != placement || !offer.Product.IsValid || !_catalog.Products.TryGet(offer.Product, out var product))
                        continue;
                    if (System.Array.IndexOf(product.Entitlements, item) >= 0)
                    {
                        found = offer;
                        break;
                    }
                }
            }
            return found != null;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (_store.IsReady != _wasReady)
            {
                // Prices just arrived (or the store dropped): price buttons change state.
                _wasReady = _store.IsReady;
                RefreshViews();
            }
            if (_modalCloseIn >= 0f)
            {
                _modalCloseIn -= unscaledDeltaTime;
                if (_modalCloseIn < 0f && OfferOpen)
                    CloseOffer();
            }
            _shopView?.Tick(unscaledDeltaTime);
            _offerView?.Tick(unscaledDeltaTime);
        }

        // ---------------- IShopActions ----------------

        public void Buy(ContentId offer)
        {
            if (Busy)
                return;
            _busyOffer = offer;
            _analytics.OfferClick(offer, _catalog.Offers.TryGet(offer, out var definition) ? definition.Placement : default);
            _audio.Play(AudioEventId.UiTap, Vector3.zero, default);
            _haptics.Play(HapticEventId.TapLight);
            RefreshViews();
            _ = BuyAsync(offer);
        }

        public void Restore()
        {
            if (Busy)
                return;
            _restoring = true;
            RefreshViews();
            _ = RestoreAsync();
        }

        public void CloseShop()
        {
            if (!ShopOpen)
                return;
            _shopView.Close();
            _audio.Play(AudioEventId.PanelClose, Vector3.zero, default);
            _setWorldVisible(true);
        }

        public void CloseOffer()
        {
            _modalCloseIn = -1f;
            _modalOffer = ContentId.None;
            if (OfferOpen)
            {
                _offerView.Close();
                _audio.Play(AudioEventId.UiBack, Vector3.zero, default);
            }
        }

        // ---------------- Flow ----------------

        private async Task BuyAsync(ContentId offer)
        {
            PurchaseOutcome outcome;
            try
            {
                outcome = await _store.PurchaseAsync(offer, _lifetime);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                GameLog.Warning(LogCategory.Network, "Purchase flow failed: " + ex.Message);
                outcome = new PurchaseOutcome(PurchaseFlowStatus.Failed, offer);
            }
            finally
            {
                _busyOffer = ContentId.None;
            }
            Present(outcome);
        }

        private async Task RestoreAsync()
        {
            int restored = 0;
            bool failed = false;
            try
            {
                restored = await _store.RestoreAsync(_lifetime);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                GameLog.Warning(LogCategory.Network, "Restore failed: " + ex.Message);
                failed = true;
            }
            finally
            {
                _restoring = false;
            }
            RefreshViews();
            Message(failed ? ShopMessage.Failed : restored > 0 ? ShopMessage.Restored : ShopMessage.NothingToRestore, restored);
        }

        /// <summary>Late grants (approved pending purchase, interrupted purchase delivered at start).</summary>
        private void OnPurchaseFinished(PurchaseOutcome outcome)
        {
            // In-flight purchases and restores are presented by their own flow.
            if (_restoring || (_busyOffer.IsValid && outcome.Offer == _busyOffer))
                return;
            if (outcome.Status == PurchaseFlowStatus.Granted && outcome.Grant.IsNewGrant)
                Present(outcome);
        }

        private void Present(PurchaseOutcome outcome)
        {
            RefreshViews();
            switch (outcome.Status)
            {
                case PurchaseFlowStatus.Granted:
                    _audio.Play(AudioEventId.PurchaseComplete, Vector3.zero, default);
                    _haptics.Play(HapticEventId.PurchaseSuccess);
                    Message(ShopMessage.Thanks, outcome.Grant.Gems);
                    if (OfferOpen && outcome.Offer == _modalOffer)
                        _modalCloseIn = ModalCloseDelay;
                    break;
                case PurchaseFlowStatus.Failed:
                    _audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                    _haptics.Play(HapticEventId.ErrorSoft);
                    Message(ShopMessage.Failed, 0);
                    break;
                case PurchaseFlowStatus.Pending:
                    _audio.Play(AudioEventId.PurchasePending, Vector3.zero, default);
                    Message(ShopMessage.Pending, 0);
                    break;
                case PurchaseFlowStatus.StoreUnavailable:
                    Message(ShopMessage.Unavailable, 0);
                    break;
                case PurchaseFlowStatus.NotEnoughGems:
                    Message(ShopMessage.NotEnoughGems, 0);
                    break;
            }
        }

        private void Message(ShopMessage message, long value)
        {
            if (OfferOpen)
                _offerView.ShowMessage(message, value);
            else if (ShopOpen)
                _shopView.ShowMessage(message, value);
        }

        private void RefreshViews()
        {
            Rebuild();
            if (ShopOpen)
                _shopView.Refresh();
            if (OfferOpen && _catalog.Offers.TryGet(_modalOffer, out var modal))
                _offerView.Refresh(BuildCard(modal, _store.NowUnix));
        }

        // ---------------- Cards ----------------

        private void Rebuild()
        {
            long now = _store.NowUnix;
            int count = _offers.GetOffers(OfferPlacement.ShopFeatured, now, _buffer, includeOwned: true);
            _featuredCount = Math.Min(count, _featured.Length);
            for (int i = 0; i < _featuredCount; i++)
                _featured[i] = BuildCard(_buffer[i], now);
            count = _offers.GetOffers(OfferPlacement.ShopGems, now, _buffer, includeOwned: false);
            _gemCount = Math.Min(count, _gemPacks.Length);
            for (int i = 0; i < _gemCount; i++)
                _gemPacks[i] = BuildCard(_buffer[i], now);
        }

        private ShopCardModel BuildCard(OfferDefinition offer, long now)
        {
            var card = new ShopCardModel
            {
                Offer = offer.Id,
                Placement = offer.Placement,
                TitleKey = offer.DisplayKey,
                ArtKey = offer.ArtKey,
                Badge = offer.Badge,
                GemCost = offer.GemCost,
                OneTime = offer.PurchaseLimit == 1,
                Owned = _offers.Check(offer, now) == OfferAvailability.Owned,
                ItemKey = string.Empty,
                ItemIconKey = string.Empty,
            };
            ContentId[] items = offer.GemCostEntitlements;
            if (offer.Product.IsValid && _catalog.Products.TryGet(offer.Product, out var product))
            {
                card.Gems = product.Gems;
                items = product.Entitlements;
                card.Price = _store.TryGetPrice(product.Id, out var price) ? price : null;
                card.BonusPercent = Bonus(product);
            }
            if (items.Length > 0 && _catalog.Entitlements.TryGet(items[0], out var item))
            {
                card.ItemKey = item.DisplayKey;
                card.ItemIconKey = item.IconKey;
            }
            FillPack(ref card, offer, items);
            return card;
        }

        /// <summary>
        /// T47 pack card (a store product granting a companion, « Pack de la Prairie »): the companion is the hero art, the frozen
        /// body and « Offre unique » keys, the restore note under the button, and a row of what it holds in order: the companion,
        /// the Gems, then the other items (the burrow, the crown). No struck-through price, no percentage, no countdown.
        /// </summary>
        private void FillPack(ref ShopCardModel card, OfferDefinition offer, ContentId[] items)
        {
            if (!offer.Product.IsValid || items.Length == 0 || !_catalog.Entitlements.TryGet(items[0], out var first)
                || first.Kind != EntitlementKind.Companion)
                return;
            card.ArtKey = first.IconKey;
            card.BonusPercent = 0;
            string title = offer.DisplayKey ?? string.Empty;
            card.BodyKey = title.EndsWith(".title") ? title.Substring(0, title.Length - ".title".Length) + ".body" : string.Empty;
            card.OneTimeKey = card.OneTime ? PackOneTimeKey : string.Empty;
            card.RestoreNote = true;
            card.ContentCount = 0;
            // A companion's full name would take three lines under its icon: the row uses its short name (species.x.short).
            string firstLabel = first.Kind == EntitlementKind.Companion && first.DisplayKey.EndsWith(".name", StringComparison.Ordinal)
                ? first.DisplayKey.Substring(0, first.DisplayKey.Length - ".name".Length) + ".short"
                : first.DisplayKey;
            card.AddContent(new ShopContentItem { IconKey = first.IconKey, LabelKey = firstLabel });
            if (card.Gems > 0)
                card.AddContent(new ShopContentItem { IconKey = GemsIconKey, Amount = card.Gems });
            for (int i = 1; i < items.Length; i++)
                if (_catalog.Entitlements.TryGet(items[i], out var other))
                    card.AddContent(new ShopContentItem { IconKey = other.IconKey, LabelKey = other.DisplayKey });
        }

        public const string PackOneTimeKey = "shop.pack.one_time";
        public const string GemsIconKey = "ICO_Currency_Gems";

        /// <summary>
        /// Extra Gems compared to the smallest pack, priced per started euro (0,99 counts as 1 euro): 550 Gems for
        /// 4,99 against 100 for 0,99 shows +10%. Rounded down, never overstated; under 5% shows nothing.
        /// </summary>
        private int Bonus(ProductDefinition product)
        {
            if (_baseGemsPerEuro <= 0.0 || product.Gems <= 0 || product.Entitlements.Length > 0)
                return 0;
            double euros = Math.Ceiling(product.ReferencePriceCents / 100.0);
            double ratio = product.Gems / (euros * _baseGemsPerEuro);
            int percent = (int)Math.Floor((ratio - 1.0) * 100.0 + 1e-6);
            return percent >= 5 ? percent : 0;
        }

        private double SmallestPackRate()
        {
            ProductDefinition smallest = null;
            var offers = _catalog.Offers;
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Placement != OfferPlacement.ShopGems || !_catalog.Products.TryGet(offers[i].Product, out var product))
                    continue;
                if (product.Entitlements.Length == 0 && product.Gems > 0 && (smallest == null || product.ReferencePriceCents < smallest.ReferencePriceCents))
                    smallest = product;
            }
            return smallest == null ? 0.0 : smallest.Gems / Math.Ceiling(smallest.ReferencePriceCents / 100.0);
        }

        public void Dispose() => _store.PurchaseFinished -= _onFinished;
    }
}
