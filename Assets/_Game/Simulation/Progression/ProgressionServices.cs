#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Everything bought with Light besides eggs, and the levels that follow the player (doc 02 Améliorations): Light
    /// upgrades, zones, biome mastery, refuge level, biome gates, Light decorations and the treat stand. Built once by
    /// the bootstrap; it plugs its multipliers into the economy and the hatch, and listens to hatches for XP and score.
    /// </summary>
    public sealed class ProgressionServices : IProductionModifiers, IHatchModifiers, IDisposable
    {
        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly EconomyService _economy;
        private readonly HatchService? _hatch;
        private readonly ISaveScheduler? _save;

        public ProgressionServices(PlayerState state, ContentRegistry content, EconomyService economy, HatchService? hatch, IGameClock clock,
            ISaveScheduler? save, ProgressionConfig? config = null)
        {
            _state = state;
            _content = content;
            _economy = economy;
            _hatch = hatch;
            _save = save;
            Config = config ?? ProgressionConfig.Default;
            Store = new BiomeProgressStore(state, Config);
            Mastery = new BiomeMasteryService(Store, Config, economy);
            Upgrades = new UpgradeService(Store, Config, economy, Mastery);
            Zones = new ZoneService(Store, Config, economy, Mastery);
            Refuge = new RefugeLevelService(state, content, Config, Store, economy, clock);
            Gates = new BiomeGateService(state, content, Config, Store, economy, Refuge, Mastery, Zones, clock);
            Decorations = new DecorationShopService(state, Config, Store, economy, Refuge);
            Treats = new TreatStandService(state, content, Config, Store, economy, clock);

            economy.SetModifiers(this);
            hatch?.SetModifiers(this);
            if (hatch != null)
                hatch.Hatched += OnHatched;
            Mastery.LeveledUp += OnMasteryLeveledUp;
            Zones.Opened += OnZoneOpened;
            Upgrades.Purchased += OnPurchase;
            Decorations.Purchased += OnPurchase;
            Treats.Purchased += OnPurchase;
        }

        public ProgressionConfig Config { get; }
        public BiomeProgressStore Store { get; }
        public BiomeMasteryService Mastery { get; }
        public UpgradeService Upgrades { get; }
        public ZoneService Zones { get; }
        public RefugeLevelService Refuge { get; }
        public BiomeGateService Gates { get; }
        public DecorationShopService Decorations { get; }
        public TreatStandService Treats { get; }

        /// <summary>The biome the player starts in (zones and upgrades screens open on it by default).</summary>
        public ContentId FirstBiome => Config.Biomes[0].Id;

        public double BiomeProductionMultiplier(ContentId biome) =>
            Upgrades.ProductionMultiplier(biome) * Zones.ProductionMultiplier(biome) * Mastery.ProductionMultiplier(biome);

        public double EggPriceMultiplier(ContentId biome) => Upgrades.EggPriceMultiplier(biome);

        public double VariantChanceBonus(ContentId biome) => Upgrades.VariantChanceBonus(biome);

        /// <summary>Renewal (doc 02 Renouveau): Production and Cozy Nests start over; zones, biomes, masteries and score stay.</summary>
        public void OnRenewal() => Upgrades.OnRenewal();

        public void Dispose()
        {
            _economy.SetModifiers(null);
            if (_hatch != null)
            {
                _hatch.SetModifiers(null);
                _hatch.Hatched -= OnHatched;
            }
            Mastery.LeveledUp -= OnMasteryLeveledUp;
            Zones.Opened -= OnZoneOpened;
            Upgrades.Purchased -= OnPurchase;
            Decorations.Purchased -= OnPurchase;
            Treats.Purchased -= OnPurchase;
        }

        /// <summary>
        /// Biome mastery XP of a hatch (section 7.1): a new species 40 and its first variant 12, a new variant of a known
        /// species 12 + 2, any other hatch 2, and 20 when the species crossed a mastery milestone. Then the score.
        /// </summary>
        private void OnHatched(HatchResult result)
        {
            var animal = result.Animal;
            Refuge.OnAnimalAdded(animal);
            if (_content.Species.TryGet(animal.Species, out var species))
            {
                var biome = species.Biome;
                long xp = 0;
                if (result.NewSpecies)
                    xp += Mastery.XpFor(BiomeXpSource.Species) + Mastery.XpFor(BiomeXpSource.Variant);
                else
                    xp += Mastery.XpFor(BiomeXpSource.KnownHatch) + (result.NewVariant ? Mastery.XpFor(BiomeXpSource.Variant) : 0);
                if (result.Mastery.Milestone > 0)
                    xp += Mastery.XpFor(BiomeXpSource.SpeciesMilestone);
                Mastery.AddXp(biome, xp);
            }
            Refuge.Recompute();
        }

        private void OnMasteryLeveledUp(BiomeMasteryGain gain)
        {
            Refuge.Recompute();
            _save?.RequestSave(SaveUrgency.Normal);
        }

        private void OnZoneOpened(ZoneOpening opening)
        {
            Refuge.Recompute();
            _save?.RequestSave(SaveUrgency.Priority);
        }

        private void OnPurchase<T>(T purchase) => _save?.RequestSave(SaveUrgency.Normal);
    }
}
