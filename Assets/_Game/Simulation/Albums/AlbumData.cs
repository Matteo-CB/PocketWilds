#nullable enable
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// How a fixed sticker is given (doc 01 Albums, sections 1.3, 1.4 « Sources fixes » and 2 to 5), with the value of its
    /// condition in <see cref="StickerRow.FixedValue"/>. Stable values (written in the generated data).
    /// </summary>
    public enum StickerFixedSource : byte
    {
        /// <summary>Drawable only.</summary>
        None = 0,

        /// <summary>Given at the albums' opening, then drawable (STK-GL-01 Lapin curieux).</summary>
        Offered = 1,

        /// <summary>Slot 07 of a biome album: the companion in silhouette, owned from the album's opening.</summary>
        Companion = 2,

        /// <summary>A zone of page 2: value = the zone's index in its biome (0 arrival, expansions, passage last).</summary>
        Zone = 3,

        /// <summary>Sticker 24: drawable, and guaranteed once the first time the moment is seen.</summary>
        SignatureMoment = 4,

        /// <summary>Event free track: value = the tier (1, 6, 11, 19, 23, 26).</summary>
        EventTrack = 5,

        /// <summary>Event featured activity challenges: value = the challenge stars (9, 18, 27).</summary>
        EventChallengeStars = 6,

        /// <summary>Event long quest: value = its position (4, 7, 10).</summary>
        EventQuest = 7,

        /// <summary>A season's rare visitor, first touch: value = the visitor (1 to 3).</summary>
        Visitor = 8,

        /// <summary>Scored activity 07: the first rewarded game at 3 stars (value 3).</summary>
        ThreeStars = 9,

        /// <summary>Scored activity 09, 10, 12: value = the challenge stars (10, 20, 30).</summary>
        ChallengeStars = 10,

        /// <summary>Scored activity 11: value = the new records (5).</summary>
        Records = 11,

        ExpeditionsDone = 12,
        ExpeditionBiomes = 13,

        /// <summary>Expeditions of 12 h finished.</summary>
        LongExpeditions = 14,

        Harvests = 15,

        /// <summary>Different plants harvested.</summary>
        PlantsHarvested = 16,

        DyesObtained = 17,
        GardenLevel = 18,
        CuddleLevel = 19,
        ContestEntries = 20,

        /// <summary>Value 1 bronze, 2 silver, 3 gold.</summary>
        ContestRibbon = 21,
    }

    /// <summary>The preferred source of a drawable sticker (section 1.6): a matching draw weighs 3 instead of 1.</summary>
    public enum StickerPreference : byte
    {
        None = 0,

        /// <summary>A wish of the species <see cref="StickerRow.PreferredSubject"/>.</summary>
        Wish = 1,

        /// <summary>A rewarded game of <see cref="StickerRow.PreferredActivity"/> started in the page's biome.</summary>
        ActivityInBiome = 2,

        /// <summary>A garden harvest while the garden is open in the page's biome.</summary>
        GardenInBiome = 3,

        /// <summary>The daily gift's sticker lot or a calendar box.</summary>
        DailyGift = 4,

        /// <summary>A world event touched in the page's biome.</summary>
        WorldEvent = 5,

        /// <summary>Any draw choosing the page while the weather <see cref="StickerRow.PreferredSubject"/> is active in its biome.</summary>
        Weather = 6,

        Morning = 7,
        Evening = 8,
        SignatureMoment = 9,

        /// <summary>A season draw during the season weather <see cref="StickerRow.PreferredSubject"/>.</summary>
        SeasonWeather = 10,

        SeasonExpedition = 11,
        SeasonWish = 12,

        /// <summary>A season draw of an activity game (<see cref="StickerRow.PreferredActivity"/>, None: any activity).</summary>
        SeasonActivity = 13,

        SeasonGarden = 14,
    }

    /// <summary>One album of the generated table.</summary>
    public readonly struct AlbumRow
    {
        public readonly string Key;
        public readonly AlbumKind Kind;

        /// <summary>biome.grassland for a biome album, empty for the three big albums.</summary>
        public readonly string Subject;

        public readonly int FirstPage;
        public readonly int Pages;

        public AlbumRow(string key, AlbumKind kind, string subject, int firstPage, int pages)
        {
            Key = key;
            Kind = kind;
            Subject = subject;
            FirstPage = firstPage;
            Pages = pages;
        }
    }

    /// <summary>One page of the generated table.</summary>
    public readonly struct PageRow
    {
        public readonly string Key;
        public readonly int Album;
        public readonly AlbumKind Kind;

        /// <summary>1 or 2 for a biome page, 101 to 112 for an event, 1 to 4 for a season, the ActivityId for an activity.</summary>
        public readonly int Number;

        public readonly string Subject;

        public PageRow(string key, int album, AlbumKind kind, int number, string subject)
        {
            Key = key;
            Album = album;
            Kind = kind;
            Number = number;
            Subject = subject;
        }
    }

    /// <summary>One sticker of the generated table (section 1.14 StickerDefinition, without its capture and sprite).</summary>
    public readonly struct StickerRow
    {
        public readonly string Key;
        public readonly int Page;
        public readonly int Slot;
        public readonly StickerRarity Rarity;
        public readonly bool Drawable;
        public readonly StickerFixedSource Fixed;
        public readonly int FixedValue;
        public readonly StickerPreference Preferred;
        public readonly ActivityId PreferredActivity;

        /// <summary>The species (species.rabbit) or the weather key (light_rain) of the preference; empty without.</summary>
        public readonly string PreferredSubject;

        public readonly StickerHint Hint;

        public StickerRow(string key, int page, int slot, StickerRarity rarity, bool drawable, StickerFixedSource fixedSource,
            int fixedValue, StickerPreference preferred, ActivityId preferredActivity, string preferredSubject, StickerHint hint)
        {
            Key = key;
            Page = page;
            Slot = slot;
            Rarity = rarity;
            Drawable = drawable;
            Fixed = fixedSource;
            FixedValue = fixedValue;
            Preferred = preferred;
            PreferredActivity = preferredActivity;
            PreferredSubject = preferredSubject;
            Hint = hint;
        }
    }

    /// <summary>
    /// T44A: the 16 albums, 60 pages and 720 stickers (doc 01 Albums, sections 2 to 5), generated by
    /// Tools/Data/pw_album_data.py into AlbumData.Generated.cs, with the lookups the rules need. Every table is built once
    /// at type load; reads never allocate. Sticker index = page × 12 + slot; new stickers are appended, an id is never reused.
    /// </summary>
    public static partial class AlbumData
    {
        public const int StickersPerPage = 12;
        public const int BiomeAlbums = 13;
        public const int EventAlbum = 13;
        public const int SeasonAlbum = 14;
        public const int ActivityAlbum = 15;
        public const int FirstEventPage = 26;
        public const int FirstSeasonPage = 38;
        public const int FirstActivityPage = 42;
        public const int ActivityPages = 18;

        /// <summary>Activity pages' drawable slots in the fixed order of a paid game (01 to 06, then 08; section 1.4).</summary>
        public static readonly int[] PaidOrderSlots = { 0, 1, 2, 3, 4, 5, 7 };

        private static Dictionary<string, int>? _byKey;

        public static ref readonly AlbumRow Album(int album) => ref AlbumRows[album];

        public static ref readonly PageRow Page(int page) => ref PageRows[page];

        public static ref readonly StickerRow Sticker(int sticker) => ref StickerRows[sticker];

        public static int StickerAt(int page, int slot) => page * StickersPerPage + slot;

        /// <summary>The biome album's index (0 to 12) of a biome id, −1 for anything else.</summary>
        public static int BiomeIndex(ContentId biome)
        {
            if (!biome.IsValid)
                return -1;
            var ids = Lookups.BiomeIds;
            for (int b = 0; b < ids.Length; b++)
                if (ids[b] == biome)
                    return b;
            return -1;
        }

        public static ContentId BiomeId(int biome) => (uint)biome < BiomeAlbums ? Lookups.BiomeIds[biome] : ContentId.None;

        /// <summary>Page 1 « Habitants » or 2 « Le biome » of a biome album, −1 outside.</summary>
        public static int BiomePage(int biome, int number) =>
            (uint)biome < BiomeAlbums && (number == 1 || number == 2) ? biome * 2 + number - 1 : -1;

        /// <summary>The biome (0 to 12) of a biome page, −1 for another page.</summary>
        public static int BiomeOfPage(int page) => (uint)page < FirstEventPage ? page / 2 : -1;

        public static int EventPage(int eventNumber) => eventNumber >= 101 && eventNumber <= 112 ? FirstEventPage + eventNumber - 101 : -1;

        public static int SeasonPage(int season) => season >= 1 && season <= 4 ? FirstSeasonPage + season - 1 : -1;

        public static int ActivityPage(ActivityId activity)
        {
            int n = (int)activity;
            return n >= 1 && n <= ActivityPages ? FirstActivityPage + n - 1 : -1;
        }

        public static ActivityId ActivityOfPage(int page) =>
            page >= FirstActivityPage && page < FirstActivityPage + ActivityPages ? (ActivityId)(page - FirstActivityPage + 1) : ActivityId.None;

        /// <summary>The species of a « Souhait d'un … » preference (None without).</summary>
        public static ContentId PreferredSpecies(int sticker) => Lookups.Species[sticker];

        /// <summary>The index of a sticker key (sticker.gl.01), −1 when unknown. Builds a dictionary on first use: tools and loading only.</summary>
        public static int IndexOf(string key)
        {
            var map = _byKey;
            if (map == null)
            {
                map = new Dictionary<string, int>(StickerCount);
                for (int i = 0; i < StickerRows.Length; i++)
                    map[StickerRows[i].Key] = i;
                _byKey = map;
            }
            return key != null && map.TryGetValue(key, out int index) ? index : -1;
        }

        /// <summary>Tables derived from the generated rows, in their own type so they are built after them.</summary>
        private static class Lookups
        {
            public static readonly ContentId[] BiomeIds = BuildBiomeIds();
            public static readonly ContentId[] Species = BuildSpecies();
        }

        private static ContentId[] BuildBiomeIds()
        {
            var ids = new ContentId[BiomeAlbums];
            for (int b = 0; b < BiomeAlbums; b++)
                ids[b] = ContentId.FromSource(AlbumRows[b].Subject);
            return ids;
        }

        private static ContentId[] BuildSpecies()
        {
            var ids = new ContentId[StickerRows.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                ref readonly var row = ref StickerRows[i];
                if (row.Preferred == StickerPreference.Wish && row.PreferredSubject.Length > 0)
                    ids[i] = ContentId.FromSource(row.PreferredSubject);
            }
            return ids;
        }
    }
}
