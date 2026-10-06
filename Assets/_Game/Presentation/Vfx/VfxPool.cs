using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Pooled one-shot particle effects. Effects are warmed with their pool, played by handle and
    /// returned automatically after their duration by one Tick from the owner (no coroutine, no
    /// Destroy, no allocation in steady state). Burst counts follow the quality scale: set at creation,
    /// and rewritten on every pooled instance when the tier changes (T34A), never per play.
    /// </summary>
    public sealed class VfxPool : IDisposable
    {
        private sealed class Entry
        {
            public GameObjectPool<ParticleSystem> Pool;
            public float Duration;
            // Burst counts of the prefab, child systems in hierarchy order, bursts in order.
            public float[] BaseBursts;
            // Every instance ever created (warmup or growth), with its systems: rescaled on a tier change.
            public readonly List<ParticleSystem[]> Instances = new List<ParticleSystem[]>(4);
        }

        private struct Playing
        {
            public ParticleSystem System;
            public int Handle;
            public float EndTime;
        }

        private readonly Transform _root;
        private float _burstScale;
        private Entry[] _entries = new Entry[8];
        private int _entryCount;
        private readonly Playing[] _playing;
        private int _playingCount;

        /// <param name="burstScale">1 on Medium; Low reduces particle counts, High raises them a little. Timing never changes.</param>
        public VfxPool(Transform root, int maxPlaying, float burstScale = 1f)
        {
            _root = root;
            _burstScale = ClampScale(burstScale);
            _playing = new Playing[Mathf.Max(1, maxPlaying)];
        }

        public int PlayingCount => _playingCount;
        public float BurstScale => _burstScale;

        public int Register(ParticleSystem prefab, int softCapacity, int hardCapacity)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (_entryCount == _entries.Length)
                Array.Resize(ref _entries, _entries.Length * 2);
            var entry = new Entry { Duration = LongestLifetime(prefab), BaseBursts = ReadBursts(prefab) };
            entry.Pool = new GameObjectPool<ParticleSystem>(prefab, _root, softCapacity, hardCapacity, system => OnCreated(entry, system));
            _entries[_entryCount] = entry;
            return _entryCount++;
        }

        public void Warmup(int handle, int count) => _entries[handle].Pool.Warmup(count);

        public GameObjectPool<ParticleSystem> GetPool(int handle) => _entries[handle].Pool;

        /// <summary>Seconds an effect stays out of its pool once played.</summary>
        public float DurationOf(int handle) => _entries[handle].Duration;

        /// <summary>Plays one effect. Returns false when the pool or the playing list is full (effect skipped, never a hitch).</summary>
        public bool Play(int handle, Vector3 position, Quaternion rotation, float time) => Play(handle, position, rotation, time, out _);

        public bool Play(int handle, Vector3 position, Quaternion rotation, float time, out ParticleSystem system)
        {
            system = null;
            if (_playingCount == _playing.Length || !_entries[handle].Pool.TryAcquire(out system))
                return false;
            system.transform.SetPositionAndRotation(position, rotation);
            system.Play(true);
            _playing[_playingCount++] = new Playing { System = system, Handle = handle, EndTime = time + _entries[handle].Duration };
            return true;
        }

        /// <summary>Quality tier change: every pooled instance gets the new burst counts (a few struct writes per system).</summary>
        public void SetBurstScale(float scale)
        {
            scale = ClampScale(scale);
            if (Mathf.Approximately(scale, _burstScale))
                return;
            _burstScale = scale;
            for (int e = 0; e < _entryCount; e++)
            {
                var entry = _entries[e];
                for (int i = 0; i < entry.Instances.Count; i++)
                    ApplyScale(entry, entry.Instances[i]);
            }
        }

        /// <summary>Particles one play of this effect emits at the current scale (tests, Gate 10).</summary>
        public int ParticleCount(int handle)
        {
            var bursts = _entries[handle].BaseBursts;
            int total = 0;
            for (int i = 0; i < bursts.Length; i++)
                total += Scaled(bursts[i]);
            return total;
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
            {
                _entries[i].Pool.Clear();
                _entries[i].Instances.Clear();
            }
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

        private void OnCreated(Entry entry, ParticleSystem system)
        {
            // Creation time only (warmup or pool growth): the systems are listed once.
            var systems = system.GetComponentsInChildren<ParticleSystem>(true);
            entry.Instances.Add(systems);
            // The first managed access to a component's transform creates its wrapper (a small allocation): done here,
            // at warmup, so the first play in game allocates nothing (measured in T34A).
            _ = system.transform;
            if (!Mathf.Approximately(_burstScale, 1f))
                ApplyScale(entry, systems);
        }

        private void ApplyScale(Entry entry, ParticleSystem[] systems)
        {
            int k = 0;
            for (int s = 0; s < systems.Length; s++)
            {
                var emission = systems[s].emission;
                int count = emission.burstCount;
                for (int b = 0; b < count && k < entry.BaseBursts.Length; b++, k++)
                {
                    var burst = emission.GetBurst(b);
                    var c = burst.count;
                    c.constant = Scaled(entry.BaseBursts[k]);
                    burst.count = c;
                    emission.SetBurst(b, burst);
                }
            }
        }

        private int Scaled(float baseCount) => Mathf.Max(1, Mathf.RoundToInt(baseCount * _burstScale));

        private static float ClampScale(float scale) => Mathf.Clamp(scale, 0.1f, 1.5f);

        private static float[] ReadBursts(ParticleSystem prefab)
        {
            var counts = new List<float>(8);
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = ps.emission;
                for (int b = 0; b < emission.burstCount; b++)
                    counts.Add(emission.GetBurst(b).count.constant);
            }
            return counts.ToArray();
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
