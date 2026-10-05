#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Content;

namespace PocketWilds.Services.Store
{
    /// <summary>A product the game asks the platform store about.</summary>
    public readonly struct StoreProductRequest
    {
        public readonly string Sku;
        public readonly ProductKind Kind;

        /// <summary>Baseline price (doc 09). Real stores ignore it; the sandbox store prices from it.</summary>
        public readonly int ReferencePriceCents;

        public StoreProductRequest(string sku, ProductKind kind, int referencePriceCents)
        {
            Sku = sku;
            Kind = kind;
            ReferencePriceCents = referencePriceCents;
        }
    }

    /// <summary>What the store says about a product: its localized price, ready to display.</summary>
    public sealed class StoreProductInfo
    {
        public StoreProductInfo(string sku, string localizedPrice, string isoCurrency, decimal price)
        {
            Sku = sku;
            LocalizedPrice = localizedPrice;
            IsoCurrency = isoCurrency;
            Price = price;
        }

        public string Sku { get; }
        public string LocalizedPrice { get; }
        public string IsoCurrency { get; }
        public decimal Price { get; }
    }

    public enum StorePurchaseStatus : byte
    {
        Success = 0,
        Cancelled = 1,
        Failed = 2,

        /// <summary>Waiting for an approval (parental, payment method): arrives later through TransactionUpdated.</summary>
        Pending = 3,
    }

    public readonly struct StorePurchaseResult
    {
        public readonly StorePurchaseStatus Status;
        public readonly string Sku;
        public readonly string TransactionId;
        public readonly string Error;

        public StorePurchaseResult(StorePurchaseStatus status, string sku, string transactionId, string error = "")
        {
            Status = status;
            Sku = sku;
            TransactionId = transactionId;
            Error = error;
        }
    }

    /// <summary>
    /// The platform store behind <see cref="StoreService"/> (Google Play, App Store, or the sandbox). Only the
    /// store service talks to it: no screen, button or gameplay code ever does (T28 criterion).
    /// </summary>
    public interface IStoreProvider
    {
        string Name { get; }

        /// <summary>Loads localized product info. Missing products are left out of the result.</summary>
        Task<IReadOnlyDictionary<string, StoreProductInfo>> InitializeAsync(IReadOnlyList<StoreProductRequest> products, CancellationToken token);

        Task<StorePurchaseResult> PurchaseAsync(string sku, CancellationToken token);

        /// <summary>One-time products the account owns (original transaction ids).</summary>
        Task<IReadOnlyList<StorePurchaseResult>> RestoreAsync(CancellationToken token);

        /// <summary>Acknowledges (consumes) a transaction once its grant is safely saved.</summary>
        void FinishTransaction(string transactionId);

        /// <summary>Transactions that complete outside a purchase call: approved pending ones, interrupted ones.</summary>
        event Action<StorePurchaseResult>? TransactionUpdated;
    }
}
