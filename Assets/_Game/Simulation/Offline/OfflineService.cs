#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>What happened while the player was away. Immutable, handed to the presentation once.</summary>
    public readonly struct OfflineReport
    {
        /// <summary>Wall time since the last save (0 when the clock went backwards).</summary>
        public readonly double AwaySeconds;

        /// <summary>Away time actually counted: after the rollback guard and the max cap.</summary>
        public readonly double CreditedSeconds;

        /// <summary>Credited time weighted by the efficiency curve.</summary>
        public readonly double EffectiveSeconds;

        public readonly BigValue RatePerSecond;
        public readonly BigValue Light;
        public readonly bool ClockRolledBack;

        /// <summary>
        /// Time since the later of the last save and the high-water mark, without the max cap (T36 return gifts count the
        /// days of a long absence). Equals <see cref="CreditedSeconds"/> when not given.
        /// </summary>
        public readonly double UncappedSeconds;

        public OfflineReport(double awaySeconds, double creditedSeconds, double effectiveSeconds, BigValue ratePerSecond,
            BigValue light, bool clockRolledBack, double uncappedSeconds = -1.0)
        {
            UncappedSeconds = uncappedSeconds >= 0.0 ? uncappedSeconds : creditedSeconds;
            AwaySeconds = awaySeconds;
            CreditedSeconds = creditedSeconds;
            EffectiveSeconds = effectiveSeconds;
            RatePerSecond = ratePerSecond;
            Light = light;
            ClockRolledBack = clockRolledBack;
        }

        public bool HasGain => !Light.IsZero;
    }

    /// <summary>
    /// Offline progression (doc 08, Codebase Blueprint OfflineService). Each save carries an exit snapshot
    /// (CaptureSnapshot); on return, ResolveReturn pays OfflineGain = RateAtSave x EffectiveSeconds in
    /// O(1), whatever the away time: no tick is replayed. Timers (births, breeding) plug in here later.
    /// <list type="bullet">
    /// <item>Efficiency 1 for OfflineFullSeconds (24 h), then eases toward a floor: never drops to zero.</item>
    /// <item>The gap starts at the last save, when the saved Light was true: nothing is paid twice.</item>
    /// <item>Clock moved back: nothing counts until real time passes the high-water mark again.</item>
    /// </list>
    /// </summary>
    public sealed class OfflineService
    {
        private readonly PlayerState _state;
        private readonly EconomyService _economy;
        private readonly IGameClock _clock;
        private readonly EconomyConfig _config;
        private OfflineReport _pending;
        private bool _hasPending;

        public OfflineService(PlayerState state, EconomyService economy, IGameClock clock)
        {
            _state = state;
            _economy = economy;
            _clock = clock;
            _config = economy.Config;
        }

        public OfflineReport LastReport { get; private set; }

        /// <summary>
        /// Save hook (SaveService.BeforeSerialize): records the production and the time mark that belong
        /// to the Light being saved.
        /// </summary>
        public void CaptureSnapshot(PlayerState state)
        {
            var offline = state.Offline;
            offline.RateAtSave = _economy.GetLightPerSecond();
            offline.HasRate = true;
            var now = _clock.UtcNow;
            if (now > offline.HighWaterUtc)
                offline.HighWaterUtc = now;
        }

        /// <summary>
        /// The player leaves now (app paused): time from here is offline time even if the pause save fails,
        /// so in-session production is never paid twice.
        /// </summary>
        public void MarkActive()
        {
            var now = _clock.UtcNow;
            if (now > _state.Offline.HighWaterUtc)
                _state.Offline.HighWaterUtc = now;
        }

        /// <summary>T35C: Long Nap's extra hours of full offline window (Essence tree).</summary>
        public double ExtraFullSeconds { get; set; }

        /// <summary>T35C: Gentle Wake's points added to the offline efficiency floor (Essence tree).</summary>
        public double FloorBonus { get; set; }

        /// <summary>Pays the away time once. Call at boot and when the app comes back from the background.</summary>
        public OfflineReport ResolveReturn()
        {
            using (GameProfilerMarkers.OfflineResolve.Auto())
            {
                var now = _clock.UtcNow;
                var offline = _state.Offline;
                var lastSave = _state.Meta.LastSaveUtc;
                var mark = offline.HighWaterUtc;
                double tolerance = _config.ClockRollbackToleranceSeconds;

                bool rolledBack = (mark - now).TotalSeconds > tolerance || (lastSave - now).TotalSeconds > tolerance;
                double away = Math.Max(0.0, (now - lastSave).TotalSeconds);
                var start = lastSave > mark ? lastSave : mark;
                double unseen = Math.Max(0.0, (now - start).TotalSeconds);
                double credited = Math.Min(unseen, _config.OfflineMaxSeconds);
                double effective = EffectiveSeconds(credited, _config, ExtraFullSeconds, FloorBonus);

                var rate = offline.HasRate ? offline.RateAtSave : _economy.GetLightPerSecond();
                var gain = effective > 0.0 ? BigValue.Multiply(rate, effective) : BigValue.Zero;
                if (!gain.IsZero)
                    _economy.Grant(CurrencyId.Light, gain, EconomyReason.Offline);

                // The resolved interval is spent: a second call before the next save pays nothing.
                if (now > offline.HighWaterUtc)
                    offline.HighWaterUtc = now;

                var report = new OfflineReport(away, credited, effective, rate, gain, rolledBack, unseen);
                LastReport = report;
                if (report.HasGain && credited >= _config.OfflineScreenMinSeconds)
                {
                    _pending = report;
                    _hasPending = true;
                }
                return report;
            }
        }

        /// <summary>The return screen takes the report once (polled by the scene, no event subscription).</summary>
        public bool TryTakePending(out OfflineReport report)
        {
            report = _pending;
            if (!_hasPending)
                return false;
            _hasPending = false;
            return true;
        }

        /// <summary>
        /// Integral of the efficiency curve over [0, seconds]: 1 during the full window, then
        /// floor + (1 - floor) x exp(-t / decay). Closed form, O(1).
        /// </summary>
        public static double EffectiveSeconds(double seconds, EconomyConfig config) => EffectiveSeconds(seconds, config, 0.0, 0.0);

        /// <summary>
        /// T35C: the same with Long Nap's extra hours of full window and Gentle Wake's higher floor (Essence tree).
        /// </summary>
        public static double EffectiveSeconds(double seconds, EconomyConfig config, double extraFullSeconds, double floorBonus)
        {
            if (seconds <= 0.0)
                return 0.0;
            double full = Math.Max(0.0, config.OfflineFullSeconds + extraFullSeconds);
            if (seconds <= full)
                return seconds;
            double beyond = seconds - full;
            double floor = Math.Min(1.0, Math.Max(0.0, config.OfflineFloorEfficiency + floorBonus));
            double decay = config.OfflineDecaySeconds;
            double easing = decay > 0.0 ? (1.0 - floor) * decay * (1.0 - Math.Exp(-beyond / decay)) : 0.0;
            return full + floor * beyond + easing;
        }
    }
}
