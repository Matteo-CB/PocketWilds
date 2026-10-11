#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Content
{
    /// <summary>Stable byte values. Stored in saves; never renumber.</summary>
    public enum RarityTier : byte
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Fabulous = 5,
    }

    /// <summary>Shared skeleton families. Stable byte values.</summary>
    public enum RigFamily : byte
    {
        SmallQuadruped = 0,
        MediumQuadruped = 1,
        HeavyQuadruped = 2,
        Bird = 3,
        Hopper = 4,
        SwimmerLow = 5,
        ClimberPrimate = 6,
        Penguin = 7,
    }

    /// <summary>
    /// Immutable runtime species data. Economy rules live here; visuals are referenced by key only.
    /// Palettes, variants, behavior and audio profiles are separate definitions linked by ID.
    /// </summary>
    public sealed class SpeciesDefinition : IContentDefinition
    {
        public SpeciesDefinition(
            ContentId id,
            string displayKey,
            RigFamily rigFamily,
            ContentId biome,
            double baseProduction,
            string visualKey,
            bool isSecret = false,
            bool isCompanion = false,
            byte hiddenSockets = 0)
        {
            HiddenSockets = hiddenSockets;
            // A companion is always secret too: every « species to find » rule (eggs, wishes, journal goals, gates) skips it.
            IsSecret = isSecret || isCompanion;
            IsCompanion = isCompanion;
            Id = id;
            DisplayKey = displayKey;
            RigFamily = rigFamily;
            Biome = biome;
            BaseProduction = baseProduction;
            VisualKey = visualKey;
        }

        public ContentId Id { get; }

        /// <summary>Secret species: hidden in the Codex until found, never targeted by the discovery pity.</summary>
        public bool IsSecret { get; }

        /// <summary>
        /// T47 companion (A-SP-101 and up): given by its entitlement, one per account. Never in an egg pool, never bred,
        /// out of the Codex completion (its own Companions tab), never asked by any requirement.
        /// </summary>
        public bool IsCompanion { get; }

        /// <summary>T47: accessory sockets this species hides (bits of <see cref="AccessorySocket"/>), e.g. the hamster's TailBase.</summary>
        public byte HiddenSockets { get; }

        public bool HidesSocket(AccessorySocket socket) => (HiddenSockets & (1 << (int)socket)) != 0;

        /// <summary>Localization key, e.g. "species.rabbit.name". Never the English text.</summary>
        public string DisplayKey { get; }

        public RigFamily RigFamily { get; }
        public ContentId Biome { get; }

        /// <summary>Light per second for one Common animal at mastery 0 and no bonuses.</summary>
        public double BaseProduction { get; }

        /// <summary>Addressable/prefab key of the species view. Presentation resolves it.</summary>
        public string VisualKey { get; }
    }
}
