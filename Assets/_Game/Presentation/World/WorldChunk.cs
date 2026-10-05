using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Root of a chunk prefab (PF_Chunk_&lt;Biome&gt;_&lt;Name&gt;). One component per chunk, never per prop, and no
    /// Update: it binds palette rows to the chunk renderers once, applies the quality density and
    /// exposes authored points (egg, animals, interests, VFX, links). Props stay plain renderers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldChunk : MonoBehaviour
    {
        [Serializable]
        public struct PaletteBinding
        {
            public Renderer renderer;
            [Tooltip("ContentId value of palette.<biome>.<name>.")] public uint paletteId;
            [Range(0, 15)] public byte brightness;

            [Tooltip("Merged props: each instance's brightness is stored in uv.y instead.")]
            public bool vertexBrightness;
            [Tooltip("Small decorative prop removed on Low quality (Biomes doc: -35 to 50% density).")]
            public bool secondary;
            [Tooltip("T33B: the renderer turns around its local Z axis in the shader (windmill sails), no script.")]
            public bool rotor;
        }

        public const int NeutralBrightness = 8;

        [SerializeField] private PaletteBinding[] _bindings = Array.Empty<PaletteBinding>();
        [SerializeField] private WanderArea _wanderArea = new WanderArea(Vector3.zero, 4f);
        [SerializeField] private Transform _eggSpawn;
        [SerializeField] private Transform[] _animalSpawns = Array.Empty<Transform>();
        [SerializeField] private InterestPoint[] _interests = Array.Empty<InterestPoint>();
        [SerializeField] private Transform[] _vfxAnchors = Array.Empty<Transform>();
        [SerializeField] private Transform[] _links = Array.Empty<Transform>();
        [Tooltip("Ground details drawn by the ScatterRenderer (T33): data only, no GameObject per plant.")]
        [SerializeField] private ScatterData _scatter;

        public int BindingCount => _bindings.Length;
        public Transform EggSpawn => _eggSpawn;
        public Transform[] AnimalSpawns => _animalSpawns;
        public int InterestCount => _interests.Length;
        public InterestPoint GetInterest(int index) => _interests[index];
        public Transform[] VfxAnchors => _vfxAnchors;
        public Transform[] Links => _links;
        public ScatterData Scatter => _scatter;

        /// <summary>Walkable disk in world space (simple region data, no NavMesh).</summary>
        public WanderArea WanderArea => new WanderArea(transform.TransformPoint(_wanderArea.Center), _wanderArea.Radius);

        /// <summary>Wander area in chunk space (authoring).</summary>
        public WanderArea LocalWanderArea => _wanderArea;

        /// <summary>World-space copy of the points of interest for the scheduler. Allocates: call once when entering.</summary>
        public InterestPoint[] GetWorldInterests()
        {
            var result = new InterestPoint[_interests.Length];
            for (int i = 0; i < _interests.Length; i++)
            {
                var p = _interests[i];
                result[i] = new InterestPoint(transform.TransformPoint(p.position), p.standOff, p.kind);
            }
            return result;
        }

        /// <summary>Editor/composer hook: points of interest in chunk space.</summary>
        public void SetInterests(InterestPoint[] interests) => _interests = interests ?? Array.Empty<InterestPoint>();

        public PaletteBinding GetBinding(int index) => _bindings[index];

        /// <summary>Editor/composer hook: the chunk's scatter placements.</summary>
        public void SetScatter(ScatterData scatter) => _scatter = scatter;

        /// <summary>Editor/composer hook. Runtime code never rewires a chunk.</summary>
        public void SetAuthoring(PaletteBinding[] bindings, WanderArea area, Transform eggSpawn, Transform[] animalSpawns,
            InterestPoint[] interests, Transform[] vfxAnchors, Transform[] links)
        {
            _bindings = bindings;
            _wanderArea = area;
            _eggSpawn = eggSpawn;
            _animalSpawns = animalSpawns;
            _interests = interests ?? Array.Empty<InterestPoint>();
            _vfxAnchors = vfxAnchors;
            _links = links;
        }

        /// <summary>Applies palette rows. Returns how many renderers reference an unknown palette (they keep defaults).</summary>
        public int Bind(PaletteTable palettes)
        {
            int missing = 0;
            for (int i = 0; i < _bindings.Length; i++)
            {
                var binding = _bindings[i];
                if (binding.renderer == null)
                    continue;
                if (!palettes.TryGetRow(new ContentId(binding.paletteId), out int row))
                {
                    missing++;
                    continue;
                }
                uint data = EnvironmentVisualPacking.Pack(row, binding.brightness, binding.vertexBrightness, binding.rotor);
                if (binding.renderer is MeshRenderer mesh)
                    mesh.SetShaderUserValue(data);
            }
            return missing;
        }

        /// <summary>Low keeps trees, rocks and landmarks and drops the secondary small props.</summary>
        public void ApplyQuality(QualityTier tier)
        {
            bool keepSecondary = tier != QualityTier.Low;
            for (int i = 0; i < _bindings.Length; i++)
            {
                var binding = _bindings[i];
                if (binding.secondary && binding.renderer != null && binding.renderer.gameObject.activeSelf != keepSecondary)
                    binding.renderer.gameObject.SetActive(keepSecondary);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            foreach (var p in _interests)
            {
                Gizmos.color = p.kind == InterestKind.Eat ? new Color(1f, 0.6f, 0.2f) : new Color(0.3f, 0.7f, 1f);
                Gizmos.DrawWireSphere(transform.TransformPoint(p.position), Mathf.Max(0.05f, p.standOff));
            }
        }
#endif
    }

    /// <summary>Packs the per-renderer Shader User Value read by SH_EnvironmentOpaque and SH_GroundMaster.</summary>
    public static class EnvironmentVisualPacking
    {
        public const uint VertexBrightnessFlag = 1u << 14;
        /// <summary>SH_EnvironmentOpaque spins the renderer around its local Z axis (windmill sails).</summary>
        public const uint RotorFlag = 1u << 15;

        public static uint Pack(int paletteRow, int brightness, bool vertexBrightness = false, bool rotor = false) =>
            (uint)Mathf.Clamp(paletteRow, 0, PaletteTable.MaxRows - 1) | ((uint)Mathf.Clamp(brightness, 0, 15) << 10)
            | (vertexBrightness ? VertexBrightnessFlag : 0u) | (rotor ? RotorFlag : 0u);

        public static bool UsesVertexBrightness(uint packed) => (packed & VertexBrightnessFlag) != 0u;
        public static bool IsRotor(uint packed) => (packed & RotorFlag) != 0u;

        public static int PaletteRow(uint packed) => (int)(packed & 1023u);
        public static int Brightness(uint packed) => (int)((packed >> 10) & 15u);
    }
}
