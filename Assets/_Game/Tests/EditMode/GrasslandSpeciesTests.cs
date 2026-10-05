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
using UnityEditor.Animations;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T23 and T24: every grassland species after the Rabbit reuses the SmallQuadruped skeleton, family clips,
    /// controller and material. A species is data and source art only: same bones, six palette variants,
    /// texts, egg pools, Codex thumbnails. Low-bodied species swap family clips through data.
    /// </summary>
    public class GrasslandSpeciesTests
    {
        public sealed class Case
        {
            public string Name;
            public (string Variant, RarityTier Rarity)[] Variants;
            public (int Min, int Max)[] Lods;
            public override string ToString() => Name;
        }

        public static IEnumerable<Case> Species()
        {
            // A-SP-002 and A-SP-003 roster variants and LOD ranges.
            yield return new Case
            {
                Name = "Squirrel",
                Variants = new[] { ("red", RarityTier.Common), ("brown", RarityTier.Common), ("grey", RarityTier.Uncommon),
                    ("cream", RarityTier.Uncommon), ("black", RarityTier.Rare), ("autumn", RarityTier.Epic) },
                Lods = new[] { (2000, 2800), (800, 1100), (300, 500) },
            };
            yield return new Case
            {
                Name = "Hedgehog",
                Variants = new[] { ("brown", RarityTier.Common), ("cream", RarityTier.Common), ("grey", RarityTier.Uncommon),
                    ("black", RarityTier.Uncommon), ("white", RarityTier.Rare), ("mushroom", RarityTier.Epic) },
                Lods = new[] { (2200, 3000), (900, 1200), (350, 500) },
            };
        }

        private const string Family = "SmallQuadruped";
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

        private static ContentId SpeciesId(Case c) => ContentId.FromSource("species." + c.Name.ToLowerInvariant());
        private static ContentId VariantId(Case c, string variant) => ContentId.FromSource($"variant.{c.Name.ToLowerInvariant()}.{variant}");

        [TestCaseSource(nameof(Species))]
        public void Content_HasTheSixRosterVariants(Case c)
        {
            var registry = Registry();
            var id = SpeciesId(c);
            Assert.IsTrue(registry.Species.TryGet(id, out var species));
            Assert.AreEqual("PF_Animal_" + c.Name, species.VisualKey);
            Assert.AreEqual(RigFamily.SmallQuadruped, species.RigFamily);
            Assert.AreEqual(ContentId.FromSource("biome.grassland"), species.Biome);
            foreach (var (name, rarity) in c.Variants)
            {
                Assert.IsTrue(registry.Variants.TryGet(VariantId(c, name), out var variant), name);
                Assert.AreEqual(id, variant.Species, name);
                Assert.AreEqual(rarity, variant.Rarity, name);
            }
            int count = 0;
            for (int i = 0; i < registry.Variants.Count; i++)
                if (registry.Variants[i].Species == id)
                    count++;
            Assert.AreEqual(6, count);
        }

        [TestCaseSource(nameof(Species))]
        public void Model_ReusesTheFamilySkeleton_SameBonesSameBindPoses(Case c)
        {
            var rabbit = (ModelImporter)AssetImporter.GetAtPath(AnimalPipeline.ModelPath("Rabbit"));
            var model = (ModelImporter)AssetImporter.GetAtPath(AnimalPipeline.ModelPath(c.Name));
            var rabbitBones = rabbit.transformPaths.Where(p => p.Contains("SK_SmallQuadruped")).OrderBy(p => p).ToArray();
            var bones = model.transformPaths.Where(p => p.Contains("SK_SmallQuadruped")).OrderBy(p => p).ToArray();
            CollectionAssert.AreEqual(rabbitBones, bones, "Clips bind by path: the hierarchy must be identical.");

            var rabbitPoses = LoadLod0(AnimalPipeline.ModelPath("Rabbit")).bindposes;
            var poses = LoadLod0(AnimalPipeline.ModelPath(c.Name)).bindposes;
            Assert.AreEqual(rabbitPoses.Length, poses.Length);
            foreach (var pose in rabbitPoses)
                Assert.IsTrue(poses.Any(p => Same(p, pose)), "A bone moved: family clips would not fit.");
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

        [TestCaseSource(nameof(Species))]
        public void Lods_AreInsideTheSpeciesBudgets(Case c)
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(AnimalPipeline.ModelPath(c.Name)).OfType<Mesh>()
                .OrderBy(m => m.name).ToArray();
            Assert.AreEqual(3, meshes.Length);
            for (int lod = 0; lod < 3; lod++)
            {
                int tris = meshes[lod].triangles.Length / 3;
                Assert.That(tris, NUnit.Framework.Is.InRange(c.Lods[lod].Min, c.Lods[lod].Max), meshes[lod].name);
            }
            Assert.IsEmpty(AssetValidator.ValidateAnimalPrefab(AnimalPipeline.PrefabPath(c.Name)).Where(i => i.Severity == IssueSeverity.Error));
        }

        [TestCaseSource(nameof(Species))]
        public void Prefab_UsesTheFamilyControllerAndTheSharedMaterial(Case c)
        {
            var view = AssetDatabase.LoadAssetAtPath<AnimalView>(AnimalPipeline.PrefabPath(c.Name));
            Assert.IsNotNull(view);
            var family = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimalPipeline.ControllerPath(Family));
            var runtime = view.Animator.runtimeAnimatorController;
            if (runtime is AnimatorOverrideController aoc)
            {
                // Overrides swap family clips only: same states, no species-only clip.
                Assert.AreSame(family, aoc.runtimeAnimatorController);
                var familyClips = AssetDatabase.LoadAllAssetsAtPath(AnimalPipeline.ClipsPath(Family)).OfType<AnimationClip>().ToList();
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                aoc.GetOverrides(pairs);
                foreach (var pair in pairs.Where(p => p.Value != null))
                    Assert.Contains(pair.Value, familyClips, pair.Value.name);
            }
            else
            {
                Assert.AreSame(family, runtime);
            }
            var master = AssetDatabase.LoadAssetAtPath<Material>(AnimalPipeline.MaterialPath);
            foreach (var r in view.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Assert.AreSame(master, r.sharedMaterial, r.name);
            foreach (var socket in new[] { "SOCK_HeadTop", "SOCK_Neck", "SOCK_Back", "SOCK_TailBase" })
                Assert.IsNotNull(AnimalPipeline.FindDeep(view.transform, socket), socket);
        }

        [TestCaseSource(nameof(Species))]
        public void SixVariants_OnOnePrefab_DistinctData_NoMaterialClone(Case c)
        {
            var registry = Registry();
            using (var palettes = new PaletteTable(registry))
            {
                var resolver = new AnimalVisualResolver(registry, palettes);
                var prefab = AssetDatabase.LoadAssetAtPath<AnimalView>(AnimalPipeline.PrefabPath(c.Name));
                var master = AssetDatabase.LoadAssetAtPath<Material>(AnimalPipeline.MaterialPath);
                var data = new HashSet<uint>();
                uint seed = 1;
                foreach (var (name, _) in c.Variants)
                {
                    var view = Object.Instantiate(prefab);
                    _spawned.Add(view.gameObject);
                    var animal = new AnimalInstance { InstanceId = seed, Seed = seed++, Species = SpeciesId(c), Variant = VariantId(c, name) };
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

        [TestCaseSource(nameof(Species))]
        public void Texts_ExistInEveryLanguage(Case c)
        {
            string lower = c.Name.ToLowerInvariant();
            var keys = new List<string> { $"species.{lower}.name" };
            keys.AddRange(c.Variants.Select(v => $"variant.{lower}.{v.Variant}.name"));
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

        [TestCaseSource(nameof(Species))]
        public void GrasslandEggs_CanGiveIt(Case c)
        {
            var registry = Registry();
            foreach (var egg in new[] { "egg.grassland.basic", "egg.grassland.focus" })
                Assert.IsTrue(registry.Eggs.Get(ContentId.FromSource(egg)).Pool.Any(e => e.Species == SpeciesId(c) && e.Weight > 0.0), egg);
        }

        [TestCaseSource(nameof(Species))]
        public void Codex_HasAThumbnailForEveryVariant_AndASilhouette(Case c)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>("Assets/_Game/Content/Visuals/DEF_CodexVisuals.asset");
            Assert.IsNotNull(catalog);
            Assert.IsTrue(catalog.TryGetSilhouette(SpeciesId(c), out var silhouette) && silhouette != null);
            foreach (var (name, _) in c.Variants)
                Assert.IsTrue(catalog.TryGetVariant(VariantId(c, name), out var sprite) && sprite != null, name);
        }

        [Test]
        public void TutorialEgg_StillStartsWithARabbit()
        {
            var basic = Registry().Eggs.Get(ContentId.FromSource("egg.grassland.basic"));
            Assert.AreEqual(ContentId.FromSource("species.rabbit"), basic.StarterSpecies, "Vertical Slice: hatch the first rabbit.");
        }

        [Test]
        public void Hedgehog_WalksAndBounces_WithFamilyClips()
        {
            var view = AssetDatabase.LoadAssetAtPath<AnimalView>(AnimalPipeline.PrefabPath("Hedgehog"));
            var aoc = view.Animator.runtimeAnimatorController as AnimatorOverrideController;
            Assert.IsNotNull(aoc, "Low-bodied species override the family clips.");
            Assert.AreEqual("AN_SmallQuadruped_Locomotion_Walk", aoc["AN_SmallQuadruped_Locomotion_Hop"].name);
            Assert.AreEqual("AN_SmallQuadruped_Happy_Bounce", aoc["AN_SmallQuadruped_Happy"].name);
            Assert.IsTrue(aoc["AN_SmallQuadruped_Locomotion_Hop"].isLooping, "The walk loops like the hop it replaces.");
            // Same length as Happy: the shared behavior timing still fits.
            Assert.AreEqual(aoc["AN_SmallQuadruped_Happy"].length, AssetDatabase.LoadAllAssetsAtPath(AnimalPipeline.ClipsPath(Family))
                .OfType<AnimationClip>().First(x => x.name == "AN_SmallQuadruped_Happy").length, 0.01f);

            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            Assert.IsTrue(catalog.TryGet("PF_Animal_Hedgehog", out var hedgehog));
            Assert.IsTrue(catalog.TryGet("PF_Animal_Rabbit", out var rabbit));
            Assert.Less(hedgehog.behavior.moveSpeed, rabbit.behavior.moveSpeed, "Short legs scurry slower than a hop.");
        }
    }
}
