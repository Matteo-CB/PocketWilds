using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Benchmarks
{
    /// <summary>
    /// SC_Benchmark_Animals driver (editor and development builds only). Performance Test Matrix
    /// scenarios P1 (1 rabbit), P2 (20), P3 (30 A + 40 B): real PF_Animal_Rabbit views from the pool,
    /// real palettes, the central AnimalScheduler. One manager Update; views have none.
    /// Optional pool churn keeps the T05 stress available.
    /// </summary>
    public sealed class BenchmarkAnimalsEntry : MonoBehaviour, ISceneEntry
    {
        [SerializeField] private AnimalVisualCatalog _catalog;
        [SerializeField] private ContentDatabaseAsset _contentDatabase;
        [SerializeField] private string _visualKey = "PF_Animal_Rabbit";
        [SerializeField] private string _speciesId = "species.rabbit";
        [SerializeField, Min(0)] private int _animalCount = 70;
        [SerializeField] private QualityTier _quality = QualityTier.Medium;
        [SerializeField] private WanderArea _area = new WanderArea(new Vector3(0f, 0f, 1f), 3f);
        [SerializeField] private bool _poolChurn;
        [SerializeField, Min(1)] private int _churnPerSecond = 4;

        private AnimalViewPool _pool;
        private PaletteTable _ownedPalettes;
        private AnimalVisualResolver _resolver;
        private AnimalScheduler _scheduler;
        private BehaviorProfile _profile;
        private AnimalView[] _views;
        private int[] _agents;
        private int _poolHandle;
        private int _cursor;
        private float _churnBudget;
        private uint _nextId = 1;
        private Camera _camera;

        public AnimalScheduler Scheduler => _scheduler;
        public AnimalViewPool Pool => _pool;
        public int ActiveCount { get; private set; }

        public void Enter(GameContext context) => Build(context);

        public void Exit() => Teardown();

        private void Start()
        {
            // Benchmarks also run straight from the scene, without the bootstrap.
            if (_scheduler == null)
                Build(null);
        }

        private void Build(GameContext context)
        {
            if (_scheduler != null)
                return;

            var catalog = context?.VisualCatalog ?? _catalog;
            if (catalog == null || !catalog.TryGet(_visualKey, out var entry) || entry.prefab == null)
            {
                GameLog.Error(LogCategory.Benchmark, "Benchmark: no prefab for " + _visualKey);
                enabled = false;
                return;
            }

            if (context != null)
            {
                _resolver = context.Visuals;
            }
            else
            {
                var registry = _contentDatabase.CreateBuilder().Build();
                _ownedPalettes = new PaletteTable(registry);
                _resolver = new AnimalVisualResolver(registry, _ownedPalettes);
            }

            _profile = entry.behavior;
            _camera = Camera.main;
            var root = new GameObject("AnimalViewPool").transform;
            root.SetParent(transform, false);
            _pool = new AnimalViewPool(root);
            int capacity = Mathf.Max(_animalCount + 8, 1);
            _poolHandle = _pool.Register(entry.visualKey, entry.prefab, capacity, capacity);
            _pool.Warmup(_poolHandle, capacity);

            _scheduler = new AnimalScheduler(capacity, SchedulerSettings.ForQuality(_quality, false), _area);
            _views = new AnimalView[_animalCount];
            _agents = new int[_animalCount];
            for (int i = 0; i < _animalCount; i++)
                Spawn(i);
            GameLog.Info(LogCategory.Benchmark, "Benchmark animals ready: " + ActiveCount);
        }

        private void Update()
        {
            if (_scheduler == null)
                return;

            if (_poolChurn && _animalCount > 0)
            {
                _churnBudget += _churnPerSecond * Time.deltaTime;
                while (_churnBudget >= 1f)
                {
                    _churnBudget -= 1f;
                    _cursor = (_cursor + 1) % _animalCount;
                    Despawn(_cursor);
                    Spawn(_cursor);
                }
            }

            if (_camera != null)
                _scheduler.SetFocus(GroundFocus(_camera.transform));
            _scheduler.Tick(Time.time, Time.deltaTime);
        }

        private void Spawn(int index)
        {
            uint id = _nextId++;
            var animal = new AnimalInstance
            {
                InstanceId = id,
                Species = ContentId.FromSource(_speciesId),
                // Unknown variant ids fall back to the species default; cycle the six rabbit palettes.
                Variant = ContentId.FromSource(VariantFor(index)),
                Seed = SeedHash.Mix(id, 0xBE7C0001u),
            };
            float angle = index * 2.39996f;
            float radius = _area.Radius * Mathf.Sqrt((index + 0.5f) / Mathf.Max(1, _animalCount));
            var position = _area.Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            _resolver.TryResolve(animal, position, (index * 137) % 360, out var snapshot);
            if (!_pool.TryAcquire(_poolHandle, snapshot, out var view))
            {
                _views[index] = null;
                _agents[index] = AnimalScheduler.InvalidHandle;
                return;
            }
            _views[index] = view;
            _agents[index] = _scheduler.Register(view, id, animal.Seed, position, snapshot.Yaw, _profile, Time.time);
            ActiveCount++;
        }

        private void Despawn(int index)
        {
            if (_views[index] == null)
                return;
            _scheduler.Unregister(_agents[index]);
            _pool.Release(_views[index]);
            _views[index] = null;
            ActiveCount--;
        }

        private static string VariantFor(int index)
        {
            switch (index % 6)
            {
                case 0: return "variant.rabbit.brown";
                case 1: return "variant.rabbit.white";
                case 2: return "variant.rabbit.grey";
                case 3: return "variant.rabbit.black";
                case 4: return "variant.rabbit.spotted";
                default: return "variant.rabbit.sakura";
            }
        }

        private static Vector3 GroundFocus(Transform camera)
        {
            var forward = camera.forward;
            if (forward.y > -0.01f)
                return camera.position;
            float t = -camera.position.y / forward.y;
            return camera.position + forward * t;
        }

        private void Teardown()
        {
            _scheduler = null;
            _pool?.Dispose();
            _pool = null;
            _ownedPalettes?.Dispose();
            _ownedPalettes = null;
        }

        private void OnDestroy() => Teardown();
    }
}
