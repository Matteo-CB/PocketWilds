#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T48B the refuge's season (doc Événements originaux, section 5): the season of the player's local month under the active
    /// month map (<see cref="SeasonConfig"/>), switching at local midnight on the first day of a season month or at once when
    /// Remote Config brings a new map. The « La saison {season} commence » card is owed once per season run (saved in
    /// <see cref="SeasonState.CardKey"/>), never on the very first launch: the first season is stored as seen silently.
    /// <see cref="Tick"/> compares the clock with the next local midnight (time-zone math at boundaries only, no allocation).
    /// </summary>
    public sealed class SeasonService : ISeasonView
    {
        private readonly PlayerState _state;
        private readonly IGameClock _clock;
        private readonly ISaveScheduler? _save;
        private TimeZoneInfo _zone = TimeZoneInfo.Local;
        private DateTimeOffset _nextCheckUtc = DateTimeOffset.MinValue;
        private RefugeSeason _current;
        private int _runKey;

        public SeasonService(PlayerState state, IGameClock clock, SeasonConfig? config = null, ISaveScheduler? save = null, TimeZoneInfo? zone = null)
        {
            _state = state;
            _clock = clock;
            _save = save;
            _zone = zone ?? TimeZoneInfo.Local;
            Config = config ?? new SeasonConfig();
            Config.Changed += Refresh;
            Evaluate(false);
        }

        public SeasonConfig Config { get; }

        /// <summary>The player's time zone (the progression's, like the rendez-vous and the journal; tests set one).</summary>
        public TimeZoneInfo Zone
        {
            get => _zone;
            set
            {
                _zone = value ?? TimeZoneInfo.Local;
                Refresh();
            }
        }

        public RefugeSeason Current => _current;

        /// <summary>Captures and tests: this season whatever the date (None: the real one). Owes no card.</summary>
        public RefugeSeason Override
        {
            get => _override;
            set
            {
                _override = value;
                Refresh();
            }
        }

        private RefugeSeason _override;

        public event Action<RefugeSeason>? Changed;

        /// <summary>The current season run: its start yyyymm × 10 + season (the card key); 0 while the flag is off.</summary>
        public int RunKey => _runKey;

        public bool CardPending => Config.Enabled && _override == RefugeSeason.None && _runKey != 0 && _state.Seasons.CardKey != _runKey;

        public void MarkCardShown()
        {
            if (!CardPending)
                return;
            _state.Seasons.CardKey = _runKey;
            _save?.RequestSave(SaveUrgency.Normal);
        }

        /// <summary>The season of a local month under the active map (Éveil while the flag is off).</summary>
        public RefugeSeason SeasonOfMonth(int month) => Config.SeasonOfMonth(month);

        /// <summary>Called by the game loop: nothing but a clock comparison until the next local midnight.</summary>
        public void Tick()
        {
            if (DailyDay.Now(_state, _clock) < _nextCheckUtc)
                return;
            Evaluate(true);
        }

        /// <summary>Re-reads the season now (a new map or flag, a resume, a zone change); raises Changed when it moved.</summary>
        public void Refresh() => Evaluate(true);

        private void Evaluate(bool notify)
        {
            var now = DailyDay.Now(_state, _clock);
            var local = TimeZoneInfo.ConvertTime(now, _zone);
            var season = Config.SeasonOfMonth(local.Month);
            _runKey = Config.Enabled ? Config.RunStart(local.Year, local.Month) * 10 + (int)season : 0;
            // The very first launch with seasons stores the season as seen: the card is for a season that comes later.
            if (_runKey != 0 && _state.Seasons.CardKey == 0)
            {
                _state.Seasons.CardKey = _runKey;
                _save?.RequestSave(SaveUrgency.Normal);
            }
            _nextCheckUtc = DailyDay.NextBoundaryUtc(now, _zone, 0);
            if (_override != RefugeSeason.None)
                season = _override;
            if (season == _current)
                return;
            _current = season;
            if (notify)
                Changed?.Invoke(season);
        }
    }
}
