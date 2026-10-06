using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Production;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T34D ED-001, ED-003, ED-011: the content wizard checks every slice species and egg on the disk and in the data,
    /// and an id still in use can never be retired.
    /// </summary>
    public class ContentWizardTests
    {
        private static string Describe(ContentWizard.Row row) =>
            row.Label + ": " + string.Join(" | ", row.Checks.Where(c => !c.Passed).Select(c => c.Name + " (" + c.Detail + ")"));

        [Test]
        public void EverySliceSpecies_IsComplete()
        {
            var rows = ContentWizard.SpeciesReport();
            CollectionAssert.IsSubsetOf(new[] { "Rabbit", "Squirrel", "Hedgehog" }, rows.Select(r => r.Label).ToArray());
            foreach (var row in rows)
                Assert.IsTrue(row.Complete, Describe(row));
        }

        [Test]
        public void EverySliceEgg_IsComplete()
        {
            var rows = ContentWizard.EggReport();
            Assert.GreaterOrEqual(rows.Count, 2);
            foreach (var row in rows)
                Assert.IsTrue(row.Complete, Describe(row));
        }

        [Test]
        public void ContentIds_CoverEveryKind_WithUniqueHashes()
        {
            var ids = ContentWizard.ContentIds();
            foreach (var kind in new[] { "species", "variant", "egg", "biome", "product", "offer" })
                Assert.IsTrue(ids.Any(r => r.Kind == kind), kind);
            Assert.AreEqual(ids.Count, ids.Select(r => r.Value).Distinct().Count(), "No two content ids share a hash.");
        }

        [Test]
        public void Retire_RefusesLiveAndEmptyIds()
        {
            int before = ContentWizard.Database().tombstones.Count;
            StringAssert.Contains("encore utilisé", ContentWizard.Retire("species.rabbit"));
            StringAssert.Contains("vide", ContentWizard.Retire("  "));
            Assert.AreEqual(before, ContentWizard.Database().tombstones.Count, "Nothing was retired.");
        }
    }
}
