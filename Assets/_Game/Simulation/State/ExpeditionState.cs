#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T42 « Expéditions » (doc 01 Contenu, section 7): everything the expeditions remember. Plain data, fixed-size arrays
    /// allocated once. Timers are absolute UTC times (unix ms): they run while the game is closed, a team back waits for
    /// ever, and nothing here is lost by a late return. Every random part of a team's loot is drawn at departure and kept in
    /// its slot (decision of 7 October): the welcome only shows it.
    /// </summary>
    public sealed class ExpeditionState
    {
        /// <summary>Slots the save can hold (2 free by default, then the Gems slot, then the Club slot; Remote Config may add).</summary>
        public const int MaxSlots = 6;

        public const int MaxTeam = 3;

        /// <summary>The most recent postcards kept besides the first of each destination (kept for ever).</summary>
        public const int RecentPostcards = 24;

        /// <summary>Room for the first postcard of each of the 37 destinations and the 24 most recent others.</summary>
        public const int MaxPostcards = 64;

        /// <summary>The Gems slot was bought (800 Gems, permanent).</summary>
        public bool SlotBought;

        /// <summary>The account's first expedition (« Première balade : 3 min », A18) has left.</summary>
        public bool FirstStrollDone;

        /// <summary>Long expeditions (8 h or more) left since the last Discovery egg of an expedition (the guarantee, 12).</summary>
        public int LongWithoutEgg;

        /// <summary>Expeditions started since install (each one's seed is derived from it: an app kill never rerolls).</summary>
        public int Started;

        /// <summary>Expeditions welcomed since install.</summary>
        public int Completed;

        /// <summary>Day key of the speed-ups counter, and the speed-ups of that day (3 a day at most).</summary>
        public int SpeedUpDayKey;

        public int SpeedUpsToday;

        /// <summary>The number the next postcard takes (its file: postcard_{id}.png).</summary>
        public int NextPostcardId = 1;

        /// <summary>The journal week the destinations below belong to (its start key), for JRN-026.</summary>
        public int JournalWeekKey;

        /// <summary>Destinations a postcard came from during that journal week: one bit per catalogue index.</summary>
        public ulong JournalPlacesMask;

        public ExpeditionSlotRecord[] Slots = new ExpeditionSlotRecord[MaxSlots];

        /// <summary>Postcards kept, oldest first: the first of each destination for ever, then the 24 most recent others.</summary>
        public PostcardRecord[] Postcards = new PostcardRecord[MaxPostcards];

        public int PostcardCount;

        /// <summary>The animal is in a team (away, or back and not welcomed yet). No allocation.</summary>
        public bool IsAway(uint instanceId)
        {
            if (instanceId == 0)
                return false;
            var slots = Slots;
            for (int s = 0; s < slots.Length; s++)
            {
                ref readonly var slot = ref slots[s];
                if (!slot.Active)
                    continue;
                int size = slot.TeamSize > MaxTeam ? MaxTeam : slot.TeamSize;
                for (int i = 0; i < size; i++)
                    if (slot.AnimalAt(i) == instanceId)
                        return true;
            }
            return false;
        }

        /// <summary>A free slot among the first <paramref name="ownedSlots"/> (no team in it).</summary>
        public bool HasFreeSlot(int ownedSlots)
        {
            int n = ownedSlots < Slots.Length ? ownedSlots : Slots.Length;
            for (int s = 0; s < n; s++)
                if (!Slots[s].Active)
                    return true;
            return false;
        }

        /// <summary>A team is in a slot (away or back).</summary>
        public bool AnyActive
        {
            get
            {
                for (int s = 0; s < Slots.Length; s++)
                    if (Slots[s].Active)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Keeps a new postcard: the first of its destination stays for ever; of the others, only the 24 most recent stay
        /// (the oldest goes). No allocation.
        /// </summary>
        public void AddPostcard(in PostcardRecord record)
        {
            if (PostcardCount >= Postcards.Length)
                RemoveOldestRecent();
            if (PostcardCount >= Postcards.Length)
                return;
            Postcards[PostcardCount++] = record;
            int recent = 0;
            for (int i = 0; i < PostcardCount; i++)
                if (!Postcards[i].FirstOfDestination)
                    recent++;
            while (recent > RecentPostcards && RemoveOldestRecent())
                recent--;
        }

        /// <summary>A postcard of this destination is kept as its first (never removed).</summary>
        public bool HasFirstPostcard(ContentId destination)
        {
            for (int i = 0; i < PostcardCount; i++)
                if (Postcards[i].FirstOfDestination && Postcards[i].Destination == destination)
                    return true;
            return false;
        }

        private bool RemoveOldestRecent()
        {
            for (int i = 0; i < PostcardCount; i++)
            {
                if (Postcards[i].FirstOfDestination)
                    continue;
                for (int j = i + 1; j < PostcardCount; j++)
                    Postcards[j - 1] = Postcards[j];
                PostcardCount--;
                Postcards[PostcardCount] = default;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// One slot's team (doc 01 Contenu, section 7.1): the animals with their variants and species at departure, the
    /// destination, the duration chosen, the start and return times (UTC unix ms), the stars of the team rating, the
    /// challenge (−1: none), the seed and the loot drawn at departure. An empty record is a free slot.
    /// </summary>
    public struct ExpeditionSlotRecord
    {
        public bool Active;
        public ContentId Destination;
        public ExpeditionDuration Duration;
        public byte TeamSize;
        public uint Animal0;
        public uint Animal1;
        public uint Animal2;
        public ContentId Variant0;
        public ContentId Variant1;
        public ContentId Variant2;
        public ContentId Species0;
        public ContentId Species1;
        public ContentId Species2;
        public long StartUnixMs;
        public long ReturnUnixMs;
        public byte Stars;

        /// <summary>The challenge played (0 to 9), −1 for a free expedition.</summary>
        public sbyte Challenge;

        /// <summary>The account's first expedition (3 min, the 1 h loot).</summary>
        public bool FirstStroll;

        public uint Seed;

        /// <summary>Every draw resolved at departure; Light in minutes of production (paid at the welcome).</summary>
        public ExpeditionLoot Loot;

        public uint AnimalAt(int index) => index == 0 ? Animal0 : index == 1 ? Animal1 : Animal2;

        public ContentId VariantAt(int index) => index == 0 ? Variant0 : index == 1 ? Variant1 : Variant2;

        public ContentId SpeciesAt(int index) => index == 0 ? Species0 : index == 1 ? Species1 : Species2;

        public void SetMember(int index, uint animal, ContentId variant, ContentId species)
        {
            switch (index)
            {
                case 0:
                    Animal0 = animal;
                    Variant0 = variant;
                    Species0 = species;
                    break;
                case 1:
                    Animal1 = animal;
                    Variant1 = variant;
                    Species1 = species;
                    break;
                case 2:
                    Animal2 = animal;
                    Variant2 = variant;
                    Species2 = species;
                    break;
            }
        }
    }

    /// <summary>A postcard kept (doc 01 Contenu, section 7.1): its number, destination, day, picture stored, first of its destination.</summary>
    public struct PostcardRecord
    {
        public int Id;
        public ContentId Destination;

        /// <summary>Days since the unix epoch (UTC) of the welcome.</summary>
        public int UnixDay;

        /// <summary>The renderer stored its picture (postcard_{Id}.png).</summary>
        public bool Rendered;

        /// <summary>The first postcard from its destination: kept for ever.</summary>
        public bool FirstOfDestination;
    }
}
