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
        public List<EggDefinitionAsset> eggs = new List<EggDefinitionAsset>();
        public List<PaletteSetAsset> paletteSets = new List<PaletteSetAsset>();
        public List<BiomeDefinitionAsset> biomes = new List<BiomeDefinitionAsset>();

        [Tooltip("Store products, offers and grantable items (T28).")]
        public List<ShopCatalogAsset> shopCatalogs = new List<ShopCatalogAsset>();

        [Tooltip("T49: the shipped events (their dates come from the calendar, their content from the shop catalogs and species).")]
        public List<EventDefinitionAsset> events = new List<EventDefinitionAsset>();

        [Tooltip("Player-facing text, one table per language (LOC_en first: it is the fallback).")]
        public List<LocalizationTableAsset> localization = new List<LocalizationTableAsset>();

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
            foreach (var egg in eggs)
            {
                if (egg != null)
                    builder.AddEgg(egg.ToRuntime(), egg.sourceId);
            }
            foreach (var biome in biomes)
            {
                if (biome != null)
                    builder.AddBiome(biome.ToRuntime(), biome.sourceId);
            }
            foreach (var set in paletteSets)
            {
                if (set == null)
                    continue;
                foreach (var entry in set.palettes)
                    builder.AddPalette(entry.ToRuntime(), entry.sourceId);
            }
            foreach (var catalog in shopCatalogs)
            {
                if (catalog != null)
                    catalog.AddTo(builder);
            }
            foreach (var e in events)
            {
                if (e != null)
                    builder.AddEvent(e.ToRuntime(), e.sourceId);
            }
            return builder;
        }
    }
}
