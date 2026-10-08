#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>What a grooming game did, for its wishes and the weekly journal (doc 01 Contenu, sections 2.3 and 5.2).</summary>
    public struct GroomingGameSummary
    {
        /// <summary>The animal groomed (the game's first animal; 0: none).</summary>
        public uint Animal;

        /// <summary>The game reached its end (bow placed or time up); false when the player left it.</summary>
        public bool Finished;

        /// <summary>A rewarded game (free mode or challenge, with a ticket or offered); false for practice.</summary>
        public bool Rewarded;

        public int Stars;
        public int Score;

        /// <summary>Debris and burrs taken off.</summary>
        public int DebrisRemoved;

        public int ZonesCleaned;
        public bool BowPlaced;
    }

    /// <summary>
    /// T38: feeds the rendez-vous from each grooming game (doc 01 Contenu: WISH-019 « Brosse {animal} », WISH-020 stars,
    /// WISH-021 leaves and twigs, WISH-022 a bow, JRN-033 « Brosse {n} animaux »). Listens to the runner's
    /// <see cref="ActivityRunner.Ended"/>, reads the game's numbers from its controller (<see cref="IGroomingGame"/>; another
    /// controller, a test's dummy, still counts the game and its stars) and the animal from <see cref="ActivityRunner.Setup"/>,
    /// then calls <see cref="DailyVisitServices.OnGroomingGame"/>. The bootstrap builds one after both facades and disposes
    /// it with them. Allocation-free.
    /// </summary>
    public sealed class GroomingHooks : IDisposable
    {
        private readonly ActivityServices _activities;
        private readonly DailyVisitServices _daily;
        private readonly ActivityEndedHandler _onEnded;
        private bool _disposed;

        public GroomingHooks(ActivityServices activities, DailyVisitServices daily)
        {
            _activities = activities;
            _daily = daily;
            _onEnded = OnEnded;
            activities.Runner.Ended += _onEnded;
        }

        /// <summary>The last grooming game's summary (for tests and the debug panel).</summary>
        public GroomingGameSummary Last { get; private set; }

        /// <summary>Grooming games seen since this object was built.</summary>
        public int Games { get; private set; }

        /// <summary>The summary of a game's result and its controller (pure; the hook's own reading).</summary>
        public static GroomingGameSummary Summarize(in ActivityGameResult result, IActivityController? controller, uint animal)
        {
            var summary = new GroomingGameSummary
            {
                Animal = animal,
                Finished = !result.Abandoned,
                Rewarded = result.Rewarded && !result.Abandoned,
                Stars = result.Abandoned ? 0 : result.Stars,
                Score = result.Score,
            };
            if (controller is IGroomingGame game)
            {
                summary.DebrisRemoved = game.DebrisRemoved;
                summary.ZonesCleaned = game.ZonesCleaned;
                summary.BowPlaced = game.BowPlaced;
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
            if (result.Activity != ActivityId.Grooming)
                return;
            var runner = _activities.Runner;
            var summary = Summarize(in result, runner.Controller, runner.Setup.Animal0);
            Last = summary;
            Games++;
            _daily.OnGroomingGame(in summary);
        }
    }
}
