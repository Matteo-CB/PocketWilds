#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Content
{
    /// <summary>What one event reward gives (T48). Stable byte values.</summary>
    public enum EventRewardKind : byte
    {
        None = 0,
        Gems = 1,
        Tickets = 2,
        Treats = 3,
        DiscoveryEgg = 4,
        LightMinutes = 5,

        /// <summary>The event page's fixed sticker of a track tier (<see cref="EventReward.Amount"/> = the tier value).</summary>
        Sticker = 6,

        Decoration = 7,
        Outfit = 8,

        /// <summary>A variant given as an animal (the featured species' v1, the Prestige v5, egg items).</summary>
        Variant = 9,

        /// <summary>A profile frame (<see cref="EventReward.Key"/> its profile key; an item when the profile does not know it).</summary>
        Frame = 10,
        HatchEffect = 11,
        Monument = 12,

        /// <summary>The dated Souvenir decoration, given to every player when the Souvenir tier is reached.</summary>
        Souvenir = 13,
        Pose = 14,
    }

    /// <summary>One reward of a track tier, a shop line or the Memory Shop. Plain data.</summary>
    public readonly struct EventReward
    {
        public readonly EventRewardKind Kind;

        /// <summary>Count of Gems, tickets, treats, eggs, Light minutes; the tier value of a sticker.</summary>
        public readonly int Amount;

        /// <summary>The item (entitlement, variant, treat or decoration); None for counts.</summary>
        public readonly ContentId Item;

        /// <summary>Profile key of a frame or pose (« frame.xxx »); empty otherwise.</summary>
        public readonly string Key;

        public EventReward(EventRewardKind kind, int amount, ContentId item, string key = "")
        {
            Kind = kind;
            Amount = amount;
            Item = item;
            Key = key ?? string.Empty;
        }

        /// <summary>A permanent item (owned once; a returning edition gives 100 Gems instead of an item already owned).</summary>
        public bool IsItem => Kind == EventRewardKind.Decoration || Kind == EventRewardKind.Outfit || Kind == EventRewardKind.Variant
                              || Kind == EventRewardKind.Frame || Kind == EventRewardKind.HatchEffect || Kind == EventRewardKind.Monument
                              || Kind == EventRewardKind.Souvenir || Kind == EventRewardKind.Pose;

        public static EventReward Gems(int amount) => new EventReward(EventRewardKind.Gems, amount, ContentId.None);
        public static EventReward Tickets(int amount) => new EventReward(EventRewardKind.Tickets, amount, ContentId.None);
        public static EventReward Treats(int amount, ContentId treat = default) => new EventReward(EventRewardKind.Treats, amount, treat);
        public static EventReward DiscoveryEgg(int amount = 1) => new EventReward(EventRewardKind.DiscoveryEgg, amount, ContentId.None);
        public static EventReward LightMinutes(int minutes) => new EventReward(EventRewardKind.LightMinutes, minutes, ContentId.None);
        public static EventReward Sticker(int tier) => new EventReward(EventRewardKind.Sticker, tier, ContentId.None);
        public static EventReward Of(EventRewardKind kind, string item, string key = "") => new EventReward(kind, 1, ContentId.FromSource(item), key);
    }

    /// <summary>One tier of the track: its free line and its Carnet d'or line (the points it needs come from the event's parameters).</summary>
    public sealed class EventTier
    {
        public EventTier(int number, EventReward[] free, EventReward[] gold)
        {
            Number = number;
            Free = free ?? Array.Empty<EventReward>();
            Gold = gold ?? Array.Empty<EventReward>();
        }

        public int Number { get; }
        public EventReward[] Free { get; }
        public EventReward[] Gold { get; }
    }

    /// <summary>One of the 7 items of the event egg: a variant of the featured species (v2, v3, v4) or an event variant of an existing species.</summary>
    public readonly struct EventEggItem
    {
        public readonly ContentId Variant;
        public readonly bool Featured;

        public EventEggItem(ContentId variant, bool featured)
        {
            Variant = variant;
            Featured = featured;
        }
    }

    /// <summary>Which parameter prices a shop or Memory Shop line (the numbers live in the event's frozen parameters).</summary>
    public enum EventPriceSlot : byte
    {
        Egg = 0,
        Outfit = 1,
        Decoration = 2,
        Decoration7 = 3,
        Decoration8 = 4,
        Tickets = 5,
        Sticker = 6,
        Seeds = 7,
        MemoryVariant = 8,
        MemoryFrame = 9,
        MemoryPose = 10,
    }

    /// <summary>How a shop line is limited: the egg never, an item once, tickets and seeds by count, a sticker once per missing sticker.</summary>
    public enum EventShopKind : byte
    {
        Egg = 0,
        Item = 1,
        Tickets = 2,
        Sticker = 3,
        Seeds = 4,
    }

    /// <summary>One line of the event shop (event currency, fixed prices).</summary>
    public sealed class EventShopItem
    {
        public EventShopItem(string key, EventShopKind kind, EventPriceSlot price, EventReward reward)
        {
            Key = key;
            KeyId = ContentIdHash.Compute(key);
            Kind = kind;
            Price = price;
            Reward = reward;
        }

        /// <summary>Stable line key (« shop.outfit_4 »): its purchases are saved under its hash.</summary>
        public string Key { get; }
        public uint KeyId { get; }
        public EventShopKind Kind { get; }
        public EventPriceSlot Price { get; }
        public EventReward Reward { get; }
    }

    /// <summary>One line of the Memory Shop of a returning event: price = the base slot × 1.25 rounded up to 50.</summary>
    public sealed class EventMemoryItem
    {
        public EventMemoryItem(string key, EventPriceSlot basePrice, EventReward reward)
        {
            Key = key;
            KeyId = ContentIdHash.Compute(key);
            BasePrice = basePrice;
            Reward = reward;
        }

        public string Key { get; }
        public uint KeyId { get; }
        public EventPriceSlot BasePrice { get; }
        public EventReward Reward { get; }
    }

    /// <summary>The three groups of daily quests (one of each a day). Long quests use them to replace a locked feature's quest.</summary>
    public enum EventQuestGroup : byte
    {
        Featured = 0,
        RendezVous = 1,
        Refuge = 2,
    }

    /// <summary>
    /// What a quest counts. Stable byte values (saved with the quests): new kinds are appended, never renumbered. Values without a
    /// template of the doc (3, 4, 14, 15) stay readable for old saves and are counted by nothing.
    /// </summary>
    public enum EventGoalKind : byte
    {
        HatchEggs = 0,
        OpenEventEggs = 1,

        /// <summary>Rewarded rounds of the quest activity: the featured activity, or its fallback when the player cannot play it.</summary>
        FeaturedRounds = 2,
        FeaturedStars = 3,
        FeaturedThreeStars = 4,
        ActivityRounds = 5,
        Wishes = 6,

        /// <summary>Expeditions welcomed back (the return, rewards taken).</summary>
        Expeditions = 7,
        GardenHarvests = 8,
        TreatsGiven = 9,

        /// <summary>Counted touches (inside the bond's daily caps, like wishes and the journal).</summary>
        BondTouches = 10,
        DailyGift = 11,
        EarnCurrency = 12,

        /// <summary>New challenge stars: the event's featured challenges, activity and expedition challenges.</summary>
        ChallengeStars = 13,
        StickersGained = 14,
        ShopPurchases = 15,

        /// <summary>Stars of rewarded activity rounds, any activity.</summary>
        ActivityStars = 16,
        ExpeditionStarts = 17,
        GroomingRounds = 18,
        GardenPlants = 19,
        GardenWaterings = 20,

        /// <summary>No decoration placing in the game yet: its templates are gated by <see cref="EventFeatureGate.Decorations"/>.</summary>
        DecorationsPlaced = 21,

        /// <summary>No photo mode yet (<see cref="EventFeatureGate.PhotoMode"/>).</summary>
        PhotosTaken = 22,

        /// <summary>Different activities played since the quest was drawn (the record's Param holds their mask).</summary>
        DistinctActivities = 23,

        /// <summary>Light collected on a return (the offline Light taken).</summary>
        LightCollections = 24,
        FocusHatches = 25,

        /// <summary>Animals hatched whose species lives in the player's most advanced open biome.</summary>
        NewestBiomeHatches = 26,

        /// <summary>Rewarded rounds with 3 stars, any activity.</summary>
        ThreeStarRounds = 27,

        /// <summary>Rewarded rounds of the activity of the day drawn with the quest (the record's Param, an ActivityId value).</summary>
        DayActivityRounds = 28,

        /// <summary>Different biomes visited since the quest was drawn (the record's Param holds their mask).</summary>
        BiomeVisits = 29,
        AccessoriesEquipped = 30,

        /// <summary>Rewarded rounds of the quest activity with 2 stars or more.</summary>
        FeaturedTwoStarRounds = 31,
        UpgradesBought = 32,

        /// <summary>No breeding yet (<see cref="EventFeatureGate.Breeding"/>).</summary>
        BreedingsStarted = 33,

        /// <summary>Expeditions started for 1 h or more.</summary>
        LongExpeditionStarts = 34,

        /// <summary>The event track's tier reached (absolute).</summary>
        TrackTier = 35,

        /// <summary>New items owned from the event egg (the 7 items and the secret, absolute).</summary>
        EventEggItems = 36,
        WeeklyJournalGoals = 37,

        /// <summary>Variants discovered since the quest was drawn (the record's Param holds the count at the draw).</summary>
        NewVariants = 38,
    }

    /// <summary>
    /// The feature a quest needs open (a locked feature's quest is never drawn). Stable byte values, appended only. Decorations,
    /// PhotoMode and Breeding are features the game does not have yet: always locked until they ship.
    /// </summary>
    public enum EventFeatureGate : byte
    {
        None = 0,

        /// <summary>At least one activity of the runner can be played.</summary>
        Activities = 1,
        Expeditions = 2,
        Garden = 3,
        Albums = 4,
        Bond = 5,
        Grooming = 6,
        PhotoMode = 7,
        Decorations = 8,
        Breeding = 9,
        FocusEgg = 10,

        /// <summary>At least 3 activities of the runner can be played.</summary>
        ThreeActivities = 11,

        /// <summary>The event's featured challenges still have stars to win.</summary>
        ChallengeStarsLeft = 12,

        /// <summary>The player owns at least the quest's target in treats.</summary>
        TreatsOwned = 13,

        /// <summary>The player owns at least one accessory.</summary>
        AccessoryOwned = 14,

        /// <summary>The player can play the featured activity or its fallback.</summary>
        FeaturedActivity = 15,
    }

    /// <summary>A daily (EQ-D-01 to 30) or long (EQ-L-01 to 20) quest template.</summary>
    public sealed class EventQuestTemplate
    {
        public EventQuestTemplate(string code, EventQuestGroup group, EventGoalKind goal, int target, EventFeatureGate gate = EventFeatureGate.None,
            int fromDay = 1)
        {
            Code = code;
            Number = int.Parse(code.Substring(code.Length - 2));
            Group = group;
            Goal = goal;
            Target = Math.Max(1, target);
            Gate = gate;
            FromDay = Math.Max(1, fromDay);
        }

        /// <summary>« EQ-D-01 », « EQ-L-07 ».</summary>
        public string Code { get; }

        /// <summary>The number in its pool (1 to 30 or 1 to 20), saved with the quest.</summary>
        public int Number { get; }

        public EventQuestGroup Group { get; }
        public EventGoalKind Goal { get; }

        /// <summary>The doc's target (EQ-D-23 « visit 3 biomes » is lowered to the open biomes when they are fewer: read the record's).</summary>
        public int Target { get; }
        public EventFeatureGate Gate { get; }

        /// <summary>First event day it may be drawn (EQ-D-02 « open 1 event egg » from day 2).</summary>
        public int FromDay { get; }

        /// <summary>Text key of its line (« event.quest.eq_d_01 »); EQ-D-23 also has « .one » for a target of 1.</summary>
        public string TextKey => "event.quest." + Code.ToLowerInvariant().Replace('-', '_');
    }

    /// <summary>
    /// The shared quest pools of every event (doc Événements, quests): 30 daily templates in three groups (Activité vedette,
    /// Rendez-vous, Refuge) and 20 long ones. The doc gives the long templates no group: each takes the group of its daily
    /// equivalent, for the rule « a locked feature's quest is replaced by another of its group ». Targets and conditions are the
    /// doc's; a casual player reaches every long target in 14 days.
    /// </summary>
    public static class EventQuestCatalog
    {
        private const EventQuestGroup F = EventQuestGroup.Featured;
        private const EventQuestGroup R = EventQuestGroup.RendezVous;
        private const EventQuestGroup H = EventQuestGroup.Refuge;

        /// <summary>The last goal this build counts (a saved quest with a later goal is dropped at load).</summary>
        public const EventGoalKind LastGoal = EventGoalKind.NewVariants;

        public static readonly EventQuestTemplate[] Daily =
        {
            new EventQuestTemplate("EQ-D-01", H, EventGoalKind.HatchEggs, 10),
            new EventQuestTemplate("EQ-D-02", F, EventGoalKind.OpenEventEggs, 1, fromDay: 2),
            new EventQuestTemplate("EQ-D-03", F, EventGoalKind.FeaturedRounds, 2, EventFeatureGate.FeaturedActivity),
            new EventQuestTemplate("EQ-D-04", F, EventGoalKind.ActivityStars, 5, EventFeatureGate.Activities),
            new EventQuestTemplate("EQ-D-05", R, EventGoalKind.Wishes, 2),
            new EventQuestTemplate("EQ-D-06", H, EventGoalKind.ExpeditionStarts, 1, EventFeatureGate.Expeditions),
            new EventQuestTemplate("EQ-D-07", H, EventGoalKind.Expeditions, 1, EventFeatureGate.Expeditions),
            new EventQuestTemplate("EQ-D-08", H, EventGoalKind.GroomingRounds, 3, EventFeatureGate.Grooming),
            new EventQuestTemplate("EQ-D-09", H, EventGoalKind.GardenHarvests, 2, EventFeatureGate.Garden),
            new EventQuestTemplate("EQ-D-10", H, EventGoalKind.GardenPlants, 3, EventFeatureGate.Garden),
            new EventQuestTemplate("EQ-D-11", H, EventGoalKind.DecorationsPlaced, 1, EventFeatureGate.Decorations),
            new EventQuestTemplate("EQ-D-12", H, EventGoalKind.PhotosTaken, 1, EventFeatureGate.PhotoMode),
            new EventQuestTemplate("EQ-D-13", R, EventGoalKind.BondTouches, 10),
            new EventQuestTemplate("EQ-D-14", R, EventGoalKind.DailyGift, 1),
            new EventQuestTemplate("EQ-D-15", F, EventGoalKind.DistinctActivities, 3, EventFeatureGate.ThreeActivities),
            new EventQuestTemplate("EQ-D-16", F, EventGoalKind.ChallengeStars, 1, EventFeatureGate.ChallengeStarsLeft),
            new EventQuestTemplate("EQ-D-17", R, EventGoalKind.LightCollections, 5),
            new EventQuestTemplate("EQ-D-18", H, EventGoalKind.FocusHatches, 1, EventFeatureGate.FocusEgg),
            new EventQuestTemplate("EQ-D-19", R, EventGoalKind.TreatsGiven, 3, EventFeatureGate.TreatsOwned),
            new EventQuestTemplate("EQ-D-20", H, EventGoalKind.NewestBiomeHatches, 5),
            new EventQuestTemplate("EQ-D-21", F, EventGoalKind.ThreeStarRounds, 1, EventFeatureGate.Activities),
            new EventQuestTemplate("EQ-D-22", F, EventGoalKind.DayActivityRounds, 1, EventFeatureGate.Activities),
            new EventQuestTemplate("EQ-D-23", R, EventGoalKind.BiomeVisits, 3),
            new EventQuestTemplate("EQ-D-24", H, EventGoalKind.AccessoriesEquipped, 1, EventFeatureGate.AccessoryOwned),
            new EventQuestTemplate("EQ-D-25", R, EventGoalKind.EarnCurrency, 200),
            new EventQuestTemplate("EQ-D-26", F, EventGoalKind.FeaturedTwoStarRounds, 1, EventFeatureGate.FeaturedActivity),
            new EventQuestTemplate("EQ-D-27", H, EventGoalKind.UpgradesBought, 1),
            new EventQuestTemplate("EQ-D-28", H, EventGoalKind.BreedingsStarted, 1, EventFeatureGate.Breeding),
            new EventQuestTemplate("EQ-D-29", H, EventGoalKind.GardenWaterings, 2, EventFeatureGate.Garden),
            new EventQuestTemplate("EQ-D-30", H, EventGoalKind.LongExpeditionStarts, 2, EventFeatureGate.Expeditions),
        };

        public static readonly EventQuestTemplate[] Long =
        {
            new EventQuestTemplate("EQ-L-01", H, EventGoalKind.HatchEggs, 150),
            new EventQuestTemplate("EQ-L-02", F, EventGoalKind.ActivityStars, 60, EventFeatureGate.Activities),
            new EventQuestTemplate("EQ-L-03", F, EventGoalKind.FeaturedRounds, 15, EventFeatureGate.FeaturedActivity),
            new EventQuestTemplate("EQ-L-04", R, EventGoalKind.Wishes, 20),
            new EventQuestTemplate("EQ-L-05", H, EventGoalKind.Expeditions, 8, EventFeatureGate.Expeditions),
            new EventQuestTemplate("EQ-L-06", F, EventGoalKind.OpenEventEggs, 8),
            new EventQuestTemplate("EQ-L-07", F, EventGoalKind.ChallengeStars, 12),
            new EventQuestTemplate("EQ-L-08", R, EventGoalKind.TrackTier, 15),
            new EventQuestTemplate("EQ-L-09", H, EventGoalKind.GroomingRounds, 25, EventFeatureGate.Grooming),
            new EventQuestTemplate("EQ-L-10", H, EventGoalKind.GardenHarvests, 15, EventFeatureGate.Garden),
            new EventQuestTemplate("EQ-L-11", H, EventGoalKind.DecorationsPlaced, 3, EventFeatureGate.Decorations),
            new EventQuestTemplate("EQ-L-12", H, EventGoalKind.PhotosTaken, 8, EventFeatureGate.PhotoMode),
            new EventQuestTemplate("EQ-L-13", R, EventGoalKind.BondTouches, 150),
            new EventQuestTemplate("EQ-L-14", F, EventGoalKind.EventEggItems, 3),
            new EventQuestTemplate("EQ-L-15", R, EventGoalKind.EarnCurrency, 3000),
            new EventQuestTemplate("EQ-L-16", R, EventGoalKind.WeeklyJournalGoals, 10),
            new EventQuestTemplate("EQ-L-17", R, EventGoalKind.DailyGift, 7),
            new EventQuestTemplate("EQ-L-18", F, EventGoalKind.ActivityRounds, 30, EventFeatureGate.Activities),
            new EventQuestTemplate("EQ-L-19", H, EventGoalKind.NewVariants, 3),
            new EventQuestTemplate("EQ-L-20", R, EventGoalKind.TreatsGiven, 30),
        };

        /// <summary>Features the game does not have yet: their templates are never drawn (EQ-D-11, 12, 28; EQ-L-11, 12).</summary>
        public static bool AlwaysLocked(EventFeatureGate gate) =>
            gate == EventFeatureGate.Decorations || gate == EventFeatureGate.PhotoMode || gate == EventFeatureGate.Breeding;
    }

    /// <summary>
    /// One big event (doc Événements, T48): key « event.&lt;key&gt; », its version (a returning event keeps its key, gets a new
    /// version and a new instance « event.&lt;key&gt;.&lt;year&gt; »), featured species and activities, currency, 30 tiers (free and
    /// Carnet d'or lines) plus the returning-year bonus tiers, the egg (7 items and the secret variant), the shop, the Memory Shop,
    /// the quest pools, the 10 challenges, the album page, the Souvenir and the Box items. Dates come from the calendar; numbers
    /// (tier points, prices, guarantees) from the event parameters frozen at a player's first entry. Activity values are
    /// <c>ActivityId</c> numbers (the content assembly does not know the enum).
    /// </summary>
    public sealed class EventDefinition : IContentDefinition
    {
        public const int TierCount = 30;
        public const int EggItemCount = 7;
        public const int ChallengeCount = 10;

        public EventDefinition(string key, int version, string nameKey, string currencyKey, ContentId featuredSpecies, int[] featuredActivities,
            int fallbackActivity, int albumEventNumber, EventTier[] tiers, EventTier[] bonusTiers, EventEggItem[] eggItems, ContentId secretVariant,
            EventShopItem[] shop, EventMemoryItem[] memory, EventQuestTemplate[] dailyQuests, EventQuestTemplate[] longQuests, int[] challengeLevels,
            ContentId challengeOutfit, ContentId souvenir, ContentId[] boxItems, EventReward[] headline, string[] themeKeys, string currencyIconKey = "",
            uint[]? themeColors = null)
        {
            Key = key;
            Id = ContentId.FromSource(key);
            Version = Math.Max(1, version);
            NameKey = nameKey;
            CurrencyKey = currencyKey;
            FeaturedSpecies = featuredSpecies;
            FeaturedActivities = featuredActivities ?? Array.Empty<int>();
            FallbackActivity = fallbackActivity;
            AlbumEventNumber = albumEventNumber;
            Tiers = tiers ?? Array.Empty<EventTier>();
            BonusTiers = bonusTiers ?? Array.Empty<EventTier>();
            EggItems = eggItems ?? Array.Empty<EventEggItem>();
            SecretVariant = secretVariant;
            Shop = shop ?? Array.Empty<EventShopItem>();
            Memory = memory ?? Array.Empty<EventMemoryItem>();
            DailyQuests = dailyQuests ?? Array.Empty<EventQuestTemplate>();
            LongQuests = longQuests ?? Array.Empty<EventQuestTemplate>();
            ChallengeLevels = challengeLevels ?? Array.Empty<int>();
            ChallengeOutfit = challengeOutfit;
            Souvenir = souvenir;
            BoxItems = boxItems ?? Array.Empty<ContentId>();
            Headline = headline ?? Array.Empty<EventReward>();
            ThemeKeys = themeKeys ?? Array.Empty<string>();
            CurrencyIconKey = currencyIconKey ?? string.Empty;
            ThemeColors = themeColors ?? Array.Empty<uint>();
        }

        public ContentId Id { get; }

        /// <summary>« event.burrows ».</summary>
        public string Key { get; }

        /// <summary>1 for the first edition; a returning event gets 2, 3… (its Memory Shop opens, its bonus tiers count).</summary>
        public int Version { get; }

        public string NameKey { get; }

        /// <summary>Text key of the currency (« Pousses »).</summary>
        public string CurrencyKey { get; }

        public ContentId FeaturedSpecies { get; }

        /// <summary>ActivityId values of the featured activities (× 1.5 currency), then the fallback when none is shipped.</summary>
        public int[] FeaturedActivities { get; }
        public int FallbackActivity { get; }

        /// <summary>The album's event number (101 to 112: page « page.event.N »); 0 for none.</summary>
        public int AlbumEventNumber { get; }

        public EventTier[] Tiers { get; }

        /// <summary>Returning-year bonus tiers T31 to T36 (free line only).</summary>
        public EventTier[] BonusTiers { get; }

        public EventEggItem[] EggItems { get; }

        /// <summary>The secret variant v6 (1 % an egg, outside the guarantee).</summary>
        public ContentId SecretVariant { get; }

        public EventShopItem[] Shop { get; }
        public EventMemoryItem[] Memory { get; }
        public EventQuestTemplate[] DailyQuests { get; }
        public EventQuestTemplate[] LongQuests { get; }

        /// <summary>Difficulty level of each of the 10 featured-activity challenges.</summary>
        public int[] ChallengeLevels { get; }

        /// <summary>Outfit 3, given at 30 challenge stars.</summary>
        public ContentId ChallengeOutfit { get; }

        /// <summary>The dated Souvenir decoration (every player, at the Souvenir tier).</summary>
        public ContentId Souvenir { get; }

        /// <summary>The Box (9.99): outfit 7 and 4 luxury decorations (its 800 Gems come from the store product).</summary>
        public ContentId[] BoxItems { get; }

        /// <summary>The 3 headline rewards of the announce banner.</summary>
        public EventReward[] Headline { get; }

        public string[] ThemeKeys { get; }

        /// <summary>T49: the currency's icon key (« ICO_Event_Pousses »); empty: the generic event currency icon.</summary>
        public string CurrencyIconKey { get; }

        /// <summary>T49: the theme colors as 0xRRGGBB (primary, background, accent); empty: the hub's tokens.</summary>
        public uint[] ThemeColors { get; }

        /// <summary>The instance of a year: « event.burrows.2027 ».</summary>
        public string InstanceId(int year) => Key + "." + year;
    }
}
