#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    // T42 « Expéditions » (doc « Mini-jeux & activités », S-ACT-005; doc 01 Contenu, section 7 and S-ACT-005): the contract
    // between the expeditions (simulation: slots, teams, timers, loot drawn at departure, the team rating, the Discovery egg
    // guarantee, the postcards' records) and their presentation (the expeditions screen, the refuge where the team leaves and
    // comes back, the postcard renderer). Timers are absolute UTC times saved in the ExpeditionState: they run while the game
    // is closed, a late return loses nothing, and a clock set back never makes a wait longer than its duration. Frozen for
    // T42: the simulation implements it, the presentation only uses it.

    /// <summary>The four durations (doc 01, section 7.1).</summary>
    public enum ExpeditionDuration : byte
    {
        OneHour = 0,
        FourHours = 1,
        EightHours = 2,
        TwelveHours = 3,
    }

    /// <summary>What a slot shows.</summary>
    public enum ExpeditionSlotState : byte
    {
        /// <summary>Not owned yet: 800 Gems (the third slot) or the Wild Club (the fourth).</summary>
        Locked = 0,

        /// <summary>Owned and free: « Choisis ton équipe ».</summary>
        Empty = 1,

        /// <summary>A team is away: « De retour dans {time} ».</summary>
        Away = 2,

        /// <summary>The team is back and waits, as long as it takes: « Voir la carte postale ».</summary>
        Back = 3,
    }

    /// <summary>How a locked slot opens.</summary>
    public enum ExpeditionSlotUnlock : byte
    {
        None = 0,
        Gems = 1,
        Club = 2,
    }

    public enum ExpeditionStartResult : byte
    {
        Started = 0,
        NotOpen = 1,
        SlotNotEmpty = 2,
        NoAnimal = 3,
        TooManyAnimals = 4,

        /// <summary>An animal is already in another team, or not in the collection.</summary>
        AnimalUnavailable = 5,

        DestinationLocked = 6,
    }

    /// <summary>The rig families as bits (a destination's favourite family; the challenges' constraints).</summary>
    [System.Flags]
    public enum RigFamilyMask : ushort
    {
        None = 0,
        SmallQuadruped = 1 << 0,
        MediumQuadruped = 1 << 1,
        HeavyQuadruped = 1 << 2,
        Bird = 1 << 3,
        SwimmerLow = 1 << 4,
        ClimberPrimate = 1 << 5,
        Hopper = 1 << 6,
    }

    /// <summary>The team rating (doc 01, section 7.2): 10 to 80 points; 1 star under 40, 2 from 40, 3 from 60.</summary>
    public readonly struct ExpeditionRating
    {
        public readonly int Points;
        public readonly byte Stars;

        /// <summary>How many animals come from the destination's biome (+10 each).</summary>
        public readonly byte FromBiome;

        /// <summary>At least one animal of the destination's favourite family (+10).</summary>
        public readonly bool FavoriteFamily;

        /// <summary>Three different species (+10).</summary>
        public readonly bool ThreeSpecies;

        public ExpeditionRating(int points, byte stars, byte fromBiome, bool favoriteFamily, bool threeSpecies)
        {
            Points = points;
            Stars = stars;
            FromBiome = fromBiome;
            FavoriteFamily = favoriteFamily;
            ThreeSpecies = threeSpecies;
        }
    }

    /// <summary>
    /// What a team brings back (section 7.3 with the stars' bonus and the destination's specialty). Before departure it is
    /// the preview (chances as percents, nothing drawn); after the return it is the loot (every draw resolved at departure).
    /// </summary>
    public struct ExpeditionLoot
    {
        /// <summary>Light in minutes of production (paid at the return with the production of that moment).</summary>
        public double LightMinutes;

        public ContentId Treat;
        public int Treats;

        /// <summary>Seeds: up to 4 plants (equal weights over the destination's line, unknown plants removed).</summary>
        public int SeedKinds;

        public ContentId Seed0;
        public ContentId Seed1;
        public ContentId Seed2;
        public ContentId Seed3;
        public int Seeds0;
        public int Seeds1;
        public int Seeds2;
        public int Seeds3;

        /// <summary>Preview: the chance in percent. Loot: 0.</summary>
        public int StickerChance;

        /// <summary>Loot: 1 when a sticker came (its album: the destination's biome). Preview: 0.</summary>
        public int Stickers;

        /// <summary>Preview: the chance in percent. Loot: 0.</summary>
        public int DiscoveryChance;

        /// <summary>Loot: 1 when a Discovery egg came. Preview: 0.</summary>
        public int DiscoveryEggs;

        public int Tickets;

        /// <summary>Bakery ingredients (Bakery open): sweet honey, vanilla cream, flour… up to two kinds.</summary>
        public ContentId Ingredient0;

        public int Ingredients0;
        public ContentId Ingredient1;
        public int Ingredients1;

        /// <summary>Bond points for each animal of the team.</summary>
        public int BondPoints;

        /// <summary>The destination biome's mastery XP.</summary>
        public int MasteryXp;

        /// <summary>Event currency, fixed at departure (none before the big events); converted to Light if the event ended.</summary>
        public int EventCurrency;

        public ContentId SeedAt(int index) => index == 0 ? Seed0 : index == 1 ? Seed1 : index == 2 ? Seed2 : Seed3;

        public int SeedsAt(int index) => index == 0 ? Seeds0 : index == 1 ? Seeds1 : index == 2 ? Seeds2 : Seeds3;
    }

    /// <summary>One destination of the catalogue (doc 01, sections 7.4 and 7.5).</summary>
    public readonly struct ExpeditionDestination
    {
        /// <summary>ContentId of the runtime id (« expedition.flower_meadows »).</summary>
        public readonly ContentId Id;

        /// <summary>The runtime id; its texts are « {Key}.name » (expedition.flower_meadows.name).</summary>
        public readonly string Key;

        /// <summary>The production id (EXP-GL…).</summary>
        public readonly string ProductionId;

        /// <summary>The biome (biome.grassland…); default for an event destination.</summary>
        public readonly ContentId Biome;

        public readonly RigFamilyMask FavoriteFamilies;

        /// <summary>A big event's destination (open only during its 14 days).</summary>
        public readonly bool Event;

        public ExpeditionDestination(ContentId id, string key, string productionId, ContentId biome, RigFamilyMask favoriteFamilies, bool isEvent)
        {
            Id = id;
            Key = key;
            ProductionId = productionId;
            Biome = biome;
            FavoriteFamilies = favoriteFamilies;
            Event = isEvent;
        }
    }

    /// <summary>
    /// A team back home, handed out once by <see cref="IExpeditions.TryCollect"/>: the destination, the team, the stars, the
    /// loot granted, and the postcard to render (its id names the file; the renderer calls
    /// <see cref="IExpeditions.PostcardRendered"/> once it is stored).
    /// </summary>
    public struct ExpeditionReturn
    {
        public int Slot;
        public ContentId Destination;
        public ExpeditionDuration Duration;
        public byte Stars;
        public int TeamSize;
        public uint Animal0;
        public uint Animal1;
        public uint Animal2;

        /// <summary>The variants and species of the team at departure (the postcard poses them even if one changes later).</summary>
        public ContentId Variant0;

        public ContentId Variant1;
        public ContentId Variant2;
        public ContentId Species0;
        public ContentId Species1;
        public ContentId Species2;

        public ExpeditionLoot Loot;

        /// <summary>The Light paid (minutes × the production of the moment).</summary>
        public BigValue Light;

        /// <summary>The postcard's number (its file: postcard_{PostcardId}.png in the postcards folder).</summary>
        public int PostcardId;

        /// <summary>The first postcard ever from this destination (journal memory « memory.postcard », sticker STK-ACT-005-07).</summary>
        public bool FirstFromDestination;

        /// <summary>The account's very first expedition (3 min, the 1 h loot; tutorial A19).</summary>
        public bool FirstStroll;

        /// <summary>A seed for the postcard's composition (poses, butterflies), from the expedition's seed.</summary>
        public uint Seed;

        public uint AnimalAt(int index) => index == 0 ? Animal0 : index == 1 ? Animal1 : Animal2;

        public ContentId VariantAt(int index) => index == 0 ? Variant0 : index == 1 ? Variant1 : Variant2;

        public ContentId SpeciesAt(int index) => index == 0 ? Species0 : index == 1 ? Species1 : Species2;
    }

    /// <summary>
    /// The expeditions as the presentation sees them. Reads never allocate; the screen polls them (timers are computed from
    /// the clock on each read). Slots are numbered 0 to <see cref="SlotCount"/> − 1: two owned, then the Gems slot, then the
    /// Club slot.
    /// </summary>
    public interface IExpeditions
    {
        /// <summary>Open (refuge level 5 and 8 animals; doc 01 A18).</summary>
        bool IsOpen { get; }

        /// <summary>Slots shown, owned or not (4).</summary>
        int SlotCount { get; }

        ExpeditionSlotState StateOf(int slot);

        ExpeditionSlotUnlock UnlockOf(int slot);

        /// <summary>The Gems slot's price (800).</summary>
        long SlotPrice { get; }

        bool TryBuySlot();

        // ---------------- a slot's team ----------------

        ContentId DestinationOf(int slot);

        ExpeditionDuration DurationOf(int slot);

        int TeamSizeOf(int slot);

        uint TeamMember(int slot, int index);

        byte StarsOf(int slot);

        /// <summary>Seconds until the team is back (0 when back), never more than <see cref="TotalSecondsOf"/>.</summary>
        double SecondsLeft(int slot);

        double TotalSecondsOf(int slot);

        /// <summary>Teams back and waiting (the hub card's and the Play button's dot, the toast).</summary>
        int BackCount { get; }

        /// <summary>The soonest return among the teams away, in seconds (−1: none away).</summary>
        double SoonestReturn { get; }

        // ---------------- finish now (doc Gemmes: 1 Gem per 6 minutes left, 5 at least, 3 a day) ----------------

        int SpeedUpCost(int slot);

        int SpeedUpsLeftToday { get; }

        /// <summary>Spends the Gems and brings the return time to now; draws nothing (the loot was drawn at departure).</summary>
        bool TrySpeedUp(int slot);

        // ---------------- destinations ----------------

        int DestinationCount { get; }

        ExpeditionDestination DestinationAt(int index);

        bool TryGetDestination(ContentId id, out ExpeditionDestination destination);

        /// <summary>Open now: its biome is open (biome destinations) or its event is running (event destinations).</summary>
        bool IsDestinationOpen(ContentId destination);

        // ---------------- the team ----------------

        /// <summary>The animal is in a team (away, or back and not welcomed yet): it is not shown in the refuge.</summary>
        bool IsAway(uint instanceId);

        /// <summary>The rating of a team for a destination (the team array is read, not kept).</summary>
        ExpeditionRating Rate(ContentId destination, uint[] team, int count);

        /// <summary>The best rated team among the free animals (« Équipe conseillée »): fills <paramref name="into"/>, returns its size.</summary>
        int SuggestTeam(ContentId destination, uint[] into);

        /// <summary>
        /// The real duration of the next departure: 3 min for the account's first expedition, −25 % during the Expeditions
        /// weekend, else 1, 4, 8 or 12 h.
        /// </summary>
        double DurationSeconds(ExpeditionDuration duration);

        /// <summary>The next expedition is the account's first (« Première balade : 3 min »).</summary>
        bool FirstStrollNext { get; }

        /// <summary>Long expeditions (8 h or more) before the guaranteed Discovery egg (« Œuf Découverte garanti dans {n} longues expéditions »).</summary>
        int DiscoveryGuaranteeIn { get; }

        /// <summary>What a team of this rating would bring back (chances in percent, nothing drawn).</summary>
        void Preview(ContentId destination, ExpeditionDuration duration, byte stars, out ExpeditionLoot preview);

        /// <summary>The team leaves: every draw of the loot happens now (saved); the animals leave the refuge.</summary>
        ExpeditionStartResult Start(int slot, ContentId destination, ExpeditionDuration duration, uint[] team, int count);

        // ---------------- challenges (doc 01 S-ACT-005 « Défis »: stars from the team rating's points) ----------------

        /// <summary>10.</summary>
        int ChallengeCount { get; }

        /// <summary>Its destination can be picked now (« vers les Prés fleuris » needs that destination open; « où l'on veut » any).</summary>
        bool IsChallengeOpen(int challenge);

        /// <summary>The fixed duration of a challenge.</summary>
        ExpeditionDuration ChallengeDuration(int challenge);

        /// <summary>The destination is allowed by the challenge (a named destination, any biome destination, the newest biome's).</summary>
        bool ChallengeAllows(int challenge, ContentId destination);

        /// <summary>The team meets the challenge's constraint (3 animals, 3 SmallQuadruped, at least one bird…).</summary>
        bool ChallengeAccepts(int challenge, ContentId destination, uint[] team, int count);

        /// <summary>Rating points needed for 1, 2 and 3 stars (<paramref name="stars"/> 1 to 3).</summary>
        int ChallengeThreshold(int challenge, int stars);

        /// <summary>The best stars reached in a challenge (saved with the activities' challenge stars).</summary>
        byte ChallengeStars(int challenge);

        /// <summary>Like <see cref="Start"/> with the challenge's duration; the challenge's stars are recorded at departure.</summary>
        ExpeditionStartResult StartChallenge(int slot, int challenge, ContentId destination, uint[] team, int count);

        // ---------------- the return ----------------

        /// <summary>
        /// A team back is welcomed: the loot is granted (Light at the production of now), the animals are free again, the
        /// slot is empty, a postcard record is kept. False when the slot is not <see cref="ExpeditionSlotState.Back"/>.
        /// </summary>
        bool TryCollect(int slot, out ExpeditionReturn welcome);

        /// <summary>The renderer stored the postcard's picture (the record keeps it for the refuge journal, T44).</summary>
        void PostcardRendered(int postcardId);

        /// <summary>Postcards kept (the first of each destination for ever, then the most recent): their ids, newest first.</summary>
        int PostcardCount { get; }

        int PostcardAt(int index);

        /// <summary>Raised on the main thread when a team leaves, comes back or is welcomed (the refuge, the hub card).</summary>
        event System.Action<int>? SlotChanged;
    }
}
