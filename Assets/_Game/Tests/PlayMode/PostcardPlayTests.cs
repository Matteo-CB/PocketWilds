using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PocketWilds.Bootstrap;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T42 postcards in the real game (doc 01 Contenu, section 7.1; doc 05 sheet 23): after a real boot, a team of three
    /// Prairie animals is posed in the Prés fleuris set (loaded from Activity_Expeditions), one frame is drawn and read back,
    /// the PNG is written off the main thread as Postcards/postcard_{id}.png (512 × 768, a real picture: sky above, meadow
    /// below), the expeditions are told, the picture shows, nobody enters anybody nor a prop, the refuge's light is given
    /// back; a second postcard reuses the set; Release lets everything go.
    /// </summary>
    public class PostcardPlayTests
    {
        private static readonly (string species, string variant)[] Team =
        {
            ("species.squirrel", "variant.squirrel.red"), ("species.rabbit", "variant.rabbit.brown"), ("species.hedgehog", "variant.hedgehog.brown"),
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
        public IEnumerator Postcard_OfATeamOfThree_IsRenderedAndStored()
        {
            SeedTeam();
            GameBootstrap bootstrap = null;
            yield return Boot(b => bootstrap = b);
            var context = bootstrap.Context;
            var collection = context.State.Collection;
            Assert.GreaterOrEqual(collection.Count, 3, "The team is in the collection.");

            var welcome = new ExpeditionReturn
            {
                Slot = 0, Destination = ContentId.FromSource(PostcardSetLayout.FlowerMeadows), Duration = ExpeditionDuration.FourHours, Stars = 3,
                TeamSize = 3, PostcardId = 77, Seed = 0x50435452u,
            };
            for (int i = 0; i < 3; i++)
            {
                var animal = collection[i];
                if (i == 0) { welcome.Animal0 = animal.InstanceId; welcome.Species0 = animal.Species; welcome.Variant0 = animal.Variant; }
                if (i == 1) { welcome.Animal1 = animal.InstanceId; welcome.Species1 = animal.Species; welcome.Variant1 = animal.Variant; }
                if (i == 2) { welcome.Animal2 = animal.InstanceId; welcome.Species2 = animal.Species; welcome.Variant2 = animal.Variant; }
            }

            var root = new GameObject("PostcardTest");
            var store = new PostcardStore(_saveDir);
            var expeditions = new PostcardFakeExpeditions();
            expeditions.Kept.Add(77);
            expeditions.Kept.Add(78);
            File.WriteAllBytes(Path.Combine(Directory.CreateDirectory(store.Folder).FullName, "postcard_5.png"), new byte[] { 1 });
            var renderer = new PostcardRenderer(context, expeditions, root.transform, store);
            var sun = RenderSettings.sun;
            var ambient = RenderSettings.ambientSkyColor;
            try
            {
                double slowest = 0;
                yield return Render(renderer, welcome, ms => slowest = Math.Max(slowest, ms));
                Debug.Log($"[PostcardPlayTests] slowest tick {slowest:F1} ms");

                Assert.IsTrue(renderer.SetLoaded, "The set comes from its Addressables group (Activity_Expeditions).");
                Assert.IsNotNull(renderer.Set, "The Prés fleuris set is built.");
                Assert.IsFalse(renderer.Set.gameObject.activeSelf, "The set hides between postcards.");
                Assert.AreSame(sun, RenderSettings.sun, "The refuge's sun is given back in the same frame.");
                Assert.AreEqual(ambient, RenderSettings.ambientSkyColor, "The refuge's ambient is given back.");
                CollectionAssert.AreEqual(new[] { 77 }, expeditions.Rendered, "The expeditions are told once the picture is stored.");

                // Nobody in anybody nor in a prop.
                Assert.AreEqual(3, renderer.PosedCount, "The three animals are posed.");
                Assert.GreaterOrEqual(renderer.PosedGap, PostcardSetLayout.SeatGap - 1e-4f, "Nobody enters anybody nor a prop (no-collision rule).");
                var data = renderer.Set.Data;
                for (int i = 0; i < 3; i++)
                    Assert.GreaterOrEqual(renderer.PosedPosition(i).y, 0f, "Nobody stands in the ground.");
                Assert.GreaterOrEqual(data.butterflies.Length, PostcardSetLayout.ButterfliesShown, "White butterflies around.");

                // The file: 512 × 768, a real picture, the sky at the top.
                Assert.IsTrue(store.Exists(77), "Postcards/postcard_77.png is written.");
                // A copy for the review (the captures folder is outside Assets and ignored by git).
                Directory.CreateDirectory(Path.Combine("TestResults", "Captures"));
                File.Copy(store.PathOf(77), Path.Combine("TestResults", "Captures", "T42_postcard_runtime.png"), true);
                var picture = new Texture2D(2, 2);
                try
                {
                    Assert.IsTrue(picture.LoadImage(File.ReadAllBytes(store.PathOf(77))), "A PNG.");
                    Assert.AreEqual(PostcardLayout.Width, picture.width);
                    Assert.AreEqual(PostcardLayout.Height, picture.height);
                    var pixels = picture.GetPixels32();
                    AssertNotBlank(pixels);
                    Assert.Greater(Blueness(pixels, picture.width, 0.85f, 1f), Blueness(pixels, picture.width, 0f, 0.12f),
                        "The sky is at the top of the postcard, the meadow at the bottom (not upside down).");
                }
                finally
                {
                    Object.Destroy(picture);
                }
                Assert.IsNotNull(renderer.Picture, "The screen shows the picture.");
                Assert.AreEqual(PostcardLayout.Width, renderer.Picture.width);
                Assert.AreEqual(PostcardLayout.Height, renderer.Picture.height);

                // The pictures no longer kept go (off the main thread, after the welcome).
                float until = Time.realtimeSinceStartup + 3f;
                while (File.Exists(store.PathOf(5)) && Time.realtimeSinceStartup < until)
                    yield return null;
                Assert.IsFalse(File.Exists(store.PathOf(5)), "A postcard no longer kept is deleted.");

                // A second postcard (a single animal) reuses the set and the picture texture.
                var set = renderer.Set;
                var texture = renderer.Picture;
                var second = welcome;
                second.PostcardId = 78;
                second.TeamSize = 1;
                second.Seed = 7;
                renderer.Begin(second);
                Assert.IsNull(renderer.Picture, "No picture until the new one is drawn.");
                renderer.Begin(welcome);
                Assert.AreEqual(78, renderer.PostcardId, "One postcard at a time: a second call while busy is ignored.");
                yield return Render(renderer, second, null);
                Assert.AreSame(set, renderer.Set, "The set is loaded once.");
                Assert.AreSame(texture, renderer.Picture, "The same texture until Release.");
                Assert.IsTrue(store.Exists(78));
                Assert.AreEqual(1, renderer.PosedCount);
                CollectionAssert.AreEqual(new[] { 77, 78 }, expeditions.Rendered);

                renderer.Release();
                Assert.IsNull(renderer.Picture, "Release lets go of the picture.");
                Assert.IsFalse(renderer.SetLoaded, "Release lets go of the set.");
                Assert.IsNull(renderer.Set);
            }
            finally
            {
                renderer.Dispose();
                Object.Destroy(root);
            }
        }

        private static IEnumerator Render(PostcardRenderer renderer, ExpeditionReturn welcome, Action<double> tick)
        {
            if (!renderer.Busy)
                renderer.Begin(welcome);
            Assert.IsTrue(renderer.Busy, "The postcard starts.");
            var watch = new Stopwatch();
            float until = Time.realtimeSinceStartup + 20f;
            while (renderer.Busy)
            {
                if (Time.realtimeSinceStartup > until)
                    Assert.Fail("The postcard took too long.");
                watch.Restart();
                renderer.Tick();
                tick?.Invoke(watch.Elapsed.TotalMilliseconds);
                yield return null;
            }
        }

        private static void AssertNotBlank(Color32[] pixels)
        {
            var colors = new HashSet<int>();
            double sum = 0, sumSq = 0;
            int n = 0;
            for (int i = 0; i < pixels.Length; i += 97)
            {
                var p = pixels[i];
                Assert.AreEqual(255, p.a, "A postcard is opaque.");
                colors.Add((p.r >> 3) << 10 | (p.g >> 3) << 5 | (p.b >> 3));
                double l = 0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b;
                sum += l;
                sumSq += l * l;
                n++;
            }
            double mean = sum / n;
            double deviation = Math.Sqrt(Math.Max(0, sumSq / n - mean * mean));
            Assert.Greater(colors.Count, 60, "Many colors: the set and the team, not a blank frame.");
            Assert.Greater(deviation, 8.0, "Light and shade: not a flat color.");
            Assert.Greater(mean, 30.0, "Not black.");
        }

        /// <summary>Mean blue minus green over a band of rows (0 the bottom row, 1 the top: Texture2D order).</summary>
        private static double Blueness(Color32[] pixels, int width, float from, float to)
        {
            int height = pixels.Length / width;
            int y0 = Mathf.Clamp((int)(from * height), 0, height - 1), y1 = Mathf.Clamp((int)(to * height), y0 + 1, height);
            double sum = 0;
            int n = 0;
            for (int y = y0; y < y1; y++)
                for (int x = 0; x < width; x += 7)
                {
                    var p = pixels[y * width + x];
                    sum += p.b - p.g;
                    n++;
                }
            return sum / Math.Max(1, n);
        }

        private void SeedTeam()
        {
            var service = new SaveService(new SaveFileStore(_saveDir), new BinarySaveSerializer(), SaveMigrations.CreatePipeline(),
                new RuntimeGameClock(), new SaveServiceOptions { AppVersion = Application.version });
            service.LoadOrCreate();
            var state = service.State;
            for (int i = 0; i < Team.Length; i++)
            {
                var species = ContentId.FromSource(Team[i].species);
                state.Collection.Add(new AnimalInstance
                {
                    Species = species, Variant = ContentId.FromSource(Team[i].variant), Rarity = RarityTier.Common,
                    Seed = SeedHash.Mix((uint)i + 1u, 0x50435344u),
                });
                state.Collection.DiscoveredSpecies.Add(species);
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
    }

    /// <summary>The expeditions as far as the postcards care: the kept postcards (newest first) and the rendered ones.</summary>
    internal sealed class PostcardFakeExpeditions : IExpeditions
    {
        public readonly List<int> Kept = new List<int>();
        public readonly List<int> Rendered = new List<int>();

        public bool IsOpen => true;
        public int SlotCount => 4;
        public ExpeditionSlotState StateOf(int slot) => ExpeditionSlotState.Empty;
        public ExpeditionSlotUnlock UnlockOf(int slot) => ExpeditionSlotUnlock.None;
        public long SlotPrice => 800;
        public bool TryBuySlot() => false;
        public ContentId DestinationOf(int slot) => ContentId.None;
        public ExpeditionDuration DurationOf(int slot) => ExpeditionDuration.OneHour;
        public int TeamSizeOf(int slot) => 0;
        public uint TeamMember(int slot, int index) => 0;
        public byte StarsOf(int slot) => 0;
        public double SecondsLeft(int slot) => 0;
        public double TotalSecondsOf(int slot) => 0;
        public int BackCount => 0;
        public double SoonestReturn => -1;
        public int SpeedUpCost(int slot) => 0;
        public int SpeedUpsLeftToday => 0;
        public bool TrySpeedUp(int slot) => false;
        public int DestinationCount => 0;
        public ExpeditionDestination DestinationAt(int index) => default;

        public bool TryGetDestination(ContentId id, out ExpeditionDestination destination)
        {
            destination = new ExpeditionDestination(id, PostcardSetLayout.FlowerMeadows, "EXP-GL-01", ContentId.FromSource("biome.grassland"), RigFamilyMask.Hopper, false);
            return true;
        }

        public bool IsDestinationOpen(ContentId destination) => true;
        public bool IsAway(uint instanceId) => false;
        public ExpeditionRating Rate(ContentId destination, uint[] team, int count) => default;
        public int SuggestTeam(ContentId destination, uint[] into) => 0;
        public double DurationSeconds(ExpeditionDuration duration) => 3600;
        public bool FirstStrollNext => false;
        public int DiscoveryGuaranteeIn => 0;

        public void Preview(ContentId destination, ExpeditionDuration duration, byte stars, out ExpeditionLoot preview) => preview = default;

        public ExpeditionStartResult Start(int slot, ContentId destination, ExpeditionDuration duration, uint[] team, int count) => ExpeditionStartResult.NotOpen;
        public int ChallengeCount => 0;
        public bool IsChallengeOpen(int challenge) => false;
        public ExpeditionDuration ChallengeDuration(int challenge) => ExpeditionDuration.OneHour;
        public bool ChallengeAllows(int challenge, ContentId destination) => false;
        public bool ChallengeAccepts(int challenge, ContentId destination, uint[] team, int count) => false;
        public int ChallengeThreshold(int challenge, int stars) => 0;
        public byte ChallengeStars(int challenge) => 0;
        public ExpeditionStartResult StartChallenge(int slot, int challenge, ContentId destination, uint[] team, int count) => ExpeditionStartResult.NotOpen;

        public bool TryCollect(int slot, out ExpeditionReturn welcome)
        {
            welcome = default;
            return false;
        }

        public void PostcardRendered(int postcardId) => Rendered.Add(postcardId);
        public int PostcardCount => Kept.Count;
        public int PostcardAt(int index) => Kept[index];

        public event Action<int> SlotChanged
        {
            add { }
            remove { }
        }
    }
}
