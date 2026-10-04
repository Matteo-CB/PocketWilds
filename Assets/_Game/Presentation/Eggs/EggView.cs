using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Pooled egg presentation (PF_Egg_Base). Cached references only: no Update, no animator, no physics.
    /// The hatch sequence drives the shell pose, crack stage and fragment flight from one manager tick.
    /// Every egg type is this prefab plus palette data (one material, no clones, no property blocks).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EggView : MonoBehaviour, IPoolable
    {
        public const int FragmentCount = 3;

        [SerializeField] private Transform _shell;
        [SerializeField] private Renderer[] _shellRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Transform[] _fragmentPivots = System.Array.Empty<Transform>();
        [SerializeField] private Renderer[] _fragmentRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Transform _vfxPoint;
        [SerializeField] private Transform _revealPoint;
        [SerializeField] private Renderer _blobShadow;

        private MeshRenderer[] _shellMeshes;
        private MeshRenderer[] _fragmentMeshes;
        private Vector3[] _fragmentRest;
        private Transform _transform;
        private bool _initialized;

        public uint VisualData { get; private set; }
        public Transform VfxPoint => _vfxPoint != null ? _vfxPoint : transform;
        public Transform RevealPoint => _revealPoint != null ? _revealPoint : transform;
        public bool ShellVisible { get; private set; } = true;
        public bool FragmentsVisible { get; private set; }

        /// <summary>Editor/prefab builder hook. Runtime code never rewires a view.</summary>
        public void SetReferences(Transform shell, Renderer[] shellRenderers, Transform[] fragmentPivots, Renderer[] fragmentRenderers,
            Transform vfxPoint, Transform revealPoint, Renderer blobShadow = null)
        {
            _blobShadow = blobShadow;
            _shell = shell;
            _shellRenderers = shellRenderers;
            _fragmentPivots = fragmentPivots;
            _fragmentRenderers = fragmentRenderers;
            _vfxPoint = vfxPoint;
            _revealPoint = revealPoint;
            _initialized = false;
        }

        public Vector3 FragmentRestPosition(int index)
        {
            EnsureInitialized();
            return _fragmentRest[index];
        }

        public void Configure(Vector3 position, float yaw, uint visualData)
        {
            EnsureInitialized();
            _transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            SetShellPose(Vector3.zero, Quaternion.identity, Vector3.one);
            SetShellVisible(true);
            SetFragmentsVisible(false);
            ApplyVisualData(visualData);
            if (_blobShadow != null)
                _blobShadow.enabled = PresentationQuality.BlobShadows;
        }

        public void SetCrackStage(int stage) => ApplyVisualData(EggVisualPacking.WithCrackStage(VisualData, stage));

        public void SetGlow(int glow) => ApplyVisualData(EggVisualPacking.WithGlow(VisualData, glow));

        public void SetShellPose(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            EnsureInitialized();
            _shell.SetLocalPositionAndRotation(localPosition, localRotation);
            _shell.localScale = localScale;
        }

        public void SetShellVisible(bool visible)
        {
            EnsureInitialized();
            ShellVisible = visible;
            for (int i = 0; i < _shellMeshes.Length; i++)
                _shellMeshes[i].enabled = visible;
            if (_blobShadow != null)
                _blobShadow.enabled = visible && PresentationQuality.BlobShadows;
        }

        public void SetFragmentsVisible(bool visible)
        {
            EnsureInitialized();
            FragmentsVisible = visible;
            for (int i = 0; i < _fragmentMeshes.Length; i++)
                _fragmentMeshes[i].enabled = visible;
            if (!visible)
            {
                for (int i = 0; i < _fragmentPivots.Length; i++)
                    SetFragmentPose(i, _fragmentRest[i], Quaternion.identity, 1f);
            }
        }

        public void SetFragmentPose(int index, Vector3 localPosition, Quaternion localRotation, float scale)
        {
            var pivot = _fragmentPivots[index];
            pivot.SetLocalPositionAndRotation(localPosition, localRotation);
            pivot.localScale = new Vector3(scale, scale, scale);
        }

        public void ResetForPool()
        {
            EnsureInitialized();
            SetShellPose(Vector3.zero, Quaternion.identity, Vector3.one);
            SetShellVisible(true);
            SetFragmentsVisible(false);
            ApplyVisualData(0u);
        }

        void IPoolable.OnAcquire()
        {
        }

        void IPoolable.OnRelease() => ResetForPool();

        private void ApplyVisualData(uint data)
        {
            VisualData = data;
            for (int i = 0; i < _shellMeshes.Length; i++)
                _shellMeshes[i].SetShaderUserValue(data);
            // Fragments are the broken shell: they always show the full crack.
            uint fragments = EggVisualPacking.WithGlow(EggVisualPacking.WithCrackStage(data, 2), 0);
            for (int i = 0; i < _fragmentMeshes.Length; i++)
                _fragmentMeshes[i].SetShaderUserValue(fragments);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;
            _initialized = true;
            _transform = transform;
            if (_shell == null)
                _shell = _transform;
            _shellMeshes = ToMeshRenderers(_shellRenderers);
            _fragmentMeshes = ToMeshRenderers(_fragmentRenderers);
            _fragmentRest = new Vector3[_fragmentPivots.Length];
            for (int i = 0; i < _fragmentPivots.Length; i++)
                _fragmentRest[i] = _fragmentPivots[i].localPosition;
        }

        private static MeshRenderer[] ToMeshRenderers(Renderer[] renderers)
        {
            int count = 0;
            foreach (var r in renderers)
                if (r is MeshRenderer) count++;
            var result = new MeshRenderer[count];
            count = 0;
            foreach (var r in renderers)
                if (r is MeshRenderer m) result[count++] = m;
            return result;
        }

        private void Awake() => EnsureInitialized();
    }
}
