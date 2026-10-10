using System;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    public class VisitorTests
    {
        private const int MeadowRabbit = 1;
        private const int FloweryHedgehog = 2;
        private const int SunSquirrel = 4;
        private const int RussetOwl = 7;
        private const int SnowRabbit = 10;

        private static VisitorService NewVisitors(out PlayerState state, out FakeGameClock clock, uint seed = 12345, bool withOwl = false)
        {
            state = new PlayerState();
            clock = new FakeGameClock(JournalTestContent.Start);
            return new VisitorService(state, JournalTestContent.Build(withOwl), clock) { Zone = TimeZoneInfo.Utc, Seed = seed, ForceOpen = true };
        }

        /// <summary>Sets the clock inside the day's first visit (a minute after its arrival) and ticks.</summary>
        private static int EnterFirstVisit(VisitorService visitors, FakeGameClock clock, int day)
        {
            var arrivals = new long[VisitorService.MaxVisits];
            var who = new int[VisitorService.MaxVisits];
            visitors.Schedule(day, arrivals, who);
            clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(arrivals[0] + 60_000));
            visitors.Tick();
            return who[0];
        }

        private static int Today => JournalService.DayOf(JournalTestContent.Start, TimeZoneInfo.Utc);

        // ------------------------------------------------------------------------------------------- availability

        [Test]
        public void Availability_OnlyExistingSpeciesAndVariants_SeasonFirst()
        {
            var visitors = NewVisitors(out _, out _);
            Assert.IsTrue(visitors.IsAvailable(MeadowRabbit));
            Assert.IsTrue(visitors.IsAvailable(FloweryHedgehog));
            Assert.IsTrue(visitors.IsAvailable(SnowRabbit));
            Assert.IsFalse(visitors.IsAvailable(SunSquirrel), "The squirrel exists, its sun variant does not.");
            Assert.IsFalse(visitors.IsAvailable(RussetOwl));
            var into = new int[VisitorData.Count];
            Assert.AreEqual(2, visitors.Candidates(1, into), "Spring: its own two visitors.");
            Assert.AreEqual(MeadowRabbit, into[0]);
            Assert.AreEqual(FloweryHedgehog, into[1]);
            Assert.AreEqual(1, visitors.Candidates(4, into));
            Assert.AreEqual(SnowRabbit, into[0]);
            Assert.AreEqual(3, visitors.Candidates(3, into), "Autumn's visitors do not exist: every existing one comes.");
            Assert.AreEqual(3, visitors.Candidates(2, into));

            var withOwl = NewVisitors(out _, out _, withOwl: true);
            Assert.AreEqual(1, withOwl.Candidates(3, into), "Once an autumn visitor exists, the season's come first.");
            Assert.AreEqual(RussetOwl, into[0]);
        }

        // ------------------------------------------------------------------------------------------- schedule

        [Test]
        public void Schedule_SameForTheSameDay_OneOrTwoVisits_InsideWakingHours()
        {
            var a = NewVisitors(out _, out _);
            var b = NewVisitors(out _, out _);
            var zone = TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2");
            var c = NewVisitors(out _, out _);
            c.Zone = zone;
            var arrA = new long[3];
            var arrB = new long[3];
            var whoA = new int[3];
            var whoB = new int[3];
            int ones = 0, twos = 0;
            for (int day = Today; day < Today + 400; day++)
            {
                int n = a.Schedule(day, arrA, whoA);
                Assert.AreEqual(n, b.Schedule(day, arrB, whoB));
                Assert.IsTrue(n == 1 || n == 2, "One or two visits a day.");
                if (n == 1) ones++;
                else twos++;
                var date = JournalService.DateOf(day);
                for (int i = 0; i < n; i++)
                {
                    Assert.AreEqual(arrA[i], arrB[i]);
                    Assert.AreEqual(whoA[i], whoB[i]);
                    var arrive = DateTimeOffset.FromUnixTimeMilliseconds(arrA[i]).UtcDateTime;
                    var leave = arrive.AddMilliseconds(VisitorService.VisitMs);
                    Assert.AreEqual(date, arrive.Date);
                    Assert.GreaterOrEqual(arrive.Hour, VisitorService.WakeHour);
                    Assert.LessOrEqual((leave - date).TotalHours, VisitorService.SleepHour);
                    Assert.IsTrue(a.IsAvailable(whoA[i]));
                }
                if (n == 2)
                    Assert.GreaterOrEqual(arrA[1], arrA[0] + VisitorService.VisitMs, "Two visits never overlap.");

                int m = c.Schedule(day, arrA, whoA);
                for (int i = 0; i < m; i++)
                {
                    var local = DateTimeOffset.FromUnixTimeMilliseconds(arrA[i]).ToOffset(TimeSpan.FromHours(2));
                    Assert.AreEqual(date, local.Date, "Local day in the player's zone.");
                    Assert.GreaterOrEqual(local.Hour, VisitorService.WakeHour);
                    Assert.LessOrEqual(local.Hour * 60 + local.Minute + 120, VisitorService.SleepHour * 60);
                }
            }
            Assert.Greater(ones, 120);
            Assert.Greater(twos, 120);
        }

        // ------------------------------------------------------------------------------------------- presence and touch

        [Test]
        public void Presence_Greet_GiftAndFirstTouch_ThenTheVisitorLeaves()
        {
            var visitors = NewVisitors(out var state, out var clock);
            int changes = 0;
            visitors.VisitChanged += () => changes++;
            int who = EnterFirstVisit(visitors, clock, Today);
            Assert.IsTrue(visitors.Present);
            Assert.AreEqual(VisitorData.Row(who).Key, visitors.Current.Key);
            Assert.AreEqual(VisitorData.Row(who).Variant, visitors.Current.Variant);
            Assert.AreEqual(VisitorService.VisitMs, visitors.Current.LeaveUnixMs - visitors.Current.ArriveUnixMs);
            Assert.GreaterOrEqual(changes, 1);

            Assert.IsTrue(visitors.TryGreet(out var greeting));
            Assert.AreEqual(VisitorData.Row(who).Key, greeting.VisitorKey);
            Assert.IsTrue(greeting.FirstTouch, "The first touch of this visitor ever: its sticker.");
            Assert.IsFalse(greeting.Gift.IsEmpty);
            Assert.IsFalse(visitors.Present, "Greeted: it leaves.");
            Assert.IsFalse(visitors.TryGreet(out _), "No visitor present.");
            Assert.AreEqual(1, state.Visitors.Contacts[who]);
            Assert.AreEqual(1, state.Visitors.Greetings);

            // The same visitor another day: no sticker again.
            for (int day = Today + 1; day < Today + 60; day++)
            {
                if (EnterFirstVisit(visitors, clock, day) != who)
                    continue;
                Assert.IsTrue(visitors.TryGreet(out var again));
                Assert.IsFalse(again.FirstTouch);
                return;
            }
            Assert.Fail("The visitor never came back in 60 days.");
        }

        [Test]
        public void Join_FourthContactIsGuaranteed_AndAddsTheVisitorVariant()
        {
            var visitors = NewVisitors(out var state, out var clock);
            int who = -1;
            var arrivals = new long[3];
            var whos = new int[3];
            visitors.Schedule(Today, arrivals, whos);
            who = whos[0];
            state.Visitors.Contacts[who] = 3;
            // Rolls that would refuse: the guarantee still holds at the 4th contact.
            EnterFirstVisit(visitors, clock, Today);
            Assert.IsTrue(visitors.TryGreet(out var greeting));
            Assert.IsTrue(greeting.Joined);
            Assert.IsTrue(state.Visitors.Joined(who));
            Assert.IsTrue(state.Collection.TryGet(greeting.JoinedInstanceId, out var animal));
            Assert.AreEqual(VisitorData.Row(who).Variant, animal.Variant);
            Assert.AreEqual(VisitorData.Row(who).Species, animal.Species);
            Assert.AreEqual(RarityTier.Rare, animal.Rarity);
            Assert.IsTrue(state.Collection.DiscoveredVariants.Contains(animal.Variant));
            Assert.AreEqual(1, state.Visitors.KeptCount);

            // Kept once: later touches only give their gift.
            for (int day = Today + 1; day < Today + 60; day++)
            {
                if (EnterFirstVisit(visitors, clock, day) != who)
                    continue;
                Assert.IsTrue(visitors.TryGreet(out var again));
                Assert.IsFalse(again.Joined);
                Assert.AreEqual(1, state.Collection.Count);
                return;
            }
        }

        [Test]
        public void Join_ThirtyPercentAtAFirstTouch()
        {
            int joined = 0;
            const int profiles = 2000;
            for (uint seed = 1; seed <= profiles; seed++)
            {
                var visitors = NewVisitors(out _, out var clock, seed);
                EnterFirstVisit(visitors, clock, Today);
                Assert.IsTrue(visitors.TryGreet(out var greeting));
                if (greeting.Joined)
                    joined++;
            }
            double rate = joined / (double)profiles;
            Assert.AreEqual(VisitorService.JoinChance, rate, 0.04, "30 % to join at a touch.");
        }

        [Test]
        public void Join_GuaranteedWithinFourContacts_ForEveryProfile()
        {
            for (uint seed = 1; seed <= 300; seed++)
            {
                var visitors = NewVisitors(out var state, out var clock, seed);
                int first = -1;
                for (int day = Today; day < Today + 400; day++)
                {
                    int who = EnterFirstVisit(visitors, clock, day);
                    if (first < 0)
                        first = who;
                    if (who != first)
                        continue;
                    Assert.IsTrue(visitors.TryGreet(out var greeting));
                    if (greeting.Joined)
                        break;
                    Assert.Less(state.Visitors.Contacts[first], VisitorService.GuaranteedContact);
                }
                Assert.IsTrue(state.Visitors.Joined(first), "Seed " + seed);
                Assert.LessOrEqual(state.Visitors.Contacts[first], VisitorService.GuaranteedContact);
            }
        }

        [Test]
        public void FirstOpenDay_KeepsAVisit_WhenTheDaysVisitsAreOver()
        {
            var arrivals = new long[3];
            var whos = new int[3];
            var date = JournalService.DateOf(Today);
            long latest = new DateTimeOffset(date.AddHours(20), TimeSpan.Zero).ToUnixTimeMilliseconds();
            VisitorService visitors = null;
            PlayerState state = null;
            FakeGameClock clock = null;
            long lastLeave = 0;
            for (uint seed = 1; seed < 100; seed++)
            {
                visitors = NewVisitors(out state, out clock, seed);
                int n = visitors.Schedule(Today, arrivals, whos);
                lastLeave = arrivals[n - 1] + VisitorService.VisitMs;
                if (lastLeave < latest - 120_000)
                    break;
            }
            Assert.Less(lastLeave, latest - 120_000, "A profile whose day ends before 20 h.");
            clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(lastLeave + 1000));
            visitors.Tick();
            Assert.AreEqual(Today, state.Visitors.OpenedDay);
            Assert.Greater(state.Visitors.FirstVisitUnixMs, lastLeave);
            Assert.AreEqual(state.Visitors.FirstVisitUnixMs, visitors.NextArrivalUnixMs);
            clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(state.Visitors.FirstVisitUnixMs + 1000));
            visitors.Tick();
            Assert.IsTrue(visitors.Present, "The day the visitors open keeps one visit for the player.");
        }

        // ------------------------------------------------------------------------------------------- journal and allocation

        [Test]
        public void Journal_FirstVisitorAndVisitorStays()
        {
            var visitors = NewVisitors(out var state, out var clock);
            var journal = new JournalService(state.Journal, clock, state) { Zone = TimeZoneInfo.Utc };
            using var hooks = new JournalHooks(journal, state, clock, JournalTestContent.Build(), visitors: visitors);
            var arrivals = new long[3];
            var whos = new int[3];
            visitors.Schedule(Today, arrivals, whos);
            state.Visitors.Contacts[whos[0]] = 3;
            EnterFirstVisit(visitors, clock, Today);
            Assert.IsTrue(visitors.TryGreet(out var greeting));
            Assert.IsTrue(greeting.Joined);
            Assert.IsTrue(journal.IsDone(JournalMoment.FirstVisitor));
            Assert.IsTrue(journal.HasSubject(JournalMoment.VisitorStays, (uint)whos[0] + 1));
            var names = new string[2];
            var values = new string[2];
            int stays = -1;
            for (int i = 0; i < journal.PageCount; i++)
                if (journal.PageAt(i).Key == "memory.visitor_stays")
                    stays = journal.PageAt(i).Id;
            Assert.AreEqual(1, journal.Variables(stays, names, values));
            Assert.AreEqual("visitor", names[0]);
            Assert.AreEqual("#" + VisitorData.Row(whos[0]).Key + ".name", values[0]);
        }

        [Test]
        public void ReadsAndTicks_AllocateNothing()
        {
            var visitors = NewVisitors(out var state, out var clock);
            var journal = new JournalService(state.Journal, clock, state) { Zone = TimeZoneInfo.Utc };
            for (uint s = 1; s <= 20; s++)
                journal.Add(JournalMoment.NewSpecies, s, s, "#species.rabbit.name");
            EnterFirstVisit(visitors, clock, Today);
            var names = new string[2];
            var values = new string[2];
            long sink = 0;
            void Frame()
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                visitors.Tick();
                if (visitors.Present)
                    sink += visitors.Current.ArriveUnixMs;
                sink += visitors.NextArrivalUnixMs + (visitors.IsOpen ? 1 : 0);
                for (int i = 0; i < journal.PageCount; i++)
                    sink += journal.PageAt(i).Id;
                for (int i = 0; i < journal.PendingCaptureCount; i++)
                    sink += journal.PendingCaptureAt(i).Day;
                sink += journal.Variables(journal.PageAt(0).Id, names, values) + journal.UnseenCount + journal.SeenSurpriseCount;
            }
            for (int i = 0; i < 10; i++)
                Frame();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
                Frame();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "Ticks and reads allocate nothing (" + sink + ").");
        }
    }
}
