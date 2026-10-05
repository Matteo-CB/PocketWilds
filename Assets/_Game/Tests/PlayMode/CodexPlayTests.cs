using System;
using System.Collections;
using System.Diagnostics;
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
    /// T19: the Codex through the real scene (HUD shortcut, badge, sheet, back, world camera paused while
    /// covered), and the criterion: thousands of entries scrolled without thousands of views.
    /// </summary>
    public class CodexPlayTests
    {
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");
        private string _saveDir;
        private GameObject _spawned;

        private sealed class ClosedSpy : ICodexActions
        {
            public int Closed;
            public void CodexClosed() => Closed++;
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
            if (_spawned != null)
                Object.Destroy(_spawned);
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
        public IEnumerator HudShortcut_OpensTheCodex_SheetClearsNew_BackReturnsInstantly()
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
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var refuge = entry.Refuge;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            var screen = Object.FindAnyObjectByType<CodexScreen>();
            Assert.IsNotNull(screen);
            Assert.IsFalse(hud.CodexBadgeVisible, "Nothing found yet.");

            // The first rabbit makes the badge appear.
            Assert.IsTrue(context.Hatch.Hatch(Basic).Succeeded);
            yield return null;
            Assert.IsTrue(hud.CodexBadgeVisible);

            // HUD shortcut, like a player.
            var codexButton = hud.transform.Find("SafeArea/Shortcuts/Codex/Button").GetComponent<Button>();
            codexButton.onClick.Invoke();
            Assert.IsTrue(screen.IsOpen);
            Assert.IsTrue(refuge.CodexOpen);
            Assert.IsFalse(entry.CameraRig.Camera.enabled, "The opaque Codex hides the world: no world rendering.");
            yield return null;

            int rabbit = context.Codex.IndexOf(ContentId.FromSource("species.rabbit"));
            Assert.IsTrue(screen.TryGetVisibleCard(rabbit, out var card));
            Assert.IsTrue(card.Discovered);
            Assert.IsTrue(card.ShowsNew);
            refuge.RequestHatch();
            Assert.AreEqual(1, context.State.Collection.Count, "No hatching behind the Codex.");

            card.GetComponentInChildren<Button>().onClick.Invoke();
            Assert.IsTrue(screen.SheetOpen);
            Assert.AreEqual(rabbit, screen.SheetSpecies);
            Assert.IsFalse(context.Codex.HasNew, "Opening the sheet clears New.");

            var back = screen.transform.Find("Content/SafeArea/Header/Back").GetComponent<Button>();
            back.onClick.Invoke();
            Assert.IsFalse(screen.SheetOpen);
            Assert.IsTrue(screen.TryGetVisibleCard(rabbit, out card));
            Assert.IsFalse(card.ShowsNew);
            back.onClick.Invoke();
            float closeBy = Time.realtimeSinceStartup + 2f;
            while (screen.IsOpen)
            {
                Assert.Less(Time.realtimeSinceStartup, closeBy, "The Codex did not close.");
                yield return null;
            }
            Assert.IsTrue(entry.CameraRig.Camera.enabled, "Back to the refuge at once.");
            yield return null;
            Assert.IsFalse(hud.CodexBadgeVisible);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator FiveThousandSpecies_ScrollWithAFewPooledRows()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/Screens/PF_UI_Codex.prefab");
            _spawned = Object.Instantiate(prefab);
            var screen = _spawned.GetComponent<CodexScreen>();

            // Synthetic content: 5000 species across 6 biomes, one variant each; every 3rd species found.
            var builder = new ContentRegistryBuilder();
            for (int b = 0; b < 6; b++)
                builder.AddBiome(new BiomeDefinition(ContentId.FromSource("biome.b" + b), "biome.b" + b + ".name", b), "biome.b" + b);
            var state = PlayerState.CreateNew(DateTimeOffset.UtcNow);
            for (int i = 0; i < 5000; i++)
            {
                string id = "species.s" + i;
                var speciesId = ContentId.FromSource(id);
                builder.AddSpecies(new SpeciesDefinition(speciesId, id + ".name", RigFamily.SmallQuadruped,
                    ContentId.FromSource("biome.b" + (i % 6)), 1.0, "PF_Animal_Test"), id);
                string variant = "variant.s" + i + ".a";
                builder.AddVariant(new VariantDefinition(ContentId.FromSource(variant), speciesId, variant + ".name", RarityTier.Common,
                    new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), variant);
                if (i % 3 == 0)
                {
                    state.Collection.Add(new AnimalInstance { Species = speciesId, Variant = ContentId.FromSource(variant) });
                    state.Collection.DiscoveredSpecies.Add(speciesId);
                    state.Collection.DiscoveredVariants.Add(ContentId.FromSource(variant));
                }
            }
            var codex = new CodexService(state, builder.Build(), null);
            var spy = new ClosedSpy();

            var watch = Stopwatch.StartNew();
            screen.Open(codex, null, spy);
            double openMs = watch.Elapsed.TotalMilliseconds;
            yield return null;
            Assert.AreEqual(5000 / 3 + 1, codex.DiscoveredSpecies);
            Assert.Greater(screen.RowCount, 1600, "About 1670 rows of 3 cards plus 6 biome headers.");

            // Scroll the whole list in viewport-sized steps, twice. The second pass must reuse every view.
            int maxViews = 0;
            double worstStepMs = 0.0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (float y = 0f; y < screen.ContentHeight; y += 900f)
                {
                    watch.Restart();
                    screen.ScrollTo(y);
                    worstStepMs = Math.Max(worstStepMs, watch.Elapsed.TotalMilliseconds);
                    if (pass == 0)
                        maxViews = screen.CreatedRowViews;
                }
                if (pass == 0)
                    yield return null;
            }
            UnityEngine.Debug.Log($"[Benchmark] Codex 5000 species: open {openMs:F1} ms, {screen.RowCount} rows, " +
                                  $"{screen.CreatedRowViews} row views, worst scroll step {worstStepMs:F2} ms (Editor)");
            Assert.LessOrEqual(screen.CreatedRowViews, 14, "Thousands of entries, a handful of views.");
            Assert.AreEqual(maxViews, screen.CreatedRowViews, "The second pass creates nothing.");

            // The last card of the list is bound to the right species.
            screen.ScrollTo(screen.ContentHeight);
            int last = codex.SpeciesCount - 1;
            Assert.IsTrue(screen.TryGetVisibleCard(last, out var deep));
            Assert.AreEqual(codex.GetSpecies(last).Discovered, deep.Discovered);

            screen.Close();
            float closeBy = Time.realtimeSinceStartup + 2f;
            while (screen.IsOpen)
            {
                screen.Tick(Time.unscaledDeltaTime);
                Assert.Less(Time.realtimeSinceStartup, closeBy);
                yield return null;
            }
            Assert.AreEqual(1, spy.Closed);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
