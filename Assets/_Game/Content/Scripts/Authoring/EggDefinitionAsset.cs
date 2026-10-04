using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// Authoring asset for one egg type (DEF_Egg_&lt;Biome&gt;_&lt;Tier&gt;). Plain data only, converted into an
    /// immutable <see cref="EggDefinition"/> at boot. All eggs share PF_Egg_Base and MAT_Egg_Master.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Egg Definition", fileName = "DEF_Egg_New")]
    public sealed class EggDefinitionAsset : ScriptableObject
    {
        [Tooltip("Stable source ID, e.g. egg.grassland.basic. Never change it once shipped.")]
        public string sourceId = "egg.biome.basic";
        public string displayKey = "egg.biome.basic.name";
        public string biomeId = "biome.grassland";
        public EggTier tier = EggTier.Basic;
        public string visualKey = "PF_Egg_Base";

        [Header("Palette")]
        public Color shell = new Color(0.96f, 0.92f, 0.85f);
        [Tooltip("Mask R: spots.")] public Color spots = new Color(0.66f, 0.82f, 0.55f);
        [Tooltip("Mask G: leaf band.")] public Color band = new Color(0.49f, 0.71f, 0.39f);
        [Tooltip("Mask B: tiny flowers.")] public Color flowers = new Color(0.95f, 0.65f, 0.73f);
        public Color crack = new Color(0.42f, 0.29f, 0.23f);
        public Color innerShell = new Color(1f, 0.98f, 0.93f);
        [Tooltip("Rim and reveal accent.")] public Color accent = new Color(1f, 0.93f, 0.7f);

        [Header("Mask strengths (0 to 15)")]
        [Range(0, 15)] public int spotsStrength = 15;
        [Range(0, 15)] public int bandStrength;
        [Range(0, 15)] public int flowersStrength = 15;

        public EggDefinition ToRuntime()
        {
            var palette = new Color32[EggDefinition.PaletteSize];
            palette[0] = shell;
            palette[1] = spots;
            palette[2] = band;
            palette[3] = flowers;
            palette[4] = crack;
            palette[5] = innerShell;
            palette[6] = accent;
            palette[7] = shell;
            return new EggDefinition(ContentId.FromSource(sourceId), displayKey, ContentId.FromSource(biomeId), tier, palette,
                (byte)Mathf.Clamp(spotsStrength, 0, 15), (byte)Mathf.Clamp(bandStrength, 0, 15), (byte)Mathf.Clamp(flowersStrength, 0, 15),
                visualKey);
        }
    }
}
