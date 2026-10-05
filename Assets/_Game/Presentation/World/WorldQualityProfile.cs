using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// World detail per quality tier (M-DEF-QUALITY, doc 07 table): what the biome keeps on Fluide, Équilibrée and
    /// Magnifique. Rendering settings (scale, MSAA, shadows) stay in the URP assets; this holds the world's own
    /// densities and optional layers. DEF_WorldQuality.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/World/World Quality", fileName = "DEF_WorldQuality")]
    public sealed class WorldQualityProfile : ScriptableObject
    {
        [Serializable]
        public struct Tier
        {
            [Tooltip("Share of ground details drawn (grass, flowers, pebbles).")]
            [Range(0f, 1f)] public float scatterDensity;
            [Tooltip("Share of ambient life (butterflies, bees, birds).")]
            [Range(0f, 1f)] public float ambientLifeDensity;
            [Tooltip("Drifting clouds in the sky.")]
            public bool clouds;
            [Tooltip("Height fog strength multiplier (0 = none).")]
            [Range(0f, 1f)] public float heightFog;
            [Tooltip("Weather effect intensity multiplier.")]
            [Range(0f, 1f)] public float weather;
            [Tooltip("Water (SH_WaterLite): 0 depth color + ripples, 1 + foam and sky reflection, 2 + detailed reflection and caustics.")]
            [Range(0, 2)] public int water;
            [Tooltip("Soft sun-ray cards in the light (Magnifique only in doc 07).")]
            public bool sunRays;
            [Tooltip("Ground details thin out with distance from the camera (m): full until start, a quarter at end (LOD distance).")]
            public float scatterFadeStart;
            public float scatterFadeEnd;
        }

        public Tier low = Default(QualityTier.Low);
        public Tier medium = Default(QualityTier.Medium);
        public Tier high = Default(QualityTier.High);

        public Tier Get(QualityTier tier) => tier switch
        {
            QualityTier.Low => low,
            QualityTier.High => high,
            _ => medium,
        };

        /// <summary>
        /// Doc 07 values: Fluide 30% details, 25% life, no clouds, height fog or sun rays, simple water, short LOD
        /// distance; Équilibrée 65%, 60%, clouds, foam and sky reflection; Magnifique everything, longest distance.
        /// </summary>
        public static Tier Default(QualityTier tier) => tier switch
        {
            QualityTier.Low => new Tier
            {
                scatterDensity = 0.3f, ambientLifeDensity = 0.25f, clouds = false, heightFog = 0f, weather = 0.4f,
                water = 0, sunRays = false, scatterFadeStart = 16f, scatterFadeEnd = 26f,
            },
            QualityTier.High => new Tier
            {
                scatterDensity = 1f, ambientLifeDensity = 1f, clouds = true, heightFog = 1f, weather = 1f,
                water = 2, sunRays = true, scatterFadeStart = 30f, scatterFadeEnd = 46f,
            },
            _ => new Tier
            {
                scatterDensity = 0.65f, ambientLifeDensity = 0.6f, clouds = true, heightFog = 1f, weather = 0.7f,
                water = 1, sunRays = false, scatterFadeStart = 22f, scatterFadeEnd = 34f,
            },
        };
    }
}
