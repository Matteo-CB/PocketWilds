using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T42 postcards on disk and on their set (doc 01 Contenu, section 7.1): the file names (postcard_{id}.png in the
    /// Postcards folder), a picture written whole or not at all, the pictures no longer kept deleted and nothing else, the set
    /// and card art names, and the team's seats: nobody enters anybody nor a prop, a Giant included.
    /// </summary>
    public class PostcardStoreTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "PocketWildsPostcards", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void FileNames_FollowTheLayout()
        {
            var store = new PostcardStore(_root);
            Assert.AreEqual(Path.Combine(_root, "Postcards"), store.Folder);
            Assert.AreEqual(Path.Combine(_root, "Postcards", "postcard_7.png"), store.PathOf(7));
            Assert.AreEqual("postcard_123.png", PostcardLayout.FileName(123));

            Assert.IsTrue(PostcardStore.TryParseId("postcard_42.png", out int id));
            Assert.AreEqual(42, id);
            Assert.IsTrue(PostcardStore.TryParseId(Path.Combine(_root, "Postcards", "postcard_0.png"), out id));
            Assert.AreEqual(0, id);
            foreach (var other in new[] { "postcard_.png", "postcard_4.jpg", "postcard_4.png.tmp", "card_4.png", "postcard_-3.png", "postcard_04.png",
                         "postcard_4a.png", "postcard_99999999999.png", "notes.txt", "" })
                Assert.IsFalse(PostcardStore.TryParseId(other, out _), other + " is not a postcard");
        }

        [Test]
        public void Write_LeavesAWholePicture_AndNoTemporaryFile()
        {
            var store = new PostcardStore(_root);
            Assert.IsFalse(store.Exists(3));
            var first = new byte[] { 1, 2, 3, 4 };
            store.Write(3, first);
            Assert.IsTrue(store.Exists(3));
            CollectionAssert.AreEqual(first, File.ReadAllBytes(store.PathOf(3)));
            var second = new byte[] { 9, 8, 7 };
            store.Write(3, second);
            CollectionAssert.AreEqual(second, File.ReadAllBytes(store.PathOf(3)), "A second picture of the same postcard replaces the first.");
            Assert.AreEqual(1, Directory.GetFiles(store.Folder).Length, "No temporary file left behind.");
            Assert.Throws<ArgumentException>(() => store.Write(4, Array.Empty<byte>()));
        }

        [Test]
        public void TryLoad_ReadsAStoredPicture()
        {
            var store = new PostcardStore(_root);
            var source = new Texture2D(8, 12, TextureFormat.RGBA32, false);
            var into = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[8 * 12];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32((byte)(i * 3), 120, 200, 255);
                source.SetPixels32(pixels);
                source.Apply();
                store.Write(5, source.EncodeToPNG());
                Assert.IsTrue(store.TryLoad(5, into));
                Assert.AreEqual(8, into.width);
                Assert.AreEqual(12, into.height);
                Assert.IsFalse(store.TryLoad(6, into), "A postcard never stored is not there.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(into);
            }
        }

        [Test]
        public void Prune_DeletesOnlyThePicturesNoLongerKept()
        {
            var store = new PostcardStore(_root);
            foreach (int id in new[] { 1, 2, 3, 10, 11 })
                store.Write(id, new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(store.Folder, "postcard_12.png.tmp"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(store.Folder, "readme.txt"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(store.Folder, "postcard_x.png"), new byte[] { 1 });
            var expeditions = new PostcardFakeExpeditions();
            expeditions.Kept.AddRange(new[] { 11, 10, 1 });

            Assert.AreEqual(2, store.Prune(expeditions), "Postcards 2 and 3 are no longer kept.");
            Assert.IsTrue(store.Exists(1), "The first postcard of a destination is kept for ever.");
            Assert.IsTrue(store.Exists(10));
            Assert.IsTrue(store.Exists(11));
            Assert.IsFalse(store.Exists(2));
            Assert.IsFalse(store.Exists(3));
            Assert.IsFalse(File.Exists(Path.Combine(store.Folder, "postcard_12.png.tmp")), "A half-written picture goes.");
            Assert.IsTrue(File.Exists(Path.Combine(store.Folder, "readme.txt")), "Other files are left alone.");
            Assert.IsTrue(File.Exists(Path.Combine(store.Folder, "postcard_x.png")), "Other files are left alone.");
            Assert.AreEqual(0, store.Prune(expeditions), "A second prune finds nothing more.");
            Assert.AreEqual(0, new PostcardStore(Path.Combine(_root, "none")).Prune(expeditions), "No folder yet: nothing to prune.");
        }

        [Test]
        public void SetAndCardArt_AreNamedFromTheDestination()
        {
            Assert.AreEqual("PF_Activity_Postcard_FlowerMeadows", PostcardSetLayout.PrefabName("expedition.flower_meadows"));
            Assert.AreEqual("activity.expeditions.postcard.flower_meadows", PostcardSetLayout.SetAddress("expedition.flower_meadows"));
            Assert.AreEqual("EXP_flower_meadows", PostcardLayout.CardArtName("expedition.flower_meadows"));
            Assert.AreEqual(0, PostcardSetLayout.SeatIndex(1, 0));
            Assert.AreEqual(2, PostcardSetLayout.SeatIndex(2, 1));
            Assert.AreEqual(5, PostcardSetLayout.SeatIndex(3, 2));
            Assert.LessOrEqual(Mathf.Max(PostcardSetLayout.CardArtWidth, PostcardSetLayout.CardArtHeight), 512, "The card art stays a small texture.");
        }

        [Test]
        public void TeamLayout_KeepsEveryBodyClear_EvenAGiant()
        {
            // The set's seats (PostcardSetPipeline.SeatPlan), the basket at the right, three Prairie bodies (the catalog's).
            var data = new PostcardSetData
            {
                blanketTop = 0.018f, blanketHalf = new Vector2(0.95f, 0.55f), blanketPower = 7f,
                seats = new[]
                {
                    new Vector3(-0.12f, 0.01f, 0.05f), new Vector3(-0.40f, 0.01f, 0.05f), new Vector3(0.04f, 0.01f, 0.05f),
                    new Vector3(-0.62f, 0.01f, 0f), new Vector3(-0.20f, 0.01f, 0.12f), new Vector3(0.22f, 0.01f, 0f),
                },
                seatYaws = new[] { 180f, 172f, 188f, 172f, 180f, 188f },
                obstacles = new[] { new Obstacle(new Vector2(0.66f, 0.1f), new Vector2(0.74f, 0.1f), 0.13f, 0.01f, -0.01f, 0.3f, ObstacleKind.Solid) },
            };
            var rabbit = new BodyShape(0.2345f, -0.3035f, 0.1634f, 0.658f);
            var squirrel = new BodyShape(0.2309f, -0.4085f, 0.1485f, 0.55f);
            var hedgehog = new BodyShape(0.2551f, -0.2701f, 0.1663f, 0.334f);
            var bodies = new[] { squirrel, rabbit, hedgehog };
            var positions = new Vector3[3];
            var yaws = new float[3];

            float gap = PostcardTeamLayout.Solve(data, 3, bodies, new[] { 1f, 1f, 1f }, positions, yaws);
            Assert.GreaterOrEqual(gap, PostcardSetLayout.SeatGap - 1e-4f, "A team of three sits on its seats with room to spare.");
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(data.seats[PostcardSetLayout.SeatIndex(3, i)], positions[i], "Everybody on their own seat.");

            // Three Giants of the largest size (1.15 × 1.2): pushed aside along the line, still nobody in anybody nor the basket.
            gap = PostcardTeamLayout.Solve(data, 3, new[] { hedgehog, hedgehog, hedgehog }, new[] { 1.38f, 1.38f, 1.38f }, positions, yaws);
            Assert.GreaterOrEqual(gap, PostcardSetLayout.SeatGap - 1e-3f, "Giants are pushed apart until they clear each other and the basket.");
            Assert.AreEqual(gap, PostcardTeamLayout.SmallestGap(data, 3, new[] { hedgehog, hedgehog, hedgehog }, new[] { 1.38f, 1.38f, 1.38f }, positions, yaws), 1e-5f);
            for (int i = 0; i < 3; i++)
                Assert.That(positions[i].y, Is.EqualTo(data.blanketTop).Or.EqualTo(0f).Or.EqualTo(data.seats[PostcardSetLayout.SeatIndex(3, i)].y),
                    "A moved animal stands on the cloth's highest point or on the meadow, never in the blanket.");

            gap = PostcardTeamLayout.Solve(data, 1, new[] { rabbit }, new[] { 1f }, positions, yaws);
            Assert.AreEqual(data.seats[0], positions[0], "Alone: the middle seat.");
            Assert.AreEqual(180f, yaws[0], "Facing the camera.");
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
            destination = new ExpeditionDestination(id, "expedition.flower_meadows", "EXP-GL-01", ContentId.FromSource("biome.grassland"), RigFamilyMask.Hopper, false);
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
