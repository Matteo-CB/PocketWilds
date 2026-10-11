#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T44C: wires the existing systems' events to the journal's 63 moments (doc 01 Succès, section 6):
    /// <list type="bullet">
    /// <item>The first egg (a new account), hatches (first hatch, three animals, population, new species, first variant,
    /// rarities, secret, traits), species mastery 10 and 30, biomes, zones, refuge levels (every 10), bonds 1, 3 and 5.</item>
    /// <item>Activities (first rewarded game, three stars, record), expeditions (first one, postcards), the garden (first
    /// harvest, the 12 starter plants), wishes, the calendar's cycles, the weekly journal, the albums (pages, biome albums),
    /// achievements' first Star, the first title chosen, Renewals (first, records), the refuge seasons, rare visitors.</item>
    /// <item>Systems without an event or not built yet keep a hook ready (names, favorites, Codex, companions, the activities'
    /// 10 challenges, placed decorations, breeding, big events, photos, outfits, Circles, world surprises).</item>
    /// <item>An older account's past is marked done once, without pages (no « first hatch » on its 50th animal).</item>
    /// </list>
    /// Every page also counts for ACH-200. Handlers are cached delegates; the bootstrap builds it after the services it lists.
    /// </summary>
    public sealed class JournalHooks : IDisposable
    {
        private static readonly int[] Population = { 100, 250, 500, 1000 };
        private const int GardenStarterPlants = 12;

        private readonly JournalService _journal;
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
        private readonly AchievementService? _achievements;
        private readonly ProfileItemService? _items;
        private readonly VisitorService? _visitors;
        private readonly Dictionary<ContentId, ContentId> _firstVariant = new Dictionary<ContentId, ContentId>();

        private readonly Action<HatchResult> _onHatched;
        private readonly Action<MasteryGain> _onMastery;
        private readonly Action<RefugeLevelUp> _onRefuge;
        private readonly Action<BiomeOpening> _onBiome;
        private readonly Action<ZoneOpening> _onZone;
        private readonly Action<RenewalDone> _onRenewed;
        private readonly ActivityEndedHandler _onEnded;
        private readonly Action<WishClaim> _onWish;
        private readonly Action<CalendarClaim> _onCalendar;
        private readonly Action<int, GrantedRewards> _onWeekly;
        private readonly Action<int> _onDay;
        private readonly Action<uint, int> _onBond;
        private readonly ExpeditionStartedHandler _onStarted;
        private readonly ExpeditionWelcomedHandler _onWelcomed;
        private readonly GardenHarvestedHandler _onHarvested;
        private readonly AlbumRewardHandler _onAlbumReward;
        private readonly Action<int, AchievementTier> _onTier;
        private readonly ProfileItemHandler _onSelection;
        private readonly VisitorGreetedHandler _onGreeted;
        private readonly Action<int, int> _onWritten;
        private bool _disposed;

        public JournalHooks(JournalService journal, PlayerState state, IGameClock clock, ContentRegistry? content = null,
            HatchService? hatch = null, MasteryService? mastery = null, ProgressionServices? progression = null,
            ActivityServices? activities = null, DailyVisitServices? daily = null, ExpeditionService? expeditions = null,
            GardenService? garden = null, AlbumService? albums = null, AchievementService? achievements = null,
            ProfileItemService? items = null, VisitorService? visitors = null)
        {
            _journal = journal;
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
            _achievements = achievements;
            _items = items;
            _visitors = visitors;
            if (content != null)
            {
                // « Variante 1 » of a species: its first variant in the content's order.
                for (int i = 0; i < content.Variants.Count; i++)
                {
                    var v = content.Variants[i];
                    if (!_firstVariant.ContainsKey(v.Species))
                        _firstVariant.Add(v.Species, v.Id);
                }
            }

            _onHatched = OnHatched;
            _onMastery = OnMastery;
            _onRefuge = OnRefuge;
            _onBiome = OnBiome;
            _onZone = OnZone;
            _onRenewed = OnRenewed;
            _onEnded = OnEnded;
            _onWish = OnWish;
            _onCalendar = OnCalendar;
            _onWeekly = OnWeekly;
            _onDay = OnDay;
            _onBond = OnBond;
            _onStarted = OnStarted;
            _onWelcomed = OnWelcomed;
            _onHarvested = OnHarvested;
            _onAlbumReward = OnAlbumReward;
            _onTier = OnTier;
            _onSelection = OnSelection;
            _onGreeted = OnGreeted;
            _onWritten = OnWritten;

            if (hatch != null)
                hatch.Hatched += _onHatched;
            if (mastery != null)
                mastery.LeveledUp += _onMastery;
            if (progression != null)
            {
                progression.Refuge.LeveledUp += _onRefuge;
                progression.Gates.Opened += _onBiome;
                progression.Zones.Opened += _onZone;
                progression.Renewal.Renewed += _onRenewed;
            }
            if (activities != null)
                activities.Runner.Ended += _onEnded;
            if (daily != null)
            {
                daily.Wishes.Claimed += _onWish;
                daily.Calendar.Claimed += _onCalendar;
                daily.Journal.Claimed += _onWeekly;
                daily.DayChanged += _onDay;
                daily.Bond.BondGained += _onBond;
            }
            if (expeditions != null)
            {
                expeditions.Started += _onStarted;
                expeditions.Welcomed += _onWelcomed;
            }
            if (garden != null)
                garden.Harvested += _onHarvested;
            if (albums != null)
                albums.RewardEarned += _onAlbumReward;
            if (achievements != null)
                achievements.TierReached += _onTier;
            if (items != null)
                items.SelectionChanged += _onSelection;
            if (visitors != null)
                visitors.Greeted += _onGreeted;
            journal.Written += _onWritten;

            Initialize();
            _achievements?.Raise(AchievementCounter.JournalPages, _journal.PageCount);
        }

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
                _progression.Refuge.LeveledUp -= _onRefuge;
                _progression.Gates.Opened -= _onBiome;
                _progression.Zones.Opened -= _onZone;
                _progression.Renewal.Renewed -= _onRenewed;
            }
            if (_activities != null)
                _activities.Runner.Ended -= _onEnded;
            if (_daily != null)
            {
                _daily.Wishes.Claimed -= _onWish;
                _daily.Calendar.Claimed -= _onCalendar;
                _daily.Journal.Claimed -= _onWeekly;
                _daily.DayChanged -= _onDay;
                _daily.Bond.BondGained -= _onBond;
            }
            if (_expeditions != null)
            {
                _expeditions.Started -= _onStarted;
                _expeditions.Welcomed -= _onWelcomed;
            }
            if (_garden != null)
                _garden.Harvested -= _onHarvested;
            if (_albums != null)
                _albums.RewardEarned -= _onAlbumReward;
            if (_achievements != null)
                _achievements.TierReached -= _onTier;
            if (_items != null)
                _items.SelectionChanged -= _onSelection;
            if (_visitors != null)
                _visitors.Greeted -= _onGreeted;
            _journal.Written -= _onWritten;
        }

        // ------------------------------------------------------------------------------------------- set-up

        /// <summary>
        /// Once per account: a new one gets « An egg is waiting at the refuge »; an older one's past moments are marked done
        /// without pages. The current refuge season counts as arrived (its page comes with the next season).
        /// </summary>
        private void Initialize()
        {
            var j = _state.Journal;
            if (!j.Initialized)
            {
                j.Initialized = true;
                var collection = _state.Collection;
                if (collection.Count == 0)
                    _journal.Add(JournalMoment.FirstEgg);
                else
                    MarkPast();
                _journal.MarkDone(JournalMoment.Season, (uint)CurrentSeason());
            }
        }

        private void MarkPast()
        {
            var collection = _state.Collection;
            _journal.MarkDone(JournalMoment.FirstEgg);
            _journal.MarkDone(JournalMoment.FirstHatch);
            if (collection.Count >= 3)
                _journal.MarkDone(JournalMoment.ThreeAnimals);
            for (int i = 0; i < Population.Length; i++)
                if (collection.Count >= Population[i])
                    _journal.MarkDone(JournalMoment.Population, (uint)Population[i]);
            if (collection.CustomNames.Count > 0)
                _journal.MarkDone(JournalMoment.FirstName);
            for (int i = 0; i < collection.Count; i++)
            {
                ref readonly var a = ref collection[i];
                if (a.IsFavorite)
                    _journal.MarkDone(JournalMoment.FirstFavorite);
                MarkObtained(in a);
                if (a.BondPoints >= AchievementRecount.BondLevel1)
                    _journal.MarkDone(JournalMoment.FirstBond1);
                if (a.BondPoints >= AchievementRecount.BondLevel3)
                    _journal.MarkDone(JournalMoment.FirstBond3);
                if (a.BondPoints >= AchievementRecount.BondLevel5)
                    _journal.MarkDone(JournalMoment.FirstBond5);
            }
            foreach (var species in collection.DiscoveredSpecies)
                _journal.MarkDone(JournalMoment.NewSpecies, species.Value);
            if (_state.Expeditions.Started > 0)
                _journal.MarkDone(JournalMoment.FirstExpedition);
            int renewals = AchievementData.IndexOf(AchievementCounter.Renewals);
            if ((renewals >= 0 && _state.Achievements.Progress[renewals] > 0) || _state.Progression.Renewal.EssenceEarned > 0)
                _journal.MarkDone(JournalMoment.FirstRenewal);
            _state.Journal.GardenPlants = _state.Achievements.PlantsHarvested;
            if (_state.Journal.GardenPlants != 0)
                _journal.MarkDone(JournalMoment.FirstHarvest);
        }

        private void MarkObtained(in AnimalInstance a)
        {
            int rarity = RarityMoment(a.Rarity);
            if (rarity >= 0)
                _journal.MarkDone(rarity);
            for (int t = 0; t < 6; t++)
                if ((a.TraitBits & TraitBit(t)) != 0)
                    _journal.MarkDone(JournalMoment.FirstTiny + t);
            if (_content != null && _content.Variants.TryGet(a.Variant, out var variant) && !variant.IsVisitor && variant.MasteryLevel <= 0)
            {
                if (variant.IsSecret)
                    _journal.MarkDone(JournalMoment.FirstSecret);
                else if (!IsFirstVariant(variant))
                    _journal.MarkDone(JournalMoment.FirstVariant);
            }
        }

        // ------------------------------------------------------------------------------------------- collection

        private void OnHatched(HatchResult result)
        {
            if (!result.Succeeded)
                return;
            var animal = result.Animal;
            _journal.Add(JournalMoment.FirstHatch, 0, animal.InstanceId, NameOf(in animal));
            OnObtained(in animal, result.NewSpecies, result.NewVariant);
        }

        /// <summary>
        /// An animal arrived in the collection (a hatch, a visitor kept, a breeding, an event path): three animals, the
        /// population, a new species, the first variant, rarities, a secret, traits. Hatches and visitors call it.
        /// </summary>
        public void OnObtained(in AnimalInstance animal, bool newSpecies, bool newVariant)
        {
            uint id = animal.InstanceId;
            int count = _state.Collection.Count;
            if (count >= 3)
                _journal.Add(JournalMoment.ThreeAnimals);
            for (int i = 0; i < Population.Length; i++)
                if (count >= Population[i])
                    _journal.Add(JournalMoment.Population, (uint)Population[i], 0, Population[i].ToString(CultureInfo.InvariantCulture));
            if (newSpecies)
                _journal.Add(JournalMoment.NewSpecies, animal.Species.Value, id, SpeciesValue(animal.Species));
            VariantDefinition? variant = null;
            if (_content != null && _content.Variants.TryGet(animal.Variant, out var found))
                variant = found;
            if (variant != null && !variant.IsVisitor && variant.MasteryLevel <= 0)
            {
                if (variant.IsSecret && newVariant)
                    _journal.Add(JournalMoment.FirstSecret, 0, id, Key(variant.DisplayKey));
                else if (newVariant && !variant.IsSecret && !IsFirstVariant(variant))
                    _journal.Add(JournalMoment.FirstVariant, 0, id, Key(variant.DisplayKey));
            }
            int rarity = RarityMoment(animal.Rarity);
            if (rarity >= 0 && !_journal.IsDone(rarity))
                _journal.Add(rarity, 0, id, NameOf(in animal));
            for (int t = 0; t < 6; t++)
                if ((animal.TraitBits & TraitBit(t)) != 0 && !_journal.IsDone(JournalMoment.FirstTiny + t))
                    _journal.Add(JournalMoment.FirstTiny + t, 0, id, NameOf(in animal));
        }

        /// <summary>An animal was given a name (the naming screen calls it: no event yet).</summary>
        public void OnAnimalNamed(uint animal)
        {
            if (!_journal.IsDone(JournalMoment.FirstName))
                _journal.Add(JournalMoment.FirstName, 0, animal, NameOf(animal));
        }

        /// <summary>An animal was added to the favorites (the animal sheet calls it: no event yet).</summary>
        public void OnFavorite(uint animal)
        {
            if (!_journal.IsDone(JournalMoment.FirstFavorite))
                _journal.Add(JournalMoment.FirstFavorite, 0, animal, NameOf(animal));
        }

        private void OnMastery(MasteryGain gain)
        {
            if (gain.Before.Level < 10 && gain.After.Level >= 10)
                _journal.Add(JournalMoment.Mastery10, gain.Species.Value, AnimalOf(gain.Species), SpeciesValue(gain.Species));
            if (gain.Before.Level < 30 && gain.After.Level >= 30)
                _journal.Add(JournalMoment.Mastery30, gain.Species.Value, AnimalOf(gain.Species), SpeciesValue(gain.Species));
        }

        /// <summary>A biome's Codex complete (every color of its six species): the Codex calls it when it knows.</summary>
        public void OnBiomeCodexComplete(ContentId biome) =>
            _journal.Add(JournalMoment.BiomeCodex, biome.Value, biome.Value, BiomeValue(biome));

        // ------------------------------------------------------------------------------------------- world and renewal

        private void OnBiome(BiomeOpening opening) =>
            _journal.Add(JournalMoment.BiomeUnlocked, opening.Biome.Value, opening.Biome.Value, BiomeValue(opening.Biome));

        private void OnZone(ZoneOpening opening)
        {
            var zone = opening.Zone;
            _journal.Add(JournalMoment.ZoneOpened, zone.Id.Value, 0, string.IsNullOrEmpty(zone.DisplayKey) ? null : Key(zone.DisplayKey));
        }

        private void OnRefuge(RefugeLevelUp levelUp)
        {
            for (int level = (levelUp.From / 10 + 1) * 10; level <= levelUp.To; level += 10)
                _journal.Add(JournalMoment.RefugeLevel, (uint)level, 0, level.ToString(CultureInfo.InvariantCulture));
        }

        private void OnRenewed(RenewalDone done)
        {
            long essence = (long)done.Gain.Earned;
            var j = _state.Journal;
            bool first = _journal.Add(JournalMoment.FirstRenewal);
            if (!first && essence > j.BestRenewalEssence)
                _journal.Add(JournalMoment.RenewalRecord, (uint)done.Count, 0, essence.ToString(CultureInfo.InvariantCulture));
            if (essence > j.BestRenewalEssence)
                j.BestRenewalEssence = essence;
        }

        private void OnDay(int dayKey) => CheckSeason();

        /// <summary>The first time each refuge season arrives (Éveil, Soleil, Récolte, Givre).</summary>
        public void CheckSeason()
        {
            int season = CurrentSeason();
            _journal.Add(JournalMoment.Season, (uint)season, 0, Key("page.season." + season.ToString(CultureInfo.InvariantCulture) + ".name"));
        }

        /// <summary>T48B the active month map (Remote Config « events.refuge_seasons »); null: the default map.</summary>
        public SeasonConfig? Seasons { get; set; }

        private int CurrentSeason() => SeasonConfig.SeasonNumber(Seasons, TimeZoneInfo.ConvertTime(DailyDay.Now(_state, _clock), _journal.Zone).Month);

        // ------------------------------------------------------------------------------------------- bonds and companions

        private void OnBond(uint animal, int added)
        {
            if (!_state.Collection.TryGet(animal, out var a))
                return;
            if (a.BondPoints >= AchievementRecount.BondLevel1)
                _journal.Add(JournalMoment.FirstBond1, 0, animal, NameOf(in a));
            if (a.BondPoints >= AchievementRecount.BondLevel3)
                _journal.Add(JournalMoment.FirstBond3, 0, animal, NameOf(in a));
            if (a.BondPoints >= AchievementRecount.BondLevel5)
                _journal.Add(JournalMoment.FirstBond5, 0, animal, NameOf(in a));
        }

        /// <summary>A companion arrives at the refuge (companions are not built yet): its id, the animal greeting, its name key.</summary>
        public void OnCompanionJoined(ContentId companion, uint animal, string nameKey) =>
            _journal.Add(JournalMoment.CompanionJoined, companion.Value, animal, Key(nameKey));

        public void OnCompanionBond25(ContentId companion, uint animal, string nameKey) =>
            _journal.Add(JournalMoment.CompanionBond25, companion.Value, animal, Key(nameKey));

        // ------------------------------------------------------------------------------------------- activities

        private void OnEnded(in ActivityGameResult result)
        {
            if (result.Abandoned || result.Refunded || result.Mode == ActivityMode.Practice)
                return;
            uint activity = (uint)result.Activity;
            if (activity == 0)
                return;
            string? name = ActivityValue(result.Activity);
            if (result.Rewarded)
                _journal.Add(JournalMoment.FirstActivity, activity, activity, name);
            if (result.Rewarded && result.Stars >= 3)
                _journal.Add(JournalMoment.FirstThreeStars, activity, activity, name);
            if (result.NewRecord && !result.FirstScore)
                _journal.Add(JournalMoment.FirstRecord, activity, activity, name, result.Score.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>The 10 challenges of an activity at 3 stars (the challenges' screen calls it when it sees it).</summary>
        public void OnActivityPerfect(ActivityId activity) =>
            _journal.Add(JournalMoment.ActivityPerfect, (uint)activity, (uint)activity, ActivityValue(activity));

        // ------------------------------------------------------------------------------------------- expeditions and garden

        private void OnStarted(in ExpeditionStartInfo info)
        {
            if (_journal.IsDone(JournalMoment.FirstExpedition))
                return;
            uint animal = (uint)info.Slot < (uint)_state.Expeditions.Slots.Length ? _state.Expeditions.Slots[info.Slot].Animal0 : 0;
            _journal.Add(JournalMoment.FirstExpedition, 0, animal, NameOf(animal), DestinationValue(info.Destination));
        }

        private void OnWelcomed(in ExpeditionWelcomeInfo info)
        {
            ref readonly var back = ref info.Return;
            if (back.PostcardId > 0 && back.FirstFromDestination)
                _journal.Add(JournalMoment.Postcard, back.Destination.Value, (uint)back.PostcardId, DestinationValue(back.Destination));
        }

        private void OnHarvested(in GardenHarvestInfo info)
        {
            var plant = info.Harvest.Plant;
            int index = GardenCatalog.IndexOf(plant);
            string? name = index >= 0 ? Key(GardenCatalog.Plants[index].Key + ".name") : null;
            _journal.Add(JournalMoment.FirstHarvest, 0, 0, name);
            if (index < 0 || index >= GardenStarterPlants)
                return;
            var j = _state.Journal;
            j.GardenPlants |= 1u << index;
            const uint all = (1u << GardenStarterPlants) - 1u;
            if ((j.GardenPlants & all) == all)
                _journal.Add(JournalMoment.GardenComplete);
        }

        // ------------------------------------------------------------------------------------------- daily rendez-vous

        private void OnWish(WishClaim claim) => _journal.Add(JournalMoment.FirstWish, 0, claim.Wish.Animal, NameOf(claim.Wish.Animal));

        private void OnCalendar(CalendarClaim claim)
        {
            if (claim.Number >= 28)
                _journal.Add(JournalMoment.CalendarComplete, (uint)claim.Cycle, claim.Decoration.Value);
        }

        private void OnWeekly(int goal, GrantedRewards granted)
        {
            if (_state.DailyVisit.JournalCompleteClaimed)
                _journal.Add(JournalMoment.WeeklyComplete);
        }

        // ------------------------------------------------------------------------------------------- visitors, albums, profile

        /// <summary>The key of doc 05's SUR-06 (its name « surprise.visitor_arrival.name »).</summary>
        public const string VisitorSurpriseKey = "surprise.visitor_arrival";

        private void OnGreeted(in VisitorGreeting greeting, int visitor)
        {
            if ((uint)visitor >= VisitorData.Count)
                return;
            string name = VisitorData.Row(visitor).NameValue;
            // Doc 05 SUR-06 « Un visiteur arrive »: the first world surprise the game has, on the « Moments » page once.
            _journal.AddSeenSurprise(VisitorSurpriseKey);
            _journal.Add(JournalMoment.FirstVisitor, 0, greeting.JoinedInstanceId, name);
            if (greeting.Joined && _state.Collection.TryGet(greeting.JoinedInstanceId, out var animal))
            {
                _journal.Add(JournalMoment.VisitorStays, (uint)visitor + 1u, greeting.JoinedInstanceId, name);
                OnObtained(in animal, false, true);
            }
        }

        private void OnAlbumReward(in AlbumReward reward)
        {
            if (reward.Kind == AlbumRewardKind.PageComplete)
                _journal.Add(JournalMoment.AlbumPage, (uint)reward.Page + 1u, (uint)reward.Page, PageValue(reward.Page));
            else if (reward.Kind == AlbumRewardKind.AlbumComplete && reward.Album < AlbumData.BiomeAlbums)
                _journal.Add(JournalMoment.AlbumComplete, (uint)reward.Album + 1u, (uint)reward.Album, BiomeValue(AlbumData.BiomeId(reward.Album)));
        }

        private void OnTier(int index, AchievementTier tier)
        {
            if (tier == AchievementTier.Star && !_journal.IsDone(JournalMoment.FirstStarAchievement))
                _journal.Add(JournalMoment.FirstStarAchievement, 0, (uint)index, Key(AchievementData.Row(index).Key + ".name"));
        }

        private void OnSelection(ProfileItemKind kind, int index)
        {
            if (kind == ProfileItemKind.Title && index >= 0 && !_journal.IsDone(JournalMoment.FirstTitle))
                _journal.Add(JournalMoment.FirstTitle, 0, (uint)index, Key(ProfileData.Row(ProfileItemKind.Title, index).Key + ".name"));
        }

        private void OnWritten(int pageId, int moment) => _achievements?.Add(AchievementCounter.JournalPages);

        // ------------------------------------------------------------------------------------------- systems to come

        /// <summary>A decoration placed (placement is not built yet): the first one, and the first landmark.</summary>
        public void OnDecorationPlaced(ContentId decoration, string nameKey, bool landmark)
        {
            _journal.Add(landmark ? JournalMoment.FirstLandmark : JournalMoment.FirstDecoration, 0, decoration.Value, Key(nameKey));
            if (landmark)
                _journal.Add(JournalMoment.FirstDecoration, 0, decoration.Value, Key(nameKey));
        }

        /// <summary>The first breeding egg hatched (breeding is not built yet): the newborn and its parents.</summary>
        public void OnBreedingHatch(uint newborn, uint parent1, uint parent2) =>
            _journal.Add(JournalMoment.FirstBreeding, 0, newborn, NameOf(parent1), NameOf(parent2));

        public void OnMutation(uint animal) => _journal.Add(JournalMoment.FirstMutation, 0, animal, NameOf(animal));

        public void OnThirdGeneration(uint animal) => _journal.Add(JournalMoment.ThirdGeneration, 0, animal, NameOf(animal));

        /// <summary>A big event's first day played (its number 101 to 112 and its name key; events are not built yet).</summary>
        public void OnEventStart(int eventNumber, string nameKey, uint year) =>
            _journal.Add(JournalMoment.EventStart, (uint)eventNumber * 10000u + year, (uint)eventNumber, Key(nameKey));

        public void OnEventFeatured(uint animal, string eventNameKey) =>
            _journal.Add(JournalMoment.EventFeatured, 0, animal, NameOf(animal), Key(eventNameKey));

        public void OnEventSouvenir(string eventNameKey, ContentId decoration, string decorationNameKey) =>
            _journal.Add(JournalMoment.EventSouvenir, decoration.Value, decoration.Value, Key(eventNameKey), Key(decorationNameKey));

        public void OnEventGoal(int eventNumber, string eventNameKey) =>
            _journal.Add(JournalMoment.EventGoal, 0, (uint)eventNumber, Key(eventNameKey));

        /// <summary>A photo kept by the player or a good shot at the Photo Safari (photo mode is not built yet).</summary>
        public void OnPhoto(uint photo, uint animal)
        {
            _journal.Add(JournalMoment.FirstPhoto, 0, photo, NameOf(animal));
            var date = JournalService.DateOf(_journal.Today());
            _journal.Add(JournalMoment.PlayerPhoto, 0, photo, date.ToString("d", CultureInfo.InvariantCulture));
        }

        public void OnOutfit(uint animal, string outfitNameKey) =>
            _journal.Add(JournalMoment.FirstOutfit, 0, animal, NameOf(animal), Key(outfitNameKey));

        public void OnCircle(string circleNameKey) => _journal.Add(JournalMoment.FirstCircle, 0, 0, Key(circleNameKey));

        /// <summary>A world surprise seen (doc 05; the surprises are not built yet): the « Moments » page.</summary>
        public void OnSurpriseSeen(string key) => _journal.AddSeenSurprise(key);

        // ------------------------------------------------------------------------------------------- values

        private static string Key(string textKey) => JournalService.KeyPrefix + textKey;

        private static int RarityMoment(RarityTier rarity) => rarity switch
        {
            RarityTier.Rare => JournalMoment.FirstRare,
            RarityTier.Epic => JournalMoment.FirstEpic,
            RarityTier.Legendary => JournalMoment.FirstLegendary,
            RarityTier.Fabulous => JournalMoment.FirstFabulous,
            _ => -1,
        };

        /// <summary>Tiny, Giant, Shiny, Golden, Albino, Melanistic: the doc's order of memory.first_tiny to first_melanistic.</summary>
        private static uint TraitBit(int index) => index switch
        {
            0 => (uint)AnimalTrait.Tiny,
            1 => (uint)AnimalTrait.Giant,
            2 => (uint)AnimalTrait.Shiny,
            3 => (uint)AnimalTrait.Golden,
            4 => (uint)AnimalTrait.Albino,
            _ => (uint)AnimalTrait.Melanistic,
        };

        private bool IsFirstVariant(VariantDefinition variant) =>
            !_firstVariant.TryGetValue(variant.Species, out var first) || first == variant.Id;

        /// <summary>{name}: the animal's own name, else its species (a text key).</summary>
        private string? NameOf(uint animal) => _state.Collection.TryGet(animal, out var a) ? NameOf(in a) : null;

        private string? NameOf(in AnimalInstance animal)
        {
            if (_state.Collection.CustomNames.TryGetValue(animal.InstanceId, out var name) && !string.IsNullOrEmpty(name))
                return name;
            return SpeciesValue(animal.Species);
        }

        private string? SpeciesValue(ContentId species) =>
            _content != null && _content.Species.TryGet(species, out var def) ? Key(def.DisplayKey) : null;

        private string? BiomeValue(ContentId biome) =>
            _content != null && _content.Biomes.TryGet(biome, out var def) ? Key(def.DisplayKey) : null;

        private static string? ActivityValue(ActivityId activity) =>
            ActivityCatalog.TryGet(activity, out var def) ? Key("activity." + def.Key + ".name") : null;

        private static string? DestinationValue(ContentId destination) =>
            ExpeditionCatalog.TryGet(destination, out var place) ? Key(place.Destination.Key + ".name") : null;

        private static readonly string[] BiomeNames =
        {
            "grassland", "forest", "lake", "mountain", "snow", "jungle", "desert", "sakura", "mushroom", "cloudlands", "lunar",
            "prehistoric", "starlight",
        };

        /// <summary>{page}: « page.grassland.1.name », « page.event.101.name », « page.season.1.name », « page.activity.001.name ».</summary>
        public static string? PageValue(int page)
        {
            var inv = CultureInfo.InvariantCulture;
            if (page < 0)
                return null;
            if (page < AlbumData.FirstEventPage)
                return Key("page." + BiomeNames[page / 2] + "." + (page % 2 + 1).ToString(inv) + ".name");
            if (page < AlbumData.FirstSeasonPage)
                return Key("page.event." + (101 + page - AlbumData.FirstEventPage).ToString(inv) + ".name");
            if (page < AlbumData.FirstActivityPage)
                return Key("page.season." + (1 + page - AlbumData.FirstSeasonPage).ToString(inv) + ".name");
            if (page < AlbumData.FirstActivityPage + AlbumData.ActivityPages)
                return Key("page.activity." + (1 + page - AlbumData.FirstActivityPage).ToString("000", inv) + ".name");
            return null;
        }

        /// <summary>An animal of the species for its portrait (0: none, the thumbnail stands in).</summary>
        private uint AnimalOf(ContentId species)
        {
            var collection = _state.Collection;
            for (int i = collection.Count - 1; i >= 0; i--)
                if (collection[i].Species == species)
                    return collection[i].InstanceId;
            return 0;
        }
    }
}
