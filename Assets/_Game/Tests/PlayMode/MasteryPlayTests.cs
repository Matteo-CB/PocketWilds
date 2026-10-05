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
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>T20 in the real scene: each hatch shows species progress on the HUD; the Codex sheet agrees.</summary>
    public class MasteryPlayTests
    {
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
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

        [UnityTest]
        public IEnumerator EachHatch_ShowsMastery_AndTheSheetMatches()
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
            yield return null;
            var context = bootstrap.Context;
            var refuge = Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
            var hud = Object.FindAnyObjectByType<RefugeHud>();

            for (int hatch = 0; hatch < 3; hatch++)
            {
                if (hatch > 0)
                    context.Economy.Grant(CurrencyId.Light, context.Hatch.GetCost(ContentId.FromSource("egg.grassland.basic")), EconomyReason.Reward);
                var masteryBefore = new System.Collections.Generic.Dictionary<ContentId, SpeciesMastery>(context.State.Mastery.Species);
                refuge.RequestHatch();
                Assert.AreEqual(hatch + 1, context.State.Collection.Count);
                // Grassland eggs give rabbits and squirrels (T23): follow the species that hatched.
                var species = context.State.Collection[context.State.Collection.Count - 1].Species;
                var before = masteryBefore.TryGetValue(species, out var m) ? m : default;
                refuge.RequestHatch(); // skip the staging: the reveal shows the progress
                yield return null;
                Assert.IsTrue(hud.MasteryVisible, "Hatch " + hatch + " shows species progress.");
                var after = context.State.Mastery.Species[species];
                Assert.IsTrue(after.Level > before.Level || after.Xp > before.Xp, "Progress on every hatch.");

                float until = Time.realtimeSinceStartup + 4f;
                while (hud.MasteryVisible || refuge.WaitingEgg == null || refuge.Hatch.IsPlaying)
                {
                    Assert.Less(Time.realtimeSinceStartup, until, "Toast or next egg stuck.");
                    yield return null;
                }
                Assert.AreEqual(after.Level, hud.MasteryLevelShown);
            }

            // The Codex sheet shows the same level.
            refuge.OpenCodex();
            var screen = Object.FindAnyObjectByType<CodexScreen>();
            screen.OpenSheet(context.Codex.IndexOf(Rabbit));
            Assert.AreEqual(context.State.Mastery.GetLevel(Rabbit), screen.SheetMasteryLevel);
            screen.Close();
            yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
