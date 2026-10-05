using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T15 Grassland kit pipeline: MAT_Environment_Common, MAT_Ground_Grassland, DEF_Palettes_Grassland,
    /// one prefab per kit mesh (plain renderers, no scripts) and PF_Chunk_Grassland_Start composed with a
    /// deterministic layout. Idempotent: run again after re-exporting BL_Biome_Grassland_Core.blend.
    /// </summary>
    public static class GrasslandPipeline
    {
        public const string EnvironmentShader = "PocketWilds/EnvironmentOpaque";
        public const string GroundShader = "PocketWilds/GroundMaster";
        public const string EnvironmentMaterialPath = "Assets/_Game/Art/Materials/MAT_Environment_Common.mat";
        public const string GroundMaterialPath = "Assets/_Game/Art/Materials/MAT_Ground_Grassland.mat";
        public const string ModelsFolder = "Assets/_Game/Art/Environment/Grassland/Models";
        public const string PrefabsFolder = "Assets/_Game/Prefabs/Environment/Grassland";
        public const string ChunkPath = "Assets/_Game/Prefabs/World/Chunks/Grassland/PF_Chunk_Grassland_Start.prefab";
        public const string BakedFolder = "Assets/_Game/Art/Environment/Grassland/Baked";
        public const string PalettesPath = "Assets/_Game/Content/Palettes/DEF_Palettes_Grassland.asset";

        public const string PaletteGround = "palette.grassland.ground";
        public const string PaletteGrass = "palette.grassland.grass";
        public const string PaletteRock = "palette.grassland.rock";
        public const string PaletteTree = "palette.grassland.tree";
        public static readonly string[] FlowerPalettes =
        {
            "palette.grassland.flower_white", "palette.grassland.flower_yellow", "palette.grassland.flower_pink",
            "palette.grassland.flower_blue", "palette.grassland.flower_lilac",
        };

        /// <summary>Tree LOD switch points (screen height). Small props cull when tiny.</summary>
        public static readonly float[] TreeLods = { 0.32f, 0.14f, 0.03f };
        public const float SmallPropCull = 0.012f;

        [MenuItem("Pocket Wilds/Environment/Build Grassland Kit (T15)", priority = 170)]
        public static void BuildAll()
        {
            var envMaterial = EnsureMaterial(EnvironmentMaterialPath, EnvironmentShader, "MAT_Environment_Common");
            var groundMaterial = EnsureMaterial(GroundMaterialPath, GroundShader, "MAT_Ground_Grassland");
            EnsurePalettes();
            var prefabs = BuildPropPrefabs(envMaterial, groundMaterial);
            BuildStartChunk(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("[GrasslandPipeline] Materials, palettes, " + prefabs.Count + " prop prefabs and PF_Chunk_Grassland_Start up to date.");
        }

        // ---------------- Materials and palettes ----------------

        private static Material EnsureMaterial(string path, string shaderName, string name)
        {
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Shader not found: " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.enableInstancing = false; // SRP Batcher; palettes travel in the Shader User Value.
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsurePalettes()
        {
            var set = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(PalettesPath);
            if (set == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PalettesPath));
                set = ScriptableObject.CreateInstance<PaletteSetAsset>();
                AssetDatabase.CreateAsset(set, PalettesPath);
            }
            set.palettes.Clear();
            // base, R, G, B, A, occlusion, accent (Grassland identity: fresh soft green, cream stone, pastel flowers).
            Add(set, PaletteGround, "78A862", "8FB878", "5F9152", "B8C98E", "8F6A50", "4A6542", "9CBC74");
            Add(set, PaletteGrass, "66A253", "9FCE78", "9FCE78", "66A253", "66A253", "476F3E", "9FCE78");
            Add(set, PaletteRock, "E8DCC4", "E8DCC4", "9CC77A", "E8DCC4", "E8DCC4", "A39478", "FFFFFF");
            Add(set, PaletteTree, "93694F", "7DB960", "A9D57C", "578F4B", "6E4C3A", "3B6338", "FFFFFF");
            Add(set, FlowerPalettes[0], "6FAF55", "F8F4EA", "F2C94C", "6FAF55", "6FAF55", "4A7340", "FFFFFF");
            Add(set, FlowerPalettes[1], "6FAF55", "F7DF7A", "F29C4C", "6FAF55", "6FAF55", "4A7340", "FFFFFF");
            Add(set, FlowerPalettes[2], "6FAF55", "F3C4D6", "F2C94C", "6FAF55", "6FAF55", "4A7340", "FFFFFF");
            Add(set, FlowerPalettes[3], "6FAF55", "A9C8F2", "F2C94C", "6FAF55", "6FAF55", "4A7340", "FFFFFF");
            Add(set, FlowerPalettes[4], "6FAF55", "C9B4EE", "F2C94C", "6FAF55", "6FAF55", "4A7340", "FFFFFF");
            EditorUtility.SetDirty(set);

            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            if (!database.paletteSets.Contains(set))
                database.paletteSets.Add(set);
            EditorUtility.SetDirty(database);
        }

        private static void Add(PaletteSetAsset set, string id, params string[] hex)
        {
            set.palettes.Add(new PaletteSetAsset.Entry
            {
                sourceId = id,
                baseColor = Hex(hex[0]),
                regionR = Hex(hex[1]),
                regionG = Hex(hex[2]),
                regionB = Hex(hex[3]),
                regionA = Hex(hex[4]),
                occlusion = Hex(hex[5]),
                accent = Hex(hex[6]),
            });
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        // ---------------- Prop prefabs ----------------

        private static Dictionary<string, GameObject> BuildPropPrefabs(Material env, Material ground)
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Mesh>())
                    meshes[mesh.name] = mesh;
            if (!meshes.ContainsKey("GL_Ground_Core_A"))
                throw new InvalidOperationException("Grassland models are not imported. Export them from BL_Biome_Grassland_Core.blend first.");

            Directory.CreateDirectory(PrefabsFolder);
            var result = new Dictionary<string, GameObject>();
            result["GL_Ground_Core_A"] = SaveSingle(meshes["GL_Ground_Core_A"], ground, ShadowCastingMode.Off, cull: 0f);
            foreach (var size in new[] { "S", "M", "L" })
            {
                result["GL_GrassTuft_" + size] = SaveSingle(meshes["GL_GrassTuft_" + size], env, ShadowCastingMode.Off, SmallPropCull);
                result["GL_Rock_" + size] = SaveSingle(meshes["GL_Rock_" + size], env, ShadowCastingMode.On, size == "S" ? SmallPropCull : 0.006f);
            }
            foreach (var kind in new[] { "A", "B", "C" })
                result["GL_Flower_" + kind] = SaveSingle(meshes["GL_Flower_" + kind], env, ShadowCastingMode.Off, SmallPropCull);
            result["GL_Tree_A"] = SaveTree(meshes, env);
            return result;
        }

        private static GameObject SaveSingle(Mesh mesh, Material material, ShadowCastingMode shadows, float cull)
        {
            var go = new GameObject("PF_" + mesh.name);
            try
            {
                var renderer = AddRenderer(go, mesh, material, shadows);
                if (cull > 0f)
                {
                    var lod = go.AddComponent<LODGroup>();
                    lod.SetLODs(new[] { new LOD(cull, new Renderer[] { renderer }) });
                    lod.RecalculateBounds();
                }
                return PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/PF_{mesh.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static GameObject SaveTree(Dictionary<string, Mesh> meshes, Material material)
        {
            var go = new GameObject("PF_GL_Tree_A");
            try
            {
                var renderers = new Renderer[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    var child = new GameObject("GL_Tree_A_LOD" + lod);
                    child.transform.SetParent(go.transform, false);
                    renderers[lod] = AddRenderer(child, meshes["GL_Tree_A_LOD" + lod], material, lod < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off);
                }
                var group = go.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(TreeLods[0], new[] { renderers[0] }),
                    new LOD(TreeLods[1], new[] { renderers[1] }),
                    new LOD(TreeLods[2], new[] { renderers[2] }),
                });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/PF_GL_Tree_A.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static MeshRenderer AddRenderer(GameObject go, Mesh mesh, Material material, ShadowCastingMode shadows)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            return renderer;
        }

        // ---------------- Chunk composition ----------------

        private struct Placement
        {
            public string Prop;
            public Vector3 Position;
            public float Yaw;
            public float Scale;
            public string Palette;
            public bool Secondary;
        }

        /// <summary>Superellipse footprint of GL_Ground_Core_A (power 4, half size 7 m).</summary>
        private static bool InsideIsland(Vector3 p, float margin)
        {
            float half = 7f - margin;
            return Mathf.Pow(Mathf.Abs(p.x) / half, 4f) + Mathf.Pow(Mathf.Abs(p.z) / half, 4f) <= 1f;
        }

        /// <summary>Center of the painted clearing (vertex color B) of the ground mesh.</summary>
        public static Vector3 ClearingCenter(Mesh ground)
        {
            // Imported meshes stay readable in the editor even with Read/Write off (that flag is for players).
            var vertices = ground.vertices;
            var colors = ground.colors32;
            Vector3 sum = Vector3.zero;
            float weight = 0f;
            for (int i = 0; i < vertices.Length && i < colors.Length; i++)
            {
                float w = colors[i].b / 255f;
                if (w < 0.25f)
                    continue;
                sum += vertices[i] * w;
                weight += w;
            }
            if (weight <= 0f)
                throw new InvalidOperationException("Ground mesh has no painted clearing (PW_Regions.B).");
            var center = sum / weight;
            center.y = 0f;
            return center;
        }

        private static List<Placement> Layout(Vector3 egg, List<Vector3> patchCenters = null)
        {
            var list = new List<Placement>();
            var rng = new Random(1507);

            // Trees frame the portrait view from behind and from the far sides; none between camera (-Z) and the egg.
            (float x, float z, float yaw, float scale)[] trees =
            {
                (-1.8f, 4.5f, 30f, 1.0f), (2.3f, 6.0f, 200f, 0.85f), (-4.7f, 1.2f, 120f, 0.9f), (4.5f, 2.4f, 300f, 0.95f),
                (-3.4f, 6.0f, 80f, 0.8f), (5.1f, -2.6f, 10f, 0.85f), (-5.1f, -3.1f, 170f, 0.9f),
            };
            foreach (var t in trees)
                list.Add(new Placement { Prop = "GL_Tree_A", Position = new Vector3(t.x, 0f, t.z), Yaw = t.yaw, Scale = t.scale, Palette = PaletteTree });

            (string size, float x, float z, float yaw, float scale)[] rocks =
            {
                ("L", 2.0f, 1.7f, 40f, 1f), ("S", 2.6f, 1.15f, 0f, 1f), ("M", -2.0f, -0.4f, 100f, 1f), ("S", -1.55f, 2.8f, 210f, 0.9f),
                ("M", 1.1f, 4.9f, 10f, 0.9f), ("L", -4.3f, -1.5f, 200f, 0.9f), ("S", -3.8f, -0.9f, 60f, 1.1f), ("M", 4.6f, -0.7f, 250f, 1f),
                ("S", 0.7f, -3.7f, 120f, 1f), ("M", -1.1f, 6.2f, 330f, 0.85f), ("S", 3.4f, 4.4f, 15f, 1f), ("L", 3.9f, -4.2f, 75f, 0.8f),
            };
            foreach (var r in rocks)
                list.Add(new Placement { Prop = "GL_Rock_" + r.size, Position = new Vector3(r.x, 0f, r.z), Yaw = r.yaw, Scale = r.scale, Palette = PaletteRock });

            var anchors = list.Select(p => p.Position).ToList();
            bool Free(Vector3 p, float clearance)
            {
                if (!InsideIsland(p, 0.6f) || Vector3.Distance(p, egg) < clearance)
                    return false;
                foreach (var placed in list)
                {
                    float min = placed.Prop.StartsWith("GL_Tree", StringComparison.Ordinal) ? 0.55f
                        : placed.Prop.StartsWith("GL_Rock_L", StringComparison.Ordinal) ? 0.6f
                        : placed.Prop.StartsWith("GL_Rock", StringComparison.Ordinal) ? 0.35f : 0.22f;
                    if (Vector3.Distance(placed.Position, p) < min * placed.Scale)
                        return false;
                }
                return true;
            }

            // Grass: tufts hug rocks and trees, then a sparse meadow scatter, thinner where animals play.
            string[] tufts = { "GL_GrassTuft_S", "GL_GrassTuft_M", "GL_GrassTuft_M", "GL_GrassTuft_L" };
            foreach (var anchor in anchors)
            {
                int around = 3 + rng.Next(3);
                for (int i = 0; i < around; i++)
                {
                    float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float d = 0.45f + (float)rng.NextDouble() * 0.5f;
                    var p = anchor + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                    if (Free(p, 1.4f))
                        list.Add(Tuft(rng, tufts, p, secondary: rng.NextDouble() < 0.3));
                }
            }
            for (int attempt = 0; attempt < 700 && list.Count(p => p.Prop.StartsWith("GL_Grass", StringComparison.Ordinal)) < 160; attempt++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 13.0 - 6.5), 0f, (float)(rng.NextDouble() * 13.0 - 6.5));
                float fromEgg = Vector3.Distance(p, egg);
                double keep = fromEgg < 1.4f ? 0.0 : fromEgg < 3.2f ? 0.3 : 1.0;
                if (rng.NextDouble() > keep || !Free(p, 1.4f))
                    continue;
                list.Add(Tuft(rng, tufts, p, secondary: rng.NextDouble() < 0.5));
            }

            // Flowers in small single-color patches: they read as color notes, not as noise.
            string[] kinds = { "GL_Flower_A", "GL_Flower_A", "GL_Flower_B", "GL_Flower_C" };
            int patches = 0;
            for (int attempt = 0; attempt < 200 && patches < 14; attempt++)
            {
                var center = new Vector3((float)(rng.NextDouble() * 12.0 - 6.0), 0f, (float)(rng.NextDouble() * 12.0 - 6.0));
                if (Vector3.Distance(center, egg) < 1.9f || !Free(center, 1.9f))
                    continue;
                string kind = kinds[rng.Next(kinds.Length)];
                string palette = kind == "GL_Flower_B" ? FlowerPalettes[3 + rng.Next(2)]
                    : kind == "GL_Flower_C" ? FlowerPalettes[2]
                    : FlowerPalettes[rng.Next(2)];
                int count = 3 + rng.Next(4);
                for (int i = 0; i < count; i++)
                {
                    var p = center + new Vector3((float)(rng.NextDouble() - 0.5) * 0.7f, 0f, (float)(rng.NextDouble() - 0.5) * 0.7f);
                    if (!Free(p, 1.6f))
                        continue;
                    list.Add(new Placement
                    {
                        // Flowers are color notes: slightly oversized so they still read at the gameplay distance.
                        Prop = kind, Position = p, Yaw = (float)(rng.NextDouble() * 360.0), Scale = 1.3f + (float)rng.NextDouble() * 0.3f,
                        Palette = palette, Secondary = i >= 2 && rng.NextDouble() < 0.6,
                    });
                }
                patchCenters?.Add(center);
                patches++;
            }
            return list;
        }

        private static Placement Tuft(Random rng, string[] tufts, Vector3 p, bool secondary) => new Placement
        {
            Prop = tufts[rng.Next(tufts.Length)],
            Position = p,
            Yaw = (float)(rng.NextDouble() * 360.0),
            Scale = 0.85f + (float)rng.NextDouble() * 0.4f,
            Palette = PaletteGrass,
            Secondary = secondary,
        };

        private static void BuildStartChunk(Dictionary<string, GameObject> prefabs)
        {
            var groundMesh = prefabs["GL_Ground_Core_A"].GetComponent<MeshFilter>().sharedMesh;
            var egg = ClearingCenter(groundMesh);
            var layout = Layout(egg);
            var rng = new Random(4242);

            var root = new GameObject("PF_Chunk_Grassland_Start");
            try
            {
                var chunk = root.AddComponent<WorldChunk>();
                var bindings = new List<WorldChunk.PaletteBinding>();

                var ground = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["GL_Ground_Core_A"], root.transform);
                ground.name = "Ground";
                bindings.Add(new WorldChunk.PaletteBinding
                {
                    renderer = ground.GetComponent<Renderer>(), paletteId = ContentId.FromSource(PaletteGround).Value, brightness = WorldChunk.NeutralBrightness,
                });

                var props = Child(root.transform, "Props");
                var groups = new Dictionary<string, Transform>
                {
                    ["GL_Tree"] = Child(props, "Trees"), ["GL_Rock"] = Child(props, "Rocks"),
                    ["GL_Grass"] = Child(props, "Grass"), ["GL_Flower"] = Child(props, "Flowers"),
                };
                // Grass tufts and flowers are merged per palette and density class (one renderer each):
                // ~190 small renderers became about a dozen draws. Each instance keeps its brightness in uv.y.
                var merged = new Dictionary<(string palette, bool secondary, string group), List<(Mesh mesh, Matrix4x4 matrix, byte brightness)>>();
                foreach (var placement in layout)
                {
                    var group = groups.First(g => placement.Prop.StartsWith(g.Key, StringComparison.Ordinal)).Value;
                    bool mergeable = placement.Prop.StartsWith("GL_Grass", StringComparison.Ordinal)
                                     || placement.Prop.StartsWith("GL_Flower", StringComparison.Ordinal);
                    if (mergeable)
                    {
                        byte level = (byte)(5 + rng.Next(7));
                        var key = (placement.Palette, placement.Secondary, group.name);
                        if (!merged.TryGetValue(key, out var list))
                            merged[key] = list = new List<(Mesh, Matrix4x4, byte)>();
                        var matrix = Matrix4x4.TRS(placement.Position, Quaternion.Euler(0f, placement.Yaw, 0f), Vector3.one * placement.Scale);
                        list.Add((prefabs[placement.Prop].GetComponent<MeshFilter>().sharedMesh, matrix, level));
                        continue;
                    }
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[placement.Prop], group);
                    instance.transform.SetLocalPositionAndRotation(placement.Position, Quaternion.Euler(0f, placement.Yaw, 0f));
                    instance.transform.localScale = Vector3.one * placement.Scale;
                    byte brightness = (byte)(placement.Prop.StartsWith("GL_Tree", StringComparison.Ordinal) ? 6 + rng.Next(5) : 5 + rng.Next(7));
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        bindings.Add(new WorldChunk.PaletteBinding
                        {
                            renderer = renderer, paletteId = ContentId.FromSource(placement.Palette).Value,
                            brightness = brightness, secondary = placement.Secondary,
                        });
                    }
                }

                var env = prefabs["GL_Rock_S"].GetComponent<MeshRenderer>().sharedMaterial;
                Directory.CreateDirectory(BakedFolder);
                int index = 0;
                foreach (var pair in merged.OrderBy(p => p.Key.group).ThenBy(p => p.Key.palette).ThenBy(p => p.Key.secondary))
                {
                    string shortName = pair.Key.palette.Substring(pair.Key.palette.LastIndexOf('.') + 1);
                    string name = $"Merged_{shortName}_{(pair.Key.secondary ? "Secondary" : "Primary")}";
                    var mesh = MergeProps(pair.Value, $"MSH_Chunk_Grassland_Start_{index++:00}_{shortName}");
                    var go = new GameObject(name);
                    go.transform.SetParent(groups.First(g => g.Value.name == pair.Key.group).Value, false);
                    var renderer = AddRenderer(go, mesh, env, ShadowCastingMode.Off);
                    bindings.Add(new WorldChunk.PaletteBinding
                    {
                        renderer = renderer, paletteId = ContentId.FromSource(pair.Key.palette).Value,
                        brightness = WorldChunk.NeutralBrightness, secondary = pair.Key.secondary, vertexBrightness = true,
                    });
                }

                var spawns = Child(root.transform, "SpawnPoints");
                var eggSpawn = Point(spawns, "EggSpawn", egg);
                var animals = new[]
                {
                    Point(spawns, "AnimalSpawn_01", egg + new Vector3(0.95f, 0f, 0.55f)),
                    Point(spawns, "AnimalSpawn_02", egg + new Vector3(-0.95f, 0f, 0.75f)),
                    Point(spawns, "AnimalSpawn_03", egg + new Vector3(0.15f, 0f, 1.6f)),
                };
                var area = new WanderArea(egg + new Vector3(0f, 0f, 0.6f), 3.2f);
                var interestPoints = Interests(egg, area);
                var anchors = Child(root.transform, "VFXAnchors");
                var vfxAnchors = new[]
                {
                    Point(anchors, "VFX_Hatch", egg),
                    Point(anchors, "VFX_Butterfly_01", new Vector3(-2.2f, 0.6f, 2.0f)),
                    Point(anchors, "VFX_Butterfly_02", new Vector3(2.6f, 0.6f, 3.2f)),
                };
                var linksRoot = Child(root.transform, "ChunkLinks");
                var links = new[]
                {
                    Point(linksRoot, "Link_N", new Vector3(0f, 0f, 7f)), Point(linksRoot, "Link_E", new Vector3(7f, 0f, 0f)),
                    Point(linksRoot, "Link_S", new Vector3(0f, 0f, -7f)), Point(linksRoot, "Link_W", new Vector3(-7f, 0f, 0f)),
                };

                chunk.SetAuthoring(bindings.ToArray(), area, eggSpawn, animals, interestPoints, vfxAnchors, links);
                Directory.CreateDirectory(Path.GetDirectoryName(ChunkPath));
                PrefabUtility.SaveAsPrefabAsset(root, ChunkPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// One static mesh for many prop instances: positions and normals in chunk space, vertex colors kept
        /// (palette regions), uv.x kept (occlusion), uv.y = instance brightness level / 15.
        /// </summary>
        private static Mesh MergeProps(List<(Mesh mesh, Matrix4x4 matrix, byte brightness)> instances, string assetName)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color32>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var sourceVertices = new List<Vector3>();
            var sourceNormals = new List<Vector3>();
            var sourceColors = new List<Color32>();
            var sourceUvs = new List<Vector2>();
            foreach (var (mesh, matrix, brightness) in instances)
            {
                int offset = vertices.Count;
                mesh.GetVertices(sourceVertices);
                mesh.GetNormals(sourceNormals);
                mesh.GetColors(sourceColors);
                mesh.GetUVs(0, sourceUvs);
                var normalMatrix = matrix.inverse.transpose;
                float level = brightness / 15f;
                for (int v = 0; v < sourceVertices.Count; v++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(sourceVertices[v]));
                    normals.Add(normalMatrix.MultiplyVector(v < sourceNormals.Count ? sourceNormals[v] : Vector3.up).normalized);
                    colors.Add(v < sourceColors.Count ? sourceColors[v] : new Color32(0, 0, 0, 0));
                    uvs.Add(new Vector2(v < sourceUvs.Count ? sourceUvs[v].x : 0f, level));
                }
                var source = mesh.triangles;
                for (int t = 0; t < source.Length; t++)
                    triangles.Add(source[t] + offset);
            }

            string path = $"{BakedFolder}/{assetName}.asset";
            var merged = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (merged == null)
            {
                merged = new Mesh();
                AssetDatabase.CreateAsset(merged, path);
            }
            merged.Clear();
            merged.name = assetName;
            merged.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            merged.SetVertices(vertices);
            merged.SetNormals(normals);
            merged.SetColors(colors);
            merged.SetUVs(0, uvs);
            merged.SetTriangles(triangles, 0);
            merged.RecalculateBounds();
            merged.UploadMeshData(false);
            EditorUtility.SetDirty(merged);
            return merged;
        }

        /// <summary>
        /// T25 points of interest, from the same deterministic layout as the props: rocks and tree trunks to
        /// inspect, flower patches and a meadow spot to nibble. Only points an animal can reach from the wander area.
        /// </summary>
        public static InterestPoint[] Interests(Vector3 egg, WanderArea area)
        {
            var centers = new List<Vector3>();
            var layout = Layout(egg, centers);
            var result = new List<InterestPoint>();
            void Add(Vector3 position, float standOff, InterestKind kind)
            {
                if (Vector3.Distance(new Vector3(position.x, 0f, position.z), new Vector3(area.Center.x, 0f, area.Center.z)) <= area.Radius + standOff + 0.2f)
                    result.Add(new InterestPoint(position, standOff, kind));
            }
            foreach (var p in layout)
            {
                if (p.Prop.StartsWith("GL_Rock_", StringComparison.Ordinal))
                {
                    float size = p.Prop.EndsWith("L", StringComparison.Ordinal) ? 0.62f : p.Prop.EndsWith("M", StringComparison.Ordinal) ? 0.45f : 0.32f;
                    Add(p.Position, size * p.Scale, InterestKind.Inspect);
                }
                else if (p.Prop.StartsWith("GL_Tree", StringComparison.Ordinal))
                {
                    Add(p.Position, 0.42f * p.Scale, InterestKind.Inspect);
                }
            }
            foreach (var c in centers)
                Add(c, 0.16f, InterestKind.Eat);
            Add(egg + new Vector3(-1.7f, 0f, -1.3f), 0f, InterestKind.Eat);
            return result.ToArray();
        }

        /// <summary>Rewrites only the start chunk's points of interest (no prop or mesh rebuild).</summary>
        [MenuItem("Pocket Wilds/Environment/Update Chunk Points of Interest", priority = 172)]
        public static void UpdateChunkInterests()
        {
            var root = PrefabUtility.LoadPrefabContents(ChunkPath);
            try
            {
                var chunk = root.GetComponent<WorldChunk>();
                var ground = root.transform.Find("Ground").GetComponent<MeshFilter>().sharedMesh;
                var egg = ClearingCenter(ground);
                var legacy = root.transform.Find("InterestPoints");
                if (legacy != null)
                    UnityEngine.Object.DestroyImmediate(legacy.gameObject);
                var points = Interests(egg, chunk.LocalWanderArea);
                chunk.SetInterests(points);
                PrefabUtility.SaveAsPrefabAsset(root, ChunkPath);
                Debug.Log($"[GrasslandPipeline] {points.Length} points of interest in the start chunk.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static Transform Point(Transform parent, string name, Vector3 localPosition)
        {
            var t = Child(parent, name);
            t.localPosition = localPosition;
            return t;
        }

        // ---------------- Editor preview ----------------

        /// <summary>Binds palettes on every chunk of the open scenes (edit time only, not saved).</summary>
        [MenuItem("Pocket Wilds/Environment/Preview Palettes In Open Scenes", priority = 171)]
        public static void PreviewPalettes()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath).CreateBuilder().Build();
            // Kept alive on purpose for the editor session: the global palette texture must outlive this call.
            var table = new PaletteTable(registry);
            int missing = 0;
            foreach (var chunk in Object.FindObjectsByType<WorldChunk>(FindObjectsSortMode.None))
                missing += chunk.Bind(table);
            Debug.Log("[GrasslandPipeline] Palette preview bound, missing palettes: " + missing);
        }
    }
}
