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
            state.World.Biomes.Add(new BiomeProgress
            {
                Biome = ContentId.FromSource("biome.grassland"), Unlocked = true, Expansions = 4, ZonesOpened = 0b11111u,
                MasteryXp = 4_321, MasteryLevel = 10, ProductionLevel = 27, NestsLevel = 9, CuriosityLevel = 3,
            });
            state.Progression.Refuge.Score = 512;
            state.Progression.Refuge.Level = 11;
            state.Progression.Refuge.LastRewardedLevel = 9;
            state.Progression.Refuge.PendingEssenceFromLevels = 5;
            state.Progression.DecorationCopies[ContentId.FromSource("decor.bench_cute")] = 3;
            state.Progression.TreatStand.DayKey = 20261007;
            state.Progression.TreatStand.Slot0 = ContentId.FromSource("treat.clover_crunch");
            state.Progression.TreatStand.Slot2 = ContentId.FromSource("treat.toasted_hazelnut");
            state.Progression.TreatStand.BoughtMask = 0b101;
            state.Progression.Treats[ContentId.FromSource("treat.clover_crunch")] = 4;
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
            var daily = state.DailyVisit;
            daily.LastDayKey = 20261007;
            daily.LoginDays = 4;
            daily.OnboardingDone = true;
            daily.TutorialStep = 3;
            daily.WelcomeClaimed = 2;
            daily.GiftsOpened = 3;
            daily.GiftsSinceBig = 2;
            daily.CalendarNext = 27;
            daily.CalendarCycle = 2;
            daily.CalendarStamps = 55;
            daily.Wishes[0] = new WishRecord
            {
                Template = 7, Animal = 4, Param = ContentId.FromSource("species.rabbit"), ParamKind = 1, ParamExtra = 2, Target = 5,
                Progress = 2, DrawnDay = 20261006, Flags = WishRecord.FlagTutorial,
            };
            daily.WishCount = 1;
            daily.WishGemsTotal = 90;
            daily.History[0] = new WishHistoryEntry { Template = 7, DayKey = 20261006 };
            daily.HistoryCount = 1;
            daily.HistoryNext = 1;
            daily.SystemUnlockDay[3] = 5;
            daily.TouchedForWish[0] = 4;
            daily.TouchedForWish[1] = 6;
            daily.TouchedCount = 2;
            daily.Goals[0] = new JournalGoalRecord { Goal = 52, Slot = 4, Target = 9, Progress = 9, Claimed = true };
            daily.GoalCount = 1;
            daily.JournalCompleteClaimed = true;
            daily.PreviousGoals[0] = 51;
            daily.PreviousGoalCount = 1;
            daily.CarryTickets = 2;
            daily.CarryDiscovery = 1;
            daily.LastBigReturnGiftDay = 30;
            daily.PendingReturnGift = 2;
            daily.PendingReturnDays = 15;
            daily.TouchDayKey = 20261007;
            daily.TouchesToday = 3;
            daily.TouchesByAnimal[4] = 3;
            state.Inventory.Tickets = 5;
            state.Inventory.Add(ContentId.FromSource("plant.daisy"), 2);
            var activities = state.Activities;
            activities.TicketsStarted = true;
            activities.FreeTickets = 6;
            activities.PaidTickets = 1;
            activities.RefillAnchorUnixMs = 1_791_000_000_123L;
            activities.PackDayKey = 20261007;
            activities.PacksToday = 2;
            activities.GemsDayKey = 20261007;
            activities.GemsToday = 6;
            activities.ChallengesThreeStars = 11;
            activities.ChallengeDecorations = 2;
            activities.GamesStarted = 140;
            activities.Records[(int)ActivityId.HideAndSeek - 1] = new ActivityRecord
            {
                Unlocked = true, Best = 9, BestStars = 3, Difficulty = 10, ZeroStarStreak = 1, ThreeStarStreak = 2, GamesPlayed = 70,
                RewardedGames = 41, FreeGameUsed = true, ThreeStarGemsDayKey = 20261006, LastTheme = ContentId.FromSource("biome.lake"),
                PaidStickerPoints = 95,
            };
            activities.ChallengeStars[ActivityState.ChallengeIndex((int)ActivityId.GrandContest - 1, 9)] = 2;
            activities.ChallengeAttempts[ActivityState.ChallengeIndex((int)ActivityId.GrandContest - 1, 9)] = 65_000;
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
            var grassland = loaded.World.Biomes[0];
            Assert.AreEqual(4, grassland.Expansions);
            Assert.AreEqual(0b11111u, grassland.ZonesOpened);
            Assert.AreEqual(4_321, grassland.MasteryXp);
            Assert.AreEqual(10, grassland.MasteryLevel);
            Assert.AreEqual(27, grassland.ProductionLevel);
            Assert.AreEqual(9, grassland.NestsLevel);
            Assert.AreEqual(3, grassland.CuriosityLevel);
            var progression = loaded.Progression;
            Assert.AreEqual(512, progression.Refuge.Score);
            Assert.AreEqual(11, progression.Refuge.Level);
            Assert.AreEqual(9, progression.Refuge.LastRewardedLevel);
            Assert.AreEqual(5, progression.Refuge.PendingEssenceFromLevels);
            Assert.AreEqual(3, progression.DecorationCopies[ContentId.FromSource("decor.bench_cute")]);
            Assert.AreEqual(20261007, progression.TreatStand.DayKey);
            Assert.AreEqual(ContentId.FromSource("treat.clover_crunch"), progression.TreatStand.Slot0);
            Assert.AreEqual(ContentId.None, progression.TreatStand.Slot1);
            Assert.AreEqual(ContentId.FromSource("treat.toasted_hazelnut"), progression.TreatStand.Slot2);
            Assert.AreEqual(0b101, progression.TreatStand.BoughtMask);
            Assert.AreEqual(4, progression.Treats[ContentId.FromSource("treat.clover_crunch")]);
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

            var daily = loaded.DailyVisit;
            Assert.AreEqual(20261007, daily.LastDayKey);
            Assert.AreEqual(4, daily.LoginDays);
            Assert.IsTrue(daily.OnboardingDone);
            Assert.AreEqual(3, daily.TutorialStep);
            Assert.AreEqual(2, daily.WelcomeClaimed);
            Assert.AreEqual(3, daily.GiftsOpened);
            Assert.AreEqual(2, daily.GiftsSinceBig);
            Assert.AreEqual(27, daily.CalendarNext);
            Assert.AreEqual(2, daily.CalendarCycle);
            Assert.AreEqual(55, daily.CalendarStamps);
            Assert.AreEqual(1, daily.WishCount);
            var wish = daily.Wishes[0];
            Assert.AreEqual(7, wish.Template);
            Assert.AreEqual(4u, wish.Animal);
            Assert.AreEqual(ContentId.FromSource("species.rabbit"), wish.Param);
            Assert.AreEqual(1, wish.ParamKind);
            Assert.AreEqual(2, wish.ParamExtra);
            Assert.AreEqual(5, wish.Target);
            Assert.AreEqual(2, wish.Progress);
            Assert.AreEqual(20261006, wish.DrawnDay);
            Assert.IsTrue(wish.IsTutorial);
            Assert.AreEqual(90L, daily.WishGemsTotal);
            Assert.AreEqual(1, daily.HistoryCount);
            Assert.AreEqual(1, daily.HistoryNext);
            Assert.AreEqual(7, daily.History[0].Template);
            Assert.AreEqual(5, daily.SystemUnlockDay[3]);
            Assert.AreEqual(2, daily.TouchedCount);
            Assert.AreEqual(6u, daily.TouchedForWish[1]);
            Assert.AreEqual(1, daily.GoalCount);
            Assert.AreEqual(52, daily.Goals[0].Goal);
            Assert.AreEqual(4, daily.Goals[0].Slot);
            Assert.IsTrue(daily.Goals[0].Claimed);
            Assert.IsTrue(daily.JournalCompleteClaimed);
            Assert.AreEqual(51, daily.PreviousGoals[0]);
            Assert.AreEqual(2, daily.CarryTickets);
            Assert.AreEqual(1, daily.CarryDiscovery);
            Assert.AreEqual(30, daily.LastBigReturnGiftDay);
            Assert.AreEqual(2, daily.PendingReturnGift);
            Assert.AreEqual(15, daily.PendingReturnDays);
            Assert.AreEqual(3, daily.TouchesToday);
            Assert.AreEqual(3, daily.TouchesByAnimal[4]);
            Assert.AreEqual(5, loaded.Inventory.Tickets);
            Assert.AreEqual(2, loaded.Inventory.Count(ContentId.FromSource("plant.daisy")));

            var act = loaded.Activities;
            Assert.IsTrue(act.TicketsStarted);
            Assert.AreEqual(6, act.FreeTickets);
            Assert.AreEqual(1, act.PaidTickets);
            Assert.AreEqual(1_791_000_000_123L, act.RefillAnchorUnixMs);
            Assert.AreEqual(20261007, act.PackDayKey);
            Assert.AreEqual(2, act.PacksToday);
            Assert.AreEqual(20261007, act.GemsDayKey);
            Assert.AreEqual(6, act.GemsToday);
            Assert.AreEqual(11, act.ChallengesThreeStars);
            Assert.AreEqual(2, act.ChallengeDecorations);
            Assert.AreEqual(140, act.GamesStarted);
            var hide = act.Records[(int)ActivityId.HideAndSeek - 1];
            Assert.IsTrue(hide.Unlocked && hide.FreeGameUsed);
            Assert.AreEqual(9, hide.Best);
            Assert.AreEqual(3, hide.BestStars);
            Assert.AreEqual(10, hide.Difficulty);
            Assert.AreEqual(1, hide.ZeroStarStreak);
            Assert.AreEqual(2, hide.ThreeStarStreak);
            Assert.AreEqual(70, hide.GamesPlayed);
            Assert.AreEqual(41, hide.RewardedGames);
            Assert.AreEqual(20261006, hide.ThreeStarGemsDayKey);
            Assert.AreEqual(ContentId.FromSource("biome.lake"), hide.LastTheme);
            Assert.AreEqual(95, hide.PaidStickerPoints);
            Assert.AreEqual(2, act.ChallengeStars[ActivityState.ChallengeIndex((int)ActivityId.GrandContest - 1, 9)]);
            Assert.AreEqual(65_000, act.ChallengeAttempts[ActivityState.ChallengeIndex((int)ActivityId.GrandContest - 1, 9)]);
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
        [TestCase("GoldenSave_Shop_v5")]
        [TestCase("GoldenSave_Analytics_v6")]
        [TestCase("GoldenSave_Progression_v7")]
        [TestCase("GoldenSave_Eggs_v8")]
        [TestCase("GoldenSave_Renewal_v9")]
        [TestCase("GoldenSave_DailyVisit_v10")]
        [TestCase("GoldenSave_Activities_v11")]
        [TestCase("GoldenSave_Expeditions_v12")]
        [TestCase("GoldenSave_Garden_v13")]
        public void GoldenSaves_LoadWithCurrentBuild(string name)
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "_Game/Tests/Fixtures/GoldenSaves", name + ".pwsave");
            Assert.IsTrue(File.Exists(path), "Missing golden save " + path);
            File.Copy(path, Path.Combine(_dir, "profile0.pwsave"));

            var service = NewService();
            var result = service.LoadOrCreate();
            Assert.AreEqual(SaveLoadOutcome.Loaded, result.Outcome, string.Join("; ", result.Problems));
            var state = service.State;
            int version = int.Parse(name.Substring(name.LastIndexOf("_v", StringComparison.Ordinal) + 2));
            if (name.EndsWith("_v1"))
            {
                Assert.AreEqual(1, result.FromSchemaVersion);
                Assert.AreEqual(SaveSchema.CurrentVersion - 1, result.MigrationsApplied);
                Assert.IsFalse(state.Offline.HasRate, "Schema 1 has no exit snapshot: the rate is rebuilt.");
            }
            if (version < 4)
                Assert.AreEqual(0, state.Pity.NewSpeciesMisses.Count + state.Pity.VariantMisses.Count, "Older saves start without pity.");
            if (version < 5)
                Assert.AreEqual(0, state.Shop.Offers.Count + state.Shop.PaidPurchases, "Older saves start without shop history.");
            if (version < 8)
                Assert.AreEqual(0, state.World.DiscoveryEggs, "Older saves start with no Discovery egg.");
            if (version < 11)
                Assert.IsFalse(state.Activities.TicketsStarted, "Older saves start the ticket pool on first use.");
            if (version < 13)
            {
                Assert.IsFalse(state.Garden.AnyActive || state.Garden.PlotBought, "Older saves: every plot bare, the Gems plot not bought.");
                Assert.AreEqual(0, state.Garden.Points + state.Garden.Palettes + state.Garden.Dyes[0]);
                Assert.AreEqual(-1, state.Garden.Challenge);
            }
            if (version < 12)
            {
                Assert.IsFalse(state.Expeditions.AnyActive || state.Expeditions.SlotBought || state.Expeditions.FirstStrollDone,
                    "Older saves: no team away, the first stroll still to come.");
                Assert.AreEqual(0, state.Expeditions.PostcardCount + state.Expeditions.LongWithoutEgg);
            }
            if (version < 10)
            {
                Assert.AreEqual(state.Collection.Count >= 5, state.DailyVisit.OnboardingDone, "Five animals or more: the welcome is over.");
                Assert.AreEqual(0, state.DailyVisit.WishCount + state.DailyVisit.LoginDays + state.Inventory.Tickets);
                Assert.AreEqual(1, state.DailyVisit.CalendarCycle);
            }
            if (version < 6)
            {
                Assert.AreEqual(0, state.Analytics.Sessions, "Older saves start counting sessions now.");
                Assert.AreEqual(state.Collection.Count > 0, state.Analytics.Has(FunnelMilestone.FirstHatch), "Veterans never send a first hatch.");
            }
            if (version < 3)
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
                    // World 1 counted zones bought: the arrival zone plus three opened, nothing else.
                    var day7Grassland = state.World.Biomes.Find(b => b.Biome == ContentId.FromSource("biome.grassland"));
                    Assert.AreEqual(3, day7Grassland.Expansions);
                    Assert.AreEqual(0b1111u, day7Grassland.ZonesOpened);
                    Assert.AreEqual(0, day7Grassland.ProductionLevel);
                    Assert.AreEqual(1, state.Progression.Refuge.Level, "Computed by the game on load, not stored by schema 1.");
                    break;

                case "GoldenSave_Progression_v7":
                    Assert.AreEqual(210, state.Meta.Revision);
                    var grassland = state.World.Biomes.Find(b => b.Biome == ContentId.FromSource("biome.grassland"));
                    Assert.AreEqual(0b111u, grassland.ZonesOpened);
                    Assert.AreEqual(2, grassland.Expansions);
                    Assert.AreEqual(600, grassland.MasteryXp);
                    Assert.AreEqual(4, grassland.MasteryLevel);
                    Assert.AreEqual(12, grassland.ProductionLevel);
                    Assert.AreEqual(3, grassland.NestsLevel);
                    Assert.AreEqual(150, state.Progression.Refuge.Score);
                    Assert.AreEqual(5, state.Progression.Refuge.Level);
                    Assert.AreEqual(3, state.Progression.Refuge.LastRewardedLevel);
                    Assert.AreEqual(2, state.Progression.DecorationCopies[ContentId.FromSource("decor.bench_cute")]);
                    Assert.AreEqual(0b001, state.Progression.TreatStand.BoughtMask);
                    Assert.AreEqual(1, state.Progression.Treats[ContentId.FromSource("treat.clover_crunch")]);
                    Assert.AreEqual(9, state.Analytics.Sessions);
                    break;

                case "GoldenSave_Renewal_v9":
                    Assert.AreEqual(260, state.Meta.Revision);
                    Assert.AreEqual(1, state.World.RenewalCount);
                    Assert.AreEqual(7, state.Wallet.Essence);
                    Assert.AreEqual(18, state.Progression.Renewal.EssenceFromLight);
                    Assert.AreEqual(41, state.Progression.Renewal.EssenceEarned);
                    Assert.AreEqual(3, state.Progression.Renewal.SpeciesAtLast);
                    Assert.AreEqual(1, state.Progression.Renewal.BiomesAtLast);
                    Assert.AreEqual(5, state.Progression.EssenceRanks[ContentId.FromSource("essence.economy.soft_light")]);
                    Assert.AreEqual(2, state.Progression.EssenceRanks[ContentId.FromSource("essence.economy.morning_eggs")]);
                    Assert.AreEqual(4, state.World.DiscoveryEggs, "Everything of schema 8 still there.");
                    break;

                case "GoldenSave_Activities_v11":
                    Assert.AreEqual(300, state.Meta.Revision);
                    Assert.AreEqual(SaveSchema.CurrentVersion - 11, result.MigrationsApplied);
                    var activities = state.Activities;
                    Assert.IsTrue(activities.TicketsStarted);
                    Assert.AreEqual(3, activities.FreeTickets);
                    Assert.AreEqual(2, activities.PaidTickets);
                    Assert.AreEqual(new DateTimeOffset(2026, 10, 12, 9, 30, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(), activities.RefillAnchorUnixMs);
                    Assert.AreEqual(1, activities.PacksToday);
                    Assert.AreEqual(4, activities.GemsToday);
                    Assert.AreEqual(6, activities.ChallengesThreeStars);
                    Assert.AreEqual(23, activities.GamesStarted);
                    var groom = activities.Records[(int)ActivityId.Grooming - 1];
                    Assert.IsTrue(groom.Unlocked && groom.FreeGameUsed);
                    Assert.AreEqual(1840, groom.Best);
                    Assert.AreEqual(3, groom.BestStars);
                    Assert.AreEqual(5, groom.Difficulty);
                    Assert.AreEqual(ContentId.FromSource("biome.grassland"), groom.LastTheme);
                    Assert.AreEqual(45, groom.PaidStickerPoints);
                    Assert.AreEqual(2, activities.Records[(int)ActivityId.NutRain - 1].BestStars);
                    Assert.AreEqual(6, activities.ChallengesDone((int)ActivityId.Grooming - 1));
                    Assert.AreEqual(4, activities.ChallengeAttempts[ActivityState.ChallengeIndex((int)ActivityId.Grooming - 1, 6)]);
                    Assert.AreEqual(20261012, state.DailyVisit.LastDayKey, "Everything of schema 10 still there.");
                    break;

                case "GoldenSave_Expeditions_v12":
                    Assert.AreEqual(320, state.Meta.Revision);
                    Assert.AreEqual(SaveSchema.CurrentVersion - 12, result.MigrationsApplied);
                    var e = state.Expeditions;
                    var meadows = ContentId.FromSource("expedition.flower_meadows");
                    Assert.IsTrue(e.SlotBought && e.FirstStrollDone);
                    Assert.AreEqual(7, e.LongWithoutEgg);
                    Assert.AreEqual(15, e.Started);
                    Assert.AreEqual(13, e.Completed);
                    Assert.AreEqual(2, e.SpeedUpsToday);
                    Assert.AreEqual(14, e.NextPostcardId);
                    Assert.AreEqual(1UL, e.JournalPlacesMask);
                    var away = e.Slots[0];
                    Assert.IsTrue(away.Active);
                    Assert.AreEqual(meadows, away.Destination);
                    Assert.AreEqual(ExpeditionDuration.EightHours, away.Duration);
                    Assert.AreEqual(2, away.TeamSize);
                    Assert.AreEqual(2u, away.Animal1);
                    Assert.AreEqual(ContentId.FromSource("variant.rabbit.white"), away.Variant1);
                    Assert.AreEqual(new DateTimeOffset(2026, 10, 12, 16, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(), away.ReturnUnixMs);
                    Assert.AreEqual(3, away.Challenge);
                    Assert.AreEqual(123457u, away.Seed);
                    Assert.AreEqual(99.0, away.Loot.LightMinutes);
                    Assert.AreEqual(2, away.Loot.SeedKinds);
                    Assert.AreEqual(ContentId.FromSource("plant.daisy"), away.Loot.Seed1);
                    Assert.AreEqual(1, away.Loot.Stickers);
                    Assert.AreEqual(ContentId.FromSource("ingredient.gentle_honey"), away.Loot.Ingredient0);
                    Assert.IsFalse(e.Slots[1].Active);
                    Assert.IsTrue(e.Slots[2].Active);
                    Assert.AreEqual(-1, e.Slots[2].Challenge);
                    Assert.AreEqual(3u, e.Slots[2].Animal0);
                    Assert.IsTrue(e.IsAway(1) && e.IsAway(3) && !e.IsAway(4));
                    Assert.AreEqual(3, e.PostcardCount);
                    Assert.IsTrue(e.Postcards[0].FirstOfDestination && e.Postcards[0].Rendered);
                    Assert.AreEqual(13, e.Postcards[2].Id);
                    Assert.IsFalse(e.Postcards[2].Rendered);
                    Assert.AreEqual(6, state.Activities.ChallengesThreeStars, "Everything of schema 11 still there.");
                    break;

                case "GoldenSave_Garden_v13":
                    Assert.AreEqual(340, state.Meta.Revision);
                    Assert.AreEqual(SaveSchema.CurrentVersion - 13, result.MigrationsApplied);
                    var g = state.Garden;
                    long planted = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
                    Assert.IsTrue(g.PlotBought && g.FirstVisitDone);
                    Assert.AreEqual(230, g.Points);
                    Assert.AreEqual(4, GardenRules.LevelOf(g.Points));
                    Assert.AreEqual(21, g.Planted);
                    Assert.AreEqual(19, g.Harvested);
                    Assert.AreEqual(1, g.SpeedUpsToday);
                    Assert.AreEqual(3, g.Challenge);
                    Assert.AreEqual(2, g.ChallengeHarvests);
                    Assert.AreEqual(5, g.ChallengeStars);
                    Assert.AreEqual(3u, g.ChallengeMask);
                    var clover = g.Plots[0];
                    Assert.IsTrue(clover.Active && clover.Watered && clover.FlowerBeside);
                    Assert.AreEqual(ContentId.FromSource("plant.sweet_clover"), clover.Plant);
                    Assert.AreEqual(planted, clover.PlantedUnixMs);
                    Assert.AreEqual(planted + 27 * 60_000L, clover.RipeUnixMs);
                    Assert.AreEqual(30 * 60_000L, clover.GrowMs);
                    Assert.AreEqual(424242u, clover.Seed);
                    Assert.AreEqual(1, clover.SeedsBack);
                    Assert.AreEqual(600_000L, clover.HelpRightMs);
                    Assert.IsTrue(g.Plots[1].Active && g.Plots[1].Happy && !g.Plots[1].Watered);
                    Assert.IsFalse(g.Plots[2].Active);
                    Assert.AreEqual(7, g.Dyes[0]);
                    Assert.AreEqual(3, g.Dyes[8]);
                    Assert.AreEqual((1u << 0) | (1u << 25), g.Palettes);
                    Assert.AreEqual(4, state.Inventory.Count(ContentId.FromSource("plant.sweet_clover")));
                    Assert.AreEqual(9, state.Inventory.Count(ContentId.FromSource("crop.clover_flower")));
                    Assert.IsTrue(state.Expeditions.SlotBought, "Everything of schema 12 still there.");
                    break;

                case "GoldenSave_DailyVisit_v10":
                    Assert.AreEqual(280, state.Meta.Revision);
                    Assert.AreEqual(SaveSchema.CurrentVersion - 10, result.MigrationsApplied);
                    var daily = state.DailyVisit;
                    Assert.AreEqual(20261012, daily.LastDayKey);
                    Assert.AreEqual(9, daily.LoginDays);
                    Assert.IsTrue(daily.OnboardingDone);
                    Assert.AreEqual(5, daily.CalendarNext);
                    Assert.AreEqual(2, daily.WishCount);
                    Assert.IsTrue(daily.Wishes[1].IsDone);
                    Assert.AreEqual(ContentId.FromSource("biome.grassland"), daily.Wishes[1].Param);
                    Assert.AreEqual(3, daily.GoalCount);
                    Assert.IsTrue(daily.Goals[1].Claimed);
                    Assert.AreEqual(10, daily.CarryGems);
                    Assert.AreEqual(1, daily.PendingReturnGift);
                    Assert.AreEqual(4, daily.TouchesByAnimal[1]);
                    Assert.AreEqual(2, state.Inventory.Tickets);
                    Assert.AreEqual(3, state.Inventory.Count(ContentId.FromSource("ingredient.flower_sugar")));
                    Assert.AreEqual(1, state.World.RenewalCount, "Everything of schema 9 still there.");
                    break;

                case "GoldenSave_Eggs_v8":
                    Assert.AreEqual(240, state.Meta.Revision);
                    Assert.AreEqual(4, state.World.DiscoveryEggs);
                    Assert.AreEqual(6, state.World.EggPurchasesInCycle[ContentId.FromSource("egg.grassland.focus")]);
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

                case "GoldenSave_Shop_v5":
                    Assert.AreEqual(150, state.Meta.Revision);
                    Assert.AreEqual(620, state.Wallet.Gems);
                    Assert.AreEqual(1, state.Shop.PaidPurchases);
                    var starter = state.Shop.Get(ContentId.FromSource("offer.starter.001"));
                    Assert.AreEqual(1, starter.Purchases);
                    Assert.AreEqual(2, starter.Impressions);
                    Assert.AreEqual(1791000000L, starter.LastShownUnix);
                    Assert.IsTrue(state.Entitlements.Owned.Contains(ContentId.FromSource("cosmetic.flower_crown")));
                    Assert.IsTrue(state.Entitlements.ProcessedTransactions.Contains("sandbox-0001"));
                    Assert.AreEqual(13, state.Pity.NewSpeciesMisses[ContentId.FromSource("egg.grassland.basic")]);
                    break;

                case "GoldenSave_Analytics_v6":
                    Assert.AreEqual(180, state.Meta.Revision);
                    Assert.AreEqual(9, state.Analytics.Sessions);
                    Assert.IsTrue(state.Analytics.Has(FunnelMilestone.FirstPurchase));
                    Assert.IsTrue(state.Analytics.Has(FunnelMilestone.CodexOpen));
                    Assert.IsFalse(state.Analytics.Has(FunnelMilestone.FirstRare), "Stored milestones are not re-inferred.");
                    Assert.AreEqual(620, state.Wallet.Gems);
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
