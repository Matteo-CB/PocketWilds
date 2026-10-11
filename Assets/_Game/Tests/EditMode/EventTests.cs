using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Economy;
using PocketWilds.Editor.Events;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T48 events framework: the accelerated test event played end to end, its guarantees, saves and the pacing targets.</summary>
    public class EventTests
    {
        private static readonly ContentId GoldProduct = ContentId.FromSource("product.event.goldbook");
        private static readonly ContentId BoxProduct = ContentId.FromSource("product.event.chest");

        private sealed class Rig
        {
            public readonly FakeGameClock Clock;
            public readonly PlayerState State;
            public readonly ContentRegistry Content;
            public readonly EconomyService Economy;
            public readonly AlbumService Albums;
            public readonly MasteryService Mastery;
            public readonly GrantService Grants;
            public readonly EventService Events;
            public readonly EventConfig Config;
            public readonly EventCalendarEntry Entry;

            public Rig(int version = 1, int year = EventTestFixture.Year, bool albumOpen = true, PlayerState state = null, params EventCalendarEntry[] more)
            {
                var builder = new ContentRegistryBuilder();
                EventTestFixture.AddContent(builder, version);
                builder.AddProduct(new ProductDefinition(GoldProduct, "pw.event.goldbook", ProductKind.Consumable, 499, 0, null, EventProductScope.GoldBook),
                    "product.event.goldbook");
                builder.AddProduct(new ProductDefinition(BoxProduct, "pw.event.chest", ProductKind.Consumable, 999, 800, null, EventProductScope.Box),
                    "product.event.chest");
                Content = builder.Build();
                Entry = EventTestFixture.Entry(year, version);
                Clock = new FakeGameClock(Entry.StartUtc.AddDays(-5));
                State = state ?? PlayerState.CreateNew(Clock.UtcNow);
                Economy = new EconomyService(State, Content, EconomyConfig.Default, null);
                Mastery = new MasteryService(State, Economy.Config, Economy);
                Albums = new AlbumService(State.Albums) { Unlocked = albumOpen };
                Grants = new GrantService(Content, State, null);
                Config = new EventConfig();
                var entries = new List<EventCalendarEntry>(more) { Entry };
                Events = new EventService(State, Content, Clock, null, Config, EventTestFixture.Calendar(entries.ToArray()))
                {
                    Economy = Economy, Granter = new RewardGranter(State, Economy, null, null), Mastery = Mastery, Albums = Albums,
                    DefaultTreat = () => ContentId.FromSource(EventTestFixture.Treat),
                };
                Grants.Events = Events;
                if (state == null)
                {
                    var animal = new AnimalInstance
                    {
                        Species = ContentId.FromSource(EventTestFixture.RabbitSpecies), Variant = ContentId.FromSource("variant.testrabbit.plain"), Seed = 7,
                        Rarity = RarityTier.Common,
                    };
                    animal.InstanceId = State.Collection.Add(animal);
                    State.Collection.DiscoveredSpecies.Add(animal.Species);
                    State.Collection.DiscoveredVariants.Add(animal.Variant);
                    Economy.OnAnimalAdded(animal);
                }
            }

            public void At(double days) => Clock.Set(Entry.StartUtc.AddDays(days));

            public EventInstanceState Inst => Events.Instance;

            public int StickerOnPage(int slot) => AlbumData.StickerAt(AlbumData.EventPage(EventTestFixture.AlbumNumber), slot);

            public bool Owns(string item) => State.Entitlements.Owned.Contains(ContentId.FromSource(item));
        }

        [Test]
        public void Phases_FollowTheClock_AnnounceRunEnd()
        {
            var rig = new Rig();
            rig.Clock.Set(rig.Entry.StartUtc.AddHours(-49));
            Assert.AreEqual(EventPhase.None, rig.Events.Phase);
            rig.Clock.Set(rig.Entry.StartUtc.AddHours(-47));
            Assert.AreEqual(EventPhase.Announce, rig.Events.Phase);
            Assert.IsFalse(rig.Events.Join(), "No entry during the announce.");
            rig.Events.GrantEventCurrency(40, ActivityId.HideAndSeek);
            Assert.IsNull(rig.Events.Instance, "No currency before the run.");
            rig.At(0);
            Assert.AreEqual(EventPhase.Running, rig.Events.Phase);
            Assert.AreEqual(rig.Entry.EndUtc, rig.Events.EndUtc);
            Assert.AreEqual(new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero), rig.Entry.StartUtc, "2nd Friday 10:00 UTC.");
            Assert.AreEqual(14, (rig.Entry.EndUtc - rig.Entry.StartUtc).TotalDays);
            Assert.IsTrue(rig.Events.Join());
            Assert.AreEqual(1, rig.Events.Day);
            rig.Clock.Set(rig.Entry.EndUtc);
            Assert.AreEqual(EventPhase.None, rig.Events.Phase);
            Assert.IsTrue(rig.Inst == null && rig.State.Events.Instances[0].Ended);
        }

        [Test]
        public void Join_FreezesParameters_AndOnlyGenerousChangesPass()
        {
            var rig = new Rig();
            rig.At(0.1);
            Assert.IsTrue(rig.Config.TryOverride("events.egg_cost", 400));
            Assert.IsTrue(rig.Events.Join());
            Assert.AreEqual(400, rig.Events.EggCost, "Frozen at the first entry.");
            Assert.IsTrue(rig.Events.Albums.IsPageOpen(AlbumData.EventPage(EventTestFixture.AlbumNumber)), "The album page opens.");
            rig.Config.TryOverride("events.egg_cost", 500);
            rig.Config.TryOverride("events.duplicate_refund", 90);
            var refused = new List<EventParam>();
            Assert.AreEqual(1, rig.Events.OnConfigApplied(refused));
            Assert.AreEqual(EventParam.EggCost, refused[0]);
            Assert.AreEqual(400, rig.Events.EggCost, "A stingier change is refused mid-instance.");
            Assert.AreEqual(90, rig.Inst.Frozen.Int(EventParam.DuplicateRefund), "A more generous change passes.");
            rig.Config.TryOverride("events.egg_cost", 250);
            rig.Events.OnConfigApplied();
            Assert.AreEqual(250, rig.Events.EggCost);
            Assert.IsFalse(rig.Config.TryOverride("events.egg_cost", 700), "Out of bounds.");
            Assert.IsFalse(rig.Config.ApplyDomain(new Dictionary<string, double> { ["events.egg_cost"] = 200, ["events.unknown"] = 1 }), "Discarded whole.");
            Assert.AreEqual(250, rig.Config.Params.Int(EventParam.EggCost));
            Assert.IsTrue(EventConfigGate.CanApply(new EventApplyMoment { ColdStart = true }));
            Assert.IsFalse(EventConfigGate.CanApply(new EventApplyMoment { SecondsInBackground = 1700 }));
            Assert.IsTrue(EventConfigGate.CanApply(new EventApplyMoment { SecondsInBackground = 1800 }));
            Assert.IsFalse(EventConfigGate.CanApply(new EventApplyMoment { ColdStart = true, Hatching = true }));
            Assert.IsFalse(EventConfigGate.CanApply(new EventApplyMoment { ColdStart = true, ModalOpen = true }));
            var gate = new EventConfigGate();
            gate.Offer(new Dictionary<string, double> { ["events.duplicate_refund"] = 100 });
            Assert.IsFalse(gate.TryApply(rig.Config, new EventApplyMoment { ColdStart = true, ActivityRunning = true }, rig.Events));
            Assert.IsTrue(gate.TryApply(rig.Config, new EventApplyMoment { ColdStart = true }, rig.Events));
            Assert.AreEqual(100, rig.Inst.Frozen.Int(EventParam.DuplicateRefund));
        }

        [Test]
        public void Currency_FromEverySource_PointsNeverLowered()
        {
            var rig = new Rig();
            rig.At(0.2);
            var e = rig.Events;
            Assert.IsTrue(e.IsFeatured(ActivityId.HideAndSeek));
            Assert.IsFalse(e.IsFeatured(ActivityId.Grooming));
            e.GrantEventCurrency(60, ActivityId.HideAndSeek);
            Assert.IsNotNull(rig.Inst, "The first currency joins (nothing is lost).");
            e.OnWishClaimed();
            e.OnEggHatched();
            e.GrantEventCurrency(50, ActivityId.Expeditions);
            Assert.AreEqual(60 + 15 + 2 + 50, rig.Inst.Points);
            int before = rig.Inst.Currency;
            e.ReportChallenge(0, 1);
            Assert.AreEqual(before + 25, rig.Inst.Currency, "A challenge star: 25 once.");
            e.ReportChallenge(0, 1);
            Assert.AreEqual(before + 25, rig.Inst.Currency);
            // Complete a daily quest of the Rendez-vous group and the first long quest, whatever was drawn.
            int quests = 0;
            var daily = rig.Inst.Daily[0];
            before = rig.Inst.Currency;
            e.Report((EventGoalKind)daily.Goal, daily.Target);
            Assert.IsTrue(rig.Inst.Daily[0].Done);
            quests++;
            Assert.GreaterOrEqual(rig.Inst.Currency - before, 40);
            var firstLong = rig.Inst.Long[0];
            before = rig.Inst.LongDone;
            e.Report((EventGoalKind)firstLong.Goal, firstLong.Target);
            Assert.IsTrue(rig.Inst.Long[0].Done);
            Assert.AreEqual(before + 1, rig.Inst.LongDone);
            Assert.Greater(quests, 0);
            rig.Inst.Currency += 1000;
            int points = rig.Inst.Points;
            Assert.AreEqual(EventEggStatus.Opened, e.OpenEgg().Status);
            Assert.AreEqual(points, rig.Inst.Points, "Spending never lowers the track.");
            e.ActivityShipped = a => a != ActivityId.HideAndSeek;
            Assert.IsTrue(e.IsFeatured(ActivityId.Grooming), "The fallback activity when the featured one is not shipped.");
        }

        [Test]
        public void Track_ClaimsGoldRetroactive_BonusOnPointsOnly_SouvenirAtTier5()
        {
            var rig = new Rig();
            rig.At(0.5);
            var e = rig.Events;
            e.GrantEventCurrency(1200, ActivityId.HideAndSeek);
            Assert.AreEqual(10, e.Tier);
            Assert.AreEqual(1200, e.PointsForTier(10));
            Assert.AreEqual(6000, e.PointsForTier(30));
            Assert.AreNotEqual(0, rig.Inst.SouvenirUnixDay, "The dated Souvenir at tier 5 for every player.");
            Assert.IsTrue(rig.Owns(EventTestFixture.Souvenir));
            Assert.AreEqual(EventClaimStatus.NeedsGold, e.Claim(1, true));
            long gems = rig.State.Wallet.Gems;
            Assert.AreEqual(EventClaimStatus.Claimed, e.Claim(2, false));
            Assert.AreEqual(gems + 25, rig.State.Wallet.Gems);
            Assert.AreEqual(EventClaimStatus.AlreadyClaimed, e.Claim(2, false));
            Assert.AreEqual(EventClaimStatus.NotReached, e.Claim(11, false));
            Assert.AreEqual(OfferAvailability.Available, e.CheckEventOffer(rig.Content.Shop.Products.Get(GoldProduct)));
            var grant = rig.Grants.GrantPurchase("tx-gold", GoldProduct, ContentId.None);
            Assert.IsTrue(grant.IsNewGrant);
            Assert.AreEqual(0, grant.Gems);
            Assert.IsTrue(e.GoldOwned);
            Assert.AreEqual(OfferAvailability.Owned, e.CheckEventOffer(rig.Content.Shop.Products.Get(GoldProduct)));
            Assert.AreEqual(GrantStatus.AlreadyGranted, rig.Grants.GrantPurchase("tx-gold", GoldProduct, ContentId.None).Status);
            int claimed = e.ClaimAll();
            Assert.AreEqual(9 + 10, claimed, "Free 1 and 3-10, gold 1-10 (retroactive).");
            Assert.IsTrue(rig.Owns(EventTestFixture.Outfit(5)), "Gold T10 outfit.");
            Assert.IsTrue(rig.State.Collection.DiscoveredVariants.Contains(ContentId.FromSource(EventTestFixture.Variant(1))), "v1 always free at T10.");
            Assert.IsTrue(rig.Albums.Owned(rig.StickerOnPage(0)), "Track sticker 1 through the album.");
            int currency = rig.Inst.Currency, points = rig.Inst.Points;
            e.GrantEventCurrency(100, ActivityId.Grooming);
            Assert.AreEqual(currency + 100, rig.Inst.Currency, "The Carnet d'or never adds currency.");
            Assert.AreEqual(points + 110, rig.Inst.Points, "+10 % track points.");
            var box = rig.Grants.GrantPurchase("tx-box", BoxProduct, ContentId.None);
            Assert.AreEqual(800, box.Gems);
            Assert.IsTrue(e.BoxOwned && rig.Owns(EventTestFixture.Outfit(7)) && rig.Owns(EventTestFixture.Luxury(4)));
            Assert.AreEqual(0, rig.Grants.GrantPurchase("tx-box-restore", BoxProduct, ContentId.None, restore: true).Gems, "Never Gems twice.");
        }

        [Test]
        public void EggRules_GuaranteesHold_OverManySeeds()
        {
            int secretHits = 0, secretChances = 0;
            for (int seed = 1; seed <= 3000; seed++)
            {
                var counters = new EventEggCounters();
                uint rng = EventEggRules.Seed(new Guid(seed, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10), "event.test.2026");
                int completeAt = 0;
                for (int egg = 1; egg <= 30; egg++)
                {
                    bool secretBefore = counters.Secret;
                    int missingBefore = 7 - EventEggRules.Owned(counters.Owned, 7);
                    var draw = EventEggRules.Draw(ref counters, ref rng, 7, 3, 21, 0.01);
                    if (!secretBefore)
                    {
                        secretChances++;
                        if (draw.Secret)
                            secretHits++;
                    }
                    if (missingBefore > 0)
                        Assert.Less(counters.Misses, 3, $"Seed {seed}: a new item at least every 3 eggs.");
                    if (completeAt == 0 && EventEggRules.Owned(counters.Owned, 7) == 7)
                        completeAt = egg;
                    Assert.IsFalse(draw.Guaranteed && !draw.IsNew);
                }
                Assert.That(completeAt, Is.InRange(7, 21), $"Seed {seed}: all 7 by egg 21.");
            }
            double rate = secretHits / (double)secretChances;
            Assert.That(rate, Is.InRange(0.007, 0.013), "The secret at 1 % an egg.");
            // The secret is outside the guarantee: a certain secret changes nothing in the items' guarantees.
            var c = new EventEggCounters();
            uint r = 12345;
            for (int egg = 1; egg <= 21; egg++)
                EventEggRules.Draw(ref c, ref r, 7, 3, 21, 1.0);
            Assert.AreEqual(7, EventEggRules.Owned(c.Owned, 7));
            Assert.IsTrue(c.Secret);
            Assert.AreEqual(1, EventEggRules.EggsUntilNew(new EventEggCounters { Opened = 2, Misses = 2, Owned = 1 }, 7, 3, 21));
        }

        [Test]
        public void Egg_Service_AllItemsBy21_DuplicatesRefund60AndMasteryXp()
        {
            var rig = new Rig();
            rig.At(1);
            rig.Events.Join();
            rig.Inst.Currency = 30 * 300;
            int duplicates = 0;
            for (int i = 0; i < 25; i++)
            {
                long earned = rig.Inst.CurrencyEarned;
                var masteryBefore = new Dictionary<ContentId, SpeciesMastery>(rig.State.Mastery.Species);
                var egg = rig.Events.OpenEgg();
                Assert.AreEqual(EventEggStatus.Opened, egg.Status);
                if (i < 21 && !egg.IsNew)
                    Assert.Less(rig.Inst.EggMisses, 3);
                if (!egg.IsNew)
                {
                    duplicates++;
                    Assert.AreEqual(60, egg.Refund);
                    Assert.GreaterOrEqual(rig.Inst.CurrencyEarned - earned, 60, "60 currency back.");
                    var species = rig.Content.Variants.Get(egg.Item).Species;
                    masteryBefore.TryGetValue(species, out var m0);
                    var m1 = rig.State.Mastery.Species[species];
                    Assert.IsTrue(m1.Level > m0.Level || m1.Xp >= m0.Xp + 2, "2 species mastery XP.");
                }
                if (i == 20)
                    Assert.AreEqual(7, rig.Events.EggItemsOwned, "All 7 by egg 21.");
            }
            Assert.Greater(duplicates, 0);
            Assert.AreEqual(25, rig.Inst.EggsOpened);
            Assert.AreEqual(0, rig.Events.EggsUntilNew);
            Assert.IsTrue(rig.State.Collection.DiscoveredVariants.Contains(ContentId.FromSource(EventTestFixture.Variant(2))));
            Assert.AreEqual(EventEggStatus.NotEnoughCurrency, NoCurrency(rig).Status);
        }

        private static EventEggResult NoCurrency(Rig rig)
        {
            rig.Inst.Currency = 10;
            return rig.Events.OpenEgg();
        }

        [Test]
        public void Shop_RespectsItsLimits()
        {
            var rig = new Rig();
            rig.At(1);
            rig.Events.Join();
            rig.Inst.Currency = 20_000;
            int tickets = Line(rig, "shop.tickets"), outfit = Line(rig, "shop.outfit_4"), seeds = Line(rig, "shop.seeds"), sticker = Line(rig, "shop.sticker");
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(EventBuyStatus.Bought, rig.Events.Buy(tickets));
            Assert.AreEqual(EventBuyStatus.SoldOut, rig.Events.Buy(tickets));
            Assert.AreEqual(15, rig.State.Inventory.Tickets);
            Assert.AreEqual(EventBuyStatus.Bought, rig.Events.Buy(outfit));
            Assert.AreEqual(EventBuyStatus.Owned, rig.Events.Buy(outfit));
            Assert.AreEqual(20_000 - 5 * 120 - 900, rig.Inst.Currency);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(EventBuyStatus.Bought, rig.Events.Buy(seeds));
            Assert.AreEqual(EventBuyStatus.SoldOut, rig.Events.Buy(seeds));
            int missing = rig.Events.ShopRemaining(sticker);
            Assert.AreEqual(12, missing);
            Assert.AreEqual(EventBuyStatus.Bought, rig.Events.Buy(sticker, rig.StickerOnPage(5)));
            Assert.AreEqual(EventBuyStatus.NotMissing, rig.Events.Buy(sticker, rig.StickerOnPage(5)));
            Assert.AreEqual(missing - 1, rig.Events.ShopRemaining(sticker));
            Assert.AreEqual(-1, rig.Events.ShopRemaining(Line(rig, "shop.egg")));
            Assert.AreEqual(EventBuyStatus.Bought, rig.Events.Buy(Line(rig, "shop.egg")));
        }

        private static int Line(Rig rig, string key)
        {
            for (int i = 0; i < rig.Events.ShopCount; i++)
                if (rig.Events.ShopItem(i).Key == key)
                    return i;
            throw new AssertionException("No line " + key);
        }

        [Test]
        public void Quests_ThreeADay_OneFreeSwap_PendingMax9_LongStickers()
        {
            var rig = new Rig();
            rig.At(0.05);
            rig.Events.Join();
            var daily = rig.Events.DailyQuests;
            Assert.AreEqual(3, daily.Count);
            for (int g = 0; g < 3; g++)
                Assert.AreEqual(g, daily[g].Group, "One quest from each group.");
            Assert.AreEqual(10, rig.Events.LongQuests.Count);
            byte before = daily[1].Template;
            Assert.IsTrue(rig.Events.Swap(1));
            Assert.AreNotEqual(before, rig.Events.DailyQuests[1].Template);
            Assert.AreEqual(1, rig.Events.DailyQuests[1].Group);
            Assert.IsFalse(rig.Events.Swap(0), "One free swap a day.");
            for (int d = 1; d < 6; d++)
            {
                rig.At(d + 0.05);
                rig.Events.Tick();
            }
            Assert.AreEqual(9, rig.Events.PendingDailyCount, "Undone quests stay, at most 9.");
            rig.Events.Tick();
            Assert.IsTrue(rig.Events.Swap(0), "A new day, a new swap.");
            var inst = rig.Inst;
            // Only the quests in 4th, 7th and 10th position carry a sticker: finishing the first one gives none.
            Finish(inst.Long, 0);
            Assert.IsFalse(rig.Albums.Owned(rig.StickerOnPage(9)), "The first long quest done carries no sticker.");
            for (int i = 1; i < 10; i++)
                Finish(inst.Long, i);
            Assert.AreEqual(10, inst.LongDone);
            Assert.IsTrue(rig.Albums.Owned(rig.StickerOnPage(9)) && rig.Albums.Owned(rig.StickerOnPage(10)) && rig.Albums.Owned(rig.StickerOnPage(11)),
                "Long quests 4, 7 and 10 give their stickers.");

            void Finish(List<EventQuestRecord> list, int index)
            {
                var q = list[index];
                if (!q.Done)
                    rig.Events.Report((EventGoalKind)q.Goal, q.Target - q.Progress);
                Assert.IsTrue(list[index].Done, ((EventGoalKind)q.Goal).ToString());
            }
        }

        [Test]
        public void Quests_Catalog_IsTheDocs()
        {
            Assert.AreEqual(30, EventQuestCatalog.Daily.Length);
            Assert.AreEqual(20, EventQuestCatalog.Long.Length);
            for (int i = 0; i < 30; i++)
                Assert.AreEqual(i + 1, EventQuestCatalog.Daily[i].Number);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(i + 1, EventQuestCatalog.Long[i].Number);
            void Check(EventQuestTemplate t, EventQuestGroup group, EventGoalKind goal, int target, EventFeatureGate gate)
            {
                Assert.AreEqual(group, t.Group, t.Code);
                Assert.AreEqual(goal, t.Goal, t.Code);
                Assert.AreEqual(target, t.Target, t.Code);
                Assert.AreEqual(gate, t.Gate, t.Code);
            }
            var d = EventQuestCatalog.Daily;
            var l = EventQuestCatalog.Long;
            Check(d[0], EventQuestGroup.Refuge, EventGoalKind.HatchEggs, 10, EventFeatureGate.None);
            Check(d[1], EventQuestGroup.Featured, EventGoalKind.OpenEventEggs, 1, EventFeatureGate.None);
            Assert.AreEqual(2, d[1].FromDay, "EQ-D-02 from event day 2.");
            Check(d[2], EventQuestGroup.Featured, EventGoalKind.FeaturedRounds, 2, EventFeatureGate.FeaturedActivity);
            Check(d[10], EventQuestGroup.Refuge, EventGoalKind.DecorationsPlaced, 1, EventFeatureGate.Decorations);
            Check(d[16], EventQuestGroup.RendezVous, EventGoalKind.LightCollections, 5, EventFeatureGate.None);
            Check(d[21], EventQuestGroup.Featured, EventGoalKind.DayActivityRounds, 1, EventFeatureGate.Activities);
            Check(d[22], EventQuestGroup.RendezVous, EventGoalKind.BiomeVisits, 3, EventFeatureGate.None);
            Check(d[24], EventQuestGroup.RendezVous, EventGoalKind.EarnCurrency, 200, EventFeatureGate.None);
            Check(d[27], EventQuestGroup.Refuge, EventGoalKind.BreedingsStarted, 1, EventFeatureGate.Breeding);
            Check(d[29], EventQuestGroup.Refuge, EventGoalKind.LongExpeditionStarts, 2, EventFeatureGate.Expeditions);
            Check(l[7], EventQuestGroup.RendezVous, EventGoalKind.TrackTier, 15, EventFeatureGate.None);
            Check(l[13], EventQuestGroup.Featured, EventGoalKind.EventEggItems, 3, EventFeatureGate.None);
            Check(l[14], EventQuestGroup.RendezVous, EventGoalKind.EarnCurrency, 3000, EventFeatureGate.None);
            Check(l[18], EventQuestGroup.Refuge, EventGoalKind.NewVariants, 3, EventFeatureGate.None);
            int featured = 0, rendezVous = 0, refuge = 0;
            foreach (var t in d)
            {
                featured += t.Group == EventQuestGroup.Featured ? 1 : 0;
                rendezVous += t.Group == EventQuestGroup.RendezVous ? 1 : 0;
                refuge += t.Group == EventQuestGroup.Refuge ? 1 : 0;
            }
            Assert.AreEqual(new[] { 8, 7, 15 }, new[] { featured, rendezVous, refuge }, "The doc's groups.");
        }

        [Test]
        public void Quests_LockedTemplatesAreNeverDrawn()
        {
            var closed = new HashSet<EventFeatureGate> { EventFeatureGate.Expeditions, EventFeatureGate.Garden, EventFeatureGate.Grooming };
            var rig = new Rig();
            // The game's missing features (decorations, photos, breeding) stay locked even when the override says open.
            rig.Events.FeatureOpen = gate => !closed.Contains(gate);
            rig.At(0.05);
            rig.Events.Join();
            void CheckAll()
            {
                foreach (var q in rig.Inst.Daily)
                {
                    var t = EventQuestCatalog.Daily[q.Template - 1];
                    Assert.IsFalse(closed.Contains(t.Gate) || EventQuestCatalog.AlwaysLocked(t.Gate), t.Code + " needs a locked feature.");
                }
                foreach (var q in rig.Inst.Long)
                {
                    var t = EventQuestCatalog.Long[q.Template - 1];
                    Assert.IsFalse(closed.Contains(t.Gate) || EventQuestCatalog.AlwaysLocked(t.Gate), t.Code + " needs a locked feature.");
                }
            }
            for (int day = 0; day < 14; day++)
            {
                rig.At(day + 0.05);
                rig.Events.Tick();
                CheckAll();
                // A swap, then every quest of the day finished so that 3 new ones come each day (every template gets its chance).
                rig.Events.Swap(0);
                CheckAll();
                for (int i = 0; i < rig.Inst.Daily.Count; i++)
                    if (!rig.Inst.Daily[i].Done)
                        rig.Events.Report((EventGoalKind)rig.Inst.Daily[i].Goal, rig.Inst.Daily[i].Target);
            }
            Assert.AreEqual(10, rig.Inst.Long.Count);
            for (int i = 1; i < 10; i++)
                Assert.Less(rig.Inst.Long[i - 1].Template, rig.Inst.Long[i].Template, "Long quests listed in template order.");
        }

        [Test]
        public void Quests_LongDraw_ByUnlockedFeatures_ReplacedInTheirGroup()
        {
            var drawn = new HashSet<int>();
            for (int year = 2030; year < 2042; year++)
            {
                var rig = new Rig(year: year);
                rig.Events.FeatureOpen = gate => gate != EventFeatureGate.Activities && gate != EventFeatureGate.FeaturedActivity && gate != EventFeatureGate.Grooming;
                rig.At(0.05);
                Assert.IsTrue(rig.Events.Join());
                var longs = rig.Inst.Long;
                Assert.AreEqual(10, longs.Count, "10 long quests an event.");
                foreach (var q in longs)
                {
                    var t = EventQuestCatalog.Long[q.Template - 1];
                    Assert.IsTrue(t.Gate == EventFeatureGate.None || t.Gate == EventFeatureGate.Expeditions || t.Gate == EventFeatureGate.Garden, t.Code);
                    Assert.AreEqual((byte)t.Group, q.Group);
                    Assert.AreEqual(t.Target, q.Target);
                    drawn.Add(q.Template);
                }
            }
            Assert.Greater(drawn.Count, 10, "The draw varies between events.");
        }

        [Test]
        public void Quests_DayActivityAndQuestActivity()
        {
            var rig = new Rig();
            rig.At(0.05);
            rig.Events.Join();
            var inst = rig.Inst;
            rig.Events.ActivityShipped = a => a == ActivityId.HideAndSeek || a == ActivityId.Grooming;
            inst.Daily.Clear();
            inst.QuestDay = 0;
            rig.At(1.05);
            rig.Events.Tick();
            int day = -1;
            for (int i = 0; i < inst.Daily.Count; i++)
                if (inst.Daily[i].Goal == (byte)EventGoalKind.DayActivityRounds)
                    day = i;
            if (day < 0)
            {
                inst.Daily.Add(new EventQuestRecord { Template = 22, Group = 0, Goal = (byte)EventGoalKind.DayActivityRounds, Target = 1, Param = (int)ActivityId.Grooming });
                day = inst.Daily.Count - 1;
            }
            var activity = (ActivityId)inst.Daily[day].Param;
            Assert.IsTrue(activity == ActivityId.HideAndSeek || activity == ActivityId.Grooming, "Drawn among the playable activities: " + activity);
            Assert.AreEqual(activity, rig.Events.QuestActivityOf(inst.Daily[day]));
            rig.Events.OnActivityEnded(activity == ActivityId.Grooming ? ActivityId.HideAndSeek : ActivityId.Grooming, 3);
            Assert.AreEqual(0, inst.Daily[day].Progress, "Another activity does not count.");
            rig.Events.OnActivityEnded(activity, 1);
            Assert.IsTrue(inst.Daily[day].Done);
            Assert.AreEqual(ActivityId.HideAndSeek, rig.Events.QuestActivity, "The featured activity names the featured quests.");
            rig.Events.ActivityShipped = a => a != ActivityId.HideAndSeek;
            Assert.AreEqual(rig.Events.Definition.FallbackActivity, (int)rig.Events.QuestActivity, "Its fallback when the player cannot play it.");
        }

        /// <summary>Invokes the event service's own handler of a source's C# event (other subscribers are left alone).</summary>
        private static void Raise(object source, string name, EventService events, params object[] args)
        {
            var field = source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, source.GetType().Name + "." + name);
            var handler = field.GetValue(source) as Delegate;
            Assert.IsNotNull(handler, name + " has no subscriber.");
            int invoked = 0;
            foreach (var d in handler.GetInvocationList())
            {
                if (!ReferenceEquals(d.Target, events))
                    continue;
                var parameters = d.Method.GetParameters();
                var values = new object[parameters.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    var type = parameters[i].ParameterType.IsByRef ? parameters[i].ParameterType.GetElementType() : parameters[i].ParameterType;
                    values[i] = i < args.Length && args[i] != null ? args[i] : type.IsValueType ? Activator.CreateInstance(type) : null;
                }
                d.DynamicInvoke(values);
                invoked++;
            }
            Assert.AreEqual(1, invoked, "The event service listens to " + name + ".");
        }

        [Test]
        public void Quests_EveryGoalCountsFromItsRealHook()
        {
            var builder = new ContentRegistryBuilder();
            EventTestFixture.AddContent(builder, 1);
            var focusEgg = ContentId.FromSource("egg.grassland.focus");
            var grassland = ContentId.FromSource("biome.grassland");
            builder.AddEgg(new EggDefinition(focusEgg, "egg.grassland.focus.name", grassland, EggTier.Focus, new UnityEngine.Color32[EggDefinition.PaletteSize], 15, 0,
                15, "PF_Egg_Base", 10.0, 1.18, new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 },
                new[] { new EggDropEntry(ContentId.FromSource(EventTestFixture.RabbitSpecies), 1.0) }, unlockBiomeMastery: 3, focusTargetCount: 1), "egg.grassland.focus");
            var content = builder.Build();
            var entry = EventTestFixture.Entry();
            var world = ExpeditionRig.Create(now: entry.StartUtc.AddHours(2));
            var state = world.State;
            var events = new EventService(state, content, world.Clock, null, new EventConfig(), EventTestFixture.Calendar(entry))
            {
                Economy = world.Economy, NewestBiome = () => grassland,
            };
            var garden = new GardenService(world.A);
            var offline = new OfflineService(state, world.Economy, world.Clock);
            var cosmetics = new CosmeticService(state, world.Registry, null);
            events.AttachSources(new EventQuestSources
            {
                Hatch = world.Hatch, Wishes = world.Daily.Wishes, Runner = world.A.Runner, Expeditions = world.E, Garden = garden, DailyGift = world.Daily.Gift,
                Bond = world.Daily.Bond, Offline = offline, Cosmetics = cosmetics, Upgrades = world.P.Upgrades, WeeklyJournal = world.Daily.Journal,
            });
            Assert.IsTrue(events.Join());
            var inst = events.Instance;
            inst.Daily.Clear();
            inst.Long.Clear();
            int variants = state.Collection.DiscoveredVariants.Count;
            foreach (EventGoalKind kind in Enum.GetValues(typeof(EventGoalKind)))
                inst.Daily.Add(new EventQuestRecord
                {
                    Template = 1, Group = 0, Goal = (byte)kind, Target = 100_000, Day = 1,
                    Param = kind == EventGoalKind.DayActivityRounds ? (int)ActivityId.Grooming : kind == EventGoalKind.NewVariants ? variants : 0,
                });
            int P(EventGoalKind kind)
            {
                foreach (var q in inst.Daily)
                    if (q.Goal == (byte)kind)
                        return q.Progress;
                return -1;
            }

            object hatched = default(HatchResult);
            typeof(HatchResult).GetField("Egg").SetValue(hatched, focusEgg);
            typeof(HatchResult).GetField("NewVariant").SetValue(hatched, true);
            typeof(HatchResult).GetField("Animal").SetValue(hatched, new AnimalInstance { Species = ContentId.FromSource(EventTestFixture.RabbitSpecies) });
            state.Collection.DiscoveredVariants.Add(ContentId.FromSource("variant.testrabbit.hook"));
            Raise(world.Hatch, "Hatched", events, hatched);
            Assert.AreEqual(1, P(EventGoalKind.HatchEggs));
            Assert.AreEqual(1, P(EventGoalKind.FocusHatches));
            Assert.AreEqual(1, P(EventGoalKind.NewestBiomeHatches));
            Assert.AreEqual(1, P(EventGoalKind.NewVariants));

            Raise(world.Hatch, "CameraBiomeChanged", events, grassland);
            Raise(world.Hatch, "CameraBiomeChanged", events, grassland);
            Raise(world.Hatch, "CameraBiomeChanged", events, ContentId.FromSource("biome.forest"));
            Assert.AreEqual(2, P(EventGoalKind.BiomeVisits), "Different biomes count once each.");

            Raise(world.Daily.Wishes, "Claimed", events);
            Assert.AreEqual(1, P(EventGoalKind.Wishes));

            Raise(world.A.Runner, "Ended", events, new ActivityGameResult
            {
                Activity = ActivityId.Grooming, Mode = ActivityMode.Free, Stars = 3, Rewarded = true, ChallengeImproved = true, ChallengeStarsBefore = 1,
            });
            // Free games, not challenges (the runner writes -1; 0 would be the featured activity's first challenge).
            Raise(world.A.Runner, "Ended", events, new ActivityGameResult { Activity = ActivityId.HideAndSeek, Mode = ActivityMode.Free, Stars = 2, Rewarded = true, Challenge = -1 });
            Raise(world.A.Runner, "Ended", events, new ActivityGameResult { Activity = ActivityId.HideAndSeek, Mode = ActivityMode.Practice, Stars = 3, Challenge = -1 });
            Assert.AreEqual(2, P(EventGoalKind.ActivityRounds), "Practice never counts.");
            Assert.AreEqual(5, P(EventGoalKind.ActivityStars));
            Assert.AreEqual(1, P(EventGoalKind.ThreeStarRounds));
            Assert.AreEqual(1, P(EventGoalKind.GroomingRounds));
            Assert.AreEqual(2, P(EventGoalKind.DistinctActivities));
            Assert.AreEqual(1, P(EventGoalKind.DayActivityRounds));
            Assert.AreEqual(1, P(EventGoalKind.FeaturedRounds));
            Assert.AreEqual(1, P(EventGoalKind.FeaturedTwoStarRounds));
            Assert.AreEqual(2, P(EventGoalKind.ChallengeStars));

            Raise(world.E, "Started", events, new ExpeditionStartInfo { Duration = ExpeditionDuration.FourHours, Challenge = 0, ChallengeStars = 2 });
            Raise(world.E, "Welcomed", events);
            Assert.AreEqual(1, P(EventGoalKind.ExpeditionStarts));
            Assert.AreEqual(1, P(EventGoalKind.LongExpeditionStarts));
            Assert.AreEqual(1, P(EventGoalKind.Expeditions));
            Assert.AreEqual(4, P(EventGoalKind.ChallengeStars));

            Raise(garden, "Planted", events);
            Raise(garden, "SproutWatered", events, 0);
            Raise(garden, "Harvested", events);
            Assert.AreEqual(1, P(EventGoalKind.GardenPlants));
            Assert.AreEqual(1, P(EventGoalKind.GardenWaterings));
            Assert.AreEqual(1, P(EventGoalKind.GardenHarvests));

            Raise(world.Daily.Gift, "Opened", events);
            Raise(world.Daily.Bond, "Touched", events, 1u, true);
            Raise(world.Daily.Bond, "Touched", events, 1u, false);
            Raise(world.Daily.Bond, "TreatGiven", events, 1u);
            Raise(offline, "LightCollected", events);
            Raise(cosmetics, "Equipped", events, 1u);
            Raise(world.P.Upgrades, "Purchased", events);
            Raise(world.Daily.Journal, "GoalReached", events, 0);
            Assert.AreEqual(1, P(EventGoalKind.DailyGift));
            Assert.AreEqual(1, P(EventGoalKind.BondTouches), "Only counted touches.");
            Assert.AreEqual(1, P(EventGoalKind.TreatsGiven));
            Assert.AreEqual(1, P(EventGoalKind.LightCollections));
            Assert.AreEqual(1, P(EventGoalKind.AccessoriesEquipped));
            Assert.AreEqual(1, P(EventGoalKind.UpgradesBought));
            Assert.AreEqual(1, P(EventGoalKind.WeeklyJournalGoals));

            inst.Currency += 1000;
            Assert.AreEqual(EventEggStatus.Opened, events.OpenEgg().Status);
            Assert.AreEqual(1, P(EventGoalKind.OpenEventEggs));
            Assert.AreEqual(1, P(EventGoalKind.EventEggItems), "The first egg always gives a new item.");
            Assert.Greater(P(EventGoalKind.EarnCurrency), 0);
            inst.Points = events.PointsForTier(3) - 1;
            events.GrantEventCurrency(1, ActivityId.Grooming);
            Assert.GreaterOrEqual(inst.TierReached, 3);
            Assert.AreEqual(inst.TierReached, P(EventGoalKind.TrackTier));
            foreach (var locked in new[] { EventGoalKind.DecorationsPlaced, EventGoalKind.PhotosTaken, EventGoalKind.BreedingsStarted })
                Assert.AreEqual(0, P(locked), locked + ": no hook until the feature exists.");
            events.Dispose();
        }

        [Test]
        public void Challenges_30Stars_Outfit3_StickersAt9_18_27()
        {
            var rig = new Rig(albumOpen: false);
            rig.At(2);
            rig.Events.Join();
            int start = rig.Inst.Currency;
            for (int i = 0; i < 10; i++)
                rig.Events.ReportChallenge(i, 3);
            Assert.AreEqual(30, rig.Events.TotalChallengeStars);
            Assert.GreaterOrEqual(rig.Inst.Currency - start, 750);
            Assert.IsTrue(rig.Owns(EventTestFixture.Outfit(3)));
            Assert.AreNotEqual(0, rig.Inst.StickersOwed, "The album is closed: the stickers wait.");
            rig.Albums.Unlocked = true;
            rig.At(2.1);
            rig.Events.Tick();
            Assert.AreEqual(0, rig.Inst.StickersOwed);
            Assert.IsTrue(rig.Albums.Owned(rig.StickerOnPage(6)) && rig.Albums.Owned(rig.StickerOnPage(7)) && rig.Albums.Owned(rig.StickerOnPage(8)));
            int currency = rig.Inst.Currency;
            rig.Events.ReportChallenge(4, 3);
            Assert.AreEqual(currency, rig.Inst.Currency, "25 per star once.");
        }

        [Test]
        public void End_ConvertsLeftoverToLightOnce_RewindReopensNothing()
        {
            var rig = new Rig();
            rig.At(3);
            rig.Events.GrantEventCurrency(100, ActivityId.Grooming);
            rig.Inst.Currency = 100;
            string id = rig.Inst.InstanceId;
            double rate = rig.Economy.GetLightPerSecond().ToDoubleClamped();
            Assert.Greater(rate, 0.0);
            double light = rig.State.Wallet.Light.ToDoubleClamped();
            rig.Clock.Set(rig.Entry.EndUtc.AddHours(2));
            rig.Events.Tick();
            var inst = rig.State.Events.Find(id);
            Assert.IsTrue(inst.Converted && inst.Ended && inst.Currency == 0);
            Assert.AreEqual(100 * 10 * rate, rig.State.Wallet.Light.ToDoubleClamped() - light, rate * 1e-3 + 1e-6, "1 unit = 10 s of production.");
            Assert.IsTrue(rig.Events.TryGetPendingSummary(out var summary));
            Assert.AreEqual(100, summary.CurrencyLeft);
            rig.Events.MarkSummarySeen(id);
            Assert.IsFalse(rig.Events.TryGetPendingSummary(out _));
            double after = rig.State.Wallet.Light.ToDoubleClamped();
            rig.Events.Tick();
            rig.Clock.Set(rig.Entry.StartUtc.AddDays(5));
            rig.Events.Tick();
            Assert.AreEqual(EventPhase.None, rig.Events.Phase, "Rewinding the clock reopens nothing.");
            rig.Events.GrantEventCurrency(50, ActivityId.HideAndSeek);
            Assert.AreEqual(after, rig.State.Wallet.Light.ToDoubleClamped(), "Converted exactly once.");
            Assert.AreEqual(0, inst.Currency);
        }

        [Test]
        public void End_UnclaimedRewardsAreGiven_NeverLost()
        {
            var rig = new Rig();
            rig.At(4);
            rig.Events.GrantEventCurrency(600, ActivityId.HideAndSeek);
            long gems = rig.State.Wallet.Gems;
            rig.Clock.Set(rig.Entry.EndUtc.AddMinutes(1));
            rig.Events.Tick();
            Assert.AreEqual(gems + 25, rig.State.Wallet.Gems, "T2's 25 Gems given at the end.");
            Assert.IsTrue(rig.Owns(EventTestFixture.Outfit(1)));
        }

        [Test]
        public void MemoryShop_ReturningEdition_PricesAndAlreadyYours()
        {
            var rig = new Rig(version: 2, year: 2027);
            rig.State.Events.Instances.Add(new EventInstanceState { InstanceId = "event.test.2026", Key = "event.test", Year = 2026, Version = 1, Ended = true, Converted = true, SummarySeen = true });
            rig.At(1);
            Assert.IsTrue(rig.Events.MemoryShopOpen);
            var prices = new Dictionary<string, int>();
            for (int i = 0; i < rig.Events.MemoryCount; i++)
                prices[rig.Events.MemoryItem(i).Key] = rig.Events.MemoryPrice(i);
            Assert.AreEqual(1500, prices["memory.variant_1"]);
            Assert.AreEqual(1150, prices["memory.outfit_1"]);
            Assert.AreEqual(650, prices["memory.deco_1"]);
            Assert.AreEqual(850, prices["memory.deco_7"]);
            Assert.AreEqual(1000, prices["memory.deco_8"]);
            Assert.AreEqual(750, prices["memory.frame"]);
            Assert.AreEqual(1000, prices["memory.pose"]);
            Assert.AreEqual(36, rig.Events.TierCount, "Returning-year bonus tiers.");
            rig.State.Entitlements.Owned.Add(ContentId.FromSource(EventTestFixture.Deco(1)));
            rig.Events.Join();
            rig.Inst.Currency = 5000;
            Assert.IsTrue(rig.Events.MemoryOwned(5));
            Assert.AreEqual(EventBuyStatus.Owned, rig.Events.BuyMemory(5), "« Déjà à toi ».");
            Assert.AreEqual(EventBuyStatus.Bought, rig.Events.BuyMemory(0));
            Assert.AreEqual(3500, rig.Inst.Currency);
            Assert.IsTrue(rig.State.Collection.DiscoveredVariants.Contains(ContentId.FromSource(EventTestFixture.Variant(1))));
            var first = new Rig();
            first.At(1);
            Assert.IsFalse(first.Events.MemoryShopOpen, "First editions have no Memory Shop.");
        }

        [Test]
        public void Calendar_RefusesOverlaps_AndEmptyByDefault()
        {
            Assert.AreEqual(0, new EventCalendar().Entries.Count);
            Assert.AreEqual(new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.Zero), EventSchedule.SecondFriday(2026, 10));
            var calendar = new EventCalendar();
            var a = EventTestFixture.Entry(2026);
            var overlap = new EventCalendarEntry("event.other", 2026, 1, a.StartUnix + 86_400, a.EndUnix + 86_400);
            Assert.IsFalse(calendar.TrySet(new[] { a, overlap }));
            var next = EventSchedule.Entry("event.other", 2026, 12, 1);
            Assert.IsTrue(calendar.TrySet(new[] { next, a }));
            Assert.AreEqual(a.Key, calendar.Entries[0].Key);
            var tooMany = new EventCalendarEntry[41];
            for (int i = 0; i < tooMany.Length; i++)
                tooMany[i] = EventSchedule.Entry("event.k" + i, 2030 + i / 12, 1 + i % 12, 1);
            Assert.IsFalse(calendar.TrySet(tooMany));
            // An entry whose content is missing is ignored by the service.
            var rig = new Rig(more: EventSchedule.Entry("event.missing", 2026, 12, 1));
            rig.Clock.Set(EventSchedule.SecondFriday(2026, 12).AddHours(1));
            Assert.AreEqual(EventPhase.None, rig.Events.Phase);
        }

        [Test]
        public void Content_RefusesAPaidLineGivingLight()
        {
            var builder = new ContentRegistryBuilder();
            var good = EventTestFixture.Build();
            var tiers = (EventTier[])good.Tiers.Clone();
            tiers[0] = new EventTier(1, tiers[0].Free, new[] { EventReward.LightMinutes(60) });
            var bad = new EventDefinition(good.Key, 1, good.NameKey, good.CurrencyKey, good.FeaturedSpecies, good.FeaturedActivities, good.FallbackActivity,
                good.AlbumEventNumber, tiers, good.BonusTiers, good.EggItems, good.SecretVariant, good.Shop, good.Memory, good.DailyQuests, good.LongQuests,
                good.ChallengeLevels, good.ChallengeOutfit, good.Souvenir, good.BoxItems, good.Headline, good.ThemeKeys);
            builder.AddEvent(bad, good.Key);
            Assert.IsTrue(builder.HasErrors);
            Assert.AreEqual(1, EventTestFixture.BuildContent().Events.Count);
        }

        [Test]
        public void Save_RoundTrip_AndMigration17To18_UnknownEventKept()
        {
            var rig = new Rig();
            rig.At(1);
            rig.Events.GrantEventCurrency(1500, ActivityId.HideAndSeek);
            rig.Events.ClaimAll();
            rig.Inst.Currency += 900;
            rig.Events.OpenEgg();
            rig.Events.Buy(Line(rig, "shop.tickets"));
            rig.Events.ReportChallenge(2, 2);
            rig.Inst.Frozen[EventParam.EggCost] = 280;
            rig.State.Events.Instances.Add(new EventInstanceState { InstanceId = "event.unknown.2026", Key = "event.unknown", Year = 2026, Version = 1,
                StartUnix = rig.Entry.StartUnix, EndUnix = rig.Entry.EndUnix, Currency = 70 });
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(rig.State, writer);
            var bytes = writer.WrittenSpan.ToArray();
            var loaded = serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
            var a = rig.Inst;
            var b = loaded.Events.Find(a.InstanceId);
            Assert.IsNotNull(b);
            Assert.AreEqual(a.Currency, b.Currency);
            Assert.AreEqual(a.Points, b.Points);
            Assert.AreEqual(a.TierReached, b.TierReached);
            Assert.AreEqual(a.FreeClaimed, b.FreeClaimed);
            Assert.AreEqual(a.EggRng, b.EggRng);
            Assert.AreEqual(a.EggItems, b.EggItems);
            Assert.AreEqual(a.EggsOpened, b.EggsOpened);
            Assert.AreEqual(280, b.Frozen.Int(EventParam.EggCost));
            Assert.AreEqual(a.Purchases.Count, b.Purchases.Count);
            Assert.AreEqual(a.Daily.Count, b.Daily.Count);
            Assert.AreEqual(a.Long.Count, b.Long.Count);
            Assert.AreEqual(a.Long[3].Target, b.Long[3].Target);
            Assert.AreEqual(2, b.ChallengeStars[2]);
            Assert.AreEqual(a.SouvenirUnixDay, b.SouvenirUnixDay);
            Assert.AreEqual(rig.State.Events.LastSeenUnix, loaded.Events.LastSeenUnix);
            Assert.AreEqual(70, loaded.Events.Find("event.unknown.2026").Currency, "An unknown event's instance is kept.");
            // The unknown event still converts at its end; the service ignores it otherwise.
            var reloaded = new Rig(state: loaded);
            reloaded.Clock.Set(rig.Entry.EndUtc.AddHours(1));
            reloaded.Events.Tick();
            Assert.IsTrue(loaded.Events.Find("event.unknown.2026").Converted);

            var document = serializer.ReadDocument(bytes, bytes.Length);
            document.RemoveSection(SaveSectionId.Events);
            document.SchemaVersion = 17;
            SaveMigrations.CreatePipeline().Migrate(document);
            Assert.AreEqual(18, document.SchemaVersion);
            var migrated = serializer.Deserialize(document);
            Assert.AreEqual(0, migrated.Events.Instances.Count);
        }

        [Test]
        public void PacingSimulator_HoldsTheTargets()
        {
            var runs = EventPacingSimulator.RunAll();
            var regular = runs[0];
            var casual = runs[1];
            TestContext.WriteLine(EventPacingSimulator.Report(runs));
            Assert.IsTrue(regular.GuaranteesHeld && casual.GuaranteesHeld, string.Join("; ", regular.Problems) + string.Join("; ", casual.Problems));
            Assert.That(regular.DayOfTier(10), Is.InRange(2, 4), "Regular: tier 10 on day 3.");
            Assert.That(regular.DayOfTier(30), Is.InRange(10, 12), "Regular: tier 30 around day 11.");
            Assert.That(regular.EggsOpened, Is.InRange(17, 26), "Regular: about 21 eggs.");
            Assert.That(regular.CurrencyEarned, Is.InRange(7_800, 10_500), "Regular: about 9,000.");
            Assert.AreEqual(7, regular.ItemsOwned);
            Assert.That(casual.DayOfTier(10), Is.InRange(5, 7), "Casual: tier 10 around day 6.");
            Assert.That(casual.EggsOpened, Is.InRange(8, 10), "Casual: 8 to 10 eggs.");
            Assert.That(casual.ItemsOwned, Is.InRange(3, 6), "Casual: about half the items.");
            Assert.That(casual.CurrencyEarned, Is.InRange(2_900, 4_200), "Casual: about 3,500.");
            Assert.Less(regular.Milliseconds + casual.Milliseconds, 10_000.0);
        }
    }
}
