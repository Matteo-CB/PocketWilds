#nullable enable
using System.Collections.Generic;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>One quest of an event instance (daily or long): its template number, goal, progress and target at draw time.</summary>
    public struct EventQuestRecord
    {
        /// <summary>The template's number in its pool (EQ-D-07: 7). A number this build does not know is dropped at load.</summary>
        public byte Template;

        public byte Group;
        public byte Goal;
        public int Progress;
        public int Target;

        /// <summary>Day key it was drawn (daily quests); 0 for long quests.</summary>
        public int Day;

        public bool Done;

        /// <summary>
        /// What the goal needs beside its count: the activity of the day (EQ-D-22, an ActivityId value), the mask of the activities
        /// or biomes already counted (EQ-D-15, EQ-D-23), the variants discovered at the draw (EQ-L-19); 0 otherwise.
        /// </summary>
        public int Param;
    }

    /// <summary>
    /// T48: one player's run of one event instance (« event.&lt;key&gt;.&lt;year&gt; »): the parameters frozen at the first entry,
    /// the currency (spent) and the track points (never lowered), the tiers claimed, the egg's counters and seed, the shop
    /// purchases, the quests, the challenges, the stickers still owed while the album is closed, the Souvenir's date and the
    /// end (ended once, converted to Light once, summary seen). An instance whose event is not in this build is kept as it is.
    /// </summary>
    public sealed class EventInstanceState
    {
        public string InstanceId = string.Empty;
        public string Key = string.Empty;
        public int Version;
        public int Year;
        public long StartUnix;
        public long EndUnix;
        public long JoinedUnix;

        /// <summary>The event's numbers at the first entry (mid-instance changes only ever make them more generous).</summary>
        public EventParams Frozen = new EventParams();

        /// <summary>Currency in hand (spent on eggs and the shop; the leftover becomes Light at the end).</summary>
        public int Currency;

        /// <summary>Currency earned in total (refunds included): the achievement and the summary.</summary>
        public long CurrencyEarned;

        /// <summary>Track points: the currency earned from play (+ the Carnet d'or bonus); spending never lowers them.</summary>
        public int Points;

        /// <summary>Highest tier already announced (stickers, analytics, Souvenir).</summary>
        public int TierReached;

        /// <summary>Bit t − 1 = tier t claimed (free line, gold line; up to 36 tiers).</summary>
        public ulong FreeClaimed;
        public ulong GoldClaimed;

        // ---- The egg (deterministic, counters saved) ----
        public int EggsOpened;
        public int EggMisses;
        public byte EggItems;
        public bool SecretOwned;
        public uint EggRng;

        /// <summary>Shop and Memory Shop lines bought: line key hash to count.</summary>
        public Dictionary<uint, int> Purchases { get; } = new Dictionary<uint, int>();

        public List<EventQuestRecord> Daily { get; } = new List<EventQuestRecord>();
        public List<EventQuestRecord> Long { get; } = new List<EventQuestRecord>();

        /// <summary>Last day key daily quests were drawn; the day key and count of the free swaps used.</summary>
        public int QuestDay;
        public int SwapDay;
        public int SwapsUsed;
        public int LongDone;

        /// <summary>Best stars (0 to 3) of each of the 10 challenges.</summary>
        public byte[] ChallengeStars = new byte[10];

        /// <summary>Bits: sticker at 9, 18, 27 stars, outfit 3 at 30 stars.</summary>
        public byte ChallengeMilestones;

        /// <summary>Album stickers earned while the album was closed (bits: track 1, 6, 11, 19, 23, 26; stars 9, 18, 27; long quests 4, 7, 10).</summary>
        public ushort StickersOwed;

        /// <summary>Unix day the Souvenir was given (its date); 0 before.</summary>
        public int SouvenirUnixDay;

        public bool GoalReached;

        public bool Ended;
        public bool Converted;
        public int ConvertedCurrency;
        public double ConvertedSeconds;
        public BigValue ConvertedLight;
        public bool SummarySeen;

        public int ClaimedCount(bool gold)
        {
            ulong bits = gold ? GoldClaimed : FreeClaimed;
            int count = 0;
            while (bits != 0)
            {
                bits &= bits - 1;
                count++;
            }
            return count;
        }
    }

    /// <summary>T48: every event instance the profile entered, and the latest time the event systems have seen (a clock moved back reopens nothing).</summary>
    public sealed class EventState
    {
        public List<EventInstanceState> Instances { get; } = new List<EventInstanceState>();

        /// <summary>Highest Unix second the event systems have seen.</summary>
        public long LastSeenUnix;

        public EventInstanceState? Find(string instanceId)
        {
            for (int i = 0; i < Instances.Count; i++)
                if (Instances[i].InstanceId == instanceId)
                    return Instances[i];
            return null;
        }

        /// <summary>True when an earlier instance of this key was entered (a returning event's Memory Shop and « already yours »).</summary>
        public bool HasEarlier(string key, int year)
        {
            for (int i = 0; i < Instances.Count; i++)
                if (Instances[i].Key == key && Instances[i].Year < year)
                    return true;
            return false;
        }
    }
}
