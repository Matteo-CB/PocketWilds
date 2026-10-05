#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public enum GrantStatus : byte
    {
        Granted = 0,

        /// <summary>The store sent the same transaction again (retry, restore, crash recovery): nothing added.</summary>
        AlreadyGranted = 1,

        UnknownProduct = 2,
    }

    /// <summary>What one purchase gave. Immutable, handed to presentation and analytics.</summary>
    public readonly struct GrantResult
    {
        public readonly GrantStatus Status;
        public readonly ContentId Product;
        public readonly ContentId Offer;
        public readonly long Gems;
        public readonly int NewItems;
        public readonly string TransactionId;

        public GrantResult(GrantStatus status, ContentId product, ContentId offer, long gems, int newItems, string transactionId)
        {
            Status = status;
            Product = product;
            Offer = offer;
            Gems = gems;
            NewItems = newItems;
            TransactionId = transactionId;
        }

        public bool IsNewGrant => Status == GrantStatus.Granted;
    }

    /// <summary>
    /// The only writer of purchased Gems and items (Codebase Blueprint: the gameplay listens to an idempotent
    /// GrantResult). A store transaction id is granted once, whatever the number of callbacks, restores or
    /// restarts. Every grant asks for a priority save.
    /// </summary>
    public sealed class GrantService
    {
        private readonly ContentRegistry _content;
        private readonly PlayerState _state;
        private readonly ISaveScheduler? _save;

        public GrantService(ContentRegistry content, PlayerState state, ISaveScheduler? save)
        {
            _content = content;
            _state = state;
            _save = save;
        }

        /// <summary>Raised for every new grant (never for a repeated transaction).</summary>
        public event Action<GrantResult>? Granted;

        public bool Owns(ContentId item) => _state.Entitlements.Owned.Contains(item);

        /// <summary>True when every item a product grants is already owned (one-time products).</summary>
        public bool OwnsAll(ProductDefinition product)
        {
            if (product.Entitlements.Length == 0)
                return false;
            foreach (var item in product.Entitlements)
            {
                if (!Owns(item))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Grants a store transaction once. A restore (new device, reinstall) only gives back the permanent items:
        /// the Gems of a bundle were already spent on the first device.
        /// </summary>
        public GrantResult GrantPurchase(string transactionId, ContentId productId, ContentId offerId, bool restore = false)
        {
            if (string.IsNullOrEmpty(transactionId))
                throw new ArgumentException("A store grant needs its transaction id.", nameof(transactionId));
            if (_state.Entitlements.ProcessedTransactions.Contains(transactionId))
                return new GrantResult(GrantStatus.AlreadyGranted, productId, offerId, 0, 0, transactionId);
            if (!_content.Shop.Products.TryGet(productId, out var product))
                return new GrantResult(GrantStatus.UnknownProduct, productId, offerId, 0, 0, transactionId);

            _state.Entitlements.ProcessedTransactions.Add(transactionId);
            long gems = restore ? 0 : product.Gems;
            _state.Wallet.Gems += gems;
            int newItems = AddItems(product.Entitlements);
            if (!restore)
                _state.Shop.PaidPurchases++;
            var result = new GrantResult(GrantStatus.Granted, productId, offerId, gems, newItems, transactionId);
            _save?.RequestSave(SaveUrgency.Priority);
            Granted?.Invoke(result);
            return result;
        }

        /// <summary>Items bought with Gems (the Gems were spent by the caller in the same step).</summary>
        internal GrantResult GrantItems(ContentId offerId, ContentId[] items)
        {
            int newItems = AddItems(items);
            var result = new GrantResult(GrantStatus.Granted, ContentId.None, offerId, 0, newItems, string.Empty);
            _save?.RequestSave(SaveUrgency.Priority);
            Granted?.Invoke(result);
            return result;
        }

        private int AddItems(ContentId[] items)
        {
            int added = 0;
            foreach (var item in items)
            {
                if (_state.Entitlements.Owned.Add(item))
                    added++;
            }
            return added;
        }
    }
}
