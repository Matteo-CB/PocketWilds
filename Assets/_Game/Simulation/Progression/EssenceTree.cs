#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public enum EssenceBranch : byte
    {
        Economy = 0,
        Discovery = 1,
        Offline = 2,
        Habitat = 3,
        Breeding = 4,
    }

    /// <summary>What a node changes (doc 02 Renouveau, section 2). Value is per rank unless the node has one rank.</summary>
    public enum EssenceEffect : byte
    {
        /// <summary>+value of the total production per rank (added).</summary>
        ProductionPercent = 0,

        /// <summary>Basic and Focus egg prices × (1 − value) per rank.</summary>
        EggPrice = 1,

        /// <summary>After a Renewal, Production of each open biome starts at this many levels per rank.</summary>
        RestartProduction = 2,

        /// <summary>Zones and biome gates × (1 − value) per rank.</summary>
        ZoneGatePrice = 3,

        /// <summary>+value of the Light of activities and expeditions per rank.</summary>
        ActivityLight = 4,

        /// <summary>× value on the total production (one rank).</summary>
        ProductionMultiplier = 5,

        /// <summary>+value per miss to the new species protection per rank (same cap).</summary>
        NewSpeciesPity = 6,

        /// <summary>+value (relative) of the new variant protection per miss per rank.</summary>
        VariantPity = 7,

        /// <summary>+value (relative) of the trait chances per rank.</summary>
        TraitChance = 8,

        /// <summary>+value to a rare visitor's chance to join per rank.</summary>
        VisitorJoin = 9,

        /// <summary>+value to the sticker chance of an activity per rank.</summary>
        StickerChance = 10,

        /// <summary>+value (relative) of species mastery XP (one rank).</summary>
        SpeciesMasteryXp = 11,

        /// <summary>+value hours to the full offline window per rank.</summary>
        OfflineWindowHours = 12,

        /// <summary>+value to the offline efficiency floor per rank.</summary>
        OfflineFloor = 13,

        /// <summary>Expedition durations × (1 − value) per rank.</summary>
        ExpeditionDuration = 14,

        /// <summary>Garden growth times × (1 − value) per rank.</summary>
        GardenGrowth = 15,

        /// <summary>+value activity tickets at most (one rank).</summary>
        ExtraTicket = 16,

        /// <summary>The return gift from 2 days away, with one more Discovery egg (one rank).</summary>
        WarmReturn = 17,

        /// <summary>+value visible animals per biome per rank.</summary>
        VisibleAnimals = 18,

        /// <summary>+value decoration spots per zone per rank.</summary>
        DecorationSpots = 19,

        /// <summary>+value (relative) of bond points of every animal per rank.</summary>
        BondGain = 20,

        /// <summary>+value (relative) of biome mastery XP per rank.</summary>
        BiomeMasteryXp = 21,

        /// <summary>+value (relative) of bond XP of favorites and companions per rank.</summary>
        FavoriteBond = 22,

        /// <summary>The world's small surprises twice as often (one rank).</summary>
        LivelyRefuge = 23,

        /// <summary>Breeding durations × (1 − value) per rank.</summary>
        BreedingDuration = 24,

        /// <summary>+value to the mutation chance per rank.</summary>
        Mutation = 25,

        /// <summary>+value to the chance of passing a trait per rank.</summary>
        Heritage = 26,

        /// <summary>+value breeding slots (one rank).</summary>
        BreedingSlot = 27,

        /// <summary>+value to the chance of a second egg per rank.</summary>
        Twins = 28,

        /// <summary>A mutation may give a secret color of a species at mastery 30 or more (value per birth, one rank).</summary>
        FineLine = 29,
    }

    /// <summary>One node of the Essence tree: rank r costs Cost × r Essence, needs RequiredPoints ranks in its branch.</summary>
    public readonly struct EssenceNodeDefinition
    {
        public readonly ContentId Id;
        public readonly string SourceId;
        public readonly EssenceBranch Branch;
        public readonly EssenceEffect Effect;
        public readonly double Value;
        public readonly int Ranks;
        public readonly int Cost;
        public readonly int RequiredPoints;

        public EssenceNodeDefinition(string sourceId, EssenceBranch branch, EssenceEffect effect, double value, int ranks, int cost,
            int requiredPoints)
        {
            SourceId = sourceId;
            Id = ContentId.FromSource(sourceId);
            Branch = branch;
            Effect = effect;
            Value = value;
            Ranks = ranks;
            Cost = cost;
            RequiredPoints = requiredPoints;
        }

        /// <summary>Localization key of the node's name (its effect text is built from the values).</summary>
        public string DisplayKey => SourceId + ".name";

        /// <summary>Essence for the rank after <paramref name="rank"/> (rank r costs Cost × r).</summary>
        public int CostOfRank(int rank) => Cost * rank;

        /// <summary>Essence for every rank up to <paramref name="rank"/>.</summary>
        public long TotalCost(int rank) => (long)Cost * rank * (rank + 1) / 2;
    }

    /// <summary>
    /// The 30 nodes of doc « 02 — Renouveau, Essence, maîtrises et liens », section 2, in the doc's order: 5 branches of 6,
    /// two start nodes each, the next three after 5 ranks in the branch, the last after 15. Buying all of them costs
    /// 1 587 Essence (Economy 775, Discovery 288, Offline 197, Habitat 134, Breeding 193).
    /// </summary>
    public static class EssenceCatalog
    {
        public const int StartPoints = 0;
        public const int MiddlePoints = 5;
        public const int FinalPoints = 15;

        public static readonly EssenceNodeDefinition[] Nodes =
        {
            new EssenceNodeDefinition("essence.economy.soft_light", EssenceBranch.Economy, EssenceEffect.ProductionPercent, 0.10, 25, 1, StartPoints),
            new EssenceNodeDefinition("essence.economy.morning_eggs", EssenceBranch.Economy, EssenceEffect.EggPrice, 0.03, 15, 2, StartPoints),
            new EssenceNodeDefinition("essence.economy.restart", EssenceBranch.Economy, EssenceEffect.RestartProduction, 5, 5, 3, MiddlePoints),
            new EssenceNodeDefinition("essence.economy.easy_zones", EssenceBranch.Economy, EssenceEffect.ZoneGatePrice, 0.05, 10, 2, MiddlePoints),
            new EssenceNodeDefinition("essence.economy.activity_harvest", EssenceBranch.Economy, EssenceEffect.ActivityLight, 0.10, 5, 2, MiddlePoints),
            new EssenceNodeDefinition("essence.economy.grand_refuge", EssenceBranch.Economy, EssenceEffect.ProductionMultiplier, 1.5, 1, 25, FinalPoints),

            new EssenceNodeDefinition("essence.discovery.flair", EssenceBranch.Discovery, EssenceEffect.NewSpeciesPity, 0.005, 4, 2, StartPoints),
            new EssenceNodeDefinition("essence.discovery.variant_eye", EssenceBranch.Discovery, EssenceEffect.VariantPity, 0.10, 5, 2, StartPoints),
            new EssenceNodeDefinition("essence.discovery.traits", EssenceBranch.Discovery, EssenceEffect.TraitChance, 0.05, 10, 3, MiddlePoints),
            new EssenceNodeDefinition("essence.discovery.visitors", EssenceBranch.Discovery, EssenceEffect.VisitorJoin, 0.05, 3, 3, MiddlePoints),
            new EssenceNodeDefinition("essence.discovery.stickers", EssenceBranch.Discovery, EssenceEffect.StickerChance, 0.02, 5, 2, MiddlePoints),
            new EssenceNodeDefinition("essence.discovery.fast_mastery", EssenceBranch.Discovery, EssenceEffect.SpeciesMasteryXp, 0.25, 1, 25, FinalPoints),

            new EssenceNodeDefinition("essence.offline.long_nap", EssenceBranch.Offline, EssenceEffect.OfflineWindowHours, 2, 6, 2, StartPoints),
            new EssenceNodeDefinition("essence.offline.soft_wake", EssenceBranch.Offline, EssenceEffect.OfflineFloor, 0.05, 5, 2, StartPoints),
            new EssenceNodeDefinition("essence.offline.quick_trips", EssenceBranch.Offline, EssenceEffect.ExpeditionDuration, 0.04, 5, 3, MiddlePoints),
            new EssenceNodeDefinition("essence.offline.green_thumb", EssenceBranch.Offline, EssenceEffect.GardenGrowth, 0.04, 5, 3, MiddlePoints),
            new EssenceNodeDefinition("essence.offline.extra_ticket", EssenceBranch.Offline, EssenceEffect.ExtraTicket, 1, 1, 15, MiddlePoints),
            new EssenceNodeDefinition("essence.offline.warm_return", EssenceBranch.Offline, EssenceEffect.WarmReturn, 1, 1, 20, FinalPoints),

            new EssenceNodeDefinition("essence.habitat.more_room", EssenceBranch.Habitat, EssenceEffect.VisibleAnimals, 2, 5, 1, StartPoints),
            new EssenceNodeDefinition("essence.habitat.decor_spots", EssenceBranch.Habitat, EssenceEffect.DecorationSpots, 2, 3, 2, StartPoints),
            new EssenceNodeDefinition("essence.habitat.comfort", EssenceBranch.Habitat, EssenceEffect.BondGain, 0.10, 5, 2, MiddlePoints),
            new EssenceNodeDefinition("essence.habitat.biome_mastery", EssenceBranch.Habitat, EssenceEffect.BiomeMasteryXp, 0.10, 5, 3, MiddlePoints),
            new EssenceNodeDefinition("essence.habitat.best_friends", EssenceBranch.Habitat, EssenceEffect.FavoriteBond, 0.10, 3, 2, MiddlePoints),
            new EssenceNodeDefinition("essence.habitat.living_refuge", EssenceBranch.Habitat, EssenceEffect.LivelyRefuge, 2, 1, 20, FinalPoints),

            new EssenceNodeDefinition("essence.breeding.quick_nest", EssenceBranch.Breeding, EssenceEffect.BreedingDuration, 0.05, 8, 1, StartPoints),
            new EssenceNodeDefinition("essence.breeding.mutation", EssenceBranch.Breeding, EssenceEffect.Mutation, 0.005, 6, 2, StartPoints),
            new EssenceNodeDefinition("essence.breeding.heritage", EssenceBranch.Breeding, EssenceEffect.Heritage, 0.03, 5, 2, MiddlePoints),
            new EssenceNodeDefinition("essence.breeding.extra_slot", EssenceBranch.Breeding, EssenceEffect.BreedingSlot, 1, 1, 15, MiddlePoints),
            new EssenceNodeDefinition("essence.breeding.twins", EssenceBranch.Breeding, EssenceEffect.Twins, 0.02, 5, 3, MiddlePoints),
            new EssenceNodeDefinition("essence.breeding.golden_line", EssenceBranch.Breeding, EssenceEffect.FineLine, 0.005, 1, 25, FinalPoints),
        };

        private static Dictionary<ContentId, int>? _index;

        public static bool TryGet(ContentId id, out EssenceNodeDefinition node)
        {
            if (_index == null)
            {
                var index = new Dictionary<ContentId, int>(Nodes.Length);
                for (int i = 0; i < Nodes.Length; i++)
                    index.Add(Nodes[i].Id, i);
                _index = index;
            }
            if (_index.TryGetValue(id, out int at))
            {
                node = Nodes[at];
                return true;
            }
            node = default;
            return false;
        }

        /// <summary>Essence to buy every rank of every node (1 587 with the doc's values).</summary>
        public static long TotalCost()
        {
            long total = 0;
            for (int i = 0; i < Nodes.Length; i++)
                total += Nodes[i].TotalCost(Nodes[i].Ranks);
            return total;
        }
    }

    /// <summary>One Essence node bought (analytics essence_node_buy, the tree's sound and sparkle-free burst).</summary>
    public readonly struct EssencePurchase
    {
        public readonly ContentId Node;
        public readonly int Rank;
        public readonly int Cost;

        public EssencePurchase(ContentId node, int rank, int cost)
        {
            Node = node;
            Rank = rank;
            Cost = cost;
        }
    }

    /// <summary>
    /// The Essence tree (doc 02 Renouveau, section 2): ranks, branch conditions, costs and the effects every system reads.
    /// Effects are summed once per change (<see cref="Changed"/>), never per frame. Redistribution (every rank refunded)
    /// is only possible while a Renewal is being prepared (<see cref="RenewalService"/> opens and closes it).
    /// </summary>
    public sealed class EssenceService
    {
        private readonly PlayerState _state;
        private readonly EconomyService _economy;
        private readonly double[] _effects = new double[30];

        public EssenceService(PlayerState state, EconomyService economy)
        {
            _state = state;
            _economy = economy;
            Recompute();
        }

        /// <summary>A rank was bought or the tree was redistributed: systems refresh their multipliers.</summary>
        public event Action? Changed;

        public event Action<EssencePurchase>? Purchased;

        /// <summary>True only during a Renewal's preparation: <see cref="RefundAll"/> works then and only then.</summary>
        public bool RedistributionOpen { get; internal set; }

        public long Essence => _state.Wallet.Essence;

        public int GetRank(ContentId node) => _state.Progression.EssenceRanks.TryGetValue(node, out int rank) ? rank : 0;

        /// <summary>Ranks bought in a branch (the "points" the next nodes ask for).</summary>
        public int BranchPoints(EssenceBranch branch)
        {
            int points = 0;
            var nodes = EssenceCatalog.Nodes;
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].Branch == branch)
                    points += GetRank(nodes[i].Id);
            return points;
        }

        public bool IsOpen(in EssenceNodeDefinition node) => BranchPoints(node.Branch) >= node.RequiredPoints;

        /// <summary>Cost of the next rank, 0 when the node is at its maximum.</summary>
        public int NextCost(in EssenceNodeDefinition node)
        {
            int rank = GetRank(node.Id);
            return rank >= node.Ranks ? 0 : node.CostOfRank(rank + 1);
        }

        public bool CanBuy(in EssenceNodeDefinition node)
        {
            int rank = GetRank(node.Id);
            return rank < node.Ranks && IsOpen(node) && Essence >= node.CostOfRank(rank + 1);
        }

        public bool TryBuy(ContentId nodeId, out EssencePurchase purchase)
        {
            purchase = default;
            if (!EssenceCatalog.TryGet(nodeId, out var node) || !CanBuy(node))
                return false;
            int rank = GetRank(nodeId) + 1;
            int cost = node.CostOfRank(rank);
            if (!_economy.Spend(CurrencyId.Essence, BigValue.FromDouble(cost), EconomyReason.EssenceNode))
                return false;
            _state.Progression.EssenceRanks[nodeId] = rank;
            purchase = new EssencePurchase(nodeId, rank, cost);
            Recompute();
            Purchased?.Invoke(purchase);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Essence spent on the tree so far.</summary>
        public long Spent()
        {
            long spent = 0;
            var nodes = EssenceCatalog.Nodes;
            for (int i = 0; i < nodes.Length; i++)
                spent += nodes[i].TotalCost(GetRank(nodes[i].Id));
            return spent;
        }

        /// <summary>
        /// Free redistribution (doc 02 Renouveau, section 1.5): every rank is refunded. Only during a Renewal's
        /// preparation; false (nothing changes) otherwise.
        /// </summary>
        public bool RefundAll()
        {
            if (!RedistributionOpen)
                return false;
            long spent = Spent();
            _state.Progression.EssenceRanks.Clear();
            if (spent > 0)
                _economy.Grant(CurrencyId.Essence, BigValue.FromDouble(spent), EconomyReason.Renewal);
            Recompute();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Summed value of an effect: ranks × value for the per-rank effects, the value for one-rank ones.</summary>
        public double Sum(EssenceEffect effect) => _effects[(int)effect];

        public int Ranks(EssenceEffect effect)
        {
            int ranks = 0;
            var nodes = EssenceCatalog.Nodes;
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].Effect == effect)
                    ranks += GetRank(nodes[i].Id);
            return ranks;
        }

        // ---- What each system reads ----

        /// <summary>Total production: (1 + 10 % per Soft Light rank) × 1.5 with Grand Refuge.</summary>
        public double ProductionMultiplier =>
            (1.0 + Sum(EssenceEffect.ProductionPercent)) * (Ranks(EssenceEffect.ProductionMultiplier) > 0 ? NodeValue(EssenceEffect.ProductionMultiplier) : 1.0);

        /// <summary>Basic and Focus egg prices: × 0.97 per Morning Eggs rank.</summary>
        public double EggPriceMultiplier => Math.Pow(1.0 - NodeValue(EssenceEffect.EggPrice), Ranks(EssenceEffect.EggPrice));

        /// <summary>Zones and biome gates: × 0.95 per Easy Spaces rank.</summary>
        public double ZoneGatePriceMultiplier => Math.Pow(1.0 - NodeValue(EssenceEffect.ZoneGatePrice), Ranks(EssenceEffect.ZoneGatePrice));

        /// <summary>Production level of each open biome right after a Renewal (5 per Fresh Start rank).</summary>
        public int RestartProductionLevel => (int)Math.Round(Sum(EssenceEffect.RestartProduction));

        public double ActivityLightMultiplier => 1.0 + Sum(EssenceEffect.ActivityLight);
        public double NewSpeciesPityBonus => Sum(EssenceEffect.NewSpeciesPity);
        public double VariantPityMultiplier => 1.0 + Sum(EssenceEffect.VariantPity);
        public double TraitChanceMultiplier => 1.0 + Sum(EssenceEffect.TraitChance);
        public double SpeciesMasteryXpMultiplier => 1.0 + Sum(EssenceEffect.SpeciesMasteryXp);
        public double BiomeMasteryXpMultiplier => 1.0 + Sum(EssenceEffect.BiomeMasteryXp);
        public double OfflineExtraSeconds => Sum(EssenceEffect.OfflineWindowHours) * 3600.0;
        public double OfflineFloorBonus => Sum(EssenceEffect.OfflineFloor);
        public int ExtraVisibleAnimals => (int)Math.Round(Sum(EssenceEffect.VisibleAnimals));

        private double NodeValue(EssenceEffect effect)
        {
            var nodes = EssenceCatalog.Nodes;
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].Effect == effect)
                    return nodes[i].Value;
            return 0.0;
        }

        private void Recompute()
        {
            Array.Clear(_effects, 0, _effects.Length);
            var nodes = EssenceCatalog.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                int rank = GetRank(nodes[i].Id);
                if (rank > 0)
                    _effects[(int)nodes[i].Effect] += nodes[i].Ranks == 1 ? nodes[i].Value : nodes[i].Value * rank;
            }
        }
    }
}
