#nullable enable
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// Free-standing palette for world props and ground (palette.&lt;biome&gt;.&lt;name&gt;). Same 8-color row
    /// format as animal variants and eggs, so one global table serves every master shader.
    /// Prop layout: base, region R, region G, region B, region A, occlusion tint, accent, reserved.
    /// </summary>
    public sealed class PaletteDefinition : IContentDefinition
    {
        public const int PaletteSize = 8;

        public PaletteDefinition(ContentId id, Color32[] colors)
        {
            Id = id;
            Colors = colors;
        }

        public ContentId Id { get; }

        /// <summary>Exactly <see cref="PaletteSize"/> colors.</summary>
        public Color32[] Colors { get; }
    }
}
