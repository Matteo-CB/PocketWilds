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
    /// T37 activity screens (doc 06 screens 029 and 030; doc « Mini-jeux & activités » sections 1 and 2; doc 01 Contenu
    /// sections A15 and 12.0; doc 05 Sensations sheet 20), PlayMode with fake models (the presenter drives them in the real
    /// scene elsewhere). The hub opens and closes in one gesture, recycles 8 card views over 18 activities (locked ones show
    /// what opens them), toggles practice, shows the Play cost or « Gratuit », the real time to the next ticket, picks a
    /// challenge; the result card ends with every star and reward shown (normal, Reduce Motion, one-tap reveal) with its
    /// beats in order and the record ribbon; the in-game HUD rolls the score, lights the star marks, empties the soft ring,
    /// shows the streak and the pause card; the « no ticket » card puts the time first and dims the pack calmly; the HUD's
    /// Play button shows its ticket badge above Today, bounces once and rests, and the whole HUD fades out and takes no
    /// touch while a game plays. Ticks and refreshes allocate nothing after warm-up; nothing is instantiated after the
    /// prewarm.
    /// </summary>
    public class ActivityScreensPlayTests
    {
        private const string HubPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_ActivityHub.prefab";
        private const string HudPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_ActivityHud.prefab";
        private const string ResultPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_ActivityResult.prefab";
        private const string EmptyPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_TicketsEmpty.prefab";
        private const string RefugeHudPath = "Assets/_Game/Prefabs/UI/Screens/PF_UI_HUD_Main.prefab";
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

        // ------------------------------------------------------------------------------------------- hub (screen 029)

        [UnityTest]
        public IEnumerator Hub_OpensAndClosesInOneGesture()
        {
            var hub = Spawn<ActivityHubScreen>(HubPath);
            var model = new FakeHub(hub);
            hub.Prewarm(model, _text, model);
            yield return null;
            var canvas = hub.GetComponent<Canvas>();
            Assert.IsFalse(hub.IsOpen);
            Assert.IsFalse(canvas.enabled, "A closed screen costs nothing: canvas off.");
            Assert.AreEqual(22, canvas.sortingOrder, "Today's level: they never show together.");

            for (int gesture = 0; gesture < 3; gesture++)
            {
                hub.Open(model, _text, model);
                Assert.IsTrue(hub.IsOpen);
                Assert.IsTrue(canvas.enabled);
                Tick(hub, 0.3f);
                int closes = model.Closes;
                if (gesture == 0)
                    hub.OutsideButton.onClick.Invoke();
                else if (gesture == 1)
                    hub.CloseButton.onClick.Invoke();
                else
                    hub.PressBack();
                Assert.AreEqual(closes + 1, model.Closes, "One gesture asks to close.");
                Assert.IsFalse(hub.IsOpen);
                Tick(hub, 0.4f);
                Assert.IsFalse(canvas.enabled, "Closed: canvas disabled.");
                Assert.IsFalse(hub.Visible);
            }
        }

        [UnityTest]
        public IEnumerator Hub_RecyclesCards_EighteenActivities_LockedOnesShowTheirHint()
        {
            var hub = Spawn<ActivityHubScreen>(HubPath);
            var model = new FakeHub(hub);
            hub.Prewarm(model, _text, model);
            hub.Open(model, _text, model);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Tick(hub, 0.3f);
            int objects = hub.GetComponentsInChildren<Transform>(true).Length;
            Assert.AreEqual(ActivityHubScreen.CardPool, hub.CardViews, "8 views for 18 activities.");
            Assert.Less(hub.CardViews, model.CardCount);

            var first = hub.ViewOf(0);
            Assert.IsNotNull(first, "The first card is on screen.");
            Assert.AreEqual("Grooming", first.NameText);
            Assert.AreEqual(2, first.StarsLit, "Its best game's stars.");
            Assert.AreEqual("380", first.BestText);
            Assert.AreEqual(3, first.PipsLit, "Challenges done at 3 stars, as round pips.");
            Assert.AreEqual("3/10", first.PipText);
            Assert.IsTrue(first.FreeShown, "« Gratuit »: the first Grooming game is offered (A15).");
            Assert.IsTrue(first.Selected, "The selected card lifts.");
            Assert.IsTrue(hub.ViewOf(1).NewShown, "« Nouveau » on an activity never played.");
            Assert.IsTrue(hub.ViewOf(2).FeaturedShown, "The featured event activity is marked.");
            Assert.AreEqual(string.Empty, hub.ViewOf(1).BestText, "No record yet: no number.");

            for (int card = 0; card < model.CardCount; card++)
            {
                hub.ScrollTo(card);
                Tick(hub, Frame);
                var view = hub.ViewOf(card);
                Assert.IsNotNull(view, "Card " + card + " is bound when scrolled to.");
                Assert.AreEqual(model.Cards[card].Name, view.NameText);
                if (model.Cards[card].State == ActivityCardState.Locked)
                {
                    Assert.IsTrue(view.Locked);
                    Assert.AreEqual(model.Cards[card].UnlockHint, view.HintText, "A locked card says what opens it.");
                    Assert.AreEqual(0, view.StarsLit);
                }
            }
            Assert.IsNull(hub.ViewOf(0), "Far cards leave the window: their view is reused.");
            Assert.AreEqual(objects, hub.GetComponentsInChildren<Transform>(true).Length, "Cards are rebound, never created.");

            // A touch on a card selects its activity; the details follow.
            hub.ScrollTo(3);
            Tick(hub, Frame);
            hub.ViewOf(3).Button.onClick.Invoke();
            Assert.AreEqual(3, model.SelectedCard);
            Assert.AreEqual(model.Cards[3].Name, hub.NameText);
            Assert.IsTrue(hub.ViewOf(3).Selected);
            Assert.IsFalse(hub.ViewOf(2).Selected);

            // A locked activity: no « Jouer », no challenge, the hint on its card.
            hub.ScrollTo(10);
            Tick(hub, Frame);
            hub.ViewOf(10).Button.onClick.Invoke();
            Assert.IsFalse(hub.PlayEnabled, "A locked activity does not play.");
            Assert.IsFalse(hub.ChallengesShown);
        }

        [UnityTest]
        public IEnumerator Hub_PracticeToggle_SaysFree_AndThePlayCost()
        {
            var hub = Spawn<ActivityHubScreen>(HubPath);
            var model = new FakeHub(hub);
            model.Cards[0].FreeGame = false;
            hub.Prewarm(model, _text, model);
            hub.Open(model, _text, model);
            yield return null;
            Tick(hub, 0.3f);
            Assert.IsFalse(hub.PracticeOn);
            Assert.IsTrue(hub.CostTicketShown, "A rewarded game costs a ticket.");
            Assert.AreEqual("1", hub.CostText);
            Assert.Less(hub.KnobPosition, 0f, "The switch rests on the left.");

            hub.PracticeButton.onClick.Invoke();
            Assert.AreEqual(1, model.PracticeChanges);
            Assert.IsTrue(model.Practice);
            Assert.IsTrue(hub.PracticeOn);
            Assert.IsFalse(hub.CostTicketShown, "Practice is free.");
            Assert.AreEqual("Free", hub.CostText);
            Tick(hub, 0.3f);
            Assert.Greater(hub.KnobPosition, 0f, "The switch slid to the right.");
            hub.PlayButton.onClick.Invoke();
            Assert.AreEqual(1, model.Plays, "Practice still plays with one touch.");

            hub.PracticeButton.onClick.Invoke();
            Assert.IsFalse(model.Practice);
            Assert.IsTrue(hub.CostTicketShown);

            // The first Grooming game is offered (A15): « Gratuit » without a ticket.
            model.Cards[0].FreeGame = true;
            hub.Refresh();
            Assert.AreEqual("Free", hub.CostText);
            Assert.IsFalse(hub.CostTicketShown);
        }

        [UnityTest]
        public IEnumerator Hub_TicketChip_ShowsTheRealTime_NeverRed()
        {
            var hub = Spawn<ActivityHubScreen>(HubPath);
            var model = new FakeHub(hub);
            hub.Prewarm(model, _text, model);
            hub.Open(model, _text, model);
            yield return null;
            Assert.AreEqual("3/5", hub.TicketCountText);
            Assert.AreEqual("Next ticket in 1 h 12 min", hub.TicketNextText, "Hours and minutes, never seconds.");
            model.SetTickets(3, 5, 120);
            Assert.AreEqual("Next ticket in 2 h", hub.TicketNextText);
            model.SetTickets(3, 5, 65);
            Assert.AreEqual("Next ticket in 1 h 05 min", hub.TicketNextText);
            model.SetTickets(0, 5, 7);
            Assert.AreEqual("0/5", hub.TicketCountText);
            Assert.AreEqual("Next ticket in 7 min", hub.TicketNextText);
            model.SetTickets(5, 5, 0);
            Assert.AreEqual("All your tickets are ready", hub.TicketNextText);
            var color = hub.TicketNextColor;
            Assert.Less(color.r - color.g, 0.25f, "The time is never written in red.");
            hub.TicketChip.onClick.Invoke();
            Assert.AreEqual(1, model.Buys, "« + » opens the packs.");
        }

        [UnityTest]
        public IEnumerator Hub_Challenges_PickPlayLetGo_AndThemes()
        {
            var hub = Spawn<ActivityHubScreen>(HubPath);
            var model = new FakeHub(hub);
            hub.Prewarm(model, _text, model);
            hub.Open(model, _text, model);
            yield return null;
            Assert.IsTrue(hub.ChallengesShown);
            Assert.AreEqual(10, hub.CellsShown, "The 10 challenges of the activity.");
            Assert.AreEqual(3, hub.Cell(0).StarsShown);
            Assert.IsFalse(hub.Cell(3).Locked);
            Assert.IsTrue(hub.Cell(4).Locked, "Challenges open in order.");
            Assert.AreEqual("Tap a challenge to pick it", hub.GoalText);

            hub.Cell(2).Button.onClick.Invoke();
            Assert.AreEqual(2, hub.PickedChallenge);
            Assert.IsTrue(hub.Cell(2).Picked);
            Assert.AreEqual("Challenge 3: Goal 3", hub.GoalText);
            hub.PlayButton.onClick.Invoke();
            Assert.AreEqual(2, model.PlayedChallenge, "« Jouer » plays the picked challenge.");
            Assert.AreEqual(0, model.Plays);

            hub.Cell(2).Button.onClick.Invoke();
            Assert.AreEqual(-1, hub.PickedChallenge, "A second touch lets go.");
            hub.PlayButton.onClick.Invoke();
            Assert.AreEqual(1, model.Plays, "Then « Jouer » plays the free mode.");

            hub.Cell(5).Button.onClick.Invoke();
            Assert.AreEqual(-1, hub.PickedChallenge, "A locked challenge is not picked.");
            Assert.AreEqual("A star on challenge 5 opens this one", hub.GoalText);

            // Themes: one pill per open biome, from two.
            Assert.IsTrue(hub.ThemesShown);
            Assert.AreEqual(2, hub.PillsShown);
            Assert.AreEqual("Meadow", hub.Pill(0).NameText);
            Assert.AreEqual("Forest", hub.Pill(1).NameText);
            Assert.IsTrue(hub.Pill(0).Chosen);
            hub.Pill(1).Button.onClick.Invoke();
            Assert.AreEqual(1, model.SelectedTheme);
            Assert.IsTrue(hub.Pill(1).Chosen);
            model.ThemeCount = 1;
            hub.Refresh();
            Assert.IsFalse(hub.ThemesShown, "One theme: nothing to choose.");
        }

        // ------------------------------------------------------------------------------------------- result (sheet 20)

        [UnityTest]
        public IEnumerator Result_SequenceEndsWithEveryStarAndReward()
        {
            yield return ResultSequence(false);
            yield return ResultSequence(true);
        }

        private IEnumerator ResultSequence(bool reduceMotion)
        {
            var result = Spawn<ActivityResultScreen>(ResultPath);
            var beats = new FakeBeats();
            var actions = new FakeResultActions();
            result.ReduceMotion = reduceMotion;
            result.SetBeats(beats);
            result.Prewarm(_text);
            Assert.AreEqual(0, beats.Count, "The prewarm plays no beat.");
            var rewards = Rewards(out int count);
            var model = new ActivityResultModel
            {
                Activity = ActivityId.Grooming, Mode = ActivityMode.Free, Score = 520, Best = 480, Stars = 3, NewRecord = true, Rewarded = true,
                FixedStickerRule = true,
            };
            result.Open(model, rewards, count, _text, actions);
            yield return null;
            Assert.AreEqual(30, result.GetComponent<Canvas>().sortingOrder);
            Assert.AreEqual("Perfect!", result.TitleText);
            Assert.AreEqual("Grooming", result.SubtitleText);
            Assert.AreEqual(4, result.RewardsShown);
            Assert.IsFalse(result.Finished);

            float maxStar = 0f;
            float t = 0f;
            void Run(float until)
            {
                for (; t < until; t += Frame)
                {
                    result.Tick(Frame);
                    for (int i = 0; i < 3; i++)
                        maxStar = Mathf.Max(maxStar, result.StarScale(i));
                }
            }

            Run(0.3f);
            Assert.AreEqual(1, beats.Stars.Count, "The first star lands after the card's slide (200 ms).");
            Run(0.62f);
            Assert.AreEqual(2, beats.Stars.Count, "350 ms a star.");
            int rolling = int.Parse(result.ScoreText);
            Assert.Greater(rolling, 0);
            Assert.Less(rolling, 520, "The score rolls while the stars pop.");
            Run(1.2f);
            Assert.AreEqual(3, beats.Stars.Count);
            Assert.AreEqual("520", result.ScoreText, "600 ms of roll.");
            Assert.AreEqual(0, beats.Rewards.Count, "The rewards wait for the stars.");
            if (!reduceMotion)
                Assert.Greater(result.Burst.ActiveImages, 0, "H-VFX-073: round bursts and a ring per star.");
            // Doc 05 sheet 20: 200 + 1 050 + 300 per reward, 2 450 ms with 4.
            Run(2.5f);
            Assert.IsTrue(result.Finished);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, beats.Stars, "One beat per star, in order (fa, la, do).");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, beats.Rewards, "One beat per card, in order.");
            Assert.AreEqual(1, beats.Records, "J-SFX-REWARD-001 once.");
            Assert.AreEqual(0, beats.RevealedAll);
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(result.StarVisible(i), "Star " + i + " is shown at rest.");
            for (int i = 0; i < count; i++)
                Assert.IsTrue(result.RewardVisible(i), "Reward " + i + " shows its face.");
            Assert.IsTrue(result.RibbonShown, "« Nouveau record ».");
            Assert.IsFalse(result.BestShown);
            Assert.IsTrue(result.StickerNoteShown, "« Prochain autocollant de la page » under a sticker of a paid game.");
            Assert.AreEqual("+2", result.RewardCell(1).AmountText);
            if (reduceMotion)
                Assert.LessOrEqual(maxStar, 1.0001f, "Reduce Motion: stars only fade in.");
            else
                Assert.Greater(maxStar, 1.15f, "Stars pop to 1.20.");

            result.ReplayButton.onClick.Invoke();
            result.BackButton.onClick.Invoke();
            Assert.AreEqual(1, actions.Replays);
            Assert.AreEqual(1, actions.Backs);
            result.Close();
            Tick(result, 0.4f);
            Assert.IsFalse(result.GetComponent<Canvas>().enabled);
        }

        [UnityTest]
        public IEnumerator Result_QuickReveal_OneTapShowsEverything()
        {
            var result = Spawn<ActivityResultScreen>(ResultPath);
            var beats = new FakeBeats();
            result.SetBeats(beats);
            result.Prewarm(_text);
            var rewards = Rewards(out int count);
            var model = new ActivityResultModel
            {
                Activity = ActivityId.Grooming, Score = 300, Best = 200, Stars = 2, NewRecord = true, Rewarded = true, QuickReveal = false,
            };
            result.Open(model, rewards, count, _text, new FakeResultActions());
            yield return null;
            Tick(result, 0.3f);
            result.CardTap.onClick.Invoke();
            Assert.IsFalse(result.Finished, "The first game of an activity plays its whole reveal.");

            model.QuickReveal = true;
            result.Open(model, rewards, count, _text, new FakeResultActions());
            beats.Clear();
            Tick(result, 0.3f);
            result.CardTap.onClick.Invoke();
            Assert.IsTrue(result.Finished, "From the second game, one tap reveals everything.");
            Assert.AreEqual(1, beats.RevealedAll);
            Assert.AreEqual("300", result.ScoreText);
            Assert.IsTrue(result.StarVisible(0) && result.StarVisible(1));
            Assert.IsFalse(result.StarVisible(2), "Two stars earned, two shown.");
            for (int i = 0; i < count; i++)
                Assert.IsTrue(result.RewardVisible(i));
            Assert.IsTrue(result.RibbonShown);
            int stars = beats.Stars.Count;
            Tick(result, 3f);
            Assert.AreEqual(stars, beats.Stars.Count, "No beat after the reveal.");
            Assert.AreEqual(0, beats.Rewards.Count);
        }

        [UnityTest]
        public IEnumerator Result_BestLine_Practice_AndChallenge()
        {
            var result = Spawn<ActivityResultScreen>(ResultPath);
            result.Prewarm(_text);
            var rewards = Rewards(out int count);
            result.Open(new ActivityResultModel { Activity = ActivityId.Grooming, Score = 120, Best = 400, Stars = 0, Rewarded = true }, rewards, count, _text,
                new FakeResultActions());
            yield return null;
            Tick(result, 3f);
            Assert.AreEqual("Nice try", result.TitleText, "Kind words at 0 stars.");
            Assert.IsTrue(result.BestShown);
            Assert.AreEqual("Best: 400", result.BestText);
            Assert.IsFalse(result.RibbonShown);
            Assert.IsFalse(result.StickerNoteShown, "A game with a free ticket: no rule line.");

            // Practice: stars and score only.
            result.Open(new ActivityResultModel { Activity = ActivityId.Grooming, Mode = ActivityMode.Practice, Score = 410, Stars = 2 }, rewards, count, _text,
                new FakeResultActions());
            Tick(result, 3f);
            Assert.AreEqual(0, result.RewardsShown);
            Assert.IsTrue(result.PracticeNoteShown, "« Partie d'entraînement ».");
            Assert.IsTrue(result.Finished);

            // A challenge: « Défi n », its stars, « Nouvelle étoile ».
            result.Open(new ActivityResultModel
            {
                Activity = ActivityId.Grooming, Mode = ActivityMode.Challenge, ChallengeIndex = 2, Score = 300, Stars = 2, Rewarded = true,
                ChallengeImproved = true,
            }, rewards, count, _text, new FakeResultActions());
            Assert.IsTrue(result.ChallengeShown);
            Tick(result, 3f);
            Assert.IsTrue(result.NewStarShown);
        }

        // ------------------------------------------------------------------------------------------- in-game HUD (030)

        [UnityTest]
        public IEnumerator Hud_RollsTheScore_LightsMarks_SoftRing_StreakAndPause()
        {
            var hud = Spawn<ActivityHudScreen>(HudPath);
            var actions = new FakeHudActions(hud);
            hud.Prewarm(_text);
            Assert.IsFalse(hud.GetComponent<Canvas>().enabled);
            var model = new ActivityHudModel
            {
                Activity = ActivityId.Grooming, Mode = ActivityMode.Free, Duration = 45f, Star1 = 200, Star2 = 380, Star3 = 520, Best = 300,
            };
            hud.Show(model, _text, actions);
            yield return null;
            Canvas.ForceUpdateCanvases();
            hud.Show(model, _text, actions);
            Assert.AreEqual(20, hud.GetComponent<Canvas>().sortingOrder);
            Assert.IsTrue(hud.IsShown);
            Assert.AreEqual("0", hud.ScoreText);
            Assert.IsTrue(hud.TimeShown);
            Assert.AreEqual("45", hud.SecondsText);
            Assert.IsFalse(hud.PracticeShown);
            Assert.Less(hud.MarkX(0), hud.MarkX(1));
            Assert.Less(hud.MarkX(1), hud.MarkX(2));
            Assert.AreEqual(hud.BarWidth * ActivityHudScreen.ThirdStarShare, hud.MarkX(2), 1f, "The third star at 87 % of the bar.");

            hud.SetScore(250);
            hud.Tick(Frame);
            Assert.Less(hud.ScoreShown, 250f, "The score rolls up.");
            Tick(hud, 0.6f);
            Assert.AreEqual("250", hud.ScoreText);
            Assert.IsTrue(hud.MarkLit(0), "First star reached.");
            Assert.IsFalse(hud.MarkLit(1));
            Assert.Greater(hud.FillWidth, 0f);

            hud.SetTimeLeft(22.5f);
            Assert.AreEqual(0.5f, hud.TimeFill, 0.01f, "The ring empties with the time.");
            Assert.AreEqual("23", hud.SecondsText);
            var ring = hud.TimeColor;
            Assert.Greater(ring.g, 0.6f, "A soft ring, never red.");
            hud.SetTimeLeft(3f);
            Assert.AreEqual(ring, hud.TimeColor, "The last seconds keep the same calm color.");

            hud.SetStreak(1);
            Assert.IsFalse(hud.StreakShown);
            hud.SetStreak(3);
            Assert.IsTrue(hud.StreakShown);
            Assert.AreEqual("Streak ×3", hud.StreakText);
            hud.SetStreak(0);
            Assert.IsFalse(hud.StreakShown);

            hud.PauseButton.onClick.Invoke();
            Assert.AreEqual(1, actions.Pauses);
            Assert.IsTrue(hud.PauseCardShown, "The small pause card.");
            Tick(hud, 0.2f);
            hud.ResumeButton.onClick.Invoke();
            Assert.AreEqual(1, actions.Resumes);
            Tick(hud, 0.2f);
            Assert.IsFalse(hud.PauseCardShown);
            hud.PauseButton.onClick.Invoke();
            hud.QuitButton.onClick.Invoke();
            Assert.AreEqual(1, actions.Quits);

            // Practice, no timer.
            hud.Show(new ActivityHudModel { Activity = ActivityId.Grooming, Mode = ActivityMode.Practice, Star1 = 1, Star2 = 2, Star3 = 3 }, _text, actions);
            Assert.IsTrue(hud.PracticeShown, "« Entraînement » when nothing is at stake.");
            Assert.IsFalse(hud.TimeShown, "No timer: no ring.");
            Assert.IsFalse(hud.PauseCardShown);
            hud.Hide();
            Tick(hud, 0.3f);
            Assert.IsFalse(hud.GetComponent<Canvas>().enabled);
        }

        // ------------------------------------------------------------------------------------------- no ticket

        [UnityTest]
        public IEnumerator TicketsEmpty_TimeFirst_ThenPractice_ThenThePack()
        {
            var card = Spawn<TicketsEmptyCard>(EmptyPath);
            var actions = new FakeEmptyActions(card);
            card.Prewarm(_text);
            Assert.IsFalse(TicketsEmptyCard.AnyOpen);
            var tickets = new TicketsModel { Count = 0, Max = 5, MinutesToNext = 72, PackPrice = 30, PackSize = 3, PacksLeftToday = 2, Gems = 100 };
            card.Open(tickets, _text, actions);
            yield return null;
            Assert.AreEqual(31, card.GetComponent<Canvas>().sortingOrder);
            Assert.IsTrue(TicketsEmptyCard.AnyOpen);
            Assert.AreEqual("Tickets on the way", card.TitleText);
            Assert.AreEqual("Next ticket in 1 h 12 min", card.TimeText);
            Assert.IsTrue(card.TimeAboveOptions, "The time first, then the options.");
            Assert.AreEqual("Play for practice", card.PracticeText);
            Assert.AreEqual("3 tickets", card.PackText);
            Assert.AreEqual("30", card.PriceText);
            Assert.IsTrue(card.PackEnabled);
            Assert.AreEqual(string.Empty, card.PackNote);

            card.PracticeButton.onClick.Invoke();
            Assert.AreEqual(1, actions.Practices, "Practice right away, free.");
            card.PackButton.onClick.Invoke();
            Assert.AreEqual(1, actions.Buys);

            tickets.PacksLeftToday = 0;
            card.Refresh(tickets);
            Assert.IsFalse(card.PackEnabled);
            Assert.AreEqual(0.5f, card.PackAlpha, 1e-3f, "Dimmed softly.");
            Assert.AreEqual("More packs tomorrow", card.PackNote);
            card.PackButton.onClick.Invoke();
            Assert.AreEqual(1, actions.Buys, "A dimmed pack does nothing.");
            tickets.PacksLeftToday = 1;
            tickets.Gems = 10;
            card.Refresh(tickets);
            Assert.AreEqual("Not enough Gems yet", card.PackNote);

            card.OutsideButton.onClick.Invoke();
            Assert.AreEqual(1, actions.Closed, "Never blocking: a touch beside closes it.");
            Tick(card, 0.3f);
            Assert.IsFalse(card.GetComponent<Canvas>().enabled);
            Assert.IsFalse(TicketsEmptyCard.AnyOpen);
            card.Open(tickets, _text, actions);
            card.PressBack();
            Assert.AreEqual(2, actions.Closed, "Back closes it too.");
        }

        // ------------------------------------------------------------------------------------------- HUD Play button

        [UnityTest]
        public IEnumerator PlayButton_OnTheRailAboveToday_BadgeBounceAndHint()
        {
            var refuge = SpawnRefugeHud(false, out var fake);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.IsNotNull(refuge.PlayButton, "The Play button is wired (T37 HUD build).");
            Assert.IsFalse(refuge.PlayVisible, "No Play button before refuge level 3 and 5 animals (A15).");

            refuge.SetPlay(true, 5, false);
            refuge.SetToday(true, true);
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(refuge.PlayVisible);
            Assert.AreEqual("5", refuge.PlayCountText, "The tickets owned, in its badge.");
            var play = ScreenRect(refuge.PlayRoot);
            var today = ScreenRect(refuge.TodayRoot);
            Assert.Greater(play.center.y, today.center.y, "Play goes above Today.");
            Assert.AreEqual(play.center.x, today.center.x, 1f, "Same rail.");
            Assert.IsFalse(play.Overlaps(today));
            Assert.GreaterOrEqual(refuge.PlayRoot.rect.width, 124f, "48 dp touch target.");
            Assert.IsTrue(refuge.IsOverUi(refuge.PlayScreenPosition));
            refuge.PlayButton.onClick.Invoke();
            Assert.AreEqual(1, fake.Activities, "The button opens the activities.");

            refuge.SetPlay(true, 2, true);
            Assert.AreEqual("2", refuge.PlayCountText);
            Assert.IsTrue(refuge.PlayDotVisible);
            refuge.PlayPlayAppear();
            Assert.IsTrue(refuge.PlayAppearing);
            Assert.Less(refuge.PlayRoot.localScale.x, 0.2f, "It starts small.");
            float peak = 0f;
            for (int i = 0; i < 40; i++)
            {
                refuge.Tick(Frame);
                peak = Mathf.Max(peak, refuge.PlayRoot.localScale.x);
            }
            Assert.Greater(peak, 1.03f, "A soft overshoot (0.35 s).");
            Assert.Less(peak, 1.2f);
            Assert.IsFalse(refuge.PlayAppearing);
            Assert.AreEqual(1f, refuge.PlayRoot.localScale.x, 1e-4f, "It ends at rest.");

            // A15: the paw on Play (Today's paw goes first).
            refuge.SetTodayHint(false);
            refuge.SetPlayHint(true);
            Tick(refuge, 0.5f);
            Assert.IsTrue(refuge.PlayHintShown);
            Assert.IsTrue(refuge.TapHint.Visible);
            refuge.SetPlayHint(false);
            Tick(refuge, 0.6f);
            Assert.IsFalse(refuge.TapHint.Visible);

            refuge.SetPlay(false, 2, true);
            Assert.IsFalse(refuge.PlayVisible);
            Assert.IsFalse(refuge.PlayDotVisible, "No dot without its button.");
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(play.center.y, ScreenRect(refuge.TodayRoot).center.y, 1f, "Today moves up to the first place of the rail.");
        }

        [UnityTest]
        public IEnumerator RefugeHud_FadesOutAndTakesNoTouch_WhileAGamePlays()
        {
            yield return HudWhilePlaying(false);
            yield return HudWhilePlaying(true);
        }

        private IEnumerator HudWhilePlaying(bool reduceMotion)
        {
            var refuge = SpawnRefugeHud(reduceMotion, out _);
            yield return null;
            refuge.SetPlay(true, 5, false);
            var group = refuge.RootGroup;
            var canvas = refuge.GetComponent<Canvas>();
            Assert.IsNotNull(group, "The HUD's root fades as one.");
            Assert.IsTrue(refuge.IsOverUi(refuge.HatchScreenPosition));

            refuge.SetPlaying(true);
            Assert.IsFalse(group.blocksRaycasts, "No touch for the HUD while a game plays.");
            Assert.IsFalse(group.interactable);
            Assert.IsFalse(refuge.IsOverUi(refuge.HatchScreenPosition), "Touches belong to the game.");
            if (!reduceMotion)
            {
                refuge.Tick(0.05f);
                Assert.Greater(group.alpha, 0f, "A 150 ms fade.");
                Assert.Less(group.alpha, 1f);
            }
            Tick(refuge, 0.2f);
            Assert.AreEqual(0f, group.alpha, 1e-4f);
            Assert.IsFalse(canvas.enabled, "Faded out: nothing left to draw.");

            refuge.SetPlaying(false);
            Assert.IsTrue(canvas.enabled);
            Assert.IsTrue(group.blocksRaycasts);
            Assert.IsTrue(group.interactable);
            Tick(refuge, 0.2f);
            Assert.AreEqual(1f, group.alpha, 1e-4f, "Back when the game ends.");
            Assert.IsTrue(refuge.IsOverUi(refuge.HatchScreenPosition));
            Object.Destroy(refuge.gameObject);
        }

        // ------------------------------------------------------------------------------------------- performance

        [UnityTest]
        public IEnumerator TickAndRefresh_AllocateNothing_AfterWarmup()
        {
            var hub = Spawn<ActivityHubScreen>(HubPath);
            var model = new FakeHub(hub);
            hub.Prewarm(model, _text, model);
            hub.Open(model, _text, model);
            var hud = Spawn<ActivityHudScreen>(HudPath);
            var hudActions = new FakeHudActions(hud);
            hud.Prewarm(_text);
            var result = Spawn<ActivityResultScreen>(ResultPath);
            result.Prewarm(_text);
            var empty = Spawn<TicketsEmptyCard>(EmptyPath);
            var emptyActions = new FakeEmptyActions(empty);
            empty.Prewarm(_text);
            var refuge = SpawnRefugeHud(false, out _);
            yield return null;
            refuge.SetPlay(true, 5, true);
            refuge.SetToday(true, true);

            for (int i = 0; i < 120; i++)
            {
                hub.Tick(Frame);
                hub.Refresh();
                refuge.Tick(Frame);
            }
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                {
                    hub.Tick(Frame);
                    refuge.SetPlay(true, 5, true);
                    refuge.Tick(Frame);
                }
                for (int i = 0; i < 60; i++)
                    hub.Refresh();
            }, Is.Not.AllocatingGCMemory(), "Hub and Play button: steady ticks and refreshes allocate nothing.");

            hub.Close();
            Tick(hub, 0.4f);
            var hudModel = new ActivityHudModel { Activity = ActivityId.Grooming, Duration = 45f, Star1 = 200, Star2 = 380, Star3 = 520, Best = 300 };
            hud.Show(hudModel, _text, hudActions);
            hud.SetScore(250);
            hud.SetStreak(3);
            for (int i = 0; i < 120; i++)
            {
                hud.SetTimeLeft(30.9f - i * 0.005f);
                hud.Tick(Frame);
            }
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                {
                    hud.SetScore(250);
                    hud.SetStreak(3);
                    hud.SetTimeLeft(30.5f - i * 0.002f);
                    hud.Tick(Frame);
                }
            }, Is.Not.AllocatingGCMemory(), "In-game HUD: per-frame setters allocate nothing.");

            var rewards = Rewards(out int count);
            result.Open(new ActivityResultModel { Activity = ActivityId.Grooming, Score = 520, Best = 480, Stars = 3, NewRecord = true, Rewarded = true },
                rewards, count, _text, new FakeResultActions());
            Tick(result, 3f);
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                    result.Tick(Frame);
            }, Is.Not.AllocatingGCMemory(), "Result: ticking after the reveal allocates nothing.");

            var tickets = new TicketsModel { Count = 0, Max = 5, MinutesToNext = 72, PackPrice = 30, PackSize = 3, PacksLeftToday = 2, Gems = 100 };
            empty.Open(tickets, _text, emptyActions);
            for (int i = 0; i < 30; i++)
                empty.Tick(Frame);
            Assert.That(() =>
            {
                for (int i = 0; i < 120; i++)
                {
                    empty.Refresh(tickets);
                    empty.Tick(Frame);
                }
            }, Is.Not.AllocatingGCMemory(), "« No ticket » card: refreshes allocate nothing.");

            result.Close();
            hud.Hide();
            empty.Close();
            Tick(result, 0.4f);
            Tick(hud, 0.4f);
            Tick(empty, 0.4f);
            Assert.That(() =>
            {
                for (int i = 0; i < 240; i++)
                {
                    hub.Tick(Frame);
                    hud.Tick(Frame);
                    result.Tick(Frame);
                    empty.Tick(Frame);
                }
            }, Is.Not.AllocatingGCMemory(), "Closed screens cost nothing.");
        }

        [UnityTest]
        public IEnumerator Prewarm_ThenOpening_CreatesNoObject()
        {
            var hub = Spawn<ActivityHubScreen>(HubPath);
            var model = new FakeHub(hub);
            var hud = Spawn<ActivityHudScreen>(HudPath);
            var result = Spawn<ActivityResultScreen>(ResultPath);
            var empty = Spawn<TicketsEmptyCard>(EmptyPath);
            hub.Prewarm(model, _text, model);
            hud.Prewarm(_text);
            result.Prewarm(_text);
            empty.Prewarm(_text);
            yield return null;
            int objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            hub.Open(model, _text, model);
            hub.ScrollTo(17);
            Tick(hub, 0.3f);
            hub.Cell(1).Button.onClick.Invoke();
            hub.PracticeButton.onClick.Invoke();
            hud.Show(new ActivityHudModel { Activity = ActivityId.Grooming, Duration = 45f, Star1 = 200, Star2 = 380, Star3 = 520 }, _text,
                new FakeHudActions(hud));
            hud.SetScore(600);
            hud.SetStreak(4);
            hud.SetPaused(true);
            Tick(hud, 1f);
            var rewards = Rewards(out int count);
            result.Open(new ActivityResultModel { Activity = ActivityId.Grooming, Score = 520, Best = 480, Stars = 3, NewRecord = true, Rewarded = true },
                rewards, count, _text, new FakeResultActions());
            Tick(result, 3f);
            empty.Open(new TicketsModel { Count = 0, Max = 5, MinutesToNext = 30, PackPrice = 30, PackSize = 3, PacksLeftToday = 0, Gems = 5 }, _text,
                new FakeEmptyActions(empty));
            Tick(empty, 0.3f);
            yield return null;
            Assert.AreEqual(objects, Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                "Cards, cells, bursts and rewards are pooled: nothing instantiated after the prewarm.");
        }

        // ------------------------------------------------------------------------------------------- helpers

        private T Spawn<T>(string path) where T : Component
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assume.That(prefab != null, "Run Pocket Wilds/UI/Build Activities (T37) first: " + path);
            var go = Object.Instantiate(prefab);
            _spawned.Add(go);
            return go.GetComponent<T>();
#else
            Assert.Ignore("Editor only.");
            return null;
#endif
        }

        private RefugeHud SpawnRefugeHud(bool reduceMotion, out FakeRefuge fake)
        {
            var hud = Spawn<RefugeHud>(RefugeHudPath);
            fake = new FakeRefuge();
            hud.ReduceMotion = reduceMotion;
            hud.Bind(fake);
            hud.BindDaily(fake);
            hud.BindPlay(fake);
            return hud;
        }

        private static RewardCell[] Rewards(out int count)
        {
            var cells = new RewardCell[RewardCells.Max];
            cells[0] = new RewardCell(RewardKind.Light, 0) { LightMinutes = 20 };
            cells[1] = new RewardCell(RewardKind.Gems, 2);
            cells[2] = new RewardCell(RewardKind.Bond, 6);
            cells[3] = new RewardCell(RewardKind.Sticker, 1);
            count = 4;
            return cells;
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void Tick(ActivityHubScreen screen, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                screen.Tick(Frame);
        }

        private static void Tick(ActivityHudScreen screen, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                screen.Tick(Frame);
        }

        private static void Tick(ActivityResultScreen screen, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                screen.Tick(Frame);
        }

        private static void Tick(TicketsEmptyCard screen, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                screen.Tick(Frame);
        }

        private static void Tick(RefugeHud hud, float seconds)
        {
            for (float t = 0f; t < seconds; t += Frame)
                hud.Tick(Frame);
        }

        private sealed class FakeText : ILocalization
        {
            private readonly Dictionary<string, string> _texts = new Dictionary<string, string>
            {
                { ActivityText.HubTitle, "Activities" },
                { ActivityText.HubPlay, "Play" },
                { ActivityText.HubFree, "Free" },
                { ActivityText.HubNew, "New" },
                { ActivityText.HubBest, "Best {n}" },
                { ActivityText.HubChallenges, "Challenges" },
                { ActivityText.HubChallengePick, "Tap a challenge to pick it" },
                { ActivityText.HubChallengeGoal, "Challenge {n}: {goal}" },
                { ActivityText.HubChallengeLocked, "A star on challenge {n} opens this one" },
                { ActivityText.HubTheme, "Theme {n}" },
                { ActivityText.Challenge, "Challenge {n}" },
                { ActivityText.TicketsNextMinutes, "Next ticket in {n} min" },
                { ActivityText.TicketsNextHours, "Next ticket in {h} h" },
                { ActivityText.TicketsNextHoursMinutes, "Next ticket in {h} h {m} min" },
                { ActivityText.TicketsFull, "All your tickets are ready" },
                { ActivityText.Practice, "Practice" },
                { ActivityText.PracticeLine, "Free, no rewards" },
                { ActivityText.HudStreak, "Streak ×{n}" },
                { ActivityText.PauseTitle, "Paused" },
                { ActivityText.PauseResume, "Resume" },
                { ActivityText.PauseQuit, "Quit" },
                { ActivityText.ResultTitleNone, "Nice try" },
                { ActivityText.ResultTitleOne, "Well played" },
                { ActivityText.ResultTitleTwo, "Great job" },
                { ActivityText.ResultTitleThree, "Perfect!" },
                { ActivityText.ResultRecord, "New record" },
                { ActivityText.ResultBest, "Best: {n}" },
                { ActivityText.ResultReplay, "Play again" },
                { ActivityText.ResultBack, "Back" },
                { ActivityText.ResultNextSticker, "Next sticker on the page" },
                { ActivityText.ResultPractice, "Practice game" },
                { ActivityText.ResultNewStar, "New star" },
                { ActivityText.EmptyTitle, "Tickets on the way" },
                { ActivityText.EmptyPractice, "Play for practice" },
                { ActivityText.EmptyPack, "{n} tickets" },
                { ActivityText.EmptyPack + ".one", "{n} ticket" },
                { ActivityText.EmptyPack + ".other", "{n} tickets" },
                { ActivityText.EmptyPacksUsed, "More packs tomorrow" },
                { ActivityText.EmptyNeedGems, "Not enough Gems yet" },
                { "activity.grooming.name", "Grooming" },
                { "biome.grassland.name", "Meadow" },
                { "biome.forest.name", "Forest" },
                { DailyVisitText.RewardLightMinutes, "{n} min" },
            };

            public string Language => "en";
            public string Get(string key) => _texts.TryGetValue(key, out var text) ? text : key;
        }

        private sealed class FakeHub : IActivityHubModel, IActivityHubActions
        {
            public readonly ActivityCardModel[] Cards = new ActivityCardModel[18];
            private readonly ChallengeCellModel[] _challenges = new ChallengeCellModel[10];
            private readonly string[] _goals = new string[10];
            private readonly ActivityHubScreen _view;
            private TicketsModel _tickets = new TicketsModel { Count = 3, Max = 5, MinutesToNext = 72, PackPrice = 30, PackSize = 3, PacksLeftToday = 2 };
            public int Closes;
            public int Plays;
            public int Buys;
            public int PracticeChanges;
            public int PlayedChallenge = -1;

            public FakeHub(ActivityHubScreen view)
            {
                _view = view;
                for (int i = 0; i < Cards.Length; i++)
                {
                    var id = (ActivityId)(i + 1);
                    bool locked = i >= 6;
                    Cards[i] = new ActivityCardModel
                    {
                        Activity = id,
                        State = locked ? ActivityCardState.Locked : i == 1 ? ActivityCardState.New : ActivityCardState.Open,
                        Name = i == 0 ? "Grooming" : "Activity " + (i + 1),
                        UnlockHint = locked ? "Refuge level " + (i + 2) : null,
                        Best = i == 0 ? 380 : i >= 2 && !locked ? 100 * i : 0,
                        BestStars = (byte)(i == 0 ? 2 : i >= 2 && !locked ? 1 : 0),
                        ChallengesDone = i == 0 ? 3 : 0,
                        FreeGame = i == 0,
                        Featured = i == 2,
                    };
                }
                for (int i = 0; i < _challenges.Length; i++)
                {
                    _goals[i] = "Goal " + (i + 1);
                    _challenges[i] = new ChallengeCellModel
                    {
                        Index = i, Stars = (byte)(i < 3 ? 3 - i : 0), Locked = i >= 4, Goal = _goals[i],
                    };
                }
                _challenges[0].Stars = 3;
            }

            public int CardCount => Cards.Length;
            public ActivityCardModel Card(int index) => Cards[index];
            public int SelectedCard { get; private set; }
            public TicketsModel Tickets => _tickets;
            public bool Practice { get; private set; }
            public int ChallengeCount => 10;
            public ChallengeCellModel Challenge(int index) => _challenges[index];
            public int ThemeCount { get; set; } = 2;
            public ContentId Theme(int index) => ContentId.FromSource(index == 0 ? "biome.grassland" : "biome.forest");
            public int SelectedTheme { get; private set; }

            public void SetTickets(int count, int max, int minutes)
            {
                _tickets.Count = count;
                _tickets.Max = max;
                _tickets.MinutesToNext = minutes;
                _view.Refresh();
            }

            public void SelectActivity(int card)
            {
                SelectedCard = card;
                _view.Refresh();
            }

            public void Play() => Plays++;
            public void PlayChallenge(int challenge) => PlayedChallenge = challenge;

            public void SetPractice(bool practice)
            {
                PracticeChanges++;
                Practice = practice;
                _view.Refresh();
            }

            public void SetTheme(int theme)
            {
                SelectedTheme = theme;
                _view.Refresh();
            }

            public void BuyTickets() => Buys++;

            public void CloseActivities()
            {
                Closes++;
                _view.Close();
            }
        }

        private sealed class FakeBeats : IActivityResultBeats
        {
            public readonly List<int> Stars = new List<int>(8);
            public readonly List<int> Rewards = new List<int>(8);
            public int Records;
            public int RevealedAll;
            public int Count => Stars.Count + Rewards.Count + Records + RevealedAll;

            public void ResultStarLanded(int star) => Stars.Add(star);
            public void ResultRewardShown(int reward) => Rewards.Add(reward);
            public void ResultRecordShown() => Records++;
            public void ResultRevealedAll() => RevealedAll++;

            public void Clear()
            {
                Stars.Clear();
                Rewards.Clear();
                Records = 0;
                RevealedAll = 0;
            }
        }

        private sealed class FakeResultActions : IActivityResultActions
        {
            public int Replays;
            public int Backs;
            public void PlayAgain() => Replays++;
            public void BackToHub() => Backs++;
        }

        private sealed class FakeHudActions : IActivityHudActions
        {
            private readonly ActivityHudScreen _view;
            public int Pauses;
            public int Resumes;
            public int Quits;

            public FakeHudActions(ActivityHudScreen view) => _view = view;

            public void PauseActivity()
            {
                Pauses++;
                _view.SetPaused(true);
            }

            public void ResumeActivity()
            {
                Resumes++;
                _view.SetPaused(false);
            }

            public void QuitActivity() => Quits++;
        }

        private sealed class FakeEmptyActions : ITicketsEmptyActions
        {
            private readonly TicketsEmptyCard _view;
            public int Buys;
            public int Practices;
            public int Closed;

            public FakeEmptyActions(TicketsEmptyCard view) => _view = view;

            public void BuyTickets() => Buys++;
            public void PlayPractice() => Practices++;

            public void TicketsEmptyClosed()
            {
                Closed++;
                _view.Close();
            }
        }

        private sealed class FakeRefuge : IRefugeActions, IDailyHudActions, IPlayHudActions
        {
            public int Activities;

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

            public void OpenToday()
            {
            }

            public void OpenActivities() => Activities++;
        }
    }
}
