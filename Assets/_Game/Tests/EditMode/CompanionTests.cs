using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T47 companions: unique per account, the staged arrival, the 25 bond levels and their rewards, the exclusions.</summary>
    public class CompanionTests
    {
        private static readonly ContentId WheatCream = ContentId.FromSource("variant.fieldhamster.wheatcream");
        private static readonly ContentId FieldLavender = ContentId.FromSource("variant.fieldhamster.fieldlavender");

        private ContentRegistry _content;
        private PlayerState _state;

        [SetUp]
        public void SetUp()
        {
            _content = T47Content.Build();
            _state = PlayerState.CreateNew(TestUtil.T0);
        }

        [Test]
        public void BondCurve_Is40Plus20PerLevel_7500InAll()
        {
            Assert.AreEqual(60, CompanionService.XpToReach(1));
            Assert.AreEqual(140, CompanionService.XpToReach(2));
            Assert.AreEqual(7500, CompanionService.XpToReach(25));
            Assert.AreEqual(7500, CompanionService.MaxXp);
            Assert.AreEqual(0, CompanionService.LevelOf(59));
            Assert.AreEqual(1, CompanionService.LevelOf(60));
            Assert.AreEqual(24, CompanionService.LevelOf(7499));
            Assert.AreEqual(25, CompanionService.LevelOf(99999));
        }

        [Test]
        public void Companion_IsUniquePerAccount()
        {
            var companions = T47Content.Companions(_content, _state, out var grants);
            var arrivals = new List<uint>();
            companions.Arrived += (species, animal) => arrivals.Add(animal);

            grants.GrantPurchase("txn-1", T47Content.Pack, ContentId.None);
            grants.GrantPurchase("txn-1", T47Content.Pack, ContentId.None, restore: true);
            grants.GrantPurchase("txn-2", T47Content.Pack, ContentId.None, restore: true);
            Assert.AreEqual(0, companions.Sync());
            Assert.AreEqual(0, companions.Sync());

            Assert.AreEqual(1, arrivals.Count, "One arrival, whatever the callbacks.");
            Assert.AreEqual(1, _state.Companions.Companions.Count);
            int hamsters = 0;
            for (int i = 0; i < _state.Collection.Count; i++)
                if (_state.Collection[i].Species == T47Content.Hamster)
                    hamsters++;
            Assert.AreEqual(1, hamsters);
            Assert.AreEqual(arrivals[0], companions.AnimalOf(T47Content.Hamster));
        }

        [Test]
        public void Arrival_IsOwedToTheViewUntilSeen()
        {
            var companions = T47Content.Companions(_content, _state, out var grants);
            Assert.IsFalse(companions.TryGetPendingArrival(out _, out _));
            grants.GrantPurchase("txn-1", T47Content.Pack, ContentId.None);
            Assert.IsTrue(companions.TryGetPendingArrival(out var species, out uint animal));
            Assert.AreEqual(T47Content.Hamster, species);
            Assert.AreEqual(companions.AnimalOf(T47Content.Hamster), animal);
            companions.MarkArrivalSeen(T47Content.Hamster);
            Assert.IsFalse(companions.TryGetPendingArrival(out _, out _));
        }

        [Test]
        public void BondRewards_At5_10_15_20_25()
        {
            var items = new ProfileItemService(new AchievementState());
            var companions = T47Content.Companions(_content, _state, out _, items);
            _state.Entitlements.Owned.Add(T47Content.CompanionItem);
            companions.Sync();
            uint hamster = companions.AnimalOf(T47Content.Hamster);
            var ups = new List<CompanionLevelUp>();
            companions.LevelReached += (in CompanionLevelUp up) => ups.Add(up);
            int poses = items.OwnedCount(ProfileItemKind.Pose);
            int titles = items.OwnedCount(ProfileItemKind.Title);

            Assert.IsFalse(companions.IsLookUnlocked(T47Content.Hamster, WheatCream));
            Assert.IsFalse(companions.SetLook(T47Content.Hamster, WheatCream), "Locked before bond 5.");

            ReachLevel(companions, 5);
            Assert.AreEqual(5, ups.Count, "One event per level, in order.");
            Assert.AreEqual(CompanionReward.Light, ups[0].Reward);
            Assert.AreEqual(CompanionReward.Look, ups[4].Reward);
            Assert.AreEqual(WheatCream, ups[4].Item);
            Assert.IsTrue(companions.SetLook(T47Content.Hamster, WheatCream));
            Assert.IsTrue(_state.Collection.TryGet(hamster, out var dressed));
            Assert.AreEqual(WheatCream, dressed.Variant);
            Assert.AreEqual(RarityTier.Rare, dressed.Rarity);

            ReachLevel(companions, 10);
            Assert.AreEqual(CompanionReward.Look, ups[9].Reward);
            Assert.AreEqual(FieldLavender, ups[9].Item);
            Assert.IsTrue(companions.IsLookUnlocked(T47Content.Hamster, FieldLavender));

            Assert.IsFalse(_state.Entitlements.Owned.Contains(T47Content.Basket));
            ReachLevel(companions, 15);
            Assert.AreEqual(CompanionReward.Accessory, ups[14].Reward);
            Assert.AreEqual(T47Content.Basket, ups[14].Item);
            Assert.IsTrue(_state.Entitlements.Owned.Contains(T47Content.Basket), "Bond 15: the Seed Basket.");

            ReachLevel(companions, 20);
            Assert.AreEqual(CompanionReward.Pose, ups[19].Reward);
            Assert.AreEqual(poses + 1, items.OwnedCount(ProfileItemKind.Pose), "Bond 20: pose.fill_cheeks.");

            ReachLevel(companions, 25);
            Assert.AreEqual(25, ups.Count);
            Assert.AreEqual(CompanionReward.Title, ups[24].Reward);
            Assert.AreEqual(titles + 1, items.OwnedCount(ProfileItemKind.Title), "Bond 25: title.full_cheeks.");
            var bond = companions.GetBond(T47Content.Hamster);
            Assert.AreEqual(25, bond.Level);
            Assert.AreEqual(0, bond.XpForNext);
            Assert.AreEqual(0, companions.AddBondXp(T47Content.Hamster, 100), "The bond stops at 7 500 XP.");
        }

        [Test]
        public void BondFromPlay_FeedsTheCompanionTrack()
        {
            var companions = T47Content.Companions(_content, _state, out var grants);
            grants.GrantPurchase("txn-1", T47Content.Pack, ContentId.None);
            uint hamster = companions.AnimalOf(T47Content.Hamster);
            uint rabbit = T47Content.AddAnimal(_state, T47Content.Rabbit, "variant.rabbit.brown");
            ICompanionBondSink sink = companions;

            Assert.IsTrue(sink.IsCompanion(hamster));
            Assert.IsFalse(sink.IsCompanion(rabbit));
            sink.OnBondEvent(hamster, BondSource.Touch);
            sink.OnBondEvent(hamster, BondSource.Wish);
            sink.OnBondEvent(rabbit, BondSource.Wish);
            Assert.AreEqual(35, companions.GetBond(T47Content.Hamster).TotalXp, "Touch 5 + wish 30; other animals feed nothing.");
            Assert.IsTrue(companions.OnPhoto(hamster));
            Assert.IsFalse(companions.OnPhoto(hamster), "One photo a day.");
            Assert.AreEqual(45, companions.GetBond(T47Content.Hamster).TotalXp);
        }

        [Test]
        public void Companion_ProducesLikeARare_AndStaysOutOfCodexBreedingAndDraws()
        {
            var companions = T47Content.Companions(_content, _state, out var grants);
            var grassland = ContentId.FromSource("biome.grassland");
            Assert.AreEqual(1.0, CompanionCatalog.BiomeMultiplier(_state.Entitlements, grassland), 1e-9);
            grants.GrantPurchase("txn-1", T47Content.Pack, ContentId.None);
            Assert.IsTrue(_state.Collection.TryGet(companions.AnimalOf(T47Content.Hamster), out var hamster));
            Assert.AreEqual(RarityTier.Rare, hamster.Rarity);
            Assert.AreEqual(1.05, CompanionCatalog.BiomeMultiplier(_state.Entitlements, grassland), 1e-9, "Light +5 % for the Meadow.");
            Assert.AreEqual(1.0, CompanionCatalog.BiomeMultiplier(_state.Entitlements, ContentId.FromSource("biome.forest")), 1e-9);

            var species = _content.Species.Get(T47Content.Hamster);
            Assert.IsTrue(species.IsCompanion && species.IsSecret, "A companion is secret: no rule asks to find it.");
            Assert.IsFalse(_state.Collection.DiscoveredSpecies.Contains(T47Content.Hamster), "Not a discovery.");
            Assert.AreEqual(3, new CodexService(_state, _content, null).TotalSpecies, "Out of the Codex sections and completion.");

            var twin = hamster;
            twin.InstanceId = hamster.InstanceId + 100;
            Assert.AreEqual(BreedCheck.Companion, new GeneticsService(_content).Check(hamster, twin));
        }

        private static void ReachLevel(CompanionService companions, int level)
        {
            int missing = CompanionService.XpToReach(level) - companions.GetBond(T47Content.Hamster).TotalXp;
            Assert.AreEqual(missing, companions.AddBondXp(T47Content.Hamster, missing));
            Assert.AreEqual(level, companions.GetBond(T47Content.Hamster).Level);
        }
    }
}
