using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T11: data to view. One prefab, one material, every variant from data only.</summary>
    public class AnimalVisualTests
    {
        private const string PrefabPath = "Assets/_Game/Prefabs/Animals/PF_Animal_Rabbit.prefab";

        private ContentRegistry _registry;
        private PaletteTable _palettes;
        private AnimalVisualResolver _resolver;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            _registry = database.CreateBuilder().Build();
            _palettes = new PaletteTable(_registry);
            _resolver = new AnimalVisualResolver(_registry, _palettes);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                Object.DestroyImmediate(go);
            _spawned.Clear();
            _palettes.Dispose();
        }

        private static AnimalInstance Rabbit(string variant, uint seed) => new AnimalInstance
        {
            InstanceId = seed,
            Species = ContentId.FromSource("species.rabbit"),
            Variant = ContentId.FromSource(variant),
            Seed = seed,
        };

        [Test]
        public void SeedHash_IsFrozen()
        {
            Assert.AreEqual(0u, SeedHash.Mix(0u));
            Assert.AreEqual(0x688990c0u, SeedHash.Mix(1u));
            Assert.AreEqual(0xe628c683u, SeedHash.Mix(0xdeadbeefu));
            Assert.AreEqual(0x4644701eu, SeedHash.Mix(42u, 7u));
        }

        [Test]
        public void IdlePhase_IsStableAndSpread()
        {
            double sum = 0;
            int lowHalf = 0;
            for (uint seed = 1; seed <= 4000; seed++)
            {
                float phase = AnimalVisualResolver.IdlePhase(seed);
                Assert.That(phase, NUnit.Framework.Is.InRange(0f, 0.99999994f));
                Assert.AreEqual(phase, AnimalVisualResolver.IdlePhase(seed));
                sum += phase;
                if (phase < 0.5f) lowHalf++;
            }
            Assert.AreEqual(0.5, sum / 4000, 0.02);
            Assert.AreEqual(2000, lowHalf, 120);
        }

        [Test]
        public void Resolver_PacksPaletteRowAndPattern()
        {
            var spotted = ContentId.FromSource("variant.rabbit.spotted");
            Assert.IsTrue(_palettes.TryGetRow(spotted, out int row));
            Assert.IsTrue(_resolver.TryResolve(Rabbit("variant.rabbit.spotted", 9), new Vector3(1f, 0f, 2f), 90f, out var snapshot));
            Assert.AreEqual(row, AnimalVisualPacking.PaletteRow(snapshot.VisualData));
            Assert.AreEqual(PatternChannel.MaskA, AnimalVisualPacking.Pattern(snapshot.VisualData));
            Assert.AreEqual(15, AnimalVisualPacking.PatternStrength(snapshot.VisualData));
            Assert.AreEqual(9u, snapshot.InstanceId);
            Assert.AreEqual(90f, snapshot.Yaw);
            Assert.AreEqual(AnimalVisualResolver.IdlePhase(9), snapshot.IdlePhase);
        }

        [Test]
        public void Resolver_EverySixRabbitVariantsGetDistinctData()
        {
            var seen = new HashSet<uint>();
            foreach (var id in new[] { "brown", "white", "grey", "black", "spotted", "sakura" })
            {
                Assert.IsTrue(_resolver.TryResolve(Rabbit("variant.rabbit." + id, 1), Vector3.zero, 0f, out var s), id);
                Assert.IsTrue(seen.Add(s.VisualData), id);
            }
        }

        [Test]
        public void Resolver_UnknownVariant_FallsBackToSpeciesDefault()
        {
            _resolver.TryResolve(Rabbit("variant.rabbit.brown", 1), Vector3.zero, 0f, out var brown);
            Assert.IsTrue(_resolver.TryResolve(Rabbit("variant.rabbit.removed_in_v2", 1), Vector3.zero, 0f, out var fallback));
            Assert.AreEqual(brown.VisualData, fallback.VisualData);

            var ghost = Rabbit("variant.ghost", 1);
            ghost.Species = ContentId.FromSource("species.ghost");
            Assert.IsFalse(_resolver.TryResolve(ghost, Vector3.zero, 0f, out _));
        }

        [Test]
        public void Resolve_DoesNotAllocate()
        {
            var animal = Rabbit("variant.rabbit.sakura", 77);
            _resolver.TryResolve(animal, Vector3.zero, 0f, out _);
            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                    _resolver.TryResolve(animal, Vector3.zero, i, out _);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void SixVariants_OnRealPrefab_ShareOneMaterial_NoClone_NoPropertyBlock()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<AnimalView>(PrefabPath);
            var master = AssetDatabase.LoadAssetAtPath<Material>(AnimalPipeline.MaterialPath);
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(master);

            var data = new HashSet<uint>();
            int i = 0;
            foreach (var id in new[] { "brown", "white", "grey", "black", "spotted", "sakura" })
            {
                var view = Object.Instantiate(prefab);
                _spawned.Add(view.gameObject);
                _resolver.TryResolve(Rabbit("variant.rabbit." + id, (uint)(i + 1)), new Vector3(i++ * 0.5f, 0f, 0f), 0f, out var snapshot);
                view.Configure(snapshot);
                data.Add(view.VisualData);

                foreach (var r in view.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name == "BlobShadow")
                    {
                        // Low-quality ground contact: its own shared material, never a clone.
                        Assert.AreSame(AssetDatabase.LoadAssetAtPath<Material>(AssetValidator.BlobShadowMaterialPath), r.sharedMaterial);
                        Assert.IsFalse(r.HasPropertyBlock());
                        continue;
                    }
                    Assert.AreEqual(1, r.sharedMaterials.Length, r.name);
                    Assert.AreSame(master, r.sharedMaterial, r.name + " uses a material clone.");
                    Assert.IsFalse(r.HasPropertyBlock(), r.name + " uses a MaterialPropertyBlock.");
                    Assert.AreEqual(snapshot.VisualData, GetUserValue(r), r.name);
                }
            }
            Assert.AreEqual(6, data.Count);
        }

        private static uint GetUserValue(Renderer r)
        {
            if (r is SkinnedMeshRenderer s) return s.GetShaderUserValue();
            if (r is MeshRenderer m) return m.GetShaderUserValue();
            return 0u;
        }
    }
}
