using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PocketWilds.Editor.Economy;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T35C, doc « 02 — Économie des Gemmes », section 7: the economy simulator plays the 6 profiles for 120 days on the
    /// shipped content in under 10 seconds, writes its CSV and report in Docs/Economy, and every guardrail of the Gems
    /// doc (sections 5 and 6), of the Upgrades doc (sections 1 and 17) and of the Renewal doc (section 1.3) holds. A
    /// guardrail the docs' own numbers cannot reach waits for the owner's decision in
    /// <see cref="SimGuardrails.PendingOwnerDecision"/>, with its reason; any other guardrail out of bounds fails.
    /// </summary>
    public class EconomySimulatorTests
    {
        private List<SimRun> _runs = new List<SimRun>();
        private List<SimCheck> _checks = new List<SimCheck>();
        private double _seconds;

        [OneTimeSetUp]
        public void RunOnce()
        {
            var shipped = SimReport.LoadShipped();
            var watch = Stopwatch.StartNew();
            _runs = EconomySimulator.RunAll(shipped);
            _seconds = watch.Elapsed.TotalSeconds;
            _checks = SimGuardrails.Evaluate(_runs, _seconds);
            SimReport.Write(_runs, _checks, _seconds, SimReport.RunOptimizer(shipped));
        }

        [Test]
        public void SixProfilesOf120DaysRunInUnderTenSeconds()
        {
            Assert.AreEqual(6, _runs.Count);
            foreach (var run in _runs)
                Assert.AreEqual(EconomySimulator.DefaultDays, run.Days.Count, run.Profile.Id);
            Assert.Less(_seconds, 10.0, $"{_seconds:F2} s");
        }

        [Test]
        public void SameInputsGiveTheSameDays()
        {
            var shipped = SimReport.LoadShipped();
            var profile = SimProfile.All()[(int)SimProfileKind.Regular];
            var a = EconomySimulator.Run(shipped, profile, 20);
            var b = EconomySimulator.Run(shipped, profile, 20);
            for (int i = 0; i < a.Days.Count; i++)
            {
                Assert.AreEqual(a.Days[i].Hatches, b.Days[i].Hatches, $"day {i + 1}");
                Assert.AreEqual(a.Days[i].RefugeScore, b.Days[i].RefugeScore, $"day {i + 1}");
                Assert.AreEqual(a.Days[i].LightLog10, b.Days[i].LightLog10, 1e-9, $"day {i + 1}");
            }
        }

        [Test]
        public void EveryGuardrailHoldsOrWaitsForTheOwner()
        {
            var failures = new StringBuilder();
            foreach (var check in _checks)
            {
                if (!check.Pass && !SimGuardrails.PendingOwnerDecision.ContainsKey(check.Id))
                    failures.Append('\n').Append(check.Id).Append(" : ").Append(check.Rule).Append(" (mesuré : ").Append(check.Measured).Append(')');
            }
            Assert.IsTrue(failures.Length == 0, "Garde-fous hors bornes :" + failures);
        }

        [Test]
        public void EveryPendingGuardrailExistsAndHasItsReason()
        {
            var ids = new HashSet<string>(_checks.Select(c => c.Id));
            foreach (var pair in SimGuardrails.PendingOwnerDecision)
            {
                Assert.IsTrue(ids.Contains(pair.Key), $"{pair.Key} : garde-fou inconnu");
                Assert.IsFalse(string.IsNullOrWhiteSpace(pair.Value), $"{pair.Key} : raison manquante");
            }
        }
    }
}
