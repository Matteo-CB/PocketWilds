using System;
using System.Collections;
using System.IO;
using System.Linq;
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
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T47 review captures in the real game (run on request only): the crown and the seed basket worn by the companion and by
    /// species of both families, the burrow in the meadow, the outfit picker, the Codex's Companions tab, the shop's pack card
    /// and its offer. Images go to TestResults/Captures/T47_*.png at the game view's size.
    /// </summary>
    public class T47CapturePlayTests
    {
        private static readonly ContentId Crown = ContentId.FromSource("cosmetic.flower_crown");
        private static readonly ContentId Basket = ContentId.FromSource("accessory.fieldhamster.seedbasket");
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
            foreach (var splash in Object.FindObjectsByType<SplashScreen>(FindObjectsSortMode.None))
                Object.Destroy(splash.gameObject);
            TestSaveDir.Release(_saveDir);
        }

        [UnityTest, Explicit("T47 review captures, run on request.")]
        public IEnumerator Capture_Accessories_Burrow_Picker_Codex()
        {
            PrepareSave(owned: true);
            yield return Boot();
            var context = Context;
            var refuge = Refuge;
            context.Companions?.Sync();
            yield return Wait(4f);
            yield return ClearPopups(refuge);

            var hamster = ViewOf("species.fieldhamster");
            Assert.IsNotNull(hamster, "The companion is in the refuge.");
            context.Cosmetics.Equip(hamster.OwnerInstanceId, Crown, AccessorySocket.HeadTop, 0);
            context.Cosmetics.Equip(hamster.OwnerInstanceId, Basket, AccessorySocket.Back, 0);
            var dressed = new (string Species, string Name, ContentId Item, AccessorySocket Socket, int Palette)[]
            {
                ("species.rabbit", "rabbit_crown", Crown, AccessorySocket.HeadTop, 1),
                ("species.fawn", "fawn_crown", Crown, AccessorySocket.HeadTop, 2),
                ("species.hedgehog", "hedgehog_crown", Crown, AccessorySocket.HeadTop, 0),
                ("species.guineapig", "guineapig_basket", Basket, AccessorySocket.Back, 0),
            };
            foreach (var d in dressed)
            {
                var view = ViewOf(d.Species);
                if (view != null)
                    context.Cosmetics.Equip(view.OwnerInstanceId, d.Item, d.Socket, d.Palette);
            }
            yield return Wait(0.5f);

            var rig = Object.FindAnyObjectByType<RefugeCameraRig>();
            yield return Close(rig, hamster, "hamster_crown_basket");
            foreach (var d in dressed)
            {
                var view = ViewOf(d.Species);
                if (view != null)
                    yield return Close(rig, view, d.Name);
            }
            if (refuge.Burrow != null)
            {
                rig.FocusOn(refuge.Burrow.transform.position, RefugeCameraState.FocusAnimal, 3.2f);
                yield return Wait(1.5f);
                yield return Shot("burrow");
            }
            rig.ReturnToWorld();
            yield return Wait(1f);

            refuge.OpenOutfit(hamster.OwnerInstanceId);
            yield return Wait(1.2f);
            yield return Shot("outfit_picker");
            Object.FindAnyObjectByType<OutfitPickerScreen>().Close();
            rig.ReturnToWorld();
            yield return Wait(1f);

            refuge.OpenCodex();
            yield return Wait(0.3f);
            var codex = Object.FindAnyObjectByType<CodexScreen>();
            codex.CompanionsTabButton.onClick.Invoke();
            yield return Wait(0.8f);
            yield return Shot("codex_companions");
        }

        [UnityTest, Explicit("T47 review captures, run on request.")]
        public IEnumerator Capture_Shop_Pack_And_Offer()
        {
            PrepareSave(owned: false);
            yield return Boot();
            var refuge = Refuge;
            yield return ClearPopups(refuge);
            refuge.OpenCodex();
            yield return Wait(0.3f);
            Object.FindAnyObjectByType<CodexScreen>().CompanionsTabButton.onClick.Invoke();
            yield return Wait(0.8f);
            yield return Shot("codex_silhouette");
            Object.FindAnyObjectByType<CodexScreen>().Close();
            yield return Wait(0.6f);

            refuge.OpenShop();
            yield return Wait(1f);
            yield return Shot("shop_pack_card");
            refuge.Shop.CloseShop();
            yield return Wait(0.6f);
            refuge.Shop.TryOpenOfferGranting(CompanionCatalog.FieldHamster.Entitlement);
            yield return Wait(1.2f);
            yield return Shot("pack_offer");
        }

        // ---------------- helpers ----------------

        private void PrepareSave(bool owned)
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            string[] animals = { "rabbit.brown", "squirrel.red", "hedgehog.brown", "fawn.chestnutspots", "guineapig.caramel", "fieldmouse.warmbrown", "rabbit.white" };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource("species." + animals[i].Substring(0, animals[i].IndexOf('.')));
                var variant = ContentId.FromSource("variant." + animals[i]);
                state.Collection.Add(new AnimalInstance { Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = (uint)i + 11u });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            state.Wallet.Light = BigValue.FromMantissaExponent(1.0, 9);
            if (owned)
            {
                state.Entitlements.Owned.Add(Crown);
                state.Entitlements.Owned.Add(Basket);
                state.Entitlements.Owned.Add(CompanionCatalog.FieldHamster.Entitlement);
                state.Entitlements.Owned.Add(ContentId.FromSource("decor.cozy_burrow"));
            }
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private IEnumerator Boot()
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
                StoreProviderOverride = new SandboxStoreProvider("fr", 0),
                LoadTargetScene = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 60f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return Wait(1f);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        /// <summary>Claims the level cards and closes a moment offer, as a player would, before the photos.</summary>
        private static IEnumerator ClearPopups(RefugeController refuge)
        {
            var presenter = Object.FindAnyObjectByType<VerticalSliceEntry>().ProgressionScreens;
            for (int round = 0; round < 6; round++)
            {
                if (refuge.Shop != null && refuge.Shop.OfferOpen)
                    refuge.Shop.CloseOffer();
                var card = Object.FindAnyObjectByType<LevelRewardScreen>();
                if (presenter != null && presenter.RewardWaiting && (card == null || !card.IsOpen))
                    presenter.ShowPendingRewards();
                yield return Wait(0.4f);
                card = Object.FindAnyObjectByType<LevelRewardScreen>();
                if (card != null && card.IsOpen)
                    card.ClaimButton.onClick.Invoke();
                yield return Wait(0.6f);
                if ((presenter == null || !presenter.RewardWaiting) && !refuge.ModalOpen)
                    yield break;
            }
        }

        private static RefugeController Refuge => Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
        private static GameContext Context => Object.FindAnyObjectByType<GameBootstrap>().Context;

        private static AnimalView ViewOf(string species)
        {
            var id = ContentId.FromSource(species);
            return Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None)
                .FirstOrDefault(v => v.gameObject.activeInHierarchy && v.OwnerInstanceId != 0 && v.Species == id);
        }

        private static IEnumerator Close(RefugeCameraRig rig, AnimalView view, string name)
        {
            rig.FocusOn(view.transform.position + new Vector3(0f, 0.15f, 0f), RefugeCameraState.FocusAnimal, 1.1f);
            yield return Wait(1.2f);
            yield return Shot(name);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        private static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "Captures");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "T47_" + name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
