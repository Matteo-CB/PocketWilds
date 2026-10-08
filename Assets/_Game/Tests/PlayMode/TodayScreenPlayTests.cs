using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using UnityEngine.UI;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T36 screens (doc 06 screens 031, 032 and 004; doc 01 Contenu sections 1.4 to 6; doc 05 Sensations sheets 18 and 19),
    /// PlayMode with fake models (the presenter is tested with the real scene elsewhere). Today opens and closes in one
    /// gesture (a touch above the sheet, Close, Back); the gift's opening ends with every cell of the lot shown and
    /// « À demain »; wish rows are rebound in place (9, then 2); the paw stamp plays on today's box; the journal pays its
    /// goals and its chest; the return gift block shows for a long absence and hides for an ordinary one; the A12 paw taps
    /// on the box until it opens; ticks and refreshes allocate nothing after warm-up; the first opening after the prewarm
    /// creates no object.
    /// </summary>
    public class TodayScreenPlayTests
    {
        private const string TodayPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_Today.prefab";
        private const string JournalPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_WeeklyJournal.prefab";
        private const string OfflinePath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_OfflineReturn.prefab";
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

        // ------------------------------------------------------------------------------------------- Today

        [UnityTest]
        public IEnumerator Today_OpensAndClosesInOneGesture()
        {
            var screen = Spawn<TodayScreen>(TodayPath);
            var model = new FakeToday(screen);
            screen.Prewarm(model, _text, model);
            yield return null;
            var canvas = screen.GetComponent<Canvas>();
            Assert.IsFalse(screen.IsOpen);
            Assert.IsFalse(canvas.enabled, "A closed screen costs nothing: canvas off.");

            for (int gesture = 0; gesture < 3; gesture++)
            {
                screen.Open(model, _text, model);
                Assert.IsTrue(screen.IsOpen);
                Assert.IsTrue(canvas.enabled);
                Tick(screen, 0.3f);
                int closes = model.Closes;
                if (gesture == 0)
                    screen.OutsideButton.onClick.Invoke();
                else if (gesture == 1)
                    screen.CloseButton.onClick.Invoke();
                else
                    screen.PressBack();
                Assert.AreEqual(closes + 1, model.Closes, "One gesture asks to close.");
                Assert.IsFalse(screen.IsOpen, "Closing at once.");
                Tick(screen, 0.4f);
                Assert.IsFalse(canvas.enabled, "Closed: canvas disabled.");
                Assert.IsFalse(screen.Visible);
            }
        }

        [UnityTest]
        public IEnumerator Gift_OpeningEndsWithEveryCellVisible()
        {
            yield return GiftOpening(false);
            yield return GiftOpening(true);
        }

        private IEnumerator GiftOpening(bool reduceMotion)
        {
            var screen = Spawn<TodayScreen>(TodayPath);
            var model = new FakeToday(screen) { Gift = TodayGiftState.Ready };
            screen.ReduceMotion = reduceMotion;
            screen.Prewarm(model, _text, model);
            screen.Open(model, _text, model);
            yield return null;
            var gift = screen.Gift;
            Assert.IsTrue(screen.GiftShown);
            Assert.IsTrue(gift.OpenShown, "« Ouvrir » while the box is closed.");
            Assert.AreEqual(0, gift.CellsVisible, "The box never shows a lot before the real one.");

            gift.BoxButton.onClick.Invoke();
            Assert.AreEqual(1, model.GiftOpens, "A touch on the box opens it.");
            Assert.IsTrue(gift.Revealing, "The opening plays.");
            Assert.AreEqual(0, gift.CellsVisible, "Cells wait for the lid.");
            Tick(screen, 0.5f);
            if (!reduceMotion)
                Assert.Greater(screen.Burst.ActiveImages, 0, "H-VFX-071: round glints and rings.");
            // Doc 05 sheet 18: 1 600 ms plus 220 ms per object.
            Tick(screen, 1.6f + 0.22f * 3f);
            Assert.IsFalse(gift.Revealing);
            Assert.AreEqual(3, gift.CellsVisible, "Every object of the lot is shown.");
            Assert.IsTrue(gift.TomorrowShown, "« À demain ».");
            Assert.IsFalse(gift.OpenShown);
            Assert.AreEqual("Big gift", gift.SizeText);
            Assert.AreEqual("+10", gift.Cell(0).AmountText, "Gems as the wish card writes them.");
            StringAssert.StartsWith("+", gift.Cell(1).AmountText, "The Light really given.");
            Assert.AreEqual("×1", gift.Cell(2).AmountText, "One Discovery egg.");
            Assert.Greater(gift.BandColor.r, gift.BandColor.b, "A big lot's band is gold.");

            // A touch during a later opening shows everything at once.
            model.Gift = TodayGiftState.Ready;
            screen.Refresh();
            gift.BoxButton.onClick.Invoke();
            gift.BoxButton.onClick.Invoke();
            Assert.IsFalse(gift.Revealing, "Touch to pass.");
            Assert.AreEqual(3, gift.CellsVisible);
        }

        [UnityTest]
        public IEnumerator WishRows_AreRebound_NineThenTwo()
        {
            var screen = Spawn<TodayScreen>(TodayPath);
            var model = new FakeToday(screen) { WishCount = 9 };
            model.Rows[0].Card.Done = true;
            model.Rows[0].Card.Progress = 3;
            screen.Prewarm(model, _text, model);
            int objects = screen.GetComponentsInChildren<Transform>(true).Length;
            screen.Open(model, _text, model);
            yield return null;
            Assert.AreEqual(9, screen.RowsShown);
            Assert.IsTrue(screen.WaitingShown, "Nine wishes wait: nothing new is drawn (doc 01 section 2.4).");
            Assert.IsTrue(screen.ClaimAllShown, "« Tout récupérer » with a fulfilled wish.");
            Assert.IsTrue(screen.Row(0).ClaimShown);
            Assert.IsFalse(screen.Row(0).SwapShown);
            Assert.IsTrue(screen.Row(1).SwapShown, "« Changer » while the day's swap remains.");
            Assert.AreEqual("1/3", screen.Row(1).ProgressText);

            model.WishCount = 2;
            model.Rows[0].Card.Done = false;
            model.Rows[0].Card.Progress = 1;
            screen.Refresh();
            Assert.AreEqual(2, screen.RowsShown, "Two rows shown, seven kept for later.");
            Assert.AreEqual(objects, screen.GetComponentsInChildren<Transform>(true).Length, "Rows are rebound, never created.");
            Assert.IsFalse(screen.WaitingShown);
            Assert.IsFalse(screen.ClaimAllShown);
            Assert.AreEqual(model.Rows[1].Card.Text, screen.Row(1).Text);

            model.SwapsLeft = 0;
            screen.Refresh();
            Assert.IsFalse(screen.Row(1).SwapShown);
            Assert.IsTrue(screen.SwapUsedShown, "« Changement gratuit utilisé. Le prochain arrive demain. »");

            model.Rows[1].Card.Done = true;
            screen.Refresh();
            screen.Row(1).ClaimButton.onClick.Invoke();
            Assert.AreEqual(1, model.ClaimedRow, "« Récupérer » claims its own row.");

            model.WishCount = 0;
            screen.Refresh();
            Assert.AreEqual(0, screen.RowsShown);
            Assert.IsTrue(screen.WaitingShown, "« Tes souhaits t'attendent ».");
        }

        [UnityTest]
        public IEnumerator Stamp_PlaysOnTodaysBox()
        {
            var screen = Spawn<TodayScreen>(TodayPath);
            var model = new FakeToday(screen);
            model.SetCalendar(6, true);
            screen.Prewarm(model, _text, model);
            screen.Open(model, _text, model);
            yield return null;
            var calendar = screen.Calendar;
            Assert.AreEqual(6, calendar.TodayBox);
            Assert.IsTrue(calendar.BigMarked(6), "Box 7 is a big box.");
            Assert.IsFalse(calendar.BigMarked(5));
            Assert.IsTrue(calendar.StampShown(0) && calendar.StampShown(5), "Stamped boxes keep their paw.");
            Assert.IsFalse(calendar.StampShown(6));
            Assert.AreEqual("Big box today", calendar.BigInText);
            Assert.AreEqual("Tomorrow: 30 min of Light", calendar.TomorrowText);

            // A touch on a box ahead only answers; today's box stamps.
            calendar.BoxButton(9).onClick.Invoke();
            Assert.AreEqual(0, model.Stamps);
            calendar.BoxButton(6).onClick.Invoke();
            Assert.AreEqual(1, model.Stamps, "One touch stamps today's box.");
            Assert.IsTrue(calendar.Stamping);
            Assert.IsTrue(calendar.StampShown(6));
            Assert.Greater(calendar.StampScale(6).x, 1.2f, "The paw comes down from 1.4.");
            Tick(screen, 0.2f);
            Assert.Greater(screen.Burst.ActiveImages, 0, "H-VFX-072 with round stars at a big box.");
            Tick(screen, 1.5f);
            Assert.IsFalse(calendar.Stamping);
            Assert.AreEqual(1f, calendar.StampScale(6).x, 1e-3f);
            Assert.IsFalse(calendar.StampButton.gameObject.activeSelf, "Stamped for today.");

            // Real days to the next big box, never a countdown.
            model.SetCalendar(2, true);
            model.DaysToBigBox = 5;
            screen.Refresh();
            Assert.AreEqual("4 more days", calendar.BigInText);
            model.SetCalendar(3, false);
            model.DaysToBigBox = 1;
            screen.Refresh();
            Assert.AreEqual("Big box tomorrow", calendar.BigInText);
        }

        [UnityTest]
        public IEnumerator GiftHint_TapsOnTheBox_UntilItOpens()
        {
            var screen = Spawn<TodayScreen>(TodayPath);
            var model = new FakeToday(screen) { Gift = TodayGiftState.Ready };
            screen.Prewarm(model, _text, model);
            screen.Open(model, _text, model);
            yield return null;
            Tick(screen, 0.3f);
            screen.SetGiftHint(true);
            Assert.IsTrue(screen.HintShown, "A12: the paw taps on the gift box.");
            Tick(screen, 0.5f);
            Assert.IsTrue(screen.Hint.Shown);
            var hint = (RectTransform)screen.Hint.transform;
            Vector2 at = RectTransformUtility.WorldToScreenPoint(null, hint.TransformPoint(hint.rect.center));
            Assert.Less(Vector2.Distance(at, screen.GiftScreenPosition), 4f, "The paw's ring is on the box.");

            screen.ReduceMotion = true;
            Assert.IsTrue(screen.Hint.ReduceMotion, "Reduce Motion: the paw stays still, the ring breathes.");
            screen.ReduceMotion = false;

            screen.Gift.BoxButton.onClick.Invoke();
            Assert.IsFalse(screen.HintShown, "The paw leaves once the box opens.");
            screen.SetGiftHint(true);
            Assert.IsFalse(screen.HintShown, "Never on an open box.");
        }

        // ------------------------------------------------------------------------------------------- journal

        [UnityTest]
        public IEnumerator Journal_ClaimsGoalsAndTheChest()
        {
            var today = Spawn<TodayScreen>(TodayPath);
            var todayModel = new FakeToday(today);
            var screen = Spawn<WeeklyJournalScreen>(JournalPath);
            var model = new FakeJournal(screen);
            today.Prewarm(todayModel, _text, todayModel);
            screen.Prewarm(model, _text, model);
            today.Open(todayModel, _text, todayModel);
            Assert.IsTrue(today.JournalShown, "The journal's row in Today.");
            Assert.AreEqual("Weekly journal 2/7", today.JournalText);
            today.JournalButton.onClick.Invoke();
            Assert.AreEqual(1, todayModel.JournalOpens);

            screen.Open(model, _text, model);
            yield return null;
            Assert.IsTrue(WeeklyJournalScreen.AnyOpen);
            Assert.AreEqual(7, screen.RowsShown);
            Assert.AreEqual("Your weekly journal", screen.TitleText, "The first journal's title.");
            Assert.AreEqual("3 days left", screen.DaysText, "Real days, never a countdown.");
            Assert.IsTrue(screen.Row(0).ClaimShown);
            Assert.IsTrue(screen.Row(1).HeadStartShown, "« Tu as déjà commencé » on a goal that started ahead.");
            Assert.IsFalse(screen.Row(2).ClaimShown);
            Assert.IsFalse(screen.ChestClaimShown);

            // Back closes the journal only: Today stays.
            today.PressBack();
            Assert.AreEqual(0, todayModel.Closes, "Today leaves Back to the journal above it.");

            screen.Row(0).ClaimButton.onClick.Invoke();
            Assert.AreEqual(0, model.ClaimedGoal);
            Assert.IsTrue(screen.Row(0).CheckShown);
            Assert.Greater(screen.Burst.ActiveImages, 0, "The claim's burst.");

            model.DaysLeft = 1;
            screen.Refresh();
            Assert.AreEqual("Last day", screen.DaysText);

            model.Complete = true;
            screen.Refresh();
            Assert.IsTrue(screen.ChestClaimShown);
            screen.ChestClaimButton.onClick.Invoke();
            Assert.AreEqual(1, model.ChestClaims);
            Assert.IsTrue(screen.ChestOpening, "The chest opens.");
            Tick(screen, 1.5f);
            Assert.IsFalse(screen.ChestOpening);
            Assert.IsTrue(screen.ChestCheckShown);

            screen.OutsideButton.onClick.Invoke();
            Assert.AreEqual(1, model.Closes);
            Tick(screen, 0.4f);
            Assert.IsFalse(WeeklyJournalScreen.AnyOpen);
            Assert.IsTrue(today.IsOpen, "Today is still there.");
        }

        // ------------------------------------------------------------------------------------------- return gift

        [UnityTest]
        public IEnumerator ReturnGiftBlock_ShowsForALongAbsence_HidesForAnOrdinaryOne()
        {
            var screen = Spawn<OfflineReturnScreen>(OfflinePath);
            yield return null;
            var actions = new FakeOffline();
            screen.ClearGift();
            float plain = screen.CardHeight;
            Assert.IsFalse(screen.GiftShown);

            var cells = new RewardCell[RewardCells.Max];
            var bundle = new RewardBundle { LightMinutes = 120, Tickets = 3, DiscoveryEggs = 1, Treats = 3, Treat = ContentId.FromSource("treat.clover_crunch") };
            int count = RewardCells.FromBundle(bundle, cells);
            var card = new ReturnGiftCard { Big = false, DaysAway = 4, Line0 = "3 wishes are waiting" };
            screen.SetGift(card, cells, count, _text);
            screen.Show(BigValue.FromDouble(500), _text, actions);
            Assert.IsTrue(screen.GiftShown);
            Assert.AreEqual(4, screen.GiftCellsShown);
            Assert.AreEqual(1, screen.GiftLinesShown);
            Assert.AreEqual("Welcome back", screen.TitleText);
            Assert.AreEqual("A gift for you", screen.GiftSubtitle);
            Assert.Greater(screen.CardHeight, plain, "The card grows to hold the gift.");
            for (int i = 0; i < 80; i++)
                screen.Tick(Frame);
            for (int i = 0; i < count; i++)
                Assert.IsTrue(screen.GiftCell(i).FullyVisible, "Every cell popped in.");

            Button collect = null;
            foreach (var button in screen.GetComponentsInChildren<Button>(true))
                if (button.name == "Collect")
                    collect = button;
            collect.onClick.Invoke();
            Assert.AreEqual(1, actions.Collects, "One « Récupérer » collects the Light and the gift.");
            for (int i = 0; i < 30; i++)
                screen.Tick(Frame);

            screen.SetGift(new ReturnGiftCard { Big = true, DaysAway = 15 }, cells, count, _text);
            Assert.AreEqual("A big gift for you", screen.GiftSubtitle);
            Assert.AreEqual(0, screen.GiftLinesShown);
            screen.ClearGift();
            Assert.IsFalse(screen.GiftShown, "An ordinary return: no gift block.");
            Assert.AreEqual(plain, screen.CardHeight, 1e-3f);
        }

        // ------------------------------------------------------------------------------------------- performance

        [UnityTest]
        public IEnumerator TickAndRefresh_AllocateNothing_AfterWarmup()
        {
            var screen = Spawn<TodayScreen>(TodayPath);
            var model = new FakeToday(screen) { Gift = TodayGiftState.Opened, WishCount = 9 };
            model.SetCalendar(3, true);
            model.Rows[2].Card.Done = true;
            screen.Prewarm(model, _text, model);
            screen.Open(model, _text, model);
            var journal = Spawn<WeeklyJournalScreen>(JournalPath);
            var journalModel = new FakeJournal(journal);
            journal.Prewarm(journalModel, _text, journalModel);
            yield return null;
            for (int i = 0; i < 120; i++)
            {
                screen.Tick(Frame);
                screen.Refresh();
            }
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                    screen.Tick(Frame);
                for (int i = 0; i < 60; i++)
                    screen.Refresh();
            }, Is.Not.AllocatingGCMemory(), "Today: steady ticks and refreshes allocate nothing.");

            screen.Close();
            Tick(screen, 0.4f);
            journal.Open(journalModel, _text, journalModel);
            for (int i = 0; i < 60; i++)
            {
                journal.Tick(Frame);
                journal.Refresh();
            }
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                    journal.Tick(Frame);
                for (int i = 0; i < 60; i++)
                    journal.Refresh();
            }, Is.Not.AllocatingGCMemory(), "Journal: steady ticks and refreshes allocate nothing.");
            journal.Close();
            Tick(journal, 0.4f);
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                {
                    screen.Tick(Frame);
                    journal.Tick(Frame);
                }
            }, Is.Not.AllocatingGCMemory(), "Closed screens cost nothing.");
        }

        [UnityTest]
        public IEnumerator Prewarm_ThenFirstOpening_CreatesNoObject()
        {
            var screen = Spawn<TodayScreen>(TodayPath);
            var model = new FakeToday(screen) { Gift = TodayGiftState.Ready, WishCount = 3 };
            model.SetCalendar(13, true);
            model.Rows[0].Card.Done = true;
            screen.Prewarm(model, _text, model);
            yield return null;
            int objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            screen.Open(model, _text, model);
            screen.Gift.BoxButton.onClick.Invoke();
            screen.Calendar.BoxButton(13).onClick.Invoke();
            screen.ClaimAllButton.onClick.Invoke();
            Tick(screen, 2.5f);
            yield return null;
            Assert.AreEqual(objects, Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                "Bursts, cells and rows are pooled: nothing instantiated after the prewarm.");
        }

        // ------------------------------------------------------------------------------------------- helpers

        private T Spawn<T>(string path) where T : Component
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assume.That(prefab != null, "Run Pocket Wilds/UI/Build Daily Visit (T36) first: " + path);
            var go = Object.Instantiate(prefab);
            _spawned.Add(go);
            return go.GetComponent<T>();
#else
            Assert.Ignore("Editor only.");
            return null;
#endif
        }

        private static void Tick(TodayScreen screen, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                screen.Tick(Frame);
        }

        private static void Tick(WeeklyJournalScreen screen, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                screen.Tick(Frame);
        }

        private sealed class FakeText : ILocalization
        {
            private readonly Dictionary<string, string> _texts = new Dictionary<string, string>
            {
                { DailyVisitText.TodayTitle, "Today" },
                { DailyVisitText.TodayClose, "Close" },
                { DailyVisitText.TodayClaim, "Claim" },
                { DailyVisitText.TodayClaimAll, "Claim all" },
                { DailyVisitText.WelcomeTitle, "Welcome gift" },
                { DailyVisitText.WelcomeDay, "Day {n} of 3" },
                { DailyVisitText.GiftTitle, "Daily gift" },
                { DailyVisitText.GiftOpen, "Open" },
                { DailyVisitText.GiftTomorrow, "See you tomorrow" },
                { DailyVisitText.GiftSmall, "Small gift" },
                { DailyVisitText.GiftNice, "Nice gift" },
                { DailyVisitText.GiftBig, "Big gift" },
                { DailyVisitText.WishesTitle, "Wishes" },
                { DailyVisitText.WishWaiting, "Your wishes are waiting" },
                { DailyVisitText.WishSwap, "Swap" },
                { DailyVisitText.WishSwapUsed, "Free swap used. Next one tomorrow." },
                { DailyVisitText.WishClaim, "Claim" },
                { DailyVisitText.CalendarTitle, "Calendar" },
                { DailyVisitText.CalendarStamp, "Stamp" },
                { DailyVisitText.CalendarDone, "Stamped for today" },
                { DailyVisitText.CalendarTomorrow, "Tomorrow: {reward}" },
                { DailyVisitText.CalendarBigIn, "{n} more days" },
                { DailyVisitText.CalendarBigIn + ".one", "{n} more day" },
                { DailyVisitText.CalendarBigIn + ".other", "{n} more days" },
                { DailyVisitText.CalendarBigTomorrow, "Big box tomorrow" },
                { DailyVisitTextFormatter.CalendarBigTodayKey, "Big box today" },
                { DailyVisitText.CalendarCycle, "Cycle {n} decoration" },
                { DailyVisitText.JournalTitle, "Weekly journal" },
                { DailyVisitText.JournalRow, "Weekly journal {n}/{total}" },
                { DailyVisitText.JournalDaysLeft, "{n} days left" },
                { DailyVisitText.JournalDaysLeft + ".one", "{n} day left" },
                { DailyVisitText.JournalDaysLeft + ".other", "{n} days left" },
                { DailyVisitText.JournalLastDay, "Last day" },
                { DailyVisitText.JournalFirst, "Your weekly journal" },
                { DailyVisitText.JournalHeadStart, "Off to a good start" },
                { DailyVisitText.JournalClaim, "Claim" },
                { DailyVisitText.JournalComplete, "Journal complete" },
                { DailyVisitText.JournalCarry, "Saved for you from last week" },
                { DailyVisitText.ReturnTitle, "Welcome back" },
                { DailyVisitText.ReturnSubtitle, "A gift for you" },
                { DailyVisitText.ReturnSubtitleBig, "A big gift for you" },
                { DailyVisitText.RewardLightMinutes, "{n} min" },
                { DailyVisitTextFormatter.LightMinutesWordsKey, "{n} min of Light" },
                { DailyVisitText.RewardGems + ".one", "{n} Gem" },
                { DailyVisitText.RewardGems + ".other", "{n} Gems" },
                { DailyVisitText.RewardTickets + ".one", "{n} ticket" },
                { DailyVisitText.RewardTickets + ".other", "{n} tickets" },
                { UiTextKeys.OfflineWelcomeBack, "Welcome back" },
                { UiTextKeys.CollectButton, "Collect" },
            };

            public string Language => "en";
            public string Get(string key) => _texts.TryGetValue(key, out var text) ? text : key;
        }

        private sealed class FakeToday : ITodayModel, ITodayActions
        {
            public readonly TodayWishRow[] Rows = new TodayWishRow[TodayScreen.MaxWishRows];
            public readonly CalendarBoxCell[] Boxes = new CalendarBoxCell[CalendarStripView.Boxes];
            private readonly TodayScreen _view;
            public int Closes;
            public int GiftOpens;
            public int Stamps;
            public int JournalOpens;
            public int ClaimedRow = -1;

            public FakeToday(TodayScreen view)
            {
                _view = view;
                for (int i = 0; i < Rows.Length; i++)
                {
                    Rows[i] = new TodayWishRow
                    {
                        Card = new WishCardData
                        {
                            Text = "Wish number " + i, Progress = 1, Target = 3, System = (WishSystem)(1 + i % 5), CanSwap = true,
                        },
                        Animal = (uint)(i + 1),
                        AnimalName = "Mochi " + i,
                        Gems = 3,
                    };
                }
                for (int i = 0; i < Boxes.Length; i++)
                {
                    var reward = (i + 1) % 7 == 0 ? new RewardCell(RewardKind.Gems, 20) : new RewardCell(RewardKind.Light, 0) { LightMinutes = 30 };
                    Boxes[i] = new CalendarBoxCell { Reward = reward, Big = (i + 1) % 7 == 0 };
                }
                SetCalendar(0, true);
            }

            public int WelcomeIndex { get; set; } = 0;
            public bool WelcomeClaimable { get; set; } = true;
            public TodayGiftState Gift { get; set; } = TodayGiftState.Ready;
            public GiftSize GiftSize { get; set; } = GiftSize.Big;
            public int WishCount { get; set; } = 3;
            public int SwapsLeft { get; set; } = 1;
            public int CalendarCycle { get; set; } = 1;
            public int CalendarNext { get; private set; }
            public bool CalendarClaimable { get; private set; }
            public RewardCell Tomorrow => new RewardCell(RewardKind.Light, 0) { LightMinutes = 30 };
            public int DaysToBigBox { get; set; } = 1;
            public bool JournalVisible => true;
            public int JournalReached => 2;
            public int JournalGoals => 7;
            public bool JournalDot => true;

            public bool AnyWishDone
            {
                get
                {
                    for (int i = 0; i < WishCount; i++)
                        if (Rows[i].Card.Done)
                            return true;
                    return false;
                }
            }

            public void SetCalendar(int next, bool claimable)
            {
                CalendarNext = next;
                CalendarClaimable = claimable;
                for (int i = 0; i < Boxes.Length; i++)
                {
                    Boxes[i].Stamped = claimable ? i < next : i <= next;
                    Boxes[i].Today = i == next;
                }
                DaysToBigBox = 7 - next % 7;
            }

            public int WelcomeRewards(RewardCell[] into)
            {
                into[0] = new RewardCell(RewardKind.Gems, 100);
                return 1;
            }

            public int GiftRewards(RewardCell[] into)
            {
                if (Gift != TodayGiftState.Opened)
                    return 0;
                into[0] = new RewardCell(RewardKind.Gems, 10);
                into[1] = new RewardCell(RewardKind.Light, 0) { LightMinutes = 30, Light = BigValue.FromDouble(1234) };
                into[2] = new RewardCell(RewardKind.DiscoveryEgg, 1);
                return 3;
            }

            public TodayWishRow Wish(int row) => Rows[row];
            public CalendarBoxCell Box(int box) => Boxes[box];

            public void ClaimWelcome()
            {
                WelcomeClaimable = false;
                _view.PlayWelcomeClaimed();
                _view.Refresh();
            }

            public void OpenGift()
            {
                GiftOpens++;
                Gift = TodayGiftState.Opened;
                _view.PlayGiftOpened();
                _view.Refresh();
            }

            public void ClaimWish(int row)
            {
                ClaimedRow = row;
                _view.PlayWishClaimed(row);
            }

            public void ClaimAllWishes()
            {
                for (int i = 0; i < WishCount; i++)
                    if (Rows[i].Card.Done)
                        _view.PlayWishClaimed(i);
                for (int i = 0; i < WishCount; i++)
                    Rows[i].Card.Done = false;
                _view.Refresh();
            }

            public void SwapWish(int row)
            {
            }

            public void StampCalendar()
            {
                Stamps++;
                int box = CalendarNext;
                SetCalendar(box, false);
                _view.PlayStamp(box);
                _view.Refresh();
            }

            public void OpenJournal() => JournalOpens++;

            public void OpenGiftInfo()
            {
            }

            public void CloseToday()
            {
                Closes++;
                _view.Close();
            }
        }

        private sealed class FakeJournal : IJournalModel, IJournalActions
        {
            private readonly JournalGoalRow[] _goals = new JournalGoalRow[7];
            private readonly WeeklyJournalScreen _view;
            public int ClaimedGoal = -1;
            public int ChestClaims;
            public int Closes;
            public bool Complete;
            public bool Claimed;

            public FakeJournal(WeeklyJournalScreen view)
            {
                _view = view;
                for (int i = 0; i < _goals.Length; i++)
                {
                    _goals[i] = new JournalGoalRow
                    {
                        Text = "Goal number " + i, Progress = i == 0 ? 30 : 6, Target = 30, Reached = i == 0, Slot = (JournalSlot)i,
                        HeadStart = i == 1, Reward0 = new RewardCell(RewardKind.Gems, 5), Reward1 = new RewardCell(RewardKind.Ticket, 2),
                    };
                }
            }

            public int GoalCount => _goals.Length;
            public JournalGoalRow Goal(int i) => _goals[i];
            public int DaysLeft { get; set; } = 3;
            public bool IsFirst => true;
            public bool CanClaimComplete => Complete && !Claimed;
            public bool CompleteClaimed => Claimed;
            public bool HasCarry => false;

            public int CompleteRewards(RewardCell[] into)
            {
                into[0] = new RewardCell(RewardKind.Gems, 15);
                into[1] = new RewardCell(RewardKind.DiscoveryEgg, 1);
                return 2;
            }

            public int CarryRewards(RewardCell[] into) => 0;

            public void ClaimGoal(int i)
            {
                ClaimedGoal = i;
                _goals[i].Claimed = true;
                _view.PlayGoalClaimed(i);
                _view.Refresh();
            }

            public void ClaimComplete()
            {
                ChestClaims++;
                Claimed = true;
                _view.PlayCompleteClaimed();
                _view.Refresh();
            }

            public void ClaimCarry()
            {
            }

            public void CloseJournal()
            {
                Closes++;
                _view.Close();
            }
        }

        private sealed class FakeOffline : IOfflineReturnActions
        {
            public int Collects;
            public void CollectOffline() => Collects++;
        }
    }
}
