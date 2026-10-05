using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T23: the second species reuses the SmallQuadruped skeleton, clips, material and pipeline. It is data
    /// and source art only: same bones, same controller, same material, six palette variants.
    /// </summary>
    public class SquirrelTests
    {
        private const string SquirrelPrefab = "Assets/_Game/Prefabs/Animals/PF_Animal_Squirrel.prefab";
        private const string RabbitPrefab = "Assets/_Game/Prefabs/Animals/PF_Animal_Rabbit.prefab";
        private static readonly ContentId Squirrel = ContentId.FromSource("species.squirrel");

        private static readonly (string Name, RarityTier Rarity)[] Variants =
        {
            ("red", RarityTier.Common), ("brown", RarityTier.Common), ("grey", RarityTier.Uncommon),
            ("cream", RarityTier.Uncommon), ("black", RarityTier.Rare), ("autumn", RarityTier.Epic),
        };

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private static ContentRegistry Registry()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var builder = database.CreateBuilder();
            Assert.IsFalse(builder.HasErrors, string.Join("; ", builder.Issues));
            return builder.Build();
        }

        [Test]
        public void Content_HasSquirrel_WithTheSixRosterVariants()
        {
            var registry = Registry();
            Assert.IsTrue(registry.Species.TryGet(Squirrel, out var species));
            Assert.AreEqual("PF_Animal_Squirrel", species.VisualKey);
            Assert.AreEqual(RigFamily.SmallQuadruped, species.RigFamily);
            Assert.AreEqual(ContentId.FromSource("biome.grassland"), species.Biome);
            foreach (var (name, rarity) in Variants)
            {
                Assert.IsTrue(registry.Variants.TryGet(ContentId.FromSource("variant.squirrel." + name), out var variant), name);
                Assert.AreEqual(Squirrel, variant.Species, name);
                Assert.AreEqual(rarity, variant.Rarity, name);
            }
            int count = 0;
            for (int i = 0; i < registry.Variants.Count; i++)
                if (registry.Variants[i].Species == Squirrel)
                    count++;
            Assert.AreEqual(6, count);
        }

        [Test]
        public void Model_ReusesTheRabbitSkeleton_SameBonesSameBindPoses()
        {
            var rabbit = (ModelImporter)AssetImporter.GetAtPath(AnimalPipeline.ModelPath("Rabbit"));
            var squirrel = (ModelImporter)AssetImporter.GetAtPath(AnimalPipeline.ModelPath("Squirrel"));
            var rabbitBones = rabbit.transformPaths.Where(p => p.Contains("SK_SmallQuadruped")).OrderBy(p => p).ToArray();
            var squirrelBones = squirrel.transformPaths.Where(p => p.Contains("SK_SmallQuadruped")).OrderBy(p => p).ToArray();
            CollectionAssert.AreEqual(rabbitBones, squirrelBones, "Clips bind by path: the hierarchy must be identical.");

            // Same rest pose: every bind pose of the rabbit exists in the squirrel skin.
            var rabbitPoses = LoadLod0(AnimalPipeline.ModelPath("Rabbit")).bindposes;
            var squirrelPoses = LoadLod0(AnimalPipeline.ModelPath("Squirrel")).bindposes;
            Assert.AreEqual(rabbitPoses.Length, squirrelPoses.Length);
            foreach (var pose in rabbitPoses)
                Assert.IsTrue(squirrelPoses.Any(p => Same(p, pose)), "A bone moved: family clips would not fit.");
        }

        private static Mesh LoadLod0(string modelPath) =>
            AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().First(m => m.name.EndsWith("_LOD0"));

        private static bool Same(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(a[i] - b[i]) > 1e-4f)
                    return false;
            return true;
        }

        [Test]
        public void Lods_AreInsideTheSquirrelBudgets()
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(AnimalPipeline.ModelPath("Squirrel")).OfType<Mesh>()
                .OrderBy(m => m.name).ToArray();
            Assert.AreEqual(3, meshes.Length);
            // A-SP-002: 2.0-2.8k / 0.8-1.1k / 0.3-0.5k triangles.
            var ranges = new[] { (2000, 2800), (800, 1100), (300, 500) };
            for (int lod = 0; lod < 3; lod++)
            {
                int tris = meshes[lod].triangles.Length / 3;
                Assert.That(tris, NUnit.Framework.Is.InRange(ranges[lod].Item1, ranges[lod].Item2), meshes[lod].name);
            }
            Assert.IsEmpty(AssetValidator.ValidateAnimalPrefab(SquirrelPrefab).Where(i => i.Severity == IssueSeverity.Error));
        }

        [Test]
        public void Prefab_SharesControllerAndMaterialWithRabbit()
        {
            var squirrel = AssetDatabase.LoadAssetAtPath<AnimalView>(SquirrelPrefab);
            var rabbit = AssetDatabase.LoadAssetAtPath<AnimalView>(RabbitPrefab);
            Assert.IsNotNull(squirrel);
            Assert.AreSame(rabbit.Animator.runtimeAnimatorController, squirrel.Animator.runtimeAnimatorController);
            var master = AssetDatabase.LoadAssetAtPath<Material>(AnimalPipeline.MaterialPath);
            foreach (var r in squirrel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Assert.AreSame(master, r.sharedMaterial, r.name);
            Assert.IsNotNull(AnimalPipeline.FindDeep(squirrel.transform, "SOCK_Back"));
            Assert.IsNotNull(AnimalPipeline.FindDeep(squirrel.transform, "SOCK_TailBase"));
        }

        [Test]
        public void SixVariants_OnOnePrefab_DistinctData_NoMaterialClone()
        {
            var registry = Registry();
            using (var palettes = new PaletteTable(registry))
            {
                var resolver = new AnimalVisualResolver(registry, palettes);
                var prefab = AssetDatabase.LoadAssetAtPath<AnimalView>(SquirrelPrefab);
                var master = AssetDatabase.LoadAssetAtPath<Material>(AnimalPipeline.MaterialPath);
                var data = new HashSet<uint>();
                uint seed = 1;
                foreach (var (name, _) in Variants)
                {
                    var view = Object.Instantiate(prefab);
                    _spawned.Add(view.gameObject);
                    var animal = new AnimalInstance
                    {
                        InstanceId = seed, Seed = seed++, Species = Squirrel,
                        Variant = ContentId.FromSource("variant.squirrel." + name),
                    };
                    Assert.IsTrue(resolver.TryResolve(animal, Vector3.zero, 0f, out var snapshot), name);
                    view.Configure(snapshot);
                    Assert.IsTrue(data.Add(view.VisualData), name);
                    foreach (var r in view.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        Assert.AreSame(master, r.sharedMaterial, r.name);
                        Assert.IsFalse(r.HasPropertyBlock(), r.name);
                        Assert.AreEqual(snapshot.VisualData, r.GetShaderUserValue(), r.name);
                    }
                }
                Assert.AreEqual(6, data.Count);
            }
        }

        [Test]
        public void Texts_ExistInEveryLanguage()
        {
            var keys = new List<string> { "species.squirrel.name" };
            keys.AddRange(Variants.Select(v => $"variant.squirrel.{v.Name}.name"));
            foreach (var language in new[] { "en", "fr" })
            {
                var table = AssetDatabase.LoadAssetAtPath<LocalizationTableAsset>($"Assets/_Game/Content/Localization/LOC_{language}.asset");
                Assert.IsNotNull(table, language);
                foreach (var key in keys)
                {
                    Assert.IsTrue(table.TryGet(key, out var value), language + " " + key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(value), language + " " + key);
                }
            }
        }

        [Test]
        public void GrasslandEggs_CanGiveASquirrel_TutorialEggStartsWithRabbit()
        {
            var registry = Registry();
            var basic = registry.Eggs.Get(ContentId.FromSource("egg.grassland.basic"));
            var focus = registry.Eggs.Get(ContentId.FromSource("egg.grassland.focus"));
            Assert.IsTrue(basic.Pool.Any(e => e.Species == Squirrel && e.Weight > 0.0));
            Assert.IsTrue(focus.Pool.Any(e => e.Species == Squirrel && e.Weight > 0.0));
            Assert.AreEqual(ContentId.FromSource("species.rabbit"), basic.StarterSpecies, "Vertical Slice: hatch the first rabbit.");
        }

        [Test]
        public void Codex_HasAThumbnailForEveryVariant_AndASilhouette()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>("Assets/_Game/Content/Visuals/DEF_CodexVisuals.asset");
            Assert.IsNotNull(catalog);
            Assert.IsTrue(catalog.TryGetSilhouette(Squirrel, out var silhouette) && silhouette != null);
            foreach (var (name, _) in Variants)
                Assert.IsTrue(catalog.TryGetVariant(ContentId.FromSource("variant.squirrel." + name), out var sprite) && sprite != null, name);
        }
    }
}
