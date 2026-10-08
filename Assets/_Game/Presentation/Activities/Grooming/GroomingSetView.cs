using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>One biome theme of the set (doc 01 Contenu, S-ACT-001 « Thèmes par biome »): its debris, its colors.</summary>
    [Serializable]
    public sealed class GroomingTheme
    {
        /// <summary>Source id of the theme's biome (biome.grassland).</summary>
        public string biome;

        /// <summary>The debris kinds of the theme (Prairie: grass blades, daisy petals, clover leaves, seeds) and their palettes.</summary>
        public Mesh[] debrisMeshes = Array.Empty<Mesh>();

        public uint[] debrisPalettes = Array.Empty<uint>();

        /// <summary>Half thickness of each kind (m): the lift that keeps it on the fur, never in it.</summary>
        public float[] debrisLift = Array.Empty<float>();
    }

    /// <summary>Everything PF_Activity_Grooming_Set holds, written by GroomingPipeline (runtime code never rewires it).</summary>
    [Serializable]
    public sealed class GroomingSetData
    {
        [Header("Set")]
        public Camera camera;
        public Light light;

        /// <summary>The cushion's top center: the animal's feet stand here (y = the top surface).</summary>
        public Transform animalAnchor;

        /// <summary>Radius of the cushion's flat top (m), measured on the mound mesh: the feet never leave it.</summary>
        public float moundTopRadius = 0.45f;

        /// <summary>Mound, cushion, hills and flowers: palette rows bound once at load (like a world chunk).</summary>
        public WorldChunk.PaletteBinding[] bindings = Array.Empty<WorldChunk.PaletteBinding>();

        public GroomingBodyCatalog bodies;

        public GroomingTheme[] themes = Array.Empty<GroomingTheme>();

        [Header("Props (pooled, built once)")]
        public Transform props;

        public MeshRenderer brush;
        public Material brushMaterial;
        public Material goldMaterial;
        public uint brushPalette;
        public uint goldPalette;

        /// <summary>Height of the brush's bristles (m): its contact point glides this far above the fur.</summary>
        public float brushHover = 0.012f;

        /// <summary>The golden brush lying on its zone until taken.</summary>
        public MeshRenderer goldenBrush;

        public MeshRenderer bow;

        /// <summary>Pink, sky blue, cream (GroomingBow order).</summary>
        public uint[] bowPalettes = Array.Empty<uint>();

        /// <summary>The bow's width at scale 1 (m), measured on GR_Bow.</summary>
        public float bowWidth = 0.08f;

        /// <summary>Soap bubbles (SH_SoapBubble): their user value is the sheen's lean (0 neutral, 1 pink, 2 sky).</summary>
        public MeshRenderer[] bubbles = Array.Empty<MeshRenderer>();
        public uint bubbleTint;

        /// <summary>The soap bubble's radius at scale 1 (m), measured on GR_SoapBubble.</summary>
        public float bubbleRadius = 0.5f;

        public MeshRenderer[] debris = Array.Empty<MeshRenderer>();
        public Mesh burrMesh;
        public uint burrPalette;
        public float burrLift = 0.012f;

        /// <summary>Foam bubbles (SH_SoapBubble too), their sheens taken in turn.</summary>
        public MeshRenderer[] foam = Array.Empty<MeshRenderer>();
        public uint[] foamTints = Array.Empty<uint>();

        /// <summary>H-VFX-ACT-001-03 round glints (additive, the feedback atlas), pooled.</summary>
        public ParticleSystem[] glints = Array.Empty<ParticleSystem>();

        [Header("HUD icons (agent GI, Art/UI/Icons)")]
        // The bow and golden brush icons are borrowed from the HUD (IActivityHudView.Icon): the set's bundle never copies
        // the UI atlas.

        [Header("Light and sky while the set shows (the Prairie's warm day)")]
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
        [Range(0f, 1f)] public float cloudCoverage = 0.32f;
    }

    /// <summary>
    /// Root of PF_Activity_Grooming_Set (T38, doc 01 Contenu S-ACT-001): the set's references and its measured numbers. No
    /// Update: the grooming stage drives it. Binds its palette rows once, like a world chunk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroomingSetView : MonoBehaviour
    {
        [SerializeField] private GroomingSetData _data = new GroomingSetData();

        public GroomingSetData Data => _data;

        /// <summary>
        /// Set by the grooming stage: called once per frame after the animation (the set's only LateUpdate), so the debris,
        /// the brush and the bow sit on the fur as it is drawn this frame. Inactive set: nothing runs.
        /// </summary>
        public Action LateTick { get; set; }

        private void LateUpdate() => LateTick?.Invoke();

        /// <summary>Editor/pipeline hook.</summary>
        public void SetAuthoring(GroomingSetData data) => _data = data ?? new GroomingSetData();

        /// <summary>The theme of a biome, else the first one (Prairie).</summary>
        public GroomingTheme Theme(ContentId biome)
        {
            var themes = _data.themes;
            if (themes == null || themes.Length == 0)
                return null;
            for (int i = 0; i < themes.Length; i++)
                if (themes[i] != null && ContentId.FromSource(themes[i].biome) == biome)
                    return themes[i];
            return themes[0];
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

        /// <summary>The packed user value of a palette (row and neutral brightness); 0 when unknown.</summary>
        public static uint Packed(PaletteTable palettes, uint paletteId, int brightness = WorldChunk.NeutralBrightness)
        {
            if (palettes == null || paletteId == 0 || !palettes.TryGetRow(new ContentId(paletteId), out int row))
                return EnvironmentVisualPacking.Pack(0, brightness);
            return EnvironmentVisualPacking.Pack(row, brightness);
        }
    }
}
