#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The rendez-vous calendar (doc 01 Contenu, section 0): a day runs from the reset hour (local midnight by default) to
    /// the next one, a week from Monday to Monday. Day keys are yyyymmdd integers of the local date the reset hour shifts
    /// back. « Now » is never earlier than the latest time the profile has seen (save doc: a clock moved back changes
    /// nothing), and services keep the highest key already processed, so a time-zone hop or a rollback can neither regrant
    /// nor go back. Time-zone math runs at boundaries and on resume only, never per frame.
    /// </summary>
    public static class DailyDay
    {
        /// <summary>The later of the clock and the profile's high-water mark.</summary>
        public static DateTimeOffset Now(PlayerState state, IGameClock clock) =>
            clock.UtcNow > state.Offline.HighWaterUtc ? clock.UtcNow : state.Offline.HighWaterUtc;

        /// <summary>yyyymmdd of the local day that contains <paramref name="utc"/>, the reset hour applied.</summary>
        public static int DayKey(DateTimeOffset utc, TimeZoneInfo zone, int resetHour)
        {
            var local = TimeZoneInfo.ConvertTime(utc, zone).DateTime.AddHours(-resetHour);
            return local.Year * 10000 + local.Month * 100 + local.Day;
        }

        public static DateTime ToDate(int dayKey) => new DateTime(dayKey / 10000, dayKey / 100 % 100, dayKey % 100);

        public static int FromDate(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        /// <summary>Days from <paramref name="from"/> to <paramref name="to"/> (negative when to is earlier).</summary>
        public static int DaysBetween(int from, int to) => (int)((ToDate(to) - ToDate(from)).Ticks / TimeSpan.TicksPerDay);

        public static int AddDays(int dayKey, int days) => FromDate(ToDate(dayKey).AddDays(days));

        /// <summary>The Monday of the week holding this day.</summary>
        public static int WeekStartKey(int dayKey)
        {
            var date = ToDate(dayKey);
            int back = ((int)date.DayOfWeek + 6) % 7;
            return FromDate(date.AddDays(-back));
        }

        /// <summary>
        /// The next instant the day key changes after <paramref name="utc"/>. A reset time that falls in a daylight-saving gap
        /// moves to the first valid instant after it; an ambiguous one takes the earlier instant.
        /// </summary>
        public static DateTimeOffset NextBoundaryUtc(DateTimeOffset utc, TimeZoneInfo zone, int resetHour)
        {
            var local = TimeZoneInfo.ConvertTime(utc, zone).DateTime;
            var boundary = local.Date.AddHours(resetHour);
            if (boundary <= local)
                boundary = boundary.AddDays(1);
            boundary = DateTime.SpecifyKind(boundary, DateTimeKind.Unspecified);
            while (zone.IsInvalidTime(boundary))
                boundary = boundary.AddMinutes(1);
            var offset = zone.IsAmbiguousTime(boundary) ? MaxOffset(zone.GetAmbiguousTimeOffsets(boundary)) : zone.GetUtcOffset(boundary);
            var result = new DateTimeOffset(boundary, offset).ToUniversalTime();
            return result > utc ? result : utc.AddMinutes(1);
        }

        private static TimeSpan MaxOffset(TimeSpan[] offsets)
        {
            // The earlier instant of an ambiguous local time is the one with the larger UTC offset.
            var best = offsets[0];
            for (int i = 1; i < offsets.Length; i++)
                if (offsets[i] > best)
                    best = offsets[i];
            return best;
        }
    }
}
