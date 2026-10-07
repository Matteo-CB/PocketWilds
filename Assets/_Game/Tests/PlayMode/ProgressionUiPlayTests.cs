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
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T35A, doc 02 Améliorations section 11 (PlayMode): the progression screens in the real scene. The Upgrades button
    /// arrives after the third hatch with its dot and tutorial paw; buying works from the cards (×1, ×10, locked until
    /// mastery, Cozy Nests lowers the egg's price at once); the sign opens the zone window and its button opens the
    /// zone; the level card waits for a calm moment and pays its Gems; the badge opens the refuge level; the Forest's
    /// gate reads Coming soon. Steady ticks allocate nothing.
    /// </summary>
    public class ProgressionUiPlayTests
    {
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Meadow = ContentId.FromSource("zone.grassland.meadow");
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
                Directory.Delete(_saveDir, true);
            }
            catch (IOException)
            {
            }
        }

        [UnityTest]
        public IEnumerator UpgradesButton_AfterThreeHatches_CardsBuy_NestsLowerTheEggPrice()
        {
#if UNITY_EDITOR
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var refuge = entry.Refuge;
            var presenter = entry.ProgressionScreens;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            Assert.IsNotNull(presenter, "The scene runs the progression screens.");
            Assert.IsFalse(hud.UpgradeVisible, "No Upgrades button on a new profile.");

            context.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e6), EconomyReason.Debug);
            yield return Hatch(refuge, presenter, 3);
            yield return WaitFor(() => hud.UpgradeVisible, 2f, "The Upgrades button arrives after the third hatch.");
            Assert.IsTrue(hud.UpgradeDotVisible, "Its dot: one level of Production is worth +10% of the production.");
            yield return ClaimCards(presenter);
            yield return WaitFor(() => hud.UpgradeHintShown, 4f, "The tutorial paw taps on the new button.");

            hud.UpgradeButton.onClick.Invoke();
            var screen = Object.FindAnyObjectByType<UpgradesScreen>();
            Assert.IsTrue(screen.IsOpen, "The button opens the Upgrades screen.");
            Assert.IsTrue(refuge.ModalOpen, "World touches and hatching wait while it is open.");
            yield return null;
            Assert.IsFalse(hud.UpgradeHintShown, "The paw leaves once the screen was opened.");

            // Production ×1: 5 B = 50 Light for +10%.
            var production = screen.GetCard(0);
            Assert.AreEqual(UpgradeCardState.Affordable, production.State);
            Assert.IsTrue(production.BestShown, "The only affordable production card is the best pick.");
            var light = context.Economy.Light;
            var rate = context.Economy.GetLightPerSecond();
            production.BuyButton.onClick.Invoke();
            Assert.AreEqual(1, context.Progression.Upgrades.GetLevel(Grassland, UpgradeKind.Production));
            Assert.AreEqual(50.0, BigValue.Subtract(light, context.Economy.Light).ToDoubleClamped(), 1e-6, "One level costs 5 B.");
            Assert.AreEqual(1.1, BigValue.Divide(context.Economy.GetLightPerSecond(), rate).ToDoubleClamped(), 1e-6, "+10% Light.");
            Assert.IsTrue(production.Animating, "The card bounces.");

            // ×10: ten levels priced together.
            screen.QtyButton(1).onClick.Invoke();
            Assert.AreEqual(PurchaseQty.Ten, presenter.Qty);
            Assert.AreEqual(10, presenter.GetCard(0).Levels);
            var expected = ProgressionMath.GeometricTotal(context.Progression.Upgrades.LevelPrice(Grassland, UpgradeKind.Production, 1), 1.25, 10);
            Assert.IsTrue(BigValue.ApproximatelyEqual(expected, presenter.GetCard(0).Price, 1e-9), "×10 shows the total price.");
            screen.QtyButton(0).onClick.Invoke();

            // Cozy Nests waits for mastery 2 (lucky first hatches may already bring it), then lowers the egg's price at once.
            if (context.Progression.Mastery.GetLevel(Grassland) < 2)
                Assert.AreEqual(UpgradeCardState.Locked, screen.GetCard(1).State, "Cozy Nests waits for mastery 2.");
            context.Progression.Mastery.AddXp(Grassland, 500);
            yield return WaitFor(() => screen.GetCard(1).State == UpgradeCardState.Affordable, 1f, "Cozy Nests opens with mastery 2.");
            var egg = refuge.EggCost;
            screen.GetCard(1).BuyButton.onClick.Invoke();
            // Egg prices are whole numbers: a 4% cut of a small price shows as one Light less.
            Assert.IsTrue(refuge.EggCost < egg, "The egg costs less right away.");
            Assert.AreEqual(context.Hatch.GetCost(ContentId.FromSource("egg.grassland.basic")), refuge.EggCost, "The HUD's cached price follows.");

            // Not enough Light: nothing is spent, the card shakes.
            context.Economy.Spend(CurrencyId.Light, context.Economy.Light, EconomyReason.Debug);
            yield return WaitFor(() => screen.GetCard(0).State == UpgradeCardState.NotEnough, 1f, "The price turns soft.");
            int level = context.Progression.Upgrades.GetLevel(Grassland, UpgradeKind.Production);
            screen.GetCard(0).BuyButton.onClick.Invoke();
            Assert.AreEqual(level, context.Progression.Upgrades.GetLevel(Grassland, UpgradeKind.Production));
            Assert.IsTrue(screen.GetCard(0).Animating, "A soft shake says not yet.");

            screen.CloseButton.onClick.Invoke();
            yield return WaitFor(() => !refuge.ModalOpen, 1f, "The screen closes.");

            // Steady state (the mastery's level card collected, every window done closing): the presenter's checks
            // allocate nothing.
            yield return ClaimCards(presenter);
            yield return new WaitForSecondsRealtime(0.5f);
            float t = Time.time;
            Assert.That(() =>
            {
                for (int i = 0; i < 120; i++)
                {
                    t += 1f / 60f;
                    presenter.Tick(t, 1f / 60f);
                }
            }, Is.Not.AllocatingGCMemory());
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SignOpensTheZoneWindow_LevelCardPays_BadgeAndGateScreens()
        {
#if UNITY_EDITOR
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var refuge = entry.Refuge;
            var presenter = entry.ProgressionScreens;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            context.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e6), EconomyReason.Debug);

            // Hatching levels the refuge up: the badge celebrates, the card waits (a dot) until a calm moment.
            yield return Hatch(refuge, presenter, 3);
            Assert.Greater(context.Progression.Refuge.Level, 1, "Three hatches reach refuge level 2.");
            Assert.IsTrue(presenter.RewardWaiting, "A level card waits after the level up.");
            var card = Object.FindAnyObjectByType<LevelRewardScreen>();
            Assert.IsFalse(card.IsOpen, "The card never cuts a hatching run.");
            yield return null;
            Assert.IsTrue(hud.LevelRewardDotVisible, "A dot on the badge says a reward waits.");
            yield return WaitFor(() => card.IsOpen, ProgressionPresenter.RewardIdleSeconds + 2f, "The card comes in a calm moment.");
            long gems = context.State.Wallet.Gems;
            var pending = context.Progression.Refuge.PendingRewards();
            card.ClaimButton.onClick.Invoke();
            Assert.AreEqual(gems + pending.Gems, context.State.Wallet.Gems, "The card pays its Gems.");
            yield return WaitFor(() => !refuge.ModalOpen, 1f, "The card closes.");
            Assert.IsFalse(hud.LevelRewardDotVisible);

            // The badge opens the refuge level screen.
            hud.LevelButton.onClick.Invoke();
            var level = Object.FindAnyObjectByType<RefugeLevelScreen>();
            Assert.IsTrue(level.IsOpen, "The badge opens the refuge level screen.");
            Assert.AreEqual(context.Progression.Refuge.Level.ToString(), level.LevelText);
            Assert.GreaterOrEqual(level.SourcesShown, 1, "Species at least.");
            Assert.AreEqual(RefugeLevelData.MaxTrack, level.TrackShown);
            Assert.IsTrue(level.TrackReached(0), "The track starts with the level reached.");
            presenter.CloseRefugeLevel();
            yield return WaitFor(() => !refuge.ModalOpen, 1f, "The level screen closes.");

            // The meadow's sign opens its window; with mastery 1 and the Light, its button opens the zone.
            yield return WaitFor(() => entry.Zones.Boundary.IsPlaced, 10f, "The hedge stands.");
            context.Progression.Mastery.AddXp(Grassland, 40);
            var camera = entry.CameraRig.Camera;
            var sign = entry.Zones.Boundary.SignPosition + new Vector3(0f, 0.6f, 0f);
            Assert.IsTrue(entry.Zones.TryTap(new Ray(camera.transform.position, (sign - camera.transform.position).normalized)), "The sign takes the touch.");
            var window = Object.FindAnyObjectByType<ZoneOpenScreen>();
            Assert.IsTrue(window.IsOpen, "The sign opens the zone window.");
            Assert.AreEqual(presenter.BuildZoneData(context.Progression.Config.Zones[1]).Title, window.TitleText, "Open the Flower Meadow.");
            Assert.IsTrue(window.CanOpen, $"The meadow can open (state {context.Progression.Zones.GetState(context.Progression.Config.Zones[1])}).");
            window.OpenButton.onClick.Invoke();
            Assert.IsTrue(context.Progression.Zones.IsOpen(context.Progression.Config.Zones[1]), "Its button opens the zone.");
            Assert.AreEqual(Meadow, context.Progression.Config.Zones[1].Id);
            yield return WaitFor(() => !window.IsOpen, 1f, "The window steps aside for the opening.");

            // The Forest's gate: its conditions with their ticks, and Coming soon (no Forest content yet).
            presenter.ShowBiomeGate(ContentId.FromSource("biome.forest"));
            var gate = Object.FindAnyObjectByType<BiomeGateScreen>();
            Assert.IsTrue(gate.IsOpen, $"The gate window opens (screens open: {refuge.ModalOpen}, presenter {presenter.IsOpen}: " +
                $"upgrades {Object.FindAnyObjectByType<UpgradesScreen>().IsOpen}, zone {window.IsOpen}, level {level.IsOpen}, card {card.IsOpen}).");
            Assert.IsTrue(gate.SoonShown, "No Forest species in this build: Coming soon.");
            Assert.AreEqual(4, gate.ConditionsShown, "Level, species, mastery and play time.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
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

        /// <summary>Hatches like a player in a run (each hatch skipped), never pausing long enough for a level card.</summary>
        private static IEnumerator Hatch(RefugeController refuge, ProgressionPresenter presenter, int count)
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
            if (!presenter.RewardWaiting)
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
