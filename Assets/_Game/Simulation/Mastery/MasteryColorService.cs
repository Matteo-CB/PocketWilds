#nullable enable
using System;
using System.Collections.Generic;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>A mastery color that just joined the collection.</summary>
    public readonly struct MasteryColorGrant
    {
        public readonly ContentId Species;
        public readonly ContentId Variant;
        public readonly uint InstanceId;
        public readonly int Level;

        public MasteryColorGrant(ContentId species, ContentId variant, uint instanceId, int level)
        {
            Species = species;
            Variant = variant;
            InstanceId = instanceId;
            Level = level;
        }
    }

    public delegate void MasteryColorHandler(in MasteryColorGrant grant);

    /// <summary>
    /// T46 « Couleurs de maîtrise » (doc 02 Renouveau 3.2, « Variante de maîtrise 1/2/3 »). Reaching mastery 10, 20 and 30 of a
    /// species gives one animal of the species' variant with that <see cref="VariantDefinition.MasteryLevel"/>, once: it joins the
    /// collection like a visitor who stays (discoveries, mastery, refuge score, achievements) and <see cref="Granted"/> tells the
    /// refuge. A grant can level the species up again: that gain is queued, never handled inside the first. These colors are
    /// secret and never in an egg. <see cref="CatchUp"/> grants what an older save already earned, without an event. Reads and
    /// the grants allocate nothing once the service exists.
    /// </summary>
    public sealed class MasteryColorService : IDisposable
    {
        public static readonly int[] Thresholds = { 10, 20, 30 };

        private readonly PlayerState _player;
        private readonly ContentRegistry _content;
        private readonly IGameClock _clock;
        private readonly Queue<MasteryGain> _pending = new Queue<MasteryGain>(8);
        private readonly Action<MasteryGain> _onLeveledUp;
        private MasteryService? _mastery;
        private bool _busy;
        private bool _silent;
        private uint _counter;

        public MasteryColorService(PlayerState player, ContentRegistry content, IGameClock clock)
        {
            _player = player;
            _content = content;
            _clock = clock;
            _onLeveledUp = OnLeveledUp;
        }

        public MasteryService? Mastery
        {
            get => _mastery;
            set
            {
                if (_mastery != null)
                    _mastery.LeveledUp -= _onLeveledUp;
                _mastery = value;
                if (_mastery != null)
                    _mastery.LeveledUp += _onLeveledUp;
            }
        }

        public EconomyService? Economy { get; set; }
        public RefugeLevelService? Refuge { get; set; }
        public AchievementHooks? AchievementHooks { get; set; }
        public ISaveScheduler? Save { get; set; }

        /// <summary>Raised once per color given by a level reached in play (not by <see cref="CatchUp"/>).</summary>
        public event MasteryColorHandler? Granted;

        /// <summary>The variant of a species given at a mastery level (10, 20 or 30), or false when the build holds none.</summary>
        public bool TryFind(ContentId species, int level, out VariantDefinition variant)
        {
            var all = _content.Variants;
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i];
                if (v.MasteryLevel == level && v.Species == species)
                {
                    variant = v;
                    return true;
                }
            }
            variant = null!;
            return false;
        }

        /// <summary>At boot: grants the colors a save already earned (levels reached before this rule existed). No event.</summary>
        public void CatchUp()
        {
            if (_mastery == null)
                return;
            bool wasSilent = _silent;
            _silent = true;
            _busy = true;
            try
            {
                var all = _content.Variants;
                // A granted animal can raise its species' level, so run again until nothing is left.
                for (int pass = 0; pass < 4; pass++)
                {
                    bool any = false;
                    for (int i = 0; i < all.Count; i++)
                    {
                        var v = all[i];
                        if (v.MasteryLevel <= 0 || _player.Collection.DiscoveredVariants.Contains(v.Id))
                            continue;
                        if (_mastery.Get(v.Species).Level < v.MasteryLevel)
                            continue;
                        Grant(v);
                        any = true;
                    }
                    if (!any)
                        break;
                }
                _pending.Clear();
            }
            finally
            {
                _busy = false;
                _silent = wasSilent;
            }
        }

        public void Dispose()
        {
            Mastery = null;
            Granted = null;
            _pending.Clear();
        }

        private void OnLeveledUp(MasteryGain gain)
        {
            if (_silent)
                return;
            _pending.Enqueue(gain);
            if (_busy)
                return;
            _busy = true;
            try
            {
                while (_pending.Count > 0)
                {
                    var next = _pending.Dequeue();
                    for (int t = 0; t < Thresholds.Length; t++)
                    {
                        int level = Thresholds[t];
                        if (next.Before.Level < level && next.After.Level >= level && TryFind(next.Species, level, out var variant))
                            Grant(variant);
                    }
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private void Grant(VariantDefinition variant)
        {
            var collection = _player.Collection;
            if (collection.DiscoveredVariants.Contains(variant.Id))
                return;
            long now = _clock.UtcNow.ToUnixTimeSeconds();
            uint seed = Mix((uint)now ^ unchecked((uint)variant.Id.GetHashCode() * 2654435761u) ^ unchecked(++_counter * 40503u));
            var animal = new AnimalInstance
            {
                Species = variant.Species,
                Variant = variant.Id,
                Seed = seed,
                CreatedUtcSeconds = (uint)Math.Max(0L, now),
                Rarity = variant.Rarity,
            };
            animal.InstanceId = collection.Add(animal);
            bool newSpecies = collection.DiscoveredSpecies.Add(variant.Species);
            bool newVariant = collection.DiscoveredVariants.Add(variant.Id);
            Economy?.OnAnimalAdded(animal);
            _mastery?.OnAnimalObtained(variant.Species, newSpecies, newVariant);
            Refuge?.Recompute();
            AchievementHooks?.CountObtained(in animal);
            AchievementHooks?.Recount.Collection();
            Save?.RequestSave(SaveUrgency.Priority);
            if (!_silent)
                Granted?.Invoke(new MasteryColorGrant(variant.Species, variant.Id, animal.InstanceId, variant.MasteryLevel));
        }

        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x == 0u ? 1u : x;
        }
    }
}
