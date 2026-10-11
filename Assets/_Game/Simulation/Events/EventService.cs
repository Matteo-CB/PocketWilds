#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>T48: the items a per-instance store product (Carnet d'or, Box) grants for the running event (GrantService asks).</summary>
    public interface IEventPurchases
    {
        ContentId[] ItemsFor(ProductDefinition product);
    }

    /// <summary>T48: whether a per-instance store product can be offered now (OfferService asks).</summary>
    public interface IEventOfferGate
    {
        OfferAvailability CheckEventOffer(ProductDefinition product);
    }

    public enum EventClaimStatus : byte { Claimed = 0, NoEvent = 1, NotReached = 2, AlreadyClaimed = 3, NeedsGold = 4 }

    public enum EventEggStatus : byte { Opened = 0, NoEvent = 1, NotEnoughCurrency = 2, Disabled = 3 }

    public enum EventBuyStatus : byte { Bought = 0, NoEvent = 1, NotEnoughCurrency = 2, SoldOut = 3, Owned = 4, UnknownItem = 5, Disabled = 6, NotMissing = 7 }

    public readonly struct EventSeenInfo
    {
        public readonly ContentId Event;
        public readonly EventPhase Phase;
        public EventSeenInfo(ContentId e, EventPhase phase) { Event = e; Phase = phase; }
    }

    public readonly struct EventJoinInfo
    {
        public readonly ContentId Event;
        public readonly int Day;
        public EventJoinInfo(ContentId e, int day) { Event = e; Day = day; }
    }

    public readonly struct EventTierInfo
    {
        public readonly ContentId Event;
        public readonly int Tier;
        public readonly bool Gold;
        public readonly int Points;
        public EventTierInfo(ContentId e, int tier, bool gold, int points) { Event = e; Tier = tier; Gold = gold; Points = points; }
    }

    /// <summary>What one event egg gave (the view plays it; analytics 203).</summary>
    public readonly struct EventEggResult
    {
        public readonly EventEggStatus Status;
        public readonly ContentId Event;
        public readonly int ItemIndex;
        public readonly ContentId Item;
        public readonly bool IsNew;
        public readonly bool Guaranteed;
        public readonly bool Secret;
        public readonly int Refund;
        public readonly int Opened;

        public EventEggResult(EventEggStatus status, ContentId e, int itemIndex, ContentId item, bool isNew, bool guaranteed, bool secret, int refund, int opened)
        {
            Status = status; Event = e; ItemIndex = itemIndex; Item = item; IsNew = isNew; Guaranteed = guaranteed; Secret = secret; Refund = refund; Opened = opened;
        }

        public static EventEggResult Fail(EventEggStatus status) => new EventEggResult(status, ContentId.None, -1, ContentId.None, false, false, false, 0, 0);
    }

    public readonly struct EventShopBuy
    {
        public readonly ContentId Event;
        public readonly uint Line;
        public readonly ContentId Item;
        public readonly int Cost;
        public readonly bool Memory;
        public EventShopBuy(ContentId e, uint line, ContentId item, int cost, bool memory) { Event = e; Line = line; Item = item; Cost = cost; Memory = memory; }
    }

    public readonly struct EventQuestDoneInfo
    {
        public readonly ContentId Event;
        public readonly bool Long;
        public readonly int Template;
        public EventQuestDoneInfo(ContentId e, bool isLong, int template) { Event = e; Long = isLong; Template = template; }
    }

    /// <summary>The end summary (currency to Light), shown once at the first open after the end.</summary>
    public readonly struct EventEndSummary
    {
        public readonly string InstanceId;
        public readonly ContentId Event;
        public readonly int CurrencyLeft;
        public readonly double LightSeconds;
        public readonly BigValue Light;
        public readonly int Tier;
        public readonly int Points;
        public readonly int EggItems;
        public readonly bool GoalReached;

        public EventEndSummary(string instanceId, ContentId e, int currencyLeft, double lightSeconds, BigValue light, int tier, int points, int eggItems, bool goal)
        {
            InstanceId = instanceId; Event = e; CurrencyLeft = currencyLeft; LightSeconds = lightSeconds; Light = light; Tier = tier; Points = points;
            EggItems = eggItems; GoalReached = goal;
        }
    }

    /// <summary>
    /// T48: the game services whose moments count for the event quests (the bootstrap fills it; any may be null, then its goals
    /// only move through <see cref="EventService.Report"/>).
    /// </summary>
    public sealed class EventQuestSources
    {
        public HatchService? Hatch { get; set; }
        public WishService? Wishes { get; set; }
        public ActivityRunner? Runner { get; set; }

        /// <summary>Which activities the player can play (activity gates, the featured activity's fallback, the activity of the day).</summary>
        public ActivityServices? Activities { get; set; }
        public AlbumService? Albums { get; set; }
        public ExpeditionService? Expeditions { get; set; }
        public GardenService? Garden { get; set; }
        public DailyGiftService? DailyGift { get; set; }
        public BondService? Bond { get; set; }
        public OfflineService? Offline { get; set; }
        public CosmeticService? Cosmetics { get; set; }
        public UpgradeService? Upgrades { get; set; }
        public WeeklyJournalService? WeeklyJournal { get; set; }

        /// <summary>The open biomes (the newest biome, the biome count of « visit 3 biomes »).</summary>
        public BiomeProgressStore? Biomes { get; set; }
    }

    /// <summary>
    /// T48: the events framework (doc Événements). Phases announce, run and end from the calendar and the injectable clock, never
    /// earlier than the profile's high-water mark (a clock moved back reopens nothing); join (parameters frozen); the currency from
    /// every source (activities and expeditions through <see cref="IActivityEventHooks"/>, wishes, hatches, quests, challenge
    /// stars); the 30-tier track with free and Carnet d'or claims (gold retroactive, +10 % points, never currency); the egg with its
    /// guarantees (<see cref="EventEggRules"/>); the shop and its limits; daily and long quests; the challenges; the album page's
    /// stickers; the dated Souvenir; the personal goal; the end (leftover currency to Light once, a summary for the view); the
    /// Memory Shop of a returning event. Rewards go through the existing services (granter, collection, entitlements, profile).
    /// </summary>
    public sealed class EventService : IActivityEventHooks, IEventPurchases, IEventOfferGate, IDisposable
    {
        public const int ItemOwnedGems = 100;
        public const int MainTiers = EventDefinition.TierCount;

        /// <summary>Album page values of the owed-sticker bits: track tiers (bits 0-5), challenge stars (6-8), long quests (9-11).</summary>
        public static readonly int[] TrackStickerTiers = { 1, 6, 11, 19, 23, 26 };
        public static readonly int[] StarStickers = { 9, 18, 27 };
        public static readonly int[] QuestStickers = { 4, 7, 10 };

        private readonly PlayerState _player;
        private readonly EventState _state;
        private readonly ContentRegistry _content;
        private readonly IGameClock _clock;
        private readonly ISaveScheduler? _save;

        private EventDefinition? _definition;
        private EventCalendarEntry _entry;
        private EventInstanceState? _instance;
        private EventPhase _phase;
        private ContentId _goldItem;
        private ContentId _boxItem;
        private long _refreshedUnix = long.MinValue;
        private int _seenMask;
        private string _seenFor = string.Empty;
        private uint _animalCounter;

        private HatchService? _hatch;
        private WishService? _wishes;
        private ActivityRunner? _runner;
        private AlbumService? _albumSource;
        private Action<HatchResult>? _onHatched;
        private Action<WishClaim>? _onWish;
        private ActivityEndedHandler? _onActivity;
        private AlbumGainHandler? _onSticker;

        private ActivityServices? _activitySource;
        private ExpeditionService? _expeditions;
        private GardenService? _garden;
        private DailyGiftService? _gift;
        private BondService? _bond;
        private OfflineService? _offline;
        private CosmeticService? _cosmetics;
        private UpgradeService? _upgrades;
        private WeeklyJournalService? _weekly;
        private BiomeProgressStore? _biomes;
        private ExpeditionStartedHandler? _onExpeditionStarted;
        private ExpeditionWelcomedHandler? _onExpeditionWelcomed;
        private GardenPlantedHandler? _onPlanted;
        private GardenHarvestedHandler? _onHarvested;
        private Action<int>? _onWatered;
        private Action<GiftOpening>? _onGift;
        private Action<uint, bool>? _onTouched;
        private Action<uint, ContentId, int>? _onTreat;
        private Action<OfflineReport>? _onLight;
        private AccessoryEquippedHandler? _onEquipped;
        private Action<UpgradePurchase>? _onUpgrade;
        private Action<int>? _onJournalGoal;
        private Action<ContentId>? _onBiome;
        private readonly List<ContentId> _extraBiomes = new List<ContentId>(2);

        public EventService(PlayerState player, ContentRegistry content, IGameClock clock, ISaveScheduler? save, EventConfig? config = null,
            EventCalendar? calendar = null)
        {
            _player = player;
            _state = player.Events;
            _content = content;
            _clock = clock;
            _save = save;
            Config = config ?? new EventConfig();
            Calendar = calendar ?? new EventCalendar();
        }

        public EventConfig Config { get; }
        public EventCalendar Calendar { get; }

        public EconomyService? Economy { get; set; }
        public RewardGranter? Granter { get; set; }
        public MasteryService? Mastery { get; set; }
        public AlbumService? Albums { get; set; }
        public ProfileItemService? ProfileItems { get; set; }
        public AchievementService? Achievements { get; set; }
        public AchievementHooks? AchievementHooks { get; set; }
        public RefugeLevelService? Refuge { get; set; }
        public JournalService? Journal { get; set; }
        public IEventContentAvailability Availability { get; set; } = BundledEventContent.Instance;

        private EventDefinition? _pendingDownload;
        private EventCalendarEntry _pendingEntry;

        /// <summary>T49: an event of the calendar (announce or run) whose content group is not on the device yet; null otherwise.</summary>
        public EventDefinition? PendingDownload { get { Refresh(); return _definition == null ? _pendingDownload : null; } }

        public EventCalendarEntry PendingEntry { get { Refresh(); return _pendingEntry; } }

        /// <summary>Bytes to download for <see cref="PendingDownload"/> (0 when unknown or nothing is missing).</summary>
        public long PendingDownloadBytes =>
            PendingDownload is { } def && Availability is IEventContentDownload download ? Math.Max(0L, download.DownloadBytes(def)) : 0L;

        public bool Downloading => PendingDownload is { } def && Availability is IEventContentDownload download && download.IsDownloading(def);

        /// <summary>« Télécharger »: fetches the missing group, then the calendar is read again (the announce or the run shows).</summary>
        public bool StartDownload(Action<bool>? done = null)
        {
            var def = PendingDownload;
            if (def == null || !(Availability is IEventContentDownload download) || download.IsDownloading(def))
                return false;
            download.Download(def, ok =>
            {
                Refresh(true);
                done?.Invoke(ok);
            });
            return true;
        }

        /// <summary>The rendez-vous day key (daily quests, swaps); null: the UTC date.</summary>
        public Func<int>? DayKey { get; set; }

        /// <summary>
        /// Closes quest features on top of the attached sources (tests, the simulator); null: the sources decide alone. Decorations,
        /// photo mode and breeding stay locked whatever it says (<see cref="EventQuestCatalog.AlwaysLocked"/>).
        /// </summary>
        public Func<EventFeatureGate, bool>? FeatureOpen { get; set; }

        /// <summary>The player's newest biome (« hatch 5 animals in your newest biome »); null: the attached biome store's.</summary>
        public Func<ContentId>? NewestBiome { get; set; }

        /// <summary>Whether an activity ships in this build (else the event's fallback activity is featured); null: all.</summary>
        public Func<ActivityId, bool>? ActivityShipped { get; set; }

        /// <summary>The treat a « treats » reward gives when the reward names none (the newest biome's).</summary>
        public Func<ContentId>? DefaultTreat { get; set; }

        public bool ActivitiesWeekend { get; set; }

        public event Action<EventPhase>? PhaseChanged;
        public event Action<EventSeenInfo>? Seen;
        public event Action<EventJoinInfo>? Joined;
        public event Action<int>? CurrencyChanged;
        public event Action<EventTierInfo>? TierReached;
        public event Action<EventEggResult>? EggOpened;
        public event Action<EventShopBuy>? ShopBought;
        public event Action<EventQuestDoneInfo>? QuestDone;
        public event Action<EventEndSummary>? Ended;

        /// <summary>A variant given as an animal (track, egg, Memory Shop): its variant and the animal's instance id.</summary>
        public event Action<ContentId, uint>? AnimalGiven;

        // ------------------------------------------------------------------------------------------- view API

        public EventPhase Phase { get { Refresh(); return _phase; } }
        public EventDefinition? Definition { get { Refresh(); return _definition; } }
        public EventCalendarEntry Entry { get { Refresh(); return _entry; } }

        /// <summary>The current instance once joined (null before the first entry).</summary>
        public EventInstanceState? Instance { get { Refresh(); return _instance; } }

        public bool IsJoined => Instance != null;
        public DateTimeOffset StartUtc => Entry.StartUtc;

        /// <summary>The real end date (« Jusqu'au {date} »).</summary>
        public DateTimeOffset EndUtc => Entry.EndUtc;

        public DateTimeOffset AnnounceUtc => DateTimeOffset.FromUnixTimeSeconds(Entry.AnnounceUnix);

        /// <summary>Day of the event, 1 to 14 (0 outside the run).</summary>
        public int Day => Phase == EventPhase.Running ? EventDay(NowUnix()) : 0;

        public int Currency => Instance?.Currency ?? 0;
        public int Points => Instance?.Points ?? 0;
        public int Tier => Instance?.TierReached ?? 0;
        public int TierCount => _definition != null ? TierCountOf(Instance, _definition) : MainTiers;
        public int PersonalGoal => Params.Int(EventParam.PersonalGoal);
        public bool GoalReached => Instance?.GoalReached ?? false;
        public bool GoldOwned { get { var inst = Instance; return inst != null && _player.Entitlements.Owned.Contains(_goldItem); } }
        public bool BoxOwned { get { var inst = Instance; return inst != null && _player.Entitlements.Owned.Contains(_boxItem); } }

        /// <summary>The numbers in force: the frozen ones once joined, else the live config.</summary>
        public EventParams Params => Instance?.Frozen ?? Config.Params;

        /// <summary>Cumulative points tier <paramref name="tier"/> needs (tier 0: 0).</summary>
        public int PointsForTier(int tier) => PointsFor(Params, tier);

        public static int PointsFor(EventParams p, int tier)
        {
            int p1 = p.Int(EventParam.TierPoints1To10), p2 = p.Int(EventParam.TierPoints11To30), p3 = p.Int(EventParam.BonusTierPoints);
            if (tier <= 0)
                return 0;
            if (tier <= 10)
                return tier * p1;
            if (tier <= MainTiers)
                return 10 * p1 + (tier - 10) * p2;
            return 10 * p1 + 20 * p2 + (tier - MainTiers) * p3;
        }

        public bool IsClaimed(int tier, bool gold)
        {
            var inst = Instance;
            return inst != null && tier >= 1 && tier <= 64 && ((gold ? inst.GoldClaimed : inst.FreeClaimed) & (1UL << (tier - 1))) != 0;
        }

        public bool CanClaim(int tier, bool gold)
        {
            var inst = Instance;
            if (inst == null || _definition == null || tier < 1 || tier > inst.TierReached || IsClaimed(tier, gold))
                return false;
            return !gold || (tier <= MainTiers && GoldOwned);
        }

        public int EggCost => Params.Int(EventParam.EggCost);

        /// <summary>« Nouvel objet dans {n} œufs »: eggs until a new item is certain (0 when all 7 are owned).</summary>
        public int EggsUntilNew
        {
            get
            {
                var inst = Instance;
                var p = Params;
                var counters = inst != null ? Counters(inst) : default;
                int items = _definition?.EggItems.Length ?? EventDefinition.EggItemCount;
                return EventEggRules.EggsUntilNew(in counters, items, p.Int(EventParam.EggNewEvery), p.Int(EventParam.EggCompleteBy));
            }
        }

        public bool EggItemOwned(int index) => Instance != null && index >= 0 && index < 8 && (_instance!.EggItems & (1 << index)) != 0;
        public int EggItemsOwned => Instance != null ? EventEggRules.Owned(_instance!.EggItems, _definition?.EggItems.Length ?? 7) : 0;
        public bool SecretOwned => Instance?.SecretOwned ?? false;

        /// <summary>Odds behind the info button: each item equally likely, the secret on top.</summary>
        public double ItemChance => 1.0 / Math.Max(1, _definition?.EggItems.Length ?? 7);
        public double SecretChance => Params[EventParam.EggSecretChance];

        public IReadOnlyList<EventQuestRecord> DailyQuests => (IReadOnlyList<EventQuestRecord>?)Instance?.Daily ?? Array.Empty<EventQuestRecord>();
        public IReadOnlyList<EventQuestRecord> LongQuests => (IReadOnlyList<EventQuestRecord>?)Instance?.Long ?? Array.Empty<EventQuestRecord>();

        public EventQuestTemplate? TemplateOf(in EventQuestRecord record, bool daily)
        {
            var def = Definition;
            if (def == null)
                return null;
            var pool = daily ? def.DailyQuests : def.LongQuests;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].Number == record.Template)
                    return pool[i];
            return null;
        }

        /// <summary>
        /// {activity} of the featured quests (EQ-D-03, EQ-D-26, EQ-L-03): the first featured activity the player can play, else the
        /// event's fallback activity; None outside an event.
        /// </summary>
        public ActivityId QuestActivity
        {
            get
            {
                var def = Definition;
                if (def == null)
                    return ActivityId.None;
                for (int i = 0; i < def.FeaturedActivities.Length; i++)
                    if (ActivityOpen((ActivityId)def.FeaturedActivities[i]))
                        return (ActivityId)def.FeaturedActivities[i];
                var fallback = (ActivityId)def.FallbackActivity;
                if (fallback != ActivityId.None && ActivityOpen(fallback))
                    return fallback;
                for (int i = 0; i < def.FeaturedActivities.Length; i++)
                    if (IsFeatured((ActivityId)def.FeaturedActivities[i]))
                        return (ActivityId)def.FeaturedActivities[i];
                return fallback;
            }
        }

        /// <summary>The activity a quest line names ({activity}): the activity of the day of EQ-D-22, else <see cref="QuestActivity"/>.</summary>
        public ActivityId QuestActivityOf(in EventQuestRecord quest) =>
            quest.Goal == (byte)EventGoalKind.DayActivityRounds && quest.Param > 0 ? (ActivityId)quest.Param : QuestActivity;

        public int PendingDailyCount => Instance != null ? Pending(_instance!) : 0;

        public int SwapsLeft
        {
            get
            {
                var inst = Instance;
                if (inst == null)
                    return 0;
                int free = inst.Frozen.Int(EventParam.FreeSwaps);
                return inst.SwapDay == Today() ? Math.Max(0, free - inst.SwapsUsed) : free;
            }
        }

        public int ChallengeStars(int index) => Instance != null && (uint)index < (uint)_instance!.ChallengeStars.Length ? _instance.ChallengeStars[index] : 0;
        public int TotalChallengeStars => Instance != null ? Total(_instance!.ChallengeStars) : 0;

        public bool MemoryShopOpen
        {
            get
            {
                Refresh();
                return _phase == EventPhase.Running && _definition != null && _definition.Memory.Length > 0 && _entry.Version >= 2
                       && Config.MemoryShopEnabled && Config.ShopEnabled;
            }
        }

        public bool ShopOpen { get { Refresh(); return _phase == EventPhase.Running && _definition != null && Config.ShopEnabled; } }

        // ------------------------------------------------------------------------------------------- lifecycle

        /// <summary>Everything the clock moved: ended instances convert (once), the current event and phase, today's quests.</summary>
        public void Tick() => Refresh(true);

        public void Refresh(bool force = false)
        {
            long now = NowUnix();
            if (!force && now == _refreshedUnix)
                return;
            _refreshedUnix = now;
            if (now > _state.LastSeenUnix)
                _state.LastSeenUnix = now;
            for (int i = 0; i < _state.Instances.Count; i++)
            {
                var inst = _state.Instances[i];
                if (!inst.Converted && inst.EndUnix > 0 && now >= inst.EndUnix)
                    End(inst);
            }

            EventDefinition? def = null;
            var entry = default(EventCalendarEntry);
            var phase = EventPhase.None;
            _pendingDownload = null;
            if (Config.EventsEnabled)
            {
                var entries = Calendar.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (now < e.AnnounceUnix || now >= e.EndUnix)
                        continue;
                    if (!TryLive(e, out var d))
                    {
                        // T49: in the build but its content group is missing: the hub offers the download.
                        if (_pendingDownload == null && Config.IsKeyEnabled(e.Key) && _content.Events.TryGet(ContentId.FromSource(e.Key), out var missing))
                        {
                            var done = _state.Find(e.InstanceId);
                            if (done == null || !done.Ended)
                            {
                                _pendingDownload = missing;
                                _pendingEntry = e;
                            }
                        }
                        continue;
                    }
                    var existing = _definition != null && _entry.Key == e.Key && _entry.Year == e.Year ? _instance : _state.Find(e.InstanceId);
                    if (existing != null && existing.Ended)
                        continue;
                    def = d;
                    entry = e;
                    phase = now < e.StartUnix ? EventPhase.Announce : EventPhase.Running;
                    break;
                }
            }
            bool sameEntry = def != null && _definition != null && _entry.Key == entry.Key && _entry.Year == entry.Year;
            bool changed = phase != _phase || !sameEntry;
            if (!sameEntry)
            {
                _definition = def;
                _entry = entry;
                SetInstance(def != null ? _state.Find(entry.InstanceId) : null);
            }
            _phase = phase;
            if (_instance != null && phase == EventPhase.Running)
            {
                EnsureDailyQuests(_instance, _definition!, now);
                FlushStickers(_instance, _definition!);
                SyncAbsoluteGoals(_instance, _definition!);
            }
            if (changed)
                PhaseChanged?.Invoke(phase);
        }

        /// <summary>The first entry: the parameters are frozen, the album page opens, the long quests are drawn. False when already joined or not running.</summary>
        public bool Join()
        {
            Refresh();
            if (_phase != EventPhase.Running || _definition == null || _instance != null)
                return false;
            long now = NowUnix();
            var def = _definition;
            var inst = new EventInstanceState
            {
                InstanceId = _entry.InstanceId, Key = _entry.Key, Version = _entry.Version, Year = _entry.Year, StartUnix = _entry.StartUnix,
                EndUnix = _entry.EndUnix, JoinedUnix = now, Frozen = Config.Params.Clone(),
                EggRng = EventEggRules.Seed(_player.Meta.ProfileId, _entry.InstanceId),
            };
            _state.Instances.Add(inst);
            SetInstance(inst);
            if (def.AlbumEventNumber > 0)
                Albums?.OpenEventPage(def.AlbumEventNumber);
            DrawLongQuests(inst, def);
            EnsureDailyQuests(inst, def, now);
            Achievements?.Add(AchievementCounter.EventsJoined);
            Journal?.Add(JournalMoment.EventStart, def.Id.Value, 0, Key(def.NameKey));
            Joined?.Invoke(new EventJoinInfo(def.Id, EventDay(now)));
            RequestSave(SaveUrgency.Priority);
            return true;
        }

        /// <summary>The hub or the banner was shown: event_seen once per phase and session.</summary>
        public void MarkSeen()
        {
            Refresh();
            if (_definition == null || _phase == EventPhase.None)
                return;
            string id = _entry.InstanceId;
            if (id != _seenFor)
            {
                _seenFor = id;
                _seenMask = 0;
            }
            int bit = 1 << (int)_phase;
            if ((_seenMask & bit) != 0)
                return;
            _seenMask |= bit;
            Seen?.Invoke(new EventSeenInfo(_definition.Id, _phase));
        }

        /// <summary>
        /// A fetched « events » domain was applied to <see cref="Config"/>: the running instance takes only the more generous
        /// changes (the stingier ones are refused and listed). Returns the refused parameters' count.
        /// </summary>
        public int OnConfigApplied(List<EventParam>? refused = null)
        {
            Refresh(true);
            var inst = _instance;
            if (inst == null || inst.Ended)
                return 0;
            var list = refused ?? new List<EventParam>();
            int before = list.Count;
            EventConfig.MergeGenerous(inst.Frozen, Config.Params, list);
            AdvanceTrack(inst, _definition!);
            RequestSave(SaveUrgency.Normal);
            return list.Count - before;
        }

        // ------------------------------------------------------------------------------------------- currency sources

        bool IActivityEventHooks.EventRunning => IsRunning;

        public bool IsRunning { get { Refresh(); return _phase == EventPhase.Running && _definition != null; } }

        public bool IsFeatured(ActivityId activity)
        {
            var def = Definition;
            if (def == null || activity == ActivityId.None)
                return false;
            bool anyShipped = false;
            for (int i = 0; i < def.FeaturedActivities.Length; i++)
            {
                var a = (ActivityId)def.FeaturedActivities[i];
                if (ActivityShipped != null && !ActivityShipped(a))
                    continue;
                anyShipped = true;
                if (a == activity)
                    return true;
            }
            return !anyShipped && (int)activity == def.FallbackActivity;
        }

        /// <summary>Activity rounds (20/30/40 by stars, × 1.5 featured, from the reward service) and expeditions (30/50/70/90).</summary>
        public void GrantEventCurrency(int amount, ActivityId activity) => Earn(amount, true);

        public void OnWishClaimed()
        {
            EarnParam(EventParam.WishReward);
            Report(EventGoalKind.Wishes, 1);
        }

        public void OnEggHatched()
        {
            EarnParam(EventParam.HatchReward);
            Report(EventGoalKind.HatchEggs, 1);
        }

        /// <summary>
        /// A rewarded activity game ended (not practice): rounds, stars, 3-star rounds, grooming, different activities, the activity
        /// of the day, and the quest activity's rounds (2 stars or more for EQ-D-26).
        /// </summary>
        public void OnActivityEnded(ActivityId activity, int stars)
        {
            if (!QuestsLive(out var inst, out var def))
                return;
            stars = Math.Max(0, Math.Min(3, stars));
            AdvanceOn(inst, def, EventGoalKind.ActivityRounds, 1, 0, GoalCount.Add);
            AdvanceOn(inst, def, EventGoalKind.ActivityStars, stars, 0, GoalCount.Add);
            if (stars >= 3)
                AdvanceOn(inst, def, EventGoalKind.ThreeStarRounds, 1, 0, GoalCount.Add);
            if (activity == ActivityId.Grooming)
                AdvanceOn(inst, def, EventGoalKind.GroomingRounds, 1, 0, GoalCount.Add);
            int slot = ActivityCatalog.SlotOf(activity);
            if (slot >= 0 && slot < 31)
                AdvanceOn(inst, def, EventGoalKind.DistinctActivities, 1, slot, GoalCount.Mask);
            AdvanceOn(inst, def, EventGoalKind.DayActivityRounds, 1, (int)activity, GoalCount.Match);
            if (activity != ActivityId.None && activity == QuestActivity)
            {
                AdvanceOn(inst, def, EventGoalKind.FeaturedRounds, 1, 0, GoalCount.Add);
                if (stars >= 2)
                    AdvanceOn(inst, def, EventGoalKind.FeaturedTwoStarRounds, 1, 0, GoalCount.Add);
            }
        }

        /// <summary>The camera arrived in a biome (EQ-D-23 counts different biomes since the quest was drawn).</summary>
        public void OnBiomeVisited(ContentId biome)
        {
            int index = BiomeIndex(biome);
            if (index >= 0 && index < 31 && QuestsLive(out var inst, out var def))
                AdvanceOn(inst, def, EventGoalKind.BiomeVisits, 1, index, GoalCount.Mask);
        }

        /// <summary>Feeds hatches, wishes, activity games and album stickers into the event (bootstrap; null sources are skipped).</summary>
        public void AttachSources(HatchService? hatch, WishService? wishes, ActivityRunner? runner, AlbumService? albums) =>
            AttachSources(new EventQuestSources { Hatch = hatch, Wishes = wishes, Runner = runner, Albums = albums });

        /// <summary>
        /// Subscribes to every game moment a quest counts (the bootstrap, once; null sources are skipped). Every handler is cached:
        /// nothing allocates per moment.
        /// </summary>
        public void AttachSources(EventQuestSources sources)
        {
            DetachSources();
            _hatch = sources.Hatch;
            _wishes = sources.Wishes;
            _runner = sources.Runner;
            _albumSource = sources.Albums;
            _activitySource = sources.Activities;
            _expeditions = sources.Expeditions;
            _garden = sources.Garden;
            _gift = sources.DailyGift;
            _bond = sources.Bond;
            _offline = sources.Offline;
            _cosmetics = sources.Cosmetics;
            _upgrades = sources.Upgrades;
            _weekly = sources.WeeklyJournal;
            _biomes = sources.Biomes;
            _onHatched ??= HandleHatched;
            _onWish ??= HandleWish;
            _onActivity ??= HandleActivity;
            _onSticker ??= HandleSticker;
            _onExpeditionStarted ??= HandleExpeditionStarted;
            _onExpeditionWelcomed ??= HandleExpeditionWelcomed;
            _onPlanted ??= HandlePlanted;
            _onHarvested ??= HandleHarvested;
            _onWatered ??= HandleWatered;
            _onGift ??= HandleGift;
            _onTouched ??= HandleTouched;
            _onTreat ??= HandleTreat;
            _onLight ??= HandleLight;
            _onEquipped ??= HandleEquipped;
            _onUpgrade ??= HandleUpgrade;
            _onJournalGoal ??= HandleJournalGoal;
            _onBiome ??= OnBiomeVisited;
            if (_hatch != null)
            {
                _hatch.Hatched += _onHatched;
                _hatch.CameraBiomeChanged += _onBiome;
            }
            if (_wishes != null) _wishes.Claimed += _onWish;
            if (_runner != null) _runner.Ended += _onActivity;
            if (_albumSource != null) _albumSource.StickerGained += _onSticker;
            if (_expeditions != null)
            {
                _expeditions.Started += _onExpeditionStarted;
                _expeditions.Welcomed += _onExpeditionWelcomed;
            }
            if (_garden != null)
            {
                _garden.Planted += _onPlanted;
                _garden.Harvested += _onHarvested;
                _garden.SproutWatered += _onWatered;
            }
            if (_gift != null) _gift.Opened += _onGift;
            if (_bond != null)
            {
                _bond.Touched += _onTouched;
                _bond.TreatGiven += _onTreat;
            }
            if (_offline != null) _offline.LightCollected += _onLight;
            if (_cosmetics != null) _cosmetics.Equipped += _onEquipped;
            if (_upgrades != null) _upgrades.Purchased += _onUpgrade;
            if (_weekly != null) _weekly.GoalReached += _onJournalGoal;
        }

        public void Dispose() => DetachSources();

        private void HandleHatched(HatchResult result)
        {
            if (!result.Succeeded)
                return;
            OnEggHatched();
            if (!QuestsLive(out var inst, out var def))
                return;
            if (_content.Eggs.TryGet(result.Egg, out var egg) && egg.Tier == EggTier.Focus)
                AdvanceOn(inst, def, EventGoalKind.FocusHatches, 1, 0, GoalCount.Add);
            var newest = NewestBiome != null ? NewestBiome() : _biomes?.MostAdvancedUnlocked() ?? ContentId.None;
            if (newest.IsValid && _content.Species.TryGet(result.Animal.Species, out var species) && species.Biome == newest)
                AdvanceOn(inst, def, EventGoalKind.NewestBiomeHatches, 1, 0, GoalCount.Add);
            if (result.NewVariant)
                AdvanceOn(inst, def, EventGoalKind.NewVariants, _player.Collection.DiscoveredVariants.Count, 0, GoalCount.SinceDraw);
        }

        private void HandleWish(WishClaim claim) => OnWishClaimed();

        private void HandleActivity(in ActivityGameResult result)
        {
            // T49: a challenge of the featured activity counts for the event, rewarded or practiced (no ticket needed); its new
            // event stars feed the quests there, never twice.
            bool eventChallenge = !result.Abandoned && result.Challenge >= 0 && OnChallengePlayed(result.Activity, result.Challenge, result.Stars);
            if (!result.Rewarded || result.Abandoned || result.Mode == ActivityMode.Practice)
                return;
            OnActivityEnded(result.Activity, result.Stars);
            if (!eventChallenge && result.ChallengeImproved && result.Stars > result.ChallengeStarsBefore)
                Report(EventGoalKind.ChallengeStars, result.Stars - result.ChallengeStarsBefore);
        }

        /// <summary>
        /// T49: a challenge game ended (rewarded or practice). When the activity is the running event's featured one, its stars go
        /// to <see cref="ReportChallenge"/> (the event keeps its own best per challenge). True when the event took it.
        /// </summary>
        public bool OnChallengePlayed(ActivityId activity, int challenge, int stars)
        {
            Refresh();
            var def = _definition;
            if (_phase != EventPhase.Running || def == null || challenge < 0 || challenge >= def.ChallengeLevels.Length
                || Array.IndexOf(def.FeaturedActivities, (int)activity) < 0)
                return false;
            ReportChallenge(challenge, stars);
            return true;
        }

        private void HandleSticker(in AlbumGainInfo info) => Report(EventGoalKind.StickersGained, 1);

        private void HandleExpeditionStarted(in ExpeditionStartInfo info)
        {
            Report(EventGoalKind.ExpeditionStarts, 1);
            // Every expedition lasts 1 h or more (1, 4, 8, 12 h): the nominal duration counts, not the time left after a boost.
            if (info.Duration >= ExpeditionDuration.OneHour)
                Report(EventGoalKind.LongExpeditionStarts, 1);
            if (info.Challenge >= 0 && info.ChallengeStars > info.ChallengeStarsBefore)
                Report(EventGoalKind.ChallengeStars, info.ChallengeStars - info.ChallengeStarsBefore);
        }

        private void HandleExpeditionWelcomed(in ExpeditionWelcomeInfo info) => Report(EventGoalKind.Expeditions, 1);
        private void HandlePlanted(in GardenPlantInfo info) => Report(EventGoalKind.GardenPlants, 1);
        private void HandleHarvested(in GardenHarvestInfo info) => Report(EventGoalKind.GardenHarvests, 1);
        private void HandleWatered(int plot) => Report(EventGoalKind.GardenWaterings, 1);
        private void HandleGift(GiftOpening opening) => Report(EventGoalKind.DailyGift, 1);

        private void HandleTouched(uint animal, bool counted)
        {
            if (counted)
                Report(EventGoalKind.BondTouches, 1);
        }

        private void HandleTreat(uint animal, ContentId treat, int points) => Report(EventGoalKind.TreatsGiven, 1);
        private void HandleLight(OfflineReport report) => Report(EventGoalKind.LightCollections, 1);
        private void HandleEquipped(uint animal, ContentId accessory, ContentId species, AccessorySocket socket) => Report(EventGoalKind.AccessoriesEquipped, 1);
        private void HandleUpgrade(UpgradePurchase purchase) => Report(EventGoalKind.UpgradesBought, 1);
        private void HandleJournalGoal(int goal) => Report(EventGoalKind.WeeklyJournalGoals, 1);

        private void DetachSources()
        {
            if (_hatch != null)
            {
                _hatch.Hatched -= _onHatched;
                _hatch.CameraBiomeChanged -= _onBiome;
            }
            if (_wishes != null) _wishes.Claimed -= _onWish;
            if (_runner != null) _runner.Ended -= _onActivity;
            if (_albumSource != null) _albumSource.StickerGained -= _onSticker;
            if (_expeditions != null)
            {
                _expeditions.Started -= _onExpeditionStarted;
                _expeditions.Welcomed -= _onExpeditionWelcomed;
            }
            if (_garden != null)
            {
                _garden.Planted -= _onPlanted;
                _garden.Harvested -= _onHarvested;
                _garden.SproutWatered -= _onWatered;
            }
            if (_gift != null) _gift.Opened -= _onGift;
            if (_bond != null)
            {
                _bond.Touched -= _onTouched;
                _bond.TreatGiven -= _onTreat;
            }
            if (_offline != null) _offline.LightCollected -= _onLight;
            if (_cosmetics != null) _cosmetics.Equipped -= _onEquipped;
            if (_upgrades != null) _upgrades.Purchased -= _onUpgrade;
            if (_weekly != null) _weekly.GoalReached -= _onJournalGoal;
            _hatch = null;
            _wishes = null;
            _runner = null;
            _albumSource = null;
            _activitySource = null;
            _expeditions = null;
            _garden = null;
            _gift = null;
            _bond = null;
            _offline = null;
            _cosmetics = null;
            _upgrades = null;
            _weekly = null;
            _biomes = null;
        }

        // ------------------------------------------------------------------------------------------- track

        public EventClaimStatus Claim(int tier, bool gold)
        {
            Refresh();
            if (_instance == null || _definition == null || _phase != EventPhase.Running)
                return EventClaimStatus.NoEvent;
            var status = ClaimOn(_instance, _definition, tier, gold);
            if (status == EventClaimStatus.Claimed)
                RequestSave(SaveUrgency.Priority);
            return status;
        }

        /// <summary>Claims every reached tier (free, and gold when the Carnet d'or is owned: retroactive). Returns the claims made.</summary>
        public int ClaimAll()
        {
            Refresh();
            if (_instance == null || _definition == null || _phase != EventPhase.Running)
                return 0;
            int claimed = ClaimAllOn(_instance, _definition);
            if (claimed > 0)
                RequestSave(SaveUrgency.Priority);
            return claimed;
        }

        private int ClaimAllOn(EventInstanceState inst, EventDefinition def)
        {
            int claimed = 0;
            for (int t = 1; t <= inst.TierReached; t++)
            {
                if (ClaimOn(inst, def, t, false) == EventClaimStatus.Claimed)
                    claimed++;
                if (t <= MainTiers && ClaimOn(inst, def, t, true) == EventClaimStatus.Claimed)
                    claimed++;
            }
            return claimed;
        }

        private EventClaimStatus ClaimOn(EventInstanceState inst, EventDefinition def, int tier, bool gold)
        {
            if (tier < 1 || tier > TierCountOf(inst, def) || tier > inst.TierReached)
                return EventClaimStatus.NotReached;
            ulong bit = 1UL << (tier - 1);
            var row = TierAt(def, tier);
            if (gold)
            {
                if (tier > MainTiers)
                    return EventClaimStatus.NotReached;
                if (!_player.Entitlements.Owned.Contains(GoldItemOf(inst)))
                    return EventClaimStatus.NeedsGold;
                if ((inst.GoldClaimed & bit) != 0)
                    return EventClaimStatus.AlreadyClaimed;
                inst.GoldClaimed |= bit;
                if (row != null)
                    GiveAll(inst, def, row.Gold);
                return EventClaimStatus.Claimed;
            }
            if ((inst.FreeClaimed & bit) != 0)
                return EventClaimStatus.AlreadyClaimed;
            const ulong mainMask = (1UL << MainTiers) - 1;
            bool wasComplete = (inst.FreeClaimed & mainMask) == mainMask;
            inst.FreeClaimed |= bit;
            if (row != null)
                GiveAll(inst, def, row.Free);
            if (!wasComplete && (inst.FreeClaimed & mainMask) == mainMask)
                Achievements?.Add(AchievementCounter.FreePathsComplete);
            return EventClaimStatus.Claimed;
        }

        private static EventTier? TierAt(EventDefinition def, int tier)
        {
            if (tier <= MainTiers)
                return tier - 1 < def.Tiers.Length ? def.Tiers[tier - 1] : null;
            int bonus = tier - MainTiers - 1;
            return bonus < def.BonusTiers.Length ? def.BonusTiers[bonus] : null;
        }

        private int TierCountOf(EventInstanceState? inst, EventDefinition def)
        {
            int version = inst?.Version ?? _entry.Version;
            return MainTiers + (version >= 2 ? def.BonusTiers.Length : 0);
        }

        private void AdvanceTrack(EventInstanceState inst, EventDefinition def)
        {
            int count = TierCountOf(inst, def);
            while (inst.TierReached < count && inst.Points >= PointsFor(inst.Frozen, inst.TierReached + 1))
            {
                int tier = ++inst.TierReached;
                Achievements?.Add(AchievementCounter.EventPathTiers);
                if (tier == inst.Frozen.Int(EventParam.SouvenirTier))
                    GiveSouvenir(inst, def);
                TierReached?.Invoke(new EventTierInfo(def.Id, tier, _player.Entitlements.Owned.Contains(GoldItemOf(inst)), inst.Points));
            }
            if (!inst.GoalReached && inst.Points >= inst.Frozen.Int(EventParam.PersonalGoal))
            {
                inst.GoalReached = true;
                Journal?.Add(JournalMoment.EventGoal, def.Id.Value, 0, Key(def.NameKey));
            }
            if (!inst.Ended)
                AdvanceOn(inst, def, EventGoalKind.TrackTier, inst.TierReached, 0, GoalCount.Absolute);
        }

        // ------------------------------------------------------------------------------------------- egg

        public EventEggResult OpenEgg()
        {
            Refresh();
            if (!Config.EggEnabled)
                return EventEggResult.Fail(EventEggStatus.Disabled);
            if (_phase != EventPhase.Running || _definition == null || _definition.EggItems.Length == 0 || !EnsureJoined())
                return EventEggResult.Fail(EventEggStatus.NoEvent);
            var inst = _instance!;
            var def = _definition;
            var p = inst.Frozen;
            int cost = p.Int(EventParam.EggCost);
            if (inst.Currency < cost)
                return EventEggResult.Fail(EventEggStatus.NotEnoughCurrency);
            inst.Currency -= cost;
            var counters = Counters(inst);
            uint rng = inst.EggRng;
            double secretChance = def.SecretVariant.IsValid ? p[EventParam.EggSecretChance] : 0.0;
            var draw = EventEggRules.Draw(ref counters, ref rng, def.EggItems.Length, p.Int(EventParam.EggNewEvery), p.Int(EventParam.EggCompleteBy), secretChance);
            inst.EggRng = rng;
            inst.EggsOpened = counters.Opened;
            inst.EggMisses = counters.Misses;
            inst.EggItems = counters.Owned;
            inst.SecretOwned = counters.Secret;
            var item = def.EggItems[draw.Item];
            int refund = 0;
            if (draw.IsNew)
                GiveVariant(def, item.Variant);
            else
            {
                refund = p.Int(EventParam.DuplicateRefund);
                inst.Currency += refund;
                inst.CurrencyEarned += refund;
                int xp = p.Int(EventParam.DuplicateMasteryXp);
                if (xp > 0 && _content.Variants.TryGet(item.Variant, out var variant))
                    Mastery?.AddXp(variant.Species, MasteryReason.Duplicate, xp);
            }
            if (draw.Secret)
                GiveVariant(def, def.SecretVariant);
            var result = new EventEggResult(EventEggStatus.Opened, def.Id, draw.Item, item.Variant, draw.IsNew, draw.Guaranteed, draw.Secret, refund, inst.EggsOpened);
            EggOpened?.Invoke(result);
            Report(EventGoalKind.OpenEventEggs, 1);
            SyncAbsoluteGoals(inst, def);
            CurrencyChanged?.Invoke(inst.Currency);
            RequestSave(SaveUrgency.Priority);
            return result;
        }

        private static EventEggCounters Counters(EventInstanceState inst) =>
            new EventEggCounters { Opened = inst.EggsOpened, Misses = inst.EggMisses, Owned = inst.EggItems, Secret = inst.SecretOwned };

        // ------------------------------------------------------------------------------------------- shop

        public int ShopCount => Definition?.Shop.Length ?? 0;
        public EventShopItem? ShopItem(int index) => Definition != null && (uint)index < (uint)_definition!.Shop.Length ? _definition.Shop[index] : null;
        public int ShopPrice(int index) => ShopItem(index) is { } item ? PriceOf(Params, item.Price) : 0;

        /// <summary>Purchases left on a line: −1 unlimited (the egg); a sticker line counts the page's missing stickers.</summary>
        public int ShopRemaining(int index)
        {
            var item = ShopItem(index);
            var inst = _instance;
            if (item == null)
                return 0;
            int bought = inst != null && inst.Purchases.TryGetValue(item.KeyId, out int b) ? b : 0;
            switch (item.Kind)
            {
                case EventShopKind.Egg: return -1;
                case EventShopKind.Item: return bought > 0 || IsOwned(item.Reward) ? 0 : 1;
                case EventShopKind.Tickets: return Math.Max(0, Params.Int(EventParam.ShopTicketsLimit) - bought);
                case EventShopKind.Seeds: return Math.Max(0, Params.Int(EventParam.ShopSeedsLimit) - bought);
                case EventShopKind.Sticker: return MissingStickers();
            }
            return 0;
        }

        /// <summary>Buys one line (a sticker line needs the album sticker index of the event page).</summary>
        public EventBuyStatus Buy(int index, int sticker = -1)
        {
            Refresh();
            if (!Config.ShopEnabled)
                return EventBuyStatus.Disabled;
            var item = ShopItem(index);
            if (_phase != EventPhase.Running || _definition == null)
                return EventBuyStatus.NoEvent;
            if (item == null)
                return EventBuyStatus.UnknownItem;
            if (item.Kind == EventShopKind.Egg)
            {
                var egg = OpenEgg();
                return egg.Status == EventEggStatus.Opened ? EventBuyStatus.Bought
                    : egg.Status == EventEggStatus.NotEnoughCurrency ? EventBuyStatus.NotEnoughCurrency
                    : egg.Status == EventEggStatus.Disabled ? EventBuyStatus.Disabled : EventBuyStatus.NoEvent;
            }
            if (!EnsureJoined())
                return EventBuyStatus.NoEvent;
            var inst = _instance!;
            var def = _definition;
            if (item.Kind == EventShopKind.Item && IsOwned(item.Reward))
                return EventBuyStatus.Owned;
            if (ShopRemaining(index) <= 0)
                return EventBuyStatus.SoldOut;
            int page = AlbumData.EventPage(def.AlbumEventNumber);
            if (item.Kind == EventShopKind.Sticker
                && (Albums == null || !Albums.IsOpen || page < 0 || (uint)sticker >= AlbumState.Stickers || AlbumData.Sticker(sticker).Page != page
                    || Albums.Owned(sticker)))
                return EventBuyStatus.NotMissing;
            int cost = PriceOf(inst.Frozen, item.Price);
            if (inst.Currency < cost)
                return EventBuyStatus.NotEnoughCurrency;
            inst.Currency -= cost;
            switch (item.Kind)
            {
                case EventShopKind.Sticker:
                    Albums!.GrantFromEventShop(sticker, def.AlbumEventNumber);
                    break;
                case EventShopKind.Tickets:
                    GrantBundle(new RewardBundle { Tickets = Math.Max(1, item.Reward.Amount) });
                    break;
                case EventShopKind.Seeds:
                    GrantBundle(new RewardBundle { SeedPlant = item.Reward.Item, Seeds = Math.Max(1, item.Reward.Amount) });
                    break;
                default:
                    Give(inst, def, item.Reward);
                    break;
            }
            inst.Purchases.TryGetValue(item.KeyId, out int bought);
            inst.Purchases[item.KeyId] = bought + 1;
            ShopBought?.Invoke(new EventShopBuy(def.Id, item.KeyId, item.Reward.Item, cost, false));
            Report(EventGoalKind.ShopPurchases, 1);
            CurrencyChanged?.Invoke(inst.Currency);
            RequestSave(SaveUrgency.Priority);
            return EventBuyStatus.Bought;
        }

        public int MemoryCount => Definition?.Memory.Length ?? 0;
        public EventMemoryItem? MemoryItem(int index) => Definition != null && (uint)index < (uint)_definition!.Memory.Length ? _definition.Memory[index] : null;

        /// <summary>Base × 1.25 rounded up to 50 (« events.souvenir.price_multiplier », « events.souvenir.round_to »).</summary>
        public int MemoryPrice(int index)
        {
            var item = MemoryItem(index);
            var p = Params;
            return item == null ? 0 : MemoryPriceOf(PriceOf(p, item.BasePrice), p[EventParam.MemoryMultiplier], p.Int(EventParam.MemoryRoundTo));
        }

        public static int MemoryPriceOf(int basePrice, double multiplier, int roundTo)
        {
            double raw = basePrice * multiplier;
            int step = Math.Max(1, roundTo);
            return (int)(Math.Ceiling(raw / step - 1e-9) * step);
        }

        /// <summary>« Déjà à toi »: the player already owns it.</summary>
        public bool MemoryOwned(int index) => MemoryItem(index) is { } item && IsOwned(item.Reward);

        public EventBuyStatus BuyMemory(int index)
        {
            if (!MemoryShopOpen)
                return EventBuyStatus.Disabled;
            var item = MemoryItem(index);
            if (item == null)
                return EventBuyStatus.UnknownItem;
            if (IsOwned(item.Reward))
                return EventBuyStatus.Owned;
            if (!EnsureJoined())
                return EventBuyStatus.NoEvent;
            var inst = _instance!;
            int cost = MemoryPrice(index);
            if (inst.Currency < cost)
                return EventBuyStatus.NotEnoughCurrency;
            inst.Currency -= cost;
            Give(inst, _definition!, item.Reward);
            inst.Purchases.TryGetValue(item.KeyId, out int bought);
            inst.Purchases[item.KeyId] = bought + 1;
            ShopBought?.Invoke(new EventShopBuy(_definition!.Id, item.KeyId, item.Reward.Item, cost, true));
            CurrencyChanged?.Invoke(inst.Currency);
            RequestSave(SaveUrgency.Priority);
            return EventBuyStatus.Bought;
        }

        public static int PriceOf(EventParams p, EventPriceSlot slot) => slot switch
        {
            EventPriceSlot.Egg => p.Int(EventParam.EggCost),
            EventPriceSlot.Outfit => p.Int(EventParam.ShopOutfit),
            EventPriceSlot.Decoration => p.Int(EventParam.ShopDecoration),
            EventPriceSlot.Decoration7 => p.Int(EventParam.ShopDecoration7),
            EventPriceSlot.Decoration8 => p.Int(EventParam.ShopDecoration8),
            EventPriceSlot.Tickets => p.Int(EventParam.ShopTickets),
            EventPriceSlot.Sticker => p.Int(EventParam.ShopSticker),
            EventPriceSlot.Seeds => p.Int(EventParam.ShopSeeds),
            EventPriceSlot.MemoryVariant => p.Int(EventParam.MemoryVariantBase),
            EventPriceSlot.MemoryFrame => p.Int(EventParam.MemoryFrameBase),
            EventPriceSlot.MemoryPose => p.Int(EventParam.MemoryPoseBase),
            _ => 0,
        };

        private int MissingStickers()
        {
            var def = _definition;
            int page = def != null ? AlbumData.EventPage(def.AlbumEventNumber) : -1;
            if (Albums == null || !Albums.IsOpen || page < 0)
                return 0;
            int missing = 0;
            for (int s = 0; s < AlbumData.StickersPerPage; s++)
                if (!Albums.Owned(AlbumData.StickerAt(page, s)))
                    missing++;
            return missing;
        }

        // ------------------------------------------------------------------------------------------- quests

        /// <summary>How a goal moves: by an amount, once per new bit of the record's mask, only for the record's param, to an absolute value, or since the draw.</summary>
        private enum GoalCount : byte { Add = 0, Mask = 1, Match = 2, Absolute = 3, SinceDraw = 4 }

        /// <summary>
        /// Adds progress to every open quest counting this goal (the hooks call it; the simulator and the tests too). Daily quests
        /// give 40, long quests 150; the long quests in 4th, 7th and 10th position also give their album sticker.
        /// </summary>
        public void Report(EventGoalKind kind, int amount)
        {
            if (amount > 0 && QuestsLive(out var inst, out var def))
                AdvanceOn(inst, def, kind, amount, 0, GoalCount.Add);
        }

        private bool QuestsLive(out EventInstanceState inst, out EventDefinition def)
        {
            Refresh();
            inst = _instance!;
            def = _definition!;
            return _instance != null && _definition != null && _phase == EventPhase.Running && !_instance.Ended;
        }

        private void AdvanceOn(EventInstanceState inst, EventDefinition def, EventGoalKind kind, int value, int param, GoalCount mode)
        {
            if (value <= 0 && mode != GoalCount.SinceDraw)
                return;
            int reward = 0, done = 0;
            int dailyReward = inst.Frozen.Int(EventParam.DailyQuestReward), longReward = inst.Frozen.Int(EventParam.LongQuestReward);
            for (int i = 0; i < inst.Daily.Count; i++)
            {
                var q = inst.Daily[i];
                if (q.Done || q.Goal != (byte)kind || !Move(ref q, value, param, mode))
                    continue;
                inst.Daily[i] = q;
                if (!q.Done)
                    continue;
                reward += dailyReward;
                done++;
                QuestDone?.Invoke(new EventQuestDoneInfo(def.Id, false, q.Template));
            }
            for (int i = 0; i < inst.Long.Count; i++)
            {
                var q = inst.Long[i];
                if (q.Done || q.Goal != (byte)kind || !Move(ref q, value, param, mode))
                    continue;
                inst.Long[i] = q;
                if (!q.Done)
                    continue;
                reward += longReward;
                done++;
                inst.LongDone++;
                int at = Array.IndexOf(QuestStickers, i + 1);
                if (at >= 0)
                    inst.StickersOwed |= (ushort)(1 << (9 + at));
                QuestDone?.Invoke(new EventQuestDoneInfo(def.Id, true, q.Template));
            }
            if (done == 0)
                return;
            Achievements?.Add(AchievementCounter.EventQuests, done);
            FlushStickers(inst, def);
            Earn(reward, true);
            RequestSave(SaveUrgency.Priority);
        }

        /// <summary>Moves one record; false when nothing changed.</summary>
        private static bool Move(ref EventQuestRecord q, int value, int param, GoalCount mode)
        {
            long progress;
            switch (mode)
            {
                case GoalCount.Mask:
                    int bit = 1 << param;
                    if ((q.Param & bit) != 0)
                        return false;
                    q.Param |= bit;
                    progress = q.Progress + 1L;
                    break;
                case GoalCount.Match:
                    if (q.Param != param)
                        return false;
                    progress = (long)q.Progress + value;
                    break;
                case GoalCount.Absolute:
                    progress = value;
                    break;
                case GoalCount.SinceDraw:
                    progress = (long)value - q.Param;
                    break;
                default:
                    progress = (long)q.Progress + value;
                    break;
            }
            int next = (int)Math.Max(0L, Math.Min(q.Target, progress));
            if (next <= q.Progress)
                return false;
            q.Progress = next;
            q.Done = next >= q.Target;
            return true;
        }

        /// <summary>The goals read from the game's state: the track tier, the event egg's items, the variants discovered since the draw.</summary>
        private void SyncAbsoluteGoals(EventInstanceState inst, EventDefinition def)
        {
            if (inst.Ended)
                return;
            AdvanceOn(inst, def, EventGoalKind.TrackTier, inst.TierReached, 0, GoalCount.Absolute);
            int items = EventEggRules.Owned(inst.EggItems, def.EggItems.Length) + (inst.SecretOwned ? 1 : 0);
            AdvanceOn(inst, def, EventGoalKind.EventEggItems, items, 0, GoalCount.Absolute);
            AdvanceOn(inst, def, EventGoalKind.NewVariants, _player.Collection.DiscoveredVariants.Count, 0, GoalCount.SinceDraw);
        }

        /// <summary>Replaces an open daily quest by another of its group (1 free swap a day).</summary>
        public bool Swap(int index)
        {
            Refresh();
            var inst = _instance;
            var def = _definition;
            if (inst == null || def == null || _phase != EventPhase.Running || (uint)index >= (uint)inst.Daily.Count || inst.Daily[index].Done
                || SwapsLeft <= 0)
                return false;
            var old = inst.Daily[index];
            int today = Today();
            uint salt = 101u + (uint)inst.SwapsUsed;
            var template = PickDaily(inst, def, (EventQuestGroup)old.Group, EventDay(NowUnix()), today, salt, old.Template);
            if (template == null)
                return false;
            inst.Daily[index] = NewQuest(inst, template, old.Day, salt);
            if (inst.SwapDay != today)
            {
                inst.SwapDay = today;
                inst.SwapsUsed = 0;
            }
            inst.SwapsUsed++;
            RequestSave(SaveUrgency.Normal);
            return true;
        }

        private void EnsureDailyQuests(EventInstanceState inst, EventDefinition def, long now)
        {
            int today = Today();
            if (today <= inst.QuestDay)
                return;
            inst.QuestDay = today;
            int perDay = Math.Min(3, inst.Frozen.Int(EventParam.DailyQuests));
            int max = inst.Frozen.Int(EventParam.PendingMax);
            int day = EventDay(now);
            for (int g = 0; g < perDay; g++)
            {
                if (Pending(inst) >= max)
                    break;
                var template = PickDaily(inst, def, (EventQuestGroup)g, day, today, (uint)g, -1);
                if (template != null)
                    inst.Daily.Add(NewQuest(inst, template, today, (uint)g));
            }
            RequestSave(SaveUrgency.Normal);
        }

        private EventQuestTemplate? PickDaily(EventInstanceState inst, EventDefinition def, EventQuestGroup group, int day, int today, uint salt, int exclude)
        {
            var pool = def.DailyQuests;
            int eligible = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int pick = pass == 0 ? 0 : (int)(EventEggRules.Mix(ContentIdHash.Compute(inst.InstanceId) ^ (uint)today * 2654435761u ^ salt * 40503u)
                                                 % (uint)Math.Max(1, eligible));
                if (pass == 1 && eligible == 0)
                    return null;
                for (int i = 0; i < pool.Length; i++)
                {
                    var t = pool[i];
                    if (t.Group != group || t.FromDay > day || t.Number == exclude || !Open(t) || HasPending(inst, t.Number))
                        continue;
                    if (pass == 0)
                        eligible++;
                    else if (pick-- == 0)
                        return t;
                }
            }
            return null;
        }

        /// <summary>
        /// The event's long quests, drawn once at the first entry: a seeded draw of 10 among the templates (the instance's seed);
        /// one whose feature is locked is replaced by an open one of its group further in the draw, else by any open one. They are
        /// listed in template order; positions 4, 7 and 10 carry the album stickers.
        /// </summary>
        private void DrawLongQuests(EventInstanceState inst, EventDefinition def)
        {
            var pool = def.LongQuests;
            int count = Math.Min(inst.Frozen.Int(EventParam.LongQuests), pool.Length);
            var order = new int[pool.Length];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            uint rng = EventEggRules.Mix(ContentIdHash.Compute(inst.InstanceId) ^ 0x4C4F4E47u);
            for (int i = order.Length - 1; i > 0; i--)
            {
                rng = EventEggRules.Mix(rng + (uint)i * 2654435761u);
                int j = (int)(rng % (uint)(i + 1));
                (order[i], order[j]) = (order[j], order[i]);
            }
            var used = new bool[pool.Length];
            var picked = new List<int>(count);
            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                int pick = !used[i] && Open(pool[i]) ? i : -1;
                for (int m = count; pick < 0 && m < order.Length; m++)
                    if (!used[order[m]] && pool[order[m]].Group == pool[i].Group && Open(pool[order[m]]))
                        pick = order[m];
                for (int m = count; pick < 0 && m < order.Length; m++)
                    if (!used[order[m]] && Open(pool[order[m]]))
                        pick = order[m];
                if (pick < 0)
                    continue;
                used[pick] = true;
                picked.Add(pick);
            }
            picked.Sort();
            for (int k = 0; k < picked.Count; k++)
                inst.Long.Add(NewQuest(inst, pool[picked[k]], 0, (uint)picked[k]));
        }

        /// <summary>
        /// A new quest record: EQ-D-23's target lowered to the open biomes, EQ-D-22's activity of the day (drawn among the open
        /// activities), EQ-L-19's variants at the draw.
        /// </summary>
        private EventQuestRecord NewQuest(EventInstanceState inst, EventQuestTemplate t, int day, uint salt)
        {
            var q = new EventQuestRecord { Template = (byte)t.Number, Group = (byte)t.Group, Goal = (byte)t.Goal, Target = t.Target, Day = day };
            switch (t.Goal)
            {
                case EventGoalKind.BiomeVisits:
                    if (_biomes != null)
                        q.Target = Math.Max(1, Math.Min(t.Target, _biomes.UnlockedCount()));
                    break;
                case EventGoalKind.DayActivityRounds:
                    q.Param = (int)DayActivity(inst, day, salt);
                    break;
                case EventGoalKind.NewVariants:
                    q.Param = _player.Collection.DiscoveredVariants.Count;
                    break;
            }
            return q;
        }

        /// <summary>EQ-D-22: an activity drawn among the ones the player can play (seeded by the instance, the day and the slot).</summary>
        private ActivityId DayActivity(EventInstanceState inst, int day, uint salt)
        {
            int open = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int pick = pass == 0 ? 0 : (int)(EventEggRules.Mix(ContentIdHash.Compute(inst.InstanceId) ^ (uint)day * 374761393u ^ (salt + 7u) * 668265263u)
                                                 % (uint)Math.Max(1, open));
                if (pass == 1 && open == 0)
                    break;
                for (int slot = 0; slot < ActivityCatalog.SlotCount; slot++)
                {
                    var id = ActivityCatalog.IdOfSlot(slot);
                    if (!RunnerActivity(id) || !ActivityOpen(id))
                        continue;
                    if (pass == 0)
                        open++;
                    else if (pick-- == 0)
                        return id;
                }
            }
            return QuestActivity;
        }

        private static int Pending(EventInstanceState inst)
        {
            int pending = 0;
            for (int i = 0; i < inst.Daily.Count; i++)
                if (!inst.Daily[i].Done)
                    pending++;
            return pending;
        }

        private static bool HasPending(EventInstanceState inst, int template)
        {
            for (int i = 0; i < inst.Daily.Count; i++)
                if (!inst.Daily[i].Done && inst.Daily[i].Template == template)
                    return true;
            return false;
        }

        /// <summary>Whether a template can be drawn: its feature open (sources, then <see cref="FeatureOpen"/>); never a feature the game lacks.</summary>
        private bool Open(EventQuestTemplate t)
        {
            var gate = t.Gate;
            if (gate == EventFeatureGate.None)
                return true;
            if (EventQuestCatalog.AlwaysLocked(gate) || (FeatureOpen != null && !FeatureOpen(gate)))
                return false;
            switch (gate)
            {
                case EventFeatureGate.Activities: return RunnerActivitiesOpen(1);
                case EventFeatureGate.ThreeActivities: return RunnerActivitiesOpen(3);
                case EventFeatureGate.Grooming: return ActivityOpen(ActivityId.Grooming);
                case EventFeatureGate.FeaturedActivity: return ActivityOpen(QuestActivity);
                case EventFeatureGate.Expeditions: return _expeditions?.IsOpen ?? true;
                case EventFeatureGate.Garden: return _garden?.IsOpen ?? true;
                case EventFeatureGate.Albums: return Albums?.IsOpen ?? true;
                case EventFeatureGate.FocusEgg: return _hatch == null || AnyFocusEggOffered();
                case EventFeatureGate.ChallengeStarsLeft:
                    var def = _definition;
                    return def != null && _instance != null && Total(_instance.ChallengeStars) < 3 * Math.Min(def.ChallengeLevels.Length, _instance.ChallengeStars.Length);
                case EventFeatureGate.TreatsOwned: return TreatsOwned() >= t.Target;
                case EventFeatureGate.AccessoryOwned: return AnyAccessoryOwned();
                default: return true;
            }
        }

        /// <summary>The player can play this activity: shipped, enabled and open (the attached activities), else shipped in the catalog.</summary>
        private bool ActivityOpen(ActivityId activity)
        {
            if (activity == ActivityId.None || (ActivityShipped != null && !ActivityShipped(activity)))
                return false;
            if (_activitySource != null)
                return _activitySource.IsAvailable(activity);
            return ActivityCatalog.TryGet(activity, out var definition) && definition.ShippedInThisBuild;
        }

        /// <summary>An activity played through the runner (rounds, stars): not a meta activity (expeditions, garden), not the test one.</summary>
        private static bool RunnerActivity(ActivityId activity) =>
            activity != ActivityId.None && activity != ActivityId.Test && ActivityCatalog.TryGet(activity, out var definition) && !definition.Meta;

        private bool RunnerActivitiesOpen(int needed)
        {
            int open = 0;
            for (int slot = 0; slot < ActivityCatalog.SlotCount && open < needed; slot++)
            {
                var id = ActivityCatalog.IdOfSlot(slot);
                if (RunnerActivity(id) && ActivityOpen(id))
                    open++;
            }
            return open >= needed;
        }

        private bool AnyFocusEggOffered()
        {
            var eggs = _content.Eggs;
            for (int i = 0; i < eggs.Count; i++)
                if (eggs[i].Tier == EggTier.Focus && _hatch!.IsOffered(eggs[i].Id))
                    return true;
            return false;
        }

        private int TreatsOwned()
        {
            int total = 0;
            foreach (var pair in _player.Progression.Treats)
                total += Math.Max(0, pair.Value);
            return total;
        }

        private bool AnyAccessoryOwned()
        {
            var accessories = _content.Shop.Accessories;
            for (int i = 0; i < accessories.Count; i++)
                if (_player.Entitlements.Owned.Contains(accessories[i].Id))
                    return true;
            return false;
        }

        /// <summary>A biome's bit in the visit masks: its place in the content's biome table (a biome the table lacks: a bit after them).</summary>
        private int BiomeIndex(ContentId biome)
        {
            if (!biome.IsValid)
                return -1;
            var biomes = _content.Biomes;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i].Id == biome)
                    return i;
            // Never in the game (every biome is content): the next free bit, for this session.
            int extra = _extraBiomes.IndexOf(biome);
            if (extra < 0 && biomes.Count + _extraBiomes.Count < 31)
            {
                _extraBiomes.Add(biome);
                extra = _extraBiomes.Count - 1;
            }
            return extra < 0 ? -1 : biomes.Count + extra;
        }

        // ------------------------------------------------------------------------------------------- challenges

        /// <summary>A featured-activity challenge played (practice counts, no ticket): 25 currency per new star, stickers at 9/18/27, outfit 3 at 30.</summary>
        public void ReportChallenge(int index, int stars)
        {
            Refresh();
            if (_phase != EventPhase.Running || _definition == null || !EnsureJoined())
                return;
            var inst = _instance!;
            var def = _definition;
            if ((uint)index >= (uint)inst.ChallengeStars.Length)
                return;
            stars = Math.Max(0, Math.Min(3, stars));
            int old = inst.ChallengeStars[index];
            if (stars <= old)
                return;
            inst.ChallengeStars[index] = (byte)stars;
            int gained = stars - old;
            Achievements?.Add(AchievementCounter.FeaturedChallengeStars, gained);
            int total = Total(inst.ChallengeStars);
            for (int k = 0; k < StarStickers.Length; k++)
            {
                if (total >= StarStickers[k] && (inst.ChallengeMilestones & (1 << k)) == 0)
                {
                    inst.ChallengeMilestones |= (byte)(1 << k);
                    inst.StickersOwed |= (ushort)(1 << (6 + k));
                }
            }
            if (total >= inst.Frozen.Int(EventParam.ChallengeOutfitStars) && (inst.ChallengeMilestones & 8) == 0)
            {
                inst.ChallengeMilestones |= 8;
                if (def.ChallengeOutfit.IsValid)
                    GiveItem(def.ChallengeOutfit);
                Achievements?.Add(AchievementCounter.ChallengeOutfits);
            }
            FlushStickers(inst, def);
            Report(EventGoalKind.ChallengeStars, gained);
            Earn(gained * inst.Frozen.Int(EventParam.ChallengeStarReward), true);
            RequestSave(SaveUrgency.Priority);
        }

        private static int Total(byte[] stars)
        {
            int total = 0;
            for (int i = 0; i < stars.Length; i++)
                total += stars[i];
            return total;
        }

        // ------------------------------------------------------------------------------------------- end

        /// <summary>The first converted instance whose summary the view has not shown yet.</summary>
        public bool TryGetPendingSummary(out EventEndSummary summary)
        {
            Refresh();
            for (int i = 0; i < _state.Instances.Count; i++)
            {
                var inst = _state.Instances[i];
                if (inst.Converted && !inst.SummarySeen)
                {
                    summary = SummaryOf(inst);
                    return true;
                }
            }
            summary = default;
            return false;
        }

        public void MarkSummarySeen(string instanceId)
        {
            var inst = _state.Find(instanceId);
            if (inst == null || inst.SummarySeen)
                return;
            inst.SummarySeen = true;
            RequestSave(SaveUrgency.Normal);
        }

        private void End(EventInstanceState inst)
        {
            if (inst.Converted)
                return;
            var def = DefinitionOf(inst);
            // Never lost: what was reached and not claimed is given now (free, and gold when owned).
            if (def != null && !inst.Ended)
                ClaimAllOn(inst, def);
            inst.Ended = true;
            int left = Math.Max(0, inst.Currency);
            double seconds = left * Math.Max(0.0, inst.Frozen[EventParam.LightSecondsPerUnit]);
            var light = BigValue.Zero;
            if (seconds > 0.0 && Economy != null)
            {
                light = BigValue.Multiply(Economy.GetLightPerSecond(), seconds);
                if (light < BigValue.One)
                    light = BigValue.One;
                Economy.Grant(CurrencyId.Light, light, EconomyReason.Event);
            }
            inst.Currency = 0;
            inst.Converted = true;
            inst.ConvertedCurrency = left;
            inst.ConvertedSeconds = seconds;
            inst.ConvertedLight = light;
            inst.SummarySeen = false;
            Ended?.Invoke(SummaryOf(inst));
            RequestSave(SaveUrgency.Priority);
        }

        private EventEndSummary SummaryOf(EventInstanceState inst) =>
            new EventEndSummary(inst.InstanceId, ContentId.FromSource(inst.Key), inst.ConvertedCurrency, inst.ConvertedSeconds, inst.ConvertedLight,
                inst.TierReached, inst.Points, EventEggRules.Owned(inst.EggItems, EventDefinition.EggItemCount), inst.GoalReached);

        // ------------------------------------------------------------------------------------------- store (Carnet d'or, Box)

        public static ContentId GoldItemOf(EventInstanceState inst) => ContentId.FromSource(inst.InstanceId + ".gold");
        public static ContentId BoxItemOf(EventInstanceState inst) => ContentId.FromSource(inst.InstanceId + ".box");

        /// <summary>The running instance's « .gold » or « .box » item, the Box's items with it (joining first if needed).</summary>
        public ContentId[] ItemsFor(ProductDefinition product)
        {
            Refresh();
            var inst = _phase == EventPhase.Running && EnsureJoined() ? _instance : LatestJoined();
            if (inst == null)
                return Array.Empty<ContentId>();
            if (product.EventScope == EventProductScope.GoldBook)
                return new[] { GoldItemOf(inst) };
            if (product.EventScope != EventProductScope.Box)
                return Array.Empty<ContentId>();
            var def = DefinitionOf(inst);
            var items = new List<ContentId> { BoxItemOf(inst) };
            if (def != null)
            {
                foreach (var item in def.BoxItems)
                {
                    items.Add(item);
                    // T49: the Box's outfit comes with its pieces (the store grants items, never outfits by themselves).
                    if (_content.Shop.Outfits.TryGet(item, out var outfit))
                        items.AddRange(outfit.Accessories);
                }
            }
            return items.ToArray();
        }

        public OfferAvailability CheckEventOffer(ProductDefinition product)
        {
            Refresh();
            if (_phase != EventPhase.Running || _definition == null)
                return OfferAvailability.OutOfWindow;
            bool gold = product.EventScope == EventProductScope.GoldBook;
            if (gold ? !Config.GoldBookEnabled : !Config.ChestEnabled)
                return OfferAvailability.NotEligible;
            if (_instance != null && _player.Entitlements.Owned.Contains(gold ? _goldItem : _boxItem))
                return OfferAvailability.Owned;
            return OfferAvailability.Available;
        }

        // ------------------------------------------------------------------------------------------- rewards

        private void GiveAll(EventInstanceState inst, EventDefinition def, EventReward[] rewards)
        {
            for (int i = 0; i < rewards.Length; i++)
                Give(inst, def, rewards[i]);
        }

        private void Give(EventInstanceState inst, EventDefinition def, in EventReward r)
        {
            switch (r.Kind)
            {
                case EventRewardKind.Gems:
                    GrantBundle(new RewardBundle { Gems = r.Amount });
                    break;
                case EventRewardKind.Tickets:
                    GrantBundle(new RewardBundle { Tickets = r.Amount });
                    break;
                case EventRewardKind.Treats:
                    var treat = r.Item.IsValid ? r.Item : DefaultTreat?.Invoke() ?? ContentId.None;
                    GrantBundle(new RewardBundle { Treat = treat, Treats = treat.IsValid ? r.Amount : 0 });
                    break;
                case EventRewardKind.DiscoveryEgg:
                    GrantBundle(new RewardBundle { DiscoveryEggs = r.Amount });
                    break;
                case EventRewardKind.LightMinutes:
                    GrantBundle(new RewardBundle { LightMinutes = r.Amount });
                    break;
                case EventRewardKind.Sticker:
                    int bit = Array.IndexOf(TrackStickerTiers, r.Amount);
                    if (bit >= 0)
                        inst.StickersOwed |= (ushort)(1 << bit);
                    FlushStickers(inst, def);
                    break;
                case EventRewardKind.Souvenir:
                    GiveSouvenir(inst, def);
                    break;
                case EventRewardKind.Variant:
                    GiveVariant(def, r.Item);
                    break;
                case EventRewardKind.Frame:
                case EventRewardKind.Pose:
                    GiveProfileItem(r);
                    break;
                default:
                    if (r.Item.IsValid)
                        GiveItem(r.Item);
                    break;
            }
        }

        private void GrantBundle(in RewardBundle bundle)
        {
            if (Granter != null)
            {
                Granter.Grant(in bundle, EconomyReason.Event, DiscoverySource.EventTrack);
                return;
            }
            _player.Wallet.Gems += bundle.Gems;
            _player.World.DiscoveryEggs += bundle.DiscoveryEggs;
            _player.Inventory.Tickets += bundle.Tickets;
            if (bundle.Treats > 0 && bundle.Treat.IsValid)
            {
                _player.Progression.Treats.TryGetValue(bundle.Treat, out int owned);
                _player.Progression.Treats[bundle.Treat] = owned + bundle.Treats;
            }
            if (bundle.Seeds > 0)
                _player.Inventory.Add(bundle.SeedPlant, bundle.Seeds);
        }

        /// <summary>A permanent item; one already owned (a returning edition) becomes 100 Gems.</summary>
        private void GiveItem(ContentId item)
        {
            bool added = _player.Entitlements.Owned.Add(item);
            // T49: an outfit is worn through its accessories: owning it owns every piece.
            if (_content.Shop.Outfits.TryGet(item, out var outfit))
                foreach (var piece in outfit.Accessories)
                    added |= _player.Entitlements.Owned.Add(piece);
            // T49: a new hatch effect becomes the one hatches play (the newest owned), unless the player turned them off.
            if (added && HatchEffects.IsHatchEffect(item))
                HatchEffects.OnGranted(_player.Entitlements, item);
            if (!added)
                GrantBundle(new RewardBundle { Gems = ItemOwnedGems });
        }

        /// <summary>T49: an outfit counts as owned once every piece is (a piece may come from elsewhere first).</summary>
        private bool OwnsItem(ContentId item)
        {
            if (!_content.Shop.Outfits.TryGet(item, out var outfit))
                return _player.Entitlements.Owned.Contains(item);
            foreach (var piece in outfit.Accessories)
                if (!_player.Entitlements.Owned.Contains(piece))
                    return false;
            return true;
        }

        private void GiveProfileItem(in EventReward r)
        {
            var profile = ProfileItems;
            if (profile != null && ProfileItemService.TryKindOf(r.Key, out var kind))
            {
                int index = ProfileData.IndexOf(kind, r.Key);
                if (index >= 0)
                {
                    if (!profile.Grant(kind, index))
                        GrantBundle(new RewardBundle { Gems = ItemOwnedGems });
                    return;
                }
            }
            if (r.Item.IsValid)
                GiveItem(r.Item);
        }

        private void GiveVariant(EventDefinition def, ContentId variantId)
        {
            if (!_content.Variants.TryGet(variantId, out var variant))
                return;
            var collection = _player.Collection;
            if (collection.DiscoveredVariants.Contains(variantId))
            {
                GrantBundle(new RewardBundle { Gems = ItemOwnedGems });
                return;
            }
            long now = NowUnix();
            uint seed = EventEggRules.Mix((uint)now ^ unchecked((uint)variantId.GetHashCode() * 2654435761u) ^ unchecked(++_animalCounter * 40503u));
            var animal = new AnimalInstance
            {
                Species = variant.Species, Variant = variant.Id, Seed = seed == 0 ? 1u : seed, CreatedUtcSeconds = (uint)Math.Max(0L, now),
                Rarity = variant.Rarity,
            };
            animal.InstanceId = collection.Add(animal);
            bool newSpecies = collection.DiscoveredSpecies.Add(variant.Species);
            bool newVariant = collection.DiscoveredVariants.Add(variant.Id);
            Economy?.OnAnimalAdded(animal);
            Mastery?.OnAnimalObtained(variant.Species, newSpecies, newVariant);
            Refuge?.Recompute();
            AchievementHooks?.CountObtained(in animal);
            AchievementHooks?.Recount.Collection();
            Achievements?.Add(variant.Species == def.FeaturedSpecies ? AchievementCounter.FeaturedAnimals : AchievementCounter.EventVariants);
            AnimalGiven?.Invoke(variant.Id, animal.InstanceId);
        }

        private void GiveSouvenir(EventInstanceState inst, EventDefinition def)
        {
            if (inst.SouvenirUnixDay != 0)
                return;
            inst.SouvenirUnixDay = (int)Math.Max(1L, NowUnix() / 86_400L);
            if (def.Souvenir.IsValid)
                _player.Entitlements.Owned.Add(def.Souvenir);
            string decoration = _content.Shop.Entitlements.TryGet(def.Souvenir, out var item) ? item.DisplayKey : string.Empty;
            Journal?.Add(JournalMoment.EventSouvenir, def.Id.Value, 0, Key(def.NameKey), decoration.Length > 0 ? Key(decoration) : null);
        }

        /// <summary>Gives the owed album stickers once the album is open (else they wait, never lost).</summary>
        private void FlushStickers(EventInstanceState inst, EventDefinition def)
        {
            if (inst.StickersOwed == 0 || Albums == null || !Albums.IsOpen || def.AlbumEventNumber <= 0)
                return;
            int number = def.AlbumEventNumber;
            Albums.OpenEventPage(number);
            for (int bit = 0; bit < 12; bit++)
            {
                if ((inst.StickersOwed & (1 << bit)) == 0)
                    continue;
                if (bit < 6)
                    Albums.OnEventTrack(number, TrackStickerTiers[bit]);
                else if (bit < 9)
                    Albums.OnEventChallengeStars(number, StarStickers[bit - 6]);
                else
                    Albums.OnEventQuest(number, QuestStickers[bit - 9]);
            }
            inst.StickersOwed = 0;
        }

        /// <summary>Owned already: an item, a variant discovered or a profile item.</summary>
        public bool IsOwned(in EventReward r)
        {
            switch (r.Kind)
            {
                case EventRewardKind.Variant:
                    return _player.Collection.DiscoveredVariants.Contains(r.Item);
                case EventRewardKind.Frame:
                case EventRewardKind.Pose:
                    if (ProfileItems != null && ProfileItemService.TryKindOf(r.Key, out var kind))
                    {
                        int index = ProfileData.IndexOf(kind, r.Key);
                        if (index >= 0)
                            return ProfileItems.Owned(kind, index);
                    }
                    return r.Item.IsValid && _player.Entitlements.Owned.Contains(r.Item);
                default:
                    return r.IsItem && r.Item.IsValid && OwnsItem(r.Item);
            }
        }

        // ------------------------------------------------------------------------------------------- helpers

        private void Earn(int amount, bool track)
        {
            if (amount <= 0 || !EnsureJoined())
                return;
            var inst = _instance!;
            inst.Currency = (int)Math.Min(int.MaxValue, (long)inst.Currency + amount);
            inst.CurrencyEarned += amount;
            if (track)
            {
                bool gold = _player.Entitlements.Owned.Contains(_goldItem);
                long points = gold ? (long)Math.Floor(amount * (1.0 + Math.Max(0.0, inst.Frozen[EventParam.GoldPointsBonus])) + 1e-9) : amount;
                inst.Points = (int)Math.Min(int.MaxValue, inst.Points + points);
                Achievements?.Add(AchievementCounter.EventCurrency, amount);
                AdvanceTrack(inst, _definition!);
            }
            CurrencyChanged?.Invoke(inst.Currency);
            if (track)
                Report(EventGoalKind.EarnCurrency, amount);
            RequestSave(SaveUrgency.Normal);
        }

        private void EarnParam(EventParam param)
        {
            if (EnsureJoined())
                Earn(_instance!.Frozen.Int(param), true);
        }

        /// <summary>Joins at the first currency (nothing earned is ever lost); false outside a run.</summary>
        private bool EnsureJoined()
        {
            Refresh();
            if (_phase != EventPhase.Running || _definition == null)
                return false;
            if (_instance == null)
                Join();
            return _instance != null;
        }

        private void SetInstance(EventInstanceState? inst)
        {
            _instance = inst;
            _goldItem = inst != null ? GoldItemOf(inst) : ContentId.None;
            _boxItem = inst != null ? BoxItemOf(inst) : ContentId.None;
        }

        private EventInstanceState? LatestJoined()
        {
            EventInstanceState? best = null;
            for (int i = 0; i < _state.Instances.Count; i++)
                if (best == null || _state.Instances[i].StartUnix > best.StartUnix)
                    best = _state.Instances[i];
            return best;
        }

        private EventDefinition? DefinitionOf(EventInstanceState inst)
        {
            if (_definition != null && _entry.Key == inst.Key)
                return _definition;
            return _content.Events.TryGet(ContentId.FromSource(inst.Key), out var def) ? def : null;
        }

        private bool TryLive(in EventCalendarEntry e, out EventDefinition definition)
        {
            if (!Config.IsKeyEnabled(e.Key) || !_content.Events.TryGet(ContentId.FromSource(e.Key), out definition!))
            {
                definition = null!;
                return false;
            }
            return Availability.IsPresent(definition);
        }

        private long NowUnix()
        {
            long now = DailyDay.Now(_player, _clock).ToUnixTimeSeconds();
            return now > _state.LastSeenUnix ? now : _state.LastSeenUnix;
        }

        private int EventDay(long now)
        {
            long start = _instance?.StartUnix ?? _entry.StartUnix;
            return now < start ? 0 : (int)((now - start) / 86_400L) + 1;
        }

        private int Today()
        {
            if (DayKey != null)
                return DayKey();
            var utc = DateTimeOffset.FromUnixTimeSeconds(NowUnix()).UtcDateTime;
            return utc.Year * 10000 + utc.Month * 100 + utc.Day;
        }

        private static string Key(string textKey) => JournalService.KeyPrefix + textKey;

        private void RequestSave(SaveUrgency urgency) => _save?.RequestSave(urgency);
    }
}
