using System;
using System.Collections;
using System.Collections.Generic;
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
    /// T44A « Albums » in the real scene (doc « 01 Albums et autocollants », sections 1.7, 1.10, 1.12): the refuge HUD shows
    /// the Albums button once the albums open; it opens PF_UI_Album from its Addressables group over a hidden refuge with the
    /// borrowed fonts and nothing of the scene in its bundle; the shelf opens a page, a swipe turns it (a short drag springs
    /// back), nothing allocates per frame while it shows; a gain is revealed (Common before Shiny in a batch, « Déjà collé »
    /// for a duplicate, a touch opens the pouch and ends the reveal); 10 stars buy a missing sticker after « 10 étoiles pour
    /// cet autocollant ? »; closing releases the screen; with the screen closed a gain is revealed over the refuge (the
    /// reveal layer alone) unless something blocks it.
    /// </summary>
    public class AlbumPlayTests
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
                TestSaveDir.Release(_saveDir);
            }
            catch (IOException)
            {
            }
        }

        [UnityTest]
        public IEnumerator Albums_OpenSwipeRevealAndChoose_OnTheirScreen()
        {
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            Assert.IsNotNull(entry);
            yield return ClearScreens(entry);
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            Assert.IsNotNull(hud, "The refuge HUD is in the scene.");
            Assert.IsNotNull(hud.AlbumsButton, "The HUD carries the Albums button (rebuild the HUD with UiPipeline).");

            var albums = new ScriptedAlbums { Open = false };
            // The scene's own presenter (the real albums, closed here) would drive the same HUD button: the test takes over.
            entry.DetachAlbums();
            var screen = AlbumScreenPresenter.Create(context, albums, entry.Activities, hud);
            try
            {
                // Hidden until the albums open (refuge level 12 and day 5), then shown.
                Frame(screen);
                Assert.IsFalse(hud.AlbumsVisible, "No Albums button before the albums open.");
                albums.Open = true;
                yield return WaitFrames(screen, 40);
                Assert.IsTrue(hud.AlbumsVisible, "The Albums button shows once the albums open.");
                Assert.IsTrue(hud.AlbumsDotVisible, "A sticker waits to be seen: the dot.");

                // The button opens the screen from its Addressables group over a hidden refuge.
                hud.AlbumsButton.onClick.Invoke();
                Assert.IsTrue(screen.IsOpen);
                yield return WaitFor(screen, () => screen.View != null && screen.ScreenShown, 5f, "The albums screen loads in a moment.");
                var view = (AlbumScreenView)screen.View;
                Assert.IsTrue(screen.ScreenLoaded, "The screen comes from its Addressables group (Albums).");
                Assert.IsTrue(view.Valid);
                Assert.AreEqual(AlbumLayout.SortingOrder, view.Canvas.sortingOrder);
                Assert.AreEqual(AlbumScreenMode.Shelf, screen.Mode);
                Assert.IsFalse(entry.CameraRig.Camera.enabled, "The refuge does not render under the screen.");
#if UNITY_EDITOR
                foreach (string dependency in UnityEditor.AssetDatabase.GetDependencies(AlbumLayout.PrefabPath, true))
                {
                    StringAssert.DoesNotContain("/Fonts/", dependency, "No font in the screen's bundle (borrowed from the scene).");
                    StringAssert.DoesNotContain("/Art/UI/Kit/", dependency, "No kit sprite in the screen's bundle.");
                    StringAssert.DoesNotContain("/Art/UI/Icons/", dependency, "No icon of the scene's atlas in the screen's bundle.");
                }
#endif
                foreach (var label in view.BorrowedLabels)
                    Assert.IsNotNull(label.font, label.name + " got its font from the scene.");
                Assert.AreEqual("14", view.StarsText, "The album stars are always shown.");
                Assert.IsTrue(view.Parts.choose.interactable, "« Choisir un autocollant » from 10 stars.");
                Assert.AreEqual(3, screen.CoverCount);

                // A cover opens its first open page; the « Nouveau » sticker counts as seen once shown.
                screen.OpenAlbum(0);
                Assert.AreEqual(AlbumScreenMode.Page, screen.Mode);
                Assert.AreEqual(0, screen.PageShown);
                Assert.AreEqual("12/12", view.RatioText);
                Assert.IsFalse(albums.Unseen(3), "The page was seen.");
                Assert.IsTrue(view.Parts.slots[11].sign.gameObject.activeSelf, "The Shiny's round pearl.");
                Assert.AreSame(view.PearlMaterial, view.Parts.slots[11].art.material, "The Shiny's pearl sheen material.");

                // The swipe: past the threshold the page turns, a short drag springs back; closed pages are skipped.
                view.Parts.swipe.Simulate(-AlbumLayout.SwipeThreshold - 60f);
                Assert.AreEqual(1, screen.PageShown, "Swiped to the next page.");
                Assert.AreEqual("6/12", view.RatioText);
                Assert.IsTrue(view.Parts.slots[1].silhouette.gameObject.activeSelf, "A missing sticker shows its silhouette.");
                Assert.AreSame(view.SilhouetteMaterial, view.Parts.slots[1].silhouette.material);
                view.Parts.swipe.Simulate(-40f);
                Assert.AreEqual(1, screen.PageShown, "A short drag springs back.");
                view.Parts.swipe.Simulate(AlbumLayout.SwipeThreshold + 60f);
                Assert.AreEqual(0, screen.PageShown, "Swiped back.");
                screen.NextPage();
                screen.NextPage();
                Assert.AreEqual(2, screen.PageShown);
                Assert.IsTrue(view.Parts.slots[10].soon.gameObject.activeSelf, "A subject not in this build: « bientôt ».");
                screen.NextPage();
                Assert.AreEqual(2, screen.PageShown, "The album's last open page.");
                screen.TapSlot(1);
                Assert.IsTrue(view.NoteShown, "An empty slot's touch shows its source's help.");

                // No allocation per frame while the screen shows.
                for (int f = 0; f < 60; f++)
                    Frame(screen);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int f = 0; f < 120; f++)
                    Frame(screen);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0, allocated, "The albums screen allocates nothing per frame.");

                // A batch of gains: Common first, then Shiny; a touch opens the pouch, another ends the reveal.
                screen.PreviousPage();
                screen.PreviousPage();
                Assert.AreEqual(0, screen.PageShown);
                albums.Gain(ScriptedAlbums.Sticker(1, 11), true, 0);
                albums.Gain(ScriptedAlbums.Sticker(1, 1), true, 0);
                Frame(screen);
                Assert.IsTrue(screen.Revealing, "The reveal starts over the screen.");
                Assert.AreEqual(2, screen.RevealCount);
                Assert.AreEqual(ScriptedAlbums.Sticker(1, 1), screen.RevealAt(0).Sticker, "Common first.");
                Assert.AreEqual(ScriptedAlbums.Sticker(1, 11), screen.RevealAt(1).Sticker, "The Shiny last (the peak).");
                Assert.AreEqual(0, albums.PendingCount, "The pending gains were drained.");
                yield return WaitFor(screen, () => view.RevealShown, 3f, "The pouch shows.");
                Assert.IsFalse(screen.RevealOpened);
                screen.TapReveal();
                Assert.IsTrue(screen.RevealOpened, "A touch opens the pouch.");
                screen.TapReveal();
                yield return WaitFor(screen, () => !screen.Revealing, 8f, "The batch ends (the second pouch opens alone after 0.8 s).");

                // A duplicate: « Déjà collé : +2 étoiles ».
                albums.Gain(ScriptedAlbums.Sticker(0, 8), false, 2);
                yield return WaitFor(screen, () => view.RevealShown, 3f, "The duplicate's pouch shows.");
                screen.TapReveal();
                yield return WaitFrames(screen, 10);
                StringAssert.Contains("2", view.DuplicateText, "« Déjà collé : +2 étoiles ».");
                yield return WaitFor(screen, () => !screen.Revealing, 4f, "The duplicate's reveal ends.");
                Assert.AreEqual("16", view.StarsText, "The stars counter took the duplicate's stars.");

                // 10 stars for a missing sticker: the choose list, the page, « 10 étoiles pour cet autocollant ? ».
                screen.Back();
                Assert.AreEqual(AlbumScreenMode.Shelf, screen.Mode);
                screen.OpenChoose();
                Assert.AreEqual(AlbumScreenMode.ChooseList, screen.Mode);
                Assert.Greater(screen.ChoiceRowCount, 0, "The open pages with stickers to find.");
                int page = screen.ChoiceRowPage(0);
                screen.PickChoicePage(0);
                Assert.AreEqual(AlbumScreenMode.ChoosePage, screen.Mode);
                int slot = albums.FirstMissing(page);
                Assert.GreaterOrEqual(slot, 0);
                Assert.IsTrue(view.Parts.slots[slot].priceChip.gameObject.activeSelf, "A missing sticker shows its price.");
                screen.TapSlot(slot);
                Assert.IsTrue(view.ConfirmShown, "« 10 étoiles pour cet autocollant ? ».");
                screen.ConfirmChoice();
                Assert.IsFalse(view.ConfirmShown);
                Assert.AreEqual(ScriptedAlbums.Sticker(page, slot), albums.Chosen, "The sticker was bought.");
                Assert.AreEqual(6, albums.AlbumStars);
                Assert.AreEqual(AlbumScreenMode.Page, screen.Mode, "Out of stars, a plain page again.");
                yield return WaitFor(screen, () => view.RevealShown, 3f, "The chosen sticker's reveal.");
                screen.TapReveal();
                screen.TapReveal();
                yield return WaitFor(screen, () => !screen.Revealing, 4f, "Pasted onto its page.");
                Assert.IsTrue(view.Parts.slots[slot].art.gameObject.activeSelf || view.Parts.slots[slot].silhouette.gameObject.activeSelf);
                Assert.IsTrue(albums.Owned(ScriptedAlbums.Sticker(page, slot)));

                // Closing releases the screen; the refuge and its HUD come back.
                screen.CloseScreen();
                Assert.IsFalse(screen.IsOpen);
                Assert.IsFalse(screen.ScreenLoaded, "The screen's bundle is released.");
                Assert.IsTrue(entry.CameraRig.Camera.enabled, "The refuge renders again.");

                // Over the refuge: blocked while something is open, then the reveal layer alone.
                screen.RevealBlocked = () => true;
                albums.Gain(ScriptedAlbums.Sticker(1, 3), true, 0);
                yield return WaitFrames(screen, 10);
                Assert.IsFalse(screen.Revealing, "No reveal while an activity or a screen is open.");
                screen.RevealBlocked = () => false;
                yield return WaitFor(screen, () => screen.View != null && screen.View.RevealShown, 5f, "The reveal plays over the refuge.");
                Assert.IsFalse(screen.View.ScreenShown, "Only the reveal layer shows.");
                screen.TapReveal();
                screen.TapReveal();
                yield return WaitFor(screen, () => !screen.Revealing, 4f, "The reveal ends.");
                yield return WaitFrames(screen, 2);
                Assert.IsFalse(screen.ScreenLoaded, "Released after the reveal.");
            }
            finally
            {
                screen.Dispose();
            }
        }

        private static void Frame(AlbumScreenPresenter screen)
        {
            screen.Tick(1f / 60f);
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator WaitFrames(AlbumScreenPresenter screen, int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                screen.Tick(1f / 60f);
                yield return null;
            }
        }

        private static IEnumerator WaitFor(AlbumScreenPresenter screen, Func<bool> condition, float seconds, string message)
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

        /// <summary>
        /// Scripted albums: album 0 has 3 open pages (full, half, with « bientôt » slots 9 to 11), album 1 one open page and a
        /// closed one, album 2 a closed page. Slots 8 to 10 are Rare, 11 Shiny. 14 stars.
        /// </summary>
        private sealed class ScriptedAlbums : IAlbums
        {
            private const int Pages = 6;
            private readonly StickerInfo[] _stickers = new StickerInfo[Pages * 12];
            private readonly AlbumPageInfo[] _pages = new AlbumPageInfo[Pages];
            private readonly bool[] _owned = new bool[Pages * 12];
            private readonly bool[] _unseen = new bool[Pages * 12];
            private readonly List<StickerGain> _pending = new List<StickerGain>();
            private static readonly int[] AlbumOf = { 0, 0, 0, 1, 1, 2 };

            public ScriptedAlbums()
            {
                for (int p = 0; p < Pages; p++)
                {
                    _pages[p] = new AlbumPageInfo(p, "page.test." + (p + 1), AlbumOf[p], AlbumKind.Biome, p * 12);
                    for (int s = 0; s < 12; s++)
                    {
                        int i = p * 12 + s;
                        var rarity = s == 11 ? StickerRarity.Shiny : s >= 8 ? StickerRarity.Rare : StickerRarity.Common;
                        _stickers[i] = new StickerInfo(i, "sticker.test." + (i + 1), p, s, rarity, true, (StickerHint)(s % 11));
                    }
                }
                for (int s = 0; s < 12; s++)
                    _owned[s] = true;
                for (int s = 0; s < 6; s++)
                    _owned[12 + s * 2] = true;
                _unseen[3] = true;
                AlbumStars = 14;
            }

            public bool Open { get; set; } = true;
            public int Chosen { get; private set; } = -1;

            public static int Sticker(int page, int slot) => page * 12 + slot;

            public int FirstMissing(int page)
            {
                for (int s = 0; s < 12; s++)
                    if (!_owned[page * 12 + s] && Available(page * 12 + s))
                        return s;
                return -1;
            }

            public void Gain(int sticker, bool isNew, int stars)
            {
                if (isNew)
                {
                    _owned[sticker] = true;
                    _unseen[sticker] = true;
                }
                else
                {
                    AlbumStars += stars;
                }
                _pending.Add(new StickerGain(sticker, isNew, stars, false, false, false));
                Gained?.Invoke();
            }

            public bool IsOpen => Open;
            public int AlbumCount => 3;
            public AlbumKind KindOfAlbum(int album) => AlbumKind.Biome;
            public string AlbumKey(int album) => "album.test." + album;
            public int PageCount => Pages;
            public AlbumPageInfo PageAt(int page) => _pages[page];
            public bool IsPageOpen(int page) => page <= 3;
            public int StickerCount => _stickers.Length;
            public StickerInfo StickerAt(int sticker) => _stickers[sticker];
            public bool Owned(int sticker) => _owned[sticker];
            public bool Unseen(int sticker) => _unseen[sticker];

            public void MarkPageSeen(int page)
            {
                for (int s = 0; s < 12; s++)
                    _unseen[page * 12 + s] = false;
            }

            public bool Available(int sticker) => !(_stickers[sticker].Page == 2 && _stickers[sticker].Slot >= 9);

            public int OwnedOnPage(int page)
            {
                int n = 0;
                for (int s = 0; s < 12; s++)
                    if (_owned[page * 12 + s])
                        n++;
                return n;
            }

            public int AlbumStars { get; private set; }
            public int ChoicePrice => 10;

            public bool TryChoose(int sticker)
            {
                if (AlbumStars < ChoicePrice || _owned[sticker])
                    return false;
                AlbumStars -= ChoicePrice;
                Chosen = sticker;
                Gain(sticker, true, 0);
                return true;
            }

            public int PendingCount => _pending.Count;
            public StickerGain PendingAt(int index) => _pending[index];
            public void ClearPending() => _pending.Clear();
            public event Action Gained;
        }
    }
}
