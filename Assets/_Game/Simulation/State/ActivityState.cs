#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T37, doc « Mini-jeux &amp; activités » section 2 and doc 01 Contenu sections 0 and 12.0: everything the activities
    /// remember. Plain data, fixed-size arrays allocated once. Nothing here is reset by an absence: the refill anchor is a
    /// UTC time (offline time refills too), day keys only move forward, records and counters only grow.
    /// </summary>
    public sealed class ActivityState
    {
        /// <summary>Record slots: the 18 activities (slot = id − 1) and the framework's test activity (last slot, never saved).</summary>
        public const int SlotCount = ActivityCatalog.SlotCount;

        public const int ChallengesPerActivity = ActivityCatalog.ChallengesPerActivity;

        // ---------------- Tickets (doc Mini-jeux, section 2; doc 01, section 0 « Billet ») ----------------

        /// <summary>The pool was started (the first use fills it to the reserve, A15 shows 5).</summary>
        public bool TicketsStarted;

        /// <summary>Free tickets (refill, gifts, ads, the Club, events, Circles); used first.</summary>
        public int FreeTickets;

        /// <summary>Tickets bought with Gems, counted apart and used after the free ones (no paid randomness).</summary>
        public int PaidTickets;

        /// <summary>UTC milliseconds of the last refill tick (or of the moment the reserve stopped being full).</summary>
        public long RefillAnchorUnixMs;

        /// <summary>Day key of the ticket packs counter, and packs bought that day.</summary>
        public int PackDayKey;
        public int PacksToday;

        // ---------------- Gems of the day (doc 08 gems.activity_first_3stars, gems.activity_daily_cap) ----------------

        public int GemsDayKey;
        public int GemsToday;

        // ---------------- Challenges (doc 01, section 12.0 « Défis ») ----------------

        /// <summary>Challenges completed at 3 stars in all activities (a challenge decoration every 5).</summary>
        public int ChallengesThreeStars;

        /// <summary>Challenge decorations granted (E-DEC-CH-01 onward, in order).</summary>
        public int ChallengeDecorations;

        /// <summary>Games started since install (the deterministic seed of each game).</summary>
        public int GamesStarted;

        /// <summary>One record per activity slot (<see cref="ActivityCatalog.SlotOf"/>).</summary>
        public ActivityRecord[] Records = new ActivityRecord[SlotCount];

        /// <summary>Stars (0 to 3) of each challenge: slot × 10 + challenge.</summary>
        public byte[] ChallengeStars = new byte[SlotCount * ChallengesPerActivity];

        /// <summary>Rewarded attempts of each challenge (analytics 112 « attempts »): slot × 10 + challenge.</summary>
        public ushort[] ChallengeAttempts = new ushort[SlotCount * ChallengesPerActivity];

        public static int ChallengeIndex(int slot, int challenge) => slot * ChallengesPerActivity + challenge;

        /// <summary>Challenges of a slot completed at 3 stars (the hub's « n/10 »).</summary>
        public int ChallengesDone(int slot)
        {
            if ((uint)slot >= SlotCount)
                return 0;
            int done = 0;
            int start = slot * ChallengesPerActivity;
            for (int i = 0; i < ChallengesPerActivity; i++)
                if (ChallengeStars[start + i] >= 3)
                    done++;
            return done;
        }
    }

    /// <summary>What one activity remembers (doc 01, section 12.0). A zero record is a never-played activity.</summary>
    public struct ActivityRecord
    {
        /// <summary>The activity opened once: openings are final (doc 08, rule 3), the hub never locks it again.</summary>
        public bool Unlocked;

        /// <summary>Best score of a rewarded free-mode game (practice never sets records).</summary>
        public int Best;

        /// <summary>Most stars of a rewarded free-mode game.</summary>
        public byte BestStars;

        /// <summary>Adaptive difficulty d, 1 to 10; 0 means never adapted yet (the start value, 3).</summary>
        public byte Difficulty;

        /// <summary>Free-mode games in a row at 0 stars, and at 3 stars (practice counts).</summary>
        public byte ZeroStarStreak;
        public byte ThreeStarStreak;

        /// <summary>Games finished, every mode.</summary>
        public int GamesPlayed;

        /// <summary>Rewarded games finished (a ticket or the free game).</summary>
        public int RewardedGames;

        /// <summary>The free first game was used (Grooming, doc 01 A15).</summary>
        public bool FreeGameUsed;

        /// <summary>Day key of the « first 3 stars of the day » Gems of this activity.</summary>
        public int ThreeStarGemsDayKey;

        /// <summary>The last theme chosen (a biome id), kept by the hub.</summary>
        public ContentId LastTheme;

        /// <summary>The fixed sticker counter of games played with a bought ticket: 10, 20 or 35 points, a sticker at 100.</summary>
        public int PaidStickerPoints;
    }
}
