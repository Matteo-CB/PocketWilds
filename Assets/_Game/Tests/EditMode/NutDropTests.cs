using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T40 « Pluie de noisettes » (doc 01 Contenu, S-ACT-003; doc Mini-jeux, section 6): the playfield and the basket, the
    /// parameters at d1 / d5 / d10, the 10 challenges, scoring and the multiplier, the pinecone, the misses, the catch
    /// geometry, the specials, deterministic and fair spawning, pause, the slower mode, the end, and a game that never
    /// allocates.
    /// </summary>
    public sealed class NutDropTests
    {
        internal const float Frame = 1f / 60f;

        internal static NutDropController Start(int difficulty = 3, int challenge = -1, uint seed = 11, bool slower = false,
            NutDropController game = null, ActivityRules rules = null)
        {
            game ??= new NutDropController();
            var setup = NutDropBalanceTests.Setup(rules ?? NutDropRules.Create(), difficulty, challenge, seed, slower);
            game.Begin(in setup);
            return game;
        }

        /// <summary>Rules whose free mode holds these values at every d (the other parameters the game's own).</summary>
        internal static ActivityRules Fixed(double rate = 1.0, double fall = 3.0, double pinecones = 0.0, double width = 1.0,
            double golden = 0.0, double berriesOnly = 0, double wind = 0.0)
        {
            var parameters = new[]
            {
                new DifficultyParameter("rate", rate, rate, rate, 3),
                new DifficultyParameter("fall_speed", fall, fall, fall, 3),
                new DifficultyParameter("pinecone_share", pinecones, pinecones, pinecones, 3),
                new DifficultyParameter("basket_width", width, width, width, 3),
                new DifficultyParameter("golden_share", golden, golden, golden, 3),
                new DifficultyParameter("berries_only", berriesOnly, berriesOnly, berriesOnly),
                new DifficultyParameter("wind", wind, wind, wind, 3),
                new DifficultyParameter("streak_goal", 0, 0, 0),
            };
            return new ActivityRules(ActivityId.NutRain, new StarThresholds(1, 2, 3), parameters, NutDropRules.Create().Challenges);
        }

        internal static List<NutDropEvent> Drain(NutDropController game)
        {
            var list = new List<NutDropEvent>();
            for (int i = 0; i < game.EventCount; i++)
                list.Add(game.Event(i));
            game.ClearEvents();
            return list;
        }

        /// <summary>The item in the air that reaches the rim first (−1: none).</summary>
        internal static int Next(NutDropController game, bool pinecones = true)
        {
            int next = -1;
            float best = float.MaxValue;
            for (int i = 0; i < game.ItemCapacity; i++)
            {
                if (!game.ItemActive(i) || game.ItemMissed(i))
                    continue;
                if (!pinecones && game.ItemKind(i) == NutDropItemKind.Pinecone)
                    continue;
                float t = game.ItemRimSeconds(i);
                if (t < best)
                {
                    best = t;
                    next = i;
                }
            }
            return next;
        }

        /// <summary>One frame with the basket under the next item (a magnet: catches whatever it can, pinecones too).</summary>
        internal static void CatchFrame(NutDropController game, bool pinecones = true)
        {
            int next = Next(game, pinecones);
            if (next >= 0)
                game.SetBasketTarget(game.ItemRimX(next));
            game.Tick(Frame);
        }

        /// <summary>One frame with the basket as far as it goes from the next item.</summary>
        internal static void DodgeFrame(NutDropController game)
        {
            int next = Next(game);
            if (next >= 0)
                game.SetBasketTarget(game.ItemRimX(next) > 0f ? -10f : 10f);
            game.Tick(Frame);
        }

        // ---------------- the basket ----------------

        [Test]
        public void TheBasket_RunsAt14_EasesNearItsTarget_AndStaysInTheField()
        {
            var game = Start(difficulty: 3);
            Assert.AreEqual(2.4f, game.HalfWidth);
            Assert.AreEqual(6f, game.SpawnHeight);
            Assert.AreEqual(-0.6f, game.GroundY);
            Assert.AreEqual(1.10f, game.BasketWidth, 1e-4f, "d3: 1.20 → 1.00, a straight line.");
            Assert.AreEqual(2.4f - 0.55f, game.BasketLimit, 1e-4f);
            Assert.AreEqual(0f, game.BasketX);

            game.SetBasketTarget(1.5f);
            game.Tick(Frame);
            Assert.AreEqual(14f / 60f, game.BasketX, 1e-4f, "Full speed while more than 1 unit away.");
            Assert.AreEqual(14f, game.BasketVelocity, 1e-3f);

            // Within the last unit it eases: speed = 14 × the distance left.
            game.SetBasketTarget(game.BasketX + 0.5f);
            float from = game.BasketX;
            game.Tick(Frame);
            Assert.AreEqual(14f * 0.5f / 60f, game.BasketX - from, 1e-4f);
            Assert.Less(game.BasketVelocity, 14f);

            // Far past the side: the center stops at the field's limit.
            game.SetBasketTarget(100f);
            for (int i = 0; i < 90; i++)
            {
                game.Tick(Frame);
                Assert.LessOrEqual(game.BasketX, game.BasketLimit + 1e-5f);
            }
            Assert.AreEqual(game.BasketLimit, game.BasketX, 1e-3f);
            Assert.AreEqual(0f, game.BasketVelocity, 0.05f, "At rest at the side.");
            game.SetBasketTarget(-100f);
            for (int i = 0; i < 90; i++)
                game.Tick(Frame);
            Assert.AreEqual(-game.BasketLimit, game.BasketX, 1e-3f);
            Assert.Less(game.BasketX, 0f);

            // Nonsense is ignored.
            game.SetBasketTarget(float.NaN);
            game.Tick(Frame);
            Assert.AreEqual(-game.BasketLimit, game.BasketX, 1e-3f);

            // Challenge 4: a 0.70 basket.
            Assert.AreEqual(0.70f, Start(challenge: NutDropRules.SmallBasketChallenge).BasketWidth, 1e-4f);
        }

        // ---------------- parameters and challenges ----------------

        [Test]
        public void TheParameters_AtD1_D5_D10_AreTheDocs()
        {
            var game = new NutDropController();
            Start(difficulty: 1, game: game);
            Assert.AreEqual(1.0f, game.SpawnRate, 1e-4f);
            Assert.AreEqual(2.5f, game.BaseFallSpeed, 1e-4f);
            Assert.AreEqual(0.10f, game.PineconeShare, 1e-4f);
            Assert.AreEqual(1.20f, game.BaseBasketWidth, 1e-4f);
            Assert.AreEqual(0.03f, game.GoldenShare, 1e-4f);
            Assert.AreEqual(60f, game.Seconds);
            Assert.AreEqual(0f, game.Wind);

            Start(difficulty: 5, game: game);
            Assert.AreEqual(1.6f, game.SpawnRate, 1e-4f);
            Assert.AreEqual(3.5f, game.BaseFallSpeed, 1e-4f);
            Assert.AreEqual(0.18f, game.PineconeShare, 1e-4f);
            Assert.AreEqual(1.00f, game.BaseBasketWidth, 1e-4f);
            Assert.AreEqual(0.04f, game.GoldenShare, 1e-4f);

            Start(difficulty: 10, game: game);
            Assert.AreEqual(2.4f, game.SpawnRate, 1e-4f);
            Assert.AreEqual(5.0f, game.BaseFallSpeed, 1e-4f);
            Assert.AreEqual(0.25f, game.PineconeShare, 1e-4f);
            Assert.AreEqual(0.85f, game.BaseBasketWidth, 1e-4f);
            Assert.AreEqual(0.05f, game.GoldenShare, 1e-4f);

            var rules = NutDropRules.Create();
            Assert.AreEqual(NutDropRules.ParameterCount, rules.ParameterCount);
            Assert.AreEqual(ActivityId.NutRain, rules.Activity);
            Assert.AreEqual(new StarThresholds(500, 700, 900).Three, rules.Free.Three);
            Assert.AreEqual(500, rules.Free.One);
            Assert.AreEqual(700, rules.Free.Two);
            Assert.AreEqual(1.3, rules.Value(NutDropRules.Rate, 3), 1e-9);
            Assert.AreEqual(3.0, rules.Value(NutDropRules.FallSpeed, 3), 1e-9);
        }

        [Test]
        public void TheTenChallenges_FixTheDocsValues()
        {
            var rules = NutDropRules.Create();
            var game = new NutDropController();
            int[] levels = { 1, 5, 5, 5, 5, 5, 5, 5, 5, 10 };
            for (int c = 0; c < 10; c++)
                Assert.AreEqual(levels[c], rules.Challenges[c].Level, "Challenge " + (c + 1));

            Start(challenge: NutDropRules.FirstChallenge, game: game);
            Assert.AreEqual(1.0f, game.SpawnRate, 1e-4f, "01: the d1 parameters.");
            Assert.AreEqual(1.20f, game.BaseBasketWidth, 1e-4f);

            Start(challenge: NutDropRules.NoPineconeChallenge, game: game);
            Assert.AreEqual(0f, game.PineconeShare, "02: no pinecone.");
            Assert.AreEqual(4.0f, game.BaseFallSpeed, 1e-4f, "02: falls at 4.0.");
            Assert.AreEqual(1.6f, game.SpawnRate, 1e-4f, "02: d5 otherwise.");

            Start(challenge: NutDropRules.PineconeChallenge, game: game);
            Assert.AreEqual(0.30f, game.PineconeShare, 1e-4f, "03: 30 % pinecones.");

            Start(challenge: NutDropRules.SmallBasketChallenge, game: game);
            Assert.AreEqual(0.70f, game.BaseBasketWidth, 1e-4f, "04: the basket 0.70.");

            Start(challenge: NutDropRules.DownpourChallenge, game: game);
            Assert.AreEqual(2.5f, game.SpawnRate, 1e-4f, "05: 2.5 objects a second.");

            Start(challenge: NutDropRules.StreakChallenge, game: game);
            Assert.AreEqual(0.18f, game.PineconeShare, 1e-4f, "06: d5.");
            Assert.AreEqual(10, rules.Challenges[NutDropRules.StreakChallenge].Stars.One, "06: 1 star at 10 (doc 20); the doc's 35 and 50 kept.");
            Assert.AreEqual(35, rules.Challenges[NutDropRules.StreakChallenge].Stars.Two);
            Assert.AreEqual(50, rules.Challenges[NutDropRules.StreakChallenge].Stars.Three);

            Start(challenge: NutDropRules.BerriesChallenge, game: game);
            Assert.IsTrue(game.BerriesOnly, "07: berries only.");
            for (int i = 0; i < game.SpecialCount; i++)
                Assert.AreNotEqual(NutDropItemKind.GoldenShower, game.SpecialKind(i), "07: no golden shower.");

            Start(challenge: NutDropRules.WindChallenge, game: game);
            float strongest = 0f;
            for (int i = 0; i < 60 * 9; i++)
            {
                game.Tick(Frame);
                strongest = Math.Max(strongest, Math.Abs(game.Wind));
            }
            Assert.AreEqual(0.6f, strongest, 0.02f, "08: a gentle sine wind, 0.6 at most.");

            Start(challenge: NutDropRules.GoldenChallenge, game: game);
            Assert.AreEqual(0.10f, game.GoldenShare, 1e-4f, "09: 10 % golden hazelnuts.");
            Assert.AreEqual(4.5f, game.BaseFallSpeed, 1e-4f, "09: falls at 4.5.");

            Start(challenge: NutDropRules.HarvestChallenge, game: game);
            Assert.AreEqual(90f, game.Seconds, "10: 90 s.");
            Assert.AreEqual(2.4f, game.SpawnRate, 1e-4f, "10: the d10 parameters.");
            Assert.AreEqual(0.85f, game.BaseBasketWidth, 1e-4f);

            // The doc's thresholds where they fit (challenges 2 and 4).
            Assert.AreEqual(1600, rules.Challenges[NutDropRules.NoPineconeChallenge].Stars.Three);
            Assert.AreEqual(1300, rules.Challenges[NutDropRules.SmallBasketChallenge].Stars.Three);
        }

        [Test]
        public void Challenge2_HasNoPinecone_Challenge3_AboutThirtyPercent()
        {
            int pinecones = 0, items = 0;
            for (uint seed = 1; seed <= 4; seed++)
            {
                var game = Start(challenge: NutDropRules.NoPineconeChallenge, seed: seed);
                while (!game.Finished)
                {
                    game.Tick(Frame);
                    foreach (var e in Drain(game))
                        if (e.Kind == NutDropEventKind.Spawned && e.ItemKind == NutDropItemKind.Pinecone)
                            pinecones++;
                }
            }
            Assert.AreEqual(0, pinecones, "Challenge 2: no pinecone ever falls.");

            for (uint seed = 1; seed <= 8; seed++)
            {
                var game = Start(challenge: NutDropRules.PineconeChallenge, seed: seed);
                while (!game.Finished)
                {
                    game.Tick(Frame);
                    foreach (var e in Drain(game))
                    {
                        if (e.Kind != NutDropEventKind.Spawned || NutDropRules.IsSpecial(e.ItemKind))
                            continue;
                        items++;
                        if (e.ItemKind == NutDropItemKind.Pinecone)
                            pinecones++;
                    }
                }
            }
            Assert.AreEqual(0.30, pinecones / (double)items, 0.05, "Challenge 3: about 30 % pinecones (" + pinecones + " of " + items + ").");
        }

        [Test]
        public void TheKinds_FollowTheShares()
        {
            var counts = new Dictionary<NutDropItemKind, int>();
            int total = 0;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var game = Start(difficulty: 10, seed: seed);
                while (!game.Finished)
                {
                    game.Tick(Frame);
                    foreach (var e in Drain(game))
                    {
                        if (e.Kind != NutDropEventKind.Spawned || NutDropRules.IsSpecial(e.ItemKind) || game.ActiveSpecial == NutDropItemKind.GoldenShower)
                            continue;
                        counts.TryGetValue(e.ItemKind, out int n);
                        counts[e.ItemKind] = n + 1;
                        total++;
                    }
                }
            }
            double Share(NutDropItemKind kind) => counts.TryGetValue(kind, out int n) ? n / (double)total : 0.0;
            Assert.AreEqual(0.25, Share(NutDropItemKind.Pinecone), 0.04, "d10: 25 % pinecones.");
            Assert.AreEqual(0.05, Share(NutDropItemKind.GoldenNut), 0.025, "d10: 5 % golden hazelnuts.");
            foreach (var kind in new[] { NutDropItemKind.Hazelnut, NutDropItemKind.Acorn, NutDropItemKind.Berry })
                Assert.AreEqual(0.70 / 3.0, Share(kind), 0.04, "The rest alike: " + kind);
        }

        // ---------------- scoring ----------------

        [Test]
        public void TheMultiplier_Steps0Point1Every5Catches_UpTo3()
        {
            Assert.AreEqual(10, NutDropRules.MultiplierTenths(0));
            Assert.AreEqual(10, NutDropRules.MultiplierTenths(4));
            Assert.AreEqual(11, NutDropRules.MultiplierTenths(5));
            Assert.AreEqual(11, NutDropRules.MultiplierTenths(9));
            Assert.AreEqual(12, NutDropRules.MultiplierTenths(10));
            Assert.AreEqual(29, NutDropRules.MultiplierTenths(99));
            Assert.AreEqual(30, NutDropRules.MultiplierTenths(100));
            Assert.AreEqual(30, NutDropRules.MultiplierTenths(1000));
            Assert.AreEqual(10, NutDropRules.Points(NutDropItemKind.Hazelnut));
            Assert.AreEqual(10, NutDropRules.Points(NutDropItemKind.Acorn));
            Assert.AreEqual(15, NutDropRules.Points(NutDropItemKind.Berry));
            Assert.AreEqual(50, NutDropRules.Points(NutDropItemKind.GoldenNut));
            Assert.AreEqual(0, NutDropRules.Points(NutDropItemKind.Pinecone));
            Assert.AreEqual(0, NutDropRules.Points(NutDropItemKind.WideBasket));
        }

        [Test]
        public void Scoring_ThePointsTimesTheMultiplier_StepsAndRises()
        {
            var game = Start(rules: Fixed(rate: 1.2, fall: 3.0, golden: 0.2), seed: 5);
            int score = 0, streak = 0, ups = 0, catches = 0;
            while (!game.Finished)
            {
                CatchFrame(game);
                foreach (var e in Drain(game))
                {
                    if (e.Kind == NutDropEventKind.Caught)
                    {
                        catches++;
                        streak++;
                        Assert.AreEqual(streak, e.Streak, "Each catch adds to the streak.");
                        int tenths = NutDropRules.MultiplierTenths(streak);
                        Assert.AreEqual(tenths, e.MultiplierTenths, "The multiplier at the catch counts it.");
                        int expected = (NutDropRules.Points(e.ItemKind) * tenths + 5) / 10;
                        Assert.AreEqual(expected, e.Points, e.ItemKind + " at streak " + streak);
                        score += e.Points;
                    }
                    else if (e.Kind == NutDropEventKind.MultiplierUp)
                    {
                        ups++;
                        Assert.AreEqual(0, e.Streak % 5, "+0.1 every 5 in a row.");
                        Assert.AreEqual(NutDropRules.MultiplierTenths(e.Streak), e.MultiplierTenths);
                    }
                }
            }
            Assert.Greater(catches, 50, "A magnet catches the whole rain.");
            Assert.AreEqual(score, game.Score);
            Assert.AreEqual(streak, game.Streak);
            Assert.AreEqual(streak, game.BestStreak);
            Assert.AreEqual(Math.Min(20, streak / 5), ups);
            Assert.AreEqual(NutDropRules.MultiplierTenths(streak), game.MultiplierTenths);
        }

        [Test]
        public void APinecone_ResetsTheStreak_AMiss_NeverBreaksIt()
        {
            var game = Start(rules: Fixed(rate: 1.5, fall: 3.0, pinecones: 0.35), seed: 9);
            int pinecones = 0, misses = 0, streak = 0;
            bool dodge = false;
            while (!game.Finished)
            {
                // Catch everything, but let an item go after every 4th catch in a row.
                if (dodge)
                    DodgeFrame(game);
                else
                    CatchFrame(game);
                foreach (var e in Drain(game))
                {
                    switch (e.Kind)
                    {
                        case NutDropEventKind.Caught:
                            streak++;
                            Assert.AreEqual(streak, e.Streak);
                            dodge = streak % 4 == 0;
                            break;
                        case NutDropEventKind.Pinecone:
                            pinecones++;
                            streak = 0;
                            Assert.AreEqual(0, e.Streak, "A pinecone: the streak goes back to 0.");
                            Assert.AreEqual(10, e.MultiplierTenths, "× 1.0 again.");
                            Assert.AreEqual(0, e.Points);
                            Assert.AreEqual(0, game.Streak);
                            break;
                        case NutDropEventKind.Missed:
                            misses++;
                            dodge = false;
                            Assert.AreEqual(streak, e.Streak, "A miss keeps the streak.");
                            Assert.AreEqual(streak, game.Streak);
                            break;
                    }
                }
            }
            Assert.Greater(pinecones, 5);
            Assert.Greater(misses, 3);
            Assert.AreEqual(pinecones, game.PineconesCaught);
            Assert.AreEqual(misses, game.Missed);
            Assert.GreaterOrEqual(game.BestStreak, 4);
        }

        [Test]
        public void Challenge7_OnlyTheBerriesScore_TheOthersKeepTheStreakGoing()
        {
            var game = Start(challenge: NutDropRules.BerriesChallenge, seed: 21);
            int berries = 0, others = 0, streak = 0;
            while (!game.Finished)
            {
                CatchFrame(game, pinecones: false);
                foreach (var e in Drain(game))
                {
                    if (e.Kind == NutDropEventKind.Pinecone)
                        streak = 0;
                    if (e.Kind != NutDropEventKind.Caught)
                        continue;
                    streak++;
                    Assert.AreEqual(streak, e.Streak, "Every nut adds to the streak.");
                    if (e.ItemKind == NutDropItemKind.Berry)
                    {
                        berries++;
                        Assert.AreEqual((15 * e.MultiplierTenths + 5) / 10, e.Points);
                    }
                    else
                    {
                        others++;
                        Assert.AreEqual(0, e.Points, e.ItemKind + " counts 0.");
                    }
                }
            }
            Assert.Greater(berries, 10);
            Assert.Greater(others, 20);
            Assert.AreEqual(game.Score, game.ChallengeScore, "The berries' score.");
            Assert.AreEqual(berries, game.BerriesCaught);
        }

        [Test]
        public void Challenge6_MeasuresTheLongestStreak()
        {
            var game = Start(challenge: NutDropRules.StreakChallenge, seed: 3);
            while (!game.Finished)
                CatchFrame(game);
            Assert.Greater(game.PineconesCaught, 0, "The magnet takes pinecones too.");
            Assert.AreEqual(game.BestStreak, game.ChallengeScore);
            Assert.AreNotEqual(game.Score, game.ChallengeScore);
            Assert.Greater(game.Score, 0);
        }

        // ---------------- the catch ----------------

        [Test]
        public void TheCatch_IsWithinHalfTheWidthPlusTheRadius_AsItCrossesTheRim()
        {
            var game = Start(rules: Fixed(rate: 0.5, fall: 2.5, width: 1.0), seed: 4);
            float reach = 0.5f + NutDropRules.ItemRadius;
            int caught = 0, missed = 0;
            bool inside = true;
            int aimed = -1;
            float edge = 0f;
            while (!game.Finished && (caught < 3 || missed < 3))
            {
                int next = Next(game);
                if (next >= 0 && next != aimed)
                {
                    // Alternately just inside and just outside the catch zone, on the field's open side.
                    aimed = next;
                    float x = game.ItemRimX(next);
                    float side = x > 0f ? -1f : 1f;
                    edge = x + side * (inside ? reach - 0.02f : reach + 0.02f);
                    inside = !inside;
                }
                game.SetBasketTarget(edge);
                game.Tick(Frame);
                foreach (var e in Drain(game))
                {
                    if (e.Kind == NutDropEventKind.Caught)
                    {
                        caught++;
                        Assert.LessOrEqual(Math.Abs(e.X - game.BasketX), reach + 1e-3f);
                        Assert.IsFalse(game.ItemActive(e.Item), "Caught: it leaves the field (into the basket).");
                    }
                    else if (e.Kind == NutDropEventKind.Missed)
                    {
                        missed++;
                        Assert.AreEqual(game.GroundY, game.ItemY(e.Item), 1e-5f, "Missed on the ground, never under it.");
                        Assert.Greater(Math.Abs(e.X - game.BasketX), reach - 1e-3f, "Beside the basket.");
                    }
                }
            }
            Assert.GreaterOrEqual(caught, 3, "Just inside: caught.");
            Assert.GreaterOrEqual(missed, 3, "Just outside: missed.");
        }

        [Test]
        public void AMissedItem_IsPushedAside_NeverThroughTheBasket()
        {
            var game = Start(rules: Fixed(rate: 0.4, fall: 1.0, width: 1.0), seed: 6);
            float reach = 0.5f + NutDropRules.ItemRadius;
            int pushed = 0;
            for (int f = 0; f < 60 * 50 && !game.Finished; f++)
            {
                int below = -1;
                for (int i = 0; i < game.ItemCapacity; i++)
                    if (game.ItemMissed(i))
                        below = i;
                int next = Next(game);
                if (below >= 0)
                {
                    // Run the basket right through the missed item.
                    float x = game.ItemX(below);
                    game.SetBasketTarget(x + (x > game.BasketX ? 3f : -3f));
                }
                else if (next >= 0)
                {
                    // Let it pass just beside.
                    float x = game.ItemRimX(next);
                    game.SetBasketTarget(x + (x > 0f ? -1f : 1f) * (reach + 0.1f));
                }
                game.Tick(Frame);
                for (int i = 0; i < game.ItemCapacity; i++)
                {
                    if (!game.ItemMissed(i))
                        continue;
                    float gap = Math.Abs(game.ItemX(i) - game.BasketX);
                    Assert.GreaterOrEqual(gap, reach - 1e-3f, "A missed item never enters the basket's column.");
                    if (gap < reach + 1e-3f)
                        pushed++;
                }
                Drain(game);
            }
            Assert.Greater(pushed, 0, "The basket pushed a missed item aside.");
        }

        // ---------------- specials ----------------

        [Test]
        public void TheSpecials_FallAtMostOnceEach_Between15And45s_9sApart_HalfTheGames()
        {
            var game = new NutDropController();
            var seen = new Dictionary<NutDropItemKind, int>();
            for (uint seed = 1; seed <= 400; seed++)
            {
                Start(seed: seed, game: game);
                Assert.LessOrEqual(game.SpecialCount, 3);
                for (int i = 0; i < game.SpecialCount; i++)
                {
                    var kind = game.SpecialKind(i);
                    Assert.IsTrue(NutDropRules.IsSpecial(kind));
                    for (int j = 0; j < i; j++)
                        Assert.AreNotEqual(game.SpecialKind(j), kind, "Each at most once.");
                    Assert.That(game.SpecialSecond(i), NUnit.Framework.Is.InRange(15f, 45f));
                    if (i > 0)
                        Assert.GreaterOrEqual(game.SpecialSecond(i) - game.SpecialSecond(i - 1), 9f - 1e-4f, "Their effects never overlap.");
                    seen.TryGetValue(kind, out int n);
                    seen[kind] = n + 1;
                }
            }
            foreach (var kind in new[] { NutDropItemKind.WideBasket, NutDropItemKind.Feather, NutDropItemKind.GoldenShower })
                Assert.AreEqual(0.5, seen[kind] / 400.0, 0.08, kind + ": a 50 % chance.");

            Start(seed: 7, slower: true, game: game);
            for (int i = 0; i < game.SpecialCount; i++)
                Assert.That(game.SpecialSecond(i), NUnit.Framework.Is.InRange(18.75f, 56.25f), "× 1.25 slower.");
        }

        internal static uint SeedWithAllSpecials(int difficulty = 3)
        {
            var game = new NutDropController();
            for (uint seed = 1; seed < 2000; seed++)
            {
                Start(difficulty: difficulty, seed: seed, game: game);
                if (game.SpecialCount == 3)
                    return seed;
            }
            Assert.Fail("No seed with the three specials.");
            return 0;
        }

        [Test]
        public void TheSpecials_WideBasket_Feather_GoldenShower()
        {
            uint seed = SeedWithAllSpecials();
            var game = Start(seed: seed);
            float width = game.BaseBasketWidth, fall = game.BaseFallSpeed;
            var started = new List<NutDropItemKind>();
            var ended = new List<NutDropItemKind>();
            var startedAt = new Dictionary<NutDropItemKind, float>();
            int showerSpawns = 0;
            bool shower = false;
            while (!game.Finished)
            {
                CatchFrame(game, pinecones: false);
                foreach (var e in Drain(game))
                {
                    if (e.Kind == NutDropEventKind.SpecialStarted)
                    {
                        started.Add(e.ItemKind);
                        Assert.AreEqual(NutDropRules.SpecialSeconds(e.ItemKind), e.Seconds, 1e-4f, "The event carries its seconds.");
                        Assert.AreEqual(e.ItemKind, game.ActiveSpecial);
                        startedAt[e.ItemKind] = game.Elapsed;
                        shower = e.ItemKind == NutDropItemKind.GoldenShower;
                    }
                    else if (e.Kind == NutDropEventKind.SpecialEnded)
                    {
                        ended.Add(e.ItemKind);
                        shower = false;
                        Assert.AreEqual(NutDropRules.SpecialSeconds(e.ItemKind), game.Elapsed - startedAt[e.ItemKind], 0.05f, e.ItemKind + " lasts its time.");
                        Assert.AreEqual(NutDropItemKind.None, game.ActiveSpecial);
                    }
                    else if (e.Kind == NutDropEventKind.Spawned && shower)
                    {
                        showerSpawns++;
                        Assert.AreEqual(NutDropItemKind.GoldenNut, e.ItemKind, "The golden shower: only golden hazelnuts.");
                    }
                }
                switch (game.ActiveSpecial)
                {
                    case NutDropItemKind.WideBasket:
                        Assert.AreEqual(width * 1.5f, game.BasketWidth, 1e-4f, "The wide basket: × 1.5.");
                        Assert.AreEqual(fall, game.FallSpeedNow, 1e-4f);
                        break;
                    case NutDropItemKind.Feather:
                        Assert.AreEqual(fall * 0.6f, game.FallSpeedNow, 1e-4f, "The feather: × 0.6.");
                        Assert.AreEqual(width, game.BasketWidth, 1e-4f);
                        break;
                    case NutDropItemKind.None:
                        Assert.AreEqual(width, game.BasketWidth, 1e-4f);
                        Assert.AreEqual(fall, game.FallSpeedNow, 1e-4f);
                        Assert.AreEqual(0f, game.SpecialLeft);
                        break;
                }
                Assert.LessOrEqual(Math.Abs(game.BasketX), game.BasketLimit + 1e-4f, "The basket stays inside as its width changes.");
            }
            CollectionAssert.AreEquivalent(new[] { NutDropItemKind.WideBasket, NutDropItemKind.Feather, NutDropItemKind.GoldenShower }, started);
            CollectionAssert.AreEquivalent(started, ended);
            Assert.GreaterOrEqual(showerSpawns, 4, "About 5 s of golden hazelnuts at d3.");
        }

        // ---------------- spawning ----------------

        private static List<(NutDropItemKind Kind, float X, float T)> Rain(uint seed, float frame, int difficulty = 5, int challenge = -1)
        {
            var game = Start(difficulty: difficulty, challenge: challenge, seed: seed);
            var list = new List<(NutDropItemKind, float, float)>();
            while (!game.Finished)
            {
                game.Tick(frame);
                foreach (var e in Drain(game))
                    if (e.Kind == NutDropEventKind.Spawned)
                        list.Add((e.ItemKind, e.X, e.Seconds));
            }
            return list;
        }

        [Test]
        public void Spawning_IsDeterministicBySeed_WhateverTheFrames()
        {
            var a = Rain(77, Frame);
            var b = Rain(77, Frame);
            var c = Rain(77, 1f / 30f);
            var other = Rain(78, Frame);
            Assert.Greater(a.Count, 80);
            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a.Count, c.Count, "Spawns keep their own clock: 30 or 60 frames a second give the same rain.");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].X, b[i].X);
                Assert.AreEqual(a[i].Kind, c[i].Kind, "Item " + i);
                Assert.AreEqual(a[i].X, c[i].X, 0.02f, "Item " + i);
            }
            Assert.IsFalse(a.Select(s => s.X).SequenceEqual(other.Select(s => s.X)), "Another seed, another rain.");
        }

        [Test]
        public void Spawning_IsFair_CatchableInARow_PineconesAvoidable_NeverOverlapping()
        {
            float r = NutDropRules.ItemRadius;
            int pairs = 0, unfair = 0, crowded = 0;
            foreach (int challenge in new[] { -1, NutDropRules.DownpourChallenge, NutDropRules.PineconeChallenge, NutDropRules.SmallBasketChallenge })
            {
                for (uint seed = 1; seed <= 6; seed++)
                {
                    var game = Start(difficulty: 10, challenge: challenge, seed: seed);
                    float fallSeconds = game.SpawnHeight / game.BaseFallSpeed;
                    bool hasGood = false;
                    float goodX = 0f, goodT = 0f;
                    while (!game.Finished)
                    {
                        game.Tick(Frame);
                        foreach (var e in Drain(game))
                        {
                            if (e.Kind != NutDropEventKind.Spawned)
                                continue;
                            if (e.ItemKind == NutDropItemKind.Pinecone)
                                continue;
                            if (hasGood && game.ActiveSpecial != NutDropItemKind.Feather)
                            {
                                pairs++;
                                float allowed = game.BaseBasketWidth + 2f * r + NutDropRules.FairReachSpeed
                                    * Math.Max(0f, e.Seconds - goodT - NutDropRules.FairReactionSeconds);
                                if (Math.Abs(e.X - goodX) > allowed + 0.1f)
                                    unfair++;
                            }
                            hasGood = true;
                            goodX = e.X;
                            goodT = e.Seconds;
                        }
                        // Nothing in the air ever touches.
                        for (int i = 0; i < game.ItemCapacity; i++)
                        {
                            if (!game.ItemActive(i) || game.ItemMissed(i))
                                continue;
                            for (int j = i + 1; j < game.ItemCapacity; j++)
                            {
                                if (!game.ItemActive(j) || game.ItemMissed(j))
                                    continue;
                                float dx = game.ItemX(i) - game.ItemX(j), dy = game.ItemY(i) - game.ItemY(j);
                                if (dx * dx + dy * dy < 4f * r * r - 1e-4f)
                                    crowded++;
                            }
                            Assert.LessOrEqual(Math.Abs(game.ItemX(i)), game.HalfWidth - r + 1e-4f, "Inside the field.");
                        }
                    }
                    Assert.Greater(fallSeconds, 0f);
                }
            }
            Assert.Greater(pairs, 1000);
            Assert.AreEqual(0, crowded, "No item ever enters another.");
            Assert.LessOrEqual(unfair, pairs / 200, "Two nuts in a row are catchable (" + unfair + " of " + pairs + ").");
        }

        [Test]
        public void Challenge8_TheWindCarriesEveryItemAlike_InsideTheField()
        {
            var game = Start(challenge: NutDropRules.WindChallenge, seed: 13);
            int drifting = 0;
            var x = new float[game.ItemCapacity];
            for (int f = 0; f < 60 * 40; f++)
            {
                for (int i = 0; i < game.ItemCapacity; i++)
                    x[i] = game.ItemX(i);
                game.Tick(Frame);
                Drain(game);
                float wind = game.Wind;
                for (int i = 0; i < game.ItemCapacity; i++)
                {
                    if (!game.ItemActive(i) || game.ItemMissed(i) || game.ItemY(i) >= game.SpawnHeight - 0.2f)
                        continue;
                    float dx = game.ItemX(i) - x[i];
                    Assert.LessOrEqual(Math.Abs(game.ItemX(i)), game.HalfWidth - game.ItemRadius + 1e-4f);
                    if (Math.Abs(game.ItemX(i)) < game.HalfWidth - game.ItemRadius - 1e-3f && Math.Abs(wind) > 0.1f)
                    {
                        Assert.AreEqual(wind * Frame, dx, 0.004f, "Drifts with the wind.");
                        drifting++;
                    }
                }
            }
            Assert.Greater(drifting, 200);
        }

        // ---------------- clocks ----------------

        [Test]
        public void PauseAndResume_StopTheClocks()
        {
            var game = Start(seed: 31);
            for (int i = 0; i < 120; i++)
                game.Tick(Frame);
            int slot = Next(game);
            Assert.GreaterOrEqual(slot, 0);
            float y = game.ItemY(slot), elapsed = game.Elapsed;
            game.SetBasketTarget(2f);
            game.Pause();
            for (int i = 0; i < 120; i++)
                game.Tick(Frame);
            Assert.AreEqual(elapsed, game.Elapsed, "Paused: no time passes.");
            Assert.AreEqual(y, game.ItemY(slot), "Nothing falls.");
            Assert.AreEqual(0f, game.BasketVelocity);
            game.Resume();
            game.Tick(Frame);
            Assert.Greater(game.Elapsed, elapsed);
            Assert.Less(game.ItemY(slot), y);
        }

        [Test]
        public void TheSlowerMode_Times125_TheSpeeds08_TheBasketKeepsItsOwn()
        {
            var game = Start(difficulty: 5, slower: true, seed: SeedWithAllSpecials(5));
            Assert.AreEqual(75f, game.Seconds, 1e-4f);
            Assert.AreEqual(1.6f * 0.8f, game.SpawnRate, 1e-4f);
            Assert.AreEqual(3.5f * 0.8f, game.BaseFallSpeed, 1e-4f);
            game.SetBasketTarget(2f);
            game.Tick(Frame);
            Assert.AreEqual(14f / 60f, game.BasketX, 1e-4f, "The basket is the player's: never slowed.");
            float wide = 0f;
            while (!game.Finished)
            {
                CatchFrame(game, pinecones: false);
                foreach (var e in Drain(game))
                    if (e.Kind == NutDropEventKind.SpecialStarted && e.ItemKind == NutDropItemKind.WideBasket)
                        wide = e.Seconds;
            }
            Assert.AreEqual(8f * 1.25f, wide, 1e-4f, "The specials last × 1.25.");
        }

        [Test]
        public void TheGame_EndsAtItsTime_TheRainStopsInTime()
        {
            var game = Start(difficulty: 5, seed: 41);
            float fallSeconds = game.SpawnHeight / game.BaseFallSpeed;
            float lastSpawn = 0f;
            int finished = 0;
            while (!game.Finished)
            {
                game.Tick(Frame);
                foreach (var e in Drain(game))
                {
                    if (e.Kind == NutDropEventKind.Spawned)
                        lastSpawn = e.Seconds;
                    if (e.Kind == NutDropEventKind.Finished)
                        finished++;
                }
            }
            Assert.AreEqual(1, finished);
            Assert.AreEqual(60f, game.Elapsed, 0.02f);
            Assert.LessOrEqual(lastSpawn + fallSeconds, 60f + 0.02f, "No item that could not reach the rim before the end.");
            Assert.Greater(lastSpawn, 60f - fallSeconds - 1.5f, "The rain goes on almost to the end.");
            game.Tick(1f);
            Assert.AreEqual(0, game.EventCount, "Nothing after the end.");

            // One long tick still steps every 1/60 s: nothing slips past the rim.
            var big = Start(difficulty: 5, seed: 41);
            big.Tick(30f);
            Assert.AreEqual(30f, big.Elapsed, 0.01f);
            Assert.LessOrEqual(big.EventCount, NutDropController.EventCapacity, "The ring keeps the last 128.");
        }

        [Test]
        public void TheRewardCounts_AreTheHazelnutsAndTheBerries()
        {
            var game = Start(difficulty: 1, seed: 8);
            while (!game.Finished)
                CatchFrame(game, pinecones: false);
            Assert.Greater(game.HazelnutsCaught, 5);
            Assert.Greater(game.BerriesCaught, 5);
            Assert.AreEqual(game.HazelnutsCaught, game.RewardCount(NutDropRules.HazelnutCount));
            Assert.AreEqual(game.BerriesCaught, game.RewardCount(NutDropRules.BerryCount));
            Assert.AreEqual(0, game.RewardCount(2));
            Assert.AreEqual(0, game.PineconesCaught);
            Assert.GreaterOrEqual(game.Caught, game.HazelnutsCaught + game.BerriesCaught);
        }

        // ---------------- no allocation ----------------

        [Test]
        public void TheGame_DoesNotAllocate()
        {
            var game = new NutDropController();
            var setup = NutDropBalanceTests.Setup(NutDropRules.Create(), 10, NutDropRules.WindChallenge, SeedWithAllSpecials(10));
            game.Begin(in setup);
            game.Tick(Frame);
            int sum = 0;
            TestDelegate begin = () => game.Begin(in setup);
            TestDelegate play = () =>
            {
                for (int f = 0; f < 600; f++)
                {
                    CatchFrame(game);
                    for (int i = 0; i < game.EventCount; i++)
                        sum += game.Event(i).Points;
                    game.ClearEvents();
                }
            };
            begin();
            play();
            begin();
            Assert.That(begin, Is.Not.AllocatingGCMemory(), "A new game.");
            Assert.That(play, Is.Not.AllocatingGCMemory(), "Ten seconds of play.");
            Assert.That(() => game.Tick(16f), Is.Not.AllocatingGCMemory(), "A long frame.");
            Assert.That(() => game.Pause(), Is.Not.AllocatingGCMemory());
            Assert.That(() => game.Resume(), Is.Not.AllocatingGCMemory());
            Assert.GreaterOrEqual(sum, 0);
        }
    }
}
