#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>How a wish was claimed (analytics wish_done, I1). Stable values.</summary>
    public enum WishClaimPath : byte
    {
        Bubble = 0,
        Animal = 1,
        TodayRow = 2,
        ClaimAll = 3,

        /// <summary>Done but not claimed: given at the next Today opening (doc 01 Contenu, section 2.6).</summary>
        Auto = 4,
    }

    /// <summary>One wish claimed: the record as it was (already removed from the stock) and what it gave.</summary>
    public readonly struct WishClaim
    {
        /// <summary>The slot it held when claimed.</summary>
        public readonly int Slot;

        public readonly WishRecord Wish;

        /// <summary>Gems really given (the cumulative cap may lower them, section 2.6).</summary>
        public readonly long Gems;

        /// <summary>The treat given: the one of the animal's biome.</summary>
        public readonly ContentId Treat;

        /// <summary>Bond points really added to the animal (0 without a bond sink).</summary>
        public readonly int Bond;

        public readonly WishClaimPath Path;

        /// <summary>The expressing animal's species and biome (None when the animal is gone).</summary>
        public readonly ContentId Species;

        public readonly ContentId Biome;

        /// <summary>Wishes left in the stock after this claim (analytics wish_done, I3).</summary>
        public readonly int PendingAfter;

        /// <summary>
        /// Minutes from the draw to the completion (analytics wish_done, D0). Exact for a wish drawn and completed while this
        /// service ran; otherwise counted from the start of its draw day, or to the claim when the completion time is unknown.
        /// </summary>
        public readonly double MinutesFromDraw;

        public WishClaim(int slot, in WishRecord wish, long gems, ContentId treat, int bond, WishClaimPath path, ContentId species,
            ContentId biome, int pendingAfter, double minutesFromDraw)
        {
            Slot = slot;
            Wish = wish;
            Gems = gems;
            Treat = treat;
            Bond = bond;
            Path = path;
            Species = species;
            Biome = biome;
            PendingAfter = pendingAfter;
            MinutesFromDraw = minutesFromDraw;
        }

        /// <summary>The template number (analytics wish_done, I0).</summary>
        public ushort Template => Wish.Template;

        public WishTemplateId TemplateId => (WishTemplateId)Wish.Template;
    }

    /// <summary>A wish entered the stock: a daily draw, a swap or the tutorial wish (analytics wish_drawn).</summary>
    public readonly struct WishDraw
    {
        public readonly int Slot;

        /// <summary>The template number (I0).</summary>
        public readonly int Template;

        public readonly WishSystem System;

        /// <summary>The refuge level tier its amount comes from, 1 to 4 (I2).</summary>
        public readonly int Tier;

        /// <summary>Wishes in the stock with this one (I3).</summary>
        public readonly int PendingAfter;

        /// <summary>It replaced a wish through the free swap.</summary>
        public readonly bool IsSwap;

        /// <summary>The tutorial wish WISH-T01 (outside the daily draw).</summary>
        public readonly bool IsTutorial;

        public WishDraw(int slot, int template, WishSystem system, int tier, int pendingAfter, bool isSwap, bool isTutorial)
        {
            Slot = slot;
            Template = template;
            System = system;
            Tier = tier;
            PendingAfter = pendingAfter;
            IsSwap = isSwap;
            IsTutorial = isTutorial;
        }

        public WishTemplateId TemplateId => (WishTemplateId)Template;
    }

    /// <summary>A free swap (analytics wish_swap): the slot, the template replaced and the new one.</summary>
    public readonly struct WishSwapInfo
    {
        public readonly int Slot;
        public readonly int OldTemplate;
        public readonly int NewTemplate;

        public WishSwapInfo(int slot, int oldTemplate, int newTemplate)
        {
            Slot = slot;
            OldTemplate = oldTemplate;
            NewTemplate = newTemplate;
        }
    }

    /// <summary>What the wishes already drawn on a day hold, for the same-day rules (doc 01 Contenu, section 2.4).</summary>
    public struct WishDaySet
    {
        /// <summary>One bit per system (bit = system value).</summary>
        public uint Systems;

        public byte Collection;
        public byte Care;
        public byte Refuge;
        public byte Games;
        public byte Travels;
        public byte Quick;
        public byte Timer;
        public byte Count;

        public readonly bool HasSystem(WishSystem system) => (Systems & WishDrawRules.Bit(system)) != 0;

        public readonly int FamilyCount(WishFamily family)
        {
            switch (family)
            {
                case WishFamily.Collection: return Collection;
                case WishFamily.Care: return Care;
                case WishFamily.Refuge: return Refuge;
                case WishFamily.Games: return Games;
                default: return Travels;
            }
        }

        public void Add(WishSystem system, bool quick, bool timer)
        {
            Systems |= WishDrawRules.Bit(system);
            switch (WishSystems.FamilyOf(system))
            {
                case WishFamily.Collection: Collection++; break;
                case WishFamily.Care: Care++; break;
                case WishFamily.Refuge: Refuge++; break;
                case WishFamily.Games: Games++; break;
                default: Travels++; break;
            }
            if (quick)
                Quick++;
            if (timer)
                Timer++;
            Count++;
        }
    }

    /// <summary>
    /// The same-day rules of the daily draw (doc 01 Contenu, section 2.4), pure and allocation-free: never two wishes of the
    /// same system, at most two of a family, at least one quick wish (the last slot is drawn among quick ones when the day
    /// has none), at most one timer wish. When no candidate passes, the rules are lifted in this order: family, then quick,
    /// then timer. The same-system rule is lifted only when fewer than 3 systems are open, and then only when nothing else
    /// works (the other rules apply again at full strength first); a day never gets a duplicate template instead.
    /// </summary>
    public static class WishDrawRules
    {
        public const int MaxPerFamily = 2;
        public const int MaxTimers = 1;

        /// <summary>The same-system rule holds whenever at least this many systems are open.</summary>
        public const int SystemRuleMinSystems = 3;

        // Flags of the rules lifted for a pick.
        public const int RelaxedFamily = 1;
        public const int RelaxedQuick = 2;
        public const int RelaxedTimer = 4;
        public const int RelaxedSystem = 8;

        public static uint Bit(WishSystem system) => (int)system < 32 ? 1u << (int)system : 0u;

        /// <summary>
        /// Picks one candidate by weight under the rules, lifting them only as needed; returns its index (-1: none) and the
        /// rules lifted. Candidates come already filtered (eligible, not in the stock) with their final weights.
        /// </summary>
        public static int Pick(int count, WishSystem[] systems, bool[] quick, bool[] timer, double[] weights, in WishDaySet today,
            bool lastSlot, int openSystems, ref DeterministicRandom rng, out int relaxed)
        {
            bool needQuick = lastSlot && today.Quick == 0;
            int passes = openSystems < SystemRuleMinSystems ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                bool systemRule = pass == 0;
                // Level 0: every rule; 1: family lifted; 2: family and quick; 3: family, quick and timer.
                for (int level = 0; level <= 3; level++)
                {
                    double total = 0.0;
                    for (int i = 0; i < count; i++)
                        if (Allowed(i, level, systemRule, needQuick, systems, quick, timer, weights, in today))
                            total += weights[i];
                    if (!(total > 0.0))
                        continue;

                    relaxed = (level >= 1 ? RelaxedFamily : 0) | (level >= 2 ? RelaxedQuick : 0) | (level >= 3 ? RelaxedTimer : 0)
                        | (systemRule ? 0 : RelaxedSystem);
                    double roll = rng.NextDouble() * total;
                    int chosen = -1;
                    for (int i = 0; i < count; i++)
                    {
                        if (!Allowed(i, level, systemRule, needQuick, systems, quick, timer, weights, in today))
                            continue;
                        chosen = i;
                        roll -= weights[i];
                        if (roll < 0.0)
                            break;
                    }
                    return chosen;
                }
            }
            relaxed = 0;
            return -1;
        }

        private static bool Allowed(int i, int level, bool systemRule, bool needQuick, WishSystem[] systems, bool[] quick, bool[] timer,
            double[] weights, in WishDaySet today)
        {
            if (!(weights[i] > 0.0))
                return false;
            var system = systems[i];
            if (systemRule && today.HasSystem(system))
                return false;
            if (level < 1 && today.FamilyCount(WishSystems.FamilyOf(system)) >= MaxPerFamily)
                return false;
            if (level < 2 && needQuick && !quick[i])
                return false;
            if (level < 3 && timer[i] && today.Timer >= MaxTimers)
                return false;
            return true;
        }
    }

    /// <summary>
    /// The wishes (doc 01 Contenu, section 2): refuge animals ask for small things, shown as bubbles in the world and listed
    /// in Today.
    /// <list type="bullet">
    /// <item>Draw (section 2.4): once per login day, k = min(wishes per day, stock - pending) new wishes, one slot at a time
    /// without replacement among the shipped templates whose system is open and whose eligibility holds now and which are
    /// not in the stock. Weight = base x 3 during the 3 days after the system opened, x 0.3 when the template came out in
    /// the last 3 days, x 0.25 when its system is pending from an earlier day; same-day rules in <see cref="WishDrawRules"/>.
    /// The animal: favorites weigh 2, others 1, x 1.5 for the lowest-bond quarter, never one already holding a wish.
    /// Amounts are fixed from the refuge level tier at draw time. Day 1: SYS-01 to SYS-04 only, with a quick SYS-03 wish.</item>
    /// <item>Progress (sections 2.1, 2.3) counts from the moment a wish appears, through <see cref="IDailyProgressHooks"/>.</item>
    /// <item>Claim: 3 Gems (in total at most 9 per day since install), +15 bond, 1 treat of the animal's biome, biome mastery
    /// XP (doc 02 Améliorations, section 7.1) and species mastery XP (doc 08 econ.mastery_xp.wish).</item>
    /// <item>One free swap a day (section 2.5), the tutorial wish WISH-T01 (welcome steps A08 to A10), auto-claim at the
    /// next Today opening (section 2.6).</item>
    /// </list>
    /// The facade calls it; it never subscribes to game events. Draws and hooks allocate nothing (buffers sized at
    /// construction) and draws are deterministic for a profile and a day.
    /// </summary>
    public sealed class WishService : IDailyProgressHooks
    {
        public const double NoveltyMultiplier = 3.0;
        public const double RepeatMultiplier = 0.3;
        public const double StockMultiplier = 0.25;

        /// <summary>Days after a system opened during which its templates weigh x 3 (the opening day counts as day 0).</summary>
        public const int NoveltyDays = 3;

        /// <summary>A template drawn this many days ago or fewer weighs x 0.3.</summary>
        public const int RepeatDays = 3;

        public const double FavoriteWeight = 2.0;
        public const double LowBondMultiplier = 1.5;

        /// <summary>WISH-015: touches count when at least this far apart (section 2.3).</summary>
        public const double CuddleSpacingSeconds = 2.0;

        private const uint DrawSalt = 0x57150001u;
        private const uint FirstDaySalt = 0x57150002u;
        private const uint SwapSalt = 0x57150003u;

        private const double PreciousEggMinutes = 15.0;
        private const double UpgradeMinutes = 30.0;
        private const double ExpansionMinutes = 45.0;
        private const long RecentDiscoverySeconds = 3L * 24 * 3600;
        private const double MasteryNearFraction = 0.4;
        private const int BondBins = 1001;

        private enum DrawMode : byte
        {
            Daily = 0,
            FirstDay = 1,
            Swap = 2,
        }

        private readonly DailyContext _ctx;
        private readonly IBondSink? _bond;

        // Per template (catalog index), refilled at each draw.
        private readonly bool[] _eligible;
        private readonly bool[] _recent;
        private readonly bool[] _inStock;

        // Candidates of one slot.
        private readonly int[] _candTemplate;
        private readonly WishSystem[] _candSystem;
        private readonly bool[] _candQuick;
        private readonly bool[] _candTimer;
        private readonly double[] _candWeight;

        // Parameters of a template (species, biomes, biome and upgrade kind pairs).
        private readonly ContentId[] _paramIds;
        private readonly byte[] _paramExtra;

        // Per species (content dense index): in the pool of an open egg; oldest animal's birth time.
        private readonly bool[] _speciesOffered;
        private readonly uint[] _oldest;
        private bool _oldestReady;

        private readonly int[] _bondHistogram = new int[BondBins];

        // The current draw.
        private int _dayNumber;
        private int _tier = 1;
        private uint _openMask;
        private int _openSystems;

        // WISH-015: the last touch that counted (session time, not saved: a new session starts a fresh spacing).
        private double _lastCuddle = double.NegativeInfinity;

        // WISH-026: the upgrade's level when the wish appeared (or the service started), to count levels bought since.
        private int _upgradeBaseline;
        private bool _upgradeBaselineValid;

        // Per slot, Unix seconds the wish was drawn and completed while this service ran (NaN: before, not saved).
        private readonly double[] _drawnAt;
        private readonly double[] _doneAt;

        public WishService(DailyContext ctx, IBondSink? bond)
        {
            _ctx = ctx;
            _bond = bond;
            int templates = WishCatalog.All.Length;
            _eligible = new bool[templates];
            _recent = new bool[templates];
            _inStock = new bool[templates];
            _candTemplate = new int[templates];
            _candSystem = new WishSystem[templates];
            _candQuick = new bool[templates];
            _candTimer = new bool[templates];
            _candWeight = new double[templates];
            int species = Math.Max(1, ctx.Content.Species.Count);
            _speciesOffered = new bool[species];
            _oldest = new uint[species];
            int pairs = Math.Max(species, ctx.ProgressionConfig.Biomes.Length * 3);
            _paramIds = new ContentId[pairs];
            _paramExtra = new byte[pairs];
            int slots = Math.Max(DailyVisitState.MaxWishStock, ctx.State.DailyVisit.Wishes.Length);
            _drawnAt = new double[slots];
            _doneAt = new double[slots];
            for (int i = 0; i < slots; i++)
            {
                _drawnAt[i] = double.NaN;
                _doneAt[i] = double.NaN;
            }
            TakeUpgradeBaseline();
        }

        /// <summary>A wish entered the stock (<see cref="WishDraw.Slot"/>): a draw, a swap or the tutorial wish.</summary>
        public event Action<WishDraw>? Drawn;

        /// <summary>A wish's progress moved and it is not done yet.</summary>
        public event Action<int>? Progressed;

        /// <summary>A wish just became done (its bubble turns into a check; nothing is given before the claim).</summary>
        public event Action<int>? Completed;

        public event Action<WishClaim>? Claimed;

        /// <summary>A free swap replaced the wish of a slot (raised before the <see cref="Drawn"/> of the new one).</summary>
        public event Action<WishSwapInfo>? Swapped;

        /// <summary>The tutorial wish was fulfilled: the welcome is over (A10), Today may open and the first draw follow.</summary>
        public event Action? OnboardingFinished;

        private PlayerState State => _ctx.State;
        private DailyVisitConfig Config => _ctx.Config;

        private int Pending
        {
            get
            {
                var visit = _ctx.State.DailyVisit;
                return Math.Max(0, Math.Min(visit.WishCount, visit.Wishes.Length));
            }
        }

        /// <summary>Wishes in the stock (done ones included until claimed).</summary>
        public int PendingCount => Pending;

        public WishRecord Get(int slot)
        {
            if ((uint)slot >= (uint)Pending)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return State.DailyVisit.Wishes[slot];
        }

        public bool HasDoneUnclaimed
        {
            get
            {
                var wishes = State.DailyVisit.Wishes;
                for (int i = 0; i < Pending; i++)
                    if (wishes[i].IsDone)
                        return true;
                return false;
            }
        }

        /// <summary>The wish this animal expresses (a bubble above it), if any.</summary>
        public bool WishOf(uint animal, out int slot)
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = 0; i < Pending; i++)
            {
                if (wishes[i].Animal == animal)
                {
                    slot = i;
                    return true;
                }
            }
            slot = -1;
            return false;
        }

        /// <summary>The tutorial wish is live: started at A08, not done yet (welcome steps A08 to A10).</summary>
        public bool TutorialActive => State.DailyVisit.TutorialStep == 2 && TutorialSlot() >= 0;

        /// <summary>A shipped system is open for wishes (its opening day is known).</summary>
        public bool IsSystemOpen(WishSystem system)
        {
            int s = (int)system;
            var unlock = State.DailyVisit.SystemUnlockDay;
            return s > 0 && s < unlock.Length && unlock[s] > 0 && WishCatalog.IsShipped(system);
        }

        /// <summary>
        /// Records the day each shipped system opens (x 3 during its first days; the weekly journal reads it too). Draws do it
        /// themselves; the facade may also call it at a day change. A time-zone conversion: not per frame.
        /// </summary>
        public void RefreshSystemUnlocks() => UpdateSystemUnlocks(_ctx.DayNumber);

        // ------------------------------------------------------------------------------------------- draws

        /// <summary>
        /// The draw of a login day (section 2.4): k = min(wishes per day, stock - pending) new wishes. Once per day key (a
        /// second call the same day, or an earlier key, draws nothing). Nothing happens before a wish system is open (the
        /// welcome). Returns how many were drawn.
        /// </summary>
        public int DrawForDay(int dayKey) => Draw(dayKey, DrawMode.Daily);

        /// <summary>The first draw, at the first Today visit of day 1 (A12): SYS-01 to SYS-04 only, with a quick SYS-03 wish.</summary>
        public int DrawFirstDay(int dayKey) => Draw(dayKey, DrawMode.FirstDay);

        /// <summary>
        /// Today's draw if it has not happened yet (idempotent for a day key; the facade calls it whenever Today is open):
        /// the day-1 rules for the account's very first draw or on day 1, the daily draw otherwise. Returns how many were drawn.
        /// </summary>
        public int EnsureDrawn(int dayKey)
        {
            var visit = State.DailyVisit;
            if (dayKey <= visit.WishDrawDayKey)
                return 0;
            return visit.WishDrawDayKey == 0 || _ctx.DayNumber <= 1 ? DrawFirstDay(dayKey) : DrawForDay(dayKey);
        }

        /// <summary>Free swaps left today (section 2.5: never accumulated).</summary>
        public int SwapsLeft
        {
            get
            {
                var visit = State.DailyVisit;
                int used = visit.SwapDayKey == _ctx.TodayKey ? visit.SwapsUsed : 0;
                return Math.Max(0, Config.FreeSwaps - used);
            }
        }

        /// <summary>A free swap is left today and a pending wish can take it.</summary>
        public bool CanSwap
        {
            get
            {
                if (!Config.WishesEnabled || SwapsLeft <= 0)
                    return false;
                var wishes = State.DailyVisit.Wishes;
                for (int i = 0; i < Pending; i++)
                    if (!wishes[i].IsDone && !wishes[i].IsTutorial)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// The free swap (section 2.5): a pending, unfinished wish is replaced in place by one drawn under the same-day rules
        /// of today, never its template nor its system. The animal keeps expressing it. False (nothing used) when no
        /// replacement exists.
        /// </summary>
        public bool TrySwap(int slot)
        {
            var visit = State.DailyVisit;
            if ((uint)slot >= (uint)Pending || !Config.WishesEnabled || SwapsLeft <= 0)
                return false;
            var old = visit.Wishes[slot];
            if (old.IsDone || old.IsTutorial)
                return false;
            int today = _ctx.TodayKey;
            PrepareDraw(today, DrawMode.Swap);
            if (_openSystems == 0)
                return false;
            var oldId = (WishTemplateId)old.Template;
            var oldSystem = WishCatalog.TryGet(oldId, out var oldTemplate) ? oldTemplate.System : WishSystem.None;
            var rng = new DeterministicRandom(_ctx.Seed(today, SwapSalt ^ ((uint)slot * 977u) ^ ((uint)old.Template << 10) ^ visit.SwapsUsed));
            if (DrawOne(today, ref rng, true, false, slot, oldId, oldSystem) < 0)
                return false;
            if (visit.SwapDayKey != today)
            {
                visit.SwapDayKey = today;
                visit.SwapsUsed = 0;
            }
            if (visit.SwapsUsed < byte.MaxValue)
                visit.SwapsUsed++;
            _ctx.RequestSave(SaveUrgency.Priority);
            Swapped?.Invoke(new WishSwapInfo(slot, old.Template, visit.Wishes[slot].Template));
            RaiseDrawn(slot, true, Pending);
            return true;
        }

        /// <summary>
        /// A template's draw weight now for a day (base x novelty x repeat x stock), eligibility aside: the multipliers of
        /// section 2.4, for tests and tools. A time-zone conversion: not per frame.
        /// </summary>
        public double DrawWeight(WishTemplateId id, int dayKey)
        {
            int index = WishCatalog.IndexOf(id);
            if (index < 0)
                return 0.0;
            var template = WishCatalog.All[index];
            return Weight(template, _ctx.DayNumber, RecentlyDrawn(id, dayKey), EarlierSystems(dayKey, -1));
        }

        /// <summary>
        /// The weight this animal has now in the animal draw (section 2.4): 2 for a favorite, 1 otherwise, x 1.5 in the
        /// lowest-bond quarter, 0 when it already holds a wish or is unknown.
        /// </summary>
        public double AnimalWeight(uint animal)
        {
            if (!State.Collection.TryGetIndex(animal, out int index))
                return 0.0;
            return AnimalWeightAt(index, LowBondThreshold());
        }

        private int Draw(int dayKey, DrawMode mode)
        {
            var visit = State.DailyVisit;
            if (!Config.WishesEnabled || dayKey <= visit.WishDrawDayKey)
                return 0;
            PrepareDraw(dayKey, mode);
            // Before the welcome ends no wish system is open: the day is not used up.
            if (_openSystems == 0)
                return 0;
            visit.WishDrawDayKey = dayKey;
            int first = Pending;
            int k = Math.Min(Config.WishesPerDay, Config.WishStock - first);
            int drawn = 0;
            if (k > 0)
            {
                var rng = new DeterministicRandom(_ctx.Seed(dayKey, mode == DrawMode.FirstDay ? FirstDaySalt : DrawSalt));
                for (int i = 0; i < k; i++)
                {
                    bool last = i == k - 1;
                    bool cuddle = mode == DrawMode.FirstDay && i == 0;
                    int slot = DrawOne(dayKey, ref rng, last, cuddle, -1, WishTemplateId.None, WishSystem.None);
                    if (slot < 0 && cuddle)
                        slot = DrawOne(dayKey, ref rng, last, false, -1, WishTemplateId.None, WishSystem.None);
                    if (slot < 0)
                        break;
                    drawn++;
                }
            }
            _ctx.RequestSave(SaveUrgency.Priority);
            // Events once the day's draw is complete: a handler can never disturb a draw in progress.
            for (int slot = first; slot < first + drawn; slot++)
                RaiseDrawn(slot, false, slot + 1);
            return drawn;
        }

        private void RaiseDrawn(int slot, bool swap, int pendingAfter)
        {
            var handler = Drawn;
            if (handler == null || (uint)slot >= (uint)Pending)
                return;
            var wish = State.DailyVisit.Wishes[slot];
            var system = WishCatalog.TryGet((WishTemplateId)wish.Template, out var template) ? template.System : WishSystem.None;
            handler(new WishDraw(slot, wish.Template, system, _tier, pendingAfter, swap, wish.IsTutorial));
        }

        /// <summary>Unlock days, open systems (day 1: SYS-01 to SYS-04), the tier, every template's eligibility now, repeats.</summary>
        private void PrepareDraw(int dayKey, DrawMode mode)
        {
            _dayNumber = _ctx.DayNumber;
            UpdateSystemUnlocks(_dayNumber);
            _tier = WishCatalog.TierOf(_ctx.RefugeLevel);
            bool firstDayRules = mode == DrawMode.FirstDay || (mode == DrawMode.Swap && _dayNumber <= 1);
            _openMask = 0;
            _openSystems = 0;
            for (int s = 1; s < WishSystems.Count; s++)
            {
                var system = (WishSystem)s;
                if (!IsSystemOpen(system) || (firstDayRules && system > WishSystem.Upgrades))
                    continue;
                _openMask |= WishDrawRules.Bit(system);
                _openSystems++;
            }
            if (_openSystems == 0)
                return;

            _oldestReady = false;
            FillOfferedSpecies();
            var all = WishCatalog.All;
            for (int i = 0; i < all.Length; i++)
                _eligible[i] = all[i].ShippedInThisBuild && (_openMask & WishDrawRules.Bit(all[i].System)) != 0 && IsEligible(all[i]);
            MarkRecent(dayKey);
        }

        /// <summary>
        /// Draws one wish: the animal (kept for a swap), then a template under the same-day rules, then its parameters at
        /// today's tier. Writes it at the end of the stock, or over <paramref name="replaceSlot"/>. Returns the slot or -1.
        /// </summary>
        private int DrawOne(int dayKey, ref DeterministicRandom rng, bool lastSlot, bool quickCuddleOnly, int replaceSlot,
            WishTemplateId excludeTemplate, WishSystem excludeSystem)
        {
            var visit = State.DailyVisit;
            var wishes = visit.Wishes;
            int pending = Pending;
            if (replaceSlot < 0 && pending >= wishes.Length)
                return -1;

            // Today's wishes (same-day rules), systems pending from earlier days (x 0.25), templates in the stock.
            var today = new WishDaySet();
            uint earlier = 0;
            Array.Clear(_inStock, 0, _inStock.Length);
            var all = WishCatalog.All;
            for (int i = 0; i < pending; i++)
            {
                int index = WishCatalog.IndexOf((WishTemplateId)wishes[i].Template);
                if (index < 0)
                    continue;
                _inStock[index] = true;
                if (i == replaceSlot || wishes[i].IsTutorial)
                    continue;
                if (wishes[i].DrawnDay == dayKey)
                    today.Add(all[index].System, all[index].Quick, all[index].Timer);
                else
                    earlier |= WishDrawRules.Bit(all[index].System);
            }

            uint animal;
            if (replaceSlot >= 0)
                animal = wishes[replaceSlot].Animal;
            else if (!PickAnimal(ref rng, out animal))
                return -1;

            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (!_eligible[i] || _inStock[i])
                    continue;
                var template = all[i];
                if (template.Id == excludeTemplate || template.System == excludeSystem)
                    continue;
                if (quickCuddleOnly && (template.System != WishSystem.CuddlesAndTreats || !template.Quick))
                    continue;
                _candTemplate[count] = i;
                _candSystem[count] = template.System;
                _candQuick[count] = template.Quick;
                _candTimer[count] = template.Timer;
                _candWeight[count] = Weight(template, _dayNumber, _recent[i], earlier);
                count++;
            }
            int pick = WishDrawRules.Pick(count, _candSystem, _candQuick, _candTimer, _candWeight, in today, lastSlot, _openSystems, ref rng,
                out _);
            if (pick < 0)
                return -1;

            var chosen = all[_candTemplate[pick]];
            var record = Build(chosen, animal, dayKey, ref rng);
            int slot = replaceSlot;
            if (slot < 0)
            {
                slot = pending;
                visit.WishCount = pending + 1;
            }
            else if ((WishTemplateId)wishes[slot].Template == WishTemplateId.UpgradeInBiome)
            {
                _upgradeBaselineValid = false;
            }
            wishes[slot] = record;
            StampDrawn(slot);
            PushHistory(chosen.Id, dayKey);
            switch (chosen.Id)
            {
                case WishTemplateId.TapAnimals:
                    visit.TouchedCount = 0;
                    break;
                case WishTemplateId.CuddleAnimal:
                    _lastCuddle = double.NegativeInfinity;
                    break;
                case WishTemplateId.UpgradeInBiome:
                    TakeUpgradeBaseline(record);
                    break;
            }
            return slot;
        }

        /// <summary>The record of a drawn template: amount at the draw's tier (fixed from now on) and the subject it names.</summary>
        private WishRecord Build(in WishTemplate template, uint animal, int dayKey, ref DeterministicRandom rng)
        {
            var record = new WishRecord
            {
                Template = (ushort)template.Id,
                Animal = animal,
                ParamKind = (byte)template.ParamKind,
                Target = Math.Max(1, template.Amount(_tier)),
                DrawnDay = dayKey,
            };
            int n;
            switch (template.Id)
            {
                case WishTemplateId.HatchBiomeEggs:
                    n = CollectBiomeEggBiomes();
                    if (n > 0)
                        record.Param = _paramIds[rng.Next(n)];
                    break;
                case WishTemplateId.RaiseSpeciesMastery:
                    n = CollectMasterySpecies();
                    if (n > 0)
                        record.Param = _paramIds[rng.Next(n)];
                    break;
                case WishTemplateId.LookAtSpeciesInCodex:
                    n = CollectCodexSpecies();
                    if (n > 0)
                        record.Param = _paramIds[rng.Next(n)];
                    break;
                case WishTemplateId.UpgradeInBiome:
                    n = CollectUpgrades();
                    if (n > 0)
                    {
                        int k = rng.Next(n);
                        record.Param = _paramIds[k];
                        record.ParamExtra = _paramExtra[k];
                    }
                    break;
            }
            return record;
        }

        private double Weight(in WishTemplate template, int dayNumber, bool recent, uint earlierSystems)
        {
            double weight = template.Weight;
            var unlock = State.DailyVisit.SystemUnlockDay;
            int s = (int)template.System;
            if (s < unlock.Length && unlock[s] > 0)
            {
                int since = dayNumber - unlock[s];
                if (since >= 0 && since <= NoveltyDays)
                    weight *= NoveltyMultiplier;
            }
            if (recent)
                weight *= RepeatMultiplier;
            if ((earlierSystems & WishDrawRules.Bit(template.System)) != 0)
                weight *= StockMultiplier;
            return weight;
        }

        /// <summary>Systems of pending wishes (not the tutorial) drawn on another day than <paramref name="dayKey"/>.</summary>
        private uint EarlierSystems(int dayKey, int excludeSlot)
        {
            var wishes = State.DailyVisit.Wishes;
            uint mask = 0;
            for (int i = 0; i < Pending; i++)
            {
                if (i == excludeSlot || wishes[i].IsTutorial || wishes[i].DrawnDay == dayKey)
                    continue;
                int index = WishCatalog.IndexOf((WishTemplateId)wishes[i].Template);
                if (index >= 0)
                    mask |= WishDrawRules.Bit(WishCatalog.All[index].System);
            }
            return mask;
        }

        private void MarkRecent(int dayKey)
        {
            Array.Clear(_recent, 0, _recent.Length);
            var visit = State.DailyVisit;
            int count = Math.Min(visit.HistoryCount, visit.History.Length);
            for (int i = 0; i < count; i++)
            {
                var entry = visit.History[i];
                int index = WishCatalog.IndexOf((WishTemplateId)entry.Template);
                if (index < 0)
                    continue;
                int days = DaysSince(entry.DayKey, dayKey);
                if (days >= 0 && days <= RepeatDays)
                    _recent[index] = true;
            }
        }

        private bool RecentlyDrawn(WishTemplateId id, int dayKey)
        {
            var visit = State.DailyVisit;
            int count = Math.Min(visit.HistoryCount, visit.History.Length);
            for (int i = 0; i < count; i++)
            {
                var entry = visit.History[i];
                if (entry.Template != (ushort)id)
                    continue;
                int days = DaysSince(entry.DayKey, dayKey);
                if (days >= 0 && days <= RepeatDays)
                    return true;
            }
            return false;
        }

        private void PushHistory(WishTemplateId id, int dayKey)
        {
            var visit = State.DailyVisit;
            var history = visit.History;
            if (history.Length == 0)
                return;
            int next = ((visit.HistoryNext % history.Length) + history.Length) % history.Length;
            history[next] = new WishHistoryEntry { Template = (ushort)id, DayKey = dayKey };
            visit.HistoryNext = (next + 1) % history.Length;
            visit.HistoryCount = Math.Min(history.Length, Math.Max(0, visit.HistoryCount) + 1);
        }

        // ------------------------------------------------------------------------------------------- animals

        /// <summary>One animal by weight among the refuge's animals free of a wish (section 2.4).</summary>
        private bool PickAnimal(ref DeterministicRandom rng, out uint animal)
        {
            animal = 0;
            var collection = State.Collection;
            int count = collection.Count;
            if (count == 0)
                return false;
            int threshold = LowBondThreshold();
            double total = 0.0;
            for (int i = 0; i < count; i++)
                total += AnimalWeightAt(i, threshold);
            if (!(total > 0.0))
                return false;
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < count; i++)
            {
                double weight = AnimalWeightAt(i, threshold);
                if (weight <= 0.0)
                    continue;
                animal = collection[i].InstanceId;
                roll -= weight;
                if (roll < 0.0)
                    break;
            }
            return animal != 0;
        }

        private double AnimalWeightAt(int index, int lowBondThreshold)
        {
            ref readonly var animal = ref State.Collection[index];
            if (HoldsWish(animal.InstanceId))
                return 0.0;
            double weight = animal.IsFavorite ? FavoriteWeight : 1.0;
            if (Math.Min((int)animal.BondPoints, BondBins - 1) <= lowBondThreshold)
                weight *= LowBondMultiplier;
            return weight;
        }

        /// <summary>The bond value at or below which an animal is in the lowest quarter of the refuge (a histogram, no sort).</summary>
        private int LowBondThreshold()
        {
            var collection = State.Collection;
            int count = collection.Count;
            if (count == 0)
                return -1;
            Array.Clear(_bondHistogram, 0, _bondHistogram.Length);
            for (int i = 0; i < count; i++)
                _bondHistogram[Math.Min((int)collection[i].BondPoints, BondBins - 1)]++;
            int quarter = (count + 3) / 4;
            int seen = 0;
            for (int b = 0; b < BondBins; b++)
            {
                seen += _bondHistogram[b];
                if (seen >= quarter)
                    return b;
            }
            return BondBins - 1;
        }

        private bool HoldsWish(uint animal)
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = 0; i < Pending; i++)
                if (wishes[i].Animal == animal)
                    return true;
            return false;
        }

        // ------------------------------------------------------------------------------------------- systems and eligibility

        private void UpdateSystemUnlocks(int dayNumber)
        {
            var unlock = State.DailyVisit.SystemUnlockDay;
            for (int s = 1; s < WishSystems.Count && s < unlock.Length; s++)
            {
                if (unlock[s] > 0)
                    continue;
                var system = (WishSystem)s;
                if (WishCatalog.IsShipped(system) && SystemOpensNow(system))
                    unlock[s] = Math.Max(1, dayNumber);
            }
        }

        /// <summary>
        /// Section 2.2: SYS-01 to SYS-03 open at the end of the welcome (A10); SYS-04 with the first upgrade bought or the
        /// Upgrade button (A07); SYS-06 when a biome offers its first expansion (zone signs shown, the next zone allowed);
        /// T38: SYS-09 with Grooming (refuge level 3 and 5 animals) once the build has activities; T39: SYS-10 with Hide and
        /// Seek (refuge level 4 and 8 animals) the same way.
        /// </summary>
        private bool SystemOpensNow(WishSystem system)
        {
            if (!State.IsOnboardingDone)
                return false;
            switch (system)
            {
                case WishSystem.Hatching:
                case WishSystem.DiscoveriesAndMastery:
                case WishSystem.CuddlesAndTreats:
                    return true;
                case WishSystem.Upgrades:
                    return AnyUpgradeBought() || _ctx.Feature(RefugeFeature.UpgradeButton);
                case WishSystem.Expansions:
                    return ExpansionOffered();
                case WishSystem.Grooming:
                    return GroomingOpen;
                case WishSystem.HideAndSeek:
                    return HideSeekOpen;
                default:
                    return false;
            }
        }

        /// <summary>« Toilettage débloqué »: the build has its activities (the bootstrap sets it) and Grooming's function is open.</summary>
        private bool GroomingOpen => Config.ActivitiesAvailable && _ctx.Feature(RefugeFeature.Grooming);

        /// <summary>« Cache-cache débloqué »: the build has its activities and Hide and Seek's function is open (level 4, 8 animals).</summary>
        private bool HideSeekOpen => Config.ActivitiesAvailable && _ctx.Feature(RefugeFeature.HideAndSeek);

        /// <summary>Each shipped template's eligibility now (section 2.3, plan § 1.6), with the services of this build.</summary>
        private bool IsEligible(in WishTemplate template)
        {
            switch (template.Id)
            {
                case WishTemplateId.HatchEggs:
                case WishTemplateId.CuddleAnimal:
                case WishTemplateId.EarnBond:
                    return true;
                case WishTemplateId.HatchBiomeEggs:
                    return CollectBiomeEggBiomes() > 0;
                case WishTemplateId.HatchPreciousEgg:
                    return PreciousEggWithinReach();
                case WishTemplateId.DiscoverVariant:
                    return MissingVariantOffered();
                case WishTemplateId.RaiseSpeciesMastery:
                    return CollectMasterySpecies() > 0;
                case WishTemplateId.LookAtSpeciesInCodex:
                    return CollectCodexSpecies() > 0;
                case WishTemplateId.DiscoverSpecies:
                    return MissingSpeciesOffered();
                case WishTemplateId.TapAnimals:
                    // « Always », as long as the refuge has n different animals to tap.
                    return State.Collection.Count >= template.Amount(_tier);
                case WishTemplateId.GiveAnimalTreat:
                    return TreatsOwned() >= 1 || StandOpen();
                case WishTemplateId.GiveTreats:
                    return TreatsOwned() >= template.Amount(_tier) || StandOpen();
                case WishTemplateId.BuyUpgrades:
                case WishTemplateId.UpgradeInBiome:
                    return CollectUpgrades() > 0;
                case WishTemplateId.ExpandBiome:
                    return ExpansionWithinReach();
                case WishTemplateId.GroomAnimal:
                case WishTemplateId.GroomingStars:
                case WishTemplateId.GroomingLeaves:
                case WishTemplateId.GroomingBow:
                    // « Toilettage débloqué » (the system's opening, checked by the draw).
                    return GroomingOpen;
                case WishTemplateId.PlayHideAndSeek:
                case WishTemplateId.FindHiddenAnimals:
                case WishTemplateId.FindEveryHiddenAnimal:
                    // « Cache-cache débloqué ».
                    return HideSeekOpen;
                default:
                    return false;
            }
        }

        private bool AnyUpgradeBought()
        {
            var biomes = State.World.Biomes;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i].ProductionLevel > 0 || biomes[i].NestsLevel > 0 || biomes[i].CuriosityLevel > 0)
                    return true;
            return false;
        }

        private bool ExpansionOffered()
        {
            var biomes = State.World.Biomes;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i].Expansions > 0)
                    return true;
            var progression = _ctx.Progression;
            if (progression == null || !_ctx.Feature(RefugeFeature.ZoneSigns))
                return false;
            var config = _ctx.ProgressionConfig.Biomes;
            for (int b = 0; b < config.Length; b++)
            {
                if (!progression.Store.IsUnlocked(config[b].Id) || !progression.Zones.TryGetNext(config[b].Id, out var zone))
                    continue;
                var state = progression.Zones.GetState(zone);
                if (state == ZoneState.Available || state == ZoneState.TooExpensive)
                    return true;
            }
            return false;
        }

        /// <summary>WISH-030: the next zone of an open biome is allowed and costs at most 45 minutes of production.</summary>
        private bool ExpansionWithinReach()
        {
            var progression = _ctx.Progression;
            if (progression == null)
                return false;
            var limit = ProductionFor(ExpansionMinutes);
            if (limit.IsZero)
                return false;
            var config = _ctx.ProgressionConfig.Biomes;
            for (int b = 0; b < config.Length; b++)
            {
                if (!progression.Store.IsUnlocked(config[b].Id) || !progression.Zones.TryGetNext(config[b].Id, out var zone))
                    continue;
                var state = progression.Zones.GetState(zone);
                if ((state == ZoneState.Available || state == ZoneState.TooExpensive) && progression.Zones.Price(zone) <= limit)
                    return true;
            }
            return false;
        }

        /// <summary>WISH-025/026: upgrade levels of open biomes that cost at most 30 minutes of production (biome, kind pairs).</summary>
        private int CollectUpgrades()
        {
            var progression = _ctx.Progression;
            if (progression == null)
                return 0;
            var limit = ProductionFor(UpgradeMinutes);
            if (limit.IsZero)
                return 0;
            var upgrades = progression.Upgrades;
            var config = _ctx.ProgressionConfig.Biomes;
            int n = 0;
            for (int b = 0; b < config.Length; b++)
            {
                var biome = config[b].Id;
                if (!progression.Store.IsUnlocked(biome))
                    continue;
                for (int k = 0; k <= (int)UpgradeKind.Curiosity; k++)
                {
                    var kind = (UpgradeKind)k;
                    if (!upgrades.IsUnlocked(biome, kind))
                        continue;
                    int level = upgrades.GetLevel(biome, kind);
                    if (level >= upgrades.MaxLevel(kind) || upgrades.LevelPrice(biome, kind, level) > limit || n >= _paramIds.Length)
                        continue;
                    _paramIds[n] = biome;
                    _paramExtra[n] = (byte)k;
                    n++;
                }
            }
            return n;
        }

        /// <summary>WISH-002: open biomes other than the newest with an open Basic egg, when at least 2 biomes are open.</summary>
        private int CollectBiomeEggBiomes()
        {
            var progression = _ctx.Progression;
            if (progression == null || progression.Store.UnlockedCount() < 2)
                return 0;
            var newest = progression.Store.MostAdvancedUnlocked();
            var config = _ctx.ProgressionConfig.Biomes;
            int n = 0;
            for (int b = 0; b < config.Length && n < _paramIds.Length; b++)
            {
                var biome = config[b].Id;
                if (biome != newest && progression.Store.IsUnlocked(biome) && HasOfferedBasicEgg(biome))
                    _paramIds[n++] = biome;
            }
            return n;
        }

        /// <summary>WISH-003: an open Focus egg costs at most 15 minutes of production.</summary>
        private bool PreciousEggWithinReach()
        {
            var hatch = _ctx.Hatch;
            if (hatch == null)
                return false;
            var limit = ProductionFor(PreciousEggMinutes);
            if (limit.IsZero)
                return false;
            var eggs = _ctx.Content.Eggs;
            for (int i = 0; i < eggs.Count; i++)
            {
                var egg = eggs[i];
                if (egg.Tier == EggTier.Focus && EggOffered(egg) && hatch.GetCost(egg.Id) <= limit)
                    return true;
            }
            return false;
        }

        /// <summary>WISH-005: owned species missing at most 40 % of their next mastery level's XP.</summary>
        private int CollectMasterySpecies()
        {
            var mastery = _ctx.SpeciesMastery;
            if (mastery == null)
                return 0;
            var species = _ctx.Content.Species;
            var discovered = State.Collection.DiscoveredSpecies;
            int n = 0;
            for (int s = 0; s < species.Count && n < _paramIds.Length; s++)
            {
                var id = species[s].Id;
                if (!discovered.Contains(id))
                    continue;
                var progress = mastery.Get(id);
                if (progress.XpForNext > 0 && progress.XpForNext - progress.Xp <= MasteryNearFraction * progress.XpForNext)
                    _paramIds[n++] = id;
            }
            return n;
        }

        /// <summary>
        /// WISH-006: species discovered less than 3 days ago (their oldest animal's birth, no discovery time is stored) whose
        /// Codex page was never opened, with the Codex button open.
        /// </summary>
        private int CollectCodexSpecies()
        {
            if (!_ctx.Feature(RefugeFeature.Codex))
                return 0;
            if (!_oldestReady)
            {
                FillOldest();
                _oldestReady = true;
            }
            long now = DailyDay.Now(State, _ctx.Clock).ToUnixTimeSeconds();
            var species = _ctx.Content.Species;
            var discovered = State.Collection.DiscoveredSpecies;
            var seen = State.Codex.SeenSpecies;
            int n = 0;
            for (int s = 0; s < species.Count && s < _oldest.Length && n < _paramIds.Length; s++)
            {
                var id = species[s].Id;
                if (_oldest[s] == uint.MaxValue || now - _oldest[s] >= RecentDiscoverySeconds || !discovered.Contains(id) || seen.Contains(id))
                    continue;
                _paramIds[n++] = id;
            }
            return n;
        }

        private void FillOldest()
        {
            for (int s = 0; s < _oldest.Length; s++)
                _oldest[s] = uint.MaxValue;
            var collection = State.Collection;
            var species = _ctx.Content.Species;
            for (int i = 0; i < collection.Count; i++)
            {
                ref readonly var animal = ref collection[i];
                if (species.TryGetIndex(animal.Species, out int s) && s < _oldest.Length && animal.CreatedUtcSeconds < _oldest[s])
                    _oldest[s] = animal.CreatedUtcSeconds;
            }
        }

        /// <summary>Species in the pool of an open Basic or Focus egg (an egg of an open biome, offered by its mastery).</summary>
        private void FillOfferedSpecies()
        {
            Array.Clear(_speciesOffered, 0, _speciesOffered.Length);
            var eggs = _ctx.Content.Eggs;
            var species = _ctx.Content.Species;
            for (int e = 0; e < eggs.Count; e++)
            {
                var egg = eggs[e];
                if (!EggOffered(egg))
                    continue;
                var pool = egg.Pool;
                for (int p = 0; p < pool.Length; p++)
                    if (species.TryGetIndex(pool[p].Species, out int s) && s < _speciesOffered.Length)
                        _speciesOffered[s] = true;
            }
        }

        /// <summary>WISH-007: a normal species not discovered yet in an open egg.</summary>
        private bool MissingSpeciesOffered()
        {
            var species = _ctx.Content.Species;
            var discovered = State.Collection.DiscoveredSpecies;
            for (int s = 0; s < species.Count && s < _speciesOffered.Length; s++)
                if (_speciesOffered[s] && !species[s].IsSecret && !discovered.Contains(species[s].Id))
                    return true;
            return false;
        }

        /// <summary>WISH-004: a base (non-secret) variant not found yet, of a species an open egg gives.</summary>
        private bool MissingVariantOffered()
        {
            var variants = _ctx.Content.Variants;
            var species = _ctx.Content.Species;
            var discovered = State.Collection.DiscoveredVariants;
            for (int v = 0; v < variants.Count; v++)
            {
                var variant = variants[v];
                if (variant.IsSecret || discovered.Contains(variant.Id))
                    continue;
                if (species.TryGetIndex(variant.Species, out int s) && s < _speciesOffered.Length && _speciesOffered[s])
                    return true;
            }
            return false;
        }

        private bool EggOffered(EggDefinition egg)
        {
            if (egg.Tier != EggTier.Basic && egg.Tier != EggTier.Focus)
                return false;
            if (!BiomeOpen(egg.Biome))
                return false;
            var hatch = _ctx.Hatch;
            return hatch != null ? hatch.IsOffered(egg.Id) : egg.UnlockBiomeMastery <= 0;
        }

        private bool HasOfferedBasicEgg(ContentId biome)
        {
            var eggs = _ctx.Content.Eggs;
            for (int i = 0; i < eggs.Count; i++)
                if (eggs[i].Tier == EggTier.Basic && eggs[i].Biome == biome && EggOffered(eggs[i]))
                    return true;
            return false;
        }

        private bool BiomeOpen(ContentId biome)
        {
            var progression = _ctx.Progression;
            if (progression != null)
                return progression.Store.IsUnlocked(biome);
            var biomes = State.World.Biomes;
            if (biomes.Count == 0)
                return true;
            for (int i = 0; i < biomes.Count; i++)
                if (biomes[i].Biome == biome)
                    return biomes[i].Unlocked;
            return false;
        }

        private int TreatsOwned()
        {
            int total = 0;
            foreach (var pair in State.Progression.Treats)
                if (pair.Value > 0)
                    total += pair.Value;
            return total;
        }

        /// <summary>The treat stand (« épicerie ») lives in the shop: open with the shop, after the welcome.</summary>
        private bool StandOpen() => State.IsOnboardingDone && _ctx.Feature(RefugeFeature.Shop);

        private BigValue ProductionFor(double minutes) => BigValue.Multiply(_ctx.Economy.GetLightPerSecond(), minutes * 60.0);

        // ------------------------------------------------------------------------------------------- tutorial

        /// <summary>
        /// WISH-T01 « Fais éclore 2 œufs » (welcome step A08): the first animal asks for 2 eggs, outside the daily draw. It
        /// completes after 2 hatches even without a touch, which ends the welcome (doc 01, section 1.3). Once only.
        /// </summary>
        public void StartTutorialWish(uint firstAnimal)
        {
            var visit = State.DailyVisit;
            if (visit.OnboardingDone || visit.TutorialStep >= 2 || TutorialSlot() >= 0)
                return;
            int pending = Pending;
            if (pending >= visit.Wishes.Length)
                return;
            visit.Wishes[pending] = new WishRecord
            {
                Template = (ushort)WishTemplateId.Tutorial,
                Animal = firstAnimal,
                Target = Math.Max(1, WishCatalog.Tutorial.P1),
                DrawnDay = _ctx.TodayKey,
                Flags = WishRecord.FlagTutorial,
            };
            visit.WishCount = pending + 1;
            visit.TutorialStep = 2;
            StampDrawn(pending);
            _tier = WishCatalog.TierOf(_ctx.RefugeLevel);
            _ctx.RequestSave(SaveUrgency.Priority);
            RaiseDrawn(pending, false, pending + 1);
        }

        private int TutorialSlot()
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = 0; i < Pending; i++)
                if (wishes[i].IsTutorial)
                    return i;
            return -1;
        }

        // ------------------------------------------------------------------------------------------- claims

        /// <summary>
        /// Gives a done wish (sections 2.1, 2.6): 3 Gems within the cumulative cap (9 per day since install; the tutorial
        /// wish is outside the daily wishes' cap), +15 bond, 1 treat of the animal's biome, biome and species mastery XP.
        /// The wish leaves the stock (kept compact) before anything is given.
        /// </summary>
        public bool TryClaim(int slot, WishClaimPath path, out WishClaim claim)
        {
            claim = default;
            var visit = State.DailyVisit;
            if ((uint)slot >= (uint)Pending)
                return false;
            var wish = visit.Wishes[slot];
            if (!wish.IsDone)
                return false;
            double minutes = MinutesFromDraw(slot, wish.DrawnDay);
            RemoveAt(slot);

            ContentId species = default, biome = default;
            if (State.Collection.TryGet(wish.Animal, out var animal))
            {
                species = animal.Species;
                if (_ctx.Content.Species.TryGet(species, out var definition))
                    biome = definition.Biome;
            }
            var treat = TreatOf(biome);

            long gems = Math.Max(0L, Config.WishGems);
            if (!wish.IsTutorial)
            {
                long cap = Config.WishGemCapPerDay * Math.Max(1, _ctx.DayNumber);
                gems = Math.Min(gems, Math.Max(0L, cap - visit.WishGemsTotal));
                visit.WishGemsTotal += gems;
            }
            var bundle = new RewardBundle { Gems = gems, Treat = treat, Treats = treat.IsValid ? 1 : 0 };
            _ctx.Granter.Grant(bundle, EconomyReason.Wish, DiscoverySource.Debug);
            int bond = _bond != null ? _bond.AddBond(wish.Animal, Config.WishBond, BondSource.Wish) : 0;
            if (biome.IsValid)
                _ctx.Progression?.Mastery.Add(biome, BiomeXpSource.Wish);
            if (species.IsValid)
                _ctx.SpeciesMastery?.AddXp(species, MasteryReason.Wish, Config.WishSpeciesXp);
            visit.WishesCompleted++;
            _ctx.RequestSave(SaveUrgency.Priority);

            claim = new WishClaim(slot, wish, gems, treat, bond, path, species, biome, Pending, minutes);
            Claimed?.Invoke(claim);
            return true;
        }

        private double NowSeconds() => _ctx.Clock.UtcNow.ToUnixTimeSeconds();

        private void StampDrawn(int slot)
        {
            if ((uint)slot >= (uint)_drawnAt.Length)
                return;
            _drawnAt[slot] = NowSeconds();
            _doneAt[slot] = double.NaN;
        }

        /// <summary>
        /// Draw to completion, in minutes: the times stamped while this service ran, else the start of the draw day (local,
        /// reset hour) and the claim time. Never negative; -1 when the draw day is not a real date.
        /// </summary>
        private double MinutesFromDraw(int slot, int drawnDay)
        {
            bool known = (uint)slot < (uint)_drawnAt.Length;
            double drawn = known ? _drawnAt[slot] : double.NaN;
            double done = known ? _doneAt[slot] : double.NaN;
            if (double.IsNaN(done))
                done = NowSeconds();
            if (double.IsNaN(drawn))
            {
                if (!ValidKey(drawnDay))
                    return -1.0;
                var local = DailyDay.ToDate(drawnDay).AddHours(Config.ResetHour);
                drawn = new DateTimeOffset(local, _ctx.Zone.GetUtcOffset(local)).ToUnixTimeSeconds();
            }
            return Math.Max(0.0, (done - drawn) / 60.0);
        }

        /// <summary>Claims every done wish (« Tout récupérer »); returns how many.</summary>
        public int ClaimAll(WishClaimPath path)
        {
            int claimed = 0;
            bool again = true;
            // A claim can complete another wish (bond, mastery): pass again until none is done.
            while (again)
            {
                again = false;
                for (int i = 0; i < Pending;)
                {
                    if (State.DailyVisit.Wishes[i].IsDone && TryClaim(i, path, out _))
                    {
                        claimed++;
                        again = true;
                        continue;
                    }
                    i++;
                }
            }
            return claimed;
        }

        /// <summary>Done but unclaimed wishes are given at the next Today opening (section 2.6).</summary>
        public int AutoClaimOnTodayOpen() => ClaimAll(WishClaimPath.Auto);

        private ContentId TreatOf(ContentId biome)
        {
            if (biome.IsValid && _ctx.ProgressionConfig.TryGetBiome(biome, out var economy) && economy.Treat.IsValid)
                return economy.Treat;
            return _ctx.NewestTreat();
        }

        private void RemoveAt(int slot)
        {
            var visit = State.DailyVisit;
            var wishes = visit.Wishes;
            int last = Pending - 1;
            var removed = (WishTemplateId)wishes[slot].Template;
            for (int i = slot; i < last; i++)
            {
                wishes[i] = wishes[i + 1];
                if (i + 1 < _drawnAt.Length)
                {
                    _drawnAt[i] = _drawnAt[i + 1];
                    _doneAt[i] = _doneAt[i + 1];
                }
            }
            wishes[last] = default;
            if (last < _drawnAt.Length)
            {
                _drawnAt[last] = double.NaN;
                _doneAt[last] = double.NaN;
            }
            visit.WishCount = last;
            if (removed == WishTemplateId.UpgradeInBiome)
                _upgradeBaselineValid = false;
        }

        // ------------------------------------------------------------------------------------------- progress hooks

        /// <summary>WISH-001 (any egg), 002 (an egg of its biome), 003 (a Focus egg), 004 (a new variant or species), 007 (a new
        /// species) and the tutorial wish. A × 10 calls this ten times.</summary>
        public void OnHatched(in HatchResult result)
        {
            if (!result.Succeeded)
                return;
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
            {
                if (i >= Pending || wishes[i].IsDone)
                    continue;
                switch ((WishTemplateId)wishes[i].Template)
                {
                    case WishTemplateId.Tutorial:
                    case WishTemplateId.HatchEggs:
                        Advance(i, 1);
                        break;
                    case WishTemplateId.HatchBiomeEggs:
                        if (_ctx.Content.Eggs.TryGet(result.Egg, out var biomeEgg) && biomeEgg.Tier != EggTier.Discovery
                            && biomeEgg.Biome == wishes[i].Param)
                            Advance(i, 1);
                        break;
                    case WishTemplateId.HatchPreciousEgg:
                        if (_ctx.Content.Eggs.TryGet(result.Egg, out var focusEgg) && focusEgg.Tier == EggTier.Focus)
                            Advance(i, 1);
                        break;
                    case WishTemplateId.DiscoverVariant:
                        if (result.NewVariant || result.NewSpecies)
                            Advance(i, 1);
                        break;
                    case WishTemplateId.DiscoverSpecies:
                        if (result.NewSpecies)
                            Advance(i, 1);
                        break;
                }
            }
        }

        /// <summary>WISH-005: its species gained levels.</summary>
        public void OnMasteryLeveled(ContentId species, int levels)
        {
            if (levels <= 0)
                return;
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
                if (i < Pending && !wishes[i].IsDone && (WishTemplateId)wishes[i].Template == WishTemplateId.RaiseSpeciesMastery
                    && wishes[i].Param == species)
                    Advance(i, levels);
        }

        /// <summary>WISH-006: its species' Codex page was opened.</summary>
        public void OnCodexPageOpened(ContentId species)
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
                if (i < Pending && !wishes[i].IsDone && (WishTemplateId)wishes[i].Template == WishTemplateId.LookAtSpeciesInCodex
                    && wishes[i].Param == species)
                    Advance(i, 1);
        }

        /// <summary>WISH-014 (n different animals, any touch) and WISH-015 (its animal, touches at least 2 s apart).</summary>
        public void OnAnimalTouched(uint animal, bool countedForBond)
        {
            var visit = State.DailyVisit;
            var wishes = visit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
            {
                if (i >= Pending || wishes[i].IsDone)
                    continue;
                switch ((WishTemplateId)wishes[i].Template)
                {
                    case WishTemplateId.TapAnimals:
                        if (TouchedAlready(animal))
                            break;
                        if (visit.TouchedCount < visit.TouchedForWish.Length)
                            visit.TouchedForWish[visit.TouchedCount++] = animal;
                        Advance(i, 1);
                        break;
                    case WishTemplateId.CuddleAnimal:
                        if (wishes[i].Animal != animal)
                            break;
                        double now = _ctx.Clock.SessionSeconds;
                        if (now - _lastCuddle < CuddleSpacingSeconds)
                            break;
                        _lastCuddle = now;
                        Advance(i, 1);
                        break;
                }
            }
        }

        /// <summary>WISH-016 (a treat to its animal) and WISH-017 (any treat given).</summary>
        public void OnTreatGiven(uint animal)
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
            {
                if (i >= Pending || wishes[i].IsDone)
                    continue;
                var template = (WishTemplateId)wishes[i].Template;
                if (template == WishTemplateId.GiveTreats || (template == WishTemplateId.GiveAnimalTreat && wishes[i].Animal == animal))
                    Advance(i, 1);
            }
        }

        /// <summary>WISH-018: bond points earned by any animal.</summary>
        public void OnBondGained(uint animal, int points)
        {
            if (points <= 0)
                return;
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
                if (i < Pending && !wishes[i].IsDone && (WishTemplateId)wishes[i].Template == WishTemplateId.EarnBond)
                    Advance(i, points);
        }

        /// <summary>
        /// WISH-025: every level bought counts (a × 10 or Max counts them all). WISH-026: the levels its upgrade gained since
        /// the wish appeared (read from the upgrade itself; a Renewal that set it back is taken into account).
        /// </summary>
        public void OnUpgradesBought(int levels)
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
            {
                if (i >= Pending || wishes[i].IsDone)
                    continue;
                switch ((WishTemplateId)wishes[i].Template)
                {
                    case WishTemplateId.BuyUpgrades:
                        if (levels > 0)
                            Advance(i, levels);
                        break;
                    case WishTemplateId.UpgradeInBiome:
                        int gained = UpgradeLevelsGained(wishes[i]);
                        if (gained > 0)
                            Advance(i, gained);
                        break;
                }
            }
        }

        /// <summary>
        /// An upgrade purchase with its biome and kind (the facade calls it before <see cref="OnUpgradesBought"/>): WISH-026
        /// counts the levels of its own upgrade here, and the level-reading fallback of <see cref="OnUpgradesBought"/> then sees
        /// nothing new, so a level never counts twice. The first upgrade bought opens SYS-04 (section 2.2).
        /// </summary>
        public void OnUpgrade(in UpgradePurchase purchase)
        {
            NoteSystemOpen(WishSystem.Upgrades);
            int levels = purchase.LevelTo - purchase.LevelFrom;
            if (levels <= 0)
                return;
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
            {
                if (i >= Pending || wishes[i].IsDone || (WishTemplateId)wishes[i].Template != WishTemplateId.UpgradeInBiome)
                    continue;
                if (wishes[i].Param != purchase.Biome || wishes[i].ParamExtra != (byte)purchase.Kind)
                    continue;
                _upgradeBaseline = purchase.LevelTo;
                _upgradeBaselineValid = true;
                Advance(i, levels);
            }
        }

        /// <summary>
        /// A zone opened, with its details (the facade calls it next to <see cref="OnZoneOpened"/>, which counts WISH-030): an
        /// expansion bought means SYS-06 is open (section 2.2).
        /// </summary>
        public void OnZone(in ZoneOpening opening) => NoteSystemOpen(WishSystem.Expansions);

        /// <summary>WISH-030: a zone opened.</summary>
        public void OnZoneOpened()
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
                if (i < Pending && !wishes[i].IsDone && (WishTemplateId)wishes[i].Template == WishTemplateId.ExpandBiome)
                    Advance(i, 1);
        }

        /// <summary>
        /// T38 (SYS-09, section 2.3): a grooming game ended. WISH-019 « Brosse {animal} »: a game finished with its animal,
        /// practice included; WISH-020: the stars of a rewarded game (cumulated); WISH-021: the debris and burrs taken off,
        /// in any game, even one left before its end (cumulated: the leaves were really picked off); WISH-022: a bow placed.
        /// Allocation-free.
        /// </summary>
        public void OnGroomingGame(in GroomingGameSummary game)
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
            {
                if (i >= Pending || wishes[i].IsDone)
                    continue;
                switch ((WishTemplateId)wishes[i].Template)
                {
                    case WishTemplateId.GroomAnimal:
                        if (game.Finished && game.Animal != 0 && wishes[i].Animal == game.Animal)
                            Advance(i, 1);
                        break;
                    case WishTemplateId.GroomingStars:
                        if (game.Finished && game.Rewarded && game.Stars > 0)
                            Advance(i, game.Stars);
                        break;
                    case WishTemplateId.GroomingLeaves:
                        if (game.DebrisRemoved > 0)
                            Advance(i, game.DebrisRemoved);
                        break;
                    case WishTemplateId.GroomingBow:
                        if (game.Finished && game.BowPlaced)
                            Advance(i, 1);
                        break;
                }
            }
        }

        /// <summary>
        /// T39 (SYS-10, section 2.3): a hide-and-seek game ended. WISH-051 « Joue à Cache-cache »: a game played to its end,
        /// practice included (a game left early is not a game played); WISH-052 « Trouve {n} animaux cachés »: the animals
        /// found in any game, even one left before its end (cumulated: they really were found); WISH-053 « Trouve tous les
        /// animaux d'une partie »: every animal of one game found, practice included. Allocation-free.
        /// </summary>
        public void OnHideSeekGame(in HideSeekGameSummary game)
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = Pending - 1; i >= 0; i--)
            {
                if (i >= Pending || wishes[i].IsDone)
                    continue;
                switch ((WishTemplateId)wishes[i].Template)
                {
                    case WishTemplateId.PlayHideAndSeek:
                        if (game.Finished)
                            Advance(i, 1);
                        break;
                    case WishTemplateId.FindHiddenAnimals:
                        if (game.Found > 0)
                            Advance(i, game.Found);
                        break;
                    case WishTemplateId.FindEveryHiddenAnimal:
                        if (game.Finished && game.AllFound)
                            Advance(i, 1);
                        break;
                }
            }
        }

        /// <summary>
        /// The animal of a pending WISH-019 « Brosse {animal} », for the hub to feature it (false: none). Allocation-free.
        /// </summary>
        public bool GroomWishAnimal(out uint animal)
        {
            var wishes = State.DailyVisit.Wishes;
            for (int i = 0; i < Pending; i++)
            {
                if (wishes[i].IsDone || (WishTemplateId)wishes[i].Template != WishTemplateId.GroomAnimal)
                    continue;
                if (!State.Collection.TryGetIndex(wishes[i].Animal, out _))
                    continue;
                animal = wishes[i].Animal;
                return true;
            }
            animal = 0;
            return false;
        }

        /// <summary>No wish of this build counts the rendez-vous themselves.</summary>
        public void OnWishCompleted()
        {
        }

        public void OnGiftOpened()
        {
        }

        public void OnCalendarStamped()
        {
        }

        /// <summary>
        /// Adds progress to a wish (capped at its target). Done: flagged, and the tutorial wish ends the welcome (TutorialStep
        /// 3) even without a touch. Raises <see cref="Progressed"/> or <see cref="Completed"/>; callers walk the stock from
        /// the end, so a handler that claims this wish never shifts a wish still to visit.
        /// </summary>
        private void Advance(int slot, long amount)
        {
            var visit = State.DailyVisit;
            ref var wish = ref visit.Wishes[slot];
            if (wish.IsDone || amount <= 0)
                return;
            int target = Math.Max(1, wish.Target);
            wish.Progress = (int)Math.Min(target, Math.Max(0, wish.Progress) + amount);
            bool done = wish.Progress >= target;
            bool welcomeOver = false;
            if (done)
            {
                wish.Flags |= WishRecord.FlagDone;
                if ((uint)slot < (uint)_doneAt.Length)
                    _doneAt[slot] = NowSeconds();
                if (wish.IsTutorial)
                {
                    welcomeOver = !visit.OnboardingDone;
                    visit.OnboardingDone = true;
                    visit.TutorialStep = 3;
                }
            }
            _ctx.RequestSave(done ? SaveUrgency.Priority : SaveUrgency.Normal);
            if (!done)
            {
                Progressed?.Invoke(slot);
                return;
            }
            Completed?.Invoke(slot);
            if (welcomeOver)
                OnboardingFinished?.Invoke();
        }

        /// <summary>A system just proved open (an upgrade bought, a zone opened): its opening day, once, after the welcome.</summary>
        private void NoteSystemOpen(WishSystem system)
        {
            var unlock = State.DailyVisit.SystemUnlockDay;
            int s = (int)system;
            if (s <= 0 || s >= unlock.Length || unlock[s] > 0 || !State.IsOnboardingDone)
                return;
            unlock[s] = Math.Max(1, _ctx.DayNumber);
        }

        private bool TouchedAlready(uint animal)
        {
            var visit = State.DailyVisit;
            int count = Math.Min(visit.TouchedCount, visit.TouchedForWish.Length);
            for (int i = 0; i < count; i++)
                if (visit.TouchedForWish[i] == animal)
                    return true;
            return false;
        }

        private int UpgradeLevelsGained(in WishRecord wish)
        {
            var progression = _ctx.Progression;
            if (progression == null)
                return 0;
            int level = progression.Upgrades.GetLevel(wish.Param, (UpgradeKind)wish.ParamExtra);
            if (!_upgradeBaselineValid)
            {
                // Unknown starting point (a wish written from outside): count from now.
                _upgradeBaseline = level;
                _upgradeBaselineValid = true;
                return 0;
            }
            // Below the baseline, a Renewal set Production and Cozy Nests back to 0: what it has now was bought since.
            int gained = level >= _upgradeBaseline ? level - _upgradeBaseline : level;
            _upgradeBaseline = level;
            return gained;
        }

        /// <summary>A pending WISH-026 (loaded from a save) counts the levels bought from now on.</summary>
        private void TakeUpgradeBaseline()
        {
            _upgradeBaselineValid = false;
            var wishes = State.DailyVisit.Wishes;
            for (int i = 0; i < Pending; i++)
            {
                if ((WishTemplateId)wishes[i].Template == WishTemplateId.UpgradeInBiome && !wishes[i].IsDone)
                {
                    TakeUpgradeBaseline(wishes[i]);
                    return;
                }
            }
        }

        private void TakeUpgradeBaseline(in WishRecord wish)
        {
            var progression = _ctx.Progression;
            _upgradeBaselineValid = progression != null;
            _upgradeBaseline = progression != null ? progression.Upgrades.GetLevel(wish.Param, (UpgradeKind)wish.ParamExtra) : 0;
        }

        /// <summary>Days between two day keys; int.MaxValue when either key is not a real date (a damaged save).</summary>
        private static int DaysSince(int fromKey, int toKey) =>
            ValidKey(fromKey) && ValidKey(toKey) ? DailyDay.DaysBetween(fromKey, toKey) : int.MaxValue;

        private static bool ValidKey(int key)
        {
            int year = key / 10000, month = key / 100 % 100, day = key % 100;
            return year >= 1 && year <= 9999 && month >= 1 && month <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);
        }
    }
}
