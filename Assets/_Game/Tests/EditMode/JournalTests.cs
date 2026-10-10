using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.UI;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T44C: shared content for the journal and visitor tests (rabbit and hedgehog with their visitor variants).</summary>
    internal static class JournalTestContent
    {
        public static readonly DateTimeOffset Start = new DateTimeOffset(2026, 10, 10, 6, 0, 0, TimeSpan.Zero);

        /// <summary>Rabbit (brown, white, the meadow and snow visitors), hedgehog (brown, the flower visitor), squirrel (no
        /// visitor variant); <paramref name="withOwl"/> adds the autumn owl and its russet visitor.</summary>
        public static ContentRegistry Build(bool withOwl = false)
        {
            var builder = new ContentRegistryBuilder();
            builder.AddBiome(new BiomeDefinition(ContentId.FromSource("biome.grassland"), "biome.grassland.name", 0), "biome.grassland");
            Species(builder, "rabbit");
            Variant(builder, "variant.rabbit.brown", "rabbit", RarityTier.Common, false);
            Variant(builder, "variant.rabbit.white", "rabbit", RarityTier.Rare, false);
            Variant(builder, "variant.rabbit.meadow_visitor", "rabbit", RarityTier.Rare, true);
            Variant(builder, "variant.rabbit.snow_visitor", "rabbit", RarityTier.Rare, true);
            Species(builder, "hedgehog");
            Variant(builder, "variant.hedgehog.brown", "hedgehog", RarityTier.Common, false);
            Variant(builder, "variant.hedgehog.flower_visitor", "hedgehog", RarityTier.Rare, true);
            Species(builder, "squirrel");
            Variant(builder, "variant.squirrel.red", "squirrel", RarityTier.Common, false);
            if (withOwl)
            {
                Species(builder, "owl");
                Variant(builder, "variant.owl.brown", "owl", RarityTier.Common, false);
                Variant(builder, "variant.owl.russet_visitor", "owl", RarityTier.Rare, true);
            }
            return builder.Build();
        }

        private static void Species(ContentRegistryBuilder builder, string name)
        {
            string id = "species." + name;
            builder.AddSpecies(new SpeciesDefinition(ContentId.FromSource(id), id + ".name", RigFamily.SmallQuadruped,
                ContentId.FromSource("biome.grassland"), 1.0, "PF_Animal_Test"), id);
        }

        private static void Variant(ContentRegistryBuilder builder, string id, string species, RarityTier rarity, bool visitor)
        {
            builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), ContentId.FromSource("species." + species), id + ".name",
                rarity, new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0, isSecret: visitor, isVisitor: visitor), id);
        }

        public static uint Own(PlayerState state, string variant, string species, RarityTier rarity = RarityTier.Common, uint traits = 0)
        {
            var animal = new AnimalInstance
            {
                Species = ContentId.FromSource(species),
                Variant = ContentId.FromSource(variant),
                Rarity = rarity,
                TraitBits = traits,
            };
            animal.InstanceId = state.Collection.Add(animal);
            state.Collection.DiscoveredSpecies.Add(animal.Species);
            state.Collection.DiscoveredVariants.Add(animal.Variant);
            return animal.InstanceId;
        }
    }

    public class JournalTests
    {
        private static JournalService NewJournal(out PlayerState state, out FakeGameClock clock)
        {
            state = new PlayerState();
            clock = new FakeGameClock(JournalTestContent.Start);
            return new JournalService(state.Journal, clock, state) { Zone = TimeZoneInfo.Utc };
        }

        // ------------------------------------------------------------------------------------------- data

        [Test]
        public void Data_Holds63Moments_FromTheDocTable()
        {
            Assert.AreEqual(63, JournalData.Count);
            var keys = new HashSet<string>();
            int repeatable = 0;
            for (int i = 0; i < JournalData.Count; i++)
            {
                var row = JournalData.Row(i);
                StringAssert.StartsWith("memory.", row.Key);
                Assert.IsTrue(keys.Add(row.Key), row.Key);
                Assert.LessOrEqual(row.Variables.Length, 2, row.Key);
                Assert.AreEqual(i, JournalData.IndexOf(row.Key));
                if (row.Repeatable)
                    repeatable++;
            }
            Assert.AreEqual(24, repeatable);
            Assert.AreEqual(0, JournalMoment.FirstEgg);
            Assert.AreEqual(62, JournalMoment.FirstCircle);
            Assert.AreEqual(JournalImageKind.RefugeCapture, JournalData.Row(JournalMoment.FirstEgg).Image);
            Assert.AreEqual(JournalImageKind.AnimalCapture, JournalData.Row(JournalMoment.FirstHatch).Image);
            Assert.AreEqual(JournalImageKind.Thumbnail, JournalData.Row(JournalMoment.BiomeUnlocked).Image);
            Assert.AreEqual(JournalImageKind.Postcard, JournalData.Row(JournalMoment.Postcard).Image);
            Assert.AreEqual(JournalImageKind.Photo, JournalData.Row(JournalMoment.PlayerPhoto).Image);
            Assert.IsTrue(JournalData.Row(JournalMoment.NewSpecies).Repeatable);
            Assert.IsFalse(JournalData.Row(JournalMoment.FirstHatch).Repeatable);
            CollectionAssert.AreEqual(new[] { "parent1", "parent2" }, JournalData.Row(JournalMoment.FirstBreeding).Variables);
            CollectionAssert.AreEqual(new[] { "activity", "score" }, JournalData.Row(JournalMoment.FirstRecord).Variables);
            Assert.AreEqual(JournalSystem.Visitors, JournalData.Row(JournalMoment.VisitorStays).System);
        }

        [Test]
        public void Texts_EveryMomentAndVisitor_EnglishAndFrench_NoEmDashOrSparkle()
        {
            var texts = JournalTexts.Texts;
            var keys = new Dictionary<string, int>();
            for (int r = 0; r < texts.GetLength(0); r++)
                keys[texts[r, 0]] = r;
            for (int i = 0; i < JournalData.Count; i++)
            {
                var row = JournalData.Row(i);
                Assert.IsTrue(keys.TryGetValue(row.Key + ".text", out int r), row.Key);
                for (int v = 0; v < row.Variables.Length; v++)
                {
                    StringAssert.Contains("{" + row.Variables[v] + "}", texts[r, 1], row.Key);
                    StringAssert.Contains("{" + row.Variables[v] + "}", texts[r, 2], row.Key);
                }
            }
            for (int i = 0; i < VisitorData.Count; i++)
                Assert.IsTrue(keys.ContainsKey(VisitorData.Row(i).Key + ".name"), VisitorData.Row(i).Key);
            for (int r = 0; r < texts.GetLength(0); r++)
                for (int c = 1; c < 3; c++)
                    foreach (char ch in texts[r, c])
                        Assert.IsFalse(ch == (char)0x2014 || ch == (char)0x2728 || ch == (char)0x2726 || ch == (char)0x2727, texts[r, 0]);
        }

        // ------------------------------------------------------------------------------------------- pages

        [Test]
        public void FirstTime_Once_Repeatable_EachTime_Subjects_OncePerSubject()
        {
            var journal = NewJournal(out var state, out _);
            int added = 0;
            journal.PageAdded += () => added++;
            Assert.IsTrue(journal.Add(JournalMoment.FirstHatch, 0, 1, "Pip"));
            Assert.IsFalse(journal.Add(JournalMoment.FirstHatch, 0, 2, "Other"), "A first time is written once.");
            Assert.IsTrue(journal.Add(JournalMoment.PlayerPhoto, 0, 7, "10/10/2026"));
            Assert.IsTrue(journal.Add(JournalMoment.PlayerPhoto, 0, 8, "10/10/2026"), "A repeatable moment, each time.");
            Assert.IsTrue(journal.Add(JournalMoment.NewSpecies, 55, 1));
            Assert.IsFalse(journal.Add(JournalMoment.NewSpecies, 55, 1), "Once per subject.");
            Assert.IsTrue(journal.Add(JournalMoment.Population, 100));
            Assert.IsTrue(journal.Add(JournalMoment.Population, 250), "Each population step once.");
            Assert.IsFalse(journal.Add(JournalMoment.Population, 100));
            Assert.AreEqual(6, journal.PageCount);
            Assert.AreEqual(6, added);
            Assert.AreEqual(6, journal.UnseenCount);

            var newest = journal.PageAt(0);
            Assert.AreEqual("memory.population", newest.Key);
            Assert.AreEqual(6, newest.Id);
            var oldest = journal.PageAt(5);
            Assert.AreEqual("memory.first_hatch", oldest.Key);
            Assert.AreEqual(1u, oldest.ImageRef);
            Assert.AreEqual(JournalService.DayOf(JournalTestContent.Start, TimeZoneInfo.Utc), oldest.Day);
            Assert.AreEqual(new DateTime(2026, 10, 10), JournalService.DateOf(oldest.Day));

            var names = new string[4];
            var values = new string[4];
            Assert.AreEqual(1, journal.Variables(oldest.Id, names, values));
            Assert.AreEqual("name", names[0]);
            Assert.AreEqual("Pip", values[0]);
            journal.Add(JournalMoment.FirstRecord, 3, 3, "#activity.nut_drop.name", "120");
            Assert.AreEqual(2, journal.Variables(journal.PageAt(0).Id, names, values));
            Assert.AreEqual("activity", names[0]);
            Assert.AreEqual("#activity.nut_drop.name", values[0]);
            Assert.AreEqual("score", names[1]);
            Assert.AreEqual("120", values[1]);
            Assert.AreEqual(0, journal.Variables(999, names, values));

            journal.MarkSeen();
            Assert.AreEqual(0, journal.UnseenCount);
        }

        [Test]
        public void CaptureQueue_HoldsEight_OlderFallBack_StoredOnes_LeaveTheQueue()
        {
            var journal = NewJournal(out _, out _);
            for (uint s = 1; s <= 10; s++)
                journal.Add(JournalMoment.NewSpecies, s, 100 + s);
            Assert.AreEqual(JournalService.MaxPending, journal.PendingCaptureCount);
            Assert.AreEqual(3, journal.PendingCaptureAt(0).Id, "The two oldest gave way.");
            Assert.IsFalse(journal.PageAt(9).Captured);
            Assert.AreEqual(0, journal.State.Pages[0].Flags & JournalEntry.FlagPending);
            Assert.AreNotEqual(0, journal.State.Pages[0].Flags & JournalEntry.FlagThumbnail);

            journal.CaptureStored(5);
            Assert.AreEqual(7, journal.PendingCaptureCount);
            Assert.IsTrue(journal.PageAt(10 - 5).Captured);
            Assert.AreEqual(1, journal.StoredCaptureCount);
            journal.CaptureStored(5);
            Assert.AreEqual(1, journal.StoredCaptureCount, "Stored once.");

            journal.CaptureFailed(6);
            Assert.AreEqual(6, journal.PendingCaptureCount);

            // A thumbnail moment never waits; a portrait without its animal falls back at once.
            journal.Add(JournalMoment.BiomeUnlocked, 9, 9);
            journal.Add(JournalMoment.FirstName, 0, 0, "Pip");
            Assert.AreEqual(6, journal.PendingCaptureCount);
        }

        [Test]
        public void Captures_300Cap_DropsTheOldestRepeatable_FirstTimesKeepTheirs()
        {
            var journal = NewJournal(out _, out _);
            var dropped = new List<int>();
            journal.CaptureDropped += dropped.Add;
            journal.Add(JournalMoment.FirstHatch, 0, 1, "Pip");
            journal.CaptureStored(1);
            for (uint s = 1; s <= JournalService.MaxCaptures; s++)
            {
                journal.Add(JournalMoment.NewSpecies, s, s);
                journal.CaptureStored(journal.PageAt(0).Id);
            }
            Assert.AreEqual(JournalService.MaxCaptures, journal.StoredCaptureCount);
            Assert.AreEqual(1, dropped.Count);
            Assert.AreEqual(2, dropped[0], "The oldest repeatable capture went back to its thumbnail.");
            Assert.IsTrue(journal.PageAt(journal.PageCount - 1).Captured, "The first hatch keeps its capture.");
            Assert.IsFalse(journal.PageAt(journal.PageCount - 2).Captured);
            Assert.AreEqual(JournalService.MaxCaptures + 1, journal.PageCount, "The page and its text stay.");
        }

        [Test]
        public void SeenSurprises_KeptOnce()
        {
            var journal = NewJournal(out _, out _);
            Assert.AreEqual(0, journal.SeenSurpriseCount);
            Assert.IsTrue(journal.AddSeenSurprise("surprise.visitor_arrival"));
            Assert.IsFalse(journal.AddSeenSurprise("surprise.visitor_arrival"));
            Assert.AreEqual(1, journal.SeenSurpriseCount);
            Assert.AreEqual("surprise.visitor_arrival", journal.SeenSurpriseAt(0));
        }

        // ------------------------------------------------------------------------------------------- hooks

        [Test]
        public void Hooks_NewAccount_FirstEgg_ThenHatchMoments()
        {
            var journal = NewJournal(out var state, out var clock);
            var content = JournalTestContent.Build();
            using var hooks = new JournalHooks(journal, state, clock, content);
            Assert.AreEqual(1, journal.PageCount);
            Assert.AreEqual("memory.first_egg", journal.PageAt(0).Key);

            uint a = JournalTestContent.Own(state, "variant.rabbit.brown", "species.rabbit");
            state.Collection.TryGet(a, out var animal);
            hooks.OnObtained(in animal, true, true);
            Assert.AreEqual("memory.new_species", journal.PageAt(0).Key);
            var names = new string[2];
            var values = new string[2];
            journal.Variables(journal.PageAt(0).Id, names, values);
            Assert.AreEqual("#species.rabbit.name", values[0]);

            uint b = JournalTestContent.Own(state, "variant.rabbit.white", "species.rabbit", RarityTier.Rare, (uint)AnimalTrait.Shiny);
            state.Collection.TryGet(b, out var white);
            hooks.OnObtained(in white, false, true);
            Assert.IsTrue(journal.IsDone(JournalMoment.FirstVariant), "Not the species' variant 1.");
            Assert.IsTrue(journal.IsDone(JournalMoment.FirstRare));
            Assert.IsTrue(journal.IsDone(JournalMoment.FirstShiny));
            Assert.IsFalse(journal.IsDone(JournalMoment.ThreeAnimals));
            JournalTestContent.Own(state, "variant.hedgehog.brown", "species.hedgehog");
            state.Collection.TryGet(3, out var third);
            hooks.OnObtained(in third, true, true);
            Assert.IsTrue(journal.IsDone(JournalMoment.ThreeAnimals));

            hooks.OnAnimalNamed(a);
            state.Collection.CustomNames[b] = "Snow";
            hooks.OnFavorite(b);
            journal.Variables(journal.PageAt(0).Id, names, values);
            Assert.AreEqual("memory.first_favorite", journal.PageAt(0).Key);
            Assert.AreEqual("Snow", values[0]);
        }

        [Test]
        public void Hooks_OlderAccount_PastMarkedDone_WithoutPages()
        {
            var journal = NewJournal(out var state, out var clock);
            JournalTestContent.Own(state, "variant.rabbit.brown", "species.rabbit");
            JournalTestContent.Own(state, "variant.rabbit.white", "species.rabbit", RarityTier.Rare);
            using var hooks = new JournalHooks(journal, state, clock, JournalTestContent.Build());
            Assert.AreEqual(0, journal.PageCount);
            Assert.IsTrue(journal.IsDone(JournalMoment.FirstHatch));
            Assert.IsTrue(journal.IsDone(JournalMoment.FirstRare));
            Assert.IsTrue(journal.HasSubject(JournalMoment.NewSpecies, ContentId.FromSource("species.rabbit").Value));
            Assert.IsTrue(state.Journal.Initialized);
        }

        // ------------------------------------------------------------------------------------------- save

        [Test]
        public void Save_RoundTrip_KeepsPagesFlagsAndVisitors()
        {
            var journal = NewJournal(out var state, out _);
            journal.Add(JournalMoment.FirstHatch, 0, 1, "Pip");
            journal.Add(JournalMoment.NewSpecies, 77, 1, "#species.rabbit.name");
            journal.Add(JournalMoment.BiomeUnlocked, 9, 9, null, null);
            journal.CaptureStored(1);
            journal.AddSeenSurprise("surprise.visitor_arrival");
            state.Journal.GardenPlants = 5;
            state.Journal.BestRenewalEssence = 40;
            state.Visitors.Contacts[1] = 3;
            state.Visitors.JoinedMask = 1 << 10;
            state.Visitors.GreetedDay = 9780;
            state.Visitors.GreetedMask = 2;
            state.Visitors.OpenedDay = 9779;
            state.Visitors.FirstVisitUnixMs = 1_790_000_000_000L;
            state.Visitors.FirstVisitor = 2;
            state.Visitors.Greetings = 4;

            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(state, writer);
            var loaded = serializer.Deserialize(serializer.ReadDocument(writer.Buffer, writer.Length));
            var j = loaded.Journal;
            Assert.IsTrue(j.Initialized == state.Journal.Initialized);
            Assert.AreEqual(3, j.PageCount);
            Assert.AreEqual(4, j.NextId);
            Assert.AreEqual("Pip", j.Pages[0].Value0);
            Assert.IsNull(j.Pages[2].Value0);
            Assert.IsTrue(j.Pages[0].Captured);
            Assert.IsTrue(j.Pages[1].Pending);
            Assert.AreEqual(77u, j.Pages[1].Subject);
            Assert.AreEqual(state.Journal.FirstDone, j.FirstDone);
            Assert.IsTrue(j.HasSubject(JournalMoment.NewSpecies, 77));
            Assert.AreEqual(3, j.Unseen);
            Assert.AreEqual(5u, j.GardenPlants);
            Assert.AreEqual(40, j.BestRenewalEssence);
            Assert.AreEqual(1, j.SurpriseCount);
            var reloaded = new JournalService(j);
            Assert.AreEqual(1, reloaded.StoredCaptureCount);
            Assert.AreEqual(1, reloaded.PendingCaptureCount, "The waiting portrait is still waiting after a load.");
            var v = loaded.Visitors;
            Assert.AreEqual(3, v.Contacts[1]);
            Assert.IsTrue(v.Joined(10));
            Assert.AreEqual(1, v.KeptCount);
            Assert.AreEqual(9780, v.GreetedDay);
            Assert.AreEqual(2, v.GreetedMask);
            Assert.AreEqual(9779, v.OpenedDay);
            Assert.AreEqual(1_790_000_000_000L, v.FirstVisitUnixMs);
            Assert.AreEqual(2, v.FirstVisitor);
            Assert.AreEqual(4, v.Greetings);
        }

        [Test]
        public void GoldenSaves_Schema15_LoadsWithAnEmptyJournal_Schema16_KeepsIt()
        {
            string folder = Path.Combine(Application.dataPath, "_Game/Tests/Fixtures/GoldenSaves");
            var old = Load(Path.Combine(folder, "GoldenSave_Achievements_v15.pwsave"));
            Assert.IsFalse(old.Journal.Initialized);
            Assert.AreEqual(0, old.Journal.PageCount + old.Visitors.Greetings + old.Visitors.OpenedDay);

            string path = Path.Combine(folder, "GoldenSave_Journal_v16.pwsave");
            Assert.IsTrue(File.Exists(path), "Missing golden save " + path + " (menu Pocket Wilds/Saves/Create Missing Golden Saves).");
            var state = Load(path);
            Assert.AreEqual(420, state.Meta.Revision);
            var j = state.Journal;
            Assert.IsTrue(j.Initialized);
            Assert.AreEqual(3, j.PageCount);
            Assert.AreEqual(4, j.NextId);
            Assert.IsTrue(j.Pages[0].Captured);
            Assert.IsTrue(j.Pages[1].Pending);
            Assert.AreEqual("#species.rabbit.name", j.Pages[1].Value0);
            Assert.AreEqual(77u, j.Pages[2].Subject);
            Assert.IsTrue(j.IsFirstDone(JournalMoment.FirstHatch));
            Assert.IsTrue(j.HasSubject(JournalMoment.Season, 3));
            Assert.AreEqual(2, j.Unseen);
            Assert.AreEqual(3u, j.GardenPlants);
            Assert.AreEqual("surprise.visitor_arrival", j.Surprises[0]);
            var v = state.Visitors;
            Assert.AreEqual(2, v.Contacts[1]);
            Assert.IsTrue(v.Joined(10));
            Assert.AreEqual(9800, v.OpenedDay);
            Assert.AreEqual(1, v.FirstVisitor);
            Assert.AreEqual(3, v.Greetings);
            Assert.AreEqual(250, state.Achievements.Progress[82], "Everything of schema 15 still there.");
        }

        private static PlayerState Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var serializer = new BinarySaveSerializer();
            var document = serializer.ReadDocument(bytes, bytes.Length);
            SaveMigrations.CreatePipeline().Migrate(document);
            return serializer.Deserialize(document);
        }
    }
}
