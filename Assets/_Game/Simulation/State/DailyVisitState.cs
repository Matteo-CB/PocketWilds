#nullable enable
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T36, doc « 01 — Contenu des rendez-vous, des activités et de l'accueil »: everything the daily rendez-vous remembers
    /// (welcome gifts, daily gift, 28-day calendar, wishes, weekly journal, return gifts, daily touch caps). Plain data,
    /// fixed-size arrays allocated once. Nothing here is ever reset by an absence (doc 01, section 6.4): day keys only move
    /// forward and every counter survives a missed day.
    /// </summary>
    public sealed class DailyVisitState
    {
        /// <summary>Wish slots kept: the stock (at most 9 by default) plus room for the Remote Config bounds.</summary>
        public const int MaxWishStock = 28;

        public const int HistorySize = 16;
        public const int MaxJournalGoals = 10;
        public const int MaxWishSystems = 32;
        public const int TouchedForWishSize = 12;

        // ---------------- Days ----------------

        /// <summary>Highest local day key (yyyymmdd, reset hour applied) already processed. Never decreases.</summary>
        public int LastDayKey;

        /// <summary>Distinct days the game was opened (the calendar and the welcome gifts advance per login day).</summary>
        public int LoginDays;

        /// <summary>The welcome is over (tutorial wish done, or a save that already had 5 animals).</summary>
        public bool OnboardingDone;

        /// <summary>0 not started, 1 waiting for its trigger (A08), 2 the tutorial wish is live, 3 done.</summary>
        public byte TutorialStep;

        // ---------------- Welcome gifts (doc 01, section 1.4) ----------------

        /// <summary>Welcome gifts claimed (0 to 3).</summary>
        public byte WelcomeClaimed;

        /// <summary>Day key of the last welcome claim (one a login day).</summary>
        public int WelcomeDayKey;

        // ---------------- Daily gift (section 3) ----------------

        /// <summary>Day key of the last gift opened (0: never).</summary>
        public int GiftDayKey;

        /// <summary>Gifts opened since install; 0 means the next one is the fixed Discovery egg.</summary>
        public int GiftsOpened;

        /// <summary>Non-big gifts in a row (the guarantee: the 15th is big).</summary>
        public int GiftsSinceBig;

        /// <summary>The last lot opened (shown again until midnight).</summary>
        public byte LastGiftLot;

        // ---------------- Calendar (section 4) ----------------

        /// <summary>Next box, 0 to 27.</summary>
        public byte CalendarNext;

        /// <summary>Cycle of the next box, from 1.</summary>
        public int CalendarCycle = 1;

        /// <summary>Day key of the last stamp.</summary>
        public int CalendarDayKey;

        /// <summary>Boxes stamped since install.</summary>
        public int CalendarStamps;

        // ---------------- Wishes (section 2) ----------------

        public WishRecord[] Wishes = new WishRecord[MaxWishStock];
        public int WishCount;

        /// <summary>Day key of the last daily draw.</summary>
        public int WishDrawDayKey;

        /// <summary>Day key of the free swap counter, and swaps used that day.</summary>
        public int SwapDayKey;
        public byte SwapsUsed;

        /// <summary>Gems paid by wishes since install (the cap is 9 × days since install).</summary>
        public long WishGemsTotal;

        /// <summary>Wishes completed since install.</summary>
        public int WishesCompleted;

        /// <summary>Templates drawn recently (× 0.3 when drawn in the last 3 days), a ring buffer.</summary>
        public WishHistoryEntry[] History = new WishHistoryEntry[HistorySize];
        public int HistoryCount;
        public int HistoryNext;

        /// <summary>Day number each wish system was first seen open (0: not yet). × 3 during its first 3 days.</summary>
        public int[] SystemUnlockDay = new int[MaxWishSystems];

        /// <summary>Animals already counted by the « touch n different animals » wish.</summary>
        public uint[] TouchedForWish = new uint[TouchedForWishSize];
        public byte TouchedCount;

        // ---------------- Weekly journal (section 5) ----------------

        public int JournalStartKey;
        public int JournalEndKey;
        public bool FirstJournalDone;
        public JournalGoalRecord[] Goals = new JournalGoalRecord[MaxJournalGoals];
        public byte GoalCount;
        public bool JournalCompleteClaimed;

        /// <summary>Goals of the previous journal (× 0.3 in the next draw).</summary>
        public ushort[] PreviousGoals = new ushort[MaxJournalGoals];
        public byte PreviousGoalCount;

        /// <summary>Goals reached but not claimed before Monday: paid at the next opening.</summary>
        public int CarryGems;
        public int CarryTickets;
        public int CarryDiscovery;

        // ---------------- Return gifts (section 6) ----------------

        /// <summary>Day numbers of the last return gift and of the last big one.</summary>
        public int LastReturnGiftDay;
        public int LastBigReturnGiftDay;

        /// <summary>A return gift waiting for its card: 0 none, 1 short (3 to 13 days), 2 big (14 days or more).</summary>
        public byte PendingReturnGift;

        /// <summary>Days away measured for the pending gift (shown in the card).</summary>
        public int PendingReturnDays;

        // ---------------- Bond touches (doc 08 econ.bond.*) ----------------

        public int TouchDayKey;
        public int TouchesToday;
        public Dictionary<uint, byte> TouchesByAnimal { get; } = new Dictionary<uint, byte>(64);
    }

    /// <summary>One pending wish. Its parameters are fixed when drawn and never change afterwards.</summary>
    public struct WishRecord
    {
        public const byte FlagDone = 1;
        public const byte FlagTutorial = 2;

        /// <summary>WishTemplateId value.</summary>
        public ushort Template;

        /// <summary>The animal that expresses the wish (instance id).</summary>
        public uint Animal;

        /// <summary>The template's subject when it names one: a species, a biome, an upgrade (by its biome).</summary>
        public ContentId Param;

        /// <summary>0 none, 1 species, 2 biome, 3 animal (the expressing one), 4 upgrade kind in <see cref="Param"/>'s biome.</summary>
        public byte ParamKind;

        /// <summary>Extra parameter (an upgrade kind) when the template needs one.</summary>
        public byte ParamExtra;

        public int Target;
        public int Progress;

        /// <summary>Day key it was drawn.</summary>
        public int DrawnDay;

        public byte Flags;

        public bool IsDone => (Flags & FlagDone) != 0;
        public bool IsTutorial => (Flags & FlagTutorial) != 0;
    }

    public struct WishHistoryEntry
    {
        public ushort Template;
        public int DayKey;
    }

    public struct JournalGoalRecord
    {
        /// <summary>Goal id (JRN-001 = 1 … JRN-052 = 52).</summary>
        public ushort Goal;

        /// <summary>JournalSlot value.</summary>
        public byte Slot;

        public int Target;
        public int Progress;
        public bool Claimed;

        public bool IsReached => Progress >= Target;
    }

    /// <summary>
    /// T36: what the player holds besides currencies and treats: activity tickets, seeds, ingredients, decorations won
    /// (calendar cycles), stickers waiting for the albums. Extended by T37 (tickets), T43 (Garden), T44 (albums), T56.
    /// </summary>
    public sealed class InventoryState
    {
        /// <summary>Activity tickets won while the activities are not open yet (T37 moves them into its ticket pool).</summary>
        public int Tickets;

        /// <summary>Stickers won before the albums exist (T44 lets the player pick them).</summary>
        public int PendingStickers;

        /// <summary>Counted items by content id: seeds (plant.*), ingredients (ingredient.*), decorations (decor.*).</summary>
        public Dictionary<ContentId, int> Items { get; } = new Dictionary<ContentId, int>();

        public int Count(ContentId item) => Items.TryGetValue(item, out int n) ? n : 0;

        public void Add(ContentId item, int count)
        {
            if (count <= 0 || item == ContentId.None)
                return;
            Items[item] = Count(item) + count;
        }
    }
}
