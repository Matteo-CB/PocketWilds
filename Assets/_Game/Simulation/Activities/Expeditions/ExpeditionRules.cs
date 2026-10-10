#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>What the loot of one departure reads besides the table: the systems open and the bonuses of that moment.</summary>
    public struct ExpeditionLootContext
    {
        /// <summary>The destination biome's treat (an event destination: the newest biome's).</summary>
        public ContentId Treat;

        /// <summary>The seed line drawn from (an event destination: the newest biome's destination line).</summary>
        public ContentId[] SeedLine;

        /// <summary>Plants of <see cref="SeedLine"/> known now, as bits (a biome plant whose biome is closed is removed).</summary>
        public int KnownPlants;

        /// <summary>« Pâtisserie ouverte »: the Bakery's ingredients come.</summary>
        public bool BakeryOpen;

        /// <summary>« Teintures ouvertes »: the Moon Garden's dye comes.</summary>
        public bool DyesOpen;

        /// <summary>A big event runs: its currency is fixed at departure (none before T48).</summary>
        public bool EventRunning;

        /// <summary>The Essence tree's « Light of activities and expeditions » (+value).</summary>
        public double LightBonus;

        /// <summary>The destination biome's mastery XP.</summary>
        public int MasteryXp;
    }

    /// <summary>
    /// The rules of the expeditions (doc 01 Contenu, sections 7.1 to 7.3 and S-ACT-005): the team rating and its stars, the
    /// common loot table with the stars' bonus and the destination's specialty, the draws made at departure with the
    /// expedition's seed, the Discovery egg guarantee, the speed-up price and the challenges' stars. Pure functions on the
    /// config and the catalogue: nothing allocates.
    /// </summary>
    public static class ExpeditionRules
    {
        public const int PointsPerAnimal = 10;
        public const int PointsFromBiome = 10;
        public const int PointsFavoriteFamily = 10;
        public const int PointsThreeSpecies = 10;
        public const int TwoStarPoints = 40;
        public const int ThreeStarPoints = 60;

        private const uint StickerSalt = 0x45585331u;
        private const uint DiscoverySalt = 0x45584432u;
        private const uint SeedSalt = 0x45585333u;
        private const uint IngredientSalt = 0x45584934u;
        private const uint PostcardSalt = 0x45585035u;

        /// <summary>Section 7.2: 1 star under 40 points, 2 from 40, 3 from 60.</summary>
        public static byte Stars(int points) => (byte)(points >= ThreeStarPoints ? 3 : points >= TwoStarPoints ? 2 : 1);

        /// <summary>
        /// Section 7.2: 10 per animal, +10 per animal whose species comes from the destination's biome, +10 for at least one
        /// animal of the destination's favourite family, +10 for 3 different species. 10 to 80 points.
        /// </summary>
        public static ExpeditionRating Rate(int count, int fromBiome, bool favoriteFamily, bool threeSpecies)
        {
            count = Math.Max(0, Math.Min(ExpeditionState.MaxTeam, count));
            fromBiome = Math.Max(0, Math.Min(count, fromBiome));
            bool three = threeSpecies && count == 3;
            bool favorite = favoriteFamily && count > 0;
            int points = count * PointsPerAnimal + fromBiome * PointsFromBiome + (favorite ? PointsFavoriteFamily : 0) + (three ? PointsThreeSpecies : 0);
            return new ExpeditionRating(points, count > 0 ? Stars(points) : (byte)0, (byte)fromBiome, favorite, three);
        }

        /// <summary>A rig family as the catalogue's mask bit (the Penguin has none: no destination favours it).</summary>
        public static RigFamilyMask MaskOf(RigFamily family)
        {
            switch (family)
            {
                case RigFamily.SmallQuadruped: return RigFamilyMask.SmallQuadruped;
                case RigFamily.MediumQuadruped: return RigFamilyMask.MediumQuadruped;
                case RigFamily.HeavyQuadruped: return RigFamilyMask.HeavyQuadruped;
                case RigFamily.Bird: return RigFamilyMask.Bird;
                case RigFamily.SwimmerLow: return RigFamilyMask.SwimmerLow;
                case RigFamily.ClimberPrimate: return RigFamilyMask.ClimberPrimate;
                case RigFamily.Hopper: return RigFamilyMask.Hopper;
                default: return RigFamilyMask.None;
            }
        }

        /// <summary>Doc Gemmes: 1 Gem per started 6 minutes left, 5 at least; 0 when nothing is left.</summary>
        public static int SpeedUpCost(double secondsLeft, ExpeditionConfig config)
        {
            if (!(secondsLeft > 0.0))
                return 0;
            double step = Math.Max(1.0, config.SpeedUpStepMinutes * 60.0);
            long steps = (long)Math.Ceiling(secondsLeft / step - 1e-9);
            long cost = Math.Max(1L, steps) * Math.Max(1, config.SpeedUpGemsPerStep);
            return (int)Math.Min(int.MaxValue, Math.Max(config.SpeedUpMinGems, cost));
        }

        /// <summary>A long expedition counts for the Discovery egg guarantee (8 h or more, the duration chosen).</summary>
        public static bool IsLong(ExpeditionDuration duration, ExpeditionConfig config) => config.Hours(duration) + 1e-9 >= config.GuaranteeMinHours;

        /// <summary>The challenge's stars for a team rating (0: under the first threshold).</summary>
        public static byte ChallengeStars(in ExpeditionChallenge challenge, int points) =>
            (byte)(points >= challenge.ThreeStars ? 3 : points >= challenge.TwoStars ? 2 : points >= challenge.OneStar ? 1 : 0);

        /// <summary>A seed for the postcard's composition (poses, butterflies), from the expedition's seed.</summary>
        public static uint PostcardSeed(uint seed) => SeedHash.Mix(seed, PostcardSalt);

        /// <summary>
        /// The fixed part of a loot (section 7.3 for a 1-star team, the stars' bonus of 7.2, the destination's specialty):
        /// Light minutes, treats, the seed count and the plants that can come, the sticker and Discovery chances in percent,
        /// tickets, the Bakery ingredients (the honey or cream not drawn yet: <see cref="ExpeditionLoot.Ingredient0"/> is
        /// None in a preview), bond and mastery. <paramref name="guaranteed"/>: the Discovery egg is due (100 %).
        /// </summary>
        public static void Preview(ExpeditionConfig config, ExpeditionDestinationInfo info, ExpeditionDuration duration, byte stars,
            in ExpeditionLootContext ctx, bool guaranteed, out ExpeditionLoot loot)
        {
            loot = default;
            var sp = info.Specialty;
            int s = Math.Max(1, Math.Min(3, (int)stars));

            int lightPercent = (s >= 3 ? config.ThreeStarLightPercent : s == 2 ? config.TwoStarLightPercent : 0)
                               + (duration >= sp.LightFrom ? sp.LightPercent : 0);
            loot.LightMinutes = config.Light(duration) * (1.0 + lightPercent / 100.0) * (1.0 + Math.Max(0.0, ctx.LightBonus));

            loot.Treat = ctx.Treat;
            loot.Treats = Math.Max(0, ExpeditionConfig.At(config.Treats, duration)
                                      + (s >= 3 ? config.ThreeStarTreats : s == 2 ? config.TwoStarTreats : 0) + sp.Treats);
            if (!loot.Treat.IsValid)
                loot.Treats = 0;

            // Seeds: the plants that can come (known ones of the line), the total count in Seeds0.
            int seeds = Math.Max(0, ExpeditionConfig.At(config.Seeds, duration) + (s >= 3 ? config.ThreeStarSeeds : 0) + sp.Seeds);
            int kinds = 0;
            var line = ctx.SeedLine;
            if (line != null)
            {
                for (int i = 0; i < line.Length && kinds < 4; i++)
                {
                    if ((ctx.KnownPlants & (1 << i)) == 0)
                        continue;
                    SetSeed(ref loot, kinds++, line[i], 0);
                }
            }
            int plantSeeds = sp.PlantSeeds > 0 && duration >= sp.PlantFrom && IsKnown(in ctx, sp.Plant) ? sp.PlantSeeds : 0;
            if (kinds == 0)
                seeds = 0;
            loot.SeedKinds = kinds;
            loot.Seeds0 = kinds > 0 ? seeds + plantSeeds : 0;

            loot.StickerChance = Math.Max(0, Math.Min(100, ExpeditionConfig.At(config.StickerChance, duration)
                                                         + (s >= 3 ? config.ThreeStarStickerPoints : 0) + sp.StickerPoints));
            loot.DiscoveryChance = guaranteed
                ? 100
                : Math.Max(0, Math.Min(100, ExpeditionConfig.At(config.DiscoveryChance, duration) + (duration >= sp.DiscoveryFrom ? sp.DiscoveryPoints : 0)));
            loot.Tickets = Math.Max(0, ExpeditionConfig.At(config.Tickets, duration) + (duration >= sp.TicketsFrom ? sp.Tickets : 0));

            if (ctx.BakeryOpen)
            {
                loot.Ingredient0 = ContentId.None;
                loot.Ingredients0 = Math.Max(0, ExpeditionConfig.At(config.Ingredients, duration));
                if (sp.Ingredients > 0 && duration >= sp.IngredientFrom)
                {
                    loot.Ingredient1 = sp.Ingredient;
                    loot.Ingredients1 = sp.Ingredients;
                }
            }
            loot.BondPoints = Math.Max(0, config.BondPoints);
            loot.MasteryXp = Math.Max(0, ctx.MasteryXp);
            if (ctx.EventRunning)
                loot.EventCurrency = (int)Math.Round(ExpeditionConfig.At(config.EventCurrency, duration) * (1.0 + sp.CurrencyPercent / 100.0),
                    MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Every draw of a departure, with the expedition's seed (section 7.1, decision of 7 October): the sticker, the
        /// Discovery egg (or the guarantee), each seed over the known plants of the line with equal weights, the honey or the
        /// cream. Same seed, same loot. The Moon Garden's dye is not in the contract's loot: it is left to the Garden's task.
        /// </summary>
        public static void Draw(ExpeditionConfig config, ExpeditionDestinationInfo info, ExpeditionDuration duration, byte stars,
            in ExpeditionLootContext ctx, bool guaranteed, uint seed, out ExpeditionLoot loot)
        {
            Preview(config, info, duration, stars, in ctx, guaranteed, out var preview);
            loot = preview;

            loot.Stickers = preview.StickerChance > 0 && SeedHash.Unit(seed, StickerSalt) * 100f < preview.StickerChance ? 1 : 0;
            loot.StickerChance = 0;
            loot.DiscoveryEggs = guaranteed || (preview.DiscoveryChance > 0 && SeedHash.Unit(seed, DiscoverySalt) * 100f < preview.DiscoveryChance) ? 1 : 0;
            loot.DiscoveryChance = 0;

            // Seeds: the line's plants that can come, then each seed drawn with equal weights; the named plant's seeds join.
            int kinds = preview.SeedKinds;
            int total = preview.Seeds0;
            var sp = info.Specialty;
            int plantSeeds = sp.PlantSeeds > 0 && duration >= sp.PlantFrom && IsKnown(in ctx, sp.Plant) ? sp.PlantSeeds : 0;
            loot.Seeds0 = loot.Seeds1 = loot.Seeds2 = loot.Seeds3 = 0;
            if (kinds > 0)
            {
                var rng = new DeterministicRandom(SeedHash.Mix(seed, SeedSalt));
                for (int i = 0; i < total - plantSeeds; i++)
                {
                    int k = rng.Next(kinds);
                    AddSeeds(ref loot, k, 1);
                }
                if (plantSeeds > 0)
                {
                    int k = KindOf(in loot, sp.Plant);
                    if (k >= 0)
                        AddSeeds(ref loot, k, plantSeeds);
                }
                // Plants that drew nothing leave the list (the loot shows only what came).
                Compact(ref loot);
            }

            // Honey or cream, one chance in two (one draw for the lot: two kinds at most, with the specialty's).
            if (preview.Ingredients0 > 0)
            {
                var kind = SeedHash.Unit(seed, IngredientSalt) < 0.5f ? ExpeditionCatalog.GentleHoney : ExpeditionCatalog.VanillaCream;
                loot.Ingredient0 = kind;
                if (loot.Ingredients1 > 0 && loot.Ingredient1 == kind)
                {
                    loot.Ingredients0 += loot.Ingredients1;
                    loot.Ingredient1 = ContentId.None;
                    loot.Ingredients1 = 0;
                }
            }
            else if (loot.Ingredients1 > 0)
            {
                loot.Ingredient0 = loot.Ingredient1;
                loot.Ingredients0 = loot.Ingredients1;
                loot.Ingredient1 = ContentId.None;
                loot.Ingredients1 = 0;
            }
            else
            {
                loot.Ingredient0 = ContentId.None;
            }
        }

        /// <summary>The bits of the plants of a line known now (a biome plant needs its biome open).</summary>
        public static int KnownPlants(ContentId[] line, ProgressionServices? progression)
        {
            int bits = 0;
            if (line == null)
                return 0;
            for (int i = 0; i < line.Length && i < 30; i++)
            {
                var biome = ExpeditionCatalog.PlantBiome(line[i]);
                bool known = !biome.IsValid || (progression != null ? progression.Store.IsUnlocked(biome) : biome == ActivityCatalog.Meadow);
                if (known)
                    bits |= 1 << i;
            }
            return bits;
        }

        /// <summary>Analytics 141's reward_mask: 1 Light, 2 treats, 4 seeds, 8 sticker, 16 Discovery egg, 32 tickets, 64 ingredients, 128 event currency.</summary>
        public static int RewardMask(in ExpeditionLoot loot)
        {
            int mask = 0;
            if (loot.LightMinutes > 0.0) mask |= 1;
            if (loot.Treats > 0) mask |= 2;
            if (loot.Seeds0 + loot.Seeds1 + loot.Seeds2 + loot.Seeds3 > 0) mask |= 4;
            if (loot.Stickers > 0) mask |= 8;
            if (loot.DiscoveryEggs > 0) mask |= 16;
            if (loot.Tickets > 0) mask |= 32;
            if (loot.Ingredients0 + loot.Ingredients1 > 0) mask |= 64;
            if (loot.EventCurrency > 0) mask |= 128;
            return mask;
        }

        private static bool IsKnown(in ExpeditionLootContext ctx, ContentId plant)
        {
            var line = ctx.SeedLine;
            if (line == null || !plant.IsValid)
                return false;
            for (int i = 0; i < line.Length && i < 30; i++)
                if (line[i] == plant)
                    return (ctx.KnownPlants & (1 << i)) != 0;
            return false;
        }

        private static int KindOf(in ExpeditionLoot loot, ContentId plant)
        {
            for (int i = 0; i < loot.SeedKinds; i++)
                if (loot.SeedAt(i) == plant)
                    return i;
            return -1;
        }

        private static void SetSeed(ref ExpeditionLoot loot, int index, ContentId plant, int count)
        {
            switch (index)
            {
                case 0: loot.Seed0 = plant; loot.Seeds0 = count; break;
                case 1: loot.Seed1 = plant; loot.Seeds1 = count; break;
                case 2: loot.Seed2 = plant; loot.Seeds2 = count; break;
                case 3: loot.Seed3 = plant; loot.Seeds3 = count; break;
            }
        }

        private static void AddSeeds(ref ExpeditionLoot loot, int index, int count)
        {
            switch (index)
            {
                case 0: loot.Seeds0 += count; break;
                case 1: loot.Seeds1 += count; break;
                case 2: loot.Seeds2 += count; break;
                case 3: loot.Seeds3 += count; break;
            }
        }

        private static void Compact(ref ExpeditionLoot loot)
        {
            int write = 0;
            int kinds = loot.SeedKinds;
            for (int read = 0; read < kinds; read++)
            {
                var plant = loot.SeedAt(read);
                int count = loot.SeedsAt(read);
                if (count <= 0)
                    continue;
                SetSeed(ref loot, write++, plant, count);
            }
            for (int i = write; i < 4; i++)
                SetSeed(ref loot, i, ContentId.None, 0);
            loot.SeedKinds = write;
        }
    }
}
