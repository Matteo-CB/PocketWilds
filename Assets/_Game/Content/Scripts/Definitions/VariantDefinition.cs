#nullable enable
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// Which authored pattern mask a variant uses. Masks are painted per species in Blender and
    /// exported as UV channels: A = uv0.x, B = uv0.y, C = uv1.x, D = uv1.y. Stable byte values.
    /// </summary>
    public enum PatternChannel : byte
    {
        None = 0,
        MaskA = 1,
        MaskB = 2,
        MaskC = 3,
        MaskD = 4,
    }

    /// <summary>
    /// One visual variant of a species: a palette plus an optional pattern. Same mesh, same material.
    /// Palette order: primary, secondary, belly, detail, eye, pattern, then two reserved slots.
    /// </summary>
    public sealed class VariantDefinition : IContentDefinition
    {
        public const int PaletteSize = 8;

        public VariantDefinition(ContentId id, ContentId species, string displayKey, RarityTier rarity,
            Color32[] palette, PatternChannel patternChannel, byte patternStrength, bool isSecret = false)
        {
            IsSecret = isSecret;
            Id = id;
            Species = species;
            DisplayKey = displayKey;
            Rarity = rarity;
            Palette = palette;
            PatternChannel = patternChannel;
            PatternStrength = patternStrength;
        }

        public ContentId Id { get; }
        public ContentId Species { get; }
        public string DisplayKey { get; }
        public RarityTier Rarity { get; }

        /// <summary>Secret variant (Moon Rabbit...): purely cosmetic, never targeted by the variant pity.</summary>
        public bool IsSecret { get; }

        /// <summary>Exactly <see cref="PaletteSize"/> colors.</summary>
        public Color32[] Palette { get; }

        public PatternChannel PatternChannel { get; }

        /// <summary>0 to 15.</summary>
        public byte PatternStrength { get; }
    }
}
