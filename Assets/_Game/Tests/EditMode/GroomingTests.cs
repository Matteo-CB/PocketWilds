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
    /// T38 « Toilettage » (doc 01 Contenu, S-ACT-001; doc Mini-jeux, S-ACT-001 and section 6): the rules as data (thresholds,
    /// parameters at d1 / d5 / d8 / d10, the 10 challenges), the deterministic layout, the scoring of passes and the streak,
    /// rebrushes and their kindness windows, debris and burrs, the soap bubbles, the golden brush, the bow phase and its
    /// time bonus, the challenges' special rules, the times scaled with the game's length, the event ring and a game that
    /// never allocates.
    /// </summary>
    public sealed class GroomingTests
    {
        internal static GroomingController Start(int difficulty = 3, int challenge = -1, uint seed = 11, int zones = 40, bool slower = false)
        {
            var game = new GroomingController();
            game.SetSyntheticBody(zones);
            var setup = GroomingBalanceTests.Setup(GroomingRules.Create(), difficulty, challenge, seed, slower);
            game.Begin(in setup);
            return game;
        }

        internal static List<int> Dirty(GroomingController game)
        {
            var list = new List<int>();
            for (int i = 0; i < game.ZoneCount; i++)
                if (game.IsDirty(i))
                    list.Add(i);
            return list;
        }

        internal static int NeverDirty(GroomingController game)
        {
            for (int i = 0; i < game.ZoneCount; i++)
                if (!game.WasDirty(i))
                    return i;
            return -1;
        }

        /// <summary>Brushes a dirty zone until it is clean; returns the points it gave.</summary>
        internal static int Clean(GroomingController game, int zone)
        {
            int before = game.Score;
            for (int i = 0; i < 10 && game.IsDirty(zone); i++)
                game.Brush(zone);
            Assert.IsFalse(game.IsDirty(zone));
            return game.Score - before;
        }

        internal static List<GroomingEvent> Drain(GroomingController game)
        {
            var list = new List<GroomingEvent>();
            for (int i = 0; i < game.EventCount; i++)
                list.Add(game.Event(i));
            game.ClearEvents();
            return list;
        }

        private static int Count(List<GroomingEvent> events, GroomingEventKind kind) => events.Count(e => e.Kind == kind);

        private static int Burrs(GroomingController game)
        {
            int burrs = 0;
            for (int i = 0; i < game.DebrisCount; i++)
                if (game.DebrisIsBurr(i))
                    burrs++;
            return burrs;
        }

        // ---------------- the rules (doc 01, S-ACT-001) ----------------

        /// <summary>
        /// Collision rule: zones measured too thin (an ear, a tail tip) carry no debris, burr, bubble or golden brush; they are
        /// still dirtied and brushed. Without the measure every zone may carry a prop.
        /// </summary>
        [Test]
        public void ThinZones_CarryNoProps_ButStayBrushable()
        {
            var thin = new bool[40];
            for (int i = 0; i < 40; i++)
                thin[i] = i % 4 == 0;
            bool thinDirty = false;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var game = new GroomingController();
                game.SetSyntheticBody(40);
                game.SetPropZones(thin, 40);
                var setup = GroomingBalanceTests.Setup(GroomingRules.Create(), 10, -1, seed);
                game.Begin(in setup);
                for (int d = 0; d < game.DebrisCount; d++)
                    Assert.IsFalse(thin[game.DebrisZone(d)], $"Seed {seed}: debris {d} on a thin zone.");
                for (int b = 0; b < game.BubbleCount; b++)
                    Assert.IsFalse(thin[game.BubbleZone(b)], $"Seed {seed}: bubble {b} on a thin zone.");
                Assert.Greater(game.DebrisCount, 0, "Props still found their places.");
                var plain = new GroomingController();
                plain.SetSyntheticBody(40);
                plain.Begin(in setup);
                Assert.AreEqual(plain.DebrisCount, game.DebrisCount, "The measure moves props, it never removes any.");
                for (int z = 0; z < 40; z++)
                    thinDirty |= thin[z] && game.IsDirty(z);
                // Brushing a thin zone works as anywhere.
                for (int z = 0; z < 40; z++)
                {
                    if (!thin[z] || !game.IsDirty(z))
                        continue;
                    int before = game.ZonesCleaned;
                    while (game.IsDirty(z))
                        game.Brush(z);
                    Assert.AreEqual(before + 1, game.ZonesCleaned);
                    break;
                }
            }
            Assert.IsTrue(thinDirty, "Thin zones are still dirtied.");

            // A new body forgets the previous measure.
            var fresh = new GroomingController();
            fresh.SetSyntheticBody(40);
            fresh.SetPropZones(thin, 40);
            fresh.SetSyntheticBody(40);
            var anySetup = GroomingBalanceTests.Setup(GroomingRules.Create(), 10, -1, 3);
            fresh.Begin(in anySetup);
            bool anyOnThin = false;
            for (uint seed = 1; seed <= 30 && !anyOnThin; seed++)
            {
                fresh.SetSyntheticBody(40);
                var again = GroomingBalanceTests.Setup(GroomingRules.Create(), 10, -1, seed);
                fresh.Begin(in again);
                for (int d = 0; d < fresh.DebrisCount; d++)
                    anyOnThin |= thin[fresh.DebrisZone(d)];
            }
            Assert.IsTrue(anyOnThin, "Without the measure, any zone may carry a prop.");
        }

        /// <summary>A game quit mid-way (still brushing): EndGame lets the next animal's body in.</summary>
        [Test]
        public void EndGame_AfterAQuit_LetsTheNextBodyIn()
        {
            var game = Start(zones: 40);
            game.Tick(5f);
            Assert.AreEqual(GroomingPhase.Brushing, game.Phase);
            game.SetSyntheticBody(30);
            Assert.AreEqual(40, game.ZoneCount, "A body never changes during a game.");
            game.EndGame();
            game.SetSyntheticBody(30);
            Assert.AreEqual(30, game.ZoneCount, "The next animal's zones are taken after the quit.");
        }

        [Test]
        public void TheRules_AreDoc01_ThresholdsParametersAndPassesStep()
        {
            var rules = GroomingRules.Create();
            Assert.AreEqual(ActivityId.Grooming, rules.Activity);
            Assert.AreEqual(200, rules.Free.One);
            Assert.AreEqual(380, rules.Free.Two);
            Assert.AreEqual(520, rules.Free.Three);
            Assert.AreEqual(GroomingRules.ParameterCount, rules.ParameterCount);
            Assert.AreEqual("zones", rules.Parameters[GroomingRules.Zones].Name);
            Assert.AreEqual("passes", rules.Parameters[GroomingRules.Passes].Name);

            int[] zones = { 16, 18, 20, 22, 24, 26, 27, 29, 30, 32 };
            int[] debris = { 4, 5, 6, 7, 8, 9, 10, 10, 11, 12 };
            int[] burrs = { 0, 1, 1, 2, 2, 2, 3, 3, 4, 4 };
            for (int d = 1; d <= 10; d++)
            {
                Assert.AreEqual(zones[d - 1], rules.Value(GroomingRules.Zones, d), 1e-9, "zones d" + d);
                Assert.AreEqual(debris[d - 1], rules.Value(GroomingRules.Debris, d), 1e-9, "debris d" + d);
                Assert.AreEqual(burrs[d - 1], rules.Value(GroomingRules.Burrs, d), 1e-9, "burrs d" + d);
                Assert.AreEqual(d >= 8 ? 3 : 2, rules.Value(GroomingRules.Passes, d), 1e-9, "Passes: 2, then 3 from d8 (a step).");
                Assert.AreEqual(0, rules.Value(GroomingRules.RebrushPenalty, d), "Nothing is ever lost in the free mode.");
                Assert.AreEqual(0, rules.Value(GroomingRules.BowDeadline, d));
            }
            Assert.AreEqual(0.0, rules.Value(GroomingRules.Movement, 1), 1e-9);
            Assert.AreEqual(0.025, rules.Value(GroomingRules.Movement, 3), 1e-9);
            Assert.AreEqual(0.05, rules.Value(GroomingRules.Movement, 5), 1e-9, "5 % of the screen's width.");
            Assert.AreEqual(0.12, rules.Value(GroomingRules.Movement, 10), 1e-9, "12 %.");
            Assert.AreEqual(GroomingRules.ThreeStarTreat, ActivityTestRig.CloverCrunch, "« Objet gagné : 1 Trèfle croquant ».");
        }

        [Test]
        public void TheTenChallenges_AreTheDocTable()
        {
            var rules = GroomingRules.Create();
            var definition = ActivityCatalog.Get(ActivityId.Grooming);
            // stars, zones, debris, burrs, passes, movement, penalty, deadline, seconds
            var table = new (int one, int two, int three, int zones, int debris, int burrs, int passes, double movement, int penalty, int deadline, float seconds)[]
            {
                (150, 260, 360, 16, 4, 0, 2, 0.0, 0, 0, 45f),
                (200, 320, 420, 20, 6, 1, 2, 0.025, 20, 0, 45f),
                (280, 420, 540, 20, 14, 0, 2, 0.025, 0, 0, 45f),
                (200, 300, 400, 20, 6, 1, 2, 0.025, 0, 0, 30f),
                (220, 360, 480, 24, 8, 2, 2, 0.15, 0, 0, 45f),
                (200, 330, 450, 20, 6, 1, 3, 0.025, 0, 0, 45f),
                (300, 480, 620, 32, 8, 2, 2, 0.05, 0, 0, 45f),
                (260, 380, 500, 20, 6, 1, 2, 0.025, 0, 35, 45f),
                (300, 440, 580, 20, 6, 8, 2, 0.025, 0, 0, 45f),
                (400, 580, 720, 36, 12, 3, 3, 0.10, 0, 0, 45f),
            };
            Assert.AreEqual(10, rules.Challenges.Length);
            for (int c = 0; c < table.Length; c++)
            {
                var row = table[c];
                var t = rules.Thresholds(c);
                string name = definition.ChallengeId(c);
                Assert.AreEqual("challenge.grooming." + (c + 1).ToString("00"), name);
                Assert.AreEqual(row.one, t.One, name);
                Assert.AreEqual(row.two, t.Two, name);
                Assert.AreEqual(row.three, t.Three, name);
                Assert.AreEqual(row.zones, rules.Value(GroomingRules.Zones, 3, c), 1e-9, name + " zones");
                Assert.AreEqual(row.debris, rules.Value(GroomingRules.Debris, 3, c), 1e-9, name + " debris");
                Assert.AreEqual(row.burrs, rules.Value(GroomingRules.Burrs, 3, c), 1e-9, name + " burrs");
                Assert.AreEqual(row.passes, rules.Value(GroomingRules.Passes, 3, c), 1e-9, name + " passes");
                Assert.AreEqual(row.movement, rules.Value(GroomingRules.Movement, 3, c), 1e-9, name + " movement");
                Assert.AreEqual(row.penalty, rules.Value(GroomingRules.RebrushPenalty, 3, c), 1e-9, name + " penalty");
                Assert.AreEqual(row.deadline, rules.Value(GroomingRules.BowDeadline, 3, c), 1e-9, name + " deadline");
                Assert.AreEqual(row.seconds, rules.ChallengeSeconds(c, 45f), 1e-6, name + " seconds");
            }
            Assert.AreEqual(6, GroomingRules.HeavyAnimalChallenge, "Challenge 7 « Grand gabarit » features the heaviest animal.");
            Assert.AreEqual(1, rules.Challenges[0].Level);
            Assert.AreEqual(8, rules.Challenges[9].Level);
        }

        [Test]
        public void Begin_ReadsTheParameters_AtD1D5D8D10()
        {
            // A body of 64 zones: nothing is clamped.
            var expected = new (int d, int zones, int items, int burrs, int passes, float movement)[]
            {
                (1, 16, 4, 0, 2, 0f), (5, 24, 10, 2, 2, 0.05f), (8, 29, 13, 3, 3, 0.092f), (10, 32, 16, 4, 3, 0.12f),
            };
            foreach (var row in expected)
            {
                var game = Start(row.d, zones: 64);
                Assert.AreEqual(row.zones, game.DirtyZones, "d" + row.d + " zones");
                Assert.AreEqual(row.zones, Dirty(game).Count);
                Assert.AreEqual(row.items, game.DebrisCount, "d" + row.d + " debris and burrs");
                Assert.AreEqual(row.burrs, Burrs(game), "d" + row.d + " burrs");
                Assert.AreEqual(row.passes, game.PassesPerZone, "d" + row.d + " passes");
                Assert.AreEqual(row.movement, game.Movement, 1e-5f, "d" + row.d + " movement");
                Assert.AreEqual(GroomingPhase.Brushing, game.Phase);
                Assert.AreEqual(45f, game.Seconds);
                Assert.AreEqual(40f, game.BowSecond, 1e-5f);
                Assert.AreEqual(1f, game.Dust(Dirty(game)[0]));
            }
        }

        [Test]
        public void EachChallenge_SetsItsGameUp()
        {
            for (int c = 0; c < 10; c++)
            {
                int body = c == GroomingRules.HeavyAnimalChallenge ? 56 : 40;
                var game = Start(challenge: c, zones: body);
                var rules = GroomingRules.Create();
                int zones = (int)rules.Value(GroomingRules.Zones, 0, c);
                int items = (int)rules.Value(GroomingRules.Debris, 0, c) + (int)rules.Value(GroomingRules.Burrs, 0, c);
                Assert.AreEqual(Math.Min(zones, body), game.DirtyZones, "challenge " + (c + 1));
                Assert.AreEqual(items, game.DebrisCount, "challenge " + (c + 1));
                Assert.AreEqual((int)rules.Value(GroomingRules.Burrs, 0, c), Burrs(game));
                Assert.AreEqual((int)rules.Value(GroomingRules.Passes, 0, c), game.PassesPerZone);
                Assert.AreEqual((float)rules.Value(GroomingRules.Movement, 0, c), game.Movement, 1e-5f);
            }
            Assert.AreEqual(0.15f, Start(challenge: 4).Movement, 1e-6f, "Challenge 5 « Petit agité »: 15 %.");
            Assert.AreEqual(32, Start(challenge: 6, zones: 56).DirtyZones, "Challenge 7 on a HeavyQuadruped's 56 zones.");
            Assert.AreEqual(30, Start(challenge: 9, zones: 30).DirtyZones, "36 zones on a Hopper's 30: clamped.");
        }

        // ---------------- layout ----------------

        [Test]
        public void TheLayout_IsTheSeeds_SpreadInPatches_OneItemPerZone_NeverOnABubbleSpot()
        {
            var a = new GroomingController();
            var b = new GroomingController();
            var rules = GroomingRules.Create();
            int differences = 0;
            string previous = null;
            foreach (int body in new[] { 30, 40, 56 })
            {
                a.SetSyntheticBody(body);
                b.SetSyntheticBody(body);
                for (uint seed = 1; seed <= 60; seed++)
                {
                    foreach (int d in new[] { 1, 5, 10 })
                    {
                        var setup = GroomingBalanceTests.Setup(rules, d, -1, seed * 2654435761u);
                        a.Begin(in setup);
                        b.Begin(in setup);
                        string signature = "";
                        for (int i = 0; i < body; i++)
                        {
                            Assert.AreEqual(a.IsDirty(i), b.IsDirty(i), "Same seed, same dirt.");
                            signature += a.IsDirty(i) ? "1" : "0";
                        }
                        if (d == 5 && previous != null && previous != signature)
                            differences++;
                        if (d == 5)
                            previous = signature;
                        Assert.AreEqual(a.BubbleZone(0), b.BubbleZone(0));
                        Assert.AreEqual(a.BubbleZone(1), b.BubbleZone(1));
                        Assert.AreEqual(a.DebrisCount, b.DebrisCount);

                        // The bubble spots: two dirty zones far apart.
                        Assert.AreNotEqual(a.BubbleZone(0), a.BubbleZone(1));
                        Assert.IsTrue(a.WasDirty(a.BubbleZone(0)) && a.WasDirty(a.BubbleZone(1)));
                        Assert.Greater(Math.Sqrt(ScriptedGroomer.Distance2(a, a.BubbleZone(0), a.BubbleZone(1))), 0.4, "Bubbles far apart.");

                        // One item per zone, never on a bubble spot.
                        var used = new HashSet<int>();
                        for (int k = 0; k < a.DebrisCount; k++)
                        {
                            Assert.AreEqual(a.DebrisZone(k), b.DebrisZone(k));
                            Assert.AreEqual(a.DebrisIsBurr(k), b.DebrisIsBurr(k));
                            Assert.IsTrue(used.Add(a.DebrisZone(k)), "Never two debris on a zone.");
                            Assert.AreNotEqual(a.BubbleZone(0), a.DebrisZone(k));
                            Assert.AreNotEqual(a.BubbleZone(1), a.DebrisZone(k));
                            Assert.IsTrue(a.DebrisActive(k));
                            Assert.AreEqual(0, a.DebrisTaps(k));
                        }

                        // Spread over the body (the dirt reaches over 60 % of its length), in patches (no lone dirty zone).
                        float min = float.MaxValue, max = float.MinValue;
                        for (int i = 0; i < body; i++)
                        {
                            if (!a.WasDirty(i))
                                continue;
                            min = Math.Min(min, a.ZoneX(i));
                            max = Math.Max(max, a.ZoneX(i));
                            Assert.IsTrue(HasDirtyNeighbor(a, i, 6), "A dirty zone is part of a patch: zone " + i + " seed " + seed);
                        }
                        Assert.Greater(max - min, 0.6f, "Never one clump: seed " + seed + " body " + body + " d" + d);
                    }
                }
            }
            Assert.Greater(differences, 150, "Each seed its own layout.");
        }

        private static bool HasDirtyNeighbor(GroomingController game, int zone, int among)
        {
            for (int j = 0; j < game.ZoneCount; j++)
            {
                if (j == zone || !game.WasDirty(j))
                    continue;
                float d = ScriptedGroomer.Distance2(game, zone, j);
                int closer = 0;
                for (int k = 0; k < game.ZoneCount; k++)
                    if (k != zone && ScriptedGroomer.Distance2(game, zone, k) < d)
                        closer++;
                if (closer < among)
                    return true;
            }
            return false;
        }

        [Test]
        public void TheBody_IsCopied_Clamped_AndKeptDuringAGame()
        {
            var game = new GroomingController();
            Assert.AreEqual(GroomingController.DefaultZoneCount, game.ZoneCount, "40 zones without a measured body.");
            var x = new float[30];
            var y = new float[30];
            var z = new float[30];
            for (int i = 0; i < 30; i++)
            {
                x[i] = i * 0.1f;
                y[i] = (i % 5) * 0.1f;
                z[i] = 0f;
            }
            game.SetBody(30, x, y, z);
            Assert.AreEqual(30, game.ZoneCount);
            x[3] = 99f;
            Assert.AreEqual(0.3f, game.ZoneX(3), 1e-6f, "The arrays are copied.");
            game.Begin(GroomingBalanceTests.Setup(GroomingRules.Create(), 10, -1, 5));
            Assert.AreEqual(30, game.DirtyZones, "32 zones on a body of 30: clamped.");
            game.SetBody(10, x, y, z);
            game.SetSyntheticBody(12);
            Assert.AreEqual(30, game.ZoneCount, "A body never changes during a game.");

            var big = new GroomingController();
            var many = new float[80];
            big.SetBody(80, many, many, many);
            Assert.AreEqual(GroomingController.MaxZones, big.ZoneCount, "64 zones at most.");
            big.SetBody(0, many, many, many);
            Assert.AreEqual(GroomingController.DefaultZoneCount, big.ZoneCount);
        }

        // ---------------- scoring ----------------

        [Test]
        public void APass_ThenTheSecond_CleansAZone_TheStreakGrowsBy01_UpTo2()
        {
            var game = Start(3);
            var dirty = Dirty(game);
            int zone = dirty[0];
            game.Brush(zone);
            var events = Drain(game);
            Assert.AreEqual(GroomingEventKind.ZonePass, events.Single().Kind);
            Assert.AreEqual(0.5f, game.Dust(zone), 1e-6f, "One pass of two.");
            Assert.AreEqual(0, game.Score);
            game.Brush(zone);
            events = Drain(game);
            Assert.AreEqual(GroomingEventKind.ZoneCleaned, events.Single().Kind);
            Assert.AreEqual(10, events[0].Points, "+10 × 1.0");
            Assert.AreEqual(1, events[0].Streak);
            Assert.AreEqual(0f, game.Dust(zone));
            Assert.AreEqual(1.1f, game.StreakMultiplier, 1e-6f);

            int expected = 10;
            for (int k = 1; k < 13; k++)
            {
                int points = Clean(game, dirty[k]);
                Assert.AreEqual(10 + Math.Min(k, 10), points, "Zone " + (k + 1) + " in a row: × " + (1 + 0.1 * Math.Min(k, 10)));
                expected += points;
            }
            Assert.AreEqual(expected, game.Score);
            Assert.AreEqual(13, game.Streak);
            Assert.AreEqual(2f, game.StreakMultiplier, 1e-6f, "× 2.0 at most.");
            Assert.AreEqual(13, game.ZonesCleaned);
            Assert.AreEqual(13, game.BestStreak);
        }

        [Test]
        public void ARebrush_Giggles_ResetsTheStreak_LosesNothing_OncePerStroke_AndNotOnAZoneJustCleaned()
        {
            var game = Start(3);
            var dirty = Dirty(game);
            Clean(game, dirty[0]);
            Clean(game, dirty[1]);
            Drain(game);
            int score = game.Score;

            game.Brush(dirty[1]);
            Assert.AreEqual(0, game.EventCount, "The finger is still on the zone just cleaned: no rebrush.");
            Assert.AreEqual(2, game.Streak);

            game.Tick(0.55f);
            game.Brush(dirty[0]);
            var events = Drain(game);
            Assert.AreEqual(GroomingEventKind.Rebrush, events.Single().Kind, "Past half a second it is a rebrush: a giggle.");
            Assert.AreEqual(0, events[0].Points, "No point lost.");
            Assert.AreEqual(0, events[0].Streak);
            Assert.AreEqual(0, game.Streak);
            Assert.AreEqual(1f, game.StreakMultiplier);
            Assert.AreEqual(score, game.Score);
            Assert.AreEqual(1, game.Rebrushes);

            int never = NeverDirty(game);
            Assert.GreaterOrEqual(never, 0);
            game.Tick(0.3f);
            game.Brush(never);
            Assert.AreEqual(0, game.EventCount, "One stroke over clean fur gives one giggle (0.6 s).");
            Assert.AreEqual(1, game.Rebrushes);
            game.Tick(0.35f);
            game.Brush(never);
            Assert.AreEqual(GroomingEventKind.Rebrush, Drain(game).Single().Kind, "A zone never dirty is clean fur too.");
            Assert.AreEqual(2, game.Rebrushes);
            Assert.AreEqual(score, game.Score);

            // The streak starts again from × 1.0.
            game.Tick(1f);
            Assert.AreEqual(10, Clean(game, dirty[2]));
        }

        [Test]
        public void Challenge2_EachRebrushTakes20Points_NeverUnder0()
        {
            var game = Start(challenge: GroomingRules.GentleChallenge);
            int never = NeverDirty(game);
            game.Brush(never);
            Assert.AreEqual(0, game.Score, "Never under 0.");
            Assert.AreEqual(0, Drain(game).Single().Points);
            var dirty = Dirty(game);
            game.Tick(1f);
            Clean(game, dirty[0]);
            Clean(game, dirty[1]);
            Clean(game, dirty[2]);
            Assert.AreEqual(33, game.Score);
            game.Tick(1f);
            Drain(game);
            game.Brush(never);
            Assert.AreEqual(13, game.Score);
            Assert.AreEqual(-20, Drain(game).Single().Points);
            game.Tick(1f);
            game.Brush(never);
            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(-13, Drain(game).Single().Points);
            Assert.AreEqual(3, game.Rebrushes);
        }

        [Test]
        public void ADebris_OneTapPlus15_ABurr_TwoTapsPlus25_AGoneOne_Nothing()
        {
            var game = Start(5);
            int leaf = -1, burr = -1;
            for (int i = 0; i < game.DebrisCount; i++)
            {
                if (game.DebrisIsBurr(i) && burr < 0)
                    burr = i;
                if (!game.DebrisIsBurr(i) && leaf < 0)
                    leaf = i;
            }
            Assert.GreaterOrEqual(leaf, 0);
            Assert.GreaterOrEqual(burr, 0, "d5: 2 burrs.");
            Clean(game, Dirty(game)[0]);
            Drain(game);
            int score = game.Score;

            game.TapDebris(leaf);
            var events = Drain(game);
            Assert.AreEqual(GroomingEventKind.DebrisRemoved, events.Single().Kind);
            Assert.AreEqual(15, events[0].Points);
            Assert.AreEqual(leaf, events[0].Index);
            Assert.AreEqual(1, events[0].Streak, "Debris neither break nor grow the streak.");
            Assert.IsFalse(game.DebrisActive(leaf));
            Assert.AreEqual(score + 15, game.Score);
            game.TapDebris(leaf);
            Assert.AreEqual(0, game.EventCount, "A debris taken off: nothing.");
            Assert.AreEqual(score + 15, game.Score);

            game.TapDebris(burr);
            events = Drain(game);
            Assert.AreEqual(GroomingEventKind.BurrLoosened, events.Single().Kind);
            Assert.AreEqual(0, events[0].Points);
            Assert.IsTrue(game.DebrisActive(burr));
            Assert.AreEqual(1, game.DebrisTaps(burr));
            Assert.AreEqual(score + 15, game.Score);
            game.TapDebris(burr);
            events = Drain(game);
            Assert.AreEqual(GroomingEventKind.BurrRemoved, events.Single().Kind);
            Assert.AreEqual(25, events[0].Points);
            Assert.IsFalse(game.DebrisActive(burr));
            Assert.AreEqual(score + 40, game.Score);
            game.TapDebris(burr);
            game.TapDebris(-1);
            game.TapDebris(999);
            Assert.AreEqual(0, game.EventCount);
            Assert.AreEqual(2, game.DebrisRemoved, "Debris and burrs both count (WISH-021).");
            Assert.AreEqual(game.DebrisCount - 2, game.DebrisLeft);
        }

        // ---------------- soap bubbles ----------------

        [Test]
        public void TheBubbles_ComeAt15And30s_ATapCleansThe3NearestDirtyZones_WithTheStreak()
        {
            var game = Start(3);
            Assert.AreEqual(2, game.BubbleCount);
            Assert.AreEqual(15f, game.BubbleSecond(0));
            Assert.AreEqual(30f, game.BubbleSecond(1));
            var dirty = Dirty(game);
            int bubbleZone = game.BubbleZone(0);
            // Two zones in a row first, far from the bubble.
            var far = dirty.OrderByDescending(z => ScriptedGroomer.Distance2(game, bubbleZone, z)).Take(2).ToList();
            Clean(game, far[0]);
            Clean(game, far[1]);
            game.Tick(14.9f);
            Assert.IsFalse(game.BubbleActive(0));
            Drain(game);
            game.TapBubble(0);
            Assert.AreEqual(0, game.EventCount, "No bubble yet.");
            game.Tick(0.2f);
            Assert.IsTrue(game.BubbleActive(0));
            Assert.IsFalse(game.BubbleActive(1));
            var spawned = Drain(game);
            Assert.AreEqual(GroomingEventKind.BubbleSpawned, spawned.Single().Kind);
            Assert.AreEqual(0, spawned[0].Index);

            var nearest = Dirty(game).OrderBy(z => ScriptedGroomer.Distance2(game, bubbleZone, z)).Take(3).ToList();
            int score = game.Score;
            game.TapBubble(0);
            var events = Drain(game);
            Assert.AreEqual(GroomingEventKind.BubblePopped, events[0].Kind);
            Assert.AreEqual(12 + 13 + 14, events[0].Points, "Each zone with the streak: × 1.2, 1.3, 1.4.");
            Assert.AreEqual(5, events[0].Streak);
            Assert.AreEqual(3, Count(events, GroomingEventKind.ZoneCleaned));
            foreach (int zone in nearest)
                Assert.IsFalse(game.IsDirty(zone), "One of the 3 nearest dirty zones.");
            Assert.AreEqual(score + 39, game.Score);
            Assert.AreEqual(5, game.ZonesCleaned);
            Assert.IsFalse(game.BubbleActive(0));
            Assert.AreEqual(1, game.BubblesPopped);

            game.Tick(15f);
            Assert.IsTrue(game.BubbleActive(1), "The second bubble at 30 s.");
            game.Tick(10f);
            Assert.AreEqual(GroomingPhase.Bow, game.Phase);
            Assert.IsFalse(game.BubbleActive(1), "The bow phase sends a bubble away.");
            game.TapBubble(1);
            Assert.AreEqual(5, game.ZonesCleaned);
        }

        // ---------------- golden brush ----------------

        [Test]
        public void TheGoldenBrush_At20sWithAStreakOf15_DoublesThePassesFor5s_Once()
        {
            var game = Start(3);
            var dirty = Dirty(game);
            game.Tick(20.5f);
            Assert.AreEqual(-1, game.GoldenBrushZone, "× 1.0 at 20 s: no golden brush yet.");
            for (int k = 0; k < 4; k++)
                Clean(game, dirty[k]);
            game.Tick(0.1f);
            Assert.AreEqual(-1, game.GoldenBrushZone, "× 1.4: not yet.");
            Clean(game, dirty[4]);
            Drain(game);
            game.Tick(0.1f);
            int zone = game.GoldenBrushZone;
            Assert.GreaterOrEqual(zone, 0, "× 1.5 after 20 s: the golden brush comes.");
            Assert.IsTrue(game.IsDirty(zone), "On a dirty zone.");
            var offered = Drain(game);
            Assert.AreEqual(GroomingEventKind.GoldenBrushOffered, offered.Single().Kind);
            Assert.AreEqual(zone, offered[0].Index);
            Assert.IsFalse(game.GoldenBrushActive);

            game.TakeGoldenBrush();
            Assert.IsTrue(game.GoldenBrushActive);
            Assert.AreEqual(-1, game.GoldenBrushZone);
            Assert.AreEqual(5f, game.GoldenBrushLeft, 1e-5f);
            Assert.AreEqual(GroomingEventKind.GoldenBrushStart, Drain(game).Single().Kind);
            int next = Dirty(game)[0];
            game.Brush(next);
            Assert.IsFalse(game.IsDirty(next), "Each pass counts double: one pass cleans a 2-pass zone.");
            game.Tick(4.8f);
            Assert.IsTrue(game.GoldenBrushActive);
            Drain(game);
            game.Tick(0.3f);
            Assert.IsFalse(game.GoldenBrushActive);
            Assert.AreEqual(GroomingEventKind.GoldenBrushEnd, Drain(game).Single().Kind);
            int after = Dirty(game)[0];
            game.Brush(after);
            Assert.IsTrue(game.IsDirty(after), "Back to single passes.");
            Assert.IsTrue(game.GoldenBrushTaken);

            game.Brush(after);
            for (int k = 0; k < 6; k++)
                Clean(game, Dirty(game)[0]);
            game.Tick(0.1f);
            Assert.AreEqual(-1, game.GoldenBrushZone, "Once a game.");
        }

        [Test]
        public void AStreakReachedBefore20s_GetsTheGoldenBrushAt20s_ItLeavesAtTheBowPhase()
        {
            var game = Start(3);
            var dirty = Dirty(game);
            for (int k = 0; k < 6; k++)
                Clean(game, dirty[k]);
            game.Tick(19.9f);
            Assert.AreEqual(-1, game.GoldenBrushZone);
            game.Tick(0.15f);
            Assert.GreaterOrEqual(game.GoldenBrushZone, 0, "At 20 s.");
            game.Tick(20f);
            Assert.AreEqual(GroomingPhase.Bow, game.Phase);
            Assert.AreEqual(-1, game.GoldenBrushZone, "Not taken before the bows: it leaves.");
            game.TakeGoldenBrush();
            Assert.IsFalse(game.GoldenBrushActive);
        }

        // ---------------- the bow ----------------

        [Test]
        public void AllClean_BringsTheBows_ABowGives50AndTheSecondsLeft_AndEndsTheGame()
        {
            var game = Start(3);
            int expected = 0;
            foreach (int zone in Dirty(game))
                expected += Clean(game, zone);
            Assert.AreEqual(345, expected, "20 zones in a row: 10 to 20 points each.");
            var events = Drain(game);
            Assert.AreEqual(1, Count(events, GroomingEventKind.AllClean));
            Assert.AreEqual(GroomingEventKind.BowPhase, events[events.Count - 1].Kind);
            Assert.AreEqual(GroomingPhase.Bow, game.Phase);
            Assert.IsFalse(game.Finished);

            // The leaves never hold the bows back, and can still be taken.
            Assert.Greater(game.DebrisLeft, 0);
            game.TapDebris(0);
            Assert.AreEqual(game.DebrisIsBurr(0), game.DebrisActive(0), "A leaf goes at once, a burr loosens.");
            game.Tick(10.4f);
            Drain(game);
            int score = game.Score;
            game.PlaceBow(GroomingBow.Cream);
            events = Drain(game);
            Assert.AreEqual(GroomingEventKind.BowPlaced, events[0].Kind);
            Assert.AreEqual((int)GroomingBow.Cream, events[0].Index);
            Assert.AreEqual(50, events[0].Points);
            Assert.AreEqual(GroomingEventKind.Finished, events[1].Kind);
            Assert.AreEqual(34, events[1].Index, "45 − 10.4 = 34.6: 34 whole seconds.");
            Assert.AreEqual(170, events[1].Points, "+5 a second.");
            Assert.AreEqual(score + 50 + 170, game.Score);
            Assert.IsTrue(game.Finished);
            Assert.AreEqual(GroomingPhase.Done, game.Phase);
            Assert.IsTrue(game.BowPlaced);
            Assert.AreEqual(GroomingBow.Cream, game.Bow);
            Assert.AreEqual(170, game.TimeBonus);

            game.Brush(0);
            game.TapDebris(1);
            game.PlaceBow(GroomingBow.Pink);
            game.Tick(1f);
            Assert.AreEqual(0, game.EventCount, "The game is over.");
        }

        [Test]
        public void At40s_TheBowsCome_BrushingGoesOn_TheBowGives50WithoutTimeBonus()
        {
            var game = Start(3);
            game.PlaceBow(GroomingBow.Pink);
            Assert.IsFalse(game.BowPlaced, "No bow while brushing.");
            game.Tick(39.9f);
            Assert.AreEqual(GroomingPhase.Brushing, game.Phase);
            game.Tick(0.1f);
            Assert.AreEqual(GroomingPhase.Bow, game.Phase, "At 40 s.");
            var zone = Dirty(game)[0];
            game.Brush(zone);
            Assert.AreEqual(0.5f, game.Dust(zone), 1e-6f, "The coat can still be brushed while the bows wait.");
            game.Tick(2f);
            game.PlaceBow(GroomingBow.Pink);
            Assert.AreEqual(50, game.Score);
            Assert.AreEqual(0, game.TimeBonus, "Not all clean: no time bonus.");
            Assert.IsTrue(game.Finished);
        }

        [Test]
        public void At45s_TheGameEnds_WithoutABow()
        {
            var game = Start(3);
            Clean(game, Dirty(game)[0]);
            game.Tick(44.9f);
            Assert.IsFalse(game.Finished);
            Drain(game);
            game.Tick(0.2f);
            Assert.IsTrue(game.Finished);
            Assert.IsFalse(game.BowPlaced);
            Assert.AreEqual(10, game.Score);
            Assert.AreEqual(GroomingEventKind.Finished, Drain(game).Last().Kind);
        }

        [Test]
        public void Challenge8_ABowAfter35s_GivesNoBowPoints()
        {
            var early = Start(challenge: GroomingRules.LateBowChallenge);
            foreach (int zone in Dirty(early))
                Clean(early, zone);
            early.Tick(34.5f);
            int score = early.Score;
            early.PlaceBow(GroomingBow.Pink);
            Assert.AreEqual(score + 50 + 50, early.Score, "Before 35 s: +50, and 10 whole seconds left.");

            var late = Start(challenge: GroomingRules.LateBowChallenge);
            foreach (int zone in Dirty(late))
                Clean(late, zone);
            late.Tick(36f);
            score = late.Score;
            late.PlaceBow(GroomingBow.Pink);
            Assert.AreEqual(0, late.BowPoints, "After 35 s: no bow points.");
            Assert.AreEqual(score + 45, late.Score, "The seconds left still count.");
        }

        [Test]
        public void Challenge4_Lasts30s_ItsTimesScale_AndSoDoesTheSlowerMode()
        {
            var quick = Start(challenge: GroomingRules.QuickChallenge);
            Assert.AreEqual(30f, quick.Seconds);
            Assert.AreEqual(25f, quick.BowSecond, 1e-5f, "The bows 5 s before the end.");
            Assert.AreEqual(10f, quick.BubbleSecond(0), 1e-5f);
            Assert.AreEqual(20f, quick.BubbleSecond(1), 1e-5f);
            quick.Tick(24.9f);
            Assert.AreEqual(GroomingPhase.Brushing, quick.Phase);
            quick.Tick(0.2f);
            Assert.AreEqual(GroomingPhase.Bow, quick.Phase);
            quick.Tick(5f);
            Assert.IsTrue(quick.Finished);

            var slow = Start(3, slower: true);
            Assert.AreEqual(56.25f, slow.Seconds, 1e-4f, "« Activités plus lentes »: × 1.25.");
            Assert.AreEqual(50f, slow.BowSecond, 1e-4f, "6.25 s to pick a bow.");
            Assert.AreEqual(18.75f, slow.BubbleSecond(0), 1e-4f);
            Assert.AreEqual(0.025f * 0.8f, slow.Movement, 1e-6f, "Speeds × 0.8.");
        }

        // ---------------- states, pause and events ----------------

        [Test]
        public void Pause_StopsTheClockAndTheInputs()
        {
            var game = Start(3);
            int zone = Dirty(game)[0];
            game.Pause();
            game.Tick(20f);
            game.Brush(zone);
            game.TapDebris(0);
            Assert.AreEqual(0f, game.Elapsed);
            Assert.AreEqual(1f, game.Dust(zone));
            Assert.AreEqual(0, game.EventCount);
            game.Resume();
            game.Tick(1f);
            game.Brush(zone);
            Assert.AreEqual(1f, game.Elapsed, 1e-6f);
            Assert.AreEqual(0.5f, game.Dust(zone));
        }

        [Test]
        public void TheEventRing_KeepsTheLast128_UntilDrained()
        {
            // Challenge 10 on 64 zones: 36 zones of 3 passes, 15 debris and burrs: more than 128 events.
            var game = Start(challenge: 9, zones: 64);
            foreach (int zone in Dirty(game))
                Clean(game, zone);
            for (int i = 0; i < game.DebrisCount; i++)
            {
                game.TapDebris(i);
                game.TapDebris(i);
            }
            game.PlaceBow(GroomingBow.SkyBlue);
            Assert.AreEqual(GroomingController.EventCapacity, game.EventCount);
            Assert.AreEqual(GroomingEventKind.Finished, game.Event(game.EventCount - 1).Kind, "The newest last.");
            Assert.AreEqual(GroomingEventKind.None, game.Event(-1).Kind);
            Assert.AreEqual(GroomingEventKind.None, game.Event(game.EventCount).Kind);
            game.ClearEvents();
            Assert.AreEqual(0, game.EventCount);
        }

        [Test]
        public void ThePlayersNumbers_ForTheHooks()
        {
            var game = Start(5);
            var dirty = Dirty(game);
            Clean(game, dirty[0]);
            game.Tick(1f);
            game.Brush(dirty[0]);
            for (int i = 0; i < game.DebrisCount; i++)
            {
                game.TapDebris(i);
                game.TapDebris(i);
            }
            Assert.AreEqual(1, game.ZonesCleaned);
            Assert.AreEqual(1, game.Rebrushes);
            Assert.AreEqual(game.DebrisCount, game.DebrisRemoved, "Debris and burrs.");
            Assert.AreEqual(game.Score, game.ChallengeScore);
            Assert.IsFalse(game.BowPlaced);
        }

        [Test]
        public void APerfectGame_Is3Stars_AnAverageOne_2_AWeakOne_1()
        {
            var rules = GroomingRules.Create();
            var setup = GroomingBalanceTests.Setup(rules, 3, -1, 99);
            var game = new GroomingController();
            int perfect = ScriptedGroomer.PlayAlone(game, in setup, GroomerSkill.Strong());
            Assert.AreEqual(3, rules.Free.Stars(perfect), "A perfect game: " + perfect);
            Assert.IsTrue(game.BowPlaced);
            Assert.Greater(game.TimeBonus, 0);
            int average = ScriptedGroomer.PlayAlone(game, in setup, GroomerSkill.Casual());
            Assert.AreEqual(2, rules.Free.Stars(average), "An average game: " + average);

            // A weak game: three quarters of the zones with a slip every 4, one leaf, the bow at 40 s.
            game.Begin(in setup);
            var dirty = Dirty(game);
            for (int k = 0; k < dirty.Count * 3 / 4; k++)
            {
                Clean(game, dirty[k]);
                if (k % 4 == 3)
                {
                    game.Tick(0.7f);
                    game.Brush(NeverDirty(game));
                }
            }
            game.TapDebris(0);
            game.TapDebris(0);
            game.Tick(40f);
            game.PlaceBow(GroomingBow.Pink);
            Assert.AreEqual(1, rules.Free.Stars(game.Score), "A weak game: " + game.Score);
        }

        [Test]
        public void Tick_Brush_TapDebris_AndTheEventDrain_DoNotAllocate()
        {
            var game = Start(5);
            var dirty = Dirty(game);
            int never = NeverDirty(game);
            int sum = 0;
            // Warm-up: every path once.
            game.Tick(0.016f);
            game.Brush(dirty[0]);
            game.Brush(dirty[0]);
            game.Brush(never);
            game.TapDebris(0);
            for (int i = 0; i < game.EventCount; i++)
                sum += game.Event(i).Points;
            game.ClearEvents();

            Assert.That(() => game.Tick(0.016f), Is.Not.AllocatingGCMemory());
            Assert.That(() => game.Brush(dirty[1]), Is.Not.AllocatingGCMemory(), "A pass.");
            Assert.That(() => game.Brush(dirty[1]), Is.Not.AllocatingGCMemory(), "A zone cleaned.");
            Assert.That(() => game.Brush(never), Is.Not.AllocatingGCMemory(), "A rebrush.");
            Assert.That(() => game.TapDebris(1), Is.Not.AllocatingGCMemory());
            Assert.That(() => game.TapBubble(0), Is.Not.AllocatingGCMemory());
            Assert.That(new TestDelegate(() =>
            {
                for (int i = 0; i < game.EventCount; i++)
                    sum += game.Event(i).Points + (int)game.Event(i).Kind;
                game.ClearEvents();
                sum += (int)(game.Dust(dirty[2]) * 10f) + game.Streak + (game.IsDirty(dirty[2]) ? 1 : 0);
            }), Is.Not.AllocatingGCMemory(), "The stage's drain and reads.");
            Assert.Greater(sum, 0);
        }
    }
}
