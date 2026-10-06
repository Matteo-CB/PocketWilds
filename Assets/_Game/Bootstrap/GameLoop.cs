using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Analytics;
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
                float delta = frame;
                if (delta > MaxFrameDelta)
                    delta = MaxFrameDelta;

                _economyAccumulator += delta;
                if (_economyAccumulator >= _economyInterval)
                {
                    _context.Economy.Advance(_economyAccumulator);
                    _economyAccumulator = 0.0;
                }

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
