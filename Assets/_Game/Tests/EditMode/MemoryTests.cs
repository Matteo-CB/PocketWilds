using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T41 « Memory des empreintes » (doc 01 Contenu, S-ACT-004): the deal per grid, the pawprint mode, the flip rules, the
    /// match and the 0.8 s turn-back, the mistake rule, the glow card, the double pair, the end bonus, the time per grid, the
    /// challenges (swaps, one biome, stricter mistakes), pause, the variants list, the event ring, no allocation.
    /// </summary>
    public sealed class MemoryTests
    {
        public const float Frame = 1f / 60f;

        private static readonly ActivityRules Rules = MemoryRules.Create();

        internal static MemoryController Begin(int d = 5, int challenge = -1, uint seed = 7919u, bool slower = false, MemoryController game = null,
            float seconds = -1f)
        {
            game ??= new MemoryController();
            var setup = MemoryBalanceTests.Setup(Rules, d, challenge, seed, slower);
            if (seconds >= 0f)
                setup.Seconds = seconds;
            game.Begin(in setup);
            return game;
        }

        internal static void Run(MemoryController game, float seconds)
        {
            int frames = (int)System.Math.Ceiling(seconds / Frame);
            for (int f = 0; f < frames; f++)
                game.Tick(Frame);
        }

        internal static void SkipPreview(MemoryController game)
        {
            while (game.Previewing)
                game.Tick(Frame);
        }

        /// <summary>A face-down unmatched card of another pair than these cards (and not one that goes with them).</summary>
        internal static int Other(MemoryController game, params int[] not)
        {
            for (int c = 0; c < game.CardCount; c++)
            {
                if (game.FaceUp(c) || game.Matched(c) || not.Contains(c))
                    continue;
                if (not.Any(n => game.PairOf(n) == game.PairOf(c) || game.GoTogether(n, c)))
                    continue;
                return c;
            }
            return -1;
        }

        /// <summary>Matches one pair (its first unmatched card and its partner).</summary>
        internal static void MatchOne(MemoryController game)
        {
            for (int c = 0; c < game.CardCount; c++)
            {
                if (game.Matched(c))
                    continue;
                game.Flip(c);
                game.Flip(game.PartnerOf(c));
                return;
            }
        }

        private static List<MemoryEvent> Events(MemoryController game)
        {
            var list = new List<MemoryEvent>();
            for (int i = 0; i < game.EventCount; i++)
                list.Add(game.Event(i));
            return list;
        }

        private static HashSet<uint> DefaultVariants(MemoryController game)
        {
            var set = new HashSet<uint>();
            for (int i = 0; i < game.VariantCount; i++)
                set.Add(game.VariantAt(i));
            return set;
        }

        // ---------------- the deal ----------------

        [TestCase(1, 4, 3)]
        [TestCase(3, 4, 3)]
        [TestCase(4, 4, 4)]
        [TestCase(7, 4, 4)]
        [TestCase(8, 5, 4)]
        [TestCase(10, 5, 4)]
        public void TheDeal_HasTheGridOfTheD_TwoCardsPerPair_DistinctVariants(int d, int columns, int rows)
        {
            foreach (uint seed in MemoryBalanceTests.Seeds)
            {
                var game = Begin(d, seed: seed);
                Assert.AreEqual(columns, game.Columns, "d" + d);
                Assert.AreEqual(rows, game.Rows);
                Assert.AreEqual(columns * rows, game.CardCount);
                Assert.AreEqual(columns * rows / 2, game.PairCount);
                Assert.AreEqual(MemoryRules.SmallGrid + (d >= 8 ? 2 : d >= 4 ? 1 : 0), game.Grid);
                var known = DefaultVariants(game);
                Assert.AreEqual(18, known.Count, "The 18 Prairie variants by default.");
                var variants = new HashSet<uint>();
                for (int p = 0; p < game.PairCount; p++)
                {
                    var cards = Enumerable.Range(0, game.CardCount).Where(c => game.PairOf(c) == p).ToArray();
                    Assert.AreEqual(2, cards.Length, "Pair " + p + " has two cards.");
                    Assert.AreEqual(game.VariantOf(cards[0]), game.VariantOf(cards[1]));
                    Assert.AreEqual(game.SpeciesOf(cards[0]), game.SpeciesOf(cards[1]));
                    Assert.AreNotEqual(0u, game.SpeciesOf(cards[0]));
                    Assert.IsTrue(game.GoTogether(cards[0], cards[1]));
                    Assert.AreEqual(cards[1], game.PartnerOf(cards[0]));
                    Assert.IsTrue(known.Contains(game.VariantOf(cards[0])));
                    Assert.IsTrue(variants.Add(game.VariantOf(cards[0])), "Each pair shows its own variant.");
                }
                for (int c = 0; c < game.CardCount; c++)
                {
                    Assert.IsFalse(game.FaceUp(c));
                    Assert.IsFalse(game.Matched(c));
                    Assert.IsFalse(game.Seen(c));
                }
                Assert.AreEqual(0, game.Score);
                Assert.IsFalse(game.Finished);
            }
        }

        [Test]
        public void TheDeal_IsDeterministicBySeed()
        {
            foreach (int d in new[] { 2, 6, 9 })
            {
                var layouts = new HashSet<string>();
                foreach (uint seed in MemoryBalanceTests.Seeds)
                {
                    string a = Layout(Begin(d, seed: seed));
                    string b = Layout(Begin(d, seed: seed, game: new MemoryController()));
                    Assert.AreEqual(a, b, "Same seed, same deal.");
                    layouts.Add(a);
                }
                Assert.Greater(layouts.Count, 4, "Seeds give different deals.");
            }
        }

        private static string Layout(MemoryController game)
        {
            var parts = new List<string>();
            for (int c = 0; c < game.CardCount; c++)
                parts.Add(game.PairOf(c) + ":" + game.VariantOf(c) + ":" + (int)game.FaceOf(c) + (game.IsDouble(c) ? "D" : "") + (c == game.GlowCard ? "G" : ""));
            return string.Join(",", parts);
        }

        [Test]
        public void TheTopLeftCard_NeverHasItsPairBesideOrUnderIt()
        {
            var game = new MemoryController();
            foreach (int d in new[] { 1, 5, 9 })
                for (uint seed = 1; seed <= 300; seed++)
                {
                    Begin(d, seed: seed * 2654435761u, game: game);
                    int partner = game.PartnerOf(0);
                    Assert.AreNotEqual(1, partner, "d" + d + " seed " + seed);
                    Assert.AreNotEqual(game.Columns, partner, "d" + d + " seed " + seed);
                }
        }

        [Test]
        public void ThePawprintMode_PairsAPawprintWithAPortrait_OfTheSameVariant()
        {
            foreach (var (d, challenge, pawprints) in new[] { (5, -1, false), (6, -1, true), (10, -1, true), (0, 3, true), (0, 9, true), (0, 7, false), (0, 8, false), (0, 2, false) })
            {
                var game = Begin(d, challenge);
                Assert.AreEqual(pawprints, game.Pawprints, "d" + d + " challenge " + challenge);
                for (int c = 0; c < game.CardCount; c++)
                {
                    int partner = game.PartnerOf(c);
                    if (pawprints)
                    {
                        Assert.AreNotEqual(game.FaceOf(c), game.FaceOf(partner), "A pawprint and a portrait.");
                        Assert.AreEqual(game.VariantOf(c), game.VariantOf(partner), "Shape = species, colour = coat: the same variant.");
                    }
                    else
                    {
                        Assert.AreEqual(MemoryFace.Portrait, game.FaceOf(c), "Two identical portraits.");
                    }
                }
                if (pawprints)
                {
                    Assert.AreEqual(game.PairCount, Enumerable.Range(0, game.CardCount).Count(c => game.FaceOf(c) == MemoryFace.Pawprint));
                    // Two pawprints never go together, nor a pawprint with another variant's portrait.
                    int paw = Enumerable.Range(0, game.CardCount).First(c => game.FaceOf(c) == MemoryFace.Pawprint);
                    int otherPaw = Enumerable.Range(0, game.CardCount).Last(c => game.FaceOf(c) == MemoryFace.Pawprint);
                    Assert.IsFalse(game.GoTogether(paw, otherPaw));
                }
            }
        }

        // ---------------- the preview ----------------

        [Test]
        public void ThePreview_LastsByD_IgnoresTouches_AndDoesNotCountAsSeen()
        {
            var game = Begin(1);
            Assert.AreEqual(2f, game.PreviewSeconds, "2 s at d1.");
            Assert.IsTrue(game.Previewing);
            var events = Events(game);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(MemoryEventKind.PreviewStarted, events[0].Kind);
            Assert.AreEqual(2f, events[0].Seconds);
            game.ClearEvents();
            game.Flip(0);
            Assert.AreEqual(0, game.Moves, "Touches wait for the preview.");
            Assert.IsFalse(game.FaceUp(0));
            Run(game, 1.9f);
            Assert.IsTrue(game.Previewing);
            Run(game, 0.15f);
            Assert.IsFalse(game.Previewing);
            Assert.IsTrue(Events(game).Any(e => e.Kind == MemoryEventKind.PreviewEnded));
            Assert.IsTrue(Enumerable.Range(0, game.CardCount).All(c => !game.Seen(c)), "The preview is not « seen ».");
            Assert.Greater(game.Elapsed, 2f, "The preview is part of the game's time.");

            Assert.AreEqual(2f, Begin(2).PreviewSeconds);
            Assert.AreEqual(1f, Begin(3).PreviewSeconds, "1 s at d3 and d4.");
            Assert.AreEqual(1f, Begin(4).PreviewSeconds);
            for (int d = 5; d <= 10; d++)
            {
                var none = Begin(d);
                Assert.AreEqual(0f, none.PreviewSeconds, "None from d5.");
                Assert.IsFalse(none.Previewing);
                Assert.AreEqual(0, none.EventCount, "No preview, no event.");
            }
            Assert.AreEqual(0f, Begin(0, MemoryRules.GoodMemoryChallenge).PreviewSeconds, "Challenge 2 « Bonne mémoire »: no preview.");
            Assert.AreEqual(2f, Begin(0, MemoryRules.SmallGridChallenge).PreviewSeconds);
        }

        // ---------------- flips ----------------

        [Test]
        public void AFlip_TurnsAFaceDownCardUp_AndIgnoresEverythingElse()
        {
            var idle = new MemoryController();
            idle.Flip(0);
            Assert.AreEqual(0, idle.Moves, "Before Begin: nothing.");
            Assert.IsFalse(idle.FaceUp(0));

            var game = Begin(5);
            game.Flip(-1);
            game.Flip(game.CardCount);
            Assert.AreEqual(0, game.Moves);
            game.Flip(3);
            Assert.IsTrue(game.FaceUp(3));
            Assert.IsTrue(game.Seen(3));
            Assert.AreEqual(1, game.Moves);
            Assert.AreEqual(3, game.FirstCard);
            Assert.IsTrue(Events(game).Any(e => e.Kind == MemoryEventKind.Flipped && e.Card == 3));
            game.Flip(3);
            Assert.AreEqual(1, game.Moves, "A face-up card ignores a touch.");

            game.Flip(game.PartnerOf(3));
            Assert.IsTrue(game.Matched(3));
            int moves = game.Moves;
            game.Flip(3);
            game.Flip(game.PartnerOf(3));
            Assert.AreEqual(moves, game.Moves, "A matched card ignores a touch.");

            game.Pause();
            int free = Other(game, 3);
            game.Flip(free);
            Assert.AreEqual(moves, game.Moves, "Paused: no touch.");
            game.Resume();
            game.Flip(free);
            Assert.AreEqual(moves + 1, game.Moves);
        }

        [Test]
        public void AMatch_Stays_AndScores100()
        {
            var game = Begin(3, seed: 104729u);
            SkipPreview(game);
            game.ClearEvents();
            int a = Enumerable.Range(0, game.CardCount).First(c => !game.IsDouble(c));
            int b = game.PartnerOf(a);
            game.Flip(a);
            game.Flip(b);
            var match = Events(game).Single(e => e.Kind == MemoryEventKind.Matched);
            Assert.AreEqual(a, match.Card);
            Assert.AreEqual(b, match.Other);
            Assert.AreEqual(MemoryRules.PairPoints, match.Points);
            Assert.IsTrue(game.Matched(a) && game.Matched(b));
            Assert.IsTrue(game.FaceUp(a) && game.FaceUp(b), "The pair stays visible.");
            Assert.AreEqual(1, game.PairsFound);
            Assert.AreEqual(100, game.Score);
            Assert.AreEqual(1, game.Tries);
            Assert.AreEqual(2, game.Moves);
            Assert.IsFalse(game.Busy);
            Assert.AreEqual(-1, game.FirstCard);
        }

        [Test]
        public void AMismatch_TurnsBackAfter08Seconds_BusyMeanwhile()
        {
            var game = Begin(5);
            int a = 0;
            int b = Other(game, a);
            int c = Other(game, a, b);
            game.Flip(a);
            game.Flip(b);
            var miss = Events(game).Single(e => e.Kind == MemoryEventKind.Mismatched);
            Assert.AreEqual(a, miss.Card);
            Assert.AreEqual(b, miss.Other);
            Assert.AreEqual(0.8f, miss.Seconds, 1e-6f);
            Assert.IsFalse(miss.Mistake, "A first try is never a mistake.");
            Assert.IsTrue(game.Busy);
            game.ClearEvents();
            game.Flip(c);
            Assert.IsFalse(game.FaceUp(c), "Busy: other touches wait.");
            Run(game, 0.75f);
            Assert.IsTrue(game.Busy);
            Assert.IsTrue(game.FaceUp(a) && game.FaceUp(b));
            Assert.AreEqual(0, game.EventCount);
            Run(game, 0.1f);
            Assert.IsFalse(game.Busy);
            Assert.IsFalse(game.FaceUp(a) || game.FaceUp(b), "Both turned back.");
            var back = Events(game).Single(e => e.Kind == MemoryEventKind.FlippedBack);
            Assert.AreEqual(a, back.Card);
            Assert.AreEqual(b, back.Other);
            game.Flip(c);
            Assert.IsTrue(game.FaceUp(c));
            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(1, game.Tries);

            // « Activités plus lentes »: the turn-back keeps its 0.8 s.
            var slow = Begin(5, slower: true);
            slow.Flip(a);
            slow.Flip(Other(slow, a));
            Run(slow, 0.85f);
            Assert.IsFalse(slow.Busy);
        }

        // ---------------- the mistake rule (doc 01) ----------------

        [Test]
        public void TheMistakeRule_OnlyAMissWhereBothCardsOfTheRightPairWereSeen()
        {
            var game = Begin(5);
            int a0 = 0;
            int a1 = game.PartnerOf(a0);
            int b0 = Other(game, a0);
            int b1 = game.PartnerOf(b0);
            int c0 = Other(game, a0, b0);

            // First tries: never mistakes.
            game.Flip(a0);
            game.Flip(b0);
            Assert.AreEqual(0, game.Mistakes, "a0 and b0, both new.");
            Run(game, 1f);
            // a0 seen, its pair a1 never seen: not a mistake.
            game.Flip(a0);
            game.Flip(c0);
            Assert.AreEqual(0, game.Mistakes, "a1 had never been seen.");
            Run(game, 1f);
            // b1 new (its pair b0 was seen): a new first card is never a mistake.
            game.Flip(b1);
            game.Flip(a0);
            Assert.AreEqual(0, game.Mistakes, "b1 itself was new.");
            Run(game, 1f);
            // Now b0 and b1 were both seen: missing b0's pair is a mistake.
            game.ClearEvents();
            game.Flip(b0);
            game.Flip(c0);
            Assert.AreEqual(1, game.Mistakes, "b0 and b1 both seen before: a mistake.");
            Assert.IsTrue(Events(game).Single(e => e.Kind == MemoryEventKind.Mismatched).Mistake);
            Run(game, 1f);
            // a1 new: its first try is not a mistake even though a0 was seen.
            game.Flip(a1);
            game.Flip(c0);
            Assert.AreEqual(1, game.Mistakes);
            Run(game, 1f);
            // Finding the pair is never a mistake.
            game.Flip(b1);
            game.Flip(b0);
            Assert.IsTrue(game.Matched(b0));
            Assert.AreEqual(1, game.Mistakes);
            // a0 and a1 both seen: missing it now counts.
            game.Flip(a1);
            game.Flip(c0);
            Assert.AreEqual(2, game.Mistakes);
        }

        [Test]
        public void ThePreviewIsNotSeen_SoAMissRightAfterItIsNoMistake()
        {
            var game = Begin(1);
            SkipPreview(game);
            int a0 = 0;
            int b0 = Other(game, a0);
            game.Flip(a0);
            game.Flip(b0);
            Assert.AreEqual(0, game.Mistakes, "Every card showed in the preview, but none was « seen ».");
        }

        // ---------------- the glow card and the double pair ----------------

        [Test]
        public void TheGlowCard_MakesTheCardOfItsPairGlowFor1Second()
        {
            var game = Begin(5);
            int glow = game.GlowCard;
            Assert.GreaterOrEqual(glow, 0, "One glow card per game.");
            int partner = game.PartnerOf(glow);
            game.ClearEvents();
            game.Flip(glow);
            var lit = Events(game).Single(e => e.Kind == MemoryEventKind.Glow);
            Assert.AreEqual(glow, lit.Card);
            Assert.AreEqual(partner, lit.Other);
            Assert.AreEqual(1f, lit.Seconds);
            game.Flip(partner);
            Assert.IsTrue(game.Matched(glow));

            // Turned second, onto its face-up pair: nothing to show.
            var again = Begin(5);
            glow = again.GlowCard;
            again.Flip(again.PartnerOf(glow));
            again.ClearEvents();
            again.Flip(glow);
            Assert.IsFalse(Events(again).Any(e => e.Kind == MemoryEventKind.Glow));
            Assert.IsTrue(again.Matched(glow));

            // Every game has one, never one of the double pair's.
            foreach (int d in new[] { 1, 4, 8 })
                foreach (uint seed in MemoryBalanceTests.Seeds)
                {
                    var g = Begin(d, seed: seed);
                    Assert.GreaterOrEqual(g.GlowCard, 0);
                    Assert.IsFalse(g.IsDouble(g.GlowCard));
                }
        }

        [Test]
        public void TheDoublePair_ComesFromD4_AndIsWorth200()
        {
            for (int d = 1; d <= 10; d++)
                foreach (uint seed in MemoryBalanceTests.Seeds)
                {
                    var g = Begin(d, seed: seed);
                    int doubles = Enumerable.Range(0, g.CardCount).Count(c => g.IsDouble(c));
                    Assert.AreEqual(d >= 4 ? 2 : 0, doubles, "d" + d + ": one pair (two cards) from d4.");
                }
            var game = Begin(6);
            int card = Enumerable.Range(0, game.CardCount).First(c => game.IsDouble(c));
            Assert.IsTrue(game.IsDouble(game.PartnerOf(card)));
            game.Flip(card);
            game.Flip(game.PartnerOf(card));
            Assert.AreEqual(MemoryRules.DoublePairPoints, game.Score);
            Assert.AreEqual(200, game.Event(game.EventCount - 1).Points);
        }

        // ---------------- the end ----------------

        [Test]
        public void EveryPairFound_GivesTheEndBonus_ThenEnds()
        {
            var game = Begin(5);
            Run(game, 10.5f);
            for (int i = 0; i < game.PairCount; i++)
            {
                if (i == game.PairCount - 1)
                    game.ClearEvents();
                MatchOne(game);
            }
            Assert.IsTrue(game.Finished);
            Assert.IsTrue(game.AllMatched);
            float left = 60f - game.Elapsed;
            int bonus = 50 * 8 + 3 * (int)left;
            Assert.AreEqual(bonus, game.EndBonus, "50 × 8 pairs, no mistake, 3 per whole second left (" + left + ").");
            Assert.AreEqual(7 * 100 + 200 + bonus, game.Score, "Seven pairs, the double pair, the bonus.");
            var events = Events(game);
            int all = events.FindIndex(e => e.Kind == MemoryEventKind.AllMatched);
            int finished = events.FindIndex(e => e.Kind == MemoryEventKind.Finished);
            Assert.GreaterOrEqual(all, 0);
            Assert.AreEqual(bonus, events[all].Points, "AllMatched carries the bonus…");
            Assert.Greater(finished, all, "… then Finished.");
            int score = game.Score;
            Run(game, 5f);
            Assert.AreEqual(score, game.Score, "Nothing moves after the end.");

            // Mistakes take 20 each from the bonus (never under 0).
            var careless = Begin(5, seed: 104729u);
            int a0 = 0, b0 = Other(careless, a0);
            careless.Flip(a0);
            careless.Flip(b0);
            Run(careless, 1f);
            careless.Flip(careless.PartnerOf(a0));
            careless.Flip(b0);
            Run(careless, 1f);
            careless.Flip(a0);
            careless.Flip(b0);
            Assert.AreEqual(1, careless.Mistakes);
            Run(careless, 1f);
            while (!careless.Finished)
                MatchOne(careless);
            Assert.AreEqual(50 * 8 - 20 + 3 * (int)careless.SecondsLeft, careless.EndBonus);
            Assert.AreEqual(0, MemoryRules.EndBonus(6, 100, 20, 0f), "At least 0 before the seconds.");
            Assert.AreEqual(30, MemoryRules.EndBonus(6, 100, 20, 10.9f), "3 per whole second left.");
            Assert.AreEqual(300 - 100 + 15, MemoryRules.EndBonus(6, 2, 50, 5f));
        }

        [TestCase(1, 45f)]
        [TestCase(3, 45f)]
        [TestCase(4, 60f)]
        [TestCase(7, 60f)]
        [TestCase(8, 90f)]
        [TestCase(10, 90f)]
        public void TheTime_FollowsTheGrid(int d, float seconds)
        {
            Assert.AreEqual(seconds, Begin(d).Seconds, "From the setup (the runner's length at d).");
            Assert.AreEqual(seconds, Begin(d, seconds: 0f).Seconds, "Without one, the grid's own.");
            Assert.AreEqual(seconds * 1.25f, Begin(d, slower: true).Seconds, 1e-4f, "« Activités plus lentes »: × 1.25.");
            Assert.AreEqual(seconds * 1.25f, Begin(d, slower: true, seconds: 0f).Seconds, 1e-4f);
            Assert.AreEqual(seconds, Rules.SecondsAt(d, 60f));
        }

        [Test]
        public void TheTimeUp_CountsThePairsFound_WithoutTheBonus()
        {
            var game = Begin(3);
            SkipPreview(game);
            MatchOne(game);
            MatchOne(game);
            game.ClearEvents();
            Run(game, 50f);
            Assert.IsTrue(game.Finished);
            Assert.IsFalse(game.AllMatched);
            Assert.AreEqual(0, game.EndBonus);
            Assert.AreEqual(200, game.Score, "The pairs found.");
            var events = Events(game);
            Assert.IsFalse(events.Any(e => e.Kind == MemoryEventKind.AllMatched));
            Assert.AreEqual(1, events.Count(e => e.Kind == MemoryEventKind.Finished));
            Assert.AreEqual(45f, game.Elapsed, 0.02f, "Ended at 45 s.");
            game.Flip(Other(game));
            Assert.AreEqual(4, game.Moves, "No touch after the end.");
        }

        [Test]
        public void PauseStopsTheClock_ThePreview_AndTheTurnBack()
        {
            var game = Begin(1);
            game.Pause();
            Run(game, 5f);
            Assert.AreEqual(0f, game.Elapsed);
            Assert.IsTrue(game.Previewing);
            game.Resume();
            SkipPreview(game);
            int a = 0, b = Other(game, a);
            game.Flip(a);
            game.Flip(b);
            game.Pause();
            Run(game, 2f);
            Assert.IsTrue(game.Busy, "The turn-back waits too.");
            game.Resume();
            Run(game, 0.9f);
            Assert.IsFalse(game.Busy);
        }

        // ---------------- challenges ----------------

        [Test]
        public void EveryChallenge_HasTheDocsGridConstraintsAndLength()
        {
            // grid, pawprints, preview, penalty, swap every, same biome, seconds, thresholds.
            var expected = new (int grid, bool paw, float preview, int penalty, int swap, bool biome, float seconds, int one, int two, int three)[]
            {
                (0, false, 2f, 20, 0, false, 45f, 500, 700, 850),
                (0, false, 0f, 20, 0, false, 45f, 450, 650, 800),
                (1, false, 0f, 20, 0, false, 60f, 700, 950, 1150),
                (1, true, 0f, 20, 0, false, 60f, 650, 900, 1100),
                (0, false, 1f, 50, 0, false, 45f, 500, 700, 850),
                (1, false, 0f, 20, 4, false, 60f, 650, 900, 1100),
                (1, false, 0f, 20, 0, false, 45f, 650, 900, 1100),
                (2, false, 0f, 20, 0, false, 90f, 900, 1250, 1500),
                (2, false, 0f, 20, 0, true, 90f, 850, 1200, 1450),
                (2, true, 0f, 20, 0, false, 75f, 900, 1250, 1500),
            };
            for (int c = 0; c < 10; c++)
            {
                var e = expected[c];
                var game = Begin(0, c);
                string name = "Challenge " + (c + 1);
                Assert.AreEqual(e.grid, game.Grid, name);
                Assert.AreEqual(e.paw, game.Pawprints, name);
                Assert.AreEqual(e.preview, game.PreviewSeconds, name);
                Assert.AreEqual(e.penalty, game.MistakePenalty, name);
                Assert.AreEqual(e.swap, game.SwapEvery, name);
                Assert.AreEqual(e.biome, game.SameBiome, name);
                Assert.AreEqual(e.seconds, game.Seconds, name);
                var t = Rules.Thresholds(c);
                Assert.AreEqual((e.one, e.two, e.three), (t.One, t.Two, t.Three), name);
                Assert.AreEqual(MemoryRules.SecondsOf(MemoryRules.GridAt(Rules.Challenges[c].Level)), Rules.SecondsAt(Rules.Challenges[c].Level, 0f),
                    name + ": its d's grid is its own.");
                Assert.AreEqual(e.grid, MemoryRules.GridAt(Rules.Challenges[c].Level), name + ": its d's grid is its own.");
            }
        }

        [Test]
        public void Challenge5_TakesFiftyAMistake()
        {
            var game = Begin(0, MemoryRules.FlawlessChallenge);
            SkipPreview(game);
            int a0 = 0, b0 = Other(game, a0);
            game.Flip(a0);
            game.Flip(b0);
            Run(game, 1f);
            game.Flip(game.PartnerOf(a0));
            game.Flip(b0);
            Run(game, 1f);
            game.Flip(a0);
            game.Flip(b0);
            Assert.AreEqual(1, game.Mistakes);
            Run(game, 1f);
            while (!game.Finished)
                MatchOne(game);
            Assert.AreEqual(50 * 6 - 50 + 3 * (int)game.SecondsLeft, game.EndBonus, "« Chaque erreur retire 50 au lieu de 20 ».");
        }

        [Test]
        public void Challenge6_SwapsTwoFaceDownCards_Every4Pairs()
        {
            var game = Begin(0, MemoryRules.MovingCardsChallenge);
            // See two cards of different pairs first, so the swap takes seen ones.
            int a = 0, b = Other(game, a);
            game.Flip(a);
            game.Flip(b);
            Run(game, 1f);
            game.ClearEvents();
            for (int i = 0; i < 3; i++)
                MatchOneAvoiding(game, a, b);
            Assert.AreEqual(0, game.Swaps, "Not before the 4th pair.");
            Assert.IsFalse(Events(game).Any(e => e.Kind == MemoryEventKind.Swapped));
            var before = Snapshot(game);
            game.ClearEvents();
            MatchOneAvoiding(game, a, b);
            Assert.AreEqual(4, game.PairsFound);
            Assert.AreEqual(1, game.Swaps, "The 4th pair: two cards swap.");
            var swap = Events(game).Single(e => e.Kind == MemoryEventKind.Swapped);
            int x = swap.Card, y = swap.Other;
            Assert.IsFalse(game.FaceUp(x) || game.FaceUp(y) || game.Matched(x) || game.Matched(y), "Two face-down cards.");
            Assert.AreNotEqual(game.PairOf(x), game.PairOf(y));
            Assert.IsTrue(game.Seen(x) && game.Seen(y), "Seen cards first.");
            Assert.AreEqual(before[x], Describe(game, y), "Everything moved with the card.");
            Assert.AreEqual(before[y], Describe(game, x));
            while (!game.Finished)
                MatchOne(game);
            Assert.AreEqual(1, game.Swaps, "8 pairs: the 8th ends the game, no second swap.");
            Assert.IsTrue(game.AllMatched);

            Assert.AreEqual(0, Begin(5).SwapEvery, "Only challenge 6 moves cards.");
        }

        private static void MatchOneAvoiding(MemoryController game, int a, int b)
        {
            for (int c = 0; c < game.CardCount; c++)
            {
                if (game.Matched(c) || c == a || c == b || game.PartnerOf(c) == a || game.PartnerOf(c) == b)
                    continue;
                game.Flip(c);
                game.Flip(game.PartnerOf(c));
                return;
            }
        }

        private static string Describe(MemoryController game, int card) =>
            game.PairOf(card) + ":" + game.VariantOf(card) + ":" + game.SpeciesOf(card) + ":" + game.FaceOf(card) + ":" + game.Seen(card)
            + ":" + (card == game.GlowCard);

        private static string[] Snapshot(MemoryController game) =>
            Enumerable.Range(0, game.CardCount).Select(c => Describe(game, c)).ToArray();

        [Test]
        public void Challenge9_DealsOneBiome_TheOneThePlayerHasTheMostOf()
        {
            var meadow = ContentId.FromSource("biome.grassland").Value;
            var forest = ContentId.FromSource("biome.forest").Value;
            var variants = new uint[24];
            var species = new uint[24];
            var biomes = new uint[24];
            for (int i = 0; i < 24; i++)
            {
                variants[i] = 1000u + (uint)i;
                species[i] = 50u + (uint)(i % 3);
                // The player owns 8: 5 forest, 3 meadow; the codex knows 16 more, mostly meadow.
                biomes[i] = i < 8 ? (i < 5 ? forest : meadow) : (i < 20 ? meadow : forest);
            }
            var game = new MemoryController();
            game.SetVariants(24, variants, species);
            game.SetBiomes(24, biomes, owned: 8);
            Begin(0, MemoryRules.SameBiomeChallenge, game: game);
            Assert.AreEqual(forest, game.Biome, "The biome of most of the player's own animals.");
            var forestVariants = new HashSet<uint>(Enumerable.Range(0, 24).Where(i => biomes[i] == forest).Select(i => variants[i]));
            for (int c = 0; c < game.CardCount; c++)
                Assert.IsTrue(forestVariants.Contains(game.VariantOf(c)), "Every card from one biome.");
            // 9 forest variants for 10 pairs: the list repeats, and the game still plays to its end.
            PlayByLooks(game);
            Assert.IsTrue(game.AllMatched);

            // Without the player's count, the most common biome of the list.
            game.SetVariants(24, variants, species);
            game.SetBiomes(24, biomes);
            Begin(0, MemoryRules.SameBiomeChallenge, game: game);
            Assert.AreEqual(meadow, game.Biome);

            // Without biomes (today: the Prairie alone), every variant counts as one biome.
            game.SetVariants(24, variants, species);
            Begin(0, MemoryRules.SameBiomeChallenge, game: game);
            Assert.AreEqual(0u, game.Biome);
            Assert.AreEqual(10, Enumerable.Range(0, game.CardCount).Select(c => game.VariantOf(c)).Distinct().Count());

            // Another challenge ignores the biomes.
            game.SetVariants(24, variants, species);
            game.SetBiomes(24, biomes, owned: 8);
            Begin(0, MemoryRules.BigGridChallenge, game: game);
            Assert.AreEqual(0u, game.Biome);
        }

        /// <summary>Plays to the end matching cards by their looks (the pairs re-form around repeated variants).</summary>
        private static void PlayByLooks(MemoryController game)
        {
            SkipPreview(game);
            int guard = 0;
            while (!game.Finished && guard++ < 100)
            {
                int a = Enumerable.Range(0, game.CardCount).First(c => !game.Matched(c));
                int b = Enumerable.Range(0, game.CardCount).Last(c => !game.Matched(c) && c != a && game.GoTogether(a, c));
                game.Flip(a);
                game.Flip(b);
                Assert.IsTrue(game.Matched(a) && game.Matched(b), "Cards that look like a pair are a pair.");
            }
        }

        // ---------------- variants ----------------

        [Test]
        public void SetVariants_Copies_DropsZerosAndRepeats_KeepsAtMost64()
        {
            var variants = new uint[] { 11, 0, 12, 11, 13, 14, 15, 16, 17, 18 };
            var species = new uint[] { 1, 1, 2, 1, 3, 1, 2, 3, 1, 2 };
            var game = new MemoryController();
            game.SetVariants(variants.Length, variants, species);
            Assert.AreEqual(8, game.VariantCount, "A zero and a repeat dropped.");
            Assert.AreEqual(12u, game.VariantAt(1));
            Assert.AreEqual(2u, game.SpeciesAt(1));
            variants[0] = 99;
            Assert.AreEqual(11u, game.VariantAt(0), "Copied.");
            Begin(3, game: game);
            var dealt = Enumerable.Range(0, game.CardCount).Select(c => game.VariantOf(c)).Distinct().ToArray();
            Assert.AreEqual(6, dealt.Length);
            Assert.IsTrue(dealt.All(v => v >= 11 && v <= 18 && v != 99));

            var many = Enumerable.Range(1, 70).Select(i => (uint)i).ToArray();
            game.SetVariants(70, many, many);
            Assert.AreEqual(MemoryController.VariantCapacity, game.VariantCount);
            game.SetVariants(0, many, many);
            Assert.AreEqual(18, game.VariantCount, "Nothing given: the Prairie defaults.");
        }

        [Test]
        public void TheBestVariantsComeFirst_ASeededChoiceAmongThem()
        {
            var variants = Enumerable.Range(1, 40).Select(i => (uint)(500 + i)).ToArray();
            var species = Enumerable.Range(1, 40).Select(i => (uint)(i % 3 + 1)).ToArray();
            var game = new MemoryController();
            game.SetVariants(40, variants, species);
            var seen = new HashSet<uint>();
            foreach (uint seed in MemoryBalanceTests.Seeds)
            {
                Begin(3, seed: seed, game: game);
                for (int c = 0; c < game.CardCount; c++)
                {
                    Assert.LessOrEqual(game.VariantOf(c), 500u + 9u, "4 × 3: the first 9 variants (1.5 × 6 pairs).");
                    seen.Add(game.VariantOf(c));
                }
            }
            Assert.Greater(seen.Count, 6, "Not always the same animals.");
        }

        [Test]
        public void AShortList_Repeats_AndTheGameStillPlaysToItsEnd()
        {
            var game = new MemoryController();
            game.SetVariants(3, new uint[] { 21, 22, 23 }, new uint[] { 1, 2, 3 });
            foreach (int d in new[] { 3, 6 })
            {
                Begin(d, game: game);
                foreach (uint v in new uint[] { 21, 22, 23 })
                {
                    int cards = Enumerable.Range(0, game.CardCount).Count(c => game.VariantOf(c) == v);
                    if (d == 3)
                        Assert.AreEqual(4, cards, "6 pairs on 3 variants: 4 cards each.");
                    else
                        Assert.That(cards, NUnit.Framework.Is.EqualTo(4).Or.EqualTo(6), "8 pairs on 3 variants: 2 or 3 pairs each.");
                }
                Assert.IsTrue(Enumerable.Range(0, game.CardCount).All(c => !game.IsDouble(c)), "The double pair never on a repeated variant.");
                PlayByLooks(game);
                Assert.IsTrue(game.AllMatched, "d" + d);
            }
            game.SetVariants(3, new uint[] { 21, 22, 23 }, new uint[] { 1, 2, 3 });
            Begin(9, game: game);
            Assert.AreEqual(10, game.PairCount);
            PlayByLooks(game);
            Assert.IsTrue(game.AllMatched);
        }

        // ---------------- events and allocation ----------------

        [Test]
        public void TheEvents_LiveInAFixedRing()
        {
            var game = Begin(5);
            int a = 0, b = Other(game, a);
            for (int i = 0; i < 40; i++)
            {
                game.Flip(a);
                game.Flip(b);
                Run(game, 0.85f);
            }
            Assert.AreEqual(MemoryController.EventCapacity, game.EventCount, "Undrained: the ring keeps the last 64.");
            Assert.AreEqual(MemoryEventKind.FlippedBack, game.Event(game.EventCount - 1).Kind, "The newest last.");
            Assert.AreEqual(MemoryEventKind.None, game.Event(game.EventCount).Kind);
            game.ClearEvents();
            Assert.AreEqual(0, game.EventCount);
        }

        [Test]
        public void TheGame_DoesNotAllocate()
        {
            var game = new MemoryController();
            var variants = Enumerable.Range(1, 30).Select(i => (uint)(700 + i)).ToArray();
            var species = Enumerable.Range(1, 30).Select(i => (uint)(i % 3 + 1)).ToArray();
            var setup = MemoryBalanceTests.Setup(Rules, 6, -1, 7919u);
            var challenge = MemoryBalanceTests.Setup(Rules, 0, MemoryRules.MovingCardsChallenge, 7919u);
            game.SetVariants(30, variants, species);
            game.Begin(in setup);
            Assert.That(() => game.SetVariants(30, variants, species), Is.Not.AllocatingGCMemory(), "SetVariants copies into its own arrays.");
            Assert.That(() => game.Begin(in setup), Is.Not.AllocatingGCMemory(), "The deal.");
            Assert.That(() => game.Tick(Frame), Is.Not.AllocatingGCMemory(), "A frame.");
            int a = 0, b = Other(game, a);
            Assert.That(() =>
            {
                game.Flip(a);
                game.Flip(b);
                for (int i = 0; i < 60; i++)
                    game.Tick(Frame);
                for (int i = 0; i < game.EventCount; i++)
                    game.Event(i);
                game.ClearEvents();
            }, Is.Not.AllocatingGCMemory(), "A try, its turn-back and the events.");
            game.Begin(in challenge);
            Assert.That(() =>
            {
                for (int p = 0; p < 8; p++)
                    MatchOneNoLinq(game);
            }, Is.Not.AllocatingGCMemory(), "Matches, a swap, the end bonus.");
            Assert.IsTrue(game.AllMatched);
        }

        private static void MatchOneNoLinq(MemoryController game)
        {
            for (int c = 0; c < game.CardCount; c++)
            {
                if (game.Matched(c))
                    continue;
                game.Flip(c);
                game.Flip(game.PartnerOf(c));
                return;
            }
        }
    }
}
