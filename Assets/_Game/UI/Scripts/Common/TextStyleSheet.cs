using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>Text roles of the type system (doc 05, Styles). Every label of the game uses one of them.</summary>
    public enum TextRole
    {
        /// <summary>Pocket Plush with outline and soft shadow: reveals, event names, the biggest titles.</summary>
        Display,
        /// <summary>Pocket Plush: screen titles, offer and species names.</summary>
        Heading,
        /// <summary>Pocket Round Bold: section titles.</summary>
        Subheading,
        /// <summary>Pocket Round Regular: sentences.</summary>
        Body,
        /// <summary>Pocket Round Bold: button labels.</summary>
        Button,
        /// <summary>Pocket Round Bold: names on cards, chips and badges.</summary>
        Label,
        /// <summary>Pocket Plush with a soft shadow: gains and main counters.</summary>
        NumberLarge,
        /// <summary>Pocket Round Bold: prices, costs, secondary counters.</summary>
        NumberSmall,
        /// <summary>Pocket Round SemiBold: metadata and secondary lines.</summary>
        Caption,
    }

    /// <summary>
    /// The game's text style sheet (DEF_TextStyles): font asset, material preset and size range per role. Sizes are
    /// canvas units at the 1080 x 2340 reference; nothing readable goes below <see cref="MinimumReadableSize"/>.
    /// UI pipelines apply it when they build prefabs; runtime code can apply it to labels it creates.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/UI/Text Style Sheet", fileName = "DEF_TextStyles")]
    public sealed class TextStyleSheet : ScriptableObject
    {
        /// <summary>36 canvas units: 14 dp on the reference phone (1080 px wide, 2.625 px per dp), 12 dp on 720 x 1520.</summary>
        public const float MinimumReadableSize = 36f;

        [Serializable]
        public struct Style
        {
            public TextRole role;
            public TMP_FontAsset font;
            [Tooltip("Material preset of that font (outline, shadow) or empty for the font's own material.")]
            public Material material;
            public float minSize;
            public float maxSize;
            [Tooltip("Extra spacing in em/100, as TMP characterSpacing.")]
            public float characterSpacing;
        }

        public List<Style> styles = new List<Style>();

        public bool TryGet(TextRole role, out Style style)
        {
            for (int i = 0; i < styles.Count; i++)
            {
                if (styles[i].role == role)
                {
                    style = styles[i];
                    return true;
                }
            }
            style = default;
            return false;
        }

        /// <summary>Font, material and spacing of the role; the size is kept inside the role's range.</summary>
        public void Apply(TMP_Text text, TextRole role, float size)
        {
            if (!TryGet(role, out var style) || style.font == null)
                throw new InvalidOperationException($"{name}: no style for {role}.");
            text.font = style.font;
            text.fontSharedMaterial = style.material != null ? style.material : style.font.material;
            text.fontStyle = FontStyles.Normal; // weights come from the font assets, never from faux bold
            text.characterSpacing = style.characterSpacing;
            text.fontSize = Mathf.Clamp(size, style.minSize, style.maxSize);
        }

        /// <summary>Auto-size inside [min, size] with min never below the role minimum.</summary>
        public void ApplyAutoSize(TMP_Text text, TextRole role, float size, float min)
        {
            Apply(text, role, size);
            TryGet(role, out var style);
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Min(text.fontSize, Mathf.Max(min, style.minSize));
        }
    }
}
