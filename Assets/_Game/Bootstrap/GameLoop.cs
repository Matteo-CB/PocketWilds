using PocketWilds.Core;
using PocketWilds.Presentation;
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
        private double _economyAccumulator;
        private double _economyInterval = 0.1;

        public void Initialize(GameContext context)
        {
            _context = context;
            _economyInterval = context.Economy.Config.AdvanceIntervalSeconds;
            _economyAccumulator = 0.0;
        }

        private void Update()
        {
            if (_context == null)
                return;

            using (GameProfilerMarkers.GameLoopTick.Auto())
            {
                float delta = Time.unscaledDeltaTime;
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
