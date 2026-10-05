#nullable enable
using System;
using System.IO;
using System.Threading;

namespace PocketWilds.Services.Analytics
{
    /// <summary>Where encoded batches go. The outbox in the game, a memory sink in tests.</summary>
    public interface IAnalyticsSink : IDisposable
    {
        /// <summary>Copies the batch: the caller's buffer is free again when this returns.</summary>
        void Write(byte[] data, int length);

        /// <summary>Only with consent: batches on disk may be sent to the backend.</summary>
        bool SendAllowed { get; set; }

        /// <summary>Consent refused: drops every batch not sent yet.</summary>
        void Purge();

        /// <summary>Waits for queued batches to reach the disk (app going to the background).</summary>
        bool WaitIdle(TimeSpan timeout);
    }

    /// <summary>
    /// Batches on disk, bounded, written and sent by one background thread. The main thread only copies a batch into
    /// one of four reused slots (no file access, no allocation once the slots have grown). Files are named by time
    /// so the oldest go first; above the limit the oldest are deleted. Sending happens only with consent and a
    /// configured transport; a failed send is retried a minute later, a sent batch is deleted.
    /// </summary>
    public sealed class AnalyticsOutbox : IAnalyticsSink
    {
        public const int DefaultMaxFiles = 64;
        private const int Slots = 4;
        private const int RetryMilliseconds = 60_000;
        private const string Extension = ".json";

        private readonly string _directory;
        private readonly IAnalyticsTransport _transport;
        private readonly int _maxFiles;
        private readonly object _gate = new object();
        private readonly byte[]?[] _slots = new byte[Slots][];
        private readonly int[] _lengths = new int[Slots];

        /// <summary>0 free, -1 being written, otherwise the queue order.</summary>
        private readonly long[] _order = new long[Slots];

        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly ManualResetEventSlim _idle = new ManualResetEventSlim(true);
        private Thread? _thread;
        private long _sequence;
        private volatile bool _sendAllowed;
        private volatile bool _purge;
        private volatile bool _stopping;
        private int _droppedBatches;
        private int _filesWritten;
        private int _filesSent;
        private int _filesDeleted;

        public AnalyticsOutbox(string directory, IAnalyticsTransport transport, int maxFiles = DefaultMaxFiles)
        {
            _directory = directory;
            _transport = transport;
            _maxFiles = Math.Max(1, maxFiles);
        }

        public string Directory => _directory;
        public string TransportName => _transport.Name;

        /// <summary>Batches lost because four were already waiting for the disk.</summary>
        public int DroppedBatches => Volatile.Read(ref _droppedBatches);

        public int FilesWritten => Volatile.Read(ref _filesWritten);
        public int FilesSent => Volatile.Read(ref _filesSent);

        /// <summary>Oldest files removed to stay under the limit.</summary>
        public int FilesDeleted => Volatile.Read(ref _filesDeleted);

        public string? LastError { get; private set; }

        public bool SendAllowed
        {
            get => _sendAllowed;
            set
            {
                _sendAllowed = value;
                if (value && _thread != null)
                    _wake.Set();
            }
        }

        public void Write(byte[] data, int length)
        {
            lock (_gate)
            {
                int free = -1;
                for (int i = 0; i < Slots; i++)
                {
                    if (_order[i] == 0)
                    {
                        free = i;
                        break;
                    }
                }
                if (free < 0)
                {
                    _droppedBatches++;
                    return;
                }
                var slot = _slots[free];
                if (slot == null || slot.Length < length)
                    _slots[free] = slot = new byte[Math.Max(length, 8 * 1024)];
                Buffer.BlockCopy(data, 0, slot, 0, length);
                _lengths[free] = length;
                _order[free] = ++_sequence;
                _idle.Reset();
            }
            EnsureThread();
            _wake.Set();
        }

        public void Purge()
        {
            lock (_gate)
            {
                for (int i = 0; i < Slots; i++)
                {
                    if (_order[i] > 0)
                        _order[i] = 0;
                }
                _purge = true;
                _idle.Reset();
            }
            EnsureThread();
            _wake.Set();
        }

        public bool WaitIdle(TimeSpan timeout) => _idle.Wait(timeout);

        /// <summary>Files waiting on disk (tests and the debug overlay; reads the folder).</summary>
        public int CountFiles() => System.IO.Directory.Exists(_directory) ? System.IO.Directory.GetFiles(_directory, "*" + Extension).Length : 0;

        public void Dispose()
        {
            _stopping = true;
            _wake.Set();
            _thread?.Join(TimeSpan.FromSeconds(2));
            _wake.Dispose();
            _idle.Dispose();
        }

        private void EnsureThread()
        {
            if (_thread != null)
                return;
            _thread = new Thread(Run) { IsBackground = true, Name = "PocketWilds.AnalyticsOutbox" };
            _thread.Start();
        }

        private void Run()
        {
            while (!_stopping)
            {
                _wake.WaitOne(RetryMilliseconds);
                if (_stopping)
                    return;
                try
                {
                    if (_purge)
                    {
                        _purge = false;
                        DeleteAll();
                    }
                    WriteQueued();
                    if (_sendAllowed && _transport.IsAvailable)
                        SendFiles();
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                }
                finally
                {
                    lock (_gate)
                    {
                        if (!_purge && !HasQueued())
                            _idle.Set();
                    }
                }
            }
        }

        private bool HasQueued()
        {
            for (int i = 0; i < Slots; i++)
            {
                if (_order[i] != 0)
                    return true;
            }
            return false;
        }

        private void WriteQueued()
        {
            while (true)
            {
                int next = -1;
                long sequence;
                lock (_gate)
                {
                    for (int i = 0; i < Slots; i++)
                    {
                        if (_order[i] > 0 && (next < 0 || _order[i] < _order[next]))
                            next = i;
                    }
                    if (next < 0)
                        return;
                    sequence = _order[next];
                    _order[next] = -1;
                }
                try
                {
                    WriteFile(_slots[next]!, _lengths[next], sequence);
                }
                finally
                {
                    lock (_gate)
                        _order[next] = 0;
                }
            }
        }

        private void WriteFile(byte[] data, int length, long sequence)
        {
            System.IO.Directory.CreateDirectory(_directory);
            long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string name = unixMs.ToString("D13") + "_" + (sequence % 1_000_000).ToString("D6");
            string path = Path.Combine(_directory, name + Extension);
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
            {
                stream.Write(data, 0, length);
                stream.Flush(true);
            }
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
            Interlocked.Increment(ref _filesWritten);
            TrimToLimit();
        }

        private void TrimToLimit()
        {
            var files = System.IO.Directory.GetFiles(_directory, "*" + Extension);
            if (files.Length <= _maxFiles)
                return;
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length - _maxFiles; i++)
            {
                File.Delete(files[i]);
                Interlocked.Increment(ref _filesDeleted);
            }
        }

        private void SendFiles()
        {
            if (!System.IO.Directory.Exists(_directory))
                return;
            var files = System.IO.Directory.GetFiles(_directory, "*" + Extension);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                if (_stopping || !_sendAllowed || _purge)
                    return;
                var bytes = File.ReadAllBytes(file);
                if (!_transport.TrySend(bytes, bytes.Length))
                    return;
                File.Delete(file);
                Interlocked.Increment(ref _filesSent);
            }
        }

        private void DeleteAll()
        {
            if (!System.IO.Directory.Exists(_directory))
                return;
            foreach (var file in System.IO.Directory.GetFiles(_directory))
                File.Delete(file);
        }
    }
}
