using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Editor.UI;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEditor;
using UnityEngine;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T20: species mastery (doc 02 XP table and curve), duplicates always progress, no useless hatch.</summary>
    public class MasteryTests
    {
        private static readonly ContentId Rabbit = ContentId.FromSource("species.rabbit");
        private static readonly ContentId Basic = ContentId.FromSource("egg.grassland.basic");

        private static (PlayerState state, EconomyService economy, MasteryService mastery) Create()
        {
            var state = PlayerState.CreateNew(TestUtil.T0);
            var economy = new EconomyService(state, TestUtil.ThreeSpecies(), EconomyConfig.Default, null);
            return (state, economy, new MasteryService(state, EconomyConfig.Default, economy));
        }

        [Test]
        public void XpCurve_FollowsTheDoc()
        {
            var (_, _, mastery) = Create();
            // T35C calibration: 2.7 x L^1.15 XP for level L, 10 XP a duplicate.
            Assert.AreEqual(3, mastery.XpForNext(0), "Undiscovered to level 1.");
            Assert.AreEqual(6, mastery.XpForNext(1));
            Assert.AreEqual(10, mastery.XpForNext(2));
            Assert.AreEqual(17, mastery.XpForNext(4));
            Assert.AreEqual(25, mastery.XpFor(MasteryReason.Discovery));
            Assert.AreEqual(15, mastery.XpFor(MasteryReason.NewVariant));
            Assert.AreEqual(10, mastery.XpFor(MasteryReason.Duplicate));
            Assert.AreEqual(4, mastery.XpFor(MasteryReason.Birth));
        }

        [Test]
        public void Discovery_Duplicates_AndVariants_AddUp()
        {
            var (state, _, mastery) = Create();
            var first = mastery.OnAnimalObtained(Rabbit, true, true);
            Assert.AreEqual(MasteryReason.Discovery, first.Reason);
            Assert.AreEqual(25, first.Xp);
            Assert.AreEqual(0, first.Before.Level);
            // 3 + 6 + 10 = 19 XP for level 3, 6 XP into level 4.
            Assert.AreEqual(3, first.After.Level, "The first rabbit already levels the species up.");
            Assert.AreEqual(6, first.After.Xp);
            Assert.IsTrue(first.LeveledUp);

            var duplicate = mastery.OnAnimalObtained(Rabbit, false, false);
            Assert.AreEqual(MasteryReason.Duplicate, duplicate.Reason);
            Assert.AreEqual(10, duplicate.Xp);
            Assert.AreEqual(4, duplicate.After.Level, "16 XP: level 4 needs 13.");
            Assert.AreEqual(3, duplicate.After.Xp);
            Assert.IsTrue(duplicate.LeveledUp);

            var variant = mastery.OnAnimalObtained(Rabbit, false, true);
            Assert.AreEqual(15, variant.Xp);
            Assert.AreEqual(1, mastery.Get(Rabbit).Xp, "18 XP: level 5 needs 17.");
            Assert.AreEqual(5, state.Mastery.GetLevel(Rabbit));
        }

        [Test]
        public void BigGain_CrossesSeveralLevels_AndReportsTheMilestone()
        {
            var (state, _, mastery) = Create();
            // 3 + 6 + 10 + 13 + 17 = 49 XP to reach level 5.
            var gain = mastery.AddXp(Rabbit, MasteryReason.Birth, 60);
            Assert.AreEqual(5, gain.After.Level);
            Assert.AreEqual(11, gain.After.Xp);
            Assert.AreEqual(5, gain.LevelsGained);
            Assert.AreEqual(5, gain.Milestone);
            Assert.AreEqual(mastery.XpForNext(5), gain.After.XpForNext);

            var small = mastery.AddXp(Rabbit, MasteryReason.Duplicate, 2);
            Assert.AreEqual(0, small.Milestone, "A milestone is reported once, when crossed.");
            Assert.IsFalse(mastery.AddXp(Rabbit, MasteryReason.Duplicate, 0).HasGain);
            Assert.IsTrue(MasteryService.IsMilestone(10, 5));
            Assert.IsFalse(MasteryService.IsMilestone(0, 5));
        }

        [Test]
        public void LevelUps_RaiseProduction_ByTheDocMultiplier()
        {
            var (state, economy, mastery) = Create();
            uint id = state.Collection.Add(new AnimalInstance { Species = Rabbit });
            state.Collection.TryGet(id, out var animal);
            economy.OnAnimalAdded(animal);
            double before = economy.GetLightPerSecond().ToDoubleClamped();
            mastery.AddXp(Rabbit, MasteryReason.Birth, 60); // level 5
            double after = economy.GetLightPerSecond().ToDoubleClamped();
            Assert.AreEqual(before * (1.0 + 0.015 * 5), after, 1e-9);
        }

        [Test]
        public void EveryHatch_GivesProgress_NoHatchIsUseless()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>(UiPipeline.ContentDatabasePath);
            var content = database.CreateBuilder().Build();
            var state = PlayerState.CreateNew(TestUtil.T0);
            var economy = new EconomyService(state, content, EconomyConfig.Default, null);
            var mastery = new MasteryService(state, EconomyConfig.Default, economy);
            var hatch = new HatchService(state, content, economy, new FakeGameClock(TestUtil.T0), null, mastery);
            economy.Grant(CurrencyId.Light, BigValue.FromMantissaExponent(1.0, 300), EconomyReason.Reward);

            double rate = 0.0;
            int totalXp = 0;
            for (int i = 0; i < 2000; i++)
            {
                var result = hatch.Hatch(Basic);
                Assert.IsTrue(result.Succeeded);
                Assert.GreaterOrEqual(result.Mastery.Xp, 2, "Duplicates always give species XP.");
                if (i == 0)
                    Assert.AreEqual(MasteryReason.Discovery, result.Mastery.Reason);
                totalXp += result.Mastery.Xp;
                double now = economy.GetLightPerSecond().ToDoubleClamped();
                Assert.Greater(now, rate, "Each animal also adds production.");
                rate = now;
            }
            // Same total whatever the split between levels and species: XP is never lost on a level up.
            int spent = 0;
            foreach (var pair in state.Mastery.Species)
            {
                var progress = mastery.Get(pair.Key);
                for (int level = 0; level < progress.Level; level++)
                    spent += mastery.XpForNext(level);
                spent += progress.Xp;
                Assert.Greater(progress.Level, 5, pair.Key.ToString());
            }
            Assert.AreEqual(totalXp, spent);
        }

        [Test]
        public void Mastery_SurvivesTheSaveFile()
        {
            var (state, _, mastery) = Create();
            mastery.AddXp(Rabbit, MasteryReason.Birth, 300);
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(state, writer);
            var bytes = writer.WrittenSpan.ToArray();
            var loaded = serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
            var reloaded = new MasteryService(loaded, EconomyConfig.Default, null);
            Assert.AreEqual(mastery.Get(Rabbit).Level, reloaded.Get(Rabbit).Level);
            Assert.AreEqual(mastery.Get(Rabbit).Xp, reloaded.Get(Rabbit).Xp);
        }

        [Test]
        public void HudPrefab_HasAPassiveMasteryToast()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPipeline.HudPrefabPath);
            var toast = prefab.transform.Find("SafeArea/Mastery");
            Assert.IsNotNull(toast);
            Assert.IsNotNull(toast.GetComponent<Canvas>(), "Animated on its own canvas.");
            Assert.IsFalse(toast.GetComponent<CanvasGroup>().blocksRaycasts, "Never blocks a touch.");
            foreach (var graphic in toast.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.IsFalse(graphic.raycastTarget, graphic.name);
        }
    }
}
