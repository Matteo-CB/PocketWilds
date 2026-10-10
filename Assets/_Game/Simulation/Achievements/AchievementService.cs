#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>The rewards of one claim (every tier reached and not claimed yet, given together).</summary>
    public readonly struct AchievementClaim
    {
        public readonly int Index;
        public readonly AchievementTier From;
        public readonly AchievementTier To;
        public readonly int Gems;

        /// <summary>The Gold tier's biome decoration, given with this claim (None without).</summary>
        public readonly ContentId Decoration;

        /// <summary>The Star tier's title, frame or pose key, given with this claim (empty without).</summary>
        public readonly string Extra;

        public AchievementClaim(int index, AchievementTier from, AchievementTier to, int gems, ContentId decoration, string extra)
        {
            Index = index;
            From = from;
            To = to;
            Gems = gems;
            Decoration = decoration;
            Extra = extra;
        }
    }

    public delegate void AchievementClaimHandler(in AchievementClaim claim);

    /// <summary>
    /// T44B « Succès » (doc 01 Succès, sections 1 and 2): the 200 achievements, their counters and four tiers.
    /// <list type="bullet">
    /// <item>Counters run for the account's life, never reset (the Renewal included). Action counters add (<see cref="Add"/>);
    /// counters of a saved state are raised to the state's value and never lowered (<see cref="Raise"/>: a tier reached
    /// stays reached when an update adds content). The per-run ones (<see cref="AddRunHatches"/>, <see cref="AddRunLightLevels"/>)
    /// keep the best run; <see cref="BeginRun"/> starts a new run.</item>
    /// <item>Tiers reached wait for ever until claimed (<see cref="Claimable"/>, the HUD badge). <see cref="TryClaim"/> gives every
    /// waiting tier once: Gems 5, 10, 20, 50, the Gold decoration (biome achievements), the Star extra (title, frame, pose).</item>
    /// <item>Reads never allocate; a counter event never allocates.</item>
    /// </list>
    /// The systems are wired by <see cref="AchievementHooks"/>; testable without a scene.
    /// </summary>
    public sealed class AchievementService : IAchievements
    {
        private readonly AchievementState _state;
        private readonly AchievementInfo[] _infos = new AchievementInfo[AchievementData.Count];
        private int _claimable;

        public AchievementService(AchievementState state)
        {
            _state = state;
            for (int i = 0; i < _infos.Length; i++)
            {
                ref readonly var row = ref AchievementData.Row(i);
                _infos[i] = new AchievementInfo(i, row.Key, row.Category, row.Bronze, row.Silver, row.Gold, row.Star);
            }
            Recount();
        }

        public AchievementState State => _state;
        public ISaveScheduler? Save { get; set; }

        /// <summary>Gives the Gems of a claim (the hooks set it; null in tests: <see cref="RewardClaimed"/> carries them).</summary>
        public EconomyService? Economy { get; set; }

        /// <summary>Where the Gold decorations go.</summary>
        public InventoryState? Inventory { get; set; }

        /// <summary>Gives the Star extras.</summary>
        public ProfileItemService? Items { get; set; }

        public event Action? Changed;

        /// <summary>A tier reached for the first time (analytics achievement_tier: id, tier).</summary>
        public event Action<int, AchievementTier>? TierReached;

        /// <summary>A claim's rewards, raised after they were given.</summary>
        public event AchievementClaimHandler? RewardClaimed;

        // ------------------------------------------------------------------------------------------- IAchievements

        public int Count => _infos.Length;

        public AchievementInfo At(int index) => _infos[index];

        public long Progress(int index) => _state.Progress[index];

        public AchievementTier Reached(int index) => (AchievementTier)_state.Reached[index];

        public AchievementTier Claimed(int index) => (AchievementTier)_state.Claimed[index];

        public int Claimable => _claimable;

        /// <summary>The next tier's threshold (the Star's once every tier is reached): « {n} » of the goal line.</summary>
        public long NextThreshold(int index)
        {
            var reached = Reached(index);
            return _infos[index].Threshold(reached >= AchievementTier.Star ? AchievementTier.Star : reached + 1);
        }

        /// <summary>How many to go before the next tier (« Plus que 3 »); 0 once the Star is reached.</summary>
        public long ToGo(int index)
        {
            if (Reached(index) >= AchievementTier.Star)
                return 0;
            long left = NextThreshold(index) - _state.Progress[index];
            return left > 0 ? left : 0;
        }

        public bool TryClaim(int index)
        {
            if ((uint)index >= (uint)_infos.Length)
                return false;
            var from = (AchievementTier)_state.Claimed[index];
            var to = (AchievementTier)_state.Reached[index];
            if (to <= from)
                return false;
            int gems = 0;
            for (var t = from + 1; t <= to; t++)
                gems += AchievementData.GemsFor(t);
            ref readonly var row = ref AchievementData.Row(index);
            var decoration = from < AchievementTier.Gold && to >= AchievementTier.Gold ? AchievementData.GoldDecoration(index) : ContentId.None;
            string extra = from < AchievementTier.Star && to >= AchievementTier.Star ? row.StarExtra : string.Empty;
            _state.Claimed[index] = (byte)to;
            _claimable -= to - from;

            if (gems > 0)
                Economy?.Grant(CurrencyId.Gems, BigValue.FromDouble(gems), EconomyReason.Reward);
            if (decoration.IsValid)
                Inventory?.Add(decoration, 1);
            if (extra.Length > 0)
                Items?.Grant(extra);

            var claim = new AchievementClaim(index, from, to, gems, decoration, extra);
            RewardClaimed?.Invoke(in claim);
            Changed?.Invoke();
            Save?.RequestSave(SaveUrgency.Priority);
            return true;
        }

        /// <summary>Claims every achievement with a waiting tier (the screen's « Tout réclamer »); the claims made.</summary>
        public int ClaimAll()
        {
            int n = 0;
            for (int i = 0; i < _infos.Length && _claimable > 0; i++)
                if (TryClaim(i))
                    n++;
            return n;
        }

        // ------------------------------------------------------------------------------------------- counters

        /// <summary>An action counted (cumulated for the account's life). Unknown counters are ignored.</summary>
        public void Add(AchievementCounter counter, long amount = 1, int param = 0)
        {
            if (amount <= 0)
                return;
            int index = AchievementData.IndexOf(counter, param);
            if (index < 0)
                return;
            long value = _state.Progress[index] + amount;
            SetProgress(index, value < 0 ? long.MaxValue : value);
        }

        /// <summary>A counter of a saved state: raised to the state's value, never lowered.</summary>
        public void Raise(AchievementCounter counter, long value, int param = 0)
        {
            int index = AchievementData.IndexOf(counter, param);
            if (index >= 0 && value > _state.Progress[index])
                SetProgress(index, value);
        }

        /// <summary>Eggs hatched in this run (ACH-190 keeps the best run).</summary>
        public void AddRunHatches(long amount)
        {
            if (amount <= 0)
                return;
            _state.RunHatches += amount;
            Raise(AchievementCounter.RunHatches, _state.RunHatches);
        }

        /// <summary>Light upgrade levels bought in this run (ACH-194 keeps the best run).</summary>
        public void AddRunLightLevels(long amount)
        {
            if (amount <= 0)
                return;
            _state.RunLightLevels += amount;
            Raise(AchievementCounter.RunLightLevels, _state.RunLightLevels);
        }

        /// <summary>The run's Light upgrade levels as the save holds them (a backfill: never lowers the run's count).</summary>
        public void RaiseRunLightLevels(long levels)
        {
            if (levels > _state.RunLightLevels)
                _state.RunLightLevels = levels;
            Raise(AchievementCounter.RunLightLevels, _state.RunLightLevels);
        }

        /// <summary>A Renewal: the per-run counters start again (their achievements keep the best run).</summary>
        public void BeginRun()
        {
            _state.RunHatches = 0;
            _state.RunLightLevels = 0;
            _state.RunProductionBeaten = false;
            Save?.RequestSave(SaveUrgency.Normal);
        }

        /// <summary>« Plus haut » (ACH-192): the run's production beat the one of the last Renewal; once per run.</summary>
        public void CountProductionBeaten()
        {
            if (_state.RunProductionBeaten)
                return;
            _state.RunProductionBeaten = true;
            Add(AchievementCounter.ProductionBeaten);
        }

        /// <summary>A Renewal's Essence (ACH-191: the first sets the record and counts, each better one counts).</summary>
        public void CountRenewalEssence(long essence)
        {
            if (essence <= _state.BestRenewalEssence && _state.Progress[AchievementData.IndexOf(AchievementCounter.EssenceRecords)] > 0)
                return;
            if (essence > _state.BestRenewalEssence)
                _state.BestRenewalEssence = essence;
            Add(AchievementCounter.EssenceRecords);
        }

        /// <summary>Eggs hatched on a local day (ACH-092 keeps the best day).</summary>
        public void AddDayHatches(int dayKey, int amount)
        {
            if (amount <= 0)
                return;
            RollDay(dayKey);
            _state.DayHatches += amount;
            Raise(AchievementCounter.BestDayHatches, _state.DayHatches);
        }

        /// <summary>A wish granted on a local day (ACH-130 counts each day with 3).</summary>
        public void AddDayWish(int dayKey)
        {
            RollDay(dayKey);
            _state.DayWishes++;
            if (_state.DayWishes == 3)
                Add(AchievementCounter.FullWishDays);
        }

        private void RollDay(int dayKey)
        {
            if (_state.DayKey == dayKey)
                return;
            _state.DayKey = dayKey;
            _state.DayHatches = 0;
            _state.DayWishes = 0;
        }

        /// <summary>A bit of a distinct set (biomes hatched, rarities, activities starred, destinations, plants, poses used).</summary>
        public void AddDistinctBit(AchievementCounter counter, int bit)
        {
            if (bit < 0 || bit >= 64)
                return;
            ulong mask;
            switch (counter)
            {
                case AchievementCounter.HatchBiomes:
                    mask = _state.HatchBiomes = (uint)(_state.HatchBiomes | (1UL << bit));
                    break;
                case AchievementCounter.HatchRarities:
                    mask = _state.HatchRarities = (uint)(_state.HatchRarities | (1UL << bit));
                    break;
                case AchievementCounter.ActivitiesStarred:
                    mask = _state.ActivitiesStarred = (uint)(_state.ActivitiesStarred | (1UL << bit));
                    break;
                case AchievementCounter.Destinations:
                    mask = _state.Destinations = (uint)(_state.Destinations | (1UL << bit));
                    break;
                case AchievementCounter.PlantsHarvested:
                    mask = _state.PlantsHarvested = (uint)(_state.PlantsHarvested | (1UL << bit));
                    break;
                case AchievementCounter.PosesUsed:
                    mask = _state.PosesUsed |= 1UL << bit;
                    break;
                default:
                    return;
            }
            Raise(counter, PopCount(mask));
        }

        /// <summary>A distinct id (animals played with, wished for, petted; accessories): counted the first time only.</summary>
        public void AddDistinctId(AchievementCounter counter, uint id)
        {
            switch (counter)
            {
                case AchievementCounter.AnimalsPlayed:
                    AddId(counter, _state.AnimalsPlayed, ref _state.AnimalsPlayedCount, id);
                    break;
                case AchievementCounter.AnimalsWished:
                    AddId(counter, _state.AnimalsWished, ref _state.AnimalsWishedCount, id);
                    break;
                case AchievementCounter.AnimalsPetted:
                    AddId(counter, _state.AnimalsPetted, ref _state.AnimalsPettedCount, id);
                    break;
                case AchievementCounter.AccessoriesEquipped:
                    AddId(counter, _state.Accessories, ref _state.AccessoriesCount, id);
                    break;
            }
        }

        private void AddId(AchievementCounter counter, uint[] list, ref int count, uint id)
        {
            if (id == 0 || count >= list.Length)
                return;
            for (int i = 0; i < count; i++)
                if (list[i] == id)
                    return;
            list[count++] = id;
            Raise(counter, count);
        }

        /// <summary>An animal obtained with these trait bits (<see cref="AnimalTrait"/>): new species and trait pairs (ACH-011).</summary>
        public void AddSpeciesTraits(int species, int traitBits)
        {
            if ((uint)species >= (uint)_state.SpeciesTraits.Length)
                return;
            int known = _state.SpeciesTraits[species];
            int added = traitBits & 0x3F & ~known;
            if (added == 0)
                return;
            _state.SpeciesTraits[species] = (byte)(known | added);
            Add(AchievementCounter.SpeciesTraitPairs, PopCount((ulong)added));
        }

        // ------------------------------------------------------------------------------------------- tiers

        private void SetProgress(int index, long value)
        {
            _state.Progress[index] = value;
            var before = (AchievementTier)_state.Reached[index];
            var after = TierOf(index, value);
            if (after > before)
            {
                _state.Reached[index] = (byte)after;
                _claimable += after - before;
                for (var t = before + 1; t <= after; t++)
                    TierReached?.Invoke(index, t);
                Save?.RequestSave(SaveUrgency.Priority);
            }
            else
            {
                Save?.RequestSave(SaveUrgency.Normal);
            }
            Changed?.Invoke();
        }

        private AchievementTier TierOf(int index, long value)
        {
            ref readonly var info = ref _infos[index];
            return value >= info.Star ? AchievementTier.Star
                : value >= info.Gold ? AchievementTier.Gold
                : value >= info.Silver ? AchievementTier.Silver
                : value >= info.Bronze ? AchievementTier.Bronze
                : AchievementTier.None;
        }

        /// <summary>Tiers from the saved progress (after a load: a tier reached is kept, claims never exceed tiers).</summary>
        public void Recount()
        {
            _claimable = 0;
            for (int i = 0; i < _infos.Length; i++)
            {
                var tier = TierOf(i, _state.Progress[i]);
                if ((byte)tier > _state.Reached[i])
                    _state.Reached[i] = (byte)tier;
                if (_state.Reached[i] > (byte)AchievementTier.Star)
                    _state.Reached[i] = (byte)AchievementTier.Star;
                if (_state.Claimed[i] > _state.Reached[i])
                    _state.Claimed[i] = _state.Reached[i];
                _claimable += _state.Reached[i] - _state.Claimed[i];
            }
        }

        private static int PopCount(ulong v)
        {
            int n = 0;
            for (; v != 0; v &= v - 1)
                n++;
            return n;
        }
    }
}
