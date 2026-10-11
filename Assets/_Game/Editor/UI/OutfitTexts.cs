using System.Collections.Generic;
using PocketWilds.Presentation;
using TMPro;
using UnityEngine;

namespace PocketWilds.Editor.UI
{
    /// <summary>
    /// T47 texts (EN, FR): the outfit picker (outfit.*), the Codex's Companions tab (codex.*). Writing Guide: short, calm, no
    /// pressure words; placeholders, never sentences built from fragments.
    /// </summary>
    public static class OutfitTexts
    {
        public static readonly string[,] Texts =
        {
            { OutfitText.SheetButton, "Outfit", "Tenue" },
            { OutfitText.Title, "Outfit", "Tenue" },
            { OutfitText.SlotHeadTop, "Head", "Tête" },
            { OutfitText.SlotNeck, "Neck", "Cou" },
            { OutfitText.SlotBack, "Back", "Dos" },
            { OutfitText.SlotTailBase, "Tail", "Queue" },
            { OutfitText.SlotLocked, "Nothing for this spot yet", "Rien pour cet endroit, pour l'instant" },
            { OutfitText.SlotHidden, "Not for this animal", "Pas pour cet animal" },
            { OutfitText.Remove, "Remove", "Retirer" },
            { OutfitText.Empty, "No accessories yet.", "Pas encore d'accessoire." },
            { OutfitText.ToShop, "See the pack", "Voir le pack" },
            { OutfitText.PalettePrefix + "meadow", "Meadow", "Prairie" },
            { OutfitText.PalettePrefix + "blossom", "Blossom", "Fleurs" },
            { OutfitText.PalettePrefix + "frost", "Frost", "Givre" },
            { OutfitText.CodexSpeciesTab, "Species", "Espèces" },
            { OutfitText.CodexCompanionsTab, "Companions", "Compagnons" },
            { OutfitText.CodexLooks, "Looks", "Couleurs" },
            { OutfitText.CodexRewardLook, "New look", "Nouvelle couleur" },
            { OutfitText.CodexRewardAccessory, "Accessory", "Accessoire" },
            { OutfitText.CodexRewardPose, "Photo pose", "Pose photo" },
            { OutfitText.CodexRewardTitle, "Title", "Titre" },
            { OutfitText.CodexBonusPrefix + "fieldhamster", "Light +5% for Meadow animals", "Lumière +5 % pour les animaux de la Prairie" },
        };

        private static Dictionary<string, string>[] _lookup;

        /// <summary>The text of a key in one language (0 en, 1 fr) from every table the localization builds; the key when unknown.</summary>
        public static string Lookup(string key, int language)
        {
            if (_lookup == null)
            {
                _lookup = new[] { new Dictionary<string, string>(), new Dictionary<string, string>() };
                Add(UiPipeline.Texts);
                Add(Texts);
                Add(ProfileScreenTexts.ContentNames());
            }
            return _lookup[language].TryGetValue(key, out var text) ? text : key;
        }

        private static void Add(string[,] table)
        {
            for (int i = 0; i < table.GetLength(0); i++)
            {
                _lookup[0][table[i, 0]] = table[i, 1];
                _lookup[1][table[i, 0]] = table[i, 2];
            }
        }

        /// <summary>
        /// Text spacing rule: measures a label with every key in both languages at its smallest size, inside its box less 8 on each
        /// side (wrapped labels: within their height). Leaves the widest text in the label (the margin tests measure what is saved).
        /// Returns the problems, empty when everything fits.
        /// </summary>
        public static string Fit(TMP_Text label, params string[] keys)
        {
            var problems = new System.Text.StringBuilder();
            var box = label.rectTransform.rect.size;
            if (box.x <= 0f)
                box = label.rectTransform.sizeDelta;
            float size = label.enableAutoSizing ? label.fontSizeMin : label.fontSize;
            float keep = label.fontSize;
            // TMP measures an auto-sized label at its largest size: measure at the smallest one with auto size off.
            bool auto = label.enableAutoSizing;
            label.enableAutoSizing = false;
            bool wrap = label.textWrappingMode != TextWrappingModes.NoWrap;
            string widest = label.text;
            float widestWidth = -1f;
            label.fontSize = size;
            foreach (var key in keys)
            {
                for (int language = 0; language < UiPipeline.Languages.Length; language++)
                {
                    string text = Lookup(key, language).Replace("{0}", "13 000");
                    var preferred = label.GetPreferredValues(text, wrap ? box.x - 16f : 0f, 0f);
                    bool fits = wrap ? preferred.y <= box.y + 0.5f : preferred.x <= box.x - 16f;
                    if (!fits)
                        problems.Append($"{label.name} '{text}' {preferred.x:F0}x{preferred.y:F0} in {box.x:F0}x{box.y:F0}; ");
                    if (preferred.x > widestWidth)
                    {
                        widestWidth = preferred.x;
                        widest = text;
                    }
                }
            }
            label.fontSize = keep;
            label.enableAutoSizing = auto;
            label.text = widest;
            return problems.ToString();
        }
    }
}
