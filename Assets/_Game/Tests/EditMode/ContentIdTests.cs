using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    public class ContentIdTests
    {
        [Test]
        public void Hash_IsFrozen_ForKnownIds()
        {
            // These values are stored in player saves. If this test fails, every save breaks.
            Assert.AreEqual(0xc1e32b57u, ContentId.FromSource("species.rabbit").Value);
            Assert.AreEqual(0x2b62a4c6u, ContentId.FromSource("species.squirrel").Value);
            Assert.AreEqual(0x5bcda452u, ContentId.FromSource("species.hedgehog").Value);
            Assert.AreEqual(0xd65ebd0cu, ContentId.FromSource("biome.grassland").Value);
        }

        [Test]
        public void SameSource_SameId_DifferentSource_DifferentId()
        {
            Assert.AreEqual(ContentId.FromSource("species.rabbit"), ContentId.FromSource("species.rabbit"));
            Assert.AreNotEqual(ContentId.FromSource("species.rabbit"), ContentId.FromSource("species.squirrel"));
            Assert.IsFalse(ContentId.None.IsValid);
            Assert.IsFalse(ContentId.FromSource("").IsValid);
        }

        [TestCase("species.rabbit", true)]
        [TestCase("variant.rabbit.white", true)]
        [TestCase("offer.starter.001", true)]
        [TestCase("egg.grassland.basic", true)]
        [TestCase("Species.Rabbit", false)]
        [TestCase("rabbit", false)]
        [TestCase("species..rabbit", false)]
        [TestCase("species.rabbit.", false)]
        [TestCase(".species", false)]
        [TestCase("species rabbit", false)]
        [TestCase("", false)]
        public void Format_Validation(string source, bool expected)
        {
            Assert.AreEqual(expected, ContentIdFormat.IsValid(source));
        }

        [Test]
        public void DebugName_ResolvesInEditor()
        {
            var id = ContentId.FromSource("species.rabbit");
            Assert.AreEqual("species.rabbit", id.ToString());
        }

        [Test]
        public void DictionaryLookup_DoesNotAllocate()
        {
            var map = new Dictionary<ContentId, int> { { ContentId.FromSource("species.rabbit"), 1 } };
            var key = ContentId.FromSource("species.rabbit");
            int sum = 0;
            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    if (map.TryGetValue(key, out int v))
                        sum += v;
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(1000, sum);
        }

        [Test]
        public void Registry_LooksUpByIdAndIndex()
        {
            var registry = TestUtil.ThreeSpecies();
            Assert.AreEqual(3, registry.Species.Count);
            Assert.IsTrue(registry.Species.TryGetIndex(ContentId.FromSource("species.squirrel"), out int index));
            Assert.AreEqual(1.5, registry.Species[index].BaseProduction);
            Assert.IsFalse(registry.Species.TryGet(ContentId.FromSource("species.fox"), out _));
        }

        [Test]
        public void Registry_RejectsDuplicates_Tombstones_BadFormat_AndMismatchedIds()
        {
            var builder = new ContentRegistryBuilder();
            builder.AddTombstone("species.retired_mole");
            TestUtil.AddSpecies(builder, "species.rabbit", 1.0);
            TestUtil.AddSpecies(builder, "species.rabbit", 1.0);
            TestUtil.AddSpecies(builder, "species.retired_mole", 1.0);
            builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource("Bad.Id"), "x", RigFamily.SmallQuadruped, ContentId.None, 1, "x"), "Bad.Id");
            builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource("species.fox"), "x", RigFamily.SmallQuadruped, ContentId.None, 1, "x"), "species.wolf");

            Assert.IsTrue(builder.HasErrors);
            Assert.AreEqual(4, builder.Issues.Count);
            Assert.Throws<System.InvalidOperationException>(() => builder.Build());
        }
    }
}
