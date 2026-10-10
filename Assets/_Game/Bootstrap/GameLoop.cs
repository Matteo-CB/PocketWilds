using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Feedback;
using UnityEngine;

namespace PocketWilds.Bootstrap
{
    /// <summary>
    /// The only gameplay Update. Ticks systems in a fixed order. Animals never get their own Update.
    /// </summary>
    public sealed class GameLoop : MonoBehaviour
    {
        /// <summary>Longer gaps (app in background) are handled by offline resolution, not by ticking.</summary>
        private const float MaxFrameDelta = 0.5f;

        private GameContext _context;
        private AnalyticsService _analytics;
        private AudioService _audio;
        private HapticService _haptics;
        private double _economyAccumulator;
        private double _economyInterval = 0.1;

        public void Initialize(GameContext context)
        {
            _context = context;
            _economyInterval = context.Economy.Config.AdvanceIntervalSeconds;
            _economyAccumulator = 0.0;
        }

        /// <summary>Analytics gets the raw frame time (performance summaries), not the clamped simulation delta.</summary>
        public void SetAnalytics(AnalyticsService analytics) => _analytics = analytics;

        /// <summary>T34B: sound bursts, bed fades and haptic cooldowns follow real time.</summary>
        public void SetFeedback(AudioService audio, HapticService haptics)
        {
            _audio = audio;
            _haptics = haptics;
        }

        private void Update()
        {
            if (_context == null)
                return;

            using (GameProfilerMarkers.GameLoopTick.Auto())
            {
                float frame = Time.unscaledDeltaTime;
                _analytics?.Tick(frame);
                // T34: frame cost and heat drive the picture's quality (no allocation).
                _context.Quality?.Tick(frame);
                _audio?.Tick(frame);
                _haptics?.Tick(frame);
                float delta = frame;
                if (delta > MaxFrameDelta)
                    delta = MaxFrameDelta;

                _economyAccumulator += delta;
                if (_economyAccumulator >= _economyInterval)
                {
                    _context.Economy.Advance(_economyAccumulator);
                    _economyAccumulator = 0.0;
                }

                // T36: the day boundary is checked once a second inside (no time-zone math per frame).
                _context.Daily?.Tick(delta);
                // T37: ticket refill, day boundary and openings, once a second inside. Games are ticked by their presenter.
                _context.Activities?.Tick(delta);
                // T42: a team whose return time has passed comes back (SlotChanged once); reads the clock, no allocation.
                _context.Expeditions?.Tick();
                // T43: sprouts passing a stage or ripening (StageChanged, PlotChanged); reads the clock, no allocation.
                _context.Garden?.Tick();

                _context.Save.Tick();
            }
        }

        /// <summary>Drops time accumulated before a pause; the away time belongs to offline resolution.</summary>
        public void ResetAccumulators()
        {
            _economyAccumulator = 0.0;
        }
    }
}
