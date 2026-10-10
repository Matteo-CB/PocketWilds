using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T41 « Memory des empreintes » in the real game (doc « Mini-jeux &amp; activités » section 6: « un test PlayMode joue
    /// une partie complète »; doc 01 Contenu S-ACT-004). The first game with its tutorial: the screen loads from its
    /// Addressables group under the activity HUD and the refuge stops rendering; the player's own variants are dealt first
    /// (the favorite's first); the portraits are the codex's own thumbnails, borrowed (never in the screen's bundle); the
    /// preview turns every card up and back; the tapping paw with « Trouve les paires » until the first match; taps through
    /// the stage's hit test play a deliberate mismatch (a first try: never a mistake), then a real mistake (the first card and
    /// its pair both seen before), then every pair; no allocation per frame in play (flips, a match, its glints and heart);
    /// the score is the pairs plus the end bonus, the stars come from the grid's thresholds, the Seed Cookie at 3 stars;
    /// back to the refuge, the screen released with the hub.
    /// </summary>
    public class MemoryPlayTests
    {
        private static readonly ContentId SeedCookie = ContentId.FromSource("treat.seed_cookie");

        private static readonly (string species, string variant, bool favorite)[] Animals =
        {
            ("species.rabbit", "variant.rabbit.brown", false), ("species.hedgehog", "variant.hedgehog.brown", false),
            ("species.squirrel", "variant.squirrel.red", true), ("species.rabbit", "variant.rabbit.white", false),
            ("species.squirrel", "variant.squirrel.grey", false),
        };

        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            try
            {
                Directory.Delete(_saveDir, true);
            }
            catch (IOException)
            {
            }
        }

        [UnityTest]
        public IEnumerator Memory_PlaysAWholeGame_OnItsScreen()
        {
#if UNITY_EDITOR
            SeedAnimals();
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var services = context.Activities;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var presenter = entry.Activities;
            Assert.IsNotNull(presenter, "The scene runs the activities.");
            if (presenter.Stages is ActivityStageRegistry registry && !registry.Has(ActivityId.PawMemory))
                MemoryStage.Register(registry);
            Assert.IsTrue(presenter.Stages.Has(ActivityId.PawMemory), "Pawprint Memory's stage is registered.");

            // Doc 01: Pawprint Memory opens at refuge level 11 and day 5.
            services.Activities.Records[ActivityCatalog.SlotOf(ActivityId.PawMemory)].Unlocked = true;
            services.RefreshUnlocks();
            yield return ClearScreens(entry);
            yield return WaitFor(() => presenter.PlayAvailable, 3f, "The Play button shows once Pawprint Memory is open.");
            presenter.OpenActivities();
            yield return null;
            Assert.IsTrue(presenter.HubOpen);
            presenter.StartGame(ActivityRequest.Free(ActivityId.PawMemory));
            yield return WaitFor(() => presenter.Playing, 5f, "The screen loads in a moment (doc Mini-jeux section 1: under a second on device).");

            var stage = presenter.Stages.Acquire(ActivityId.PawMemory, null) as MemoryStage;
            Assert.IsNotNull(stage, "The memory stage plays the game.");
            stage.PollInput = false;
            var controller = (MemoryController)stage.Controller;
            var view = stage.View as MemoryScreenView;
            Assert.IsTrue(stage.ScreenLoaded, "The screen comes from its Addressables group (Activity_Memory).");
            Assert.IsNotNull(view, "PF_UI_Memory carries its MemoryScreenView.");
            Assert.IsTrue(view.Valid && view.Shown, "The screen shows.");
            Assert.IsTrue(stage.Shown);
            var hud = Object.FindAnyObjectByType<ActivityHudScreen>();
            var hudCanvas = hud != null ? hud.GetComponentInParent<Canvas>(true).rootCanvas : null;
            if (hudCanvas != null)
                Assert.Less(view.Canvas.sortingOrder, hudCanvas.sortingOrder, "The HUD (score, time, pause, tips) stays on top of the screen.");
            Assert.IsNull(view.GetComponent<UnityEngine.UI.GraphicRaycaster>(), "No raycaster on the screen: the cards are hit tested, the HUD keeps its touches.");
            var refugeCamera = entry.CameraRig.Camera;
            Assert.IsFalse(refugeCamera.enabled, "The refuge does not render while the screen shows.");

            // The player's own variants first, the favorite's first (doc 01 S-ACT-004: « un animal du joueur »).
            Assert.GreaterOrEqual(stage.VariantCount, controller.PairCount, "Enough variants for the pairs.");
            Assert.AreEqual(ContentId.FromSource("variant.squirrel.red").Value, stage.VariantAt(0), "The favorite's variant comes first.");
            for (int i = 0; i < Animals.Length; i++)
            {
                uint variant = ContentId.FromSource(Animals[i].variant).Value;
                int at = -1;
                for (int k = 0; k < stage.VariantCount; k++)
                    if (stage.VariantAt(k) == variant)
                        at = k;
                Assert.That(at, Is.InRange(0, Animals.Length - 1), Animals[i].variant + " is among the first variants given.");
            }

            // The grid: every card dealt, in the safe area under the HUD's top card.
            Assert.AreEqual(controller.Columns * controller.Rows, controller.CardCount);
            for (int card = 0; card < controller.CardCount; card++)
            {
                Assert.IsTrue(view.Card(card).root.gameObject.activeInHierarchy, $"Card {card} shows.");
                var rect = view.CardRectInSafeArea(card);
                Assert.LessOrEqual(rect.yMax, view.Grid.parent is RectTransform safe ? safe.rect.height - MemoryLayout.GridTop + 1f : float.MaxValue,
                    $"Card {card} stays under the HUD's top card.");
                Assert.AreEqual(MemoryLayout.CardAspect, rect.width / rect.height, 0.02f, "Cards of about 3:4.");
            }

            // The portraits are the codex's thumbnails, borrowed from the catalog the scene holds (never copied).
            var catalog = view.Portraits;
            Assert.IsNotNull(catalog, "The codex catalog (DEF_CodexVisuals) is found loaded.");
            int portraits = 0;
            for (int card = 0; card < controller.CardCount; card++)
            {
                if (controller.FaceOf(card) != MemoryFace.Portrait)
                {
                    Assert.Greater(view.PawTint(card).a, 0.5f, "A pawprint card shows its paw in its coat colour.");
                    continue;
                }
                Assert.IsTrue(catalog.TryGetVariant(new ContentId(controller.VariantOf(card)), out var thumbnail), "Every dealt variant has its thumbnail.");
                Assert.AreSame(thumbnail, view.PortraitShown(card), "The card shows the codex's own sprite.");
                portraits++;
            }
            Assert.Greater(portraits, 0);
            foreach (string dependency in UnityEditor.AssetDatabase.GetDependencies(MemoryLayout.PrefabPath, true))
            {
                StringAssert.DoesNotContain("/Thumbnails/", dependency, "No portrait in the screen's bundle.");
                StringAssert.DoesNotContain("DEF_CodexVisuals", dependency, "The codex catalog is borrowed, not referenced.");
                StringAssert.DoesNotContain("Atlas_CommonHUD", dependency, "Nothing of the scene's HUD atlas.");
                StringAssert.DoesNotContain("/Fonts/", dependency, "No font (the screen has no label).");
            }

            // The preview (d1 and d2: 2 s): every card turns up, then back.
            if (controller.PreviewSeconds > 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                if (controller.Previewing)
                    for (int card = 0; card < controller.CardCount; card++)
                        Assert.IsTrue(view.FaceShown(card), $"The preview shows card {card}'s face.");
                yield return WaitFor(() => !controller.Previewing, 4f, "The preview ends.");
                yield return new WaitForSeconds(0.5f);
                for (int card = 0; card < controller.CardCount; card++)
                    Assert.IsFalse(view.FaceShown(card), $"Card {card} turned back after the preview.");
            }

            // The first game: a paw taps a card and then its pair, « Trouve les paires », until the first match.
            yield return WaitFor(() => stage.TutorialStep == 2, 4f, "The first game shows its paw after the preview.");
            yield return null;
            int pawCard = stage.TutorialTarget;
            Assert.That(pawCard, Is.InRange(0, controller.CardCount - 1));
            if (hud != null)
            {
                Assert.IsTrue(hud.GestureShown, "The tapping paw shows (UI_Gesture_Tap).");
                Assert.AreEqual(ActivityGesture.Tap, hud.GestureKind);
                Assert.IsTrue(hud.TipShown, "The pairs tip shows.");
                Assert.AreEqual(context.Localization.Get(ActivityText.MemoryTipPairs), hud.TipText);
            }
            int pawPair = controller.PartnerOf(pawCard);
            yield return WaitFor(() => stage.TutorialTarget == pawPair, 3f, "The paw taps the card, then its pair.");

            // A deliberate mismatch: a first try at both cards is never a mistake; they shake, then turn back.
            int a = FaceDown(controller, -1, -1);
            int c = NotWith(controller, a, -1);
            yield return TapCard(stage, controller, a);
            Assert.IsTrue(view.Animating(a) || view.FaceShown(a), "The tapped card turns.");
            yield return new WaitForSeconds(0.15f);
            Assert.IsTrue(view.FaceShown(a), "Half way through its flip the card shows its face.");
            yield return TapCard(stage, controller, c);
            Assert.AreEqual(1, stage.Mismatches, "A mismatch.");
            Assert.AreEqual(0, controller.Mistakes, "Doc 01: a first try is never a mistake.");
            Assert.AreEqual(0, stage.MistakesSeen);
            yield return WaitFor(() => !controller.Busy, 2f, "The cards turn back after 0.8 s.");
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(view.FaceShown(a) || view.FaceShown(c), "Both cards turned face down again.");

            // A mistake: a's pair turned up (a first try), then a again with another card: a and its pair were both seen.
            int b = controller.PartnerOf(a);
            int d = NotWith(controller, b, a);
            yield return TapCard(stage, controller, b);
            yield return TapCard(stage, controller, d);
            Assert.AreEqual(0, controller.Mistakes, "b's first try is not a mistake either.");
            yield return WaitFor(() => !controller.Busy, 2f, "The cards turn back.");
            int e = NotWith(controller, a, b);
            yield return TapCard(stage, controller, a);
            yield return TapCard(stage, controller, e);
            Assert.AreEqual(1, controller.Mistakes, "Missing a pair whose two cards were both seen is a mistake.");
            Assert.AreEqual(1, stage.MistakesSeen, "The stage saw the mismatch as a mistake.");
            Assert.AreEqual(3, stage.Mismatches);
            yield return WaitFor(() => !controller.Busy, 2f, "The cards turn back.");

            // The first match: the two cards stay up with their mark, a glint on each and a heart; the paw and the tip go.
            yield return TapCard(stage, controller, a);
            yield return TapCard(stage, controller, b);
            Assert.AreEqual(1, stage.Matches, "a and its pair match.");
            Assert.IsTrue(controller.Matched(a) && controller.Matched(b));
            Assert.AreEqual(3, stage.TutorialStep, "The first match ends the paw.");
            if (hud != null)
            {
                Assert.IsFalse(hud.GestureShown, "The paw went with the first match.");
                Assert.IsFalse(hud.TipShown, "The tip went with it.");
            }
            yield return null;
            if (view.GlintSprite != null)
                Assert.AreEqual(2, view.GlintsShown, "Two round glints (H-VFX-ACT-004-01).");
            if (view.HeartSprite != null)
                Assert.AreEqual(1, view.HeartsShown, "A little heart.");
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(view.FaceShown(a) && view.FaceShown(b), "Matched cards stay face up.");
            if (view.MarkSprite != null)
                Assert.IsTrue(view.MarkShown(a) && view.MarkShown(b), "With their soft mark.");

            // No allocation per frame in play: taps, flips, a match with its glints and heart, after the warm-up.
            int warm = NextPairCard(controller);
            yield return TapCard(stage, controller, warm);
            yield return TapCard(stage, controller, controller.PartnerOf(warm));
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(controller.PairCount - controller.PairsFound, 2, "Pairs are left for the warm-up and the measured frames.");
            // The frames as the game runs them: the game, the stage (touches, cards, effects) and the canvases' rebuild.
            PlayPairFrames(stage, controller, view, 120);
            int pairsBefore = controller.PairsFound;
            long before = GC.GetAllocatedBytesForCurrentThread();
            PlayPairFrames(stage, controller, view, 240);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(pairsBefore + 1, controller.PairsFound, "The measured frames hold a match.");
            Assert.AreEqual(0, allocated, "The memory allocates nothing per frame in play.");

            // Every pair, taps through the hit test; the last pair ends the game with the end bonus.
            int guard = 0;
            while (presenter.Playing && guard++ < 40)
            {
                int card = NextPairCard(controller);
                if (card < 0)
                    break;
                yield return TapCard(stage, controller, card);
                yield return TapCard(stage, controller, controller.PartnerOf(card));
            }
            yield return WaitFor(() => !presenter.Playing, 3f, "The last pair ends the game.");
            Assert.IsTrue(controller.AllMatched, "Every pair found.");
            Assert.AreEqual(controller.PairCount, stage.Matches, "One match effect per pair.");
            yield return WaitFor(ResultOpen, 3f, "The result card shows over the screen.");
            Assert.IsTrue(stage.Shown && view.Shown, "The screen stays under the result card.");
            Assert.IsFalse(refugeCamera.enabled);

            // The score: 100 a pair (200 the double pair) and the end bonus; the stars from the grid's thresholds.
            int doubles = 0;
            for (int card = 0; card < controller.CardCount; card++)
                if (controller.IsDouble(card))
                    doubles++;
            int pairPoints = controller.PairCount * MemoryRules.PairPoints + doubles / 2 * (MemoryRules.DoublePairPoints - MemoryRules.PairPoints);
            Assert.AreEqual(pairPoints + controller.EndBonus, controller.Score, "Pairs plus the end bonus.");
            Assert.AreEqual(MemoryRules.EndBonus(controller.PairCount, controller.Mistakes, controller.MistakePenalty, controller.SecondsLeft),
                controller.EndBonus, "50 a pair, 20 a mistake, 3 a second left.");
            var result = services.Runner.LastResult;
            Assert.AreEqual(controller.Score, result.Score);
            var thresholds = MemoryRules.FreeOf(controller.Grid);
            int stars = result.Score >= thresholds.Three ? 3 : result.Score >= thresholds.Two ? 2 : result.Score >= thresholds.One ? 1 : 0;
            Assert.AreEqual(stars, result.Stars, "Stars from the grid's free-mode thresholds.");
            Assert.GreaterOrEqual(result.Stars, 2, "A whole game with one mistake earns at least 2 stars.");
            Assert.IsTrue(result.Rewarded, "A ticket game is rewarded.");
            if (result.Stars == 3)
            {
                Assert.AreEqual(SeedCookie, result.Rewards.ItemTreat, "Doc 01: the Seed Cookie at 3 stars.");
                Assert.AreEqual(1, result.Rewards.ItemTreats);
            }
            else
            {
                Assert.AreEqual(0, result.Rewards.ItemTreats, "No Seed Cookie below 3 stars.");
            }
            Debug.Log($"[MemoryPlayTests] {controller.Columns} × {controller.Rows}, score {result.Score} (bonus {controller.EndBonus}), {result.Stars} stars, "
                      + $"mistakes {controller.Mistakes}, moves {controller.Moves}, {controller.SecondsLeft:F1} s left");

            // Back to the hub: the refuge renders again; closing the hub releases the screen.
            presenter.BackToHub();
            yield return null;
            Assert.IsTrue(refugeCamera.enabled, "The refuge is back.");
            Assert.IsFalse(stage.Shown);
            Assert.IsFalse(view.Shown, "The screen hides.");
            presenter.CloseActivities();
            yield return null;
            Assert.IsFalse(stage.ScreenLoaded, "Leaving the activities releases Activity_Memory.");
            Assert.IsNull(stage.View);
#else
            yield break;
#endif
        }

        /// <summary>A face-down, unmatched card other than the two given (and not the glow card: its gold pulse is its own test).</summary>
        private static int FaceDown(MemoryController game, int notA, int notB)
        {
            for (int card = 0; card < game.CardCount; card++)
                if (card != notA && card != notB && !game.FaceUp(card) && !game.Matched(card) && card != game.GlowCard)
                    return card;
            return -1;
        }

        /// <summary>A face-down card that does not go with <paramref name="first"/> (and is not <paramref name="not"/>).</summary>
        private static int NotWith(MemoryController game, int first, int not)
        {
            for (int card = game.CardCount - 1; card >= 0; card--)
                if (card != first && card != not && !game.FaceUp(card) && !game.Matched(card) && !game.GoTogether(first, card) && card != game.GlowCard)
                    return card;
            return -1;
        }

        /// <summary>Frames played by hand: a tap on the next pair's first card, its partner half a second later (no allocation).</summary>
        private static void PlayPairFrames(MemoryStage stage, MemoryController game, MemoryScreenView view, int frames)
        {
            int next = NextPairCard(game);
            int partner = game.PartnerOf(next);
            var nextPoint = view.ScreenPoint(next);
            var partnerPoint = view.ScreenPoint(partner);
            for (int frame = 0; frame < frames; frame++)
            {
                if (frame == 10)
                    stage.PointerDown(nextPoint);
                if (frame == 40)
                    stage.PointerDown(partnerPoint);
                game.Tick(1f / 60f);
                stage.Tick(1f / 60f, 1f / 60f);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static int NextPairCard(MemoryController game)
        {
            for (int card = 0; card < game.CardCount; card++)
                if (!game.Matched(card))
                    return card;
            return -1;
        }

        /// <summary>Waits until the game takes a touch, then taps the card through its screen point (the stage's hit test).</summary>
        private static IEnumerator TapCard(MemoryStage stage, MemoryController game, int card)
        {
            Assert.That(card, Is.InRange(0, game.CardCount - 1), "A card to tap.");
            float until = Time.realtimeSinceStartup + 3f;
            while ((game.Previewing || game.Busy) && Time.realtimeSinceStartup < until)
                yield return null;
            int moves = game.Moves;
            Assert.AreEqual(card, stage.View.HitTest(stage.View.ScreenPoint(card)), "The hit test finds the card under its centre.");
            stage.Tap(card);
            Assert.AreEqual(moves + 1, game.Moves, $"The tap turned card {card}.");
            yield return null;
        }

        private static bool ResultOpen()
        {
            var result = Object.FindAnyObjectByType<ActivityResultScreen>();
            return result != null && result.IsOpen;
        }

        /// <summary>Closes what a boot may open over the refuge: the offline return card, a refuge level's reward card, Today.</summary>
        private static IEnumerator ClearScreens(VerticalSliceEntry entry)
        {
            var refuge = entry.Refuge;
            float until = Time.realtimeSinceStartup + 8f;
            bool collected = false;
            while (refuge.ModalOpen && Time.realtimeSinceStartup < until)
            {
                var offline = Object.FindAnyObjectByType<OfflineReturnScreen>();
                if (refuge.OfflineScreenOpen && !collected && offline != null && offline.IsOpen)
                {
                    yield return new WaitForSecondsRealtime(0.35f);
                    offline.GetComponentInChildren<UnityEngine.UI.Button>(true).onClick.Invoke();
                    collected = true;
                }
                var progression = entry.ProgressionScreens;
                if (progression != null && progression.RewardWaiting && !progression.IsOpen)
                    progression.ShowPendingRewards();
                var card = Object.FindAnyObjectByType<LevelRewardScreen>();
                if (card != null && card.IsOpen)
                    card.ClaimButton.onClick.Invoke();
                if (entry.DailyVisit != null && entry.DailyVisit.ScreenOpen)
                    entry.DailyVisit.CloseToday();
                yield return null;
            }
        }

        /// <summary>Five animals of the three Prairie species, the red squirrel the favorite.</summary>
        private void SeedAnimals()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            for (int i = 0; i < Animals.Length; i++)
            {
                var species = ContentId.FromSource(Animals[i].species);
                var variant = ContentId.FromSource(Animals[i].variant);
                state.Collection.Add(new AnimalInstance
                {
                    Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = SeedHash.Mix((uint)i + 1u, 0x4D454D4Fu),
                    Flags = Animals[i].favorite ? AnimalFlags.Favorite : AnimalFlags.None,
                });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private IEnumerator Boot(Action<GameBootstrap> ready)
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LoadTargetScene = true,
                ClockOverride = new FakeGameClock(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero)),
            };
#endif
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 60f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            ready(bootstrap);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string message)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until)
                    Assert.Fail(message);
                yield return null;
            }
        }
    }
}
