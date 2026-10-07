#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Reads and writes one biome's entry of <see cref="WorldProgressState.Biomes"/> by id (a short list: 13 biomes at
    /// most, a linear search, no allocation once the entry exists). The first biome is always open with its arrival
    /// zone: a new profile and an old save get it the same way.
    /// </summary>
    public sealed class BiomeProgressStore
    {
        private readonly PlayerState _state;
        private readonly ProgressionConfig _config;

        public BiomeProgressStore(PlayerState state, ProgressionConfig config)
        {
            _state = state;
            _config = config;
            var first = config.Biomes[0].Id;
            var progress = Get(first);
            if (!progress.Unlocked || !progress.IsZoneOpen(0))
            {
                progress.Unlocked = true;
                progress.ZonesOpened |= 1u;
                Set(progress);
            }
        }

        /// <summary>The biome's progress (a default, locked entry when it has none yet).</summary>
        public BiomeProgress Get(ContentId biome)
        {
            var biomes = _state.World.Biomes;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i].Biome == biome)
                    return biomes[i];
            return new BiomeProgress { Biome = biome };
        }

        public void Set(BiomeProgress progress)
        {
            var biomes = _state.World.Biomes;
            for (int i = 0; i < biomes.Count; i++)
            {
                if (biomes[i].Biome == progress.Biome)
                {
                    biomes[i] = progress;
                    return;
                }
            }
            biomes.Add(progress);
        }

        public bool IsUnlocked(ContentId biome) => Get(biome).Unlocked;

        /// <summary>The most advanced open biome (its B prices Light decorations).</summary>
        public ContentId MostAdvancedUnlocked()
        {
            var biomes = _config.Biomes;
            for (int i = biomes.Length - 1; i > 0; i--)
                if (IsUnlocked(biomes[i].Id))
                    return biomes[i].Id;
            return biomes[0].Id;
        }

        public int UnlockedCount()
        {
            int count = 0;
            var biomes = _config.Biomes;
            for (int i = 0; i < biomes.Length; i++)
                if (IsUnlocked(biomes[i].Id))
                    count++;
            return count;
        }
    }
}
