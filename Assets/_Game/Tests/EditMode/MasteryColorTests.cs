using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.UI;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T46 mastery colors: one animal of the species' mastery color at mastery 10, 20 and 30, once.</summary>
    public class MasteryColorTests
    {
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId Plain = ContentId.FromSource("variant.rabbit.plain");
        private static readonly ContentId Caramel = ContentId.FromSource("variant.rabbit.caramel");
        private static readonly ContentId Moon = ContentId.FromSource("variant.rabbit.moon");
        private static readonly ContentId Peach = ContentId.FromSource("variant.rabbit.peach");

        private static ContentRegistry Content()
        {
            var builder = new ContentRegistryBuilder();
            TestUtil.AddSpecies(builder, "species.rabbit", 1.0);
            Variant(builder, "variant.rabbit.plain", RarityTier.Common, false, 0);
            Variant(builder, "variant.rabbit.caramel", RarityTier.Rare, true, 10);
            Variant(builder, "variant.rabbit.moon", RarityTier.Epic, true, 20);
            Variant(builder, "variant.rabbit.peach", RarityTier.Legendary, true, 30);
            return builder.Build();
        }

        private static void Variant(ContentRegistryBuilder builder, string id, RarityTier rarity, bool secret, int mastery)
        {
            builder.AddVariant(new VariantDefinition(ContentId.FromSource(id), Rabbit, id + ".name", rarity,
                new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0, isSecret: secret, masteryLevel: mastery), id);
        }

        private sealed class Rig
        {
            public PlayerState State;
            public MasteryService Mastery;
            public MasteryColorService Colors;
            public ContentRegistry Content;
            public readonly List<MasteryColorGrant> Grants = new List<MasteryColorGrant>();
        }

        private static Rig Create(bool catchUp = false, bool subscribe = true)
        {
            var rig = new Rig { State = PlayerState.CreateNew(TestUtil.T0), Content = Content() };
            var economy = new EconomyService(rig.State, rig.Content, EconomyConfig.Default, null);
            rig.Mastery = new MasteryService(rig.State, EconomyConfig.Default, economy);
            rig.Colors = new MasteryColorService(rig.State, rig.Content, new FakeGameClock(TestUtil.T0)) { Economy = economy };
            if (subscribe)
                rig.Colors.Mastery = rig.Mastery;
            rig.Colors.Granted += (in MasteryColorGrant grant) => rig.Grants.Add(grant);
            if (catchUp)
                rig.Colors.CatchUp();
            return rig;
        }

        private static void ClimbTo(MasteryService mastery, int level)
        {
            int guard = 0;
            while (mastery.Get(Rabbit).Level < level && guard++ < 100000)
                mastery.AddXp(Rabbit, MasteryReason.Birth, 5);
            Assert.AreEqual(level, mastery.Get(Rabbit).Level);
        }

        [Test]
        public void Level9To10_GrantsTheFirstColorOnce()
        {
            var rig = Create();
            ClimbTo(rig.Mastery, 9);
            Assert.AreEqual(0, rig.Grants.Count);
            while (rig.Mastery.Get(Rabbit).Level < 10)
                rig.Mastery.AddXp(Rabbit, MasteryReason.Birth, 5);
            Assert.AreEqual(1, rig.Grants.Count);
            Assert.AreEqual(Caramel, rig.Grants[0].Variant);
            Assert.AreEqual(10, rig.Grants[0].Level);
            Assert.IsTrue(rig.State.Collection.DiscoveredVariants.Contains(Caramel));
            Assert.IsTrue(rig.State.Collection.TryGet(rig.Grants[0].InstanceId, out var animal));
            Assert.AreEqual(Caramel, animal.Variant);
            Assert.AreEqual(RarityTier.Rare, animal.Rarity);
            // More XP at level 10 and above (before 20) gives nothing more.
            rig.Mastery.AddXp(Rabbit, MasteryReason.Birth, 20);
            Assert.AreEqual(1, rig.Grants.Count);
        }

        [Test]
        public void Level9To31_GrantsThreeColors_NoneTwice()
        {
            var rig = Create();
            ClimbTo(rig.Mastery, 9);
            rig.Mastery.AddXp(Rabbit, MasteryReason.Birth, 10_000_000);
            Assert.GreaterOrEqual(rig.Mastery.Get(Rabbit).Level, 30);
            Assert.AreEqual(3, rig.Grants.Count);
            CollectionAssert.AreEquivalent(new[] { Caramel, Moon, Peach }, new[] { rig.Grants[0].Variant, rig.Grants[1].Variant, rig.Grants[2].Variant });
            rig.Mastery.AddXp(Rabbit, MasteryReason.Birth, 10_000_000);
            Assert.AreEqual(3, rig.Grants.Count);
        }

        [Test]
        public void AlreadyDiscovered_GrantsNothing()
        {
            var rig = Create();
            rig.State.Collection.DiscoveredVariants.Add(Caramel);
            ClimbTo(rig.Mastery, 10);
            Assert.AreEqual(0, rig.Grants.Count);
            Assert.AreEqual(0, rig.State.Collection.Count);
        }

        [Test]
        public void CatchUp_GrantsWhatTheSaveEarned_WithoutEvent()
        {
            // A save that earned mastery 20 before T46: the boot attaches the mastery service, then catches up.
            var rig = Create(subscribe: false);
            ClimbTo(rig.Mastery, 20);
            rig.Colors.Mastery = rig.Mastery;
            rig.Colors.CatchUp();
            Assert.AreEqual(0, rig.Grants.Count);
            Assert.IsTrue(rig.State.Collection.DiscoveredVariants.Contains(Caramel));
            Assert.IsTrue(rig.State.Collection.DiscoveredVariants.Contains(Moon));
            Assert.IsFalse(rig.State.Collection.DiscoveredVariants.Contains(Peach));
            int count = rig.State.Collection.Count;
            rig.Colors.CatchUp();
            Assert.AreEqual(count, rig.State.Collection.Count, "Once only.");
        }

        [Test]
        public void MasteryColors_AreSecret_SoNeverInEggOdds()
        {
            var content = Content();
            int found = 0;
            for (int i = 0; i < content.Variants.Count; i++)
            {
                var variant = content.Variants[i];
                if (variant.MasteryLevel <= 0)
                    continue;
                found++;
                Assert.IsTrue(variant.IsSecret, variant.Id.ToString());
            }
            Assert.AreEqual(3, found);
        }

        [Test]
        public void ShippedContent_EachMasteryColorIsSecret_AtMostOnePerSpeciesAndLevel()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(UiPipeline.ContentDatabasePath);
            var content = database.CreateBuilder().Build();
            var seen = new HashSet<(ContentId, int)>();
            for (int i = 0; i < content.Variants.Count; i++)
            {
                var variant = content.Variants[i];
                if (variant.MasteryLevel <= 0)
                    continue;
                Assert.IsTrue(variant.IsSecret, variant.Id.ToString());
                Assert.IsTrue(variant.MasteryLevel == 10 || variant.MasteryLevel == 20 || variant.MasteryLevel == 30, variant.Id.ToString());
                Assert.IsTrue(seen.Add((variant.Species, variant.MasteryLevel)), "Two colors for one species and level: " + variant.Id);
            }
        }

        [Test]
        public void AchievementCounter_CountsTheMasteryColors_ButNotTheSecretOnes()
        {
            var rig = Create();
            var items = new ProfileItemService(rig.State.Achievements);
            var achievements = new AchievementService(rig.State.Achievements) { Items = items };
            int index = AchievementData.IndexOf(AchievementCounter.MasteryVariants);
            int secretIndex = AchievementData.IndexOf(AchievementCounter.SecretVariants);
            using (var hooks = new AchievementHooks(achievements, items, rig.State, new FakeGameClock(TestUtil.T0), rig.Content))
            {
                rig.Colors.AchievementHooks = hooks;
                Assert.AreEqual(0, achievements.Progress(index));
                ClimbTo(rig.Mastery, 10);
                Assert.AreEqual(1, achievements.Progress(index));
                rig.Mastery.AddXp(Rabbit, MasteryReason.Birth, 10_000_000);
                Assert.AreEqual(3, achievements.Progress(index));
                Assert.AreEqual(0, achievements.Progress(secretIndex), "Mastery colors have their own counter.");
            }
        }
    }
}
