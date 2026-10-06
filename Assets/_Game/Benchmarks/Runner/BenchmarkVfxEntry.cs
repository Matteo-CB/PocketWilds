using System.Collections;
using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PocketWilds.Benchmarks
{
    /// <summary>
    /// SC_Benchmark_VFX driver (L-SC-008, editor and development builds only). Repeats the hatch and feedback effects
    /// at the pool's maximum concurrency on a plain ground, switching quality tier every few seconds (Low, Medium, High),
    /// for GPU, overdraw and pool reviews without the rest of the game. The effects come from the hatch staging of the
    /// Addressables group CommonAnimals, exactly as in the refuge.
    /// </summary>
    public sealed class BenchmarkVfxEntry : MonoBehaviour
    {
        [SerializeField] private string _hatchConfigKey = "animals.hatch";
        [Tooltip("Seconds per quality tier.")]
        [SerializeField, Min(1f)] private float _tierSeconds = 5f;
        [Tooltip("New effect every this many seconds (each lives about 1 s: the pool stays saturated).")]
        [SerializeField, Min(0.02f)] private float _spawnEvery = 0.06f;
        [SerializeField] private float _spread = 1.6f;
        [SerializeField] private bool _runOnStart = true;

        /// <summary>Set by the benchmark runner before loading the scene: it drives the run and samples the frames.</summary>
        public static bool DrivenByRunner;

        private static readonly QualityTier[] Tiers = { QualityTier.Low, QualityTier.Medium, QualityTier.High };

        private AsyncOperationHandle<HatchPresentationConfig> _config;
        private FeedbackVfx _vfx;
        private uint _rng = 0x51ED27u;

        public FeedbackVfx Vfx => _vfx;
        public bool Ready => _vfx != null;
        public int PeakPlaying { get; private set; }
        public int Spawned { get; private set; }
        public int TierSwitches { get; private set; }

        private IEnumerator Start()
        {
            _config = Addressables.LoadAssetAsync<HatchPresentationConfig>(_hatchConfigKey);
            yield return _config;
            if (_config.Status != AsyncOperationStatus.Succeeded || _config.Result.vfx == null)
            {
                GameLog.Error(LogCategory.Benchmark, "VFX benchmark: no hatch staging (" + _hatchConfigKey + ").");
                yield break;
            }
            _vfx = new FeedbackVfx(_config.Result.vfx, transform, QualityTier.Medium);
            if (_runOnStart && !DrivenByRunner)
                StartCoroutine(Run(float.PositiveInfinity));
        }

        /// <summary>Plays effects for the given seconds, cycling the tiers (the runner drives it with a fixed length).</summary>
        public IEnumerator Run(float seconds)
        {
            while (_vfx == null)
                yield return null;
            float start = Time.time, nextSpawn = start, nextTier = start;
            int tier = 0;
            while (Time.time - start < seconds)
            {
                float time = Time.time;
                if (time >= nextTier)
                {
                    _vfx.SetTier(Tiers[tier % Tiers.Length]);
                    tier++;
                    TierSwitches++;
                    nextTier = time + _tierSeconds;
                }
                while (time >= nextSpawn)
                {
                    var id = (VfxId)(Next() % (uint)VfxId.Count);
                    var at = transform.position + new Vector3(Range(-_spread, _spread), 0.05f, Range(-_spread, _spread));
                    if (_vfx.Play(id, at, time))
                        Spawned++;
                    nextSpawn += _spawnEvery;
                }
                _vfx.Tick(time);
                if (_vfx.PlayingCount > PeakPlaying)
                    PeakPlaying = _vfx.PlayingCount;
                yield return null;
            }
        }

        private float Range(float min, float max) => min + (max - min) * ((Next() >> 8) * (1f / 16777216f));

        private uint Next()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return _rng;
        }

        private void OnDestroy()
        {
            _vfx?.Dispose();
            _vfx = null;
            if (_config.IsValid())
                Addressables.Release(_config);
            _config = default;
        }
    }
}
