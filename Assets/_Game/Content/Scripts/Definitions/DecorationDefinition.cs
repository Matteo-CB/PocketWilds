#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Content
{
    /// <summary>T49B the six decoration categories of the decoration mode's filters (doc 05 Décorations, section 2). Stable values.</summary>
    public enum DecorationCategory : byte
    {
        Nature = 0,
        Comfort = 1,
        Landmark = 2,
        Event = 3,
        Souvenir = 4,
        Companion = 5,
    }

    /// <summary>T49B the footprint class: the upper limit of the room a decoration takes (doc 05, section 3). Stable values.</summary>
    public enum DecorationFootprintClass : byte
    {
        XS = 0,
        S = 1,
        M = 2,
        L = 3,
        XL = 4,
    }

    /// <summary>T49B the class's outline: a circle (radius) or a rectangle (length × width).</summary>
    public enum DecorationFootprintShape : byte
    {
        Circle = 0,
        Rectangle = 1,
    }

    /// <summary>T49B the interaction points a decoration carries (IP_Inspect, IP_Sit, IP_Eat, IP_Sleep, IP_Perch; doc 05, section 5).</summary>
    [Flags]
    public enum DecorationInteraction : byte
    {
        None = 0,
        Inspect = 1,
        Sit = 2,
        Eat = 4,
        Sleep = 8,
        Perch = 16,
    }

    /// <summary>
    /// T49B one height band of a decoration's measured volume, prefab space (yaw 0, scale 1): a capsule from A to B (x, y, z)
    /// with its radius, between Bottom and Top above the ground (CollisionProxies bands: 0-0.7, 0.7-1.5, 1.5-3, 3-6 m).
    /// </summary>
    public readonly struct DecorationBand
    {
        public readonly float AX, AY, AZ, BX, BY, BZ, Radius, Bottom, Top;

        public DecorationBand(float ax, float ay, float az, float bx, float by, float bz, float radius, float bottom, float top)
        {
            AX = ax; AY = ay; AZ = az;
            BX = bx; BY = by; BZ = bz;
            Radius = radius;
            Bottom = bottom;
            Top = top;
        }
    }

    /// <summary>
    /// T49B a decoration (doc 05 Décorations; Decoration Wizard ED-002): identity and name key, category, footprint class and
    /// shape, interaction points, its prefab by address (PF_Decor_{Prefix}_{Name}, loaded only where it is placed) with its
    /// palette row and icon, where it comes from, and the volume measured once in the editor on the LOD0 (never recomputed
    /// from renderers in play): the height bands, the convex ground outline (x, z pairs), the reach around the pivot and the
    /// height of the top. Flat decorations (under 4 cm) are walked on but still keep their outline.
    /// </summary>
    public sealed class DecorationDefinition : IContentDefinition
    {
        public DecorationDefinition(ContentId id, string sourceId, string displayKey, DecorationCategory category,
            DecorationFootprintClass footprint, DecorationFootprintShape shape, DecorationInteraction interactions, ContentId biome,
            string prefabAddress, ContentId palette, string iconKey, string source, int maxCopies,
            DecorationBand[]? bands, float[]? groundHull, float reach, float height, float topHeight)
        {
            Id = id;
            SourceId = sourceId ?? string.Empty;
            DisplayKey = displayKey ?? string.Empty;
            Category = category;
            Footprint = footprint;
            Shape = shape;
            Interactions = interactions;
            Biome = biome;
            PrefabAddress = prefabAddress ?? string.Empty;
            Palette = palette;
            IconKey = iconKey ?? string.Empty;
            Source = source ?? string.Empty;
            MaxCopies = maxCopies;
            Bands = bands ?? Array.Empty<DecorationBand>();
            GroundHull = groundHull ?? Array.Empty<float>();
            Reach = reach;
            Height = height;
            TopHeight = topHeight;
        }

        public ContentId Id { get; }

        /// <summary>« decor.grassland.daisy_tuft », « decor.burrows.moss_mound », « landmark.album.season ».</summary>
        public string SourceId { get; }

        /// <summary>Localization key of the name, e.g. « decor.grassland.daisy_tuft.name ».</summary>
        public string DisplayKey { get; }

        public DecorationCategory Category { get; }
        public DecorationFootprintClass Footprint { get; }
        public DecorationFootprintShape Shape { get; }
        public DecorationInteraction Interactions { get; }

        /// <summary>The biome of a progression decoration (its palette family); default for commons, events and monuments.</summary>
        public ContentId Biome { get; }

        /// <summary>Prefab name, also its Addressables address (PF_Decor_GL_DaisyTuft).</summary>
        public string PrefabAddress { get; }

        /// <summary>Palette row (palette.{biome}.{name}), set on every renderer's Shader User Value.</summary>
        public ContentId Palette { get; }

        /// <summary>Icon sprite name (ICO_Decor_GL_DaisyTuft).</summary>
        public string IconKey { get; }

        /// <summary>Where it comes from, as the doc writes it (« mastery.forest.9 », « album.season », « event.burrows.track.3 »).</summary>
        public string Source { get; }

        /// <summary>Copies a player may own: 10 for commons, 1 for everything else (doc 05, section 8).</summary>
        public int MaxCopies { get; }

        /// <summary>The measured volume, one capsule per height band (prefab space).</summary>
        public DecorationBand[] Bands { get; }

        /// <summary>Convex outline of the ground band seen from above: x0, z0, x1, z1... (prefab space).</summary>
        public float[] GroundHull { get; }

        /// <summary>Radius around the pivot holding every band (broad phase, m).</summary>
        public float Reach { get; }

        /// <summary>Top of the measured volume above the ground (m); under 0.04 the decoration is flat.</summary>
        public float Height { get; }

        /// <summary>Height of the top an animal sits or sleeps on (« support »), 0 when none.</summary>
        public float TopHeight { get; }

        /// <summary>Flat decorations (rugs, slabs) are walked on like the ground (doc 05, section 3).</summary>
        public bool IsFlat => Height < FlatHeight;

        public const float FlatHeight = 0.04f;

        /// <summary>The class's circle radius (m): XS 0.20, S 0.35, M 0.60, L 1.00, XL 1.60.</summary>
        public static float ClassRadius(DecorationFootprintClass footprint) =>
            footprint switch { DecorationFootprintClass.XS => 0.2f, DecorationFootprintClass.S => 0.35f, DecorationFootprintClass.M => 0.6f,
                DecorationFootprintClass.L => 1f, _ => 1.6f };

        /// <summary>The class's rectangle half sizes (m): XS 0.40 × 0.25, S 0.70 × 0.40, M 1.20 × 0.60, L 2.00 × 1.10, XL 3.20 × 1.80.</summary>
        public static void ClassRectangle(DecorationFootprintClass footprint, out float halfLength, out float halfWidth)
        {
            switch (footprint)
            {
                case DecorationFootprintClass.XS: halfLength = 0.2f; halfWidth = 0.125f; break;
                case DecorationFootprintClass.S: halfLength = 0.35f; halfWidth = 0.2f; break;
                case DecorationFootprintClass.M: halfLength = 0.6f; halfWidth = 0.3f; break;
                case DecorationFootprintClass.L: halfLength = 1f; halfWidth = 0.55f; break;
                default: halfLength = 1.6f; halfWidth = 0.9f; break;
            }
        }

        /// <summary>Places taken in the zone: 1 for XS to M, 2 for L, 3 for XL.</summary>
        public static int ClassPlaces(DecorationFootprintClass footprint) =>
            footprint == DecorationFootprintClass.XL ? 3 : footprint == DecorationFootprintClass.L ? 2 : 1;
    }
}
