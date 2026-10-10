#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>One of the 12 seasonal rare visitors (doc Événements originaux, section 5): a seasonal variant of a species.</summary>
    public readonly struct VisitorDefinition
    {
        /// <summary>visitor.meadow_rabbit…; its name text is « {Key}.name ».</summary>
        public readonly string Key;

        /// <summary>1 Éveil (March to May), 2 Soleil, 3 Récolte, 4 Givre (December to February): <see cref="AlbumHooks.SeasonOfMonth"/>.</summary>
        public readonly int Season;

        /// <summary>Its sticker on the season's album page (01 to 03).</summary>
        public readonly int Slot;

        public readonly ContentId Species;
        public readonly ContentId Variant;
        public readonly string SpeciesSource;
        public readonly string VariantSource;

        /// <summary>« #visitor.meadow_rabbit.name »: the journal's {visitor} value.</summary>
        public readonly string NameValue;

        public VisitorDefinition(string key, int season, int slot, string species, string variant)
        {
            Key = key;
            Season = season;
            Slot = slot;
            SpeciesSource = species;
            VariantSource = variant;
            Species = ContentId.FromSource(species);
            Variant = ContentId.FromSource(variant);
            NameValue = JournalService.KeyPrefix + key + ".name";
        }
    }

    /// <summary>
    /// The 12 rare visitors in season order (the album's season stickers 01 to 03). A visitor exists in the game only when its
    /// species and its seasonal variant are in the content (the variants carry <c>IsVisitor</c>); the others wait for their
    /// species. Variant ids: « variant.{species}_{word}_visitor » (agent CA made the meadow rabbit, flowery hedgehog and snow
    /// rabbit ones).
    /// </summary>
    public static class VisitorData
    {
        public const int Count = 12;

        private static readonly VisitorDefinition[] Rows =
        {
            new VisitorDefinition("visitor.white_fawn", 1, 1, "species.fawn", "variant.fawn.white_visitor"),
            new VisitorDefinition("visitor.meadow_rabbit", 1, 2, "species.rabbit", "variant.rabbit.meadow_visitor"),
            new VisitorDefinition("visitor.flowery_hedgehog", 1, 3, "species.hedgehog", "variant.hedgehog.flower_visitor"),
            new VisitorDefinition("visitor.golden_fox", 2, 1, "species.fox", "variant.fox.golden_visitor"),
            new VisitorDefinition("visitor.sun_squirrel", 2, 2, "species.squirrel", "variant.squirrel.sun_visitor"),
            new VisitorDefinition("visitor.rainbow_duck", 2, 3, "species.duck", "variant.duck.rainbow_visitor"),
            new VisitorDefinition("visitor.amber_badger", 3, 1, "species.badger", "variant.badger.amber_visitor"),
            new VisitorDefinition("visitor.russet_owl", 3, 2, "species.owl", "variant.owl.russet_visitor"),
            new VisitorDefinition("visitor.golden_chipmunk", 3, 3, "species.chipmunk", "variant.chipmunk.golden_visitor"),
            new VisitorDefinition("visitor.white_stoat", 4, 1, "species.ermine", "variant.ermine.white_visitor"),
            new VisitorDefinition("visitor.snow_rabbit", 4, 2, "species.rabbit", "variant.rabbit.snow_visitor"),
            new VisitorDefinition("visitor.crystal_tit", 4, 3, "species.chickadee", "variant.chickadee.crystal_visitor"),
        };

        public static ref readonly VisitorDefinition Row(int index) => ref Rows[index];

        public static int IndexOf(string key)
        {
            for (int i = 0; i < Rows.Length; i++)
                if (string.Equals(Rows[i].Key, key, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }

        public static int IndexOfVariant(ContentId variant)
        {
            for (int i = 0; i < Rows.Length; i++)
                if (Rows[i].Variant == variant)
                    return i;
            return -1;
        }
    }
}
