using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T28 in the real scene through the bootstrap, with the sandbox store: HUD shortcut, shop screen, a Gem pack
    /// bought by pressing its button, kept after a restart; the Starter Pack offered after a hatch and bought once.
    /// </summary>
    public class ShopPlayTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            try
            {
                Directory.Delete(_saveDir, true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>Past onboarding: six animals of two species, Light to hatch, nothing bought.</summary>
        private void PrepareSave()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            string[] animals = { "rabbit.brown", "squirrel.red", "rabbit.white", "squirrel.grey", "rabbit.grey", "rabbit.spotted" };
            for (int i = 0; i < animals.Length; i++)
            {
                string speciesName = "species." + animals[i].Substring(0, animals[i].IndexOf('.'));
                var species = ContentId.FromSource(speciesName);
                var variant = ContentId.FromSource("variant." + animals[i]);
                state.Collection.Add(new AnimalInstance { Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = (uint)i + 11u });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            state.Wallet.Light = BigValue.FromMantissaExponent(1.0, 9);
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private IEnumerator Boot(Action<GameBootstrap> done)
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LocalizationOverride = database.localization.ToArray(),
                StoreProviderOverride = new SandboxStoreProvider("en", 0),
                LoadTargetScene = true,
            };
#endif
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 20f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return null;
            yield return null;
            done(bootstrap);
        }

        [UnityTest]
        public IEnumerator Shop_FromTheHud_BuysGems_KeptAfterRestart()
        {
#if UNITY_EDITOR
            PrepareSave();
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var sanctuary = entry.Sanctuary;
            var hud = Object.FindAnyObjectByType<SanctuaryHud>();
            Assert.IsNotNull(sanctuary.Shop, "The scene has a shop.");
            Assert.IsTrue(bootstrap.Context.Store.IsReady, "Sandbox prices loaded in the background.");
            Assert.IsTrue(hud.ShopVisible, "Shop shortcut after onboarding.");

            sanctuary.OpenShop();
            yield return null;
            var screen = Object.FindAnyObjectByType<ShopScreen>();
            Assert.IsTrue(screen.IsOpen);
            Assert.IsFalse(entry.CameraRig.Camera.enabled, "Opaque shop: the world camera stops.");
            float until;
            Assert.AreEqual(6, screen.VisibleGemCards);
            Assert.AreEqual(1, screen.VisibleFeaturedCards, "Starter Pack featured for a new player.");
            var card = screen.GetGemCard(0);
            Assert.AreEqual("€0.99", card.PriceText);

            card.GetComponentInChildren<Button>().onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual(100, bootstrap.Context.State.Wallet.Gems);
            StringAssert.Contains("100", screen.ToastText);
            Assert.AreEqual("100", hud.GemsText);
            Assert.AreEqual(0, screen.VisibleFeaturedCards, "Now a payer: the Starter Pack is for new players.");

            screen.transform.Find("Content/SafeArea/Header/Back").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsFalse(screen.IsOpen);
            Assert.IsTrue(entry.CameraRig.Camera.enabled);

            // The store is told only once the background save holds the purchase.
            until = Time.realtimeSinceStartup + 5f;
            while (bootstrap.Context.Store.AwaitingSave > 0)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "The purchase was never saved and acknowledged.");
                yield return null;
            }

            // Restart: the purchase is in the save.
            Object.Destroy(bootstrap.gameObject);
            yield return null;
            yield return Boot(b => bootstrap = b);
            Assert.AreEqual(100, bootstrap.Context.State.Wallet.Gems);
            Assert.AreEqual(1, bootstrap.Context.State.Shop.PaidPurchases);
            Assert.AreEqual(1, bootstrap.Context.State.Entitlements.ProcessedTransactions.Count);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator StarterPack_OfferedAfterAHatch_BoughtOnce()
        {
#if UNITY_EDITOR
            PrepareSave();
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var sanctuary = Object.FindAnyObjectByType<VerticalSliceEntry>().Sanctuary;
            var modal = Object.FindAnyObjectByType<OfferModalView>();
            Assert.IsFalse(modal.IsOpen, "Never at start: only after a happy moment.");

            float until = Time.realtimeSinceStartup + 5f;
            while (sanctuary.WaitingEgg == null)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "No egg to hatch.");
                yield return null;
            }
            sanctuary.RequestHatch();
            until = Time.realtimeSinceStartup + 15f;
            while (!modal.IsOpen)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "The Starter Pack was not offered after the hatch.");
                if (sanctuary.Hatch.IsPlaying)
                    sanctuary.RequestHatch(); // speed up the reveal
                yield return null;
            }
            Assert.IsFalse(sanctuary.Hatch.IsPlaying, "Shown once the reveal is over.");
            Assert.AreEqual(ContentId.FromSource("offer.starter.moment"), modal.Card.Offer);
            Assert.AreEqual("€1.99", modal.Card.PriceText);

            modal.Card.GetComponentInChildren<Button>().onClick.Invoke();
            yield return null;
            var state = bootstrap.Context.State;
            Assert.AreEqual(500, state.Wallet.Gems);
            Assert.IsTrue(state.Entitlements.Owned.Contains(ContentId.FromSource("cosmetic.flower_crown")));
            StringAssert.Contains("500", modal.ToastText);
            Assert.AreEqual(bootstrap.Context.Localization.Get("shop.badge.owned"), modal.Card.PriceText);
            until = Time.realtimeSinceStartup + 4f;
            while (modal.IsOpen)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "The modal closes after the thank-you.");
                yield return null;
            }

            var store = bootstrap.Context.Store;
            var again = store.PurchaseAsync(ContentId.FromSource("offer.starter.001"), default).GetAwaiter().GetResult();
            Assert.AreEqual(PurchaseFlowStatus.NotAvailable, again.Status, "One-time offer.");
            Assert.AreEqual(500, state.Wallet.Gems);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
