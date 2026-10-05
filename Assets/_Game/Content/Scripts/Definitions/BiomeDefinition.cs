#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Content
{
    /// <summary>
    /// Minimal biome data (M-DEF-BIOME): identity, player-facing name key and order. The Codex groups
    /// species by biome in this order; unlock rules and chunk references join with world progression.
    /// </summary>
    public sealed class BiomeDefinition : IContentDefinition
    {
        public BiomeDefinition(ContentId id, string displayKey, int order)
        {
            Id = id;
            DisplayKey = displayKey;
            Order = order;
        }

        public ContentId Id { get; }

        /// <summary>Localization key, e.g. "biome.grassland.name".</summary>
        public string DisplayKey { get; }

        /// <summary>Progression order (Grassland 0, Forest 1, ...).</summary>
        public int Order { get; }
    }
}
