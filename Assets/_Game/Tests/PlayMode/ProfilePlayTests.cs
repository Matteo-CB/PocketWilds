using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T44B « Succès, titres, cadres, poses » in the real scene (doc « 01 Succès, titres, cadres, poses et journal », sections
    /// 1, 3, 4, 5): the refuge HUD shows the Succès button with its dot while a tier waits; it opens PF_UI_Achievements from
    /// its Addressables group over a hidden refuge with the borrowed fonts and nothing of the scene in its bundle; the 200
    /// badges live in 12 recycled cells (scrolling binds other achievements, nothing is instantiated); a claim takes every
    /// reached tier and the dot goes; the tabs switch; an owned title and a frame are selected (the card follows), a locked
    /// one shows its source, « Aucun titre » clears it; a pose says « Mode photo bientôt »; « Nouveau » counts as seen when
    /// leaving its tab; nothing allocates per frame while the screen shows; closing releases the screen.
    /// </summary>
    public class ProfilePlayTests
    {
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
        public IEnumerator Profile_OpenSwitchClaimAndSelect_OnTheirScreen()
        {
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            Assert.IsNotNull(entry);
            yield return ClearScreens(entry);
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            Assert.IsNotNull(hud, "The refuge HUD is in the scene.");
            Assert.IsNotNull(hud.ProfileButton, "The HUD carries the Succès button (rebuild the HUD with UiPipeline).");

            var achievements = new ScriptedAchievements();
            var items = new ScriptedItems();
            // The scene's own presenter (when it is wired) would drive the same HUD button: the test takes over.
            entry.GetType().GetMethod("DetachProfile")?.Invoke(entry, null);
            var screen = ProfileScreenPresenter.Create(context, achievements, items, entry.Activities, hud);
            try
            {
                Frame(screen);
                Assert.IsTrue(hud.ProfileVisible, "The Succès button shows from the start (every achievement is visible).");
                Assert.IsTrue(hud.ProfileDotVisible, "A tier waits to be claimed: the dot.");

                // The button opens the screen from its Addressables group over a hidden refuge.
                hud.ProfileButton.onClick.Invoke();
                Assert.IsTrue(screen.IsOpen);
                yield return WaitFor(screen, () => screen.View != null && screen.ScreenShown, 5f, "The profile screen loads in a moment.");
                var view = (ProfileScreenView)screen.View;
                Assert.IsTrue(screen.ScreenLoaded, "The screen comes from its Addressables group (Profile).");
                Assert.IsTrue(view.Valid);
                Assert.AreEqual(ProfileLayout.SortingOrder, view.Canvas.sortingOrder);
                Assert.AreEqual(ProfileTab.Achievements, screen.Tab);
                Assert.IsFalse(entry.CameraRig.Camera.enabled, "The refuge does not render under the screen.");
#if UNITY_EDITOR
                foreach (string dependency in UnityEditor.AssetDatabase.GetDependencies(ProfileLayout.PrefabPath, true))
                {
                    StringAssert.DoesNotContain("/Fonts/", dependency, "No font in the screen's bundle (borrowed from the scene).");
                    StringAssert.DoesNotContain("/Art/UI/Kit/", dependency, "No kit sprite in the screen's bundle.");
                    StringAssert.DoesNotContain("/Art/UI/Icons/", dependency, "No icon of the scene's atlas in the screen's bundle.");
                }
#endif
                foreach (var label in view.BorrowedLabels)
                    Assert.IsNotNull(label.font, label.name + " got its font from the scene.");

                // 200 badges in 12 recycled cells: scrolling binds other achievements, nothing is instantiated.
                Assert.AreEqual(ScriptedAchievements.Total, screen.FilteredCount);
                Assert.AreEqual(ProfileLayout.BadgeCells, view.PoolSize(ProfileList.Badges));
                Assert.AreEqual(ProfileLayout.BadgeCells, view.Parts.badgeContent.childCount, "Never more cells than the pool.");
                Assert.GreaterOrEqual(view.CellOf(ProfileList.Badges, 0), 0, "The first badge is on screen.");
                var content = view.Parts.badgeContent;
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 50f * ProfileScreenView.BadgePitchY);
                Frame(screen);
                Assert.GreaterOrEqual(view.CellOf(ProfileList.Badges, 100), 0, "Scrolled down: the 101st badge is bound to a pooled cell.");
                Assert.Less(view.CellOf(ProfileList.Badges, 0), 0, "The first badge's cell was recycled.");
                Assert.AreEqual(ProfileLayout.BadgeCells, view.Parts.badgeContent.childCount);
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
                Frame(screen);

                // A category filters the grid; « Tout » brings every badge back.
                screen.SelectCategory(1 + (int)AchievementCategory.Biomes);
                Assert.AreEqual(ScriptedAchievements.PerCategory, screen.FilteredCount);
                Assert.AreEqual(AchievementCategory.Biomes, achievements.At(screen.AchievementAt(0)).Category);
                screen.SelectCategory(0);
                Assert.AreEqual(ScriptedAchievements.Total, screen.FilteredCount);

                // The claim takes every reached tier (Bronze to Gold: 35 Gems) and the dot goes.
                int cell = view.CellOf(ProfileList.Badges, 0);
                Assert.IsTrue(view.Parts.badges[cell].claim.gameObject.activeSelf, "The claim button with its Gems.");
                view.Parts.badges[cell].claim.onClick.Invoke();
                Assert.AreEqual(AchievementTier.Gold, achievements.Claimed(0));
                Assert.AreEqual(35, achievements.GemsGiven);
                Assert.IsTrue(view.NoteShown, "« +35 Gemmes ».");
                Assert.IsFalse(view.Parts.badges[cell].claim.gameObject.activeSelf, "Claimed: the bar and « Plus que » again.");
                Frame(screen);
                Assert.IsFalse(hud.ProfileDotVisible, "Nothing left to claim: no dot.");
                screen.Claim(0);
                Assert.AreEqual(35, achievements.GemsGiven, "A tier is claimed once.");

                // Titres: an owned title is selected and the card follows; a locked one shows its source; « Aucun titre » clears it.
                screen.SelectTab(ProfileTab.Titles);
                Assert.AreEqual(ProfileTab.Titles, view.TabShown);
                Assert.IsTrue(view.Parts.rowsPage.activeSelf);
                Assert.IsFalse(view.Parts.achievements.activeSelf);
                screen.TapCell(ProfileList.Rows, screen.ItemOfIndex(ProfileItemKind.Title, 3));
                Assert.AreEqual(3, items.Selected(ProfileItemKind.Title));
                Assert.AreEqual(context.Localization.Get("title.test.4.name"), view.CardTitleText, "The card shows the selected title.");
                screen.TapCell(ProfileList.Rows, screen.ItemOfIndex(ProfileItemKind.Title, 1));
                Assert.AreEqual(3, items.Selected(ProfileItemKind.Title), "A locked title is not selected.");
                Assert.AreEqual(context.Localization.Get("title.test.2.source"), view.NoteText, "A locked title shows its source.");
                screen.TapCell(ProfileList.Rows, 0);
                Assert.AreEqual(-1, items.Selected(ProfileItemKind.Title), "« Aucun titre ».");
                Assert.IsTrue(items.Unseen(ProfileItemKind.Title, 3));

                // Cadres: an owned frame is selected; the card's ring takes its palette.
                screen.SelectTab(ProfileTab.Frames);
                Assert.IsFalse(items.Unseen(ProfileItemKind.Title, 3), "« Nouveau » counts as seen when leaving the tab.");
                Assert.IsTrue(view.Parts.framesPage.activeSelf);
                if (view.FrameStyles.Length > 0)
                    items.FrameKeys = view.FrameStyles.Select(s => s.key).ToArray();
                screen.TapCell(ProfileList.Frames, screen.ItemOfIndex(ProfileItemKind.Frame, 1));
                Assert.AreEqual(1, items.Selected(ProfileItemKind.Frame));
                if (view.FrameStyles.Length > 1)
                {
                    var style = view.FrameStyles.First(s => s.key == items.At(ProfileItemKind.Frame, 1).Key);
                    Assert.AreEqual(style.ring, view.Parts.cardFrame.ring.color, "The card's ring is tinted by the frame's palette.");
                }
                screen.TapCell(ProfileList.Frames, screen.ItemOfIndex(ProfileItemKind.Frame, 30));
                Assert.AreEqual(1, items.Selected(ProfileItemKind.Frame), "A locked frame is not selected.");

                // Poses: « Mode photo bientôt ».
                screen.SelectTab(ProfileTab.Poses);
                Assert.IsTrue(view.Parts.photoSoon.gameObject.activeSelf);
                screen.TapCell(ProfileList.Rows, screen.ItemOfIndex(ProfileItemKind.Pose, 0));
                Assert.AreEqual(context.Localization.Get(ProfileText.PhotoSoon), view.NoteText);

                // No allocation per frame while the screen shows.
                screen.SelectTab(ProfileTab.Achievements);
                for (int f = 0; f < 60; f++)
                    Frame(screen);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int f = 0; f < 120; f++)
                    Frame(screen);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0, allocated, "The profile screen allocates nothing per frame.");

                // Closing releases the screen and shows the refuge again.
                screen.CloseScreen();
                Assert.IsFalse(screen.IsOpen);
                yield return WaitFrames(screen, 2);
                Assert.IsFalse(screen.ScreenLoaded, "Released when closed.");
                Assert.IsTrue(entry.CameraRig.Camera.enabled, "The refuge renders again.");
            }
            finally
            {
                screen.Dispose();
            }
        }

        private static void Frame(ProfileScreenPresenter screen)
        {
            screen.Tick(1f / 60f);
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator WaitFrames(ProfileScreenPresenter screen, int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                screen.Tick(1f / 60f);
                yield return null;
            }
        }

        private static IEnumerator WaitFor(ProfileScreenPresenter screen, Func<bool> condition, float seconds, string message)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until)
                    Assert.Fail(message);
                screen.Tick(1f / 60f);
                yield return null;
            }
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
                LocalizationOverride = database.localization.ToArray(),
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

        /// <summary>200 achievements, 13 categories of 15 (the last 20); the first reached Gold with nothing claimed, the others partway.</summary>
        private sealed class ScriptedAchievements : IAchievements
        {
            public const int Total = 200;
            public const int PerCategory = 15;
            private readonly AchievementInfo[] _infos = new AchievementInfo[Total];
            private readonly long[] _progress = new long[Total];
            private readonly AchievementTier[] _claimed = new AchievementTier[Total];

            public ScriptedAchievements()
            {
                for (int i = 0; i < Total; i++)
                {
                    var category = (AchievementCategory)Math.Min(i / PerCategory, 12);
                    _infos[i] = new AchievementInfo(i, "achievement.test." + (i + 1), category, 2, 10, 50, 200);
                    _progress[i] = i % 2;
                }
                _progress[0] = 60;
            }

            public int GemsGiven { get; private set; }

            public int Count => Total;

            public AchievementInfo At(int index) => _infos[index];

            public long Progress(int index) => _progress[index];

            public AchievementTier Reached(int index)
            {
                var info = _infos[index];
                long p = _progress[index];
                return p >= info.Star ? AchievementTier.Star : p >= info.Gold ? AchievementTier.Gold : p >= info.Silver ? AchievementTier.Silver
                    : p >= info.Bronze ? AchievementTier.Bronze : AchievementTier.None;
            }

            public AchievementTier Claimed(int index) => _claimed[index];

            public int Claimable
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < Total; i++)
                        n += Math.Max(0, (int)Reached(i) - (int)_claimed[i]);
                    return n;
                }
            }

            public bool TryClaim(int index)
            {
                var reached = Reached(index);
                if (_claimed[index] >= reached)
                    return false;
                GemsGiven += ProfileLayout.GemsBetween(_claimed[index], reached);
                _claimed[index] = reached;
                Changed?.Invoke();
                return true;
            }

            public event Action Changed;
        }

        /// <summary>40 titles (0, 3, 4 owned; 3 new), 42 frames (0 to 5 owned), 47 poses (0 to 7 owned).</summary>
        private sealed class ScriptedItems : IProfileItems
        {
            private static readonly int[] Counts = { 40, 42, 47 };
            private static readonly string[] Prefix = { "title.test.", "frame.test.", "pose.test." };
            private readonly bool[][] _owned = { new bool[40], new bool[42], new bool[47] };
            private readonly bool[][] _unseen = { new bool[40], new bool[42], new bool[47] };
            private readonly int[] _selected = { 0, -1, -1 };

            public ScriptedItems()
            {
                _owned[0][0] = _owned[0][3] = _owned[0][4] = true;
                _unseen[0][3] = true;
                for (int i = 0; i < 6; i++)
                    _owned[1][i] = true;
                for (int i = 0; i < 8; i++)
                    _owned[2][i] = true;
            }

            /// <summary>The frames' keys (the prefab's baked styles, so the card's ring can be checked).</summary>
            public string[] FrameKeys { get; set; }

            public int Count(ProfileItemKind kind) => Counts[(int)kind];

            public ProfileItemInfo At(ProfileItemKind kind, int index)
            {
                string key = kind == ProfileItemKind.Frame && FrameKeys != null && index < FrameKeys.Length
                    ? FrameKeys[index]
                    : Prefix[(int)kind] + (index + 1);
                // Reverse gallery order: the presenter sorts by Order.
                return new ProfileItemInfo(index, key, Counts[(int)kind] - index);
            }

            public bool Owned(ProfileItemKind kind, int index) => _owned[(int)kind][index];

            public int Selected(ProfileItemKind kind) => kind == ProfileItemKind.Pose ? -1 : _selected[(int)kind];

            public bool TrySelect(ProfileItemKind kind, int index)
            {
                if (kind == ProfileItemKind.Pose || (index >= 0 && !_owned[(int)kind][index]) || (index < 0 && kind != ProfileItemKind.Title))
                    return false;
                _selected[(int)kind] = index;
                Changed?.Invoke();
                return true;
            }

            public bool Unseen(ProfileItemKind kind, int index) => _unseen[(int)kind][index];

            public void MarkSeen(ProfileItemKind kind)
            {
                Array.Clear(_unseen[(int)kind], 0, _unseen[(int)kind].Length);
                Changed?.Invoke();
            }

            public event Action Changed;
        }
    }
}
