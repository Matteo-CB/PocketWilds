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
        public static readonly ProfilerMarker GeneticsBreed = new ProfilerMarker(ProfilerCategory.Scripts, "Genetics.Breed");
        public static readonly ProfilerMarker OfflineResolve = new ProfilerMarker(ProfilerCategory.Scripts, "Offline.Resolve");
        public static readonly ProfilerMarker SchedulerTickA = new ProfilerMarker(ProfilerCategory.Scripts, "Scheduler.TickA");
        public static readonly ProfilerMarker SchedulerTickB = new ProfilerMarker(ProfilerCategory.Scripts, "Scheduler.TickB");
        public static readonly ProfilerMarker SaveSerialize = new ProfilerMarker(ProfilerCategory.Scripts, "Save.Serialize");
        public static readonly ProfilerMarker WorldChunkActivate = new ProfilerMarker(ProfilerCategory.Scripts, "WorldStreamer.Activate");
        public static readonly ProfilerMarker WorldStreamerTick = new ProfilerMarker(ProfilerCategory.Scripts, "WorldStreamer.Tick");
        public static readonly ProfilerMarker WorldStreamerLoad = new ProfilerMarker(ProfilerCategory.Scripts, "WorldStreamer.Load");
        public static readonly ProfilerMarker WorldStreamerDeactivate = new ProfilerMarker(ProfilerCategory.Scripts, "WorldStreamer.Deactivate");
        public static readonly ProfilerMarker WorldStreamerRelease = new ProfilerMarker(ProfilerCategory.Scripts, "WorldStreamer.Release");
        public static readonly ProfilerMarker UICodexBind = new ProfilerMarker(ProfilerCategory.Gui, "UI.CodexBind");
        public static readonly ProfilerMarker CodexRefresh = new ProfilerMarker(ProfilerCategory.Scripts, "Codex.Refresh");
        public static readonly ProfilerMarker PoolAcquire = new ProfilerMarker(ProfilerCategory.Scripts, "Pool.Acquire");
        public static readonly ProfilerMarker PoolRelease = new ProfilerMarker(ProfilerCategory.Scripts, "Pool.Release");
        public static readonly ProfilerMarker GameLoopTick = new ProfilerMarker(ProfilerCategory.Scripts, "GameLoop.Tick");
        public static readonly ProfilerMarker ScatterBuild = new ProfilerMarker(ProfilerCategory.Render, "Scatter.Build");
        public static readonly ProfilerMarker ScatterRender = new ProfilerMarker(ProfilerCategory.Render, "Scatter.Render");
        public static readonly ProfilerMarker AmbientLifeTick = new ProfilerMarker(ProfilerCategory.Scripts, "AmbientLife.Tick");
        public static readonly ProfilerMarker AmbientLifeRender = new ProfilerMarker(ProfilerCategory.Render, "AmbientLife.Render");
        public static readonly ProfilerMarker AtmosphereApply = new ProfilerMarker(ProfilerCategory.Scripts, "Atmosphere.Apply");
    }
}
