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
        Decoration = 12,
        Treat = 13,
        BiomeGate = 14,
        EssenceNode = 15,
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

        /// <summary>Indexed by RarityTier: Common, Uncommon, Rare, Epic, Legendary, Fabulous.</summary>
        public double[] RarityMultipliers { get; set; } = { 1.00, 1.18, 1.50, 2.10, 3.00, 4.50 };

        /// <summary>MasteryMultiplier = 1 + MasteryPerLevel x Level.</summary>
        public double MasteryPerLevel { get; set; } = 0.015;

        /// <summary>Bond adds at most this much (1.00 to 1.20).</summary>
        public double BondMaxBonus { get; set; } = 0.20;

        public int BondPointsForMax { get; set; } = 1000;

        public double EggBaseCost { get; set; } = 10.0;
        public double EggCostGrowth { get; set; } = 1.18;

        public double ExpansionGrowth { get; set; } = 2.15;

        // T35C calibration: 2.7 x L^1.15 XP for level L (doc 02 Formules said 20 x L^1.35, out of reach in 120 days).
        public double MasteryXpBase { get; set; } = 2.7;
        public double MasteryXpExponent { get; set; } = 1.15;

        /// <summary>Species XP per acquisition (doc 02): first discovery, new variant, duplicate, birth.</summary>
        public int MasteryXpDiscovery { get; set; } = 25;
        public int MasteryXpNewVariant { get; set; } = 15;
        public int MasteryXpDuplicate { get; set; } = 10;
        public int MasteryXpBirth { get; set; } = 4;

        /// <summary>Significant milestones every N levels; small economic steps in between.</summary>
        public int MasteryMilestoneEvery { get; set; } = 5;

        // ---- Pity (doc 02) ----

        /// <summary>New species bonus = min(cap, perMiss x misses) while the pool has species to find.</summary>
        public double PitySpeciesPerMiss { get; set; } = 0.03;
        public double PitySpeciesCap { get; set; } = 0.35;

        /// <summary>New variant bonus = min(cap, perMiss x (1 + boost x milestones) x misses).</summary>
        public double PityVariantPerMiss { get; set; } = 0.015;
        public double PityVariantCap { get; set; } = 0.30;

        /// <summary>Each mastery milestone (every 5 levels) strengthens the variant pity by this fraction.</summary>
        public double PityVariantMilestoneBoost { get; set; } = 0.10;

        /// <summary>
        /// Ceiling of the new variant chance once Curious Eye adds its points to the protection (doc 02 Améliorations,
        /// section 3.4, and doc 02 Œufs, section 2.1: 30 % of protection + 10 points, 40 % in all).
        /// </summary>
        public double PityVariantTotalCap { get; set; } = 0.40;

        /// <summary>Doc 02 Œufs, section 2.3: weight of undiscovered species of the biome the camera is in.</summary>
        public double DiscoveryCameraBiomeWeight { get; set; } = 2.0;

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
