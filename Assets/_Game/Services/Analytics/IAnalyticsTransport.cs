#nullable enable

namespace PocketWilds.Services.Analytics
{
    /// <summary>
    /// Sends one encoded batch to an analytics backend. Called on the outbox thread, never on the main thread.
    /// The backend is not chosen yet (doc 09: Firebase, GameAnalytics or own endpoint): it plugs in here.
    /// </summary>
    public interface IAnalyticsTransport
    {
        string Name { get; }

        /// <summary>False when no backend is configured: batches stay on disk (bounded) and nothing is read back.</summary>
        bool IsAvailable { get; }

        /// <summary>True when the backend accepted the batch: its file is then deleted. False keeps it for a retry.</summary>
        bool TrySend(byte[] payload, int length);
    }

    /// <summary>No backend yet: batches are kept on the phone, never sent.</summary>
    public sealed class NullAnalyticsTransport : IAnalyticsTransport
    {
        public static readonly NullAnalyticsTransport Instance = new NullAnalyticsTransport();

        public string Name => "none";
        public bool IsAvailable => false;
        public bool TrySend(byte[] payload, int length) => false;
    }
}
