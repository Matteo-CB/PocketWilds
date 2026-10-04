#nullable enable
using System;

namespace PocketWilds.Core
{
    /// <summary>
    /// Only source of time for gameplay. Nothing in simulation reads DateTime.Now directly.
    /// </summary>
    public interface IGameClock
    {
        /// <summary>Wall clock, used for saves and offline deltas.</summary>
        DateTimeOffset UtcNow { get; }

        /// <summary>Monotonic seconds since the session started. Never goes backwards.</summary>
        double SessionSeconds { get; }

        /// <summary>Time elapsed since <paramref name="previous"/>, clamped to zero if the clock went backwards.</summary>
        TimeSpan GetOfflineDelta(DateTimeOffset previous);
    }
}
