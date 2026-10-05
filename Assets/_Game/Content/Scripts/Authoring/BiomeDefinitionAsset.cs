using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>Authoring asset for a biome (DEF_Biome_Grassland). Runtime data: <see cref="BiomeDefinition"/>.</summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Biome Definition", fileName = "DEF_Biome_New")]
    public sealed class BiomeDefinitionAsset : ScriptableObject
    {
        public string sourceId = "biome.new";
        public string displayKey = "biome.new.name";

        [Tooltip("Progression order: Grassland 0, Forest 1, Lake 2...")]
        public int order;

        public BiomeDefinition ToRuntime() => new BiomeDefinition(ContentId.FromSource(sourceId), displayKey, order);
    }
}
