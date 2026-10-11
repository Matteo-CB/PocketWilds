using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T48B the season looks and texts of the editor pipelines: every season key has its English and French text (no em dash),
    /// the 4 looks exist, Soleil keeps the validated base look, only vegetation and ground rows change (never rocks, water,
    /// wood or props) and inside a row only green colors move (bark, petals, fruit, paths keep theirs), Givre has no rain.
    /// </summary>
    public class SeasonLookPipelineTests
    {
        private static readonly string[] FixedWords = { "rock", "pebble", "wood", "hay", "stone", "water", "windmill", "well", "fence", "mushroom", "life_" };

        [Test]
        public void EverySeasonKey_HasEnglishAndFrench_WithoutEmDash()
        {
            var keys = new HashSet<string>();
            for (int i = 0; i < SeasonTexts.Texts.GetLength(0); i++)
            {
                string key = SeasonTexts.Texts[i, 0];
                Assert.IsTrue(keys.Add(key), "Duplicate key " + key);
                for (int language = 1; language <= 2; language++)
                {
                    string text = SeasonTexts.Texts[i, language];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(text), key + " is empty.");
                    Assert.IsFalse(text.Contains('—'), key + " has an em dash.");
                }
            }
            foreach (var key in SeasonText.All)
                Assert.IsTrue(keys.Contains(key), "No text for " + key);
            Assert.AreEqual(6, SeasonUiPipeline.KindIcons.Length, "One chip icon per weekend kind.");
        }

        [Test]
        public void SeasonLooks_ChangeOnlyVegetationGreens()
        {
            var set = AssetDatabase.LoadAssetAtPath<SeasonLookSet>(SeasonLookPipeline.LooksPath);
            if (set == null)
                Assert.Ignore("Run Pocket Wilds/World/Build Season Looks (T48B) first.");
            var palettes = AssetDatabase.LoadAssetAtPath<PaletteSetAsset>(GrasslandPipeline.PalettesPath);
            var baseColors = palettes.palettes.Where(e => e != null).ToDictionary(e => e.sourceId, e => e.ToRuntime().Colors);
            foreach (var season in new[] { RefugeSeason.Awakening, RefugeSeason.Sun, RefugeSeason.Harvest, RefugeSeason.Frost })
                Assert.IsNotNull(set.Get(season), "Missing look " + season);
            Assert.AreEqual(0, set.Get(RefugeSeason.Sun).palettes.Length, "Soleil is the validated look.");
            Assert.IsFalse(set.Get(RefugeSeason.Frost).rain, "Givre: no rain.");
            Assert.IsFalse(set.Get(RefugeSeason.Frost).rainbow, "Givre: no rainbow.");
            Assert.IsTrue(set.Get(RefugeSeason.Sun).fireflies, "Soleil evenings: fireflies.");
            Assert.LessOrEqual(set.maxDrift, SeasonDrift.MaxInstances);
            foreach (var look in set.looks)
            {
                foreach (var row in look.palettes)
                {
                    string name = row.paletteId.Substring(row.paletteId.LastIndexOf('.') + 1);
                    Assert.IsFalse(FixedWords.Any(name.Contains), $"{look.season} changes {row.paletteId}, which never changes.");
                    Assert.IsTrue(baseColors.TryGetValue(row.paletteId, out var source), row.paletteId + " is not a Grassland palette.");
                    Assert.AreEqual(PaletteTable.Columns, row.colors.Length);
                    for (int c = 0; c < source.Length; c++)
                    {
                        Color.RGBToHSV(source[c], out float h, out float s, out float v);
                        bool green = h >= 0.16f && h <= 0.48f && s >= 0.15f && v >= 0.12f;
                        bool tip = name.Contains("grass") && c == 2;
                        bool petal = look.season == RefugeSeason.Awakening && name.Contains("flower") && !name.Contains("bush");
                        if (green || tip || petal)
                            continue;
                        var seasonal = row.colors[c];
                        Assert.IsTrue(seasonal.r == source[c].r && seasonal.g == source[c].g && seasonal.b == source[c].b,
                            $"{look.season} {row.paletteId} column {c}: only greens move.");
                    }
                }
            }
        }

        [Test]
        public void VerticalSlice_ReadsTheSeasonLooks()
        {
            // The scene file itself: a reference lost on save (an asset unloaded by the scene switch) is written as fileID 0.
            string guid = AssetDatabase.AssetPathToGUID(SeasonLookPipeline.LooksPath);
            Assert.IsFalse(string.IsNullOrEmpty(guid), "DEF_SeasonLooks exists (Pocket Wilds/World/Build Season Looks (T48B)).");
            string scene = System.IO.File.ReadAllText(UiPipeline.ScenePath);
            StringAssert.Contains("_seasonLooks: {fileID: 11400000, guid: " + guid, scene, "VerticalSliceEntry._seasonLooks is wired.");
        }

        [Test]
        public void SeasonCardTitle_FitsTheModal_InEveryLanguage()
        {
            // Text spacing rule: the card's title at the modal title's smallest size, 8 inside its box on each side.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FeedbackUiPipeline.ModalPrefabPath);
            Assert.IsNotNull(prefab, FeedbackUiPipeline.ModalPrefabPath);
            var title = prefab.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.name == "Title");
            float width = title.rectTransform.sizeDelta.x;
            var card = (RectTransform)title.rectTransform.parent;
            Assert.LessOrEqual(width, card.sizeDelta.x - 2f * 40f + 0.5f, "The title box keeps 40 from the card's edges.");
            float size = title.enableAutoSizing ? title.fontSizeMin : title.fontSize;
            var texts = new Dictionary<string, string[]>();
            for (int i = 0; i < SeasonTexts.Texts.GetLength(0); i++)
                texts[SeasonTexts.Texts[i, 0]] = new[] { SeasonTexts.Texts[i, 1], SeasonTexts.Texts[i, 2] };
            float keep = title.fontSize;
            bool auto = title.enableAutoSizing;
            title.enableAutoSizing = false;
            title.fontSize = size;
            try
            {
                foreach (RefugeSeason season in new[] { RefugeSeason.Awakening, RefugeSeason.Sun, RefugeSeason.Harvest, RefugeSeason.Frost })
                    for (int language = 0; language < 2; language++)
                    {
                        string text = texts[SeasonText.CardTitle(season)][language];
                        float preferred = title.GetPreferredValues(text, 0f, 0f).x;
                        Assert.LessOrEqual(preferred, width - 16f, "'" + text + "' at " + size + " pt");
                    }
            }
            finally
            {
                title.fontSize = keep;
                title.enableAutoSizing = auto;
            }
        }
    }
}
