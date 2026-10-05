using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T33B Prairie Magnifique part 2: the strip of eight chunks east of the start (Meadow, StreamBend, Orchard, Hill,
    /// WindmillRise, PondEdge, ForestEdge), the stream and the pond (SH_WaterLite), the landmarks (old tree with a
    /// swing, windmill whose sails turn in the shader), the last props (picket fence, well, rim pieces, stepping
    /// stones). Grounds and water come from BL_Biome_Grassland_Magnifique.blend (pw_grassland_t33b.py); every ground
    /// shares the biome's world-space grass patches and the worn path that runs along the whole strip.
    /// </summary>
    public static partial class GrasslandPipeline
    {
        public const string StripChunkFolder = "Assets/_Game/Prefabs/World/Chunks/Grassland";

        /// <summary>One chunk of the strip: where it sits, its ground and water, its props and its life.</summary>
        private sealed class StripChunk
        {
            public string Name;
            public string Address;
            public float OriginX;
            public string Ground;
            public string Water;
            public bool EastIsland;
            public int Seed;
            public int FlowerPatches = 30;
            public int FlowerSingles = 36;
            public Func<List<Placement>> Layout;
            public Vector3[] AnimalSpawns;
            public (string name, float x, float height, float z)[] Anchors;

            public string Path => $"{StripChunkFolder}/PF_Chunk_Grassland_{Name}.prefab";
            public string ScatterPath => $"{ScatterFolder}/SCT_Chunk_Grassland_{Name}.asset";
        }

        private static readonly StripChunk[] StripChunks =
        {
            new StripChunk
            {
                Name = "StreamBend", Address = "chunk.grassland.streambend", OriginX = 28f, Ground = "GL_Ground_StreamBend",
                Water = "GL_Stream_A", Seed = 3303, Layout = StreamBendLayout,
                AnimalSpawns = new[] { new Vector3(-2.4f, 0f, -1.6f), new Vector3(3.4f, 0f, -0.8f), new Vector3(-3.6f, 0f, 2.6f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_01", -3.6f, 0.6f, 1.6f), ("VFX_Butterfly_02", 3.2f, 0.6f, 3.0f), ("VFX_Bees_01", 3.2f, 0.45f, 1.9f),
                    ("VFX_Birds_01", -2.6f, 0f, -3.4f),
                },
            },
            new StripChunk
            {
                Name = "Orchard", Address = "chunk.grassland.orchard", OriginX = 42f, Ground = "GL_Ground_Orchard",
                Seed = 3304, FlowerPatches = 26, Layout = OrchardLayout,
                AnimalSpawns = new[] { new Vector3(-0.2f, 0f, -1.8f), new Vector3(2.8f, 0f, 1.4f), new Vector3(-3.2f, 0f, 1.2f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_01", -3.0f, 0.7f, 4.6f), ("VFX_Butterfly_02", 1.6f, 0.7f, 4.6f), ("VFX_Bees_01", 5.4f, 0.45f, 1.6f),
                    ("VFX_Birds_01", -1.6f, 0f, -2.4f),
                },
            },
            new StripChunk
            {
                Name = "Hill", Address = "chunk.grassland.hill", OriginX = 56f, Ground = "GL_Ground_Hill",
                Seed = 3305, FlowerPatches = 40, FlowerSingles = 50, Layout = HillLayout,
                AnimalSpawns = new[] { new Vector3(-2.6f, 0f, -2.6f), new Vector3(2.8f, 0f, -2.2f), new Vector3(-0.6f, 0f, -4.4f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_01", -2.0f, 0.6f, 0.4f), ("VFX_Butterfly_02", 3.2f, 0.6f, 1.2f), ("VFX_Bees_01", -3.4f, 0.45f, -1.2f),
                    ("VFX_Birds_01", 2.4f, 0f, -4.2f),
                },
            },
            new StripChunk
            {
                Name = "WindmillRise", Address = "chunk.grassland.windmillrise", OriginX = 70f, Ground = "GL_Ground_WindmillRise",
                Seed = 3306, Layout = WindmillLayout,
                AnimalSpawns = new[] { new Vector3(-1.8f, 0f, -2.4f), new Vector3(3.2f, 0f, -1.4f), new Vector3(-3.8f, 0f, 0.6f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_01", -2.8f, 0.6f, 3.4f), ("VFX_Butterfly_02", 4.0f, 0.6f, 0.8f), ("VFX_Bees_01", -4.2f, 0.45f, -1.6f),
                    ("VFX_Birds_01", 1.8f, 0f, -3.6f),
                },
            },
            new StripChunk
            {
                Name = "PondEdge", Address = "chunk.grassland.pondedge", OriginX = 84f, Ground = "GL_Ground_PondEdge",
                Water = "GL_Stream_B", Seed = 3307, FlowerPatches = 32, Layout = PondLayout,
                AnimalSpawns = new[] { new Vector3(-2.2f, 0f, -2.2f), new Vector3(3.6f, 0f, -1.6f), new Vector3(-4.2f, 0f, 1.6f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_01", -3.4f, 0.6f, 4.0f), ("VFX_Butterfly_02", 3.6f, 0.6f, 3.4f), ("VFX_Bees_01", 4.0f, 0.45f, -2.2f),
                    ("VFX_Birds_01", -0.8f, 0f, -1.6f),
                },
            },
            new StripChunk
            {
                Name = "ForestEdge", Address = "chunk.grassland.forestedge", OriginX = 98f, Ground = "GL_Ground_ForestEdge",
                EastIsland = true, Seed = 3308, FlowerPatches = 20, FlowerSingles = 24, Layout = ForestLayout,
                AnimalSpawns = new[] { new Vector3(-3.0f, 0f, -1.8f), new Vector3(0.6f, 0f, -2.8f), new Vector3(-4.2f, 0f, 1.8f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_01", -3.6f, 0.6f, 2.2f), ("VFX_Butterfly_02", 0.4f, 0.6f, -1.4f), ("VFX_Bees_01", -1.6f, 0.45f, 3.6f),
                    ("VFX_Birds_01", -2.0f, 0f, -3.6f),
                },
            },
        };

        /// <summary>Address, prefab path and origin of the strip chunks, east of the meadow (WorldContentPipeline).</summary>
        public static IEnumerable<(string address, string path, Vector3 origin)> StripChunkEntries =>
            StripChunks.Select(c => (c.Address, c.Path, new Vector3(c.OriginX, 0f, 0f)));

        /// <summary>The worn path running along the strip, as in pw_grassland_t33b.path_y (world x -> Unity z).</summary>
        public static float PathZ(float worldX) =>
            -(0.85f * Mathf.Sin(worldX * 0.19f + 0.6f) + 0.35f * Mathf.Sin(worldX * 0.51f + 1.9f) - 0.35f);

        [MenuItem("Pocket Wilds/Environment/Build Grassland Strip (T33B)", priority = 176)]
        public static void BuildStripMenu() => Debug.Log("[GrasslandPipeline] " + BuildStrip());

        /// <summary>Kit pieces, landmark prefabs and the six new chunks; also refreshes the Magnifique start and meadow.</summary>
        public static string BuildStrip()
        {
            string magnifique = BuildMagnifique();
            var env = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentMaterialPath);
            var ground = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            var water = EnsureWaterMaterial();
            var kit = AssetDatabase.LoadAssetAtPath<ScatterKit>(ScatterKitPath);
            var prefabs = BuildStripPrefabs(env, ground);
            var reports = new List<string> { magnifique };
            foreach (var chunk in StripChunks)
                reports.Add(BuildStripChunk(chunk, prefabs, kit, water));
            AssetDatabase.SaveAssets();
            return string.Join("; ", reports);
        }

        // ---------------- prefabs ----------------

        private static Dictionary<string, GameObject> BuildStripPrefabs(Material env, Material ground)
        {
            var meshes = ModelMeshes();
            foreach (var name in new[] { "GL_Fence_B", "GL_WellTiny", "GL_StreamStones" })
                SaveSingle(KitMesh(meshes[name]), env, ShadowCastingMode.On, 0.008f);
            foreach (var name in new[] { "GL_Edge_A", "GL_Edge_B" })
                SaveSingle(KitMesh(meshes[name]), env, ShadowCastingMode.Off, 0.01f);
            foreach (var chunk in StripChunks)
                SaveSingle(KitMesh(meshes[chunk.Ground]), ground, ShadowCastingMode.Off, 0f);
            SaveLandmarkTree(meshes, env);
            SaveWindmill(meshes, env);

            var prefabs = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                prefabs[prefab.name.Substring(3)] = prefab;
            }
            return prefabs;
        }

        private static void SaveLandmarkTree(Dictionary<string, Mesh> meshes, Material material)
        {
            var go = new GameObject("PF_GL_Landmark_OldTree");
            try
            {
                var renderers = new Renderer[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    var child = new GameObject("GL_Landmark_OldTree_LOD" + lod);
                    child.transform.SetParent(go.transform, false);
                    renderers[lod] = AddRenderer(child, KitMesh(meshes["GL_Landmark_OldTree_LOD" + lod]), material,
                        lod < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off);
                }
                var group = go.AddComponent<LODGroup>();
                // Landmarks stay detailed longer than the kit trees (doc 07: long LOD distance for monuments).
                group.SetLODs(new[]
                {
                    new LOD(0.22f, new[] { renderers[0] }), new LOD(0.09f, new[] { renderers[1] }), new LOD(0.01f, new[] { renderers[2] }),
                });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/PF_GL_Landmark_OldTree.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Hub of the sails in the tower's space (Blender (0, 0.95, 3.9): Y toward the camera, Z up).</summary>
        public static readonly Vector3 WindmillHub = new Vector3(0f, 3.9f, -0.98f);

        private static void SaveWindmill(Dictionary<string, Mesh> meshes, Material material)
        {
            var go = new GameObject("PF_GL_Landmark_Windmill");
            try
            {
                var lods = new LOD[3];
                float[] heights = { 0.22f, 0.09f, 0.01f };
                for (int lod = 0; lod < 3; lod++)
                {
                    var tower = new GameObject("GL_Landmark_Windmill_LOD" + lod);
                    tower.transform.SetParent(go.transform, false);
                    var towerRenderer = AddRenderer(tower, KitMesh(meshes["GL_Landmark_Windmill_LOD" + lod]), material,
                        lod < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off);
                    // The sails pivot on the hub; SH_EnvironmentOpaque turns them (rotor flag in the chunk binding).
                    var sails = new GameObject("GL_Landmark_Windmill_Sails_LOD" + lod);
                    sails.transform.SetParent(go.transform, false);
                    sails.transform.localPosition = WindmillHub;
                    var sailsRenderer = AddRenderer(sails, KitMesh(meshes["GL_Landmark_Windmill_Sails_LOD" + lod]), material,
                        lod < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off);
                    lods[lod] = new LOD(heights[lod], new Renderer[] { towerRenderer, sailsRenderer });
                }
                var group = go.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/PF_GL_Landmark_Windmill.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------- layouts (chunk space: x east, z away from the gameplay camera) ----------------

        private static List<Placement> StreamBendLayout() => new List<Placement>
        {
            // The spring: a hero rock and stones on the far side where the water rises.
            Prop("GL_RockHero_B", -2.75f, 6.55f, 210f, 0.62f), Prop("GL_Rock_M", -1.15f, 6.65f, 30f, 0.9f), Prop("GL_Rock_S", -2.95f, 5.55f, 80f, 1f),
            Prop("GL_Tree_A", 4.6f, 4.6f, 40f, 1.0f), Prop("GL_Tree_C", -5.0f, 3.2f, 300f, 0.95f), Prop("GL_Tree_B", 5.3f, -3.6f, 150f, 0.9f),
            Prop("GL_Tree_C", -5.1f, -4.4f, 20f, 0.85f, secondary: true),
            Prop("GL_Bush_B", 3.3f, 1.9f, 120f, 1.0f), Prop("GL_Bush_A", -3.5f, 0.6f, 200f, 0.95f), Prop("GL_Bush_C", 5.7f, 0.6f, 10f, 0.9f),
            Prop("GL_Rock_S", -0.35f, 3.9f, 140f, 0.8f), Prop("GL_Rock_S", 2.5f, -1.0f, 260f, 0.9f), Prop("GL_Rock_M", 2.6f, -5.4f, 60f, 0.85f),
            Prop("GL_Log", 3.1f, -2.4f, 25f, 0.95f), Prop("GL_Stump", -3.1f, -2.8f, 0f, 1.0f),
            Prop("GL_Fence_B", -4.4f, 5.9f, 10f, 1.0f, secondary: true),
            Prop("GL_Edge_A", -3.7f, -6.95f, 0f, 1.0f), Prop("GL_Edge_B", 3.9f, -6.95f, 0f, 1.0f),
            Prop("GL_Edge_B", -0.9f, 6.95f, 180f, 1.0f), Prop("GL_Edge_A", 4.7f, 6.95f, 180f, 1.0f),
        };

        private static List<Placement> OrchardLayout()
        {
            var list = new List<Placement>();
            // Two loose rows of fruit trees on the far side, two near the camera edges.
            float[] xs = { -4.6f, -1.6f, 1.5f, 4.5f };
            for (int i = 0; i < xs.Length; i++)
                list.Add(Prop("GL_Tree_D", xs[i] + (i % 2) * 0.3f, 3.4f + (i % 2) * 0.5f, 40f * i, 0.92f + 0.05f * (i % 3)));
            for (int i = 0; i < 3; i++)
                list.Add(Prop("GL_Tree_D", -3.1f + i * 3.1f, 5.9f, 70f * i + 20f, 0.86f, secondary: i == 1));
            list.Add(Prop("GL_Tree_D", -5.1f, -3.9f, 130f, 0.9f));
            list.Add(Prop("GL_Tree_D", 5.0f, -4.2f, 300f, 0.88f));
            list.AddRange(new[]
            {
                Prop("GL_WellTiny", -2.7f, -1.4f, 20f, 1.0f), Prop("GL_HayBale", 2.9f, -1.6f, 70f, 1.0f), Prop("GL_HayBale", 3.6f, -2.3f, 10f, 0.9f, secondary: true),
                Prop("GL_Bush_B", 5.6f, 1.6f, 300f, 1.0f), Prop("GL_Bush_A", -5.6f, 1.2f, 80f, 0.9f),
                Prop("GL_Fence_B", -4.0f, 6.75f, 0f, 1.0f), Prop("GL_Fence_B", -2.6f, 6.75f, 2f, 1.0f, secondary: true),
                Prop("GL_Fence_B", 2.7f, 6.75f, 358f, 1.0f), Prop("GL_Fence_B", 4.1f, 6.75f, 0f, 1.0f, secondary: true),
                Prop("GL_Rock_S", 0.6f, -4.6f, 30f, 0.9f), Prop("GL_Stump", 1.4f, 1.2f, 0f, 0.9f),
                Prop("GL_Edge_B", -2.4f, -6.95f, 0f, 1.0f), Prop("GL_Edge_A", 3.0f, -6.95f, 0f, 1.0f), Prop("GL_Edge_B", 0.4f, 6.95f, 180f, 1.0f),
            });
            return list;
        }

        private static List<Placement> HillLayout() => new List<Placement>
        {
            // The old tree crowns the hill; rocks and bushes dress its slopes.
            Prop("GL_Landmark_OldTree", 0.4f, 2.4f, 200f, 1.0f),
            Prop("GL_RockHero_A", -3.9f, 4.6f, 40f, 0.75f), Prop("GL_Rock_M", 3.7f, 4.9f, 100f, 1.0f), Prop("GL_Rock_L", 4.8f, -3.3f, 210f, 0.85f),
            Prop("GL_Rock_S", -2.2f, 0.2f, 0f, 0.9f), Prop("GL_Rock_S", 2.6f, 0.4f, 70f, 0.8f),
            Prop("GL_Bush_A", -2.8f, 3.0f, 30f, 1.0f), Prop("GL_Bush_C", 3.0f, 2.6f, 300f, 0.95f), Prop("GL_Bush_B", -4.8f, -1.6f, 150f, 0.9f),
            Prop("GL_Tree_C", -5.3f, -3.0f, 100f, 0.95f), Prop("GL_Tree_A", 5.4f, 1.0f, 10f, 0.85f, secondary: true),
            Prop("GL_Tree_B", -5.4f, 5.4f, 250f, 0.85f),
            Prop("GL_Edge_A", -1.6f, -6.95f, 0f, 1.0f), Prop("GL_Edge_B", 4.2f, -6.95f, 0f, 1.0f), Prop("GL_Edge_A", 2.6f, 6.95f, 180f, 1.0f),
        };

        private static List<Placement> WindmillLayout() => new List<Placement>
        {
            Prop("GL_Landmark_Windmill", 0.9f, 2.6f, 0f, 1.0f),
            Prop("GL_Fence_A", -1.6f, 0.4f, 20f, 1.0f), Prop("GL_Fence_A", -0.4f, -0.05f, 5f, 1.0f, secondary: true),
            Prop("GL_HayBale", 3.0f, 1.2f, 30f, 1.0f), Prop("GL_HayBale", 3.5f, 2.1f, 80f, 0.9f, secondary: true),
            Prop("GL_Tree_B", -4.9f, 4.3f, 60f, 0.95f), Prop("GL_Tree_A", 5.2f, 4.9f, 200f, 0.9f), Prop("GL_Tree_D", -5.1f, -3.6f, 90f, 0.9f),
            Prop("GL_Bush_B", 4.6f, -1.8f, 20f, 0.95f), Prop("GL_Bush_C", -3.4f, 2.4f, 140f, 0.9f),
            Prop("GL_Rock_M", 2.2f, -4.6f, 300f, 0.9f), Prop("GL_Rock_S", -2.6f, -4.2f, 40f, 0.9f),
            Prop("GL_Stump", 4.8f, 0.6f, 0f, 0.9f),
            Prop("GL_Edge_B", -3.4f, -6.95f, 0f, 1.0f), Prop("GL_Edge_A", 3.6f, -6.95f, 0f, 1.0f), Prop("GL_Edge_B", -0.6f, 6.95f, 180f, 1.0f),
        };

        private static List<Placement> PondLayout() => new List<Placement>
        {
            Prop("GL_RockHero_B", -2.6f, 3.9f, 300f, 0.6f), Prop("GL_Rock_S", 2.8f, 1.2f, 20f, 0.85f), Prop("GL_Rock_M", 3.2f, 3.8f, 140f, 0.8f),
            Prop("GL_StreamStones", 1.6f, 0.55f, 35f, 0.9f), Prop("GL_Log", -2.0f, 0.3f, 160f, 0.9f),
            Prop("GL_Bush_C", 3.5f, 5.0f, 60f, 0.95f), Prop("GL_Bush_A", -4.4f, 1.8f, 230f, 0.9f),
            Prop("GL_Tree_C", -4.9f, 4.9f, 30f, 0.95f), Prop("GL_Tree_C", -3.8f, 6.0f, 200f, 0.85f, secondary: true),
            Prop("GL_Tree_A", 5.2f, -3.1f, 120f, 0.9f), Prop("GL_Tree_B", 5.4f, 5.6f, 330f, 0.85f),
            Prop("GL_Stump", -4.6f, -3.4f, 0f, 1.0f),
            Prop("GL_Edge_A", -2.8f, -6.95f, 0f, 1.0f), Prop("GL_Edge_B", 2.2f, -6.95f, 0f, 1.0f), Prop("GL_Edge_B", 1.6f, 6.95f, 180f, 1.0f),
        };

        private static List<Placement> ForestLayout() => new List<Placement>
        {
            // The meadow thickens into the forest's edge toward the east end of the island.
            Prop("GL_Tree_A", 1.6f, 4.8f, 20f, 1.05f), Prop("GL_Tree_B", 3.8f, 3.2f, 140f, 1.0f), Prop("GL_Tree_A", 4.9f, 0.4f, 260f, 0.95f),
            Prop("GL_Tree_B", 3.4f, -3.4f, 60f, 0.95f), Prop("GL_Tree_C", -0.8f, 5.6f, 330f, 0.95f), Prop("GL_Tree_C", 5.6f, -1.8f, 100f, 0.9f),
            Prop("GL_Tree_A", -3.6f, 4.6f, 200f, 0.9f, secondary: true), Prop("GL_Tree_B", 1.2f, -5.0f, 300f, 0.85f),
            Prop("GL_Bush_A", 2.0f, 1.8f, 20f, 1.0f), Prop("GL_Bush_C", 4.4f, -0.9f, 80f, 1.0f), Prop("GL_Bush_A", -1.8f, 3.0f, 160f, 0.85f),
            Prop("GL_Stump", 0.4f, 0.6f, 0f, 1.0f), Prop("GL_Log", 2.2f, -1.6f, 70f, 1.0f), Prop("GL_Log", -2.6f, 1.6f, 10f, 0.85f, secondary: true),
            Prop("GL_Rock_M", -4.2f, -2.6f, 30f, 0.9f), Prop("GL_RockHero_A", 5.0f, 4.8f, 200f, 0.6f),
            Prop("GL_Edge_B", -3.0f, -6.95f, 0f, 1.0f), Prop("GL_Edge_B", 0.6f, 6.95f, 180f, 1.0f),
        };

        // ---------------- chunk ----------------

        private static string BuildStripChunk(StripChunk def, Dictionary<string, GameObject> prefabs, ScatterKit kit, Material waterMaterial)
        {
            var meshes = ModelMeshes();
            var groundMesh = meshes[def.Ground];
            var groundSampler = new GroundSampler(groundMesh);
            var layout = def.Layout();
            // Props stand on the ground (hill, rise, banks): the layout gives x and z, the ground gives y.
            for (int i = 0; i < layout.Count; i++)
            {
                var p = layout[i];
                if (groundSampler.TrySample(p.Position.x, p.Position.z, out float h, out _))
                    p.Position = new Vector3(p.Position.x, p.Prop.StartsWith("GL_Edge", StringComparison.Ordinal) ? 0f : h - 0.02f, p.Position.z);
                layout[i] = p;
            }
            Mesh waterMesh = def.Water != null ? meshes[def.Water] : null;
            var waterSampler = waterMesh != null ? new GroundSampler(waterMesh) : null;
            bool InWater(Vector3 p)
            {
                if (waterSampler == null)
                    return false;
                for (int k = 0; k < 5; k++)
                {
                    var o = k == 0 ? Vector3.zero : Quaternion.Euler(0f, k * 90f, 0f) * new Vector3(0.14f, 0f, 0f);
                    if (waterSampler.TrySample(p.x + o.x, p.z + o.z, out _, out _))
                        return true;
                }
                return false;
            }

            // Stepping stones where the path crosses the stream.
            if (waterSampler != null && def.Water == "GL_Stream_A")
            {
                float sum = 0f;
                int hits = 0;
                for (float x = -6.5f; x <= 6.5f; x += 0.05f)
                {
                    if (waterSampler.TrySample(x, PathZ(def.OriginX + x), out _, out _))
                    {
                        sum += x;
                        hits++;
                    }
                }
                if (hits > 0)
                {
                    float x = sum / hits;
                    float z = PathZ(def.OriginX + x);
                    float slope = (PathZ(def.OriginX + x + 0.5f) - PathZ(def.OriginX + x - 0.5f)) / 1.0f;
                    float yaw = -Mathf.Atan(slope) * Mathf.Rad2Deg;
                    float y = waterSampler.TrySample(x, z, out float wh, out _) ? wh : 0f;
                    layout.Add(new Placement { Prop = "GL_StreamStones", Position = new Vector3(x, y, z), Yaw = yaw, Scale = 1.05f, Palette = PaletteFor("GL_StreamStones") });
                }
            }

            const float Margin = 0.35f;
            var rules = new ScatterRules
            {
                Seed = def.Seed, Ground = groundMesh, Worn = WornMask(groundMesh),
                Inside = def.EastIsland
                    ? p => p.x < 0f ? p.x > -6.95f && Mathf.Abs(p.z) < 6.9f : InsideIsland(p, Margin)
                    : p => Mathf.Abs(p.x) < 6.95f && Mathf.Abs(p.z) < 6.9f,
                Excluded = InWater,
                Clear = new Vector3(0f, 0f, -100f), ClearRadius = 0f, Lawn = Vector3.zero, LawnRadius = 0f,
                FlowerPatches = def.FlowerPatches, RimPatches = 0, FlowerSingles = def.FlowerSingles,
            };
            var centers = new List<Vector3>();
            var scatter = SaveScatter(def.ScatterPath, kit, GenerateScatter(kit, layout.Where(p => p.Prop != "GL_StreamStones").ToList(), rules, centers));

            var root = new GameObject("PF_Chunk_Grassland_" + def.Name);
            try
            {
                var chunk = root.AddComponent<WorldChunk>();
                var bindings = new List<WorldChunk.PaletteBinding>();
                var ground = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[def.Ground], root.transform);
                ground.name = "Ground";
                bindings.Add(new WorldChunk.PaletteBinding
                {
                    renderer = ground.GetComponent<Renderer>(), paletteId = ContentId.FromSource(PaletteGround).Value, brightness = WorldChunk.NeutralBrightness,
                });
                if (waterMesh != null)
                {
                    var water = new GameObject("Water");
                    water.transform.SetParent(root.transform, false);
                    var renderer = AddRenderer(water, KitMesh(waterMesh), waterMaterial, ShadowCastingMode.Off);
                    renderer.receiveShadows = true;
                    bindings.Add(new WorldChunk.PaletteBinding
                    {
                        renderer = renderer, paletteId = ContentId.FromSource(PaletteWater).Value, brightness = WorldChunk.NeutralBrightness,
                    });
                }
                ComposePropsT33(root.transform, layout, prefabs, new Random(def.Seed), bindings);

                var spawns = Child(root.transform, "SpawnPoints");
                var animals = def.AnimalSpawns.Select((p, i) =>
                {
                    float y = groundSampler.TrySample(p.x, p.z, out float h, out _) ? h : 0f;
                    return Point(spawns, $"AnimalSpawn_{i + 1:00}", new Vector3(p.x, y, p.z));
                }).ToArray();
                var anchorsRoot = Child(root.transform, "VFXAnchors");
                var anchors = def.Anchors.Select(a => Point(anchorsRoot, a.name, OnGround(groundMesh, a.x, a.height, a.z))).ToArray();
                var linksRoot = Child(root.transform, "ChunkLinks");
                var links = def.EastIsland
                    ? new[] { Point(linksRoot, "Link_W", new Vector3(-7f, 0f, 0f)) }
                    : new[] { Point(linksRoot, "Link_W", new Vector3(-7f, 0f, 0f)), Point(linksRoot, "Link_E", new Vector3(7f, 0f, 0f)) };
                var area = new WanderArea(Vector3.zero, 3.5f);
                chunk.SetAuthoring(bindings.ToArray(), area, null, animals, InterestsT33(layout, centers, area), anchors, links);
                chunk.SetScatter(scatter);
                Directory.CreateDirectory(StripChunkFolder);
                PrefabUtility.SaveAsPrefabAsset(root, def.Path);
                return $"{def.Name}: {layout.Count} props, {scatter.Count} scatter instances, {bindings.Count} renderers";
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
