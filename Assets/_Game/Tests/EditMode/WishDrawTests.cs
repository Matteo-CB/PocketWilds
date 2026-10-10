using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>Records what the wishes gave as bond (the real bond service lives with the facade).</summary>
    internal sealed class WishTestBondSink : IBondSink
    {
        public int Calls;
        public uint LastAnimal;
        public int LastPoints;
        public BondSource LastSource;

        public int AddBond(uint animal, int points, BondSource source)
        {
            Calls++;
            LastAnimal = animal;
            LastPoints = points;
            LastSource = source;
            return points;
        }
    }

    /// <summary>
    /// A small refuge for the wish tests: the five Prairie species (three variants each) and a Forest species, the Prairie
    /// Basic egg and its Focus egg, real economy, hatch, species mastery and progression services, a UTC calendar.
    /// </summary>
    internal sealed class WishTestRig
    {
        public static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        public static readonly ContentId Forest = ContentId.FromSource("biome.forest");
        public static readonly ContentId BasicEgg = ContentId.FromSource("egg.grassland.basic");
        public static readonly ContentId FocusEgg = ContentId.FromSource("egg.grassland.focus");
        public static readonly ContentId CloverCrunch = ContentId.FromSource("treat.clover_crunch");
        public static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        public static readonly ContentId Squirrel = ContentId.FromSource("species.squirrel");

        public PlayerState State;
        public FakeGameClock Clock;
        public ContentRegistry Registry;
        public ProgressionConfig ProgressionConfig;
        public DailyVisitConfig Config;
        public EconomyService Economy;
        public MasteryService SpeciesMastery;
        public HatchService Hatch;
        public ProgressionServices P;
        public DailyContext Ctx;
        public WishTestBondSink Bond;
        public WishService Wishes;

        /// <summary>
        /// <paramref name="animals"/> Prairie hatches (the first one free), then, with <paramref name="boost"/>, Prairie mastery
        /// 6 (the Focus egg, Cozy Nests and the first zone open) and refuge level 2 at least.
        /// </summary>
        public static WishTestRig Create(int profile = 1, int animals = 6, bool onboarded = true, DailyVisitConfig config = null,
            bool boost = true)
        {
            var rig = new WishTestRig
            {
                Registry = BuildContent(),
                ProgressionConfig = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc },
                Config = config ?? new DailyVisitConfig(),
                State = PlayerState.CreateNew(TestUtil.T0),
                Clock = new FakeGameClock(TestUtil.T0),
                Bond = new WishTestBondSink(),
            };
            rig.State.Meta.ProfileId = new Guid(profile, 7, 11, 1, 2, 3, 4, 5, 6, 7, 8);
            rig.Economy = new EconomyService(rig.State, rig.Registry, EconomyConfig.Default, null);
            rig.SpeciesMastery = new MasteryService(rig.State, rig.Economy.Config, rig.Economy);
            rig.Hatch = new HatchService(rig.State, rig.Registry, rig.Economy, rig.Clock, null, rig.SpeciesMastery);
            rig.P = new ProgressionServices(rig.State, rig.Registry, rig.Economy, rig.Hatch, rig.Clock, null, rig.ProgressionConfig,
                rig.SpeciesMastery);
            rig.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e12), EconomyReason.Debug);
            for (int i = 0; i < animals; i++)
                Assert.IsTrue(rig.Hatch.Hatch(BasicEgg).Succeeded, "Rig hatch " + i);
            if (boost)
            {
                rig.P.Mastery.AddXp(Grassland, 600);
                if (rig.State.Progression.Refuge.Level < 2)
                    rig.State.Progression.Refuge.Level = 2;
            }
            rig.State.DailyVisit.OnboardingDone = onboarded;
            rig.Ctx = new DailyContext(rig.State, rig.Registry, rig.Config, rig.ProgressionConfig, rig.Clock, rig.Economy, rig.Hatch,
                rig.SpeciesMastery, rig.P, null);
            rig.Wishes = new WishService(rig.Ctx, rig.Bond);
            return rig;
        }

        public static ContentRegistry BuildContent()
        {
            var builder = new ContentRegistryBuilder();
            string[] grassland = { "rabbit", "squirrel", "hedgehog", "mole", "duckling" };
            foreach (var name in grassland)
                AddSpecies(builder, name, Grassland);
            AddSpecies(builder, "fawn", Forest);
            var pool = grassland.Select(n => new EggDropEntry(ContentId.FromSource("species." + n), 1.0)).ToArray();
            var table = new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 };
            builder.AddEgg(new EggDefinition(BasicEgg, "egg.grassland.basic.name", Grassland, EggTier.Basic,
                new Color32[EggDefinition.PaletteSize], 15, 0, 15, "PF_Egg_Base", 10.0, 1.18, table, pool), "egg.grassland.basic");
            builder.AddEgg(new EggDefinition(FocusEgg, "egg.grassland.focus.name", Grassland, EggTier.Focus,
                new Color32[EggDefinition.PaletteSize], 15, 15, 15, "PF_Egg_Base", 30.0, 1.18, table, pool,
                unlockBiomeMastery: 3, focusTargetCount: 2), "egg.grassland.focus");
            return builder.Build();
        }

        private static void AddSpecies(ContentRegistryBuilder builder, string name, ContentId biome)
        {
            var species = ContentId.FromSource("species." + name);
            builder.AddSpecies(new SpeciesDefinition(species, "species." + name + ".name", RigFamily.SmallQuadruped, biome, 1.0, "PF_Animal_Test"),
                "species." + name);
            foreach (var (variant, rarity) in new[] { ("brown", RarityTier.Common), ("white", RarityTier.Common), ("spotted", RarityTier.Rare) })
            {
                string id = "variant." + name + "." + variant;
                builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), species, id + ".name", rarity,
                    new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), id);
            }
        }

        /// <summary>Moves the clock to day N (day 1 = install day) and today's key with it.</summary>
        public int SetDay(int day)
        {
            Clock.Set(TestUtil.T0 + TimeSpan.FromDays(day - 1));
            Ctx.TodayKey = DailyDay.DayKey(DailyDay.Now(State, Clock), ProgressionConfig.TimeZone, Config.ResetHour);
            return Ctx.TodayKey;
        }

        public uint AnimalId(int index) => State.Collection[index].InstanceId;

        /// <summary>A new service on the same state, as after a load.</summary>
        public WishService NewWishService()
        {
            Wishes = new WishService(Ctx, Bond);
            return Wishes;
        }

        /// <summary>Writes a wish into the stock as a save would hold it.</summary>
        public int AddWish(WishTemplateId template, uint animal, int target, int dayKey, ContentId param = default, byte extra = 0,
            bool done = false, bool tutorial = false)
        {
            var visit = State.DailyVisit;
            int slot = visit.WishCount++;
            visit.Wishes[slot] = new WishRecord
            {
                Template = (ushort)template,
                Animal = animal,
                Target = target,
                Progress = done ? target : 0,
                DrawnDay = dayKey,
                Param = param,
                ParamExtra = extra,
                ParamKind = WishCatalog.TryGet(template, out var t) ? (byte)t.ParamKind : (byte)0,
                Flags = (byte)((done ? WishRecord.FlagDone : 0) | (tutorial ? WishRecord.FlagTutorial : 0)),
            };
            return slot;
        }

        public List<WishRecord> Stock()
        {
            var list = new List<WishRecord>();
            for (int i = 0; i < Wishes.PendingCount; i++)
                list.Add(Wishes.Get(i));
            return list;
        }

        public WishRecord Find(WishTemplateId template) => Stock().First(w => w.Template == (ushort)template);

        public static WishTemplate TemplateOf(in WishRecord wish)
        {
            Assert.IsTrue(WishCatalog.TryGet((WishTemplateId)wish.Template, out var template), "Unknown template " + wish.Template);
            return template;
        }

        public static WishSystem SystemOf(in WishRecord wish) => TemplateOf(wish).System;
    }

    /// <summary>
    /// T36, doc « 01 — Contenu des rendez-vous, des activités et de l'accueil », sections 2.2 to 2.4 (plan section 8, item 4):
    /// the catalog, the same-day rules and their relaxation order, k = min(3, 9 - pending), no duplicate template, fewer
    /// systems or candidates giving fewer wishes, day 1, the weight multipliers, the animal draw and the amounts by tier,
    /// over many simulated days; the draw neither rerolls nor allocates.
    /// </summary>
    public class WishDrawTests
    {
        // T36: SYS-01, 02, 03, 04 and 06; T38 adds SYS-09 Grooming (WISH-019 to 022), T39 SYS-10 Hide and Seek (WISH-051 to
        // 053), T40 SYS-11 Nut Drop (WISH-054 to 056), T41 SYS-12 Pawprint Memory (WISH-057 to 059), T42 SYS-13 Expeditions
        // (WISH-041 to 046) and T43 SYS-07 the Garden (WISH-031 to 036), each with its activity.
        private static readonly int[] Shipped =
        {
            1, 2, 3, 4, 5, 6, 7, 14, 15, 16, 17, 18, 19, 20, 21, 22, 25, 26, 30, 31, 32, 33, 34, 35, 36, 41, 42, 43, 44, 45, 46, 51, 52, 53,
            54, 55, 56, 57, 58, 59,
        };

        /// <summary>
        /// T42's expedition wishes and T43's garden wishes: never eligible in a rig without the activities nor the Garden (the
        /// stock tests leave them out).
        /// </summary>
        private static bool IsExpeditionWish(int id) =>
            (id >= (int)WishTemplateId.StartExpedition && id <= (int)WishTemplateId.HeadToDestination)
            || (id >= (int)WishTemplateId.PlantSeeds && id <= (int)WishTemplateId.HarvestDye);

        // ---------------- Catalog (section 2.3) ----------------

        [Test]
        public void Catalog_HoldsTheDocTemplates()
        {
            var all = WishCatalog.All;
            Assert.AreEqual(83, all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                Assert.AreEqual(i + 1, (int)t.Id, "Doc order: WISH-001 = 1 … WISH-083 = 83.");
                Assert.Greater(t.Weight, 0, t.Id.ToString());
                Assert.AreEqual(WishSystems.FamilyOf(t.System), t.Family);
                Assert.AreEqual(!t.Timer && t.TypicalSeconds <= 120, t.Quick, t.Id + ": quick is 2 min or less, no timer.");
                Assert.AreEqual(Shipped.Contains(i + 1), t.ShippedInThisBuild, t.Id + " ships with SYS-01, 02, 03, 04, 06, 07, 09, 10, 11, 12 and 13 only.");
                Assert.AreEqual("wish." + (i + 1).ToString("000"), WishCatalog.TextKey(t.Id));
            }

            AssertTemplate(WishTemplateId.HatchEggs, WishSystem.Hatching, 10, 3, 5, 8, 10, quick: true);
            AssertTemplate(WishTemplateId.HatchBiomeEggs, WishSystem.Hatching, 6, 2, 3, 4, 5, quick: false, param: WishParamKind.Biome);
            AssertTemplate(WishTemplateId.HatchPreciousEgg, WishSystem.Hatching, 5, 1, 1, 1, 1, quick: false);
            AssertTemplate(WishTemplateId.DiscoverVariant, WishSystem.DiscoveriesAndMastery, 6, 1, 1, 1, 1, quick: false);
            AssertTemplate(WishTemplateId.RaiseSpeciesMastery, WishSystem.DiscoveriesAndMastery, 4, 1, 1, 1, 1, quick: false, param: WishParamKind.Species);
            AssertTemplate(WishTemplateId.LookAtSpeciesInCodex, WishSystem.DiscoveriesAndMastery, 3, 1, 1, 1, 1, quick: true, param: WishParamKind.Species);
            AssertTemplate(WishTemplateId.DiscoverSpecies, WishSystem.DiscoveriesAndMastery, 3, 1, 1, 1, 1, quick: false);
            AssertTemplate(WishTemplateId.TapAnimals, WishSystem.CuddlesAndTreats, 8, 5, 8, 10, 12, quick: true);
            AssertTemplate(WishTemplateId.CuddleAnimal, WishSystem.CuddlesAndTreats, 6, 3, 3, 3, 3, quick: true, param: WishParamKind.Animal);
            AssertTemplate(WishTemplateId.GiveAnimalTreat, WishSystem.CuddlesAndTreats, 7, 1, 1, 1, 1, quick: true, param: WishParamKind.Animal);
            AssertTemplate(WishTemplateId.GiveTreats, WishSystem.CuddlesAndTreats, 4, 2, 3, 3, 4, quick: true);
            AssertTemplate(WishTemplateId.EarnBond, WishSystem.CuddlesAndTreats, 4, 20, 30, 40, 50, quick: false);
            AssertTemplate(WishTemplateId.BuyUpgrades, WishSystem.Upgrades, 6, 1, 2, 3, 3, quick: false);
            AssertTemplate(WishTemplateId.UpgradeInBiome, WishSystem.Upgrades, 4, 1, 1, 1, 1, quick: false, param: WishParamKind.Upgrade);
            AssertTemplate(WishTemplateId.ExpandBiome, WishSystem.Expansions, 5, 1, 1, 1, 1, quick: false);
            AssertTemplate(WishTemplateId.NutDropCatch, WishSystem.NutRain, 4, 30, 40, 50, 60, quick: true);
            AssertTemplate(WishTemplateId.StartExpedition, WishSystem.Expeditions, 7, 1, 1, 1, 1, quick: true);
            AssertTemplate(WishTemplateId.SendAnimalOnExpedition, WishSystem.Expeditions, 4, 1, 1, 1, 1, quick: true, param: WishParamKind.Animal);
            AssertTemplate(WishTemplateId.HeadToDestination, WishSystem.Expeditions, 3, 1, 1, 1, 1, quick: true, param: WishParamKind.Destination);
            AssertTemplate(WishTemplateId.VoteGrandContest, WishSystem.GrandContest, 3, 5, 10, 10, 15, quick: true);
            Assert.IsTrue(all[(int)WishTemplateId.NutDropCatch - 1].Score);
            Assert.IsTrue(all[(int)WishTemplateId.WelcomeNewborn - 1].Timer && all[(int)WishTemplateId.WelcomeNewborn - 1].Long);
            Assert.IsTrue(all[(int)WishTemplateId.HarvestPlants - 1].Timer);
            Assert.IsTrue(all[(int)WishTemplateId.DiscoverSpecies - 1].Long, "20 minutes is long.");
            Assert.AreEqual(8, all.Count(t => t.Timer), "WISH-009, 012, 013, 032, 034, 035, 036, 045.");

            Assert.IsTrue(WishCatalog.TryGet(WishTemplateId.Tutorial, out var tutorial));
            Assert.AreEqual(WishSystem.Hatching, tutorial.System);
            Assert.AreEqual(2, tutorial.P1, "WISH-T01 « Fais éclore 2 œufs ».");
            Assert.AreEqual("wish.t01", WishCatalog.TextKey(WishTemplateId.Tutorial));
            Assert.AreEqual(-1, WishCatalog.IndexOf(WishTemplateId.Tutorial), "The tutorial wish is never in the daily draw.");
            Assert.IsFalse(WishCatalog.TryGet(WishTemplateId.None, out _));
            Assert.IsFalse(WishCatalog.TryGet((WishTemplateId)84, out _));
        }

        private static void AssertTemplate(WishTemplateId id, WishSystem system, int weight, int p1, int p2, int p3, int p4, bool quick,
            WishParamKind param = WishParamKind.None)
        {
            Assert.IsTrue(WishCatalog.TryGet(id, out var t));
            Assert.AreEqual(system, t.System, id.ToString());
            Assert.AreEqual(weight, t.Weight, id.ToString());
            Assert.AreEqual(new[] { p1, p2, p3, p4 }, new[] { t.Amount(1), t.Amount(2), t.Amount(3), t.Amount(4) }, id.ToString());
            Assert.AreEqual(quick, t.Quick, id.ToString());
            Assert.AreEqual(param, t.ParamKind, id.ToString());
        }

        [Test]
        public void Tiers_AndTheScoreRule_FollowSectionsZeroAndTwoThree()
        {
            Assert.AreEqual(1, WishCatalog.TierOf(1));
            Assert.AreEqual(1, WishCatalog.TierOf(14));
            Assert.AreEqual(2, WishCatalog.TierOf(15));
            Assert.AreEqual(2, WishCatalog.TierOf(39));
            Assert.AreEqual(3, WishCatalog.TierOf(40));
            Assert.AreEqual(3, WishCatalog.TierOf(69));
            Assert.AreEqual(4, WishCatalog.TierOf(70));
            Assert.AreEqual(4, WishCatalog.TierOf(500));

            Assert.AreEqual(50, WishCatalog.ScoreTarget(50, 0, 30), "No record: the tier value.");
            Assert.AreEqual(30, WishCatalog.ScoreTarget(50, 40, 30), "80 % of 40, to a multiple of 5.");
            Assert.AreEqual(35, WishCatalog.ScoreTarget(50, 44, 30), "35.2 rounds down to 35.");
            Assert.AreEqual(30, WishCatalog.ScoreTarget(50, 20, 30), "Never below P1.");
            Assert.AreEqual(50, WishCatalog.ScoreTarget(50, 100, 30), "A high record keeps the tier value.");
        }

        // ---------------- Same-day rules, pure (section 2.4) ----------------

        private static WishDaySet Day(params (WishSystem system, bool quick, bool timer)[] wishes)
        {
            var day = new WishDaySet();
            foreach (var (system, quick, timer) in wishes)
                day.Add(system, quick, timer);
            return day;
        }

        private static int Pick(WishSystem[] systems, bool[] quick, bool[] timer, double[] weights, WishDaySet today, bool last, int open,
            uint seed, out int relaxed)
        {
            var rng = new DeterministicRandom(seed);
            return WishDrawRules.Pick(systems.Length, systems, quick, timer, weights, in today, last, open, ref rng, out relaxed);
        }

        [Test]
        public void Rules_NeverTwoOfTheSameSystem_UnlessFewerThanThreeAreOpen()
        {
            var today = Day((WishSystem.Hatching, true, false));
            var systems = new[] { WishSystem.Hatching };
            var quick = new[] { true };
            var timer = new[] { false };
            var weights = new[] { 1.0 };
            Assert.AreEqual(-1, Pick(systems, quick, timer, weights, today, false, 5, 1, out _), "Five systems open: never a second SYS-01.");
            Assert.AreEqual(-1, Pick(systems, quick, timer, weights, today, false, 3, 1, out _));
            Assert.AreEqual(0, Pick(systems, quick, timer, weights, today, false, 2, 1, out int relaxed), "Two systems open: the rule is lifted.");
            Assert.AreEqual(WishDrawRules.RelaxedSystem, relaxed);

            // Lifting the system rule keeps the others at full strength: the last slot still goes to a quick wish.
            today = Day((WishSystem.Hatching, false, false), (WishSystem.CuddlesAndTreats, false, false));
            systems = new[] { WishSystem.Hatching, WishSystem.Hatching };
            quick = new[] { true, false };
            timer = new[] { false, false };
            weights = new[] { 1.0, 100.0 };
            for (uint seed = 1; seed <= 300; seed++)
            {
                Assert.AreEqual(0, Pick(systems, quick, timer, weights, today, true, 2, seed, out relaxed));
                Assert.AreEqual(WishDrawRules.RelaxedSystem, relaxed);
            }
        }

        [Test]
        public void Rules_AtMostTwoOfAFamily_ThenRelaxFamilyFirst()
        {
            // Two Collection wishes today; a third Collection candidate (Breeding, quick) and a Refuge one that is not quick.
            var today = Day((WishSystem.Hatching, false, false), (WishSystem.DiscoveriesAndMastery, false, false));
            var systems = new[] { WishSystem.Breeding, WishSystem.Upgrades };
            var quick = new[] { true, false };
            var timer = new[] { false, false };
            var weights = new[] { 1.0, 100.0 };
            for (uint seed = 1; seed <= 300; seed++)
            {
                Assert.AreEqual(0, Pick(systems, quick, timer, weights, today, true, 6, seed, out int relaxed),
                    "Family is lifted before quick: the quick Collection wish wins.");
                Assert.AreEqual(WishDrawRules.RelaxedFamily, relaxed);
            }
            // Not the last slot: the family rule alone decides, the Refuge wish is the only one allowed.
            for (uint seed = 1; seed <= 300; seed++)
            {
                Assert.AreEqual(1, Pick(systems, quick, timer, weights, today, false, 6, seed, out int relaxed));
                Assert.AreEqual(0, relaxed);
            }
        }

        [Test]
        public void Rules_RelaxQuickBeforeTimer()
        {
            // Today: a Hatching wish and a timer expedition, none quick; last slot.
            var today = Day((WishSystem.Hatching, false, false), (WishSystem.Expeditions, false, true));
            // A: Upgrades, not quick (breaks « quick » only). B: Grooming, quick with a timer (breaks « one timer » only).
            var systems = new[] { WishSystem.Upgrades, WishSystem.Grooming };
            var quick = new[] { false, true };
            var timer = new[] { false, true };
            var weights = new[] { 1.0, 100.0 };
            for (uint seed = 1; seed <= 300; seed++)
            {
                Assert.AreEqual(0, Pick(systems, quick, timer, weights, today, true, 6, seed, out int relaxed));
                Assert.AreEqual(WishDrawRules.RelaxedFamily | WishDrawRules.RelaxedQuick, relaxed);
            }
            Assert.AreEqual(0, Pick(new[] { WishSystem.Grooming }, new[] { true }, new[] { true }, new[] { 1.0 }, today, true, 6, 9, out int last));
            Assert.AreEqual(WishDrawRules.RelaxedFamily | WishDrawRules.RelaxedQuick | WishDrawRules.RelaxedTimer, last,
                "The timer rule goes last.");
        }

        [Test]
        public void Rules_TheLastSlotIsQuick_AndOneTimerAtMost()
        {
            var today = Day((WishSystem.Hatching, false, false), (WishSystem.DiscoveriesAndMastery, false, false));
            var systems = new[] { WishSystem.CuddlesAndTreats, WishSystem.Upgrades };
            var quick = new[] { true, false };
            var timer = new[] { false, false };
            var weights = new[] { 1.0, 100.0 };
            int nonQuick = 0;
            for (uint seed = 1; seed <= 500; seed++)
            {
                Assert.AreEqual(0, Pick(systems, quick, timer, weights, today, true, 6, seed, out _), "Third wish among quick ones.");
                if (Pick(systems, quick, timer, weights, today, false, 6, seed, out _) == 1)
                    nonQuick++;
            }
            Assert.Greater(nonQuick, 450, "Before the last slot, the weights decide.");

            var withTimer = Day((WishSystem.Expeditions, true, true));
            systems = new[] { WishSystem.Garden, WishSystem.Upgrades };
            timer = new[] { true, false };
            weights = new[] { 100.0, 1.0 };
            for (uint seed = 1; seed <= 300; seed++)
                Assert.AreEqual(1, Pick(systems, new[] { true, true }, timer, weights, withTimer, false, 6, seed, out _), "One timer a day.");
        }

        [Test]
        public void Rules_PickFollowsTheWeights()
        {
            var today = new WishDaySet();
            var systems = new[] { WishSystem.Hatching, WishSystem.CuddlesAndTreats };
            var flags = new[] { true, true };
            var none = new[] { false, false };
            var weights = new[] { 1.0, 3.0 };
            var rng = new DeterministicRandom(42);
            int[] counts = new int[2];
            for (int i = 0; i < 40000; i++)
                counts[WishDrawRules.Pick(2, systems, flags, none, weights, in today, false, 5, ref rng, out _)]++;
            Assert.AreEqual(3.0, counts[1] / (double)counts[0], 0.15);
        }

        // ---------------- The daily draw on a real refuge ----------------

        [Test]
        public void DailyDraw_GivesThreeWishesOfThreeSystems_WithAQuickOne_OnceADay()
        {
            var rig = WishTestRig.Create();
            int key = rig.SetDay(2);
            Assert.AreEqual(3, rig.Wishes.DrawForDay(key));
            var stock = rig.Stock();
            Assert.AreEqual(3, stock.Select(w => WishTestRig.SystemOf(w)).Distinct().Count());
            Assert.IsTrue(stock.Any(w => WishTestRig.TemplateOf(w).Quick));
            Assert.AreEqual(3, stock.Select(w => w.Animal).Distinct().Count());
            foreach (var wish in stock)
            {
                Assert.IsTrue(rig.State.Collection.TryGetIndex(wish.Animal, out _));
                Assert.AreEqual(key, wish.DrawnDay);
                Assert.AreEqual(0, wish.Progress);
                Assert.IsFalse(wish.IsDone || wish.IsTutorial);
                Assert.IsTrue(WishTestRig.TemplateOf(wish).ShippedInThisBuild);
                Assert.AreNotEqual((ushort)WishTemplateId.HatchBiomeEggs, wish.Template, "Only the Prairie is open: WISH-002 never passes.");
            }
            Assert.AreEqual(key, rig.State.DailyVisit.WishDrawDayKey);
            Assert.AreEqual(0, rig.Wishes.DrawForDay(key), "One draw a day.");
            Assert.AreEqual(0, rig.Wishes.DrawForDay(DailyDay.AddDays(key, -1)), "A day key never goes back.");
        }

        [Test]
        public void NothingIsDrawn_BeforeTheWelcomeEnds()
        {
            var rig = WishTestRig.Create(animals: 3, onboarded: false);
            int key = rig.SetDay(1);
            Assert.IsFalse(rig.State.IsOnboardingDone);
            Assert.AreEqual(0, rig.Wishes.DrawForDay(key));
            Assert.AreEqual(0, rig.State.DailyVisit.WishDrawDayKey, "The day is not used up.");
            Assert.IsFalse(rig.Wishes.IsSystemOpen(WishSystem.Hatching));
            rig.State.DailyVisit.OnboardingDone = true;
            Assert.Greater(rig.Wishes.DrawFirstDay(key), 0, "Once the welcome ends, the same day draws.");
            Assert.AreEqual(1, rig.State.DailyVisit.SystemUnlockDay[(int)WishSystem.Hatching]);
        }

        [Test]
        public void K_IsTheSmallerOfThreeAndTheRoomInTheStock()
        {
            var rig = WishTestRig.Create();
            int day1 = rig.SetDay(1);
            // Seven pending wishes of systems this build does not draw (their animals elsewhere).
            for (int i = 0; i < 7; i++)
                rig.AddWish((WishTemplateId)(51 + i), 100000u + (uint)i, 1, day1);
            int key = rig.SetDay(2);
            Assert.AreEqual(2, rig.Wishes.DrawForDay(key), "min(3, 9 - 7)");
            Assert.AreEqual(9, rig.Wishes.PendingCount);
            key = rig.SetDay(3);
            Assert.AreEqual(0, rig.Wishes.DrawForDay(key), "« Tes souhaits t'attendent »: nothing drawn, nothing lost.");
            Assert.AreEqual(9, rig.Wishes.PendingCount);
            Assert.AreEqual(key, rig.State.DailyVisit.WishDrawDayKey);

            var four = new DailyVisitConfig();
            Assert.IsTrue(four.TryOverride("daily.wishes_per_day", 4));
            Assert.AreEqual(12, four.WishStock);
            var rig4 = WishTestRig.Create(config: four);
            Assert.AreEqual(4, rig4.Wishes.DrawForDay(rig4.SetDay(2)), "Remote Config daily.wishes_per_day = 4.");
            Assert.AreEqual(4, rig4.Stock().Select(w => WishTestRig.SystemOf(w)).Distinct().Count());
        }

        [Test]
        public void FewerCandidates_GiveFewerWishes_NeverADuplicate()
        {
            var config = new DailyVisitConfig();
            Assert.IsTrue(config.TryOverride("daily.wishes_stock_days", 7));
            // T41: 28 shipped templates (27 pending below; T42's expedition and T43's garden wishes are never eligible here); a stock of 4 × 7 = 28
            // keeps room for the one left.
            Assert.IsTrue(config.TryOverride("daily.wishes_per_day", 4));
            var rig = WishTestRig.Create(config: config);
            int day1 = rig.SetDay(1);
            uint stranger = 200000;
            foreach (int id in Shipped)
                if (id != (int)WishTemplateId.CuddleAnimal && !IsExpeditionWish(id))
                    rig.AddWish((WishTemplateId)id, stranger++, 1, day1);
            int key = rig.SetDay(2);
            Assert.AreEqual(1, rig.Wishes.DrawForDay(key), "One template left: one wish, never a second copy.");
            Assert.AreEqual((ushort)WishTemplateId.CuddleAnimal, rig.Wishes.Get(rig.Wishes.PendingCount - 1).Template);
            var stock = rig.Stock();
            Assert.AreEqual(stock.Count, stock.Select(w => w.Template).Distinct().Count());
            Assert.AreEqual(0, rig.Wishes.DrawForDay(rig.SetDay(3)), "Every shipped template is pending: nothing to draw.");
        }

        [Test]
        public void FirstDay_DrawsAmongHatchingDiscoveriesCuddlesAndUpgrades_WithAQuickCuddleWish()
        {
            int expansionsLater = 0;
            for (int profile = 1; profile <= 40; profile++)
            {
                var rig = WishTestRig.Create(profile);
                int key = rig.SetDay(1);
                rig.Wishes.RefreshSystemUnlocks();
                Assert.IsTrue(rig.Wishes.IsSystemOpen(WishSystem.Expansions), "Precondition: an expansion is offered, SYS-06 is open.");
                Assert.AreEqual(3, rig.Wishes.DrawFirstDay(key));
                var stock = rig.Stock();
                Assert.IsTrue(stock.All(w => WishTestRig.SystemOf(w) <= WishSystem.Upgrades), "Day 1: SYS-01 to SYS-04 only.");
                Assert.IsTrue(stock.Any(w => WishTestRig.SystemOf(w) == WishSystem.CuddlesAndTreats && WishTestRig.TemplateOf(w).Quick),
                    "Day 1 gives a quick SYS-03 wish.");

                rig.State.DailyVisit.WishCount = 0;
                Assert.AreEqual(3, rig.Wishes.DrawForDay(rig.SetDay(2)));
                if (rig.Stock().Any(w => WishTestRig.SystemOf(w) == WishSystem.Expansions))
                    expansionsLater++;
            }
            Assert.Greater(expansionsLater, 0, "From day 2, expansion wishes come out.");
        }

        [Test]
        public void Multipliers_NewSystem_Repeat_AndStock()
        {
            var rig = WishTestRig.Create();
            var visit = rig.State.DailyVisit;
            int day1 = rig.SetDay(1);
            Assert.AreEqual(10.0, rig.Wishes.DrawWeight(WishTemplateId.HatchEggs, day1), 1e-9, "Opening day unknown: the base weight.");
            rig.Wishes.RefreshSystemUnlocks();
            Assert.AreEqual(1, visit.SystemUnlockDay[(int)WishSystem.Hatching]);
            Assert.AreEqual(30.0, rig.Wishes.DrawWeight(WishTemplateId.HatchEggs, day1), 1e-9, "x 3 the day the system opens…");
            Assert.AreEqual(30.0, rig.Wishes.DrawWeight(WishTemplateId.HatchEggs, rig.SetDay(4)), 1e-9, "… and the 3 days after it.");
            int day5 = rig.SetDay(5);
            Assert.AreEqual(10.0, rig.Wishes.DrawWeight(WishTemplateId.HatchEggs, day5), 1e-9);

            visit.History[0] = new WishHistoryEntry { Template = (ushort)WishTemplateId.HatchEggs, DayKey = DailyDay.AddDays(day5, -3) };
            visit.HistoryCount = 1;
            visit.HistoryNext = 1;
            Assert.AreEqual(3.0, rig.Wishes.DrawWeight(WishTemplateId.HatchEggs, day5), 1e-9, "x 0.3 when drawn in the last 3 days.");
            int day6 = rig.SetDay(6);
            Assert.AreEqual(10.0, rig.Wishes.DrawWeight(WishTemplateId.HatchEggs, day6), 1e-9, "4 days ago no longer counts.");

            int slot = rig.AddWish(WishTemplateId.EarnBond, 300000, 20, DailyDay.AddDays(day6, -1));
            Assert.AreEqual(2.0, rig.Wishes.DrawWeight(WishTemplateId.TapAnimals, day6), 1e-9, "x 0.25: SYS-03 pending from an earlier day.");
            visit.Wishes[slot].DrawnDay = day6;
            Assert.AreEqual(8.0, rig.Wishes.DrawWeight(WishTemplateId.TapAnimals, day6), 1e-9, "Today's wishes follow the same-day rules instead.");

            int day2 = rig.SetDay(2);
            visit.Wishes[slot].DrawnDay = day1;
            visit.History[0] = new WishHistoryEntry { Template = (ushort)WishTemplateId.TapAnimals, DayKey = day1 };
            Assert.AreEqual(8.0 * 3.0 * 0.3 * 0.25, rig.Wishes.DrawWeight(WishTemplateId.TapAnimals, day2), 1e-9, "All three together.");
        }

        [Test]
        public void Animals_FavoritesWeighTwo_LowestBondQuarterOneAndAHalf_NeverTwoWishes()
        {
            var rig = WishTestRig.Create(animals: 8);
            SetAnimal(rig, 0, true, 500);
            SetAnimal(rig, 1, true, 0);
            SetAnimal(rig, 2, false, 0);
            for (int i = 3; i < 8; i++)
                SetAnimal(rig, i, false, (ushort)(100 * (i - 2)));
            double[] expected = { 2.0, 3.0, 1.5, 1.0, 1.0, 1.0, 1.0, 1.0 };
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(expected[i], rig.Wishes.AnimalWeight(rig.AnimalId(i)), 1e-9, "Animal " + i);

            var visit = rig.State.DailyVisit;
            int key = rig.SetDay(2);
            int runs = 4000;
            var counts = new int[8];
            for (int r = 0; r < runs; r++)
            {
                visit.WishCount = 0;
                Assert.AreEqual(3, rig.Wishes.DrawForDay(key));
                Assert.AreEqual(3, rig.Stock().Select(w => w.Animal).Distinct().Count(), "Never the same animal for two wishes.");
                Assert.IsTrue(rig.State.Collection.TryGetIndex(visit.Wishes[0].Animal, out int index));
                counts[index]++;
                key = DailyDay.AddDays(key, 1);
            }
            double sum = expected.Sum();
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(expected[i] / sum, counts[i] / (double)runs, 0.025, "Animal " + i);

            visit.WishCount = 0;
            rig.AddWish(WishTemplateId.HatchEggs, rig.AnimalId(3), 3, key);
            Assert.AreEqual(0.0, rig.Wishes.AnimalWeight(rig.AnimalId(3)), "An animal holding a wish is not drawn again.");
        }

        private static void SetAnimal(WishTestRig rig, int index, bool favorite, ushort bond)
        {
            var animal = rig.State.Collection[index];
            animal.BondPoints = bond;
            animal.Flags = favorite ? animal.Flags | AnimalFlags.Favorite : animal.Flags & ~AnimalFlags.Favorite;
            rig.State.Collection.Update(animal);
        }

        [Test]
        public void Amounts_FollowTheTierAtDrawTime_AndNeverChange()
        {
            int[] levels = { 1, 14, 15, 39, 40, 69, 70, 120 };
            int varied = 0;
            foreach (int level in levels)
            {
                var rig = WishTestRig.Create(animals: 14);
                rig.State.Progression.Refuge.Level = level;
                int tier = WishCatalog.TierOf(level);
                int key = rig.SetDay(2);
                Assert.AreEqual(3, rig.Wishes.DrawForDay(key));
                var drawn = rig.Stock();
                foreach (var wish in drawn)
                {
                    var t = WishTestRig.TemplateOf(wish);
                    Assert.AreEqual(t.Amount(tier), wish.Target, t.Id + " at refuge level " + level);
                    if (t.P1 != t.P4)
                        varied++;
                }

                rig.State.Progression.Refuge.Level = level >= 70 ? 1 : 120;
                int next = WishCatalog.TierOf(rig.State.Progression.Refuge.Level);
                key = rig.SetDay(3);
                Assert.AreEqual(3, rig.Wishes.DrawForDay(key));
                var stock = rig.Stock();
                for (int i = 0; i < drawn.Count; i++)
                    Assert.AreEqual(drawn[i].Target, stock[i].Target, "Amounts never change after the draw.");
                for (int i = drawn.Count; i < stock.Count; i++)
                    Assert.AreEqual(WishTestRig.TemplateOf(stock[i]).Amount(next), stock[i].Target, "New wishes use the new tier.");
            }
            Assert.Greater(varied, 0, "Some drawn template has amounts that differ by tier.");
        }

        [Test]
        public void SameProfileAndDay_GiveTheSameDraw()
        {
            var a = WishTestRig.Create(profile: 5);
            var b = WishTestRig.Create(profile: 5);
            Assert.AreEqual(3, a.Wishes.DrawForDay(a.SetDay(2)));
            Assert.AreEqual(3, b.Wishes.DrawForDay(b.SetDay(2)));
            for (int i = 0; i < 3; i++)
            {
                var x = a.Wishes.Get(i);
                var y = b.Wishes.Get(i);
                Assert.AreEqual(x.Template, y.Template);
                Assert.AreEqual(x.Animal, y.Animal);
                Assert.AreEqual(x.Param, y.Param);
                Assert.AreEqual(x.ParamExtra, y.ParamExtra);
                Assert.AreEqual(x.Target, y.Target);
            }
        }

        // ---------------- Property test over many simulated days ----------------

        [Test]
        public void ManySimulatedDays_KeepEveryDrawRule()
        {
            int days = 0, swaps = 0, quickDays = 0, lastSlotChecks = 0;
            for (int profile = 1; profile <= 5; profile++)
            {
                var rig = WishTestRig.Create(profile, animals: 14);
                var random = new System.Random(profile * 7919);
                var visit = rig.State.DailyVisit;
                var snapshot = new Dictionary<(ushort, int), WishRecord>();
                int day = 1;
                while (day <= 600)
                {
                    int key = rig.SetDay(day);
                    if (random.NextDouble() < 0.08)
                        rig.State.Progression.Refuge.Level = 1 + random.Next(100);
                    int tier = WishCatalog.TierOf(rig.Ctx.RefugeLevel);
                    int before = rig.Wishes.PendingCount;
                    int k = Math.Max(0, Math.Min(rig.Config.WishesPerDay, rig.Config.WishStock - before));

                    int drawn = day == 1 ? rig.Wishes.DrawFirstDay(key) : rig.Wishes.DrawForDay(key);
                    Assert.LessOrEqual(drawn, k, "k = min(3, 9 - pending)");
                    Assert.AreEqual(before + drawn, rig.Wishes.PendingCount);
                    var stock = rig.Stock();
                    AssertStock(rig, stock, key);
                    var today = stock.Skip(before).ToList();
                    Assert.IsTrue(today.All(w => w.DrawnDay == key));
                    Assert.AreEqual(drawn, stock.Count(w => w.DrawnDay == key));
                    foreach (var wish in today)
                        Assert.AreEqual(WishTestRig.TemplateOf(wish).Amount(tier), wish.Target, "Amounts from today's tier.");
                    foreach (var wish in stock)
                        if (snapshot.TryGetValue((wish.Template, wish.DrawnDay), out var first))
                            AssertSameWish(first, wish);

                    if (drawn > 0 && drawn == k)
                    {
                        if (today.Any(w => WishTestRig.TemplateOf(w).Quick))
                        {
                            quickDays++;
                        }
                        else
                        {
                            // No quick wish: none could take the last slot (pending, or its system already drawn today).
                            var others = today.Take(today.Count - 1).Select(w => WishTestRig.SystemOf(w)).ToList();
                            foreach (var id in new[] { WishTemplateId.HatchEggs, WishTemplateId.CuddleAnimal })
                            {
                                WishCatalog.TryGet(id, out var t);
                                Assert.IsTrue(stock.Any(w => w.Template == (ushort)id) || others.Contains(t.System),
                                    $"Day {day}: {id} could have been the quick wish.");
                            }
                            lastSlotChecks++;
                        }
                    }

                    // Play: some wishes get done, most days they are claimed, sometimes a swap, sometimes an egg.
                    for (int i = 0; i < rig.Wishes.PendingCount; i++)
                    {
                        if (random.NextDouble() >= 0.6 || visit.Wishes[i].IsDone)
                            continue;
                        visit.Wishes[i].Progress = visit.Wishes[i].Target;
                        visit.Wishes[i].Flags |= WishRecord.FlagDone;
                    }
                    if (random.NextDouble() < 0.8)
                        rig.Wishes.ClaimAll(WishClaimPath.ClaimAll);
                    if (random.NextDouble() < 0.4 && rig.Wishes.PendingCount > 0)
                    {
                        // Claims may have raised the refuge level since the draw: a swap uses the tier of its own moment.
                        int swapTier = WishCatalog.TierOf(rig.Ctx.RefugeLevel);
                        int slot = random.Next(rig.Wishes.PendingCount);
                        var old = rig.Wishes.Get(slot);
                        if (rig.Wishes.TrySwap(slot))
                        {
                            var fresh = rig.Wishes.Get(slot);
                            Assert.AreNotEqual(old.Template, fresh.Template, "The swap never gives the same template…");
                            Assert.AreNotEqual(WishTestRig.SystemOf(old), WishTestRig.SystemOf(fresh), "… nor its system.");
                            Assert.AreEqual(old.Animal, fresh.Animal);
                            Assert.AreEqual(key, fresh.DrawnDay);
                            Assert.AreEqual(WishTestRig.TemplateOf(fresh).Amount(swapTier), fresh.Target);
                            Assert.IsFalse(rig.Wishes.CanSwap, "One free swap a day.");
                            swaps++;
                        }
                    }
                    if (random.NextDouble() < 0.1)
                        Assert.IsTrue(rig.Hatch.Hatch(WishTestRig.BasicEgg).Succeeded);
                    stock = rig.Stock();
                    AssertStock(rig, stock, key);
                    snapshot.Clear();
                    foreach (var wish in stock)
                        snapshot[(wish.Template, wish.DrawnDay)] = wish;

                    days++;
                    day += random.NextDouble() < 0.05 ? 2 + random.Next(30) : 1;
                }
            }
            Assert.Greater(days, 1200);
            Assert.Greater(swaps, 200);
            Assert.Greater(quickDays, days / 4, "Most days hold a quick wish.");
            TestContext.WriteLine($"{days} days, {swaps} swaps, {quickDays} days with a quick wish, {lastSlotChecks} days without one checked.");
        }

        private static void AssertStock(WishTestRig rig, List<WishRecord> stock, int key)
        {
            Assert.LessOrEqual(stock.Count, rig.Config.WishStock, "At most 9 pending.");
            Assert.AreEqual(stock.Count, stock.Select(w => w.Template).Distinct().Count(), "Never a template twice in the stock.");
            Assert.AreEqual(stock.Count, stock.Select(w => w.Animal).Distinct().Count(), "Never one animal for two wishes.");
            foreach (var wish in stock)
            {
                Assert.IsTrue(rig.State.Collection.TryGetIndex(wish.Animal, out _), "The animal lives in the refuge.");
                var t = WishTestRig.TemplateOf(wish);
                Assert.IsTrue(t.ShippedInThisBuild, t.Id + " is not in this build.");
                Assert.AreNotEqual(WishTemplateId.HatchBiomeEggs, t.Id);
                Assert.GreaterOrEqual(wish.Target, 1);
            }
            var today = stock.Where(w => w.DrawnDay == key).ToList();
            Assert.AreEqual(today.Count, today.Select(w => WishTestRig.SystemOf(w)).Distinct().Count(), "Never two of a system the same day.");
            foreach (var group in today.GroupBy(w => WishTestRig.TemplateOf(w).Family))
                Assert.LessOrEqual(group.Count(), WishDrawRules.MaxPerFamily, "At most two of a family the same day.");
            Assert.LessOrEqual(today.Count(w => WishTestRig.TemplateOf(w).Timer), WishDrawRules.MaxTimers);
        }

        private static void AssertSameWish(in WishRecord first, in WishRecord now)
        {
            Assert.AreEqual(first.Animal, now.Animal, "The animal never changes.");
            Assert.AreEqual(first.Param, now.Param, "Parameters never change.");
            Assert.AreEqual(first.ParamExtra, now.ParamExtra);
            Assert.AreEqual(first.Target, now.Target);
            Assert.GreaterOrEqual(now.Progress, first.Progress);
        }

        // ---------------- 0 GC ----------------

        [Test]
        public void Draws_DoNotAllocate()
        {
            var rig = WishTestRig.Create(animals: 10);
            var visit = rig.State.DailyVisit;
            int key = rig.SetDay(2);
            for (int i = 0; i < 16; i++)
            {
                visit.WishCount = 0;
                rig.Wishes.DrawForDay(key);
                key = DailyDay.AddDays(key, 1);
            }
            var keys = new int[30];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = DailyDay.AddDays(key, i);
            var wishes = rig.Wishes;
            Assert.That(() =>
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    visit.WishCount = 0;
                    wishes.DrawForDay(keys[i]);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(keys[keys.Length - 1], visit.WishDrawDayKey);
        }
    }
}
