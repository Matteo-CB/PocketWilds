using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Services.Analytics;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Analytics
{
    /// <summary>
    /// Debug table for analytics (T29): every number that travels in a batch, back to a readable name. Content ids
    /// are the stable FNV-1a hashes of their source ids; enums are their wire values. Written to
    /// Docs/Analytics/T29_Analytics_Ids.csv for whoever reads the data (dashboard, BigQuery, spreadsheet).
    /// </summary>
    public static class AnalyticsIdTable
    {
        public const string OutputPath = "Docs/Analytics/T29_Analytics_Ids.csv";

        [MenuItem("Pocket Wilds/Analytics/Export Id Table (T29)", priority = 200)]
        public static void ExportMenu() => Debug.Log("[AnalyticsIdTable] " + Export());

        public static string Export()
        {
            var rows = Build(LoadShippedContent());
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var text = new StringBuilder("kind,value,hex,name\n");
            foreach (var row in rows)
                text.Append(row.Kind).Append(',').Append(row.Value).Append(',').Append(row.Value.ToString("x8")).Append(',').Append(row.Name).Append('\n');
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            return rows.Count + " ids written to " + OutputPath;
        }

        public static ContentRegistry LoadShippedContent()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            if (database == null)
                throw new InvalidOperationException("No content database at " + AnimalPipeline.ContentDatabasePath);
            return database.CreateBuilder().Build();
        }

        public readonly struct Row
        {
            public readonly string Kind;
            public readonly uint Value;
            public readonly string Name;

            public Row(string kind, uint value, string name)
            {
                Kind = kind;
                Value = value;
                Name = name;
            }
        }

        public static List<Row> Build(ContentRegistry content)
        {
            var rows = new List<Row>();
            AddEnum<AnalyticsEventId>(rows, "event");
            AddEnum<ScreenId>(rows, "screen");
            AddEnum<FunnelMilestone>(rows, "milestone");
            AddEnum<CurrencyId>(rows, "currency");
            AddEnum<EconomyReason>(rows, "economy_reason");
            AddEnum<RarityTier>(rows, "rarity");
            AddEnum<PurchaseFlowStatus>(rows, "purchase_status");
            AddEnum<OfferPlacement>(rows, "offer_placement");
            AddEnum<AnalyticsConsent>(rows, "consent");
            AddTable(rows, "species", content.Species);
            AddTable(rows, "variant", content.Variants);
            AddTable(rows, "egg", content.Eggs);
            AddTable(rows, "biome", content.Biomes);
            AddTable(rows, "product", content.Shop.Products);
            AddTable(rows, "offer", content.Shop.Offers);
            AddTable(rows, "entitlement", content.Shop.Entitlements);
            return rows;
        }

        private static void AddEnum<T>(List<Row> rows, string kind) where T : Enum
        {
            foreach (T value in Enum.GetValues(typeof(T)))
                rows.Add(new Row(kind, Convert.ToUInt32(value), value.ToString()));
        }

        private static void AddTable<T>(List<Row> rows, string kind, ContentTable<T> table) where T : class, IContentDefinition
        {
            for (int i = 0; i < table.Count; i++)
            {
                var id = table[i].Id;
                rows.Add(new Row(kind, id.Value, ContentIdDebugNames.TryGet(id, out var name) ? name : "?"));
            }
        }
    }
}
