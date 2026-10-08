using System;
using NUnit.Framework;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// How a scripted player grooms (T38 balance checks): seconds per brush pass, per move between patches and per tap, the
    /// share of debris it takes, how often it slips onto clean fur, whether it takes the bubbles and the golden brush, and
    /// how long it needs to pick a bow.
    /// </summary>
    internal sealed class GroomerSkill
    {
        public string Name;
        public float PassSeconds;
        public float MoveSeconds;
        public float TapSeconds;
        public float DebrisShare = 1f;

        /// <summary>A slip onto clean fur after this many zones cleaned in a row (0: never).</summary>
        public int SlipEvery;

        public bool DebrisFirst;
        public bool TakesBubbles = true;
        public bool TakesGoldenBrush = true;
        public float BowSeconds;

        /// <summary>
        /// A strong player: a quick stroke crosses about 7 zones a second, a tap takes 0.4 s, no slip, the debris first,
        /// every helper taken, the bow picked in 0.8 s.
        /// </summary>
        public static GroomerSkill Strong() => new GroomerSkill
        {
            Name = "strong", PassSeconds = 0.14f, MoveSeconds = 0.3f, TapSeconds = 0.4f, DebrisFirst = true, BowSeconds = 0.8f,
        };

        /// <summary>A good player: a bit slower, slips once every 12 zones.</summary>
        public static GroomerSkill Good() => new GroomerSkill
        {
            Name = "good", PassSeconds = 0.2f, MoveSeconds = 0.4f, TapSeconds = 0.5f, SlipEvery = 12, DebrisFirst = true, BowSeconds = 1.2f,
        };

        /// <summary>
        /// A casual player: a pass every 0.4 s, 0.8 s to move between patches, 0.9 s a tap, two thirds of the debris, a slip
        /// every 5 zones, the bubbles but not the golden brush, 2 s to pick a bow.
        /// </summary>
        public static GroomerSkill Casual() => new GroomerSkill
        {
            Name = "casual", PassSeconds = 0.4f, MoveSeconds = 0.8f, TapSeconds = 0.9f, DebrisShare = 0.67f, SlipEvery = 5,
            TakesGoldenBrush = false, BowSeconds = 2f,
        };
    }

    /// <summary>
    /// Plays a whole grooming game like a person would, through a tick callback (the runner, or the controller alone): it
    /// acts only when its last gesture is over, takes the bow when the bows come, and stops when the game ends.
    /// </summary>
    internal static class ScriptedGroomer
    {
        public const float Step = 1f / 30f;

        public static void Play(GroomingController game, GroomerSkill skill, Action<float> tick, Func<bool> running)
        {
            double busy = 0.0;
            double bowSince = -1.0;
            int last = -1;
            int sinceSlip = 0;
            int cleanedSinceDebris = 0;
            int guard = 0;
            while (running() && guard++ < 100000)
            {
                tick(Step);
                if (!running() || game.Finished)
                    break;
                double now = game.Elapsed;
                if (now < busy)
                    continue;
                if (game.Phase == GroomingPhase.Bow)
                {
                    if (bowSince < 0.0)
                        bowSince = now;
                    // The bows wait: the leaves still on the coat first, then the bow.
                    int leftover = NextDebris(game, skill);
                    if (leftover >= 0 && skill.DebrisFirst)
                    {
                        game.TapDebris(leftover);
                        busy = now + skill.TapSeconds;
                        continue;
                    }
                    if (now - bowSince >= skill.BowSeconds)
                        game.PlaceBow(GroomingBow.SkyBlue);
                    continue;
                }
                if (skill.TakesGoldenBrush && game.GoldenBrushZone >= 0)
                {
                    game.TakeGoldenBrush();
                    busy = now + skill.TapSeconds;
                    continue;
                }
                if (skill.TakesBubbles)
                {
                    int bubble = game.BubbleActive(0) ? 0 : game.BubbleActive(1) ? 1 : -1;
                    if (bubble >= 0)
                    {
                        game.TapBubble(bubble);
                        busy = now + skill.TapSeconds;
                        continue;
                    }
                }
                int debris = NextDebris(game, skill);
                bool brushLeft = game.DirtyLeft > 0;
                if (debris >= 0 && (skill.DebrisFirst || !brushLeft || cleanedSinceDebris >= 3))
                {
                    game.TapDebris(debris);
                    cleanedSinceDebris = 0;
                    busy = now + skill.TapSeconds;
                    continue;
                }
                if (!brushLeft)
                    continue;
                if (skill.SlipEvery > 0 && sinceSlip >= skill.SlipEvery)
                {
                    int clean = CleanZoneNear(game, last);
                    if (clean >= 0)
                        game.Brush(clean);
                    sinceSlip = 0;
                    busy = now + skill.PassSeconds;
                    continue;
                }
                int target = last >= 0 && game.IsDirty(last) ? last : NearestDirty(game, last);
                double cost = skill.PassSeconds;
                if (last >= 0 && target != last && !Neighbor(game, last, target))
                    cost += skill.MoveSeconds;
                int before = game.ZonesCleaned;
                game.Brush(target);
                if (game.ZonesCleaned > before)
                {
                    sinceSlip += game.ZonesCleaned - before;
                    cleanedSinceDebris += game.ZonesCleaned - before;
                }
                last = target;
                busy = now + cost;
            }
        }

        /// <summary>A game played on the controller alone with this setup (no runner): its final score.</summary>
        public static int PlayAlone(GroomingController game, in ActivitySetup setup, GroomerSkill skill)
        {
            game.Begin(in setup);
            Play(game, skill, dt => game.Tick(dt), () => !game.Finished);
            return game.Score;
        }

        /// <summary>The next debris this player takes (spread by its share), −1 when none is left for it.</summary>
        public static int NextDebris(GroomingController game, GroomerSkill skill)
        {
            for (int i = 0; i < game.DebrisCount; i++)
            {
                if (!game.DebrisActive(i))
                    continue;
                if (skill.DebrisShare < 1f && (int)((i + 1) * skill.DebrisShare) == (int)(i * skill.DebrisShare))
                    continue;
                return i;
            }
            return -1;
        }

        public static int NearestDirty(GroomingController game, int from)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < game.ZoneCount; i++)
            {
                if (!game.IsDirty(i))
                    continue;
                float distance = from < 0 ? i : Distance2(game, from, i);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>A clean zone near the last one, never one cleaned within the last half second (a real slip).</summary>
        public static int CleanZoneNear(GroomingController game, int from)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < game.ZoneCount; i++)
            {
                if (game.IsDirty(i) || game.WasDirty(i))
                    continue;
                float distance = from < 0 ? i : Distance2(game, from, i);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Among the 6 zones nearest to <paramref name="from"/>: one stroke reaches it without lifting the finger.</summary>
        public static bool Neighbor(GroomingController game, int from, int to)
        {
            float d = Distance2(game, from, to);
            int closer = 0;
            for (int i = 0; i < game.ZoneCount; i++)
                if (i != from && Distance2(game, from, i) < d)
                    closer++;
            return closer < 6;
        }

        public static float Distance2(GroomingController game, int a, int b)
        {
            float dx = game.ZoneX(a) - game.ZoneX(b), dy = game.ZoneY(a) - game.ZoneY(b), dz = game.ZoneZ(a) - game.ZoneZ(b);
            return dx * dx + dy * dy + dz * dz;
        }
    }

    /// <summary>
    /// T38 balance (doc 01 Contenu, S-ACT-001; doc Mini-jeux, section 6 « 10 défis équilibrés »): scripted players on the
    /// free mode and the 10 challenges, over several seeds and body sizes.
    /// </summary>
    public sealed class GroomingBalanceTests
    {
        internal static ActivitySetup Setup(ActivityRules rules, int difficulty, int challenge = -1, uint seed = 1, bool slower = false)
        {
            float seconds = rules.ChallengeSeconds(challenge, GroomingRules.BaseSeconds);
            return new ActivitySetup
            {
                Activity = ActivityId.Grooming,
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

        /// <summary>The lowest score over a few seeds (the layout changes with each game).</summary>
        internal static int Worst(ActivityRules rules, int difficulty, int challenge, GroomerSkill skill, int zones = 40, int seeds = 6)
        {
            var game = new GroomingController();
            game.SetSyntheticBody(zones);
            int worst = int.MaxValue;
            for (uint seed = 1; seed <= seeds; seed++)
            {
                var setup = Setup(rules, difficulty, challenge, seed * 7919u);
                worst = Math.Min(worst, ScriptedGroomer.PlayAlone(game, in setup, skill));
            }
            return worst;
        }

        internal static int Best(ActivityRules rules, int difficulty, int challenge, GroomerSkill skill, int zones = 40, int seeds = 6)
        {
            var game = new GroomingController();
            game.SetSyntheticBody(zones);
            int best = 0;
            for (uint seed = 1; seed <= seeds; seed++)
            {
                var setup = Setup(rules, difficulty, challenge, seed * 7919u);
                best = Math.Max(best, ScriptedGroomer.PlayAlone(game, in setup, skill));
            }
            return best;
        }

        [Test]
        public void AGoodPlayer_Reaches3StarsAtD3_ACasualOne_1To2Stars()
        {
            var rules = GroomingRules.Create();
            int good = Worst(rules, 3, -1, GroomerSkill.Good());
            int casualWorst = Worst(rules, 3, -1, GroomerSkill.Casual());
            int casualBest = Best(rules, 3, -1, GroomerSkill.Casual());
            TestContext.WriteLine($"d3: good {good}, casual {casualWorst} to {casualBest}");
            Assert.AreEqual(3, rules.Free.Stars(good), "A good player: 3 stars at d3 (520), every seed: " + good);
            Assert.GreaterOrEqual(rules.Free.Stars(casualWorst), 1, "A casual player: at least 1 star (200): " + casualWorst);
            Assert.LessOrEqual(rules.Free.Stars(casualBest), 2, "A casual player: not 3 stars (520): " + casualBest);
        }

        [Test]
        public void TheFreeMode_StaysWithinReach_FromD1ToD10()
        {
            var rules = GroomingRules.Create();
            for (int d = 1; d <= 10; d++)
            {
                int strong = Worst(rules, d, -1, GroomerSkill.Strong());
                int casual = Worst(rules, d, -1, GroomerSkill.Casual());
                TestContext.WriteLine($"d{d}: strong {strong}, casual {casual}");
                Assert.AreEqual(3, rules.Free.Stars(strong), "d" + d + ": a strong player reaches 3 stars.");
                Assert.GreaterOrEqual(rules.Free.Stars(casual), 1, "d" + d + ": a casual player gets a star.");
            }
        }

        [Test]
        public void EachChallenge_3StarsForAStrongPlayer_1StarForACasualOne()
        {
            var rules = GroomingRules.Create();
            for (int c = 0; c < ActivityCatalog.ChallengesPerActivity; c++)
            {
                // Challenge 7 is played with a heavy animal (56 zones); the others with a small one (40).
                int zones = c == GroomingRules.HeavyAnimalChallenge ? 56 : 40;
                var stars = rules.Challenges[c].Stars;
                int strong = Worst(rules, 0, c, GroomerSkill.Strong(), zones);
                int casual = Worst(rules, 0, c, GroomerSkill.Casual(), zones);
                TestContext.WriteLine($"challenge {c + 1}: strong {strong}, casual {casual} (thresholds {stars.One}/{stars.Two}/{stars.Three})");
                Assert.AreEqual(3, stars.Stars(strong), "Challenge " + (c + 1) + ": 3 stars within reach of a strong player: " + strong);
                Assert.GreaterOrEqual(stars.Stars(casual), 1, "Challenge " + (c + 1) + ": a casual player gets a star: " + casual);
            }
        }

        [Test]
        public void TheSmallestBodies_StillGive3Stars_ToAStrongPlayer()
        {
            // A Hopper has 30 zones: Z is clamped (32 at d10, 36 in challenge 10), the stars stay within reach.
            var rules = GroomingRules.Create();
            int free = Worst(rules, 10, -1, GroomerSkill.Strong(), 30);
            int last = Worst(rules, 0, 9, GroomerSkill.Strong(), 30);
            TestContext.WriteLine($"30 zones: d10 strong {free}, challenge 10 strong {last}");
            Assert.AreEqual(3, rules.Free.Stars(free));
            Assert.AreEqual(3, rules.Challenges[9].Stars.Stars(last));
        }
    }
}
