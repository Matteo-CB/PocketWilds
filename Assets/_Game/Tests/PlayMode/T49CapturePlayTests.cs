using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// T49 review captures in the real game (run on request only): the real event.burrows of the default calendar, the clock
    /// set to its second day, the hub joined; then for each quality tier (Low, Medium, High forced through the quality service)
    /// the hub's banner and track, the egg tab, the Velvet Mole wearing the Sprout Hat in the refuge, a hatch with the Sprout
    /// Burst, and the profile card in the « Racines » frame. Images go to TestResults/Captures/T49_&lt;tier&gt;_&lt;name&gt;.png at the game
    /// view's size.
    /// </summary>
    public class T49CapturePlayTests
    {
        private static readonly ContentId SproutHat = ContentId.FromSource("accessory.burrows.sprout_hat");
        private static readonly ContentId SproutBurst = ContentId.FromSource(HatchEffects.SproutBurst);
        private const string Frame = "frame.event_burrows";
        private string _saveDir;
        private FakeGameClock _clock;

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

        [UnityTest, Explicit("T49 review captures on the three quality tiers, run on request.")]
        public IEnumerator Capture_Burrows_OnEveryTier()
        {
            var entry = DefaultEventCalendar.Entries()[0];
            _clock = new FakeGameClock(entry.StartUtc.AddDays(1.0));
            PrepareSave();
            yield return Boot();
            var context = Context;
            var refuge = Refuge;
            yield return Wait(3f);
            yield return ClearPopups(refuge);
            Assert.AreEqual(EventPhase.Running, context.Events.Phase, "The calendar override puts event.burrows in its run.");

            var mole = ViewOf("species.mole");
            Assert.IsNotNull(mole, "The Velvet Mole is in the refuge.");
            context.Cosmetics.Equip(mole.OwnerInstanceId, SproutHat, AccessorySocket.HeadTop, 0);
            int frame = ProfileData.IndexOf(ProfileItemKind.Frame, Frame);
            context.ProfileItems.Grant(ProfileItemKind.Frame, frame);
            context.ProfileItems.TrySelect(ProfileItemKind.Frame, frame);
            var rig = Object.FindAnyObjectByType<RefugeCameraRig>();

            foreach (var tier in new[] { QualityTier.Low, QualityTier.Medium, QualityTier.High })
            {
                context.Quality.ForceState(QualityState.Uniform(tier));
                yield return Wait(1f);
                string t = tier.ToString();
                // A level card or a moment offer may have come after the previous tier's hatch: the hub opens on a calm refuge.
                yield return ClearPopups(refuge);
                yield return Wait(0.6f);

                refuge.OpenEvent();
                yield return Wait(0.8f);
                var hub = Object.FindAnyObjectByType<EventHubScreen>();
                Assert.IsTrue(hub.IsOpen);
                Assert.IsTrue(context.Events.IsJoined, "Opening the hub joins the event.");
                yield return Shot(t, "hub_track");
                hub.SelectTab(EventHubTab.Egg);
                yield return Wait(0.5f);
                yield return Shot(t, "hub_egg");
                hub.Close();
                yield return Wait(0.6f);

                mole = ViewOf("species.mole");
                rig.FocusOn(mole.transform.position + new Vector3(0f, 0.12f, 0f), RefugeCameraState.FocusAnimal, 1.0f);
                yield return Wait(1.3f);
                yield return Shot(t, "mole_sprout_hat");
                rig.ReturnToWorld();
                yield return Wait(1f);

                yield return HatchWithEffect(refuge, t);
                yield return ClearPopups(refuge);
                rig.ReturnToWorld();
                yield return Wait(1f);

                var profile = ProfilePresenter();
                if (profile != null)
                {
                    profile.OpenProfile();
                    yield return Wait(1f);
                    yield return Shot(t, "profile_racines");
                    profile.CloseScreen();
                    yield return Wait(0.6f);
                }
            }
        }

        /// <summary>Waits for the egg, taps it and shoots while the Sprout Burst grows around the egg spot.</summary>
        private static IEnumerator HatchWithEffect(RefugeController refuge, string tier)
        {
            float until = Time.realtimeSinceStartup + 8f;
            while ((refuge.Hatching || refuge.WaitingEgg == null || refuge.ModalOpen) && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsNotNull(refuge.WaitingEgg, "An egg waits on its spot.");
            var camera = Object.FindAnyObjectByType<RefugeCameraRig>().Camera;
            refuge.OnTap(camera.WorldToScreenPoint(refuge.WaitingEgg.VfxPoint.position));
            until = Time.realtimeSinceStartup + 10f;
            while (refuge.Hatch.Phase != HatchPhase.Revealing && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.AreEqual(VfxId.SproutBurst, refuge.Hatch.Effect, "The owned Sprout Burst plays on this hatch.");
            yield return Wait(0.45f);
            yield return Shot(tier, "hatch_sprout_burst");
            until = Time.realtimeSinceStartup + 10f;
            while (refuge.Hatching && Time.realtimeSinceStartup < until)
                yield return null;
            yield return Wait(0.8f);
        }

        // ---------------- helpers ----------------

        private void PrepareSave()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(), _clock,
                new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            string[] animals = { "mole.velvet", "rabbit.brown", "squirrel.red", "hedgehog.brown", "fawn.chestnutspots", "guineapig.caramel" };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource("species." + animals[i].Substring(0, animals[i].IndexOf('.')));
                var variant = ContentId.FromSource("variant." + animals[i]);
                state.Collection.Add(new AnimalInstance { Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = (uint)i + 49u });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            state.Wallet.Light = BigValue.FromMantissaExponent(1.0, 12);
            state.Entitlements.Owned.Add(SproutHat);
            state.Entitlements.Owned.Add(SproutBurst);
            HatchEffects.OnGranted(state.Entitlements, SproutBurst);
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
                ClockOverride = _clock,
                EventCalendarOverride = DefaultEventCalendar.Create(),
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

        private static ProfileScreenPresenter ProfilePresenter() =>
            typeof(VerticalSliceEntry).GetField("_profilePresenter", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(Object.FindAnyObjectByType<VerticalSliceEntry>()) as ProfileScreenPresenter;

        private static RefugeController Refuge => Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
        private static GameContext Context => Object.FindAnyObjectByType<GameBootstrap>().Context;

        private static AnimalView ViewOf(string species)
        {
            var id = ContentId.FromSource(species);
            return Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None)
                .FirstOrDefault(v => v.gameObject.activeInHierarchy && v.OwnerInstanceId != 0 && v.Species == id);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        private static IEnumerator Shot(string tier, string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "Captures");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "T49_" + tier + "_" + name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
