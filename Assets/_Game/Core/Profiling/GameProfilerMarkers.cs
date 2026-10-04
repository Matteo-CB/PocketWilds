#nullable enable
using Unity.Profiling;

namespace PocketWilds.Core
{
    /// <summary>
    /// Constant profiler markers for every hot path. Use <c>using (Marker.Auto())</c>, which does not allocate.
    /// Add a marker before optimizing anything.
    /// </summary>
    public static class GameProfilerMarkers
    {
        public static readonly ProfilerMarker EconomyAdvance = new ProfilerMarker(ProfilerCategory.Scripts, "Economy.Advance");
        public static readonly ProfilerMarker HatchResolve = new ProfilerMarker(ProfilerCategory.Scripts, "Hatch.Resolve");
        public static readonly ProfilerMarker OfflineResolve = new ProfilerMarker(ProfilerCategory.Scripts, "Offline.Resolve");
        public static readonly ProfilerMarker SchedulerTickA = new ProfilerMarker(ProfilerCategory.Scripts, "Scheduler.TickA");
        public static readonly ProfilerMarker SchedulerTickB = new ProfilerMarker(ProfilerCategory.Scripts, "Scheduler.TickB");
        public static readonly ProfilerMarker SaveSerialize = new ProfilerMarker(ProfilerCategory.Scripts, "Save.Serialize");
        public static readonly ProfilerMarker WorldChunkActivate = new ProfilerMarker(ProfilerCategory.Scripts, "WorldStreamer.Activate");
        public static readonly ProfilerMarker UICodexBind = new ProfilerMarker(ProfilerCategory.Gui, "UI.CodexBind");
        public static readonly ProfilerMarker PoolAcquire = new ProfilerMarker(ProfilerCategory.Scripts, "Pool.Acquire");
        public static readonly ProfilerMarker PoolRelease = new ProfilerMarker(ProfilerCategory.Scripts, "Pool.Release");
        public static readonly ProfilerMarker GameLoopTick = new ProfilerMarker(ProfilerCategory.Scripts, "GameLoop.Tick");
    }
}
