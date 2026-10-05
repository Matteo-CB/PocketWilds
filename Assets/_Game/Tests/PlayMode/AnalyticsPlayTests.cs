using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Save;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T29 through the bootstrap, first launch of a new player: session start, load time, tutorial start, first hatch,
    /// Codex, then the app goes to the background: one batch reaches the backend (consent given), the funnel is in the
    /// save, and coming back opens session 2.
    /// </summary>
    public class AnalyticsPlayTests
    {
        private string _saveDir;

        private sealed class CapturingTransport : IAnalyticsTransport
        {
            private readonly List<string> _sent = new List<string>();
            public string Name => "capture";
            public bool IsAvailable => true;

            public bool TrySend(byte[] payload, int length)
            {
                lock (_sent)
                    _sent.Add(Encoding.UTF8.GetString(payload, 0, length));
                return true;
            }

            public string[] Sent
            {
                get
                {
                    lock (_sent)
                        return _sent.ToArray();
                }
            }
        }

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

        [UnityTest]
        public IEnumerator FirstSession_SendsTheOnboardingFunnel_WhenTheAppLeaves()
        {
#if UNITY_EDITOR
            var transport = new CapturingTransport();
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LocalizationOverride = database.localization.ToArray(),
                StoreProviderOverride = new SandboxStoreProvider("en", 0),
                AnalyticsConsentOverride = AnalyticsConsent.Granted,
                AnalyticsTransportOverride = transport,
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

            var analytics = bootstrap.Analytics;
            Assert.IsTrue(analytics.SessionActive);
            Assert.AreEqual(1, analytics.Session);
            StringAssert.StartsWith(Path.Combine(_saveDir, "Analytics"), bootstrap.AnalyticsOutbox.Directory, "Tests never write into the real app folder.");

            var sanctuary = Object.FindAnyObjectByType<VerticalSliceEntry>().Sanctuary;
            sanctuary.RequestHatch();
            Assert.AreEqual(1, bootstrap.Context.State.Collection.Count);
            float until = Time.realtimeSinceStartup + 10f;
            while (sanctuary.Hatch.IsPlaying || sanctuary.ModalOpen)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "Hatch sequence never ended.");
                yield return null;
            }
            sanctuary.OpenCodex();
            yield return null;
            Assert.IsTrue(sanctuary.CodexOpen);
            Assert.AreEqual(0, transport.Sent.Length, "Nothing leaves during the first minute.");

            // The app goes to the background.
            bootstrap.SendMessage("OnApplicationPause", true);
            until = Time.realtimeSinceStartup + 5f;
            while (transport.Sent.Length == 0)
            {
                Assert.Less(Time.realtimeSinceStartup, until, "The batch never reached the backend.");
                yield return null;
            }
            Assert.IsFalse(analytics.SessionActive);
            string batch = string.Join("\n", transport.Sent);
            TestContext.WriteLine(batch);
            StringAssert.StartsWith("{\"schema\":1,\"aid\":\"", batch);
            foreach (var id in new[]
                     {
                         AnalyticsEventId.SessionStart, AnalyticsEventId.LoadTime, AnalyticsEventId.Hatch, AnalyticsEventId.ScreenView,
                         AnalyticsEventId.Milestone, AnalyticsEventId.EconomySnapshot, AnalyticsEventId.SessionEnd,
                     })
                StringAssert.Contains("[" + (int)id + ",", batch, id + " is in the batch.");
            StringAssert.Contains("," + (int)FunnelMilestone.TutorialStart + ",", batch);
            Assert.Less(Encoding.UTF8.GetByteCount(batch), 2048, "A first session is a light payload.");

            // The funnel follows the profile: it is in the save written at pause.
            var reloaded = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            reloaded.LoadOrCreate();
            Assert.AreEqual(1, reloaded.State.Analytics.Sessions);
            Assert.IsTrue(reloaded.State.Analytics.Has(FunnelMilestone.TutorialStart));
            Assert.IsTrue(reloaded.State.Analytics.Has(FunnelMilestone.FirstHatch));
            Assert.IsTrue(reloaded.State.Analytics.Has(FunnelMilestone.CodexOpen));
            reloaded.Dispose();

            bootstrap.SendMessage("OnApplicationPause", false);
            Assert.IsTrue(analytics.SessionActive);
            Assert.AreEqual(2, analytics.Session, "Coming back opens a new session.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
