#nullable enable
using System;

namespace PocketWilds.Core
{
    /// <summary>Optional callbacks for pooled objects.</summary>
    public interface IPoolable
    {
        void OnAcquire();
        void OnRelease();
    }

    public struct PoolStats
    {
        public int Created;
        public int Destroyed;
        public int Active;
        public int Inactive;
        public int PeakActive;
        public int Misses;
        public int PendingDestroy;
    }

    /// <summary>
    /// Bounded pool. The factory runs only during warmup or when demand exceeds what is pooled.
    /// Above SoftCapacity, released objects are queued and destroyed a few per frame through
    /// <see cref="TrimPending"/>, called on calm frames. At HardCapacity, acquire fails cleanly.
    /// All storage is preallocated to HardCapacity: steady-state acquire/release does not allocate.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly Action<T>? _destroy;
        private readonly T[] _inactive;
        private readonly T[] _pendingDestroy;
        private int _inactiveCount;
        private int _pendingCount;
        private PoolStats _stats;

        public ObjectPool(Func<T> factory, Action<T>? destroy, int softCapacity, int hardCapacity)
        {
            if (softCapacity < 0 || hardCapacity < 1 || softCapacity > hardCapacity)
                throw new ArgumentOutOfRangeException(nameof(softCapacity), "Need 0 <= soft <= hard and hard >= 1.");

            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _destroy = destroy;
            SoftCapacity = softCapacity;
            HardCapacity = hardCapacity;
            _inactive = new T[hardCapacity];
            _pendingDestroy = new T[hardCapacity];
        }

        public int SoftCapacity { get; }
        public int HardCapacity { get; }
        public PoolStats Stats => _stats;

        /// <summary>Objects alive (active, pooled or pending destroy). Never exceeds HardCapacity.</summary>
        public int AliveCount => _stats.Active + _inactiveCount + _pendingCount;

        /// <summary>Creates objects until <paramref name="count"/> are pooled, bounded by HardCapacity.</summary>
        public int Warmup(int count)
        {
            int created = 0;
            int target = Math.Min(count, SoftCapacity);
            while (_inactiveCount < target && AliveCount < HardCapacity)
            {
                var item = _factory();
                _stats.Created++;
                if (item is IPoolable poolable)
                    poolable.OnRelease();
                _inactive[_inactiveCount++] = item;
                created++;
            }
            _stats.Inactive = _inactiveCount;
            return created;
        }

        /// <summary>Returns false when the hard cap is reached. The caller decides the fallback (skip, reuse oldest...).</summary>
        public bool TryAcquire(out T item)
        {
            using (GameProfilerMarkers.PoolAcquire.Auto())
            {
                if (_inactiveCount > 0)
                {
                    item = _inactive[--_inactiveCount];
                    _inactive[_inactiveCount] = null!;
                }
                else if (_pendingCount > 0)
                {
                    // Revive an object waiting for destruction instead of creating a new one.
                    item = _pendingDestroy[--_pendingCount];
                    _pendingDestroy[_pendingCount] = null!;
                }
                else if (AliveCount < HardCapacity)
                {
                    item = _factory();
                    _stats.Created++;
                    _stats.Misses++;
                }
                else
                {
                    item = null!;
                    _stats.Misses++;
                    return false;
                }

                _stats.Active++;
                if (_stats.Active > _stats.PeakActive)
                    _stats.PeakActive = _stats.Active;
                _stats.Inactive = _inactiveCount;
                _stats.PendingDestroy = _pendingCount;

                if (item is IPoolable poolable)
                    poolable.OnAcquire();
                return true;
            }
        }

        public void Release(T item)
        {
            using (GameProfilerMarkers.PoolRelease.Auto())
            {
                if (item == null)
                    throw new ArgumentNullException(nameof(item));
                if (_stats.Active <= 0)
                    throw new InvalidOperationException("Release called more times than Acquire.");

                if (item is IPoolable poolable)
                    poolable.OnRelease();

                _stats.Active--;
                if (_inactiveCount < SoftCapacity)
                    _inactive[_inactiveCount++] = item;
                else
                    _pendingDestroy[_pendingCount++] = item;

                _stats.Inactive = _inactiveCount;
                _stats.PendingDestroy = _pendingCount;
            }
        }

        /// <summary>Destroys up to <paramref name="maxCount"/> surplus objects. Call on calm frames only.</summary>
        public int TrimPending(int maxCount)
        {
            int destroyed = 0;
            while (_pendingCount > 0 && destroyed < maxCount)
            {
                var item = _pendingDestroy[--_pendingCount];
                _pendingDestroy[_pendingCount] = null!;
                _destroy?.Invoke(item);
                _stats.Destroyed++;
                destroyed++;
            }
            _stats.PendingDestroy = _pendingCount;
            return destroyed;
        }

        /// <summary>Destroys every pooled and pending object. Active objects stay owned by their users.</summary>
        public void Clear()
        {
            TrimPending(int.MaxValue);
            while (_inactiveCount > 0)
            {
                var item = _inactive[--_inactiveCount];
                _inactive[_inactiveCount] = null!;
                _destroy?.Invoke(item);
                _stats.Destroyed++;
            }
            _stats.Inactive = 0;
        }
    }
}
