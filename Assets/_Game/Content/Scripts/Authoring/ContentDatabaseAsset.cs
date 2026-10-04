using System.Collections.Generic;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// Root list of shipped content (DEF_ContentDatabase). Builds the runtime registry once at boot.
    /// Will move to Addressables groups per biome; the registry contract stays the same.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Content Database", fileName = "DEF_ContentDatabase")]
    public sealed class ContentDatabaseAsset : ScriptableObject
    {
        public List<SpeciesDefinitionAsset> species = new List<SpeciesDefinitionAsset>();

        [Tooltip("Retired source IDs. They can never be reused.")]
        public List<string> tombstones = new List<string>();

        public ContentRegistryBuilder CreateBuilder()
        {
            var builder = new ContentRegistryBuilder();
            foreach (var id in tombstones)
                builder.AddTombstone(id);
            foreach (var asset in species)
            {
                if (asset == null)
                    continue;
                builder.AddSpecies(asset.ToRuntime(), asset.sourceId);
                foreach (var variant in asset.variants)
                    builder.AddVariant(asset.ToRuntime(variant), variant.sourceId);
            }
            return builder;
        }
    }
}
