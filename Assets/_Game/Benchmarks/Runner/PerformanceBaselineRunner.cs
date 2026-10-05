using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PocketWilds.Benchmarks
{
    /// <summary>
    /// T22 performance baseline (Performance Test Matrix P0 to P3, P5, plus the queue's 10/30/50 steps).
    /// Each scenario writes a save with N rabbits, boots the real game (SC_Bootstrap, quality forced),
    /// waits, then samples every frame: CPU / main / render thread / GPU times (FrameTimingManager),
    /// simulation, animation and UI markers, managed allocations, batches and memory (ProfilerRecorder).
    /// Sampling stores into preallocated arrays; statistics and JSON are built after the window.
    /// Development players start it with "-pw-benchmark &lt;report.json&gt;" and quit when done.
    /// </summary>
    public sealed class PerformanceBaselineRunner : MonoBehaviour
    {
        public const string CommandLineFlag = "-pw-benchmark";
        public const string BootstrapScene = "SC_Bootstrap";
        public const string SliceScene = "SC_VerticalSlice_Grassland";

        private static readonly (string id, RarityTier rarity)[] RabbitVariants =
        {
            ("variant.rabbit.brown", RarityTier.Common), ("variant.rabbit.white", RarityTier.Common),
            ("variant.rabbit.grey", RarityTier.Uncommon), ("variant.rabbit.black", RarityTier.Uncommon),
            ("variant.rabbit.spotted", RarityTier.Rare), ("variant.rabbit.sakura", RarityTier.Epic),
        };

        public static PerformanceScenario[] DefaultScenarios() => new[]
        {
            new PerformanceScenario("P0", "Empty sanctuary", 0, QualityTier.Medium),
            new PerformanceScenario("P1", "1 rabbit", 1, QualityTier.Medium),
            new PerformanceScenario("S10", "10 rabbits", 10, QualityTier.Medium),
            new PerformanceScenario("P2", "20 rabbits (vertical slice baseline)", 20, QualityTier.Medium),
            new PerformanceScenario("S30", "30 rabbits", 30, QualityTier.Medium),
            new PerformanceScenario("S50", "50 rabbits (30 A + 20 B)", 50, QualityTier.Medium),
            new PerformanceScenario("P2-Low", "20 rabbits, Low tier", 20, QualityTier.Low),
            new PerformanceScenario("S50-High", "50 rabbits, High tier", 50, QualityTier.High),
            new PerformanceScenario("P5", "Hatch burst: 10 hatches in a row, 20 rabbits", 20, QualityTier.Medium, hatchBurst: true)
            {
                measureSeconds = 14f,
            },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != CommandLineFlag)
                    continue;
                var go = new GameObject("PerformanceBaselineRunner");
                DontDestroyOnLoad(go);
                var runner = go.AddComponent<PerformanceBaselineRunner>();
                runner.StartCoroutine(runner.RunAndQuit(args[i + 1]));
                return;
            }
        }

        private IEnumerator RunAndQuit(string outPath)
        {
            PerformanceReport report = null;
            yield return RunAll(DefaultScenarios(), "Player (development)", r => report = r);
            Write(report, outPath);
            Debug.Log("[Benchmark] Report written to " + outPath);
            Application.Quit(0);
        }

        public static void Write(PerformanceReport report, string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }

        /// <summary>Runs every scenario through the real bootstrap. Usable from a player or a PlayMode test.</summary>
        public static IEnumerator RunAll(PerformanceScenario[] scenarios, string buildType, Action<PerformanceReport> done)
        {
            FrameTimingManager.CaptureFrameTimings();
            var report = new PerformanceReport
            {
                buildType = buildType,
                unityVersion = Application.unityVersion,
                appVersion = Application.version,
                device = SystemInfo.deviceModel,
                os = SystemInfo.operatingSystem,
                cpu = SystemInfo.processorType + " x" + SystemInfo.processorCount,
                gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                systemMemoryMb = SystemInfo.systemMemorySize,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                timestampUtc = DateTime.UtcNow.ToString("u"),
            };

            foreach (var scenario in scenarios)
            {
                string saveDir = Path.Combine(Application.temporaryCachePath, "pw-benchmark", scenario.id);
                PrepareSave(saveDir, scenario.rabbits);
                yield return Boot(saveDir, scenario.tier);
                var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                if (entry == null || entry.Sanctuary == null || bootstrap == null)
                {
                    Debug.LogError("[Benchmark] " + scenario.id + ": the sanctuary did not start.");
                    continue;
                }

                // No stray click may hatch an egg during a measure: player input is off, the game code is not.
                var input = entry.CameraRig != null ? entry.CameraRig.GetComponent<SanctuaryCameraInput>() : null;
                if (input != null)
                    input.enabled = false;
                var events = UnityEngine.EventSystems.EventSystem.current;
                if (events != null)
                    events.enabled = false;

                // Everything the measure needs is created before the warmup: its allocations never land in a sample.
                var sampler = new FrameSampler(Mathf.CeilToInt(scenario.measureSeconds * 240f) + 64);
                IEnumerator burst = scenario.hatchBurst ? HatchBurst(entry.Sanctuary, bootstrap.Context) : null;
                float until = Time.realtimeSinceStartup + scenario.warmupSeconds;
                while (Time.realtimeSinceStartup < until)
                    yield return null;
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < scenario.measureSeconds)
                {
                    yield return null;
                    sampler.Sample();
                    if (burst != null && !burst.MoveNext())
                        burst = null;
                }
                var result = sampler.Finish(scenario, entry.Sanctuary.VisibleAnimals, Time.realtimeSinceStartup - start);
                sampler.Dispose();
                report.scenarios.Add(result);
                Debug.Log($"[Benchmark] {scenario.id} {scenario.label} ({scenario.tier}): visible {result.visibleAnimals}, " +
                          $"CPU {result.cpuFrameMs.mean:F2} ms (p95 {result.cpuFrameMs.p95:F2}), main {result.mainThreadMs.mean:F2}, " +
                          $"GPU {(result.gpuMs.valid ? result.gpuMs.mean.ToString("F2") : "n/a")}, sim {result.simulationMs.mean:F3}, " +
                          $"GC {result.gcBytes.mean:F0} B/frame ({result.framesWithGc} frames), batches {result.batches:F0}");
            }

            foreach (var leftover in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(leftover.gameObject);
            done(report);
        }

        /// <summary>A real save: N rabbits cycling the six variants, all seen, Light for the hatch burst.</summary>
        private static void PrepareSave(string directory, int rabbits)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
            Directory.CreateDirectory(directory);
            var service = new SaveService(new SaveFileStore(directory), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var species = ContentId.FromSource("species.rabbit");
            for (int i = 0; i < rabbits; i++)
            {
                var (id, rarity) = RabbitVariants[i % RabbitVariants.Length];
                var variant = ContentId.FromSource(id);
                state.Collection.Add(new AnimalInstance
                {
                    Species = species,
                    Variant = variant,
                    Seed = SeedHash.Mix((uint)i + 1u, 0xBE7C0002u),
                    Rarity = rarity,
                });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            state.Wallet.Light = BigValue.FromMantissaExponent(1.0, 12);
            service.FlushNow();
        }

        private static IEnumerator Boot(string saveDirectory, QualityTier tier)
        {
            foreach (var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);
            yield return null;
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = saveDirectory,
                QualityOverride = tier,
                LoadTargetScene = true,
            };
            SceneManager.LoadScene(BootstrapScene, LoadSceneMode.Single);
            float timeout = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < timeout)
            {
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                if (bootstrap != null && bootstrap.State == BootState.Ready && entry != null && entry.Sanctuary != null)
                    yield break;
                if (bootstrap != null && bootstrap.State == BootState.Failed)
                    yield break;
                yield return null;
            }
        }

        /// <summary>P5: ten hatches as fast as a player can tap (hatch, tap to speed up, next egg).</summary>
        private static IEnumerator HatchBurst(SanctuaryController sanctuary, GameContext context)
        {
            for (int i = 0; i < 10; i++)
            {
                while (sanctuary.WaitingEgg == null || sanctuary.Hatch.IsPlaying)
                    yield return null;
                sanctuary.RequestHatch();
                float tap = Time.realtimeSinceStartup + 0.35f;
                while (Time.realtimeSinceStartup < tap)
                    yield return null;
                sanctuary.RequestHatch(); // speed up the reveal
                yield return null;
            }
        }

        /// <summary>Per-frame sampling into preallocated arrays: nothing allocates while measuring.</summary>
        private sealed class FrameSampler : IDisposable
        {
            private readonly float[] _cpu, _main, _render, _gpu, _sim, _anim, _ui, _gc;
            private readonly FrameTiming[] _timing = new FrameTiming[1];
            private readonly List<ProfilerRecorder> _simRecorders = new List<ProfilerRecorder>();
            private readonly List<ProfilerRecorder> _animRecorders = new List<ProfilerRecorder>();
            private readonly List<ProfilerRecorder> _uiRecorders = new List<ProfilerRecorder>();
            private ProfilerRecorder _mainThread, _gcAlloc, _batches, _setPass, _drawCalls, _triangles;
            private ProfilerRecorder _usedMemory, _reservedMemory, _gcMemory, _textureMemory, _meshMemory;
            private double _batchSum, _setPassSum, _drawSum, _triSum;
            private int _count;
            private int _spikes;
            private int _gcFrames;

            public FrameSampler(int capacity)
            {
                _cpu = new float[capacity];
                _main = new float[capacity];
                _render = new float[capacity];
                _gpu = new float[capacity];
                _sim = new float[capacity];
                _anim = new float[capacity];
                _ui = new float[capacity];
                _gc = new float[capacity];

                var available = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(available);
                var byName = new Dictionary<string, ProfilerRecorderHandle>(available.Count);
                foreach (var handle in available)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    byName[description.Name] = handle;
                }
                _mainThread = Start(byName, "Main Thread");
                _gcAlloc = Start(byName, "GC Allocated In Frame");
                _batches = Start(byName, "Batches Count");
                _setPass = Start(byName, "SetPass Calls Count");
                _drawCalls = Start(byName, "Draw Calls Count");
                _triangles = Start(byName, "Triangles Count");
                _usedMemory = Start(byName, "Total Used Memory");
                _reservedMemory = Start(byName, "Total Reserved Memory");
                _gcMemory = Start(byName, "GC Used Memory");
                _textureMemory = Start(byName, "Texture Memory");
                _meshMemory = Start(byName, "Mesh Memory");
                AddMarker(byName, _simRecorders, "Scheduler.TickA");
                AddMarker(byName, _simRecorders, "Scheduler.TickB");
                AddMarker(byName, _simRecorders, "GameLoop.Tick");
                AddMarker(byName, _animRecorders, "PreLateUpdate.DirectorUpdateAnimationBegin");
                AddMarker(byName, _animRecorders, "PreLateUpdate.DirectorUpdateAnimationEnd");
                AddMarker(byName, _uiRecorders, "PostLateUpdate.PlayerUpdateCanvases");
            }

            private static ProfilerRecorder Start(Dictionary<string, ProfilerRecorderHandle> byName, string name)
            {
                if (!byName.TryGetValue(name, out var handle))
                    return default;
                var description = ProfilerRecorderHandle.GetDescription(handle);
                return ProfilerRecorder.StartNew(description.Category, name, 1);
            }

            private static void AddMarker(Dictionary<string, ProfilerRecorderHandle> byName, List<ProfilerRecorder> list, string name)
            {
                var recorder = Start(byName, name);
                if (recorder.Valid)
                    list.Add(recorder);
            }

            public void Sample()
            {
                if (_count >= _cpu.Length)
                    return;
                FrameTimingManager.CaptureFrameTimings();
                bool timing = FrameTimingManager.GetLatestTimings(1, _timing) > 0;
                int i = _count++;
                _cpu[i] = timing ? (float)_timing[0].cpuFrameTime : Time.unscaledDeltaTime * 1000f;
                _main[i] = timing && _timing[0].cpuMainThreadFrameTime > 0 ? (float)_timing[0].cpuMainThreadFrameTime : Ms(_mainThread);
                _render[i] = timing ? (float)_timing[0].cpuRenderThreadFrameTime : 0f;
                _gpu[i] = timing ? (float)_timing[0].gpuFrameTime : 0f;
                _sim[i] = SumMs(_simRecorders);
                _anim[i] = SumMs(_animRecorders);
                _ui[i] = SumMs(_uiRecorders);
                float gc = _gcAlloc.Valid ? _gcAlloc.LastValue : 0f;
                _gc[i] = gc;
                if (gc > 0f)
                    _gcFrames++;
                if (_cpu[i] > 33.3f)
                    _spikes++;
                _batchSum += _batches.Valid ? _batches.LastValue : 0;
                _setPassSum += _setPass.Valid ? _setPass.LastValue : 0;
                _drawSum += _drawCalls.Valid ? _drawCalls.LastValue : 0;
                _triSum += _triangles.Valid ? _triangles.LastValue : 0;
            }

            private static float Ms(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue * 1e-6f : 0f;

            private static float SumMs(List<ProfilerRecorder> recorders)
            {
                float total = 0f;
                for (int i = 0; i < recorders.Count; i++)
                    total += recorders[i].LastValue * 1e-6f;
                return total;
            }

            public ScenarioResult Finish(PerformanceScenario scenario, int visible, float seconds)
            {
                const float Mb = 1f / (1024f * 1024f);
                int n = Math.Max(1, _count);
                return new ScenarioResult
                {
                    id = scenario.id,
                    label = scenario.label,
                    tier = scenario.tier.ToString(),
                    rabbitsInSave = scenario.rabbits,
                    visibleAnimals = visible,
                    frames = _count,
                    seconds = seconds,
                    cpuFrameMs = Stats(_cpu),
                    mainThreadMs = Stats(_main),
                    renderThreadMs = Stats(_render),
                    gpuMs = Stats(_gpu),
                    simulationMs = _simRecorders.Count > 0 ? Stats(_sim) : default,
                    animationMs = _animRecorders.Count > 0 ? Stats(_anim) : default,
                    uiMs = _uiRecorders.Count > 0 ? Stats(_ui) : default,
                    gcBytes = _gcAlloc.Valid ? Stats(_gc) : default,
                    framesWithGc = _gcFrames,
                    spikesOver33Ms = _spikes,
                    batches = (float)(_batchSum / n),
                    setPassCalls = (float)(_setPassSum / n),
                    drawCalls = (float)(_drawSum / n),
                    triangles = (float)(_triSum / n),
                    totalUsedMemoryMb = _usedMemory.Valid ? _usedMemory.LastValue * Mb : 0f,
                    totalReservedMemoryMb = _reservedMemory.Valid ? _reservedMemory.LastValue * Mb : 0f,
                    gcUsedMemoryMb = _gcMemory.Valid ? _gcMemory.LastValue * Mb : 0f,
                    textureMemoryMb = _textureMemory.Valid ? _textureMemory.LastValue * Mb : 0f,
                    meshMemoryMb = _meshMemory.Valid ? _meshMemory.LastValue * Mb : 0f,
                };
            }

            private SampleStats Stats(float[] series)
            {
                if (_count == 0)
                    return default;
                var copy = new float[_count];
                Array.Copy(series, copy, _count);
                Array.Sort(copy);
                double sum = 0.0;
                bool any = false;
                for (int i = 0; i < _count; i++)
                {
                    sum += copy[i];
                    any |= copy[i] > 0f;
                }
                return new SampleStats
                {
                    valid = any,
                    mean = (float)(sum / _count),
                    p50 = copy[(int)((_count - 1) * 0.50f)],
                    p95 = copy[(int)((_count - 1) * 0.95f)],
                    p99 = copy[(int)((_count - 1) * 0.99f)],
                    max = copy[_count - 1],
                };
            }

            public void Dispose()
            {
                _mainThread.Dispose();
                _gcAlloc.Dispose();
                _batches.Dispose();
                _setPass.Dispose();
                _drawCalls.Dispose();
                _triangles.Dispose();
                _usedMemory.Dispose();
                _reservedMemory.Dispose();
                _gcMemory.Dispose();
                _textureMemory.Dispose();
                _meshMemory.Dispose();
                foreach (var r in _simRecorders) r.Dispose();
                foreach (var r in _animRecorders) r.Dispose();
                foreach (var r in _uiRecorders) r.Dispose();
            }
        }
    }
}
