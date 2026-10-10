#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T44C « Journal du refuge » (doc 01 Succès, section 6): the automatic memory album.
    /// <list type="bullet">
    /// <item>Pages are added by <see cref="Add"/> (the hooks call it): a first-time moment once, a repeatable one each time,
    /// a moment with a subject (a species, a biome, a level, an activity…) once per subject. The page keeps its date (local
    /// days since 2000-01-01), its image reference and its variables' values (never a sentence put together).</item>
    /// <item>Captures (animal portraits, refuge views) wait in a queue of 8 (an older one past 8 falls back to its thumbnail);
    /// the capture layer takes them one at a time at a calm moment and calls <see cref="CaptureStored"/>. At most 300 are
    /// kept: past that, the oldest capture of a repeatable moment is given back (<see cref="CaptureDropped"/>: its file can
    /// go) and its thumbnail stands in; first times always keep theirs.</item>
    /// <item>The world surprises seen (the « Moments » page) are listed by <see cref="AddSeenSurprise"/>.</item>
    /// <item>Reads never allocate.</item>
    /// </list>
    /// </summary>
    public sealed class JournalService : IJournal
    {
        /// <summary>A variable's value starting with it is a text key (« #species.rabbit.name »); otherwise it is shown as is.</summary>
        public const char KeyPrefix = '#';

        public const int MaxCaptures = 300;
        public const int MaxPending = 8;

        private static readonly DateTime Epoch = new DateTime(2000, 1, 1);

        private readonly JournalState _state;
        private readonly PlayerState? _player;
        private readonly IGameClock? _clock;
        private readonly int[] _pending = new int[MaxPending];
        private int _pendingCount;
        private int _stored;

        public JournalService(JournalState state, IGameClock? clock = null, PlayerState? player = null)
        {
            _state = state;
            _clock = clock;
            _player = player;
            RebuildCaptures();
        }

        public JournalState State => _state;
        public ISaveScheduler? Save { get; set; }

        /// <summary>The player's time zone for the pages' dates (the device's; tests set one).</summary>
        public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Local;

        public event Action? PageAdded;

        /// <summary>A page was written (its id, its moment): analytics journal_entry, the achievements' page counter.</summary>
        public event Action<int, int>? Written;

        /// <summary>A stored capture was given back (the 300 cap or a lost file): its page id; the capture layer deletes journal_{id}.jpg.</summary>
        public event Action<int>? CaptureDropped;

        public event Action<string>? SurpriseSeen;

        // ------------------------------------------------------------------------------------------- IJournal

        public int PageCount => _state.PageCount;

        public JournalPage PageAt(int index) => ToPage(in _state.Pages[_state.PageCount - 1 - index]);

        public int Variables(int pageId, string[] names, string[] values)
        {
            int index = IndexOfPage(pageId);
            if (index < 0)
                return 0;
            ref readonly var entry = ref _state.Pages[index];
            var variables = JournalData.Row(entry.Moment).Variables;
            int n = variables.Length;
            if (n > names.Length)
                n = names.Length;
            if (n > values.Length)
                n = values.Length;
            for (int i = 0; i < n; i++)
            {
                names[i] = variables[i];
                values[i] = (i == 0 ? entry.Value0 : entry.Value1) ?? string.Empty;
            }
            return n;
        }

        public int PendingCaptureCount => _pendingCount;

        public JournalPage PendingCaptureAt(int index) => ToPage(in _state.Pages[_pending[index]]);

        public void CaptureStored(int pageId)
        {
            int index = IndexOfPage(pageId);
            if (index < 0)
                return;
            ref var entry = ref _state.Pages[index];
            if (!entry.Pending)
                return;
            RemovePending(index);
            entry.Flags = (byte)((entry.Flags & ~JournalEntry.FlagPending & ~JournalEntry.FlagThumbnail) | JournalEntry.FlagCaptured);
            _stored++;
            if (_stored > MaxCaptures)
                DropOldestRepeatable();
            Save?.RequestSave(SaveUrgency.Normal);
        }

        public int SeenSurpriseCount => _state.SurpriseCount;

        public string SeenSurpriseAt(int index) => _state.Surprises[index];

        public int UnseenCount => _state.Unseen;

        public void MarkSeen()
        {
            if (_state.Unseen == 0)
                return;
            _state.Unseen = 0;
            Save?.RequestSave(SaveUrgency.Normal);
        }

        // ------------------------------------------------------------------------------------------- writing

        /// <summary>Captures stored on the device (300 at most, first times excepted).</summary>
        public int StoredCaptureCount => _stored;

        /// <summary>The first-time moment is written or marked done (a moment with subjects: <see cref="HasSubject"/>).</summary>
        public bool IsDone(int moment) => _state.IsFirstDone(moment);

        public bool HasSubject(int moment, uint subject) => _state.HasSubject(moment, subject);

        /// <summary>Marks a moment done without a page (an older account's past: no « first hatch » on its 50th animal).</summary>
        public void MarkDone(int moment, uint subject = 0)
        {
            if ((uint)moment >= JournalData.Count)
                return;
            if (subject != 0)
                _state.AddSubject(moment, subject);
            else
                _state.SetFirstDone(moment);
        }

        /// <summary>
        /// Writes the moment's page unless it is already written: a first-time moment once, a moment with a subject once per
        /// subject, a repeatable moment without one each time. <paramref name="imageRef"/>: the animal instance (captures), the
        /// postcard id, a content id value (thumbnails). Values: literals, or text keys prefixed by <see cref="KeyPrefix"/>.
        /// </summary>
        public bool Add(int moment, uint subject = 0, uint imageRef = 0, string? value0 = null, string? value1 = null)
        {
            if ((uint)moment >= JournalData.Count)
                return false;
            ref readonly var row = ref JournalData.Row(moment);
            if (subject != 0)
            {
                if (_state.HasSubject(moment, subject))
                    return false;
                _state.AddSubject(moment, subject);
            }
            else if (!row.Repeatable)
            {
                if (_state.IsFirstDone(moment))
                    return false;
                _state.SetFirstDone(moment);
            }
            ref var entry = ref _state.Append();
            int id = _state.NextId++;
            entry.Id = id;
            entry.Moment = (short)moment;
            entry.Day = Today();
            entry.ImageRef = imageRef;
            entry.Subject = subject;
            entry.Value0 = value0;
            entry.Value1 = value1;
            int index = _state.PageCount - 1;
            if (row.IsCapture)
            {
                // A portrait needs its animal; without one the moment's thumbnail stands in.
                if (row.Image == JournalImageKind.AnimalCapture && imageRef == 0)
                    entry.Flags = JournalEntry.FlagThumbnail;
                else
                    Enqueue(index);
            }
            _state.Unseen++;
            Save?.RequestSave(SaveUrgency.Normal);
            Written?.Invoke(id, moment);
            PageAdded?.Invoke();
            return true;
        }

        /// <summary>The capture could not be made (the animal left, the refuge is not shown): the thumbnail stands in.</summary>
        public void CaptureFailed(int pageId)
        {
            int index = IndexOfPage(pageId);
            if (index < 0 || !_state.Pages[index].Pending)
                return;
            RemovePending(index);
            _state.Pages[index].Flags = JournalEntry.FlagThumbnail;
            Save?.RequestSave(SaveUrgency.Normal);
        }

        /// <summary>A stored capture's file is missing (a new device after a cloud restore): the thumbnail stands in.</summary>
        public void CaptureLost(int pageId)
        {
            int index = IndexOfPage(pageId);
            if (index < 0)
                return;
            ref var entry = ref _state.Pages[index];
            if (!entry.Captured)
                return;
            entry.Flags = JournalEntry.FlagThumbnail;
            _stored--;
            Save?.RequestSave(SaveUrgency.Normal);
        }

        /// <summary>A world surprise seen (doc 05, the « Moments » page): kept once. The surprises call it when they exist.</summary>
        public bool AddSeenSurprise(string key)
        {
            if (string.IsNullOrEmpty(key) || _state.HasSurprise(key))
                return false;
            _state.AddSurprise(key);
            Save?.RequestSave(SaveUrgency.Normal);
            SurpriseSeen?.Invoke(key);
            return true;
        }

        /// <summary>The local day (days since 2000-01-01) of now.</summary>
        public int Today()
        {
            if (_clock == null)
                return 0;
            var now = _player != null ? DailyDay.Now(_player, _clock) : _clock.UtcNow;
            return DayOf(now, Zone);
        }

        public static int DayOf(DateTimeOffset utc, TimeZoneInfo zone) => (TimeZoneInfo.ConvertTime(utc, zone).Date - Epoch).Days;

        public static DateTime DateOf(int day) => Epoch.AddDays(day);

        // ------------------------------------------------------------------------------------------- captures

        /// <summary>The capture flags after a load: the stored count, the queue (the newest 8 waiting; older ones fall back).</summary>
        public void RebuildCaptures()
        {
            _stored = 0;
            _pendingCount = 0;
            for (int i = 0; i < _state.PageCount; i++)
            {
                ref var entry = ref _state.Pages[i];
                if (entry.Captured)
                    _stored++;
                else if (entry.Pending)
                {
                    entry.Flags = 0;
                    Enqueue(i);
                }
            }
        }

        private void Enqueue(int index)
        {
            if (_pendingCount == MaxPending)
            {
                // The queue holds 8: the oldest waiting capture gives way, its thumbnail stands in.
                _state.Pages[_pending[0]].Flags = JournalEntry.FlagThumbnail;
                Array.Copy(_pending, 1, _pending, 0, MaxPending - 1);
                _pendingCount--;
            }
            _pending[_pendingCount++] = index;
            _state.Pages[index].Flags = JournalEntry.FlagPending;
        }

        private void RemovePending(int index)
        {
            for (int i = 0; i < _pendingCount; i++)
            {
                if (_pending[i] != index)
                    continue;
                Array.Copy(_pending, i + 1, _pending, i, _pendingCount - i - 1);
                _pendingCount--;
                return;
            }
        }

        private void DropOldestRepeatable()
        {
            for (int i = 0; i < _state.PageCount; i++)
            {
                ref var entry = ref _state.Pages[i];
                if (!entry.Captured || !JournalData.Row(entry.Moment).Repeatable)
                    continue;
                entry.Flags = JournalEntry.FlagThumbnail;
                _stored--;
                CaptureDropped?.Invoke(entry.Id);
                return;
            }
        }

        // ------------------------------------------------------------------------------------------- reads

        private static JournalPage ToPage(in JournalEntry entry)
        {
            ref readonly var row = ref JournalData.Row(entry.Moment);
            return new JournalPage(entry.Id, row.Key, entry.Day, row.Image, entry.ImageRef, entry.Captured);
        }

        /// <summary>The page's index (ids rise with the index: a binary search), −1 when unknown.</summary>
        public int IndexOfPage(int pageId)
        {
            int lo = 0, hi = _state.PageCount - 1;
            var pages = _state.Pages;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int id = pages[mid].Id;
                if (id == pageId)
                    return mid;
                if (id < pageId)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }
            return -1;
        }
    }
}
