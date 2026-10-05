using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Capture;
using PocketWilds.Editor.UI;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using UnityEngine.UI;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T19: Codex model (sections, rarity order, discovery, New), virtual list math, assets.</summary>
    public class CodexTests
    {
        private sealed class SaveSpy : ISaveScheduler
        {
            public int Requests;
            public void RequestSave(SaveUrgency urgency) => Requests++;
        }

        private static readonly RarityTier[] Tiers = { RarityTier.Epic, RarityTier.Common, RarityTier.Rare, RarityTier.Common };

        /// <summary>Species s{i} in biomes b{i % biomes}; each with 4 variants listed in a scrambled rarity order.</summary>
        private static ContentRegistry Content(int species, int biomes, bool withBiomes = true)
        {
            var builder = new ContentRegistryBuilder();
            if (withBiomes)
            {
                for (int b = biomes - 1; b >= 0; b--) // registered out of order on purpose
                    builder.AddBiome(new BiomeDefinition(ContentId.FromSource("biome.b" + b), "biome.b" + b + ".name", b), "biome.b" + b);
            }
            for (int i = 0; i < species; i++)
            {
                string id = "species.s" + i;
                builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource(id), id + ".name", RigFamily.SmallQuadruped,
                    ContentId.FromSource("biome.b" + (i % biomes)), 1.0, "PF_Animal_Test"), id);
                for (int v = 0; v < Tiers.Length; v++)
                {
                    string variant = "variant.s" + i + ".v" + v;
                    builder.AddVariant(new VariantDefinition(ContentId.FromSource(variant), ContentId.FromSource(id), variant + ".name",
                        Tiers[v], new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), variant);
                }
            }
            return builder.Build();
        }

        private static void Own(PlayerState state, int species, int variant, int count = 1)
        {
            var speciesId = ContentId.FromSource("species.s" + species);
            var variantId = ContentId.FromSource("variant.s" + species + ".v" + variant);
            for (int i = 0; i < count; i++)
                state.Collection.Add(new AnimalInstance { Species = speciesId, Variant = variantId, Seed = (uint)i });
            state.Collection.DiscoveredSpecies.Add(speciesId);
            state.Collection.DiscoveredVariants.Add(variantId);
        }

        [Test]
        public void Sections_FollowBiomeOrder_AndKeepSpeciesWithoutBiome()
        {
            var registry = Content(7, 3);
            var codex = new CodexService(PlayerState.CreateNew(TestUtil.T0), registry, null);
            Assert.AreEqual(3, codex.SectionCount);
            Assert.AreEqual(7, codex.SpeciesCount);
            for (int s = 0; s < 3; s++)
            {
                var section = codex.GetSection(s);
                Assert.AreEqual(ContentId.FromSource("biome.b" + s), section.Biome, "Progression order, not registration order.");
                Assert.AreEqual("biome.b" + s + ".name", section.DisplayKey);
                for (int k = 0; k < section.Count; k++)
                    Assert.AreEqual(ContentId.FromSource("biome.b" + s), registry.Species.Get(codex.GetSpecies(section.Start + k).Species).Biome);
            }

            var noBiomes = new CodexService(PlayerState.CreateNew(TestUtil.T0), Content(5, 2, withBiomes: false), null);
            Assert.AreEqual(1, noBiomes.SectionCount, "Content without biomes still lists every species.");
            Assert.IsNull(noBiomes.GetSection(0).DisplayKey);
            Assert.AreEqual(5, noBiomes.GetSection(0).Count);
        }

        [Test]
        public void Variants_AreListedByRarity()
        {
            var codex = new CodexService(PlayerState.CreateNew(TestUtil.T0), Content(1, 1), null);
            Assert.AreEqual(4, codex.GetVariantCount(0));
            var rarities = Enumerable.Range(0, 4).Select(i => codex.GetVariant(0, i).Rarity).ToArray();
            CollectionAssert.AreEqual(new[] { RarityTier.Common, RarityTier.Common, RarityTier.Rare, RarityTier.Epic }, rarities);
            Assert.AreEqual(ContentId.FromSource("variant.s0.v1"), codex.GetVariant(0, 0).Variant, "Stable within a tier.");
        }

        [Test]
        public void Discovery_Counts_AndNewBadges_FollowTheCollection()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            var save = new SaveSpy();
            var codex = new CodexService(state, Content(3, 1), save);
            Assert.IsFalse(codex.HasNew);
            Own(state, 1, 1, 5);
            Own(state, 1, 2, 2);
            codex.Refresh();

            int index = codex.IndexOf(ContentId.FromSource("species.s1"));
            var entry = codex.GetSpecies(index);
            Assert.IsTrue(entry.Discovered);
            Assert.IsTrue(entry.IsNew);
            Assert.AreEqual(7, entry.Owned);
            Assert.AreEqual(2, entry.VariantsFound);
            Assert.AreEqual(4, entry.VariantsTotal);
            Assert.AreEqual(1, codex.DiscoveredSpecies);
            Assert.AreEqual(1, codex.GetSection(0).Discovered);
            Assert.IsTrue(codex.HasNew);
            Assert.IsFalse(codex.GetSpecies(codex.IndexOf(ContentId.FromSource("species.s0"))).Discovered);

            codex.MarkSeen(index);
            Assert.AreEqual(1, save.Requests);
            Assert.IsFalse(codex.HasNew);
            Assert.IsFalse(codex.GetSpecies(index).IsNew);
            codex.MarkSeen(index);
            Assert.AreEqual(1, save.Requests, "Nothing new: no save.");

            // A new variant of a known species makes the species "New" again.
            Own(state, 1, 0);
            codex.Refresh();
            Assert.IsTrue(codex.GetSpecies(index).IsNew);
            var epic = codex.GetVariant(index, 3); // rarity order: v1, v3 (Common), v2 (Rare), v0 (Epic)
            Assert.AreEqual(ContentId.FromSource("variant.s1.v0"), epic.Variant);
            Assert.IsTrue(epic.IsNew);
            Assert.IsFalse(codex.GetVariant(index, 0).IsNew, "Already seen.");
        }

        [Test]
        public void Refresh_RecountsOnlyWhenTheCollectionChanged_AndScalesTo50kAnimals()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            state.Collection.EnsureCapacity(50_016);
            var codex = new CodexService(state, Content(30, 5), null);
            for (int i = 0; i < 50_000; i++)
                Own(state, i % 30, i % 4);

            var watch = Stopwatch.StartNew();
            codex.Refresh();
            double first = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            for (int i = 0; i < 1000; i++)
                codex.Refresh();
            double cached = watch.Elapsed.TotalMilliseconds;
            UnityEngine.Debug.Log($"[Benchmark] Codex.Refresh 50k animals: {first:F2} ms, unchanged x1000: {cached:F3} ms");
            Assert.Less(first, 50.0);
            Assert.Less(cached, 5.0, "Unchanged collection: no recount.");

            int total = 0;
            for (int i = 0; i < codex.SpeciesCount; i++)
                total += codex.GetSpecies(i).Owned;
            Assert.AreEqual(50_000, total);
        }

        [Test]
        public void ReadingThousandsOfEntries_DoesNotAllocate()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            var codex = new CodexService(state, Content(5000, 6), null);
            for (int i = 0; i < 5000; i += 7)
                Own(state, i, i % 4);
            codex.Refresh();
            codex.GetSpecies(0);
            Assert.That(() =>
            {
                for (int i = 0; i < codex.SpeciesCount; i++)
                {
                    var entry = codex.GetSpecies(i);
                    if (entry.Discovered)
                        codex.GetVariant(i, 0);
                }
                for (int s = 0; s < codex.SectionCount; s++)
                    codex.GetSection(s);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void VirtualListLayout_FindsVisibleRows_ForThousandsOfRows()
        {
            var layout = new VirtualListLayout();
            layout.Begin(5001);
            for (int r = 0; r < 5001; r++)
                layout.SetHeight(r, r % 10 == 0 ? 100f : 400f);
            Assert.AreEqual(501 * 100f + 4500 * 400f, layout.ContentHeight);
            Assert.AreEqual(0, layout.RowAt(0f));
            Assert.AreEqual(0, layout.RowAt(99f));
            Assert.AreEqual(1, layout.RowAt(100f));
            Assert.AreEqual(5000, layout.RowAt(layout.ContentHeight + 500f), "Clamped to the last row.");

            Assert.IsTrue(layout.GetVisibleRange(layout.Top(2500), 1900f, 1, out int first, out int last));
            Assert.AreEqual(2499, first);
            Assert.LessOrEqual(last - first + 1, 9, "Only what fits on screen plus one row each side.");
            Assert.GreaterOrEqual(layout.Top(last) + layout.Height(last), layout.Top(2500) + 1900f);

            Assert.That(() =>
            {
                for (float y = 0; y < layout.ContentHeight; y += 37f)
                    layout.GetVisibleRange(y, 1900f, 1, out _, out _);
            }, Is.Not.AllocatingGCMemory());

            var empty = new VirtualListLayout();
            empty.Begin(0);
            Assert.IsFalse(empty.GetVisibleRange(0f, 100f, 1, out _, out _));
        }

        [Test]
        public void UiNumberText_WritesCountersWithoutStrings()
        {
            var numbers = new UiNumberText();
            int length = numbers.Write(0, 0);
            Assert.AreEqual("0", new string(numbers.Buffer, 0, length));
            length = numbers.Write(1234567, 0);
            Assert.AreEqual("1234567", new string(numbers.Buffer, 0, length));
            length = numbers.Write(-42, 0);
            Assert.AreEqual("-42", new string(numbers.Buffer, 0, length));
        }

        [Test]
        public void ShippedContent_HasThumbnailsSilhouettesAndText()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(UiPipeline.ContentDatabasePath);
            var catalog = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(AnimalThumbnailPipeline.CatalogPath);
            Assert.IsNotNull(catalog, "Run Pocket Wilds/UI/Render Animal Thumbnails.");
            var english = database.localization.Single(t => t.languageCode == "en");
            var french = database.localization.Single(t => t.languageCode == "fr");
            foreach (var biome in database.biomes)
            {
                Assert.IsTrue(english.TryGet(biome.displayKey, out _), biome.displayKey);
                Assert.IsTrue(french.TryGet(biome.displayKey, out _), biome.displayKey);
            }
            foreach (var species in database.species)
            {
                Assert.IsTrue(catalog.TryGetSilhouette(ContentId.FromSource(species.sourceId), out var silhouette), species.sourceId);
                AssertThumbnail(silhouette);
                Assert.IsTrue(english.TryGet(species.displayKey, out _), species.displayKey);
                Assert.IsTrue(french.TryGet(species.displayKey, out _), species.displayKey);
                foreach (var variant in species.variants)
                {
                    Assert.IsTrue(catalog.TryGetVariant(ContentId.FromSource(variant.sourceId), out var sprite), variant.sourceId);
                    AssertThumbnail(sprite);
                    Assert.IsTrue(english.TryGet(variant.displayKey, out _), variant.displayKey);
                    Assert.IsTrue(french.TryGet(variant.displayKey, out _), variant.displayKey);
                }
            }
        }

        private static void AssertThumbnail(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            StringAssert.StartsWith(AnimalThumbnailPipeline.Folder, path, "Thumbnails stay out of the HUD atlas folders.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.IsTrue(importer.DoesSourceTextureHaveAlpha(), path + " needs transparency.");
            Assert.AreEqual(AnimalThumbnailPipeline.Size, sprite.texture.width);
        }

        [Test]
        public void CodexPrefab_IsVirtualized_AndSitsBetweenHudAndModals()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPipeline.CodexPrefabPath);
            Assert.IsNotNull(prefab);
            int order = prefab.GetComponent<Canvas>().sortingOrder;
            Assert.Greater(order, AssetDatabase.LoadAssetAtPath<GameObject>(UiPipeline.HudPrefabPath).GetComponent<Canvas>().sortingOrder);
            Assert.Less(order, AssetDatabase.LoadAssetAtPath<GameObject>(UiPipeline.OfflineReturnPrefabPath).GetComponent<Canvas>().sortingOrder);
            Assert.IsFalse(prefab.transform.Find("Content").gameObject.activeSelf, "Closed by default.");
            Assert.AreEqual(1, prefab.GetComponentsInChildren<CodexCardRow>(true).Length, "One card row template, rows are pooled.");
            Assert.AreEqual(1, prefab.GetComponentsInChildren<CodexHeaderView>(true).Length);
            Assert.AreEqual(12, prefab.GetComponentsInChildren<CodexVariantSlotView>(true).Length);
            Assert.IsNotNull(prefab.GetComponentInChildren<RectMask2D>(true), "RectMask2D, not a stencil mask.");
            Assert.IsEmpty(prefab.GetComponentsInChildren<LayoutGroup>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<ContentSizeFitter>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Mask>(true));
        }
    }
}
