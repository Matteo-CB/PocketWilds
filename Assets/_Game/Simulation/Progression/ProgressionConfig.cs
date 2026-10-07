#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public enum UpgradeKind : byte
    {
        Production = 0,
        Nests = 1,
        Curiosity = 2,
    }

    public enum ZoneRole : byte
    {
        Arrival = 0,
        Expansion = 1,
        Passage = 2,
    }

    /// <summary>What has to be waited for before a biome gate can open, besides the other conditions.</summary>
    public enum GateFloorKind : byte
    {
        None = 0,
        PlayMinutes = 1,
        Day = 2,
    }

    /// <summary>
    /// One biome's economy and gate (doc 02 Améliorations, sections 2 and 5.1). B is the base price of the biome's
    /// Basic egg: every other price of the biome is a multiple of it.
    /// </summary>
    public readonly struct BiomeEconomy
    {
        public readonly ContentId Id;
        public readonly string SourceId;
        public readonly double EggBase;
        public readonly double BaseProduction;
        public readonly int GateRefugeLevel;
        public readonly int GateSpecies;
        public readonly int GateMastery;
        public readonly GateFloorKind FloorKind;
        public readonly int FloorValue;
        public readonly ContentId Treat;

        public BiomeEconomy(string sourceId, double eggBase, double baseProduction, int gateRefugeLevel, int gateSpecies, int gateMastery,
            GateFloorKind floorKind, int floorValue, string treat)
        {
            SourceId = sourceId;
            Id = ContentId.FromSource(sourceId);
            EggBase = eggBase;
            BaseProduction = baseProduction;
            GateRefugeLevel = gateRefugeLevel;
            GateSpecies = gateSpecies;
            GateMastery = gateMastery;
            FloorKind = floorKind;
            FloorValue = floorValue;
            Treat = ContentId.FromSource(treat);
        }
    }

    /// <summary>One zone of a biome: a streamed chunk the player opens in order (doc 02 Améliorations, section 4).</summary>
    public readonly struct ZoneDefinition
    {
        public readonly ContentId Id;
        public readonly string SourceId;
        public readonly ContentId Biome;
        public readonly ZoneRole Role;

        /// <summary>Bit of the zone in BiomeProgress.ZonesOpened (0 = arrival).</summary>
        public readonly int Index;

        /// <summary>k of the doc: 1 for the first zone to open, 0 for the arrival and the passage.</summary>
        public readonly int ExpansionIndex;

        /// <summary>Address of the zone's chunk in the biome's chunk set.</summary>
        public readonly string ChunkAddress;

        /// <summary>Localization key of the zone's name.</summary>
        public readonly string DisplayKey;

        public ZoneDefinition(string biome, string chunk, ZoneRole role, int index, int expansionIndex)
        {
            SourceId = "zone." + biome + "." + chunk;
            Id = ContentId.FromSource(SourceId);
            Biome = ContentId.FromSource("biome." + biome);
            Role = role;
            Index = index;
            ExpansionIndex = expansionIndex;
            ChunkAddress = "chunk." + biome + "." + chunk;
            DisplayKey = SourceId + ".name";
        }
    }

    /// <summary>A common decoration bought in Light once the refuge level unlocks it (doc 02, sections 6.3 and 8).</summary>
    public readonly struct CommonDecoration
    {
        public readonly ContentId Id;
        public readonly string SourceId;
        public readonly int UnlockLevel;

        public CommonDecoration(string sourceId, int unlockLevel)
        {
            SourceId = sourceId;
            Id = ContentId.FromSource(sourceId);
            UnlockLevel = unlockLevel;
        }
    }

    /// <summary>A treat sold at the treat stand, the treat of one biome (doc 01 Contenu, section 10).</summary>
    public readonly struct TreatDefinition
    {
        public readonly ContentId Id;
        public readonly string SourceId;
        public readonly RigFamily PreferredFamily;

        public TreatDefinition(string sourceId, RigFamily preferredFamily)
        {
            SourceId = sourceId;
            Id = ContentId.FromSource(sourceId);
            PreferredFamily = preferredFamily;
        }
    }

    /// <summary>
    /// Every number of the Light upgrades, zones, refuge level, biome mastery, biome gates, Light decorations and
    /// treat stand (doc « 02 — Améliorations, agrandissements, niveau du refuge et ouverture des biomes »). Defaults are
    /// the doc's starting calibration; <see cref="TryOverride"/> applies a Remote Config value within the doc's bounds
    /// (section 15) and ignores anything outside them. A change never edits a save: bought levels stay bought.
    /// </summary>
    public sealed class ProgressionConfig
    {
        public static ProgressionConfig Default { get; } = new ProgressionConfig();

        // ---- Upgrades (section 3) ----
        public double ProductionCostMult = 5.0;
        public double ProductionGrowth = 1.25;
        public double ProductionStep = 0.10;
        public int ProductionMilestoneEvery = 25;
        public double ProductionMilestoneMult = 2.0;
        public double NestsCostMult = 20.0;
        public double NestsGrowth = 1.35;
        public double NestsStep = 0.96;
        public int NestsMax = 50;
        public int NestsUnlockMastery = 2;
        public double CuriosityCostMult = 50.0;
        public double CuriosityGrowth = 2.0;
        public int CuriosityMax = 10;
        public int CuriosityUnlockMastery = 5;

        /// <summary>Variant chance added by each Curious Eye level (1 percentage point).</summary>
        public double CuriosityPerLevel = 0.01;

        // ---- Zones (section 4) ----
        public double ZoneCostMult = 25.0;
        public double ZoneGrowth = 2.15;
        public double ZoneProductionMult = 1.25;
        public int ZoneMasteryStep = 3;
        public int ZoneVisibleAnimals = 3;

        /// <summary>Opening gift: this many minutes of the production at that moment.</summary>
        public double ZoneGiftMinutes = 10.0;

        // ---- Biome gates (section 5) ----
        public double GateCostMult = 20.0;
        public long GateGems = 50;

        // ---- Refuge level (section 6) ----
        public double RefugeCurveMult = 20.0;
        public double RefugeCurveExp = 1.45;
        public int PointsSpecies = 10;
        public int PointsVariant = 3;
        public int PointsSecretVariant = 15;
        public int PointsTrait = 5;
        public int PointsSpeciesMasteryMilestone = 4;
        public int PointsBiome = 50;
        public int PointsZone = 12;
        public int PointsBiomeMasteryLevel = 5;
        public long LevelGemsUpTo50 = 5;
        public long LevelGemsAfter50 = 3;
        public int LevelEssenceEvery = 10;
        public int LevelEssence = 5;

        // ---- Biome mastery (section 7) ----
        public double MasteryCurveMult = 40.0;
        public double MasteryCurveExp = 1.3;
        public int XpSpecies = 40;
        public int XpVariant = 12;
        public int XpKnownHatch = 2;
        public int XpBirth = 6;
        public int XpActivity = 6;
        public int XpZone = 60;
        public int XpDecoration = 10;
        public int XpSticker = 8;
        public int XpSpeciesMilestone = 20;
        public int XpWish = 10;
        public int XpExpedition = 15;
        public double MasteryProductionStep = 0.02;

        // ---- Light decorations and treat stand (sections 8 and 9) ----
        public double DecorationLightMult = 30.0;
        public double DecorationCopyGrowth = 1.6;
        public int DecorationMaxCopies = 10;
        public int TreatsDaily = 3;
        public double TreatMinutes = 2.0;

        /// <summary>The player's calendar (days since install, midnight refills). Tests use a fixed zone.</summary>
        public TimeZoneInfo TimeZone = TimeZoneInfo.Local;

        // ---- Tables ----

        /// <summary>The 13 biomes in opening order (section 2 and 5.1).</summary>
        public BiomeEconomy[] Biomes { get; } =
        {
            new BiomeEconomy("biome.grassland", 10, 1, 1, 0, 0, GateFloorKind.None, 0, "treat.clover_crunch"),
            new BiomeEconomy("biome.forest", 300, 8, 4, 4, 3, GateFloorKind.PlayMinutes, 30, "treat.toasted_hazelnut"),
            new BiomeEconomy("biome.lake", 2_500, 60, 9, 5, 5, GateFloorKind.Day, 2, "treat.watercress_cookie"),
            new BiomeEconomy("biome.mountain", 20_000, 450, 15, 5, 6, GateFloorKind.Day, 3, "treat.alpine_biscuit"),
            new BiomeEconomy("biome.snow", 160_000, 3_500, 22, 5, 8, GateFloorKind.Day, 6, "treat.frost_berry"),
            new BiomeEconomy("biome.jungle", 1.3e6, 26_000, 30, 5, 9, GateFloorKind.Day, 9, "treat.mango_bite"),
            new BiomeEconomy("biome.desert", 1e7, 200_000, 38, 5, 10, GateFloorKind.Day, 13, "treat.golden_date"),
            new BiomeEconomy("biome.sakura", 8e7, 1.5e6, 46, 5, 10, GateFloorKind.Day, 18, "treat.cherry_bite"),
            new BiomeEconomy("biome.mushroom", 6.5e8, 1.1e7, 54, 5, 10, GateFloorKind.Day, 24, "treat.mushroom_biscuit"),
            new BiomeEconomy("biome.cloudlands", 5e9, 8.5e7, 62, 5, 10, GateFloorKind.Day, 30, "treat.cloud_marshmallow"),
            new BiomeEconomy("biome.lunar", 4e10, 6.5e8, 70, 5, 10, GateFloorKind.Day, 37, "treat.moon_honey"),
            new BiomeEconomy("biome.prehistoric", 3.2e11, 5e9, 78, 5, 10, GateFloorKind.Day, 45, "treat.sweet_fern"),
            new BiomeEconomy("biome.starlight", 2.6e12, 3.8e10, 86, 5, 10, GateFloorKind.Day, 54, "treat.star_candy"),
        };

        /// <summary>
        /// Zones per biome, in opening order (section 4.2). Only biomes with built chunks are listed: a biome's zones
        /// join this table with its content.
        /// </summary>
        public ZoneDefinition[] Zones { get; } =
        {
            new ZoneDefinition("grassland", "start", ZoneRole.Arrival, 0, 0),
            new ZoneDefinition("grassland", "meadow", ZoneRole.Expansion, 1, 1),
            new ZoneDefinition("grassland", "streambend", ZoneRole.Expansion, 2, 2),
            new ZoneDefinition("grassland", "orchard", ZoneRole.Expansion, 3, 3),
            new ZoneDefinition("grassland", "hill", ZoneRole.Expansion, 4, 4),
            new ZoneDefinition("grassland", "windmillrise", ZoneRole.Expansion, 5, 5),
            new ZoneDefinition("grassland", "pondedge", ZoneRole.Expansion, 6, 6),
            new ZoneDefinition("grassland", "forestedge", ZoneRole.Passage, 7, 0),
        };

        /// <summary>The 26 common decorations, one every two refuge levels from 2 to 52 (decorations catalog, section 11).</summary>
        public CommonDecoration[] Decorations { get; } =
        {
            new CommonDecoration("decor.bench_cute", 2), new CommonDecoration("decor.water_bowl", 4),
            new CommonDecoration("decor.food_bowl", 6), new CommonDecoration("decor.flower_patch", 8),
            new CommonDecoration("decor.cushion", 10), new CommonDecoration("decor.tiny_fence", 12),
            new CommonDecoration("decor.log_toy", 14), new CommonDecoration("decor.pond_pebbles", 16),
            new CommonDecoration("decor.picnic_blanket", 18), new CommonDecoration("decor.signpost", 20),
            new CommonDecoration("decor.nest", 22), new CommonDecoration("decor.snow_lantern", 24),
            new CommonDecoration("decor.lamp", 26), new CommonDecoration("decor.bird_bath", 28),
            new CommonDecoration("decor.hollow_log", 30), new CommonDecoration("decor.mushroom_stump", 32),
            new CommonDecoration("decor.windmill_toy", 34), new CommonDecoration("decor.stone_circle", 36),
            new CommonDecoration("decor.fruit_basket", 38), new CommonDecoration("decor.hammock", 40),
            new CommonDecoration("decor.tiny_bridge", 42), new CommonDecoration("decor.kite", 44),
            new CommonDecoration("decor.sakura_lantern", 46), new CommonDecoration("decor.cloud_pillow", 48),
            new CommonDecoration("decor.fossil_display", 50), new CommonDecoration("decor.star_cairn", 52),
        };

        /// <summary>The biome treats sold at the stand (the seed cookie, treat basket and golden treat never are).</summary>
        public TreatDefinition[] Treats { get; } =
        {
            new TreatDefinition("treat.clover_crunch", RigFamily.SmallQuadruped),
            new TreatDefinition("treat.toasted_hazelnut", RigFamily.MediumQuadruped),
            new TreatDefinition("treat.watercress_cookie", RigFamily.SwimmerLow),
            new TreatDefinition("treat.alpine_biscuit", RigFamily.Bird),
            new TreatDefinition("treat.frost_berry", RigFamily.HeavyQuadruped),
            new TreatDefinition("treat.mango_bite", RigFamily.ClimberPrimate),
            new TreatDefinition("treat.golden_date", RigFamily.SmallQuadruped),
            new TreatDefinition("treat.cherry_bite", RigFamily.Bird),
            new TreatDefinition("treat.mushroom_biscuit", RigFamily.Hopper),
            new TreatDefinition("treat.cloud_marshmallow", RigFamily.MediumQuadruped),
            new TreatDefinition("treat.moon_honey", RigFamily.ClimberPrimate),
            new TreatDefinition("treat.sweet_fern", RigFamily.HeavyQuadruped),
            new TreatDefinition("treat.star_candy", RigFamily.SwimmerLow),
        };

        public bool TryGetBiome(ContentId id, out BiomeEconomy biome)
        {
            var biomes = Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i].Id == id)
                {
                    biome = biomes[i];
                    return true;
                }
            }
            biome = default;
            return false;
        }

        /// <summary>Opening order of a biome (0 = Prairie), -1 when unknown.</summary>
        public int BiomeOrder(ContentId id)
        {
            var biomes = Biomes;
            for (int i = 0; i < biomes.Length; i++)
                if (biomes[i].Id == id)
                    return i;
            return -1;
        }

        /// <summary>B of a biome: the base price of its Basic egg (0 when unknown).</summary>
        public double EggBase(ContentId biome) => TryGetBiome(biome, out var b) ? b.EggBase : 0.0;

        // ---- Remote Config (section 15) ----

        private readonly struct Bound
        {
            public readonly double Min;
            public readonly double Max;
            public readonly Action<ProgressionConfig, double> Set;

            public Bound(double min, double max, Action<ProgressionConfig, double> set)
            {
                Min = min;
                Max = max;
                Set = set;
            }
        }

        private static readonly Dictionary<string, Bound> Bounds = new Dictionary<string, Bound>
        {
            ["econ.upgrade.production.cost_mult"] = new Bound(1, 20, (c, v) => c.ProductionCostMult = v),
            ["econ.upgrade.production.growth"] = new Bound(1.10, 1.40, (c, v) => c.ProductionGrowth = v),
            ["econ.upgrade.production.step"] = new Bound(0.02, 0.25, (c, v) => c.ProductionStep = v),
            ["econ.upgrade.production.milestone_every"] = new Bound(10, 50, (c, v) => c.ProductionMilestoneEvery = (int)v),
            ["econ.upgrade.production.milestone_mult"] = new Bound(1.5, 3, (c, v) => c.ProductionMilestoneMult = v),
            ["econ.upgrade.nests.cost_mult"] = new Bound(5, 100, (c, v) => c.NestsCostMult = v),
            ["econ.upgrade.nests.growth"] = new Bound(1.15, 1.60, (c, v) => c.NestsGrowth = v),
            ["econ.upgrade.nests.step"] = new Bound(0.90, 0.99, (c, v) => c.NestsStep = v),
            ["econ.upgrade.nests.max"] = new Bound(10, 80, (c, v) => c.NestsMax = (int)v),
            ["econ.upgrade.curiosity.cost_mult"] = new Bound(10, 200, (c, v) => c.CuriosityCostMult = v),
            ["econ.upgrade.curiosity.growth"] = new Bound(1.5, 3, (c, v) => c.CuriosityGrowth = v),
            ["econ.upgrade.curiosity.max"] = new Bound(5, 15, (c, v) => c.CuriosityMax = (int)v),
            ["econ.zone.cost_mult"] = new Bound(5, 100, (c, v) => c.ZoneCostMult = v),
            ["econ.zone.growth"] = new Bound(1.5, 3, (c, v) => c.ZoneGrowth = v),
            ["econ.zone.production_mult"] = new Bound(1.05, 1.5, (c, v) => c.ZoneProductionMult = v),
            ["econ.zone.mastery_step"] = new Bound(1, 6, (c, v) => c.ZoneMasteryStep = (int)v),
            ["econ.zone.visible_animals"] = new Bound(0, 6, (c, v) => c.ZoneVisibleAnimals = (int)v),
            ["econ.gate.cost_mult"] = new Bound(1, 100, (c, v) => c.GateCostMult = v),
            ["econ.refuge.curve_mult"] = new Bound(5, 60, (c, v) => c.RefugeCurveMult = v),
            ["econ.refuge.curve_exp"] = new Bound(1.2, 1.8, (c, v) => c.RefugeCurveExp = v),
            ["econ.biome_mastery.curve_mult"] = new Bound(10, 100, (c, v) => c.MasteryCurveMult = v),
            ["econ.biome_mastery.curve_exp"] = new Bound(1.1, 1.6, (c, v) => c.MasteryCurveExp = v),
            ["econ.biome_mastery.production_step"] = new Bound(0, 0.05, (c, v) => c.MasteryProductionStep = v),
            ["econ.decoration.light_mult"] = new Bound(5, 100, (c, v) => c.DecorationLightMult = v),
            ["econ.decoration.copy_growth"] = new Bound(1.2, 2.5, (c, v) => c.DecorationCopyGrowth = v),
            ["econ.treats.daily"] = new Bound(0, 5, (c, v) => c.TreatsDaily = (int)v),
            ["econ.treats.minutes"] = new Bound(1, 10, (c, v) => c.TreatMinutes = v),
        };

        /// <summary>Remote Config keys this config understands (section 15).</summary>
        public static IEnumerable<string> RemoteKeys => Bounds.Keys;

        /// <summary>
        /// Applies one Remote Config value. False (and nothing changes) for an unknown key, a value outside the doc's
        /// bounds or a value that is not a number.
        /// </summary>
        public bool TryOverride(string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || !Bounds.TryGetValue(key, out var bound))
                return false;
            if (value < bound.Min || value > bound.Max)
                return false;
            bound.Set(this, value);
            return true;
        }
    }
}
