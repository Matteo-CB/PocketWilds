#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Editor.Economy
{
    /// <summary>
    /// How the simulated player spends Light. <see cref="Guided"/> (the default, the one the guardrails are checked with)
    /// follows the game's cues; <see cref="Optimizer"/> buys whatever pays itself back fastest, a sensitivity check.
    /// </summary>
    public enum SimPolicy
    {
        Guided = 0,
        Optimizer = 1,
    }

    /// <summary>
    /// Changes the docs do not make, for the owner's decisions (the report measures each one): never used by the guardrails.
    /// </summary>
    public sealed class WhatIf
    {
        public static readonly WhatIf None = new WhatIf();

        /// <summary>The Renewal keeps the egg counters (doc 02 Renouveau, section 1.4, resets them).</summary>
        public bool KeepEggCounters;
    }

    /// <summary>What a simulated day ends with (one CSV row per day and profile).</summary>
    public sealed class SimDay
    {
        public int Day;
        public double PlayMinutes;
        public double LightLog10;
        public double RateLog10;
        public double LifetimeLog10;
        public int Hatches;
        public int NewSpecies;
        public int NewVariants;
        public int MasteryVariants;
        public int EventObjects;
        public int PassVariants;
        public int Traits;
        public int Visitors;
        public int Stickers;
        public int Biomes;
        public int Zones;
        public int RefugeLevel;
        public long RefugeScore;
        public int BiomeMastery;
        public int MaxSpeciesMastery;

        /// <summary>Species XP earned in all by the most hatched species (its mastery progress).</summary>
        public long TopSpeciesXp;
        public long GemsEarned;
        public long GemsSpent;
        public long Gems;
        public long Essence;
        public int Renewals;
        public int Goals;
        public double EggCostMaxMinutes;
        public int BaseVariantsFound;

        /// <summary>What still holds the next biome's gate at the end of the day (empty when every biome is open).</summary>
        public string NextGate = string.Empty;

        public int Discoveries => NewSpecies + NewVariants + MasteryVariants + EventObjects + PassVariants + Traits + Visitors;
    }

    /// <summary>A moment worth reporting (a biome, a zone, a refuge level, a Renewal) with its day and cumulative play time.</summary>
    public readonly struct SimMilestone
    {
        public readonly string Kind;
        public readonly string What;
        public readonly double Day;
        public readonly double PlayMinutes;
        public readonly long Value;

        public SimMilestone(string kind, string what, double day, double playMinutes, long value)
        {
            Kind = kind;
            What = what;
            Day = day;
            PlayMinutes = playMinutes;
            Value = value;
        }
    }

    public enum SpendKind
    {
        Eggs = 0,
        Upgrades = 1,
        Zones = 2,
        Gates = 3,
        Decorations = 4,
        Treats = 5,
    }

    /// <summary>One profile's 120 days.</summary>
    public sealed class SimRun
    {
        public SimProfile Profile = null!;
        public readonly List<SimDay> Days = new List<SimDay>();
        public readonly List<SimMilestone> Milestones = new List<SimMilestone>();

        /// <summary>Light spent per kind over days 1 to 30 (doc 02 Améliorations, section 1).</summary>
        public readonly double[] Spend30 = new double[6];

        public double Milliseconds;

        public double SpendShare30(SpendKind kind)
        {
            double total = 0.0;
            for (int i = 0; i < Spend30.Length; i++)
                total += Spend30[i];
            return total > 0.0 ? Spend30[(int)kind] / total : 0.0;
        }

        /// <summary>First milestone of a kind and name, or null.</summary>
        public SimMilestone? First(string kind, string what)
        {
            foreach (var m in Milestones)
                if (m.Kind == kind && m.What == what)
                    return m;
            return null;
        }
    }

    /// <summary>
    /// T35C economy simulator (doc 02 Gemmes, section 7; doc 02 Améliorations, section 17). Pure C#, no scene: it plays
    /// 120 days for 6 profiles with the game's own services (EconomyService, HatchService, MasteryService,
    /// ProgressionServices with the upgrades, zones, gates, refuge level, biome mastery, decorations, treats, Renewal and
    /// Essence tree), on the shipped content completed by <see cref="SimContent"/>. Time moves in aggregated steps:
    /// offline breaks are paid with the offline formula, and inside a session the clock jumps straight to the next
    /// purchase.
    /// <para>
    /// The player inside a session (same for every profile): opens a biome as soon as it can, then spends on the option
    /// that pays itself back fastest (an egg: its animal's production, three times worth while its pool still hides
    /// something new; a Production level: the production it adds; Cozy Nests: the savings on the next ten eggs over ten
    /// minutes; a zone: its × 1.25 and its gift), saving for it when it is within reach of the session; Light decorations
    /// and treats when they are cheap; Discovery eggs at once; a Renewal as soon as its button is active, Essence on the
    /// cheapest open node of the Economy branch first. Each hatch takes its sequence's time (2.6 s for the first 20, then
    /// 1.2 s, shared by a × 3 or × 10) and the tap.
    /// </para>
    /// <para>
    /// Systems planned but not built yet give their refuge points and biome mastery XP as their docs say (doc 02
    /// Améliorations, sections 6.1 and 7.1): traits at the doc's rates, rare visitors, stickers and album pages, Light
    /// decorations placed, wishes. Their points are added to the score the real RefugeLevelService computes. Breeding
    /// (T56) is not simulated yet.
    /// </para>
    /// </summary>
    public static class EconomySimulator
    {
        public const int DefaultDays = 120;

        /// <summary>Optional trace of every purchase (play minutes, what, price, Light per second), to read a run step by step.</summary>
        public static Action<string>? Trace;
        public static readonly DateTimeOffset Install = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

        /// <summary>Hatches the whole profile list on fresh configurations; <paramref name="tune"/> may adjust them first.</summary>
        public static List<SimRun> RunAll(ContentRegistry shipped, int days = DefaultDays,
            Action<ProgressionConfig, EconomyConfig, RenewalConfig>? tune = null, SimProfile[]? profiles = null, SimPolicy policy = SimPolicy.Guided)
        {
            var runs = new List<SimRun>();
            foreach (var profile in profiles ?? SimProfile.All())
                runs.Add(Run(shipped, profile, days, tune, policy));
            return runs;
        }

        public static SimRun Run(ContentRegistry shipped, SimProfile profile, int days = DefaultDays,
            Action<ProgressionConfig, EconomyConfig, RenewalConfig>? tune = null, SimPolicy policy = SimPolicy.Guided, WhatIf? whatIf = null)
        {
            var watch = Stopwatch.StartNew();
            var player = new Player(shipped, profile, tune, policy, whatIf ?? WhatIf.None);
            for (int day = 1; day <= days; day++)
                player.PlayDay(day);
            player.Result.Milliseconds = watch.Elapsed.TotalMilliseconds;
            return player.Result;
        }

        /// <summary>One profile's player and world.</summary>
        private sealed class Player
        {
            private const double DiscoveryWeight = 2.0;
            private const double CuriosityMinutes = 30.0;
            private const double TreatMinutes = 5.0;
            private const double NestsMinutes = 10.0;

            /// <summary>
            /// Gems of a species mastery milestone (every 5 levels): 2 since T35C, the mastery curve being about 8 times faster
            /// than the 5 Gems of doc 02 Formules assumed (the Club keeps about twice a free player's Gems).
            /// </summary>
            private const long MasteryMilestoneGems = 2;

            /// <summary>Minutes the guided player would wait for the next egg before turning to the Upgrade screen meanwhile.</summary>
            private const double PatienceMinutes = 10.0;

            /// <summary>The HUD's « amélioration conseillée » dot: a level worth at least 5 % of total production (doc 02 Améliorations, section 3.6).</summary>
            private const double AdvisedShare = 0.05;

            /// <summary>Share of the Essence earned so far that a later Renewal must add to be worth it.</summary>
            private const double RentableShare = 0.5;

            private readonly SimProfile _profile;
            private readonly SimPolicy _policy;
            private readonly WhatIf _whatIf;
            private readonly ProgressionConfig _config;
            private readonly EconomyConfig _economyConfig;
            private readonly ContentRegistry _content;
            private readonly PlayerState _state;
            private readonly FakeGameClock _clock;
            private readonly EconomyService _economy;
            private readonly MasteryService _mastery;
            private readonly HatchService _hatch;
            private readonly OfflineService _offline;
            private readonly ProgressionServices _p;
            private readonly Random _random;
            private readonly List<ContentId> _eggs = new List<ContentId>();
            private readonly Dictionary<ContentId, List<ContentId>> _baseVariants = new Dictionary<ContentId, List<ContentId>>();
            private readonly double[] _rarityValue = new double[6];
            private readonly ContentId[] _targets = new ContentId[EggOdds.MaxTargets];

            private SimDay _today = new SimDay();
            private DateTimeOffset _lastEnd;
            private int _loginDays;
            private int _giftsWithoutBig;
            private int _giftsOpened;
            private int _welcomeClaimed;
            private int _calendarStamps;
            private DeterministicRandom _giftRng;
            private readonly DailyVisitConfig _daily = new DailyVisitConfig();
            private int _longExpeditionsWithoutEgg;
            private readonly List<(DateTimeOffset end, double hours)> _expeditions = new List<(DateTimeOffset, double)>();
            private int _expeditionsToday;
            private int _activitiesToday;
            private int _lastRefugeLevel = 1;
            private int _lastBiomeMasterySum;
            private readonly HashSet<string> _seen = new HashSet<string>();

            // Time the player spends on an action (a hatch sequence, a screen) before the next one.
            private double _busy;

            // Planned systems (doc 02 Améliorations, section 6.1): their points join the refuge score.
            private long _plannedScore;
            private readonly HashSet<long> _traitsFound = new HashSet<long>();
            private int _visitorsJoined;
            private int _visitorMisses;
            private readonly int[] _albumStickers = new int[16];
            private int _decorationsPlaced;
            private int _wishBiome;

            /// <summary>Trait rates of doc 02 Formules: Tiny, Giant, Shiny, Golden, Albino, Melanistic.</summary>
            private static readonly double[] TraitRates = { 0.0040, 0.0025, 0.0012, 0.00025, 0.0008, 0.0008 };

            private const int Visitors = 13;
            private const double VisitorContactsPerDay = 0.5;
            private const int StickersPerPage = 9;
            private const int PagesPerAlbum = 5;
            private const double ActivityStickerChance = 0.15;

            public readonly SimRun Result;

            public Player(ContentRegistry shipped, SimProfile profile, Action<ProgressionConfig, EconomyConfig, RenewalConfig>? tune, SimPolicy policy,
                WhatIf whatIf)
            {
                _profile = profile;
                _policy = policy;
                _whatIf = whatIf;
                Result = new SimRun { Profile = profile };
                _config = new ProgressionConfig { TimeZone = TimeZoneInfo.Utc };
                _config.Zones = SimContent.Zones(_config);
                _economyConfig = new EconomyConfig();
                if (profile.Club)
                    _economyConfig.OfflineFullSeconds = 36.0 * 3600.0;
                var renewal = new RenewalConfig();
                tune?.Invoke(_config, _economyConfig, renewal);
                _content = SimContent.Build(shipped, _config, out _);
                _random = new Random(1000 + (int)profile.Kind);
                _giftRng = new DeterministicRandom(SeedHash.Mix(0x7035C000u + (uint)profile.Kind, DailyGiftService.RollSalt));

                _state = PlayerState.CreateNew(Install);
                // A fixed profile id per profile: the hatch draws derive from it (a new save draws a random one).
                _state.Meta.ProfileId = new Guid(0x7035C000 + (int)profile.Kind, 0, 0, new byte[8]);
                _clock = new FakeGameClock(Install);
                _economy = new EconomyService(_state, _content, _economyConfig, null);
                _mastery = new MasteryService(_state, _economyConfig, _economy);
                _hatch = new HatchService(_state, _content, _economy, _clock, null, _mastery);
                _offline = new OfflineService(_state, _economy, _clock);
                _p = new ProgressionServices(_state, _content, _economy, _hatch, _clock, null, _config, _mastery, _offline, renewal);
                _lastEnd = Install;

                for (int i = 0; i < _content.Eggs.Count; i++)
                    if (_content.Eggs[i].Tier == EggTier.Basic || _content.Eggs[i].Tier == EggTier.Focus)
                        _eggs.Add(_content.Eggs[i].Id);
                for (int i = 0; i < _content.Variants.Count; i++)
                {
                    var v = _content.Variants[i];
                    if (v.IsSecret)
                        continue;
                    if (!_baseVariants.TryGetValue(v.Species, out var list))
                        _baseVariants[v.Species] = list = new List<ContentId>();
                    list.Add(v.Id);
                }
                for (int r = 0; r < _rarityValue.Length; r++)
                    _rarityValue[r] = _economyConfig.RarityMultipliers[r];
            }

            /// <summary>
            /// The calendar day with its fraction, as the docs count days (« jour n », the install day is day 1): 1.5 is noon
            /// of the install day, 3.25 is 6 am on day 3.
            /// </summary>
            private double Today => _clock.UtcNow.Subtract(new DateTimeOffset(Install.Date, TimeSpan.Zero)).TotalDays + 1.0;
            private double PlayMinutes => _state.Meta.TotalPlaySeconds / 60.0;
            private double Rate => _economy.GetLightPerSecond().ToDoubleClamped();
            private double Light => _economy.Light.ToDoubleClamped();

            public void PlayDay(int day)
            {
                _today = new SimDay { Day = day };
                _expeditionsToday = 0;
                _activitiesToday = 0;
                double[] hours = day == 1 ? _profile.FirstDayHours : _profile.OddDayHours != null && day % 2 == 1 ? _profile.OddDayHours : _profile.DayHours;
                var dayStart = Install.Date.AddDays(day - 1);
                for (int s = 0; s < hours.Length; s++)
                {
                    var start = new DateTimeOffset(dayStart, TimeSpan.Zero).AddHours(hours[s]);
                    if (start < _lastEnd)
                        start = _lastEnd;
                    GoOffline(start);
                    if (s == 0)
                        Login(day);
                    double minutes = day == 1 ? _profile.FirstDayMinutes[s] : _profile.SessionMinutes;
                    Session(minutes * 60.0, hours.Length - s);
                    _lastEnd = _clock.UtcNow;
                }
                EndDay();
            }

            // ---------------- Time ----------------

            private void GoOffline(DateTimeOffset start)
            {
                double away = (start - _lastEnd).TotalSeconds;
                if (away > 0.0)
                {
                    double effective = OfflineService.EffectiveSeconds(away, _economyConfig, _offline.ExtraFullSeconds, _offline.FloorBonus);
                    var gain = BigValue.Multiply(_economy.GetLightPerSecond(), effective);
                    if (!gain.IsZero)
                        _economy.Grant(CurrencyId.Light, gain, EconomyReason.Offline);
                }
                _clock.Set(start);
            }

            private void Advance(double seconds)
            {
                if (seconds <= 0.0)
                    return;
                _economy.Advance(seconds);
                _clock.Advance(TimeSpan.FromSeconds(seconds));
                _state.Meta.TotalPlaySeconds += seconds;
                _today.PlayMinutes += seconds / 60.0;
            }

            // ---------------- The day's rendez-vous (doc 01 Contenu; doc 02 Gemmes, section 2) ----------------

            private void Login(int day)
            {
                _loginDays++;
                if (_profile.Club)
                    EarnGems(day == 1 ? 340 : 40);
                if (_profile.GemsBoughtPerMonth > 0 && (day - 1) % 30 == 0)
                    EarnGems(_profile.GemsBoughtPerMonth);

                // The welcome is over within the first session (5 animals): the rendez-vous opens on day 1.
                bool rendezvous = day == 1 || _p.Refuge.IsFeatureUnlocked(RefugeFeature.DailyRendezvous);
                if (rendezvous)
                {
                    // The game's own tables (T36): welcome gifts, the daily gift and the calendar box, with the substitutions.
                    if (_welcomeClaimed < DailyRewardTables.WelcomeCount)
                        Grant(DailyRewardTables.Welcome(_welcomeClaimed++, SimRewards(), _daily), DiscoverySource.DailyGift);
                    DailyGift();
                    Calendar();
                    if (_profile.WatchesBonusAd || _profile.Club)
                        EarnGems(5);
                }
                if (_p.Refuge.IsFeatureUnlocked(RefugeFeature.DailyRendezvous))
                {
                    int wishes = Math.Min(_daily.WishesPerDay, _profile.WishesPerDay);
                    EarnGems(Math.Min(_daily.WishGemCapPerDay, _daily.WishGems * wishes));
                    // A wish is fulfilled by an animal: biome mastery XP for its biome (open biomes in turn) and species
                    // mastery XP for its species.
                    for (int w = 0; w < wishes; w++)
                    {
                        _p.Mastery.Add(NextWishBiome(), BiomeXpSource.Wish);
                        var species = WishSpecies();
                        if (species != ContentId.None)
                            _mastery.AddXp(species, MasteryReason.Wish, _daily.WishSpeciesXp);
                    }
                }
                if (_p.Refuge.IsFeatureUnlocked(RefugeFeature.RareVisitors) && _random.NextDouble() < VisitorContactsPerDay)
                    VisitorContact();
                if (day % 7 == 0 && _p.Refuge.IsFeatureUnlocked(RefugeFeature.WeeklyBook) && _random.NextDouble() < _profile.WeeklyBookShare)
                {
                    EarnGems(50);
                    _hatch.GrantDiscoveryEggs(1, DiscoverySource.WeeklyBook);
                }

                // A monthly event (doc Événements): three weeks, its 7 objects one every three days, 150 Gems on the free track.
                int inMonth = (day - 1) % 30;
                if (inMonth >= 7 && inMonth <= 27 && (inMonth - 7) % 3 == 0 && _random.NextDouble() < _profile.EventShare)
                {
                    // An event object is a variant found: 3 refuge points (doc 02 Améliorations, section 6.1).
                    _today.EventObjects++;
                    _plannedScore += 3;
                }
                if (inMonth == 27 && _profile.EventShare > 0.0)
                    EarnGems(150);
                // The Refuge Pass, a 30-day season: 150 Gems free; the premium track adds 1 000 Gems and 2 exclusive colors.
                if (inMonth == 29 && _p.Refuge.IsFeatureUnlocked(RefugeFeature.RefugePass))
                {
                    EarnGems(150);
                    if (_profile.Pass)
                    {
                        EarnGems(1000);
                        _today.PassVariants += 2;
                        _plannedScore += 6;
                    }
                }
                _p.Treats.Refresh(_p.Store.MostAdvancedUnlocked());
                SpendGems();
            }

            /// <summary>Doc 01 Contenu, section 3, through the game's table: the first gift is a Discovery egg, a big one every 15.</summary>
            private void DailyGift()
            {
                var lot = _giftsOpened == 0
                    ? GiftLot.Egg
                    : DailyGiftService.Roll(ref _giftRng, DailyRewardTables.MustForceBig(_giftsWithoutBig, _daily.GiftBigEvery));
                _giftsOpened++;
                _giftsWithoutBig = DailyRewardTables.IsBig(lot) ? 0 : _giftsWithoutBig + 1;
                Grant(DailyRewardTables.Gift(lot, SimRewards()), DiscoverySource.DailyGift);
            }

            /// <summary>Doc 01 Contenu, section 4, through the game's table: one box per day the rendez-vous is open.</summary>
            private void Calendar()
            {
                int box = _calendarStamps % DailyRewardTables.CalendarBoxCount;
                int cycle = _calendarStamps / DailyRewardTables.CalendarBoxCount + 1;
                _calendarStamps++;
                Grant(DailyRewardTables.CalendarBox(box, cycle, SimRewards(), _daily), DiscoverySource.Calendar);
            }

            /// <summary>The substitutions as the game makes them: tickets become Light before the activities, stickers treats before the albums.</summary>
            private RewardContext SimRewards() => new RewardContext(
                _p.Refuge.IsFeatureUnlocked(RefugeFeature.Grooming),
                _p.Refuge.IsFeatureUnlocked(RefugeFeature.Garden),
                _p.Refuge.IsFeatureUnlocked(RefugeFeature.Albums),
                false,
                ContentId.None,
                _daily.TicketLightMinutes);

            /// <summary>
            /// A rendez-vous bundle in the simulation: Gems, Light minutes, Discovery eggs, stickers into the albums and the
            /// cycle decoration placed at once. Tickets, seeds and treats feed systems the simulation counts elsewhere.
            /// </summary>
            private void Grant(in RewardBundle bundle, DiscoverySource source)
            {
                EarnGems(bundle.Gems);
                if (bundle.LightMinutes > 0.0)
                    GrantMinutes(bundle.LightMinutes);
                if (bundle.DiscoveryEggs > 0)
                    _hatch.GrantDiscoveryEggs(bundle.DiscoveryEggs, source);
                for (int i = 0; i < bundle.Stickers; i++)
                    Sticker();
                if (bundle.Decoration != ContentId.None)
                {
                    _decorationsPlaced++;
                    _plannedScore += 2;
                    _p.Mastery.Add(_p.Store.MostAdvancedUnlocked(), BiomeXpSource.Decoration);
                }
            }

            /// <summary>
            /// The species of the animal that expresses the wish: wishes go to visible animals first (doc 01 Contenu, section
            /// 2.4), and the player looks at the biome they play in, the newest one; a random animal of it (any animal when
            /// it has none yet).
            /// </summary>
            private ContentId WishSpecies()
            {
                int count = _state.Collection.Count;
                if (count == 0)
                    return ContentId.None;
                var biome = _p.Store.MostAdvancedUnlocked();
                int start = _random.Next(count);
                for (int k = 0; k < count; k++)
                {
                    var species = _state.Collection[(start + k) % count].Species;
                    if (_content.Species.TryGet(species, out var definition) && definition.Biome == biome)
                        return species;
                }
                return _state.Collection[start].Species;
            }

            private void GrantMinutes(double minutes)
            {
                double light = Rate * minutes * 60.0;
                if (light > 0.0)
                    _economy.Grant(CurrencyId.Light, BigValue.FromDouble(light), EconomyReason.Reward);
            }

            private void EarnGems(long gems)
            {
                if (gems <= 0)
                    return;
                _state.Wallet.Gems += gems;
                _today.GemsEarned += gems;
            }

            /// <summary>Cosmetics only (doc 02 Gemmes, section 3): an item once enough Gems are kept, never power.</summary>
            private void SpendGems()
            {
                long price = _profile.CosmeticPrice;
                while (_state.Wallet.Gems >= price * 3 / 2)
                {
                    _state.Wallet.Gems -= price;
                    _today.GemsSpent += price;
                    if (_profile.GemsBoughtPerMonth <= 0)
                        break;
                }
            }

            // ---------------- A session ----------------

            private void Session(double seconds, int sessionsLeft)
            {
                ClaimLevels();
                Expeditions();
                Activities(sessionsLeft);
                double remaining = seconds;
                int guard = 0;
                while (remaining > 1e-6 && guard++ < 400_000)
                {
                    if (_busy > 0.0)
                    {
                        double busy = Math.Min(_busy, remaining);
                        Advance(busy);
                        _busy -= busy;
                        remaining -= busy;
                        continue;
                    }
                    double wait = Act(remaining);
                    if (_busy > 0.0)
                        continue;
                    double step = Math.Min(Math.Max(wait, 1.0), remaining);
                    Advance(step);
                    remaining -= step;
                }
                _busy = 0.0;
                ClaimLevels();
            }

            /// <summary>The refuge score: what the real service counts plus the planned systems' points.</summary>
            private void SyncScore()
            {
                if (_plannedScore <= 0)
                    return;
                long computed = _p.Refuge.Breakdown().Total + _plannedScore;
                if (computed > _state.Progression.Refuge.Score)
                {
                    _state.Progression.Refuge.Score = computed;
                    _p.Refuge.Recompute();
                }
            }

            private void ClaimLevels()
            {
                SyncScore();
                if (_p.Refuge.TryClaimRewards(out var rewards))
                    _today.GemsEarned += rewards.Gems;
                int level = _p.Refuge.Level;
                for (int l = _lastRefugeLevel + 1; l <= level; l++)
                {
                    Result.Milestones.Add(new SimMilestone("refuge", l.ToString(), Today, PlayMinutes, l));
                    _today.Goals++;
                }
                _lastRefugeLevel = Math.Max(_lastRefugeLevel, level);
            }

            /// <summary>Activities (doc 01 Mini-jeux): Light in minutes of production, a few Gems, biome mastery XP.</summary>
            private void Activities(int sessionsLeft)
            {
                if (!_p.Refuge.IsFeatureUnlocked(RefugeFeature.Grooming))
                    return;
                int tickets = _profile.Club ? 7 : 5;
                int wanted = Math.Min(_profile.ActivitiesPerDay, tickets);
                int left = wanted - _activitiesToday;
                if (left <= 0)
                    return;
                int now = (left + sessionsLeft - 1) / Math.Max(1, sessionsLeft);
                for (int i = 0; i < now; i++)
                {
                    GrantMinutes(_profile.ActivityMinutes * _p.Essence.ActivityLightMultiplier);
                    _p.Mastery.Add(_p.Store.MostAdvancedUnlocked(), BiomeXpSource.Activity);
                    if (_random.NextDouble() < ActivityStickerChance)
                        Sticker();
                    if (_activitiesToday < Math.Min(3, _profile.ThreeStarGamesPerDay))
                        EarnGems(2);
                    _activitiesToday++;
                }
            }

            /// <summary>Expeditions (doc 01 Contenu, section 7): back ones pay, free slots leave; the last of the day is 8 h.</summary>
            private void Expeditions()
            {
                if (!_p.Refuge.IsFeatureUnlocked(RefugeFeature.Expeditions))
                    return;
                for (int i = _expeditions.Count - 1; i >= 0; i--)
                {
                    if (_expeditions[i].end > _clock.UtcNow)
                        continue;
                    double hours = _expeditions[i].hours;
                    _expeditions.RemoveAt(i);
                    GrantMinutes((hours >= 8 ? 90 : hours >= 4 ? 50 : 15) * _p.Essence.ActivityLightMultiplier);
                    _p.Mastery.Add(LowestMasteryBiome(), BiomeXpSource.Expedition);
                    if (_random.NextDouble() < (hours >= 12 ? 0.60 : hours >= 8 ? 0.45 : hours >= 4 ? 0.25 : 0.10))
                        Sticker();
                    double chance = hours >= 12 ? 0.09 : hours >= 8 ? 0.06 : hours >= 4 ? 0.02 : 0.0;
                    bool egg = _random.NextDouble() < chance;
                    if (hours >= 8)
                    {
                        _longExpeditionsWithoutEgg++;
                        if (_longExpeditionsWithoutEgg >= 12)
                            egg = true;
                    }
                    if (egg)
                    {
                        _hatch.GrantDiscoveryEggs(1, DiscoverySource.Expedition);
                        if (hours >= 8)
                            _longExpeditionsWithoutEgg = 0;
                    }
                }
                int slots = _profile.Club ? 2 : 1;
                while (_expeditions.Count < slots && _expeditionsToday < _profile.ExpeditionsPerDay)
                {
                    _expeditionsToday++;
                    double hours = _expeditionsToday >= _profile.ExpeditionsPerDay ? 8.0 : 4.0;
                    _expeditions.Add((_clock.UtcNow.AddHours(hours), hours));
                }
            }

            // ---------------- Choices ----------------

            private enum Option { None, Egg, Production, Nests, Curiosity, Zone, Gate }

            /// <summary>
            /// Does every purchase that should happen now; returns the seconds until the saved-for option becomes affordable
            /// (or the session's end when nothing is worth saving for).
            /// </summary>
            private double Act(double remaining)
            {
                for (int guard = 0; guard < 10_000; guard++)
                {
                    ClaimLevels();
                    if (_clock.UtcNow >= _nextRenewalLook && LookAtRenewal() && RenewKeeping(out var done))
                    {
                        _upgradeFloor = -1.0;
                        _essenceTried = -1;
                        Result.Milestones.Add(new SimMilestone("renewal", done.Count.ToString(), Today, PlayMinutes, done.Gain.Total));
                        Result.Milestones.Add(new SimMilestone("renewal.light", done.Count.ToString(), Today, PlayMinutes, done.Gain.FromLight));
                        _today.Renewals++;
                        _today.Goals++;
                    }
                    SpendEssence();
                    if (_hatch.DiscoveryEggs > 0)
                    {
                        int quantity = HatchQuantity();
                        for (int n = 0; n < quantity && _hatch.DiscoveryEggs > 0 && Hatch(_hatch.DiscoveryEgg, false, quantity); n++) { }
                        return 0.0;
                    }

                    bool hasGate = _p.Gates.TryGetNextLocked(out var next);
                    var gate = hasGate ? _p.Gates.GetStatus(next) : default;
                    if (hasGate)
                    {
                        if (gate.CanOpen)
                        {
                            double price = gate.Price.ToDoubleClamped();
                            if (_p.Gates.TryOpen(next, out _))
                            {
                                Spent(SpendKind.Gates, price);
                                _upgradeFloor = -1.0;
                                Result.Milestones.Add(new SimMilestone("biome", BiomeName(next), Today, PlayMinutes, 0));
                                _today.Goals++;
                                _hatch.GrantDiscoveryEggs(1, DiscoverySource.BiomeOpening);
                                if (_profile.BiomePacks)
                                    AddCompanion(next);
                                continue;
                            }
                        }
                    }

                    double rate = Rate;
                    double light = Light;
                    if (CheapSpends(rate, light))
                        continue;

                    if (_policy == SimPolicy.Guided)
                    {
                        double guided = Guided(rate, light, remaining, hasGate && gate.Ready ? gate.Price.ToDoubleClamped() : -1.0);
                        if (guided < 0.0)
                            continue;
                        return guided;
                    }

                    var best = Best(rate, out double cost, out ContentId target, out UpgradeKind kind, out ContentId zone, out bool gateTarget);
                    if (best == Option.None)
                        return remaining;
                    if (cost > light)
                    {
                        double wait = rate > 0.0 ? (cost - light) / rate : double.PositiveInfinity;
                        return wait;
                    }
                    switch (best)
                    {
                        case Option.Egg:
                        {
                            // One tap hatches 1, 3 or 10 eggs when the Light is there (doc 01 Contenu, multi hatch).
                            int quantity = HatchQuantity();
                            for (int n = 0; n < quantity && Hatch(target, true, quantity); n++) { }
                            return 0.0;
                        }
                        case Option.Production:
                        case Option.Nests:
                        case Option.Curiosity:
                            if (_p.Upgrades.TryBuy(target, kind, PurchaseQty.One, out var purchase))
                            {
                                _busy += 0.4;
                                Spent(SpendKind.Upgrades, purchase.Cost.ToDoubleClamped());
                                if (purchase.MilestoneCrossed)
                                    _today.Goals++;
                            }
                            else
                                return remaining;
                            break;
                        case Option.Zone:
                            if (_p.Zones.TryOpen(zone, out var opening))
                            {
                                Spent(SpendKind.Zones, opening.Cost.ToDoubleClamped());
                                _upgradeFloor = -1.0;
                                _p.Zones.TryGet(zone, out var z);
                                Result.Milestones.Add(new SimMilestone("zone", z.SourceId, Today, PlayMinutes, z.ExpansionIndex));
                                _today.Goals++;
                            }
                            else
                                return remaining;
                            break;
                        case Option.Gate:
                            return remaining;
                    }
                }
                return remaining;
            }

            /// <summary>
            /// The guided player: eggs are the game; an upgrade when the HUD advises it (doc 02 Améliorations, section 3.6: at
            /// least 5 % of total production, or a × 2 milestone the Light reaches now), a zone when its sign glows, Cozy Nests
            /// and Curious Eye when cheap; a biome gate whose other conditions are met is saved for. Returns -1 after a purchase
            /// other than an egg (look again), 0 after a hatch, else the seconds until the chosen egg is affordable.
            /// </summary>
            private double Guided(double rate, double light, double remaining, double readyGatePrice)
            {
                if (readyGatePrice >= 0.0)
                    return rate > 0.0 ? Math.Max(1.0, (readyGatePrice - light) / rate) : remaining;

                var biomes = _config.Biomes;
                if (_upgradeFloor < 0.0)
                    _upgradeFloor = CheapestBiomePurchase();
                for (int b = 0; light >= _upgradeFloor && b < biomes.Length; b++)
                {
                    var biome = biomes[b].Id;
                    if (!_p.Store.IsUnlocked(biome))
                        continue;
                    if (_p.Zones.TryGetNext(biome, out var nextZone) && _p.Zones.GetState(nextZone) == ZoneState.Available
                        && _p.Zones.TryOpen(nextZone.Id, out var opening))
                    {
                        Spent(SpendKind.Zones, opening.Cost.ToDoubleClamped());
                        _upgradeFloor = -1.0;
                        Result.Milestones.Add(new SimMilestone("zone", nextZone.SourceId, Today, PlayMinutes, nextZone.ExpansionIndex));
                        _today.Goals++;
                        return -1.0;
                    }
                    if (Advised(biome, rate, light) && Buy(biome, UpgradeKind.Production))
                        return -1.0;
                    if (Cheap(biome, UpgradeKind.Nests, NestsMinutes, rate, light) && Buy(biome, UpgradeKind.Nests))
                        return -1.0;
                    if (Cheap(biome, UpgradeKind.Curiosity, CuriosityMinutes, rate, light) && BiomeHasNews(biome) && Buy(biome, UpgradeKind.Curiosity))
                        return -1.0;
                }

                var egg = ChooseEgg(out double price);
                if (egg == ContentId.None)
                    return remaining;
                double eggWait = rate > 0.0 ? Math.Max(0.0, price - light) / rate : remaining;
                if (eggWait > PatienceMinutes * 60.0)
                {
                    // The egg is more than 10 minutes away: while waiting, the player opens the Upgrade screen and takes its
                    // « Conseillé » card, the shortest payback (doc 02 Améliorations, section 3.6), or the zone that pays back sooner.
                    var pick = Best(rate, out double cost, out ContentId target, out UpgradeKind kind, out ContentId zone, out _);
                    if (pick == Option.Production || pick == Option.Nests || pick == Option.Curiosity || pick == Option.Zone)
                    {
                        if (cost <= light)
                        {
                            if (pick == Option.Zone)
                            {
                                if (_p.Zones.TryGet(zone, out var z) && _p.Zones.TryOpen(zone, out var opening))
                                {
                                    Spent(SpendKind.Zones, opening.Cost.ToDoubleClamped());
                                    _upgradeFloor = -1.0;
                        _upgradeFloor = -1.0;
                                    Result.Milestones.Add(new SimMilestone("zone", z.SourceId, Today, PlayMinutes, z.ExpansionIndex));
                                    _today.Goals++;
                                    return -1.0;
                                }
                            }
                            else if (Buy(target, kind))
                                return -1.0;
                        }
                        else if (rate > 0.0)
                            eggWait = Math.Min(eggWait, (cost - light) / rate);
                    }
                }
                if (price > light)
                    return eggWait;
                int quantity = HatchQuantity();
                for (int n = 0; n < quantity && Hatch(egg, true, quantity); n++) { }
                return 0.0;
            }

            /// <summary>
            /// The lowest price among every open biome's next zone and next upgrade levels, locked or not: no biome purchase can
            /// happen below it. Recomputed after any purchase, Renewal, Essence node or new biome (prices only go up otherwise).
            /// </summary>
            private double CheapestBiomePurchase()
            {
                double floor = double.PositiveInfinity;
                var biomes = _config.Biomes;
                for (int b = 0; b < biomes.Length; b++)
                {
                    var biome = biomes[b].Id;
                    if (!_p.Store.IsUnlocked(biome))
                        continue;
                    if (_p.Zones.TryGetNext(biome, out var zone))
                    {
                        double price = _p.Zones.Price(zone).ToDoubleClamped();
                        if (price > 0.0)
                            floor = Math.Min(floor, price);
                    }
                    foreach (var kind in UpgradeKinds)
                    {
                        int level = _p.Upgrades.GetLevel(biome, kind);
                        if (level < _p.Upgrades.MaxLevel(kind))
                            floor = Math.Min(floor, _p.Upgrades.LevelPrice(biome, kind, level).ToDoubleClamped());
                    }
                }
                return floor;
            }

            private static readonly UpgradeKind[] UpgradeKinds = { UpgradeKind.Production, UpgradeKind.Nests, UpgradeKind.Curiosity };

            /// <summary>The Production level the HUD advises: 5 % of total production, or the run to the next × 2 the Light covers.</summary>
            private bool Advised(ContentId biome, double rate, double light)
            {
                int level = _p.Upgrades.GetLevel(biome, UpgradeKind.Production);
                double price = _p.Upgrades.LevelPrice(biome, UpgradeKind.Production, level).ToDoubleClamped();
                if (price > light || price <= 0.0)
                    return false;
                double biomeRate = _economy.GetBiomeLightPerSecond(biome).ToDoubleClamped();
                double gain = biomeRate * (_p.Upgrades.ProductionMultiplierAt(level + 1) / _p.Upgrades.ProductionMultiplierAt(level) - 1.0);
                if (gain >= AdvisedShare * rate)
                    return true;
                int every = Math.Max(1, _config.ProductionMilestoneEvery);
                int levels = (level / every + 1) * every - level;
                double g = _p.Upgrades.Growth(UpgradeKind.Production);
                double toMilestone = price * (Math.Pow(g, levels) - 1.0) / (g - 1.0);
                return toMilestone <= light;
            }

            private bool Cheap(ContentId biome, UpgradeKind kind, double minutes, double rate, double light)
            {
                if (!_p.Upgrades.IsUnlocked(biome, kind))
                    return false;
                int level = _p.Upgrades.GetLevel(biome, kind);
                if (level >= _p.Upgrades.MaxLevel(kind))
                    return false;
                double price = _p.Upgrades.LevelPrice(biome, kind, level).ToDoubleClamped();
                return price <= light && price <= minutes * 60.0 * rate;
            }

            private bool Buy(ContentId biome, UpgradeKind kind)
            {
                if (!_p.Upgrades.TryBuy(biome, kind, PurchaseQty.One, out var purchase))
                    return false;
                _upgradeFloor = -1.0;
                _busy += 0.4;
                Spent(SpendKind.Upgrades, purchase.Cost.ToDoubleClamped());
                if (purchase.MilestoneCrossed)
                    _today.Goals++;
                return true;
            }

            /// <summary>
            /// The egg the player picks: one that can still give something new (the cheapest); else the Basic egg of the biome
            /// whose species is closest to its next mastery color (levels 10, 20, 30, a discovery); else the Basic egg of the newest
            /// biome (its animals produce the most); else the most production for its price.
            /// </summary>
            private ContentId ChooseEgg(out double price)
            {
                price = 0.0;
                var global = _economy.GlobalMultiplier.ToDoubleClamped();
                ContentId news = ContentId.None, best = ContentId.None, newest = ContentId.None;
                double newsPrice = double.PositiveInfinity, bestPrice = 0.0, bestValue = 0.0, newestPrice = 0.0;
                var newestBiome = _p.Store.MostAdvancedUnlocked();
                var chase = ChaseBiome();
                for (int i = 0; i < _eggs.Count; i++)
                {
                    var id = _eggs[i];
                    var egg = _content.Eggs.Get(id);
                    if (!_hatch.IsOffered(id) || !_p.Store.IsUnlocked(egg.Biome))
                        continue;
                    double cost = _hatch.GetCost(id).ToDoubleClamped();
                    if (cost <= 0.0)
                        continue;
                    if (HasNews(egg))
                    {
                        if (cost < newsPrice)
                        {
                            newsPrice = cost;
                            news = id;
                        }
                        continue;
                    }
                    if (egg.Tier == EggTier.Basic && egg.Biome == (chase != ContentId.None ? chase : newestBiome))
                    {
                        newest = id;
                        newestPrice = cost;
                    }
                    if (news != ContentId.None || newest != ContentId.None)
                        continue;
                    double value = AnimalRate(egg, global) / cost;
                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestPrice = cost;
                        best = id;
                    }
                }
                if (news != ContentId.None)
                {
                    price = newsPrice;
                    return news;
                }
                if (newest != ContentId.None)
                {
                    price = newestPrice;
                    return newest;
                }
                price = bestPrice;
                return best;
            }

            /// <summary>
            /// The open biome holding the species closest to its next mastery color (levels 10, 20, 30), counted in its
            /// hatches still needed; None when every species has its three.
            /// </summary>
            private ContentId ChaseBiome()
            {
                // Only a hatch changes species mastery: the answer is kept until the next animal.
                int animals = _state.Collection.Count;
                if (animals == _chaseAnimals)
                    return _chase;
                _chaseAnimals = animals;
                if (_xpTo == null)
                {
                    _xpTo = new long[32];
                    for (int l = 1; l < _xpTo.Length; l++)
                        _xpTo[l] = _xpTo[l - 1] + EconomyFormulas.MasteryXpForLevel(_economyConfig, l);
                }
                var pick = ContentId.None;
                long fewest = long.MaxValue;
                var species = _content.Species;
                for (int i = 0; i < species.Count; i++)
                {
                    var s = species[i];
                    if (s.IsSecret || !_p.Store.IsUnlocked(s.Biome) || !_state.Mastery.Species.TryGetValue(s.Id, out var m) || m.Level >= 30)
                        continue;
                    int target = m.Level < 10 ? 10 : m.Level < 20 ? 20 : 30;
                    long xp = _xpTo[target] - _xpTo[m.Level] - m.Xp;
                    if (xp < fewest)
                    {
                        fewest = xp;
                        pick = s.Biome;
                    }
                }
                _chase = pick;
                return pick;
            }

            private int _chaseAnimals = -1;
            private ContentId _chase;
            private long[]? _xpTo;

            /// <summary>Light decorations and the day's treats, when cheap (doc 02 Améliorations, sections 8 and 9).</summary>
            private bool CheapSpends(double rate, double light)
            {
                var decorations = _config.Decorations;
                for (int i = 0; i < decorations.Length; i++)
                {
                    if (!_p.Decorations.IsUnlocked(decorations[i]) || _p.Decorations.Copies(decorations[i].Id) > 0)
                        continue;
                    double price = _p.Decorations.NextPrice(decorations[i].Id).ToDoubleClamped();
                    if (price <= light && price <= _profile.DecorationMinutes * 60.0 * rate && _p.Decorations.TryBuy(decorations[i].Id, out _))
                    {
                        Spent(SpendKind.Decorations, price);
                        _today.Goals++;
                        // Placed at once: 2 refuge points, biome mastery XP 10 (doc 02 Améliorations, sections 6.1 and 7.1).
                        _decorationsPlaced++;
                        _plannedScore += 2;
                        _p.Mastery.Add(_p.Store.MostAdvancedUnlocked(), BiomeXpSource.Decoration);
                        _busy += 1.0;
                        return true;
                    }
                }
                if (_p.Treats.Left > 0)
                {
                    double price = _p.Treats.Price().ToDoubleClamped();
                    if (price <= light && price <= TreatMinutes * 60.0 * rate)
                    {
                        for (int slot = 0; slot < TreatStandService.SlotCount; slot++)
                        {
                            if (!_p.Treats.IsBought(slot) && _p.Treats.TryBuy(slot, out _))
                            {
                                Spent(SpendKind.Treats, price);
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            /// <summary>The option that pays itself back fastest; a biome gate whose other conditions are met wins.</summary>
            private Option Best(double rate, out double cost, out ContentId target, out UpgradeKind kind, out ContentId zone, out bool gateTarget)
            {
                cost = 0.0;
                target = default;
                kind = UpgradeKind.Production;
                zone = default;
                gateTarget = false;
                var best = Option.None;
                double bestPayback = double.PositiveInfinity;
                double bestCost = 0.0;
                ContentId bestTarget = default, bestZone = default;
                var bestKind = UpgradeKind.Production;

                if (_p.Gates.TryGetNextLocked(out var next))
                {
                    var gate = _p.Gates.GetStatus(next);
                    if (gate.Ready)
                    {
                        gateTarget = true;
                        cost = gate.Price.ToDoubleClamped();
                        target = next;
                        return Option.Gate;
                    }
                }

                var global = _economy.GlobalMultiplier.ToDoubleClamped();
                for (int i = 0; i < _eggs.Count; i++)
                {
                    var id = _eggs[i];
                    if (!_hatch.IsOffered(id) || !_p.Store.IsUnlocked(_content.Eggs.Get(id).Biome))
                        continue;
                    var egg = _content.Eggs.Get(id);
                    double price = _hatch.GetCost(id).ToDoubleClamped();
                    double gain = AnimalRate(egg, global) * (HasNews(egg) ? 1.0 + DiscoveryWeight : 1.0);
                    Consider(Option.Egg, price, gain, id, UpgradeKind.Production, default);
                }

                var biomes = _config.Biomes;
                for (int b = 0; b < biomes.Length; b++)
                {
                    var biome = biomes[b].Id;
                    if (!_p.Store.IsUnlocked(biome))
                        continue;
                    double biomeRate = _economy.GetBiomeLightPerSecond(biome).ToDoubleClamped();

                    int level = _p.Upgrades.GetLevel(biome, UpgradeKind.Production);
                    double price = _p.Upgrades.LevelPrice(biome, UpgradeKind.Production, level).ToDoubleClamped();
                    double gain = biomeRate * (_p.Upgrades.ProductionMultiplierAt(level + 1) / _p.Upgrades.ProductionMultiplierAt(level) - 1.0);
                    Consider(Option.Production, price, gain, biome, UpgradeKind.Production, default);

                    if (_p.Upgrades.IsUnlocked(biome, UpgradeKind.Nests) && _p.Upgrades.GetLevel(biome, UpgradeKind.Nests) < _p.Upgrades.MaxLevel(UpgradeKind.Nests))
                    {
                        int nests = _p.Upgrades.GetLevel(biome, UpgradeKind.Nests);
                        double nestsPrice = _p.Upgrades.LevelPrice(biome, UpgradeKind.Nests, nests).ToDoubleClamped();
                        double savings = 0.0;
                        var basic = ContentId.FromSource("egg." + biomes[b].SourceId.Substring("biome.".Length) + ".basic");
                        if (_content.Eggs.TryGet(basic, out _))
                        {
                            // The next 10 eggs' prices (Nests already applied), a geometric sum: p (g^10 - 1) / (g - 1).
                            double g = _config.EggGrowth;
                            double nextEgg = _hatch.GetCost(basic).ToDoubleClamped();
                            savings = nextEgg * (Math.Pow(g, 10) - 1.0) / (g - 1.0) * (1.0 - _config.NestsStep);
                        }
                        Consider(Option.Nests, nestsPrice, savings / 600.0, biome, UpgradeKind.Nests, default);
                    }

                    if (_p.Upgrades.IsUnlocked(biome, UpgradeKind.Curiosity)
                        && _p.Upgrades.GetLevel(biome, UpgradeKind.Curiosity) < _p.Upgrades.MaxLevel(UpgradeKind.Curiosity))
                    {
                        int curiosity = _p.Upgrades.GetLevel(biome, UpgradeKind.Curiosity);
                        double curiosityPrice = _p.Upgrades.LevelPrice(biome, UpgradeKind.Curiosity, curiosity).ToDoubleClamped();
                        if (curiosityPrice <= CuriosityMinutes * 60.0 * rate && BiomeHasNews(biome))
                            Consider(Option.Curiosity, curiosityPrice, curiosityPrice / 60.0, biome, UpgradeKind.Curiosity, default);
                    }

                    if (_p.Zones.TryGetNext(biome, out var nextZone))
                    {
                        var state = _p.Zones.GetState(nextZone);
                        if (state == ZoneState.Available || state == ZoneState.TooExpensive)
                        {
                            double zonePrice = _p.Zones.Price(nextZone).ToDoubleClamped();
                            double zoneGain = biomeRate * (_config.ZoneProductionMult - 1.0) + rate * _config.ZoneGiftMinutes * 60.0 / 600.0;
                            Consider(Option.Zone, zonePrice, zoneGain, biome, UpgradeKind.Production, nextZone.Id);
                        }
                    }
                }
                cost = bestCost;
                target = bestTarget;
                kind = bestKind;
                zone = bestZone;
                return best;

                void Consider(Option option, double price, double gain, ContentId id, UpgradeKind upgrade, ContentId zoneId)
                {
                    if (gain <= 0.0 || price <= 0.0)
                        return;
                    double payback = price / gain;
                    if (payback < bestPayback)
                    {
                        bestPayback = payback;
                        best = option;
                        bestCost = price;
                        bestTarget = id;
                        bestKind = upgrade;
                        bestZone = zoneId;
                    }
                }
            }

            /// <summary>Production of one animal from this egg: the biome's base, the table's average rarity, the biome's multipliers.</summary>
            private double AnimalRate(EggDefinition egg, double global)
            {
                double total = 0.0, value = 0.0;
                var weights = egg.RarityWeights;
                for (int r = 0; r < weights.Length && r < _rarityValue.Length; r++)
                {
                    total += weights[r];
                    value += weights[r] * _rarityValue[r];
                }
                double rarity = total > 0.0 ? value / total : 1.0;
                double baseProduction = _config.TryGetBiome(egg.Biome, out var biome) ? biome.BaseProduction : 1.0;
                return baseProduction * rarity * _p.BiomeProductionMultiplier(egg.Biome) * global;
            }

            private readonly Dictionary<ContentId, bool> _news = new Dictionary<ContentId, bool>();
            private int _newsKey = -1;

            /// <summary>The egg can still give a species or base color not found yet (cached until the next discovery).</summary>
            private bool HasNews(EggDefinition egg)
            {
                int key = _state.Collection.DiscoveredSpecies.Count * 4096 + _state.Collection.DiscoveredVariants.Count;
                if (key != _newsKey)
                {
                    _news.Clear();
                    _newsKey = key;
                }
                if (_news.TryGetValue(egg.Id, out bool cached))
                    return cached;
                bool news = ComputeNews(egg);
                _news[egg.Id] = news;
                return news;
            }

            private bool ComputeNews(EggDefinition egg)
            {
                if (egg.Tier == EggTier.Focus)
                {
                    int count = _hatch.GetFocusTargets(egg.Id, _targets);
                    for (int i = 0; i < count; i++)
                        if (SpeciesHasNews(_targets[i]))
                            return true;
                    return false;
                }
                for (int i = 0; i < egg.Pool.Length; i++)
                    if (SpeciesHasNews(egg.Pool[i].Species))
                        return true;
                return false;
            }

            private bool BiomeHasNews(ContentId biome)
            {
                for (int i = 0; i < _content.Species.Count; i++)
                    if (_content.Species[i].Biome == biome && SpeciesHasNews(_content.Species[i].Id))
                        return true;
                return false;
            }

            private bool SpeciesHasNews(ContentId species)
            {
                if (!_state.Collection.DiscoveredSpecies.Contains(species))
                    return true;
                if (_baseVariants.TryGetValue(species, out var variants))
                    for (int i = 0; i < variants.Count; i++)
                        if (!_state.Collection.DiscoveredVariants.Contains(variants[i]))
                            return true;
                return false;
            }

            private int HatchQuantity() =>
                _p.Refuge.IsFeatureUnlocked(RefugeFeature.HatchTen) ? 10 : _p.Refuge.IsFeatureUnlocked(RefugeFeature.HatchThree) ? 3 : 1;

            private bool Hatch(ContentId egg, bool paid, int quantity)
            {
                double rate = Rate;
                var result = _hatch.Hatch(egg);
                if (result.Status != HatchStatus.Hatched)
                    return false;
                _today.Hatches++;
                // The sequence (2.6 s for the first 20 hatches, then the fast 1.2 s), shared by a × 3 or × 10, and the tap.
                double sequence = _state.Collection.NextInstanceId <= 21 ? 2.6 : 1.2;
                _busy += (sequence + 0.5) / quantity;
                RollTraits(result.Animal.Species);
                double cost = result.Cost.ToDoubleClamped();
                if (paid && !result.WasFree && cost > 0.0)
                {
                    Spent(SpendKind.Eggs, cost);
                    if (rate > 0.0)
                        _today.EggCostMaxMinutes = Math.Max(_today.EggCostMaxMinutes, cost / rate / 60.0);
                }
                if (result.NewSpecies)
                {
                    _today.NewSpecies++;
                    EarnGems(10);
                }
                if (result.NewVariant || result.NewSpecies)
                {
                    _today.NewVariants++;
                    EarnGems(2);
                }
                var mastery = result.Mastery;
                if (mastery.HasGain)
                {
                    for (int l = mastery.Before.Level + 1; l <= mastery.After.Level; l++)
                    {
                        if (l % 5 == 0)
                        {
                            EarnGems(MasteryMilestoneGems);
                            _today.Goals++;
                        }
                        if (l == 10 || l == 20 || l == 30)
                        {
                            _today.MasteryVariants++;
                            _plannedScore += 3;
                        }
                    }
                }
                return true;
            }

            /// <summary>Traits (doc 02 Formules): each found for the first time on a species is a discovery and 5 refuge points.</summary>
            private void RollTraits(ContentId species)
            {
                for (int t = 0; t < TraitRates.Length; t++)
                {
                    if (_random.NextDouble() >= TraitRates[t] * _p.Essence.Sum(EssenceEffect.TraitChance) + TraitRates[t])
                        continue;
                    long key = ((long)species.Value << 3) | (uint)t;
                    if (_traitsFound.Add(key))
                    {
                        _today.Traits++;
                        _plannedScore += 5;
                    }
                }
            }

            /// <summary>A rare visitor comes by (doc 02 Formules): 30 % to join, sure at the 4th contact; 8 refuge points.</summary>
            private void VisitorContact()
            {
                if (_visitorsJoined >= Math.Min(Visitors, _p.Store.UnlockedCount()))
                    return;
                double join = Math.Min(0.45, 0.30 + _p.Essence.Sum(EssenceEffect.VisitorJoin));
                if (_visitorMisses >= 3 || _random.NextDouble() < join)
                {
                    _visitorsJoined++;
                    _visitorMisses = 0;
                    _today.Visitors++;
                    _plannedScore += 8;
                }
                else
                    _visitorMisses++;
            }

            /// <summary>
            /// A sticker (doc 01 Albums), once albums are open: the least complete album of an open biome; 1 refuge point,
            /// 10 more per page complete, biome mastery XP 8; an album complete is worth 5 Essence.
            /// </summary>
            private void Sticker()
            {
                if (!_p.Refuge.IsFeatureUnlocked(RefugeFeature.Albums))
                    return;
                int open = Math.Min(_albumStickers.Length, _p.Store.UnlockedCount());
                int pick = -1;
                for (int b = 0; b < open; b++)
                    if (_albumStickers[b] < StickersPerPage * PagesPerAlbum && (pick < 0 || _albumStickers[b] < _albumStickers[pick]))
                        pick = b;
                if (pick < 0)
                    return;
                _albumStickers[pick]++;
                _today.Stickers++;
                _plannedScore += 1;
                if (_albumStickers[pick] % StickersPerPage == 0)
                    _plannedScore += 10;
                if (_albumStickers[pick] == StickersPerPage * PagesPerAlbum)
                    _p.Renewal.GrantEssence(5);
                _p.Mastery.Add(_config.Biomes[pick].Id, BiomeXpSource.Sticker);
            }

            private ContentId NextWishBiome()
            {
                int open = Math.Max(1, _p.Store.UnlockedCount());
                _wishBiome = (_wishBiome + 1) % open;
                return _config.Biomes[Math.Min(_wishBiome, _config.Biomes.Length - 1)].Id;
            }

            /// <summary>The player sends expeditions where the biome mastery is lowest (to open its zones).</summary>
            private ContentId LowestMasteryBiome()
            {
                var pick = _config.Biomes[0].Id;
                int lowest = int.MaxValue;
                foreach (var biome in _config.Biomes)
                {
                    if (!_p.Store.IsUnlocked(biome.Id))
                        continue;
                    int level = _p.Mastery.GetLevel(biome.Id);
                    if (level < lowest)
                    {
                        lowest = level;
                        pick = biome.Id;
                    }
                }
                return pick;
            }

            /// <summary>A biome pack's companion (doc 02 Gemmes, section 4): it produces like a Rare animal of its biome.</summary>
            private void AddCompanion(ContentId biome)
            {
                for (int i = 0; i < _content.Species.Count; i++)
                {
                    if (_content.Species[i].Biome != biome)
                        continue;
                    _economy.OnAnimalAdded(new AnimalInstance { Species = _content.Species[i].Id, Rarity = RarityTier.Rare });
                    return;
                }
            }

            private DateTimeOffset _nextRenewalLook;
            private long _essenceTried = -1;
            private double _upgradeFloor = -1.0;

            /// <summary>The player looks at the Renewal button about once a minute of play.</summary>
            private bool LookAtRenewal()
            {
                _nextRenewalLook = _clock.UtcNow.AddMinutes(1);
                return _p.Renewal.CanRenew() && Rentable();
            }

            /// <summary>The Renewal; with the what-if <see cref="WhatIf.KeepEggCounters"/>, the egg counters survive it.</summary>
            private bool RenewKeeping(out RenewalDone done)
            {
                if (!_whatIf.KeepEggCounters)
                    return _p.Renewal.TryRenew(out done);
                var counters = new Dictionary<ContentId, int>(_state.World.EggPurchasesInCycle);
                if (!_p.Renewal.TryRenew(out done))
                    return false;
                foreach (var pair in counters)
                    _state.World.EggPurchasesInCycle[pair.Key] = pair.Value;
                return true;
            }

            /// <summary>
            /// A Renewal worth its restart (doc 02 Renouveau, section 1.3, « un Renouveau rentable »): the first one as soon as
            /// the button lights up; then when it adds at least half of all the Essence earned so far.
            /// </summary>
            private bool Rentable()
            {
                if (_state.World.RenewalCount == 0)
                    return true;
                return _p.Renewal.Preview().Earned >= RentableShare * _state.Progression.Renewal.EssenceEarned;
            }

            /// <summary>Essence goes to the cheapest open node, the Economy branch first, then Discovery and Offline.</summary>
            private void SpendEssence()
            {
                if (_state.Wallet.Essence <= 0 || _state.Wallet.Essence == _essenceTried)
                    return;
                _essenceTried = _state.Wallet.Essence;
                for (int guard = 0; guard < 200; guard++)
                {
                    EssenceNodeDefinition? pick = null;
                    int pickCost = int.MaxValue;
                    foreach (var node in EssenceCatalog.Nodes)
                    {
                        if (!_p.Essence.CanBuy(node))
                            continue;
                        int weight = node.Branch == EssenceBranch.Economy ? 1 : node.Branch == EssenceBranch.Discovery ? 2 : 3;
                        int cost = _p.Essence.NextCost(node) * weight;
                        if (cost < pickCost)
                        {
                            pickCost = cost;
                            pick = node;
                        }
                    }
                    if (pick == null || !_p.Essence.TryBuy(pick.Value.Id, out _))
                        return;
                    _upgradeFloor = -1.0;
                }
            }

            private void Spent(SpendKind kind, double light)
            {
                Trace?.Invoke($"{PlayMinutes:F2} min, day {Today:F2}: {kind} {light:G4} (Light {Light:G4}, {Rate:G4}/s, animals {_state.Collection.Count}, level {_p.Refuge.Level})");
                if (Today < 31.0)
                    Result.Spend30[(int)kind] += light;
            }

            private void EndDay()
            {
                var d = _today;
                d.LightLog10 = Log10(_economy.Light);
                d.RateLog10 = Log10(_economy.GetLightPerSecond());
                d.LifetimeLog10 = Log10(_state.Wallet.LifetimeLight);
                d.Biomes = _p.Store.UnlockedCount();
                int zones = 0, masterySum = 0;
                foreach (var biome in _config.Biomes)
                {
                    zones += _p.Zones.ExpansionsOpen(biome.Id);
                    masterySum += _p.Mastery.GetLevel(biome.Id);
                    d.BiomeMastery = Math.Max(d.BiomeMastery, _p.Mastery.GetLevel(biome.Id));
                }
                if (masterySum > _lastBiomeMasterySum)
                    d.Goals += masterySum - _lastBiomeMasterySum;
                _lastBiomeMasterySum = masterySum;
                d.Zones = zones;
                d.RefugeLevel = _p.Refuge.Level;
                d.RefugeScore = _p.Refuge.Score;
                foreach (var pair in _state.Mastery.Species)
                {
                    d.MaxSpeciesMastery = Math.Max(d.MaxSpeciesMastery, pair.Value.Level);
                    long xp = pair.Value.Xp;
                    for (int l = 1; l <= pair.Value.Level; l++)
                        xp += EconomyFormulas.MasteryXpForLevel(_economyConfig, l);
                    d.TopSpeciesXp = Math.Max(d.TopSpeciesXp, xp);
                }
                d.Gems = _state.Wallet.Gems;
                d.Essence = _state.Wallet.Essence;
                d.BaseVariantsFound = _state.Collection.DiscoveredVariants.Count;
                d.NextGate = GateBlockers();
                Target("codex.grassland", BiomesComplete(1));
                Target("codex.softlaunch", BiomesComplete(4));
                Target("mastery30", d.MaxSpeciesMastery >= 30);
                Result.Days.Add(d);
            }

            /// <summary>The next gate's unmet conditions, as the gate screen lists them: "Neige : niveau 18/22, maîtrise 5/8".</summary>
            private string GateBlockers()
            {
                if (!_p.Gates.TryGetNextLocked(out var next))
                    return string.Empty;
                var g = _p.Gates.GetStatus(next);
                var parts = new List<string>();
                if ((g.Met & GateConditions.RefugeLevel) == 0)
                    parts.Add($"niveau {_p.Refuge.Level}/{g.RefugeLevelNeeded}");
                if ((g.Met & GateConditions.Species) == 0)
                    parts.Add($"espèces {g.SpeciesFound}/{g.SpeciesNeeded}");
                if ((g.Met & GateConditions.Mastery) == 0)
                    parts.Add($"maîtrise {g.MasteryLevel}/{g.MasteryNeeded}");
                if ((g.Met & GateConditions.Floor) == 0)
                    parts.Add($"plancher dans {g.FloorLeft} {(g.FloorKind == GateFloorKind.PlayMinutes ? "min" : "j.")}");
                if ((g.Met & GateConditions.Light) == 0)
                    parts.Add($"Lumière {Light / Math.Max(1.0, g.Price.ToDoubleClamped()) * 100.0:F0} %");
                return SimGuardrails.French(BiomeName(next)) + " : " + (parts.Count == 0 ? "prête" : string.Join(", ", parts));
            }

            /// <summary>Records a progression target the first day it is reached (doc 02 Gemmes, section 5).</summary>
            private void Target(string what, bool reached)
            {
                if (!reached || !_seen.Add(what))
                    return;
                Result.Milestones.Add(new SimMilestone("target", what, _today.Day, PlayMinutes, 0));
            }

            /// <summary>Every base color of every species of the first <paramref name="count"/> biomes is found.</summary>
            private bool BiomesComplete(int count)
            {
                for (int b = 0; b < count && b < _config.Biomes.Length; b++)
                {
                    var biome = _config.Biomes[b].Id;
                    for (int i = 0; i < _content.Species.Count; i++)
                    {
                        var species = _content.Species[i];
                        if (species.Biome != biome || species.IsSecret)
                            continue;
                        if (!_state.Collection.DiscoveredSpecies.Contains(species.Id))
                            return false;
                        if (_baseVariants.TryGetValue(species.Id, out var variants))
                            foreach (var v in variants)
                                if (!_state.Collection.DiscoveredVariants.Contains(v))
                                    return false;
                    }
                }
                return true;
            }

            private string BiomeName(ContentId biome)
            {
                foreach (var b in _config.Biomes)
                    if (b.Id == biome)
                        return b.SourceId;
                return biome.Value.ToString();
            }

            private static double Log10(BigValue value) => value.IsZero || value.IsNegative ? 0.0 : BigValue.Log10(value);
        }
    }
}
