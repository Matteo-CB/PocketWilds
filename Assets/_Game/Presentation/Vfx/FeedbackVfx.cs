using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// The refuge's feedback effects by name (T34A): one <see cref="VfxPool"/> holding every effect of
    /// <see cref="VfxCatalog"/>, warmed at scene entry so no effect instantiates during play (Gate 10). Particle
    /// counts follow the device tier (<see cref="PresentationQuality.Tier"/>, the last step adaptive quality
    /// lowers): feedback keeps speaking on Low, with about half the particles.
    /// </summary>
    public sealed class FeedbackVfx : IDisposable
    {
        private readonly VfxCatalog _catalog;
        private readonly VfxPool _pool;
        private readonly int[] _handles = new int[(int)VfxId.Count];

        public FeedbackVfx(VfxCatalog catalog, Transform root, QualityTier tier)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            Tier = tier;
            _pool = new VfxPool(root, catalog.maxPlaying, catalog.CountScale(tier));
            for (int i = 0; i < _handles.Length; i++)
                _handles[i] = -1;
            foreach (var effect in catalog.effects)
            {
                if (effect.prefab == null || effect.id >= VfxId.Count || _handles[(int)effect.id] >= 0)
                    continue;
                int handle = _pool.Register(effect.prefab, Math.Max(1, effect.softCapacity), Math.Max(1, effect.hardCapacity));
                _handles[(int)effect.id] = handle;
                if (effect.warmup > 0)
                    _pool.Warmup(handle, effect.warmup);
            }
        }

        public QualityTier Tier { get; private set; }
        public int PlayingCount => _pool.PlayingCount;

        public bool Has(VfxId id) => _handles[(int)id] >= 0;

        public bool Play(VfxId id, Vector3 position, float time)
        {
            int handle = _handles[(int)id];
            return handle >= 0 && _pool.Play(handle, position, Quaternion.identity, time);
        }

        /// <summary>Plays with the root system's start color replaced (rare sparkles take the animal's glow color).</summary>
        public bool Play(VfxId id, Vector3 position, float time, Color tint)
        {
            int handle = _handles[(int)id];
            if (handle < 0 || !_pool.Play(handle, position, Quaternion.identity, time, out var system))
                return false;
            var main = system.main;
            main.startColor = tint;
            return true;
        }

        /// <summary>Device tier or adaptive resolution step changed: counts are rewritten once on the pooled instances.</summary>
        public void SetTier(QualityTier tier)
        {
            if (tier == Tier)
                return;
            Tier = tier;
            _pool.SetBurstScale(_catalog.CountScale(tier));
        }

        public void Tick(float time) => _pool.Tick(time);

        public void StopAll() => _pool.StopAll();

        public void Dispose() => _pool.Dispose();

        /// <summary>Particles one play emits at the current tier, -1 when the effect is missing.</summary>
        public int ParticleCount(VfxId id) => _handles[(int)id] >= 0 ? _pool.ParticleCount(_handles[(int)id]) : -1;

        public float DurationOf(VfxId id) => _handles[(int)id] >= 0 ? _pool.DurationOf(_handles[(int)id]) : 0f;

        public GameObjectPool<ParticleSystem> GetPool(VfxId id) => _handles[(int)id] >= 0 ? _pool.GetPool(_handles[(int)id]) : null;
    }
}
