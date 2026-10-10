using System.Linq;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T34A Gate 10 (VFX review), runtime side: counts per tier against doc 07, short durations, pooled reset without
    /// instantiation or allocation, worst-case concurrency, and a tier change that rewrites counts in place.
    /// </summary>
    public class FeedbackVfxPlayTests
    {
        private GameObject _root;
        private FeedbackVfx _vfx;

        private static VfxCatalog Catalog
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.AssetDatabase.LoadAssetAtPath<VfxCatalog>("Assets/_Game/Content/Visuals/DEF_VfxCatalog.asset");
#else
                return null;
#endif
            }
        }

        [TearDown]
        public void TearDown()
        {
            _vfx?.Dispose();
            if (_root != null)
                Object.Destroy(_root);
        }

        private FeedbackVfx Make(QualityTier tier)
        {
            _root = new GameObject("VfxTestRoot");
            _vfx = new FeedbackVfx(Catalog, _root.transform, tier);
            return _vfx;
        }

        /// <summary>Doc 07 counts at Medium (H-VFX-001 to 012); a common burst plus its rarity accent for the hatch tiers.</summary>
        [TestCase(VfxId.EggTap, 6, 10)]
        [TestCase(VfxId.Crack, 8, 14)]
        [TestCase(VfxId.HatchCommon, 12, 20)]
        [TestCase(VfxId.HatchRare, 20, 28)]
        [TestCase(VfxId.HatchEpic, 24, 36)]
        [TestCase(VfxId.HatchLegendary, 30, 45)]
        [TestCase(VfxId.HatchFabulous, 35, 55)]
        [TestCase(VfxId.FavoriteHeart, 1, 3)]
        [TestCase(VfxId.NewVariant, 4, 12)]
        [TestCase(VfxId.NewSpecies, 10, 24)]
        [TestCase(VfxId.MasteryLevelUp, 8, 18)]
        [TestCase(VfxId.RareGlint, 1, 4)]
        public void MediumCounts_FollowTheDoc_LowHalves_HighStaysModest(VfxId id, int min, int max)
        {
            var vfx = Make(QualityTier.Medium);
            int medium = vfx.ParticleCount(id);
            Assert.That(medium, Is.InRange(min, max), id + " at Medium.");
            vfx.SetTier(QualityTier.Low);
            int low = vfx.ParticleCount(id);
            Assert.LessOrEqual(low, Mathf.CeilToInt(medium * 0.6f) + 1, id + ": Low keeps about half (count of each burst rounds up, never 0).");
            Assert.GreaterOrEqual(low, 1, id + ": the effect still speaks on Low.");
            vfx.SetTier(QualityTier.High);
            Assert.LessOrEqual(vfx.ParticleCount(id), Mathf.CeilToInt(medium * 1.35f) + 1, id + ": High never doubles.");
        }

        [Test]
        public void Durations_StayShort()
        {
            var vfx = Make(QualityTier.Medium);
            Assert.LessOrEqual(vfx.DurationOf(VfxId.EggTap), 0.45f, "Egg tap under 0.4 s (+0.05 s pool margin).");
            Assert.LessOrEqual(vfx.DurationOf(VfxId.FavoriteHeart), 0.75f, "Hearts under 0.7 s.");
            Assert.LessOrEqual(vfx.DurationOf(VfxId.HatchCommon), 1.25f, "Common hatch under 1.2 s.");
            for (int i = 0; i < (int)VfxId.Count; i++)
                Assert.LessOrEqual(vfx.DurationOf((VfxId)i), 1.8f, (VfxId)i + ": still short.");
        }

        [Test]
        public void TierChange_RewritesPooledInstances_WithoutNewOnes()
        {
            var vfx = Make(QualityTier.Medium);
            var pool = vfx.GetPool(VfxId.HatchFabulous);
            int created = pool.InstantiateCount;
            Assert.IsTrue(pool.TryAcquire(out var instance));
            int Count() => instance.GetComponentsInChildren<ParticleSystem>(true).Sum(ps =>
                Enumerable.Range(0, ps.emission.burstCount).Sum(b => (int)ps.emission.GetBurst(b).count.constant));
            int medium = Count();
            vfx.SetTier(QualityTier.Low);
            Assert.Less(Count(), medium, "The pooled instance itself carries the Low counts.");
            vfx.SetTier(QualityTier.Medium);
            Assert.AreEqual(medium, Count(), "And comes back to Medium exactly.");
            pool.Release(instance);
            Assert.AreEqual(created, pool.InstantiateCount);
        }

        [Test]
        public void EveryEffect_PlaysTwice_AfterWarmup_WithoutInstantiate_NorGarbage_AndResets()
        {
            var vfx = Make(QualityTier.Medium);
            var counts = new int[(int)VfxId.Count];
            for (int i = 0; i < counts.Length; i++)
                counts[i] = vfx.GetPool((VfxId)i).InstantiateCount;
            float time = 0f;
            var tint = Color.white;
            void Round()
            {
                for (int i = 0; i < counts.Length; i++)
                    vfx.Play((VfxId)i, new Vector3(i, 0f, 0f), time, tint);
                time += 3f;
                vfx.Tick(time);
            }
            // Warm the exact code path once (first plays, first tinted plays, first returns).
            Round();
            Assert.That(() =>
            {
                Round();
                Round();
            }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(0, vfx.PlayingCount, "Every effect went back to its pool after its duration.");
            for (int i = 0; i < counts.Length; i++)
                Assert.AreEqual(counts[i], vfx.GetPool((VfxId)i).InstantiateCount, "No instantiation after warmup: " + (VfxId)i);
            foreach (var ps in _root.GetComponentsInChildren<ParticleSystem>(true))
                Assert.AreEqual(0, ps.particleCount, ps.name + ": released effects hold no particle.");
        }

        [Test]
        public void WorstCase_AllEffectsAtOnce_FitTheCap_AndTheSkipNeverHitches()
        {
            var vfx = Make(QualityTier.High);
            int played = 0;
            for (int i = 0; i < (int)VfxId.Count; i++)
                if (vfx.Play((VfxId)i, Vector3.zero, 0f))
                    played++;
            Assert.AreEqual((int)VfxId.Count, played, "A hatch with every accent, discovery, mastery, heart and glint at once all play.");
            // Spam beyond the cap: extra plays are skipped (return false), never instantiate.
            int before = vfx.GetPool(VfxId.HatchCommon).InstantiateCount;
            for (int i = 0; i < 40; i++)
                vfx.Play(VfxId.HatchCommon, Vector3.zero, 0f);
            Assert.LessOrEqual(vfx.PlayingCount, Catalog.maxPlaying);
            Assert.LessOrEqual(vfx.GetPool(VfxId.HatchCommon).InstantiateCount, Catalog.effects.First(e => e.id == VfxId.HatchCommon).hardCapacity);
            Assert.GreaterOrEqual(vfx.GetPool(VfxId.HatchCommon).InstantiateCount, before);
            int total = 0;
            for (int i = 0; i < (int)VfxId.Count; i++)
                total += vfx.ParticleCount((VfxId)i);
            // T42 adds the expedition arrival dust (H-VFX-076: 8 dots, 10 on High): 320 became 340.
            // T43 adds the four garden effects (drops 6, ring 1, harvest 11, hearts 2 on High: 20, 327 + 20): 340 became 350.
            Assert.LessOrEqual(total, 350, "Everything at once on High stays a few hundred small particles.");
        }
    }
}
