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
    /// T47 screens in the real scene through the bootstrap (sandbox store): the sheet's « Tenue » opens the outfit picker and the
    /// crown goes on a rabbit; the Codex's Companions tab shows the silhouette and « Pack de la Prairie » until owned, then the
    /// companion's card; the shop's featured pack card shows the frozen texts, its content row and the restore note.
    /// </summary>
    public class OutfitCompanionPlayTests
    {
        private static readonly ContentId Crown = ContentId.FromSource("cosmetic.flower_crown");
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
            try
            {
                TestSaveDir.Release(_saveDir);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>Past onboarding: six animals of two species, Light to hatch; optionally the crown or the companion owned.</summary>
        private void PrepareSave(bool crown, bool companion)
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            string[] animals = { "rabbit.brown", "squirrel.red", "rabbit.white", "squirrel.grey", "rabbit.grey", "rabbit.spotted" };
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
            if (crown)
                state.Entitlements.Owned.Add(Crown);
            if (companion)
                state.Entitlements.Owned.Add(CompanionCatalog.FieldHamster.Entitlement);
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
                StoreProviderOverride = new SandboxStoreProvider("en", 0),
                LoadTargetScene = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 20f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            for (int i = 0; i < 30; i++)
                yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        private static RefugeController Refuge => Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
        private static GameContext Context => Object.FindAnyObjectByType<GameBootstrap>().Context;

        [UnityTest]
        public IEnumerator SheetOutfit_OpensThePicker_AndTheCrownGoesOnARabbit()
        {
            PrepareSave(crown: true, companion: false);
            yield return Boot();
            var refuge = Refuge;
            var context = Context;
            var rabbit = ContentId.FromSource("species.rabbit");
            var rig = Object.FindAnyObjectByType<RefugeCameraRig>();
            var camera = rig.Camera;
            var view = Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None)
                .Where(v => v.gameObject.activeInHierarchy && v.OwnerInstanceId != 0 && v.Species == rabbit)
                .OrderBy(v => Vector3.Distance(v.transform.position, camera.transform.position)).First();
            uint id = view.OwnerInstanceId;
            Assert.AreEqual(EquipResult.Ok, context.Cosmetics.CanEquip(id, Crown, AccessorySocket.HeadTop), "The crown fits a rabbit's head.");
            Vector2 Screen() => camera.WorldToScreenPoint(view.transform.position + new Vector3(0f, 0.25f, 0f));

            var sheet = Object.FindAnyObjectByType<AnimalSheetScreen>();
            refuge.OnTap(Screen());
            yield return null;
            refuge.OnTap(Screen());
            Assert.IsTrue(sheet.IsOpen, "The second touch opens the sheet.");
            Assert.IsTrue(sheet.OutfitShown, "« Tenue » shows: the crown is owned.");

            sheet.OutfitButton.onClick.Invoke();
            yield return null;
            var picker = Object.FindAnyObjectByType<OutfitPickerScreen>();
            Assert.IsNotNull(picker, "PF_UI_OutfitPicker in the vertical slice.");
            Assert.IsFalse(sheet.IsOpen, "The sheet gives the bottom of the screen to the picker.");
            Assert.IsTrue(picker.IsOpen);
            Assert.AreEqual(id, refuge.OutfitAnimal);
            Assert.IsTrue(refuge.ModalOpen, "World touches wait while the picker is open.");
            Assert.AreEqual(RefugeCameraState.FocusAnimal, rig.State, "The camera keeps the animal in view.");
            Assert.AreEqual(0, picker.SelectedSlot, "Opens on the head, where the crown fits.");
            Assert.IsFalse(picker.SlotLockedShown(0));
            Assert.IsTrue(picker.SlotLockedShown(1), "Nothing owned for the neck: calm locked state.");
            Assert.GreaterOrEqual(picker.ChoicesShown, 1);

            picker.ChoiceButton(0).onClick.Invoke();
            var worn = context.Cosmetics.GetEquipped(id, AccessorySocket.HeadTop);
            Assert.AreEqual(Crown, worn.Accessory, "The crown is on the rabbit.");
            Assert.AreEqual(3, picker.PalettesShown, "meadow, blossom, frost");
            picker.PaletteButton(1).onClick.Invoke();
            Assert.AreEqual(1, context.Cosmetics.GetEquipped(id, AccessorySocket.HeadTop).Palette, "blossom");

            picker.RemoveButton.onClick.Invoke();
            Assert.IsTrue(context.Cosmetics.GetEquipped(id, AccessorySocket.HeadTop).IsEmpty, "« Retirer » takes it off.");
            picker.ChoiceButton(0).onClick.Invoke();
            Assert.AreEqual(Crown, context.Cosmetics.GetEquipped(id, AccessorySocket.HeadTop).Accessory);

            picker.CloseButton.onClick.Invoke();
            float until = Time.realtimeSinceStartup + 2f;
            while (picker.IsOpen && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsFalse(picker.IsOpen);
            Assert.AreEqual(RefugeCameraState.DefaultWorld, rig.State, "The view goes back to the refuge.");
            Assert.AreEqual(Crown, context.Cosmetics.GetEquipped(id, AccessorySocket.HeadTop).Accessory, "Kept after closing.");
        }

        [UnityTest]
        public IEnumerator CodexCompanions_ShowTheSilhouette_UntilOwned_ThenTheOfferOpens()
        {
            PrepareSave(crown: false, companion: false);
            yield return Boot();
            var context = Context;
            Refuge.OpenCodex();
            yield return null;
            var codex = Object.FindAnyObjectByType<CodexScreen>();
            Assert.IsTrue(codex.IsOpen);
            Assert.IsFalse(codex.CompanionsShown, "The Codex opens on its species.");
            codex.CompanionsTabButton.onClick.Invoke();
            Assert.IsTrue(codex.CompanionsShown, "« Compagnons » tab.");
            var card = codex.GetCompanionCard(0);
            Assert.IsTrue(card.SilhouetteShown, "Not owned: the silhouette.");
            Assert.IsFalse(card.OwnedShown);
            Assert.AreEqual(context.Localization.Get("shop.pack.grassland.title"), card.PackText, "« Pack de la Prairie » under it.");

            card.PackButton.onClick.Invoke();
            yield return null;
            var modal = Object.FindAnyObjectByType<OfferModalView>();
            Assert.IsTrue(modal.IsOpen, "The pack's offer opens over the Codex.");
            Assert.AreEqual(context.Localization.Get("shop.pack.grassland.title"), modal.Card.TitleText);
        }

        [UnityTest]
        public IEnumerator CodexCompanions_OwnedHamster_ShowsItsCard_AndNeverCountsInThePercentage()
        {
            PrepareSave(crown: false, companion: true);
            yield return Boot();
            var context = Context;
            context.Companions?.Sync();
            Refuge.OpenCodex();
            yield return null;
            var codex = Object.FindAnyObjectByType<CodexScreen>();
            codex.CompanionsTabButton.onClick.Invoke();
            var card = codex.GetCompanionCard(0);
            Assert.IsTrue(card.OwnedShown, "Owned: its card.");
            Assert.IsFalse(card.SilhouetteShown);
            Assert.AreEqual(context.Localization.Get("species.fieldhamster.name"), card.NameText);
            Assert.AreEqual(context.Localization.Get("codex.companions.bonus.fieldhamster"), card.BonusText);
            int level = context.Companions.GetBond(CompanionCatalog.FieldHamster.Species).Level;
            Assert.AreEqual(level, card.BondLevelShown);
            Assert.AreEqual(level < 5, card.RungLockShown(0), "Bond 5 locked until reached.");
            Assert.IsTrue(card.RungLockShown(4) || level >= 25, "Bond 25 locked until reached.");
            Assert.AreEqual(2, context.Codex.DiscoveredSpecies, "Companions never count in the Codex percentage.");
        }

        [UnityTest]
        public IEnumerator ShopPackCard_ShowsThePackTexts_ContentRow_AndRestore()
        {
            PrepareSave(crown: false, companion: false);
            yield return Boot();
            var loc = Context.Localization;
            Refuge.OpenShop();
            yield return null;
            var screen = Object.FindAnyObjectByType<ShopScreen>();
            Assert.IsTrue(screen.IsOpen);
            Assert.AreEqual(1, screen.VisibleFeaturedCards, "The Meadow Pack is featured for a new player.");
            var card = screen.GetFeaturedCard(0);
            Assert.AreEqual(loc.Get("shop.pack.grassland.title"), card.TitleText);
            Assert.AreEqual(loc.Get("shop.pack.grassland.body"), card.ContentsText);
            Assert.AreEqual(loc.Get("shop.pack.one_time"), card.OneTimeText, "« Offre unique » badge.");
            Assert.AreEqual(loc.Get("shop.restore_note"), card.NoteText, "Restore note under the button.");
            Assert.AreEqual(4, card.PackItemsShown, "Hamster, 500 Gems, burrow, crown.");
            // Under its icon the companion goes by its short name (the full name would take three lines in French).
            Assert.AreEqual(loc.Get("species.fieldhamster.short"), card.PackLabel(0));
            StringAssert.Contains("500", card.PackLabel(1));
            Assert.AreEqual(loc.Get("decor.cozy_burrow.name"), card.PackLabel(2));
            Assert.AreEqual(loc.Get("entitlement.cosmetic.flower_crown.name"), card.PackLabel(3));
            Assert.IsNotNull(card.ArtShown, "The hamster is the hero image.");
            StringAssert.DoesNotContain("%", card.PriceText, "No percentage on the pack.");
            var restore = screen.transform.Find("Content/SafeArea/Viewport/List/Restore");
            Assert.IsNotNull(restore, "« Restaurer les achats » at the bottom of the shop.");
        }
    }
}
