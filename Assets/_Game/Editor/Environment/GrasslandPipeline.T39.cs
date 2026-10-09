using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Activities;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T39 « Cache-cache »: the Prairie's hiding decor in the refuge itself (doc 01 Contenu S-ACT-002, « Thèmes par biome »,
    /// Prairie: « hautes herbes, derrière les petits arbres, derrière les buissons fleuris »; queue T39 « cachettes dans tous
    /// les chunks Prairie »). Agent HB's GL_TallGrass_A/B/C and GL_FlowerBush_A/B (GL_Props_T39.fbx,
    /// BL_Biome_Grassland_T39.blend, pw_grassland_t39_props.py) join every Grassland chunk's layout before it is settled,
    /// so they go through the T33 composition like any prop: settled against every volume (seams included), palette
    /// bindings, obstacles measured from their meshes (T33C), scatter keep-out (no tuft or flower grows inside them),
    /// interest points. The clumps stand in patches of two (6 cm apart, never intersecting, their blades stretched to about
    /// 1 m), the flowered bushes at × 1.5; they are shape-only prefabs, and each chunk draws all its patches in one merged
    /// renderer and its bushes in one per palette row (FoliageGusts is positional, so a patch still ripples where the hint
    /// is). Each chunk gets its flowered bushes, then its patches, in its hiding zone (<see cref="HideZoneFocus"/>), where an
    /// animal behind them hides best from the zone camera: the places are measured on the layout itself (the hide-spot
    /// pipeline's silhouette meter, the hiding pose of every species) with the same rules as the spots (only the decor itself
    /// hides, the decor shows past every other prop, a landing a clear hop away where every species shows, the island and
    /// never its edge behind the animal, 1.5 m from the strip's edge), near a prop or a meadow edge; never on the worn path,
    /// the egg spot, the walk area's heart or the water.
    /// </summary>
    public static partial class GrasslandPipeline
    {
        public const string PaletteTallGrass = "palette.grassland.tall_grass";
        public const string PaletteFlowerBushPink = "palette.grassland.flower_bush_pink";
        public const string PaletteFlowerBushCream = "palette.grassland.flower_bush_cream";
        public const string PaletteFlowerBushYellow = "palette.grassland.flower_bush_yellow";
        public const string PatchFolder = BakedFolder + "/Patches";

        /// <summary>The T39 props (single clumps keep their prefabs, PF_&lt;name&gt; in <see cref="PrefabsFolder"/>).</summary>
        public static readonly string[] HideDecorProps = { "GL_TallGrass_A", "GL_TallGrass_B", "GL_TallGrass_C", "GL_FlowerBush_A", "GL_FlowerBush_B" };

        public static bool IsTallGrass(string prop) => prop != null && prop.StartsWith("GL_TallGrass", StringComparison.Ordinal);
        public static bool IsFlowerBush(string prop) => prop != null && prop.StartsWith("GL_FlowerBush", StringComparison.Ordinal);

        /// <summary>
        /// Scale of the hiding decor in the chunks. HB modeled the clumps 0.43 to 0.59 m and the bushes 0.56 to 0.59 m; in a
        /// patch the two clumps stand at × 1.4, their blades stretched up (<see cref="PatchHeightStretch"/>, about 1 m); the
        /// flowered bushes at × 1.5 (about 0.86 m, dense: alone they hide an eating rabbit to 0 to 20 % from the zone camera,
        /// round 3's rule that no other prop does the hiding).
        /// </summary>
        public static float HideDecorScale(string prop) => IsFlowerBush(prop) ? 1.5f : IsTallGrass(prop) ? PatchClumpScale : 1f;

        /// <summary>The patch's back clumps (next to the hidden animal: the ones that hide it) and its low front one.</summary>
        public const float PatchClumpScale = 1.4f;
        public const float PatchFrontScale = 1.25f;

        /// <summary>
        /// The patch's blades grow this much taller than the clumps' scale (the clumps stretched up, their footprint kept): about
        /// 1 m of tall grass, so the patch alone hides an eating rabbit from the zone camera (round 3: no other prop may do the
        /// hiding for it).
        /// </summary>
        public const float PatchHeightStretch = 1.3f;
        private const float PatchGap = 0.06f;

        /// <summary>
        /// Each chunk's hide-and-seek camera (chunk space): the ground point at the middle of its frame (the part of the chunk
        /// richest in hiding decor), its heading (degrees: 0 looks north as the refuge's own view does, 90 east and -90 west
        /// along the strip) and its elevation (the pitch it is placed at, <see cref="HideSpotPipeline.NominalDistance"/> from the
        /// focus). The frame's background must be the island (meadow, other chunks, trees), never its edge or the void: along
        /// the strip the meadow goes on; looking north the frame stops short of the back edge. Keys are the chunk names of the
        /// layouts; <see cref="HideZoneFocusFor"/> and <see cref="HideZoneViewFor"/> read them by address.
        /// </summary>
        public static readonly (string chunk, string address, Vector2 focus, float yaw, float elevation)[] HideZoneFocus =
        {
            // The start chunk looks north as the refuge does (its west end is the island's end), just west of the egg (whose
            // surroundings stay open for the newborns).
            ("Start", "chunk.grassland.start", new Vector2(-1.0f, 1.0f), 0f, 24f),
            // The strip's chunks look west along the strip (the meadow goes on behind; the day's sun comes from behind
            // the camera), over their south half (the path runs along the middle).
            ("Meadow", "chunk.grassland.meadow", new Vector2(0f, -1.5f), -90f, 24f),
            ("StreamBend", "chunk.grassland.streambend", new Vector2(0f, -1.5f), -90f, 24f),
            ("Orchard", "chunk.grassland.orchard", new Vector2(0f, -1.5f), -90f, 24f),
            ("Hill", "chunk.grassland.hill", new Vector2(0f, -1.5f), -90f, 24f),
            ("WindmillRise", "chunk.grassland.windmillrise", new Vector2(0f, -1.5f), -90f, 24f),
            ("PondEdge", "chunk.grassland.pondedge", new Vector2(0f, -1.5f), -90f, 24f),
            // The island's east end: the camera stands over the chunk's west half so the frame's foot stays on the ground.
            ("ForestEdge", "chunk.grassland.forestedge", new Vector2(-1.0f, -1.5f), -90f, 24f),
        };

        /// <summary>Diagnostics: a zone camera tried without editing the table (address to focus, heading, elevation).</summary>
        public static readonly Dictionary<string, (Vector2 focus, float yaw, float elevation)> HideZoneTrial =
            new Dictionary<string, (Vector2, float, float)>();

        /// <summary>The hiding zone's focus of a chunk (null: none authored, the hide-spot pipeline searches the chunk).</summary>
        public static Vector2? HideZoneFocusFor(string address)
        {
            if (address != null && HideZoneTrial.TryGetValue(address, out var trial))
                return trial.focus;
            foreach (var z in HideZoneFocus)
                if (z.address == address)
                    return z.focus;
            return null;
        }

        /// <summary>The hiding zone camera's heading and elevation (degrees); north at the nominal pitch when none is authored.</summary>
        public static (float yaw, float elevation) HideZoneViewFor(string address)
        {
            if (address != null && HideZoneTrial.TryGetValue(address, out var trial))
                return (trial.yaw, trial.elevation);
            foreach (var z in HideZoneFocus)
                if (z.address == address)
                    return (z.yaw, z.elevation);
            return (0f, HideSpotPipeline.NominalPitch);
        }

        /// <summary>
        /// Patches and flowered bushes per chunk (as many as fit in the zone's frame; the scout stops where no place hides).
        /// The bushes go first (they hide by themselves); the two patches keep the Prairie's tall grass in every chunk, out of
        /// the zone camera's view when no place there hides. They are drawn merged (one renderer for the tall grass, one per
        /// bush palette row), so they cost 1 to 4 of a chunk's 50 renderers (WorldStripTests).
        /// </summary>
        private static readonly Dictionary<string, (int patches, int bushes)> HideDecorPlan = new Dictionary<string, (int, int)>
        {
            // Round 4: up to two more bushes than round 3 placed (the paths, the walk areas' hearts and the egg kept open).
            ["Start"] = (2, 6), ["Meadow"] = (2, 7), ["StreamBend"] = (2, 7), ["Orchard"] = (2, 7),
            ["Hill"] = (2, 7), ["WindmillRise"] = (2, 7), ["PondEdge"] = (2, 7), ["ForestEdge"] = (2, 5),
        };

        /// <summary>
        /// The clumps of patch i, side by side (a third clump in front made the patch round: its measured volume, a disc,
        /// then kept the hidden animal too far behind it; two in a row give a long, shallow volume the animal hugs).
        /// </summary>
        private static readonly string[][] PatchClumps =
        {
            new[] { "GL_TallGrass_A", "GL_TallGrass_B" },
            new[] { "GL_TallGrass_B", "GL_TallGrass_A" },
            new[] { "GL_TallGrass_A", "GL_TallGrass_A" },
            new[] { "GL_TallGrass_B", "GL_TallGrass_B" },
            new[] { "GL_TallGrass_B", "GL_TallGrass_A" },
        };

        /// <summary>Flowered bushes take the three daisy colors in turn (HB's rows), chunk by chunk.</summary>
        private static string FlowerBushPalette(string chunkName, int index)
        {
            string[] rows = { PaletteFlowerBushPink, PaletteFlowerBushCream, PaletteFlowerBushYellow };
            int hash = 0;
            foreach (char c in chunkName)
                hash = hash * 31 + c;
            return rows[(int)((uint)(hash + index) % (uint)rows.Length)];
        }

        /// <summary>
        /// Distance kept from the egg's place (beyond the decor's reach: the newborns and the crowd around the egg keep their
        /// room, CollisionRuleTests' hatches in a row) and from the walk area's center (where the refuge's life gathers).
        /// </summary>
        private const float HideDecorEggClearance = 2.6f;
        private const float HideDecorHeartClearance = 1.5f;
        private const float HideDecorGap = 0.1f;

        /// <summary>Room left between two hiding decor of a chunk (beyond their outlines): one spot behind each, 0.9 m apart.</summary>
        private const float HideDecorApart = 0.45f;

        /// <summary>The last build's lines (one per chunk), for the T39 report.</summary>
        public static readonly List<string> HideDecorReport = new List<string>();

        [MenuItem("Pocket Wilds/Environment/Build Grassland Strip and Hide Decor (T39)", priority = 177)]
        public static void BuildHideDecorMenu()
        {
            HideDecorReport.Clear();
            string strip = BuildStrip();
            Debug.Log("[GrasslandPipeline] T39 hide decor: " + string.Join("; ", HideDecorReport) + "\n" + strip);
        }

        /// <summary>
        /// The chunk's layout with its T39 hiding decor appended (called by the T33 and T33B chunk builders before the props
        /// are settled): its tall grass patches (merged meshes and prefabs made here) and flowered bushes, each where the
        /// zone camera measures the best hiding behind it. A missing model leaves the layout as it is.
        /// </summary>
        private static List<Placement> WithHideDecorT39(string chunkName, List<Placement> layout, Dictionary<string, GameObject> prefabs,
            Mesh groundMesh, Mesh waterMesh)
        {
            if (!HideDecorPlan.TryGetValue(chunkName, out var plan) || groundMesh == null)
                return layout;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            EnsureHideDecorPalettes();
            var missing = EnsureHideDecorPrefabs(prefabs);
            var lines = new List<string>();
            if (missing.Count > 0)
                lines.Add("missing models: " + string.Join(", ", missing));
            var clumps = new Dictionary<string, Mesh>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Mesh>())
                    if (IsTallGrass(mesh.name))
                        clumps[mesh.name] = mesh;

            bool strip = chunkName != "Start" && chunkName != "Meadow";
            var scout = new HideDecorScout(chunkName, layout, prefabs, groundMesh, waterMesh, strip, lines);
            if (!scout.Ready)
            {
                Report(chunkName, lines);
                return layout;
            }

            // The flowered bushes first: dense, they hide by themselves; the tall grass patches (the theme, lighter) after.
            for (int i = 0; i < plan.bushes; i++)
            {
                string prop = i % 2 == 0 ? "GL_FlowerBush_A" : "GL_FlowerBush_B";
                if (!prefabs.TryGetValue(prop, out var prefab) || prefab == null)
                    continue;
                var found = scout.BestProp(prefab, HideDecorScale(prop), false, out float yaw, out float score);
                if (found == null)
                {
                    lines.Add($"{prop}: no place where it hides {scout.LastSearch}");
                    continue;
                }
                var placement = new Placement
                {
                    Prop = prop, Position = new Vector3(found.Value.x, 0f, found.Value.y), Yaw = yaw, Scale = HideDecorScale(prop),
                    Palette = FlowerBushPalette(chunkName, i),
                };
                layout.Add(placement);
                scout.Add(placement, prefab);
                lines.Add($"{prop} ({found.Value.x:F2}, {found.Value.y:F2}) x{placement.Scale:F2}, hides to {score * 100f:F0} %");
            }

            int made = 0;
            for (int i = 0; i < plan.patches; i++)
            {
                var names = PatchClumps[i % PatchClumps.Length];
                if (names.Any(n => !clumps.ContainsKey(n)))
                    break;
                var patch = BuildPatchMesh(names.Select(n => clumps[n]).ToArray(), i * 37 + chunkName.Length * 11, out var hull, out float height);
                string name = $"GL_TallGrassPatch_{chunkName}_{made}";
                var prefab = SavePatch(name, patch);
                // Measured from its own mesh like every prop (the cache would keep an earlier build's outline).
                CollisionProxies.ClearCache();
                // The Prairie theme stands in every chunk (at least two patches): the last ones take the best place even
                // where it hides less (a spot may still come of it, or it is the meadow's decor).
                bool needed = made < 2 && plan.patches - i <= 2 - made;
                var found = scout.BestProp(prefab, 1f, true, out float yaw, out float score, needed);
                if (found == null)
                {
                    lines.Add($"patch {i} ({names.Length} clumps): no place where it hides {scout.LastSearch}");
                    continue;
                }
                prefabs[name] = prefab;
                var placement = new Placement
                {
                    Prop = name, Position = new Vector3(found.Value.x, 0f, found.Value.y), Yaw = yaw, Scale = 1f, Palette = PaletteTallGrass,
                };
                layout.Add(placement);
                scout.Add(placement, prefab);
                lines.Add($"{name} ({found.Value.x:F2}, {found.Value.y:F2}) {names.Length} clumps, {HullWidth(hull):F2} m wide, hides to {score * 100f:F0} %");
                made++;
            }
            DeleteStalePatches(chunkName, made);

            lines.Add($"{watch.Elapsed.TotalSeconds:F1} s");
            Report(chunkName, lines);
            return layout;
        }

        private static void Report(string chunkName, List<string> lines)
        {
            string line = $"{chunkName}: " + string.Join(", ", lines);
            HideDecorReport.RemoveAll(l => l.StartsWith(chunkName + ":", StringComparison.Ordinal));
            HideDecorReport.Add(line);
            Debug.Log("[GrasslandPipeline] T39 " + line);
        }

        private static float HullWidth(Vector2[] hull) => hull.Length == 0 ? 0f : hull.Max(p => p.x) - hull.Min(p => p.x);

        // ---------------------------------------------------------------- patches

        /// <summary>
        /// One patch: its two back clumps at <see cref="PatchClumpScale"/> side by side (local +z, away from the camera), the
        /// third (the low one, <see cref="PatchFrontScale"/>) in front between them, each turned by a fixed seed, their ground outlines
        /// 6 cm apart (never intersecting). Merged in patch space (pivot on the ground at the patch's middle), with every
        /// clump's colors and PW_Occlusion uv (y the height weight SH_FoliageWind sways). Returns the ground outline.
        /// </summary>
        private static Mesh BuildPatchMesh(Mesh[] sources, int seed, out Vector2[] hull, out float height)
        {
            var rng = new System.Random(seed);
            var yaws = sources.Select(_ => (float)(rng.NextDouble() * 360.0)).ToArray();
            var outlines = new Vector2[sources.Length][];
            var scales = sources.Select((_, c) => c < 2 ? PatchClumpScale : PatchFrontScale).ToArray();
            for (int c = 0; c < sources.Length; c++)
                outlines[c] = ClumpOutline(sources[c], yaws[c], scales[c]);
            var offsets = new Vector2[sources.Length];
            if (sources.Length > 1)
            {
                // Back right: slide along +x until 6 cm from the back left.
                for (float dx = 0.1f; dx < 2f; dx += 0.01f)
                {
                    offsets[1] = new Vector2(dx, 0f);
                    if (Gap(outlines[0], offsets[0], outlines[1], offsets[1]) >= PatchGap)
                        break;
                }
            }
            // Any further clump (a lower one, in front): it slides toward the camera until 6 cm from every clump already placed.
            for (int c = 2; c < sources.Length; c++)
            {
                float x = sources.Length > 3 ? offsets[1].x * (c == 2 ? 0.2f : 0.8f) : offsets[1].x * 0.5f;
                for (float dz = 0.05f; dz < 2f; dz += 0.01f)
                {
                    offsets[c] = new Vector2(x, -dz);
                    bool apart = true;
                    for (int o = 0; o < c && apart; o++)
                        apart = Gap(outlines[o], offsets[o], outlines[c], offsets[c]) >= PatchGap;
                    if (apart)
                        break;
                }
            }
            var all = new List<Vector2>();
            for (int c = 0; c < sources.Length; c++)
                all.AddRange(outlines[c].Select(p => p + offsets[c]));
            var middle = Vector2.zero;
            var full = CollisionProxies.ConvexHull(all);
            foreach (var p in full)
                middle += p;
            middle /= Mathf.Max(1, full.Length);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            height = 0f;
            for (int c = 0; c < sources.Length; c++)
            {
                var mesh = sources[c];
                var rotation = Quaternion.Euler(0f, yaws[c], 0f);
                var shift = new Vector3(offsets[c].x - middle.x, 0f, offsets[c].y - middle.y);
                int start = vertices.Count;
                var v = mesh.vertices;
                var n = mesh.normals;
                var col = mesh.colors;
                var uv = mesh.uv;
                for (int i = 0; i < v.Length; i++)
                {
                    var p = rotation * Vector3.Scale(v[i] * scales[c], new Vector3(1f, PatchHeightStretch, 1f)) + shift;
                    vertices.Add(p);
                    height = Mathf.Max(height, p.y);
                    normals.Add(n.Length == v.Length ? rotation * new Vector3(n[i].x, n[i].y / PatchHeightStretch, n[i].z).normalized : Vector3.up);
                    colors.Add(col.Length == v.Length ? col[i] : Color.clear);
                    uvs.Add(uv.Length == v.Length ? uv[i] : Vector2.zero);
                }
                foreach (int t in mesh.triangles)
                    triangles.Add(start + t);
            }
            hull = full.Select(p => p - middle).ToArray();
            var merged = new Mesh { name = "GL_TallGrassPatch" };
            merged.SetVertices(vertices);
            merged.SetNormals(normals);
            merged.SetColors(colors);
            merged.SetUVs(0, uvs);
            merged.SetTriangles(triangles, 0);
            merged.RecalculateBounds();
            return merged;
        }

        /// <summary>A clump's outline on the ground (its vertices up to 0.7 m, scaled and turned), convex.</summary>
        private static Vector2[] ClumpOutline(Mesh mesh, float yaw, float scale)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var points = mesh.vertices.Select(v => rotation * Vector3.Scale(v * scale, new Vector3(1f, PatchHeightStretch, 1f))).Where(p => p.y < 0.7f)
                .Select(p => new Vector2(p.x, p.z)).ToList();
            return CollisionProxies.ConvexHull(points);
        }

        private static float Gap(Vector2[] a, Vector2 oa, Vector2[] b, Vector2 ob)
        {
            var ha = a.Select(p => p + oa).ToArray();
            var hb = b.Select(p => p + ob).ToArray();
            if (CollisionProxies.HullOverlap(ha, hb, out _, out _))
                return -1f;
            float best = float.MaxValue;
            foreach (var p in ha)
                best = Mathf.Min(best, CollisionProxies.HullDistance(hb, p));
            foreach (var p in hb)
                best = Mathf.Min(best, CollisionProxies.HullDistance(ha, p));
            return best;
        }

        /// <summary>
        /// The patch's mesh (Baked/Patches, Read/Write off) and its shape-only prefab: a mesh filter and no renderer. The patch
        /// is placed, settled and measured (obstacles, scatter keep-out) like any prop; its look is drawn by the chunk's one
        /// merged tall grass renderer (<see cref="ComposeHideDecorT39"/>).
        /// </summary>
        private static GameObject SavePatch(string name, Mesh source)
        {
            var mesh = SaveBakedMesh(PatchFolder, name, source);
            return SaveShape(mesh);
        }

        private static Mesh SaveBakedMesh(string folder, string name, Mesh source)
        {
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.asset";
            source.name = name;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                AssetDatabase.CreateAsset(source, path);
                mesh = source;
            }
            else
            {
                EditorUtility.CopySerialized(source, mesh);
                mesh.name = name;
                Object.DestroyImmediate(source);
            }
            MakeRuntimeOnly(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            Resources.UnloadAsset(mesh);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>PF_&lt;mesh&gt;: a mesh filter only (no renderer, no LOD): a hide decor's shape in the chunk layout.</summary>
        private static GameObject SaveShape(Mesh mesh)
        {
            var go = new GameObject("PF_" + mesh.name);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                return PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsFolder}/PF_{mesh.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Stale patches and merged meshes waiting for their chunk to be saved without them.</summary>
        private static readonly HashSet<string> StaleHideDecor = new HashSet<string>();

        /// <summary>
        /// Patches of an earlier build this chunk no longer has (fewer places found): their prefab and mesh go once the chunk
        /// is saved without them (deleting a prefab the old chunk still nests makes its reimport fail).
        /// </summary>
        private static void DeleteStalePatches(string chunkName, int kept)
        {
            for (int i = kept; i < 12; i++)
            {
                string name = $"GL_TallGrassPatch_{chunkName}_{i}";
                foreach (var path in new[] { $"{PrefabsFolder}/PF_{name}.prefab", $"{PatchFolder}/{name}.asset" })
                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
                        QueueStale(path);
            }
        }

        private static void QueueStale(string path)
        {
            StaleHideDecor.Add(path);
            EditorApplication.delayCall -= DeleteUnusedStaleHideDecor;
            EditorApplication.delayCall += DeleteUnusedStaleHideDecor;
        }

        /// <summary>After the build: the stale assets no chunk prefab uses any more are deleted.</summary>
        private static void DeleteUnusedStaleHideDecor()
        {
            var used = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { StripChunkFolder }))
                used.UnionWith(AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid), true));
            foreach (var path in StaleHideDecor.OrderBy(p => p.EndsWith(".prefab", StringComparison.Ordinal) ? 0 : 1))
                if (!used.Contains(path))
                    AssetDatabase.DeleteAsset(path);
            StaleHideDecor.Clear();
        }

        // ---------------------------------------------------------------- merged look

        public const string MergedFolder = BakedFolder + "/HideDecor";

        /// <summary>
        /// The chunk's T39 decor drawn as merged props (the chunk's merged-props convention, renderer budget of the start
        /// chunk): every tall grass patch in ONE mesh on MAT_Foliage_Grassland (uv1 = each vertex's own patch base, so each
        /// patch still sways from its own root; FoliageGusts is positional, the hint still ripples there), every flowered bush
        /// in one mesh per palette row on MAT_Environment_Common with each bush's brightness in uv.y (vertex brightness, shadows
        /// on). Called by the chunk builders after the props are composed, from the settled layout.
        /// </summary>
        private static void ComposeHideDecorT39(string chunkName, Transform root, List<Placement> layout, Dictionary<string, GameObject> prefabs,
            List<WorldChunk.PaletteBinding> bindings)
        {
            var decor = layout.Where(p => (IsTallGrass(p.Prop) || IsFlowerBush(p.Prop)) && prefabs.ContainsKey(p.Prop)).ToList();
            var keep = new HashSet<string>();
            if (decor.Count > 0)
            {
                var group = Child(root.Find("Props") ?? Child(root, "Props"), "HideDecor");
                var grass = decor.Where(p => IsTallGrass(p.Prop)).ToList();
                if (grass.Count > 0)
                {
                    var foliage = AssetDatabase.LoadAssetAtPath<Material>(FoliageMaterialPath) ?? EnsureFoliageMaterial();
                    var mesh = Merge(grass, prefabs, true, null);
                    string name = $"GL_HideDecor_{chunkName}_TallGrass";
                    keep.Add(name);
                    var renderer = AddRenderer(Child(group, "TallGrass").gameObject, SaveBakedMesh(MergedFolder, name, mesh), foliage, ShadowCastingMode.Off);
                    bindings.Add(new WorldChunk.PaletteBinding
                    {
                        renderer = renderer, paletteId = ContentId.FromSource(PaletteTallGrass).Value, brightness = WorldChunk.NeutralBrightness,
                    });
                }
                var env = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentMaterialPath)
                          ?? EnsureMaterial(EnvironmentMaterialPath, EnvironmentShader, "MAT_Environment_Common");
                foreach (var row in decor.Where(p => IsFlowerBush(p.Prop)).GroupBy(p => p.Palette).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    var rng = new System.Random(chunkName.Length * 977 + row.Key.Length);
                    var mesh = Merge(row.ToList(), prefabs, false, rng);
                    string tail = row.Key.Substring(row.Key.LastIndexOf('.') + 1);
                    string name = $"GL_HideDecor_{chunkName}_{tail}";
                    keep.Add(name);
                    var renderer = AddRenderer(Child(group, tail).gameObject, SaveBakedMesh(MergedFolder, name, mesh), env, ShadowCastingMode.On);
                    bindings.Add(new WorldChunk.PaletteBinding
                    {
                        renderer = renderer, paletteId = ContentId.FromSource(row.Key).Value, brightness = WorldChunk.NeutralBrightness,
                        vertexBrightness = true,
                    });
                }
            }
            // Merged meshes of an earlier build this chunk no longer has (deleted once the chunk is saved without them).
            if (!AssetDatabase.IsValidFolder(MergedFolder))
                return;
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MergedFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (file.StartsWith($"GL_HideDecor_{chunkName}_", StringComparison.Ordinal) && !keep.Contains(file))
                    QueueStale(path);
            }
        }

        /// <summary>
        /// The placements' shape meshes in chunk space, one mesh. Foliage: uv1 = (the piece's base, 1) for SH_FoliageWind;
        /// environment: uv.y = the piece's brightness (0 to 1 for levels 0 to 15, like SH_EnvironmentOpaque's merged props).
        /// </summary>
        private static Mesh Merge(List<Placement> pieces, Dictionary<string, GameObject> prefabs, bool foliage, System.Random rng)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var bases = new List<Vector4>();
            var triangles = new List<int>();
            foreach (var piece in pieces)
            {
                var filter = prefabs[piece.Prop].GetComponentInChildren<MeshFilter>(true);
                var source = filter != null ? filter.sharedMesh : null;
                if (source == null)
                    continue;
                var local = prefabs[piece.Prop].transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var m = Matrix4x4.TRS(piece.Position, Quaternion.Euler(0f, piece.Yaw, 0f), Vector3.one * piece.Scale) * local;
                int start = vertices.Count;
                var v = source.vertices;
                var n = source.normals;
                var c = source.colors;
                var uv = source.uv;
                float level = foliage ? 0f : (5 + rng.Next(7)) / 15f;
                for (int i = 0; i < v.Length; i++)
                {
                    vertices.Add(m.MultiplyPoint3x4(v[i]));
                    normals.Add(n.Length == v.Length ? m.MultiplyVector(n[i]).normalized : Vector3.up);
                    colors.Add(c.Length == v.Length ? c[i] : Color.clear);
                    var t = uv.Length == v.Length ? uv[i] : Vector2.zero;
                    uvs.Add(foliage ? t : new Vector2(t.x, level));
                    bases.Add(new Vector4(piece.Position.x, piece.Position.y, piece.Position.z, 1f));
                }
                foreach (int t in source.triangles)
                    triangles.Add(start + t);
            }
            var mesh = new Mesh { indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            if (foliage)
                mesh.SetUVs(1, bases);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- the scout

        /// <summary>
        /// Chooses where a piece of hiding decor goes in a chunk being built: candidate places in the zone camera's frame
        /// (its middle and far part, where the ground is seen at the lowest angles), free ground near a prop or a meadow edge,
        /// off the path, the water, the egg and the walk area's heart; for each, the visible share of a rabbit and a squirrel
        /// (the tallest, in their hiding pose) standing just behind it on each side, measured from the zone camera with the
        /// layout's own triangles (HideSpotPipeline's meter). The best place hides both on both sides.
        /// </summary>
        private sealed class HideDecorScout
        {
            private readonly string _chunk;
            private readonly Dictionary<string, GameObject> _prefabs;
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();
            private readonly List<Obstacle> _obstacles = new List<Obstacle>();
            private readonly List<CollisionProxies.Placed> _placed;
            private readonly List<(Vector2 center, float reach)> _hiders = new List<(Vector2, float)>();
            private readonly GroundSampler _ground;
            private readonly GroundSampler _water;
            private readonly Func<Vector3, float> _worn;
            private readonly GroundHeightField _field;
            private readonly Vector3 _egg;
            private readonly bool _start;
            private readonly bool _strip;
            private readonly bool _westEnd;
            private readonly bool _eastEnd;
            private readonly List<HideSpotPipeline.SpeciesModel> _all;
            private List<Vector3> _groundVertices = new List<Vector3>();
            private List<int> _groundTriangles = new List<int>();
            private readonly List<HideSpotPipeline.SpeciesModel> _species;
            private readonly float _standReach;
            private readonly float _standHeight;
            private readonly Vector3 _camera;
            private readonly Quaternion _rotation;
            public readonly bool Ready;
            public string LastSearch = "";

            public HideDecorScout(string chunk, List<Placement> layout, Dictionary<string, GameObject> prefabs, Mesh groundMesh, Mesh waterMesh,
                bool strip, List<string> lines)
            {
                _chunk = chunk;
                _prefabs = prefabs;
                _ground = new GroundSampler(groundMesh);
                _water = waterMesh != null ? new GroundSampler(waterMesh) : null;
                _worn = WornMask(groundMesh);
                _field = BuildGroundField(groundMesh, GroundFieldCell);
                _start = chunk == "Start";
                _westEnd = chunk == "Start";
                _eastEnd = chunk == "ForestEdge";
                _strip = strip;
                _egg = _start ? ClearingCenter(groundMesh) : new Vector3(0f, 0f, -100f);
                string address = HideZoneFocus.First(z => z.chunk == chunk).address;
                var focus = HideZoneFocusFor(address) ?? Vector2.zero;
                var view = HideZoneViewFor(address);
                _rotation = Quaternion.Euler(view.elevation, view.yaw, 0f);
                var target = new Vector3(focus.x, _field.Sample(focus.x, focus.y), focus.y);
                _camera = target - _rotation * Vector3.forward * HideSpotPipeline.NominalDistance;
                var all = HideSpotPipeline.LoadSpecies(new StringBuilder());
                _all = all;
                _species = all.Where(s => s.Name != "hedgehog").ToList();
                if (_species.Count == 0)
                {
                    lines.Add("no species to measure with");
                    return;
                }
                _standReach = HideSpotPipeline.StandReach(all);
                _standHeight = all.Max(s => s.Body.height);

                // The layout as it will stand (strip chunks: props on the ground under their footprint).
                var placed = new List<Placement>();
                foreach (var p in layout)
                {
                    var q = p;
                    if (strip && !q.Prop.StartsWith("GL_Edge", StringComparison.Ordinal))
                        q.Position = new Vector3(q.Position.x, GroundUnder(_ground, q.Position.x, q.Position.z, FootprintRadius(q)), q.Position.z);
                    placed.Add(q);
                }
                _placed = PlaceLayout(placed, prefabs);
                foreach (var volume in _placed)
                    _obstacles.AddRange(volume.Bands.Select(b => b.Capsule));
                _obstacles.AddRange(WaterObstacles(waterMesh));
                _obstacles.AddRange(SteepObstacles(_field));
                if (_start)
                {
                    var egg = new Vector2(_egg.x, _egg.z);
                    _obstacles.Add(new Obstacle(egg, egg, RefugeController.EggSpotRadius, 0f, -0.5f, 1f, ObstacleKind.Solid));
                }
                foreach (var p in placed)
                    AddTriangles(prefabs[p.Prop], p.Position, p.Yaw, p.Scale);
                int start = _vertices.Count;
                _vertices.AddRange(groundMesh.vertices);
                _triangles.AddRange(groundMesh.triangles.Select(t => start + t));
                _groundVertices = groundMesh.vertices.ToList();
                _groundTriangles = groundMesh.triangles.ToList();
                Ready = true;
            }

            private void AddTriangles(GameObject prefab, Vector3 position, float yaw, float scale)
            {
                var renderers = new List<Renderer>();
                var lod = prefab.GetComponent<LODGroup>();
                if (lod != null && lod.lodCount > 0)
                    renderers.AddRange(lod.GetLODs()[0].renderers.Where(r => r != null));
                else
                    renderers.AddRange(prefab.GetComponentsInChildren<MeshRenderer>(true));
                var placement = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
                var filters = renderers.Select(r => r.GetComponent<MeshFilter>()).ToList();
                if (filters.Count == 0)
                    filters.AddRange(prefab.GetComponentsInChildren<MeshFilter>(true));
                foreach (var filter in filters)
                {
                    if (filter == null || filter.sharedMesh == null)
                        continue;
                    var m = placement * prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    int start = _vertices.Count;
                    foreach (var v in filter.sharedMesh.vertices)
                        _vertices.Add(m.MultiplyPoint3x4(v));
                    foreach (int t in filter.sharedMesh.triangles)
                        _triangles.Add(start + t);
                }
            }

            /// <summary>The layout without the candidate (rebuilt after each decor added): whether a prop stands before the candidate.</summary>
            private HideSpotPipeline.ShareMeter _layoutMeter;
            private int _dShown, _dUnplaced, _dOthers, _dLanding, _dPeek, _dFits, _dOwn, _dIsland, _dBacked;

            /// <summary>A placed decor joins the layout: its volume, its triangles, its room for spots.</summary>
            public void Add(Placement placement, GameObject prefab)
            {
                _layoutMeter = null;
                var volume = CollisionProxies.Place(CollisionProxies.Measure(prefab), placement.Position, placement.Yaw, placement.Scale);
                _placed.Add(volume);
                _obstacles.AddRange(volume.Bands.Select(b => b.Capsule));
                AddTriangles(prefab, placement.Position, placement.Yaw, placement.Scale);
                var ground = volume.Ground ?? volume.Bands[0];
                var c = new Vector2(placement.Position.x, placement.Position.z);
                _hiders.Add((c, ground.Hull.Max(p => (p - c).magnitude)));
            }

            private float Tangent(Vector3 world)
            {
                var d = Quaternion.Inverse(_rotation) * (world - _camera);
                return d.z > 0.5f ? d.y / d.z : float.MaxValue;
            }

            private bool InFrame(Vector3 world, float bottom)
            {
                var d = Quaternion.Inverse(_rotation) * (world - _camera);
                if (d.z <= 0.5f)
                    return false;
                float tan = Mathf.Tan(HideSpotPipeline.SelectFov * 0.5f * Mathf.Deg2Rad);
                float y = d.y / d.z / tan;
                return Mathf.Abs(d.x / d.z) <= 0.86f * tan * HideSpotPipeline.NarrowAspect && y <= 0.64f && y >= bottom;
            }

            private bool OnFreeGround(Vector2 p, float reach)
            {
                for (int k = 0; k < 9; k++)
                {
                    var o = k == 0 ? Vector2.zero : new Vector2(Mathf.Cos(k * Mathf.PI / 4f), Mathf.Sin(k * Mathf.PI / 4f)) * reach;
                    var q = new Vector3(p.x + o.x, 0f, p.y + o.y);
                    if (_worn(q) > 0.15f || (_water != null && _water.TrySample(q.x, q.z, out _, out _)))
                        return false;
                    if (!_ground.TrySample(q.x, q.z, out float h, out var normal) || normal.y < 0.85f || h < -0.3f)
                        return false;
                }
                return true;
            }

            /// <summary>
            /// The strip's ground at least <paramref name="margin"/> inside its outer edge (chunk space; the back and front
            /// edges, the start chunk's rounded west end, the forest edge's rounded east end), as the spot pipeline reads it.
            /// </summary>
            private bool OnIsland(Vector2 p, float margin)
            {
                if (Mathf.Abs(p.y) > 7f - margin)
                    return false;
                if ((_westEnd && p.x < 0f) || (_eastEnd && p.x > 0f))
                    return InsideIsland(new Vector3(p.x, 0f, p.y), margin);
                return true;
            }

            /// <summary>Outside the zone camera's widest frame (3:4 at the widest field of view, with a margin) or behind it.</summary>
            private bool OutOfView(Vector3 world)
            {
                var d = Quaternion.Inverse(_rotation) * (world - _camera);
                return d.z <= 0.5f || Mathf.Abs(d.x / d.z) > Mathf.Tan(HideSpotPipeline.MaxFov * 0.5f * Mathf.Deg2Rad) * HideSpotPipeline.WideAspect * 1.15f;
            }

            /// <summary>The ray from the zone camera through this point meets the ground inside the island (no void behind it).</summary>
            private bool BackedByIsland(Vector3 point)
            {
                var dir = point - _camera;
                if (dir.y >= -1e-3f)
                    return false;
                var hit = _camera + dir * (-_camera.y / dir.y);
                return OnIsland(new Vector2(hit.x, hit.z), HideSpotPipeline.VoidMargin);
            }

            /// <summary>Where a hidden animal may stand: on the ground, off the path and the egg's place, clear of every volume.</summary>
            private bool Fits(HideSpotPipeline.SpeciesModel s, Vector2 p, float yaw)
            {
                if (Mathf.Abs(p.x) > 6.2f || Mathf.Abs(p.y) > 6.9f || p.magnitude < 1.5f || Vector2.Distance(p, new Vector2(_egg.x, _egg.z)) < 1.3f + s.Reach)
                    return false;
                // Away from the strip's outer edge, and the island behind it as the camera sees it.
                if (!OnIsland(p, HideSpotPipeline.EdgeClearance))
                {
                    _dIsland++;
                    return false;
                }
                if (!BackedByIsland(new Vector3(p.x, _field.Sample(p.x, p.y) + s.Height, p.y)))
                {
                    _dBacked++;
                    return false;
                }
                if (_worn(new Vector3(p.x, 0f, p.y)) > 0.3f || !_ground.TrySample(p.x, p.y, out float h, out _) || h < -0.3f)
                    return false;
                var position = new Vector3(p.x, _field.Sample(p.x, p.y), p.y);
                s.HideBody.Segment(position, yaw, HideSpotPipeline.ClearanceScale, out var a, out var b);
                float radius = s.HideBody.halfWidth * HideSpotPipeline.ClearanceScale;
                foreach (var o in _obstacles)
                    if (o.BlocksWalker(position.y, s.HideHeight) && CollisionMath.Gap(a, b, radius, o) < HideSpotPipeline.BodyGap)
                        return false;
                return InFrame(position, -0.76f);
            }

            /// <summary>
            /// Every species, slid sideways from its hidden place along the decor, shows at least 30 % (the peek-out level)
            /// somewhere a little past the decor's end, on free ground.
            /// </summary>
            private bool PeeksOut(HideSpotPipeline.ShareMeter meter, Vector2[] hiddenPlaces, Vector2 center, float edge, Vector2 slide, float yaw,
                List<Obstacle> own)
            {
                for (int k = 0; k < _species.Count; k++)
                {
                    var s = _species[k];
                    float along = Vector2.Dot(hiddenPlaces[k] - center, slide);
                    bool seen = false;
                    foreach (float past in new[] { 0.25f, 0.45f, 0.7f, 1.0f })
                    {
                        var q = hiddenPlaces[k] + slide * Mathf.Max(0f, edge + past - along);
                        if (!Fits(s, q, yaw))
                            continue;
                        var position = new Vector3(q.x, _field.Sample(q.x, q.y), q.y);
                        s.HideBody.Segment(position, yaw, HideSpotPipeline.ClearanceScale, out var a, out var b);
                        if (own.Any(o => o.BlocksWalker(position.y, s.HideHeight)
                                         && CollisionMath.Gap(a, b, s.HideBody.halfWidth * HideSpotPipeline.ClearanceScale, o) < HideSpotPipeline.BodyGap))
                            continue;
                        if (meter.Measure(s, position, yaw, out _) >= HideSpotSet.ShareAt(HideSpotSet.ShareLevels - 1))
                        {
                            seen = true;
                            break;
                        }
                    }
                    if (!seen)
                        return false;
                }
                return true;
            }

            /// <summary>
            /// Room for a found animal's hop out on one side (as the spot pipeline's landing): free ground in front of the decor,
            /// toward that side, on this chunk and in the frame, clear of every volume for the largest body in any heading.
            /// </summary>
            private bool LandingRoom(HideSpotPipeline.ShareMeter meter, Vector2 center, float groundReach, Vector2 behind, Vector2 slide,
                List<Obstacle> own, Vector2[] hiddenPlaces)
            {
                var front = -behind;
                float yawToCamera = Mathf.Atan2(front.x, front.y) * Mathf.Rad2Deg;
                foreach (float angle in new[] { 25f, 40f, 10f, 55f, 70f, 85f, 100f, 115f })
                {
                    float rad = angle * Mathf.Deg2Rad;
                    var dir = front * Mathf.Cos(rad) + slide * Mathf.Sin(rad);
                    foreach (float extra in new[] { 0.05f, 0.2f, 0.35f, 0.55f })
                    {
                        var q = center + dir * (groundReach + _standReach + HideSpotPipeline.BodyGap + extra);
                        if (Mathf.Abs(q.x) > 6.6f - _standReach || Mathf.Abs(q.y) > 6.95f - _standReach || !OnIsland(q, HideSpotPipeline.EdgeClearance))
                            continue;
                        // A clear hop away from where it hid.
                        if (hiddenPlaces.Any(h => (h - q).magnitude < HideSpotPipeline.LandingDistance))
                            continue;
                        if (Vector2.Distance(q, new Vector2(_egg.x, _egg.z)) < 1.3f + _standReach)
                            continue;
                        if ((_water != null && _water.TrySample(q.x, q.y, out _, out _)) || !_ground.TrySample(q.x, q.y, out float h, out _) || h < -0.3f)
                            continue;
                        var position = new Vector3(q.x, _field.Sample(q.x, q.y), q.y);
                        if (!InFrame(position, -0.68f) || !InFrame(position + Vector3.up * 0.5f, -0.68f) || !BackedByIsland(position + Vector3.up * _standHeight))
                            continue;
                        bool free = true;
                        foreach (var o in _obstacles.Concat(own))
                        {
                            if (o.BlocksWalker(position.y, _standHeight) && CollisionMath.Gap(q, q, _standReach, o) < HideSpotPipeline.BodyGap)
                            {
                                free = false;
                                break;
                            }
                        }
                        // Clearly seen once out.
                        if (free && _all.All(s => meter.Measure(s, position, yawToCamera, out _) >= HideSpotPipeline.LandingVisible))
                            return true;
                    }
                }
                return false;
            }

            /// <summary>The best place for a prop prefab at a scale (a patch turned to the camera, a flowered bush).</summary>
            public Vector2? BestProp(GameObject prefab, float scale, bool faceCamera, out float yaw, out float score, bool any = false)
            {
                var proxy = CollisionProxies.Measure(prefab);
                var band = proxy.Ground ?? proxy.Bands.FirstOrDefault();
                float reach = band != null ? band.Hull.Max(v => v.magnitude) * scale : 0.5f;
                var filter = prefab.GetComponentInChildren<MeshFilter>(true);
                var vertices = filter.sharedMesh.vertices;
                var triangles = filter.sharedMesh.triangles;
                var local = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                return Search(reach, (p, y) => CollisionProxies.Place(proxy, p, y, scale), (p, y) =>
                {
                    var m = Matrix4x4.TRS(p, Quaternion.Euler(0f, y, 0f), Vector3.one * scale) * local;
                    return (vertices.Select(v => m.MultiplyPoint3x4(v)).ToList(), triangles);
                }, faceCamera, any, out yaw, out score);
            }

            private Vector2? Search(float reach, Func<Vector3, float, CollisionProxies.Placed> place,
                Func<Vector3, float, (List<Vector3> vertices, int[] triangles)> mesh, bool faceCamera, bool any, out float bestYaw, out float bestScore)
            {
                bestYaw = 0f;
                bestScore = float.MaxValue;
                Vector2? best = null;
                const float Step = 0.3f;
                int cFrame = 0, cApart = 0, cGround = 0, cFree = 0, cMeasured = 0, cNoRoom = 0;
                Vector2? fallback = null;
                float fallbackYaw = 0f, fallbackNear = float.MaxValue;
                _dShown = _dUnplaced = _dOthers = _dLanding = _dPeek = _dFits = _dOwn = _dIsland = _dBacked = 0;
                var scores = new List<float>();
                for (float x = -6.0f; x <= 6.01f; x += Step)
                {
                    for (float z = -6.3f; z <= 6.31f; z += Step)
                    {
                        var p = new Vector2(x, z);
                        if (Mathf.Abs(x) > 6.2f - reach || Mathf.Abs(z) > 6.4f - reach)
                            continue;
                        if (p.magnitude < HideDecorHeartClearance + reach || Vector2.Distance(p, new Vector2(_egg.x, _egg.z)) < HideDecorEggClearance + reach)
                            continue;
                        if (_start && x < 0f && !InsideIsland(new Vector3(x, 0f, z), 0.3f + reach))
                            continue;
                        var position = new Vector3(x, _strip ? GroundUnder(_ground, x, z, reach * 0.9f) : 0f, z);
                        // The middle and far part of the frame: the ground is seen there at the lowest angles. The theme's
                        // patches with no hiding place there stand out of the zone camera's view (no decor of the game behind
                        // them), beside a prop.
                        bool outOfView = OutOfView(position);
                        if (!InFrame(position + Vector3.up * 0.3f, -0.4f) && !(any && outOfView))
                        {
                            cFrame++;
                            continue;
                        }
                        // Apart from the other hiding decor of this build: their spots must not meet.
                        if (_hiders.Any(h => (h.center - p).magnitude < h.reach + reach + HideDecorApart))
                        {
                            cApart++;
                            continue;
                        }
                        if (!OnFreeGround(p, reach))
                        {
                            cGround++;
                            continue;
                        }
                        var behind = (p - new Vector2(_camera.x, _camera.z)).normalized;
                        float yaw = faceCamera ? Mathf.Atan2(behind.x, behind.y) * Mathf.Rad2Deg : (x * 37f + z * 53f) % 360f;
                        var volume = place(position, yaw);
                        bool free = true;
                        float nearest = float.MaxValue;
                        foreach (var other in _placed)
                        {
                            if ((CollisionMath.Flat(other.Position) - p).sqrMagnitude > 49f)
                                continue;
                            if (CollisionProxies.Overlap(other, volume, out _) > 0f || GroundGap(other, volume) < HideDecorGap)
                            {
                                free = false;
                                break;
                            }
                            nearest = Mathf.Min(nearest, GroundGap(other, volume));
                        }
                        if (!free)
                        {
                            cFree++;
                            continue;
                        }
                        if (outOfView)
                        {
                            if (nearest < fallbackNear)
                            {
                                fallbackNear = nearest;
                                fallback = p;
                                fallbackYaw = yaw;
                            }
                            continue;
                        }
                        float score = Measure(p, behind, reach, volume, mesh(position, yaw));
                        cMeasured++;
                        if (score >= float.MaxValue)
                            cNoRoom++;
                        else
                            scores.Add(score);
                        // Natural groups: beside a prop (a tree's foot, a rock, a bush, a fence) reads better than alone.
                        if (nearest < 1.0f)
                            score -= 0.03f;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = p;
                            bestYaw = yaw;
                        }
                    }
                }
                scores.Sort();
                LastSearch = $"[frame {cFrame}, apart {cApart}, ground {cGround}, overlap {cFree}, measured {cMeasured}, no room {cNoRoom} (shown {_dShown}, unplaced {_dUnplaced}: fits {_dFits} (island {_dIsland}, backed {_dBacked}), own {_dOwn}; others {_dOthers}, landing {_dLanding}, peek {_dPeek}), best "
                             + string.Join("/", scores.Take(5).Select(v => (v * 100f).ToString("F0"))) + "]";
                if (best == null && fallback != null)
                {
                    best = fallback;
                    bestYaw = fallbackYaw;
                    bestScore = 1f;
                }
                if (best == null || (!any && bestScore > HideSpotPipeline.MaxHiddenShare))
                    return null;
                bestScore = Mathf.Max(0f, bestScore);
                return best;
            }

            /// <summary>
            /// The worst visible share of the measured species standing just behind the decor, one on each side (sideways,
            /// as a hidden animal stands); with room on one side only the place costs a little more: the best give two spots.
            /// </summary>
            private float Measure(Vector2 p, Vector2 behind, float reach, CollisionProxies.Placed volume, (List<Vector3> vertices, int[] triangles) decor)
            {
                var vertices = new List<Vector3>(_vertices);
                int start = vertices.Count;
                vertices.AddRange(decor.vertices);
                var triangles = new List<int>(_triangles);
                triangles.AddRange(decor.triangles.Select(t => start + t));
                var meter = new HideSpotPipeline.ShareMeter(_camera, _rotation, vertices, triangles);
                // The decor itself shows past every prop already there (the player sees what to tap).
                _layoutMeter ??= new HideSpotPipeline.ShareMeter(_camera, _rotation, _vertices, _triangles);
                var anchor = new Vector3(p.x, 0f, p.y);
                var silhouette = new[] { decor.vertices.Select(v => v - anchor).ToArray() };
                var model = new HideSpotPipeline.SpeciesModel { Name = "decor", Frames = silhouette, SearchFrames = silhouette, Triangles = decor.triangles };
                if (_layoutMeter.Measure(model, anchor, 0f, out _) < HideSpotPipeline.DecorVisible)
                {
                    _dShown++;
                    return float.MaxValue;
                }
                // The ground and this decor alone: other props must not be what hides the animal.
                var alone = new List<Vector3>(_groundVertices);
                int aloneStart = alone.Count;
                alone.AddRange(decor.vertices);
                var aloneTriangles = new List<int>(_groundTriangles);
                aloneTriangles.AddRange(decor.triangles.Select(t => aloneStart + t));
                var own = new HideSpotPipeline.ShareMeter(_camera, _rotation, alone, aloneTriangles);
                var obstacles = volume.Bands.Select(b => b.Capsule).ToList();
                var hull = (volume.Ground ?? volume.Bands[0]).Hull;
                var worsts = new List<float>();
                for (int side = 0; side < 2; side++)
                {
                    var slide = side == 0 ? new Vector2(behind.y, -behind.x) : new Vector2(-behind.y, behind.x);
                    float worst = float.MaxValue, bestYaw = 0f;
                    var bestHidden = new Vector2[_species.Count];
                    var hidden = new Vector2[_species.Count];
                    // Sideways or turned toward the camera (as the spot pipeline tries), a little toward the side: the two
                    // spots of a patch stand at its two ends.
                    foreach (float facing in new[] { 0f, 80f })
                    foreach (float lateral in new[] { 0.3f, 0.1f, 0.5f })
                    {
                        var f = slide * Mathf.Cos(facing * Mathf.Deg2Rad) - behind * Mathf.Sin(facing * Mathf.Deg2Rad);
                        float yaw = Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg;
                        float sideWorst = 0f;
                        bool fits = true;
                        for (int k = 0; k < _species.Count; k++)
                        {
                            var s = _species[k];
                            // Right behind the decor: its outline's depth along the view, the body's half width, a gap.
                            var origin = p + slide * lateral;
                            float depth = hull.Max(h => Vector2.Dot(h - origin, behind));
                            bool placed = false;
                            for (float extra = 0.04f; extra <= 0.3f && !placed; extra += 0.06f)
                            {
                                var q = origin + behind * (depth + s.HideBody.halfWidth * HideSpotPipeline.ClearanceScale + extra);
                                if (!Fits(s, q, yaw))
                                {
                                    _dFits++;
                                    continue;
                                }
                                var position = new Vector3(q.x, _field.Sample(q.x, q.y), q.y);
                                s.HideBody.Segment(position, yaw, HideSpotPipeline.ClearanceScale, out var a, out var b);
                                // Only the decor's bands at the animal's height (a tall patch's blades spread above it).
                                if (obstacles.Any(o => o.BlocksWalker(position.y, s.HideHeight)
                                                       && CollisionMath.Gap(a, b, s.HideBody.halfWidth * HideSpotPipeline.ClearanceScale, o) < HideSpotPipeline.BodyGap))
                                {
                                    _dOwn++;
                                    continue;
                                }
                                float share = meter.Measure(s, position, yaw, out _);
                                if (own.Measure(s, position, yaw, out _) - share > 0.05f)
                                {
                                    _dOthers++;
                                    continue;
                                }
                                sideWorst = Mathf.Max(sideWorst, share);
                                hidden[k] = q;
                                placed = true;
                            }
                            if (!placed)
                            {
                                _dUnplaced++;
                                fits = false;
                                break;
                            }
                        }
                        if (fits && sideWorst < worst)
                        {
                            worst = sideWorst;
                            bestYaw = yaw;
                            Array.Copy(hidden, bestHidden, hidden.Length);
                        }
                    }
                    // A spot also needs its landing (the found animal hops out in front, toward its side) and room to peek out:
                    // a little past the decor's end every species shows its 30 % (never under a crown that hides it anyway).
                    float edge = hull.Max(h => Vector2.Dot(h - p, slide));
                    if (worst < float.MaxValue && !LandingRoom(meter, p, hull.Max(h => (h - p).magnitude), behind, slide, obstacles, bestHidden))
                        _dLanding++;
                    else if (worst < float.MaxValue && !PeeksOut(meter, bestHidden, p, edge, slide, bestYaw, obstacles))
                        _dPeek++;
                    if (worst < float.MaxValue && LandingRoom(meter, p, hull.Max(h => (h - p).magnitude), behind, slide, obstacles, bestHidden)
                        && PeeksOut(meter, bestHidden, p, edge, slide, bestYaw, obstacles))
                        worsts.Add(worst);
                }
                // Two spots (one on each side) are what a patch is for: one side only costs a little more.
                return worsts.Count == 2 ? Mathf.Max(worsts[0], worsts[1]) : worsts.Count == 1 ? worsts[0] + 0.1f : float.MaxValue;
            }
        }

        // ---------------------------------------------------------------- palettes and prefabs

        /// <summary>Smallest distance between two props' ground outlines (0 when they touch or overlap).</summary>
        private static float GroundGap(CollisionProxies.Placed a, CollisionProxies.Placed b)
        {
            var ha = a.Ground?.Hull;
            var hb = b.Ground?.Hull;
            if (ha == null || hb == null)
                return float.MaxValue;
            float best = float.MaxValue;
            foreach (var v in ha)
                best = Mathf.Min(best, CollisionProxies.HullDistance(hb, v));
            foreach (var v in hb)
                best = Mathf.Min(best, CollisionProxies.HullDistance(ha, v));
            return Mathf.Max(0f, best);
        }

        /// <summary>HB's rows (pw_grassland_t39_props.T39_PALETTES), added once to DEF_Palettes_Grassland.</summary>
        private static void EnsureHideDecorPalettes()
        {
            var set = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(PalettesPath);
            if (set == null)
                return;
            set.palettes.RemoveAll(e => e.sourceId == PaletteTallGrass || e.sourceId == PaletteFlowerBushPink
                                        || e.sourceId == PaletteFlowerBushCream || e.sourceId == PaletteFlowerBushYellow);
            // Tall grass: the meadow grass with its clovers (B pink heads, A white heads); bushes: GL_Bush foliage, base the
            // daisy hearts, A the petals. Accent FFFFFF on the bushes (SH_EnvironmentOpaque has no accent column).
            Add(set, PaletteTallGrass, "66A253", "9FCE78", "D0CB9A", "F3C4D6", "F8F4EA", "476F3E", "B9D56C");
            Add(set, PaletteFlowerBushPink, "F2C94C", "73B25A", "A2D27E", "4F8A46", "F3BDD3", "355E33", "FFFFFF");
            Add(set, PaletteFlowerBushCream, "F2C94C", "73B25A", "A2D27E", "4F8A46", "F8F1DD", "355E33", "FFFFFF");
            Add(set, PaletteFlowerBushYellow, "F29C4C", "73B25A", "A2D27E", "4F8A46", "F7DF7A", "355E33", "FFFFFF");
            EditorUtility.SetDirty(set);
        }

        /// <summary>
        /// PF_GL_TallGrass_A/B/C on MAT_Foliage_Grassland (the single clumps; the chunks use patches made of them) and the
        /// shape-only PF_GL_FlowerBush_A/B (their look is merged per chunk and palette row, MAT_Environment_Common, shadows
        /// on, like GL_Bush). Standalone kit meshes (never the FBX in a bundle). Returns the models not exported yet.
        /// </summary>
        private static List<string> EnsureHideDecorPrefabs(Dictionary<string, GameObject> prefabs)
        {
            var missing = new List<string>();
            var meshes = new Dictionary<string, Mesh>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Mesh>())
                    if (Array.IndexOf(HideDecorProps, mesh.name) >= 0)
                        meshes[mesh.name] = mesh;
            var foliage = AssetDatabase.LoadAssetAtPath<Material>(FoliageMaterialPath) ?? EnsureFoliageMaterial();
            // The tufts' material takes its palette row from the renderer (ScatterRenderer sets it per draw instead).
            if (foliage.HasProperty("_PaletteRow") && !Mathf.Approximately(foliage.GetFloat("_PaletteRow"), -1f))
            {
                foliage.SetFloat("_PaletteRow", -1f);
                EditorUtility.SetDirty(foliage);
            }
            Directory.CreateDirectory(PrefabsFolder);
            foreach (var name in HideDecorProps)
            {
                if (!meshes.TryGetValue(name, out var source))
                {
                    missing.Add(name);
                    continue;
                }
                string path = $"{PrefabsFolder}/PF_{name}.prefab";
                bool grass = IsTallGrass(name);
                // Several chunks ask in one build: the kit mesh is refreshed in place (same asset), the prefab saved again only
                // when it does not hold it yet. Flowered bushes are shape-only (drawn by the chunk's merged bush renderers);
                // the single clumps keep a renderer (the patches are made from their meshes).
                var kit = KitMesh(source);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (grass)
                {
                    if (!UpToDate(prefab, kit, foliage, ShadowCastingMode.Off))
                        SaveSingle(kit, foliage, ShadowCastingMode.Off, 0.008f);
                }
                else if (!ShapeUpToDate(prefab, kit))
                {
                    SaveShape(kit);
                }
                prefabs[name] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return missing;
        }

        /// <summary>The shape-only prefab already holds this kit mesh (and no renderer).</summary>
        private static bool ShapeUpToDate(GameObject prefab, Mesh kit)
        {
            var filter = prefab != null ? prefab.GetComponent<MeshFilter>() : null;
            return filter != null && filter.sharedMesh == kit && prefab.GetComponentInChildren<Renderer>(true) == null;
        }

        /// <summary>The prefab already draws this kit mesh with this material and these shadows.</summary>
        private static bool UpToDate(GameObject prefab, Mesh kit, Material material, ShadowCastingMode shadows)
        {
            var filter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>(true) : null;
            var renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            return renderer != null && filter.sharedMesh == kit && renderer.sharedMaterial == material && renderer.shadowCastingMode == shadows
                   && prefab.GetComponent<LODGroup>() != null;
        }
    }
}
