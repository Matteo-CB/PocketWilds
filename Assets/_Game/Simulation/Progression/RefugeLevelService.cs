#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Functions the refuge level opens (doc 02 Améliorations, section 6.4). Stable values.</summary>
    public enum RefugeFeature : byte
    {
        Codex = 0,
        UpgradeButton = 1,
        ZoneSigns = 2,
        DailyRendezvous = 3,
        Shop = 4,
        DecorationMode = 5,
        Achievements = 6,
        Grooming = 7,
        HideAndSeek = 8,
        Expeditions = 9,
        WeeklyBook = 10,
        RefugePass = 11,
        Garden = 12,
        RareVisitors = 13,
        Breeding = 14,
        NutRain = 15,
        PawMemory = 16,
        Albums = 17,
        HatchThree = 18,
        RenewalGoal = 19,
        PhotoSafari = 20,
        TilePuzzle = 21,
        BurrowMaze = 22,
        HatchTen = 23,
    }

    /// <summary>The refuge score by source, as the Refuge level screen lists it.</summary>
    public struct RefugeScoreBreakdown
    {
        public long Species;
        public long Variants;
        public long Traits;
        public long SpeciesMastery;
        public long Biomes;
        public long Zones;
        public long BiomeMastery;

        public long Total => Species + Variants + Traits + SpeciesMastery + Biomes + Zones + BiomeMastery;
    }

    public readonly struct RefugeLevelUp
    {
        public readonly int From;
        public readonly int To;
        public readonly long Score;

        public RefugeLevelUp(int from, int to, long score)
        {
            From = from;
            To = to;
            Score = score;
        }
    }

    /// <summary>Rewards of the levels reached and not yet claimed, gathered on one card (section 6.3).</summary>
    public readonly struct LevelRewards
    {
        public readonly int FromLevel;
        public readonly int ToLevel;
        public readonly long Gems;
        public readonly int Essence;
        public readonly int Decorations;

        public LevelRewards(int fromLevel, int toLevel, long gems, int essence, int decorations)
        {
            FromLevel = fromLevel;
            ToLevel = toLevel;
            Gems = gems;
            Essence = essence;
            Decorations = decorations;
        }

        public bool Any => ToLevel > FromLevel;
    }

    /// <summary>
    /// Refuge level (doc 02 Améliorations, section 6). Score = discoveries + world: species 10, variants 3 (secret 15),
    /// first trait of a species 5, species mastery milestone 4, biome opened 50 (not the first), zone opened 12, biome
    /// mastery level 5. It is recomputed from the profile and never goes down; companions never count. Level L needs
    /// round(20 x (L - 1)^1.45), with no maximum. Rewards wait on a card until claimed.
    /// </summary>
    public sealed class RefugeLevelService
    {
        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly ProgressionConfig _config;
        private readonly BiomeProgressStore _store;
        private readonly EconomyService? _economy;
        private readonly IGameClock _clock;

        /// <summary>Trait bits already found, per species (the first of each counts once).</summary>
        private readonly Dictionary<ContentId, uint> _traitsBySpecies = new Dictionary<ContentId, uint>();

        private long _traitPoints;

        public RefugeLevelService(PlayerState state, ContentRegistry content, ProgressionConfig config, BiomeProgressStore store,
            EconomyService? economy, IGameClock clock)
        {
            _state = state;
            _content = content;
            _config = config;
            _store = store;
            _economy = economy;
            _clock = clock;
            var collection = state.Collection;
            for (int i = 0; i < collection.Count; i++)
                AddTraits(collection[i]);
            // A save older than the refuge level gets its level now, its rewards on the card.
            Recompute(false);
        }

        public event Action<RefugeLevelUp>? LeveledUp;

        public long Score => _state.Progression.Refuge.Score;
        public int Level => _state.Progression.Refuge.Level;

        public long ScoreForLevel(int level) => ProgressionMath.RefugeScoreForLevel(_config, level);

        /// <summary>Progress toward the next level, 0 to 1.</summary>
        public float LevelFraction
        {
            get
            {
                long from = ScoreForLevel(Level), to = ScoreForLevel(Level + 1);
                return to > from ? Math.Min(1f, (Score - from) / (float)(to - from)) : 0f;
            }
        }

        /// <summary>A new animal: its traits may be a first for its species.</summary>
        public void OnAnimalAdded(in AnimalInstance animal) => AddTraits(animal);

        /// <summary>Recomputes the score after any discovery, zone, biome or mastery change. Raises LeveledUp once.</summary>
        public void Recompute() => Recompute(true);

        private int _variantsCounted = -1;
        private long _variantPoints;

        public RefugeScoreBreakdown Breakdown()
        {
            var b = new RefugeScoreBreakdown();
            var collection = _state.Collection;
            b.Species = (long)collection.DiscoveredSpecies.Count * _config.PointsSpecies;
            // Variants only change by discovery: their points are counted again only when the set grew (every hatch reads this).
            if (collection.DiscoveredVariants.Count != _variantsCounted)
            {
                long points = 0;
                foreach (var id in collection.DiscoveredVariants)
                {
                    bool secret = _content.Variants.TryGet(id, out var variant) && variant.IsSecret;
                    points += secret ? _config.PointsSecretVariant : _config.PointsVariant;
                }
                _variantPoints = points;
                _variantsCounted = collection.DiscoveredVariants.Count;
            }
            b.Variants = _variantPoints;
            b.Traits = _traitPoints;
            int every = 5;
            foreach (var pair in _state.Mastery.Species)
                b.SpeciesMastery += (long)(pair.Value.Level / every) * _config.PointsSpeciesMasteryMilestone;
            var biomes = _config.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                var progress = _store.Get(biomes[i].Id);
                // The first biome is there from the start: only biomes opened at a gate count.
                if (progress.Unlocked && i > 0)
                    b.Biomes += _config.PointsBiome;
                b.Zones += (long)progress.Expansions * _config.PointsZone;
                b.BiomeMastery += (long)progress.MasteryLevel * _config.PointsBiomeMasteryLevel;
            }
            return b;
        }

        public bool IsFeatureUnlocked(RefugeFeature feature)
        {
            int level = Level;
            var collection = _state.Collection;
            int animals = collection.Count;
            int species = collection.DiscoveredSpecies.Count;
            // The calendar day only for the functions that wait for one (a time zone conversion, checked often by the HUD).
            int day = feature >= RefugeFeature.WeeklyBook && feature <= RefugeFeature.BurrowMaze
                ? ProgressionMath.DayNumber(_state, _clock.UtcNow, _config.TimeZone)
                : 0;
            switch (feature)
            {
                case RefugeFeature.Codex: return level >= 1 && animals >= 2;
                case RefugeFeature.UpgradeButton: return level >= 1 && animals >= 3;
                case RefugeFeature.ZoneSigns: return level >= 2;
                // T36 (doc 01 Contenu, section 0): level 2 and the welcome over (tutorial wish done or 5 animals).
                case RefugeFeature.DailyRendezvous: return level >= 2 && _state.IsOnboardingDone;
                case RefugeFeature.Shop: return level >= 2 && animals >= 5 && species >= 2;
                case RefugeFeature.DecorationMode: return level >= 2;
                case RefugeFeature.Achievements: return level >= 3;
                case RefugeFeature.Grooming: return level >= 3 && animals >= 5;
                case RefugeFeature.HideAndSeek: return level >= 4 && animals >= 8;
                case RefugeFeature.Expeditions: return level >= 5 && animals >= 8;
                case RefugeFeature.WeeklyBook: return level >= 6 && day >= 2;
                case RefugeFeature.RefugePass:
                case RefugeFeature.Garden:
                case RefugeFeature.RareVisitors: return level >= 8 && day >= 3;
                case RefugeFeature.Breeding: return level >= 10 && day >= 4;
                case RefugeFeature.NutRain: return level >= 10 && (day >= 4 || animals >= 15);
                case RefugeFeature.PawMemory: return level >= 11 && day >= 5;
                case RefugeFeature.Albums: return level >= 12 && day >= 5;
                case RefugeFeature.HatchThree: return level >= _config.MultiThreeLevel;
                case RefugeFeature.RenewalGoal: return level >= 15 && day >= 6;
                case RefugeFeature.PhotoSafari: return level >= 16 && day >= 6;
                case RefugeFeature.TilePuzzle: return level >= 18 && day >= 8;
                case RefugeFeature.BurrowMaze: return level >= 20 && day >= 10;
                case RefugeFeature.HatchTen: return level >= _config.MultiTenLevel && _state.World.RenewalCount > 0;
                default: return false;
            }
        }

        /// <summary>Rewards of the levels reached since the last claim.</summary>
        public LevelRewards PendingRewards()
        {
            var refuge = _state.Progression.Refuge;
            int from = refuge.LastRewardedLevel, to = refuge.Level;
            long gems = 0;
            int essence = 0, decorations = 0;
            for (int level = from + 1; level <= to; level++)
            {
                gems += level <= 50 ? _config.LevelGemsUpTo50 : _config.LevelGemsAfter50;
                if (level % 2 == 0 && level <= 2 * _config.Decorations.Length)
                    decorations++;
                if (_config.LevelEssenceEvery > 0 && level >= _config.LevelEssenceEvery && level % _config.LevelEssenceEvery == 0)
                    essence += _config.LevelEssence;
            }
            return new LevelRewards(from, Math.Max(from, to), gems, essence, decorations);
        }

        /// <summary>Claims the card: Gems now, Essence now after the first Renewal (kept for it before).</summary>
        public bool TryClaimRewards(out LevelRewards rewards)
        {
            rewards = PendingRewards();
            if (!rewards.Any)
                return false;
            var refuge = _state.Progression.Refuge;
            refuge.LastRewardedLevel = rewards.ToLevel;
            if (rewards.Gems > 0)
            {
                if (_economy != null)
                    _economy.Grant(CurrencyId.Gems, BigValue.FromDouble(rewards.Gems), EconomyReason.Reward);
                else
                    _state.Wallet.Gems += rewards.Gems;
            }
            if (rewards.Essence > 0)
            {
                if (_state.World.RenewalCount > 0)
                {
                    _state.Wallet.Essence += rewards.Essence;
                    _state.Progression.Renewal.EssenceEarned += rewards.Essence;
                }
                else
                    refuge.PendingEssenceFromLevels += rewards.Essence;
            }
            return true;
        }

        private void Recompute(bool notify)
        {
            var refuge = _state.Progression.Refuge;
            long score = Math.Max(refuge.Score, Breakdown().Total);
            refuge.Score = score;
            int before = Math.Max(1, refuge.Level);
            int level = Math.Max(before, ProgressionMath.RefugeLevelForScore(_config, score));
            refuge.Level = level;
            if (refuge.LastRewardedLevel < 1)
                refuge.LastRewardedLevel = 1;
            if (notify && level > before)
                LeveledUp?.Invoke(new RefugeLevelUp(before, level, score));
        }

        private void AddTraits(in AnimalInstance animal)
        {
            if (animal.TraitBits == 0)
                return;
            _traitsBySpecies.TryGetValue(animal.Species, out uint known);
            uint added = animal.TraitBits & ~known;
            if (added == 0)
                return;
            _traitsBySpecies[animal.Species] = known | added;
            _traitPoints += (long)PopCount(added) * _config.PointsTrait;
        }

        private static int PopCount(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }
    }
}
