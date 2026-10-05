#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PocketWilds.Services.Store
{
    /// <summary>
    /// Used when no real store is connected (release builds until the platform provider ships): the shop shows
    /// "unavailable" and never grants anything. The sandbox store only exists in the editor and development builds.
    /// </summary>
    public sealed class UnavailableStoreProvider : IStoreProvider
    {
        private static readonly IReadOnlyDictionary<string, StoreProductInfo> NoProducts = new Dictionary<string, StoreProductInfo>();
        private static readonly IReadOnlyList<StorePurchaseResult> NoPurchases = Array.Empty<StorePurchaseResult>();

        public string Name => "Unavailable";

        public Task<IReadOnlyDictionary<string, StoreProductInfo>> InitializeAsync(IReadOnlyList<StoreProductRequest> products, CancellationToken token) =>
            Task.FromResult(NoProducts);

        public Task<StorePurchaseResult> PurchaseAsync(string sku, CancellationToken token) =>
            Task.FromResult(new StorePurchaseResult(StorePurchaseStatus.Failed, sku, string.Empty, "store unavailable"));

        public Task<IReadOnlyList<StorePurchaseResult>> RestoreAsync(CancellationToken token) => Task.FromResult(NoPurchases);

        public void FinishTransaction(string transactionId)
        {
        }

        public event Action<StorePurchaseResult>? TransactionUpdated
        {
            add { }
            remove { }
        }
    }
}
