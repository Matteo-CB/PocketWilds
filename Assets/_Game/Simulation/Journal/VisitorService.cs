#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public delegate void VisitorGreetedHandler(in VisitorGreeting greeting, int visitor);

    /// <summary>
    /// T44C « Visiteurs rares » (doc Rétention; doc Événements originaux, section 5; doc 05, sheet 25 and SUR-06).
    /// <list type="bullet">
    /// <item>Open at refuge level 8 and day 3 (doc 02 Améliorations). The day the visitors open keeps one visit for the player:
    /// when that day's visits are over, one comes a minute later (saved, so a reload keeps it).</item>
    /// <item>A visitor exists only when its species and seasonal variant are in the content. The season's visitors come; while
    /// none of them exists, every visitor that exists comes instead (today the meadow rabbit, the flowery hedgehog and the
    /// snow rabbit).</item>
    /// <item>The schedule is a pure function of the local day and the profile seed: 1 or 2 visits of 2 hours, arriving
    /// between 8 h and 20 h local so they leave by 22 h (two visits never overlap). A visit missed while away is not lost
    /// from the rules: the day simply had it.</item>
    /// <item>Touching the visitor (<see cref="TryGreet"/>): a gift (5 minutes of Light, a treat or an activity ticket), its
    /// seasonal sticker on the first touch ever, 30 % (plus the Essence « Friendly Visitors ») to join the collection,
    /// guaranteed at the 4th contact; the visitor then leaves (it was greeted).</item>
    /// <item><see cref="Tick"/> and every read allocate nothing.</item>
    /// </list>
    /// </summary>
    public sealed class VisitorService : IVisitors
    {
        public const double JoinChance = 0.30;
        public const int GuaranteedContact = 4;
        public const long VisitMs = 2L * 3600L * 1000L;
        public const int WakeHour = 8;
        public const int SleepHour = 22;
        public const double GiftLightMinutes = 5.0;
        public const int MaxVisits = 3;

        private const uint ScheduleSalt = 0x7615_17E5u;
        private const uint GreetSalt = 0x6EE7_1A60u;
        private const long FirstVisitDelayMs = 60_000L;

        private readonly PlayerState _player;
        private readonly VisitorState _state;
        private readonly ContentRegistry _content;
        private readonly IGameClock _clock;
        private readonly bool[] _available = new bool[VisitorData.Count];
        private readonly ContentId[] _biome = new ContentId[VisitorData.Count];
        private readonly int[] _candidates = new int[VisitorData.Count];

        // Today's schedule (cached until the local day changes).
        private int _day = int.MinValue;
        private long _dayStartMs;
        private long _dayEndMs;
        private int _visitCount;
        private readonly long[] _arrive = new long[MaxVisits];
        private readonly int[] _visitor = new int[MaxVisits];
        private int _current = -1;

        public VisitorService(PlayerState player, ContentRegistry content, IGameClock clock, ISaveScheduler? save = null)
        {
            _player = player;
            _state = player.Visitors;
            _content = content;
            _clock = clock;
            Save = save;
            Seed = SeedHash.Mix((uint)player.Meta.ProfileId.GetHashCode());
            for (int i = 0; i < VisitorData.Count; i++)
            {
                ref readonly var row = ref VisitorData.Row(i);
                _available[i] = content.Species.TryGetIndex(row.Species, out _) && content.Variants.TryGetIndex(row.Variant, out _);
                _biome[i] = _available[i] ? content.Species.Get(row.Species).Biome : default;
            }
        }

        public VisitorState State => _state;
        public ISaveScheduler? Save { get; set; }

        /// <summary>The schedule's seed (the profile's; tests set one).</summary>
        public uint Seed { get; set; }

        /// <summary>The player's time zone (the device's; tests set one).</summary>
        public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Local;

        /// <summary>T48B the active month map (Remote Config « events.refuge_seasons »): a visitor comes in its season; null: the default map.</summary>
        public SeasonConfig? Seasons { get; set; }

        /// <summary>
        /// T51 the player's open biomes: a visitor comes only once its species' biome is open for the player (the golden fox
        /// waits for the Forest). Null: every biome counts as open (tests).
        /// </summary>
        public BiomeProgressStore? Biomes { get; set; }

        /// <summary>Gives IsOpen (refuge level 8 and day 3); null: <see cref="ForceOpen"/> decides.</summary>
        public RefugeLevelService? Refuge { get; set; }

        public bool ForceOpen { get; set; }

        public RewardGranter? Rewards { get; set; }
        public AlbumService? Albums { get; set; }
        public MasteryService? Mastery { get; set; }
        public EconomyService? Economy { get; set; }
        public EssenceService? Essence { get; set; }
        public AchievementService? Achievements { get; set; }
        public AchievementHooks? AchievementHooks { get; set; }

        /// <summary>A ticket can be given (the activities are open); else the gift is Light.</summary>
        public Func<bool>? TicketsOpen { get; set; }

        /// <summary>The treat a visitor may bring.</summary>
        public ContentId GiftTreat { get; set; } = ContentId.FromSource("treat.clover_crunch");

        public event Action? VisitChanged;

        /// <summary>A visitor arrived (its index): analytics visitor_arrive, the arrival scene.</summary>
        public event Action<int>? Arrived;

        /// <summary>A visit ended (its visitor, greeted or not): analytics visitor_left.</summary>
        public event Action<int, bool>? Left;

        /// <summary>A visitor was touched (after the gift, the sticker and the collection were given).</summary>
        public event VisitorGreetedHandler? Greeted;

        // ------------------------------------------------------------------------------------------- IVisitors

        public bool IsOpen => Refuge != null ? Refuge.IsFeatureUnlocked(RefugeFeature.RareVisitors) : ForceOpen;

        public bool Present => _current >= 0;

        public VisitorVisit Current
        {
            get
            {
                if (_current < 0)
                    return default;
                ref readonly var row = ref VisitorData.Row(_visitor[_current]);
                return new VisitorVisit(row.Key, row.Species, row.Variant, _arrive[_current], _arrive[_current] + VisitMs);
            }
        }

        public long NextArrivalUnixMs
        {
            get
            {
                if (!IsOpen)
                    return -1;
                long now = NowMs();
                long next = -1;
                for (int i = 0; i < _visitCount; i++)
                    if (_arrive[i] > now && (next < 0 || _arrive[i] < next))
                        next = _arrive[i];
                return next;
            }
        }

        public bool TryGreet(out VisitorGreeting greeting)
        {
            Tick();
            if (_current < 0)
            {
                greeting = default;
                return false;
            }
            int slot = _current;
            int v = _visitor[slot];
            ref readonly var row = ref VisitorData.Row(v);
            if (_state.GreetedDay != _day)
            {
                _state.GreetedDay = _day;
                _state.GreetedMask = 0;
            }
            _state.GreetedMask |= (byte)(1 << slot);
            int contacts = _state.Contacts[v] < 255 ? ++_state.Contacts[v] : 255;
            bool firstTouch = contacts == 1;
            _state.Greetings++;
            var rng = new DeterministicRandom(SeedHash.Mix(Seed ^ (uint)(_day * 7 + slot), GreetSalt ^ (uint)contacts));

            var gift = DrawGift(rng.NextDouble());
            Rewards?.Grant(in gift, EconomyReason.Visitor, DiscoverySource.Debug);
            if (firstTouch)
                Albums?.OnVisitorFirstTouch(row.Season, row.Slot);

            bool joined = false;
            uint instance = 0;
            double roll = rng.NextDouble();
            if (!_state.Joined(v) && (contacts >= GuaranteedContact || roll < JoinChanceNow))
            {
                instance = AddToCollection(v, rng.NextUInt());
                joined = instance != 0;
            }
            Achievements?.Add(AchievementCounter.VisitorsTapped);
            if (joined)
                Achievements?.Add(AchievementCounter.VisitorsKept);

            greeting = new VisitorGreeting(row.Key, in gift, firstTouch, joined, instance);
            Save?.RequestSave(SaveUrgency.Priority);
            _current = -1;
            Left?.Invoke(v, true);
            Greeted?.Invoke(in greeting, v);
            VisitChanged?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------------------------------- rules

        /// <summary>The chance to join at a touch: 30 % plus the Essence « Friendly Visitors » (+5 % a rank).</summary>
        public double JoinChanceNow => JoinChance + (Essence != null ? Essence.Sum(EssenceEffect.VisitorJoin) : 0.0);

        /// <summary>The visitor exists in the game (its species and its seasonal variant are in the content).</summary>
        public bool IsAvailable(int visitor) => (uint)visitor < (uint)_available.Length && _available[visitor];

        /// <summary>T51: the visitor exists and its species' biome is open for the player (<see cref="Biomes"/>). Allocation-free.</summary>
        public bool CanCome(int visitor) => IsAvailable(visitor) && (Biomes == null || Biomes.IsUnlocked(_biome[visitor]));

        /// <summary>
        /// The visitors that may come in this season (1 to 4) into <paramref name="into"/>: the season's, else every one that can
        /// come (T51: only visitors whose species' biome is open for the player).
        /// </summary>
        public int Candidates(int season, int[] into)
        {
            int n = 0;
            for (int i = 0; i < VisitorData.Count; i++)
                if (CanCome(i) && VisitorData.Row(i).Season == season)
                    into[n++] = i;
            if (n > 0)
                return n;
            for (int i = 0; i < VisitorData.Count; i++)
                if (CanCome(i))
                    into[n++] = i;
            return n;
        }

        /// <summary>The visits of a local day (days since 2000-01-01): arrivals (UTC unix ms) and visitors; the count.</summary>
        public int Schedule(int day, long[] arrivals, int[] visitors)
        {
            var date = JournalService.DateOf(day);
            int count = Candidates(SeasonConfig.SeasonNumber(Seasons, date.Month), _candidates);
            if (count == 0)
                return 0;
            var rng = new DeterministicRandom(SeedHash.Mix(Seed ^ (uint)day, ScheduleSalt));
            int visits = rng.NextDouble() < 0.5 ? 1 : 2;
            int pick = (int)(rng.NextUInt() % (uint)count);
            if (visits == 1)
            {
                // 8 h to 20 h: the visit leaves by 22 h.
                arrivals[0] = LocalMs(date, WakeHour * 60 + (int)(rng.NextUInt() % (uint)((SleepHour - 2 - WakeHour) * 60 + 1)));
                visitors[0] = _candidates[pick];
            }
            else
            {
                // Morning (8 h to 12 h) then afternoon (14 h to 20 h): never together.
                arrivals[0] = LocalMs(date, WakeHour * 60 + (int)(rng.NextUInt() % (4u * 60u + 1u)));
                arrivals[1] = LocalMs(date, 14 * 60 + (int)(rng.NextUInt() % (6u * 60u + 1u)));
                visitors[0] = _candidates[pick];
                int other = count > 1 ? (pick + 1 + (int)(rng.NextUInt() % (uint)(count - 1))) % count : pick;
                visitors[1] = _candidates[other];
            }
            return visits;
        }

        /// <summary>The visitors' clock: the arrivals and departures of now, raising <see cref="VisitChanged"/>. Allocation-free.</summary>
        public void Tick()
        {
            if (!IsOpen)
            {
                SetCurrent(-1, false);
                return;
            }
            long now = NowMs();
            if (now < _dayStartMs || now >= _dayEndMs)
                LoadDay(DailyDay.Now(_player, _clock));
            if (_state.OpenedDay == 0)
                OpenFirst(now);
            int current = -1;
            bool greetedToday = _state.GreetedDay == _day;
            for (int i = 0; i < _visitCount; i++)
            {
                if (now < _arrive[i] || now >= _arrive[i] + VisitMs)
                    continue;
                if (greetedToday && (_state.GreetedMask & (1 << i)) != 0)
                    continue;
                current = i;
                break;
            }
            SetCurrent(current, false);
        }

        private void SetCurrent(int current, bool greeted)
        {
            if (current == _current)
                return;
            int before = _current;
            _current = current;
            if (before >= 0)
                Left?.Invoke(_visitor[before], greeted);
            if (current >= 0)
                Arrived?.Invoke(_visitor[current]);
            VisitChanged?.Invoke();
        }

        private void LoadDay(DateTimeOffset utcNow)
        {
            int day = JournalService.DayOf(utcNow, Zone);
            var date = JournalService.DateOf(day);
            _day = day;
            _dayStartMs = LocalMs(date, 0);
            _dayEndMs = LocalMs(date.AddDays(1), 0);
            _visitCount = Schedule(day, _arrive, _visitor);
            if (_state.OpenedDay == day && _state.FirstVisitUnixMs > 0 && _visitCount < MaxVisits)
            {
                _arrive[_visitCount] = _state.FirstVisitUnixMs;
                _visitor[_visitCount] = _state.FirstVisitor < VisitorData.Count ? _state.FirstVisitor : 0;
                _visitCount++;
            }
            _current = -1;
        }

        /// <summary>The visitors open: that day keeps a visit for the player (doc 05 calendar, day 3: the first visit is guaranteed).</summary>
        private void OpenFirst(long now)
        {
            _state.OpenedDay = _day;
            for (int i = 0; i < _visitCount; i++)
                if (_arrive[i] + VisitMs > now)
                {
                    Save?.RequestSave(SaveUrgency.Normal);
                    return;
                }
            var date = JournalService.DateOf(_day);
            long latest = LocalMs(date, (SleepHour - 2) * 60);
            long earliest = LocalMs(date, WakeHour * 60);
            int count = Candidates(SeasonConfig.SeasonNumber(Seasons, date.Month), _candidates);
            if (count > 0 && now + FirstVisitDelayMs <= latest && _visitCount < MaxVisits)
            {
                long at = Math.Max(now + FirstVisitDelayMs, earliest);
                _state.FirstVisitUnixMs = at;
                _state.FirstVisitor = (byte)_candidates[(int)(SeedHash.Mix(Seed ^ (uint)_day) % (uint)count)];
                _arrive[_visitCount] = at;
                _visitor[_visitCount] = _state.FirstVisitor;
                _visitCount++;
            }
            Save?.RequestSave(SaveUrgency.Normal);
        }

        private RewardBundle DrawGift(double roll)
        {
            var gift = new RewardBundle { Size = GiftSize.Small };
            if (roll >= 0.5 && roll < 0.75 && TicketsOpen != null && TicketsOpen())
                gift.Tickets = 1;
            else if (roll >= 0.75 && GiftTreat != ContentId.None)
            {
                gift.Treat = GiftTreat;
                gift.Treats = 1;
            }
            else
                gift.LightMinutes = GiftLightMinutes;
            return gift;
        }

        /// <summary>The visitor joins the collection with its seasonal variant (like a hatch: discoveries, mastery, refuge score).</summary>
        private uint AddToCollection(int v, uint seed)
        {
            ref readonly var row = ref VisitorData.Row(v);
            if (!_content.Variants.TryGet(row.Variant, out var variant))
                return 0;
            var animal = new AnimalInstance
            {
                Species = variant.Species,
                Variant = variant.Id,
                Seed = seed,
                CreatedUtcSeconds = (uint)Math.Max(0L, _clock.UtcNow.ToUnixTimeSeconds()),
                Rarity = variant.Rarity,
            };
            var collection = _player.Collection;
            animal.InstanceId = collection.Add(animal);
            bool newSpecies = collection.DiscoveredSpecies.Add(variant.Species);
            bool newVariant = collection.DiscoveredVariants.Add(variant.Id);
            _state.JoinedMask |= (ushort)(1 << v);
            Economy?.OnAnimalAdded(animal);
            Mastery?.OnAnimalObtained(variant.Species, newSpecies, newVariant);
            // « Visiteur rare ajouté à la collection »: 8 points of the refuge score (Discovery part, doc 02 section 6.1).
            Refuge?.Recompute();
            AchievementHooks?.CountObtained(in animal);
            return animal.InstanceId;
        }

        private long NowMs() => DailyDay.Now(_player, _clock).ToUnixTimeMilliseconds();

        /// <summary>A local time of a date (minutes after midnight) in UTC unix ms, daylight saving included.</summary>
        private long LocalMs(DateTime date, int minutes)
        {
            var local = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified).AddMinutes(minutes);
            return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUnixTimeMilliseconds();
        }
    }
}
