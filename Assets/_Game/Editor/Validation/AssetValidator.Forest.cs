using System;
using System.Collections.Generic;
using System.Linq;
using PocketWilds.Editor.Environment;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Validation
{
    /// <summary>
    /// T50 the Forest's kit and chunks, by the Grassland's rules: plain renderers on the shared world materials, the budgets of
    /// the brief (trees 0.6-1.8k with LOD1 35-55 % and LOD2 15-25 %, landmarks up to 2.5k, bushes 150-500, ferns up to 220,
    /// mushrooms up to 200, moss rocks 60-250 and the hero under 600, props under 600, grounds like the strip's), every
    /// renderer bound to a palette, scatter data, collision bands and ground heights, three animal spawns standing clear, no
    /// prop entering another (the ForestEdge seam included), the Forest's life on the instanced life material.
    /// </summary>
    public static partial class AssetValidator
    {
        /// <summary>Forest budgets (LOD0 triangles): first matching prefix wins.</summary>
        public static readonly (string Prefix, int Min, int Max)[] ForestBudgets =
        {
            ("FO_Tree_A_LOD0", 600, 1800), ("FO_Tree_B_LOD0", 600, 1800), ("FO_Tree_C_LOD0", 600, 1800), ("FO_Tree_D_LOD0", 600, 1800),
            ("FO_Landmark_AncientTree_LOD0", 800, 2500), ("FO_Landmark_GiantStump_LOD0", 800, 2500),
            ("FO_Bush_", 150, 500), ("FO_Fern_", 20, 220), ("FO_Mushroom_", 20, 200),
            ("FO_MossRock_Hero", 60, 600), ("FO_MossRock_", 60, 250),
            ("FO_Stump", 24, 600), ("FO_HollowLog", 24, 600), ("FO_RootArch", 24, 600), ("FO_TinyBridge", 24, 600), ("FO_SteppingStones", 24, 600),
            // The brook's ground is finer to carve its bed, like the Prairie's stream and pond grounds.
            ("FO_Ground_Brook", 200, 8000), ("FO_Ground_", 200, 6000),
        };

        [MenuItem("Pocket Wilds/Validate/Forest Kit (T50)", priority = 404)]
        public static void ValidateForestMenu() => Report(ValidateForest());

        public static List<ValidationIssue> ValidateForest()
        {
            var issues = new List<ValidationIssue>();
            var allowed = new HashSet<Material>(EnvironmentMaterialPaths.Select(AssetDatabase.LoadAssetAtPath<Material>));
            if (!AssetDatabase.IsValidFolder(ForestPipeline.PrefabsFolder))
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, ForestPipeline.PrefabsFolder, "Load", "Forest kit missing (Pocket Wilds/Environment/Build Forest Magnifique (T50))."));
                return issues;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ForestPipeline.PrefabsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                CheckPlainProps(prefab, path, allowed, issues, allowChunkRoot: false);
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null)
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Mesh", filter.name + " has no mesh."));
                        continue;
                    }
                    // Decision 8: kit pieces use baked standalone meshes, never an FBX sub-mesh (it would drag the whole model).
                    if (AssetDatabase.GetAssetPath(mesh).EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Mesh", $"{mesh.name} comes from an FBX: bake it (ForestPipeline.Kit)."));
                    foreach (var (prefix, min, max) in ForestBudgets)
                    {
                        if (!mesh.name.StartsWith(prefix, StringComparison.Ordinal))
                            continue;
                        int tris = (int)(mesh.GetIndexCount(0) / 3);
                        var severity = tris < min || tris > max ? IssueSeverity.Error : IssueSeverity.Info;
                        issues.Add(new ValidationIssue(severity, path, "Budget", $"{mesh.name}: {tris} tris ({min}-{max})."));
                        break;
                    }
                }
                bool lods = prefab.name.StartsWith("PF_FO_Tree", StringComparison.Ordinal) || prefab.name.StartsWith("PF_FO_Landmark", StringComparison.Ordinal);
                if (!lods)
                    continue;
                var group = prefab.GetComponent<LODGroup>();
                var levels = group != null ? group.GetLODs() : Array.Empty<LOD>();
                if (levels.Length != 3)
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Error, path, "LODGroup", "Trees and landmarks need LOD0/1/2."));
                    continue;
                }
                float t0 = Triangles(levels[0].renderers[0]);
                float r1 = Triangles(levels[1].renderers[0]) / t0, r2 = Triangles(levels[2].renderers[0]) / t0;
                if (r1 < 0.35f || r1 > 0.55f || r2 < 0.15f || r2 > 0.25f)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, path, "LOD", $"LOD ratios {r1:P0} / {r2:P0}, expected 35-55% / 15-25%."));
            }

            foreach (var (address, path, _) in ForestPipeline.ChunkEntries)
                ValidateChunk(path, allowed, issues, needsEgg: false);

            var life = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(ForestPipeline.LifeKitPath);
            if (life == null)
                issues.Add(new ValidationIssue(IssueSeverity.Error, ForestPipeline.LifeKitPath, "Load", "Forest life kit missing."));
            else
            {
                if (life.material == null || !life.material.enableInstancing || life.material.shader.name != "PocketWilds/AmbientLife")
                    issues.Add(new ValidationIssue(IssueSeverity.Error, ForestPipeline.LifeKitPath, "Material", "Life uses MAT_AmbientLife (PocketWilds/AmbientLife) with instancing."));
                foreach (var species in life.species)
                {
                    int tris = species.mesh != null ? Triangles(species.mesh) : 0;
                    var severity = tris == 0 || tris > ScatterMaxTriangles ? IssueSeverity.Error : IssueSeverity.Info;
                    issues.Add(new ValidationIssue(severity, ForestPipeline.LifeKitPath, "Budget", $"{species.id}: {tris} tris (max {ScatterMaxTriangles})."));
                }
            }

            var forestChunks = new HashSet<string>(ForestPipeline.ChunkEntries.Select(e => System.IO.Path.GetFileNameWithoutExtension(e.path)));
            foreach (var (a, b, depth) in GrasslandPipeline.PropOverlaps())
            {
                if (!forestChunks.Contains(a.Chunk) && !forestChunks.Contains(b.Chunk))
                    continue;
                issues.Add(new ValidationIssue(IssueSeverity.Error, a.Chunk, "Collision",
                    $"{a.Prop} at ({a.Placed.Position.x:F2}, {a.Placed.Position.z:F2}) enters {b.Chunk}/{b.Prop} by {depth * 100f:F1} cm."));
            }
            return issues;
        }
    }
}
