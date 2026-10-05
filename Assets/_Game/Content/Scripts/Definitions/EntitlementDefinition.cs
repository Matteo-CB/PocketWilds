#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Content
{
    /// <summary>What an owned item is. Stable byte values.</summary>
    public enum EntitlementKind : byte
    {
        Cosmetic = 0,
        Decoration = 1,
        Perk = 2,
        AdRemoval = 3,
    }

    /// <summary>
    /// Something the player owns for good once granted (purchase, pass, event): a cosmetic, a decoration, a
    /// perk. Owned ids live in the save (EntitlementState.Owned); the feature that uses the item reads them.
    /// </summary>
    public sealed class EntitlementDefinition : IContentDefinition
    {
        public EntitlementDefinition(ContentId id, EntitlementKind kind, string displayKey, string iconKey)
        {
            Id = id;
            Kind = kind;
            DisplayKey = displayKey;
            IconKey = iconKey;
        }

        public ContentId Id { get; }
        public EntitlementKind Kind { get; }

        /// <summary>Localization key of the item name, e.g. "entitlement.cosmetic.flower_crown.name".</summary>
        public string DisplayKey { get; }

        /// <summary>Icon used by shop cards and reward lists.</summary>
        public string IconKey { get; }
    }
}
