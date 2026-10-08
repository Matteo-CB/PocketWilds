using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    // T37 « Cadre des activités »: the view contracts between the activity presenter (Presentation assembly) and the
    // hub, the in-game HUD, the result card, the « no ticket » card and the HUD's Play button (UI assembly). Doc
    // « Mini-jeux & activités » sections 1 and 2, doc 01 Contenu section 12.0, doc 06 screens 029 and 030, doc 05
    // Sensations sheet 20. Views never read the simulation; the presenter fills these models once per change.

    // ------------------------------------------------------------------------------------------- HUD Play button

    /// <summary>The HUD's Play button on the right rail (doc 01 A15: refuge level 3 and 5 animals), with its ticket count.</summary>
    public interface IPlayHud
    {
        void BindPlay(IPlayHudActions actions);

        /// <summary>The Play button, the tickets owned shown on it, and a dot (a free game, a new activity, a challenge to try).</summary>
        void SetPlay(bool visible, int tickets, bool dot);

        /// <summary>The button appears with a 0.35 s bounce, when no screen is open. Once.</summary>
        void PlayPlayAppear();

        void SetPlayHint(bool visible);

        bool PlayVisible { get; }

        Vector2 PlayScreenPosition { get; }

        /// <summary>
        /// A game plays: the refuge HUD (chips, egg button, rails, toasts) fades out in 150 ms and catches no touch, then
        /// fades back when the game ends. The activity HUD has the screen to itself.
        /// </summary>
        void SetPlaying(bool playing);
    }

    public interface IPlayHudActions
    {
        void OpenActivities();
    }

    // ------------------------------------------------------------------------------------------- tickets

    public struct TicketsModel
    {
        public int Count;
        public int Max;

        /// <summary>Minutes before the next free ticket (0 when the reserve is full). Shown as real time, never a pressure.</summary>
        public int MinutesToNext;

        /// <summary>Ticket packs for Gems: price, tickets per pack, packs left today.</summary>
        public int PackPrice;
        public int PackSize;
        public int PacksLeftToday;
        public long Gems;

        /// <summary>A rewarded ad for tickets exists in this build (T55).</summary>
        public bool AdAvailable;
    }

    // ------------------------------------------------------------------------------------------- hub (screen 029)

    public enum ActivityCardState : byte
    {
        Locked = 0,
        Open = 1,

        /// <summary>Open and never played: a soft « Nouveau » tag.</summary>
        New = 2,
    }

    /// <summary>One card of the hub: the activity, its best score and stars, its challenges, or what opens it.</summary>
    public struct ActivityCardModel
    {
        public ActivityId Activity;
        public ActivityCardState State;
        public string Name;

        /// <summary>What opens a locked card (« Niveau du refuge 4 », « Jour 5 »…), already formatted.</summary>
        public string UnlockHint;

        public int Best;
        public byte BestStars;

        /// <summary>Challenges completed at 3 stars, out of 10.</summary>
        public int ChallengesDone;

        /// <summary>The next rewarded game of this activity is free (the first Grooming game, doc 01 A15).</summary>
        public bool FreeGame;

        /// <summary>The featured activity of a running event.</summary>
        public bool Featured;
    }

    public struct ChallengeCellModel
    {
        public int Index;
        public byte Stars;

        /// <summary>Challenges open in order: one star on the previous one opens the next.</summary>
        public bool Locked;

        /// <summary>The challenge's goal in one short line (« 25 noisettes en 40 s »).</summary>
        public string Goal;
    }

    public interface IActivityHubModel
    {
        int CardCount { get; }
        ActivityCardModel Card(int index);

        int SelectedCard { get; }

        TicketsModel Tickets { get; }

        /// <summary>Practice: free, unlimited, no reward (doc Mini-jeux, section 1).</summary>
        bool Practice { get; }

        int ChallengeCount { get; }
        ChallengeCellModel Challenge(int index);

        /// <summary>Theme pills of the selected activity (one per open biome), and the chosen one.</summary>
        int ThemeCount { get; }
        ContentId Theme(int index);
        int SelectedTheme { get; }
    }

    public interface IActivityHubActions
    {
        void SelectActivity(int card);
        void Play();
        void PlayChallenge(int challenge);
        void SetPractice(bool practice);
        void SetTheme(int theme);
        void BuyTickets();
        void CloseActivities();
    }

    /// <summary>
    /// PF_UI_ActivityHub (screen 029): virtualized activity cards (thumbnail, name, best, stars, challenges, locked with
    /// its hint), the ticket chip with the time to the next free ticket, the practice toggle, the theme pills, the 10
    /// challenges of the selected activity. Closed: canvas disabled, ticked by the presenter.
    /// </summary>
    public interface IActivityHubView
    {
        bool IsOpen { get; }
        void Open(IActivityHubModel model, ILocalization text, IActivityHubActions actions);
        void Refresh();
        void Close();
        void Tick(float unscaledDeltaTime);
        void Prewarm(IActivityHubModel model, ILocalization text, IActivityHubActions actions);
        Vector2 CardScreenPosition(int card);
    }

    // ------------------------------------------------------------------------------------------- no ticket

    public interface ITicketsEmptyActions
    {
        void BuyTickets();
        void PlayPractice();
        void TicketsEmptyClosed();
    }

    /// <summary>
    /// « Plus de billets » (doc Mini-jeux, section 2): first the time before the next free ticket, then the other options
    /// (practice right away, a pack for Gems, an ad later). Never blocking; a tap beside closes it.
    /// </summary>
    public interface ITicketsEmptyView
    {
        bool IsOpen { get; }
        void Open(in TicketsModel tickets, ILocalization text, ITicketsEmptyActions actions);
        void Refresh(in TicketsModel tickets);
        void Close();
        void Tick(float unscaledDeltaTime);
        void Prewarm(ILocalization text);
    }

    // ------------------------------------------------------------------------------------------- in-game HUD (screen 030)

    public struct ActivityHudModel
    {
        public ActivityId Activity;
        public ActivityMode Mode;

        /// <summary>Seconds of the game (0: no timer, the activity ends on its own rule).</summary>
        public float Duration;

        /// <summary>Score needed for 1, 2 and 3 stars (the HUD's small star marks on the score bar).</summary>
        public int Star1;
        public int Star2;
        public int Star3;

        public int Best;
    }

    public interface IActivityHudActions
    {
        void PauseActivity();
        void ResumeActivity();
        void QuitActivity();
    }

    /// <summary>PF_UI_ActivityHud: score, time left (a soft ring, never a red countdown), streak, pause.</summary>
    public interface IActivityHudView
    {
        bool IsShown { get; }
        void Show(in ActivityHudModel model, ILocalization text, IActivityHudActions actions);

        /// <summary>Per frame from the runner: labels change only when the value changes.</summary>
        void SetScore(int score);
        void SetTimeLeft(float seconds);
        void SetStreak(int streak);
        void SetPaused(bool paused);
        void Hide();
        void Tick(float unscaledDeltaTime);
        void Prewarm(ILocalization text);
    }

    // ------------------------------------------------------------------------------------------- result (doc 05 sheet 20)

    public struct ActivityResultModel
    {
        public ActivityId Activity;
        public ActivityMode Mode;
        public int ChallengeIndex;
        public int Score;
        public int Best;
        public byte Stars;
        public bool NewRecord;

        /// <summary>The game was rewarded (a ticket or the free game); practice shows stars and score only.</summary>
        public bool Rewarded;

        /// <summary>A challenge reached a new star count (the challenge row shows it).</summary>
        public bool ChallengeImproved;

        /// <summary>A game of this activity was already played this session: one tap reveals everything at once.</summary>
        public bool QuickReveal;

        /// <summary>Under a sticker cell of a paid game: « Prochain autocollant de la page » (no paid randomness).</summary>
        public bool FixedStickerRule;

        /// <summary>A ticket is left for « Rejouer » (else Rejouer leads to the « no ticket » card or practice).</summary>
        public bool CanReplay;
    }

    public interface IActivityResultActions
    {
        void PlayAgain();
        void BackToHub();
    }

    /// <summary>
    /// PF_UI_ActivityResult: the card slides in (200 ms), stars pop one by one (350 ms each) while the score rolls, then the
    /// rewards turn over one by one (300 ms each), « Nouveau record » ribbon, « Rejouer » and « Retour » (doc 05 sheet 20).
    /// </summary>
    public interface IActivityResultView
    {
        bool IsOpen { get; }
        void Open(in ActivityResultModel model, RewardCell[] rewards, int rewardCount, ILocalization text, IActivityResultActions actions);
        void RevealAll();
        void Close();
        void Tick(float unscaledDeltaTime);
        void Prewarm(ILocalization text);
    }
}
