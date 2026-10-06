using System;
using System.Collections.Generic;
using System.Linq;
using PocketWilds.Presentation;
using UnityEngine;

namespace PocketWilds.Editor.Environment
{
    /// <summary>
    /// T33C no-collision rule (Docs/Design/Collision_Rule.md): the solid volume of a prop measured from its own
    /// meshes. The prop's LOD0 surfaces are sampled every few centimeters and cut into height bands; each band keeps
    /// its convex outline seen from above (exact enough to test props against props and scatter against props) and
    /// the capsule that encloses it (cheap enough for animals and birds at runtime, see <see cref="Obstacle"/>).
    /// Turning parts (windmill sails) are swept around their axis first. No collider, no physics.
    /// </summary>
    public static class CollisionProxies
    {
        /// <summary>Band limits above the prop's base (m): ground level (animals, scatter), trunks, crowns, landmarks.</summary>
        public static readonly float[] BandLimits = { 0f, 0.7f, 1.5f, 3f, 6f, 12f };

        private const float SampleSpacing = 0.04f;
        private const int RotorSteps = 24;

        /// <summary>One height band of a prop, in the prefab's space (scale 1, yaw 0).</summary>
        public sealed class Band
        {
            public float Bottom;
            public float Top;
            public Vector2[] Hull;
            public Vector2 A;
            public Vector2 B;
            public float Radius;
        }

        public sealed class Proxy
        {
            public string Name;
            public List<Band> Bands = new List<Band>();
            public Band Ground => Bands.Count > 0 && Bands[0].Bottom < BandLimits[1] ? Bands[0] : null;
        }

        /// <summary>A band placed in a chunk: outline, enclosing capsule and heights (world y).</summary>
        public sealed class PlacedBand
        {
            public float Bottom;
            public float Top;
            public Vector2[] Hull;
            public Obstacle Capsule;
        }

        public sealed class Placed
        {
            public string Name;
            public Vector3 Position;
            public List<PlacedBand> Bands = new List<PlacedBand>();
            public PlacedBand Ground => Bands.Count > 0 && Bands[0].Bottom - Position.y < BandLimits[1] ? Bands[0] : null;
        }

        private static readonly Dictionary<string, Proxy> Cache = new Dictionary<string, Proxy>();

        public static void ClearCache() => Cache.Clear();

        /// <summary>Measures (once per prefab) the prop's bands from its LOD0 renderers.</summary>
        public static Proxy Measure(GameObject prefab)
        {
            string key = prefab.name + "#" + prefab.GetInstanceID();
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            var root = prefab.transform;
            var renderers = new List<Renderer>();
            var lodGroup = prefab.GetComponent<LODGroup>();
            if (lodGroup != null && lodGroup.lodCount > 0)
                renderers.AddRange(lodGroup.GetLODs()[0].renderers.Where(r => r != null));
            else
                renderers.AddRange(prefab.GetComponentsInChildren<MeshRenderer>(true));

            var buckets = new List<Vector3>[BandLimits.Length - 1];
            for (int i = 0; i < buckets.Length; i++)
                buckets[i] = new List<Vector3>();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                var toPrefab = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                bool rotor = renderer.name.Contains("_Sails_");
                foreach (var p in SampleSurface(filter.sharedMesh))
                {
                    if (!rotor)
                    {
                        Add(buckets, toPrefab.MultiplyPoint3x4(p));
                        continue;
                    }
                    // SH_EnvironmentOpaque turns the sails around their local Z axis: keep every angle.
                    for (int k = 0; k < RotorSteps; k++)
                    {
                        float angle = k * Mathf.PI * 2f / RotorSteps;
                        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                        Add(buckets, toPrefab.MultiplyPoint3x4(new Vector3(p.x * c - p.y * s, p.x * s + p.y * c, p.z)));
                    }
                }
            }

            var proxy = new Proxy { Name = prefab.name };
            for (int i = 0; i < buckets.Length; i++)
            {
                var points = buckets[i];
                if (points.Count < 3)
                    continue;
                var hull = ConvexHull(points.Select(p => new Vector2(p.x, p.z)).ToList());
                if (hull.Length < 3)
                    continue;
                FitCapsule(hull, out var a, out var b, out float radius);
                proxy.Bands.Add(new Band
                {
                    Bottom = Mathf.Max(points.Min(p => p.y), BandLimits[i]),
                    Top = Mathf.Min(points.Max(p => p.y), BandLimits[i + 1]),
                    Hull = hull,
                    A = a,
                    B = b,
                    Radius = radius,
                });
            }
            Cache[key] = proxy;
            return proxy;
        }

        /// <summary>The proxy at a chunk placement (yaw around Y, uniform scale, base at position.y).</summary>
        public static Placed Place(Proxy proxy, Vector3 position, float yaw, float scale)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var offset = new Vector2(position.x, position.z);
            Vector2 Map(Vector2 p)
            {
                var v = rotation * new Vector3(p.x, 0f, p.y) * scale;
                return new Vector2(v.x, v.z) + offset;
            }
            var placed = new Placed { Name = proxy.Name, Position = position };
            foreach (var band in proxy.Bands)
            {
                placed.Bands.Add(new PlacedBand
                {
                    Bottom = position.y + band.Bottom * scale,
                    Top = position.y + band.Top * scale,
                    Hull = band.Hull.Select(Map).ToArray(),
                    Capsule = new Obstacle(Map(band.A), Map(band.B), band.Radius * scale, position.y, band.Bottom * scale, band.Top * scale,
                        ObstacleKind.Solid),
                });
            }
            return placed;
        }

        /// <summary>
        /// The deepest overlap between two placed props over every pair of bands sharing heights (0 when apart), and
        /// the direction that would push <paramref name="b"/> out of <paramref name="a"/> by that depth.
        /// </summary>
        public static float Overlap(Placed a, Placed b, out Vector2 push)
        {
            float worst = 0f;
            push = Vector2.zero;
            foreach (var ba in a.Bands)
            {
                foreach (var bb in b.Bands)
                {
                    if (ba.Bottom >= bb.Top || bb.Bottom >= ba.Top)
                        continue;
                    if (HullOverlap(ba.Hull, bb.Hull, out float depth, out var axis) && depth > worst)
                    {
                        worst = depth;
                        push = axis;
                    }
                }
            }
            return worst;
        }

        /// <summary>Separating-axis test of two convex outlines. depth: smallest push along axis (pointing from a to b).</summary>
        public static bool HullOverlap(Vector2[] a, Vector2[] b, out float depth, out Vector2 axis)
        {
            depth = float.MaxValue;
            axis = Vector2.zero;
            var ca = Centroid(a);
            var cb = Centroid(b);
            for (int pass = 0; pass < 2; pass++)
            {
                var hull = pass == 0 ? a : b;
                for (int i = 0; i < hull.Length; i++)
                {
                    var edge = hull[(i + 1) % hull.Length] - hull[i];
                    var n = new Vector2(-edge.y, edge.x);
                    if (n.sqrMagnitude < 1e-12f)
                        continue;
                    n.Normalize();
                    Project(a, n, out float minA, out float maxA);
                    Project(b, n, out float minB, out float maxB);
                    float overlap = Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB);
                    if (overlap <= 0f)
                    {
                        depth = 0f;
                        return false;
                    }
                    if (overlap < depth)
                    {
                        depth = overlap;
                        axis = Vector2.Dot(cb - ca, n) >= 0f ? n : -n;
                    }
                }
            }
            return true;
        }

        /// <summary>Signed distance from a point to a convex outline (negative inside).</summary>
        public static float HullDistance(Vector2[] hull, Vector2 p)
        {
            bool inside = true;
            float best = float.MaxValue;
            for (int i = 0; i < hull.Length; i++)
            {
                var a = hull[i];
                var b = hull[(i + 1) % hull.Length];
                var edge = b - a;
                if (edge.x * (p.y - a.y) - edge.y * (p.x - a.x) < 0f)
                    inside = false;
                best = Mathf.Min(best, Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(p, a, b)));
            }
            return inside ? -best : best;
        }

        public static float HullArea(Vector2[] hull)
        {
            float area = 0f;
            for (int i = 0; i < hull.Length; i++)
            {
                var a = hull[i];
                var b = hull[(i + 1) % hull.Length];
                area += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(area) * 0.5f;
        }

        // ---------------- sampling and fitting ----------------

        /// <summary>Vertices plus points spread over every triangle (about every 4 cm).</summary>
        private static IEnumerable<Vector3> SampleSurface(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            foreach (var v in vertices)
                yield return v;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var a = vertices[triangles[t]];
                var b = vertices[triangles[t + 1]];
                var c = vertices[triangles[t + 2]];
                float edge = Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude));
                int n = Mathf.Clamp(Mathf.CeilToInt(edge / SampleSpacing), 1, 48);
                if (n <= 1)
                    continue;
                for (int i = 0; i <= n; i++)
                {
                    for (int j = 0; j <= n - i; j++)
                    {
                        float u = i / (float)n, v = j / (float)n;
                        yield return a + (b - a) * u + (c - a) * v;
                    }
                }
            }
        }

        private static void Add(List<Vector3>[] buckets, Vector3 p)
        {
            for (int i = 0; i < buckets.Length; i++)
            {
                if (p.y < BandLimits[i + 1] || i == buckets.Length - 1)
                {
                    buckets[i].Add(p);
                    return;
                }
            }
        }

        /// <summary>Andrew's monotone chain, counter-clockwise.</summary>
        public static Vector2[] ConvexHull(List<Vector2> points)
        {
            var sorted = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            if (sorted.Count < 3)
                return sorted.ToArray();
            var hull = new Vector2[sorted.Count * 2];
            int k = 0;
            foreach (var p in sorted)
            {
                while (k >= 2 && Cross(hull[k - 2], hull[k - 1], p) <= 0f)
                    k--;
                hull[k++] = p;
            }
            for (int i = sorted.Count - 2, lower = k + 1; i >= 0; i--)
            {
                var p = sorted[i];
                while (k >= lower && Cross(hull[k - 2], hull[k - 1], p) <= 0f)
                    k--;
                hull[k++] = p;
            }
            Array.Resize(ref hull, k - 1);
            return hull;
        }

        /// <summary>The capsule along the outline's main axis that holds every outline point.</summary>
        private static void FitCapsule(Vector2[] hull, out Vector2 a, out Vector2 b, out float radius)
        {
            var mean = Vector2.zero;
            foreach (var p in hull)
                mean += p;
            mean /= hull.Length;
            float xx = 0f, xy = 0f, yy = 0f;
            foreach (var p in hull)
            {
                var d = p - mean;
                xx += d.x * d.x;
                xy += d.x * d.y;
                yy += d.y * d.y;
            }
            float angle = 0.5f * Mathf.Atan2(2f * xy, xx - yy);
            var u = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var v = new Vector2(-u.y, u.x);
            float vMin = float.MaxValue, vMax = float.MinValue;
            foreach (var p in hull)
            {
                float pv = Vector2.Dot(p - mean, v);
                vMin = Mathf.Min(vMin, pv);
                vMax = Mathf.Max(vMax, pv);
            }
            float vc = (vMin + vMax) * 0.5f;
            radius = (vMax - vMin) * 0.5f + 0.001f;
            // The segment ends where every point still fits inside the rounded ends.
            float t0 = float.MaxValue, t1 = float.MinValue;
            foreach (var p in hull)
            {
                float pt = Vector2.Dot(p - mean, u);
                float dv = Vector2.Dot(p - mean, v) - vc;
                float reach = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dv * dv));
                t0 = Mathf.Min(t0, pt + reach);
                t1 = Mathf.Max(t1, pt - reach);
            }
            var axisCenter = mean + v * vc;
            if (t1 <= t0)
            {
                // Round enough: a disc around the middle holding every point.
                a = b = axisCenter + u * ((t0 + t1) * 0.5f);
                float max = 0f;
                foreach (var p in hull)
                    max = Mathf.Max(max, (p - a).magnitude);
                radius = max + 0.001f;
                return;
            }
            a = axisCenter + u * t0;
            b = axisCenter + u * t1;
        }

        private static Vector2 Centroid(Vector2[] hull)
        {
            var c = Vector2.zero;
            foreach (var p in hull)
                c += p;
            return c / Mathf.Max(1, hull.Length);
        }

        private static void Project(Vector2[] hull, Vector2 axis, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            foreach (var p in hull)
            {
                float d = Vector2.Dot(p, axis);
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
            }
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }
}
