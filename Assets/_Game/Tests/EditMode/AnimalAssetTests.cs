using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>Gate checks that run in CI: every animal prefab passes the AssetValidator, content builds cleanly.</summary>
    public class AnimalAssetTests
    {
        [Test]
        public void AllAnimalPrefabs_PassAssetValidator()
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { AssetValidator.AnimalPrefabFolder });
            Assert.IsNotEmpty(prefabs, "No animal prefab found.");
            var errors = AssetValidator.ValidateAllAnimals().Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void ContentDatabase_BuildsWithoutErrors_AndRabbitHasSixVariants()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            Assert.IsNotNull(database);
            var builder = database.CreateBuilder();
            Assert.IsFalse(builder.HasErrors, string.Join("; ", builder.Issues));
            var registry = builder.Build();
            var rabbit = ContentId.FromSource("species.rabbit");
            Assert.IsTrue(registry.Species.TryGet(rabbit, out var species));
            Assert.AreEqual("PF_Animal_Rabbit", species.VisualKey);
            int variants = 0;
            for (int i = 0; i < registry.Variants.Count; i++)
                if (registry.Variants[i].Species == rabbit) variants++;
            Assert.AreEqual(6, variants);
        }

        [Test]
        public void VisualCatalog_ResolvesEveryShippedSpecies()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            Assert.IsNotNull(catalog);
            foreach (var species in database.species)
            {
                Assert.IsTrue(catalog.TryGet(species.visualKey, out var entry), species.visualKey);
                Assert.IsNotNull(entry.prefab, species.visualKey);
            }
        }

        [TestCase(0, PatternChannel.None, 0, 0)]
        [TestCase(1023, PatternChannel.MaskD, 15, 15)]
        [TestCase(37, PatternChannel.MaskA, 9, 3)]
        public void VisualPacking_RoundTrips(int row, PatternChannel pattern, int strength, int rare)
        {
            uint packed = AnimalVisualPacking.Pack(row, pattern, strength, rare);
            Assert.AreEqual(row, AnimalVisualPacking.PaletteRow(packed));
            Assert.AreEqual(pattern, AnimalVisualPacking.Pattern(packed));
            Assert.AreEqual(strength, AnimalVisualPacking.PatternStrength(packed));
            Assert.AreEqual(rare, AnimalVisualPacking.RareIntensity(packed));
        }

        [Test]
        public void VariantWithWrongPaletteSize_IsRejected()
        {
            var builder = new ContentRegistryBuilder();
            TestUtil.AddSpecies(builder, "species.rabbit", 1.0);
            builder.AddVariant(new VariantDefinition(ContentId.FromSource("variant.rabbit.bad"), ContentId.FromSource("species.rabbit"),
                "x", RarityTier.Common, new Color32[3], PatternChannel.None, 0), "variant.rabbit.bad");
            Assert.IsTrue(builder.HasErrors);
        }
    }
}
