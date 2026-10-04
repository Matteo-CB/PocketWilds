using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// One GameObjectPool per species visual (prefab). Callers resolve a visual key to an int handle
    /// once, then acquire by handle. Acquire configures the whole view; release resets it.
    /// </summary>
    public sealed class AnimalViewPool : IDisposable
    {
        private readonly Transform _root;
        private readonly List<GameObjectPool<AnimalView>> _pools = new List<GameObjectPool<AnimalView>>(8);
        private readonly Dictionary<string, int> _handleByKey = new Dictionary<string, int>(8);

        public AnimalViewPool(Transform root)
        {
            _root = root;
        }

        public int PoolCount => _pools.Count;

        /// <summary>Registers a prefab under a visual key and returns its handle. Idempotent per key.</summary>
        public int Register(string visualKey, AnimalView prefab, int softCapacity, int hardCapacity)
        {
            if (_handleByKey.TryGetValue(visualKey, out int existing))
                return existing;

            int handle = _pools.Count;
            GameObjectPool<AnimalView> pool = null;
            pool = new GameObjectPool<AnimalView>(prefab, _root, softCapacity, hardCapacity, view =>
            {
                view.SourcePool = pool;
                view.PoolHandle = handle;
            });
            _pools.Add(pool);
            _handleByKey.Add(visualKey, handle);
            return handle;
        }

        public bool TryGetHandle(string visualKey, out int handle) => _handleByKey.TryGetValue(visualKey, out handle);

        public GameObjectPool<AnimalView> GetPool(int handle) => _pools[handle];

        public int Warmup(int handle, int count) => _pools[handle].Warmup(count);

        public Awaitable WarmupAsync(int handle, int count, int perFrame, CancellationToken cancellationToken) =>
            _pools[handle].WarmupAsync(count, perFrame, cancellationToken);

        /// <summary>Returns false at the hard cap: the caller simply shows fewer animals.</summary>
        public bool TryAcquire(int handle, in AnimalVisualSnapshot snapshot, out AnimalView view)
        {
            if (!_pools[handle].TryAcquire(out view))
                return false;
            view.Configure(snapshot);
            return true;
        }

        public void Release(AnimalView view)
        {
            if (view == null)
                return;
            if (view.SourcePool == null)
                throw new InvalidOperationException("AnimalView was not created by an AnimalViewPool.");
            view.SourcePool.Release(view);
        }

        public int TrimPending(int maxPerPool)
        {
            int destroyed = 0;
            for (int i = 0; i < _pools.Count; i++)
                destroyed += _pools[i].TrimPending(maxPerPool);
            return destroyed;
        }

        public void Dispose()
        {
            for (int i = 0; i < _pools.Count; i++)
                _pools[i].Clear();
            _pools.Clear();
            _handleByKey.Clear();
        }
    }
}
