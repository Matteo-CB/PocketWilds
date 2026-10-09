using System;
using System.Collections.Generic;
using System.Linq;
using PocketWilds.Editor.Environment;
using PocketWilds.Presentation;
using UnityEngine;

namespace PocketWilds.Editor.Activities
{
    public static partial class HideSpotPipeline
    {
        /// <summary>The slide's step and reach (m), the search behind a decor (m), the facings tried (degrees toward the camera).</summary>
        private const float SlideStep = 0.08f;
        private const float SlideMax = 2.2f;
        private const float SlideStopShare = 0.36f;
        private const float BehindStep = 0.15f;
        private const float BehindMax = 2.6f;
        private static readonly float[] Facings = { 0f, 40f, 80f };
        private const int Bisections = 4;


        /// <summary>One measured spot candidate (and, once chosen, a spot).</summary>
        internal sealed class SpotResult
        {
            public HideSpotSet.Spot Spot;
            public Decor Decor;
            public int Side;
            public float Facing;
            public Vector2 Behind;
            public Vector2 Slide;
            public float Distance;
            public Vector3[,] Placements;
            public float[,] Measured;
            public bool[,] Unreachable;
            public float[] MostHidden;
            public Vector3 Reference;
            public Vector3 Landing;
            public int LandingBlocked;
            public List<(Vector3 point, int blocked)> Landings = new List<(Vector3, int)>();
            public Vector4 AnimalRect;
            public Vector4 DecorRect;
            public int Hits;
            public float Error;
            public float Worst;
            public bool Theme;

            /// <summary>0: every species hides to 15 % at its most hidden; 1: to <see cref="MaxHiddenShare"/>.</summary>
            public int Tier => Worst <= 0.15f ? 0 : 1;
        }

        internal sealed class ZoneResult
        {
            public string Address;
            public string Name;
            public Vector2 Focus;
            public HideSpotSet.Zone Zone;
            public List<SpotResult> Spots = new List<SpotResult>();
            public int Candidates;
            public int Hits;
            public int Entries;
            public int Unreachable;
            public int FirstUnreachable;
            public int FirstEntries;
            public int ReachableHits;
            public int Over40;
            public int Overlaps;
            public float GatherRadiusFound;
            public bool FrameFits = true;
            public string Notes = "";

            public float HitRatio => Entries - Unreachable > 0 ? ReachableHits / (float)(Entries - Unreachable) : 0f;
            public float UnreachableRatio => Entries > 0 ? Unreachable / (float)Entries : 0f;

            public string Describe(List<SpeciesModel> species, double seconds)
            {
                var z = Zone;
                var e = z.cameraRotation.eulerAngles;
                float pitch = e.x > 180f ? e.x - 360f : e.x, yaw = e.y > 180f ? e.y - 360f : e.y;
                var kinds = Spots.GroupBy(s => s.Spot.decor).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}");
                var parts = new List<string>();
                for (int sp = 0; sp < species.Count; sp++)
                {
                    var errors = new List<float>();
                    int unreachable = 0;
                    foreach (var s in Spots)
                    {
                        for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                        {
                            float error = Mathf.Abs(s.Measured[sp, level] - HideSpotSet.ShareAt(level)) * 100f;
                            errors.Add(error);
                            if (s.Unreachable[sp, level])
                                unreachable++;
                        }
                    }
                    parts.Add(errors.Count == 0 ? $"{species[sp].Name} -"
                        : $"{species[sp].Name} error min {errors.Min():F1} mean {errors.Average():F1} max {errors.Max():F1} pts, unreachable {unreachable}");
                }
                return $"{Address} ({Name}): focus ({Focus.x:F1}, {Focus.y:F1}), camera pitch {pitch:F1} yaw {yaw:F1} fov {z.fieldOfView:F1}" +
                       (FrameFits ? "" : " (content wider than the 9:21 frame at the widest field of view!)") +
                       $", {Spots.Count} spots of {Candidates} candidates [{string.Join(", ", kinds)}]; {string.Join("; ", parts)}; " +
                       $"within ±{LevelTolerance * 100f:F0} of the reachable: {ReachableHits}/{Entries - Unreachable} ({HitRatio * 100f:F0} %), unreachable {Unreachable}/{Entries} ({UnreachableRatio * 100f:F0} %, first {FirstSpots} spots {FirstUnreachable}/{FirstEntries} ({(FirstEntries > 0 ? 100f * FirstUnreachable / FirstEntries : 0f):F0} %)); most hidden above {MaxHiddenShare * 100f:F0} %: {Over40}; " +
                       $"overlaps {Overlaps}; landings with a blocked hop from some level {Spots.Sum(s => s.LandingBlocked > 0 ? 1 : 0)}; " +
                       $"gather ({z.gatherPoint.x:F2}, {z.gatherPoint.z:F2}) free {GatherRadiusFound:F1} m{Notes}; {seconds:F1} s";
            }
        }

        // ---------------------------------------------------------------- zone

        /// <summary>The zone of one chunk: the authored focus first, nearby focuses when it falls short.</summary>
        /// <summary>Diagnostics (null in a build): why each decor gave a spot or not.</summary>
        internal static System.Text.StringBuilder Trace;

        internal static ZoneResult BuildZone(ChunkWorld world, List<SpeciesModel> species)
        {
            var hint = GrasslandPipeline.HideZoneFocusFor(world.Address) ?? SearchFocus(world);
            var focuses = new[] { hint, hint + new Vector2(0.75f, 0f), hint - new Vector2(0.75f, 0f), hint + new Vector2(0f, 0.75f), hint - new Vector2(0f, 0.75f) };
            ZoneResult best = null;
            foreach (var focus in focuses)
            {
                var attempt = Attempt(world, species, focus);
                if (best == null || Better(attempt, best))
                    best = attempt;
                if (best.Spots.Count >= 12 && best.HitRatio >= 0.85f && best.UnreachableRatio <= 0.1f && best.Over40 == 0)
                    break;
            }
            return best;
        }

        /// <summary>
        /// The better attempt: the one that fits the frame, then more spots up to <see cref="TargetSpots"/> (5 hidden and
        /// decoys), then the shares met and the reachable levels of the first spots.
        /// </summary>
        private static bool Better(ZoneResult a, ZoneResult b)
        {
            if (a.FrameFits != b.FrameFits)
                return a.FrameFits;
            int ca = Mathf.Min(a.Spots.Count, TargetSpots), cb = Mathf.Min(b.Spots.Count, TargetSpots);
            if (ca != cb)
                return ca > cb;
            float FirstReach(ZoneResult r) => r.FirstEntries > 0 ? 1f - r.FirstUnreachable / (float)r.FirstEntries : 0f;
            float sa = a.HitRatio + 0.5f * FirstReach(a) + 0.02f * Mathf.Min(a.Spots.Count, 12);
            float sb = b.HitRatio + 0.5f * FirstReach(b) + 0.02f * Mathf.Min(b.Spots.Count, 12);
            return sa > sb;
        }

        /// <summary>No authored focus: the place in the chunk whose frame holds the most hiding decor (trees count double).</summary>
        private static Vector2 SearchFocus(ChunkWorld world)
        {
            var best = Vector2.zero;
            float bestScore = -1f;
            for (float x = -4.5f; x <= 4.51f; x += 0.5f)
            {
                for (float z = -4f; z <= 4.01f; z += 0.5f)
                {
                    var (position, rotation) = NominalCamera(world, new Vector2(x, z));
                    float score = 0f;
                    foreach (var d in world.Decor)
                    {
                        if (!InNominalFrame(position, rotation, d.Center + Vector3.up * 0.3f, SelectHalfWidth, SelectTop, SelectBottom))
                            continue;
                        score += d.Kind == HideDecorKind.SmallTree || d.Kind == HideDecorKind.Tree ? 2f : d.Height > 0.6f ? 1f : 0.4f;
                    }
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new Vector2(x, z);
                    }
                }
            }
            return best;
        }

        private static (Vector3 position, Quaternion rotation) NominalCamera(ChunkWorld world, Vector2 focus)
        {
            var view = GrasslandPipeline.HideZoneViewFor(world.Address);
            var rotation = Quaternion.Euler(view.elevation, view.yaw, 0f);
            var target = new Vector3(focus.x, world.GroundY(focus.x, focus.y), focus.y);
            return (target - rotation * Vector3.forward * NominalDistance, rotation);
        }

        /// <summary>A point inside the nominal camera's 9:21 frame, between the given top and bottom bands.</summary>
        private static bool InNominalFrame(Vector3 cameraPosition, Quaternion rotation, Vector3 point, float halfWidth, float top, float bottom,
            float fov = NominalFov)
        {
            var d = Quaternion.Inverse(rotation) * (point - cameraPosition);
            if (d.z <= 0.5f)
                return false;
            float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float y = d.y / d.z / tan;
            return Mathf.Abs(d.x / d.z) <= halfWidth * tan * NarrowAspect && y <= top && y >= -bottom;
        }

        private static ZoneResult Attempt(ChunkWorld world, List<SpeciesModel> species, Vector2 focus)
        {
            var result = new ZoneResult { Address = world.Address, Name = world.Name, Focus = focus };
            var (position, rotation) = NominalCamera(world, focus);
            var meter = new ShareMeter(position, rotation, world.Vertices, world.Triangles);
            var occlusion = new OcclusionMeters(world, position, rotation);
            bool InFrame(Vector3 p) => InNominalFrame(position, rotation, p, SelectHalfWidth, SelectTop, SelectBottom, SelectFov);
            // Slides, landings and the gathering may use the widest field of view, a little inside its edges (the final fit
            // frames them with the animals' boxes).
            bool InWholeFrame(Vector3 p) => InNominalFrame(position, rotation, p, 0.84f, 0.62f, 0.68f, MaxFov);
            bool InSlideFrame(Vector3 p) => InNominalFrame(position, rotation, p, 0.86f, 0.64f, 0.70f, MaxFov);

            var candidates = new List<SpotResult>();
            foreach (var decor in world.Decor)
            {
                // The decor and the ground just behind it are in the frame.
                var behind = (Flat(decor.Center) - Flat(position)).normalized;
                var probe = decor.Center + new Vector3(behind.x, 0f, behind.y) * (decor.GroundReach + 0.3f);
                if (!InFrame(decor.Center) || !InFrame(probe))
                {
                    if (Trace != null && InNominalFrame(position, rotation, decor.Center, 1f, 1f, 1f))
                        Trace.Append($"  {decor.Prop} ({decor.Center.x:F1}, {decor.Center.z:F1}): outside the selection frame\n");
                    continue;
                }
                for (int side = 0; side < 2; side++)
                {
                    var candidate = Measure(world, species, meter, occlusion, decor, side, InFrame, InWholeFrame, InSlideFrame);
                    if (candidate != null)
                        candidates.Add(candidate);
                }
            }
            result.Candidates = candidates.Count;

            // Best first: the decor hides every species to 40 %, then the levels met, the Prairie theme, the smaller error.
            candidates.Sort(Order);
            var covers = new CoverMeter(position, rotation, species);
            // Two orders, the larger choice kept: best first, and fewest conflicts first (a spot that blocks many others
            // goes later), so a crowded corner still gives as many spots as it can hold.
            var degree = candidates.ToDictionary(c => c, c => candidates.Count(o => o != c && Conflict(species, covers, o, c, 0.70f, 0.60f, false)));
            var byDegree = candidates.OrderBy(c => c.Tier).ThenBy(c => degree[c]).ThenByDescending(c => c.Hits).ToList();
            var first = Choose(species, covers, candidates);
            var second = Choose(species, covers, byDegree);
            var chosen = result.Spots;
            chosen.AddRange(second.Count > first.Count ? second : first);
            if (Trace != null)
            {
                foreach (var c in candidates)
                {
                    string tag = $"{c.Decor.Prop} ({c.Decor.Center.x:F1}, {c.Decor.Center.z:F1}) side {c.Side} ref ({c.Reference.x:F2}, {c.Reference.z:F2}) hits {c.Hits} worst {c.Worst:F2}";
                    if (chosen.Contains(c))
                    {
                        Trace.Append($"   chosen {tag}\n");
                        continue;
                    }
                    var reasons = new List<string>();
                    foreach (var o in chosen)
                    {
                        ConflictReasons.Clear();
                        if (Conflict(species, covers, o, c, 0.70f, 0.60f))
                            reasons.Add($"{o.Decor.Prop}/{o.Side}: {string.Join(",", ConflictReasons.Keys)}");
                    }
                    Trace.Append($"   left {tag}: {string.Join("; ", reasons)}\n");
                }
            }
            chosen.Sort(Order);
            // Each chosen spot keeps the landing the choice gave it.
            foreach (var s in chosen)
                s.Spot.landing = s.Landing;

            // The gathering place, then the camera's aim and field of view (the measured shares only depend on its position).
            var gather = FindGather(world, species, chosen, InWholeFrame, out float gatherRadius);
            result.GatherRadiusFound = gatherRadius;
            var (pitch, yaw, fov, fits) = AimAndFit(world, position, species, chosen, gather, gatherRadius);
            while (!fits && chosen.Count > 1)
            {
                // Too wide for the narrowest portrait, or the void would show: the spot at the frame's edge (the one that widens
                // the frame most) goes, the weakest first among equals.
                var worst = chosen.OrderByDescending(s => Mathf.Round(EdgeOf(position, Quaternion.Euler(pitch, yaw, 0f), s) * 50f)).ThenBy(s => s.Hits).First();
                chosen.Remove(worst);
                (pitch, yaw, fov, fits) = AimAndFit(world, position, species, chosen, gather, gatherRadius);
            }
            result.FrameFits = fits;

            result.Zone = new HideSpotSet.Zone
            {
                chunkId = world.Address,
                cameraPosition = position,
                cameraRotation = Quaternion.Euler(pitch, yaw, 0f),
                fieldOfView = fov,
                gatherPoint = gather,
            };
            for (int index = 0; index < chosen.Count; index++)
            {
                var s = chosen[index];
                for (int sp = 0; sp < species.Count; sp++)
                {
                    for (int level = 0; level < HideSpotSet.ShareLevels && index < FirstSpots; level++)
                    {
                        result.FirstEntries++;
                        if (s.Unreachable[sp, level])
                            result.FirstUnreachable++;
                    }
                    if (s.MostHidden[sp] > MaxHiddenShare)
                        result.Over40++;
                    for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                    {
                        result.Entries++;
                        bool hit = Mathf.Abs(s.Measured[sp, level] - HideSpotSet.ShareAt(level)) <= LevelTolerance;
                        if (hit)
                            result.Hits++;
                        if (s.Unreachable[sp, level])
                            result.Unreachable++;
                        else if (hit)
                            result.ReachableHits++;
                        if (world.Clearance(species[sp].HideBody, s.Placements[sp, level], s.Facing, 1f, species[sp].Body.height) < 0f)
                            result.Overlaps++;
                    }
                    if (world.DiscClearance(s.Landing, species[sp].Body.Reach, species[sp].Body.height) < 0f)
                        result.Overlaps++;
                }
            }
            if (chosen.Count < MinSpotsFor(world.Address))
                result.Notes += $"; only {chosen.Count} spots (fewer than {MinSpotsFor(world.Address)})";
            return result;
        }

        /// <summary>
        /// Greedy choice in one order, in passes that loosen only the screen rules (two spots beside one patch share a decor
        /// by design; screen overlap and covering loosen, never spacing, clearance or landings). Each candidate takes the
        /// first landing of its list that no chosen animal or landing touches.
        /// </summary>
        private static List<SpotResult> Choose(List<SpeciesModel> species, CoverMeter covers, List<SpotResult> order)
        {
            float[] animalLimits = { 0.25f, 0.45f, 0.60f, 0.70f };
            float[] decorLimits = { 0.20f, 0.35f, 0.50f, 0.60f };
            var chosen = new List<SpotResult>();
            var landings = new Dictionary<SpotResult, (Vector3, int)>();
            for (int pass = 0; pass < animalLimits.Length && chosen.Count < MaxSpots; pass++)
            {
                foreach (var candidate in order)
                {
                    if (chosen.Count >= MaxSpots)
                        break;
                    if (chosen.Contains(candidate))
                        continue;
                    int landing = candidate.Landings.FindIndex(l => chosen.All(s => LandingFree(species, s, l.point)));
                    if (landing < 0)
                    {
                        Why("no free landing");
                        continue;
                    }
                    candidate.Landing = candidate.Landings[landing].point;
                    candidate.LandingBlocked = candidate.Landings[landing].blocked;
                    if (chosen.Any(s => Conflict(species, covers, s, candidate, animalLimits[pass], decorLimits[pass])))
                        continue;
                    chosen.Add(candidate);
                    landings[candidate] = (candidate.Landing, candidate.LandingBlocked);
                }
            }
            // The other order may have moved a candidate's landing: each chosen spot gets back its own.
            foreach (var spot in chosen)
                (spot.Landing, spot.LandingBlocked) = landings[spot];
            return chosen;
        }

        /// <summary>The zone's order: best hiders first (the stage hides its animals in the first ones, decoys in the last).</summary>
        private static int Order(SpotResult a, SpotResult b)
        {
            int c = a.Tier.CompareTo(b.Tier);
            if (c != 0)
                return c;
            c = b.Hits.CompareTo(a.Hits);
            if (c != 0)
                return c;
            c = b.Theme.CompareTo(a.Theme);
            return c != 0 ? c : a.Error.CompareTo(b.Error);
        }

        /// <summary>How far the spot's animal box reaches toward the frame's edge (for dropping spots when too wide).</summary>
        private static float EdgeOf(Vector3 cameraPosition, Quaternion rotation, SpotResult s)
        {
            var d = Quaternion.Inverse(rotation) * (s.Reference - cameraPosition);
            return d.z > 0f ? Mathf.Max(Mathf.Abs(d.x / d.z) / NarrowAspect, Mathf.Abs(d.y / d.z)) : float.MaxValue;
        }

        // ---------------------------------------------------------------- one candidate

        /// <summary>
        /// A spot behind one decor, on one side: the most hidden line behind it (searched straight away from the camera; a
        /// tree's crown hides best a little behind its trunk), the facing that hides every species best, then each species
        /// slides sideways along that line until it peeks out; the 8/15/20/30 % points are found on the slide (bisection on
        /// the measured share). Null when it cannot hide every species to <see cref="MaxHiddenShare"/> or has no landing.
        /// </summary>
        private static SpotResult Measure(ChunkWorld world, List<SpeciesModel> species, ShareMeter meter, OcclusionMeters occlusion, Decor decor,
            int side, Func<Vector3, bool> inFrame, Func<Vector3, bool> wholeFrame, Func<Vector3, bool> slideFrame)
        {
            var center = Flat(decor.Center);
            var behind = (center - Flat(meter.Position)).normalized;
            var slide = side == 0 ? new Vector2(behind.y, -behind.x) : new Vector2(-behind.y, behind.x);
            float maxReach = species.Max(s => s.Reach);

            Vector3 Ground(Vector2 p) => new Vector3(p.x, world.GroundY(p.x, p.y), p.y);
            int failAllowed = 0, failClear = 0, failFrame = 0;
            string firstBlock = null;
            bool Fits(SpeciesModel s, Vector3 p, float yaw, bool sliding = false)
            {
                if (!world.Allowed(p, s.Reach, true)) { failAllowed++; return false; }
                if (world.Clearance(s.HideBody, p, yaw, ClearanceScale, s.HideHeight) < BodyGap)
                {
                    failClear++;
                    if (Trace != null && firstBlock == null)
                        firstBlock = $" first block at ({p.x:F2}, {p.z:F2}) by {world.Blocker(s.HideBody, p, yaw, ClearanceScale, s.HideHeight)}";
                    return false;
                }
                // The most hidden line starts in the selection frame; the slide may run on to the frame the fit allows.
                if (!(sliding ? slideFrame(p) : inFrame(p))) { failFrame++; return false; }
                return true;
            }

            // The most hidden line, its start along the decor and the facing. The line may start toward this side's end of
            // the decor (a wide patch hides two animals, one at each end, 0.9 m apart at least).
            float bestWorst = float.MaxValue, bestDistance = 0f, bestYaw = 0f, bestFacing = 0f, bestLateral = 0f;
            var order = Enumerable.Range(0, species.Count).ToArray();
            float width = decor.GroundHull.Max(h => Vector2.Dot(h - center, slide));
            foreach (float lateral in new[] { 0f, 0.4f * width, 0.75f * width })
            foreach (float facing in Facings)
            {
                float rad = facing * Mathf.Deg2Rad;
                var f = slide * Mathf.Cos(rad) - behind * Mathf.Sin(rad);
                float yaw = Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg;
                float localBest = float.MaxValue, localDistance = 0f;
                // From right behind the decor (its outline's depth along the view: a wide patch is shallow) outward, finely
                // at first.
                var origin = center + slide * lateral;
                float depth = decor.GroundHull.Max(h => Vector2.Dot(h - origin, behind));
                // A spot stands by its decor: only a tree's crown hides what stands well behind it.
                float reach = decor.Kind == HideDecorKind.SmallTree || decor.Kind == HideDecorKind.Tree ? BehindMax : 0.55f;
                for (float r = depth + 0.08f; r <= depth + reach; r += r < depth + 0.6f ? 0.05f : BehindStep)
                {
                    var p = Ground(origin + behind * r);
                    bool fits = true;
                    foreach (var s in species)
                        if (!Fits(s, p, yaw))
                        {
                            fits = false;
                            break;
                        }
                    if (!fits)
                        continue;
                    float worst = 0f;
                    foreach (int i in order)
                    {
                        worst = Mathf.Max(worst, meter.Measure(species[i], p, yaw, out _));
                        if (worst >= localBest)
                            break;
                    }
                    if (worst < localBest)
                    {
                        localBest = worst;
                        localDistance = r;
                    }
                    if (r > localDistance + 0.6f && worst > localBest + 0.25f)
                        break;
                }
                // Toward the end wins ties (within 3 points): the decor's two spots stand apart.
                bool better = localBest < bestWorst - 1e-4f;
                bool tieTowardEnd = lateral > bestLateral && localBest < float.MaxValue && localBest <= bestWorst + 0.03f;
                if (better || tieTowardEnd)
                {
                    bestWorst = localBest;
                    bestDistance = localDistance;
                    bestYaw = yaw;
                    bestFacing = facing;
                    bestLateral = lateral;
                }
            }
            Trace?.Append($"  {decor.Prop} ({decor.Center.x:F1}, {decor.Center.z:F1}) side {side}: worst {(bestWorst > 9f ? -1f : bestWorst):F2} at r {bestDistance:F2} lateral {bestLateral:F2}"
                          + $" facing {bestFacing:F0} (fails allowed {failAllowed}, clear {failClear}, frame {failFrame}){firstBlock}");
            if (bestWorst > MaxHiddenShare)
            {
                Trace?.Append(" -> too visible\n");
                return null;
            }

            var result = new SpotResult
            {
                Decor = decor, Side = side, Facing = bestYaw, Behind = behind, Slide = slide, Distance = bestDistance,
                Placements = new Vector3[species.Count, HideSpotSet.ShareLevels],
                Measured = new float[species.Count, HideSpotSet.ShareLevels],
                Unreachable = new bool[species.Count, HideSpotSet.ShareLevels],
                MostHidden = new float[species.Count],
                Theme = decor.Kind == HideDecorKind.TallGrass || decor.Kind == HideDecorKind.FlowerBush || decor.Kind == HideDecorKind.SmallTree,
            };
            var start = center + slide * bestLateral + behind * bestDistance;
            var rect = new Vector4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
            float error = 0f;
            for (int sp = 0; sp < species.Count; sp++)
            {
                var s = species[sp];
                var ts = new List<float>();
                var shares = new List<float>();
                for (float t = 0f; t <= SlideMax + 1e-4f; t += SlideStep)
                {
                    var p = Ground(start + slide * t);
                    if (!Fits(s, p, bestYaw, true))
                        break;
                    ts.Add(t);
                    shares.Add(meter.Measure(s, p, bestYaw, out _, true));
                    if (shares[shares.Count - 1] >= SlideStopShare)
                        break;
                }
                if (ts.Count == 0)
                {
                    Trace?.Append($" -> no slide for {s.Name}\n");
                    return null;
                }
                result.MostHidden[sp] = shares[0];
                for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                {
                    float target = HideSpotSet.ShareAt(level);
                    float t, share;
                    int k = shares.FindIndex(v => v >= target);
                    if (k == 0)
                    {
                        t = 0f;
                        share = shares[0];
                    }
                    else if (k > 0)
                    {
                        // Bisection between the last sample under the target and the first at or over it.
                        float lo = ts[k - 1], hi = ts[k], shareLo = shares[k - 1], shareHi = shares[k];
                        t = Mathf.Abs(shareLo - target) <= Mathf.Abs(shareHi - target) ? lo : hi;
                        share = t == lo ? shareLo : shareHi;
                        for (int i = 0; i < Bisections; i++)
                        {
                            float mid = (lo + hi) * 0.5f;
                            float m = meter.Measure(s, Ground(start + slide * mid), bestYaw, out _, true);
                            if (Mathf.Abs(m - target) < Mathf.Abs(share - target))
                            {
                                t = mid;
                                share = m;
                            }
                            if (m >= target)
                                hi = mid;
                            else
                                lo = mid;
                        }
                    }
                    else
                    {
                        // Never peeks out that far (the slide ends against something): the most visible point reached.
                        int best = 0;
                        for (int i = 1; i < shares.Count; i++)
                            if (shares[i] > shares[best])
                                best = i;
                        t = ts[best];
                        share = shares[best];
                    }
                    var placement = Ground(start + slide * t);
                    result.Placements[sp, level] = placement;
                    result.Measured[sp, level] = share;
                    // Out of reach: it shows more than the level even fully behind, or never peeks out that far.
                    result.Unreachable[sp, level] = (k == 0 && share > target + LevelTolerance) || (k < 0 && share < target - LevelTolerance);
                    meter.Measure(s, placement, bestYaw, out var box, true);
                    rect = new Vector4(Mathf.Min(rect.x, box.x), Mathf.Min(rect.y, box.y), Mathf.Max(rect.z, box.z), Mathf.Max(rect.w, box.w));
                    float e = Mathf.Abs(share - target);
                    error += e;
                    if (e <= LevelTolerance)
                        result.Hits++;
                }
            }
            result.Worst = result.MostHidden.Max();
            result.Error = error / (species.Count * HideSpotSet.ShareLevels);
            result.AnimalRect = rect;
            result.DecorRect = ProjectRect(meter, decor.Corners);
            int reference = ReferenceSpecies(species);
            result.Reference = result.Placements[reference, 1];
            // The spot's reference placement takes any species' standing body (the tests check it with the largest).
            if (species.Any(sp => world.Clearance(sp.Body, result.Reference, bestYaw, 1f) < BodyGap))
            {
                Trace?.Append(" -> reference placement too tight for the largest body\n");
                return null;
            }

            // The frame that shows this animal never shows the void: past its top, the ground goes on.
            if (!TopsOnIsland(world, meter.Position, species, result))
            {
                Trace?.Append(" -> the frame would show the island's edge behind it\n");
                return null;
            }
            // Its own decor hides it, never another prop in front of it or of the decor.
            string hidden = occlusion.OtherPropHides(decor, species, result);
            if (hidden != null)
            {
                Trace?.Append($" -> {hidden}\n");
                return null;
            }

            if (!FindLanding(world, species, meter, result, StandReach(species), wholeFrame))
            {
                Trace?.Append(" -> no landing\n");
                return null;
            }
            Trace?.Append($" -> hits {result.Hits}, most hidden {string.Join("/", result.MostHidden.Select(v => (v * 100f).ToString("F0")))}\n");

            // The decor as the stage taps it and ripples it: its own plan radius, or as far as the crown that hides the spot.
            float toReference = (Flat(result.Reference) - center).magnitude;
            result.Spot = new HideSpotSet.Spot
            {
                position = result.Reference,
                facing = Mathf.Repeat(bestYaw, 360f),
                landing = result.Landing,
                decorCenter = decor.Center,
                decorRadius = Mathf.Max(decor.GroundReach, Mathf.Min(toReference, decor.Reach)),
                decorHeight = decor.Height,
                decor = decor.Kind,
            };
            return result;
        }

        /// <summary>The spots' reference species: the rabbit (HideSpotSet.Spot.position, « a rabbit at 15 % »), else the first.</summary>
        internal static int ReferenceSpecies(List<SpeciesModel> species)
        {
            int i = species.FindIndex(s => s.Name == "rabbit");
            return i >= 0 ? i : 0;
        }

        private static Vector4 ProjectRect(ShareMeter meter, Vector3[] points)
        {
            var rect = new Vector4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
            foreach (var p in points)
            {
                if (!meter.Project(p, out var t, out _))
                    continue;
                rect = new Vector4(Mathf.Min(rect.x, t.x), Mathf.Min(rect.y, t.y), Mathf.Max(rect.z, t.x), Mathf.Max(rect.w, t.y));
            }
            return rect;
        }

        /// <summary>
        /// Free ground in front of the decor (camera side, toward the slide) where every species lands at its « coucou »:
        /// the body fits in any heading, the animal shows there, and the hop from its placements crosses no volume (HV's hop
        /// is 0.16 m high: it passes over nothing). The hop from the 30 % placements must be free; a blocked hop from a more
        /// hidden placement is counted (the stage then slips down and up instead, never through anything). Keeps every such
        /// point, best first (fewest blocked hops, front-side angles first): the choice waits for the other spots.
        /// </summary>
        private static bool FindLanding(ChunkWorld world, List<SpeciesModel> species, ShareMeter meter, SpotResult spot, float maxReach,
            Func<Vector3, bool> inFrame)
        {
            var center = Flat(spot.Decor.Center);
            var front = -spot.Behind;
            // In front of the decor first (on the camera's side, a little toward where the animal peeks out), then around.
            float[] angles = { 25f, 40f, 10f, 55f, -10f, 70f, -25f, 85f, -40f, 100f, -55f, 115f, -70f, 130f, -90f };
            float[] extra = { 0.05f, 0.2f, 0.35f, 0.55f, 0.8f, 1.1f, 1.4f };
            int failPlace = 0, failFrame = 0, failClear = 0, failHop = 0, failSeen = 0, failNear = 0;
            string landingBlock = "";
            float maxHeight = species.Max(s => s.Body.height);
            int reference = ReferenceSpecies(species);
            float yawToCamera = Mathf.Atan2(front.x, front.y) * Mathf.Rad2Deg;
            var found = new List<(Vector3 point, int blocked, int order)>();
            int order = 0;
            // Around the decor first, then around where the animal peeks out (its 30 % placement: one short hop on, toward
            // the camera or along the slide), for decor whose surroundings are under a crown or crowded.
            var candidates = new List<Vector2>();
            foreach (float angle in angles)
            {
                float rad = angle * Mathf.Deg2Rad;
                var dir = front * Mathf.Cos(rad) + spot.Slide * Mathf.Sin(rad);
                foreach (float e in extra)
                    candidates.Add(center + dir * (spot.Decor.GroundReach + maxReach + BodyGap + e));
            }
            var peek = Flat(spot.Placements[reference, HideSpotSet.ShareLevels - 1]);
            foreach (float radius in new[] { 0.5f, 0.7f, 0.95f, 1.25f })
            {
                foreach (float angle in new[] { 45f, 75f, 20f, 100f, 0f, 130f })
                {
                    float rad = angle * Mathf.Deg2Rad;
                    candidates.Add(peek + (spot.Slide * Mathf.Cos(rad) + front * Mathf.Sin(rad)) * radius);
                }
            }
            foreach (var q2 in candidates)
            {
                {
                    order++;
                    var q = new Vector3(q2.x, world.GroundY(q2.x, q2.y), q2.y);
                    if (!world.Allowed(q, maxReach, false)) { failPlace++; continue; }
                    if (!inFrame(q) || !inFrame(q + Vector3.up * 0.5f)) { failFrame++; continue; }
                    // A clear hop away from where it hid (« coucou »): never back into the decor's shadow.
                    if (!FarFromPlacements(spot, q, LandingDistance)) { failNear++; continue; }
                    if (!PointOnIsland(world, meter.Position, q + Vector3.up * maxHeight)) { failFrame++; continue; }
                    if (world.DiscClearance(q, maxReach, maxHeight) < BodyGap)
                    {
                        failClear++;
                        if (Trace != null && failClear == 1)
                            landingBlock = $" first landing block at ({q.x:F2}, {q.z:F2}) by " +
                                           world.Blocker(new BodyShape(maxReach, -maxReach, maxReach, maxHeight), q, 0f, 1f, maxHeight);
                        continue;
                    }
                    int blocked = 0;
                    bool outerFree = true;
                    for (int sp = 0; sp < species.Count && outerFree; sp++)
                    {
                        for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                        {
                            if (HopFree(world, species[sp], spot.Placements[sp, level], q))
                                continue;
                            blocked++;
                            if (level == HideSpotSet.ShareLevels - 1)
                                outerFree = false;
                        }
                    }
                    if (!outerFree) { failHop++; continue; }
                    // Clearly seen once out: every species shows at least 80 % there.
                    if (species.Any(s => meter.Measure(s, q, yawToCamera, out _, true) < LandingVisible)) { failSeen++; continue; }
                    found.Add((q, blocked, order));
                }
            }
            if (found.Count == 0)
            {
                Trace?.Append($" (landing fails: ground {failPlace}, frame {failFrame}, near {failNear}, volumes {failClear}, hop {failHop}, unseen {failSeen}){landingBlock}");
                return false;
            }
            found.Sort((x, y) => x.blocked != y.blocked ? x.blocked.CompareTo(y.blocked) : x.order.CompareTo(y.order));
            spot.Landings = found.Select(f => (f.point, f.blocked)).ToList();
            spot.Landing = found[0].point;
            spot.LandingBlocked = found[0].blocked;
            return true;
        }

        /// <summary>The hop's track on the ground is free for the body's width (its ends are checked as placements and landing).</summary>
        private static bool HopFree(ChunkWorld world, SpeciesModel s, Vector3 from, Vector3 to)
        {
            float length = (Flat(to) - Flat(from)).magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.05f));
            float radius = s.Body.halfWidth * ClearanceScale, height = s.Body.height;
            for (int i = 1; i < steps; i++)
            {
                var p = Vector3.Lerp(from, to, i / (float)steps);
                p.y = world.GroundY(p.x, p.z);
                if (world.DiscClearance(p, radius, height) < 0f)
                    return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- choosing

        /// <summary>
        /// The landed animal stands (Happy, the catalog's standing body) in a heading the stage chooses: a disc of the largest
        /// standing reach at the clearance scale.
        /// </summary>
        internal static float StandReach(List<SpeciesModel> species) => species.Max(s => s.Body.Reach) * ClearanceScale;

        /// <summary>A landing touches none of a chosen spot's animals (any species, any level) nor its landing.</summary>
        private static bool LandingFree(List<SpeciesModel> species, SpotResult chosen, Vector3 landing)
        {
            float maxReach = StandReach(species);
            if (DiscGap(chosen.Landing, maxReach, landing, maxReach) < BodyGap)
                return false;
            var q = Flat(landing);
            for (int i = 0; i < species.Count; i++)
            {
                var si = species[i];
                for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                {
                    si.Body.Segment(chosen.Placements[i, level], chosen.Facing, ClearanceScale, out var a0, out var a1);
                    if (CollisionMath.Gap(a0, a1, si.Body.halfWidth * ClearanceScale, q, q, maxReach) < BodyGap)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Two spots conflict when their animals could touch (any species at any level, or a landing), stand closer than
        /// <see cref="Spacing"/>, overlap on the screen, or one's decor covers the other's animal (measured: the share of the
        /// animal that decor alone hides from the camera).
        /// </summary>
        internal static readonly Dictionary<string, int> ConflictReasons = new Dictionary<string, int>();
        private static bool Counting = true;

        private static bool Why(string reason)
        {
            if (Trace != null && Counting)
                ConflictReasons[reason] = ConflictReasons.TryGetValue(reason, out int n) ? n + 1 : 1;
            return true;
        }

        private static bool Conflict(List<SpeciesModel> species, CoverMeter covers, SpotResult a, SpotResult b, float animalLimit, float decorLimit,
            bool count = true)
        {
            Counting = count;
            if (a.Decor == b.Decor && a.Side == b.Side)
                return Why("same");
            if ((Flat(a.Reference) - Flat(b.Reference)).magnitude < Spacing)
                return Why("spacing");
            if (RectShare(a.AnimalRect, b.AnimalRect) > animalLimit)
                return Why("screen boxes");
            if (!LandingFree(species, a, b.Landing) || !LandingFree(species, b, a.Landing))
                return Why("landings");
            for (int i = 0; i < species.Count; i++)
            {
                var si = species[i];
                float ra = si.Body.halfWidth * ClearanceScale;
                for (int la = 0; la < HideSpotSet.ShareLevels; la++)
                {
                    si.Body.Segment(a.Placements[i, la], a.Facing, ClearanceScale, out var a0, out var a1);
                    for (int j = 0; j < species.Count; j++)
                    {
                        var sj = species[j];
                        float rb = sj.Body.halfWidth * ClearanceScale;
                        for (int lb = 0; lb < HideSpotSet.ShareLevels; lb++)
                        {
                            sj.Body.Segment(b.Placements[j, lb], b.Facing, ClearanceScale, out var b0, out var b1);
                            if (CollisionMath.Gap(a0, a1, ra, b0, b1, rb) < BodyGap)
                                return Why("bodies");
                        }
                    }
                }
            }
            if (a.Decor != b.Decor && (covers.Covered(a.Decor, b) > decorLimit || covers.Covered(b.Decor, a) > decorLimit))
                return Why("decor covers");
            return false;
        }

        /// <summary>
        /// How much of a spot's animal (the reference species at 15 % and 30 %) one decor alone hides from the zone camera:
        /// « no spot's decor hides another spot ». One small meter per decor, built when first asked; results are cached.
        /// </summary>
        internal sealed class CoverMeter
        {
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly List<SpeciesModel> _species;
            private readonly int _reference;
            private readonly Dictionary<Decor, ShareMeter> _meters = new Dictionary<Decor, ShareMeter>();
            private readonly Dictionary<(Decor, SpotResult), float> _cache = new Dictionary<(Decor, SpotResult), float>();

            public CoverMeter(Vector3 position, Quaternion rotation, List<SpeciesModel> species)
            {
                _position = position;
                _rotation = rotation;
                _species = species;
                _reference = ReferenceSpecies(species);
            }

            public float Covered(Decor decor, SpotResult spot)
            {
                if (_cache.TryGetValue((decor, spot), out float value))
                    return value;
                if (!_meters.TryGetValue(decor, out var meter))
                    _meters[decor] = meter = new ShareMeter(_position, _rotation, decor.Vertices, decor.Triangles);
                var species = _species[_reference];
                value = 0f;
                for (int level = 1; level < HideSpotSet.ShareLevels; level += 2)
                    value = Mathf.Max(value, 1f - meter.Measure(species, spot.Placements[_reference, level], spot.Facing, out _));
                _cache[(decor, spot)] = value;
                return value;
            }
        }

        private static float DiscGap(Vector3 a, float ra, Vector3 b, float rb) => (Flat(a) - Flat(b)).magnitude - ra - rb;

        private static float Area(Vector4 r) => Mathf.Max(0f, r.z - r.x) * Mathf.Max(0f, r.w - r.y);

        private static float Intersection(Vector4 a, Vector4 b) =>
            Mathf.Max(0f, Mathf.Min(a.z, b.z) - Mathf.Max(a.x, b.x)) * Mathf.Max(0f, Mathf.Min(a.w, b.w) - Mathf.Max(a.y, b.y));

        /// <summary>Overlap of two screen boxes, as a share of the smaller.</summary>
        private static float RectShare(Vector4 a, Vector4 b)
        {
            float small = Mathf.Min(Area(a), Area(b));
            return small > 0f ? Intersection(a, b) / small : 0f;
        }

        // ---------------------------------------------------------------- gathering place and camera

        /// <summary>
        /// Free ground in the frame for the « all found » arc of up to 8 animals: a disc (1.2 m, else 1.0 or 0.8 m) clear of
        /// every volume, on the ground, near the frame's lower middle (in front of the spots).
        /// </summary>
        private static Vector3 FindGather(ChunkWorld world, List<SpeciesModel> species, List<SpotResult> spots, Func<Vector3, bool> inFrame,
            out float radius)
        {
            float maxReach = species.Max(s => s.Reach);
            float maxHeight = species.Max(s => s.Body.height);
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var s in spots)
            {
                center += s.Reference;
                count++;
            }
            center = count > 0 ? center / count : Vector3.zero;
            foreach (float r in new[] { GatherRadius, 1.0f, 0.8f })
            {
                float bestScore = float.MaxValue;
                Vector3 best = default;
                for (float x = -6.5f; x <= 6.5f; x += 0.25f)
                {
                    for (float z = -6.5f; z <= 6.5f; z += 0.25f)
                    {
                        var p = new Vector3(x, world.GroundY(x, z), z);
                        // In front of the spots (toward the camera) and in the frame, the whole disc included.
                        float score = (Flat(p) - Flat(center + new Vector3(0f, 0f, -1.2f))).magnitude;
                        if (score >= bestScore)
                            continue;
                        if (!inFrame(p) || !inFrame(p + new Vector3(r, 0f, 0f)) || !inFrame(p - new Vector3(r, 0f, 0f))
                            || !inFrame(p + new Vector3(0f, 0.5f, r)) || !inFrame(p - new Vector3(0f, 0f, r)))
                            continue;
                        if (!world.Allowed(p, r, false) || world.DiscClearance(p, r, maxHeight) < BodyGap)
                            continue;
                        // At the gathering every animal is found: the spots are empty and the landings left, so only the
                        // volumes count.
                        bestScore = score;
                        best = p;
                    }
                }
                if (bestScore < float.MaxValue)
                {
                    radius = r;
                    return best;
                }
            }
            radius = 0f;
            return spots.Count > 0 ? spots[0].Landing : center;
        }

        /// <summary>
        /// Aims the camera (from its measured position) at the middle of everything the game shows, then the narrowest
        /// vertical field of view that holds it at 9:21 inside the HUD's margins (the middle 70 % of the height).
        /// </summary>
        private static (float pitch, float yaw, float fov, bool fits) AimAndFit(ChunkWorld world, Vector3 position, List<SpeciesModel> species,
            List<SpotResult> spots, Vector3 gather, float gatherRadius)
        {
            var points = new List<Vector3>();
            foreach (var s in spots)
            {
                for (int sp = 0; sp < species.Count; sp++)
                {
                    float height = species[sp].Height;
                    float reach = species[sp].Body.Reach;
                    for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                    {
                        var p = s.Placements[sp, level];
                        points.Add(p + new Vector3(-reach, 0f, -reach));
                        points.Add(p + new Vector3(reach, 0f, reach));
                        points.Add(p + new Vector3(-reach, height, reach));
                        points.Add(p + new Vector3(reach, height, -reach));
                    }
                }
                float top = species.Max(x => x.Height);
                points.Add(s.Landing);
                points.Add(s.Landing + Vector3.up * top);
            }
            if (gatherRadius > 0f)
            {
                for (int k = 0; k < 8; k++)
                {
                    var o = new Vector3(Mathf.Cos(k * Mathf.PI / 4f), 0f, Mathf.Sin(k * Mathf.PI / 4f)) * gatherRadius;
                    points.Add(gather + o);
                    points.Add(gather + o + Vector3.up * 0.6f);
                }
            }
            var view = GrasslandPipeline.HideZoneViewFor(world.Address);
            float pitch = Mathf.Clamp(view.elevation, MinPitch, MaxPitch), yaw = view.yaw;
            for (int pass = 0; pass < 3 && points.Count > 0; pass++)
            {
                var inverse = Quaternion.Inverse(Quaternion.Euler(pitch, yaw, 0f));
                float axMin = float.MaxValue, axMax = float.MinValue, ayMin = float.MaxValue, ayMax = float.MinValue;
                foreach (var p in points)
                {
                    var d = inverse * (p - position);
                    float ax = Mathf.Atan2(d.x, d.z), ay = Mathf.Atan2(d.y, d.z);
                    axMin = Mathf.Min(axMin, ax);
                    axMax = Mathf.Max(axMax, ax);
                    ayMin = Mathf.Min(ayMin, ay);
                    ayMax = Mathf.Max(ayMax, ay);
                }
                // The frame between the HUD bands sits a little low on the screen: aim its middle at the content's middle.
                float tanGuess = Mathf.Max(Mathf.Tan(ayMax) / FrameTop, Mathf.Tan(-ayMin) / FrameBottom, 0.2f);
                float bandMiddle = Mathf.Atan((FrameTop - FrameBottom) * 0.5f * tanGuess);
                pitch = Mathf.Clamp(pitch - ((ayMin + ayMax) * 0.5f - bandMiddle) * Mathf.Rad2Deg, MinPitch, MaxPitch);
                yaw = Mathf.Clamp(yaw + (axMin + axMax) * 0.5f * Mathf.Rad2Deg, view.yaw - MaxYaw, view.yaw + MaxYaw);
            }
            float Fit(float p, float y)
            {
                var inv = Quaternion.Inverse(Quaternion.Euler(p, y, 0f));
                float tanHalf = 0f;
                foreach (var point in points)
                {
                    var d = inv * (point - position);
                    if (d.z <= 0.1f)
                        return float.MaxValue;
                    float ty = d.y / d.z;
                    tanHalf = Mathf.Max(tanHalf, ty >= 0f ? ty / FrameTop : -ty / FrameBottom, Mathf.Abs(d.x / d.z) / (FrameHalfWidth * NarrowAspect));
                }
                return 2f * Mathf.Atan(tanHalf) * Mathf.Rad2Deg;
            }

            // Aimed at the content; then, while the play area would show the island's edge or the void beyond it on some
            // device, the camera looks a little lower (and a little to the side), the field of view refitted each time.
            var stored = spots.Select(s => s.Spot).ToList();
            float aimedYaw = yaw;
            foreach (float dy in new[] { 0f, 4f, -4f, 8f, -8f })
            {
                float y = Mathf.Clamp(aimedYaw + dy, view.yaw - MaxYaw, view.yaw + MaxYaw);
                for (float p = pitch; p <= MaxPitch + 1e-3f; p += 0.5f)
                {
                    float fov = Fit(p, y);
                    if (fov > MaxFov + 1e-3f)
                        continue;
                    fov = Mathf.Clamp(fov, MinFov, MaxFov);
                    var why = Trace != null && dy == 0f ? new List<string>() : null;
                    if (PlayAreaClear(world, position, Quaternion.Euler(p, y, 0f), fov, stored, why))
                        return (p, y, fov, true);
                    if (why != null && why.Count > 0)
                        Trace.Append($"   fit {spots.Count} spots, pitch {p:F1} fov {fov:F1}: {why[0]}\n");
                }
            }
            return (pitch, yaw, Mathf.Clamp(Fit(pitch, yaw), MinFov, MaxFov), false);
        }

        /// <summary>
        /// The field of view the stage gives this zone on a device of this aspect: HideSeekStage.FitFieldOfView, rule for rule
        /// (HideSpotTests compares them). Phones (9:21 to 9:16) refit the measured value to hold every spot's decor and landing
        /// between the HUD's bands; wider screens keep the 9:16 horizontal extent and zoom in. Chunk space.
        /// </summary>
        public static float StageFov(Vector3 position, Quaternion rotation, float zoneFov, float aspect, IList<HideSpotSet.Spot> spots)
        {
            if (aspect > HideSeekStage.WidestFittedAspect)
            {
                float phone = StageFov(position, rotation, zoneFov, HideSeekStage.WidestFittedAspect, spots);
                float tan = Mathf.Tan(phone * 0.5f * Mathf.Deg2Rad) * HideSeekStage.WidestFittedAspect / aspect;
                return 2f * Mathf.Atan(tan) * Mathf.Rad2Deg;
            }
            float fov = zoneFov > 1f ? zoneFov : 34f;
            if (aspect < HideSeekLayout.NarrowestAspect)
                fov = 2f * Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * HideSeekLayout.NarrowestAspect / aspect) * Mathf.Rad2Deg;
            if (spots == null || spots.Count == 0)
                return fov;
            var inverse = Quaternion.Inverse(rotation);
            float up = 1f - 2f * HideSeekLayout.HudTopShare, down = 1f - 2f * HideSeekLayout.HudBottomShare;
            float side = (1f - 2f * HideSeekLayout.SideShare) * aspect;
            float need = 0f;
            float Need(Vector3 point)
            {
                var local = inverse * (point - position);
                if (local.z < 0.1f)
                    return 0f;
                float y = local.y / local.z;
                return Mathf.Max(Mathf.Abs(local.x / local.z) / side, y >= 0f ? y / up : -y / down);
            }
            foreach (var spot in spots)
            {
                need = Mathf.Max(need, Need(spot.decorCenter));
                need = Mathf.Max(need, Need(spot.decorCenter + Vector3.up * Mathf.Max(0.2f, spot.decorHeight)));
                need = Mathf.Max(need, Need(spot.landing + Vector3.up * 0.35f));
            }
            if (need <= 0f)
                return fov;
            return Mathf.Clamp(Mathf.Max(2f * Mathf.Atan(need) * Mathf.Rad2Deg, fov * 0.82f), 18f, 70f);
        }

        /// <summary>
        /// The play area (between the HUD's bands, the whole width) on every checked phone shows only the island: each ray
        /// meets the ground at least <see cref="VoidMargin"/> inside its edge, never the void beyond it or the sky. The ground
        /// is taken flat at the chunks' rim height (a hill only stops a ray sooner).
        /// </summary>
        internal static bool PlayAreaClear(ChunkWorld world, Vector3 position, Quaternion rotation, float zoneFov, List<HideSpotSet.Spot> spots,
            List<string> problems = null)
        {
            foreach (float aspect in CheckedAspects)
            {
                float fov = StageFov(position, rotation, zoneFov, aspect, spots);
                float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                foreach (float y in new[] { FrameTop, FrameTop * 0.5f, 0f, -FrameBottom * 0.5f, -FrameBottom })
                {
                    for (int k = -4; k <= 4; k++)
                    {
                        var dir = rotation * new Vector3(k / 4f * tan * aspect, y * tan, 1f);
                        if (dir.y < -1e-3f && world.OnIsland(position + dir * (-position.y / dir.y), VoidMargin))
                            continue;
                        problems?.Add($"{world.Address}: at {aspect:F2} ({fov:F1} degrees) the play area shows the island's edge or the void at screen ({k / 4f:F2}, {y:F2})");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>The ray from the camera through this point meets the ground on the island, inside its edge.</summary>
        private static bool PointOnIsland(ChunkWorld world, Vector3 camera, Vector3 point)
        {
            var dir = point - camera;
            if (dir.y >= -1e-3f)
                return false;
            return world.OnIsland(camera + dir * (-camera.y / dir.y), VoidMargin);
        }

        /// <summary>Every placement's animal (its box as the fit frames it) has the island behind it, as the frame's top will.</summary>
        private static bool TopsOnIsland(ChunkWorld world, Vector3 camera, List<SpeciesModel> species, SpotResult spot)
        {
            for (int sp = 0; sp < species.Count; sp++)
            {
                float height = species[sp].Height, reach = species[sp].Body.Reach;
                for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                {
                    var p = spot.Placements[sp, level];
                    if (!PointOnIsland(world, camera, p + new Vector3(-reach, height, reach)) || !PointOnIsland(world, camera, p + new Vector3(reach, height, -reach))
                        || !PointOnIsland(world, camera, p + new Vector3(reach, height, reach)) || !PointOnIsland(world, camera, p + new Vector3(-reach, height, -reach)))
                        return false;
                }
            }
            return true;
        }

        /// <summary>The landing stands at least this far from every placement of every species (a clear hop out).</summary>
        private static bool FarFromPlacements(SpotResult spot, Vector3 landing, float distance)
        {
            foreach (var p in spot.Placements)
                if ((Flat(p) - Flat(landing)).magnitude < distance)
                    return false;
            return true;
        }

        /// <summary>
        /// « Another prop in front is not the hiding »: per decor, a meter of the ground and that decor alone (what its spot's
        /// share must be) and the decor's own silhouette against every other prop (whether one stands before it). Built
        /// when first asked.
        /// </summary>
        internal sealed class OcclusionMeters
        {
            private readonly ChunkWorld _world;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Dictionary<Decor, ShareMeter> _own = new Dictionary<Decor, ShareMeter>();
            private readonly Dictionary<Decor, float> _shown = new Dictionary<Decor, float>();

            public OcclusionMeters(ChunkWorld world, Vector3 position, Quaternion rotation)
            {
                _world = world;
                _position = position;
                _rotation = rotation;
            }

            /// <summary>Null when only the decor hides the spot's animal; else why not.</summary>
            public string OtherPropHides(Decor decor, List<SpeciesModel> species, SpotResult spot)
            {
                float shown = DecorShown(decor);
                if (shown < DecorVisible)
                    return $"another prop hides its decor ({shown * 100f:F0} % shown)";
                for (int sp = 0; sp < species.Count; sp++)
                {
                    for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                    {
                        float alone = Alone(decor, species[sp], spot.Placements[sp, level], spot.Facing);
                        if (alone - spot.Measured[sp, level] > OtherPropsTolerance)
                            return $"another prop hides {species[sp].Name} at {HideSpotSet.ShareAt(level) * 100f:F0} % ({alone * 100f:F0} % with its decor alone)";
                    }
                }
                return null;
            }

            /// <summary>The visible share with the ground and this decor alone.</summary>
            public float Alone(Decor decor, SpeciesModel species, Vector3 position, float yaw) => Own(decor).Measure(species, position, yaw, out _, true);

            private ShareMeter Own(Decor decor)
            {
                if (_own.TryGetValue(decor, out var meter))
                    return meter;
                var vertices = new List<Vector3>(_world.GroundVertices);
                var triangles = new List<int>(_world.GroundTriangles);
                int start = vertices.Count;
                vertices.AddRange(decor.Vertices);
                triangles.AddRange(decor.Triangles.Select(t => start + t));
                return _own[decor] = new ShareMeter(_position, _rotation, vertices, triangles);
            }

            /// <summary>How much of the decor's own silhouette shows past every other prop (decor or not; the ground left out).</summary>
            public float DecorShown(Decor decor)
            {
                if (_shown.TryGetValue(decor, out float value))
                    return value;
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                void Add(List<Vector3> v, List<int> t)
                {
                    int start = vertices.Count;
                    vertices.AddRange(v);
                    triangles.AddRange(t.Select(i => start + i));
                }
                foreach (var other in _world.Decor)
                    if (other != decor)
                        Add(other.Vertices, other.Triangles);
                foreach (var (v, t) in _world.OtherProps)
                    Add(v, t);
                value = 1f;
                if (decor.Triangles.Count > 0)
                {
                    var meter = new ShareMeter(_position, _rotation, vertices, triangles);
                    var silhouette = new[] { decor.Vertices.Select(v => v - decor.Center).ToArray() };
                    var model = new SpeciesModel
                    {
                        Name = decor.Prop, Frames = silhouette, SearchFrames = silhouette, Triangles = decor.Triangles.ToArray(),
                    };
                    value = meter.Measure(model, decor.Center, 0f, out _, true);
                }
                return _shown[decor] = value;
            }
        }
    }
}
