#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The weekly journal (doc 01 Contenu, section 5): 7 cumulative goals from Monday 00:00 to the next Monday 00:00, the
    /// days left shown being the real ones.
    /// <list type="bullet">
    /// <item>Each goal reached: 5 Gems and 2 tickets (Light 15 min per ticket while the Play button does not exist). The
    /// journal complete (every goal claimed): 15 Gems and 1 Discovery egg. 50 Gems a week (section 5.1).</item>
    /// <item>Monday starts a new journal (« nouveau départ »): goals not reached cost nothing; goals reached but not
    /// claimed, and a complete journal not claimed, move into the carry-over, paid at the next opening, even weeks later.</item>
    /// <item>The draw (section 5.3): one goal per slot in the order Collection, Activités, Rendez-vous, Minuteurs, Soin,
    /// Refuge, Libre; candidates eligible now, of an open system, shipped in this build, never twice the same goal, at most
    /// 2 goals of a system; weight 1, × 0.3 when the goal was in the previous journal; the Free slot takes any eligible
    /// goal, × 3 for a system opened in the last 7 days; a slot without a candidate is filled by a Free draw. Targets are
    /// the refuge level's tier at the draw and never change afterwards. Seeded by the week: killing the app never rerolls.</item>
    /// <item>The first journal (section 5.4) opens on day 2 with refuge level 6 and lasts 4 to 10 days. Fixed content:
    /// JRN-001 (30), JRN-009 (20), JRN-021 (10), JRN-024 (5), JRN-031 (80), JRN-036 (6), JRN-034 (30); a closed system's
    /// goal is replaced by the normal draw of its slot. JRN-001 starts with the eggs hatched since install (at least 6)
    /// and JRN-021 with the wishes done (at least 2): « Déjà bien commencé ».</item>
    /// </list>
    /// Progress comes through <see cref="IDailyProgressHooks"/>, forwarded by <see cref="DailyVisitServices"/>; practice
    /// games count except for JRN-010 and JRN-014 (<see cref="JournalGoal.RewardedGamesOnly"/>). Hooks, the queries and
    /// <see cref="Ensure"/> on an ordinary day allocate nothing; the draw uses buffers allocated once.
    /// </summary>
    public sealed class WeeklyJournalService : IDailyProgressHooks
    {
        /// <summary>The id <see cref="Claimed"/> reports for the carry-over of past weeks (0 is the journal's completion).</summary>
        public const int CarryClaimId = -1;

        private const uint DrawSalt = 0x4A524E01u;
        private const int WeekDays = 7;
        private const int MaxPerSystem = 2;
        private const double PreviousJournalWeight = 0.3;
        private const double NewSystemWeight = 3.0;
        private const int NewSystemDays = 7;

        /// <summary>The first journal lasts at least this many days, else it runs to the following Monday.</summary>
        private const int FirstJournalMinDays = 4;

        private const int FirstHatchFloor = 6;
        private const int FirstWishFloor = 2;

        private static readonly JournalSlot[] SlotOrder =
        {
            JournalSlot.Collection, JournalSlot.Activities, JournalSlot.Rendezvous, JournalSlot.Timers, JournalSlot.Care,
            JournalSlot.Refuge, JournalSlot.Free,
        };

        /// <summary>The first journal's content, one per slot in <see cref="SlotOrder"/> (section 5.4), and its targets.</summary>
        private static readonly JournalGoalId[] FirstGoals =
        {
            JournalGoalId.HatchEggs, JournalGoalId.ActivityStars, JournalGoalId.Wishes, JournalGoalId.Expeditions,
            JournalGoalId.BondPoints, JournalGoalId.Upgrades, JournalGoalId.AnimalsTapped,
        };

        private static readonly int[] FirstTargets = { 30, 20, 10, 5, 80, 6, 30 };

        private readonly DailyContext _ctx;
        private readonly PlayerState _state;

        // Draw scratch, allocated once: the positions of the journal being drawn, the candidates of one slot, goals per system.
        private readonly ushort[] _posGoal = new ushort[DailyVisitState.MaxJournalGoals];
        private readonly int[] _posTarget = new int[DailyVisitState.MaxJournalGoals];
        private readonly JournalSlot[] _posSlot = new JournalSlot[DailyVisitState.MaxJournalGoals];
        private readonly JournalSlot[] _posPlaced = new JournalSlot[DailyVisitState.MaxJournalGoals];
        private readonly int[] _candidates = new int[WeeklyJournalCatalog.Count];
        private readonly double[] _weights = new double[WeeklyJournalCatalog.Count];
        private readonly int[] _perSystem = new int[DailyVisitState.MaxWishSystems];
        private readonly bool[] _systemOpen = new bool[DailyVisitState.MaxWishSystems];
        private int _positions;

        // What the eligibility reads, gathered once per draw.
        private int _tier = 1;
        private int _dayNumber;
        private int _missingVariants;
        private bool _speciesToFind;
        private bool _focusOffered;
        private bool _treatStandOpen;
        private int _expansionsLeft;

        public WeeklyJournalService(DailyContext ctx)
        {
            _ctx = ctx;
            _state = ctx.State;
        }

        /// <summary>A goal reached its target (its index in the journal). Raised once per goal.</summary>
        public event Action<int>? GoalReached;

        /// <summary>Something was paid: a goal (its JRN number), the completion (0) or the carry-over (<see cref="CarryClaimId"/>).</summary>
        public event Action<int, GrantedRewards>? Claimed;

        /// <summary>A new journal was drawn (true: the first one). « Un nouveau carnet t'attend » on Monday.</summary>
        public event Action<bool>? Started;

        private DailyVisitState Visit => _state.DailyVisit;

        private DailyVisitConfig Config => _ctx.Config;

        /// <summary>Refuge level 6 and day 2 (<see cref="RefugeFeature.WeeklyBook"/>), the welcome over, the flag on.</summary>
        public bool Unlocked => Config.JournalEnabled && _state.IsOnboardingDone && _ctx.Feature(RefugeFeature.WeeklyBook);

        public int GoalCount => Visit.GoalCount;

        /// <summary>First day key of the journal and the key it ends at (a Monday, excluded). 0: no journal yet.</summary>
        public int StartKey => Visit.JournalStartKey;

        public int EndKey => Visit.JournalEndKey;

        /// <summary>The current journal is the first one (« Ton carnet de la semaine »).</summary>
        public bool IsFirstJournal => Visit.JournalEndKey != 0 && !Visit.FirstJournalDone;

        /// <summary>Real days until the journal ends, today included (« Encore {n} jours »): 1 on its last day.</summary>
        public int DaysLeft
        {
            get
            {
                var visit = Visit;
                if (visit.JournalEndKey == 0 || _ctx.TodayKey <= 0)
                    return 0;
                return Math.Max(0, DailyDay.DaysBetween(_ctx.TodayKey, visit.JournalEndKey));
            }
        }

        public JournalGoalRecord Get(int i)
        {
            if ((uint)i >= (uint)Visit.GoalCount)
                throw new ArgumentOutOfRangeException(nameof(i));
            return Visit.Goals[i];
        }

        /// <summary>Goals reached (claimed or not), for « n/7 ».</summary>
        public int ReachedCount
        {
            get
            {
                var visit = Visit;
                int reached = 0;
                for (int i = 0; i < visit.GoalCount; i++)
                    if (visit.Goals[i].IsReached)
                        reached++;
                return reached;
            }
        }

        /// <summary>One of the two goals the first journal starts ahead (« Déjà bien commencé »).</summary>
        public bool HeadStart(int i)
        {
            if (!IsFirstJournal || (uint)i >= (uint)Visit.GoalCount)
                return false;
            var goal = (JournalGoalId)Visit.Goals[i].Goal;
            return (goal == JournalGoalId.HatchEggs && Config.JournalPrefilled >= 1) || (goal == JournalGoalId.Wishes && Config.JournalPrefilled >= 2);
        }

        public bool HasCarry
        {
            get
            {
                var visit = Visit;
                return visit.CarryGems > 0 || visit.CarryTickets > 0 || visit.CarryDiscovery > 0;
            }
        }

        /// <summary>Every goal claimed and the completion not claimed yet.</summary>
        public bool CanClaimComplete
        {
            get
            {
                var visit = Visit;
                int count = visit.GoalCount;
                if (count == 0 || visit.JournalCompleteClaimed)
                    return false;
                for (int i = 0; i < count; i++)
                    if (!visit.Goals[i].Claimed)
                        return false;
                return true;
            }
        }

        /// <summary>A goal, the completion or the carry-over can be claimed (the Today dot).</summary>
        public bool HasClaimable
        {
            get
            {
                if (HasCarry || CanClaimComplete)
                    return true;
                var visit = Visit;
                for (int i = 0; i < visit.GoalCount; i++)
                    if (visit.Goals[i].IsReached && !visit.Goals[i].Claimed)
                        return true;
                return false;
            }
        }

        // ------------------------------------------------------------------------------------------- days

        /// <summary>
        /// Called with today's key at each day change and on resume. Opens the first journal once unlocked; when the week
        /// is over, moves what was reached and not claimed into the carry-over, remembers the goals (× 0.3 next draw) and
        /// draws the journal of the current week (missed weeks are simply skipped). Nothing else, and no allocation, while
        /// the week runs.
        /// </summary>
        public void Ensure(int dayKey)
        {
            var visit = Visit;
            if (visit.JournalEndKey != 0 && dayKey < visit.JournalEndKey)
                return;
            if (dayKey <= 0 || !Unlocked)
                return;
            bool first = visit.JournalEndKey == 0;
            if (first)
                OpenFirst(dayKey);
            else
                Rollover(dayKey);
            _ctx.RequestSave(SaveUrgency.Normal);
            Started?.Invoke(first);
        }

        private void OpenFirst(int dayKey)
        {
            var visit = Visit;
            int nextMonday = DailyDay.AddDays(DailyDay.WeekStartKey(dayKey), WeekDays);
            // Fewer than 4 days before Monday: it runs to the following Monday (4 to 10 days in all).
            int end = DailyDay.DaysBetween(dayKey, nextMonday) < FirstJournalMinDays ? DailyDay.AddDays(nextMonday, WeekDays) : nextMonday;
            visit.JournalStartKey = dayKey;
            visit.JournalEndKey = end;
            visit.FirstJournalDone = false;
            visit.PreviousGoalCount = 0;
            Draw(dayKey, true);

            // Two goals start ahead, a real gift (section 5.4), never already reached.
            int prefills = Config.JournalPrefilled;
            if (prefills > 0 && HeadStartGoal(JournalGoalId.HatchEggs, (long)_state.Collection.NextInstanceId - 1, FirstHatchFloor))
                prefills--;
            if (prefills > 0)
                HeadStartGoal(JournalGoalId.Wishes, _state.DailyVisit.WishesCompleted, FirstWishFloor);
        }

        private bool HeadStartGoal(JournalGoalId id, long done, int floor)
        {
            var visit = Visit;
            for (int i = 0; i < visit.GoalCount; i++)
            {
                ref var record = ref visit.Goals[i];
                if (record.Goal != (ushort)id)
                    continue;
                long progress = Math.Max(floor, done);
                record.Progress = (int)Math.Max(0, Math.Min(progress, record.Target - 1L));
                return true;
            }
            return false;
        }

        private void Rollover(int dayKey)
        {
            var visit = Visit;
            CarryOver(visit);
            int count = Math.Min(visit.GoalCount, visit.PreviousGoals.Length);
            for (int i = 0; i < count; i++)
                visit.PreviousGoals[i] = visit.Goals[i].Goal;
            visit.PreviousGoalCount = (byte)count;
            visit.FirstJournalDone = true;
            int start = DailyDay.WeekStartKey(dayKey);
            visit.JournalStartKey = start;
            visit.JournalEndKey = DailyDay.AddDays(start, WeekDays);
            Draw(start, false);
        }

        /// <summary>Reached and unclaimed goals, and a complete journal not claimed, wait for the next opening (section 5.1).</summary>
        private void CarryOver(DailyVisitState visit)
        {
            int count = visit.GoalCount;
            int waiting = 0;
            bool allReached = count > 0;
            for (int i = 0; i < count; i++)
            {
                ref readonly var record = ref visit.Goals[i];
                if (!record.IsReached)
                    allReached = false;
                else if (!record.Claimed)
                    waiting++;
            }
            if (waiting > 0)
            {
                visit.CarryGems = AddClamped(visit.CarryGems, waiting * Math.Max(0L, Config.JournalGoalGems));
                visit.CarryTickets = AddClamped(visit.CarryTickets, waiting * (long)Math.Max(0, Config.JournalGoalTickets));
            }
            if (allReached && !visit.JournalCompleteClaimed)
            {
                visit.CarryGems = AddClamped(visit.CarryGems, Math.Max(0L, Config.JournalCompleteGems));
                visit.CarryDiscovery = AddClamped(visit.CarryDiscovery, 1);
            }
        }

        private static int AddClamped(int value, long add) => (int)Math.Min(int.MaxValue, Math.Max(0L, value + add));

        // ------------------------------------------------------------------------------------------- draw

        /// <summary>
        /// Section 5.3. The first journal's fixed goals are placed first; then each slot draws among its own goals in slot
        /// order; then the Free slot and every slot left without a candidate draw as Free, in order (a Free draw never takes
        /// the goal a later slot needed). <see cref="DailyVisitConfig.JournalGoals"/> above 7 adds Free slots; below 7 the
        /// order is cut from its end, the Free slot first. Goals are kept in slot order, the Free draws last.
        /// </summary>
        private void Draw(int seedKey, bool first)
        {
            var visit = Visit;
            int n = Math.Max(1, Math.Min(Config.JournalGoals, DailyVisitState.MaxJournalGoals));
            _positions = n;
            PrepareFacts();
            Array.Clear(_perSystem, 0, _perSystem.Length);
            for (int p = 0; p < n; p++)
            {
                _posGoal[p] = 0;
                _posTarget[p] = 0;
                _posSlot[p] = p < SlotOrder.Length ? SlotOrder[p] : JournalSlot.Free;
                _posPlaced[p] = _posSlot[p];
            }
            var rng = new DeterministicRandom(_ctx.Seed(seedKey, DrawSalt));

            if (first)
            {
                for (int p = 0; p < n && p < FirstGoals.Length; p++)
                    if (WeeklyJournalCatalog.TryGet(FirstGoals[p], out var fixedGoal) && CanTake(fixedGoal, FirstTargets[p]))
                        Place(p, fixedGoal, FirstTargets[p], _posSlot[p]);
            }
            for (int p = 0; p < n; p++)
                if (_posGoal[p] == 0 && _posSlot[p] != JournalSlot.Free)
                    DrawSlot(p, _posSlot[p], ref rng);
            for (int p = 0; p < n; p++)
                if (_posGoal[p] == 0)
                    DrawSlot(p, JournalSlot.Free, ref rng);

            int count = 0;
            for (int p = 0; p < n; p++)
                if (_posGoal[p] != 0 && _posPlaced[p] != JournalSlot.Free)
                    Write(visit, count++, p);
            for (int p = 0; p < n; p++)
                if (_posGoal[p] != 0 && _posPlaced[p] == JournalSlot.Free)
                    Write(visit, count++, p);
            for (int i = count; i < visit.Goals.Length; i++)
                visit.Goals[i] = default;
            visit.GoalCount = (byte)count;
            visit.JournalCompleteClaimed = false;
        }

        private void Write(DailyVisitState visit, int index, int position)
        {
            visit.Goals[index] = new JournalGoalRecord
            {
                Goal = _posGoal[position],
                Slot = (byte)_posPlaced[position],
                Target = _posTarget[position],
                Progress = 0,
                Claimed = false,
            };
        }

        private bool DrawSlot(int position, JournalSlot slot, ref DeterministicRandom rng)
        {
            bool free = slot == JournalSlot.Free;
            var all = WeeklyJournalCatalog.All;
            int count = 0;
            double total = 0.0;
            for (int i = 0; i < all.Length; i++)
            {
                var goal = all[i];
                if (!free && goal.Slot != slot)
                    continue;
                if (!CanTake(goal, goal.Target(_tier)))
                    continue;
                double weight = Weight(goal, free);
                _candidates[count] = i;
                _weights[count] = weight;
                total += weight;
                count++;
            }
            if (count == 0 || total <= 0.0)
                return false;
            double roll = rng.NextDouble() * total;
            int chosen = count - 1;
            for (int k = 0; k < count; k++)
            {
                roll -= _weights[k];
                if (roll < 0.0)
                {
                    chosen = k;
                    break;
                }
            }
            var pick = all[_candidates[chosen]];
            Place(position, pick, pick.Target(_tier), slot);
            return true;
        }

        /// <summary>Shipped, of an open system that holds fewer than 2 goals, not in the journal yet, eligible for this target.</summary>
        private bool CanTake(in JournalGoal goal, int target)
        {
            if (!goal.ShippedInThisBuild)
                return false;
            int system = (int)goal.System;
            if (system >= _systemOpen.Length || !_systemOpen[system] || _perSystem[system] >= MaxPerSystem)
                return false;
            for (int p = 0; p < _positions; p++)
                if (_posGoal[p] == (ushort)goal.Id)
                    return false;
            return IsEligible(goal.Id, target);
        }

        private void Place(int position, in JournalGoal goal, int target, JournalSlot placed)
        {
            _posGoal[position] = (ushort)goal.Id;
            _posTarget[position] = Math.Max(1, target);
            _posPlaced[position] = placed;
            _perSystem[(int)goal.System]++;
        }

        private double Weight(in JournalGoal goal, bool freeSlot)
        {
            double weight = 1.0;
            if (WasInPreviousJournal(goal.Id))
                weight *= PreviousJournalWeight;
            if (freeSlot && IsNewSystem(goal.System))
                weight *= NewSystemWeight;
            return weight;
        }

        private bool WasInPreviousJournal(JournalGoalId id)
        {
            var visit = Visit;
            int count = Math.Min(visit.PreviousGoalCount, visit.PreviousGoals.Length);
            for (int i = 0; i < count; i++)
                if (visit.PreviousGoals[i] == (ushort)id)
                    return true;
            return false;
        }

        /// <summary>Opened in the last 7 days (the day number each system was first seen open, kept by the wishes).</summary>
        private bool IsNewSystem(WishSystem system)
        {
            var days = Visit.SystemUnlockDay;
            int index = (int)system;
            if (index <= 0 || index >= days.Length)
                return false;
            int opened = days[index];
            return opened > 0 && _dayNumber >= opened && _dayNumber - opened < NewSystemDays;
        }

        /// <summary>
        /// The weight a goal would have in a draw now, before the journal's own rules (no twice the same goal, 2 per system):
        /// 0 when it cannot be drawn (not shipped, system closed, not eligible, a Free-slot goal outside the Free slot).
        /// For tests and the debug panel.
        /// </summary>
        public double DrawWeight(JournalGoalId id, bool freeSlot)
        {
            if (!WeeklyJournalCatalog.TryGet(id, out var goal))
                return 0.0;
            PrepareFacts();
            int system = (int)goal.System;
            if (!goal.ShippedInThisBuild || system >= _systemOpen.Length || !_systemOpen[system] || !IsEligible(goal.Id, goal.Target(_tier)))
                return 0.0;
            if (!freeSlot && goal.Slot == JournalSlot.Free)
                return 0.0;
            return Weight(goal, freeSlot);
        }

        // ------------------------------------------------------------------------------------------- eligibility

        /// <summary>Reads once per draw what the goals' eligibility needs (section 5.2, column « Éligibilité »).</summary>
        private void PrepareFacts()
        {
            _tier = WeeklyJournalCatalog.TierOf(_ctx.RefugeLevel);
            _dayNumber = _ctx.DayNumber;
            _missingVariants = MissingVariantsInOpenEggs();
            _speciesToFind = AnySpeciesToFind();
            _focusOffered = AnyFocusEggOffered();
            _expansionsLeft = ExpansionsLeft();
            // The treat stand is in the Shop (doc 01, section 0: « Boutique et épicerie »).
            _treatStandOpen = _ctx.Feature(RefugeFeature.Shop);

            // Systems this build has (doc 01, section 2.2); the others stay closed until their task ships them.
            Array.Clear(_systemOpen, 0, _systemOpen.Length);
            bool welcomeOver = _state.IsOnboardingDone;
            _systemOpen[(int)WishSystem.Hatching] = welcomeOver;
            _systemOpen[(int)WishSystem.DiscoveriesAndMastery] = welcomeOver;
            _systemOpen[(int)WishSystem.CuddlesAndTreats] = welcomeOver;
            _systemOpen[(int)WishSystem.Upgrades] = _ctx.Feature(RefugeFeature.UpgradeButton);
            _systemOpen[(int)WishSystem.Expansions] = _ctx.Feature(RefugeFeature.ZoneSigns);
            _systemOpen[(int)WishSystem.Rendezvous] = _ctx.TodayOpen;
            // T38: Grooming (refuge level 3 and 5 animals) once the build has activities.
            _systemOpen[(int)WishSystem.Grooming] = welcomeOver && Config.ActivitiesAvailable && _ctx.Feature(RefugeFeature.Grooming);
        }

        private bool IsEligible(JournalGoalId id, int target)
        {
            switch (id)
            {
                case JournalGoalId.HatchEggs:
                case JournalGoalId.MasteryLevels:
                case JournalGoalId.BondPoints:
                case JournalGoalId.AnimalsTapped:
                case JournalGoalId.Upgrades:
                case JournalGoalId.Grooming:
                    return true;
                case JournalGoalId.DiscoverVariants:
                    return _missingVariants >= target;
                case JournalGoalId.NewSpecies:
                    return _speciesToFind;
                case JournalGoalId.PreciousEggs:
                    return _focusOffered;
                case JournalGoalId.Wishes:
                    return Config.WishesEnabled;
                case JournalGoalId.DailyGifts:
                    return Config.GiftEnabled;
                case JournalGoalId.CalendarDays:
                    return Config.CalendarEnabled;
                case JournalGoalId.TreatsGiven:
                    return _treatStandOpen;
                case JournalGoalId.Expansions:
                    return _expansionsLeft >= target;
                default:
                    return false;
            }
        }

        /// <summary>Base (non-secret) variants not discovered yet whose species an unlocked egg can give.</summary>
        private int MissingVariantsInOpenEggs()
        {
            var variants = _ctx.Content.Variants;
            var discovered = _state.Collection.DiscoveredVariants;
            int missing = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (!variant.IsSecret && !discovered.Contains(variant.Id) && InOpenEggPool(variant.Species))
                    missing++;
            }
            return missing;
        }

        private bool InOpenEggPool(ContentId species)
        {
            var eggs = _ctx.Content.Eggs;
            for (int i = 0; i < eggs.Count; i++)
            {
                var egg = eggs[i];
                if (egg.Tier == EggTier.Discovery || !EggOpen(egg))
                    continue;
                var pool = egg.Pool;
                for (int k = 0; k < pool.Length; k++)
                    if (pool[k].Species == species)
                        return true;
            }
            return false;
        }

        /// <summary>A normal species not discovered yet in an open biome.</summary>
        private bool AnySpeciesToFind()
        {
            var species = _ctx.Content.Species;
            var discovered = _state.Collection.DiscoveredSpecies;
            for (int i = 0; i < species.Count; i++)
            {
                var definition = species[i];
                if (!definition.IsSecret && !discovered.Contains(definition.Id) && BiomeOpen(definition.Biome))
                    return true;
            }
            return false;
        }

        private bool AnyFocusEggOffered()
        {
            var eggs = _ctx.Content.Eggs;
            for (int i = 0; i < eggs.Count; i++)
                if (eggs[i].Tier == EggTier.Focus && EggOpen(eggs[i]))
                    return true;
            return false;
        }

        /// <summary>The egg can be chosen: its biome open and its biome mastery reached (Focus: 3).</summary>
        private bool EggOpen(EggDefinition egg)
        {
            if (!BiomeOpen(egg.Biome))
                return false;
            if (_ctx.Hatch != null)
                return _ctx.Hatch.IsOffered(egg.Id);
            return egg.UnlockBiomeMastery <= 0 || BiomeProgressOf(egg.Biome).MasteryLevel >= egg.UnlockBiomeMastery;
        }

        /// <summary>Zones still to open by purchase in the open biomes.</summary>
        private int ExpansionsLeft()
        {
            var zones = _ctx.ProgressionConfig.Zones;
            int left = 0;
            for (int i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone.Role == ZoneRole.Expansion && BiomeOpen(zone.Biome) && !BiomeProgressOf(zone.Biome).IsZoneOpen(zone.Index))
                    left++;
            }
            return left;
        }

        /// <summary>As the hatch reads it: a profile with no biome recorded yet (tests, before the progression starts) has them all.</summary>
        private bool BiomeOpen(ContentId biome)
        {
            var biomes = _state.World.Biomes;
            if (biomes.Count == 0)
                return true;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i].Biome == biome)
                    return biomes[i].Unlocked;
            return false;
        }

        private BiomeProgress BiomeProgressOf(ContentId biome)
        {
            var biomes = _state.World.Biomes;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i].Biome == biome)
                    return biomes[i];
            return new BiomeProgress { Biome = biome };
        }

        // ------------------------------------------------------------------------------------------- claims

        /// <summary>A reached goal: 5 Gems and 2 tickets (or Light 15 min a ticket while the Play button is closed).</summary>
        public bool TryClaimGoal(int i, out GrantedRewards granted)
        {
            granted = default;
            var visit = Visit;
            if ((uint)i >= (uint)visit.GoalCount)
                return false;
            ref var record = ref visit.Goals[i];
            if (record.Claimed || !record.IsReached)
                return false;
            record.Claimed = true;
            int goal = record.Goal;
            granted = _ctx.Granter.Grant(Bundle(Config.JournalGoalGems, Config.JournalGoalTickets, 0), EconomyReason.WeeklyJournal,
                DiscoverySource.WeeklyBook);
            Claimed?.Invoke(goal, granted);
            return true;
        }

        /// <summary>Every goal claimed: 15 Gems and 1 Discovery egg (« Carnet complet »).</summary>
        public bool TryClaimComplete(out GrantedRewards granted)
        {
            granted = default;
            if (!CanClaimComplete)
                return false;
            Visit.JournalCompleteClaimed = true;
            granted = _ctx.Granter.Grant(Bundle(Config.JournalCompleteGems, 0, 1), EconomyReason.WeeklyJournal, DiscoverySource.WeeklyBook);
            Claimed?.Invoke(0, granted);
            return true;
        }

        /// <summary>What past weeks left reached and unclaimed, paid at the next opening of the journal.</summary>
        public bool ClaimCarry(out GrantedRewards granted)
        {
            granted = default;
            if (!HasCarry)
                return false;
            var visit = Visit;
            var bundle = Bundle(visit.CarryGems, visit.CarryTickets, visit.CarryDiscovery);
            visit.CarryGems = 0;
            visit.CarryTickets = 0;
            visit.CarryDiscovery = 0;
            granted = _ctx.Granter.Grant(bundle, EconomyReason.WeeklyJournal, DiscoverySource.WeeklyBook);
            Claimed?.Invoke(CarryClaimId, granted);
            return true;
        }

        /// <summary>Tickets become Light (15 min each) while the Play button does not exist, as in the reward tables.</summary>
        private RewardBundle Bundle(long gems, int tickets, int discoveryEggs)
        {
            var bundle = new RewardBundle
            {
                Gems = Math.Max(0L, gems),
                Tickets = Math.Max(0, tickets),
                DiscoveryEggs = Math.Max(0, discoveryEggs),
                Size = discoveryEggs > 0 ? GiftSize.Big : GiftSize.Small,
            };
            if (bundle.Tickets > 0)
            {
                var rewards = _ctx.Rewards();
                if (!rewards.PlayOpen)
                {
                    bundle.LightMinutes = bundle.Tickets * rewards.TicketLightMinutes;
                    bundle.Tickets = 0;
                    bundle.Substituted = true;
                }
            }
            return bundle;
        }

        // ------------------------------------------------------------------------------------------- progress hooks

        public void OnHatched(in HatchResult result)
        {
            if (!result.Succeeded || Visit.GoalCount == 0)
                return;
            AddProgress(JournalGoalId.HatchEggs, 1);
            if (result.NewVariant || result.NewSpecies)
                AddProgress(JournalGoalId.DiscoverVariants, 1);
            if (result.NewSpecies)
                AddProgress(JournalGoalId.NewSpecies, 1);
            if (_ctx.Content.Eggs.TryGet(result.Egg, out var egg) && egg.Tier == EggTier.Focus)
                AddProgress(JournalGoalId.PreciousEggs, 1);
        }

        public void OnMasteryLeveled(ContentId species, int levels) => AddProgress(JournalGoalId.MasteryLevels, levels);

        public void OnCodexPageOpened(ContentId species)
        {
        }

        /// <summary>Only touches inside the daily bond caps count (plan, gap 17: no tap spam).</summary>
        public void OnAnimalTouched(uint animal, bool countedForBond)
        {
            if (countedForBond)
                AddProgress(JournalGoalId.AnimalsTapped, 1);
        }

        public void OnTreatGiven(uint animal) => AddProgress(JournalGoalId.TreatsGiven, 1);

        public void OnBondGained(uint animal, int points) => AddProgress(JournalGoalId.BondPoints, points);

        public void OnUpgradesBought(int levels) => AddProgress(JournalGoalId.Upgrades, levels);

        public void OnZoneOpened() => AddProgress(JournalGoalId.Expansions, 1);

        public void OnWishCompleted() => AddProgress(JournalGoalId.Wishes, 1);

        public void OnGiftOpened() => AddProgress(JournalGoalId.DailyGifts, 1);

        public void OnCalendarStamped() => AddProgress(JournalGoalId.CalendarDays, 1);

        /// <summary>T37: the stars of a rewarded activity game, and a challenge passed (its first star).</summary>
        public void OnActivityGame(int stars, bool challengePassed)
        {
            AddProgress(JournalGoalId.ActivityStars, stars);
            if (challengePassed)
                AddProgress(JournalGoalId.ActivityChallenges, 1);
        }

        /// <summary>
        /// T38: JRN-033 « Brosse {n} animaux »: each grooming game finished brushes one animal, practice included (section
        /// 5.1); a game left before its end does not count. The same animal twice counts twice (kind: a refuge of 5 animals
        /// never blocks the goal).
        /// </summary>
        public void OnGroomingGame(in GroomingGameSummary game)
        {
            if (game.Finished)
                AddProgress(JournalGoalId.Grooming, 1);
        }

        /// <summary>Adds to every goal of this kind while its week runs, up to its target.</summary>
        private void AddProgress(JournalGoalId id, int amount)
        {
            if (amount <= 0)
                return;
            var visit = Visit;
            int count = visit.GoalCount;
            if (count == 0 || _ctx.TodayKey >= visit.JournalEndKey)
                return;
            var goals = visit.Goals;
            for (int i = 0; i < count && i < goals.Length; i++)
            {
                ref var record = ref goals[i];
                if (record.Goal != (ushort)id || record.Progress >= record.Target)
                    continue;
                long progress = (long)record.Progress + amount;
                record.Progress = progress >= record.Target ? record.Target : (int)progress;
                if (record.Progress >= record.Target)
                    GoalReached?.Invoke(i);
            }
        }
    }
}
