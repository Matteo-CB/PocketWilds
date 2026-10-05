#nullable enable
using System;
using System.Threading;

namespace PocketWilds.Services.Save
{
    /// <summary>
    /// One long-lived background thread that writes save files. Replaces a Task per save: starting a write
    /// allocates nothing (T22: 0 B per frame in gameplay, autosaves included). The thread is created on the
    /// first background write and stops with Dispose; it is a background thread, never keeping the app alive.
    /// </summary>
    internal sealed class SaveWriteWorker : IDisposable
    {
        private const int Idle = 0;
        private const int Writing = 1;
        private const int Succeeded = 2;
        private const int Failed = 3;

        private readonly SaveFileStore _store;
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly ManualResetEventSlim _done = new ManualResetEventSlim(true);
        private Thread? _thread;
        private byte[]? _buffer;
        private int _length;
        private int _status;
        private Exception? _error;
        private volatile bool _stopping;

        public SaveWriteWorker(SaveFileStore store)
        {
            _store = store;
        }

        public bool Busy => Volatile.Read(ref _status) == Writing;
        public bool HasResult => Volatile.Read(ref _status) >= Succeeded;

        /// <summary>The buffer must stay untouched until the write completes (SaveService serializes only when idle).</summary>
        public void Begin(byte[] buffer, int length)
        {
            if (Busy)
                throw new InvalidOperationException("A save write is already in flight.");
            EnsureThread();
            _buffer = buffer;
            _length = length;
            _error = null;
            _done.Reset();
            Volatile.Write(ref _status, Writing);
            _wake.Set();
        }

        /// <summary>Takes the outcome of a finished write. False while writing or when nothing was started.</summary>
        public bool TryTakeResult(out Exception? error)
        {
            error = null;
            int status = Volatile.Read(ref _status);
            if (status < Succeeded)
                return false;
            error = status == Failed ? _error : null;
            Volatile.Write(ref _status, Idle);
            return true;
        }

        public bool Wait(TimeSpan timeout) => _done.Wait(timeout);

        public void Dispose()
        {
            _stopping = true;
            _wake.Set();
            _thread?.Join(TimeSpan.FromSeconds(3));
            _wake.Dispose();
            _done.Dispose();
        }

        private void EnsureThread()
        {
            if (_thread != null)
                return;
            _thread = new Thread(Run) { IsBackground = true, Name = "PocketWilds.SaveWriter" };
            _thread.Start();
        }

        private void Run()
        {
            while (true)
            {
                _wake.WaitOne();
                if (_stopping)
                    return;
                if (Volatile.Read(ref _status) != Writing)
                    continue;
                try
                {
                    _store.WriteAtomic(_buffer!, _length);
                    Volatile.Write(ref _status, Succeeded);
                }
                catch (Exception ex)
                {
                    _error = ex;
                    Volatile.Write(ref _status, Failed);
                }
                finally
                {
                    _done.Set();
                }
            }
        }
    }
}
