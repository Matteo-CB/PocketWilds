using System;
using PocketWilds.Core;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T37 « Cadre des activités » in the refuge (doc « Mini-jeux &amp; activités » sections 1 and 2, doc 01 Contenu section
    /// 12.0 and A15, doc 06 screens 029 and 030, doc 05 Sensations sheet 20): the HUD's Play button, the hub, the « no
    /// ticket » card, a game (its stage, the activity HUD, pause), the result card and its beats, the activity music. It
    /// reads <see cref="ActivityServices"/>, fills the view models once per change, and turns touches into games. A plain
    /// class ticked by the scene entry; nothing here allocates per frame (texts are built when a screen opens or changes).
    /// </summary>
    public sealed class ActivityPresenter : IActivityHubModel, IActivityHubActions, IPlayHudActions, ITicketsEmptyActions,
        IActivityHudActions, IActivityResultActions, IActivityResultBeats, IDisposable
    {
        /// <summary>The Play button, its badge and the ticket chip are checked twice a second (never per frame).</summary>
        private const float CheckInterval = 0.5f;

        /// <summary>Doc 05 sheet 20: the end chord plays, then the result card slides in.</summary>
        private const float ResultDelay = 0.6f;

        /// <summary>A long frame (a hitch, the app coming back) never jumps a game forward by more than this.</summary>
        private const float MaxGameDelta = 0.1f;

        private const int MaxThemes = 8;

        private enum Phase : byte
        {
            None = 0,

            /// <summary>The stage is loading its content (under a second, doc Mini-jeux section 1).</summary>
            Loading = 1,

            Playing = 2,

            /// <summary>The end chord: the stage still shows, the result card comes next.</summary>
            Ending = 3,

            Result = 4,
        }

        private readonly GameContext _context;
        private readonly ActivityServices _services;
        private readonly IPlayHud _playHud;
        private readonly IRefugeHud _refugeHud;
        private readonly IActivityHubView _hub;
        private readonly IActivityHudView _hud;
        private readonly IActivityResultView _result;
        private readonly ITicketsEmptyView _empty;
        private readonly ILocalization _text;
        private readonly ActivityStageHost _host;
        private readonly ActivityEndedHandler _onEnded;
        private readonly Action _onTicketsChanged;

        private readonly ActivityCardModel[] _cards = new ActivityCardModel[ActivityCatalog.SlotCount];
        private readonly ActivityId[] _cardIds = new ActivityId[ActivityCatalog.SlotCount];
        private readonly ChallengeCellModel[] _challenges = new ChallengeCellModel[ActivityCatalog.ChallengesPerActivity];
        private readonly ContentId[] _themes = new ContentId[MaxThemes];
        private readonly bool[] _playedThisSession = new bool[ActivityCatalog.SlotCount];
        private readonly RewardCell[] _cells = new RewardCell[RewardCells.Max];

        private int _cardCount;
        private int _selected;
        private int _challengeCount;
        private int _themeCount;
        private int _selectedTheme;
        private bool _practice;
        private TicketsModel _tickets;
        private bool _ticketsDirty = true;

        private float _time;
        private float _nextCheck;
        private Phase _phase;
        private IActivityStage _stage;
        private ActivityRequest _pending;
        private ActivityRequest _last;
        private bool _hasLast;
        private ActivityGameResult _ended;
        private float _resultAt;
        private int _streakShown;
        private ActivityId _lastPlayed;

        // The « no ticket » card: the game the player wanted, and what they chose (doc 08, tickets_empty).
        private bool _emptyReported = true;
        private int _emptyChoice;

        // The Play button (doc 01 A15).
        private bool _playShown;
        private int _playTickets = -1;
        private bool _playDot;
        private bool _playAppeared;
        private bool _playHint;

        // The activity music (Addressables, released once the refuge's music is back).
        private AsyncOperationHandle<AudioClip> _music;
        private string _musicAddress;
        private bool _musicOn;

        /// <summary>The music's clip is released once the refuge's beds have faded back in (releasing earlier cuts it).</summary>
        private float _musicReleaseAt = float.MaxValue;

        public ActivityPresenter(GameContext context, IPlayHud playHud, IActivityHubView hub, IActivityHudView hud, IActivityResultView result,
            ITicketsEmptyView empty, ActivityStageHost host)
        {
            _context = context;
            _services = context.Activities ?? throw new ArgumentException("No activity services.", nameof(context));
            _playHud = playHud;
            _refugeHud = playHud as IRefugeHud;
            _hub = hub;
            _hud = hud;
            _result = result;
            _empty = empty;
            _text = context.Localization ?? KeysOnly.Instance;
            _host = host;
            if (_host != null)
            {
                _host.Hud = hud;
                _host.Text = _text;
            }
            var stages = new ActivityStageRegistry();
            // The stages of the activities shipped in this build (each registers its factory and its themes).
            GroomingStage.Register(stages);
            Stages = stages;

            _onEnded = OnEnded;
            _onTicketsChanged = OnTicketsChanged;
            _services.Runner.Ended += _onEnded;
            _services.Tickets.Changed += _onTicketsChanged;

            _playHud?.BindPlay(this);
            (_result as IActivityResultBeatSource)?.SetBeats(this);
            // Built once now, behind the loading screen (a first opening never hitches).
            RebuildTickets();
            RebuildCards();
            RebuildSelection();
            _hub?.Prewarm(this, _text, this);
            _hud?.Prewarm(_text);
            _result?.Prewarm(_text);
            _empty?.Prewarm(_text);
            // A profile that already played never replays the button's appearance.
            _playAppeared = _services.Activities.GamesStarted > 0;
        }

        /// <summary>Where the activities' stages come from (each shipped activity registers its own; tests add theirs).</summary>
        public IActivityStageProvider Stages { get; set; }

        /// <summary>The refuge's beds, set back when the player leaves a game.</summary>
        public BiomeAudioSet RefugeBeds { get; set; }

        /// <summary>The rendez-vous presenter: Today and the hub never show together.</summary>
        public DailyVisitPresenter Daily { get; set; }

        /// <summary>Something of the activities covers the refuge: world touches and hatching wait.</summary>
        public bool IsOpen => HubOpen || EmptyOpen || _phase != Phase.None;

        public bool HubOpen => _hub != null && _hub.IsOpen;

        public bool EmptyOpen => _empty != null && _empty.IsOpen;

        /// <summary>A game is loading, running, ending or shows its result.</summary>
        public bool InGame => _phase != Phase.None;

        public bool Playing => _phase == Phase.Playing;

        /// <summary>The Play button shows (an activity of this build is open).</summary>
        public bool PlayAvailable { get; private set; }

        // ---------------- IActivityHubModel ----------------

        public int CardCount => _cardCount;

        public ActivityCardModel Card(int index) => (uint)index < (uint)_cardCount ? _cards[index] : default;

        public int SelectedCard => _selected;

        public TicketsModel Tickets => _tickets;

        public bool Practice => _practice;

        public int ChallengeCount => _challengeCount;

        public ChallengeCellModel Challenge(int index) => (uint)index < (uint)_challengeCount ? _challenges[index] : default;

        public int ThemeCount => _themeCount;

        public ContentId Theme(int index) => (uint)index < (uint)_themeCount ? _themes[index] : ContentId.None;

        public int SelectedTheme => _selectedTheme;

        private ActivityId SelectedActivity => (uint)_selected < (uint)_cardCount ? _cardIds[_selected] : ActivityId.None;

        // ---------------- Play button ----------------

        public void OpenActivities()
        {
            if (!PlayAvailable || InGame)
                return;
            if (Daily != null && Daily.ScreenOpen)
                Daily.CloseToday();
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
            if (_playHint)
            {
                _playHint = false;
                _playHud?.SetPlayHint(false);
            }
            OpenHub();
        }

        private void OpenHub()
        {
            if (_hub == null)
                return;
            _services.Tickets.Refresh();
            RebuildTickets();
            RebuildCards();
            _selected = DefaultCard();
            RebuildSelection();
            if (_hub.IsOpen)
                _hub.Refresh();
            else
                _hub.Open(this, _text, this);
            _context.Analytics.ScreenView(ScreenId.ActivityHub);
            ShowCardHint();
        }

        // ---------------- A15: the first Grooming game (doc 01 Contenu, A15) ----------------

        private bool _cardHint;

        /// <summary>The free first game of Grooming, never played yet: the game shows its gestures and tips.</summary>
        private bool IsTutorialGame(in ActivityRequest request) =>
            request.Activity == ActivityId.Grooming && request.Mode != ActivityMode.Practice
            && _services.FreeGameAvailable(ActivityId.Grooming) && _services.Record(ActivityId.Grooming).GamesPlayed == 0;

        /// <summary>A15: the paw on the Grooming card (selected first as « Nouveau ») until the first game starts.</summary>
        private void ShowCardHint()
        {
            if (_refugeHud == null || _hub == null || !_hub.IsOpen)
                return;
            bool pending = _services.IsAvailable(ActivityId.Grooming) && _services.FreeGameAvailable(ActivityId.Grooming)
                           && _services.Record(ActivityId.Grooming).GamesPlayed == 0;
            int card = -1;
            for (int i = 0; i < _cardCount; i++)
                if (_cardIds[i] == ActivityId.Grooming)
                    card = i;
            if (!pending || card < 0)
            {
                HideCardHint();
                return;
            }
            _cardHint = true;
            _refugeHud.SetTapHint(true, _hub.CardScreenPosition(card));
        }

        private void HideCardHint()
        {
            if (!_cardHint)
                return;
            _cardHint = false;
            _refugeHud?.SetTapHint(false, default);
        }

        /// <summary>Today opens over the refuge: the hub steps aside without a sound.</summary>
        public void CloseHubQuietly()
        {
            if (_hub != null && _hub.IsOpen)
                _hub.Close();
        }

        // ---------------- T38: Grooming from the animal sheet ----------------

        /// <summary>Grooming can start from a sheet now (open in this build, no game running); the next game is free.</summary>
        public bool CanGroom(out bool free)
        {
            free = _services.FreeGameAvailable(ActivityId.Grooming);
            return PlayAvailable && !InGame && _services.IsAvailable(ActivityId.Grooming);
        }

        /// <summary>The sheet's « Brosser »: a game with this animal (practice when the hub's practice toggle is on).</summary>
        public void GroomAnimal(uint instanceId)
        {
            if (!CanGroom(out _))
                return;
            var request = _practice ? ActivityRequest.Practice(ActivityId.Grooming) : ActivityRequest.Free(ActivityId.Grooming);
            request.SetAnimals(instanceId);
            _context.Audio.Play(AudioEventId.UiConfirm, Vector3.zero, default);
            StartGame(request);
        }

        // ---------------- IActivityHubActions ----------------

        public void SelectActivity(int card)
        {
            if ((uint)card >= (uint)_cardCount || card == _selected)
                return;
            _selected = card;
            RebuildSelection();
            _hub?.Refresh();
            _context.Audio.Play(AudioEventId.UiTab, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
        }

        public void Play()
        {
            var activity = SelectedActivity;
            if (activity == ActivityId.None)
                return;
            var request = _practice ? ActivityRequest.Practice(activity) : ActivityRequest.Free(activity);
            StartGame(request);
        }

        public void PlayChallenge(int challenge)
        {
            var activity = SelectedActivity;
            if (activity == ActivityId.None || (uint)challenge >= (uint)_challengeCount)
                return;
            var request = _practice ? ActivityRequest.Practice(activity, challenge) : ActivityRequest.ChallengeGame(activity, challenge);
            StartGame(request);
        }

        public void SetPractice(bool practice)
        {
            if (_practice == practice)
                return;
            _practice = practice;
            _context.Audio.Play(practice ? AudioEventId.ToggleOn : AudioEventId.ToggleOff, Vector3.zero, default);
            _hub?.Refresh();
        }

        public void SetTheme(int theme)
        {
            if ((uint)theme >= (uint)_themeCount || theme == _selectedTheme)
                return;
            _selectedTheme = theme;
            _services.SetTheme(SelectedActivity, _themes[theme]);
            _context.Audio.Play(AudioEventId.UiTab, Vector3.zero, default);
            _hub?.Refresh();
        }

        public void BuyTickets()
        {
            var tickets = _services.Tickets;
            if (!tickets.CanBuyPack || !tickets.TryBuyPack())
            {
                _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
                _context.Haptics.Play(HapticEventId.ErrorSoft);
                _refugeHud?.PulseNotEnough();
                return;
            }
            _context.Audio.Play(AudioEventId.PurchaseComplete, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.PurchaseSuccess);
            RebuildTickets();
            if (EmptyOpen)
            {
                // The player bought tickets to play: the game they asked for starts at once.
                _emptyChoice = 3;
                CloseEmpty();
                StartGame(_pending);
                return;
            }
            _hub?.Refresh();
        }

        public void CloseActivities()
        {
            if (_hub == null || !_hub.IsOpen)
                return;
            HideCardHint();
            _hub.Close();
            _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);
            // Leaving the activities releases their content (doc Mini-jeux, section 1: loaded at the opening, freed at the exit).
            Stages?.ReleaseAll();
        }

        // ---------------- ITicketsEmptyActions ----------------

        public void PlayPractice()
        {
            _emptyChoice = 1;
            CloseEmpty();
            StartGame(ActivityRequest.Practice(_pending.Activity, _pending.Mode == ActivityMode.Challenge ? _pending.Challenge : -1));
        }

        public void TicketsEmptyClosed() => ReportEmpty();

        private void OpenTicketsEmpty(in ActivityRequest request)
        {
            if (_empty == null)
                return;
            _pending = request;
            _emptyChoice = 0;
            _emptyReported = false;
            RebuildTickets();
            _empty.Open(in _tickets, _text, this);
            _context.Audio.Play(AudioEventId.PanelOpen, Vector3.zero, default);
        }

        private void CloseEmpty()
        {
            if (_empty != null && _empty.IsOpen)
                _empty.Close();
            ReportEmpty();
        }

        /// <summary>Once per opening (doc 08, tickets_empty): what the player chose and the time left before a free ticket.</summary>
        private void ReportEmpty()
        {
            if (_emptyReported)
                return;
            _emptyReported = true;
            _context.Analytics.TicketsEmpty(_emptyChoice, _services.Tickets.SecondsToNext);
        }

        // ---------------- A game ----------------

        /// <summary>Starts a game: checks (locked, challenge, ticket), then the stage, the HUD, the start chime and the music.</summary>
        public void StartGame(ActivityRequest request)
        {
            if (_phase == Phase.Loading || _phase == Phase.Playing || _phase == Phase.Ending)
                return;
            if (request.Theme == ContentId.None)
                request.Theme = _services.Theme(request.Activity);
            if (Stages != null && !Stages.SupportsTheme(request.Activity, request.Theme) && ActivityCatalog.TryGet(request.Activity, out var played))
                request.Theme = played.BaseBiome;
            if (request.AnimalCount == 0)
                request.SetAnimals(PickAnimal(request.Activity, request.Challenge));
            var verdict = _services.Runner.CanStart(request);
            if (verdict == ActivityStartResult.NoTicket)
            {
                OpenTicketsEmpty(in request);
                return;
            }
            if (verdict != ActivityStartResult.Started)
            {
                Refuse(verdict, request.Activity);
                return;
            }
            var stage = Stages?.Acquire(request.Activity, _host);
            if (stage == null)
            {
                Refuse(ActivityStartResult.NoRules, request.Activity);
                return;
            }
            if (_phase == Phase.Result)
                LeaveResult();
            if (_host != null)
                _host.Tutorial = IsTutorialGame(in request);
            HideCardHint();
            _stage = stage;
            _pending = request;
            _phase = Phase.Loading;
            if (_hub != null && _hub.IsOpen)
                _hub.Close();
            _playHud?.SetPlaying(true);
            LoadMusic(request.Activity);
            // The stage builds the played animal and measures it before the runner begins the game (Grooming's zones).
            (stage as IActivityStagePreparation)?.Prepare(in request);
            if (stage.Ready)
                BeginGame();
        }

        private void BeginGame()
        {
            var runner = _services.Runner;
            var verdict = runner.TryStart(_pending, _stage.Controller);
            if (verdict != ActivityStartResult.Started)
            {
                ExitGame(true);
                Refuse(verdict, _pending.Activity);
                return;
            }
            ref readonly var setup = ref runner.Setup;
            _stage.Enter(in setup, _host);
            var model = new ActivityHudModel
            {
                Activity = setup.Activity,
                Mode = setup.Mode,
                Duration = setup.Seconds,
                Star1 = setup.Thresholds.One,
                Star2 = setup.Thresholds.Two,
                Star3 = setup.Thresholds.Three,
                Best = _services.Record(setup.Activity).Best,
            };
            _hud?.Show(in model, _text, this);
            _streakShown = 0;
            _last = _pending;
            _hasLast = true;
            _lastPlayed = setup.Activity;
            _phase = Phase.Playing;
            // J-SFX-ACT-000-01: the start chime, then the music comes in (no countdown).
            _context.Audio.Play(AudioEventId.ActivityStart, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.TapLight);
            if (_musicOn)
                _context.Audio.SetBedLevels(0f, ActivityAudio.AmbienceTrimDb);
            else
                PlayMusicIfReady();
        }

        private void Refuse(ActivityStartResult verdict, ActivityId activity)
        {
            _context.Audio.Play(AudioEventId.ErrorSoft, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.ErrorSoft);
            string text = verdict switch
            {
                ActivityStartResult.Locked => ActivityTextFormatter.UnlockHint(_text, activity, _services.BuildUnlockInput()),
                ActivityStartResult.ChallengeLocked => _text.Get(ActivityText.HubChallengeLocked),
                _ => _text.Get(ActivityText.UnlockSoon),
            };
            _refugeHud?.ShowToast(ToastKind.Unlock, text);
        }

        private void OnEnded(in ActivityGameResult result)
        {
            if (_phase != Phase.Playing)
                return;
            _stage?.End();
            if (result.Abandoned)
            {
                ExitGame(true);
                return;
            }
            _ended = result;
            _phase = Phase.Ending;
            _resultAt = _time + ResultDelay;
            // J-SFX-ACT-000-02: the end chord; the music steps back under the result.
            _context.Audio.Play(AudioEventId.ActivityEnd, Vector3.zero, default);
            _context.Audio.SetBedLevels(ActivityAudio.ResultMusicTrimDb, ActivityAudio.AmbienceTrimDb);
            _context.Haptics.Play(HapticEventId.Confirm);
        }

        private void OpenResult()
        {
            _phase = Phase.Result;
            _hud?.Hide();
            ref readonly var r = ref _ended;
            int slot = ActivityCatalog.SlotOf(r.Activity);
            bool quick = slot >= 0 && _playedThisSession[slot];
            if (slot >= 0)
                _playedThisSession[slot] = true;
            var model = new ActivityResultModel
            {
                Activity = r.Activity,
                Mode = r.Mode,
                ChallengeIndex = r.Challenge,
                Score = r.Score,
                Best = r.Best,
                Stars = (byte)Math.Max(0, Math.Min(3, r.Stars)),
                NewRecord = r.NewRecord,
                Rewarded = r.Rewarded,
                ChallengeImproved = r.ChallengeImproved,
                QuickReveal = quick,
                FixedStickerRule = r.Rewards.FixedStickerRule,
                CanReplay = r.Mode == ActivityMode.Practice || _services.Tickets.Count > 0 || _services.FreeGameAvailable(r.Activity),
            };
            int count = r.Rewarded ? BuildCells(in r.Rewards) : 0;
            if (_result != null)
                _result.Open(in model, _cells, count, _text, this);
            else
                BackToHub();
            _context.Analytics.ScreenView(ScreenId.ActivityResult);
        }

        /// <summary>The reward cells of a game, in the doc's display order (Gems, Light, then the rest).</summary>
        private int BuildCells(in ActivityRewardOutcome rewards)
        {
            int n = 0;
            if (rewards.Gems > 0)
                _cells[n++] = new RewardCell(RewardKind.Gems, rewards.Gems);
            if (rewards.LightMinutes > 0.0)
                _cells[n++] = new RewardCell(RewardKind.Light, 0) { LightMinutes = rewards.LightMinutes, Light = rewards.Light };
            if (rewards.BondAnimals > 0 && rewards.BondPerAnimal > 0)
                _cells[n++] = new RewardCell(RewardKind.Bond, rewards.BondPerAnimal);
            if (rewards.Decoration != ContentId.None)
                _cells[n++] = new RewardCell(RewardKind.Decoration, 1, rewards.Decoration);
            // The activity's own item (Grooming: a Clover Crunch at 3 stars), merged with the sticker's treats when the same.
            int itemTreats = rewards.ItemTreats;
            if (rewards.Treats > 0)
            {
                bool same = itemTreats > 0 && rewards.ItemTreat == rewards.Treat;
                _cells[n++] = new RewardCell(RewardKind.Treat, rewards.Treats + (same ? itemTreats : 0), rewards.Treat);
                if (same)
                    itemTreats = 0;
            }
            else if (rewards.Stickers > 0)
            {
                _cells[n++] = new RewardCell(RewardKind.Sticker, rewards.Stickers);
            }
            if (itemTreats > 0 && n < _cells.Length)
                _cells[n++] = new RewardCell(RewardKind.Treat, itemTreats, rewards.ItemTreat);
            return n;
        }

        // ---------------- IActivityHudActions ----------------

        public void PauseActivity()
        {
            if (_phase != Phase.Playing || _services.Runner.IsPaused)
                return;
            _services.Runner.Pause();
            _stage?.SetPaused(true);
            _hud?.SetPaused(true);
            _context.Audio.PlayVariant(AudioEventId.ActivityPause, 0);
        }

        public void ResumeActivity()
        {
            if (_phase != Phase.Playing || !_services.Runner.IsPaused)
                return;
            _services.Runner.Resume();
            _stage?.SetPaused(false);
            _hud?.SetPaused(false);
            _context.Audio.PlayVariant(AudioEventId.ActivityPause, 1);
        }

        public void QuitActivity()
        {
            if (_phase != Phase.Playing)
                return;
            _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);
            // The runner ends the game as abandoned (no reward; a ticket spent in the first seconds comes back).
            _services.Runner.Quit();
        }

        /// <summary>The app goes to the background mid-game: the game pauses (the player finds the pause card on return).</summary>
        public void OnApplicationPause(bool paused)
        {
            if (paused && _phase == Phase.Playing && !_services.Runner.IsPaused)
            {
                _services.Runner.Pause();
                _stage?.SetPaused(true);
                _hud?.SetPaused(true);
            }
        }

        // ---------------- IActivityResultActions ----------------

        public void PlayAgain()
        {
            if (_phase != Phase.Result || !_hasLast)
                return;
            _context.Audio.Play(AudioEventId.UiConfirm, Vector3.zero, default);
            var request = _last;
            request.SetAnimals(0);
            request.AnimalCount = 0;
            StartGame(request);
        }

        public void BackToHub()
        {
            if (_phase != Phase.Result && _phase != Phase.Ending)
                return;
            _context.Audio.Play(AudioEventId.UiBack, Vector3.zero, default);
            LeaveResult();
            ExitGame(true);
        }

        private void LeaveResult()
        {
            if (_result != null && _result.IsOpen)
                _result.Close();
            _stage?.Exit();
            _host?.SetWorldVisible?.Invoke(true);
            _phase = Phase.None;
        }

        /// <summary>Back from a game: stage away, the refuge HUD and its music back, the hub open again (or the refuge).</summary>
        private void ExitGame(bool openHub)
        {
            if (_phase == Phase.Playing || _phase == Phase.Loading || _phase == Phase.Ending)
                _stage?.Exit();
            _stage = null;
            _phase = Phase.None;
            _hud?.Hide();
            _host?.SetWorldVisible?.Invoke(true);
            _playHud?.SetPlaying(false);
            RestoreBeds();
            if (openHub)
                OpenHub();
        }

        // ---------------- IActivityResultBeats (doc 05 sheet 20) ----------------

        public void ResultStarLanded(int star)
        {
            _context.Audio.PlayVariant(AudioEventId.StarEarned, star);
            _context.Haptics.Play(star >= 2 ? HapticEventId.Confirm : HapticEventId.TapLight);
        }

        public void ResultRewardShown(int reward) => _context.Audio.PlayVariant(AudioEventId.CardFlip, reward);

        public void ResultRecordShown()
        {
            _context.Audio.Play(AudioEventId.ActivityRecord, Vector3.zero, default);
            _context.Haptics.Play(HapticEventId.Confirm);
        }

        public void ResultRevealedAll() => _context.Audio.Play(AudioEventId.Claim, Vector3.zero, default);

        // ---------------- Tick ----------------

        /// <summary>Per frame from the scene entry: game time for the game, unscaled time for the screens. No allocation.</summary>
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            _time += unscaledDeltaTime;
            _hub?.Tick(unscaledDeltaTime);
            _empty?.Tick(unscaledDeltaTime);
            _hud?.Tick(unscaledDeltaTime);
            _result?.Tick(unscaledDeltaTime);

            switch (_phase)
            {
                case Phase.Loading:
                    if (_stage != null && _stage.Ready)
                        BeginGame();
                    break;
                case Phase.Playing:
                    TickGame(deltaTime, unscaledDeltaTime);
                    break;
                case Phase.Ending:
                    _stage?.Tick(deltaTime, unscaledDeltaTime);
                    if (_time >= _resultAt)
                        OpenResult();
                    break;
                case Phase.Result:
                    _stage?.Tick(deltaTime, unscaledDeltaTime);
                    break;
            }
            if (_musicOn == false && _phase == Phase.Playing)
                PlayMusicIfReady();
            else if (_phase == Phase.None && _time >= _musicReleaseAt)
            {
                _musicReleaseAt = float.MaxValue;
                ReleaseMusic();
            }

            if (_time < _nextCheck)
                return;
            _nextCheck = _time + CheckInterval;
            UpdatePlayButton();
            if (HubOpen || EmptyOpen)
            {
                int minutes = _services.Tickets.MinutesToNext;
                if (_ticketsDirty || minutes != _tickets.MinutesToNext)
                {
                    RebuildTickets();
                    if (HubOpen)
                        _hub.Refresh();
                    if (EmptyOpen)
                        _empty.Refresh(in _tickets);
                }
            }
        }

        private void TickGame(float deltaTime, float unscaledDeltaTime)
        {
            var runner = _services.Runner;
            if (!runner.IsPaused)
                runner.Tick(Mathf.Min(deltaTime, MaxGameDelta));
            // The runner may have ended the game in that tick (OnEnded already moved the phase on).
            if (_phase != Phase.Playing)
                return;
            _stage?.Tick(deltaTime, unscaledDeltaTime);
            if (_hud == null)
                return;
            _hud.SetScore(runner.Score);
            _hud.SetTimeLeft(runner.TimeLeft);
            int streak = runner.Streak;
            _hud.SetStreak(streak);
            // Activities with their own streak sound (Grooming's rising glass) keep the shared one quiet.
            if (streak > _streakShown && streak >= 2 && !ActivityAudio.HasOwnStreakSound(runner.Setup.Activity))
                _context.Audio.PlayVariant(AudioEventId.ActivityStreak, streak - 2);
            _streakShown = streak;
        }

        // ---------------- The Play button (doc 01 A15) ----------------

        private void UpdatePlayButton()
        {
            if (_playHud == null)
                return;
            bool available = false;
            bool dot = false;
            for (int slot = 0; slot < ActivityCatalog.SlotCount; slot++)
            {
                var id = ActivityCatalog.IdOfSlot(slot);
                if (!_services.IsAvailable(id))
                    continue;
                available = true;
                if (_services.FreeGameAvailable(id) || _services.Activities.Records[slot].GamesPlayed == 0)
                    dot = true;
            }
            bool firstAppearance = available && !_playShown && !_playAppeared
                                   && !_context.State.Analytics.Has(FunnelMilestone.FirstActivity);
            // A15: the first appearance waits until no screen covers the refuge and no hatch plays.
            var refuge = _host?.Refuge;
            if (firstAppearance && refuge != null && (refuge.ModalOpen || refuge.Hatching))
                return;
            PlayAvailable = available;
            int tickets = available ? _services.Tickets.Count : 0;
            if (available == _playShown && tickets == _playTickets && dot == _playDot)
                return;
            bool appearing = available && !_playShown;
            _playShown = available;
            _playTickets = tickets;
            _playDot = dot;
            _playHud.SetPlay(available, tickets, dot);
            if (appearing && firstAppearance)
            {
                // A15: the button arrives with a small bounce, then the paw shows it once.
                _playAppeared = true;
                _playHud.PlayPlayAppear();
                _context.Audio.Play(AudioEventId.UiConfirm, Vector3.zero, default);
                _playHint = true;
                _playHud.SetPlayHint(true);
            }
        }

        // ---------------- Models ----------------

        private void OnTicketsChanged() => _ticketsDirty = true;

        private void RebuildTickets()
        {
            _ticketsDirty = false;
            var t = _services.Tickets;
            _tickets = new TicketsModel
            {
                Count = t.Count,
                Max = t.Max,
                MinutesToNext = t.MinutesToNext,
                PackPrice = t.PackPrice,
                PackSize = t.PackSize,
                PacksLeftToday = t.PacksLeftToday,
                Gems = _context.State.Wallet.Gems,
                AdAvailable = false,
            };
        }

        /// <summary>
        /// The hub's cards: the activities of this build, in their opening order (the open ones come first by design), each
        /// with its record, its challenges and what opens it. Built when the hub opens or a game ends.
        /// </summary>
        private void RebuildCards()
        {
            _cardCount = 0;
            var input = _services.BuildUnlockInput();
            for (int pass = 0; pass < 2; pass++)
            {
                for (int slot = 0; slot < ActivityCatalog.SlotCount; slot++)
                {
                    var id = ActivityCatalog.IdOfSlot(slot);
                    if (!_services.IsShipped(id) || !_services.Config.IsEnabled(id))
                        continue;
                    bool unlocked = _services.IsUnlocked(id);
                    if (unlocked != (pass == 0))
                        continue;
                    var record = _services.Activities.Records[slot];
                    _cardIds[_cardCount] = id;
                    _cards[_cardCount++] = new ActivityCardModel
                    {
                        Activity = id,
                        State = !unlocked ? ActivityCardState.Locked : record.GamesPlayed == 0 ? ActivityCardState.New : ActivityCardState.Open,
                        Name = _text.Get(ActivityText.NameKey(id)),
                        UnlockHint = unlocked ? null : ActivityTextFormatter.UnlockHint(_text, id, input),
                        Best = record.Best,
                        BestStars = record.BestStars,
                        ChallengesDone = _services.ChallengesDone(id),
                        // The first Grooming game is offered; Expeditions and the Garden never take a ticket.
                        FreeGame = unlocked && (_services.FreeGameAvailable(id) || !ActivityCatalog.Get(id).UsesTickets),
                        Featured = _services.Events != null && _services.Events.IsFeatured(id),
                    };
                }
            }
        }

        /// <summary>The card the hub opens on: the last activity played, else the first new one, else the first open one.</summary>
        private int DefaultCard()
        {
            int firstNew = -1;
            for (int i = 0; i < _cardCount; i++)
            {
                if (_cardIds[i] == _lastPlayed && _cards[i].State != ActivityCardState.Locked)
                    return i;
                if (firstNew < 0 && _cards[i].State == ActivityCardState.New)
                    firstNew = i;
            }
            return firstNew >= 0 ? firstNew : 0;
        }

        /// <summary>The selected activity's 10 challenges and theme pills.</summary>
        private void RebuildSelection()
        {
            _challengeCount = 0;
            _themeCount = 0;
            _selectedTheme = 0;
            var activity = SelectedActivity;
            if (activity == ActivityId.None || !ActivityCatalog.TryGet(activity, out var definition))
                return;
            bool open = _services.IsUnlocked(activity);
            if (_services.Rules.Has(activity) && definition.UsesTickets)
            {
                for (int i = 0; i < ActivityCatalog.ChallengesPerActivity; i++)
                {
                    string goalKey = definition.ChallengeId(i) + ".goal";
                    string goal = _text.Has(goalKey) ? _text.Get(goalKey) : _text.Get(ActivityText.Challenge).Replace("{n}", (i + 1).ToString());
                    _challenges[_challengeCount++] = new ChallengeCellModel
                    {
                        Index = i,
                        Stars = (byte)_services.ChallengeStars(activity, i),
                        Locked = !open || !_services.IsChallengeOpen(activity, i),
                        Goal = goal,
                    };
                }
            }
            // Themes: one per open biome (doc 01, section 12.0 « Thème par biome »); the last choice is kept.
            var chosen = _services.Theme(activity);
            var biomes = _context.Content.Biomes;
            var store = _context.Progression?.Store;
            for (int i = 0; i < biomes.Count && _themeCount < MaxThemes; i++)
            {
                var biome = biomes[i].Id;
                if (store != null ? !store.IsUnlocked(biome) : biome != definition.BaseBiome)
                    continue;
                // Only the themes whose set exists in this build (the others open with their sets).
                if (Stages != null && !Stages.SupportsTheme(activity, biome))
                    continue;
                if (biome == chosen)
                    _selectedTheme = _themeCount;
                _themes[_themeCount++] = biome;
            }
        }

        /// <summary>The challenge of an activity that needs the heaviest animal owned (Grooming 7 « Grand gabarit »).</summary>
        public static bool NeedsHeaviestAnimal(ActivityId activity, int challenge) =>
            activity == ActivityId.Grooming && challenge == GroomingRules.HeavyAnimalChallenge;

        /// <summary>
        /// The animal a game features (doc Mini-jeux, section 1: « c'est son lapin qu'on brosse »). Grooming proposes the
        /// visible animal with the lowest bond (doc 01 S-ACT-001), its « Grand gabarit » challenge the heaviest one owned;
        /// the others a favorite first. Runs once per game start (never per frame).
        /// </summary>
        private uint PickAnimal(ActivityId activity, int challenge)
        {
            var collection = _context.State.Collection;
            int count = collection.Count;
            if (count == 0)
                return 0;
            if (NeedsHeaviestAnimal(activity, challenge))
                return HeaviestAnimal(collection);
            if (activity == ActivityId.Grooming)
            {
                // A pending wish « Brosse {animal} » (WISH-019) names its animal: it comes first.
                if (_context.Daily != null && _context.Daily.Wishes.GroomWishAnimal(out uint wished) && collection.TryGet(wished, out _))
                    return wished;
                uint visible = LowestBond(collection, true);
                return visible != 0 ? visible : LowestBond(collection, false);
            }
            int start = _services.Activities.GamesStarted % count;
            for (int i = 0; i < count; i++)
            {
                ref readonly var animal = ref collection[(start + i) % count];
                if (animal.IsFavorite)
                    return animal.InstanceId;
            }
            return collection[start].InstanceId;
        }

        private readonly uint[] _visible = new uint[64];

        private uint LowestBond(CollectionState collection, bool visibleOnly)
        {
            int visible = visibleOnly && _host?.Refuge != null ? _host.Refuge.CopyVisibleAnimals(_visible) : 0;
            if (visibleOnly && visible == 0)
                return 0;
            uint best = 0;
            int bestBond = int.MaxValue;
            int n = visibleOnly ? visible : collection.Count;
            for (int i = 0; i < n; i++)
            {
                AnimalInstance animal;
                if (visibleOnly)
                {
                    if (!collection.TryGet(_visible[i], out animal))
                        continue;
                }
                else
                {
                    animal = collection[i];
                }
                if (animal.BondPoints < bestBond)
                {
                    bestBond = animal.BondPoints;
                    best = animal.InstanceId;
                }
            }
            return best;
        }

        /// <summary>The heaviest animal owned: the biggest rig family first.</summary>
        private uint HeaviestAnimal(CollectionState collection)
        {
            uint best = 0;
            int bestFamily = -1;
            for (int i = 0; i < collection.Count; i++)
            {
                ref readonly var animal = ref collection[i];
                int family = _context.Content.Species.TryGet(animal.Species, out var species) ? HeavyRank(species.RigFamily) : 0;
                if (family > bestFamily)
                {
                    bestFamily = family;
                    best = animal.InstanceId;
                }
            }
            return best;
        }

        private static int HeavyRank(PocketWilds.Content.RigFamily family) =>
            family == PocketWilds.Content.RigFamily.HeavyQuadruped ? 3 : family == PocketWilds.Content.RigFamily.MediumQuadruped ? 2 : 1;

        // ---------------- Music (doc 01 Contenu, section 12.0) ----------------

        private void LoadMusic(ActivityId activity)
        {
            string address = ActivityAudio.Address(ActivityAudio.MusicOf(activity));
            _musicReleaseAt = float.MaxValue;
            // A replay keeps the same track: the music runs on instead of starting over.
            if (_music.IsValid() && string.Equals(_musicAddress, address, StringComparison.Ordinal))
                return;
            ReleaseMusic();
            _musicOn = false;
            if (string.IsNullOrEmpty(address) || !AudioAddressExists(address))
                return;
            _musicAddress = address;
            _music = Addressables.LoadAssetAsync<AudioClip>(address);
        }

        private static bool AudioAddressExists(string address)
        {
            foreach (var locator in Addressables.ResourceLocators)
                if (locator.Locate(address, typeof(AudioClip), out _))
                    return true;
            return false;
        }

        /// <summary>The activity's music, with the refuge biome's ambience under it at -6 dB, once its clip is loaded.</summary>
        private void PlayMusicIfReady()
        {
            if (_musicOn || !_music.IsValid() || !_music.IsDone || _music.Status != AsyncOperationStatus.Succeeded)
                return;
            _musicOn = true;
            var beds = RefugeBeds;
            _context.Audio.SetBeds(_music.Result, beds != null ? beds.ambience : null, ActivityAudio.MusicFadeSeconds);
            _context.Audio.SetBedLevels(0f, ActivityAudio.AmbienceTrimDb);
        }

        private void RestoreBeds()
        {
            _context.Audio.SetBedLevels(0f, 0f);
            float fade = RefugeBeds != null ? RefugeBeds.fadeSeconds : ActivityAudio.MusicFadeSeconds;
            if (_musicOn)
            {
                var beds = RefugeBeds;
                _context.Audio.SetBeds(beds != null ? beds.music : null, beds != null ? beds.ambience : null, fade);
            }
            _musicOn = false;
            _musicReleaseAt = _time + fade + 0.5f;
        }

        private void ReleaseMusic()
        {
            if (_music.IsValid())
                Addressables.Release(_music);
            _music = default;
            _musicAddress = null;
            _musicOn = false;
        }

        public void Dispose()
        {
            _services.Runner.Ended -= _onEnded;
            _services.Tickets.Changed -= _onTicketsChanged;
            if (_phase == Phase.Playing)
                _services.Runner.Quit();
            _stage?.Exit();
            _stage = null;
            Stages?.ReleaseAll();
            ReleaseMusic();
        }

        /// <summary>Headless scenes without localization: keys stand for texts.</summary>
        private sealed class KeysOnly : ILocalization
        {
            public static readonly KeysOnly Instance = new KeysOnly();

            public string Language => "en";

            public string Get(string key) => key;
        }
    }
}
