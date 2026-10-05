#nullable enable
using System;
using PocketWilds.Simulation;

namespace PocketWilds.Services.Save
{
    /// <summary>
    /// What a caller needs to know that a change reached the disk: snapshots are numbered, a change made now is in
    /// every snapshot numbered above <see cref="SerializedCount"/>, and <see cref="Written"/> reports finished writes.
    /// The store acknowledges a purchase this way without ever writing on the main thread.
    /// </summary>
    public interface ISaveWrites
    {
        long SerializedCount { get; }
        void RequestSave(SaveUrgency urgency);

        /// <summary>Main thread: the snapshot with this number is on disk.</summary>
        event Action<long>? Written;
    }
}
