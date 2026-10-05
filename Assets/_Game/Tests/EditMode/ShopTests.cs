using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Shop;
using PocketWilds.Presentation;
using PocketWilds.Services.Feedback;
using PocketWilds.Services.Save;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T28 shop sandbox: data-driven catalog and its validation, idempotent grants, offer eligibility and slots,
    /// the store flow (cancel, failure, pending, redelivery, restore) through the sandbox provider, presenter cards,
    /// and the criterion: no link between UI and the store provider.
    /// </summary>
    public class ShopTests
    {
        private static readonly string[] GemOffers =
            { "offer.gems.tier01", "offer.gems.tier02", "offer.gems.tier03", "offer.gems.tier04", "offer.gems.tier05", "offer.gems.tier06" };

        private static ContentRegistry Shipped() =>
            AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset").CreateBuilder().Build();

        /// <summary>A player past onboarding: animals of two species, nothing bought.</summary>
        private static PlayerState Player(int animals = 6, int species = 2)
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            string[] ids = { "species.rabbit", "species.squirrel", "species.hedgehog" };
            for (int i = 0; i < animals; i++)
            {
                var id = ContentId.FromSource(ids[i % species]);
                state.Collection.Add(new AnimalInstance { Species = id, Seed = (uint)i + 1u });
                state.Collection.DiscoveredSpecies.Add(id);
            }
            return state;
        }

        /// <summary>Save writes on demand: <see cref="Write"/> puts the latest snapshot on disk.</summary>
        private sealed class FakeSaves : ISaveWrites
        {
            public long SerializedCount { get; private set; }
            public int Requests;
            public event Action<long> Written;
            public void RequestSave(SaveUrgency urgency) => Requests++;

            public void Write()
            {
                SerializedCount++;
                Written?.Invoke(SerializedCount);
            }

            /// <summary>A write that started before the change and finishes after it.</summary>
            public void WriteOlderSnapshot()
            {
                long older = SerializedCount;
                SerializedCount++;
                Written?.Invoke(older);
            }
        }

        private sealed class Rig : IDisposable
        {
            public readonly PlayerState State;
            public readonly GrantService Grants;
            public readonly OfferService Offers;
            public readonly SandboxStoreProvider Provider;
            public readonly StoreService Store;
            public readonly FakeGameClock Clock = new FakeGameClock(TestUtil.T0);
            public readonly FakeSaves Saves = new FakeSaves();

            public Rig(ContentRegistry content, PlayerState state, SandboxStoreProvider provider = null, string language = "en")
            {
                State = state;
                Grants = new GrantService(content, state, null);
                Offers = new OfferService(content, state, Grants, null);
                Provider = provider ?? new SandboxStoreProvider(language, 0);
                Store = new StoreService(content, Offers, Provider, Clock, Saves);
            }

            public void Init() => Store.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

            public PurchaseOutcome Buy(string offer) =>
                Store.PurchaseAsync(ContentId.FromSource(offer), CancellationToken.None).GetAwaiter().GetResult();

            public void Dispose() => Store.Dispose();
        }

        // ---------------- Catalog ----------------

        [Test]
        public void ShippedCatalog_FollowsTheDoc09Baseline()
        {
            var shop = Shipped().Shop;
            Assert.AreEqual(3, shop.UnlockMinAnimals);
            int[] prices = { 99, 499, 999, 1999, 4999, 9999 };
            long previousGemsPerCent = 0;
            for (int i = 0; i < GemOffers.Length; i++)
            {
                var offer = shop.Offers.Get(ContentId.FromSource(GemOffers[i]));
                Assert.AreEqual(OfferPlacement.ShopGems, offer.Placement);
                var product = shop.Products.Get(offer.Product);
                Assert.AreEqual(prices[i], product.ReferencePriceCents, "Doc 09 Gem pack price ladder.");
                Assert.AreEqual(ProductKind.Consumable, product.Kind);
                long gemsPerCent = product.Gems * 1000 / product.ReferencePriceCents;
                Assert.Greater(gemsPerCent, previousGemsPerCent, "The bonus grows with the pack.");
                previousGemsPerCent = gemsPerCent;
                Assert.AreEqual(i == GemOffers.Length - 1 ? OfferBadge.BestValue : OfferBadge.None, offer.Badge, "Best value is the best value.");
            }
            var starter = shop.Offers.Get(ContentId.FromSource(ShopPipeline.StarterOffer));
            var starterProduct = shop.Products.Get(starter.Product);
            Assert.AreEqual(ProductKind.NonConsumable, starterProduct.Kind);
            Assert.AreEqual(1, starter.PurchaseLimit);
            Assert.AreEqual(OfferSegment.NonPayer, starter.Segment);
            Assert.Greater(starter.MinAnimals, shop.UnlockMinAnimals - 1, "Never at the raw start (doc 09).");
            CollectionAssert.Contains(starterProduct.Entitlements, ContentId.FromSource(ShopPipeline.FlowerCrown));
            for (int i = 0; i < shop.Offers.Count; i++)
                Assert.IsFalse(shop.Offers[i].HasEndDate, "No fake timers: sandbox offers have no end date.");
        }

        [Test]
        public void ShippedCatalog_TextsAndArtExist()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var shop = Shipped().Shop;
            var keys = new List<string>();
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (!string.IsNullOrEmpty(shop.Offers[i].DisplayKey))
                    keys.Add(shop.Offers[i].DisplayKey);
                string art = shop.Offers[i].ArtKey;
                Assert.IsTrue(File.Exists($"Assets/_Game/Art/UI/Shop/{art}.png") || File.Exists($"Assets/_Game/Art/UI/Icons/{art}.png"), "Missing art " + art);
            }
            for (int i = 0; i < shop.Entitlements.Count; i++)
                keys.Add(shop.Entitlements[i].DisplayKey);
            keys.AddRange(PocketWilds.UI.UiTextKeys.All);
            foreach (var table in database.localization)
            {
                foreach (string key in keys)
                {
                    Assert.IsTrue(table.TryGet(key, out string value) && !string.IsNullOrEmpty(value), $"{table.languageCode}: missing {key}");
                    Assert.IsFalse(value.Contains("—"), "No em dash in player text: " + key);
                }
            }
        }

        private static OfferDefinition Offer(string id, ContentId product = default, long gemCost = 0, ContentId[] items = null, int limit = 0,
            long start = 0, long end = 0, OfferPlacement placement = OfferPlacement.ShopGems, int cooldown = 0, int minAnimals = 0,
            OfferSegment segment = OfferSegment.Any) =>
            new OfferDefinition(ContentId.FromSource(id), "offer.title", placement, 0, product, gemCost, items, limit, cooldown, start, end,
                minAnimals, 0, segment, OfferBadge.None, "ICO_GemPack_01", string.Empty);

        private static ContentRegistryBuilder Builder()
        {
            var builder = new ContentRegistryBuilder();
            builder.SetShopUnlock(1);
            builder.AddEntitlement(new EntitlementDefinition(ContentId.FromSource("cosmetic.test_hat"), EntitlementKind.Cosmetic, "hat.name", "ICO_Hat"), "cosmetic.test_hat");
            return builder;
        }

        [Test]
        public void Catalog_RejectsInvalidCommercialData()
        {
            var hat = ContentId.FromSource("cosmetic.test_hat");
            var gems = ContentId.FromSource("product.test.gems");
            var once = ContentId.FromSource("product.test.once");

            void Expect(Action<ContentRegistryBuilder> add, string because)
            {
                var builder = Builder();
                builder.AddProduct(new ProductDefinition(gems, "sku_gems", ProductKind.Consumable, 99, 100, null), "product.test.gems");
                builder.AddProduct(new ProductDefinition(once, "sku_once", ProductKind.NonConsumable, 199, 50, new[] { hat }), "product.test.once");
                add(builder);
                Assert.IsTrue(builder.HasErrors || Throws(builder), because);
            }

            Expect(b => b.AddOffer(Offer("offer.test.both", gems, gemCost: 10, items: new[] { hat }), "offer.test.both"), "store product and Gem price at once");
            Expect(b => b.AddOffer(Offer("offer.test.none"), "offer.test.none"), "sells nothing");
            Expect(b => b.AddOffer(Offer("offer.test.once", once, limit: 0), "offer.test.once"), "one-time product without limit 1");
            Expect(b => b.AddProduct(new ProductDefinition(ContentId.FromSource("product.test.dup"), "sku_gems", ProductKind.Consumable, 99, 10, null), "product.test.dup"), "duplicate SKU");
            Expect(b => b.AddProduct(new ProductDefinition(ContentId.FromSource("product.test.item"), "sku_item", ProductKind.Consumable, 99, 0, new[] { hat }), "product.test.item"), "items sold as consumable");
            Expect(b => b.AddProduct(new ProductDefinition(ContentId.FromSource("product.test.ghost"), "sku_ghost", ProductKind.NonConsumable, 99, 0,
                new[] { ContentId.FromSource("cosmetic.ghost") }), "product.test.ghost"), "unknown item");
            Expect(b => b.AddProduct(new ProductDefinition(ContentId.FromSource("product.test.free"), "sku_free", ProductKind.Consumable, 0, 10, null), "product.test.free"), "no price");
            Expect(b => b.AddOffer(Offer("offer.test.window", gems, start: 2000, end: 1000), "offer.test.window"), "ends before it starts");

            var valid = Builder();
            valid.AddProduct(new ProductDefinition(gems, "sku_gems", ProductKind.Consumable, 99, 100, null), "product.test.gems");
            valid.AddOffer(Offer("offer.test.ok", gems), "offer.test.ok");
            Assert.DoesNotThrow(() => valid.Build());
        }

        private static bool Throws(ContentRegistryBuilder builder)
        {
            try
            {
                builder.Build();
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        // ---------------- Grants and offers ----------------

        [Test]
        public void Grant_IsIdempotentPerTransaction_AndRestoreGivesItemsOnly()
        {
            var content = Shipped();
            var state = Player();
            var grants = new GrantService(content, state, null);
            var starter = ContentId.FromSource("product.starter.001");
            var first = grants.GrantPurchase("txn-1", starter, ContentId.None);
            var again = grants.GrantPurchase("txn-1", starter, ContentId.None);
            Assert.AreEqual(GrantStatus.Granted, first.Status);
            Assert.AreEqual(GrantStatus.AlreadyGranted, again.Status);
            Assert.AreEqual(ShopPipeline.StarterGems, state.Wallet.Gems);
            Assert.AreEqual(1, state.Shop.PaidPurchases);
            Assert.IsTrue(grants.Owns(ContentId.FromSource(ShopPipeline.FlowerCrown)));

            var newDevice = Player();
            var restored = new GrantService(content, newDevice, null).GrantPurchase("txn-1", starter, ContentId.None, restore: true);
            Assert.AreEqual(GrantStatus.Granted, restored.Status);
            Assert.AreEqual(0, newDevice.Wallet.Gems, "Restore gives back the permanent items, not the Gems already spent.");
            Assert.IsTrue(newDevice.Entitlements.Owned.Contains(ContentId.FromSource(ShopPipeline.FlowerCrown)));
            Assert.AreEqual(0, newDevice.Shop.PaidPurchases);
        }

        [Test]
        public void Offers_FollowOnboarding_Segment_AndLimits()
        {
            var content = Shipped();
            long now = TestUtil.T0.ToUnixTimeSeconds();
            var buffer = new OfferDefinition[8];

            var newcomer = Player(animals: 2, species: 1);
            var offers = new OfferService(content, newcomer, new GrantService(content, newcomer, null), null);
            Assert.IsFalse(offers.ShopUnlocked, "No shop before onboarding.");
            Assert.AreEqual(0, offers.GetOffers(OfferPlacement.ShopFeatured, now, buffer, includeOwned: true));

            var fourAnimals = Player(animals: 4, species: 2);
            offers = new OfferService(content, fourAnimals, new GrantService(content, fourAnimals, null), null);
            Assert.IsTrue(offers.ShopUnlocked);
            Assert.AreEqual(0, offers.GetOffers(OfferPlacement.ShopFeatured, now, buffer, includeOwned: true), "Starter Pack waits for 5 animals.");
            Assert.AreEqual(6, offers.GetOffers(OfferPlacement.ShopGems, now, buffer, includeOwned: false));
            for (int i = 0; i < GemOffers.Length; i++)
                Assert.AreEqual(ContentId.FromSource(GemOffers[i]), buffer[i].Id, "Gem packs from small to big.");

            var ready = Player();
            var grants = new GrantService(content, ready, null);
            offers = new OfferService(content, ready, grants, null);
            Assert.AreEqual(1, offers.GetOffers(OfferPlacement.ShopFeatured, now, buffer, includeOwned: true));
            Assert.AreEqual(ContentId.FromSource(ShopPipeline.StarterOffer), buffer[0].Id);

            grants.GrantPurchase("txn-starter", ContentId.FromSource("product.starter.001"), buffer[0].Id);
            offers.RecordPurchase(buffer[0].Id);
            Assert.AreEqual(OfferAvailability.Owned, offers.Check(buffer[0], now));
            Assert.AreEqual(1, offers.GetOffers(OfferPlacement.ShopFeatured, now, buffer, includeOwned: true), "Owned stays visible as owned.");
            Assert.AreEqual(0, offers.GetOffers(OfferPlacement.ShopFeatured, now, buffer, includeOwned: false));
            Assert.IsFalse(offers.TryGetMomentOffer(now, out _), "The moment offer sells the same one-time product: owned.");

            var payer = Player();
            payer.Shop.PaidPurchases = 1;
            offers = new OfferService(content, payer, new GrantService(content, payer, null), null);
            Assert.AreEqual(0, offers.GetOffers(OfferPlacement.ShopFeatured, now, buffer, includeOwned: true), "Starter Pack is for new players.");
        }

        [Test]
        public void MomentOffer_RespectsItsCooldown_AndWindowsAreReal()
        {
            var content = Shipped();
            var state = Player();
            var offers = new OfferService(content, state, new GrantService(content, state, null), null);
            long now = TestUtil.T0.ToUnixTimeSeconds();
            Assert.IsTrue(offers.TryGetMomentOffer(now, out var moment));
            offers.RecordShown(moment.Id, now);
            Assert.IsFalse(offers.TryGetMomentOffer(now + 3600, out _), "Not again an hour later.");
            Assert.IsFalse(offers.TryGetMomentOffer(now + moment.CooldownSeconds - 1, out _));
            Assert.IsTrue(offers.TryGetMomentOffer(now + moment.CooldownSeconds, out _), "Back after its cooldown.");

            var hat = ContentId.FromSource("cosmetic.test_hat");
            var builder = Builder();
            builder.AddProduct(new ProductDefinition(ContentId.FromSource("product.test.gems"), "sku_gems", ProductKind.Consumable, 99, 100, null), "product.test.gems");
            builder.AddOffer(Offer("offer.test.event", ContentId.FromSource("product.test.gems"), start: now + 100, end: now + 200), "offer.test.event");
            var windowed = builder.Build();
            var player = Player();
            var windowOffers = new OfferService(windowed, player, new GrantService(windowed, player, null), null);
            var offer = windowed.Shop.Offers.Get(ContentId.FromSource("offer.test.event"));
            Assert.AreEqual(OfferAvailability.OutOfWindow, windowOffers.Check(offer, now));
            Assert.AreEqual(OfferAvailability.Available, windowOffers.Check(offer, now + 150));
            Assert.AreEqual(OfferAvailability.OutOfWindow, windowOffers.Check(offer, now + 200));
        }

        // ---------------- Store flow ----------------

        [Test]
        public void Store_PricesComeFromTheStore_LocalizedByLanguage()
        {
            var content = Shipped();
            using (var fr = new Rig(content, Player(), language: "fr"))
            using (var en = new Rig(content, Player(), language: "en"))
            {
                var tier01 = ContentId.FromSource("product.gems.tier01");
                Assert.IsFalse(fr.Store.TryGetPrice(tier01, out _), "No price before the store answers.");
                fr.Init();
                en.Init();
                Assert.IsTrue(fr.Store.IsReady);
                Assert.IsTrue(fr.Store.TryGetPrice(tier01, out string frPrice));
                Assert.IsTrue(en.Store.TryGetPrice(tier01, out string enPrice));
                Assert.AreEqual("0,99 €", frPrice);
                Assert.AreEqual("€0.99", enPrice);
            }
        }

        [Test]
        public void Store_GrantsOnce_AndAcknowledgesOnlyOnceTheGrantIsOnDisk()
        {
            using (var rig = new Rig(Shipped(), Player()))
            {
                var unavailable = rig.Buy("offer.gems.tier01");
                Assert.AreEqual(PurchaseFlowStatus.StoreUnavailable, unavailable.Status, "Before the store answered.");
                rig.Init();

                var outcome = rig.Buy("offer.gems.tier01");
                Assert.AreEqual(PurchaseFlowStatus.Granted, outcome.Status);
                Assert.AreEqual(100, rig.State.Wallet.Gems);
                Assert.AreEqual(1, rig.State.Shop.PaidPurchases);
                Assert.Greater(rig.Saves.Requests, 0, "A priority save is requested, never written on the spot.");
                CollectionAssert.DoesNotContain(rig.Provider.Finished, outcome.Grant.TransactionId, "Not acknowledged before the save.");

                // The store sends it again before the save (retry): no double grant, still not acknowledged.
                rig.Provider.Redeliver("pw_gems_tier01", outcome.Grant.TransactionId);
                Assert.AreEqual(100, rig.State.Wallet.Gems);
                CollectionAssert.DoesNotContain(rig.Provider.Finished, outcome.Grant.TransactionId);

                // A snapshot taken before the grant finishes writing: not enough.
                rig.Saves.WriteOlderSnapshot();
                CollectionAssert.DoesNotContain(rig.Provider.Finished, outcome.Grant.TransactionId);
                rig.Saves.Write();
                CollectionAssert.Contains(rig.Provider.Finished, outcome.Grant.TransactionId, "Acknowledged once the grant is on disk.");
                Assert.AreEqual(0, rig.Store.AwaitingSave);

                // Delivered again after a restart (the saved state already has it): acknowledged at once, nothing added.
                rig.Provider.Redeliver("pw_gems_tier01", outcome.Grant.TransactionId);
                Assert.AreEqual(100, rig.State.Wallet.Gems);
            }
        }

        [Test]
        public void Store_CancelFailurePending_NeverGrantTwiceOrWrongly()
        {
            using (var rig = new Rig(Shipped(), Player()))
            {
                rig.Init();
                var late = new List<PurchaseOutcome>();
                rig.Store.PurchaseFinished += o => late.Add(o);

                rig.Provider.ScriptNext(StorePurchaseStatus.Cancelled);
                Assert.AreEqual(PurchaseFlowStatus.Cancelled, rig.Buy("offer.gems.tier03").Status);
                rig.Provider.ScriptNext(StorePurchaseStatus.Failed);
                Assert.AreEqual(PurchaseFlowStatus.Failed, rig.Buy("offer.gems.tier03").Status);
                Assert.AreEqual(0, rig.State.Wallet.Gems);
                Assert.AreEqual(0, rig.State.Shop.PaidPurchases);

                rig.Provider.ScriptNext(StorePurchaseStatus.Pending);
                Assert.AreEqual(PurchaseFlowStatus.Pending, rig.Buy("offer.gems.tier03").Status);
                Assert.AreEqual(0, rig.State.Wallet.Gems, "Nothing until the approval.");
                late.Clear();
                rig.Provider.ApprovePending();
                Assert.AreEqual(1150, rig.State.Wallet.Gems);
                Assert.AreEqual(1, late.Count(o => o.Status == PurchaseFlowStatus.Granted && o.Grant.IsNewGrant));
                Assert.AreEqual(ContentId.FromSource("offer.gems.tier03"), late[0].Offer);
            }
        }

        [Test]
        public void Store_OneTimeOffer_ThenRestoreOnANewDevice()
        {
            var content = Shipped();
            var provider = new SandboxStoreProvider("en", 0);
            using (var rig = new Rig(content, Player(), provider))
            {
                rig.Init();
                Assert.AreEqual(PurchaseFlowStatus.Granted, rig.Buy(ShopPipeline.StarterOffer).Status);
                Assert.AreEqual(ShopPipeline.StarterGems, rig.State.Wallet.Gems);
                Assert.AreEqual(PurchaseFlowStatus.NotAvailable, rig.Buy(ShopPipeline.StarterOffer).Status, "One-time offer.");
                Assert.AreEqual(PurchaseFlowStatus.NotAvailable, rig.Buy(ShopPipeline.StarterMoment).Status, "Same product, same limit.");
            }
            // Same store account, fresh install.
            using (var device = new Rig(content, Player(), provider))
            {
                device.Init();
                Assert.AreEqual(1, device.Store.RestoreAsync(CancellationToken.None).GetAwaiter().GetResult());
                Assert.IsTrue(device.State.Entitlements.Owned.Contains(ContentId.FromSource(ShopPipeline.FlowerCrown)));
                Assert.AreEqual(0, device.State.Wallet.Gems);
                Assert.AreEqual(0, device.Store.RestoreAsync(CancellationToken.None).GetAwaiter().GetResult(), "Restoring twice gives nothing more.");
                Assert.AreEqual(PurchaseFlowStatus.NotAvailable, device.Buy(ShopPipeline.StarterOffer).Status);
            }
        }

        [Test]
        public void GemPricedOffer_SpendsGems_AndGrantsItsItems()
        {
            var hat = ContentId.FromSource("cosmetic.test_hat");
            var builder = Builder();
            builder.AddProduct(new ProductDefinition(ContentId.FromSource("product.test.gems"), "sku_gems", ProductKind.Consumable, 99, 100, null), "product.test.gems");
            builder.AddOffer(Offer("offer.test.hat", gemCost: 80, items: new[] { hat }, limit: 1, placement: OfferPlacement.ShopFeatured), "offer.test.hat");
            var content = builder.Build();
            using (var rig = new Rig(content, Player()))
            {
                rig.Init();
                Assert.AreEqual(PurchaseFlowStatus.NotEnoughGems, rig.Buy("offer.test.hat").Status);
                rig.State.Wallet.Gems = 100;
                var outcome = rig.Buy("offer.test.hat");
                Assert.AreEqual(PurchaseFlowStatus.Granted, outcome.Status);
                Assert.AreEqual(20, rig.State.Wallet.Gems);
                Assert.IsTrue(rig.State.Entitlements.Owned.Contains(hat));
                Assert.AreEqual(0, rig.State.Shop.PaidPurchases, "Gems are not a real-money purchase.");
                Assert.AreEqual(PurchaseFlowStatus.NotAvailable, rig.Buy("offer.test.hat").Status);
                Assert.AreEqual(20, rig.State.Wallet.Gems);
            }
        }

        private sealed class HeldProvider : IStoreProvider
        {
            public readonly TaskCompletionSource<StorePurchaseResult> Pending = new TaskCompletionSource<StorePurchaseResult>();
            public string Name => "Held";
            public event Action<StorePurchaseResult> TransactionUpdated { add { } remove { } }

            public Task<IReadOnlyDictionary<string, StoreProductInfo>> InitializeAsync(IReadOnlyList<StoreProductRequest> products, CancellationToken token)
            {
                IReadOnlyDictionary<string, StoreProductInfo> infos = products.ToDictionary(p => p.Sku, p => new StoreProductInfo(p.Sku, "1", "EUR", 1m));
                return Task.FromResult(infos);
            }

            public Task<StorePurchaseResult> PurchaseAsync(string sku, CancellationToken token) => Pending.Task;
            public Task<IReadOnlyList<StorePurchaseResult>> RestoreAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<StorePurchaseResult>>(Array.Empty<StorePurchaseResult>());

            public void FinishTransaction(string transactionId)
            {
            }
        }

        [Test]
        public void Store_OnePurchaseAtATime()
        {
            var content = Shipped();
            var state = Player();
            var grants = new GrantService(content, state, null);
            var offers = new OfferService(content, state, grants, null);
            var provider = new HeldProvider();
            using (var store = new StoreService(content, offers, provider, new FakeGameClock(TestUtil.T0), new FakeSaves()))
            {
                store.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
                var first = store.PurchaseAsync(ContentId.FromSource("offer.gems.tier01"), CancellationToken.None);
                Assert.IsTrue(store.IsBusy);
                var second = store.PurchaseAsync(ContentId.FromSource("offer.gems.tier02"), CancellationToken.None).GetAwaiter().GetResult();
                Assert.AreEqual(PurchaseFlowStatus.Busy, second.Status);
                provider.Pending.SetResult(new StorePurchaseResult(StorePurchaseStatus.Success, "pw_gems_tier01", "held-1"));
                Assert.AreEqual(PurchaseFlowStatus.Granted, first.GetAwaiter().GetResult().Status);
                Assert.IsFalse(store.IsBusy);
                Assert.AreEqual(100, state.Wallet.Gems);
            }
        }

        // ---------------- Presenter ----------------

        private sealed class FakeShopView : IShopView, IOfferModalView
        {
            public bool Shown;
            public IShopModel Model;
            public readonly List<ShopMessage> Messages = new List<ShopMessage>();
            public ShopCardModel ModalCard;
            public bool IsOpen => Shown;
            public void Open(IShopModel model, ILocalization text, IShopActions actions) { Model = model; Shown = true; }
            public void Open(ShopCardModel card, IShopModel model, ILocalization text, IShopActions actions) { ModalCard = card; Model = model; Shown = true; }
            public void Close() => Shown = false;
            public int Prewarms;
            public void Prewarm(IShopModel model, ILocalization text, IShopActions actions) => Prewarms++;
            public void Prewarm(ShopCardModel card, IShopModel model, ILocalization text, IShopActions actions) => Prewarms++;
            public void Refresh() { }
            public void Refresh(ShopCardModel card) => ModalCard = card;
            public void ShowMessage(ShopMessage message, long value) => Messages.Add(message);
            public void Tick(float unscaledDeltaTime) { }
        }

        [Test]
        public void Presenter_BuildsHonestCards_AndOnlyTalksInOfferIds()
        {
            using (var rig = new Rig(Shipped(), Player()))
            {
                rig.Init();
                var shopView = new FakeShopView();
                var modalView = new FakeShopView();
                bool? worldVisible = null;
                using (var presenter = new ShopPresenter(rig.Store, null, shopView, modalView, NullAudioService.Instance, NullHapticService.Instance,
                           visible => worldVisible = visible, CancellationToken.None))
                {
                    presenter.Prewarm();
                    Assert.AreEqual(1, shopView.Prewarms);
                    Assert.AreEqual(1, modalView.Prewarms, "The Starter Pack modal is prewarmed too.");
                    Assert.IsFalse(shopView.Shown, "Prewarm leaves the screens closed.");
                    presenter.OpenShop();
                    Assert.IsTrue(shopView.Shown);
                    Assert.AreEqual(false, worldVisible, "Opaque shop: the world camera stops.");
                    var model = shopView.Model;
                    Assert.AreEqual(1, model.FeaturedCount);
                    Assert.AreEqual(6, model.GemPackCount);
                    int[] bonuses = { 0, 10, 15, 20, 25, 30 };
                    for (int i = 0; i < 6; i++)
                    {
                        var card = model.GetGemPack(i);
                        Assert.AreEqual(bonuses[i], card.BonusPercent, "Bonus vs the smallest pack, never overstated.");
                        Assert.IsNotNull(card.Price, "Price from the store.");
                    }
                    var starter = model.GetFeatured(0);
                    Assert.AreEqual(ShopPipeline.StarterGems, starter.Gems);
                    Assert.AreEqual("entitlement.cosmetic.flower_crown.name", starter.ItemKey);
                    Assert.IsTrue(starter.OneTime);

                    presenter.Buy(starter.Offer);
                    Assert.IsFalse(model.Busy);
                    Assert.AreEqual(ShopMessage.Thanks, shopView.Messages.Last());
                    Assert.IsTrue(model.GetFeatured(0).Owned, "The card shows it is owned.");
                    presenter.CloseShop();
                    Assert.AreEqual(true, worldVisible);
                    Assert.IsFalse(presenter.TryShowMomentOffer(), "Already owned: no moment offer.");
                }
            }
        }

        [Test]
        public void SteadyState_ShopTicksAndQueries_DoNotAllocate()
        {
            using (var rig = new Rig(Shipped(), Player()))
            {
                rig.Init();
                var view = new FakeShopView();
                using (var presenter = new ShopPresenter(rig.Store, null, view, view, NullAudioService.Instance, NullHapticService.Instance, _ => { },
                           CancellationToken.None))
                {
                    var buffer = new OfferDefinition[16];
                    long now = TestUtil.T0.ToUnixTimeSeconds();
                    presenter.Tick(0.016f);
                    rig.Offers.GetOffers(OfferPlacement.ShopGems, now, buffer, includeOwned: false);
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 1000; i++)
                    {
                        presenter.Tick(0.016f);
                        rig.Offers.GetOffers(OfferPlacement.ShopGems, now, buffer, includeOwned: false);
                        rig.Offers.GetOffers(OfferPlacement.ShopFeatured, now, buffer, includeOwned: true);
                        _ = rig.Offers.ShopUnlocked;
                    }
                    Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "The shop costs nothing per frame while closed.");
                }
            }
        }

        // ---------------- Criterion: no link between UI and the store provider ----------------

        [Test]
        public void Ui_HasNoLinkToTheStoreProvider()
        {
            var uiAsmdef = File.ReadAllText("Assets/_Game/UI/Scripts/Game.UI.asmdef");
            StringAssert.DoesNotContain("StoreSandbox", uiAsmdef, "The UI assembly cannot even see the sandbox store.");
            var forbidden = typeof(IStoreProvider).Namespace;
            var ui = typeof(PocketWilds.UI.ShopScreen).Assembly;
            var offenders = new List<string>();
            foreach (var type in ui.GetTypes())
            {
                foreach (var used in UsedTypes(type))
                {
                    if (used.Namespace == forbidden)
                        offenders.Add(type.Name + " -> " + used.Name);
                }
            }
            CollectionAssert.IsEmpty(offenders, "UI types must not use store types (provider, service, results).");

            // Presentation uses the provider-independent StoreService only, never a provider.
            foreach (var type in typeof(ShopPresenter).Assembly.GetTypes())
            {
                foreach (var used in UsedTypes(type))
                    Assert.IsFalse(typeof(IStoreProvider).IsAssignableFrom(used), type.Name + " uses a store provider: " + used.Name);
            }
        }

        private static IEnumerable<Type> UsedTypes(Type type)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            if (type.BaseType != null)
                yield return type.BaseType;
            foreach (var i in type.GetInterfaces())
                yield return i;
            foreach (var field in type.GetFields(all))
                yield return Element(field.FieldType);
            foreach (var property in type.GetProperties(all))
                yield return Element(property.PropertyType);
            foreach (var method in type.GetMethods(all))
            {
                yield return Element(method.ReturnType);
                foreach (var parameter in method.GetParameters())
                    yield return Element(parameter.ParameterType);
                var body = method.GetMethodBody();
                if (body != null)
                {
                    foreach (var local in body.LocalVariables)
                        yield return Element(local.LocalType);
                }
            }
        }

        private static Type Element(Type type)
        {
            while (type.HasElementType)
                type = type.GetElementType();
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    if (argument.Namespace == typeof(IStoreProvider).Namespace)
                        return argument;
                }
            }
            return type;
        }

        // ---------------- Save ----------------

        [Test]
        public void ShopState_RoundTripsThroughTheSave()
        {
            var state = Player();
            state.Wallet.Gems = 1234;
            state.Shop.PaidPurchases = 2;
            state.Shop.Offers[ContentId.FromSource(ShopPipeline.StarterMoment)] = new OfferRecord { Purchases = 0, Impressions = 3, LastShownUnix = 1791000123 };
            state.Entitlements.ProcessedTransactions.Add("sandbox-0042");
            var writer = new ByteBufferWriter();
            var serializer = new BinarySaveSerializer();
            serializer.Serialize(state, writer);
            var bytes = new byte[writer.Length];
            Array.Copy(writer.Buffer, bytes, writer.Length);
            var loaded = serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
            Assert.AreEqual(1234, loaded.Wallet.Gems);
            Assert.AreEqual(2, loaded.Shop.PaidPurchases);
            var record = loaded.Shop.Get(ContentId.FromSource(ShopPipeline.StarterMoment));
            Assert.AreEqual(3, record.Impressions);
            Assert.AreEqual(1791000123L, record.LastShownUnix);
            Assert.IsTrue(loaded.Entitlements.ProcessedTransactions.Contains("sandbox-0042"));
        }
    }
}
