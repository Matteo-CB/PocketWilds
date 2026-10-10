using System;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>How a scripted memory player plays (T41 balance checks): how long a flip takes and how often it forgets a card.</summary>
    internal sealed class MemorySkill
    {
        public string Name;

        /// <summary>Seconds between two flips (and after the preview, before the first one).</summary>
        public float FlipSeconds;

        /// <summary>Each time a card is seen face up, the chance the player will not remember it.</summary>
        public double ForgetChance;

        /// <summary>A strong player: perfect memory, a flip every 0.6 s.</summary>
        public static MemorySkill Strong() => new MemorySkill { Name = "strong", FlipSeconds = 0.6f, ForgetChance = 0.0 };

        /// <summary>A casual player: forgets a seen card with 35 % probability, a flip every 1.1 s.</summary>
        public static MemorySkill Casual() => new MemorySkill { Name = "casual", FlipSeconds = 1.1f, ForgetChance = 0.35 };
    }

    /// <summary>
    /// Plays a whole memory game like a person would, through a tick callback (the runner, or the controller alone): it
    /// remembers each card it turns (unless it forgets it), never the preview's faces (a conservative player), follows the
    /// cards that swap (challenge 6); with a remembered pair it turns it, else it turns a card it does not know, then the
    /// remembered card that goes with it, else another unknown one. It flips once every <see cref="MemorySkill.FlipSeconds"/>
    /// whenever the game takes a touch (not previewing, not busy).
    /// </summary>
    internal static class ScriptedMemoryPlayer
    {
        public const float Step = 1f / 60f;

        public static void Play(MemoryController game, MemorySkill skill, uint seed, Action<float> tick, Func<bool> running)
        {
            var known = new bool[MemoryRules.MaxCards];
            var rng = new DeterministicRandom(SeedHash.Mix(seed, 0x4D504C59u));
            double next = skill.FlipSeconds;
            int guard = 0;
            while (running() && !game.Finished && guard++ < 200000)
            {
                Drain(game, skill, known, ref rng, ref next);
                if (!game.Previewing && !game.Busy && game.Elapsed >= next)
                {
                    int card = Choose(game, known, ref rng);
                    if (card >= 0)
                    {
                        game.Flip(card);
                        next = game.Elapsed + skill.FlipSeconds;
                        Drain(game, skill, known, ref rng, ref next);
                    }
                }
                tick(Step);
            }
            // The last pair ends the game on a touch: one more tick lets the runner see it and finish.
            if (running())
                tick(Step);
            Drain(game, skill, known, ref rng, ref next);
        }

        private static void Drain(MemoryController game, MemorySkill skill, bool[] known, ref DeterministicRandom rng, ref double next)
        {
            for (int e = 0; e < game.EventCount; e++)
            {
                var ev = game.Event(e);
                switch (ev.Kind)
                {
                    case MemoryEventKind.Flipped:
                        known[ev.Card] = rng.NextDouble() >= skill.ForgetChance;
                        break;
                    case MemoryEventKind.Matched:
                        known[ev.Card] = false;
                        known[ev.Other] = false;
                        break;
                    case MemoryEventKind.Swapped:
                        (known[ev.Card], known[ev.Other]) = (known[ev.Other], known[ev.Card]);
                        break;
                    case MemoryEventKind.PreviewEnded:
                        next = game.Elapsed + skill.FlipSeconds;
                        break;
                }
            }
            game.ClearEvents();
        }

        private static bool Open(MemoryController game, int card) => !game.Matched(card) && !game.FaceUp(card);

        private static int Choose(MemoryController game, bool[] known, ref DeterministicRandom rng)
        {
            int first = game.FirstCard;
            int n = game.CardCount;
            if (first < 0)
            {
                // A remembered pair first.
                for (int i = 0; i < n; i++)
                {
                    if (!known[i] || !Open(game, i))
                        continue;
                    for (int j = i + 1; j < n; j++)
                        if (known[j] && Open(game, j) && game.GoTogether(i, j))
                            return i;
                }
                return Unknown(game, known, -1, ref rng);
            }
            for (int i = 0; i < n; i++)
                if (i != first && known[i] && Open(game, i) && game.GoTogether(first, i))
                    return i;
            return Unknown(game, known, first, ref rng);
        }

        /// <summary>A card the player does not remember (any face-down one when it remembers them all).</summary>
        private static int Unknown(MemoryController game, bool[] known, int exclude, ref DeterministicRandom rng)
        {
            int n = game.CardCount;
            int count = 0;
            for (int i = 0; i < n; i++)
                if (i != exclude && !known[i] && Open(game, i))
                    count++;
            bool any = count == 0;
            if (any)
                for (int i = 0; i < n; i++)
                    if (i != exclude && Open(game, i))
                        count++;
            if (count == 0)
                return -1;
            int pick = rng.Next(count);
            for (int i = 0; i < n; i++)
            {
                if (i == exclude || !Open(game, i) || (!any && known[i]))
                    continue;
                if (pick-- == 0)
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// T41 balance (doc Mini-jeux, section 6 « 10 défis équilibrés »): scripted players on six seeds. A strong player
    /// (perfect memory, a flip every 0.6 s) reaches 3 stars in the free mode at every d (every grid) and in every challenge;
    /// a casual one (forgets a seen card 35 % of the time, a flip every 1.1 s) gets 1 or 2 stars in the free mode at d3 and at
    /// least 1 in every challenge. The scripted players never use the preview.
    /// </summary>
    public sealed class MemoryBalanceTests
    {
        public static readonly uint[] Seeds = { 7919u, 104729u, 1299709u, 15485863u, 32452843u, 49979687u };

        public static ActivitySetup Setup(ActivityRules rules, int d, int challenge, uint seed, bool slower = false)
        {
            int level = challenge >= 0 ? rules.Challenges[challenge].Level : d;
            float seconds = rules.ChallengeSeconds(challenge, rules.SecondsAt(level, 60f));
            return new ActivitySetup
            {
                Activity = ActivityId.PawMemory,
                Mode = challenge >= 0 ? ActivityMode.Challenge : ActivityMode.Free,
                Challenge = challenge,
                Difficulty = level,
                Seed = seed,
                Seconds = seconds * (slower ? ActivityConfig.SlowerTimeMultiplier : 1f),
                Slower = slower,
                SpeedMultiplier = slower ? ActivityConfig.SlowerSpeedMultiplier : 1f,
                Rules = rules,
                Thresholds = rules.Thresholds(challenge, level),
            };
        }

        /// <summary>One game by a scripted player on the controller alone: its score.</summary>
        internal static int Score(ActivityRules rules, int d, int challenge, MemorySkill skill, uint seed, MemoryController game = null)
        {
            game ??= new MemoryController();
            var setup = Setup(rules, d, challenge, seed);
            game.Begin(in setup);
            ScriptedMemoryPlayer.Play(game, skill, seed, dt => game.Tick(dt), () => !game.Finished);
            return game.Score;
        }

        internal static int Worst(ActivityRules rules, int d, int challenge, MemorySkill skill)
        {
            int worst = int.MaxValue;
            foreach (uint seed in Seeds)
                worst = Math.Min(worst, Score(rules, d, challenge, skill, seed));
            return worst;
        }

        internal static int Best(ActivityRules rules, int d, int challenge, MemorySkill skill)
        {
            int best = int.MinValue;
            foreach (uint seed in Seeds)
                best = Math.Max(best, Score(rules, d, challenge, skill, seed));
            return best;
        }

        [Test]
        public void AStrongPlayer_Reaches3Stars_InTheFreeModeAtEveryGrid()
        {
            var rules = MemoryRules.Create();
            for (int d = AdaptiveDifficulty.Min; d <= AdaptiveDifficulty.Max; d++)
            {
                int worst = Worst(rules, d, -1, MemorySkill.Strong());
                var thresholds = rules.FreeAt(d);
                TestContext.WriteLine("d" + d + ": strong " + worst + " (" + thresholds.One + "/" + thresholds.Two + "/" + thresholds.Three + ")");
                Assert.AreEqual(3, thresholds.Stars(worst), "d" + d + " strong worst " + worst);
            }
        }

        [Test]
        public void ACasualPlayer_Gets1To2Stars_InTheFreeModeAtD3()
        {
            var rules = MemoryRules.Create();
            var thresholds = rules.FreeAt(3);
            int worst = Worst(rules, 3, -1, MemorySkill.Casual());
            int best = Best(rules, 3, -1, MemorySkill.Casual());
            TestContext.WriteLine("d3: casual " + worst + " to " + best + " (" + thresholds.One + "/" + thresholds.Two + "/" + thresholds.Three + ")");
            Assert.GreaterOrEqual(thresholds.Stars(worst), 1, "Casual worst " + worst);
            Assert.LessOrEqual(thresholds.Stars(best), 2, "Casual best " + best);
        }

        [Test]
        public void EveryChallenge_3StarsForAStrongPlayer_1ForACasualOne()
        {
            var rules = MemoryRules.Create();
            for (int c = 0; c < ActivityCatalog.ChallengesPerActivity; c++)
            {
                var thresholds = rules.Thresholds(c);
                int strong = Worst(rules, 0, c, MemorySkill.Strong());
                int casual = Worst(rules, 0, c, MemorySkill.Casual());
                TestContext.WriteLine("challenge " + (c + 1) + ": strong " + strong + ", casual " + casual + " (" + thresholds.One + "/"
                    + thresholds.Two + "/" + thresholds.Three + ")");
                Assert.AreEqual(3, thresholds.Stars(strong), "Challenge " + (c + 1) + " strong worst " + strong);
                Assert.GreaterOrEqual(thresholds.Stars(casual), 1, "Challenge " + (c + 1) + " casual worst " + casual);
            }
        }

        [Test]
        public void AStrongPlayer_FindsEveryPair_WithoutAMistake_WithinWish059sMoves()
        {
            var rules = MemoryRules.Create();
            var game = new MemoryController();
            foreach (int d in new[] { 1, 5, 10 })
                foreach (uint seed in Seeds)
                {
                    Score(rules, d, -1, MemorySkill.Strong(), seed, game);
                    Assert.IsTrue(game.AllMatched, "d" + d + " seed " + seed);
                    Assert.AreEqual(0, game.Mistakes, "Perfect memory never misses a known pair.");
                    Assert.LessOrEqual(game.Tries, game.MovesTarget, "d" + d + ": " + game.Tries + " tries");
                }
        }
    }
}
