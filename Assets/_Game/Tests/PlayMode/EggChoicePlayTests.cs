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
    /// T35B, doc 02 Œufs sections 3, 5 and 6 (PlayMode, the real scene): the egg pills arrive with the Focus egg and a
    /// Discovery egg in stock, a pill or a swipe changes the egg (its name, its targets, its price or « Gratuit »), the
    /// odds window names the egg, × 3 waits for refuge level 12. Two × 10 hatches in a row: the eggs stand apart, each
    /// newborn walks out without ever entering another animal, a prop or the water (rule « aucune collision »), the
    /// summary card shows the ten newborns with their rarity and a « Nouveau » badge for each new color, and the next egg
    /// is back within moments.
    /// </summary>
    public class EggChoicePlayTests
    {
        private static readonly ContentId Grassland = ContentId.FromSource("biome.grassland");
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");
        private static readonly ContentId Focus = ContentId.FromSource("egg.grassland.focus");
        private static readonly ContentId Discovery = ContentId.FromSource("egg.discovery");
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            ForgetChoice();
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
            ForgetChoice();
        }

        private static void ForgetChoice()
        {
            PlayerPrefs.DeleteKey(EggChoice.SelectedKey);
            PlayerPrefs.DeleteKey(EggChoice.QuantityKey);
        }

        [UnityTest]
        public IEnumerator Pills_Swipe_FocusTargets_FreeDiscovery_AndQuantity()
        {
#if UNITY_EDITOR
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var refuge = entry.Refuge;
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            var bar = hud.EggChoice;
            var loc = context.Localization;
            Assert.IsNotNull(bar, "The HUD carries the egg choice.");
            Assert.AreEqual(1, refuge.Choice.Count, "A new profile has one egg: no pill.");
            Assert.IsFalse(bar.QuantityVisible, "× 3 waits for refuge level 12.");

            yield return Hatch(refuge, 1);
            context.Progression.Mastery.AddXp(Grassland, 100000);
            context.Hatch.GrantDiscoveryEggs(2, DiscoverySource.Debug);
            yield return Calm(entry);
            yield return WaitFor(() => bar.SlotsShown == 3, 2f, "Basic, Focus (biome mastery 3) and Discovery (in stock).");
            // The egg's name and the mastery toast share the space above the button: the toast of the last hatch goes first.
            yield return WaitFor(() => !hud.MasteryVisible, 5f, "The mastery toast leaves.");

            bar.Pill(1).onClick.Invoke();
            Assert.AreEqual(Focus, refuge.EggId);
            Assert.IsTrue(bar.TargetsVisible, "The Focus egg shows its three targets.");
            Assert.IsTrue(bar.ButtonTinted, "The button's egg takes the Focus egg's colors.");
            context.Content.Eggs.TryGet(Focus, out var focus);
            Assert.AreEqual(loc.Get(focus.DisplayKey), bar.NameText, "Its name shows for a moment.");
            Assert.AreEqual(context.Hatch.GetCost(Focus), refuge.EggCost);

            bar.Step(1);
            Assert.AreEqual(Discovery, refuge.EggId, "A swipe goes to the next egg.");
            Assert.AreEqual(loc.Get(EggText.Free), hud.PriceTextShown, "A Discovery egg reads « Gratuit ».");
            Assert.IsFalse(bar.TargetsVisible);

            var modal = Object.FindAnyObjectByType<ModalScreen>();
            hud.transform.Find("SafeArea/HatchAction/Info/Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            context.Content.Eggs.TryGet(Discovery, out var discovery);
            Assert.AreEqual(loc.Get(discovery.DisplayKey), modal.TitleText, "The odds window names the egg.");
            StringAssert.Contains(loc.Get(EggText.OddsDiscovery), modal.BodyText, "Its rules, then its table.");
            modal.ConfirmButton.onClick.Invoke();
            yield return WaitFor(() => !refuge.ModalOpen, 1f, "The window closes.");

            bar.Step(1);
            Assert.AreEqual(Basic, refuge.EggId, "The swipe wraps around.");
            Assert.IsNull(hud.PriceTextShown, "The Light price is back.");

            context.State.Progression.Refuge.Level = 12;
            yield return WaitFor(() => bar.QuantityVisible, 2f, "× 3 arrives at refuge level 12.");
            bar.QuantityButton.onClick.Invoke();
            Assert.AreEqual(3, refuge.Choice.Quantity);
            Assert.AreEqual("× 3", bar.QuantityText);
            Assert.AreEqual(context.Hatch.GetCost(Basic, 3), refuge.EggCost, "The price of the next three.");
            bar.QuantityButton.onClick.Invoke();
            Assert.AreEqual(1, refuge.Choice.Quantity, "× 10 waits for level 25 and the first Renewal: back to × 1.");
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TenEggs_Twice_WalkOutWithoutOverlap_SummaryShowsTheNews()
        {
#if UNITY_EDITOR
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var refuge = entry.Refuge;
            var scheduler = refuge.Scheduler;
            var summary = Object.FindAnyObjectByType<RewardSummaryScreen>();
            Assert.IsNotNull(summary, "PF_UI_RewardSummary in the vertical slice.");

            context.Economy.Grant(CurrencyId.Light, BigValue.FromDouble(1e15), EconomyReason.Debug);
            yield return Hatch(refuge, 1);
            context.State.Progression.Refuge.Level = 25;
            context.State.World.RenewalCount = 1;
            yield return Calm(entry);
            yield return WaitFor(() => refuge.Choice.MaxQuantity == 10, 2f, "× 10 at level 25 after a Renewal.");
            while (refuge.Choice.Quantity != 10)
                refuge.CycleHatchQuantity();

            for (int run = 0; run < 2; run++)
            {
                // Ten hatches may bring a level card: it is collected first.
                yield return Calm(entry);
                yield return WaitFor(() => refuge.WaitingEgg != null && !refuge.ModalOpen, 4f, $"Run {run}: the egg is back.");
                int variants = context.State.Collection.DiscoveredVariants.Count;
                int animals = context.State.Collection.Count;
                refuge.RequestHatch();
                yield return WaitFor(() => refuge.Multi.IsPlaying, 5f, $"Run {run}: the ten eggs find their places.");

                float worst = AnimalScheduler.ClearanceHorizon;
                string where = string.Empty;
                float until = Time.realtimeSinceStartup + 40f;
                while (refuge.Multi.IsPlaying)
                {
                    if (Time.realtimeSinceStartup > until)
                        Assert.Fail($"Run {run}: the staging did not end.");
                    float newborns = refuge.MultiClearance();
                    if (newborns < worst)
                    {
                        worst = newborns;
                        where = Describe(refuge, "newborns");
                    }
                    for (int i = 0; i < scheduler.Count; i++)
                    {
                        int handle = scheduler.GetActiveHandle(i);
                        float clearance = scheduler.Clearance(handle);
                        if (clearance < worst)
                        {
                            worst = clearance;
                            where = Describe(refuge, $"meadow animal at {scheduler.GetPosition(handle)}");
                        }
                    }
                    yield return null;
                }
                // Then the newborns walk out as animals of the meadow: every step checked against every animal.
                float walk = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < walk)
                {
                    for (int i = 0; i < scheduler.Count; i++)
                    {
                        int handle = scheduler.GetActiveHandle(i);
                        float clearance = scheduler.Clearance(handle);
                        if (clearance < worst)
                        {
                            worst = clearance;
                            where = $"walking out, animal at {scheduler.GetPosition(handle)}";
                        }
                    }
                    yield return null;
                }
                Assert.GreaterOrEqual(worst, -AnimalScheduler.ContactTolerance - 1e-4f,
                    $"Run {run}: no newborn enters another animal, a prop or the water ({worst * 100f:F1} cm, {where}).");
                Assert.AreEqual(animals + 10, context.State.Collection.Count, "Ten hatches, all paid at once.");

                float shows = Time.realtimeSinceStartup + 1f;
                while (!summary.IsOpen)
                {
                    if (Time.realtimeSinceStartup > shows)
                        Assert.Fail($"Run {run}: the summary card shows (summary {refuge.SummaryOpen}, cells {summary.CellsShown}, " +
                            $"modal {refuge.ModalOpen}, shop {refuge.ShopOpen}, progression {refuge.ProgressionOpen}, offline {refuge.OfflineScreenOpen}, " +
                            $"playing {refuge.Multi.IsPlaying}, count {refuge.Multi.Count}, animals {context.State.Collection.Count - animals}).");
                    yield return null;
                }
                Assert.IsTrue(refuge.ModalOpen, "Nothing hatches under the card.");
                Assert.AreEqual(10, summary.CellsShown);
                int news = 0;
                for (int i = 0; i < summary.CellsShown; i++)
                {
                    if (summary.NewShown(i))
                        news++;
                    Assert.IsNotEmpty(summary.NameText(i));
                    Assert.IsNotEmpty(summary.RarityText(i), "Each rarity has its word.");
                    Assert.That(summary.StarsShown(i), NUnit.Framework.Is.InRange(1, RewardSummaryScreen.MaxStars), "And its stars.");
                }
                Assert.AreEqual(context.State.Collection.DiscoveredVariants.Count - variants, news, "One « Nouveau » badge per new color.");
                summary.ContinueButton.onClick.Invoke();
                yield return WaitFor(() => !refuge.SummaryOpen, 1f, "The button closes the card.");

                // The newborns stood beyond the egg's apron: the next egg needs no long wait.
                yield return WaitFor(() => refuge.WaitingEgg != null, 3f, $"Run {run}: the next egg is back within moments.");
            }
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        /// <summary>Where the staging was when the smallest gap was seen (failure messages only).</summary>
        private static string Describe(RefugeController refuge, string what)
        {
            var multi = refuge.Multi;
            var text = new System.Text.StringBuilder(what);
            text.Append($"; t {multi.Elapsed:F2} s, playing {multi.IsPlaying}, burst {multi.Burst}, moment {multi.CurrentMoment}; newborns");
            for (int i = 0; i < multi.Count; i++)
            {
                var animal = multi.Animal(i);
                if (animal != null)
                    text.Append($" {animal.name}@{animal.transform.position} x{animal.transform.localScale.x:F2}");
            }
            return text.ToString();
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
            // A cold first boot (the fixture run alone, right after a domain reload) compiles shaders and loads every bundle.
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

        /// <summary>Hatches like a player (each hatch skipped).</summary>
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

        /// <summary>Collects the level cards the jumps in mastery or level brought, until no screen covers the refuge.</summary>
        private static IEnumerator Calm(VerticalSliceEntry entry)
        {
            var presenter = entry.ProgressionScreens;
            var card = Object.FindAnyObjectByType<LevelRewardScreen>();
            var offer = Object.FindAnyObjectByType<OfferModalView>();
            var refuge = entry.Refuge;
            float until = Time.realtimeSinceStartup + 6f;
            while (refuge.ModalOpen || (presenter != null && presenter.RewardWaiting))
            {
                if (Time.realtimeSinceStartup > until)
                    Assert.Fail($"A screen stays open (shop {refuge.ShopOpen}, dialog {refuge.DialogOpen}, sheet {refuge.SheetOpen}, " +
                        $"codex {refuge.CodexOpen}, offline {refuge.OfflineScreenOpen}, progression {refuge.ProgressionOpen}, summary {refuge.SummaryOpen}).");
                // A happy moment may bring the Starter Pack's offer: declined here.
                if (offer != null && offer.IsOpen)
                {
                    foreach (var button in offer.GetComponentsInChildren<UnityEngine.UI.Button>())
                    {
                        if (button.name == "Close")
                        {
                            button.onClick.Invoke();
                            break;
                        }
                    }
                }
                if (presenter != null && presenter.RewardWaiting && !presenter.IsOpen)
                    presenter.ShowPendingRewards();
                if (card != null && card.IsOpen)
                    card.ClaimButton.onClick.Invoke();
                yield return null;
            }
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
