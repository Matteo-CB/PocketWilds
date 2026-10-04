#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    [Flags]
    public enum AnimalFlags : byte
    {
        None = 0,
        Favorite = 1 << 0,
        HasCustomName = 1 << 1,
    }

    /// <summary>
    /// Persistent identity of one animal. 28 bytes, no references. Colors, pattern and small
    /// visual details are rebuilt from Seed + species tables, so they are not stored.
    /// </summary>
    public struct AnimalInstance
    {
        public uint InstanceId;
        public ContentId Species;
        public ContentId Variant;
        public uint Seed;

        /// <summary>Unix seconds (UTC). uint covers dates until 2106.</summary>
        public uint CreatedUtcSeconds;

        /// <summary>Extraordinary traits (Tiny, Giant, Shiny, Golden...) as bits.</summary>
        public uint TraitBits;

        public ushort BondPoints;
        public RarityTier Rarity;
        public AnimalFlags Flags;

        public bool IsFavorite => (Flags & AnimalFlags.Favorite) != 0;
    }
}
