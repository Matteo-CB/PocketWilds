using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Editor.UI;
using PocketWilds.Presentation;
using PocketWilds.Services.Localization;
using PocketWilds.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T34A screens (Gate 12, UI DONE rule): structure of the sheet, modal and splash prefabs, the HUD's feedback
    /// parts, touch targets of at least 48 dp, rarity never by color alone, and every new label surviving a 30 %
    /// longer translation at the role's minimum size.
    /// </summary>
    public class FeedbackScreenTests
    {
        /// <summary>48 dp on the 1080 x 2340 reference (36 units = 14 dp).</summary>
        private const float MinTouchUnits = 48f * 36f / 14f;

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        [Test]
        public void ScreensExist_WithTheirOwnOverlayCanvas_InTheRightOrder()
        {
            var sheet = Load(FeedbackUiPipeline.SheetPrefabPath);
            var modal = Load(FeedbackUiPipeline.ModalPrefabPath);
            var splash = Load(FeedbackUiPipeline.SplashPrefabPath);
            Assert.IsNotNull(sheet, "Run Pocket Wilds/UI/Build Feedback Screens (T34A).");
            Assert.IsNotNull(modal);
            Assert.IsNotNull(splash);
            int hud = Load(UiPipeline.HudPrefabPath).GetComponent<Canvas>().sortingOrder;
            int codex = Load(UiPipeline.CodexPrefabPath).GetComponent<Canvas>().sortingOrder;
            int offline = Load(UiPipeline.OfflineReturnPrefabPath).GetComponent<Canvas>().sortingOrder;
            Assert.Greater(sheet.GetComponent<Canvas>().sortingOrder, codex, "The sheet sits above the Codex and shop.");
            Assert.Less(sheet.GetComponent<Canvas>().sortingOrder, offline);
            Assert.Greater(modal.GetComponent<Canvas>().sortingOrder, offline, "Modals sit above every screen.");
            Assert.Greater(splash.GetComponent<Canvas>().sortingOrder, modal.GetComponent<Canvas>().sortingOrder);
            Assert.Greater(sheet.GetComponent<Canvas>().sortingOrder, hud);
            foreach (var screen in new[] { sheet, modal })
            {
                Assert.IsFalse(screen.GetComponent<Canvas>().enabled, screen.name + ": a closed screen costs nothing.");
                Assert.IsFalse(screen.transform.Find("Content").gameObject.activeSelf, screen.name + ": content saved inactive.");
                Assert.IsEmpty(screen.GetComponentsInChildren<LayoutGroup>(true), screen.name + ": no layout group.");
                Assert.IsEmpty(screen.GetComponentsInChildren<Animator>(true));
            }
            Assert.IsNull(splash.GetComponent<GraphicRaycaster>(), "The boot screen takes no touch.");
        }

        [Test]
        public void EveryButton_IsAtLeast48dp()
        {
            foreach (var path in new[] { UiPipeline.HudPrefabPath, FeedbackUiPipeline.SheetPrefabPath, FeedbackUiPipeline.ModalPrefabPath })
            {
                foreach (var button in Load(path).GetComponentsInChildren<Button>(true))
                {
                    var rect = (RectTransform)button.transform;
                    var size = rect.rect.size;
                    if (button.name == "Scrim")
                        continue;
                    Assert.GreaterOrEqual(Mathf.Min(size.x, size.y), MinTouchUnits - 0.5f,
                        $"{path}: {button.transform.parent.name}/{button.name} is {size} units (48 dp = {MinTouchUnits:F0}).");
                }
            }
        }

        [Test]
        public void HudFeedbackParts_ArePassive_ExceptTheOddsButton()
        {
            var hud = Load(UiPipeline.HudPrefabPath);
            foreach (var part in new[] { "SafeArea/RevealBanner", "SafeArea/Toast", "TapHint", "CollectMotes" })
            {
                var t = hud.transform.Find(part);
                Assert.IsNotNull(t, part);
                Assert.IsNotNull(t.GetComponent<Canvas>(), part + ": own canvas (animates without rebuilding the HUD).");
                Assert.IsFalse(t.gameObject.activeSelf, part + ": hidden until used.");
                Assert.IsFalse(t.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), part + ": takes no touch.");
            }
            Assert.IsNotNull(hud.transform.Find("SafeArea/HatchAction/Info/Button").GetComponent<Button>(), "Egg odds button next to the hatch.");
        }

        [Test]
        public void Rarity_IsShapeStarsAndWord_NotColorAlone()
        {
            var set = AssetDatabase.LoadAssetAtPath<UiRaritySet>(FeedbackUiPipeline.RaritySetPath);
            Assert.IsNotNull(set);
            Assert.AreEqual(7, set.frames.Length);
            Assert.AreEqual(7, set.badges.Length);
            Assert.AreEqual(6, set.traits.Length);
            Assert.AreEqual(set.frames.Length, set.frames.Distinct().Count(), "Each rarity has its own frame motif.");
            Assert.AreEqual(set.badges.Length, set.badges.Distinct().Count(), "Each rarity has its own badge motif.");
            Assert.IsTrue(set.frames.Concat(set.badges).Concat(set.traits).All(s => s != null));
            Assert.IsNotNull(set.favoriteOn);
            Assert.AreNotEqual(set.favoriteOn, set.favoriteOff, "Favorite on and off differ by shape (filled, outline).");
            var sheet = Load(FeedbackUiPipeline.SheetPrefabPath).GetComponent<AnimalSheetScreen>();
            var stars = Load(FeedbackUiPipeline.SheetPrefabPath).transform.Find("Content/SafeArea/Card/Star5");
            Assert.IsNotNull(stars, "Six star slots: the rarity is also counted in stars.");
            Assert.IsNotNull(Load(FeedbackUiPipeline.SheetPrefabPath).transform.Find("Content/SafeArea/Card/Rarity"), "And named.");
            Assert.IsNotNull(sheet);
        }

        /// <summary>
        /// Localization expansion (Gate 12): every T34A label takes the longest shipped text plus 30 % and still fits
        /// its box at the role's minimum size (auto-size labels) or wraps inside it (the modal body).
        /// </summary>
        [Test]
        public void Labels_SurviveThirtyPercentLongerText()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(UiPipeline.ContentDatabasePath);
            string Longest(params string[] keys)
            {
                string best = string.Empty;
                foreach (string language in UiPipeline.Languages)
                {
                    var text = new LocalizationService(database.localization, language, SystemLanguage.English);
                    foreach (var key in keys)
                    {
                        string value = text.Get(key);
                        if (value.Length > best.Length)
                            best = value;
                    }
                }
                return best + new string('e', Mathf.CeilToInt(best.Length * 0.3f));
            }

            var cases = new List<(string prefab, string label, string text)>
            {
                (UiPipeline.HudPrefabPath, "SafeArea/RevealBanner/Title", Longest(FeedbackText.RevealNewSpecies, FeedbackText.RevealNewVariant, FeedbackText.RarityLegendary, FeedbackText.RarityUncommon)),
                (UiPipeline.HudPrefabPath, "SafeArea/RevealBanner/Name", Longest("species.hedgehog.name") + " · " + Longest("variant.hedgehog.mushroom.name")),
                (UiPipeline.HudPrefabPath, "SafeArea/Toast/Label", Longest(FeedbackText.ToastNotEnoughLight, FeedbackText.ToastShopOpen)),
                (FeedbackUiPipeline.SheetPrefabPath, "Content/SafeArea/Card/Name", Longest("species.hedgehog.name", "species.squirrel.name")),
                (FeedbackUiPipeline.SheetPrefabPath, "Content/SafeArea/Card/Species", Longest("species.hedgehog.name") + " · " + Longest("variant.hedgehog.mushroom.name")),
                (FeedbackUiPipeline.SheetPrefabPath, "Content/SafeArea/Card/Rarity", Longest(FeedbackText.RarityLegendary, FeedbackText.RarityUncommon)),
                (FeedbackUiPipeline.SheetPrefabPath, "Content/SafeArea/Card/BondLabel", Longest(FeedbackText.SheetBond)),
                (FeedbackUiPipeline.SheetPrefabPath, "Content/SafeArea/Card/Details/Label", Longest(FeedbackText.SheetDetailsButton)),
                (FeedbackUiPipeline.ModalPrefabPath, "Content/Card/Title", Longest(FeedbackText.EggOddsTitle)),
                (FeedbackUiPipeline.ModalPrefabPath, "Content/Card/Confirm/Label", Longest(FeedbackText.ModalOkButton, FeedbackText.ModalCancelButton)),
                // T36: the wish card's buttons and the sheet's treat button.
                (UiPipeline.HudPrefabPath, "WishCard/Content/Card/Claim/Label", Longest(DailyVisitText.WishClaim)),
                (UiPipeline.HudPrefabPath, "WishCard/Content/Card/Swap/Label", Longest(DailyVisitText.WishSwap)),
                (FeedbackUiPipeline.SheetPrefabPath, "Content/SafeArea/Card/Treat/Button/Label", Longest(DailyVisitText.SheetTreat)),
                // T37: the activity screens' one-line labels (found by the end of their path).
                (ActivityUiPipeline.HubPath, "Play/Label", Longest(ActivityText.HubPlay)),
                (ActivityUiPipeline.HubPath, "Practice/Label", Longest(ActivityText.Practice)),
                (ActivityUiPipeline.HubPath, "Details/Name", Longest(ActivityNames())),
                (ActivityUiPipeline.HudPath, "Resume/Label", Longest(ActivityText.PauseResume)),
                (ActivityUiPipeline.HudPath, "Quit/Label", Longest(ActivityText.PauseQuit)),
                (ActivityUiPipeline.HudPath, "Streak/Label", Longest(ActivityText.HudStreak)),
                (ActivityUiPipeline.ResultPath, "Replay/Label", Longest(ActivityText.ResultReplay)),
                (ActivityUiPipeline.ResultPath, "Back/Label", Longest(ActivityText.ResultBack)),
                (ActivityUiPipeline.ResultPath, "Record/Label", Longest(ActivityText.ResultRecord)),
                (ActivityUiPipeline.ResultPath, "Card/Title", Longest(ActivityText.ResultTitleNone, ActivityText.ResultTitleOne,
                    ActivityText.ResultTitleTwo, ActivityText.ResultTitleThree)),
                (ActivityUiPipeline.TicketsEmptyPath, "Practice/Label", Longest(ActivityText.EmptyPractice)),
                (ActivityUiPipeline.TicketsEmptyPath, "Card/Title", Longest(ActivityText.EmptyTitle)),
                // T38: the HUD's tip bubble (its widest box) and the sheet's « Brosser » button with its « Gratuit » tag.
                (ActivityUiPipeline.HudPath, "Tip/Label", Longest(ActivityText.GroomingTipBrush, ActivityText.GroomingTipLeaves,
                    ActivityText.GroomingTipBurr, ActivityText.GroomingTipBow, ActivityText.GroomingTipBubble)),
                (FeedbackUiPipeline.SheetPrefabPath, "Content/SafeArea/Card/Groom/Button/Label", Longest(ActivityText.SheetGroom)),
                (FeedbackUiPipeline.SheetPrefabPath, "Content/SafeArea/Card/Groom/Button/Free/Label", Longest(ActivityText.HubFree)),
            };
            var problems = new List<string>();
            foreach (var group in cases.GroupBy(c => c.prefab))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load(group.Key));
                try
                {
                    foreach (var c in group)
                    {
                        var label = instance.transform.Find(c.label)?.GetComponent<TMP_Text>() ?? FindByPathEnd(instance.transform, c.label);
                        if (label == null)
                        {
                            problems.Add(c.label + ": missing");
                            continue;
                        }
                        label.gameObject.SetActive(true);
                        label.text = c.text;
                        float width = label.rectTransform.rect.width;
                        float size = label.enableAutoSizing ? label.fontSizeMin : label.fontSize;
                        label.enableAutoSizing = false;
                        label.fontSize = size;
                        float preferred = label.GetPreferredValues(c.text, float.PositiveInfinity, float.PositiveInfinity).x;
                        if (preferred > width + 1f)
                            problems.Add($"{group.Key} {c.label}: \"{c.text}\" needs {preferred:F0} at {size}, box {width:F0}");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>The 18 activity names' keys (the hub's details title shows the selected one).</summary>
        private static string[] ActivityNames()
        {
            var keys = new string[PocketWilds.Simulation.ActivityCatalog.ActivityCount];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = ActivityText.NameKey(PocketWilds.Simulation.ActivityCatalog.IdOfSlot(i));
            return keys;
        }

        /// <summary>The first label whose path ends with <paramref name="end"/> (« Play/Label »), inactive ones included.</summary>
        private static TMP_Text FindByPathEnd(Transform root, string end)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                string path = text.name;
                for (var t = text.transform.parent; t != null && t != root; t = t.parent)
                    path = t.name + "/" + path;
                if (path == end || path.EndsWith("/" + end, System.StringComparison.Ordinal))
                    return text;
            }
            return null;
        }
    }
}
