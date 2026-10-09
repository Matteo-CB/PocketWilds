using System;
using NUnit.Framework;
using PocketWilds.Simulation;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// How a scripted player seeks (T39 balance checks): seconds to spot an animal that gives no hint (at 30 % visible,
    /// longer as less shows), seconds to notice a hint, a wiggle or the magnifier's ring and touch it, the touch itself, how
    /// often it touches an empty spot, when it reaches for the items, and how much the night and identical animals slow it.
    /// </summary>
    internal sealed class SeekerSkill
    {
        public string Name;

        /// <summary>Seconds to spot an animal giving no hint at 30 % visible; × (0.30 / visible)^<see cref="SpotExponent"/>.</summary>
        public float SpotSeconds;

        public float SpotExponent = 0.52f;

        /// <summary>Seconds to notice a hint (or a wiggle) and touch its spot; half of it for the magnifier's ring.</summary>
        public float HintReaction;

        /// <summary>A touch's own time (nothing else meanwhile).</summary>
        public float TapSeconds;

        /// <summary>An empty touch after this many finds (a decoy when there is one; 0: never).</summary>
        public int MistakeEvery;

        /// <summary>The magnifier, then the whistle, after this long without a find nor a hint (0: never).</summary>
        public float StuckSeconds;

        /// <summary>Share of the whistle's wiggles noticed (the others are found by looking, after-hint points all the same).</summary>
        public float WhistleNotice = 1f;

        /// <summary>The night's lit circle slows the spotting and the hints.</summary>
        public float NightFactor = 1.5f;

        /// <summary>Six identical animals: a little slower to tell apart.</summary>
        public float SameSpeciesFactor = 1.1f;

        /// <summary>
        /// A strong player: an animal every 2 s at 30 % visible (4 s at 8 %), a hint taken in 0.9 s, a 0.3 s touch, never an
        /// empty touch, the items after 7 s stuck, every wiggle noticed.
        /// </summary>
        public static SeekerSkill Strong() => new SeekerSkill
        {
            Name = "strong", SpotSeconds = 2f, HintReaction = 0.9f, TapSeconds = 0.3f, StuckSeconds = 7f,
        };

        /// <summary>
        /// A casual player: an animal every 5 s at 30 % visible (10 s at 8 %), 2.5 s to notice a hint and touch it, a 0.6 s
        /// touch, an empty touch (a rustling decoy first) after every 2 finds, the items after 9 s stuck, half the wiggles.
        /// </summary>
        public static SeekerSkill Casual() => new SeekerSkill
        {
            Name = "casual", SpotSeconds = 5f, HintReaction = 2.5f, TapSeconds = 0.6f, MistakeEvery = 2, StuckSeconds = 9f,
            WhistleNotice = 0.5f,
        };

        /// <summary>Seconds to spot an animal giving no hint in this game.</summary>
        public double Spot(HideSeekController game)
        {
            double visible = Math.Max(0.01, game.Visibility);
            double seconds = SpotSeconds * Math.Pow(0.30 / visible, SpotExponent);
            if (game.NightSpotlight > 0f)
                seconds *= NightFactor;
            if (game.SameSpecies)
                seconds *= SameSpeciesFactor;
            return seconds;
        }

        public double Reaction(HideSeekController game) => HintReaction * (game.NightSpotlight > 0f ? NightFactor : 1f);
    }

    /// <summary>
    /// Plays a whole hide-and-seek game like a person would, through a tick callback (the runner, or the controller alone): it
    /// acts only when its last touch is over; takes the magnifier's ring, then the animals it noticed (hints, wiggles), each
    /// after its reaction time; slips an empty touch now and then; otherwise finds the next animal by looking (a search that
    /// starts again when that animal moves away); reaches for the items when stuck; stops when the game ends.
    /// </summary>
    internal static class ScriptedSeeker
    {
        public const float Step = 1f / 30f;

        public static void Play(HideSeekController game, SeekerSkill skill, Action<float> tick, Func<bool> running)
        {
            int n = game.HiddenCount;
            var lastSpot = new int[HideSeekController.MaxSpots];
            var noticed = new double[HideSeekController.MaxSpots];
            var whistled = new bool[HideSeekController.MaxSpots];
            for (int a = 0; a < n; a++)
            {
                lastSpot[a] = game.AnimalSpot(a);
                noticed[a] = double.NaN;
            }
            double busy = 0.0, searchStart = 0.0, lastFind = 0.0, lastAction = double.NegativeInfinity, magnifierAt = 0.0;
            int target = -1, findsSinceMistake = 0, found = 0;
            int guard = 0;
            while (running() && guard++ < 100000)
            {
                tick(Step);
                if (!running() || game.Finished)
                    break;
                double now = game.Elapsed;
                if (game.FoundCount != found)
                {
                    findsSinceMistake += game.FoundCount - found;
                    found = game.FoundCount;
                    lastFind = now;
                }
                for (int a = 0; a < n; a++)
                {
                    int spot = game.AnimalSpot(a);
                    if (game.AnimalFound(a) || spot == lastSpot[a])
                        continue;
                    // Moved (challenge 9): the search for it starts again; a hint or a wiggle must be noticed again.
                    lastSpot[a] = spot;
                    if (a == target)
                        searchStart = now;
                    if (!double.IsNaN(noticed[a]))
                        noticed[a] = now;
                }
                for (int a = 0; a < n; a++)
                    if (game.AnimalHinting(a) && double.IsNaN(noticed[a]))
                        noticed[a] = Math.Max(now - Step, game.HintSecond(a));
                if (now < busy)
                    continue;

                double reaction = skill.Reaction(game);
                if (game.MagnifierSpot >= 0 && now - magnifierAt >= reaction * 0.5)
                {
                    Tap(game, game.MagnifierSpot, skill, now, ref busy, ref lastAction);
                    continue;
                }
                int known = -1;
                double knownAt = double.MaxValue;
                for (int a = 0; a < n; a++)
                {
                    if (game.AnimalFound(a) || double.IsNaN(noticed[a]))
                        continue;
                    if (noticed[a] < knownAt)
                    {
                        knownAt = noticed[a];
                        known = a;
                    }
                }
                if (known >= 0 && now - Math.Max(knownAt, lastAction) >= reaction)
                {
                    Tap(game, game.AnimalSpot(known), skill, now, ref busy, ref lastAction);
                    continue;
                }
                if (skill.MistakeEvery > 0 && findsSinceMistake >= skill.MistakeEvery)
                {
                    findsSinceMistake = 0;
                    int empty = EmptySpot(game);
                    if (empty >= 0)
                    {
                        Tap(game, empty, skill, now, ref busy, ref lastAction);
                        continue;
                    }
                }
                int next = -1;
                for (int a = 0; a < n && next < 0; a++)
                    if (!game.AnimalFound(a) && double.IsNaN(noticed[a]))
                        next = a;
                target = next;
                if (next >= 0 && now - searchStart >= skill.Spot(game))
                {
                    Tap(game, game.AnimalSpot(next), skill, now, ref busy, ref lastAction);
                    searchStart = now;
                    continue;
                }
                if (known < 0 && skill.StuckSeconds > 0f && game.ItemsEnabled && now - lastFind >= skill.StuckSeconds)
                {
                    if (game.MagnifierAvailable)
                    {
                        game.UseMagnifier(0f, 0f, 0f);
                        magnifierAt = now;
                        lastFind = now;
                        busy = now + skill.TapSeconds;
                    }
                    else if (game.WhistleAvailable)
                    {
                        game.UseWhistle();
                        int left = 0;
                        for (int a = 0; a < n; a++)
                            if (!game.AnimalFound(a))
                                left++;
                        int noticing = (int)Math.Ceiling(left * skill.WhistleNotice);
                        for (int a = 0; a < n && noticing > 0; a++)
                        {
                            if (game.AnimalFound(a) || whistled[a])
                                continue;
                            whistled[a] = true;
                            if (double.IsNaN(noticed[a]))
                                noticed[a] = now;
                            noticing--;
                        }
                        lastFind = now;
                        busy = now + skill.TapSeconds;
                    }
                }
            }
        }

        /// <summary>A game played on the controller alone with this setup (no runner): its final score.</summary>
        public static int PlayAlone(HideSeekController game, in ActivitySetup setup, SeekerSkill skill)
        {
            game.Begin(in setup);
            Play(game, skill, dt => game.Tick(dt), () => !game.Finished);
            return game.Score;
        }

        /// <summary>The empty spot a casual player touches: a decoy (it moved in the wind), else the first free spot.</summary>
        public static int EmptySpot(HideSeekController game)
        {
            if (game.DecoyCount > 0)
                return game.DecoySpot(0);
            for (int s = 0; s < game.SpotCount; s++)
                if (game.AnimalAt(s) < 0 && !game.FoundAt(s))
                    return s;
            return -1;
        }

        private static void Tap(HideSeekController game, int spot, SeekerSkill skill, double now, ref double busy, ref double lastAction)
        {
            game.TapSpot(spot);
            busy = now + skill.TapSeconds;
            lastAction = now;
        }
    }

    /// <summary>
    /// T39 balance (doc 01 Contenu, S-ACT-002; doc Mini-jeux, section 6 « 10 défis équilibrés »): scripted players on the
    /// free mode and the 10 challenges, over several seeds, on irregular zones of 7, 6, 5 and 4 spots like those a refuge
    /// chunk frames (T39 deviation: the game plays in the refuge, zones of 3 to 6 good spots, 3 / 4 / 5 animals, the
    /// thresholds rebalanced for them).
    /// </summary>
    public sealed class HideSeekBalanceTests
    {
        internal static ActivitySetup Setup(ActivityRules rules, int difficulty, int challenge = -1, uint seed = 1, bool slower = false)
        {
            float seconds = rules.ChallengeSeconds(challenge, HideSeekRules.BaseSeconds);
            return new ActivitySetup
            {
                Activity = ActivityId.HideAndSeek,
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

        /// <summary>Ten measured-looking spots of a small clearing (meters), not a grid.</summary>
        internal static readonly float[] ClearingX = { -2.6f, -1.1f, 0.4f, 2.2f, -2.0f, -0.3f, 1.5f, 2.7f, -1.2f, 0.9f };
        internal static readonly float[] ClearingY = { 0f, 0f, 0.05f, 0f, 0f, 0.1f, 0f, 0f, 0f, 0f };
        internal static readonly float[] ClearingZ = { -1.6f, -1.9f, -1.4f, -1.7f, 0.1f, 0.3f, -0.2f, 0.4f, 1.8f, 1.6f };

        /// <summary>A framed zone of 8 spots in the pipeline's order (best hider first, scattered as a chunk's decor is).</summary>
        internal static readonly float[] Zone8X = { -2.4f, 1.9f, 0.2f, -1.2f, 2.6f, 0.4f, -2.8f, 1.2f };
        internal static readonly float[] Zone8Z = { -1.0f, 1.2f, -1.5f, 1.3f, -0.8f, 0.4f, 0.6f, -0.2f };

        /// <summary>Zones of 7, 6, 5 and 4 spots, best first.</summary>
        internal static readonly float[] Zone7X = { -2.2f, 1.8f, 0.1f, -1.5f, 2.5f, 0.3f, -2.9f };
        internal static readonly float[] Zone7Z = { -1.1f, 0.9f, -1.4f, 1.2f, -1.0f, 0.5f, 0.3f };
        internal static readonly float[] Zone6X = { -2.1f, 1.7f, 0.2f, -1.3f, 2.4f, 0.3f };
        internal static readonly float[] Zone6Z = { -1.0f, 0.9f, -1.4f, 1.1f, -0.9f, 0.6f };
        internal static readonly float[] Zone4X = { -1.8f, 1.5f, 0.3f, -0.9f };
        internal static readonly float[] Zone4Z = { -0.7f, 0.8f, -1.2f, 1.0f };
        internal static readonly float[] Zone5X = { -1.9f, 1.6f, 0.2f, -1.0f, 2.4f };
        internal static readonly float[] Zone5Z = { -0.8f, 0.9f, -1.3f, 1.2f, -0.9f };

        /// <summary>A game on a set: 12 (the synthetic meadow), 10 (the clearing), 8, 7, 6, 5 or 4 spots.</summary>
        internal static HideSeekController Game(int spots = 12)
        {
            var game = new HideSeekController();
            switch (spots)
            {
                case 10:
                    game.SetSpots(ClearingX.Length, ClearingX, ClearingY, ClearingZ);
                    break;
                case 8:
                    game.SetSpots(8, Zone8X, new float[8], Zone8Z);
                    break;
                case 7:
                    game.SetSpots(7, Zone7X, new float[7], Zone7Z);
                    break;
                case 6:
                    game.SetSpots(6, Zone6X, new float[6], Zone6Z);
                    break;
                case 4:
                    game.SetSpots(4, Zone4X, new float[4], Zone4Z);
                    break;
                case 5:
                    game.SetSpots(5, Zone5X, new float[5], Zone5Z);
                    break;
            }
            return game;
        }

        /// <summary>The sets the balance plays on: refuge-like zones of 7, 6, 5 and 4 spots.</summary>
        internal static readonly int[] Sets = { 7, 6, 5, 4 };

        /// <summary>The lowest score over a few seeds (the layout and the hints change with each game).</summary>
        internal static int Worst(ActivityRules rules, int difficulty, int challenge, SeekerSkill skill, int spots = 12, int seeds = 6)
        {
            var game = Game(spots);
            int worst = int.MaxValue;
            for (uint seed = 1; seed <= seeds; seed++)
            {
                var setup = Setup(rules, difficulty, challenge, seed * 7919u);
                worst = Math.Min(worst, ScriptedSeeker.PlayAlone(game, in setup, skill));
            }
            return worst;
        }

        internal static int Best(ActivityRules rules, int difficulty, int challenge, SeekerSkill skill, int spots = 12, int seeds = 6)
        {
            var game = Game(spots);
            int best = 0;
            for (uint seed = 1; seed <= seeds; seed++)
            {
                var setup = Setup(rules, difficulty, challenge, seed * 7919u);
                best = Math.Max(best, ScriptedSeeker.PlayAlone(game, in setup, skill));
            }
            return best;
        }

        [Test]
        public void AStrongPlayer_Reaches3StarsAtD3_ACasualOne_1To2Stars()
        {
            var rules = HideSeekRules.Create();
            foreach (int spots in Sets)
            {
                int strong = Worst(rules, 3, -1, SeekerSkill.Strong(), spots);
                int casualWorst = Worst(rules, 3, -1, SeekerSkill.Casual(), spots);
                int casualBest = Best(rules, 3, -1, SeekerSkill.Casual(), spots);
                TestContext.WriteLine($"{spots} spots, d3: strong {strong}, casual {casualWorst} to {casualBest}");
                Assert.AreEqual(3, rules.Free.Stars(strong), spots + " spots: a strong player gets 3 stars at d3, every seed: " + strong);
                Assert.GreaterOrEqual(rules.Free.Stars(casualWorst), 1, spots + " spots: a casual player gets at least 1 star: " + casualWorst);
                Assert.LessOrEqual(rules.Free.Stars(casualBest), 2, spots + " spots: a casual player does not get 3 stars: " + casualBest);
            }
        }

        [Test]
        public void TheFreeMode_StaysWithinReach_FromD1ToD10()
        {
            var rules = HideSeekRules.Create();
            foreach (int spots in Sets)
            {
                for (int d = 1; d <= 10; d++)
                {
                    int strong = Worst(rules, d, -1, SeekerSkill.Strong(), spots);
                    int casual = Worst(rules, d, -1, SeekerSkill.Casual(), spots);
                    TestContext.WriteLine($"{spots} spots, d{d}: strong {strong}, casual {casual}");
                    Assert.AreEqual(3, rules.Free.Stars(strong), spots + " spots, d" + d + ": a strong player reaches 3 stars.");
                    Assert.GreaterOrEqual(rules.Free.Stars(casual), 1, spots + " spots, d" + d + ": a casual player gets a star.");
                }
            }
        }

        [Test]
        public void EachChallenge_3StarsForAStrongPlayer_1StarForACasualOne()
        {
            var rules = HideSeekRules.Create();
            foreach (int spots in Sets)
            {
                for (int c = 0; c < ActivityCatalog.ChallengesPerActivity; c++)
                {
                    var stars = rules.Challenges[c].Stars;
                    int strong = Worst(rules, 0, c, SeekerSkill.Strong(), spots);
                    int casual = Worst(rules, 0, c, SeekerSkill.Casual(), spots);
                    TestContext.WriteLine($"{spots} spots, challenge {c + 1}: strong {strong}, casual {casual} ({stars.One}/{stars.Two}/{stars.Three})");
                    Assert.AreEqual(3, stars.Stars(strong), spots + " spots, challenge " + (c + 1) + ": 3 stars within reach of a strong player: " + strong);
                    Assert.GreaterOrEqual(stars.Stars(casual), 1, spots + " spots, challenge " + (c + 1) + ": a casual player gets a star: " + casual);
                }
            }
        }

        [Test]
        public void TheSlowerMode_OnlyGivesTime()
        {
            // « Activités plus lentes »: 75 s, the hints at their usual seconds: never fewer points.
            var rules = HideSeekRules.Create();
            var game = new HideSeekController();
            for (uint seed = 1; seed <= 4; seed++)
            {
                var normal = Setup(rules, 5, -1, seed);
                var slow = Setup(rules, 5, -1, seed, slower: true);
                int a = ScriptedSeeker.PlayAlone(game, in normal, SeekerSkill.Casual());
                int b = ScriptedSeeker.PlayAlone(game, in slow, SeekerSkill.Casual());
                Assert.GreaterOrEqual(b, a, "seed " + seed);
            }
        }
    }
}
