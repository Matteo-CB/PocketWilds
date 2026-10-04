#nullable enable
using System;

namespace PocketWilds.Core
{
    /// <summary>Controllable clock for tests, offline simulations and dev tools.</summary>
    public sealed class FakeGameClock : IGameClock
    {
        private DateTimeOffset _utcNow;
        private double _sessionSeconds;

        public FakeGameClock(DateTimeOffset start)
        {
            _utcNow = start;
        }

        public DateTimeOffset UtcNow => _utcNow;
        public double SessionSeconds => _sessionSeconds;

        /// <summary>Moves wall time only. Session time is monotonic and is not affected.</summary>
        public void Set(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        /// <summary>Moves wall time and session time forward together.</summary>
        public void Advance(TimeSpan delta)
        {
            if (delta < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delta), "Use Set() to move wall time backwards.");

            _utcNow += delta;
            _sessionSeconds += delta.TotalSeconds;
        }

        public TimeSpan GetOfflineDelta(DateTimeOffset previous)
        {
            var delta = _utcNow - previous;
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }
    }
}
