using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// How the basket sits on one species (measured by NutDropPipeline on the bind pose at scale 1, nothing guessed): its
    /// head socket at rest, the lift that keeps the basket's underside over every point of the head, the ears and the tail
    /// within its footprint (at its widest), and the body the falling items keep clear of.
    /// </summary>
    [Serializable]
    public sealed class NutDropSeat
    {
        public string visualKey;

        /// <summary>SocketHeadTop at rest in the root's space (m at scale 1): height and forward offset.</summary>
        public float socketY = 0.3f;

        public float socketZ = 0.1f;

        /// <summary>The basket's pivot this far above the socket (m) keeps its underside over the head and the ears.</summary>
        public float seat = NutDropLayout.DefaultSeat;

        /// <summary>The highest point of the animal over the socket within the basket's footprint (m): a bigger animal lifts the basket by its share.</summary>
        public float above = 0.1f;

        /// <summary>The body seen from the front (m at scale 1): half its width, its front and back, its height.</summary>
        public float halfWidth = 0.15f;

        public float front = 0.2f;
        public float back = -0.2f;
        public float height = 0.4f;
    }

    /// <summary>Everything PF_Activity_NutDrop_Set holds, written by NutDropPipeline (runtime code never rewires it).</summary>
    [Serializable]
    public sealed class NutDropSetData
    {
        [Header("Set")]
        public Camera camera;
        public Light light;

        /// <summary>The playfield's origin: the meadow floor (y = 0) under the field's center, on its plane (z = 0).</summary>
        public Transform origin;

        /// <summary>The floor, the backdrop, the tree and the grass: palette rows bound once at load (like a world chunk).</summary>
        public WorldChunk.PaletteBinding[] bindings = Array.Empty<WorldChunk.PaletteBinding>();

        /// <summary>The spawn line (m above the floor): just under the canopy's leaves over the whole field, measured.</summary>
        public float spawnY = NutDropLayout.DefaultSpawnY;

        /// <summary>The canopy's leaves at their lowest over the field (m above the floor), for the report and the tests.</summary>
        public float canopyBottom = NutDropLayout.DefaultSpawnY + 0.15f;

        public NutDropSeat[] seats = Array.Empty<NutDropSeat>();

        [Header("The basket (ND_Basket), measured in its own space")]
        public MeshRenderer basket;
        public uint basketPalette;

        /// <summary>Outer radius of the rim (its width / 2) and the top of the rim, at the mesh's own scale.</summary>
        public float basketRimRadius = 0.5f;

        public float basketRimTop = 0.5f;

        /// <summary>The lowest point of the basket (its foot), at the mesh's own scale.</summary>
        public float basketBottom;

        /// <summary>The inside: its radius at the rim, and the floor of the weave at 9 radii from its axis to that radius (heights).</summary>
        public float basketInnerRadius = 0.45f;

        public float[] basketInner = Array.Empty<float>();

        [Header("Items (pooled, built once): one mesh per NutDropItemKind (index = kind)")]
        public Mesh[] kindMeshes = Array.Empty<Mesh>();

        /// <summary>Each mesh's bounding radius around its pivot (the pivot is the item's center).</summary>
        public float[] kindRadii = Array.Empty<float>();

        public uint[] kindPalettes = Array.Empty<uint>();

        /// <summary>The kinds drawn with the gold material (golden nut, golden shower).</summary>
        public bool[] kindGold = Array.Empty<bool>();

        public Material propsMaterial;
        public Material goldMaterial;
        public MeshRenderer[] items = Array.Empty<MeshRenderer>();

        /// <summary>H-VFX-ACT-003-01: the round glints on the basket's rim (4 a catch, emitted by the stage; the shared additive material of CommonAnimals).</summary>
        [Header("Effects")]
        public ParticleSystem[] glints = Array.Empty<ParticleSystem>();

        /// <summary>H-VFX-ACT-003-02: the golden nut's trail of soft dots (emitted by the stage).</summary>
        public ParticleSystem trail;

        /// <summary>H-VFX-ACT-003-04: the soft ring around the basket when a special is caught.</summary>
        public ParticleSystem ring;

        /// <summary>A heart over the head on every 5-catch step.</summary>
        public ParticleSystem hearts;

        /// <summary>H-VFX-ACT-003-03: the small leaves of a pinecone bonk (kit leaf mesh).</summary>
        public MeshRenderer[] leaves = Array.Empty<MeshRenderer>();

        public uint leafPalette;

        /// <summary>The leaf's scale on the set and its radius there (m).</summary>
        public float leafScale = 1f;

        public float leafRadius = 0.04f;

        /// <summary>Lying flat, the leaf's pivot this far over the meadow (its underside on it, never in it).</summary>
        public float leafLift = 0.004f;

        [Header("Light and sky while the set shows (the Prairie's warm day under the hazel)")]
        public Color ambientTop = new Color(0.66f, 0.72f, 0.82f);
        public Color ambientEquator = new Color(0.72f, 0.7f, 0.62f);
        public Color ambientBottom = new Color(0.44f, 0.46f, 0.36f);
        public Color skyTop = new Color(0.55f, 0.74f, 0.93f);
        public Color skyHorizon = new Color(1f, 0.9f, 0.78f);
        public Color skyBottom = new Color(0.93f, 0.86f, 0.74f);
        public Color sunGlowTint = new Color(1f, 0.88f, 0.68f);
        [Range(0f, 1f)] public float sunGlow = 0.35f;
        public Color cloudLight = new Color(1f, 0.98f, 0.94f);
        public Color cloudShade = new Color(0.86f, 0.82f, 0.86f);
        [Range(0f, 1f)] public float cloudCoverage = 0.3f;
    }

    /// <summary>
    /// Root of PF_Activity_NutDrop_Set (T40, doc 01 Contenu S-ACT-003): the set's references and its measured numbers. No
    /// Update: the nut drop stage drives it. Binds its palette rows once, like a world chunk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NutDropSetView : MonoBehaviour
    {
        [SerializeField] private NutDropSetData _data = new NutDropSetData();

        public NutDropSetData Data => _data;

        /// <summary>
        /// Set by the stage: called once per frame after the animation (the set's only LateUpdate), so the basket sits on the
        /// head as it is drawn this frame. Inactive set: nothing runs.
        /// </summary>
        public Action LateTick { get; set; }

        private void LateUpdate() => LateTick?.Invoke();

        /// <summary>Editor/pipeline hook.</summary>
        public void SetAuthoring(NutDropSetData data) => _data = data ?? new NutDropSetData();

        /// <summary>The measured seat of a species (its visual key), or null.</summary>
        public NutDropSeat Seat(string visualKey)
        {
            var seats = _data.seats;
            if (seats == null || string.IsNullOrEmpty(visualKey))
                return null;
            for (int i = 0; i < seats.Length; i++)
                if (seats[i] != null && string.Equals(seats[i].visualKey, visualKey, StringComparison.Ordinal))
                    return seats[i];
            return null;
        }

        /// <summary>Palette rows of the set's renderers. Returns how many palettes are unknown (they keep defaults).</summary>
        public int Bind(PaletteTable palettes)
        {
            if (palettes == null)
                return 0;
            int missing = 0;
            var bindings = _data.bindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding.renderer == null)
                    continue;
                if (!palettes.TryGetRow(new ContentId(binding.paletteId), out int row))
                {
                    missing++;
                    continue;
                }
                if (binding.renderer is MeshRenderer mesh)
                    mesh.SetShaderUserValue(EnvironmentVisualPacking.Pack(row, binding.brightness, binding.vertexBrightness, binding.rotor));
            }
            return missing;
        }
    }
}
