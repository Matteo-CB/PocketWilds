#nullable enable
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>Which mesh channel carries the variant pattern mask (see SH_AnimalMaster).</summary>
    public enum PatternChannel : byte
    {
        None = 0,
        Uv1X = 1,
        Uv1Y = 2,
        Uv2X = 3,
        Uv2Y = 4,
    }

    /// <summary>
    /// One visual variant of a species: a palette plus an optional pattern. Same mesh, same material.
    /// Palette order: primary, secondary, belly, detail, eye, pattern, then two reserved slots.
    /// </summary>
    public sealed class VariantDefinition : IContentDefinition
    {
        public const int PaletteSize = 8;

        public VariantDefinition(ContentId id, ContentId species, string displayKey, RarityTier rarity,
            Color32[] palette, PatternChannel patternChannel, byte patternStrength)
        {
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

        /// <summary>Exactly <see cref="PaletteSize"/> colors.</summary>
        public Color32[] Palette { get; }

        public PatternChannel PatternChannel { get; }

        /// <summary>0 to 15.</summary>
        public byte PatternStrength { get; }
    }
}
