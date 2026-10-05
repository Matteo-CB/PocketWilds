#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Content;

namespace PocketWilds.Services.Store
{
    /// <summary>
    /// T28 sandbox store: fake Gems, no money. Prices come from the catalog's reference prices, formatted like a
    /// real store would (locale currency). Behaves like a platform store where it matters for the game: latency,
    /// cancel, failure, pending approval, one-time products restorable, transactions acknowledged after the grant.
    /// Compiled only in the editor and development builds (assembly define constraint): a release build can never
    /// grant free Gems.
    /// </summary>
    public sealed class SandboxStoreProvider : IStoreProvider
    {
        private readonly CultureInfo _culture;
        private readonly int _latencyMs;
        private readonly Dictionary<string, StoreProductRequest> _products = new Dictionary<string, StoreProductRequest>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _ownedOneTime = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pending = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _finished = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<StorePurchaseStatus> _scripted = new Queue<StorePurchaseStatus>();
        private int _transactions;

        /// <param name="languageCode">Player language: decides the price format ("fr" gives "1,99 €").</param>
        /// <param name="latencyMs">Simulated store round trip. 0 completes synchronously (tests).</param>
        public SandboxStoreProvider(string languageCode, int latencyMs = 450)
        {
            _culture = languageCode == "fr" ? CultureInfo.GetCultureInfo("fr-FR") : CultureInfo.GetCultureInfo("en-IE");
            _latencyMs = latencyMs;
        }

        public string Name => "Sandbox";

        /// <summary>Tests: the next purchases end with these statuses (then Success again).</summary>
        public void ScriptNext(StorePurchaseStatus status) => _scripted.Enqueue(status);

        public bool FailInitialize { get; set; }

        /// <summary>Transactions acknowledged by the game (consumed or confirmed).</summary>
        public IReadOnlyCollection<string> Finished => _finished;

        public event Action<StorePurchaseResult>? TransactionUpdated;

        public async Task<IReadOnlyDictionary<string, StoreProductInfo>> InitializeAsync(IReadOnlyList<StoreProductRequest> products, CancellationToken token)
        {
            await Latency(token);
            if (FailInitialize)
                throw new InvalidOperationException("sandbox store offline");
            var infos = new Dictionary<string, StoreProductInfo>(products.Count, StringComparer.Ordinal);
            foreach (var product in products)
            {
                _products[product.Sku] = product;
                decimal price = product.ReferencePriceCents / 100m;
                // Prices never break across lines, and narrow no-break spaces (some cultures) are missing from game fonts.
                string label = price.ToString("C", _culture).Replace('\u202F', '\u00A0').Replace(' ', '\u00A0');
                infos[product.Sku] = new StoreProductInfo(product.Sku, label, "EUR", price);
            }
            return infos;
        }

        public async Task<StorePurchaseResult> PurchaseAsync(string sku, CancellationToken token)
        {
            await Latency(token);
            if (!_products.TryGetValue(sku, out var product))
                return new StorePurchaseResult(StorePurchaseStatus.Failed, sku, string.Empty, "unknown product");
            var status = _scripted.Count > 0 ? _scripted.Dequeue() : StorePurchaseStatus.Success;
            if (status == StorePurchaseStatus.Cancelled || status == StorePurchaseStatus.Failed)
                return new StorePurchaseResult(status, sku, string.Empty, status == StorePurchaseStatus.Failed ? "sandbox failure" : string.Empty);
            if (product.Kind == ProductKind.NonConsumable && _ownedOneTime.TryGetValue(sku, out var original))
                return new StorePurchaseResult(StorePurchaseStatus.Success, sku, original);

            string transaction = "sandbox-" + (++_transactions).ToString("D4", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            if (status == StorePurchaseStatus.Pending)
            {
                _pending[transaction] = sku;
                return new StorePurchaseResult(StorePurchaseStatus.Pending, sku, transaction);
            }
            if (product.Kind == ProductKind.NonConsumable)
                _ownedOneTime[sku] = transaction;
            return new StorePurchaseResult(StorePurchaseStatus.Success, sku, transaction);
        }

        /// <summary>Tests: the approval of a pending purchase arrives (as it would minutes later on a phone).</summary>
        public void ApprovePending()
        {
            foreach (var pair in new List<KeyValuePair<string, string>>(_pending))
            {
                _pending.Remove(pair.Key);
                if (_products.TryGetValue(pair.Value, out var product) && product.Kind == ProductKind.NonConsumable)
                    _ownedOneTime[pair.Value] = pair.Key;
                TransactionUpdated?.Invoke(new StorePurchaseResult(StorePurchaseStatus.Success, pair.Value, pair.Key));
            }
        }

        /// <summary>Tests: the store delivers a transaction again (retry after a crash before acknowledgement).</summary>
        public void Redeliver(string sku, string transactionId) =>
            TransactionUpdated?.Invoke(new StorePurchaseResult(StorePurchaseStatus.Success, sku, transactionId));

        public async Task<IReadOnlyList<StorePurchaseResult>> RestoreAsync(CancellationToken token)
        {
            await Latency(token);
            var owned = new List<StorePurchaseResult>(_ownedOneTime.Count);
            foreach (var pair in _ownedOneTime)
                owned.Add(new StorePurchaseResult(StorePurchaseStatus.Success, pair.Key, pair.Value));
            return owned;
        }

        public void FinishTransaction(string transactionId) => _finished.Add(transactionId);

        private Task Latency(CancellationToken token) => _latencyMs > 0 ? Task.Delay(_latencyMs, token) : Task.CompletedTask;
    }
}
