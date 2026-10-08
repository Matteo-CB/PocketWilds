using System;
using PocketWilds.Core;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T36 « Rendez-vous du jour » in the refuge (doc 01 Contenu sections 1 to 6, doc 06 screens 002, 031, 032, doc 05
    /// Sensations sheets 17 to 19): the Today button and its dot, the Gems chip, the wish card, the Today and journal
    /// screens, the welcome's last steps (A08 to A12) and the return gift. It reads <see cref="DailyVisitServices"/>, fills
    /// the view models once per change (texts built here), and turns the player's touches into claims with their feel
    /// (sound, motes, haptics, toasts). A plain class ticked by the scene entry; nothing here allocates per frame.
    /// </summary>
    public sealed class DailyVisitPresenter : ITodayModel, ITodayActions, IJournalModel, IJournalActions, IWishCardActions,
        IDailyHudActions, IWishBubbleSource, IWishBubbleListener, IDisposable
    {
        private const float CheckInterval = 0.25f;

        /// <summary>Doc 01 section 1.1: the paw comes 1 s after a step starts, once more after 10 s without the gesture.</summary>
        private const float HintDelay = 1f;

        /// <summary>A08: the first wish appears when the Upgrades panel closes, or 30 s after the Upgrade button.</summary>
        private const float FirstWishDelay = 30f;

        /// <summary>Doc 05 sheet 17: the Gems motes land about 0.77 s after the wish sound; the Gems sound plays then.</summary>
        private const float GemsLandDelay = 0.77f;

        private readonly GameContext _context;
        private readonly DailyVisitServices _daily;
        private readonly RefugeController _refuge;
        private readonly ProgressionPresenter _progression;
        private readonly IDailyHud _hud;
        private readonly IRefugeHud _refugeHud;
        private readonly ITodayView _today;
        private readonly IJournalView _journal;
        private readonly IWishCardView _card;
        private readonly ILocalization _text;
        private readonly DailyVisitTextFormatter _format;

        private readonly RewardCell[] _cells = new RewardCell[RewardCells.Max];
        private readonly RewardCell[] _cardCells = new RewardCell[RewardCells.Max];
        private readonly TodayWishRow[] _rows = new TodayWishRow[DailyVisitState.MaxWishStock];
        private readonly JournalGoalRow[] _goals = new JournalGoalRow[DailyVisitState.MaxJournalGoals];
        private readonly CalendarBoxCell[] _boxes = new CalendarBoxCell[DailyRewardTables.CalendarBoxCount];

        private readonly Action<WishDraw> _onWishDrawn;
        private readonly Action<int> _onWishChanged;
        private readonly Action<WishClaim> _onWishClaimed;
        private readonly Action<WishSwapInfo> _onWishSwapped;
        private readonly Action<GiftOpening> _onGiftOpened;
        private readonly Action<bool> _onJournalStarted;
        private readonly Action<int> _onDayChanged;
        private readonly Action _onUpgradesClosed;

        private float _time;
        private float _nextCheck;
        private int _wishVersion;
        private int _rowsVersion = -1;
        private int _goalsVersion = -1;
        private int _boxesStamp = -1;
        private int _calendarCycleShown;
        private int _calendarStampedShown;
        private bool _todayShown;
        private bool _dotShown;
        private bool _gemsShown;
        private long _gemsValue = -1;
        private bool _journalVisible;
        private BigValue _giftLight;

        // The open wish card: the slot it shows and the animal it follows.
        private int _cardSlot = -1;
        private uint _cardAnimal;

        // One Today visit, for DailyVisitDone (doc 01 section 0: under two minutes).
        private float _todayOpenedAt;
        private float _lastClaimAt;
        private int _taps;
        private int _items;

        // The welcome's last steps (A08, A10, A12) and their paws.
        private float _upgradeSeenAt = -1f;
        private bool _upgradesClosedOnce;
        private bool _todayAppeared;
        private float _hintIdle;
        private HintTarget _hint;
        private bool _hatchHintPending;
        private float _gemsSoundAt = -1f;

        // Gems of a wish claimed in the world: their motes leave from the bubble when it bursts (doc 05 sheet 17).
        private long _burstGems;

        // Animals of new wishes to bring into view, once no hatch plays (a wish drawn during a hatch may name the newborn,
        // which the hatch itself adds to the meadow).
        private readonly uint[] _toShow = new uint[DailyVisitState.MaxWishStock];
        private int _toShowCount;

        private enum HintTarget : byte
        {
            None = 0,
            Mochi = 1,
            Today = 2,
            Gift = 3,
            Hatch = 4,
        }

        public DailyVisitPresenter(GameContext context, RefugeController refuge, ProgressionPresenter progression, IDailyHud hud,
            ITodayView today, IJournalView journal, IWishCardView card)
        {
            _context = context;
            _daily = context.Daily ?? throw new ArgumentException("No daily services.", nameof(context));
            _refuge = refuge;
            _progression = progression;
            _hud = hud;
            _refugeHud = hud as IRefugeHud;
            _today = today;
            _journal = journal;
            _card = card;
            _text = context.Localization;
            _format = new DailyVisitTextFormatter(context.Content, _text, context.State);

            _onWishDrawn = OnWishDrawn;
            _onWishChanged = OnWishChanged;
            _onWishClaimed = OnWishClaimed;
            _onWishSwapped = OnWishSwapped;
            _onGiftOpened = OnGiftOpened;
            _onJournalStarted = OnJournalStarted;
            _onDayChanged = OnDayChanged;
            _onUpgradesClosed = OnUpgradesClosed;
            var wishes = _daily.Wishes;
            wishes.Drawn += _onWishDrawn;
            wishes.Progressed += _onWishChanged;
            wishes.Completed += _onWishChanged;
            wishes.Claimed += _onWishClaimed;
            wishes.Swapped += _onWishSwapped;
            _daily.Gift.Opened += _onGiftOpened;
            _daily.Journal.Started += _onJournalStarted;
            _daily.DayChanged += _onDayChanged;
            if (_progression != null)
                _progression.UpgradesClosed += _onUpgradesClosed;

            _hud?.BindDaily(this);
            // Built once now, behind the loading screen (a first opening never hitches).
            _journalVisible = _daily.JournalUnlocked;
            RebuildRows();
            RebuildGoals();
            RebuildBoxes();
            _today?.Prewarm(this, _text, this);
            _journal?.Prewarm(this, _text, this);
            _card?.Prewarm(_text);
            // A profile that already has Today never replays its appearance.
            _todayAppeared = _daily.TodayUnlocked;
        }

        /// <summary>A rendez-vous screen or the wish card covers the refuge: world touches and hatching wait.</summary>
        public bool IsOpen => ScreenOpen || CardOpen;

        public bool ScreenOpen => (_today != null && _today.IsOpen) || (_journal != null && _journal.IsOpen);

        public bool CardOpen => _card != null && _card.IsOpen;

        /// <summary>Changes whenever a wish is drawn, progresses, completes, is claimed or swapped (the bubbles re-read then).</summary>
        public int WishVersion => _wishVersion;

        /// <summary>The wish bubbles in the world (null without the prefab).</summary>
        public WishBubbleLayer Bubbles { get; set; }

        /// <summary>Only the wish card is open: its bubble stays shown under it.</summary>
        public bool CardOnlyOpen => CardOpen && !ScreenOpen;

        // ---------------- IWishBubbleSource ----------------

        int IWishBubbleSource.Version => _wishVersion;

        int IWishBubbleSource.Count => _daily.Wishes.PendingCount;

        bool IWishBubbleSource.TryGet(int index, out WishBubbleState state)
        {
            state = default;
            var wishes = _daily.Wishes;
            if ((uint)index >= (uint)wishes.PendingCount)
                return false;
            var wish = wishes.Get(index);
            state = new WishBubbleState
            {
                Slot = index,
                Animal = wish.Animal,
                Progress = wish.Progress,
                Target = wish.Target,
                Done = wish.IsDone,
                Tutorial = wish.IsTutorial,
                System = WishCatalog.TryGet((WishTemplateId)wish.Template, out var template) ? template.System : WishSystem.None,
            };
            return true;
        }

        /// <summary>The bubble of a wish claimed in the world burst (250 ms after the claim): effect, sound, motes, a happy hop.</summary>
        void IWishBubbleListener.OnWishBubbleBurst(int slot, uint animal, Vector3 worldPoint)
        {
            _refuge.PlayWishBurst(animal, worldPoint, _time);
            _context.Audio.Play(AudioEventId.WishDone, worldPoint, default);
            _context.Haptics.Play(HapticEventId.Confirm);
            if (_burstGems > 0 && _refuge.TryWorldToScreen(worldPoint, out var screen))
                ScheduleGems(screen);
            _burstGems = 0;
        }

        public DailyVisitServices Services => _daily;

        // ------------------------------------------------------------------------------------------- tick

        public void Tick(float time, float deltaTime)
        {
            _time = time;
            _today?.Tick(deltaTime);
            _journal?.Tick(deltaTime);
            _card?.Tick(deltaTime);
            if (_card != null && _card.IsOpen && CardPoint(_cardAnimal, out var point))
                _card.Follow(point);
            if (_gemsSoundAt >= 0f && time >= _gemsSoundAt)
            {
                _gemsSoundAt = -1f;
                _context.Audio.Play(AudioEventId.CollectGems, Vector3.zero, default);
            }
            if (time >= _nextCheck)
            {
                _nextCheck = time + CheckInterval;
                Check();
                ShowWishAnimals();
            }
            UpdateWelcome(deltaTime);
        }

        /// <summary>Four times a second: the rail button and its dot, the Gems chip, the journal row.</summary>
        private void Check()
        {
            if (_hud == null)
                return;
            bool today = _daily.TodayUnlocked;
            bool dot = today && _daily.TodayDot;
            if (today != _todayShown || dot != _dotShown)
            {
                // A12: the button arrives with its bounce when no screen is open (doc 01 section 1.1).
                if (today && !_todayAppeared)
                {
                    if (!Calm())
                        return;
                    _todayAppeared = true;
                    _hud.SetToday(true, dot);
                    _hud.PlayTodayAppear();
                    _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
                }
                else
                {
                    _hud.SetToday(today, dot);
                }
                _todayShown = today;
                _dotShown = dot;
            }
            var state = _context.State;
            bool gems = state.Wallet.Gems > 0 || state.IsOnboardingDone;
            if (gems != _gemsShown || state.Wallet.Gems != _gemsValue)
            {
                _gemsShown = gems;
                _gemsValue = state.Wallet.Gems;
                _hud.SetGems(gems, _gemsValue);
            }
        }

        private bool Calm() => !_refuge.ModalOpen && !_refuge.Hatching && (_progression == null || !_progression.IsOpen);

        // ------------------------------------------------------------------------------------------- welcome (A08 to A12)

        private void UpdateWelcome(float deltaTime)
        {
            var state = _context.State;
            var visit = state.DailyVisit;
            var wishes = _daily.Wishes;

            // A08: the first wish, over Mochi, when the Upgrades panel closes or 30 s after the Upgrade button appeared.
            if (visit.TutorialStep < 2 && !state.IsOnboardingDone && _progression != null && _progression.UpgradesUnlocked)
            {
                if (_upgradeSeenAt < 0f)
                    _upgradeSeenAt = _time;
                if ((_upgradesClosedOnce || _time - _upgradeSeenAt >= FirstWishDelay) && Calm() && state.Collection.Count > 0)
                    wishes.StartTutorialWish(state.Collection[0].InstanceId);
            }

            var wanted = HintTarget.None;
            if (wishes.TutorialActive && wishes.WishOf(state.Collection.Count > 0 ? state.Collection[0].InstanceId : 0, out int slot))
            {
                // A08 then A10: the paw on Mochi's bubble while the wish waits, on Mochi once it is fulfilled.
                if (!CardOpen && Calm())
                    wanted = HintTarget.Mochi;
                _ = slot;
            }
            else if (_daily.TodayUnlocked && visit.GiftsOpened == 0)
            {
                // A12: the paw on Today, then on the gift card.
                if (_today != null && _today.IsOpen)
                    wanted = _daily.Gift.CanOpen ? HintTarget.Gift : HintTarget.None;
                else if (Calm() && _todayAppeared)
                    wanted = HintTarget.Today;
            }
            else if (_hatchHintPending && Calm())
            {
                wanted = HintTarget.Hatch;
            }
            ShowHint(wanted, deltaTime);
        }

        private void ShowHint(HintTarget wanted, float deltaTime)
        {
            _hintIdle = wanted != HintTarget.None && wanted == _hint ? _hintIdle + deltaTime : wanted != HintTarget.None ? 0f : _hintIdle;
            if (wanted != _hint)
            {
                HideHint();
                _hint = wanted;
                _hintIdle = 0f;
            }
            if (_hint == HintTarget.None || _hintIdle < HintDelay)
                return;
            switch (_hint)
            {
                case HintTarget.Mochi:
                    if (_context.State.Collection.Count > 0
                        && _refuge.TryGetAnimalScreenPoint(_context.State.Collection[0].InstanceId, out var mochi))
                        _refugeHud?.SetTapHint(true, mochi);
                    break;
                case HintTarget.Today:
                    _hud?.SetTodayHint(true);
                    break;
                case HintTarget.Gift:
                    _today?.SetGiftHint(true);
                    break;
                case HintTarget.Hatch:
                    if (_hud != null)
                        _refugeHud?.SetTapHint(true, _hud.HatchScreenPosition);
                    break;
            }
        }

        private void HideHint()
        {
            switch (_hint)
            {
                case HintTarget.Mochi:
                case HintTarget.Hatch:
                    _refugeHud?.SetTapHint(false, default);
                    break;
                case HintTarget.Today:
                    _hud?.SetTodayHint(false);
                    break;
                case HintTarget.Gift:
                    _today?.SetGiftHint(false);
                    break;
            }
            _hint = HintTarget.None;
        }

        private void OnUpgradesClosed() => _upgradesClosedOnce = true;

        /// <summary>The player hatched (any egg): the paw on Éclore has done its job.</summary>
        public void OnHatchRequested()
        {
            if (_hatchHintPending)
            {
                _hatchHintPending = false;
                if (_hint == HintTarget.Hatch)
                    HideHint();
            }
        }

        // ------------------------------------------------------------------------------------------- world touches

        /// <summary>
        /// A tap on an animal (doc 01 A10, section 2.6 path 1): its fulfilled wish is claimed; otherwise the touch counts
        /// for the bond (daily caps). Returns true when a wish was claimed (the caller plays no other reaction).
        /// </summary>
        public bool OnAnimalTapped(uint instanceId)
        {
            var wishes = _daily.Wishes;
            if (wishes.WishOf(instanceId, out int slot) && wishes.Get(slot).IsDone)
                return ClaimFromWorld(slot, WishClaimPath.Animal);
            _daily.Bond.TryTouch(instanceId);
            return false;
        }

        /// <summary>A tap on a wish bubble: a fulfilled wish is claimed (path 0), a pending one opens its card.</summary>
        public void OnBubbleTapped(int slot)
        {
            var wishes = _daily.Wishes;
            if ((uint)slot >= (uint)wishes.PendingCount)
                return;
            var wish = wishes.Get(slot);
            if (wish.IsDone)
            {
                ClaimFromWorld(slot, WishClaimPath.Bubble);
                return;
            }
            OpenCard(slot, wish);
        }

        private bool ClaimFromWorld(int slot, WishClaimPath path)
        {
            var wishes = _daily.Wishes;
            if (!wishes.Get(slot).IsDone)
                return false;
            uint animal = wishes.Get(slot).Animal;
            _refuge.TryGetAnimalScreenPoint(animal, out var from);
            // The bubble bursts first (slots as of the last tick); the feel follows at the burst (doc 05 sheet 17).
            bool burst = Bubbles != null && Bubbles.PlayCompleted(slot);
            if (!wishes.TryClaim(slot, path, out var claim))
                return false;
            _card?.Close();
            _wishVersion++;
            if (burst)
            {
                _burstGems = claim.Gems;
            }
            else
            {
                _refuge.PlayWishDoneAt(animal, _time);
                Celebrate(claim.Gems, from);
            }
            _refugeHud?.ShowToast(ToastKind.Reward, _text.Get(DailyVisitText.ToastWishDone));
            return true;
        }

        /// <summary>Where the wish card points: its bubble when shown, else just above the animal.</summary>
        private bool CardPoint(uint animal, out Vector2 point)
        {
            if (Bubbles != null && _daily.Wishes.WishOf(animal, out int slot) && Bubbles.TryGetScreenPoint(slot, out point))
                return true;
            return _refuge.TryGetAnimalScreenPoint(animal, out point);
        }

        private void OpenCard(int slot, in WishRecord wish)
        {
            if (_card == null || !CardPoint(wish.Animal, out var point))
                return;
            var data = CardData(wish);
            int count = WishRewards(wish, _cardCells);
            _cardSlot = slot;
            _cardAnimal = wish.Animal;
            _card.Open(data, _cardCells, count, point, _text, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        public void ClaimWishFromCard()
        {
            int slot = CardSlot();
            if (slot >= 0)
                ClaimFromWorld(slot, WishClaimPath.Bubble);
        }

        public void SwapWishFromCard()
        {
            int slot = CardSlot();
            if (slot < 0 || !_daily.Wishes.TrySwap(slot))
                return;
            _context.Audio.Play(AudioEventId.UiTab, Vector3.zero, default);
            var wish = _daily.Wishes.Get(slot);
            _cardAnimal = wish.Animal;
            _card?.Refresh(CardData(wish));
        }

        public void WishCardClosed()
        {
            _cardSlot = -1;
            _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);
        }

        /// <summary>The card's slot, found again by its animal (slots move when another wish is claimed).</summary>
        private int CardSlot() => _cardSlot >= 0 && _daily.Wishes.WishOf(_cardAnimal, out int slot) ? slot : -1;

        private WishCardData CardData(in WishRecord wish) => new WishCardData
        {
            Text = _format.Wish(wish),
            Progress = Math.Min(wish.Progress, wish.Target),
            Target = wish.Target,
            Done = wish.IsDone,
            Tutorial = wish.IsTutorial,
            System = WishCatalog.TryGet((WishTemplateId)wish.Template, out var template) ? template.System : WishSystem.Hatching,
            CanSwap = !wish.IsTutorial && !wish.IsDone && _daily.Wishes.CanSwap,
        };

        /// <summary>What a wish gives (doc 01 section 2.6): Gems, a treat of the animal's biome, bond points.</summary>
        private int WishRewards(in WishRecord wish, RewardCell[] into)
        {
            int n = 0;
            long gems = _daily.Config.WishGems;
            if (gems > 0)
                into[n++] = new RewardCell(RewardKind.Gems, gems);
            var treat = TreatOfAnimal(wish.Animal);
            if (treat != ContentId.None)
                into[n++] = new RewardCell(RewardKind.Treat, 1, treat);
            if (_daily.Config.WishBond > 0)
                into[n++] = new RewardCell(RewardKind.Bond, _daily.Config.WishBond);
            for (int i = n; i < into.Length; i++)
                into[i] = default;
            return n;
        }

        private ContentId TreatOfAnimal(uint animal)
        {
            var progression = _context.Progression;
            if (progression == null || !_context.State.Collection.TryGet(animal, out var instance)
                || !_context.Content.Species.TryGet(instance.Species, out var species)
                || !progression.Config.TryGetBiome(species.Biome, out var biome))
                return ContentId.None;
            return biome.Treat;
        }

        // ------------------------------------------------------------------------------------------- Today (screen 031)

        public void OpenToday()
        {
            if (_today == null || _today.IsOpen || !_daily.TodayUnlocked || _refuge.Hatching)
                return;
            _card?.Close();
            _daily.CheckDay();
            _todayOpenedAt = _time;
            _lastClaimAt = _time;
            _taps = 0;
            _items = 0;
            // Doc 01 section 2.6: opening Today claims the fulfilled wishes at once.
            int claimed = _daily.Wishes.AutoClaimOnTodayOpen();
            _items += claimed;
            _journalVisible = _daily.JournalUnlocked;
            RebuildRows();
            RebuildBoxes();
            _today.Open(this, _text, this);
            _context.Analytics.ScreenView(ScreenId.Today);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
            if (_hint == HintTarget.Today)
                HideHint();
        }

        public void CloseToday()
        {
            if (_today == null || !_today.IsOpen)
                return;
            _today.Close();
            _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);
            if (_hint == HintTarget.Gift)
                HideHint();
            // The visit is done when nothing claimable is left (the field measure of the two-minute criterion).
            if (_items > 0 && !_daily.TodayDot)
                _context.Analytics.DailyVisitDone(_lastClaimAt - _todayOpenedAt, _taps, _items);
        }

        public void ClaimWelcome()
        {
            _taps++;
            var from = _today != null ? _today.GiftScreenPosition : default;
            if (!_daily.Welcome.TryClaim(out var granted))
                return;
            Claimed(1);
            _today?.PlayWelcomeClaimed();
            Celebrate(granted.Bundle.Gems, from);
            _today?.Refresh();
        }

        public void OpenGift()
        {
            _taps++;
            if (!_daily.Gift.TryOpen(out var opening))
                return;
            Claimed(1);
            _giftLight = opening.Granted.Light;
            if (_hint == HintTarget.Gift)
                HideHint();
            _today?.PlayGiftOpened();
            _context.Audio.Play(AudioEventId.GiftOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
            if (opening.Granted.Bundle.Gems > 0)
                ScheduleGems(_today != null ? _today.GiftScreenPosition : default);
            if (opening.Granted.Bundle.DiscoveryEggs > 0)
            {
                // A12: the free Discovery egg waits on the hatch button, « Gratuit »; the paw shows Éclore once.
                var egg = _context.Hatch.DiscoveryEgg;
                _refuge.SelectEgg(egg);
                if (opening.FirstFixed)
                    _hatchHintPending = true;
            }
            _today?.Refresh();
        }

        public void ClaimWish(int row)
        {
            _taps++;
            var from = _today != null ? _today.RowScreenPosition(row) : default;
            if (!_daily.Wishes.TryClaim(row, WishClaimPath.TodayRow, out var claim))
                return;
            Claimed(1);
            _today?.PlayWishClaimed(row);
            Celebrate(claim.Gems, from);
            RebuildRows();
            _today?.Refresh();
        }

        public void ClaimAllWishes()
        {
            _taps++;
            var from = _today != null ? _today.RowScreenPosition(0) : default;
            long gemsBefore = _context.State.Wallet.Gems;
            int claimed = _daily.Wishes.ClaimAll(WishClaimPath.ClaimAll);
            if (claimed <= 0)
                return;
            Claimed(claimed);
            for (int i = 0; i < claimed; i++)
                _today?.PlayWishClaimed(i);
            Celebrate(_context.State.Wallet.Gems - gemsBefore, from);
            RebuildRows();
            _today?.Refresh();
        }

        public void SwapWish(int row)
        {
            _taps++;
            if (!_daily.Wishes.TrySwap(row))
            {
                _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                return;
            }
            _context.Audio.Play(AudioEventId.UiTab, Vector3.zero, default);
            RebuildRows();
            _today?.Refresh();
        }

        public void StampCalendar()
        {
            _taps++;
            if (!_daily.Calendar.TryClaim(out var claim))
                return;
            Claimed(1);
            RebuildBoxes();
            _today?.PlayStamp(claim.Box);
            _context.Audio.Play(AudioEventId.PawStamp, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
            if (claim.Granted.Bundle.Gems > 0)
            {
                var from = _today != null ? _today.GiftScreenPosition : default;
                _hud?.PlayGemsCollect(from);
                _gemsSoundAt = _time + 0.3f;
            }
            if (claim.Granted.Bundle.DiscoveryEggs > 0)
                _refuge.SelectEgg(_context.Hatch.DiscoveryEgg);
            _today?.Refresh();
        }

        public void OpenJournal()
        {
            _taps++;
            if (_journal == null || _journal.IsOpen || !_daily.JournalUnlocked)
                return;
            // Goals reached last week and not claimed are paid at the next opening (doc 01 section 5.3).
            if (_daily.Journal.ClaimCarry(out var carry))
                Celebrate(carry.Bundle.Gems, default);
            RebuildGoals();
            _journal.Open(this, _text, this);
            _context.Analytics.ScreenView(ScreenId.WeeklyJournal);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        public void OpenGiftInfo()
        {
            _taps++;
            _refuge.ShowInfo(_text.Get(DailyVisitText.GiftInfoTitle), _text.Get(DailyVisitText.GiftInfo));
        }

        private void Claimed(int items)
        {
            _items += items;
            _lastClaimAt = _time;
        }

        /// <summary>A reward taken: the claim sound, the Gems motes toward the chip, a confirm haptic (doc 05 sheet 17).</summary>
        private void Celebrate(long gems, Vector2 from)
        {
            _context.Audio.Play(AudioEventId.WishDone, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
            if (gems > 0)
                ScheduleGems(from);
            _wishVersion++;
        }

        private void ScheduleGems(Vector2 from)
        {
            _hud?.PlayGemsCollect(from);
            _gemsSoundAt = _time + GemsLandDelay;
        }

        // ---------------- ITodayModel ----------------

        public int WelcomeIndex
        {
            get
            {
                var welcome = _daily.Welcome;
                if (welcome.CanClaim)
                    return welcome.NextIndex;
                return welcome.ClaimedToday ? welcome.NextIndex - 1 : -1;
            }
        }

        public bool WelcomeClaimable => _daily.Welcome.CanClaim;

        public int WelcomeRewards(RewardCell[] into)
        {
            int index = WelcomeIndex;
            if (index < 0)
                return 0;
            var bundle = DailyRewardTables.Welcome(index, _daily.Context.Rewards(), _daily.Config);
            return RewardCells.FromBundle(bundle, into);
        }

        public TodayGiftState Gift
        {
            get
            {
                var gift = _daily.Gift;
                if (!_daily.Config.GiftEnabled)
                    return TodayGiftState.Hidden;
                if (gift.CanOpen)
                    return TodayGiftState.Ready;
                return gift.OpenedToday ? TodayGiftState.Opened : TodayGiftState.Hidden;
            }
        }

        public GiftSize GiftSize => DailyRewardTables.SizeOf(_daily.Gift.LastLot);

        public int GiftRewards(RewardCell[] into) =>
            Gift == TodayGiftState.Opened ? RewardCells.FromBundle(_daily.Gift.LastBundle, into, _giftLight) : 0;

        public int WishCount => _daily.Wishes.PendingCount;

        public TodayWishRow Wish(int row)
        {
            if (_rowsVersion != _wishVersion)
                RebuildRows();
            return (uint)row < (uint)_rows.Length ? _rows[row] : default;
        }

        public int SwapsLeft => _daily.Wishes.SwapsLeft;

        public bool AnyWishDone => _daily.Wishes.HasDoneUnclaimed;

        public int CalendarCycle => _calendarCycleShown;

        public int CalendarNext => _daily.Calendar.CanClaim ? _calendarStampedShown : Math.Min(_calendarStampedShown, 27);

        public bool CalendarClaimable => _daily.Calendar.CanClaim;

        public CalendarBoxCell Box(int box)
        {
            if (_boxesStamp != _daily.Calendar.Stamps)
                RebuildBoxes();
            return (uint)box < (uint)_boxes.Length ? _boxes[box] : default;
        }

        public RewardCell Tomorrow
        {
            get
            {
                var calendar = _daily.Calendar;
                var bundle = calendar.PeekBox(calendar.CanClaim ? 1 : 0);
                return RewardCells.FromBundle(bundle, _cells) > 0 ? _cells[0] : default;
            }
        }

        public int DaysToBigBox => _daily.Calendar.DaysToNextBig;

        public bool JournalVisible => _journalVisible;
        public int JournalReached => _daily.Journal.ReachedCount;
        public int JournalGoals => _daily.Journal.GoalCount;
        public bool JournalDot => _daily.Journal.HasClaimable;

        private void RebuildRows()
        {
            _rowsVersion = _wishVersion;
            var wishes = _daily.Wishes;
            var collection = _context.State.Collection;
            int count = Math.Min(wishes.PendingCount, _rows.Length);
            for (int i = 0; i < count; i++)
            {
                var wish = wishes.Get(i);
                var row = new TodayWishRow
                {
                    Card = CardData(wish),
                    Animal = wish.Animal,
                    Gems = _daily.Config.WishGems,
                };
                if (collection.TryGet(wish.Animal, out var animal))
                {
                    row.Variant = animal.Variant;
                    row.AnimalName = _format.AnimalName(animal.InstanceId);
                }
                _rows[i] = row;
            }
        }

        /// <summary>
        /// The 28 boxes of the shown cycle: stamped ones, today's, the rest. A cycle finished today stays shown complete
        /// until midnight (doc 01 section 4.1).
        /// </summary>
        private void RebuildBoxes()
        {
            var calendar = _daily.Calendar;
            _boxesStamp = calendar.Stamps;
            bool finishedToday = calendar.StampedToday && calendar.NextBox == 0;
            int cycle = finishedToday ? Math.Max(1, calendar.Cycle - 1) : calendar.Cycle;
            int stamped = finishedToday ? DailyRewardTables.CalendarBoxCount : calendar.NextBox;
            _calendarCycleShown = cycle;
            _calendarStampedShown = stamped;
            var rewards = _daily.Context.Rewards();
            bool claimable = calendar.CanClaim;
            for (int box = 0; box < _boxes.Length; box++)
            {
                var bundle = DailyRewardTables.CalendarBox(box, cycle, rewards, _daily.Config);
                _boxes[box] = new CalendarBoxCell
                {
                    Reward = RewardCells.FromBundle(bundle, _cells) > 0 ? _cells[0] : default,
                    Big = DailyRewardTables.IsBigBox(box),
                    Stamped = box < stamped,
                    Today = claimable ? box == stamped : calendar.StampedToday && box == stamped - 1,
                };
            }
        }

        // ---------------- IJournalModel / IJournalActions ----------------

        public int GoalCount => _daily.Journal.GoalCount;

        public JournalGoalRow Goal(int i)
        {
            if (_goalsVersion != _wishVersion)
                RebuildGoals();
            return (uint)i < (uint)_goals.Length ? _goals[i] : default;
        }

        public int DaysLeft => _daily.Journal.DaysLeft;
        public bool IsFirst => _daily.Journal.IsFirstJournal;
        public bool CanClaimComplete => _daily.Journal.CanClaimComplete;
        public bool CompleteClaimed => _context.State.DailyVisit.JournalCompleteClaimed;

        public int CompleteRewards(RewardCell[] into)
        {
            var config = _daily.Config;
            var bundle = new RewardBundle { Gems = config.JournalCompleteGems, DiscoveryEggs = 1, Size = GiftSize.Big };
            return RewardCells.FromBundle(bundle, into);
        }

        public bool HasCarry => _daily.Journal.HasCarry;

        public int CarryRewards(RewardCell[] into)
        {
            var visit = _context.State.DailyVisit;
            var bundle = new RewardBundle { Gems = visit.CarryGems, Tickets = visit.CarryTickets, DiscoveryEggs = visit.CarryDiscovery };
            return RewardCells.FromBundle(bundle, into);
        }

        public void ClaimGoal(int i)
        {
            var from = _journal != null ? _journal.GoalScreenPosition(i) : default;
            if (!_daily.Journal.TryClaimGoal(i, out var granted))
                return;
            _journal?.PlayGoalClaimed(i);
            Celebrate(granted.Bundle.Gems, from);
            RebuildGoals();
            _journal?.Refresh();
            _today?.Refresh();
        }

        public void ClaimComplete()
        {
            if (!_daily.Journal.TryClaimComplete(out var granted))
                return;
            _journal?.PlayCompleteClaimed();
            _context.Audio.Play(AudioEventId.GiftOpen, Vector3.zero, default);
            Celebrate(granted.Bundle.Gems, default);
            if (granted.Bundle.DiscoveryEggs > 0)
                _refuge.SelectEgg(_context.Hatch.DiscoveryEgg);
            _journal?.Refresh();
            _today?.Refresh();
        }

        public void ClaimCarry()
        {
            if (_daily.Journal.ClaimCarry(out var granted))
                Celebrate(granted.Bundle.Gems, default);
            _journal?.Refresh();
        }

        public void CloseJournal()
        {
            if (_journal == null || !_journal.IsOpen)
                return;
            _journal.Close();
            _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);
            _today?.Refresh();
        }

        private void RebuildGoals()
        {
            _goalsVersion = _wishVersion;
            var journal = _daily.Journal;
            int count = Math.Min(journal.GoalCount, _goals.Length);
            var rewards = _daily.Context.Rewards();
            var config = _daily.Config;
            for (int i = 0; i < count; i++)
            {
                var record = journal.Get(i);
                var row = new JournalGoalRow
                {
                    Text = _format.Goal(record),
                    Progress = Math.Min(record.Progress, record.Target),
                    Target = record.Target,
                    Reached = record.IsReached,
                    Claimed = record.Claimed,
                    Slot = (JournalSlot)record.Slot,
                    HeadStart = journal.HeadStart(i),
                    Reward0 = new RewardCell(RewardKind.Gems, config.JournalGoalGems),
                };
                row.Reward1 = rewards.PlayOpen
                    ? new RewardCell(RewardKind.Ticket, config.JournalGoalTickets)
                    : new RewardCell(RewardKind.Light, 0) { LightMinutes = config.JournalGoalTickets * rewards.TicketLightMinutes };
                _goals[i] = row;
            }
        }

        // ------------------------------------------------------------------------------------------- return gift (section 6)

        /// <summary>
        /// The return gift to show on the offline return screen (doc 01 section 6): fills the card and its cells; false when
        /// none waits. Lines say what waits in the refuge (wishes, the gift, the journal, the calendar).
        /// </summary>
        public bool TryBuildReturnGift(out ReturnGiftCard card, RewardCell[] cells, out int count)
        {
            card = default;
            count = 0;
            var returns = _daily.Returns;
            if (!returns.HasPending)
                return false;
            var gift = returns.Pending;
            count = RewardCells.FromBundle(gift.Bundle, cells);
            card.Big = gift.IsBig;
            card.DaysAway = gift.DaysAway;
            int line = 0;
            int wishes = _daily.Wishes.PendingCount;
            if (wishes > 0)
                SetLine(ref card, line++, _format.ReturnLine(DailyVisitText.ReturnLineWishes, wishes));
            if (_daily.Gift.CanOpen)
                SetLine(ref card, line++, _text.Get(DailyVisitText.ReturnLineGift));
            if (_daily.Calendar.CanClaim)
                SetLine(ref card, line++, _text.Get(DailyVisitText.ReturnLineCalendar));
            if (_daily.Journal.HasClaimable && line < 4)
                SetLine(ref card, line, _text.Get(DailyVisitText.ReturnLineJournal));
            return true;
        }

        private static void SetLine(ref ReturnGiftCard card, int index, string text)
        {
            switch (index)
            {
                case 0: card.Line0 = text; break;
                case 1: card.Line1 = text; break;
                case 2: card.Line2 = text; break;
                case 3: card.Line3 = text; break;
            }
        }

        /// <summary>The offline return's « Récupérer » also takes the return gift.</summary>
        public void ClaimReturnGift()
        {
            if (!_daily.Returns.TryClaim(out var granted))
                return;
            _context.Audio.Play(AudioEventId.GiftOpen, Vector3.zero, default);
            if (granted.Bundle.Gems > 0)
                ScheduleGems(default);
            if (granted.Bundle.DiscoveryEggs > 0)
                _refuge.SelectEgg(_context.Hatch.DiscoveryEgg);
        }

        // ------------------------------------------------------------------------------------------- events

        private void OnWishDrawn(WishDraw draw)
        {
            _wishVersion++;
            // The animal of a new wish comes into view (its bubble is in the world, doc 06 screen 002), after any hatch.
            if ((uint)draw.Slot < (uint)_daily.Wishes.PendingCount && _toShowCount < _toShow.Length)
                _toShow[_toShowCount++] = _daily.Wishes.Get(draw.Slot).Animal;
        }

        private void ShowWishAnimals()
        {
            if (_toShowCount == 0 || _refuge.Hatching)
                return;
            for (int i = 0; i < _toShowCount; i++)
                _refuge.EnsureVisible(_toShow[i]);
            _toShowCount = 0;
        }

        private void OnWishChanged(int slot)
        {
            _wishVersion++;
            if (_card != null && _card.IsOpen && slot == CardSlot())
                _card.Refresh(CardData(_daily.Wishes.Get(slot)));
        }

        private void OnWishClaimed(WishClaim claim)
        {
            _wishVersion++;
            if (claim.Wish.IsTutorial)
            {
                // A10: the welcome is over; the Gems chip appears with its first gain (doc 01 section 1.3).
                _context.Save?.RequestSave(SaveUrgency.Priority);
            }
        }

        private void OnWishSwapped(WishSwapInfo swap) => _wishVersion++;

        private void OnGiftOpened(GiftOpening opening) => _giftLight = opening.Granted.Light;

        private void OnJournalStarted(bool first)
        {
            _journalVisible = _daily.JournalUnlocked;
            if (!first)
                _refugeHud?.ShowToast(ToastKind.Unlock, _text.Get(DailyVisitText.ToastJournalNew));
        }

        private void OnDayChanged(int dayKey)
        {
            _wishVersion++;
            _giftLight = BigValue.Zero;
            _journalVisible = _daily.JournalUnlocked;
            RebuildBoxes();
            if (_today != null && _today.IsOpen)
            {
                RebuildRows();
                _today.Refresh();
            }
        }

        public void Dispose()
        {
            HideHint();
            var wishes = _daily.Wishes;
            wishes.Drawn -= _onWishDrawn;
            wishes.Progressed -= _onWishChanged;
            wishes.Completed -= _onWishChanged;
            wishes.Claimed -= _onWishClaimed;
            wishes.Swapped -= _onWishSwapped;
            _daily.Gift.Opened -= _onGiftOpened;
            _daily.Journal.Started -= _onJournalStarted;
            _daily.DayChanged -= _onDayChanged;
            if (_progression != null)
                _progression.UpgradesClosed -= _onUpgradesClosed;
        }
    }
}
