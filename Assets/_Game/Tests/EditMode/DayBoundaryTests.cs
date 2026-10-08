using System;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36, doc « 01 — Contenu des rendez-vous », section 0 (a day from the reset hour to the next, a week from Monday to
    /// Monday) and plan section 3.3: day keys with reset hours 0 and 4, daylight saving with a change at midnight (São
    /// Paulo style: local midnight skipped in spring, the last hour of Saturday twice in the fall) and a Paris-style change
    /// at 02:00 and 03:00, the next boundary always after now and exactly where the key changes, Mondays, day arithmetic,
    /// and the services' rule effective key = max(last processed, computed): a time-zone change westward or a clock moved
    /// back never makes the day go back nor gives a day twice.
    /// </summary>
    public class DayBoundaryTests
    {
        private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

        private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0) =>
            new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

        /// <summary>UTC−3, daylight +1 h from the first Sunday of November 00:00 to the third Sunday of February 00:00 (daylight time).</summary>
        private static TimeZoneInfo SaoPauloStyle()
        {
            var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 11, 1, DayOfWeek.Sunday);
            var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 2, 3, DayOfWeek.Sunday);
            var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2000, 1, 1), new DateTime(2099, 12, 31),
                TimeSpan.FromHours(1), start, end);
            return TimeZoneInfo.CreateCustomTimeZone("Test/SaoPauloStyle", TimeSpan.FromHours(-3), "Sao Paulo style", "Standard",
                "Daylight", new[] { rule });
        }

        /// <summary>UTC+1, daylight +1 h from the last Sunday of March 02:00 to the last Sunday of October 03:00 (daylight time).</summary>
        private static TimeZoneInfo ParisStyle()
        {
            var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday);
            var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday);
            var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2000, 1, 1), new DateTime(2099, 12, 31),
                TimeSpan.FromHours(1), start, end);
            return TimeZoneInfo.CreateCustomTimeZone("Test/ParisStyle", TimeSpan.FromHours(1), "Paris style", "Standard", "Daylight",
                new[] { rule });
        }

        private static TimeZoneInfo Fixed(int hours) =>
            TimeZoneInfo.CreateCustomTimeZone("Test/Fixed" + hours, TimeSpan.FromHours(hours), "Fixed " + hours, "Fixed " + hours);

        // ---------------- Reset hour ----------------

        [Test]
        public void ResetHourZero_DayChangesAtLocalMidnight()
        {
            var zone = TimeZoneInfo.Utc;
            Assert.AreEqual(20261004, DailyDay.DayKey(Utc(2026, 10, 4, 23, 59) + TimeSpan.FromSeconds(59), zone, 0));
            Assert.AreEqual(20261005, DailyDay.DayKey(Utc(2026, 10, 5, 0), zone, 0));
            Assert.AreEqual(Utc(2026, 10, 5, 0), DailyDay.NextBoundaryUtc(Utc(2026, 10, 4, 12), zone, 0));
            Assert.AreEqual(Utc(2026, 10, 6, 0), DailyDay.NextBoundaryUtc(Utc(2026, 10, 5, 0), zone, 0), "Exactly on the boundary: the next one.");
        }

        [Test]
        public void ResetHourFour_DayChangesAtFourInTheMorning()
        {
            var zone = TimeZoneInfo.Utc;
            Assert.AreEqual(20261004, DailyDay.DayKey(Utc(2026, 10, 5, 3, 59), zone, 4), "03:59 still belongs to the day before.");
            Assert.AreEqual(20261005, DailyDay.DayKey(Utc(2026, 10, 5, 4), zone, 4));
            Assert.AreEqual(Utc(2026, 10, 5, 4), DailyDay.NextBoundaryUtc(Utc(2026, 10, 5, 2), zone, 4), "Before 04:00: today's 04:00.");
            Assert.AreEqual(Utc(2026, 10, 5, 4), DailyDay.NextBoundaryUtc(Utc(2026, 10, 4, 12), zone, 4));
            Assert.AreEqual(Utc(2026, 10, 6, 4), DailyDay.NextBoundaryUtc(Utc(2026, 10, 5, 4), zone, 4));
        }

        [Test]
        public void LocalZone_ShiftsTheDay()
        {
            var tokyo = Fixed(9);
            var losAngeles = Fixed(-8);
            var instant = Utc(2026, 10, 4, 16);
            Assert.AreEqual(20261005, DailyDay.DayKey(instant, tokyo, 0), "01:00 on the 5th in UTC+9.");
            Assert.AreEqual(20261004, DailyDay.DayKey(instant, losAngeles, 0), "08:00 on the 4th in UTC−8.");
            Assert.AreEqual(Utc(2026, 10, 5, 15), DailyDay.NextBoundaryUtc(instant, tokyo, 0), "Midnight of the 6th in UTC+9.");
            Assert.AreEqual(Utc(2026, 10, 5, 12), DailyDay.NextBoundaryUtc(instant, losAngeles, 4), "04:00 of the 5th in UTC−8.");
        }

        // ---------------- Daylight saving ----------------

        [Test]
        public void SpringGap_AtMidnight_TheDayStartsAtTheFirstValidInstant()
        {
            // 1 November 2026: local 00:00 (UTC−3) jumps to 01:00 (UTC−2), so local midnight never exists.
            var zone = SaoPauloStyle();
            var boundary = DailyDay.NextBoundaryUtc(Utc(2026, 10, 31, 23), zone, 0);
            Assert.AreEqual(Utc(2026, 11, 1, 3), boundary, "The first valid local instant, 01:00 daylight time.");
            Assert.AreEqual(20261031, DailyDay.DayKey(boundary - Second, zone, 0));
            Assert.AreEqual(20261101, DailyDay.DayKey(boundary, zone, 0));
            var next = DailyDay.NextBoundaryUtc(boundary, zone, 0);
            Assert.AreEqual(Utc(2026, 11, 2, 2), next, "Midnight of the 2nd in daylight time (UTC−2).");
            Assert.AreEqual(TimeSpan.FromHours(23), next - boundary, "The spring day lasts 23 hours.");
        }

        [Test]
        public void FallOverlap_AtMidnight_SaturdayLastsTwentyFiveHours()
        {
            // 21 February 2027: at 00:00 daylight time (UTC−2) the clock goes back to Saturday 23:00 (UTC−3).
            var zone = SaoPauloStyle();
            var saturday = DailyDay.NextBoundaryUtc(Utc(2027, 2, 19, 12), zone, 0);
            Assert.AreEqual(Utc(2027, 2, 20, 2), saturday, "Saturday starts at 00:00 daylight time.");
            Assert.AreEqual(20270220, DailyDay.DayKey(Utc(2027, 2, 21, 1, 30), zone, 0), "23:30 daylight time.");
            Assert.AreEqual(20270220, DailyDay.DayKey(Utc(2027, 2, 21, 2, 30), zone, 0), "23:30 again, standard time: still Saturday.");
            var sunday = DailyDay.NextBoundaryUtc(Utc(2027, 2, 20, 23), zone, 0);
            Assert.AreEqual(Utc(2027, 2, 21, 3), sunday, "Sunday starts after the repeated hour.");
            Assert.AreEqual(20270220, DailyDay.DayKey(sunday - Second, zone, 0));
            Assert.AreEqual(20270221, DailyDay.DayKey(sunday, zone, 0));
            Assert.AreEqual(TimeSpan.FromHours(25), sunday - saturday, "The fall day lasts 25 hours.");
            Assert.AreEqual(sunday, DailyDay.NextBoundaryUtc(Utc(2027, 2, 21, 1, 30), zone, 0), "From inside the repeated hour too.");
        }

        [Test]
        public void ParisSpring_ResetInsideTheGap_MovesToTheFirstValidInstant()
        {
            // 29 March 2026: local 02:00 (UTC+1) jumps to 03:00 (UTC+2); a 02:00 reset hour falls in the gap.
            var zone = ParisStyle();
            var boundary = DailyDay.NextBoundaryUtc(Utc(2026, 3, 28, 22), zone, 2);
            Assert.AreEqual(Utc(2026, 3, 29, 1), boundary, "03:00 daylight time, the first valid instant.");
            Assert.AreEqual(20260328, DailyDay.DayKey(boundary - Second, zone, 2));
            Assert.AreEqual(20260329, DailyDay.DayKey(boundary, zone, 2));
            Assert.AreEqual(Utc(2026, 3, 28, 23), DailyDay.NextBoundaryUtc(Utc(2026, 3, 28, 12), zone, 0), "Midnight is not in the gap.");
            Assert.AreEqual(Utc(2026, 3, 29, 22), DailyDay.NextBoundaryUtc(Utc(2026, 3, 29, 12), zone, 0), "The next midnight in daylight time.");
        }

        [Test]
        public void ParisFall_ResetInsideTheRepeatedHour_TakesTheEarlierInstant()
        {
            // 25 October 2026: local 03:00 (UTC+2) goes back to 02:00 (UTC+1); 02:00 happens twice.
            var zone = ParisStyle();
            var boundary = DailyDay.NextBoundaryUtc(Utc(2026, 10, 24, 20), zone, 2);
            Assert.AreEqual(Utc(2026, 10, 25, 0), boundary, "The first 02:00, daylight time.");
            Assert.AreEqual(20261024, DailyDay.DayKey(boundary - Second, zone, 2));
            Assert.AreEqual(20261025, DailyDay.DayKey(boundary, zone, 2));
            Assert.AreEqual(20261025, DailyDay.DayKey(Utc(2026, 10, 25, 1, 30), zone, 2), "The second 02:30 stays on the new day.");
            Assert.AreEqual(Utc(2026, 10, 26, 1), DailyDay.NextBoundaryUtc(Utc(2026, 10, 25, 1, 30), zone, 2), "Then 02:00 standard time.");
            Assert.AreEqual(Utc(2026, 10, 24, 22), DailyDay.NextBoundaryUtc(Utc(2026, 10, 24, 20), zone, 0), "Midnight in daylight time.");
        }

        [Test]
        public void NextBoundary_IsAfterNow_AndExactlyWhereTheKeyChanges()
        {
            var zones = new[] { SaoPauloStyle(), ParisStyle(), TimeZoneInfo.Utc, Fixed(9), Fixed(-8) };
            var windows = new[] { Utc(2026, 10, 30, 0), Utc(2027, 2, 19, 0), Utc(2026, 3, 27, 0), Utc(2026, 10, 23, 0) };
            var step = TimeSpan.FromMinutes(17);
            foreach (var zone in zones)
            {
                foreach (int resetHour in new[] { 0, 2, 4, 6 })
                {
                    foreach (var start in windows)
                    {
                        int previous = DailyDay.DayKey(start, zone, resetHour);
                        for (var t = start; t < start + TimeSpan.FromDays(4); t += step)
                        {
                            int key = DailyDay.DayKey(t, zone, resetHour);
                            Assert.GreaterOrEqual(key, previous, "The key never goes back as time moves on (" + zone.Id + ").");
                            previous = key;

                            var boundary = DailyDay.NextBoundaryUtc(t, zone, resetHour);
                            Assert.Greater(boundary, t, "Always after now (" + zone.Id + ", " + resetHour + ").");
                            Assert.LessOrEqual(boundary - t, TimeSpan.FromHours(25), "At most one day ahead.");
                            Assert.AreEqual(key, DailyDay.DayKey(boundary - Second, zone, resetHour),
                                "Nothing changes before the boundary (" + zone.Id + ", " + resetHour + ", " + t + ").");
                            Assert.AreEqual(DailyDay.AddDays(key, 1), DailyDay.DayKey(boundary, zone, resetHour),
                                "The boundary starts the next day (" + zone.Id + ", " + resetHour + ", " + t + ").");
                        }
                    }
                }
            }
        }

        // ---------------- Weeks and day arithmetic ----------------

        [Test]
        public void WeekStart_IsTheMondayOfEveryDay()
        {
            Assert.AreEqual(20260928, DailyDay.WeekStartKey(20261004), "Sunday 4 October 2026 belongs to the week of Monday 28 September.");
            Assert.AreEqual(20261005, DailyDay.WeekStartKey(20261005), "A Monday starts its own week.");
            int key = 20260101;
            for (int i = 0; i < 2 * 366; i++)
            {
                int monday = DailyDay.WeekStartKey(key);
                Assert.AreEqual(DayOfWeek.Monday, DailyDay.ToDate(monday).DayOfWeek, "Week of " + key);
                int back = DailyDay.DaysBetween(monday, key);
                Assert.GreaterOrEqual(back, 0);
                Assert.LessOrEqual(back, 6);
                key = DailyDay.AddDays(key, 1);
            }
        }

        [Test]
        public void DaysBetween_AndAddDays_RoundTrip()
        {
            Assert.AreEqual(1, DailyDay.DaysBetween(20261231, 20270101));
            Assert.AreEqual(-1, DailyDay.DaysBetween(20270101, 20261231));
            Assert.AreEqual(2, DailyDay.DaysBetween(20280228, 20280301), "29 February 2028.");
            Assert.AreEqual(365, DailyDay.DaysBetween(20260101, 20270101));
            Assert.AreEqual(20280229, DailyDay.AddDays(20280228, 1));
            foreach (int key in new[] { 20260101, 20261004, 20261231, 20280228, 20280229 })
            {
                for (int days = -400; days <= 400; days += 7)
                {
                    int moved = DailyDay.AddDays(key, days);
                    Assert.AreEqual(days, DailyDay.DaysBetween(key, moved), key + " + " + days);
                    Assert.AreEqual(key, DailyDay.AddDays(moved, -days));
                    Assert.AreEqual(moved, DailyDay.FromDate(DailyDay.ToDate(moved)));
                }
            }
        }

        // ---------------- The services' rule: max(last processed, computed) ----------------

        [Test]
        public void WestwardZoneChange_NeverMovesTheDayBack_NorGivesItTwice()
        {
            var east = Fixed(9);
            var west = Fixed(-8);
            var start = Utc(2026, 10, 4, 16);

            // Played on the 5th in UTC+9: the 5th is processed.
            int last = DailyDay.DayKey(start, east, 0);
            Assert.AreEqual(20261005, last);
            int newDays = 0;
            int naiveChanges = 0;
            int naiveLast = last;
            var firstNewDay = DateTimeOffset.MinValue;

            // The phone moves to UTC−8 at the same instant (its local date is the 4th again), then time runs for 3 days.
            for (var t = start; t <= start + TimeSpan.FromDays(3); t += TimeSpan.FromMinutes(30))
            {
                int computed = DailyDay.DayKey(t, west, 0);
                int effective = Math.Max(last, computed);
                Assert.GreaterOrEqual(effective, last, "The effective key never decreases.");
                if (effective > last)
                {
                    newDays++;
                    if (firstNewDay == DateTimeOffset.MinValue)
                        firstNewDay = t;
                    last = effective;
                }
                if (computed != naiveLast)
                {
                    naiveChanges++;
                    naiveLast = computed;
                }
            }

            Assert.AreEqual(Utc(2026, 10, 6, 8), firstNewDay, "The next day starts at midnight of the 6th in the new zone.");
            Assert.AreEqual(2, newDays, "The 6th and the 7th, never the 4th or the 5th again.");
            Assert.AreEqual(20261007, last);
            Assert.AreEqual(4, naiveChanges, "Without the rule the 4th and the 5th would come back (shown for contrast).");
        }

        [Test]
        public void ClockMovedBack_NowStaysAtTheHighWaterMark()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            var clock = new FakeGameClock(TestUtil.T0 + TimeSpan.FromDays(3));
            state.Offline.HighWaterUtc = clock.UtcNow;
            int seen = DailyDay.DayKey(DailyDay.Now(state, clock), TimeZoneInfo.Utc, 0);
            Assert.AreEqual(20261007, seen);

            clock.Set(TestUtil.T0);
            Assert.AreEqual(state.Offline.HighWaterUtc, DailyDay.Now(state, clock), "Three days back: now is still the latest time seen.");
            Assert.AreEqual(seen, DailyDay.DayKey(DailyDay.Now(state, clock), TimeZoneInfo.Utc, 0), "Nothing to give again, nothing reset.");

            clock.Set(TestUtil.T0 + TimeSpan.FromDays(4));
            Assert.AreEqual(20261008, DailyDay.DayKey(DailyDay.Now(state, clock), TimeZoneInfo.Utc, 0), "Real time past the mark counts again.");
        }
    }
}
