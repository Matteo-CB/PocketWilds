#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T44A: wires every sticker source to the albums (doc 01 Albums, section 1.4) and keeps them open and their pages in
    /// step with the world:
    /// <list type="bullet">
    /// <item>Opening: refuge level 12 and day 5 (<see cref="RefugeLevelService.LeveledUp"/>, <see cref="DailyVisitServices.DayChanged"/>);
    /// the pages already earned open at once (biomes unlocked with their companion, activities unlocked, the season), then
    /// STK-GL-01, the zones already open and the milestones already reached; stickers kept in the inventory before become
    /// 10 album stars each (a sticker of choice).</item>
    /// <item>Activities (<see cref="ActivityRunner.Ended"/>): the record's sticker, the chance's draw (the game's active biome),
    /// a paid game's fixed order; 07 at the first 3-star game, 09/10/12 by challenge stars (challenges count in practice too),
    /// 11 at the 5th new record.</item>
    /// <item>Wishes (the 1st and 2nd of the day 20 %, the 3rd 100 %, then 20 %), the daily gift's and the calendar's sticker
    /// lots (missing ones), expeditions (the loot's sticker drawn at departure, its page chosen at the return), garden
    /// harvests (5, 10, 15 % by stars; a sprout finished with Gems follows the fixed counter), zones and passages opened,
    /// biomes opened, activities unlocked, the season of the day.</item>
    /// <item>Page and album rewards: Gems, tickets (past the maximum), decorations and monuments into the inventory, titles
    /// raised for the titles system (<see cref="TitleEarned"/>, T44B).</item>
    /// </list>
    /// The bootstrap builds it after the albums, the activities, the rendez-vous, the expeditions and the garden, and
    /// disposes it with them. Handlers are cached delegates: no allocation per event.
    /// </summary>
    public sealed class AlbumHooks : IDisposable
    {
        private readonly AlbumService _albums;
        private readonly ActivityServices _activities;
        private readonly ActivityContext _ctx;
        private readonly DailyVisitServices? _daily;
        private readonly ExpeditionService? _expeditions;
        private readonly GardenService? _garden;
        private readonly ProgressionServices? _progression;

        private readonly ActivityEndedHandler _onEnded;
        private readonly Action<ActivityId, int, int, int> _onChallenge;
        private readonly Action<ActivityId> _onUnlocked;
        private readonly Action<WishClaim> _onWish;
        private readonly Action<GiftOpening> _onGift;
        private readonly Action<CalendarClaim> _onCalendar;
        private readonly Action<int> _onDay;
        private readonly ExpeditionWelcomedHandler _onWelcomed;
        private readonly GardenHarvestedHandler _onHarvested;
        private readonly Action<int, int, double> _onGardenSpedUp;
        private readonly Action<int, int> _onGardenLevel;
        private readonly Action<ZoneOpening> _onZone;
        private readonly Action<ZoneDefinition> _onPassage;
        private readonly Action<BiomeOpening> _onBiome;
        private readonly Action<RefugeLevelUp> _onLevel;
        private readonly AlbumRewardHandler _onReward;
        private readonly Func<int, EconomyReason, bool> _sink;
        private bool _disposed;

        public AlbumHooks(AlbumService albums, ActivityServices activities, DailyVisitServices? daily = null,
            ExpeditionService? expeditions = null, GardenService? garden = null)
        {
            _albums = albums;
            _activities = activities;
            _ctx = activities.Context;
            _daily = daily;
            _expeditions = expeditions;
            _garden = garden;
            _progression = _ctx.Progression;

            // Building the albums makes their lots real (before: treats, doc 01 Contenu substitutions).
            activities.Config.AlbumsAvailable = true;
            if (daily != null)
                daily.Config.AlbumsAvailable = true;
            albums.Save ??= _ctx.Save;
            if (albums.State.Seed == 0)
                albums.State.Seed = SeedOf(_ctx.State.Meta.ProfileId);
            albums.SetSubjects(ActivityShipped, SpeciesShipped, ZoneShipped, BiomeShipped);

            _onEnded = OnEnded;
            _onChallenge = OnChallenge;
            _onUnlocked = OnActivityUnlocked;
            _onWish = OnWish;
            _onGift = OnGift;
            _onCalendar = OnCalendar;
            _onDay = OnDay;
            _onWelcomed = OnWelcomed;
            _onHarvested = OnHarvested;
            _onGardenSpedUp = OnGardenSpedUp;
            _onGardenLevel = OnGardenLevel;
            _onZone = OnZone;
            _onPassage = OnPassage;
            _onBiome = OnBiome;
            _onLevel = OnLevel;
            _onReward = OnReward;
            _sink = OnBundleStickers;

            activities.Runner.Ended += _onEnded;
            activities.Runner.ChallengeImproved += _onChallenge;
            activities.ActivityUnlocked += _onUnlocked;
            _ctx.Granter.StickerSink = _sink;
            if (daily != null)
            {
                daily.Wishes.Claimed += _onWish;
                daily.Gift.Opened += _onGift;
                daily.Calendar.Claimed += _onCalendar;
                daily.DayChanged += _onDay;
                daily.Context.Granter.StickerSink = _sink;
            }
            if (expeditions != null)
                expeditions.Welcomed += _onWelcomed;
            if (garden != null)
            {
                garden.Harvested += _onHarvested;
                garden.SpedUp += _onGardenSpedUp;
                garden.LeveledUp += _onGardenLevel;
            }
            if (_progression != null)
            {
                _progression.Zones.Opened += _onZone;
                _progression.Zones.PassageOpened += _onPassage;
                _progression.Gates.Opened += _onBiome;
                _progression.Refuge.LeveledUp += _onLevel;
            }
            albums.RewardEarned += _onReward;
            Refresh();
        }

        /// <summary>A title earned by a complete page or album (title.album.grassland, title.event.101, title.activity.001).</summary>
        public event Action<string>? TitleEarned;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _activities.Runner.Ended -= _onEnded;
            _activities.Runner.ChallengeImproved -= _onChallenge;
            _activities.ActivityUnlocked -= _onUnlocked;
            if (_ctx.Granter.StickerSink == _sink)
                _ctx.Granter.StickerSink = null;
            if (_daily != null)
            {
                _daily.Wishes.Claimed -= _onWish;
                _daily.Gift.Opened -= _onGift;
                _daily.Calendar.Claimed -= _onCalendar;
                _daily.DayChanged -= _onDay;
                if (_daily.Context.Granter.StickerSink == _sink)
                    _daily.Context.Granter.StickerSink = null;
            }
            if (_expeditions != null)
                _expeditions.Welcomed -= _onWelcomed;
            if (_garden != null)
            {
                _garden.Harvested -= _onHarvested;
                _garden.SpedUp -= _onGardenSpedUp;
                _garden.LeveledUp -= _onGardenLevel;
            }
            if (_progression != null)
            {
                _progression.Zones.Opened -= _onZone;
                _progression.Zones.PassageOpened -= _onPassage;
                _progression.Gates.Opened -= _onBiome;
                _progression.Refuge.LeveledUp -= _onLevel;
            }
            _albums.RewardEarned -= _onReward;
        }

        // ------------------------------------------------------------------------------------------- opening and pages

        /// <summary>
        /// Checks the opening (level 12 and day 5), opens the pages earned, gives the opening's progress once, the zones open
        /// and the milestones reached. Called at construction, on a level up and on a new day (cheap: no allocation).
        /// </summary>
        public void Refresh()
        {
            bool open = _ctx.AlbumsOpen;
            _albums.Unlocked = open;
            if (!open)
                return;
            _albums.Draws.NewestBiome = NewestBiome();
            SyncPages();
            if (_albums.RunOpening() || _ctx.State.Inventory.PendingStickers > 0)
            {
                // Stickers won before the albums existed: a sticker of choice each (10 stars).
                int pending = _ctx.State.Inventory.PendingStickers;
                if (pending > 0)
                {
                    _ctx.State.Inventory.PendingStickers = 0;
                    _albums.AddStars(pending * _albums.ChoicePrice);
                }
            }
            SyncZones(-1);
            CheckAllMilestones();
        }

        private void SyncPages()
        {
            var store = _progression?.Store;
            for (int b = 0; b < AlbumData.BiomeAlbums; b++)
                // The Prairie is the first biome: always open.
                if (b == 0 || (store != null && store.IsUnlocked(AlbumData.BiomeId(b))))
                    _albums.OpenBiome(b);
            for (int a = 1; a <= AlbumData.ActivityPages; a++)
                if (_activities.IsAvailable((ActivityId)a))
                    _albums.OpenActivity((ActivityId)a);
            _albums.SetSeason(CurrentSeason());
        }

        /// <summary>The zone stickers of every open zone (of one biome, or all with −1).</summary>
        private void SyncZones(int biome)
        {
            if (_progression == null)
                return;
            var zones = _progression.Config.Zones;
            for (int i = 0; i < zones.Length; i++)
            {
                if (biome >= 0 && AlbumData.BiomeIndex(zones[i].Biome) != biome)
                    continue;
                // The arrival zone is open with its biome (« ouverte d'office »); the others once opened.
                if (zones[i].Role == ZoneRole.Arrival || _progression.Zones.IsOpen(in zones[i]))
                    GrantZone(in zones[i]);
            }
        }

        private void GrantZone(in ZoneDefinition zone)
        {
            int page = AlbumData.BiomePage(AlbumData.BiomeIndex(zone.Biome), 2);
            if (page < 0 || zone.Index < 0 || zone.Index >= AlbumData.StickersPerPage)
                return;
            int sticker = AlbumData.StickerAt(page, zone.Index);
            ref readonly var row = ref AlbumData.Sticker(sticker);
            if (row.Fixed == StickerFixedSource.Zone && row.FixedValue == zone.Index && _albums.IsPageOpen(page))
                _albums.GrantFixed(sticker, false);
        }

        /// <summary>The refuge season of the local date: 1 Éveil (March to May), 2 Soleil, 3 Récolte, 4 Givre (December to February).</summary>
        public int CurrentSeason()
        {
            var zone = _progression?.Config.TimeZone ?? TimeZoneInfo.Local;
            return SeasonOfMonth(TimeZoneInfo.ConvertTime(_ctx.Now, zone).Month);
        }

        public static int SeasonOfMonth(int month) => month >= 3 && month <= 5 ? 1 : month >= 6 && month <= 8 ? 2 : month >= 9 && month <= 11 ? 3 : 4;

        private int NewestBiome()
        {
            var store = _progression?.Store;
            int b = store != null ? AlbumData.BiomeIndex(store.MostAdvancedUnlocked()) : 0;
            return b >= 0 ? b : 0;
        }

        // ------------------------------------------------------------------------------------------- milestones

        private void CheckAllMilestones()
        {
            for (int a = 1; a <= AlbumData.ActivityPages; a++)
                CheckActivityMilestones((ActivityId)a);
            CheckExpeditions();
            CheckGarden();
        }

        /// <summary>A scored activity's 09, 10, 12 (challenge stars) and 11 (new records).</summary>
        private void CheckActivityMilestones(ActivityId activity)
        {
            int page = AlbumData.ActivityPage(activity);
            int slot = ActivityCatalog.SlotOf(activity);
            if (page < 0 || slot < 0 || !_albums.IsPageOpen(page))
                return;
            var stars = _ctx.State.Activities.ChallengeStars;
            int sum = 0, first = slot * ActivityState.ChallengesPerActivity;
            for (int i = 0; i < ActivityState.ChallengesPerActivity && first + i < stars.Length; i++)
                sum += stars[first + i];
            _albums.GrantMilestones(page, StickerFixedSource.ChallengeStars, sum);
            _albums.GrantMilestones(page, StickerFixedSource.Records, _albums.State.Records[(int)activity - 1]);
        }

        private void CheckExpeditions()
        {
            int page = AlbumData.ActivityPage(ActivityId.Expeditions);
            if (!_albums.IsPageOpen(page))
                return;
            var s = _albums.State;
            _albums.GrantMilestones(page, StickerFixedSource.ExpeditionsDone, _ctx.State.Expeditions.Completed);
            _albums.GrantMilestones(page, StickerFixedSource.ExpeditionBiomes, Bits(s.ExpeditionBiomes));
            _albums.GrantMilestones(page, StickerFixedSource.LongExpeditions, s.LongExpeditions);
        }

        private void CheckGarden()
        {
            int page = AlbumData.ActivityPage(ActivityId.Garden);
            if (!_albums.IsPageOpen(page))
                return;
            var s = _albums.State;
            var g = _ctx.State.Garden;
            _albums.GrantMilestones(page, StickerFixedSource.Harvests, g.Harvested);
            _albums.GrantMilestones(page, StickerFixedSource.PlantsHarvested, Bits(s.PlantsHarvested));
            _albums.GrantMilestones(page, StickerFixedSource.DyesObtained, s.DyesObtained);
            _albums.GrantMilestones(page, StickerFixedSource.GardenLevel, GardenRules.LevelOf(g.Points));
        }

        private static int Bits(uint mask)
        {
            int n = 0;
            for (; mask != 0; mask &= mask - 1)
                n++;
            return n;
        }

        // ------------------------------------------------------------------------------------------- sources

        private void OnEnded(in ActivityGameResult result)
        {
            if (result.Mode == ActivityMode.Practice || !result.Rewarded)
            {
                if (result.ChallengeImproved)
                    CheckActivityMilestones(result.Activity);
                return;
            }
            int index = (int)result.Activity - 1;
            var s = _albums.State;
            if (result.NewRecord && (uint)index < AlbumState.ActivityPages && s.Records[index] < byte.MaxValue)
                s.Records[index]++;
            if (!_albums.IsOpen)
                return;
            var outcome = result.Rewards;
            if (outcome.Stickers > 0)
            {
                if (outcome.FixedStickerRule)
                {
                    // A game paid with Gems: no draw, the page's fixed order (the record's sticker too).
                    for (int i = 0; i < outcome.Stickers; i++)
                        _albums.GivePaid(result.Activity);
                }
                else
                {
                    int biome = AlbumData.BiomeIndex(outcome.Biome);
                    int chance = outcome.Stickers;
                    if (outcome.RecordSticker)
                    {
                        _albums.Draw(new StickerDrawRequest(StickerSource.Record, result.Activity, biome));
                        chance--;
                    }
                    for (int i = 0; i < chance; i++)
                        _albums.Draw(new StickerDrawRequest(StickerSource.ActivityGame, result.Activity, biome));
                }
            }
            int page = AlbumData.ActivityPage(result.Activity);
            if (result.Stars >= 3)
                _albums.GrantMilestones(page, StickerFixedSource.ThreeStars, 3);
            CheckActivityMilestones(result.Activity);
        }

        private void OnChallenge(ActivityId activity, int challenge, int stars, int attempts) => CheckActivityMilestones(activity);

        private void OnActivityUnlocked(ActivityId activity)
        {
            if (!_albums.IsOpen)
                return;
            _albums.OpenActivity(activity);
            CheckActivityMilestones(activity);
            if (activity == ActivityId.Expeditions)
                CheckExpeditions();
            else if (activity == ActivityId.Garden)
                CheckGarden();
        }

        private void OnWish(WishClaim claim)
        {
            var s = _albums.State;
            int today = _ctx.TodayKey;
            if (s.WishDayKey != today)
            {
                s.WishDayKey = today;
                s.WishesToday = 0;
            }
            if (s.WishesToday < byte.MaxValue)
                s.WishesToday++;
            if (!_albums.IsOpen || !_albums.Draws.Roll(_albums.Config.WishChance(s.WishesToday)))
                return;
            var request = new StickerDrawRequest(StickerSource.Wish, ActivityId.None, AlbumData.BiomeIndex(claim.Biome))
            {
                Species = claim.Species,
            };
            _albums.Draw(in request);
        }

        private void OnGift(GiftOpening opening) => DrawLots(StickerSource.DailyGift, opening.Granted.Bundle.Stickers);

        private void OnCalendar(CalendarClaim claim) => DrawLots(StickerSource.Calendar, claim.Granted.Bundle.Stickers);

        private void DrawLots(StickerSource source, int count)
        {
            if (!_albums.IsOpen)
                return;
            for (int i = 0; i < count; i++)
                _albums.Draw(new StickerDrawRequest(source));
        }

        private void OnDay(int dayKey) => Refresh();

        private void OnLevel(RefugeLevelUp levelUp) => Refresh();

        private void OnWelcomed(in ExpeditionWelcomeInfo info)
        {
            ref readonly var back = ref info.Return;
            var s = _albums.State;
            if (back.Duration == ExpeditionDuration.TwelveHours)
                s.LongExpeditions++;
            int biome = -1;
            bool eventDestination = false;
            if (ExpeditionCatalog.TryGet(back.Destination, out var destination))
            {
                biome = AlbumData.BiomeIndex(destination.Destination.Biome);
                eventDestination = destination.EventNumber > 0;
                if (biome >= 0)
                    s.ExpeditionBiomes |= (ushort)(1 << biome);
            }
            if (_albums.IsOpen)
            {
                // The chance was drawn at departure (a speed-up never changes it); the page and sticker are chosen now.
                for (int i = 0; i < back.Loot.Stickers; i++)
                {
                    var request = new StickerDrawRequest(StickerSource.Expedition, ActivityId.Expeditions, biome)
                    {
                        EventDestination = eventDestination,
                    };
                    _albums.Draw(in request);
                }
            }
            CheckExpeditions();
        }

        private void OnHarvested(in GardenHarvestInfo info)
        {
            ref readonly var harvest = ref info.Harvest;
            var s = _albums.State;
            int plant = GardenCatalog.IndexOf(harvest.Plant);
            if (plant >= 0 && plant < 32)
                s.PlantsHarvested |= 1u << plant;
            if (harvest.Dyes > 0)
                s.DyesObtained += harvest.Dyes;
            int bit = harvest.Plot >= 0 && harvest.Plot < 8 ? 1 << harvest.Plot : 0;
            bool paid = (s.GardenSpedUp & bit) != 0;
            s.GardenSpedUp = (byte)(s.GardenSpedUp & ~bit);
            if (_albums.IsOpen)
            {
                double chance = _albums.Config.GardenChance(harvest.Stars);
                if (paid)
                {
                    // A sprout finished with Gems: the fixed counter (5, 10, 15 points), a sticker at 100, never a draw.
                    const int slot = (int)ActivityId.Garden - 1;
                    int points = s.PaidPoints[slot] + (int)Math.Round(chance * 100.0, MidpointRounding.AwayFromZero);
                    int won = points / ActivityConfig.PaidStickerThreshold;
                    s.PaidPoints[slot] = (byte)(points - won * ActivityConfig.PaidStickerThreshold);
                    for (int i = 0; i < won; i++)
                        _albums.GivePaid(ActivityId.Garden);
                }
                else if (_albums.Draws.Roll(chance))
                {
                    _albums.Draw(new StickerDrawRequest(StickerSource.GardenHarvest, ActivityId.Garden, NewestBiome()));
                }
            }
            CheckGarden();
        }

        private void OnGardenSpedUp(int plot, int gems, double seconds)
        {
            if (gems > 0 && plot >= 0 && plot < 8)
                _albums.State.GardenSpedUp |= (byte)(1 << plot);
        }

        private void OnGardenLevel(int before, int after) => CheckGarden();

        private void OnZone(ZoneOpening opening)
        {
            if (_albums.IsOpen)
                GrantZone(opening.Zone);
        }

        private void OnPassage(ZoneDefinition zone)
        {
            if (_albums.IsOpen)
                GrantZone(in zone);
        }

        private void OnBiome(BiomeOpening opening)
        {
            if (!_albums.IsOpen)
                return;
            int biome = AlbumData.BiomeIndex(opening.Biome);
            _albums.Draws.NewestBiome = NewestBiome();
            _albums.OpenBiome(biome);
            SyncZones(biome);
        }

        // ------------------------------------------------------------------------------------------- rewards and lots

        private void OnReward(in AlbumReward reward)
        {
            if (reward.Gems > 0)
                _ctx.Economy.Grant(CurrencyId.Gems, BigValue.FromDouble(reward.Gems), EconomyReason.Reward);
            if (reward.Tickets > 0)
                _activities.Tickets.Grant(reward.Tickets, TicketSource.Album);
            if (reward.Decoration.IsValid)
                _ctx.State.Inventory.Add(reward.Decoration, 1);
            if (reward.Landmark.IsValid)
                _ctx.State.Inventory.Add(reward.Landmark, 1);
            if (!string.IsNullOrEmpty(reward.Title))
                TitleEarned?.Invoke(reward.Title);
            _ctx.RequestSave(SaveUrgency.Priority);
        }

        /// <summary>
        /// A reward bundle's stickers: the activity games, the daily gift, the calendar and the expeditions are drawn by their
        /// hooks above (true: not kept); any other lot (a return gift's « autocollant au choix ») becomes a sticker of choice
        /// (10 stars each). Closed albums: false, they wait in the inventory as before.
        /// </summary>
        private bool OnBundleStickers(int count, EconomyReason reason)
        {
            if (!_albums.IsOpen)
                return false;
            switch (reason)
            {
                case EconomyReason.Activity:
                case EconomyReason.DailyGift:
                case EconomyReason.Calendar:
                case EconomyReason.Expedition:
                    return true;
                default:
                    _albums.AddStars(count * _albums.ChoicePrice);
                    return true;
            }
        }

        // ------------------------------------------------------------------------------------------- subjects

        private static bool ActivityShipped(ActivityId activity) =>
            ActivityCatalog.SlotOf(activity) >= 0 && ActivityCatalog.Get(activity).ShippedInThisBuild;

        private bool SpeciesShipped(ContentId species) => _ctx.Content.Species.TryGet(species, out _);

        private bool ZoneShipped(int biome, int index)
        {
            if (_progression == null)
                return true;
            var zones = _progression.Config.Zones;
            var id = AlbumData.BiomeId(biome);
            for (int i = 0; i < zones.Length; i++)
                if (zones[i].Biome == id && zones[i].Index == index)
                    return true;
            return false;
        }

        private bool BiomeShipped(int biome)
        {
            if (_progression == null)
                return biome == 0;
            var zones = _progression.Config.Zones;
            var id = AlbumData.BiomeId(biome);
            for (int i = 0; i < zones.Length; i++)
                if (zones[i].Biome == id)
                    return true;
            return false;
        }

        /// <summary>The account's generator seed from its profile id (never 0).</summary>
        public static ulong SeedOf(Guid profile)
        {
            var bytes = profile.ToByteArray();
            ulong seed = 0xCBF29CE484222325UL;
            for (int i = 0; i < bytes.Length; i++)
                seed = (seed ^ bytes[i]) * 0x100000001B3UL;
            return seed == 0 ? 1UL : seed;
        }
    }
}
