#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Where biome mastery XP comes from (doc 02 Améliorations, section 7.1). Stable values.</summary>
    public enum BiomeXpSource : byte
    {
        Species = 0,
        Variant = 1,
        KnownHatch = 2,
        Birth = 3,
        Activity = 4,
        Zone = 5,
        Decoration = 6,
        Sticker = 7,
        SpeciesMilestone = 8,
        Wish = 9,
        Expedition = 10,
    }

    public readonly struct BiomeMasteryProgress
    {
        public readonly int Level;
        public readonly long XpInLevel;
        public readonly long XpForNext;

        public BiomeMasteryProgress(int level, long xpInLevel, long xpForNext)
        {
            Level = level;
            XpInLevel = xpInLevel;
            XpForNext = xpForNext;
        }

        public float Fraction => XpForNext > 0 ? Math.Min(1f, XpInLevel / (float)XpForNext) : 0f;
    }

    public readonly struct BiomeMasteryGain
    {
        public readonly ContentId Biome;
        public readonly long Xp;
        public readonly int LevelBefore;
        public readonly int LevelAfter;

        public BiomeMasteryGain(ContentId biome, long xp, int levelBefore, int levelAfter)
        {
            Biome = biome;
            Xp = xp;
            LevelBefore = levelBefore;
            LevelAfter = levelAfter;
        }

        public bool LeveledUp => LevelAfter > LevelBefore;
    }

    /// <summary>
    /// Biome mastery (doc 02 Améliorations, section 7): XP from discoveries, hatches, zones and later activities;
    /// XP for level L = round(40 x L^1.3), no maximum, never reset. Each level adds 2 % production to the biome's animals
    /// and opens the right to its zones (3k - 2), Cozy Nests (2) and Curious Eye (5).
    /// </summary>
    public sealed class BiomeMasteryService
    {
        private readonly BiomeProgressStore _store;
        private readonly ProgressionConfig _config;
        private readonly EconomyService? _economy;

        public BiomeMasteryService(BiomeProgressStore store, ProgressionConfig config, EconomyService? economy)
        {
            _store = store;
            _config = config;
            _economy = economy;
        }

        /// <summary>A biome reached a new mastery level (once per gain, levels may be several).</summary>
        public event Action<BiomeMasteryGain>? LeveledUp;

        public int GetLevel(ContentId biome) => _store.Get(biome).MasteryLevel;

        public BiomeMasteryProgress Get(ContentId biome)
        {
            var progress = _store.Get(biome);
            int level = ProgressionMath.MasteryLevelForXp(_config, progress.MasteryXp, out long inLevel);
            return new BiomeMasteryProgress(level, inLevel, ProgressionMath.MasteryXpForLevel(_config, level + 1));
        }

        public int XpFor(BiomeXpSource source)
        {
            switch (source)
            {
                case BiomeXpSource.Species: return _config.XpSpecies;
                case BiomeXpSource.Variant: return _config.XpVariant;
                case BiomeXpSource.KnownHatch: return _config.XpKnownHatch;
                case BiomeXpSource.Birth: return _config.XpBirth;
                case BiomeXpSource.Activity: return _config.XpActivity;
                case BiomeXpSource.Zone: return _config.XpZone;
                case BiomeXpSource.Decoration: return _config.XpDecoration;
                case BiomeXpSource.Sticker: return _config.XpSticker;
                case BiomeXpSource.SpeciesMilestone: return _config.XpSpeciesMilestone;
                case BiomeXpSource.Wish: return _config.XpWish;
                case BiomeXpSource.Expedition: return _config.XpExpedition;
                default: return 0;
            }
        }

        /// <summary>Production multiplier of the biome's animals: 1 + 0.02 x level.</summary>
        public double ProductionMultiplier(ContentId biome) => 1.0 + _config.MasteryProductionStep * GetLevel(biome);

        public BiomeMasteryGain Add(ContentId biome, BiomeXpSource source, int count = 1) => AddXp(biome, (long)XpFor(source) * count);

        /// <summary>T35C: Roots (Essence tree) adds 10 % of biome mastery XP per rank; 1 without it.</summary>
        public double XpMultiplier { get; set; } = 1.0;

        public BiomeMasteryGain AddXp(ContentId biome, long xp)
        {
            if (XpMultiplier != 1.0 && xp > 0)
                xp = (long)Math.Round(xp * XpMultiplier, MidpointRounding.AwayFromZero);
            var progress = _store.Get(biome);
            int before = progress.MasteryLevel;
            if (xp <= 0)
                return new BiomeMasteryGain(biome, 0, before, before);
            progress.MasteryXp += xp;
            progress.MasteryLevel = ProgressionMath.MasteryLevelForXp(_config, progress.MasteryXp, out _);
            _store.Set(progress);
            var gain = new BiomeMasteryGain(biome, xp, before, progress.MasteryLevel);
            if (gain.LeveledUp)
            {
                _economy?.MarkProductionDirty();
                LeveledUp?.Invoke(gain);
            }
            return gain;
        }
    }
}
