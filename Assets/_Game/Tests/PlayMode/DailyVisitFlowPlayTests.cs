using System;
using System.Collections;
using System.IO;
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
    /// T36 « Rendez-vous du jour » in the real game (doc 01 Contenu sections 0 to 6): the welcome's first wish over Mochi
    /// ends the welcome and brings the Today button; a whole daily visit (welcome gift, daily gift, calendar stamp, close)
    /// takes a handful of taps and well under two minutes; midnight during play brings a new gift and a new box; a long
    /// absence brings the return gift on the offline card, taken with the Light by one « Récolter ». A fake clock drives
    /// the days.
    /// </summary>
    public class DailyVisitFlowPlayTests
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

        /// <summary>Ten in the morning, local time (the day key follows the player's time zone).</summary>
        private static DateTimeOffset LocalMorning() =>
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Unspecified), TimeZoneInfo.Local), TimeSpan.Zero);

        [UnityTest]
        public IEnumerator FirstWish_EndsTheWelcome_ThenTodayAppears()
        {
#if UNITY_EDITOR
            var clock = new FakeGameClock(LocalMorning());
            GameBootstrap bootstrap = null;
            yield return Boot(clock, b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var refuge = entry.Refuge;
            var progression = entry.ProgressionScreens;
            var daily = entry.DailyVisit;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            Assert.IsNotNull(daily, "The scene runs the rendez-vous.");
            var state = context.State;
            Assert.IsFalse(state.IsOnboardingDone);
            Assert.IsFalse(hud.TodayVisible, "No Today button during the welcome.");

            context.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e6), EconomyReason.Debug);
            yield return Hatch(refuge, 3);
            yield return ClaimCards(progression);

            // A08: the Upgrades panel closes, the first wish appears over Mochi.
            progression.OpenUpgrades();
            yield return null;
            progression.CloseUpgrades();
            yield return WaitFor(() => daily.Services.Wishes.TutorialActive, 3f, "The first wish appears when the Upgrades panel closes.");
            uint mochi = state.Collection[0].InstanceId;
            Assert.IsTrue(daily.Services.Wishes.WishOf(mochi, out int slot), "Mochi expresses it.");
            Assert.AreEqual(2, daily.Services.Wishes.Get(slot).Target, "« Fais éclore 2 œufs ».");
            if (daily.Bubbles != null)
                yield return WaitFor(() => daily.Bubbles.ShownCount >= 1, 3f, "Its bubble shows in the world.");

            // A10: two eggs fulfil it; touching Mochi claims it and ends the welcome.
            yield return Hatch(refuge, 2);
            yield return ClaimCards(progression);
            Assert.IsTrue(daily.Services.Wishes.Get(slot).IsDone, "Two hatches fulfil the wish.");
            Assert.IsTrue(state.DailyVisit.OnboardingDone, "The welcome is over.");
            long gems = state.Wallet.Gems;
            Assert.IsTrue(daily.OnAnimalTapped(mochi), "Touching Mochi claims the fulfilled wish.");
            Assert.Greater(state.Wallet.Gems, gems, "Its Gems are paid.");
            Assert.IsFalse(daily.Services.Wishes.WishOf(mochi, out _));

            // A12: Today arrives on the right rail with its dot (level 2, the welcome over).
            if (context.Progression.Refuge.Level < 2)
                state.Progression.Refuge.Level = 2;
            yield return WaitFor(() => hud.TodayVisible && hud.TodayDotVisible, 6f, "The Today button arrives with its dot.");
            Assert.IsTrue(hud.GemsChipVisible, "The Gems chip shows from the first Gems.");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator DailyVisit_UnderTwoMinutes()
        {
#if UNITY_EDITOR
            var clock = new FakeGameClock(LocalMorning());
            GameBootstrap bootstrap = null;
            yield return Boot(clock, b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var daily = entry.DailyVisit;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            yield return OnboardedAtLevelTwo(context, entry, daily);
            yield return WaitFor(() => hud.TodayVisible, 6f, "Today is open.");

            var today = Object.FindAnyObjectByType<TodayScreen>();
            var state = context.State;
            float start = Time.realtimeSinceStartup;
            int taps = 0;

            daily.OpenToday();
            taps++;
            yield return WaitFor(() => today.IsOpen, 2f, "Today opens.");
            Assert.IsTrue(today.WelcomeClaimShown, "Day 1: the welcome gift waits.");
            long gems = state.Wallet.Gems;
            daily.ClaimWelcome();
            taps++;
            Assert.AreEqual(gems + 100, state.Wallet.Gems, "100 Gems of welcome (doc 01 section 1.4).");

            int eggs = state.World.DiscoveryEggs;
            daily.OpenGift();
            taps++;
            Assert.AreEqual(1, state.DailyVisit.GiftsOpened);
            Assert.AreEqual(eggs + 1, state.World.DiscoveryEggs, "The first gift is a Discovery egg (doc 01 section 3).");
            yield return new WaitForSecondsRealtime(2.5f);

            daily.StampCalendar();
            taps++;
            Assert.AreEqual(1, state.DailyVisit.CalendarStamps, "Box 1 is stamped.");
            yield return new WaitForSecondsRealtime(1f);
            if (daily.Services.Wishes.HasDoneUnclaimed)
            {
                daily.ClaimAllWishes();
                taps++;
            }

            daily.CloseToday();
            taps++;
            yield return WaitFor(() => !today.Visible, 2f, "Today closes in one gesture.");
            float seconds = Time.realtimeSinceStartup - start;
            Assert.IsFalse(daily.Services.TodayDot, "Nothing left to claim: the dot is off.");
            // A human taps about every 1.5 s; the screen's own animations are measured.
            Assert.Less(seconds + taps * 1.5f, 120f, $"The visit takes {seconds:F1} s of animation and {taps} taps.");
            Assert.LessOrEqual(taps, 7, "A handful of taps.");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Midnight_DuringPlay_BringsANewGiftAndBox()
        {
#if UNITY_EDITOR
            var lateLocal = new DateTime(2026, 10, 12, 23, 59, 30, DateTimeKind.Unspecified);
            var clock = new FakeGameClock(new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(lateLocal, TimeZoneInfo.Local), TimeSpan.Zero));
            GameBootstrap bootstrap = null;
            yield return Boot(clock, b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var daily = entry.DailyVisit;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            yield return OnboardedAtLevelTwo(context, entry, daily);
            var services = daily.Services;
            Assert.IsTrue(services.Gift.TryOpen(out _));
            Assert.IsTrue(services.Calendar.TryClaim(out _));
            Assert.IsFalse(services.Gift.CanOpen);
            int day = services.TodayKey;
            int logins = context.State.DailyVisit.LoginDays;

            clock.Advance(TimeSpan.FromSeconds(45));
            yield return WaitFor(() => services.TodayKey != day, 3f, "The day boundary is found during play (once a second).");
            Assert.AreEqual(DailyDay.AddDays(day, 1), services.TodayKey);
            Assert.AreEqual(logins + 1, context.State.DailyVisit.LoginDays);
            Assert.IsTrue(services.Gift.CanOpen, "A new day, a new gift.");
            Assert.IsTrue(services.Calendar.CanClaim, "And a new box to stamp.");
            yield return WaitFor(() => hud.TodayDotVisible, 2f, "The dot lights again.");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator LongAbsence_ReturnGift_OnTheOfflineCard()
        {
#if UNITY_EDITOR
            var clock = new FakeGameClock(LocalMorning());
            GameBootstrap bootstrap = null;
            yield return Boot(clock, b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var daily = entry.DailyVisit;
            yield return OnboardedAtLevelTwo(context, entry, daily);
            context.Save.FlushNow();

            // Five days away, then the app comes back (what OnApplicationPause(false) does).
            clock.Advance(TimeSpan.FromDays(5));
            var report = context.Offline.ResolveReturn();
            context.Daily.OnResume(report);
            Assert.IsTrue(context.Daily.Returns.HasPending, "Five days away: the short return gift (doc 01 section 6).");
            var offline = Object.FindAnyObjectByType<OfflineReturnScreen>();
            yield return WaitFor(() => offline.IsOpen, 4f, "The offline card opens.");
            Assert.IsTrue(offline.GiftShown, "The return gift sits under the Light.");
            Assert.Greater(offline.GiftCellsShown, 0);

            int eggs = context.State.World.DiscoveryEggs;
            entry.Refuge.CollectOffline();
            Assert.IsFalse(context.Daily.Returns.HasPending, "One « Récolter » takes the Light and the gift.");
            Assert.AreEqual(eggs + 1, context.State.World.DiscoveryEggs, "Its Discovery egg is in the stock.");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator Treat_FromTheSheet_AddsBond()
        {
#if UNITY_EDITOR
            var clock = new FakeGameClock(LocalMorning());
            GameBootstrap bootstrap = null;
            yield return Boot(clock, b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            yield return OnboardedAtLevelTwo(context, entry, entry.DailyVisit);
            var state = context.State;
            var treat = ContentId.FromSource("treat.clover_crunch");
            state.Progression.Treats[treat] = 2;
            uint animal = state.Collection[0].InstanceId;
            state.Collection.TryGet(animal, out var before);
            entry.Refuge.GiveTreat(animal);
            state.Collection.TryGet(animal, out var after);
            Assert.Greater(after.BondPoints, before.BondPoints, "The treat adds bond (doc 01 section 10.1).");
            Assert.AreEqual(1, state.Progression.Treats[treat], "One treat is eaten.");
            yield return null;
#else
            yield break;
#endif
        }

        /// <summary>A profile past the welcome at refuge level 2 (five animals hatched), Today drawn for the day.</summary>
        private static IEnumerator OnboardedAtLevelTwo(GameContext context, VerticalSliceEntry entry, DailyVisitPresenter daily)
        {
            context.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e6), EconomyReason.Debug);
            yield return Hatch(entry.Refuge, 5);
            yield return ClaimCards(entry.ProgressionScreens);
            context.State.DailyVisit.OnboardingDone = true;
            if (context.Progression.Refuge.Level < 2)
                context.State.Progression.Refuge.Level = 2;
            daily.Services.CheckDay();
            daily.Services.Refresh();
        }

        private IEnumerator Boot(IGameClock clock, Action<GameBootstrap> ready)
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
                ClockOverride = clock,
            };
#endif
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 20f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                if (Time.realtimeSinceStartup > timeout)
                    Assert.Fail("Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            ready(bootstrap);
        }

        /// <summary>Hatches like a player in a run (each hatch skipped).</summary>
        private static IEnumerator Hatch(RefugeController refuge, int count)
        {
            for (int i = 0; i < count; i++)
            {
                refuge.RequestHatch();
                refuge.RequestHatch();
                float wait = Time.realtimeSinceStartup + 8f;
                while (refuge.Hatch.IsPlaying || refuge.WaitingEgg == null)
                {
                    if (Time.realtimeSinceStartup > wait)
                        Assert.Fail($"Hatch {i} did not finish (screens open: {refuge.ModalOpen}).");
                    yield return null;
                }
            }
        }

        /// <summary>Collects any level card the run left waiting.</summary>
        private static IEnumerator ClaimCards(ProgressionPresenter presenter)
        {
            if (presenter == null || !presenter.RewardWaiting)
                yield break;
            presenter.ShowPendingRewards();
            var card = Object.FindAnyObjectByType<LevelRewardScreen>();
            yield return WaitFor(() => card.IsOpen, 2f, "The waiting card shows.");
            card.ClaimButton.onClick.Invoke();
            yield return WaitFor(() => !card.IsOpen && !presenter.IsOpen, 1f, "The card closes.");
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
