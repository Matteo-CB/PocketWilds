using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T38 parts of PF_UI_ActivityHud (doc « Mini-jeux & activités » section 1: understood without text in 3 s; doc 01
    /// A15 and S-ACT-001) and the animal sheet's « Brosser » button, PlayMode with fakes: the tip bubble shows under the
    /// score (never over it), bounces in softly and hides after its seconds (paused time does not count); the tutorial paw
    /// slides between its two screen points and loops, taps once or twice per 1.1 s, and stays still and breathing with
    /// Reduce Motion; the round choices bounce in one after the other, a pick calls the stage once and lets the others go;
    /// the boost ring empties over its seconds in gold, never red; the sheet shows « Brosser » beside the treat with
    /// « Gratuit » or a ticket, hidden when Grooming cannot start, and its tap asks to groom this animal. Ticks allocate
    /// nothing after warm-up and nothing is instantiated after the prewarm.
    /// </summary>
    public class ActivityHudT38PlayTests
    {
        private const string HudPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_ActivityHud.prefab";
        private const string SheetPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_AnimalQuickSheet.prefab";
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

        private static ActivityHudModel Model(ActivityMode mode = ActivityMode.Free) => new ActivityHudModel
        {
            Activity = ActivityId.Grooming, Mode = mode, Duration = 45f, Star1 = 200, Star2 = 380, Star3 = 520, Best = 300,
        };

        // ------------------------------------------------------------------------------------------- tip

        [UnityTest]
        public IEnumerator Tip_ShowsUnderTheScore_BouncesSoftly_AndHidesAfterItsSeconds()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            hud.SetStreak(3);

            hud.ShowTip("Brush the fur", 2f);
            Assert.IsTrue(hud.TipShown);
            Assert.AreEqual("Brush the fur", hud.TipText);
            Assert.Greater(hud.TipBubbleWidth, ActivityHudScreen.TipMinWidth - 0.5f);
            Assert.Less(hud.TipBubbleWidth, ActivityHudScreen.TipMaxWidth, "The bubble hugs a short tip.");
            float smallest = 2f;
            float largest = 0f;
            for (int i = 0; i < 24; i++)
            {
                hud.Tick(Frame);
                smallest = Mathf.Min(smallest, hud.TipScale);
                largest = Mathf.Max(largest, hud.TipScale);
                if (i == 5)
                {
                    Assert.Greater(hud.TipAlpha, 0f, "A gentle fade in.");
                    Assert.Less(hud.TipAlpha, 1f);
                }
            }
            Assert.GreaterOrEqual(smallest, 0.9f - 1e-3f);
            Assert.Greater(largest, 1.005f, "A tiny bounce.");
            Assert.Less(largest, 1.05f, "Tiny.");
            Assert.AreEqual(1f, hud.TipAlpha, 1e-3f);
            Assert.AreEqual(1f, hud.TipScale, 1e-3f, "It settles.");
            Canvas.ForceUpdateCanvases();
            Assert.IsFalse(Overlap(hud.TipBubble, hud.ScoreRect), "The tip never covers the score.");

            Tick(hud, 1.3f);
            Assert.IsTrue(hud.TipShown, "Still there before its 2 s.");
            Tick(hud, 0.4f);
            Assert.IsFalse(hud.TipShown, "Gone after its seconds.");
            Tick(hud, 0.35f);
            Assert.IsFalse(hud.TipVisible, "Faded and put away.");

            // Paused time does not count.
            hud.ShowTip("Pick off the leaves", 1f);
            hud.SetPaused(true);
            Tick(hud, 2f);
            Assert.IsTrue(hud.TipShown, "The pause keeps the tip's time.");
            hud.SetPaused(false);
            Tick(hud, 1.1f);
            Assert.IsFalse(hud.TipShown);

            // A very long tip: the bubble takes its widest size (the label shrinks inside).
            hud.ShowTip(new string('m', 90), 2f);
            Assert.AreEqual(ActivityHudScreen.TipMaxWidth, hud.TipBubbleWidth, 0.5f);
            hud.HideTip();
            Assert.IsFalse(hud.TipShown);
            Tick(hud, 0.35f);
            Assert.IsFalse(hud.TipVisible);
            hud.ShowTip(null, 2f);
            Assert.IsFalse(hud.TipShown, "No text, no bubble.");

            // Reduce Motion: a fade only.
            hud.ReduceMotion = true;
            hud.ShowTip("Tap the bubble", 2f);
            for (int i = 0; i < 20; i++)
            {
                hud.Tick(Frame);
                Assert.AreEqual(1f, hud.TipScale, 1e-4f, "Reduce Motion: no bounce.");
            }
        }

        [UnityTest]
        public IEnumerator Tip_AndBoost_KeepTheirPlaces_WithThePracticeTag()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            hud.SetStreak(4);
            hud.ShowBoost(null, 5f);
            hud.ShowTip("Two little taps for burrs", 3f);
            Tick(hud, 0.5f);
            Canvas.ForceUpdateCanvases();
            Assert.IsFalse(Overlap(hud.BoostRect, hud.ScoreRect), "The chip sits under the score card.");
            Assert.Less(Top(hud.BoostRect), Bottom(hud.ScoreRect) + 1f);
            Assert.IsFalse(Overlap(hud.BoostRect, hud.StreakRect), "Beside the streak pill.");
            Assert.IsFalse(Overlap(hud.TipBubble, hud.BoostRect));
            Assert.IsFalse(Overlap(hud.TipBubble, hud.StreakRect));

            // Practice: the tag takes the right of the row, the chip goes under it and the tip under the chip.
            hud.Hide();
            Tick(hud, 0.4f);
            hud.Show(Model(ActivityMode.Practice), _text, actions);
            hud.SetStreak(4);
            hud.ShowBoost(null, 5f);
            hud.ShowTip("Two little taps for burrs", 3f);
            Tick(hud, 0.6f);
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(hud.PracticeShown);
            foreach (var other in new[] { hud.ScoreRect, hud.StreakRect, hud.PracticeRect })
            {
                Assert.IsFalse(Overlap(hud.BoostRect, other), "The chip touches nothing: " + other.name);
                Assert.IsFalse(Overlap(hud.TipBubble, other), "The tip touches nothing: " + other.name);
            }
            Assert.IsFalse(Overlap(hud.TipBubble, hud.BoostRect));
        }

        // ------------------------------------------------------------------------------------------- gesture

        [UnityTest]
        public IEnumerator Gesture_SwipeSlidesBetweenThePoints_AndLoops()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var from = new Vector2(Screen.width * 0.3f, Screen.height * 0.45f);
            var to = new Vector2(Screen.width * 0.7f, Screen.height * 0.5f);
            hud.ShowGesture(ActivityGesture.Swipe, from, to);
            Assert.IsTrue(hud.GestureShown);
            Assert.AreEqual(ActivityGesture.Swipe, hud.GestureKind);
            Assert.IsNotNull(hud.PawSprite, "UI_Gesture_Swipe, or the tap paw while it is not drawn.");
            Near(from, hud.GesturePoint, "It starts on the first point.");

            Ticks(hud, 6);
            Near(from, hud.GesturePoint, "It comes down onto the fur first.");
            Assert.Greater(hud.PawAlpha, 0f);
            float lastX = hud.GesturePoint.x;
            float pressed = 2f;
            int trail = 0;
            for (int i = 6; i < 60; i++)
            {
                hud.Tick(Frame);
                Assert.GreaterOrEqual(hud.GesturePoint.x, lastX - 0.01f, "It slides one way.");
                lastX = hud.GesturePoint.x;
                if (i > 14)
                    pressed = Mathf.Min(pressed, hud.PawScale);
                trail = Mathf.Max(trail, hud.TrailShown);
            }
            Assert.Less(pressed, 0.97f, "A soft press while it slides.");
            Assert.Greater(trail, 0, "Soft round dots where it brushed.");
            Ticks(hud, 3);
            Near(to, hud.GesturePoint, "0.8 s later it is on the second point.");
            Ticks(hud, 27);
            Assert.AreEqual(0f, hud.PawAlpha, 1e-3f, "Lifted and faded before it starts again.");
            Assert.AreEqual(0, hud.GestureCycles);
            Ticks(hud, 15);
            Assert.AreEqual(1, hud.GestureCycles, "It loops.");
            Near(from, hud.GesturePoint, "Back on the first point.");

            // UI_Gesture_Swipe is drawn sliding right: it tilts with the path (30° at most, a soft lean on top) and a
            // leftward slide flips it, never a half turn. The same call again only moves it; hiding fades it out.
            bool drawn = hud.PawSprite.name == "UI_Gesture_Swipe";
            Assert.AreEqual(1f, hud.PawFlip);
            hud.ShowGesture(ActivityGesture.Swipe, to, from);
            Assert.AreEqual(1, hud.GestureCycles, "No restart for a move.");
            Ticks(hud, 30);
            if (drawn)
                Assert.AreEqual(-1f, hud.PawFlip, "A leftward slide flips the paw.");
            Assert.LessOrEqual(Mathf.Abs(hud.PawAngle), 36.5f, "Tilted with its path, never upside down.");
            hud.ShowGesture(ActivityGesture.Swipe, from, from + new Vector2(10f, Screen.height * 0.3f));
            for (int i = 0; i < 60; i++)
            {
                hud.Tick(Frame);
                Assert.LessOrEqual(Mathf.Abs(hud.PawAngle), 36.5f, "A steep path tilts it 30° at most.");
            }
            hud.HideGesture();
            Assert.IsFalse(hud.GestureShown);
            Tick(hud, 0.3f);
            Assert.IsFalse(hud.GestureVisible, "Put away.");
            Assert.AreEqual(0, hud.TrailShown);
        }

        [UnityTest]
        public IEnumerator Gesture_TapPressesOnce_DoubleTapTwice_PerLoop()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var point = new Vector2(Screen.width * 0.5f, Screen.height * 0.4f);
            foreach (var gesture in new[] { ActivityGesture.Tap, ActivityGesture.DoubleTap })
            {
                hud.ShowGesture(gesture, point, Vector2.zero);
                Assert.AreEqual(gesture, hud.GestureKind);
                int presses = 0;
                bool down = false;
                int rings = 0;
                for (int i = 0; i < 64; i++)
                {
                    hud.Tick(Frame);
                    Near(point, hud.GesturePoint, "The paw shows its point.");
                    Assert.Greater(hud.PawScreenPosition.y, point.y, "It floats above the point, never hiding it.");
                    bool now = hud.PawScale < 0.95f;
                    if (now && !down)
                        presses++;
                    down = now;
                    rings = Mathf.Max(rings, hud.RingsShown);
                }
                Assert.AreEqual(gesture == ActivityGesture.Tap ? 1 : 2, presses, gesture + ": presses per 1.1 s.");
                Assert.AreEqual(gesture == ActivityGesture.Tap ? 1 : 2, rings, "A soft ring from each press.");
                Ticks(hud, 4);
                Assert.AreEqual(1, hud.GestureCycles, "It loops every 1.1 s.");
                hud.HideGesture();
                Tick(hud, 0.3f);
                Assert.IsFalse(hud.GestureVisible);
            }
        }

        [UnityTest]
        public IEnumerator Gesture_ReduceMotion_AStillPawThatBreathes()
        {
            var hud = SpawnHud(out var actions);
            hud.ReduceMotion = true;
            hud.Show(Model(), _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var from = new Vector2(Screen.width * 0.3f, Screen.height * 0.45f);
            var to = new Vector2(Screen.width * 0.7f, Screen.height * 0.45f);
            hud.ShowGesture(ActivityGesture.Swipe, from, to);
            Tick(hud, 0.3f);
            var position = hud.PawPosition;
            float low = 2f;
            float high = 0f;
            for (int i = 0; i < 120; i++)
            {
                hud.Tick(Frame);
                Assert.AreEqual(position, hud.PawPosition, "Reduce Motion: the paw stays still.");
                Assert.AreEqual(1f, hud.PawScale, 1e-4f);
                low = Mathf.Min(low, hud.PawAlpha);
                high = Mathf.Max(high, hud.PawAlpha);
            }
            Assert.Greater(high - low, 0.1f, "A soft pulse.");
            Assert.Greater(low, 0.5f, "Always readable.");
            Assert.AreEqual(ActivityHudScreen.TrailDots, hud.TrailShown, "The way to go stays drawn in soft dots.");
            Near(from, hud.GesturePoint, "On the first point.");

            hud.ShowGesture(ActivityGesture.DoubleTap, from, from);
            Tick(hud, 0.3f);
            position = hud.PawPosition;
            Tick(hud, 1f);
            Assert.AreEqual(position, hud.PawPosition);
            Assert.AreEqual(1, hud.RingsShown, "A soft ring on the point.");
            Assert.AreEqual(0, hud.TrailShown);
        }

        // ------------------------------------------------------------------------------------------- picker

        [UnityTest]
        public IEnumerator Picker_BouncesIn_PickCallsOnce_OthersLetGo()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var picks = new FakePicks();
            var items = Bows();
            hud.ShowPicker(items, 3, picks);
            Assert.IsTrue(hud.PickerShown);
            Assert.IsFalse(hud.PickerItemVisible(3), "Three bows, three choices.");
            for (int i = 0; i < 3; i++)
            {
                Assert.IsNotNull(hud.PickerIconShown(i));
                Assert.AreEqual(items[i].Tint, hud.PickerTintShown(i), "Each icon in its tint.");
                var rect = (RectTransform)hud.PickerButton(i).transform;
                Assert.GreaterOrEqual(Mathf.Min(rect.rect.width, rect.rect.height), 150f, "Big round buttons.");
            }
            Ticks(hud, 3);
            Assert.Greater(hud.PickerItemScale(0), 0f);
            Assert.AreEqual(0f, hud.PickerItemScale(2), 1e-4f, "One after the other.");
            float peak = 0f;
            for (int i = 0; i < 36; i++)
            {
                hud.Tick(Frame);
                peak = Mathf.Max(peak, hud.PickerItemScale(0));
            }
            Assert.Greater(peak, 1.05f, "A small bounce.");
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(hud.PickerItemVisible(i));
                Assert.AreEqual(1f, hud.PickerItemScale(i), 1e-3f, "Each choice at rest.");
            }

            hud.PickerButton(1).onClick.Invoke();
            CollectionAssert.AreEqual(new[] { 1 }, picks.Picks, "The pick goes to the stage.");
            Assert.IsFalse(hud.PickerShown);
            hud.PickerButton(0).onClick.Invoke();
            hud.PickerButton(1).onClick.Invoke();
            Assert.AreEqual(1, picks.Picks.Count, "Once.");
            peak = 0f;
            for (int i = 0; i < 18; i++)
            {
                hud.Tick(Frame);
                peak = Mathf.Max(peak, hud.PickerItemScale(1));
            }
            Assert.Greater(peak, 1.1f, "The picked bow bounces.");
            Assert.IsFalse(hud.PickerItemVisible(0), "The others let go.");
            Assert.IsFalse(hud.PickerItemVisible(2));
            Assert.IsTrue(hud.PickerItemVisible(1));
            Tick(hud, 0.8f);
            Assert.IsFalse(hud.PickerVisible, "Then the picker is put away.");

            // Hidden without a pick: no call; nothing without items; Reduce Motion: fades only.
            hud.ShowPicker(items, 3, picks);
            Tick(hud, 0.2f);
            hud.HidePicker();
            Assert.IsFalse(hud.PickerShown);
            Tick(hud, 0.3f);
            Assert.IsFalse(hud.PickerVisible);
            Assert.AreEqual(1, picks.Picks.Count);
            hud.ShowPicker(items, 0, picks);
            Assert.IsFalse(hud.PickerShown);
            hud.ReduceMotion = true;
            hud.ShowPicker(items, 3, picks);
            for (int i = 0; i < 30; i++)
            {
                hud.Tick(Frame);
                Assert.AreEqual(1f, hud.PickerItemScale(2), 1e-4f, "Reduce Motion: no bounce.");
            }
            hud.PickerButton(2).onClick.Invoke();
            Assert.AreEqual(2, picks.Picks[1]);
        }

        // ------------------------------------------------------------------------------------------- boost

        [UnityTest]
        public IEnumerator Boost_RingEmptiesOverItsSeconds_NeverRed_PauseWaits()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            hud.ShowBoost(null, 5f);
            Assert.IsTrue(hud.BoostShown);
            Assert.IsNotNull(hud.BoostIconShown, "The golden brush (or the paw while it is not drawn).");
            Assert.AreEqual(1f, hud.BoostFill, 1e-4f);
            Ticks(hud, 150);
            Assert.AreEqual(0.5f, hud.BoostFill, 0.02f, "The ring empties with the seconds.");
            var color = hud.BoostRingColor;
            Assert.Greater(color.g, 0.6f, "Soft gold, never red.");
            Assert.Less(color.r - color.g, 0.3f);

            hud.SetPaused(true);
            float fill = hud.BoostFill;
            Ticks(hud, 60);
            Assert.AreEqual(fill, hud.BoostFill, 1e-4f, "The pause keeps the boost's time.");
            hud.SetPaused(false);
            Ticks(hud, 160);
            Assert.IsFalse(hud.BoostShown, "Done after its seconds.");
            Assert.AreEqual(color, hud.BoostRingColor, "The same calm color to the end.");
            Tick(hud, 0.35f);
            Assert.IsFalse(hud.BoostVisible, "Faded and put away.");

            hud.ShowBoost(null, 5f);
            hud.HideBoost();
            Assert.IsFalse(hud.BoostShown);
            Tick(hud, 0.35f);
            Assert.IsFalse(hud.BoostVisible);
        }

        // ------------------------------------------------------------------------------------------- lifecycle

        [UnityTest]
        public IEnumerator Parts_AreClosedByANewGame_AndWithTheHud()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            ShowEverything(hud, new FakePicks());
            Tick(hud, 0.5f);
            hud.Hide();
            Assert.IsFalse(hud.TipShown);
            Assert.IsFalse(hud.GestureShown);
            Assert.IsFalse(hud.PickerShown);
            Assert.IsFalse(hud.BoostShown);
            Tick(hud, 0.4f);
            Assert.IsFalse(hud.GetComponent<Canvas>().enabled);
            hud.Show(Model(), _text, actions);
            Assert.IsFalse(hud.TipVisible, "A new game starts clean.");
            Assert.IsFalse(hud.GestureVisible);
            Assert.IsFalse(hud.PickerVisible);
            Assert.IsFalse(hud.BoostVisible);
        }

        [UnityTest]
        public IEnumerator Prewarm_ThenEveryPart_CreatesNoObject()
        {
            var hud = SpawnHud(out var actions);
            yield return null;
            int objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            hud.Show(Model(), _text, actions);
            ShowEverything(hud, new FakePicks());
            Tick(hud, 2f);
            hud.ShowGesture(ActivityGesture.DoubleTap, new Vector2(300f, 300f), Vector2.zero);
            hud.PickerButton(0).onClick.Invoke();
            Tick(hud, 1f);
            yield return null;
            Assert.AreEqual(objects, Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                "Paw, rings, trail dots and choices are pooled in the prefab.");
        }

        [UnityTest]
        public IEnumerator Tick_AllocatesNothing_AfterWarmup_AndClosedPartsCostNothing()
        {
            var hud = SpawnHud(out var actions);
            hud.Show(Model(), _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            hud.SetScore(250);
            hud.SetStreak(3);
            ShowEverything(hud, new FakePicks());
            Ticks(hud, 240);
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                    hud.Tick(Frame);
            }, Is.Not.AllocatingGCMemory(), "Tip, swipe paw and its trail, choices and boost ring: ticking allocates nothing.");

            hud.ShowGesture(ActivityGesture.DoubleTap, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), Vector2.zero);
            Ticks(hud, 140);
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                    hud.Tick(Frame);
            }, Is.Not.AllocatingGCMemory(), "The tapping paw allocates nothing.");

            hud.HideTip();
            hud.HideGesture();
            hud.HidePicker();
            hud.HideBoost();
            Tick(hud, 0.5f);
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                    hud.Tick(Frame);
            }, Is.Not.AllocatingGCMemory(), "Closed parts cost nothing.");
        }

        // ------------------------------------------------------------------------------------------- animal sheet

        [UnityTest]
        public IEnumerator Sheet_GroomButton_BesideTheTreat_FreeOrTicket_TapGrooms()
        {
            var sheet = Spawn<AnimalSheetScreen>(SheetPath);
            Assume.That(sheet.GroomButton != null, "Run Pocket Wilds/UI/Build Feedback Screens (T34A) for the T38 « Brosser » button.");
            var actions = new FakeSheetActions();
            sheet.Prewarm(_text);
            var data = new AnimalSheetData(42u, "Pip", "Rabbit", null, default, default, RarityTier.Common, false, 0.3f, 1, 0u);
            sheet.Open(data, _text, actions);
            yield return null;
            Assert.IsFalse(sheet.GroomShown, "Hidden until the refuge says Grooming can start.");

            sheet.SetTreat(new SheetTreatData { Available = true, Owned = 3 });
            float solo = sheet.TreatPosition.x;
            sheet.SetGroom(true, true);
            Assert.IsTrue(sheet.GroomShown);
            Assert.AreEqual("Groom", sheet.GroomLabelText);
            Assert.IsTrue(sheet.GroomFreeShown, "« Gratuit » for the free first game (A15).");
            Assert.AreEqual("Free", sheet.GroomFreeText);
            Assert.IsFalse(sheet.GroomTicketShown);
            Canvas.ForceUpdateCanvases();
            Assert.Less(sheet.TreatPosition.x, solo, "The treat makes room on the left.");
            Assert.Greater(sheet.GroomPosition.x, solo, "« Brosser » beside it on the right.");
            Assert.AreEqual(sheet.TreatPosition.y, sheet.GroomPosition.y, 0.5f, "Same row.");
            Assert.IsFalse(Overlap(sheet.TreatRect, sheet.GroomRect), "Side by side, never one in the other.");
            var size = ((RectTransform)sheet.GroomButton.transform).rect.size;
            Assert.GreaterOrEqual(Mathf.Min(size.x, size.y), 124f, "48 dp touch target.");

            sheet.SetGroom(true, false);
            Assert.IsTrue(sheet.GroomTicketShown, "A small ticket and « 1 ».");
            Assert.IsFalse(sheet.GroomFreeShown);

            sheet.GroomButton.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { 42u }, actions.Groomed, "The tap asks to groom this animal.");

            sheet.SetGroom(false, false);
            Assert.IsFalse(sheet.GroomShown, "Hidden when Grooming cannot start.");
            Assert.AreEqual(solo, sheet.TreatPosition.x, 0.5f, "The treat goes back under the bond bar.");
            sheet.GroomButton.onClick.Invoke();
            Assert.AreEqual(1, actions.Groomed.Count, "A hidden button does nothing.");

            // Without a treat row, « Brosser » sits alone, centered.
            sheet.Open(data, _text, actions);
            Assert.IsFalse(sheet.GroomShown, "Each opening waits for the refuge again.");
            sheet.SetGroom(true, true);
            Assert.AreEqual(solo, sheet.GroomPosition.x, 0.5f);
            sheet.Close();
            for (int i = 0; i < 30; i++)
                sheet.Tick(Frame);
            Assert.IsFalse(sheet.IsOpen);
        }

        // ------------------------------------------------------------------------------------------- helpers

        private ActivityHudScreen SpawnHud(out FakeHudActions actions)
        {
            var hud = Spawn<ActivityHudScreen>(HudPath);
            Assume.That(hud.HasT38Parts, "Run Pocket Wilds/UI/Build Activities (T37) for the T38 HUD parts.");
            actions = new FakeHudActions(hud);
            hud.Prewarm(_text);
            return hud;
        }

        private static ActivityPickerItem[] Bows() => new[]
        {
            new ActivityPickerItem { Tint = new Color(0.98f, 0.72f, 0.80f, 1f) },
            new ActivityPickerItem { Tint = new Color(0.62f, 0.82f, 0.96f, 1f) },
            new ActivityPickerItem { Tint = new Color(1f, 0.95f, 0.84f, 1f) },
        };

        private static void ShowEverything(ActivityHudScreen hud, FakePicks picks)
        {
            hud.ShowTip("Brush the fur", 60f);
            hud.ShowGesture(ActivityGesture.Swipe, new Vector2(Screen.width * 0.3f, Screen.height * 0.45f),
                new Vector2(Screen.width * 0.7f, Screen.height * 0.5f));
            hud.ShowPicker(Bows(), 3, picks);
            hud.ShowBoost(null, 60f);
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

        private static void Near(Vector2 expected, Vector2 actual, string message)
        {
            Assert.AreEqual(expected.x, actual.x, 2f, message);
            Assert.AreEqual(expected.y, actual.y, 2f, message);
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static bool Overlap(RectTransform a, RectTransform b) => ScreenRect(a).Overlaps(ScreenRect(b));
        private static float Top(RectTransform rect) => ScreenRect(rect).yMax;
        private static float Bottom(RectTransform rect) => ScreenRect(rect).yMin;

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
                { ActivityText.GroomingTipBrush, "Brush the fur" },
                { ActivityText.GroomingTipBurr, "Two little taps for burrs" },
                { ActivityText.SheetGroom, "Groom" },
                { ActivityText.HubFree, "Free" },
                { DailyVisitText.SheetTreat, "Treat" },
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

        private sealed class FakePicks : IActivityPickerActions
        {
            public readonly List<int> Picks = new List<int>(4);
            public void Pick(int index) => Picks.Add(index);
        }

        private sealed class FakeSheetActions : IAnimalSheetActions, IAnimalSheetGroomActions, IAnimalSheetTreatActions
        {
            public readonly List<uint> Groomed = new List<uint>(4);

            public void ToggleFavorite(uint instanceId)
            {
            }

            public void OpenSpeciesDetails(ContentId species)
            {
            }

            public void AnimalSheetClosed()
            {
            }

            public void GroomAnimal(uint instanceId) => Groomed.Add(instanceId);

            public void GiveTreat(uint instanceId)
            {
            }
        }
    }
}
