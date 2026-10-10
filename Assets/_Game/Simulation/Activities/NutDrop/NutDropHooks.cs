#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>What a nut drop game did, for its wishes and the weekly journal (doc 01 Contenu, sections 2.3 and 5.2).</summary>
    public struct NutDropGameSummary
    {
        /// <summary>The game reached its end (time up); false when the player left it.</summary>
        public bool Finished;

        /// <summary>A rewarded game (free mode or challenge, with a ticket); false for practice.</summary>
        public bool Rewarded;

        public int Stars;
        public int Score;

        /// <summary>The challenge played (0 to 9), −1 for the free mode.</summary>
        public int Challenge;

        /// <summary>Nuts caught (hazelnuts, acorns, berries, golden hazelnuts; even in a game left early: they really were).</summary>
        public int Caught;

        public int HazelnutsCaught;
        public int BerriesCaught;
        public int PineconesCaught;

        /// <summary>The longest streak of the game.</summary>
        public int BestStreak;
    }

    /// <summary>
    /// T40: feeds the rendez-vous from each nut drop game (doc 01 Contenu: WISH-054 « Joue à Pluie de noisettes », WISH-055
    /// « Attrape {n} noisettes en une partie », WISH-056 « Fais une série de {n} à Pluie de noisettes », JRN-017 « Attrape {n}
    /// noisettes »). Listens to the runner's <see cref="ActivityRunner.Ended"/>, reads the game's numbers from its controller
    /// (<see cref="INutDropGame"/>; another controller, a test's dummy, still counts the game), then calls
    /// <see cref="DailyVisitServices.OnNutDropGame"/>. The bootstrap builds one after both facades, next to the
    /// <see cref="HideSeekHooks"/>, and disposes it with them. Allocation-free.
    /// </summary>
    public sealed class NutDropHooks : IDisposable
    {
        private readonly ActivityServices _activities;
        private readonly DailyVisitServices _daily;
        private readonly ActivityEndedHandler _onEnded;
        private bool _disposed;

        public NutDropHooks(ActivityServices activities, DailyVisitServices daily)
        {
            _activities = activities;
            _daily = daily;
            _onEnded = OnEnded;
            activities.Runner.Ended += _onEnded;
        }

        /// <summary>The last nut drop game's summary (for tests and the debug panel).</summary>
        public NutDropGameSummary Last { get; private set; }

        /// <summary>Nut drop games seen since this object was built.</summary>
        public int Games { get; private set; }

        /// <summary>The summary of a game's result and its controller (pure; the hook's own reading).</summary>
        public static NutDropGameSummary Summarize(in ActivityGameResult result, IActivityController? controller)
        {
            var summary = new NutDropGameSummary
            {
                Finished = !result.Abandoned,
                Rewarded = result.Rewarded && !result.Abandoned,
                Stars = result.Abandoned ? 0 : result.Stars,
                Score = result.Score,
                Challenge = result.Challenge,
            };
            if (controller is INutDropGame game)
            {
                summary.Caught = game.Caught;
                summary.HazelnutsCaught = game.HazelnutsCaught;
                summary.BerriesCaught = game.BerriesCaught;
                summary.PineconesCaught = game.PineconesCaught;
                summary.BestStreak = game.BestStreak;
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
            if (result.Activity != ActivityId.NutRain)
                return;
            var summary = Summarize(in result, _activities.Runner.Controller);
            Last = summary;
            Games++;
            _daily.OnNutDropGame(in summary);
        }
    }
}
