using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// DEF_VfxCatalog: the feedback effect prefabs with their pool sizes, and the VFX quality profile (doc 07, VFX
    /// QUALITY DATA). Particle counts follow the device tier: Low keeps about half, High a quarter more, never double.
    /// Built by VfxPipeline (Pocket Wilds/VFX/Build Feedback VFX).
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Visuals/VFX Catalog", fileName = "DEF_VfxCatalog")]
    public sealed class VfxCatalog : ScriptableObject
    {
        [Serializable]
        public struct Effect
        {
            public VfxId id;
            public ParticleSystem prefab;
            [Min(1)] public int softCapacity;
            [Min(1)] public int hardCapacity;
            [Min(0)] public int warmup;
        }

        public Effect[] effects = Array.Empty<Effect>();

        [Header("Quality (count multipliers per device tier)")]
        [Range(0.3f, 1f)] public float lowCountScale = 0.5f;
        [Range(1f, 1.35f)] public float highCountScale = 1.25f;

        [Tooltip("Effects playing at once; one more is skipped, never a hitch.")]
        [Min(4)] public int maxPlaying = 16;

        public float CountScale(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low: return lowCountScale;
                case QualityTier.High: return highCountScale;
                default: return 1f;
            }
        }

        public bool TryGet(VfxId id, out Effect effect)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].id == id && effects[i].prefab != null)
                {
                    effect = effects[i];
                    return true;
                }
            }
            effect = default;
            return false;
        }

        public ParticleSystem Prefab(VfxId id) => TryGet(id, out var effect) ? effect.prefab : null;
    }
}
