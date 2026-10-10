#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>A team left (analytics 140 expedition_start; WISH-041 to 044 and 046; a challenge's stars).</summary>
    public struct ExpeditionStartInfo
    {
        public int Slot;
        public ContentId Destination;

        /// <summary>The duration column of the loot (the first stroll: 1 h).</summary>
        public ExpeditionDuration Duration;

        /// <summary>The real wait in seconds (3 min for the first stroll, −25 % during the Expeditions weekend).</summary>
        public double Seconds;

        public int TeamSize;
        public uint Animal0;
        public uint Animal1;
        public uint Animal2;
        public int Points;
        public byte Stars;
        public bool FirstStroll;

        /// <summary>The challenge (0 to 9), −1 for a free expedition.</summary>
        public int Challenge;

        /// <summary>The challenge's stars of this departure and the best before it.</summary>
        public byte ChallengeStars;

        public byte ChallengeStarsBefore;

        public uint AnimalAt(int index) => index == 0 ? Animal0 : index == 1 ? Animal1 : Animal2;

        public bool HasAnimal(uint animal)
        {
            for (int i = 0; i < TeamSize && i < ExpeditionState.MaxTeam; i++)
                if (AnimalAt(i) == animal)
                    return true;
            return false;
        }
    }

    /// <summary>A team welcomed (analytics 141 expedition_return; WISH-045; JRN-009, 024, 025, 026).</summary>
    public struct ExpeditionWelcomeInfo
    {
        public ExpeditionReturn Return;

        /// <summary>A long expedition (8 h or more): JRN-025.</summary>
        public bool Long;

        /// <summary>A destination not brought back yet during the current journal week: JRN-026.</summary>
        public bool NewPlaceThisWeek;

        /// <summary>Analytics 141's reward_mask (<see cref="ExpeditionRules.RewardMask"/>).</summary>
        public int RewardMask;
    }

    public delegate void ExpeditionStartedHandler(in ExpeditionStartInfo info);

    public delegate void ExpeditionWelcomedHandler(in ExpeditionWelcomeInfo info);

    /// <summary>
    /// T42 « Expéditions » (doc 01 Contenu, section 7 and S-ACT-005): the first meta activity. Teams of 1 to 3 animals leave
    /// for 1, 4, 8 or 12 h (the account's first for 3 min) and come back with loot and a postcard.
    /// <list type="bullet">
    /// <item>Timers are absolute UTC times in <see cref="ExpeditionState"/>, read from <see cref="IGameClock.UtcNow"/>: they run
    /// while the game is closed; a team back waits for ever; a clock set back never makes a wait longer than its duration
    /// (<see cref="SecondsLeft"/> is clamped and <see cref="Tick"/> moves such a team's times to now).</item>
    /// <item>Every random part of the loot is drawn at departure with the expedition's seed and saved; the welcome only
    /// grants it (Light as minutes of the production of that moment). A speed-up draws nothing.</item>
    /// <item>The welcome grants through the existing services: <see cref="RewardGranter"/> (Light, treats, tickets, the
    /// Discovery egg with <see cref="DiscoverySource.Expedition"/>, the sticker count), the inventory (seeds, ingredients),
    /// the bond (<see cref="BondSource.Expedition"/>) and the biome mastery.</item>
    /// </list>
    /// Built by the bootstrap after <see cref="ActivityServices"/>; <see cref="Tick"/> each frame (cheap, no allocation).
    /// Reads never allocate.
    /// </summary>
    public sealed class ExpeditionService : IExpeditions
    {
        private readonly ActivityServices _activities;
        private readonly ActivityContext _ctx;
        private readonly ExpeditionState _state;
        private readonly ExpeditionSlotState[] _seen = new ExpeditionSlotState[ExpeditionState.MaxSlots];
        private bool _seenReady;

        // SuggestTeam's scratch, grown with the collection (a button press, never per frame).
        private uint[] _freeIds = new uint[64];
        private byte[] _freeClass = new byte[64];
        private bool[] _firstOfSpecies = new bool[64];
        private readonly int[] _classAnimals = new int[4];
        private readonly int[] _classSpecies = new int[4];
        private readonly int[] _pick = new int[4];
        private readonly int[] _bestPick = new int[4];

        public ExpeditionService(ActivityServices activities, ExpeditionConfig? config = null)
        {
            _activities = activities;
            _ctx = activities.Context;
            _state = _ctx.State.Expeditions;
            Config = config ?? new ExpeditionConfig();
        }

        public ExpeditionConfig Config { get; }
        public ExpeditionState State => _state;

        /// <summary>The Wild Club is active (the Club slot); the activities' flag is the one the shop sets.</summary>
        public bool ClubActive => _activities.Config.ClubActive;

        public event Action<int>? SlotChanged;

        /// <summary>A team left (after the save request).</summary>
        public event ExpeditionStartedHandler? Started;

        /// <summary>A team was welcomed (after the grants).</summary>
        public event ExpeditionWelcomedHandler? Welcomed;

        /// <summary>Finish now: slot, Gems spent, seconds that were left (analytics 142).</summary>
        public event Action<int, int, double>? SpedUp;

        /// <summary>A challenge's best stars rose: challenge (0 to 9), stars, attempts (analytics 112).</summary>
        public event Action<int, int, int>? ChallengeImproved;

        /// <summary>The Gems slot was bought.</summary>
        public event Action? SlotBought;

        // ------------------------------------------------------------------------------------------- opening and slots

        public bool IsOpen => Config.Enabled && _activities.IsAvailable(ActivityId.Expeditions);

        public int SlotCount => Math.Min(ExpeditionState.MaxSlots, Math.Max(1, Config.SlotsBase) + 1 + Math.Max(0, Config.SlotsClubBonus));

        /// <summary>The Gems slot's index (after the free ones).</summary>
        public int GemsSlot => Math.Min(ExpeditionState.MaxSlots - 1, Math.Max(1, Config.SlotsBase));

        public long SlotPrice => Math.Max(0, Config.SlotPrice);

        /// <summary>The slot can take a team: a free one, the Gems slot once bought, the Club slots while the Club runs.</summary>
        public bool IsOwned(int slot)
        {
            if ((uint)slot >= (uint)SlotCount)
                return false;
            int gems = GemsSlot;
            if (slot < gems)
                return true;
            if (slot == gems)
                return _state.SlotBought;
            return ClubActive;
        }

        public int OwnedSlots
        {
            get
            {
                int owned = 0;
                for (int s = 0; s < SlotCount; s++)
                    if (IsOwned(s))
                        owned++;
                return owned;
            }
        }

        public ExpeditionSlotState StateOf(int slot) => StateAt(slot, NowMs);

        public ExpeditionSlotUnlock UnlockOf(int slot)
        {
            if ((uint)slot >= (uint)SlotCount || IsOwned(slot) || _state.Slots[slot].Active)
                return ExpeditionSlotUnlock.None;
            return slot == GemsSlot ? ExpeditionSlotUnlock.Gems : ExpeditionSlotUnlock.Club;
        }

        public bool TryBuySlot()
        {
            if (!IsOpen || _state.SlotBought)
                return false;
            if (!_ctx.Economy.Spend(CurrencyId.Gems, BigValue.FromDouble(SlotPrice), EconomyReason.Slot))
                return false;
            _state.SlotBought = true;
            _ctx.RequestSave(SaveUrgency.Priority);
            SlotBought?.Invoke();
            Raise(GemsSlot);
            return true;
        }

        // ------------------------------------------------------------------------------------------- a slot's team

        public ContentId DestinationOf(int slot) => Active(slot) ? _state.Slots[slot].Destination : ContentId.None;

        public ExpeditionDuration DurationOf(int slot) => Active(slot) ? _state.Slots[slot].Duration : ExpeditionDuration.OneHour;

        public int TeamSizeOf(int slot) => Active(slot) ? Math.Min(ExpeditionState.MaxTeam, (int)_state.Slots[slot].TeamSize) : 0;

        public uint TeamMember(int slot, int index) =>
            Active(slot) && (uint)index < (uint)TeamSizeOf(slot) ? _state.Slots[slot].AnimalAt(index) : 0u;

        public byte StarsOf(int slot) => Active(slot) ? _state.Slots[slot].Stars : (byte)0;

        public double SecondsLeft(int slot)
        {
            if (!Active(slot))
                return 0.0;
            ref readonly var record = ref _state.Slots[slot];
            return Left(in record, NowMs);
        }

        public double TotalSecondsOf(int slot)
        {
            if (!Active(slot))
                return 0.0;
            ref readonly var record = ref _state.Slots[slot];
            return Math.Max(0L, record.ReturnUnixMs - record.StartUnixMs) / 1000.0;
        }

        public int BackCount
        {
            get
            {
                long now = NowMs;
                int back = 0;
                for (int s = 0; s < ExpeditionState.MaxSlots; s++)
                    if (StateAt(s, now) == ExpeditionSlotState.Back)
                        back++;
                return back;
            }
        }

        public double SoonestReturn
        {
            get
            {
                long now = NowMs;
                double soonest = -1.0;
                for (int s = 0; s < ExpeditionState.MaxSlots; s++)
                {
                    if (StateAt(s, now) != ExpeditionSlotState.Away)
                        continue;
                    double left = Left(in _state.Slots[s], now);
                    if (soonest < 0.0 || left < soonest)
                        soonest = left;
                }
                return soonest;
            }
        }

        // ------------------------------------------------------------------------------------------- finish now

        public int SpeedUpCost(int slot) => StateOf(slot) == ExpeditionSlotState.Away ? ExpeditionRules.SpeedUpCost(SecondsLeft(slot), Config) : 0;

        public int SpeedUpsLeftToday
        {
            get
            {
                int used = _state.SpeedUpDayKey == _ctx.TodayKey ? _state.SpeedUpsToday : 0;
                return Math.Max(0, Config.SpeedUpsPerDay - used);
            }
        }

        public bool TrySpeedUp(int slot)
        {
            long now = NowMs;
            if (StateAt(slot, now) != ExpeditionSlotState.Away || SpeedUpsLeftToday <= 0)
                return false;
            ref var record = ref _state.Slots[slot];
            double left = Left(in record, now);
            int cost = ExpeditionRules.SpeedUpCost(left, Config);
            if (cost <= 0 || !_ctx.Economy.Spend(CurrencyId.Gems, BigValue.FromDouble(cost), EconomyReason.SpeedUp))
                return false;
            int today = _ctx.TodayKey;
            if (_state.SpeedUpDayKey != today)
            {
                _state.SpeedUpDayKey = today;
                _state.SpeedUpsToday = 0;
            }
            _state.SpeedUpsToday++;
            // The return comes now; the loot was drawn at departure and does not change.
            record.ReturnUnixMs = Math.Max(record.StartUnixMs, now);
            _ctx.RequestSave(SaveUrgency.Priority);
            SpedUp?.Invoke(slot, cost, left);
            Raise(slot);
            return true;
        }

        // ------------------------------------------------------------------------------------------- destinations

        public int DestinationCount => ExpeditionCatalog.Count;

        public ExpeditionDestination DestinationAt(int index) =>
            (uint)index < (uint)ExpeditionCatalog.Count ? ExpeditionCatalog.All[index].Destination : default;

        public bool TryGetDestination(ContentId id, out ExpeditionDestination destination)
        {
            int index = ExpeditionCatalog.IndexOf(id);
            destination = index >= 0 ? ExpeditionCatalog.All[index].Destination : default;
            return index >= 0;
        }

        public bool IsDestinationOpen(ContentId destination)
        {
            int index = ExpeditionCatalog.IndexOf(destination);
            return index >= 0 && ExpeditionCatalog.IsOpen(ExpeditionCatalog.All[index], _ctx.Progression);
        }

        // ------------------------------------------------------------------------------------------- the team

        public bool IsAway(uint instanceId) => _state.IsAway(instanceId);

        public ExpeditionRating Rate(ContentId destination, uint[] team, int count)
        {
            int index = ExpeditionCatalog.IndexOf(destination);
            var info = index >= 0 ? ExpeditionCatalog.All[index] : null;
            int valid = 0, fromBiome = 0;
            bool favorite = false;
            ContentId s0 = default, s1 = default, s2 = default;
            int n = team == null ? 0 : Math.Min(Math.Min(count, team.Length), ExpeditionState.MaxTeam);
            for (int i = 0; i < n; i++)
            {
                if (!TrySpecies(team![i], out var species, out var definition) || Duplicate(team, i))
                    continue;
                if (info != null && !info.IsEvent && definition.Biome == info.Destination.Biome)
                    fromBiome++;
                if (info != null && (info.Destination.FavoriteFamilies & ExpeditionRules.MaskOf(definition.RigFamily)) != 0)
                    favorite = true;
                if (valid == 0) s0 = species;
                else if (valid == 1) s1 = species;
                else s2 = species;
                valid++;
            }
            bool three = valid == 3 && s0 != s1 && s0 != s2 && s1 != s2;
            return ExpeditionRules.Rate(valid, fromBiome, favorite, three);
        }

        /// <summary>
        /// « Équipe conseillée »: the best rated team among the free animals. The rating only reads each animal's species
        /// (its biome and family) and whether the species differ, so the animals are grouped in four classes (from the
        /// destination's biome or not, of its favourite family or not) and every split of the team over the classes is
        /// scored; the best one is filled with distinct species first.
        /// </summary>
        public int SuggestTeam(ContentId destination, uint[] into)
        {
            if (into == null || into.Length == 0)
                return 0;
            int index = ExpeditionCatalog.IndexOf(destination);
            var info = index >= 0 ? ExpeditionCatalog.All[index] : null;
            var collection = _ctx.State.Collection;
            EnsureScratch(collection.Count);
            Array.Clear(_classAnimals, 0, 4);
            Array.Clear(_classSpecies, 0, 4);
            int free = 0;
            for (int i = 0; i < collection.Count; i++)
            {
                ref readonly var animal = ref collection[i];
                if (_state.IsAway(animal.InstanceId) || !_ctx.Content.Species.TryGet(animal.Species, out var definition))
                    continue;
                bool inBiome = info != null && !info.IsEvent && definition.Biome == info.Destination.Biome;
                bool fav = info != null && (info.Destination.FavoriteFamilies & ExpeditionRules.MaskOf(definition.RigFamily)) != 0;
                byte c = (byte)((inBiome ? 2 : 0) + (fav ? 1 : 0));
                bool first = true;
                for (int j = 0; j < free; j++)
                {
                    if (_firstOfSpecies[j] && collection.TryGet(_freeIds[j], out var other) && other.Species == animal.Species)
                    {
                        first = false;
                        break;
                    }
                }
                _freeIds[free] = animal.InstanceId;
                _freeClass[free] = c;
                _firstOfSpecies[free] = first;
                _classAnimals[c]++;
                if (first)
                    _classSpecies[c]++;
                free++;
            }
            int size = Math.Min(Math.Min(ExpeditionState.MaxTeam, into.Length), free);
            if (size <= 0)
                return 0;

            int bestScore = -1;
            bool bestDistinct = false;
            for (int a = 0; a <= size; a++)
            for (int b = 0; a + b <= size; b++)
            for (int c = 0; a + b + c <= size; c++)
            {
                int d = size - a - b - c;
                _pick[0] = a;
                _pick[1] = b;
                _pick[2] = c;
                _pick[3] = d;
                bool any = true, distinct = size == 3;
                for (int k = 0; k < 4; k++)
                {
                    any &= _classAnimals[k] >= _pick[k];
                    distinct &= _classSpecies[k] >= _pick[k];
                }
                if (!any)
                    continue;
                int score = size * ExpeditionRules.PointsPerAnimal + (c + d) * ExpeditionRules.PointsFromBiome
                            + (b + d > 0 ? ExpeditionRules.PointsFavoriteFamily : 0) + (distinct ? ExpeditionRules.PointsThreeSpecies : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDistinct = distinct;
                    Array.Copy(_pick, _bestPick, 4);
                }
            }

            int written = 0;
            for (int k = 0; k < 4; k++)
            {
                int want = _bestPick[k];
                // Distinct species first (one animal of each), then any animal of the class.
                for (int pass = 0; pass < 2 && want > 0; pass++)
                {
                    for (int i = 0; i < free && want > 0; i++)
                    {
                        if (_freeClass[i] != k || Contains(into, written, _freeIds[i]))
                            continue;
                        if (pass == 0 && !_firstOfSpecies[i])
                            continue;
                        if (pass == 1 && bestDistinct)
                            break;
                        into[written++] = _freeIds[i];
                        want--;
                    }
                }
            }
            return written;
        }

        public double DurationSeconds(ExpeditionDuration duration)
        {
            if (FirstStrollNext)
                return Math.Max(1.0, Config.FirstStrollSeconds);
            double seconds = Config.Hours(duration) * 3600.0;
            if (Config.WeekendActive)
                seconds *= Math.Max(0.1, Math.Min(1.0, Config.WeekendFactor));
            // The Essence tree's « quick trips »: durations × (1 − value), never under a quarter.
            seconds *= Math.Max(0.25, 1.0 - Math.Max(0.0, _ctx.Essence(EssenceEffect.ExpeditionDuration)));
            return Math.Max(1.0, seconds);
        }

        public bool FirstStrollNext => !_state.FirstStrollDone;

        public int DiscoveryGuaranteeIn => Math.Max(1, Config.DiscoveryGuarantee - _state.LongWithoutEgg);

        public void Preview(ContentId destination, ExpeditionDuration duration, byte stars, out ExpeditionLoot preview)
        {
            preview = default;
            int index = ExpeditionCatalog.IndexOf(destination);
            if (index < 0)
                return;
            var info = ExpeditionCatalog.All[index];
            bool first = FirstStrollNext;
            var column = first ? ExpeditionDuration.OneHour : duration;
            var context = LootContext(info);
            ExpeditionRules.Preview(Config, info, column, stars, in context, GuaranteeDue(column, first), out preview);
        }

        public ExpeditionStartResult Start(int slot, ContentId destination, ExpeditionDuration duration, uint[] team, int count) =>
            StartTeam(slot, destination, duration, team, count, -1);

        // ------------------------------------------------------------------------------------------- challenges

        public int ChallengeCount => ExpeditionCatalog.ChallengeCount;

        public bool IsChallengeOpen(int challenge)
        {
            if (!IsOpen || (uint)challenge >= (uint)ExpeditionCatalog.ChallengeCount)
                return false;
            for (int i = 0; i < ExpeditionCatalog.Count; i++)
                if (ChallengeAllows(challenge, ExpeditionCatalog.All[i].Id))
                    return true;
            return false;
        }

        public ExpeditionDuration ChallengeDuration(int challenge) =>
            (uint)challenge < (uint)ExpeditionCatalog.ChallengeCount ? ExpeditionCatalog.Challenges[challenge].Duration : ExpeditionDuration.OneHour;

        public bool ChallengeAllows(int challenge, ContentId destination)
        {
            if ((uint)challenge >= (uint)ExpeditionCatalog.ChallengeCount)
                return false;
            int index = ExpeditionCatalog.IndexOf(destination);
            if (index < 0)
                return false;
            var info = ExpeditionCatalog.All[index];
            if (!ExpeditionCatalog.IsOpen(info, _ctx.Progression))
                return false;
            ref readonly var rule = ref ExpeditionCatalog.Challenges[challenge];
            switch (rule.Target)
            {
                case ExpeditionChallengeTarget.Named: return info.Id == rule.Destination;
                case ExpeditionChallengeTarget.AnyBiome: return !info.IsEvent;
                case ExpeditionChallengeTarget.NewestBiome:
                    var newest = _ctx.Progression?.Store.MostAdvancedUnlocked() ?? ActivityCatalog.Meadow;
                    return !info.IsEvent && info.Destination.Biome == newest;
                default: return true;
            }
        }

        public bool ChallengeAccepts(int challenge, ContentId destination, uint[] team, int count)
        {
            if ((uint)challenge >= (uint)ExpeditionCatalog.ChallengeCount || team == null)
                return false;
            int n = Math.Min(count, team.Length);
            if (n < 1 || n > ExpeditionState.MaxTeam)
                return false;
            int index = ExpeditionCatalog.IndexOf(destination);
            var destinationBiome = index >= 0 ? ExpeditionCatalog.All[index].Destination.Biome : default;
            ref readonly var rule = ref ExpeditionCatalog.Challenges[challenge];
            int ofBiome = 0, ofFamily = 0, fromDestination = 0;
            ContentId s0 = default, s1 = default, s2 = default, b0 = default, b1 = default, b2 = default;
            RigFamily f0 = default, f1 = default, f2 = default;
            for (int i = 0; i < n; i++)
            {
                if (!TrySpecies(team[i], out var species, out var definition) || Duplicate(team, i))
                    return false;
                if (rule.Biome.IsValid && definition.Biome == rule.Biome)
                    ofBiome++;
                if ((rule.Family & ExpeditionRules.MaskOf(definition.RigFamily)) != 0)
                    ofFamily++;
                if (destinationBiome.IsValid && definition.Biome == destinationBiome)
                    fromDestination++;
                if (i == 0) { s0 = species; b0 = definition.Biome; f0 = definition.RigFamily; }
                else if (i == 1) { s1 = species; b1 = definition.Biome; f1 = definition.RigFamily; }
                else { s2 = species; b2 = definition.Biome; f2 = definition.RigFamily; }
            }
            switch (rule.Rule)
            {
                case ExpeditionChallengeRule.BiomeAnimals: return ofBiome >= rule.Count;
                case ExpeditionChallengeRule.AnyOfFamily: return ofFamily >= Math.Max(1, rule.Count);
                case ExpeditionChallengeRule.TeamOfThree: return n == 3;
                case ExpeditionChallengeRule.ThreeOfFamily: return n == 3 && ofFamily == 3;
                case ExpeditionChallengeRule.ThreeFamilies: return n == 3 && f0 != f1 && f0 != f2 && f1 != f2;
                case ExpeditionChallengeRule.ThreeFromDestinationBiome: return n == 3 && fromDestination == 3;
                case ExpeditionChallengeRule.ThreeBiomes:
                    return n == 3 && s0 != s1 && s0 != s2 && s1 != s2 && b0 != b1 && b0 != b2 && b1 != b2;
                default: return true;
            }
        }

        public int ChallengeThreshold(int challenge, int stars) =>
            (uint)challenge < (uint)ExpeditionCatalog.ChallengeCount ? ExpeditionCatalog.Challenges[challenge].Threshold(stars) : 0;

        public byte ChallengeStars(int challenge)
        {
            if ((uint)challenge >= (uint)ExpeditionCatalog.ChallengeCount)
                return 0;
            int slot = ActivityCatalog.SlotOf(ActivityId.Expeditions);
            return _ctx.Activities.ChallengeStars[ActivityState.ChallengeIndex(slot, challenge)];
        }

        public ExpeditionStartResult StartChallenge(int slot, int challenge, ContentId destination, uint[] team, int count)
        {
            if (!IsOpen)
                return ExpeditionStartResult.NotOpen;
            if (!IsChallengeOpen(challenge) || !ChallengeAllows(challenge, destination))
                return ExpeditionStartResult.DestinationLocked;
            if (team != null && count >= 1 && count <= ExpeditionState.MaxTeam && !ChallengeAccepts(challenge, destination, team, count))
                return ExpeditionStartResult.AnimalUnavailable;
            return StartTeam(slot, destination, ExpeditionCatalog.Challenges[challenge].Duration, team!, count, challenge);
        }

        // ------------------------------------------------------------------------------------------- the return

        public bool TryCollect(int slot, out ExpeditionReturn welcome)
        {
            welcome = default;
            long now = NowMs;
            if (StateAt(slot, now) != ExpeditionSlotState.Back)
                return false;
            var record = _state.Slots[slot];
            var loot = record.Loot;
            welcome = new ExpeditionReturn
            {
                Slot = slot,
                Destination = record.Destination,
                Duration = record.Duration,
                Stars = record.Stars,
                TeamSize = Math.Min(ExpeditionState.MaxTeam, (int)record.TeamSize),
                Animal0 = record.Animal0,
                Animal1 = record.Animal1,
                Animal2 = record.Animal2,
                Variant0 = record.Variant0,
                Variant1 = record.Variant1,
                Variant2 = record.Variant2,
                Species0 = record.Species0,
                Species1 = record.Species1,
                Species2 = record.Species2,
                Loot = loot,
                FirstStroll = record.FirstStroll,
                Seed = ExpeditionRules.PostcardSeed(record.Seed),
            };

            // Light (minutes × the production of now), treats, tickets, the Discovery egg and the sticker count.
            var bundle = new RewardBundle
            {
                LightMinutes = loot.LightMinutes,
                Treat = loot.Treat,
                Treats = loot.Treats,
                DiscoveryEggs = loot.DiscoveryEggs,
                Stickers = loot.Stickers,
            };
            var granted = _ctx.Granter.Grant(in bundle, EconomyReason.Expedition, DiscoverySource.Expedition);
            welcome.Light = granted.Light;
            // Tickets go straight into the activity pool (analytics 114 with their source).
            if (loot.Tickets > 0)
                _activities.Tickets.Grant(loot.Tickets, TicketSource.Expedition);

            // Seeds and ingredients into the inventory (the Garden and the Bakery take them from there).
            var inventory = _ctx.State.Inventory;
            for (int i = 0; i < loot.SeedKinds && i < 4; i++)
                if (loot.SeedsAt(i) > 0 && loot.SeedAt(i).IsValid)
                    inventory.Add(loot.SeedAt(i), loot.SeedsAt(i));
            if (loot.Ingredients0 > 0 && loot.Ingredient0.IsValid)
                inventory.Add(loot.Ingredient0, loot.Ingredients0);
            if (loot.Ingredients1 > 0 && loot.Ingredient1.IsValid)
                inventory.Add(loot.Ingredient1, loot.Ingredients1);

            // Bond for each animal still in the refuge, the destination biome's mastery, the event currency.
            var bond = _ctx.Bond;
            if (bond != null && loot.BondPoints > 0)
                for (int i = 0; i < welcome.TeamSize; i++)
                    bond.AddBond(record.AnimalAt(i), loot.BondPoints, BondSource.Expedition);
            int index = ExpeditionCatalog.IndexOf(record.Destination);
            var biome = index >= 0 && !ExpeditionCatalog.All[index].IsEvent ? ExpeditionCatalog.All[index].Destination.Biome : NewestBiome();
            if (loot.MasteryXp > 0 && biome.IsValid)
                _ctx.Progression?.Mastery.AddXp(biome, loot.MasteryXp);
            // Species mastery, once per species of the team (two rabbits give it once), from the species at departure.
            var speciesMastery = _ctx.SpeciesMastery;
            if (speciesMastery != null && Config.SpeciesXp > 0)
            {
                for (int i = 0; i < welcome.TeamSize; i++)
                {
                    var species = welcome.SpeciesAt(i);
                    bool seen = false;
                    for (int j = 0; j < i && !seen; j++)
                        seen = welcome.SpeciesAt(j) == species;
                    if (species.IsValid && !seen)
                        speciesMastery.AddXp(species, MasteryReason.Expedition, Config.SpeciesXp);
                }
            }
            if (loot.EventCurrency > 0)
                _activities.Events?.GrantEventCurrency(loot.EventCurrency, ActivityId.Expeditions);

            // The postcard: the first of its destination is kept for ever.
            bool first = !_state.HasFirstPostcard(record.Destination);
            int id = Math.Max(1, _state.NextPostcardId);
            _state.NextPostcardId = id + 1;
            _state.AddPostcard(new PostcardRecord
            {
                Id = id, Destination = record.Destination, UnixDay = (int)(now / 86_400_000L), Rendered = false, FirstOfDestination = first,
            });
            welcome.PostcardId = id;
            welcome.FirstFromDestination = first;

            // JRN-026: a destination brought back for the first time this journal week.
            bool newPlace = false;
            int week = _ctx.State.DailyVisit.JournalStartKey;
            if (_state.JournalWeekKey != week)
            {
                _state.JournalWeekKey = week;
                _state.JournalPlacesMask = 0;
            }
            if (index >= 0 && index < 64)
            {
                ulong bit = 1UL << index;
                newPlace = (_state.JournalPlacesMask & bit) == 0;
                _state.JournalPlacesMask |= bit;
            }

            _state.Completed++;
            _state.Slots[slot] = default;
            _seen[slot] = OwnedOrLocked(slot);
            _ctx.RequestSave(SaveUrgency.Priority);

            var info = new ExpeditionWelcomeInfo
            {
                Return = welcome,
                Long = !record.FirstStroll && ExpeditionRules.IsLong(record.Duration, Config),
                NewPlaceThisWeek = newPlace,
                RewardMask = ExpeditionRules.RewardMask(in loot),
            };
            Welcomed?.Invoke(in info);
            SlotChanged?.Invoke(slot);
            return true;
        }

        public void PostcardRendered(int postcardId)
        {
            var cards = _state.Postcards;
            for (int i = 0; i < _state.PostcardCount; i++)
            {
                if (cards[i].Id != postcardId || cards[i].Rendered)
                    continue;
                cards[i].Rendered = true;
                _ctx.RequestSave(SaveUrgency.Normal);
                return;
            }
        }

        public int PostcardCount => _state.PostcardCount;

        public int PostcardAt(int index) =>
            (uint)index < (uint)_state.PostcardCount ? _state.Postcards[_state.PostcardCount - 1 - index].Id : 0;

        /// <summary>The record of a postcard by id (false: not kept).</summary>
        public bool TryGetPostcard(int postcardId, out PostcardRecord record)
        {
            for (int i = 0; i < _state.PostcardCount; i++)
            {
                if (_state.Postcards[i].Id != postcardId)
                    continue;
                record = _state.Postcards[i];
                return true;
            }
            record = default;
            return false;
        }

        // ------------------------------------------------------------------------------------------- the frame

        /// <summary>
        /// Each frame (cheap, no allocation): a team whose return time came raises <see cref="SlotChanged"/> once; a team
        /// whose wait grew past its duration (a clock set back) gets its times moved to now, so it never waits longer than
        /// its duration.
        /// </summary>
        public void Tick()
        {
            long now = NowMs;
            for (int s = 0; s < ExpeditionState.MaxSlots; s++)
            {
                ref var record = ref _state.Slots[s];
                if (record.Active)
                {
                    long total = record.ReturnUnixMs - record.StartUnixMs;
                    if (total < 0L)
                    {
                        record.ReturnUnixMs = record.StartUnixMs;
                        total = 0L;
                    }
                    if (now < record.StartUnixMs)
                    {
                        // The clock went back before the departure: the wait starts again from now, never longer.
                        record.StartUnixMs = now;
                        record.ReturnUnixMs = now + total;
                        _ctx.RequestSave(SaveUrgency.Normal);
                    }
                }
                var state = StateAt(s, now);
                if (!_seenReady)
                {
                    _seen[s] = state;
                    continue;
                }
                if (_seen[s] == state)
                    continue;
                _seen[s] = state;
                SlotChanged?.Invoke(s);
            }
            _seenReady = true;
        }

        // ------------------------------------------------------------------------------------------- departure

        private ExpeditionStartResult StartTeam(int slot, ContentId destination, ExpeditionDuration duration, uint[] team, int count, int challenge)
        {
            if (!IsOpen)
                return ExpeditionStartResult.NotOpen;
            long now = NowMs;
            if (!IsOwned(slot) || StateAt(slot, now) != ExpeditionSlotState.Empty)
                return ExpeditionStartResult.SlotNotEmpty;
            if (team == null || count < 1 || team.Length < 1)
                return ExpeditionStartResult.NoAnimal;
            if (count > ExpeditionState.MaxTeam)
                return ExpeditionStartResult.TooManyAnimals;
            if (count > team.Length)
                return ExpeditionStartResult.NoAnimal;
            for (int i = 0; i < count; i++)
                if (team[i] == 0 || !_ctx.State.Collection.TryGetIndex(team[i], out _) || _state.IsAway(team[i]) || Duplicate(team, i))
                    return ExpeditionStartResult.AnimalUnavailable;
            int index = ExpeditionCatalog.IndexOf(destination);
            if (index < 0 || !ExpeditionCatalog.IsOpen(ExpeditionCatalog.All[index], _ctx.Progression))
                return ExpeditionStartResult.DestinationLocked;
            if ((uint)duration > (uint)ExpeditionDuration.TwelveHours)
                duration = ExpeditionDuration.OneHour;

            var info = ExpeditionCatalog.All[index];
            bool firstStroll = FirstStrollNext;
            var column = firstStroll ? ExpeditionDuration.OneHour : duration;
            double seconds = DurationSeconds(duration);
            var rating = Rate(destination, team, count);

            // The Discovery egg guarantee advances at departure, with the draw (section 7.1).
            bool isLong = !firstStroll && ExpeditionRules.IsLong(column, Config);
            if (isLong)
                _state.LongWithoutEgg++;
            bool guaranteed = isLong && _state.LongWithoutEgg >= Math.Max(1, Config.DiscoveryGuarantee);

            uint seed = SeedHash.Mix(_ctx.ProfileSeed ^ (uint)_state.Started, 0x45585044u);
            _state.Started++;
            var context = LootContext(info);
            ExpeditionRules.Draw(Config, info, column, rating.Stars, in context, guaranteed, seed, out var loot);
            if (loot.DiscoveryEggs > 0)
                _state.LongWithoutEgg = 0;

            var record = new ExpeditionSlotRecord
            {
                Active = true,
                Destination = destination,
                Duration = column,
                TeamSize = (byte)count,
                StartUnixMs = now,
                ReturnUnixMs = now + (long)Math.Round(seconds * 1000.0),
                Stars = rating.Stars,
                Challenge = (sbyte)challenge,
                FirstStroll = firstStroll,
                Seed = seed,
                Loot = loot,
            };
            for (int i = 0; i < count; i++)
            {
                _ctx.State.Collection.TryGet(team[i], out var animal);
                record.SetMember(i, team[i], animal.Variant, animal.Species);
            }
            _state.Slots[slot] = record;
            _state.FirstStrollDone = true;

            byte challengeStars = 0, before = 0;
            if (challenge >= 0)
                RecordChallenge(challenge, rating.Points, out challengeStars, out before);

            _seen[slot] = ExpeditionSlotState.Away;
            _ctx.RequestSave(SaveUrgency.Priority);
            var started = new ExpeditionStartInfo
            {
                Slot = slot,
                Destination = destination,
                Duration = column,
                Seconds = seconds,
                TeamSize = count,
                Animal0 = record.Animal0,
                Animal1 = record.Animal1,
                Animal2 = record.Animal2,
                Points = rating.Points,
                Stars = rating.Stars,
                FirstStroll = firstStroll,
                Challenge = challenge,
                ChallengeStars = challengeStars,
                ChallengeStarsBefore = before,
            };
            Started?.Invoke(in started);
            SlotChanged?.Invoke(slot);
            return ExpeditionStartResult.Started;
        }

        /// <summary>
        /// A challenge's stars are recorded at departure (best kept, attempts counted); its first 3 stars give the challenge
        /// Gems once and count toward the challenge decorations, as every activity's (doc 01, section 12.0 « Défis »).
        /// </summary>
        private void RecordChallenge(int challenge, int points, out byte stars, out byte before)
        {
            var activities = _ctx.Activities;
            int cell = ActivityState.ChallengeIndex(ActivityCatalog.SlotOf(ActivityId.Expeditions), challenge);
            stars = ExpeditionRules.ChallengeStars(in ExpeditionCatalog.Challenges[challenge], points);
            before = activities.ChallengeStars[cell];
            if (activities.ChallengeAttempts[cell] < ushort.MaxValue)
                activities.ChallengeAttempts[cell]++;
            if (stars <= before)
                return;
            activities.ChallengeStars[cell] = stars;
            if (stars >= 3 && before < 3)
            {
                var config = _activities.Config;
                activities.ChallengesThreeStars++;
                var bundle = new RewardBundle { Gems = Math.Max(0, config.GemsChallenge) };
                int every = Math.Max(1, config.ChallengeDecorationEvery);
                if (activities.ChallengeDecorations < activities.ChallengesThreeStars / every)
                {
                    bundle.Decoration = ActivityRewardService.ChallengeDecoration(activities.ChallengeDecorations);
                    if (bundle.Decoration.IsValid)
                        activities.ChallengeDecorations++;
                }
                if (!bundle.IsEmpty)
                    _ctx.Granter.Grant(in bundle, EconomyReason.ActivityChallenge, DiscoverySource.Debug);
            }
            ChallengeImproved?.Invoke(challenge, stars, activities.ChallengeAttempts[cell]);
        }

        // ------------------------------------------------------------------------------------------- helpers

        private long NowMs => _ctx.Clock.UtcNow.ToUnixTimeMilliseconds();

        private bool Active(int slot) => (uint)slot < ExpeditionState.MaxSlots && _state.Slots[slot].Active;

        private ExpeditionSlotState StateAt(int slot, long now)
        {
            if ((uint)slot >= ExpeditionState.MaxSlots)
                return ExpeditionSlotState.Locked;
            ref readonly var record = ref _state.Slots[slot];
            if (record.Active)
                return now < record.ReturnUnixMs && Left(in record, now) > 0.0 ? ExpeditionSlotState.Away : ExpeditionSlotState.Back;
            return OwnedOrLocked(slot);
        }

        private ExpeditionSlotState OwnedOrLocked(int slot) => IsOwned(slot) ? ExpeditionSlotState.Empty : ExpeditionSlotState.Locked;

        /// <summary>Seconds left, clamped to [0, the team's total]: a clock set back never shows a longer wait.</summary>
        private static double Left(in ExpeditionSlotRecord record, long now)
        {
            long total = Math.Max(0L, record.ReturnUnixMs - record.StartUnixMs);
            long left = record.ReturnUnixMs - now;
            if (left <= 0L)
                return 0.0;
            return Math.Min(left, total) / 1000.0;
        }

        private bool GuaranteeDue(ExpeditionDuration column, bool firstStroll) =>
            !firstStroll && ExpeditionRules.IsLong(column, Config) && _state.LongWithoutEgg + 1 >= Math.Max(1, Config.DiscoveryGuarantee);

        private ExpeditionLootContext LootContext(ExpeditionDestinationInfo info)
        {
            var progression = _ctx.Progression;
            var line = info.SeedLine;
            ContentId treatBiome = info.Destination.Biome;
            if (info.IsEvent)
            {
                // Event destinations: the treats and seeds of the player's newest biome (section 7.5).
                treatBiome = NewestBiome();
                int newest = ExpeditionCatalog.IndexOfBiome(treatBiome);
                line = newest >= 0 ? ExpeditionCatalog.All[newest].SeedLine : ExpeditionCatalog.All[0].SeedLine;
            }
            var config = _ctx.ProgressionConfig;
            var treat = config.TryGetBiome(treatBiome, out var economy) ? economy.Treat : config.Biomes[0].Treat;
            var events = _activities.Events;
            return new ExpeditionLootContext
            {
                Treat = treat,
                SeedLine = line,
                KnownPlants = ExpeditionRules.KnownPlants(line, progression),
                BakeryOpen = _ctx.BakeryOpen,
                DyesOpen = _ctx.GardenOpen && _activities.GardenLevel >= 4,
                EventRunning = events != null && events.EventRunning,
                LightBonus = _ctx.Essence(EssenceEffect.ActivityLight),
                MasteryXp = progression != null ? progression.Mastery.XpFor(BiomeXpSource.Expedition) : Config.MasteryXp,
            };
        }

        private ContentId NewestBiome() => _ctx.Progression?.Store.MostAdvancedUnlocked() ?? ActivityCatalog.Meadow;

        private bool TrySpecies(uint animal, out ContentId species, out SpeciesDefinition definition)
        {
            species = default;
            definition = null!;
            if (animal == 0 || !_ctx.State.Collection.TryGet(animal, out var instance))
                return false;
            species = instance.Species;
            return _ctx.Content.Species.TryGet(instance.Species, out definition);
        }

        private static bool Duplicate(uint[] team, int index)
        {
            for (int j = 0; j < index; j++)
                if (team[j] == team[index])
                    return true;
            return false;
        }

        private static bool Contains(uint[] team, int count, uint animal)
        {
            for (int i = 0; i < count; i++)
                if (team[i] == animal)
                    return true;
            return false;
        }

        private void EnsureScratch(int count)
        {
            if (_freeIds.Length >= count)
                return;
            int size = Math.Max(count, _freeIds.Length * 2);
            _freeIds = new uint[size];
            _freeClass = new byte[size];
            _firstOfSpecies = new bool[size];
        }

        private void Raise(int slot)
        {
            if ((uint)slot < ExpeditionState.MaxSlots)
                _seen[slot] = StateAt(slot, NowMs);
            SlotChanged?.Invoke(slot);
        }
    }
}
