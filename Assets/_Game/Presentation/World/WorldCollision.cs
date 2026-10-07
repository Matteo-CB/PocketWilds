using System;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// What an obstacle is made of. Water and ground too steep to walk on block walkers whatever their height;
    /// fliers pass over both.
    /// </summary>
    public enum ObstacleKind : byte
    {
        Solid = 0,
        Water = 1,
        Steep = 2,
    }

    /// <summary>
    /// A solid volume of the world for movement (no-collision rule, Docs/Design/Collision_Rule.md). No physics, no
    /// collider: seen from above, a segment A-B swept by a radius (a disc when A == B); vertically, the band from
    /// <see cref="bottom"/> to <see cref="top"/> above <see cref="baseY"/>. The chunk pipeline builds them from the
    /// props' own meshes (one per height band), from the water surface and from the egg spot.
    /// </summary>
    [Serializable]
    public struct Obstacle
    {
        public Vector2 a;
        public Vector2 b;
        [Min(0f)] public float radius;
        [Tooltip("Ground height under the prop (water: its surface).")]
        public float baseY;
        [Tooltip("Height band of the volume above baseY (m).")]
        public float bottom;
        public float top;
        public ObstacleKind kind;

        public Obstacle(Vector2 a, Vector2 b, float radius, float baseY, float bottom, float top, ObstacleKind kind)
        {
            this.a = a;
            this.b = b;
            this.radius = radius;
            this.baseY = baseY;
            this.bottom = bottom;
            this.top = top;
            this.kind = kind;
        }

        public Vector2 Center => (a + b) * 0.5f;

        /// <summary>Radius of the disc around <see cref="Center"/> holding the whole shape (broad phase).</summary>
        public float Reach => (b - a).magnitude * 0.5f + radius;

        public float WorldBottom => baseY + bottom;
        public float WorldTop => baseY + top;

        /// <summary>Blocks something standing on the ground at <paramref name="groundY"/>, that tall (animals, ground birds).</summary>
        public bool BlocksWalker(float groundY, float height) =>
            kind != ObstacleKind.Solid || (baseY + bottom < groundY + height && baseY + top > groundY - 0.05f);

        /// <summary>A volume in the air (fliers): only solids; water and steep ground lie below the flight.</summary>
        public bool IsSolid => kind == ObstacleKind.Solid;

        /// <summary>Same obstacle moved by a chunk origin.</summary>
        public Obstacle Translated(Vector3 offset)
        {
            var shift = new Vector2(offset.x, offset.z);
            return new Obstacle(a + shift, b + shift, radius, baseY + offset.y, bottom, top, kind);
        }
    }

    /// <summary>Plan-view (x, z) distance helpers for capsules: animals' bodies, obstacles, paths.</summary>
    public static class CollisionMath
    {
        public static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        public static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-12f)
                return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            return a + ab * t;
        }

        public static float PointSegmentDistanceSq(Vector2 p, Vector2 a, Vector2 b) => (p - ClosestOnSegment(p, a, b)).sqrMagnitude;

        /// <summary>
        /// Cross products (m²) this small say "on the line": their sign is rounding noise. Two animals walking one behind
        /// the other on the same line once read as crossing 1.1 m apart.
        /// </summary>
        private const float CollinearCross = 1e-6f;

        /// <summary>Squared distance between two segments (0 when they cross).</summary>
        public static float SegmentSegmentDistanceSq(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
        {
            // A real crossing only: each segment has its ends clearly on both sides of the other's line. Ends lying on
            // the other line are settled exactly by the end distances below (0 when they touch it, the gap otherwise).
            if (Straddles(Cross(q1 - p1, p2 - p1), Cross(q1 - p1, q2 - p1)) && Straddles(Cross(q2 - p2, p1 - p2), Cross(q2 - p2, q1 - p2)))
                return 0f;
            float d = PointSegmentDistanceSq(p1, p2, q2);
            d = Mathf.Min(d, PointSegmentDistanceSq(q1, p2, q2));
            d = Mathf.Min(d, PointSegmentDistanceSq(p2, p1, q1));
            return Mathf.Min(d, PointSegmentDistanceSq(q2, p1, q1));
        }

        /// <summary>Free space between two capsules (negative: they overlap).</summary>
        public static float Gap(Vector2 a0, Vector2 a1, float radiusA, Vector2 b0, Vector2 b1, float radiusB) =>
            Mathf.Sqrt(SegmentSegmentDistanceSq(a0, a1, b0, b1)) - radiusA - radiusB;

        /// <summary>Gap between a capsule and an obstacle, seen from above.</summary>
        public static float Gap(Vector2 a0, Vector2 a1, float radius, in Obstacle obstacle) =>
            Gap(a0, a1, radius, obstacle.a, obstacle.b, obstacle.radius);

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        private static bool Straddles(float a, float b) => (a > CollinearCross && b < -CollinearCross) || (a < -CollinearCross && b > CollinearCross);
    }

    /// <summary>
    /// Footprint of an animal seen from above, in its own space (z forward, meters at scale 1): a capsule along z
    /// from <see cref="back"/> to <see cref="front"/>, <see cref="halfWidth"/> wide on each side. Measured from the
    /// species mesh by the AnimalPipeline, so noses, tails and flanks never enter anything.
    /// </summary>
    [Serializable]
    public struct BodyShape
    {
        public float front;
        public float back;
        [Min(0.01f)] public float halfWidth;
        [Min(0.01f)] public float height;

        public BodyShape(float front, float back, float halfWidth, float height)
        {
            this.front = front;
            this.back = back;
            this.halfWidth = halfWidth;
            this.height = height;
        }

        /// <summary>Farthest point from the root seen from above (any yaw fits in this disc).</summary>
        public float Reach => Mathf.Max(Mathf.Max(front, -back), halfWidth);

        /// <summary>The capsule's axis in world space for a pose.</summary>
        public void Segment(Vector3 position, float yaw, float scale, out Vector2 start, out Vector2 end)
        {
            float rad = yaw * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            float r = halfWidth;
            float s0 = back + r, s1 = front - r;
            if (s1 < s0)
                s0 = s1 = (back + front) * 0.5f;
            var root = new Vector2(position.x, position.z);
            start = root + forward * (s0 * scale);
            end = root + forward * (s1 * scale);
        }
    }

    /// <summary>
    /// Ground heights of a chunk on a regular grid (chunk space), sampled from the ground mesh by the pipeline.
    /// Animals and ground birds stand on it: no foot sinks in a bump, none floats over a dip.
    /// </summary>
    [Serializable]
    public sealed class GroundHeightField
    {
        public Vector2 origin;
        [Min(0.05f)] public float cell = 0.5f;
        public int countX;
        public int countZ;
        public float[] heights = Array.Empty<float>();

        public bool IsValid => heights != null && countX > 1 && countZ > 1 && heights.Length == countX * countZ;

        /// <summary>Bilinear height at a chunk-space point (clamped to the grid).</summary>
        public float Sample(float x, float z)
        {
            float fx = Mathf.Clamp((x - origin.x) / cell, 0f, countX - 1.001f);
            float fz = Mathf.Clamp((z - origin.y) / cell, 0f, countZ - 1.001f);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            int i = iz * countX + ix;
            float h0 = Mathf.Lerp(heights[i], heights[i + 1], tx);
            float h1 = Mathf.Lerp(heights[i + countX], heights[i + countX + 1], tx);
            return Mathf.Lerp(h0, h1, tz);
        }
    }

    /// <summary>A chunk's ground in world space (height field plus the chunk origin); flat when no field is given.</summary>
    public readonly struct GroundSurface
    {
        private const float NormalStep = 0.15f;

        private readonly GroundHeightField _field;
        private readonly Vector3 _offset;

        public GroundSurface(GroundHeightField field, Vector3 offset)
        {
            _field = field != null && field.IsValid ? field : null;
            _offset = offset;
        }

        public static GroundSurface Flat(float height) => new GroundSurface(null, new Vector3(0f, height, 0f));

        public bool HasField => _field != null;

        public float HeightAt(float x, float z) => _field != null ? _offset.y + _field.Sample(x - _offset.x, z - _offset.z) : _offset.y;

        public Vector3 NormalAt(float x, float z)
        {
            if (_field == null)
                return Vector3.up;
            float dx = HeightAt(x + NormalStep, z) - HeightAt(x - NormalStep, z);
            float dz = HeightAt(x, z + NormalStep) - HeightAt(x, z - NormalStep);
            return new Vector3(-dx, 2f * NormalStep, -dz).normalized;
        }
    }
}
