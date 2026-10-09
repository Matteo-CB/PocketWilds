using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Localization;
using PocketWilds.Simulation;
using PocketWilds.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using UnityEngine.UI;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T39 parts of PF_UI_ActivityHud (doc 01 S-ACT-002 « Objets » and challenge 7 « Dans le noir »), PlayMode with fakes:
    /// the item buttons bounce in one after the other in the bottom right corner (inside the safe area, under the picker's
    /// choices, never over the middle of the play area); a tap presses one, calls the stage once and greys it softly with a
    /// small round check, no more taps until the stage gives it back; hiding fades them; Reduce Motion keeps fades only. The
    /// night darkens the play area (blue-violet, never black) except a soft circle of the given share of the screen's width
    /// around a screen point, glides after the point, turns off at 0, catches no touch and sits under the score, the tips and
    /// the items. Ticks allocate nothing with items and the night on, nothing is instantiated after the prewarm, and the hub's
    /// « Défi n : goal » line fits for the 10 Hide and Seek challenges.
    /// </summary>
    public class ActivityHudT39PlayTests
    {
        private const string HudPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_ActivityHud.prefab";
        private const string HubPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_ActivityHub.prefab";
        private const string DatabasePath = "Assets/_Game/Content/DEF_ContentDatabase.asset";
        private const float Frame = 1f / 60f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private FakeText _text;

        [SetUp]
        public void SetUp() => _text = new FakeText();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();
        }

        private static ActivityHudModel Model() => new ActivityHudModel
        {
            Activity = ActivityId.HideAndSeek, Mode = ActivityMode.Free, Duration = 60f, Star1 = 300, Star2 = 600, Star3 = 900, Best = 420,
        };

        // ------------------------------------------------------------------------------------------- items

        [UnityTest]
        public IEnumerator Items_BounceIn_InTheCorner_ClearOfThePickerAndTheMiddle()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var uses = new FakeItems(hud);
            hud.ShowItems(Items(hud), 2, uses);
            Assert.IsTrue(hud.ItemsShown);
            Assert.AreEqual(2, hud.ItemCount);
            Assert.IsFalse(hud.ItemRect(2).gameObject.activeSelf, "Two items, two buttons.");
            for (int i = 0; i < 2; i++)
            {
                Assert.IsNotNull(hud.ItemIconShown(i), "The item's icon (or the paw while it is not drawn).");
                var size = ((RectTransform)hud.ItemButton(i).transform).rect.size;
                Assert.GreaterOrEqual(Mathf.Min(size.x, size.y), 150f - 0.5f, "Big round buttons (48 dp is 124).");
            }
            Ticks(hud, 3);
            Assert.Greater(hud.ItemScale(0), 0f);
            Assert.AreEqual(0f, hud.ItemScale(1), 1e-4f, "One after the other.");
            float peak = 0f;
            for (int i = 0; i < 36; i++)
            {
                hud.Tick(Frame);
                peak = Mathf.Max(peak, hud.ItemScale(0));
            }
            Assert.Greater(peak, 1.05f, "A small bounce.");
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(hud.ItemVisible(i));
                Assert.AreEqual(1f, hud.ItemScale(i), 1e-3f, "Each item at rest.");
            }

            // In the bottom right corner, inside the safe area, away from the middle where the game happens.
            Canvas.ForceUpdateCanvases();
            var safe = ScreenRect(hud.SafeAreaRect);
            foreach (int count in new[] { 2, 3 })
            {
                hud.ShowItems(Items(hud), count, uses);
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < count; i++)
                {
                    var rect = ScreenRect(hud.ItemRect(i));
                    Assert.Greater(rect.center.x, Screen.width * 0.5f, $"{count} items: item {i} on the right half.");
                    Assert.Less(rect.yMax, safe.yMin + safe.height * 0.2f, $"{count} items: item {i} low in the corner.");
                    Assert.GreaterOrEqual(rect.xMin, safe.xMin - 0.5f, "Inside the safe area.");
                    Assert.LessOrEqual(rect.xMax, safe.xMax + 0.5f);
                    Assert.GreaterOrEqual(rect.yMin, safe.yMin - 0.5f);
                    foreach (var other in new[] { hud.ScoreRect, hud.StreakRect, hud.PracticeRect, hud.BoostRect, hud.TipBubble })
                        Assert.IsFalse(ScreenRect(other).Overlaps(rect), $"Item {i} touches nothing at the top: {other.name}.");
                }
            }

            // Clear of the picker's place: its 4 choices, even at the picked one's bounce (1.16).
            hud.ShowPicker(Choices(), 4, new FakePicks());
            Tick(hud, 0.6f);
            Canvas.ForceUpdateCanvases();
            for (int c = 0; c < 4; c++)
            {
                var choice = Grow(ScreenRect(hud.PickerChoiceRect(c)), 1.16f);
                for (int i = 0; i < 3; i++)
                    Assert.IsFalse(choice.Overlaps(ScreenRect(hud.ItemRect(i))), $"Choice {c} and item {i} never touch.");
            }
        }

        [UnityTest]
        public IEnumerator Items_TapCallsOnce_Presses_ThenUsedGreyNotTappable_AndHide()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            var uses = new FakeItems(hud);
            hud.ShowItems(Items(hud), 2, uses);
            Tick(hud, 0.6f);
            var disc = hud.ItemDiscColor(0);
            var icon = hud.ItemIconColor(0);
            Assert.IsTrue(hud.ItemTappable(0));
            Assert.IsFalse(hud.ItemCheckShown(0));

            hud.ItemButton(0).onClick.Invoke();
            CollectionAssert.AreEqual(new[] { 0 }, uses.Used, "The tap goes to the stage.");
            Assert.IsTrue(hud.ItemUsed(0));
            Assert.IsFalse(hud.ItemTappable(0), "Used: no more taps.");
            hud.ItemButton(0).onClick.Invoke();
            Assert.AreEqual(1, uses.Used.Count, "Once.");
            float low = 2f;
            float high = 0f;
            for (int i = 0; i < 18; i++)
            {
                hud.Tick(Frame);
                low = Mathf.Min(low, hud.ItemScale(0));
                high = Mathf.Max(high, hud.ItemScale(0));
            }
            Assert.Less(low, 0.97f, "A press: it gives under the finger…");
            Assert.Greater(high, 1.02f, "…and bounces back.");
            Tick(hud, 0.3f);
            Assert.AreEqual(1f, hud.ItemScale(0), 1e-3f);
            Assert.IsTrue(hud.ItemCheckShown(0), "A small round check.");
            Assert.Less(hud.ItemIconColor(0).a, icon.a - 0.2f, "The icon greys softly.");
            Assert.AreNotEqual(disc, hud.ItemDiscColor(0), "The disc goes a soft grey.");
            Assert.AreEqual(1f, hud.ItemDiscColor(0).a, 1e-3f, "Softly: never see-through.");
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual(1f, hud.ItemButton(0).targetGraphic.canvasRenderer.GetColor().a, 1e-3f, "The disabled button adds no fade of its own.");
            Assert.IsTrue(hud.ItemTappable(1), "The other item is still there.");

            // The stage's word: used, given back.
            hud.SetItemUsed(1, true);
            Assert.IsFalse(hud.ItemTappable(1));
            hud.ItemButton(1).onClick.Invoke();
            Assert.AreEqual(1, uses.Used.Count, "A used item takes no tap.");
            Tick(hud, 0.3f);
            Assert.IsTrue(hud.ItemCheckShown(1));
            hud.SetItemUsed(0, false);
            Assert.IsTrue(hud.ItemTappable(0), "Given back.");
            Tick(hud, 0.3f);
            Assert.IsFalse(hud.ItemCheckShown(0));
            Assert.AreEqual(icon.a, hud.ItemIconColor(0).a, 1e-3f, "Its colors back.");
            hud.ItemButton(0).onClick.Invoke();
            CollectionAssert.AreEqual(new[] { 0, 0 }, uses.Used);
            hud.SetItemUsed(7, true);
            hud.SetItemUsed(-1, false);

            // A stage that refuses (the game said no) gives it back at once.
            hud.ShowItems(Items(hud), 2, uses);
            Assert.IsTrue(hud.ItemTappable(0), "A new ShowItems: every item available.");
            uses.Refuse = true;
            hud.ItemButton(1).onClick.Invoke();
            Assert.AreEqual(3, uses.Used.Count);
            Assert.IsTrue(hud.ItemTappable(1), "Refused: still available.");
            uses.Refuse = false;

            // Hidden: no taps, faded, put away; nothing without items.
            hud.HideItems();
            Assert.IsFalse(hud.ItemsShown);
            hud.ItemButton(1).onClick.Invoke();
            Assert.AreEqual(3, uses.Used.Count, "A hidden row takes no tap.");
            Tick(hud, 0.3f);
            Assert.IsFalse(hud.ItemsVisible);
            hud.ShowItems(Items(hud), 0, uses);
            Assert.IsFalse(hud.ItemsShown);
            hud.ShowItems(null, 2, uses);
            Assert.IsFalse(hud.ItemsShown);
        }

        [UnityTest]
        public IEnumerator Items_ReduceMotion_FadesOnly_StillGreysWithItsCheck()
        {
            var hud = SpawnHud(out var actions);
            hud.ReduceMotion = true;
            hud.Show(Model(), _text, actions);
            yield return null;
            var uses = new FakeItems(hud);
            hud.ShowItems(Items(hud), 2, uses);
            for (int i = 0; i < 30; i++)
            {
                hud.Tick(Frame);
                Assert.AreEqual(1f, hud.ItemScale(1), 1e-4f, "Reduce Motion: no bounce in.");
            }
            Assert.IsTrue(hud.ItemVisible(1), "A fade.");
            hud.ItemButton(1).onClick.Invoke();
            for (int i = 0; i < 24; i++)
            {
                hud.Tick(Frame);
                Assert.AreEqual(1f, hud.ItemScale(1), 1e-4f, "Reduce Motion: no press bounce.");
            }
            Assert.IsTrue(hud.ItemCheckShown(1), "Used reads the same: grey and its check.");
            Assert.IsFalse(hud.ItemTappable(1));
            CollectionAssert.AreEqual(new[] { 1 }, uses.Used);
        }

        // ------------------------------------------------------------------------------------------- night

        [UnityTest]
        public IEnumerator Night_LitCircleOfTheWidth_GlidesAfterThePoint_OffAtZero()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            float w = Screen.width;
            float h = Screen.height;
            var a = new Vector2(w * 0.5f, h * 0.5f);
            hud.SetSpotlight(0.25f, a);
            Assert.IsTrue(hud.SpotlightShown);
            Near(a, hud.SpotlightCenter, "A new night starts on its point.");
            Tick(hud, 0.3f);
            Assert.AreEqual(1f, hud.SpotlightFade, 1e-4f, "Faded in.");

            Assert.Less(hud.SpotlightOpacity(a), 0.02f, "Lit around the point.");
            Assert.Less(hud.SpotlightOpacity(a + new Vector2(w * 0.18f, 0f)), 0.05f, "Inside the circle (a quarter of the width).");
            Assert.Less(hud.SpotlightOpacity(a + new Vector2(0f, w * 0.18f)), 0.05f, "Round on any screen: the same up and down.");
            float edge = hud.SpotlightOpacity(a + new Vector2(w * 0.25f, 0f));
            Assert.Greater(edge, 0.2f, "A soft edge around the radius…");
            Assert.Less(edge, 0.6f, "…never a hard rim.");
            float night = hud.SpotlightOpacity(a + new Vector2(w * 0.32f, 0f));
            Assert.Greater(night, 0.7f, "Night outside.");
            Assert.Greater(hud.SpotlightOpacity(a + new Vector2(0f, w * 0.32f)), 0.7f);
            float far = hud.SpotlightOpacity(new Vector2(1f, 1f));
            Assert.GreaterOrEqual(far, night - 1e-3f, "A touch denser far from the circle.");
            Assert.Less(far, 0.92f, "About 80 %, the meadow still shows.");
            var color = hud.SpotlightNight;
            Assert.Greater(color.b, color.r, "Deep blue-violet.");
            Assert.Greater(color.b, color.g);
            Assert.Greater(color.b, 0.15f, "Never pure black.");

            // The shader's values: the center in UVs, the radius in widths, the screen's height over its width.
            var spot = hud.SpotlightShaderSpot;
            Assert.AreEqual(0.5f, spot.x, 0.01f);
            Assert.AreEqual(0.5f, spot.y, 0.01f);
            Assert.AreEqual(0.25f, spot.z, 1e-4f);
            Assert.AreEqual(h / w, spot.w, 0.02f);

            // It glides after the finger (called every frame, as the stage does) and settles on it.
            var b = new Vector2(w * 0.7f, h * 0.62f);
            hud.SetSpotlight(0.25f, b);
            hud.Tick(Frame);
            float travelled = Vector2.Distance(a, hud.SpotlightCenter);
            Assert.Greater(travelled, 1f, "It follows at once…");
            Assert.Less(travelled, Vector2.Distance(a, b) - 1f, "…smoothly, never a jump.");
            for (float t = 0f; t < 0.5f; t += Frame)
            {
                hud.SetSpotlight(0.25f, b);
                hud.Tick(Frame);
            }
            Near(b, hud.SpotlightCenter, "Settled on the finger.");
            Assert.Less(hud.SpotlightOpacity(b), 0.02f);
            Assert.Greater(hud.SpotlightOpacity(a), 0.7f, "The old place is dark again.");
            hud.SetSpotlight(0.3f, b);
            Tick(hud, 0.5f);
            Assert.AreEqual(0.3f, hud.SpotlightRadius, 1e-3f, "A new radius, eased.");
            Assert.AreEqual(0.3f, hud.SpotlightShaderSpot.z, 1e-3f);

            // 0 turns it off: it fades and is put away, the Image back on the shared material.
            hud.SetSpotlight(0f, b);
            Assert.IsFalse(hud.SpotlightShown);
            Assert.IsTrue(hud.SpotlightVisible, "A soft fade out.");
            Tick(hud, 0.4f);
            Assert.IsFalse(hud.SpotlightVisible);
            Assert.IsFalse(hud.SpotlightRect.gameObject.activeSelf);
            Assert.AreEqual(0f, hud.SpotlightOpacity(b));
            Assert.AreEqual(HideFlags.None, hud.SpotlightImage.material.hideFlags & HideFlags.DontSave, "The runtime copy only while it shows.");
            hud.SetSpotlight(0.25f, a);
            Near(a, hud.SpotlightCenter, "Back on: it starts on its new point.");
        }

        [UnityTest]
        public IEnumerator Night_CatchesNoTouch_AndSitsUnderTheScoreTipsAndItems()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            hud.SetSpotlight(0.25f, center + new Vector2(Screen.width * 0.3f, 0f));
            hud.ShowItems(Items(hud), 2, new FakeItems(hud));
            hud.ShowTip("Tap the hidden animals", 30f);
            Tick(hud, 0.6f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            // Drawn first: the score, the time, the tips and the items (all in the safe area) are drawn over it.
            var night = hud.SpotlightRect;
            Assert.AreEqual(hud.SafeAreaRect.parent, night.parent, "Beside the safe area, full screen.");
            Assert.Less(night.GetSiblingIndex(), hud.SafeAreaRect.GetSiblingIndex(), "Under the score, the tips and the items.");
            foreach (var part in new[] { hud.ScoreRect, hud.TipBubble, hud.ItemRect(0), hud.ItemRect(1) })
                Assert.IsTrue(part.IsChildOf(hud.SafeAreaRect), part.name + " is drawn over the night.");
            var canvas = night.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "Its own canvas: following the finger rebuilds nothing else.");
            Assert.IsFalse(canvas.overrideSorting, "Drawn in its place in the HUD.");
            Assert.AreEqual(1, night.GetComponentsInChildren<Graphic>(true).Length, "One Image, one draw call.");
            Assert.IsNull(night.GetComponent<GraphicRaycaster>());
            Assert.IsFalse(night.GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.IsFalse(hud.SpotlightImage.raycastTarget, "It catches no touch.");

            // A touch in the night goes to the game; a touch on an item goes to the item.
            Assert.IsEmpty(TouchTargets(hud, center), "The middle of the play area is the game's, night or day.");
            var onItem = TouchTargets(hud, hud.ItemScreenPosition(0));
            Assert.AreEqual(1, onItem.Count);
            Assert.IsTrue(onItem[0].transform.IsChildOf(hud.ItemRect(0)), "The item takes its tap through the night.");
            var events = EventSystem.current;
            if (events == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                _spawned.Add(go);
                events = go.GetComponent<EventSystem>();
            }
            var results = new List<RaycastResult>();
            foreach (var point in new[] { center, hud.ItemScreenPosition(0), new Vector2(4f, 4f) })
            {
                results.Clear();
                events.RaycastAll(new PointerEventData(events) { position = point }, results);
                foreach (var result in results)
                    Assert.IsFalse(result.gameObject.transform.IsChildOf(night), "The night is never hit: " + result.gameObject.name);
            }
        }

        // ------------------------------------------------------------------------------------------- lifecycle and performance

        [UnityTest]
        public IEnumerator Parts_AreClosedByANewGame_AndWithTheHud()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            hud.ShowItems(Items(hud), 2, new FakeItems(hud));
            hud.SetSpotlight(0.25f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Tick(hud, 0.5f);
            hud.Hide();
            Assert.IsFalse(hud.ItemsShown);
            Assert.IsFalse(hud.SpotlightShown);
            Tick(hud, 0.4f);
            Assert.IsFalse(hud.GetComponent<Canvas>().enabled);
            Assert.IsFalse(hud.ItemsVisible, "Put away with the HUD.");
            Assert.IsFalse(hud.SpotlightVisible);
            hud.Show(Model(), _text, actions);
            Assert.IsFalse(hud.ItemsVisible, "A new game starts clean.");
            Assert.IsFalse(hud.SpotlightVisible);
        }

        [UnityTest]
        public IEnumerator Prewarm_ThenItemsAndNight_CreateNoObject()
        {
            var hud = SpawnHud(out var actions);
            yield return null;
            int objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            hud.Show(Model(), _text, actions);
            hud.ShowItems(Items(hud), 3, new FakeItems(hud));
            hud.SetSpotlight(0.25f, new Vector2(Screen.width * 0.4f, Screen.height * 0.5f));
            Tick(hud, 1f);
            hud.ItemButton(0).onClick.Invoke();
            hud.SetItemUsed(2, true);
            Tick(hud, 1f);
            yield return null;
            Assert.AreEqual(objects, Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                "Item buttons, checks and the night are pooled in the prefab.");
        }

        [UnityTest]
        public IEnumerator Tick_AllocatesNothing_WithItemsAndTheNight()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            hud.SetScore(300);
            hud.ShowItems(Items(hud), 2, new FakeItems(hud));
            hud.ItemButton(0).onClick.Invoke();
            float w = Screen.width;
            float h = Screen.height;
            for (int i = 0; i < 240; i++)
                Step(hud, i, w, h);
            Assert.That(() =>
            {
                for (int i = 240; i < 480; i++)
                    Step(hud, i, w, h);
            }, Is.Not.AllocatingGCMemory(), "The night following the finger, an item greying and given back: ticking allocates nothing.");

            hud.SetSpotlight(0f, Vector2.zero);
            hud.HideItems();
            Tick(hud, 0.5f);
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                    hud.Tick(Frame);
            }, Is.Not.AllocatingGCMemory(), "Closed parts cost nothing.");
        }

        /// <summary>One frame as the stage drives it: the finger moves in a circle, item 1 is used and given back now and then.</summary>
        private static void Step(ActivityHudScreen hud, int frame, float w, float h)
        {
            float angle = frame * 0.05f;
            hud.SetSpotlight(0.25f, new Vector2(w * (0.5f + 0.2f * Mathf.Cos(angle)), h * (0.5f + 0.15f * Mathf.Sin(angle))));
            if (frame % 40 == 0)
                hud.SetItemUsed(1, frame % 80 == 0);
            hud.Tick(Frame);
        }

        // ------------------------------------------------------------------------------------------- texts

        [UnityTest]
        public IEnumerator HubGoalLines_OfTheTenChallenges_FitOneLine_AndTwoWithThirtyPercent()
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(DatabasePath);
            Assume.That(database != null, DatabasePath);
            var hub = Spawn<ActivityHubScreen>(HubPath);
            yield return null;
            var goal = FindByPathEnd(hub.transform, "Challenges/Goal");
            Assert.IsNotNull(goal, "The hub's goal line.");
            goal.gameObject.SetActive(true);
            float width = goal.rectTransform.rect.width;
            float height = goal.rectTransform.rect.height;
            float largest = goal.enableAutoSizing ? goal.fontSizeMax : goal.fontSize;
            float smallest = goal.enableAutoSizing ? goal.fontSizeMin : goal.fontSize;
            goal.enableAutoSizing = false;
            var problems = new List<string>();
            foreach (string language in new[] { "en", "fr" })
            {
                ILocalization text = new LocalizationService(database.localization, language, SystemLanguage.English);
                string template = text.Get(ActivityText.HubChallengeGoal);
                for (int i = 0; i < 10; i++)
                {
                    string key = ActivityText.ChallengeGoalKey(ActivityId.HideAndSeek, i);
                    Assume.That(text.Has(key), "Run Pocket Wilds/UI/Build Activities (T37) for the T39 texts: " + key);
                    string line = template.Replace("{n}", (i + 1).ToString()).Replace("{goal}", text.Get(key));
                    goal.fontSize = largest;
                    float one = goal.GetPreferredValues(line, float.PositiveInfinity, float.PositiveInfinity).x;
                    if (one > width + 1f)
                        problems.Add($"{language} \"{line}\": {one:F0} at {largest} on one line, box {width:F0}");
                    string longer = line + new string('e', Mathf.CeilToInt(line.Length * 0.3f));
                    goal.fontSize = smallest;
                    float two = goal.GetPreferredValues(longer, width, float.PositiveInfinity).y;
                    if (two > height + 1f)
                        problems.Add($"{language} \"{longer}\": {two:F0} high at {smallest}, box {height:F0}");
                    string name = text.Get(ActivityText.ChallengeNameKey(ActivityId.HideAndSeek, i));
                    Assert.IsFalse(name.IndexOf('—') >= 0 || line.IndexOf('—') >= 0, "No em dash.");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        // ------------------------------------------------------------------------------------------- helpers

        private ActivityHudScreen SpawnHud(out FakeHudActions actions)
        {
            var hud = Spawn<ActivityHudScreen>(HudPath);
            Assume.That(hud.HasT39Parts, "Run Pocket Wilds/UI/Build Activities (T37) for the T39 HUD parts (items and night).");
            actions = new FakeHudActions(hud);
            hud.Prewarm(_text);
            return hud;
        }

        /// <summary>The magnifier and the whistle the HUD lends (null while agent HI's icons are not drawn: the paw shows).</summary>
        private static ActivityPickerItem[] Items(ActivityHudScreen hud) => new[]
        {
            new ActivityPickerItem { Icon = hud.Icon(ActivityText.IconMagnifier) },
            new ActivityPickerItem { Icon = hud.Icon(ActivityText.IconWhistle) },
            new ActivityPickerItem { Icon = null, Tint = new Color(1f, 0.9f, 0.8f, 1f) },
        };

        private static ActivityPickerItem[] Choices() => new[]
        {
            new ActivityPickerItem { Tint = Color.white }, new ActivityPickerItem { Tint = Color.white },
            new ActivityPickerItem { Tint = Color.white }, new ActivityPickerItem { Tint = Color.white },
        };

        /// <summary>The HUD's touch targets under a screen point (active, raycast targets), whatever the canvas has drawn yet.</summary>
        private static List<Graphic> TouchTargets(ActivityHudScreen hud, Vector2 point)
        {
            var hits = new List<Graphic>();
            foreach (var graphic in hud.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.raycastTarget || !graphic.isActiveAndEnabled)
                    continue;
                var group = graphic.GetComponentInParent<CanvasGroup>();
                if (group != null && !group.blocksRaycasts)
                    continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, point, null))
                    hits.Add(graphic);
            }
            return hits;
        }

        private T Spawn<T>(string path) where T : Component
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assume.That(prefab != null, "Build the UI prefabs first: " + path);
            var go = Object.Instantiate(prefab);
            _spawned.Add(go);
            return go.GetComponent<T>();
#else
            Assert.Ignore("Editor only.");
            return null;
#endif
        }

        /// <summary>The first label whose path ends with <paramref name="end"/>, inactive ones included.</summary>
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

        private static void Near(Vector2 expected, Vector2 actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, 1f, message);
            Assert.AreEqual(expected.y, actual.y, 1f, message);
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static Rect Grow(Rect rect, float scale)
        {
            var size = rect.size * scale;
            return new Rect(rect.center - size * 0.5f, size);
        }

        private static void Ticks(ActivityHudScreen hud, int frames)
        {
            for (int i = 0; i < frames; i++)
                hud.Tick(Frame);
        }

        private static void Tick(ActivityHudScreen hud, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                hud.Tick(Frame);
        }

        private sealed class FakeText : ILocalization
        {
            private readonly Dictionary<string, string> _texts = new Dictionary<string, string>
            {
                { ActivityText.HudStreak, "Streak ×{n}" },
                { ActivityText.Practice, "Practice" },
                { ActivityText.PauseTitle, "Paused" },
                { ActivityText.PauseResume, "Resume" },
                { ActivityText.PauseQuit, "Quit" },
                { ActivityText.GroomingTipBurr, "Two little taps for burrs" },
                { ActivityText.HideSeekTipFind, "Tap the hidden animals" },
            };

            public string Language => "en";
            public string Get(string key) => _texts.TryGetValue(key, out var text) ? text : key;
        }

        private sealed class FakeHudActions : IActivityHudActions
        {
            private readonly ActivityHudScreen _view;

            public FakeHudActions(ActivityHudScreen view) => _view = view;

            public void PauseActivity() => _view.SetPaused(true);
            public void ResumeActivity() => _view.SetPaused(false);

            public void QuitActivity()
            {
            }
        }

        /// <summary>The stage's side: records each use; when it refuses (the game said no), it gives the item back at once.</summary>
        private sealed class FakeItems : IActivityItemActions
        {
            private readonly ActivityHudScreen _view;
            public readonly List<int> Used = new List<int>(8);
            public bool Refuse;

            public FakeItems(ActivityHudScreen view) => _view = view;

            public void UseItem(int index)
            {
                Used.Add(index);
                if (Refuse)
                    _view.SetItemUsed(index, false);
            }
        }

        private sealed class FakePicks : IActivityPickerActions
        {
            public void Pick(int index)
            {
            }
        }
    }
}
