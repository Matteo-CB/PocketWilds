#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public readonly struct TreatPurchase
    {
        public readonly ContentId Treat;
        public readonly int Slot;
        public readonly BigValue Cost;

        public TreatPurchase(ContentId treat, int slot, BigValue cost)
        {
            Treat = treat;
            Slot = slot;
            Cost = cost;
        }
    }

    /// <summary>
    /// The treat stand of the shop's Light tab (doc 02 Améliorations, section 9; doc 01 Contenu, section 10.3): three
    /// treats a day, each worth two minutes of the current production, refilled at local midnight.
    /// <list type="bullet">
    /// <item>Slot 1: the treat of the most recent biome.</item>
    /// <item>Slot 2: the treat of another open biome, drawn with the day as seed (Clover Crunch with a single biome).</item>
    /// <item>Slot 3: the treat of the biome the camera shows, or the treat the latest favorite's family prefers when
    /// that is slot 1's.</item>
    /// </list>
    /// </summary>
    public sealed class TreatStandService
    {
        public const int SlotCount = 3;

        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly ProgressionConfig _config;
        private readonly BiomeProgressStore _store;
        private readonly EconomyService _economy;
        private readonly IGameClock _clock;

        public TreatStandService(PlayerState state, ContentRegistry content, ProgressionConfig config, BiomeProgressStore store,
            EconomyService economy, IGameClock clock)
        {
            _state = state;
            _content = content;
            _config = config;
            _store = store;
            _economy = economy;
            _clock = clock;
        }

        public event Action<TreatPurchase>? Purchased;

        /// <summary>Fills the stand for today when the local day changed. cameraBiome: the biome on screen (slot 3).</summary>
        public void Refresh(ContentId cameraBiome)
        {
            var stand = _state.Progression.TreatStand;
            var now = _clock.UtcNow > _state.Offline.HighWaterUtc ? _clock.UtcNow : _state.Offline.HighWaterUtc;
            int today = ProgressionMath.LocalDayKey(now, _config.TimeZone);
            if (stand.DayKey == today)
                return;
            stand.DayKey = today;
            stand.BoughtMask = 0;

            var latest = _store.MostAdvancedUnlocked();
            stand.Slot0 = TreatOf(latest);
            stand.Slot1 = OtherBiomeTreat(latest, today);
            var camera = _store.IsUnlocked(cameraBiome) ? cameraBiome : latest;
            var third = TreatOf(camera);
            stand.Slot2 = third == stand.Slot0 ? FavoriteFamilyTreat(third) : third;
        }

        public ContentId GetSlot(int slot)
        {
            var stand = _state.Progression.TreatStand;
            return slot == 0 ? stand.Slot0 : slot == 1 ? stand.Slot1 : slot == 2 ? stand.Slot2 : default;
        }

        public bool IsBought(int slot) => (_state.Progression.TreatStand.BoughtMask & (1 << slot)) != 0;

        /// <summary>Slots still for sale today (the daily count may be below three by Remote Config).</summary>
        public int Left
        {
            get
            {
                int left = 0;
                for (int i = 0; i < Math.Min(SlotCount, _config.TreatsDaily); i++)
                    if (!IsBought(i))
                        left++;
                return left;
            }
        }

        /// <summary>Two minutes of the current production, at least 1 Light.</summary>
        public BigValue Price()
        {
            var price = BigValue.Multiply(_economy.GetLightPerSecond(), _config.TreatMinutes * 60.0);
            if (price.Exponent10 < 15)
                price = BigValue.FromDouble(Math.Max(1.0, Math.Round(price.ToDoubleClamped(), MidpointRounding.AwayFromZero)));
            return price;
        }

        public int Owned(ContentId treat) => _state.Progression.Treats.TryGetValue(treat, out int count) ? count : 0;

        public bool TryBuy(int slot, out TreatPurchase purchase)
        {
            purchase = default;
            if (slot < 0 || slot >= Math.Min(SlotCount, _config.TreatsDaily) || IsBought(slot))
                return false;
            var treat = GetSlot(slot);
            if (!treat.IsValid)
                return false;
            var cost = Price();
            if (!_economy.Spend(CurrencyId.Light, cost, EconomyReason.Treat))
                return false;
            _state.Progression.TreatStand.BoughtMask |= 1 << slot;
            _state.Progression.Treats[treat] = Owned(treat) + 1;
            purchase = new TreatPurchase(treat, slot, cost);
            Purchased?.Invoke(purchase);
            return true;
        }

        private ContentId TreatOf(ContentId biome) => _config.TryGetBiome(biome, out var b) ? b.Treat : _config.Biomes[0].Treat;

        private ContentId OtherBiomeTreat(ContentId latest, int dayKey)
        {
            var biomes = _config.Biomes;
            int others = 0;
            for (int i = 0; i < biomes.Length; i++)
                if (biomes[i].Id != latest && _store.IsUnlocked(biomes[i].Id))
                    others++;
            if (others == 0)
                return biomes[0].Treat;
            int pick = (int)(SeedHash.Mix((uint)dayKey, 0x77EA0001u) % (uint)others);
            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i].Id == latest || !_store.IsUnlocked(biomes[i].Id))
                    continue;
                if (pick-- == 0)
                    return biomes[i].Treat;
            }
            return biomes[0].Treat;
        }

        /// <summary>The treat preferred by the latest favorite animal's family, among open biomes; else <paramref name="fallback"/>.</summary>
        private ContentId FavoriteFamilyTreat(ContentId fallback)
        {
            var collection = _state.Collection;
            uint newest = 0;
            ContentId species = default;
            for (int i = 0; i < collection.Count; i++)
            {
                ref readonly var animal = ref collection[i];
                if (animal.IsFavorite && animal.InstanceId >= newest)
                {
                    newest = animal.InstanceId;
                    species = animal.Species;
                }
            }
            if (!species.IsValid || !_content.Species.TryGet(species, out var definition))
                return fallback;
            var biomes = _config.Biomes;
            var treats = _config.Treats;
            for (int i = 0; i < biomes.Length && i < treats.Length; i++)
                if (_store.IsUnlocked(biomes[i].Id) && treats[i].PreferredFamily == definition.RigFamily)
                    return treats[i].Id;
            return fallback;
        }
    }
}
