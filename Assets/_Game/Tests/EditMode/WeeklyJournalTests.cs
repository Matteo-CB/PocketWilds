using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T36, doc « 01 — Contenu des rendez-vous, des activités et de l'accueil », section 5 (plan section 8, item 6): the 52
    /// goals; the draw's slot order and its fallback to the Free slot; at most 2 goals of a system; × 0.3 for a goal of the
    /// previous journal; × 3 in the Free slot for a system opened in the last 7 days; the first journal's fixed goals and
    /// their replacements, its 4 to 10 days and its head start; the Monday renewal with the carry-over paid at the next
    /// opening, even weeks later; the real days left across a daylight-saving change; the rewards (5 Gems and 2 tickets or
    /// their Light a goal, 15 Gems and a Discovery egg complete, 50 Gems a week); the hooks; no allocation.
    /// </summary>
    public class WeeklyJournalTests
    {
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");
        private static readonly ContentId Focus = ContentId.FromSource("egg.grassland.focus");
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId Squirrel = ContentId.FromSource("species.squirrel");
        private static readonly ContentId Hedgehog = ContentId.FromSource("species.hedgehog");
        private static readonly string[] SpeciesNames = { "rabbit", "squirrel", "hedgehog" };

        // TestUtil.T0 is Sunday 4 October 2026, the install day.
        private const int Sunday = 20261004;
        private const int Monday = 20261005;
        private const int NextMonday = 20261012;

        private sealed class Rig
        {
            public PlayerState State;
            public FakeGameClock Clock;
            public EconomyService Economy;
            public HatchService Hatch;
            public ProgressionServices P;
            public DailyVisitConfig Config;
            public DailyContext Ctx;
            public WeeklyJournalService Journal;

            public DailyVisitState Visit => State.DailyVisit;

            /// <summary>That local time on that day: the clock and today's key, as the facade keeps them.</summary>
            public void SetClock(int dayKey, int hour = 12, int minute = 0)
            {
                var date = DailyDay.ToDate(dayKey);
                var local = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, Ctx.Zone);
                Clock.Set(new DateTimeOffset(utc, TimeSpan.Zero));
                Ctx.TodayKey = DailyDay.DayKey(Clock.UtcNow, Ctx.Zone, Config.ResetHour);
            }

            /// <summary>A day change: the clock, today's key, then the journal's check (the facade's order).</summary>
            public void Day(int dayKey, int hour = 12, int minute = 0)
            {
                SetClock(dayKey, hour, minute);
                Journal.Ensure(Ctx.TodayKey);
            }

            public JournalGoalRecord Goal(JournalGoalId id)
            {
                for (int i = 0; i < Journal.GoalCount; i++)
                    if (Journal.Get(i).Goal == (ushort)id)
                        return Journal.Get(i);
                return default;
            }

            public int IndexOf(JournalGoalId id)
            {
                for (int i = 0; i < Journal.GoalCount; i++)
                    if (Journal.Get(i).Goal == (ushort)id)
                        return i;
                return -1;
            }

            public bool Has(JournalGoalId id) => IndexOf(id) >= 0;
        }

        private static ContentRegistry Content()
        {
            var builder = new ContentRegistryBuilder();
            foreach (var name in SpeciesNames)
            {
                var species = ContentId.FromSource("species." + name);
                builder.AddSpecies(new SpeciesDefinition(species, "species." + name + ".name", RigFamily.SmallQuadruped, Grassland, 1.0,
                    "PF_Animal_Test"), "species." + name);
                foreach (var (variant, rarity) in new[] { ("brown", RarityTier.Common), ("white", RarityTier.Common), ("spotted", RarityTier.Rare) })
                {
                    string id = "variant." + name + "." + variant;
                    builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), species, id + ".name", rarity,
                        new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), id);
                }
            }
            builder.AddEgg(new EggDefinition(Basic, "egg.grassland.basic.name", Grassland, EggTier.Basic, new Color32[EggDefinition.PaletteSize],
                15, 0, 15, "PF_Egg_Base", 10.0, 1.18, new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 }, Pool()), "egg.grassland.basic");
            builder.AddEgg(new EggDefinition(Focus, "egg.grassland.focus.name", Grassland, EggTier.Focus, new Color32[EggDefinition.PaletteSize],
                15, 0, 15, "PF_Egg_Base", 30.0, 1.18, new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 }, Pool(), unlockBiomeMastery: 3,
                focusTargetCount: 2), "egg.grassland.focus");
            return builder.Build();
        }

        private static EggDropEntry[] Pool() =>
            new[] { new EggDropEntry(Rabbit, 1.0), new EggDropEntry(Squirrel, 1.0), new EggDropEntry(Hedgehog, 1.0) };

        /// <summary>
        /// A profile past the welcome, the Prairie open with biome mastery 3 (the Focus egg offered) and its 6 zones to open.
        /// Without <paramref name="progression"/>, every refuge function counts as open (one service alone).
        /// </summary>
        private static Rig Create(Action<DailyVisitConfig> configure = null, TimeZoneInfo zone = null, DateTimeOffset? start = null,
            bool progression = false, Action<PlayerState> prepare = null)
        {
            var t = start ?? TestUtil.T0;
            var rig = new Rig { State = PlayerState.CreateNew(t), Clock = new FakeGameClock(t), Config = new DailyVisitConfig() };
            configure?.Invoke(rig.Config);
            rig.State.DailyVisit.OnboardingDone = true;
            rig.State.World.Biomes.Add(new BiomeProgress { Biome = Grassland, Unlocked = true, ZonesOpened = 1u, MasteryLevel = 3 });
            prepare?.Invoke(rig.State);
            var content = Content();
            var progressionConfig = new ProgressionConfig { TimeZone = zone ?? TimeZoneInfo.Utc };
            rig.Economy = new EconomyService(rig.State, content, EconomyConfig.Default, null);
            rig.Hatch = new HatchService(rig.State, content, rig.Economy, rig.Clock, null);
            if (progression)
                rig.P = new ProgressionServices(rig.State, content, rig.Economy, rig.Hatch, rig.Clock, null, progressionConfig);
            rig.Ctx = new DailyContext(rig.State, content, rig.Config, progressionConfig, rig.Clock, rig.Economy, rig.Hatch, null, rig.P, null);
            rig.Journal = new WeeklyJournalService(rig.Ctx);
            return rig;
        }

        /// <summary>The week of <paramref name="monday"/> starts after an ordinary journal (no first journal, nothing remembered).</summary>
        private static void NewWeek(Rig rig, int monday, int keepGoals = 0)
        {
            var visit = rig.Visit;
            visit.GoalCount = (byte)keepGoals;
            visit.FirstJournalDone = true;
            visit.JournalStartKey = DailyDay.AddDays(monday, -7);
            visit.JournalEndKey = monday;
            rig.Day(monday);
        }

        private static HatchResult Hatched(ContentId egg, bool newSpecies = false, bool newVariant = false) =>
            new HatchResult(HatchStatus.Hatched, new AnimalInstance { InstanceId = 1, Species = Rabbit }, egg, BigValue.Zero, RarityTier.Common,
                newSpecies, newVariant, false);

        /// <summary>Plays what the goal counts until it is reached, through the hooks only.</summary>
        private static void Reach(Rig rig, int index)
        {
            var journal = rig.Journal;
            var record = journal.Get(index);
            int missing = record.Target - record.Progress;
            switch ((JournalGoalId)record.Goal)
            {
                case JournalGoalId.MasteryLevels:
                    journal.OnMasteryLeveled(Rabbit, missing);
                    break;
                case JournalGoalId.BondPoints:
                    journal.OnBondGained(1, missing);
                    break;
                case JournalGoalId.Upgrades:
                    journal.OnUpgradesBought(missing);
                    break;
                default:
                    for (int k = 0; k < missing; k++)
                        Step(journal, (JournalGoalId)record.Goal);
                    break;
            }
            Assert.IsTrue(journal.Get(index).IsReached, "JRN-" + record.Goal.ToString("000") + " reached through its hook.");
        }

        private static void Step(WeeklyJournalService journal, JournalGoalId goal)
        {
            switch (goal)
            {
                case JournalGoalId.HatchEggs: journal.OnHatched(Hatched(Basic)); break;
                case JournalGoalId.DiscoverVariants: journal.OnHatched(Hatched(Basic, newVariant: true)); break;
                case JournalGoalId.NewSpecies: journal.OnHatched(Hatched(Basic, newSpecies: true, newVariant: true)); break;
                case JournalGoalId.PreciousEggs: journal.OnHatched(Hatched(Focus)); break;
                case JournalGoalId.Wishes: journal.OnWishCompleted(); break;
                case JournalGoalId.DailyGifts: journal.OnGiftOpened(); break;
                case JournalGoalId.CalendarDays: journal.OnCalendarStamped(); break;
                case JournalGoalId.TreatsGiven: journal.OnTreatGiven(1); break;
                case JournalGoalId.AnimalsTapped: journal.OnAnimalTouched(1, true); break;
                case JournalGoalId.Expansions: journal.OnZoneOpened(); break;
                // T38: with the activities in the build (ActivitiesAvailable), the draw may bring JRN-033.
                case JournalGoalId.Grooming: journal.OnGroomingGame(new GroomingGameSummary { Animal = 1, Finished = true }); break;
                // T39: and JRN-016 (every animal found counts).
                case JournalGoalId.HideAndSeekFinds: journal.OnHideSeekGame(new HideSeekGameSummary { Hidden = 1, Found = 1, Finished = true }); break;
                // T40: and JRN-017 (every nut caught counts).
                case JournalGoalId.NutsCaught: journal.OnNutDropGame(new NutDropGameSummary { Caught = 1, Finished = true }); break;
                default: Assert.Fail("No hook for " + goal); break;
            }
        }

        private static void ReachAll(Rig rig)
        {
            for (int i = 0; i < rig.Journal.GoalCount; i++)
                Reach(rig, i);
        }

        private static JournalSlot[] Slots(Rig rig)
        {
            var slots = new JournalSlot[rig.Journal.GoalCount];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = (JournalSlot)rig.Journal.Get(i).Slot;
            return slots;
        }

        private static void AssertJournalRules(Rig rig, string context)
        {
            var seen = new HashSet<ushort>();
            var perSystem = new Dictionary<WishSystem, int>();
            for (int i = 0; i < rig.Journal.GoalCount; i++)
            {
                var record = rig.Journal.Get(i);
                Assert.IsTrue(seen.Add(record.Goal), context + ": JRN-" + record.Goal + " twice.");
                Assert.IsTrue(WeeklyJournalCatalog.TryGet((JournalGoalId)record.Goal, out var goal));
                Assert.IsTrue(goal.ShippedInThisBuild, context + ": JRN-" + record.Goal + " is not in this build.");
                if ((JournalSlot)record.Slot != JournalSlot.Free)
                    Assert.AreEqual(goal.Slot, (JournalSlot)record.Slot, context + ": a slot draws its own goals.");
                perSystem.TryGetValue(goal.System, out int n);
                perSystem[goal.System] = n + 1;
                Assert.LessOrEqual(n + 1, 2, context + ": at most 2 goals of " + goal.System + ".");
                Assert.AreEqual(0, record.Progress);
                Assert.IsFalse(record.Claimed);
            }
        }

        private static TimeZoneInfo ParisLike()
        {
            // UTC+1, summer time from the last Sunday of March 02:00 to the last Sunday of October 03:00.
            var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday);
            var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday);
            var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2000, 1, 1), new DateTime(2099, 12, 31), TimeSpan.FromHours(1),
                start, end);
            return TimeZoneInfo.CreateCustomTimeZone("PocketWilds/TestParis", TimeSpan.FromHours(1), "Test Paris", "Test Paris", "Test Paris Summer",
                new[] { rule });
        }

        // ---------------- Catalog (section 5.2) ----------------

        [Test]
        public void Catalog_Has52Goals_WithTheDocSlotsTargetsAndTheShippedSet()
        {
            var all = WeeklyJournalCatalog.All;
            Assert.AreEqual(52, all.Length);
            Assert.AreEqual(52, WeeklyJournalCatalog.Count);
            for (int i = 0; i < all.Length; i++)
            {
                Assert.AreEqual(i + 1, (int)all[i].Id, "All[id - 1].");
                Assert.IsTrue(WeeklyJournalCatalog.TryGet(all[i].Id, out var goal));
                Assert.AreEqual(all[i].Id, goal.Id);
                Assert.Greater(all[i].P1, 0);
                Assert.LessOrEqual(all[i].P1, all[i].P2);
                Assert.LessOrEqual(all[i].P2, all[i].P3);
                Assert.LessOrEqual(all[i].P3, all[i].P4);
            }
            Assert.IsFalse(WeeklyJournalCatalog.TryGet(JournalGoalId.None, out _));
            Assert.IsFalse(WeeklyJournalCatalog.TryGet((JournalGoalId)53, out _));
            Assert.AreEqual("journal.001", WeeklyJournalCatalog.TextKey(JournalGoalId.HatchEggs));
            Assert.AreEqual("journal.052", WeeklyJournalCatalog.TextKey(JournalGoalId.MemoryPairs));
            Assert.AreEqual(string.Empty, WeeklyJournalCatalog.TextKey(JournalGoalId.None));

            var slots = new Dictionary<JournalSlot, int[]>
            {
                [JournalSlot.Collection] = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 46 },
                [JournalSlot.Activities] = new[] { 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 43, 47, 48, 49, 50, 51, 52 },
                [JournalSlot.Rendezvous] = new[] { 21, 22, 23 },
                [JournalSlot.Timers] = new[] { 24, 25, 26, 27, 28, 29, 30 },
                [JournalSlot.Care] = new[] { 31, 32, 33, 34, 35 },
                [JournalSlot.Refuge] = new[] { 36, 37, 38, 39, 40, 41 },
                [JournalSlot.Free] = new[] { 42, 44, 45 },
            };
            foreach (var pair in slots)
                foreach (int id in pair.Value)
                    Assert.AreEqual(pair.Key, all[id - 1].Slot, "Slot of JRN-" + id.ToString("000"));

            // T36's goals, T38's JRN-033 « Brosse {n} animaux » with Grooming, T39's JRN-016 « Trouve {n} animaux à
            // Cache-cache » with Hide and Seek and T40's JRN-017 « Attrape {n} noisettes » with Nut Drop.
            var shipped = new HashSet<int> { 1, 2, 3, 4, 5, 16, 17, 21, 22, 23, 31, 32, 33, 34, 36, 38 };
            var rewardedOnly = new HashSet<int> { 10, 14 };
            foreach (var goal in all)
            {
                Assert.AreEqual(shipped.Contains((int)goal.Id), goal.ShippedInThisBuild, "Shipped in T36, T38, T39 or T40: JRN-" + (int)goal.Id);
                Assert.AreEqual(rewardedOnly.Contains((int)goal.Id), goal.RewardedGamesOnly, "Rewarded games only: JRN-" + (int)goal.Id);
            }

            // The shipped goals' targets (P1 / P2 / P3 / P4) and systems (plan, gap 18).
            var expected = new Dictionary<JournalGoalId, (int[] targets, WishSystem system)>
            {
                [JournalGoalId.HatchEggs] = (new[] { 30, 60, 100, 150 }, WishSystem.Hatching),
                [JournalGoalId.DiscoverVariants] = (new[] { 2, 3, 3, 4 }, WishSystem.DiscoveriesAndMastery),
                [JournalGoalId.MasteryLevels] = (new[] { 3, 5, 6, 8 }, WishSystem.DiscoveriesAndMastery),
                [JournalGoalId.NewSpecies] = (new[] { 1, 1, 1, 1 }, WishSystem.DiscoveriesAndMastery),
                [JournalGoalId.PreciousEggs] = (new[] { 2, 3, 4, 5 }, WishSystem.Hatching),
                [JournalGoalId.Wishes] = (new[] { 10, 12, 15, 15 }, WishSystem.Rendezvous),
                [JournalGoalId.DailyGifts] = (new[] { 4, 5, 5, 5 }, WishSystem.Rendezvous),
                [JournalGoalId.CalendarDays] = (new[] { 4, 5, 5, 5 }, WishSystem.Rendezvous),
                [JournalGoalId.BondPoints] = (new[] { 80, 120, 160, 200 }, WishSystem.CuddlesAndTreats),
                [JournalGoalId.TreatsGiven] = (new[] { 6, 9, 12, 15 }, WishSystem.CuddlesAndTreats),
                [JournalGoalId.AnimalsTapped] = (new[] { 30, 45, 60, 75 }, WishSystem.CuddlesAndTreats),
                [JournalGoalId.Grooming] = (new[] { 3, 4, 5, 6 }, WishSystem.Grooming),
                [JournalGoalId.HideAndSeekFinds] = (new[] { 20, 30, 40, 50 }, WishSystem.HideAndSeek),
                [JournalGoalId.NutsCaught] = (new[] { 150, 250, 350, 450 }, WishSystem.NutRain),
                [JournalGoalId.Upgrades] = (new[] { 6, 10, 12, 15 }, WishSystem.Upgrades),
                [JournalGoalId.Expansions] = (new[] { 1, 2, 2, 3 }, WishSystem.Expansions),
            };
            foreach (var pair in expected)
            {
                var goal = all[(int)pair.Key - 1];
                for (int tier = 1; tier <= 4; tier++)
                    Assert.AreEqual(pair.Value.targets[tier - 1], goal.Target(tier), pair.Key + " P" + tier);
                Assert.AreEqual(pair.Value.system, goal.System, pair.Key + " system");
            }
            Assert.AreEqual(WishSystem.None, all[8].System, "JRN-009 counts for the Play button as a whole.");
            Assert.AreEqual(WishSystem.HideAndSeek, all[15].System, "A goal about one activity counts for that activity.");

            Assert.AreEqual(1, WeeklyJournalCatalog.TierOf(1));
            Assert.AreEqual(1, WeeklyJournalCatalog.TierOf(14));
            Assert.AreEqual(2, WeeklyJournalCatalog.TierOf(15));
            Assert.AreEqual(2, WeeklyJournalCatalog.TierOf(39));
            Assert.AreEqual(3, WeeklyJournalCatalog.TierOf(40));
            Assert.AreEqual(3, WeeklyJournalCatalog.TierOf(69));
            Assert.AreEqual(4, WeeklyJournalCatalog.TierOf(70));
            Assert.AreEqual(4, WeeklyJournalCatalog.TierOf(500));
        }

        // ---------------- Draw (section 5.3) ----------------

        [Test]
        public void Draw_OneGoalPerSlotInOrder_SlotsWithoutCandidates_AreFilledByTheFreeSlot()
        {
            var c = JournalSlot.Collection;
            var r = JournalSlot.Rendezvous;
            var ca = JournalSlot.Care;
            var re = JournalSlot.Refuge;
            var f = JournalSlot.Free;

            // T36: Activités and Minuteurs have no candidate, they fall back to Free with the Free slot itself.
            var rig = Create();
            rig.Day(Sunday);
            for (int w = 0; w < 100; w++)
            {
                rig.Day(DailyDay.AddDays(NextMonday, 7 * w));
                CollectionAssert.AreEqual(new[] { c, r, ca, re, f, f, f }, Slots(rig), "Week " + w);
                AssertJournalRules(rig, "Week " + w);
            }

            // A slot whose goals are all hidden (no wishes, gift or calendar) is filled by a Free draw.
            rig = Create(cfg => cfg.WishesEnabled = cfg.GiftEnabled = cfg.CalendarEnabled = false);
            NewWeek(rig, NextMonday);
            CollectionAssert.AreEqual(new[] { c, ca, re, f, f, f, f }, Slots(rig));
            Assert.IsFalse(rig.Has(JournalGoalId.Wishes) || rig.Has(JournalGoalId.DailyGifts) || rig.Has(JournalGoalId.CalendarDays));

            // daily.journal_goals: more goals are Free slots; fewer cut the order from its end, the Free slot first.
            var expectedBySize = new Dictionary<int, JournalSlot[]>
            {
                [5] = new[] { c, r, ca, f, f },
                [6] = new[] { c, r, ca, re, f, f },
                [9] = new[] { c, r, ca, re, f, f, f, f, f },
                [10] = new[] { c, r, ca, re, f, f, f, f, f, f },
            };
            foreach (var pair in expectedBySize)
            {
                int size = pair.Key;
                rig = Create(cfg => cfg.JournalGoals = size);
                for (int w = 0; w < 30; w++)
                {
                    NewWeek(rig, DailyDay.AddDays(NextMonday, 7 * w));
                    CollectionAssert.AreEqual(pair.Value, Slots(rig), size + " goals, week " + w);
                    AssertJournalRules(rig, size + " goals, week " + w);
                }
            }
        }

        [Test]
        public void Draw_NeverTwiceTheSameGoal_AtMostTwoOfASystem_OnlyEligibleGoals()
        {
            // Ten goals use every goal the caps allow: 2 hatching, 2 discoveries, 2 rendez-vous, 2 care, upgrades, expansions.
            var rig = Create(cfg => cfg.JournalGoals = 10);
            rig.Day(Sunday);
            for (int w = 0; w < 300; w++)
            {
                rig.Day(DailyDay.AddDays(NextMonday, 7 * w));
                Assert.AreEqual(10, rig.Journal.GoalCount);
                AssertJournalRules(rig, "Week " + w);
            }

            // Eligibility: everything discovered, no Focus egg yet, every zone open, no treat stand rule broken.
            rig = Create(prepare: state =>
            {
                foreach (var name in SpeciesNames)
                {
                    state.Collection.DiscoveredSpecies.Add(ContentId.FromSource("species." + name));
                    foreach (var variant in new[] { "brown", "white", "spotted" })
                        state.Collection.DiscoveredVariants.Add(ContentId.FromSource("variant." + name + "." + variant));
                }
                state.World.Biomes[0] = new BiomeProgress { Biome = Grassland, Unlocked = true, ZonesOpened = 0x7Fu, MasteryLevel = 0 };
            });
            for (int w = 0; w < 100; w++)
            {
                NewWeek(rig, DailyDay.AddDays(NextMonday, 7 * w));
                AssertJournalRules(rig, "Week " + w);
                Assert.IsFalse(rig.Has(JournalGoalId.DiscoverVariants), "No base variant left to find.");
                Assert.IsFalse(rig.Has(JournalGoalId.NewSpecies), "No species left to find.");
                Assert.IsFalse(rig.Has(JournalGoalId.PreciousEggs), "No Focus egg unlocked.");
                Assert.IsFalse(rig.Has(JournalGoalId.Expansions), "No expansion left.");
            }
        }

        [Test]
        public void Draw_AGoalOfThePreviousJournal_WeighsThreeTenths()
        {
            var rig = Create();
            rig.SetClock(NextMonday);
            rig.Visit.PreviousGoals[0] = (ushort)JournalGoalId.Expansions;
            rig.Visit.PreviousGoalCount = 1;
            Assert.AreEqual(0.3, rig.Journal.DrawWeight(JournalGoalId.Expansions, false), 1e-9);
            Assert.AreEqual(1.0, rig.Journal.DrawWeight(JournalGoalId.Upgrades, false), 1e-9);
            Assert.AreEqual(0.0, rig.Journal.DrawWeight(JournalGoalId.ActivityStars, false), "Not in this build: never drawn.");

            // The Refuge slot holds Upgrades and Expansions: 0.3 / 1.3 for the goal of last week, 1 / 2 otherwise.
            const int weeks = 6000;
            int remembered = 0, fresh = 0;
            for (int w = 0; w < weeks; w++)
            {
                int monday = DailyDay.AddDays(NextMonday, 7 * w);
                rig.Visit.Goals[0] = new JournalGoalRecord { Goal = (ushort)JournalGoalId.Expansions, Slot = (byte)JournalSlot.Refuge, Target = 1 };
                NewWeek(rig, monday, keepGoals: 1);
                if (rig.Visit.PreviousGoalCount != 1 || rig.Visit.PreviousGoals[0] != (ushort)JournalGoalId.Expansions)
                    Assert.Fail("The previous journal is remembered.");
                if (RefugeGoal(rig) == JournalGoalId.Expansions)
                    remembered++;

                NewWeek(rig, DailyDay.AddDays(monday, 7 * weeks + 7));
                if (RefugeGoal(rig) == JournalGoalId.Expansions)
                    fresh++;
            }
            Assert.AreEqual(0.3 / 1.3, remembered / (double)weeks, 0.03, "× 0.3 when it was in the previous journal.");
            Assert.AreEqual(0.5, fresh / (double)weeks, 0.03, "Weight 1 otherwise.");
        }

        private static JournalGoalId RefugeGoal(Rig rig)
        {
            for (int i = 0; i < rig.Journal.GoalCount; i++)
                if ((JournalSlot)rig.Journal.Get(i).Slot == JournalSlot.Refuge)
                    return (JournalGoalId)rig.Journal.Get(i).Goal;
            return JournalGoalId.None;
        }

        [Test]
        public void FreeSlot_ASystemOpenedInTheLastSevenDays_WeighsThreeTimes()
        {
            var rig = Create();
            rig.SetClock(20261020);
            int today = rig.Ctx.DayNumber;
            Assert.AreEqual(17, today);
            var opened = rig.Visit.SystemUnlockDay;

            opened[(int)WishSystem.Expansions] = today - 2;
            Assert.AreEqual(3.0, rig.Journal.DrawWeight(JournalGoalId.Expansions, true), 1e-9);
            Assert.AreEqual(1.0, rig.Journal.DrawWeight(JournalGoalId.Expansions, false), 1e-9, "Only the Free slot favors new systems.");
            opened[(int)WishSystem.Expansions] = today - 6;
            Assert.AreEqual(3.0, rig.Journal.DrawWeight(JournalGoalId.Expansions, true), 1e-9, "Opened 6 days ago: inside the 7 days.");
            opened[(int)WishSystem.Expansions] = today - 7;
            Assert.AreEqual(1.0, rig.Journal.DrawWeight(JournalGoalId.Expansions, true), 1e-9, "Opened 7 days ago: no longer new.");
            opened[(int)WishSystem.Expansions] = 0;
            Assert.AreEqual(1.0, rig.Journal.DrawWeight(JournalGoalId.Expansions, true), 1e-9);
            opened[(int)WishSystem.Expansions] = today;
            rig.Visit.PreviousGoals[0] = (ushort)JournalGoalId.Expansions;
            rig.Visit.PreviousGoalCount = 1;
            Assert.AreEqual(0.9, rig.Journal.DrawWeight(JournalGoalId.Expansions, true), 1e-9, "Both rules multiply.");

            // Collection {001, 003}, Care {031, 032, 034}, Refuge {036, 038}, no rendez-vous goal: after the three slots, the
            // first Free draw takes among the Collection goal left, the two Care goals left and the Refuge goal left. When the
            // Refuge slot took Upgrades, Expansions is the last of these four: 3 / 6 when new, 1 / 4 otherwise.
            rig = Create(cfg => cfg.WishesEnabled = cfg.GiftEnabled = cfg.CalendarEnabled = false, prepare: state =>
            {
                foreach (var name in SpeciesNames)
                {
                    state.Collection.DiscoveredSpecies.Add(ContentId.FromSource("species." + name));
                    foreach (var variant in new[] { "brown", "white", "spotted" })
                        state.Collection.DiscoveredVariants.Add(ContentId.FromSource("variant." + name + "." + variant));
                }
                state.World.Biomes[0] = new BiomeProgress { Biome = Grassland, Unlocked = true, ZonesOpened = 1u, MasteryLevel = 0 };
            });
            const int weeks = 12000;
            int newCases = 0, newHits = 0, oldCases = 0, oldHits = 0;
            for (int w = 0; w < weeks; w++)
            {
                bool isNew = (w & 1) == 0;
                int monday = DailyDay.AddDays(NextMonday, 7 * w);
                var visit = rig.Visit;
                visit.GoalCount = 0;
                visit.FirstJournalDone = true;
                visit.JournalEndKey = monday;
                rig.SetClock(monday);
                visit.SystemUnlockDay[(int)WishSystem.Expansions] = isNew ? rig.Ctx.DayNumber : 0;
                rig.Journal.Ensure(rig.Ctx.TodayKey);

                if (rig.Journal.GoalCount != 6 || (JournalSlot)rig.Journal.Get(0).Slot != JournalSlot.Collection
                    || (JournalSlot)rig.Journal.Get(1).Slot != JournalSlot.Care || (JournalSlot)rig.Journal.Get(2).Slot != JournalSlot.Refuge)
                    Assert.Fail("Collection, Care, Refuge, then three Free goals: everything but one Care goal (2 per system).");
                if (rig.Journal.Get(2).Goal != (ushort)JournalGoalId.Upgrades)
                    continue;
                bool hit = rig.Journal.Get(3).Goal == (ushort)JournalGoalId.Expansions;
                if (isNew)
                {
                    newCases++;
                    if (hit)
                        newHits++;
                }
                else
                {
                    oldCases++;
                    if (hit)
                        oldHits++;
                }
            }
            Assert.Greater(newCases, 2000);
            Assert.Greater(oldCases, 2000);
            Assert.AreEqual(0.5, newHits / (double)newCases, 0.05, "× 3 for a system opened in the last 7 days.");
            Assert.AreEqual(0.25, oldHits / (double)oldCases, 0.05, "Weight 1 otherwise.");
        }

        [Test]
        public void Draw_TargetsFollowTheRefugeLevelTier_FixedAtTheDraw()
        {
            var rig = Create(progression: true);
            for (int i = 0; i < 5; i++)
                rig.State.Collection.Add(new AnimalInstance { Species = i < 3 ? Rabbit : Squirrel });
            rig.State.Collection.DiscoveredSpecies.Add(Rabbit);
            rig.State.Collection.DiscoveredSpecies.Add(Squirrel);
            rig.State.Progression.Refuge.Level = 20;
            rig.Day(Monday);
            Assert.AreEqual(Monday, rig.Journal.StartKey, "Day 2 at refuge level 6 or more: the first journal opens.");
            Assert.AreEqual(30, rig.Goal(JournalGoalId.HatchEggs).Target, "The first journal keeps its fixed targets.");

            rig.Day(NextMonday);
            Assert.AreEqual(7, rig.Journal.GoalCount);
            for (int i = 0; i < rig.Journal.GoalCount; i++)
            {
                var record = rig.Journal.Get(i);
                WeeklyJournalCatalog.TryGet((JournalGoalId)record.Goal, out var goal);
                Assert.AreEqual(goal.Target(2), record.Target, "Refuge level 20: P2 for JRN-" + record.Goal);
            }
            var before = new int[rig.Journal.GoalCount];
            for (int i = 0; i < before.Length; i++)
                before[i] = rig.Journal.Get(i).Target;

            rig.State.Progression.Refuge.Level = 75;
            rig.Day(DailyDay.AddDays(NextMonday, 3));
            for (int i = 0; i < before.Length; i++)
                Assert.AreEqual(before[i], rig.Journal.Get(i).Target, "Targets never change after the draw.");

            rig.Day(DailyDay.AddDays(NextMonday, 7));
            for (int i = 0; i < rig.Journal.GoalCount; i++)
            {
                var record = rig.Journal.Get(i);
                WeeklyJournalCatalog.TryGet((JournalGoalId)record.Goal, out var goal);
                Assert.AreEqual(goal.Target(4), record.Target, "Refuge level 75: P4 for JRN-" + record.Goal);
            }
        }

        [Test]
        public void SameWeek_SameDraw_AndNoRedrawDuringTheWeek()
        {
            var profile = new Guid("6b1d3e52-90aa-4c55-8d1e-2f4c3a7b9e10");
            var a = Create(prepare: state => state.Meta.ProfileId = profile);
            var b = Create(prepare: state => state.Meta.ProfileId = profile);
            a.Day(Sunday);
            b.Day(Sunday);
            for (int w = 0; w < 20; w++)
            {
                int monday = DailyDay.AddDays(NextMonday, 7 * w);
                a.Day(monday);
                b.Day(monday);
                Assert.AreEqual(a.Journal.GoalCount, b.Journal.GoalCount);
                for (int i = 0; i < a.Journal.GoalCount; i++)
                    Assert.AreEqual(a.Journal.Get(i).Goal, b.Journal.Get(i).Goal, "Same profile and week: same journal (no reroll).");
            }

            var goals = new ushort[a.Journal.GoalCount];
            for (int i = 0; i < goals.Length; i++)
                goals[i] = a.Journal.Get(i).Goal;
            a.Journal.OnHatched(Hatched(Basic));
            int start = a.Journal.StartKey;
            for (int d = 1; d < 7; d++)
                a.Day(DailyDay.AddDays(start, d));
            for (int i = 0; i < goals.Length; i++)
                Assert.AreEqual(goals[i], a.Journal.Get(i).Goal, "No redraw before Monday.");
            Assert.AreEqual(start, a.Journal.StartKey);
        }

        // ---------------- First journal (section 5.4) ----------------

        [Test]
        public void Unlocked_RefugeLevelSix_DayTwo_TheWelcomeOver_AndTheFlag()
        {
            var rig = Create(progression: true);
            rig.State.Progression.Refuge.Level = 6;
            rig.Day(Sunday);
            Assert.IsFalse(rig.Journal.Unlocked, "Day 1: not yet.");
            Assert.AreEqual(0, rig.Journal.GoalCount);
            Assert.AreEqual(0, rig.Journal.EndKey);

            rig.SetClock(Monday);
            rig.State.Progression.Refuge.Level = 5;
            Assert.IsFalse(rig.Journal.Unlocked, "Refuge level 5: not yet.");
            rig.State.Progression.Refuge.Level = 6;
            rig.State.DailyVisit.OnboardingDone = false;
            Assert.IsFalse(rig.Journal.Unlocked, "The welcome is not over.");
            rig.State.DailyVisit.OnboardingDone = true;
            rig.Config.JournalEnabled = false;
            Assert.IsFalse(rig.Journal.Unlocked, "flags.weekly_journal off.");
            rig.Journal.Ensure(rig.Ctx.TodayKey);
            Assert.AreEqual(0, rig.Journal.GoalCount);

            rig.Config.JournalEnabled = true;
            Assert.IsTrue(rig.Journal.Unlocked);
            bool started = false, first = false;
            rig.Journal.Started += isFirst =>
            {
                started = true;
                first = isFirst;
            };
            rig.Journal.Ensure(rig.Ctx.TodayKey);
            Assert.IsTrue(started && first, "The first journal opens on day 2.");
            Assert.IsTrue(rig.Journal.IsFirstJournal);
            Assert.Greater(rig.Journal.GoalCount, 0);

            // The flag off hides the journal and keeps it: no renewal while hidden, nothing erased.
            rig.Config.JournalEnabled = false;
            int goals = rig.Journal.GoalCount;
            rig.Day(NextMonday);
            Assert.AreEqual(Monday, rig.Journal.StartKey);
            Assert.AreEqual(goals, rig.Journal.GoalCount);
            rig.Config.JournalEnabled = true;
            rig.Day(NextMonday);
            Assert.AreEqual(NextMonday, rig.Journal.StartKey);
            Assert.IsFalse(rig.Journal.IsFirstJournal);
        }

        [Test]
        public void FirstJournal_HasTheFixedGoals_AClosedSystemsGoalIsReplacedByItsSlotsDraw()
        {
            var rig = Create();
            rig.Day(Sunday);
            Assert.IsTrue(rig.Journal.IsFirstJournal);
            Assert.AreEqual(7, rig.Journal.GoalCount);
            var expected = new (JournalGoalId id, int target, JournalSlot slot)[]
            {
                (JournalGoalId.HatchEggs, 30, JournalSlot.Collection),
                (JournalGoalId.Wishes, 10, JournalSlot.Rendezvous),
                (JournalGoalId.BondPoints, 80, JournalSlot.Care),
                (JournalGoalId.Upgrades, 6, JournalSlot.Refuge),
                (JournalGoalId.AnimalsTapped, 30, JournalSlot.Free),
            };
            foreach (var (id, target, slot) in expected)
            {
                Assert.IsTrue(rig.Has(id), id + " is fixed.");
                Assert.AreEqual(target, rig.Goal(id).Target, id + " target.");
                Assert.AreEqual(slot, (JournalSlot)rig.Goal(id).Slot, id + " slot.");
            }
            // JRN-009 (stars) and JRN-024 (expeditions) need closed systems: their slots draw normally, find nothing in
            // T36 and fall back to Free; 031 and 034 already fill Care's system, so 032 cannot come.
            Assert.IsFalse(rig.Has(JournalGoalId.ActivityStars));
            Assert.IsFalse(rig.Has(JournalGoalId.Expeditions));
            Assert.IsFalse(rig.Has(JournalGoalId.TreatsGiven));
            CollectionAssert.AreEqual(new[]
            {
                JournalSlot.Collection, JournalSlot.Rendezvous, JournalSlot.Care, JournalSlot.Refuge, JournalSlot.Free, JournalSlot.Free,
                JournalSlot.Free,
            }, Slots(rig));
            var seen = new HashSet<ushort>();
            var perSystem = new Dictionary<WishSystem, int>();
            for (int i = 0; i < rig.Journal.GoalCount; i++)
            {
                var record = rig.Journal.Get(i);
                Assert.IsTrue(seen.Add(record.Goal));
                WeeklyJournalCatalog.TryGet((JournalGoalId)record.Goal, out var goal);
                Assert.IsTrue(goal.ShippedInThisBuild);
                perSystem.TryGetValue(goal.System, out int n);
                perSystem[goal.System] = n + 1;
                Assert.LessOrEqual(n + 1, 2);
            }

            // Wishes hidden: JRN-021's place goes to the normal draw of the Rendez-vous slot.
            rig = Create(cfg => cfg.WishesEnabled = false);
            rig.Day(Sunday);
            Assert.IsFalse(rig.Has(JournalGoalId.Wishes));
            int rendezvous = -1;
            for (int i = 0; i < rig.Journal.GoalCount; i++)
                if ((JournalSlot)rig.Journal.Get(i).Slot == JournalSlot.Rendezvous)
                    rendezvous = i;
            Assert.GreaterOrEqual(rendezvous, 0);
            var drawn = (JournalGoalId)rig.Journal.Get(rendezvous).Goal;
            Assert.IsTrue(drawn == JournalGoalId.DailyGifts || drawn == JournalGoalId.CalendarDays);
            Assert.AreEqual(4, rig.Journal.Get(rendezvous).Target, "The slot's draw: its P1 target.");
            Assert.AreEqual(30, rig.Goal(JournalGoalId.HatchEggs).Target, "The other fixed goals stay.");
        }

        [Test]
        public void FirstJournal_LastsFourToTenDays_UntilAMonday()
        {
            // Opened Monday 5 to Sunday 11 October 2026.
            int[] expectedDays = { 7, 6, 5, 4, 10, 9, 8 };
            for (int d = 0; d < 7; d++)
            {
                var rig = Create();
                int day = DailyDay.AddDays(Monday, d);
                rig.Day(day);
                int length = DailyDay.DaysBetween(rig.Journal.StartKey, rig.Journal.EndKey);
                Assert.AreEqual(day, rig.Journal.StartKey);
                Assert.AreEqual(expectedDays[d], length, "Opened " + DailyDay.ToDate(day).DayOfWeek);
                Assert.AreEqual(expectedDays[d], rig.Journal.DaysLeft);
                Assert.That(length, NUnit.Framework.Is.InRange(4, 10));
                Assert.AreEqual(DayOfWeek.Monday, DailyDay.ToDate(rig.Journal.EndKey).DayOfWeek);
            }
        }

        [Test]
        public void FirstJournal_StartsAhead_WithTheEggsSinceInstall_AndTheWishesDone()
        {
            // A new profile: at least 6 of 30 and 2 of 10.
            var rig = Create();
            rig.Day(Sunday);
            Assert.AreEqual(6, rig.Goal(JournalGoalId.HatchEggs).Progress);
            Assert.AreEqual(2, rig.Goal(JournalGoalId.Wishes).Progress);
            Assert.IsTrue(rig.Journal.HeadStart(rig.IndexOf(JournalGoalId.HatchEggs)));
            Assert.IsTrue(rig.Journal.HeadStart(rig.IndexOf(JournalGoalId.Wishes)));
            Assert.IsFalse(rig.Journal.HeadStart(rig.IndexOf(JournalGoalId.BondPoints)));
            Assert.AreEqual(0, rig.Goal(JournalGoalId.BondPoints).Progress);

            // 12 eggs hatched since install (instance ids 1 to 12) and 5 wishes done.
            rig = Create(prepare: state =>
            {
                state.Collection.SetNextInstanceId(13);
                state.DailyVisit.WishesCompleted = 5;
            });
            rig.Day(Sunday);
            Assert.AreEqual(12, rig.Goal(JournalGoalId.HatchEggs).Progress);
            Assert.AreEqual(5, rig.Goal(JournalGoalId.Wishes).Progress);

            // A long-time player starts well ahead, never already done.
            rig = Create(prepare: state =>
            {
                state.Collection.SetNextInstanceId(501);
                state.DailyVisit.WishesCompleted = 40;
            });
            rig.Day(Sunday);
            Assert.AreEqual(29, rig.Goal(JournalGoalId.HatchEggs).Progress);
            Assert.AreEqual(9, rig.Goal(JournalGoalId.Wishes).Progress);
            Assert.IsFalse(rig.Journal.HasClaimable);

            // daily.journal_prefilled_goals.
            rig = Create(cfg => cfg.JournalPrefilled = 1);
            rig.Day(Sunday);
            Assert.AreEqual(6, rig.Goal(JournalGoalId.HatchEggs).Progress);
            Assert.AreEqual(0, rig.Goal(JournalGoalId.Wishes).Progress);
            rig = Create(cfg => cfg.JournalPrefilled = 0);
            rig.Day(Sunday);
            Assert.AreEqual(0, rig.Goal(JournalGoalId.HatchEggs).Progress);
            Assert.IsFalse(rig.Journal.HeadStart(rig.IndexOf(JournalGoalId.HatchEggs)));

            // The next journals start from zero.
            rig = Create();
            rig.Day(Sunday);
            rig.Day(NextMonday);
            Assert.IsFalse(rig.Journal.IsFirstJournal);
            for (int i = 0; i < rig.Journal.GoalCount; i++)
            {
                Assert.AreEqual(0, rig.Journal.Get(i).Progress);
                Assert.IsFalse(rig.Journal.HeadStart(i));
            }
        }

        // ---------------- Monday renewal and carry-over (section 5.1) ----------------

        [Test]
        public void Monday_StartsANewJournal_ReachedUnclaimedGoalsArePaidAtTheNextOpening_EvenWeeksLater()
        {
            var rig = Create();
            var reached = new List<int>();
            var claimed = new List<int>();
            rig.Journal.GoalReached += i => reached.Add(i);
            rig.Journal.Claimed += (id, granted) => claimed.Add(id);
            rig.Day(Sunday);
            Assert.AreEqual(NextMonday, rig.Journal.EndKey);

            int hatch = rig.IndexOf(JournalGoalId.HatchEggs);
            for (int k = 0; k < 30; k++)
                rig.Journal.OnHatched(Hatched(Basic));
            Assert.AreEqual(30, rig.Journal.Get(hatch).Progress, "Capped at the target.");
            CollectionAssert.AreEqual(new[] { hatch }, reached, "Raised once.");
            int bond = rig.IndexOf(JournalGoalId.BondPoints);
            rig.Journal.OnBondGained(3, 80);
            Assert.IsTrue(rig.Journal.TryClaimGoal(bond, out _));
            Assert.AreEqual(5, rig.State.Wallet.Gems);
            var oldGoals = new HashSet<ushort>();
            for (int i = 0; i < rig.Journal.GoalCount; i++)
                oldGoals.Add(rig.Journal.Get(i).Goal);

            // Sunday night ends nothing; Monday 00:00 does.
            rig.Day(DailyDay.AddDays(NextMonday, -1), 23, 59);
            Assert.AreEqual(Sunday, rig.Journal.StartKey);
            Assert.AreEqual(1, rig.Journal.DaysLeft);
            bool newJournal = false;
            rig.Journal.Started += first => newJournal = !first;
            rig.Day(NextMonday, 0, 0);
            Assert.IsTrue(newJournal, "« Un nouveau carnet t'attend ».");
            Assert.IsFalse(rig.Journal.IsFirstJournal);
            Assert.AreEqual(NextMonday, rig.Journal.StartKey);
            Assert.AreEqual(DailyDay.AddDays(NextMonday, 7), rig.Journal.EndKey);
            Assert.AreEqual(7, rig.Journal.DaysLeft);
            Assert.AreEqual(5, rig.Visit.CarryGems, "The reached, unclaimed hatching goal waits.");
            Assert.AreEqual(2, rig.Visit.CarryTickets);
            Assert.AreEqual(0, rig.Visit.CarryDiscovery, "The journal was not complete.");
            Assert.AreEqual(5, rig.State.Wallet.Gems, "Nothing paid by the renewal itself, nothing taken.");
            Assert.AreEqual(7, rig.Visit.PreviousGoalCount);
            for (int i = 0; i < 7; i++)
                Assert.IsTrue(oldGoals.Contains(rig.Visit.PreviousGoals[i]), "The previous goals are remembered.");
            Assert.IsTrue(rig.Journal.HasCarry);
            Assert.IsTrue(rig.Journal.HasClaimable);

            // Away for the whole next week: the journal of the current week, the carry-over still there.
            rig.Day(DailyDay.AddDays(NextMonday, 15));
            Assert.AreEqual(DailyDay.AddDays(NextMonday, 14), rig.Journal.StartKey);
            Assert.AreEqual(5, rig.Visit.CarryGems);
            Assert.AreEqual(2, rig.Visit.CarryTickets);

            Assert.IsTrue(rig.Journal.ClaimCarry(out var carry));
            Assert.AreEqual(5, carry.Bundle.Gems);
            Assert.AreEqual(0, carry.Bundle.Tickets, "No Play button yet: the tickets are Light.");
            Assert.AreEqual(30.0, carry.Bundle.LightMinutes, 1e-9);
            Assert.IsTrue(carry.Bundle.Substituted);
            Assert.IsFalse(carry.Light.IsZero);
            Assert.AreEqual(10, rig.State.Wallet.Gems);
            Assert.AreEqual(WeeklyJournalService.CarryClaimId, claimed[claimed.Count - 1]);
            Assert.IsFalse(rig.Journal.HasCarry);
            Assert.IsFalse(rig.Journal.ClaimCarry(out _));
            Assert.AreEqual(10, rig.State.Wallet.Gems);
        }

        [Test]
        public void Monday_ACompleteJournalNotClaimed_CarriesItsCompletionToo()
        {
            var rig = Create(cfg => cfg.ActivitiesAvailable = true);
            NewWeek(rig, NextMonday);
            ReachAll(rig);
            rig.Day(DailyDay.AddDays(NextMonday, 7));
            Assert.AreEqual(7 * 5 + 15, rig.Visit.CarryGems);
            Assert.AreEqual(14, rig.Visit.CarryTickets);
            Assert.AreEqual(1, rig.Visit.CarryDiscovery);

            Assert.IsTrue(rig.Journal.ClaimCarry(out var granted));
            Assert.AreEqual(50, rig.State.Wallet.Gems, "A whole week: 50 Gems.");
            Assert.AreEqual(14, rig.State.Inventory.Tickets, "The Play button exists: real tickets.");
            Assert.AreEqual(0.0, granted.Bundle.LightMinutes);
            Assert.AreEqual(1, rig.State.World.DiscoveryEggs);
            Assert.AreEqual(1, granted.Bundle.DiscoveryEggs);
        }

        [Test]
        public void DaysLeft_IsTheRealCount_AcrossDaylightSavingChanges()
        {
            var paris = ParisLike();
            Assert.AreEqual(TimeSpan.FromHours(2), paris.GetUtcOffset(new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero)));
            Assert.AreEqual(TimeSpan.FromHours(1), paris.GetUtcOffset(new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero)));

            // Autumn: opened Wednesday 21 October 2026, summer time ends Sunday 25 at 03:00 (a 25-hour day).
            var rig = Create(zone: paris, start: new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            rig.Day(20261021);
            Assert.AreEqual(20261026, rig.Journal.EndKey);
            Assert.AreEqual(5, rig.Journal.DaysLeft);
            var steps = new (int day, int hour, int minute, int left)[]
            {
                (20261022, 0, 30, 4), (20261023, 12, 0, 3), (20261024, 23, 30, 2), (20261025, 0, 30, 1), (20261025, 12, 0, 1),
                (20261025, 23, 30, 1),
            };
            foreach (var (day, hour, minute, left) in steps)
            {
                rig.Day(day, hour, minute);
                Assert.AreEqual(day, rig.Ctx.TodayKey, "Local day " + day + " " + hour + ":" + minute);
                Assert.AreEqual(left, rig.Journal.DaysLeft, "Days left on " + day + " at " + hour + ":" + minute);
                Assert.AreEqual(20261021, rig.Journal.StartKey);
            }
            rig.Day(20261026, 0, 30);
            Assert.AreEqual(20261026, rig.Journal.StartKey, "Monday 00:30 local, still Sunday in UTC: the new journal.");
            Assert.AreEqual(7, rig.Journal.DaysLeft);

            // Spring: opened Thursday 25 March 2027 (4 days), summer time starts Sunday 28 at 02:00 (a 23-hour day).
            Assert.AreEqual(TimeSpan.FromHours(1), paris.GetUtcOffset(new DateTimeOffset(2027, 3, 27, 10, 0, 0, TimeSpan.Zero)));
            Assert.AreEqual(TimeSpan.FromHours(2), paris.GetUtcOffset(new DateTimeOffset(2027, 3, 29, 10, 0, 0, TimeSpan.Zero)));
            rig = Create(zone: paris, start: new DateTimeOffset(2027, 3, 25, 11, 0, 0, TimeSpan.Zero));
            rig.Day(20270325);
            Assert.AreEqual(20270329, rig.Journal.EndKey);
            Assert.AreEqual(4, rig.Journal.DaysLeft);
            steps = new[]
            {
                (20270327, 23, 30, 2), (20270328, 0, 30, 1), (20270328, 12, 0, 1), (20270328, 23, 30, 1),
            };
            foreach (var (day, hour, minute, left) in steps)
            {
                rig.Day(day, hour, minute);
                Assert.AreEqual(day, rig.Ctx.TodayKey);
                Assert.AreEqual(left, rig.Journal.DaysLeft, "Days left on " + day + " at " + hour + ":" + minute);
            }
            rig.Day(20270329, 0, 30);
            Assert.AreEqual(20270329, rig.Journal.StartKey);
            Assert.AreEqual(7, rig.Journal.DaysLeft);
        }

        // ---------------- Rewards (section 5.1) ----------------

        [Test]
        public void Rewards_FiveGemsAndTwoTicketsAGoal_FifteenGemsAndAnEggComplete_FiftyGemsAWeek()
        {
            var rig = Create();
            var claimed = new List<int>();
            rig.Journal.Claimed += (id, granted) => claimed.Add(id);
            NewWeek(rig, NextMonday);
            Assert.AreEqual(7, rig.Journal.GoalCount);
            Assert.IsFalse(rig.Journal.TryClaimGoal(0, out _), "Not reached yet.");
            Assert.IsFalse(rig.Journal.CanClaimComplete);
            Assert.IsFalse(rig.Journal.HasClaimable);

            ReachAll(rig);
            Assert.AreEqual(7, rig.Journal.ReachedCount);
            Assert.IsTrue(rig.Journal.HasClaimable);
            Assert.IsFalse(rig.Journal.TryClaimComplete(out _), "Every goal must be claimed first.");
            for (int i = 0; i < 7; i++)
            {
                Assert.IsTrue(rig.Journal.TryClaimGoal(i, out var granted));
                Assert.AreEqual(5, granted.Bundle.Gems);
                Assert.AreEqual(0, granted.Bundle.Tickets);
                Assert.AreEqual(30.0, granted.Bundle.LightMinutes, 1e-9, "2 tickets: Light 15 min each before the Play button.");
                Assert.IsTrue(granted.Bundle.Substituted);
                Assert.IsFalse(granted.Light.IsZero);
                Assert.AreEqual(rig.Journal.Get(i).Goal, (ushort)claimed[claimed.Count - 1]);
                Assert.IsFalse(rig.Journal.TryClaimGoal(i, out _), "Once.");
            }
            Assert.AreEqual(35, rig.State.Wallet.Gems);
            Assert.AreEqual(0, rig.State.Inventory.Tickets);
            Assert.IsTrue(rig.Journal.CanClaimComplete);
            Assert.IsTrue(rig.Journal.TryClaimComplete(out var complete));
            Assert.AreEqual(15, complete.Bundle.Gems);
            Assert.AreEqual(1, complete.Bundle.DiscoveryEggs);
            Assert.AreEqual(1, rig.State.World.DiscoveryEggs, "The Discovery egg reaches the stock.");
            Assert.AreEqual(0, claimed[claimed.Count - 1]);
            Assert.AreEqual(50, rig.State.Wallet.Gems, "50 Gems a week.");
            Assert.IsFalse(rig.Journal.TryClaimComplete(out _));
            Assert.IsFalse(rig.Journal.HasClaimable);

            // Nothing is carried once everything was claimed.
            rig.Day(DailyDay.AddDays(NextMonday, 7));
            Assert.IsFalse(rig.Journal.HasCarry);

            // With the Play button, real tickets.
            rig = Create(cfg => cfg.ActivitiesAvailable = true);
            NewWeek(rig, NextMonday);
            ReachAll(rig);
            for (int i = 0; i < 7; i++)
            {
                Assert.IsTrue(rig.Journal.TryClaimGoal(i, out var granted));
                Assert.AreEqual(2, granted.Bundle.Tickets);
                Assert.AreEqual(0.0, granted.Bundle.LightMinutes);
            }
            Assert.AreEqual(14, rig.State.Inventory.Tickets);
        }

        // ---------------- Progress hooks ----------------

        private static void Manual(Rig rig, params JournalGoalId[] goals)
        {
            var visit = rig.Visit;
            for (int i = 0; i < goals.Length; i++)
                visit.Goals[i] = new JournalGoalRecord { Goal = (ushort)goals[i], Slot = (byte)JournalSlot.Free, Target = 1000 };
            visit.GoalCount = (byte)goals.Length;
            visit.JournalStartKey = rig.Ctx.TodayKey;
            visit.JournalEndKey = DailyDay.AddDays(rig.Ctx.TodayKey, 7);
            visit.FirstJournalDone = true;
        }

        private static int[] Progress(Rig rig)
        {
            var progress = new int[rig.Journal.GoalCount];
            for (int i = 0; i < progress.Length; i++)
                progress[i] = rig.Journal.Get(i).Progress;
            return progress;
        }

        [Test]
        public void Hooks_CountTheirGoals_OnlyWhileTheWeekRuns()
        {
            var rig = Create();
            rig.SetClock(NextMonday);
            Manual(rig, JournalGoalId.HatchEggs, JournalGoalId.DiscoverVariants, JournalGoalId.MasteryLevels, JournalGoalId.NewSpecies,
                JournalGoalId.PreciousEggs, JournalGoalId.Wishes, JournalGoalId.DailyGifts, JournalGoalId.CalendarDays, JournalGoalId.BondPoints,
                JournalGoalId.TreatsGiven);
            var j = rig.Journal;

            j.OnHatched(Hatched(Basic));
            CollectionAssert.AreEqual(new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, Progress(rig), "A hatch.");
            j.OnHatched(Hatched(Basic, newVariant: true));
            CollectionAssert.AreEqual(new[] { 2, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, Progress(rig), "A new variant.");
            j.OnHatched(Hatched(Basic, newSpecies: true, newVariant: true));
            CollectionAssert.AreEqual(new[] { 3, 2, 0, 1, 0, 0, 0, 0, 0, 0 }, Progress(rig), "A new species is a new variant too.");
            j.OnHatched(Hatched(Focus));
            CollectionAssert.AreEqual(new[] { 4, 2, 0, 1, 1, 0, 0, 0, 0, 0 }, Progress(rig), "A Focus egg is a precious egg.");
            j.OnHatched(HatchResult.Failed(HatchStatus.NotEnoughLight, Basic, BigValue.One));
            CollectionAssert.AreEqual(new[] { 4, 2, 0, 1, 1, 0, 0, 0, 0, 0 }, Progress(rig), "A failed hatch counts nothing.");
            j.OnMasteryLeveled(Rabbit, 2);
            j.OnCodexPageOpened(Rabbit);
            j.OnWishCompleted();
            j.OnGiftOpened();
            j.OnCalendarStamped();
            j.OnBondGained(7, 15);
            j.OnTreatGiven(7);
            CollectionAssert.AreEqual(new[] { 4, 2, 2, 1, 1, 1, 1, 1, 15, 1 }, Progress(rig));

            Manual(rig, JournalGoalId.AnimalsTapped, JournalGoalId.Upgrades, JournalGoalId.Expansions);
            j.OnAnimalTouched(7, true);
            j.OnAnimalTouched(7, false);
            j.OnUpgradesBought(10);
            j.OnZoneOpened();
            CollectionAssert.AreEqual(new[] { 1, 10, 1 }, Progress(rig), "Only touches inside the bond caps count; every level bought counts.");
            j.OnHatched(Hatched(Focus, newSpecies: true, newVariant: true));
            j.OnBondGained(7, 50);
            CollectionAssert.AreEqual(new[] { 1, 10, 1 }, Progress(rig), "Goals not in the journal are ignored.");

            // The week is over (before the facade's renewal): nothing more counts.
            rig.Ctx.TodayKey = rig.Visit.JournalEndKey;
            j.OnAnimalTouched(7, true);
            j.OnUpgradesBought(10);
            CollectionAssert.AreEqual(new[] { 1, 10, 1 }, Progress(rig));
        }

        [Test]
        public void HooksQueriesAndAnOrdinaryDay_DoNotAllocate()
        {
            var rig = Create();
            rig.Day(Sunday);
            int reachedEvents = 0;
            rig.Journal.GoalReached += i => reachedEvents++;
            var journal = rig.Journal;
            var plain = Hatched(Basic);
            var focus = Hatched(Focus, newSpecies: true, newVariant: true);
            int today = rig.Ctx.TodayKey;
            journal.OnHatched(plain);
            journal.OnHatched(focus);
            journal.Ensure(today);
            Assert.AreEqual(Sunday, journal.StartKey);
            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    journal.OnHatched(plain);
                    journal.OnHatched(focus);
                    journal.OnMasteryLeveled(Rabbit, 1);
                    journal.OnCodexPageOpened(Rabbit);
                    journal.OnAnimalTouched(3, true);
                    journal.OnTreatGiven(3);
                    journal.OnBondGained(3, 2);
                    journal.OnUpgradesBought(1);
                    journal.OnZoneOpened();
                    journal.OnWishCompleted();
                    journal.OnGiftOpened();
                    journal.OnCalendarStamped();
                    journal.Ensure(today);
                    if (journal.HasClaimable && journal.CanClaimComplete && journal.HasCarry)
                        reachedEvents--;
                    reachedEvents += journal.DaysLeft + journal.ReachedCount + journal.GoalCount;
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.Greater(reachedEvents, 0);
        }
    }
}
