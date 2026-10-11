using System.Collections.Generic;
using System.Linq;
using PocketWilds.Editor.Environment;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Editor.Cosmetics
{
    /// <summary>
    /// T47 Cozy Burrow (decor.cozy_burrow): a free spot of the arrival chunk (PF_Chunk_Grassland_Start), chosen and validated
    /// here, saved in DEF_CosmeticVisualCatalog. Its measured volume (CollisionProxies height bands) keeps a margin from every
    /// obstacle of the chunk (props, water, steep ground), stays inside the walk area, off the worn path, off flat-breaking
    /// ground, out of the egg apron and away from the animal spawn points; the entrance (local +Z) opens on a free corridor.
    /// Among the valid spots: about 3 m from the egg, entrance turned toward it. At runtime the refuge spawns it there and adds
    /// the same volume to the walk obstacles. Idempotent.
    /// </summary>
    public static class CozyBurrowPlacementPipeline
    {
        private const float Margin = 0.06f;
        private const float Step = 0.25f;
        private const float EntranceRun = 1.1f;
        private const float EntranceHalfWidth = 0.3f;
        private const float MaxRise = 0.06f;
        private const float AreaInset = 0.35f;
        private const float WornLimit = 0.3f;
        private const float PreferredEggDistance = 3.2f;

        [MenuItem("Pocket Wilds/Cosmetics/Place Cozy Burrow (T47)", priority = 194)]
        public static void BuildAllMenu() => Debug.Log("[CozyBurrowPlacementPipeline] " + BuildAll());

        public static string BuildAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticVisualCatalog>(AccessoryVisualPipeline.CatalogPath);
            if (catalog == null || catalog.burrowPrefab == null)
                return "run AccessoryVisualPipeline.BuildAll first";
            var root = PrefabUtility.LoadPrefabContents(GrasslandPipeline.ChunkPath);
            try
            {
                var chunk = root.GetComponentInChildren<WorldChunk>(true);
                if (chunk == null || chunk.GroundField == null)
                    return "no WorldChunk with ground in " + GrasslandPipeline.ChunkPath;
                var toChunk = chunk.transform.worldToLocalMatrix;
                var obstacles = Enumerable.Range(0, chunk.ObstacleCount).Select(chunk.GetObstacle).ToArray();
                var field = chunk.GroundField;
                var area = chunk.LocalWanderArea;
                var egg = chunk.EggSpawn != null ? Flat(toChunk.MultiplyPoint3x4(chunk.EggSpawn.position)) : Vector2.zero;
                var spawns = chunk.AnimalSpawns.Where(s => s != null).Select(s => Flat(toChunk.MultiplyPoint3x4(s.position))).ToArray();
                var worn = WornPoints(root, toChunk);
                CollisionProxies.ClearCache();
                var proxy = CollisionProxies.Measure(catalog.burrowPrefab);
                if (proxy.Bands.Count == 0)
                    return "PF_Decor_CozyBurrow has no measurable volume";

                float extent = area.Radius + Mathf.Max(area.HalfSize.x, area.HalfSize.y) + 1f;
                CollisionProxies.Placed best = null;
                float bestCost = float.MaxValue, bestYaw = 0f;
                int tested = 0, valid = 0;
                for (float x = area.Center.x - extent; x <= area.Center.x + extent; x += Step)
                {
                    for (float z = area.Center.z - extent; z <= area.Center.z + extent; z += Step)
                    {
                        if (!area.Contains(new Vector3(x, area.Center.y, z)))
                            continue;
                        for (int k = 0; k < 16; k++)
                        {
                            float yaw = k * 22.5f;
                            tested++;
                            var placed = CollisionProxies.Place(proxy, new Vector3(x, field.Sample(x, z), z), yaw, 1f);
                            var forward = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                            if (!IsFree(placed, forward, obstacles, field, area, egg, spawns, worn))
                                continue;
                            valid++;
                            var center = new Vector2(x, z);
                            var toEgg = egg - center;
                            float cost = Mathf.Abs(toEgg.magnitude - PreferredEggDistance)
                                         + (1f - Vector2.Dot(forward, toEgg.normalized)) * 0.8f;
                            if (cost >= bestCost)
                                continue;
                            bestCost = cost;
                            best = placed;
                            bestYaw = yaw;
                        }
                    }
                }
                if (best == null)
                {
                    catalog.burrowPlaced = false;
                    EditorUtility.SetDirty(catalog);
                    AssetDatabase.SaveAssets();
                    return $"no free spot ({tested} tested): the burrow is not placed";
                }
                var bestForward = new Vector2(Mathf.Sin(bestYaw * Mathf.Deg2Rad), Mathf.Cos(bestYaw * Mathf.Deg2Rad));
                float reach = best.Bands.Max(b => b.Capsule.Reach);
                var c2 = new Vector2(best.Position.x, best.Position.z);
                var rest = c2 + bestForward * (reach + 0.3f);
                catalog.burrowPlaced = true;
                catalog.burrowPosition = best.Position;
                catalog.burrowYaw = bestYaw;
                catalog.burrowRadius = reach;
                catalog.burrowObstacles = best.Bands.Select(b => b.Capsule).ToArray();
                catalog.burrowRestSpot = new Vector3(rest.x, field.Sample(rest.x, rest.y), rest.y);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                return $"placed at ({best.Position.x:0.00}, {best.Position.z:0.00}) yaw {bestYaw:0}, {(egg - c2).magnitude:0.0} m from the egg, " +
                       $"{catalog.burrowObstacles.Length} bands, {valid}/{tested} valid";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        private static bool IsFree(CollisionProxies.Placed placed, Vector2 forward, Obstacle[] obstacles, GroundHeightField field, WanderArea area,
            Vector2 egg, Vector2[] spawns, List<(Vector2 p, float worn)> worn)
        {
            var center = new Vector2(placed.Position.x, placed.Position.z);
            float lowest = float.MaxValue, highest = float.MinValue;
            foreach (var band in placed.Bands)
            {
                var capsule = band.Capsule;
                // Inside the walk area with room around it, on even ground, off the worn path.
                foreach (var h in band.Hull)
                {
                    var outward = (h - center).sqrMagnitude > 1e-6f ? (h - center).normalized : Vector2.right;
                    var probe = h + outward * AreaInset;
                    if (!area.Contains(new Vector3(probe.x, area.Center.y, probe.y)))
                        return false;
                    float y = field.Sample(h.x, h.y);
                    lowest = Mathf.Min(lowest, y);
                    highest = Mathf.Max(highest, y);
                    if (Worn(worn, h) > WornLimit)
                        return false;
                }
                // Never into a prop, the water or steep ground.
                foreach (var o in obstacles)
                {
                    bool heights = o.kind != ObstacleKind.Solid || (capsule.WorldBottom < o.WorldTop && capsule.WorldTop > o.WorldBottom);
                    if (heights && SegmentDistance(capsule.a, capsule.b, o.a, o.b) < capsule.radius + o.radius + Margin)
                        return false;
                }
                // Out of the egg apron and away from the spawn points.
                if (PointSegment(egg, capsule.a, capsule.b) < RefugeController.EggApronRadius + capsule.radius + Margin)
                    return false;
                foreach (var s in spawns)
                    if (PointSegment(s, capsule.a, capsule.b) < GrasslandPipeline.SpawnClearance + capsule.radius + Margin)
                        return false;
            }
            if (highest - lowest > MaxRise || Worn(worn, center) > WornLimit)
                return false;
            // The entrance opens on free ground: a corridor in front of it, inside the area, clear of every obstacle.
            float reach = placed.Bands.Max(b => b.Capsule.Reach);
            var start = center + forward * (reach + 0.05f);
            var end = center + forward * (reach + EntranceRun);
            if (!area.Contains(new Vector3(end.x, area.Center.y, end.y)))
                return false;
            foreach (var o in obstacles)
                if (SegmentDistance(start, end, o.a, o.b) < EntranceHalfWidth + o.radius)
                    return false;
            return true;
        }

        /// <summary>Worn ground (vertex color B of the chunk's ground meshes), chunk space.</summary>
        private static List<(Vector2 p, float worn)> WornPoints(GameObject root, Matrix4x4 toChunk)
        {
            var result = new List<(Vector2, float)>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !filter.name.Contains("Ground"))
                    continue;
                var colors = mesh.colors32;
                if (colors == null || colors.Length != mesh.vertexCount)
                    continue;
                var matrix = toChunk * filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    result.Add((Flat(matrix.MultiplyPoint3x4(vertices[i])), colors[i].b / 255f));
            }
            return result;
        }

        private static float Worn(List<(Vector2 p, float worn)> worn, Vector2 at)
        {
            float best = float.MaxValue, value = 0f;
            foreach (var (p, w) in worn)
            {
                float d = (p - at).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    value = w;
                }
            }
            return value;
        }

        private static float PointSegment(Vector2 p, Vector2 a, Vector2 b) => (p - CollisionMath.ClosestOnSegment(p, a, b)).magnitude;

        /// <summary>Plan distance between two segments.</summary>
        private static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (Intersects(a, b, c, d))
                return 0f;
            return Mathf.Min(Mathf.Min(PointSegment(a, c, d), PointSegment(b, c, d)), Mathf.Min(PointSegment(c, a, b), PointSegment(d, a, b)));
        }

        private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float Cross(Vector2 o, Vector2 p, Vector2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);
            float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }
    }
}
