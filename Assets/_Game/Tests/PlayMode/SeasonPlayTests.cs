using System;
using System.Collections;
using System.Diagnostics;
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
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T48B seasons and weekends on screen: a season change (a new Remote Config month map fires Changed) rewrites only the
    /// changed palette rows, swaps the atmosphere tint and the drift without a reload, at a measured near-zero cost; the drift is
    /// one draw with 0 B per frame within the tier caps; Soleil evening fireflies never enter a volume; the season card shows
    /// once and never during a hatch; the weekend chip shows only during a weekend. The services are faked through
    /// <see cref="ISeasonView"/> and <see cref="IWeekendView"/> (GameContext.Seasons and Weekends have setters).
    /// </summary>
    public class SeasonPlayTests
    {
        private const string LooksPath = "Assets/_Game/Content/World/DEF_SeasonLooks.asset";
        private string _saveDir;
        private FakeGameClock _clock;

        private sealed class FakeSeasons : ISeasonView
        {
            public RefugeSeason Current { get; set; } = RefugeSeason.Sun;
            public event Action<RefugeSeason> Changed;
            public bool CardPending { get; set; }
            public int Marks;

            public void MarkCardShown()
            {
                Marks++;
                CardPending = false;
            }

            public void Set(RefugeSeason season)
            {
                Current = season;
                Changed?.Invoke(season);
            }
        }

        private sealed class FakeWeekends : IWeekendView
        {
            public WeekendKind Active { get; private set; }
            public long EndsUnix { get; private set; }
            public event Action<WeekendKind> Changed;
            public int Seen;

            public void MarkSeen() => Seen++;

            public void Set(WeekendKind kind, long endsUnix)
            {
                Active = kind;
                EndsUnix = kind == WeekendKind.None ? 0 : endsUnix;
                Changed?.Invoke(kind);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "PocketWildsPlayTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            _clock = new FakeGameClock(new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero));
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

        // ---------------- without boot ----------------

        [Test]
        public void SeasonSwitch_RewritesOnlyChangedRows_FastAndWithoutGarbage()
        {
            var looks = Looks();
            var table = new PaletteTable(Registry());
            try
            {
                var palettes = new SeasonPalettes(table, looks);
                Assert.Greater(palettes.RowCount, 0, "Every non-Soleil season changes vegetation rows.");
                Assert.AreEqual(0, palettes.MissingPalettes, "Every authored row names a palette of the table.");
                Assert.IsTrue(table.TryGetRow(ContentId.FromSource("palette.grassland.rock"), out int rock));
                Assert.IsTrue(table.TryGetRow(ContentId.FromSource("palette.grassland.grass"), out int grass));
                var rockBase = Row(table, rock);
                var grassBase = Row(table, grass);
                palettes.Apply(RefugeSeason.Frost);
                palettes.Apply(RefugeSeason.Sun);

                var watch = new Stopwatch();
                long before = GC.GetAllocatedBytesForCurrentThread();
                watch.Start();
                int rows = palettes.Apply(RefugeSeason.Harvest);
                watch.Stop();
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Debug.Log($"[T48B] season switch: {watch.Elapsed.TotalMilliseconds:F3} ms, {bytes} B, {rows} rows written of {palettes.RowCount} seasonal ({table.RowCount} in the table)");
                Assert.Greater(rows, 0);
                Assert.LessOrEqual(rows, palettes.RowCount, "Only seasonal rows are written.");
                Assert.AreEqual(0, bytes, "A season switch allocates nothing.");
                Assert.Less(watch.Elapsed.TotalMilliseconds, 4.0, "A season switch costs a few rows and one upload.");
                CollectionAssert.AreEqual(rockBase, Row(table, rock), "Rocks never change.");
                CollectionAssert.AreNotEqual(grassBase, Row(table, grass), "The grass wears the season.");
                Assert.AreEqual(0, palettes.Apply(RefugeSeason.Harvest), "The same season writes nothing again.");
                palettes.Apply(RefugeSeason.Sun);
                CollectionAssert.AreEqual(grassBase, Row(table, grass), "Soleil is the validated base look.");
            }
            finally
            {
                table.Dispose();
            }
        }

        [Test]
        public void RemoteMapChange_SwitchesRowsLightAndDrift_WithoutReload()
        {
            var looks = Looks();
            var table = new PaletteTable(Registry());
            var day = ScriptableObject.CreateInstance<LightingProfile>();
            var tier = WorldQualityProfile.Default(QualityTier.High);
            var atmosphere = new WorldAtmosphere(null, day, null, null, null, tier);
            var view = new FakeSeasons { Current = RefugeSeason.Sun };
            var presenter = new SeasonPresenter(view, table, looks, atmosphere, null, null, tier);
            try
            {
                Assert.AreEqual(RefugeSeason.Sun, presenter.Shown);
                Assert.IsNotNull(presenter.Drift, "DEF_SeasonLooks carries the quad and the drift material.");
                Assert.AreEqual(0, presenter.Drift.Count, "No drift in Soleil.");
                atmosphere.Apply(8f);
                var sunFog = RenderSettings.fogColor;
                var sunAmbient = RenderSettings.ambientSkyColor;

                // A valid month map applied at runtime (or the first of a season month): the service raises Changed.
                view.Set(RefugeSeason.Frost);
                Assert.AreEqual(RefugeSeason.Frost, presenter.Shown, "The look follows Changed at once.");
                Assert.Greater(presenter.LastRowsWritten, 0);
                Assert.AreEqual(SeasonDriftKind.Snow, presenter.Drift.Kind);
                Assert.Greater(presenter.Drift.Count, 0);
                atmosphere.Apply(8f);
                Assert.AreNotEqual(sunFog, RenderSettings.fogColor, "Givre's light differs (pink morning, cooler fog).");
                Assert.AreNotEqual(sunAmbient, RenderSettings.ambientSkyColor);
                Debug.Log($"[T48B] RC switch to Givre: {presenter.LastSwitchMilliseconds:F3} ms, {presenter.LastRowsWritten} rows");

                view.Set(RefugeSeason.Harvest);
                Assert.AreEqual(SeasonDriftKind.Leaves, presenter.Drift.Kind);
                view.Set(RefugeSeason.Awakening);
                Assert.AreEqual(SeasonDriftKind.Petals, presenter.Drift.Kind);
                presenter.ForceSeason(RefugeSeason.Sun);
                Assert.AreEqual(RefugeSeason.Sun, presenter.Shown, "Captures force a season over the service's.");
                Assert.IsTrue(presenter.Fireflies);
            }
            finally
            {
                presenter.Dispose();
                table.Dispose();
                Object.DestroyImmediate(day);
            }
        }

        [Test]
        public void Drift_IsOneDraw_ZeroBytesPerFrame_WithinTierCaps()
        {
            var looks = Looks();
            var go = new GameObject("DriftCamera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 6f, -8f);
                camera.transform.LookAt(Vector3.zero);
                foreach (var (tier, cap) in new[] { (QualityTier.High, 160), (QualityTier.Medium, 112), (QualityTier.Low, 64) })
                {
                    var drift = new SeasonDrift(looks, WorldQualityProfile.Default(tier));
                    foreach (var season in new[] { RefugeSeason.Awakening, RefugeSeason.Harvest, RefugeSeason.Frost })
                    {
                        drift.SetLook(looks.Get(season));
                        drift.Render(camera, Vector3.zero);
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        for (int frame = 0; frame < 120; frame++)
                            drift.Render(camera, Vector3.zero);
                        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                        Assert.AreEqual(0, bytes, $"{tier} {season}: the drift allocates nothing per frame.");
                        Assert.AreEqual(1, drift.LastDraws, $"{tier} {season}: one instanced draw.");
                        Assert.Greater(drift.LastInstances, 0);
                        Assert.LessOrEqual(drift.LastInstances, cap, $"{tier} {season}: capped at {cap}.");
                        Debug.Log($"[T48B] drift {tier} {season}: {drift.LastInstances} instances, 1 draw, 0 B");
                    }
                    drift.SetLook(looks.Get(RefugeSeason.Sun));
                    drift.Render(camera, Vector3.zero);
                    Assert.AreEqual(0, drift.LastDraws, "Soleil draws no drift.");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------- in the game ----------------

        [UnityTest]
        public IEnumerator Fireflies_SoleilEvening_NeverInsideAVolume()
        {
            PrepareSave(null);
            yield return Boot();
            var entry = Entry;
            Assert.IsNotNull(entry.Seasons, "The vertical slice runs the season presenter.");
            Assert.IsNotNull(entry.Life);
            entry.Atmosphere.ForceHour(21f);
            entry.Seasons.ForceSeason(RefugeSeason.Sun);
            yield return Wait(1.6f);
            Assert.Greater(entry.Life.ActiveFireflies, 0, "Soleil evening: fireflies are out (DEF_SeasonLooks wired?).");
            float worst = float.MaxValue;
            string where = null;
            float until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until)
            {
                float clearance = entry.Life.MinClearance(out string closest);
                if (clearance < worst)
                {
                    worst = clearance;
                    where = closest;
                }
                yield return null;
            }
            Assert.GreaterOrEqual(worst, -0.002f, "A firefly or an insect entered a volume: " + where);
            entry.Seasons.ForceSeason(RefugeSeason.Frost);
            yield return Wait(1.2f);
            Assert.AreEqual(0, entry.Life.ActiveFireflies, "No fireflies outside Soleil.");
            entry.Atmosphere.ForceHour(13f);
            entry.Seasons.ForceSeason(RefugeSeason.Sun);
            yield return Wait(1.6f);
            Assert.AreEqual(0, entry.Life.ActiveFireflies, "No fireflies in daylight.");
        }

        [UnityTest]
        public IEnumerator SeasonCard_ShownOnce_NeverDuringAHatch()
        {
            PrepareSave(null);
            yield return Boot();
            var context = Context;
            var refuge = Refuge;
            yield return ClearPopups(refuge);
            var seasons = new FakeSeasons { Current = RefugeSeason.Harvest };
            context.Seasons = seasons;
            int shownBefore = refuge.SeasonCardsShown;

            float deadline = Time.realtimeSinceStartup + 8f;
            while (!refuge.Hatching && Time.realtimeSinceStartup < deadline)
            {
                refuge.RequestHatch();
                yield return Wait(0.3f);
            }
            Assert.IsTrue(refuge.Hatching, "A hatch is playing.");
            seasons.CardPending = true;
            while (refuge.Hatching)
            {
                Assert.AreEqual(shownBefore, refuge.SeasonCardsShown, "Never over a hatch.");
                yield return null;
            }
            deadline = Time.realtimeSinceStartup + 20f;
            while (refuge.SeasonCardsShown == shownBefore && Time.realtimeSinceStartup < deadline)
            {
                // A level card or a reveal may come first: the season card waits for a calm refuge.
                yield return ClearPopups(refuge);
                yield return Wait(0.5f);
            }
            Assert.AreEqual(shownBefore + 1, refuge.SeasonCardsShown, "The card shows once the refuge is calm.");
            Assert.AreEqual(1, seasons.Marks);
            Assert.IsTrue(refuge.DialogOpen);
            CloseModal(refuge);
            yield return Wait(3f);
            Assert.AreEqual(shownBefore + 1, refuge.SeasonCardsShown, "Once per season.");
            Assert.AreEqual(1, seasons.Marks);
        }

        [UnityTest]
        public IEnumerator WeekendChip_ShowsOnlyDuringAWeekend()
        {
            PrepareSave(null);
            yield return Boot();
            var context = Context;
            var refuge = Refuge;
            yield return ClearPopups(refuge);
            var hud = Object.FindAnyObjectByType<RefugeHud>();
            var weekends = new FakeWeekends();
            context.Weekends = weekends;
            yield return Wait(1.3f);
            Assert.IsFalse(hud.WeekendVisible, "No chip outside a weekend.");

            long now = _clock.UtcNow.ToUnixTimeSeconds();
            weekends.Set(WeekendKind.Eggs, now + 2 * 86400 + 5 * 3600);
            yield return Wait(1.3f);
            Assert.IsTrue(hud.WeekendVisible, "The chip shows during a weekend.");
            Assert.AreEqual((int)WeekendKind.Eggs, hud.WeekendKindShown);
            Assert.GreaterOrEqual(weekends.Seen, 1, "weekend_seen when it shows.");
            Assert.AreEqual(WeekendKind.Eggs, refuge.WeekendShown);

            hud.WeekendButton.onClick.Invoke();
            yield return Wait(0.6f);
            Assert.IsTrue(refuge.DialogOpen, "A tap opens the weekend's sheet.");
            CloseModal(refuge);
            yield return Wait(0.6f);

            weekends.Set(WeekendKind.None, 0);
            yield return Wait(1.3f);
            Assert.IsFalse(hud.WeekendVisible, "The chip leaves with the weekend.");
        }

        [UnityTest, Explicit("T48B review captures (fr): the 4 seasons of the same refuge view, fireflies, the weekend chip and sheet, the season card.")]
        public IEnumerator Capture_Seasons_Fr()
        {
            PrepareSave("fr");
            yield return Boot();
            var context = Context;
            var refuge = Refuge;
            var entry = Entry;
            yield return Wait(2f);
            yield return ClearPopups(refuge);
            foreach (var season in new[] { RefugeSeason.Awakening, RefugeSeason.Sun, RefugeSeason.Harvest, RefugeSeason.Frost })
            {
                entry.Seasons.ForceSeason(season);
                foreach (float hour in new[] { 8.5f, 13f })
                {
                    entry.Atmosphere.ForceHour(hour);
                    yield return Wait(2.5f);
                    yield return Shot(season + "_" + (hour < 10f ? "Morning" : "Day"));
                }
            }
            entry.Seasons.ForceSeason(RefugeSeason.Sun);
            entry.Atmosphere.ForceHour(21f);
            yield return Wait(4f);
            yield return Shot("Sun_Evening_Fireflies");
            entry.Atmosphere.ForceHour(13f);

            var weekends = new FakeWeekends();
            context.Weekends = weekends;
            weekends.Set(WeekendKind.Eggs, _clock.UtcNow.ToUnixTimeSeconds() + 2 * 86400 + 5 * 3600);
            yield return Wait(1.5f);
            yield return Shot("WeekendChip");
            refuge.OpenWeekend();
            yield return Wait(1f);
            yield return Shot("WeekendSheet");
            CloseModal(refuge);
            weekends.Set(WeekendKind.None, 0);
            yield return Wait(1.5f);

            var seasons = new FakeSeasons { Current = RefugeSeason.Frost, CardPending = true };
            entry.Seasons.ForceSeason(RefugeSeason.Frost);
            context.Seasons = seasons;
            yield return Wait(2f);
            yield return Shot("SeasonCard");
            CloseModal(refuge);
            entry.Seasons.ForceSeason(RefugeSeason.None);
        }

        // ---------------- helpers ----------------

        private static SeasonLookSet Looks()
        {
#if UNITY_EDITOR
            var looks = UnityEditor.AssetDatabase.LoadAssetAtPath<SeasonLookSet>(LooksPath);
            if (looks == null)
                Assert.Ignore("Run Pocket Wilds/World/Build Season Looks (T48B) first.");
            return looks;
#else
            Assert.Ignore("Editor only.");
            return null;
#endif
        }

        private static ContentRegistry Registry()
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            return database.CreateBuilder().Build();
#else
            Assert.Ignore("Editor only.");
            return null;
#endif
        }

        private static uint[] Row(PaletteTable table, int row)
        {
            var colors = new Color32[PaletteTable.Columns];
            table.ReadRow(row, colors);
            return colors.Select(c => (uint)c.r | (uint)c.g << 8 | (uint)c.b << 16 | (uint)c.a << 24).ToArray();
        }

        private void PrepareSave(string language)
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(), _clock,
                new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            string[] animals = { "rabbit.brown", "squirrel.red", "hedgehog.brown", "fawn.chestnutspots" };
            for (int i = 0; i < animals.Length; i++)
            {
                var species = ContentId.FromSource("species." + animals[i].Substring(0, animals[i].IndexOf('.')));
                var variant = ContentId.FromSource("variant." + animals[i]);
                state.Collection.Add(new AnimalInstance { Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = (uint)i + 48u });
                state.Collection.DiscoveredSpecies.Add(species);
                state.Collection.DiscoveredVariants.Add(variant);
            }
            state.Codex.SeenSpecies.UnionWith(state.Collection.DiscoveredSpecies);
            state.Codex.SeenVariants.UnionWith(state.Collection.DiscoveredVariants);
            state.Wallet.Light = BigValue.FromMantissaExponent(1.0, 12);
            if (language != null)
                state.Settings.LanguageCode = language;
            Assert.IsTrue(service.FlushNow());
            service.Dispose();
        }

        private IEnumerator Boot()
        {
#if UNITY_EDITOR
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            GameBootstrap.PendingOptions = new BootOptions
            {
                SaveDirectory = _saveDir,
                ContentOverride = database.CreateBuilder().Build(),
                VisualCatalogOverride = catalog,
                LocalizationOverride = database.localization.ToArray(),
                StoreProviderOverride = new SandboxStoreProvider("fr", 0),
                ClockOverride = _clock,
                LoadTargetScene = true,
                MuteAudio = true,
            };
            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            float timeout = Time.realtimeSinceStartup + 60f;
            while (bootstrap.State == BootState.NotStarted || bootstrap.State == BootState.Running)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "Boot timed out.");
                yield return null;
            }
            Assert.AreEqual(BootState.Ready, bootstrap.State);
            yield return Wait(1.5f);
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        /// <summary>Claims the level cards and closes a moment offer or a modal, as a player would.</summary>
        private static IEnumerator ClearPopups(RefugeController refuge)
        {
            var presenter = Entry.ProgressionScreens;
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

        private static void CloseModal(RefugeController refuge) =>
            (typeof(RefugeController).GetField("_modalView", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(refuge) as IModalView)?.Close();

        private static VerticalSliceEntry Entry => Object.FindAnyObjectByType<VerticalSliceEntry>();
        private static RefugeController Refuge => Entry.Refuge;
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
            File.WriteAllBytes(Path.Combine(folder, "T48B_fr_" + name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
