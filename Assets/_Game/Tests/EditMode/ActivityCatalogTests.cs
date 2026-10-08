using System;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T37: the 18 activities against doc 01 Contenu (section 0 « Déblocage des systèmes », the head line of each S-ACT,
    /// section 12.0 musics, the doc 01 keys), the unlock predicate on its own and against the refuge level service,
    /// final openings, the Remote Config flags and bounds of the config.
    /// </summary>
    public sealed class ActivityCatalogTests
    {
        private static ActivityUnlockInput Input(int level, int animals, int day) =>
            new ActivityUnlockInput { RefugeLevel = level, Animals = animals, DayNumber = day };

        private static bool Open(ActivityId id, in ActivityUnlockInput input) => ActivityCatalog.Get(id).IsUnlocked(in input);

        [Test]
        public void The18Activities_HaveTheirDocNumbers_Keys_AndSlots()
        {
            string[] keys =
            {
                "grooming", "hide_seek", "nut_drop", "memory", "expeditions", "garden", "treasure_fishing", "lily_hop", "baking",
                "photo_safari", "cuddle_match", "glide", "constellations", "firefly_rhythm", "bubbles", "tile_puzzle", "burrow_maze",
                "grand_contest",
            };
            Assert.AreEqual(18, ActivityCatalog.ActivityCount);
            for (int slot = 0; slot < ActivityCatalog.ActivityCount; slot++)
            {
                var definition = ActivityCatalog.AtSlot(slot);
                Assert.AreEqual((ActivityId)(slot + 1), definition.Id, "S-ACT-" + (slot + 1).ToString("000"));
                Assert.AreEqual(slot, ActivityCatalog.SlotOf(definition.Id));
                Assert.AreEqual(definition.Id, ActivityCatalog.IdOfSlot(slot));
                Assert.AreEqual(keys[slot], definition.Key);
                Assert.AreEqual("activity." + keys[slot], definition.SourceId, "doc 01 « Noms anglais des activités ».");
                Assert.AreEqual(ContentId.FromSource("activity." + keys[slot]), definition.ContentId);
                Assert.AreEqual("activity." + keys[slot] + ".name", definition.NameKey);
                Assert.AreEqual("challenge." + keys[slot] + ".01", definition.ChallengeId(0));
                Assert.AreEqual("challenge." + keys[slot] + ".10", definition.ChallengeId(9));
                // T37 shipped the framework alone; T38 ships Grooming, the others wait for their tasks.
                Assert.AreEqual(definition.Id == ActivityId.Grooming, definition.ShippedInThisBuild, definition.Id + " shipped.");
            }
            Assert.AreEqual(ActivityCatalog.TestSlot, ActivityCatalog.SlotOf(ActivityId.Test));
            Assert.AreEqual(-1, ActivityCatalog.SlotOf(ActivityId.None));
            Assert.IsFalse(ActivityCatalog.TryGet(ActivityId.None, out _));
        }

        [Test]
        public void TicketsMusicsDurationsAndBaseBiomes_FollowDoc01()
        {
            foreach (var meta in new[] { ActivityId.Expeditions, ActivityId.Garden })
            {
                Assert.IsTrue(ActivityCatalog.Get(meta).Meta);
                Assert.IsFalse(ActivityCatalog.Get(meta).UsesTickets, "Timers, no tickets (doc Mini-jeux, section 2).");
            }
            Assert.IsFalse(ActivityCatalog.Get(ActivityId.GrandContest).UsesTickets, "Sans billet.");
            Assert.IsTrue(ActivityCatalog.Get(ActivityId.Grooming).FirstGameFree, "A15: the first Grooming game is offered.");
            for (int slot = 0; slot < ActivityCatalog.ActivityCount; slot++)
                if (slot != 0)
                    Assert.IsFalse(ActivityCatalog.AtSlot(slot).FirstGameFree);

            ActivityId[] calm = { ActivityId.Grooming, ActivityId.PawMemory, ActivityId.Bakery, ActivityId.Constellations, ActivityId.TilePuzzle, ActivityId.PhotoSafari };
            ActivityId[] lively = { ActivityId.HideAndSeek, ActivityId.NutRain, ActivityId.TreasureFishing, ActivityId.LilyHop, ActivityId.Slide, ActivityId.Bubbles, ActivityId.BurrowMaze };
            ActivityId[] puzzle = { ActivityId.CuddleMatch, ActivityId.GrandContest };
            foreach (var id in calm)
                Assert.AreEqual(ActivityMusic.Calm, ActivityCatalog.Get(id).Music, id.ToString());
            foreach (var id in lively)
                Assert.AreEqual(ActivityMusic.Lively, ActivityCatalog.Get(id).Music, id.ToString());
            foreach (var id in puzzle)
                Assert.AreEqual(ActivityMusic.Puzzle, ActivityCatalog.Get(id).Music, id.ToString());

            Assert.AreEqual(45, ActivityCatalog.Get(ActivityId.Grooming).Seconds);
            Assert.AreEqual(60, ActivityCatalog.Get(ActivityId.HideAndSeek).Seconds);
            Assert.AreEqual(90, ActivityCatalog.Get(ActivityId.PhotoSafari).Seconds);
            Assert.AreEqual(0, ActivityCatalog.Get(ActivityId.LilyHop).Seconds, "An endless run has no timer.");
            Assert.AreEqual(ActivityCatalog.Lake, ActivityCatalog.Get(ActivityId.TreasureFishing).BaseBiome);
            Assert.AreEqual(ActivityCatalog.Mountain, ActivityCatalog.Get(ActivityId.Slide).BaseBiome);
            Assert.AreEqual(ActivityCatalog.Forest, ActivityCatalog.Get(ActivityId.BurrowMaze).BaseBiome);
            Assert.AreEqual(ActivityCatalog.Meadow, ActivityCatalog.Get(ActivityId.Grooming).BaseBiome);
        }

        [Test]
        public void TheUnlockTable_IsDoc01Section0()
        {
            // Level, animals, day: each condition at its boundary.
            Assert.IsFalse(Open(ActivityId.Grooming, Input(2, 5, 1)));
            Assert.IsFalse(Open(ActivityId.Grooming, Input(3, 4, 1)));
            Assert.IsTrue(Open(ActivityId.Grooming, Input(3, 5, 1)));
            Assert.IsFalse(Open(ActivityId.HideAndSeek, Input(4, 7, 1)));
            Assert.IsTrue(Open(ActivityId.HideAndSeek, Input(4, 8, 1)));
            Assert.IsFalse(Open(ActivityId.Expeditions, Input(4, 8, 1)));
            Assert.IsTrue(Open(ActivityId.Expeditions, Input(5, 8, 1)));
            Assert.IsFalse(Open(ActivityId.Garden, Input(8, 50, 2)));
            Assert.IsTrue(Open(ActivityId.Garden, Input(8, 0, 3)));
            Assert.IsFalse(Open(ActivityId.NutRain, Input(10, 14, 3)), "Day 4 or 15 animals.");
            Assert.IsTrue(Open(ActivityId.NutRain, Input(10, 15, 1)));
            Assert.IsTrue(Open(ActivityId.NutRain, Input(10, 0, 4)));
            Assert.IsFalse(Open(ActivityId.NutRain, Input(9, 99, 99)));
            Assert.IsFalse(Open(ActivityId.PawMemory, Input(11, 0, 4)));
            Assert.IsTrue(Open(ActivityId.PawMemory, Input(11, 0, 5)));
            Assert.IsFalse(Open(ActivityId.CuddleMatch, Input(13, 99, 99)));
            Assert.IsTrue(Open(ActivityId.CuddleMatch, Input(14, 0, 1)), "Level 14, nothing else.");
            Assert.IsTrue(Open(ActivityId.PhotoSafari, Input(16, 0, 6)));
            Assert.IsFalse(Open(ActivityId.PhotoSafari, Input(16, 0, 5)));
            Assert.IsTrue(Open(ActivityId.TilePuzzle, Input(18, 0, 8)));
            Assert.IsFalse(Open(ActivityId.TilePuzzle, Input(17, 0, 8)));
            Assert.IsTrue(Open(ActivityId.BurrowMaze, Input(20, 0, 10)));
            Assert.IsFalse(Open(ActivityId.BurrowMaze, Input(20, 0, 9)));
            Assert.IsTrue(Open(ActivityId.FireflyRhythm, Input(22, 0, 12)));
            Assert.IsFalse(Open(ActivityId.FireflyRhythm, Input(21, 0, 12)));

            // The special conditions; a system that does not exist yet keeps its activity locked.
            var late = Input(99, 999, 99);
            Assert.IsFalse(Open(ActivityId.TreasureFishing, in late), "Lake unlocked.");
            Assert.IsFalse(Open(ActivityId.LilyHop, in late));
            Assert.IsFalse(Open(ActivityId.Slide, in late), "Mountain unlocked.");
            Assert.IsFalse(Open(ActivityId.Bubbles, in late), "Axolotl discovered.");
            Assert.IsFalse(Open(ActivityId.Bakery, in late), "Garden level 3 (T43).");
            Assert.IsFalse(Open(ActivityId.GrandContest, in late), "An accessory or outfit owned.");
            var lake = Input(1, 0, 1);
            lake.LakeUnlocked = true;
            Assert.IsTrue(Open(ActivityId.TreasureFishing, in lake));
            Assert.IsTrue(Open(ActivityId.LilyHop, in lake));
            var mountain = Input(1, 0, 1);
            mountain.MountainUnlocked = true;
            Assert.IsTrue(Open(ActivityId.Slide, in mountain));
            var axolotl = Input(1, 0, 1);
            axolotl.AxolotlDiscovered = true;
            Assert.IsTrue(Open(ActivityId.Bubbles, in axolotl));
            var garden = Input(1, 0, 1);
            garden.GardenLevel = 2;
            Assert.IsFalse(Open(ActivityId.Bakery, in garden));
            garden.GardenLevel = 3;
            Assert.IsTrue(Open(ActivityId.Bakery, in garden));
            Assert.IsFalse(Open(ActivityId.Constellations, Input(1, 0, 9)));
            Assert.IsTrue(Open(ActivityId.Constellations, Input(1, 0, 10)), "Day 10 at the latest.");
            var night = Input(1, 0, 2);
            night.NightVisitorSeen = true;
            Assert.IsTrue(Open(ActivityId.Constellations, in night), "The first night visitor.");
            var contest = Input(24, 0, 14);
            Assert.IsFalse(Open(ActivityId.GrandContest, in contest));
            contest.OutfitOwned = true;
            Assert.IsTrue(Open(ActivityId.GrandContest, in contest));
            contest.DayNumber = 13;
            Assert.IsFalse(Open(ActivityId.GrandContest, in contest));
        }

        [Test]
        public void TheFeatureBackedActivities_AgreeWithTheRefugeLevelService()
        {
            var rig = ActivityTestRig.Create(animals: 16);
            int[] levels = { 1, 3, 4, 5, 8, 10, 11, 16, 18, 20, 25 };
            int[] days = { 1, 3, 4, 5, 6, 8, 10, 12 };
            for (int l = 0; l < levels.Length; l++)
                for (int d = 0; d < days.Length; d++)
                {
                    rig.State.Progression.Refuge.Level = levels[l];
                    rig.Clock.Set(TestUtil.T0 + TimeSpan.FromDays(days[d] - 1));
                    var withFeatures = rig.S.BuildUnlockInput();
                    var table = withFeatures;
                    table.HasFeatures = false;
                    table.Features = 0;
                    for (int slot = 0; slot < ActivityCatalog.ActivityCount; slot++)
                    {
                        var definition = ActivityCatalog.AtSlot(slot);
                        if (!definition.Feature.HasValue)
                            continue;
                        bool refuge = rig.P.Refuge.IsFeatureUnlocked(definition.Feature.Value);
                        Assert.AreEqual(refuge, definition.IsUnlocked(in withFeatures), definition.Id + " level " + levels[l] + " day " + days[d]);
                        Assert.AreEqual(refuge, definition.IsUnlocked(in table),
                            "The doc 01 table and the refuge level service agree: " + definition.Id + " level " + levels[l] + " day " + days[d]);
                    }
                }
        }

        [Test]
        public void OpeningsAreFinal_AndAnnouncedOnce()
        {
            var rig = ActivityTestRig.Create(animals: 6, level: 2);
            int announced = 0;
            ActivityId last = ActivityId.None;
            rig.S.ActivityUnlocked += id =>
            {
                announced++;
                last = id;
            };
            Assert.IsFalse(rig.S.IsUnlocked(ActivityId.Grooming));
            Assert.IsTrue(rig.S.IsUnlocked(ActivityId.Test));

            rig.State.Progression.Refuge.Level = 3;
            rig.S.MarkUnlocksDirty();
            rig.S.Tick(1.0);
            Assert.IsTrue(rig.S.IsUnlocked(ActivityId.Grooming));
            Assert.AreEqual(1, announced);
            Assert.AreEqual(ActivityId.Grooming, last);

            rig.State.Progression.Refuge.Level = 1;
            rig.S.RefreshUnlocks();
            Assert.IsTrue(rig.S.IsUnlocked(ActivityId.Grooming), "Once open, always open (doc 08, rule 3).");
            Assert.AreEqual(1, announced);
            Assert.IsTrue(rig.Record(ActivityId.Grooming).Unlocked);
        }

        [Test]
        public void AHatch_OrANewDay_RefreshTheUnlocksAtTheNextTick()
        {
            var rig = ActivityTestRig.Create(animals: 4, level: 3);
            Assert.IsFalse(rig.S.IsUnlocked(ActivityId.Grooming), "4 animals.");
            Assert.IsTrue(rig.Hatch.Hatch(WishTestRig.BasicEgg).Succeeded);
            rig.State.Progression.Refuge.Level = 3;
            Assert.IsFalse(rig.S.IsUnlocked(ActivityId.Grooming), "Not before the tick.");
            rig.S.Tick(1.0);
            Assert.IsTrue(rig.S.IsUnlocked(ActivityId.Grooming), "The fifth animal opens Grooming.");

            rig.State.Progression.Refuge.Level = 8;
            rig.S.RefreshUnlocks();
            Assert.IsFalse(rig.S.IsUnlocked(ActivityId.Garden), "Day 1.");
            rig.Clock.Advance(TimeSpan.FromDays(2));
            rig.S.Tick(1.0);
            Assert.IsTrue(rig.S.IsUnlocked(ActivityId.Garden), "Day 3: the day boundary refreshes the unlocks.");
        }

        [Test]
        public void Availability_NeedsTheBuild_TheFlags_TheRules_AndTheOpening()
        {
            var rig = ActivityTestRig.Create();
            // T38 ships Grooming, but the rig registers only the test activity's rules (never RegisterShipped).
            Assert.IsTrue(rig.S.IsShipped(ActivityId.Grooming));
            Assert.IsFalse(rig.S.IsAvailable(ActivityId.Grooming), "Shipped and open, but without its rules: not shown.");
            Assert.IsTrue(rig.S.IsAvailable(ActivityId.Test), "The test activity shows only once a test registered its rules (the rig does).");
            var bare = new ActivityServices(rig.State, new ActivityState(), rig.Registry, rig.Economy, rig.Hatch, rig.SpeciesMastery, rig.P,
                rig.Bond, rig.Clock, null, new ActivityConfig());
            bare.RefreshUnlocks();
            Assert.IsFalse(bare.IsAvailable(ActivityId.Test), "Without its rules (the game), the test activity is never shown.");
            Assert.IsFalse(bare.AnyShipped, "No rules registered: no Play button.");
            ActivityRegistry.RegisterShipped(bare);
            Assert.IsTrue(bare.AnyShipped, "T38: the boot registers Grooming, the Play button exists.");
            Assert.IsTrue(bare.IsAvailable(ActivityId.Grooming), "Level 3 and 6 animals.");
            Assert.IsFalse(bare.IsAvailable(ActivityId.Test), "Never the test activity in the game.");
            bare.Dispose();
            Assert.IsTrue(rig.Config.TryOverrideFlag("flags.activity.grooming", false));
            Assert.IsFalse(rig.Config.IsEnabled(ActivityId.Grooming));
            Assert.IsTrue(rig.Config.IsEnabled(ActivityId.HideAndSeek));
            Assert.IsFalse(rig.Config.TryOverrideFlag("flags.activity.unknown", false));
            Assert.IsTrue(rig.Config.TryOverrideFlag("flags.activities", false));
            Assert.IsFalse(rig.Config.IsEnabled(ActivityId.Test));
        }

        [Test]
        public void TheChallengeDecorations_Are36_InTheCatalogueOrder()
        {
            Assert.AreEqual(36, ActivityRewardService.ChallengeDecorationCount);
            var seen = new System.Collections.Generic.HashSet<ContentId>();
            for (int i = 0; i < ActivityRewardService.ChallengeDecorationCount; i++)
            {
                var id = ActivityRewardService.ChallengeDecoration(i);
                Assert.IsTrue(id.IsValid, "E-DEC-CH-" + (i + 1).ToString("00"));
                Assert.IsTrue(seen.Add(id), "Unique.");
                StringAssert.StartsWith("decor.", ActivityRewardService.ChallengeDecorationSource(i));
            }
        }

        [Test]
        public void EveryRemoteKey_IsADoc08Key_AndItsBoundsHold()
        {
            var config = new ActivityConfig();
            int keys = 0;
            foreach (var key in ActivityConfig.RemoteKeys)
            {
                keys++;
                Assert.IsFalse(config.TryOverride(key, -1000), key + " refuses a value under its bounds.");
                Assert.IsFalse(config.TryOverride(key, double.NaN), key);
            }
            Assert.AreEqual(30, keys);
            int flags = 0;
            foreach (var flag in ActivityConfig.RemoteFlags)
            {
                flags++;
                Assert.IsTrue(config.TryOverrideFlag(flag, true), flag);
            }
            Assert.AreEqual(16, flags, "flags.activities and the 15 activity flags of doc 08.");
            Assert.IsTrue(config.TryOverride("activities.reward.featured_multiplier", 2.5), "A decimal key takes decimals.");
            Assert.IsFalse(config.TryOverride("gems.price.tickets_pack", 30.5));
        }
    }
}
