using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Editor.Economy
{
    /// <summary>
    /// The content the simulator plays with: the shipped definitions (species, variants, eggs) completed with the planned
    /// roster where content is not built yet (doc 02 Améliorations, sections 2 and 4.2; doc 03 Roster). Each of the 13
    /// biomes gets 6 species of 6 base variants (2 Common, 2 Uncommon, 1 Rare, 1 Epic, like the shipped species), a
    /// Basic egg (B, 1.18, base table) and a Focus egg (4 B, Focus table, biome mastery 3, 3 targets); zones follow
    /// section 4.2 (an arrival, 5 or 6 zones to open, a passage). Planned species are ids under "sim." and never ship.
    /// </summary>
    public static class SimContent
    {
        public const int SpeciesPerBiome = 6;

        /// <summary>Base variants of a planned species, the shipped species' pattern.</summary>
        private static readonly RarityTier[] VariantRarities =
        {
            RarityTier.Common, RarityTier.Common, RarityTier.Uncommon, RarityTier.Uncommon, RarityTier.Rare, RarityTier.Epic,
        };

        private static readonly double[] BasicTable = { 72.0, 20.0, 6.0, 1.6, 0.36, 0.04 };
        private static readonly double[] FocusTable = { 50.0, 30.0, 14.0, 4.5, 1.3, 0.2 };

        /// <summary>Zones to open per biome, in order (section 4.2; biomes 7 to 13 follow the same rule).</summary>
        private static readonly string[][] Expansions =
        {
            new[] { "meadow", "streambend", "orchard", "hill", "windmillrise", "pondedge" },
            new[] { "deepforest", "clearing", "hollow", "brook", "mushroomcorner" },
            new[] { "pond", "reedcorner", "smallisland", "waterfall", "dock" },
            new[] { "rockyshelf", "alpinemeadow", "torrent", "bamboogrove", "peakview" },
            new[] { "pinepocket", "icepondedge", "auroraview", "frostcave" },
            new[] { "leafgarden", "rootgrove", "rainpool", "waterfall", "elephantbath" },
        };

        /// <summary>
        /// Builds the simulated registry from the shipped one: every shipped species, variant and egg is kept (eggs get the
        /// whole biome as pool), the planned species complete each biome to six.
        /// </summary>
        public static ContentRegistry Build(ContentRegistry shipped, ProgressionConfig config, out int plannedSpecies)
        {
            var builder = new ContentRegistryBuilder();
            plannedSpecies = 0;
            var speciesByBiome = new Dictionary<ContentId, List<ContentId>>();
            for (int b = 0; b < config.Biomes.Length; b++)
            {
                var biome = config.Biomes[b];
                builder.AddBiome(new BiomeDefinition(biome.Id, biome.SourceId + ".name", b), biome.SourceId);
                speciesByBiome[biome.Id] = new List<ContentId>();
            }

            for (int i = 0; i < shipped.Species.Count; i++)
            {
                var s = shipped.Species[i];
                if (!speciesByBiome.ContainsKey(s.Biome))
                    continue;
                builder.AddSpecies(new SpeciesDefinition(s.Id, s.DisplayKey, s.RigFamily, s.Biome, s.BaseProduction, s.VisualKey, s.IsSecret),
                    s.DisplayKey.Replace(".name", string.Empty));
                if (!s.IsSecret)
                    speciesByBiome[s.Biome].Add(s.Id);
            }
            for (int i = 0; i < shipped.Variants.Count; i++)
            {
                var v = shipped.Variants[i];
                if (!shipped.Species.TryGet(v.Species, out var owner) || !speciesByBiome.ContainsKey(owner.Biome))
                    continue;
                builder.AddVariant(new VariantDefinition(v.Id, v.Species, v.DisplayKey, v.Rarity, v.Palette, v.PatternChannel, v.PatternStrength,
                    v.IsSecret), v.DisplayKey.Replace(".name", string.Empty));
            }

            for (int b = 0; b < config.Biomes.Length; b++)
            {
                var biome = config.Biomes[b];
                var list = speciesByBiome[biome.Id];
                string tag = biome.SourceId.Substring("biome.".Length);
                for (int n = list.Count; n < SpeciesPerBiome; n++)
                {
                    string speciesSource = $"sim.species.{tag}.{n + 1}";
                    var species = ContentId.FromSource(speciesSource);
                    builder.AddSpecies(new SpeciesDefinition(species, speciesSource + ".name", RigFamily.SmallQuadruped, biome.Id, biome.BaseProduction,
                        "PF_Animal_Sim"), speciesSource);
                    for (int v = 0; v < VariantRarities.Length; v++)
                    {
                        string variantSource = $"sim.variant.{tag}.{n + 1}.{v + 1}";
                        builder.AddVariant(new VariantDefinition(ContentId.FromSource(variantSource), species, variantSource + ".name", VariantRarities[v],
                            new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), variantSource);
                    }
                    list.Add(species);
                    plannedSpecies++;
                }
            }

            for (int b = 0; b < config.Biomes.Length; b++)
            {
                var biome = config.Biomes[b];
                var pool = new EggDropEntry[speciesByBiome[biome.Id].Count];
                for (int i = 0; i < pool.Length; i++)
                    pool[i] = new EggDropEntry(speciesByBiome[biome.Id][i], 1.0);
                string tag = biome.SourceId.Substring("biome.".Length);
                AddEgg(builder, shipped, $"egg.{tag}.basic", biome.Id, EggTier.Basic, biome.EggBase, config.EggGrowth, BasicTable, pool, 0, 0);
                AddEgg(builder, shipped, $"egg.{tag}.focus", biome.Id, EggTier.Focus, 4 * biome.EggBase, config.EggGrowth, FocusTable, pool, 3, 3);
            }
            for (int i = 0; i < shipped.Eggs.Count; i++)
            {
                var egg = shipped.Eggs[i];
                if (egg.Tier == EggTier.Discovery)
                    builder.AddEgg(egg, egg.DisplayKey.Replace(".name", string.Empty));
            }
            return builder.Build();
        }

        /// <summary>
        /// A biome's egg: the shipped definition's numbers when it exists, the doc's otherwise; the whole biome as pool. The
        /// game prices every biome egg with econ.egg.growth (HatchService.GrowthOverride): planned eggs carry it too.
        /// </summary>
        private static void AddEgg(ContentRegistryBuilder builder, ContentRegistry shipped, string source, ContentId biome, EggTier tier, double baseCost,
            double growth, double[] table, EggDropEntry[] pool, int unlockMastery, int targets)
        {
            var id = ContentId.FromSource(source);
            var palette = new Color32[EggDefinition.PaletteSize];
            if (shipped.Eggs.TryGet(id, out var real))
            {
                baseCost = real.BaseCost;
                table = real.RarityWeights;
                unlockMastery = real.UnlockBiomeMastery;
                targets = real.FocusTargetCount;
                palette = real.Palette;
            }
            builder.AddEgg(new EggDefinition(id, source + ".name", biome, tier, palette, 15, 0, 15, "PF_Egg_Base", baseCost, growth, table, pool,
                EggDefinition.DefaultPityHardLimit, default, unlockMastery, targets), source);
        }

        /// <summary>
        /// Zones of the 13 biomes: the shipped Prairie table, then section 4.2's order for the next five and five zones to
        /// open for biomes 7 to 13; every biome but the last ends with its passage.
        /// </summary>
        public static ZoneDefinition[] Zones(ProgressionConfig config)
        {
            var zones = new List<ZoneDefinition>(config.Zones);
            for (int b = 1; b < config.Biomes.Length; b++)
            {
                string tag = config.Biomes[b].SourceId.Substring("biome.".Length);
                zones.Add(new ZoneDefinition(tag, "arrival", ZoneRole.Arrival, 0, 0));
                string[] names = b < Expansions.Length ? Expansions[b] : new[] { "zone1", "zone2", "zone3", "zone4", "zone5" };
                for (int k = 0; k < names.Length; k++)
                    zones.Add(new ZoneDefinition(tag, names[k], ZoneRole.Expansion, k + 1, k + 1));
                if (b < config.Biomes.Length - 1)
                    zones.Add(new ZoneDefinition(tag, "passage", ZoneRole.Passage, names.Length + 1, 0));
            }
            return zones.ToArray();
        }
    }
}
