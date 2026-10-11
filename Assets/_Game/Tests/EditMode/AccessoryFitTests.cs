using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Cosmetics;
using PocketWilds.Editor.Events;
using PocketWilds.Editor.Shop;
using PocketWilds.Presentation;
using UnityEditor;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T47 « tenues ajustées » (owner rule: nothing enters anything): every measured (accessory, species) fit of every shop catalog
    /// (DEF_ShopCatalog, DEF_ShopCatalog_Burrows) keeps 2 mm from the LOD0 skin, ears included, at bind pose and at the sampled
    /// frames of every clip (AccessoryFitMeasure, exact point to triangle check); the Flower Crown fits at least three very
    /// different species, the fawn among them. T49: the 8 burrows pieces have their prefab and are rigid, all resting along Up (the
    /// scarf and the overalls' strap junction on the nape, Neck socket), and every burrows outfit fits at least three
    /// species with all its pieces on each (outfit 07: the bib and the straps on the same three).
    /// </summary>
    public class AccessoryFitTests
    {
        private List<ShopCatalogAsset> _catalogs;
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
            _catalogs = AccessoryFitMeasure.ShopCatalogs(_database);
        }

        private IEnumerable<ShopCatalogAsset.Accessory> AllAccessories() => _catalogs.SelectMany(c => c.accessories).Where(a => a != null);

        [Test]
        public void EveryFittedPair_KeepsTwoMillimeters_AtBindPoseAndSampledFrames()
        {
            var problems = new List<string>();
            var surfaces = new Dictionary<(string, string), AccessoryFitMeasure.Surface>();
            int checkedPairs = 0;
            foreach (var accessory in AllAccessories())
            {
                var entry = _cosmetics.Find(accessory.sourceId);
                Assert.IsNotNull(entry?.prefab, accessory.sourceId + ": no accessory prefab.");
                var shape = AccessoryFitMeasure.LoadAccessory(entry.prefab);
                string socket = AccessoryFitMeasure.SocketName(accessory.socket);
                foreach (var fit in accessory.speciesFits)
                {
                    if (!surfaces.TryGetValue((fit.speciesId, socket), out var surface))
                    {
                        var prefab = AccessoryFitMeasure.PrefabOf(_database, _visuals, fit.speciesId);
                        surface = prefab != null ? AccessoryFitMeasure.Bake(prefab, socket) : null;
                        surfaces[(fit.speciesId, socket)] = surface;
                    }
                    if (surface == null)
                    {
                        problems.Add($"{accessory.sourceId} on {fit.speciesId}: no visual, {socket} or skin");
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
            foreach (var accessory in AllAccessories())
            {
                bool conform = AccessoryFitMeasure.IsConform(accessory.sourceId);
                foreach (var fit in accessory.speciesFits)
                {
                    string who = accessory.sourceId + " on " + fit.speciesId;
                    if (conform)
                    {
                        // Bands: x across and y up scale apart (0.7 to 1.5), z follows their geometric mean.
                        Assert.That(fit.localScale.x, Is.InRange(AccessoryFitMeasure.ConformMinScale - 1e-3f, AccessoryFitMeasure.ConformMaxScale + 1e-3f), who);
                        Assert.That(fit.localScale.y, Is.InRange(AccessoryFitMeasure.ConformMinScale - 1e-3f, AccessoryFitMeasure.ConformMaxScale + 1e-3f), who);
                        Assert.AreEqual(UnityEngine.Mathf.Sqrt(fit.localScale.x * fit.localScale.y), fit.localScale.z, 2e-3f, who + ": z scale is the mean of x and y.");
                    }
                    else
                    {
                        Assert.That(fit.localScale.x, Is.InRange(AccessoryFitMeasure.MinScale - 1e-3f, AccessoryFitMeasure.MaxScale + 1e-3f), who);
                        Assert.AreEqual(fit.localScale.x, fit.localScale.y, 1e-5f, who + ": uniform scale only.");
                        Assert.AreEqual(fit.localScale.x, fit.localScale.z, 1e-5f, who + ": uniform scale only.");
                    }
                    // The offset runs across the rest axis: forward or back for Up pieces, up or down for Forward ones.
                    bool forward = !conform && AccessoryFitMeasure.RestAxisOf(accessory.sourceId) == AccessoryFitMeasure.RestAxis.Forward;
                    float offset = forward ? fit.localPosition.y : fit.localPosition.z;
                    Assert.LessOrEqual(UnityEngine.Mathf.Abs(offset), 0.0201f, who + ": offset beyond 2 cm.");
                    Assert.AreEqual(0f, fit.localPosition.x, 1e-5f, who + ": no sideways offset.");
                    Assert.AreEqual(UnityEngine.Vector3.zero, fit.localEuler, who + ": no rotation.");
                    CollectionAssert.DoesNotContain(accessory.excludedSpecies, fit.speciesId, accessory.sourceId);
                }
            }
        }

        [Test]
        public void BurrowsPieces_HavePrefabsInEveryPalette_AndAreFitted()
        {
            var problems = new List<string>();
            foreach (var row in BurrowsEventPipeline.Accessories)
            {
                var accessory = AllAccessories().FirstOrDefault(a => a.sourceId == row.Id);
                var entry = _cosmetics.Find(row.Id);
                if (accessory == null)
                    problems.Add(row.Id + ": not in a shop catalog (run BurrowsEventPipeline)");
                else if (accessory.speciesFits.Count == 0)
                    problems.Add(row.Id + ": no fitted species (run AccessoryFitPipeline)");
                if (entry == null || entry.prefab == null)
                {
                    problems.Add(row.Id + ": no PF_Acc_" + row.Name);
                    continue;
                }
                if (accessory != null)
                    foreach (var palette in accessory.palettes)
                        if (!entry.palettes.Contains(palette))
                            problems.Add($"{row.Id}: no palette row for « {palette} »");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void BurrowsPieces_AreRigid_AndRestAlongTheirAxis()
        {
            foreach (var row in BurrowsEventPipeline.Accessories)
            {
                Assert.IsFalse(AccessoryFitMeasure.IsConform(row.Id), row.Id + ": a rigid piece since T49 (nothing wraps around the body).");
                // The scarf and the bib rest on the nape (the top of the neck behind the head), like every other piece along Up.
                Assert.AreEqual(AccessoryFitMeasure.RestAxis.Up, AccessoryFitMeasure.RestAxisOf(row.Id), row.Id + ": rest axis.");
                if (row.Name == "GardenerScarf" || row.Name == "OverallsBib")
                    Assert.AreEqual(AccessorySocket.Neck, row.Socket, row.Id + ": the nape pieces hang on the neck socket.");
            }
        }

        [Test]
        public void EveryBurrowsOutfit_FitsAtLeastThreeSpecies()
        {
            var burrows = AssetDatabase.LoadAssetAtPath<ShopCatalogAsset>(BurrowsEventPipeline.CatalogPath);
            Assert.IsNotNull(burrows, "DEF_ShopCatalog_Burrows missing: run BurrowsEventPipeline.");
            var problems = new List<string>();
            foreach (var outfit in burrows.outfits)
            {
                IEnumerable<string> species = null;
                foreach (var pieceId in outfit.accessories)
                {
                    var accessory = AllAccessories().FirstOrDefault(a => a.sourceId == pieceId);
                    var fitted = accessory != null ? accessory.speciesFits.Select(f => f.speciesId) : Enumerable.Empty<string>();
                    species = species == null ? fitted.ToList() : species.Intersect(fitted).ToList();
                }
                var list = (species ?? Enumerable.Empty<string>()).Distinct().ToList();
                if (list.Count < 3)
                    problems.Add($"{outfit.sourceId}: {list.Count} species ({string.Join(", ", list)})");
            }
            Assert.IsEmpty(problems, "Outfits fitted on fewer than 3 species (doc: 3 very different species):\n" + string.Join("\n", problems));
        }
    }
}
