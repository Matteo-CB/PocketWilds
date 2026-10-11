using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Cosmetics;
using PocketWilds.Editor.Shop;
using PocketWilds.Presentation;
using UnityEditor;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T47 « tenues ajustées » (owner rule: nothing enters anything): every measured (accessory, species) fit keeps 2 mm from the
    /// LOD0 skin, ears included, at bind pose and at the sampled frames of every clip (AccessoryFitMeasure, exact point to triangle
    /// check); the Flower Crown fits at least three very different species, the fawn among them.
    /// </summary>
    public class AccessoryFitTests
    {
        private ShopCatalogAsset _shop;
        private ContentDatabaseAsset _database;
        private AnimalVisualCatalog _visuals;
        private CosmeticVisualCatalog _cosmetics;

        [SetUp]
        public void SetUp()
        {
            _shop = AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>(ShopPipeline.CatalogPath);
            _database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            _visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            _cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(AccessoryVisualPipeline.CatalogPath);
            Assert.IsNotNull(_shop, "Shop catalog missing.");
            Assert.IsNotNull(_database, "Content database missing.");
            Assert.IsNotNull(_visuals, "Animal visual catalog missing.");
            Assert.IsNotNull(_cosmetics, "Cosmetic visual catalog missing: run AccessoryVisualPipeline.BuildAll.");
        }

        [Test]
        public void EveryFittedPair_KeepsTwoMillimeters_AtBindPoseAndSampledFrames()
        {
            var problems = new List<string>();
            int checkedPairs = 0;
            foreach (var accessory in _shop.accessories)
            {
                var entry = _cosmetics.Find(accessory.sourceId);
                Assert.IsNotNull(entry?.prefab, accessory.sourceId + ": no accessory prefab.");
                var shape = AccessoryFitMeasure.LoadAccessory(entry.prefab);
                string socket = AccessoryFitMeasure.SocketName(accessory.socket);
                foreach (var fit in accessory.speciesFits)
                {
                    var prefab = AccessoryFitMeasure.PrefabOf(_database, _visuals, fit.speciesId);
                    if (prefab == null)
                    {
                        problems.Add($"{accessory.sourceId} on {fit.speciesId}: no visual");
                        continue;
                    }
                    var surface = AccessoryFitMeasure.Bake(prefab, socket);
                    if (surface == null)
                    {
                        problems.Add($"{accessory.sourceId} on {fit.speciesId}: no {socket} or skin");
                        continue;
                    }
                    Assert.Greater(surface.Poses.Count, 1, fit.speciesId + ": no sampled clip frame.");
                    var placement = new AccessoryFit(fit.localPosition, fit.localEuler, fit.localScale);
                    float clearance = AccessoryFitMeasure.MinClearance(shape, surface, placement, out string pose);
                    checkedPairs++;
                    if (clearance < AccessoryFitMeasure.Clearance - 1e-4f)
                        problems.Add($"{accessory.sourceId} on {fit.speciesId}: {clearance * 1000f:0.0} mm at {pose}");
                }
            }
            Assert.Greater(checkedPairs, 0, "No fitted pair: run AccessoryFitPipeline.BuildAll.");
            Assert.IsEmpty(problems, "Accessories enter the animal (2 mm rule):\n" + string.Join("\n", problems));
        }

        [Test]
        public void FlowerCrown_FitsThreeVeryDifferentSpecies_IncludingTheFawn()
        {
            var crown = _shop.accessories.FirstOrDefault(a => a.sourceId == ShopPipeline.FlowerCrown);
            Assert.IsNotNull(crown, "Flower Crown missing.");
            var fitted = crown.speciesFits.Select(f => f.speciesId).Distinct().ToList();
            Assert.GreaterOrEqual(fitted.Count, 3, "The crown fits too few species: " + string.Join(", ", fitted));
            CollectionAssert.Contains(fitted, "species.fawn", "The crown must fit the fawn (MediumQuadruped).");
            var families = fitted.Select(id => _database.species.First(s => s != null && s.sourceId == id).rigFamily).Distinct().Count();
            Assert.GreaterOrEqual(families, 2, "The crown's species must span both quadruped families.");
        }

        [Test]
        public void Fits_StayInTheirRange_AndNeverOnAnExcludedSpecies()
        {
            foreach (var accessory in _shop.accessories)
            {
                foreach (var fit in accessory.speciesFits)
                {
                    Assert.That(fit.localScale.x, Is.InRange(AccessoryFitMeasure.MinScale - 1e-3f, AccessoryFitMeasure.MaxScale + 1e-3f), fit.speciesId);
                    Assert.AreEqual(fit.localScale.x, fit.localScale.y, 1e-5f, fit.speciesId + ": uniform scale only.");
                    Assert.AreEqual(fit.localScale.x, fit.localScale.z, 1e-5f, fit.speciesId + ": uniform scale only.");
                    Assert.LessOrEqual(UnityEngine.Mathf.Abs(fit.localPosition.z), 0.0201f, fit.speciesId + ": offset beyond 2 cm.");
                    Assert.AreEqual(0f, fit.localPosition.x, 1e-5f, fit.speciesId + ": no sideways offset.");
                    CollectionAssert.DoesNotContain(accessory.excludedSpecies, fit.speciesId, accessory.sourceId);
                }
            }
        }
    }
}
