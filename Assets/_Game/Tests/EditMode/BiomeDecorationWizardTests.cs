using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Editor.Cosmetics;
using PocketWilds.Editor.Production;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T49B ED-004 Biome Wizard and ED-002 Decoration Wizard: the Prairie is complete through the wizard (zones in the legacy
    /// order, every asset adopted, validators green); every decoration is measured, inside its class, addressed and in sync with
    /// its CosmeticVisualCatalog copy; the footprint classes follow doc 05; and a scratch biome made in a temporary root is
    /// created, reported, validated, then removed without a trace (assets, groups, label, database entries, texts).
    /// </summary>
    public class BiomeDecorationWizardTests
    {
        private const string ScratchRoot = "Assets/_Scratch_BiomeWizard";

        private static string Describe(ContentWizard.Row row) =>
            row.Label + ": " + string.Join(" | ", row.Checks.Where(c => !c.Passed).Select(c => c.Name + " (" + c.Detail + ")"));

        [Test]
        public void EveryBiome_IsComplete()
        {
            var rows = BiomeWizard.BiomeReport();
            var grassland = rows.FirstOrDefault(r => r.Id == "biome.grassland");
            Assert.IsNotNull(grassland, "DEF_Biome_Grassland is not registered: run BiomeWizard.MigrateGrassland.");
            Assert.IsTrue(grassland.Complete, Describe(grassland));
            var errors = AssetValidator.ValidateBiomes().Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Grassland_ZonesKeepTheLegacyOrder()
        {
            var asset = ContentWizard.Database().biomes.FirstOrDefault(b => b != null && b.sourceId == "biome.grassland");
            Assert.IsNotNull(asset, "DEF_Biome_Grassland missing.");
            var runtime = asset.ToRuntime();
            Assert.AreEqual("grassland", runtime.Key);
            Assert.AreEqual("GL", runtime.Prefix);
            var expected = new[] { "start", "meadow", "streambend", "orchard", "hill", "windmillrise", "pondedge", "forestedge" };
            CollectionAssert.AreEqual(expected, runtime.Zones.Select(z => z.Chunk).ToArray(), "A zone's save bit is its position.");
            Assert.AreEqual(BiomeZoneRole.Arrival, runtime.Zones[0].Role);
            Assert.AreEqual(BiomeZoneRole.Passage, runtime.Zones[7].Role);
            for (int i = 1; i <= 6; i++)
            {
                Assert.AreEqual(BiomeZoneRole.Expansion, runtime.Zones[i].Role, expected[i]);
                Assert.AreEqual(i, runtime.Zones[i].ExpansionIndex, expected[i]);
            }
        }

        [Test]
        public void ForestSpec_IsWritable()
        {
            var forest = BiomeWizard.ForestSpec();
            Assert.IsEmpty(BiomeWizard.SpecProblems(forest));
            Assert.AreEqual(7, forest.Zones.Count);
            Assert.AreEqual("entry", forest.Zones[0].Chunk);
            Assert.AreEqual(BiomeZoneRole.Passage, forest.Zones[6].Role);
            Assert.AreEqual("audio.forest", BiomeWizard.AudioAddress(forest.Key));
            Assert.AreEqual("egg.forest.basic", forest.BasicEggId);
            Assert.IsEmpty(BiomeWizard.SpecProblems(BiomeWizard.GrasslandSpec()));
        }

        [Test]
        public void EveryDecoration_IsComplete()
        {
            var rows = DecorationWizard.DecorationReport();
            CollectionAssert.IsSubsetOf(DecorationWizard.AllSpecs().Select(s => s.SourceId).ToArray(), rows.Select(r => r.Id).ToArray(),
                "Run DecorationWizard.UpdateAll.");
            var problems = rows.Where(r => !r.Complete).Select(Describe).ToList();
            Assert.IsEmpty(problems, string.Join("\n", problems));
            var errors = AssetValidator.ValidateDecorations().Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void DecorEntry_MatchesItsDefinition()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(AccessoryVisualPipeline.CatalogPath);
            Assert.IsNotNull(catalog, "DEF_CosmeticVisualCatalog missing.");
            var definitions = ContentWizard.Database().decorations.Where(d => d != null).ToDictionary(d => d.sourceId);
            var problems = new List<string>();
            foreach (var spec in DecorationWizard.AllSpecs())
            {
                if (!definitions.TryGetValue(spec.SourceId, out var definition))
                {
                    problems.Add(spec.SourceId + ": no definition");
                    continue;
                }
                if (spec.SyncCatalog)
                {
                    var entry = catalog.FindDecor(spec.SourceId);
                    if (entry == null)
                        problems.Add(spec.SourceId + ": no DecorEntry");
                    else if (!DecorationWizard.Matches(entry, definition, out string why))
                        problems.Add($"{spec.SourceId}: DecorEntry differs ({why})");
                }
                else if (catalog.burrowItem == spec.SourceId)
                {
                    if (catalog.burrowPalette != definition.palette || AssetDatabase.GetAssetPath(catalog.burrowPrefab) != definition.prefabPath)
                        problems.Add($"{spec.SourceId}: the catalog's burrow says {catalog.burrowPalette} / {AssetDatabase.GetAssetPath(catalog.burrowPrefab)}");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void FootprintClasses_FollowTheDoc()
        {
            Vector2[] Box(float halfX, float halfZ) =>
                new[] { new Vector2(-halfX, -halfZ), new Vector2(halfX, -halfZ), new Vector2(halfX, halfZ), new Vector2(-halfX, halfZ) };

            Assert.IsTrue(DecorationWizard.Fits(new[] { new Vector2(0.2015f, 0f) }, DecorationFootprintClass.XS, DecorationFootprintShape.Circle), "2 mm tolerance.");
            Assert.IsFalse(DecorationWizard.Fits(new[] { new Vector2(0.203f, 0f) }, DecorationFootprintClass.XS, DecorationFootprintShape.Circle));
            Assert.IsTrue(DecorationWizard.Fits(Box(0.55f, 0.25f), DecorationFootprintClass.M, DecorationFootprintShape.Rectangle));
            Assert.IsTrue(DecorationWizard.Fits(Box(0.25f, 0.55f), DecorationFootprintClass.M, DecorationFootprintShape.Rectangle), "Turned 90°.");
            Assert.IsFalse(DecorationWizard.Fits(Box(0.25f, 0.65f), DecorationFootprintClass.M, DecorationFootprintShape.Rectangle));

            Assert.IsTrue(DecorationWizard.TryFit(Box(0.15f, 0.15f), null, out var footprint, out var shape));
            Assert.AreEqual(DecorationFootprintClass.S, footprint);
            Assert.AreEqual(DecorationFootprintShape.Circle, shape);
            Assert.IsTrue(DecorationWizard.TryFit(Box(0.6f, 0.1f), DecorationFootprintShape.Rectangle, out footprint, out shape));
            Assert.AreEqual(DecorationFootprintClass.M, footprint);
            Assert.IsFalse(DecorationWizard.TryFit(Box(2f, 2f), null, out _, out _), "Past XL.");
        }

        [Test]
        public void BiomeWizard_RoundTrip_OnAScratchBiome()
        {
            var spec = ScratchSpec();
            var paths = new BiomeWizard.BiomePaths(spec.Name, ScratchRoot);
            Assert.Throws<InvalidOperationException>(() => BiomeWizard.Remove("Grassland", "grassland", BiomeWizard.DefaultRoot),
                "A shipped biome is never removed by a tool.");
            try
            {
                string result = BiomeWizard.CreateOrUpdate(spec, ScratchRoot);
                BiomeWizard.CreateOrUpdate(spec, ScratchRoot);
                var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinitionAsset>(paths.Definition);
                Assert.IsNotNull(biome, result);
                Assert.AreEqual(1, ContentWizard.Database().biomes.Count(b => b != null && b.sourceId == spec.SourceId), "Rerunning never duplicates.");
                Assert.AreEqual(3, biome.zones.Count);

                var set = AssetDatabase.LoadAssetAtPath<WorldChunkSet>(paths.ChunkSet);
                Assert.IsNotNull(set);
                Assert.AreEqual(3, set.Count);
                Assert.AreEqual("chunk.scratchwood.glade", set.Get(1).id);
                Assert.AreEqual(new Vector3(BiomeWizard.ChunkSpacing, 0f, 0f), set.Get(1).origin);
                Assert.IsTrue(set.Get(0).pinned);
                Assert.IsFalse(set.Get(2).pinned);
                foreach (var mood in BiomeWizard.Moods)
                    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<LightingProfile>(paths.Lighting(mood)), mood);
                var world = AssetDatabase.LoadAssetAtPath<BiomeWorldProfile>(paths.World);
                Assert.IsNotNull(world);
                Assert.AreSame(set, world.chunkSet);

                var row = BiomeWizard.BiomeRow(biome);
                var waiting = new[] { "Prefabs de chunks", "Météo et vie ambiante", "Kit de zones" };
                foreach (var check in row.Checks.Where(c => !waiting.Contains(c.Name)))
                    Assert.IsTrue(check.Passed, check.Name + ": " + check.Detail);
                Assert.IsFalse(row.Checks.First(c => c.Name == "Prefabs de chunks").Passed, "No chunk prefab yet: « à faire ».");
                var errors = AssetValidator.ValidateBiome(biome).Where(i => i.Severity == IssueSeverity.Error).ToList();
                Assert.IsEmpty(errors, string.Join("\n", errors));

                // The zones only grow at the end: a zone slipped before an existing one would move every save bit after it.
                var moved = ScratchSpec();
                moved.Zones.Insert(1, new BiomeWizard.ZoneSpec("pond", "Pond", BiomeZoneRole.Expansion, "the Pond", "la Mare"));
                Assert.Throws<InvalidOperationException>(() => BiomeWizard.CreateOrUpdate(moved, ScratchRoot));
            }
            finally
            {
                BiomeWizard.Remove(spec.Name, spec.Key, ScratchRoot);
            }

            Assert.IsFalse(AssetDatabase.IsValidFolder(ScratchRoot), "Scratch folder removed.");
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            foreach (var group in new[] { paths.ChunksGroup, paths.KitGroup, paths.ZonesGroup, paths.AudioGroup })
                Assert.IsNull(settings.FindGroup(group), group);
            Assert.IsFalse(settings.GetLabels().Contains(BiomeWizard.Label(spec.Key)), "Label removed.");
            var database = ContentWizard.Database();
            Assert.IsFalse(database.biomes.Any(b => b == null || b.sourceId == spec.SourceId), "Biome entry removed.");
            Assert.IsFalse(database.paletteSets.Any(p => p == null), "Palette set entry removed.");
            foreach (var table in database.localization.Where(t => t != null))
                Assert.IsFalse(table.entries.Any(e => e.key == spec.DisplayKey || (e.key ?? string.Empty).StartsWith("zone.scratchwood.", StringComparison.Ordinal)),
                    table.languageCode + " texts removed.");
        }

        private static BiomeWizard.BiomeSpec ScratchSpec()
        {
            var spec = new BiomeWizard.BiomeSpec
            {
                Name = "Scratchwood", Key = "scratchwood", Prefix = "SW", Order = 90, English = "Scratch Wood", French = "Bois d'essai",
                BasicEggId = "egg.scratchwood.basic",
                Mood = new BiomeWizard.MoodSpec { Sun = new Color(1f, 0.82f, 0.55f), Ambient = new Color(0.5f, 0.6f, 0.42f), Sky = Color.white, Fog = Color.white, Strength = 0.3f },
            };
            spec.Zones.Add(new BiomeWizard.ZoneSpec("entry", "Entry", BiomeZoneRole.Arrival, "the Entry", "l'Entrée"));
            spec.Zones.Add(new BiomeWizard.ZoneSpec("glade", "Glade", BiomeZoneRole.Expansion, "the Glade", "la Clairière d'essai"));
            spec.Zones.Add(new BiomeWizard.ZoneSpec("exit", "Exit", BiomeZoneRole.Passage, "the Exit", "la Sortie"));
            spec.Palettes.Add(("palette.scratchwood.tree", new[] { "5E4636", "3F7A4A", "5E9A52", "D9A441", "8FB86A", "2A2620", "FFE2A8" }));
            return spec;
        }
    }
}
