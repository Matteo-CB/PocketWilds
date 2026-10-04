using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Pooled, temporary presentation of one animal. Holds cached references only:
    /// no Update, no save data, no economy, no persistent RNG. Losing a view never loses an animal.
    /// Variants are applied through a MaterialPropertyBlock, never a material clone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimalView : MonoBehaviour, IPoolable
    {
        [SerializeField] private Renderer[] _renderers = System.Array.Empty<Renderer>();
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _socketHeadTop;
        [SerializeField] private Transform _socketNeck;
        [SerializeField] private Transform _socketBack;
        [SerializeField] private Transform _socketTailBase;

        private MaterialPropertyBlock _block;
        private Transform _transform;

        /// <summary>0 when the view is free in its pool.</summary>
        public uint OwnerInstanceId { get; private set; }

        public ContentId Species { get; private set; }

        /// <summary>Pool that created this view. Set once by AnimalViewPool.</summary>
        internal GameObjectPool<AnimalView> SourcePool { get; set; }

        internal int PoolHandle { get; set; } = -1;

        public Animator Animator => _animator;
        public Transform SocketHeadTop => _socketHeadTop;
        public Transform SocketNeck => _socketNeck;
        public Transform SocketBack => _socketBack;
        public Transform SocketTailBase => _socketTailBase;
        public int RendererCount => _renderers.Length;

        public void Configure(in AnimalVisualSnapshot snapshot)
        {
            EnsureInitialized();
            OwnerInstanceId = snapshot.InstanceId;
            Species = snapshot.Species;

            _transform.SetPositionAndRotation(snapshot.Position, Quaternion.Euler(0f, snapshot.Yaw, 0f));
            float scale = snapshot.Scale > 0f ? snapshot.Scale : 1f;
            _transform.localScale = new Vector3(scale, scale, scale);

            _block.Clear();
            _block.SetColor(AnimalShaderIds.PrimaryColor, snapshot.PrimaryColor);
            _block.SetColor(AnimalShaderIds.SecondaryColor, snapshot.SecondaryColor);
            _block.SetColor(AnimalShaderIds.BellyColor, snapshot.BellyColor);
            _block.SetColor(AnimalShaderIds.DetailColor, snapshot.DetailColor);
            _block.SetColor(AnimalShaderIds.EyeColor, snapshot.EyeColor);
            _block.SetFloat(AnimalShaderIds.PatternIndex, snapshot.PatternIndex);
            _block.SetFloat(AnimalShaderIds.PatternStrength, snapshot.PatternStrength);
            _block.SetFloat(AnimalShaderIds.RareEffectIntensity, snapshot.RareEffectIntensity);
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].SetPropertyBlock(_block);
        }

        /// <summary>Clears everything a previous owner set, so a recycled view never shows stale data.</summary>
        public void ResetForPool()
        {
            EnsureInitialized();
            OwnerInstanceId = 0;
            Species = ContentId.None;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].SetPropertyBlock(null);
            _transform.localScale = Vector3.one;
        }

        void IPoolable.OnAcquire()
        {
        }

        void IPoolable.OnRelease() => ResetForPool();

        private void EnsureInitialized()
        {
            if (_block != null)
                return;

            _block = new MaterialPropertyBlock();
            _transform = transform;
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);
        }

        private void Awake() => EnsureInitialized();
    }
}
