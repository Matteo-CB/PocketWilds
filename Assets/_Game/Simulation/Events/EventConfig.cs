#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Simulation
{
    /// <summary>The numbers of an event (doc 08 Remote Config, domain « events » and the event guarantees). Stable byte values: saved with each instance.</summary>
    public enum EventParam : byte
    {
        TierPoints1To10 = 0,
        TierPoints11To30 = 1,
        BonusTierPoints = 2,
        GoldPointsBonus = 3,
        SouvenirTier = 4,
        EggCost = 5,
        EggNewEvery = 6,
        EggCompleteBy = 7,
        EggSecretChance = 8,
        DuplicateRefund = 9,
        DuplicateMasteryXp = 10,
        PersonalGoal = 11,
        DailyQuests = 12,
        LongQuests = 13,
        PendingMax = 14,
        FreeSwaps = 15,
        DailyQuestReward = 16,
        LongQuestReward = 17,
        ChallengeStarReward = 18,
        WishReward = 19,
        HatchReward = 20,
        LightSecondsPerUnit = 21,
        MemoryMultiplier = 22,
        MemoryRoundTo = 23,
        ShopOutfit = 24,
        ShopDecoration = 25,
        ShopDecoration7 = 26,
        ShopDecoration8 = 27,
        ShopTickets = 28,
        ShopTicketsLimit = 29,
        ShopSticker = 30,
        ShopSeeds = 31,
        ShopSeedsLimit = 32,
        MemoryVariantBase = 33,
        MemoryFrameBase = 34,
        MemoryPoseBase = 35,
        ChallengeOutfitStars = 36,
    }

    /// <summary>One value per <see cref="EventParam"/>. Defaults are the docs'; a copy is frozen into each instance at the first entry.</summary>
    public sealed class EventParams
    {
        public const int Count = 37;

        private readonly double[] _values = new double[Count];

        public EventParams()
        {
            for (int i = 0; i < Count; i++)
                _values[i] = EventConfig.Rows[i].Default;
        }

        public double this[EventParam param]
        {
            get => _values[(int)param];
            set => _values[(int)param] = value;
        }

        public int Int(EventParam param) => (int)Math.Round(_values[(int)param]);

        public EventParams Clone()
        {
            var copy = new EventParams();
            Array.Copy(_values, copy._values, Count);
            return copy;
        }

        public void CopyFrom(EventParams other) => Array.Copy(other._values, _values, Count);
    }

    /// <summary>
    /// T48: every number of the events (doc 08: the keys of domain « events », the guarantees « guarantees.event_egg_* »,
    /// econ.event_currency.light_seconds) and their flags. <see cref="TryOverride"/> applies one value inside its bounds;
    /// <see cref="ApplyDomain"/> applies a whole domain or nothing (an invalid domain is discarded whole). A running instance
    /// keeps the numbers frozen at the player's first entry: <see cref="MergeGenerous"/> only lets a change through when it is
    /// more generous. Remote Config never writes a save, holds no money price, content id or egg pool composition.
    /// </summary>
    public sealed class EventConfig
    {
        /// <summary>One parameter: key, default, bounds, integer, and which way is generous (true: higher gives more).</summary>
        public readonly struct Row
        {
            public readonly string Key;
            public readonly double Default;
            public readonly double Min;
            public readonly double Max;
            public readonly bool Integer;
            public readonly bool HigherIsGenerous;

            public Row(string key, double value, double min, double max, bool higherIsGenerous, bool integer = true)
            {
                Key = key;
                Default = value;
                Min = min;
                Max = max;
                Integer = integer;
                HigherIsGenerous = higherIsGenerous;
            }
        }

        /// <summary>Indexed by <see cref="EventParam"/>.</summary>
        public static readonly Row[] Rows =
        {
            new Row("events.track.tier_points_1_10", 120, 30, 600, false),
            new Row("events.track.tier_points_11_30", 240, 60, 1200, false),
            new Row("events.track.bonus_tier_points", 300, 60, 1500, false),
            new Row("events.goldbook.points_bonus", 0.10, 0, 0.20, true, false),
            new Row("events.souvenir_tier", 5, 1, 30, false),
            new Row("events.egg_cost", 300, 100, 600, false),
            new Row("guarantees.event_egg_new_every", 3, 2, 5, false),
            new Row("guarantees.event_egg_complete_by", 21, 7, 21, false),
            new Row("guarantees.event_egg_secret_chance", 0.01, 0, 0.05, true, false),
            new Row("events.duplicate_refund", 60, 0, 150, true),
            new Row("events.duplicate_mastery_xp", 2, 0, 10, true),
            new Row("events.personal_goal_points", 3000, 500, 9000, false),
            new Row("events.daily_quests", 3, 0, 3, true),
            new Row("events.long_quests", 10, 0, 10, true),
            new Row("events.daily_quests_pending_max", 9, 3, 15, true),
            new Row("events.daily_quest_free_swaps", 1, 0, 3, true),
            new Row("events.currency.daily_quest", 40, 0, 200, true),
            new Row("events.currency.long_quest", 150, 0, 500, true),
            new Row("events.currency.challenge_star", 25, 0, 100, true),
            new Row("events.currency.wish", 15, 0, 100, true),
            new Row("events.currency.hatch", 2, 0, 20, true),
            new Row("econ.event_currency.light_seconds", 10, 1, 60, true),
            new Row("events.souvenir.price_multiplier", 1.25, 1, 2, false, false),
            new Row("events.souvenir.round_to", 50, 1, 100, false),
            new Row("events.shop.outfit", 900, 300, 2000, false),
            new Row("events.shop.decoration", 500, 150, 1500, false),
            new Row("events.shop.decoration_7", 650, 200, 2000, false),
            new Row("events.shop.decoration_8", 800, 250, 2500, false),
            new Row("events.shop.tickets", 120, 40, 400, false),
            new Row("events.shop.tickets_limit", 5, 1, 10, true),
            new Row("events.shop.sticker", 250, 50, 800, false),
            new Row("events.shop.seeds", 150, 50, 500, false),
            new Row("events.shop.seeds_limit", 3, 1, 10, true),
            new Row("events.souvenir.variant_base", 1200, 400, 3000, false),
            new Row("events.souvenir.frame_base", 600, 200, 2000, false),
            new Row("events.souvenir.pose_base", 800, 200, 2500, false),
            new Row("events.challenges.outfit_stars", 30, 10, 30, false),
        };

        public const string EventsFlag = "flags.events";
        public const string EventFlagPrefix = "flags.event.";
        public const string GoldBookFlag = "flags.event_goldbook";
        public const string ChestFlag = "flags.event_chest";
        public const string EggFlag = "flags.event_egg";
        public const string ShopFlag = "flags.event_shop";
        public const string MemoryShopFlag = "flags.souvenir_shop";

        private static Dictionary<string, int>? _byKey;

        public static EventConfig Default { get; } = new EventConfig();

        public EventParams Params { get; } = new EventParams();

        public bool EventsEnabled = true;
        public bool GoldBookEnabled = true;
        public bool ChestEnabled = true;
        public bool EggEnabled = true;
        public bool ShopEnabled = true;
        public bool MemoryShopEnabled = true;

        private readonly HashSet<string> _disabledKeys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>« flags.event.&lt;key&gt; » off for this event key (« event.burrows » or « burrows »).</summary>
        public bool IsKeyEnabled(string eventKey) => !_disabledKeys.Contains(ShortKey(eventKey));

        public static IEnumerable<string> RemoteKeys
        {
            get
            {
                for (int i = 0; i < Rows.Length; i++)
                    yield return Rows[i].Key;
            }
        }

        public static IEnumerable<string> RemoteFlags
        {
            get
            {
                yield return EventsFlag;
                yield return GoldBookFlag;
                yield return ChestFlag;
                yield return EggFlag;
                yield return ShopFlag;
                yield return MemoryShopFlag;
            }
        }

        public static bool TryFind(string key, out EventParam param)
        {
            if (_byKey == null)
            {
                var map = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < Rows.Length; i++)
                    map[Rows[i].Key] = i;
                _byKey = map;
            }
            bool found = _byKey.TryGetValue(key ?? string.Empty, out int index);
            param = (EventParam)index;
            return found;
        }

        /// <summary>True when the value fits its key's bounds (and is whole where it must be).</summary>
        public static bool IsValid(string key, double value, out EventParam param)
        {
            if (!TryFind(key, out param) || double.IsNaN(value) || double.IsInfinity(value))
                return false;
            var row = Rows[(int)param];
            return value >= row.Min && value <= row.Max && (!row.Integer || value == Math.Floor(value));
        }

        /// <summary>One Remote Config number: false (nothing changes) when unknown, out of bounds or not whole where it must be.</summary>
        public bool TryOverride(string key, double value)
        {
            if (!IsValid(key, value, out var param))
                return false;
            Params[param] = value;
            return true;
        }

        public bool TryOverrideFlag(string key, bool value)
        {
            switch (key)
            {
                case EventsFlag: EventsEnabled = value; return true;
                case GoldBookFlag: GoldBookEnabled = value; return true;
                case ChestFlag: ChestEnabled = value; return true;
                case EggFlag: EggEnabled = value; return true;
                case ShopFlag: ShopEnabled = value; return true;
                case MemoryShopFlag: MemoryShopEnabled = value; return true;
            }
            if (key == null || !key.StartsWith(EventFlagPrefix, StringComparison.Ordinal) || key.Length == EventFlagPrefix.Length)
                return false;
            string eventKey = key.Substring(EventFlagPrefix.Length);
            if (value)
                _disabledKeys.Remove(eventKey);
            else
                _disabledKeys.Add(eventKey);
            return true;
        }

        /// <summary>
        /// A whole « events » domain: every number and flag valid, or nothing applied (doc 08: an invalid domain is discarded
        /// whole). Unknown keys make it invalid too. True when applied.
        /// </summary>
        public bool ApplyDomain(IReadOnlyDictionary<string, double>? numbers, IReadOnlyDictionary<string, bool>? flags = null)
        {
            if (numbers != null)
                foreach (var pair in numbers)
                    if (!IsValid(pair.Key, pair.Value, out _))
                        return false;
            if (flags != null)
                foreach (var pair in flags)
                    if (!IsFlag(pair.Key))
                        return false;
            if (numbers != null)
                foreach (var pair in numbers)
                    TryOverride(pair.Key, pair.Value);
            if (flags != null)
                foreach (var pair in flags)
                    TryOverrideFlag(pair.Key, pair.Value);
            return true;
        }

        public static bool IsFlag(string key) =>
            key == EventsFlag || key == GoldBookFlag || key == ChestFlag || key == EggFlag || key == ShopFlag || key == MemoryShopFlag
            || (key != null && key.StartsWith(EventFlagPrefix, StringComparison.Ordinal) && key.Length > EventFlagPrefix.Length);

        /// <summary>True when <paramref name="proposed"/> gives at least as much as <paramref name="frozen"/> for this parameter.</summary>
        public static bool IsAtLeastAsGenerous(EventParam param, double frozen, double proposed)
        {
            var row = Rows[(int)param];
            return row.HigherIsGenerous ? proposed >= frozen : proposed <= frozen;
        }

        /// <summary>
        /// The mid-instance validator: copies into <paramref name="frozen"/> every parameter of <paramref name="proposed"/> that is
        /// at least as generous and refuses the stingier ones (listed in <paramref name="refused"/>). True when nothing was refused.
        /// </summary>
        public static bool MergeGenerous(EventParams frozen, EventParams proposed, List<EventParam>? refused = null)
        {
            bool all = true;
            for (int i = 0; i < EventParams.Count; i++)
            {
                var param = (EventParam)i;
                if (IsAtLeastAsGenerous(param, frozen[param], proposed[param]))
                    frozen[param] = proposed[param];
                else
                {
                    all = false;
                    refused?.Add(param);
                }
            }
            return all;
        }

        private static string ShortKey(string eventKey) =>
            eventKey != null && eventKey.StartsWith("event.", StringComparison.Ordinal) ? eventKey.Substring(6) : eventKey ?? string.Empty;
    }

    /// <summary>Where the app stands when a fetched « events » domain could be applied.</summary>
    public struct EventApplyMoment
    {
        public bool ColdStart;
        public double SecondsInBackground;
        public bool ActivityRunning;
        public bool Hatching;
        public bool Purchasing;
        public bool ModalOpen;
    }

    /// <summary>
    /// The apply rules of the events domain (doc 08): only at a cold start or after 30 minutes in the background, never during an
    /// activity, a hatch, a purchase or a modal. A fetched domain waits here until such a moment.
    /// </summary>
    public sealed class EventConfigGate
    {
        public const double BackgroundSeconds = 30 * 60;

        private Dictionary<string, double>? _numbers;
        private Dictionary<string, bool>? _flags;

        public bool HasPending => _numbers != null || _flags != null;

        public static bool CanApply(in EventApplyMoment moment) =>
            (moment.ColdStart || moment.SecondsInBackground >= BackgroundSeconds)
            && !moment.ActivityRunning && !moment.Hatching && !moment.Purchasing && !moment.ModalOpen;

        /// <summary>Keeps the latest fetched domain (a newer fetch replaces it).</summary>
        public void Offer(IReadOnlyDictionary<string, double>? numbers, IReadOnlyDictionary<string, bool>? flags = null)
        {
            _numbers = numbers != null ? new Dictionary<string, double>(numbers.Count) : null;
            if (numbers != null)
                foreach (var pair in numbers)
                    _numbers![pair.Key] = pair.Value;
            _flags = flags != null ? new Dictionary<string, bool>(flags.Count) : null;
            if (flags != null)
                foreach (var pair in flags)
                    _flags![pair.Key] = pair.Value;
        }

        /// <summary>
        /// Applies the pending domain when the moment allows it: into <paramref name="config"/> whole or not at all, then the running
        /// instance takes only the more generous changes (<see cref="EventService.OnConfigApplied"/>). True when applied.
        /// </summary>
        public bool TryApply(EventConfig config, in EventApplyMoment moment, EventService? events = null)
        {
            if (!HasPending || !CanApply(in moment))
                return false;
            bool applied = config.ApplyDomain(_numbers, _flags);
            _numbers = null;
            _flags = null;
            if (applied)
                events?.OnConfigApplied();
            return applied;
        }
    }
}
