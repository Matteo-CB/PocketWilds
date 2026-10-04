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
            [Tooltip("Small decorative prop removed on Low quality (Biomes doc: -35 to 50% density).")]
            public bool secondary;
        }

        public const int NeutralBrightness = 8;

        [SerializeField] private PaletteBinding[] _bindings = Array.Empty<PaletteBinding>();
        [SerializeField] private WanderArea _wanderArea = new WanderArea(Vector3.zero, 4f);
        [SerializeField] private Transform _eggSpawn;
        [SerializeField] private Transform[] _animalSpawns = Array.Empty<Transform>();
        [SerializeField] private Transform[] _interestPoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] _vfxAnchors = Array.Empty<Transform>();
        [SerializeField] private Transform[] _links = Array.Empty<Transform>();

        public int BindingCount => _bindings.Length;
        public Transform EggSpawn => _eggSpawn;
        public Transform[] AnimalSpawns => _animalSpawns;
        public Transform[] InterestPoints => _interestPoints;
        public Transform[] VfxAnchors => _vfxAnchors;
        public Transform[] Links => _links;

        /// <summary>Walkable disk in world space (simple region data, no NavMesh).</summary>
        public WanderArea WanderArea => new WanderArea(transform.TransformPoint(_wanderArea.Center), _wanderArea.Radius);

        public PaletteBinding GetBinding(int index) => _bindings[index];

        /// <summary>Editor/composer hook. Runtime code never rewires a chunk.</summary>
        public void SetAuthoring(PaletteBinding[] bindings, WanderArea area, Transform eggSpawn, Transform[] animalSpawns,
            Transform[] interestPoints, Transform[] vfxAnchors, Transform[] links)
        {
            _bindings = bindings;
            _wanderArea = area;
            _eggSpawn = eggSpawn;
            _animalSpawns = animalSpawns;
            _interestPoints = interestPoints;
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
                uint data = EnvironmentVisualPacking.Pack(row, binding.brightness);
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
    }

    /// <summary>Packs the per-renderer Shader User Value read by SH_EnvironmentOpaque and SH_GroundMaster.</summary>
    public static class EnvironmentVisualPacking
    {
        public static uint Pack(int paletteRow, int brightness) =>
            (uint)Mathf.Clamp(paletteRow, 0, PaletteTable.MaxRows - 1) | ((uint)Mathf.Clamp(brightness, 0, 15) << 10);

        public static int PaletteRow(uint packed) => (int)(packed & 1023u);
        public static int Brightness(uint packed) => (int)((packed >> 10) & 15u);
    }
}
