#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Stable values, used by analytics. Never renumber.</summary>
    public enum CurrencyId : byte
    {
        Light = 1,
        Essence = 2,
        Gems = 3,
    }

    /// <summary>Source/sink reasons for analytics and debugging. Stable values, append only.</summary>
    public enum EconomyReason : ushort
    {
        Unknown = 0,
        Production = 1,
        Offline = 2,
        EggPurchase = 3,
        Upgrade = 4,
        Expansion = 5,
        Reward = 6,
        Purchase = 7,
        Renewal = 8,
        Event = 9,
        Migration = 10,
        Debug = 11,
    }

    public readonly struct EconomyChange
    {
        public readonly CurrencyId Currency;
        public readonly BigValue Delta;
        public readonly BigValue NewBalance;
        public readonly EconomyReason Reason;

        public EconomyChange(CurrencyId currency, BigValue delta, BigValue newBalance, EconomyReason reason)
        {
            Currency = currency;
            Delta = delta;
            NewBalance = newBalance;
            Reason = reason;
        }
    }

    /// <summary>
    /// Balance constants from "02 — Formules exactes". Baseline values; meant to be overridden
    /// by validated data or Remote Config later, never edited in code at runtime.
    /// </summary>
    public sealed class EconomyConfig
    {
        public static EconomyConfig Default { get; } = new EconomyConfig();

        /// <summary>Indexed by RarityTier: Common, Uncommon, Rare, Epic, Legendary, Mythic.</summary>
        public double[] RarityMultipliers { get; set; } = { 1.00, 1.18, 1.50, 2.10, 3.00, 4.50 };

        /// <summary>MasteryMultiplier = 1 + MasteryPerLevel x Level.</summary>
        public double MasteryPerLevel { get; set; } = 0.015;

        /// <summary>Bond adds at most this much (1.00 to 1.20).</summary>
        public double BondMaxBonus { get; set; } = 0.20;

        public int BondPointsForMax { get; set; } = 1000;

        public double EggBaseCost { get; set; } = 10.0;
        public double EggCostGrowth { get; set; } = 1.18;

        public double ExpansionGrowth { get; set; } = 2.15;

        public double MasteryXpBase { get; set; } = 20.0;
        public double MasteryXpExponent { get; set; } = 1.35;

        /// <summary>Species XP per acquisition (doc 02): first discovery, new variant, duplicate, birth.</summary>
        public int MasteryXpDiscovery { get; set; } = 25;
        public int MasteryXpNewVariant { get; set; } = 15;
        public int MasteryXpDuplicate { get; set; } = 2;
        public int MasteryXpBirth { get; set; } = 4;

        /// <summary>Significant milestones every N levels; small economic steps in between.</summary>
        public int MasteryMilestoneEvery { get; set; } = 5;

        public double RenewalExponent { get; set; } = 0.42;

        /// <summary>Production is applied at this cadence, not every frame.</summary>
        public double AdvanceIntervalSeconds { get; set; } = 0.1;

        // ---- Offline (doc 02: OfflineGain = ProductionAtExit x EffectiveSeconds x Efficiency) ----

        /// <summary>Away time paid at full efficiency (baseline 24 h).</summary>
        public double OfflineFullSeconds { get; set; } = 24.0 * 3600.0;

        /// <summary>Beyond the full window, efficiency eases from 1 toward the floor with this time constant.</summary>
        public double OfflineDecaySeconds { get; set; } = 24.0 * 3600.0;

        /// <summary>Efficiency never drops below this: long breaks still pay, just less.</summary>
        public double OfflineFloorEfficiency { get; set; } = 0.25;

        /// <summary>Away time counted at most (bounds clock jumps).</summary>
        public double OfflineMaxSeconds { get; set; } = 30.0 * 24.0 * 3600.0;

        /// <summary>Shorter breaks are paid silently, without the return screen.</summary>
        public double OfflineScreenMinSeconds { get; set; } = 120.0;

        /// <summary>Small backwards corrections (network time sync) are not treated as a clock rollback.</summary>
        public double ClockRollbackToleranceSeconds { get; set; } = 300.0;
    }
}
