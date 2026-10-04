#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Pure formulas from "02 — Formules exactes de progression & économie".</summary>
    public static class EconomyFormulas
    {
        /// <summary>P = BaseSpecies x Rarity x Mastery x Bond x Global, for one animal.</summary>
        public static BigValue AnimalProduction(
            EconomyConfig config, double baseProduction, RarityTier rarity, int masteryLevel, ushort bondPoints, BigValue global)
        {
            double rarityMultiplier = config.RarityMultipliers[(int)rarity];
            double masteryMultiplier = 1.0 + config.MasteryPerLevel * masteryLevel;
            double bondRatio = Math.Min(bondPoints, config.BondPointsForMax) / (double)config.BondPointsForMax;
            double bondMultiplier = 1.0 + config.BondMaxBonus * bondRatio;
            return BigValue.Multiply(
                BigValue.FromDouble(baseProduction * rarityMultiplier * masteryMultiplier * bondMultiplier), global);
        }

        /// <summary>Cost(n) = round(base x growth^n), n = eggs bought in the current cycle.</summary>
        public static BigValue EggCost(double baseCost, double growth, int purchasedInCycle)
        {
            if (purchasedInCycle < 0)
                throw new ArgumentOutOfRangeException(nameof(purchasedInCycle));

            var raw = BigValue.Multiply(BigValue.PowPositive(BigValue.FromDouble(growth), purchasedInCycle), baseCost);
            return RoundIfSmall(raw);
        }

        /// <summary>ExpansionCost(k) = BaseExpansion x 2.15^k, per biome.</summary>
        public static BigValue ExpansionCost(BigValue baseExpansion, double growth, int expansionIndex)
        {
            if (expansionIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(expansionIndex));
            return RoundIfSmall(BigValue.Multiply(baseExpansion, BigValue.PowPositive(BigValue.FromDouble(growth), expansionIndex)));
        }

        /// <summary>XP needed to go from level L-1 to L: round(20 x L^1.35). Level 0 means undiscovered.</summary>
        public static int MasteryXpForLevel(EconomyConfig config, int targetLevel)
        {
            if (targetLevel <= 0)
                return 0;
            return (int)Math.Round(config.MasteryXpBase * Math.Pow(targetLevel, config.MasteryXpExponent), MidpointRounding.AwayFromZero);
        }

        /// <summary>Essence = floor((LifetimeLight / RenewalScale)^0.42) + completion bonus.</summary>
        public static long RenewalEssence(EconomyConfig config, BigValue lifetimeLight, double renewalScale, long completionBonus)
        {
            if (renewalScale <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(renewalScale));
            if (lifetimeLight.IsZero || lifetimeLight.IsNegative)
                return completionBonus;

            var ratio = BigValue.Divide(lifetimeLight, renewalScale);
            double essence = Math.Floor(BigValue.PowPositive(ratio, config.RenewalExponent).ToDoubleClamped());
            if (essence >= long.MaxValue - completionBonus)
                return long.MaxValue;
            return (long)essence + completionBonus;
        }

        /// <summary>Rounds to an integer while the value is small enough for that to matter.</summary>
        private static BigValue RoundIfSmall(BigValue value)
        {
            if (value.Exponent10 >= 15)
                return value;
            return BigValue.FromDouble(Math.Round(value.ToDoubleClamped(), MidpointRounding.AwayFromZero));
        }
    }
}
