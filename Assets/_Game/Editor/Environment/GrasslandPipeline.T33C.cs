using System;
using System.Collections.Generic;
using System.Linq;
using PocketWilds.Presentation;
using UnityEngine;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T33C no-collision rule (Docs/Design/Collision_Rule.md), chunk side: props are settled until no two volumes
    /// overlap (neighbour chunks included), every chunk stores its obstacles (props per height band, water) and its
    /// ground heights for the animals and the ambient life, and scatter never grows inside a prop.
    /// </summary>
    public static partial class GrasslandPipeline
    {
        /// <summary>Ground height grid step (m). The refuge's relief is a few centimeters: 0.25 m keeps feet on it.</summary>
        public const float GroundFieldCell = 0.25f;

        /// <summary>Overlap allowed between two props (m): touching is fine, entering is not.</summary>
        public const float PropContactTolerance = 0.005f;

        // Room left between two props pushed apart.
        private const float SettleGap = 0.02f;
        // A layout needing more than this to settle is wrong by design: fix it by hand.
        private const float MaxSettleMove = 0.9f;
        private const float WaterRowSpacing = 0.25f;
        private const float WaterScanStep = 0.05f;

        /// <summary>Props of the chunks built so far, world space: the next chunk settles against them at its seams.</summary>
        private static readonly Dictionary<string, List<(string prop, Vector3 position, float yaw, float scale)>> BuiltProps =
            new Dictionary<string, List<(string prop, Vector3 position, float yaw, float scale)>>();

        private static List<CollisionProxies.Placed> PlaceLayout(IEnumerable<Placement> layout, Dictionary<string, GameObject> prefabs, Vector3 offset = default) =>
            layout.Select(p => CollisionProxies.Place(CollisionProxies.Measure(prefabs[p.Prop]), p.Position + offset, p.Yaw, p.Scale)).ToList();

        /// <summary>Who stays put when two props overlap: landmarks, then rim pieces, trees, bushes, the rest.</summary>
        private static int SettlePriority(string prop) =>
            prop.StartsWith("GL_Landmark", StringComparison.Ordinal) ? 5
            : prop.StartsWith("GL_Edge", StringComparison.Ordinal) || prop == "GL_StreamStones" ? 4
            : prop.StartsWith("GL_Tree", StringComparison.Ordinal) ? 3
            : prop.StartsWith("GL_Bush", StringComparison.Ordinal) || prop.StartsWith("GL_RockHero", StringComparison.Ordinal) ? 2
            : 1;

        /// <summary>
        /// Pushes overlapping props apart (the lesser one moves, along the shortest way out) until no volume enters
        /// another, this chunk's and the neighbours' at the seams. Returns one line per moved prop.
        /// </summary>
        private static List<string> SettleProps(string chunkName, Vector3 origin, List<Placement> layout, Dictionary<string, GameObject> prefabs,
            Func<float, float, float> groundY = null)
        {
            var neighbours = new List<CollisionProxies.Placed>();
            foreach (var entry in BuiltProps)
            {
                if (entry.Key == chunkName)
                    continue;
                foreach (var p in entry.Value)
                {
                    if (Mathf.Abs(p.position.x - origin.x) > 10f)
                        continue;
                    neighbours.Add(CollisionProxies.Place(CollisionProxies.Measure(prefabs[p.prop]), p.position - origin, p.yaw, p.scale));
                }
            }

            var start = layout.Select(p => p.Position).ToList();
            void Move(int index, Vector2 delta)
            {
                var p = layout[index];
                float x = p.Position.x + delta.x, z = p.Position.z + delta.y;
                // Rim pieces hang at the island's edge height; everything else follows the ground it moves onto.
                float y = groundY != null && !p.Prop.StartsWith("GL_Edge", StringComparison.Ordinal) ? groundY(x, z) : p.Position.y;
                p.Position = new Vector3(x, y, z);
                layout[index] = p;
            }

            for (int pass = 0; pass < 80; pass++)
            {
                var placed = PlaceLayout(layout, prefabs);
                bool any = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    for (int j = i + 1; j < placed.Count; j++)
                    {
                        float depth = CollisionProxies.Overlap(placed[i], placed[j], out var push);
                        if (depth <= PropContactTolerance)
                            continue;
                        any = true;
                        int pi = SettlePriority(layout[i].Prop), pj = SettlePriority(layout[j].Prop);
                        float ai = GroundArea(placed[i]), aj = GroundArea(placed[j]);
                        bool moveJ = pj < pi || (pj == pi && aj <= ai);
                        int mover = moveJ ? j : i;
                        Move(mover, (moveJ ? push : -push) * (depth + SettleGap));
                        placed[mover] = CollisionProxies.Place(CollisionProxies.Measure(prefabs[layout[mover].Prop]), layout[mover].Position,
                            layout[mover].Yaw, layout[mover].Scale);
                    }
                    foreach (var other in neighbours)
                    {
                        float depth = CollisionProxies.Overlap(other, placed[i], out var push);
                        if (depth <= PropContactTolerance)
                            continue;
                        any = true;
                        Move(i, push * (depth + SettleGap));
                        placed[i] = CollisionProxies.Place(CollisionProxies.Measure(prefabs[layout[i].Prop]), layout[i].Position, layout[i].Yaw,
                            layout[i].Scale);
                    }
                }
                if (!any)
                    break;
            }

            var moves = new List<string>();
            for (int i = 0; i < layout.Count; i++)
            {
                var delta = layout[i].Position - start[i];
                delta.y = 0f;
                if (delta.magnitude < 0.005f)
                    continue;
                if (delta.magnitude > MaxSettleMove)
                    throw new InvalidOperationException($"{chunkName}: {layout[i].Prop} needs {delta.magnitude:F2} m to stop overlapping, fix the layout.");
                moves.Add($"{layout[i].Prop} {delta.magnitude:F2} m");
            }
            BuiltProps[chunkName] = layout.Select(p => (p.Prop, p.Position + origin, p.Yaw, p.Scale)).ToList();
            return moves;
        }

        private static string SettleSummary(List<string> moves) => moves.Count == 0 ? "none moved" : "settled: " + string.Join(", ", moves);

        private static float GroundArea(CollisionProxies.Placed placed)
        {
            var band = placed.Ground ?? placed.Bands.FirstOrDefault();
            return band != null ? CollisionProxies.HullArea(band.Hull) : 0f;
        }

        /// <summary>The chunk's obstacles (props' bands, water) and ground heights, chunk space.</summary>
        private static (Obstacle[] obstacles, GroundHeightField ground) BuildCollision(List<Placement> layout, Dictionary<string, GameObject> prefabs,
            Mesh groundMesh, Mesh waterMesh)
        {
            var obstacles = new List<Obstacle>();
            foreach (var placed in PlaceLayout(layout, prefabs))
                obstacles.AddRange(placed.Bands.Select(b => b.Capsule));
            obstacles.AddRange(WaterObstacles(waterMesh));
            var field = BuildGroundField(groundMesh, GroundFieldCell);
            obstacles.AddRange(SteepObstacles(field));
            return (obstacles.ToArray(), field);
        }

        /// <summary>Room kept around animal spawn points: a squirrel (tail included) fits whatever its heading.</summary>
        public const float SpawnClearance = 0.45f;

        /// <summary>Moves spawn points out of every obstacle (props, water, steep ground) and back onto the ground.</summary>
        private static void ClearSpawns(IEnumerable<Transform> spawns, Obstacle[] obstacles, GroundHeightField field)
        {
            foreach (var spawn in spawns)
            {
                if (spawn == null)
                    continue;
                var p = new Vector2(spawn.localPosition.x, spawn.localPosition.z);
                for (int pass = 0; pass < 20; pass++)
                {
                    bool moved = false;
                    foreach (var o in obstacles)
                    {
                        if (!o.BlocksWalker(field.Sample(p.x, p.y), 0.7f))
                            continue;
                        var closest = CollisionMath.ClosestOnSegment(p, o.a, o.b);
                        var d = p - closest;
                        float min = o.radius + SpawnClearance + 0.01f;
                        if (d.sqrMagnitude >= min * min)
                            continue;
                        var n = d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.right;
                        p = closest + n * min;
                        moved = true;
                    }
                    if (!moved)
                        break;
                }
                spawn.localPosition = new Vector3(p.x, field.Sample(p.x, p.y), p.y);
            }
        }

        /// <summary>Steepest ground an animal or a ground bird walks on (degrees): banks and hill flanks beyond are off limits.</summary>
        public const float MaxWalkSlope = 32f;

        /// <summary>Ground steeper than <see cref="MaxWalkSlope"/>, as runs of capsules along the grid rows.</summary>
        private static List<Obstacle> SteepObstacles(GroundHeightField field)
        {
            var result = new List<Obstacle>();
            float limit = Mathf.Tan(MaxWalkSlope * Mathf.Deg2Rad);
            float radius = field.cell * 0.75f;
            for (int z = 1; z < field.countZ - 1; z++)
            {
                int runStart = -1;
                float top = float.MinValue;
                for (int x = 1; x <= field.countX - 1; x++)
                {
                    bool steep = false;
                    if (x < field.countX - 1)
                    {
                        int i = z * field.countX + x;
                        float dx = (field.heights[i + 1] - field.heights[i - 1]) / (2f * field.cell);
                        float dz = (field.heights[i + field.countX] - field.heights[i - field.countX]) / (2f * field.cell);
                        steep = Mathf.Sqrt(dx * dx + dz * dz) > limit;
                        if (steep)
                            top = Mathf.Max(top, field.heights[i]);
                    }
                    if (steep)
                    {
                        if (runStart < 0)
                            runStart = x;
                        continue;
                    }
                    if (runStart < 0)
                        continue;
                    float wz = field.origin.y + z * field.cell;
                    var a = new Vector2(field.origin.x + runStart * field.cell, wz);
                    var b = new Vector2(field.origin.x + (x - 1) * field.cell, wz);
                    result.Add(new Obstacle(a, b, radius, top, -2f, 0.05f, ObstacleKind.Steep));
                    runStart = -1;
                    top = float.MinValue;
                }
            }
            return result;
        }

        /// <summary>The ground mesh's top surface on a grid over the chunk (holes take their nearest neighbour).</summary>
        private static GroundHeightField BuildGroundField(Mesh groundMesh, float cell)
        {
            const float Half = 7.5f;
            var sampler = new GroundSampler(groundMesh);
            int count = Mathf.RoundToInt(2f * Half / cell) + 1;
            var heights = new float[count * count];
            var known = new bool[heights.Length];
            for (int z = 0; z < count; z++)
            {
                for (int x = 0; x < count; x++)
                {
                    int i = z * count + x;
                    known[i] = sampler.TrySample(-Half + x * cell, -Half + z * cell, out heights[i], out _);
                }
            }
            for (int pass = 0; pass < count && known.Any(k => !k); pass++)
            {
                var next = (bool[])known.Clone();
                for (int z = 0; z < count; z++)
                {
                    for (int x = 0; x < count; x++)
                    {
                        int i = z * count + x;
                        if (known[i])
                            continue;
                        float sum = 0f;
                        int n = 0;
                        if (x > 0 && known[i - 1]) { sum += heights[i - 1]; n++; }
                        if (x < count - 1 && known[i + 1]) { sum += heights[i + 1]; n++; }
                        if (z > 0 && known[i - count]) { sum += heights[i - count]; n++; }
                        if (z < count - 1 && known[i + count]) { sum += heights[i + count]; n++; }
                        if (n == 0)
                            continue;
                        heights[i] = sum / n;
                        next[i] = true;
                    }
                }
                known = next;
            }
            return new GroundHeightField { origin = new Vector2(-Half, -Half), cell = cell, countX = count, countZ = count, heights = heights };
        }

        /// <summary>
        /// The water surface as capsules: runs of water along rows (or columns, whichever needs fewer), each a capsule
        /// wide enough to cover the row gap. Animals and ground birds never step in; fliers pass over.
        /// </summary>
        private static List<Obstacle> WaterObstacles(Mesh waterMesh)
        {
            if (waterMesh == null)
                return new List<Obstacle>();
            var sampler = new GroundSampler(waterMesh);
            List<Obstacle> Runs(bool alongX)
            {
                var result = new List<Obstacle>();
                float radius = WaterRowSpacing * 0.75f;
                for (float w = -7.5f; w <= 7.5f + 1e-4f; w += WaterRowSpacing)
                {
                    float runStart = float.NaN, last = 0f, surface = float.MinValue;
                    for (float u = -7.5f; u <= 7.5f + WaterScanStep; u += WaterScanStep)
                    {
                        float h = 0f;
                        bool inside = u <= 7.5f && sampler.TrySample(alongX ? u : w, alongX ? w : u, out h, out _);
                        if (inside)
                        {
                            if (float.IsNaN(runStart))
                                runStart = u;
                            last = u;
                            surface = Mathf.Max(surface, h);
                            continue;
                        }
                        if (float.IsNaN(runStart))
                            continue;
                        var a = alongX ? new Vector2(runStart, w) : new Vector2(w, runStart);
                        var b = alongX ? new Vector2(last, w) : new Vector2(w, last);
                        result.Add(new Obstacle(a, b, radius, surface, -2f, 0.03f, ObstacleKind.Water));
                        runStart = float.NaN;
                        surface = float.MinValue;
                    }
                }
                return result;
            }
            var rows = Runs(true);
            var columns = Runs(false);
            return rows.Count <= columns.Count ? rows : columns;
        }

        // ---------------- reports (validation, tests) ----------------

        /// <summary>One placed prop of the built Grassland, world space (for the validator and the tests).</summary>
        public sealed class PropVolume
        {
            public string Chunk;
            public string Prop;
            public CollisionProxies.Placed Placed;
        }

        /// <summary>The props of every built Grassland chunk prefab, measured again from their instances (world space).</summary>
        public static List<PropVolume> BuiltPropVolumes()
        {
            var result = new List<PropVolume>();
            foreach (var (address, path, origin, _) in World.WorldContentPipeline.GrasslandChunks)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;
                var props = prefab.transform.Find("Props");
                if (props == null)
                    continue;
                foreach (Transform group in props)
                {
                    foreach (Transform instance in group)
                    {
                        var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                        if (source == null)
                            continue;
                        var proxy = CollisionProxies.Measure(source);
                        result.Add(new PropVolume
                        {
                            Chunk = prefab.name,
                            Prop = source.name,
                            Placed = CollisionProxies.Place(proxy, origin + instance.localPosition, instance.localEulerAngles.y, instance.localScale.x),
                        });
                    }
                }
            }
            return result;
        }

        /// <summary>Every pair of built props whose volumes enter each other (seams included), deepest first.</summary>
        public static List<(PropVolume a, PropVolume b, float depth)> PropOverlaps(List<PropVolume> volumes = null)
        {
            volumes ??= BuiltPropVolumes();
            var result = new List<(PropVolume, PropVolume, float)>();
            for (int i = 0; i < volumes.Count; i++)
            {
                for (int j = i + 1; j < volumes.Count; j++)
                {
                    if ((volumes[i].Placed.Position - volumes[j].Placed.Position).sqrMagnitude > 64f)
                        continue;
                    float depth = CollisionProxies.Overlap(volumes[i].Placed, volumes[j].Placed, out _);
                    if (depth > PropContactTolerance)
                        result.Add((volumes[i], volumes[j], depth));
                }
            }
            return result.OrderByDescending(r => r.Item3).ToList();
        }

        /// <summary>Overlaps of the layouts as written (before settling), for the T33C report.</summary>
        public static string LayoutOverlapReport()
        {
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                prefabs[prefab.name.Substring(3)] = prefab;
            }
            var layouts = new List<(string name, Vector3 origin, List<Placement> layout)>
            {
                ("Start", Vector3.zero, StartLayoutT33()),
                ("Meadow", MeadowOffset, MeadowLayoutT33()),
            };
            layouts.AddRange(StripChunks.Select(c => (c.Name, new Vector3(c.OriginX, 0f, 0f), c.Layout())));
            var volumes = new List<PropVolume>();
            foreach (var (name, origin, layout) in layouts)
            {
                foreach (var p in layout)
                {
                    volumes.Add(new PropVolume
                    {
                        Chunk = name, Prop = p.Prop,
                        Placed = CollisionProxies.Place(CollisionProxies.Measure(prefabs[p.Prop]), p.Position + origin, p.Yaw, p.Scale),
                    });
                }
            }
            var overlaps = PropOverlaps(volumes);
            return $"{overlaps.Count} overlapping pairs: " + string.Join("; ",
                overlaps.Select(o => $"{o.a.Chunk}/{o.a.Prop}@{o.a.Placed.Position.x:F1},{o.a.Placed.Position.z:F1} x {o.b.Chunk}/{o.b.Prop} {o.depth:F2}"));
        }
    }
}
