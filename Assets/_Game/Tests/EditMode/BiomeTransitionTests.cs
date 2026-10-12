using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Editor.Production;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T50 the world strip of several biomes (frozen decisions 1 and 2): biome regions by x and their blend band (x 100 to 110
    /// for the Forest), the camera biome's hysteresis, one streamer over several chunk sets (order, residency, only the world's
    /// first arrival pinned), two mood sets blended without allocating, the sun ray areas, and the Forest's place in the wizard.
    /// </summary>
    public class BiomeTransitionTests
    {
        private sealed class FakeSource : IChunkSource
        {
            public readonly List<string> Requested = new List<string>();

            public IChunkRequest Request(in WorldChunkSet.Entry entry, Transform stagingParent)
            {
                Requested.Add(entry.id);
                return new FakeRequest(entry.id, stagingParent);
            }
        }

        private sealed class FakeRequest : IChunkRequest
        {
            private readonly string _id;
            private readonly Transform _parent;
            private WorldChunk _chunk;

            public FakeRequest(string id, Transform parent)
            {
                _id = id;
                _parent = parent;
            }

            public ChunkRequestStatus Status
            {
                get
                {
                    if (_chunk == null)
                    {
                        var go = new GameObject(_id);
                        go.transform.SetParent(_parent, false);
                        _chunk = go.AddComponent<WorldChunk>();
                    }
                    return ChunkRequestStatus.Ready;
                }
            }

            public WorldChunk Chunk => _chunk;
            public string Error => null;

            public void Release()
            {
                if (_chunk != null)
                    Object.DestroyImmediate(_chunk.gameObject);
                _chunk = null;
            }
        }

        private readonly List<Object> _cleanup = new List<Object>();
        private WorldStreamer _streamer;

        [TearDown]
        public void TearDown()
        {
            _streamer?.Dispose();
            _streamer = null;
            foreach (var o in _cleanup)
                if (o != null)
                    Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        /// <summary>A set of chunks every 14 m from <paramref name="originX"/>; the first one marked pinned when asked.</summary>
        private WorldChunkSet Set(string key, float originX, int count, bool pinFirst)
        {
            var set = ScriptableObject.CreateInstance<WorldChunkSet>();
            _cleanup.Add(set);
            var entries = new WorldChunkSet.Entry[count];
            for (int i = 0; i < count; i++)
                entries[i] = new WorldChunkSet.Entry { id = $"chunk.{key}.c{i}", origin = new Vector3(originX + i * 14f, 0f, 0f), pinned = pinFirst && i == 0 };
            set.SetAuthoring(entries, 7f, StreamingWindow.Default);
            return set;
        }

        private BiomeRegions Strip() => BiomeRegions.FromChunkSets(new[] { Set("grassland", 0f, 8, true), Set("forest", 112f, 7, true) });

        private static Rect FootprintEastEdgeAt(float x) => Rect.MinMaxRect(x - 4f, -3f, x, 3f);

        private static void TickN(WorldStreamer streamer, Rect footprint, ref float time, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                streamer.Tick(footprint, time);
                time += 0.1f;
            }
        }

        [Test]
        public void Regions_TheForestStartsAtTheWestEdgeOfItsEntry()
        {
            var regions = Strip();
            Assert.AreEqual(2, regions.Count);
            Assert.AreEqual(105f, regions.StartOf(1), 1e-4f, "Entry at 112, half size 7.");
            Assert.AreEqual(0, regions.RegionAt(104.9f));
            Assert.AreEqual(1, regions.RegionAt(105f));
            Assert.AreEqual(1, regions.RegionAt(196f));
            Assert.AreEqual(0, regions.RegionAt(-20f));
        }

        [Test]
        public void Weights_BlendAcrossTheBand_SmoothlyAndAddingUpToOne()
        {
            var regions = Strip();
            Assert.AreEqual(0f, regions.Weight(1, 14f), 1e-5f);
            Assert.AreEqual(0f, regions.Weight(1, 100f), 1e-5f, "The band starts at x 100.");
            Assert.AreEqual(0.5f, regions.Weight(1, 105f), 1e-4f);
            Assert.AreEqual(1f, regions.Weight(1, 110f), 1e-5f, "The band ends at x 110.");
            Assert.AreEqual(1f, regions.Weight(1, 140f), 1e-5f);
            float previous = 0f, maxStep = 0f;
            for (float x = 95f; x <= 115f; x += 0.1f)
            {
                float w = regions.Weight(1, x);
                Assert.AreEqual(1f, w + regions.Weight(0, x), 1e-5f, "Shares add up to 1 at x " + x);
                Assert.GreaterOrEqual(w, previous - 1e-6f, "The Forest's share never falls going east.");
                maxStep = Mathf.Max(maxStep, w - previous);
                previous = w;
            }
            Assert.Less(maxStep, 0.02f, "No pop: at most 2 % per 10 cm.");
            regions.Resolve(140f, out int from, out int to, out float t);
            Assert.AreEqual((1, 1, 0f), (from, to, t));
            regions.Resolve(102f, out from, out to, out t);
            Assert.AreEqual((0, 1), (from, to));
            Assert.That(t, Is.GreaterThan(0f).And.LessThan(0.5f));
        }

        [Test]
        public void CameraBiome_SwitchesPastTheStart_WithHysteresis()
        {
            var regions = Strip();
            int biome = 0;
            foreach (float x in new[] { 104.6f, 105.4f, 104.7f, 105.3f, 105f })
            {
                biome = regions.CameraBiome(x, biome);
                Assert.AreEqual(0, biome, "A camera resting on the start stays in the Grassland (x " + x + ").");
            }
            biome = regions.CameraBiome(105.6f, biome);
            Assert.AreEqual(1, biome, "0.5 m past the start: the Forest.");
            foreach (float x in new[] { 105.4f, 104.6f, 105f })
            {
                biome = regions.CameraBiome(x, biome);
                Assert.AreEqual(1, biome, "No flicker back (x " + x + ").");
            }
            Assert.AreEqual(0, regions.CameraBiome(104.4f, biome), "0.5 m before the start: back in the Grassland.");
            Assert.AreEqual(1, regions.CameraBiome(140f, -1), "No current biome: the biome at x.");
            Assert.AreEqual(1, regions.CameraBiome(140f, 0), "A jump far past the start switches at once.");
        }

        [Test]
        public void Streamer_SeveralSets_OnlyTheFirstPins_AndTheForestStreamsWithTheCamera()
        {
            var source = new FakeSource();
            var grassland = Set("grassland", 0f, 8, true);
            var forest = Set("forest", 112f, 7, true); // an old set with its arrival pinned: ignored
            _streamer = new WorldStreamer(new[] { grassland, forest }, source, null, QualityTier.Medium);
            Assert.AreEqual(15, _streamer.Count);
            Assert.AreEqual(2, _streamer.SetCount);
            int entry = _streamer.IndexOf("chunk.forest.c0");
            Assert.AreEqual(8, entry, "The Forest's slots follow the Grassland's.");
            Assert.AreEqual(1, _streamer.SetIndexOf(entry));
            Assert.AreEqual(0, _streamer.SetIndexOf(0));
            Assert.IsTrue(_streamer.IsPinned(0));
            Assert.IsFalse(_streamer.IsPinned(entry), "Only the world's first arrival is pinned.");
            Assert.IsFalse(_streamer.IsResident(entry));

            float t = 0f;
            TickN(_streamer, FootprintEastEdgeAt(3f), ref t, 10);
            Assert.IsTrue(_streamer.PinnedSettled);
            Assert.AreEqual(ChunkStreamState.Unloaded, _streamer.GetState(entry), "Far from the Forest: nothing of it loads.");
            Assert.IsFalse(source.Requested.Any(id => id.StartsWith("chunk.forest.", StringComparison.Ordinal)));

            // At the ForestEdge, the footprint 1 m short of x 105: the Entry preloads and activates, in set order after the edge.
            source.Requested.Clear();
            TickN(_streamer, Rect.MinMaxRect(96f, -3f, 104f, 3f), ref t, 10);
            Assert.AreEqual(ChunkStreamState.Active, _streamer.GetState(entry));
            Assert.AreEqual(ChunkStreamState.Active, _streamer.GetState(_streamer.IndexOf("chunk.grassland.c7")));
            Assert.Less(source.Requested.IndexOf("chunk.grassland.c7"), source.Requested.IndexOf("chunk.forest.c0"), "One load per tick, in set order.");
            Assert.AreEqual(ChunkStreamState.Unloaded, _streamer.GetState(_streamer.IndexOf("chunk.forest.c1")), "The Undergrowth waits for the camera.");

            // Into the Clearing and back: the Entry leaves after the release delay unless it is resident.
            TickN(_streamer, FootprintEastEdgeAt(142f), ref t, 20);
            Assert.AreEqual(ChunkStreamState.Active, _streamer.GetState(_streamer.IndexOf("chunk.forest.c2")));
            _streamer.SetResident(_streamer.IndexOf("chunk.forest.c2"), true);
            TickN(_streamer, FootprintEastEdgeAt(3f), ref t, 80);
            Assert.AreEqual(ChunkStreamState.Unloaded, _streamer.GetState(entry), "Released once out of the window for 4 s.");
            Assert.AreEqual(ChunkStreamState.Staged, _streamer.GetState(_streamer.IndexOf("chunk.forest.c2")), "A resident chunk stays loaded, hidden.");
            Assert.IsTrue(_streamer.TryGetBounds(out var bounds));
            Assert.AreEqual(203f, bounds.xMax, 1e-4f, "The world's bounds run to the Forest's last chunk.");
        }

        [Test]
        public void Atmosphere_BlendsTwoMoodSets_WithoutAllocating()
        {
            var grasslandDay = ScriptableObject.CreateInstance<LightingProfile>();
            var forestDay = ScriptableObject.CreateInstance<LightingProfile>();
            _cleanup.Add(grasslandDay);
            _cleanup.Add(forestDay);
            grasslandDay.sunIntensity = 1f;
            forestDay.sunIntensity = 3f;
            var sunObject = new GameObject("Sun");
            _cleanup.Add(sunObject);
            var sun = sunObject.AddComponent<Light>();
            var atmosphere = new WorldAtmosphere(null, grasslandDay, null, sun, null, WorldQualityProfile.Default(QualityTier.Medium));
            Assert.AreEqual(1, atmosphere.AddBiome(null, forestDay, null, null));
            Assert.AreEqual(2, atmosphere.BiomeCount);

            atmosphere.Apply(13f);
            Assert.AreEqual(1f, sun.intensity, 1e-4f, "Start: the Grassland's light.");
            atmosphere.SetRegion(0, 1, 0.5f);
            atmosphere.Apply(13f);
            Assert.AreEqual(2f, sun.intensity, 1e-4f, "Mid band: half of each.");
            atmosphere.SetRegion(1, 1, 0f);
            atmosphere.Apply(13f);
            Assert.AreEqual(3f, sun.intensity, 1e-4f, "In the Forest: its light.");

            atmosphere.ForceHour(13f);
            for (int i = 0; i < 20; i++)
            {
                atmosphere.SetRegion(0, 1, i / 20f);
                atmosphere.Tick(i);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 400; i++)
            {
                atmosphere.SetRegion(0, 1, (i % 100) / 100f);
                atmosphere.Tick(100f + i * 0.016f);
            }
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "The blend allocates nothing.");
            Assert.AreEqual(0.99f, atmosphere.RegionBlend, 1e-4f, "Every visible move is taken.");
        }

        [Test]
        public void Weather_RayAreas_FadePastTheirEnds_AndRegionsClamp()
        {
            var areas = new[] { new Vector2(105f, 119f), new Vector2(133f, 147f) };
            Assert.AreEqual(1f, WeatherSystem.RayAreaWeight(areas, 112f), 1e-5f, "Inside the Entry.");
            Assert.AreEqual(1f, WeatherSystem.RayAreaWeight(areas, 140f), 1e-5f, "Inside the Clearing.");
            Assert.AreEqual(0f, WeatherSystem.RayAreaWeight(areas, 126f), 1e-5f, "The Undergrowth stays shaded.");
            float edge = WeatherSystem.RayAreaWeight(areas, 120f);
            Assert.That(edge, Is.GreaterThan(0f).And.LessThan(1f), "Faded over 2 m past an end.");
            Assert.AreEqual(1f, WeatherSystem.RayAreaWeight(Array.Empty<Vector2>(), 50f), "No area: everywhere.");
            Assert.IsFalse(WeatherSystem.RaysOn(null, true, QualityTier.High));

            var grassland = ScriptableObject.CreateInstance<WeatherKit>();
            var forest = ScriptableObject.CreateInstance<WeatherKit>();
            _cleanup.Add(grassland);
            _cleanup.Add(forest);
            forest.sunRaysFrom = QualityTier.Medium;
            var weather = new WeatherSystem(grassland, null, WorldQualityProfile.Default(QualityTier.Medium), QualityTier.Medium);
            Assert.AreEqual(1, weather.AddBiome(forest));
            Assert.AreEqual(2, weather.AddBiome(null), "A biome without a kit borrows the first kit's look.");
            weather.SetRegion(0, 5, 0.3f);
            Assert.AreEqual((0, 2, 0.3f), (weather.RegionFrom, weather.RegionTo, weather.RegionBlend));
            weather.SetRegion(1, 1, 0.7f);
            Assert.AreEqual(0f, weather.RegionBlend, "One biome: no blend.");
            Assert.AreEqual(0f, weather.RayShare(1, 112f), "A kit without ray meshes shows none.");
        }

        [Test]
        public void Wizard_TheForestContinuesTheStrip_AndOnlyTheWorldsFirstArrivalPins()
        {
            var forest = BiomeWizard.ForestSpec();
            var grassland = BiomeWizard.GrasslandSpec();
            Assert.AreEqual(112f, BiomeWizard.ChunkOrigin(forest, 0).x, 1e-4f, "Entry at 112.");
            Assert.AreEqual(196f, BiomeWizard.ChunkOrigin(forest, forest.Zones.Count - 1).x, 1e-4f, "LakeTransition at 196.");
            Assert.AreEqual(98f, BiomeWizard.ChunkOrigin(grassland, grassland.Zones.Count - 1).x, 1e-4f, "ForestEdge at 98.");
            Assert.IsTrue(BiomeWizard.PinsArrival(grassland));
            Assert.IsFalse(BiomeWizard.PinsArrival(forest), "The Forest's arrival streams with the camera.");
            Assert.AreEqual(QualityTier.Medium, forest.SunRaysFrom, "The Forest's shafts from Équilibrée.");
            CollectionAssert.AreEqual(new[] { new Vector2(105f, 119f), new Vector2(133f, 147f) }, BiomeWizard.SunRayAreas(forest),
                "Shafts in the Entry and the Clearing.");
            Assert.IsEmpty(BiomeWizard.SunRayAreas(grassland));
        }

        [Test]
        public void ShippedForest_WhenMade_ContinuesTheGrasslandStrip()
        {
            var forestSet = AssetDatabase.LoadAssetAtPath<WorldChunkSet>("Assets/_Game/Content/Biomes/DEF_ChunkSet_Forest.asset");
            var grasslandSet = AssetDatabase.LoadAssetAtPath<WorldChunkSet>("Assets/_Game/Content/Biomes/DEF_ChunkSet_Grassland.asset");
            if (forestSet == null)
                Assert.Ignore("DEF_ChunkSet_Forest is made by 'Pocket Wilds/Content/Create Forest Biome (T50)'.");
            Assert.IsNotNull(grasslandSet);
            for (int i = 0; i < forestSet.Count; i++)
            {
                var entry = forestSet.Get(i);
                Assert.AreEqual(112f + i * 14f, entry.origin.x, 1e-4f, entry.id);
                Assert.IsFalse(entry.pinned, entry.id + ": only the world's first arrival is pinned.");
            }
            var regions = BiomeRegions.FromChunkSets(new[] { grasslandSet, forestSet });
            Assert.AreEqual(105f, regions.StartOf(1), 1e-4f, "The Forest region starts at x 105.");
            var world = AssetDatabase.LoadAssetAtPath<BiomeWorldProfile>("Assets/_Game/Content/Biomes/DEF_World_Forest.asset");
            Assert.IsNotNull(world, "DEF_World_Forest");
            Assert.AreSame(forestSet, world.chunkSet);
            if (world.weatherKit != null)
                Assert.AreEqual(QualityTier.Medium, world.weatherKit.sunRaysFrom, "The Forest's shafts from Équilibrée.");
        }
    }
}
