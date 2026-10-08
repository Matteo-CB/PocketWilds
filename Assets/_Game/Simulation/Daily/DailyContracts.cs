#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The 28 systems a wish or a journal goal belongs to (doc 01 Contenu, section 2.2, SYS-01 to SYS-28). Stable values:
    /// the value is the doc's number. Each system opens with its feature; T36 ships the wishes of Hatching, Discoveries and
    /// mastery, Cuddles and treats, Upgrades and Expansions.
    /// </summary>
    public enum WishSystem : byte
    {
        None = 0,
        Hatching = 1,
        DiscoveriesAndMastery = 2,
        CuddlesAndTreats = 3,
        Upgrades = 4,
        Decoration = 5,
        Expansions = 6,
        Garden = 7,
        Bakery = 8,
        Grooming = 9,
        HideAndSeek = 10,
        NutRain = 11,
        PawMemory = 12,
        Expeditions = 13,
        TreasureFishing = 14,
        LilyHop = 15,
        Photo = 16,
        CuddleMatch = 17,
        Slide = 18,
        Constellations = 19,
        FireflyRhythm = 20,
        Bubbles = 21,
        TilePuzzle = 22,
        BurrowMaze = 23,
        Breeding = 24,
        Albums = 25,
        RareVisitors = 26,
        Outfits = 27,
        GrandContest = 28,

        /// <summary>The rendez-vous themselves (journal goals about wishes, gifts and the calendar; not a wish system).</summary>
        Rendezvous = 29,
    }

    /// <summary>The 5 families of systems (doc 01, section 2.2). Stable values.</summary>
    public enum WishFamily : byte
    {
        Collection = 0,
        Care = 1,
        Refuge = 2,
        Games = 3,
        Travels = 4,
    }

    public static class WishSystems
    {
        public const int Count = 29;

        public static WishFamily FamilyOf(WishSystem system)
        {
            switch (system)
            {
                case WishSystem.Hatching:
                case WishSystem.DiscoveriesAndMastery:
                case WishSystem.Breeding:
                case WishSystem.Albums:
                case WishSystem.RareVisitors:
                    return WishFamily.Collection;
                case WishSystem.CuddlesAndTreats:
                case WishSystem.Grooming:
                case WishSystem.Outfits:
                    return WishFamily.Care;
                case WishSystem.Upgrades:
                case WishSystem.Decoration:
                case WishSystem.Expansions:
                case WishSystem.Garden:
                case WishSystem.Bakery:
                    return WishFamily.Refuge;
                case WishSystem.Expeditions:
                case WishSystem.Photo:
                    return WishFamily.Travels;
                default:
                    return WishFamily.Games;
            }
        }
    }

    /// <summary>Where bond points come from (analytics, Essence multipliers). Stable values.</summary>
    public enum BondSource : byte
    {
        Touch = 0,
        Treat = 1,
        Wish = 2,
        Activity = 3,
    }

    /// <summary>Gives bond to an animal; returns the points really added (after multipliers and the cap).</summary>
    public interface IBondSink
    {
        int AddBond(uint animal, int points, BondSource source);
    }

    /// <summary>
    /// The play events wishes and journal goals count (doc 01, sections 2.3 and 5.2). <see cref="DailyVisitServices"/>
    /// subscribes once to the game services and forwards each event to every listener; a listener ignores what it does not
    /// count. Calls happen on the main thread and must not allocate.
    /// </summary>
    public interface IDailyProgressHooks
    {
        /// <summary>One animal hatched (a × 10 calls this ten times).</summary>
        void OnHatched(in HatchResult result);

        /// <summary>A species gained <paramref name="levels"/> mastery levels.</summary>
        void OnMasteryLeveled(ContentId species, int levels);

        void OnCodexPageOpened(ContentId species);

        /// <summary>An animal was touched; <paramref name="countedForBond"/> when the touch was inside the daily caps.</summary>
        void OnAnimalTouched(uint animal, bool countedForBond);

        void OnTreatGiven(uint animal);

        void OnBondGained(uint animal, int points);

        /// <summary>Upgrade levels bought at once (a × 10 or Max counts every level).</summary>
        void OnUpgradesBought(int levels);

        void OnZoneOpened();

        void OnWishCompleted();

        void OnGiftOpened();

        void OnCalendarStamped();
    }
}
