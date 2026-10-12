using System;
using System.Collections.Generic;
using System.Linq;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Production;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using GroundSampler = PocketWilds.Editor.Environment.GrasslandPipeline.GroundSampler;
using Object = UnityEngine.Object;
using Placement = PocketWilds.Editor.Environment.GrasslandPipeline.Placement;
using Random = System.Random;
using ScatterPoint = PocketWilds.Editor.Environment.GrasslandPipeline.ScatterPoint;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T50 the Forest's seven chunks on the strip (origins 112 to 196, 14 m apart, BiomeWizard.ChunkOrigin): layouts written in
    /// chunk space (x east, z away from the gameplay camera) around the ground's own features (pw_forest_ground.py: root mounds,
    /// the ancient tree's rise, the hollow, the brook, the mushroom spot), checked crown against crown so that nothing needs more
    /// than a few centimetres to settle. Big trees stand on the back and near rims only (T50 art pass: no trunk in the middle of
    /// the frame), the worn path stays readable and fern-lined, the middle keeps small pieces and open ground for the animals,
    /// the Clearing's ancient tree is the one big trunk allowed nearer (its crown reads in the frame). Ferns and mushrooms are shape-only pieces
    /// (obstacles like any prop) drawn as one merged renderer each per chunk.
    /// </summary>
    public static partial class ForestPipeline
    {
        /// <summary>Half width of the worn path the ferns line (pw_forest_ground.forest_field: 0.7 plus or minus 0.1).</summary>
        private const float PathHalf = 0.8f;

        private sealed class ForestChunk
        {
            public string Name;
            public string Key;
            public int Zone;
            public int Seed;
            /// <summary>LakeTransition: the east rim stays closed (the Lake comes in T57).</summary>
            public bool EastRim;
            public bool Brook;
            /// <summary>Chunk x beyond which the path has faded out (LakeTransition).</summary>
            public float PathEnd = 99f;
            public Func<List<Placement>> Layout;
            public (float x0, float x1)[] FernRuns;
            /// <summary>Reed-like young ferns in a damp area (x0, x1, z0, z1) and how many.</summary>
            public (Vector4 area, int count) Reeds;
            public int FlowerPatches;
            public int FlowerSingles;
            public Vector3[] AnimalSpawns;
            public Vector3 WanderCenter;
            public float WanderRadius = 3.4f;
            public (string name, float x, float height, float z)[] Anchors;

            public float OriginX => BiomeWizard.ChunkOrigin(Spec, Zone).x;
            public string Address => BiomeWizard.ChunkAddress(Spec.Key, Key);
            public string Path => Paths.ChunkPrefab(Name);
            public string ScatterPath => $"{GrasslandPipeline.ScatterFolder}/SCT_Chunk_Forest_{Name}.asset";
            public string Ground => "FO_Ground_" + Name;
            /// <summary>Key of the chunk's props among the strip's built props (GrasslandPipeline.BuiltProps).</summary>
            public string SettleName => "Forest" + Name;
        }

        private static readonly BiomeWizard.BiomeSpec Spec = BiomeWizard.ForestSpec();

        private static readonly ForestChunk[] Chunks =
        {
            new ForestChunk
            {
                Name = "Entry", Key = "entry", Zone = 0, Seed = 5101, Layout = EntryLayout, FernRuns = new[] { (-2.6f, 6.8f) },
                FlowerPatches = 5, FlowerSingles = 12, WanderCenter = new Vector3(-0.6f, 0f, -0.4f),
                AnimalSpawns = new[] { new Vector3(-4.0f, 0f, -1.6f), new Vector3(0.6f, 0f, -3.4f), new Vector3(-2.4f, 0f, 1.6f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_Dusk_01", -3.2f, 0.7f, 1.8f), ("VFX_Butterfly_Dusk_02", 1.6f, 0.9f, 1.4f), ("VFX_Butterfly_Dusk_03", 4.6f, 0.8f, 2.4f),
                    ("VFX_Birds_01", -4.6f, 0f, 0.9f), ("VFX_Birds_02", 3.4f, 0f, -2.6f),
                },
            },
            new ForestChunk
            {
                Name = "DeepForest", Key = "deepforest", Zone = 1, Seed = 5102, Layout = DeepForestLayout, FernRuns = new[] { (-6.8f, 6.8f) },
                WanderCenter = new Vector3(0f, 0f, -0.4f), WanderRadius = 3.0f,
                AnimalSpawns = new[] { new Vector3(-2.0f, 0f, -1.0f), new Vector3(2.4f, 0f, -0.6f), new Vector3(-0.4f, 0f, 1.9f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_Dusk_01", -3.0f, 0.7f, 0.0f), ("VFX_Butterfly_Dusk_02", 1.6f, 0.8f, 1.8f), ("VFX_Butterfly_Dusk_03", 4.4f, 0.7f, -1.2f),
                    ("VFX_Birds_01", -0.6f, 0f, -0.6f), ("VFX_Birds_02", 4.2f, 0f, 2.4f),
                },
            },
            new ForestChunk
            {
                Name = "Clearing", Key = "clearing", Zone = 2, Seed = 5103, Layout = ClearingLayout,
                FernRuns = new[] { (-6.8f, -4.6f), (4.6f, 6.8f) }, FlowerPatches = 14, FlowerSingles = 30,
                WanderCenter = new Vector3(0f, 0f, -0.2f), WanderRadius = 3.8f,
                AnimalSpawns = new[] { new Vector3(-1.6f, 0f, -1.6f), new Vector3(1.8f, 0f, -1.2f), new Vector3(1.0f, 0f, 1.0f) },
                Anchors = new[]
                {
                    ("VFX_ButterflyGlade_01", -2.0f, 0.6f, 0.8f), ("VFX_ButterflyGlade_02", 2.2f, 0.6f, -0.6f), ("VFX_ButterflyGlade_03", 0.4f, 0.6f, -2.4f),
                    ("VFX_Butterfly_Dusk_01", -4.4f, 0.8f, 1.0f), ("VFX_Butterfly_Dusk_02", 4.4f, 0.8f, 1.4f),
                    ("VFX_Birds_01", 1.2f, 0f, -2.8f), ("VFX_Birds_02", -3.0f, 0f, -1.6f),
                },
            },
            new ForestChunk
            {
                Name = "Hollow", Key = "hollow", Zone = 3, Seed = 5104, Layout = HollowLayout, FernRuns = new[] { (-6.8f, 6.8f) },
                WanderCenter = new Vector3(0f, 0f, -0.2f),
                AnimalSpawns = new[] { new Vector3(-1.0f, 0f, -0.8f), new Vector3(1.6f, 0f, -0.4f), new Vector3(0.0f, 0f, -2.6f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_Dusk_01", -2.4f, 0.6f, 0.4f), ("VFX_Butterfly_Dusk_02", 2.2f, 0.7f, 2.4f), ("VFX_Butterfly_Dusk_03", 0.6f, 0.8f, -3.2f),
                    ("VFX_Birds_01", -0.8f, 0f, -1.8f), ("VFX_Birds_02", 3.6f, 0f, 0.4f),
                },
            },
            new ForestChunk
            {
                Name = "Brook", Key = "brook", Zone = 4, Seed = 5105, Brook = true, Layout = BrookLayout,
                FernRuns = new[] { (-6.8f, -2.0f), (0.7f, 6.8f) }, WanderCenter = new Vector3(-0.2f, 0f, -0.8f),
                AnimalSpawns = new[] { new Vector3(-3.0f, 0f, -1.6f), new Vector3(3.0f, 0f, 0.4f), new Vector3(-2.4f, 0f, 1.8f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_Dusk_01", -1.6f, 0.7f, 2.2f), ("VFX_Butterfly_Dusk_02", 1.6f, 0.8f, -1.4f), ("VFX_Butterfly_Dusk_03", 3.4f, 0.8f, 3.6f),
                    ("VFX_Birds_01", -3.4f, 0f, 0.2f), ("VFX_Birds_02", 3.4f, 0f, -1.8f),
                },
            },
            new ForestChunk
            {
                Name = "MushroomCorner", Key = "mushroomcorner", Zone = 5, Seed = 5106, Layout = MushroomCornerLayout, FernRuns = new[] { (-6.8f, 6.8f) },
                WanderCenter = new Vector3(0.4f, 0f, -0.6f),
                AnimalSpawns = new[] { new Vector3(0.6f, 0f, -1.2f), new Vector3(-3.2f, 0f, -1.0f), new Vector3(3.0f, 0f, 1.8f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_Dusk_01", -1.1f, 0.9f, 1.6f), ("VFX_Butterfly_Dusk_02", 2.6f, 0.7f, 1.2f), ("VFX_Butterfly_Dusk_03", -3.6f, 0.8f, -1.6f),
                    ("VFX_Birds_01", 1.8f, 0f, -1.0f), ("VFX_Birds_02", -2.6f, 0f, 0.0f),
                },
            },
            new ForestChunk
            {
                Name = "LakeTransition", Key = "laketransition", Zone = 6, Seed = 5107, EastRim = true, PathEnd = 4.6f, Layout = LakeTransitionLayout,
                FernRuns = new[] { (-6.8f, 3.8f) }, Reeds = (new Vector4(1.6f, 5.6f, -1.6f, 3.4f), 9), WanderCenter = new Vector3(-0.8f, 0f, 0f),
                AnimalSpawns = new[] { new Vector3(-3.0f, 0f, -0.8f), new Vector3(0.8f, 0f, -1.6f), new Vector3(-0.6f, 0f, 2.0f) },
                Anchors = new[]
                {
                    ("VFX_Butterfly_Dusk_01", -3.2f, 0.7f, 0.2f), ("VFX_Butterfly_Dusk_02", 1.0f, 0.8f, 1.8f), ("VFX_Butterfly_Dusk_03", 3.8f, 0.6f, -0.6f),
                    ("VFX_Birds_01", -1.6f, 0f, -1.2f), ("VFX_Birds_02", 2.8f, 0f, 0.6f),
                },
            },
        };

        /// <summary>Address, prefab path and origin of the Forest's chunks in strip order (validator, tests, the strip's checks).</summary>
        public static IEnumerable<(string address, string path, Vector3 origin)> ChunkEntries =>
            Chunks.Select(c => (c.Address, c.Path, new Vector3(c.OriginX, 0f, 0f)));

        /// <summary>
        /// T50 art pass: the mushrooms read too small beside the animals (A 0.22 m, B 0.15, C 0.12 at scale 1, the rabbit 0.65 m
        /// with its ears): every mushroom placement is drawn this much bigger (A about 0.4 m), measured, settled and ringed at that size.
        /// </summary>
        public const float MushroomScale = 1.8f;

        /// <summary>Big trees stand on the rims only: their trunks never inside this band of chunk z (the middle of the frame).</summary>
        public const float TreeBackRim = 4.0f;
        public const float TreeNearRim = -4.5f;

        private static Placement P(string piece, float x, float z, float yaw, float scale, bool secondary = false) => new Placement
        {
            Prop = "FO_" + piece, Position = new Vector3(x, 0f, z), Yaw = yaw, Scale = IsMushroom("FO_" + piece) ? scale * MushroomScale : scale,
            Palette = PaletteFor("FO_" + piece), Secondary = secondary,
        };

        // ---------------- layouts (chunk space; the path is GrasslandPipeline.PathZ of the world x) ----------------
        //
        // Framing (T50 art pass, the Grassland's rule): the camera looks down at 40 degrees over a frame about 4 m wide and from
        // chunk z -4.3 (bottom) to +8.5 (top) around a focus at z 0, panning along the strip. Big trees (FO_Tree_A..D) stand on
        // the back rim (z 4 and more, crowns above the top of the frame) and the near rim (z -5.4 and less, crowns low in the
        // frame's bottom), staggered so their crowns frame the view without touching across the seams; the middle keeps the small
        // things (ferns, mushrooms, rocks, logs, stumps, bushes, the bridge) and open ground for the animals. Root mounds the
        // trees left carry a stump, a bush or a moss rock. Checked crown against crown (measured LOD0 bands) within each chunk and
        // against the west neighbour: nothing needs more than a few centimetres to settle.

        /// <summary>The woodland edge: the meadow's last grass on the west, the path running under the birch-framed edge, an oak on its root mound.</summary>
        private static List<Placement> EntryLayout() => new List<Placement>
        {
            P("Tree_B", -4.4f, 5.6f, 20f, 1.0f), P("Tree_D", -1.8f, 4.3f, 30f, 1.0f), P("Tree_B", 0.4f, 6.1f, 140f, 1.05f), P("Tree_A", 3.8f, 4.4f, 200f, 1.05f),
            P("Tree_D", -2.6f, -5.7f, 210f, 0.95f), P("Tree_A", 5.0f, -5.6f, 80f, 0.9f),
            P("Bush_A", 2.5f, 2.9f, 40f, 0.9f), P("Bush_C", -5.0f, 3.0f, 120f, 1.0f), P("Bush_B", -4.4f, -3.6f, 300f, 0.9f),
            P("Bush_A", 6.3f, -0.9f, 200f, 0.85f, secondary: true),
            P("MossRock_M", -1.4f, 3.2f, 40f, 1.0f), P("MossRock_S", -0.7f, 3.8f, 160f, 1.0f, secondary: true), P("Stump", -1.6f, -2.8f, 0f, 0.9f),
            P("MossRock_L", 4.5f, -1.0f, 70f, 0.9f), P("MossRock_M", 1.8f, -2.6f, 150f, 0.9f),
            P("Mushroom_A", -0.55f, -2.35f, 30f, 1.0f), P("Mushroom_B", -2.35f, -1.8f, 200f, 1.0f), P("Mushroom_A", 3.35f, 3.2f, 120f, 0.9f),
            P("Mushroom_C", 2.45f, -2.25f, 80f, 1.0f), P("Mushroom_B", 1.35f, -3.25f, 300f, 0.95f),
        };

        /// <summary>The undergrowth: an oak on its root mound, tall trees on the back rim, birches and a leaning tree on the near rim,
        /// a stump, a bush and moss rocks on the other mounds, the path winding between bushes, a hollow log with mushrooms.</summary>
        private static List<Placement> DeepForestLayout() => new List<Placement>
        {
            P("Tree_B", -4.6f, 5.8f, 70f, 1.05f), P("Tree_A", -1.0f, 4.7f, 160f, 1.1f), P("Tree_B", 2.6f, 6.0f, 10f, 1.0f), P("Tree_D", 6.0f, 5.6f, 0f, 1.0f),
            P("Tree_D", -5.0f, -5.6f, 300f, 0.95f), P("Tree_D", -1.6f, -5.8f, 0f, 1.0f), P("Tree_C", 3.9f, -5.6f, 0f, 1.0f),
            P("Bush_A", -2.6f, 2.4f, 30f, 0.95f), P("Bush_C", 2.9f, 3.6f, 200f, 1.0f), P("Bush_B", 5.6f, 1.7f, 100f, 0.95f),
            P("Bush_A", -4.6f, -1.6f, 250f, 0.9f, secondary: true), P("Stump", -3.4f, 3.8f, 40f, 0.95f),
            P("HollowLog", 0.6f, -2.4f, 15f, 1.0f), P("MossRock_L", -0.4f, -4.9f, 120f, 0.9f), P("MossRock_S", 1.9f, -1.4f, 0f, 1.0f, secondary: true),
            P("MossRock_M", -2.2f, -3.3f, 60f, 1.0f), P("MossRock_M", 3.4f, -3.1f, 200f, 0.9f),
            P("Mushroom_A", 1.9f, -1.9f, 0f, 1.0f), P("Mushroom_B", -0.55f, -1.8f, 90f, 1.0f), P("Mushroom_C", 0.15f, -1.5f, 200f, 1.0f),
            P("Mushroom_C", -1.6f, 3.6f, 40f, 1.0f), P("Mushroom_A", -2.45f, 3.3f, 150f, 0.95f),
        };

        /// <summary>
        /// The sunny clearing: the ancient tree back left (owl branch to the west, knot hole toward the camera) with its broad crown
        /// over the upper part of the frame and room to walk around its roots, open grass and flowers, a mushroom cluster at its feet.
        /// </summary>
        private static List<Placement> ClearingLayout() => new List<Placement>
        {
            P("Landmark_AncientTree", AncientTreeSpot.x, AncientTreeSpot.y, 0f, 1.0f),
            P("Tree_B", -5.3f, 6.2f, 40f, 1.0f), P("Tree_B", 3.0f, 6.0f, 140f, 1.0f), P("Tree_D", 5.9f, 5.3f, 200f, 1.0f),
            P("Tree_A", -5.6f, -5.7f, 120f, 0.95f), P("Tree_D", 5.4f, -5.8f, 60f, 0.9f),
            P("Bush_B", -4.6f, 2.4f, 20f, 0.95f), P("Bush_C", 4.3f, 2.8f, 150f, 1.0f), P("Bush_A", 3.8f, -4.7f, 60f, 0.9f),
            P("Bush_B", -3.3f, -5.5f, 240f, 0.85f, secondary: true),
            P("MossRock_M", -4.0f, 1.4f, 80f, 1.0f), P("MossRock_L", 5.6f, -1.7f, 10f, 0.85f),
            P("Mushroom_A", 0.55f, 2.0f, 0f, 1.0f), P("Mushroom_B", 0.85f, 2.6f, 130f, 1.0f), P("Mushroom_C", 2.2f, 2.9f, 300f, 1.0f),
        };

        /// <summary>
        /// The ancient tree's place in the Clearing (chunk space): z 2.6 shows its crown (2.95 to 6.2 m up, 8.6 m wide) along the
        /// top of the frame with its trunk below it. The ground's low rise (pw_forest_ground.ANCIENT_TREE_SPOT, 140.6, 4.4) still
        /// sits where the tree first stood until the Forest ground is exported again with the rise here (world 138.8, 2.6).
        /// </summary>
        public static readonly Vector2 AncientTreeSpot = new Vector2(-1.2f, 2.6f);

        /// <summary>The hollow: the giant stump on the far lip (door toward the camera), moss rocks on the slopes, a root arch.</summary>
        private static List<Placement> HollowLayout() => new List<Placement>
        {
            P("Landmark_GiantStump", 0.4f, 4.9f, 0f, 1.0f),
            P("MossRock_Hero", -3.6f, -2.4f, 30f, 1.0f), P("MossRock_L", 3.4f, -2.2f, 200f, 1.0f), P("MossRock_M", 2.5f, -3.3f, 60f, 1.0f),
            P("MossRock_S", -1.6f, -3.9f, 0f, 1.0f, secondary: true), P("RootArch", -4.2f, 3.4f, 20f, 1.0f),
            P("Tree_B", -5.6f, 5.8f, 100f, 1.0f), P("Tree_A", 4.4f, 4.8f, 250f, 1.0f), P("Tree_C", -5.2f, -5.4f, 180f, 1.0f),
            P("Tree_D", 5.7f, -5.2f, 330f, 0.95f), P("Tree_B", 3.0f, -5.9f, 0f, 0.95f),
            P("Bush_A", -2.0f, 3.7f, 0f, 0.9f), P("Bush_C", 2.4f, 3.3f, 100f, 1.0f),
            P("Mushroom_B", -0.9f, 3.35f, 0f, 1.0f), P("Mushroom_C", 2.1f, 4.25f, 120f, 1.0f), P("Mushroom_A", -2.3f, -1.7f, 60f, 1.0f),
        };

        /// <summary>The brook: a moss rock hides its spring, ferns on the banks; the bridge and the stepping stones come with the water.</summary>
        private static List<Placement> BrookLayout() => new List<Placement>
        {
            P("MossRock_M", 1.9f, 6.1f, 0f, 1.0f),
            P("Tree_A", -4.6f, 4.6f, 40f, 1.05f), P("Tree_B", 4.8f, 5.4f, 200f, 1.0f), P("Tree_D", -1.2f, 5.9f, 270f, 0.95f),
            P("Tree_D", -5.0f, -5.6f, 120f, 1.0f), P("Tree_B", 5.0f, -5.6f, 0f, 1.0f),
            P("Bush_C", -2.7f, 3.4f, 30f, 1.0f), P("Bush_A", 2.9f, -2.5f, 80f, 0.9f), P("Bush_B", -3.6f, -5.3f, 160f, 0.9f, secondary: true),
            P("MossRock_S", -2.3f, -1.5f, 0f, 1.0f), P("Mushroom_B", -3.9f, 3.1f, 20f, 1.0f),
            P("Mushroom_A", -2.0f, -2.0f, 200f, 1.0f), P("Mushroom_C", -1.7f, -2.45f, 40f, 0.95f),
        };

        /// <summary>Mushroom spot (pw_forest_ground.MUSHROOM_SPOT, chunk space) and the ring of its soil (0.85 to 1.35 m).</summary>
        private static readonly Vector2 MushroomSpot = new Vector2(-1.1f, 3.0f);

        /// <summary>The mushroom corner: a stump in its litter disc with a ring of mushrooms around it, a hollow log, lush moss.</summary>
        private static List<Placement> MushroomCornerLayout()
        {
            // The ring's stump comes first among the decor (the tests find it as the first PF_FO_Stump).
            var list = new List<Placement>
            {
                P("Stump", MushroomSpot.x, MushroomSpot.y, 0f, 1.0f), P("HollowLog", 2.6f, -2.6f, 165f, 1.0f),
                P("Tree_A", -5.0f, 5.3f, 300f, 1.05f), P("Tree_B", 2.8f, 5.5f, 60f, 1.0f), P("Tree_D", 5.6f, 5.0f, 150f, 0.95f),
                P("Tree_C", -4.6f, -5.6f, 180f, 0.95f), P("Tree_A", 5.2f, -5.6f, 30f, 0.9f),
                P("Bush_B", 1.5f, 3.0f, 300f, 0.9f), P("Bush_C", -4.0f, 2.4f, 60f, 1.0f), P("Bush_A", -2.6f, -3.1f, 10f, 0.95f),
                P("Bush_A", 4.7f, -0.9f, 140f, 0.85f, secondary: true),
                P("MossRock_M", 0.6f, -3.7f, 40f, 1.0f), P("MossRock_S", 1.0f, 1.95f, 0f, 1.0f, secondary: true),
                P("Mushroom_C", 3.95f, -2.0f, 0f, 1.0f), P("Mushroom_A", 1.6f, -1.95f, 70f, 1.0f),
            };
            // The ring: nine mushrooms (A, B, C in turn, so the broad B caps never stand side by side) around the stump's measured
            // foot (its roots reach 0.94 m), 1.34 to 1.42 m out at their bigger size so their caps clear the roots and read as a ring.
            var rng = new Random(1821);
            var foot = new Vector2(MushroomSpot.x - 0.04f, MushroomSpot.y + 0.13f);
            string[] kinds = { "Mushroom_A", "Mushroom_B", "Mushroom_C" };
            for (int k = 0; k < 9; k++)
            {
                float a = k / 9f * Mathf.PI * 2f + (float)(rng.NextDouble() - 0.5) * 0.16f;
                float r = 1.34f + (float)rng.NextDouble() * 0.08f;
                list.Add(P(kinds[k % 3], foot.x + Mathf.Cos(a) * r, foot.y + Mathf.Sin(a) * r, (float)(rng.NextDouble() * 360.0), 0.95f + (float)rng.NextDouble() * 0.2f));
            }
            return list;
        }

        /// <summary>The way toward the lake: light birches thinning out eastward, damp ground, reed-like young ferns, a stump with mushrooms.</summary>
        private static List<Placement> LakeTransitionLayout() => new List<Placement>
        {
            P("Tree_D", -5.0f, 4.6f, 20f, 1.0f), P("Tree_B", -1.9f, 5.6f, 300f, 0.95f), P("Tree_D", 1.9f, 4.4f, 160f, 0.9f),
            P("Tree_D", -5.2f, -5.4f, 200f, 0.9f), P("Tree_D", 2.6f, -5.7f, 80f, 0.85f),
            P("MossRock_L", 4.2f, 1.6f, 30f, 0.9f), P("MossRock_S", 5.0f, 2.5f, 0f, 1.0f, secondary: true), P("MossRock_M", -1.2f, -3.1f, 120f, 1.0f),
            P("Bush_A", -3.6f, 2.7f, 90f, 0.9f), P("Bush_C", 0.2f, 3.1f, 0f, 0.95f), P("Bush_B", -2.1f, -2.3f, 200f, 0.9f, secondary: true),
            P("Stump", 2.4f, -2.2f, 0f, 0.85f), P("Mushroom_A", 3.5f, -2.1f, 0f, 1.0f), P("Mushroom_C", 3.3f, -2.65f, 210f, 1.0f),
        };

        // ---------------- the brook ----------------

        /// <summary>The brook's line in Brook chunk space (pw_forest_ground.BROOK_POINTS), from the spring to the fall.</summary>
        private static readonly Vector2[] BrookPoints =
        {
            new Vector2(1.9f, 6.1f), new Vector2(1.35f, 4.6f), new Vector2(0.1f, 3.15f), new Vector2(-0.85f, 1.6f), new Vector2(-0.7f, -0.2f),
            new Vector2(0.15f, -2.0f), new Vector2(0.85f, -3.9f), new Vector2(0.5f, -5.6f), new Vector2(0.7f, -7.0f),
        };

        /// <summary>Where the path crosses the brook (pw_forest_ground.bridge_spot): Brook chunk x, z and yaw of the TinyBridge.</summary>
        public static readonly Vector3 BridgeSpot = new Vector3(-0.70f, 12.5f, -0.115f);

        /// <summary>The stepping stones: a second crossing upstream, across the brook between its third and fourth bends.</summary>
        public static readonly Vector3 StonesSpot = new Vector3(0.72f, 40f, 3.88f);

        private static bool IsCrossing(string prop) => prop == "FO_TinyBridge" || prop == "FO_SteppingStones";

        private static void AddCrossings(List<Placement> layout, GroundSampler ground, GroundSampler water)
        {
            // The bridge's pivot is mid-span at bank level: both ends rest on the banks (the lower one sets the height).
            var along = Quaternion.Euler(0f, BridgeSpot.y, 0f) * Vector3.right;
            float Height(float x, float z) => ground.TrySample(x, z, out float h, out _) ? h : 0f;
            float bank = Mathf.Min(Height(BridgeSpot.x - along.x, BridgeSpot.z - along.z), Height(BridgeSpot.x + along.x, BridgeSpot.z + along.z));
            var bridge = P("TinyBridge", BridgeSpot.x, BridgeSpot.z, BridgeSpot.y, 1.0f);
            bridge.Position = new Vector3(BridgeSpot.x, bank - 0.01f, BridgeSpot.z);
            layout.Add(bridge);
            // The stones lie in the stream like the Prairie's: their base at the water's surface.
            float surface = water != null && water.TrySample(StonesSpot.x, StonesSpot.z, out float wh, out _) ? wh : Height(StonesSpot.x, StonesSpot.z);
            var stones = P("SteppingStones", StonesSpot.x, StonesSpot.z, StonesSpot.y, 1.0f);
            stones.Position = new Vector3(StonesSpot.x, surface - 0.01f, StonesSpot.z);
            layout.Add(stones);
        }

        // ---------------- chunk ----------------

        private static string BuildChunk(ForestChunk def, Dictionary<string, GameObject> prefabs, ScatterKit kit, Material waterMaterial,
            Dictionary<string, Mesh> meshes)
        {
            var groundMesh = meshes[def.Ground];
            var sampler = new GroundSampler(groundMesh);
            var regions = new RegionSampler(groundMesh);
            Mesh waterMesh = def.Brook ? meshes["FO_Brook_Water"] : null;
            var waterSampler = waterMesh != null ? new GroundSampler(waterMesh) : null;
            var origin = new Vector3(def.OriginX, 0f, 0f);

            // A plant or a fern (radius r) stays out of the water: its center and its rim all around.
            bool InWater(Vector3 p, float r)
            {
                if (waterSampler == null)
                    return false;
                float reach = Mathf.Max(r, 0.14f);
                for (int k = 0; k < 9; k++)
                {
                    var o = k == 0 ? Vector3.zero : Quaternion.Euler(0f, k * 45f, 0f) * new Vector3(reach, 0f, 0f);
                    if (waterSampler.TrySample(p.x + o.x, p.z + o.z, out _, out _))
                        return true;
                }
                return false;
            }
            Func<Vector3, float, bool> excluded = waterSampler != null ? InWater : (Func<Vector3, float, bool>)null;
            bool Inside(Vector3 p, float margin) => def.EastRim && p.x > 0f
                ? GrasslandPipeline.InsideIsland(p, 0.35f + margin)
                : Mathf.Abs(p.x) < 6.95f - margin && Mathf.Abs(p.z) < 6.9f - margin;

            var layout = def.Layout();
            if (def.Brook)
                AddCrossings(layout, sampler, waterSampler);
            int ferns = AddFerns(def, layout, prefabs, origin, excluded, Inside);
            // Props stand on the lowest ground under their footprint (root mounds, the hollow, the lake's slope).
            for (int i = 0; i < layout.Count; i++)
            {
                var p = layout[i];
                if (IsCrossing(p.Prop) || !sampler.TrySample(p.Position.x, p.Position.z, out _, out _))
                    continue;
                p.Position = new Vector3(p.Position.x, GrasslandPipeline.GroundUnder(sampler, p.Position.x, p.Position.z, GrasslandPipeline.FootprintRadius(p)), p.Position.z);
                layout[i] = p;
            }
            // Nothing enters anything, the ForestEdge's and the west neighbour's props included; moved props follow the ground.
            var moves = GrasslandPipeline.SettleProps(def.SettleName, origin, layout, prefabs,
                (x, z, radius) => GrasslandPipeline.GroundUnder(sampler, x, z, radius));

            var solids = GrasslandPipeline.PlaceLayout(layout, prefabs);
            var eat = new List<Vector3>();
            var points = GenerateScatter(def, kit, layout, solids, sampler, regions, p => Inside(p, 0f), excluded, eat);
            var scatter = GrasslandPipeline.SaveScatter(def.ScatterPath, kit, points);

            var root = new GameObject("PF_Chunk_Forest_" + def.Name);
            try
            {
                var chunk = root.AddComponent<WorldChunk>();
                var bindings = new List<WorldChunk.PaletteBinding>();
                var ground = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[def.Ground], root.transform);
                ground.name = "Ground";
                bindings.Add(new WorldChunk.PaletteBinding
                {
                    renderer = ground.GetComponent<Renderer>(), paletteId = Id(PaletteGround), brightness = WorldChunk.NeutralBrightness,
                });
                if (waterMesh != null)
                {
                    var water = new GameObject("Water");
                    water.transform.SetParent(root.transform, false);
                    var renderer = GrasslandPipeline.AddRenderer(water, Kit(waterMesh), waterMaterial, ShadowCastingMode.Off);
                    renderer.receiveShadows = true;
                    bindings.Add(new WorldChunk.PaletteBinding
                    {
                        renderer = renderer, paletteId = Id(PaletteWater), brightness = WorldChunk.NeutralBrightness,
                    });
                }
                ComposeProps(def, root.transform, layout, prefabs, bindings);

                var spawns = GrasslandPipeline.Child(root.transform, "SpawnPoints");
                var animals = def.AnimalSpawns.Select((p, i) =>
                {
                    float y = sampler.TrySample(p.x, p.z, out float h, out _) ? h : 0f;
                    return GrasslandPipeline.Point(spawns, $"AnimalSpawn_{i + 1:00}", new Vector3(p.x, y, p.z));
                }).ToArray();
                var anchorsRoot = GrasslandPipeline.Child(root.transform, "VFXAnchors");
                var anchors = def.Anchors.Select(a => GrasslandPipeline.Point(anchorsRoot, a.name, GrasslandPipeline.OnGround(groundMesh, a.x, a.height, a.z))).ToArray();
                var linksRoot = GrasslandPipeline.Child(root.transform, "ChunkLinks");
                var links = def.EastRim
                    ? new[] { GrasslandPipeline.Point(linksRoot, "Link_W", new Vector3(-7f, 0f, 0f)) }
                    : new[] { GrasslandPipeline.Point(linksRoot, "Link_W", new Vector3(-7f, 0f, 0f)), GrasslandPipeline.Point(linksRoot, "Link_E", new Vector3(7f, 0f, 0f)) };
                var area = new WanderArea(def.WanderCenter, def.WanderRadius);
                chunk.SetAuthoring(bindings.ToArray(), area, null, animals, Interests(layout, eat, area), anchors, links);
                chunk.SetScatter(scatter);
                var (obstacles, groundField) = GrasslandPipeline.BuildCollision(layout, prefabs, groundMesh, waterMesh);
                chunk.SetCollision(obstacles, groundField);
                GrasslandPipeline.ClearSpawns(animals, obstacles, groundField);
                PrefabUtility.SaveAsPrefabAsset(root, def.Path);
                int renderers = root.GetComponentsInChildren<MeshRenderer>(true).Length;
                return $"{def.Name}: {layout.Count} pieces ({ferns} ferns; {(moves.Count == 0 ? "none moved" : "settled: " + string.Join(", ", moves))}), " +
                       $"{obstacles.Length} obstacles, {scatter.Count} scatter instances, {renderers} renderers";
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Prop instances under Props (landmarks, trees, bushes, rocks, ferns, mushrooms, decor) with their palette bindings, then
        /// the merged looks: every fern of the chunk in ONE mesh on MAT_Foliage_Grassland (uv1 = each fern's root, so each sways
        /// from its own base), every mushroom in one mesh on MAT_Environment_Common (each one's brightness in uv.y).
        /// </summary>
        private static void ComposeProps(ForestChunk def, Transform root, List<Placement> layout, Dictionary<string, GameObject> prefabs,
            List<WorldChunk.PaletteBinding> bindings)
        {
            var props = GrasslandPipeline.Child(root, "Props");
            var groups = new (string prefix, Transform group)[]
            {
                ("FO_Landmark", GrasslandPipeline.Child(props, "Landmarks")), ("FO_Tree", GrasslandPipeline.Child(props, "Trees")),
                ("FO_Bush", GrasslandPipeline.Child(props, "Bushes")), ("FO_MossRock", GrasslandPipeline.Child(props, "Rocks")),
                ("FO_Fern", GrasslandPipeline.Child(props, "Ferns")), ("FO_Mushroom", GrasslandPipeline.Child(props, "Mushrooms")),
                ("FO_", GrasslandPipeline.Child(props, "Decor")),
            };
            var rng = new Random(def.Seed);
            foreach (var placement in layout)
            {
                var group = groups.First(g => placement.Prop.StartsWith(g.prefix, StringComparison.Ordinal)).group;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[placement.Prop], group);
                instance.transform.SetLocalPositionAndRotation(placement.Position, Quaternion.Euler(0f, placement.Yaw, 0f));
                instance.transform.localScale = Vector3.one * placement.Scale;
                byte brightness = (byte)(placement.Prop.StartsWith("FO_Tree", StringComparison.Ordinal) ? 6 + rng.Next(5) : 5 + rng.Next(7));
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    bindings.Add(new WorldChunk.PaletteBinding
                    {
                        renderer = renderer, paletteId = Id(placement.Palette), brightness = brightness, secondary = placement.Secondary,
                    });
                }
            }

            var fernPieces = layout.Where(p => IsFern(p.Prop)).ToList();
            if (fernPieces.Count > 0)
            {
                var foliage = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.FoliageMaterialPath) ?? GrasslandPipeline.EnsureFoliageMaterial();
                var mesh = GrasslandPipeline.SaveBakedMesh(MergedFolder, $"FO_Merged_{def.Name}_Ferns", GrasslandPipeline.Merge(fernPieces, prefabs, true, null));
                var group = groups.First(g => g.prefix == "FO_Fern").group;
                var renderer = GrasslandPipeline.AddRenderer(GrasslandPipeline.Child(group, "FernsMerged").gameObject, mesh, foliage, ShadowCastingMode.Off);
                bindings.Add(new WorldChunk.PaletteBinding { renderer = renderer, paletteId = Id(PaletteFern), brightness = WorldChunk.NeutralBrightness });
            }
            var mushroomPieces = layout.Where(p => IsMushroom(p.Prop)).ToList();
            if (mushroomPieces.Count > 0)
            {
                var env = AssetDatabase.LoadAssetAtPath<Material>(GrasslandPipeline.EnvironmentMaterialPath);
                var mesh = GrasslandPipeline.SaveBakedMesh(MergedFolder, $"FO_Merged_{def.Name}_Mushrooms",
                    GrasslandPipeline.Merge(mushroomPieces, prefabs, false, new Random(def.Seed + 5)));
                var group = groups.First(g => g.prefix == "FO_Mushroom").group;
                var renderer = GrasslandPipeline.AddRenderer(GrasslandPipeline.Child(group, "MushroomsMerged").gameObject, mesh, env, ShadowCastingMode.On);
                bindings.Add(new WorldChunk.PaletteBinding
                {
                    renderer = renderer, paletteId = Id(PaletteMushroom), brightness = WorldChunk.NeutralBrightness, vertexBrightness = true,
                });
            }
        }

        // ---------------- ferns ----------------

        /// <summary>
        /// The ferns of a chunk, each one only where its measured outline is free (props, other ferns, the neighbours' props at the
        /// seams), off the worn path and out of the water: lining the path on both sides, at the feet of trees, stumps, logs and
        /// big rocks, on the brook's banks, and as young upright fronds in the lake's damp corner. Returns how many were placed.
        /// </summary>
        private static int AddFerns(ForestChunk def, List<Placement> layout, Dictionary<string, GameObject> prefabs, Vector3 origin,
            Func<Vector3, float, bool> excluded, Func<Vector3, float, bool> inside)
        {
            var rng = new Random(def.Seed + 17);
            var placed = GrasslandPipeline.PlaceLayout(layout, prefabs);
            foreach (var entry in GrasslandPipeline.BuiltProps)
            {
                if (entry.Key == def.SettleName)
                    continue;
                foreach (var p in entry.Value)
                {
                    if (Mathf.Abs(p.position.x - origin.x) > 10f || !prefabs.TryGetValue(p.prop, out var neighbour))
                        continue;
                    placed.Add(CollisionProxies.Place(CollisionProxies.Measure(neighbour), p.position - origin, p.yaw, p.scale));
                }
            }
            var proxies = new Dictionary<string, CollisionProxies.Proxy>
            {
                ["FO_Fern_A"] = CollisionProxies.Measure(prefabs["FO_Fern_A"]), ["FO_Fern_B"] = CollisionProxies.Measure(prefabs["FO_Fern_B"]),
            };
            int count = 0;
            bool TryAdd(string kind, Vector3 p, float yaw, float scale)
            {
                float r = PropRadius(kind) * scale;
                if (!inside(p, r) || (excluded != null && excluded(p, r)))
                    return false;
                if (p.x < def.PathEnd && Mathf.Abs(p.z - GrasslandPipeline.PathZ(origin.x + p.x)) < PathHalf + r)
                    return false;
                var candidate = CollisionProxies.Place(proxies[kind], p, yaw, scale);
                foreach (var other in placed)
                {
                    if (CollisionProxies.Overlap(other, candidate, out _) > 0f)
                        return false;
                }
                layout.Add(P(kind.Substring(3), p.x, p.z, yaw, scale));
                placed.Add(candidate);
                count++;
                return true;
            }
            string Kind() => rng.NextDouble() < 0.55 ? "FO_Fern_A" : "FO_Fern_B";
            float Scale() => 0.85f + (float)rng.NextDouble() * 0.3f;
            float Yaw() => (float)(rng.NextDouble() * 360.0);

            // Along the path, both sides.
            foreach (var (x0, x1) in def.FernRuns)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    for (float x = x0 + (float)rng.NextDouble() * 0.6f; x < x1; x += 0.9f + (float)rng.NextDouble() * 0.9f)
                    {
                        string kind = Kind();
                        float scale = Scale();
                        float z = GrasslandPipeline.PathZ(origin.x + x) + side * (PathHalf + PropRadius(kind) * scale + 0.04f + (float)rng.NextDouble() * 0.3f);
                        TryAdd(kind, new Vector3(x, 0f, z), Yaw(), scale);
                    }
                }
            }
            // At the feet of the trees and of the big pieces.
            foreach (var piece in layout.Where(p => p.Prop.StartsWith("FO_Tree", StringComparison.Ordinal) || p.Prop.StartsWith("FO_Landmark", StringComparison.Ordinal)
                                                    || p.Prop == "FO_Stump" || p.Prop == "FO_HollowLog" || p.Prop == "FO_RootArch"
                                                    || p.Prop == "FO_MossRock_L" || p.Prop == "FO_MossRock_Hero").ToList())
            {
                int wanted = piece.Prop.StartsWith("FO_Landmark", StringComparison.Ordinal) ? 3 : piece.Prop.StartsWith("FO_Tree", StringComparison.Ordinal) ? 2 : 1;
                for (int n = 0, tries = 0; n < wanted && tries < 10; tries++)
                {
                    string kind = Kind();
                    float scale = Scale();
                    float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float d = PropRadius(piece.Prop) * piece.Scale + PropRadius(kind) * scale + 0.05f + (float)rng.NextDouble() * 0.15f;
                    if (TryAdd(kind, piece.Position + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), Yaw(), scale))
                        n++;
                }
            }
            // On the brook's banks, both sides of the water.
            if (def.Brook)
            {
                for (int i = 0; i < BrookPoints.Length - 1; i++)
                {
                    var a = BrookPoints[i];
                    var b = BrookPoints[i + 1];
                    var tangent = (b - a).normalized;
                    var normal = new Vector2(-tangent.y, tangent.x);
                    for (float t = (float)rng.NextDouble() * 0.5f; t < (b - a).magnitude; t += 0.8f + (float)rng.NextDouble() * 0.7f)
                    {
                        foreach (int side in new[] { -1, 1 })
                        {
                            string kind = Kind();
                            float scale = Scale();
                            var q = a + tangent * t + normal * side * (0.62f + PropRadius(kind) * scale + (float)rng.NextDouble() * 0.25f);
                            TryAdd(kind, new Vector3(q.x, 0f, q.y), Yaw(), scale);
                        }
                    }
                }
            }
            // Reed-like young ferns in the damp corner.
            for (int n = 0, tries = 0; n < def.Reeds.count && tries < def.Reeds.count * 12; tries++)
            {
                var area = def.Reeds.area;
                var p = new Vector3(Mathf.Lerp(area.x, area.y, (float)rng.NextDouble()), 0f, Mathf.Lerp(area.z, area.w, (float)rng.NextDouble()));
                if (TryAdd("FO_Fern_B", p, Yaw(), 0.95f + (float)rng.NextDouble() * 0.3f))
                    n++;
            }
            return count;
        }

        // ---------------- interests ----------------

        private static InterestPoint[] Interests(List<Placement> layout, List<Vector3> eat, WanderArea area)
        {
            var result = new List<InterestPoint>();
            void Add(Vector3 position, float standOff, InterestKind kind)
            {
                if (Vector3.Distance(new Vector3(position.x, 0f, position.z), new Vector3(area.Center.x, 0f, area.Center.z)) <= area.Radius + standOff + 0.2f)
                    result.Add(new InterestPoint(position, standOff, kind));
            }
            foreach (var p in layout)
            {
                if (IsFern(p.Prop) || IsCrossing(p.Prop))
                    continue;
                Add(p.Position, PropRadius(p.Prop) * p.Scale, InterestKind.Inspect);
            }
            foreach (var c in eat)
                Add(c, 0.16f, InterestKind.Eat);
            return result.ToArray();
        }

        // ---------------- scatter ----------------

        /// <summary>
        /// The ground details of a Forest chunk from the ground's own paint (PW_Regions: R moss, G leaf litter, B meadow, A worn
        /// path): the Prairie's grass where it is meadow (the Entry's west, the Clearing), moss cushions on the moss, fallen
        /// leaves and twigs on the litter, more leaves, acorns and pine cones under the trees, clover patches, low flower patches
        /// in the meadow, pebbles on the path's edges. Nothing grows inside a prop, solid details keep apart, nothing in the water.
        /// </summary>
        private static List<ScatterPoint> GenerateScatter(ForestChunk def, ScatterKit kit, List<Placement> props, List<CollisionProxies.Placed> solids,
            GroundSampler sampler, RegionSampler regions, Func<Vector3, bool> inside, Func<Vector3, float, bool> excluded, List<Vector3> eat)
        {
            var rng = new Random(def.Seed);
            var points = new List<ScatterPoint>();
            int Kind(string id)
            {
                int at = kit.IndexOf(id);
                if (at < 0)
                    throw new InvalidOperationException("DEF_ScatterKit_Forest has no " + id);
                return at;
            }
            var kindRadius = kit.kinds.Select(k => k.mesh != null ? MeshRadius(k.mesh) : 0.05f).ToArray();
            var solidKind = kit.kinds.Select(k => IsSolidScatter(k.id)).ToArray();
            var outlines = solids.Select(s => s.Ground).Where(b => b != null).Select(b =>
            {
                var c = Vector2.zero;
                foreach (var v in b.Hull)
                    c += v;
                c /= b.Hull.Length;
                return (hull: b.Hull, center: c, reach: b.Hull.Max(v => (v - c).magnitude));
            }).ToList();
            var cells = new Dictionary<(int, int), List<(Vector2 p, float r)>>();
            float maxSolid = 0.05f;

            float Clearance(Vector3 p)
            {
                var q = new Vector2(p.x, p.z);
                float best = float.MaxValue;
                foreach (var (hull, center, reach) in outlines)
                {
                    if ((q - center).magnitude - reach >= best)
                        continue;
                    best = Mathf.Min(best, CollisionProxies.HullDistance(hull, q));
                }
                return best;
            }
            bool SolidFree(Vector2 q, float r)
            {
                int range = Mathf.CeilToInt((r + maxSolid) / 0.25f);
                int cx = Mathf.FloorToInt(q.x / 0.25f), cz = Mathf.FloorToInt(q.y / 0.25f);
                for (int x = cx - range; x <= cx + range; x++)
                    for (int z = cz - range; z <= cz + range; z++)
                        if (cells.TryGetValue((x, z), out var list))
                            foreach (var (other, radius) in list)
                                if ((other - q).sqrMagnitude < (radius + r) * (radius + r))
                                    return false;
                return true;
            }
            bool Place(int kind, Vector3 p, float scale, float minClearance = 0.02f)
            {
                if (!inside(p))
                    return false;
                float radius = kindRadius[kind] * scale;
                var q = new Vector2(p.x, p.z);
                if (Clearance(p) < Mathf.Max(minClearance, radius) || (excluded != null && excluded(p, radius)))
                    return false;
                if (solidKind[kind] && !SolidFree(q, radius))
                    return false;
                if (!sampler.TrySample(p.x, p.z, out float height, out var normal) || normal.y < 0.8f)
                    return false;
                points.Add(new ScatterPoint
                {
                    Kind = kind, Position = new Vector3(p.x, height - 0.01f, p.z), Yaw = (float)(rng.NextDouble() * Math.PI * 2.0),
                    Scale = scale, Rank = rng.NextDouble(),
                });
                if (solidKind[kind])
                {
                    var cell = (Mathf.FloorToInt(q.x / 0.25f), Mathf.FloorToInt(q.y / 0.25f));
                    if (!cells.TryGetValue(cell, out var list))
                        cells[cell] = list = new List<(Vector2, float)>();
                    list.Add((q, radius));
                    maxSolid = Mathf.Max(maxSolid, radius);
                }
                return true;
            }
            float Noise(float x, float z, float scale, float offset) => Mathf.PerlinNoise(x * scale + offset, z * scale + offset * 1.7f);
            float R(float min, float span) => min + (float)rng.NextDouble() * span;
            Vector3 Around(Vector3 c, float r0, float r1)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2.0);
                float d = R(r0, r1 - r0);
                return c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            }

            // 1. The carpet, from the ground's paint.
            const float Spacing = 0.22f;
            const int CarpetCap = 3300;
            int[] tufts = { Kind("grass.tuft_s"), Kind("grass.tuft_m"), Kind("grass.tuft_l") };
            int wisp = Kind("grass.wisp_m"), moss = Kind("moss.tuft"), leafA = Kind("detail.leaf_a"), leafB = Kind("detail.leaf_b"), twig = Kind("detail.twig");
            for (float x = -7f; x <= 7f && points.Count < CarpetCap; x += Spacing)
            {
                for (float z = -7f; z <= 7f; z += Spacing)
                {
                    var p = new Vector3(x + (float)(rng.NextDouble() - 0.5) * Spacing * 0.9f, 0f, z + (float)(rng.NextDouble() - 0.5) * Spacing * 0.9f);
                    double roll = rng.NextDouble();
                    if (!regions.TrySample(p.x, p.z, out var w))
                        continue;
                    float open = Mathf.Clamp01(1f - w.a * 1.8f);
                    float grass = w.b * Mathf.Lerp(0.45f, 1f, Noise(p.x, p.z, 0.32f, def.Seed * 0.013f)) * open;
                    float cushions = w.r * open * 0.3f * Mathf.Lerp(0.55f, 1f, Noise(p.x, p.z, 0.45f, 11f + def.Seed * 0.01f));
                    float litter = w.g * Mathf.Clamp01(1f - w.a * 1.4f) * 0.26f;
                    if (roll < grass)
                    {
                        bool hug = Clearance(p) < 0.5f;
                        float tall = Noise(p.x, p.z, 0.5f, 40f + def.Seed * 0.01f);
                        double r = rng.NextDouble();
                        if (w.a > 0.25f)
                            Place(tufts[0], p, R(0.72f, 0.2f), 0.04f);
                        else if (hug || tall > 0.64f)
                            Place(r < 0.3 ? wisp : r < 0.7 ? tufts[2] : tufts[1], p, R(0.95f, 0.3f), 0.04f);
                        else
                            Place(r < 0.38 ? tufts[1] : r < 0.68 ? tufts[0] : r < 0.9 ? tufts[2] : wisp, p, R(0.85f, 0.35f), 0.04f);
                    }
                    else if (roll < grass + cushions)
                    {
                        Place(moss, p, R(0.8f, 0.5f), 0.03f);
                    }
                    else if (roll < grass + cushions + litter)
                    {
                        double r = rng.NextDouble();
                        Place(r < 0.46 ? leafA : r < 0.92 ? leafB : twig, p, R(0.8f, 0.5f));
                    }
                }
            }

            // 2. Under the trees and around the old wood.
            int acorn = Kind("detail.acorn"), cone = Kind("detail.pinecone");
            foreach (var prop in props)
            {
                var c = prop.Position;
                float s = prop.Scale;
                float foot = PropRadius(prop.Prop) * s;
                if (prop.Prop.StartsWith("FO_Tree", StringComparison.Ordinal) || prop.Prop == "FO_Landmark_AncientTree")
                {
                    float crown = prop.Prop == "FO_Landmark_AncientTree" ? 3.0f : prop.Prop == "FO_Tree_A" ? 1.7f : prop.Prop == "FO_Tree_B" ? 1.1f : 1.3f;
                    int leaves = 8 + rng.Next(7);
                    for (int i = 0; i < leaves; i++)
                        Place(rng.NextDouble() < 0.5 ? leafA : leafB, Around(c, foot + 0.1f, crown * s), R(0.8f, 0.5f));
                    if (prop.Prop == "FO_Tree_A" || prop.Prop == "FO_Landmark_AncientTree")
                    {
                        int n = 3 + rng.Next(4);
                        for (int i = 0; i < n; i++)
                            Place(acorn, Around(c, foot + 0.1f, 1.4f * s), R(0.9f, 0.4f));
                    }
                    else if (prop.Prop == "FO_Tree_B")
                    {
                        int n = 3 + rng.Next(3);
                        for (int i = 0; i < n; i++)
                            Place(cone, Around(c, foot + 0.1f, 1.1f * s), R(0.9f, 0.4f));
                    }
                }
                else if (prop.Prop == "FO_Stump" || prop.Prop == "FO_HollowLog" || prop.Prop == "FO_Landmark_GiantStump" || prop.Prop == "FO_RootArch")
                {
                    for (int i = 0; i < 2; i++)
                        Place(twig, Around(c, foot + 0.08f, foot + 0.5f), R(0.8f, 0.4f));
                    for (int i = 0; i < 3; i++)
                        Place(rng.NextDouble() < 0.5 ? leafA : leafB, Around(c, foot + 0.05f, foot + 0.6f), R(0.8f, 0.5f));
                }
            }

            // 3. Clover patches on moss and meadow (a nibble for the animals).
            int clover = Kind("detail.clover");
            int patches = 0;
            for (int attempt = 0; attempt < 500 && patches < 7; attempt++)
            {
                var center = new Vector3(R(-6.3f, 12.6f), 0f, R(-6.3f, 12.6f));
                if (!regions.TrySample(center.x, center.z, out var w) || w.a > 0.2f || w.r + w.b < 0.5f || Clearance(center) < 0.3f
                    || Noise(center.x, center.z, 0.6f, 7f) < 0.45f || eat.Any(e => Vector3.Distance(e, center) < 1.4f))
                    continue;
                int placed = 0;
                int count = 4 + rng.Next(5);
                for (int i = 0; i < count; i++)
                    if (Place(clover, Around(center, 0f, 0.45f), R(0.9f, 0.5f), 0.05f))
                        placed++;
                if (placed == 0)
                    continue;
                eat.Add(center);
                patches++;
            }

            // 4. Low flowers in the meadow: small patches of one kind, a few singles.
            (int kind, float weight)[] flowers = { (Kind("flower.daisy_white"), 0.45f), (Kind("flower.buttercup"), 0.35f), (Kind("flower.bell_blue"), 0.2f) };
            int PickFlower()
            {
                double r = rng.NextDouble();
                foreach (var (kind, weight) in flowers)
                    if ((r -= weight) <= 0)
                        return kind;
                return flowers[0].kind;
            }
            var flowerCenters = new List<Vector3>();
            for (int attempt = 0; attempt < 700 && flowerCenters.Count < def.FlowerPatches; attempt++)
            {
                var center = new Vector3(R(-6.3f, 12.6f), 0f, R(-6.3f, 12.6f));
                if (!regions.TrySample(center.x, center.z, out var w) || w.b < 0.55f || w.a > 0.15f || Clearance(center) < 0.3f
                    || flowerCenters.Any(c => Vector3.Distance(c, center) < 1.1f))
                    continue;
                int kind = PickFlower();
                int count = 5 + rng.Next(6);
                int placed = 0;
                for (int i = 0; i < count * 2 && placed < count; i++)
                {
                    var p = Around(center, 0f, 0.48f);
                    if (Place(kind, p, R(1.15f, 0.3f), 0.06f))
                        placed++;
                }
                if (placed == 0)
                    continue;
                flowerCenters.Add(center);
                eat.Add(center);
            }
            for (int i = 0, tries = 0; i < def.FlowerSingles && tries < def.FlowerSingles * 12; tries++)
            {
                var p = new Vector3(R(-6.5f, 13f), 0f, R(-6.5f, 13f));
                if (!regions.TrySample(p.x, p.z, out var w) || w.b < 0.5f || w.a > 0.15f)
                    continue;
                if (Place(PickFlower(), p, R(1.1f, 0.3f), 0.08f))
                    i++;
            }

            // 5. Pebbles on the path's edges.
            int pebble = Kind("detail.pebble_a");
            for (int i = 0, tries = 0; i < 22 && tries < 700; tries++)
            {
                var p = new Vector3(R(-6.5f, 13f), 0f, R(-6.5f, 13f));
                if (!regions.TrySample(p.x, p.z, out var w) || w.a < 0.08f || w.a > 0.5f)
                    continue;
                if (Place(pebble, p, R(0.7f, 0.5f)))
                    i++;
            }
            return points;
        }

        /// <summary>The ground's paint under a point (PW_Regions weights: R moss, G litter, B meadow, A path), interpolated on its top faces.</summary>
        private sealed class RegionSampler
        {
            private const float CellSize = 1f;
            private const int Cells = 16;
            private readonly Vector3[] _vertices;
            private readonly int[] _triangles;
            private readonly Color[] _colors;
            private readonly List<int>[,] _cells;

            public RegionSampler(Mesh mesh)
            {
                _vertices = mesh.vertices;
                _triangles = mesh.triangles;
                var colors = mesh.colors;
                _colors = colors.Length == _vertices.Length ? colors : new Color[_vertices.Length];
                _cells = new List<int>[Cells, Cells];
                for (int t = 0; t < _triangles.Length; t += 3)
                {
                    var a = _vertices[_triangles[t]];
                    var b = _vertices[_triangles[t + 1]];
                    var c = _vertices[_triangles[t + 2]];
                    if (Vector3.Cross(b - a, c - a).y <= 0f)
                        continue; // walls and the underside carry no paint for the scatter
                    int x0 = Cell(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Cell(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                    int z0 = Cell(Mathf.Min(a.z, Mathf.Min(b.z, c.z))), z1 = Cell(Mathf.Max(a.z, Mathf.Max(b.z, c.z)));
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                            (_cells[x, z] ??= new List<int>()).Add(t);
                }
            }

            private static int Cell(float v) => Mathf.Clamp(Mathf.FloorToInt((v + Cells * CellSize * 0.5f) / CellSize), 0, Cells - 1);

            public bool TrySample(float x, float z, out Color weights)
            {
                weights = Color.clear;
                var list = _cells[Cell(x), Cell(z)];
                if (list == null)
                    return false;
                foreach (int t in list)
                {
                    int ia = _triangles[t], ib = _triangles[t + 1], ic = _triangles[t + 2];
                    var a = _vertices[ia];
                    var b = _vertices[ib];
                    var c = _vertices[ic];
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 1e-8f)
                        continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                    float w = 1f - u - v;
                    if (u < -1e-4f || v < -1e-4f || w < -1e-4f)
                        continue;
                    weights = _colors[ia] * u + _colors[ib] * v + _colors[ic] * w;
                    return true;
                }
                return false;
            }
        }
    }
}
