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
        private readonly MasteryService? _speciesMastery;
        private readonly OfflineService? _offline;
        private readonly IGameClock _clock;

        // The calendar day of the egg price cap, read again once a minute (a time zone conversion).
        private int _capDay;
        private DateTimeOffset _capDayUntil = DateTimeOffset.MinValue;

        public ProgressionServices(PlayerState state, ContentRegistry content, EconomyService economy, HatchService? hatch, IGameClock clock,
            ISaveScheduler? save, ProgressionConfig? config = null, MasteryService? speciesMastery = null, OfflineService? offline = null,
            RenewalConfig? renewal = null)
        {
            _speciesMastery = speciesMastery;
            _offline = offline;
            _clock = clock;
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
            // T35C: the Essence tree and the Renewal (doc 02 Renouveau).
            Essence = new EssenceService(state, economy);
            Renewal = new RenewalService(state, content, economy, Essence, Store, Upgrades, Refuge, clock, Config, renewal);

            economy.SetModifiers(this);
            hatch?.SetModifiers(this);
            if (hatch != null)
            {
                hatch.GrowthOverride = Config.EggGrowth;
                hatch.Hatched += OnHatched;
            }
            Mastery.LeveledUp += OnMasteryLeveledUp;
            Zones.Opened += OnZoneOpened;
            Upgrades.Purchased += OnPurchase;
            Decorations.Purchased += OnPurchase;
            Treats.Purchased += OnPurchase;
            Essence.Changed += ApplyEssence;
            Essence.Purchased += OnPurchase;
            Renewal.Renewed += OnRenewed;
            ApplyEssence();
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
        public EssenceService Essence { get; }
        public RenewalService Renewal { get; }

        /// <summary>The biome the player starts in (zones and upgrades screens open on it by default).</summary>
        public ContentId FirstBiome => Config.Biomes[0].Id;

        /// <summary>Upgrades, zones and biome mastery, then the companion packs owned (T47 Meadow Pack: +5 % for the Meadow).</summary>
        public double BiomeProductionMultiplier(ContentId biome) =>
            Upgrades.ProductionMultiplier(biome) * Zones.ProductionMultiplier(biome) * Mastery.ProductionMultiplier(biome)
            * CompanionCatalog.BiomeMultiplier(_state.Entitlements, biome);

        /// <summary>Cozy Nests of the biome, then Morning Eggs of the Essence tree (Basic and Focus eggs).</summary>
        public double EggPriceMultiplier(ContentId biome) => Upgrades.EggPriceMultiplier(biome) * Essence.EggPriceMultiplier;

        public double VariantChanceBonus(ContentId biome) => Upgrades.VariantChanceBonus(biome);

        /// <summary>
        /// The egg price cap (T35C): 15 minutes of production during the first 7 calendar days, 45 after (doc 02 Formules,
        /// Remote Config econ.egg.cap_minutes_week1 and econ.egg.cap_minutes). Zero while nothing produces.
        /// </summary>
        public BigValue EggPriceCap()
        {
            var now = _clock.UtcNow;
            if (now >= _capDayUntil || now < _capDayUntil.AddMinutes(-2))
            {
                _capDay = ProgressionMath.DayNumber(_state, now, Config.TimeZone);
                _capDayUntil = now.AddMinutes(1);
            }
            double minutes = _capDay <= Config.EggCapFirstDays ? Config.EggCapMinutesFirstDays : Config.EggCapMinutes;
            var rate = _economy.GetLightPerSecond();
            return rate.IsZero || minutes <= 0.0 ? BigValue.Zero : BigValue.Multiply(rate, minutes * 60.0);
        }


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
            Essence.Changed -= ApplyEssence;
            Essence.Purchased -= OnPurchase;
            Renewal.Renewed -= OnRenewed;
        }

        /// <summary>
        /// The Essence tree's effects reach their systems once per change (doc 02 Renouveau, section 2): total production,
        /// zone and gate prices, biome and species mastery XP, the egg protections, the offline window and floor. Egg
        /// prices read <see cref="EggPriceMultiplier"/> when they are computed.
        /// </summary>
        private void ApplyEssence()
        {
            _economy.SetGlobalMultiplier(BigValue.FromDouble(Essence.ProductionMultiplier));
            Zones.PriceMultiplier = Essence.ZoneGatePriceMultiplier;
            Gates.PriceMultiplier = Essence.ZoneGatePriceMultiplier;
            Mastery.XpMultiplier = Essence.BiomeMasteryXpMultiplier;
            if (_speciesMastery != null)
                _speciesMastery.XpMultiplier = Essence.SpeciesMasteryXpMultiplier;
            if (_hatch != null)
            {
                _hatch.SpeciesPityBonusPerMiss = Essence.NewSpeciesPityBonus;
                _hatch.VariantPityMultiplier = Essence.VariantPityMultiplier;
            }
            if (_offline != null)
            {
                _offline.ExtraFullSeconds = Essence.OfflineExtraSeconds;
                _offline.FloorBonus = Essence.OfflineFloorBonus;
            }
        }

        private void OnRenewed(RenewalDone done)
        {
            Refuge.Recompute();
            _save?.RequestSave(SaveUrgency.Priority);
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
