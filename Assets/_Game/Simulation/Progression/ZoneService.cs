#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public enum ZoneState : byte
    {
        Open = 0,

        /// <summary>The next zone to open, paid for and allowed: the sign shows its price.</summary>
        Available = 1,

        /// <summary>The next zone to open, allowed, but more Light is needed.</summary>
        TooExpensive = 2,

        /// <summary>The next zone to open, waiting for biome mastery.</summary>
        NeedsMastery = 3,

        /// <summary>A later zone: the one before it opens first.</summary>
        Later = 4,

        /// <summary>The passage: it opens with the next biome, never by purchase.</summary>
        Passage = 5,

        /// <summary>The biome itself is not open.</summary>
        BiomeLocked = 6,
    }

    public readonly struct ZoneOpening
    {
        public readonly ZoneDefinition Zone;
        public readonly BigValue Cost;

        /// <summary>The opening gift: Light worth a few minutes of production (granted).</summary>
        public readonly BigValue Gift;

        public ZoneOpening(ZoneDefinition zone, BigValue cost, BigValue gift)
        {
            Zone = zone;
            Cost = cost;
            Gift = gift;
        }
    }

    /// <summary>
    /// Zones of a biome (doc 02 Améliorations, section 4): the arrival zone is open with the biome, the zones to open
    /// come one by one in order for Light (25 B x 2.15^(k-1)) once biome mastery reaches 3k - 2, the passage opens with
    /// the next biome. Each zone opened multiplies the biome's production by 1.25 and adds 3 visible animals.
    /// </summary>
    public sealed class ZoneService
    {
        private readonly BiomeProgressStore _store;
        private readonly ProgressionConfig _config;
        private readonly EconomyService _economy;
        private readonly BiomeMasteryService _mastery;

        public ZoneService(BiomeProgressStore store, ProgressionConfig config, EconomyService economy, BiomeMasteryService mastery)
        {
            _store = store;
            _config = config;
            _economy = economy;
            _mastery = mastery;
        }

        /// <summary>A zone opened by purchase (analytics, the opening sequence in the world).</summary>
        public event Action<ZoneOpening>? Opened;

        /// <summary>A passage opened with the next biome.</summary>
        public event Action<ZoneDefinition>? PassageOpened;

        public bool TryGet(ContentId zoneId, out ZoneDefinition zone)
        {
            var zones = _config.Zones;
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].Id == zoneId)
                {
                    zone = zones[i];
                    return true;
                }
            }
            zone = default;
            return false;
        }

        /// <summary>The zone built on a chunk (by its streaming address), if any.</summary>
        public bool TryGetByChunk(string chunkAddress, out ZoneDefinition zone)
        {
            var zones = _config.Zones;
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].ChunkAddress == chunkAddress)
                {
                    zone = zones[i];
                    return true;
                }
            }
            zone = default;
            return false;
        }

        public bool IsOpen(in ZoneDefinition zone) => _store.Get(zone.Biome).IsZoneOpen(zone.Index);

        public int ExpansionsOpen(ContentId biome) => _store.Get(biome).Expansions;

        /// <summary>The first zone of the biome still to open by purchase, in order.</summary>
        public bool TryGetNext(ContentId biome, out ZoneDefinition zone)
        {
            var progress = _store.Get(biome);
            var zones = _config.Zones;
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].Biome != biome || zones[i].Role != ZoneRole.Expansion || progress.IsZoneOpen(zones[i].Index))
                    continue;
                zone = zones[i];
                return true;
            }
            zone = default;
            return false;
        }

        /// <summary>Price of zone k: 25 x B x 2.15^(k - 1).</summary>
        /// <summary>T35C: Easy Spaces (Essence tree) lowers every zone's price; 1 without it.</summary>
        public double PriceMultiplier { get; set; } = 1.0;

        public BigValue Price(in ZoneDefinition zone) =>
            zone.Role != ZoneRole.Expansion
                ? BigValue.Zero
                : ProgressionMath.Price(_config.ZoneCostMult * _config.EggBase(zone.Biome) * PriceMultiplier, _config.ZoneGrowth,
                    zone.ExpansionIndex - 1);

        /// <summary>Biome mastery zone k needs: 3k - 2.</summary>
        public int RequiredMastery(in ZoneDefinition zone) =>
            zone.Role != ZoneRole.Expansion ? 0 : Math.Max(0, _config.ZoneMasteryStep * zone.ExpansionIndex - 2);

        public ZoneState GetState(in ZoneDefinition zone)
        {
            var progress = _store.Get(zone.Biome);
            if (progress.IsZoneOpen(zone.Index))
                return ZoneState.Open;
            if (!progress.Unlocked)
                return ZoneState.BiomeLocked;
            if (zone.Role == ZoneRole.Passage)
                return ZoneState.Passage;
            if (zone.Role == ZoneRole.Arrival)
                return ZoneState.Available;
            if (!TryGetNext(zone.Biome, out var next) || next.Id != zone.Id)
                return ZoneState.Later;
            if (_mastery.GetLevel(zone.Biome) < RequiredMastery(zone))
                return ZoneState.NeedsMastery;
            return _economy.CanSpend(CurrencyId.Light, Price(zone)) ? ZoneState.Available : ZoneState.TooExpensive;
        }

        /// <summary>Opens the next zone of its biome for Light. False (nothing spent) when it cannot open now.</summary>
        public bool TryOpen(ContentId zoneId, out ZoneOpening opening)
        {
            opening = default;
            if (!TryGet(zoneId, out var zone) || zone.Role != ZoneRole.Expansion || GetState(zone) != ZoneState.Available)
                return false;
            var cost = Price(zone);
            // The gift is worth a few minutes of the production at the moment of the purchase.
            var gift = BigValue.Multiply(_economy.GetLightPerSecond(), _config.ZoneGiftMinutes * 60.0);
            if (!_economy.Spend(CurrencyId.Light, cost, EconomyReason.Expansion))
                return false;

            var progress = _store.Get(zone.Biome);
            progress.ZonesOpened |= 1u << zone.Index;
            progress.Expansions++;
            _store.Set(progress);
            _economy.MarkProductionDirty();
            _mastery.Add(zone.Biome, BiomeXpSource.Zone);
            _economy.Grant(CurrencyId.Light, gift, EconomyReason.Reward);
            opening = new ZoneOpening(zone, cost, gift);
            Opened?.Invoke(opening);
            return true;
        }

        /// <summary>The next biome opened: the passage of this biome toward it opens for free.</summary>
        public void OpenPassage(ContentId biome)
        {
            var zones = _config.Zones;
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].Biome != biome || zones[i].Role != ZoneRole.Passage)
                    continue;
                var progress = _store.Get(biome);
                if (progress.IsZoneOpen(zones[i].Index))
                    continue;
                progress.ZonesOpened |= 1u << zones[i].Index;
                _store.Set(progress);
                PassageOpened?.Invoke(zones[i]);
            }
        }

        /// <summary>Production multiplier from the zones opened: 1.25^zones.</summary>
        public double ProductionMultiplier(ContentId biome) => Math.Pow(_config.ZoneProductionMult, _store.Get(biome).Expansions);

        /// <summary>Visible animals the opened zones add (3 per zone).</summary>
        public int VisibleAnimalBonus(ContentId biome) => _config.ZoneVisibleAnimals * _store.Get(biome).Expansions;
    }
}
