using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEngine;

namespace PocketWilds.Benchmarks
{
    /// <summary>
    /// SC_Benchmark_Animals driver (editor and development builds only).
    /// Until the real Rabbit prefab exists it uses a TEMP cube placeholder, built at runtime and
    /// never saved as an asset. One manager Update; views have none.
    /// </summary>
    public sealed class BenchmarkAnimalsEntry : MonoBehaviour, ISceneEntry
    {
        private const string PlaceholderKey = "TEMP_PlaceholderAnimal";

        [SerializeField] private int _viewCount = 20;
        [SerializeField] private int _softCapacity = 64;
        [SerializeField] private int _hardCapacity = 128;
        [SerializeField] private bool _recycleEveryFrame = true;
        [SerializeField] private int _recyclesPerFrame = 4;
        [SerializeField] private float _spacing = 1.2f;

        private AnimalViewPool _pool;
        private int _handle;
        private AnimalView[] _views;
        private GameObject _template;
        private int _cursor;
        private uint _nextFakeId = 1;

        public AnimalViewPool Pool => _pool;
        public int ActiveCount { get; private set; }

        public void Enter(GameContext context) => Build();

        public void Exit() => Teardown();

        private void Start()
        {
            // Benchmarks may also run directly from the scene, without the bootstrap.
            if (_pool == null)
                Build();
        }

        private void Build()
        {
            if (_pool != null)
                return;

            _template = CreatePlaceholderTemplate();
            var root = new GameObject("AnimalViewPool").transform;
            root.SetParent(transform, false);
            _pool = new AnimalViewPool(root);
            _handle = _pool.Register(PlaceholderKey, _template.GetComponent<AnimalView>(), _softCapacity, _hardCapacity);
            _pool.Warmup(_handle, Mathf.Min(_viewCount + _recyclesPerFrame, _softCapacity));

            _views = new AnimalView[_viewCount];
            for (int i = 0; i < _viewCount; i++)
                _views[i] = Spawn(i);
            GameLog.Info(LogCategory.Benchmark, "Benchmark animals ready: " + ActiveCount);
        }

        private void Update()
        {
            if (_pool == null || !_recycleEveryFrame || _views.Length == 0)
                return;

            for (int n = 0; n < _recyclesPerFrame; n++)
            {
                _cursor = (_cursor + 1) % _views.Length;
                if (_views[_cursor] != null)
                {
                    _pool.Release(_views[_cursor]);
                    ActiveCount--;
                }
                _views[_cursor] = Spawn(_cursor);
            }
        }

        private AnimalView Spawn(int slot)
        {
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(_viewCount)));
            var snapshot = new AnimalVisualSnapshot
            {
                InstanceId = _nextFakeId++,
                Position = new Vector3((slot % columns - columns * 0.5f) * _spacing, 0f, (slot / columns) * _spacing),
                Yaw = (slot * 47) % 360,
                Scale = 0.45f,
                PrimaryColor = Color.HSVToRGB((slot * 0.137f) % 1f, 0.35f, 0.95f),
                SecondaryColor = Color.white,
                BellyColor = Color.white,
                DetailColor = Color.black,
                EyeColor = Color.black,
            };

            if (!_pool.TryAcquire(_handle, snapshot, out var view))
                return null;
            ActiveCount++;
            return view;
        }

        private GameObject CreatePlaceholderTemplate()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.name = PlaceholderKey;
            // Immediate: the template is cloned in this same frame during warmup.
            DestroyImmediate(template.GetComponent<Collider>());
            template.AddComponent<AnimalView>();
            template.SetActive(false);
            template.transform.SetParent(transform, false);
            return template;
        }

        private void Teardown()
        {
            _pool?.Dispose();
            _pool = null;
            if (_template != null)
                Destroy(_template);
        }

        private void OnDestroy() => Teardown();
    }
}
