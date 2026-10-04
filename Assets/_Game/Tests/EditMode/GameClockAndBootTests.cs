using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PocketWilds.Core;
using UnityEngine.TestTools;

namespace PocketWilds.Tests.EditMode
{
    public class GameClockTests
    {
        [Test]
        public void FakeClock_AdvanceMovesWallAndSessionTime()
        {
            var clock = new FakeGameClock(TestUtil.T0);
            clock.Advance(TimeSpan.FromHours(3));
            Assert.AreEqual(TestUtil.T0.AddHours(3), clock.UtcNow);
            Assert.AreEqual(3 * 3600.0, clock.SessionSeconds, 1e-9);
        }

        [Test]
        public void FakeClock_SetMovesOnlyWallTime_AndRejectsNegativeAdvance()
        {
            var clock = new FakeGameClock(TestUtil.T0);
            clock.Set(TestUtil.T0.AddDays(-1));
            Assert.AreEqual(0.0, clock.SessionSeconds);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromSeconds(-1)));
        }

        [Test]
        public void OfflineDelta_ClampsBackwardsClockToZero()
        {
            var clock = new FakeGameClock(TestUtil.T0);
            Assert.AreEqual(TimeSpan.FromDays(30), clock.GetOfflineDelta(TestUtil.T0.AddDays(-30)));
            Assert.AreEqual(TimeSpan.Zero, clock.GetOfflineDelta(TestUtil.T0.AddMinutes(5)));
        }

        [Test]
        public void RuntimeClock_SessionIsMonotonic_AndWallClockIsCurrent()
        {
            var clock = new RuntimeGameClock();
            double a = clock.SessionSeconds;
            Thread.Sleep(5);
            double b = clock.SessionSeconds;
            Assert.GreaterOrEqual(b, a);
            Assert.Less(Math.Abs((clock.UtcNow - DateTimeOffset.UtcNow).TotalSeconds), 1.0);
            Assert.AreEqual(TimeSpan.Zero, clock.GetOfflineDelta(DateTimeOffset.UtcNow.AddHours(1)));
        }
    }

    public class BootSequenceTests
    {
        [UnityTest]
        public IEnumerator BlockingSteps_RunInDeclaredOrder_IncludingAsyncOnes()
        {
            var order = new List<string>();
            var sequence = new BootSequence();
            sequence.AddBlocking("A", () => order.Add("A"));
            sequence.AddBlocking("B", async ct =>
            {
                await Task.Yield();
                order.Add("B");
            });
            sequence.AddBlocking("C", () => order.Add("C"));

            yield return TestUtil.Await(sequence.RunAsync(CancellationToken.None));

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, order);
            Assert.AreEqual(3, sequence.Reports.Count);
        }

        [UnityTest]
        public IEnumerator FailingBlockingStep_StopsBoot_AndRethrows()
        {
            var order = new List<string>();
            var sequence = new BootSequence();
            sequence.AddBlocking("A", () => order.Add("A"));
            sequence.AddBlocking("Broken", () => throw new InvalidOperationException("boom"));
            sequence.AddBlocking("C", () => order.Add("C"));

            var task = sequence.RunAsync(CancellationToken.None);
            while (!task.IsCompleted)
                yield return null;

            Assert.IsTrue(task.IsFaulted);
            CollectionAssert.AreEqual(new[] { "A" }, order);
            Assert.AreEqual(BootStepOutcome.Failed, sequence.Reports[1].Outcome);
        }

        [UnityTest]
        public IEnumerator BackgroundSteps_StartAfterBlocking_AndNeverFailTheBoot()
        {
            var order = new List<string>();
            var sequence = new BootSequence();
            sequence.AddBackground("Cloud", async ct =>
            {
                order.Add("Cloud");
                await Task.Yield();
                throw new TimeoutException("offline");
            });
            sequence.AddBlocking("Save", () => order.Add("Save"));

            yield return TestUtil.Await(sequence.RunAsync(CancellationToken.None));
            yield return TestUtil.Await(sequence.BackgroundCompletion);

            CollectionAssert.AreEqual(new[] { "Save", "Cloud" }, order);
            Assert.AreEqual(BootStepOutcome.Failed, sequence.Reports[sequence.Reports.Count - 1].Outcome);
        }

        [Test]
        public void Cancellation_BeforeStart_Throws_AndSequenceCannotBeExtendedAfterStart()
        {
            var sequence = new BootSequence();
            sequence.AddBlocking("A", () => { });
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var task = sequence.RunAsync(cts.Token);
            Assert.IsTrue(task.IsCanceled || task.IsFaulted);
            Assert.Throws<InvalidOperationException>(() => sequence.AddBlocking("B", () => { }));
        }
    }
}
