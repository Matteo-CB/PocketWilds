#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Services.Save
{
    public enum SaveLoadOutcome
    {
        /// <summary>No save existed. A fresh profile was created.</summary>
        NewProfile,

        /// <summary>The newest file loaded normally.</summary>
        Loaded,

        /// <summary>The newest file was damaged; an older valid file was used.</summary>
        LoadedFromBackup,

        /// <summary>Files existed but none could be read. They were kept aside and a fresh profile was created.</summary>
        NewProfileAfterFailure,

        /// <summary>The save comes from a newer build. Nothing is written until the game is updated.</summary>
        NewerSchemaRefused,
    }

    public sealed class SaveLoadResult
    {
        public SaveLoadResult(SaveLoadOutcome outcome, PlayerState? state, SaveSlotKind? source, int fromSchemaVersion, int migrationsApplied, List<string> problems)
        {
            Outcome = outcome;
            State = state;
            Source = source;
            FromSchemaVersion = fromSchemaVersion;
            MigrationsApplied = migrationsApplied;
            Problems = problems;
        }

        public SaveLoadOutcome Outcome { get; }
        public PlayerState? State { get; }
        public SaveSlotKind? Source { get; }
        public int FromSchemaVersion { get; }
        public int MigrationsApplied { get; }
        public IReadOnlyList<string> Problems { get; }
    }

    public sealed class SaveServiceOptions
    {
        public double NormalDebounceSeconds { get; set; } = 5.0;
        public double PriorityDelaySeconds { get; set; } = 0.25;
        public double PeriodicSeconds { get; set; } = 60.0;
        public double RetryDelaySeconds { get; set; } = 10.0;
        public string AppVersion { get; set; } = string.Empty;
    }

    public struct SaveDebugSnapshot
    {
        public long Revision;
        public bool PendingNormal;
        public bool PendingPriority;
        public bool WriteInFlight;
        public bool ReadOnly;
        public int ConsecutiveFailures;
        public int LastBytes;
        public double LastSerializeMilliseconds;
        public double LastSaveSessionSeconds;
    }

    /// <summary>
    /// Owns the PlayerState lifecycle: load with recovery and migrations, debounced autosave,
    /// priority saves, and a synchronous flush for pause/quit. Serialization happens on the main
    /// thread into a reused buffer (measured by Save.Serialize); disk IO runs on a worker thread.
    /// </summary>
    public sealed class SaveService : ISaveScheduler, IDisposable
    {
        /// <summary>
        /// Raised on the main thread right before each serialization, after Meta.LastSaveUtc is set.
        /// Lets simulation write snapshots that must match the save time (offline rate, time mark).
        /// </summary>
        public event Action<PlayerState>? BeforeSerialize;

        private readonly SaveFileStore _store;
        private readonly ISaveSerializer _serializer;
        private readonly SaveMigrationPipeline _migrations;
        private readonly IGameClock _clock;
        private readonly SaveServiceOptions _options;
        private readonly ByteBufferWriter _writer = new ByteBufferWriter(64 * 1024);

        private readonly SaveWriteWorker _worker;
        private PlayerState? _state;
        private bool _normalPending;
        private bool _priorityPending;
        private double _normalRequestedAt;
        private double _priorityRequestedAt;
        private double _lastSaveAt;
        private double _retryAt = -1.0;
        private double _lastAccountedSession;
        private int _consecutiveFailures;
        private double _lastSerializeMs;

        public SaveService(SaveFileStore store, ISaveSerializer serializer, SaveMigrationPipeline migrations, IGameClock clock, SaveServiceOptions options)
        {
            _store = store;
            _serializer = serializer;
            _migrations = migrations;
            _clock = clock;
            _options = options;
            _worker = new SaveWriteWorker(store);
        }

        public PlayerState State => _state ?? throw new InvalidOperationException("Save not loaded yet.");
        public bool HasState => _state != null;
        public bool IsReadOnly { get; private set; }
        public bool IsWriteInFlight => _worker.Busy;
        public SaveLoadResult? LastLoad { get; private set; }
        public Exception? LastWriteError { get; private set; }

        /// <summary>Exposed for tests: completes when the current background write finishes (allocates; tests only).</summary>
        public Task PendingWrite => _worker.Busy ? Task.Run(() => _worker.Wait(Timeout.InfiniteTimeSpan)) : Task.CompletedTask;

        /// <summary>Stops the background writer thread. A pending write finishes first (up to 3 s).</summary>
        public void Dispose()
        {
            _worker.Wait(TimeSpan.FromSeconds(3));
            PollWrite();
            _worker.Dispose();
        }

        public Task<SaveLoadResult> LoadOrCreateAsync(CancellationToken cancellationToken)
        {
            return Task.Run(LoadOrCreate, cancellationToken);
        }

        /// <summary>Thread-agnostic load. Picks the highest valid revision among current, temp and previous.</summary>
        public SaveLoadResult LoadOrCreate()
        {
            var problems = new List<string>();
            var candidates = _store.ReadCandidates();
            var valid = new List<(SaveFileCandidate Candidate, SaveHeader Header)>(candidates.Count);

            // A damaged temp file is a normal interrupted write, not a lost save.
            bool firstChoiceFailed = false;
            foreach (var candidate in candidates)
            {
                if (SaveFileFormat.TryValidate(candidate.Data, candidate.Data.Length, out var header, out var error))
                {
                    valid.Add((candidate, header));
                    continue;
                }

                problems.Add($"{candidate.Kind}: {error}");
                if (candidate.Kind == SaveSlotKind.Temp)
                {
                    _store.DeleteTemp();
                }
                else
                {
                    _store.Quarantine(candidate.Path);
                    firstChoiceFailed = true;
                }
            }

            valid.Sort((a, b) =>
            {
                int byRevision = b.Header.Revision.CompareTo(a.Header.Revision);
                return byRevision != 0 ? byRevision : a.Candidate.Kind.CompareTo(b.Candidate.Kind);
            });

            PlayerState? state = null;
            SaveSlotKind? source = null;
            int fromSchema = SaveSchema.CurrentVersion;
            int migrations = 0;

            foreach (var entry in valid)
            {
                if (entry.Header.SchemaVersion > _migrations.CurrentSchemaVersion)
                {
                    IsReadOnly = true;
                    problems.Add($"{entry.Candidate.Kind}: schema {entry.Header.SchemaVersion} is newer than this build.");
                    LastLoad = new SaveLoadResult(SaveLoadOutcome.NewerSchemaRefused, null, entry.Candidate.Kind, entry.Header.SchemaVersion, 0, problems);
                    return LastLoad;
                }

                try
                {
                    var document = _serializer.ReadDocument(entry.Candidate.Data, entry.Candidate.Data.Length);
                    fromSchema = document.SchemaVersion;
                    migrations = _migrations.Migrate(document);
                    state = _serializer.Deserialize(document);
                    source = entry.Candidate.Kind;
                    break;
                }
                catch (Exception ex) when (ex is SaveCorruptException || ex is SaveMigrationException)
                {
                    problems.Add($"{entry.Candidate.Kind}: {ex.Message}");
                    firstChoiceFailed = true;
                    if (entry.Candidate.Kind != SaveSlotKind.Temp)
                        _store.Quarantine(entry.Candidate.Path);
                }
            }

            SaveLoadOutcome outcome;
            if (state != null)
            {
                outcome = firstChoiceFailed ? SaveLoadOutcome.LoadedFromBackup : SaveLoadOutcome.Loaded;
            }
            else
            {
                outcome = firstChoiceFailed ? SaveLoadOutcome.NewProfileAfterFailure : SaveLoadOutcome.NewProfile;
                state = PlayerState.CreateNew(_clock.UtcNow);
            }

            state.Meta.LastKnownSessionId = Guid.NewGuid();
            _state = state;
            _lastAccountedSession = _clock.SessionSeconds;
            _lastSaveAt = _clock.SessionSeconds;

            if (outcome != SaveLoadOutcome.Loaded || migrations > 0)
                RequestSave(SaveUrgency.Priority);

            LastLoad = new SaveLoadResult(outcome, state, source, fromSchema, migrations, problems);
            return LastLoad;
        }

        public void RequestSave(SaveUrgency urgency)
        {
            double now = _clock.SessionSeconds;
            if (urgency == SaveUrgency.Priority)
            {
                if (!_priorityPending)
                {
                    _priorityPending = true;
                    _priorityRequestedAt = now;
                }
            }
            else if (!_normalPending)
            {
                _normalPending = true;
                _normalRequestedAt = now;
            }
        }

        /// <summary>Called once per frame by the game loop. Cheap when nothing is due.</summary>
        public void Tick()
        {
            if (_state == null || IsReadOnly)
                return;

            PollWrite();
            if (_worker.Busy)
                return;

            double now = _clock.SessionSeconds;
            bool due = (_priorityPending && now >= _priorityRequestedAt + _options.PriorityDelaySeconds)
                       || (_normalPending && now >= _normalRequestedAt + _options.NormalDebounceSeconds)
                       || now >= _lastSaveAt + _options.PeriodicSeconds
                       || (_retryAt >= 0.0 && now >= _retryAt);
            if (due)
                BeginBackgroundWrite();
        }

        /// <summary>Synchronous save for app pause/quit. Returns false if nothing could be written.</summary>
        public bool FlushNow()
        {
            if (_state == null || IsReadOnly)
                return false;

            if (_worker.Busy)
            {
                _worker.Wait(TimeSpan.FromSeconds(3));
                PollWrite();
                if (_worker.Busy)
                    return false;
            }
            PollWrite();

            SerializeState();
            try
            {
                _store.WriteAtomic(_writer.Buffer, _writer.Length);
                OnWriteSucceeded();
                return true;
            }
            catch (Exception ex)
            {
                OnWriteFailed(ex);
                return false;
            }
        }

        public SaveDebugSnapshot GetDebugSnapshot()
        {
            return new SaveDebugSnapshot
            {
                Revision = _state?.Meta.Revision ?? 0,
                PendingNormal = _normalPending,
                PendingPriority = _priorityPending,
                WriteInFlight = IsWriteInFlight,
                ReadOnly = IsReadOnly,
                ConsecutiveFailures = _consecutiveFailures,
                LastBytes = _writer.Length,
                LastSerializeMilliseconds = _lastSerializeMs,
                LastSaveSessionSeconds = _lastSaveAt,
            };
        }

        private void BeginBackgroundWrite()
        {
            SerializeState();
            _worker.Begin(_writer.Buffer, _writer.Length);
        }

        private void SerializeState()
        {
            var state = State;
            double now = _clock.SessionSeconds;
            state.Meta.Revision++;
            state.Meta.LastSaveUtc = _clock.UtcNow;
            state.Meta.AppVersion = _options.AppVersion;
            state.Meta.TotalPlaySeconds += Math.Max(0.0, now - _lastAccountedSession);
            _lastAccountedSession = now;
            BeforeSerialize?.Invoke(state);

            long start = Stopwatch.GetTimestamp();
            using (GameProfilerMarkers.SaveSerialize.Auto())
            {
                _serializer.Serialize(state, _writer);
            }
            _lastSerializeMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

            _normalPending = false;
            _priorityPending = false;
            _retryAt = -1.0;
            _lastSaveAt = now;
        }

        private void PollWrite()
        {
            if (!_worker.TryTakeResult(out var error))
                return;
            if (error != null)
                OnWriteFailed(error);
            else
                OnWriteSucceeded();
        }

        private void OnWriteSucceeded()
        {
            _consecutiveFailures = 0;
            LastWriteError = null;
        }

        private void OnWriteFailed(Exception ex)
        {
            _consecutiveFailures++;
            LastWriteError = ex;
            _retryAt = _clock.SessionSeconds + _options.RetryDelaySeconds;
            GameLog.Error(LogCategory.Save, "Save write failed (" + _consecutiveFailures + "): " + ex.Message);
        }
    }
}
