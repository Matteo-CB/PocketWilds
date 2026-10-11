#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Where an event stands for the player. Stable byte values (analytics).</summary>
    public enum EventPhase : byte
    {
        None = 0,

        /// <summary>The banner, 48 h before the start: silhouette, the real start date, 3 headline rewards.</summary>
        Announce = 1,

        Running = 2,
        Ended = 3,
    }

    /// <summary>One scheduled instance: event key, year, version and its UTC window (announce 48 h before the start).</summary>
    public readonly struct EventCalendarEntry
    {
        public const long AnnounceSeconds = 48 * 3600;

        public readonly string Key;
        public readonly int Year;
        public readonly int Version;
        public readonly long StartUnix;
        public readonly long EndUnix;

        public EventCalendarEntry(string key, int year, int version, long startUnix, long endUnix)
        {
            Key = key ?? string.Empty;
            Year = year;
            Version = Math.Max(1, version);
            StartUnix = startUnix;
            EndUnix = endUnix;
        }

        public string InstanceId => Key + "." + Year;
        public long AnnounceUnix => StartUnix - AnnounceSeconds;
        public DateTimeOffset StartUtc => DateTimeOffset.FromUnixTimeSeconds(StartUnix);
        public DateTimeOffset EndUtc => DateTimeOffset.FromUnixTimeSeconds(EndUnix);
        public bool IsValid => ContentIdFormat.IsValid(Key) && Key.StartsWith("event.", StringComparison.Ordinal) && EndUnix > StartUnix;
    }

    /// <summary>
    /// The default schedule rule (doc Événements): start the 2nd Friday of the month at 10:00 UTC, end 14 days later at 10:00 UTC,
    /// announce 48 h before. No fake countdown: the UI shows the real end date.
    /// </summary>
    public static class EventSchedule
    {
        public const int StartHourUtc = 10;
        public const int Days = 14;

        public static DateTimeOffset SecondFriday(int year, int month)
        {
            var first = new DateTimeOffset(year, month, 1, StartHourUtc, 0, 0, TimeSpan.Zero);
            int toFriday = ((int)DayOfWeek.Friday - (int)first.DayOfWeek + 7) % 7;
            return first.AddDays(toFriday + 7);
        }

        public static EventCalendarEntry Entry(string key, int year, int month, int version)
        {
            var start = SecondFriday(year, month);
            return new EventCalendarEntry(key, year, version, start.ToUnixTimeSeconds(), start.AddDays(Days).ToUnixTimeSeconds());
        }
    }

    /// <summary>
    /// The event calendar (« events.calendar »: 0 to 40 entries, no overlap). Empty by default (T48 ships no live event).
    /// Entries whose content is missing are ignored by the service, one by one.
    /// </summary>
    public sealed class EventCalendar
    {
        public const int MaxEntries = 40;

        private readonly List<EventCalendarEntry> _entries = new List<EventCalendarEntry>();

        public IReadOnlyList<EventCalendarEntry> Entries => _entries;

        /// <summary>Replaces the calendar when the whole list is valid (sorted by start, each start at or after the previous end).</summary>
        public bool TrySet(IReadOnlyList<EventCalendarEntry>? entries)
        {
            if (entries == null || entries.Count > MaxEntries)
                return false;
            var sorted = new List<EventCalendarEntry>(entries);
            sorted.Sort((a, b) => a.StartUnix.CompareTo(b.StartUnix));
            for (int i = 0; i < sorted.Count; i++)
            {
                if (!sorted[i].IsValid)
                    return false;
                // The announce of the next event may show while the previous one runs; the runs never overlap.
                if (i > 0 && sorted[i].StartUnix < sorted[i - 1].EndUnix)
                    return false;
            }
            _entries.Clear();
            _entries.AddRange(sorted);
            return true;
        }
    }

    /// <summary>The guarantee counters of an event egg (saved with the instance).</summary>
    public struct EventEggCounters
    {
        public int Opened;

        /// <summary>Eggs in a row without a new item.</summary>
        public int Misses;

        /// <summary>Bit i = item i owned.</summary>
        public byte Owned;

        public bool Secret;
    }

    /// <summary>What one event egg gave: an item (new or a duplicate), whether a guarantee forced it, and the secret on top.</summary>
    public readonly struct EventEggDraw
    {
        public readonly int Item;
        public readonly bool IsNew;
        public readonly bool Guaranteed;
        public readonly bool Secret;

        public EventEggDraw(int item, bool isNew, bool guaranteed, bool secret)
        {
            Item = item;
            IsNew = isNew;
            Guaranteed = guaranteed;
            Secret = secret;
        }
    }

    /// <summary>
    /// The event egg's draw (doc Événements, egg), pure and deterministic from its saved seed: each egg gives one of the items,
    /// equally likely; a new item is forced when <c>newEvery</c> − 1 eggs in a row gave none, or when the missing items would no
    /// longer fit before egg <c>completeBy</c>. The secret variant is a separate roll on top (outside the guarantee: it never
    /// counts as the new item and is never forced).
    /// </summary>
    public static class EventEggRules
    {
        public static EventEggDraw Draw(ref EventEggCounters counters, ref uint rng, int items, int newEvery, int completeBy, double secretChance)
        {
            items = Math.Max(1, Math.Min(8, items));
            int n = counters.Opened + 1;
            int missing = items - Owned(counters.Owned, items);
            bool secret = !counters.Secret && secretChance > 0.0 && Next01(ref rng) < secretChance;
            int item = Range(ref rng, items);
            bool isNew = (counters.Owned & (1 << item)) == 0;
            bool guaranteed = false;
            if (missing > 0 && !isNew)
            {
                bool force = counters.Misses + 1 >= Math.Max(1, newEvery) || missing > completeBy - n;
                if (force)
                {
                    item = NthMissing(counters.Owned, items, Range(ref rng, missing));
                    isNew = true;
                    guaranteed = true;
                }
            }
            counters.Opened = n;
            if (isNew)
            {
                counters.Owned |= (byte)(1 << item);
                counters.Misses = 0;
            }
            else
                counters.Misses = missing > 0 ? counters.Misses + 1 : 0;
            if (secret)
                counters.Secret = true;
            return new EventEggDraw(item, isNew, guaranteed, secret);
        }

        /// <summary>Eggs until a new item is certain (« Nouvel objet dans {n} œufs »); 0 when every item is owned.</summary>
        public static int EggsUntilNew(in EventEggCounters counters, int items, int newEvery, int completeBy)
        {
            int missing = items - Owned(counters.Owned, items);
            if (missing <= 0)
                return 0;
            int byStreak = Math.Max(1, newEvery - counters.Misses);
            // The deadline forces a new item at egg n when missing > completeBy − n.
            int byDeadline = Math.Max(1, completeBy - missing + 1 - counters.Opened);
            return Math.Min(byStreak, byDeadline);
        }

        public static int Owned(byte mask, int items)
        {
            int count = 0;
            for (int i = 0; i < items; i++)
                if ((mask & (1 << i)) != 0)
                    count++;
            return count;
        }

        /// <summary>A fresh egg seed for one profile and one instance (never 0).</summary>
        public static uint Seed(Guid profile, string instanceId)
        {
            uint h = ContentIdHash.Compute(instanceId) ^ (uint)profile.GetHashCode() ^ 0x9E3779B9u;
            h = Mix(h);
            return h == 0 ? 1u : h;
        }

        public static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        public static uint Next(ref uint state)
        {
            // xorshift32: the state is saved after every egg.
            uint x = state == 0 ? 0x6D2B79F5u : state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        public static double Next01(ref uint state) => (Next(ref state) >> 8) * (1.0 / 16777216.0);

        public static int Range(ref uint state, int count) => count <= 1 ? 0 : (int)((ulong)Next(ref state) * (ulong)count >> 32);

        private static int NthMissing(byte owned, int items, int nth)
        {
            for (int i = 0; i < items; i++)
            {
                if ((owned & (1 << i)) != 0)
                    continue;
                if (nth-- == 0)
                    return i;
            }
            return 0;
        }
    }

    /// <summary>
    /// Decision 5 of T48: an event is live only when its definition is in the build and this hook says its content is present.
    /// Bundled content is always present (T49 adds the download check when a remote group exists).
    /// </summary>
    public interface IEventContentAvailability
    {
        bool IsPresent(EventDefinition definition);
    }

    public sealed class BundledEventContent : IEventContentAvailability
    {
        public static readonly BundledEventContent Instance = new BundledEventContent();

        public bool IsPresent(EventDefinition definition) => true;
    }

    /// <summary>
    /// T49: an availability hook that can also fetch a missing content group (« Téléchargement de l'événement : {n} Mo »). The
    /// event stays out of the calendar's phases until its content is present; the hub shows the download page meanwhile.
    /// </summary>
    public interface IEventContentDownload : IEventContentAvailability
    {
        /// <summary>Bytes still to download for this event's group (0 once present or unknown).</summary>
        long DownloadBytes(EventDefinition definition);

        bool IsDownloading(EventDefinition definition);

        /// <summary>Starts the download; <paramref name="done"/> gets true once the group is present.</summary>
        void Download(EventDefinition definition, Action<bool>? done);
    }

    /// <summary>T49: each event's own assets live in one Addressables group « LiveOps_&lt;Name&gt; » with the label « liveops.&lt;name&gt; ».</summary>
    public static class EventContentGroups
    {
        public const string GroupPrefix = "LiveOps_";
        public const string LabelPrefix = "liveops.";

        /// <summary>The short name of « event.burrows »: « burrows ».</summary>
        public static string ShortName(string eventKey) =>
            eventKey != null && eventKey.StartsWith("event.", StringComparison.Ordinal) ? eventKey.Substring(6) : eventKey ?? string.Empty;

        /// <summary>« LiveOps_Burrows ».</summary>
        public static string GroupOf(string eventKey)
        {
            var name = ShortName(eventKey);
            var builder = new System.Text.StringBuilder(GroupPrefix);
            bool upper = true;
            foreach (char c in name)
            {
                if (c == '_' || c == '.')
                {
                    upper = true;
                    continue;
                }
                builder.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return builder.ToString();
        }

        /// <summary>« liveops.burrows ».</summary>
        public static string LabelOf(string eventKey) => LabelPrefix + ShortName(eventKey);

        /// <summary>Whole megabytes shown on the download page, rounded up (never 0 while something is missing).</summary>
        public static int Megabytes(long bytes) => bytes <= 0 ? 0 : (int)Math.Max(1L, (bytes + 1024L * 1024L - 1L) / (1024L * 1024L));
    }

    /// <summary>
    /// T49: the calendar shipped in the build (a fetched « events.calendar » replaces it). L'Éveil des Terriers 2027: announce
    /// 10 March 10:00 UTC, run 12 to 26 March 10:00 UTC (the 2nd Friday of March, 14 days).
    /// </summary>
    public static class DefaultEventCalendar
    {
        public const string Burrows = "event.burrows";
        public const int BurrowsYear = 2027;
        public const int BurrowsMonth = 3;

        public static EventCalendarEntry[] Entries() => new[] { EventSchedule.Entry(Burrows, BurrowsYear, BurrowsMonth, 1) };

        public static EventCalendar Create()
        {
            var calendar = new EventCalendar();
            calendar.TrySet(Entries());
            return calendar;
        }
    }
}
