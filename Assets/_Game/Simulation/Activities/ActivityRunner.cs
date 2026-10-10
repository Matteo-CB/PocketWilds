#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// One activity's game logic (doc « Mini-jeux &amp; activités », sections 1 and 2 « Données »: a plain class ticked by
    /// the runner, no Update per object). Pure C#: the activity's scene objects read it. Ticked only between
    /// <see cref="Begin"/> and the end, never while paused; must not allocate in <see cref="Tick"/>.
    /// </summary>
    public interface IActivityController
    {
        /// <summary>A new game: everything the controller needs (parameters at d, seed, duration, animals, theme).</summary>
        void Begin(in ActivitySetup setup);

        void Tick(float deltaSeconds);

        void Pause();

        void Resume();

        /// <summary>The score shown (and the free mode's star measure).</summary>
        int Score { get; }

        /// <summary>What the challenge's thresholds measure (its score, longest streak, berries…); the score for most.</summary>
        int ChallengeScore { get; }

        /// <summary>The current streak for the HUD (0 when the activity has none).</summary>
        int Streak { get; }

        /// <summary>The game ended on its own rule (all clean, the last pair, the exit…). The runner also ends it at its time.</summary>
        bool Finished { get; }
    }

    /// <summary>What the hub asks for: an activity, a mode, a challenge, the theme and the animals played.</summary>
    public struct ActivityRequest
    {
        public ActivityId Activity;
        public ActivityMode Mode;

        /// <summary>0 to 9 for a challenge (rewarded or practiced), −1 for the free mode.</summary>
        public int Challenge;

        /// <summary>The theme's biome (None: the activity's base biome).</summary>
        public ContentId Theme;

        public int AnimalCount;
        public uint Animal0;
        public uint Animal1;
        public uint Animal2;

        public static ActivityRequest Free(ActivityId activity, uint animal = 0) => Make(activity, ActivityMode.Free, -1, animal);

        public static ActivityRequest Practice(ActivityId activity, int challenge = -1, uint animal = 0) =>
            Make(activity, ActivityMode.Practice, challenge, animal);

        public static ActivityRequest ChallengeGame(ActivityId activity, int challenge, uint animal = 0) =>
            Make(activity, ActivityMode.Challenge, challenge, animal);

        public void SetAnimals(uint a, uint b = 0, uint c = 0)
        {
            Animal0 = a;
            Animal1 = b;
            Animal2 = c;
            AnimalCount = a == 0 ? 0 : b == 0 ? 1 : c == 0 ? 2 : 3;
        }

        private static ActivityRequest Make(ActivityId activity, ActivityMode mode, int challenge, uint animal)
        {
            var request = new ActivityRequest { Activity = activity, Mode = mode, Challenge = challenge };
            request.SetAnimals(animal);
            return request;
        }
    }

    /// <summary>
    /// Everything a controller receives at <see cref="IActivityController.Begin"/> (doc 01 Contenu, section 12.0): the mode,
    /// the challenge, d (or the challenge's fixed level), the game's deterministic seed, its duration (× 1.25 with « slower
    /// activities ») and speed multiplier (× 0.8), the animals, the theme and the rules.
    /// </summary>
    public struct ActivitySetup
    {
        public ActivityId Activity;
        public ActivityMode Mode;
        public int Challenge;
        public int Difficulty;
        public uint Seed;

        /// <summary>Seconds of the game (0: no timer), slower mode applied.</summary>
        public float Seconds;

        public bool Slower;

        /// <summary>Every speed × this (0.8 with « slower activities », else 1).</summary>
        public float SpeedMultiplier;

        public ContentId Theme;

        /// <summary>The featured activity of a running event (its objects and colors).</summary>
        public bool Featured;

        public int AnimalCount;
        public uint Animal0;
        public uint Animal1;
        public uint Animal2;

        public ActivityRules Rules;
        public StarThresholds Thresholds;

        public bool Rewarded => Mode != ActivityMode.Practice;

        public uint Animal(int index) => index == 0 ? Animal0 : index == 1 ? Animal1 : Animal2;

        /// <summary>A parameter of this game: the challenge's fixed value, else the value at d.</summary>
        public double Param(int index) => Rules.Value(index, Difficulty, Challenge);

        public int ParamInt(int index) => (int)Math.Round(Param(index), MidpointRounding.AwayFromZero);
    }

    public enum ActivityStartResult : byte
    {
        Started = 0,

        /// <summary>A game is already running.</summary>
        Busy = 1,

        Unknown = 2,
        Locked = 3,

        /// <summary>Remote Config switched it off (doc 08 flags.activity.*).</summary>
        Disabled = 4,

        /// <summary>Its rules are not registered in this build.</summary>
        NoRules = 5,

        /// <summary>The challenge waits for one star on the previous one.</summary>
        ChallengeLocked = 6,

        /// <summary>No ticket for a rewarded game: the « no ticket » card (practice stays open).</summary>
        NoTicket = 7,

        /// <summary>Expeditions and the Garden have their own screens.</summary>
        Meta = 8,
    }

    /// <summary>A game started (analytics 110 ActivityStart).</summary>
    public struct ActivityStartInfo
    {
        public ActivityId Activity;
        public ActivityMode Mode;
        public int Challenge;
        public int Difficulty;

        /// <summary>Tickets left after this start.</summary>
        public int Tickets;

        public bool Featured;
        public bool Rewarded;
        public bool PaidTicket;
        public bool FreeGame;

        /// <summary>doc 08 « Mode d'activité »: 1 rewarded, 2 practice, 3 challenge.</summary>
        public int AnalyticsMode => ActivityAnalytics.Mode(Mode, Challenge);
    }

    /// <summary>A game ended, finished or quit (analytics 111 ActivityEnd and 112 ActivityChallenge; the result card).</summary>
    public struct ActivityGameResult
    {
        public ActivityId Activity;
        public ActivityMode Mode;
        public int Challenge;

        /// <summary>d of this game, and d for the next one (free mode only moves).</summary>
        public int Difficulty;
        public int DifficultyNext;

        public int Score;

        /// <summary>The best score after this game.</summary>
        public int Best;

        public int Stars;
        public bool NewRecord;

        /// <summary>The first score of a rewarded free-mode game (nothing to beat yet: no record sticker).</summary>
        public bool FirstScore;

        public bool Rewarded;
        public bool PaidTicket;
        public bool FreeGame;
        public bool Abandoned;

        /// <summary>A game quit within the first seconds gave its ticket back.</summary>
        public bool Refunded;

        /// <summary>A rewarded challenge reached more stars than before (analytics 112).</summary>
        public bool ChallengeImproved;
        public int ChallengeStarsBefore;

        /// <summary>Rewarded attempts of this challenge, this one included.</summary>
        public int ChallengeAttempts;

        /// <summary>Seconds played (pauses excluded).</summary>
        public double Seconds;

        /// <summary>Tickets owned after the game.</summary>
        public int Tickets;

        public ActivityRewardOutcome Rewards;

        /// <summary>doc 08 « Mode d'activité »: 1 rewarded, 2 practice, 3 challenge.</summary>
        public int AnalyticsMode => ActivityAnalytics.Mode(Mode, Challenge);

        /// <summary>doc 08 « Drapeaux de fin de partie »: 1 record, 2 abandon, 4 challenge succeeded, 8 ticket bought with Gems.</summary>
        public int AnalyticsFlags =>
            (NewRecord ? 1 : 0) | (Abandoned ? 2 : 0) | (ChallengeImproved ? 4 : 0) | (PaidTicket ? 8 : 0);
    }

    /// <summary>The doc 08 enumerations of the activity events (110 to 114).</summary>
    public static class ActivityAnalytics
    {
        public static int Mode(ActivityMode mode, int challenge) =>
            mode == ActivityMode.Practice ? 2 : mode == ActivityMode.Challenge || challenge >= 0 ? 3 : 1;

        /// <summary>
        /// doc 08 « Source de billets » (114): 1 refill, 2 ad, 3 Gems, 4 daily gift, 5 event, 6 Circle, 7 journal, 8 Pass,
        /// 9 album reward, 10 return gift, 11 Premiers pas (the welcome gifts); appended after the doc's list (T37):
        /// 13 calendar, 14 visitor, 15 wish, 16 debug.
        /// </summary>
        public static int TicketSourceCode(TicketSource source)
        {
            switch (source)
            {
                case TicketSource.Refill: return 1;
                case TicketSource.Ad: return 2;
                case TicketSource.Gems: return 3;
                case TicketSource.DailyGift: return 4;
                case TicketSource.Event: return 5;
                case TicketSource.Circle: return 6;
                case TicketSource.WeeklyJournal: return 7;
                case TicketSource.Album: return 9;
                case TicketSource.ReturnGift: return 10;
                case TicketSource.WelcomeGift: return 11;
                case TicketSource.Calendar: return 13;
                case TicketSource.Visitor: return 14;
                case TicketSource.Wish: return 15;
                case TicketSource.Debug: return 16;
                // T42: not in doc 08's list yet (appended after it, like Calendar, Visitor and Wish).
                case TicketSource.Expedition: return 17;
                default: return 0;
            }
        }
    }

    public delegate void ActivityStartedHandler(in ActivityStartInfo info);

    public delegate void ActivityEndedHandler(in ActivityGameResult result);

    /// <summary>
    /// The session of one activity game (doc « Mini-jeux &amp; activités » section 2; doc 01 Contenu section 12.0 and A15):
    /// <list type="bullet">
    /// <item><see cref="TryStart"/>: the activity is open, enabled and has its rules, the challenge is open; a rewarded game
    /// takes the free first game (Grooming) or spends a ticket now (free tickets first); practice is free and unlimited.</item>
    /// <item><see cref="Tick"/> drives the controller, never while paused, and ends the game on its rule or at its time.</item>
    /// <item><see cref="Quit"/>: no reward; a ticket spent less than 3 seconds ago is given back, the free first game stays
    /// free, d and the records do not move.</item>
    /// <item>The end: stars from the free mode's or the challenge's thresholds, the record (rewarded free-mode games),
    /// the challenge's stars (rewarded challenges), the rewards (<see cref="ActivityRewardService"/>, rewarded games only),
    /// d (free-mode games, practice included; never challenges), then <see cref="Ended"/>.</item>
    /// </list>
    /// Each game has a deterministic seed (the profile and the games started). Nothing allocates per tick.
    /// </summary>
    public sealed class ActivityRunner
    {
        private readonly ActivityServices _services;
        private readonly ActivityContext _ctx;

        private IActivityController? _controller;
        private ActivitySetup _setup;
        private ActivityGameResult _last;
        private bool _running;
        private bool _paused;
        private bool _ticketSpent;
        private bool _paidTicket;
        private bool _freeGame;
        private double _elapsed;

        public ActivityRunner(ActivityServices services)
        {
            _services = services;
            _ctx = services.Context;
        }

        public event ActivityStartedHandler? Started;
        public event ActivityEndedHandler? Ended;

        /// <summary>A rewarded challenge reached a new star count: activity, challenge, stars, attempts (analytics 112).</summary>
        public event Action<ActivityId, int, int, int>? ChallengeImproved;

        public event Action<bool>? PausedChanged;

        public bool IsRunning => _running;
        public bool IsPaused => _paused;
        public IActivityController? Controller => _controller;

        /// <summary>The running (or last) game's setup.</summary>
        public ref readonly ActivitySetup Setup => ref _setup;

        /// <summary>The last game's result (the result card reads it after <see cref="Ended"/>).</summary>
        public ref readonly ActivityGameResult LastResult => ref _last;

        public double Elapsed => _elapsed;

        /// <summary>Seconds left (0 without a timer).</summary>
        public float TimeLeft => _setup.Seconds > 0f ? (float)Math.Max(0.0, _setup.Seconds - _elapsed) : 0f;

        public int Score => _controller?.Score ?? 0;
        public int Streak => _controller?.Streak ?? 0;

        /// <summary>Whether <see cref="TryStart"/> would start, without spending anything (the hub's Play button).</summary>
        public ActivityStartResult CanStart(in ActivityRequest request)
        {
            var check = Check(in request, out var definition);
            if (check != ActivityStartResult.Started || request.Mode == ActivityMode.Practice)
                return check;
            var record = _ctx.Activities.Records[definition.Slot];
            if (definition.FirstGameFree && !record.FreeGameUsed)
                return ActivityStartResult.Started;
            if (definition.UsesTickets && _services.Tickets.Count <= 0)
                return ActivityStartResult.NoTicket;
            return ActivityStartResult.Started;
        }

        /// <summary>Starts a game with the activity's controller; a rewarded game spends its ticket now.</summary>
        public ActivityStartResult TryStart(in ActivityRequest request, IActivityController controller)
        {
            var check = Check(in request, out var definition);
            if (check != ActivityStartResult.Started)
                return check;
            _services.Rules.TryGet(request.Activity, out var rules);
            var state = _ctx.Activities;
            ref var record = ref state.Records[definition.Slot];

            bool rewarded = request.Mode != ActivityMode.Practice;
            bool free = false, spent = false, paid = false;
            if (rewarded)
            {
                if (definition.FirstGameFree && !record.FreeGameUsed)
                    free = true;
                else if (definition.UsesTickets)
                {
                    if (!_services.Tickets.TrySpend(out paid))
                        return ActivityStartResult.NoTicket;
                    spent = true;
                }
            }

            int challenge = request.Mode == ActivityMode.Free ? -1 : request.Challenge;
            bool slower = _services.SlowerActivities;
            // d: the challenge's fixed level, else the free mode's adaptive one. T41: the length and the free mode's thresholds
            // may depend on it (Pawprint Memory's grid: 45, 60 or 90 s), so both are read at this d.
            int level = challenge >= 0 ? rules.Challenges[challenge].Level : AdaptiveDifficulty.Level(in record);
            float seconds = rules.ChallengeSeconds(challenge, rules.SecondsAt(level, definition.Seconds));
            var theme = request.Theme.IsValid ? request.Theme : (record.LastTheme.IsValid ? record.LastTheme : definition.BaseBiome);
            var events = _services.Rewards.Events;
            _setup = new ActivitySetup
            {
                Activity = request.Activity,
                Mode = request.Mode,
                Challenge = challenge,
                Difficulty = level,
                Seed = _ctx.GameSeed(state.GamesStarted, request.Activity),
                Seconds = seconds * (slower ? ActivityConfig.SlowerTimeMultiplier : 1f),
                Slower = slower,
                SpeedMultiplier = slower ? ActivityConfig.SlowerSpeedMultiplier : 1f,
                Theme = theme,
                Featured = events != null && events.EventRunning && events.IsFeatured(request.Activity),
                AnimalCount = Math.Max(0, Math.Min(3, request.AnimalCount)),
                Animal0 = request.Animal0,
                Animal1 = request.Animal1,
                Animal2 = request.Animal2,
                Rules = rules,
                Thresholds = rules.Thresholds(challenge, level),
            };
            state.GamesStarted++;
            if (request.Theme.IsValid)
                record.LastTheme = request.Theme;

            _controller = controller;
            _running = true;
            _paused = false;
            _elapsed = 0.0;
            _ticketSpent = spent;
            _paidTicket = paid;
            _freeGame = free;
            _ctx.RequestSave(spent ? SaveUrgency.Priority : SaveUrgency.Normal);

            controller.Begin(in _setup);
            var info = new ActivityStartInfo
            {
                Activity = _setup.Activity,
                Mode = _setup.Mode,
                Challenge = challenge,
                Difficulty = _setup.Difficulty,
                Tickets = _services.Tickets.Count,
                Featured = _setup.Featured,
                Rewarded = rewarded,
                PaidTicket = paid,
                FreeGame = free,
            };
            Started?.Invoke(in info);
            return ActivityStartResult.Started;
        }

        /// <summary>Per frame while a game runs: the controller's tick, then the end on its rule or at its time. No allocation.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!_running || _paused || deltaSeconds <= 0f)
                return;
            var controller = _controller!;
            _elapsed += deltaSeconds;
            controller.Tick(deltaSeconds);
            if (controller.Finished || (_setup.Seconds > 0f && _elapsed >= _setup.Seconds))
                Finish();
        }

        public void Pause()
        {
            if (!_running || _paused)
                return;
            _paused = true;
            _controller!.Pause();
            PausedChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!_running || !_paused)
                return;
            _paused = false;
            _controller!.Resume();
            PausedChanged?.Invoke(false);
        }

        /// <summary>
        /// Abandons the game: no reward, nothing recorded but the analytics; a ticket spent less than
        /// <see cref="ActivityConfig.QuitRefundSeconds"/> ago comes back. False when no game runs.
        /// </summary>
        public bool Quit()
        {
            if (!_running)
                return false;
            _running = false;
            _paused = false;
            bool refunded = false;
            if (_ticketSpent && _elapsed < _ctx.Config.QuitRefundSeconds)
            {
                _services.Tickets.Refund(_paidTicket);
                refunded = true;
            }
            var record = _ctx.Activities.Records[ActivityCatalog.SlotOf(_setup.Activity)];
            int d = _setup.Challenge >= 0 ? _setup.Difficulty : AdaptiveDifficulty.Level(in record);
            _last = new ActivityGameResult
            {
                Activity = _setup.Activity,
                Mode = _setup.Mode,
                Challenge = _setup.Challenge,
                Difficulty = _setup.Difficulty,
                DifficultyNext = d,
                Score = _controller?.Score ?? 0,
                Best = record.Best,
                Rewarded = false,
                PaidTicket = _paidTicket,
                FreeGame = false,
                Abandoned = true,
                Refunded = refunded,
                Seconds = _elapsed,
                Tickets = _services.Tickets.Count,
            };
            _last.Rewards.Rewarded = false;
            _ctx.RequestSave(SaveUrgency.Normal);
            Ended?.Invoke(in _last);
            return true;
        }

        /// <summary>Ends the running game now (its rule or its time): stars, records, rewards, d, then <see cref="Ended"/>.</summary>
        public void Finish()
        {
            if (!_running)
                return;
            var controller = _controller!;
            _running = false;
            _paused = false;

            var state = _ctx.Activities;
            int slot = ActivityCatalog.SlotOf(_setup.Activity);
            ref var record = ref state.Records[slot];
            int challenge = _setup.Challenge;
            bool rewarded = _setup.Rewarded;
            int score = controller.Score;
            int stars = _setup.Thresholds.Stars(challenge >= 0 ? controller.ChallengeScore : score);

            bool newRecord = false, firstScore = false, improved = false, firstThree = false;
            int starsBefore = 0, attempts = 0;
            if (rewarded)
            {
                record.RewardedGames++;
                if (_freeGame)
                    record.FreeGameUsed = true;
                if (challenge < 0)
                {
                    if (score > record.Best)
                    {
                        newRecord = record.Best > 0;
                        firstScore = record.Best <= 0;
                        record.Best = score;
                    }
                    if (stars > record.BestStars)
                        record.BestStars = (byte)stars;
                }
                else
                {
                    int index = ActivityState.ChallengeIndex(slot, challenge);
                    if (state.ChallengeAttempts[index] < ushort.MaxValue)
                        state.ChallengeAttempts[index]++;
                    attempts = state.ChallengeAttempts[index];
                    starsBefore = state.ChallengeStars[index];
                    if (stars > starsBefore)
                    {
                        state.ChallengeStars[index] = (byte)stars;
                        improved = true;
                        firstThree = stars >= 3;
                    }
                }
            }
            if (challenge < 0)
                AdaptiveDifficulty.Apply(ref record, stars, _ctx.Config);
            record.GamesPlayed++;

            ActivityRewardOutcome outcome;
            if (rewarded)
            {
                var request = new ActivityRewardRequest
                {
                    Activity = _setup.Activity,
                    Mode = _setup.Mode,
                    Challenge = challenge,
                    Stars = stars,
                    NewRecord = newRecord,
                    ChallengeFirstThreeStars = firstThree,
                    PaidTicket = _paidTicket,
                    Seed = _setup.Seed,
                    Theme = _setup.Theme,
                    AnimalCount = _setup.AnimalCount,
                    Animal0 = _setup.Animal0,
                    Animal1 = _setup.Animal1,
                    Animal2 = _setup.Animal2,
                };
                // T40: what the game gathered, for its counted items (Nut Drop: hazelnuts and berries).
                if (controller is IActivityRewardCounts counts)
                {
                    request.Count0 = counts.RewardCount(0);
                    request.Count1 = counts.RewardCount(1);
                }
                _services.Rewards.Grant(in request, out outcome);
            }
            else
            {
                outcome = default;
                outcome.Stars = stars;
            }
            _ctx.RequestSave(SaveUrgency.Priority);

            _last = new ActivityGameResult
            {
                Activity = _setup.Activity,
                Mode = _setup.Mode,
                Challenge = challenge,
                Difficulty = _setup.Difficulty,
                DifficultyNext = challenge >= 0 ? _setup.Difficulty : AdaptiveDifficulty.Level(in record),
                Score = score,
                Best = record.Best,
                Stars = stars,
                NewRecord = newRecord,
                FirstScore = firstScore,
                Rewarded = rewarded,
                PaidTicket = _paidTicket,
                FreeGame = _freeGame,
                ChallengeImproved = improved,
                ChallengeStarsBefore = starsBefore,
                ChallengeAttempts = attempts,
                Seconds = _elapsed,
                Tickets = _services.Tickets.Count,
                Rewards = outcome,
            };
            if (improved)
                ChallengeImproved?.Invoke(_setup.Activity, challenge, stars, attempts);
            Ended?.Invoke(in _last);
        }

        private ActivityStartResult Check(in ActivityRequest request, out ActivityDefinition definition)
        {
            if (_running)
            {
                definition = null!;
                return ActivityStartResult.Busy;
            }
            if (!ActivityCatalog.TryGet(request.Activity, out definition))
                return ActivityStartResult.Unknown;
            if (definition.Meta)
                return ActivityStartResult.Meta;
            if (!_ctx.Config.IsEnabled(request.Activity))
                return ActivityStartResult.Disabled;
            if (!_services.IsUnlocked(request.Activity))
                return ActivityStartResult.Locked;
            if (!_services.Rules.Has(request.Activity))
                return ActivityStartResult.NoRules;
            if (request.Mode == ActivityMode.Challenge && request.Challenge < 0)
                return ActivityStartResult.Unknown;
            if (request.Mode != ActivityMode.Free && request.Challenge >= ActivityCatalog.ChallengesPerActivity)
                return ActivityStartResult.Unknown;
            if (request.Mode != ActivityMode.Free && request.Challenge >= 0 && !_services.IsChallengeOpen(request.Activity, request.Challenge))
                return ActivityStartResult.ChallengeLocked;
            return ActivityStartResult.Started;
        }
    }
}
