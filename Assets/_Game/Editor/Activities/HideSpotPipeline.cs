using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PocketWilds.Editor.Activities
{
    /// <summary>
    /// T39 « Cache-cache » hiding spots (doc 01 Contenu S-ACT-002; doc Mini-jeux S-ACT-002 « chaque chunk a ses cachettes »,
    /// asset « marqueurs de cachettes dans les chunks (HideSpots) »; queue T39 « cachettes dans tous les chunks Prairie »).
    /// Measures DEF_HideSpots_Grassland (<see cref="HideSpotSet"/>) on the built Grassland chunks: one zone per chunk, its
    /// portrait camera (a 3/4 high view looking north or along the strip, <see cref="GrasslandPipeline.HideZoneFocus"/>), 5 to
    /// 14 spots each (the start chunk at least 4), best first, each beside a decor (flowered bushes, tall grass, small trees,
    /// bushes, rocks, logs, stumps, hay bales, fences), and for every species of the content catalog the ground point where 8,
    /// 15, 20 or 30 % of its silhouette stays visible from the zone camera (its LOD0 in the Eat pose, the largest share over
    /// six frames of the loop, against every triangle of the chunk).
    /// Nothing enters anything (Docs/Design/Collision_Rule.md): every placement, landing and the gathering place keep the body
    /// capsule (the measured BodyShape, a little larger for big individuals) out of the chunk's obstacles, the egg's place, the
    /// water and each other.
    /// What a filmed game shows (round 3): the play area between the HUD's bands shows only the island on every phone from
    /// 9:21 to 9:16 (never its edge or the void beyond it; wider screens zoom in on the 9:16 frame); no spot or landing stands within 1.5 m of the strip's outer edge;
    /// only a spot's own decor hides its animal (no other prop stands before the decor or the animal); the « coucou » landing
    /// is a clear hop (0.7 m from every placement) where every species shows at least 80 %. <see cref="Audit"/> measures the
    /// built set again for the tests and the validator.
    /// Registers the set in the Addressables group Activity_HideAndSeek. Run after « Build Grassland Strip and Hide Decor
    /// (T39) », « Setup Grassland Streaming (T27) » and « Measure Bodies ». Deterministic and idempotent; the log tells, per
    /// chunk, what was found and how well the shares were met (Diagnose(address) traces every decor's verdict). The build
    /// measures on every core and keeps what it measured in Library (HideSpotPipeline.Cache.cs): a zone whose inputs did not
    /// change is read back, a changed species or chunk is measured again; the asset is only written when it changes.
    /// </summary>
    public static partial class HideSpotPipeline
    {
        public const string AssetPath = "Assets/_Game/Content/Activities/DEF_HideSpots_Grassland.asset";
        public const string Group = "Activity_HideAndSeek";
        public const string Address = "activity.hide_seek.spots.grassland";

        /// <summary>
        /// The spots of a zone. Free mode hides 3 to 5 animals, the challenges at most 5 (4 and a free spot in challenge 9);
        /// decoys are optional (the game drops them when room is short), and the stage frames a zone that holds the whole
        /// game, else the largest open one (the game then hides fewer). A real portrait corner of the refuge holds 3 to 7
        /// spots under the capture rules (T39, four measured rounds), so: every zone holds at least 3 (a d1 game, the start
        /// chunk's tutorial included), at least <see cref="GoodZones"/> zones hold <see cref="GoodZoneSpots"/> (a full game
        /// from d3, every challenge), and one zone holds <see cref="DecoySpots"/> (d10's five hidden and a decoy; the search
        /// still aims at <see cref="TargetSpots"/>, both decoys: since the small field mouse and guinea pig (T45), whose
        /// placements spread wider to show their share, the roomiest Prairie zone frames 6). The
        /// economy simulator opens the second Prairie zone (Meadow, 5 spots) within the first hour, before refuge level 4
        /// unlocks Hide and Seek. The stage takes the spots best first (<see cref="HideSpotSet"/>).
        /// </summary>
        public const int MinSpots = 3;
        public const int StartMinSpots = 3;
        public const int TargetSpots = 7;
        public const int DecoySpots = 6;
        public const int GoodZoneSpots = 5;
        public const int GoodZones = 4;
        public const int RoomyZones = 1;
        public const int MaxSpots = 14;
        public const string StartAddress = "chunk.grassland.start";

        /// <summary>The fewest spots a zone keeps (the start chunk's zone more).</summary>
        public static int MinSpotsFor(string address) => address == StartAddress ? StartMinSpots : MinSpots;

        /// <summary>The first spots of a zone, those the stage hides its animals in (the first 5 of the best-first order).</summary>
        public const int FirstSpots = 5;

        /// <summary>At most this share of the levels (species × spots × 4) may be unreachable (the slide meets the next decor).</summary>
        public const float UnreachableLimit = 0.20f;

        /// <summary>Two spots' reference placements stand at least this far apart (m).</summary>
        public const float Spacing = 0.9f;

        /// <summary>A level is met when the measured share is within this of the target (4 points).</summary>
        public const float ShareTolerance = 0.04f;

        /// <summary>The most an animal shows at its most hidden placement (the 8 % level never exceeds it): the first choice.</summary>
        public const float MaxHiddenShare = 0.40f;

        /// <summary>A level is counted as met by the tests when within this of its target (5 points).</summary>
        public const float LevelTolerance = 0.05f;

        /// <summary>
        /// Bodies are kept clear at this scale: the largest size gene (1 + 6 × 0.025, AnimalGenome) fits without the stage's
        /// help; a Giant (× 1.2 more) is pushed out by the stage at run time.
        /// </summary>
        public const float ClearanceScale = 1.15f;

        /// <summary>Free space kept between a body and anything else (HideSeekLayout.BodyGap, the collision rule's contact).</summary>
        public const float BodyGap = 0.02f;

        /// <summary>Free disc around the gathering point for the « all found » arc of up to 8 animals (m), first choice.</summary>
        public const float GatherRadius = 1.2f;

        /// <summary>
        /// The narrowest supported portrait (9:21) and the frame kept clear of the HUD, in normalized screen coordinates
        /// (-1 to 1): the score card covers the top 15 % (up to 0.70), the item buttons the bottom 12 % (down to -0.76), the
        /// sides keep 4 % (HideSeekLayout.HudTopShare, HudBottomShare, SideShare).
        /// </summary>
        public const float NarrowAspect = 9f / 21f;
        public const float WideAspect = 3f / 4f;
        public const float FrameTop = 0.70f;
        public const float FrameBottom = 0.76f;
        public const float FrameHalfWidth = 0.90f;

        /// <summary>
        /// The zone camera before it is aimed (the refuge's: pitch 40°, FOV 30°, 13.8 m; « a little closer »). Its position is
        /// what the shares are measured from: a little lower than the refuge's, because from 36 to 40° a clump of grass or a
        /// bush hides only an animal's lower body (measured: a rabbit shows 60 to 85 % behind GL_Bush_A and 35 to 65 % behind a
        /// scaled T39 clump, a squirrel's tail more; from 28° the clump leaves the rabbit 10 to 35 %); trees' crowns hide from
        /// both. After the spots are chosen the
        /// camera is only turned (pitch and yaw) to frame them, which changes no share, and the field of view is fitted.
        /// The main session's round 2: pitch 24 to 34° (the refuge's own view shows the far fence and the sky at the top, so a
        /// lower game camera keeps its look), field of view up to 50°.
        /// </summary>
        public const float NominalPitch = 24f;
        public const float NominalDistance = 12.5f;
        public const float NominalFov = 50f;

        /// <summary>
        /// The frame the spots (and the decor scout) are chosen in: narrower than the widest fit, so the chosen content stays
        /// compact and the final field of view keeps the frame's top corners on the island.
        /// </summary>
        public const float SelectFov = 40f;
        public const float MinPitch = 24f;
        public const float MaxPitch = 34f;
        public const float MaxYaw = 8f;
        public const float MinFov = 30f;
        public const float MaxFov = 50f;

        /// <summary>Spot candidates and spots stay inside these fractions of the nominal frame (the final fit frames them).</summary>
        private const float SelectHalfWidth = 0.86f;
        private const float SelectTop = 0.66f;
        private const float SelectBottom = 0.72f;

        /// <summary>Spots stay this far inside the chunk (m): the next zone's hedge stands at the seam.</summary>
        private const float ChunkMarginX = 6.2f;

        /// <summary>No spot, landing or gathering within this of the strip's outer edge (m).</summary>
        public const float EdgeClearance = 1.5f;

        /// <summary>The play area's rays meet the ground at least this far inside the island's edge (m).</summary>
        public const float VoidMargin = 0.5f;

        /// <summary>
        /// The phone aspects the play area is checked at (the stage fits the field of view to each): 9:21 to 9:16. Wider
        /// screens keep the 9:16 horizontal extent and zoom in (HideSeekStage.FitFieldOfView), so they show less.
        /// </summary>
        public static readonly float[] CheckedAspects = { 9f / 21f, 9f / 19.5f, 9f / 16f };

        /// <summary>The found animal's landing: at least this much of every species shows there, this far from any placement.</summary>
        public const float LandingVisible = 0.8f;
        public const float LandingDistance = 0.7f;

        /// <summary>
        /// Other props never hide a spot: the visible share with every prop and with the ground and the spot's own decor
        /// alone differ by at most this; the decor itself shows at least <see cref="DecorVisible"/> past the other props.
        /// </summary>
        public const float OtherPropsTolerance = 0.03f;
        public const float DecorVisible = 0.85f;
        private const float ChunkMarginZ = 6.9f;
        private const float EggClearance = 1.3f;
        private const float HeartClearance = 1.5f;
        private const float PathWorn = 0.3f;

        [MenuItem("Pocket Wilds/Activities/Build Hide Spots (T39)", priority = 241)]
        public static void BuildMenu() => Debug.Log("[HideSpotPipeline] " + Build());

        [MenuItem("Pocket Wilds/Activities/Build Hide Spots, Measuring Everything Again (T39)", priority = 243)]
        public static void RebuildMenu() => Debug.Log("[HideSpotPipeline] " + Build(false));

        public static string Build() => Build(true);

        /// <summary>
        /// Builds the set. <paramref name="useCache"/>: read the zones and shares cached in Library (false measures everything
        /// again; the cache is written either way).
        /// </summary>
        public static string Build(bool useCache)
        {
            var watch = Stopwatch.StartNew();
            var report = new StringBuilder();
            var species = LoadSpecies(report);
            if (species.Count == 0)
                return "No species with a visual prefab: run the animal pipeline first. " + report;
            var worlds = LoadZoneWorlds(report);
            double loadSeconds = watch.Elapsed.TotalSeconds;

            var stats = new MeasureStats();
            var entries = MeasureZones(worlds, species, useCache, true, stats);
            var zones = new List<HideSpotSet.Zone>();
            var spots = new List<HideSpotSet.Spot>();
            var placements = new List<Vector3>();
            var measured = new List<float>();
            var lines = new List<string>();
            int hitTotal = 0, entryTotal = 0, unreachableTotal = 0, over40 = 0, overlaps = 0, firstUnreachable = 0, firstEntries = 0;
            foreach (var zone in entries)
            {
                var z = zone.Zone;
                z.firstSpot = spots.Count;
                z.spotCount = zone.Spots.Length;
                zones.Add(z);
                spots.AddRange(zone.Spots);
                placements.AddRange(zone.Placements);
                measured.AddRange(zone.Measured);
                hitTotal += zone.ReachableHits;
                entryTotal += zone.Entries;
                unreachableTotal += zone.Unreachable;
                firstUnreachable += zone.FirstUnreachable;
                firstEntries += zone.FirstEntries;
                over40 += zone.Over40;
                overlaps += zone.Overlaps;
                lines.Add(zone.Line);
            }

            var asset = AssetDatabase.LoadAssetAtPath<HideSpotSet>(AssetPath);
            if (asset == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                asset = ScriptableObject.CreateInstance<HideSpotSet>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            var speciesIds = species.Select(s => s.Id).ToArray();
            bool changed = !SameContent(asset, zones, spots, speciesIds, placements, measured);
            if (changed)
            {
                asset.SetAuthoring(zones.ToArray(), spots.ToArray(), speciesIds, placements.ToArray(), measured.ToArray());
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
            string addressables = EnsureAddressables(asset);

            report.Insert(0, $"{AssetPath}: {zones.Count} zones, {spots.Count} spots, {species.Count} species " +
                             $"({string.Join(", ", species.Select(s => $"{s.Name} {s.Pose}, {s.Height:F2} m"))}); " +
                             $"reachable levels within ±{LevelTolerance * 100f:F0} points: {hitTotal}/{entryTotal - unreachableTotal} " +
                             $"({(entryTotal - unreachableTotal > 0 ? 100f * hitTotal / (entryTotal - unreachableTotal) : 0f):F0} %, target 75 %), " +
                             $"unreachable {unreachableTotal}/{entryTotal} ({(entryTotal > 0 ? 100f * unreachableTotal / entryTotal : 0f):F1} %, at most {UnreachableLimit * 100f:F0} %; " +
                             $"among the first {FirstSpots} spots of each zone {firstUnreachable}/{firstEntries} ({(firstEntries > 0 ? 100f * firstUnreachable / firstEntries : 0f):F1} %)), " +
                             $"zones with {GoodZoneSpots}+ spots: {zones.Count(z => z.spotCount >= GoodZoneSpots)} (at least {GoodZones}), " +
                             $"most hidden above {MaxHiddenShare * 100f:F0} %: {over40}, overlaps: {overlaps} (must be 0); {addressables}; " +
                             $"asset {(changed ? "written" : "unchanged")}; {watch.Elapsed.TotalSeconds:F1} s (load {loadSeconds:F1} s, keys {stats.KeySeconds:F1} s, " +
                             $"{stats.Cached} zones from the cache, {stats.Measured} measured in {stats.MeasureSeconds:F1} s on {Math.Max(1, System.Environment.ProcessorCount - 1)} threads, " +
                             $"shares read back {stats.ShareHits}, measured {stats.ShareMisses}; per zone: thread seconds).\n");
            report.Append(string.Join("\n", lines));
            return report.ToString();
        }

        /// <summary>The Grassland chunks of the chunk set, loaded as the pipeline sees them, each with its neighbours' obstacles.</summary>
        internal static List<ChunkWorld> LoadZoneWorlds(StringBuilder report)
        {
            var set = AssetDatabase.LoadAssetAtPath<WorldChunkSet>(WorldContentPipeline.ChunkSetPath);
            var ids = new HashSet<string>();
            if (set != null)
                for (int i = 0; i < set.Count; i++)
                    ids.Add(set.Get(i).id);
            else
                report.Append("DEF_ChunkSet_Grassland missing: chunk ids taken from WorldContentPipeline. ");

            var worlds = new List<ChunkWorld>();
            foreach (var (address, path, origin, _) in WorldContentPipeline.GrasslandChunks)
            {
                if (set != null && !ids.Contains(address))
                {
                    report.Append($"{address} is not in the chunk set, skipped. ");
                    continue;
                }
                var world = ChunkWorld.Load(address, path, origin);
                if (world == null)
                    report.Append($"{address}: prefab missing ({path}). ");
                else
                    worlds.Add(world);
            }
            foreach (var world in worlds)
                world.AddNeighbourObstacles(worlds);
            return worlds;
        }

        /// <summary>The asset already holds exactly these tables (bit for bit): nothing to write.</summary>
        private static bool SameContent(HideSpotSet asset, List<HideSpotSet.Zone> zones, List<HideSpotSet.Spot> spots, uint[] species,
            List<Vector3> placements, List<float> measured)
        {
            if (asset.ZoneCount != zones.Count || asset.SpotCount != spots.Count || asset.SpeciesCount != species.Length)
                return false;
            for (int i = 0; i < zones.Count; i++)
            {
                var a = asset.GetZone(i);
                var b = zones[i];
                if (a.chunkId != b.chunkId || !Same(a.cameraPosition, b.cameraPosition) || !Same(a.cameraRotation, b.cameraRotation)
                    || !Same(a.fieldOfView, b.fieldOfView) || !Same(a.gatherPoint, b.gatherPoint) || a.firstSpot != b.firstSpot || a.spotCount != b.spotCount)
                    return false;
            }
            for (int i = 0; i < spots.Count; i++)
                if (!Same(asset.GetSpot(i), spots[i]))
                    return false;
            for (int i = 0; i < species.Length; i++)
                if (asset.GetSpecies(i).Value != species[i])
                    return false;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var storedPlacements = (Vector3[])typeof(HideSpotSet).GetField("_placements", flags).GetValue(asset);
            var storedMeasured = (float[])typeof(HideSpotSet).GetField("_measured", flags).GetValue(asset);
            if (storedPlacements.Length != placements.Count || storedMeasured.Length != measured.Count)
                return false;
            for (int i = 0; i < placements.Count; i++)
                if (!Same(storedPlacements[i], placements[i]) || !Same(storedMeasured[i], measured[i]))
                    return false;
            return true;
        }

        /// <summary>Diagnostics: the authored focus of each listed chunk, every decor's verdict, the chosen spots. Writes nothing.</summary>
        /// <summary>One zone's five focuses side by side (spots, frame fit, levels met): why a focus wins.</summary>
        public static string CompareFocuses(string address)
        {
            var report = new StringBuilder();
            var species = LoadSpecies(report);
            var worlds = WorldContentPipeline.GrasslandChunks.Select(c => ChunkWorld.Load(c.address, c.path, c.origin)).Where(w => w != null).ToList();
            foreach (var w in worlds)
                w.AddNeighbourObstacles(worlds);
            var world = worlds.FirstOrDefault(w => w.Address == address);
            if (world == null)
                return "no chunk " + address;
            var hint = GrasslandPipeline.HideZoneFocusFor(world.Address) ?? Vector2.zero;
            foreach (var focus in new[] { hint, hint + new Vector2(0.75f, 0f), hint - new Vector2(0.75f, 0f), hint + new Vector2(0f, 0.75f), hint - new Vector2(0f, 0.75f) })
            {
                ConflictReasons.Clear();
                var trace = new StringBuilder();
                Trace = trace;
                ZoneResult zone;
                try { zone = Attempt(world, species, focus); }
                finally { Trace = null; }
                foreach (var line in trace.ToString().Split('\n'))
                    if (line.Contains(" left ") || line.Contains(" chosen "))
                        report.Append(line).Append('\n');
                report.Append($"focus ({focus.x:F2}, {focus.y:F2}): {zone.Spots.Count} spots of {zone.Candidates}, frame fits {zone.FrameFits}, " +
                              $"hits {zone.HitRatio:P0}, conflicts {string.Join(", ", ConflictReasons.Select(p => $"{p.Key} {p.Value}"))}\n");
            }
            return report.ToString();
        }

        public static string Diagnose(params string[] addresses)
        {
            var report = new StringBuilder();
            var species = LoadSpecies(report);
            var worlds = WorldContentPipeline.GrasslandChunks.Select(c => ChunkWorld.Load(c.address, c.path, c.origin)).Where(w => w != null).ToList();
            foreach (var world in worlds)
                world.AddNeighbourObstacles(worlds);
            foreach (var s in species)
                report.Append($"{s.Name}: {s.Pose}, {s.Vertices.Length} vertices, height {s.Height:F2}, hide body front {s.HideBody.front:F2} back {s.HideBody.back:F2} half width {s.HideBody.halfWidth:F2} height {s.HideBody.height:F2}\n");
            Trace = report;
            try
            {
                foreach (var world in worlds.Where(w => addresses.Length == 0 || addresses.Contains(w.Address)))
                {
                    report.Append($"{world.Address}: {world.Decor.Count} decor, {world.Obstacles.Count} obstacles, {world.Triangles.Count / 3} triangles\n");
                    var focus = GrasslandPipeline.HideZoneFocusFor(world.Address) ?? Vector2.zero;
                    ConflictReasons.Clear();
                    var zone = Attempt(world, species, focus);
                    report.Append(zone.Describe(species, 0) + "\n");
                    report.Append("  conflicts: " + string.Join(", ", ConflictReasons.Select(p => $"{p.Key} {p.Value}")) + "\n");
                }
            }
            finally
            {
                Trace = null;
            }
            return report.ToString();
        }

        /// <summary>The zones of the listed chunks as the build would find them (every focus tried), one line each. Writes nothing.</summary>
        public static string Preview(params string[] addresses)
        {
            var report = new StringBuilder();
            var species = LoadSpecies(report);
            var worlds = WorldContentPipeline.GrasslandChunks.Select(c => ChunkWorld.Load(c.address, c.path, c.origin)).Where(w => w != null).ToList();
            foreach (var world in worlds)
                world.AddNeighbourObstacles(worlds);
            foreach (var world in worlds.Where(w => addresses.Length == 0 || addresses.Contains(w.Address)))
            {
                var watch = Stopwatch.StartNew();
                var zone = BuildZone(world, species);
                report.Append(zone.Describe(species, watch.Elapsed.TotalSeconds)).Append('\n');
            }
            return report.ToString();
        }

        /// <summary>
        /// The built set measured again against its chunks, as a filmed game shows it. Each problem is one line, prefixed by
        /// its kind:
        /// - « void »: the play area on some checked device shows the island's edge or the void.
        /// - « edge »: a placement or landing stands within <see cref="EdgeClearance"/> of the strip's outer edge.
        /// - « landing »: a landing is nearer than <see cref="LandingDistance"/> to a placement, or a species shows less than
        ///   <see cref="LandingVisible"/> there.
        /// - « occlusion »: another prop hides a spot's decor or its animal.
        /// Writes nothing.
        /// </summary>
        public static List<string> Audit()
        {
            var problems = new List<string>();
            var set = AssetDatabase.LoadAssetAtPath<HideSpotSet>(AssetPath);
            if (set == null)
            {
                problems.Add("load: " + AssetPath + " is missing");
                return problems;
            }
            var models = LoadSpecies(new StringBuilder());
            var species = new SpeciesModel[set.SpeciesCount];
            for (int s = 0; s < species.Length; s++)
            {
                species[s] = models.FirstOrDefault(m => m.Id == set.GetSpecies(s).Value);
                if (species[s] == null)
                    problems.Add($"load: species {set.GetSpecies(s).Value} has no model");
            }
            if (problems.Count > 0)
                return problems;
            var worlds = WorldContentPipeline.GrasslandChunks.Select(c => ChunkWorld.Load(c.address, c.path, c.origin)).Where(w => w != null).ToList();
            foreach (var world in worlds)
                world.AddNeighbourObstacles(worlds);
            for (int z = 0; z < set.ZoneCount; z++)
            {
                var zone = set.GetZone(z);
                var world = worlds.FirstOrDefault(w => w.Address == zone.chunkId);
                if (world == null)
                {
                    problems.Add($"load: {zone.chunkId} has no chunk");
                    continue;
                }
                var spots = Enumerable.Range(zone.firstSpot, zone.spotCount).Select(set.GetSpot).ToList();
                var voids = new List<string>();
                if (!PlayAreaClear(world, zone.cameraPosition, zone.cameraRotation, zone.fieldOfView, spots, voids))
                    problems.AddRange(voids.Select(v => "void: " + v));
                var meter = new ShareMeter(zone.cameraPosition, zone.cameraRotation, world.Vertices, world.Triangles);
                var occlusion = new OcclusionMeters(world, zone.cameraPosition, zone.cameraRotation);
                for (int i = zone.firstSpot; i < zone.firstSpot + zone.spotCount; i++)
                {
                    var spot = set.GetSpot(i);
                    string name = $"{zone.chunkId} spot {i - zone.firstSpot} ({spot.decor})";
                    var decor = world.Decor.OrderBy(d => (Flat(d.Center) - Flat(spot.decorCenter)).magnitude).FirstOrDefault();
                    if (decor == null || (Flat(decor.Center) - Flat(spot.decorCenter)).magnitude > 0.05f)
                    {
                        problems.Add($"occlusion: {name}: its decor is not in the chunk");
                        continue;
                    }
                    float shown = occlusion.DecorShown(decor);
                    if (shown < DecorVisible - 0.005f)
                        problems.Add($"occlusion: {name}: another prop hides its decor ({shown * 100f:F0} % shown)");
                    if (!world.OnIsland(spot.landing, EdgeClearance))
                        problems.Add($"edge: {name}: its landing is within {EdgeClearance} m of the strip's edge");
                    var front = Flat(zone.cameraPosition) - Flat(spot.decorCenter);
                    float yawToCamera = Mathf.Atan2(front.x, front.y) * Mathf.Rad2Deg;
                    for (int s = 0; s < species.Length; s++)
                    {
                        float seen = meter.Measure(species[s], spot.landing, yawToCamera, out _, true);
                        if (seen < LandingVisible - 0.02f)
                            problems.Add($"landing: {name}: {species[s].Name} shows {seen * 100f:F0} % at the landing");
                        for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                        {
                            var p = set.Placement(i, s, HideSpotSet.ShareAt(level));
                            if (!world.OnIsland(p, EdgeClearance))
                                problems.Add($"edge: {name}: {species[s].Name} at {HideSpotSet.ShareAt(level) * 100f:F0} % stands within {EdgeClearance} m of the strip's edge");
                            float hop = (Flat(p) - Flat(spot.landing)).magnitude;
                            if (hop < LandingDistance - 0.01f)
                                problems.Add($"landing: {name}: {species[s].Name} at {HideSpotSet.ShareAt(level) * 100f:F0} % is {hop:F2} m from the landing");
                            float alone = occlusion.Alone(decor, species[s], p, spot.facing);
                            float measured = set.MeasuredShare(i, s, level);
                            if (alone - measured > OtherPropsTolerance + 0.005f)
                                problems.Add($"occlusion: {name}: another prop hides {species[s].Name} at {HideSpotSet.ShareAt(level) * 100f:F0} % ({measured * 100f:F0} % instead of {alone * 100f:F0} %)");
                        }
                    }
                }
            }
            return problems;
        }

        /// <summary>Group Activity_HideAndSeek (doc Mini-jeux, section 1): loaded at the first game, released on leaving.</summary>
        public static string EnsureAddressables(HideSpotSet asset)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(Group) ?? settings.CreateGroup(Group, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.IncludeInBuild = true;
            bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            var update = group.GetSchema<ContentUpdateGroupSchema>() ?? group.AddSchema<ContentUpdateGroupSchema>();
            update.StaticContent = true;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)), group, false, false);
            entry.address = Address;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return $"Addressables {Group}: {Address}";
        }

        // ---------------------------------------------------------------- decor kinds

        /// <summary>The hide-and-seek kind of a Grassland prop (null: not something to hide behind).</summary>
        public static HideDecorKind? KindOf(string prop)
        {
            if (string.IsNullOrEmpty(prop))
                return null;
            if (prop.StartsWith("PF_", StringComparison.Ordinal))
                prop = prop.Substring(3);
            if (GrasslandPipeline.IsTallGrass(prop))
                return HideDecorKind.TallGrass;
            // The meadow's blossom bush is a flowered bush too (palette.grassland.bush_blossom).
            if (GrasslandPipeline.IsFlowerBush(prop) || prop == "GL_Bush_B")
                return HideDecorKind.FlowerBush;
            if (prop.StartsWith("GL_Bush", StringComparison.Ordinal))
                return HideDecorKind.Bush;
            if (prop.StartsWith("GL_Tree", StringComparison.Ordinal))
                return HideDecorKind.SmallTree;
            if (prop.StartsWith("GL_Landmark_OldTree", StringComparison.Ordinal))
                return HideDecorKind.Tree;
            if (prop.StartsWith("GL_Rock", StringComparison.Ordinal))
                return HideDecorKind.Rock;
            if (prop == "GL_Log")
                return HideDecorKind.Log;
            if (prop == "GL_Stump")
                return HideDecorKind.Stump;
            if (prop == "GL_HayBale")
                return HideDecorKind.HayBale;
            if (prop.StartsWith("GL_Fence", StringComparison.Ordinal))
                return HideDecorKind.Fence;
            if (prop == "GL_WellTiny" || prop == "GL_BirdBath" || prop.StartsWith("GL_Landmark_Windmill", StringComparison.Ordinal))
                return HideDecorKind.Other;
            return null; // rim pieces, stepping stones
        }

        // ---------------------------------------------------------------- species

        /// <summary>
        /// One species as measured: its LOD0 in the hiding pose (root space, scale 1), several frames of it, and the bodies
        /// the rules keep clear: the catalog's capsule (standing) and one that also holds every hiding frame.
        /// </summary>
        internal sealed class SpeciesModel
        {
            public uint Id;
            public string Name;
            public string VisualKey;
            public BodyShape Body;
            public BodyShape HideBody;
            public Vector3[][] Frames;
            public Vector3[][] SearchFrames;
            public int[] Triangles;
            public float Height;
            public string Pose;

            /// <summary>The cache's keys (HideSpotPipeline.Cache.cs): what the meter reads of it, and all a zone uses.</summary>
            public Key128 MeasureKey;
            public Key128 Key;

            public Vector3[] Vertices => Frames[0];

            /// <summary>Plan reach of the hiding body in any heading, at the clearance scale.</summary>
            public float Reach => Mathf.Max(HideBody.Reach, Body.Reach) * ClearanceScale;

            /// <summary>
            /// How tall the hidden animal is for the volumes above it: the catalog's standing height (the hint's React lifts
            /// the head in place; the Eat pose is lower), as the scheduler and the tests read the obstacles' height bands.
            /// </summary>
            public float HideHeight => Mathf.Max(HideBody.height, Body.height);
        }

        /// <summary>The biome these spots belong to (the Grassland strip); its species are the ones measured.</summary>
        public const string SpotsBiome = "biome.grassland";

        /// <summary>
        /// Every species of the spots' biome with a visual prefab, in catalog order. T51: the Forest's species (fox, badger...)
        /// are bigger than the Grassland's flower bushes and would strike spots; the Forest is closed to players for now and will
        /// get its own spots; an unmeasured species uses the spots' reference placement in the stage.
        /// </summary>
        internal static List<SpeciesModel> LoadSpecies(StringBuilder report)
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            var result = new List<SpeciesModel>();
            if (database == null || catalog == null)
            {
                report.Append("Content database or visual catalog missing. ");
                return result;
            }
            foreach (var definition in database.species)
            {
                if (definition == null || definition.biomeId != SpotsBiome)
                    continue;
                var entry = catalog.entries.FirstOrDefault(e => e != null && e.visualKey == definition.visualKey);
                if (entry == null || entry.prefab == null)
                {
                    report.Append($"{definition.sourceId}: no visual prefab, not measured (the stage uses the spots' reference placement). ");
                    continue;
                }
                var model = PoseSpecies(entry.prefab, entry.behavior.body);
                if (model == null)
                {
                    report.Append($"{definition.sourceId}: no LOD0 skinned mesh, not measured. ");
                    continue;
                }
                model.Id = ContentId.FromSource(definition.sourceId).Value;
                model.Name = definition.sourceId.Replace("species.", "");
                model.VisualKey = definition.visualKey;
                result.Add(model);
            }
            return result;
        }

        /// <summary>Frames of the hiding pose sampled over its loop: the share kept is the largest of them (conservative).</summary>
        public const int PoseFrames = 6;

        /// <summary>
        /// The species' LOD0 in the pose a hidden animal holds: its Eat loop (head down, nibbling: decision of the main
        /// session, T39), sampled at <see cref="PoseFrames"/> times over the loop (Idle, then the rest pose, when it cannot be
        /// sampled), in the animal's root space at scale 1 (GroomingPipeline's skinning, bones × bind poses).
        /// </summary>
        private static SpeciesModel PoseSpecies(AnimalView prefab, BodyShape body)
        {
            var instance = Object.Instantiate(prefab.gameObject);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator != null)
                    AnimatorUtility.DeoptimizeTransformHierarchy(animator.gameObject);
                var skin = Lod0(instance);
                if (skin == null || skin.sharedMesh == null)
                    return null;
                var rest = Skin(skin);
                float restHeight = rest.Max(v => v.y);
                var restCenter = Centroid(rest);
                var frames = new List<Vector3[]>();
                string pose = "rest";
                var controller = animator != null ? animator.runtimeAnimatorController : null;
                foreach (var (clip, name) in new[] { (EatClip(controller), "eat"), (IdleClip(controller), "idle") })
                {
                    if (clip == null || frames.Count > 0)
                        continue;
                    int samples = name == "eat" ? PoseFrames : 1;
                    for (int k = 0; k < samples; k++)
                    {
                        try
                        {
                            clip.SampleAnimation(animator.gameObject, clip.length * k / samples);
                            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                            var sampled = Skin(skin);
                            float height = sampled.Max(v => v.y);
                            float minY = sampled.Min(v => v.y);
                            var drift = Centroid(sampled) - restCenter;
                            drift.y = 0f;
                            // A sampled frame is kept when it is sane: on the ground, not taller than standing, not carried
                            // away by root motion.
                            if (height > restHeight * 0.3f && height < restHeight * 1.3f && Mathf.Abs(minY) < 0.08f && drift.magnitude < 0.15f)
                                frames.Add(sampled);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[HideSpotPipeline] {prefab.name}: {clip.name} not sampled ({e.Message}).");
                        }
                    }
                    if (frames.Count > 0)
                        pose = name;
                }
                if (frames.Count == 0)
                    frames.Add(rest);

                // The capsule holding the catalog's body and every hiding frame (a lowered head reaches a little forward).
                float front = body.front, back = body.back, side = body.halfWidth, top = 0f;
                foreach (var frame in frames)
                    foreach (var v in frame)
                    {
                        front = Mathf.Max(front, v.z + 0.01f);
                        back = Mathf.Min(back, v.z - 0.01f);
                        side = Mathf.Max(side, Mathf.Abs(v.x) + 0.01f);
                        top = Mathf.Max(top, v.y + 0.01f);
                    }
                var tops = frames.Select(f => f.Max(v => v.y)).ToArray();
                int highest = Array.IndexOf(tops, tops.Max()), lowest = Array.IndexOf(tops, tops.Min());
                return new SpeciesModel
                {
                    Body = body,
                    HideBody = new BodyShape(front, back, side, top),
                    Frames = frames.ToArray(),
                    SearchFrames = highest == lowest ? new[] { frames[highest] } : new[] { frames[highest], frames[lowest] },
                    Triangles = skin.sharedMesh.triangles,
                    Height = tops.Max(),
                    Pose = pose + (frames.Count > 1 ? $" x{frames.Count}" : ""),
                };
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static AnimationClip EatClip(RuntimeAnimatorController controller)
        {
            if (controller == null)
                return null;
            var clips = controller.animationClips;
            return clips.FirstOrDefault(c => c != null && c.name.EndsWith("Eat_Loop", StringComparison.OrdinalIgnoreCase))
                   ?? clips.FirstOrDefault(c => c != null && c.name.IndexOf("Eat", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static Vector3 Centroid(Vector3[] points)
        {
            var sum = Vector3.zero;
            foreach (var p in points)
                sum += p;
            return points.Length > 0 ? sum / points.Length : Vector3.zero;
        }

        private static AnimationClip IdleClip(RuntimeAnimatorController controller)
        {
            if (controller == null)
                return null;
            var clips = controller.animationClips;
            return clips.FirstOrDefault(c => c != null && c.name.Equals("Idle", StringComparison.OrdinalIgnoreCase))
                   ?? clips.FirstOrDefault(c => c != null && c.name.EndsWith("Idle", StringComparison.OrdinalIgnoreCase))
                   ?? clips.FirstOrDefault(c => c != null && c.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0
                                                && c.name.IndexOf("Look", StringComparison.OrdinalIgnoreCase) < 0);
        }

        private static SkinnedMeshRenderer Lod0(GameObject root)
        {
            var group = root.GetComponentInChildren<LODGroup>(true);
            if (group != null && group.lodCount > 0)
                foreach (var r in group.GetLODs()[0].renderers)
                    if (r is SkinnedMeshRenderer skin)
                        return skin;
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>The skinned vertices at the bones' current pose, in the instance's root space.</summary>
        private static Vector3[] Skin(SkinnedMeshRenderer skin)
        {
            var mesh = skin.sharedMesh;
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var bindposes = mesh.bindposes;
            var bones = skin.bones;
            var toRoot = skin.transform.root.worldToLocalMatrix;
            bool skinned = bones != null && bones.Length == bindposes.Length && bones.Length > 0 && bones.All(b => b != null)
                           && weights.Length == vertices.Length;
            var result = new Vector3[vertices.Length];
            if (!skinned)
            {
                var m = toRoot * skin.transform.localToWorldMatrix;
                for (int v = 0; v < vertices.Length; v++)
                    result[v] = m.MultiplyPoint3x4(vertices[v]);
                return result;
            }
            var matrices = bones.Select((b, i) => toRoot * b.localToWorldMatrix * bindposes[i]).ToArray();
            for (int v = 0; v < vertices.Length; v++)
            {
                var w = weights[v];
                var p = vertices[v];
                result[v] = matrices[w.boneIndex0].MultiplyPoint3x4(p) * w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(p) * w.weight1
                            + matrices[w.boneIndex2].MultiplyPoint3x4(p) * w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(p) * w.weight3;
            }
            return result;
        }

        // ---------------------------------------------------------------- the chunk as the pipeline sees it

        /// <summary>One prop of the chunk that something can hide behind.</summary>
        internal sealed class Decor
        {
            public string Prop;
            public HideDecorKind Kind;
            public Vector3 Center;
            public float GroundReach;
            public float Reach;
            public float Height;
            public Vector3[] Corners;
            public Vector2[] GroundHull;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
        }

        /// <summary>A built chunk in its own space: obstacles, ground, renderer triangles, decor.</summary>
        internal sealed class ChunkWorld
        {
            public string Address;
            public string Name;
            public Vector3 Origin;
            public List<Obstacle> Obstacles = new List<Obstacle>();
            public GroundHeightField Field;

            /// <summary>The cache's keys of its triangles and of its ground (HideSpotPipeline.Cache.cs).</summary>
            public Key128 GeometryKey;
            public Key128 GroundKey;
            public Vector3? Egg;
            public Vector3 Heart;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
            public readonly List<Decor> Decor = new List<Decor>();
            private Vector3[] _groundVertices = Array.Empty<Vector3>();
            private int[] _groundTriangles = Array.Empty<int>();
            private Color32[] _groundColors = Array.Empty<Color32>();
            private List<int>[,] _groundCells;
            private Obstacle[] _own = Array.Empty<Obstacle>();

            public static ChunkWorld Load(string address, string path, Vector3 origin)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var chunk = prefab != null ? prefab.GetComponent<WorldChunk>() : null;
                if (chunk == null)
                    return null;
                var world = new ChunkWorld
                {
                    Address = address, Name = prefab.name.Replace("PF_Chunk_Grassland_", ""), Origin = origin, Field = chunk.GroundField,
                    Heart = chunk.LocalWanderArea.Center,
                };
                var own = new Obstacle[chunk.ObstacleCount];
                for (int i = 0; i < own.Length; i++)
                    own[i] = chunk.GetObstacle(i);
                world._own = own;
                world.Obstacles.AddRange(own);
                if (chunk.EggSpawn != null)
                {
                    world.Egg = chunk.EggSpawn.localPosition;
                    var egg = new Vector2(world.Egg.Value.x, world.Egg.Value.z);
                    world.Obstacles.Add(new Obstacle(egg, egg, RefugeController.EggSpotRadius, world.Egg.Value.y, -0.5f, 1f, ObstacleKind.Solid));
                }

                // Renderer triangles (LOD0 only; the water is a surface, never in front of anything above it).
                var root = prefab.transform;
                var skip = new HashSet<Renderer>();
                foreach (var group in prefab.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = group.GetLODs();
                    for (int l = 1; l < lods.Length; l++)
                        foreach (var r in lods[l].renderers)
                            if (r != null)
                                skip.Add(r);
                }
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (skip.Contains(renderer) || renderer.name == "Water")
                        continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    var mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null)
                        continue;
                    var m = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    int start = world.Vertices.Count;
                    foreach (var v in mesh.vertices)
                        world.Vertices.Add(m.MultiplyPoint3x4(v));
                    foreach (int t in mesh.triangles)
                        world.Triangles.Add(start + t);
                    if (renderer.name == "Ground")
                        world.SetGround(mesh, m);
                }

                // Decor: the props' measured volumes (CollisionProxies, as their obstacles).
                var props = root.Find("Props");
                if (props != null)
                {
                    foreach (Transform group in props)
                    {
                        foreach (Transform instance in group)
                        {
                            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                            if (source == null)
                                continue;
                            var kind = KindOf(source.name);
                            if (kind == null)
                            {
                                // Not a hiding place, still a prop that may stand between the camera and a spot.
                                var vertices = new List<Vector3>();
                                var triangles = new List<int>();
                                AddInstanceTriangles(root, instance, skip, vertices, triangles);
                                world.OtherProps.Add((vertices, triangles));
                                continue;
                            }
                            var placed = CollisionProxies.Place(CollisionProxies.Measure(source), instance.localPosition,
                                instance.localEulerAngles.y, instance.localScale.x);
                            if (placed.Bands.Count == 0)
                                continue;
                            var center = instance.localPosition;
                            var flat = new Vector2(center.x, center.z);
                            var ground = placed.Ground ?? placed.Bands[0];
                            var corners = new List<Vector3>();
                            foreach (var band in placed.Bands)
                                foreach (var p in band.Hull)
                                {
                                    corners.Add(new Vector3(p.x, band.Bottom, p.y));
                                    corners.Add(new Vector3(p.x, band.Top, p.y));
                                }
                            var decor = new Decor
                            {
                                Prop = source.name.Substring(3),
                                Kind = kind.Value,
                                Center = center,
                                GroundReach = ground.Hull.Max(p => (p - flat).magnitude),
                                Reach = placed.Bands.Max(b => b.Hull.Max(p => (p - flat).magnitude)),
                                Height = placed.Bands.Max(b => b.Top) - center.y,
                                Corners = corners.ToArray(),
                                GroundHull = ground.Hull,
                            };
                            // Its own triangles: whether it covers another spot's animal on the screen.
                            AddInstanceTriangles(root, instance, skip, decor.Vertices, decor.Triangles);
                            world.Decor.Add(decor);
                        }
                    }
                }
                return world;
            }

            /// <summary>
            /// A prop instance's LOD0 triangles in chunk space. The T39 decor is shape-only (drawn by the chunk's merged
            /// renderers): its mesh filter gives them.
            /// </summary>
            private static void AddInstanceTriangles(Transform root, Transform instance, HashSet<Renderer> skip, List<Vector3> vertices, List<int> triangles)
            {
                var filters = instance.GetComponentsInChildren<MeshRenderer>(true).Where(r => !skip.Contains(r))
                    .Select(r => r.GetComponent<MeshFilter>()).ToList();
                if (instance.GetComponentInChildren<Renderer>(true) == null)
                    filters.AddRange(instance.GetComponentsInChildren<MeshFilter>(true));
                foreach (var filter in filters)
                {
                    if (filter == null || filter.sharedMesh == null)
                        continue;
                    var m = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    int start = vertices.Count;
                    foreach (var v in filter.sharedMesh.vertices)
                        vertices.Add(m.MultiplyPoint3x4(v));
                    foreach (int t in filter.sharedMesh.triangles)
                        triangles.Add(start + t);
                }
            }

            /// <summary>The other chunks' obstacles near the seams, moved into this chunk's space.</summary>
            public void AddNeighbourObstacles(List<ChunkWorld> worlds)
            {
                _strip = worlds;
                foreach (var other in worlds)
                {
                    if (other == this || Mathf.Abs(other.Origin.x - Origin.x) > 14.5f || Mathf.Abs(other.Origin.z - Origin.z) > 14.5f)
                        continue;
                    var shift = other.Origin - Origin;
                    foreach (var o in other._own)
                    {
                        var moved = o.Translated(shift);
                        if (Mathf.Abs(moved.Center.x) < 9f && Mathf.Abs(moved.Center.y) < 9f)
                            Obstacles.Add(moved);
                    }
                }
            }

            private void SetGround(Mesh mesh, Matrix4x4 m)
            {
                _groundVertices = mesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
                _groundTriangles = mesh.triangles;
                _groundColors = mesh.colors32;
                _groundCells = new List<int>[16, 16];
                for (int t = 0; t < _groundTriangles.Length; t += 3)
                {
                    var a = _groundVertices[_groundTriangles[t]];
                    var b = _groundVertices[_groundTriangles[t + 1]];
                    var c = _groundVertices[_groundTriangles[t + 2]];
                    if (Mathf.Abs(Vector3.Cross(b - a, c - a).normalized.y) < 0.5f)
                        continue;
                    int x0 = Cell(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Cell(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                    int z0 = Cell(Mathf.Min(a.z, Mathf.Min(b.z, c.z))), z1 = Cell(Mathf.Max(a.z, Mathf.Max(b.z, c.z)));
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                            (_groundCells[x, z] ??= new List<int>()).Add(t);
                }
            }

            private static int Cell(float v) => Mathf.Clamp(Mathf.FloorToInt(v + 8f), 0, 15);

            /// <summary>The whole strip (set with the neighbours' obstacles): the island's ground for the edge and void rules.</summary>
            private List<ChunkWorld> _strip;

            /// <summary>Some top-facing ground triangle lies under this chunk-space point (water beds included).</summary>
            public bool GroundCovers(float x, float z)
            {
                var list = _groundCells?[Cell(x), Cell(z)];
                if (list == null || x < -7.6f || x > 7.6f || z < -7.6f || z > 7.6f)
                    return false;
                foreach (int t in list)
                {
                    var a = _groundVertices[_groundTriangles[t]];
                    var b = _groundVertices[_groundTriangles[t + 1]];
                    var c = _groundVertices[_groundTriangles[t + 2]];
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 1e-9f)
                        continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                    if (u >= -1e-4f && v >= -1e-4f && 1f - u - v >= -1e-4f)
                        return true;
                }
                return false;
            }

            /// <summary>The strip's ground (any chunk) under this world point.</summary>
            private bool StripGround(float worldX, float worldZ)
            {
                if (_strip == null)
                    return GroundCovers(worldX - Origin.x, worldZ - Origin.z);
                foreach (var w in _strip)
                    if (Mathf.Abs(worldX - w.Origin.x) <= 7.6f && Mathf.Abs(worldZ - w.Origin.z) <= 7.6f
                        && w.GroundCovers(worldX - w.Origin.x, worldZ - w.Origin.z))
                        return true;
                return false;
            }

            /// <summary>
            /// The chunk-space point is on the island at least <paramref name="margin"/> from its outer edge (the strip's back and
            /// front edges and its two rounded ends; the seams between chunks are no edge): ground all around it.
            /// </summary>
            public bool OnIsland(Vector3 p, float margin)
            {
                float x = p.x + Origin.x, z = p.z + Origin.z;
                if (!StripGround(x, z))
                    return false;
                if (margin <= 0f)
                    return true;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                    if (!StripGround(x + cos * margin, z + sin * margin) || !StripGround(x + cos * margin * 0.5f, z + sin * margin * 0.5f))
                        return false;
                }
                return true;
            }

            /// <summary>The ground triangles (chunk space), for meters that leave out every prop.</summary>
            public Vector3[] GroundVertices => _groundVertices;
            public int[] GroundTriangles => _groundTriangles;
            public Color32[] GroundColors => _groundColors;

            /// <summary>The chunks whose ground makes the island (null until <see cref="AddNeighbourObstacles"/>).</summary>
            public List<ChunkWorld> Strip => _strip;

            /// <summary>Every prop instance of the chunk that is not a decor of the zone (fences, the well, the windmill...).</summary>
            public readonly List<(List<Vector3> vertices, List<int> triangles)> OtherProps = new List<(List<Vector3>, List<int>)>();

            /// <summary>Height of the ground's top surface (the island's top, not its sides); false off the ground.</summary>
            public bool TopSurface(float x, float z, out float height)
            {
                height = float.MinValue;
                var list = _groundCells?[Cell(x), Cell(z)];
                if (list == null)
                    return false;
                bool found = false;
                foreach (int t in list)
                {
                    var a = _groundVertices[_groundTriangles[t]];
                    var b = _groundVertices[_groundTriangles[t + 1]];
                    var c = _groundVertices[_groundTriangles[t + 2]];
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 1e-9f)
                        continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                    float w = 1f - u - v;
                    if (u < -1e-4f || v < -1e-4f || w < -1e-4f)
                        continue;
                    float h = u * a.y + v * b.y + w * c.y;
                    if (h > height)
                        height = h;
                    found = true;
                }
                return found && height > -0.3f;
            }

            /// <summary>The worn path's paint (ground vertex color B) at a point, nearest vertex (GrasslandPipeline.WornMask).</summary>
            public float Worn(float x, float z)
            {
                if (_groundColors.Length == 0)
                    return 0f;
                float best = float.MaxValue;
                byte worn = 0;
                for (int i = 0; i < _groundVertices.Length && i < _groundColors.Length; i++)
                {
                    var v = _groundVertices[i];
                    if (v.y < -0.3f)
                        continue;
                    float d = (v.x - x) * (v.x - x) + (v.z - z) * (v.z - z);
                    if (d < best)
                    {
                        best = d;
                        worn = _groundColors[i].b;
                    }
                }
                return worn / 255f;
            }

            public float GroundY(float x, float z) => Field != null && Field.IsValid ? Field.Sample(x, z) : 0f;

            /// <summary>
            /// Where an animal may stand to hide: inside the chunk (away from the seams' hedge), on the ground's top, off the
            /// worn path (« jamais sur les chemins »), off the egg's place and the walk area's heart.
            /// </summary>
            public bool Allowed(Vector3 p, float reach, bool hiding)
            {
                // A hidden animal keeps away from the seams (the next zone's hedge); a landing or the gathering only stays
                // on this chunk's ground.
                if (Mathf.Abs(p.x) > (hiding ? ChunkMarginX : 6.6f - reach) || Mathf.Abs(p.z) > (hiding ? ChunkMarginZ : 6.95f - reach))
                    return false;
                // Never by the strip's outer edge (the island's rim and the void beyond it must stay out of the frame).
                if (!OnIsland(p, EdgeClearance))
                    return false;
                if (Egg.HasValue && Flat(p - Egg.Value).magnitude < EggClearance + reach)
                    return false;
                for (int k = 0; k < 5; k++)
                {
                    var o = k == 0 ? Vector2.zero : new Vector2(k == 1 ? reach : k == 2 ? -reach : 0f, k == 3 ? reach : k == 4 ? -reach : 0f);
                    if (!TopSurface(p.x + o.x, p.z + o.y, out _))
                        return false;
                }
                if (!hiding)
                    return true;
                if (Flat(p - Heart).magnitude < HeartClearance)
                    return false;
                return Worn(p.x, p.z) <= PathWorn;
            }

            /// <summary>Free space between a body capsule and the obstacles it could touch at its height (negative: inside).</summary>
            public float Clearance(BodyShape body, Vector3 position, float yaw, float scale, float height = -1f)
            {
                body.Segment(position, yaw, scale, out var a, out var b);
                float radius = body.halfWidth * scale;
                if (height < 0f)
                    height = body.height * scale;
                float best = float.MaxValue;
                foreach (var o in Obstacles)
                {
                    if (!o.BlocksWalker(position.y, height))
                        continue;
                    var c = o.Center;
                    float far = (CollisionMath.Flat(position) - c).magnitude - o.Reach - Mathf.Max(body.front, -body.back) * scale;
                    if (far > best)
                        continue;
                    best = Mathf.Min(best, CollisionMath.Gap(a, b, radius, o));
                }
                return best;
            }

            /// <summary>Diagnostics: the obstacle a body enters most deeply.</summary>
            public string Blocker(BodyShape body, Vector3 position, float yaw, float scale, float height)
            {
                body.Segment(position, yaw, scale, out var a, out var b);
                float best = float.MaxValue;
                string which = "nothing";
                foreach (var o in Obstacles)
                {
                    if (!o.BlocksWalker(position.y, height))
                        continue;
                    float gap = CollisionMath.Gap(a, b, body.halfWidth * scale, o);
                    if (gap < best)
                    {
                        best = gap;
                        which = $"{o.kind} ({o.Center.x:F2}, {o.Center.y:F2}) r {o.radius:F2} band {o.bottom:F2}-{o.top:F2} base {o.baseY:F2} gap {gap:F2}";
                    }
                }
                return which;
            }

            /// <summary>
            /// <see cref="DiscClearance"/> below 0 (the disc enters something), answered at the first such obstacle, past the
            /// ones whose bounding disc stands clearly apart (their gap is positive whatever the rounding).
            /// </summary>
            public bool DiscOverlaps(Vector3 position, float radius, float height)
            {
                var p = CollisionMath.Flat(position);
                foreach (var o in Obstacles)
                {
                    if (!o.BlocksWalker(position.y, height))
                        continue;
                    float far = o.Reach + radius + 0.01f;
                    if ((p - o.Center).sqrMagnitude > far * far)
                        continue;
                    if (Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(p, o.a, o.b)) - o.radius - radius < 0f)
                        return true;
                }
                return false;
            }

            /// <summary>Free space around a disc on the ground (an animal in any heading, the gathering place).</summary>
            public float DiscClearance(Vector3 position, float radius, float height)
            {
                var p = CollisionMath.Flat(position);
                float best = float.MaxValue;
                foreach (var o in Obstacles)
                {
                    if (!o.BlocksWalker(position.y, height))
                        continue;
                    best = Mathf.Min(best, Mathf.Sqrt(CollisionMath.PointSegmentDistanceSq(p, o.a, o.b)) - o.radius - radius);
                }
                return best;
            }
        }

        internal static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
    }
}
