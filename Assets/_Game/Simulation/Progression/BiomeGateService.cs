#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Conditions of a biome gate, as bits (analytics biome_gate_seen, the gate screen's ticks). Stable values.</summary>
    [Flags]
    public enum GateConditions : byte
    {
        None = 0,
        RefugeLevel = 1,
        Species = 2,
        Mastery = 4,
        Floor = 8,
        Light = 16,
        All = RefugeLevel | Species | Mastery | Floor | Light,
    }

    /// <summary>Everything the gate screen shows for one biome.</summary>
    public readonly struct GateStatus
    {
        public readonly ContentId Biome;
        public readonly ContentId Previous;
        public readonly bool Unlocked;

        /// <summary>False: the biome is not in this version yet ("Coming soon").</summary>
        public readonly bool ContentAvailable;

        public readonly GateConditions Met;
        public readonly int RefugeLevelNeeded;
        public readonly int SpeciesFound;
        public readonly int SpeciesNeeded;
        public readonly int MasteryLevel;
        public readonly int MasteryNeeded;
        public readonly GateFloorKind FloorKind;
        public readonly int FloorValue;

        /// <summary>Days (or minutes of play) still to wait for the floor; 0 when reached.</summary>
        public readonly int FloorLeft;

        public readonly BigValue Price;

        public GateStatus(ContentId biome, ContentId previous, bool unlocked, bool contentAvailable, GateConditions met, int refugeLevelNeeded,
            int speciesFound, int speciesNeeded, int masteryLevel, int masteryNeeded, GateFloorKind floorKind, int floorValue, int floorLeft,
            BigValue price)
        {
            Biome = biome;
            Previous = previous;
            Unlocked = unlocked;
            ContentAvailable = contentAvailable;
            Met = met;
            RefugeLevelNeeded = refugeLevelNeeded;
            SpeciesFound = speciesFound;
            SpeciesNeeded = speciesNeeded;
            MasteryLevel = masteryLevel;
            MasteryNeeded = masteryNeeded;
            FloorKind = floorKind;
            FloorValue = floorValue;
            FloorLeft = floorLeft;
            Price = price;
        }

        /// <summary>Everything but the Light is met: the gate glows and shows its price.</summary>
        public bool Ready => ContentAvailable && !Unlocked && (Met | GateConditions.Light) == GateConditions.All;

        public bool CanOpen => ContentAvailable && !Unlocked && Met == GateConditions.All;
    }

    public readonly struct BiomeOpening
    {
        public readonly ContentId Biome;
        public readonly BigValue Cost;
        public readonly long Gems;

        public BiomeOpening(ContentId biome, BigValue cost, long gems)
        {
            Biome = biome;
            Cost = cost;
            Gems = gems;
        }
    }

    /// <summary>
    /// Biome gates (doc 02 Améliorations, section 5): refuge level, species of the previous biome found, mastery of
    /// the previous biome, a floor in days since install (or play minutes for the Forest) and a price of 20 B. A biome
    /// whose content is not in this version shows "Coming soon" and opens normally once an update brings it.
    /// </summary>
    public sealed class BiomeGateService
    {
        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly ProgressionConfig _config;
        private readonly BiomeProgressStore _store;
        private readonly EconomyService _economy;
        private readonly RefugeLevelService _refuge;
        private readonly BiomeMasteryService _mastery;
        private readonly ZoneService _zones;
        private readonly IGameClock _clock;

        public BiomeGateService(PlayerState state, ContentRegistry content, ProgressionConfig config, BiomeProgressStore store,
            EconomyService economy, RefugeLevelService refuge, BiomeMasteryService mastery, ZoneService zones, IGameClock clock)
        {
            _state = state;
            _content = content;
            _config = config;
            _store = store;
            _economy = economy;
            _refuge = refuge;
            _mastery = mastery;
            _zones = zones;
            _clock = clock;
        }

        public event Action<BiomeOpening>? Opened;

        /// <summary>The first biome still closed, in opening order (false when every biome is open).</summary>
        public bool TryGetNextLocked(out ContentId biome)
        {
            var biomes = _config.Biomes;
            for (int i = 1; i < biomes.Length; i++)
            {
                if (_store.IsUnlocked(biomes[i].Id))
                    continue;
                biome = biomes[i].Id;
                return true;
            }
            biome = default;
            return false;
        }

        /// <summary>The biome has species in this version's content.</summary>
        public bool IsContentAvailable(ContentId biome)
        {
            var species = _content.Species;
            for (int i = 0; i < species.Count; i++)
                if (species[i].Biome == biome)
                    return true;
            return false;
        }

        public GateStatus GetStatus(ContentId biome)
        {
            int order = _config.BiomeOrder(biome);
            if (order <= 0)
                return new GateStatus(biome, default, true, true, GateConditions.All, 0, 0, 0, 0, 0, GateFloorKind.None, 0, 0, BigValue.Zero);
            var gate = _config.Biomes[order];
            var previous = _config.Biomes[order - 1].Id;
            bool unlocked = _store.IsUnlocked(biome);
            var met = GateConditions.None;

            if (_refuge.Level >= gate.GateRefugeLevel)
                met |= GateConditions.RefugeLevel;
            int found = SpeciesFound(previous);
            if (found >= gate.GateSpecies)
                met |= GateConditions.Species;
            int mastery = _mastery.GetLevel(previous);
            if (mastery >= gate.GateMastery)
                met |= GateConditions.Mastery;
            int floorLeft = FloorLeft(gate);
            if (floorLeft <= 0)
                met |= GateConditions.Floor;
            var price = ProgressionMath.Price(_config.GateCostMult * gate.EggBase, 1.0, 0);
            if (_economy.CanSpend(CurrencyId.Light, price))
                met |= GateConditions.Light;
            // A gate is only reachable from an open previous biome.
            if (!_store.IsUnlocked(previous))
                met &= ~(GateConditions.RefugeLevel | GateConditions.Species | GateConditions.Mastery);

            return new GateStatus(biome, previous, unlocked, IsContentAvailable(biome), met, gate.GateRefugeLevel, found, gate.GateSpecies,
                mastery, gate.GateMastery, gate.FloorKind, gate.FloorValue, Math.Max(0, floorLeft), price);
        }

        /// <summary>Opens the biome when every condition is met. False (nothing spent) otherwise.</summary>
        public bool TryOpen(ContentId biome, out BiomeOpening opening)
        {
            opening = default;
            var status = GetStatus(biome);
            if (!status.CanOpen)
                return false;
            if (!_economy.Spend(CurrencyId.Light, status.Price, EconomyReason.BiomeGate))
                return false;

            var progress = _store.Get(biome);
            progress.Unlocked = true;
            progress.ZonesOpened |= 1u; // the arrival zone
            _store.Set(progress);
            _zones.OpenPassage(status.Previous);
            _economy.Grant(CurrencyId.Gems, BigValue.FromDouble(_config.GateGems), EconomyReason.Reward);
            _economy.MarkProductionDirty();
            _refuge.Recompute();
            opening = new BiomeOpening(biome, status.Price, _config.GateGems);
            Opened?.Invoke(opening);
            return true;
        }

        private int SpeciesFound(ContentId biome)
        {
            int found = 0;
            var species = _content.Species;
            var discovered = _state.Collection.DiscoveredSpecies;
            for (int i = 0; i < species.Count; i++)
                if (species[i].Biome == biome && !species[i].IsSecret && discovered.Contains(species[i].Id))
                    found++;
            return found;
        }

        /// <summary>Days (or minutes of play) left before the floor; 0 or less when reached.</summary>
        private int FloorLeft(in BiomeEconomy gate)
        {
            switch (gate.FloorKind)
            {
                case GateFloorKind.PlayMinutes:
                    return gate.FloorValue - (int)Math.Floor(_state.Meta.TotalPlaySeconds / 60.0);
                case GateFloorKind.Day:
                    return gate.FloorValue - ProgressionMath.DayNumber(_state, _clock.UtcNow, _config.TimeZone);
                default:
                    return 0;
            }
        }
    }
}
