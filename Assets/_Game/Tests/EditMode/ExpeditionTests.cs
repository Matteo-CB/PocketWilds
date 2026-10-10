using System;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// A refuge for the expedition tests: Prairie species of several families (rabbit, hedgehog and mole SmallQuadruped, a
    /// duckling Bird, a squirrel ClimberPrimate), a Forest fawn (MediumQuadruped) and a Lake otter (SwimmerLow); real economy,
    /// hatch, mastery, progression, rendez-vous (bond, wishes, journal) and activity services, a UTC calendar, the expedition
    /// service and its hooks. Refuge level 5 and 8 animals by default: the expeditions are open.
    /// </summary>
    internal sealed class ExpeditionRig
    {
        public static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        public static readonly ContentId Forest = ContentId.FromSource("biome.forest");
        public static readonly ContentId Lake = ContentId.FromSource("biome.lake");
        public static readonly ContentId Meadows = ExpeditionCatalog.FlowerMeadows;
        public static readonly ContentId BasicEgg = ContentId.FromSource("egg.grassland.basic");

        public static readonly string[] DefaultAnimals = { "rabbit", "rabbit", "rabbit", "hedgehog", "mole", "duckling", "squirrel", "fawn" };

        private static readonly (string name, RigFamily family, string biome)[] SpeciesTable =
        {
            ("rabbit", RigFamily.SmallQuadruped, "biome.grassland"),
            ("hedgehog", RigFamily.SmallQuadruped, "biome.grassland"),
            ("mole", RigFamily.SmallQuadruped, "biome.grassland"),
            ("duckling", RigFamily.Bird, "biome.grassland"),
            ("squirrel", RigFamily.ClimberPrimate, "biome.grassland"),
            ("fawn", RigFamily.MediumQuadruped, "biome.forest"),
            ("otter", RigFamily.SwimmerLow, "biome.lake"),
        };

        public PlayerState State;
        public FakeGameClock Clock;
        public ContentRegistry Registry;
        public EconomyService Economy;
        public MasteryService SpeciesMastery;
        public HatchService Hatch;
        public ProgressionServices P;
        public DailyVisitServices Daily;
        public ActivityServices A;
        public ExpeditionService E;
        public ExpeditionHooks Hooks;

        public static ContentRegistry BuildContent()
        {
            var builder = new ContentRegistryBuilder();
            foreach (var (name, family, biome) in SpeciesTable)
            {
                var species = ContentId.FromSource("species." + name);
                builder.AddSpecies(new SpeciesDefinition(species, "species." + name + ".name", family, ContentId.FromSource(biome), 1.0, "PF_Animal_Test"),
                    "species." + name);
                string id = "variant." + name + ".brown";
                builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), species, id + ".name", RarityTier.Common,
                    new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), id);
            }
            var pool = new[] { new EggDropEntry(ContentId.FromSource("species.rabbit"), 1.0) };
            var table = new[] { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 };
            builder.AddEgg(new EggDefinition(BasicEgg, "egg.grassland.basic.name", Grassland, EggTier.Basic,
                new Color32[EggDefinition.PaletteSize], 15, 0, 15, "PF_Egg_Base", 10.0, 1.18, table, pool), "egg.grassland.basic");
            return builder.Build();
        }

        /// <summary>A new refuge (or <paramref name="state"/>, as after a load) at <paramref name="now"/>.</summary>
        public static ExpeditionRig Create(string[] animals = null, int level = 5, long gems = 0, ExpeditionConfig config = null,
            PlayerState state = null, DateTimeOffset? now = null, int profile = 42, bool firstStrollDone = true)
        {
            var rig = new ExpeditionRig { Registry = BuildContent(), Clock = new FakeGameClock(now ?? TestUtil.T0) };
            if (state == null)
            {
                state = PlayerState.CreateNew(TestUtil.T0);
                state.Meta.ProfileId = new Guid(profile, 3, 5, 1, 2, 3, 4, 5, 6, 7, 8);
                foreach (var name in animals ?? DefaultAnimals)
                    AddAnimal(state, name);
                state.DailyVisit.OnboardingDone = true;
                state.Expeditions.FirstStrollDone = firstStrollDone;
            }
            rig.State = state;
            var progressionConfig = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
            rig.Economy = new EconomyService(state, rig.Registry, EconomyConfig.Default, null);
            rig.SpeciesMastery = new MasteryService(state, rig.Economy.Config, rig.Economy);
            rig.Hatch = new HatchService(state, rig.Registry, rig.Economy, rig.Clock, null, rig.SpeciesMastery);
            rig.P = new ProgressionServices(state, rig.Registry, rig.Economy, rig.Hatch, rig.Clock, null, progressionConfig, rig.SpeciesMastery);
            state.Progression.Refuge.Level = level;
            if (gems > 0)
                rig.Economy.Grant(CurrencyId.Gems, BigValue.FromDouble(gems), EconomyReason.Debug);
            rig.Daily = new DailyVisitServices(state, rig.Registry, rig.Economy, rig.Hatch, rig.SpeciesMastery, null, rig.P, rig.Clock, null,
                new DailyVisitConfig { ActivitiesAvailable = true });
            rig.A = new ActivityServices(state, state.Activities, rig.Registry, rig.Economy, rig.Hatch, rig.SpeciesMastery, rig.P, rig.Daily.Bond,
                rig.Clock, null, new ActivityConfig(), rig.Daily.Context);
            rig.A.OnResume();
            rig.E = new ExpeditionService(rig.A, config);
            rig.Hooks = new ExpeditionHooks(rig.E, rig.Daily);
            rig.E.Tick();
            return rig;
        }

        public static uint AddAnimal(PlayerState state, string species)
        {
            var id = ContentId.FromSource("species." + species);
            state.Collection.DiscoveredSpecies.Add(id);
            return state.Collection.Add(new AnimalInstance
            {
                Species = id,
                Variant = ContentId.FromSource("variant." + species + ".brown"),
                Seed = (uint)(state.Collection.Count + 1) * 7919u,
                CreatedUtcSeconds = (uint)TestUtil.T0.ToUnixTimeSeconds(),
            });
        }

        /// <summary>The instance ids of the first animals of these species (in collection order, never twice the same).</summary>
        public uint[] Team(params string[] species)
        {
            var team = new uint[species.Length];
            for (int k = 0; k < species.Length; k++)
            {
                var id = ContentId.FromSource("species." + species[k]);
                for (int i = 0; i < State.Collection.Count; i++)
                {
                    var animal = State.Collection[i];
                    if (animal.Species == id && !team.Take(k).Contains(animal.InstanceId))
                    {
                        team[k] = animal.InstanceId;
                        break;
                    }
                }
                Assert.AreNotEqual(0u, team[k], "The rig holds a " + species[k]);
            }
            return team;
        }

        public ExpeditionStartResult Start(int slot, ExpeditionDuration duration, params string[] species)
        {
            var team = Team(species);
            return E.Start(slot, Meadows, duration, team, team.Length);
        }

        public void Advance(TimeSpan delta)
        {
            Clock.Advance(delta);
            E.Tick();
        }

        public long Gems => (long)Economy.Balance(CurrencyId.Gems).ToDoubleClamped();

        /// <summary>Saves and loads the state (the binary save, migrated to the current schema).</summary>
        public static PlayerState RoundTrip(PlayerState state)
        {
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(state, writer);
            var bytes = new byte[writer.Length];
            Array.Copy(writer.Buffer, bytes, writer.Length);
            var document = serializer.ReadDocument(bytes, bytes.Length);
            return serializer.Deserialize(document);
        }
    }

    /// <summary>
    /// T42 « Expéditions » (doc 01 Contenu, section 7 and S-ACT-005): exact offline timers, a clock set back, a late return
    /// that loses nothing, loot drawn at departure (same seed, same loot; a speed-up draws nothing), the Discovery egg
    /// guarantee, the team rating of section 7.2, slots and speed-up prices, challenges, the welcome's grants, postcards,
    /// the hooks, and reads and ticks that never allocate.
    /// </summary>
    public sealed class ExpeditionTests
    {
        // ---------------- Opening and catalogue ----------------

        [Test]
        public void Opening_RefugeLevel5And8Animals_AndTheCatalogueHoldsThe37Destinations()
        {
            Assert.IsTrue(ActivityCatalog.Get(ActivityId.Expeditions).ShippedInThisBuild, "T42 ships the first meta activity.");
            Assert.IsTrue(ExpeditionRig.Create().E.IsOpen, "Level 5 and 8 animals (A18).");
            Assert.IsFalse(ExpeditionRig.Create(level: 4).E.IsOpen, "Level 4: closed.");
            Assert.IsFalse(ExpeditionRig.Create(animals: ExpeditionRig.DefaultAnimals.Take(7).ToArray()).E.IsOpen, "7 animals: closed.");
            var rig = ExpeditionRig.Create();
            Assert.IsTrue(rig.E.Config.TryOverrideFlag(ExpeditionConfig.EnabledFlag, false));
            Assert.IsFalse(rig.E.IsOpen, "flags.expeditions off.");
            Assert.IsTrue(rig.E.Config.TryOverrideFlag(ExpeditionConfig.EnabledFlag, true));

            Assert.AreEqual(37, rig.E.DestinationCount);
            Assert.AreEqual(13, ExpeditionCatalog.All.Count(d => !d.IsEvent));
            Assert.AreEqual(24, ExpeditionCatalog.All.Count(d => d.IsEvent));
            Assert.AreEqual(37, ExpeditionCatalog.All.Select(d => d.Id).Distinct().Count());
            var meadows = rig.E.DestinationAt(0);
            Assert.AreEqual("expedition.flower_meadows", meadows.Key);
            Assert.AreEqual("EXP-GL", meadows.ProductionId);
            Assert.AreEqual(ExpeditionRig.Grassland, meadows.Biome);
            Assert.AreEqual(RigFamilyMask.SmallQuadruped, meadows.FavoriteFamilies);
            Assert.AreEqual(4, ExpeditionCatalog.All[0].SeedLine.Length);
            Assert.IsTrue(rig.E.TryGetDestination(ContentId.FromSource("expedition.nebula_shore"), out var nebula));
            Assert.AreEqual("EXP-SL", nebula.ProductionId);
            Assert.AreEqual(RigFamilyMask.SwimmerLow, nebula.FavoriteFamilies);
            Assert.IsTrue(rig.E.TryGetDestination(ContentId.FromSource("expedition.harvest_golden_oaks"), out var oaks));
            Assert.IsTrue(oaks.Event);
            Assert.AreEqual("EXP-EV110-B", oaks.ProductionId);

            // T42: the Flower Meadows alone are open; the Forest's destination waits for its postcard set, events for T48+.
            Assert.IsTrue(rig.E.IsDestinationOpen(ExpeditionRig.Meadows));
            Assert.IsFalse(rig.E.IsDestinationOpen(ContentId.FromSource("expedition.stump_wood")));
            Assert.IsFalse(rig.E.IsDestinationOpen(oaks.Id));
            Assert.AreEqual(1, ExpeditionCatalog.OpenCount(rig.P));
            var team = rig.Team("fawn");
            Assert.AreEqual(ExpeditionStartResult.DestinationLocked, rig.E.Start(0, ContentId.FromSource("expedition.stump_wood"),
                ExpeditionDuration.OneHour, team, 1));
        }

        // ---------------- Timers (decision 2: absolute UTC, offline) ----------------

        [Test]
        public void OfflineExactness_SavedAndLoaded_BackAfterOneHour_NotAfter59Minutes()
        {
            var rig = ExpeditionRig.Create();
            Assert.AreEqual(ExpeditionStartResult.Started, rig.Start(0, ExpeditionDuration.OneHour, "rabbit"));
            Assert.AreEqual(3600.0, rig.E.TotalSecondsOf(0), 1e-6);
            Assert.AreEqual(ExpeditionSlotState.Away, rig.E.StateOf(0));
            var saved = ExpeditionRig.RoundTrip(rig.State);

            var at59 = ExpeditionRig.Create(state: ExpeditionRig.RoundTrip(saved), now: TestUtil.T0.AddMinutes(59));
            Assert.AreEqual(ExpeditionSlotState.Away, at59.E.StateOf(0), "59 min later: still away.");
            Assert.AreEqual(60.0, at59.E.SecondsLeft(0), 1e-6);
            Assert.IsTrue(at59.E.IsAway(at59.State.Expeditions.Slots[0].Animal0));
            Assert.AreEqual(0, at59.E.BackCount);
            Assert.AreEqual(60.0, at59.E.SoonestReturn, 1e-6);
            Assert.IsFalse(at59.E.TryCollect(0, out _), "Not back yet.");

            var at60 = ExpeditionRig.Create(state: ExpeditionRig.RoundTrip(saved), now: TestUtil.T0.AddHours(1));
            Assert.AreEqual(ExpeditionSlotState.Back, at60.E.StateOf(0), "An hour later, game closed: back.");
            Assert.AreEqual(0.0, at60.E.SecondsLeft(0));
            Assert.AreEqual(1, at60.E.BackCount);
            Assert.AreEqual(-1.0, at60.E.SoonestReturn);
        }

        [Test]
        public void ATeamComingBack_RaisesSlotChangedOnce()
        {
            var rig = ExpeditionRig.Create();
            int changes = 0;
            rig.E.SlotChanged += slot => changes++;
            rig.Start(1, ExpeditionDuration.OneHour, "rabbit");
            Assert.AreEqual(1, changes, "The departure.");
            rig.Advance(TimeSpan.FromMinutes(30));
            Assert.AreEqual(1, changes);
            rig.Advance(TimeSpan.FromMinutes(30));
            Assert.AreEqual(2, changes, "Away to back: once.");
            for (int i = 0; i < 10; i++)
                rig.Advance(TimeSpan.FromSeconds(1));
            Assert.AreEqual(2, changes, "Never again while it waits.");
        }

        [Test]
        public void AClockSetBack_NeverMakesAWaitLongerThanItsDuration()
        {
            var rig = ExpeditionRig.Create();
            rig.Start(0, ExpeditionDuration.FourHours, "rabbit");
            rig.Advance(TimeSpan.FromHours(1));
            Assert.AreEqual(3 * 3600.0, rig.E.SecondsLeft(0), 1e-6);

            // The phone's clock goes back two days.
            rig.Clock.Set(TestUtil.T0.AddDays(-2));
            Assert.AreEqual(4 * 3600.0, rig.E.SecondsLeft(0), 1e-6, "Clamped to the duration.");
            Assert.LessOrEqual(rig.E.SecondsLeft(0), rig.E.TotalSecondsOf(0));
            rig.E.Tick();
            Assert.AreEqual(4 * 3600.0, rig.E.TotalSecondsOf(0), 1e-6, "The duration is kept.");
            rig.Advance(TimeSpan.FromHours(4) - TimeSpan.FromSeconds(1));
            Assert.AreEqual(ExpeditionSlotState.Away, rig.E.StateOf(0));
            rig.Advance(TimeSpan.FromSeconds(1));
            Assert.AreEqual(ExpeditionSlotState.Back, rig.E.StateOf(0), "Back at most one duration after the clock went back.");
        }

        [Test]
        public void ALateReturn_AWeekLate_GivesTheSameLoot_NothingIsLost()
        {
            var rig = ExpeditionRig.Create();
            rig.Start(0, ExpeditionDuration.EightHours, "rabbit", "duckling", "squirrel");
            var drawn = rig.State.Expeditions.Slots[0].Loot;
            rig.Advance(TimeSpan.FromDays(7));
            Assert.AreEqual(ExpeditionSlotState.Back, rig.E.StateOf(0), "A team back waits for ever.");
            rig.State.Progression.Treats.TryGetValue(drawn.Treat, out int treatsBefore);
            int seedsBefore = Enumerable.Range(0, drawn.SeedKinds).Sum(i => rig.State.Inventory.Count(drawn.SeedAt(i)));

            Assert.IsTrue(rig.E.TryCollect(0, out var welcome));
            Assert.AreEqual(drawn.LightMinutes, welcome.Loot.LightMinutes);
            Assert.AreEqual(drawn.Treats, welcome.Loot.Treats);
            Assert.AreEqual(drawn.SeedKinds, welcome.Loot.SeedKinds);
            Assert.AreEqual(drawn.Seeds0, welcome.Loot.Seeds0);
            Assert.AreEqual(drawn.Stickers, welcome.Loot.Stickers);
            Assert.AreEqual(drawn.DiscoveryEggs, welcome.Loot.DiscoveryEggs);
            Assert.AreEqual(drawn.Treats, rig.State.Progression.Treats[drawn.Treat] - treatsBefore);
            int seedsAfter = Enumerable.Range(0, drawn.SeedKinds).Sum(i => rig.State.Inventory.Count(drawn.SeedAt(i)));
            Assert.AreEqual(drawn.Seeds0 + drawn.Seeds1 + drawn.Seeds2 + drawn.Seeds3, seedsAfter - seedsBefore);
            Assert.AreEqual(ExpeditionSlotState.Empty, rig.E.StateOf(0));
            Assert.IsFalse(rig.E.TryCollect(0, out _), "Welcomed once.");
        }

        // ---------------- The first stroll (A18, A19) ----------------

        [Test]
        public void TheFirstStroll_Lasts3Minutes_AndGivesThe1HourLoot()
        {
            var rig = ExpeditionRig.Create(firstStrollDone: false);
            Assert.IsTrue(rig.E.FirstStrollNext);
            Assert.AreEqual(180.0, rig.E.DurationSeconds(ExpeditionDuration.TwelveHours), "« Première balade : 3 min ».");
            rig.E.Preview(ExpeditionRig.Meadows, ExpeditionDuration.TwelveHours, 1, out var preview);
            Assert.AreEqual(15.0, preview.LightMinutes, 1e-9, "The 1 h column.");
            Assert.AreEqual(ExpeditionStartResult.Started, rig.Start(0, ExpeditionDuration.TwelveHours, "fawn"));
            Assert.IsFalse(rig.E.FirstStrollNext);
            Assert.AreEqual(ExpeditionDuration.OneHour, rig.E.DurationOf(0));
            Assert.AreEqual(180.0, rig.E.TotalSecondsOf(0), 1e-6);
            Assert.AreEqual(3600.0, rig.E.DurationSeconds(ExpeditionDuration.OneHour), "The next ones last their hours.");
            rig.Advance(TimeSpan.FromMinutes(3));
            Assert.IsTrue(rig.E.TryCollect(0, out var welcome));
            Assert.IsTrue(welcome.FirstStroll);
            Assert.IsTrue(welcome.FirstFromDestination);
            Assert.AreEqual(15.0, welcome.Loot.LightMinutes, 1e-9);
            Assert.AreEqual(0, rig.E.DiscoveryGuaranteeIn - rig.E.Config.DiscoveryGuarantee, "The stroll is not a long expedition.");
        }

        // ---------------- Loot drawn at departure (decision 3) ----------------

        [Test]
        public void TheLoot_IsDrawnAtDeparture_SameSeedSameLoot_AndASpeedUpChangesNothing()
        {
            var a = ExpeditionRig.Create(gems: 500, profile: 7);
            var b = ExpeditionRig.Create(gems: 500, profile: 7);
            a.Start(0, ExpeditionDuration.TwelveHours, "rabbit", "duckling", "squirrel");
            b.Start(0, ExpeditionDuration.TwelveHours, "rabbit", "duckling", "squirrel");
            var la = a.State.Expeditions.Slots[0].Loot;
            var lb = b.State.Expeditions.Slots[0].Loot;
            Assert.AreEqual(a.State.Expeditions.Slots[0].Seed, b.State.Expeditions.Slots[0].Seed);
            Assert.AreEqual(la.LightMinutes, lb.LightMinutes);
            Assert.AreEqual(la.Stickers, lb.Stickers);
            Assert.AreEqual(la.DiscoveryEggs, lb.DiscoveryEggs);
            Assert.AreEqual(la.SeedKinds, lb.SeedKinds);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(la.SeedAt(i), lb.SeedAt(i));
                Assert.AreEqual(la.SeedsAt(i), lb.SeedsAt(i));
            }
            Assert.AreEqual(0, la.StickerChance + la.DiscoveryChance, "Loot: chances resolved.");
            // 12 h, 3 stars (80 points): Light 120 × 1.2, treats 4 + 1, seeds 4 + 1 (3 stars) + 1 (Flower Meadows).
            Assert.AreEqual(3, a.E.StarsOf(0));
            Assert.AreEqual(144.0, la.LightMinutes, 1e-9);
            Assert.AreEqual(5, la.Treats);
            Assert.AreEqual(6, la.Seeds0 + la.Seeds1 + la.Seeds2 + la.Seeds3);
            Assert.LessOrEqual(la.SeedKinds, 4);
            Assert.AreEqual(1, la.Tickets);
            Assert.AreEqual(0, la.Ingredients0 + la.Ingredients1, "The Bakery is closed: no ingredient.");
            Assert.AreEqual(5, la.BondPoints);
            Assert.AreEqual(15, la.MasteryXp);

            int cost = a.E.SpeedUpCost(0);
            Assert.AreEqual(120, cost, "12 h left: 120 started 6-minute steps.");
            Assert.IsTrue(a.E.TrySpeedUp(0));
            Assert.AreEqual(500 - 120, a.Gems);
            Assert.AreEqual(ExpeditionSlotState.Back, a.E.StateOf(0));
            var after = a.State.Expeditions.Slots[0].Loot;
            Assert.AreEqual(la.LightMinutes, after.LightMinutes, "A speed-up draws nothing.");
            Assert.AreEqual(la.Stickers, after.Stickers);
            Assert.AreEqual(la.DiscoveryEggs, after.DiscoveryEggs);
            Assert.AreEqual(la.Seeds0, after.Seeds0);
            Assert.AreEqual(la.Seed0, after.Seed0);
        }

        [Test]
        public void Seeds_ComeFromTheKnownPlantsOfTheLine_AndTheBakeryIngredientsOnlyWhenItIsOpen()
        {
            var rig = ExpeditionRig.Create();
            var line = ExpeditionCatalog.All[0].SeedLine;
            Assert.AreEqual(0b1111, ExpeditionRules.KnownPlants(line, rig.P), "The Prairie's line is all known.");
            var nebula = ExpeditionCatalog.All[ExpeditionCatalog.IndexOf(ContentId.FromSource("expedition.nebula_shore"))];
            Assert.AreEqual(0, ExpeditionRules.KnownPlants(nebula.SeedLine, rig.P), "Star Bloom, Moonflower, Cloud Puff: their biomes are closed.");

            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(ExpeditionStartResult.Started, rig.Start(0, ExpeditionDuration.TwelveHours, "rabbit"));
                var loot = rig.State.Expeditions.Slots[0].Loot;
                for (int k = 0; k < loot.SeedKinds; k++)
                {
                    Assert.Contains(loot.SeedAt(k), line);
                    Assert.Greater(loot.SeedsAt(k), 0);
                }
                rig.Advance(TimeSpan.FromHours(12));
                Assert.IsTrue(rig.E.TryCollect(0, out _));
            }

            var context = new ExpeditionLootContext
            {
                Treat = ContentId.FromSource("treat.clover_crunch"), SeedLine = line, KnownPlants = 0b1111, BakeryOpen = true, MasteryXp = 15,
            };
            ExpeditionRules.Draw(rig.E.Config, ExpeditionCatalog.All[0], ExpeditionDuration.TwelveHours, 1, in context, false, 99u, out var bakery);
            Assert.AreEqual(3, bakery.Ingredients0 + bakery.Ingredients1, "12 h: 2 honey or cream, and the Flower Meadows' Wheat Flour.");
            Assert.IsTrue(bakery.Ingredient0 == ExpeditionCatalog.GentleHoney || bakery.Ingredient0 == ExpeditionCatalog.VanillaCream);
            Assert.AreEqual(ExpeditionCatalog.WheatFlour, bakery.Ingredient1);
            ExpeditionRules.Draw(rig.E.Config, ExpeditionCatalog.All[0], ExpeditionDuration.OneHour, 1, in context, false, 99u, out var short1);
            Assert.AreEqual(0, short1.Ingredients0 + short1.Ingredients1, "1 h: none (section 7.3).");
        }

        [Test]
        public void TheDiscoveryEgg_IsGuaranteed_AtThe12thLongExpeditionWithoutOne()
        {
            var config = new ExpeditionConfig { DiscoveryChance = new[] { 0, 0, 0, 0 } };
            var rig = ExpeditionRig.Create(config: config);
            int eggsBefore = rig.State.World.DiscoveryEggs;
            Assert.AreEqual(12, rig.E.DiscoveryGuaranteeIn);

            // A 4 h expedition never counts.
            rig.Start(0, ExpeditionDuration.FourHours, "rabbit");
            rig.Advance(TimeSpan.FromHours(4));
            Assert.IsTrue(rig.E.TryCollect(0, out _));
            Assert.AreEqual(12, rig.E.DiscoveryGuaranteeIn);

            for (int i = 1; i <= 12; i++)
            {
                rig.E.Preview(ExpeditionRig.Meadows, ExpeditionDuration.EightHours, 1, out var preview);
                Assert.AreEqual(i == 12 ? 100 : 0, preview.DiscoveryChance, "Preview of long expedition " + i);
                Assert.AreEqual(ExpeditionStartResult.Started, rig.Start(0, ExpeditionDuration.EightHours, "rabbit"));
                rig.Advance(TimeSpan.FromHours(8));
                Assert.IsTrue(rig.E.TryCollect(0, out var welcome));
                if (i < 12)
                {
                    Assert.AreEqual(0, welcome.Loot.DiscoveryEggs, "Long expedition " + i);
                    Assert.AreEqual(12 - i, rig.E.DiscoveryGuaranteeIn);
                }
                else
                {
                    Assert.AreEqual(1, welcome.Loot.DiscoveryEggs, "The 12th long expedition brings the Discovery egg.");
                    Assert.AreEqual(eggsBefore + 1, rig.State.World.DiscoveryEggs, "Into the Discovery stock (DiscoverySource.Expedition).");
                    Assert.AreEqual(12, rig.E.DiscoveryGuaranteeIn, "The counter starts again.");
                }
            }
        }

        // ---------------- The team rating (section 7.2) ----------------

        [Test]
        public void TheTeamRating_FollowsSection7_2()
        {
            var rig = ExpeditionRig.Create(animals: new[] { "rabbit", "rabbit", "rabbit", "hedgehog", "duckling", "squirrel", "fawn", "otter" });
            void Check(int points, byte stars, int fromBiome, bool favorite, bool three, params string[] species)
            {
                var team = rig.Team(species);
                var rating = rig.E.Rate(ExpeditionRig.Meadows, team, team.Length);
                string what = string.Join(" + ", species);
                Assert.AreEqual(points, rating.Points, what);
                Assert.AreEqual(stars, rating.Stars, what);
                Assert.AreEqual(fromBiome, rating.FromBiome, what);
                Assert.AreEqual(favorite, rating.FavoriteFamily, what);
                Assert.AreEqual(three, rating.ThreeSpecies, what);
            }

            Check(30, 1, 1, true, false, "rabbit");
            Check(10, 1, 0, false, false, "fawn");
            Check(40, 2, 2, false, false, "duckling", "squirrel");
            Check(30, 1, 1, false, false, "duckling", "fawn");
            Check(70, 3, 3, true, false, "rabbit", "rabbit", "rabbit");
            Check(80, 3, 3, true, true, "rabbit", "duckling", "squirrel");
            Check(50, 2, 1, false, true, "fawn", "otter", "duckling");
            Check(50, 2, 2, true, false, "rabbit", "hedgehog");

            Assert.AreEqual(1, ExpeditionRules.Stars(39));
            Assert.AreEqual(2, ExpeditionRules.Stars(40));
            Assert.AreEqual(2, ExpeditionRules.Stars(59));
            Assert.AreEqual(3, ExpeditionRules.Stars(60));
            Assert.AreEqual(0, rig.E.Rate(ExpeditionRig.Meadows, new uint[3], 0).Points, "No animal: no rating.");

            // « Équipe conseillée »: the best rated team among the free animals.
            var into = new uint[3];
            Assert.AreEqual(3, rig.E.SuggestTeam(ExpeditionRig.Meadows, into));
            Assert.AreEqual(80, rig.E.Rate(ExpeditionRig.Meadows, into, 3).Points);
            rig.Start(0, ExpeditionDuration.OneHour, "rabbit", "duckling", "squirrel");
            Assert.AreEqual(3, rig.E.SuggestTeam(ExpeditionRig.Meadows, into));
            Assert.IsFalse(into.Any(rig.E.IsAway), "Only free animals.");
            Assert.AreEqual(70, rig.E.Rate(ExpeditionRig.Meadows, into, 3).Points, "Two rabbits and the hedgehog: 3 from the biome, the family.");
        }

        // ---------------- Slots and finish now ----------------

        [Test]
        public void Slots_TwoFree_TheThirdFor800Gems_TheFourthWithTheClub()
        {
            var rig = ExpeditionRig.Create(gems: 900);
            Assert.AreEqual(4, rig.E.SlotCount);
            Assert.AreEqual(ExpeditionSlotState.Empty, rig.E.StateOf(0));
            Assert.AreEqual(ExpeditionSlotState.Empty, rig.E.StateOf(1));
            Assert.AreEqual(ExpeditionSlotState.Locked, rig.E.StateOf(2));
            Assert.AreEqual(ExpeditionSlotState.Locked, rig.E.StateOf(3));
            Assert.AreEqual(ExpeditionSlotUnlock.Gems, rig.E.UnlockOf(2));
            Assert.AreEqual(ExpeditionSlotUnlock.Club, rig.E.UnlockOf(3));
            Assert.AreEqual(ExpeditionSlotUnlock.None, rig.E.UnlockOf(0));
            Assert.AreEqual(800, rig.E.SlotPrice);
            Assert.AreEqual(ExpeditionStartResult.SlotNotEmpty, rig.Start(2, ExpeditionDuration.OneHour, "rabbit"), "A locked slot takes no team.");

            Assert.IsTrue(rig.E.TryBuySlot());
            Assert.AreEqual(100, rig.Gems);
            Assert.AreEqual(ExpeditionSlotState.Empty, rig.E.StateOf(2));
            Assert.IsFalse(rig.E.TryBuySlot(), "Once.");
            rig.A.Config.ClubActive = true;
            Assert.AreEqual(ExpeditionSlotState.Empty, rig.E.StateOf(3), "The Wild Club's slot.");
            Assert.AreEqual(ExpeditionStartResult.Started, rig.Start(3, ExpeditionDuration.OneHour, "fawn"));
            rig.A.Config.ClubActive = false;
            Assert.AreEqual(ExpeditionSlotState.Away, rig.E.StateOf(3), "The Club ends: its expedition finishes normally.");
            rig.Advance(TimeSpan.FromHours(1));
            Assert.IsTrue(rig.E.TryCollect(3, out _));
            Assert.AreEqual(ExpeditionSlotState.Locked, rig.E.StateOf(3), "Then the slot locks.");

            var poor = ExpeditionRig.Create(gems: 799);
            Assert.IsFalse(poor.E.TryBuySlot());
            Assert.AreEqual(799, poor.Gems);

            // One animal in one team at a time; 1 to 3 animals; the slot must be free.
            Assert.AreEqual(ExpeditionStartResult.Started, rig.Start(0, ExpeditionDuration.OneHour, "rabbit"));
            Assert.AreEqual(ExpeditionStartResult.SlotNotEmpty, rig.Start(0, ExpeditionDuration.OneHour, "hedgehog"));
            Assert.AreEqual(ExpeditionStartResult.AnimalUnavailable, rig.Start(1, ExpeditionDuration.OneHour, "rabbit"), "The first rabbit is away.");
            var twice = rig.Team("hedgehog");
            Assert.AreEqual(ExpeditionStartResult.AnimalUnavailable, rig.E.Start(1, ExpeditionRig.Meadows, ExpeditionDuration.OneHour,
                new[] { twice[0], twice[0] }, 2));
            Assert.AreEqual(ExpeditionStartResult.NoAnimal, rig.E.Start(1, ExpeditionRig.Meadows, ExpeditionDuration.OneHour, new uint[3], 0));
            Assert.AreEqual(ExpeditionStartResult.TooManyAnimals, rig.E.Start(1, ExpeditionRig.Meadows, ExpeditionDuration.OneHour, new uint[4], 4));
            Assert.AreEqual(ExpeditionStartResult.NotOpen, ExpeditionRig.Create(level: 4).Start(0, ExpeditionDuration.OneHour, "rabbit"));
        }

        [Test]
        public void FinishNow_OneGemPerStarted6Minutes_5AtLeast_3ADay()
        {
            var config = ExpeditionConfig.Default;
            Assert.AreEqual(0, ExpeditionRules.SpeedUpCost(0.0, config));
            Assert.AreEqual(5, ExpeditionRules.SpeedUpCost(60.0, config), "5 at least.");
            Assert.AreEqual(5, ExpeditionRules.SpeedUpCost(30 * 60.0, config));
            Assert.AreEqual(6, ExpeditionRules.SpeedUpCost(31 * 60.0, config), "6 started steps.");
            Assert.AreEqual(10, ExpeditionRules.SpeedUpCost(3600.0, config));
            Assert.AreEqual(40, ExpeditionRules.SpeedUpCost(4 * 3600.0, config));
            Assert.AreEqual(120, ExpeditionRules.SpeedUpCost(12 * 3600.0, config));

            var rig = ExpeditionRig.Create(gems: 1000);
            Assert.IsTrue(rig.E.TryBuySlot());
            Assert.AreEqual(3, rig.E.SpeedUpsLeftToday);
            Assert.AreEqual(0, rig.E.SpeedUpCost(0), "Nothing away.");
            Assert.IsFalse(rig.E.TrySpeedUp(0));
            int spedUp = 0;
            rig.E.SpedUp += (slot, gems, seconds) => spedUp += gems;
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.AreEqual(ExpeditionStartResult.Started, rig.Start(slot, ExpeditionDuration.OneHour, new[] { "hedgehog", "mole", "fawn" }[slot]));
                rig.Advance(TimeSpan.FromMinutes(30));
                Assert.AreEqual(5, rig.E.SpeedUpCost(slot));
                Assert.IsTrue(rig.E.TrySpeedUp(slot));
                Assert.AreEqual(ExpeditionSlotState.Back, rig.E.StateOf(slot));
                Assert.IsTrue(rig.E.TryCollect(slot, out _));
            }
            Assert.AreEqual(15, spedUp);
            Assert.AreEqual(0, rig.E.SpeedUpsLeftToday);
            rig.Start(0, ExpeditionDuration.OneHour, "squirrel");
            long gems = rig.Gems;
            Assert.IsFalse(rig.E.TrySpeedUp(0), "3 a day.");
            Assert.AreEqual(gems, rig.Gems);
            rig.Clock.Advance(TimeSpan.FromHours(23));
            rig.A.Tick(1.0);
            rig.Daily.CheckDay();
            rig.Start(1, ExpeditionDuration.FourHours, "duckling");
            Assert.AreEqual(3, rig.E.SpeedUpsLeftToday, "A new day.");
            Assert.IsTrue(rig.E.TrySpeedUp(1));
        }

        // ---------------- Challenges (S-ACT-005) ----------------

        [Test]
        public void Challenges_StarsFromTheRatingPoints_RecordedAtDeparture()
        {
            var rig = ExpeditionRig.Create(gems: 0);
            Assert.AreEqual(10, rig.E.ChallengeCount);
            Assert.IsTrue(rig.E.IsChallengeOpen(0), "01: to the Flower Meadows.");
            Assert.IsFalse(rig.E.IsChallengeOpen(1), "02: the Stump Wood is not open.");
            Assert.IsFalse(rig.E.IsChallengeOpen(2), "03: the Reed Shores are not open.");
            Assert.IsTrue(rig.E.IsChallengeOpen(3), "04: anywhere.");
            Assert.IsFalse(rig.E.IsChallengeOpen(5), "06: the Torrent Pasture is not open.");
            Assert.IsTrue(rig.E.IsChallengeOpen(8), "09: a biome destination.");
            Assert.IsTrue(rig.E.IsChallengeOpen(9), "10: the newest biome (the Prairie) has its destination.");
            Assert.AreEqual(ExpeditionDuration.OneHour, rig.E.ChallengeDuration(0));
            Assert.AreEqual(ExpeditionDuration.TwelveHours, rig.E.ChallengeDuration(7));
            Assert.AreEqual(10, rig.E.ChallengeThreshold(0, 1));
            Assert.AreEqual(30, rig.E.ChallengeThreshold(0, 2));
            Assert.AreEqual(40, rig.E.ChallengeThreshold(0, 3));
            Assert.AreEqual(80, rig.E.ChallengeThreshold(8, 3));

            // 05 « Tout petits »: 3 SmallQuadruped; 07 « Trio varié »: 3 families; 09: 3 of the destination's biome.
            Assert.IsTrue(rig.E.ChallengeAccepts(4, ExpeditionRig.Meadows, rig.Team("rabbit", "hedgehog", "mole"), 3));
            Assert.IsFalse(rig.E.ChallengeAccepts(4, ExpeditionRig.Meadows, rig.Team("rabbit", "hedgehog", "duckling"), 3));
            Assert.IsTrue(rig.E.ChallengeAccepts(6, ExpeditionRig.Meadows, rig.Team("rabbit", "duckling", "fawn"), 3));
            Assert.IsFalse(rig.E.ChallengeAccepts(6, ExpeditionRig.Meadows, rig.Team("rabbit", "hedgehog", "fawn"), 3));
            Assert.IsTrue(rig.E.ChallengeAccepts(8, ExpeditionRig.Meadows, rig.Team("rabbit", "duckling", "squirrel"), 3));
            Assert.IsFalse(rig.E.ChallengeAccepts(8, ExpeditionRig.Meadows, rig.Team("rabbit", "duckling", "fawn"), 3));
            Assert.IsFalse(rig.E.ChallengeAccepts(3, ExpeditionRig.Meadows, rig.Team("rabbit", "duckling"), 2), "04: 3 animals.");
            Assert.IsTrue(rig.E.ChallengeAllows(0, ExpeditionRig.Meadows));
            Assert.IsFalse(rig.E.ChallengeAllows(1, ExpeditionRig.Meadows));

            int improved = 0;
            rig.E.ChallengeImproved += (challenge, stars, attempts) => improved++;
            // 01 with a fawn alone: 10 points, 1 star.
            var fawn = rig.Team("fawn");
            Assert.AreEqual(ExpeditionStartResult.Started, rig.E.StartChallenge(0, 0, ExpeditionRig.Meadows, fawn, 1));
            Assert.AreEqual(1, rig.E.ChallengeStars(0));
            Assert.AreEqual(ExpeditionDuration.OneHour, rig.E.DurationOf(0));
            Assert.AreEqual(0, (int)rig.State.Expeditions.Slots[0].Challenge, "Challenge 01 is index 0.");
            // Then 70 points: 3 stars, the challenge's Gems once.
            int threeBefore = rig.State.Activities.ChallengesThreeStars;
            var trio = rig.Team("rabbit", "rabbit", "rabbit");
            Assert.AreEqual(ExpeditionStartResult.Started, rig.E.StartChallenge(1, 0, ExpeditionRig.Meadows, trio, 3));
            Assert.AreEqual(3, rig.E.ChallengeStars(0));
            Assert.AreEqual(threeBefore + 1, rig.State.Activities.ChallengesThreeStars);
            Assert.AreEqual(new ActivityConfig().GemsChallenge, rig.Gems, "First 3 stars: the challenge's Gems.");
            Assert.AreEqual(2, improved);
            Assert.AreEqual(2, rig.State.Activities.ChallengeAttempts[ActivityState.ChallengeIndex(ActivityCatalog.SlotOf(ActivityId.Expeditions), 0)]);
            Assert.AreEqual(ExpeditionStartResult.AnimalUnavailable,
                rig.E.StartChallenge(1, 4, ExpeditionRig.Meadows, rig.Team("hedgehog", "duckling", "squirrel"), 3), "05 needs 3 SmallQuadruped.");
        }

        // ---------------- The welcome ----------------

        [Test]
        public void TheWelcome_GrantsLightTreatsSeedsBondMasteryAndKeepsAPostcard()
        {
            var rig = ExpeditionRig.Create();
            var team = rig.Team("rabbit", "duckling");
            Assert.AreEqual(ExpeditionStartResult.Started, rig.E.Start(0, ExpeditionRig.Meadows, ExpeditionDuration.EightHours, team, 2));
            var drawn = rig.State.Expeditions.Slots[0].Loot;
            rig.Advance(TimeSpan.FromHours(8));
            double light = rig.Economy.Light.ToDoubleClamped();
            double rate = rig.Economy.GetLightPerSecond().ToDoubleClamped();
            rig.State.Collection.TryGet(team[0], out var before0);
            long masteryBefore = rig.P.Store.Get(ExpeditionRig.Grassland).MasteryXp;
            int stickers = rig.State.Inventory.PendingStickers;
            int tickets = rig.A.Tickets.Count;

            Assert.IsTrue(rig.E.TryCollect(0, out var welcome));
            Assert.AreEqual(0, welcome.Slot);
            Assert.AreEqual(ExpeditionRig.Meadows, welcome.Destination);
            Assert.AreEqual(2, welcome.TeamSize);
            Assert.AreEqual(team[1], welcome.AnimalAt(1));
            Assert.AreEqual(ContentId.FromSource("species.duckling"), welcome.SpeciesAt(1));
            Assert.AreEqual(ContentId.FromSource("variant.duckling.brown"), welcome.VariantAt(1));
            Assert.AreEqual(1, welcome.PostcardId);
            Assert.IsTrue(welcome.FirstFromDestination);
            double expected = Math.Max(1.0, rate * drawn.LightMinutes * 60.0);
            Assert.AreEqual(expected, welcome.Light.ToDoubleClamped(), expected * 1e-6, "Minutes of the production of now.");
            Assert.AreEqual(light + expected, rig.Economy.Light.ToDoubleClamped(), expected * 1e-6);
            rig.State.Collection.TryGet(team[0], out var after0);
            Assert.AreEqual(before0.BondPoints + 5, after0.BondPoints, "5 bond points to each animal.");
            Assert.AreEqual(masteryBefore + 15, rig.P.Store.Get(ExpeditionRig.Grassland).MasteryXp, "The Prairie's mastery XP.");
            Assert.AreEqual(stickers + drawn.Stickers, rig.State.Inventory.PendingStickers, "Stickers stay a count (T44).");
            Assert.AreEqual(tickets + drawn.Tickets, rig.A.Tickets.Count, "8 h: 1 ticket into the pool.");
            Assert.IsFalse(rig.E.IsAway(team[0]));
            Assert.AreEqual(1, rig.State.Expeditions.Completed);

            // Postcards: rendered once, the first of a destination kept for ever, the 24 most recent others.
            Assert.AreEqual(1, rig.E.PostcardCount);
            Assert.AreEqual(1, rig.E.PostcardAt(0));
            rig.E.PostcardRendered(1);
            Assert.IsTrue(rig.E.TryGetPostcard(1, out var card) && card.Rendered && card.FirstOfDestination);
            for (int i = 0; i < 30; i++)
            {
                rig.Start(0, ExpeditionDuration.OneHour, "hedgehog");
                rig.Advance(TimeSpan.FromHours(1));
                Assert.IsTrue(rig.E.TryCollect(0, out var next));
                Assert.IsFalse(next.FirstFromDestination);
            }
            Assert.AreEqual(25, rig.E.PostcardCount, "The first and the 24 most recent.");
            Assert.AreEqual(31, rig.E.PostcardAt(0), "Newest first.");
            Assert.AreEqual(1, rig.E.PostcardAt(24), "The first of the Flower Meadows stays.");
            Assert.IsFalse(rig.E.TryGetPostcard(2, out _), "The oldest others went.");
        }

        [Test]
        public void TheHooks_FeedTheWishesAndTheJournal()
        {
            var rig = ExpeditionRig.Create();
            int day = rig.Daily.TodayKey;
            var visit = rig.State.DailyVisit;
            var rabbit = rig.Team("rabbit")[0];
            void Wish(WishTemplateId template, uint animal = 0, ContentId param = default)
            {
                visit.Wishes[visit.WishCount++] = new WishRecord
                {
                    Template = (ushort)template, Animal = animal == 0 ? 900000u + (uint)template : animal, Target = 1, DrawnDay = day, Param = param,
                };
            }
            Wish(WishTemplateId.StartExpedition);
            Wish(WishTemplateId.StartLongExpedition);
            Wish(WishTemplateId.SendAnimalOnExpedition, rabbit);
            Wish(WishTemplateId.SendTeamOfThree);
            Wish(WishTemplateId.OpenPostcard);
            Wish(WishTemplateId.HeadToDestination, 0, ExpeditionRig.Meadows);

            rig.Start(0, ExpeditionDuration.OneHour, "rabbit", "duckling");
            Assert.AreEqual(1, rig.Hooks.Departures);
            bool Done(WishTemplateId t) => visit.Wishes.Take(visit.WishCount).Any(w => (WishTemplateId)w.Template == t && w.IsDone);
            Assert.IsTrue(Done(WishTemplateId.StartExpedition));
            Assert.IsFalse(Done(WishTemplateId.StartLongExpedition), "1 h is not 4 h or more.");
            Assert.IsTrue(Done(WishTemplateId.SendAnimalOnExpedition));
            Assert.IsFalse(Done(WishTemplateId.SendTeamOfThree));
            Assert.IsTrue(Done(WishTemplateId.HeadToDestination));
            Assert.IsFalse(Done(WishTemplateId.OpenPostcard));
            rig.Advance(TimeSpan.FromHours(1));
            Assert.IsTrue(rig.E.TryCollect(0, out _));
            Assert.AreEqual(1, rig.Hooks.Welcomes);
            Assert.IsTrue(Done(WishTemplateId.OpenPostcard));
            rig.Start(1, ExpeditionDuration.FourHours, "hedgehog", "mole", "squirrel");
            Assert.IsTrue(Done(WishTemplateId.StartLongExpedition));
            Assert.IsTrue(Done(WishTemplateId.SendTeamOfThree));

            // An animal away expresses no wish.
            Assert.IsTrue(rig.State.Expeditions.IsAway(rig.Team("hedgehog")[0]));
            Assert.IsTrue(WishCatalog.IsShipped(WishSystem.Expeditions));
            Assert.IsTrue(WeeklyJournalCatalog.TryGet(JournalGoalId.PostcardPlaces, out var places) && places.ShippedInThisBuild);
            var journal = new WeeklyJournalService(rig.Daily.Context);
            Assert.Greater(journal.DrawWeight(JournalGoalId.Expeditions, false), 0.0, "JRN-024 in the Timers slot.");
            Assert.Greater(journal.DrawWeight(JournalGoalId.LongExpeditions, false), 0.0);
            Assert.AreEqual(0.0, journal.DrawWeight(JournalGoalId.PostcardPlaces, false), "One destination open: JRN-026 needs 2.");
        }

        // ---------------- Save ----------------

        [Test]
        public void TheSave_KeepsTeamsTimersLootAndPostcards()
        {
            var rig = ExpeditionRig.Create(gems: 900);
            Assert.IsTrue(rig.E.TryBuySlot());
            rig.Start(0, ExpeditionDuration.OneHour, "fawn");
            rig.Advance(TimeSpan.FromHours(1));
            Assert.IsTrue(rig.E.TryCollect(0, out _));
            rig.E.PostcardRendered(1);
            Assert.AreEqual(ExpeditionStartResult.Started, rig.E.StartChallenge(2, 3, ExpeditionRig.Meadows, rig.Team("rabbit", "duckling", "squirrel"), 3));
            rig.Start(1, ExpeditionDuration.FourHours, "hedgehog");
            var e = rig.State.Expeditions;

            var loaded = ExpeditionRig.RoundTrip(rig.State).Expeditions;
            Assert.AreEqual(e.SlotBought, loaded.SlotBought);
            Assert.AreEqual(e.FirstStrollDone, loaded.FirstStrollDone);
            Assert.AreEqual(e.LongWithoutEgg, loaded.LongWithoutEgg);
            Assert.AreEqual(e.Started, loaded.Started);
            Assert.AreEqual(e.Completed, loaded.Completed);
            Assert.AreEqual(e.NextPostcardId, loaded.NextPostcardId);
            Assert.AreEqual(e.JournalPlacesMask, loaded.JournalPlacesMask);
            for (int s = 0; s < ExpeditionState.MaxSlots; s++)
            {
                var a = e.Slots[s];
                var b = loaded.Slots[s];
                Assert.AreEqual(a.Active, b.Active, "Slot " + s);
                if (!a.Active)
                    continue;
                Assert.AreEqual(a.Destination, b.Destination);
                Assert.AreEqual(a.Duration, b.Duration);
                Assert.AreEqual(a.TeamSize, b.TeamSize);
                for (int i = 0; i < 3; i++)
                {
                    Assert.AreEqual(a.AnimalAt(i), b.AnimalAt(i));
                    Assert.AreEqual(a.VariantAt(i), b.VariantAt(i));
                    Assert.AreEqual(a.SpeciesAt(i), b.SpeciesAt(i));
                }
                Assert.AreEqual(a.StartUnixMs, b.StartUnixMs);
                Assert.AreEqual(a.ReturnUnixMs, b.ReturnUnixMs);
                Assert.AreEqual(a.Stars, b.Stars);
                Assert.AreEqual(a.Challenge, b.Challenge);
                Assert.AreEqual(a.FirstStroll, b.FirstStroll);
                Assert.AreEqual(a.Seed, b.Seed);
                Assert.AreEqual(a.Loot.LightMinutes, b.Loot.LightMinutes);
                Assert.AreEqual(a.Loot.Treat, b.Loot.Treat);
                Assert.AreEqual(a.Loot.Treats, b.Loot.Treats);
                Assert.AreEqual(a.Loot.SeedKinds, b.Loot.SeedKinds);
                for (int i = 0; i < 4; i++)
                {
                    Assert.AreEqual(a.Loot.SeedAt(i), b.Loot.SeedAt(i));
                    Assert.AreEqual(a.Loot.SeedsAt(i), b.Loot.SeedsAt(i));
                }
                Assert.AreEqual(a.Loot.Stickers, b.Loot.Stickers);
                Assert.AreEqual(a.Loot.DiscoveryEggs, b.Loot.DiscoveryEggs);
                Assert.AreEqual(a.Loot.Tickets, b.Loot.Tickets);
                Assert.AreEqual(a.Loot.BondPoints, b.Loot.BondPoints);
                Assert.AreEqual(a.Loot.MasteryXp, b.Loot.MasteryXp);
            }
            Assert.AreEqual(3, (int)loaded.Slots[2].Challenge);
            Assert.AreEqual(-1, (int)loaded.Slots[1].Challenge);
            Assert.AreEqual(e.PostcardCount, loaded.PostcardCount);
            Assert.AreEqual(1, loaded.Postcards[0].Id);
            Assert.IsTrue(loaded.Postcards[0].Rendered && loaded.Postcards[0].FirstOfDestination);
            Assert.AreEqual(e.Postcards[0].UnixDay, loaded.Postcards[0].UnixDay);
            Assert.GreaterOrEqual(SaveSchema.CurrentVersion, 12, "Schema 12 brought the expeditions (T43's garden made it 13).");
            Assert.AreEqual(18, (int)SaveSectionId.Expeditions);
        }

        // ---------------- Stable values and performance ----------------

        [Test]
        public void WireIds_AreStable()
        {
            Assert.AreEqual(18, (int)EconomyReason.Expedition);
            Assert.AreEqual(38, (int)EconomyReason.SpeedUp);
            Assert.AreEqual(39, (int)EconomyReason.Slot);
            Assert.AreEqual(4, (int)BondSource.Expedition);
            Assert.AreEqual(14, (int)TicketSource.Expedition);
            Assert.AreEqual(5, (int)WishParamKind.Destination);
            Assert.AreEqual(140, (int)Services.Analytics.AnalyticsEventId.ExpeditionStart);
            Assert.AreEqual(141, (int)Services.Analytics.AnalyticsEventId.ExpeditionReturn);
            Assert.AreEqual(142, (int)Services.Analytics.AnalyticsEventId.ExpeditionSpeedup);
        }

        [Test]
        public void TheTick_AndTheReads_DoNotAllocate()
        {
            var rig = ExpeditionRig.Create();
            rig.Start(0, ExpeditionDuration.FourHours, "rabbit", "duckling");
            rig.Start(1, ExpeditionDuration.OneHour, "hedgehog");
            var team = rig.Team("mole", "squirrel");
            var into = new uint[3];
            uint away = rig.Team("rabbit")[0];
            double sink = 0;
            void Frame()
            {
                rig.Clock.Advance(TimeSpan.FromSeconds(1));
                rig.E.Tick();
                for (int s = 0; s < rig.E.SlotCount; s++)
                {
                    sink += (int)rig.E.StateOf(s) + rig.E.SecondsLeft(s) + rig.E.TotalSecondsOf(s) + rig.E.SpeedUpCost(s) + rig.E.TeamSizeOf(s)
                            + rig.E.TeamMember(s, 0) + rig.E.StarsOf(s) + (int)rig.E.UnlockOf(s);
                }
                sink += rig.E.BackCount + rig.E.SoonestReturn + rig.E.SpeedUpsLeftToday + rig.E.DiscoveryGuaranteeIn + rig.E.PostcardCount;
                sink += rig.E.IsAway(away) ? 1 : 0;
                sink += rig.E.IsDestinationOpen(ExpeditionRig.Meadows) ? 1 : 0;
                sink += rig.E.Rate(ExpeditionRig.Meadows, team, 2).Points;
                sink += rig.E.DurationSeconds(ExpeditionDuration.EightHours);
                rig.E.Preview(ExpeditionRig.Meadows, ExpeditionDuration.EightHours, 2, out var preview);
                sink += preview.LightMinutes;
                sink += rig.E.SuggestTeam(ExpeditionRig.Meadows, into);
            }
            Frame();
            Assert.That(Frame, Is.Not.AllocatingGCMemory());
            rig.Advance(TimeSpan.FromHours(1));
            Assert.That(Frame, Is.Not.AllocatingGCMemory(), "With a team back.");
            Assert.Greater(sink, 0);
        }
    }
}
