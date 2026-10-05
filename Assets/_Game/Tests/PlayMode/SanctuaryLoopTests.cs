using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T17 first playable loop through the real bootstrap, scene and HUD: free tutorial egg, the rabbit
    /// produces Light, paid egg at the doc price, the save keeps both animals and the next price.
    /// </summary>
    public class SanctuaryLoopTests
    {
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");
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

#if UNITY_EDITOR
        private IEnumerator Boot(Action<GameBootstrap> onReady)
        {
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LoadTargetScene = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 20f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return null;
            onReady(bootstrap);
        }

        private static IEnumerator WaitHatchDone(SanctuaryController sanctuary)
        {
            float timeout = Time.realtimeSinceStartup + 8f;
            while (sanctuary.Hatch.IsPlaying || sanctuary.WaitingEgg == null)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Hatch did not finish or the next egg did not appear.");
                yield return null;
            }
        }
#endif

        [UnityTest]
        public IEnumerator FreeEgg_ThenPaidEgg_SurvivesARestart()
        {
#if UNITY_EDITOR
            GameBootstrap first = null;
            yield return Boot(b => first = b);
            var context = first.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var sanctuary = entry.Sanctuary;
            Assert.IsNotNull(sanctuary, "The vertical slice must start the sanctuary loop.");
            var hud = Object.FindAnyObjectByType<SanctuaryHud>();
            Assert.IsNotNull(hud);
            Assert.IsTrue(hud.IsBound);
            Assert.AreEqual(0, sanctuary.VisibleAnimals);
            Assert.IsNotNull(sanctuary.WaitingEgg, "The tutorial egg waits from the first frame.");
            yield return null;
            Assert.IsFalse(hud.CostVisible, "The free egg shows no price.");
            Assert.AreEqual(1, hud.VisualState, "The free egg is ready to touch.");

            // First touch: through the HUD button, like a player.
            var button = hud.GetComponentInChildren<Button>();
            button.onClick.Invoke();
            Assert.AreEqual(1, context.State.Collection.Count);
            Assert.IsTrue(sanctuary.Hatch.IsPlaying);
            yield return WaitHatchDone(sanctuary);
            Assert.AreEqual(1, sanctuary.VisibleAnimals);
            Assert.Greater(context.Economy.GetLightPerSecond().ToDoubleClamped(), 0.0);

            // Not enough Light yet: nothing is bought.
            yield return null;
            Assert.IsTrue(hud.CostVisible);
            Assert.AreEqual(10.0, sanctuary.EggCost.ToDoubleClamped());
            if (!context.Hatch.CanHatch(Basic))
            {
                Assert.AreEqual(0, hud.VisualState);
                button.onClick.Invoke();
                Assert.AreEqual(1, context.State.Collection.Count);
                Assert.IsFalse(sanctuary.Hatch.IsPlaying);
            }

            // The first rabbit produces the Light (production path, no grant), then the second egg is
            // bought at the doc price.
            double rate = context.Economy.GetLightPerSecond().ToDoubleClamped();
            double missing = 10.0 - context.Economy.Light.ToDoubleClamped();
            context.Economy.Advance(missing / rate + 0.05);
            Assert.GreaterOrEqual(context.Economy.Light.ToDoubleClamped(), 10.0);
            yield return null;
            Assert.AreEqual(1, hud.VisualState);
            double before = context.Economy.Light.ToDoubleClamped();
            button.onClick.Invoke();
            Assert.AreEqual(2, context.State.Collection.Count);
            Assert.AreEqual(before - 10.0, context.Economy.Light.ToDoubleClamped(), 1e-6);
            Assert.AreEqual(12.0, context.Hatch.GetCost(Basic).ToDoubleClamped());
            button.onClick.Invoke(); // a second touch speeds the reveal up
            yield return WaitHatchDone(sanctuary);
            Assert.AreEqual(2, sanctuary.VisibleAnimals);
            Assert.AreEqual(12.0, sanctuary.EggCost.ToDoubleClamped());

            Assert.IsTrue(context.Save.FlushNow());
            Object.Destroy(first.gameObject);
            yield return null;

            GameBootstrap second = null;
            yield return Boot(b => second = b);
            var reloaded = Object.FindAnyObjectByType<VerticalSliceEntry>().Sanctuary;
            Assert.AreEqual(2, second.Context.State.Collection.Count);
            Assert.AreEqual(2, reloaded.VisibleAnimals, "Saved animals are back in the meadow.");
            Assert.AreEqual(12.0, reloaded.EggCost.ToDoubleClamped(), "The price curve survives a restart.");
            Assert.IsFalse(second.Context.Hatch.IsNextHatchFree);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
