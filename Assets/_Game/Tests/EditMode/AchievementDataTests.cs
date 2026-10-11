using System;
using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Editor.UI;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T44B: the 200 achievements, 322 titles, 42 frames and 47 poses are validated data (doc 01 Succès, sections 1 to 5).</summary>
    public class AchievementDataTests
    {
        private static readonly int[] PerCategory = { 20, 52, 10, 12, 26, 10, 12, 10, 12, 12, 10, 8, 6 };

        [Test]
        public void Achievements_200_InTheir13Categories_WithRisingThresholds()
        {
            Assert.AreEqual(200, AchievementData.Count);
            var counts = new int[13];
            var keys = new HashSet<string>();
            for (int i = 0; i < AchievementData.Count; i++)
            {
                ref readonly var row = ref AchievementData.Row(i);
                Assert.AreEqual($"achievement.{i + 1:000}", row.Key);
                Assert.IsTrue(keys.Add(row.Key), row.Key + " twice");
                Assert.AreEqual(i, AchievementData.IndexOf(row.Key));
                counts[(int)row.Category]++;
                Assert.IsTrue(0 < row.Bronze && row.Bronze < row.Silver && row.Silver < row.Gold && row.Gold < row.Star,
                    row.Key + " thresholds must rise");
                Assert.AreEqual(i, AchievementData.IndexOf(row.Counter, row.Param), row.Key + ": its counter belongs to it alone");
            }
            CollectionAssert.AreEqual(PerCategory, counts);
            // Categories follow the doc's ranges (ACH-021 to 072 biomes, ACH-195 to 200 refuge).
            Assert.AreEqual(AchievementCategory.Biomes, AchievementData.Row(20).Category);
            Assert.AreEqual(AchievementCategory.Biomes, AchievementData.Row(71).Category);
            Assert.AreEqual(AchievementCategory.Refuge, AchievementData.Row(194).Category);
            Assert.AreEqual(2000, AchievementData.Row(81).Star, "« 2 000 » reads as 2000.");
            Assert.AreEqual(200000, AchievementData.Row(103).Star);
        }

        [Test]
        public void Rewards_Gems17000_GoldDecorationsOnBiomes_StarExtrasListed()
        {
            int gems = 0, decorations = 0, extras = 0;
            for (int i = 0; i < AchievementData.Count; i++)
            {
                for (var t = AchievementTier.Bronze; t <= AchievementTier.Star; t++)
                    gems += AchievementData.GemsFor(t);
                ref readonly var row = ref AchievementData.Row(i);
                bool biome = row.Category == AchievementCategory.Biomes;
                Assert.AreEqual(biome, AchievementData.GoldDecoration(i).IsValid, row.Key);
                if (biome)
                {
                    decorations++;
                    StringAssert.StartsWith("decor.achievement.", row.GoldDecoration);
                }
                if (row.StarExtra.Length == 0)
                    continue;
                extras++;
                Assert.IsTrue(ProfileItemService.TryKindOf(row.StarExtra, out var kind), row.StarExtra);
                int index = ProfileData.IndexOf(kind, row.StarExtra);
                Assert.GreaterOrEqual(index, 0, row.StarExtra + " is in no table");
                ref readonly var item = ref ProfileData.Row(kind, index);
                Assert.AreEqual(ProfileSource.Achievement, item.Source);
                Assert.AreEqual(i + 1, item.Subject);
            }
            Assert.AreEqual(17000, gems, "85 Gems per achievement.");
            Assert.AreEqual(52, decorations);
            Assert.AreEqual(43 + 12 + 6, extras);
        }

        [Test]
        public void ProfileItems_322Titles_42Frames_47Poses_BySource()
        {
            Assert.AreEqual(322, ProfileData.Count(ProfileItemKind.Title));
            Assert.AreEqual(43, ProfileData.Count(ProfileItemKind.Frame), "42 from the doc and the T49 gold « Racines ».");
            Assert.AreEqual(47, ProfileData.Count(ProfileItemKind.Pose));
            AssertSources(ProfileItemKind.Title, "title.", new Dictionary<ProfileSource, int>
            {
                { ProfileSource.Start, 1 }, { ProfileSource.Achievement, 43 }, { ProfileSource.EventPage, 12 },
                { ProfileSource.ActivityPage, 18 }, { ProfileSource.Companion, 13 }, { ProfileSource.RefugeLevel, 7 },
                { ProfileSource.BiomeMastery, 39 }, { ProfileSource.SpeciesMastery, 180 }, { ProfileSource.PassSeason, 8 },
                { ProfileSource.JournalMoments, 1 },
            });
            AssertSources(ProfileItemKind.Frame, "frame.", new Dictionary<ProfileSource, int>
            {
                { ProfileSource.Start, 1 }, { ProfileSource.BiomeMastery, 13 }, { ProfileSource.EventTrack, 12 },
                { ProfileSource.RefugeLevel, 3 }, { ProfileSource.Achievement, 12 }, { ProfileSource.JournalMoments, 1 },
                { ProfileSource.EventGold, 1 },
            });
            AssertSources(ProfileItemKind.Pose, "pose.", new Dictionary<ProfileSource, int>
            {
                { ProfileSource.PhotoMode, 8 }, { ProfileSource.EventGoal, 12 }, { ProfileSource.Companion, 13 },
                { ProfileSource.PassSeason, 8 }, { ProfileSource.Achievement, 6 },
            });
            Assert.AreEqual(0, ProfileData.IndexOf(ProfileItemKind.Title, ProfileData.StartingTitle));
            Assert.AreEqual(0, ProfileData.IndexOf(ProfileItemKind.Frame, ProfileData.StartingFrame));
            Assert.AreEqual(90, ProfileData.SpeciesCount);
            Assert.AreEqual(0, ProfileData.SpeciesIndex(Core.ContentId.FromSource("species.rabbit")));
            Assert.IsTrue(ProfileData.IsEventSpecies(ProfileData.Species(ProfileData.FirstEventSpecies)));
        }

        private static void AssertSources(ProfileItemKind kind, string prefix, Dictionary<ProfileSource, int> expected)
        {
            var counts = new Dictionary<ProfileSource, int>();
            var keys = new HashSet<string>();
            for (int i = 0; i < ProfileData.Count(kind); i++)
            {
                ref readonly var row = ref ProfileData.Row(kind, i);
                StringAssert.StartsWith(prefix, row.Key);
                Assert.IsTrue(keys.Add(row.Key), row.Key + " twice");
                Assert.AreEqual(i + 1, row.Order, row.Key + " gallery order");
                Assert.AreEqual(i, ProfileData.IndexOf(kind, row.Key));
                counts.TryGetValue(row.Source, out int n);
                counts[row.Source] = n + 1;
            }
            CollectionAssert.AreEquivalent(expected, counts, kind.ToString());
        }

        [Test]
        public void Texts_EveryNameGoalAndSource_InEnglishAndFrench()
        {
            var texts = ProfileTexts.Texts;
            var rows = new Dictionary<string, (string en, string fr)>();
            for (int i = 0; i < texts.GetLength(0); i++)
            {
                Assert.IsTrue(rows.TryAdd(texts[i, 0], (texts[i, 1], texts[i, 2])), texts[i, 0] + " twice");
                Assert.IsFalse(string.IsNullOrEmpty(texts[i, 1]) || string.IsNullOrEmpty(texts[i, 2]), texts[i, 0]);
                Assert.IsFalse(texts[i, 2].Contains(((char)0x2014).ToString()), texts[i, 0] + ": no em dash in French");
                foreach (int code in new[] { 0x2728, 0x2726, 0x2727 })
                    Assert.IsFalse(texts[i, 1].Contains(char.ConvertFromUtf32(code)) || texts[i, 2].Contains(char.ConvertFromUtf32(code)), texts[i, 0]);
            }
            // T49: the gold Roots frame is written by hand with its event's texts (BurrowsTexts), not generated from the doc.
            for (int i = 0; i < BurrowsTexts.Texts.GetLength(0); i++)
                rows.TryAdd(BurrowsTexts.Texts[i, 0], (BurrowsTexts.Texts[i, 1], BurrowsTexts.Texts[i, 2]));
            for (int i = 0; i < AchievementData.Count; i++)
            {
                string key = AchievementData.Row(i).Key;
                Assert.IsTrue(rows.ContainsKey(key + ".name"), key);
                StringAssert.Contains("{n}", rows[key + ".goal"].en);
                StringAssert.Contains("{n}", rows[key + ".goal"].fr);
            }
            foreach (var c in (AchievementCategory[])Enum.GetValues(typeof(AchievementCategory)))
                Assert.IsTrue(rows.ContainsKey("achievement.category." + c.ToString().ToLowerInvariant() + ".name"), c.ToString());
            for (int k = 0; k < 3; k++)
            {
                var kind = (ProfileItemKind)k;
                for (int i = 0; i < ProfileData.Count(kind); i++)
                {
                    string key = ProfileData.Row(kind, i).Key;
                    Assert.IsTrue(rows.ContainsKey(key + ".name") && rows.ContainsKey(key + ".source"), key);
                }
            }
            Assert.AreEqual("Little Paw", rows["title.little_paw.name"].en);
            Assert.AreEqual("Petite patte", rows["title.little_paw.name"].fr);
        }
    }
}
