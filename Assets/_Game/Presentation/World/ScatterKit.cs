using System;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// The scatter vocabulary of a biome (DEF_ScatterKit_&lt;Biome&gt;): grass tufts, flowers, pebbles, petals...
    /// Every kind shares the biome's foliage material (SH_FoliageWind); kinds differ by mesh, palette and wind.
    /// Chunks only store placements (ScatterData) that index these kinds.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/World/Scatter Kit", fileName = "DEF_ScatterKit_Biome")]
    public sealed class ScatterKit : ScriptableObject
    {
        [Serializable]
        public struct Kind
        {
            [Tooltip("Stable id, e.g. grass.tuft_m_a, flower.daisy_white, detail.pebble_a.")]
            public string id;
            public Mesh mesh;
            [Tooltip("ContentId value of palette.<biome>.<name>.")]
            public uint paletteId;
            [Tooltip("Wind multiplier: 1 for grass, less for flowers, 0 for pebbles.")]
            [Range(0f, 2f)] public float windScale;
            [Tooltip("Share kept even on low tiers: flowers carry the biome's charm, grass is detail (doc 07).")]
            [Range(0f, 1f)] public float minDensity;
        }

        [Tooltip("MAT_Foliage_<Biome> (PocketWilds/FoliageWind), GPU instancing on.")]
        public Material material;
        public Kind[] kinds = Array.Empty<Kind>();

        public int IndexOf(string id)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                if (kinds[i].id == id)
                    return i;
            }
            return -1;
        }
    }
}
