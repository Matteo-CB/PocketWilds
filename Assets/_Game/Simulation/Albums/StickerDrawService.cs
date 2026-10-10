#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>Where a sticker comes from (section 1.4; analytics album_sticker_gained « source »). Stable values.</summary>
    public enum StickerSource : byte
    {
        None = 0,

        // ---- Draws (StickerDrawService) ----
        ActivityGame = 1,
        Record = 2,
        CuddleLevel = 3,
        CuddleGuarantee = 4,
        GardenHarvest = 5,
        Wish = 6,
        Expedition = 7,
        DailyGift = 8,
        Calendar = 9,
        WorldEvent = 10,
        Contest = 11,

        // ---- No draw ----
        /// <summary>A game, moves or a speed-up paid with Gems: the next missing drawable sticker of the activity page.</summary>
        PaidOrder = 20,

        /// <summary>A fixed condition (zone, companion, milestone, event track, visitor, signature moment).</summary>
        Fixed = 21,

        /// <summary>10 album stars, a missing sticker of choice.</summary>
        Choice = 22,

        EventShop = 23,
        ReturnGift = 24,

        /// <summary>The albums' opening (STK-GL-01 and the progress already made).</summary>
        Opening = 25,
    }

    /// <summary>A draw asked by a source: the page table of the source and what its preferred source matches against.</summary>
    public struct StickerDrawRequest
    {
        public StickerSource Source;

        /// <summary>The activity played (activity games, records, the Garden, the Hug Match, the Grand Contest).</summary>
        public ActivityId Activity;

        /// <summary>The biome (0 to 12): the active biome of a game, the wishing animal's, the destination's, the world event's; −1 none.</summary>
        public int Biome;

        /// <summary>The species of a wish (its preferred stickers weigh 3).</summary>
        public ContentId Species;

        /// <summary>An expedition to an event destination (the least complete page 75 %, the Expeditions page 25 %).</summary>
        public bool EventDestination;

        public StickerDrawRequest(StickerSource source, ActivityId activity = ActivityId.None, int biome = -1)
        {
            Source = source;
            Activity = activity;
            Biome = biome;
            Species = ContentId.None;
            EventDestination = false;
        }
    }

    /// <summary>The draw's result.</summary>
    public struct StickerDraw
    {
        /// <summary>The sticker drawn, −1 when no open page has a drawable sticker (the caller gives 1 album star instead).</summary>
        public int Sticker;

        public int Page;

        /// <summary>The 15 % season roll chose the season page.</summary>
        public bool SeasonDraw;

        public bool NewnessGuarantee;
        public bool ShinyGuarantee;
    }

    /// <summary>
    /// The simulated world a draw's preferred source looks at (doc 04 Heure, météo, saisons: weather and light are simulated
    /// states, the same on the three quality tiers; a mist not drawn in Fluide still counts). Null: nothing matches.
    /// </summary>
    public interface IAlbumWorld
    {
        /// <summary>The weather or ambience <paramref name="weatherKey"/> (light_rain, rainbow…) is active in the biome (0 to 12).</summary>
        bool WeatherActive(int biome, string weatherKey);

        /// <summary>The morning or evening light of the biome (−1: the refuge's current biome).</summary>
        bool Morning(int biome);

        bool Evening(int biome);

        bool SignatureMomentActive(int biome);

        /// <summary>The season weather <paramref name="weatherKey"/> (small_rain, frost…) is active.</summary>
        bool SeasonWeatherActive(string weatherKey);
    }

    /// <summary>
    /// T44A: the draws of the albums (doc 01 Albums, sections 1.4 to 1.6), testable without a scene: the season roll, the
    /// source's pages and shares, the exclusions (closed pages, pages without a drawable sticker, complete pages unless all
    /// are) and the prorata; the rarity weights 70/25/5 among the rarities the page holds; the preferred source (weight 3);
    /// the « missing » sources (daily gift, calendar, record); the newness guarantee (after 3 duplicates) and the Shiny
    /// guarantee (30th draw of a page); the fixed order of paid games. A deterministic generator per account (seed saved in
    /// <see cref="AlbumState.Seed"/>) so the server and tests replay a result. Only drawable and available stickers are ever
    /// drawn. Buffers allocated once: a draw never allocates.
    /// </summary>
    public sealed class StickerDrawService
    {
        private readonly AlbumState _state;
        private readonly AlbumConfig _config;
        private readonly bool[] _available;
        private readonly int[] _pages = new int[AlbumState.Pages];
        private readonly double[] _shares = new double[AlbumState.Pages];
        private readonly int[] _pool = new int[AlbumData.StickersPerPage];
        private readonly double[] _weights = new double[AlbumData.StickersPerPage];

        public StickerDrawService(AlbumState state, AlbumConfig config, bool[] available)
        {
            _state = state;
            _config = config;
            _available = available;
        }

        public IAlbumWorld? World { get; set; }

        /// <summary>The refuge's season (1 Éveil to 4 Givre; 0: none known).</summary>
        public int Season { get; set; }

        /// <summary>The newest biome unlocked (0 to 12; −1: none known, the Prairie is used).</summary>
        public int NewestBiome { get; set; } = -1;

        // ------------------------------------------------------------------------------------------- the generator

        /// <summary>The next number in [0, 1) of the account's generator (splitmix64 over the saved seed).</summary>
        public double NextUnit()
        {
            ulong z = _state.Seed += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>A roll of the generator under <paramref name="chance"/> (0 never rolls; 1 or more always succeeds without rolling).</summary>
        public bool Roll(double chance)
        {
            if (chance <= 0.0)
                return false;
            if (chance >= 1.0)
                return true;
            return NextUnit() < chance;
        }

        // ------------------------------------------------------------------------------------------- page facts

        public bool IsDrawable(int sticker) => AlbumData.Sticker(sticker).Drawable && _available[sticker];

        public int DrawableCount(int page)
        {
            int first = page * AlbumData.StickersPerPage, n = 0;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
                if (IsDrawable(first + i))
                    n++;
            return n;
        }

        public int MissingDrawables(int page)
        {
            int first = page * AlbumData.StickersPerPage, n = 0;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
                if (IsDrawable(first + i) && !_state.IsOwned(first + i))
                    n++;
            return n;
        }

        public int OwnedCount(int page)
        {
            int first = page * AlbumData.StickersPerPage, n = 0;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
                if (_state.IsOwned(first + i))
                    n++;
            return n;
        }

        /// <summary>The page's drawable and available Shiny, −1 without.</summary>
        public int ShinyOf(int page)
        {
            int first = page * AlbumData.StickersPerPage;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
                if (AlbumData.Sticker(first + i).Rarity == StickerRarity.Shiny && IsDrawable(first + i))
                    return first + i;
            return -1;
        }

        /// <summary>The next missing drawable sticker of an activity page in the fixed order 01 to 06 then 08 (−1: none left).</summary>
        public int NextPaidSticker(ActivityId activity)
        {
            int page = AlbumData.ActivityPage(activity);
            if (page < 0)
                return -1;
            var order = AlbumData.PaidOrderSlots;
            for (int i = 0; i < order.Length; i++)
            {
                int sticker = AlbumData.StickerAt(page, order[i]);
                if (IsDrawable(sticker) && !_state.IsOwned(sticker))
                    return sticker;
            }
            return -1;
        }

        /// <summary>
        /// The least complete open page with a missing drawable sticker (smallest share owned; the oldest, the lowest index,
        /// on a tie), −1 when none.
        /// </summary>
        public int LeastCompletePage()
        {
            int best = -1, bestOwned = int.MaxValue;
            for (int p = 0; p < AlbumState.Pages; p++)
            {
                if (!_state.IsPageOpen(p) || MissingDrawables(p) == 0)
                    continue;
                int owned = OwnedCount(p);
                if (owned < bestOwned)
                {
                    best = p;
                    bestOwned = owned;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------------------------------- the draw

        /// <summary>
        /// One draw (sections 1.5 and 1.6). Updates the duplicate streak and the page's Shiny counter; the caller gives the
        /// sticker (<see cref="AlbumService"/>). False and −1 when no open page can give a drawable sticker.
        /// </summary>
        public bool Draw(in StickerDrawRequest request, out StickerDraw draw)
        {
            draw = default;
            draw.Sticker = -1;
            draw.Page = -1;
            int page = -1;

            // 1. The season draw.
            if (SeasonEligible(request.Source))
            {
                int season = AlbumData.SeasonPage(Season);
                if (season >= 0 && _state.IsPageOpen(season) && MissingDrawables(season) > 0 && NextUnit() < _config.SeasonShare)
                {
                    page = season;
                    draw.SeasonDraw = true;
                }
            }

            // 2 to 5. The source's pages, the exclusions, the prorata.
            if (page < 0)
            {
                int n = Filter(Candidates(in request), -1, false);
                if (n == 0)
                    return false;
                page = Pick(n);
            }

            // The Shiny guarantee: the page counts the draws that chose it while its Shiny is missing.
            int shiny = ShinyOf(page);
            if (shiny >= 0 && !_state.IsOwned(shiny))
            {
                if (_state.ShinyDraws[page] < byte.MaxValue)
                    _state.ShinyDraws[page]++;
                if (_state.ShinyDraws[page] >= _config.ShinyAfter)
                {
                    draw.ShinyGuarantee = true;
                    return Finish(page, shiny, ref draw);
                }
            }

            // The newness guarantee: after 3 duplicates, a missing sticker (another page by prorata if this one has none).
            bool missingOnly = IsMissingSource(request.Source) && MissingDrawables(page) > 0;
            if (_state.DuplicateStreak >= _config.NewnessAfter)
            {
                if (MissingDrawables(page) == 0)
                {
                    int n = Filter(Candidates(in request), page, true);
                    if (n > 0)
                        page = Pick(n);
                }
                if (MissingDrawables(page) > 0)
                {
                    missingOnly = true;
                    draw.NewnessGuarantee = true;
                }
            }

            int sticker = PickSticker(page, in request, missingOnly);
            if (sticker < 0)
                return false;
            return Finish(page, sticker, ref draw);
        }

        private bool Finish(int page, int sticker, ref StickerDraw draw)
        {
            draw.Page = page;
            draw.Sticker = sticker;
            if (_state.IsOwned(sticker))
            {
                if (_state.DuplicateStreak < byte.MaxValue)
                    _state.DuplicateStreak++;
            }
            else
            {
                _state.DuplicateStreak = 0;
            }
            return true;
        }

        public static bool SeasonEligible(StickerSource source) =>
            source == StickerSource.ActivityGame || source == StickerSource.CuddleLevel || source == StickerSource.GardenHarvest
            || source == StickerSource.Wish || source == StickerSource.Expedition;

        /// <summary>Daily gift, calendar and new record: a missing drawable sticker while their pages have one.</summary>
        public static bool IsMissingSource(StickerSource source) =>
            source == StickerSource.DailyGift || source == StickerSource.Calendar || source == StickerSource.Record;

        /// <summary>The source's pages and shares (section 1.4), before the exclusions.</summary>
        private int Candidates(in StickerDrawRequest request)
        {
            int n = 0;
            var c = _config;
            int biome = request.Biome;
            switch (request.Source)
            {
                case StickerSource.ActivityGame:
                case StickerSource.CuddleLevel:
                case StickerSource.GardenHarvest:
                    var activity = request.Source == StickerSource.CuddleLevel ? ActivityId.CuddleMatch
                        : request.Source == StickerSource.GardenHarvest ? ActivityId.Garden : request.Activity;
                    n = Add(n, AlbumData.ActivityPage(activity), c.ShareActivityPage);
                    n = Add(n, AlbumData.BiomePage(biome, 1), c.ShareActiveBiomePage1);
                    n = Add(n, AlbumData.BiomePage(biome, 2), c.ShareActiveBiomePage2);
                    break;
                case StickerSource.Record:
                    n = Add(n, AlbumData.ActivityPage(request.Activity), 1.0);
                    break;
                case StickerSource.CuddleGuarantee:
                    n = Add(n, AlbumData.ActivityPage(ActivityId.CuddleMatch), 1.0);
                    break;
                case StickerSource.Contest:
                    n = Add(n, AlbumData.ActivityPage(ActivityId.GrandContest), 1.0);
                    break;
                case StickerSource.Wish:
                    n = Add(n, AlbumData.BiomePage(biome, 1), c.ShareWishPage1);
                    n = Add(n, AlbumData.BiomePage(biome, 2), c.ShareWishPage2);
                    break;
                case StickerSource.Expedition:
                    if (request.EventDestination)
                    {
                        n = Add(n, LeastCompletePage(), c.ShareExpeditionLeastComplete);
                    }
                    else
                    {
                        n = Add(n, AlbumData.BiomePage(biome, 1), c.ShareExpeditionPage1);
                        n = Add(n, AlbumData.BiomePage(biome, 2), c.ShareExpeditionPage2);
                    }
                    n = Add(n, AlbumData.ActivityPage(ActivityId.Expeditions), c.ShareExpeditionActivity);
                    break;
                case StickerSource.DailyGift:
                case StickerSource.Calendar:
                {
                    int newest = NewestBiome >= 0 ? NewestBiome : 0;
                    int p1 = AlbumData.BiomePage(newest, 1), p2 = AlbumData.BiomePage(newest, 2);
                    n = Add(n, p1, c.ShareGiftNewestBiome * 0.5);
                    n = Add(n, p2, c.ShareGiftNewestBiome * 0.5);
                    int others = 0;
                    for (int p = 0; p < AlbumState.Pages; p++)
                        if (IsGiftOther(p, p1, p2))
                            others++;
                    if (others > 0)
                    {
                        double share = (1.0 - c.ShareGiftNewestBiome) / others;
                        for (int p = 0; p < AlbumState.Pages; p++)
                            if (IsGiftOther(p, p1, p2))
                                n = Add(n, p, share);
                    }
                    break;
                }
                case StickerSource.WorldEvent:
                    n = Add(n, AlbumData.BiomePage(biome, 2), c.ShareWorldPage2);
                    n = Add(n, AlbumData.BiomePage(biome, 1), c.ShareWorldPage1);
                    break;
            }
            return n;
        }

        /// <summary>The daily gift's « other open pages »: biome, season and activity pages (never an event page).</summary>
        private bool IsGiftOther(int page, int p1, int p2) =>
            page != p1 && page != p2 && _state.IsPageOpen(page) && AlbumData.Page(page).Kind != AlbumKind.Event && DrawableCount(page) > 0;

        private int Add(int n, int page, double share)
        {
            if (page < 0 || share <= 0.0)
                return n;
            for (int i = 0; i < n; i++)
            {
                if (_pages[i] != page)
                    continue;
                _shares[i] += share;
                return n;
            }
            _pages[n] = page;
            _shares[n] = share;
            return n + 1;
        }

        /// <summary>
        /// Steps 3 and 4: removes closed pages and pages without a drawable sticker, then the complete pages unless that
        /// removes them all (a duplicate then). With <paramref name="missingOnly"/>, complete pages always go and
        /// <paramref name="exclude"/> too (the newness guarantee's second pass).
        /// </summary>
        private int Filter(int n, int exclude, bool missingOnly)
        {
            int kept = 0;
            for (int i = 0; i < n; i++)
            {
                int page = _pages[i];
                if (page == exclude || !_state.IsPageOpen(page) || DrawableCount(page) == 0)
                    continue;
                _pages[kept] = page;
                _shares[kept] = _shares[i];
                kept++;
            }
            bool anyMissing = false;
            for (int i = 0; i < kept && !anyMissing; i++)
                anyMissing = MissingDrawables(_pages[i]) > 0;
            if (!anyMissing)
                return missingOnly ? 0 : kept;
            int left = 0;
            for (int i = 0; i < kept; i++)
            {
                if (MissingDrawables(_pages[i]) == 0)
                    continue;
                _pages[left] = _pages[i];
                _shares[left] = _shares[i];
                left++;
            }
            return left;
        }

        /// <summary>Step 5: the shares put back to 100 % by prorata, then the page drawn.</summary>
        private int Pick(int n)
        {
            double total = 0.0;
            for (int i = 0; i < n; i++)
                total += _shares[i];
            double roll = NextUnit();
            if (total <= 0.0)
                return _pages[System.Math.Min(n - 1, (int)(roll * n))];
            double at = roll * total;
            for (int i = 0; i < n; i++)
            {
                at -= _shares[i];
                if (at < 0.0)
                    return _pages[i];
            }
            return _pages[n - 1];
        }

        /// <summary>Section 1.6: the rarity among those the pool holds (prorata when one is missing), then the sticker (1, or 3 when preferred).</summary>
        private int PickSticker(int page, in StickerDrawRequest request, bool missingOnly)
        {
            int first = page * AlbumData.StickersPerPage;
            double common = 0.0, rare = 0.0, shiny = 0.0;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
            {
                int s = first + i;
                if (!IsDrawable(s) || (missingOnly && _state.IsOwned(s)))
                    continue;
                switch (AlbumData.Sticker(s).Rarity)
                {
                    case StickerRarity.Common: common = _config.Weight(StickerRarity.Common); break;
                    case StickerRarity.Rare: rare = _config.Weight(StickerRarity.Rare); break;
                    default: shiny = _config.Weight(StickerRarity.Shiny); break;
                }
            }
            double total = common + rare + shiny;
            StickerRarity rarity;
            if (total <= 0.0)
            {
                rarity = StickerRarity.Common;
                if (!HasRarity(page, StickerRarity.Common, missingOnly))
                    rarity = HasRarity(page, StickerRarity.Rare, missingOnly) ? StickerRarity.Rare : StickerRarity.Shiny;
            }
            else
            {
                double at = NextUnit() * total;
                rarity = at < common ? StickerRarity.Common : at < common + rare ? StickerRarity.Rare : StickerRarity.Shiny;
            }

            int n = 0;
            double sum = 0.0;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
            {
                int s = first + i;
                if (!IsDrawable(s) || (missingOnly && _state.IsOwned(s)) || AlbumData.Sticker(s).Rarity != rarity)
                    continue;
                double weight = Matches(s, page, in request) ? System.Math.Max(1.0, _config.PreferredWeight) : 1.0;
                _pool[n] = s;
                _weights[n] = weight;
                sum += weight;
                n++;
            }
            if (n == 0)
                return -1;
            double pick = NextUnit() * sum;
            for (int i = 0; i < n; i++)
            {
                pick -= _weights[i];
                if (pick < 0.0)
                    return _pool[i];
            }
            return _pool[n - 1];
        }

        private bool HasRarity(int page, StickerRarity rarity, bool missingOnly)
        {
            int first = page * AlbumData.StickersPerPage;
            for (int i = 0; i < AlbumData.StickersPerPage; i++)
            {
                int s = first + i;
                if (IsDrawable(s) && (!missingOnly || !_state.IsOwned(s)) && AlbumData.Sticker(s).Rarity == rarity)
                    return true;
            }
            return false;
        }

        /// <summary>The sticker's preferred source matches this draw (section 1.6 table).</summary>
        public bool Matches(int sticker, int page, in StickerDrawRequest request)
        {
            ref readonly var row = ref AlbumData.Sticker(sticker);
            int biome = AlbumData.BiomeOfPage(page);
            var world = World;
            switch (row.Preferred)
            {
                case StickerPreference.Wish:
                    return request.Source == StickerSource.Wish && request.Species.IsValid && request.Species == AlbumData.PreferredSpecies(sticker);
                case StickerPreference.ActivityInBiome:
                    return request.Source == StickerSource.ActivityGame && request.Activity == row.PreferredActivity && biome >= 0
                           && request.Biome == biome;
                case StickerPreference.GardenInBiome:
                    return request.Source == StickerSource.GardenHarvest && biome >= 0 && request.Biome == biome;
                case StickerPreference.DailyGift:
                    return request.Source == StickerSource.DailyGift || request.Source == StickerSource.Calendar;
                case StickerPreference.WorldEvent:
                    return request.Source == StickerSource.WorldEvent && biome >= 0 && request.Biome == biome;
                case StickerPreference.Weather:
                    return world != null && biome >= 0 && world.WeatherActive(biome, row.PreferredSubject);
                case StickerPreference.Morning:
                    return world != null && world.Morning(biome >= 0 ? biome : request.Biome);
                case StickerPreference.Evening:
                    return world != null && world.Evening(biome >= 0 ? biome : request.Biome);
                case StickerPreference.SignatureMoment:
                    return world != null && biome >= 0 && world.SignatureMomentActive(biome);
                case StickerPreference.SeasonWeather:
                    return world != null && world.SeasonWeatherActive(row.PreferredSubject);
                case StickerPreference.SeasonExpedition:
                    return request.Source == StickerSource.Expedition;
                case StickerPreference.SeasonWish:
                    return request.Source == StickerSource.Wish;
                case StickerPreference.SeasonActivity:
                    return request.Source == StickerSource.ActivityGame
                           && (row.PreferredActivity == ActivityId.None || row.PreferredActivity == request.Activity);
                case StickerPreference.SeasonGarden:
                    return request.Source == StickerSource.GardenHarvest;
                default:
                    return false;
            }
        }
    }
}
