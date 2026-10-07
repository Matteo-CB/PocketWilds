using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T35A: the entrance of the next zone to open (doc 02 Améliorations, section 4.6). A low hedge of round bushes
    /// across the seam and a wooden sign on the open side. Pooled renderers made once; placing them never allocates
    /// except the obstacle array (when the next zone changes). The hedge sinks into the ground in 0.5 s when the zone
    /// opens, and the next one rises at the next seam. Nothing enters anything: each bush and the sign skip any prop,
    /// water or steep ground, and give their own volumes to the animals, the plants and the birds.
    /// </summary>
    public sealed class ZoneBoundary : IDisposable
    {
        public const int MaxBushes = 32;
        private const float MoveSeconds = 0.5f;
        private const float ReduceMotionSeconds = 0.3f;
        private const float SignSetback = 0.85f;

        private readonly ZoneKit _kit;
        private readonly Transform _root;
        private readonly MeshRenderer[] _bushes = new MeshRenderer[MaxBushes];
        private readonly Vector3[] _bushRest = new Vector3[MaxBushes];
        // Per hedge piece: half its length (laid along the hedge) and half its depth (its capsule's radius).
        private readonly float[] _halfLength;
        private readonly float[] _halfDepth;
        private readonly MeshRenderer _sign;
        private Vector3 _signRest;
        private int _count;
        private bool _hasSign;
        private Obstacle[] _obstacles = Array.Empty<Obstacle>();
        private float _moveStart = -1f;
        private float _moveSeconds = MoveSeconds;
        private bool _sinking;

        public ZoneBoundary(ZoneKit kit, PaletteTable palettes, Transform parent)
        {
            _kit = kit ?? throw new ArgumentNullException(nameof(kit));
            _root = new GameObject("ZoneBoundary").transform;
            _root.SetParent(parent, false);
            uint bushData = Pack(palettes, kit.paletteId, kit.brightness);
            int kinds = kit.bushes != null ? kit.bushes.Length : 0;
            _halfLength = new float[Math.Max(1, kinds)];
            _halfDepth = new float[Math.Max(1, kinds)];
            for (int k = 0; k < kinds; k++)
                Extents(kit.bushes[k], out _halfLength[k], out _halfDepth[k]);
            for (int i = 0; i < MaxBushes && kinds > 0; i++)
                _bushes[i] = MakeRenderer("Hedge_" + i, kit.bushes[i % kinds], kit.material, bushData);
            if (kit.signMesh != null)
                _sign = MakeRenderer("ZoneSign", kit.signMesh, kit.material, Pack(palettes, kit.signPaletteId, kit.signBrightness));
            Hide();
        }

        /// <summary>The hedge is in the world (a next zone exists).</summary>
        public bool IsPlaced { get; private set; }

        /// <summary>x of the seam the hedge stands on.</summary>
        public float SeamX { get; private set; }

        public int BushCount => _count;
        public bool HasSign => _hasSign;
        public Vector3 SignPosition => _signRest;

        /// <summary>Volumes of the hedge and the sign (world space). Replaced, not edited, when the hedge moves.</summary>
        public Obstacle[] Obstacles => _obstacles;

        public bool IsMoving => _moveStart >= 0f;

        /// <summary>
        /// Stands the hedge across x = <paramref name="seamX"/> from <paramref name="zMin"/> to <paramref name="zMax"/>, on the
        /// ground, clear of every volume in <paramref name="avoid"/> (props and water of both zones). The sign stands on
        /// the open side near z = 0.
        /// </summary>
        public void Place(float seamX, float zMin, float zMax, GroundSurface ground, IReadOnlyList<Obstacle> avoid)
        {
            SeamX = seamX;
            IsPlaced = true;
            _count = 0;
            var volumes = new List<Obstacle>(MaxBushes + 1);
            int kinds = _kit.bushes != null ? _kit.bushes.Length : 0;
            if (kinds > 0)
            {
                float z = zMin;
                int kind = 0;
                uint hash = SeedHash.Mix((uint)Mathf.RoundToInt(seamX * 10f), 0x4ED60001u);
                while (_count < MaxBushes)
                {
                    int piece = kind % kinds;
                    float half = _halfLength[piece], depth = _halfDepth[piece];
                    float centerZ = z + half;
                    if (centerZ + half > zMax)
                        break;
                    hash = SeedHash.Mix(hash);
                    float jitter = ((hash & 0xFF) / 255f - 0.5f) * 0.08f;
                    var p = new Vector3(seamX + jitter, 0f, centerZ);
                    // The piece seen from above: a capsule along the hedge.
                    float reach = Mathf.Max(0f, half - depth);
                    var a = new Vector2(p.x, centerZ - reach);
                    var b = new Vector2(p.x, centerZ + reach);
                    if (Clear(a, b, depth, avoid, ground.HeightAt(p.x, p.z)))
                    {
                        p.y = ground.HeightAt(p.x, p.z);
                        var bush = _bushes[_count];
                        bush.GetComponent<MeshFilter>().sharedMesh = _kit.bushes[piece];
                        // Long side along the hedge, every other piece turned around, a little off so it never reads as copies.
                        float yaw = 90f + ((hash >> 9 & 1) == 0 ? 0f : 180f) + ((hash >> 8 & 0xFF) / 255f - 0.5f) * (reach > 0.2f ? 6f : 24f);
                        bush.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
                        bush.gameObject.SetActive(true);
                        _bushRest[_count] = p;
                        volumes.Add(new Obstacle(a, b, depth, p.y, 0f, _kit.bushHeight, ObstacleKind.Solid));
                        _count++;
                        kind++;
                        z = centerZ + half;
                    }
                    else
                    {
                        z += 0.15f; // a prop or the water there: a little further
                    }
                }
            }
            for (int i = _count; i < MaxBushes; i++)
                if (_bushes[i] != null)
                    _bushes[i].gameObject.SetActive(false);

            _hasSign = false;
            if (_sign != null)
            {
                // Near the middle of the entrance, on the open side, wherever it stands clear.
                for (int k = 0; k < 9 && !_hasSign; k++)
                {
                    float z = (k & 1) == 0 ? k * 0.35f : -(k + 1) * 0.35f;
                    var p = new Vector3(seamX - SignSetback, 0f, Mathf.Clamp(z, zMin + 0.5f, zMax - 0.5f));
                    var c = new Vector2(p.x, p.z);
                    if (!Clear(c, c, _kit.signRadius, avoid, ground.HeightAt(p.x, p.z)) || Touches(p, _kit.signRadius, volumes))
                        continue;
                    p.y = ground.HeightAt(p.x, p.z);
                    _sign.transform.SetPositionAndRotation(p, Quaternion.identity);
                    _sign.gameObject.SetActive(true);
                    _signRest = p;
                    _hasSign = true;
                    volumes.Add(new Obstacle(new Vector2(p.x, p.z), new Vector2(p.x, p.z), _kit.signRadius, p.y, 0f, _kit.signHeight, ObstacleKind.Solid));
                }
            }
            if (!_hasSign && _sign != null)
                _sign.gameObject.SetActive(false);
            _obstacles = volumes.ToArray();
            _moveStart = -1f;
        }

        public void Hide()
        {
            IsPlaced = false;
            _count = 0;
            _hasSign = false;
            _obstacles = Array.Empty<Obstacle>();
            for (int i = 0; i < MaxBushes; i++)
                if (_bushes[i] != null)
                    _bushes[i].gameObject.SetActive(false);
            if (_sign != null)
                _sign.gameObject.SetActive(false);
            _moveStart = -1f;
        }

        /// <summary>The zone opened: the hedge and the sign sink into the ground (shorter with Reduce Motion).</summary>
        public void Sink(float time, bool reduceMotion)
        {
            _moveStart = time;
            _moveSeconds = reduceMotion ? ReduceMotionSeconds : MoveSeconds;
            _sinking = true;
        }

        /// <summary>The hedge just placed comes up out of the ground.</summary>
        public void Rise(float time, bool reduceMotion)
        {
            _moveStart = time;
            _moveSeconds = reduceMotion ? ReduceMotionSeconds : MoveSeconds;
            _sinking = false;
            Apply(0f);
        }

        /// <summary>Animates a sink or a rise. Returns true the frame a sink ends (the caller moves the hedge on).</summary>
        public bool Tick(float time)
        {
            if (_moveStart < 0f)
                return false;
            float t = Mathf.Clamp01((time - _moveStart) / _moveSeconds);
            Apply(_sinking ? 1f - t : t);
            if (t < 1f)
                return false;
            _moveStart = -1f;
            return _sinking;
        }

        /// <summary>A tap ray hits the sign or a bush of the hedge.</summary>
        public bool Hits(Ray ray)
        {
            if (!IsPlaced)
                return false;
            if (_hasSign && HitsSphere(ray, _signRest + new Vector3(0f, _kit.signHeight * 0.6f, 0f), 0.45f))
                return true;
            // Each piece: three spheres along its length (pieces are laid along z).
            for (int i = 0; i < _count; i++)
            {
                var mid = _bushRest[i] + new Vector3(0f, _kit.bushHeight * 0.5f, 0f);
                float half = Mathf.Max(0f, _obstacles.Length > i ? (_obstacles[i].b - _obstacles[i].a).magnitude * 0.5f : 0f);
                for (int k = -1; k <= 1; k++)
                    if (HitsSphere(ray, mid + new Vector3(0f, 0f, k * half), 0.5f))
                        return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
        }

        /// <summary>Raise 0 (in the ground) to 1 (standing), eased.</summary>
        private void Apply(float raise)
        {
            float eased = raise * raise * (3f - 2f * raise);
            float drop = (1f - eased) * (_kit.bushHeight + 0.15f);
            for (int i = 0; i < _count; i++)
                _bushes[i].transform.position = _bushRest[i] - new Vector3(0f, drop, 0f);
            if (_hasSign)
                _sign.transform.position = _signRest - new Vector3(0f, (1f - eased) * (_kit.signHeight + 0.1f), 0f);
        }

        private static bool Clear(Vector2 a, Vector2 b, float radius, IReadOnlyList<Obstacle> avoid, float groundY)
        {
            for (int i = 0; i < avoid.Count; i++)
            {
                var o = avoid[i];
                if (!o.BlocksWalker(groundY, 1.5f))
                    continue;
                float min = radius + o.radius;
                if (CollisionMath.SegmentSegmentDistanceSq(a, b, o.a, o.b) < min * min)
                    return false;
            }
            return true;
        }

        private static bool Touches(Vector3 p, float radius, List<Obstacle> volumes)
        {
            var c = new Vector2(p.x, p.z);
            for (int i = 0; i < volumes.Count; i++)
            {
                float min = radius + volumes[i].radius;
                if (CollisionMath.PointSegmentDistanceSq(c, volumes[i].a, volumes[i].b) < min * min)
                    return true;
            }
            return false;
        }

        private MeshRenderer MakeRenderer(string name, Mesh mesh, Material material, uint userValue)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.SetShaderUserValue(userValue);
            go.SetActive(false);
            return renderer;
        }

        private static uint Pack(PaletteTable palettes, uint paletteId, byte brightness)
        {
            int row = palettes != null && palettes.TryGetRow(new ContentId(paletteId), out int r) ? r : 0;
            return EnvironmentVisualPacking.Pack(row, brightness, false, false);
        }

        /// <summary>Half the mesh's length along its x (laid along the hedge) and half its depth along z, from above.</summary>
        private static void Extents(Mesh mesh, out float halfLength, out float halfDepth)
        {
            if (mesh == null)
            {
                halfLength = halfDepth = 0.45f;
                return;
            }
            var b = mesh.bounds;
            halfLength = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x));
            halfDepth = Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
        }

        private static bool HitsSphere(Ray ray, Vector3 center, float radius)
        {
            var toCenter = center - ray.origin;
            float along = Vector3.Dot(toCenter, ray.direction);
            if (along < 0f)
                return false;
            return (toCenter - ray.direction * along).sqrMagnitude <= radius * radius;
        }
    }
}
