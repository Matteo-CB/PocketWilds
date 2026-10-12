using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWilds.Editor.Activities
{
    public static partial class HideSpotPipeline
    {
        /// <summary>
        /// The visible share of an animal's silhouette from one camera position (doc 01 S-ACT-002: « une part de sa
        /// silhouette reste visible »). The species' LOD0 triangles, posed in its Idle bind pose, are projected from the
        /// camera onto a small screen grid (40 cells across the animal): the cells they cover are its silhouette, each with
        /// the animal's nearest depth. The chunk's own triangles (ground and every prop's LOD0, the decor included) are
        /// projected onto the same grid; a silhouette cell is visible when nothing of the chunk lies in front of the animal
        /// there. Share = visible cells / silhouette cells. Only the camera's position matters (not its rotation or field of
        /// view), so the zone camera can be re-aimed afterwards without measuring again. Editor only. A meter is read-only once
        /// built: the buffers a measure works in belong to the calling thread, so the build measures from many threads at once.
        /// </summary>
        internal sealed class ShareMeter
        {
            /// <summary>One thread's working buffers. The stamps only grow, so a stamp left by another meter never matches.</summary>
            private sealed class Scratch
            {
                public readonly float[] AnimalDepth = new float[(Cells + 2) * (Cells + 2)];
                public readonly float[] ChunkDepth = new float[(Cells + 2) * (Cells + 2)];
                public float[] Au = Array.Empty<float>();
                public float[] Aw = Array.Empty<float>();
                public float[] Aiz = Array.Empty<float>();
                public int[] Stamp = Array.Empty<int>();
                public int StampId;
            }

            [ThreadStatic] private static Scratch _scratch;

            /// <summary>The build's measures cache and this meter's key in it (its camera and triangles); null: none.</summary>
            private readonly MeasureMemo _memo;
            private readonly Key128 _id;

            public const int Cells = 40;
            private const float NearZ = 0.05f;
            private const float DepthEpsilon = 0.02f;
            private const int Bins = 64;
            private const float BinMin = -1.25f;
            private const float BinSize = 2.5f / Bins;
            private const int MaxBinsPerTriangle = 256;

            private readonly Vector3 _position;
            private readonly Vector3 _right;
            private readonly Vector3 _up;
            private readonly Vector3 _forward;

            // Chunk triangles, projected once: tangent-space u = x / z, w = y / z and 1 / z per vertex.
            private readonly float[] _u;
            private readonly float[] _w;
            private readonly float[] _iz;
            private readonly int[] _triangles;
            private readonly float[] _box;   // per triangle: uMin, uMax, wMin, wMax, zMin
            private readonly int[][] _bins = new int[Bins * Bins][];
            private readonly int[] _big;
            private readonly int _triangleCount;

            public Vector3 Position => _position;
            public int TriangleCount => _triangles.Length / 3;

            public ShareMeter(Vector3 cameraPosition, Quaternion cameraRotation, List<Vector3> vertices, List<int> triangles, MeasureMemo memo = null)
            {
                if (memo != null)
                {
                    _memo = memo;
                    _id = Hasher.Of(h => h.Add(cameraPosition).Add(cameraRotation).Add(vertices).Add(triangles));
                }
                _position = cameraPosition;
                _right = cameraRotation * Vector3.right;
                _up = cameraRotation * Vector3.up;
                _forward = cameraRotation * Vector3.forward;
                int n = vertices.Count;
                _u = new float[n];
                _w = new float[n];
                _iz = new float[n];
                for (int i = 0; i < n; i++)
                {
                    var d = vertices[i] - _position;
                    float z = Vector3.Dot(d, _forward);
                    if (z <= NearZ)
                    {
                        _iz[i] = -1f;
                        continue;
                    }
                    _iz[i] = 1f / z;
                    _u[i] = Vector3.Dot(d, _right) * _iz[i];
                    _w[i] = Vector3.Dot(d, _up) * _iz[i];
                }
                var kept = new List<int>(triangles.Count);
                var box = new List<float>(triangles.Count / 3 * 5);
                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (_iz[a] <= 0f || _iz[b] <= 0f || _iz[c] <= 0f)
                        continue;
                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                    box.Add(Mathf.Min(_u[a], Mathf.Min(_u[b], _u[c])));
                    box.Add(Mathf.Max(_u[a], Mathf.Max(_u[b], _u[c])));
                    box.Add(Mathf.Min(_w[a], Mathf.Min(_w[b], _w[c])));
                    box.Add(Mathf.Max(_w[a], Mathf.Max(_w[b], _w[c])));
                    box.Add(1f / Mathf.Max(_iz[a], Mathf.Max(_iz[b], _iz[c])));
                }
                _triangles = kept.ToArray();
                _box = box.ToArray();
                _triangleCount = _triangles.Length / 3;
                var bins = new List<int>[Bins * Bins];
                for (int i = 0; i < bins.Length; i++)
                    bins[i] = new List<int>();
                var big = new List<int>();
                for (int t = 0; t < _triangleCount; t++)
                {
                    int x0 = Bin(_box[t * 5]), x1 = Bin(_box[t * 5 + 1]);
                    int y0 = Bin(_box[t * 5 + 2]), y1 = Bin(_box[t * 5 + 3]);
                    if ((x1 - x0 + 1) * (y1 - y0 + 1) > MaxBinsPerTriangle)
                    {
                        big.Add(t);
                        continue;
                    }
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                            bins[y * Bins + x].Add(t);
                }
                for (int i = 0; i < bins.Length; i++)
                    _bins[i] = bins[i].ToArray();
                _big = big.ToArray();
            }

            private static int Bin(float v) => Mathf.Clamp((int)Mathf.Floor((v - BinMin) / BinSize), 0, Bins - 1);

            /// <summary>Tangent-space point of a world point (x = right / depth, y = up / depth); false behind the camera.</summary>
            public bool Project(Vector3 world, out Vector2 tangent, out float depth)
            {
                var d = world - _position;
                depth = Vector3.Dot(d, _forward);
                if (depth <= NearZ)
                {
                    tangent = default;
                    return false;
                }
                tangent = new Vector2(Vector3.Dot(d, _right) / depth, Vector3.Dot(d, _up) / depth);
                return true;
            }

            /// <summary>
            /// The share of the species' silhouette visible at this ground point and yaw (scale 1): the largest over its
            /// sampled frames of the hiding pose (<paramref name="allFrames"/>; else the search subset), and the frames'
            /// tangent-space box (min x, min y, max x, max y). The chunk is drawn once into the frames' common grid.
            /// <paramref name="stopAt"/>: the caller only asks whether the share reaches it; the frames stop as soon as one
            /// does, and the share returned is then that frame's (at least <paramref name="stopAt"/>, at most the full share).
            /// </summary>
            public float Measure(SpeciesModel species, Vector3 position, float yaw, out Vector4 box, bool allFrames = false,
                float stopAt = float.PositiveInfinity)
            {
                if (_memo == null || species.MeasureKey.IsEmpty)
                    return MeasureNow(species, position, yaw, out box, allFrames, stopAt, out _);
                var key = new MemoKey(_id, species.MeasureKey, position, yaw, allFrames);
                if (_memo.TryGet(key, out var known))
                {
                    box = known.Box;
                    return known.Share;
                }
                float share = MeasureNow(species, position, yaw, out box, allFrames, stopAt, out bool stopped);
                if (!stopped)
                    _memo.Add(key, new MemoValue(share, box));
                return share;
            }

            private float MeasureNow(SpeciesModel species, Vector3 position, float yaw, out Vector4 box, bool allFrames, float stopAt, out bool stopped)
            {
                stopped = false;
                var sc = _scratch ?? (_scratch = new Scratch());
                var frames = allFrames ? species.Frames : species.SearchFrames;
                int n = species.Frames[0].Length;
                int total = n * frames.Length;
                if (sc.Au.Length < total)
                {
                    sc.Au = new float[total];
                    sc.Aw = new float[total];
                    sc.Aiz = new float[total];
                }
                if (sc.Stamp.Length < _triangleCount)
                    sc.Stamp = new int[_triangleCount];
                float[] au = sc.Au, aw = sc.Aw, aiz = sc.Aiz, chunkDepth = sc.ChunkDepth, animalDepth = sc.AnimalDepth;
                float rad = yaw * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
                float uMin = float.MaxValue, uMax = float.MinValue, wMin = float.MaxValue, wMax = float.MinValue, zMax = 0f;
                for (int f = 0; f < frames.Length; f++)
                {
                    var vertices = frames[f];
                    int o = f * n;
                    for (int i = 0; i < n; i++)
                    {
                        var v = vertices[i];
                        var p = new Vector3(position.x + v.x * cos + v.z * sin, position.y + v.y, position.z - v.x * sin + v.z * cos);
                        var d = p - _position;
                        float z = Mathf.Max(NearZ, Vector3.Dot(d, _forward));
                        float iz = 1f / z;
                        float u = Vector3.Dot(d, _right) * iz, w = Vector3.Dot(d, _up) * iz;
                        au[o + i] = u;
                        aw[o + i] = w;
                        aiz[o + i] = iz;
                        uMin = Mathf.Min(uMin, u);
                        uMax = Mathf.Max(uMax, u);
                        wMin = Mathf.Min(wMin, w);
                        wMax = Mathf.Max(wMax, w);
                        zMax = Mathf.Max(zMax, z);
                    }
                }
                box = new Vector4(uMin, wMin, uMax, wMax);
                float extent = Mathf.Max(uMax - uMin, wMax - wMin);
                if (extent <= 1e-6f)
                    return 0f;
                float cell = extent / Cells;
                float inv = 1f / cell;
                int cols = Mathf.Min(Cells + 1, Mathf.CeilToInt((uMax - uMin) * inv) + 1);
                int rows = Mathf.Min(Cells + 1, Mathf.CeilToInt((wMax - wMin) * inv) + 1);
                int count = cols * rows;
                for (int i = 0; i < count; i++)
                    chunkDepth[i] = float.PositiveInfinity;

                // The chunk's triangles over the animal's box, nearer than its far side.
                int stampId = ++sc.StampId;
                var stamp = sc.Stamp;
                float uHi = uMin + cols * cell, wHi = wMin + rows * cell;
                int bx0 = Bin(uMin), bx1 = Bin(uHi), by0 = Bin(wMin), by1 = Bin(wHi);
                for (int y = by0; y <= by1; y++)
                    for (int x = bx0; x <= bx1; x++)
                        foreach (int t in _bins[y * Bins + x])
                            ChunkTriangle(t, stamp, stampId, chunkDepth, uMin, wMin, uHi, wHi, zMax, cols, rows, inv);
                foreach (int t in _big)
                    ChunkTriangle(t, stamp, stampId, chunkDepth, uMin, wMin, uHi, wHi, zMax, cols, rows, inv);

                var triangles = species.Triangles;
                float best = 0f;
                for (int f = 0; f < frames.Length; f++)
                {
                    for (int i = 0; i < count; i++)
                        animalDepth[i] = float.PositiveInfinity;
                    int o = f * n;
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                    {
                        int a = o + triangles[t], b = o + triangles[t + 1], c = o + triangles[t + 2];
                        Raster(animalDepth, cols, rows, uMin, wMin, inv, au[a], aw[a], aiz[a], au[b], aw[b], aiz[b], au[c], aw[c], aiz[c]);
                    }
                    int covered = 0, visible = 0;
                    for (int i = 0; i < count; i++)
                    {
                        float d = animalDepth[i];
                        if (float.IsPositiveInfinity(d))
                            continue;
                        covered++;
                        if (d < chunkDepth[i] - DepthEpsilon)
                            visible++;
                    }
                    if (covered > 0)
                        best = Mathf.Max(best, visible / (float)covered);
                    // Every cell shows (the share is exactly 1): no later frame shows more.
                    if (best >= 1f)
                        break;
                    if (best >= stopAt)
                    {
                        stopped = true;
                        break;
                    }
                }
                return best;
            }

            private void ChunkTriangle(int t, int[] stamp, int stampId, float[] chunkDepth, float uMin, float wMin, float uHi, float wHi, float zMax,
                int cols, int rows, float inv)
            {
                if (stamp[t] == stampId)
                    return;
                stamp[t] = stampId;
                int o = t * 5;
                if (_box[o + 4] >= zMax || _box[o + 1] < uMin || _box[o] > uHi || _box[o + 3] < wMin || _box[o + 2] > wHi)
                    return;
                int a = _triangles[t * 3], b = _triangles[t * 3 + 1], c = _triangles[t * 3 + 2];
                Raster(chunkDepth, cols, rows, uMin, wMin, inv, _u[a], _w[a], _iz[a], _u[b], _w[b], _iz[b], _u[c], _w[c], _iz[c]);
            }

            /// <summary>
            /// No cell center (k + 0.5) lies between these three grid coordinates: their span, widened by 0.01 cell each way,
            /// stays inside one cell (most of an animal's triangles are smaller than a cell). Conservative: a triangle it lets
            /// through is drawn exactly as before, and one it stops could not have drawn a cell, whatever precision the
            /// coordinates are carried in; so the shares stay bit for bit the same.
            /// </summary>
            private static bool BetweenCenters(float a, float b, float c)
            {
                float lo = (a < b ? (a < c ? a : c) : (b < c ? b : c)) + 0.49f;
                float hi = (a > b ? (a > c ? a : c) : (b > c ? b : c)) + 0.51f;
                return lo >= 0f && hi < 1e6f && (int)lo == (int)hi;
            }

            /// <summary>Nearest depth of a triangle at each covered cell center (1 / z interpolated: exact in perspective).</summary>
            private static void Raster(float[] depth, int cols, int rows, float u0, float w0, float inv,
                float ua, float wa, float iza, float ub, float wb, float izb, float uc, float wc, float izc)
            {
                float ax = (ua - u0) * inv, ay = (wa - w0) * inv;
                float bx = (ub - u0) * inv, by = (wb - w0) * inv;
                float cx = (uc - u0) * inv, cy = (wc - w0) * inv;
                if (BetweenCenters(ax, bx, cx) || BetweenCenters(ay, by, cy))
                    return;
                float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                if (Mathf.Abs(area) < 1e-9f)
                    return;
                float invArea = 1f / area;
                int x0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(ax, Mathf.Min(bx, cx)) - 0.5f));
                int x1 = Mathf.Min(cols - 1, Mathf.FloorToInt(Mathf.Max(ax, Mathf.Max(bx, cx)) - 0.5f));
                int y0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(ay, Mathf.Min(by, cy)) - 0.5f));
                int y1 = Mathf.Min(rows - 1, Mathf.FloorToInt(Mathf.Max(ay, Mathf.Max(by, cy)) - 0.5f));
                for (int y = y0; y <= y1; y++)
                {
                    float py = y + 0.5f;
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f;
                        float l0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) * invArea;
                        float l1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) * invArea;
                        float l2 = 1f - l0 - l1;
                        if (l0 < -1e-5f || l1 < -1e-5f || l2 < -1e-5f)
                            continue;
                        float iz = l0 * iza + l1 * izb + l2 * izc;
                        if (iz <= 0f)
                            continue;
                        float d = 1f / iz;
                        int i = y * cols + x;
                        if (d < depth[i])
                            depth[i] = d;
                    }
                }
            }
        }
    }
}
