#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PocketWilds.Core
{
    /// <summary>
    /// Explicit, ordered boot steps. Blocking steps run one after another and gate entry into
    /// the refuge. Background steps (cloud, remote config, analytics) start afterwards and
    /// can never block or fail the boot.
    /// </summary>
    public sealed class BootSequence
    {
        private readonly struct Step
        {
            public readonly string Name;
            public readonly Func<CancellationToken, Task> Run;

            public Step(string name, Func<CancellationToken, Task> run)
            {
                Name = name;
                Run = run;
            }
        }

        private readonly List<Step> _blocking = new List<Step>(16);
        private readonly List<Step> _background = new List<Step>(8);
        private readonly List<BootStepReport> _reports = new List<BootStepReport>(24);
        private bool _started;

        public IReadOnlyList<BootStepReport> Reports => _reports;

        /// <summary>Completes when every background step has finished, failed or been cancelled.</summary>
        public Task BackgroundCompletion { get; private set; } = Task.CompletedTask;

        public void AddBlocking(string name, Func<CancellationToken, Task> run)
        {
            EnsureNotStarted();
            _blocking.Add(new Step(name, run));
        }

        public void AddBlocking(string name, Action run)
        {
            EnsureNotStarted();
            _blocking.Add(new Step(name, _ =>
            {
                run();
                return Task.CompletedTask;
            }));
        }

        public void AddBackground(string name, Func<CancellationToken, Task> run)
        {
            EnsureNotStarted();
            _background.Add(new Step(name, run));
        }

        /// <summary>
        /// Runs blocking steps in order. A failing blocking step stops the boot and rethrows.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            EnsureNotStarted();
            _started = true;

            for (int i = 0; i < _blocking.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = _blocking[i];
                var watch = Stopwatch.StartNew();
                try
                {
                    await step.Run(cancellationToken);
                    _reports.Add(new BootStepReport(step.Name, BootStepOutcome.Succeeded, watch.Elapsed.TotalMilliseconds, null));
                }
                catch (Exception ex)
                {
                    _reports.Add(new BootStepReport(step.Name, BootStepOutcome.Failed, watch.Elapsed.TotalMilliseconds, ex));
                    throw;
                }
            }

            if (_background.Count == 0)
                return;

            var tasks = new Task[_background.Count];
            for (int i = 0; i < _background.Count; i++)
                tasks[i] = RunBackground(_background[i], cancellationToken);
            BackgroundCompletion = Task.WhenAll(tasks);
        }

        private async Task RunBackground(Step step, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                await step.Run(cancellationToken);
                AddReport(new BootStepReport(step.Name, BootStepOutcome.Succeeded, watch.Elapsed.TotalMilliseconds, null));
            }
            catch (OperationCanceledException)
            {
                AddReport(new BootStepReport(step.Name, BootStepOutcome.Cancelled, watch.Elapsed.TotalMilliseconds, null));
            }
            catch (Exception ex)
            {
                // Background services are optional. The refuge keeps running without them.
                AddReport(new BootStepReport(step.Name, BootStepOutcome.Failed, watch.Elapsed.TotalMilliseconds, ex));
                GameLog.Warning(LogCategory.Boot, $"Background step '{step.Name}' failed: {ex.Message}");
            }
        }

        private void AddReport(BootStepReport report)
        {
            lock (_reports)
            {
                _reports.Add(report);
            }
        }

        private void EnsureNotStarted()
        {
            if (_started)
                throw new InvalidOperationException("Boot sequence already started.");
        }
    }

    public enum BootStepOutcome
    {
        Succeeded,
        Failed,
        Cancelled,
    }

    public readonly struct BootStepReport
    {
        public readonly string Name;
        public readonly BootStepOutcome Outcome;
        public readonly double Milliseconds;
        public readonly Exception? Error;

        public BootStepReport(string name, BootStepOutcome outcome, double milliseconds, Exception? error)
        {
            Name = name;
            Outcome = outcome;
            Milliseconds = milliseconds;
            Error = error;
        }
    }
}
