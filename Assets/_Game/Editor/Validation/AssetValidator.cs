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
    /// ED-007 Asset Validator (v2: animal prefabs, egg models). Errors block promotion to production.
    /// Budgets are ceilings from the Master Asset Manifest, not targets.
    /// </summary>
    public static partial class AssetValidator
    {
        public const string AnimalPrefabFolder = "Assets/_Game/Prefabs/Animals";
        public const int MaxBonesSmallFamily = 32;

        /// <summary>Per-species LOD ceilings (triangles). Missing species fall back to the small-animal defaults.</summary>
        public static readonly Dictionary<string, int[]> LodBudgets = new Dictionary<string, int[]>
        {
            ["Rabbit"] = new[] { 2600, 1000, 450 },
            ["Squirrel"] = new[] { 2800, 1100, 500 },
            ["Hedgehog"] = new[] { 3000, 1200, 500 },
            // T46: the first medium animal (Manifest « moyen »: 2.5k to 4k triangles at LOD0).
            ["Fawn"] = new[] { 4000, 1600, 800 },
            // T51 Forest species (A-SP-007 to A-SP-011): the top of each doc range.
            ["Fox"] = new[] { 3800, 1500, 650 },
            ["Badger"] = new[] { 3400, 1300, 600 },
            ["Raccoon"] = new[] { 3000, 1200, 500 },
            ["RedPanda"] = new[] { 3400, 1400, 600 },
        };

        public static readonly int[] DefaultSmallAnimalBudget = { 3000, 1200, 500 };

        private static readonly string[] RequiredSockets = { "SOCK_HeadTop", "SOCK_Neck", "SOCK_Back", "SOCK_TailBase" };

        public const string EggBaseModelPath = "Assets/_Game/Art/Eggs/Models/SM_Egg_Base.fbx";
        public const string EggFragmentsModelPath = "Assets/_Game/Art/Eggs/Models/SM_Egg_Fragments.fbx";
        public const string EggMaskPath = "Assets/_Game/Art/Eggs/Textures/T_EggMask_Common.png";

        /// <summary>Egg family ceilings (05 Eggs prompts): base LOD0 250-400, LOD1 100-180, fragments under 300 in total.</summary>
        public static readonly (string Mesh, int Min, int Max)[] EggBaseBudgets =
        {
            ("SM_Egg_Base_LOD0", 250, 400),
            ("SM_Egg_Base_LOD1", 100, 180),
        };

        public static readonly string[] EggFragments = { "SM_Egg_Fragment_A", "SM_Egg_Fragment_B", "SM_Egg_Fragment_C" };
        public const int EggFragmentsMaxTotal = 300;
        public const string EggPrefabPath = "Assets/_Game/Prefabs/Eggs/PF_Egg_Base.prefab";
        public const string EggMaterialPath = "Assets/_Game/Art/Materials/MAT_Egg_Master.mat";

        public const string GrasslandPrefabFolder = "Assets/_Game/Prefabs/Environment/Grassland";
        public const string GrasslandChunkPath = "Assets/_Game/Prefabs/World/Chunks/Grassland/PF_Chunk_Grassland_Start.prefab";
        public const string GrasslandMeadowChunkPath = "Assets/_Game/Prefabs/World/Chunks/Grassland/PF_Chunk_Grassland_Meadow.prefab";
        public static readonly string[] EnvironmentMaterialPaths =
        {
            "Assets/_Game/Art/Materials/MAT_Environment_Common.mat",
            "Assets/_Game/Art/Materials/MAT_Ground_Grassland.mat",
            // T33B: the stream and the pond (SH_WaterLite master).
            "Assets/_Game/Art/Materials/MAT_Water_Grassland.mat",
            // T39: the tall grass props sway with the meadow's tufts, on their material (SH_FoliageWind, already drawn by the
            // ScatterRenderer in every chunk).
            "Assets/_Game/Art/Materials/MAT_Foliage_Grassland.mat",
        };

        /// <summary>Biomes doc budgets (triangles, LOD0): name prefix, min, max.</summary>
        public static readonly (string Prefix, int Min, int Max)[] EnvironmentBudgets =
        {
            ("GL_GrassTuft_", 20, 60),
            ("GL_GrassWisp_", 20, 60),
            ("GL_Flower_", 20, 150),
            ("GL_Rock_", 60, 250),
            ("GL_Tree_A_LOD0", 600, 1000),
            ("GL_Ground_Core_", 200, 2500),
            // T33 (doc 04 shared prop construction): hero trees 0.6-1.8k, hero rocks under 600, small props stay light.
            ("GL_Tree_B_LOD0", 600, 1800),
            ("GL_Tree_C_LOD0", 600, 1800),
            ("GL_Tree_D_LOD0", 600, 1800),
            ("GL_RockHero_", 60, 600),
            ("GL_Bush_", 150, 450),
            ("GL_Log", 24, 400),
            ("GL_Stump", 24, 400),
            ("GL_Fence_", 24, 400),
            ("GL_HayBale", 24, 400),
            ("GL_BirdBath", 24, 400),
            // T33B: strip grounds (the stream and pond grounds are finer to carve their beds), landmarks, last props.
            ("GL_Ground_StreamBend", 200, 8000),
            ("GL_Ground_PondEdge", 200, 8000),
            ("GL_Ground_Orchard", 200, 5000),
            ("GL_Ground_Hill", 200, 5000),
            ("GL_Ground_WindmillRise", 200, 5000),
            ("GL_Ground_ForestEdge", 200, 5000),
            ("GL_Landmark_OldTree_LOD0", 600, 1800),
            ("GL_Landmark_Windmill_LOD0", 300, 2500),
            ("GL_Landmark_Windmill_Sails_LOD0", 24, 600),
            ("GL_WellTiny", 24, 400),
            ("GL_Edge_", 24, 400),
            ("GL_StreamStones", 24, 300),
            // T39 (agent HB's budgets: tall grass 500, flowered bush 650; a patch merges two clumps).
            ("GL_TallGrass_", 150, 600),
            ("GL_TallGrassPatch_", 300, 1500),
            ("GL_FlowerBush_", 200, 800),
        };

        /// <summary>Doc 04: plants and scatter details 20-150 tris (tiny pebbles may go lower), life under 150.</summary>
        public const int ScatterMaxTriangles = 150;
        /// <summary>Doc 07: up to about 4,000 ground detail instances visible on Magnifique (one chunk fills most of the frame).</summary>
        public const int ScatterMaxInstancesPerChunk = 4000;

        /// <summary>MVP Asset Pack A03: under 500 triangles for everything visible at once.</summary>
        public const int EggVisibleMaxTriangles = 500;

        [MenuItem("Pocket Wilds/Validate/Animal Prefabs", priority = 400)]
        public static void ValidateAllMenu()
        {
            var issues = ValidateAllAnimals();
            Report(issues);
        }

        [MenuItem("Pocket Wilds/Validate/Eggs", priority = 401)]
        public static void ValidateEggsMenu()
        {
            var issues = ValidateEggModels();
            issues.AddRange(ValidateEggPrefab());
            Report(issues);
        }

        [MenuItem("Pocket Wilds/Validate/Grassland Kit", priority = 402)]
        public static void ValidateEnvironmentMenu() => Report(ValidateEnvironment());

        [MenuItem("Pocket Wilds/Validate/Hide Spots (T39)", priority = 403)]
        public static void ValidateHideSpotsMenu() => Report(ValidateHideSpots());

        public static void ValidateAllBatch()
        {
            var issues = ValidateAllAnimals();
            issues.AddRange(ValidateEggModels());
            issues.AddRange(ValidateEggPrefab());
            issues.AddRange(ValidateEnvironment());
            issues.AddRange(ValidateForest());
            issues.AddRange(ValidateHideSpots());
            issues.AddRange(ValidateBiomes());
            issues.AddRange(ValidateDecorations());
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

            // Forbidden runtime components (Performance Guide).
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
                if (IsBlobShadow(r, issues, path))
                    continue;
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

        public static List<ValidationIssue> ValidateEggModels()
        {
            var issues = new List<ValidationIssue>();
            void Error(string path, string rule, string msg) => issues.Add(new ValidationIssue(IssueSeverity.Error, path, rule, msg));
            void Info(string path, string rule, string msg) => issues.Add(new ValidationIssue(IssueSeverity.Info, path, rule, msg));

            var baseMeshes = LoadMeshes(EggBaseModelPath, Error);
            foreach (var (name, min, max) in EggBaseBudgets)
            {
                if (!baseMeshes.TryGetValue(name, out var mesh))
                {
                    Error(EggBaseModelPath, "Mesh", "Missing " + name);
                    continue;
                }
                int tris = Triangles(mesh);
                if (tris > max || tris < min)
                    Error(EggBaseModelPath, "Budget", $"{name}: {tris} tris outside {min}-{max}.");
                else
                    Info(EggBaseModelPath, "Budget", $"{name}: {tris} tris ({min}-{max}).");
                CheckEggMesh(EggBaseModelPath, mesh, Error);
                // Eggs sit on the ground: pivot at the bottom of the shell.
                if (Mathf.Abs(mesh.bounds.min.y) > 0.002f)
                    Error(EggBaseModelPath, "Pivot", $"{name}: bottom at y = {mesh.bounds.min.y:F4}, expected 0.");
            }

            var fragmentMeshes = LoadMeshes(EggFragmentsModelPath, Error);
            int total = 0;
            foreach (var name in EggFragments)
            {
                if (!fragmentMeshes.TryGetValue(name, out var mesh))
                {
                    Error(EggFragmentsModelPath, "Mesh", "Missing " + name);
                    continue;
                }
                total += Triangles(mesh);
                CheckEggMesh(EggFragmentsModelPath, mesh, Error);
            }
            if (total > EggFragmentsMaxTotal)
                Error(EggFragmentsModelPath, "Budget", $"Fragments: {total} tris > {EggFragmentsMaxTotal}.");
            else
                Info(EggFragmentsModelPath, "Budget", $"Fragments: {total} tris (max {EggFragmentsMaxTotal}).");

            if (AssetImporter.GetAtPath(EggMaskPath) is TextureImporter mask)
            {
                if (mask.sRGBTexture)
                    Error(EggMaskPath, "Texture", "Mask must be linear (sRGB off).");
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(EggMaskPath);
                if (texture != null && (texture.width > 256 || texture.height > 256))
                    Error(EggMaskPath, "Texture", $"{texture.width}x{texture.height}: egg masks stay at 256.");
            }
            else
            {
                Error(EggMaskPath, "Texture", "Egg mask texture missing.");
            }
            return issues;
        }

        public static List<ValidationIssue> ValidateEggPrefab()
        {
            var issues = new List<ValidationIssue>();
            void Error(string rule, string msg) => issues.Add(new ValidationIssue(IssueSeverity.Error, EggPrefabPath, rule, msg));
            void Info(string rule, string msg) => issues.Add(new ValidationIssue(IssueSeverity.Info, EggPrefabPath, rule, msg));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EggPrefabPath);
            if (prefab == null)
            {
                Error("Load", "Egg prefab missing.");
                return issues;
            }
            if (prefab.GetComponent<EggView>() == null)
                Error("EggView", "Root must carry EggView.");

            foreach (var c in prefab.GetComponentsInChildren<Component>(true))
            {
                if (c == null) { Error("MissingScript", "Missing script reference."); continue; }
                if (c is Collider) Error("Forbidden", $"Collider on {c.name}: taps use centralized picking.");
                if (c is Rigidbody) Error("Forbidden", $"Rigidbody on {c.name}: fragments fly without physics.");
                if (c is AudioSource) Error("Forbidden", $"AudioSource on {c.name}: sounds go through the audio service.");
                if (c is Animator) Error("Forbidden", $"Animator on {c.name}: the hatch sequence drives the egg.");
                if (c is ParticleSystem) Error("Forbidden", $"ParticleSystem on {c.name}: VFX are pooled separately.");
                if (c is MonoBehaviour mb && !(mb is EggView))
                    Error("Forbidden", $"Unexpected script {mb.GetType().Name} on {c.name}.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(EggMaterialPath);
            int fragmentTris = 0, shellTris = 0;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (IsBlobShadow(r, issues, EggPrefabPath))
                    continue;
                if (r.sharedMaterials.Length != 1 || r.sharedMaterial != material)
                    Error("Materials", $"{r.name} must use only {EggMaterialPath}.");
                bool fragment = r.name.StartsWith("SM_Egg_Fragment_", StringComparison.Ordinal);
                if (fragment)
                {
                    fragmentTris += Triangles(r);
                    if (r.enabled)
                        Error("Fragments", $"{r.name} must be hidden until the burst.");
                    if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                        Error("Fragments", $"{r.name}: fragments do not cast shadows.");
                }
                else if (r.name.EndsWith("_LOD0", StringComparison.Ordinal))
                {
                    shellTris = Triangles(r);
                }
            }
            int visible = Math.Max(shellTris, fragmentTris);
            if (visible > EggVisibleMaxTriangles)
                Error("Budget", $"{visible} tris visible at once > {EggVisibleMaxTriangles}.");
            else
                Info("Budget", $"Shell {shellTris} tris, fragments {fragmentTris} tris (max {EggVisibleMaxTriangles} visible).");

            var lodGroup = prefab.GetComponent<LODGroup>();
            if (lodGroup == null || lodGroup.lodCount != 2)
                Error("LODGroup", "Egg needs a LODGroup with LOD0 and LOD1.");
            return issues;
        }

        /// <summary>Grassland kit prefabs and the start chunk: plain renderers, two shared materials, budgets.</summary>
        public static List<ValidationIssue> ValidateEnvironment()
        {
            var issues = new List<ValidationIssue>();
            var allowed = new HashSet<Material>(EnvironmentMaterialPaths.Select(AssetDatabase.LoadAssetAtPath<Material>));

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { GrasslandPrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                CheckPlainProps(prefab, path, allowed, issues, allowChunkRoot: false);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    string meshName = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : renderer.name;
                    foreach (var (prefix, min, max) in EnvironmentBudgets)
                    {
                        if (!meshName.StartsWith(prefix, StringComparison.Ordinal))
                            continue;
                        int tris = Triangles(renderer);
                        var severity = tris < min || tris > max ? IssueSeverity.Error : IssueSeverity.Info;
                        issues.Add(new ValidationIssue(severity, path, "Budget", $"{meshName}: {tris} tris ({min}-{max})."));
                    }
                }
                // T39 hide decor: shape-only prefabs (their look is merged per chunk); the budget holds for their mesh.
                if (prefab.GetComponentInChildren<Renderer>(true) == null)
                {
                    foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.sharedMesh == null)
                            continue;
                        string meshName = filter.sharedMesh.name;
                        foreach (var (prefix, min, max) in EnvironmentBudgets)
                        {
                            if (!meshName.StartsWith(prefix, StringComparison.Ordinal))
                                continue;
                            int tris = (int)(filter.sharedMesh.GetIndexCount(0) / 3);
                            var severity = tris < min || tris > max ? IssueSeverity.Error : IssueSeverity.Info;
                            issues.Add(new ValidationIssue(severity, path, "Budget", $"{meshName}: {tris} tris ({min}-{max}), shape only."));
                        }
                    }
                }
                var lod = prefab.GetComponent<LODGroup>();
                if (prefab.name.StartsWith("PF_GL_Tree", StringComparison.Ordinal))
                {
                    var lods = lod != null ? lod.GetLODs() : Array.Empty<LOD>();
                    if (lods.Length != 3)
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Error, path, "LODGroup", "Trees need LOD0/1/2."));
                    }
                    else
                    {
                        float t0 = Triangles(lods[0].renderers[0]);
                        float r1 = Triangles(lods[1].renderers[0]) / t0, r2 = Triangles(lods[2].renderers[0]) / t0;
                        if (r1 < 0.35f || r1 > 0.55f || r2 < 0.15f || r2 > 0.25f)
                            issues.Add(new ValidationIssue(IssueSeverity.Error, path, "LOD", $"LOD ratios {r1:P0} / {r2:P0}, expected 35-55% / 15-25%."));
                    }
                }
            }

            ValidateChunk(GrasslandChunkPath, allowed, issues, needsEgg: true);
            ValidateChunk(GrasslandMeadowChunkPath, allowed, issues, needsEgg: false);
            foreach (var strip in PocketWilds.Editor.Environment.GrasslandPipeline.StripChunkEntries)
                ValidateChunk(strip.path, allowed, issues, needsEgg: false);
            ValidateChunkSet(issues);
            ValidateLife(issues);
            ValidatePropOverlaps(issues);
            return issues;
        }

        /// <summary>T33C no-collision rule: no prop's volume enters another one, inside a chunk or across a seam.</summary>
        private static void ValidatePropOverlaps(List<ValidationIssue> issues)
        {
            foreach (var (a, b, depth) in PocketWilds.Editor.Environment.GrasslandPipeline.PropOverlaps())
                issues.Add(new ValidationIssue(IssueSeverity.Error, a.Chunk, "Collision",
                    $"{a.Prop} at ({a.Placed.Position.x:F2}, {a.Placed.Position.z:F2}) enters {b.Chunk}/{b.Prop} by {depth * 100f:F1} cm."));
        }

        /// <summary>
        /// T39 « Cache-cache » (Docs/Design/Collision_Rule.md): every hiding spot of DEF_HideSpots_Grassland keeps every
        /// species' body out of its chunk's obstacles and the egg's place at every measured level, its landing takes the
        /// largest body in any heading, and the spots of a zone stand 0.9 m apart. Too few spots in a zone is a warning
        /// (HideSpotTests holds the 5 to 14, the start chunk's 4).
        /// </summary>
        public static List<ValidationIssue> ValidateHideSpots()
        {
            var issues = new List<ValidationIssue>();
            const string path = PocketWilds.Editor.Activities.HideSpotPipeline.AssetPath;
            var set = AssetDatabase.LoadAssetAtPath<HideSpotSet>(path);
            if (set == null)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Warning, path, "Load", "Hide spots missing (Pocket Wilds/Activities/Build Hide Spots (T39))."));
                return issues;
            }
            var database = AssetDatabase.LoadAssetAtPath<PocketWilds.Content.ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var bodies = new BodyShape[set.SpeciesCount];
            float largest = 0f;
            for (int s = 0; s < set.SpeciesCount; s++)
            {
                var definition = database != null
                    ? database.species.FirstOrDefault(d => d != null && Core.ContentId.FromSource(d.sourceId).Value == set.GetSpecies(s).Value) : null;
                bodies[s] = definition != null && catalog != null && catalog.TryGet(definition.visualKey, out var entry)
                    ? entry.behavior.body : new BehaviorProfile().body;
                largest = Mathf.Max(largest, bodies[s].Reach);
            }
            float Clearance(WorldChunk chunk, BodyShape body, Vector3 p, float yaw)
            {
                body.Segment(p, yaw, 1f, out var a, out var b);
                float best = float.MaxValue;
                for (int i = 0; i < chunk.ObstacleCount; i++)
                {
                    var o = chunk.GetObstacle(i);
                    if (o.BlocksWalker(p.y, body.height))
                        best = Mathf.Min(best, CollisionMath.Gap(a, b, body.halfWidth, o));
                }
                if (chunk.EggSpawn != null)
                {
                    var egg = new Vector2(chunk.EggSpawn.localPosition.x, chunk.EggSpawn.localPosition.z);
                    best = Mathf.Min(best, CollisionMath.Gap(a, b, body.halfWidth, egg, egg, RefugeController.EggSpotRadius));
                }
                return best;
            }
            const float Tolerance = 0.002f;
            for (int z = 0; z < set.ZoneCount; z++)
            {
                var zone = set.GetZone(z);
                var chunkEntry = PocketWilds.Editor.World.WorldContentPipeline.GrasslandChunks.FirstOrDefault(c => c.address == zone.chunkId);
                var chunk = chunkEntry.path != null ? AssetDatabase.LoadAssetAtPath<WorldChunk>(chunkEntry.path) : null;
                if (chunk == null)
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Zone", $"{zone.chunkId}: no such Grassland chunk."));
                    continue;
                }
                int least = PocketWilds.Editor.Activities.HideSpotPipeline.MinSpotsFor(zone.chunkId);
                if (zone.spotCount < least || zone.spotCount > PocketWilds.Editor.Activities.HideSpotPipeline.MaxSpots)
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, path, "Spots",
                        $"{zone.chunkId}: {zone.spotCount} spots ({least} to {PocketWilds.Editor.Activities.HideSpotPipeline.MaxSpots} wanted)."));
                for (int i = zone.firstSpot; i < zone.firstSpot + zone.spotCount; i++)
                {
                    var spot = set.GetSpot(i);
                    for (int s = 0; s < set.SpeciesCount; s++)
                    {
                        for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                        {
                            float gap = Clearance(chunk, bodies[s], set.Placement(i, s, HideSpotSet.ShareAt(level)), spot.facing);
                            if (gap < -Tolerance)
                                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Collision",
                                    $"{zone.chunkId} spot {i - zone.firstSpot}: species {s} at {HideSpotSet.ShareAt(level) * 100f:F0} % enters something by {-gap * 100f:F1} cm."));
                        }
                    }
                    var disc = new BodyShape(largest, -largest, largest, bodies.Length > 0 ? bodies.Max(b => b.height) : 0.7f);
                    float landing = Clearance(chunk, disc, spot.landing, 0f);
                    if (landing < -Tolerance)
                        issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Collision",
                            $"{zone.chunkId} spot {i - zone.firstSpot}: its landing is {-landing * 100f:F1} cm inside something."));
                    for (int j = i + 1; j < zone.firstSpot + zone.spotCount; j++)
                    {
                        var other = set.GetSpot(j);
                        float d = new Vector2(spot.position.x - other.position.x, spot.position.z - other.position.z).magnitude;
                        if (d < PocketWilds.Editor.Activities.HideSpotPipeline.Spacing - 1e-3f)
                            issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Spacing",
                                $"{zone.chunkId}: spots {i - zone.firstSpot} and {j - zone.firstSpot} stand {d:F2} m apart (0.9 m at least)."));
                    }
                }
            }
            // What a filmed game shows: the play area, the strip's edge, the landings, other props in front.
            foreach (var line in PocketWilds.Editor.Activities.HideSpotPipeline.Audit())
            {
                int colon = line.IndexOf(':');
                string kind = colon > 0 ? line.Substring(0, colon) : "Audit";
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Spots " + kind, colon > 0 ? line.Substring(colon + 1).Trim() : line));
            }
            return issues;
        }

        /// <summary>
        /// T33C: the chunk carries its obstacles and ground heights; spawn points and the egg spot stand clear of every
        /// obstacle (a rabbit fits there whatever its heading).
        /// </summary>
        private static void ValidateCollision(WorldChunk world, string path, List<ValidationIssue> issues)
        {
            if (world.ObstacleCount == 0)
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Collision", "Chunk has no obstacles (Build Grassland Strip, T33C)."));
            if (world.GroundField == null || !world.GroundField.IsValid)
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Collision", "Chunk has no ground height field (T33C)."));
            var body = new BehaviorProfile().body;
            void Check(Transform point, float radius, string label)
            {
                if (point == null)
                    return;
                var p = point.localPosition;
                var c = new Vector2(p.x, p.z);
                for (int i = 0; i < world.ObstacleCount; i++)
                {
                    var o = world.GetObstacle(i);
                    if (!o.BlocksWalker(p.y, body.height))
                        continue;
                    float gap = Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(c, o.a, o.b)) - o.radius - radius;
                    if (gap < 0f)
                        issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Collision",
                            $"{label} at ({p.x:F2}, {p.z:F2}) is {-gap * 100f:F1} cm inside a {o.kind} obstacle."));
                }
            }
            Check(world.EggSpawn, PocketWilds.Presentation.RefugeController.EggSpotRadius, "Egg spot");
            foreach (var spawn in world.AnimalSpawns)
                Check(spawn, body.Reach, spawn != null ? spawn.name : "Animal spawn");
        }

        /// <summary>T33: the biome's ambient life uses its instanced material and light meshes.</summary>
        private static void ValidateLife(List<ValidationIssue> issues)
        {
            const string path = PocketWilds.Editor.Environment.GrasslandPipeline.LifeKitPath;
            var kit = AssetDatabase.LoadAssetAtPath<AmbientLifeKit>(path);
            if (kit == null)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Load", "Ambient life kit missing (Build Grassland Magnifique)."));
                return;
            }
            if (kit.material == null || !kit.material.enableInstancing || kit.material.shader.name != "PocketWilds/AmbientLife")
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Material", "Life uses MAT_AmbientLife (PocketWilds/AmbientLife) with instancing."));
            foreach (var species in kit.species)
            {
                int tris = species.mesh != null ? Triangles(species.mesh) : 0;
                var severity = tris == 0 || tris > ScatterMaxTriangles ? IssueSeverity.Error : IssueSeverity.Info;
                issues.Add(new ValidationIssue(severity, path, "Budget", $"{species.id}: {tris} tris (max {ScatterMaxTriangles})."));
            }
        }

        /// <summary>T33: ground details are scatter data drawn by the ScatterRenderer, within the doc 04/07 budgets.</summary>
        private static void ValidateScatter(WorldChunk world, string path, List<ValidationIssue> issues)
        {
            var data = world.Scatter;
            if (data == null || data.kit == null)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Scatter", "Chunk needs scatter data with a kit (doc 04 chunk requirements)."));
                return;
            }
            if (data.kit.material == null || !data.kit.material.enableInstancing)
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Scatter", "Scatter kit material must enable GPU instancing."));
            if (data.Count > ScatterMaxInstancesPerChunk)
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Scatter", $"{data.Count} scatter instances (max {ScatterMaxInstancesPerChunk})."));
            foreach (var kind in data.kit.kinds)
            {
                int tris = kind.mesh != null ? Triangles(kind.mesh) : 0;
                if (tris == 0 || tris > ScatterMaxTriangles)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Budget", $"Scatter {kind.id}: {tris} tris (max {ScatterMaxTriangles})."));
            }
        }

        private static void ValidateChunk(string path, HashSet<Material> allowed, List<ValidationIssue> issues, bool needsEgg)
        {
            var chunk = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (chunk == null)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Load", "Chunk missing."));
                return;
            }
            CheckPlainProps(chunk, path, allowed, issues, allowChunkRoot: true);
            var world = chunk.GetComponent<WorldChunk>();
            if (world == null)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "WorldChunk", "Chunk root must carry WorldChunk."));
                return;
            }
            var bound = new HashSet<Renderer>();
            for (int i = 0; i < world.BindingCount; i++)
                bound.Add(world.GetBinding(i).renderer);
            foreach (var renderer in chunk.GetComponentsInChildren<Renderer>(true))
                if (!bound.Contains(renderer))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Palette", renderer.name + " has no palette binding."));
            ValidateScatter(world, path, issues);
            ValidateCollision(world, path, issues);
            if ((needsEgg && world.EggSpawn == null) || world.AnimalSpawns.Length < 3)
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Points", needsEgg
                    ? "Start chunk needs an egg spawn and 3 animal spawns." : "Chunk needs 3 animal spawns."));
        }

        /// <summary>T27: every chunk of the set is an Addressable prefab whose address is the chunk id.</summary>
        private static void ValidateChunkSet(List<ValidationIssue> issues)
        {
            const string setPath = PocketWilds.Editor.World.WorldContentPipeline.ChunkSetPath;
            var set = AssetDatabase.LoadAssetAtPath<WorldChunkSet>(setPath);
            if (set == null)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, setPath, "Load", "Chunk set missing (Pocket Wilds/World/Setup Grassland Streaming)."));
                return;
            }
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(false);
            int pinned = 0;
            var ids = new HashSet<string>();
            for (int i = 0; i < set.Count; i++)
            {
                var entry = set.Get(i);
                if (entry.pinned)
                    pinned++;
                if (string.IsNullOrEmpty(entry.id) || !ids.Add(entry.id))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, setPath, "Id", $"Chunk {i} has an empty or duplicate id."));
                string guid = entry.prefab != null ? entry.prefab.AssetGUID : null;
                var asset = string.IsNullOrEmpty(guid) ? null : settings?.FindAssetEntry(guid);
                if (asset == null)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, setPath, "Addressable", $"{entry.id}: prefab is not Addressable."));
                else if (asset.address != entry.id)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, setPath, "Addressable", $"{entry.id}: address is {asset.address}."));
                else if (AssetDatabase.LoadAssetAtPath<GameObject>(asset.AssetPath)?.GetComponent<WorldChunk>() == null)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, setPath, "WorldChunk", $"{entry.id}: prefab has no WorldChunk."));
            }
            if (pinned != 1)
                issues.Add(new ValidationIssue(IssueSeverity.Error, setPath, "Pinned", $"Exactly one pinned chunk expected, found {pinned}."));
        }

        private static void CheckPlainProps(GameObject root, string path, HashSet<Material> allowed, List<ValidationIssue> issues, bool allowChunkRoot)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) { issues.Add(new ValidationIssue(IssueSeverity.Error, path, "MissingScript", "Missing script reference.")); continue; }
                if (c is Collider || c is Rigidbody || c is AudioSource || c is Light || c is Animator || c is ParticleSystem)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Forbidden", $"{c.GetType().Name} on {c.name}: props are plain renderers."));
                if (c is MonoBehaviour mb && !(allowChunkRoot && mb is WorldChunk && c.gameObject == root))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Forbidden", $"Script {mb.GetType().Name} on {c.name}: no per-prop logic."));
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterials.Length != 1 || !allowed.Contains(renderer.sharedMaterial))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Materials", $"{renderer.name} must use one shared environment material."));
            }
        }

        private static Dictionary<string, Mesh> LoadMeshes(string path, Action<string, string, string> error)
        {
            var meshes = new Dictionary<string, Mesh>();
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                error(path, "Import", "Model not found.");
                return meshes;
            }
            if (importer.isReadable)
                error(path, "Import", "Read/Write must be disabled.");
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
                error(path, "Import", "FBX materials must not be imported.");
            if (importer.animationType != ModelImporterAnimationType.None)
                error(path, "Import", "Static props have no rig.");
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Mesh mesh)
                    meshes[mesh.name] = mesh;
            return meshes;
        }

        private static void CheckEggMesh(string path, Mesh mesh, Action<string, string, string> error)
        {
            if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))
                error(path, "Mesh", mesh.name + ": UV0 (mask layout) missing.");
            if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color))
                error(path, "Mesh", mesh.name + ": PW_Regions vertex colors missing.");
            if (mesh.subMeshCount != 1)
                error(path, "Mesh", mesh.name + $": {mesh.subMeshCount} submeshes, one expected.");
        }

        public const string BlobShadowMaterialPath = "Assets/_Game/Art/Materials/MAT_BlobShadow.mat";

        /// <summary>The Low-quality blob shadow is the only renderer allowed besides the master material.</summary>
        private static bool IsBlobShadow(Renderer renderer, List<ValidationIssue> issues, string path)
        {
            if (renderer.name != "BlobShadow")
                return false;
            if (AssetDatabase.GetAssetPath(renderer.sharedMaterial) != BlobShadowMaterialPath)
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "Materials", "BlobShadow must use " + BlobShadowMaterialPath + "."));
            if (renderer.enabled)
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "BlobShadow", "BlobShadow is disabled in the prefab; views enable it on Low only."));
            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                issues.Add(new ValidationIssue(IssueSeverity.Error, path, "BlobShadow", "BlobShadow never casts shadows."));
            return true;
        }

        public static int Triangles(Mesh mesh)
        {
            long indices = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                indices += mesh.GetIndexCount(i);
            return (int)(indices / 3);
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
