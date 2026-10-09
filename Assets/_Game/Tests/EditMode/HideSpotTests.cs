using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.Activities;
using PocketWilds.Editor.Animals;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T39 « Cache-cache » hiding spots (doc 01 S-ACT-002; doc Mini-jeux S-ACT-002 « chaque chunk a ses cachettes »; queue
    /// T39 « cachettes dans tous les chunks Prairie »): DEF_HideSpots_Grassland, measured by HideSpotPipeline on the built
    /// chunks. Every Prairie chunk has its zone and 5 to 14 spots (the start chunk at least 4, three zones at least 6: a game
    /// hides 3 to 5 animals, decoys optional, best spots first); nothing enters anything (Docs/Design/Collision_Rule.md);
    /// the spots are apart, in the zone camera's frame clear of the HUD, on the ground; the measured shares meet the levels;
    /// the set is Addressable in Activity_HideAndSeek; the Prairie theme decor stands in every chunk.
    /// </summary>
    public class HideSpotTests
    {
        /// <summary>Contact is allowed, entering is not (the collision rule's 2 mm of rounding).</summary>
        private const float Tolerance = 0.002f;

        /// <summary>
        /// The share gates: the reachable levels within ±5 points for 75 % of (spot, species, level), the unreachable ones
        /// (more than the level shows even fully behind, or the slide never peeks out that far) 20 % at most (round 3; the
        /// share among each zone's first 5 spots is reported); every spot's most hidden placement at 40 % at most for every
        /// species.
        /// </summary>
        private const float ShareHitTarget = 0.75f;
        private const float UnreachableLimit = HideSpotPipeline.UnreachableLimit;
        private const float MostHiddenLimit = 0.40f;

        private HideSpotSet _set;
        private readonly Dictionary<string, WorldChunk> _chunks = new Dictionary<string, WorldChunk>();
        private readonly Dictionary<uint, BodyShape> _bodies = new Dictionary<uint, BodyShape>();

        [OneTimeSetUp]
        public void SetUp()
        {
            _set = AssetDatabase.LoadAssetAtPath<HideSpotSet>(HideSpotPipeline.AssetPath);
            foreach (var entry in WorldContentPipeline.GrasslandChunks)
                _chunks[entry.address] = AssetDatabase.LoadAssetAtPath<WorldChunk>(entry.path);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(AnimalPipeline.ContentDatabasePath);
            var catalog = AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>(AnimalPipeline.VisualCatalogPath);
            foreach (var species in database.species.Where(s => s != null))
                if (catalog.TryGet(species.visualKey, out var visual))
                    _bodies[ContentId.FromSource(species.sourceId).Value] = visual.behavior.body;
        }

        private HideSpotSet Set
        {
            get
            {
                Assert.IsNotNull(_set, HideSpotPipeline.AssetPath + " missing: run Pocket Wilds/Activities/Build Hide Spots (T39).");
                return _set;
            }
        }

        private IEnumerable<(HideSpotSet.Zone zone, WorldChunk chunk)> Zones()
        {
            for (int z = 0; z < Set.ZoneCount; z++)
            {
                var zone = Set.GetZone(z);
                Assert.IsTrue(_chunks.TryGetValue(zone.chunkId, out var chunk) && chunk != null, zone.chunkId + ": no such Grassland chunk.");
                yield return (zone, chunk);
            }
        }

        private BodyShape Body(int species)
        {
            Assert.IsTrue(_bodies.TryGetValue(Set.GetSpecies(species).Value, out var body), $"Species {Set.GetSpecies(species).Value} has no body.");
            return body;
        }

        /// <summary>Free space between a capsule and the chunk's obstacles it could touch (the egg's place included).</summary>
        private static float Clearance(WorldChunk chunk, BodyShape body, Vector3 position, float yaw, float scale)
        {
            body.Segment(position, yaw, scale, out var a, out var b);
            float best = float.MaxValue;
            for (int i = 0; i < chunk.ObstacleCount; i++)
            {
                var o = chunk.GetObstacle(i);
                if (o.BlocksWalker(position.y, body.height * scale))
                    best = Mathf.Min(best, CollisionMath.Gap(a, b, body.halfWidth * scale, o));
            }
            if (chunk.EggSpawn != null)
            {
                var egg = new Vector2(chunk.EggSpawn.localPosition.x, chunk.EggSpawn.localPosition.z);
                best = Mathf.Min(best, CollisionMath.Gap(a, b, body.halfWidth * scale, egg, egg, RefugeController.EggSpotRadius));
            }
            return best;
        }

        [Test]
        public void EveryGrasslandChunk_HasAZone_WithThreeToFourteenSpots()
        {
            foreach (var entry in WorldContentPipeline.GrasslandChunks)
            {
                int index = Set.FindZone(entry.address);
                Assert.GreaterOrEqual(index, 0, entry.address + " has no hiding zone.");
                var zone = Set.GetZone(index);
                Assert.That(zone.spotCount, Is.InRange(HideSpotPipeline.MinSpotsFor(entry.address), HideSpotPipeline.MaxSpots), entry.address);
                Assert.That(zone.firstSpot + zone.spotCount, Is.LessThanOrEqualTo(Set.SpotCount));
                Assert.That(zone.fieldOfView, Is.InRange(HideSpotPipeline.MinFov, HideSpotPipeline.MaxFov), entry.address);
            }
            Assert.AreEqual(WorldContentPipeline.GrasslandChunks.Length, Set.ZoneCount);
        }

        [Test]
        public void StartZone_HoldsATutorialGame_FourZonesHoldAFullGame_OneHoldsDecoys()
        {
            int start = Set.FindZone(HideSpotPipeline.StartAddress);
            Assert.GreaterOrEqual(start, 0, "The start chunk has no hiding zone.");
            Assert.GreaterOrEqual(Set.GetZone(start).spotCount, HideSpotPipeline.StartMinSpots,
                "The first game (d1: 3 animals, no decoy) may play in the start chunk.");
            var counts = Zones().Select(z => $"{z.zone.chunkId} {z.zone.spotCount}").ToList();
            TestContext.WriteLine("Spots per zone: " + string.Join(", ", counts));
            int good = Zones().Count(z => z.zone.spotCount >= HideSpotPipeline.GoodZoneSpots);
            Assert.GreaterOrEqual(good, HideSpotPipeline.GoodZones,
                $"Zones holding a full game ({HideSpotPipeline.GoodZoneSpots} spots or more): " + string.Join(", ", counts));
            int roomy = Zones().Count(z => z.zone.spotCount >= HideSpotPipeline.TargetSpots);
            Assert.GreaterOrEqual(roomy, HideSpotPipeline.RoomyZones,
                $"Zones holding d10 with its decoys ({HideSpotPipeline.TargetSpots} spots): " + string.Join(", ", counts));
            int meadow = Set.FindZone("chunk.grassland.meadow");
            Assert.GreaterOrEqual(meadow, 0);
            Assert.GreaterOrEqual(Set.GetZone(meadow).spotCount, HideSpotPipeline.GoodZoneSpots,
                "The second zone, open before Hide and Seek unlocks, holds a full game.");
        }

        [Test]
        public void EveryCatalogSpecies_IsMeasured()
        {
            foreach (var id in _bodies.Keys)
                Assert.GreaterOrEqual(Set.SpeciesIndex(new ContentId(id)), 0, $"Species {id} has a visual but no row in the spots' table.");
        }

        [Test]
        public void SpotsAndLandings_StayFreeOfEveryObstacle()
        {
            int largest = Enumerable.Range(0, Set.SpeciesCount).OrderByDescending(s => Body(s).Reach).First();
            var problems = new List<string>();
            foreach (var (zone, chunk) in Zones())
            {
                for (int i = zone.firstSpot; i < zone.firstSpot + zone.spotCount; i++)
                {
                    var spot = Set.GetSpot(i);
                    for (int s = 0; s < Set.SpeciesCount; s++)
                    {
                        for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                        {
                            var p = Set.Placement(i, s, HideSpotSet.ShareAt(level));
                            float gap = Clearance(chunk, Body(s), p, spot.facing, 1f);
                            if (gap < -Tolerance)
                                problems.Add($"{zone.chunkId} spot {i - zone.firstSpot} species {s} level {level}: {-gap * 100f:F1} cm inside");
                        }
                    }
                    float reference = Clearance(chunk, Body(largest), spot.position, spot.facing, 1f);
                    if (reference < -Tolerance)
                        problems.Add($"{zone.chunkId} spot {i - zone.firstSpot}: the largest body enters something by {-reference * 100f:F1} cm");
                    // The landing: any heading (a disc of the largest reach).
                    var body = Body(largest);
                    var disc = new BodyShape(body.Reach, -body.Reach, body.Reach, body.height);
                    float landing = Clearance(chunk, disc, spot.landing, 0f, 1f);
                    if (landing < -Tolerance)
                        problems.Add($"{zone.chunkId} spot {i - zone.firstSpot}: landing {-landing * 100f:F1} cm inside");
                }
                var gather = new BodyShape(0.3f, -0.3f, 0.3f, 0.7f);
                if (Clearance(chunk, gather, zone.gatherPoint, 0f, 1f) < -Tolerance)
                    problems.Add($"{zone.chunkId}: the gathering point stands inside something");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Spots_StandApart_AndTheirAnimalsNeverTouch()
        {
            foreach (var (zone, _) in Zones())
            {
                for (int i = zone.firstSpot; i < zone.firstSpot + zone.spotCount; i++)
                {
                    for (int j = i + 1; j < zone.firstSpot + zone.spotCount; j++)
                    {
                        var a = Set.GetSpot(i);
                        var b = Set.GetSpot(j);
                        float d = new Vector2(a.position.x - b.position.x, a.position.z - b.position.z).magnitude;
                        Assert.GreaterOrEqual(d, HideSpotPipeline.Spacing - 1e-3f, $"{zone.chunkId}: spots {i - zone.firstSpot} and {j - zone.firstSpot}");
                        for (int s = 0; s < Set.SpeciesCount; s++)
                        {
                            for (int t = 0; t < Set.SpeciesCount; t++)
                            {
                                Body(s).Segment(Set.Placement(i, s, 0.15f), a.facing, 1f, out var a0, out var a1);
                                Body(t).Segment(Set.Placement(j, t, 0.15f), b.facing, 1f, out var b0, out var b1);
                                Assert.GreaterOrEqual(CollisionMath.Gap(a0, a1, Body(s).halfWidth, b0, b1, Body(t).halfWidth), -Tolerance,
                                    $"{zone.chunkId}: the animals of spots {i - zone.firstSpot} and {j - zone.firstSpot} touch");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void SpotsAndLandings_AreInTheZoneCameraFrame_At9x21And3x4()
        {
            var problems = new List<string>();
            foreach (var (zone, _) in Zones())
            {
                var inverse = Quaternion.Inverse(zone.cameraRotation);
                float tan = Mathf.Tan(zone.fieldOfView * 0.5f * Mathf.Deg2Rad);
                void Check(Vector3 point, string label)
                {
                    foreach (float aspect in new[] { HideSpotPipeline.NarrowAspect, HideSpotPipeline.WideAspect })
                    {
                        foreach (float lift in new[] { 0f, 0.3f })
                        {
                            var d = inverse * (point + Vector3.up * lift - zone.cameraPosition);
                            float y = d.z > 0f ? d.y / d.z / tan : 0f;
                            bool inside = d.z > 0.5f && y <= HideSpotPipeline.FrameTop + 0.02f && y >= -HideSpotPipeline.FrameBottom - 0.02f
                                          && Mathf.Abs(d.x / d.z) <= tan * aspect * (HideSpotPipeline.FrameHalfWidth + 0.02f);
                            if (!inside)
                                problems.Add($"{zone.chunkId} {label} out of the frame at {aspect:F2}");
                        }
                    }
                }
                for (int i = zone.firstSpot; i < zone.firstSpot + zone.spotCount; i++)
                {
                    Check(Set.GetSpot(i).position, $"spot {i - zone.firstSpot}");
                    Check(Set.GetSpot(i).landing, $"landing {i - zone.firstSpot}");
                }
                Check(zone.gatherPoint, "gathering point");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems.Distinct()));
        }

        [Test]
        public void MeasuredShares_MeetTheLevels()
        {
            int hits = 0, reachable = 0, unreachable = 0, total = 0, firstUnreachable = 0, firstTotal = 0;
            var over = new List<string>();
            var firsts = new List<string>();
            foreach (var (zone, _) in Zones())
            {
                int zoneFirstOut = 0, zoneFirst = 0;
                for (int i = zone.firstSpot; i < zone.firstSpot + zone.spotCount; i++)
                {
                    bool first = i - zone.firstSpot < HideSpotPipeline.FirstSpots;
                    for (int s = 0; s < Set.SpeciesCount; s++)
                    {
                        var mostHidden = Set.Placement(i, s, HideSpotSet.ShareAt(0));
                        var mostSeen = Set.Placement(i, s, HideSpotSet.ShareAt(HideSpotSet.ShareLevels - 1));
                        for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                        {
                            float measured = Set.MeasuredShare(i, s, level);
                            float target = HideSpotSet.ShareAt(level);
                            Assert.That(measured, Is.InRange(0f, 1f));
                            total++;
                            if (first)
                                zoneFirst++;
                            var p = Set.Placement(i, s, target);
                            // Out of reach: stuck at the most hidden placement and still showing more, or at the most visible
                            // one and still showing less.
                            bool out_ = (measured > target + HideSpotPipeline.LevelTolerance && (p - mostHidden).magnitude < 1e-3f)
                                        || (measured < target - HideSpotPipeline.LevelTolerance && (p - mostSeen).magnitude < 1e-3f);
                            if (out_)
                            {
                                unreachable++;
                                if (first)
                                    zoneFirstOut++;
                                continue;
                            }
                            reachable++;
                            if (Mathf.Abs(measured - target) <= HideSpotPipeline.LevelTolerance)
                                hits++;
                        }
                        if (Set.MeasuredShare(i, s, 0) > MostHiddenLimit)
                            over.Add($"{zone.chunkId} spot {i - zone.firstSpot} species {s}: {Set.MeasuredShare(i, s, 0) * 100f:F0} % at the 8 % level");
                    }
                }
                firstUnreachable += zoneFirstOut;
                firstTotal += zoneFirst;
                firsts.Add($"{zone.chunkId} {zoneFirstOut}/{zoneFirst} ({(zoneFirst > 0 ? 100f * zoneFirstOut / zoneFirst : 0f):F0} %)");
            }
            float ratio = reachable > 0 ? hits / (float)reachable : 0f;
            float outRatio = total > 0 ? unreachable / (float)total : 1f;
            TestContext.WriteLine($"Reachable levels within ±{HideSpotPipeline.LevelTolerance * 100f:F0} points: {hits}/{reachable} ({ratio * 100f:F0} %); " +
                                  $"unreachable {unreachable}/{total} ({outRatio * 100f:F1} %); among the first {HideSpotPipeline.FirstSpots} spots of each zone " +
                                  $"{firstUnreachable}/{firstTotal} ({(firstTotal > 0 ? 100f * firstUnreachable / firstTotal : 0f):F1} %): {string.Join(", ", firsts)}.");
            Assert.GreaterOrEqual(ratio, ShareHitTarget, $"{hits}/{reachable} reachable levels within ±5 points.");
            Assert.LessOrEqual(outRatio, UnreachableLimit, $"{unreachable}/{total} levels out of reach.");
            Assert.IsEmpty(over, string.Join("\n", over));
        }

        [Test]
        public void Placements_StandOnTheGround()
        {
            foreach (var (zone, chunk) in Zones())
            {
                var field = chunk.GroundField;
                Assert.IsTrue(field != null && field.IsValid, zone.chunkId);
                void Check(Vector3 p, string label) =>
                    Assert.AreEqual(field.Sample(p.x, p.z), p.y, 0.03f, $"{zone.chunkId} {label} stands on the ground.");
                for (int i = zone.firstSpot; i < zone.firstSpot + zone.spotCount; i++)
                {
                    for (int s = 0; s < Set.SpeciesCount; s++)
                        for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                            Check(Set.Placement(i, s, HideSpotSet.ShareAt(level)), $"spot {i - zone.firstSpot} species {s} level {level}");
                    Check(Set.GetSpot(i).position, $"spot {i - zone.firstSpot}");
                    Check(Set.GetSpot(i).landing, $"landing {i - zone.firstSpot}");
                }
                Check(zone.gatherPoint, "gathering point");
            }
        }

        [Test]
        public void Asset_IsInTheHideAndSeekGroupOnly()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.IsNotNull(settings, "Addressables settings missing.");
            string guid = AssetDatabase.AssetPathToGUID(HideSpotPipeline.AssetPath);
            var entry = settings.FindAssetEntry(guid);
            Assert.IsNotNull(entry, "The hide spots are not Addressable.");
            Assert.AreEqual(HideSpotPipeline.Group, entry.parentGroup.Name);
            Assert.AreEqual(HideSpotPipeline.Address, entry.address);
            int groups = settings.groups.Count(g => g != null && g.entries.Any(e => e.guid == guid));
            Assert.AreEqual(1, groups, "One group owns the set.");
        }

        [Test]
        public void Placement_InterpolatesBetweenTheMeasuredLevels_AndClampsToTheEnds()
        {
            var set = ScriptableObject.CreateInstance<HideSpotSet>();
            try
            {
                var spot = new HideSpotSet.Spot { position = new Vector3(9f, 0f, 9f), decor = HideDecorKind.TallGrass };
                var zone = new HideSpotSet.Zone { chunkId = "chunk.test", firstSpot = 0, spotCount = 1 };
                var placements = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 1f), new Vector3(4f, 0f, 1f) };
                var measured = new[] { 0.07f, 0.16f, 0.2f, 0.31f };
                set.SetAuthoring(new[] { zone }, new[] { spot }, new[] { 42u }, placements, measured);
                Assert.AreEqual(0, set.FindZone("chunk.test"));
                Assert.AreEqual(-1, set.FindZone("chunk.none"));
                Assert.AreEqual(0, set.SpeciesIndex(new ContentId(42u)));
                Assert.AreEqual(-1, set.SpeciesIndex(new ContentId(7u)));
                void Near(Vector3 expected, Vector3 actual, string label) => Assert.Less((expected - actual).magnitude, 1e-4f, label);
                Near(placements[0], set.Placement(0, 0, 0.02f), "Below 8 %: the first level.");
                Near(placements[0], set.Placement(0, 0, 0.08f), "8 %.");
                Near(new Vector3(0.5f, 0f, 0f), set.Placement(0, 0, 0.115f), "Halfway between 8 and 15 %.");
                Near(placements[1], set.Placement(0, 0, 0.15f), "15 %.");
                Near(new Vector3(1.5f, 0f, 0.5f), set.Placement(0, 0, 0.175f), "Halfway between 15 and 20 %.");
                Near(new Vector3(3f, 0f, 1f), set.Placement(0, 0, 0.25f), "Halfway between 20 and 30 %.");
                Near(placements[3], set.Placement(0, 0, 0.3f), "30 %.");
                Near(placements[3], set.Placement(0, 0, 0.6f), "Above 30 %: the last level.");
                Near(spot.position, set.Placement(0, -1, 0.15f), "An unmeasured species takes the spot's reference placement.");
                Assert.AreEqual(0.16f, set.MeasuredShare(0, 0, 1), 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void PrairieThemeDecor_StandsInEveryChunk()
        {
            foreach (var entry in WorldContentPipeline.GrasslandChunks)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.path);
                var props = prefab.transform.Find("Props");
                Assert.IsNotNull(props, entry.address);
                int grass = 0, bushes = 0;
                foreach (Transform group in props)
                {
                    foreach (Transform instance in group)
                    {
                        var source = PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                        string name = source != null ? source.name.Replace("PF_", "") : "";
                        if (GrasslandPipeline.IsTallGrass(name))
                            grass++;
                        else if (GrasslandPipeline.IsFlowerBush(name))
                            bushes++;
                    }
                }
                Assert.GreaterOrEqual(grass, 2, entry.address + ": the Prairie's tall grass (doc 01: « hautes herbes »).");
                Assert.GreaterOrEqual(bushes, 1, entry.address + ": a flowered bush (doc 01: « buissons fleuris »).");
            }
        }

        [Test]
        public void Spots_RecordTheirDecor()
        {
            foreach (var (zone, _) in Zones())
            {
                var kinds = new HashSet<HideDecorKind>();
                for (int i = zone.firstSpot; i < zone.firstSpot + zone.spotCount; i++)
                {
                    var spot = Set.GetSpot(i);
                    Assert.Greater(spot.decorRadius, 0.05f, zone.chunkId);
                    Assert.Greater(spot.decorHeight, 0.2f, zone.chunkId);
                    Assert.Less(new Vector2(spot.position.x - spot.decorCenter.x, spot.position.z - spot.decorCenter.z).magnitude, 4.5f,
                        zone.chunkId + ": a spot stands by its decor.");
                    kinds.Add(spot.decor);
                }
                TestContext.WriteLine($"{zone.chunkId}: {zone.spotCount} spots, decor {string.Join(", ", kinds)}");
            }
        }

        // ---------------- what a filmed game shows (HideSpotPipeline.Audit: the set measured again against its chunks) ----------------

        private static List<string> _audit;

        private static List<string> Audit(string kind)
        {
            _audit ??= HideSpotPipeline.Audit();
            Assert.IsFalse(_audit.Any(l => l.StartsWith("load:")), string.Join("\n", _audit.Where(l => l.StartsWith("load:"))));
            return _audit.Where(l => l.StartsWith(kind + ":")).ToList();
        }

        /// <summary>The play area (between the HUD's bands) shows only the island on every phone, never its edge or the void.</summary>
        [Test]
        public void PlayArea_ShowsOnlyTheIsland_OnEveryPhone()
        {
            var problems = Audit("void");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>
        /// The stage's field of view (HideSeekStage.FitFieldOfView) and the pipeline's copy (StageFov, which the play-area check
        /// uses) agree on every zone and aspect; wider than 9:16 the stage keeps the 9:16 horizontal extent (it zooms in).
        /// </summary>
        [Test]
        public void StageFieldOfView_MatchesThePlayAreaCheck_AndZoomsInOnWideScreens()
        {
            float[] aspects = { 0.4f, 9f / 21f, 9f / 19.5f, 9f / 16f, 2f / 3f, 3f / 4f, 1f };
            foreach (var (zone, _) in Zones())
            {
                var spots = Enumerable.Range(zone.firstSpot, zone.spotCount).Select(Set.GetSpot).ToList();
                float phone = 0f;
                foreach (float aspect in aspects)
                {
                    float stage = HideSeekStage.FitFieldOfView(Set, zone.firstSpot, zone.spotCount, Vector3.zero, zone.cameraPosition, zone.cameraRotation,
                        zone.fieldOfView, aspect);
                    float pipeline = HideSpotPipeline.StageFov(zone.cameraPosition, zone.cameraRotation, zone.fieldOfView, aspect, spots);
                    Assert.AreEqual(stage, pipeline, 1e-3f, $"{zone.chunkId} at {aspect:F2}");
                    if (Mathf.Approximately(aspect, HideSeekStage.WidestFittedAspect))
                        phone = stage;
                }
                foreach (float aspect in aspects.Where(a => a > HideSeekStage.WidestFittedAspect + 1e-4f))
                {
                    float stage = HideSeekStage.FitFieldOfView(Set, zone.firstSpot, zone.spotCount, Vector3.zero, zone.cameraPosition, zone.cameraRotation,
                        zone.fieldOfView, aspect);
                    Assert.AreEqual(Mathf.Tan(phone * 0.5f * Mathf.Deg2Rad) * HideSeekStage.WidestFittedAspect, Mathf.Tan(stage * 0.5f * Mathf.Deg2Rad) * aspect, 1e-4f,
                        $"{zone.chunkId} at {aspect:F2}: the 9:16 horizontal extent");
                }
            }
        }

        /// <summary>No placement or landing within 1.5 m of the strip's outer edge.</summary>
        [Test]
        public void Spots_StandAwayFromTheStripEdge()
        {
            var problems = Audit("edge");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>The « coucou » landing is a clear hop (0.7 m from every placement) where every species shows at least 80 %.</summary>
        [Test]
        public void Landings_AreAClearHop_AndClearlySeen()
        {
            var problems = Audit("landing");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>Only a spot's own decor hides its animal: no other prop stands before the decor or the animal.</summary>
        [Test]
        public void OnlyTheirDecor_HidesTheSpots()
        {
            var problems = Audit("occlusion");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
