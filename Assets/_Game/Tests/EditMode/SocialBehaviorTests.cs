using NUnit.Framework;
using PocketWilds.Presentation;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T25: greetings and points of interest, headless. Pairs are reserved together, meet nose to nose, are
    /// always linked both ways and are released cleanly; points of interest are walked to, faced and held
    /// by one animal at a time. No NavMesh, no allocation.
    /// </summary>
    public class SocialBehaviorTests
    {
        private const float Dt = 1f / 60f;

        private static SchedulerSettings Settings(int maxA = 30) => new SchedulerSettings
        {
            MaxTierA = maxA,
            TickRateA = 5f,
            TickRateB = 1f,
            TierUpdateInterval = 0.5f,
        };

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

        private static AnimalScheduler Populate(int count, BehaviorProfile profile, int maxA = 30, float radius = 4f)
        {
            var scheduler = new AnimalScheduler(128, Settings(maxA), new WanderArea(Vector3.zero, radius));
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            for (int i = 0; i < count; i++)
            {
                var position = new Vector3((i % columns - columns * 0.5f) * 0.7f, 0f, (i / columns - columns * 0.5f) * 0.7f);
                scheduler.Register(null, (uint)(i + 1), (uint)(1000 + i * 7919), position, i * 37f, profile, 0f);
            }
            return scheduler;
        }

        /// <summary>Two animals, tier A, idle: the first one invites the second.</summary>
        private static (AnimalScheduler scheduler, int a, int b, float time) Pair(float distance = 1.2f)
        {
            var scheduler = new AnimalScheduler(8, Settings(), new WanderArea(Vector3.zero, 4f));
            var profile = new BehaviorProfile { idleSeconds = new Vector2(30f, 30f) };
            int a = scheduler.Register(null, 1, 11, new Vector3(-distance * 0.5f, 0f, 0f), 90f, profile, 0f);
            int b = scheduler.Register(null, 2, 22, new Vector3(distance * 0.5f, 0f, 0.3f), 200f, profile, 0f);
            float time = 0f;
            Run(scheduler, 0.1f, ref time); // tiers settle (both A), nobody decides before 30 s
            return (scheduler, a, b, time);
        }

        private static float FacingError(AnimalScheduler s, int from, int to)
        {
            var dir = s.GetPosition(to) - s.GetPosition(from);
            float wanted = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(s.GetYaw(from), wanted));
        }

        [Test]
        public void Greeting_PairMeetsNoseToNose_FacingEachOther_ThenIsReleased()
        {
            var (s, a, b, time) = Pair();
            Assert.IsTrue(s.TryStartSocial(a, time));
            Assert.AreEqual(b, s.GetPartner(a));
            Assert.AreEqual(a, s.GetPartner(b));

            bool together = false;
            float startedAt = -1f, endA = -1f, endB = -1f;
            Run(s, 6f, ref time, () =>
            {
                bool sa = s.GetState(a) == AnimalBehaviorState.Social, sb = s.GetState(b) == AnimalBehaviorState.Social;
                if ((sa || sb) && startedAt < 0f)
                {
                    Assert.IsTrue(sa && sb, "Both start the greeting in the same frame.");
                    startedAt = time;
                    together = true;
                }
                if (startedAt > 0f && !sa && endA < 0f) endA = time;
                if (startedAt > 0f && !sb && endB < 0f) endB = time;
                if (startedAt > 0f && time - startedAt > 0.4f && time - startedAt < 1.2f)
                {
                    float gap = Vector3.Distance(s.GetPosition(a), s.GetPosition(b));
                    Assert.AreEqual(0.48f, gap, 0.03f, "Roots socialGap apart: noses just touch, never overlap (T33C).");
                    Assert.Less(FacingError(s, a, b), 6f);
                    Assert.Less(FacingError(s, b, a), 6f);
                }
            });
            Assert.IsTrue(together, "The greeting happened.");
            // Each one leaves on its own tick (5 Hz); the clips themselves end together.
            Assert.LessOrEqual(Mathf.Abs(endA - endB), 0.25f);
            Assert.AreEqual(1, s.SocialGreetings);
            Assert.AreEqual(AnimalScheduler.InvalidHandle, s.GetPartner(a));
            Assert.AreEqual(AnimalScheduler.InvalidHandle, s.GetPartner(b));
            Assert.AreNotEqual(AnimalBehaviorState.Social, s.GetState(a));
            Assert.AreNotEqual(AnimalBehaviorState.SocialWait, s.GetState(b));
        }

        [Test]
        public void Greeting_PartnerPoked_BreaksThePairWithoutLeavingAnyoneStuck()
        {
            var (s, a, b, time) = Pair(1.6f);
            Assert.IsTrue(s.TryStartSocial(a, time));
            Run(s, 0.3f, ref time);
            s.Poke(b, time);
            Assert.AreEqual(AnimalScheduler.InvalidHandle, s.GetPartner(a));
            Assert.AreEqual(AnimalScheduler.InvalidHandle, s.GetPartner(b));
            Assert.AreEqual(AnimalBehaviorState.React, s.GetState(b));
            Run(s, 4f, ref time);
            Assert.AreNotEqual(AnimalBehaviorState.SocialWait, s.GetState(a), "Nobody waits for a partner that left.");
            Assert.AreNotEqual(AnimalBehaviorState.Social, s.GetState(a));
            Assert.AreEqual(0, s.SocialGreetings);
        }

        [Test]
        public void Greeting_PartnerLeavesTheMeadow_FirstOneSettlesDown()
        {
            var (s, a, b, time) = Pair(1.6f);
            Assert.IsTrue(s.TryStartSocial(a, time));
            Run(s, 0.4f, ref time);
            s.Unregister(b);
            Assert.AreEqual(AnimalScheduler.InvalidHandle, s.GetPartner(a));
            Run(s, 5f, ref time);
            var state = s.GetState(a);
            Assert.IsTrue(state != AnimalBehaviorState.SocialWait && state != AnimalBehaviorState.Social, state.ToString());
        }

        [Test]
        public void Meadow_GreetsOverTime_LinksAlwaysMutual()
        {
            var profile = new BehaviorProfile();
            var s = Populate(24, profile, radius: 3.2f);
            float time = 0f;
            Run(s, 240f, ref time, () =>
            {
                for (int h = 0; h < 24; h++)
                {
                    int p = s.GetPartner(h);
                    if (p >= 0)
                        Assert.AreEqual(h, s.GetPartner(p), "A greeting reserves both animals.");
                    if (s.GetState(h) == AnimalBehaviorState.SocialWait)
                        Assert.GreaterOrEqual(p, 0, "Waiting always has a partner on its way.");
                }
            });
            Assert.Greater(s.SocialInvitations, 10, "Greetings happen in a lively meadow.");
            Assert.Greater(s.SocialGreetings, s.SocialInvitations * 6 / 10, "Most invitations reach the nose touch.");
        }

        [Test]
        public void TierB_NeverGreets()
        {
            var profile = new BehaviorProfile { socialWeight = 1f };
            var s = Populate(20, profile, maxA: 0);
            float time = 0f;
            Run(s, 120f, ref time);
            Assert.AreEqual(0, s.SocialInvitations);
        }

        [Test]
        public void DifferentSocialGroups_DoNotGreet()
        {
            var s = new AnimalScheduler(4, Settings(), new WanderArea(Vector3.zero, 4f));
            var a = s.Register(null, 1, 11, new Vector3(-0.5f, 0f, 0f), 0f, new BehaviorProfile { socialGroup = 0 }, 0f);
            s.Register(null, 2, 22, new Vector3(0.5f, 0f, 0f), 0f, new BehaviorProfile { socialGroup = 1 }, 0f);
            float time = 0f;
            Run(s, 0.1f, ref time);
            Assert.IsFalse(s.TryStartSocial(a, time), "Greeting clips are shared inside one family only.");
        }

        private static BehaviorProfile InspectOnly() => new BehaviorProfile
        {
            idleWeight = 0f, idleLookWeight = 0f, wanderWeight = 0f, eatWeight = 0f, inspectWeight = 1f, sleepWeight = 0f, socialWeight = 0f,
            idleSeconds = new Vector2(0.2f, 0.4f),
        };

        [Test]
        public void Inspect_WalksToThePoint_StopsBeforeIt_AndFacesIt()
        {
            var s = new AnimalScheduler(4, Settings(), new WanderArea(Vector3.zero, 4f));
            var point = new InterestPoint(new Vector3(1.5f, 0f, 1f), 0.45f, InterestKind.Inspect);
            s.SetInterests(new[] { point });
            int a = s.Register(null, 1, 11, new Vector3(-0.5f, 0f, -0.3f), 180f, InspectOnly(), 0f);
            float time = 0f;
            bool checkedOnce = false;
            Run(s, 12f, ref time, () =>
            {
                if (s.GetState(a) == AnimalBehaviorState.Inspect && s.GetInterest(a) == 0 && !checkedOnce)
                {
                    var flat = s.GetPosition(a) - point.position;
                    flat.y = 0f;
                    Assert.AreEqual(point.standOff, flat.magnitude, 0.06f);
                    checkedOnce = true;
                }
            });
            Assert.IsTrue(checkedOnce, "The animal reached the rock.");
            Assert.Greater(s.InterestVisits, 0);
        }

        [Test]
        public void Inspect_OneAnimalPerPoint()
        {
            var s = new AnimalScheduler(8, Settings(), new WanderArea(Vector3.zero, 4f));
            s.SetInterests(new[] { new InterestPoint(new Vector3(0f, 0f, 1f), 0.4f, InterestKind.Inspect) });
            var profile = InspectOnly();
            for (int i = 0; i < 4; i++)
                s.Register(null, (uint)(i + 1), (uint)(100 + i * 31), new Vector3(i * 0.5f - 0.75f, 0f, -0.5f), 0f, profile, 0f);
            float time = 0f;
            Run(s, 30f, ref time, () =>
            {
                int holders = 0;
                for (int h = 0; h < 4; h++)
                    if (s.GetInterest(h) == 0)
                    {
                        holders++;
                        Assert.AreEqual(h, s.GetInterestOwner(0));
                    }
                Assert.LessOrEqual(holders, 1);
            });
            Assert.Greater(s.InterestVisits, 3, "The point is shared over time.");
        }

        [Test]
        public void Tick_WithGreetingsAndPoints_DoesNotAllocate()
        {
            var s = Populate(40, new BehaviorProfile { socialWeight = 0.3f, inspectWeight = 0.3f, eatWeight = 0.3f }, maxA: 30, radius: 4f);
            s.SetInterests(new[]
            {
                new InterestPoint(new Vector3(2f, 0f, 1.5f), 0.5f, InterestKind.Inspect),
                new InterestPoint(new Vector3(-2f, 0f, 0f), 0.4f, InterestKind.Inspect),
                new InterestPoint(new Vector3(1f, 0f, -2f), 0.15f, InterestKind.Eat),
            });
            float time = 0f;
            Run(s, 5f, ref time);
            Assert.That(() =>
            {
                for (int f = 0; f < 1200; f++)
                {
                    time += Dt;
                    s.Tick(time, Dt);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.Greater(s.SocialInvitations + s.InterestVisits, 0);
        }
    }
}
