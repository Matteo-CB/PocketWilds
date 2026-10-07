#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public readonly struct DecorationPurchase
    {
        public readonly ContentId Decoration;

        /// <summary>1 for the first copy.</summary>
        public readonly int Copy;

        public readonly BigValue Cost;

        public DecorationPurchase(ContentId decoration, int copy, BigValue cost)
        {
            Decoration = decoration;
            Copy = copy;
            Cost = cost;
        }
    }

    /// <summary>
    /// Common decorations bought in Light (doc 02 Améliorations, section 8): unlocked one every two refuge levels from
    /// 2 to 52, then up to 10 copies each. First copy 30 x B of the most advanced open biome, each next copy x 1.6, so a
    /// decoration always costs about 1 to 5 minutes of production. Placing them is the decoration mode's job.
    /// </summary>
    public sealed class DecorationShopService
    {
        private readonly PlayerState _state;
        private readonly ProgressionConfig _config;
        private readonly BiomeProgressStore _store;
        private readonly EconomyService _economy;
        private readonly RefugeLevelService _refuge;

        public DecorationShopService(PlayerState state, ProgressionConfig config, BiomeProgressStore store, EconomyService economy,
            RefugeLevelService refuge)
        {
            _state = state;
            _config = config;
            _store = store;
            _economy = economy;
            _refuge = refuge;
        }

        public event Action<DecorationPurchase>? Purchased;

        public bool TryGet(ContentId id, out CommonDecoration decoration)
        {
            var list = _config.Decorations;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Id == id)
                {
                    decoration = list[i];
                    return true;
                }
            }
            decoration = default;
            return false;
        }

        public bool IsUnlocked(in CommonDecoration decoration) => _refuge.Level >= decoration.UnlockLevel;

        public int Copies(ContentId id) => _state.Progression.DecorationCopies.TryGetValue(id, out int copies) ? copies : 0;

        /// <summary>Price of the next copy (zero at the maximum).</summary>
        public BigValue NextPrice(ContentId id)
        {
            int copies = Copies(id);
            if (copies >= _config.DecorationMaxCopies)
                return BigValue.Zero;
            double b = _config.EggBase(_store.MostAdvancedUnlocked());
            return ProgressionMath.Price(_config.DecorationLightMult * b, _config.DecorationCopyGrowth, copies);
        }

        public bool CanBuy(ContentId id) =>
            TryGet(id, out var decoration) && IsUnlocked(decoration) && Copies(id) < _config.DecorationMaxCopies
            && _economy.CanSpend(CurrencyId.Light, NextPrice(id));

        public bool TryBuy(ContentId id, out DecorationPurchase purchase)
        {
            purchase = default;
            if (!CanBuy(id))
                return false;
            var cost = NextPrice(id);
            if (!_economy.Spend(CurrencyId.Light, cost, EconomyReason.Decoration))
                return false;
            int copy = Copies(id) + 1;
            _state.Progression.DecorationCopies[id] = copy;
            purchase = new DecorationPurchase(id, copy, cost);
            Purchased?.Invoke(purchase);
            return true;
        }
    }
}
