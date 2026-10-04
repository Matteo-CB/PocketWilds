using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T14: egg definitions are data, palettes share the global table, packing is lossless.</summary>
    public class EggContentTests
    {
        private static EggDefinition Egg(string id, int paletteSize = EggDefinition.PaletteSize, byte strength = 15) =>
            new EggDefinition(ContentId.FromSource(id), id + ".name", ContentId.FromSource("biome.grassland"), EggTier.Basic,
                new Color32[paletteSize], strength, 0, strength, "PF_Egg_Base");

        [Test]
        public void ShippedContent_HasGrasslandBasicAndFocusEggs()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var registry = database.CreateBuilder().Build();
            Assert.IsTrue(registry.Eggs.TryGet(ContentId.FromSource("egg.grassland.basic"), out var basic));
            Assert.IsTrue(registry.Eggs.TryGet(ContentId.FromSource("egg.grassland.focus"), out var focus));
            Assert.AreEqual(EggTier.Basic, basic.Tier);
            Assert.AreEqual(EggTier.Focus, focus.Tier);
            Assert.AreEqual("PF_Egg_Base", basic.VisualKey);
            Assert.AreEqual(basic.VisualKey, focus.VisualKey, "Eggs differ by data only, never by prefab.");
        }

        [Test]
        public void PaletteTable_PutsEggsAfterVariants()
        {
            var builder = new ContentRegistryBuilder();
            TestUtil.AddSpecies(builder, "species.rabbit", 1.0);
            builder.AddVariant(new VariantDefinition(ContentId.FromSource("variant.rabbit.a"), ContentId.FromSource("species.rabbit"),
                "a", RarityTier.Common, new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), "variant.rabbit.a");
            builder.AddEgg(Egg("egg.grassland.basic"), "egg.grassland.basic");
            var registry = builder.Build();
            using (var table = new PaletteTable(registry))
            {
                Assert.AreEqual(2, table.PaletteCount);
                Assert.IsTrue(table.TryGetRow(ContentId.FromSource("variant.rabbit.a"), out int variantRow));
                Assert.IsTrue(table.TryGetRow(ContentId.FromSource("egg.grassland.basic"), out int eggRow));
                Assert.AreEqual(0, variantRow);
                Assert.AreEqual(1, eggRow);
            }
        }

        [Test]
        public void EggWithWrongPaletteOrStrength_IsRejected()
        {
            var builder = new ContentRegistryBuilder();
            builder.AddEgg(Egg("egg.grassland.bad", paletteSize: 3), "egg.grassland.bad");
            Assert.IsTrue(builder.HasErrors);

            builder = new ContentRegistryBuilder();
            builder.AddEgg(Egg("egg.grassland.loud", strength: 16), "egg.grassland.loud");
            Assert.IsTrue(builder.HasErrors);
        }

        [TestCase(0, 0, 0, 0, 0, 0)]
        [TestCase(1023, 3, 15, 15, 15, 15)]
        [TestCase(37, 2, 8, 0, 11, 6)]
        public void EggPacking_RoundTrips(int row, int stage, int r, int g, int b, int glow)
        {
            uint packed = EggVisualPacking.Pack(row, stage, r, g, b, glow);
            Assert.AreEqual(row, EggVisualPacking.PaletteRow(packed));
            Assert.AreEqual(stage, EggVisualPacking.CrackStage(packed));
            Assert.AreEqual(r, EggVisualPacking.StrengthR(packed));
            Assert.AreEqual(g, EggVisualPacking.StrengthG(packed));
            Assert.AreEqual(b, EggVisualPacking.StrengthB(packed));
            Assert.AreEqual(glow, EggVisualPacking.Glow(packed));
        }

        [Test]
        public void CrackAndGlowUpdates_KeepTheRestOfThePacking()
        {
            uint packed = EggVisualPacking.Pack(41, 0, 15, 3, 9, 0);
            uint cracked = EggVisualPacking.WithGlow(EggVisualPacking.WithCrackStage(packed, 2), 12);
            Assert.AreEqual(2, EggVisualPacking.CrackStage(cracked));
            Assert.AreEqual(12, EggVisualPacking.Glow(cracked));
            Assert.AreEqual(41, EggVisualPacking.PaletteRow(cracked));
            Assert.AreEqual(15, EggVisualPacking.StrengthR(cracked));
            Assert.AreEqual(3, EggVisualPacking.StrengthG(cracked));
            Assert.AreEqual(9, EggVisualPacking.StrengthB(cracked));
        }
    }
}
