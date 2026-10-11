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
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T34A in the real refuge: the tutorial paw on the first egg, the new species banner and ring, the animal sheet on
    /// a second touch (favorite saved, details open the Codex), the egg odds, the calm "not enough" toast, collect
    /// motes, Reduce Motion reaching every screen, the boot screen leaving after boot.
    /// </summary>
    public class FeedbackPlayTests
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

        private void Seed(int rabbits, bool reduceMotion, double light)
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var rabbit = ContentId.FromSource("species.rabbit");
            for (int i = 0; i < rabbits; i++)
            {
                state.Collection.Add(new AnimalInstance
                {
                    Species = rabbit, Variant = ContentId.FromSource("variant.rabbit.brown"), Rarity = RarityTier.Common,
                    Seed = SeedHash.Mix((uint)i + 1u, 0x0A0A0002u),
                });
            }
            if (rabbits > 0)
                state.Collection.DiscoveredSpecies.Add(rabbit);
            state.Settings.ReduceMotion = reduceMotion;
            state.Wallet.Light = BigValue.FromDouble(light);
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
            yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        private static RefugeController Refuge => Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
        private static GameContext Context => Object.FindAnyObjectByType<GameBootstrap>().Context;

        private static IEnumerator WaitHatchDone(RefugeController refuge)
        {
            float timeout = Time.realtimeSinceStartup + 8f;
            while (refuge.Hatch.IsPlaying || refuge.WaitingEgg == null)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Hatch did not finish.");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FirstEgg_ShowsThePaw_ThenTheNewSpeciesBannerAndRing()
        {
            Seed(0, false, 0);
            yield return Boot();
            var refuge = Refuge;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            float until = Time.realtimeSinceStartup + 3f;
            while (!refuge.TapHintShown && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(refuge.TapHintShown, "The paw invites the first touch on the egg.");
            yield return null;
            Assert.IsTrue(hud.TapHint.Shown);

            refuge.RequestHatch();
            Assert.IsTrue(refuge.Hatch.IsPlaying);
            Assert.AreEqual(HatchNews.NewSpecies, refuge.Hatch.News, "The first animal is a new species.");
            until = Time.realtimeSinceStartup + 4f;
            while (!hud.Reveal.Visible && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(hud.Reveal.Visible, "The banner shows at the reveal.");
            Assert.AreEqual(Context.Localization.Get(FeedbackText.RevealNewSpecies), hud.Reveal.TitleText);
            Assert.AreEqual(Context.Localization.Get("species.rabbit.name"), hud.Reveal.NameText);
            Assert.GreaterOrEqual(hud.Reveal.StarsShown, 1);
            Assert.IsFalse(refuge.TapHintShown, "The paw leaves with the first hatch.");
            var ring = refuge.Vfx.GetPool(VfxId.NewSpecies);
            Assert.GreaterOrEqual(ring.Stats.Active, 1, "The discovery ring plays around the newborn.");

            yield return WaitHatchDone(refuge);
            for (int i = 0; i < 90; i++)
                yield return null;
            Assert.IsFalse(refuge.TapHintShown, "Never again once an animal lives in the refuge.");
        }

        [UnityTest]
        public IEnumerator SecondTouch_OpensTheSheet_FavoriteIsSaved_DetailsOpenTheCodex()
        {
            Seed(6, false, 0);
            yield return Boot();
            var refuge = Refuge;
            var context = Context;
            var sheet = Object.FindAnyObjectByType<AnimalSheetScreen>();
            Assert.IsNotNull(sheet, "PF_UI_AnimalQuickSheet in the vertical slice.");
            for (int i = 0; i < 30; i++)
                yield return null;
            var camera = Object.FindAnyObjectByType<RefugeCameraRig>().Camera;
            var view = Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None)
                .Where(v => v.gameObject.activeInHierarchy && v.OwnerInstanceId != 0)
                .OrderBy(v => Vector3.Distance(v.transform.position, camera.transform.position)).First();
            Vector2 Screen() => camera.WorldToScreenPoint(view.transform.position + new Vector3(0f, 0.25f, 0f));

            refuge.OnTap(Screen());
            Assert.IsFalse(sheet.IsOpen, "The first touch is a reaction, not a screen.");
            yield return null;
            refuge.OnTap(Screen());
            Assert.IsTrue(sheet.IsOpen, "The second touch on the same animal opens its sheet.");
            Assert.AreEqual(view.OwnerInstanceId, sheet.InstanceShown);
            Assert.AreEqual(context.Localization.Get("species.rabbit.name"), sheet.NameText);
            Assert.AreEqual(context.Localization.Get(FeedbackText.RarityCommon), sheet.RarityText);
            Assert.AreEqual(1, sheet.StarsShown);
            Assert.IsTrue(refuge.ModalOpen, "World touches wait while the sheet is open.");

            sheet.FavoriteButton.onClick.Invoke();
            Assert.IsTrue(context.State.Collection.TryGet(view.OwnerInstanceId, out var animal));
            Assert.IsTrue(animal.IsFavorite, "The heart marks the animal as a favorite.");
            Assert.IsTrue(sheet.FavoriteShown, "The heart fills at once.");
            Assert.GreaterOrEqual(refuge.Vfx.GetPool(VfxId.FavoriteHeart).Stats.Active, 1, "A few hearts pop over the animal.");
            sheet.FavoriteButton.onClick.Invoke();
            context.State.Collection.TryGet(view.OwnerInstanceId, out animal);
            Assert.IsFalse(animal.IsFavorite);

            sheet.DetailsButton.onClick.Invoke();
            Assert.IsFalse(sheet.IsOpen);
            var codex = Object.FindAnyObjectByType<CodexScreen>();
            Assert.IsTrue(codex.IsOpen, "Details open the Codex...");
            Assert.IsTrue(codex.SheetOpen, "...on the animal's species page.");
        }

        [UnityTest]
        public IEnumerator OddsButton_ShowsEveryRarity_AndTheGuarantee()
        {
            Seed(3, false, 0);
            yield return Boot();
            var modal = Object.FindAnyObjectByType<ModalScreen>();
            Assert.IsNotNull(modal, "PF_UI_Modal in the vertical slice.");
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            var info = hud.transform.Find("SafeArea/HatchAction/Info/Button").GetComponent<UnityEngine.UI.Button>();
            info.onClick.Invoke();
            Assert.IsTrue(modal.IsOpen);
            Assert.AreEqual(ModalKind.Info, modal.Kind);
            // T35B: the window names the chosen egg (several eggs can be offered).
            Context.Content.Eggs.TryGet(ContentId.FromSource("egg.grassland.basic"), out var egg);
            Assert.AreEqual(Context.Localization.Get(egg.DisplayKey), modal.TitleText);
            // T35B: each line is the rarity's stars, then its word and chance (doc 02 Œufs, section 6).
            var lines = modal.BodyText.Split('\n');
            foreach (var key in new[] { FeedbackText.RarityCommon, FeedbackText.RarityRare, FeedbackText.RarityFabulous })
                Assert.IsTrue(lines.Any(l => l.TrimStart('★', ' ').StartsWith(Context.Localization.Get(key), StringComparison.Ordinal)), key);
            Assert.IsTrue(modal.BodyText.Contains("20"), "The new species guarantee (20 eggs) while species are left.");
            Assert.IsFalse(modal.CancelVisible, "Info has one button.");
            Assert.Greater(modal.CardHeight, 600f, "The card grew to its body.");
            modal.ConfirmButton.onClick.Invoke();
            for (int i = 0; i < 20; i++)
                yield return null;
            Assert.IsFalse(modal.IsOpen);
        }

        [UnityTest]
        public IEnumerator NotEnoughLight_OneCalmToast_NotASpam()
        {
            Seed(2, false, 0);
            yield return Boot();
            var refuge = Refuge;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            Assert.IsFalse(Context.Hatch.CanHatch(ContentId.FromSource("egg.grassland.basic")), "Two rabbits, no Light: the egg waits.");
            refuge.RequestHatch();
            yield return null;
            Assert.IsTrue(hud.Toasts.Showing || hud.Toasts.Waiting > 0, "One calm line says why.");
            Assert.AreEqual(ToastKind.Warning, hud.Toasts.CurrentKind);
            Assert.AreEqual(Context.Localization.Get(FeedbackText.ToastNotEnoughLight), hud.Toasts.CurrentText);
            for (int i = 0; i < 5; i++)
                refuge.RequestHatch();
            Assert.AreEqual(0, hud.Toasts.Waiting, "Tapping again does not queue the same line.");
        }

        [UnityTest]
        public IEnumerator CollectMotes_FlyIntoTheChip_AndSettle()
        {
            Seed(2, false, 0);
            yield return Boot();
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            hud.PlayCollect(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Assert.IsTrue(hud.Collect.Playing);
            Assert.That(hud.Collect.ActiveMotes, Is.InRange(CollectMotes.LowMotes, CollectMotes.MaxMotes), "Aggregated, never one per unit.");
            float until = Time.realtimeSinceStartup + 3f;
            while (hud.Collect.Playing && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsFalse(hud.Collect.Playing);
            Assert.AreEqual(hud.Collect.ActiveMotes, hud.Collect.Landed, "Every mote lands in the chip.");
            for (int i = 0; i < 30; i++)
                yield return null;
            Assert.AreEqual(Vector3.one, hud.transform.Find("SafeArea/LightChip").localScale, "The chip settles back after its pop.");
        }

        [UnityTest]
        public IEnumerator ReduceMotion_ReachesEveryScreen()
        {
            Seed(1, true, 0);
            yield return Boot();
            Assert.IsTrue(Object.FindAnyObjectByType<RefugeHud>().ReduceMotion);
            Assert.IsTrue(Object.FindAnyObjectByType<RefugeHud>().Reveal.ReduceMotion);
            Assert.IsTrue(Object.FindAnyObjectByType<AnimalSheetScreen>().ReduceMotion);
            Assert.IsTrue(Object.FindAnyObjectByType<ModalScreen>().ReduceMotion);
            Assert.IsTrue(Object.FindAnyObjectByType<CodexScreen>().ReduceMotion);
            Assert.IsTrue(Object.FindAnyObjectByType<OfflineReturnScreen>().ReduceMotion);
        }

        [UnityTest]
        public IEnumerator Splash_LeavesWithAFade()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<SplashScreen>("Assets/_Game/Prefabs/UI/Screens/PF_UI_Splash.prefab");
            Assert.IsNotNull(prefab);
            var splash = Object.Instantiate(prefab);
            yield return null;
            Assert.IsFalse(splash.FadingOut);
            splash.Dismiss();
            float until = Time.realtimeSinceStartup + 2f;
            while (splash != null && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(splash == null, "The boot screen destroys itself after its fade.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
