#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Production aggregates per species (dense index from the ContentRegistry).
    /// Updated only when an animal is added, removed or its bond changes.
    /// The economy never iterates individual animals per frame.
    /// </summary>
    public sealed class SpeciesAggregateTable
    {
        private readonly int[] _count;

        /// <summary>Sum over the species' animals of RarityMultiplier x BondMultiplier.</summary>
        private readonly double[] _rarityBondSum;

        private readonly EconomyConfig _config;

        public SpeciesAggregateTable(int speciesCount, EconomyConfig config)
        {
            _count = new int[speciesCount];
            _rarityBondSum = new double[speciesCount];
            _config = config;
        }

        public int SpeciesCount => _count.Length;

        public int GetCount(int speciesIndex) => _count[speciesIndex];

        public double GetRarityBondSum(int speciesIndex) => _rarityBondSum[speciesIndex];

        public double AnimalFactor(RarityTier rarity, ushort bondPoints)
        {
            double rarityMultiplier = _config.RarityMultipliers[(int)rarity];
            double bondRatio = Math.Min(bondPoints, _config.BondPointsForMax) / (double)_config.BondPointsForMax;
            return rarityMultiplier * (1.0 + _config.BondMaxBonus * bondRatio);
        }

        public void Add(int speciesIndex, RarityTier rarity, ushort bondPoints)
        {
            _count[speciesIndex]++;
            _rarityBondSum[speciesIndex] += AnimalFactor(rarity, bondPoints);
        }

        public void Remove(int speciesIndex, RarityTier rarity, ushort bondPoints)
        {
            if (_count[speciesIndex] <= 0)
                throw new InvalidOperationException("Removing from an empty species aggregate.");

            _count[speciesIndex]--;
            if (_count[speciesIndex] == 0)
                _rarityBondSum[speciesIndex] = 0.0; // Kills accumulated float drift.
            else
                _rarityBondSum[speciesIndex] -= AnimalFactor(rarity, bondPoints);
        }

        public void ChangeBond(int speciesIndex, RarityTier rarity, ushort oldBond, ushort newBond)
        {
            _rarityBondSum[speciesIndex] += AnimalFactor(rarity, newBond) - AnimalFactor(rarity, oldBond);
        }

        /// <summary>Full rebuild after load or migration. O(animals), never per frame.</summary>
        public void Rebuild(CollectionState collection, ContentRegistry content, out int unknownSpeciesCount)
        {
            Array.Clear(_count, 0, _count.Length);
            Array.Clear(_rarityBondSum, 0, _rarityBondSum.Length);
            unknownSpeciesCount = 0;

            for (int i = 0; i < collection.Count; i++)
            {
                ref readonly var animal = ref collection[i];
                if (!content.Species.TryGetIndex(animal.Species, out int speciesIndex))
                {
                    // Retired or future species: kept in the save, simply not producing.
                    unknownSpeciesCount++;
                    continue;
                }
                Add(speciesIndex, animal.Rarity, animal.BondPoints);
            }
        }
    }
}
