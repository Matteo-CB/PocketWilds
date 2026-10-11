using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Cosmetics;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.Production;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Events
{
    /// <summary>
    /// T49 decorations of L'Éveil des Terriers (E-DEC-EVT-burrows-01..13, the gold versions of 1-4, the monument E-LMK-EVT-burrows).
    /// <see cref="BuildAll"/>: the palette rows of pw_event_burrows_props.PALETTES in DEF_Palettes_EventBurrows (registered in the
    /// content database; the gold versions share their model with palette.event_burrows.gold), one prefab per model in
    /// Prefabs/Events/Burrows on the shared MAT_Environment_Common (shadows on), and one DEF_CosmeticVisualCatalog decoration entry
    /// per owned item: prefab address, palette row and the measured volume (CollisionProxies height bands from the model's own
    /// surface, like the chunk props; its ground outline for decoration against decoration), so the decoration mode (T56) keeps
    /// « nothing enters anything ». The prefabs carry « Burrows » in their names: BurrowsContentGroup puts them in LiveOps_Burrows.
    /// <see cref="RenderIcons"/>: ICO_Decor_Burrows_&lt;Name&gt; (and &lt;Name&gt;Gold, RootBurrow) from the real prefabs. Idempotent.
    /// </summary>
    public static class BurrowsDecorPipeline
    {
        public const string ModelFolder = "Assets/_Game/Art/Events/Burrows/Models";
        public const string PrefabFolder = "Assets/_Game/Prefabs/Events/Burrows";
        public const string PalettesPath = "Assets/_Game/Content/Palettes/DEF_Palettes_EventBurrows.asset";
        public const string RowPrefix = "palette.event_burrows.";
        public const string GoldRow = RowPrefix + "gold";
        public const string MonumentModel = "Landmark_Burrows_RootBurrow";
        public const string MonumentArt = "RootBurrow";

        /// <summary>Triangle budgets (doc): decorations, Coffret (Box) decorations, monument; strictly under.</summary>
        public const int DecoBudget = 1500;
        public const int BoxBudget = 2000;
        public const int MonumentBudget = 2500;

        /// <summary>One owned decoration: item id, model object (= FBX name), prefab, palette row, icon, budget.</summary>
        public readonly struct Piece
        {
            public readonly string ItemId;
            public readonly string Model;
            public readonly string Prefab;
            public readonly string Row;
            public readonly string Icon;
            public readonly int Budget;
            public readonly bool Gold;

            public Piece(string itemId, string model, string row, string icon, int budget, bool gold)
            {
                ItemId = itemId;
                Model = model;
                Prefab = "PF_" + model;
                Row = row;
                Icon = icon;
                Budget = budget;
                Gold = gold;
            }
        }

        /// <summary>The 13 decorations, the gold versions of 1-4 (same prefab, gold row), the monument.</summary>
        public static IEnumerable<Piece> Pieces()
        {
            var decorations = BurrowsEventPipeline.Decorations;
            for (int n = 1; n <= decorations.Length; n++)
            {
                var (id, name) = decorations[n - 1];
                int budget = n >= 9 && n <= 12 ? BoxBudget : DecoBudget;
                yield return new Piece(id, "Decor_Burrows_" + name, RowPrefix + Snake(name), BurrowsEventPipeline.DecoIcon(name), budget, false);
            }
            for (int n = 1; n <= 4; n++)
            {
                string name = decorations[n - 1].name;
                yield return new Piece(BurrowsEventPipeline.GoldDeco(n), "Decor_Burrows_" + name, GoldRow, BurrowsEventPipeline.DecoIcon(name + "Gold"), DecoBudget, true);
            }
            yield return new Piece(BurrowsEventPipeline.Monument, MonumentModel, RowPrefix + Snake(MonumentArt), BurrowsEventPipeline.DecoIcon(MonumentArt), MonumentBudget, false);
        }

        public static string PrefabPath(string prefab) => $"{PrefabFolder}/{prefab}.prefab";

        public static string ModelPath(string model) => $"{ModelFolder}/{model}.fbx";

        /// <summary>pw_event_burrows_props.PALETTES (base, R, G, B, A, occlusion, accent).</summary>
        public static readonly (string row, string[] hex)[] Rows =
        {
            (RowPrefix + "flower_entrance", new[] { "8A6646", "7FAF5A", "F2C94C", "CFEFF2", "F8F4EA", "3E2E24", "FFFFFF" }),
            (RowPrefix + "moss_mound", new[] { "8A6646", "6FA34E", "A6D07A", "CFEFF2", "D8D2C4", "3E2E24", "FFFFFF" }),
            (RowPrefix + "dew_mushrooms", new[] { "E58A6B", "7FAF5A", "F3E6CC", "CFEFF2", "FBF6EA", "5A4034", "FFFFFF" }),
            (RowPrefix + "watering_can", new[] { "79A88F", "5E8C74", "D9A85A", "CFEFF2", "F8F4EA", "34483E", "FFFFFF" }),
            (GoldRow, new[] { "D9A441", "E8C25A", "B8862E", "FFF0B0", "FFF6D8", "7A5A22", "FFFFFF" }),
            (RowPrefix + "wheelbarrow", new[] { "F0E3C4", "8DC26F", "C9925A", "5E4E44", "7A5640", "4A3A2E", "FFFFFF" }),
            (RowPrefix + "seedling_pots", new[] { "D08A5E", "86C25E", "6B4A36", "CFEFF2", "E8D2A8", "4A3A2E", "FFFFFF" }),
            (RowPrefix + "burrow_sign", new[] { "B5835A", "86C25E", "8A6646", "3B2A22", "E9D2A6", "4A3A2E", "FFFFFF" }),
            (RowPrefix + "worm_cushion", new[] { "F2A9A6", "E58F95", "F8EEDC", "3A2A2E", "F28C9A", "8C5A5E", "FFFFFF" }),
            (RowPrefix + "garden_shed", new[] { "DDB98C", "7FAF5A", "8C5E3C", "BFE0E3", "F4EAD2", "4A3A2E", "FFFFFF" }),
            (RowPrefix + "greenhouse", new[] { "B5835A", "86C25E", "D08A5E", "CFE8DF", "F6F0E2", "4A3A2E", "FFFFFF" }),
            (RowPrefix + "wooden_bench", new[] { "C9925A", "86C25E", "8C5E3C", "F2C94C", "F8F4EA", "4A3A2E", "FFFFFF" }),
            (RowPrefix + "garden_fence", new[] { "F3EBDD", "7FB85A", "C9925A", "F2C94C", "F3BDD3", "6A5A4A", "FFFFFF" }),
            (RowPrefix + "souvenir", new[] { "B5835A", "86C25E", "8A6646", "CFEFF2", "F4EAD2", "4A3A2E", "FFFFFF" }),
            (RowPrefix + "root_burrow", new[] { "8A6646", "7FAF5A", "9A6A45", "CFEFF2", "F8F4EA", "2E221B", "FFFFFF" }),
        };

        /// <summary>FlowerEntrance → flower_entrance.</summary>
        public static string Snake(string pascal)
        {
            var text = new StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                if (char.IsUpper(pascal[i]) && i > 0)
                    text.Append('_');
                text.Append(char.ToLowerInvariant(pascal[i]));
            }
            return text.ToString();
        }

        [MenuItem("Pocket Wilds/Events/Build Burrows Decorations (T49)", priority = 197)]
        public static void BuildAllMenu() => Debug.Log("[BurrowsDecorPipeline] " + BuildAll());

        [MenuItem("Pocket Wilds/Events/Render Burrows Decoration Icons (T49)", priority = 198)]
        public static void RenderIconsMenu() => Debug.Log("[BurrowsDecorPipeline] " + RenderIcons());

        /// <summary>
        /// T49B: the palettes, then the Decoration Wizard (ED-002) builds the prefabs from the models, measures them, writes the
        /// DEF_Decor_Burrows_* definitions (Content/Decorations/Burrows), keeps the prefabs addressed in LiveOps_Burrows and syncs the
        /// DEF_CosmeticVisualCatalog decoration entries from the definitions.
        /// </summary>
        public static string BuildAll()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.EnvironmentMaterialPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(AccessoryVisualPipeline.CatalogPath);
            if (material == null || catalog == null)
                return "missing MAT_Environment_Common or DEF_CosmeticVisualCatalog: run GrasslandPipeline and AccessoryVisualPipeline first";
            EnsurePalettes();
            Directory.CreateDirectory(PrefabFolder);
            return DecorationWizard.CreateOrUpdate(DecorationWizard.BurrowsSpecs());
        }

        /// <summary>DEF_Palettes_EventBurrows from <see cref="Rows"/>, registered in the content database.</summary>
        public static void EnsurePalettes()
        {
            var set = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(PalettesPath);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PalettesPath));
                set = ScriptableObject.CreateInstance<PaletteSetAsset>();
                AssetDatabase.CreateAsset(set, PalettesPath);
            }
            set.palettes.Clear();
            foreach (var (row, hex) in Rows)
                AccessoryVisualPipeline.Add(set, row, hex);
            EditorUtility.SetDirty(set);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            if (database != null && !database.paletteSets.Contains(set))
            {
                database.paletteSets.Add(set);
                EditorUtility.SetDirty(database);
            }
        }

        /// <summary>The 18 decoration icons from the real prefabs (gold versions in the gold row), three-quarter front view.</summary>
        public static string RenderIcons() => DecorationWizard.RenderIcons(DecorationWizard.BurrowsSpecs());
    }
}
