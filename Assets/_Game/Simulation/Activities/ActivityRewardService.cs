#nullable enable
using System;
using PocketWilds.Content;
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The event systems activities feed (doc 09 Événements; none exists in T37): a running big event and its featured
    /// activity, the « Week-end des Activités », and the event currency sink. Null: no event, no weekend.
    /// </summary>
    public interface IActivityEventHooks
    {
        bool EventRunning { get; }
        bool IsFeatured(ActivityId activity);
        bool ActivitiesWeekend { get; }
        void GrantEventCurrency(int amount, ActivityId activity);
    }

    /// <summary>What a finished game asks the reward service (built by the runner).</summary>
    public struct ActivityRewardRequest
    {
        public ActivityId Activity;
        public ActivityMode Mode;

        /// <summary>The challenge played (0 to 9), −1 for the free mode.</summary>
        public int Challenge;

        public int Stars;

        /// <summary>A rewarded free-mode game beat the previous best (a guaranteed sticker).</summary>
        public bool NewRecord;

        /// <summary>A challenge reached 3 stars for the first time (5 Gems once, a decoration every 5).</summary>
        public bool ChallengeFirstThreeStars;

        /// <summary>The game was paid with a ticket bought with Gems: nothing is drawn at random.</summary>
        public bool PaidTicket;

        /// <summary>The game's deterministic seed (the sticker draw of a free-ticket game).</summary>
        public uint Seed;

        /// <summary>The theme's biome (biome mastery XP), None to use the first animal's biome.</summary>
        public ContentId Theme;

        public int AnimalCount;
        public uint Animal0;
        public uint Animal1;
        public uint Animal2;

        public uint Animal(int index) => index == 0 ? Animal0 : index == 1 ? Animal1 : Animal2;
    }

    /// <summary>
    /// Everything a rewarded game gave, for the result card (doc 05 Sensations, sheet 20) and analytics: each reward and
    /// the Light really granted.
    /// </summary>
    public struct ActivityRewardOutcome
    {
        public bool Rewarded;
        public int Stars;

        public double LightMinutes;

        /// <summary>The Light really added (minutes of the production at that moment, at least 1).</summary>
        public BigValue Light;

        /// <summary>Bond points of the table per animal, and the points really added to all of them.</summary>
        public int BondPerAnimal;
        public int BondAdded;
        public int BondAnimals;

        /// <summary>Species mastery XP per species played, and the number of species.</summary>
        public int SpeciesXp;
        public int Species;

        public ContentId Biome;
        public long BiomeXp;

        /// <summary>The « first 3 stars of the day » Gems (inside the daily cap).</summary>
        public long DailyGems;

        /// <summary>A challenge's first 3 stars.</summary>
        public long ChallengeGems;

        public ContentId Decoration;

        /// <summary>Stickers won: the record's (guaranteed) and the chance's (a draw, or the fixed counter of a paid game).</summary>
        public int Stickers;
        public bool RecordSticker;
        public bool ChanceSticker;

        /// <summary>A paid game: the sticker is the next one of the page and its chance a fixed counter (« Prochain autocollant de la page »).</summary>
        public bool FixedStickerRule;

        /// <summary>The paid counter after this game (0 to 99).</summary>
        public int StickerPoints;

        /// <summary>Stickers turned into treats while the albums are closed (3 treats of the newest biome each).</summary>
        public ContentId Treat;
        public int Treats;

        /// <summary>
        /// T38: the activity's own item (doc 01 Contenu, « Objet gagné »: Grooming gives 1 Clover Crunch at 3 stars), a treat
        /// given with the rewards and shown on the result card; None and 0 without one. Never doubled by the weekend.
        /// </summary>
        public ContentId ItemTreat;
        public int ItemTreats;

        public int EventCurrency;
        public bool Weekend;

        public long Gems => DailyGems + ChallengeGems;
    }

    /// <summary>
    /// The rewards of a finished rewarded game (doc « Mini-jeux &amp; activités » section 2 « Récompenses »; doc 01 Contenu
    /// section 12.0; doc 02 Gemmes; doc 08 keys in <see cref="ActivityConfig"/>):
    /// <list type="bullet">
    /// <item>0 stars: nothing (every table starts at 1 star).</item>
    /// <item>Light 5, 10 or 20 minutes of the current production (+10 % per Essence « Activity Harvest » rank).</item>
    /// <item>Bond +2, +4 or +6 to each animal played (<see cref="BondSource.Activity"/>); species mastery XP 1, 2 or 3 to
    /// each species played (<see cref="MasteryReason.Activity"/>); biome mastery XP (<see cref="BiomeXpSource.Activity"/>) to
    /// the theme's biome, or the first animal's.</item>
    /// <item>Gems: 2 for the first 3 stars of the day in each activity, 6 a day at most across activities; a challenge's
    /// first 3 stars: 5 Gems once, and every 5 such challenges the next challenge decoration (E-DEC-CH-01 to 36, in order;
    /// doc 05 Décorations, section 16).</item>
    /// <item>Stickers: a new record gives one, guaranteed; then a chance of 10, 20 or 35 % (+2 % per Essence « Stickers »
    /// rank). A game paid with a bought ticket draws nothing at random: its chance is a fixed counter of 10, 20 or 35
    /// points, a sticker at 100, the rest kept (decision of 7 October). Before the albums (T44) a sticker becomes 3
    /// treats of the newest biome, as T36 does.</item>
    /// <item>Event currency 20, 30 or 40 during a big event (× 1.5 on the featured activity); the « Week-end des
    /// Activités » doubles the rewards except Gems and stickers.</item>
    /// <item>T38: the activity's item (« Objet gagné »), a treat set by <see cref="SetItemReward"/> (Grooming: 1 Clover
    /// Crunch at 3 stars), in every rewarded game reaching its stars, free mode or challenge; an item, so the weekend never
    /// doubles it.</item>
    /// </list>
    /// <see cref="Compute"/> reads only; <see cref="Grant"/> computes, writes the counters and gives through the T36
    /// <see cref="RewardGranter"/> (Gems with <see cref="EconomyReason.Activity"/>, Light, treats, decoration, stickers).
    /// Practice never comes here. No allocation.
    /// </summary>
    public sealed class ActivityRewardService
    {
        /// <summary>The 36 challenge decorations in their order (doc 05 Décorations, section 16).</summary>
        public const int ChallengeDecorationCount = 36;

        private const uint StickerSalt = 0x51C4E7u;

        /// <summary>E-DEC-CH-01 to 36 (decor.&lt;English name&gt;), declared before the ids built from them.</summary>
        private static readonly string[] DecorationNames =
        {
            "giant_brush", "ribbon_tub", "hiding_bush", "peekaboo_board", "big_nut_basket", "round_hazel_tree",
            "giant_memory_cards", "pawprint_lectern", "expedition_map_stand", "explorer_trunk", "seed_sacks", "garden_tools",
            "giant_message_bottle", "giant_bobber", "hopping_lily_pad", "stone_frog", "giant_strawberry_tart", "pastry_counter",
            "tripod_camera", "giant_photo_frame", "matching_cushions", "giant_tokens", "mini_slope", "course_poles",
            "star_board", "constellation_cushion", "music_stand", "giant_xylophone", "bubble_maker", "foam_tub",
            "giant_puzzle", "tile_cube", "maze_gate", "golden_molehill", "grooming_mirror", "great_challenge_rosette",
        };

        private static readonly ContentId[] ChallengeDecorationIds = BuildDecorations();

        private readonly ActivityContext _ctx;

        // T38: each activity's item (« Objet gagné »), by slot.
        private readonly ContentId[] _itemTreat = new ContentId[ActivityCatalog.SlotCount];
        private readonly int[] _itemCount = new int[ActivityCatalog.SlotCount];
        private readonly int[] _itemStars = new int[ActivityCatalog.SlotCount];

        public ActivityRewardService(ActivityContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>The event systems (null until they exist).</summary>
        public IActivityEventHooks? Events { get; set; }

        /// <summary>
        /// The item a rewarded game of this activity gives from <paramref name="minStars"/> stars (doc 01 Contenu, « Objet
        /// gagné »): <paramref name="count"/> of a treat. Registered by <see cref="ActivityRegistry.RegisterShipped"/>; a count
        /// of 0 removes it.
        /// </summary>
        public void SetItemReward(ActivityId activity, ContentId treat, int count, int minStars = 3)
        {
            int slot = ActivityCatalog.SlotOf(activity);
            if (slot < 0)
                return;
            bool valid = count > 0 && treat.IsValid;
            _itemTreat[slot] = valid ? treat : ContentId.None;
            _itemCount[slot] = valid ? count : 0;
            _itemStars[slot] = Math.Max(1, Math.Min(3, minStars));
        }

        /// <summary>The item of an activity: its treat (None: none), its count and the stars it needs.</summary>
        public ContentId ItemReward(ActivityId activity, out int count, out int minStars)
        {
            int slot = ActivityCatalog.SlotOf(activity);
            count = slot >= 0 ? _itemCount[slot] : 0;
            minStars = slot >= 0 ? _itemStars[slot] : 0;
            return slot >= 0 && count > 0 ? _itemTreat[slot] : ContentId.None;
        }

        /// <summary>The n-th challenge decoration (0 = E-DEC-CH-01 Brosse géante), None past the 36th.</summary>
        public static ContentId ChallengeDecoration(int index) =>
            (uint)index < (uint)ChallengeDecorationIds.Length ? ChallengeDecorationIds[index] : ContentId.None;

        /// <summary>The source id of a challenge decoration (localization keys), null past the 36th.</summary>
        public static string? ChallengeDecorationSource(int index) =>
            (uint)index < (uint)DecorationNames.Length ? "decor." + DecorationNames[index] : null;

        /// <summary>The rewards a game would give, without giving anything.</summary>
        public void Compute(in ActivityRewardRequest request, out ActivityRewardOutcome outcome)
        {
            outcome = default;
            outcome.Stars = request.Stars;
            outcome.Rewarded = request.Mode != ActivityMode.Practice;
            if (!outcome.Rewarded || request.Stars <= 0 || ActivityCatalog.SlotOf(request.Activity) < 0)
                return;

            var config = _ctx.Config;
            var state = _ctx.Activities;
            int stars = Math.Min(3, request.Stars);
            var events = Events;
            bool weekend = events != null && events.ActivitiesWeekend;
            double times = weekend ? Math.Max(1.0, config.WeekendMultiplier) : 1.0;
            outcome.Weekend = weekend;

            outcome.LightMinutes = config.LightMinutes(stars) * (1.0 + Math.Max(0.0, _ctx.Essence(EssenceEffect.ActivityLight))) * times;
            outcome.BondPerAnimal = Scale(config.Bond(stars), times);
            outcome.SpeciesXp = Scale(config.SpeciesXp(stars), times);

            int animals = Math.Max(0, Math.Min(3, request.AnimalCount));
            outcome.BondAnimals = 0;
            outcome.Species = 0;
            ContentId firstBiome = default;
            for (int i = 0; i < animals; i++)
            {
                if (!_ctx.State.Collection.TryGet(request.Animal(i), out var animal))
                    continue;
                outcome.BondAnimals++;
                if (!SpeciesSeenBefore(in request, i, animal.Species))
                    outcome.Species++;
                if (!firstBiome.IsValid && _ctx.Content.Species.TryGet(animal.Species, out var definition))
                    firstBiome = definition.Biome;
            }
            outcome.BondAdded = outcome.BondPerAnimal * outcome.BondAnimals;

            var mastery = _ctx.Progression?.Mastery;
            outcome.Biome = request.Theme.IsValid ? request.Theme : firstBiome;
            if (mastery != null && outcome.Biome.IsValid)
                outcome.BiomeXp = (long)Math.Round(mastery.XpFor(BiomeXpSource.Activity) * times, MidpointRounding.AwayFromZero);

            // Gems: never doubled.
            var record = state.Records[ActivityCatalog.SlotOf(request.Activity)];
            int today = _ctx.TodayKey;
            if (stars >= 3 && record.ThreeStarGemsDayKey != today)
            {
                long given = state.GemsDayKey == today ? state.GemsToday : 0;
                outcome.DailyGems = Math.Max(0, Math.Min(config.GemsFirstThreeStars, config.GemsDailyCap - given));
            }
            if (request.ChallengeFirstThreeStars && request.Challenge >= 0)
            {
                outcome.ChallengeGems = Math.Max(0, config.GemsChallenge);
                int every = Math.Max(1, config.ChallengeDecorationEvery);
                int due = (state.ChallengesThreeStars + 1) / every;
                if (state.ChallengeDecorations < due)
                    outcome.Decoration = ChallengeDecoration(state.ChallengeDecorations);
            }

            // Stickers: the record's is guaranteed; the chance is a draw, or a fixed counter for a paid game. Never doubled.
            double chance = config.StickerChance(stars) + Math.Max(0.0, _ctx.Essence(EssenceEffect.StickerChance));
            if (request.NewRecord)
            {
                outcome.Stickers++;
                outcome.RecordSticker = true;
            }
            if (request.PaidTicket)
            {
                outcome.FixedStickerRule = true;
                int points = record.PaidStickerPoints + (int)Math.Round(chance * 100.0, MidpointRounding.AwayFromZero);
                int won = points / ActivityConfig.PaidStickerThreshold;
                outcome.StickerPoints = points - won * ActivityConfig.PaidStickerThreshold;
                if (won > 0)
                {
                    outcome.Stickers += won;
                    outcome.ChanceSticker = true;
                }
            }
            else
            {
                outcome.StickerPoints = record.PaidStickerPoints;
                if (chance > 0.0 && SeedHash.Unit(request.Seed, StickerSalt) < chance)
                {
                    outcome.Stickers++;
                    outcome.ChanceSticker = true;
                }
            }
            if (outcome.Stickers > 0 && !_ctx.AlbumsOpen)
            {
                outcome.Treat = _ctx.NewestTreat();
                outcome.Treats = outcome.Stickers * DailyRewardTables.StickerTreats;
            }

            // T38: the activity's item, an item: never doubled.
            int item = ActivityCatalog.SlotOf(request.Activity);
            if (_itemCount[item] > 0 && stars >= _itemStars[item])
            {
                outcome.ItemTreat = _itemTreat[item];
                outcome.ItemTreats = _itemCount[item];
            }

            if (events != null && events.EventRunning)
            {
                double featured = events.IsFeatured(request.Activity) ? Math.Max(1.0, config.FeaturedMultiplier) : 1.0;
                outcome.EventCurrency = Scale(config.EventCurrency(stars), featured * times);
            }
        }

        /// <summary>
        /// Computes and gives the rewards, writes the daily Gems, the paid sticker counter and the challenge counters, and
        /// asks for a priority save (a game once rewarded is never rewarded again).
        /// </summary>
        public void Grant(in ActivityRewardRequest request, out ActivityRewardOutcome outcome)
        {
            Compute(in request, out outcome);
            if (!outcome.Rewarded || outcome.Stars <= 0)
                return;

            var state = _ctx.Activities;
            int slot = ActivityCatalog.SlotOf(request.Activity);
            int today = _ctx.TodayKey;
            ref var record = ref state.Records[slot];
            if (outcome.Stars >= 3 && record.ThreeStarGemsDayKey != today)
            {
                record.ThreeStarGemsDayKey = today;
                if (state.GemsDayKey != today)
                {
                    state.GemsDayKey = today;
                    state.GemsToday = 0;
                }
                state.GemsToday += (int)outcome.DailyGems;
            }
            record.PaidStickerPoints = outcome.StickerPoints;
            if (request.ChallengeFirstThreeStars && request.Challenge >= 0)
            {
                state.ChallengesThreeStars++;
                if (outcome.Decoration.IsValid)
                    state.ChallengeDecorations++;
            }

            bool albums = _ctx.AlbumsOpen;
            var bundle = new RewardBundle
            {
                Gems = outcome.Gems,
                LightMinutes = outcome.LightMinutes,
                Decoration = outcome.Decoration,
                Stickers = albums ? outcome.Stickers : 0,
                Treat = outcome.Treat,
                Treats = outcome.Treats,
            };
            // The item rides in the same bundle when it is the same treat (or no other treat comes), else in its own.
            bool itemApart = false;
            if (outcome.ItemTreats > 0)
            {
                if (bundle.Treats <= 0 || bundle.Treat == outcome.ItemTreat)
                {
                    bundle.Treat = outcome.ItemTreat;
                    bundle.Treats += outcome.ItemTreats;
                }
                else
                {
                    itemApart = true;
                }
            }
            var granted = _ctx.Granter.Grant(in bundle, EconomyReason.Activity, DiscoverySource.Debug);
            outcome.Light = granted.Light;
            if (itemApart)
            {
                var item = new RewardBundle { Treat = outcome.ItemTreat, Treats = outcome.ItemTreats };
                _ctx.Granter.Grant(in item, EconomyReason.Activity, DiscoverySource.Debug);
            }

            int added = 0;
            var bond = _ctx.Bond;
            var speciesMastery = _ctx.SpeciesMastery;
            int animals = Math.Max(0, Math.Min(3, request.AnimalCount));
            for (int i = 0; i < animals; i++)
            {
                uint id = request.Animal(i);
                if (!_ctx.State.Collection.TryGet(id, out var animal))
                    continue;
                if (bond != null && outcome.BondPerAnimal > 0)
                    added += bond.AddBond(id, outcome.BondPerAnimal, BondSource.Activity);
                if (speciesMastery != null && outcome.SpeciesXp > 0 && !SpeciesSeenBefore(in request, i, animal.Species))
                    speciesMastery.AddXp(animal.Species, MasteryReason.Activity, outcome.SpeciesXp);
            }
            outcome.BondAdded = added;

            if (outcome.BiomeXp > 0 && outcome.Biome.IsValid)
                _ctx.Progression?.Mastery.AddXp(outcome.Biome, outcome.BiomeXp);
            if (outcome.EventCurrency > 0)
                Events?.GrantEventCurrency(outcome.EventCurrency, request.Activity);
            _ctx.RequestSave(SaveUrgency.Priority);
        }

        private bool SpeciesSeenBefore(in ActivityRewardRequest request, int index, ContentId species)
        {
            for (int j = 0; j < index; j++)
                if (_ctx.State.Collection.TryGet(request.Animal(j), out var other) && other.Species == species)
                    return true;
            return false;
        }

        private static int Scale(int value, double times) =>
            value <= 0 ? 0 : (int)Math.Round(value * times, MidpointRounding.AwayFromZero);

        private static ContentId[] BuildDecorations()
        {
            var ids = new ContentId[DecorationNames.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = ContentId.FromSource("decor." + DecorationNames[i]);
            return ids;
        }
    }
}
