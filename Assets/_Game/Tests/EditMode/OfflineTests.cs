using System;
using System.Diagnostics;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Services.Save;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T18: away time is paid analytically (doc 02 OfflineGain = ProductionAtExit x EffectiveSeconds),
    /// for many durations, in constant time, without replaying ticks; rollbacks and replays pay nothing.
    /// </summary>
    public class OfflineTests
    {
        private const double Hour = 3600.0;
        private const double Day = 24.0 * Hour;

        private sealed class Fixture
        {
            public PlayerState State;
            public EconomyService Economy;
            public FakeGameClock Clock;
            public OfflineService Offline;

            /// <summary>What SaveService does on each save: stamp the time, then the snapshot hook.</summary>
            public void Save()
            {
                State.Meta.LastSaveUtc = Clock.UtcNow;
                Offline.CaptureSnapshot(State);
            }
        }

        private static Fixture Create(int rabbits = 3, PlayerState state = null, bool save = true)
        {
            var f = new Fixture { Clock = new FakeGameClock(TestUtil.T0) };
            f.State = state ?? PlayerState.CreateNew(TestUtil.T0);
            var content = TestUtil.ThreeSpecies();
            f.Economy = new EconomyService(f.State, content, EconomyConfig.Default, null);
            for (int i = 0; i < rabbits; i++)
            {
                uint id = f.State.Collection.Add(new AnimalInstance { Species = ContentId.FromSource("species.rabbit"), Seed = (uint)i });
                f.State.Collection.TryGet(id, out var animal);
                f.Economy.OnAnimalAdded(animal);
            }
            f.Offline = new OfflineService(f.State, f.Economy, f.Clock);
            if (save)
                f.Save();
            return f;
        }

        [Test]
        public void EffectiveSeconds_IsFullFor24h_ThenEasesTowardTheFloor()
        {
            var config = EconomyConfig.Default;
            Assert.AreEqual(0.0, OfflineService.EffectiveSeconds(0, config));
            Assert.AreEqual(0.0, OfflineService.EffectiveSeconds(-50, config));
            Assert.AreEqual(Hour, OfflineService.EffectiveSeconds(Hour, config));
            Assert.AreEqual(Day, OfflineService.EffectiveSeconds(Day, config), 1e-9);

            double floor = config.OfflineFloorEfficiency;
            double expected48 = Day + floor * Day + (1 - floor) * config.OfflineDecaySeconds * (1 - Math.Exp(-1));
            Assert.AreEqual(expected48, OfflineService.EffectiveSeconds(2 * Day, config), 1e-6);

            // Never decreasing, never above real time, never below the floor rate.
            double previous = 0.0;
            for (double t = 600; t <= 60 * Day; t += 600)
            {
                double e = OfflineService.EffectiveSeconds(t, config);
                double step = e - previous;
                Assert.LessOrEqual(step, 600.0 + 1e-6, "Efficiency never exceeds 1.");
                Assert.GreaterOrEqual(step, floor * 600.0 - 1e-6, "Long breaks still pay at least the floor.");
                previous = e;
            }
        }

        [TestCase(30.0)]
        [TestCase(10.0 * 60.0)]
        [TestCase(Hour)]
        [TestCase(8 * Hour)]
        [TestCase(Day)]
        [TestCase(3 * Day)]
        [TestCase(29 * Day)]
        public void Durations_PayProductionTimesEffectiveSeconds(double seconds)
        {
            var f = Create();
            var before = f.Economy.Light;
            var rate = f.Economy.GetLightPerSecond();
            f.Clock.Advance(TimeSpan.FromSeconds(seconds));

            var report = f.Offline.ResolveReturn();
            double effective = OfflineService.EffectiveSeconds(seconds, EconomyConfig.Default);
            Assert.AreEqual(seconds, report.AwaySeconds, 1e-6);
            Assert.AreEqual(seconds, report.CreditedSeconds, 1e-6);
            Assert.AreEqual(effective, report.EffectiveSeconds, 1e-6);
            Assert.IsTrue(BigValue.ApproximatelyEqual(BigValue.Multiply(rate, effective), report.Light, 1e-12));
            Assert.IsTrue(BigValue.ApproximatelyEqual(BigValue.Add(before, report.Light), f.Economy.Light, 1e-12));
            Assert.IsFalse(report.ClockRolledBack);
        }

        [Test]
        public void ShortBreak_MatchesTickReplay_WithoutReplayingTicks()
        {
            const double away = 2 * Hour;
            var analytic = Create();
            analytic.Clock.Advance(TimeSpan.FromSeconds(away));
            analytic.Offline.ResolveReturn();

            // Reference: what the frame loop would have produced, tick by tick (100 ms cadence).
            var replay = Create();
            for (int i = 0; i < (int)(away / 0.1); i++)
                replay.Economy.Advance(0.1);

            Assert.IsTrue(BigValue.ApproximatelyEqual(replay.Economy.Light, analytic.Economy.Light, 1e-9));
        }

        [Test]
        public void HugeAbsence_IsCapped_AndResolvesInConstantTimeWithoutAllocation()
        {
            var f = Create();
            f.Clock.Advance(TimeSpan.FromDays(400));
            var report = f.Offline.ResolveReturn();
            Assert.AreEqual(400 * Day, report.AwaySeconds, 1e-3);
            Assert.AreEqual(EconomyConfig.Default.OfflineMaxSeconds, report.CreditedSeconds, 1e-6, "Clock jumps are bounded.");

            // Constant time: 10 000 resolves of 400 days each, far below one frame.
            var away = TestUtil.T0.AddDays(400);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                f.State.Meta.LastSaveUtc = TestUtil.T0;
                f.State.Offline.HighWaterUtc = TestUtil.T0;
                f.Clock.Set(away);
                f.Offline.ResolveReturn();
            }
            watch.Stop();
            UnityEngine.Debug.Log($"[Benchmark] Offline.Resolve x10000 (400 days each): {watch.Elapsed.TotalMilliseconds:F2} ms");
            Assert.Less(watch.Elapsed.TotalMilliseconds, 100.0);

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    f.State.Meta.LastSaveUtc = TestUtil.T0;
                    f.State.Offline.HighWaterUtc = TestUtil.T0;
                    f.Clock.Set(away);
                    f.Offline.ResolveReturn();
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void SecondResolve_BeforeTheNextSave_PaysNothing()
        {
            var f = Create();
            f.Clock.Advance(TimeSpan.FromHours(5));
            Assert.IsTrue(f.Offline.ResolveReturn().HasGain);
            var light = f.Economy.Light;
            var again = f.Offline.ResolveReturn();
            Assert.IsFalse(again.HasGain, "The same interval is never paid twice.");
            Assert.AreEqual(light, f.Economy.Light);
        }

        [Test]
        public void ClockMovedBack_PaysNothing_UntilRealTimePassesTheMark()
        {
            var f = Create();
            f.Clock.Advance(TimeSpan.FromDays(1));
            f.Save(); // high-water mark: T0 + 1 day

            f.Clock.Set(TestUtil.T0); // the device clock goes back one day
            var back = f.Offline.ResolveReturn();
            Assert.IsTrue(back.ClockRolledBack);
            Assert.IsFalse(back.HasGain);

            f.Clock.Advance(TimeSpan.FromHours(1));
            f.Save(); // played an hour on the wrong clock: LastSaveUtc = T0 + 1 h, mark stays T0 + 1 day

            f.Clock.Set(TestUtil.T0.AddDays(1).AddHours(2));
            var report = f.Offline.ResolveReturn();
            Assert.AreEqual(2 * Hour, report.CreditedSeconds, 1e-6, "Only time after the mark counts.");
            Assert.AreEqual(Day + Hour, report.AwaySeconds, 1e-6);
        }

        [Test]
        public void SmallBackwardsCorrection_IsNotARollback()
        {
            var f = Create();
            f.Clock.Set(TestUtil.T0.AddSeconds(-30)); // network time sync
            var report = f.Offline.ResolveReturn();
            Assert.IsFalse(report.ClockRolledBack);
            Assert.IsFalse(report.HasGain);
        }

        [Test]
        public void RateAtSave_IsUsed_NotTodaysRate()
        {
            var f = Create();
            var rateAtExit = f.Economy.GetLightPerSecond();
            f.Economy.SetGlobalMultiplier(BigValue.FromDouble(2)); // new balance shipped while away
            f.Clock.Advance(TimeSpan.FromHours(1));
            var report = f.Offline.ResolveReturn();
            Assert.AreEqual(rateAtExit, report.RatePerSecond);
            Assert.IsTrue(BigValue.ApproximatelyEqual(BigValue.Multiply(rateAtExit, Hour), report.Light, 1e-12));
        }

        [Test]
        public void SaveWithoutSnapshot_RebuildsTheRateFromTheCollection()
        {
            var f = Create();
            f.State.Offline.HasRate = false; // schema 1 save
            f.State.Offline.RateAtSave = BigValue.Zero;
            f.Clock.Advance(TimeSpan.FromHours(1));
            var report = f.Offline.ResolveReturn();
            Assert.AreEqual(f.Economy.GetLightPerSecond(), report.RatePerSecond);
            Assert.IsTrue(report.HasGain);
        }

        [Test]
        public void ShortBreaks_ArePaidSilently_LongerOnesOpenTheReturnScreenOnce()
        {
            var f = Create();
            f.Clock.Advance(TimeSpan.FromSeconds(60));
            Assert.IsTrue(f.Offline.ResolveReturn().HasGain);
            Assert.IsFalse(f.Offline.TryTakePending(out _), "One minute away: no screen.");

            f.Save();
            f.Clock.Advance(TimeSpan.FromMinutes(10));
            var report = f.Offline.ResolveReturn();
            Assert.IsTrue(f.Offline.TryTakePending(out var pending));
            Assert.AreEqual(report.Light, pending.Light);
            Assert.IsFalse(f.Offline.TryTakePending(out _), "Taken once.");

            var empty = Create(rabbits: 0);
            empty.Clock.Advance(TimeSpan.FromHours(3));
            Assert.IsFalse(empty.Offline.ResolveReturn().HasGain);
            Assert.IsFalse(empty.Offline.TryTakePending(out _), "Nothing earned: no screen.");
        }

        [Test]
        public void MarkActive_StartsTheGap_EvenWhenThePauseSaveFails()
        {
            var f = Create();
            f.Clock.Advance(TimeSpan.FromMinutes(30)); // played 30 min, production already in the wallet
            f.Offline.MarkActive();                    // app paused, save refused
            f.Clock.Advance(TimeSpan.FromHours(2));
            var report = f.Offline.ResolveReturn();
            Assert.AreEqual(2 * Hour, report.CreditedSeconds, 1e-6, "The played half hour is not paid again.");
        }

        [Test]
        public void OfflineSnapshot_SurvivesTheSaveFile()
        {
            var f = Create();
            f.Clock.Advance(TimeSpan.FromMinutes(7));
            f.Save();
            var serializer = new BinarySaveSerializer();
            var writer = new ByteBufferWriter();
            serializer.Serialize(f.State, writer);
            var bytes = writer.WrittenSpan.ToArray();
            var loaded = serializer.Deserialize(serializer.ReadDocument(bytes, bytes.Length));
            Assert.IsTrue(loaded.Offline.HasRate);
            Assert.AreEqual(f.State.Offline.RateAtSave, loaded.Offline.RateAtSave);
            Assert.AreEqual(f.State.Offline.HighWaterUtc, loaded.Offline.HighWaterUtc);

            // Same away time after a restart: same gain as without the restart.
            var restarted = Create(rabbits: 0, state: loaded, save: false);
            restarted.Clock.Set(f.Clock.UtcNow.AddHours(6));
            f.Clock.Advance(TimeSpan.FromHours(6));
            Assert.AreEqual(f.Offline.ResolveReturn().Light, restarted.Offline.ResolveReturn().Light);
        }
    }
}
