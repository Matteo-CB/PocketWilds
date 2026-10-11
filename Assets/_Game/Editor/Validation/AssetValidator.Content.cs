using System.Collections.Generic;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Editor.Production;
using UnityEditor;

namespace PocketWilds.Editor.Validation
{
    /// <summary>
    /// T49B biome and decoration rules (doc 06 Content IDs, doc 05 Décorations), read from the same checks as the Biome and
    /// Decoration wizards: a blocking check that fails is an error; work still « à faire » (a chunk prefab whose art is not built
    /// yet, the zone kit, the weather) is only information.
    /// </summary>
    public static partial class AssetValidator
    {
        [MenuItem("Pocket Wilds/Validate/Biomes and Decorations (T49B)", priority = 404)]
        public static void ValidateContentMenu()
        {
            var issues = ValidateBiomes();
            issues.AddRange(ValidateDecorations());
            Report(issues);
        }

        /// <summary>Every biome of the content database.</summary>
        public static List<ValidationIssue> ValidateBiomes()
        {
            var issues = new List<ValidationIssue>();
            foreach (var biome in ContentWizard.Database().biomes.Where(b => b != null))
                issues.AddRange(ValidateBiome(biome));
            return issues;
        }

        /// <summary>
        /// Zones (one arrival first, at most one passage last, no gap), the chunk set against the zones, the Addressables groups and
        /// label, the four lighting moods, the audio beds, the palettes, the world profile and the fr and en texts.
        /// </summary>
        public static List<ValidationIssue> ValidateBiome(BiomeDefinitionAsset biome)
        {
            string path = AssetDatabase.GetAssetPath(biome);
            return BiomeWizard.Inspect(biome).Where(f => !f.Passed)
                .Select(f => new ValidationIssue(f.Blocking ? IssueSeverity.Error : IssueSeverity.Info, path, "Biome." + f.Name, f.Detail)).ToList();
        }

        /// <summary>
        /// Every decoration of the content database: measured, outline inside its class (2 mm), in budget, on the shared material,
        /// turning parts named _Sails_, address = prefab name, palette, icon, texts, and its presentation copy in sync.
        /// </summary>
        public static List<ValidationIssue> ValidateDecorations()
        {
            var issues = new List<ValidationIssue>();
            foreach (var definition in ContentWizard.Database().decorations.Where(d => d != null))
            {
                string path = AssetDatabase.GetAssetPath(definition);
                issues.AddRange(DecorationWizard.Inspect(definition).Where(f => !f.Passed)
                    .Select(f => new ValidationIssue(f.Blocking ? IssueSeverity.Error : IssueSeverity.Info, path, "Decoration." + f.Name, f.Detail)));
            }
            return issues;
        }
    }
}
