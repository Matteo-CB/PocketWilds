using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Editor.Cosmetics;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.Events;
using PocketWilds.Editor.Shop;
using PocketWilds.Editor.Validation;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Production
{
    /// <summary>
    /// ED-002 Decoration Wizard (T49B, doc 05 Décorations). From a <see cref="DecorationSpec"/> it writes one
    /// DecorationDefinitionAsset per decoration under Content/Decorations/{Folder}/DEF_Decor_*.asset: identity, category,
    /// footprint class and shape (given, or the smallest class holding the measured outline), interactions, the prefab by
    /// address only (PF_Decor_{Prefix}_{Name}; built from a model and mesh names like the event decorations, or adopted), the
    /// palette row and icon, and the volume measured once on the LOD0 with CollisionProxies (height bands, convex ground
    /// outline, reach, height, triangles), never recomputed in play. The prefab gets its Addressables entry (address = prefab
    /// name) in the group the spec names (event decorations stay in their event group), the definition is registered in
    /// DEF_ContentDatabase, and the CosmeticVisualCatalog.DecorEntry presentation copy is synced from it. Icons come from the
    /// real prefab in the IconStudio (<see cref="RenderIcons"/>). The same <see cref="Inspect"/> feeds the wizard report and
    /// <see cref="AssetValidator.ValidateDecorations"/>.
    /// </summary>
    public static class DecorationWizard
    {
        public const string DefaultRoot = BiomeWizard.DefaultRoot;

        /// <summary>Outline tolerance past the class (m).</summary>
        public const float Tolerance = 0.002f;

        /// <summary>Name parts of a part that turns: it must carry « _Sails_ » (the rotor flag of the shared shader).</summary>
        public static readonly string[] TurningTokens = { "sail", "rotor", "propeller", "pinwheel", "vane", "spinner" };

        /// <summary>One decoration to write (doc 05): identity, class, prefab and its source, palette, icon, Addressables group.</summary>
        public sealed class DecorationSpec
        {
            public string SourceId = string.Empty;
            public string DisplayKeyOverride;
            public string DocId = string.Empty;
            public DecorationCategory Category = DecorationCategory.Nature;
            public string Biome = string.Empty;
            public string Source = string.Empty;
            public int MaxCopies = 1;

            /// <summary>Null: the smallest class holding the measured outline (with <see cref="Shape"/> when given).</summary>
            public DecorationFootprintClass? Footprint;
            public DecorationFootprintShape? Shape;

            public DecorationInteraction Interactions = DecorationInteraction.Inspect;
            public float TopHeight;
            public int TrisBudget = 300;

            /// <summary>Prefab name, also its address (PF_Decor_GL_DaisyTuft).</summary>
            public string Prefab = string.Empty;
            public string PrefabPath = string.Empty;

            /// <summary>When set, the prefab is built from this model (all its meshes, or <see cref="ModelObject"/>) on MAT_Environment_Common.</summary>
            public string ModelPath;
            public string ModelObject;

            public string Palette = string.Empty;
            public string Icon = string.Empty;
            public float IconYaw = 30f;

            /// <summary>Addressables group of the prefab; empty when a base-build catalog references the prefab directly.</summary>
            public string Group = string.Empty;
            public string Label;

            /// <summary>Content/Decorations/{Folder}.</summary>
            public string Folder = string.Empty;

            /// <summary>Definition file name (DEF_Decor_Burrows_MossMound).</summary>
            public string AssetName = string.Empty;

            /// <summary>Writes the CosmeticVisualCatalog.DecorEntry presentation copy.</summary>
            public bool SyncCatalog;

            public string DisplayKey => DisplayKeyOverride ?? SourceId + ".name";
        }

        public static string DefinitionPath(DecorationSpec spec, string root = DefaultRoot) =>
            $"{root}/Content/Decorations/{spec.Folder}/{spec.AssetName}.asset";

        // ---------------------------------------------------------------- specs

        /// <summary>
        /// T49 L'Éveil des Terriers: the 13 decorations (1-4 track, 5-8 shop, 9-12 Box, 13 the keepsake), the gold versions of 1-4
        /// (same model, gold row) and the monument, in LiveOps_Burrows, built from pw_event_burrows_props models.
        /// </summary>
        public static IEnumerable<DecorationSpec> BurrowsSpecs()
        {
            var decorations = BurrowsEventPipeline.Decorations;
            foreach (var piece in BurrowsDecorPipeline.Pieces())
            {
                bool monument = piece.ItemId == BurrowsEventPipeline.Monument;
                bool souvenir = piece.ItemId == BurrowsEventPipeline.Souvenir;
                int n = Array.FindIndex(decorations, d => d.id == piece.ItemId) + 1;
                int gold = Array.FindIndex(decorations, d => d.id + "_gold" == piece.ItemId) + 1;
                yield return new DecorationSpec
                {
                    SourceId = piece.ItemId,
                    DocId = monument ? "E-LMK-EVT-burrows" : piece.Gold ? $"E-DEC-EVT-burrows-{gold:00}-gold" : $"E-DEC-EVT-burrows-{n:00}",
                    Category = monument ? DecorationCategory.Landmark : souvenir ? DecorationCategory.Souvenir : DecorationCategory.Event,
                    Source = monument ? "event.burrows.monument" : souvenir ? "event.burrows.souvenir" : piece.Gold ? "event.burrows.track.gold"
                        : n <= 4 ? "event.burrows.track" : n <= 8 ? "event.burrows.shop" : "event.burrows.box",
                    TrisBudget = piece.Budget,
                    Prefab = piece.Prefab,
                    PrefabPath = BurrowsDecorPipeline.PrefabPath(piece.Prefab),
                    ModelPath = BurrowsDecorPipeline.ModelPath(piece.Model),
                    Palette = piece.Row,
                    Icon = piece.Icon,
                    Group = BurrowsContentGroup.Group,
                    Label = BurrowsContentGroup.Label,
                    Folder = "Burrows",
                    AssetName = "DEF_" + piece.Icon.Substring("ICO_".Length),
                    SyncCatalog = true,
                };
            }
        }

        /// <summary>T47 the Cozy Burrow (decor.cozy_burrow, Meadow Pack): adopted prefab, referenced directly by the cosmetic catalog.</summary>
        public static IEnumerable<DecorationSpec> CozyBurrowSpecs()
        {
            yield return new DecorationSpec
            {
                SourceId = ShopPipeline.CozyBurrow,
                DocId = "decor.cozy_burrow",
                Category = DecorationCategory.Companion,
                Source = ShopPipeline.MeadowPackProduct,
                Interactions = DecorationInteraction.Inspect | DecorationInteraction.Sleep,
                TrisBudget = BurrowsDecorPipeline.DecoBudget,
                Prefab = Path.GetFileNameWithoutExtension(AccessoryVisualPipeline.BurrowPrefabPath),
                PrefabPath = AccessoryVisualPipeline.BurrowPrefabPath,
                Palette = AccessoryVisualPipeline.BurrowPalette,
                Icon = AccessoryVisualPipeline.BurrowIcon,
                Folder = "Companions",
                AssetName = "DEF_Decor_CozyBurrow",
            };
        }

        public static IEnumerable<DecorationSpec> AllSpecs() => BurrowsSpecs().Concat(CozyBurrowSpecs());

        // ---------------------------------------------------------------- menus

        [MenuItem("Pocket Wilds/Content/Update Decoration Definitions (T49B)", priority = 152)]
        public static void UpdateAllMenu() => Debug.Log("[DecorationWizard] " + UpdateAll());

        [MenuItem("Pocket Wilds/Content/Render Decoration Icons (T49B)", priority = 153)]
        public static void RenderIconsMenu() => Debug.Log("[DecorationWizard] " + RenderIcons(AllSpecs()));

        /// <summary>The burrows decorations (palettes, prefabs from the models, definitions) and the Cozy Burrow (adopted prefab).</summary>
        public static string UpdateAll() =>
            BurrowsDecorPipeline.BuildAll() + " | " + CreateOrUpdate(CozyBurrowSpecs(), DefaultRoot, rebuildPrefabs: false);

        // ---------------------------------------------------------------- create or update

        /// <summary>Writes, measures and registers every spec's definition under <paramref name="root"/>. Idempotent.</summary>
        public static string CreateOrUpdate(IEnumerable<DecorationSpec> specs, string root = DefaultRoot, bool rebuildPrefabs = true)
        {
            var list = specs.ToList();
            var material = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.EnvironmentMaterialPath)
                           ?? throw new InvalidOperationException("MAT_Environment_Common absent : lancer GrasslandPipeline d'abord.");
            var database = ContentWizard.Database();
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(AccessoryVisualPipeline.CatalogPath);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            CollisionProxies.ClearCache();
            var report = new List<string>();
            var measured = new Dictionary<string, (CollisionProxies.Proxy proxy, int tris)>();
            foreach (var spec in list)
            {
                if (measured.ContainsKey(spec.PrefabPath))
                    continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                if (!string.IsNullOrEmpty(spec.ModelPath) && (rebuildPrefabs || prefab == null))
                {
                    var meshes = AccessoryVisualPipeline.ModelMeshes(spec.ModelPath, spec.ModelObject);
                    if (meshes.Count == 0)
                    {
                        report.Add($"{spec.Prefab} : modèle absent ({spec.ModelPath})");
                        measured[spec.PrefabPath] = (null, 0);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(spec.PrefabPath) ?? string.Empty);
                    prefab = AccessoryVisualPipeline.SavePrefab(spec.Prefab, spec.PrefabPath, meshes, material, ShadowCastingMode.On);
                }
                if (prefab == null)
                {
                    report.Add($"{spec.Prefab} : prefab absent ({spec.PrefabPath})");
                    measured[spec.PrefabPath] = (null, 0);
                    continue;
                }
                measured[spec.PrefabPath] = (CollisionProxies.Measure(prefab), Lod0Triangles(prefab));
            }

            int written = 0;
            foreach (var spec in list)
            {
                var (proxy, tris) = measured[spec.PrefabPath];
                if (proxy == null || proxy.Bands.Count == 0)
                {
                    report.Add($"{spec.SourceId} : aucune empreinte mesurable");
                    continue;
                }
                string path = DefinitionPath(spec, root);
                BiomeWizard.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
                var definition = AssetDatabase.LoadAssetAtPath<DecorationDefinitionAsset>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<DecorationDefinitionAsset>();
                    AssetDatabase.CreateAsset(definition, path);
                }
                definition.sourceId = spec.SourceId;
                definition.displayKey = spec.DisplayKey;
                definition.category = spec.Category;
                definition.docId = spec.DocId;
                definition.biome = spec.Biome;
                definition.source = spec.Source;
                definition.maxCopies = Mathf.Max(1, spec.MaxCopies);
                definition.interactions = spec.Interactions;
                definition.topHeight = spec.TopHeight;
                definition.trisBudget = spec.TrisBudget;
                definition.prefabAddress = spec.Prefab;
                definition.prefabPath = spec.PrefabPath;
                definition.palette = spec.Palette;
                definition.icon = spec.Icon;
                var ground = proxy.Ground ?? proxy.Bands[0];
                definition.bands = proxy.Bands.Select(b => new DecorationDefinitionAsset.Band
                {
                    a = new Vector3(b.A.x, 0f, b.A.y), b = new Vector3(b.B.x, 0f, b.B.y), radius = b.Radius, bottom = b.Bottom, top = b.Top,
                }).ToList();
                definition.groundHull = ground.Hull.ToList();
                definition.reach = proxy.Bands.SelectMany(b => b.Hull).Max(p => p.magnitude);
                definition.height = proxy.Bands.Max(b => b.Top);
                definition.measuredTris = tris;
                if (spec.Footprint.HasValue && spec.Shape.HasValue)
                {
                    definition.footprint = spec.Footprint.Value;
                    definition.shape = spec.Shape.Value;
                }
                else if (TryFit(definition.groundHull, spec.Shape, out var footprint, out var shape))
                {
                    definition.footprint = spec.Footprint ?? footprint;
                    definition.shape = shape;
                }
                else
                {
                    definition.footprint = DecorationFootprintClass.XL;
                    definition.shape = spec.Shape ?? DecorationFootprintShape.Rectangle;
                    report.Add($"{spec.SourceId} : contour plus grand que la classe XL");
                }
                EditorUtility.SetDirty(definition);

                if (!string.IsNullOrEmpty(spec.Group))
                {
                    var group = settings.FindGroup(spec.Group)
                                ?? BiomeWizard.EnsureGroup(settings, spec.Group, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
                    BiomeWizard.EnsureEntry(settings, group, spec.PrefabPath, spec.Prefab, spec.Label);
                }
                database.decorations.RemoveAll(d => d == null);
                if (!database.decorations.Contains(definition))
                    database.decorations.Add(definition);
                if (spec.SyncCatalog && catalog != null)
                    SyncCatalog(catalog, definition);
                written++;
            }
            EditorUtility.SetDirty(database);
            if (catalog != null)
                EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return $"{written}/{list.Count} définitions de décoration" + (report.Count > 0 ? " ; " + string.Join(" ; ", report) : ".");
        }

        /// <summary>Upserts the CosmeticVisualCatalog.DecorEntry copy of a definition (presentation reads it, the definition is the truth).</summary>
        public static void SyncCatalog(CosmeticVisualCatalog catalog, DecorationDefinitionAsset definition)
        {
            var entry = EntryOf(definition);
            int at = catalog.decorations.FindIndex(e => e != null && e.itemId == definition.sourceId);
            if (at >= 0)
                catalog.decorations[at] = entry;
            else
                catalog.decorations.Add(entry);
        }

        public static CosmeticVisualCatalog.DecorEntry EntryOf(DecorationDefinitionAsset definition) => new CosmeticVisualCatalog.DecorEntry
        {
            itemId = definition.sourceId,
            prefabAddress = definition.prefabAddress,
            paletteRow = definition.palette,
            bands = definition.bands.Select(b => new Obstacle(new Vector2(b.a.x, b.a.z), new Vector2(b.b.x, b.b.z), b.radius, 0f, b.bottom, b.top,
                ObstacleKind.Solid)).ToArray(),
            groundHull = definition.groundHull.ToArray(),
            reach = definition.reach,
            height = definition.height,
        };

        /// <summary>Whether the presentation copy still says what the definition says (prefab, palette, bands, outline, reach, height).</summary>
        public static bool Matches(CosmeticVisualCatalog.DecorEntry entry, DecorationDefinitionAsset definition, out string why)
        {
            var expected = EntryOf(definition);
            why = null;
            if (entry.prefabAddress != expected.prefabAddress || entry.paletteRow != expected.paletteRow)
                why = $"prefab ou palette ({entry.prefabAddress} / {entry.paletteRow})";
            else if (entry.bands == null || entry.bands.Length != expected.bands.Length)
                why = "nombre de bandes";
            else if (entry.groundHull == null || entry.groundHull.Length != expected.groundHull.Length)
                why = "contour au sol";
            else if (Mathf.Abs(entry.reach - expected.reach) > 1e-4f || Mathf.Abs(entry.height - expected.height) > 1e-4f)
                why = "portée ou hauteur";
            else
            {
                for (int i = 0; i < expected.bands.Length && why == null; i++)
                {
                    var a = entry.bands[i];
                    var b = expected.bands[i];
                    if ((a.a - b.a).sqrMagnitude > 1e-8f || (a.b - b.b).sqrMagnitude > 1e-8f || Mathf.Abs(a.radius - b.radius) > 1e-4f
                        || Mathf.Abs(a.bottom - b.bottom) > 1e-4f || Mathf.Abs(a.top - b.top) > 1e-4f || a.kind != b.kind)
                        why = $"bande {i}";
                }
                for (int i = 0; i < expected.groundHull.Length && why == null; i++)
                    if ((entry.groundHull[i] - expected.groundHull[i]).sqrMagnitude > 1e-8f)
                        why = $"point {i} du contour";
            }
            return why == null;
        }

        // ---------------------------------------------------------------- footprint classes

        /// <summary>Whether the outline stays inside the class (circle by radius; rectangle by half sizes, turned 90° if needed).</summary>
        public static bool Fits(IReadOnlyList<Vector2> hull, DecorationFootprintClass footprint, DecorationFootprintShape shape)
        {
            if (hull == null || hull.Count == 0)
                return false;
            if (shape == DecorationFootprintShape.Circle)
                return hull.Max(p => p.magnitude) <= DecorationDefinition.ClassRadius(footprint) + Tolerance;
            DecorationDefinition.ClassRectangle(footprint, out float halfLength, out float halfWidth);
            float x = hull.Max(p => Mathf.Abs(p.x)), z = hull.Max(p => Mathf.Abs(p.y));
            return (x <= halfLength + Tolerance && z <= halfWidth + Tolerance) || (z <= halfLength + Tolerance && x <= halfWidth + Tolerance);
        }

        /// <summary>The smallest class holding the outline; a circle before a rectangle of the same class (or only the given shape).</summary>
        public static bool TryFit(IReadOnlyList<Vector2> hull, DecorationFootprintShape? only, out DecorationFootprintClass footprint, out DecorationFootprintShape shape)
        {
            foreach (DecorationFootprintClass c in Enum.GetValues(typeof(DecorationFootprintClass)))
            {
                foreach (var s in new[] { DecorationFootprintShape.Circle, DecorationFootprintShape.Rectangle })
                {
                    if (only.HasValue && only.Value != s || !Fits(hull, c, s))
                        continue;
                    footprint = c;
                    shape = s;
                    return true;
                }
            }
            footprint = DecorationFootprintClass.XL;
            shape = only ?? DecorationFootprintShape.Rectangle;
            return false;
        }

        /// <summary>Triangles of the LOD0 (the LODGroup's first level, or every mesh when there is none).</summary>
        public static int Lod0Triangles(GameObject prefab)
        {
            var lod = prefab.GetComponent<LODGroup>();
            IEnumerable<Renderer> renderers = lod != null ? lod.GetLODs()[0].renderers.Where(r => r != null) : prefab.GetComponentsInChildren<MeshRenderer>(true);
            int tris = 0;
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    tris += AssetValidator.Triangles(filter.sharedMesh);
            }
            return tris;
        }

        // ---------------------------------------------------------------- icons

        /// <summary>ICO_* from the real prefabs on their palette row, three-quarter front view (IconStudio, Art/UI/Shop).</summary>
        public static string RenderIcons(IEnumerable<DecorationSpec> specs)
        {
            var list = specs.ToList();
            // The studio switches scenes: only plain spec data crosses it, every asset is loaded after.
            var studio = AccessoryVisualPipeline.IconStudio.Open(out string refusal);
            if (studio == null)
                return refusal;
            var missing = new List<string>();
            using (studio)
            {
                foreach (var spec in list)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                    if (prefab == null)
                    {
                        missing.Add(spec.Icon);
                        continue;
                    }
                    var go = Object.Instantiate(prefab);
                    try
                    {
                        studio.Tint(go, spec.Palette);
                        studio.Shoot(go, spec.Icon, spec.IconYaw);
                    }
                    finally
                    {
                        Object.DestroyImmediate(go);
                    }
                }
            }
            return studio.Written.Count + " icônes : " + string.Join(", ", studio.Written.Select(Path.GetFileNameWithoutExtension))
                   + (missing.Count > 0 ? " ; prefab absent : " + string.Join(", ", missing) : string.Empty);
        }

        public static string IconPath(string icon) => $"{AccessoryVisualPipeline.IconsFolder}/{icon}.png";

        // ---------------------------------------------------------------- inspection (report and validator)

        public static List<ContentWizard.Row> DecorationReport()
        {
            var rows = new List<ContentWizard.Row>();
            foreach (var definition in ContentWizard.Database().decorations.Where(d => d != null))
            {
                var row = new ContentWizard.Row { Id = definition.sourceId, Label = definition.name };
                row.Checks.AddRange(Inspect(definition).Select(f => f.ToCheck()));
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>Every check of a decoration, on the disk and in the data.</summary>
        public static List<WizardFinding> Inspect(DecorationDefinitionAsset definition)
        {
            var findings = new List<WizardFinding>();
            var database = ContentWizard.Database();
            var prefab = string.IsNullOrEmpty(definition.prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(definition.prefabPath);
            string prefabName = Path.GetFileNameWithoutExtension(definition.prefabPath ?? string.Empty);
            bool named = prefabName == definition.prefabAddress
                         && (prefabName.StartsWith("PF_Decor_", StringComparison.Ordinal) || prefabName.StartsWith("PF_Landmark_", StringComparison.Ordinal))
                         && (definition.icon ?? string.Empty).StartsWith("ICO_Decor_", StringComparison.Ordinal);
            findings.Add(new WizardFinding("Prefab", prefab != null && named, prefab == null ? $"{definition.prefabPath} absent"
                : named ? definition.prefabAddress : $"noms : {prefabName} / adresse {definition.prefabAddress} / icône {definition.icon} (PF_Decor_*, ICO_Decor_*)"));

            var hull = definition.groundHull ?? new List<Vector2>();
            bool measured = definition.bands.Count > 0 && hull.Count >= 3 && CollisionProxies.HullArea(hull.ToArray()) >= 1e-4f
                            && definition.reach > 0f && definition.height > 0f && definition.bands.All(b => b.radius > 0f && b.top > b.bottom);
            findings.Add(new WizardFinding("Empreinte mesurée", measured, measured
                ? $"{definition.bands.Count} bandes, contour {hull.Count} points, hauteur {definition.height:0.00} m{(definition.ToRuntime().IsFlat ? " (plate : on marche dessus)" : string.Empty)}"
                : "pas de bandes ni de contour au sol : relancer l'assistant"));

            bool fits = Fits(hull, definition.footprint, definition.shape);
            string extent = hull.Count == 0 ? "-" : definition.shape == DecorationFootprintShape.Circle
                ? $"rayon {hull.Max(p => p.magnitude):0.000} m pour {DecorationDefinition.ClassRadius(definition.footprint):0.00}"
                : $"{2f * hull.Max(p => Mathf.Abs(p.x)):0.00} × {2f * hull.Max(p => Mathf.Abs(p.y)):0.00} m";
            findings.Add(new WizardFinding("Classe", fits, $"{definition.footprint} {(definition.shape == DecorationFootprintShape.Circle ? "cercle" : "rectangle")}, {extent}"
                                                              + (fits ? string.Empty : " : le contour dépasse sa classe")));

            int current = prefab != null ? Lod0Triangles(prefab) : 0;
            bool inBudget = definition.measuredTris > 0 && (definition.measuredTris < definition.trisBudget
                                                          || (definition.footprint == DecorationFootprintClass.XL && definition.measuredTris <= definition.trisBudget));
            bool fresh = prefab == null || current == definition.measuredTris;
            findings.Add(new WizardFinding("Budget", inBudget && fresh, !fresh ? $"mesure périmée ({definition.measuredTris} mesurés, {current} dans le prefab) : relancer l'assistant"
                : $"{definition.measuredTris} triangles (budget {definition.trisBudget})"));

            var material = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.EnvironmentMaterialPath);
            var renderers = prefab != null ? prefab.GetComponentsInChildren<MeshRenderer>(true) : Array.Empty<MeshRenderer>();
            var foreign = renderers.Where(r => r.sharedMaterials.Length == 0 || r.sharedMaterials.Any(m => m != material)).Select(r => r.name).ToList();
            findings.Add(new WizardFinding("Matériau partagé", prefab != null && foreign.Count == 0,
                foreign.Count == 0 ? "MAT_Environment_Common" : "autre matériau : " + string.Join(", ", foreign.Take(3))));

            var turning = renderers.Where(r => !r.name.Contains("_Sails_")
                                               && TurningTokens.Any(t => r.name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)).Select(r => r.name).ToList();
            findings.Add(new WizardFinding("Pièces tournantes", turning.Count == 0,
                turning.Count == 0 ? "aucune sans _Sails_" : "une pièce qui tourne porte _Sails_ : " + string.Join(", ", turning)));

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            var entry = settings?.FindAssetEntry(AssetDatabase.AssetPathToGUID(definition.prefabPath ?? string.Empty));
            findings.Add(new WizardFinding("Adresse", entry == null || entry.address == definition.prefabAddress, entry == null
                ? "référence directe (build de base)" : entry.address == definition.prefabAddress ? $"{entry.address} dans {entry.parentGroup?.Name}"
                : $"adresse {entry.address}, attendu {definition.prefabAddress}"));

            bool palette = !string.IsNullOrEmpty(definition.palette)
                           && database.paletteSets.Any(s => s != null && s.palettes.Any(p => p != null && p.sourceId == definition.palette));
            findings.Add(new WizardFinding("Palette", palette, palette ? definition.palette : $"ligne {definition.palette} absente des palettes enregistrées"));

            bool icon = !string.IsNullOrEmpty(definition.icon) && File.Exists(IconPath(definition.icon));
            findings.Add(new WizardFinding("Icône", icon, icon ? IconPath(definition.icon) : $"{IconPath(definition.icon)} absente (Icônes des décorations)"));

            var missing = BiomeWizard.MissingTexts(database, new[] { definition.displayKey });
            findings.Add(new WizardFinding("Textes en et fr", missing.Count == 0, missing.Count == 0 ? definition.displayKey : "manque : " + string.Join(", ", missing)));

            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(AccessoryVisualPipeline.CatalogPath);
            var copy = catalog != null ? catalog.FindDecor(definition.sourceId) : null;
            string why = null;
            bool synced = copy == null ? catalog == null || catalog.burrowItem != definition.sourceId
                                         || (catalog.burrowPalette == definition.palette && catalog.burrowPrefab != null
                                             && AssetDatabase.GetAssetPath(catalog.burrowPrefab) == definition.prefabPath)
                : Matches(copy, definition, out why);
            findings.Add(new WizardFinding("Catalogue visuel", synced, copy != null ? (synced ? "copie de présentation à jour" : "copie différente : " + why)
                : catalog != null && catalog.burrowItem == definition.sourceId ? (synced ? "terrier du catalogue conforme" : "terrier du catalogue : prefab ou palette différents")
                : "pas de copie de présentation"));

            bool registered = database.decorations.Contains(definition);
            findings.Add(new WizardFinding("Base de contenu", registered, registered ? "enregistrée" : "absente de DEF_ContentDatabase.decorations"));
            return findings;
        }
    }
}
