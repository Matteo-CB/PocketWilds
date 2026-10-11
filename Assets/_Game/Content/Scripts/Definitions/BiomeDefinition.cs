#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Content
{
    /// <summary>T49B a zone's role in its biome (doc 02 Améliorations, section 4.2). Same values as Simulation's ZoneRole.</summary>
    public enum BiomeZoneRole : byte
    {
        Arrival = 0,
        Expansion = 1,
        Passage = 2,
    }

    /// <summary>
    /// T49B one zone of a biome, in opening order: its chunk name (« start », « meadow », the chunk address is
    /// chunk.{biome key}.{chunk}) and role. The zone's bit in the save is its position in <see cref="BiomeDefinition.Zones"/>;
    /// the expansion index counts the expansions from 1 (0 for the arrival and the passage).
    /// </summary>
    public readonly struct BiomeZone
    {
        public readonly string Chunk;
        public readonly BiomeZoneRole Role;
        public readonly int ExpansionIndex;

        public BiomeZone(string chunk, BiomeZoneRole role, int expansionIndex)
        {
            Chunk = chunk ?? string.Empty;
            Role = role;
            ExpansionIndex = expansionIndex;
        }
    }

    /// <summary>
    /// Biome data (M-DEF-BIOME): identity, player-facing name key, order and, since T49B, the short key (« grassland »,
    /// « forest »: chunk, zone and Addressables names), the mesh and decoration prefixes (GL, FO) and the zones in opening
    /// order. The Codex groups species by biome in this order; ProgressionConfig.Zones is built from the zones of every
    /// biome at boot (Biome Wizard, ED-004).
    /// </summary>
    public sealed class BiomeDefinition : IContentDefinition
    {
        public BiomeDefinition(ContentId id, string displayKey, int order)
            : this(id, displayKey, order, string.Empty, string.Empty, null)
        {
        }

        public BiomeDefinition(ContentId id, string displayKey, int order, string key, string prefix, IReadOnlyList<BiomeZone>? zones)
        {
            Id = id;
            DisplayKey = displayKey;
            Order = order;
            Key = key ?? string.Empty;
            Prefix = prefix ?? string.Empty;
            Zones = zones ?? Array.Empty<BiomeZone>();
        }

        public ContentId Id { get; }

        /// <summary>Localization key, e.g. "biome.grassland.name".</summary>
        public string DisplayKey { get; }

        /// <summary>Progression order (Grassland 0, Forest 1, ...).</summary>
        public int Order { get; }

        /// <summary>T49B the short key: « grassland » for biome.grassland (chunk.grassland.start, zone.grassland.start).</summary>
        public string Key { get; }

        /// <summary>T49B the two-letter prefix of meshes and decorations: GL (GL_Tree_A, E-DEC-GL-01), FO for the Forest.</summary>
        public string Prefix { get; }

        /// <summary>T49B the zones in opening order: the arrival first, then the expansions, the passage (if any) last.</summary>
        public IReadOnlyList<BiomeZone> Zones { get; }
    }
}
