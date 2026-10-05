using System.Collections.Generic;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>T12: central scheduler, headless. Ticks A/B, no synchronized spike, no allocation.</summary>
    public class AnimalSchedulerTests
    {
        private const float Dt = 1f / 60f;

        private static SchedulerSettings Settings(int maxA = 30) => new SchedulerSettings
        {
            MaxTierA = maxA,
            TickRateA = 5f,
            TickRateB = 1f,
            TierUpdateInterval = 0.5f,
        };

        private static AnimalScheduler Populate(int count, int maxA = 30, float radius = 4f)
        {
            var scheduler = new AnimalScheduler(128, Settings(maxA), new WanderArea(Vector3.zero, radius));
            var profile = new BehaviorProfile();
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            for (int i = 0; i < count; i++)
            {
                var position = new Vector3((i % columns - columns * 0.5f) * 0.6f, 0f, (i / columns - columns * 0.5f) * 0.6f);
                int handle = scheduler.Register(null, (uint)(i + 1), (uint)(1000 + i * 7919), position, i * 37f, profile, 0f);
                Assert.AreNotEqual(AnimalScheduler.InvalidHandle, handle);
            }
            return scheduler;
        }

        private static void Run(AnimalScheduler scheduler, float seconds, ref float time, System.Action perFrame = null)
        {
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int f = 0; f < frames; f++)
            {
                time += Dt;
                scheduler.Tick(time, Dt);
                perFrame?.Invoke();
            }
        }

        [Test]
        public void ThirtyRabbits_TickAtTheirTierRate()
        {
            var scheduler = Populate(30);
            float time = 0f;
            Run(scheduler, 20f, ref time);
            Assert.AreEqual(30, scheduler.CountA);
            Assert.AreEqual(0, scheduler.CountB);
            // 5 Hz each: 30 x 5 x 20 s = 3000 evaluations.
            Assert.AreEqual(3000, scheduler.TotalTicksA, 30);
        }

        [Test]
        public void ThirtyRabbits_NeverDecideTogether()
        {
            var scheduler = Populate(30);
            float time = 0f;
            int maxChanges = 0, maxTicked = 0;
            var histogram = new int[31];
            Run(scheduler, 120f, ref time, () =>
            {
                maxChanges = Mathf.Max(maxChanges, scheduler.StateChangesLastTick);
                maxTicked = Mathf.Max(maxTicked, scheduler.AgentsTickedLastTick);
                histogram[scheduler.StateChangesLastTick]++;
            });

            Assert.Greater(scheduler.TotalStateChanges, 30 * 120 / 8, "The refuge must feel alive.");
            // 30 agents x 5 Hz / 60 fps = 2.5 evaluations per frame, never a burst.
            Assert.LessOrEqual(maxTicked, 3);
            Assert.LessOrEqual(maxChanges, 3, "Synchronized decision spike.");
            int framesWithChange = 0;
            for (int i = 1; i < histogram.Length; i++) framesWithChange += histogram[i];
            Assert.Less(histogram[3], framesWithChange / 20 + 1, "Triple changes must stay rare.");
        }

        [Test]
        public void SpawnedTogether_FirstDecisionsAreSpread()
        {
            var scheduler = Populate(30);
            float time = 0f;
            int firstSecondChanges = 0, maxChanges = 0;
            Run(scheduler, 1f, ref time, () =>
            {
                firstSecondChanges += scheduler.StateChangesLastTick;
                maxChanges = Mathf.Max(maxChanges, scheduler.StateChangesLastTick);
            });
            Assert.LessOrEqual(maxChanges, 3);
            Assert.Less(firstSecondChanges, 20, "A fresh group must not all react in the first second.");
        }

        [Test]
        public void Tiers_NearestAreA_RestAreB_WithSlowerTicks()
        {
            var scheduler = Populate(70, maxA: 30, radius: 6f);
            scheduler.SetFocus(new Vector3(-2f, 0f, -2f));
            float time = 0f;
            Run(scheduler, 10f, ref time);
            Assert.AreEqual(30, scheduler.CountA);
            Assert.AreEqual(40, scheduler.CountB);
            Assert.AreEqual(30 * 5 * 10, scheduler.TotalTicksA, 40);
            Assert.AreEqual(40 * 1 * 10, scheduler.TotalTicksB, 20);

            float farthestA = 0f, nearestB = float.MaxValue;
            for (int h = 0; h < 70; h++)
            {
                float d = Vector3.Distance(scheduler.GetPosition(h), new Vector3(-2f, 0f, -2f));
                if (scheduler.GetTier(h) == SimulationTier.A) farthestA = Mathf.Max(farthestA, d);
                else nearestB = Mathf.Min(nearestB, d);
            }
            // Tiers are refreshed every 0.5 s while animals walk at 0.38 m/s.
            Assert.LessOrEqual(farthestA, nearestB + 0.4f);
        }

        [Test]
        public void TierB_NeverPlaysLookOrInspect()
        {
            var scheduler = Populate(40, maxA: 0, radius: 5f);
            float time = 0f;
            bool sawForbidden = false;
            Run(scheduler, 120f, ref time, () =>
            {
                for (int h = 0; h < 40; h++)
                {
                    var s = scheduler.GetState(h);
                    sawForbidden |= s == AnimalBehaviorState.IdleLook || s == AnimalBehaviorState.Inspect;
                }
            });
            Assert.IsFalse(sawForbidden);
        }

        [Test]
        public void Wandering_StaysInsideTheArea_AndVisitsSeveralStates()
        {
            var scheduler = Populate(30, radius: 3f);
            float time = 0f;
            var seen = new HashSet<AnimalBehaviorState>();
            float maxRadius = 0f;
            var start = new Vector3[30];
            for (int h = 0; h < 30; h++) start[h] = scheduler.GetPosition(h);
            Run(scheduler, 90f, ref time, () =>
            {
                for (int h = 0; h < 30; h++)
                {
                    seen.Add(scheduler.GetState(h));
                    maxRadius = Mathf.Max(maxRadius, new Vector2(scheduler.GetPosition(h).x, scheduler.GetPosition(h).z).magnitude);
                }
            });
            Assert.LessOrEqual(maxRadius, 3.001f);
            Assert.IsTrue(seen.Contains(AnimalBehaviorState.Wander));
            Assert.IsTrue(seen.Contains(AnimalBehaviorState.Eat));
            Assert.IsTrue(seen.Contains(AnimalBehaviorState.Sleep));
            int moved = 0;
            for (int h = 0; h < 30; h++)
                if (Vector3.Distance(start[h], scheduler.GetPosition(h)) > 0.3f) moved++;
            Assert.Greater(moved, 20);
        }

        [Test]
        public void SameSeeds_SameStory()
        {
            var a = Populate(30);
            var b = Populate(30);
            float ta = 0f, tb = 0f;
            Run(a, 30f, ref ta);
            Run(b, 30f, ref tb);
            for (int h = 0; h < 30; h++)
            {
                Assert.AreEqual(a.GetState(h), b.GetState(h));
                Assert.AreEqual(a.GetPosition(h), b.GetPosition(h));
                Assert.AreEqual(a.GetTemperament(h), b.GetTemperament(h));
            }
        }

        [Test]
        public void Poke_ReactsAtOnce_AndWakesSleepers()
        {
            var scheduler = Populate(30);
            float time = 0f;
            Run(scheduler, 2f, ref time);
            scheduler.Poke(3, time);
            Assert.AreEqual(AnimalBehaviorState.React, scheduler.GetState(3));
            Run(scheduler, 1.5f, ref time);
            Assert.AreNotEqual(AnimalBehaviorState.React, scheduler.GetState(3), "React is a short one-shot.");

            int sleeper = -1;
            Run(scheduler, 200f, ref time, () =>
            {
                if (sleeper < 0)
                    for (int h = 0; h < 30; h++)
                        if (scheduler.GetState(h) == AnimalBehaviorState.Sleep) { sleeper = h; break; }
            });
            Assert.GreaterOrEqual(sleeper, 0, "Someone should nap within 200 s.");
            while (scheduler.GetState(sleeper) != AnimalBehaviorState.Sleep)
                Run(scheduler, 0.5f, ref time);
            scheduler.Poke(sleeper, time);
            Assert.AreEqual(AnimalBehaviorState.Wake, scheduler.GetState(sleeper));
        }

        [Test]
        public void Unregister_KeepsOthersRunning_AndFreesTheSlot()
        {
            var scheduler = Populate(30);
            float time = 0f;
            Run(scheduler, 2f, ref time);
            scheduler.Unregister(5);
            scheduler.Unregister(17);
            Assert.AreEqual(28, scheduler.Count);
            Assert.IsFalse(scheduler.IsValid(5));
            Run(scheduler, 5f, ref time);
            Assert.AreEqual(28, scheduler.CountA + scheduler.CountB);
            int handle = scheduler.Register(null, 99, 99, Vector3.zero, 0f, new BehaviorProfile(), time);
            Assert.IsTrue(handle == 5 || handle == 17);
        }

        [Test]
        public void Full_ReturnsInvalidHandle()
        {
            var scheduler = new AnimalScheduler(2, Settings(), new WanderArea(Vector3.zero, 2f));
            var profile = new BehaviorProfile();
            scheduler.Register(null, 1, 1, Vector3.zero, 0f, profile, 0f);
            scheduler.Register(null, 2, 2, Vector3.right, 0f, profile, 0f);
            Assert.AreEqual(AnimalScheduler.InvalidHandle, scheduler.Register(null, 3, 3, Vector3.left, 0f, profile, 0f));
        }

        [Test]
        public void Tick_DoesNotAllocate()
        {
            var scheduler = Populate(70, maxA: 30, radius: 6f);
            float time = 0f;
            Run(scheduler, 2f, ref time);
            Assert.That(() =>
            {
                for (int f = 0; f < 600; f++)
                {
                    time += Dt;
                    scheduler.Tick(time, Dt);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void QualitySettings_FollowTheBudgets()
        {
            Assert.AreEqual(20, SchedulerSettings.ForQuality(QualityTier.Low, false).MaxTierA);
            Assert.AreEqual(30, SchedulerSettings.ForQuality(QualityTier.Medium, false).MaxTierA);
            Assert.AreEqual(40, SchedulerSettings.ForQuality(QualityTier.High, false).MaxTierA);
            Assert.Less(SchedulerSettings.ForQuality(QualityTier.Medium, true).TickRateB, SchedulerSettings.ForQuality(QualityTier.Medium, false).TickRateB);
        }

        [Test]
        public void Profile_OneShotDurations_MatchTheFamilyClips()
        {
            var profile = new BehaviorProfile();
            var clips = new Dictionary<string, float>();
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Animations/SmallQuadruped/AN_SmallQuadruped.fbx"))
                if (asset is AnimationClip clip) clips[clip.name] = clip.length;

            // Exit time 0.92 + 0.12 s fade, as built by AnimalPipeline.
            float Expected(string name) => clips["AN_SmallQuadruped_" + name] * 0.92f + 0.12f;
            Assert.AreEqual(Expected("Idle_B"), profile.idleLookSeconds, 0.05f);
            Assert.AreEqual(Expected("Inspect"), profile.inspectSeconds, 0.05f);
            Assert.AreEqual(Expected("ReactTouch"), profile.reactSeconds, 0.05f);
            Assert.AreEqual(Expected("Happy"), profile.happySeconds, 0.05f);
            Assert.AreEqual(Expected("Sleep_Exit"), profile.wakeSeconds, 0.05f);
            Assert.AreEqual(Expected("Social_Nose"), profile.socialSeconds, 0.05f);
        }
    }
}
