#nullable enable
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>Egg families from the Eggs prompts (C-EGG-001..015). Stable byte values.</summary>
    public enum EggTier : byte
    {
        Basic = 0,
        Focus = 1,
        Discovery = 2,
        Event = 3,
        Mythic = 4,
    }

    /// <summary>
    /// One egg type. Every egg shares the same mesh, mask texture and material: identity comes from
    /// the palette and how strongly each mask channel shows (Eggs prompts: no unique topology).
    /// Palette order: shell, mask R (spots), mask G (leaf band), mask B (flowers), crack, inner shell,
    /// accent, reserved. What hatches (drop table) arrives with HatchService (T17).
    /// </summary>
    public sealed class EggDefinition : IContentDefinition
    {
        public const int PaletteSize = 8;

        public EggDefinition(ContentId id, string displayKey, ContentId biome, EggTier tier, Color32[] palette,
            byte maskStrengthR, byte maskStrengthG, byte maskStrengthB, string visualKey)
        {
            Id = id;
            DisplayKey = displayKey;
            Biome = biome;
            Tier = tier;
            Palette = palette;
            MaskStrengthR = maskStrengthR;
            MaskStrengthG = maskStrengthG;
            MaskStrengthB = maskStrengthB;
            VisualKey = visualKey;
        }

        public ContentId Id { get; }
        public string DisplayKey { get; }
        public ContentId Biome { get; }
        public EggTier Tier { get; }

        /// <summary>Exactly <see cref="PaletteSize"/> colors.</summary>
        public Color32[] Palette { get; }

        /// <summary>0 to 15 each.</summary>
        public byte MaskStrengthR { get; }
        public byte MaskStrengthG { get; }
        public byte MaskStrengthB { get; }

        /// <summary>Key of the shared egg view prefab.</summary>
        public string VisualKey { get; }
    }
}
