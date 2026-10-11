#nullable enable
using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Content
{
    /// <summary>
    /// Where an accessory hangs on the rig (doc Accessoires: SOCK_HeadTop, SOCK_Neck, SOCK_Back, SOCK_TailBase, SOCK_Ear_L/R).
    /// Same orientation convention across the species of a family. Stable byte values: saved per animal.
    /// </summary>
    public enum AccessorySocket : byte
    {
        HeadTop = 0,
        Neck = 1,
        Back = 2,
        TailBase = 3,
        EarL = 4,
        EarR = 5,
    }

    /// <summary>Local placement of an accessory under its socket (neutral rig space, meters and degrees).</summary>
    [Serializable]
    public struct AccessoryFit
    {
        public Vector3 LocalPosition;
        public Vector3 LocalEuler;
        public Vector3 LocalScale;

        public AccessoryFit(Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalEuler = localEuler;
            LocalScale = localScale;
        }

        /// <summary>No offset, no turn, scale 1.</summary>
        public static AccessoryFit Identity => new AccessoryFit(Vector3.zero, Vector3.zero, Vector3.one);
    }

    /// <summary>The fitting profile of one rig family.</summary>
    public readonly struct AccessoryFamilyFit
    {
        public readonly RigFamily Family;
        public readonly AccessoryFit Fit;

        public AccessoryFamilyFit(RigFamily family, AccessoryFit fit)
        {
            Family = family;
            Fit = fit;
        }
    }

    /// <summary>A measured fit for one species (written later by the fitting pipeline). Wins over its family's profile.</summary>
    public readonly struct AccessorySpeciesFit
    {
        public readonly ContentId Species;
        public readonly AccessoryFit Fit;

        public AccessorySpeciesFit(ContentId species, AccessoryFit fit)
        {
            Species = species;
            Fit = fit;
        }
    }

    /// <summary>
    /// T47 accessory (doc Accessoires, M-DEF-ACCESSORY): a rigid mesh modeled once in neutral rig space, child of one socket,
    /// with a fitting profile per compatible rig family plus measured per-species fits; species whose clipping cannot be solved
    /// are excluded. No per-species duplicate mesh, one shared accessory material, no cloth and no physics chain. Its id is the
    /// id of the <see cref="EntitlementDefinition"/> (kind Cosmetic) the player owns: owning the item is owning the accessory.
    /// </summary>
    public sealed class AccessoryDefinition : IContentDefinition
    {
        /// <summary>Budgets of the doc: small 40 to 250 triangles, medium 150 to 500, a large back piece under 700.</summary>
        public const int MaxTris = 700;

        public AccessoryDefinition(ContentId id, string displayKey, AccessorySocket socket, AccessoryFamilyFit[] familyFits,
            AccessorySpeciesFit[]? speciesFits, ContentId[]? excludedSpecies, string[] palettes, int trisBudget, string iconKey,
            string visualKey, string sourceKey)
        {
            Id = id;
            DisplayKey = displayKey;
            Socket = socket;
            FamilyFits = familyFits ?? Array.Empty<AccessoryFamilyFit>();
            SpeciesFits = speciesFits ?? Array.Empty<AccessorySpeciesFit>();
            ExcludedSpecies = excludedSpecies ?? Array.Empty<ContentId>();
            Palettes = palettes ?? Array.Empty<string>();
            TrisBudget = trisBudget;
            IconKey = iconKey;
            VisualKey = visualKey;
            SourceKey = sourceKey;
        }

        /// <summary>Same id as the owned item (entitlement), e.g. cosmetic.flower_crown.</summary>
        public ContentId Id { get; }

        public string DisplayKey { get; }
        public AccessorySocket Socket { get; }

        /// <summary>Compatible rig families and their fitting profile. A family not listed never wears it.</summary>
        public AccessoryFamilyFit[] FamilyFits { get; }

        /// <summary>Per-species measured fits (fitting pipeline). Empty until measured: the family profile is used.</summary>
        public AccessorySpeciesFit[] SpeciesFits { get; }

        /// <summary>Species that never wear it (clipping that no fit solves, or a socket the species hides).</summary>
        public ContentId[] ExcludedSpecies { get; }

        /// <summary>Palette names of the shared accessory material (meadow, blossom, frost). The saved palette is an index here: append only.</summary>
        public string[] Palettes { get; }

        public int TrisBudget { get; }
        public string IconKey { get; }

        /// <summary>Key of the accessory prefab in the presentation catalog.</summary>
        public string VisualKey { get; }

        /// <summary>Editable source (handmade rule): the .blend and its script, listed in SourceArt/Handmade_Sources.json.</summary>
        public string SourceKey { get; }

        public bool FitsFamily(RigFamily family)
        {
            for (int i = 0; i < FamilyFits.Length; i++)
                if (FamilyFits[i].Family == family)
                    return true;
            return false;
        }

        public bool Excludes(ContentId species) => Array.IndexOf(ExcludedSpecies, species) >= 0;

        /// <summary>The fit for a species: its measured fit first, else its family profile. False when the family is not compatible.</summary>
        public bool TryGetFit(ContentId species, RigFamily family, out AccessoryFit fit)
        {
            for (int i = 0; i < SpeciesFits.Length; i++)
            {
                if (SpeciesFits[i].Species == species)
                {
                    fit = SpeciesFits[i].Fit;
                    return true;
                }
            }
            for (int i = 0; i < FamilyFits.Length; i++)
            {
                if (FamilyFits[i].Family == family)
                {
                    fit = FamilyFits[i].Fit;
                    return true;
                }
            }
            fit = AccessoryFit.Identity;
            return false;
        }

        /// <summary>Index of a palette name, or -1.</summary>
        public int PaletteIndex(string palette) => Array.IndexOf(Palettes, palette);
    }
}
