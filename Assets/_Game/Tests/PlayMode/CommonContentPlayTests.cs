using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Presentation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T34C in the real boot: the animal catalog and the hatch staging come from the Addressables group CommonAnimals
    /// (no override), the warmup runs the built-in set and the groups' sets, and the refuge hatches with them.
    /// </summary>
    public class CommonContentPlayTests
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

        [UnityTest]
        public IEnumerator Boot_LoadsTheSharedContent_FromAddressables()
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                LocalizationOverride = database.localization.ToArray(),
                LoadTargetScene = true,
                MuteAudio = true,
            };
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/SC_Bootstrap.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            float timeout = Time.realtimeSinceStartup + 30f;
            GameBootstrap bootstrap = null;
            while (bootstrap == null || bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);

            var catalog = bootstrap.Context.VisualCatalog;
            Assert.IsNotNull(catalog, "The animal catalog came from the CommonAnimals group.");
            Assert.IsTrue(catalog.TryGet("PF_Animal_Rabbit", out var rabbit) && rabbit.prefab != null, "With its prefabs.");
            Assert.That(ShaderWarmup.LastSource, Does.StartWith("states").Or.StartWith("variants"), "Built-in and group warmups ran.");

            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            Assert.IsNotNull(entry.HatchConfig, "The hatch staging came from the CommonAnimals group.");
            Assert.IsNotNull(entry.Refuge, "The refuge started with them.");
            var refuge = entry.Refuge;
            bootstrap.Context.State.Wallet.Light = PocketWilds.Core.BigValue.FromDouble(1e9);
            refuge.RequestHatch();
            float until = Time.realtimeSinceStartup + 8f;
            while ((refuge.Hatch.IsPlaying || refuge.WaitingEgg == null) && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.AreEqual(1, bootstrap.Context.State.Collection.Count, "A hatch works with the shared content.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
