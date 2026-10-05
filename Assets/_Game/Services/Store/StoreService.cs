#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;

namespace PocketWilds.Services.Store
{
    public enum PurchaseFlowStatus : byte
    {
        Granted = 0,
        Cancelled = 1,
        Failed = 2,

        /// <summary>The store is waiting for an approval: the grant arrives later.</summary>
        Pending = 3,

        /// <summary>Not offered to this player now (limit reached, window closed, not eligible).</summary>
        NotAvailable = 4,
        NotEnoughGems = 5,

        /// <summary>No store connection or product unknown to the store.</summary>
        StoreUnavailable = 6,

        /// <summary>Another purchase is still running.</summary>
        Busy = 7,
    }

    public readonly struct PurchaseOutcome
    {
        public readonly PurchaseFlowStatus Status;
        public readonly ContentId Offer;
        public readonly GrantResult Grant;

        public PurchaseOutcome(PurchaseFlowStatus status, ContentId offer, GrantResult grant = default)
        {
            Status = status;
            Offer = offer;
            Grant = grant;
        }
    }

    /// <summary>
    /// Provider-independent store (Codebase Blueprint StoreService): catalog prices, Purchase(offer), Restore,
    /// late transactions. The only object that talks to an <see cref="IStoreProvider"/>. Grants go through the
    /// idempotent <see cref="GrantService"/>; a transaction is acknowledged to the store only once a save snapshot
    /// taken after its grant is on disk (background write, never a main-thread flush). A crash in between makes the
    /// store send it again; the save did not keep the grant, so it is granted then, once.
    /// </summary>
    public sealed class StoreService : IDisposable
    {
        private readonly ContentRegistry _content;
        private readonly OfferService _offers;
        private readonly IStoreProvider _provider;
        private readonly IGameClock _clock;
        private readonly ISaveWrites _saves;
        private readonly Action<long> _onWritten;
        private readonly List<(string transaction, long serialized)> _awaitingSave = new List<(string, long)>();
        private readonly Dictionary<string, ProductDefinition> _productBySku = new Dictionary<string, ProductDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<ContentId, string> _prices = new Dictionary<ContentId, string>();
        private readonly Action<StorePurchaseResult> _onTransaction;
        private bool _busy;

        public StoreService(ContentRegistry content, OfferService offers, IStoreProvider provider, IGameClock clock, ISaveWrites saves)
        {
            _content = content;
            _offers = offers;
            _provider = provider;
            _clock = clock;
            _saves = saves;
            _onWritten = OnSaveWritten;
            _saves.Written += _onWritten;
            var products = content.Shop.Products;
            for (int i = 0; i < products.Count; i++)
                _productBySku[products[i].StoreSku] = products[i];
            _onTransaction = OnTransactionUpdated;
            _provider.TransactionUpdated += _onTransaction;
        }

        public string ProviderName => _provider.Name;

        /// <summary>Prices are known: real-money offers can be shown with a price and bought.</summary>
        public bool IsReady { get; private set; }

        public bool IsBusy => _busy;

        public OfferService Offers => _offers;

        /// <summary>Every finished purchase, including late ones (pending approvals, interrupted purchases).</summary>
        public event Action<PurchaseOutcome>? PurchaseFinished;

        public long NowUnix => _clock.UtcNow.ToUnixTimeSeconds();

        public async Task InitializeAsync(CancellationToken token)
        {
            var requests = new List<StoreProductRequest>(_productBySku.Count);
            foreach (var product in _productBySku.Values)
                requests.Add(new StoreProductRequest(product.StoreSku, product.Kind, product.ReferencePriceCents));
            try
            {
                var infos = await _provider.InitializeAsync(requests, token);
                foreach (var info in infos.Values)
                {
                    if (_productBySku.TryGetValue(info.Sku, out var product))
                        _prices[product.Id] = info.LocalizedPrice;
                }
                IsReady = _prices.Count > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                IsReady = false;
                GameLog.Warning(LogCategory.Network, "Store " + _provider.Name + " unavailable: " + ex.Message);
            }
        }

        /// <summary>Localized price from the store (never from the catalog). False until the store answered.</summary>
        public bool TryGetPrice(ContentId product, out string localizedPrice) => _prices.TryGetValue(product, out localizedPrice!);

        public async Task<PurchaseOutcome> PurchaseAsync(ContentId offerId, CancellationToken token)
        {
            if (!_content.Shop.Offers.TryGet(offerId, out var offer))
                return Finish(new PurchaseOutcome(PurchaseFlowStatus.NotAvailable, offerId));
            if (_offers.Check(offer, NowUnix) != OfferAvailability.Available)
                return Finish(new PurchaseOutcome(PurchaseFlowStatus.NotAvailable, offerId));

            if (offer.IsGemPriced)
            {
                var status = _offers.TryBuyWithGems(offerId, NowUnix, out var gemGrant);
                return Finish(new PurchaseOutcome(status switch
                {
                    GemPurchaseStatus.Purchased => PurchaseFlowStatus.Granted,
                    GemPurchaseStatus.NotEnoughGems => PurchaseFlowStatus.NotEnoughGems,
                    _ => PurchaseFlowStatus.NotAvailable,
                }, offerId, gemGrant));
            }

            if (_busy)
                return new PurchaseOutcome(PurchaseFlowStatus.Busy, offerId);
            var product = _content.Shop.Products.Get(offer.Product);
            if (!IsReady || !_prices.ContainsKey(product.Id))
                return Finish(new PurchaseOutcome(PurchaseFlowStatus.StoreUnavailable, offerId));

            _busy = true;
            try
            {
                var result = await _provider.PurchaseAsync(product.StoreSku, token);
                switch (result.Status)
                {
                    case StorePurchaseStatus.Success:
                        return Finish(Grant(result, product, offer.Id, restore: false));
                    case StorePurchaseStatus.Pending:
                        return Finish(new PurchaseOutcome(PurchaseFlowStatus.Pending, offerId));
                    case StorePurchaseStatus.Cancelled:
                        return Finish(new PurchaseOutcome(PurchaseFlowStatus.Cancelled, offerId));
                    default:
                        GameLog.Warning(LogCategory.Network, "Purchase of " + product.StoreSku + " failed: " + result.Error);
                        return Finish(new PurchaseOutcome(PurchaseFlowStatus.Failed, offerId));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                GameLog.Warning(LogCategory.Network, "Purchase of " + product.StoreSku + " failed: " + ex.Message);
                return Finish(new PurchaseOutcome(PurchaseFlowStatus.Failed, offerId));
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Gives back one-time items the store account owns. Returns how many purchases were restored.</summary>
        public async Task<int> RestoreAsync(CancellationToken token)
        {
            var owned = await _provider.RestoreAsync(token);
            int restored = 0;
            foreach (var transaction in owned)
            {
                if (transaction.Status != StorePurchaseStatus.Success || !_productBySku.TryGetValue(transaction.Sku, out var product)
                    || product.Kind != ProductKind.NonConsumable)
                    continue;
                var outcome = Grant(transaction, product, OfferSelling(product.Id), restore: true);
                if (outcome.Grant.IsNewGrant)
                {
                    restored++;
                    PurchaseFinished?.Invoke(outcome);
                }
            }
            return restored;
        }

        private PurchaseOutcome Grant(in StorePurchaseResult transaction, ProductDefinition product, ContentId offerId, bool restore)
        {
            var grant = _offers.Grants.GrantPurchase(transaction.TransactionId, product.Id, offerId, restore);
            if (grant.IsNewGrant)
            {
                if (offerId.IsValid)
                    _offers.RecordPurchase(offerId);
                // Acknowledged once a snapshot that contains the grant is on disk.
                _awaitingSave.Add((transaction.TransactionId, _saves.SerializedCount));
                _saves.RequestSave(SaveUrgency.Priority);
            }
            else if (grant.Status == GrantStatus.AlreadyGranted && !IsAwaitingSave(transaction.TransactionId))
            {
                // Granted in an earlier, saved session: the store only needs the acknowledgement.
                _provider.FinishTransaction(transaction.TransactionId);
            }
            return new PurchaseOutcome(PurchaseFlowStatus.Granted, offerId, grant);
        }

        /// <summary>Transactions granted but not yet acknowledged (waiting for their save).</summary>
        public int AwaitingSave => _awaitingSave.Count;

        private bool IsAwaitingSave(string transaction)
        {
            for (int i = 0; i < _awaitingSave.Count; i++)
            {
                if (_awaitingSave[i].transaction == transaction)
                    return true;
            }
            return false;
        }

        private void OnSaveWritten(long snapshot)
        {
            for (int i = _awaitingSave.Count - 1; i >= 0; i--)
            {
                if (snapshot > _awaitingSave[i].serialized)
                {
                    _provider.FinishTransaction(_awaitingSave[i].transaction);
                    _awaitingSave.RemoveAt(i);
                }
            }
        }

        private void OnTransactionUpdated(StorePurchaseResult transaction)
        {
            if (transaction.Status != StorePurchaseStatus.Success || !_productBySku.TryGetValue(transaction.Sku, out var product))
                return;
            var outcome = Grant(transaction, product, OfferSelling(product.Id), restore: false);
            if (outcome.Grant.IsNewGrant)
                PurchaseFinished?.Invoke(outcome);
        }

        private ContentId OfferSelling(ContentId product)
        {
            var offers = _content.Shop.Offers;
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Product == product)
                    return offers[i].Id;
            }
            return ContentId.None;
        }

        private PurchaseOutcome Finish(PurchaseOutcome outcome)
        {
            PurchaseFinished?.Invoke(outcome);
            return outcome;
        }

        public void Dispose()
        {
            _provider.TransactionUpdated -= _onTransaction;
            _saves.Written -= _onWritten;
        }
    }
}
