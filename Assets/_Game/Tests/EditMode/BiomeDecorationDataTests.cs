using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T49B data rules of the Biome Wizard (ED-004) and the Decoration Wizard (ED-002): zone rules of a biome, the Grassland zones from
    /// data equal to the legacy table (saves unchanged), a biome appended after it keeps its bits, and the decoration checks
    /// (measured, outline inside its class within 2 mm, palette and biome exist).
    /// </summary>
    public class BiomeDecorationDataTests
    {
        private static BiomeZone[] GrasslandZones() => new[]
        {
            new BiomeZone("start", BiomeZoneRole.Arrival, 0),
            new BiomeZone("meadow", BiomeZoneRole.Expansion, 1),
            new BiomeZone("streambend", BiomeZoneRole.Expansion, 2),
            new BiomeZone("orchard", BiomeZoneRole.Expansion, 3),
            new BiomeZone("hill", BiomeZoneRole.Expansion, 4),
            new BiomeZone("windmillrise", BiomeZoneRole.Expansion, 5),
            new BiomeZone("pondedge", BiomeZoneRole.Expansion, 6),
            new BiomeZone("forestedge", BiomeZoneRole.Passage, 0),
        };

        private static BiomeZone[] ForestZones() => new[]
        {
            new BiomeZone("entry", BiomeZoneRole.Arrival, 0),
            new BiomeZone("deepforest", BiomeZoneRole.Expansion, 1),
            new BiomeZone("clearing", BiomeZoneRole.Expansion, 2),
            new BiomeZone("hollow", BiomeZoneRole.Expansion, 3),
            new BiomeZone("brook", BiomeZoneRole.Expansion, 4),
            new BiomeZone("mushroomcorner", BiomeZoneRole.Expansion, 5),
            new BiomeZone("laketransition", BiomeZoneRole.Passage, 0),
        };

        private static BiomeDefinition Biome(string key, string prefix, int order, params BiomeZone[] zones) =>
            new BiomeDefinition(ContentId.FromSource("biome." + key), "biome." + key + ".name", order, key, prefix, zones);

        private static int Errors(ContentRegistryBuilder builder) =>
            builder.Issues.Count(i => i.Severity == ContentIssueSeverity.Error);

        private static List<BiomeDefinition> OneBiome(string key, string prefix, params BiomeZone[] zones) =>
            new List<BiomeDefinition> { Biome(key, prefix, 0, zones) };

        // ---- Biome zone rules ----

        [Test]
        public void Biome_ValidZones_Pass()
        {
            var builder = new ContentRegistryBuilder();
            builder.AddBiome(Biome("grassland", "GL", 0, GrasslandZones()), "biome.grassland");
            builder.AddBiome(Biome("forest", "FO", 1, ForestZones()), "biome.forest");
            Assert.AreEqual(0, Errors(builder), string.Join("; ", builder.Issues));
        }

        [Test]
        public void Biome_WithoutKeyOrZones_StaysLegacyValid()
        {
            var builder = new ContentRegistryBuilder();
            builder.AddBiome(new BiomeDefinition(ContentId.FromSource("biome.grassland"), "biome.grassland.name", 0), "biome.grassland");
            Assert.AreEqual(0, Errors(builder));
        }

        [Test]
        public void Biome_KeyMustMatchSourceId()
        {
            var builder = new ContentRegistryBuilder();
            builder.AddBiome(Biome("forest", "FO", 1, ForestZones()), "biome.woods");
            Assert.AreEqual(1, Errors(builder));
        }

        [TestCase("")]
        [TestCase("F")]
        [TestCase("fo")]
        [TestCase("FOR")]
        [TestCase("F1")]
        public void Biome_PrefixIsTwoCapitals(string prefix)
        {
            var builder = new ContentRegistryBuilder();
            builder.AddBiome(Biome("forest", prefix, 1, ForestZones()), "biome.forest");
            Assert.AreEqual(1, Errors(builder));
        }

        [Test]
        public void Biome_ExactlyOneArrival_AndItIsFirst()
        {
            var noArrival = new[] { new BiomeZone("a", BiomeZoneRole.Expansion, 1), new BiomeZone("b", BiomeZoneRole.Expansion, 2) };
            var twoArrivals = new[] { new BiomeZone("a", BiomeZoneRole.Arrival, 0), new BiomeZone("b", BiomeZoneRole.Arrival, 0) };
            var arrivalSecond = new[] { new BiomeZone("a", BiomeZoneRole.Expansion, 1), new BiomeZone("b", BiomeZoneRole.Arrival, 0) };
            foreach (var zones in new[] { noArrival, twoArrivals, arrivalSecond })
            {
                var builder = new ContentRegistryBuilder();
                builder.AddBiome(Biome("forest", "FO", 1, zones), "biome.forest");
                Assert.AreEqual(1, Errors(builder));
            }
        }

        [Test]
        public void Biome_AtMostOnePassage_AndItIsLast()
        {
            var twoPassages = new[]
            {
                new BiomeZone("a", BiomeZoneRole.Arrival, 0), new BiomeZone("b", BiomeZoneRole.Passage, 0), new BiomeZone("c", BiomeZoneRole.Passage, 0),
            };
            var passageInMiddle = new[]
            {
                new BiomeZone("a", BiomeZoneRole.Arrival, 0), new BiomeZone("b", BiomeZoneRole.Passage, 0), new BiomeZone("c", BiomeZoneRole.Expansion, 1),
            };
            foreach (var zones in new[] { twoPassages, passageInMiddle })
            {
                var builder = new ContentRegistryBuilder();
                builder.AddBiome(Biome("forest", "FO", 1, zones), "biome.forest");
                Assert.AreEqual(1, Errors(builder));
            }
        }

        [Test]
        public void Biome_ChunkNames_AreUniqueLowerCaseAndDigits()
        {
            foreach (var bad in new[] { "meadow", "Meadow", "mea_dow", "mea dow", "" })
            {
                var zones = new[]
                {
                    new BiomeZone("start", BiomeZoneRole.Arrival, 0),
                    new BiomeZone("meadow", BiomeZoneRole.Expansion, 1),
                    new BiomeZone(bad, BiomeZoneRole.Expansion, 2),
                };
                var builder = new ContentRegistryBuilder();
                builder.AddBiome(Biome("forest", "FO", 1, zones), "biome.forest");
                Assert.AreEqual(1, Errors(builder), "'" + bad + "'");
            }
            var digits = new[] { new BiomeZone("start", BiomeZoneRole.Arrival, 0), new BiomeZone("camp2", BiomeZoneRole.Expansion, 1) };
            var ok = new ContentRegistryBuilder();
            ok.AddBiome(Biome("forest", "FO", 1, digits), "biome.forest");
            Assert.AreEqual(0, Errors(ok));
        }

        [Test]
        public void Biome_ZoneCount_StaysWithinTheSaveBits()
        {
            var zones = new List<BiomeZone> { new BiomeZone("start", BiomeZoneRole.Arrival, 0) };
            for (int i = 1; i < ContentRegistryBuilder.MaxBiomeZones; i++)
                zones.Add(new BiomeZone("z" + i, BiomeZoneRole.Expansion, i));
            var full = new ContentRegistryBuilder();
            full.AddBiome(Biome("forest", "FO", 1, zones.ToArray()), "biome.forest");
            Assert.AreEqual(0, Errors(full), "32 zones fit in the uint.");

            zones.Add(new BiomeZone("z32", BiomeZoneRole.Expansion, 32));
            var over = new ContentRegistryBuilder();
            over.AddBiome(Biome("forest", "FO", 1, zones.ToArray()), "biome.forest");
            Assert.AreEqual(1, Errors(over));
        }

        [Test]
        public void Biome_AuthoringAsset_NumbersTheExpansionsFromOne()
        {
            var asset = ScriptableObject.CreateInstance<BiomeDefinitionAsset>();
            try
            {
                asset.sourceId = "biome.grassland";
                asset.displayKey = "biome.grassland.name";
                asset.key = "grassland";
                asset.prefix = "GL";
                foreach (var z in GrasslandZones())
                    asset.zones.Add(new BiomeDefinitionAsset.ZoneRow { chunk = z.Chunk, role = z.Role });
                var runtime = asset.ToRuntime();
                Assert.AreEqual(8, runtime.Zones.Count);
                for (int i = 0; i < 8; i++)
                    Assert.AreEqual(GrasslandZones()[i].ExpansionIndex, runtime.Zones[i].ExpansionIndex, runtime.Zones[i].Chunk);
                var builder = new ContentRegistryBuilder();
                builder.AddBiome(runtime, asset.sourceId);
                Assert.AreEqual(0, Errors(builder));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        // ---- Zones from data ----

        [Test]
        public void GrasslandZones_FromData_EqualTheLegacyTable()
        {
            var legacy = ProgressionConfig.Default.Zones;
            var fromData = ProgressionConfig.ZonesFrom(OneBiome("grassland", "GL", GrasslandZones()));
            Assert.AreEqual(legacy.Length, fromData.Length);
            for (int i = 0; i < legacy.Length; i++)
            {
                Assert.AreEqual(legacy[i].SourceId, fromData[i].SourceId, "source " + i);
                Assert.AreEqual(legacy[i].Id, fromData[i].Id, "id " + i);
                Assert.AreEqual(legacy[i].Biome, fromData[i].Biome, "biome " + i);
                Assert.AreEqual(legacy[i].Role, fromData[i].Role, "role " + i);
                Assert.AreEqual(legacy[i].Index, fromData[i].Index, "bit " + i);
                Assert.AreEqual(legacy[i].ExpansionIndex, fromData[i].ExpansionIndex, "expansion " + i);
                Assert.AreEqual(legacy[i].ChunkAddress, fromData[i].ChunkAddress, "chunk " + i);
                Assert.AreEqual(legacy[i].DisplayKey, fromData[i].DisplayKey, "display key " + i);
            }
        }

        [Test]
        public void ForestAppendedAfterGrassland_KeepsTheGrasslandBits()
        {
            var builder = new ContentRegistryBuilder();
            builder.AddBiome(Biome("forest", "FO", 1, ForestZones()), "biome.forest");
            builder.AddBiome(Biome("grassland", "GL", 0, GrasslandZones()), "biome.grassland");
            var registry = builder.Build();
            var config = ProgressionConfig.ForContent(registry);
            var legacy = ProgressionConfig.Default.Zones;

            Assert.AreEqual(legacy.Length + 7, config.Zones.Length);
            for (int i = 0; i < legacy.Length; i++)
            {
                Assert.AreEqual(legacy[i].Id, config.Zones[i].Id, "grassland zone " + i + " (order sorts the biomes)");
                Assert.AreEqual(legacy[i].Index, config.Zones[i].Index);
            }
            var forest = config.Zones.Where(z => z.Biome == ContentId.FromSource("biome.forest")).ToArray();
            Assert.AreEqual(7, forest.Length);
            for (int i = 0; i < forest.Length; i++)
                Assert.AreEqual(i, forest[i].Index, "the forest counts its own bits from 0");
            Assert.AreEqual("chunk.forest.entry", forest[0].ChunkAddress);
            Assert.AreEqual(ZoneRole.Passage, forest[6].Role);
            Assert.AreEqual(5, forest[5].ExpansionIndex);
        }

        [Test]
        public void WithoutZonesInTheRegistry_TheDefaultTableStays()
        {
            Assert.AreSame(ProgressionConfig.Default, ProgressionConfig.ForContent(ContentRegistry.Empty));
            Assert.AreSame(ProgressionConfig.Default, ProgressionConfig.ForContent(null));
            var builder = new ContentRegistryBuilder();
            builder.AddBiome(new BiomeDefinition(ContentId.FromSource("biome.grassland"), "biome.grassland.name", 0), "biome.grassland");
            Assert.AreSame(ProgressionConfig.Default, ProgressionConfig.ForContent(builder.Build()));
        }

        [Test]
        public void ForContent_NeverModifiesTheSharedDefault()
        {
            int before = ProgressionConfig.Default.Zones.Length;
            var builder = new ContentRegistryBuilder();
            builder.AddBiome(Biome("forest", "FO", 1, ForestZones()), "biome.forest");
            var config = ProgressionConfig.ForContent(builder.Build());
            Assert.AreNotSame(ProgressionConfig.Default, config);
            Assert.AreEqual(before, ProgressionConfig.Default.Zones.Length);
        }

        // ---- Decorations ----

        private static DecorationDefinition Decor(string source, DecorationFootprintClass footprint, DecorationFootprintShape shape,
            float[] hull, float height = 0.5f, bool bands = true, string biome = "", string palette = "", int maxCopies = 1)
        {
            var bandList = bands ? new[] { new DecorationBand(0f, 0f, 0f, 0f, 0.4f, 0f, 0.1f, 0f, 0.7f) } : null;
            return new DecorationDefinition(ContentId.FromSource(source), source, source + ".name", DecorationCategory.Nature, footprint, shape,
                DecorationInteraction.Inspect, string.IsNullOrEmpty(biome) ? default : ContentId.FromSource(biome), "PF_Decor_GL_Test",
                string.IsNullOrEmpty(palette) ? default : ContentId.FromSource(palette), "ICO_Decor_GL_Test", "shop.common", maxCopies,
                bandList, hull, 0.3f, height, 0f);
        }

        private static float[] Square(float half) => new[] { -half, -half, half, -half, half, half, -half, half };

        private static int AddDecor(DecorationDefinition d, out ContentRegistryBuilder builder)
        {
            builder = new ContentRegistryBuilder();
            builder.AddDecoration(d, d.SourceId);
            return Errors(builder);
        }

        [Test]
        public void Decoration_Valid_IsRegistered()
        {
            var builder = new ContentRegistryBuilder();
            var d = Decor("decor.grassland.daisy_tuft", DecorationFootprintClass.XS, DecorationFootprintShape.Circle, Square(0.14f));
            builder.AddDecoration(d, d.SourceId);
            Assert.AreEqual(0, Errors(builder));
            var registry = builder.Build();
            Assert.AreEqual(1, registry.Decorations.Count);
            Assert.IsTrue(registry.Decorations.TryGet(d.Id, out var found));
            Assert.AreEqual(DecorationFootprintClass.XS, found.Footprint);
        }

        [Test]
        public void Decoration_Duplicate_IsRefused()
        {
            var builder = new ContentRegistryBuilder();
            var d = Decor("decor.grassland.daisy_tuft", DecorationFootprintClass.XS, DecorationFootprintShape.Circle, Square(0.1f));
            builder.AddDecoration(d, d.SourceId);
            builder.AddDecoration(d, d.SourceId);
            Assert.AreEqual(1, Errors(builder));
        }

        [Test]
        public void Decoration_NeedsDisplayKeyAndPrefabAddress_AndCopies()
        {
            var noKey = new DecorationDefinition(ContentId.FromSource("decor.a"), "decor.a", "", DecorationCategory.Nature, DecorationFootprintClass.XS,
                DecorationFootprintShape.Circle, DecorationInteraction.None, default, "PF_Decor_GL_A", default, "ICO", "", 1,
                null, Square(0.1f), 0.1f, 0.01f, 0f);
            Assert.AreEqual(1, AddDecor(noKey, out _));
            var noPrefab = new DecorationDefinition(ContentId.FromSource("decor.b"), "decor.b", "decor.b.name", DecorationCategory.Nature,
                DecorationFootprintClass.XS, DecorationFootprintShape.Circle, DecorationInteraction.None, default, "", default, "ICO", "", 1,
                null, Square(0.1f), 0.1f, 0.01f, 0f);
            Assert.AreEqual(1, AddDecor(noPrefab, out _));
            Assert.AreEqual(1, AddDecor(Decor("decor.c", DecorationFootprintClass.XS, DecorationFootprintShape.Circle, Square(0.1f), maxCopies: 0), out _));
        }

        [Test]
        public void Decoration_NotMeasured_IsRefused_UnlessFlatWithAnOutline()
        {
            Assert.AreEqual(1, AddDecor(Decor("decor.nobands", DecorationFootprintClass.S, DecorationFootprintShape.Circle, Square(0.2f), bands: false), out _),
                "a standing decoration without bands is not measured");
            Assert.AreEqual(1, AddDecor(Decor("decor.nohull", DecorationFootprintClass.S, DecorationFootprintShape.Circle, new float[0]), out _),
                "no outline at all");
            Assert.AreEqual(1, AddDecor(Decor("decor.twopoints", DecorationFootprintClass.S, DecorationFootprintShape.Circle, new[] { 0f, 0f, 0.1f, 0.1f }), out _));
            var flat = Decor("decor.rug", DecorationFootprintClass.M, DecorationFootprintShape.Rectangle, Square(0.3f), height: 0.02f, bands: false);
            Assert.IsTrue(flat.IsFlat);
            Assert.AreEqual(0, AddDecor(flat, out _), "flat decorations keep their outline and walk like the ground");
        }

        [Test]
        public void Decoration_Circle_OutlineWithinRadiusPlusTwoMillimetres()
        {
            // XS radius 0.20 m: a square of half 0.14 has corners at 0.198; half 0.1415 reaches 0.2001 (inside the 2 mm tolerance).
            Assert.AreEqual(0, AddDecor(Decor("decor.in", DecorationFootprintClass.XS, DecorationFootprintShape.Circle, Square(0.1415f)), out _));
            Assert.AreEqual(1, AddDecor(Decor("decor.out", DecorationFootprintClass.XS, DecorationFootprintShape.Circle, Square(0.15f)), out _));
            Assert.AreEqual(0, AddDecor(Decor("decor.edge", DecorationFootprintClass.XS, DecorationFootprintShape.Circle, new[] { 0.2019f, 0f, 0f, 0.1f, -0.1f, -0.1f }), out _));
            Assert.AreEqual(1, AddDecor(Decor("decor.past", DecorationFootprintClass.XS, DecorationFootprintShape.Circle, new[] { 0.2025f, 0f, 0f, 0.1f, -0.1f, -0.1f }), out _));
        }

        [Test]
        public void Decoration_Rectangle_AllowsTheQuarterTurn()
        {
            // M rectangle: half sizes 0.60 x 0.30.
            var along = new[] { -0.6f, -0.3f, 0.6f, -0.3f, 0.6f, 0.3f, -0.6f, 0.3f };
            var turned = new[] { -0.3f, -0.6f, 0.3f, -0.6f, 0.3f, 0.6f, -0.3f, 0.6f };
            var tooLong = new[] { -0.7f, -0.3f, 0.7f, -0.3f, 0.7f, 0.3f, -0.7f, 0.3f };
            var tooWide = new[] { -0.6f, -0.4f, 0.6f, -0.4f, 0.6f, 0.4f, -0.6f, 0.4f };
            Assert.AreEqual(0, AddDecor(Decor("decor.along", DecorationFootprintClass.M, DecorationFootprintShape.Rectangle, along), out _));
            Assert.AreEqual(0, AddDecor(Decor("decor.turned", DecorationFootprintClass.M, DecorationFootprintShape.Rectangle, turned), out _));
            Assert.AreEqual(1, AddDecor(Decor("decor.long", DecorationFootprintClass.M, DecorationFootprintShape.Rectangle, tooLong), out _));
            Assert.AreEqual(1, AddDecor(Decor("decor.wide", DecorationFootprintClass.M, DecorationFootprintShape.Rectangle, tooWide), out _));
        }

        [Test]
        public void Decoration_PaletteAndBiome_MustExistWhenSet()
        {
            var d = Decor("decor.grassland.fern", DecorationFootprintClass.S, DecorationFootprintShape.Circle, Square(0.2f),
                biome: "biome.grassland", palette: "palette.grassland.decor_fern");

            var missing = new ContentRegistryBuilder();
            missing.AddDecoration(d, d.SourceId);
            Assert.Throws<System.InvalidOperationException>(() => missing.Build(), "unknown palette and biome");

            var ok = new ContentRegistryBuilder();
            ok.AddBiome(new BiomeDefinition(ContentId.FromSource("biome.grassland"), "biome.grassland.name", 0), "biome.grassland");
            ok.AddPalette(new PaletteDefinition(ContentId.FromSource("palette.grassland.decor_fern"), new Color32[PaletteDefinition.PaletteSize]),
                "palette.grassland.decor_fern");
            ok.AddDecoration(d, d.SourceId);
            Assert.AreEqual(0, Errors(ok), string.Join("; ", ok.Issues));
            Assert.AreEqual(1, ok.Build().Decorations.Count);
        }

        [Test]
        public void Decoration_AuthoringAsset_RoundTripsToTheRuntimeDefinition()
        {
            var asset = ScriptableObject.CreateInstance<DecorationDefinitionAsset>();
            try
            {
                asset.sourceId = "decor.grassland.daisy_tuft";
                asset.displayKey = "decor.grassland.daisy_tuft.name";
                asset.biome = "biome.grassland";
                asset.footprint = DecorationFootprintClass.XS;
                asset.shape = DecorationFootprintShape.Circle;
                asset.prefabAddress = "PF_Decor_GL_DaisyTuft";
                asset.icon = "ICO_Decor_GL_DaisyTuft";
                asset.maxCopies = 10;
                asset.bands.Add(new DecorationDefinitionAsset.Band { a = new Vector3(0, 0, 0), b = new Vector3(0, 0.2f, 0), radius = 0.1f, bottom = 0f, top = 0.7f });
                foreach (var p in new[] { new Vector2(-0.1f, -0.1f), new Vector2(0.1f, -0.1f), new Vector2(0f, 0.1f) })
                    asset.groundHull.Add(p);
                asset.height = 0.3f;
                var runtime = asset.ToRuntime();
                Assert.AreEqual(1, runtime.Bands.Length);
                Assert.AreEqual(6, runtime.GroundHull.Length);
                Assert.AreEqual(10, runtime.MaxCopies);
                var builder = new ContentRegistryBuilder();
                builder.AddDecoration(runtime, asset.sourceId);
                Assert.AreEqual(0, Errors(builder), string.Join("; ", builder.Issues));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
