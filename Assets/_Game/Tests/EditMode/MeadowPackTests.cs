using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Shop;
using PocketWilds.Simulation;
using UnityEditor;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T47 Meadow Pack (« Pack de la Prairie », the Starter Pack's product): one purchase grants the Field Hamster, the Cozy
    /// Burrow, the Flower Crown and 500 Gems once; a restore brings the items and the companion back, never the Gems; a refund
    /// takes the items and the companion away.
    /// </summary>
    public class MeadowPackTests
    {
        private ContentRegistry _content;

        [SetUp]
        public void SetUp() => _content = T47Content.Build();

        [Test]
        public void Purchase_GrantsAllFourItemsAnd500Gems_Once()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            var companions = T47Content.Companions(_content, state, out var grants);
            var first = grants.GrantPurchase("txn-meadow", T47Content.Pack, ContentId.None);
            Assert.AreEqual(GrantStatus.Granted, first.Status);
            Assert.AreEqual(3, first.NewItems);
            Assert.AreEqual(T47Content.PackGems, state.Wallet.Gems);
            Assert.IsTrue(grants.Owns(T47Content.CompanionItem) && grants.Owns(T47Content.Burrow) && grants.Owns(T47Content.Crown));
            Assert.AreNotEqual(0u, companions.AnimalOf(T47Content.Hamster), "The Field Hamster arrives.");
            int animals = state.Collection.Count;

            var again = grants.GrantPurchase("txn-meadow", T47Content.Pack, ContentId.None);
            Assert.AreEqual(GrantStatus.AlreadyGranted, again.Status);
            Assert.AreEqual(T47Content.PackGems, state.Wallet.Gems, "A repeated transaction adds nothing.");
            Assert.AreEqual(animals, state.Collection.Count);
            Assert.AreEqual(1, state.Shop.PaidPurchases);
        }

        [Test]
        public void Restore_OnAnEmptyProfile_BringsBackCompanionBurrowAndCrown_NoGems()
        {
            var device = PlayerState.CreateNew(TestUtil.T0);
            var companions = T47Content.Companions(_content, device, out var grants);
            var restored = grants.GrantPurchase("txn-meadow", T47Content.Pack, ContentId.None, restore: true);
            Assert.AreEqual(GrantStatus.Granted, restored.Status);
            Assert.AreEqual(0, device.Wallet.Gems, "The Gems were spent on the first device.");
            Assert.AreEqual(0, device.Shop.PaidPurchases);
            Assert.IsTrue(device.Entitlements.Owned.Contains(T47Content.CompanionItem));
            Assert.IsTrue(device.Entitlements.Owned.Contains(T47Content.Burrow));
            Assert.IsTrue(device.Entitlements.Owned.Contains(T47Content.Crown));
            Assert.AreNotEqual(0u, companions.AnimalOf(T47Content.Hamster), "The companion comes back.");
            Assert.AreEqual(1, device.Collection.Count);
        }

        [Test]
        public void Revoke_RemovesThePackItemsAndTheCompanion()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            var companions = T47Content.Companions(_content, state, out var grants);
            var cosmetics = new CosmeticService(state, _content, null) { Grants = grants };
            companions.Cosmetics = cosmetics;
            uint rabbit = T47Content.AddAnimal(state, T47Content.Rabbit, "variant.rabbit.brown");
            grants.GrantPurchase("txn-meadow", T47Content.Pack, ContentId.None);
            uint hamster = companions.AnimalOf(T47Content.Hamster);
            Assert.AreEqual(EquipResult.Ok, cosmetics.Equip(rabbit, T47Content.Crown, AccessorySocket.HeadTop));
            Assert.AreEqual(EquipResult.Ok, cosmetics.Equip(hamster, T47Content.Crown, AccessorySocket.HeadTop));
            var left = 0u;
            companions.Left += (species, animal) => left = animal;

            var result = grants.Revoke("txn-meadow", T47Content.Pack);
            Assert.AreEqual(GrantStatus.Revoked, result.Status);
            Assert.AreEqual(3, result.NewItems, "Items removed.");
            Assert.IsFalse(state.Entitlements.Owned.Contains(T47Content.CompanionItem));
            Assert.IsFalse(state.Entitlements.Owned.Contains(T47Content.Burrow));
            Assert.IsFalse(state.Entitlements.Owned.Contains(T47Content.Crown));
            Assert.AreEqual(hamster, left);
            Assert.IsFalse(state.Collection.TryGetIndex(hamster, out _), "The companion leaves.");
            Assert.AreEqual(0u, companions.AnimalOf(T47Content.Hamster));
            Assert.AreEqual(0, state.Cosmetics.Animals.Count, "The crown comes off.");
            Assert.AreEqual(1.0, CompanionCatalog.BiomeMultiplier(state.Entitlements, ContentId.FromSource("biome.grassland")), 1e-9);
            Assert.AreEqual(GrantStatus.AlreadyGranted, grants.GrantPurchase("txn-meadow", T47Content.Pack, ContentId.None).Status,
                "A late callback of a refunded purchase grants nothing.");
        }

        [Test]
        public void ShippedCatalog_IsTheMeadowPack()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var content = database.CreateBuilder().Build();
            var shop = content.Shop;
            var pack = shop.Products.Get(ContentId.FromSource(ShopPipeline.MeadowPackProduct));
            Assert.AreEqual("pw.pack.grassland", pack.StoreSku);
            Assert.AreEqual(ProductKind.NonConsumable, pack.Kind);
            Assert.AreEqual(199, pack.ReferencePriceCents);
            Assert.AreEqual(500, pack.Gems);
            CollectionAssert.AreEquivalent(new[] { T47Content.CompanionItem, T47Content.Burrow, T47Content.Crown }, pack.Entitlements);
            Assert.AreEqual(EntitlementKind.Companion, shop.Entitlements.Get(T47Content.CompanionItem).Kind);
            Assert.AreEqual(EntitlementKind.Decoration, shop.Entitlements.Get(T47Content.Burrow).Kind);
            Assert.AreEqual("shop.pack.grassland.title", shop.Offers.Get(ContentId.FromSource(ShopPipeline.StarterOffer)).DisplayKey);
            Assert.AreEqual(AccessorySocket.HeadTop, shop.Accessories.Get(T47Content.Crown).Socket);
            Assert.AreEqual(AccessorySocket.Back, shop.Accessories.Get(T47Content.Basket).Socket);

            var hamster = content.Species.Get(T47Content.Hamster);
            Assert.IsTrue(hamster.IsCompanion && hamster.HidesSocket(AccessorySocket.TailBase));
            for (int e = 0; e < content.Eggs.Count; e++)
                foreach (var entry in content.Eggs[e].Pool)
                    Assert.AreNotEqual(T47Content.Hamster, entry.Species, "A companion is never in an egg.");

            string[] keys = { "shop.pack.grassland.title", "shop.pack.grassland.body", "shop.pack.one_time", "shop.restore_note", "species.fieldhamster.name" };
            foreach (var table in database.localization)
                foreach (string key in keys)
                    Assert.IsTrue(table.TryGet(key, out string value) && !string.IsNullOrEmpty(value), $"{table.languageCode}: missing {key}");
        }
    }
}
