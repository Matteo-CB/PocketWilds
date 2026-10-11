using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// T44C « Journal du refuge et visiteurs rares » in the real scene (doc « 01 Succès, titres, cadres, poses et journal »,
    /// section 6; doc 05 sheet 25): the journal screen opens from its Addressables group, a page added appears at the top of the
    /// timeline (its memory with its variables, « Nouveau »), the cards are recycled, nothing allocates per frame while it
    /// shows; a pending refuge capture is drawn at a calm moment only, written as journal_{id}.jpg and stored, and the screen
    /// reads it into the card; a rare visitor walks in, sits, is greeted by a touch (its gift), walks out, and a second one
    /// leaves when its visit ends; the visitor's tick allocates nothing.
    /// </summary>
    public class JournalPlayTests
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
        public IEnumerator Journal_PageAdded_AppearsOnTheTimeline()
        {
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            Assert.IsNotNull(entry);
            var journal = new ScriptedJournal(9, 2);
            var store = new JournalImageStore(_saveDir);
            var text = new TestTexts(context.Localization);
            var screen = new JournalScreenPresenter(context, journal, null, null, store, text) { ReduceMotionOverride = false };
            try
            {
                screen.OpenJournal();
                yield return WaitFor(screen, () => screen.ScreenShown, 5f, "The journal screen loads from its Addressables group.");
                var view = (JournalScreenView)screen.View;
                Assert.IsTrue(screen.ScreenLoaded, "The screen comes from its Addressables group (Journal).");
                Assert.IsTrue(view.Valid);
                Assert.AreEqual(JournalLayout.SortingOrder, view.Canvas.sortingOrder);
                Assert.AreEqual(0, journal.UnseenCount, "Opening the journal marks its pages seen (the entry's dot goes).");
                Assert.AreEqual(2, screen.NewCount, "The two pages added since the last visit say « Nouveau ».");
                int months = journal.Months();
                Assert.AreEqual(journal.PageCount + months, screen.TimelineCount, "One header per month, one card per page.");
                Assert.IsTrue(screen.IsHeader(0), "The timeline starts with the newest month.");

                // A page added while the screen shows comes at the top, its memory with its variables filled.
                journal.Add("memory.test.added", journal.NewestDay);
                Frame(screen);
                Assert.AreEqual(journal.PageCount + months, screen.TimelineCount);
                int item = screen.ItemOfPage(0);
                Assert.AreEqual(1, item, "The new page is the first card, under its month.");
                int cell = view.CardCellOf(item);
                Assert.GreaterOrEqual(cell, 0, "The new page's card is on screen.");
                StringAssert.Contains("Noisette", view.CardText(cell), "Its variables are filled ({name}).");
                StringAssert.DoesNotContain("{name}", view.CardText(cell));

                // The cards are recycled: scrolling to the bottom binds other pages, nothing is instantiated.
                int cards = view.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Length;
                view.TimelineScroll.verticalNormalizedPosition = 0f;
                for (int f = 0; f < 4; f++)
                    Frame(screen);
                Assert.AreEqual(cards, view.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Length, "No cell is made while scrolling.");
                Assert.GreaterOrEqual(view.CardCellOf(screen.ItemOfPage(journal.PageCount - 1)), 0, "The oldest page shows at the bottom.");

                // The Moments tab lists the surprises seen.
                screen.SelectTab(JournalTab.Moments);
                Frame(screen);
                Assert.AreEqual(journal.SeenSurpriseCount, screen.MomentCount);
                Assert.IsFalse(view.Parts.momentsEmpty.gameObject.activeSelf, "Moments seen: no empty state.");
                screen.SelectTab(JournalTab.Pages);

                // No allocation per frame while the screen shows.
                for (int f = 0; f < 30; f++)
                    Frame(screen);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int f = 0; f < 120; f++)
                    Frame(screen);
                Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "The journal screen allocates nothing per frame.");

                screen.CloseScreen();
                yield return null;
                Assert.IsFalse(screen.ScreenLoaded, "Released when closed.");
            }
            finally
            {
                screen.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Journal_Capture_IsStoredAtACalmMoment_AndShown()
        {
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            yield return ClearScreens(entry);
            var journal = new ScriptedJournal(3, 0);
            int pageId = journal.AddPending("memory.test.refuge", JournalImageKind.RefugeCapture, 0u);
            var store = new JournalImageStore(_saveDir);
            var layer = new JournalCaptureLayer(context, journal, null, store)
            {
                GameCamera = () => entry.CameraRig.Camera,
            };
            JournalScreenPresenter screen = null;
            try
            {
                // Never while something is going on.
                bool busy = true;
                layer.Busy = () => busy;
                for (int f = 0; f < 30; f++)
                {
                    layer.Tick(Time.unscaledTime);
                    yield return null;
                }
                Assert.AreEqual(0, layer.Stored, "No capture while a hatch, an activity, a purchase or a level up is on.");

                busy = false;
                float until = Time.realtimeSinceStartup + 10f;
                while (layer.Stored == 0 && Time.realtimeSinceStartup < until)
                {
                    layer.Tick(Time.unscaledTime);
                    yield return null;
                }
                Assert.AreEqual(1, layer.Stored, "The refuge view is captured at a calm moment.");
                Assert.IsTrue(journal.Stored.Contains(pageId), "CaptureStored is called with the page.");
                Assert.IsTrue(File.Exists(store.PathOf(pageId)), "journal_{id}.jpg is under the journal folder.");
                long size = new FileInfo(store.PathOf(pageId)).Length;
                Assert.Greater(size, 2000, "A real JPEG.");
                Assert.Less(size, 200000, "About 40 KB at 512 × 512.");
                Assert.AreEqual(0, journal.PendingCaptureCount);

                // At most one a second: a second pending page waits a second.
                journal.AddPending("memory.test.refuge2", JournalImageKind.RefugeCapture, 0u);
                float start = Time.realtimeSinceStartup;
                until = start + 10f;
                while (layer.Stored == 1 && Time.realtimeSinceStartup < until)
                {
                    layer.Tick(Time.unscaledTime);
                    yield return null;
                }
                Assert.AreEqual(2, layer.Stored);
                Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, JournalLayout.CaptureGapSeconds * 0.9f, "One capture a second at most.");

                // The journal screen reads the stored picture into its card.
                screen = new JournalScreenPresenter(context, journal, null, null, store) { ReduceMotionOverride = false };
                screen.OpenJournal();
                yield return WaitFor(screen, () => screen.ScreenShown, 5f, "The journal screen loads.");
                int item = screen.ItemOfPage(journal.IndexOf(pageId));
                for (int f = 0; f < 10 && !screen.PictureLoaded(item); f++)
                    Frame(screen);
                Assert.IsTrue(screen.PictureLoaded(item), "The card shows the stored capture.");
                Assert.IsTrue(((JournalScreenView)screen.View).CardShowsPicture(screen.View.CardCellOf(item)));
            }
            finally
            {
                screen?.Dispose();
                layer.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Visitor_ArrivesIsGreetedAndLeaves()
        {
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            yield return ClearScreens(entry);
            var refuge = entry.Refuge;
            var species = ContentId.None;
            for (int i = 0; i < context.Content.Species.Count && species == ContentId.None; i++)
                if (refuge.PooledSparesOf(context.Content.Species[i].Id) > 0)
                    species = context.Content.Species[i].Id;
            Assert.AreNotEqual(ContentId.None, species, "A species with a view in the refuge.");
            var visitors = new ScriptedVisitors(species);
            refuge.Visitors = visitors;
            try
            {
                // It walks in from the side and sits on a free spot.
                visitors.Arrive();
                yield return WaitUntil(() => refuge.VisitorShown, 3f, "The visitor comes in.");
                Assert.AreEqual(visitors.Current.Key, refuge.VisitorKey);
                yield return WaitUntil(() => refuge.VisitorSeated, 10f, "It reaches its spot within 8 s and sits.");
                var seat = refuge.VisitorPosition;
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.Less(Vector3.Distance(seat, refuge.VisitorPosition), 0.6f, "It stays on its spot and looks at the refuge.");

                // Its tick allocates nothing.
                var method = typeof(RefugeController).GetMethod("TickVisitors", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method);
                var tick = (Action<float, float>)method.CreateDelegate(typeof(Action<float, float>), refuge);
                tick(Time.time, 0f);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int f = 0; f < 120; f++)
                    tick(Time.time, 1f / 60f);
                Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "The seated visitor allocates nothing per frame.");

                // A touch greets it: the gift, then it walks out (it did not join).
                var camera = entry.CameraRig.Camera;
                var screenPoint = camera.WorldToScreenPoint(refuge.VisitorPosition + new Vector3(0f, 0.25f, 0f));
                refuge.OnTap(new Vector2(screenPoint.x, screenPoint.y));
                Assert.AreEqual(1, visitors.Greets, "The touch greets the visitor.");
                yield return WaitUntil(() => refuge.VisitorLeaving, 4f, "After the greeting it walks out.");
                yield return WaitUntil(() => !refuge.VisitorShown, 12f, "It leaves the refuge.");

                // A second visitor leaves when its visit ends.
                visitors.Arrive();
                yield return WaitUntil(() => refuge.VisitorShown, 3f, "The next visitor comes in.");
                visitors.Leave();
                yield return WaitUntil(() => refuge.VisitorLeaving || !refuge.VisitorShown, 3f, "The visit ends: it walks out.");
                yield return WaitUntil(() => !refuge.VisitorShown, 12f, "It is gone.");
            }
            finally
            {
                refuge.Visitors = null;
            }
        }

        // ---------------- helpers ----------------

        private static void Frame(JournalScreenPresenter screen)
        {
            screen.Tick(1f / 60f);
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator WaitFor(JournalScreenPresenter screen, Func<bool> condition, float seconds, string message)
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

        private static IEnumerator WaitUntil(Func<bool> condition, float seconds, string message)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until)
                    Assert.Fail(message);
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
            // The refuge scene and its Addressables load cold when this is the run's first boot (as ActivityFlowPlayTests).
            float timeout = Time.realtimeSinceStartup + 120f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            ready(bootstrap);
        }

        /// <summary>Closes what a boot may open over the refuge (the offline return card, a level's reward card, Today).</summary>
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

        /// <summary>The game's texts, with a memory for the test pages (« memory.test.….text »).</summary>
        private sealed class TestTexts : ILocalization
        {
            private readonly ILocalization _inner;

            public TestTexts(ILocalization inner) => _inner = inner;

            public string Language => _inner.Language;

            public string Get(string key) =>
                key.StartsWith("memory.test.", StringComparison.Ordinal) && key.EndsWith(".text", StringComparison.Ordinal)
                    ? "{name} found a sunny spot in {biome}."
                    : _inner.Get(key);
        }

        /// <summary>Pages over the last months (newest first), their variables, the pending captures and the surprises seen.</summary>
        private sealed class ScriptedJournal : IJournal
        {
            private static readonly DateTime Epoch = new DateTime(2000, 1, 1);
            private readonly List<JournalPage> _pages = new List<JournalPage>();
            private readonly List<string> _surprises = new List<string>();
            private int _nextId = 1;
            private int _unseen;

            public readonly List<int> Stored = new List<int>();

            public ScriptedJournal(int pages, int unseen)
            {
                int today = (int)(new DateTime(2026, 10, 12) - Epoch).TotalDays;
                for (int i = pages - 1; i >= 0; i--)
                    _pages.Insert(0, new JournalPage(_nextId++, "memory.test." + i, today - (pages - 1 - i) * 9, JournalImageKind.Thumbnail, 0u, false));
                for (int i = 0; i < 6; i++)
                    _surprises.Add("surprise.test." + i);
                _unseen = unseen;
            }

            public int NewestDay => _pages.Count > 0 ? _pages[0].Day : 0;

            public int Months() => _pages.Select(p => Epoch.AddDays(p.Day).Year * 12 + Epoch.AddDays(p.Day).Month).Distinct().Count();

            public int IndexOf(int pageId) => _pages.FindIndex(p => p.Id == pageId);

            public void Add(string key, int day)
            {
                _pages.Insert(0, new JournalPage(_nextId++, key, day, JournalImageKind.Thumbnail, 0u, false));
                _unseen++;
                PageAdded?.Invoke();
            }

            public int AddPending(string key, JournalImageKind image, uint imageRef)
            {
                int id = _nextId++;
                _pages.Insert(0, new JournalPage(id, key, NewestDay, image, imageRef, false));
                return id;
            }

            public int PageCount => _pages.Count;

            public JournalPage PageAt(int index) => _pages[index];

            public int Variables(int pageId, string[] names, string[] values)
            {
                names[0] = "name";
                values[0] = "Noisette";
                names[1] = "{biome}";
                values[1] = "Prairie";
                return 2;
            }

            public int PendingCaptureCount
            {
                get
                {
                    int n = 0;
                    foreach (var p in _pages)
                        if (!p.Captured && (p.Image == JournalImageKind.AnimalCapture || p.Image == JournalImageKind.RefugeCapture))
                            n++;
                    return n;
                }
            }

            public JournalPage PendingCaptureAt(int index)
            {
                foreach (var p in _pages)
                    if (!p.Captured && (p.Image == JournalImageKind.AnimalCapture || p.Image == JournalImageKind.RefugeCapture) && index-- == 0)
                        return p;
                return default;
            }

            public void CaptureStored(int pageId)
            {
                Stored.Add(pageId);
                int i = IndexOf(pageId);
                if (i >= 0)
                {
                    var p = _pages[i];
                    _pages[i] = new JournalPage(p.Id, p.Key, p.Day, p.Image, p.ImageRef, true);
                }
            }

            public int SeenSurpriseCount => _surprises.Count;

            public string SeenSurpriseAt(int index) => _surprises[index];

            public int UnseenCount => _unseen;

            public void MarkSeen() => _unseen = 0;

            public event Action PageAdded;
        }

        /// <summary>One visitor at a time, arriving and leaving on demand; greetings give Gems and never join.</summary>
        private sealed class ScriptedVisitors : IVisitors
        {
            private readonly ContentId _species;
            private bool _present;
            private int _visits;

            public int Greets;

            public ScriptedVisitors(ContentId species) => _species = species;

            public bool IsOpen => true;

            public bool Present => _present;

            public VisitorVisit Current { get; private set; }

            public long NextArrivalUnixMs => -1;

            public void Arrive()
            {
                _visits++;
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Current = new VisitorVisit("visitor.test." + _visits, _species, ContentId.None, now, now + 2L * 3600L * 1000L);
                _present = true;
                VisitChanged?.Invoke();
            }

            public void Leave()
            {
                _present = false;
                VisitChanged?.Invoke();
            }

            public bool TryGreet(out VisitorGreeting greeting)
            {
                greeting = default;
                if (!_present)
                    return false;
                Greets++;
                var gift = new RewardBundle { Gems = 5 };
                greeting = new VisitorGreeting(Current.Key, in gift, Greets == 1, false, 0u);
                _present = false;
                return true;
            }

            public event Action VisitChanged;
        }
    }
}
