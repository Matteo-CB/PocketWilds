using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Analytics;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Capture;
using PocketWilds.Editor.Eggs;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using PocketWilds.UI;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Production
{
    /// <summary>
    /// ED-001 SpeciesWizard, ED-003 EggWizard and ED-011 ContentIdRegistryTool (T34D): what each species and egg still
    /// needs, checked on the disk and in the content data (never from a status file), with the Unity steps one click
    /// away; and every content id with its hash, plus retiring an id for good (tombstones). The Blender steps of a new
    /// species stay in SourceArt/Blender/Tools/pw_species.py; the wizard says which step comes next.
    /// </summary>
    public static class ContentWizard
    {
        public const string BlenderAnimals = "SourceArt/Blender/Animals";
        public const string BlenderEggs = "SourceArt/Blender/Eggs";
        public const int BaseVariantsTarget = 6;
        /// <summary>A companion's looks (T47: base, bond 5, bond 10).</summary>
        public const int CompanionLooksTarget = 3;

        /// <summary>One check of a species or an egg: passed, or what is missing.</summary>
        public readonly struct Check
        {
            public Check(string name, bool passed, string detail)
            {
                Name = name;
                Passed = passed;
                Detail = detail;
            }

            public string Name { get; }
            public bool Passed { get; }
            public string Detail { get; }
        }

        public sealed class Row
        {
            public string Id;
            public string Label;
            public readonly List<Check> Checks = new List<Check>();
            public bool Complete => Checks.All(c => c.Passed);
            public Check? NextMissing => Checks.Where(c => !c.Passed).Select(c => (Check?)c).FirstOrDefault();
        }

        public static ContentDatabaseAsset Database() =>
            AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath)
            ?? throw new InvalidOperationException("No content database at " + AnimalPipeline.ContentDatabasePath);

        /// <summary>Species name used by the file conventions (PF_Animal_Rabbit -> Rabbit).</summary>
        public static string SpeciesName(SpeciesDefinitionAsset species) =>
            species.visualKey.StartsWith("PF_Animal_", StringComparison.Ordinal) ? species.visualKey.Substring("PF_Animal_".Length) : species.visualKey;

        // ---------------------------------------------------------------- species (ED-001)

        public static List<Row> SpeciesReport()
        {
            var database = Database();
            var codex = AssetDatabase.LoadAssetAtPath<CodexVisualCatalog>(AnimalThumbnailPipeline.CatalogPath);
            var visuals = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var rows = new List<Row>();
            foreach (var species in database.species.Where(s => s != null))
            {
                string name = SpeciesName(species);
                var row = new Row { Id = species.sourceId, Label = name };
                string blendDir = $"{BlenderAnimals}/{name}";
                bool blend = Directory.Exists(blendDir) && Directory.GetFiles(blendDir, "*.blend").Length > 0;
                row.Checks.Add(new Check("Source Blender", blend, blend ? blendDir : $"aucun .blend dans {blendDir} (pw_species.py : start_from puis build_blockout)"));

                string model = AnimalPipeline.ModelPath(name);
                bool hasModel = File.Exists(model);
                row.Checks.Add(new Check("Modèle exporté", hasModel, hasModel ? model : $"{model} absent (pw_species.py jusqu'à place_sockets, puis pw_export)"));

                string prefab = AnimalPipeline.PrefabPath(name);
                bool hasPrefab = File.Exists(prefab);
                row.Checks.Add(new Check("Prefab", hasPrefab, hasPrefab ? prefab : $"{prefab} absent (Reconstruire côté Unity)"));

                var errors = hasPrefab ? AssetValidator.ValidateAnimalPrefab(prefab).Where(i => i.Severity == IssueSeverity.Error).ToList() : new List<ValidationIssue>();
                row.Checks.Add(new Check("AssetValidator", hasPrefab && errors.Count == 0,
                    !hasPrefab ? "pas de prefab" : errors.Count == 0 ? "aucune erreur" : string.Join(" ; ", errors.Take(3))));

                bool inCatalog = visuals != null && visuals.TryGet(species.visualKey, out var entry) && entry.prefab != null;
                row.Checks.Add(new Check("Catalogue visuel", inCatalog, inCatalog ? species.visualKey : $"{species.visualKey} absent de DEF_AnimalVisualCatalog"));

                // A companion (T47) has its own looks (three, given by its bond) and never comes from an egg; an event species (T49, the
                // mole: every color secret, given by its event) neither.
                int baseVariants = species.variants.Count(v => !v.secret);
                bool eggless = species.companion || (species.variants.Count > 0 && baseVariants == 0);
                if (eggless)
                    row.Checks.Add(new Check("Variantes", species.variants.Count >= CompanionLooksTarget,
                        $"compagnon ou espèce d'événement : {species.variants.Count} apparences (cible {CompanionLooksTarget})"));
                else
                    row.Checks.Add(new Check("Variantes", baseVariants >= BaseVariantsTarget,
                        $"{baseVariants} de base (cible {BaseVariantsTarget}), {species.variants.Count(v => v.secret)} secrètes"));

                var keys = new[] { species.displayKey }.Concat(species.variants.Select(v => v.displayKey)).ToList();
                var missing = MissingTexts(database, keys);
                row.Checks.Add(new Check("Textes en et fr", missing.Count == 0, missing.Count == 0 ? $"{keys.Count} clés" : "manque : " + string.Join(", ", missing.Take(4))));

                int portraits = codex == null ? 0 : species.variants.Count(v => codex.variants.Any(e => e.sourceId == v.sourceId && e.sprite != null));
                bool silhouette = codex != null && codex.silhouettes.Any(e => e.sourceId == species.sourceId && e.sprite != null);
                bool thumbnails = portraits == species.variants.Count && silhouette;
                row.Checks.Add(new Check("Vignettes et silhouette", thumbnails,
                    $"{portraits}/{species.variants.Count} vignettes, silhouette {(silhouette ? "oui" : "non")} (Vignettes)"));

                var eggs = database.eggs.Where(e => e != null && e.pool.Any(p => p.speciesId == species.sourceId && p.weight > 0f)).Select(e => e.name).ToList();
                if (eggless)
                    row.Checks.Add(new Check("Œufs", eggs.Count == 0, eggs.Count == 0 ? "compagnon ou espèce d'événement : jamais dans un œuf" : "ni un compagnon ni une espèce d'événement ne vont dans un œuf : " + string.Join(", ", eggs)));
                else
                    row.Checks.Add(new Check("Œufs", eggs.Count > 0, eggs.Count > 0 ? string.Join(", ", eggs) : "aucun œuf ne la donne (pool des DEF_Egg_*)"));
                rows.Add(row);
            }
            return rows;
        }

        private static List<string> MissingTexts(ContentDatabaseAsset database, IEnumerable<string> keys)
        {
            var missing = new List<string>();
            foreach (var key in keys)
            {
                foreach (var language in new[] { "en", "fr" })
                {
                    var table = database.localization.FirstOrDefault(t => t != null && t.languageCode == language);
                    if (table == null || !table.TryGet(key, out var value) || string.IsNullOrWhiteSpace(value))
                        missing.Add($"{key} ({language})");
                }
            }
            return missing;
        }

        /// <summary>Runs the Unity side of a species (prefab, controller, material, definition, catalog) by its build method.</summary>
        public static string RebuildSpecies(string name)
        {
            var method = typeof(AnimalPipeline).GetMethod("Build" + name, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null)
                return $"Pas de AnimalPipeline.Build{name} : ajoute EnsureXDefinition et un menu Build {name} (comme Squirrel, T23).";
            method.Invoke(null, null);
            return name + " reconstruit côté Unity.";
        }

        // ---------------------------------------------------------------- eggs (ED-003)

        public static List<Row> EggReport()
        {
            var database = Database();
            var species = new HashSet<string>(database.species.Where(s => s != null).Select(s => s.sourceId));
            var biomes = new HashSet<string>(database.biomes.Where(b => b != null).Select(b => b.sourceId));
            bool blend = Directory.Exists(BlenderEggs) && Directory.GetFiles(BlenderEggs, "*.blend").Length > 0;
            var prefabErrors = AssetValidator.ValidateEggPrefab().Where(i => i.Severity == IssueSeverity.Error).ToList();
            var rows = new List<Row>();
            foreach (var egg in database.eggs.Where(e => e != null))
            {
                var row = new Row { Id = egg.sourceId, Label = egg.name };
                row.Checks.Add(new Check("Source Blender", blend, blend ? BlenderEggs : $"aucun .blend dans {BlenderEggs}"));
                row.Checks.Add(new Check("Prefab et validateur", File.Exists(EggPipeline.EggPrefabPath) && prefabErrors.Count == 0,
                    prefabErrors.Count == 0 ? EggPipeline.EggPrefabPath : string.Join(" ; ", prefabErrors.Take(3))));
                // A Discovery egg belongs to no biome and has no pool: it picks by its own rules (doc 02 Œufs, section 2.3).
                bool discovery = egg.tier == EggTier.Discovery;
                row.Checks.Add(discovery
                    ? new Check("Biome", string.IsNullOrEmpty(egg.biomeId), "aucun : œuf Découverte")
                    : new Check("Biome", biomes.Contains(egg.biomeId), egg.biomeId));
                var unknown = egg.pool.Where(p => !species.Contains(p.speciesId)).Select(p => p.speciesId).ToList();
                bool pool = discovery ? egg.pool.Count == 0 : egg.pool.Count > 0 && egg.pool.All(p => p.weight > 0f) && unknown.Count == 0;
                row.Checks.Add(new Check("Pool", pool, discovery ? "règles de l'œuf Découverte"
                    : unknown.Count > 0 ? "espèces inconnues : " + string.Join(", ", unknown)
                    : $"{egg.pool.Count} espèces, poids {string.Join(" / ", egg.pool.Select(p => p.weight.ToString("0.##")))}"));
                bool weights = egg.rarityWeights != null && egg.rarityWeights.Length == 6 && egg.rarityWeights.All(w => w >= 0.0) && egg.rarityWeights.Sum() > 0.0;
                row.Checks.Add(new Check("Raretés", weights, weights ? string.Join(" / ", egg.rarityWeights) : "6 poids positifs attendus"));
                row.Checks.Add(new Check("Prix", egg.baseCost >= 1.0 && egg.costGrowth >= 1.0, $"{egg.baseCost} x {egg.costGrowth}^n"));
                var missing = MissingTexts(database, new[] { egg.displayKey });
                row.Checks.Add(new Check("Textes en et fr", missing.Count == 0, missing.Count == 0 ? egg.displayKey : "manque : " + string.Join(", ", missing)));
                rows.Add(row);
            }
            return rows;
        }

        // ---------------------------------------------------------------- content ids (ED-011)

        public static List<AnalyticsIdTable.Row> ContentIds()
        {
            var kinds = new HashSet<string> { "species", "variant", "egg", "biome", "product", "offer", "entitlement" };
            return AnalyticsIdTable.Build(AnalyticsIdTable.LoadShippedContent()).Where(r => kinds.Contains(r.Kind)).ToList();
        }

        /// <summary>
        /// Retires a source id for good: it joins the tombstones, and ContentRegistryBuilder refuses it for any content
        /// type from then on. A live id is refused (remove its content first) and a tombstone is never removed.
        /// </summary>
        public static string Retire(string sourceId)
        {
            sourceId = (sourceId ?? string.Empty).Trim();
            if (sourceId.Length == 0)
                return "Identifiant vide.";
            var database = Database();
            if (database.tombstones.Contains(sourceId))
                return sourceId + " est déjà retiré.";
            if (ContentIds().Any(r => r.Name == sourceId))
                return sourceId + " est encore utilisé : retire d'abord son contenu.";
            database.tombstones.Add(sourceId);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return sourceId + " retiré : il ne pourra plus jamais servir (hash " + ContentIdHash.Compute(sourceId).ToString("x8") + ").";
        }
    }

    /// <summary>The wizard window: Espèces, Œufs, Identifiants, and since T49B Biomes (ED-004) and Décorations (ED-002).</summary>
    public sealed class ContentWizardWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "Espèces", "Œufs", "Identifiants", "Biomes", "Décorations" };

        private int _tab;
        private Vector2 _scroll;
        private List<ContentWizard.Row> _species;
        private List<ContentWizard.Row> _eggs;
        private List<AnalyticsIdTable.Row> _ids;
        private List<ContentWizard.Row> _biomes;
        private List<ContentWizard.Row> _decorations;
        private string _search = string.Empty;
        private string _retire = string.Empty;
        private string _message = string.Empty;

        [MenuItem("Pocket Wilds/Content Wizard (Species, Eggs, IDs)", priority = 50)]
        public static void Open() => GetWindow<ContentWizardWindow>("Content Wizard").Refresh();

        private void Refresh()
        {
            try
            {
                _species = ContentWizard.SpeciesReport();
                _eggs = ContentWizard.EggReport();
                _ids = ContentWizard.ContentIds();
                _biomes = BiomeWizard.BiomeReport();
                _decorations = DecorationWizard.DecorationReport();
            }
            catch (Exception ex)
            {
                _message = ex.Message;
            }
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _tab = GUILayout.Toolbar(_tab, Tabs);
                if (GUILayout.Button("Actualiser", GUILayout.Width(90)))
                    Refresh();
            }
            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Info);
            if (_species == null)
                Refresh();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: SpeciesTab(); break;
                case 1: EggsTab(); break;
                case 3: BiomesTab(); break;
                case 4: DecorationsTab(); break;
                default: IdsTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void SpeciesTab()
        {
            EditorGUILayout.HelpBox(
                "Nouvelle espèce : côté Blender, SourceArt/Blender/Tools/pw_species.py (SPECS, puis start_from, build_blockout, place_features, " +
                "build_lod0, skin_lod0, build_lods, place_sockets), pw_masks et pw_export ; côté Unity, EnsureXDefinition et Build X dans " +
                "AnimalPipeline, les textes dans UiPipeline.Texts, le pool d'un œuf, puis les vignettes. Chaque ligne dit l'étape suivante.",
                MessageType.None);
            if (GUILayout.Button("Vignettes et silhouettes de toutes les espèces"))
                Run(() => AnimalThumbnailPipeline.RenderAll());
            if (GUILayout.Button("Mettre à jour les définitions (palettes, variantes)"))
                Run(() => { AnimalPipeline.UpdateSpeciesDefinitions(); return "Définitions à jour."; });
            foreach (var row in _species ?? new List<ContentWizard.Row>())
            {
                DrawRow(row);
                if (GUILayout.Button("Reconstruire " + row.Label + " côté Unity"))
                    Run(() => ContentWizard.RebuildSpecies(row.Label));
            }
        }

        private void EggsTab()
        {
            if (GUILayout.Button("Reconstruire la famille d'œufs (modèle, prefab, effets)"))
                Run(() => { EggPipeline.BuildAll(); return "Famille d'œufs reconstruite."; });
            if (GUILayout.Button("Mettre à jour les définitions d'œufs"))
                Run(() => { EggPipeline.UpdateEggDefinitions(); return "Définitions d'œufs à jour."; });
            foreach (var row in _eggs ?? new List<ContentWizard.Row>())
                DrawRow(row);
        }

        private void IdsTab()
        {
            var database = ContentWizard.Database();
            _search = EditorGUILayout.TextField("Chercher", _search);
            foreach (var id in (_ids ?? new List<AnalyticsIdTable.Row>())
                     .Where(r => string.IsNullOrEmpty(_search) || r.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                     .Take(300))
                EditorGUILayout.LabelField($"{id.Kind,-12} {id.Value:x8}", id.Name);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Identifiants retirés ({database.tombstones.Count})", EditorStyles.boldLabel);
            foreach (var tombstone in database.tombstones)
                EditorGUILayout.LabelField(tombstone, ContentIdHash.Compute(tombstone).ToString("x8"));
            using (new EditorGUILayout.HorizontalScope())
            {
                _retire = EditorGUILayout.TextField("Retirer", _retire);
                if (GUILayout.Button("Retirer pour toujours", GUILayout.Width(150))
                    && EditorUtility.DisplayDialog("Retirer un identifiant", $"Retirer {_retire} pour toujours ? Il ne pourra plus jamais servir.", "Retirer", "Annuler"))
                    Run(() => ContentWizard.Retire(_retire));
            }
        }

        private void BiomesTab()
        {
            EditorGUILayout.HelpBox(
                "Un biome se crée depuis sa fiche (BiomeWizard.BiomeSpec) : définition et zones, palettes, quatre ambiances de lumière tirées " +
                "de la Prairie, ensemble de chunks, audio, groupes Addressables et label, profil du monde, textes fr et en, base de contenu. " +
                "Relancer met à jour sans rien dupliquer ni écraser l'art existant ; les zones ne s'ajoutent qu'à la fin (bit de sauvegarde). " +
                "Un prefab de chunk pas encore construit reste « à faire » sans être une erreur.",
                MessageType.None);
            if (GUILayout.Button("Migrer la Prairie (adopter l'existant)"))
                Run(BiomeWizard.MigrateGrassland);
            if (GUILayout.Button("Créer ou mettre à jour la Forêt (T50)")
                && EditorUtility.DisplayDialog("Forêt", "Créer ou mettre à jour la Forêt depuis sa fiche ? Ses assets de données s'ajoutent au projet.", "Créer", "Annuler"))
                Run(() => BiomeWizard.CreateOrUpdate(BiomeWizard.ForestSpec()));
            if (GUILayout.Button("Valider les biomes"))
                Run(() => Summary(AssetValidator.ValidateBiomes()));
            foreach (var row in _biomes ?? new List<ContentWizard.Row>())
                DrawRow(row);
        }

        private void DecorationsTab()
        {
            EditorGUILayout.HelpBox(
                "Une décoration se crée depuis sa fiche (DecorationWizard.DecorationSpec) : prefab depuis le modèle ou adopté, empreinte mesurée " +
                "une fois sur le LOD0 (bandes de hauteur et contour au sol), classe XS à XL, adresse = nom du prefab dans le groupe de la fiche, " +
                "définition enregistrée, copie du catalogue visuel synchronisée, puis l'icône depuis le vrai prefab.",
                MessageType.None);
            if (GUILayout.Button("Décorations des Terriers et Terrier douillet (mesurer, définitions)"))
                Run(DecorationWizard.UpdateAll);
            if (GUILayout.Button("Icônes des décorations"))
                Run(() => DecorationWizard.RenderIcons(DecorationWizard.AllSpecs()));
            if (GUILayout.Button("Valider les décorations"))
                Run(() => Summary(AssetValidator.ValidateDecorations()));
            foreach (var row in _decorations ?? new List<ContentWizard.Row>())
                DrawRow(row);
        }

        private static string Summary(List<ValidationIssue> issues)
        {
            var errors = issues.Where(i => i.Severity == IssueSeverity.Error).ToList();
            return errors.Count == 0
                ? $"Aucune erreur ({issues.Count} notes « à faire »)."
                : $"{errors.Count} erreurs : " + string.Join(" ; ", errors.Take(4).Select(e => e.Rule + " " + e.Message));
        }

        private static void DrawRow(ContentWizard.Row row)
        {
            EditorGUILayout.Space();
            var next = row.NextMissing;
            EditorGUILayout.LabelField($"{row.Label}  ({row.Id})", row.Complete ? "complet" : "suivant : " + next?.Name, EditorStyles.boldLabel);
            foreach (var check in row.Checks)
                EditorGUILayout.LabelField((check.Passed ? "  ok   " : "  à faire   ") + check.Name, check.Detail, EditorStyles.wordWrappedLabel);
        }

        private void Run(Func<string> action)
        {
            try
            {
                _message = action();
            }
            catch (Exception ex)
            {
                _message = ex.Message;
                Debug.LogException(ex);
            }
            Refresh();
        }
    }
}
