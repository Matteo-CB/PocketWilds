using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// A biome's world palettes (DEF_Palettes_&lt;Biome&gt;): ground, trees, rocks, grass, flowers.
    /// Recoloring a prop or making a seasonal variant is a data change, never a new material.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Palette Set", fileName = "DEF_Palettes_New")]
    public sealed class PaletteSetAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable source ID, e.g. palette.grassland.tree. Never change it once shipped.")]
            public string sourceId = "palette.biome.name";
            public Color baseColor = Color.white;
            [Tooltip("Vertex color R region.")] public Color regionR = Color.white;
            [Tooltip("Vertex color G region.")] public Color regionG = Color.white;
            [Tooltip("Vertex color B region.")] public Color regionB = Color.white;
            [Tooltip("Vertex color A region.")] public Color regionA = Color.white;
            [Tooltip("Baked occlusion (UV0.x) blends toward this tint.")] public Color occlusion = new Color(0.4f, 0.45f, 0.4f);
            public Color accent = Color.white;

            public PaletteDefinition ToRuntime()
            {
                var colors = new Color32[PaletteDefinition.PaletteSize];
                colors[0] = baseColor;
                colors[1] = regionR;
                colors[2] = regionG;
                colors[3] = regionB;
                colors[4] = regionA;
                colors[5] = occlusion;
                colors[6] = accent;
                colors[7] = baseColor;
                return new PaletteDefinition(ContentId.FromSource(sourceId), colors);
            }
        }

        public List<Entry> palettes = new List<Entry>();

        public bool TryGet(string sourceId, out Entry entry)
        {
            foreach (var e in palettes)
            {
                if (e != null && e.sourceId == sourceId)
                {
                    entry = e;
                    return true;
                }
            }
            entry = null;
            return false;
        }
    }
}
