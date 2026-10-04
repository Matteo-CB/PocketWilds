#nullable enable
using System;
using System.Diagnostics;

namespace PocketWilds.Core
{
    /// <summary>System wall clock plus a monotonic session timer.</summary>
    public sealed class RuntimeGameClock : IGameClock
    {
        private readonly long _sessionStartTimestamp;

        public RuntimeGameClock()
        {
            _sessionStartTimestamp = Stopwatch.GetTimestamp();
        }

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public double SessionSeconds =>
            (Stopwatch.GetTimestamp() - _sessionStartTimestamp) / (double)Stopwatch.Frequency;

        public TimeSpan GetOfflineDelta(DateTimeOffset previous)
        {
            var delta = UtcNow - previous;
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }
    }
}
