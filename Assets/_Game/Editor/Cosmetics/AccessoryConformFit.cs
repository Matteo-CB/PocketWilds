using System;
using System.Collections.Generic;
using System.Linq;
using PocketWilds.Content;
using UnityEngine;

namespace PocketWilds.Editor.Cosmetics
{
    /// <summary>
    /// T49 conform fit (owner rule: nothing enters anything), kept for later bands; no piece uses it since the T49 redesign (the
    /// goggles, neckerchief, shovel and overalls are rigid pieces resting along their socket's up or forward:
    /// <see cref="AccessoryFitMeasure.RestAxisOf"/>). A band that lies on the body is shaped on the Field Hamster skin; another
    /// wearer needs it wider or taller, not only bigger. For each species the band takes a non-uniform scale around its socket (x across and
    /// y up, i.e. Blender x and z, each 0.7 to 1.5, z the geometric mean so the band keeps its width), a lift and a small forward
    /// or back offset. The skin of every sampled pose is voxelized in the socket's space (1 mm cells, inside by ray parity, the
    /// surface cells solid) and turned into a distance field (exact Euclidean transform), once for all poses and once at rest.
    /// A placement is kept when every band point stays FitClearance (plus the voxel margin) from all poses and the band's inner
    /// side (the points lying on the reference skin) hugs the body: per point the closest the skin comes over the clip poses,
    /// mean at most 6 mm and 90th percentile at most 9.5 mm, and at rest a mean gap of at most 9 mm.
    /// The tightest placements then pass the exact point to triangle check (2 mm) of <see cref="MinClearance"/>; a species where
    /// none does is excluded with the reason (touches, floats).
    /// </summary>
    public static partial class AccessoryFitMeasure
    {
        public const float ConformMinScale = 0.7f;
        public const float ConformMaxScale = 1.5f;

        /// <summary>Mean gap between the band's inner side and the resting skin, at most (the band hugs, never floats).</summary>
        public const float MaxHugGap = 0.006f;

        public const float MaxHugP90 = 0.0095f;

        /// <summary>Mean gap of the inner side at rest, at most (a band clearing a bending neck may rest a little looser).</summary>
        public const float MaxRestGap = 0.009f;

        private const float ConformScaleStep = 0.05f;
        private const float ConformLift = 0.015f;
        private const float ConformLiftStep = 0.0015f;
        private const float MaxAnisotropy = 1.45f;
        private const float InnerBand = 0.0055f;
        private const float VoxelCell = 0.001f;
        private const float VoxelMargin = 0.0006f;
        private const float RoiMargin = 0.014f;
        private const float Jitter = 0.0137f;
        private const int ExactTries = 6;
        private const long MaxCells = 12_000_000;
        private const double Far = 1e12;

        /// <summary>The pieces fitted by <see cref="SolveConform"/>: none since the T49 redesign (every piece is rigid).</summary>
        public static readonly HashSet<string> ConformPieces = new HashSet<string>();

        public static bool IsConform(string accessoryId) => ConformPieces.Contains(accessoryId);

        /// <summary>The band points lying on the reference skin at rest (within 5.5 mm): the side that must hug every wearer.</summary>
        public static bool[] InnerPoints(Shape shape, Surface reference)
        {
            var points = shape.Coarse;
            var inner = new bool[points.Length];
            int count = 0;
            if (reference != null && points.Length > 0)
            {
                var min = points.Aggregate(Vector3.positiveInfinity, Vector3.Min) - Vector3.one * RoiMargin;
                var max = points.Aggregate(Vector3.negativeInfinity, Vector3.Max) + Vector3.one * RoiMargin;
                BuildFields(reference, min, max, true, out _, out var rest);
                for (int i = 0; i < points.Length; i++)
                {
                    inner[i] = rest.Sample(points[i]) <= InnerBand;
                    if (inner[i])
                        count++;
                }
            }
            if (count < 8)
                for (int i = 0; i < inner.Length; i++)
                    inner[i] = true;
            return inner;
        }

        /// <summary>The conform placement of a band on one species (see the class summary); Clearance holds the exact check.</summary>
        public static Result SolveConform(Shape shape, Surface surface, bool[] inner)
        {
            var points = shape.Coarse;
            if (points.Length == 0)
                return new Result { Ok = false, Reason = "empty accessory" };
            var pmin = points.Aggregate(Vector3.positiveInfinity, Vector3.Min);
            var pmax = points.Aggregate(Vector3.negativeInfinity, Vector3.Max);
            float Lo(float a) => a < 0f ? a * ConformMaxScale : a * ConformMinScale;
            float Hi(float a) => a > 0f ? a * ConformMaxScale : a * ConformMinScale;
            var min = new Vector3(Lo(pmin.x), Lo(pmin.y) - ConformLift, Lo(pmin.z) - MaxOffset) - Vector3.one * RoiMargin;
            var max = new Vector3(Hi(pmax.x), Hi(pmax.y) + ConformLift, Hi(pmax.z) + MaxOffset) + Vector3.one * RoiMargin;
            BuildFields(surface, min, max, false, out var all, out var rest);

            var innerIndex = Enumerable.Range(0, points.Length).Where(i => inner == null || i >= inner.Length || inner[i]).ToArray();
            var gaps = new float[innerIndex.Length];
            var found = new List<(float cost, float nearest, Result result)>();
            bool anyClear = false;
            float closestFloat = float.MaxValue;
            int scales = Mathf.RoundToInt((ConformMaxScale - ConformMinScale) / ConformScaleStep);
            int lifts = Mathf.RoundToInt(ConformLift / ConformLiftStep);
            int offsets = Mathf.RoundToInt(MaxOffset / OffsetStep);
            float need = FitClearance + VoxelMargin;
            for (int i = 0; i <= scales; i++)
            {
                float sx = (float)Math.Round(ConformMinScale + i * ConformScaleStep, 2);
                for (int j = 0; j <= scales; j++)
                {
                    float sy = (float)Math.Round(ConformMinScale + j * ConformScaleStep, 2);
                    if (Mathf.Max(sx, sy) / Mathf.Min(sx, sy) > MaxAnisotropy + 1e-4f)
                        continue;
                    float sz = (float)Math.Round(Math.Sqrt(sx * sy), 3);
                    for (int o = -offsets; o <= offsets; o++)
                    {
                        float dz = o * OffsetStep;
                        for (int l = -lifts; l <= lifts; l++)
                        {
                            float lift = l * ConformLiftStep;
                            float nearest = float.MaxValue;
                            for (int k = 0; k < points.Length; k++)
                            {
                                var p = points[k];
                                float d = all.Sample(new Vector3(sx * p.x, sy * p.y + lift, sz * p.z + dz));
                                if (d < nearest)
                                {
                                    nearest = d;
                                    if (d < need)
                                        break;
                                }
                            }
                            if (nearest < need)
                                continue;
                            anyClear = true;
                            // Hug: per inner point, the closest the skin comes over the clip poses; and the gap at rest.
                            double sum = 0, sumRest = 0;
                            for (int k = 0; k < innerIndex.Length; k++)
                            {
                                var p = points[innerIndex[k]];
                                var q = new Vector3(sx * p.x, sy * p.y + lift, sz * p.z + dz);
                                gaps[k] = all.Sample(q);
                                sum += gaps[k];
                                sumRest += rest.Sample(q);
                            }
                            float mean = (float)(sum / Math.Max(1, innerIndex.Length));
                            float meanRest = (float)(sumRest / Math.Max(1, innerIndex.Length));
                            Array.Sort(gaps);
                            float p90 = gaps.Length > 0 ? gaps[Mathf.Min(gaps.Length - 1, (int)(gaps.Length * 0.9f))] : 0f;
                            if (mean > MaxHugGap || p90 > MaxHugP90 || meanRest > MaxRestGap)
                            {
                                closestFloat = Mathf.Min(closestFloat, mean);
                                continue;
                            }
                            float cost = mean / 0.001f + meanRest / 0.002f + Mathf.Abs(sx - sy) / 0.1f + Mathf.Abs(dz) / 0.01f + Mathf.Abs(lift) / 0.02f;
                            found.Add((cost, nearest, new Result
                            {
                                Ok = true, MeanGap = mean, RestGap = meanRest,
                                Fit = new AccessoryFit(new Vector3(0f, lift, dz), Vector3.zero, new Vector3(sx, sy, sz)),
                            }));
                        }
                    }
                }
            }
            if (found.Count == 0)
            {
                return new Result
                {
                    Ok = false,
                    Reason = anyClear ? $"floats: closest hug {closestFloat * 1000f:0.0} mm, more than {MaxHugGap * 1000f:0} mm"
                        : "touches: no placement clears the skin in every pose",
                };
            }
            found.Sort((a, b) => a.cost.CompareTo(b.cost));
            // The tightest placements first; after a failed exact check, only placements with more room than it.
            float floor = float.MinValue, bestExact = float.MinValue;
            string bestPose = string.Empty;
            int tries = 0;
            foreach (var (_, nearest, result) in found)
            {
                if (nearest <= floor)
                    continue;
                if (++tries > ExactTries)
                    break;
                float clearance = MinClearance(shape, surface, result.Fit, out string pose);
                if (clearance >= Clearance)
                {
                    var ok = result;
                    ok.Clearance = clearance;
                    return ok;
                }
                if (clearance > bestExact)
                {
                    bestExact = clearance;
                    bestPose = pose;
                }
                floor = nearest + 0.0005f;
            }
            return new Result { Ok = false, Reason = $"touches: exact check {bestExact * 1000f:0.0} mm at {bestPose}" };
        }

        // ---------------------------------------------------------------- distance fields of the skin

        private sealed class Voxels
        {
            public Vector3 Origin;
            public float Cell;
            public int Nx;
            public int Ny;
            public int Nz;
            public float[] Distance;

            /// <summary>Trilinear distance (m) to the skin; 0 outside the measured box (never clear).</summary>
            public float Sample(Vector3 p)
            {
                float gx = (p.x - Origin.x) / Cell - 0.5f, gy = (p.y - Origin.y) / Cell - 0.5f, gz = (p.z - Origin.z) / Cell - 0.5f;
                int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy), z0 = Mathf.FloorToInt(gz);
                if (x0 < 0 || y0 < 0 || z0 < 0 || x0 >= Nx - 1 || y0 >= Ny - 1 || z0 >= Nz - 1)
                    return 0f;
                float fx = gx - x0, fy = gy - y0, fz = gz - z0;
                int sy = Nx, sz = Nx * Ny;
                int i = (z0 * Ny + y0) * Nx + x0;
                var d = Distance;
                float c00 = d[i] + (d[i + 1] - d[i]) * fx;
                float c10 = d[i + sy] + (d[i + sy + 1] - d[i + sy]) * fx;
                float c01 = d[i + sz] + (d[i + sz + 1] - d[i + sz]) * fx;
                float c11 = d[i + sz + sy] + (d[i + sz + sy + 1] - d[i + sz + sy]) * fx;
                float c0 = c00 + (c10 - c00) * fy;
                float c1 = c01 + (c11 - c01) * fy;
                return c0 + (c1 - c0) * fz;
            }
        }

        /// <summary>
        /// Voxelizes the skin in a box of socket space: cells whose center is inside (ray parity along y, per pose) or that hold a
        /// surface sample are solid. <paramref name="all"/> takes every pose (null when <paramref name="restOnly"/>), <paramref name="rest"/>
        /// the bind pose only; both become distance fields.
        /// </summary>
        private static void BuildFields(Surface surface, Vector3 min, Vector3 max, bool restOnly, out Voxels all, out Voxels rest)
        {
            var size = Vector3.Max(max - min, Vector3.one * 0.004f);
            float cell = VoxelCell;
            while ((long)(size.x / cell + 2) * (long)(size.y / cell + 2) * (long)(size.z / cell + 2) > MaxCells)
                cell *= 1.25f;
            int nx = Mathf.CeilToInt(size.x / cell) + 1, ny = Mathf.CeilToInt(size.y / cell) + 1, nz = Mathf.CeilToInt(size.z / cell) + 1;
            int n = nx * ny * nz;
            var solidAll = restOnly ? null : new bool[n];
            var solidRest = new bool[n];
            var columns = new List<float>[nx * nz];
            var samples = new List<Vector3>(256);
            var tris = surface.Triangles;
            int poses = restOnly ? 1 : surface.Poses.Count;
            for (int pose = 0; pose < poses; pose++)
            {
                var v = surface.Poses[pose];
                foreach (var list in columns)
                    list?.Clear();
                void Mark(int index)
                {
                    if (pose == 0)
                        solidRest[index] = true;
                    if (solidAll != null)
                        solidAll[index] = true;
                }
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                    var tmin = Vector3.Min(a, Vector3.Min(b, c));
                    var tmax = Vector3.Max(a, Vector3.Max(b, c));
                    if (tmax.x < min.x || tmin.x > max.x || tmax.z < min.z || tmin.z > max.z)
                        continue;
                    // Ray parity: the crossing height of the triangle over each column center it covers.
                    float det = (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
                    if (Mathf.Abs(det) > 1e-14f)
                    {
                        int ix0 = Math.Max(0, Mathf.CeilToInt((tmin.x - min.x) / cell - 0.5f - Jitter));
                        int ix1 = Math.Min(nx - 1, Mathf.FloorToInt((tmax.x - min.x) / cell - 0.5f - Jitter));
                        int iz0 = Math.Max(0, Mathf.CeilToInt((tmin.z - min.z) / cell - 0.5f - Jitter));
                        int iz1 = Math.Min(nz - 1, Mathf.FloorToInt((tmax.z - min.z) / cell - 0.5f - Jitter));
                        for (int iz = iz0; iz <= iz1; iz++)
                        {
                            float pz = min.z + (iz + 0.5f + Jitter) * cell;
                            for (int ix = ix0; ix <= ix1; ix++)
                            {
                                float px = min.x + (ix + 0.5f + Jitter) * cell;
                                float u = ((px - a.x) * (c.z - a.z) - (c.x - a.x) * (pz - a.z)) / det;
                                float w = ((b.x - a.x) * (pz - a.z) - (px - a.x) * (b.z - a.z)) / det;
                                if (u < 0f || w < 0f || u + w > 1f)
                                    continue;
                                int col = iz * nx + ix;
                                (columns[col] ??= new List<float>(4)).Add(a.y + u * (b.y - a.y) + w * (c.y - a.y));
                            }
                        }
                    }
                    // The surface itself.
                    if (tmax.y < min.y || tmin.y > max.y)
                        continue;
                    samples.Clear();
                    Sample(a, b, c, cell * 0.7f, samples);
                    foreach (var p in samples)
                    {
                        int ix = (int)((p.x - min.x) / cell), iy = (int)((p.y - min.y) / cell), iz = (int)((p.z - min.z) / cell);
                        if (p.x < min.x || p.y < min.y || p.z < min.z || ix >= nx || iy >= ny || iz >= nz)
                            continue;
                        Mark((iz * ny + iy) * nx + ix);
                    }
                }
                for (int col = 0; col < columns.Length; col++)
                {
                    var list = columns[col];
                    if (list == null || list.Count < 2)
                        continue;
                    list.Sort();
                    int ix = col % nx, iz = col / nx;
                    for (int k = 0; k + 1 < list.Count; k += 2)
                    {
                        int iy0 = Math.Max(0, Mathf.CeilToInt((list[k] - min.y) / cell - 0.5f));
                        int iy1 = Math.Min(ny - 1, Mathf.FloorToInt((list[k + 1] - min.y) / cell - 0.5f));
                        for (int iy = iy0; iy <= iy1; iy++)
                            Mark((iz * ny + iy) * nx + ix);
                    }
                }
            }
            all = restOnly ? null : DistanceField(solidAll, min, cell, nx, ny, nz);
            rest = DistanceField(solidRest, min, cell, nx, ny, nz);
        }

        /// <summary>Exact Euclidean distance transform (Felzenszwalb and Huttenlocher, three 1D passes) from the solid cells.</summary>
        private static Voxels DistanceField(bool[] solid, Vector3 origin, float cell, int nx, int ny, int nz)
        {
            int n = solid.Length;
            var f = new double[n];
            for (int i = 0; i < n; i++)
                f[i] = solid[i] ? 0.0 : Far;
            int m = Math.Max(nx, Math.Max(ny, nz));
            var line = new double[m];
            var result = new double[m];
            var parabolas = new int[m];
            var bounds = new double[m + 1];
            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                {
                    int start = (z * ny + y) * nx;
                    for (int x = 0; x < nx; x++)
                        line[x] = f[start + x];
                    Edt(line, nx, result, parabolas, bounds);
                    for (int x = 0; x < nx; x++)
                        f[start + x] = result[x];
                }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    for (int y = 0; y < ny; y++)
                        line[y] = f[(z * ny + y) * nx + x];
                    Edt(line, ny, result, parabolas, bounds);
                    for (int y = 0; y < ny; y++)
                        f[(z * ny + y) * nx + x] = result[y];
                }
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    for (int z = 0; z < nz; z++)
                        line[z] = f[(z * ny + y) * nx + x];
                    Edt(line, nz, result, parabolas, bounds);
                    for (int z = 0; z < nz; z++)
                        f[(z * ny + y) * nx + x] = result[z];
                }
            var distance = new float[n];
            for (int i = 0; i < n; i++)
                distance[i] = f[i] >= Far * 0.5 ? 1f : (float)Math.Sqrt(f[i]) * cell;
            return new Voxels { Origin = origin, Cell = cell, Nx = nx, Ny = ny, Nz = nz, Distance = distance };
        }

        /// <summary>1D squared distance transform of a sampled function (lower envelope of parabolas).</summary>
        private static void Edt(double[] f, int n, double[] d, int[] v, double[] z)
        {
            int k = 0;
            v[0] = 0;
            z[0] = double.NegativeInfinity;
            z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s = (f[q] + (double)q * q - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = (f[q] + (double)q * q - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q)
                    k++;
                double dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }
    }
}
