using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.UI;
using TMPro;
using UnityEditor;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T31/T32 (doc 05): Pocket Round and Pocket Plush show every shipped string, every store price format and every game
    /// number without a missing glyph, from static atlases (no glyph generation while the game runs).
    /// </summary>
    public class FontCoverageTests
    {
        /// <summary>
        /// Prices as the stores print them for the doc 05 languages and currencies, separators included
        /// (no-break space U+00A0, narrow no-break space U+202F, typographic apostrophe U+2019).
        /// </summary>
        public static readonly string[] PriceSamples =
        {
            "1,99 €", "€1.99", "1.99 €", "$1.99", "US$1.99", "CA$2.49", "A$2.99", "NZ$2.99", "HK$15.00", "S$2.98",
            "MX$39.00", "R$ 9,90", "R$ 9,90", "£1.79", "¥300", "CN¥14.00", "₩2,500", "₹149.00", "₽199", "199 ₽",
            "₺49,99", "49,99 TL", "₫49.000", "49.000 ₫", "₱99.00", "₪9.90", "9.90 ₪", "฿39.00", "₦900.00", "₴49,00",
            "49,00 ₴", "₸990", "990 ₸", "₡990", "₲9.900", "GH₵19.99", "Rp 15.000", "RM 8.90", "8,99 zł", "29,00 kr",
            "kr 29,00", "49,90 Kč", "1 990 Ft", "1 990 Ft", "4,99 lei", "CHF 2.00", "CHF 1’000.00", "R 34,99",
            "ZAR 34.99", "SAR 7.99", "AED 7.99", "KSh 250", "Rs 450", "PKR 450", "1 234,56 €", "¢99",
        };

        /// <summary>Group and decimal separators the game formats numbers with (BigValueFormatter).</summary>
        private static readonly NumberFormatSettings[] NumberFormats =
        {
            NumberFormatSettings.Invariant,
            new NumberFormatSettings(',', ' '),
            new NumberFormatSettings(',', ' '),
            new NumberFormatSettings(',', '.'),
            new NumberFormatSettings('.', '’'),
            new NumberFormatSettings(',', ' '),
        };

        /// <summary>Every shipped font asset with its family and weight.</summary>
        private static IEnumerable<(string family, string weight)> ShippedFonts() =>
            FontPipeline.RoundWeights.Select(w => (FontPipeline.Round, w)).Concat(FontPipeline.PlushWeights.Select(w => (FontPipeline.Plush, w)));

        private static IEnumerable<TMP_FontAsset> FontAssets() => ShippedFonts().Select(f => FontPipeline.Load(f.family, f.weight));

        private static string Missing(TMP_FontAsset font, string text)
        {
            var missing = new StringBuilder();
            foreach (char c in text)
            {
                if (c < ' ' || char.IsSurrogate(c))
                    continue;
                if (!font.HasCharacter(c, false, false) && missing.ToString().IndexOf(c) < 0)
                    missing.Append(c);
            }
            return missing.ToString();
        }

        private static void AssertCovers(string label, IEnumerable<string> texts)
        {
            foreach (var font in FontAssets())
            {
                foreach (string text in texts)
                {
                    string missing = Missing(font, text);
                    Assert.IsEmpty(missing, $"{font.name} misses \"{missing}\" in {label}: \"{text}\"");
                }
            }
        }

        [Test]
        public void ShippedFonts_AreStatic_OneAtlasPerWeight_NoSourceFontInBuilds()
        {
            foreach (var (family, weight) in ShippedFonts())
            {
                var font = FontPipeline.Load(family, weight);
                Assert.IsNotNull(font, "Missing " + FontPipeline.AssetPath(family, weight));
                Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode, font.name);
                Assert.AreEqual(1, font.atlasTextures.Length, font.name + ": one atlas texture");
                Assert.AreEqual(FontPipeline.AtlasSize, font.atlasWidth, font.name);
                Assert.AreEqual(FontPipeline.AtlasSize, font.atlasHeight, font.name);
                Assert.IsFalse(font.atlasTexture.isReadable, font.name + ": no CPU copy of the atlas in builds");
                Assert.IsNull(font.sourceFontFile, font.name + ": a static font asset does not carry the TTF into builds");
                Assert.AreEqual(0f, font.boldSpacing, font.name + ": real weights, no faux-bold spacing");
                Assert.AreEqual(FontPipeline.MobileShader, font.material.shader.name, font.name);
                string kern = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath)!,
                    FontPipeline.SourceFolder, $"{family}-{weight}.kern.tsv");
                int expected = System.IO.File.ReadAllLines(kern).Count(l => l.Length > 0 && l[0] != '#');
                Assert.Greater(expected, 1000, kern);
                Assert.AreEqual(expected, font.fontFeatureTable.glyphPairAdjustmentRecords.Count, font.name + ": every kerning pair of the build");
            }
        }

        [Test]
        public void PocketRound_RegularLinksItsRealSemiBoldAndBold()
        {
            var regular = FontPipeline.Load(FontPipeline.Round, "Regular");
            Assert.AreSame(FontPipeline.Load(FontPipeline.Round, "SemiBold"), regular.fontWeightTable[6].regularTypeface);
            Assert.AreSame(FontPipeline.Load(FontPipeline.Round, "Bold"), regular.fontWeightTable[7].regularTypeface);
        }

        [Test]
        public void ShippedFonts_HaveTheWholeDoc05CharacterSet()
        {
            AssertCovers("charset.txt", new[] { FontPipeline.Charset() });
        }

        [Test]
        public void EveryShippedString_HasAllItsGlyphs()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(UiPipeline.ContentDatabasePath);
            var texts = new List<string>();
            foreach (var table in database.localization)
            {
                foreach (var entry in table.entries)
                    texts.Add(Regex.Replace(entry.value, "<[^>]+>", string.Empty));
            }
            Assert.Greater(texts.Count, 50, "The shipped tables should hold the UI text.");
            AssertCovers("the localization tables", texts);
        }

        [Test]
        public void EveryStorePriceFormat_HasAllItsGlyphs()
        {
            AssertCovers("store prices", PriceSamples);
        }

        [Test]
        public void EveryGameNumberFormat_HasAllItsGlyphs()
        {
            var texts = new List<string>();
            var buffer = new char[BigValueFormatter.MaxLength];
            var values = new[]
            {
                BigValue.FromDouble(0), BigValue.FromDouble(4.5), BigValue.FromDouble(999), BigValue.FromDouble(1250),
                BigValue.FromDouble(12400), BigValue.FromDouble(-86.4), BigValue.FromDouble(2.1e6), BigValue.FromDouble(3.75e9),
                BigValue.FromDouble(1e12), BigValue.FromDouble(4.2e15), BigValue.FromDouble(7.7e200),
                BigValue.FromMantissaExponent(1.23, 3000),
            };
            foreach (var format in NumberFormats)
            {
                foreach (var value in values)
                {
                    int length = BigValueFormatter.Format(value, buffer, 0, format);
                    texts.Add("+" + new string(buffer, 0, length) + "/s ×2 x6 %");
                }
            }
            AssertCovers("game numbers", texts);
        }
    }
}
