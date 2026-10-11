#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T48B the special weekends (doc Événements originaux, section 6): Friday 00:00 to Sunday 23:59:59 local, never while a big
    /// event runs, kinds from Remote Config « events.weekends » or the default rotation (<see cref="WeekendConfig"/>). It switches
    /// the existing weekend numbers on and off: activity rewards (<see cref="EventService.ActivitiesWeekend"/>), Discovery egg
    /// novelty (<see cref="HatchService.EggNoveltyBonus"/>), bond points (<see cref="BondService.WeekendMultiplier"/>),
    /// garden growth and seeds (<see cref="GardenConfig.WeekendActive"/>), expedition durations
    /// (<see cref="ExpeditionConfig.WeekendActive"/>) and duplicate album stars (<see cref="AlbumConfig.WeekendActive"/>).
    /// <see cref="Tick"/> is a clock comparison until the next window edge (once a minute inside a window, for an event that
    /// starts); no allocation.
    /// </summary>
    public sealed class WeekendService : IWeekendView
    {
        /// <summary>Inside a window, how often the big event is checked again (an event starting ends the weekend).</summary>
        public const long EventRecheckSeconds = 60;

        private readonly PlayerState _state;
        private readonly IGameClock _clock;
        private readonly ISaveScheduler? _save;
        private TimeZoneInfo _zone = TimeZoneInfo.Local;
        private long _nextCheckUnix = long.MinValue;
        private WeekendKind _active;
        private long _endsUnix;
        private int _key;
        private bool _evaluating;

        public WeekendService(PlayerState state, IGameClock clock, WeekendConfig? config = null, ISaveScheduler? save = null)
        {
            _state = state;
            _clock = clock;
            _save = save;
            Config = config ?? new WeekendConfig();
            Config.Changed += Refresh;
        }

        public WeekendConfig Config { get; }

        /// <summary>The player's time zone (the progression's; tests set one).</summary>
        public TimeZoneInfo Zone
        {
            get => _zone;
            set
            {
                _zone = value ?? TimeZoneInfo.Local;
                Invalidate();
            }
        }

        /// <summary>True while a big event runs (the bootstrap reads <see cref="EventService.IsRunning"/>); null: never.</summary>
        public Func<bool>? BigEventRunning { get; set; }

        public EventService? Events { get; set; }
        public HatchService? Hatch { get; set; }
        public BondService? Bond { get; set; }
        public GardenConfig? Garden { get; set; }
        public ExpeditionConfig? Expeditions { get; set; }
        public AlbumConfig? Albums { get; set; }

        public WeekendKind Active => _active;

        public long EndsUnix => _endsUnix;

        /// <summary>The running weekend's key: its first local day yyyymmdd × 10 + kind; 0 when none.</summary>
        public int Key => _key;

        public event Action<WeekendKind>? Changed;

        /// <summary>The weekend was seen on screen for the first time (analytics weekend_seen).</summary>
        public event Action<WeekendKind>? Seen;

        public void MarkSeen()
        {
            if (_active == WeekendKind.None || _state.Seasons.WeekendSeenKey == _key)
                return;
            _state.Seasons.WeekendSeenKey = _key;
            _save?.RequestSave(SaveUrgency.Normal);
            Seen?.Invoke(_active);
        }

        /// <summary>Called by the game loop: nothing but a clock comparison until the next edge.</summary>
        public void Tick()
        {
            if (DailyDay.Now(_state, _clock).ToUnixTimeSeconds() < _nextCheckUnix)
                return;
            Evaluate();
        }

        /// <summary>Re-reads the weekend now (a new list, flag or number, the big event's phase, a resume).</summary>
        public void Refresh() => Evaluate();

        /// <summary>The next <see cref="Tick"/> re-reads the weekend (the big event changed phase).</summary>
        public void Invalidate() => _nextCheckUnix = long.MinValue;

        private void Evaluate()
        {
            if (_evaluating)
                return;
            _evaluating = true;
            try
            {
                var now = DailyDay.Now(_state, _clock);
                long nowUnix = now.ToUnixTimeSeconds();
                var kind = WeekendKind.None;
                long ends = 0;
                int key = 0;
                long next = long.MaxValue;
                if (Config.Enabled)
                {
                    var list = Config.Schedule;
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var w = list[i];
                            long start = LocalToUnix(w.StartLocal, _zone);
                            long end = LocalToUnix(w.EndLocal, _zone);
                            if (nowUnix >= start && nowUnix < end)
                            {
                                next = Math.Min(next, end);
                                if (kind != WeekendKind.None)
                                    continue;
                                kind = w.Kind;
                                ends = end;
                                key = DailyDay.FromDate(w.StartLocal.Date) * 10 + (int)kind;
                            }
                            else if (start > nowUnix)
                                next = Math.Min(next, start);
                        }
                    }
                    else
                    {
                        var local = TimeZoneInfo.ConvertTime(now, _zone).DateTime;
                        int sinceFriday = ((int)local.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
                        var friday = local.Date.AddDays(-sinceFriday);
                        if (sinceFriday <= 2)
                        {
                            kind = WeekendConfig.DefaultKind(friday);
                            ends = LocalToUnix(friday.AddDays(3), _zone);
                            key = DailyDay.FromDate(friday) * 10 + (int)kind;
                            next = ends;
                        }
                        else
                            next = LocalToUnix(friday.AddDays(7), _zone);
                    }
                }
                if (kind != WeekendKind.None)
                {
                    next = Math.Min(next, nowUnix + EventRecheckSeconds);
                    if (BigEventRunning != null && BigEventRunning())
                    {
                        kind = WeekendKind.None;
                        ends = 0;
                        key = 0;
                    }
                }
                _nextCheckUnix = next;
                _endsUnix = ends;
                _key = key;
                bool changed = kind != _active;
                _active = kind;
                ApplyEffects();
                if (changed)
                    Changed?.Invoke(kind);
            }
            finally
            {
                _evaluating = false;
            }
        }

        private void ApplyEffects()
        {
            var kind = _active;
            if (Events != null)
                Events.ActivitiesWeekend = kind == WeekendKind.Activities;
            if (Hatch != null)
                Hatch.EggNoveltyBonus = kind == WeekendKind.Eggs ? Config.EggNoveltyBonus : 0.0;
            if (Bond != null)
                Bond.WeekendMultiplier = kind == WeekendKind.Cuddles ? Config.BondMultiplier : 1.0;
            if (Garden != null)
                Garden.WeekendActive = kind == WeekendKind.Garden;
            if (Expeditions != null)
                Expeditions.WeekendActive = kind == WeekendKind.Expeditions;
            if (Albums != null)
                Albums.WeekendActive = kind == WeekendKind.Albums;
        }

        /// <summary>A local wall-clock time to Unix seconds: a time in a daylight-saving gap moves to the first valid minute after it, an ambiguous one takes the earlier instant.</summary>
        public static long LocalToUnix(DateTime local, TimeZoneInfo zone)
        {
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            while (zone.IsInvalidTime(local))
                local = local.AddMinutes(1);
            TimeSpan offset;
            if (zone.IsAmbiguousTime(local))
            {
                var offsets = zone.GetAmbiguousTimeOffsets(local);
                offset = offsets[0];
                for (int i = 1; i < offsets.Length; i++)
                    if (offsets[i] > offset)
                        offset = offsets[i];
            }
            else
                offset = zone.GetUtcOffset(local);
            return new DateTimeOffset(local, offset).ToUnixTimeSeconds();
        }
    }
}
