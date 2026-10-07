using System;
using System.Collections.Generic;
using PocketWilds.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// ScatterRenderer (doc 07, doc 04): draws the ground details of the active chunks with GPU instancing.
    /// No GameObject, no Update per plant: the scene entry calls <see cref="Render"/> once per frame. Matrices are
    /// built once when a chunk becomes active; a frame only culls chunk bounds and submits one instanced draw per
    /// kind and chunk. The quality tier keeps a prefix of every kind group (stable hash rank, see ScatterData).
    /// Nothing allocates per frame. Scatter never casts shadows.
    /// </summary>
    public sealed class ScatterRenderer
    {
        private static readonly int PaletteRowId = Shader.PropertyToID("_PaletteRow");
        private static readonly int WindScaleId = Shader.PropertyToID("_WindScale");

        private sealed class Batch
        {
            public Mesh Mesh;
            public float MinDensity;
            public Matrix4x4[] Matrices;
            public RenderParams Params;
        }

        private sealed class Entry
        {
            public WorldChunk Chunk;
            public Bounds Bounds;
            public Batch[] Batches;
        }

        private readonly List<Entry> _entries = new List<Entry>(8);
        private readonly PaletteTable _palettes;
        private readonly Plane[] _planes = new Plane[6];
        private float _density = 1f;
        private float _fadeStart = float.PositiveInfinity;
        private float _fadeEnd = float.PositiveInfinity;

        public ScatterRenderer(PaletteTable palettes, float density = 1f)
        {
            _palettes = palettes;
            SetDensity(density);
        }

        public int ChunkCount => _entries.Count;
        /// <summary>Instances submitted by the last <see cref="Render"/> (after culling and density).</summary>
        public int LastInstances { get; private set; }
        /// <summary>Instanced draws submitted by the last <see cref="Render"/>.</summary>
        public int LastDraws { get; private set; }
        public float Density => _density;
        /// <summary>Editor captures only (overdraw review): every batch drawn with this material. Null in the game.</summary>
        public Material OverrideMaterial { get; set; }

        /// <summary>Share of every kind group drawn: 0.3 Fluide, 0.65 Équilibrée, 1 Magnifique.</summary>
        public void SetDensity(float density) => _density = Mathf.Clamp01(density);

        /// <summary>
        /// LOD by distance (doc 07 "Distance des LOD"): chunks farther than <paramref name="start"/> meters from the
        /// camera thin out to a quarter at <paramref name="end"/>. Same stable prefix, so nothing pops in place.
        /// </summary>
        public void SetDistanceFade(float start, float end)
        {
            _fadeStart = start > 0f ? start : float.PositiveInfinity;
            _fadeEnd = Mathf.Max(_fadeStart + 0.01f, end);
        }

        private Obstacle[] _keepOut = Array.Empty<Obstacle>();

        /// <summary>
        /// T35A: world volumes nothing grows in (a zone's hedge). Chunks added afterwards skip the plants inside; call
        /// <see cref="Rebuild"/> for chunks already shown. The array is kept, not copied.
        /// </summary>
        public void SetKeepOut(Obstacle[] volumes) => _keepOut = volumes ?? Array.Empty<Obstacle>();

        /// <summary>Builds a chunk again (after the keep-out volumes changed). Allocates, like Add.</summary>
        public void Rebuild(WorldChunk chunk)
        {
            if (Remove(chunk))
                Add(chunk);
        }

        private bool KeptOut(in Matrix4x4 matrix, float radius)
        {
            if (_keepOut.Length == 0)
                return false;
            var p = new Vector2(matrix.m03, matrix.m23);
            float scale = new Vector2(matrix.m00, matrix.m20).magnitude;
            for (int k = 0; k < _keepOut.Length; k++)
            {
                ref var o = ref _keepOut[k];
                float min = o.radius + radius * scale;
                if (CollisionMath.PointSegmentDistanceSq(p, o.a, o.b) < min * min)
                    return true;
            }
            return false;
        }

        /// <summary>Builds the chunk's instance matrices (allocates: call when the chunk becomes active).</summary>
        public bool Add(WorldChunk chunk)
        {
            var data = chunk != null ? chunk.Scatter : null;
            if (data == null || data.kit == null || data.kit.material == null || data.Count == 0 || IndexOf(chunk) >= 0)
                return false;
            using var marker = GameProfilerMarkers.ScatterBuild.Auto();
            var toWorld = chunk.transform.localToWorldMatrix;
            var kit = data.kit;
            var worldBounds = TransformBounds(toWorld, data.bounds);
            var batches = new List<Batch>(data.groups.Length);
            foreach (var group in data.groups)
            {
                if (group.kind < 0 || group.kind >= kit.kinds.Length || group.count <= 0 || kit.kinds[group.kind].mesh == null)
                    continue;
                var kind = kit.kinds[group.kind];
                var kept = new List<Matrix4x4>(group.count);
                var meshBounds = kind.mesh.bounds;
                float kindRadius = Mathf.Max(Mathf.Max(Mathf.Abs(meshBounds.min.x), Mathf.Abs(meshBounds.max.x)),
                    Mathf.Max(Mathf.Abs(meshBounds.min.z), Mathf.Abs(meshBounds.max.z)));
                for (int i = 0; i < group.count; i++)
                {
                    var matrix = toWorld * data.LocalMatrix(group.start + i);
                    // No plant grows inside a hedge (no-collision rule); order is kept for the density prefix.
                    if (!KeptOut(matrix, kindRadius))
                        kept.Add(matrix);
                }
                if (kept.Count == 0)
                    continue;
                var matrices = kept.ToArray();
                var properties = new MaterialPropertyBlock();
                int row = _palettes != null && _palettes.TryGetRow(new ContentId(kind.paletteId), out int r) ? r : -1;
                properties.SetFloat(PaletteRowId, row);
                properties.SetFloat(WindScaleId, kind.windScale);
                batches.Add(new Batch
                {
                    Mesh = kind.mesh,
                    MinDensity = kind.minDensity,
                    Matrices = matrices,
                    Params = new RenderParams(kit.material)
                    {
                        matProps = properties,
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = true,
                        layer = chunk.gameObject.layer,
                        worldBounds = worldBounds,
                    },
                });
            }
            _entries.Add(new Entry { Chunk = chunk, Bounds = worldBounds, Batches = batches.ToArray() });
            return true;
        }

        private static RenderParams WithMaterial(RenderParams source, Material material)
        {
            source.material = material;
            return source;
        }

        public bool Remove(WorldChunk chunk)
        {
            int index = IndexOf(chunk);
            if (index < 0)
                return false;
            _entries.RemoveAt(index);
            return true;
        }

        public void Clear() => _entries.Clear();

        /// <summary>Submits the visible scatter for this frame (all cameras). No allocation.</summary>
        public void Render(Camera camera)
        {
            LastInstances = 0;
            LastDraws = 0;
            if (camera == null || _entries.Count == 0 || _density <= 0f)
                return;
            using var marker = GameProfilerMarkers.ScatterRender.Auto();
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            var eye = camera.transform.position;
            for (int e = 0; e < _entries.Count; e++)
            {
                var entry = _entries[e];
                if (entry.Chunk == null || !GeometryUtility.TestPlanesAABB(_planes, entry.Bounds))
                    continue;
                float distance = Mathf.Sqrt(entry.Bounds.SqrDistance(eye));
                float fade = float.IsPositiveInfinity(_fadeStart) ? 1f : Mathf.Lerp(1f, 0.25f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_fadeStart, _fadeEnd, distance)));
                var batches = entry.Batches;
                for (int b = 0; b < batches.Length; b++)
                {
                    var batch = batches[b];
                    int count = Mathf.CeilToInt(batch.Matrices.Length * Mathf.Max(_density, batch.MinDensity) * fade - 0.0001f);
                    if (count <= 0)
                        continue;
                    // Bound to this camera: a draw queued for one camera never leaks into another render.
                    batch.Params.camera = camera;
                    if (OverrideMaterial == null)
                        Graphics.RenderMeshInstanced(batch.Params, batch.Mesh, 0, batch.Matrices, count);
                    else
                        Graphics.RenderMeshInstanced(WithMaterial(batch.Params, OverrideMaterial), batch.Mesh, 0, batch.Matrices, count);
                    LastInstances += count;
                    LastDraws++;
                }
            }
        }

        private int IndexOf(WorldChunk chunk)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Chunk == chunk)
                    return i;
            }
            return -1;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds local)
        {
            var center = matrix.MultiplyPoint3x4(local.center);
            var e = local.extents;
            var x = matrix.MultiplyVector(new Vector3(e.x, 0f, 0f));
            var y = matrix.MultiplyVector(new Vector3(0f, e.y, 0f));
            var z = matrix.MultiplyVector(new Vector3(0f, 0f, e.z));
            var extents = new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            // Wind bends tips a little outside the authored bounds.
            return new Bounds(center, (extents + Vector3.one * 0.25f) * 2f);
        }
    }
}
