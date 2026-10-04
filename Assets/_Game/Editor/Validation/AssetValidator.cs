using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Editor.Animals;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Validation
{
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error,
    }

    public readonly struct ValidationIssue
    {
        public readonly IssueSeverity Severity;
        public readonly string AssetPath;
        public readonly string Rule;
        public readonly string Message;

        public ValidationIssue(IssueSeverity severity, string assetPath, string rule, string message)
        {
            Severity = severity;
            AssetPath = assetPath;
            Rule = rule;
            Message = message;
        }

        public override string ToString() => $"{Severity} [{Rule}] {AssetPath}: {Message}";
    }

    /// <summary>
    /// ED-007 Asset Validator (v1: animal prefabs and models). Errors block promotion to production.
    /// Budgets are ceilings from the Master Asset Manifest, not targets.
    /// </summary>
    public static class AssetValidator
    {
        public const string AnimalPrefabFolder = "Assets/_Game/Prefabs/Animals";
        public const int MaxBonesSmallFamily = 32;

        /// <summary>Per-species LOD ceilings (triangles). Missing species fall back to the small-animal defaults.</summary>
        public static readonly Dictionary<string, int[]> LodBudgets = new Dictionary<string, int[]>
        {
            ["Rabbit"] = new[] { 2600, 1000, 450 },
            ["Squirrel"] = new[] { 2800, 1100, 500 },
            ["Hedgehog"] = new[] { 3000, 1200, 500 },
        };

        public static readonly int[] DefaultSmallAnimalBudget = { 3000, 1200, 500 };

        private static readonly string[] RequiredSockets = { "SOCK_HeadTop", "SOCK_Neck", "SOCK_Back", "SOCK_TailBase" };

        [MenuItem("Pocket Wilds/Validate/Animal Prefabs", priority = 400)]
        public static void ValidateAllMenu()
        {
            var issues = ValidateAllAnimals();
            Report(issues);
        }

        public static void ValidateAllBatch()
        {
            var issues = ValidateAllAnimals();
            Report(issues);
            EditorApplication.Exit(issues.Any(i => i.Severity == IssueSeverity.Error) ? 1 : 0);
        }

        public static List<ValidationIssue> ValidateAllAnimals()
        {
            var issues = new List<ValidationIssue>();
            if (!AssetDatabase.IsValidFolder(AnimalPrefabFolder))
                return issues;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { AnimalPrefabFolder }))
                issues.AddRange(ValidateAnimalPrefab(AssetDatabase.GUIDToAssetPath(guid)));
            return issues;
        }

        public static List<ValidationIssue> ValidateAnimalPrefab(string path)
        {
            var issues = new List<ValidationIssue>();
            void Error(string rule, string msg) => issues.Add(new ValidationIssue(IssueSeverity.Error, path, rule, msg));
            void Warn(string rule, string msg) => issues.Add(new ValidationIssue(IssueSeverity.Warning, path, rule, msg));
            void Info(string rule, string msg) => issues.Add(new ValidationIssue(IssueSeverity.Info, path, rule, msg));

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (!fileName.StartsWith("PF_Animal_", StringComparison.Ordinal))
                Error("Naming", "Animal prefabs are named PF_Animal_<Species>.");
            string species = fileName.Replace("PF_Animal_", string.Empty);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Error("Load", "Prefab cannot be loaded.");
                return issues;
            }

            var view = prefab.GetComponent<AnimalView>();
            if (view == null)
                Error("AnimalView", "Root must carry AnimalView.");

            // Forbidden runtime components (Performance Bible).
            foreach (var c in prefab.GetComponentsInChildren<Component>(true))
            {
                if (c == null)
                {
                    Error("MissingScript", "Missing script reference.");
                    continue;
                }
                if (c is Collider) Error("Forbidden", $"Collider on {c.name}: animals use centralized selection.");
                if (c is Rigidbody) Error("Forbidden", $"Rigidbody on {c.name}.");
                if (c.GetType().Name == "NavMeshAgent") Error("Forbidden", $"NavMeshAgent on {c.name}.");
                if (c is AudioSource) Error("Forbidden", $"AudioSource on {c.name}: sounds go through AudioService.");
                if (c is MonoBehaviour mb && !(mb is AnimalView))
                    Error("Forbidden", $"Unexpected script {mb.GetType().Name} on {c.name}.");
            }

            var lodGroup = prefab.GetComponentInChildren<LODGroup>(true);
            var lods = lodGroup != null ? lodGroup.GetLODs() : Array.Empty<LOD>();
            if (lodGroup == null || lods.Length != 3)
                Error("LODGroup", $"Need a LODGroup with 3 LODs (found {lods.Length}).");

            var budget = LodBudgets.TryGetValue(species, out var b) ? b : DefaultSmallAnimalBudget;
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var materials = new HashSet<Material>();
            for (int lod = 0; lod < lods.Length; lod++)
            {
                foreach (var r in lods[lod].renderers)
                {
                    if (r == null)
                    {
                        Error("LODGroup", $"LOD{lod} has an empty renderer slot.");
                        continue;
                    }
                    if (!r.name.EndsWith("_LOD" + lod, StringComparison.Ordinal))
                        Error("Naming", $"LOD{lod} renderer is {r.name}, expected *_LOD{lod}.");
                    int tris = Triangles(r);
                    if (lod < budget.Length && tris > budget[lod])
                        Error("Budget", $"{r.name}: {tris} tris > ceiling {budget[lod]}.");
                    else
                        Info("Budget", $"{r.name}: {tris} tris (ceiling {(lod < budget.Length ? budget[lod] : 0)}).");
                }
            }

            foreach (var r in renderers)
            {
                if (r.sharedMaterials.Length != 1)
                    Error("Materials", $"{r.name} has {r.sharedMaterials.Length} materials; one shared material expected.");
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null)
                    {
                        Error("Materials", $"{r.name} has an empty material slot.");
                        continue;
                    }
                    materials.Add(m);
                    if (m.shader == null || m.shader.name != AnimalPipeline.ShaderName)
                        Error("Shader", $"{r.name} uses {(m.shader ? m.shader.name : "none")}, only {AnimalPipeline.ShaderName} is approved.");
                    if (AssetDatabase.GetAssetPath(m) != AnimalPipeline.MaterialPath)
                        Error("Materials", $"{r.name} uses {AssetDatabase.GetAssetPath(m)}; animals share {AnimalPipeline.MaterialPath}.");
                }
                if (r is SkinnedMeshRenderer smr)
                {
                    if (smr.bones.Length > MaxBonesSmallFamily)
                        Warn("Bones", $"{r.name}: {smr.bones.Length} bones.");
                    if (smr.updateWhenOffscreen)
                        Error("Skinning", $"{r.name}: updateWhenOffscreen must be off.");
                    var mesh = smr.sharedMesh;
                    if (mesh != null && mesh.isReadable)
                        Error("ReadWrite", $"{mesh.name}: Read/Write must be disabled.");
                }
            }
            if (materials.Count > 1)
                Error("Materials", $"{materials.Count} distinct materials; animals use exactly one.");

            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
                Error("Animator", "Animator with a family controller is required.");
            else
            {
                if (animator.cullingMode != AnimatorCullingMode.CullCompletely)
                    Error("Animator", "Animator culling must be CullCompletely.");
                if (animator.applyRootMotion)
                    Error("Animator", "Root motion must be off; movement comes from the simulation.");
            }

            foreach (var socket in RequiredSockets)
            {
                if (AnimalPipeline.FindDeep(prefab.transform, socket) == null)
                    Error("Sockets", $"Missing {socket}.");
            }

            string modelPath = AnimalPipeline.ModelPath(species);
            if (AssetImporter.GetAtPath(modelPath) is ModelImporter importer)
            {
                if (importer.animationType != ModelImporterAnimationType.Generic)
                    Error("Import", "Rig must be Generic.");
                if (importer.isReadable)
                    Error("Import", "Model Read/Write must be disabled.");
                if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
                    Error("Import", "FBX materials must not be imported; renderers use the master material.");
            }
            else
            {
                Error("Import", "Model not found at " + modelPath);
            }

            return issues;
        }

        public static int Triangles(Renderer renderer)
        {
            Mesh mesh = renderer is SkinnedMeshRenderer s ? s.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null)
                return 0;
            long indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                indices += mesh.GetIndexCount(i);
            return (int)(indices / 3);
        }

        public static void Report(List<ValidationIssue> issues)
        {
            var sb = new StringBuilder();
            int errors = issues.Count(i => i.Severity == IssueSeverity.Error);
            int warnings = issues.Count(i => i.Severity == IssueSeverity.Warning);
            sb.AppendLine($"[AssetValidator] {errors} error(s), {warnings} warning(s).");
            foreach (var issue in issues)
                sb.AppendLine(issue.ToString());
            if (errors > 0)
                Debug.LogError(sb.ToString());
            else
                Debug.Log(sb.ToString());
        }
    }
}
