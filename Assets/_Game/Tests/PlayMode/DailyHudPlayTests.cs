using System.Collections;
using NUnit.Framework;
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
    /// T36 HUD parts on PF_UI_HUD_Main (doc 06 screen 002, doc 01 sections 1.3, A08 and A12, doc 05 sheet 17): the right
    /// rail's Today button shows and hides with its dot, sits on the right edge under the top bar (never on the hatch
    /// area, the egg choice or the chips), pops in once and ends at rest, its dot breathes without ever flashing; the Gems
    /// chip shows without the shop and its counter changes as the Gems motes land; the wish card opens at a bubble's
    /// point, stays inside the safe area wherever the bubble goes, swaps, claims and closes on a touch beside it; steady
    /// ticks allocate nothing. The HUD prefab runs alone (no boot), driven like the presenter drives it.
    /// </summary>
    public class DailyHudPlayTests
    {
        private const string HudPrefabPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_HUD_Main.prefab";
        private const float Frame = 1f / 60f;

        private RefugeHud _hud;
        private Fake _fake;

        [TearDown]
        public void TearDown()
        {
            if (_hud != null)
                Object.Destroy(_hud.gameObject);
            _hud = null;
        }

        private IEnumerator Spawn(bool reduceMotion = false)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.IsNotNull(prefab, "Run Pocket Wilds/UI/Build HUD and Screens.");
            _fake = new Fake();
            _hud = Object.Instantiate(prefab).GetComponent<RefugeHud>();
            _hud.ReduceMotion = reduceMotion;
            _hud.Bind(_fake);
            _hud.BindDaily(_fake);
            Assert.IsNotNull(_hud.TodayButton, "The Today button is wired (T36 HUD build).");
            Assert.IsNotNull(_hud.WishCard, "The wish card travels with the HUD.");
            // The canvas scaler sizes the HUD on its first frame.
            yield return null;
            Canvas.ForceUpdateCanvases();
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        private void Tick(float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                _hud.Tick(Frame);
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            // Overlay canvases: world space is screen space.
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private Rect Part(string path)
        {
            var t = _hud.transform.Find(path);
            Assert.IsNotNull(t, path);
            return ScreenRect((RectTransform)t);
        }

        private static bool Inside(Rect inner, Rect outer, float tolerance = 1f) =>
            inner.xMin >= outer.xMin - tolerance && inner.xMax <= outer.xMax + tolerance
            && inner.yMin >= outer.yMin - tolerance && inner.yMax <= outer.yMax + tolerance;

        [UnityTest]
        public IEnumerator TodayButton_ShowsHidesWithItsDot_OnTheRightRail_AndOpensToday()
        {
            yield return Spawn();
            Assert.IsFalse(_hud.TodayVisible, "No Today button before the welcome ends (A12).");
            Assert.IsFalse(_hud.TodayRoot.gameObject.activeSelf);

            _hud.SetToday(true, true);
            Assert.IsTrue(_hud.TodayVisible);
            Assert.IsTrue(_hud.TodayDotVisible, "Its dot: a gift or a wish waits.");
            Canvas.ForceUpdateCanvases();

            // Right edge, under the top bar, inside the safe area; never on the hatch area, the egg choice or the chips.
            var today = ScreenRect(_hud.TodayRoot);
            Assert.Greater(today.center.x, Screen.width * 0.5f, "Right rail.");
            Assert.Greater(today.center.y, Screen.height * 0.5f, "Upper half, under the top bar.");
            Assert.IsTrue(Inside(today, Screen.safeArea), "Inside the safe area: " + today + " in " + Screen.safeArea);
            Assert.Less(today.yMax, Part("SafeArea/GemsChip").yMin, "Under the Gems chip.");
            foreach (var part in new[]
                     {
                         "SafeArea/HatchAction", "SafeArea/HatchAction/EggPills", "SafeArea/HatchAction/Quantity",
                         "SafeArea/HatchAction/FocusTargets", "SafeArea/LightChip", "SafeArea/GemsChip", "SafeArea/Shortcuts/RefugeLevel",
                         "SafeArea/Toast",
                     })
                Assert.IsFalse(today.Overlaps(Part(part)), "The Today button stays off " + part);
            float scale = _hud.GetComponent<Canvas>().scaleFactor;
            Assert.GreaterOrEqual(_hud.TodayRoot.rect.width, 48f * 36f / 14f - 0.5f, "48 dp touch target.");
            Assert.Greater(scale, 0f);

            var center = _hud.TodayScreenPosition;
            Assert.IsTrue(today.Contains(center), "TodayScreenPosition is the button's center.");
            Assert.IsTrue(_hud.IsOverUi(center), "A touch on Today is not a world touch.");
            _hud.TodayButton.onClick.Invoke();
            Assert.AreEqual(1, _fake.Today, "The button opens Today.");

            _hud.SetToday(true, false);
            Assert.IsFalse(_hud.TodayDotVisible);
            Assert.IsTrue(_hud.TodayVisible);
            _hud.SetToday(false, true);
            Assert.IsFalse(_hud.TodayVisible);
            Assert.IsFalse(_hud.TodayDotVisible, "No dot without its button.");
            Assert.IsFalse(_hud.IsOverUi(center), "A hidden button takes no touch.");

            var hatch = _hud.HatchScreenPosition;
            Assert.IsTrue(Part("SafeArea/HatchAction").Contains(hatch), "HatchScreenPosition is on the Hatch button.");
        }

        [UnityTest]
        public IEnumerator TodayAppear_Bounces_ThenRests_DotBreathesWithoutFlashing()
        {
            yield return Spawn();
            _hud.SetToday(true, true);
            _hud.PlayTodayAppear();
            Assert.IsTrue(_hud.TodayAppearing);
            Assert.Less(_hud.TodayRoot.localScale.x, 0.2f, "It starts small.");
            float peak = 0f;
            for (int i = 0; i < 40; i++)
            {
                _hud.Tick(Frame);
                peak = Mathf.Max(peak, _hud.TodayRoot.localScale.x);
            }
            Assert.Greater(peak, 1.03f, "A soft overshoot (0.35 s bounce, A12).");
            Assert.Less(peak, 1.2f);
            Assert.IsFalse(_hud.TodayAppearing, "Done after 0.35 s.");
            Assert.AreEqual(1f, _hud.TodayRoot.localScale.x, 1e-4f, "It ends at rest.");
            Assert.AreEqual(1f, _hud.TodayRoot.GetComponent<CanvasGroup>().alpha, 1e-4f);

            // The dot breathes slowly: always shown, alpha untouched, scale between 1 and 1.15.
            var dot = _hud.TodayRoot.Find("Dot");
            var image = dot.GetComponent<UnityEngine.UI.Image>();
            float alpha = image.color.a;
            float low = 2f, high = 0f;
            for (int i = 0; i < 300; i++)
            {
                _hud.Tick(Frame);
                Assert.IsTrue(dot.gameObject.activeSelf, "Never blinks off.");
                Assert.AreEqual(alpha, image.color.a, 1e-5f, "Never flashes.");
                low = Mathf.Min(low, dot.localScale.x);
                high = Mathf.Max(high, dot.localScale.x);
            }
            Assert.GreaterOrEqual(low, 0.999f);
            Assert.Greater(high, 1.05f, "It breathes.");
            Assert.LessOrEqual(high, 1.15f, "Gently.");

            _hud.SetToday(true, false);
            _hud.Tick(Frame);
            Assert.AreEqual(Vector3.one, dot.localScale, "Back at rest without the dot.");
            Assert.AreEqual(Quaternion.identity, _hud.TodayRoot.Find("Button/Icon").localRotation, "The page rests too.");
        }

        [UnityTest]
        public IEnumerator TodayAppear_WithReduceMotion_OnlyFades_AndNothingMoves()
        {
            yield return Spawn(reduceMotion: true);
            _hud.SetToday(true, true);
            _hud.PlayTodayAppear();
            var group = _hud.TodayRoot.GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, group.alpha, 1e-4f, "It fades in.");
            for (int i = 0; i < 40; i++)
            {
                _hud.Tick(Frame);
                Assert.AreEqual(1f, _hud.TodayRoot.localScale.x, 1e-5f, "No bounce with Reduce Motion.");
                Assert.AreEqual(Vector3.one, _hud.TodayRoot.Find("Dot").localScale, "No breathing with Reduce Motion.");
            }
            Assert.AreEqual(1f, group.alpha, 1e-4f);
        }

        [UnityTest]
        public IEnumerator TodayHint_PawOnTheButton_UntilTakenAway()
        {
            yield return Spawn();
            _hud.SetToday(true, true);
            _hud.SetTodayHint(true);
            Tick(0.5f);
            Assert.IsTrue(_hud.TodayHintShown);
            Assert.IsTrue(_hud.TapHint.Visible, "The tutorial paw (A12) taps on Today.");
            _hud.SetTodayHint(false);
            Tick(0.6f);
            Assert.IsFalse(_hud.TapHint.Visible);
            Assert.IsFalse(_hud.TapHint.Shown, "It fades out and leaves.");
        }

        [UnityTest]
        public IEnumerator GemsChip_ShowsFromTheFirstGem_WithoutTheShop()
        {
            yield return Spawn();
            var chip = _hud.transform.Find("SafeArea/GemsChip").gameObject;
            _hud.SetShop(false, 0);
            _hud.SetGems(false, 0);
            Assert.IsFalse(chip.activeSelf, "No Gems chip before the first Gem.");

            _hud.SetGems(true, 3);
            Assert.IsTrue(chip.activeSelf, "A10: the counter appears with the first Gems, shop or not.");
            Assert.IsTrue(_hud.GemsChipVisible);
            Assert.IsFalse(_hud.ShopVisible, "The shop button keeps its own rule.");
            Assert.AreEqual("3", _hud.GemsText);

            _hud.SetGems(false, 3);
            _hud.SetShop(true, 3);
            Assert.IsTrue(chip.activeSelf, "The shop asks for it too.");
            _hud.SetShop(false, 3);
            Assert.IsFalse(chip.activeSelf, "Hidden only when neither asks.");
        }

        [UnityTest]
        public IEnumerator GemsCollect_MotesFly_TheCounterChangesAsTheyLand_AndPops()
        {
            yield return Spawn();
            _hud.SetGems(true, 0);
            Assert.AreEqual("0", _hud.GemsText);
            _hud.PlayGemsCollect(new Vector2(Screen.width * 0.5f, Screen.height * 0.4f));
            _hud.SetGems(true, 3);
            Assert.IsTrue(_hud.GemsCollectPlaying);
            Assert.AreEqual(3, _hud.GemsCollect.ActiveMotes, "Doc 05 sheet 17: three Gem motes.");
            Assert.AreEqual("0", _hud.GemsText, "The counter waits for the motes.");

            var chip = _hud.transform.Find("SafeArea/GemsChip");
            float peak = 1f;
            for (int i = 0; i < 120 && _hud.GemsCollect.Landed == 0; i++)
                _hud.Tick(Frame);
            Assert.Greater(_hud.GemsCollect.Landed, 0, "The motes land.");
            _hud.Tick(Frame);
            Assert.AreEqual("3", _hud.GemsText, "The counter changes as the first mote lands.");
            for (int i = 0; i < 60; i++)
            {
                _hud.Tick(Frame);
                peak = Mathf.Max(peak, chip.localScale.x);
            }
            Assert.Greater(peak, 1.02f, "The chip pops.");
            Assert.IsFalse(_hud.GemsCollectPlaying);
            Assert.AreEqual(Vector3.one, chip.localScale, "And settles.");

            // Reduce Motion: no flight, the value at once.
            _hud.ReduceMotion = true;
            _hud.PlayGemsCollect(Vector2.zero);
            _hud.SetGems(true, 6);
            Assert.AreEqual("6", _hud.GemsText);
        }

        [UnityTest]
        public IEnumerator WishCard_OpensAtTheBubble_StaysInsideTheSafeArea_ClosesOnATouchBeside()
        {
            yield return Spawn();
            var card = _hud.WishCard;
            var text = new FakeText();
            card.Prewarm(text);
            Assert.IsFalse(card.IsOpen, "Prewarm leaves it closed.");
            Assert.IsFalse(card.CardCanvas.enabled, "A closed card draws nothing.");

            var rewards = new[]
            {
                new RewardCell(RewardKind.Gems, 3), new RewardCell(RewardKind.Treat, 1, ContentId.FromSource("treat.clover_crunch")),
                new RewardCell(RewardKind.Bond, 15),
            };
            var data = new WishCardData { Text = "Hatch 2 eggs", Progress = 1, Target = 2, System = WishSystem.Hatching, CanSwap = true };
            // A bubble with room above it for the card (whatever the Game view's size): under the top bar, the card and its lift.
            float scale = _hud.GetComponent<Canvas>().scaleFactor;
            float room = (WishCardView.TopMargin + card.Card.rect.height + WishCardView.Lift + 40f) * scale;
            var point = new Vector2(Screen.width * 0.5f, Screen.safeArea.yMax - room);
            Assert.Greater(point.y, Screen.safeArea.yMin + 20f, "The Game view is tall enough for this test.");
            card.Open(data, rewards, rewards.Length, point, text, _fake);
            Assert.IsTrue(card.IsOpen);
            Assert.IsTrue(card.CardCanvas.enabled);
            Assert.Greater(card.CardCanvas.sortingOrder, 21, "Above the Upgrades panel.");
            Assert.Less(card.CardCanvas.sortingOrder, 34, "Under the windows.");
            for (int i = 0; i < 20; i++)
                card.Tick(Frame);
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual("Hatch 2 eggs", card.TextShown);
            Assert.AreEqual("1/2", card.ProgressText);
            Assert.AreEqual(3, card.RewardsShown, "Gems, a treat, bond.");
            Assert.AreNotSame(card.RewardIcon(0), card.RewardIcon(1));
            Assert.IsTrue(card.SwapVisible, "« Changer » while the free swap remains.");
            Assert.IsFalse(card.ClaimVisible);

            // At the bubble: above it, centered on it, never over it.
            var rect = ScreenRect(card.Card);
            Assert.IsTrue(card.Above);
            Assert.Greater(rect.yMin, point.y, "Above the bubble.");
            Assert.AreEqual(point.x, rect.center.x, 1f, "Centered on it.");
            Assert.IsFalse(rect.Contains(point), "The bubble stays visible.");
            Assert.IsTrue(card.IsOverCard(rect.center));
            Assert.IsTrue(_hud.IsOverUi(point), "While the card is open, a touch is the card's (A08).");

            // The bubble walks to the edges: the card follows and stays inside the safe area.
            var safe = Screen.safeArea;
            var points = new[]
            {
                new Vector2(2f, Screen.height * 0.5f), new Vector2(Screen.width - 2f, Screen.height * 0.5f),
                new Vector2(Screen.width * 0.5f, Screen.height - 4f), new Vector2(Screen.width * 0.3f, 6f),
                new Vector2(Screen.width - 10f, Screen.height - 10f),
            };
            foreach (var p in points)
            {
                card.Follow(p);
                card.Tick(Frame);
                rect = ScreenRect(card.Card);
                Assert.IsTrue(Inside(rect, safe), $"Bubble at {p}: card {rect} inside {safe}.");
            }
            card.Follow(new Vector2(Screen.width * 0.5f, Screen.height - 4f));
            Assert.IsFalse(card.Above, "Too close to the top: the card turns to below the animal.");
            Assert.Less(ScreenRect(card.Card).yMax, Screen.height - 4f);

            // Swap, then the wish is fulfilled: « Récupérer », the card claims.
            card.Follow(point);
            card.SwapButton.onClick.Invoke();
            Assert.AreEqual(1, _fake.Swaps);
            card.BodyButton.onClick.Invoke();
            Assert.AreEqual(0, _fake.Claims, "A wish not fulfilled is not claimed by touching the card.");
            var icon = card.IconShown;
            card.Refresh(new WishCardData { Text = "Hatch 2 eggs", Progress = 2, Target = 2, Done = true, System = WishSystem.Hatching });
            Assert.IsTrue(card.ClaimVisible);
            Assert.IsFalse(card.SwapVisible);
            Assert.AreEqual("2/2", card.ProgressText);
            Assert.AreNotSame(icon, card.IconShown, "The check with a heart.");
            card.ClaimButton.onClick.Invoke();
            Assert.AreEqual(1, _fake.Claims);
            card.BodyButton.onClick.Invoke();
            Assert.AreEqual(2, _fake.Claims, "A fulfilled wish's card claims on a touch anywhere.");

            // A touch beside it closes it and tells the presenter.
            card.OutsideButton.onClick.Invoke();
            Assert.IsFalse(card.IsOpen);
            Assert.AreEqual(1, _fake.Closed);
            Assert.IsTrue(_hud.IsOverUi(point), "The closing touch stays the card's in its frame.");
            for (int i = 0; i < 20; i++)
                card.Tick(Frame);
            Assert.IsFalse(card.Closing);
            Assert.IsFalse(card.CardCanvas.enabled, "Closed: the canvas is off.");
            Assert.IsFalse(card.IsOverCard(point));

            // Closed by the presenter (a claim, a screen opening): no callback.
            card.Open(data, rewards, rewards.Length, point, text, _fake);
            card.Close();
            Assert.AreEqual(1, _fake.Closed);
        }

        [UnityTest]
        public IEnumerator SteadyTicks_DoNotAllocate()
        {
            yield return Spawn();
            var card = _hud.WishCard;
            var text = new FakeText();
            card.Prewarm(text);
            _hud.SetToday(true, true);
            _hud.SetTodayHint(true);
            _hud.SetGems(true, 3);
            var rewards = new[] { new RewardCell(RewardKind.Gems, 3), new RewardCell(RewardKind.Bond, 15) };
            var data = new WishCardData { Text = "Hatch 2 eggs", Progress = 1, Target = 2, System = WishSystem.Hatching, CanSwap = true };
            var point = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
            card.Open(data, rewards, rewards.Length, point, text, _fake);
            float x = point.x;
            for (int i = 0; i < 120; i++)
            {
                x += 1.5f;
                _hud.Tick(Frame);
                card.Follow(new Vector2(x, point.y));
                card.Tick(Frame);
            }
            Assert.That(() =>
            {
                for (int i = 0; i < 300; i++)
                {
                    x += i % 2 == 0 ? 1.5f : -1.5f;
                    _hud.SetToday(true, true);
                    _hud.SetGems(true, 3);
                    _hud.Tick(Frame);
                    card.Follow(new Vector2(x, point.y));
                    card.Refresh(data);
                    card.Tick(Frame);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        private sealed class Fake : IRefugeActions, IDailyHudActions, IWishCardActions
        {
            public int Today;
            public int Claims;
            public int Swaps;
            public int Closed;

            public void RequestHatch()
            {
            }

            public void OpenCodex()
            {
            }

            public void OpenShop()
            {
            }

            public void OpenEggInfo()
            {
            }

            public void OpenToday() => Today++;
            public void ClaimWishFromCard() => Claims++;
            public void SwapWishFromCard() => Swaps++;
            public void WishCardClosed() => Closed++;
        }

        private sealed class FakeText : ILocalization
        {
            public string Language => "en";
            public string Get(string key) => key;
        }
    }
}
