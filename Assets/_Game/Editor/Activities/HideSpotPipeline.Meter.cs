using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWilds.Editor.Activities
{
    public static partial class HideSpotPipeline
    {
        internal static class Prof
        {
            public static readonly string[] Names = { "load species", "load chunks", "zones", "save", "meter ctor", "measure line", "measure slide", "measure landing",
                "measure alone", "measure cover", "measure decorShown", "measure other", "allowed", "worn", "clearance", "discClearance", "onIsland",
                "cand line search", "cand slide", "cand otherProps", "cand landing", "attempt candidates", "attempt degree", "attempt choose", "attempt gather",
                "attempt aimfit", "attempt stats", "own meter ctor", "decorShown total", "hopFree", "measure bisect", "measure box" };
            public const int LoadSpecies = 0, LoadChunks = 1, Zones = 2, Save = 3, MeterCtor = 4, MLine = 5, MSlide = 6, MLanding = 7, MAlone = 8, MCover = 9,
                MDecorShown = 10, MOther = 11, Allowed = 12, Worn = 13, Clearance = 14, DiscClearance = 15, OnIsland = 16, CLine = 17, CSlide = 18,
                COther = 19, CLanding = 20, ACand = 21, ADegree = 22, AChoose = 23, AGather = 24, AAim = 25, AStats = 26, OwnCtor = 27, DecorShownT = 28,
                HopFree = 29, MBisect = 30, MBox = 31;
            public static readonly long[] Ticks = new long[Names.Length];
            public static readonly long[] Count = new long[Names.Length];
            public static int Cat = MOther;
            public static long ChunkVisited, ChunkRastered, ChunkTicks, AnimalTicks, ProjectTicks, AnimalTris, BigVisited;
            public static long Now => System.Diagnostics.Stopwatch.GetTimestamp();
            public static void Add(int i, long start) { Ticks[i] += Now - start; Count[i]++; }
            public static void Reset() { Array.Clear(Ticks, 0, Ticks.Length); Array.Clear(Count, 0, Count.Length); ChunkVisited = ChunkRastered = ChunkTicks = AnimalTicks = ProjectTicks = AnimalTris = BigVisited = 0; }
            public static string Report()
            {
                var sb = new System.Text.StringBuilder("PROFILE\n");
                for (int i = 0; i < Names.Length; i++)
                    sb.Append($"{Names[i]}: {Ticks[i] / (double)System.Diagnostics.Stopwatch.Frequency:F2} s, {Count[i]} calls, {(Count[i] > 0 ? Ticks[i] * 1e6 / System.Diagnostics.Stopwatch.Frequency / Count[i] : 0):F1} us/call\n");
                double f = System.Diagnostics.Stopwatch.Frequency;
                sb.Append($"measure parts: project {ProjectTicks / f:F2} s, chunk {ChunkTicks / f:F2} s ({ChunkVisited} visited, {ChunkRastered} rastered, big {BigVisited}), animal {AnimalTicks / f:F2} s ({AnimalTris} tris)").AppendLine();
                return sb.ToString();
            }
        }

        /// <summary>
        /// The visible share of an animal's silhouette from one camera position (doc 01 S-ACT-002: « une part de sa
        /// silhouette reste visible »). The species' LOD0 triangles, posed in its Idle bind pose, are projected from the
        /// camera onto a small screen grid (40 cells across the animal): the cells they cover are its silhouette, each with
        /// the animal's nearest depth. The chunk's own triangles (ground and every prop's LOD0, the decor included) are
        /// projected onto the same grid; a silhouette cell is visible when nothing of the chunk lies in front of the animal
        /// there. Share = visible cells / silhouette cells. Only the camera's position matters (not its rotation or field of
        /// view), so the zone camera can be re-aimed afterwards without measuring again. Editor only; buffers are reused.
        /// </summary>
        internal sealed class ShareMeter
        {
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
            private readonly List<int>[] _bins = new List<int>[Bins * Bins];
            private readonly List<int> _big = new List<int>();
            private readonly int[] _stamp;
            private int _stampId;

            private readonly float[] _animalDepth = new float[(Cells + 2) * (Cells + 2)];
            private readonly float[] _chunkDepth = new float[(Cells + 2) * (Cells + 2)];
            private float[] _au = Array.Empty<float>();
            private float[] _aw = Array.Empty<float>();
            private float[] _aiz = Array.Empty<float>();

            public Vector3 Position => _position;
            public int TriangleCount => _triangles.Length / 3;

            public ShareMeter(Vector3 cameraPosition, Quaternion cameraRotation, List<Vector3> vertices, List<int> triangles)
            {
                long p0 = Prof.Now;
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
                _stamp = new int[_triangles.Length / 3];
                for (int i = 0; i < _bins.Length; i++)
                    _bins[i] = new List<int>();
                for (int t = 0; t < _stamp.Length; t++)
                {
                    int x0 = Bin(_box[t * 5]), x1 = Bin(_box[t * 5 + 1]);
                    int y0 = Bin(_box[t * 5 + 2]), y1 = Bin(_box[t * 5 + 3]);
                    if ((x1 - x0 + 1) * (y1 - y0 + 1) > MaxBinsPerTriangle)
                    {
                        _big.Add(t);
                        continue;
                    }
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                            _bins[y * Bins + x].Add(t);
                }
                Prof.Add(Prof.MeterCtor, p0);
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
            /// </summary>
            public float Measure(SpeciesModel species, Vector3 position, float yaw, out Vector4 box, bool allFrames = false)
            {
                long p0 = Prof.Now;
                try { return MeasureCore(species, position, yaw, out box, allFrames); }
                finally { Prof.Add(Prof.Cat, p0); }
            }

            private float MeasureCore(SpeciesModel species, Vector3 position, float yaw, out Vector4 box, bool allFrames = false)
            {
                var frames = allFrames ? species.Frames : species.SearchFrames;
                int n = species.Frames[0].Length;
                int total = n * frames.Length;
                if (_au.Length < total)
                {
                    _au = new float[total];
                    _aw = new float[total];
                    _aiz = new float[total];
                }
                long q0 = Prof.Now;
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
                        _au[o + i] = u;
                        _aw[o + i] = w;
                        _aiz[o + i] = iz;
                        uMin = Mathf.Min(uMin, u);
                        uMax = Mathf.Max(uMax, u);
                        wMin = Mathf.Min(wMin, w);
                        wMax = Mathf.Max(wMax, w);
                        zMax = Mathf.Max(zMax, z);
                    }
                }
                box = new Vector4(uMin, wMin, uMax, wMax);
                long q1 = Prof.Now; Prof.ProjectTicks += q1 - q0;
                float extent = Mathf.Max(uMax - uMin, wMax - wMin);
                if (extent <= 1e-6f)
                    return 0f;
                float cell = extent / Cells;
                float inv = 1f / cell;
                int cols = Mathf.Min(Cells + 1, Mathf.CeilToInt((uMax - uMin) * inv) + 1);
                int rows = Mathf.Min(Cells + 1, Mathf.CeilToInt((wMax - wMin) * inv) + 1);
                int count = cols * rows;
                for (int i = 0; i < count; i++)
                    _chunkDepth[i] = float.PositiveInfinity;

                // The chunk's triangles over the animal's box, nearer than its far side.
                _stampId++;
                float uHi = uMin + cols * cell, wHi = wMin + rows * cell;
                int bx0 = Bin(uMin), bx1 = Bin(uHi), by0 = Bin(wMin), by1 = Bin(wHi);
                for (int y = by0; y <= by1; y++)
                    for (int x = bx0; x <= bx1; x++)
                        foreach (int t in _bins[y * Bins + x])
                            ChunkTriangle(t, uMin, wMin, uHi, wHi, zMax, cols, rows, inv);
                foreach (int t in _big)
                    ChunkTriangle(t, uMin, wMin, uHi, wHi, zMax, cols, rows, inv);
                Prof.BigVisited += _big.Count;
                long q2 = Prof.Now; Prof.ChunkTicks += q2 - q1;

                var triangles = species.Triangles;
                float best = 0f;
                for (int f = 0; f < frames.Length; f++)
                {
                    for (int i = 0; i < count; i++)
                        _animalDepth[i] = float.PositiveInfinity;
                    int o = f * n;
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                    {
                        int a = o + triangles[t], b = o + triangles[t + 1], c = o + triangles[t + 2];
                        Raster(_animalDepth, cols, rows, uMin, wMin, inv, _au[a], _aw[a], _aiz[a], _au[b], _aw[b], _aiz[b], _au[c], _aw[c], _aiz[c]);
                    }
                    int covered = 0, visible = 0;
                    for (int i = 0; i < count; i++)
                    {
                        float d = _animalDepth[i];
                        if (float.IsPositiveInfinity(d))
                            continue;
                        covered++;
                        if (d < _chunkDepth[i] - DepthEpsilon)
                            visible++;
                    }
                    if (covered > 0)
                        best = Mathf.Max(best, visible / (float)covered);
                }
                Prof.AnimalTris += triangles.Length / 3 * frames.Length;
                Prof.AnimalTicks += Prof.Now - q2;
                return best;
            }

            private void ChunkTriangle(int t, float uMin, float wMin, float uHi, float wHi, float zMax, int cols, int rows, float inv)
            {
                if (_stamp[t] == _stampId)
                    return;
                _stamp[t] = _stampId;
                Prof.ChunkVisited++;
                int o = t * 5;
                if (_box[o + 4] >= zMax || _box[o + 1] < uMin || _box[o] > uHi || _box[o + 3] < wMin || _box[o + 2] > wHi)
                    return;
                Prof.ChunkRastered++;
                int a = _triangles[t * 3], b = _triangles[t * 3 + 1], c = _triangles[t * 3 + 2];
                Raster(_chunkDepth, cols, rows, uMin, wMin, inv, _u[a], _w[a], _iz[a], _u[b], _w[b], _iz[b], _u[c], _w[c], _iz[c]);
            }

            /// <summary>Nearest depth of a triangle at each covered cell center (1 / z interpolated: exact in perspective).</summary>
            private static void Raster(float[] depth, int cols, int rows, float u0, float w0, float inv,
                float ua, float wa, float iza, float ub, float wb, float izb, float uc, float wc, float izc)
            {
                float ax = (ua - u0) * inv, ay = (wa - w0) * inv;
                float bx = (ub - u0) * inv, by = (wb - w0) * inv;
                float cx = (uc - u0) * inv, cy = (wc - w0) * inv;
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
