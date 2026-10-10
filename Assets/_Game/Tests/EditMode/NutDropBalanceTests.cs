using System;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// How a scripted player catches (T40 balance checks): how soon it sees a new item, how fast its finger slides, how
    /// precisely it aims, how often it lets a nut go, and how often it dodges a pinecone heading for its basket.
    /// </summary>
    internal sealed class CatcherSkill
    {
        public string Name;

        /// <summary>Seconds after an item appears before the player reacts to it.</summary>
        public float Reaction;

        /// <summary>The finger's top speed, playfield units per second (the basket follows at up to 14).</summary>
        public float FingerSpeed;

        /// <summary>The aim's error, ± this many units around the item (seeded per item).</summary>
        public float AimError;

        /// <summary>Share of the nuts the player lets go (distracted, or another one looked better).</summary>
        public float SkipChance;

        /// <summary>Share of the pinecones the player sees coming and dodges.</summary>
        public float DodgeChance = 1f;

        /// <summary>Share of the pinecones the player takes for a nut at a glance and goes to catch.</summary>
        public float ChaseChance;

        /// <summary>A strong player: reacts in 0.15 s, a 9 u/s finger, aims within ± 0.04, never lets a nut go, dodges every pinecone.</summary>
        public static CatcherSkill Strong() => new CatcherSkill
        {
            Name = "strong", Reaction = 0.15f, FingerSpeed = 9f, AimError = 0.04f, SkipChance = 0f, DodgeChance = 1f,
        };

        /// <summary>
        /// A casual player: reacts in 0.6 s, a 2.4 u/s finger, aims within ± 0.35, lets 30 % of the nuts go, takes 30 % of the
        /// pinecones for nuts, and dodges a third of the others heading for its basket.
        /// </summary>
        public static CatcherSkill Casual() => new CatcherSkill
        {
            Name = "casual", Reaction = 0.6f, FingerSpeed = 2.4f, AimError = 0.35f, SkipChance = 0.3f, DodgeChance = 0.33f,
            ChaseChance = 0.3f,
        };
    }

    /// <summary>
    /// Plays a whole nut drop game like a person would, through a tick callback (the runner, or the controller alone): each
    /// item is noticed after the reaction time; the finger slides toward the next nut it means to catch (the earliest to reach
    /// the rim), aiming at the side of its catch zone away from a pinecone landing close in time; a pinecone about to land in
    /// the basket is dodged (when the player sees it coming); the basket follows the finger.
    /// </summary>
    internal static class ScriptedCatcher
    {
        public const float Step = 1f / 60f;

        public static void Play(NutDropController game, CatcherSkill skill, uint seed, Action<float> tick, Func<bool> running)
        {
            int n = NutDropController.Capacity;
            var seen = new double[n];
            var aim = new float[n];
            var skip = new bool[n];
            var dodge = new bool[n];
            var chase = new bool[n];
            var side = new sbyte[n];
            var rng = new DeterministicRandom(SeedHash.Mix(seed, 0xCA7C4u));
            for (int i = 0; i < n; i++)
                seen[i] = double.PositiveInfinity;
            float finger = game.BasketX;
            int guard = 0;
            while (running() && !game.Finished && guard++ < 200000)
            {
                // New items: when they will be noticed, the aim's error, a nut let go, a pinecone dodged.
                for (int e = 0; e < game.EventCount; e++)
                {
                    var ev = game.Event(e);
                    if (ev.Kind != NutDropEventKind.Spawned)
                        continue;
                    seen[ev.Item] = game.Elapsed + skill.Reaction;
                    aim[ev.Item] = skill.AimError * (float)(2.0 * rng.NextDouble() - 1.0);
                    double roll = rng.NextDouble();
                    double glance = rng.NextDouble();
                    bool pinecone = ev.ItemKind == NutDropItemKind.Pinecone;
                    skip[ev.Item] = !pinecone && roll < skill.SkipChance;
                    chase[ev.Item] = pinecone && glance < skill.ChaseChance;
                    dodge[ev.Item] = pinecone && !chase[ev.Item] && roll < skill.DodgeChance;
                    side[ev.Item] = 0;
                }
                game.ClearEvents();

                float want = Want(game, skill, seen, aim, skip, dodge, chase, side);
                float slide = skill.FingerSpeed * Step;
                finger += Math.Max(-slide, Math.Min(slide, want - finger));
                game.SetBasketTarget(finger);
                tick(Step);
            }
        }

        /// <summary>Where the player wants the basket now.</summary>
        private static float Want(NutDropController game, CatcherSkill skill, double[] seen, float[] aim, bool[] skip, bool[] dodge,
            bool[] chase, sbyte[] side)
        {
            double now = game.Elapsed;
            float reach = game.BasketWidth * 0.5f + game.ItemRadius;
            int target = -1;
            float targetTime = float.MaxValue;
            for (int i = 0; i < NutDropController.Capacity; i++)
            {
                if (!game.ItemActive(i) || game.ItemMissed(i) || now < seen[i] || skip[i])
                    continue;
                // A pinecone only when taken for a nut at a glance.
                if (game.ItemKind(i) == NutDropItemKind.Pinecone && !chase[i])
                    continue;
                float t = game.ItemRimSeconds(i);
                // Too far to reach in time: the next one.
                if (Math.Abs(game.ItemRimX(i) - game.BasketX) - reach > skill.FingerSpeed * t + 0.02f)
                    continue;
                if (t < targetTime)
                {
                    targetTime = t;
                    target = i;
                }
            }
            float want = target >= 0 ? game.ItemRimX(target) + aim[target] : game.BasketTarget;
            if (target >= 0)
            {
                // A pinecone landing close in time: catch the nut with the side of the basket away from it.
                float x = game.ItemRimX(target);
                float edge = reach - 0.05f;
                for (int i = 0; i < NutDropController.Capacity; i++)
                {
                    if (!game.ItemActive(i) || game.ItemMissed(i) || game.ItemKind(i) != NutDropItemKind.Pinecone || !dodge[i] || now < seen[i])
                        continue;
                    if (Math.Abs(game.ItemRimSeconds(i) - targetTime) > 0.45f)
                        continue;
                    float p = game.ItemRimX(i);
                    if (Math.Abs(want - p) >= reach + 0.06f)
                        continue;
                    float away = x >= p ? 1f : -1f;
                    want = Math.Max(x - edge, Math.Min(x + edge, p + away * (reach + 0.08f)));
                }
            }
            // A pinecone about to land where the basket would be: hurry past it when there is time, else wait beside it; once
            // chosen, the side is kept.
            float limit = game.BasketLimit;
            float speed = Math.Min(skill.FingerSpeed, 12f);
            for (int i = 0; i < NutDropController.Capacity; i++)
            {
                if (!game.ItemActive(i) || game.ItemMissed(i) || game.ItemKind(i) != NutDropItemKind.Pinecone || !dodge[i] || now < seen[i])
                    continue;
                float t = game.ItemRimSeconds(i);
                if (t > 0.6f || (target >= 0 && t > targetTime + 0.05f))
                    continue;
                float p = game.ItemRimX(i);
                float zone = reach + 0.06f;
                float left = p - zone - 0.06f, right = p + zone + 0.06f;
                if (side[i] == 0)
                {
                    float travel = speed * Math.Max(0f, t - 0.15f);
                    float there = game.BasketX + Math.Max(-travel, Math.Min(travel, want - game.BasketX));
                    if (Math.Abs(there - p) > zone)
                        continue;
                    bool leftOk = left >= -limit && Math.Abs(left - game.BasketX) <= travel * 0.8f;
                    bool rightOk = right <= limit && Math.Abs(right - game.BasketX) <= travel * 0.8f;
                    bool here = game.BasketX <= p;
                    if (leftOk && rightOk)
                        side[i] = (sbyte)(Math.Abs(left - want) <= Math.Abs(right - want) ? -1 : 1);
                    else if (leftOk || rightOk)
                        side[i] = (sbyte)(leftOk ? -1 : 1);
                    else if (left < -limit)
                        side[i] = 1;
                    else if (right > limit)
                        side[i] = -1;
                    else
                        side[i] = (sbyte)(here ? -1 : 1);
                }
                want = side[i] < 0 ? left : right;
            }
            return Math.Max(-limit, Math.Min(limit, want));
        }
    }

    /// <summary>
    /// T40 balance (doc 01 Contenu, S-ACT-003; doc Mini-jeux, section 6 « 10 défis équilibrés »): scripted players on the
    /// free mode and the 10 challenges, over six seeds. A strong player reaches 3 stars in the free mode from d1 to d10 and
    /// in every challenge; a casual one 1 to 2 stars in the free mode at d3 and at least 1 in every challenge.
    /// </summary>
    public sealed class NutDropBalanceTests
    {
        internal const int Seeds = 6;

        internal static ActivitySetup Setup(ActivityRules rules, int difficulty, int challenge = -1, uint seed = 1, bool slower = false)
        {
            float seconds = rules.ChallengeSeconds(challenge, NutDropRules.BaseSeconds);
            return new ActivitySetup
            {
                Activity = ActivityId.NutRain,
                Mode = challenge >= 0 ? ActivityMode.Challenge : ActivityMode.Free,
                Challenge = challenge,
                Difficulty = challenge >= 0 ? rules.Challenges[challenge].Level : difficulty,
                Seed = seed,
                Seconds = seconds * (slower ? ActivityConfig.SlowerTimeMultiplier : 1f),
                Slower = slower,
                SpeedMultiplier = slower ? ActivityConfig.SlowerSpeedMultiplier : 1f,
                Rules = rules,
                Thresholds = rules.Thresholds(challenge),
            };
        }

        /// <summary>One whole game on the controller alone; returns what the stars measure (the score, challenge 6's streak).</summary>
        internal static int PlayAlone(NutDropController game, in ActivitySetup setup, CatcherSkill skill)
        {
            game.Begin(in setup);
            ScriptedCatcher.Play(game, skill, setup.Seed, dt => game.Tick(dt), () => !game.Finished);
            Assert.IsTrue(game.Finished, "The game ended.");
            return setup.Challenge >= 0 ? game.ChallengeScore : game.Score;
        }

        internal static uint SeedOf(int index) => (uint)(index + 1) * 7919u;

        internal static int Worst(ActivityRules rules, int difficulty, int challenge, CatcherSkill skill, int seeds = Seeds)
        {
            var game = new NutDropController();
            int worst = int.MaxValue;
            for (int s = 0; s < seeds; s++)
            {
                var setup = Setup(rules, difficulty, challenge, SeedOf(s));
                worst = Math.Min(worst, PlayAlone(game, in setup, skill));
            }
            return worst;
        }

        internal static int Best(ActivityRules rules, int difficulty, int challenge, CatcherSkill skill, int seeds = Seeds)
        {
            var game = new NutDropController();
            int best = int.MinValue;
            for (int s = 0; s < seeds; s++)
            {
                var setup = Setup(rules, difficulty, challenge, SeedOf(s));
                best = Math.Max(best, PlayAlone(game, in setup, skill));
            }
            return best;
        }

        [Test]
        public void AStrongPlayer_Reaches3Stars_InTheFreeMode_FromD1ToD10()
        {
            var rules = NutDropRules.Create();
            for (int d = 1; d <= 10; d++)
            {
                int worst = Worst(rules, d, -1, CatcherSkill.Strong());
                TestContext.WriteLine($"d{d}: strong {worst} ({rules.Free.One}/{rules.Free.Two}/{rules.Free.Three})");
                Assert.AreEqual(3, rules.Free.Stars(worst), "A strong player at d" + d + ": " + worst);
            }
        }

        [Test]
        public void ACasualPlayer_Gets1To2Stars_InTheFreeMode_AtD3()
        {
            var rules = NutDropRules.Create();
            int worst = Worst(rules, AdaptiveDifficulty.Start, -1, CatcherSkill.Casual());
            int best = Best(rules, AdaptiveDifficulty.Start, -1, CatcherSkill.Casual());
            TestContext.WriteLine($"d3: casual {worst} to {best}");
            Assert.GreaterOrEqual(rules.Free.Stars(worst), 1, "A casual player at d3, worst seed: " + worst);
            Assert.LessOrEqual(rules.Free.Stars(best), 2, "A casual player at d3, best seed: " + best);
        }

        [Test]
        public void EachChallenge_3StarsForAStrongPlayer_1StarForACasualOne()
        {
            var rules = NutDropRules.Create();
            for (int c = 0; c < ActivityCatalog.ChallengesPerActivity; c++)
            {
                var stars = rules.Thresholds(c);
                int strong = Worst(rules, 0, c, CatcherSkill.Strong());
                int casual = Worst(rules, 0, c, CatcherSkill.Casual());
                TestContext.WriteLine($"challenge {c + 1}: strong {strong}, casual {casual} ({stars.One}/{stars.Two}/{stars.Three})");
                Assert.AreEqual(3, stars.Stars(strong), "Challenge " + (c + 1) + ", a strong player: " + strong);
                Assert.GreaterOrEqual(stars.Stars(casual), 1, "Challenge " + (c + 1) + ", a casual player: " + casual);
                Assert.Less(stars.Stars(casual), 3, "Challenge " + (c + 1) + " still asks something of a casual player: " + casual);
            }
        }

        [Test]
        public void TheSlowerMode_GivesTime_AsManyItems()
        {
            var rules = NutDropRules.Create();
            var game = new NutDropController();
            int normalScore = 0, slowScore = 0, normalCaught = 0, slowCaught = 0;
            for (int s = 0; s < Seeds; s++)
            {
                var normal = Setup(rules, 5, -1, SeedOf(s));
                var slow = Setup(rules, 5, -1, SeedOf(s), slower: true);
                PlayAlone(game, in normal, CatcherSkill.Casual());
                int items = game.Spawned;
                normalScore += game.Score;
                normalCaught += game.Caught;
                PlayAlone(game, in slow, CatcherSkill.Casual());
                slowScore += game.Score;
                slowCaught += game.Caught;
                Assert.AreEqual(75f, game.Seconds, 1e-3f, "× 1.25 the time.");
                Assert.AreEqual(items, game.Spawned, 4, "About as many items over the longer game (× 0.8 the rate).");
            }
            // The specials make single games swing; over six games the slower mode is never harder.
            Assert.GreaterOrEqual(slowCaught, normalCaught, "A casual player catches at least as many nuts.");
            Assert.GreaterOrEqual(slowScore, normalScore * 0.95, "And scores about as much.");
        }
    }
}
