using System;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>T48B what drifts in the air of a season: round soft petals, rounded leaves, fine round snow.</summary>
    public enum SeasonDriftKind : byte
    {
        None = 0,
        Petals = 1,
        Leaves = 2,
        Snow = 3,
    }

    /// <summary>
    /// T48B the four looks of the refuge (DEF_SeasonLooks, built by the editor's SeasonLookPipeline from the base world palettes
    /// with hand-tuned transforms, then editable by hand). Only vegetation and ground palette rows change; rocks, water, wood,
    /// props, animals and UI never do. Soleil is the validated look: no row, neutral light. A season change rewrites the changed
    /// rows of the global palette texture, swaps an atmosphere tint, the weather rules and the drift (one instanced draw).
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/World/Season Looks", fileName = "DEF_SeasonLooks")]
    public sealed class SeasonLookSet : ScriptableObject
    {
        /// <summary>The 8 colors a world palette wears during a season (same layout as PaletteDefinition).</summary>
        [Serializable]
        public sealed class PaletteRow
        {
            [Tooltip("palette.<biome>.<name> of a vegetation or ground palette.")]
            public string paletteId;
            public Color32[] colors = new Color32[8];
        }

        [Serializable]
        public sealed class Look
        {
            public RefugeSeason season;
            [Tooltip("Vegetation and ground rows of this season. Rows absent here wear their base colors.")]
            public PaletteRow[] palettes = Array.Empty<PaletteRow>();

            [Header("Light (multiplied over the mood of the hour, before the rain)")]
            public Color sunTint = Color.white;
            [Range(0.6f, 1.4f)] public float sunIntensity = 1f;
            public Color ambientTint = Color.white;
            public Color skyTint = Color.white;
            public Color cloudTint = Color.white;
            [Tooltip("Morning sky color (Givre: pink) and its weight at the height of the morning.")]
            public Color morningSky = Color.white;
            [Range(0f, 1f)] public float morningSkyWeight;
            public Color fogTint = Color.white;
            [Range(0f, 1f)] public float fogTintWeight;
            [Tooltip("Height fog boost in the morning (Récolte: morning mist).")]
            [Range(0f, 1f)] public float morningMist;

            [Header("Weather")]
            [Tooltip("Showers come this many times more often (Éveil 1.8).")]
            [Range(0.25f, 4f)] public float showerFrequency = 1f;
            public bool rain = true;
            public bool rainbow = true;

            [Header("Ambient")]
            public SeasonDriftKind drift;
            public Color driftColorA = Color.white;
            public Color driftColorB = Color.white;
            [Tooltip("Share of the drift instances (the tier scales it further).")]
            [Range(0f, 1f)] public float driftAmount;
            [Tooltip("Diameter of one petal, leaf or flake (m).")]
            [Range(0.01f, 0.2f)] public float driftSize = 0.06f;
            [Tooltip("Fall speed (m/s).")]
            [Range(0.05f, 3f)] public float driftFall = 0.5f;
            [Tooltip("Side sway (m).")]
            [Range(0f, 1.5f)] public float driftSway = 0.4f;
            [Tooltip("Fireflies at evening and night (Soleil), steered by AmbientLife around every volume.")]
            public bool fireflies;

            /// <summary>The light part, as plain values for WorldAtmosphere.</summary>
            public SeasonTint Tint => new SeasonTint
            {
                Active = true,
                Sun = sunTint,
                SunIntensity = sunIntensity,
                Ambient = ambientTint,
                Sky = skyTint,
                Cloud = cloudTint,
                MorningSky = morningSky,
                MorningSkyWeight = morningSkyWeight,
                Fog = fogTint,
                FogWeight = fogTintWeight,
                MorningMist = morningMist,
            };
        }

        public Look[] looks = Array.Empty<Look>();

        [Header("Drift: one instanced draw, motion in the vertex shader")]
        [Tooltip("MSH_SeasonQuad: a unit quad (corners at plus or minus 0.5), shared with the fireflies.")]
        public Mesh quadMesh;
        [Tooltip("MAT_SeasonDrift (PocketWilds/SeasonDrift), GPU instancing on.")]
        public Material driftMaterial;
        [Tooltip("Instances on Magnifique (Équilibrée -30 %, Fluide -60 % through WorldQualityProfile.weather).")]
        [Range(0, SeasonDrift.MaxInstances)] public int maxDrift = SeasonDrift.MaxInstances;
        [Tooltip("Drift box around the view: x and z extent, height (m). Its floor stays above the ground.")]
        public Vector3 driftBox = new Vector3(14f, 5f, 14f);

        [Header("Fireflies (Soleil evenings)")]
        [Tooltip("MAT_Firefly (PocketWilds/Firefly), GPU instancing on.")]
        public Material fireflyMaterial;
        [Tooltip("Spawned at the butterflies' anchors; behaviour Firefly. The mesh is the quad.")]
        public AmbientLifeKit.Species fireflies;

        /// <summary>The look of a season; null when none is authored (the base look shows).</summary>
        public Look Get(RefugeSeason season)
        {
            for (int i = 0; i < looks.Length; i++)
            {
                if (looks[i] != null && looks[i].season == season)
                    return looks[i];
            }
            return null;
        }
    }
}
