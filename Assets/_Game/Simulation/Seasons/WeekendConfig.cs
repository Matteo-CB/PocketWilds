#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PocketWilds.Simulation
{
    /// <summary>T48B one special weekend of the Remote Config list: its kind and local window [start, end) (wall-clock times of the player).</summary>
    public readonly struct WeekendWindow
    {
        public readonly WeekendKind Kind;
        public readonly DateTime StartLocal;

        /// <summary>The first local instant after the window (« 2027-01-10T23:59 » ends at 2027-01-11 00:00).</summary>
        public readonly DateTime EndLocal;

        public WeekendWindow(WeekendKind kind, DateTime startLocal, DateTime endLocal)
        {
            Kind = kind;
            StartLocal = startLocal;
            EndLocal = endLocal;
        }
    }

    /// <summary>
    /// T48B the special weekends' Remote Config (doc 08, doc Événements originaux section 6): « flags.weekend_specials » (false:
    /// none), « events.weekends » (a JSON list of 0 to 60 entries {"kind","start_local","end_local"}; absent: the default rule,
    /// every weekend from Friday 00:00 to Sunday 23:59 local, kinds rotating by week from Friday 1 January 2027) and the two
    /// numbers this task adds: events.weekend.bond_multiplier (2) and events.weekend.egg_novelty_bonus (0.5). The other weekend
    /// numbers live in their own configs (activity, garden, expedition, album).
    /// </summary>
    public sealed class WeekendConfig
    {
        public const string ListKey = "events.weekends";
        public const string EnabledFlag = "flags.weekend_specials";
        public const string BondKey = "events.weekend.bond_multiplier";
        public const string EggKey = "events.weekend.egg_novelty_bonus";
        public const int MaxEntries = 60;

        /// <summary>The rotation's week 0: Friday 1 January 2027 (earlier weeks rotate too, by positive modulo).</summary>
        public static readonly DateTime RotationEpoch = new DateTime(2027, 1, 1);

        /// <summary>The default rotation, one kind per week.</summary>
        public static readonly WeekendKind[] Rotation =
            { WeekendKind.Activities, WeekendKind.Eggs, WeekendKind.Cuddles, WeekendKind.Garden, WeekendKind.Expeditions, WeekendKind.Albums };

        /// <summary>The Remote Config names of the kinds, indexed by <see cref="WeekendKind"/> (0 unused).</summary>
        public static readonly string[] KindKeys = { string.Empty, "activities", "eggs", "cuddles", "garden", "expeditions", "albums" };

        private WeekendWindow[]? _schedule;

        public bool Enabled { get; private set; } = true;

        /// <summary>Week-end des Câlins: bond points × this, every source (bounds 1 to 3).</summary>
        public double BondMultiplier { get; private set; } = 2.0;

        /// <summary>Week-end des Œufs: the Discovery eggs' novelty chance × (1 + this), at most 1 (bounds 0 to 1).</summary>
        public double EggNoveltyBonus { get; private set; } = 0.5;

        /// <summary>The Remote Config list; null: the default rule (every weekend, rotating kinds).</summary>
        public IReadOnlyList<WeekendWindow>? Schedule => _schedule;

        /// <summary>Raised when anything changed (the weekend service re-reads the weekend at once).</summary>
        public event Action? Changed;

        /// <summary>The default rule's kind of the week whose Friday is <paramref name="fridayLocal"/>.</summary>
        public static WeekendKind DefaultKind(DateTime fridayLocal)
        {
            long days = (long)Math.Floor((fridayLocal.Date - RotationEpoch).TotalDays);
            long week = days >= 0 ? days / 7 : -((-days + 6) / 7);
            int index = (int)(((week % Rotation.Length) + Rotation.Length) % Rotation.Length);
            return Rotation[index];
        }

        /// <summary>
        /// Remote Config « events.weekends »: null or empty restores the default rule; a valid list (0 to 60 entries, known kinds,
        /// local times « yyyy-MM-dd[THH:mm[:ss]] », end after start) replaces it; anything invalid is ignored whole. True when applied.
        /// </summary>
        public bool ApplyRemoteList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                if (_schedule != null)
                {
                    _schedule = null;
                    Changed?.Invoke();
                }
                return true;
            }
            if (!TryParseList(json!, out var list))
                return false;
            _schedule = list;
            Changed?.Invoke();
            return true;
        }

        public bool TryOverride(string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return false;
            switch (key)
            {
                case BondKey:
                    if (value < 1.0 || value > 3.0)
                        return false;
                    BondMultiplier = value;
                    break;
                case EggKey:
                    if (value < 0.0 || value > 1.0)
                        return false;
                    EggNoveltyBonus = value;
                    break;
                default:
                    return false;
            }
            Changed?.Invoke();
            return true;
        }

        public bool TryOverrideFlag(string key, bool value)
        {
            if (key != EnabledFlag)
                return false;
            if (Enabled != value)
            {
                Enabled = value;
                Changed?.Invoke();
            }
            return true;
        }

        public static IEnumerable<string> RemoteKeys
        {
            get
            {
                yield return BondKey;
                yield return EggKey;
            }
        }

        /// <summary>The strict list parser (see <see cref="ApplyRemoteList"/>); every entry needs its three keys and no other.</summary>
        public static bool TryParseList(string json, out WeekendWindow[] list)
        {
            list = Array.Empty<WeekendWindow>();
            if (json == null)
                return false;
            var read = new List<WeekendWindow>();
            var reader = new SeasonJson(json);
            bool ok = reader.TryReadList(() =>
            {
                if (read.Count >= MaxEntries)
                    return false;
                var kind = WeekendKind.None;
                DateTime start = default, end = default;
                bool hasStart = false, hasEnd = false;
                bool entry = reader.TryReadObject(key =>
                {
                    if (!reader.TryReadString(out string value))
                        return false;
                    switch (key)
                    {
                        case "kind":
                            int k = Array.IndexOf(KindKeys, value);
                            if (k < 1)
                                return false;
                            kind = (WeekendKind)k;
                            return true;
                        case "start_local":
                            hasStart = TryParseLocal(value, false, out start);
                            return hasStart;
                        case "end_local":
                            hasEnd = TryParseLocal(value, true, out end);
                            return hasEnd;
                        default:
                            return false;
                    }
                });
                if (!entry || kind == WeekendKind.None || !hasStart || !hasEnd || end <= start)
                    return false;
                read.Add(new WeekendWindow(kind, start, end));
                return true;
            });
            if (!ok || !reader.AtEnd)
                return false;
            list = read.ToArray();
            return true;
        }

        private static readonly string[] DateFormats = { "yyyy-MM-dd" };
        private static readonly string[] MinuteFormats = { "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm" };
        private static readonly string[] SecondFormats = { "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss" };

        /// <summary>
        /// A local wall-clock time. An end is inclusive to its precision: a date covers its whole day, a minute its 60 seconds,
        /// a second itself (the value returned is the first instant after it).
        /// </summary>
        public static bool TryParseLocal(string text, bool end, out DateTime local)
        {
            local = default;
            if (string.IsNullOrEmpty(text))
                return false;
            const DateTimeStyles style = DateTimeStyles.None;
            if (DateTime.TryParseExact(text, SecondFormats, CultureInfo.InvariantCulture, style, out local))
            {
                if (end)
                    local = local.AddSeconds(1);
            }
            else if (DateTime.TryParseExact(text, MinuteFormats, CultureInfo.InvariantCulture, style, out local))
            {
                if (end)
                    local = local.AddMinutes(1);
            }
            else if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, style, out local))
            {
                if (end)
                    local = local.AddDays(1);
            }
            else
                return false;
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return true;
        }
    }
}
