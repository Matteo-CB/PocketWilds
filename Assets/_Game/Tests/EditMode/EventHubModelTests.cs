using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Events;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T48 event hub view model (EventHubModel) over the test event: tier rows and their states, the HUD ring's progress, the
    /// claimable count, which tabs show in each phase, real dates and short odds in both languages; and the hub's text table.
    /// </summary>
    public class EventHubModelTests
    {
        private static (EventService Events, FakeGameClock Clock, EventCalendarEntry Entry) Rig(int version = 1)
        {
            var builder = new ContentRegistryBuilder();
            EventTestFixture.AddContent(builder, version);
            var content = builder.Build();
            var entry = EventTestFixture.Entry(EventTestFixture.Year, version);
            var clock = new FakeGameClock(entry.StartUtc.AddDays(-5));
            var state = PlayerState.CreateNew(clock.UtcNow);
            var economy = new EconomyService(state, content, EconomyConfig.Default, null);
            var events = new EventService(state, content, clock, null, new EventConfig(), EventTestFixture.Calendar(entry))
            {
                Economy = economy, Granter = new RewardGranter(state, economy, null, null), Albums = new AlbumService(state.Albums) { Unlocked = true },
                DefaultTreat = () => ContentId.FromSource(EventTestFixture.Treat),
            };
            return (events, clock, entry);
        }

        [Test]
        public void Announce_ShowsNoTab_AndNoTrack()
        {
            var (events, clock, entry) = Rig();
            clock.Set(entry.StartUtc.AddHours(-24));
            var model = new EventHubModel(events);
            Assert.AreEqual(EventPhase.Announce, model.Phase);
            Assert.IsTrue(model.Announce);
            foreach (EventHubTab tab in Enum.GetValues(typeof(EventHubTab)))
                Assert.IsFalse(model.TabVisible(tab), tab + " waits for the start");
            Assert.AreEqual(3, model.Definition.Headline.Length, "Three headline rewards on the announce page.");
        }

        [Test]
        public void Running_TierStates_RingAndClaims()
        {
            var (events, clock, entry) = Rig();
            clock.Set(entry.StartUtc.AddHours(2));
            Assert.IsTrue(events.Join());
            var model = new EventHubModel(events);
            Assert.AreEqual(EventDefinition.TierCount, model.TierRows);
            Assert.IsTrue(model.TabVisible(EventHubTab.Track));
            Assert.IsTrue(model.TabVisible(EventHubTab.Shop));
            Assert.IsFalse(model.TabVisible(EventHubTab.Memory), "The Memory Shop is for a returning event only.");
            Assert.AreEqual(EventCellState.Locked, model.GetTier(1).FreeState);
            Assert.AreEqual(0f, model.TierProgress, 1e-4f);

            events.GrantEventCurrency(300, ActivityId.Grooming);
            Assert.GreaterOrEqual(events.Tier, 2);
            var row = model.GetTier(1);
            Assert.AreEqual(EventCellState.Claimable, row.FreeState);
            Assert.AreEqual(EventCellState.NeedsGold, row.GoldState, "Gold reached but not owned: the lock.");
            Assert.AreEqual(EventRewardKind.Sticker, row.Free.Kind);
            Assert.AreEqual(EventService.PointsFor(events.Params, 1), row.Points);
            Assert.AreEqual(events.Tier, model.ClaimableCount, "Only the free line is claimable without the Carnet d'or.");
            float expected = EventHubModel.Progress(events.Points, events.Tier, events.TierCount, events.PointsForTier);
            Assert.AreEqual(expected, model.TierProgress, 1e-5f);
            Assert.That(model.TierProgress, Is.InRange(0f, 1f));

            Assert.AreEqual(EventClaimStatus.Claimed, events.Claim(1, false));
            Assert.AreEqual(EventCellState.Claimed, model.GetTier(1).FreeState);
            Assert.AreEqual(EventRewardKind.Souvenir, model.GetTier(5).FreeExtra.Kind, "Tier 5 also gives the Souvenir.");
            Assert.AreEqual(EventCellState.Locked, model.GetTier(30).FreeState);
        }

        [Test]
        public void ReturningEvent_ShowsTheMemoryShop()
        {
            var (events, clock, entry) = Rig(version: 2);
            clock.Set(entry.StartUtc.AddHours(2));
            events.Join();
            Assert.IsTrue(new EventHubModel(events).TabVisible(EventHubTab.Memory));
        }

        [Test]
        public void Progress_IsTheShareToTheNextTier()
        {
            Func<int, int> points = t => t * 120;
            Assert.AreEqual(0.5f, EventHubModel.Progress(60, 0, 30, points), 1e-5f);
            Assert.AreEqual(0.25f, EventHubModel.Progress(150, 1, 30, points), 1e-5f);
            Assert.AreEqual(1f, EventHubModel.Progress(9000, 30, 30, points), 1e-5f);
        }

        [Test]
        public void Dates_AreRealDates_InBothLanguages()
        {
            var start = EventTestFixture.Entry().StartUtc;
            Assert.AreEqual("13 novembre", EventHubModel.FormatDate(start, "fr", TimeSpan.Zero));
            Assert.AreEqual("November 13", EventHubModel.FormatDate(start, "en", TimeSpan.Zero));
        }

        [Test]
        public void Odds_AreShortPercentages()
        {
            Assert.AreEqual("14,3", EventHubModel.FormatPercent(1.0 / 7.0, "fr"));
            Assert.AreEqual("14.3", EventHubModel.FormatPercent(1.0 / 7.0, "en"));
            Assert.AreEqual("1", EventHubModel.FormatPercent(0.01, "fr"));
        }

        [Test]
        public void EveryHubKey_HasEnglishAndFrench_WithoutDashOrSparkle()
        {
            var table = new Dictionary<string, (string En, string Fr)>();
            for (int i = 0; i < EventTexts.Texts.GetLength(0); i++)
                table[EventTexts.Texts[i, 0]] = (EventTexts.Texts[i, 1], EventTexts.Texts[i, 2]);
            // T49: the download page's words live with the first event that needs them (BurrowsTexts).
            for (int i = 0; i < BurrowsTexts.Texts.GetLength(0); i++)
                table.TryAdd(BurrowsTexts.Texts[i, 0], (BurrowsTexts.Texts[i, 1], BurrowsTexts.Texts[i, 2]));
            var keys = new List<string>(EventText.RewardKeys);
            foreach (var field in typeof(EventText).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.IsLiteral && field.FieldType == typeof(string) && field.Name != nameof(EventText.RewardPrefix))
                    keys.Add((string)field.GetValue(null));
            foreach (var key in keys)
            {
                Assert.IsTrue(table.TryGetValue(key, out var text), "Missing text " + key);
                Assert.IsNotEmpty(text.En, key);
                Assert.IsNotEmpty(text.Fr, key);
                foreach (var value in new[] { text.En, text.Fr })
                {
                    // Code points, never the characters themselves (the sparkle rule scans every file).
                    Assert.IsFalse(value.IndexOf((char)0x2014) >= 0, "No em dash: " + key);
                    foreach (int code in new[] { 0x2728, 0x2726, 0x2727 })
                        Assert.IsFalse(value.IndexOf((char)code) >= 0, "No sparkle: " + key);
                }
            }
        }
    }
}
