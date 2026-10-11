using System;
using System.Linq;
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
    /// T42 « Expéditions », the screen in the real game (doc 01 Contenu section 7, A18 and A19; doc 05 sheet 23), over a
    /// scripted IExpeditions (its clock advanced by the test) and a recording postcard renderer: the screen loads from its
    /// Addressables group over a hidden refuge, references nothing of the scene (fonts, kit, codex thumbnails borrowed);
    /// A18's paw on Prés fleuris, on an animal, on « Partir », the first stroll's single chip; « Partir » sends the team (the
    /// refuge lets it go), the slot counts down « De retour dans 3 min » with no allocation per frame; « Finir maintenant »
    /// brings it back (A19: the slot glows, the paw on it); the welcome collects once, shows the world while the team walks
    /// in, asks for the postcard of that return, pivots it in, then shows the loot in the result card with « Repartir »;
    /// « Repartir » opens the sheet with the same team and destination; closing releases the screen and brings the hub back;
    /// a team coming back while the player is in the refuge shows the toast « Expédition de retour ».
    /// </summary>
    public class ExpeditionPlayTests
    {
        private static readonly (string species, string variant)[] Animals =
        {
            ("species.rabbit", "variant.rabbit.brown"), ("species.hedgehog", "variant.hedgehog.brown"), ("species.squirrel", "variant.squirrel.red"),
            ("species.rabbit", "variant.rabbit.white"), ("species.squirrel", "variant.squirrel.grey"), ("species.hedgehog", "variant.hedgehog.brown"),
            ("species.rabbit", "variant.rabbit.brown"), ("species.squirrel", "variant.squirrel.red"),
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
                TestSaveDir.Release(_saveDir);
            }
            catch (IOException)
            {
            }
        }

        [UnityTest]
        public IEnumerator Expeditions_SendFinishAndWelcome_OnTheirScreen()
        {
            SeedAnimals();
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var entry = Object.FindAnyObjectByType<VerticalSliceEntry>();
            var activities = entry.Activities;
            Assert.IsNotNull(activities, "The scene runs the activities.");
            yield return ClearScreens(entry);

            var previous = activities.Expeditions;
            var expeditions = new ScriptedExpeditions(context.State.Collection);
            var renderer = new RecordingRenderer();
            var world = new RecordingWorld(entry.Refuge);
            var screen = new ExpeditionScreenPresenter(context, expeditions, activities, renderer, world, null);
            try
            {
                Assert.AreSame(screen, activities.Expeditions, "The screen registers itself with the hub.");
                Assert.AreEqual(ActivityCardState.New, screen.CardStatus(out _), "« Nouveau » before the first expedition (A18).");
                Assert.IsTrue(screen.WantsDot, "A18: a dot on Jouer.");

                // The screen loads from Activity_Expeditions over a hidden refuge.
                screen.OpenFromHub();
                yield return WaitFor(() => screen.View != null && screen.View.Shown, 5f, "The screen loads in a moment.");
                var view = (ExpeditionScreenView)screen.View;
                Assert.IsTrue(screen.ScreenLoaded, "The screen comes from its Addressables group (Activity_Expeditions).");
                Assert.IsTrue(view.Valid);
                Assert.IsTrue(activities.IsOpen, "World touches wait while the screen shows.");
                Assert.IsFalse(entry.CameraRig.Camera.enabled, "The refuge does not render under the screen.");
                Assert.AreEqual(ExpeditionLayout.SortingOrder, view.Canvas.sortingOrder);
#if UNITY_EDITOR
                foreach (string dependency in UnityEditor.AssetDatabase.GetDependencies(ExpeditionLayout.PrefabPath, true))
                {
                    StringAssert.DoesNotContain("/Fonts/", dependency, "No font in the screen's bundle (borrowed from the scene).");
                    StringAssert.DoesNotContain("/Art/UI/Kit/", dependency, "No kit sprite in the screen's bundle.");
                    StringAssert.DoesNotContain("/Art/UI/Icons/", dependency, "No icon of the scene's atlas in the screen's bundle.");
                    StringAssert.DoesNotContain("/Thumbnails/", dependency, "No portrait in the screen's bundle.");
                }
#endif
                foreach (var label in view.BorrowedLabels)
                    Assert.IsNotNull(label.font, label.name + " got its font from the scene.");
                Assert.IsNotNull(view.Portraits, "The codex catalog is found loaded (the team's portraits).");

                // A18: the paw on the destination, then on an animal, then on « Partir »; the first stroll's single chip.
                yield return null;
                Assert.AreEqual(1, screen.TutorialStep);
                Assert.IsTrue(view.PawShown, "The paw shows Prés fleuris.");
                screen.TapDestination(0);
                Assert.IsTrue(screen.SheetOpen && view.SheetShown, "The destination opens the planning sheet.");
                Assert.AreEqual(0, screen.SheetSlot);
                Assert.AreEqual(2, screen.TutorialStep, "The paw moves to an animal.");
                Assert.GreaterOrEqual(screen.AnimalCount, Animals.Length, "Every free animal is offered.");
                Assert.IsTrue(view.Parts.durations[0].root.gameObject.activeSelf, "« Première balade : 3 min ».");
                for (int i = 1; i < ExpeditionLayout.DurationCount; i++)
                    Assert.IsFalse(view.Parts.durations[i].root.gameObject.activeSelf, "The first stroll offers one duration.");
                StringAssert.Contains("3", view.Parts.durations[0].label.text);
                screen.TapAnimal(0);
                screen.TapAnimal(1);
                Assert.AreEqual(2, screen.TeamCount);
                Assert.AreEqual(3, screen.TutorialStep, "The paw moves to « Partir ».");
                Assert.IsTrue(view.Parts.go.interactable);
                uint first = screen.TeamAt(0), second = screen.TeamAt(1);

                // « Partir »: the team leaves (the refuge lets it go), the slot counts down.
                screen.Go();
                Assert.AreEqual(1, expeditions.Starts, "The team left.");
                Assert.AreEqual(1, world.Left, "The refuge let the team go.");
                Assert.AreEqual(2, world.LastCount);
                Assert.IsFalse(screen.SheetOpen);
                Assert.AreEqual(0, screen.TutorialStep, "No paw while the team is away.");
                yield return null;
                Assert.AreEqual(ExpeditionSlotState.Away, view.SlotStateShown(0));
                string timer = view.SlotTimerText(0);
                StringAssert.Contains("3", timer, "« De retour dans 3 min ».");
                Assert.AreNotEqual(ActivityCardState.New, screen.CardStatus(out string status));
                Assert.IsNotNull(status, "The hub card shows the soonest return.");

                // No allocation per frame while the screen is open (the timer's text unchanged inside a minute).
                expeditions.Advance(10.0);
                for (int f = 0; f < 60; f++)
                    Frame(screen, expeditions);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int f = 0; f < 120; f++)
                    Frame(screen, expeditions);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0, allocated, "The expeditions screen allocates nothing per frame.");
                Assert.AreEqual(timer, view.SlotTimerText(0), "Still « 3 min » (rounded up).");

                // « Finir maintenant »: back at once; the slot glows, A19's paw on it.
                screen.SpeedUp(0);
                Assert.AreEqual(1, expeditions.SpeedUps);
                yield return null;
                Assert.AreEqual(ExpeditionSlotState.Back, view.SlotStateShown(0));
                Assert.IsTrue(view.SlotGlowing(0), "A19: the slot glows.");
                Assert.AreEqual(4, screen.TutorialStep, "A19: the paw on the slot back.");

                // The welcome (doc 05 sheet 23): collected once, the world shows, the team walks in, the postcard, the loot.
                screen.TapSlot(0);
                Assert.AreEqual(1, expeditions.Collects, "TryCollect once.");
                Assert.IsTrue(screen.Welcoming);
                Assert.AreEqual(1, world.Back, "The team walks back in.");
                Assert.AreEqual(2, world.LastCount);
                Assert.IsTrue(entry.CameraRig.Camera.enabled, "The world shows for the welcome.");
                Assert.IsFalse(view.ContentShown);
                var welcome = screen.WelcomeAt(0);
                Assert.AreEqual(first, welcome.Animal0);
                Assert.AreEqual(second, welcome.Animal1);
                Assert.AreEqual(1, renderer.Begun, "The postcard is asked for at once (it renders while the team walks).");
                Assert.AreEqual(welcome.PostcardId, renderer.LastPostcard, "The postcard of that return.");
                yield return WaitFor(() => view.PostcardShown(0), 3f, "The postcard pivots in after the walk (1.2 s at most).");
                Assert.IsTrue(view.Dimmed, "Over a dimmed world.");
                yield return WaitFor(() => screen.ResultOpen, 2f, "The loot follows the postcard.");
                Assert.AreSame(renderer.Picture, view.PostcardPicture(0), "The rendered picture shows on the card.");
                var result = Object.FindAnyObjectByType<ActivityResultScreen>();
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsOpen, "PF_UI_ActivityResult shows the loot.");
                Assert.GreaterOrEqual(result.RewardsShown, 4, "Light, treats, seeds and bond at least.");
                bool bond = false, light = false;
                for (int i = 0; i < result.RewardsShown; i++)
                {
                    bond |= result.RewardCell(i).Kind == RewardKind.Bond;
                    light |= result.RewardCell(i).Kind == RewardKind.Light;
                }
                Assert.IsTrue(bond && light, "The bond points per animal and the Light stay readable.");
                Assert.AreEqual(context.Localization.Get(ExpeditionText.SendAgain), result.ReplayButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                    "« Repartir ».");
                StringAssert.Contains(context.Localization.Get("expedition.flower_meadows.name"), result.SubtitleText, "The destination under the title.");

                // « Repartir »: the sheet with the same team and destination.
                result.ReplayButton.onClick.Invoke();
                yield return null;
                Assert.IsFalse(screen.ResultOpen);
                Assert.IsTrue(screen.SheetOpen, "Send again opens the planning sheet.");
                Assert.AreEqual(2, screen.TeamCount);
                Assert.AreEqual(first, screen.TeamAt(0));
                Assert.AreEqual(welcome.Destination, screen.SheetDestination);
                Assert.IsFalse(entry.CameraRig.Camera.enabled, "The screen covers the world again.");

                // Closing releases the screen; the refuge and the hub come back.
                screen.CloseSheet();
                screen.CloseScreen();
                yield return null;
                Assert.IsFalse(screen.IsOpen);
                Assert.IsFalse(screen.ScreenLoaded, "The screen's bundle is released.");
                Assert.IsTrue(entry.CameraRig.Camera.enabled);
                Assert.IsTrue(activities.HubOpen, "The hub comes back on close.");
                activities.CloseActivities();
                yield return null;

                // A team back while the player is in the refuge: « Expédition de retour ».
                expeditions.SetAway(1, 2.0, 3600.0);
                yield return new WaitForSecondsRealtime(0.7f);
                expeditions.Advance(5.0);
                var toasts = Object.FindAnyObjectByType<ToastQueue>(FindObjectsInactive.Include);
                yield return WaitFor(() => toasts != null && (toasts.Showing && toasts.CurrentKind == ToastKind.Expedition || toasts.Waiting > 0), 2f,
                    "The toast « Expédition de retour » shows in the refuge.");
                Assert.AreEqual(1, expeditions.BackCount);
                screen.CardStatus(out status);
                StringAssert.Contains("1", status, "« 1 de retour » on the hub card.");
            }
            finally
            {
                screen.Dispose();
                activities.Expeditions = previous;
                renderer.Dispose();
            }
        }

        /// <summary>One frame of the open screen as the game runs it (the clock, the presenter, the canvases' rebuild).</summary>
        private static void Frame(ExpeditionScreenPresenter screen, ScriptedExpeditions expeditions)
        {
            expeditions.Advance(1.0 / 60.0);
            screen.Tick(1f / 60f);
            Canvas.ForceUpdateCanvases();
        }

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
                    Species = species, Variant = variant, Rarity = RarityTier.Common, Seed = SeedHash.Mix((uint)i + 1u, 0x45585044u),
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

        /// <summary>The refuge's side, recorded (and passed on to the real refuge).</summary>
        private sealed class RecordingWorld : IExpeditionWorld
        {
            private readonly IExpeditionWorld _inner;

            public RecordingWorld(IExpeditionWorld inner) => _inner = inner;

            public int Left { get; private set; }
            public int Back { get; private set; }
            public int LastCount { get; private set; }

            public void TeamLeft(uint[] team, int count)
            {
                Left++;
                LastCount = count;
                _inner?.TeamLeft(team, count);
            }

            public float TeamBack(uint[] team, int count)
            {
                Back++;
                LastCount = count;
                return _inner != null ? Mathf.Min(1.2f, _inner.TeamBack(team, count)) : 1.2f;
            }
        }

        /// <summary>The postcard renderer, recorded: busy for a few frames, then a 512 × 768 picture.</summary>
        private sealed class RecordingRenderer : IPostcardRenderer, IDisposable
        {
            private readonly Texture2D _texture = new Texture2D(PostcardLayout.Width, PostcardLayout.Height, TextureFormat.RGBA32, false);
            private int _busyFrames;
            private Texture2D _picture;

            public int Begun { get; private set; }
            public int LastPostcard { get; private set; }

            public void Begin(in ExpeditionReturn welcome)
            {
                if (Busy)
                    return;
                Begun++;
                LastPostcard = welcome.PostcardId;
                _busyFrames = 3;
                _picture = null;
            }

            public bool Busy => _busyFrames > 0;

            public Texture2D Picture => _picture;

            public void Tick()
            {
                if (_busyFrames <= 0)
                    return;
                _busyFrames--;
                if (_busyFrames == 0)
                    _picture = _texture;
            }

            public void Release() => _picture = null;

            public void Dispose() => Object.Destroy(_texture);
        }

        /// <summary>
        /// A scripted IExpeditions: two owned slots, the Gems slot and the Club slot; Prés fleuris open; a clock advanced by
        /// the test; the first expedition lasts 3 min; the loot of section 7.3 (1 h column) with the 2 star bonus.
        /// </summary>
        private sealed class ScriptedExpeditions : IExpeditions
        {
            private static readonly ContentId Meadows = ContentId.FromSource("expedition.flower_meadows");
            private static readonly ContentId Forest = ContentId.FromSource("expedition.stump_wood");

            private readonly CollectionState _collection;
            private readonly ExpeditionDestination[] _destinations =
            {
                new ExpeditionDestination(Meadows, "expedition.flower_meadows", "EXP-GL", ContentId.FromSource("biome.grassland"), RigFamilyMask.SmallQuadruped, false),
                new ExpeditionDestination(Forest, "expedition.stump_wood", "EXP-FO", ContentId.FromSource("biome.forest"), RigFamilyMask.MediumQuadruped, false),
            };

            private readonly ExpeditionSlotState[] _states = { ExpeditionSlotState.Empty, ExpeditionSlotState.Empty, ExpeditionSlotState.Locked, ExpeditionSlotState.Locked };
            private readonly double[] _returnAt = new double[4];
            private readonly double[] _total = new double[4];
            private readonly int[] _sizes = new int[4];
            private readonly uint[,] _teams = new uint[4, 3];
            private readonly ContentId[] _where = new ContentId[4];
            private readonly ExpeditionDuration[] _durations = new ExpeditionDuration[4];
            private double _now;
            private int _postcards;

            public ScriptedExpeditions(CollectionState collection) => _collection = collection;

            public int Starts { get; private set; }
            public int SpeedUps { get; private set; }
            public int Collects { get; private set; }

            public void Advance(double seconds)
            {
                _now += seconds;
                for (int s = 0; s < 4; s++)
                {
                    if (_states[s] == ExpeditionSlotState.Away && _now >= _returnAt[s])
                    {
                        _states[s] = ExpeditionSlotState.Back;
                        SlotChanged?.Invoke(s);
                    }
                }
            }

            public void SetAway(int slot, double seconds, double total)
            {
                _states[slot] = ExpeditionSlotState.Away;
                _returnAt[slot] = _now + seconds;
                _total[slot] = total;
                _sizes[slot] = 1;
                _teams[slot, 0] = _collection[_collection.Count - 1].InstanceId;
                _where[slot] = Meadows;
                SlotChanged?.Invoke(slot);
            }

            public bool IsOpen => true;
            public int SlotCount => 4;
            public ExpeditionSlotState StateOf(int slot) => _states[slot];
            public ExpeditionSlotUnlock UnlockOf(int slot) => slot == 2 ? ExpeditionSlotUnlock.Gems : slot == 3 ? ExpeditionSlotUnlock.Club : ExpeditionSlotUnlock.None;
            public long SlotPrice => 800;
            public bool TryBuySlot() => false;
            public ContentId DestinationOf(int slot) => _where[slot];
            public ExpeditionDuration DurationOf(int slot) => _durations[slot];
            public int TeamSizeOf(int slot) => _sizes[slot];
            public uint TeamMember(int slot, int index) => _teams[slot, index];
            public byte StarsOf(int slot) => 2;
            public double SecondsLeft(int slot) => _states[slot] == ExpeditionSlotState.Away ? Math.Min(_total[slot], Math.Max(0.0, _returnAt[slot] - _now)) : 0.0;
            public double TotalSecondsOf(int slot) => _total[slot];

            public int BackCount
            {
                get
                {
                    int n = 0;
                    for (int s = 0; s < 4; s++)
                        if (_states[s] == ExpeditionSlotState.Back)
                            n++;
                    return n;
                }
            }

            public double SoonestReturn
            {
                get
                {
                    double best = -1.0;
                    for (int s = 0; s < 4; s++)
                        if (_states[s] == ExpeditionSlotState.Away && (best < 0.0 || SecondsLeft(s) < best))
                            best = SecondsLeft(s);
                    return best;
                }
            }

            public int SpeedUpCost(int slot) => Math.Max(5, (int)Math.Ceiling(SecondsLeft(slot) / 360.0));
            public int SpeedUpsLeftToday => 3 - SpeedUps;

            public bool TrySpeedUp(int slot)
            {
                if (_states[slot] != ExpeditionSlotState.Away)
                    return false;
                SpeedUps++;
                _returnAt[slot] = _now;
                Advance(0.0);
                return true;
            }

            public int DestinationCount => _destinations.Length;
            public ExpeditionDestination DestinationAt(int index) => _destinations[index];

            public bool TryGetDestination(ContentId id, out ExpeditionDestination destination)
            {
                for (int i = 0; i < _destinations.Length; i++)
                {
                    if (_destinations[i].Id == id)
                    {
                        destination = _destinations[i];
                        return true;
                    }
                }
                destination = default;
                return false;
            }

            public bool IsDestinationOpen(ContentId destination) => destination == Meadows;

            public bool IsAway(uint instanceId)
            {
                for (int s = 0; s < 4; s++)
                    if (_states[s] == ExpeditionSlotState.Away || _states[s] == ExpeditionSlotState.Back)
                        for (int k = 0; k < _sizes[s]; k++)
                            if (_teams[s, k] == instanceId)
                                return true;
                return false;
            }

            public ExpeditionRating Rate(ContentId destination, uint[] team, int count)
            {
                int points = 20 * count;
                return new ExpeditionRating(points, (byte)(points >= 60 ? 3 : points >= 40 ? 2 : 1), (byte)count, true, count >= 3);
            }

            public int SuggestTeam(ContentId destination, uint[] into)
            {
                int n = 0;
                for (int i = 0; i < _collection.Count && n < 3 && n < into.Length; i++)
                    if (!IsAway(_collection[i].InstanceId))
                        into[n++] = _collection[i].InstanceId;
                return n;
            }

            public double DurationSeconds(ExpeditionDuration duration) => _postcards == 0 && Starts == 0 ? 180.0 : 3600.0 * (duration switch
            {
                ExpeditionDuration.OneHour => 1, ExpeditionDuration.FourHours => 4, ExpeditionDuration.EightHours => 8, _ => 12,
            });

            public bool FirstStrollNext => Starts == 0;
            public int DiscoveryGuaranteeIn => 12;

            public void Preview(ContentId destination, ExpeditionDuration duration, byte stars, out ExpeditionLoot preview)
            {
                preview = new ExpeditionLoot
                {
                    LightMinutes = 15.0 * (stars >= 2 ? 1.1 : 1.0), Treat = ContentId.FromSource("treat.clover_crunch"), Treats = 1 + (stars >= 2 ? 1 : 0),
                    SeedKinds = 1, Seed0 = ContentId.FromSource("plant.sweet_pea"), Seeds0 = 2, StickerChance = 10, Tickets = 0, BondPoints = 5, MasteryXp = 15,
                };
            }

            public ExpeditionStartResult Start(int slot, ContentId destination, ExpeditionDuration duration, uint[] team, int count)
            {
                if (_states[slot] != ExpeditionSlotState.Empty)
                    return ExpeditionStartResult.SlotNotEmpty;
                if (count <= 0)
                    return ExpeditionStartResult.NoAnimal;
                double seconds = DurationSeconds(duration);
                Starts++;
                _states[slot] = ExpeditionSlotState.Away;
                _where[slot] = destination;
                _durations[slot] = duration;
                _total[slot] = seconds;
                _returnAt[slot] = _now + seconds;
                _sizes[slot] = count;
                for (int k = 0; k < count; k++)
                    _teams[slot, k] = team[k];
                SlotChanged?.Invoke(slot);
                return ExpeditionStartResult.Started;
            }

            public int ChallengeCount => 10;
            public bool IsChallengeOpen(int challenge) => challenge == 0;
            public ExpeditionDuration ChallengeDuration(int challenge) => ExpeditionDuration.OneHour;
            public bool ChallengeAllows(int challenge, ContentId destination) => destination == Meadows;
            public bool ChallengeAccepts(int challenge, ContentId destination, uint[] team, int count) => count > 0;
            public int ChallengeThreshold(int challenge, int stars) => stars == 1 ? 10 : stars == 2 ? 30 : 40;
            public byte ChallengeStars(int challenge) => 0;

            public ExpeditionStartResult StartChallenge(int slot, int challenge, ContentId destination, uint[] team, int count) =>
                Start(slot, destination, ChallengeDuration(challenge), team, count);

            public bool TryCollect(int slot, out ExpeditionReturn welcome)
            {
                welcome = default;
                if (_states[slot] != ExpeditionSlotState.Back)
                    return false;
                Collects++;
                Preview(_where[slot], _durations[slot], 2, out var loot);
                loot.StickerChance = 0;
                welcome = new ExpeditionReturn
                {
                    Slot = slot, Destination = _where[slot], Duration = _durations[slot], Stars = 2, TeamSize = _sizes[slot],
                    Animal0 = _teams[slot, 0], Animal1 = _teams[slot, 1], Animal2 = _teams[slot, 2], Loot = loot,
                    Light = BigValue.FromDouble(1500.0), PostcardId = ++_postcards, FirstStroll = _postcards == 1, Seed = 7u,
                };
                _states[slot] = ExpeditionSlotState.Empty;
                _sizes[slot] = 0;
                SlotChanged?.Invoke(slot);
                return true;
            }

            public void PostcardRendered(int postcardId)
            {
            }

            public int PostcardCount => _postcards;
            public int PostcardAt(int index) => _postcards - index;
            public event Action<int> SlotChanged;
        }
    }
}
