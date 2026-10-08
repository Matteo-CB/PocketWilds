using System;
using PocketWilds.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// The groomed animal on the set (T38, doc 01 Contenu S-ACT-001): a second view of the player's own animal, built once
    /// per species from the visual catalog's prefab when its first game loads, then reused game after game. Its LOD0 wears
    /// the measured groom mesh and the stage's dust material (SH_AnimalMaster with _PW_DUST); its bones are opened up
    /// (AnimatorUtility.DeoptimizeTransformHierarchy, this view only) so the zones are skinned exactly like the fur and the
    /// debris lie on it whatever the animation. Per frame: zone points, normals and their screen disks, no allocation.
    /// </summary>
    public sealed class GroomingAnimal
    {
        private static readonly int ZonesId = Shader.PropertyToID("_PW_GroomZones");
        private static readonly int ParamsId = Shader.PropertyToID("_PW_GroomParams");

        /// <summary>A zone smaller than this on screen (share of the screen's height) is widened: always a fair target.</summary>
        public const float MinScreenRadius = 0.022f;

        private readonly GroomingBody _body;
        private readonly Transform _root;
        private readonly Transform[] _bones;
        private readonly int[] _usedBones;
        private readonly Matrix4x4[] _boneMatrices;
        private readonly MaterialPropertyBlock _block;
        private readonly float[] _zones = new float[GroomingLayout.MaxZones];
        private Vector4 _params;
        private bool _blockDirty = true;
        private readonly int _zoneCount;

        public GroomingAnimal(AnimalView view, GroomingBody body, Material dustMaterial)
        {
            View = view != null ? view : throw new ArgumentNullException(nameof(view));
            _root = view.transform;
            _body = body;
            Skin = FindLod0(view);
            var visual = view.Animator != null ? view.Animator.gameObject : view.gameObject;
            bool skinned = body != null && body.HasSkin;
            if (skinned && view.Animator != null)
                AnimatorUtility.DeoptimizeTransformHierarchy(visual);
            HeadTop = FindDeep(view.transform, "SOCK_HeadTop") ?? view.SocketHeadTop;
            Neck = FindDeep(view.transform, "SOCK_Neck") ?? view.SocketNeck;

            _zoneCount = body != null ? Mathf.Min(body.zoneCount, GroomingLayout.MaxZones) : 0;
            if (skinned)
                _bones = ResolveBones(Skin, body.boneNames, view.transform);
            Skinned = skinned && _bones != null;
            if (Skinned)
            {
                // The bones the zones use, read once per frame.
                var used = new bool[_bones.Length];
                int count = 0;
                for (int i = 0; i < _zoneCount * GroomingBody.Influences; i++)
                {
                    int bone = body.zoneBones[i];
                    if (bone >= 0 && bone < _bones.Length && !used[bone])
                    {
                        used[bone] = true;
                        count++;
                    }
                }
                _usedBones = new int[count];
                count = 0;
                for (int i = 0; i < used.Length; i++)
                    if (used[i])
                        _usedBones[count++] = i;
                _boneMatrices = new Matrix4x4[_bones.Length];
            }

            if (Skin != null && body != null && body.groomMesh != null && dustMaterial != null)
            {
                Skin.sharedMesh = body.groomMesh;
                Skin.sharedMaterial = dustMaterial;
                _block = new MaterialPropertyBlock();
                Dusty = true;
            }
            // The close-up always shows LOD0, the only one wearing the dust.
            if (view.LodGroup != null)
                view.LodGroup.ForceLOD(0);

            Points = new Vector3[GroomingLayout.MaxZones];
            Normals = new Vector3[GroomingLayout.MaxZones];
            Screen = new Vector2[GroomingLayout.MaxZones];
            ScreenRadii = new float[GroomingLayout.MaxZones];
            Hittable = new bool[GroomingLayout.MaxZones];
            Depth = new float[GroomingLayout.MaxZones];
        }

        public AnimalView View { get; }

        /// <summary>LOD0: the renderer wearing the groom mesh and the dust.</summary>
        public SkinnedMeshRenderer Skin { get; }

        public Transform HeadTop { get; }
        public Transform Neck { get; }

        public GroomingBody Body => _body;

        /// <summary>The zones follow the bones (else they ride the root, rigid: a species whose bones could not be opened).</summary>
        public bool Skinned { get; }

        /// <summary>The fur shows the dust (a measured groom mesh exists for this species).</summary>
        public bool Dusty { get; }

        public int ZoneCount => _zoneCount;

        public float Scale => _root.localScale.x;

        // Per zone, refreshed by Update.
        public Vector3[] Points { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Screen { get; }
        public float[] ScreenRadii { get; }
        public bool[] Hittable { get; }

        /// <summary>Distance along the camera's view (m).</summary>
        public float[] Depth { get; }

        /// <summary>The dust value written for a zone (dust + 2 × glint step), as the shader reads it.</summary>
        public float ShaderValue(int zone) => (uint)zone < (uint)_zones.Length ? _zones[zone] : 0f;

        public float Content => _params.x;

        /// <summary>World zone points and normals (after the animation, call from LateUpdate), then their screen disks.</summary>
        public void Update(Camera camera)
        {
            int n = _zoneCount;
            if (Skinned)
            {
                for (int i = 0; i < _usedBones.Length; i++)
                {
                    int b = _usedBones[i];
                    var bone = _bones[b];
                    _boneMatrices[b] = bone != null ? bone.localToWorldMatrix : _root.localToWorldMatrix;
                }
                var bones = _body.zoneBones;
                var weights = _body.zoneWeights;
                var points = _body.zoneLocalPoints;
                var normals = _body.zoneLocalNormals;
                for (int z = 0; z < n; z++)
                {
                    var p = Vector3.zero;
                    var nn = Vector3.zero;
                    int k0 = z * GroomingBody.Influences;
                    for (int k = k0; k < k0 + GroomingBody.Influences; k++)
                    {
                        int b = bones[k];
                        float w = weights[k];
                        if (b < 0 || w <= 0f)
                            continue;
                        ref readonly var m = ref _boneMatrices[b];
                        p += w * m.MultiplyPoint3x4(points[k]);
                        nn += w * m.MultiplyVector(normals[k]);
                    }
                    Points[z] = p;
                    float len = nn.magnitude;
                    Normals[z] = len > 1e-5f ? nn / len : Vector3.up;
                }
            }
            else if (_body != null)
            {
                var m = _root.localToWorldMatrix;
                for (int z = 0; z < n; z++)
                {
                    Points[z] = m.MultiplyPoint3x4(_body.zonePoints[z]);
                    var nn = m.MultiplyVector(_body.zoneNormals[z]);
                    float len = nn.magnitude;
                    Normals[z] = len > 1e-5f ? nn / len : Vector3.up;
                }
            }
            if (camera != null)
                Project(camera);
        }

        private void Project(Camera camera)
        {
            var viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
            var rect = camera.pixelRect;
            float tanHalf = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float minRadius = MinScreenRadius * rect.height;
            var eye = camera.transform.position;
            float scale = Scale;
            for (int z = 0; z < _zoneCount; z++)
            {
                var p = Points[z];
                var clip = viewProjection * new Vector4(p.x, p.y, p.z, 1f);
                if (clip.w <= 0.01f)
                {
                    Hittable[z] = false;
                    continue;
                }
                float inv = 1f / clip.w;
                Screen[z] = new Vector2(rect.x + (clip.x * inv * 0.5f + 0.5f) * rect.width, rect.y + (clip.y * inv * 0.5f + 0.5f) * rect.height);
                Depth[z] = clip.w;
                float pixelsPerMeter = rect.height / (2f * clip.w * tanHalf);
                float radius = _body.zoneRadii.Length > z ? _body.zoneRadii[z] : 0.03f;
                ScreenRadii[z] = Mathf.Max(minRadius, radius * scale * pixelsPerMeter);
                var toEye = eye - p;
                // A zone turned away from the camera (a head turning) waits until it faces it again.
                Hittable[z] = Vector3.Dot(Normals[z], toEye) > -0.05f * toEye.magnitude;
            }
        }

        // ---------------- dust (MaterialPropertyBlock, written only on a change) ----------------

        /// <summary>A zone's dust (0 to 1) and clean glint (0 to 1). Marks the block dirty only when the value moves.</summary>
        public void SetZone(int zone, float dust, float glint)
        {
            if ((uint)zone >= (uint)_zones.Length)
                return;
            float value = Mathf.Clamp01(dust) + 2f * Mathf.Round(Mathf.Clamp01(glint) * 15f);
            if (Mathf.Abs(value - _zones[zone]) < 0.004f)
                return;
            _zones[zone] = value;
            _blockDirty = true;
        }

        /// <summary>The content eyes (0 open, 1 half closed: « il ferme les yeux »).</summary>
        public void SetContent(float content)
        {
            content = Mathf.Clamp01(content);
            if (Mathf.Abs(content - _params.x) < 0.01f && !(content == 0f && _params.x != 0f))
                return;
            _params.x = content;
            _blockDirty = true;
        }

        public void ClearDust()
        {
            for (int i = 0; i < _zones.Length; i++)
                _zones[i] = 0f;
            _params = Vector4.zero;
            _blockDirty = true;
            Upload();
        }

        /// <summary>Sends the zone array to the renderer when something changed (SetFloatArray copies, no allocation).</summary>
        public void Upload()
        {
            if (!_blockDirty || _block == null || Skin == null)
                return;
            _blockDirty = false;
            _block.SetFloatArray(ZonesId, _zones);
            _block.SetVector(ParamsId, _params);
            Skin.SetPropertyBlock(_block);
        }

        // ---------------- building ----------------

        private static SkinnedMeshRenderer FindLod0(AnimalView view)
        {
            if (view.LodGroup != null && view.LodGroup.lodCount > 0)
            {
                var lods = view.LodGroup.GetLODs();
                if (lods.Length > 0 && lods[0].renderers != null)
                    foreach (var r in lods[0].renderers)
                        if (r is SkinnedMeshRenderer skin)
                            return skin;
            }
            return view.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        /// <summary>The renderer's bones after the hierarchy was opened, checked by name (found by name when the order differs).</summary>
        private static Transform[] ResolveBones(SkinnedMeshRenderer skin, string[] names, Transform root)
        {
            if (skin == null || names == null || names.Length == 0)
                return null;
            var bones = skin.bones;
            if (bones != null && bones.Length == names.Length)
            {
                bool match = true;
                for (int i = 0; i < bones.Length && match; i++)
                    match = bones[i] != null && bones[i].name == names[i];
                if (match)
                    return bones;
            }
            var found = new Transform[names.Length];
            int hits = 0;
            for (int i = 0; i < names.Length; i++)
            {
                found[i] = FindDeep(root, names[i]);
                if (found[i] != null)
                    hits++;
            }
            if (hits < names.Length)
                GameLog.Warning(LogCategory.Presentation, $"Grooming: {names.Length - hits} bones of {root.name} not found, their zones ride the root.");
            return hits > 0 ? found : null;
        }

        public static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null)
                return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;
                var found = FindDeep(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        public void Destroy()
        {
            if (View == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(View.gameObject);
            else
                Object.DestroyImmediate(View.gameObject);
        }
    }
}
