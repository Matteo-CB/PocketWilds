using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T47 screens' view models: the outfit picker's slot states (hidden by the species, locked with nothing owned, open, worn) and
    /// choices, and the companion reward ladder (5, 10, 15, 20, 25, locked until reached, names from the content and profile rows).
    /// </summary>
    public class OutfitScreenModelTests
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
        }

        private SpeciesDefinition Species(ContentId id) => _content.Species.Get(id);

        [Test]
        public void NothingOwned_EverySlotIsLocked_AndThePickerSaysSo()
        {
            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            var picker = new OutfitPicker(_cosmetics);
            picker.Bind(rabbit, Species(T47Content.Rabbit));
            Assert.IsFalse(picker.AnyOwned);
            for (int i = 0; i < OutfitPicker.Sockets.Length; i++)
            {
                Assert.AreEqual(OutfitSlotState.Locked, picker.GetSlot(i).State, OutfitPicker.Sockets[i].ToString());
                Assert.AreEqual(0, picker.ChoiceCount(i));
            }
            Assert.AreEqual(0, picker.FirstUsableSlot());
        }

        [Test]
        public void OwnedCrown_OpensTheHead_ThenWornWithItsPalette_OtherSlotsStayLocked()
        {
            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            _state.Entitlements.Owned.Add(T47Content.Crown);
            var picker = new OutfitPicker(_cosmetics);
            picker.Bind(rabbit, Species(T47Content.Rabbit));
            Assert.IsTrue(picker.AnyOwned);
            Assert.AreEqual(OutfitSlotState.Open, picker.GetSlot(0).State);
            Assert.AreEqual(1, picker.ChoiceCount(0));
            Assert.AreEqual(T47Content.Crown, picker.GetChoice(0, 0).Accessory);
            Assert.AreEqual(3, picker.GetChoice(0, 0).Palettes.Length);
            Assert.IsFalse(picker.GetChoice(0, 0).Worn);
            Assert.AreEqual(OutfitSlotState.Locked, picker.GetSlot(1).State, "Neck: nothing owned fits.");
            Assert.AreEqual(OutfitSlotState.Locked, picker.GetSlot(2).State, "Back: the basket is not owned.");

            Assert.AreEqual(EquipResult.Ok, _cosmetics.Equip(rabbit, T47Content.Crown, AccessorySocket.HeadTop, 2));
            picker.Rebuild();
            var head = picker.GetSlot(0);
            Assert.AreEqual(OutfitSlotState.Worn, head.State);
            Assert.AreEqual(T47Content.Crown, head.Worn);
            Assert.AreEqual(2, head.Palette, "frost");
            Assert.IsTrue(picker.GetChoice(0, 0).Worn);

            Assert.IsTrue(_cosmetics.Unequip(rabbit, AccessorySocket.HeadTop));
            picker.Rebuild();
            Assert.AreEqual(OutfitSlotState.Open, picker.GetSlot(0).State);
        }

        [Test]
        public void TheHamsterHidesItsTail_EvenWithTheRibbonOwned_AndAFawnCannotWearTheBasket()
        {
            _state.Entitlements.Owned.Add(T47Content.Ribbon);
            _state.Entitlements.Owned.Add(T47Content.Basket);
            uint hamster = T47Content.AddAnimal(_state, T47Content.Hamster, "variant.fieldhamster.fieldgold");
            Assert.AreEqual(OutfitSlotState.Hidden,
                OutfitPicker.StateOf(_cosmetics, Species(T47Content.Hamster), hamster, AccessorySocket.TailBase, out int tail));
            Assert.AreEqual(0, tail);
            Assert.AreEqual(OutfitSlotState.Open,
                OutfitPicker.StateOf(_cosmetics, Species(T47Content.Hamster), hamster, AccessorySocket.Back, out int back));
            Assert.AreEqual(1, back);

            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            Assert.AreEqual(OutfitSlotState.Open,
                OutfitPicker.StateOf(_cosmetics, Species(T47Content.Rabbit), rabbit, AccessorySocket.TailBase, out _), "A rabbit shows its tail.");
            uint fawn = T47Content.AddAnimal(_state, T47Content.Fawn, "variant.fawn.lightbrown");
            Assert.AreEqual(OutfitSlotState.Locked,
                OutfitPicker.StateOf(_cosmetics, Species(T47Content.Fawn), fawn, AccessorySocket.Back, out _), "The basket fits small animals only.");
            Assert.AreEqual(OutfitSlotState.Hidden, OutfitPicker.StateOf(_cosmetics, null, fawn, AccessorySocket.Back, out _));
        }

        [Test]
        public void Ladder_FiveRungs_LockedUntilReached()
        {
            var row = CompanionCatalog.FieldHamster;
            var rungs = new CompanionRung[CompanionLadder.Rungs];
            Assert.AreEqual(5, CompanionLadder.Fill(row, 0, _content, rungs));
            CollectionAssert.AreEqual(new[] { 5, 10, 15, 20, 25 }, System.Array.ConvertAll(rungs, r => r.Level));
            foreach (var rung in rungs)
                Assert.IsFalse(rung.Unlocked, rung.Level.ToString());

            CompanionLadder.Fill(row, 12, _content, rungs);
            Assert.IsTrue(rungs[0].Unlocked);
            Assert.IsTrue(rungs[1].Unlocked);
            Assert.IsFalse(rungs[2].Unlocked, "Bond 15 not reached at 12.");
            Assert.IsFalse(rungs[4].Unlocked);

            CompanionLadder.Fill(row, CompanionService.MaxLevel, _content, rungs);
            foreach (var rung in rungs)
                Assert.IsTrue(rung.Unlocked, rung.Level.ToString());
        }

        [Test]
        public void Ladder_NamesTheLooksTheBasketThePoseAndTheTitle()
        {
            var row = CompanionCatalog.FieldHamster;
            var rungs = new CompanionRung[CompanionLadder.Rungs];
            CompanionLadder.Fill(row, 20, _content, rungs);
            Assert.AreEqual(CompanionReward.Look, rungs[0].Reward);
            Assert.AreEqual(row.Look5, rungs[0].Item);
            Assert.AreEqual("variant.fieldhamster.wheatcream.name", rungs[0].NameKey);
            Assert.AreEqual("variant.fieldhamster.fieldlavender.name", rungs[1].NameKey);
            Assert.AreEqual(CompanionReward.Accessory, rungs[2].Reward);
            Assert.AreEqual("accessory.fieldhamster.seedbasket.name", rungs[2].NameKey);
            Assert.AreEqual(CompanionReward.Pose, rungs[3].Reward);
            Assert.AreEqual("pose.fill_cheeks.name", rungs[3].NameKey);
            Assert.AreEqual(CompanionReward.Title, rungs[4].Reward);
            Assert.AreEqual("title.full_cheeks.name", rungs[4].NameKey);
            Assert.AreEqual(OutfitText.CodexRewardPose, rungs[3].KindKey);
            Assert.AreEqual(row.BaseLook, CompanionLadder.Look(row, 0));
            Assert.AreEqual(row.Look10, CompanionLadder.Look(row, 2));
        }
    }
}
