#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Content
{
    /// <summary>
    /// T47 outfit (doc Accessoires, M-DEF-OUTFIT): 1 to 3 accessories on different sockets worn together with one shared
    /// palette. Equipping it equips each accessory the animal can wear; the player still owns the accessories one by one.
    /// </summary>
    public sealed class OutfitDefinition : IContentDefinition
    {
        public const int MaxAccessories = 3;

        public OutfitDefinition(ContentId id, string displayKey, ContentId[] accessories, string palette, string iconKey, string sourceKey)
        {
            Id = id;
            DisplayKey = displayKey;
            Accessories = accessories ?? Array.Empty<ContentId>();
            Palette = palette ?? string.Empty;
            IconKey = iconKey;
            SourceKey = sourceKey;
        }

        public ContentId Id { get; }
        public string DisplayKey { get; }

        /// <summary>Accessory ids (1 to 3, each on its own socket).</summary>
        public ContentId[] Accessories { get; }

        /// <summary>Palette name shared by every accessory of the outfit (each one lists it).</summary>
        public string Palette { get; }

        public string IconKey { get; }

        /// <summary>Where the outfit was authored (catalog or capture), for the review grid.</summary>
        public string SourceKey { get; }
    }
}
