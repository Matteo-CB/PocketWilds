#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// One companion of the catalog (doc Packs, A-SP-101 and up): the item that gives it, its species and biome, its looks and
    /// bond rewards, and the Light bonus its pack gives the biome while owned. Ids are frozen.
    /// </summary>
    public sealed class CompanionRow
    {
        public CompanionRow(int subject, string entitlement, string species, string biome, string baseLook, string look5, string look10,
            string accessory15, double biomeBonus)
        {
            Subject = subject;
            Entitlement = ContentId.FromSource(entitlement);
            Species = ContentId.FromSource(species);
            Biome = ContentId.FromSource(biome);
            BaseLook = ContentId.FromSource(baseLook);
            Look5 = ContentId.FromSource(look5);
            Look10 = ContentId.FromSource(look10);
            Accessory15 = ContentId.FromSource(accessory15);
            BiomeBonus = biomeBonus;
        }

        /// <summary>Profile subject of its pose (bond 20) and title (bond 25): <see cref="ProfileSource.Companion"/> rows.</summary>
        public int Subject { get; }

        public ContentId Entitlement { get; }
        public ContentId Species { get; }
        public ContentId Biome { get; }

        /// <summary>The look it arrives with, then the looks given at bond 5 and 10 (variants of its species).</summary>
        public ContentId BaseLook { get; }
        public ContentId Look5 { get; }
        public ContentId Look10 { get; }

        /// <summary>Its signature accessory, given at bond 15 (an item of kind Cosmetic).</summary>
        public ContentId Accessory15 { get; }

        /// <summary>Light bonus for the animals of its biome while its item is owned (+5 %: 0.05).</summary>
        public double BiomeBonus { get; }
    }

    /// <summary>The shipped companions (T47: the Field Hamster of the Meadow Pack).</summary>
    public static class CompanionCatalog
    {
        public static readonly CompanionRow FieldHamster = new CompanionRow(101, "companion.fieldhamster", "species.fieldhamster", "biome.grassland",
            "variant.fieldhamster.fieldgold", "variant.fieldhamster.wheatcream", "variant.fieldhamster.fieldlavender", "accessory.fieldhamster.seedbasket", 0.05);

        public static readonly CompanionRow[] Rows = { FieldHamster };

        public static CompanionRow? BySpecies(ContentId species)
        {
            for (int i = 0; i < Rows.Length; i++)
                if (Rows[i].Species == species)
                    return Rows[i];
            return null;
        }

        public static CompanionRow? ByEntitlement(ContentId item)
        {
            for (int i = 0; i < Rows.Length; i++)
                if (Rows[i].Entitlement == item)
                    return Rows[i];
            return null;
        }

        /// <summary>× (1 + bonus) of every owned companion pack of this biome (the Meadow Pack: × 1.05 for the Meadow). No allocation.</summary>
        public static double BiomeMultiplier(EntitlementState entitlements, ContentId biome)
        {
            double multiplier = 1.0;
            for (int i = 0; i < Rows.Length; i++)
                if (Rows[i].Biome == biome && entitlements.Owned.Contains(Rows[i].Entitlement))
                    multiplier *= 1.0 + Rows[i].BiomeBonus;
            return multiplier;
        }
    }

    /// <summary>What a companion bond level gives (doc 02 Renouveau, section 5). Stable byte values.</summary>
    public enum CompanionReward : byte
    {
        /// <summary>Every level without a named reward: 10 minutes of production in Light.</summary>
        Light = 0,
        Look = 1,
        Accessory = 2,
        Pose = 3,
        Title = 4,
    }

    /// <summary>A companion reached a bond level (one event per level, in order).</summary>
    public readonly struct CompanionLevelUp
    {
        public readonly ContentId Species;
        public readonly uint Animal;
        public readonly int Level;
        public readonly CompanionReward Reward;

        /// <summary>The look (variant) or accessory given; None for Light, pose and title (profile items).</summary>
        public readonly ContentId Item;

        public readonly BigValue Light;

        public CompanionLevelUp(ContentId species, uint animal, int level, CompanionReward reward, ContentId item, BigValue light)
        {
            Species = species;
            Animal = animal;
            Level = level;
            Reward = reward;
            Item = item;
            Light = light;
        }
    }

    /// <summary>A companion's bond: level, XP inside the level and XP the next level needs (0 at level 25).</summary>
    public readonly struct CompanionBond
    {
        public readonly int Level;
        public readonly int XpInLevel;
        public readonly int XpForNext;
        public readonly int TotalXp;

        public CompanionBond(int level, int xpInLevel, int xpForNext, int totalXp)
        {
            Level = level;
            XpInLevel = xpInLevel;
            XpForNext = xpForNext;
            TotalXp = totalXp;
        }
    }

    public delegate void CompanionLevelHandler(in CompanionLevelUp levelUp);

    /// <summary>Lets <see cref="BondService"/> feed a companion's bond XP (its own track, 25 levels) and raise its touch cap.</summary>
    public interface ICompanionBondSink
    {
        bool IsCompanion(uint animal);
        void OnBondEvent(uint animal, BondSource source);
    }

    /// <summary>
    /// T47 companions (doc Packs; doc 02 Renouveau, section 5). Owning the item (<see cref="EntitlementKind.Companion"/>) places
    /// the companion animal once in its biome; it is unique per account, produces like a Rare animal of its biome, and leaves when
    /// the item is revoked. <see cref="Arrived"/> asks the view for the 2 s staged arrival (also kept as
    /// <see cref="CompanionRecord.ArrivalPending"/> until <see cref="MarkArrivalSeen"/>, so an arrival at boot is not lost).
    /// Bond: 25 levels, level L needs 40 + 20 × L XP (7 500 in all); touch 5 (10 touches a day), treat 25, wish 30, activity 15,
    /// expedition 20, photo 10 once a day; Essence Best Friends +10 % per rank. Rewards: 5 and 10 the second and third looks,
    /// 15 the signature accessory, 20 the photo pose, 25 the profile title, every other level 10 minutes of Light. A companion
    /// never breeds, is out of the Codex completion and of every egg pool, and nothing requires one.
    /// </summary>
    public sealed class CompanionService : ICompanionBondSink, IDisposable
    {
        public const int MaxLevel = 25;

        /// <summary>Counted touches a day on a companion (BondService uses it as the companion's per-animal cap).</summary>
        public const int TouchesPerDay = 10;

        public const int PhotoXp = 10;
        public const int LightRewardMinutes = 10;

        private readonly PlayerState _state;
        private readonly ContentRegistry _content;
        private readonly IGameClock _clock;
        private readonly ISaveScheduler? _save;
        private readonly Action<GrantResult> _onGrantChanged;
        private GrantService? _grants;
        private uint _counter;
        private bool _rewarding;

        public CompanionService(PlayerState state, ContentRegistry content, IGameClock clock, ISaveScheduler? save)
        {
            _state = state;
            _content = content;
            _clock = clock;
            _save = save;
            _onGrantChanged = _ =>
            {
                // The bond 15 accessory is itself a grant: the rewards loop handles it, no sync inside it.
                if (!_rewarding)
                    Sync();
            };
        }

        /// <summary>Grants and revokes of store items: each one syncs the companions.</summary>
        public GrantService? Grants
        {
            get => _grants;
            set
            {
                if (_grants != null)
                {
                    _grants.Granted -= _onGrantChanged;
                    _grants.Revoked -= _onGrantChanged;
                }
                _grants = value;
                if (_grants != null)
                {
                    _grants.Granted += _onGrantChanged;
                    _grants.Revoked += _onGrantChanged;
                }
            }
        }

        public EconomyService? Economy { get; set; }
        public EssenceService? Essence { get; set; }
        public RefugeLevelService? Refuge { get; set; }
        public ProfileItemService? ProfileItems { get; set; }
        public CosmeticService? Cosmetics { get; set; }

        /// <summary>The player's calendar day (DailyContext.TodayKey). Null: the UTC day.</summary>
        public Func<int>? DayKey { get; set; }

        /// <summary>A companion joined the refuge: species, animal. The view stages its 2 s arrival.</summary>
        public event Action<ContentId, uint>? Arrived;

        /// <summary>A companion left because its item was revoked (refund): species, animal removed.</summary>
        public event Action<ContentId, uint>? Left;

        /// <summary>Bond XP really added: species, XP.</summary>
        public event Action<ContentId, int>? BondGained;

        /// <summary>One event per level reached, in order, with its reward already given.</summary>
        public event CompanionLevelHandler? LevelReached;

        public static int MaxXp => XpToReach(MaxLevel);

        /// <summary>Total XP to reach a level: sum of 40 + 20 × k for k = 1 to level (25: 7 500).</summary>
        public static int XpToReach(int level)
        {
            level = Math.Max(0, Math.Min(MaxLevel, level));
            return 40 * level + 10 * level * (level + 1);
        }

        public static int LevelOf(int xp)
        {
            int level = 0;
            while (level < MaxLevel && xp >= XpToReach(level + 1))
                level++;
            return level;
        }

        /// <summary>Companion bond XP per bond source (econ.companion_bond.*).</summary>
        public static int XpFor(BondSource source) => source switch
        {
            BondSource.Touch => 5,
            BondSource.Treat => 25,
            BondSource.Wish => 30,
            BondSource.Activity => 15,
            BondSource.Expedition => 20,
            _ => 0,
        };

        public bool IsCompanion(uint animal) => _state.Companions.IndexOfAnimal(animal) >= 0;

        /// <summary>The account owns this companion's item.</summary>
        public bool Owns(ContentId species)
        {
            var row = CompanionCatalog.BySpecies(species);
            return row != null && _state.Entitlements.Owned.Contains(row.Entitlement);
        }

        public bool TryGet(ContentId species, out CompanionRecord record)
        {
            int index = _state.Companions.IndexOf(species);
            record = index >= 0 ? _state.Companions.Companions[index] : default;
            return index >= 0;
        }

        /// <summary>The companion's animal in the refuge, or 0.</summary>
        public uint AnimalOf(ContentId species) => TryGet(species, out var record) ? record.AnimalId : 0;

        public CompanionBond GetBond(ContentId species)
        {
            int xp = TryGet(species, out var record) ? record.BondXp : 0;
            int level = LevelOf(xp);
            int start = XpToReach(level);
            int next = level >= MaxLevel ? 0 : XpToReach(level + 1) - start;
            return new CompanionBond(level, xp - start, next, xp);
        }

        /// <summary>The base look always; the second from bond 5, the third from bond 10.</summary>
        public bool IsLookUnlocked(ContentId species, ContentId variant)
        {
            var row = CompanionCatalog.BySpecies(species);
            if (row == null)
                return false;
            int level = GetBond(species).Level;
            return variant == row.BaseLook || (variant == row.Look5 && level >= 5) || (variant == row.Look10 && level >= 10);
        }

        /// <summary>Dresses the companion in an unlocked look (its variant). False when locked, unknown or not in the refuge.</summary>
        public bool SetLook(ContentId species, ContentId variant)
        {
            uint animal = AnimalOf(species);
            if (animal == 0 || !IsLookUnlocked(species, variant) || !_content.Variants.TryGet(variant, out var definition) || definition.Species != species)
                return false;
            if (!_state.Collection.TryGet(animal, out var instance) || instance.Variant == variant)
                return false;
            instance.Variant = variant;
            _state.Collection.Update(instance);
            _save?.RequestSave(SaveUrgency.Normal);
            return true;
        }

        /// <summary>The first companion whose staged arrival the view still owes.</summary>
        public bool TryGetPendingArrival(out ContentId species, out uint animal)
        {
            var list = _state.Companions.Companions;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].ArrivalPending && list[i].AnimalId != 0)
                {
                    species = list[i].Species;
                    animal = list[i].AnimalId;
                    return true;
                }
            }
            species = ContentId.None;
            animal = 0;
            return false;
        }

        /// <summary>The view played the arrival.</summary>
        public void MarkArrivalSeen(ContentId species)
        {
            int index = _state.Companions.IndexOf(species);
            if (index < 0 || !_state.Companions.Companions[index].ArrivalPending)
                return;
            var record = _state.Companions.Companions[index];
            record.ArrivalPending = false;
            _state.Companions.Companions[index] = record;
            _save?.RequestSave(SaveUrgency.Normal);
        }

        /// <summary>
        /// Brings the refuge in line with the items owned: places each owned companion missing from the collection (once), removes
        /// the companion of a revoked item. Called on every grant and revoke, and once at boot (a restore delivered at launch, an
        /// older save). Returns the number of companions placed or removed.
        /// </summary>
        public int Sync()
        {
            int changes = 0;
            var rows = CompanionCatalog.Rows;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                bool owned = _state.Entitlements.Owned.Contains(row.Entitlement);
                int index = _state.Companions.IndexOf(row.Species);
                bool present = index >= 0 && _state.Companions.Companions[index].AnimalId != 0
                               && _state.Collection.TryGetIndex(_state.Companions.Companions[index].AnimalId, out _);
                if (owned && !present && Place(row, ref index))
                    changes++;
                else if (!owned && present && Remove(row, index))
                    changes++;
                if (owned && index >= 0)
                    CatchUpRewards(row, index);
            }
            // The pack's Light bonus follows the item.
            Economy?.MarkProductionDirty();
            return changes;
        }

        /// <summary>A photo of the companion (photo mode): +10 XP once a day. False when not a companion or already today.</summary>
        public bool OnPhoto(uint animal)
        {
            int index = _state.Companions.IndexOfAnimal(animal);
            if (index < 0)
                return false;
            int day = Today();
            var record = _state.Companions.Companions[index];
            if (record.PhotoDay == day)
                return false;
            record.PhotoDay = day;
            _state.Companions.Companions[index] = record;
            AddXp(index, PhotoXp);
            return true;
        }

        void ICompanionBondSink.OnBondEvent(uint animal, BondSource source)
        {
            int index = _state.Companions.IndexOfAnimal(animal);
            if (index >= 0)
                AddXp(index, XpFor(source));
        }

        /// <summary>Adds bond XP before Essence (tools and tests; play goes through BondService). Returns the XP really added.</summary>
        public int AddBondXp(ContentId species, int xp)
        {
            int index = _state.Companions.IndexOf(species);
            return index >= 0 && _state.Companions.Companions[index].AnimalId != 0 ? AddXp(index, xp) : 0;
        }

        public void Dispose()
        {
            Grants = null;
            Arrived = null;
            Left = null;
            BondGained = null;
            LevelReached = null;
        }

        private int AddXp(int index, int xp)
        {
            if (xp <= 0)
                return 0;
            // Essence Best Friends: +10 % per rank for favorites and companions (doc 02 Renouveau, section 2).
            double multiplier = 1.0 + (Essence != null ? Essence.Sum(EssenceEffect.FavoriteBond) : 0.0);
            int scaled = (int)Math.Round(xp * Math.Max(0.0, multiplier), MidpointRounding.AwayFromZero);
            var record = _state.Companions.Companions[index];
            int added = Math.Min(scaled, MaxXp - record.BondXp);
            if (added <= 0)
                return 0;
            record.BondXp += added;
            _state.Companions.Companions[index] = record;
            BondGained?.Invoke(record.Species, added);
            var row = CompanionCatalog.BySpecies(record.Species);
            if (row != null)
                GiveRewards(row, index, silent: false);
            _save?.RequestSave(SaveUrgency.Normal);
            return added;
        }

        private bool Place(CompanionRow row, ref int index)
        {
            if (!_content.Species.TryGet(row.Species, out _))
                return false;
            var look = row.BaseLook;
            if (!_content.Variants.TryGet(look, out _) && !TryFirstVariant(row.Species, out look))
                return false;
            long now = _clock.UtcNow.ToUnixTimeSeconds();
            var animal = new AnimalInstance
            {
                Species = row.Species,
                Variant = look,
                Seed = Mix((uint)now ^ unchecked(row.Species.Value * 2654435761u) ^ unchecked(++_counter * 40503u)),
                CreatedUtcSeconds = (uint)Math.Max(0L, now),
                // « Produit comme un animal Rare de son biome » (doc Packs).
                Rarity = RarityTier.Rare,
            };
            animal.InstanceId = _state.Collection.Add(animal);
            if (index < 0)
            {
                _state.Companions.Companions.Add(new CompanionRecord { Species = row.Species });
                index = _state.Companions.Companions.Count - 1;
            }
            var record = _state.Companions.Companions[index];
            record.AnimalId = animal.InstanceId;
            record.ArrivalPending = true;
            _state.Companions.Companions[index] = record;
            Economy?.OnAnimalAdded(animal);
            Refuge?.Recompute();
            _save?.RequestSave(SaveUrgency.Priority);
            Arrived?.Invoke(row.Species, animal.InstanceId);
            return true;
        }

        private bool Remove(CompanionRow row, int index)
        {
            var record = _state.Companions.Companions[index];
            uint id = record.AnimalId;
            if (_state.Collection.TryGet(id, out var animal))
            {
                _state.Collection.Remove(id);
                Economy?.OnAnimalRemoved(animal);
            }
            if (_state.Cosmetics.Animals.Remove(id))
                Cosmetics?.Prune();
            // The bond stays: a companion bought again comes back with it.
            record.AnimalId = 0;
            record.ArrivalPending = false;
            _state.Companions.Companions[index] = record;
            Refuge?.Recompute();
            _save?.RequestSave(SaveUrgency.Priority);
            Left?.Invoke(row.Species, id);
            return true;
        }

        /// <summary>Rewards an older state already earned, without events (and the profile rows, which are idempotent).</summary>
        private void CatchUpRewards(CompanionRow row, int index)
        {
            GiveRewards(row, index, silent: true);
            int level = LevelOf(_state.Companions.Companions[index].BondXp);
            if (level >= 20)
                ProfileItems?.OnCompanionBond(row.Subject, level);
            if (level >= 15)
                _state.Entitlements.Owned.Add(row.Accessory15);
        }

        private void GiveRewards(CompanionRow row, int index, bool silent)
        {
            var record = _state.Companions.Companions[index];
            int level = LevelOf(record.BondXp);
            if (_rewarding || record.RewardedLevel >= level)
                return;
            _rewarding = true;
            try
            {
                while (record.RewardedLevel < level)
                {
                    int next = record.RewardedLevel + 1;
                    record.RewardedLevel = (byte)next;
                    _state.Companions.Companions[index] = record;
                    var reward = CompanionReward.Light;
                    var item = ContentId.None;
                    var light = BigValue.Zero;
                    switch (next)
                    {
                        case 5:
                            reward = CompanionReward.Look;
                            item = row.Look5;
                            break;
                        case 10:
                            reward = CompanionReward.Look;
                            item = row.Look10;
                            break;
                        case 15:
                            reward = CompanionReward.Accessory;
                            item = row.Accessory15;
                            if (_grants != null)
                                _grants.GrantItems(ContentId.None, new[] { item });
                            else
                                _state.Entitlements.Owned.Add(item);
                            break;
                        case 20:
                            reward = CompanionReward.Pose;
                            ProfileItems?.OnCompanionBond(row.Subject, next);
                            break;
                        case MaxLevel:
                            reward = CompanionReward.Title;
                            ProfileItems?.OnCompanionBond(row.Subject, next);
                            break;
                        default:
                            if (!silent && Economy != null)
                            {
                                light = BigValue.Multiply(Economy.GetLightPerSecond(), LightRewardMinutes * 60.0);
                                if (!light.IsZero)
                                    Economy.Grant(CurrencyId.Light, light, EconomyReason.Reward);
                            }
                            break;
                    }
                    record = _state.Companions.Companions[index];
                    if (!silent)
                        LevelReached?.Invoke(new CompanionLevelUp(row.Species, record.AnimalId, next, reward, item, light));
                }
            }
            finally
            {
                _rewarding = false;
            }
        }

        private bool TryFirstVariant(ContentId species, out ContentId variant)
        {
            var all = _content.Variants;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Species == species)
                {
                    variant = all[i].Id;
                    return true;
                }
            }
            variant = ContentId.None;
            return false;
        }

        private int Today() => DayKey != null ? DayKey() : (int)(_clock.UtcNow.ToUnixTimeSeconds() / 86400L);

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
