using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T44A: stars, the Albums Weekend, the choice, rewards once, fixed stickers and the hooks (doc 01 Albums, 1.4 to 1.8).</summary>
    public class AlbumServiceTests
    {
        private static readonly int Grooming = AlbumData.ActivityPage(ActivityId.Grooming);

        [Test]
        public void Duplicates_GiveStars_DoubledOnTheAlbumsWeekend()
        {
            var config = new AlbumConfig();
            var albums = AlbumDrawTests.Open(config, 1UL, 0, 1);
            int common = 1, rare = 7, shiny = 11;
            foreach (int s in new[] { common, rare, shiny })
                Assert.IsTrue(albums.GrantFixed(s, true));
            Assert.AreEqual(0, albums.AlbumStars, "New stickers give no star.");
            albums.GrantFixed(common, true);
            albums.GrantFixed(rare, true);
            albums.GrantFixed(shiny, true);
            Assert.AreEqual(1 + 2 + 3, albums.AlbumStars);
            config.WeekendActive = true;
            albums.GrantFixed(common, true);
            albums.GrantFixed(rare, true);
            albums.GrantFixed(shiny, true);
            Assert.AreEqual(6 + 2 + 4 + 6, albums.AlbumStars, "Week-end des Albums: Commun 2, Rare 4, Brillant 6.");
            Assert.IsFalse(albums.GrantFixed(common, false), "A fixed condition met again gives nothing.");

            albums.ClearPending();
            albums.GrantFixed(common, true);
            Assert.AreEqual(1, albums.PendingCount);
            var gain = albums.PendingAt(0);
            Assert.IsFalse(gain.New);
            Assert.AreEqual(2, gain.StarsAdded);
        }

        [Test]
        public void Choice_Costs10Stars_AMissingStickerOfAnOpenPage()
        {
            var albums = AlbumDrawTests.Open(null, 2UL, 0, Grooming);
            int zone = AlbumData.StickerAt(1, 3);
            int target = AlbumData.StickerAt(Grooming, 11);
            albums.AddStars(9);
            Assert.IsFalse(albums.TryChoose(target), "9 stars are not enough.");
            albums.AddStars(1);
            Assert.IsFalse(albums.TryChoose(zone), "Page 2 is not open.");
            var spent = new List<int>();
            albums.StarsSpent += spent.Add;
            Assert.IsTrue(albums.TryChoose(target), "Any rarity, fixed ones too: 10 stars.");
            Assert.AreEqual(0, albums.AlbumStars);
            Assert.IsTrue(albums.Owned(target));
            Assert.AreEqual(new[] { target }, spent.ToArray());
            albums.AddStars(10);
            Assert.IsFalse(albums.TryChoose(target), "Only a missing sticker.");
        }

        [Test]
        public void Rewards_AreGivenOnce()
        {
            var albums = AlbumDrawTests.Open(null, 3UL, 0, 1, Grooming);
            var rewards = new List<AlbumReward>();
            albums.RewardEarned += (in AlbumReward r) => rewards.Add(r);
            for (int slot = 0; slot < 5; slot++)
                albums.GrantFixed(AlbumData.StickerAt(Grooming, slot), false);
            Assert.AreEqual(0, rewards.Count);
            albums.GrantFixed(AlbumData.StickerAt(Grooming, 5), false);
            Assert.AreEqual(1, rewards.Count);
            Assert.AreEqual(AlbumRewardKind.PageHalf, rewards[0].Kind);
            Assert.AreEqual(3, rewards[0].Tickets, "An activity page at 6/12: 3 tickets.");
            Assert.AreEqual(0, rewards[0].Gems);
            Assert.IsTrue(albums.PendingAt(albums.PendingCount - 1).PageHalf);
            for (int slot = 0; slot < 12; slot++)
                albums.GrantFixed(AlbumData.StickerAt(Grooming, slot), true);
            Assert.AreEqual(2, rewards.Count, "Duplicates never give a reward again.");
            Assert.AreEqual(AlbumRewardKind.PageComplete, rewards[1].Kind);
            Assert.AreEqual(25, rewards[1].Gems);
            Assert.AreEqual("title.activity.001", rewards[1].Title);

            rewards.Clear();
            for (int s = 0; s < 24; s++)
                albums.GrantFixed(s, false);
            Assert.AreEqual(5, rewards.Count, "Prairie: page 1 half and complete, page 2 half and complete, the album.");
            Assert.AreEqual(0, rewards[0].Tickets, "A biome page at half: a decoration, no ticket.");
            Assert.AreEqual(ContentId.FromSource("decor.album.grassland.13"), rewards[0].Decoration);
            Assert.AreEqual(ContentId.FromSource("decor.album.grassland.14"), rewards[1].Decoration);
            Assert.AreEqual(AlbumRewardKind.AlbumComplete, rewards[4].Kind);
            Assert.AreEqual(ContentId.FromSource("landmark.album.grassland"), rewards[4].Landmark);
            Assert.AreEqual("title.album.grassland", rewards[4].Title);
            Assert.IsTrue(albums.PendingAt(albums.PendingCount - 1).AlbumComplete);
            albums.State.Owned[0] = 0;
            albums.Recount();
            albums.GrantFixed(0, false);
            Assert.AreEqual(5, rewards.Count, "Claimed bits are saved: never twice.");
        }

        [Test]
        public void SignatureMoment_IsGuaranteedOnce()
        {
            var albums = AlbumDrawTests.Open(null, 4UL, 0, 1);
            int signature = AlbumData.StickerAt(1, 11);
            Assert.IsTrue(albums.OnSignatureMomentSeen(0));
            Assert.IsTrue(albums.Owned(signature));
            int stars = albums.AlbumStars;
            Assert.IsFalse(albums.OnSignatureMomentSeen(0), "Once.");
            Assert.AreEqual(stars, albums.AlbumStars);
            Assert.AreEqual(1, albums.State.SignatureSeen);
            Assert.IsFalse(albums.OnSignatureMomentSeen(1), "The Forest's page is not open.");
        }

        [Test]
        public void EventStickers_FixedAndDuplicatesNextYear()
        {
            var albums = AlbumDrawTests.Open(null, 5UL);
            albums.OpenEventPage(101);
            int page = AlbumData.EventPage(101);
            Assert.AreEqual(1, albums.OnEventTrack(101, 1));
            Assert.AreEqual(0, albums.OnEventTrack(101, 2), "Tier 2 gives nothing.");
            Assert.AreEqual(1, albums.OnEventChallengeStars(101, 18));
            Assert.IsTrue(albums.Owned(AlbumData.StickerAt(page, 7)));
            Assert.AreEqual(1, albums.OnEventQuest(101, 10));
            Assert.IsTrue(albums.Owned(AlbumData.StickerAt(page, 11)));
            Assert.AreEqual(1, albums.OnEventTrack(101, 1), "Next year: the same tier, a duplicate.");
            Assert.AreEqual(1, albums.AlbumStars);
            Assert.IsTrue(albums.GrantFromEventShop(AlbumData.StickerAt(page, 5), 101));
            Assert.IsFalse(albums.GrantFromEventShop(AlbumData.StickerAt(page, 5), 101), "Only a missing one.");
            Assert.IsFalse(albums.GrantFromEventShop(AlbumData.StickerAt(page + 1, 0), 101), "Only the running event's page.");
        }

        [Test]
        public void Milestones_GiveFixedStickersAtTheirThresholds()
        {
            var albums = AlbumDrawTests.Open(null, 6UL, Grooming, AlbumData.ActivityPage(ActivityId.Garden));
            Assert.AreEqual(1, albums.GrantMilestones(Grooming, StickerFixedSource.ChallengeStars, 12));
            Assert.IsTrue(albums.Owned(AlbumData.StickerAt(Grooming, 8)));
            Assert.AreEqual(2, albums.GrantMilestones(Grooming, StickerFixedSource.ChallengeStars, 30));
            Assert.AreEqual(0, albums.GrantMilestones(Grooming, StickerFixedSource.ChallengeStars, 30));
            int garden = AlbumData.ActivityPage(ActivityId.Garden);
            Assert.AreEqual(1, albums.GrantMilestones(garden, StickerFixedSource.Harvests, 49), "Première récolte.");
            Assert.AreEqual(1, albums.GrantMilestones(garden, StickerFixedSource.Harvests, 50));
            Assert.AreEqual(0, albums.GrantMilestones(AlbumData.ActivityPage(ActivityId.Expeditions), StickerFixedSource.ExpeditionsDone, 100),
                "A closed page gives nothing.");
        }

        [Test]
        public void ClosedAlbums_GiveNothing()
        {
            var albums = new AlbumService(new AlbumState { Seed = 3UL });
            Assert.IsFalse(albums.IsOpen);
            Assert.AreEqual(-1, albums.Draw(new StickerDrawRequest(StickerSource.DailyGift)));
            Assert.IsFalse(albums.GrantFixed(0, true));
            Assert.IsFalse(albums.OpenPage(0));
            Assert.AreEqual(-1, albums.GivePaid(ActivityId.Grooming));
            Assert.AreEqual(0, albums.PendingCount);
        }

        [Test]
        public void Config_RemoteValuesStayInsideTheirBounds()
        {
            var config = new AlbumConfig();
            Assert.IsTrue(config.TryOverride("albums.guarantee.shiny", 40));
            Assert.AreEqual(40, config.ShinyAfter);
            Assert.IsFalse(config.TryOverride("albums.guarantee.shiny", 2.5));
            Assert.IsFalse(config.TryOverride("albums.season_share", 0.9));
            Assert.IsFalse(config.TryOverride("albums.unknown", 1));
            Assert.IsTrue(config.TryOverride("events.weekend.albums_multiplier", 3));
            Assert.IsTrue(config.TryOverrideFlag(AlbumConfig.EnabledFlag, false));
            Assert.IsFalse(new AlbumService(new AlbumState(), config) { Unlocked = true }.IsOpen, "flags.albums off hides them.");
            Assert.AreEqual(1, AlbumHooks.SeasonOfMonth(3));
            Assert.AreEqual(2, AlbumHooks.SeasonOfMonth(8));
            Assert.AreEqual(3, AlbumHooks.SeasonOfMonth(11));
            Assert.AreEqual(4, AlbumHooks.SeasonOfMonth(1));
            Assert.AreEqual(160, (int)Services.Analytics.AnalyticsEventId.AlbumStickerGained);
            Assert.AreEqual(165, (int)Services.Analytics.AnalyticsEventId.AlbumInfoOpened);
        }

        // ---------------- Hooks: the opening, zones, pending stickers ----------------

        private static (GardenRig rig, AlbumService albums, AlbumHooks hooks) Rig(int level, int day, int pending = 0)
        {
            var rig = GardenRig.Create(level: level, now: TestUtil.T0 + TimeSpan.FromDays(day - 1));
            rig.State.Inventory.PendingStickers = pending;
            var albums = new AlbumService(rig.State.Albums);
            var hooks = new AlbumHooks(albums, rig.A, rig.Daily, null, rig.G);
            return (rig, albums, hooks);
        }

        [Test]
        public void Opening_AtLevel12AndDay5_GivesTheOfferedProgressAndTheZoneStickers()
        {
            var (_, closed, closedHooks) = Rig(11, 6);
            Assert.IsFalse(closed.IsOpen, "Level 11: closed.");
            Assert.IsFalse(closed.Owned(0));
            closedHooks.Dispose();

            var (early, _, earlyHooks) = Rig(12, 4);
            Assert.IsFalse(early.State.Albums.Opened, "Day 4: closed.");
            earlyHooks.Dispose();

            var (rig, albums, hooks) = Rig(12, 5, pending: 2);
            Assert.IsTrue(albums.IsOpen);
            Assert.IsTrue(rig.State.Albums.Opened);
            Assert.IsTrue(albums.IsPageOpen(0) && albums.IsPageOpen(1), "The Prairie album opens at once.");
            Assert.IsTrue(albums.Owned(0), "STK-GL-01 Lapin curieux offered.");
            Assert.IsTrue(albums.Owned(6), "STK-GL-07 the companion in silhouette.");
            Assert.IsTrue(albums.Owned(12), "STK-GL-13 the Start, the arrival zone open.");
            Assert.GreaterOrEqual(albums.OwnedOnPage(0), 2, "Page 1 starts at 2 of 12.");
            Assert.AreEqual(20, albums.AlbumStars, "2 stickers kept before: a sticker of choice each.");
            Assert.AreEqual(0, rig.State.Inventory.PendingStickers);
            Assert.IsTrue(albums.IsPageOpen(AlbumData.SeasonPage(hooks.CurrentSeason())), "The season's page.");
            int owned = albums.OwnedOnPage(0) + albums.OwnedOnPage(1);
            hooks.Refresh();
            Assert.AreEqual(owned, albums.OwnedOnPage(0) + albums.OwnedOnPage(1), "The opening happens once.");
            Assert.IsTrue(rig.A.Config.AlbumsAvailable && rig.Daily.Config.AlbumsAvailable, "Sticker lots are real from now on.");
            hooks.Dispose();
        }

        [Test]
        public void BundleStickers_GoToTheAlbums_OnceOpen()
        {
            var (rig, albums, hooks) = Rig(12, 5);
            var bundle = new RewardBundle { Stickers = 1 };
            rig.A.Context.Granter.Grant(in bundle, EconomyReason.ReturnGift, DiscoverySource.Debug);
            Assert.AreEqual(0, rig.State.Inventory.PendingStickers);
            Assert.AreEqual(10, albums.AlbumStars, "A return gift's sticker of choice: 10 stars.");
            rig.A.Context.Granter.Grant(in bundle, EconomyReason.DailyGift, DiscoverySource.Debug);
            Assert.AreEqual(0, rig.State.Inventory.PendingStickers, "The daily gift's lot is drawn by its hook.");
            hooks.Dispose();
            rig.A.Context.Granter.Grant(in bundle, EconomyReason.DailyGift, DiscoverySource.Debug);
            Assert.AreEqual(1, rig.State.Inventory.PendingStickers, "Without the hooks: kept as before.");
        }

        [Test]
        public void Save_RoundTripsTheAlbums()
        {
            var (rig, albums, hooks) = Rig(12, 5);
            albums.AddStars(17);
            albums.State.ShinyDraws[3] = 9;
            albums.State.Records[2] = 4;
            albums.State.GardenSpedUp = 4;
            var copy = ExpeditionRig.RoundTrip(rig.State).Albums;
            Assert.AreEqual(albums.State.Stars, copy.Stars);
            Assert.AreEqual(albums.State.Owned, copy.Owned);
            Assert.AreEqual(albums.State.Unseen, copy.Unseen);
            Assert.AreEqual(albums.State.PagesOpen, copy.PagesOpen);
            Assert.AreEqual(albums.State.Seed, copy.Seed);
            Assert.AreEqual(9, copy.ShinyDraws[3]);
            Assert.AreEqual(4, copy.Records[2]);
            Assert.AreEqual(4, copy.GardenSpedUp);
            Assert.IsTrue(copy.Opened);
            hooks.Dispose();
        }
    }
}
