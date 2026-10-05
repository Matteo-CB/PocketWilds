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

        /// <summary>Doc 07 values: Fluide 30% details, 25% life, no clouds or height fog; Équilibrée 65%, 60%.</summary>
        public static Tier Default(QualityTier tier) => tier switch
        {
            QualityTier.Low => new Tier { scatterDensity = 0.3f, ambientLifeDensity = 0.25f, clouds = false, heightFog = 0f, weather = 0.4f },
            QualityTier.High => new Tier { scatterDensity = 1f, ambientLifeDensity = 1f, clouds = true, heightFog = 1f, weather = 1f },
            _ => new Tier { scatterDensity = 0.65f, ambientLifeDensity = 0.6f, clouds = true, heightFog = 1f, weather = 0.7f },
        };
    }
}
