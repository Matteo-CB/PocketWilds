using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Editor.Animals;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Production
{
    /// <summary>
    /// T30 Vertical Slice Gate, automated part (doc 12 VALIDATION): Gate 00 coverage for the VerticalSlice scope with
    /// real file checks (the status file is never trusted on its own), Gate 06/31 materials, Gate 07 textures, Gate 08
    /// prefabs, Gate 16 content data, Gate 30 source traceability and Gate 32 unused assets. Writes
    /// Docs/Gates/T30_Audit.md. It reports; it never deletes or edits assets.
    /// </summary>
    public static class VerticalSliceGate
    {
        public const string StatusPath = "Assets/_Game/Editor/Production/AssetProductionStatus.json";
        public const string ReportPath = "Docs/Gates/T30_Audit.md";

        private static readonly string[] ApprovedShaderPrefixes =
        {
            "Shader Graphs/", "PocketWilds/", "TextMeshPro/", "UI/Default", "Universal Render Pipeline/Particles/",
            "Hidden/",
        };

        [Serializable]
        private sealed class StatusFile
        {
            public AssetEntry[] assets;
        }

        [Serializable]
        private sealed class AssetEntry
        {
            public string id;
            public string name;
            public string family;
            public string releaseScope;
            public string status;
            public string sourcePath;
            public string runtimePath;
        }

        public sealed class CoverageRow
        {
            public string Id, Name, Family, Status, Source, Runtime, Finding;
            public bool Blocking;
        }

        [MenuItem("Pocket Wilds/Gates/Vertical Slice Audit (T30)", priority = 600)]
        public static void RunMenu() => Debug.Log("[VerticalSliceGate] " + Run());

        public static string Run()
        {
            string root = Path.GetDirectoryName(Application.dataPath)!;
            var md = new StringBuilder();
            md.Append("# T30 Vertical Slice Gate — audit automatique\n\n");
            md.Append("Généré par `Pocket Wilds/Gates/Vertical Slice Audit (T30)` le ").Append(DateTime.UtcNow.ToString("u")).Append(".\n");
            md.Append("Chaque ligne est vérifiée sur le disque ; le fichier de statut n'est jamais cru sur parole.\n\n");

            var coverage = Coverage(root);
            WriteCoverage(md, coverage);
            var materials = Materials(md);
            var textures = Textures(md);
            var prefabs = Prefabs(md);
            var content = ContentData(md);
            var trace = Traceability(md, root);
            var unused = Unused(md);

            string full = Path.Combine(root, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, md.ToString(), new UTF8Encoding(false));
            int blocking = coverage.Count(r => r.Blocking);
            return $"coverage {coverage.Count} rows ({blocking} blocking), materials {materials}, textures {textures}, prefabs {prefabs}, " +
                   $"content {content}, traceability {trace}, unused {unused} -> {ReportPath}";
        }

        // ---------------- Gate 00 ----------------

        public static List<CoverageRow> Coverage(string root)
        {
            var file = JsonUtility.FromJson<StatusFile>(File.ReadAllText(Path.Combine(root, StatusPath)));
            var rows = new List<CoverageRow>();
            foreach (var a in file.assets.Where(a => a.releaseScope == "VerticalSlice"))
            {
                var src = Check(root, a.sourcePath, a);
                var run = Check(root, a.runtimePath, a);
                var row = new CoverageRow { Id = a.id, Name = a.name, Family = a.family, Status = a.status, Source = src, Runtime = run };
                bool integrated = a.status is "INTEGRATED" or "PERFORMANCE_REVIEW" or "DONE";
                bool runtimeKnown = run is "present" or "missing";
                if (integrated && run == "missing")
                {
                    row.Finding = "statut en avance : runtime absent";
                    row.Blocking = true;
                }
                else if (integrated && src == "missing")
                {
                    row.Finding = "statut en avance : source absente";
                    row.Blocking = true;
                }
                else if (a.status == "PLANNED" && run == "present")
                    row.Finding = "statut en retard : runtime présent";
                else if (a.status == "PLANNED")
                {
                    row.Finding = "à produire";
                    row.Blocking = true;
                }
                else if (a.status is "BLOCKOUT" or "ART_REVIEW")
                {
                    row.Finding = runtimeKnown && run == "missing" ? "en cours, runtime absent" : "en cours (revue)";
                    row.Blocking = true;
                }
                else if (!runtimeKnown && src is not ("present"))
                    row.Finding = "aucun chemin vérifiable";
                else
                    row.Finding = "ok";
                rows.Add(row);
            }
            return rows;
        }

        private static string Check(string root, string path, AssetEntry entry)
        {
            if (string.IsNullOrEmpty(path))
                return "-";
            if (path.IndexOfAny(new[] { '<', '*', '{' }) >= 0)
                return "motif";
            string full = Path.Combine(root, path);
            if (File.Exists(full))
                return "present";
            if (!Directory.Exists(full))
                return "missing";
            // A folder proves something only when it belongs to this item (its name appears in the item name or id);
            // a shared folder such as Assets/_Game/Content/ always holds other files.
            string folder = Path.GetFileName(path.TrimEnd('/', '\\'));
            string key = (entry.name + " " + entry.id).Replace(" ", "").Replace("_", "").ToLowerInvariant();
            if (!key.Contains(folder.Replace("_", "").ToLowerInvariant()))
                return "dossier générique";
            return Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories).Any(f => !f.EndsWith(".meta")) ? "present" : "missing";
        }

        private static void WriteCoverage(StringBuilder md, List<CoverageRow> rows)
        {
            int total = rows.Count;
            int done = rows.Count(r => r.Status == "DONE");
            int integrated = rows.Count(r => r.Status is "INTEGRATED" or "PERFORMANCE_REVIEW");
            int review = rows.Count(r => r.Status is "BLOCKOUT" or "ART_REVIEW");
            int planned = rows.Count(r => r.Status == "PLANNED");
            md.Append("## Gate 00 — couverture VerticalSlice\n\n");
            md.Append($"| Total | DONE | INTEGRATED ou PERFORMANCE_REVIEW | En revue (BLOCKOUT, ART_REVIEW) | PLANNED | Bloquants |\n|---|---|---|---|---|---|\n");
            md.Append($"| {total} | {done} | {integrated} | {review} | {planned} | {rows.Count(r => r.Blocking)} |\n\n");
            md.Append("Écarts entre statut et réalité :\n\n| ID | Nom | Statut | Source | Runtime | Constat |\n|---|---|---|---|---|---|\n");
            foreach (var r in rows.Where(r => r.Finding.StartsWith("statut")))
                md.Append($"| {r.Id} | {r.Name} | {r.Status} | {r.Source} | {r.Runtime} | {r.Finding} |\n");
            md.Append("\nÉléments bloquants (à produire ou en revue) :\n\n| ID | Nom | Famille | Statut | Constat |\n|---|---|---|---|---|\n");
            foreach (var r in rows.Where(r => r.Blocking).OrderBy(r => r.Family).ThenBy(r => r.Id))
                md.Append($"| {r.Id} | {r.Name} | {r.Family} | {r.Status} | {r.Finding} |\n");
            md.Append('\n');
        }

        // ---------------- Gates 06 / 31 ----------------

        private static string Materials(StringBuilder md)
        {
            var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game" });
            var byShader = new Dictionary<string, List<string>>();
            var byHash = new Dictionary<string, List<string>>();
            var unapproved = new List<string>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                    continue;
                string shader = mat.shader != null ? mat.shader.name : "(none)";
                if (!byShader.TryGetValue(shader, out var list))
                    byShader[shader] = list = new List<string>();
                list.Add(path);
                if (!ApprovedShaderPrefixes.Any(p => shader.StartsWith(p, StringComparison.Ordinal)))
                    unapproved.Add($"{path} ({shader})");
                string hash = MaterialHash(mat);
                if (!byHash.TryGetValue(hash, out var same))
                    byHash[hash] = same = new List<string>();
                same.Add(path);
            }
            var duplicates = byHash.Values.Where(v => v.Count > 1).ToList();
            md.Append("## Gates 06 et 31 — matériaux\n\n");
            md.Append($"{guids.Length} matériaux dans Assets/_Game.\n\n| Shader | Matériaux |\n|---|---|\n");
            foreach (var kv in byShader.OrderByDescending(k => k.Value.Count))
                md.Append($"| {kv.Key} | {kv.Value.Count} |\n");
            md.Append($"\nShaders non approuvés : {(unapproved.Count == 0 ? "aucun" : string.Join(", ", unapproved))}.\n\n");
            md.Append($"Doublons exacts (mêmes shader et propriétés) : {(duplicates.Count == 0 ? "aucun" : string.Join(" ; ", duplicates.Select(d => string.Join(" = ", d))))}.\n\n");
            return $"{guids.Length} ({unapproved.Count} unapproved, {duplicates.Count} duplicate groups)";
        }

        private static string MaterialHash(Material mat)
        {
            var sb = new StringBuilder(mat.shader != null ? mat.shader.name : "");
            var so = new SerializedObject(mat);
            var props = so.FindProperty("m_SavedProperties");
            if (props != null)
            {
                var it = props.Copy();
                var end = props.GetEndProperty();
                while (it.NextVisible(true) && !SerializedProperty.EqualContents(it, end))
                {
                    switch (it.propertyType)
                    {
                        case SerializedPropertyType.Float: sb.Append(it.propertyPath).Append('=').Append(it.floatValue.ToString("R")).Append(';'); break;
                        case SerializedPropertyType.Color: sb.Append(it.propertyPath).Append('=').Append(it.colorValue).Append(';'); break;
                        case SerializedPropertyType.ObjectReference:
                            sb.Append(it.propertyPath).Append('=').Append(it.objectReferenceValue != null ? it.objectReferenceValue.GetInstanceID() : 0).Append(';');
                            break;
                        case SerializedPropertyType.Vector4: sb.Append(it.propertyPath).Append('=').Append(it.vector4Value).Append(';'); break;
                    }
                }
            }
            sb.Append(string.Join(",", mat.shaderKeywords.OrderBy(k => k)));
            return sb.ToString();
        }

        // ---------------- Gate 07 ----------------

        private static string Textures(StringBuilder md)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Art" });
            var rows = new List<(string path, int w, int h, string format, bool mips, long bytes)>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                    continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                string format = "?";
                if (importer != null)
                {
                    var android = importer.GetPlatformTextureSettings("Android");
                    format = android.overridden ? android.format.ToString() : importer.textureCompression.ToString();
                }
                long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                rows.Add((path, tex.width, tex.height, format, tex.mipmapCount > 1, bytes));
            }
            long total = rows.Sum(r => r.bytes);
            var oversized = rows.Where(r => Math.Max(r.w, r.h) > 1024 && !r.path.Contains("Atlas")).ToList();
            md.Append("## Gate 07 — textures (Assets/_Game/Art)\n\n");
            md.Append($"{rows.Count} textures, {total / 1048576f:F1} Mo en mémoire éditeur (indicatif, la compression Android réduit).\n\n");
            md.Append("| Texture | Taille | Format Android | Mipmaps | Mémoire (Ko) |\n|---|---|---|---|---|\n");
            foreach (var r in rows.OrderByDescending(r => r.bytes).Take(20))
                md.Append($"| {r.path.Replace("Assets/_Game/Art/", "")} | {r.w}x{r.h} | {r.format} | {(r.mips ? "oui" : "non")} | {r.bytes / 1024} |\n");
            md.Append($"\nTextures de plus de 1024 px hors atlas : {(oversized.Count == 0 ? "aucune" : string.Join(", ", oversized.Select(o => o.path)))}.\n\n");
            return $"{rows.Count} ({oversized.Count} oversized)";
        }

        // ---------------- Gate 08 ----------------

        private static readonly string[] ForbiddenTypeNames = { "MeshCollider", "Rigidbody", "NavMeshAgent", "AudioSource", "CharacterController" };

        private static string Prefabs(StringBuilder md)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" });
            var findings = new List<string>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                    continue;
                bool world = !path.Contains("/UI/");
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null)
                    {
                        findings.Add($"{path} : script manquant");
                        continue;
                    }
                    var type = c.GetType();
                    if (ForbiddenTypeNames.Contains(type.Name))
                        findings.Add($"{path} : composant interdit {type.Name} sur {c.gameObject.name}");
                    if (world && c is MonoBehaviour && HasUnityTick(type))
                        findings.Add($"{path} : {type.Name} a un Update/LateUpdate/FixedUpdate");
                    if (c is Renderer renderer)
                    {
                        foreach (var m in renderer.sharedMaterials)
                        {
                            if (m != null && !AssetDatabase.Contains(m))
                                findings.Add($"{path} : matériau non asset (instance) sur {renderer.name}");
                        }
                    }
                }
            }
            md.Append("## Gate 08 — prefabs\n\n");
            md.Append($"{guids.Length} prefabs dans Assets/_Game/Prefabs. Interdits cherchés : {string.Join(", ", ForbiddenTypeNames)}, " +
                      "Update/LateUpdate/FixedUpdate dans les prefabs du monde, matériaux instanciés, scripts manquants.\n\n");
            md.Append(findings.Count == 0 ? "Aucun problème.\n\n" : string.Join("\n", findings.Select(f => "- " + f)) + "\n\n");
            return $"{guids.Length} ({findings.Count} findings)";
        }

        private static bool HasUnityTick(Type type)
        {
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                if (t.GetMethod("Update", flags, null, Type.EmptyTypes, null) != null
                    || t.GetMethod("LateUpdate", flags, null, Type.EmptyTypes, null) != null
                    || t.GetMethod("FixedUpdate", flags, null, Type.EmptyTypes, null) != null)
                    return true;
            }
            return false;
        }

        // ---------------- Gate 16 ----------------

        private static string ContentData(StringBuilder md)
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var builder = database.CreateBuilder();
            var issues = builder.Issues;
            md.Append("## Gate 16 — données de contenu\n\n");
            if (issues.Count == 0)
            {
                var registry = builder.Build();
                md.Append($"Aucune erreur. {registry.Species.Count} espèces, {registry.Variants.Count} variantes, {registry.Eggs.Count} œufs, " +
                          $"{registry.Biomes.Count} biome(s), {registry.Shop.Products.Count} produits, {registry.Shop.Offers.Count} offres.\n\n");
                return "0 issues";
            }
            md.Append(string.Join("\n", issues.Select(i => "- " + i)) + "\n\n");
            return $"{issues.Count} issues ({(builder.HasErrors ? "errors" : "warnings only")})";
        }

        // ---------------- Gate 30 ----------------

        private static string Traceability(StringBuilder md, string root)
        {
            var models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Game/Art" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            var rows = new List<(string model, string source)>();
            foreach (var model in models)
                rows.Add((model, SourceFor(root, model)));
            var orphans = rows.Where(r => r.source == null).ToList();
            md.Append("## Gate 30 — traçabilité des sources\n\n| Modèle runtime | Source Blender |\n|---|---|\n");
            foreach (var r in rows)
                md.Append($"| {r.model.Replace("Assets/_Game/Art/", "")} | {r.source ?? "**introuvable**"} |\n");
            md.Append("\nTextures et sprites générés : SourceArt/UI/Tools/pw_ui_sprites.py (UI), SourceArt/Blender/Tools/pw_masks.py (masques), " +
                      "maillages de kit cuits depuis les FBX par GrasslandPipeline.BakeKitMeshes.\n\n");
            return $"{rows.Count} models ({orphans.Count} without source)";
        }

        private static string SourceFor(string root, string modelPath)
        {
            string rel = modelPath.Replace("Assets/_Game/Art/", "");
            string[] parts = rel.Split('/');
            string blender = Path.Combine(root, "SourceArt", "Blender");
            string dir = parts[0] switch
            {
                "Animals" => Path.Combine(blender, "Animals", parts[1]),
                "Animations" => Path.Combine(blender, "Rigs"),
                "Eggs" => Path.Combine(blender, "Eggs"),
                "Environment" => Path.Combine(blender, "Environment", parts[1]),
                "Rigs" => Path.Combine(blender, "Rigs"),
                _ => null,
            };
            if (dir == null || !Directory.Exists(dir))
                return null;
            var blend = Directory.GetFiles(dir, "*.blend").FirstOrDefault();
            return blend == null ? null : blend.Substring(root.Length + 1).Replace('\\', '/');
        }

        // ---------------- Gate 32 ----------------

        private static string Unused(StringBuilder md)
        {
            var roots = new List<string>();
            roots.AddRange(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
            roots.Add(AnimalPipeline.ContentDatabasePath);
            roots.AddRange(AssetDatabase.FindAssets("t:AddressableAssetGroup").Select(AssetDatabase.GUIDToAssetPath));
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                foreach (var group in settings.groups.Where(g => g != null))
                    roots.AddRange(group.entries.Select(e => e.AssetPath));
            }
            var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
            var candidates = AssetDatabase.FindAssets("", new[] { "Assets/_Game/Art", "Assets/_Game/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Distinct()
                .ToList();
            var unused = candidates.Where(p => !used.Contains(p)).OrderBy(p => p).ToList();
            md.Append("## Gate 32 — assets non référencés\n\n");
            md.Append($"Racines : scènes du build, base de contenu, entrées Addressables. {candidates.Count} assets dans Art et Prefabs, " +
                      $"{unused.Count} non atteints depuis ces racines (rapport seulement, rien n'est supprimé ; les sources d'export FBX " +
                      "et les assets d'outils éditeur apparaissent ici normalement).\n\n");
            foreach (var p in unused)
                md.Append("- ").Append(p.Replace("Assets/_Game/", "")).Append('\n');
            md.Append('\n');
            return $"{unused.Count} unreferenced of {candidates.Count}";
        }
    }
}
