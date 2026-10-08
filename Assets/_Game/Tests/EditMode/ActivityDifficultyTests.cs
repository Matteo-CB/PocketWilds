using NUnit.Framework;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T37: the adaptive difficulty (doc 01 Contenu, section 12.0): d starts at 3, two 0-star games in a row lower it, two
    /// 3-star games raise it, it stays between 1 and 10, the Remote Config counts and step apply; parameters at d1, d5 and
    /// d10 joined by straight lines and rounded; a challenge's fixed parameters.
    /// </summary>
    public sealed class ActivityDifficultyTests
    {
        private static int Play(ref ActivityRecord record, ActivityConfig config, params int[] stars)
        {
            for (int i = 0; i < stars.Length; i++)
                AdaptiveDifficulty.Apply(ref record, stars[i], config);
            return AdaptiveDifficulty.Level(in record);
        }

        [Test]
        public void DStartsAt3_AndMovesAfterTwoGamesInARow()
        {
            var config = new ActivityConfig();
            var record = default(ActivityRecord);
            Assert.AreEqual(3, AdaptiveDifficulty.Level(in record));
            Assert.AreEqual(3, Play(ref record, config, 0));
            Assert.AreEqual(2, Play(ref record, config, 0), "Two failures: a little easier.");
            Assert.AreEqual(2, Play(ref record, config, 0), "The streak starts again after a change.");
            Assert.AreEqual(1, Play(ref record, config, 0));
            Assert.AreEqual(1, Play(ref record, config, 0, 0, 0, 0), "Never under 1.");

            Assert.AreEqual(1, Play(ref record, config, 3));
            Assert.AreEqual(2, Play(ref record, config, 3), "Two 3-star games: a little harder.");
            Assert.AreEqual(10, Play(ref record, config, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3), "Never over 10.");
        }

        [Test]
        public void AnythingElse_BreaksTheStreak()
        {
            var config = new ActivityConfig();
            var record = default(ActivityRecord);
            Assert.AreEqual(3, Play(ref record, config, 0, 1, 0), "0, 1, 0: no change.");
            record = default;
            Assert.AreEqual(3, Play(ref record, config, 3, 2, 3), "3, 2, 3: no change.");
            record = default;
            Assert.AreEqual(3, Play(ref record, config, 3, 0, 3), "A failure breaks a perfect streak.");
            Assert.AreEqual(4, Play(ref record, config, 3));
            record = default;
            Assert.AreEqual(3, Play(ref record, config, 0, 3, 0), "A perfect game breaks a failure streak.");
            Assert.AreEqual(2, Play(ref record, config, 0));
        }

        [Test]
        public void TheCountsAndTheStep_FollowRemoteConfig()
        {
            var config = new ActivityConfig();
            Assert.IsTrue(config.TryOverride("activities.adaptive.easier_after_fails", 3));
            Assert.IsTrue(config.TryOverride("activities.adaptive.harder_after_perfects", 1));
            Assert.IsFalse(config.TryOverride("activities.adaptive.easier_after_fails", 6));
            var record = default(ActivityRecord);
            Assert.AreEqual(3, Play(ref record, config, 0, 0));
            Assert.AreEqual(2, Play(ref record, config, 0));
            Assert.AreEqual(3, Play(ref record, config, 3), "One perfect game is enough now.");

            Assert.IsTrue(config.TryOverride("activities.adaptive.step", 0.2));
            Assert.IsFalse(config.TryOverride("activities.adaptive.step", 0.5));
            Assert.AreEqual(2, config.AdaptiveLevels);
            Assert.AreEqual(5, Play(ref record, config, 3), "A step of 0.2 moves two levels.");
            Assert.IsTrue(config.TryOverride("activities.adaptive.step", 0.02));
            Assert.AreEqual(1, config.AdaptiveLevels, "At least one level.");
        }

        [Test]
        public void Parameters_AreStraightLinesBetweenD1D5AndD10_Rounded()
        {
            // Grooming's dirty zones (doc 01, S-ACT-001): 16, 24, 32.
            int[] zones = { 16, 18, 20, 22, 24, 26, 27, 29, 30, 32 };
            for (int d = 1; d <= 10; d++)
                Assert.AreEqual(zones[d - 1], AdaptiveDifficulty.Param(16, 24, 32, d), "zones at d" + d);

            // Nut Drop's items per second (1 decimal) and basket width (2 decimals).
            Assert.AreEqual(1.0, AdaptiveDifficulty.Param(1.0, 1.6, 2.4, 1, 1), 1e-9);
            Assert.AreEqual(1.3, AdaptiveDifficulty.Param(1.0, 1.6, 2.4, 3, 1), 1e-9);
            Assert.AreEqual(1.6, AdaptiveDifficulty.Param(1.0, 1.6, 2.4, 5, 1), 1e-9);
            Assert.AreEqual(2.1, AdaptiveDifficulty.Param(1.0, 1.6, 2.4, 8, 1), 1e-9, "2.08 rounds to 2.1.");
            Assert.AreEqual(2.4, AdaptiveDifficulty.Param(1.0, 1.6, 2.4, 10, 1), 1e-9);
            Assert.AreEqual(1.15, AdaptiveDifficulty.Param(1.20, 1.00, 0.85, 2, 2), 1e-9, "Decreasing values too.");
            Assert.AreEqual(0.94, AdaptiveDifficulty.Param(1.20, 1.00, 0.85, 7, 2), 1e-9);
            Assert.AreEqual(16, AdaptiveDifficulty.Param(16, 24, 32, 0), "d is clamped to 1 to 10.");
            Assert.AreEqual(32, AdaptiveDifficulty.Param(16, 24, 32, 12));

            var parameter = new DifficultyParameter("debris", 4, 8, 12);
            Assert.AreEqual(6.0, parameter.At(3), 1e-9);
        }

        [Test]
        public void AChallenge_FixesItsParameters_AndItsThresholds()
        {
            var rules = ActivityTestRig.TestRules(ActivityId.Test);
            Assert.AreEqual(1.5, rules.Value(0, 3), 1e-9, "Free mode at d3: speed 1.5.");
            Assert.AreEqual(15.0, rules.Value(1, 3), 1e-9);
            Assert.AreEqual(1.0, rules.Value(0, 9, 0), 1e-9, "Challenge 1 is at d1, whatever d.");
            Assert.AreEqual(9.5, rules.Value(0, 3, 1), 1e-9, "Challenge 2 fixes its speed.");
            Assert.AreEqual(20.0, rules.Value(1, 3, 1), 1e-9, "Its other parameter is at its level, d5.");
            Assert.IsTrue(double.IsNaN(rules.Value(5, 3)));
            Assert.AreEqual(100, rules.Thresholds(-1).One);
            Assert.AreEqual(50, rules.Thresholds(4).One);
            Assert.AreEqual(20f, rules.ChallengeSeconds(2, 30f));
            Assert.AreEqual(30f, rules.ChallengeSeconds(3, 30f));
        }

        [Test]
        public void StarThresholds_GiveZeroToThreeStars()
        {
            var stars = new StarThresholds(200, 380, 520);
            Assert.AreEqual(0, stars.Stars(199));
            Assert.AreEqual(1, stars.Stars(200));
            Assert.AreEqual(2, stars.Stars(519));
            Assert.AreEqual(3, stars.Stars(520));
            Assert.AreEqual(0, default(StarThresholds).Stars(1000), "No thresholds, no stars.");
            Assert.Throws<System.ArgumentException>(() => new StarThresholds(300, 200, 400));
        }

        [Test]
        public void ThroughTheRunner_PracticeCounts_ChallengesDoNot()
        {
            var rig = ActivityTestRig.Create();
            rig.Play(ActivityMode.Practice, 0);
            rig.Play(ActivityMode.Challenge, 0, challenge: 0);
            rig.Play(ActivityMode.Challenge, 0, challenge: 0);
            Assert.AreEqual(3, rig.S.Difficulty(ActivityId.Test), "Challenges neither count nor break the streak.");
            rig.Play(ActivityMode.Free, 0);
            Assert.AreEqual(2, rig.S.Difficulty(ActivityId.Test), "A practice failure and a rewarded one: easier.");
            Assert.AreEqual(3, rig.Controller.Setup.Difficulty, "That game was played at d3.");
            rig.Play(ActivityMode.Free, 0);
            Assert.AreEqual(2, rig.Controller.Setup.Difficulty, "The next one is played at d2.");
            Assert.AreEqual(1.3, rig.Controller.Setup.Param(0), 1e-9, "Its speed: 1.25 rounded to 1 decimal.");
        }
    }
}
