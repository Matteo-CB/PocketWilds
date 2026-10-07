using System;
using System.Diagnostics;
using System.Threading;
using PocketWilds.Core;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Runs the warmup behind the loading screen. Graphics states are built in small batches by worker jobs that the
    /// main thread only polls, so the loading screen keeps its pace even where one state takes several milliseconds.
    /// </summary>
    public static class ShaderWarmup
    {
        /// <summary>Command line flag of a player run that records every graphics state it draws: "-pw-trace-graphics &lt;file&gt;".</summary>
        public const string TraceFlag = "-pw-trace-graphics";

        /// <summary>Command line flag that skips the warmup (to measure what it saves).</summary>
        public const string SkipFlag = "-pw-no-warmup";

        /// <summary>Graphics states (or shader variants, for the fallback) warmed by the last run.</summary>
        public static int LastWarmed { get; private set; }

        public static double LastMilliseconds { get; private set; }

        /// <summary>Longest frame (ms) while the last warmup ran: the loading screen must stay smooth.</summary>
        public static float LastLongestFrameMs { get; private set; }

        /// <summary>"states" (traced collection), "variants" (fallback), "none" or "skipped".</summary>
        public static string LastSource { get; private set; } = "none";

        public static Awaitable RunAsync(ShaderWarmupSet set, CancellationToken cancellationToken, int perBatch = 16) =>
            RunAsync(new[] { set }, cancellationToken, perBatch);

        /// <summary>
        /// T34C: several sets, one per place the shaders live (built into the player, Addressables groups Bootstrap and
        /// CommonAnimals): each set warms its own copy of its shaders. Totals are summed.
        /// </summary>
        public static async Awaitable RunAsync(System.Collections.Generic.IReadOnlyList<ShaderWarmupSet> sets, CancellationToken cancellationToken,
            int perBatch = 16)
        {
            LastWarmed = 0;
            LastLongestFrameMs = 0f;
            LastSource = "none";
            if (HasArgument(SkipFlag))
            {
                LastSource = "skipped";
                return;
            }
            if (sets == null)
                return;
            var watch = Stopwatch.StartNew();
            bool anyTraced = false, anyVariants = false;
            foreach (var set in sets)
            {
                if (set == null)
                    continue;
                bool traced = false;
                foreach (var collection in set.collections)
                {
                    if (collection == null || collection.graphicsDeviceType != SystemInfo.graphicsDeviceType || collection.runtimePlatform != Application.platform)
                        continue;
                    traced = true;
                    // Bounded: a state the device cannot build must never keep the loading screen up.
                    int batches = 0, maxBatches = MaxSteps(collection.totalGraphicsStateCount, perBatch);
                    while (!collection.isWarmedUp && batches++ < maxBatches)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var batch = collection.WarmUpProgressively(perBatch);
                        do
                        {
                            await NextFrame(cancellationToken);
                        }
                        while (!batch.IsCompleted);
                        batch.Complete();
                    }
                    if (!collection.isWarmedUp)
                        GameLog.Warning(LogCategory.Boot, $"Graphics state warmup stopped after {maxBatches} batches ({collection.name}).");
                    LastWarmed += collection.totalGraphicsStateCount;
                }
                if (!traced && set.variants != null)
                {
                    // WarmUpProgressively returns true once every variant is done; isWarmedUp only reflects a full
                    // WarmUp() call and stays false on devices (Android, T66 measure): looping on it never ended there.
                    bool done = set.variants.isWarmedUp;
                    int steps = 0, maxSteps = MaxSteps(set.variants.variantCount, perBatch);
                    while (!done && steps++ < maxSteps)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        done = set.variants.WarmUpProgressively(perBatch);
                        await NextFrame(cancellationToken);
                    }
                    if (!done)
                        GameLog.Warning(LogCategory.Boot, $"Shader variant warmup stopped after {maxSteps} steps ({set.variants.name}).");
                    LastWarmed += set.variants.variantCount;
                    anyVariants = true;
                }
                anyTraced |= traced;
            }
            LastSource = anyTraced ? (anyVariants ? "states+variants" : "states") : anyVariants ? "variants" : "none";
            LastMilliseconds = watch.Elapsed.TotalMilliseconds;
            GameLog.Info(LogCategory.Boot, $"Shader warmup: {LastWarmed} {LastSource} in {LastMilliseconds:F0} ms, longest frame {LastLongestFrameMs:F1} ms");
        }

        /// <summary>Enough steps for every item in batches of perBatch, plus a margin; never unbounded.</summary>
        public static int MaxSteps(int itemCount, int perBatch) => Math.Max(1, itemCount) / Math.Max(1, perBatch) + 60;

        private static async Awaitable NextFrame(CancellationToken cancellationToken)
        {
            await Awaitable.NextFrameAsync(cancellationToken);
            float ms = Time.unscaledDeltaTime * 1000f;
            if (ms > LastLongestFrameMs)
                LastLongestFrameMs = ms;
        }

        private static GraphicsStateCollection _trace;
        private static string _tracePath;
        private static int _traceIndex;

        /// <summary>
        /// A player run with <see cref="TraceFlag"/> records every graphics state it draws. One trace per boot, ended
        /// before the boot's scene is unloaded (a trace kept across unloads crashes the player); each one is saved as
        /// &lt;file&gt;_NN and the editor merges them (Import Graphics State Trace).
        /// </summary>
        public static void BeginBootTrace()
        {
            if (_trace != null)
                return;
            string path = Argument(TraceFlag);
            if (string.IsNullOrEmpty(path))
                return;
            _trace = new GraphicsStateCollection();
            if (!_trace.BeginTrace())
            {
                GameLog.Warning(LogCategory.Boot, "Graphics state trace could not start.");
                _trace = null;
                return;
            }
            string folder = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string extension = System.IO.Path.GetExtension(path);
            _tracePath = System.IO.Path.Combine(folder, $"{name}_{_traceIndex++:00}{extension}");
            GameLog.Info(LogCategory.Boot, "Tracing graphics states to " + _tracePath);
        }

        /// <summary>Ends and saves the boot's trace (the bootstrap goes away, before its scene unloads).</summary>
        public static void EndBootTrace()
        {
            if (_trace == null)
                return;
            _trace.EndTrace();
            bool saved = _trace.SaveToFile(_tracePath);
            GameLog.Info(LogCategory.Boot, $"Graphics state trace: {_trace.variantCount} variants, {_trace.totalGraphicsStateCount} states, saved {saved}");
            Object.Destroy(_trace);
            _trace = null;
        }

        private static bool HasArgument(string flag)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg == flag)
                    return true;
            return false;
        }

        private static string Argument(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag)
                    return args[i + 1];
            return null;
        }
    }
}
