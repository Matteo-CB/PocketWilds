using System;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T37: activity tickets (doc Mini-jeux section 2; doc 01 section 0; doc 08 activities.tickets_*, gems.tickets_pack_*):
    /// the first fill and the T36 stock, exactly +1 every 120 minutes across absences with the remainder kept, the cap,
    /// gifts above the reserve kept, packs for Gems (price, daily cap, reason), bought tickets used last, the clock moved
    /// back, the Club and Essence reserves, Remote Config bounds.
    /// </summary>
    public sealed class TicketTests
    {
        private static void Advance(ActivityTestRig rig, double minutes)
        {
            rig.Clock.Advance(TimeSpan.FromMinutes(minutes));
            rig.S.Tickets.Refresh();
        }

        private static void Spend(ActivityTestRig rig, int count)
        {
            for (int i = 0; i < count; i++)
                Assert.IsTrue(rig.S.Tickets.TrySpend(out _), "Spend " + i);
        }

        [Test]
        public void TheFirstUse_FillsTheReserve_AndAbsorbsTheT36Stock()
        {
            var rig = ActivityTestRig.Create(beforeServices: state => state.Inventory.Tickets = 2);
            Assert.IsTrue(rig.Activities.TicketsStarted);
            Assert.AreEqual(7, rig.S.Tickets.Count, "5 for the reserve, 2 won before the Play button.");
            Assert.AreEqual(0, rig.State.Inventory.Tickets, "The stock moved into the pool.");
            Assert.IsTrue(rig.S.Tickets.Full);
            Assert.AreEqual(0, rig.S.Tickets.MinutesToNext);

            // A ticket a gift still puts into the inventory joins at the next refresh.
            rig.State.Inventory.Tickets = 1;
            rig.S.Tickets.Refresh();
            Assert.AreEqual(8, rig.S.Tickets.Count);
            Assert.AreEqual(0, rig.State.Inventory.Tickets);

            // A second start never refills.
            rig.S.Tickets.Refresh();
            Assert.AreEqual(8, rig.S.Tickets.Count);
        }

        [Test]
        public void TheRefill_IsExactlyOneEvery120Minutes_AndKeepsTheTimeAlreadyWaited()
        {
            var rig = ActivityTestRig.Create();
            Spend(rig, 3);
            Assert.AreEqual(2, rig.S.Tickets.Count);
            Assert.AreEqual(120, rig.S.Tickets.MinutesToNext, "Leaving a full reserve starts a new interval.");

            Advance(rig, 119);
            Assert.AreEqual(2, rig.S.Tickets.Count);
            Assert.AreEqual(1, rig.S.Tickets.MinutesToNext);
            Advance(rig, 1);
            Assert.AreEqual(3, rig.S.Tickets.Count, "Exactly at 120 minutes.");
            Assert.AreEqual(120, rig.S.Tickets.MinutesToNext);

            // An absence of 250 minutes without any refresh: the 2 missing tickets, and the reserve is full.
            rig.Clock.Advance(TimeSpan.FromMinutes(250));
            rig.S.OnResume();
            Assert.AreEqual(5, rig.S.Tickets.Count, "Capped at the reserve.");
            Assert.AreEqual(0, rig.S.Tickets.MinutesToNext);
        }

        [Test]
        public void TheRemainder_IsKept_WhenTheReserveIsNotReached()
        {
            var rig = ActivityTestRig.Create();
            Spend(rig, 4);
            rig.Clock.Advance(TimeSpan.FromMinutes(250));
            rig.S.OnResume();
            Assert.AreEqual(3, rig.S.Tickets.Count, "1 + 2 refills.");
            Assert.AreEqual(110, rig.S.Tickets.MinutesToNext, "250 = 2 × 120 + 10: 110 minutes to go.");
            Advance(rig, 110);
            Assert.AreEqual(4, rig.S.Tickets.Count);
        }

        [Test]
        public void ALongAbsence_RefillsToTheReserve_Only()
        {
            var rig = ActivityTestRig.Create();
            int refilled = 0;
            rig.S.Tickets.Refilled += (count, balance) => refilled += count;
            Spend(rig, 5);
            rig.Clock.Advance(TimeSpan.FromDays(10));
            rig.S.OnResume();
            Assert.AreEqual(5, rig.S.Tickets.Count);
            Assert.AreEqual(5, refilled, "The refill is reported apart (economy_flow), never as TicketsGain.");
        }

        [Test]
        public void GiftsAboveTheReserve_AreKept_AndStopTheRefill_UpToTen_TheRestWaits()
        {
            var rig = ActivityTestRig.Create();
            TicketSource source = TicketSource.Debug;
            int amount = 0, balance = 0;
            rig.S.Tickets.TicketsGained += (s, a, b) =>
            {
                source = s;
                amount = a;
                balance = b;
            };
            rig.S.Tickets.Grant(3, TicketSource.DailyGift);
            Assert.AreEqual(8, rig.S.Tickets.Count, "« Aucun billet retiré ».");
            Assert.AreEqual(TicketSource.DailyGift, source);
            Assert.AreEqual(3, amount);
            Assert.AreEqual(8, balance);

            Advance(rig, 600);
            Assert.AreEqual(8, rig.S.Tickets.Count, "Above the reserve the refill waits.");

            Spend(rig, 4);
            Assert.AreEqual(4, rig.S.Tickets.Count);
            Advance(rig, 120);
            Assert.AreEqual(5, rig.S.Tickets.Count, "Under the reserve the refill starts again.");

            rig.S.Tickets.Grant(7, TicketSource.ReturnGift);
            Assert.AreEqual(10, rig.S.Tickets.Count, "A gift raises the pool to 10 at most.");
            Assert.AreEqual(2, rig.State.Inventory.Tickets, "The rest waits, never lost.");
            Assert.AreEqual(7, amount, "Reported whole.");
            Spend(rig, 1);
            rig.S.Tickets.Refresh();
            Assert.AreEqual(10, rig.S.Tickets.Count);
            Assert.AreEqual(1, rig.State.Inventory.Tickets);
        }

        [Test]
        public void APack_Costs30Gems_Gives3BoughtTickets_TwiceADay()
        {
            var rig = ActivityTestRig.Create(gems: 100);
            EconomyReason reason = EconomyReason.Debug;
            rig.Economy.Changed += change =>
            {
                if (change.Currency == CurrencyId.Gems)
                    reason = change.Reason;
            };
            TicketSource source = TicketSource.Debug;
            rig.S.Tickets.TicketsGained += (s, a, b) => source = s;

            Assert.AreEqual(2, rig.S.Tickets.PacksLeftToday);
            Assert.IsTrue(rig.S.Tickets.CanBuyPack);
            Assert.IsTrue(rig.S.Tickets.TryBuyPack());
            Assert.AreEqual(70, rig.Gems);
            Assert.AreEqual(EconomyReason.TicketPack, reason);
            Assert.AreEqual(TicketSource.Gems, source);
            Assert.AreEqual(3, rig.S.Tickets.Paid);
            Assert.AreEqual(8, rig.S.Tickets.Count, "Bought tickets may pass the reserve.");
            Assert.IsTrue(rig.S.Tickets.TryBuyPack());
            Assert.AreEqual(40, rig.Gems);
            Assert.AreEqual(0, rig.S.Tickets.PacksLeftToday);
            Assert.IsFalse(rig.S.Tickets.TryBuyPack(), "Twice a day at most.");
            Assert.AreEqual(40, rig.Gems);

            rig.Clock.Advance(TimeSpan.FromDays(1));
            rig.S.OnResume();
            Assert.AreEqual(2, rig.S.Tickets.PacksLeftToday, "A new day, two new packs.");
            Assert.IsTrue(rig.S.Tickets.TryBuyPack());
            Assert.AreEqual(10, rig.Gems);
            Assert.IsFalse(rig.S.Tickets.CanBuyPack, "10 Gems are not enough.");
            Assert.IsFalse(rig.S.Tickets.TryBuyPack());
            Assert.AreEqual(9, rig.S.Tickets.Paid);
        }

        [Test]
        public void FreeTicketsAreUsedFirst_BoughtOnesLast_AndOnlyFreeOnesRefill()
        {
            var rig = ActivityTestRig.Create(gems: 30);
            Assert.IsTrue(rig.S.Tickets.TryBuyPack());
            Spend(rig, 5);
            Assert.AreEqual(0, rig.S.Tickets.Free);
            Assert.AreEqual(3, rig.S.Tickets.Paid);
            Assert.IsTrue(rig.S.Tickets.TrySpend(out bool paid));
            Assert.IsTrue(paid, "Bought tickets come after the free ones.");
            Assert.AreEqual(2, rig.S.Tickets.Paid);
            Assert.AreEqual(120, rig.S.Tickets.MinutesToNext, "Bought tickets never stop the refill.");
            Advance(rig, 120);
            Assert.AreEqual(1, rig.S.Tickets.Free);
            Assert.IsTrue(rig.S.Tickets.TrySpend(out paid));
            Assert.IsFalse(paid);
        }

        [Test]
        public void AClockMovedBack_NeitherRefillsNorBreaks()
        {
            var rig = ActivityTestRig.Create();
            Spend(rig, 2);
            Advance(rig, 60);
            rig.State.Offline.HighWaterUtc = rig.Clock.UtcNow;
            rig.Clock.Set(rig.Clock.UtcNow.AddHours(-10));
            rig.S.Tickets.Refresh();
            Assert.AreEqual(3, rig.S.Tickets.Count);
            Assert.AreEqual(60, rig.S.Tickets.MinutesToNext, "The high-water mark holds the time.");
            rig.Clock.Set(rig.State.Offline.HighWaterUtc.AddMinutes(60));
            rig.S.Tickets.Refresh();
            Assert.AreEqual(4, rig.S.Tickets.Count);
        }

        [Test]
        public void TheReserve_Is7WithTheClub_AndGrowsWithTheExtraTicketNode()
        {
            var rig = ActivityTestRig.Create();
            Assert.AreEqual(5, rig.S.Tickets.Max);
            rig.Config.ClubActive = true;
            Assert.AreEqual(7, rig.S.Tickets.Max);
            Assert.IsFalse(rig.S.Tickets.Full);
            Advance(rig, 240);
            Assert.AreEqual(7, rig.S.Tickets.Count);

            // The Club ends: nothing is taken back (doc 08), the extra tickets stay until used.
            rig.Config.ClubActive = false;
            rig.S.Tickets.Refresh();
            Assert.AreEqual(7, rig.S.Tickets.Count);

            // The Essence node Extra Ticket: one more in the reserve (doc 08 offline.extra_ticket).
            rig.State.Progression.EssenceRanks[ContentId.FromSource("essence.offline.long_nap")] = 5;
            rig.Economy.Grant(CurrencyId.Essence, BigValue.FromDouble(100), EconomyReason.Debug);
            Assert.IsTrue(rig.P.Essence.TryBuy(ContentId.FromSource("essence.offline.extra_ticket"), out _));
            Assert.AreEqual(6, rig.S.Tickets.Max);
        }

        [Test]
        public void TheRefillInterval_FollowsRemoteConfig_InsideItsBounds()
        {
            var config = new ActivityConfig();
            Assert.IsTrue(config.TryOverride("activities.tickets_refill_minutes", 90));
            Assert.IsFalse(config.TryOverride("activities.tickets_refill_minutes", 20));
            Assert.IsFalse(config.TryOverride("activities.tickets_refill_minutes", 90.5));
            Assert.IsFalse(config.TryOverride("activities.tickets_max", 2));
            Assert.IsFalse(config.TryOverride("activities.tickets_max", 11));
            Assert.IsTrue(config.TryOverride("activities.tickets_max_club", 4));
            Assert.AreEqual(5, config.Reserve(true), "The Club's reserve is never under the normal one.");
            Assert.IsFalse(config.TryOverride("unknown.key", 1));

            var rig = ActivityTestRig.Create(config: config);
            Spend(rig, 1);
            Advance(rig, 90);
            Assert.AreEqual(5, rig.S.Tickets.Count);
        }
    }
}
