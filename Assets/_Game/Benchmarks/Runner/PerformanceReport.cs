using System;
using System.Collections.Generic;

namespace PocketWilds.Benchmarks
{
    /// <summary>One Performance Test Matrix scenario to measure.</summary>
    [Serializable]
    public sealed class PerformanceScenario
    {
        public string id;
        public string label;
        public int rabbits;
        public PocketWilds.Core.QualityTier tier;
        public bool hatchBurst;
        /// <summary>T23: every other animal of the save is a squirrel (two species share the meadow).</summary>
        public bool mixed;
        public float warmupSeconds = 2f;
        public float measureSeconds = 8f;

        public PerformanceScenario(string id, string label, int rabbits, PocketWilds.Core.QualityTier tier, bool hatchBurst = false)
        {
            this.id = id;
            this.label = label;
            this.rabbits = rabbits;
            this.tier = tier;
            this.hatchBurst = hatchBurst;
        }
    }

    /// <summary>Summary of one series of per-frame samples (milliseconds unless stated).</summary>
    [Serializable]
    public struct SampleStats
    {
        public bool valid;
        public float mean;
        public float p50;
        public float p95;
        public float p99;
        public float max;
    }

    [Serializable]
    public sealed class ScenarioResult
    {
        public string id;
        public string label;
        public string tier;
        public int rabbitsInSave;
        public int visibleAnimals;
        public int frames;
        /// <summary>FrameTimingManager samples contradicted by the wall clock and replaced by it.</summary>
        public int rejectedTimings;
        public float seconds;

        public SampleStats cpuFrameMs;
        public SampleStats mainThreadMs;
        public SampleStats renderThreadMs;
        public SampleStats gpuMs;
        public SampleStats simulationMs;
        public SampleStats animationMs;
        public SampleStats uiMs;

        /// <summary>Managed allocation per frame (bytes).</summary>
        public SampleStats gcBytes;
        public int framesWithGc;
        public int spikesOver33Ms;

        public float batches;
        public float setPassCalls;
        public float drawCalls;
        public float triangles;

        public float totalUsedMemoryMb;
        public float totalReservedMemoryMb;
        public float gcUsedMemoryMb;
        public float textureMemoryMb;
        public float meshMemoryMb;
    }

    /// <summary>Machine-readable baseline (Performance Test Matrix: JSON per version).</summary>
    [Serializable]
    public sealed class PerformanceReport
    {
        public string buildType;
        public string unityVersion;
        public string appVersion;
        public string device;
        public string os;
        public string cpu;
        public string gpu;
        public string graphicsApi;
        public int systemMemoryMb;
        public int screenWidth;
        public int screenHeight;
        public string timestampUtc;
        public List<ScenarioResult> scenarios = new List<ScenarioResult>();
    }
}
