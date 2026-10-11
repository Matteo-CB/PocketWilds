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
    /// T18 through the real bootstrap and scene: three hours away at cold boot, then one hour in the
    /// background. Each return pays rate x time once, opens "Welcome back", and Collect counts the Light
    /// into the HUD. The wall clock is simulated; nothing waits for real hours.
    /// </summary>
    public class OfflineReturnTests
    {
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");
        private string _saveDir;
        private FakeGameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            _clock = new FakeGameClock(DateTimeOffset.UtcNow);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            try
            {
                TestSaveDir.Release(_saveDir);
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
                LocalizationOverride = database.localization.ToArray(),
                ClockOverride = _clock,
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

        private static IEnumerator Collect(OfflineReturnScreen screen, RefugeController refuge, GameContext context)
        {
            // Wait for the opening animation, then press Collect like a player.
            float timeout = Time.realtimeSinceStartup + 3f;
            while (!screen.IsOpen)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("The return screen did not open.");
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.35f);
            screen.GetComponentInChildren<Button>(true).onClick.Invoke();
            timeout = Time.realtimeSinceStartup + 3f;
            while (screen.IsOpen || refuge.OfflineCollecting)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Collect did not finish.");
                yield return null;
            }
        }
#endif

        [UnityTest]
        public IEnumerator AwayAtBoot_AndInBackground_PaysOnce_AndCountsIntoTheHud()
        {
#if UNITY_EDITOR
            // First session: the tutorial rabbit, then the app closes.
            GameBootstrap first = null;
            yield return Boot(b => first = b);
            Assert.IsTrue(first.Context.Hatch.Hatch(Basic).Succeeded);
            var rate = first.Context.Economy.GetLightPerSecond();
            Assert.Greater(rate.ToDoubleClamped(), 0.0);
            Assert.IsTrue(first.Context.Save.FlushNow());
            var lightAtExit = first.Context.Economy.Light;
            Object.Destroy(first.gameObject);
            yield return null;

            // Three hours later: cold boot.
            _clock.Advance(TimeSpan.FromHours(3));
            GameBootstrap second = null;
            yield return Boot(b => second = b);
            var context = second.Context;
            var report = second.OfflineAtBoot;
            Assert.AreEqual(3 * 3600.0, report.CreditedSeconds, 1.0);
            Assert.IsTrue(BigValue.ApproximatelyEqual(BigValue.Multiply(rate, report.EffectiveSeconds), report.Light, 1e-9));
            Assert.Greater(context.Economy.Light.ToDoubleClamped(), lightAtExit.ToDoubleClamped() + report.Light.ToDoubleClamped() * 0.999);

            var refuge = Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
            var screen = Object.FindAnyObjectByType<OfflineReturnScreen>();
            Assert.IsNotNull(screen);
            yield return null;
            Assert.IsTrue(screen.IsOpen, "Welcome back after three hours.");
            Assert.IsTrue(refuge.OfflineScreenOpen);
            Assert.Less(refuge.DisplayedLight.ToDoubleClamped(), context.Economy.Light.ToDoubleClamped(),
                "The HUD holds the offline Light back until Collect.");
            string expectedTitle = context.Localization.Get(UiTextKeys.OfflineWelcomeBack);
            Assert.AreEqual(expectedTitle, screen.transform.Find("Content/Card/Title").GetComponent<TMPro.TMP_Text>().text);
            StringAssert.StartsWith("+", screen.AmountText);

            // While open, the world and the HUD ignore touches.
            int animals = context.State.Collection.Count;
            refuge.RequestHatch();
            Assert.AreEqual(animals, context.State.Collection.Count);

            yield return Collect(screen, refuge, context);
            Assert.IsFalse(refuge.OfflineScreenOpen);
            Assert.AreEqual(context.Economy.Light, refuge.DisplayedLight, "Everything counted in.");

            // One hour in the background, same process.
            second.SendMessage("OnApplicationPause", true);
            _clock.Advance(TimeSpan.FromHours(1));
            var beforeResume = context.Economy.Light;
            second.SendMessage("OnApplicationPause", false);
            double gained = context.Economy.Light.ToDoubleClamped() - beforeResume.ToDoubleClamped();
            Assert.AreEqual(rate.ToDoubleClamped() * 3600.0, gained, rate.ToDoubleClamped() * 2.0);
            yield return null;
            Assert.IsTrue(screen.IsOpen, "Welcome back after the background hour.");
            Assert.Less(refuge.DisplayedLight.ToDoubleClamped(), context.Economy.Light.ToDoubleClamped());
            yield return Collect(screen, refuge, context);

            // Resuming again right away pays nothing and opens nothing.
            var settled = context.Economy.Light;
            second.SendMessage("OnApplicationPause", true);
            second.SendMessage("OnApplicationPause", false);
            Assert.AreEqual(settled, context.Economy.Light);
            yield return null;
            Assert.IsFalse(screen.IsOpen);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
