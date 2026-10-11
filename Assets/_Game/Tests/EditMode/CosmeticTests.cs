using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T47 test content: a small quadruped (rabbit), a medium one (fawn), a bird, the Field Hamster companion (no TailBase), the
    /// Flower Crown (HeadTop, small and medium), the Seed Basket (Back, small), a test tail ribbon (TailBase, small) and the
    /// Meadow Pack product (companion + burrow + crown + 500 Gems).
    /// </summary>
    internal static class T47Content
    {
        public static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        public static readonly ContentId Fawn = ContentId.FromSource("species.fawn");
        public static readonly ContentId Sparrow = ContentId.FromSource("species.sparrow");
        public static readonly ContentId Hamster = CompanionCatalog.FieldHamster.Species;
        public static readonly ContentId Crown = ContentId.FromSource("cosmetic.flower_crown");
        public static readonly ContentId Basket = ContentId.FromSource("accessory.fieldhamster.seedbasket");
        public static readonly ContentId Ribbon = ContentId.FromSource("cosmetic.test_tail_ribbon");
        public static readonly ContentId Burrow = ContentId.FromSource("decor.cozy_burrow");
        public static readonly ContentId CompanionItem = CompanionCatalog.FieldHamster.Entitlement;
        public static readonly ContentId Pack = ContentId.FromSource("product.starter.001");
        public const long PackGems = 500;
        public static readonly string[] Palettes = { "meadow", "blossom", "frost" };

        public static ContentRegistry Build()
        {
            var b = new ContentRegistryBuilder();
            b.SetShopUnlock(1);
            Species(b, "species.rabbit", RigFamily.SmallQuadruped, "variant.rabbit.brown", RarityTier.Common);
            Species(b, "species.fawn", RigFamily.MediumQuadruped, "variant.fawn.lightbrown", RarityTier.Common);
            Species(b, "species.sparrow", RigFamily.Bird, "variant.sparrow.brown", RarityTier.Common);
            b.AddSpecies(new SpeciesDefinition(Hamster, "species.fieldhamster.name", RigFamily.SmallQuadruped, ContentId.FromSource("biome.grassland"), 1.0,
                "PF_Animal_FieldHamster", isCompanion: true, hiddenSockets: (byte)(1 << (int)AccessorySocket.TailBase)), "species.fieldhamster");
            foreach (var look in new[] { "variant.fieldhamster.fieldgold", "variant.fieldhamster.wheatcream", "variant.fieldhamster.fieldlavender" })
                b.AddVariant(new VariantDefinition(ContentId.FromSource(look), Hamster, look + ".name", RarityTier.Rare, Palette(), PatternChannel.None, 0, isSecret: true), look);

            Item(b, "cosmetic.flower_crown", EntitlementKind.Cosmetic);
            Item(b, "accessory.fieldhamster.seedbasket", EntitlementKind.Cosmetic);
            Item(b, "cosmetic.test_tail_ribbon", EntitlementKind.Cosmetic);
            Item(b, "decor.cozy_burrow", EntitlementKind.Decoration);
            Item(b, "companion.fieldhamster", EntitlementKind.Companion);
            Accessory(b, "cosmetic.flower_crown", AccessorySocket.HeadTop, 350, RigFamily.SmallQuadruped, RigFamily.MediumQuadruped);
            Accessory(b, "accessory.fieldhamster.seedbasket", AccessorySocket.Back, 200, RigFamily.SmallQuadruped);
            Accessory(b, "cosmetic.test_tail_ribbon", AccessorySocket.TailBase, 120, RigFamily.SmallQuadruped);
            b.AddProduct(new ProductDefinition(Pack, "pw.pack.grassland", ProductKind.NonConsumable, 199, PackGems, new[] { CompanionItem, Burrow, Crown }),
                "product.starter.001");
            return b.Build();
        }

        public static uint AddAnimal(PlayerState state, ContentId species, string variant) =>
            state.Collection.Add(new AnimalInstance { Species = species, Variant = ContentId.FromSource(variant), Seed = 7u, Rarity = RarityTier.Common });

        /// <summary>A grant service and a companion service wired like the bootstrap.</summary>
        public static CompanionService Companions(ContentRegistry content, PlayerState state, out GrantService grants, ProfileItemService items = null)
        {
            grants = new GrantService(content, state, null);
            return new CompanionService(state, content, new FakeGameClock(TestUtil.T0), null) { Grants = grants, ProfileItems = items };
        }

        private static void Species(ContentRegistryBuilder b, string id, RigFamily family, string variant, RarityTier rarity)
        {
            b.AddSpecies(new SpeciesDefinition(ContentId.FromSource(id), id + ".name", family, ContentId.FromSource("biome.grassland"), 1.0, "PF_Animal_Test"), id);
            b.AddVariant(new VariantDefinition(ContentId.FromSource(variant), ContentId.FromSource(id), variant + ".name", rarity, Palette(), PatternChannel.None, 0), variant);
        }

        private static void Item(ContentRegistryBuilder b, string id, EntitlementKind kind) =>
            b.AddEntitlement(new EntitlementDefinition(ContentId.FromSource(id), kind, id + ".name", "ICO_Test"), id);

        private static void Accessory(ContentRegistryBuilder b, string id, AccessorySocket socket, int tris, params RigFamily[] families)
        {
            var fits = new AccessoryFamilyFit[families.Length];
            for (int i = 0; i < fits.Length; i++)
                fits[i] = new AccessoryFamilyFit(families[i], AccessoryFit.Identity);
            b.AddAccessory(new AccessoryDefinition(ContentId.FromSource(id), id + ".name", socket, fits, null, null, Palettes, tris, "ICO_Test",
                "PF_Accessory_Test", "SourceArt/Blender/Test.blend"), id);
        }

        private static Color32[] Palette()
        {
            var palette = new Color32[VariantDefinition.PaletteSize];
            for (int i = 0; i < palette.Length; i++)
                palette[i] = new Color32(200, 160, 100, 255);
            return palette;
        }
    }

    /// <summary>T47 accessories and outfits: who can wear what, outfits all or nothing, the save and its migration.</summary>
    public class CosmeticTests
    {
        private ContentRegistry _content;
        private PlayerState _state;
        private CosmeticService _cosmetics;

        [SetUp]
        public void SetUp()
        {
            _content = T47Content.Build();
            _state = PlayerState.CreateNew(TestUtil.T0);
            _cosmetics = new CosmeticService(_state, _content, null);
            _state.Entitlements.Owned.Add(T47Content.Crown);
            _state.Entitlements.Owned.Add(T47Content.Basket);
            _state.Entitlements.Owned.Add(T47Content.Ribbon);
        }

        [Test]
        public void Hamster_CannotWearTailBase_ButOtherSmallQuadrupedsCan()
        {
            uint hamster = T47Content.AddAnimal(_state, T47Content.Hamster, "variant.fieldhamster.fieldgold");
            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            Assert.AreEqual(EquipResult.SpeciesExcluded, _cosmetics.CanEquip(hamster, T47Content.Ribbon, AccessorySocket.TailBase), "Hidden tail: no TailBase.");
            Assert.AreEqual(EquipResult.SpeciesExcluded, _cosmetics.Equip(hamster, T47Content.Ribbon, AccessorySocket.TailBase));
            Assert.IsTrue(_cosmetics.GetEquipped(hamster, AccessorySocket.TailBase).IsEmpty);
            Assert.AreEqual(EquipResult.Ok, _cosmetics.CanEquip(rabbit, T47Content.Ribbon, AccessorySocket.TailBase));
            // The hamster still wears HeadTop and Back.
            Assert.AreEqual(EquipResult.Ok, _cosmetics.CanEquip(hamster, T47Content.Crown, AccessorySocket.HeadTop));
            Assert.AreEqual(EquipResult.Ok, _cosmetics.CanEquip(hamster, T47Content.Basket, AccessorySocket.Back));
        }

        [Test]
        public void Crown_EquipsOnHeadTop_ForSmallAndMediumQuadrupeds()
        {
            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            uint fawn = T47Content.AddAnimal(_state, T47Content.Fawn, "variant.fawn.lightbrown");
            uint sparrow = T47Content.AddAnimal(_state, T47Content.Sparrow, "variant.sparrow.brown");
            var changed = new List<uint>();
            _cosmetics.Changed += changed.Add;

            Assert.AreEqual(EquipResult.Ok, _cosmetics.Equip(rabbit, T47Content.Crown, AccessorySocket.HeadTop, 2));
            Assert.AreEqual(EquipResult.Ok, _cosmetics.Equip(fawn, T47Content.Crown, AccessorySocket.HeadTop));
            CollectionAssert.AreEqual(new[] { rabbit, fawn }, changed);
            Assert.AreEqual(T47Content.Crown, _cosmetics.GetEquipped(rabbit, AccessorySocket.HeadTop).Accessory);
            Assert.AreEqual(2, _cosmetics.GetEquipped(rabbit, AccessorySocket.HeadTop).Palette);
            Assert.IsTrue(_cosmetics.TryGetWorn(fawn, AccessorySocket.HeadTop, out var crown, out string palette, out _));
            Assert.AreEqual("meadow", palette);
            Assert.AreEqual(AccessorySocket.HeadTop, crown.Socket);

            Assert.AreEqual(EquipResult.WrongSocket, _cosmetics.CanEquip(rabbit, T47Content.Crown, AccessorySocket.Neck));
            Assert.AreEqual(EquipResult.RigNotCompatible, _cosmetics.CanEquip(sparrow, T47Content.Crown, AccessorySocket.HeadTop));
            Assert.AreEqual(EquipResult.UnknownPalette, _cosmetics.Equip(rabbit, T47Content.Crown, AccessorySocket.HeadTop, 3));
            _state.Entitlements.Owned.Remove(T47Content.Crown);
            Assert.AreEqual(EquipResult.NotOwned, _cosmetics.CanEquip(rabbit, T47Content.Crown, AccessorySocket.HeadTop));

            Assert.IsTrue(_cosmetics.Unequip(fawn, AccessorySocket.HeadTop));
            Assert.IsFalse(_cosmetics.Unequip(fawn, AccessorySocket.HeadTop));
            Assert.IsFalse(_state.Cosmetics.Animals.ContainsKey(fawn), "A bare animal keeps no entry.");
        }

        [Test]
        public void Outfit_IsAllOrNothing_WithItsSharedPalette()
        {
            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            uint fawn = T47Content.AddAnimal(_state, T47Content.Fawn, "variant.fawn.lightbrown");
            var picnic = new OutfitDefinition(ContentId.FromSource("outfit.test_picnic"), "outfit.test_picnic.name",
                new[] { T47Content.Crown, T47Content.Basket }, "blossom", "ICO_Test", "test");
            var equipped = 0;
            _cosmetics.Equipped += (animal, item, species, socket) => equipped++;

            Assert.AreEqual(EquipResult.Ok, _cosmetics.EquipOutfit(rabbit, picnic));
            Assert.AreEqual(2, equipped, "One outfit_equip per piece.");
            Assert.AreEqual(1, _cosmetics.GetEquipped(rabbit, AccessorySocket.HeadTop).Palette);
            Assert.AreEqual(1, _cosmetics.GetEquipped(rabbit, AccessorySocket.Back).Palette);

            // The basket only fits small quadrupeds: the fawn gets nothing, not even the crown.
            Assert.AreEqual(EquipResult.RigNotCompatible, _cosmetics.EquipOutfit(fawn, picnic));
            Assert.IsFalse(_state.Cosmetics.Animals.ContainsKey(fawn));

            var twoHats = new OutfitDefinition(ContentId.FromSource("outfit.test_hats"), "outfit.test_hats.name",
                new[] { T47Content.Crown, T47Content.Crown }, "meadow", "ICO_Test", "test");
            Assert.AreEqual(EquipResult.InvalidOutfit, _cosmetics.EquipOutfit(rabbit, twoHats));
        }

        [Test]
        public void Catalog_RejectsBadAccessoriesAndOutfits()
        {
            var b = new ContentRegistryBuilder();
            b.AddAccessory(new AccessoryDefinition(T47Content.Crown, "x.name", AccessorySocket.HeadTop, new AccessoryFamilyFit[0], null, null,
                T47Content.Palettes, 100, "ICO", "PF", "src"), "cosmetic.flower_crown");
            Assert.IsTrue(b.HasErrors, "An accessory needs a compatible family.");

            var heavy = new ContentRegistryBuilder();
            heavy.AddAccessory(new AccessoryDefinition(T47Content.Crown, "x.name", AccessorySocket.HeadTop,
                new[] { new AccessoryFamilyFit(RigFamily.SmallQuadruped, AccessoryFit.Identity) }, null, null, T47Content.Palettes, 900, "ICO", "PF", "src"),
                "cosmetic.flower_crown");
            Assert.IsTrue(heavy.HasErrors, "Over the 700 triangle budget.");

            var orphan = new ContentRegistryBuilder();
            orphan.AddAccessory(new AccessoryDefinition(T47Content.Crown, "x.name", AccessorySocket.HeadTop,
                new[] { new AccessoryFamilyFit(RigFamily.SmallQuadruped, AccessoryFit.Identity) }, null, null, T47Content.Palettes, 300, "ICO", "PF", "src"),
                "cosmetic.flower_crown");
            Assert.Throws<System.InvalidOperationException>(() => orphan.Build(), "An accessory needs its Cosmetic item.");
        }

        [Test]
        public void Save_RoundTripsWornItemsAndCompanions()
        {
            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            _cosmetics.Equip(rabbit, T47Content.Crown, AccessorySocket.HeadTop, 1);
            _cosmetics.Equip(rabbit, T47Content.Ribbon, AccessorySocket.TailBase, 2);
            _state.Companions.Companions.Add(new CompanionRecord
            {
                Species = T47Content.Hamster, AnimalId = 42, BondXp = 1300, RewardedLevel = 9, ArrivalPending = true, PhotoDay = 20371,
            });

            var loaded = RoundTrip(_state);
            Assert.AreEqual(T47Content.Crown, loaded.Cosmetics.Get(rabbit, AccessorySocket.HeadTop).Accessory);
            Assert.AreEqual(1, loaded.Cosmetics.Get(rabbit, AccessorySocket.HeadTop).Palette);
            Assert.AreEqual(2, loaded.Cosmetics.Get(rabbit, AccessorySocket.TailBase).Palette);
            Assert.AreEqual(1, loaded.Companions.Companions.Count);
            var c = loaded.Companions.Companions[0];
            Assert.AreEqual(T47Content.Hamster, c.Species);
            Assert.AreEqual(42u, c.AnimalId);
            Assert.AreEqual(1300, c.BondXp);
            Assert.AreEqual(9, c.RewardedLevel);
            Assert.IsTrue(c.ArrivalPending);
            Assert.AreEqual(20371, c.PhotoDay);
        }

        [Test]
        public void Save_UnknownIdsLoadSafely_AndArePruned()
        {
            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            _state.Cosmetics.Set(rabbit, AccessorySocket.Neck, new EquippedAccessory { Accessory = ContentId.FromSource("cosmetic.retired_scarf"), Palette = 0 });
            _state.Cosmetics.Set(rabbit, AccessorySocket.HeadTop, new EquippedAccessory { Accessory = T47Content.Crown, Palette = 9 });
            _state.Cosmetics.Set(999u, AccessorySocket.Back, new EquippedAccessory { Accessory = T47Content.Basket, Palette = 0 });
            _state.Companions.Companions.Add(new CompanionRecord { Species = ContentId.FromSource("species.future_companion"), AnimalId = 0, BondXp = 99999 });

            PlayerState loaded = null;
            Assert.DoesNotThrow(() => loaded = RoundTrip(_state));
            Assert.AreEqual(CompanionService.MaxXp, loaded.Companions.Companions[0].BondXp, "Bond XP is clamped on load.");
            var cosmetics = new CosmeticService(loaded, _content, null);
            Assert.AreEqual(3, cosmetics.Prune(), "Unknown item, palette out of range, animal gone.");
            Assert.AreEqual(0, loaded.Cosmetics.Animals.Count);
            var companions = new CompanionService(loaded, _content, new FakeGameClock(TestUtil.T0), null);
            Assert.AreEqual(0, companions.Sync(), "A companion this build does not know is left alone.");
        }

        [Test]
        public void Migration_16To17_StartsBare_AndPlacesAnOwnedCompanionAtBoot()
        {
            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            _cosmetics.Equip(rabbit, T47Content.Crown, AccessorySocket.HeadTop);
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(_state, writer);
            var bytes = writer.WrittenSpan.ToArray();
            var document = serializer.ReadDocument(bytes, bytes.Length);
            // A schema 16 file: everything but the Cosmetics section.
            document.RemoveSection(SaveSectionId.Cosmetics);
            document.SchemaVersion = 16;

            SaveMigrations.CreatePipeline().Migrate(document);
            Assert.AreEqual(17, SaveSchema.CurrentVersion);
            Assert.AreEqual(SaveSchema.CurrentVersion, document.SchemaVersion);
            var loaded = serializer.Deserialize(document);
            Assert.AreEqual(0, loaded.Cosmetics.Animals.Count + loaded.Companions.Companions.Count);
            Assert.AreEqual(_state.Collection.Count, loaded.Collection.Count);

            loaded.Entitlements.Owned.Add(T47Content.CompanionItem);
            var companions = new CompanionService(loaded, _content, new FakeGameClock(TestUtil.T0), null);
            Assert.AreEqual(1, companions.Sync());
            Assert.AreNotEqual(0u, companions.AnimalOf(T47Content.Hamster));
        }

        private static PlayerState RoundTrip(PlayerState state)
        {
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(state, writer);
            var bytes = writer.WrittenSpan.ToArray();
            return serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
        }
    }
}
