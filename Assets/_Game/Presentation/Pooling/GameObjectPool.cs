using System;
using System.Threading;
using PocketWilds.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Pool of prefab instances parented under one root. Instantiate happens during warmup or
    /// when demand exceeds the pool; steady state reuses objects with SetActive only.
    /// </summary>
    public sealed class GameObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _root;
        private readonly Action<T> _onCreated;
        private readonly ObjectPool<T> _pool;

        public GameObjectPool(T prefab, Transform root, int softCapacity, int hardCapacity, Action<T> onCreated = null)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _root = root;
            _onCreated = onCreated;
            _pool = new ObjectPool<T>(Create, DestroyItem, softCapacity, hardCapacity);
        }

        public int InstantiateCount { get; private set; }
        public int DestroyCount { get; private set; }
        public PoolStats Stats => _pool.Stats;
        public int SoftCapacity => _pool.SoftCapacity;
        public int HardCapacity => _pool.HardCapacity;

        public int Warmup(int count) => _pool.Warmup(count);

        /// <summary>Spreads instantiation over frames so preloading a chunk never spikes.</summary>
        public async Awaitable WarmupAsync(int count, int perFrame, CancellationToken cancellationToken)
        {
            perFrame = Mathf.Max(1, perFrame);
            int target = Mathf.Min(count, _pool.SoftCapacity);
            while (_pool.Stats.Inactive < target && _pool.AliveCount < _pool.HardCapacity)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int before = _pool.Stats.Inactive;
                _pool.Warmup(Mathf.Min(target, before + perFrame));
                if (_pool.Stats.Inactive == before)
                    break;
                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        public bool TryAcquire(out T item)
        {
            if (!_pool.TryAcquire(out item))
                return false;
            item.gameObject.SetActive(true);
            return true;
        }

        public void Release(T item)
        {
            // IPoolable.OnRelease runs inside the pool and resets presentation state.
            _pool.Release(item);
            item.gameObject.SetActive(false);
        }

        /// <summary>Call on calm frames to destroy objects above the soft capacity.</summary>
        public int TrimPending(int maxCount) => _pool.TrimPending(maxCount);

        public void Clear() => _pool.Clear();

        private T Create()
        {
            var item = Object.Instantiate(_prefab, _root);
            item.gameObject.SetActive(false);
            InstantiateCount++;
            _onCreated?.Invoke(item);
            return item;
        }

        private void DestroyItem(T item)
        {
            if (item != null)
                Object.Destroy(item.gameObject);
            DestroyCount++;
        }
    }
}
