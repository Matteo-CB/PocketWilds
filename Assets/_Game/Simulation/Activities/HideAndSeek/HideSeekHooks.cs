#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>What a hide-and-seek game did, for its wishes and the weekly journal (doc 01 Contenu, sections 2.3 and 5.2).</summary>
    public struct HideSeekGameSummary
    {
        /// <summary>The game reached its end (every animal found, time up, challenge 6's 4th mistake); false when the player left it.</summary>
        public bool Finished;

        /// <summary>A rewarded game (free mode or challenge, with a ticket); false for practice.</summary>
        public bool Rewarded;

        public int Stars;
        public int Score;

        /// <summary>The challenge played (0 to 9), −1 for the free mode.</summary>
        public int Challenge;

        /// <summary>Animals hidden this game, animals found (even in a game left early: they really were), and found before their hint.</summary>
        public int Hidden;
        public int Found;
        public int FoundBeforeHint;

        /// <summary>Every animal of the game found (the game ends on it).</summary>
        public bool AllFound;

        public int EmptyTouches;
    }

    /// <summary>
    /// T39: feeds the rendez-vous from each hide-and-seek game (doc 01 Contenu: WISH-051 « Joue à Cache-cache », WISH-052
    /// « Trouve {n} animaux cachés », WISH-053 « Trouve tous les animaux d'une partie », JRN-016 « Trouve {n} animaux à
    /// Cache-cache »). Listens to the runner's <see cref="ActivityRunner.Ended"/>, reads the game's numbers from its
    /// controller (<see cref="IHideSeekGame"/>; another controller, a test's dummy, still counts the game), then calls
    /// <see cref="DailyVisitServices.OnHideSeekGame"/>. The bootstrap builds one after both facades, next to the
    /// <see cref="GroomingHooks"/>, and disposes it with them. Allocation-free.
    /// </summary>
    public sealed class HideSeekHooks : IDisposable
    {
        private readonly ActivityServices _activities;
        private readonly DailyVisitServices _daily;
        private readonly ActivityEndedHandler _onEnded;
        private bool _disposed;

        public HideSeekHooks(ActivityServices activities, DailyVisitServices daily)
        {
            _activities = activities;
            _daily = daily;
            _onEnded = OnEnded;
            activities.Runner.Ended += _onEnded;
        }

        /// <summary>The last hide-and-seek game's summary (for tests and the debug panel).</summary>
        public HideSeekGameSummary Last { get; private set; }

        /// <summary>Hide-and-seek games seen since this object was built.</summary>
        public int Games { get; private set; }

        /// <summary>The summary of a game's result and its controller (pure; the hook's own reading).</summary>
        public static HideSeekGameSummary Summarize(in ActivityGameResult result, IActivityController? controller)
        {
            var summary = new HideSeekGameSummary
            {
                Finished = !result.Abandoned,
                Rewarded = result.Rewarded && !result.Abandoned,
                Stars = result.Abandoned ? 0 : result.Stars,
                Score = result.Score,
                Challenge = result.Challenge,
            };
            if (controller is IHideSeekGame game)
            {
                summary.Hidden = game.HiddenCount;
                summary.Found = game.FoundCount;
                summary.FoundBeforeHint = game.FoundBeforeHint;
                summary.AllFound = game.AllFound;
                summary.EmptyTouches = game.EmptyTouches;
            }
            return summary;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _activities.Runner.Ended -= _onEnded;
        }

        private void OnEnded(in ActivityGameResult result)
        {
            if (result.Activity != ActivityId.HideAndSeek)
                return;
            var summary = Summarize(in result, _activities.Runner.Controller);
            Last = summary;
            Games++;
            _daily.OnHideSeekGame(in summary);
        }
    }
}
