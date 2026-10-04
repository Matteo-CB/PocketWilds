using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T00-T05 gate: the app boots, creates and reloads a save, advances an economy with
    /// BigValue, and enters the vertical slice scene through the real bootstrap path.
    /// </summary>
    public class BootstrapSmokeTests
    {
        private string _saveDir;
        private ContentRegistry _content;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            _content = null;
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

        private static ContentRegistry TempContent()
        {
            var builder = new ContentRegistryBuilder();
            builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource("species.rabbit"), "species.rabbit.name",
                RigFamily.SmallQuadruped, ContentId.FromSource("biome.grassland"), 1.0, "TEMP_PlaceholderAnimal"), "species.rabbit");
            return builder.Build();
        }

        private IEnumerator Boot(bool loadScene, Action<GameBootstrap> onReady)
        {
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = _content ?? TempContent(),
                LoadTargetScene = loadScene,
            };
            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();

            float timeout = Time.realtimeSinceStartup + 15f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Boot timed out.");
                yield return null;
            }

            Assert.AreEqual(BootState.Ready, bootstrap.State);
            onReady(bootstrap);
        }

        [UnityTest]
        public IEnumerator Boot_CreatesSave_AdvancesEconomy_AndReloads()
        {
            GameBootstrap first = null;
            yield return Boot(false, b => first = b);

            var context = first.Context;
            Assert.AreEqual(SaveLoadOutcome.NewProfile, first.LoadResult.Outcome);
            for (int i = 0; i < 3; i++)
            {
                uint id = context.State.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit"), Seed = (uint)i });
                context.State.Collection.TryGet(id, out var animal);
                context.Economy.OnAnimalAdded(animal);
            }
            Assert.AreEqual(3.0, context.Economy.GetLightPerSecond().ToDoubleClamped(), 1e-9);

            float until = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < until)
                yield return null;

            Assert.Greater(context.State.Wallet.Light.ToDoubleClamped(), 0.0, "GameLoop must advance production.");
            Assert.IsTrue(context.Save.FlushNow());
            var profileId = context.State.Meta.ProfileId;
            Object.Destroy(first.gameObject);
            yield return null;

            GameBootstrap second = null;
            yield return Boot(false, b => second = b);
            Assert.AreEqual(SaveLoadOutcome.Loaded, second.LoadResult.Outcome);
            Assert.AreEqual(profileId, second.Context.State.Meta.ProfileId);
            Assert.AreEqual(3, second.Context.State.Collection.Count);
            Assert.AreEqual(3.0, second.Context.Economy.GetLightPerSecond().ToDoubleClamped(), 1e-9);
        }

        [UnityTest]
        public IEnumerator Boot_EntersVerticalSliceScene_WithContext()
        {
            GameBootstrap bootstrap = null;
            yield return Boot(true, b => bootstrap = b);

            var scene = SceneManager.GetActiveScene();
            Assert.AreEqual("SC_VerticalSlice_Grassland", scene.name);
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            Assert.IsNotNull(entry);
            Assert.IsTrue(entry.HasContext);
        }

        [UnityTest]
        public IEnumerator Boot_WithShippedContent_ResolvesRabbitVariants()
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            Assert.IsNotNull(database);
            _content = database.CreateBuilder().Build();
            GameBootstrap bootstrap = null;
            yield return Boot(false, b => bootstrap = b);

            var context = bootstrap.Context;
            int palettes = _content.Variants.Count + _content.Eggs.Count + _content.Palettes.Count;
            Assert.AreEqual(palettes, context.Palettes.PaletteCount);
            Assert.AreEqual((float)Mathf.NextPowerOfTwo(palettes), Shader.GetGlobalFloat(PaletteTable.RowCountId));
            var animal = new AnimalInstance
            {
                InstanceId = 1,
                Species = ContentId.FromSource("species.rabbit"),
                Variant = ContentId.FromSource("variant.rabbit.spotted"),
                Seed = 5,
            };
            Assert.IsTrue(context.Visuals.TryResolve(animal, Vector3.zero, 0f, out var snapshot));
            Assert.AreEqual(PatternChannel.MaskA, AnimalVisualPacking.Pattern(snapshot.VisualData));

            Object.Destroy(bootstrap.gameObject);
            yield return null;
            Assert.AreEqual(0f, Shader.GetGlobalFloat(PaletteTable.RowCountId), "Palettes must be released with the bootstrap.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Boot_IntoVerticalSlice_BindsTheGrasslandChunk()
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            _content = database.CreateBuilder().Build();
            GameBootstrap bootstrap = null;
            yield return Boot(true, b => bootstrap = b);

            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            Assert.IsNotNull(entry);
            Assert.IsNotNull(entry.StartChunk, "SC_VerticalSlice_Grassland must reference its start chunk.");
            Assert.IsTrue(bootstrap.Context.Palettes.TryGetRow(ContentId.FromSource("palette.grassland.ground"), out int row));
            var ground = entry.StartChunk.transform.Find("Ground").GetComponent<MeshRenderer>();
            Assert.AreEqual(row, EnvironmentVisualPacking.PaletteRow(ground.GetShaderUserValue()));

            // T16: lighting profile applied for the active tier, camera framed on the egg.
            Assert.IsNotNull(entry.Lighting);
            Assert.AreEqual(entry.Lighting.sky, RenderSettings.skybox);
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(bootstrap.Context.Quality.CurrentTier, PresentationQuality.Tier);
            Assert.IsNotNull(entry.CameraRig);
            Assert.Less(Vector3.Distance(entry.CameraRig.Focus, entry.StartChunk.EggSpawn.position), 0.01f);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }
    }
}
