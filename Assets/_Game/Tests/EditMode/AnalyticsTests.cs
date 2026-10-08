using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Analytics;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Save;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using UnityEditor;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T29 analytics: stable wire ids, light compact batches, onboarding/economy/monetization/performance events
    /// from the real services, milestones once per profile, consent, bounded outbox, no per-frame allocation.
    /// </summary>
    public class AnalyticsTests
    {
        private static ContentRegistry Shipped() =>
            AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset").CreateBuilder().Build();

        private static AnalyticsDeviceInfo Device() => new AnalyticsDeviceInfo
        {
            AnalyticsId = "0123456789abcdef0123456789abcdef",
            AppVersion = "0.1.0",
            BuildType = "development",
            Platform = "Android",
            DeviceModel = "Test \"Phone\"",
            OperatingSystem = "Android 14",
            SystemMemoryMb = 4096,
        };

        /// <summary>Keeps every batch as text (tests only; the game uses the disk outbox).</summary>
        private sealed class MemorySink : IAnalyticsSink
        {
            public readonly List<string> Batches = new List<string>();
            public bool SendAllowed { get; set; }
            public int Purges;
            public bool Keep = true;

            public void Write(byte[] data, int length)
            {
                if (Keep)
                    Batches.Add(Encoding.UTF8.GetString(data, 0, length));
            }

            public void Purge()
            {
                Purges++;
                Batches.Clear();
            }

            public bool WaitIdle(TimeSpan timeout) => true;

            public void Dispose()
            {
            }

            public string All => string.Join("\n", Batches);
        }

        private sealed class FakeSaves : ISaveWrites, ISaveScheduler
        {
            public long SerializedCount { get; private set; }
            public int Requests;
            public event Action<long> Written;
            public void RequestSave(SaveUrgency urgency) => Requests++;

            public void Write()
            {
                SerializedCount++;
                Written?.Invoke(SerializedCount);
            }
        }

        private sealed class Rig : IDisposable
        {
            public readonly ContentRegistry Content;
            public readonly PlayerState State;
            public readonly FakeGameClock Clock = new FakeGameClock(TestUtil.T0);
            public readonly FakeSaves Saves = new FakeSaves();
            public readonly MemorySink Sink = new MemorySink();
            public readonly EconomyService Economy;
            public readonly HatchService Hatch;
            public readonly OfferService Offers;
            public readonly StoreService Store;
            public readonly AnalyticsService Analytics;

            public Rig(AnalyticsConsent consent = AnalyticsConsent.Granted, AnalyticsConfig config = null, PlayerState state = null)
            {
                Content = Shipped();
                State = state ?? PlayerState.CreateNew(TestUtil.T0);
                Economy = new EconomyService(State, Content, EconomyConfig.Default, Saves);
                Hatch = new HatchService(State, Content, Economy, Clock, Saves);
                var grants = new GrantService(Content, State, Saves);
                Offers = new OfferService(Content, State, grants, Saves);
                Store = new StoreService(Content, Offers, new SandboxStoreProvider("en", 0), Clock, Saves);
                Store.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
                Analytics = new AnalyticsService(State, Content, Clock, Saves, Device(), Sink, consent, config);
                Analytics.Attach(Economy, Hatch, Store);
            }

            public ContentId Egg => Content.Eggs[0].Id;

            public HatchResult HatchOne()
            {
                State.Wallet.Light = BigValue.FromMantissaExponent(1.0, 30);
                var result = Hatch.Hatch(Egg);
                Assert.IsTrue(result.Succeeded, result.Status.ToString());
                return result;
            }

            public void Dispose()
            {
                Analytics.Dispose();
                Store.Dispose();
            }
        }

        private static int Count(string text, string part)
        {
            int count = 0;
            for (int i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
                count++;
            return count;
        }

        /// <summary>Fields of the first event with this id: [id, time, session, ...].</summary>
        private static string[] Fields(string text, AnalyticsEventId id)
        {
            int start = text.IndexOf("[" + (int)id + ",", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, id + " not found");
            int end = text.IndexOf(']', start);
            return text.Substring(start + 1, end - start - 1).Split(',');
        }

        /// <summary>Events of one id in a batch text: each event starts with "[id,".</summary>
        private static int Events(string text, AnalyticsEventId id) => Count(text, "[" + (int)id + ",");

        // ---------------- Criterion: stable ids ----------------

        [Test]
        public void WireIds_AreStable()
        {
            // Values already sent can never change: renumbering breaks every dashboard and older data. Aligned on the
            // catalogue of doc 08 and doc 09 in T37, before any data was sent.
            var expected = new (Type type, string name, int value)[]
            {
                (typeof(AnalyticsEventId), "SessionStart", 1), (typeof(AnalyticsEventId), "SessionEnd", 2),
                (typeof(AnalyticsEventId), "LoadTime", 3), (typeof(AnalyticsEventId), "Milestone", 10),
                (typeof(AnalyticsEventId), "Hatch", 20), (typeof(AnalyticsEventId), "Economy", 30),
                (typeof(AnalyticsEventId), "EconomySnapshot", 31), (typeof(AnalyticsEventId), "EconomyFlow", 32),
                (typeof(AnalyticsEventId), "ScreenView", 40), (typeof(AnalyticsEventId), "OfferImpression", 41),
                (typeof(AnalyticsEventId), "OfferClick", 42), (typeof(AnalyticsEventId), "PurchaseResult", 43),
                (typeof(AnalyticsEventId), "Restore", 44), (typeof(AnalyticsEventId), "PerfSample", 50),
                (typeof(AnalyticsEventId), "OfflineReturn", 60), (typeof(AnalyticsEventId), "ReturnGift", 61),
                (typeof(AnalyticsEventId), "UpgradeBuy", 70), (typeof(AnalyticsEventId), "UpgradeMilestone", 71),
                (typeof(AnalyticsEventId), "ZoneOpen", 72), (typeof(AnalyticsEventId), "BiomeUnlock", 73),
                (typeof(AnalyticsEventId), "BiomeGateSeen", 74), (typeof(AnalyticsEventId), "RefugeLevelUp", 75),
                (typeof(AnalyticsEventId), "BiomeMasteryUp", 76), (typeof(AnalyticsEventId), "DecorationBuyLight", 77),
                (typeof(AnalyticsEventId), "TreatBuy", 78), (typeof(AnalyticsEventId), "TreatGive", 79),
                (typeof(AnalyticsEventId), "Renewal", 80), (typeof(AnalyticsEventId), "EssenceNodeBuy", 81),
                (typeof(AnalyticsEventId), "EggBuy", 90), (typeof(AnalyticsEventId), "MultiHatch", 91),
                (typeof(AnalyticsEventId), "DiscoveryEggGain", 92), (typeof(AnalyticsEventId), "EggOddsOpen", 93),
                (typeof(AnalyticsEventId), "ActivityStart", 110), (typeof(AnalyticsEventId), "ActivityEnd", 111),
                (typeof(AnalyticsEventId), "ActivityChallenge", 112), (typeof(AnalyticsEventId), "TicketsEmpty", 113),
                (typeof(AnalyticsEventId), "TicketsGain", 114),
                (typeof(AnalyticsEventId), "TodayOpen", 120), (typeof(AnalyticsEventId), "DailyGift", 121),
                (typeof(AnalyticsEventId), "WishDone", 122), (typeof(AnalyticsEventId), "WishReroll", 123),
                (typeof(AnalyticsEventId), "CalendarDay", 124), (typeof(AnalyticsEventId), "WishDrawn", 125),
                (typeof(AnalyticsEventId), "WelcomeGift", 126), (typeof(AnalyticsEventId), "DailyVisitDone", 127),
                (typeof(AnalyticsEventId), "JournalGoal", 130), (typeof(AnalyticsEventId), "JournalComplete", 131),
                (typeof(EconomyReason), "Activity", 16), (typeof(EconomyReason), "TicketPack", 37),
                (typeof(MasteryReason), "Activity", 5),
                (typeof(ScreenId), "Today", 26), (typeof(ScreenId), "WeeklyJournal", 27),
                (typeof(ScreenId), "ActivityHub", 24), (typeof(ScreenId), "ActivityResult", 25),
                (typeof(EconomyReason), "Wish", 12), (typeof(EconomyReason), "DailyGift", 13), (typeof(EconomyReason), "Calendar", 14),
                (typeof(EconomyReason), "WeeklyJournal", 15), (typeof(EconomyReason), "WelcomeGift", 32),
                (typeof(EconomyReason), "ReturnGift", 47), (typeof(EconomyReason), "Bond", 58),
                (typeof(EconomyReason), "ActivityChallenge", 17), (typeof(EconomyReason), "EssenceNode", 51),
                (typeof(FunnelMilestone), "FirstWish", 13), (typeof(FunnelMilestone), "FirstActivity", 14),
                (typeof(MasteryReason), "Wish", 4),
                (typeof(WishSystem), "Hatching", 1), (typeof(WishSystem), "Expansions", 6), (typeof(WishSystem), "GrandContest", 28),
                (typeof(WishSystem), "Rendezvous", 29), (typeof(WishFamily), "Collection", 0), (typeof(WishFamily), "Travels", 4),
                (typeof(BondSource), "Touch", 0), (typeof(BondSource), "Treat", 1), (typeof(BondSource), "Wish", 2),
                (typeof(BondSource), "Activity", 3),
                (typeof(DiscoverySource), "Debug", 0), (typeof(DiscoverySource), "Calendar", 1), (typeof(DiscoverySource), "WeeklyBook", 2),
                (typeof(DiscoverySource), "Expedition", 3), (typeof(DiscoverySource), "EventTrack", 4), (typeof(DiscoverySource), "DailyGift", 5),
                (typeof(DiscoverySource), "BiomeOpening", 6), (typeof(DiscoverySource), "Pass", 7), (typeof(DiscoverySource), "ReturnGift", 8),
                (typeof(DiscoveryRule), "None", 0), (typeof(DiscoveryRule), "NewSpecies", 1), (typeof(DiscoveryRule), "NewVariant", 2),
                (typeof(DiscoveryRule), "RareOrBetter", 3),
                (typeof(EconomyReason), "Upgrade", 4), (typeof(EconomyReason), "Expansion", 5),
                (typeof(EconomyReason), "Decoration", 54), (typeof(EconomyReason), "Treat", 55), (typeof(EconomyReason), "BiomeGate", 43),
                (typeof(UpgradeKind), "Production", 0), (typeof(UpgradeKind), "Nests", 1), (typeof(UpgradeKind), "Curiosity", 2),
                (typeof(PurchaseQty), "One", 0), (typeof(PurchaseQty), "Ten", 1), (typeof(PurchaseQty), "Max", 2),
                (typeof(FunnelMilestone), "TutorialStart", 0), (typeof(FunnelMilestone), "FirstHatch", 1),
                (typeof(FunnelMilestone), "FirstCollect", 2), (typeof(FunnelMilestone), "FirstUpgrade", 3),
                (typeof(FunnelMilestone), "FirstNewSpecies", 4), (typeof(FunnelMilestone), "FirstRare", 5),
                (typeof(FunnelMilestone), "BiomeUnlock", 6), (typeof(FunnelMilestone), "CodexOpen", 7),
                (typeof(FunnelMilestone), "FirstShopExposure", 8), (typeof(FunnelMilestone), "FirstRewardedAd", 9),
                (typeof(FunnelMilestone), "FirstOfferImpression", 10), (typeof(FunnelMilestone), "FirstPurchase", 11),
                (typeof(FunnelMilestone), "SecondPurchase", 12),
                (typeof(ScreenId), "Refuge", 1), (typeof(ScreenId), "Codex", 2), (typeof(ScreenId), "CodexSheet", 3),
                (typeof(ScreenId), "Shop", 4), (typeof(ScreenId), "OfferModal", 5), (typeof(ScreenId), "OfflineReturn", 6),
                (typeof(CurrencyId), "Light", 1), (typeof(CurrencyId), "Essence", 2), (typeof(CurrencyId), "Gems", 3),
                (typeof(EconomyReason), "EggPurchase", 3), (typeof(EconomyReason), "Purchase", 7), (typeof(EconomyReason), "Debug", 11),
                (typeof(PurchaseFlowStatus), "Granted", 0), (typeof(PurchaseFlowStatus), "Cancelled", 1),
                (typeof(PurchaseFlowStatus), "Failed", 2), (typeof(PurchaseFlowStatus), "Pending", 3),
                (typeof(PurchaseFlowStatus), "NotEnoughGems", 5), (typeof(PurchaseFlowStatus), "Busy", 7),
                (typeof(AnalyticsConsent), "Unknown", 0), (typeof(AnalyticsConsent), "Granted", 1), (typeof(AnalyticsConsent), "Denied", 2),
            };
            foreach (var (type, name, value) in expected)
                Assert.AreEqual(value, Convert.ToInt32(Enum.Parse(type, name)), type.Name + "." + name);
            Assert.AreEqual(1, AnalyticsSchema.Version);
        }

        [Test]
        public void ContentIds_AreUniqueStableHashes_AndExported()
        {
            var content = Shipped();
            var rows = AnalyticsIdTable.Build(content);
            var seen = new Dictionary<uint, string>();
            foreach (var row in rows)
            {
                if (row.Kind is "species" or "variant" or "egg" or "biome" or "product" or "offer" or "entitlement")
                {
                    Assert.AreNotEqual("?", row.Name);
                    Assert.AreEqual(ContentIdHash.Compute(row.Name), row.Value, "Content ids travel as the hash of their source id.");
                    Assert.IsFalse(seen.TryGetValue(row.Value, out var other), $"Hash collision: {row.Name} and {other}");
                    seen[row.Value] = row.Name;
                }
            }
            Assert.Greater(seen.Count, 20);
            Assert.AreEqual(ContentId.FromSource("species.rabbit").Value, ContentIdHash.Compute("species.rabbit"));
        }

        [Test]
        public void Schema_EveryEventHasAValidLayout()
        {
            foreach (AnalyticsEventId id in Enum.GetValues(typeof(AnalyticsEventId)))
            {
                var layout = AnalyticsSchema.Layout(id);
                Assert.Greater(layout.Length, 0, id.ToString());
                var used = new HashSet<string>();
                foreach (var field in layout)
                {
                    int max = field.Kind == 'I' ? 4 : 3;
                    Assert.Less(field.Slot, max, id + " slot");
                    Assert.IsTrue(used.Add(field.Kind.ToString() + field.Slot), id + " uses a slot twice");
                    Assert.LessOrEqual(field.Decimals, 6);
                }
            }
        }

        // ---------------- Encoding ----------------

        [Test]
        public void Batch_IsValidCompactJson_WithTheHeaderOnce()
        {
            using (var rig = new Rig())
            {
                rig.Analytics.StartSession(125.4);
                rig.HatchOne();
                rig.Analytics.ScreenView(ScreenId.Codex);
                rig.Analytics.EndSession();
                Assert.AreEqual(1, rig.Sink.Batches.Count);
                string batch = rig.Sink.Batches[0];
                MiniJson.Validate(batch);
                StringAssert.StartsWith("{\"schema\":1,\"aid\":\"0123456789abcdef0123456789abcdef\",\"app\":\"0.1.0\"", batch);
                StringAssert.Contains("\"device\":\"Test \\\"Phone\\\"\"", batch, "Header strings are escaped.");
                Assert.AreEqual(1, Count(batch, "\"schema\""), "Device facts are sent once per batch, never per event.");
                long ms = TestUtil.T0.ToUnixTimeMilliseconds();
                StringAssert.Contains("[1," + ms + ",1,125,0,0,", batch, "Session start: offline seconds, animals, species...");
                StringAssert.Contains("[40," + ms + ",1,2]", batch, "Screen view: id, time, session, screen.");
                Assert.AreEqual(1, Events(batch, AnalyticsEventId.Hatch));
                Assert.AreEqual(1, Events(batch, AnalyticsEventId.SessionEnd));
                Assert.IsFalse(batch.Substring(batch.IndexOf("\"events\"", StringComparison.Ordinal)).Contains(" "), "Compact: no whitespace.");
                Assert.IsFalse(batch.Contains("species."), "Content travels as hashes, never as names.");
            }
        }

        [Test]
        public void Numbers_UseFixedDecimals_TrimmedAndFinite()
        {
            var ring = new AnalyticsEvent[1];
            ring[0] = new AnalyticsEvent(AnalyticsEventId.PerfSample)
            {
                UnixMs = 5, Session = 2, I0 = 3600, D0 = 16.6666, D1 = 33.0, I1 = -4, D2 = double.NaN, U0 = 4000000000u, I2 = 2, I3 = 1,
            };
            var encoder = new AnalyticsBatchEncoder(Device());
            encoder.Encode(7, 0, ring, 0, 1);
            string text = Encoding.UTF8.GetString(encoder.Buffer, 0, encoder.Length);
            StringAssert.Contains("\"batch\":7", text);
            StringAssert.EndsWith("[[50,5,2,3600,16.67,33,-4,0,4000000000,2,1]]}", text);

            ring[0] = new AnalyticsEvent(AnalyticsEventId.Economy) { I0 = 3, I1 = 7, D0 = -0.125, I2 = 0, D1 = 1e22, I3 = 0 };
            encoder.Encode(8, 0, ring, 0, 1);
            text = Encoding.UTF8.GetString(encoder.Buffer, 0, encoder.Length);
            StringAssert.EndsWith("[[30,0,0,3,7,-0.125,0,9000000000000000000,0]]}", text, "Out of range: clamped, never garbage.");
            MiniJson.Validate(text);
        }

        [Test]
        public void Payload_IsLight()
        {
            using (var rig = new Rig(config: new AnalyticsConfig { FlushThreshold = 1000, MaxEventsPerBatch = 200 }))
            {
                rig.Analytics.StartSession(0);
                for (int i = 0; i < 40; i++)
                    rig.HatchOne();
                for (int i = 0; i < 30; i++)
                    rig.Analytics.ScreenView(ScreenId.Codex);
                rig.Analytics.Flush();
                int events = 1 + 40 + 30 + 4; // + milestones
                int bytes = Encoding.UTF8.GetByteCount(rig.Sink.Batches[0]);
                Assert.Less(bytes, 300 + events * 80, $"{bytes} bytes for about {events} events.");
                TestContext.WriteLine($"Batch: {bytes} bytes, {rig.Sink.Batches[0].Length} chars");
            }
        }

        // ---------------- Onboarding funnel ----------------

        [Test]
        public void Milestones_FireOncePerProfile_AndSurviveARestart()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            using (var rig = new Rig(state: state))
            {
                rig.Analytics.StartSession(0);
                rig.Clock.Advance(TimeSpan.FromSeconds(42));
                rig.HatchOne();
                rig.HatchOne();
                rig.Analytics.ScreenView(ScreenId.Codex);
                rig.Analytics.ScreenView(ScreenId.Codex);
                rig.Analytics.EndSession();
                string text = rig.Sink.All;
                Assert.IsTrue(state.Analytics.Has(FunnelMilestone.TutorialStart));
                Assert.IsTrue(state.Analytics.Has(FunnelMilestone.FirstHatch));
                Assert.IsTrue(state.Analytics.Has(FunnelMilestone.CodexOpen));
                StringAssert.Contains(",1,42]", text, "First hatch, 42 s after install.");
                int milestones = Events(text, AnalyticsEventId.Milestone);
                Assert.AreEqual(Count(Convert.ToString((long)state.Analytics.Milestones, 2), "1"), milestones, "One event per reached milestone.");
            }
            using (var rig = new Rig(state: state))
            {
                rig.Analytics.StartSession(0);
                rig.Analytics.ScreenView(ScreenId.Codex);
                rig.Analytics.EndSession();
                Assert.AreEqual(0, Events(rig.Sink.All, AnalyticsEventId.Milestone), "Nothing fires twice for the same profile.");
                Assert.AreEqual(2, state.Analytics.Sessions);
                StringAssert.Contains(",2,", rig.Sink.All, "Session number follows the profile.");
            }
        }

        [Test]
        public void SessionStart_ForAVeteran_IsNotATutorialStart()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            state.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit"), Seed = 1 });
            using (var rig = new Rig(state: state))
            {
                rig.Analytics.StartSession(3600);
                rig.Analytics.EndSession();
                Assert.IsFalse(state.Analytics.Has(FunnelMilestone.TutorialStart));
                Assert.AreEqual(0, Events(rig.Sink.All, AnalyticsEventId.Milestone));
            }
        }

        // ---------------- Economy ----------------

        [Test]
        public void Economy_LightIsSummedPerReason_GemsAreSentOneByOne()
        {
            using (var rig = new Rig())
            {
                rig.Analytics.StartSession(0);
                for (int i = 0; i < 25; i++)
                    rig.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(100), EconomyReason.Reward);
                rig.Economy.Spend(CurrencyId.Light, BigValue.FromDouble(500), EconomyReason.Upgrade);
                rig.Economy.Grant(CurrencyId.Gems, BigValue.FromDouble(50), EconomyReason.Reward);
                rig.Analytics.Flush();
                Assert.AreEqual(0, Events(rig.Sink.All, AnalyticsEventId.EconomyFlow), "Light flows wait for the session end.");
                Assert.AreEqual(1, Events(rig.Sink.All, AnalyticsEventId.Economy), "Gems: one event per change.");
                Assert.IsTrue(rig.State.Analytics.Has(FunnelMilestone.FirstUpgrade));

                rig.Analytics.EndSession();
                string text = rig.Sink.All;
                Assert.AreEqual(2, Events(text, AnalyticsEventId.EconomyFlow), "Reward and Upgrade, one summary each.");
                StringAssert.Contains(",1,6,25,2.5,3]", text, "Light, Reward, 25 changes, 2.5e3.");
                StringAssert.Contains(",1,4,1,-5,2]", text, "Light, Upgrade, 1 change, -5e2.");
                Assert.AreEqual(1, Events(text, AnalyticsEventId.EconomySnapshot));
            }
        }

        [Test]
        public void Hatch_CarriesEggSpeciesVariantRarityAndCost()
        {
            using (var rig = new Rig())
            {
                rig.Analytics.StartSession(0);
                var result = rig.HatchOne();
                rig.Analytics.Flush();
                string expected = "[20," + TestUtil.T0.ToUnixTimeMilliseconds() + ",1," + result.Egg.Value + "," + result.Animal.Species.Value + ","
                                  + result.Animal.Variant.Value + "," + (int)result.Animal.Rarity + ",";
                StringAssert.Contains(expected, rig.Sink.All);
            }
        }

        [Test]
        public void Eggs_PurchasesMultiHatchesDiscoveryEggsAndOdds_AreSent()
        {
            using (var rig = new Rig())
            {
                rig.Analytics.StartSession(0);
                rig.HatchOne();
                rig.State.Wallet.Light = BigValue.FromMantissaExponent(1.0, 30);
                var results = new HatchResult[3];
                Assert.AreEqual(3, rig.Hatch.Hatch(rig.Egg, 3, results));
                rig.Hatch.GrantDiscoveryEggs(2, DiscoverySource.BiomeOpening);
                rig.Analytics.EggOddsOpened(rig.Egg);
                rig.Analytics.Flush();
                string all = rig.Sink.All;
                Assert.AreEqual(4, Events(all, AnalyticsEventId.Hatch));
                Assert.AreEqual(2, Events(all, AnalyticsEventId.EggBuy), "The single egg, then one purchase for the three.");
                var multi = Fields(all, AnalyticsEventId.MultiHatch);
                Assert.AreEqual(rig.Egg.Value.ToString(), multi[3]);
                Assert.AreEqual("3", multi[4]);
                var gain = Fields(all, AnalyticsEventId.DiscoveryEggGain);
                Assert.AreEqual(((int)DiscoverySource.BiomeOpening).ToString(), gain[3]);
                Assert.AreEqual("2", gain[4]);
                Assert.AreEqual("2", gain[5]);
                Assert.AreEqual(1, Events(all, AnalyticsEventId.EggOddsOpen));
            }
        }

        [Test]
        public void Renewal_AndEssenceNodes_AreSent()
        {
            using (var rig = new Rig())
            {
                var progression = new ProgressionServices(rig.State, rig.Content, rig.Economy, rig.Hatch, rig.Clock, null,
                    new ProgressionConfig { TimeZone = TimeZoneInfo.Utc });
                rig.Analytics.AttachProgression(progression);
                rig.Analytics.StartSession(0);
                rig.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e12), EconomyReason.Debug);
                rig.State.Progression.Refuge.Level = 15;
                rig.Clock.Set(TestUtil.T0 + TimeSpan.FromDays(5));
                Assert.IsTrue(progression.Renewal.TryRenew(out var done));
                Assert.IsTrue(progression.Essence.TryBuy(ContentId.FromSource("essence.economy.soft_light"), out _));
                rig.Analytics.Flush();
                string all = rig.Sink.All;
                var renewal = Fields(all, AnalyticsEventId.Renewal);
                Assert.AreEqual("1", renewal[3], "First Renewal.");
                Assert.AreEqual(done.Gain.Total.ToString(), renewal[4]);
                var node = Fields(all, AnalyticsEventId.EssenceNodeBuy);
                Assert.AreEqual(ContentId.FromSource("essence.economy.soft_light").Value.ToString(), node[3]);
                Assert.AreEqual("1", node[4]);
                rig.Analytics.AttachProgression(null);
                progression.Dispose();
            }
        }

        [Test]
        public void ActivityEvents_FollowTheDoc08Codes()
        {
            using (var rig = new Rig())
            {
                rig.State.Progression.Refuge.Level = 3;
                var activities = new ActivityServices(rig.State, rig.State.Activities, rig.Content, rig.Economy, rig.Hatch, null, null, null,
                    rig.Clock, null);
                activities.Rules.Register(ActivityTestRig.TestRules(ActivityId.Test));
                activities.OnResume();
                activities.RefreshUnlocks();
                rig.Analytics.AttachActivities(activities);
                rig.Analytics.StartSession(0);
                uint id = ActivityCatalog.Get(ActivityId.Test).ContentId.Value;

                var game = new DummyActivityController { NextScore = 320 };
                Assert.AreEqual(ActivityStartResult.Started, activities.Runner.TryStart(ActivityRequest.Free(ActivityId.Test), game));
                activities.Runner.Tick(0.5f);
                activities.Runner.Finish();
                var challenge = new DummyActivityController { NextScore = 160 };
                Assert.AreEqual(ActivityStartResult.Started, activities.Runner.TryStart(ActivityRequest.ChallengeGame(ActivityId.Test, 0), challenge));
                activities.Runner.Tick(0.5f);
                activities.Runner.Finish();
                activities.Tickets.Grant(2, TicketSource.Calendar);
                rig.Analytics.TodayOpen(2, true, 5);
                rig.Analytics.TicketsEmpty(3, 4321.0);
                rig.Analytics.Flush();
                string all = rig.Sink.All;

                Assert.AreEqual(2, Events(all, AnalyticsEventId.ActivityStart));
                var start = Fields(all, AnalyticsEventId.ActivityStart);
                Assert.AreEqual(id.ToString(), start[3], "The activity travels as its content id.");
                Assert.AreEqual("1", start[4], "Mode 1: rewarded (doc 08, section 9.5).");
                Assert.AreEqual("4", start[6], "Tickets after the start.");
                var end = Fields(all, AnalyticsEventId.ActivityEnd);
                Assert.AreEqual("1", end[4]);
                Assert.AreEqual("3", end[5], "Three stars.");
                Assert.AreEqual("0", end[7], "Free mode: challenge 0.");
                Assert.AreEqual("320", end[8]);
                var improved = Fields(all, AnalyticsEventId.ActivityChallenge);
                Assert.AreEqual("1", improved[4], "Challenges travel from 1.");
                Assert.AreEqual("3", improved[5]);
                Assert.AreEqual("1", improved[6], "First attempt.");
                var gain = Fields(all, AnalyticsEventId.TicketsGain);
                Assert.AreEqual("13", gain[3], "The calendar, appended after the doc's sources.");
                Assert.AreEqual("2", gain[4]);
                var today = Fields(all, AnalyticsEventId.TodayOpen);
                Assert.AreEqual(new[] { "2", "1", "5" }, new[] { today[3], today[4], today[5] });
                var empty = Fields(all, AnalyticsEventId.TicketsEmpty);
                Assert.AreEqual("3", empty[3], "Choice 3: Gems.");
                Assert.AreEqual("4321", empty[4]);
                Assert.IsFalse(rig.State.Analytics.Has(FunnelMilestone.FirstActivity), "The framework's test activity is not a player's first activity.");
                rig.Analytics.AttachActivities(null);
                activities.Dispose();
            }
        }

        // ---------------- Monetization ----------------

        [Test]
        public void Purchases_ImpressionsClicksAndRestore_AreSent()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            for (int i = 0; i < 6; i++)
            {
                var species = ContentId.FromSource(i % 2 == 0 ? "species.rabbit" : "species.squirrel");
                state.Collection.Add(new AnimalInstance { Species = species, Seed = (uint)i + 1 });
                state.Collection.DiscoveredSpecies.Add(species);
            }
            using (var rig = new Rig(state: state))
            {
                rig.Analytics.StartSession(0);
                var starter = ContentId.FromSource("offer.starter.001");
                var pack = ContentId.FromSource("offer.gems.tier01");
                rig.Analytics.OfferImpression(starter, OfferPlacement.ShopFeatured);
                rig.Analytics.OfferImpression(starter, OfferPlacement.ShopFeatured);
                rig.Analytics.OfferClick(starter, OfferPlacement.ShopFeatured);
                var outcome = rig.Store.PurchaseAsync(starter, CancellationToken.None).GetAwaiter().GetResult();
                Assert.AreEqual(PurchaseFlowStatus.Granted, outcome.Status);
                rig.Store.PurchaseAsync(pack, CancellationToken.None).GetAwaiter().GetResult();
                rig.Store.RestoreAsync(CancellationToken.None).GetAwaiter().GetResult();
                rig.Analytics.EndSession();

                string text = rig.Sink.All;
                Assert.AreEqual(1, Events(text, AnalyticsEventId.OfferImpression), "One impression per offer per session.");
                Assert.AreEqual(1, Events(text, AnalyticsEventId.OfferClick));
                Assert.AreEqual(2, Events(text, AnalyticsEventId.PurchaseResult));
                Assert.AreEqual(1, Events(text, AnalyticsEventId.Restore));
                StringAssert.Contains("," + starter.Value + "," + outcome.Grant.Product.Value + ",0,199,500,1,1,2]", text,
                    "Starter: granted, 1,99 reference, 500 Gems, first purchase, new grant.");
                Assert.IsTrue(state.Analytics.Has(FunnelMilestone.FirstOfferImpression));
                Assert.IsTrue(state.Analytics.Has(FunnelMilestone.FirstPurchase));
                Assert.IsTrue(state.Analytics.Has(FunnelMilestone.SecondPurchase));
                Assert.IsFalse(text.Contains("\"sku\"") || text.Contains("pw."), "No store SKU or transaction id in analytics.");
            }
        }

        [Test]
        public void CancelledPurchase_IsSent_WithoutAFunnelStep()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            for (int i = 0; i < 6; i++)
            {
                var species = ContentId.FromSource(i % 2 == 0 ? "species.rabbit" : "species.squirrel");
                state.Collection.Add(new AnimalInstance { Species = species, Seed = (uint)i + 1 });
                state.Collection.DiscoveredSpecies.Add(species);
            }
            var provider = new SandboxStoreProvider("en", 0);
            using (var rig = new Rig(state: state))
            {
                rig.Analytics.StartSession(0);
                var store = new StoreService(rig.Content, rig.Offers, provider, rig.Clock, rig.Saves);
                store.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
                rig.Analytics.Attach(rig.Economy, rig.Hatch, store);
                provider.ScriptNext(StorePurchaseStatus.Cancelled);
                var outcome = store.PurchaseAsync(ContentId.FromSource("offer.gems.tier02"), CancellationToken.None).GetAwaiter().GetResult();
                Assert.AreEqual(PurchaseFlowStatus.Cancelled, outcome.Status);
                rig.Analytics.Flush();
                StringAssert.Contains(",1,499,0,0,0,0]", rig.Sink.All, "Cancelled, 4,99 reference, nothing granted.");
                Assert.IsFalse(state.Analytics.Has(FunnelMilestone.FirstPurchase));
                store.Dispose();
            }
        }

        // ---------------- Performance ----------------

        [Test]
        public void PerfSample_SummarizesAMinuteOfFrames()
        {
            using (var rig = new Rig())
            {
                rig.Analytics.SetDeviceState(1, true, () => 321u);
                rig.Analytics.StartSession(0);
                for (int i = 0; i < 20; i++)
                    rig.Analytics.Tick(0.1f); // scene entry: inside the warmup, not measured
                for (int i = 0; i < 3900; i++)
                    rig.Analytics.Tick(i % 100 == 0 ? 0.05f : 0.016f);
                rig.Analytics.Flush();
                string text = rig.Sink.All;
                Assert.AreEqual(1, Events(text, AnalyticsEventId.PerfSample), "One summary per minute of play.");
                var f = Fields(text, AnalyticsEventId.PerfSample);
                int frames = int.Parse(f[3]);
                Assert.That(frames, Is.InRange(3600, 3800), "About a minute of frames, warmup excluded.");
                Assert.AreEqual(16.5, double.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture), "p95 (0.5 ms buckets).");
                Assert.That(int.Parse(f[6]), Is.InRange(34, 40), "Slow frames: the 50 ms ones.");
                Assert.AreEqual("50", f[7], "Max frame.");
                Assert.AreEqual("321", f[8], "Memory MB.");
                Assert.AreEqual("1", f[9], "Quality tier.");
                Assert.AreEqual("1", f[10], "Battery saver.");
            }
        }

        [Test]
        public void Histogram_Percentiles()
        {
            var histogram = new FrameTimeHistogram();
            for (int i = 1; i <= 100; i++)
                histogram.Add(i);
            Assert.AreEqual(95f, histogram.Percentile(0.95), 0.51f);
            Assert.AreEqual(100f, histogram.MaxMs);
            Assert.AreEqual(67, histogram.SlowFrames);
            histogram.Add(500f);
            Assert.AreEqual(500f, histogram.Percentile(1.0));
            histogram.Reset();
            Assert.AreEqual(0f, histogram.Percentile(0.95));
        }

        // ---------------- Batching, consent, outbox ----------------

        [Test]
        public void Flush_EveryMinute_Or_At100Events()
        {
            using (var rig = new Rig())
            {
                rig.Analytics.StartSession(0);
                rig.Analytics.ScreenView(ScreenId.Codex);
                rig.Analytics.Tick(30f);
                Assert.AreEqual(0, rig.Sink.Batches.Count);
                rig.Analytics.Tick(31f);
                Assert.AreEqual(1, rig.Sink.Batches.Count, "A minute of play.");
                for (int i = 0; i < 100; i++)
                    rig.Analytics.OfferClick(ContentId.FromSource("offer.gems.tier01"), OfferPlacement.ShopGems);
                rig.Analytics.Tick(0.016f);
                Assert.AreEqual(2, rig.Sink.Batches.Count, "100 events waiting.");
                Assert.AreEqual(0, rig.Analytics.Pending);
            }
        }

        [Test]
        public void FullRing_DropsTheOldest_AndSaysSo()
        {
            using (var rig = new Rig(config: new AnalyticsConfig { RingCapacity = 16, FlushThreshold = 1000 }))
            {
                rig.Analytics.StartSession(0);
                for (int i = 0; i < 30; i++)
                    rig.Analytics.OfferClick(ContentId.FromSource("offer.gems.tier01"), OfferPlacement.ShopGems);
                Assert.AreEqual(16, rig.Analytics.Pending);
                rig.Analytics.Flush();
                StringAssert.Contains("\"dropped\":16", rig.Sink.Batches[0], "Session start, tutorial start and 14 clicks lost.");
                Assert.AreEqual(16, Events(rig.Sink.Batches[0], AnalyticsEventId.OfferClick));
            }
        }

        [Test]
        public void Consent_UnknownKeepsLocal_GrantedSends_DeniedPurges()
        {
            using (var rig = new Rig(AnalyticsConsent.Unknown))
            {
                Assert.IsFalse(rig.Sink.SendAllowed, "Not asked yet: nothing leaves the phone.");
                rig.Analytics.StartSession(0);
                rig.Analytics.Flush();
                Assert.AreEqual(1, rig.Sink.Batches.Count, "Kept locally.");

                rig.Analytics.SetConsent(AnalyticsConsent.Granted);
                Assert.IsTrue(rig.Sink.SendAllowed);

                rig.Analytics.ScreenView(ScreenId.Shop);
                rig.Analytics.SetConsent(AnalyticsConsent.Denied);
                Assert.IsFalse(rig.Sink.SendAllowed);
                Assert.AreEqual(1, rig.Sink.Purges);
                Assert.AreEqual(0, rig.Analytics.Pending, "Pending events deleted.");
                rig.HatchOne();
                rig.Analytics.Milestone(FunnelMilestone.CodexOpen);
                rig.Analytics.EndSession();
                Assert.AreEqual(0, rig.Sink.Batches.Count, "Nothing collected after a refusal.");
                Assert.IsFalse(rig.State.Analytics.Has(FunnelMilestone.CodexOpen));
            }
        }

        private sealed class FakeTransport : IAnalyticsTransport
        {
            public readonly List<string> Sent = new List<string>();
            public bool Accept = true;
            public string Name => "fake";
            public bool IsAvailable => true;

            public bool TrySend(byte[] payload, int length)
            {
                if (!Accept)
                    return false;
                lock (Sent)
                    Sent.Add(Encoding.UTF8.GetString(payload, 0, length));
                return true;
            }
        }

        [Test]
        public void Outbox_IsBounded_SendsOnlyWithConsent_AndPurges()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PocketWildsAnalyticsTests", Guid.NewGuid().ToString("N"));
            var transport = new FakeTransport();
            try
            {
                using (var outbox = new AnalyticsOutbox(directory, transport, maxFiles: 4))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        var data = Encoding.UTF8.GetBytes("{\"batch\":" + i + "}");
                        outbox.Write(data, data.Length);
                        Assert.IsTrue(outbox.WaitIdle(TimeSpan.FromSeconds(5)));
                    }
                    Assert.AreEqual(4, outbox.CountFiles(), "Bounded: the oldest batches go first.");
                    Assert.AreEqual(10, outbox.FilesWritten);
                    Assert.AreEqual(6, outbox.FilesDeleted);
                    Assert.AreEqual(0, transport.Sent.Count, "No consent, nothing sent.");

                    outbox.SendAllowed = true;
                    Assert.IsTrue(SpinWait.SpinUntil(() => outbox.FilesSent == 4, 5000), "Consent given: the kept batches leave.");
                    Assert.AreEqual("{\"batch\":6}", transport.Sent[0], "Oldest first.");
                    var more = Encoding.UTF8.GetBytes("{\"batch\":10}");
                    outbox.Write(more, more.Length);
                    Assert.IsTrue(outbox.WaitIdle(TimeSpan.FromSeconds(5)));
                    Assert.AreEqual(0, outbox.CountFiles(), "Sent batches are deleted.");
                    Assert.AreEqual(5, transport.Sent.Count, "The four kept and the new one.");
                    Assert.IsNull(outbox.LastError);

                    transport.Accept = false;
                    outbox.Write(more, more.Length);
                    Assert.IsTrue(outbox.WaitIdle(TimeSpan.FromSeconds(5)));
                    Assert.AreEqual(1, outbox.CountFiles(), "A refused batch stays for a retry.");
                    outbox.Purge();
                    Assert.IsTrue(outbox.WaitIdle(TimeSpan.FromSeconds(5)));
                    Assert.AreEqual(0, outbox.CountFiles(), "Consent refused: everything kept is deleted.");
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        // ---------------- Performance criterion: nothing per frame ----------------

        [Test]
        public void SteadyState_TicksEventsAndBatches_DoNotAllocate()
        {
            using (var rig = new Rig(config: new AnalyticsConfig { FlushThreshold = 50 }))
            {
                rig.Sink.Keep = false;
                rig.Analytics.SetDeviceState(1, false, () => 100u);
                rig.Analytics.StartSession(0);
                var offer = ContentId.FromSource("offer.gems.tier01");
                rig.State.Wallet.Light = BigValue.FromMantissaExponent(1.0, 30);
                for (int i = 0; i < 300; i++)
                {
                    rig.Analytics.Tick(0.016f);
                    rig.Analytics.ScreenView(ScreenId.Codex);
                    rig.Analytics.OfferClick(offer, OfferPlacement.ShopGems);
                    rig.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(10), EconomyReason.Reward);
                }
                rig.Analytics.Tick(61f);

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 5000; i++)
                {
                    rig.Analytics.Tick(0.016f);
                    rig.Analytics.ScreenView(ScreenId.Codex);
                    rig.Analytics.OfferImpression(offer, OfferPlacement.ShopGems);
                    rig.Analytics.OfferClick(offer, OfferPlacement.ShopGems);
                    rig.Analytics.Milestone(FunnelMilestone.CodexOpen);
                    rig.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(10), EconomyReason.Reward);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Greater(rig.Analytics.BatchesWritten, 100, "Batches were encoded during the loop.");
                Assert.AreEqual(0, allocated, "Events, perf sampling and batch encoding cost no allocation.");
            }
        }

        /// <summary>Minimal JSON syntax check (objects, arrays, strings, numbers, literals).</summary>
        private static class MiniJson
        {
            public static void Validate(string text)
            {
                int i = 0;
                Value(text, ref i);
                Assert.AreEqual(text.Length, i, "Trailing characters after the JSON value.");
            }

            private static void Value(string s, ref int i)
            {
                Assert.Less(i, s.Length, "Unexpected end of JSON.");
                char c = s[i];
                if (c == '{')
                {
                    i++;
                    if (s[i] == '}')
                    {
                        i++;
                        return;
                    }
                    while (true)
                    {
                        String(s, ref i);
                        Assert.AreEqual(':', s[i++]);
                        Value(s, ref i);
                        if (s[i] == ',')
                        {
                            i++;
                            continue;
                        }
                        Assert.AreEqual('}', s[i++]);
                        return;
                    }
                }
                if (c == '[')
                {
                    i++;
                    if (s[i] == ']')
                    {
                        i++;
                        return;
                    }
                    while (true)
                    {
                        Value(s, ref i);
                        if (s[i] == ',')
                        {
                            i++;
                            continue;
                        }
                        Assert.AreEqual(']', s[i++]);
                        return;
                    }
                }
                if (c == '"')
                {
                    String(s, ref i);
                    return;
                }
                int start = i;
                if (s[i] == '-')
                    i++;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
                    i++;
                Assert.Greater(i, start, "Bad JSON value at " + start);
                Assert.IsTrue(double.TryParse(s.Substring(start, i - start), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _), "Bad number at " + start);
            }

            private static void String(string s, ref int i)
            {
                Assert.AreEqual('"', s[i++]);
                while (s[i] != '"')
                {
                    if (s[i] == '\\')
                        i++;
                    Assert.GreaterOrEqual(s[i], ' ', "Control character in a string.");
                    i++;
                }
                i++;
            }
        }
    }
}
