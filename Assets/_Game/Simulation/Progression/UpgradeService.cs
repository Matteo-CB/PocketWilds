#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Quantity buttons of the Upgrades screen (doc 02 Améliorations, section 3.5). Stable values.</summary>
    public enum PurchaseQty : byte
    {
        One = 0,
        Ten = 1,
        Max = 2,
    }

    /// <summary>What a purchase would do now: levels, total price, and why it cannot happen when it cannot.</summary>
    public readonly struct UpgradeQuote
    {
        public readonly ContentId Biome;
        public readonly UpgradeKind Kind;
        public readonly int LevelFrom;
        public readonly int Levels;
        public readonly BigValue Cost;
        public readonly bool Affordable;
        public readonly bool Maxed;

        /// <summary>Biome mastery level the upgrade waits for (0 when open).</summary>
        public readonly int LockedUntilMastery;

        public UpgradeQuote(ContentId biome, UpgradeKind kind, int levelFrom, int levels, BigValue cost, bool affordable, bool maxed,
            int lockedUntilMastery)
        {
            Biome = biome;
            Kind = kind;
            LevelFrom = levelFrom;
            Levels = levels;
            Cost = cost;
            Affordable = affordable;
            Maxed = maxed;
            LockedUntilMastery = lockedUntilMastery;
        }

        public bool Locked => LockedUntilMastery > 0;
        public int LevelTo => LevelFrom + Levels;
    }

    public readonly struct UpgradePurchase
    {
        public readonly ContentId Biome;
        public readonly UpgradeKind Kind;
        public readonly int LevelFrom;
        public readonly int LevelTo;
        public readonly BigValue Cost;
        public readonly PurchaseQty Qty;

        /// <summary>Production only: a x2 milestone (every 25 levels) was crossed by this purchase.</summary>
        public readonly bool MilestoneCrossed;

        public UpgradePurchase(ContentId biome, UpgradeKind kind, int levelFrom, int levelTo, BigValue cost, PurchaseQty qty, bool milestoneCrossed)
        {
            Biome = biome;
            Kind = kind;
            LevelFrom = levelFrom;
            LevelTo = levelTo;
            Cost = cost;
            Qty = qty;
            MilestoneCrossed = milestoneCrossed;
        }
    }

    /// <summary>
    /// Light upgrades, three per open biome (doc 02 Améliorations, section 3):
    /// <list type="bullet">
    /// <item>Production: x (1 + 0.10 L) x 2^floor(L / 25) on the biome's animals, no maximum, price 5 B x 1.25^L.</item>
    /// <item>Cozy Nests: egg prices of the biome x 0.96^L, at most 50, price 20 B x 1.35^L, from biome mastery 2.</item>
    /// <item>Curious Eye: +1 point of undiscovered variant chance per level, at most 10, price 50 B x 2^L, from mastery 5.</item>
    /// </list>
    /// Max is the closed geometric sum (no loop over levels). Never sold for Gems or money, never changes rarity odds.
    /// </summary>
    public sealed class UpgradeService
    {
        private readonly BiomeProgressStore _store;
        private readonly ProgressionConfig _config;
        private readonly EconomyService _economy;
        private readonly BiomeMasteryService _mastery;

        public UpgradeService(BiomeProgressStore store, ProgressionConfig config, EconomyService economy, BiomeMasteryService mastery)
        {
            _store = store;
            _config = config;
            _economy = economy;
            _mastery = mastery;
        }

        /// <summary>Every purchase (analytics, the screen's effects). Not raised for a refused purchase.</summary>
        public event Action<UpgradePurchase>? Purchased;

        public int GetLevel(ContentId biome, UpgradeKind kind)
        {
            var progress = _store.Get(biome);
            switch (kind)
            {
                case UpgradeKind.Production: return progress.ProductionLevel;
                case UpgradeKind.Nests: return progress.NestsLevel;
                default: return progress.CuriosityLevel;
            }
        }

        public int MaxLevel(UpgradeKind kind) =>
            kind == UpgradeKind.Nests ? _config.NestsMax : kind == UpgradeKind.Curiosity ? _config.CuriosityMax : int.MaxValue;

        /// <summary>Biome mastery the upgrade needs (Production comes with the biome).</summary>
        public int UnlockMastery(UpgradeKind kind) =>
            kind == UpgradeKind.Nests ? _config.NestsUnlockMastery : kind == UpgradeKind.Curiosity ? _config.CuriosityUnlockMastery : 0;

        public bool IsUnlocked(ContentId biome, UpgradeKind kind) =>
            _store.IsUnlocked(biome) && _mastery.GetLevel(biome) >= UnlockMastery(kind);

        /// <summary>Price of one level bought from <paramref name="level"/>.</summary>
        public BigValue LevelPrice(ContentId biome, UpgradeKind kind, int level)
        {
            double b = _config.EggBase(biome);
            switch (kind)
            {
                case UpgradeKind.Production: return ProgressionMath.Price(_config.ProductionCostMult * b, _config.ProductionGrowth, level);
                case UpgradeKind.Nests: return ProgressionMath.Price(_config.NestsCostMult * b, _config.NestsGrowth, level);
                default: return ProgressionMath.Price(_config.CuriosityCostMult * b, _config.CuriosityGrowth, level);
            }
        }

        public double Growth(UpgradeKind kind) =>
            kind == UpgradeKind.Production ? _config.ProductionGrowth : kind == UpgradeKind.Nests ? _config.NestsGrowth : _config.CuriosityGrowth;

        public UpgradeQuote Quote(ContentId biome, UpgradeKind kind, PurchaseQty qty) => Quote(biome, kind, qty, _economy.Light);

        public UpgradeQuote Quote(ContentId biome, UpgradeKind kind, PurchaseQty qty, BigValue budget)
        {
            int level = GetLevel(biome, kind);
            int max = MaxLevel(kind);
            int left = max == int.MaxValue ? int.MaxValue : Math.Max(0, max - level);
            if (left == 0)
                return new UpgradeQuote(biome, kind, level, 0, BigValue.Zero, false, true, 0);
            if (!IsUnlocked(biome, kind))
                return new UpgradeQuote(biome, kind, level, 1, LevelPrice(biome, kind, level), false, false,
                    Math.Max(1, UnlockMastery(kind)));

            var first = LevelPrice(biome, kind, level);
            double growth = Growth(kind);
            int levels;
            if (qty == PurchaseQty.One)
                levels = 1;
            else if (qty == PurchaseQty.Ten)
                levels = Math.Min(10, left);
            else
                levels = Math.Max(1, ProgressionMath.MaxAffordable(first, growth, budget, left));
            var cost = ProgressionMath.GeometricTotal(first, growth, levels);
            return new UpgradeQuote(biome, kind, level, levels, cost, cost <= budget, false, 0);
        }

        /// <summary>Buys now. False when locked, at the maximum or not affordable (nothing is spent then).</summary>
        public bool TryBuy(ContentId biome, UpgradeKind kind, PurchaseQty qty, out UpgradePurchase purchase)
        {
            purchase = default;
            var quote = Quote(biome, kind, qty);
            if (quote.Maxed || quote.Locked || !quote.Affordable || quote.Levels <= 0)
                return false;
            if (!_economy.Spend(CurrencyId.Light, quote.Cost, EconomyReason.Upgrade))
                return false;

            var progress = _store.Get(biome);
            int to = quote.LevelTo;
            switch (kind)
            {
                case UpgradeKind.Production: progress.ProductionLevel = to; break;
                case UpgradeKind.Nests: progress.NestsLevel = to; break;
                default: progress.CuriosityLevel = to; break;
            }
            _store.Set(progress);
            if (kind == UpgradeKind.Production)
                _economy.MarkProductionDirty();

            int every = Math.Max(1, _config.ProductionMilestoneEvery);
            bool milestone = kind == UpgradeKind.Production && to / every > quote.LevelFrom / every;
            purchase = new UpgradePurchase(biome, kind, quote.LevelFrom, to, quote.Cost, qty, milestone);
            Purchased?.Invoke(purchase);
            return true;
        }

        /// <summary>Production multiplier at a level: (1 + step x L) x milestone^floor(L / every).</summary>
        public double ProductionMultiplierAt(int level)
        {
            int every = Math.Max(1, _config.ProductionMilestoneEvery);
            return (1.0 + _config.ProductionStep * level) * Math.Pow(_config.ProductionMilestoneMult, level / every);
        }

        public double ProductionMultiplier(ContentId biome) => ProductionMultiplierAt(_store.Get(biome).ProductionLevel);

        /// <summary>Egg price multiplier of the biome: 0.96^L.</summary>
        public double EggPriceMultiplier(ContentId biome) => Math.Pow(_config.NestsStep, _store.Get(biome).NestsLevel);

        /// <summary>Undiscovered variant chance added by Curious Eye (0.01 per level).</summary>
        public double VariantChanceBonus(ContentId biome) =>
            _config.CuriosityPerLevel * Math.Min(_store.Get(biome).CuriosityLevel, _config.CuriosityMax);

        /// <summary>Renewal (doc 02 Renouveau): Production and Cozy Nests start over, Curious Eye stays.</summary>
        public void OnRenewal()
        {
            var biomes = _config.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                var progress = _store.Get(biomes[i].Id);
                if (progress.ProductionLevel == 0 && progress.NestsLevel == 0)
                    continue;
                progress.ProductionLevel = 0;
                progress.NestsLevel = 0;
                _store.Set(progress);
            }
            _economy.MarkProductionDirty();
        }
    }
}
