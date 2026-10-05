using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.Lighting;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T33 Prairie Magnifique: instanced scatter, ambient life, sky and lighting moods follow doc 07 (tiers,
    /// densities, zero allocation per frame) and doc 04 (Grassland kit).
    /// </summary>
    public class WorldBeautyTests
    {
        private ContentRegistry _registry;
        private PaletteTable _palettes;
        private GameObject _start;
        private GameObject _meadow;
        private GameObject _cameraGo;
        private Camera _camera;

        private WorldChunk StartChunk => _start.GetComponent<WorldChunk>();
        private WorldChunk MeadowChunk => _meadow.GetComponent<WorldChunk>();

        [OneTimeSetUp]
        public void SetUp()
        {
            _registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            _palettes = new PaletteTable(_registry);
            _start = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GrasslandPipeline.ChunkPath));
            _meadow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GrasslandPipeline.MeadowChunkPath));
            _meadow.transform.position = GrasslandPipeline.MeadowOffset;
            _cameraGo = new GameObject("WorldBeautyTests Camera");
            _camera = _cameraGo.AddComponent<Camera>();
            _camera.fieldOfView = 30f;
            _camera.aspect = 540f / 1170f;
            _camera.farClipPlane = 80f;
            // The gameplay frame: about 14 m from the egg, looking down 40 degrees.
            var focus = StartChunk.EggSpawn.position;
            var rotation = Quaternion.Euler(40f, 0f, 0f);
            _cameraGo.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 14f, rotation);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_start);
            Object.DestroyImmediate(_meadow);
            Object.DestroyImmediate(_cameraGo);
            _palettes?.Dispose();
        }

        // ---------------- quality tiers (doc 07 table) ----------------

        [Test]
        public void WorldQuality_FollowsTheDoc07Table()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WorldQualityProfile>(GrasslandPipeline.WorldQualityPath);
            Assert.IsNotNull(asset);
            foreach (var get in new System.Func<QualityTier, WorldQualityProfile.Tier>[] { asset.Get, WorldQualityProfile.Default })
            {
                Assert.AreEqual(0.3f, get(QualityTier.Low).scatterDensity, 1e-4f);
                Assert.AreEqual(0.65f, get(QualityTier.Medium).scatterDensity, 1e-4f);
                Assert.AreEqual(1f, get(QualityTier.High).scatterDensity, 1e-4f);
                Assert.AreEqual(0.25f, get(QualityTier.Low).ambientLifeDensity, 1e-4f);
                Assert.AreEqual(0.6f, get(QualityTier.Medium).ambientLifeDensity, 1e-4f);
                Assert.AreEqual(1f, get(QualityTier.High).ambientLifeDensity, 1e-4f);
                Assert.IsFalse(get(QualityTier.Low).clouds, "Fluide: no clouds.");
                Assert.AreEqual(0f, get(QualityTier.Low).heightFog, "Fluide: no height fog.");
                Assert.IsTrue(get(QualityTier.Medium).clouds);
                Assert.Greater(get(QualityTier.Medium).heightFog, 0f);
            }
        }

        // ---------------- scatter ----------------

        [Test]
        public void Scatter_TiersKeepAStableShare_FlowersKeepTheCharm()
        {
            int Instances(float density)
            {
                var scatter = new ScatterRenderer(_palettes, density);
                scatter.Add(StartChunk);
                scatter.Add(MeadowChunk);
                scatter.Render(_camera);
                return scatter.LastInstances;
            }
            int high = Instances(1f), medium = Instances(0.65f), low = Instances(0.3f);
            Assert.Greater(high, 1500, "The gameplay frame shows a dense meadow.");
            Assert.LessOrEqual(high, 4000, "Doc 07: up to about 4,000 visible ground details on Magnifique.");
            // Flowers, pebbles and mushrooms keep a minimum share: Low lands a little above 30 %.
            Assert.That(low / (float)high, NUnit.Framework.Is.InRange(0.3f, 0.42f));
            Assert.That(medium / (float)high, NUnit.Framework.Is.InRange(0.65f, 0.75f));
        }

        [Test]
        public void Scatter_DrawsOneBatchPerKindAndChunk_AndCullsChunksOutOfView()
        {
            var scatter = new ScatterRenderer(_palettes, 1f);
            Assert.IsTrue(scatter.Add(StartChunk));
            Assert.IsFalse(scatter.Add(StartChunk), "A chunk is added once.");
            scatter.Render(_camera);
            Assert.LessOrEqual(scatter.LastDraws, StartChunk.Scatter.groups.Length);
            var position = _cameraGo.transform.position;
            var rotation = _cameraGo.transform.rotation;
            _cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 10f, -60f), Quaternion.LookRotation(Vector3.back));
            scatter.Render(_camera);
            _cameraGo.transform.SetPositionAndRotation(position, rotation);
            Assert.AreEqual(0, scatter.LastDraws, "A chunk behind the camera is culled.");
            Assert.IsTrue(scatter.Remove(StartChunk));
            Assert.AreEqual(0, scatter.ChunkCount);
        }

        [Test]
        public void Scatter_Render_DoesNotAllocate()
        {
            var scatter = new ScatterRenderer(_palettes, 0.65f);
            scatter.Add(StartChunk);
            scatter.Add(MeadowChunk);
            scatter.Render(_camera);
            Assert.That(() =>
            {
                for (int f = 0; f < 120; f++)
                    scatter.Render(_camera);
            }, Is.Not.AllocatingGCMemory());
        }

        // ---------------- ambient life ----------------

        private static AmbientLifeKit LifeKit() => AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(GrasslandPipeline.LifeKitPath);

        [Test]
        public void LifeKit_HasThreeKindsOfLife_OnTheInstancedMaterial()
        {
            var kit = LifeKit();
            Assert.IsNotNull(kit);
            Assert.AreEqual("PocketWilds/AmbientLife", kit.material.shader.name);
            Assert.IsTrue(kit.material.enableInstancing);
            var behaviours = kit.species.Select(s => s.behaviour).Distinct().ToList();
            Assert.AreEqual(3, behaviours.Count, "Doc 04: butterflies, bees, small birds.");
            Assert.IsTrue(kit.species.Any(s => s.spawnChance < 1f && s.behaviour == AmbientLifeBehaviour.Butterfly), "Doc 04: a rare golden butterfly.");
            foreach (var species in kit.species)
            {
                Assert.IsNotNull(species.mesh, species.id);
                Assert.IsFalse(string.IsNullOrEmpty(species.anchorPrefix), species.id);
                Assert.IsTrue(_registry.Palettes.TryGet(new ContentId(species.paletteId), out _), species.id + " palette");
                Assert.Greater(species.spawnChance, 0f, species.id);
            }
        }

        [Test]
        public void AmbientLife_SpawnsAtTheChunkAnchors_AndTiersKeepAShare()
        {
            var full = new AmbientLife(LifeKit(), _palettes, 1f);
            Assert.IsTrue(full.Add(StartChunk));
            Assert.IsTrue(full.Add(MeadowChunk));
            Assert.IsFalse(full.Add(StartChunk));
            full.Tick(1f / 30f, 0f);
            int all = full.LastActive;
            Assert.AreEqual(full.AgentCount, all);
            Assert.That(all, NUnit.Framework.Is.InRange(16, 40));
            var low = new AmbientLife(LifeKit(), _palettes, 0.25f);
            low.Add(StartChunk);
            low.Add(MeadowChunk);
            low.Tick(1f / 30f, 0f);
            Assert.That(low.LastActive / (float)all, NUnit.Framework.Is.InRange(0.2f, 0.6f), "Fluide keeps about a quarter (at least one per anchor).");
            Assert.IsTrue(full.Remove(MeadowChunk));
            Assert.Less(full.AgentCount, all);
            full.Clear();
            Assert.AreEqual(0, full.AgentCount);
        }

        [Test]
        public void AmbientLife_TapStartlesTheBirds_WhichLandAgain()
        {
            var life = new AmbientLife(LifeKit(), _palettes, 1f);
            life.Add(StartChunk);
            var birds = StartChunk.VfxAnchors.First(a => a.name.StartsWith("VFX_Birds")).position;
            float time = 0f;
            for (int i = 0; i < 30; i++)
                life.Tick(1f / 30f, time += 1f / 30f);
            Assert.AreEqual(0, life.LastAirborne);
            Assert.Greater(life.Startle(birds, 2f), 0);
            life.Tick(1f / 30f, time += 1f / 30f);
            Assert.Greater(life.LastAirborne, 0, "Tapped birds fly away.");
            for (int i = 0; i < 150; i++)
                life.Tick(1f / 30f, time += 1f / 30f);
            Assert.AreEqual(0, life.LastAirborne, "They land again nearby after the flight.");
        }

        [Test]
        public void AmbientLife_TickAndRender_DoNotAllocate()
        {
            var life = new AmbientLife(LifeKit(), _palettes, 1f);
            life.Add(StartChunk);
            life.Add(MeadowChunk);
            float time = 0f;
            life.Tick(1f / 30f, time);
            life.Render(_camera, time);
            var birds = StartChunk.VfxAnchors.First(a => a.name.StartsWith("VFX_Birds")).position;
            Assert.That(() =>
            {
                for (int f = 0; f < 300; f++)
                {
                    time += 1f / 30f;
                    if (f == 100)
                        life.Startle(birds, 3f);
                    life.Tick(1f / 30f, time);
                    life.Render(_camera, time);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        // ---------------- atmosphere ----------------

        [Test]
        public void Moods_SoftenedDay_HasNoBlackNight()
        {
            void Check(float hour, LightingMood from, LightingMood to)
            {
                WorldAtmosphere.Resolve(hour, out var a, out var b, out _);
                Assert.AreEqual(from, a, hour.ToString());
                Assert.AreEqual(to, b, hour.ToString());
            }
            Check(8f, LightingMood.Morning, LightingMood.Morning);
            Check(13f, LightingMood.Day, LightingMood.Day);
            Check(20f, LightingMood.Evening, LightingMood.Evening);
            Check(2f, LightingMood.Evening, LightingMood.Evening);
            Check(10.5f, LightingMood.Morning, LightingMood.Day);
            Check(17f, LightingMood.Day, LightingMood.Evening);
        }

        [Test]
        public void Moods_ShareTheSky_AndApplyLinearGlobals_WithoutAllocating()
        {
            var day = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.ProfilePath);
            var morning = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.MorningProfilePath);
            var evening = AssetDatabase.LoadAssetAtPath<LightingProfile>(LightingPipeline.EveningProfilePath);
            Assert.IsNotNull(morning);
            Assert.IsNotNull(evening);
            Assert.AreSame(day.sky, morning.sky, "One sky material per biome; moods blend through globals.");
            Assert.AreSame(day.sky, evening.sky);
            Assert.Greater(evening.sunColor.r - evening.sunColor.b, day.sunColor.r - day.sunColor.b, "Evening is warmer (doc 04: warm gold).");
            Assert.Greater(morning.heightFogStrength, day.heightFogStrength, "Morning keeps a soft mist (doc 04).");

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            var ambient = (RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
                RenderSettings.fogColor, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            try
            {
                var atmosphere = new WorldAtmosphere(morning, day, evening, sun, _camera, WorldQualityProfile.Default(QualityTier.Medium));
                atmosphere.Apply(13f);
                // Shader.SetGlobalColor does not convert to linear: the atmosphere must, or the sky washes out.
                var top = Shader.GetGlobalColor("_PW_SkyTop");
                Assert.AreEqual(day.skyTop.linear.r, top.r, 1e-3f);
                Assert.AreEqual(day.skyTop.linear.b, top.b, 1e-3f);
                Assert.AreEqual(day.cloudCoverage, Shader.GetGlobalVector("_PW_SkyParams").x, 1e-4f);
                atmosphere.SetQuality(WorldQualityProfile.Default(QualityTier.Low));
                atmosphere.Apply(13f);
                Assert.AreEqual(0f, Shader.GetGlobalVector("_PW_SkyParams").x, "Fluide hides the clouds.");
                Assert.AreEqual(0f, Shader.GetGlobalVector("_PW_HeightFog").z, "Fluide has no height fog.");
                atmosphere.ForceMood(LightingMood.Evening);
                float time = 0f;
                atmosphere.Tick(time);
                Assert.AreEqual(19.5f, atmosphere.Hour, 1e-3f);
                Assert.That(() =>
                {
                    for (int f = 0; f < 300; f++)
                        atmosphere.Tick(time += 1f / 30f);
                }, Is.Not.AllocatingGCMemory());
            }
            finally
            {
                Object.DestroyImmediate(sunGo);
                RenderSettings.ambientSkyColor = ambient.Item1;
                RenderSettings.ambientEquatorColor = ambient.Item2;
                RenderSettings.ambientGroundColor = ambient.Item3;
                RenderSettings.fogColor = ambient.Item4;
                RenderSettings.fogStartDistance = ambient.Item5;
                RenderSettings.fogEndDistance = ambient.Item6;
                day.Apply(null, null, QualityTier.Medium);
            }
        }

        // ---------------- Grassland kit (doc 04 D-BIO-001) ----------------

        [Test]
        public void GrasslandKit_HasTheDoc04Vocabulary()
        {
            var kit = AssetDatabase.LoadAssetAtPath<ScatterKit>(GrasslandPipeline.ScatterKitPath);
            var ids = kit.kinds.Select(k => k.id).ToList();
            Assert.AreEqual(6, ids.Count(id => id.StartsWith("grass.")), "Grass tufts in 3 sizes x 2 shapes.");
            Assert.That(kit.kinds.Where(k => k.id.StartsWith("flower.")).Select(k => k.mesh.name).Distinct().Count(),
                NUnit.Framework.Is.InRange(6, 8), "6 to 8 small plants and flowers.");
            Assert.That(kit.kinds.Where(k => k.id.StartsWith("detail.")).Select(k => k.mesh.name).Distinct().Count(),
                NUnit.Framework.Is.InRange(6, 10), "6 to 10 scatter detail kinds.");
            foreach (var name in new[]
            {
                "PF_GL_Tree_A", "PF_GL_Tree_B", "PF_GL_Tree_C", "PF_GL_Tree_D", "PF_GL_Bush_A", "PF_GL_Bush_B", "PF_GL_Bush_C",
                "PF_GL_Rock_S", "PF_GL_Rock_M", "PF_GL_Rock_L", "PF_GL_RockHero_A", "PF_GL_RockHero_B",
                "PF_GL_Log", "PF_GL_Stump", "PF_GL_Fence_A", "PF_GL_BirdBath", "PF_GL_HayBale",
            })
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>($"{GrasslandPipeline.PrefabsFolder}/{name}.prefab"), name);
        }
    }
}
