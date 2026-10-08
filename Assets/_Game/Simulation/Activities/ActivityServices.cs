#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The activity framework (T37, doc « Mini-jeux &amp; activités » sections 1 and 2, doc 01 Contenu sections 0, A15 and
    /// 12.0). Built once by the bootstrap right after <see cref="DailyVisitServices"/>, like it a facade: it owns the shared
    /// <see cref="ActivityContext"/>, the tickets, the rules of the shipped activities, the reward service and the runner,
    /// keeps the activities' openings (final once reached, doc 08 rule 3) and refills tickets once a second. Unlocks are
    /// recomputed when something they read changes (a hatch, a refuge level, a biome, a new day), at most once a second.
    /// </summary>
    public sealed class ActivityServices : IDisposable
    {
        private const double CheckIntervalSeconds = 1.0;

        private readonly HatchService? _hatch;
        private readonly ProgressionServices? _progression;

        // Handlers kept once: subscribing and unsubscribing never allocate a new delegate.
        private readonly Action<HatchResult> _onHatched;
        private readonly Action<RefugeLevelUp> _onRefugeLevel;
        private readonly Action<BiomeOpening> _onBiome;

        private double _sinceCheck = CheckIntervalSeconds;
        private DateTimeOffset _nextBoundaryUtc = DateTimeOffset.MinValue;
        private bool _unlocksDirty = true;

        public ActivityServices(PlayerState state, ActivityState activities, ContentRegistry content, EconomyService economy,
            HatchService? hatch, MasteryService? mastery, ProgressionServices? progression, IBondSink? bond, IGameClock clock,
            ISaveScheduler? save, ActivityConfig? config = null, DailyContext? daily = null)
        {
            _hatch = hatch;
            _progression = progression;
            Config = config ?? new ActivityConfig();
            Context = new ActivityContext(state, activities, content, Config, progression?.Config ?? ProgressionConfig.Default, clock, economy,
                hatch, mastery, progression, bond, save, daily);
            Rules = new ActivityRulesRegistry();
            Tickets = new TicketService(Context);
            Rewards = new ActivityRewardService(Context);
            Runner = new ActivityRunner(this);

            _onHatched = OnHatched;
            _onRefugeLevel = OnRefugeLevel;
            _onBiome = OnBiomeOpened;
            if (hatch != null)
                hatch.Hatched += _onHatched;
            if (progression != null)
            {
                progression.Refuge.LeveledUp += _onRefugeLevel;
                progression.Gates.Opened += _onBiome;
            }
        }

        public ActivityConfig Config { get; }
        public ActivityContext Context { get; }
        public ActivityRulesRegistry Rules { get; }
        public TicketService Tickets { get; }
        public ActivityRewardService Rewards { get; }
        public ActivityRunner Runner { get; }

        public PlayerState State => Context.State;
        public ActivityState Activities => Context.Activities;

        /// <summary>The event systems (big events, the « Week-end des Activités »); null until they exist.</summary>
        public IActivityEventHooks? Events
        {
            get => Rewards.Events;
            set => Rewards.Events = value;
        }

        /// <summary>The accessibility setting « Activités plus lentes » (speeds × 0.8, times × 1.25; thresholds unchanged).</summary>
        public bool SlowerActivities { get; set; }

        /// <summary>The presenter's inputs the progression cannot give yet (T43 Garden level, rare visitors, outfits).</summary>
        public int GardenLevel { get; set; }
        public bool NightVisitorSeen { get; set; }
        public bool OutfitOwned { get; set; }

        /// <summary>An activity opened (its card turns « Nouveau »). Raised once per activity and profile.</summary>
        public event Action<ActivityId>? ActivityUnlocked;

        /// <summary>Once a second: the ticket refill, the day boundary and the unlocks when something changed. No allocation.</summary>
        public void Tick(double deltaSeconds)
        {
            _sinceCheck += deltaSeconds;
            if (_sinceCheck < CheckIntervalSeconds)
                return;
            _sinceCheck = 0.0;
            if (Context.Clock.UtcNow >= _nextBoundaryUtc)
                CheckDay();
            Tickets.Refresh();
            if (_unlocksDirty)
                RefreshUnlocks();
        }

        /// <summary>At boot (after the offline resolution) and when the app comes back: the day, the refill of the time away, the unlocks.</summary>
        public void OnResume()
        {
            CheckDay();
            Tickets.Refresh();
            RefreshUnlocks();
        }

        /// <summary>Recomputes the day key and the next boundary (time-zone math: at boundaries and on resume only).</summary>
        public void CheckDay()
        {
            _nextBoundaryUtc = Context.CheckDay();
            _unlocksDirty = true;
        }

        /// <summary>Marks the unlocks to recompute at the next tick (the presenter calls it when the Garden or a visitor changes).</summary>
        public void MarkUnlocksDirty() => _unlocksDirty = true;

        /// <summary>
        /// Evaluates every activity's opening once (doc 01, section 0) and keeps the openings: an activity once open stays
        /// open even if a condition moves later (doc 08, rule 3). Raises <see cref="ActivityUnlocked"/> for the new ones.
        /// </summary>
        public void RefreshUnlocks()
        {
            _unlocksDirty = false;
            var input = BuildUnlockInput();
            var records = Activities.Records;
            bool any = false;
            for (int slot = 0; slot < ActivityCatalog.SlotCount; slot++)
            {
                if (records[slot].Unlocked)
                    continue;
                var definition = ActivityCatalog.AtSlot(slot);
                if (!definition.IsUnlocked(in input))
                    continue;
                records[slot].Unlocked = true;
                any = true;
                ActivityUnlocked?.Invoke(definition.Id);
            }
            if (any)
                Context.RequestSave(SaveUrgency.Normal);
        }

        /// <summary>
        /// What the unlock checks read, gathered once: the refuge level, the animals, the day, the refuge level service's
        /// verdicts (so the hub and the refuge level screen agree), the Lake, the Mountain, the Axolotl, the presenter's
        /// inputs. A few time-zone conversions: on change, not per frame.
        /// </summary>
        public ActivityUnlockInput BuildUnlockInput()
        {
            var state = State;
            var input = new ActivityUnlockInput
            {
                RefugeLevel = Context.RefugeLevel,
                Animals = state.Collection.Count,
                DayNumber = Context.DayNumber,
                AxolotlDiscovered = state.Collection.DiscoveredSpecies.Contains(ActivityCatalog.Axolotl),
                GardenLevel = GardenLevel,
                NightVisitorSeen = NightVisitorSeen,
                OutfitOwned = OutfitOwned,
            };
            var progression = _progression;
            if (progression != null)
            {
                input.LakeUnlocked = progression.Store.IsUnlocked(ActivityCatalog.Lake);
                input.MountainUnlocked = progression.Store.IsUnlocked(ActivityCatalog.Mountain);
                for (int slot = 0; slot < ActivityCatalog.ActivityCount; slot++)
                {
                    var feature = ActivityCatalog.AtSlot(slot).Feature;
                    if (feature.HasValue)
                        input.SetFeature(feature.Value, progression.Refuge.IsFeatureUnlocked(feature.Value));
                }
            }
            return input;
        }

        /// <summary>The activity opened once (its record keeps it).</summary>
        public bool IsUnlocked(ActivityId activity)
        {
            int slot = ActivityCatalog.SlotOf(activity);
            return slot >= 0 && Activities.Records[slot].Unlocked;
        }

        /// <summary>
        /// The hub shows it as playable: shipped in this build, enabled by Remote Config, its rules registered and open.
        /// </summary>
        public bool IsAvailable(ActivityId activity) => IsShipped(activity) && Config.IsEnabled(activity) && Rules.Has(activity) && IsUnlocked(activity);

        /// <summary>
        /// The activity exists in this build (its task shipped it), or it is the framework's test activity and a test
        /// registered its rules (never in the game).
        /// </summary>
        public bool IsShipped(ActivityId activity) =>
            ActivityCatalog.TryGet(activity, out var definition)
            && (definition.ShippedInThisBuild || (activity == ActivityId.Test && Rules.Has(ActivityId.Test)));

        /// <summary>At least one activity of this build can be played once open (the Play button exists at all).</summary>
        public bool AnyShipped
        {
            get
            {
                for (int slot = 0; slot < ActivityCatalog.SlotCount; slot++)
                {
                    var id = ActivityCatalog.IdOfSlot(slot);
                    if (IsShipped(id) && Config.IsEnabled(id) && Rules.Has(id))
                        return true;
                }
                return false;
            }
        }

        /// <summary>A challenge is open: the first one, then one star on the previous one opens the next.</summary>
        public bool IsChallengeOpen(ActivityId activity, int challenge)
        {
            int slot = ActivityCatalog.SlotOf(activity);
            if (slot < 0 || (uint)challenge >= ActivityCatalog.ChallengesPerActivity)
                return false;
            return challenge == 0 || Activities.ChallengeStars[ActivityState.ChallengeIndex(slot, challenge - 1)] >= 1;
        }

        public int ChallengeStars(ActivityId activity, int challenge)
        {
            int slot = ActivityCatalog.SlotOf(activity);
            if (slot < 0 || (uint)challenge >= ActivityCatalog.ChallengesPerActivity)
                return 0;
            return Activities.ChallengeStars[ActivityState.ChallengeIndex(slot, challenge)];
        }

        public int ChallengesDone(ActivityId activity) => Activities.ChallengesDone(ActivityCatalog.SlotOf(activity));

        /// <summary>The record of an activity (a copy; an unknown activity gives an empty record).</summary>
        public ActivityRecord Record(ActivityId activity)
        {
            int slot = ActivityCatalog.SlotOf(activity);
            return slot >= 0 ? Activities.Records[slot] : default;
        }

        /// <summary>The next free-mode game's d.</summary>
        public int Difficulty(ActivityId activity) => AdaptiveDifficulty.Level(Record(activity));

        /// <summary>The next rewarded game is offered (the first Grooming game, doc 01 A15).</summary>
        public bool FreeGameAvailable(ActivityId activity) =>
            ActivityCatalog.TryGet(activity, out var definition) && definition.FirstGameFree && !Record(activity).FreeGameUsed;

        /// <summary>The theme the hub selects: the last one chosen, else the activity's base biome.</summary>
        public ContentId Theme(ActivityId activity)
        {
            if (!ActivityCatalog.TryGet(activity, out var definition))
                return ContentId.None;
            var last = Activities.Records[definition.Slot].LastTheme;
            return last.IsValid ? last : definition.BaseBiome;
        }

        /// <summary>The hub's theme pill: the last choice is kept (doc 01, section 12.0 « Thème par biome »).</summary>
        public void SetTheme(ActivityId activity, ContentId biome)
        {
            int slot = ActivityCatalog.SlotOf(activity);
            if (slot < 0 || Activities.Records[slot].LastTheme == biome)
                return;
            Activities.Records[slot].LastTheme = biome;
            Context.RequestSave(SaveUrgency.Normal);
        }

        public void Dispose()
        {
            if (_hatch != null)
                _hatch.Hatched -= _onHatched;
            if (_progression != null)
            {
                _progression.Refuge.LeveledUp -= _onRefugeLevel;
                _progression.Gates.Opened -= _onBiome;
            }
        }

        private void OnHatched(HatchResult result) => _unlocksDirty = true;

        private void OnRefugeLevel(RefugeLevelUp level) => _unlocksDirty = true;

        private void OnBiomeOpened(BiomeOpening opening) => _unlocksDirty = true;
    }
}
