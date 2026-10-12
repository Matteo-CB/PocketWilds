using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Audio;
using PocketWilds.Editor.Eggs;
using PocketWilds.Editor.Production;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T51 the first Forest species (doc 03 Roster, A-SP-007 to A-SP-011): Fox and Badger on the MediumQuadruped skeleton,
    /// Raccoon and Red Panda on the SmallQuadruped skeleton with the Rabbit's bind poses, their prefab scaled to their size.
    /// Rig reuse and no code exception: same bones, family controller and material, one species clip each swapped through data,
    /// six roster colors, mastery 10/20/30, secret and visitor colors, texts, voices, the Forest eggs, Codex thumbnails.
    /// T52 the Owl (A-SP-010, SK_Bird) and the Boar Piglet (A-SP-012, SK_HeavyQuadruped): each the first species of its own
    /// family, playing the family controller as is (its minimal library is its behavior), modelled at the doc's size.
    /// </summary>
    public class ForestSpeciesTests
    {
        public sealed class Case
        {
            public string Name;
            public string Family;
            public (string Variant, RarityTier Rarity)[] Variants;
            public (int Min, int Max)[] Lods;
            public (string State, string Clip) SpeciesClip;
            public string[] Mastery;
            public string[] Secrets;
            public string Visitor;
            public float Pitch;
            /// <summary>Sockets the species hides (the owl has no ears); every other socket is supported (doc 03).</summary>
            public AccessorySocket[] Hidden = new AccessorySocket[0];
            /// <summary>T52 doc 03 « silhouette mesurée » (length x height, meters) of a species modelled at its size, or 0.</summary>
            public (float Length, float Height) Size;
            public override string ToString() => Name;
        }

        public static IEnumerable<Case> Species()
        {
            yield return new Case
            {
                Name = "Fox", Family = AnimalPipeline.MediumFamily,
                Variants = new[] { ("red", RarityTier.Common), ("silver", RarityTier.Common), ("cream", RarityTier.Uncommon),
                    ("cross", RarityTier.Uncommon), ("white", RarityTier.Rare), ("firefly", RarityTier.Epic) },
                Lods = new[] { (2800, 3800), (1100, 1500), (400, 650) },
                SpeciesClip = ("Sleep_Loop", "FoxCurlSleep_Loop"),
                Mastery = new[] { "brick", "mist", "goldleaf" }, Secrets = new[] { "ember", "bilberry" }, Visitor = "golden_visitor", Pitch = 1.05f,
            };
            yield return new Case
            {
                Name = "Badger", Family = AnimalPipeline.MediumFamily,
                Variants = new[] { ("classic", RarityTier.Common), ("honey", RarityTier.Common), ("grey", RarityTier.Uncommon),
                    ("dark", RarityTier.Uncommon), ("cream", RarityTier.Rare), ("moss", RarityTier.Epic) },
                Lods = new[] { (2500, 3400), (1000, 1300), (350, 600) },
                SpeciesClip = ("Inspect", "BadgerDig"),
                Mastery = new[] { "charcoal", "sage", "amber" }, Secrets = new[] { "licorice" }, Visitor = "amber_visitor", Pitch = 0.80f,
            };
            yield return new Case
            {
                Name = "Raccoon", Family = AnimalPipeline.SmallFamily,
                Variants = new[] { ("classic", RarityTier.Common), ("dark", RarityTier.Common), ("cream", RarityTier.Uncommon),
                    ("grey", RarityTier.Uncommon), ("cinnamon", RarityTier.Rare), ("moonmask", RarityTier.Epic) },
                Lods = new[] { (2200, 3000), (900, 1200), (350, 500) },
                SpeciesClip = ("Inspect", "RaccoonWash"),
                Mastery = new[] { "slate", "milktea", "goldenrings" }, Secrets = new[] { "morningmist" }, Pitch = 1.05f,
            };
            yield return new Case
            {
                Name = "RedPanda", Family = AnimalPipeline.SmallFamily,
                Variants = new[] { ("classic", RarityTier.Common), ("golden", RarityTier.Common), ("cream", RarityTier.Uncommon),
                    ("dark", RarityTier.Uncommon), ("snow", RarityTier.Rare), ("bamboo", RarityTier.Epic) },
                Lods = new[] { (2600, 3400), (1000, 1400), (400, 600) },
                SpeciesClip = ("Idle_B", "RedPandaStand"),
                Mastery = new[] { "rust", "blackberry", "goldenlantern" }, Secrets = new[] { "cherry" }, Pitch = 1.12f,
            };
            // T52: no species clip, the family library is the species' own (SpeciesClip left empty).
            yield return new Case
            {
                Name = "Owl", Family = AnimalPipeline.BirdFamily,
                Variants = new[] { ("brown", RarityTier.Common), ("tawny", RarityTier.Common), ("grey", RarityTier.Uncommon),
                    ("cream", RarityTier.Uncommon), ("dark", RarityTier.Rare), ("constellation", RarityTier.Epic) },
                Lods = new[] { (2000, 2800), (800, 1100), (300, 500) },
                Mastery = new[] { "cocoa", "periwinkle", "honeywings" }, Secrets = new[] { "fullmoon" }, Visitor = "russet_visitor", Pitch = 0.82f,
                Hidden = new[] { AccessorySocket.EarL, AccessorySocket.EarR }, Size = (0.36f, 0.44f),
            };
            yield return new Case
            {
                Name = "BoarPiglet", Family = AnimalPipeline.HeavyFamily,
                Variants = new[] { ("brown", RarityTier.Common), ("chestnutstripe", RarityTier.Common), ("grey", RarityTier.Uncommon),
                    ("black", RarityTier.Uncommon), ("cream", RarityTier.Rare), ("acorn", RarityTier.Epic) },
                Lods = new[] { (2500, 3500), (1000, 1400), (400, 650) },
                Mastery = new[] { "walnut", "powderpink", "goldentruffle" }, Secrets = new[] { "mintstripe" }, Pitch = 0.95f,
                Size = (0.66f, 0.42f),
            };
        }

        private static readonly ContentId Forest = ContentId.FromSource("biome.forest");
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

        private static string Lower(Case c) => c.Name.ToLowerInvariant();
        private static RigFamily Rig(Case c) => (RigFamily)System.Enum.Parse(typeof(RigFamily), c.Family);
        private static ContentId SpeciesId(Case c) => ContentId.FromSource("species." + Lower(c));
        private static ContentId VariantId(Case c, string variant) => ContentId.FromSource($"variant.{Lower(c)}.{variant}");

        [TestCaseSource(nameof(Species))]
        public void Content_HasTheSixRosterVariants_InTheForest(Case c)
        {
            var registry = Registry();
            var id = SpeciesId(c);
            Assert.IsTrue(registry.Species.TryGet(id, out var species));
            Assert.AreEqual("PF_Animal_" + c.Name, species.VisualKey);
            Assert.AreEqual(Rig(c), species.RigFamily, "the family name is the RigFamily name");
            Assert.AreEqual(Forest, species.Biome);
            Assert.IsFalse(species.IsSecret);
            foreach (var socket in new[] { AccessorySocket.HeadTop, AccessorySocket.Neck, AccessorySocket.Back, AccessorySocket.TailBase,
                         AccessorySocket.EarL, AccessorySocket.EarR })
                Assert.AreEqual(c.Hidden.Contains(socket), species.HidesSocket(socket), socket + ": doc 03 « Sockets supportés ».");
            foreach (var (name, rarity) in c.Variants)
            {
                Assert.IsTrue(registry.Variants.TryGet(VariantId(c, name), out var variant), name);
                Assert.AreEqual(id, variant.Species, name);
                Assert.AreEqual(rarity, variant.Rarity, name);
                Assert.IsFalse(variant.IsSecret, name);
            }
            int count = 0;
            for (int i = 0; i < registry.Variants.Count; i++)
                if (registry.Variants[i].Species == id && !registry.Variants[i].IsSecret)
                    count++;
            Assert.AreEqual(6, count, "the six roster colors (mastery, secret and visitor colors are secret)");
        }

        [TestCaseSource(nameof(Species))]
        public void Production_IsTheForestsBase(Case c)
        {
            var registry = Registry();
            var config = new ProgressionConfig();
            var forest = config.Biomes[config.BiomeOrder(Forest)];
            Assert.AreEqual(8.0, forest.BaseProduction, 1e-9, "ProgressionConfig: the Forest's base production.");
            Assert.AreEqual(forest.BaseProduction, registry.Species.Get(SpeciesId(c)).BaseProduction, 1e-9);
            Assert.AreEqual(1.0, registry.Species.Get(ContentId.FromSource("species.rabbit")).BaseProduction, 1e-9, "Grassland species unchanged.");
        }

        [TestCaseSource(nameof(Species))]
        public void MasterySecretAndVisitorColors_AreSecret_AndKeepTheirRoles(Case c)
        {
            var registry = Registry();
            var levels = new List<int>();
            for (int i = 0; i < c.Mastery.Length; i++)
            {
                Assert.IsTrue(registry.Variants.TryGet(VariantId(c, c.Mastery[i]), out var variant), c.Mastery[i]);
                Assert.IsTrue(variant.IsSecret, c.Mastery[i] + ": a mastery color is never in eggs.");
                Assert.IsFalse(variant.IsVisitor, c.Mastery[i]);
                Assert.AreEqual((i + 1) * 10, variant.MasteryLevel, c.Mastery[i]);
                levels.Add(variant.MasteryLevel);
            }
            for (int i = 0; i < registry.Variants.Count; i++)
                if (registry.Variants[i].Species == SpeciesId(c) && registry.Variants[i].MasteryLevel > 0)
                    Assert.Contains(registry.Variants[i].MasteryLevel, levels);
            Assert.AreEqual(RarityTier.Legendary, registry.Variants.Get(VariantId(c, c.Mastery[2])).Rarity, "Mastery 30 is Legendary.");
            foreach (var secret in c.Secrets)
            {
                Assert.IsTrue(registry.Variants.TryGet(VariantId(c, secret), out var variant), secret);
                Assert.IsTrue(variant.IsSecret, secret);
                Assert.IsFalse(variant.IsVisitor, secret);
                Assert.AreEqual(0, variant.MasteryLevel, secret);
            }
            if (c.Visitor == null)
                return;
            Assert.IsTrue(registry.Variants.TryGet(VariantId(c, c.Visitor), out var visitor), c.Visitor);
            Assert.IsTrue(visitor.IsSecret && visitor.IsVisitor, c.Visitor + ": palette only, never in eggs.");
            Assert.AreEqual(0, visitor.MasteryLevel);
            int row = VisitorData.IndexOfVariant(visitor.Id);
            Assert.GreaterOrEqual(row, 0, "VisitorData lists it.");
            Assert.AreEqual(SpeciesId(c), VisitorData.Row(row).Species);
        }

        [TestCaseSource(nameof(Species))]
        public void Model_ReusesTheFamilySkeleton_SameBonesSameBindPoses(Case c)
        {
            string skeleton = "SK_" + c.Family;
            bool small = c.Family == AnimalPipeline.SmallFamily;
            // The family's reference skeleton: the Rabbit for the small family, the family library (AN_<Family>.fbx, the
            // armature only) for every other family.
            string referencePath = small ? AnimalPipeline.ModelPath("Rabbit") : AnimalPipeline.ClipsPath(c.Family);
            var reference = (ModelImporter)AssetImporter.GetAtPath(referencePath);
            Assert.IsNotNull(reference, referencePath);
            var model = (ModelImporter)AssetImporter.GetAtPath(AnimalPipeline.ModelPath(c.Name));
            Assert.IsNotNull(model, AnimalPipeline.ModelPath(c.Name));
            var referenceBones = reference.transformPaths.Where(p => p.Contains(skeleton) && !p.Contains("/SOCK_")).OrderBy(p => p).ToArray();
            var bones = model.transformPaths.Where(p => p.Contains(skeleton) && !p.Contains("/SOCK_")).OrderBy(p => p).ToArray();
            CollectionAssert.AreEqual(referenceBones, bones, "Clips bind by path: the hierarchy must be identical.");
            if (!small)
                return;
            // The small skeleton keeps the Rabbit's bind poses: the species size comes from the prefab's uniform scale.
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
            Assert.IsTrue(AssetValidator.LodBudgets.TryGetValue(c.Name, out var budget), "AssetValidator.LodBudgets row.");
            CollectionAssert.AreEqual(c.Lods.Select(l => l.Max).ToArray(), budget, "the validator ceilings are the doc's.");
            var meshes = AssetDatabase.LoadAllAssetsAtPath(AnimalPipeline.ModelPath(c.Name)).OfType<Mesh>()
                .Where(m => !AnimalPipeline.IsModule(m.name)).OrderBy(m => m.name).ToArray();
            Assert.AreEqual(3, meshes.Length);
            for (int lod = 0; lod < 3; lod++)
            {
                int tris = meshes[lod].triangles.Length / 3;
                Assert.That(tris, NUnit.Framework.Is.InRange(c.Lods[lod].Min, c.Lods[lod].Max), meshes[lod].name);
            }
            Assert.IsEmpty(AssetValidator.ValidateAnimalPrefab(AnimalPipeline.PrefabPath(c.Name)).Where(i => i.Severity == IssueSeverity.Error));
        }

        [TestCaseSource(nameof(Species))]
        public void Prefab_UsesTheFamilyController_ItsSpeciesClip_AndTheSharedMaterial(Case c)
        {
            var view = AssetDatabase.LoadAssetAtPath<AnimalView>(AnimalPipeline.PrefabPath(c.Name));
            Assert.IsNotNull(view);
            var family = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimalPipeline.ControllerPath(c.Family));
            Assert.IsNotNull(family, AnimalPipeline.ControllerPath(c.Family));
            if (c.SpeciesClip.Clip == null)
            {
                // T52: the first species of its family plays the family controller itself.
                Assert.AreSame(family, view.Animator.runtimeAnimatorController, "no override: the family library is the species' own");
            }
            else
            {
                var aoc = view.Animator.runtimeAnimatorController as AnimatorOverrideController;
                Assert.IsNotNull(aoc, "The species clip swaps a family clip through an override.");
                Assert.AreSame(family, aoc.runtimeAnimatorController);
                var familyClips = AssetDatabase.LoadAllAssetsAtPath(AnimalPipeline.ClipsPath(c.Family)).OfType<AnimationClip>().ToList();
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                aoc.GetOverrides(pairs);
                foreach (var pair in pairs.Where(p => p.Value != null))
                    Assert.Contains(pair.Value, familyClips, pair.Value.name + ": family clips only.");
                Assert.AreEqual($"AN_{c.Family}_{c.SpeciesClip.Clip}", aoc[$"AN_{c.Family}_{c.SpeciesClip.State}"].name);
            }

            var master = AssetDatabase.LoadAssetAtPath<Material>(AnimalPipeline.MaterialPath);
            foreach (var r in view.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Assert.AreSame(master, r.sharedMaterial, r.name);
            foreach (var socket in new[] { "SOCK_HeadTop", "SOCK_Neck", "SOCK_Back", "SOCK_TailBase" })
                Assert.IsNotNull(AnimalPipeline.FindDeep(view.transform, socket), socket);
            foreach (var (socket, name) in new[] { (AccessorySocket.EarL, "SOCK_Ear_L"), (AccessorySocket.EarR, "SOCK_Ear_R") })
                if (!c.Hidden.Contains(socket))
                    Assert.IsNotNull(AnimalPipeline.FindDeep(view.transform, name), name + ": exposed by AssetImportRules.FamilySockets");
        }

        [TestCaseSource(nameof(Species))]
        public void Size_ScaledSmallSpeciesMeasureTheirRosterLength_OthersKeepScaleOne(Case c)
        {
            var view = AssetDatabase.LoadAssetAtPath<AnimalView>(AnimalPipeline.PrefabPath(c.Name));
            var visual = view.transform.Find("VisualRoot");
            Assert.IsNotNull(visual);
            var scale = visual.localScale;
            Assert.AreEqual(scale.x, scale.y, 1e-5f, "uniform scale only");
            Assert.AreEqual(scale.x, scale.z, 1e-5f, "uniform scale only");
            var body = AnimalPipeline.MeasureBody(view.gameObject);
            if (AnimalPipeline.ScaledLengths.TryGetValue(c.Name, out float length))
            {
                Assert.AreEqual(c.Family, AnimalPipeline.SmallFamily, "only a family skeleton shown bigger is scaled");
                Assert.That(scale.x, NUnit.Framework.Is.InRange(AnimalPipeline.MinSpeciesScale, AnimalPipeline.MaxSpeciesScale));
                Assert.AreEqual(length, body.front - body.back - 0.02f, 0.01f, "nose to tail tip at bind pose");
            }
            else
            {
                Assert.AreEqual(1f, scale.x, 1e-5f, "medium, heavy and bird species are modelled at their size");
            }
            if (c.Size.Length > 0f)
            {
                // The doc's size is indicative: within a fifth of it (an export scale error is far off).
                Assert.AreEqual(c.Size.Length, body.front - body.back - 0.02f, c.Size.Length * 0.2f, "length at bind pose (doc 03)");
                Assert.AreEqual(c.Size.Height, body.height - 0.01f, c.Size.Height * 0.2f, "height at bind pose (doc 03)");
            }
            // The measured body in the catalog is the scaled one (no-collision rule).
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            Assert.IsTrue(catalog.TryGet("PF_Animal_" + c.Name, out var entry));
            Assert.AreEqual(body.front, entry.behavior.body.front, 1e-3f, "Measure Bodies after the build.");
            Assert.AreEqual(body.height, entry.behavior.body.height, 1e-3f);
        }

        [Test]
        public void Behavior_FamiliesGreetTheirOwnSize_TheBadgerIsSlowerThanTheFox()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            AnimalVisualCatalog.Entry Entry(string name)
            {
                Assert.IsTrue(catalog.TryGet("PF_Animal_" + name, out var entry), name);
                return entry;
            }
            Assert.AreEqual(Entry("Fawn").behavior.socialGroup, Entry("Fox").behavior.socialGroup, "medium animals greet each other");
            Assert.AreEqual(AnimalPipeline.MediumSocialGroup, Entry("Badger").behavior.socialGroup);
            Assert.AreEqual(AnimalPipeline.ScaledSmallSocialGroup, Entry("Raccoon").behavior.socialGroup);
            Assert.AreEqual(AnimalPipeline.ScaledSmallSocialGroup, Entry("RedPanda").behavior.socialGroup);
            Assert.AreNotEqual(Entry("Rabbit").behavior.socialGroup, Entry("Raccoon").behavior.socialGroup, "a scaled skeleton never greets a rabbit");
            Assert.Less(Entry("Badger").behavior.moveSpeed, Entry("Fox").behavior.moveSpeed);
            Assert.Greater(Entry("Raccoon").behavior.inspectWeight, Entry("Rabbit").behavior.inspectWeight, "doc 02: Inspect weighted higher");
        }

        [Test]
        public void Behavior_NewFamiliesGreetTheirOwnFamily_AndHoldTheirOwnClips()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            AnimalVisualCatalog.Entry Entry(string name)
            {
                Assert.IsTrue(catalog.TryGet("PF_Animal_" + name, out var entry), name);
                return entry;
            }
            var groups = new[] { 0, AnimalPipeline.MediumSocialGroup, AnimalPipeline.ScaledSmallSocialGroup, AnimalPipeline.HeavySocialGroup,
                AnimalPipeline.BirdSocialGroup };
            Assert.AreEqual(groups.Length, groups.Distinct().Count(), "one greeting group per skeleton");
            Assert.AreEqual(AnimalPipeline.BirdSocialGroup, Entry("Owl").behavior.socialGroup);
            Assert.AreEqual(AnimalPipeline.HeavySocialGroup, Entry("BoarPiglet").behavior.socialGroup);
            Assert.Greater(Entry("Owl").behavior.sleepWeight, Entry("Rabbit").behavior.sleepWeight, "doc 03: Sleepy");
            Assert.Greater(Entry("BoarPiglet").behavior.inspectWeight, Entry("Rabbit").behavior.inspectWeight, "doc 03: the snout first");
            // One-shots hand over when the Animator is back in Idle: the durations are the family library's, not the small one's.
            foreach (var (name, family) in new[] { ("Owl", AnimalPipeline.BirdFamily), ("BoarPiglet", AnimalPipeline.HeavyFamily) })
            {
                var b = Entry(name).behavior;
                foreach (var (suffix, seconds) in new[] { ("Idle_B", b.idleLookSeconds), ("Inspect", b.inspectSeconds), ("ReactTouch", b.reactSeconds),
                             ("Happy", b.happySeconds), ("Sleep_Exit", b.wakeSeconds), ("Social_Nose", b.socialSeconds) })
                {
                    float expected = AnimalPipeline.OneShotSeconds(family, suffix, -1f);
                    Assert.Greater(expected, 0f, $"AN_{family}_{suffix} is in the library");
                    Assert.AreEqual(expected, seconds, 1e-3f, $"{name} {suffix}: run the species build after the library import");
                }
            }
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
            string lower = Lower(c);
            var keys = new List<string> { $"species.{lower}.name" };
            keys.AddRange(c.Variants.Select(v => $"variant.{lower}.{v.Variant}.name"));
            keys.AddRange(c.Mastery.Select(v => $"variant.{lower}.{v}.name"));
            keys.AddRange(c.Secrets.Select(v => $"variant.{lower}.{v}.name"));
            keys.AddRange(c.Secrets.Select(v => $"variant.{lower}.{v}.hint"));
            if (c.Visitor != null)
                keys.Add($"variant.{lower}.{c.Visitor}.name");
            keys.AddRange(new[] { "egg.forest.basic.name", "egg.forest.focus.name", "biome.forest.mastery" });
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
        public void Voice_IsTheSmallBankAtTheSpeciesPitch(Case c)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioPipeline.CatalogPath);
            Assert.IsNotNull(catalog);
            var voice = catalog.FindSpecies(SpeciesId(c));
            Assert.IsNotNull(voice);
            Assert.AreEqual(ContentId.FromSource(voice.species), SpeciesId(c), "its own row, not the family fallback");
            Assert.AreEqual(c.Pitch, voice.pitch, 1e-4f);
            Assert.IsNotEmpty(voice.happy);
            Assert.IsNotEmpty(voice.sleep);
            Assert.IsNotEmpty(voice.eat);
        }

        [TestCaseSource(nameof(Species))]
        public void ForestEggs_CanGiveIt_GrasslandEggsNever(Case c)
        {
            var registry = Registry();
            foreach (var egg in new[] { "egg.forest.basic", "egg.forest.focus" })
                Assert.IsTrue(registry.Eggs.Get(ContentId.FromSource(egg)).Pool.Any(e => e.Species == SpeciesId(c) && e.Weight > 0.0), egg);
            foreach (var egg in new[] { "egg.grassland.basic", "egg.grassland.focus" })
                Assert.IsFalse(registry.Eggs.Get(ContentId.FromSource(egg)).Pool.Any(e => e.Species == SpeciesId(c)), egg);
        }

        [Test]
        public void ForestEggs_FollowTheGrasslandRules_AtTheForestsPrice()
        {
            var registry = Registry();
            var basic = registry.Eggs.Get(ContentId.FromSource("egg.forest.basic"));
            var focus = registry.Eggs.Get(ContentId.FromSource("egg.forest.focus"));
            Assert.AreEqual(Forest, basic.Biome);
            Assert.AreEqual(Forest, focus.Biome);
            Assert.AreEqual(EggTier.Basic, basic.Tier);
            Assert.AreEqual(EggTier.Focus, focus.Tier);
            var config = new ProgressionConfig();
            double b = config.Biomes[config.BiomeOrder(Forest)].EggBase;
            Assert.AreEqual(300.0, b, 1e-9);
            Assert.AreEqual(b, basic.BaseCost, 1e-9, "C-EGG-004: 300 x 1.18^n");
            Assert.AreEqual(4.0 * b, focus.BaseCost, 1e-9, "C-EGG-005: 1 200 x 1.18^n");
            Assert.AreEqual(1.18, basic.CostGrowth, 1e-9);
            Assert.AreEqual(1.18, focus.CostGrowth, 1e-9);
            CollectionAssert.AreEqual(new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 }, basic.RarityWeights);
            CollectionAssert.AreEqual(new[] { 50.0, 30.0, 14.0, 4.5, 1.3, 0.2 }, focus.RarityWeights);
            Assert.AreEqual(0, basic.UnlockBiomeMastery);
            Assert.AreEqual(3, focus.UnlockBiomeMastery, "from Forest mastery 3");
            Assert.AreEqual(3, focus.FocusTargetCount);
            Assert.AreEqual(ContentId.None, basic.StarterSpecies, "only the Meadow egg starts the game");
            foreach (var egg in new[] { basic, focus })
            {
                CollectionAssert.AreEquivalent(EggPipeline.ForestSpecies.Select(s => ContentId.FromSource(s)).ToArray(), egg.Pool.Select(e => e.Species).ToArray(), egg.Id.ToString());
                Assert.IsTrue(egg.Pool.All(e => registry.Species.Get(e.Species).Biome == Forest), "Forest species only");
            }
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
        public void Forest_StaysComingSoonForPlayers_TheMeadowIsOpen()
        {
            Assert.IsFalse(BiomeWizard.ForestSpec().OpenToPlayers, "the Forest waits for the bear cub (T53)");
            Assert.IsTrue(BiomeWizard.GrasslandSpec().OpenToPlayers);
            var registry = Registry();
            Assert.IsTrue(registry.Biomes.TryGet(Forest, out var forest));
            Assert.IsFalse(forest.OpenToPlayers, "DEF_Biome_Forest: run Update Biome Open Flags (T51).");
            Assert.IsTrue(registry.Biomes.TryGet(ContentId.FromSource("biome.grassland"), out var grassland));
            Assert.IsTrue(grassland.OpenToPlayers);
        }
    }
}
