using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T44B: tiers and claims, the Renewal, the backfill, the profile items' sources, the save, no allocation.</summary>
    public class AchievementServiceTests
    {
        private static readonly int Hatches = AchievementData.IndexOf(AchievementCounter.Hatches);
        private static readonly int RunHatches = AchievementData.IndexOf(AchievementCounter.RunHatches);

        private static (AchievementService achievements, ProfileItemService items) New(AchievementState state = null)
        {
            state ??= new AchievementState();
            var items = new ProfileItemService(state);
            var achievements = new AchievementService(state) { Items = items };
            return (achievements, items);
        }

        [Test]
        public void Tiers_WaitUntilClaimed_GemsAndExtrasGivenOnce()
        {
            var (a, items) = New();
            var reached = new List<AchievementTier>();
            var claims = new List<AchievementClaim>();
            a.TierReached += (index, tier) => { if (index == Hatches) reached.Add(tier); };
            a.RewardClaimed += (in AchievementClaim c) => claims.Add(c);

            a.Add(AchievementCounter.Hatches, 9);
            Assert.AreEqual(AchievementTier.None, a.Reached(Hatches));
            Assert.AreEqual(1, a.ToGo(Hatches), "« Plus que 1 ».");
            a.Add(AchievementCounter.Hatches, 241);
            Assert.AreEqual(AchievementTier.Silver, a.Reached(Hatches));
            CollectionAssert.AreEqual(new[] { AchievementTier.Bronze, AchievementTier.Silver }, reached);
            Assert.AreEqual(2, a.Claimable);
            Assert.AreEqual(2500, a.NextThreshold(Hatches));

            Assert.IsTrue(a.TryClaim(Hatches));
            Assert.AreEqual(15, claims[0].Gems, "Bronze 5 + Silver 10.");
            Assert.AreEqual(0, a.Claimable);
            Assert.IsFalse(a.TryClaim(Hatches), "Nothing claimed twice.");

            a.Add(AchievementCounter.Hatches, 25000);
            Assert.AreEqual(AchievementTier.Star, a.Reached(Hatches));
            Assert.AreEqual(2, a.Claimable);
            Assert.IsTrue(a.TryClaim(Hatches));
            Assert.AreEqual(70, claims[1].Gems, "Gold 20 + Star 50.");
            Assert.AreEqual("title.golden_nest", claims[1].Extra);
            int title = ProfileData.IndexOf(ProfileItemKind.Title, "title.golden_nest");
            Assert.IsTrue(items.Owned(ProfileItemKind.Title, title) && items.Unseen(ProfileItemKind.Title, title));
            Assert.IsFalse(a.TryClaim(Hatches));
            Assert.AreEqual(2, claims.Count);
            Assert.AreEqual(0, a.ToGo(Hatches));
        }

        [Test]
        public void GoldTier_OfABiomeAchievement_GivesItsDecoration()
        {
            var (a, _) = New();
            var claims = new List<AchievementClaim>();
            a.RewardClaimed += (in AchievementClaim c) => claims.Add(c);
            int lakeBonds = AchievementData.IndexOf(AchievementCounter.BiomeBonds, 2);
            a.Raise(AchievementCounter.BiomeBonds, 8, 2);
            Assert.AreEqual(AchievementTier.Gold, a.Reached(lakeBonds));
            Assert.IsTrue(a.TryClaim(lakeBonds));
            Assert.AreEqual(35, claims[0].Gems);
            Assert.AreEqual(ContentId.FromSource("decor.achievement.lake.20"), claims[0].Decoration);
            a.Raise(AchievementCounter.BiomeBonds, 20, 2);
            Assert.IsTrue(a.TryClaim(lakeBonds));
            Assert.IsFalse(claims[1].Decoration.IsValid, "The decoration comes with the Gold tier only.");
        }

        [Test]
        public void Counters_NeverLowered_AndTheRenewalKeepsThemAndTheBestRun()
        {
            var (a, _) = New();
            a.Add(AchievementCounter.Hatches, 300);
            a.AddRunHatches(300);
            a.Raise(AchievementCounter.RefugeLevel, 60);
            a.Raise(AchievementCounter.RefugeLevel, 20);
            Assert.AreEqual(60, a.Progress(AchievementData.IndexOf(AchievementCounter.RefugeLevel)), "A state counter is never lowered.");

            a.BeginRun();
            a.Add(AchievementCounter.Hatches, 10);
            a.AddRunHatches(10);
            Assert.AreEqual(310, a.Progress(Hatches), "Cumulated since the account began, the Renewal included.");
            Assert.AreEqual(300, a.Progress(RunHatches), "The best run counts.");
            Assert.AreEqual(10, a.State.RunHatches);
            a.AddRunHatches(400);
            Assert.AreEqual(410, a.Progress(RunHatches));

            a.CountRenewalEssence(100);
            a.CountRenewalEssence(80);
            a.CountRenewalEssence(150);
            Assert.AreEqual(2, a.Progress(AchievementData.IndexOf(AchievementCounter.EssenceRecords)), "The first sets the record, then each better one.");
        }

        [Test]
        public void DayAndDistinctCounters_CountOncePerDayAndId()
        {
            var (a, _) = New();
            a.AddDayHatches(20261010, 15);
            a.AddDayHatches(20261011, 20);
            a.AddDayHatches(20261011, 5);
            a.AddDayHatches(20261012, 3);
            Assert.AreEqual(25, a.Progress(AchievementData.IndexOf(AchievementCounter.BestDayHatches)));
            for (int i = 0; i < 4; i++)
                a.AddDayWish(20261012);
            a.AddDayWish(20261013);
            Assert.AreEqual(1, a.Progress(AchievementData.IndexOf(AchievementCounter.FullWishDays)));
            a.AddDistinctId(AchievementCounter.AnimalsPetted, 7);
            a.AddDistinctId(AchievementCounter.AnimalsPetted, 7);
            a.AddDistinctId(AchievementCounter.AnimalsPetted, 9);
            Assert.AreEqual(2, a.Progress(AchievementData.IndexOf(AchievementCounter.AnimalsPetted)));
            a.AddSpeciesTraits(3, (int)(AnimalTrait.Shiny | AnimalTrait.Tiny));
            a.AddSpeciesTraits(3, (int)AnimalTrait.Shiny);
            Assert.AreEqual(2, a.Progress(AchievementData.IndexOf(AchievementCounter.SpeciesTraitPairs)));
            a.AddDistinctBit(AchievementCounter.HatchRarities, 0);
            a.AddDistinctBit(AchievementCounter.HatchRarities, 5);
            a.AddDistinctBit(AchievementCounter.HatchRarities, 5);
            Assert.AreEqual(2, a.Progress(AchievementData.IndexOf(AchievementCounter.HatchRarities)));
        }

        [Test]
        public void Backfill_FromAnOlderSave_CountsTheStateAndUnlocksItsItems()
        {
            var player = new PlayerState();
            for (int i = 0; i < 12; i++)
                player.Collection.DiscoveredSpecies.Add(ProfileData.Species(i));
            player.Mastery.Species[ContentId.FromSource("species.rabbit")] = new SpeciesMastery { Level = 30 };
            player.Mastery.Species[ContentId.FromSource("species.fox")] = new SpeciesMastery { Level = 12 };
            player.Progression.Refuge.Level = 50;
            player.World.Biomes.Add(new BiomeProgress { Biome = AlbumData.BiomeId(0), Unlocked = true, MasteryLevel = 25, ZonesOpened = 0b110 });
            player.DailyVisit.GiftsOpened = 5;
            player.Expeditions.Completed = 3;
            player.World.RenewalCount = 2;

            var (a, items) = New(player.Achievements);
            new AchievementRecount(a, items, player, null).All();

            Assert.AreEqual(12, a.Progress(AchievementData.IndexOf(AchievementCounter.SpeciesDiscovered)));
            Assert.AreEqual(AchievementTier.Bronze, a.Reached(AchievementData.IndexOf(AchievementCounter.SpeciesDiscovered)));
            Assert.AreEqual(1, a.Progress(AchievementData.IndexOf(AchievementCounter.SpeciesMastery30)));
            Assert.AreEqual(2, a.Progress(AchievementData.IndexOf(AchievementCounter.SpeciesMastery10)));
            Assert.AreEqual(42, a.Progress(AchievementData.IndexOf(AchievementCounter.MasteryLevels)));
            Assert.AreEqual(8, a.Progress(AchievementData.IndexOf(AchievementCounter.MasteryMilestones)));
            Assert.AreEqual(50, a.Progress(AchievementData.IndexOf(AchievementCounter.RefugeLevel)));
            Assert.AreEqual(3, a.Progress(AchievementData.IndexOf(AchievementCounter.ZonesOpen)), "The arrival zone and two more.");
            Assert.AreEqual(5, a.Progress(AchievementData.IndexOf(AchievementCounter.DailyGifts)));
            Assert.AreEqual(3, a.Progress(AchievementData.IndexOf(AchievementCounter.ExpeditionsDone)));
            Assert.AreEqual(2, a.Progress(AchievementData.IndexOf(AchievementCounter.Renewals)));

            AssertOwned(items, ProfileItemKind.Title, "title.pal_rabbit", true);
            AssertOwned(items, ProfileItemKind.Title, "title.heart_rabbit", false);
            AssertOwned(items, ProfileItemKind.Title, "title.refuge_grand", true);
            AssertOwned(items, ProfileItemKind.Title, "title.refuge_wonders", false);
            AssertOwned(items, ProfileItemKind.Frame, "frame.golden_paws", true);
            AssertOwned(items, ProfileItemKind.Title, "title.grassland_pillar", true);
            AssertOwned(items, ProfileItemKind.Title, "title.grassland_heart", false);
            AssertOwned(items, ProfileItemKind.Frame, "frame.biome_grassland", true);

            // Run again (the next load): nothing counted twice.
            new AchievementRecount(a, items, player, null).All();
            Assert.AreEqual(42, a.Progress(AchievementData.IndexOf(AchievementCounter.MasteryLevels)));
        }

        [Test]
        public void Hooks_WithoutServices_GiveTheStartingItemsAndCountTheSeason()
        {
            var player = new PlayerState();
            var (a, items) = New(player.Achievements);
            var clock = new FakeGameClock(new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));
            using (var hooks = new AchievementHooks(a, items, player, clock))
            {
                Assert.IsTrue(player.Achievements.StartGiven);
                Assert.AreEqual(1, a.Progress(AchievementData.IndexOf(AchievementCounter.RefugeSeasons)));
                hooks.CountSeason();
                Assert.AreEqual(1, a.Progress(AchievementData.IndexOf(AchievementCounter.RefugeSeasons)), "Once per season.");
                clock.Set(new DateTimeOffset(2027, 1, 5, 9, 0, 0, TimeSpan.Zero));
                hooks.CountSeason();
                clock.Set(new DateTimeOffset(2027, 2, 20, 9, 0, 0, TimeSpan.Zero));
                hooks.CountSeason();
                Assert.AreEqual(2, a.Progress(AchievementData.IndexOf(AchievementCounter.RefugeSeasons)), "A winter spans two years.");
                hooks.CountPhoto(3);
                hooks.CountPhoto(3);
                Assert.AreEqual(1, a.Progress(AchievementData.IndexOf(AchievementCounter.PosesUsed)));
            }
        }

        [Test]
        public void StartingTitleAndFrame_GivenOnceAndShown()
        {
            var (_, items) = New();
            Assert.AreEqual(-1, items.Selected(ProfileItemKind.Title));
            items.GiveStarting();
            items.GiveStarting();
            Assert.IsTrue(items.Owned(ProfileItemKind.Title, 0) && items.Owned(ProfileItemKind.Frame, 0));
            Assert.AreEqual(0, items.Selected(ProfileItemKind.Title));
            Assert.AreEqual(0, items.Selected(ProfileItemKind.Frame));
            Assert.IsFalse(items.Unseen(ProfileItemKind.Title, 0), "A starting gift is not « Nouveau ».");
            Assert.AreEqual(1, items.OwnedCount(ProfileItemKind.Title));

            var selected = new List<int>();
            items.SelectionChanged += (kind, index) => selected.Add(index);
            Assert.IsFalse(items.TrySelect(ProfileItemKind.Title, 5), "Only an owned title is shown.");
            Assert.IsFalse(items.TrySelect(ProfileItemKind.Pose, 0), "Poses are not shown.");
            Assert.IsTrue(items.TrySelect(ProfileItemKind.Title, -1));
            Assert.AreEqual(-1, items.Selected(ProfileItemKind.Title));
            CollectionAssert.AreEqual(new[] { -1 }, selected);
        }

        [Test]
        public void AlbumPageCompletion_GivesItsTitle()
        {
            var (_, items) = New();
            int given = 0;
            for (int p = 0; p < AlbumData.PageCount; p++)
            {
                ref readonly var page = ref AlbumData.Page(p);
                // The keys AlbumService raises with a complete page (AlbumHooks.TitleEarned).
                string key = page.Kind == AlbumKind.Event ? "title.event." + page.Number
                    : page.Kind == AlbumKind.Activity ? "title.activity." + page.Number.ToString("000") : null;
                if (key == null)
                    continue;
                Assert.IsTrue(items.OnAlbumTitle(key), key);
                Assert.IsFalse(items.OnAlbumTitle(key), key + " is given once");
                given++;
            }
            Assert.AreEqual(30, given);
            Assert.AreEqual(30, items.OwnedCount(ProfileItemKind.Title));
            AssertOwned(items, ProfileItemKind.Title, "title.event_aurora", true);
            AssertOwned(items, ProfileItemKind.Title, "title.event_frost", true);
            AssertOwned(items, ProfileItemKind.Title, "title.activity_grooming", true);
            AssertOwned(items, ProfileItemKind.Title, "title.activity_grand_contest", true);
            Assert.IsFalse(items.OnAlbumTitle("title.album.grassland"), "A biome album gives its monument, no title.");
        }

        [Test]
        public void MasteryLevels_GiveTheirTitlesAndFrames()
        {
            var (_, items) = New();
            var rabbit = ContentId.FromSource("species.rabbit");
            Assert.AreEqual(0, items.OnSpeciesMastery(rabbit, 29));
            Assert.AreEqual(1, items.OnSpeciesMastery(rabbit, 30));
            AssertOwned(items, ProfileItemKind.Title, "title.pal_rabbit", true);
            AssertOwned(items, ProfileItemKind.Title, "title.heart_rabbit", false);
            Assert.AreEqual(1, items.OnSpeciesMastery(rabbit, 100));
            AssertOwned(items, ProfileItemKind.Title, "title.heart_rabbit", true);
            Assert.AreEqual(0, items.OnSpeciesMastery(ContentId.FromSource("species.unknown"), 100));

            Assert.AreEqual(2, items.OnBiomeMastery(10, 20), "Lunar mastery 20: its mastery 10 title and its frame.");
            AssertOwned(items, ProfileItemKind.Title, "title.lunar_pal", true);
            AssertOwned(items, ProfileItemKind.Frame, "frame.biome_lunar", true);
            Assert.AreEqual(1, items.OnCompanionBond(101, 20));
            AssertOwned(items, ProfileItemKind.Pose, "pose.fill_cheeks", true);
            Assert.AreEqual(1, items.OnCompanionBond(101, 25));
            AssertOwned(items, ProfileItemKind.Title, "title.full_cheeks", true);
            Assert.AreEqual(8, items.OnPhotoModeUnlocked());
            Assert.AreEqual(1, items.OnPassSeason(2, 40, false));
            AssertOwned(items, ProfileItemKind.Pose, "pose.pass02_float_cloud", false);
        }

        private static void AssertOwned(ProfileItemService items, ProfileItemKind kind, string key, bool owned)
        {
            int index = ProfileData.IndexOf(kind, key);
            Assert.GreaterOrEqual(index, 0, key);
            Assert.AreEqual(owned, items.Owned(kind, index), key);
        }

        [Test]
        public void Save_RoundTrip_KeepsEverything()
        {
            var state = new PlayerState();
            var (a, items) = New(state.Achievements);
            items.GiveStarting();
            a.Add(AchievementCounter.Hatches, 300);
            a.TryClaim(Hatches);
            a.AddRunHatches(40);
            a.AddDayHatches(20261010, 7);
            a.AddDistinctId(AchievementCounter.AnimalsWished, 42);
            a.AddDistinctBit(AchievementCounter.PosesUsed, 46);
            a.AddSpeciesTraits(89, 0x3F);
            items.Grant("title.lucky_star");
            items.Grant("pose.wink");
            items.TrySelect(ProfileItemKind.Title, ProfileData.IndexOf(ProfileItemKind.Title, "title.lucky_star"));

            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(state, writer);
            var loaded = serializer.Deserialize(serializer.ReadDocument(writer.Buffer, writer.Length)).Achievements;
            var s = state.Achievements;
            CollectionAssert.AreEqual(s.Progress, loaded.Progress);
            CollectionAssert.AreEqual(s.Reached, loaded.Reached);
            CollectionAssert.AreEqual(s.Claimed, loaded.Claimed);
            Assert.AreEqual(40, loaded.RunHatches);
            Assert.AreEqual(20261010, loaded.DayKey);
            Assert.AreEqual(7, loaded.DayHatches);
            Assert.AreEqual(1, loaded.AnimalsWishedCount);
            Assert.AreEqual(42u, loaded.AnimalsWished[0]);
            Assert.AreEqual(1UL << 46, loaded.PosesUsed);
            Assert.AreEqual(0x3F, loaded.SpeciesTraits[89]);
            Assert.IsTrue(loaded.StartGiven);
            CollectionAssert.AreEqual(s.TitlesOwned, loaded.TitlesOwned);
            CollectionAssert.AreEqual(s.TitlesUnseen, loaded.TitlesUnseen);
            Assert.AreEqual(s.FramesOwned, loaded.FramesOwned);
            Assert.AreEqual(s.PosesOwned, loaded.PosesOwned);
            Assert.AreEqual(s.PosesUnseen, loaded.PosesUnseen);
            Assert.AreEqual(s.SelectedTitle, loaded.SelectedTitle);
            Assert.AreEqual(0, loaded.SelectedFrame);

            var reloaded = new AchievementService(loaded);
            Assert.AreEqual(a.Claimable, reloaded.Claimable);
            Assert.AreEqual(AchievementTier.Silver, reloaded.Claimed(Hatches));
        }

        [Test]
        public void Reads_AndACounterEvent_NeverAllocate()
        {
            var (a, items) = New();
            items.GiveStarting();
            long sink = 0;
            // Warm-up (JIT, static tables).
            Read(a, items, ref sink);
            a.Add(AchievementCounter.Pets);
            a.Add(AchievementCounter.Hatches, 10);
            a.AddDistinctId(AchievementCounter.AnimalsPetted, 1);

            long before = GC.GetAllocatedBytesForCurrentThread();
            Read(a, items, ref sink);
            a.Add(AchievementCounter.Pets);
            a.Add(AchievementCounter.Hatches, 300);
            a.Raise(AchievementCounter.RefugeLevel, 12);
            a.AddDistinctId(AchievementCounter.AnimalsPetted, 2);
            a.AddDayHatches(20261010, 1);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "Reads and counter events allocate nothing.");
            Assert.Greater(sink, 0);
        }

        private static void Read(AchievementService a, ProfileItemService items, ref long sink)
        {
            for (int i = 0; i < a.Count; i++)
            {
                var info = a.At(i);
                sink += info.Star + a.Progress(i) + (int)a.Reached(i) + (int)a.Claimed(i) + a.NextThreshold(i) + a.ToGo(i);
            }
            sink += a.Claimable;
            for (int k = 0; k < 3; k++)
            {
                var kind = (ProfileItemKind)k;
                for (int i = 0; i < items.Count(kind); i++)
                    sink += items.At(kind, i).Order + (items.Owned(kind, i) ? 1 : 0) + (items.Unseen(kind, i) ? 1 : 0);
                sink += items.Selected(kind) + items.UnseenCount(kind);
            }
        }
    }
}
