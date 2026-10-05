using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.Lighting;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T33B Prairie Magnifique part 2: the strip of chunks (water, landmarks, rotor sails), weather (rain, rainbow,
    /// sun rays), the night mood of events and the scatter LOD by distance. Doc 04 D-BIO-001, doc 07.
    /// </summary>
    public class WorldStripTests
    {
        private static WorldChunk Load(string path) => AssetDatabase.LoadAssetAtPath<WorldChunk>(path);

        [Test]
        public void Strip_HasEightChunks_EachWithGroundScatterSpawnsAndLife()
        {
            var entries = WorldContentPipeline.GrasslandChunks;
            Assert.AreEqual(8, entries.Length, "Doc 04: 6 to 8 Grassland chunk compositions.");
            foreach (var entry in entries)
            {
                var chunk = Load(entry.path);
                Assert.IsNotNull(chunk, entry.path);
                Assert.IsNotNull(chunk.Scatter, entry.address);
                Assert.GreaterOrEqual(chunk.AnimalSpawns.Length, 3, entry.address);
                Assert.IsTrue(chunk.VfxAnchors.Any(a => a.name.StartsWith("VFX_Butterfly")), entry.address);
                Assert.IsTrue(chunk.VfxAnchors.Any(a => a.name.StartsWith("VFX_Birds")), entry.address);
                Assert.LessOrEqual(chunk.GetComponentsInChildren<MeshRenderer>(true).Length, 50, entry.address);
            }
        }

        [Test]
        public void StreamAndPond_UseWaterLite_AndNothingGrowsInTheWater()
        {
            foreach (var name in new[] { "StreamBend", "PondEdge" })
            {
                var chunk = Load($"{GrasslandPipeline.StripChunkFolder}/PF_Chunk_Grassland_{name}.prefab");
                var water = chunk.transform.Find("Water").GetComponent<MeshRenderer>();
                Assert.AreEqual("PocketWilds/WaterLite", water.sharedMaterial.shader.name, name);
                Assert.AreEqual(ShadowCastingModeOff(water), true, name + ": water never casts shadows.");
                var mesh = water.GetComponent<MeshFilter>().sharedMesh;
                var scatter = chunk.Scatter;
                int inside = 0;
                var waterTriangles = AssetDatabase.LoadAllAssetsAtPath($"{GrasslandPipeline.ModelsFolder}/GL_Water.fbx").OfType<Mesh>().First(m => m.name == mesh.name);
                var vertices = waterTriangles.vertices;
                var triangles = waterTriangles.triangles;
                for (int i = 0; i < scatter.Count; i++)
                {
                    var p = scatter.placements[i];
                    if (InsideFlat(vertices, triangles, p.x, p.z))
                        inside++;
                }
                Assert.AreEqual(0, inside, name + ": no grass or flower stands in the water.");
            }
        }

        private static bool ShadowCastingModeOff(Renderer r) => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off;

        private static bool InsideFlat(Vector3[] v, int[] t, float x, float z)
        {
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                if (Mathf.Abs(n.normalized.y) < 0.8f)
                    continue;
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-8f)
                    continue;
                float w0 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                float w1 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                if (w0 >= 0.05f && w1 >= 0.05f && 1f - w0 - w1 >= 0.05f)
                    return true;
            }
            return false;
        }

        [Test]
        public void Windmill_SailsTurnInTheShader_WithTheirOwnPalette()
        {
            var chunk = Load($"{GrasslandPipeline.StripChunkFolder}/PF_Chunk_Grassland_WindmillRise.prefab");
            int sails = 0;
            for (int i = 0; i < chunk.BindingCount; i++)
            {
                var binding = chunk.GetBinding(i);
                bool isSails = binding.renderer.name.Contains("_Sails_");
                Assert.AreEqual(isSails, binding.rotor, binding.renderer.name);
                if (isSails)
                {
                    sails++;
                    Assert.AreEqual(ContentId.FromSource(GrasslandPipeline.PaletteWindmillSails).Value, binding.paletteId);
                }
            }
            Assert.AreEqual(3, sails, "Sails on each of the three LODs.");
            uint packed = EnvironmentVisualPacking.Pack(12, 8, false, true);
            Assert.IsTrue(EnvironmentVisualPacking.IsRotor(packed));
            Assert.AreEqual(12, EnvironmentVisualPacking.PaletteRow(packed));
            Assert.IsFalse(EnvironmentVisualPacking.IsRotor(EnvironmentVisualPacking.Pack(12, 8)));
        }

        [Test]
        public void Landmarks_HaveThreeLods()
        {
            foreach (var name in new[] { "PF_GL_Landmark_OldTree", "PF_GL_Landmark_Windmill" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{GrasslandPipeline.PrefabsFolder}/{name}.prefab");
                Assert.IsNotNull(prefab, name);
                Assert.AreEqual(3, prefab.GetComponent<LODGroup>().GetLODs().Length, name);
            }
        }

        // ---------------- weather ----------------

        private static WorldAtmosphere Atmosphere(out Light sun, out GameObject sunGo)
        {
            sunGo = new GameObject("Sun");
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            var day = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.ProfilePath);
            var night = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.NightProfilePath);
            var atmosphere = new WorldAtmosphere(null, day, null, sun, null, WorldQualityProfile.Default(QualityTier.Medium), night);
            atmosphere.ForceHour(13f);
            atmosphere.Tick(0f);
            return atmosphere;
        }

        [Test]
        public void Weather_ShowerRampsIn_ThenARainbowInDaylight_ThenClears()
        {
            var kit = AssetDatabase.LoadAssetAtPath<WeatherKit>(GrasslandPipeline.WeatherKitPath);
            Assert.IsNotNull(kit);
            var atmosphere = Atmosphere(out _, out var sunGo);
            try
            {
                var weather = new WeatherSystem(kit, atmosphere, WorldQualityProfile.Default(QualityTier.Medium));
                weather.StartShower(0f);
                weather.Tick(kit.rampSeconds * 0.5f);
                Assert.AreEqual(WeatherSystem.Phase.RainIn, weather.Current);
                Assert.That(weather.Rain, NUnit.Framework.Is.InRange(0.2f, 0.8f));
                Assert.AreEqual(weather.Rain, atmosphere.Rain, 1e-3f, "The shower greys the atmosphere.");
                float t = kit.rampSeconds + 0.01f;
                weather.Tick(t);
                Assert.AreEqual(WeatherSystem.Phase.Raining, weather.Current);
                weather.Tick(t += kit.showerSeconds.y + 0.01f);
                Assert.AreEqual(WeatherSystem.Phase.RainOut, weather.Current);
                weather.Tick(t += kit.rampSeconds + 0.01f);
                Assert.AreEqual(WeatherSystem.Phase.Rainbow, weather.Current, "13:00 is daylight: a rainbow follows.");
                weather.Tick(t += kit.rainbowSeconds * 0.5f);
                Assert.Greater(weather.Rainbow, 0.5f);
                weather.Tick(t += kit.rainbowSeconds);
                Assert.AreEqual(WeatherSystem.Phase.Clear, weather.Current);
                Assert.AreEqual(0f, weather.Rain);
                Assert.AreEqual(0f, atmosphere.Rain);
            }
            finally
            {
                Object.DestroyImmediate(sunGo);
            }
        }

        [Test]
        public void Weather_TiersScaleTheDrops_AndRenderDoesNotAllocate()
        {
            var kit = AssetDatabase.LoadAssetAtPath<WeatherKit>(GrasslandPipeline.WeatherKitPath);
            var cameraGo = new GameObject("Camera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 9f, -11f), Quaternion.Euler(40f, 0f, 0f));
            try
            {
                int Drops(QualityTier tier)
                {
                    var weather = new WeatherSystem(kit, null, WorldQualityProfile.Default(tier));
                    weather.Force(1f, 0f);
                    weather.Render(camera, Vector3.zero);
                    return weather.LastDrops;
                }
                int high = Drops(QualityTier.High), medium = Drops(QualityTier.Medium), low = Drops(QualityTier.Low);
                Assert.AreEqual(kit.maxDrops, high);
                Assert.AreEqual(0.7f, medium / (float)high, 0.01f, "Doc 07: weather -30 % on Équilibrée.");
                Assert.AreEqual(0.4f, low / (float)high, 0.01f, "Doc 07: weather -60 % on Fluide.");

                var w = new WeatherSystem(kit, null, WorldQualityProfile.Default(QualityTier.High));
                w.Force(1f, 1f);
                w.Render(camera, Vector3.zero);
                Assert.That(() =>
                {
                    for (int f = 0; f < 200; f++)
                    {
                        w.Tick(f / 30f);
                        w.Render(camera, Vector3.zero);
                    }
                }, Is.Not.AllocatingGCMemory());
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void NightMood_ShowsStars_AndWaterQualityFollowsTheTier()
        {
            var atmosphere = Atmosphere(out _, out var sunGo);
            var day = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.ProfilePath);
            try
            {
                Assert.IsTrue(atmosphere.HasNight);
                Assert.AreEqual(0f, Shader.GetGlobalVector("_PW_SkyParams").z, 1e-4f, "No stars by day.");
                Assert.AreEqual(1f, Shader.GetGlobalFloat("_PW_WaterQuality"), 1e-4f, "Équilibrée: foam and sky reflection.");
                atmosphere.SetNight(true);
                atmosphere.Tick(10f);
                Assert.Greater(Shader.GetGlobalVector("_PW_SkyParams").z, 0.5f, "The night of events shows stars.");
                atmosphere.SetNight(false);
                atmosphere.SetQuality(WorldQualityProfile.Default(QualityTier.Low));
                atmosphere.Tick(20f);
                Assert.AreEqual(0f, Shader.GetGlobalFloat("_PW_WaterQuality"), 1e-4f, "Fluide: depth color and ripples only.");
            }
            finally
            {
                Object.DestroyImmediate(sunGo);
                day.Apply(null, null, QualityTier.Medium);
            }
        }

        [Test]
        public void Scatter_ThinsOutWithDistance()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            using var palettes = new PaletteTable(registry);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrasslandPipeline.MeadowChunkPath);
            var instance = Object.Instantiate(prefab);
            var cameraGo = new GameObject("Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.farClipPlane = 200f;
            try
            {
                var scatter = new ScatterRenderer(palettes, 1f);
                scatter.Add(instance.GetComponent<WorldChunk>());
                var quality = WorldQualityProfile.Default(QualityTier.Medium);
                scatter.SetDistanceFade(quality.scatterFadeStart, quality.scatterFadeEnd);
                cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 9f, -11f), Quaternion.Euler(40f, 0f, 0f));
                scatter.Render(camera);
                int near = scatter.LastInstances;
                cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 30f, -60f), Quaternion.LookRotation(new Vector3(0f, -30f, 60f)));
                scatter.Render(camera);
                int far = scatter.LastInstances;
                Assert.Greater(near, 0);
                Assert.Less(far, near * 0.5f, "A far chunk keeps about a quarter of its details (LOD by distance).");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(cameraGo);
            }
        }
    }
}
