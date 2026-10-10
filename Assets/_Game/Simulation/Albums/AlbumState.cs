#nullable enable

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T44A « Albums » (doc 01 Albums, section 1.14 « Sauvegarde »): everything the albums remember. Plain data, fixed-size
    /// arrays allocated once, bit sets by sticker or page index (index = page × 12 + slot; new stickers are appended, an id is
    /// never reused). Saved in section 20 (schema 14).
    /// </summary>
    public sealed class AlbumState
    {
        public const int Stickers = 720;
        public const int Pages = 60;
        public const int Albums = 16;
        public const int ActivityPages = 18;
        public const int Biomes = 13;
        public const int StickerBytes = (Stickers + 7) / 8;
        public const int PageRewardBytes = (Pages * 2 + 7) / 8;

        /// <summary>The albums' opening was done (STK-GL-01, the companions and the zones already open given once).</summary>
        public bool Opened;

        /// <summary>720 bits: stickers owned (90 bytes).</summary>
        public byte[] Owned = new byte[StickerBytes];

        /// <summary>720 bits: gained but not looked at yet (« Nouveau » until the page is seen).</summary>
        public byte[] Unseen = new byte[StickerBytes];

        /// <summary>Album stars: never expire, no cap, never bought.</summary>
        public int Stars;

        /// <summary>Draws in a row that gave a duplicate (the newness guarantee at 3).</summary>
        public byte DuplicateStreak;

        /// <summary>Per page, the draws that chose it since it opened while its Shiny was missing (the Shiny guarantee at 30).</summary>
        public byte[] ShinyDraws = new byte[Pages];

        /// <summary>
        /// Per activity page, the fixed counter of games paid with Gems (points of chance, a sticker at 100). The scored
        /// activities keep theirs in <see cref="ActivityRecord.PaidStickerPoints"/> (T37); the meta activities (the Garden's
        /// harvests sped up with Gems, the Hug Match's bought moves) use this one.
        /// </summary>
        public byte[] PaidPoints = new byte[ActivityPages];

        /// <summary>13 bits: the signature moment of each biome already seen (its sticker 24 guaranteed once).</summary>
        public ushort SignatureSeen;

        /// <summary>120 bits: page × 2 the half reward claimed, page × 2 + 1 the complete reward claimed.</summary>
        public byte[] PageRewards = new byte[PageRewardBytes];

        /// <summary>16 bits: the album complete reward claimed.</summary>
        public ushort AlbumRewards;

        /// <summary>The account's deterministic generator (0: not seeded yet, the profile id seeds it).</summary>
        public ulong Seed;

        /// <summary>60 bits: pages open (a page once open never closes).</summary>
        public ulong PagesOpen;

        // ---- Counters the albums' sources need and no other system keeps (counted from T44A on) ----

        /// <summary>The day of <see cref="WishesToday"/> (yyyymmdd) and the wishes granted that day (the 3rd is a sure sticker).</summary>
        public int WishDayKey;

        public byte WishesToday;

        /// <summary>New records per activity page (sticker 11 at the 5th).</summary>
        public byte[] Records = new byte[ActivityPages];

        /// <summary>Expeditions of 12 h finished, biomes reached (13 bits), plants harvested (25 bits), dyes obtained.</summary>
        public int LongExpeditions;

        public ushort ExpeditionBiomes;
        public uint PlantsHarvested;
        public int DyesObtained;

        /// <summary>Garden plots whose sprout was finished with Gems (their harvest follows the fixed counter, never a draw).</summary>
        public byte GardenSpedUp;

        public static bool Bit(byte[] bits, int index) => (bits[index >> 3] & (1 << (index & 7))) != 0;

        public static void SetBit(byte[] bits, int index, bool value)
        {
            if (value)
                bits[index >> 3] |= (byte)(1 << (index & 7));
            else
                bits[index >> 3] &= (byte)~(1 << (index & 7));
        }

        public bool IsOwned(int sticker) => Bit(Owned, sticker);

        public bool IsPageOpen(int page) => (PagesOpen & (1UL << page)) != 0;

        /// <summary>Back to a new account (tests, the debug panel).</summary>
        public void Clear()
        {
            Opened = false;
            System.Array.Clear(Owned, 0, Owned.Length);
            System.Array.Clear(Unseen, 0, Unseen.Length);
            Stars = 0;
            DuplicateStreak = 0;
            System.Array.Clear(ShinyDraws, 0, ShinyDraws.Length);
            System.Array.Clear(PaidPoints, 0, PaidPoints.Length);
            SignatureSeen = 0;
            System.Array.Clear(PageRewards, 0, PageRewards.Length);
            AlbumRewards = 0;
            Seed = 0;
            PagesOpen = 0;
            WishDayKey = 0;
            WishesToday = 0;
            System.Array.Clear(Records, 0, Records.Length);
            LongExpeditions = 0;
            ExpeditionBiomes = 0;
            PlantsHarvested = 0;
            DyesObtained = 0;
            GardenSpedUp = 0;
        }
    }
}
