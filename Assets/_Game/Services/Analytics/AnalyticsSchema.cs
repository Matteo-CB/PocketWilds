#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Services.Analytics
{
    /// <summary>
    /// Analytics event ids (doc 09 Analytics, T29). Wire values: stable, append only, never renumbered. The field
    /// layout of each event is in <see cref="AnalyticsSchema"/> and Docs/Analytics/T29_Analytics_Schema.md.
    /// </summary>
    public enum AnalyticsEventId : ushort
    {
        SessionStart = 1,
        SessionEnd = 2,
        LoadTime = 3,
        Milestone = 10,
        Hatch = 20,
        UpgradeBuy = 21,
        UpgradeMilestone = 22,
        ZoneOpen = 23,
        BiomeUnlock = 24,
        BiomeGateSeen = 25,
        RefugeLevelUp = 26,
        BiomeMasteryUp = 27,
        DecorationBuyLight = 28,
        TreatBuy = 29,
        Economy = 30,
        // T35B (doc 02 Œufs, section 8).
        EggBuy = 33,
        MultiHatch = 34,
        DiscoveryEggGain = 35,
        EggOddsOpen = 36,
        EconomySnapshot = 31,
        EconomyFlow = 32,
        ScreenView = 40,
        OfferImpression = 41,
        OfferClick = 42,
        PurchaseResult = 43,
        Restore = 44,
        PerfSample = 50,
        OfflineReturn = 60,
    }

    /// <summary>Screens worth counting (stable values).</summary>
    public enum ScreenId : byte
    {
        Refuge = 1,
        Codex = 2,
        CodexSheet = 3,
        Shop = 4,
        OfferModal = 5,
        OfflineReturn = 6,
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

        private static Field[][] BuildLayouts()
        {
            var layouts = new Field[64][];
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
