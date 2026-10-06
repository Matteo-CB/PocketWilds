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
        /// <summary>T27: the camera crosses into the meadow chunk and back twice (streaming in and out).</summary>
        public bool streaming;

        /// <summary>T28: the shop opens, a Gem pack is bought in the sandbox store, the shop closes and reopens.</summary>
        public bool shop;
        /// <summary>T33B: the camera travels the whole strip of chunks east and back (streaming, water, landmarks).</summary>
        public bool stripTravel;
        /// <summary>T33B: a light shower is forced for the whole measure.</summary>
        public bool rain;
        /// <summary>T34: every adaptive quality step from the boot tier down to Low and back up, one every 1.2 s.</summary>
        public bool qualityLadder;
        /// <summary>T34A: every feedback effect at once (worst case), then one by one, the animal sheet, the egg odds and a toast.</summary>
        public bool feedbackBurst;
        /// <summary>T34B: every animal keeps asking for its voice, taps are spammed, collects and hatches repeat, 50 reactions at once.</summary>
        public bool audioStorm;
        /// <summary>T34C: a scene of its own (SC_Benchmark_UI, SC_Benchmark_VFX) instead of the refuge.</summary>
        public string scene;
        /// <summary>Grassland species cycled through the save: 1 rabbits only, 2 + squirrels (T23), 3 + hedgehogs (T24).</summary>
        public int species = 1;
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
        /// <summary>T33: scatter render, ambient life tick and render, atmosphere apply (CPU, ms).</summary>
        public SampleStats worldMs;
        /// <summary>T33: ground detail and ambient life instances drawn per frame (average).</summary>
        public float scatterInstances;
        public float lifeInstances;

        /// <summary>
        /// T33C no-collision rule, checked in the player every 60 frames: smallest free space (cm) between an animal
        /// body and a prop, the water, the egg spot or another animal; between an insect or bird and a volume, an
        /// animal or its kind. overlapSamples counts the checks that found an overlap (must stay 0).
        /// </summary>
        public float minAnimalClearanceCm;
        public float minLifeClearanceCm;
        public int overlapSamples;

        /// <summary>T34: longest frame of the first seconds in the refuge, where everything is displayed for the first time (ms).</summary>
        public float entryMaxFrameMs;
        /// <summary>When the measure's longest frame happened (s after the measure started).</summary>
        public float maxFrameAtSeconds;
        /// <summary>T34: frame work time as adaptive quality reads it (FrameTimingManager), 95th percentile (ms).</summary>
        public float adaptiveWorkP95Ms;
        /// <summary>T34 quality ladder: steps applied and the longest frame among the 3 after each step (ms).</summary>
        public int qualitySwitches;
        public float maxSwitchFrameMs;

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

        public StreamingResult streaming;
        public ShopResult shop;

        /// <summary>
        /// T34B audio (every scenario, beds playing, output muted): main-thread audio update (AudioManager.Update, ms per
        /// frame), one-shot voices at once at most, sounds started and requests dropped (cooldowns, caps, off view).
        /// </summary>
        /// <summary>T34C C1 (gate 13): Codex with 5000 species and a 5000-animal collection: open, fast scroll, reopen cycles.</summary>
        public UiStressResult ui;
        /// <summary>T34C V1 (L-SC-008): feedback effects at the pool's maximum concurrency while the tier changes.</summary>
        public VfxStressResult vfx;

        public float audioMainMs;
        public float audioMainMaxMs;
        public int audioVoicesPeak;
        public int audioStarted;
        public int audioDropped;
        public bool audioBedsPlaying;
    }

    [Serializable]
    public sealed class UiStressResult
    {
        public bool valid;
        public int species;
        public int animals;
        public int rows;
        public int rowViews;
        public float firstOpenMs;
        public float reopenMaxMs;
        public int openCloseCycles;
    }

    [Serializable]
    public sealed class VfxStressResult
    {
        public bool valid;
        public int peakPlaying;
        public int spawned;
        public int tierSwitches;
    }

    /// <summary>T28: CPU time of the frames that open, buy in, close and reopen the shop.</summary>
    [Serializable]
    public struct ShopResult
    {
        public bool valid;
        public float firstOpenFrameMs;
        public float purchaseFrameMs;
        public float closeFrameMs;
        public float reopenFrameMs;
        public long gemsAfter;
    }

    /// <summary>T27 chunk streaming: transitions, their frame cost, and memory with the meadow out, in, out.</summary>
    [Serializable]
    public struct StreamingResult
    {
        public bool valid;
        public int loads;
        public int activations;
        public int deactivations;
        public int releases;
        public int failures;
        public int lateActivations;
        /// <summary>Frames where a chunk changed state (load start, staged, active, inactive, released).</summary>
        public int transitionFrames;
        public float transitionFrameMaxMs;
        /// <summary>WorldStreamer.Tick per frame (includes load, bind, activate and release work).</summary>
        public float streamerTickMaxMs;
        public float meshMemoryReleasedMb;
        public float meshMemoryLoadedMb;
        public float meshMemoryReleasedAgainMb;
        public float meshMemoryReleasedFinalMb;
        public long assetCountReleased;
        public long assetCountLoaded;
        public long assetCountReleasedAgain;
        /// <summary>After the measure: meadow-only meshes in memory and loaded bundles, out / in / out.</summary>
        public int meadowMeshesBefore;
        public int meadowMeshesLoaded;
        public int meadowMeshesAfter;
        public int bundlesBefore;
        public int bundlesLoaded;
        public int bundlesAfter;
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
        /// <summary>T34 shader warmup at the first boot: "states" (traced), "variants" (fallback), "none" or "skipped".</summary>
        public string warmupSource;
        public int warmupCount;
        public float warmupMs;
        public float warmupLongestFrameMs;
        public List<ScenarioResult> scenarios = new List<ScenarioResult>();
    }
}
