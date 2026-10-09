using System;
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
    /// T39 « Cache-cache » (doc 01 Contenu, S-ACT-002; doc Mini-jeux, S-ACT-002 and section 6): the rules as data (thresholds,
    /// parameters at every d, the 10 challenges), the deterministic spread layout, the scoring before and after the hints
    /// with the seconds left, the Timid delay, the staggered hints, empty touches and the 3-in-2-s pause, the decoys and their
    /// rustles, the magnifier and the whistle, the all-found bonus and the end, each challenge's own rule, pause, the event
    /// ring and a game that never allocates.
    /// </summary>
    public sealed class HideSeekTests
    {
        private const float Frame = 1f / 60f;

        internal static HideSeekController Start(int difficulty = 3, int challenge = -1, uint seed = 11, bool slower = false,
            HideSeekController game = null)
        {
            game ??= new HideSeekController();
            var setup = HideSeekBalanceTests.Setup(HideSeekRules.Create(), difficulty, challenge, seed, slower);
            game.Begin(in setup);
            return game;
        }

        internal static List<HideSeekEvent> Drain(HideSeekController game)
        {
            var list = new List<HideSeekEvent>();
            for (int i = 0; i < game.EventCount; i++)
                list.Add(game.Event(i));
            game.ClearEvents();
            return list;
        }

        internal static int Count(List<HideSeekEvent> events, HideSeekEventKind kind) => events.Count(e => e.Kind == kind);

        /// <summary>A spot with no animal, not a decoy, whose animal was not found: a plain empty spot.</summary>
        internal static int FreeSpot(HideSeekController game, int skip = -1)
        {
            for (int s = 0; s < game.SpotCount; s++)
                if (s != skip && game.AnimalAt(s) < 0 && !game.IsDecoy(s) && !game.FoundAt(s))
                    return s;
            return -1;
        }

        /// <summary>Ticks by frames until the game's clock reaches <paramref name="second"/> (or the game ends).</summary>
        internal static void TickTo(HideSeekController game, double second, float step = Frame)
        {
            for (int guard = 0; guard < 100000 && game.Elapsed < second - 1e-5 && !game.Finished; guard++)
                game.Tick((float)Math.Min(step, second - game.Elapsed + 1e-5));
        }

        // ---------------- rules ----------------

        [Test]
        public void TheRules_AreDoc01_WithTheT39Counts_ThresholdsAndParametersAtEveryD()
        {
            // T39 deviation (Docs/Design/T39_Cache_cache.md): the game plays in the refuge, in a framed zone of 5 to 8 good
            // spots: hidden 3 / 4 / 5 (doc 4 / 6 / 8), decoys 0 / 1 / 2 (doc 0 / 2 / 4); visibility and hint delays are the
            // doc's; the free mode's 3 stars at 750 (doc 900) for the fewer animals (balance tests).
            var rules = HideSeekRules.Create();
            Assert.AreEqual(ActivityId.HideAndSeek, rules.Activity);
            Assert.AreEqual(300, rules.Free.One);
            Assert.AreEqual(600, rules.Free.Two);
            Assert.AreEqual(750, rules.Free.Three);
            Assert.AreEqual(HideSeekRules.ParameterCount, rules.ParameterCount);
            string[] names = { "hidden", "visibility", "hint_delay", "decoys", "no_hints", "no_items", "mistake_limit", "night", "same_species", "move_every" };
            for (int i = 0; i < names.Length; i++)
                Assert.AreEqual(names[i], rules.Parameters[i].Name);

            int[] hidden = { 3, 3, 4, 4, 4, 4, 4, 5, 5, 5 };
            // A straight line 30 % → 15 % → 8 %, rounded to 0.01 (d3 is 22.4999… % in doubles: 0.22).
            double[] visible = { 0.30, 0.26, 0.22, 0.19, 0.15, 0.14, 0.12, 0.11, 0.09, 0.08 };
            int[] decoys = { 0, 0, 1, 1, 1, 1, 1, 2, 2, 2 };
            for (int d = 1; d <= 10; d++)
            {
                Assert.AreEqual(hidden[d - 1], rules.Value(HideSeekRules.Hidden, d), 1e-9, "hidden d" + d);
                Assert.AreEqual(visible[d - 1], rules.Value(HideSeekRules.Visibility, d), 1e-9, "visibility d" + d);
                Assert.AreEqual(5 + d, rules.Value(HideSeekRules.HintDelay, d), 1e-9, "hint delay d" + d + ": 6 s to 15 s");
                Assert.AreEqual(decoys[d - 1], rules.Value(HideSeekRules.Decoys, d), 1e-9, "decoys d" + d);
                for (int p = HideSeekRules.NoHints; p <= HideSeekRules.MoveEvery; p++)
                    Assert.AreEqual(0.0, rules.Value(p, d), 1e-9, "The free mode has no challenge rule: " + names[p]);
            }
            Assert.AreEqual(60, ActivityCatalog.Get(ActivityId.HideAndSeek).Seconds);
            Assert.AreEqual(HideSeekRules.BaseSeconds, rules.ChallengeSeconds(-1, HideSeekRules.BaseSeconds));
            Assert.AreEqual(ContentId.FromSource("treat.mushroom_biscuit"), HideSeekRules.ThreeStarTreat, "« 1 Biscuit champignon à 3 étoiles ».");
        }

        [Test]
        public void TheTenChallenges_AreTheDocTable_WithTheT39Counts()
        {
            // The doc's 10 challenges with the T39 counts (the game plays in a refuge zone of 5 to 8 good spots, see
            // Docs/Design/T39_Cache_cache.md): 3 animals for « Premiers cachés » (doc 4), 4 for the doc's « 5 animaux » ones,
            // 5 for « Grande famille » (doc 8), « Tous pareils » (doc 6) and « Le grand cache-cache » (doc 8), 4 for
            // « Cachettes mouvantes » (doc 6) plus its free spot; no decoy in 5, 9 and 10; challenge 10 keeps its items (real
            // zones hold 3 to 6 spots). Thresholds: the doc's where they still fit; 3 stars lowered in 3, 4, 5, 8, 9 and 10,
            // and 10's 1 and 2 stars too (balance tests on zones of 7, 6, 5 and 4 spots).
            var rules = HideSeekRules.Create();
            var definition = ActivityCatalog.Get(ActivityId.HideAndSeek);
            var table = new (int one, int two, int three, int level, int hidden, double visible, int hint, int decoys, int noHints, int noItems,
                int mistakes, double night, int same, int move, float seconds)[]
            {
                (200, 450, 650, 1, 3, 0.30, 6, 0, 0, 0, 0, 0.0, 0, 0, 60f),
                (300, 600, 850, 3, 4, 0.22, 8, 1, 1, 1, 0, 0.0, 0, 0, 60f),
                (300, 600, 800, 3, 4, 0.10, 8, 1, 0, 0, 0, 0.0, 0, 0, 60f),
                (250, 500, 700, 3, 4, 0.22, 8, 1, 0, 0, 0, 0.0, 0, 0, 40f),
                (450, 900, 1000, 5, 5, 0.20, 10, 0, 0, 0, 0, 0.0, 0, 0, 60f),
                (300, 600, 900, 3, 4, 0.22, 8, 1, 0, 0, 4, 0.0, 0, 0, 60f),
                (250, 550, 800, 3, 4, 0.22, 8, 1, 0, 0, 0, 0.25, 0, 0, 60f),
                (350, 700, 900, 5, 5, 0.15, 10, 1, 0, 0, 0, 0.0, 1, 0, 60f),
                (350, 700, 850, 5, 4, 0.15, 10, 0, 0, 0, 0, 0.0, 0, 15, 60f),
                (350, 600, 750, 10, 5, 0.08, 15, 0, 1, 0, 0, 0.0, 0, 0, 45f),
            };
            Assert.AreEqual(10, rules.Challenges.Length);
            for (int c = 0; c < table.Length; c++)
            {
                var row = table[c];
                var t = rules.Thresholds(c);
                string name = definition.ChallengeId(c);
                Assert.AreEqual("challenge.hide_seek." + (c + 1).ToString("00"), name);
                Assert.AreEqual(row.one, t.One, name);
                Assert.AreEqual(row.two, t.Two, name);
                Assert.AreEqual(row.three, t.Three, name);
                Assert.AreEqual(row.level, rules.Challenges[c].Level, name + " level");
                Assert.AreEqual(row.hidden, rules.Value(HideSeekRules.Hidden, 3, c), 1e-9, name + " hidden");
                Assert.AreEqual(row.visible, rules.Value(HideSeekRules.Visibility, 3, c), 1e-9, name + " visibility");
                Assert.AreEqual(row.hint, rules.Value(HideSeekRules.HintDelay, 3, c), 1e-9, name + " hint delay");
                Assert.AreEqual(row.decoys, rules.Value(HideSeekRules.Decoys, 3, c), 1e-9, name + " decoys");
                Assert.AreEqual(row.noHints, rules.Value(HideSeekRules.NoHints, 3, c), 1e-9, name + " no hints");
                Assert.AreEqual(row.noItems, rules.Value(HideSeekRules.NoItems, 3, c), 1e-9, name + " no items");
                Assert.AreEqual(row.mistakes, rules.Value(HideSeekRules.MistakeLimit, 3, c), 1e-9, name + " mistakes");
                Assert.AreEqual(row.night, rules.Value(HideSeekRules.Night, 3, c), 1e-9, name + " night");
                Assert.AreEqual(row.same, rules.Value(HideSeekRules.SameSpecies, 3, c), 1e-9, name + " same species");
                Assert.AreEqual(row.move, rules.Value(HideSeekRules.MoveEvery, 3, c), 1e-9, name + " move every");
                Assert.AreEqual(row.seconds, rules.ChallengeSeconds(c, HideSeekRules.BaseSeconds), 1e-6, name + " seconds");
            }
            Assert.AreEqual(1, HideSeekRules.NoHintChallenge);
            Assert.AreEqual(3, HideSeekRules.QuickChallenge);
            Assert.AreEqual(5, HideSeekRules.OneLookChallenge);
            Assert.AreEqual(6, HideSeekRules.NightChallenge);
            Assert.AreEqual(7, HideSeekRules.SameSpeciesChallenge, "The presenter picks one species for challenge 8's five animals.");
            Assert.AreEqual(8, HideSeekRules.MovingChallenge);
            Assert.AreEqual(9, HideSeekRules.GrandChallenge);
        }

        [Test]
        public void Begin_ReadsTheParameters_AtD1D5D10()
        {
            var expected = new (int d, int hidden, int decoys, float visible, float hint)[]
            {
                (1, 3, 0, 0.30f, 6f), (5, 4, 1, 0.15f, 10f), (10, 5, 2, 0.08f, 15f),
            };
            foreach (var row in expected)
            {
                var game = Start(row.d);
                string d = "d" + row.d;
                Assert.IsTrue(game.UsesDefaultSpots);
                Assert.AreEqual(HideSeekController.DefaultSpotCount, game.SpotCount);
                Assert.AreEqual(row.hidden, game.HiddenCount, d);
                Assert.AreEqual(row.decoys, game.DecoyCount, d);
                Assert.AreEqual(row.visible, game.Visibility, 1e-6f, d);
                Assert.AreEqual(row.hint, game.HintDelay, 1e-6f, d);
                Assert.IsTrue(game.HintsEnabled);
                Assert.IsTrue(game.ItemsEnabled);
                Assert.IsTrue(game.MagnifierAvailable);
                Assert.IsTrue(game.WhistleAvailable);
                Assert.AreEqual(0, game.MistakeLimit);
                Assert.AreEqual(0f, game.NightSpotlight);
                Assert.IsFalse(game.SameSpecies);
                Assert.AreEqual(0f, game.MoveEvery);
                Assert.AreEqual(60f, game.Seconds);
                Assert.AreEqual(0f, game.Elapsed);
                Assert.AreEqual(0, game.Score);
                Assert.IsFalse(game.Finished);
                Assert.AreEqual(-1, game.MagnifierSpot);
                Assert.AreEqual(row.decoys, Enumerable.Range(0, game.SpotCount).Count(game.IsDecoy));
                var spots = new HashSet<int>();
                for (int a = 0; a < game.HiddenCount; a++)
                {
                    int spot = game.AnimalSpot(a);
                    Assert.IsTrue(spots.Add(spot), "One animal per spot.");
                    Assert.AreEqual(a, game.AnimalAt(spot));
                    Assert.IsFalse(game.IsDecoy(spot), "A decoy is empty.");
                    Assert.IsFalse(game.AnimalFound(a));
                    Assert.IsFalse(game.AnimalHinting(a));
                    Assert.IsFalse(game.CountsAfterHint(a));
                    Assert.That(game.HintSecond(a), NUnit.Framework.Is.InRange(row.hint, row.hint + HideSeekRules.HintStaggerSeconds));
                }
                Assert.AreEqual(-1, game.AnimalSpot(game.HiddenCount));
                Assert.AreEqual(-1, game.AnimalSpot(-1));
            }
        }

        [Test]
        public void EachChallenge_SetsItsGameUp()
        {
            var rules = HideSeekRules.Create();
            for (int c = 0; c < 10; c++)
            {
                var game = Start(challenge: c);
                string name = "challenge " + (c + 1);
                bool helpers = c != HideSeekRules.NoHintChallenge && c != HideSeekRules.GrandChallenge;
                bool items = c != HideSeekRules.NoHintChallenge;
                Assert.AreEqual((int)rules.Value(HideSeekRules.Hidden, 0, c), game.HiddenCount, name);
                Assert.AreEqual((int)rules.Value(HideSeekRules.Decoys, 0, c), game.DecoyCount, name);
                Assert.AreEqual((float)rules.Value(HideSeekRules.Visibility, 0, c), game.Visibility, 1e-6f, name);
                Assert.AreEqual(helpers, game.HintsEnabled, name + ": hints off in challenges 2 and 10.");
                Assert.AreEqual(items, game.ItemsEnabled, name + ": items off in challenge 2 only.");
                Assert.AreEqual(items, game.MagnifierAvailable, name);
                Assert.AreEqual(items, game.WhistleAvailable, name);
                Assert.AreEqual(c == HideSeekRules.OneLookChallenge ? 4 : 0, game.MistakeLimit, name);
                Assert.AreEqual(c == HideSeekRules.NightChallenge ? 0.25f : 0f, game.NightSpotlight, 1e-6f, name);
                Assert.AreEqual(c == HideSeekRules.SameSpeciesChallenge, game.SameSpecies, name);
                Assert.AreEqual(c == HideSeekRules.MovingChallenge ? 15f : 0f, game.MoveEvery, 1e-6f, name);
                Assert.AreEqual(c == HideSeekRules.QuickChallenge ? 40f : c == HideSeekRules.GrandChallenge ? 45f : 60f, game.Seconds, 1e-6f, name);
            }
            Assert.AreEqual(0.10f, Start(challenge: 2).Visibility, 1e-6f, "Challenge 3 « Bien cachés »: 10 %.");
            var family = Start(challenge: 4);
            Assert.AreEqual(5, family.HiddenCount, "Challenge 5 « Grande famille »: 5 animals at 20 % (T39; doc 8).");
            Assert.AreEqual(0.20f, family.Visibility, 1e-6f);
            Assert.AreEqual(0, family.DecoyCount);
            var same = Start(challenge: 7);
            Assert.AreEqual(5, same.HiddenCount, "Challenge 8: five animals of one species at 15 % (T39; doc 6).");
            Assert.AreEqual(0.15f, same.Visibility, 1e-6f);
            var moving = Start(challenge: 8);
            Assert.AreEqual(4, moving.HiddenCount, "Challenge 9: four animals (T39; doc 6), no decoy, a free spot.");
            Assert.AreEqual(0, moving.DecoyCount);
            var grand = Start(challenge: 9);
            Assert.AreEqual(5, grand.HiddenCount, "Challenge 10: 5 animals (T39; doc 8), 8 %, 45 s, no decoy.");
            Assert.AreEqual(0.08f, grand.Visibility, 1e-6f);
            Assert.AreEqual(0, grand.DecoyCount);

            // « Activités plus lentes »: every length × 1.25, and challenge 9's moves too; the hints keep their seconds.
            Assert.AreEqual(75f, Start(3, slower: true).Seconds, 1e-4f);
            Assert.AreEqual(50f, Start(challenge: HideSeekRules.QuickChallenge, slower: true).Seconds, 1e-4f);
            var slowMoves = Start(challenge: HideSeekRules.MovingChallenge, slower: true);
            Assert.AreEqual(18.75f, slowMoves.MoveEvery, 1e-4f);
            Assert.AreEqual(10f, slowMoves.HintDelay, 1e-6f);
        }

        // ---------------- layout ----------------

        [Test]
        public void TheLayout_IsTheSeed_NeverSideBySide_WhenTheSetAllowsIt_DecoysAmongTheFreeSpots()
        {
            string Key(HideSeekController g)
            {
                var parts = Enumerable.Range(0, g.HiddenCount).Select(a => g.AnimalSpot(a).ToString())
                    .Concat(Enumerable.Range(0, g.DecoyCount).Select(k => "d" + g.DecoySpot(k)));
                return string.Join(",", parts);
            }

            var game = new HideSeekController();
            Assert.AreEqual(Key(Start(5, seed: 42, game: game)), Key(Start(5, seed: 42)), "Same seed, same layout.");
            game.LastLayout = 0;
            string again = Key(Start(5, seed: 42, game: game));
            Assert.AreNotEqual(again, Key(Start(5, seed: 42, game: game)), "Replayed on the same spots: never the same layout twice in a row.");
            game.LastLayout = 0;
            Assert.AreEqual(again, Key(Start(5, seed: 42, game: game)), "Same seed and same last layout: same layout.");
            var layouts = new HashSet<string>();
            for (uint seed = 1; seed <= 60; seed++)
            {
                foreach (int d in new[] { 1, 3, 5 })
                {
                    Start(d, seed: seed, game: game);
                    layouts.Add(d + ":" + Key(game));
                    // On the 4 × 3 grid up to 6 animals fit apart (a checkerboard): never two side by side.
                    for (int a = 0; a < game.HiddenCount; a++)
                        for (int b = a + 1; b < game.HiddenCount; b++)
                            Assert.IsFalse(game.SideBySide(game.AnimalSpot(a), game.AnimalSpot(b)), "d" + d + " seed " + seed);
                    for (int k = 0; k < game.DecoyCount; k++)
                    {
                        int spot = game.DecoySpot(k);
                        Assert.IsTrue(game.IsDecoy(spot));
                        Assert.AreEqual(-1, game.AnimalAt(spot), "A decoy is a free spot.");
                    }
                }
                // 8 animals and 4 decoys (rules crowded on purpose) fill the 12 spots; 8 cannot all stand apart on the grid, the
                // fewest pairs touch (4).
                var crowded = CrowdedSetup(seed);
                game.Begin(in crowded);
                Assert.AreEqual(12, Enumerable.Range(0, 12).Count(s => game.AnimalAt(s) >= 0 || game.IsDecoy(s)));
                int pairs = 0;
                for (int a = 0; a < game.HiddenCount; a++)
                    for (int b = a + 1; b < game.HiddenCount; b++)
                        if (game.SideBySide(game.AnimalSpot(a), game.AnimalSpot(b)))
                            pairs++;
                Assert.AreEqual(4, pairs, "6 apart, the 2 others beside 2 each: seed " + seed);
            }
            Assert.Greater(layouts.Count, 100, "The seed changes the layout: " + layouts.Count + " of 180 (3 or 4 animals spread farthest first).");

            // The grid's neighbors: 2 m apart side by side, the 2.83 m diagonals not.
            Assert.IsTrue(game.SideBySide(0, 1));
            Assert.IsTrue(game.SideBySide(0, 4));
            Assert.IsFalse(game.SideBySide(0, 5));
            Assert.IsFalse(game.SideBySide(0, 0));
        }

        /// <summary>The free mode at d5 with 8 animals and 4 decoys: more than the 4 × 3 grid holds apart.</summary>
        private static ActivitySetup CrowdedSetup(uint seed) => Forced(seed, hidden: 8, decoys: 4);

        /// <summary>
        /// A game of the shipped rules (the free mode at d5, or a challenge) with some values forced (NaN keeps the rule's):
        /// the visibility above all, which sets how many of the best spots the animals may take.
        /// </summary>
        internal static ActivitySetup Forced(uint seed, int challenge = -1, double hidden = double.NaN, double visibility = double.NaN,
            double decoys = double.NaN)
        {
            var rules = HideSeekRules.Create();
            var parameters = (DifficultyParameter[])rules.Parameters.Clone();
            var challenges = (ChallengeRule[])rules.Challenges.Clone();
            double[] forced = { hidden, visibility, decoys };
            int[] index = { HideSeekRules.Hidden, HideSeekRules.Visibility, HideSeekRules.Decoys };
            for (int i = 0; i < forced.Length; i++)
            {
                if (double.IsNaN(forced[i]))
                    continue;
                var parameter = parameters[index[i]];
                parameters[index[i]] = new DifficultyParameter(parameter.Name, forced[i], forced[i], forced[i], 2);
                if (challenge < 0)
                    continue;
                var rule = challenges[challenge];
                var overrides = (double[])rule.Overrides.Clone();
                overrides[index[i]] = forced[i];
                challenges[challenge] = new ChallengeRule(rule.Stars, rule.Level, overrides, rule.Seconds);
            }
            var result = new ActivityRules(ActivityId.HideAndSeek, rules.Free, parameters, challenges);
            return HideSeekBalanceTests.Setup(result, 5, challenge, seed);
        }

        [Test]
        public void TheBestSpots_HideTheAnimals_TheFirstNPlus2At8Percent_WideningOnlyForSpacing()
        {
            // The stage gives the zone's spots best first (the hide-spot pipeline's order). At 8 % visible only the best
            // hiders leave that little: the animals take the best N + 2.
            var game = HideSeekBalanceTests.Game(8);
            var used = new HashSet<int>();
            for (uint seed = 1; seed <= 80; seed++)
            {
                var setup = Forced(seed, hidden: 4, visibility: 0.08, decoys: 1);
                game.Begin(in setup);
                Assert.AreEqual(4, game.HiddenCount);
                for (int a = 0; a < 4; a++)
                {
                    int spot = game.AnimalSpot(a);
                    Assert.Less(spot, 6, "8 spots, 4 animals at 8 %: among the best 6 (seed " + seed + ").");
                    used.Add(spot);
                    for (int b = a + 1; b < 4; b++)
                        Assert.IsFalse(game.SideBySide(spot, game.AnimalSpot(b)), "Still apart: seed " + seed);
                }
                Assert.AreEqual(1, game.DecoyCount);
                Assert.AreEqual(-1, game.AnimalAt(game.DecoySpot(0)), "The decoy among the other spots.");
            }
            Assert.Greater(used.Count, 4, "The seed still varies which of the best spots hide.");

            // Challenge 3 « Bien cachés » at 10 %: still the best 6 (6 + 2 × 1/6, rounded).
            for (uint seed = 1; seed <= 20; seed++)
            {
                Start(challenge: 2, seed: seed, game: game);
                Assert.IsTrue(Enumerable.Range(0, 4).All(a => game.AnimalSpot(a) < 6), "seed " + seed);
            }

            // The window widens only when spacing forces it: the first 4 spots of this zone all touch, the animals go further.
            var tight = new HideSeekController();
            tight.SetSpots(8, new[] { 0f, 1f, 0f, 1f, 4f, -4f, 4f, -4f }, new float[8], new[] { 0f, 0f, 1f, 1f, 4f, 4f, -4f, -4f });
            var low = Forced(3, hidden: 3, visibility: 0.08, decoys: 0);
            tight.Begin(in low);
            Assert.AreEqual(3, tight.HiddenCount);
            var spots = Enumerable.Range(0, 3).Select(tight.AnimalSpot).ToArray();
            Assert.IsFalse(spots.Any(a => spots.Any(b => a != b && tight.SideBySide(a, b))), "Apart, beyond the best 5.");

            // The synthetic meadow has no order: its 12 spots all take animals over the seeds.
            var meadow = new HashSet<int>();
            for (uint seed = 1; seed <= 40; seed++)
            {
                var grid = Start(5, seed: seed);
                for (int a = 0; a < grid.HiddenCount; a++)
                    meadow.Add(grid.AnimalSpot(a));
            }
            Assert.AreEqual(12, meadow.Count);

            // Challenge 9 at 8 %: 4 animals on the best 6 of 8, a move to one of the best free spots (2 free there).
            for (uint seed = 1; seed <= 20; seed++)
            {
                var moving = Forced(seed, HideSeekRules.MovingChallenge, visibility: 0.08);
                game.Begin(in moving);
                Assert.AreEqual(4, game.HiddenCount);
                Assert.IsTrue(Enumerable.Range(0, 4).All(a => game.AnimalSpot(a) < 6));
                TickTo(game, 15.05, 0.1f);
                var move = Drain(game).Single(e => e.Kind == HideSeekEventKind.Moved);
                Assert.Less(move.Spot, 6, "A best free spot: seed " + seed);
            }
        }

        [Test]
        public void Replays_UseTheWholeZoneAt30Percent_NeverTheSameLayoutTwiceInARow_AnimalsInASeededOrder()
        {
            // From 20 % visible every measured spot reaches the share: the whole zone, the previous game's spots weighing half.
            var game = HideSeekBalanceTests.Game(8);
            var zeros = new float[8];
            var used = new int[8];
            var firstAnimal = new HashSet<int>();
            uint previous = 0;
            for (uint seed = 1; seed <= 10; seed++)
            {
                // The stage ends a game left running and gives the same zone's spots again: the memory stays.
                game.EndGame();
                game.SetSpots(8, HideSeekBalanceTests.Zone8X, zeros, HideSeekBalanceTests.Zone8Z);
                Assert.AreEqual(previous, game.LastLayout);
                Start(1, seed: seed, game: game);
                Assert.AreEqual(0.30f, game.Visibility, 1e-6f);
                uint mask = game.LayoutMask;
                Assert.AreNotEqual(previous, mask, "Two games in a row, two layouts: seed " + seed);
                Assert.AreEqual(mask, game.LastLayout, "Remembered for the next game on these spots.");
                for (int a = 0; a < game.HiddenCount; a++)
                {
                    Assert.AreNotEqual(0u, mask & (1u << game.AnimalSpot(a)));
                    used[game.AnimalSpot(a)]++;
                }
                firstAnimal.Add(game.AnimalSpot(0));
                previous = mask;
            }
            for (int spot = 0; spot < 8; spot++)
                Assert.GreaterOrEqual(used[spot], 1, "Spot " + spot + " hides an animal at least once in 10 replays.");
            Assert.Greater(firstAnimal.Count, 3, "The first animal is not tied to a spot: the animals take the spots in a seeded order.");

            // Another zone in between: its spots reset the memory; the stage gives this zone's last layout back.
            game.EndGame();
            game.SetSpots(7, HideSeekBalanceTests.Zone7X, new float[7], HideSeekBalanceTests.Zone7Z);
            Assert.AreEqual(0u, game.LastLayout, "Other spots: nothing remembered.");
            Start(1, seed: 11, game: game);
            game.EndGame();
            game.SetSpots(8, HideSeekBalanceTests.Zone8X, zeros, HideSeekBalanceTests.Zone8Z);
            for (uint seed = 20; seed < 40; seed++)
            {
                game.LastLayout = previous;
                Start(1, seed: seed, game: game);
                Assert.AreNotEqual(previous, game.LayoutMask, "The zone's last layout, given back by the stage, is avoided: seed " + seed);
                game.EndGame();
            }

            // A zone with room for one layout only (3 animals on 3 spots) repeats it.
            var three = new HideSeekController();
            three.SetSpots(3, new[] { -2f, 0f, 2f }, new float[3], new[] { 0f, 1f, 0f });
            Start(1, seed: 1, game: three);
            uint only = three.LayoutMask;
            Assert.AreEqual(7u, only);
            three.EndGame();
            Start(1, seed: 2, game: three);
            Assert.AreEqual(only, three.LayoutMask);
        }

        [Test]
        public void TheZone_BoundsTheCounts_AndChallenge9_KeepsAFreeSpot()
        {
            // 5 spots: d10's 5 animals and 2 decoys become 5 and 0; challenge 9 keeps its 4 animals and a spot to move to;
            // challenge 10 keeps its 5.
            var game = HideSeekBalanceTests.Game(5);
            Start(10, game: game);
            Assert.AreEqual(5, game.HiddenCount);
            Assert.AreEqual(0, game.DecoyCount);
            Start(challenge: HideSeekRules.MovingChallenge, game: game);
            Assert.AreEqual(4, game.HiddenCount);
            Assert.AreEqual(0, game.DecoyCount);
            TickTo(game, 15.05, 0.1f);
            Assert.AreEqual(1, game.Moves, "The free spot takes the move.");
            Start(challenge: HideSeekRules.GrandChallenge, game: game);
            Assert.AreEqual(5, game.HiddenCount);

            // 7 spots: challenge 9's 4 animals (3 spots free); d8 keeps 5 animals and 2 decoys.
            game = HideSeekBalanceTests.Game(7);
            Start(challenge: HideSeekRules.MovingChallenge, game: game);
            Assert.AreEqual(4, game.HiddenCount);
            Start(8, game: game);
            Assert.AreEqual(5, game.HiddenCount);
            Assert.AreEqual(2, game.DecoyCount);

            // 4 spots: challenge 9's 4 animals become 3 (a spot to move to); d10's 5 animals and 2 decoys become 4 and 0;
            // d3's decoy gives way to its 4 animals.
            game = HideSeekBalanceTests.Game(4);
            Start(challenge: HideSeekRules.MovingChallenge, game: game);
            Assert.AreEqual(3, game.HiddenCount);
            Assert.AreEqual(0, game.DecoyCount);
            TickTo(game, 15.05, 0.1f);
            Assert.AreEqual(1, game.Moves);
            Start(10, game: game);
            Assert.AreEqual(4, game.HiddenCount);
            Assert.AreEqual(0, game.DecoyCount);
            Start(3, game: game);
            Assert.AreEqual(4, game.HiddenCount);
            Assert.AreEqual(0, game.DecoyCount);

            // 5 spots, challenge 9 with a decoy forced: it gives way to the free spot.
            var rules = HideSeekRules.Create();
            var parameters = (DifficultyParameter[])rules.Parameters.Clone();
            var challenges = (ChallengeRule[])rules.Challenges.Clone();
            var nine = challenges[HideSeekRules.MovingChallenge];
            var overrides = (double[])nine.Overrides.Clone();
            overrides[HideSeekRules.Decoys] = 1;
            challenges[HideSeekRules.MovingChallenge] = new ChallengeRule(nine.Stars, nine.Level, overrides, nine.Seconds);
            var withDecoy = new ActivityRules(ActivityId.HideAndSeek, rules.Free, parameters, challenges);
            var five = HideSeekBalanceTests.Game(5);
            var setup = HideSeekBalanceTests.Setup(withDecoy, 0, HideSeekRules.MovingChallenge, 7);
            five.Begin(in setup);
            Assert.AreEqual(4, five.HiddenCount);
            Assert.AreEqual(0, five.DecoyCount, "4 animals on 5 spots: the last one stays free, no decoy.");
        }

        [Test]
        public void TheSpots_AreCopied_Clamped_AndKeptDuringAGame()
        {
            var game = new HideSeekController();
            var x = (float[])HideSeekBalanceTests.ClearingX.Clone();
            var y = (float[])HideSeekBalanceTests.ClearingY.Clone();
            var z = (float[])HideSeekBalanceTests.ClearingZ.Clone();
            game.SetSpots(x.Length, x, y, z);
            x[0] = 99f;
            Assert.AreEqual(HideSeekBalanceTests.ClearingX[0], game.SpotX(0), "Copied.");
            Assert.IsFalse(game.UsesDefaultSpots);
            Assert.AreEqual(10, game.SpotCount);

            // 10 spots at d10: 5 animals, 2 decoys.
            Start(10, game: game);
            Assert.AreEqual(5, game.HiddenCount);
            Assert.AreEqual(2, game.DecoyCount);

            // During a game the set stays; after the end (or EndGame after a quit) the next one applies.
            var few = new[] { 0f, 3f, 6f, 9f, 12f };
            var zeros = new float[5];
            game.SetSpots(5, few, zeros, zeros);
            Assert.AreEqual(10, game.SpotCount, "Ignored during a game.");
            game.EndGame();
            game.SetSpots(5, few, zeros, zeros);
            Assert.AreEqual(5, game.SpotCount);
            Start(10, game: game);
            Assert.AreEqual(5, game.HiddenCount, "5 animals on 5 spots.");
            Assert.AreEqual(0, game.DecoyCount, "No spot left for a decoy (N + D clamped to the spots).");
            Assert.AreEqual(0, Enumerable.Range(0, 5).Count(s => game.AnimalAt(s) < 0));

            // More than 32: 32. Nothing (or a count of 0): the synthetic meadow.
            game.EndGame();
            var many = Enumerable.Range(0, 40).Select(i => (float)i).ToArray();
            game.SetSpots(40, many, new float[40], new float[40]);
            Assert.AreEqual(HideSeekController.MaxSpots, game.SpotCount);
            game.SetSpots(0, many, many, many);
            Assert.IsTrue(game.UsesDefaultSpots);
            Assert.AreEqual(12, game.SpotCount);
            Assert.AreEqual(-3f, game.SpotX(0));
            Assert.AreEqual(3f, game.SpotX(11));
            Assert.AreEqual(2f, game.SpotZ(11));
            Assert.AreEqual(0f, game.SpotX(12));
        }

        // ---------------- scoring ----------------

        [Test]
        public void AFind_BeforeItsHint_Is100Plus2PerSecondLeft_AfterIt50Plus1()
        {
            var game = Start(3);
            Assert.AreEqual(8f, game.HintDelay);
            TickTo(game, 3.4);
            Drain(game);
            Assert.AreEqual(56, game.SecondsLeft, "Whole seconds left: 56.6 → 56.");
            int spot = game.AnimalSpot(0);
            game.TapSpot(spot);
            var events = Drain(game);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(HideSeekEventKind.Found, events[0].Kind, "« coucou »");
            Assert.AreEqual(spot, events[0].Spot);
            Assert.AreEqual(0, events[0].Animal);
            Assert.AreEqual(100 + 2 * 56, events[0].Points);
            Assert.AreEqual(212, game.Score);
            Assert.IsTrue(game.AnimalFound(0));
            Assert.AreEqual(-1, game.AnimalSpot(0), "−1 once found.");
            Assert.AreEqual(-1, game.AnimalAt(spot));
            Assert.IsTrue(game.FoundAt(spot));
            Assert.AreEqual(1, game.FoundBeforeHint);
            Assert.AreEqual(1, game.Streak);

            // Animal 1's hint, then its find: +50 and +1 per second left.
            TickTo(game, game.HintSecond(1) + 0.02);
            Assert.IsTrue(game.AnimalHinting(1));
            Assert.IsTrue(game.CountsAfterHint(1));
            Assert.That(Drain(game).Any(e => e.Kind == HideSeekEventKind.Hint && e.Animal == 1 && e.Spot == game.AnimalSpot(1)));
            int left = game.SecondsLeft;
            spot = game.AnimalSpot(1);
            game.TapSpot(spot);
            events = Drain(game);
            Assert.AreEqual(HideSeekEventKind.FoundAfterHint, events[0].Kind);
            Assert.AreEqual(50 + left, events[0].Points);
            Assert.AreEqual(212 + 50 + left, game.Score);
            Assert.AreEqual(1, game.FoundBeforeHint);
            Assert.AreEqual(2, game.FoundCount);
            Assert.IsFalse(game.AnimalHinting(1), "Found: no longer hinting.");
            Assert.AreEqual(2, game.Streak);
            Assert.AreEqual(game.Score, game.ChallengeScore);

            // A found animal's spot does nothing more: no point, no leaves, no streak lost.
            game.TapSpot(spot);
            Assert.AreEqual(0, game.EventCount);
            Assert.AreEqual(0, game.EmptyTouches);
            Assert.AreEqual(2, game.Streak);
        }

        [Test]
        public void TheHints_ComeAtTheDelay_StaggeredOver1Point5s_NeverTogether()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var game = Start(10, seed: seed);
                var seconds = Enumerable.Range(0, game.HiddenCount).Select(a => (double)game.HintSecond(a)).OrderBy(s => s).ToList();
                Assert.GreaterOrEqual(seconds[0], 15.0);
                Assert.LessOrEqual(seconds[seconds.Count - 1], 15.0 + HideSeekRules.HintStaggerSeconds + 1e-6);
                // 5 staggers spread over 1.5 s: at least half their 0.375 s spacing apart.
                for (int i = 1; i < seconds.Count; i++)
                    Assert.Greater(seconds[i] - seconds[i - 1], 0.1, "seed " + seed);
                Assert.Greater(seconds[seconds.Count - 1] - seconds[0], 1.0, "Spread over the span, not bunched.");

                TickTo(game, 14.9);
                Assert.AreEqual(0, Count(Drain(game), HideSeekEventKind.Hint), "No hint before the delay.");
                int hints = 0;
                while (game.Elapsed < 17f)
                {
                    game.Tick(Frame);
                    var events = Drain(game);
                    int now = Count(events, HideSeekEventKind.Hint);
                    Assert.LessOrEqual(now, 1, "Never two hints in the same frame: seed " + seed);
                    foreach (var e in events.Where(e => e.Kind == HideSeekEventKind.Hint))
                    {
                        Assert.AreEqual(game.HintSecond(e.Animal), game.Elapsed, Frame + 1e-4f);
                        Assert.AreEqual(game.AnimalSpot(e.Animal), e.Spot);
                    }
                    hints += now;
                }
                Assert.AreEqual(5, hints);
                Assert.IsTrue(Enumerable.Range(0, 5).All(game.AnimalHinting), "They keep hinting until found.");
            }
            var a1 = Start(5, seed: 9);
            var a2 = Start(5, seed: 9);
            for (int a = 0; a < a1.HiddenCount; a++)
                Assert.AreEqual(a1.HintStagger(a), a2.HintStagger(a), "Deterministic.");
        }

        [Test]
        public void ATimidAnimal_Waits2sMoreForItsHint()
        {
            var game = Start(1, seed: 5);
            float plain = game.HintSecond(2);
            game.SetTimid(2, true);
            Assert.IsTrue(game.IsTimid(2));
            Assert.AreEqual(plain + 2f, game.HintSecond(2), 1e-5f);
            game.SetTimid(9, true);
            game.SetTimid(-1, true);
            TickTo(game, plain + 0.1);
            Assert.IsFalse(game.AnimalHinting(2), "Not yet.");
            Assert.IsTrue(game.AnimalHinting(0) || game.HintSecond(0) > plain + 0.1);
            TickTo(game, plain + 2.05);
            Assert.IsTrue(game.AnimalHinting(2), "2 s later.");
            Assert.IsTrue(game.CountsAfterHint(2));

            game = Start(1, seed: 5);
            game.SetTimid(2, true);
            game.SetTimid(2, false);
            Assert.AreEqual(plain, game.HintSecond(2), 1e-5f, "Back to the delay.");
            Start(1, seed: 5, game: game);
            Assert.IsFalse(game.IsTimid(2), "A new game forgets the temperaments.");
        }

        [Test]
        public void AnEmptyTouch_FliesLeaves_LosesNothing_AndTheStreakStartsAgain()
        {
            var game = Start(5);
            game.Tick(2f);
            game.TapSpot(game.AnimalSpot(0));
            game.TapSpot(game.AnimalSpot(1));
            int score = game.Score;
            Assert.AreEqual(2, game.Streak);
            Drain(game);
            int empty = FreeSpot(game);
            game.TapSpot(empty);
            var events = Drain(game);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(HideSeekEventKind.Empty, events[0].Kind);
            Assert.AreEqual(empty, events[0].Spot);
            Assert.AreEqual(-1, events[0].Animal);
            Assert.AreEqual(0, events[0].Points);
            Assert.AreEqual(score, game.Score, "No point lost.");
            Assert.AreEqual(0, game.Streak);
            Assert.AreEqual(2, game.BestStreak);
            Assert.AreEqual(1, game.EmptyTouches);
            Assert.AreEqual(0, game.Mistakes, "Mistakes only count in challenge 6.");

            // A decoy is empty too.
            game.Tick(1f);
            Drain(game);
            int decoy = game.DecoySpot(0);
            game.TapSpot(decoy);
            events = Drain(game);
            Assert.AreEqual(HideSeekEventKind.Empty, events.Single().Kind);
            Assert.AreEqual(decoy, events[0].Spot);
            Assert.AreEqual(2, game.EmptyTouches);
            Assert.AreEqual(score, game.Score);

            // Out of the set: nothing.
            game.TapSpot(-1);
            game.TapSpot(game.SpotCount);
            Assert.AreEqual(0, game.EventCount);
        }

        [Test]
        public void ThreeEmptyTouchesWithin2s_IgnoreTheTouchesFor1s_WithOneEvent()
        {
            var game = Start(5);
            game.Tick(1f);
            int empty = FreeSpot(game);
            game.TapSpot(empty);
            game.Tick(0.9f);
            game.TapSpot(empty);
            game.Tick(0.9f);
            Assert.IsFalse(game.TouchesPaused);
            game.TapSpot(empty);
            var events = Drain(game);
            Assert.AreEqual(3, Count(events, HideSeekEventKind.Empty));
            Assert.AreEqual(1, Count(events, HideSeekEventKind.TouchesPaused), "Once.");
            Assert.AreEqual(-1, events.Last().Spot);
            Assert.IsTrue(game.TouchesPaused);

            // Ignored for 1 s: an animal's spot gives nothing, an empty one nothing either.
            game.Tick(0.5f);
            Drain(game);
            game.TapSpot(game.AnimalSpot(0));
            game.TapSpot(empty);
            Assert.AreEqual(0, game.EventCount);
            Assert.AreEqual(0, game.FoundCount);
            Assert.AreEqual(3, game.EmptyTouches);
            game.Tick(0.51f);
            Assert.IsFalse(game.TouchesPaused);
            game.TapSpot(game.AnimalSpot(0));
            Assert.AreEqual(1, game.FoundCount, "Touches work again after 1 s.");

            // Three empty touches spread over more than 2 s: no pause.
            game = Start(5);
            game.Tick(1f);
            empty = FreeSpot(game);
            game.TapSpot(empty);
            game.Tick(1.1f);
            game.TapSpot(empty);
            game.Tick(1.1f);
            game.TapSpot(empty);
            Assert.AreEqual(0, Count(Drain(game), HideSeekEventKind.TouchesPaused));
            Assert.IsFalse(game.TouchesPaused);
        }

        [Test]
        public void TheDecoys_RustleOnTheirOwn_NeverWithAHintNorTogether()
        {
            for (uint seed = 1; seed <= 12; seed++)
            {
                var game = Start(10, seed: seed);
                if (seed % 3 == 0)
                    game.SetTimid(1, true);
                var hints = Enumerable.Range(0, game.HiddenCount).Select(a => (double)game.HintSecond(a)).ToList();
                var decoys = new HashSet<int>(Enumerable.Range(0, game.DecoyCount).Select(game.DecoySpot));
                var rustles = new List<(double at, int spot)>();
                while (!game.Finished)
                {
                    game.Tick(Frame);
                    foreach (var e in Drain(game))
                        if (e.Kind == HideSeekEventKind.DecoyRustle)
                            rustles.Add((game.Elapsed, e.Spot));
                }
                Assert.IsTrue(rustles.All(r => decoys.Contains(r.spot)), "Only decoys rustle.");
                foreach (int decoy in decoys)
                    Assert.GreaterOrEqual(rustles.Count(r => r.spot == decoy), 8, "Every few seconds over 60 s: seed " + seed);
                foreach (var r in rustles)
                    foreach (double hint in hints)
                        Assert.GreaterOrEqual(Math.Abs(r.at - hint), HideSeekRules.HintGapSeconds - Frame - 1e-4,
                            $"seed {seed}: a rustle at {r.at:F2} s and a hint at {hint:F2} s");
                for (int i = 1; i < rustles.Count; i++)
                    Assert.GreaterOrEqual(rustles[i].at - rustles[i - 1].at, HideSeekRules.DecoyGapSeconds - Frame - 1e-4, "seed " + seed);
            }
            var calm = Start(1);
            calm.Tick(30f);
            Assert.AreEqual(0, Count(Drain(calm), HideSeekEventKind.DecoyRustle), "d1: no decoy.");
        }

        // ---------------- items ----------------

        [Test]
        public void TheMagnifier_RingsTheNearestOccupiedSpotFor2s_Once_ItsAnimalCountsAfterItsHint()
        {
            var game = Start(5);
            game.Tick(1f);
            Drain(game);
            int spot = game.AnimalSpot(2);
            game.UseMagnifier(game.SpotX(spot) + 0.2f, game.SpotY(spot), game.SpotZ(spot) - 0.1f);
            Assert.AreEqual(spot, game.MagnifierSpot, "The occupied spot nearest to the point.");
            Assert.AreEqual(2f, game.MagnifierLeft, 1e-6f);
            Assert.IsTrue(game.MagnifierUsed);
            Assert.IsFalse(game.MagnifierAvailable, "Once per game.");
            Assert.IsTrue(game.WhistleAvailable);
            Assert.IsTrue(game.CountsAfterHint(2));
            Assert.IsFalse(game.AnimalHinting(2), "Ringed, not hinting.");
            var events = Drain(game);
            Assert.AreEqual(HideSeekEventKind.Magnifier, events.Single().Kind);
            Assert.AreEqual(spot, events[0].Spot);
            Assert.AreEqual(2, events[0].Animal);
            game.UseMagnifier(0f, 0f, 0f);
            Assert.AreEqual(0, game.EventCount, "A second magnifier: nothing.");
            game.Tick(1f);
            Assert.AreEqual(1f, game.MagnifierLeft, 1e-5f);
            game.Tick(1.01f);
            Assert.AreEqual(-1, game.MagnifierSpot, "2 s.");
            Assert.AreEqual(0f, game.MagnifierLeft);

            // Before its hint, still the after-hint points.
            Drain(game);
            int left = game.SecondsLeft;
            game.TapSpot(spot);
            events = Drain(game);
            Assert.AreEqual(HideSeekEventKind.FoundAfterHint, events.Single().Kind);
            Assert.AreEqual(50 + left, events[0].Points);
            Assert.AreEqual(0, game.FoundBeforeHint);

            // A ring found within its 2 s ends with the find; the nearest occupied spot only (never an empty one).
            game = Start(5, seed: 3);
            int empty = FreeSpot(game);
            game.UseMagnifier(game.SpotX(empty), game.SpotY(empty), game.SpotZ(empty));
            Assert.IsTrue(game.AnimalAt(game.MagnifierSpot) >= 0);
            game.TapSpot(game.MagnifierSpot);
            Assert.AreEqual(-1, game.MagnifierSpot);
            Assert.AreEqual(0f, game.MagnifierLeft);

            // « Activités plus lentes »: 2.5 s.
            game = Start(5, slower: true);
            game.UseMagnifier(0f, 0f, 0f);
            Assert.AreEqual(2.5f, game.MagnifierLeft, 1e-5f);
        }

        [Test]
        public void TheWhistle_WigglesEveryAnimalStillHidden_Once_TheyCountAfterTheirHint()
        {
            var game = Start(5);
            game.Tick(1f);
            game.TapSpot(game.AnimalSpot(0));
            Drain(game);
            game.UseWhistle();
            var events = Drain(game);
            Assert.AreEqual(3, events.Count, "One wiggle per animal still hidden (4 − 1).");
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreEqual(HideSeekEventKind.Whistle, events[i].Kind);
                Assert.AreEqual(i + 1, events[i].Animal, "In animal order.");
                Assert.AreEqual(game.AnimalSpot(i + 1), events[i].Spot);
            }
            Assert.IsTrue(game.WhistleUsed);
            Assert.IsFalse(game.WhistleAvailable);
            Assert.IsTrue(game.MagnifierAvailable, "The other item stays.");
            game.UseWhistle();
            Assert.AreEqual(0, game.EventCount, "Once per game.");
            for (int a = 1; a < 4; a++)
            {
                Assert.IsTrue(game.CountsAfterHint(a));
                int left = game.SecondsLeft;
                game.TapSpot(game.AnimalSpot(a));
                var found = Drain(game).First();
                Assert.AreEqual(HideSeekEventKind.FoundAfterHint, found.Kind);
                Assert.AreEqual(50 + left, found.Points);
            }
            Assert.AreEqual(1, game.FoundBeforeHint);
            Assert.IsTrue(game.AllFound);
        }

        // ---------------- the end ----------------

        [Test]
        public void AllFound_Gives5PerSecondLeft_AndEndsTheGame()
        {
            var game = Start(1);
            TickTo(game, 4.5);
            int expected = 0;
            for (int a = 0; a < 3; a++)
                expected += 100 + 2 * 55;
            for (int a = 0; a < 3; a++)
                game.TapSpot(game.AnimalSpot(a));
            var events = Drain(game);
            Assert.AreEqual(3, Count(events, HideSeekEventKind.Found));
            var all = events.Single(e => e.Kind == HideSeekEventKind.AllFound);
            Assert.AreEqual(5 * 55, all.Points, "+5 per whole second left.");
            Assert.AreEqual(5 * 55, game.TimeBonus);
            Assert.AreEqual(HideSeekEventKind.Finished, events.Last().Kind);
            Assert.AreEqual(expected + 5 * 55, game.Score);
            Assert.IsTrue(game.AllFound);
            Assert.IsTrue(game.Finished);
            Assert.AreEqual(3, game.BestStreak);
            Assert.IsFalse(game.MagnifierAvailable, "The game is over.");

            // Over: the clock and the touches stop.
            game.Tick(1f);
            game.TapSpot(FreeSpot(game));
            game.UseWhistle();
            Assert.AreEqual(4.5f, game.Elapsed, 1e-3f);
            Assert.AreEqual(0, game.EventCount);
        }

        [Test]
        public void TimeUp_EndsTheGame_At60_40And45s_75sSlower()
        {
            var cases = new (int challenge, bool slower, float seconds)[] { (-1, false, 60f), (3, false, 40f), (9, false, 45f), (-1, true, 75f), (3, true, 50f) };
            foreach (var row in cases)
            {
                var game = Start(4, row.challenge, slower: row.slower);
                TickTo(game, row.seconds - 0.05, 0.25f);
                Assert.IsFalse(game.Finished, row.challenge + " " + row.slower);
                Drain(game);
                game.Tick(0.1f);
                Assert.IsTrue(game.Finished, row.challenge + " " + row.slower);
                Assert.AreEqual(HideSeekEventKind.Finished, Drain(game).Last().Kind);
                Assert.IsFalse(game.AllFound);
                Assert.AreEqual(0, game.TimeBonus);
            }
        }

        // ---------------- challenges ----------------

        [Test]
        public void Challenge2_GivesNoHintAndNoItem()
        {
            foreach (int c in new[] { HideSeekRules.NoHintChallenge })
            {
                var game = Start(challenge: c);
                TickTo(game, 40, 0.1f);
                var events = Drain(game);
                Assert.AreEqual(0, Count(events, HideSeekEventKind.Hint), "challenge " + (c + 1));
                Assert.IsFalse(Enumerable.Range(0, game.HiddenCount).Any(game.AnimalHinting));
                game.UseMagnifier(0f, 0f, 0f);
                game.UseWhistle();
                Assert.AreEqual(-1, game.MagnifierSpot);
                Assert.IsFalse(game.MagnifierUsed);
                Assert.IsFalse(game.WhistleUsed);
                Assert.AreEqual(0, Count(Drain(game), HideSeekEventKind.Whistle));
                int left = game.SecondsLeft;
                game.TapSpot(game.AnimalSpot(0));
                Assert.AreEqual(100 + 2 * left, Drain(game).First().Points, "Every find is a find before its hint.");
            }
        }

        [Test]
        public void Challenge10_GivesNoHint_ButKeepsItsItems()
        {
            // Doc 01: challenge 10 is « aucun indice » only; challenge 2 alone says « ni loupe ni sifflet ».
            var game = Start(challenge: HideSeekRules.GrandChallenge);
            TickTo(game, 30, 0.1f);
            Assert.AreEqual(0, Count(Drain(game), HideSeekEventKind.Hint));
            Assert.IsTrue(game.ItemsEnabled);
            game.UseWhistle();
            Assert.IsTrue(game.WhistleUsed);
            Assert.AreEqual(game.HiddenCount, Count(Drain(game), HideSeekEventKind.Whistle));
            int left = game.SecondsLeft;
            game.TapSpot(game.AnimalSpot(0));
            var found = Drain(game).First();
            Assert.AreEqual(HideSeekEventKind.FoundAfterHint, found.Kind, "After the whistle a find counts as after its hint.");
            Assert.AreEqual(50 + left, found.Points);
        }

        [Test]
        public void Challenge6_The4thMistakeEndsTheGame()
        {
            var game = Start(challenge: HideSeekRules.OneLookChallenge);
            game.Tick(1f);
            game.TapSpot(game.AnimalSpot(0));
            int empty = FreeSpot(game);
            for (int i = 1; i <= 3; i++)
            {
                game.Tick(1.1f);
                game.TapSpot(empty);
                Assert.AreEqual(i, game.Mistakes);
                Assert.IsFalse(game.Finished, "Mistake " + i);
            }
            var events = Drain(game);
            Assert.AreEqual(3, Count(events, HideSeekEventKind.Mistake));
            Assert.AreEqual(3, Count(events, HideSeekEventKind.Empty));
            Assert.AreEqual(0, Count(events, HideSeekEventKind.TouchesPaused), "1.1 s apart: no pause.");
            int score = game.Score;
            game.Tick(1.1f);
            Drain(game);
            game.TapSpot(game.DecoySpot(0));
            events = Drain(game);
            Assert.AreEqual(HideSeekEventKind.Empty, events[0].Kind);
            Assert.AreEqual(HideSeekEventKind.Mistake, events[1].Kind);
            Assert.AreEqual(HideSeekEventKind.Finished, events[2].Kind, "The 4th mistake (a decoy) ends the game.");
            Assert.AreEqual(4, game.Mistakes);
            Assert.AreEqual(4, game.MistakeLimit);
            Assert.IsTrue(game.Finished);
            Assert.AreEqual(score, game.Score, "Nothing lost.");
        }

        [Test]
        public void Challenge9_AnAnimalMovesEvery15s_NeverIntoADecoyAnOccupiedSpotNorAFoundOne_NeverTheRingedOne()
        {
            for (uint seed = 1; seed <= 25; seed++)
            {
                var game = Start(challenge: HideSeekRules.MovingChallenge, seed: seed);
                Assert.AreEqual(15.0, game.NextMoveSecond, 1e-9);
                game.Tick(1f);
                game.TapSpot(game.AnimalSpot(0));
                int touched = FreeSpot(game);
                game.Tick(1f);
                game.TapSpot(touched);
                Assert.AreEqual(touched, game.LastTapSpot);
                TickTo(game, 13.5);
                // The magnifier rings an animal from 13.5 s to 15.5 s: it never moves while ringed.
                game.UseMagnifier(0f, 0f, 0f);
                int ringed = game.AnimalAt(game.MagnifierSpot);
                TickTo(game, 14.9);
                Assert.AreEqual(0, Count(Drain(game), HideSeekEventKind.Moved), "Not before 15 s.");
                var before = Enumerable.Range(0, game.HiddenCount).Select(game.AnimalSpot).ToArray();
                TickTo(game, 15.05);
                var moved = Drain(game).Where(e => e.Kind == HideSeekEventKind.Moved).ToList();
                Assert.AreEqual(1, moved.Count, "One animal at 15 s: seed " + seed);
                var move = moved[0];
                Assert.AreNotEqual(0, move.Animal, "A found animal never moves.");
                Assert.AreNotEqual(ringed, move.Animal, "Never the ringed one.");
                Assert.AreNotEqual(before[move.Animal], move.Spot, "Another spot.");
                Assert.IsFalse(game.IsDecoy(move.Spot), "Never a decoy.");
                Assert.IsFalse(game.FoundAt(move.Spot), "Never where an animal was found.");
                Assert.IsFalse(before.Contains(move.Spot), "Never an occupied spot.");
                Assert.AreEqual(move.Spot, game.AnimalSpot(move.Animal));
                Assert.AreEqual(move.Animal, game.AnimalAt(move.Spot));
                Assert.AreEqual(-1, game.AnimalAt(before[move.Animal]), "Its old spot is free.");
                Assert.AreEqual(1, game.Moves);

                // Far from the last touch: among the farthest free spots (the jitter is ± 15 %).
                double Distance(int s) => Math.Sqrt(Math.Pow(game.SpotX(s) - game.SpotX(touched), 2) + Math.Pow(game.SpotZ(s) - game.SpotZ(touched), 2));
                double farthest = Enumerable.Range(0, game.SpotCount)
                    .Where(s => s == move.Spot || (!game.IsDecoy(s) && !game.FoundAt(s) && game.AnimalAt(s) < 0 && s != before[move.Animal]))
                    .Max(Distance);
                Assert.GreaterOrEqual(Distance(move.Spot), farthest * 0.75, "seed " + seed);

                // Then at 30 s and 45 s; never at the end.
                TickTo(game, 59.9, 0.1f);
                Assert.AreEqual(2, Count(Drain(game), HideSeekEventKind.Moved), "At 30 s and 45 s.");
                Assert.AreEqual(3, game.Moves);
            }
            var still = Start(5);
            still.Tick(59f);
            Assert.AreEqual(0, still.Moves, "Only challenge 9 moves.");
        }

        [Test]
        public void Challenge9_AHintingAnimal_KeepsHinting_AtItsNewSpot()
        {
            var game = Start(challenge: HideSeekRules.MovingChallenge, seed: 4);
            TickTo(game, 14.95);
            Assert.IsTrue(Enumerable.Range(0, game.HiddenCount).All(game.AnimalHinting), "Every animal hints by 11.5 s.");
            TickTo(game, 15.05);
            var move = Drain(game).Single(e => e.Kind == HideSeekEventKind.Moved);
            Assert.IsTrue(game.AnimalHinting(move.Animal));
            int left = game.SecondsLeft;
            game.TapSpot(move.Spot);
            Assert.AreEqual(50 + left, Drain(game).Single(e => e.Kind == HideSeekEventKind.FoundAfterHint).Points);
        }

        // ---------------- states, pause and events ----------------

        [Test]
        public void Pause_StopsTheClocksAndTheInputs()
        {
            var game = Start(5);
            game.Tick(1f);
            Drain(game);
            game.Pause();
            game.Tick(20f);
            game.TapSpot(game.AnimalSpot(0));
            game.UseMagnifier(0f, 0f, 0f);
            game.UseWhistle();
            Assert.AreEqual(1f, game.Elapsed);
            Assert.AreEqual(0, game.FoundCount);
            Assert.AreEqual(0, game.EventCount);
            Assert.IsFalse(game.AnimalHinting(0), "The hint clock stopped too.");
            Assert.IsFalse(game.MagnifierUsed);
            game.Resume();
            game.Tick(1f);
            game.TapSpot(game.AnimalSpot(0));
            Assert.AreEqual(2f, game.Elapsed, 1e-6f);
            Assert.AreEqual(1, game.FoundCount);
        }

        [Test]
        public void TheEventRing_KeepsTheLast128_UntilDrained()
        {
            var game = Start(10);
            // Three empty touches and their pause every second, hints and rustles, never drained: well over 128 events.
            while (!game.Finished)
            {
                for (int k = 0; k < 3; k++)
                    game.TapSpot(game.DecoySpot(k % game.DecoyCount));
                game.Tick(1.01f);
            }
            Assert.AreEqual(HideSeekController.EventCapacity, game.EventCount);
            Assert.AreEqual(HideSeekEventKind.None, game.Event(-1).Kind);
            Assert.AreEqual(HideSeekEventKind.None, game.Event(game.EventCount).Kind);
            Assert.AreEqual(HideSeekEventKind.Finished, game.Event(game.EventCount - 1).Kind, "The newest last.");
            game.ClearEvents();
            Assert.AreEqual(0, game.EventCount);
        }

        [Test]
        public void ThePlayersNumbers_ForTheHooks()
        {
            var game = Start(3);
            game.Tick(2f);
            game.TapSpot(game.AnimalSpot(0));
            game.TapSpot(FreeSpot(game));
            TickTo(game, 9.6);
            game.TapSpot(game.AnimalSpot(1));
            game.TapSpot(game.AnimalSpot(2));
            Assert.AreEqual(3, game.FoundCount);
            Assert.AreEqual(1, game.FoundBeforeHint);
            Assert.AreEqual(1, game.EmptyTouches);
            Assert.AreEqual(2, game.Streak, "Two in a row since the empty touch.");
            Assert.AreEqual(2, game.BestStreak);
            Assert.IsFalse(game.AllFound);
            Assert.AreEqual(game.Score, game.ChallengeScore);
        }

        [Test]
        public void APerfectGame_Is3Stars_ACasualOne_2_AWeakOne_1()
        {
            var rules = HideSeekRules.Create();
            var setup = HideSeekBalanceTests.Setup(rules, 3, -1, 99);
            var game = new HideSeekController();

            // Perfect: every animal within the first 5 seconds.
            game.Begin(in setup);
            for (int a = 0; a < game.HiddenCount; a++)
            {
                game.Tick(1f);
                game.TapSpot(game.AnimalSpot(a));
            }
            Assert.AreEqual(3, rules.Free.Stars(game.Score), "A perfect game: " + game.Score);
            Assert.IsTrue(game.AllFound);

            int casual = ScriptedSeeker.PlayAlone(game, in setup, SeekerSkill.Casual());
            Assert.AreEqual(2, rules.Free.Stars(casual), "A casual game: " + casual);

            // Weak: one animal at 5 s, two more after their hints at 25 s and 40 s, the rest never found.
            game.Begin(in setup);
            TickTo(game, 5, 0.1f);
            game.TapSpot(game.AnimalSpot(0));
            TickTo(game, 25, 0.1f);
            game.TapSpot(game.AnimalSpot(1));
            TickTo(game, 40, 0.1f);
            game.TapSpot(game.AnimalSpot(2));
            TickTo(game, 60, 0.5f);
            Assert.IsTrue(game.Finished);
            Assert.AreEqual(1, rules.Free.Stars(game.Score), "A weak game: " + game.Score);
        }

        [Test]
        public void Tick_TapSpot_TheItems_AndTheEventDrain_DoNotAllocate()
        {
            var game = Start(challenge: HideSeekRules.MovingChallenge);
            int sum = 0;
            // Warm-up: every path once.
            game.Tick(Frame);
            game.TapSpot(game.AnimalSpot(0));
            game.TapSpot(FreeSpot(game));
            for (int i = 0; i < game.EventCount; i++)
                sum += game.Event(i).Points;
            game.ClearEvents();

            Assert.That(() => game.Tick(Frame), Is.Not.AllocatingGCMemory());
            Assert.That(() => game.TapSpot(game.AnimalSpot(1)), Is.Not.AllocatingGCMemory(), "A find.");
            Assert.That(() => game.TapSpot(FreeSpot(game)), Is.Not.AllocatingGCMemory(), "An empty touch.");
            Assert.That(() => game.UseMagnifier(0f, 0f, 0f), Is.Not.AllocatingGCMemory());
            Assert.That(() => game.UseWhistle(), Is.Not.AllocatingGCMemory());
            Assert.That(() => game.Tick(16f), Is.Not.AllocatingGCMemory(), "Hints, decoy rustles and a move.");
            Assert.That(new TestDelegate(() =>
            {
                for (int i = 0; i < game.EventCount; i++)
                    sum += game.Event(i).Points + (int)game.Event(i).Kind + game.Event(i).Spot;
                game.ClearEvents();
                for (int a = 0; a < game.HiddenCount; a++)
                    sum += game.AnimalSpot(a) + (game.AnimalHinting(a) ? 1 : 0) + (game.AnimalFound(a) ? 1 : 0);
                sum += game.Streak + game.MagnifierSpot + (int)(game.MagnifierLeft * 10f) + (game.TouchesPaused ? 1 : 0);
            }), Is.Not.AllocatingGCMemory(), "The stage's drain and reads.");
            var setup = HideSeekBalanceTests.Setup(HideSeekRules.Create(), 10, -1, 5);
            game.Begin(in setup);
            Assert.That(() => game.Begin(in setup), Is.Not.AllocatingGCMemory(), "Even a new game's layout.");
            Assert.Greater(sum, 0);
        }
    }
}
