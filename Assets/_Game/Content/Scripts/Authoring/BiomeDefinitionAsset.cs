using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// Authoring asset for a biome (DEF_Biome_Grassland), made by the Biome Wizard (ED-004). Runtime data:
    /// <see cref="BiomeDefinition"/>. The zones are listed in opening order: the arrival first, the expansions, the passage last.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Biome Definition", fileName = "DEF_Biome_New")]
    public sealed class BiomeDefinitionAsset : ScriptableObject
    {
        [Serializable]
        public sealed class ZoneRow
        {
            [Tooltip("Chunk name, lower case letters and digits (« start », « meadow »): chunk.{key}.{chunk}, zone.{key}.{chunk}.")]
            public string chunk = string.Empty;

            public BiomeZoneRole role = BiomeZoneRole.Expansion;
        }

        public string sourceId = "biome.new";
        public string displayKey = "biome.new.name";

        [Tooltip("Progression order: Grassland 0, Forest 1, Lake 2...")]
        public int order;

        [Tooltip("T49B short key (« grassland »): chunk, zone, palette and Addressables names.")]
        public string key = string.Empty;

        [Tooltip("T49B two-letter prefix of meshes and decorations (GL, FO).")]
        public string prefix = string.Empty;

        [Tooltip("T49B zones in opening order (the zone's save bit is its position).")]
        public List<ZoneRow> zones = new List<ZoneRow>();

        public BiomeDefinition ToRuntime()
        {
            var rows = new BiomeZone[zones.Count];
            int expansion = 0;
            for (int i = 0; i < zones.Count; i++)
            {
                var row = zones[i];
                int index = row != null && row.role == BiomeZoneRole.Expansion ? ++expansion : 0;
                rows[i] = new BiomeZone(row?.chunk ?? string.Empty, row?.role ?? BiomeZoneRole.Expansion, index);
            }
            return new BiomeDefinition(ContentId.FromSource(sourceId), displayKey, order, key, prefix, rows);
        }
    }
}
