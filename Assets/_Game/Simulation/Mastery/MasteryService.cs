#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public enum MasteryReason : byte
    {
        Duplicate = 0,
        NewVariant = 1,
        Discovery = 2,
        Birth = 3,

        /// <summary>T36: a wish fulfilled by an animal of the species (doc 08 econ.mastery_xp.wish).</summary>
        Wish = 4,

        /// <summary>T37: a rewarded activity game played with an animal of the species (econ.mastery_xp.activity_*star).</summary>
        Activity = 5,
    }

    /// <summary>Where a species stands: level, XP inside the level, XP needed for the next one.</summary>
    public readonly struct MasteryProgress
    {
        public readonly int Level;
        public readonly int Xp;
        public readonly int XpForNext;

        public MasteryProgress(int level, int xp, int xpForNext)
        {
            Level = level;
            Xp = xp;
            XpForNext = xpForNext;
        }

        public float Fraction => XpForNext > 0 ? Math.Min(1f, Xp / (float)XpForNext) : 0f;
    }

    /// <summary>Result of one acquisition. Immutable, carried by HatchResult to the presentation.</summary>
    public readonly struct MasteryGain
    {
        public readonly ContentId Species;
        public readonly MasteryReason Reason;
        public readonly int Xp;
        public readonly MasteryProgress Before;
        public readonly MasteryProgress After;

        /// <summary>Highest milestone level crossed by this gain (5, 10...), or 0.</summary>
        public readonly int Milestone;

        public MasteryGain(ContentId species, MasteryReason reason, int xp, MasteryProgress before, MasteryProgress after, int milestone)
        {
            Species = species;
            Reason = reason;
            Xp = xp;
            Before = before;
            After = after;
            Milestone = milestone;
        }

        public bool HasGain => Xp > 0;
        public int LevelsGained => After.Level - Before.Level;
        public bool LeveledUp => After.Level > Before.Level;
    }

    /// <summary>
    /// Species mastery (doc 02, Codebase Blueprint MasteryService): every acquisition gives species XP,
    /// duplicates included, so no hatch is ever wasted. XP to reach level L = round(20 x L^1.35); level 0
    /// means undiscovered. Each level adds a small production bonus (1 + 0.015 x level); milestones every
    /// 5 levels are reported for unlocks. Returns results only; never drives UI or VFX itself.
    /// </summary>
    public sealed class MasteryService
    {
        private readonly PlayerState _state;
        private readonly EconomyConfig _config;
        private readonly EconomyService? _economy;

        public MasteryService(PlayerState state, EconomyConfig config, EconomyService? economy)
        {
            _state = state;
            _config = config;
            _economy = economy;
        }

        public MasteryProgress Get(ContentId species)
        {
            _state.Mastery.Species.TryGetValue(species, out var mastery);
            return new MasteryProgress(mastery.Level, mastery.Xp, XpForNext(mastery.Level));
        }

        /// <summary>XP needed to go from <paramref name="level"/> to the next level.</summary>
        public int XpForNext(int level) => EconomyFormulas.MasteryXpForLevel(_config, level + 1);

        public int XpFor(MasteryReason reason)
        {
            switch (reason)
            {
                case MasteryReason.Discovery: return _config.MasteryXpDiscovery;
                case MasteryReason.NewVariant: return _config.MasteryXpNewVariant;
                case MasteryReason.Birth: return _config.MasteryXpBirth;
                default: return _config.MasteryXpDuplicate;
            }
        }

        /// <summary>A hatched animal: discovery, new variant or duplicate.</summary>
        public MasteryGain OnAnimalObtained(ContentId species, bool newSpecies, bool newVariant)
        {
            var reason = newSpecies ? MasteryReason.Discovery : (newVariant ? MasteryReason.NewVariant : MasteryReason.Duplicate);
            return Add(species, reason);
        }

        public MasteryGain Add(ContentId species, MasteryReason reason) => AddXp(species, reason, XpFor(reason));

        /// <summary>T35C: Fast Mastery (Essence tree) adds 25 % of species mastery XP; 1 without it.</summary>
        public double XpMultiplier { get; set; } = 1.0;

        public MasteryGain AddXp(ContentId species, MasteryReason reason, int xp)
        {
            if (XpMultiplier != 1.0 && xp > 0)
                xp = (int)Math.Round(xp * XpMultiplier, MidpointRounding.AwayFromZero);
            var before = Get(species);
            if (xp <= 0)
                return new MasteryGain(species, reason, 0, before, before, 0);

            int level = before.Level;
            long pool = (long)before.Xp + xp;
            int milestone = 0;
            int every = Math.Max(1, _config.MasteryMilestoneEvery);
            // Several levels at once are possible (big rewards); each step is O(1).
            while (true)
            {
                int need = XpForNext(level);
                if (need <= 0 || pool < need)
                    break;
                pool -= need;
                level++;
                if (level % every == 0)
                    milestone = level;
            }

            _state.Mastery.Species[species] = new SpeciesMastery { Level = level, Xp = (int)pool };
            if (level != before.Level)
                _economy?.MarkProductionDirty();
            var gain = new MasteryGain(species, reason, xp, before, new MasteryProgress(level, (int)pool, XpForNext(level)), milestone);
            if (level != before.Level)
                LeveledUp?.Invoke(gain);
            return gain;
        }

        /// <summary>T36: a species gained one level or more (wishes and the weekly journal count them).</summary>
        public event Action<MasteryGain>? LeveledUp;

        public static bool IsMilestone(int level, int every) => level > 0 && every > 0 && level % every == 0;
    }
}
