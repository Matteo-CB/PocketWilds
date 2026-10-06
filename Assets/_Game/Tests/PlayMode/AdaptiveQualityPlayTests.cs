using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T34 in the real refuge: every adaptive quality step reaches the world (life, ground details, weather, sun
    /// shadows and blob shadows, renderer level), and heat shows the HUD note.
    /// </summary>
    public class AdaptiveQualityPlayTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var rabbit = ContentId.FromSource("species.rabbit");
            for (int i = 0; i < 6; i++)
            {
                state.Collection.Add(new AnimalInstance
                {
                    Species = rabbit, Variant = ContentId.FromSource("variant.rabbit.brown"), Rarity = RarityTier.Common,
                    Seed = SeedHash.Mix((uint)i + 1u, 0x0A0A0001u),
                });
            }
            state.Collection.DiscoveredSpecies.Add(rabbit);
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            QualitySettings.SetQualityLevel((int)QualityTier.Medium, true);
            PresentationQuality.Apply(QualityTier.Medium);
            try
            {
                Directory.Delete(_saveDir, true);
            }
            catch (IOException)
            {
            }
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
                QualityOverride = QualityTier.High,
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

        private static WorldQualityProfile.Tier Profile(QualityTier tier) => WorldQualityProfile.Default(tier);

        [UnityTest]
        public IEnumerator EveryStep_ReachesTheWorld_DownAndBackUp()
        {
            yield return Boot();
            var context = Object.FindAnyObjectByType<GameBootstrap>().Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var sun = RenderSettings.sun;
            context.Quality.SetAdaptiveEnabled(false);
            Assert.AreEqual(QualityState.Uniform(QualityTier.High), context.Quality.State);

            void Check(QualityState s)
            {
                string at = s.ToString();
                Assert.AreEqual((int)s.Resolution, QualitySettings.GetQualityLevel(), at);
                Assert.AreEqual(Profile(s.Ground).scatterDensity, entry.Scatter.Density, 1e-4f, at);
                if (entry.Life != null)
                    Assert.AreEqual(Profile(s.Life).ambientLifeDensity, entry.Life.Density, 1e-4f, at);
                if (entry.Weather != null)
                {
                    Assert.AreEqual(Profile(s.Weather).weather, entry.Weather.Intensity, 1e-4f, at);
                    Assert.AreEqual(Profile(s.Weather).sunRays, entry.Weather.SunRays, at);
                }
                bool shadows = s.Shadows != QualityTier.Low;
                if (sun != null)
                    Assert.AreEqual(shadows ? LightShadows.Soft : LightShadows.None, sun.shadows, at);
                Assert.AreEqual(!shadows, PresentationQuality.BlobShadows, at);
                foreach (var view in Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None))
                    if (view.gameObject.activeInHierarchy)
                        Assert.AreEqual(!shadows, view.BlobShadowVisible, at + ": " + view.name);
            }

            var state = QualityState.Uniform(QualityTier.High);
            int steps = 0;
            while (state.StepDown(out _))
            {
                context.Quality.ForceState(state);
                yield return null;
                Check(state);
                steps++;
            }
            while (state.StepUp(QualityTier.High, out _))
            {
                context.Quality.ForceState(state);
                yield return null;
                Check(state);
                steps++;
            }
            Assert.AreEqual(20, steps, "Ten steps down, ten back up.");
        }

        [UnityTest]
        public IEnumerator Heat_LowersThePicture_AndTheHudSaysSo()
        {
            yield return Boot();
            var context = Object.FindAnyObjectByType<GameBootstrap>().Context;
            var hud = Object.FindAnyObjectByType<PocketWilds.UI.RefugeHud>();
            var heat = new ScriptedThermalSource { Level = ThermalLevel.Serious };
            context.Quality.SetThermalSource(heat);
            float until = Time.realtimeSinceStartup + QualityService.ThermalPollSeconds + 2f;
            while (!context.Quality.ThermalNotice && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(context.Quality.ThermalNotice, "Serious heat is read within one poll.");
            Assert.AreEqual(QualityTier.Medium, context.Quality.State.Life, "One step lighter at once.");
            yield return null;
            yield return null;
            if (hud != null)
            {
                Assert.IsTrue(hud.NoticeVisible);
                Assert.AreEqual(context.Localization.Get(QualityService.ThermalNoticeKey), hud.NoticeText);
                Assert.AreNotEqual(QualityService.ThermalNoticeKey, hud.NoticeText, "The note is a real sentence, not a key.");
            }
        }
    }
}
