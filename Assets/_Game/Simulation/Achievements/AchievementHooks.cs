#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T44B: wires the existing systems' events to the 200 achievements and the profile items (doc 01 Succès, sections 1 to 5):
    /// <list type="bullet">
    /// <item>Hatches (eggs by kind and biome, the day's and the run's eggs, rarities, traits, species and trait pairs, new
    /// discoveries), species mastery (and its titles), biome mastery, refuge level, zones and biomes opened (and their titles
    /// and frames), Light upgrades, Renewal and Essence.</item>
    /// <item>Activities (games and their units, stars, records, practice, challenges), the daily rendez-vous (gifts, wishes,
    /// calendar, weekly journal, the day of 3 wishes), bonds (pets, treats, points), expeditions, the garden, the albums
    /// (stickers by source, stars, pages; the page titles through <see cref="AlbumHooks.TitleEarned"/>), decorations bought.</item>
    /// <item>The state counters are computed from the save at construction (<see cref="AchievementRecount"/>: an older
    /// account's backfill) and the starting title and frame are given once.</item>
    /// </list>
    /// Counters of systems not built yet (visitors, breeding, photo mode, placement, accessories, big events, the journal's
    /// pages) stay at 0: their systems call <see cref="AchievementService.Add"/> / <see cref="AchievementService.Raise"/> with
    /// their counter, or the few helpers below. Handlers are cached delegates: no allocation per event, no polling.
    /// The bootstrap builds it after every service it lists and disposes it with them.
    /// </summary>
    public sealed class AchievementHooks : IDisposable
    {
        /// <summary>How an activity game feeds its achievement (ACH-095 to 110): games finished, its score, games won.</summary>
        public enum ActivityUnit : byte
        {
            None = 0,
            Games = 1,
            Score = 2,
            Successes = 3,
        }

        // S-ACT-001 to 018 (index: the ActivityId value). Expeditions and the Garden have their own achievements.
        private static readonly ActivityUnit[] Units =
        {
            ActivityUnit.None,
            ActivityUnit.Games,     // 1 Grooming: games finished
            ActivityUnit.Score,     // 2 Hide and Seek: animals found
            ActivityUnit.Score,     // 3 Nut Drop: items caught
            ActivityUnit.Score,     // 4 Pawprint Memory: pairs found
            ActivityUnit.None,      // 5 Expeditions
            ActivityUnit.None,      // 6 Garden
            ActivityUnit.Score,     // 7 Treasure Fishing: treasures reeled in
            ActivityUnit.Score,     // 8 Lily Hop: lily pads
            ActivityUnit.Successes, // 9 Bakery: cakes baked
            ActivityUnit.Successes, // 10 Photo Safari: good shots
            ActivityUnit.Successes, // 11 Hug Match: levels cleared
            ActivityUnit.Score,     // 12 Slide: metres
            ActivityUnit.Successes, // 13 Constellations: constellations drawn
            ActivityUnit.Score,     // 14 Firefly Rhythm: notes hit
            ActivityUnit.Score,     // 15 Bubbles: bubbles popped
            ActivityUnit.Successes, // 16 Tile Puzzle: puzzles finished
            ActivityUnit.Successes, // 17 Burrow Maze: ways out found
            ActivityUnit.Games,     // 18 Grand Contest: entries
        };

        private readonly AchievementService _achievements;
        private readonly ProfileItemService _items;
        private readonly PlayerState _state;
        private readonly IGameClock _clock;
        private readonly ContentRegistry? _content;
        private readonly HatchService? _hatch;
        private readonly MasteryService? _mastery;
        private readonly ProgressionServices? _progression;
        private readonly ActivityServices? _activities;
        private readonly DailyVisitServices? _daily;
        private readonly ExpeditionService? _expeditions;
        private readonly GardenService? _garden;
        private readonly AlbumService? _albums;
        private readonly AlbumHooks? _albumHooks;

        private readonly Action<HatchResult> _onHatched;
        private readonly Action<MasteryGain> _onMastery;
        private readonly Action<BiomeMasteryGain> _onBiomeMastery;
        private readonly Action<RefugeLevelUp> _onRefuge;
        private readonly Action<BiomeOpening> _onBiome;
        private readonly Action<ZoneOpening> _onZone;
        private readonly Action<UpgradePurchase> _onUpgrade;
        private readonly Action<RenewalDone> _onRenewed;
        private readonly Action _onEssence;
        private readonly Action<DecorationPurchase> _onDecoration;
        private readonly ActivityEndedHandler _onEnded;
        private readonly Action<ActivityId, int, int, int> _onChallenge;
        private readonly Action<GiftOpening> _onGift;
        private readonly Action<WishClaim> _onWish;
        private readonly Action<CalendarClaim> _onCalendar;
        private readonly Action<int> _onJournalGoal;
        private readonly Action<int, GrantedRewards> _onJournalClaim;
        private readonly Action<int> _onDay;
        private readonly Action<uint, bool> _onTouched;
        private readonly Action<uint, ContentId, int> _onTreat;
        private readonly Action<uint, int> _onBond;
        private readonly ExpeditionWelcomedHandler _onWelcomed;
        private readonly GardenPlantedHandler _onPlanted;
        private readonly Action<int> _onWatered;
        private readonly GardenHarvestedHandler _onHarvested;
        private readonly AlbumGainHandler _onSticker;
        private readonly Action<int> _onStarsSpent;
        private readonly Action<string> _onAlbumTitle;
        private bool _disposed;

        public AchievementHooks(AchievementService achievements, ProfileItemService items, PlayerState state, IGameClock clock,
            ContentRegistry? content = null, EconomyService? economy = null, HatchService? hatch = null,
            MasteryService? mastery = null, ProgressionServices? progression = null, ActivityServices? activities = null,
            DailyVisitServices? daily = null, ExpeditionService? expeditions = null, GardenService? garden = null,
            AlbumService? albums = null, AlbumHooks? albumHooks = null)
        {
            _achievements = achievements;
            _items = items;
            _state = state;
            _clock = clock;
            _content = content;
            _hatch = hatch;
            _mastery = mastery;
            _progression = progression;
            _activities = activities;
            _daily = daily;
            _expeditions = expeditions;
            _garden = garden;
            _albums = albums;
            _albumHooks = albumHooks;
            Recount = new AchievementRecount(achievements, items, state, content);

            achievements.Economy ??= economy;
            achievements.Inventory ??= state.Inventory;
            achievements.Items ??= items;

            _onHatched = OnHatched;
            _onMastery = OnMastery;
            _onBiomeMastery = OnBiomeMastery;
            _onRefuge = OnRefuge;
            _onBiome = OnBiome;
            _onZone = OnZone;
            _onUpgrade = OnUpgrade;
            _onRenewed = OnRenewed;
            _onEssence = OnEssence;
            _onDecoration = OnDecoration;
            _onEnded = OnEnded;
            _onChallenge = OnChallenge;
            _onGift = OnGift;
            _onWish = OnWish;
            _onCalendar = OnCalendar;
            _onJournalGoal = OnJournalGoal;
            _onJournalClaim = OnJournalClaim;
            _onDay = OnDay;
            _onTouched = OnTouched;
            _onTreat = OnTreat;
            _onBond = OnBond;
            _onWelcomed = OnWelcomed;
            _onPlanted = OnPlanted;
            _onWatered = OnWatered;
            _onHarvested = OnHarvested;
            _onSticker = OnSticker;
            _onStarsSpent = OnStarsSpent;
            _onAlbumTitle = OnAlbumTitle;

            if (hatch != null)
                hatch.Hatched += _onHatched;
            if (mastery != null)
                mastery.LeveledUp += _onMastery;
            if (progression != null)
            {
                progression.Mastery.LeveledUp += _onBiomeMastery;
                progression.Refuge.LeveledUp += _onRefuge;
                progression.Gates.Opened += _onBiome;
                progression.Zones.Opened += _onZone;
                progression.Upgrades.Purchased += _onUpgrade;
                progression.Renewal.Renewed += _onRenewed;
                progression.Essence.Changed += _onEssence;
                progression.Decorations.Purchased += _onDecoration;
            }
            if (activities != null)
            {
                activities.Runner.Ended += _onEnded;
                activities.Runner.ChallengeImproved += _onChallenge;
            }
            if (daily != null)
            {
                daily.Gift.Opened += _onGift;
                daily.Wishes.Claimed += _onWish;
                daily.Calendar.Claimed += _onCalendar;
                daily.Journal.GoalReached += _onJournalGoal;
                daily.Journal.Claimed += _onJournalClaim;
                daily.DayChanged += _onDay;
                daily.Bond.Touched += _onTouched;
                daily.Bond.TreatGiven += _onTreat;
                daily.Bond.BondGained += _onBond;
            }
            if (expeditions != null)
                expeditions.Welcomed += _onWelcomed;
            if (garden != null)
            {
                garden.Planted += _onPlanted;
                garden.SproutWatered += _onWatered;
                garden.Harvested += _onHarvested;
            }
            if (albums != null)
            {
                albums.StickerGained += _onSticker;
                albums.StarsSpent += _onStarsSpent;
            }
            if (albumHooks != null)
                albumHooks.TitleEarned += _onAlbumTitle;

            items.GiveStarting();
            Recount.All();
            CountSeason();
            SyncJournal();
        }

        /// <summary>The state counters' recount (the hooks call it per group; tests and the debug panel may too).</summary>
        public AchievementRecount Recount { get; }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_hatch != null)
                _hatch.Hatched -= _onHatched;
            if (_mastery != null)
                _mastery.LeveledUp -= _onMastery;
            if (_progression != null)
            {
                _progression.Mastery.LeveledUp -= _onBiomeMastery;
                _progression.Refuge.LeveledUp -= _onRefuge;
                _progression.Gates.Opened -= _onBiome;
                _progression.Zones.Opened -= _onZone;
                _progression.Upgrades.Purchased -= _onUpgrade;
                _progression.Renewal.Renewed -= _onRenewed;
                _progression.Essence.Changed -= _onEssence;
                _progression.Decorations.Purchased -= _onDecoration;
            }
            if (_activities != null)
            {
                _activities.Runner.Ended -= _onEnded;
                _activities.Runner.ChallengeImproved -= _onChallenge;
            }
            if (_daily != null)
            {
                _daily.Gift.Opened -= _onGift;
                _daily.Wishes.Claimed -= _onWish;
                _daily.Calendar.Claimed -= _onCalendar;
                _daily.Journal.GoalReached -= _onJournalGoal;
                _daily.Journal.Claimed -= _onJournalClaim;
                _daily.DayChanged -= _onDay;
                _daily.Bond.Touched -= _onTouched;
                _daily.Bond.TreatGiven -= _onTreat;
                _daily.Bond.BondGained -= _onBond;
            }
            if (_expeditions != null)
                _expeditions.Welcomed -= _onWelcomed;
            if (_garden != null)
            {
                _garden.Planted -= _onPlanted;
                _garden.SproutWatered -= _onWatered;
                _garden.Harvested -= _onHarvested;
            }
            if (_albums != null)
            {
                _albums.StickerGained -= _onSticker;
                _albums.StarsSpent -= _onStarsSpent;
            }
            if (_albumHooks != null)
                _albumHooks.TitleEarned -= _onAlbumTitle;
        }

        // ------------------------------------------------------------------------------------------- hatching and collection

        private void OnHatched(HatchResult result)
        {
            if (!result.Succeeded)
                return;
            var a = _achievements;
            a.Add(AchievementCounter.Hatches);
            a.AddRunHatches(1);
            a.AddDayHatches(TodayKey(), 1);
            if (_content != null && _content.Eggs.TryGet(result.Egg, out var egg))
            {
                switch (egg.Tier)
                {
                    case EggTier.Focus:
                        a.Add(AchievementCounter.FocusHatches);
                        break;
                    case EggTier.Discovery:
                        a.Add(AchievementCounter.DiscoveryHatches);
                        break;
                    case EggTier.Event:
                        a.Add(AchievementCounter.EventEggs);
                        break;
                }
                int biome = AlbumData.BiomeIndex(egg.Biome);
                if (biome >= 0)
                {
                    a.AddDistinctBit(AchievementCounter.HatchBiomes, biome);
                    // « Œufs Basic et Focus du biome » (doc 01 Succès, Biomes).
                    if (egg.Tier == EggTier.Basic || egg.Tier == EggTier.Focus)
                        a.Add(AchievementCounter.BiomeHatches, 1, biome);
                }
            }
            a.AddDistinctBit(AchievementCounter.HatchRarities, (int)result.Animal.Rarity);
            CountObtained(in result.Animal);
            if (result.NewSpecies || result.NewVariant)
                Recount.Collection();
        }

        /// <summary>
        /// An animal arrived in the collection (« obtenir »: a hatch, a breeding, an event egg, a visitor kept, an event path):
        /// its traits, its rarity, its species and trait pairs. Hatches call it; the systems still to come call it too.
        /// </summary>
        public void CountObtained(in AnimalInstance animal)
        {
            var a = _achievements;
            uint traits = animal.TraitBits;
            if ((traits & (uint)AnimalTrait.Tiny) != 0)
                a.Add(AchievementCounter.TraitTiny);
            if ((traits & (uint)AnimalTrait.Giant) != 0)
                a.Add(AchievementCounter.TraitGiant);
            if ((traits & (uint)AnimalTrait.Shiny) != 0)
                a.Add(AchievementCounter.TraitShiny);
            if ((traits & (uint)AnimalTrait.Golden) != 0)
                a.Add(AchievementCounter.TraitGolden);
            if ((traits & (uint)AnimalTrait.Albino) != 0)
                a.Add(AchievementCounter.TraitAlbino);
            if ((traits & (uint)AnimalTrait.Melanistic) != 0)
                a.Add(AchievementCounter.TraitMelanistic);
            switch (animal.Rarity)
            {
                case RarityTier.Rare:
                    a.Add(AchievementCounter.RarityRare);
                    break;
                case RarityTier.Epic:
                    a.Add(AchievementCounter.RarityEpic);
                    break;
                case RarityTier.Legendary:
                    a.Add(AchievementCounter.RarityLegendary);
                    break;
                case RarityTier.Fabulous:
                    a.Add(AchievementCounter.RarityFabulous);
                    break;
            }
            int species = ProfileData.SpeciesIndex(animal.Species);
            if (species >= 0)
                a.AddSpeciesTraits(species, (int)traits);
        }

        private void OnMastery(MasteryGain gain)
        {
            if (gain.LeveledUp)
                Recount.Mastery();
        }

        // ------------------------------------------------------------------------------------------- world and renewal

        private void OnBiomeMastery(BiomeMasteryGain gain)
        {
            if (gain.LeveledUp)
                Recount.World();
        }

        private void OnRefuge(RefugeLevelUp levelUp) => Recount.World();

        private void OnBiome(BiomeOpening opening) => Recount.World();

        private void OnZone(ZoneOpening opening) => Recount.World();

        private void OnUpgrade(UpgradePurchase purchase)
        {
            int levels = purchase.LevelTo - purchase.LevelFrom;
            if (levels <= 0)
                return;
            _achievements.Add(AchievementCounter.LightLevels, levels);
            _achievements.AddRunLightLevels(levels);
        }

        private void OnRenewed(RenewalDone done)
        {
            var a = _achievements;
            a.Raise(AchievementCounter.Renewals, done.Count);
            a.Raise(AchievementCounter.BiomesAtRenewal, _state.Progression.Renewal.BiomesAtLast);
            a.CountRenewalEssence(done.Gain.Earned);
            a.BeginRun();
            Recount.Renewal();
            Recount.World();
        }

        private void OnEssence() => Recount.Renewal();

        private void OnDecoration(DecorationPurchase purchase) => Recount.Decorations();

        // ------------------------------------------------------------------------------------------- activities

        /// <summary>What a game adds to its activity's achievement (ACH-095 to 110).</summary>
        public static long UnitsOf(in ActivityGameResult result)
        {
            int id = (int)result.Activity;
            var unit = (uint)id < (uint)Units.Length ? Units[id] : ActivityUnit.None;
            switch (unit)
            {
                case ActivityUnit.Games:
                    return 1;
                case ActivityUnit.Score:
                    return result.Score > 0 ? result.Score : 0;
                case ActivityUnit.Successes:
                    return result.Stars > 0 ? 1 : 0;
                default:
                    return 0;
            }
        }

        private void OnEnded(in ActivityGameResult result)
        {
            if (result.Abandoned || result.Refunded)
                return;
            var a = _achievements;
            int activity = (int)result.Activity;
            a.Add(AchievementCounter.ActivityProgress, UnitsOf(in result), activity);
            if (result.Mode == ActivityMode.Practice)
                a.Add(AchievementCounter.PracticeGames);
            else if (result.Rewarded)
            {
                // Practice games count for the activities' achievements, except stars, records and 3-star games.
                a.Add(AchievementCounter.ActivityStars, result.Stars);
                if (result.NewRecord)
                    a.Add(AchievementCounter.PersonalBests);
                if (result.Stars >= 3)
                    a.Add(AchievementCounter.ThreeStarGames);
            }
            if (result.Stars > 0)
            {
                int slot = ActivityCatalog.SlotOf(result.Activity);
                if (slot >= 0)
                    a.AddDistinctBit(AchievementCounter.ActivitiesStarred, slot);
            }
            if (result.ChallengeImproved)
                Recount.Activities();
        }

        private void OnChallenge(ActivityId activity, int challenge, int stars, int attempts) => Recount.Activities();

        /// <summary>An animal took part in a game (ACH-118) and the bond points it earned there (ACH-119).</summary>
        public void CountActivityAnimal(uint animal, int bondPoints)
        {
            _achievements.AddDistinctId(AchievementCounter.AnimalsPlayed, animal);
            _achievements.Add(AchievementCounter.ActivityBondPoints, bondPoints);
        }

        // ------------------------------------------------------------------------------------------- daily rendez-vous

        private void OnGift(GiftOpening opening) => _achievements.Add(AchievementCounter.DailyGifts);

        private void OnWish(WishClaim claim)
        {
            var a = _achievements;
            a.Add(AchievementCounter.WishesGranted);
            a.AddDistinctId(AchievementCounter.AnimalsWished, claim.Wish.Animal);
            a.AddDayWish(TodayKey());
        }

        private void OnCalendar(CalendarClaim claim)
        {
            var a = _achievements;
            a.Add(AchievementCounter.CalendarDays);
            a.Raise(AchievementCounter.CalendarsComplete, a.Progress(AchievementData.IndexOf(AchievementCounter.CalendarDays)) / 28);
        }

        private void OnJournalGoal(int goal) => SyncJournal();

        private void OnJournalClaim(int goal, GrantedRewards granted) => SyncJournal();

        /// <summary>The weekly journal's goals reached and its completion, each counted once per week.</summary>
        private void SyncJournal()
        {
            var d = _state.DailyVisit;
            var s = _achievements.State;
            if (d.JournalStartKey == 0)
                return;
            if (s.JournalWeek != d.JournalStartKey)
            {
                s.JournalWeek = d.JournalStartKey;
                s.JournalGoalsCounted = 0;
                s.JournalCompleteCounted = false;
            }
            int reached = 0;
            for (int i = 0; i < d.GoalCount && i < d.Goals.Length; i++)
                if (d.Goals[i].Target > 0 && d.Goals[i].Progress >= d.Goals[i].Target)
                    reached++;
            if (reached > s.JournalGoalsCounted)
            {
                _achievements.Add(AchievementCounter.JournalGoals, reached - s.JournalGoalsCounted);
                s.JournalGoalsCounted = reached;
            }
            if (d.JournalCompleteClaimed && !s.JournalCompleteCounted)
            {
                s.JournalCompleteCounted = true;
                _achievements.Add(AchievementCounter.JournalsComplete);
            }
        }

        private void OnDay(int dayKey)
        {
            CountSeason();
            SyncJournal();
        }

        /// <summary>The refuge season of today, counted once (ACH-199; a winter spans December to February).</summary>
        public void CountSeason()
        {
            var now = DailyDay.Now(_state, _clock);
            int season = AlbumHooks.SeasonOfMonth(now.Month);
            int year = now.Month <= 2 ? now.Year - 1 : now.Year;
            int key = year * 4 + season;
            var s = _achievements.State;
            if (s.SeasonKey == key)
                return;
            s.SeasonKey = key;
            _achievements.Add(AchievementCounter.RefugeSeasons);
        }

        private int TodayKey()
        {
            if (_daily != null && _daily.TodayKey > 0)
                return _daily.TodayKey;
            var now = DailyDay.Now(_state, _clock);
            return now.Year * 10000 + now.Month * 100 + now.Day;
        }

        // ------------------------------------------------------------------------------------------- bonds

        private void OnTouched(uint animal, bool counted)
        {
            if (!counted)
                return;
            _achievements.Add(AchievementCounter.Pets);
            _achievements.AddDistinctId(AchievementCounter.AnimalsPetted, animal);
        }

        private void OnTreat(uint animal, ContentId treat, int points) => _achievements.Add(AchievementCounter.TreatsGiven);

        private void OnBond(uint animal, int added)
        {
            // The points first: the recount raises them to the points held, never above what was earned.
            _achievements.Add(AchievementCounter.BondPoints, added);
            Recount.Bonds();
        }

        // ------------------------------------------------------------------------------------------- expeditions and garden

        private void OnWelcomed(in ExpeditionWelcomeInfo info)
        {
            ref readonly var back = ref info.Return;
            var a = _achievements;
            a.Add(AchievementCounter.ExpeditionsDone);
            // A sped-up expedition counts its planned hours.
            a.Add(AchievementCounter.ExpeditionHours, HoursOf(back.Duration));
            if (back.Duration == ExpeditionDuration.TwelveHours)
                a.Add(AchievementCounter.LongExpeditions);
            if (back.TeamSize >= ExpeditionState.MaxTeam)
                a.Add(AchievementCounter.TeamExpeditions);
            if (back.Loot.DiscoveryEggs > 0)
                a.Add(AchievementCounter.ExpeditionDiscoveryEggs, back.Loot.DiscoveryEggs);
            int destination = ExpeditionCatalog.IndexOf(back.Destination);
            if (destination >= 0)
                a.AddDistinctBit(AchievementCounter.Destinations, destination);
            if (ExpeditionCatalog.TryGet(back.Destination, out var place))
            {
                int biome = AlbumData.BiomeIndex(place.Destination.Biome);
                if (biome >= 0)
                    a.Add(AchievementCounter.BiomeExpeditions, 1, biome);
            }
        }

        public static int HoursOf(ExpeditionDuration duration) =>
            duration == ExpeditionDuration.OneHour ? 1 : duration == ExpeditionDuration.FourHours ? 4 : duration == ExpeditionDuration.EightHours ? 8 : 12;

        private void OnPlanted(in GardenPlantInfo info) => _achievements.Add(AchievementCounter.SeedsPlanted);

        private void OnWatered(int plot) => _achievements.Add(AchievementCounter.SproutsWatered);

        private void OnHarvested(in GardenHarvestInfo info)
        {
            ref readonly var harvest = ref info.Harvest;
            var a = _achievements;
            if (info.Kind == GardenPlantKind.Flower)
                a.Add(AchievementCounter.FlowersHarvested, harvest.MainCount);
            else if (info.Kind == GardenPlantKind.Fruit)
                a.Add(AchievementCounter.FruitsHarvested, harvest.MainCount);
            if (harvest.Dyes > 0)
                a.Add(AchievementCounter.DyesHarvested, harvest.Dyes);
            int plant = GardenCatalog.IndexOf(harvest.Plant);
            if (plant >= 0)
                a.AddDistinctBit(AchievementCounter.PlantsHarvested, plant);
        }

        // ------------------------------------------------------------------------------------------- albums

        private void OnSticker(in AlbumGainInfo info)
        {
            var a = _achievements;
            if (info.StarsAdded > 0)
                a.Add(AchievementCounter.AlbumStarsEarned, info.StarsAdded);
            switch (info.Source)
            {
                case StickerSource.Record:
                    a.Add(AchievementCounter.RecordStickers);
                    a.Add(AchievementCounter.ActivityStickers);
                    break;
                case StickerSource.ActivityGame:
                    a.Add(AchievementCounter.ActivityStickers);
                    break;
                case StickerSource.Expedition:
                    a.Add(AchievementCounter.ExpeditionStickers);
                    break;
            }
            if (info.New)
                Recount.Albums();
        }

        private void OnStarsSpent(int sticker) => _achievements.Add(AchievementCounter.StickersPicked);

        private void OnAlbumTitle(string key) => _items.OnAlbumTitle(key);

        // ------------------------------------------------------------------------------------------- systems to come

        /// <summary>A photo taken (ACH-173), with the pose used (its index, −1 without; ACH-174).</summary>
        public void CountPhoto(int pose)
        {
            _achievements.Add(AchievementCounter.Photos);
            if (pose >= 0)
                _achievements.AddDistinctBit(AchievementCounter.PosesUsed, pose);
        }

        /// <summary>A big event's shared goal reached, or the personal one standing in (ACH-185 and the event's pose).</summary>
        public void CountSharedGoal(int eventNumber)
        {
            _achievements.Add(AchievementCounter.SharedGoals);
            _items.OnEventGoal(eventNumber);
        }

        /// <summary>An accessory equipped (ACH-171 counts each different one).</summary>
        public void CountAccessory(ContentId accessory) => _achievements.AddDistinctId(AchievementCounter.AccessoriesEquipped, accessory.Value);
    }
}
