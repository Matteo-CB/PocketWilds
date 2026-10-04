using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Pooled one-shot particle effects. Effects are warmed with their pool, played by handle and
    /// returned automatically after their duration by one Tick from the owner (no coroutine, no
    /// Destroy, no allocation in steady state). Low quality scales burst counts at creation time.
    /// </summary>
    public sealed class VfxPool : IDisposable
    {
        private struct Entry
        {
            public GameObjectPool<ParticleSystem> Pool;
            public float Duration;
        }

        private struct Playing
        {
            public ParticleSystem System;
            public int Handle;
            public float EndTime;
        }

        private readonly Transform _root;
        private readonly float _burstScale;
        private Entry[] _entries = new Entry[8];
        private int _entryCount;
        private readonly Playing[] _playing;
        private int _playingCount;

        /// <param name="burstScale">1 on Medium/High; Low reduces particle counts but keeps timing.</param>
        public VfxPool(Transform root, int maxPlaying, float burstScale = 1f)
        {
            _root = root;
            _burstScale = Mathf.Clamp(burstScale, 0.1f, 1f);
            _playing = new Playing[Mathf.Max(1, maxPlaying)];
        }

        public int PlayingCount => _playingCount;

        public int Register(ParticleSystem prefab, int softCapacity, int hardCapacity)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (_entryCount == _entries.Length)
                Array.Resize(ref _entries, _entries.Length * 2);
            var pool = new GameObjectPool<ParticleSystem>(prefab, _root, softCapacity, hardCapacity, ApplyQuality);
            _entries[_entryCount] = new Entry { Pool = pool, Duration = LongestLifetime(prefab) };
            return _entryCount++;
        }

        public void Warmup(int handle, int count) => _entries[handle].Pool.Warmup(count);

        public GameObjectPool<ParticleSystem> GetPool(int handle) => _entries[handle].Pool;

        /// <summary>Plays one effect. Returns false when the pool or the playing list is full (effect skipped, never a hitch).</summary>
        public bool Play(int handle, Vector3 position, Quaternion rotation, float time)
        {
            if (_playingCount == _playing.Length || !_entries[handle].Pool.TryAcquire(out var system))
                return false;
            system.transform.SetPositionAndRotation(position, rotation);
            system.Play(true);
            _playing[_playingCount++] = new Playing { System = system, Handle = handle, EndTime = time + _entries[handle].Duration };
            return true;
        }

        public void Tick(float time)
        {
            for (int i = _playingCount - 1; i >= 0; i--)
            {
                if (time < _playing[i].EndTime)
                    continue;
                Return(i);
            }
        }

        /// <summary>Stops and returns every playing effect (scene exit, skip).</summary>
        public void StopAll()
        {
            for (int i = _playingCount - 1; i >= 0; i--)
                Return(i);
        }

        public void Dispose()
        {
            StopAll();
            for (int i = 0; i < _entryCount; i++)
                _entries[i].Pool.Clear();
            _entryCount = 0;
        }

        private void Return(int index)
        {
            var playing = _playing[index];
            playing.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _entries[playing.Handle].Pool.Release(playing.System);
            _playing[index] = _playing[--_playingCount];
            _playing[_playingCount] = default;
        }

        private void ApplyQuality(ParticleSystem system)
        {
            if (_burstScale >= 0.999f)
                return;
            // Creation time only (warmup): scales burst counts on the root and children.
            foreach (var ps in system.GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = ps.emission;
                int count = emission.burstCount;
                for (int b = 0; b < count; b++)
                {
                    var burst = emission.GetBurst(b);
                    var c = burst.count;
                    c.constant = Mathf.Max(1f, Mathf.Round(c.constant * _burstScale));
                    burst.count = c;
                    emission.SetBurst(b, burst);
                }
            }
        }

        private static float LongestLifetime(ParticleSystem prefab)
        {
            float longest = 0f;
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                float life = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants ? main.startLifetime.constantMax : main.startLifetime.constant;
                longest = Mathf.Max(longest, main.startDelay.constant + main.duration + life);
            }
            return longest + 0.05f;
        }
    }
}
