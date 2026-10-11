using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Services.Store;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T48 event screens in the real game with the test event « event.test » (EventTestFixture, injected through
    /// BootOptions.EventCalendarOverride and a fake clock): the HUD's Event button, the hub's track (claim), shop (buy), quests
    /// (swap) and egg (open), the end summary at the first check after the end; and, on request, the review captures
    /// TestResults/Captures/T48_*.png (announce, every tab, the odds, the shop's « Événement » section, the summary, the Memory Shop).
    /// </summary>
    public class EventHubPlayTests
    {
        private string _saveDir;
        private FakeGameClock _clock;
        private DateTimeOffset _start;
        private DateTimeOffset _end;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(bootstrap.gameObject);
            foreach (var splash in Object.FindObjectsByType<SplashScreen>(FindObjectsSortMode.None))
                Object.Destroy(splash.gameObject);
            TestSaveDir.Release(_saveDir);
        }

        [UnityTest]
        public IEnumerator Hub_Claim_Buy_Swap_Egg_ThenSummary()
        {
            yield return Boot(version: 1, daysFromStart: 1.0);
            var refuge = Refuge;
            yield return ClearPopups(refuge);
            var events = Context.Events;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            yield return Wait(1.3f);
            Assert.AreEqual(EventPhase.Running, events.Phase);
            Assert.IsTrue(hud.EventVisible, "The Event button shows on the rail during the run.");

            refuge.OpenEvent();
            yield return Wait(0.4f);
            var hub = Object.FindAnyObjectByType<EventHubScreen>();
            Assert.IsTrue(hub.IsOpen);
            Assert.IsTrue(events.IsJoined, "Opening the hub joins the event.");
            StringAssert.Contains(EventHubModel.FormatDate(_end, Context.Localization.Language), hub.DateText, "The real end date, never a countdown.");

            events.GrantEventCurrency(2000, ActivityId.Grooming);
            hub.Refresh();
            hub.PressClaim(1, false);
            Assert.IsTrue(events.IsClaimed(1, false), "Tier 1 claimed from its cell.");

            hub.SelectTab(EventHubTab.Shop);
            int before = events.Currency;
            int tickets = Enumerable.Range(0, events.ShopCount).First(i => events.ShopItem(i).Kind == EventShopKind.Tickets);
            hub.PressShop(tickets);
            Assert.Less(events.Currency, before, "The tickets line was bought with the event currency.");

            hub.SelectTab(EventHubTab.Quests);
            Assert.Greater(events.DailyQuests.Count, 0);
            int swaps = events.SwapsLeft;
            hub.PressSwap(0);
            Assert.AreEqual(swaps - 1, events.SwapsLeft, "The free swap was used.");

            hub.SelectTab(EventHubTab.Egg);
            int opened = events.Instance.EggsOpened;
            hub.PressOpenEgg();
            Assert.AreEqual(opened + 1, events.Instance.EggsOpened, "One event egg opened.");
            Assert.IsNotEmpty(hub.EggLineText);
            hub.Close();
            yield return Wait(0.3f);
            Assert.IsFalse(hub.IsOpen);

            // The first check after the end: the leftover currency becomes Light and the summary card shows once.
            _clock.Set(_end.AddHours(1));
            events.Tick();
            yield return Wait(1.6f);
            Assert.IsTrue(hub.SummaryOpen, "The end summary card is up.");
            Assert.IsNotEmpty(hub.SummaryCurrencyText);
            hub.Close();
            yield return Wait(0.3f);
            Assert.IsFalse(events.TryGetPendingSummary(out _), "Seen once, never again.");
            Assert.IsFalse(hud.EventVisible, "No button after the end.");
        }

        [UnityTest, Explicit("T48 review captures, run on request.")]
        public IEnumerator Capture_Announce_Hub_Shop_Summary()
        {
            yield return Boot(version: 1, daysFromStart: -1.0);
            var refuge = Refuge;
            yield return ClearPopups(refuge);
            yield return Wait(1.3f);
            yield return Shot("hud_announce");
            refuge.OpenEvent();
            yield return Wait(0.6f);
            yield return Shot("announce");
            var hub = Object.FindAnyObjectByType<EventHubScreen>();
            hub.Close();
            yield return Wait(0.4f);

            _clock.Set(_start.AddDays(2));
            var events = Context.Events;
            events.Tick();
            yield return Wait(1.3f);
            refuge.OpenEvent();
            yield return Wait(0.3f);
            events.GrantEventCurrency(2400, ActivityId.Grooming);
            hub.Refresh();
            yield return Wait(1.2f);
            yield return Shot("hud_running");
            foreach (EventHubTab tab in new[] { EventHubTab.Track, EventHubTab.Egg, EventHubTab.Shop, EventHubTab.Quests, EventHubTab.Challenges, EventHubTab.Goal })
            {
                hub.SelectTab(tab);
                yield return Wait(0.5f);
                yield return Shot("tab_" + tab.ToString().ToLowerInvariant());
            }
            hub.SelectTab(EventHubTab.Egg);
            hub.PressOpenEgg();
            hub.ToggleOdds();
            yield return Wait(0.5f);
            yield return Shot("egg_odds");
            hub.Close();
            yield return Wait(0.5f);

            refuge.OpenShop();
            yield return Wait(1f);
            yield return Shot("shop_event_section");
            refuge.Shop.CloseShop();
            yield return Wait(0.6f);

            _clock.Set(_end.AddHours(1));
            events.Tick();
            yield return Wait(1.6f);
            yield return Shot("end_summary");
        }

        [UnityTest, Explicit("T48 review captures, run on request.")]
        public IEnumerator Capture_MemoryShop()
        {
            yield return Boot(version: 2, daysFromStart: 1.0);
            var refuge = Refuge;
            yield return ClearPopups(refuge);
            yield return Wait(1.3f);
            refuge.OpenEvent();
            yield return Wait(0.3f);
            Context.Events.GrantEventCurrency(3000, ActivityId.Grooming);
            var hub = Object.FindAnyObjectByType<EventHubScreen>();
            hub.SelectTab(EventHubTab.Memory);
            yield return Wait(0.6f);
            Assert.AreEqual(EventHubTab.Memory, hub.Tab);
            yield return Shot("memory_shop");
            hub.PressMemory(1);
            yield return Wait(0.4f);
            yield return Shot("memory_shop_owned");
        }

        // ---------------- helpers ----------------

        /// <summary>EventTestFixture lives in the editor assembly: reached by reflection (play mode tests run in the editor).</summary>
        private static Type Fixture()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PocketWilds.Editor.Events.EventTestFixture")).FirstOrDefault(t => t != null);
            if (type == null)
                Assert.Ignore("EventTestFixture (editor assembly) not loaded.");
            return type;
        }

        private IEnumerator Boot(int version, double daysFromStart)
        {
#if UNITY_EDITOR
            var fixture = Fixture();
            var entry = (EventCalendarEntry)fixture.GetMethod("Entry").Invoke(null, new object[] { 2026, version });
            var calendar = (EventCalendar)fixture.GetMethod("Calendar").Invoke(null, new object[] { new[] { entry } });
            _start = entry.StartUtc;
            _end = entry.EndUtc;
            _clock = new FakeGameClock(_start.AddDays(daysFromStart));
            PrepareSave();
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            var builder = database.CreateBuilder();
            fixture.GetMethod("AddContent").Invoke(null, new object[] { builder, version });
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = builder.Build(),
                VisualCatalogOverride = catalog,
                LocalizationOverride = database.localization.ToArray(),
                StoreProviderOverride = new SandboxStoreProvider("fr", 0),
                ClockOverride = _clock,
                EventCalendarOverride = calendar,
                LoadTargetScene = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 60f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return Wait(1f);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        /// <summary>A refuge with animals and Light, the albums open (the track's stickers), saved at the fake clock's time.</summary>
        private void PrepareSave()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(), _clock,
                new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            string[] animals = { "rabbit.brown", "squirrel.red", "hedgehog.brown", "fawn.chestnutspots", "guineapig.caramel", "fieldmouse.warmbrown" };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource("species." + animals[i].Substring(0, animals[i].IndexOf('.')));
                var variant = ContentId.FromSource("variant." + animals[i]);
                state.Collection.Add(new AnimalInstance { Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = (uint)i + 31u });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            state.Wallet.Light = BigValue.FromMantissaExponent(1.0, 9);
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        /// <summary>Claims the level cards and closes a moment offer, as a player would, before the checks.</summary>
        private static IEnumerator ClearPopups(RefugeController refuge)
        {
            var presenter = Object.FindAnyObjectByType<VerticalSliceEntry>().ProgressionScreens;
            for (int round = 0; round < 6; round++)
            {
                if (refuge.Shop != null && refuge.Shop.OfferOpen)
                    refuge.Shop.CloseOffer();
                var card = Object.FindAnyObjectByType<LevelRewardScreen>();
                if (presenter != null && presenter.RewardWaiting && (card == null || !card.IsOpen))
                    presenter.ShowPendingRewards();
                yield return Wait(0.4f);
                card = Object.FindAnyObjectByType<LevelRewardScreen>();
                if (card != null && card.IsOpen)
                    card.ClaimButton.onClick.Invoke();
                yield return Wait(0.6f);
                if ((presenter == null || !presenter.RewardWaiting) && !refuge.ModalOpen)
                    yield break;
            }
        }

        private static RefugeController Refuge => Object.FindAnyObjectByType<VerticalSliceEntry>().Refuge;
        private static GameContext Context => Object.FindAnyObjectByType<GameBootstrap>().Context;

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        private static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "Captures");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "T48_" + name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
