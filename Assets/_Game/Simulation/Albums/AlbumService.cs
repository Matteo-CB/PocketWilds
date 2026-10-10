#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    public enum AlbumRewardKind : byte
    {
        PageHalf = 1,
        PageComplete = 2,
        AlbumComplete = 3,
    }

    /// <summary>
    /// A page or album reward reached (section 1.8), raised once. <see cref="AlbumHooks"/> gives the Gems, tickets (they may
    /// go past the ticket maximum), decorations and monuments; titles go to the titles system (T44B) by their key.
    /// </summary>
    public readonly struct AlbumReward
    {
        public readonly AlbumRewardKind Kind;

        /// <summary>The page (−1 for an album reward).</summary>
        public readonly int Page;

        public readonly int Album;
        public readonly AlbumKind AlbumKind;
        public readonly int Gems;
        public readonly int Tickets;

        /// <summary>A biome's progression decoration 13 to 16 or a season decoration (None without).</summary>
        public readonly ContentId Decoration;

        /// <summary>An album's monument (E-LMK-ALB-*; None without).</summary>
        public readonly ContentId Landmark;

        /// <summary>The title's key (title.album.grassland, title.event.101, title.activity.001), empty without.</summary>
        public readonly string Title;

        public AlbumReward(AlbumRewardKind kind, int page, int album, AlbumKind albumKind, int gems, int tickets, ContentId decoration,
            ContentId landmark, string title)
        {
            Kind = kind;
            Page = page;
            Album = album;
            AlbumKind = albumKind;
            Gems = gems;
            Tickets = tickets;
            Decoration = decoration;
            Landmark = landmark;
            Title = title;
        }
    }

    /// <summary>A sticker given (analytics album_sticker_gained: id, source, new or duplicate, rarity, page).</summary>
    public readonly struct AlbumGainInfo
    {
        public readonly int Sticker;
        public readonly StickerSource Source;
        public readonly bool New;
        public readonly StickerRarity Rarity;
        public readonly int Page;
        public readonly int StarsAdded;

        public AlbumGainInfo(int sticker, StickerSource source, bool isNew, StickerRarity rarity, int page, int starsAdded)
        {
            Sticker = sticker;
            Source = source;
            New = isNew;
            Rarity = rarity;
            Page = page;
            StarsAdded = starsAdded;
        }
    }

    public delegate void AlbumRewardHandler(in AlbumReward reward);

    public delegate void AlbumGainHandler(in AlbumGainInfo info);

    /// <summary>
    /// T44A « Albums » (doc 01 Albums et autocollants, sections 1 to 5): the 16 albums, 60 pages and 720 stickers
    /// (<see cref="AlbumData"/>), the draws and their guarantees (<see cref="StickerDrawService"/>), duplicates to album
    /// stars (the Albums Weekend doubles them), the sticker of choice for 10 stars, page and album rewards given once, the
    /// fixed stickers (zones, companions, milestones, event track, visitors, signature moments), the reveal queue and the
    /// availability of each sticker (its art captured and its subject in this build).
    /// <list type="bullet">
    /// <item>Open: refuge level 12 and day 5 (<see cref="Unlocked"/>, kept by <see cref="AlbumHooks"/>); before, no source
    /// gives a sticker. <see cref="RunOpening"/> gives the opening's progress once.</item>
    /// <item>Reads never allocate (pages and stickers cached at construction); a draw or a gain never allocates.</item>
    /// </list>
    /// Testable without a scene: the systems are wired by <see cref="AlbumHooks"/>.
    /// </summary>
    public sealed class AlbumService : IAlbums
    {
        public const int PendingCapacity = 128;

        private static readonly string[] PageTitles = BuildPageTitles();
        private static readonly string[] AlbumTitles = BuildAlbumTitles();
        private static readonly ContentId[] PageDecorationsHalf = BuildDecorations(false);
        private static readonly ContentId[] PageDecorationsComplete = BuildDecorations(true);
        private static readonly ContentId[] Landmarks = BuildLandmarks();

        private readonly AlbumState _state;
        private readonly bool[] _available = new bool[AlbumState.Stickers];
        private readonly bool[] _art = new bool[AlbumState.Stickers];
        private readonly byte[] _ownedOnPage = new byte[AlbumState.Pages];
        private readonly StickerInfo[] _stickers = new StickerInfo[AlbumState.Stickers];
        private readonly AlbumPageInfo[] _pages = new AlbumPageInfo[AlbumState.Pages];
        private readonly StickerGain[] _pending = new StickerGain[PendingCapacity];
        private int _pendingStart;
        private int _pendingCount;
        private bool _artKnown;

        private Func<ActivityId, bool>? _activityShipped;
        private Func<ContentId, bool>? _speciesShipped;
        private Func<int, int, bool>? _zoneShipped;
        private Func<int, bool>? _biomeShipped;

        public AlbumService(AlbumState state, AlbumConfig? config = null)
        {
            _state = state;
            Config = config ?? new AlbumConfig();
            Draws = new StickerDrawService(state, Config, _available);
            for (int p = 0; p < AlbumState.Pages; p++)
            {
                ref readonly var row = ref AlbumData.Page(p);
                _pages[p] = new AlbumPageInfo(p, row.Key, row.Album, row.Kind, p * AlbumData.StickersPerPage);
            }
            for (int i = 0; i < AlbumState.Stickers; i++)
            {
                ref readonly var row = ref AlbumData.Sticker(i);
                _stickers[i] = new StickerInfo(i, row.Key, row.Page, row.Slot, row.Rarity, row.Drawable, row.Hint);
            }
            RefreshAvailability();
            Recount();
        }

        public AlbumState State => _state;
        public AlbumConfig Config { get; }
        public StickerDrawService Draws { get; }
        public ISaveScheduler? Save { get; set; }

        /// <summary>Refuge level 12 and day 5 reached (doc 02 Améliorations, section 6.4); kept by <see cref="AlbumHooks"/>.</summary>
        public bool Unlocked { get; set; }

        public event Action? Gained;

        /// <summary>Every sticker given, with its source (analytics album_sticker_gained).</summary>
        public event AlbumGainHandler? StickerGained;

        public event AlbumRewardHandler? RewardEarned;

        /// <summary>Stars spent on a sticker of choice: the sticker (analytics album_star_spent).</summary>
        public event Action<int>? StarsSpent;

        // ------------------------------------------------------------------------------------------- IAlbums reads

        public bool IsOpen => Config.Enabled && Unlocked;
        public int AlbumCount => AlbumData.AlbumCount;
        public AlbumKind KindOfAlbum(int album) => AlbumData.Album(album).Kind;
        public string AlbumKey(int album) => AlbumData.Album(album).Key;
        public int PageCount => AlbumData.PageCount;
        public AlbumPageInfo PageAt(int page) => _pages[page];
        public bool IsPageOpen(int page) => (uint)page < AlbumState.Pages && _state.IsPageOpen(page);
        public int StickerCount => AlbumData.StickerCount;
        public StickerInfo StickerAt(int sticker) => _stickers[sticker];
        public bool Owned(int sticker) => (uint)sticker < AlbumState.Stickers && _state.IsOwned(sticker);
        public bool Unseen(int sticker) => (uint)sticker < AlbumState.Stickers && AlbumState.Bit(_state.Unseen, sticker);
        public bool Available(int sticker) => (uint)sticker < AlbumState.Stickers && _available[sticker];
        public int OwnedOnPage(int page) => (uint)page < AlbumState.Pages ? _ownedOnPage[page] : 0;
        public int AlbumStars => _state.Stars;
        public int ChoicePrice => Math.Max(1, Config.ChoicePrice);
        public int PendingCount => _pendingCount;
        public StickerGain PendingAt(int index) => _pending[(_pendingStart + index) % PendingCapacity];

        public void ClearPending()
        {
            _pendingStart = 0;
            _pendingCount = 0;
        }

        public void MarkPageSeen(int page)
        {
            if ((uint)page >= AlbumState.Pages)
                return;
            bool changed = false;
            int first = page * AlbumData.StickersPerPage;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
            {
                if (!AlbumState.Bit(_state.Unseen, first + i))
                    continue;
                AlbumState.SetBit(_state.Unseen, first + i, false);
                changed = true;
            }
            if (changed)
                Save?.RequestSave(SaveUrgency.Normal);
        }

        /// <summary>Owned stickers in an album (its progress bar on the shelf).</summary>
        public int OwnedInAlbum(int album)
        {
            ref readonly var row = ref AlbumData.Album(album);
            int n = 0;
            for (int p = row.FirstPage; p < row.FirstPage + row.Pages; p++)
                n += _ownedOnPage[p];
            return n;
        }

        // ------------------------------------------------------------------------------------------- availability

        /// <summary>
        /// The stickers whose art was captured (the bootstrap passes <c>StickerArtCatalog</c>'s keys). Unknown keys are
        /// ignored. Until it is called every sticker counts as having its art (tests, tools).
        /// </summary>
        public void SetAvailableArt(string[] stickerKeys)
        {
            Array.Clear(_art, 0, _art.Length);
            if (stickerKeys != null)
            {
                for (int i = 0; i < stickerKeys.Length; i++)
                {
                    int index = AlbumData.IndexOf(stickerKeys[i]);
                    if (index >= 0)
                        _art[index] = true;
                }
            }
            _artKnown = true;
            RefreshAvailability();
        }

        /// <summary>
        /// The subject rules (null: every subject exists): an activity page needs its activity shipped; a biome page its biome
        /// built (zones in this build); a zone sticker its zone; a wish sticker (01 to 06) its species.
        /// </summary>
        public void SetSubjects(Func<ActivityId, bool>? activityShipped, Func<ContentId, bool>? speciesShipped,
            Func<int, int, bool>? zoneShipped, Func<int, bool>? biomeShipped)
        {
            _activityShipped = activityShipped;
            _speciesShipped = speciesShipped;
            _zoneShipped = zoneShipped;
            _biomeShipped = biomeShipped;
            RefreshAvailability();
        }

        public void RefreshAvailability()
        {
            for (int i = 0; i < AlbumState.Stickers; i++)
                _available[i] = (!_artKnown || _art[i]) && SubjectExists(i);
        }

        private bool SubjectExists(int sticker)
        {
            ref readonly var row = ref AlbumData.Sticker(sticker);
            ref readonly var page = ref AlbumData.Page(row.Page);
            switch (page.Kind)
            {
                case AlbumKind.Activity:
                    return _activityShipped == null || _activityShipped(AlbumData.ActivityOfPage(row.Page));
                case AlbumKind.Biome:
                {
                    int biome = AlbumData.BiomeOfPage(row.Page);
                    if (_biomeShipped != null && !_biomeShipped(biome))
                        return false;
                    if (row.Fixed == StickerFixedSource.Zone && _zoneShipped != null && !_zoneShipped(biome, row.FixedValue))
                        return false;
                    var species = AlbumData.PreferredSpecies(sticker);
                    return !species.IsValid || _speciesShipped == null || _speciesShipped(species);
                }
                default:
                    return true;
            }
        }

        // ------------------------------------------------------------------------------------------- pages and opening

        /// <summary>Opens a page (never closed again). Only once the albums are open: before, the opening opens them all at once.</summary>
        public bool OpenPage(int page)
        {
            if (!IsOpen || (uint)page >= AlbumState.Pages || _state.IsPageOpen(page))
                return false;
            _state.PagesOpen |= 1UL << page;
            Save?.RequestSave(SaveUrgency.Normal);
            return true;
        }

        /// <summary>A biome's album opens with the biome: its two pages, the companion in silhouette (07) owned from now on.</summary>
        public void OpenBiome(int biome)
        {
            int p1 = AlbumData.BiomePage(biome, 1);
            if (p1 < 0 || !IsOpen)
                return;
            OpenPage(p1);
            OpenPage(p1 + 1);
            GrantFixed(AlbumData.StickerAt(p1, 6), false);
        }

        public void OpenActivity(ActivityId activity) => OpenPage(AlbumData.ActivityPage(activity));

        /// <summary>An event's page opens at its announcement (2 days before it starts; the event systems call it).</summary>
        public void OpenEventPage(int eventNumber) => OpenPage(AlbumData.EventPage(eventNumber));

        /// <summary>The refuge's season (1 Éveil, 2 Soleil, 3 Récolte, 4 Givre): its page opens on its first day lived.</summary>
        public void SetSeason(int season)
        {
            Draws.Season = season >= 1 && season <= 4 ? season : 0;
            OpenPage(AlbumData.SeasonPage(season));
        }

        /// <summary>
        /// The albums' opening, once (state <see cref="AlbumState.Opened"/>): STK-GL-01 Lapin curieux offered. The hooks open
        /// the pages already earned first (their companions come with them) and give the zones already open.
        /// </summary>
        public bool RunOpening()
        {
            if (!IsOpen || _state.Opened)
                return false;
            _state.Opened = true;
            Give(AlbumData.StickerAt(0, 0), StickerSource.Opening, false);
            Save?.RequestSave(SaveUrgency.Priority);
            return true;
        }

        // ------------------------------------------------------------------------------------------- sources

        /// <summary>One draw of a source (section 1.5): the sticker given (a duplicate becomes stars); −1 and 1 star when no page can give one.</summary>
        public int Draw(in StickerDrawRequest request)
        {
            if (!IsOpen)
                return -1;
            if (!Draws.Draw(in request, out var draw))
            {
                AddStars(1);
                return -1;
            }
            Give(draw.Sticker, request.Source, true);
            return draw.Sticker;
        }

        /// <summary>
        /// A sticker of a game, moves or a speed-up paid with Gems (section 1.4): the next missing drawable sticker of the
        /// activity page in the fixed order, never a draw nor a duplicate; 1 album star when none is left. Returns the sticker or −1.
        /// </summary>
        public int GivePaid(ActivityId activity)
        {
            if (!IsOpen)
                return -1;
            int sticker = Draws.NextPaidSticker(activity);
            if (sticker < 0)
            {
                AddStars(1);
                return -1;
            }
            Give(sticker, StickerSource.PaidOrder, false);
            return sticker;
        }

        /// <summary>The sticker the next paid game would give (« Prochain autocollant : {nom} »), −1 when none is left.</summary>
        public int NextPaidSticker(ActivityId activity) => Draws.NextPaidSticker(activity);

        /// <summary>
        /// A fixed sticker given by its condition. Already owned: nothing, or with <paramref name="duplicateToStars"/> (an
        /// event's sticker given again the next year) its stars. False when nothing was given.
        /// </summary>
        public bool GrantFixed(int sticker, bool duplicateToStars)
        {
            if (!IsOpen || (uint)sticker >= AlbumState.Stickers)
                return false;
            if (_state.IsOwned(sticker) && !duplicateToStars)
                return false;
            Give(sticker, StickerSource.Fixed, false);
            return true;
        }

        /// <summary>
        /// Every fixed sticker of a page with this condition reached (value at least the sticker's; a ribbon exactly):
        /// activity milestones, event track tiers, event challenge stars, long quests. Returns the stickers given.
        /// </summary>
        public int GrantMilestones(int page, StickerFixedSource kind, int value, bool duplicateToStars = false)
        {
            if (!IsOpen || (uint)page >= AlbumState.Pages || !_state.IsPageOpen(page))
                return 0;
            int given = 0, first = page * AlbumData.StickersPerPage;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
            {
                ref readonly var row = ref AlbumData.Sticker(first + i);
                if (row.Fixed != kind)
                    continue;
                bool reached = kind == StickerFixedSource.ContestRibbon ? value == row.FixedValue : value >= row.FixedValue;
                if (reached && GrantFixed(first + i, duplicateToStars))
                    given++;
            }
            return given;
        }

        /// <summary>The first signature moment seen in a biome: its sticker 24, guaranteed once.</summary>
        public bool OnSignatureMomentSeen(int biome)
        {
            if (!IsOpen || (uint)biome >= AlbumState.Biomes || (_state.SignatureSeen & (1 << biome)) != 0)
                return false;
            int page = AlbumData.BiomePage(biome, 2);
            if (!_state.IsPageOpen(page))
                return false;
            _state.SignatureSeen |= (ushort)(1 << biome);
            GrantFixed(AlbumData.StickerAt(page, 11), false);
            Save?.RequestSave(SaveUrgency.Priority);
            return true;
        }

        /// <summary>A world event touched in a biome with the chance of its sheet (10 or 25 %; the sheets' cap is theirs).</summary>
        public int OnWorldEventTouched(int biome, double chance)
        {
            if (!IsOpen || !Draws.Roll(chance))
                return -1;
            return Draw(new StickerDrawRequest(StickerSource.WorldEvent, ActivityId.None, biome));
        }

        /// <summary>A season's rare visitor touched for the first time (T44C): its sticker (01 to 03 of its season's page).</summary>
        public bool OnVisitorFirstTouch(int season, int visitor)
        {
            int page = AlbumData.SeasonPage(season);
            if (page < 0 || visitor < 1 || visitor > 3)
                return false;
            if (!_state.IsPageOpen(page))
                OpenPage(page);
            return GrantMilestones(page, StickerFixedSource.Visitor, visitor) > 0;
        }

        /// <summary>Event free track tier reached (1, 6, 11, 19, 23, 26): its sticker; again next year, its stars.</summary>
        public int OnEventTrack(int eventNumber, int tier) => EventFixed(eventNumber, StickerFixedSource.EventTrack, tier);

        public int OnEventChallengeStars(int eventNumber, int stars) => EventFixed(eventNumber, StickerFixedSource.EventChallengeStars, stars);

        /// <summary>A long quest done at position 4, 7 or 10.</summary>
        public int OnEventQuest(int eventNumber, int position)
        {
            int page = AlbumData.EventPage(eventNumber);
            if (page < 0 || !IsOpen)
                return 0;
            int first = page * AlbumData.StickersPerPage;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
            {
                ref readonly var row = ref AlbumData.Sticker(first + i);
                if (row.Fixed == StickerFixedSource.EventQuest && row.FixedValue == position)
                    return GrantFixed(first + i, true) ? 1 : 0;
            }
            return 0;
        }

        private int EventFixed(int eventNumber, StickerFixedSource kind, int value)
        {
            int page = AlbumData.EventPage(eventNumber);
            if (page < 0 || !IsOpen)
                return 0;
            int given = 0, first = page * AlbumData.StickersPerPage;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
            {
                ref readonly var row = ref AlbumData.Sticker(first + i);
                // A tier passed again in a later year gives the duplicate once: only the tier just reached.
                if (row.Fixed == kind && row.FixedValue == value && GrantFixed(first + i, true))
                    given++;
            }
            return given;
        }

        /// <summary>
        /// The event shop (section 1.4): a missing sticker of the running event's page, of choice, for the event currency the
        /// event systems take. False when it is not a missing sticker of that page.
        /// </summary>
        public bool GrantFromEventShop(int sticker, int runningEvent)
        {
            int page = AlbumData.EventPage(runningEvent);
            if (!IsOpen || page < 0 || (uint)sticker >= AlbumState.Stickers || AlbumData.Sticker(sticker).Page != page
                || _state.IsOwned(sticker))
                return false;
            Give(sticker, StickerSource.EventShop, false);
            return true;
        }

        /// <summary>A missing sticker of choice given by the return gift after 14 days (the same list as the stars' choice).</summary>
        public bool GrantReturnChoice(int sticker)
        {
            if (!CanChoose(sticker))
                return false;
            Give(sticker, StickerSource.ReturnGift, false);
            return true;
        }

        /// <summary>A missing, available sticker of an open page (the stars' choice list).</summary>
        public bool CanChoose(int sticker) =>
            IsOpen && (uint)sticker < AlbumState.Stickers && !_state.IsOwned(sticker) && _available[sticker]
            && _state.IsPageOpen(AlbumData.Sticker(sticker).Page);

        public bool TryChoose(int sticker)
        {
            int price = ChoicePrice;
            if (!CanChoose(sticker) || _state.Stars < price)
                return false;
            _state.Stars -= price;
            Give(sticker, StickerSource.Choice, false);
            StarsSpent?.Invoke(sticker);
            return true;
        }

        /// <summary>Album stars added outside a duplicate (no sticker left for a paid game, stickers won before the albums: 10 each).</summary>
        public void AddStars(int stars)
        {
            if (stars <= 0)
                return;
            _state.Stars = (int)Math.Min(int.MaxValue, (long)_state.Stars + stars);
            Save?.RequestSave(SaveUrgency.Normal);
        }

        // ------------------------------------------------------------------------------------------- giving

        private void Give(int sticker, StickerSource source, bool drawn)
        {
            ref readonly var row = ref AlbumData.Sticker(sticker);
            int page = row.Page;
            bool isNew = !_state.IsOwned(sticker);
            int stars = 0;
            bool half = false, complete = false, albumDone = false;
            if (isNew)
            {
                AlbumState.SetBit(_state.Owned, sticker, true);
                AlbumState.SetBit(_state.Unseen, sticker, true);
                int owned = ++_ownedOnPage[page];
                int bit = page * 2;
                if (owned >= AlbumData.StickersPerPage / 2 && !AlbumState.Bit(_state.PageRewards, bit))
                {
                    AlbumState.SetBit(_state.PageRewards, bit, true);
                    half = true;
                    RaisePageReward(page, AlbumRewardKind.PageHalf);
                }
                if (owned >= AlbumData.StickersPerPage && !AlbumState.Bit(_state.PageRewards, bit + 1))
                {
                    AlbumState.SetBit(_state.PageRewards, bit + 1, true);
                    complete = true;
                    RaisePageReward(page, AlbumRewardKind.PageComplete);
                    albumDone = CheckAlbum(AlbumData.Page(page).Album);
                }
            }
            else
            {
                stars = Config.DuplicateStars(row.Rarity);
                _state.Stars = (int)Math.Min(int.MaxValue, (long)_state.Stars + stars);
            }

            var gain = new StickerGain(sticker, isNew, stars, half, complete, albumDone);
            if (_pendingCount == PendingCapacity)
            {
                _pendingStart = (_pendingStart + 1) % PendingCapacity;
                _pendingCount--;
            }
            _pending[(_pendingStart + _pendingCount) % PendingCapacity] = gain;
            _pendingCount++;

            var info = new AlbumGainInfo(sticker, source, isNew, row.Rarity, page, stars);
            StickerGained?.Invoke(in info);
            Gained?.Invoke();
            Save?.RequestSave(SaveUrgency.Priority);
        }

        private bool CheckAlbum(int album)
        {
            if ((_state.AlbumRewards & (1 << album)) != 0)
                return false;
            ref readonly var row = ref AlbumData.Album(album);
            for (int p = row.FirstPage; p < row.FirstPage + row.Pages; p++)
                if (_ownedOnPage[p] < AlbumData.StickersPerPage)
                    return false;
            _state.AlbumRewards |= (ushort)(1 << album);
            var reward = new AlbumReward(AlbumRewardKind.AlbumComplete, -1, album, row.Kind, 0, 0, ContentId.None, Landmarks[album],
                AlbumTitles[album]);
            RewardEarned?.Invoke(in reward);
            return true;
        }

        private void RaisePageReward(int page, AlbumRewardKind kind)
        {
            ref readonly var row = ref AlbumData.Page(page);
            bool complete = kind == AlbumRewardKind.PageComplete;
            int gems = complete ? Math.Max(0, Config.PageCompleteGems) : 0;
            int tickets = !complete && row.Kind != AlbumKind.Biome ? Math.Max(0, Config.PageHalfTickets) : 0;
            var decoration = complete ? PageDecorationsComplete[page] : PageDecorationsHalf[page];
            string title = complete ? PageTitles[page] : string.Empty;
            var reward = new AlbumReward(kind, page, row.Album, row.Kind, gems, tickets, decoration, ContentId.None, title);
            RewardEarned?.Invoke(in reward);
        }

        /// <summary>Recounts the owned stickers per page (after a load).</summary>
        public void Recount()
        {
            for (int p = 0; p < AlbumState.Pages; p++)
            {
                int n = 0, first = p * AlbumData.StickersPerPage;
                for (int i = 0; i < AlbumData.StickersPerPage; i++)
                    if (_state.IsOwned(first + i))
                        n++;
                _ownedOnPage[p] = (byte)n;
            }
        }

        // ------------------------------------------------------------------------------------------- reward ids

        /// <summary>
        /// The biome pages' decorations: E-DEC-{XX}-13 (page 1 half), 14 (page 1 complete), 15 (page 2 half), 16 (page 2
        /// complete) as decor.album.{biome}.{13..16}; a season page's E-DEC-SEA-0n as decor.album.season.{n}.
        /// </summary>
        public static string? DecorationSource(int page, bool complete)
        {
            ref readonly var row = ref AlbumData.Page(page);
            if (row.Kind == AlbumKind.Biome)
            {
                int n = (row.Number == 1 ? 13 : 15) + (complete ? 1 : 0);
                return "decor.album." + row.Subject.Substring("biome.".Length) + "." + n;
            }
            if (row.Kind == AlbumKind.Season && complete)
                return "decor.album.season." + row.Number;
            return null;
        }

        private static ContentId[] BuildDecorations(bool complete)
        {
            var ids = new ContentId[AlbumState.Pages];
            for (int p = 0; p < ids.Length; p++)
            {
                string? source = DecorationSource(p, complete);
                ids[p] = source != null ? ContentId.FromSource(source) : ContentId.None;
            }
            return ids;
        }

        private static string[] BuildPageTitles()
        {
            var titles = new string[AlbumState.Pages];
            for (int p = 0; p < titles.Length; p++)
            {
                ref readonly var row = ref AlbumData.Page(p);
                titles[p] = row.Kind == AlbumKind.Event ? "title.event." + row.Number
                    : row.Kind == AlbumKind.Activity ? "title.activity." + row.Number.ToString("000")
                    : string.Empty;
            }
            return titles;
        }

        private static string[] BuildAlbumTitles()
        {
            var titles = new string[AlbumState.Albums];
            for (int a = 0; a < titles.Length; a++)
            {
                ref readonly var row = ref AlbumData.Album(a);
                titles[a] = row.Kind == AlbumKind.Biome ? "title." + row.Key : string.Empty;
            }
            return titles;
        }

        /// <summary>E-LMK-ALB-{XX} for a biome album, E-LMK-ALB-EVT, -SEA, -ACT: landmark.album.{biome|event|season|activity}.</summary>
        private static ContentId[] BuildLandmarks()
        {
            var ids = new ContentId[AlbumState.Albums];
            for (int a = 0; a < ids.Length; a++)
                ids[a] = ContentId.FromSource("landmark." + AlbumData.Album(a).Key);
            return ids;
        }
    }
}
