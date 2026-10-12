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
    /// T50 review captures of the Forêt Magnifique (BIOME DONE: the beauty review on the three tiers, Morning / Day / Evening,
    /// the Grassland to Forest seam). A save opens every Grassland zone and the Forest with its 7 zones, as the gate will once the
    /// Forest's species come; the camera snaps to each zone. Season forced to Soleil (the base look). Writes
    /// TestResults/Captures/T50_*.png. Explicit: run by name.
    /// </summary>
    public class T50CapturePlayTests
    {
        private static readonly (string name, float x)[] Zones =
        {
            ("Seam", 104f), ("Entry", 112f), ("DeepForest", 126f), ("Clearing", 140f), ("Hollow", 154f), ("Brook", 168f),
            ("MushroomCorner", 182f), ("LakeTransition", 196f),
        };

        private string _saveDir;
        private FakeGameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            _clock = new FakeGameClock(new DateTimeOffset(2027, 6, 14, 12, 0, 0, TimeSpan.Zero));
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

        [UnityTest, Explicit]
        public IEnumerator Capture_Forest_Magnifique() => CaptureTier(QualityTier.High, full: true);

        [UnityTest, Explicit]
        public IEnumerator Capture_Forest_Equilibree() => CaptureTier(QualityTier.Medium, full: false);

        [UnityTest, Explicit]
        public IEnumerator Capture_Forest_Fluide() => CaptureTier(QualityTier.Low, full: false);

        private IEnumerator CaptureTier(QualityTier tier, bool full)
        {
            PrepareSave();
            yield return Boot(tier);
            var entry = Entry;
            Assert.IsNotNull(entry);
            yield return ClearPopups(entry.Refuge);
            Assert.GreaterOrEqual(entry.BiomeCount, 2, "The Forest joins the strip (DEF_World_Forest with its 7 chunk prefabs).");
            entry.Seasons?.ForceSeason(RefugeSeason.Sun);
            entry.Atmosphere.ForceHour(13f);
            string prefix = "T50_" + tier + "_";
            if (full)
            {
                foreach (var zone in Zones)
                {
                    yield return Snap(entry, zone.x, 4.5f);
                    yield return Shot(prefix + zone.name + "_Day");
                }
                yield return Snap(entry, 140f, 4.5f);
                // The clock has no night (evening from 18:30 to 5:00; the night mood is for events only).
                foreach (var (hour, label) in new[] { (8f, "Morning"), (19.6f, "Evening") })
                {
                    entry.Atmosphere.ForceHour(hour);
                    yield return Wait(3f);
                    yield return Shot(prefix + "Clearing_" + label);
                }
                entry.Atmosphere.ForceHour(13f);
                entry.Seasons?.ForceSeason(RefugeSeason.Harvest);
                yield return Wait(2.5f);
                yield return Shot(prefix + "Clearing_Harvest");
                entry.Seasons?.ForceSeason(RefugeSeason.Sun);
            }
            else
            {
                foreach (var zone in new[] { ("Clearing", 140f), ("DeepForest", 126f), ("Seam", 104f) })
                {
                    yield return Snap(entry, zone.Item2, 4.5f);
                    yield return Shot(prefix + zone.Item1 + "_Day");
                }
            }
        }

        private static IEnumerator Snap(VerticalSliceEntry entry, float x, float settle)
        {
            var rig = entry.CameraRig;
            rig.SnapTo(new Vector3(x, 0f, rig.Focus.z));
            yield return Wait(settle);
        }

        private void PrepareSave()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(), _clock,
                new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            string[] animals = { "rabbit.brown", "squirrel.red", "hedgehog.brown", "fawn.chestnutspots" };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource("species." + animals[i].Substring(0, animals[i].IndexOf('.')));
                var variant = ContentId.FromSource("variant." + animals[i]);
                state.Collection.Add(new AnimalInstance { Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = (uint)i + 50u });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            state.Wallet.Light = BigValue.FromMantissaExponent(1.0, 12);
            var grassland = ContentId.FromSource("biome.grassland");
            var forest = ContentId.FromSource("biome.forest");
            state.World.Biomes.RemoveAll(b => b.Biome == grassland || b.Biome == forest);
            state.World.Biomes.Add(new BiomeProgress { Biome = grassland, Unlocked = true, Expansions = 6, ZonesOpened = 0xFFu });
            state.World.Biomes.Add(new BiomeProgress { Biome = forest, Unlocked = true, Expansions = 5, ZonesOpened = 0x7Fu });
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private IEnumerator Boot(QualityTier tier)
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
                QualityOverride = tier,
                LoadTargetScene = true,
                MuteAudio = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 60f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return Wait(3f);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        private static VerticalSliceEntry Entry => Object.FindAnyObjectByType<VerticalSliceEntry>();

        /// <summary>Claims the level cards and closes a moment offer, as a player would (the same steps as SeasonPlayTests).</summary>
        private static IEnumerator ClearPopups(RefugeController refuge)
        {
            var presenter = Entry.ProgressionScreens;
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
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
