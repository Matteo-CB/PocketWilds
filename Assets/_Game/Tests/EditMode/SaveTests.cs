using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    public class SaveTests
    {
        private string _dir;
        private FakeGameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _dir = TestUtil.NewTempDirectory();
            _clock = new FakeGameClock(TestUtil.T0);
        }

        [TearDown]
        public void TearDown() => TestUtil.DeleteDirectory(_dir);

        private SaveService NewService(int schema = SaveSchema.CurrentVersion, params ISaveMigration[] migrations)
        {
            // Default: the shipped migration chain, like the bootstrap.
            var pipeline = migrations.Length == 0 && schema == SaveSchema.CurrentVersion
                ? SaveMigrations.CreatePipeline()
                : new SaveMigrationPipeline(schema, migrations);
            return new SaveService(
                new SaveFileStore(_dir),
                new BinarySaveSerializer(),
                pipeline,
                _clock,
                new SaveServiceOptions { AppVersion = "test" });
        }

        private static PlayerState RichState()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            state.Wallet.Light = BigValue.FromMantissaExponent(4.2, 123);
            state.Wallet.LifetimeLight = BigValue.FromMantissaExponent(9.9, 130);
            state.Wallet.Essence = 77;
            state.Wallet.Gems = 1234;
            state.World.Biomes.Add(new BiomeProgress { Biome = ContentId.FromSource("biome.grassland"), Unlocked = true, Expansions = 4 });
            state.World.EggPurchasesInCycle[ContentId.FromSource("egg.grassland.basic")] = 19;
            state.World.RenewalCount = 2;
            for (int i = 0; i < 10; i++)
            {
                state.Collection.Add(new AnimalInstance
                {
                    Species = ContentId.FromSource("species.rabbit"),
                    Variant = ContentId.FromSource("variant.rabbit.white"),
                    Seed = (uint)(i * 31 + 7),
                    CreatedUtcSeconds = 1_790_000_000u + (uint)i,
                    TraitBits = (uint)i,
                    BondPoints = (ushort)(i * 10),
                    Rarity = (RarityTier)(i % 6),
                    Flags = i == 3 ? AnimalFlags.Favorite : AnimalFlags.None,
                });
            }
            state.Collection.Remove(5);
            state.Collection.CustomNames[3] = "Mochi";
            state.Collection.DiscoveredSpecies.Add(ContentId.FromSource("species.rabbit"));
            state.Collection.DiscoveredVariants.Add(ContentId.FromSource("variant.rabbit.white"));
            state.Mastery.Species[ContentId.FromSource("species.rabbit")] = new SpeciesMastery { Level = 6, Xp = 33 };
            state.Settings.LanguageCode = "fr";
            state.Settings.MusicVolume = 0.25f;
            state.Settings.HapticsEnabled = false;
            state.Entitlements.Owned.Add(ContentId.FromSource("offer.starter.001"));
            state.Entitlements.ProcessedTransactions.Add("GPA.1");
            return state;
        }

        private static PlayerState RoundTrip(PlayerState state)
        {
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(state, writer);
            var bytes = writer.WrittenSpan.ToArray();
            return serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
        }

        [Test]
        public void Serializer_RoundTripsEveryField()
        {
            var original = RichState();
            var loaded = RoundTrip(original);

            Assert.AreEqual(original.Meta.ProfileId, loaded.Meta.ProfileId);
            Assert.AreEqual(original.Meta.CreatedUtc, loaded.Meta.CreatedUtc);
            Assert.AreEqual(original.Wallet.Light, loaded.Wallet.Light);
            Assert.AreEqual(original.Wallet.LifetimeLight, loaded.Wallet.LifetimeLight);
            Assert.AreEqual(77, loaded.Wallet.Essence);
            Assert.AreEqual(1234, loaded.Wallet.Gems);
            Assert.AreEqual(4, loaded.World.Biomes[0].Expansions);
            Assert.AreEqual(19, loaded.World.EggPurchasesInCycle[ContentId.FromSource("egg.grassland.basic")]);
            Assert.AreEqual(2, loaded.World.RenewalCount);

            Assert.AreEqual(9, loaded.Collection.Count);
            Assert.AreEqual(11u, loaded.Collection.NextInstanceId, "Removed ids are never reused.");
            Assert.IsFalse(loaded.Collection.TryGet(5, out _));
            Assert.IsTrue(loaded.Collection.TryGet(4, out var favorite));
            Assert.IsTrue(favorite.IsFavorite);
            Assert.AreEqual(RarityTier.Epic, favorite.Rarity);
            Assert.AreEqual(100u, favorite.Seed);
            Assert.AreEqual("Mochi", loaded.Collection.CustomNames[3]);
            Assert.AreEqual(1, loaded.Collection.DiscoveredVariants.Count);

            Assert.AreEqual(6, loaded.Mastery.GetLevel(ContentId.FromSource("species.rabbit")));
            Assert.AreEqual("fr", loaded.Settings.LanguageCode);
            Assert.AreEqual(0.25f, loaded.Settings.MusicVolume);
            Assert.IsFalse(loaded.Settings.HapticsEnabled);
            Assert.IsTrue(loaded.Entitlements.Owned.Contains(ContentId.FromSource("offer.starter.001")));
            Assert.IsTrue(loaded.Entitlements.ProcessedTransactions.Contains("GPA.1"));
        }

        [Test]
        public void Serializer_SteadyStateDoesNotAllocate()
        {
            var state = RichState();
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(state, writer);
            Assert.That(() => serializer.Serialize(state, writer), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Service_CreatesNewProfile_ThenReloadsIt()
        {
            var first = NewService();
            var result = first.LoadOrCreate();
            Assert.AreEqual(SaveLoadOutcome.NewProfile, result.Outcome);
            var profileId = first.State.Meta.ProfileId;
            first.State.Wallet.Gems = 42;
            Assert.IsTrue(first.FlushNow());
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "profile0.pwsave")));

            var second = NewService();
            var reloaded = second.LoadOrCreate();
            Assert.AreEqual(SaveLoadOutcome.Loaded, reloaded.Outcome);
            Assert.AreEqual(profileId, second.State.Meta.ProfileId);
            Assert.AreEqual(42, second.State.Wallet.Gems);
            Assert.AreEqual(1, second.State.Meta.Revision);
        }

        [Test]
        public void AtomicWrite_RotatesPrevious()
        {
            var service = NewService();
            service.LoadOrCreate();
            service.FlushNow();
            service.State.Wallet.Gems = 7;
            service.FlushNow();

            var store = new SaveFileStore(_dir);
            Assert.IsTrue(File.Exists(store.PreviousPath));
            Assert.IsFalse(File.Exists(store.TempPath));
            Assert.IsTrue(SaveFileFormat.TryValidate(File.ReadAllBytes(store.PreviousPath), (int)new FileInfo(store.PreviousPath).Length, out var previous, out _));
            Assert.IsTrue(SaveFileFormat.TryValidate(File.ReadAllBytes(store.CurrentPath), (int)new FileInfo(store.CurrentPath).Length, out var current, out _));
            Assert.Less(previous.Revision, current.Revision);
        }

        [Test]
        public void CorruptCurrent_LoadsPrevious_AndQuarantinesTheDamagedFile()
        {
            var service = NewService();
            service.LoadOrCreate();
            service.State.Wallet.Gems = 1;
            service.FlushNow();
            service.State.Wallet.Gems = 2;
            service.FlushNow();

            var store = new SaveFileStore(_dir);
            var bytes = File.ReadAllBytes(store.CurrentPath);
            bytes[bytes.Length - 3] ^= 0x5A;
            File.WriteAllBytes(store.CurrentPath, bytes);

            var recovered = NewService();
            var result = recovered.LoadOrCreate();
            Assert.AreEqual(SaveLoadOutcome.LoadedFromBackup, result.Outcome);
            Assert.AreEqual(1, recovered.State.Wallet.Gems);
            Assert.IsFalse(File.Exists(store.CurrentPath));
            Assert.AreEqual(1, Directory.GetFiles(_dir, "*.corrupt-*").Length, "Damaged file is kept for diagnostics.");

            // Next save must not destroy the good backup.
            recovered.FlushNow();
            Assert.IsTrue(File.Exists(store.PreviousPath));
        }

        [Test]
        public void CrashAfterTempWritten_LoadsNewestRevisionFromTemp()
        {
            var service = NewService();
            service.LoadOrCreate();
            service.State.Wallet.Gems = 10;
            service.FlushNow();

            // Simulate a crash between "temp verified" and "rename": temp holds a newer valid save.
            var state = service.State;
            state.Wallet.Gems = 99;
            state.Meta.Revision = 50;
            var writer = new ByteBufferWriter();
            new BinarySaveSerializer().Serialize(state, writer);
            var store = new SaveFileStore(_dir);
            File.WriteAllBytes(store.TempPath, writer.WrittenSpan.ToArray());

            var reloaded = NewService();
            var result = reloaded.LoadOrCreate();
            Assert.AreEqual(SaveSlotKind.Temp, result.Source);
            Assert.AreEqual(99, reloaded.State.Wallet.Gems);
        }

        [Test]
        public void TruncatedTemp_IsIgnored_AndCurrentLoadsNormally()
        {
            var service = NewService();
            service.LoadOrCreate();
            service.State.Wallet.Gems = 5;
            service.FlushNow();

            var store = new SaveFileStore(_dir);
            var bytes = File.ReadAllBytes(store.CurrentPath);
            File.WriteAllBytes(store.TempPath, new ArraySegment<byte>(bytes, 0, bytes.Length / 2).ToArray());

            var reloaded = NewService();
            var result = reloaded.LoadOrCreate();
            Assert.AreEqual(SaveLoadOutcome.Loaded, result.Outcome);
            Assert.AreEqual(5, reloaded.State.Wallet.Gems);
            Assert.IsFalse(File.Exists(store.TempPath));
        }

        [Test]
        public void EverythingCorrupt_StartsFresh_ButKeepsFilesAside()
        {
            var store = new SaveFileStore(_dir);
            File.WriteAllBytes(store.CurrentPath, new byte[] { 1, 2, 3, 4, 5 });
            File.WriteAllBytes(store.PreviousPath, new byte[64]);

            var service = NewService();
            var result = service.LoadOrCreate();
            Assert.AreEqual(SaveLoadOutcome.NewProfileAfterFailure, result.Outcome);
            Assert.AreEqual(2, Directory.GetFiles(_dir, "*.corrupt-*").Length);
        }

        [Test]
        public void NewerSchema_IsRefused_AndNeverOverwritten()
        {
            var service = NewService();
            service.LoadOrCreate();
            service.FlushNow();

            // Pretend a future build (next schema) wrote this file. Schema lives in the header at offset 8.
            var store = new SaveFileStore(_dir);
            var before = File.ReadAllBytes(store.CurrentPath);
            before[8] = (byte)(SaveSchema.CurrentVersion + 1);
            File.WriteAllBytes(store.CurrentPath, before);

            var today = NewService();
            var result = today.LoadOrCreate();
            Assert.AreEqual(SaveLoadOutcome.NewerSchemaRefused, result.Outcome);
            Assert.IsTrue(today.IsReadOnly);
            Assert.IsFalse(today.FlushNow());
            today.Tick();
            CollectionAssert.AreEqual(before, File.ReadAllBytes(store.CurrentPath));
        }

        [Test]
        public void MigrationPipeline_AppliesChain_AndRejectsGaps()
        {
            var document = new SaveDocument(1, 3);
            var pipeline = new SaveMigrationPipeline(2, new ISaveMigration[] { new AddSectionMigration() });
            Assert.AreEqual(1, pipeline.Migrate(document));
            Assert.AreEqual(2, document.SchemaVersion);
            Assert.IsTrue(document.TryGetSection((SaveSectionId)900, out _));

            var gap = new SaveMigrationPipeline(3, new ISaveMigration[] { new AddSectionMigration() });
            Assert.Throws<SaveMigrationException>(() => gap.Migrate(new SaveDocument(1, 1)));
            Assert.Throws<ArgumentException>(() => new SaveMigrationPipeline(3, new ISaveMigration[] { new SkipMigration() }));
        }

        [Test]
        public void Autosave_DebouncesNormal_AndDelaysPriorityPastTheInteractionFrame()
        {
            var service = NewService();
            service.LoadOrCreate();
            service.FlushNow();
            long revision = service.State.Meta.Revision;

            service.RequestSave(SaveUrgency.Normal);
            _clock.Advance(TimeSpan.FromSeconds(1));
            service.Tick();
            Assert.AreEqual(revision, service.State.Meta.Revision, "Normal saves wait for the debounce.");

            _clock.Advance(TimeSpan.FromSeconds(5));
            service.Tick();
            Assert.AreEqual(revision + 1, service.State.Meta.Revision);
            service.PendingWrite.Wait();
            service.Tick();
            Assert.IsFalse(service.IsWriteInFlight);

            service.RequestSave(SaveUrgency.Priority);
            service.Tick();
            Assert.AreEqual(revision + 1, service.State.Meta.Revision, "Never on the same frame as the request.");
            _clock.Advance(TimeSpan.FromSeconds(0.3));
            service.Tick();
            Assert.AreEqual(revision + 2, service.State.Meta.Revision);
            service.PendingWrite.Wait();
        }

        [Test]
        public void AtomicWrite_ReusesItsBuffers()
        {
            var store = new SaveFileStore(_dir);
            var writer = new ByteBufferWriter();
            new BinarySaveSerializer().Serialize(RichState(), writer);
            store.WriteAtomic(writer.Buffer, writer.Length);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 5; i++)
                store.WriteAtomic(writer.Buffer, writer.Length);
            long perWrite = (GC.GetAllocatedBytesForCurrentThread() - before) / 5;
            UnityEngine.Debug.Log($"[Benchmark] Save write: {perWrite} B managed per write ({writer.Length} B file)");
            Assert.Less(perWrite, 4096, "No 64 KB stream buffer, no read-back array: only file handles.");
        }

        [Test]
        public void BackgroundAutosave_StartsWithoutAllocating()
        {
            var service = NewService();
            service.LoadOrCreate();
            // First background write creates the writer thread (once per session).
            service.RequestSave(SaveUrgency.Priority);
            _clock.Advance(TimeSpan.FromSeconds(1));
            service.Tick();
            Assert.IsTrue(service.IsWriteInFlight || service.State.Meta.Revision > 0);
            service.PendingWrite.Wait();
            service.Tick();
            long revision = service.State.Meta.Revision;

            service.RequestSave(SaveUrgency.Priority);
            _clock.Advance(TimeSpan.FromSeconds(1));
            Assert.That(() => service.Tick(), Is.Not.AllocatingGCMemory(), "Serialize + hand-off to the writer thread.");
            Assert.AreEqual(revision + 1, service.State.Meta.Revision);
            service.PendingWrite.Wait();
            service.Tick();
            Assert.IsNull(service.LastWriteError);
            service.Dispose();

            var reloaded = NewService();
            reloaded.LoadOrCreate();
            Assert.AreEqual(revision + 1, reloaded.State.Meta.Revision, "The background write reached the disk.");
        }

        [TestCase("GoldenSave_New_v1")]
        [TestCase("GoldenSave_Day7_v1")]
        [TestCase("GoldenSave_PremiumEntitlements_v1")]
        [TestCase("GoldenSave_Offline_v2")]
        [TestCase("GoldenSave_Codex_v3")]
        [TestCase("GoldenSave_Pity_v4")]
        public void GoldenSaves_LoadWithCurrentBuild(string name)
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "_Game/Tests/Fixtures/GoldenSaves", name + ".pwsave");
            Assert.IsTrue(File.Exists(path), "Missing golden save " + path);
            File.Copy(path, Path.Combine(_dir, "profile0.pwsave"));

            var service = NewService();
            var result = service.LoadOrCreate();
            Assert.AreEqual(SaveLoadOutcome.Loaded, result.Outcome, string.Join("; ", result.Problems));
            var state = service.State;
            if (name.EndsWith("_v1"))
            {
                Assert.AreEqual(1, result.FromSchemaVersion);
                Assert.AreEqual(SaveSchema.CurrentVersion - 1, result.MigrationsApplied);
                Assert.IsFalse(state.Offline.HasRate, "Schema 1 has no exit snapshot: the rate is rebuilt.");
            }
            if (!name.EndsWith("_v4"))
                Assert.AreEqual(0, state.Pity.NewSpeciesMisses.Count + state.Pity.VariantMisses.Count, "Older saves start without pity.");
            if (!name.EndsWith("_v3") && !name.EndsWith("_v4"))
            {
                Assert.IsTrue(state.Codex.SeenSpecies.SetEquals(state.Collection.DiscoveredSpecies), "Older saves: everything found counts as seen.");
                Assert.IsTrue(state.Codex.SeenVariants.SetEquals(state.Collection.DiscoveredVariants));
            }

            switch (name)
            {
                case "GoldenSave_New_v1":
                    Assert.AreEqual(new Guid("6f0c1a2e-0000-4000-8000-000000000001"), state.Meta.ProfileId);
                    Assert.AreEqual(0, state.Collection.Count);
                    Assert.IsTrue(state.Wallet.Light.IsZero);
                    break;

                case "GoldenSave_Day7_v1":
                    Assert.AreEqual(42, state.Meta.Revision);
                    Assert.AreEqual(new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero), state.Meta.LastSaveUtc);
                    Assert.IsTrue(BigValue.ApproximatelyEqual(BigValue.FromDouble(1234500), state.Wallet.Light, 1e-12));
                    Assert.AreEqual(50, state.Wallet.Gems);
                    Assert.AreEqual(120, state.Collection.Count);
                    Assert.AreEqual(121u, state.Collection.NextInstanceId);
                    Assert.IsTrue(state.Collection.TryGet(1, out var first));
                    Assert.AreEqual(ContentId.FromSource("species.rabbit"), first.Species);
                    Assert.AreEqual(1000003u, first.Seed);
                    Assert.IsTrue(first.IsFavorite);
                    Assert.AreEqual("Mochi", state.Collection.CustomNames[1]);
                    Assert.AreEqual("Pip", state.Collection.CustomNames[2]);
                    Assert.AreEqual(3, state.Collection.DiscoveredSpecies.Count);
                    Assert.AreEqual(5, state.Mastery.GetLevel(ContentId.FromSource("species.rabbit")));
                    Assert.AreEqual(37, state.World.EggPurchasesInCycle[ContentId.FromSource("egg.grassland.basic")]);
                    Assert.AreEqual("fr", state.Settings.LanguageCode);
                    Assert.IsTrue(state.Settings.ReduceMotion);
                    break;

                case "GoldenSave_Offline_v2":
                    Assert.AreEqual(77, state.Meta.Revision);
                    Assert.IsTrue(state.Offline.HasRate);
                    Assert.AreEqual(12.5, state.Offline.RateAtSave.ToDoubleClamped());
                    Assert.AreEqual(new DateTimeOffset(2026, 10, 14, 0, 3, 0, TimeSpan.Zero), state.Offline.HighWaterUtc);
                    Assert.AreEqual(1, state.Collection.Count);
                    break;

                case "GoldenSave_Codex_v3":
                    Assert.AreEqual(90, state.Meta.Revision);
                    Assert.AreEqual(2, state.Collection.Count);
                    Assert.AreEqual(1, state.Codex.SeenSpecies.Count);
                    Assert.AreEqual(1, state.Codex.SeenVariants.Count);
                    Assert.AreEqual(2, state.Collection.DiscoveredVariants.Count, "White rabbit found, not seen yet.");
                    break;

                case "GoldenSave_Pity_v4":
                    Assert.AreEqual(120, state.Meta.Revision);
                    Assert.AreEqual(13, state.Pity.NewSpeciesMisses[ContentId.FromSource("egg.grassland.basic")]);
                    Assert.AreEqual(7, state.Pity.VariantMisses[ContentId.FromSource("species.rabbit")]);
                    Assert.AreEqual(1, state.Codex.SeenSpecies.Count);
                    break;

                case "GoldenSave_PremiumEntitlements_v1":
                    Assert.AreEqual(1250, state.Wallet.Gems);
                    Assert.IsTrue(state.Entitlements.Owned.Contains(ContentId.FromSource("offer.starter.001")));
                    Assert.IsTrue(state.Entitlements.ProcessedTransactions.Contains("GPA.0000-0000-0000-00001"));
                    break;
            }
        }

        [Test]
        public void Collection50k_SavesAndLoads_Compactly()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            state.Collection.EnsureCapacity(50_016);
            var species = new[] { ContentId.FromSource("species.rabbit"), ContentId.FromSource("species.squirrel"), ContentId.FromSource("species.hedgehog") };
            for (int i = 0; i < 50_000; i++)
            {
                state.Collection.Add(new AnimalInstance
                {
                    Species = species[i % 3],
                    Seed = (uint)i * 2654435761u,
                    Rarity = (RarityTier)(i % 6),
                    BondPoints = (ushort)(i % 1000),
                });
            }

            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter(2 * 1024 * 1024);
            var watch = Stopwatch.StartNew();
            serializer.Serialize(state, writer);
            double serializeMs = watch.Elapsed.TotalMilliseconds;
            var bytes = writer.WrittenSpan.ToArray();

            watch.Restart();
            var loaded = serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
            double loadMs = watch.Elapsed.TotalMilliseconds;

            watch.Restart();
            var economy = new EconomyService(loaded, TestUtil.ThreeSpecies(), EconomyConfig.Default, null);
            double aggregateMs = watch.Elapsed.TotalMilliseconds;

            UnityEngine.Debug.Log($"[Benchmark] 50k animals: {bytes.Length / 1024} KB, serialize {serializeMs:F1} ms, load {loadMs:F1} ms, aggregate rebuild {aggregateMs:F1} ms");
            Assert.AreEqual(50_000, loaded.Collection.Count);
            Assert.Less(bytes.Length, 1_600_000, "About 28 bytes per animal.");
            Assert.Greater(economy.GetLightPerSecond().ToDoubleClamped(), 0.0);
        }

        private sealed class AddSectionMigration : ISaveMigration
        {
            public int FromVersion => 1;
            public int ToVersion => 2;
            public void Apply(SaveDocument document) => document.SetSection(new SaveSection((SaveSectionId)900, 1, new byte[] { 1 }));
        }

        private sealed class SkipMigration : ISaveMigration
        {
            public int FromVersion => 1;
            public int ToVersion => 3;
            public void Apply(SaveDocument document)
            {
            }
        }
    }
}
