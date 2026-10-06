using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Editor.Shop;
using PocketWilds.Editor.UI;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T32 (doc 05 styles, doc 06 typography): every UI label uses the project fonts through DEF_TextStyles, at a
    /// readable size, with real weights and the font's own material presets; LiberationSans is gone from the project.
    /// </summary>
    public class UiTypographyTests
    {
        private static readonly string[] UiPrefabs =
        {
            UiPipeline.HudPrefabPath, UiPipeline.OfflineReturnPrefabPath, UiPipeline.CodexPrefabPath,
            ShopUiPipeline.CardPrefabPath, ShopUiPipeline.WideCardPrefabPath, ShopUiPipeline.ShopPrefabPath,
            ShopUiPipeline.OfferModalPrefabPath,
            FeedbackUiPipeline.SheetPrefabPath, FeedbackUiPipeline.ModalPrefabPath, FeedbackUiPipeline.SplashPrefabPath,
        };

        private static HashSet<TMP_FontAsset> ProjectFonts()
        {
            var fonts = new HashSet<TMP_FontAsset>();
            foreach (string weight in FontPipeline.RoundWeights)
                fonts.Add(FontPipeline.Load(FontPipeline.Round, weight));
            foreach (string weight in FontPipeline.PlushWeights)
                fonts.Add(FontPipeline.Load(FontPipeline.Plush, weight));
            return fonts;
        }

        [Test]
        public void StyleSheet_HasEveryRole_WithProjectFontsAndReadableSizes()
        {
            var sheet = FontPipeline.LoadStyles();
            var fonts = ProjectFonts();
            foreach (TextRole role in Enum.GetValues(typeof(TextRole)))
            {
                Assert.IsTrue(sheet.TryGet(role, out var style), "No style for " + role);
                Assert.IsTrue(fonts.Contains(style.font), role + ": project font");
                Assert.GreaterOrEqual(style.minSize, TextStyleSheet.MinimumReadableSize, role + ": readable minimum");
                Assert.Greater(style.maxSize, style.minSize, role.ToString());
                if (style.material != null)
                    Assert.AreSame(style.font.atlasTexture, style.material.mainTexture, role + ": a preset of the same font");
            }
            Assert.AreSame(FontPipeline.Load(FontPipeline.Plush, "Black"), Style(sheet, TextRole.Heading).font, "Titles use Pocket Plush (owner's choice).");
            Assert.AreSame(FontPipeline.Load(FontPipeline.Round, "Regular"), Style(sheet, TextRole.Body).font);
        }

        private static TextStyleSheet.Style Style(TextStyleSheet sheet, TextRole role)
        {
            sheet.TryGet(role, out var style);
            return style;
        }

        [Test]
        public void EveryUiLabel_UsesProjectFonts_RealWeights_ReadableSizes()
        {
            var fonts = ProjectFonts();
            int labels = 0;
            foreach (string path in UiPrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    string where = path + " " + text.name;
                    labels++;
                    Assert.IsTrue(fonts.Contains(text.font), where + ": " + (text.font != null ? text.font.name : "no font"));
                    Assert.AreSame(text.font.atlasTexture, text.fontSharedMaterial.mainTexture, where + ": material of its own font");
                    Assert.AreEqual(FontStyles.Normal, text.fontStyle & (FontStyles.Bold | FontStyles.Italic), where + ": no faux bold or italic");
                    float smallest = text.enableAutoSizing ? text.fontSizeMin : text.fontSize;
                    Assert.GreaterOrEqual(smallest, TextStyleSheet.MinimumReadableSize, where + ": readable size");
                }
            }
            Assert.Greater(labels, 40, "The UI prefabs should hold the game's labels.");
        }

        [Test]
        public void LiberationSans_IsGone_AndTmpDefaultsToPocketRound()
        {
            Assert.IsEmpty(AssetDatabase.FindAssets("LiberationSans"), "TEMP font removed (doc 06).");
            Assert.AreSame(FontPipeline.Load(FontPipeline.Round, "Regular"), TMP_Settings.defaultFontAsset);
            Assert.IsTrue(TMP_Settings.fallbackFontAssets == null || TMP_Settings.fallbackFontAssets.Count == 0,
                "No fallback font: the static atlases cover the doc 05 character set.");
        }

        [Test]
        public void TheSliceScene_LoadsOnlyProjectFonts()
        {
            var fonts = ProjectFonts().Select(AssetDatabase.GetAssetPath).ToHashSet();
            foreach (string dependency in AssetDatabase.GetDependencies(UiPipeline.ScenePath, true))
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(dependency) == typeof(TMP_FontAsset))
                    Assert.IsTrue(fonts.Contains(dependency), "Unexpected font in the scene: " + dependency);
            }
        }
    }
}
