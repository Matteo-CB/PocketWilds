using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.UI;
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
        /// <summary>Optional: "-pw-scenarios P2,P2-Mix" runs only these scenario ids.</summary>
        public const string ScenarioFilterFlag = "-pw-scenarios";
        public const string BootstrapScene = "SC_Bootstrap";
        public const string SliceScene = "SC_VerticalSlice_Grassland";

        private static readonly (string id, RarityTier rarity)[] RabbitVariants =
        {
            ("variant.rabbit.brown", RarityTier.Common), ("variant.rabbit.white", RarityTier.Common),
            ("variant.rabbit.grey", RarityTier.Uncommon), ("variant.rabbit.black", RarityTier.Uncommon),
            ("variant.rabbit.spotted", RarityTier.Rare), ("variant.rabbit.sakura", RarityTier.Epic),
        };

        private static readonly (string id, RarityTier rarity)[] SquirrelVariants =
        {
            ("variant.squirrel.red", RarityTier.Common), ("variant.squirrel.brown", RarityTier.Common),
            ("variant.squirrel.grey", RarityTier.Uncommon), ("variant.squirrel.cream", RarityTier.Uncommon),
            ("variant.squirrel.black", RarityTier.Rare), ("variant.squirrel.autumn", RarityTier.Epic),
        };

        private static readonly (string id, RarityTier rarity)[] HedgehogVariants =
        {
            ("variant.hedgehog.brown", RarityTier.Common), ("variant.hedgehog.cream", RarityTier.Common),
            ("variant.hedgehog.grey", RarityTier.Uncommon), ("variant.hedgehog.black", RarityTier.Uncommon),
            ("variant.hedgehog.white", RarityTier.Rare), ("variant.hedgehog.mushroom", RarityTier.Epic),
        };

        public static PerformanceScenario[] DefaultScenarios() => new[]
        {
            new PerformanceScenario("P0", "Empty refuge", 0, QualityTier.Medium),
            new PerformanceScenario("P1", "1 rabbit", 1, QualityTier.Medium),
            new PerformanceScenario("S10", "10 rabbits", 10, QualityTier.Medium),
            new PerformanceScenario("P2", "20 rabbits (vertical slice baseline)", 20, QualityTier.Medium),
            new PerformanceScenario("S30", "30 rabbits", 30, QualityTier.Medium),
            new PerformanceScenario("S50", "50 rabbits (30 A + 20 B)", 50, QualityTier.Medium),
            new PerformanceScenario("P2-Low", "20 rabbits, Low tier", 20, QualityTier.Low),
            new PerformanceScenario("S50-High", "50 rabbits, High tier", 50, QualityTier.High),
            new PerformanceScenario("P2-High", "20 animals, rabbits, squirrels and hedgehogs, High tier (Prairie Magnifique)", 20, QualityTier.High) { species = 3 },
            new PerformanceScenario("P5", "Hatch burst: 10 hatches in a row, 20 rabbits", 20, QualityTier.Medium, hatchBurst: true)
            {
                measureSeconds = 14f,
            },
            new PerformanceScenario("P2-Mix", "20 animals, 10 rabbits + 10 squirrels", 20, QualityTier.Medium) { species = 2 },
            new PerformanceScenario("S50-Mix", "50 animals, 25 rabbits + 25 squirrels", 50, QualityTier.Medium) { species = 2 },
            new PerformanceScenario("P2-Mix3", "20 animals, rabbits, squirrels and hedgehogs", 20, QualityTier.Medium) { species = 3 },
            new PerformanceScenario("S50-Mix3", "50 animals, rabbits, squirrels and hedgehogs", 50, QualityTier.Medium) { species = 3 },
            new PerformanceScenario("W1", "Chunk streaming: meadow in and out twice, 20 animals", 20, QualityTier.Medium)
            {
                species = 3, streaming = true, measureSeconds = 28f,
            },
            new PerformanceScenario("U1", "Shop: open, buy a Gem pack (sandbox), close, reopen, 20 animals", 20, QualityTier.Medium)
            {
                species = 3, shop = true, measureSeconds = 10f,
            },
            new PerformanceScenario("W2", "Strip travel: the camera crosses the eight Grassland chunks east and back, 20 animals", 20, QualityTier.Medium)
            {
                species = 3, stripTravel = true, measureSeconds = 34f,
            },
            new PerformanceScenario("R1", "Light rain over the refuge, 20 animals", 20, QualityTier.Medium) { species = 3, rain = true },
            new PerformanceScenario("R1-High", "Light rain over the refuge, 20 animals, High tier", 20, QualityTier.High) { species = 3, rain = true },
            new PerformanceScenario("Q1", "Adaptive quality: every step from Magnifique down to Fluide and back, 20 animals", 20, QualityTier.High)
            {
                species = 3, qualityLadder = true, measureSeconds = 26f,
            },
            new PerformanceScenario("F1", "Feedback: every effect at once, then one by one, animal sheet, egg odds, toast, 20 animals", 20, QualityTier.High)
            {
                species = 3, feedbackBurst = true, measureSeconds = 16f,
            },
        };

        private static PerformanceScenario[] Filter(PerformanceScenario[] all, string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != ScenarioFilterFlag)
                    continue;
                var wanted = new System.Collections.Generic.HashSet<string>(args[i + 1].Split(','));
                return Array.FindAll(all, s => wanted.Contains(s.id));
            }
            return all;
        }

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
            yield return RunAll(Filter(DefaultScenarios(), Environment.GetCommandLineArgs()), "Player (development)", r => report = r);
            int exitCode = 0;
            try
            {
                Write(report, outPath);
                Debug.Log("[Benchmark] Report written to " + outPath);
            }
            catch (Exception ex)
            {
                // An unwritable report path must not leave the player running forever.
                Debug.LogException(ex);
                exitCode = 1;
            }
            Application.Quit(exitCode);
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
                PrepareSave(saveDir, scenario.rabbits, scenario.species);
                yield return Boot(saveDir, scenario.tier);
                var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                if (entry == null || entry.Refuge == null || bootstrap == null)
                {
                    Debug.LogError("[Benchmark] " + scenario.id + ": the refuge did not start.");
                    continue;
                }

                // No stray click may hatch an egg during a measure: player input is off, the game code is not.
                var input = entry.CameraRig != null ? entry.CameraRig.GetComponent<RefugeCameraInput>() : null;
                if (input != null)
                    input.enabled = false;
                var events = UnityEngine.EventSystems.EventSystem.current;
                if (events != null)
                    events.enabled = false;

                // Everything the measure needs is created before the warmup: its allocations never land in a sample.
                var sampler = new FrameSampler(Mathf.CeilToInt(scenario.measureSeconds * 240f) + 64);
                var probe = scenario.streaming && entry.Streamer != null ? new StreamingProbe(entry) : null;
                var shopProbe = scenario.shop && entry.Refuge.Shop != null ? new ShopProbe(entry.Refuge, bootstrap.Context) : null;
                IEnumerator burst = scenario.hatchBurst ? HatchBurst(entry.Refuge, bootstrap.Context) : probe != null ? probe.Travel() : shopProbe?.Run();
                if (scenario.stripTravel && entry.CameraRig != null)
                    burst = StripTravel(entry.CameraRig, 7f);
                if (scenario.rain)
                    entry.Weather?.Force(1f, 0f);
                var ladder = scenario.qualityLadder ? new QualityLadderProbe(bootstrap.Context.Quality, scenario.tier) : null;
                if (ladder != null)
                    burst = ladder.Run();
                if (scenario.feedbackBurst)
                    burst = FeedbackBurst(entry.Refuge, bootstrap.Context);
                var work = new float[Mathf.CeilToInt(scenario.measureSeconds * 240f) + 64];
                int workCount = 0;
                if (report.warmupSource == null)
                {
                    report.warmupSource = ShaderWarmup.LastSource;
                    report.warmupCount = ShaderWarmup.LastWarmed;
                    report.warmupMs = (float)ShaderWarmup.LastMilliseconds;
                    report.warmupLongestFrameMs = ShaderWarmup.LastLongestFrameMs;
                }
                // Garbage left by earlier scenarios (one process, many boots) is collected now, not in a sample.
                GC.Collect();
                float until = Time.realtimeSinceStartup + scenario.warmupSeconds;
                // T34: the first frames of the refuge draw everything for the first time (first displays).
                float entryMax = 0f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    entryMax = Mathf.Max(entryMax, Time.unscaledDeltaTime * 1000f);
                }
                float start = Time.realtimeSinceStartup;
                float animalClearance = float.MaxValue, lifeClearance = float.MaxValue;
                float maxFrame = 0f, maxFrameAt = 0f;
                int overlaps = 0, frame = 0;
                while (Time.realtimeSinceStartup - start < scenario.measureSeconds)
                {
                    yield return null;
                    sampler.Sample();
                    // One frame in 60 (under the 5 % the p95 looks at): the check itself never shows in the timings.
                    if (++frame % 60 == 0)
                        CheckCollisions(entry, ref animalClearance, ref lifeClearance, ref overlaps);
                    if (workCount < work.Length)
                        work[workCount++] = bootstrap.Context.Quality.LastWorkMs;
                    if (sampler.LastCpuMs > maxFrame)
                    {
                        maxFrame = sampler.LastCpuMs;
                        maxFrameAt = Time.realtimeSinceStartup - start;
                    }
                    ladder?.AfterSample(sampler.LastCpuMs);
                    sampler.SampleWorld(entry.Scatter != null ? entry.Scatter.LastInstances : 0, entry.Life != null ? entry.Life.LastInstances : 0);
                    probe?.AfterSample(sampler.LastCpuMs);
                    shopProbe?.AfterSample(sampler.LastCpuMs);
                    if (burst != null && !burst.MoveNext())
                        burst = null;
                }
                var result = sampler.Finish(scenario, entry.Refuge.VisibleAnimals, Time.realtimeSinceStartup - start);
                sampler.Dispose();
                result.adaptiveWorkP95Ms = Percentile95(work, workCount);
                result.entryMaxFrameMs = entryMax;
                result.maxFrameAtSeconds = maxFrameAt;
                if (maxFrame > 33.3f)
                    Debug.Log($"[Benchmark] {scenario.id} longest frame {maxFrame:F1} ms at {maxFrameAt:F2} s into the measure");
                if (ladder != null)
                {
                    result.qualitySwitches = ladder.Switches;
                    result.maxSwitchFrameMs = ladder.MaxSwitchFrameMs;
                    Debug.Log($"[Benchmark] {scenario.id} quality ladder: {ladder.Switches} steps, longest frame after a step {ladder.MaxSwitchFrameMs:F2} ms");
                }
                result.minAnimalClearanceCm = animalClearance < float.MaxValue ? animalClearance * 100f : 0f;
                result.minLifeClearanceCm = lifeClearance < float.MaxValue ? lifeClearance * 100f : 0f;
                result.overlapSamples = overlaps;
                if (shopProbe != null)
                {
                    result.shop = shopProbe.Finish();
                    Debug.Log($"[Benchmark] {scenario.id} shop: first open {result.shop.firstOpenFrameMs:F2} ms, purchase {result.shop.purchaseFrameMs:F2} ms, " +
                              $"close {result.shop.closeFrameMs:F2} ms, reopen {result.shop.reopenFrameMs:F2} ms, Gems {result.shop.gemsAfter}");
                }
                if (probe != null)
                {
                    // Unmeasured: name the meshes and bundles in memory with the meadow out, in, out.
                    yield return probe.ReleaseCheck();
                    result.streaming = probe.Finish();
                    probe.Dispose();
                    Debug.Log($"[Benchmark] {scenario.id} streaming: loads {result.streaming.loads}, releases {result.streaming.releases}, " +
                              $"late {result.streaming.lateActivations}, transition frames {result.streaming.transitionFrames} " +
                              $"(max {result.streaming.transitionFrameMaxMs:F2} ms), tick max {result.streaming.streamerTickMaxMs:F2} ms, " +
                              $"mesh {result.streaming.meshMemoryReleasedMb:F2} / {result.streaming.meshMemoryLoadedMb:F2} / {result.streaming.meshMemoryReleasedAgainMb:F2} MB, " +
                              $"meadow meshes {result.streaming.meadowMeshesBefore} / {result.streaming.meadowMeshesLoaded} / {result.streaming.meadowMeshesAfter}, " +
                              $"bundles {result.streaming.bundlesBefore} / {result.streaming.bundlesLoaded} / {result.streaming.bundlesAfter}");
                }
                report.scenarios.Add(result);
                Debug.Log($"[Benchmark] {scenario.id} {scenario.label} ({scenario.tier}): visible {result.visibleAnimals}, " +
                          $"CPU {result.cpuFrameMs.mean:F2} ms (p95 {result.cpuFrameMs.p95:F2}), main {result.mainThreadMs.mean:F2}, " +
                          $"GPU {(result.gpuMs.valid ? result.gpuMs.mean.ToString("F2") : "n/a")}, sim {result.simulationMs.mean:F3}, " +
                          $"GC {result.gcBytes.mean:F0} B/frame ({result.framesWithGc} frames), batches {result.batches:F0}, " +
                          $"world {result.worldMs.mean:F3} ms (max {result.worldMs.max:F3}), scatter {result.scatterInstances:F0}, life {result.lifeInstances:F0}, " +
                          $"triangles {result.triangles:F0}, clearance animals {result.minAnimalClearanceCm:F1} cm, life {result.minLifeClearanceCm:F1} cm, " +
                          $"overlaps {result.overlapSamples}");
            }

            foreach (var leftover in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(leftover.gameObject);
            done(report);
        }

        /// <summary>T33B: east along the whole strip of chunks, then back to the refuge. No allocation per step.</summary>
        private static float Percentile95(float[] values, int count)
        {
            if (count == 0)
                return 0f;
            Array.Sort(values, 0, count);
            return values[Math.Min(count - 1, Mathf.CeilToInt(count * 0.95f) - 1)];
        }

        /// <summary>
        /// T34 Q1: walks every adaptive quality step (life, ground, weather, shadows, resolution, per tier) from the boot
        /// tier down to Low and back up, one every 1.2 s, and keeps the longest frame among the 10 after each step.
        /// Adaptive steps of its own are off during the walk. In a traced run it also records every mixed state.
        /// </summary>
        private sealed class QualityLadderProbe
        {
            private const float StepSeconds = 1.2f;
            private readonly QualityService _quality;
            private readonly QualityTier _top;
            private int _watch;

            public QualityLadderProbe(QualityService quality, QualityTier top)
            {
                _quality = quality;
                _top = top;
            }

            public int Switches { get; private set; }
            public float MaxSwitchFrameMs { get; private set; }

            /// <summary>One flat enumerator: the measure loop steps it by hand, nested enumerators would not run.</summary>
            public IEnumerator Run()
            {
                _quality.SetAdaptiveEnabled(false);
                var state = QualityState.Uniform(_top);
                bool down = true;
                while (true)
                {
                    bool moved = down ? state.StepDown(out _) : state.StepUp(_top, out _);
                    if (!moved)
                    {
                        if (!down)
                            break;
                        down = false;
                        continue;
                    }
                    _quality.ForceState(state);
                    Switches++;
                    _watch = 10;
                    float until = Time.realtimeSinceStartup + StepSeconds;
                    while (Time.realtimeSinceStartup < until)
                        yield return null;
                }
                _quality.SetAdaptiveEnabled(true);
            }

            public void AfterSample(float cpuMs)
            {
                if (_watch <= 0)
                    return;
                _watch--;
                if (cpuMs > MaxSwitchFrameMs)
                    MaxSwitchFrameMs = cpuMs;
            }
        }

        /// <summary>T33C: every animal and every insect or bird against everything it must never enter.</summary>
        private static void CheckCollisions(VerticalSliceEntry entry, ref float animals, ref float life, ref int overlaps)
        {
            var scheduler = entry.Refuge.Scheduler;
            for (int i = 0; i < scheduler.Count; i++)
            {
                float clearance = scheduler.Clearance(scheduler.GetActiveHandle(i));
                animals = Mathf.Min(animals, clearance);
                if (clearance < -AnimalScheduler.ContactTolerance - 1e-4f)
                    overlaps++;
            }
            if (entry.Life == null)
                return;
            float lifeClearance = entry.Life.MinClearance();
            life = Mathf.Min(life, lifeClearance);
            if (lifeClearance < -0.005f)
                overlaps++;
        }

        private static IEnumerator StripTravel(RefugeCameraRig rig, float speed)
        {
            const float East = 98f, West = -3.5f;
            while (true)
            {
                var focus = rig.Focus;
                focus.x = Mathf.MoveTowards(focus.x, East, speed * Time.unscaledDeltaTime);
                rig.SnapTo(focus);
                if (Mathf.Abs(focus.x - East) < 0.001f)
                    break;
                yield return null;
            }
            while (true)
            {
                var focus = rig.Focus;
                focus.x = Mathf.MoveTowards(focus.x, West, speed * Time.unscaledDeltaTime);
                rig.SnapTo(focus);
                if (Mathf.Abs(focus.x - West) < 0.001f)
                    break;
                yield return null;
            }
        }

        /// <summary>
        /// A real save: N animals cycling the grassland species (1 to 3) and each species' six variants, all
        /// seen, Light for the hatch burst.
        /// </summary>
        private static void PrepareSave(string directory, int rabbits, int speciesCount)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
            Directory.CreateDirectory(directory);
            var service = new SaveService(new SaveFileStore(directory), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            var ids = new[] { ContentId.FromSource("species.rabbit"), ContentId.FromSource("species.squirrel"), ContentId.FromSource("species.hedgehog") };
            var tables = new[] { RabbitVariants, SquirrelVariants, HedgehogVariants };
            speciesCount = Mathf.Clamp(speciesCount, 1, ids.Length);
            for (int i = 0; i < rabbits; i++)
            {
                int which = i % speciesCount;
                var species = ids[which];
                var table = tables[which];
                var (id, rarity) = table[(i / speciesCount) % table.Length];
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
                if (bootstrap != null && bootstrap.State == BootState.Ready && entry != null && entry.Refuge != null)
                    yield break;
                if (bootstrap != null && bootstrap.State == BootState.Failed)
                    yield break;
                yield return null;
            }
        }

        /// <summary>
        /// T34A F1 (Gate 10 worst case on this PC): every feedback effect at once near the egg, then each alone every
        /// 0.4 s, then the animal sheet of a visible animal, the egg odds and a toast, each opened and closed. In a traced
        /// run it also records every graphics state of the effects and the new screens.
        /// </summary>
        private static IEnumerator FeedbackBurst(RefugeController refuge, GameContext context)
        {
            var vfx = refuge.Vfx;
            var egg = refuge.WaitingEgg != null ? refuge.WaitingEgg.VfxPoint.position : Vector3.zero;
            if (vfx != null)
            {
                for (int i = 0; i < (int)VfxId.Count; i++)
                    vfx.Play((VfxId)i, egg + new Vector3((i % 4 - 1.5f) * 0.3f, 0f, (i / 4) * 0.3f), refuge.SimulationTime, Color.white);
            }
            float wait = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < wait)
                yield return null;
            for (int i = 0; vfx != null && i < (int)VfxId.Count; i++)
            {
                vfx.Play((VfxId)i, egg, refuge.SimulationTime);
                wait = Time.realtimeSinceStartup + 0.4f;
                while (Time.realtimeSinceStartup < wait)
                    yield return null;
            }
            var sheet = Object.FindAnyObjectByType<AnimalSheetScreen>();
            AnimalView view = null;
            foreach (var candidate in Object.FindObjectsByType<AnimalView>(FindObjectsSortMode.None))
                if (candidate.gameObject.activeInHierarchy && candidate.OwnerInstanceId != 0)
                    view = candidate;
            if (sheet != null && view != null && refuge.TryBuildSheet(view.OwnerInstanceId, out var data))
            {
                sheet.Open(data, context.Localization, refuge);
                wait = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < wait)
                    yield return null;
                sheet.Close();
                wait = Time.realtimeSinceStartup + 0.5f;
                while (Time.realtimeSinceStartup < wait)
                    yield return null;
            }
            refuge.OpenEggInfo();
            wait = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < wait)
                yield return null;
            var modal = Object.FindAnyObjectByType<ModalScreen>();
            modal?.Close();
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            hud?.ShowToast(ToastKind.Warning, context.Localization.Get(FeedbackText.ToastNotEnoughLight));
            hud?.PlayCollect(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        /// <summary>P5: ten hatches as fast as a player can tap (hatch, tap to speed up, next egg).</summary>
        private static IEnumerator HatchBurst(RefugeController refuge, GameContext context)
        {
            for (int i = 0; i < 10; i++)
            {
                while (refuge.WaitingEgg == null || refuge.Hatch.IsPlaying)
                    yield return null;
                refuge.RequestHatch();
                float tap = Time.realtimeSinceStartup + 0.35f;
                while (Time.realtimeSinceStartup < tap)
                    yield return null;
                refuge.RequestHatch(); // speed up the reveal
                yield return null;
            }
        }

        /// <summary>
        /// W1: drives the camera like a finger drag (6 m/s) from the west end of the start chunk to the meadow's
        /// centre and back, twice, and records streamer transitions, their frame cost and memory checkpoints with
        /// allocation-free recorders. The name-level release check runs after the measure.
        /// </summary>
        private sealed class StreamingProbe : IDisposable
        {
            private const float Speed = 6f;
            private const float West = -3.5f;
            private const float MeadowCentre = 14f;
            private readonly WorldStreamer _streamer;
            private readonly RefugeCameraRig _rig;
            private readonly int _meadow;
            private readonly Action<int, ChunkStreamState> _onState;
            private readonly WorldStreamerStats _before;
            private ProfilerRecorder _tick, _mesh, _assets;
            private StreamingResult _result;
            private bool _transition;

            public StreamingProbe(VerticalSliceEntry entry)
            {
                _streamer = entry.Streamer;
                _rig = entry.CameraRig;
                _meadow = _streamer.IndexOf("chunk.grassland.meadow");
                _onState = (index, state) => _transition = true;
                _streamer.StateChanged += _onState;
                _tick = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "WorldStreamer.Tick", 1);
                _mesh = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory", 1);
                _assets = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Asset Count", 1);
                _rig.SnapTo(new Vector3(West, 0f, _rig.Focus.z));
                _before = _streamer.Stats;
                _result.valid = true;
            }

            private float MeshMb => _mesh.Valid ? _mesh.LastValue / (1024f * 1024f) : 0f;
            private long Assets => _assets.Valid ? _assets.LastValue : 0;

            private bool Step(float x)
            {
                var focus = _rig.Focus;
                focus.x = Mathf.MoveTowards(focus.x, x, Speed * Time.unscaledDeltaTime);
                _rig.SnapTo(focus);
                return Mathf.Abs(focus.x - x) < 0.001f;
            }

            /// <summary>Stepped once per sampled frame (no nested iterators: the runner calls MoveNext itself).</summary>
            public IEnumerator Travel()
            {
                float t = 0f;
                while ((t += Time.unscaledDeltaTime) < 1f)
                    yield return null;
                _result.meshMemoryReleasedMb = MeshMb;
                _result.assetCountReleased = Assets;
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    while (!Step(MeadowCentre))
                        yield return null;
                    for (t = 0f; (t += Time.unscaledDeltaTime) < 2f;)
                        yield return null;
                    if (cycle == 0)
                    {
                        _result.meshMemoryLoadedMb = MeshMb;
                        _result.assetCountLoaded = Assets;
                    }
                    while (!Step(West))
                        yield return null;
                    for (t = 0f; (t += Time.unscaledDeltaTime) < 5f;)
                        yield return null;
                    if (cycle == 0)
                    {
                        _result.meshMemoryReleasedAgainMb = MeshMb;
                        _result.assetCountReleasedAgain = Assets;
                    }
                    else
                    {
                        _result.meshMemoryReleasedFinalMb = MeshMb;
                    }
                }
            }

            public void AfterSample(float cpuMs)
            {
                if (_tick.Valid)
                    _result.streamerTickMaxMs = Mathf.Max(_result.streamerTickMaxMs, _tick.LastValue * 1e-6f);
                if (!_transition)
                    return;
                _transition = false;
                _result.transitionFrames++;
                _result.transitionFrameMaxMs = Mathf.Max(_result.transitionFrameMaxMs, cpuMs);
            }

            private static int MeadowMeshes()
            {
                int count = 0;
                foreach (var mesh in Resources.FindObjectsOfTypeAll<Mesh>())
                    if (mesh.name.StartsWith("MSH_Chunk_Grassland_Meadow", StringComparison.Ordinal) || mesh.name == "GL_Ground_Core_B")
                        count++;
                return count;
            }

            private static int Bundles()
            {
                int count = 0;
                foreach (var _ in AssetBundle.GetAllLoadedAssetBundles())
                    count++;
                return count;
            }

            private IEnumerator WaitFor(ChunkStreamState state, float seconds)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (_streamer.GetState(_meadow) != state && Time.realtimeSinceStartup < until)
                    yield return null;
                yield return null; // deferred Destroy and bundle unload settle
            }

            public IEnumerator ReleaseCheck()
            {
                yield return WaitFor(ChunkStreamState.Unloaded, 8f);
                _result.meadowMeshesBefore = MeadowMeshes();
                _result.bundlesBefore = Bundles();
                while (!Step(MeadowCentre))
                    yield return null;
                yield return WaitFor(ChunkStreamState.Active, 5f);
                _result.meadowMeshesLoaded = MeadowMeshes();
                _result.bundlesLoaded = Bundles();
                while (!Step(West))
                    yield return null;
                yield return WaitFor(ChunkStreamState.Unloaded, 8f);
                _result.meadowMeshesAfter = MeadowMeshes();
                _result.bundlesAfter = Bundles();
            }

            public StreamingResult Finish()
            {
                var stats = _streamer.Stats;
                _result.loads = stats.loads - _before.loads;
                _result.activations = stats.activations - _before.activations;
                _result.deactivations = stats.deactivations - _before.deactivations;
                _result.releases = stats.releases - _before.releases;
                _result.failures = stats.failures - _before.failures;
                _result.lateActivations = stats.lateActivations - _before.lateActivations;
                return _result;
            }

            public void Dispose()
            {
                _streamer.StateChanged -= _onState;
                _tick.Dispose();
                _mesh.Dispose();
                _assets.Dispose();
            }
        }

        /// <summary>
        /// U1: the shop as a player uses it, through the refuge and the sandbox store. Each action's cost lands in
        /// the next sampled frame, which is recorded.
        /// </summary>
        private sealed class ShopProbe
        {
            private readonly RefugeController _refuge;
            private readonly GameContext _context;
            private ShopResult _result;
            private int _pending = -1;

            public ShopProbe(RefugeController refuge, GameContext context)
            {
                _refuge = refuge;
                _context = context;
                _result.valid = true;
            }

            public IEnumerator Run()
            {
                float t = 0f;
                while ((t += Time.unscaledDeltaTime) < 1.5f)
                    yield return null;
                _refuge.OpenShop();
                _pending = 0;
                for (t = 0f; (t += Time.unscaledDeltaTime) < 2f;)
                    yield return null;
                _refuge.Shop.Buy(PocketWilds.Core.ContentId.FromSource("offer.gems.tier01"));
                // The sandbox store answers after its simulated latency: the grant frame is the one to measure.
                long gems = _context.State.Wallet.Gems;
                while (_context.State.Wallet.Gems == gems && (t += Time.unscaledDeltaTime) < 6f)
                    yield return null;
                _pending = 1;
                for (t = 0f; (t += Time.unscaledDeltaTime) < 1.5f;)
                    yield return null;
                _refuge.Shop.CloseShop();
                _pending = 2;
                for (t = 0f; (t += Time.unscaledDeltaTime) < 1f;)
                    yield return null;
                _refuge.OpenShop();
                _pending = 3;
                for (t = 0f; (t += Time.unscaledDeltaTime) < 1f;)
                    yield return null;
                _refuge.Shop.CloseShop();
            }

            private int _window;
            private int _action = -1;

            /// <summary>Worst frame within 20 frames of each action (frame timings arrive a few frames late).</summary>
            public void AfterSample(float cpuMs)
            {
                if (_pending >= 0)
                {
                    _action = _pending;
                    _pending = -1;
                    _window = 20;
                }
                if (_window <= 0 || _action < 0)
                    return;
                _window--;
                switch (_action)
                {
                    case 0: _result.firstOpenFrameMs = Mathf.Max(_result.firstOpenFrameMs, cpuMs); break;
                    case 1: _result.purchaseFrameMs = Mathf.Max(_result.purchaseFrameMs, cpuMs); break;
                    case 2: _result.closeFrameMs = Mathf.Max(_result.closeFrameMs, cpuMs); break;
                    case 3: _result.reopenFrameMs = Mathf.Max(_result.reopenFrameMs, cpuMs); break;
                }
            }

            public ShopResult Finish()
            {
                _result.gemsAfter = _context.State.Wallet.Gems;
                return _result;
            }
        }

        /// <summary>Per-frame sampling into preallocated arrays: nothing allocates while measuring.</summary>
        private sealed class FrameSampler : IDisposable
        {
            private readonly float[] _cpu, _main, _render, _gpu, _sim, _anim, _ui, _gc, _world;
            private readonly FrameTiming[] _timing = new FrameTiming[1];
            private readonly List<ProfilerRecorder> _simRecorders = new List<ProfilerRecorder>();
            private readonly List<ProfilerRecorder> _animRecorders = new List<ProfilerRecorder>();
            private readonly List<ProfilerRecorder> _uiRecorders = new List<ProfilerRecorder>();
            private readonly List<ProfilerRecorder> _worldRecorders = new List<ProfilerRecorder>();
            private double _scatterSum, _lifeSum;
            private ProfilerRecorder _mainThread, _gcAlloc, _batches, _setPass, _drawCalls, _triangles;
            private ProfilerRecorder _usedMemory, _reservedMemory, _gcMemory, _textureMemory, _meshMemory;
            private double _batchSum, _setPassSum, _drawSum, _triSum;
            private int _count;
            private int _spikes;
            private int _gcFrames;
            private int _rejected;

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
                _world = new float[capacity];

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
                AddMarker(byName, _worldRecorders, "Scatter.Render");
                AddMarker(byName, _worldRecorders, "AmbientLife.Tick");
                AddMarker(byName, _worldRecorders, "AmbientLife.Render");
                AddMarker(byName, _worldRecorders, "Atmosphere.Apply");
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
                // FrameTimingManager now and then returns a value far above the real frame (6.9 s inside a
                // 14 s run of 841 frames, T23): a timing the wall clock contradicts is replaced by the clock.
                float wallMs = Time.unscaledDeltaTime * 1000f;
                if (timing && _timing[0].cpuFrameTime > Math.Max(100.0, wallMs * 3.0))
                {
                    timing = false;
                    _rejected++;
                }
                int i = _count++;
                _cpu[i] = timing ? (float)_timing[0].cpuFrameTime : wallMs;
                _main[i] = timing && _timing[0].cpuMainThreadFrameTime > 0 ? (float)_timing[0].cpuMainThreadFrameTime : Ms(_mainThread);
                _render[i] = timing ? (float)_timing[0].cpuRenderThreadFrameTime : 0f;
                _gpu[i] = timing ? (float)_timing[0].gpuFrameTime : 0f;
                _sim[i] = SumMs(_simRecorders);
                _anim[i] = SumMs(_animRecorders);
                _ui[i] = SumMs(_uiRecorders);
                _world[i] = SumMs(_worldRecorders);
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

            public float LastCpuMs => _count > 0 ? _cpu[_count - 1] : 0f;

            /// <summary>Instances the scene entry's ScatterRenderer and AmbientLife drew this frame.</summary>
            public void SampleWorld(int scatter, int life)
            {
                _scatterSum += scatter;
                _lifeSum += life;
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
                    rejectedTimings = _rejected,
                    seconds = seconds,
                    cpuFrameMs = Stats(_cpu),
                    mainThreadMs = Stats(_main),
                    renderThreadMs = Stats(_render),
                    gpuMs = Stats(_gpu),
                    simulationMs = _simRecorders.Count > 0 ? Stats(_sim) : default,
                    animationMs = _animRecorders.Count > 0 ? Stats(_anim) : default,
                    uiMs = _uiRecorders.Count > 0 ? Stats(_ui) : default,
                    worldMs = _worldRecorders.Count > 0 ? Stats(_world) : default,
                    scatterInstances = (float)(_scatterSum / n),
                    lifeInstances = (float)(_lifeSum / n),
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
                foreach (var r in _worldRecorders) r.Dispose();
            }
        }
    }
}
