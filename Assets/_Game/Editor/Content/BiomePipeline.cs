using System;
using System.IO;
using PocketWilds.Content;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Content
{
    /// <summary>Biome definitions shipped with the vertical slice (M-DEF-BIOME). Grassland only for now.</summary>
    public static class BiomePipeline
    {
        public const string Folder = "Assets/_Game/Content/Biomes";
        public const string GrasslandPath = Folder + "/DEF_Biome_Grassland.asset";
        public const string ContentDatabasePath = "Assets/_Game/Content/DEF_ContentDatabase.asset";

        [MenuItem("Pocket Wilds/Content/Update Biome Definitions", priority = 150)]
        public static void EnsureBiomes()
        {
            Directory.CreateDirectory(Folder);
            var grassland = AssetDatabase.LoadAssetAtPath<BiomeDefinitionAsset>(GrasslandPath);
            if (grassland == null)
            {
                grassland = ScriptableObject.CreateInstance<BiomeDefinitionAsset>();
                AssetDatabase.CreateAsset(grassland, GrasslandPath);
            }
            grassland.sourceId = "biome.grassland";
            grassland.displayKey = "biome.grassland.name";
            grassland.order = 0;
            grassland.openToPlayers = true;
            EditorUtility.SetDirty(grassland);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(ContentDatabasePath)
                           ?? throw new InvalidOperationException("Missing " + ContentDatabasePath);
            database.biomes.RemoveAll(b => b == null);
            if (!database.biomes.Contains(grassland))
                database.biomes.Add(grassland);
            EditorUtility.SetDirty(database);
        }
    }
}
