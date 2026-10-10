#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>What a memory game did, for its wishes and the weekly journal (doc 01 Contenu, sections 2.3 and 5.2).</summary>
    public struct MemoryGameSummary
    {
        /// <summary>The game reached its end (every pair found, or the time); false when the player left it.</summary>
        public bool Finished;

        /// <summary>A rewarded game (free mode or challenge, with a ticket); false for practice.</summary>
        public bool Rewarded;

        public int Stars;
        public int Score;

        /// <summary>The challenge played (0 to 9), −1 for the free mode.</summary>
        public int Challenge;

        /// <summary>Pairs found (even in a game left early: they really were found).</summary>
        public int PairsFound;

        /// <summary>The grid's pairs (6, 8 or 10; 0 when another controller played).</summary>
        public int Pairs;

        /// <summary>Every pair was found (the game ended on its own rule).</summary>
        public bool AllMatched;

        /// <summary>Tries (a try is two cards): WISH-059 counts them.</summary>
        public int Tries;

        /// <summary>Flips (two per try).</summary>
        public int Moves;

        public int Mistakes;

        /// <summary>WISH-059's n for this game's grid: 2 × pairs + 6 (0 when another controller played).</summary>
        public int MovesTarget;

        /// <summary>WISH-059 « Finis un Memory en {n} coups ou moins »: every pair found in at most n tries, n of this game's grid.</summary>
        public bool WithinMoves => Finished && AllMatched && Pairs > 0 && Tries <= MovesTarget;
    }

    /// <summary>
    /// T41: feeds the rendez-vous from each memory game (doc 01 Contenu: WISH-057 « Joue au Memory des empreintes », WISH-058
    /// « Trouve {n} paires au Memory », WISH-059 « Finis un Memory en {n} coups ou moins », JRN-052 « Trouve {n} paires au
    /// Memory »). Listens to the runner's <see cref="ActivityRunner.Ended"/>, reads the game's numbers from its controller
    /// (<see cref="MemoryController"/>; another controller, a test's dummy, still counts the game), then calls
    /// <see cref="DailyVisitServices.OnMemoryGame"/>. The bootstrap builds one after both facades, next to the
    /// <see cref="NutDropHooks"/>, and disposes it with them. Allocation-free.
    /// </summary>
    public sealed class MemoryHooks : IDisposable
    {
        private readonly ActivityServices _activities;
        private readonly DailyVisitServices _daily;
        private readonly ActivityEndedHandler _onEnded;
        private bool _disposed;

        public MemoryHooks(ActivityServices activities, DailyVisitServices daily)
        {
            _activities = activities;
            _daily = daily;
            _onEnded = OnEnded;
            activities.Runner.Ended += _onEnded;
        }

        /// <summary>The last memory game's summary (for tests and the debug panel).</summary>
        public MemoryGameSummary Last { get; private set; }

        /// <summary>Memory games seen since this object was built.</summary>
        public int Games { get; private set; }

        /// <summary>The summary of a game's result and its controller (pure; the hook's own reading).</summary>
        public static MemoryGameSummary Summarize(in ActivityGameResult result, IActivityController? controller)
        {
            var summary = new MemoryGameSummary
            {
                Finished = !result.Abandoned,
                Rewarded = result.Rewarded && !result.Abandoned,
                Stars = result.Abandoned ? 0 : result.Stars,
                Score = result.Score,
                Challenge = result.Challenge,
            };
            if (controller is MemoryController game)
            {
                summary.PairsFound = game.PairsFound;
                summary.Pairs = game.PairCount;
                summary.AllMatched = game.AllMatched;
                summary.Tries = game.Tries;
                summary.Moves = game.Moves;
                summary.Mistakes = game.Mistakes;
                summary.MovesTarget = game.MovesTarget;
            }
            else if (controller is IMemoryGame other)
            {
                summary.PairsFound = other.PairsFound;
                summary.Pairs = other.PairCount;
                summary.AllMatched = other.PairCount > 0 && other.PairsFound >= other.PairCount;
                summary.Moves = other.Moves;
                summary.Tries = other.Moves / 2;
                summary.Mistakes = other.Mistakes;
                summary.MovesTarget = MemoryRules.MovesTarget(other.PairCount);
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
            if (result.Activity != ActivityId.PawMemory)
                return;
            var summary = Summarize(in result, _activities.Runner.Controller);
            Last = summary;
            Games++;
            _daily.OnMemoryGame(in summary);
        }
    }
}
