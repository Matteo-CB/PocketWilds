#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The rendez-vous of the day (T36, doc 01 « Contenu des rendez-vous, des activités et de l'accueil »): welcome gifts,
    /// daily gift, 28-day calendar, wishes, weekly journal, return gifts and the bond of touches and treats. Built once by
    /// the bootstrap right after <see cref="ProgressionServices"/>, like it a facade: it owns the shared
    /// <see cref="DailyContext"/>, keeps today's key, runs the day change, and forwards the game's events to the services
    /// that count progress. Time-zone math runs once a second at most (<see cref="Tick"/>), on resume and at boundaries.
    /// </summary>
    public sealed class DailyVisitServices : IDisposable
    {
        private const double CheckIntervalSeconds = 1.0;

        private readonly HatchService? _hatch;
        private readonly MasteryService? _mastery;
        private readonly CodexService? _codex;
        private readonly ProgressionServices? _progression;
        private readonly IDailyProgressHooks[] _hooks;

        // Handlers kept once: subscribing and unsubscribing never allocate a new delegate.
        private readonly Action<HatchResult> _onHatched;
        private readonly Action<MasteryGain> _onMastery;
        private readonly Action<ContentId> _onCodexPage;
        private readonly Action<UpgradePurchase> _onUpgrade;
        private readonly Action<ZoneOpening> _onZone;
        private readonly Action<RefugeLevelUp> _onRefugeLevel;
        private readonly Action<uint, bool> _onTouched;
        private readonly Action<uint, ContentId, int> _onTreat;
        private readonly Action<uint, int> _onBond;
        private readonly Action<GiftOpening> _onGift;
        private readonly Action<CalendarClaim> _onCalendar;
        private readonly Action<int> _onGoalReached;

        private double _sinceCheck = CheckIntervalSeconds;
        private DateTimeOffset _nextBoundaryUtc = DateTimeOffset.MinValue;
        private bool _wasTodayOpen;

        public DailyVisitServices(PlayerState state, ContentRegistry content, EconomyService economy, HatchService? hatch,
            MasteryService? mastery, CodexService? codex, ProgressionServices? progression, IGameClock clock, ISaveScheduler? save,
            DailyVisitConfig? config = null)
        {
            _hatch = hatch;
            _mastery = mastery;
            _codex = codex;
            _progression = progression;
            Config = config ?? new DailyVisitConfig();
            Context = new DailyContext(state, content, Config, progression?.Config ?? ProgressionConfig.Default, clock, economy, hatch, mastery,
                progression, save);
            Bond = new BondService(Context);
            Welcome = new WelcomeGiftService(Context);
            Gift = new DailyGiftService(Context);
            Calendar = new CalendarService(Context);
            Wishes = new WishService(Context, Bond);
            Journal = new WeeklyJournalService(Context);
            Returns = new ReturnGiftService(Context);
            _hooks = new IDailyProgressHooks[] { Wishes, Journal };

            _onHatched = OnHatched;
            _onMastery = OnMasteryLeveled;
            _onCodexPage = OnCodexPageOpened;
            _onUpgrade = OnUpgrade;
            _onZone = OnZoneOpened;
            _onRefugeLevel = OnRefugeLevel;
            _onTouched = OnTouched;
            _onTreat = OnTreatGiven;
            _onBond = OnBondGained;
            _onGift = OnGiftOpened;
            _onCalendar = OnCalendarStamped;
            _onGoalReached = OnGoalReached;

            if (hatch != null)
                hatch.Hatched += _onHatched;
            if (mastery != null)
                mastery.LeveledUp += _onMastery;
            if (codex != null)
                codex.SpeciesPageOpened += _onCodexPage;
            if (progression != null)
            {
                progression.Upgrades.Purchased += _onUpgrade;
                progression.Zones.Opened += _onZone;
                progression.Refuge.LeveledUp += _onRefugeLevel;
            }
            Bond.Touched += _onTouched;
            Bond.TreatGiven += _onTreat;
            Bond.BondGained += _onBond;
            Gift.Opened += _onGift;
            Calendar.Claimed += _onCalendar;
            Journal.GoalReached += _onGoalReached;
            Wishes.Completed += OnWishCompleted;
            Wishes.OnboardingFinished += Refresh;
        }

        public DailyVisitConfig Config { get; }
        public DailyContext Context { get; }
        public BondService Bond { get; }
        public WelcomeGiftService Welcome { get; }
        public DailyGiftService Gift { get; }
        public CalendarService Calendar { get; }
        public WishService Wishes { get; }
        public WeeklyJournalService Journal { get; }
        public ReturnGiftService Returns { get; }

        public PlayerState State => Context.State;

        /// <summary>Today's key (yyyymmdd, reset hour applied); only moves forward.</summary>
        public int TodayKey => Context.TodayKey;

        /// <summary>The Today button exists: refuge level 2 and the welcome over (doc 01, section 0).</summary>
        public bool TodayUnlocked => Context.TodayOpen;

        /// <summary>The weekly journal is open (refuge level 6, day 2). A time-zone conversion: read on change, not per frame.</summary>
        public bool JournalUnlocked => Journal.Unlocked;

        /// <summary>
        /// Something waits in Today (doc 06 screen 002, the rail's dot): a welcome gift, the daily gift, today's calendar box,
        /// a fulfilled wish not claimed yet, or a journal goal to claim.
        /// </summary>
        public bool TodayDot =>
            Context.TodayOpen
            && (Welcome.CanClaim || Gift.CanOpen || Calendar.CanClaim || Wishes.HasDoneUnclaimed || (Config.JournalEnabled && Journal.HasClaimable));

        /// <summary>The day moved forward (the new key). Presentation refreshes Today, the rail and the journal.</summary>
        public event Action<int>? DayChanged;

        /// <summary>Today opened for the first time this session (level 2 reached or the welcome finished).</summary>
        public event Action? TodayUnlockedNow;

        /// <summary>
        /// Once a second: the day boundary (the clock reaching the next reset instant). No allocation, no time-zone math
        /// until the boundary is reached.
        /// </summary>
        public void Tick(double deltaSeconds)
        {
            _sinceCheck += deltaSeconds;
            if (_sinceCheck < CheckIntervalSeconds)
                return;
            _sinceCheck = 0.0;
            if (Context.Clock.UtcNow >= _nextBoundaryUtc)
                CheckDay();
        }

        /// <summary>
        /// T37: a game ended. A rewarded game's stars and a challenge passed (its first star) count for the weekly journal;
        /// practice and abandoned games never do (doc 01, section 5).
        /// </summary>
        public void OnActivityEnded(in ActivityGameResult result)
        {
            if (!result.Rewarded || result.Abandoned)
                return;
            bool passed = result.Challenge >= 0 && result.ChallengeStarsBefore == 0 && result.Stars >= 1;
            Journal.OnActivityGame(result.Stars, passed);
        }

        /// <summary>
        /// T38: a grooming game ended (<see cref="GroomingHooks"/> calls it): its wishes (WISH-019 to 022) and the journal's
        /// JRN-033. Allocation-free.
        /// </summary>
        public void OnGroomingGame(in GroomingGameSummary game)
        {
            Wishes.OnGroomingGame(in game);
            Journal.OnGroomingGame(in game);
        }

        /// <summary>
        /// T39: a hide-and-seek game ended (<see cref="HideSeekHooks"/> calls it): its wishes (WISH-051 to 053) and the
        /// journal's JRN-016. Allocation-free.
        /// </summary>
        public void OnHideSeekGame(in HideSeekGameSummary game)
        {
            Wishes.OnHideSeekGame(in game);
            Journal.OnHideSeekGame(in game);
        }

        /// <summary>
        /// At boot (after the offline resolution) and when the app comes back: the day check first, then the return gift of
        /// this absence (doc 01, section 6).
        /// </summary>
        public void OnResume(in OfflineReport report)
        {
            CheckDay();
            Returns.Evaluate(report);
        }

        /// <summary>
        /// Recomputes today's key. A new day: the login day counts, the touch caps start again, the journal of the week is
        /// ensured and today's wishes are drawn (doc 01, section 0: nothing is lost by a missed day, nothing is regranted by
        /// a clock or a time zone moved back: the key only moves forward).
        /// </summary>
        public void CheckDay()
        {
            var now = DailyDay.Now(Context.State, Context.Clock);
            int key = DailyDay.DayKey(now, Context.Zone, Config.ResetHour);
            _nextBoundaryUtc = DailyDay.NextBoundaryUtc(now, Context.Zone, Config.ResetHour);
            if (key > Context.TodayKey)
                Context.TodayKey = key;

            var visit = Context.State.DailyVisit;
            bool newDay = Context.TodayKey > visit.LastDayKey;
            if (newDay)
            {
                visit.LastDayKey = Context.TodayKey;
                visit.LoginDays++;
                Bond.OnDayChanged();
            }
            Refresh();
            if (newDay)
            {
                Context.RequestSave(SaveUrgency.Normal);
                DayChanged?.Invoke(Context.TodayKey);
            }
        }

        /// <summary>
        /// What opens with a refuge level, the end of the welcome or a new day: the journal of the week and today's wishes.
        /// Both are idempotent for a given day.
        /// </summary>
        public void Refresh()
        {
            Journal.Ensure(Context.TodayKey);
            bool open = Context.TodayOpen;
            if (open && Config.WishesEnabled)
                Wishes.EnsureDrawn(Context.TodayKey);
            if (open && !_wasTodayOpen)
                TodayUnlockedNow?.Invoke();
            _wasTodayOpen = open;
        }

        public void Dispose()
        {
            if (_hatch != null)
                _hatch.Hatched -= _onHatched;
            if (_mastery != null)
                _mastery.LeveledUp -= _onMastery;
            if (_codex != null)
                _codex.SpeciesPageOpened -= _onCodexPage;
            if (_progression != null)
            {
                _progression.Upgrades.Purchased -= _onUpgrade;
                _progression.Zones.Opened -= _onZone;
                _progression.Refuge.LeveledUp -= _onRefugeLevel;
            }
            Bond.Touched -= _onTouched;
            Bond.TreatGiven -= _onTreat;
            Bond.BondGained -= _onBond;
            Gift.Opened -= _onGift;
            Calendar.Claimed -= _onCalendar;
            Journal.GoalReached -= _onGoalReached;
            Wishes.Completed -= OnWishCompleted;
            Wishes.OnboardingFinished -= Refresh;
        }

        // ------------------------------------------------------------------ the game's events, forwarded

        private void OnHatched(HatchResult result)
        {
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnHatched(in result);
        }

        private void OnMasteryLeveled(MasteryGain gain)
        {
            int levels = gain.After.Level - gain.Before.Level;
            if (levels <= 0)
                return;
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnMasteryLeveled(gain.Species, levels);
        }

        private void OnCodexPageOpened(ContentId species)
        {
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnCodexPageOpened(species);
        }

        private void OnUpgrade(UpgradePurchase purchase)
        {
            Wishes.OnUpgrade(in purchase);
            int levels = purchase.LevelTo - purchase.LevelFrom;
            if (levels <= 0)
                return;
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnUpgradesBought(levels);
        }

        private void OnZoneOpened(ZoneOpening opening)
        {
            Wishes.OnZone(in opening);
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnZoneOpened();
        }

        private void OnRefugeLevel(RefugeLevelUp level) => Refresh();

        private void OnTouched(uint animal, bool counted)
        {
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnAnimalTouched(animal, counted);
        }

        private void OnTreatGiven(uint animal, ContentId treat, int points)
        {
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnTreatGiven(animal);
        }

        private void OnBondGained(uint animal, int points)
        {
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnBondGained(animal, points);
        }

        private void OnWishCompleted(int slot)
        {
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnWishCompleted();
        }

        private void OnGiftOpened(GiftOpening opening)
        {
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnGiftOpened();
        }

        /// <summary>A journal goal reached is kept even if the app is killed before the next autosave.</summary>
        private void OnGoalReached(int index) => Context.RequestSave(SaveUrgency.Normal);

        private void OnCalendarStamped(CalendarClaim claim)
        {
            for (int i = 0; i < _hooks.Length; i++)
                _hooks[i].OnCalendarStamped();
        }
    }
}
