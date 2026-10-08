#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// Every number of the Renewal (doc « 02 — Renouveau, Essence, maîtrises et liens », sections 1 and 7). Defaults are
    /// the doc's starting calibration; <see cref="TryOverride"/> applies a Remote Config value within its bounds.
    /// </summary>
    public sealed class RenewalConfig
    {
        public static RenewalConfig Default { get; } = new RenewalConfig();

        /// <summary>E_light = floor((LifetimeLight / Scale)^Exponent).</summary>
        public double Scale = 1e8;

        public double Exponent = 0.42;

        /// <summary>The button needs at least this gain.</summary>
        public long MinGain = 15;

        /// <summary>After the first Renewal, the gain must also reach this share of all Essence earned.</summary>
        public double MinRatio = 0.10;

        /// <summary>Essence offered with the first Renewal.</summary>
        public long FirstGift = 20;

        /// <summary>The Renewal shows as a goal from this refuge level and day on (doc 02 Améliorations, functions).</summary>
        public int VisibleRefugeLevel = 15;

        public int VisibleDay = 6;

        // ---- Completion bonus, counted since the previous Renewal ----
        public int BonusPerSpecies = 1;
        public int BonusPerBiome = 5;
        public int BonusPerAlbumPage = 1;
        public int BonusPerMastered = 2;

        /// <summary>Species mastery level that counts for the completion bonus.</summary>
        public int MasteredLevel = 30;

        private static readonly Dictionary<string, (double min, double max, Action<RenewalConfig, double> set)> Bounds =
            new Dictionary<string, (double, double, Action<RenewalConfig, double>)>
            {
                ["econ.renewal.scale"] = (1e6, 1e12, (c, v) => c.Scale = v),
                ["econ.renewal.exponent"] = (0.3, 0.6, (c, v) => c.Exponent = v),
                ["econ.renewal.min_gain"] = (1, 100, (c, v) => c.MinGain = (long)v),
                ["econ.renewal.min_ratio"] = (0, 0.5, (c, v) => c.MinRatio = v),
                ["econ.renewal.first_gift"] = (0, 100, (c, v) => c.FirstGift = (long)v),
            };

        public static IEnumerable<string> RemoteKeys => Bounds.Keys;

        public bool TryOverride(string key, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || !Bounds.TryGetValue(key, out var bound))
                return false;
            if (value < bound.min || value > bound.max)
                return false;
            bound.set(this, value);
            return true;
        }
    }

    /// <summary>What a Renewal would give now, part by part (the screen shows each).</summary>
    public readonly struct RenewalPreview
    {
        /// <summary>E_light minus what earlier Renewals already gave for the Light.</summary>
        public readonly long FromLight;

        public readonly long FromSpecies;
        public readonly long FromBiomes;
        public readonly long FromAlbums;
        public readonly long FromMastery;

        /// <summary>Essence put aside before the first Renewal (refuge levels and other sources).</summary>
        public readonly long Pending;

        /// <summary>The first Renewal's gift.</summary>
        public readonly long Gift;

        public RenewalPreview(long fromLight, long fromSpecies, long fromBiomes, long fromAlbums, long fromMastery, long pending, long gift)
        {
            FromLight = fromLight;
            FromSpecies = fromSpecies;
            FromBiomes = fromBiomes;
            FromAlbums = fromAlbums;
            FromMastery = fromMastery;
            Pending = pending;
            Gift = gift;
        }

        public long Completion => FromSpecies + FromBiomes + FromAlbums + FromMastery;

        /// <summary>The gain the button compares with its thresholds (the Light part and the completion bonus).</summary>
        public long Earned => FromLight + Completion;

        /// <summary>Everything the player receives.</summary>
        public long Total => Earned + Pending + Gift;
    }

    public readonly struct RenewalDone
    {
        public readonly int Count;
        public readonly RenewalPreview Gain;
        public readonly BigValue LightReset;

        public RenewalDone(int count, RenewalPreview gain, BigValue lightReset)
        {
            Count = count;
            Gain = gain;
            LightReset = lightReset;
        }
    }

    /// <summary>
    /// The Renewal (doc 02 Renouveau, section 1). It relaunches the economy on a stronger curve and never takes away an
    /// animal, a decoration, a zone, a biome, a discovery or an object: only the Light in the wallet, the Production and
    /// Cozy Nests levels and the egg counters start over. The gain is E_light(LifetimeLight) minus what earlier Renewals
    /// already gave for the Light, plus the completion bonus counted since the last one, so a second Renewal in a row only
    /// gives that bonus. Essence earned elsewhere before the first Renewal waits for it.
    /// </summary>
    public sealed class RenewalService
    {
        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly EconomyService _economy;
        private readonly EssenceService _essence;
        private readonly BiomeProgressStore _store;
        private readonly UpgradeService _upgrades;
        private readonly RefugeLevelService _refuge;
        private readonly IGameClock _clock;
        private readonly ProgressionConfig _progression;

        public RenewalService(PlayerState state, ContentRegistry content, EconomyService economy, EssenceService essence, BiomeProgressStore store,
            UpgradeService upgrades, RefugeLevelService refuge, IGameClock clock, ProgressionConfig progression, RenewalConfig? config = null)
        {
            _state = state;
            _content = content;
            _economy = economy;
            _essence = essence;
            _store = store;
            _upgrades = upgrades;
            _refuge = refuge;
            _clock = clock;
            _progression = progression;
            Config = config ?? RenewalConfig.Default;
        }

        public RenewalConfig Config { get; }

        public event Action<RenewalDone>? Renewed;

        public int Count => _state.World.RenewalCount;

        /// <summary>Shown as a goal from refuge level 15 and day 6 on.</summary>
        public bool IsVisible =>
            _refuge.Level >= Config.VisibleRefugeLevel
            && ProgressionMath.DayNumber(_state, _clock.UtcNow, _progression.TimeZone) >= Config.VisibleDay;

        /// <summary>E_light(LT) = floor((LT / Scale)^Exponent), the Essence the lifetime Light is worth.</summary>
        public long EssenceForLight(BigValue lifetimeLight)
        {
            if (lifetimeLight.IsZero || lifetimeLight.IsNegative)
                return 0;
            var ratio = BigValue.Divide(lifetimeLight, Config.Scale);
            double essence = Math.Floor(BigValue.PowPositive(ratio, Config.Exponent).ToDoubleClamped());
            return essence >= long.MaxValue ? long.MaxValue : (long)essence;
        }

        public RenewalPreview Preview()
        {
            var renewal = _state.Progression.Renewal;
            long fromLight = Math.Max(0L, EssenceForLight(_state.Wallet.LifetimeLight) - renewal.EssenceFromLight);
            long species = Math.Max(0, SpeciesFound() - renewal.SpeciesAtLast) * (long)Config.BonusPerSpecies;
            long biomes = Math.Max(0, _store.UnlockedCount() - renewal.BiomesAtLast) * (long)Config.BonusPerBiome;
            long albums = 0; // albums come with T44: no page can be complete yet
            long mastery = Math.Max(0, Mastered() - renewal.MasteredAtLast) * (long)Config.BonusPerMastered;
            long pending = _state.Progression.Refuge.PendingEssenceFromLevels;
            long gift = Count == 0 ? Config.FirstGift : 0;
            return new RenewalPreview(fromLight, species, biomes, albums, mastery, pending, gift);
        }

        /// <summary>
        /// The button: visible, a gain of at least 15 and, after the first Renewal, at least 10 % of all Essence earned.
        /// </summary>
        public bool CanRenew()
        {
            if (!IsVisible)
                return false;
            long gain = Preview().Earned;
            if (gain < Config.MinGain)
                return false;
            return Count == 0 || gain >= Config.MinRatio * _state.Progression.Renewal.EssenceEarned;
        }

        /// <summary>Opens the free redistribution of the tree for this Renewal (refund every rank, buy again).</summary>
        public bool BeginRedistribution()
        {
            if (!CanRenew())
                return false;
            _essence.RedistributionOpen = true;
            return true;
        }

        /// <summary>The player left the Renewal screen without renewing: the redistribution closes.</summary>
        public void Cancel() => _essence.RedistributionOpen = false;

        public bool TryRenew(out RenewalDone done)
        {
            done = default;
            if (!CanRenew())
            {
                _essence.RedistributionOpen = false;
                return false;
            }
            var gain = Preview();
            var renewal = _state.Progression.Renewal;
            var wallet = _state.Wallet;
            var light = wallet.Light;

            // Ça repart: the Light in the wallet, Production and Cozy Nests, the egg counters.
            if (!light.IsZero)
                _economy.Spend(CurrencyId.Light, light, EconomyReason.Renewal);
            _upgrades.OnRenewal();
            _state.World.EggPurchasesInCycle.Clear();
            int restart = _essence.RestartProductionLevel;
            if (restart > 0)
            {
                var biomes = _progression.Biomes;
                for (int i = 0; i < biomes.Length; i++)
                {
                    var progress = _store.Get(biomes[i].Id);
                    if (!progress.Unlocked)
                        continue;
                    progress.ProductionLevel = restart;
                    _store.Set(progress);
                }
            }

            // The memory of this Renewal, then the Essence.
            renewal.EssenceFromLight = Math.Max(renewal.EssenceFromLight, EssenceForLight(wallet.LifetimeLight));
            renewal.SpeciesAtLast = SpeciesFound();
            renewal.BiomesAtLast = _store.UnlockedCount();
            renewal.MasteredAtLast = Mastered();
            renewal.EssenceEarned += gain.Total;
            _state.Progression.Refuge.PendingEssenceFromLevels = 0;
            _state.World.RenewalCount++;
            if (gain.Total > 0)
                _economy.Grant(CurrencyId.Essence, BigValue.FromDouble(gain.Total), EconomyReason.Renewal);
            _essence.RedistributionOpen = false;
            _economy.MarkProductionDirty();

            done = new RenewalDone(_state.World.RenewalCount, gain, light);
            Renewed?.Invoke(done);
            return true;
        }

        /// <summary>
        /// Essence from a source other than a Renewal (a Codex of a biome complete, an album, an achievement): kept for the
        /// first Renewal before it, given at once after.
        /// </summary>
        public void GrantEssence(long amount)
        {
            if (amount <= 0)
                return;
            if (Count == 0)
            {
                _state.Progression.Refuge.PendingEssenceFromLevels =
                    (int)Math.Min(int.MaxValue, _state.Progression.Refuge.PendingEssenceFromLevels + amount);
                return;
            }
            _state.Progression.Renewal.EssenceEarned += amount;
            _economy.Grant(CurrencyId.Essence, BigValue.FromDouble(amount), EconomyReason.Reward);
        }

        private int SpeciesFound()
        {
            int found = 0;
            var discovered = _state.Collection.DiscoveredSpecies;
            var species = _content.Species;
            for (int i = 0; i < species.Count; i++)
                if (!species[i].IsSecret && discovered.Contains(species[i].Id))
                    found++;
            return found;
        }

        private int Mastered()
        {
            int mastered = 0;
            foreach (var pair in _state.Mastery.Species)
                if (pair.Value.Level >= Config.MasteredLevel)
                    mastered++;
            return mastered;
        }
    }
}
