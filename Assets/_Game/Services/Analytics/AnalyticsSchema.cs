#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Services.Analytics
{
    /// <summary>
    /// Analytics event ids, aligned on the catalogue of doc 09 Analytics and doc 08 (« Catalogue des événements », 7
    /// October 2026; realigned in T37, before any data was sent). Wire values: stable from the first build that sends
    /// data, append only, never renumbered. The field layout of each event is in <see cref="AnalyticsSchema"/> and
    /// Docs/Analytics/T29_Analytics_Schema.md.
    /// </summary>
    public enum AnalyticsEventId : ushort
    {
        SessionStart = 1,
        SessionEnd = 2,
        LoadTime = 3,
        Milestone = 10,
        Hatch = 20,
        Economy = 30,
        EconomySnapshot = 31,
        EconomyFlow = 32,
        ScreenView = 40,
        OfferImpression = 41,
        OfferClick = 42,
        PurchaseResult = 43,
        Restore = 44,
        PerfSample = 50,
        OfflineReturn = 60,
        ReturnGift = 61,

        // Upgrades, zones, biomes, levels (block 70, doc 02 Améliorations, section 14).
        UpgradeBuy = 70,
        UpgradeMilestone = 71,
        ZoneOpen = 72,
        BiomeUnlock = 73,
        BiomeGateSeen = 74,
        RefugeLevelUp = 75,
        BiomeMasteryUp = 76,
        DecorationBuyLight = 77,
        TreatBuy = 78,

        /// <summary>A treat given to an animal (T36; next to TreatBuy, the doc's block 70 leaves 79 free).</summary>
        TreatGive = 79,

        // Renewal and Essence (block 80, doc 02 Renouveau, section 7).
        Renewal = 80,
        EssenceNodeBuy = 81,

        // Eggs (block 90, doc 02 Œufs, section 8).
        EggBuy = 90,
        MultiHatch = 91,
        DiscoveryEggGain = 92,
        EggOddsOpen = 93,

        // Activities (block 110, doc Mini-jeux; T37).
        ActivityStart = 110,
        ActivityEnd = 111,
        ActivityChallenge = 112,
        TicketsEmpty = 113,
        TicketsGain = 114,

        // The rendez-vous of the day (block 120, doc 01 Contenu, sections 1 to 6; T36).
        TodayOpen = 120,
        DailyGift = 121,
        WishDone = 122,
        WishReroll = 123,
        CalendarDay = 124,

        /// <summary>A wish drawn (T36; after the doc's block 120 list).</summary>
        WishDrawn = 125,

        /// <summary>A welcome gift claimed, days 1 to 3 (T36).</summary>
        WelcomeGift = 126,

        /// <summary>Everything claimable in Today claimed: the field measure of the two-minute visit (T36).</summary>
        DailyVisitDone = 127,

        // The weekly journal (block 130).
        JournalGoal = 130,
        JournalComplete = 131,

        // Expeditions (block 140, doc 08; T42).
        ExpeditionStart = 140,
        ExpeditionReturn = 141,
        ExpeditionSpeedup = 142,

        // The garden (block 150, doc 08; T43).
        GardenPlant = 150,
        GardenHarvest = 151,
        GardenSpeedup = 152,
    }

    /// <summary>Screens worth counting: the values of doc 08 section 9.5 (realigned in T37, before any data was sent).</summary>
    public enum ScreenId : byte
    {
        Refuge = 1,
        Codex = 2,
        CodexSheet = 3,
        Shop = 4,
        OfferModal = 5,
        OfflineReturn = 6,
        ActivityHub = 24,
        ActivityResult = 25,
        Today = 26,
        WeeklyJournal = 27,
    }

    /// <summary>
    /// One event: a fixed, blittable struct with a few numeric slots. Queuing it copies 64 bytes into a ring buffer:
    /// no allocation, no string, no boxing. Content ids travel as their stable 32-bit hash (debug table exported
    /// by the editor), never as names.
    /// </summary>
    public struct AnalyticsEvent
    {
        public AnalyticsEventId Id;
        public long UnixMs;
        public int Session;
        public int I0, I1, I2, I3;
        public uint U0, U1, U2;
        public double D0, D1, D2;

        public AnalyticsEvent(AnalyticsEventId id)
        {
            this = default;
            Id = id;
        }
    }

    /// <summary>
    /// Wire schema v1. A batch is compact JSON: a header once, then each event as a positional array
    /// [id, unixMs, session, fields...]. Field codes per event: I = int slots in order, U = content id slots,
    /// D = numbers with the given decimals. New fields are only ever appended (old readers ignore them).
    /// </summary>
    public static class AnalyticsSchema
    {
        public const int Version = 1;

        private static readonly Field[][] Layouts = BuildLayouts();

        /// <summary>Field layout per event, as slot names in wire order (I0..I3, U0..U2, D0..D2 with decimals). Cached.</summary>
        public static Field[] Layout(AnalyticsEventId id) => (int)id < Layouts.Length ? Layouts[(int)id] : System.Array.Empty<Field>();

        /// <summary>Highest event id plus one (the layouts are indexed by id; doc 08 numbers up to 340).</summary>
        private const int LayoutCount = 400;

        private static Field[][] BuildLayouts()
        {
            var layouts = new Field[LayoutCount][];
            for (int i = 0; i < layouts.Length; i++)
                layouts[i] = Create((AnalyticsEventId)i);
            return layouts;
        }

        private static Field[] Create(AnalyticsEventId id) => id switch
        {
            // offline seconds, animals, species, quality tier, battery saver
            AnalyticsEventId.SessionStart => new[] { Field.D(0, 0), Field.I(0), Field.I(1), Field.I(2), Field.I(3) },
            // session seconds, animals
            AnalyticsEventId.SessionEnd => new[] { Field.D(0, 1), Field.I(0) },
            // boot ms, scene ms
            AnalyticsEventId.LoadTime => new[] { Field.D(0, 0), Field.D(1, 0) },
            // milestone, seconds since install
            AnalyticsEventId.Milestone => new[] { Field.I(0), Field.D(0, 0) },
            // egg, species, variant, rarity, flags (1 new species, 2 new variant, 4 free, 8 any protection, 16 species
            // protection, 32 variant protection, 64 Discovery egg), cost mantissa, cost exponent, mastery level; T35B appends
            // the position in a multi hatch (0: single) and the Discovery rule.
            AnalyticsEventId.Hatch => new[] { Field.U(0), Field.U(1), Field.U(2), Field.I(0), Field.I(1), Field.D(0, 3), Field.I(2), Field.I(3),
                Field.D(1, 0), Field.D(2, 0) },
            // egg, quantity, eggs bought before in the cycle, cost log10, currency (0 Light, 1 Discovery stock, 2 event)
            AnalyticsEventId.EggBuy => new[] { Field.U(0), Field.I(0), Field.I(1), Field.D(0, 2), Field.I(2) },
            // egg, quantity, Rare or better, new species or variants
            AnalyticsEventId.MultiHatch => new[] { Field.U(0), Field.I(0), Field.I(1), Field.I(2) },
            // source, eggs gained, stock after
            AnalyticsEventId.DiscoveryEggGain => new[] { Field.I(0), Field.I(1), Field.I(2) },
            // egg
            AnalyticsEventId.EggOddsOpen => new[] { Field.U(0) },
            // T35C. count, Essence gained, lifetime Light log10, days since install, refuge level
            AnalyticsEventId.Renewal => new[] { Field.I(0), Field.I(1), Field.D(0, 2), Field.I(2), Field.I(3) },
            // node, rank
            AnalyticsEventId.EssenceNodeBuy => new[] { Field.U(0), Field.I(0) },
            // T36. lot, size, flags (1 forced big, 2 first fixed gift, 4 substituted), gifts since big before, Gems
            AnalyticsEventId.DailyGift => new[] { Field.I(0), Field.I(1), Field.I(2), Field.I(3), Field.D(0, 0) },
            // template, system, tier, pending after
            AnalyticsEventId.WishDrawn => new[] { Field.I(0), Field.I(1), Field.I(2), Field.I(3) },
            // template, claim path (0 bubble, 1 animal, 2 Today row, 3 claim all, 4 auto), Gems, pending after, minutes from draw
            AnalyticsEventId.WishDone => new[] { Field.I(0), Field.I(1), Field.I(2), Field.I(3), Field.D(0, 0) },
            // old template, new template
            AnalyticsEventId.WishReroll => new[] { Field.I(0), Field.I(1) },
            // box, cycle, substituted
            AnalyticsEventId.CalendarDay => new[] { Field.I(0), Field.I(1), Field.I(2) },
            // goal, slot, week index, carried over
            AnalyticsEventId.JournalGoal => new[] { Field.I(0), Field.I(1), Field.I(2), Field.I(3) },
            // week index, day of the week (1 Monday to 7 Sunday)
            AnalyticsEventId.JournalComplete => new[] { Field.I(0), Field.I(1) },
            // wishes pending, gift ready, calendar day (doc 08)
            AnalyticsEventId.TodayOpen => new[] { Field.I(0), Field.I(1), Field.I(2) },
            // T37 (doc 08, block 110, enumerations of section 9.5). activity, mode (1 rewarded, 2 practice, 3 challenge),
            // difficulty, tickets after the start, featured
            AnalyticsEventId.ActivityStart => new[] { Field.U(0), Field.I(0), Field.I(1), Field.I(2), Field.I(3) },
            // activity, mode, stars, flags (1 record, 2 abandon, 4 challenge improved, 8 ticket bought with Gems), challenge
            // (1 to 10, 0: free mode), score, seconds played
            AnalyticsEventId.ActivityEnd => new[] { Field.U(0), Field.I(0), Field.I(1), Field.I(2), Field.I(3), Field.D(0, 0), Field.D(1, 1) },
            // activity, challenge (1 to 10), stars, attempts
            AnalyticsEventId.ActivityChallenge => new[] { Field.U(0), Field.I(0), Field.I(1), Field.I(2) },
            // choice (0 closed, 1 wait, 2 ad, 3 Gems; practice chosen counts as 1, the player waits for a ticket), seconds
            // to the next free ticket
            AnalyticsEventId.TicketsEmpty => new[] { Field.I(0), Field.D(0, 0) },
            // source (doc 08 codes, ActivityAnalytics.TicketSourceCode), amount, balance after
            AnalyticsEventId.TicketsGain => new[] { Field.I(0), Field.I(1), Field.I(2) },
            // kind (1 short, 2 big), warm return, days away
            AnalyticsEventId.ReturnGift => new[] { Field.I(0), Field.I(1), Field.D(0, 0) },
            // T42 (doc 08, block 140). destination, hours (the duration column; 0 for the 3-minute first stroll), team, slot
            AnalyticsEventId.ExpeditionStart => new[] { Field.U(0), Field.I(0), Field.I(1), Field.I(2) },
            // destination, hours, reward_mask (1 Light, 2 treats, 4 seeds, 8 sticker, 16 Discovery egg, 32 tickets, 64
            // ingredients, 128 event currency), discovery_egg, event_currency
            AnalyticsEventId.ExpeditionReturn => new[] { Field.U(0), Field.I(0), Field.I(1), Field.I(2), Field.I(3) },
            // method (1 Gems, 2 ad, 3 Club), Gems, seconds that were left
            AnalyticsEventId.ExpeditionSpeedup => new[] { Field.I(0), Field.I(1), Field.D(0, 0) },
            // T43 (doc 08, block 150). seed (the plant), plot, grow_s
            AnalyticsEventId.GardenPlant => new[] { Field.U(0), Field.I(0), Field.D(0, 0) },
            // seed (the plant), yield_kind (1 flower, 2 fruit, 3 dye), count, watered
            AnalyticsEventId.GardenHarvest => new[] { Field.U(0), Field.I(0), Field.I(1), Field.I(2) },
            // method (1 Gems, 2 ad, 3 Club), Gems, seconds that were left
            AnalyticsEventId.GardenSpeedup => new[] { Field.I(0), Field.I(1), Field.D(0, 0) },
            // index (1 to 3)
            AnalyticsEventId.WelcomeGift => new[] { Field.I(0) },
            // seconds from Today opened to the last claim, taps, items claimed
            AnalyticsEventId.DailyVisitDone => new[] { Field.D(0, 0), Field.I(0), Field.I(1) },
            // treat, species, bond added, preferred family
            AnalyticsEventId.TreatGive => new[] { Field.U(0), Field.U(1), Field.I(0), Field.I(1) },
            // T35A (doc 02 Améliorations, section 14). Costs travel as their log10 (no personal data, any size).
            // biome, kind, level from, level to, cost log10, quantity mode
            AnalyticsEventId.UpgradeBuy => new[] { Field.U(0), Field.I(0), Field.I(1), Field.I(2), Field.D(0, 2), Field.I(3) },
            // biome, kind, level
            AnalyticsEventId.UpgradeMilestone => new[] { Field.U(0), Field.I(0), Field.I(1) },
            // zone, biome, index, cost log10, minutes since install
            AnalyticsEventId.ZoneOpen => new[] { Field.U(0), Field.U(1), Field.I(0), Field.D(0, 2), Field.D(1, 0) },
            // biome, refuge level, days since install, play minutes
            AnalyticsEventId.BiomeUnlock => new[] { Field.U(0), Field.I(0), Field.I(1), Field.D(0, 0) },
            // biome, conditions met (bits: level, species, mastery, floor, light)
            AnalyticsEventId.BiomeGateSeen => new[] { Field.U(0), Field.I(0) },
            // level, score, days since install
            AnalyticsEventId.RefugeLevelUp => new[] { Field.I(0), Field.D(0, 0), Field.I(1) },
            // biome, level
            AnalyticsEventId.BiomeMasteryUp => new[] { Field.U(0), Field.I(0) },
            // decoration, copy, cost log10
            AnalyticsEventId.DecorationBuyLight => new[] { Field.U(0), Field.I(0), Field.D(0, 2) },
            // treat, cost log10
            AnalyticsEventId.TreatBuy => new[] { Field.U(0), Field.D(0, 2) },
            // currency, reason, delta mantissa, delta exponent, balance mantissa, balance exponent
            AnalyticsEventId.Economy => new[] { Field.I(0), Field.I(1), Field.D(0, 3), Field.I(2), Field.D(1, 3), Field.I(3) },
            // light mantissa, light exponent, rate mantissa, rate exponent, gems, animals, species
            AnalyticsEventId.EconomySnapshot => new[] { Field.D(0, 3), Field.I(0), Field.D(1, 3), Field.I(1), Field.D(2, 0), Field.I(2), Field.I(3) },
            // currency, reason, changes, sum mantissa, sum exponent (Light and Essence, summed per session)
            AnalyticsEventId.EconomyFlow => new[] { Field.I(0), Field.I(1), Field.I(2), Field.D(0, 3), Field.I(3) },
            // screen
            AnalyticsEventId.ScreenView => new[] { Field.I(0) },
            // offer, placement
            AnalyticsEventId.OfferImpression => new[] { Field.U(0), Field.I(0) },
            AnalyticsEventId.OfferClick => new[] { Field.U(0), Field.I(0) },
            // offer, product, status, reference cents, gems (+ granted, - spent), purchases of this offer, paid purchases,
            // flags (bit0 restored, bit1 new grant)
            AnalyticsEventId.PurchaseResult => new[] { Field.U(0), Field.U(1), Field.I(0), Field.I(1), Field.D(0, 0), Field.I(2), Field.I(3), Field.U(2) },
            // restored purchases
            AnalyticsEventId.Restore => new[] { Field.I(0) },
            // frames, average ms, p95 ms, slow frames, max ms, memory MB, quality tier, battery saver (I2 bit0)
            AnalyticsEventId.PerfSample => new[] { Field.I(0), Field.D(0, 2), Field.D(1, 1), Field.I(1), Field.D(2, 1), Field.U(0), Field.I(2), Field.I(3) },
            // seconds away, light mantissa, light exponent
            AnalyticsEventId.OfflineReturn => new[] { Field.D(0, 0), Field.D(1, 3), Field.I(0) },
            _ => System.Array.Empty<Field>(),
        };

        public readonly struct Field
        {
            public readonly char Kind;
            public readonly byte Slot;
            public readonly byte Decimals;

            private Field(char kind, int slot, int decimals)
            {
                Kind = kind;
                Slot = (byte)slot;
                Decimals = (byte)decimals;
            }

            public static Field I(int slot) => new Field('I', slot, 0);
            public static Field U(int slot) => new Field('U', slot, 0);
            public static Field D(int slot, int decimals) => new Field('D', slot, decimals);
        }

        /// <summary>BigValue on the wire: mantissa and power of ten (Light can outgrow a double).</summary>
        public static void Split(BigValue value, out double mantissa, out int exponent)
        {
            mantissa = value.Mantissa;
            exponent = value.Exponent10;
        }
    }
}
