using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Pooled, temporary presentation of one animal. Cached references only: no Update, no save
    /// data, no economy, no persistent RNG. Losing a view never loses an animal.
    /// Variants are applied through the per-renderer Shader User Value: one shared material,
    /// SRP Batcher compatible, never a material clone or a MaterialPropertyBlock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalView : MonoBehaviour, IPoolable
    {
        public static readonly int IdleState = Animator.StringToHash("Idle");

        [SerializeField] private Renderer[] _renderers = System.Array.Empty<Renderer>();
        [SerializeField] private Animator _animator;
        [SerializeField] private LODGroup _lodGroup;
        [SerializeField] private Transform _socketHeadTop;
        [SerializeField] private Transform _socketNeck;
        [SerializeField] private Transform _socketBack;
        [SerializeField] private Transform _socketTailBase;
        [Tooltip("Soft disc under the animal, shown only when the quality tier has no shadow map.")]
        [SerializeField] private Renderer _blobShadow;
        [Tooltip("T49 variant modules: renderers shown only when the visual data asks for their slot (all LODs of a module).")]
        [SerializeField] private Renderer[] _moduleRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private byte[] _moduleSlots = System.Array.Empty<byte>();
        private int _shownModule = -1;

        private SkinnedMeshRenderer[] _skinned;
        private AccessoryView _accessories;
        private MeshRenderer[] _meshes;
        private Transform _transform;
        private bool _initialized;

        /// <summary>0 when the view is free in its pool.</summary>
        public uint OwnerInstanceId { get; private set; }

        public ContentId Species { get; private set; }
        public uint VisualData { get; private set; }

        /// <summary>Pool that created this view. Set once by AnimalViewPool.</summary>
        internal GameObjectPool<AnimalView> SourcePool { get; set; }

        internal int PoolHandle { get; set; } = -1;

        public Animator Animator => _animator;
        public LODGroup LodGroup => _lodGroup;
        public Transform SocketHeadTop => _socketHeadTop;
        public Transform SocketNeck => _socketNeck;
        public Transform SocketBack => _socketBack;
        public Transform SocketTailBase => _socketTailBase;
        public int RendererCount => _renderers.Length;

        /// <summary>T47: the accessories layer (added once by <see cref="AccessoryView.Ensure"/>), or null.</summary>
        public AccessoryView Accessories => _accessories;

        internal void AttachAccessories(AccessoryView accessories) => _accessories = accessories;

        /// <summary>Editor/prefab builder hook. Runtime code never rewires a view.</summary>
        public void SetReferences(Renderer[] renderers, Animator animator, LODGroup lodGroup,
            Transform headTop, Transform neck, Transform back, Transform tailBase, Renderer blobShadow = null)
        {
            _blobShadow = blobShadow;
            _renderers = renderers;
            _animator = animator;
            _lodGroup = lodGroup;
            _socketHeadTop = headTop;
            _socketNeck = neck;
            _socketBack = back;
            _socketTailBase = tailBase;
            _initialized = false;
        }

        /// <summary>T49 editor/prefab builder hook: the variant module renderers and their slots (1-3), same length.</summary>
        public void SetModules(Renderer[] renderers, byte[] slots)
        {
            _moduleRenderers = renderers ?? System.Array.Empty<Renderer>();
            _moduleSlots = slots ?? System.Array.Empty<byte>();
            _shownModule = -1;
        }

        public int ModuleCount => _moduleRenderers.Length;

        public void Configure(in AnimalVisualSnapshot snapshot)
        {
            EnsureInitialized();
            OwnerInstanceId = snapshot.InstanceId;
            Species = snapshot.Species;

            _transform.SetPositionAndRotation(snapshot.Position, Quaternion.Euler(0f, snapshot.Yaw, 0f));
            float scale = snapshot.Scale > 0f ? snapshot.Scale : 1f;
            _transform.localScale = new Vector3(scale, scale, scale);

            ApplyVisualData(snapshot.VisualData);
            if (_blobShadow != null)
                _blobShadow.enabled = PresentationQuality.BlobShadows;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.Play(IdleState, 0, snapshot.IdlePhase);
        }

        /// <summary>Moves the view. Called by the scheduler for walking animals only.</summary>
        public void SetPose(Vector3 position, float yaw)
        {
            EnsureInitialized();
            _transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>Moves the view with a full rotation (yaw tilted with the ground slope, T33C).</summary>
        public void SetPose(Vector3 position, Quaternion rotation)
        {
            EnsureInitialized();
            _transform.SetPositionAndRotation(position, rotation);
        }

        public bool BlobShadowVisible => _blobShadow != null && _blobShadow.enabled;

        /// <summary>T34: real shadows turned on or off during play (adaptive quality): the blob shadow follows.</summary>
        public void RefreshQuality()
        {
            EnsureInitialized();
            if (_blobShadow != null && _blobShadow.enabled != PresentationQuality.BlobShadows)
                _blobShadow.enabled = PresentationQuality.BlobShadows;
        }

        /// <summary>Animator playback rate (1 normally; a newborn bounding out of its shell plays faster).</summary>
        public void SetPlaybackSpeed(float speed)
        {
            EnsureInitialized();
            if (_animator != null && !Mathf.Approximately(_animator.speed, speed))
                _animator.speed = speed;
        }

        /// <summary>Uniform scale, used by staged reveals (hatch). Configure sets the resting scale.</summary>
        public void SetScale(float scale)
        {
            EnsureInitialized();
            _transform.localScale = new Vector3(scale, scale, scale);
        }

        /// <summary>Crossfades to an animator state by hash. Called by the scheduler, never every frame.</summary>
        public void PlayState(int stateHash, float fadeSeconds)
        {
            if (_animator != null)
                _animator.CrossFadeInFixedTime(stateHash, fadeSeconds);
        }

        /// <summary>Clears everything a previous owner set, so a recycled view never shows stale data.</summary>
        public void ResetForPool()
        {
            EnsureInitialized();
            // T47: a recycled view never keeps the previous owner's accessories.
            if (_accessories != null)
                _accessories.Clear();
            OwnerInstanceId = 0;
            Species = ContentId.None;
            ApplyVisualData(0u);
            _transform.localScale = Vector3.one;
            if (_animator != null)
                _animator.speed = 1f;
        }

        private void ApplyVisualData(uint data)
        {
            VisualData = data;
            for (int i = 0; i < _skinned.Length; i++)
                _skinned[i].SetShaderUserValue(data);
            for (int i = 0; i < _meshes.Length; i++)
                _meshes[i].SetShaderUserValue(data);
            // T49: a variant module shows only on its variant; toggled when the slot changes (Configure, pool reset).
            int module = AnimalVisualPacking.ModuleSlot(data);
            if (module != _shownModule)
            {
                _shownModule = module;
                int count = Mathf.Min(_moduleRenderers.Length, _moduleSlots.Length);
                for (int i = 0; i < count; i++)
                {
                    if (_moduleRenderers[i] != null)
                        _moduleRenderers[i].enabled = module != 0 && _moduleSlots[i] == module;
                }
            }
        }

        void IPoolable.OnAcquire()
        {
        }

        void IPoolable.OnRelease() => ResetForPool();

        private void EnsureInitialized()
        {
            if (_initialized)
                return;
            _initialized = true;
            _transform = transform;
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            int skinnedCount = 0, meshCount = 0;
            foreach (var r in _renderers)
            {
                if (r is SkinnedMeshRenderer) skinnedCount++;
                else if (r is MeshRenderer) meshCount++;
            }
            _skinned = new SkinnedMeshRenderer[skinnedCount];
            _meshes = new MeshRenderer[meshCount];
            skinnedCount = meshCount = 0;
            foreach (var r in _renderers)
            {
                if (r is SkinnedMeshRenderer s) _skinned[skinnedCount++] = s;
                else if (r is MeshRenderer m) _meshes[meshCount++] = m;
            }
        }

        private void Awake() => EnsureInitialized();
    }
}
