#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The rules of « Memory des empreintes » as data (doc 01 Contenu, S-ACT-004 « Règles de score », « Adaptation de
    /// difficulté », « Objets » and « Défis »; doc « Mini-jeux &amp; activités », S-ACT-004): the grid, the pair type, the
    /// preview, the glow card and the double pair by d, the scoring numbers, the free mode's thresholds and length per grid,
    /// and the 10 challenges. <see cref="MemoryController"/> reads the parameters by the indexes below.
    /// <para>The doc gives the grid, the pair type and the preview in steps (« 4 × 3 (d1 à d3) »), so they are stepped
    /// parameters (<see cref="DifficultyParameter.Steps"/>), and the free mode's thresholds and length per grid
    /// (<see cref="ActivityRules.FreeByLevel"/>, <see cref="ActivityRules.SecondsByLevel"/>). A challenge never changes the
    /// grid of its d: each challenge sits at a d whose grid is the one its table names (so its length is that grid's, unless
    /// it has its own: challenge 7 in 45 s, challenge 10 in 75 s).</para>
    /// <para>T41 readings where the doc is silent: the preview is part of the game's time (the clock runs from the first
    /// frame); « Activités plus lentes » gives × 1.25 time and keeps the 0.8 s turn-back, the preview and the 1 s glow; the end
    /// bonus counts the whole seconds left on the clock; a game ended by the clock scores its pairs only (no end bonus); the
    /// glow card is never one of the double pair's; challenges 8 and 9 deal identical portraits (only 4 and 10 name
    /// « empreinte et portrait »).</para>
    /// <para>T41 thresholds (MemoryBalanceTests: a strong scripted player at 3 stars in the free mode from d1 to d10 and in
    /// every challenge, a casual one at 1 to 2 stars in the free mode at d3 and at least 1 in every challenge, six seeds
    /// each): the doc's everywhere except the free mode's 4 × 3 3-star threshold, 850 → 980 (see <see cref="SmallFree"/>).</para>
    /// </summary>
    public static class MemoryRules
    {
        // ---------------- difficulty parameters, by index (ActivityRules.Parameters) ----------------

        /// <summary>The grid: 0 = 4 × 3 (d1 to d3), 1 = 4 × 4 (d4 to d7), 2 = 5 × 4 (d8 to d10).</summary>
        public const int Grid = 0;

        /// <summary>1: each pair is a pawprint and a portrait (d6 to d10; challenges 4 and 10); 0: two identical portraits.</summary>
        public const int Pawprints = 1;

        /// <summary>Seconds every card shows its face at the start: 2 at d1 and d2, 1 at d3 and d4, 0 from d5 (challenge 2: 0).</summary>
        public const int Preview = 2;

        /// <summary>1: one pair is worth 200 (from d4).</summary>
        public const int DoublePair = 3;

        /// <summary>1: one glow card per game (gold edge; always).</summary>
        public const int GlowCard = 4;

        /// <summary>Points a mistake takes from the end bonus: 20 (challenge 5 « Sans faute »: 50).</summary>
        public const int MistakePenalty = 5;

        /// <summary>Every this many pairs found, two face-down cards swap places (challenge 6 « Cartes qui bougent »: 4); 0: never.</summary>
        public const int SwapEvery = 6;

        /// <summary>1: every card from one biome (challenge 9 « Même biome »).</summary>
        public const int SameBiome = 7;

        public const int ParameterCount = 8;

        // ---------------- grids ----------------

        public const int SmallGrid = 0;
        public const int MediumGrid = 1;
        public const int LargeGrid = 2;
        public const int GridCount = 3;

        /// <summary>The largest grid's cards (5 × 4).</summary>
        public const int MaxCards = 20;

        public const int MaxPairs = MaxCards / 2;

        public static int Columns(int grid) => ClampGrid(grid) == LargeGrid ? 5 : 4;

        public static int Rows(int grid) => ClampGrid(grid) == SmallGrid ? 3 : 4;

        public static int Pairs(int grid) => Columns(grid) * Rows(grid) / 2;

        public static int ClampGrid(int grid) => grid < SmallGrid ? SmallGrid : grid > LargeGrid ? LargeGrid : grid;

        /// <summary>« 4 × 3 (d1 à d3), 4 × 4 (d4 à d7), 5 × 4 (d8 à d10) », by d − 1.</summary>
        private static readonly double[] GridByLevel = { 0, 0, 0, 1, 1, 1, 1, 2, 2, 2 };

        /// <summary>The free mode's grid at d (WISH-059's n is drawn from it).</summary>
        public static int GridAt(int d) => (int)GridByLevel[AdaptiveDifficulty.Clamp(d) - 1];

        /// <summary>« 45 s en 4 × 3, 60 s en 4 × 4, 90 s en 5 × 4 » (before « Activités plus lentes »).</summary>
        public static float SecondsOf(int grid)
        {
            switch (ClampGrid(grid))
            {
                case SmallGrid: return SmallSeconds;
                case MediumGrid: return MediumSeconds;
                default: return LargeSeconds;
            }
        }

        public const float SmallSeconds = 45f;
        public const float MediumSeconds = 60f;
        public const float LargeSeconds = 90f;

        /// <summary>Challenge 7 « Chrono serré »: 4 × 4 in 45 s.</summary>
        public const float TightSeconds = 45f;

        /// <summary>Challenge 10 « Grand Memory »: 5 × 4 in 75 s.</summary>
        public const float GrandSeconds = 75f;

        // ---------------- scoring (doc 01, S-ACT-004 « Règles de score » and « Objets ») ----------------

        /// <summary>« Paire trouvée : +100. »</summary>
        public const int PairPoints = 100;

        /// <summary>« Carte double : la paire vaut 200. »</summary>
        public const int DoublePairPoints = 200;

        /// <summary>« Bonus de fin : 50 × nombre de paires − 20 × erreurs (au moins 0), plus 3 points par seconde restante. »</summary>
        public const int EndPairPoints = 50;

        public const int MistakePoints = 20;

        /// <summary>Challenge 5 « Sans faute »: « chaque erreur retire 50 au lieu de 20 ».</summary>
        public const int StrictMistakePoints = 50;

        public const int SecondPoints = 3;

        /// <summary>« Sinon elles se retournent après 0,8 s » (unchanged by « Activités plus lentes »).</summary>
        public const float TurnBackSeconds = 0.8f;

        /// <summary>« Quand on la retourne, la carte de sa paire luit 1 s. »</summary>
        public const float GlowSeconds = 1f;

        /// <summary>WISH-059 « Finis un Memory en {n} coups ou moins »: n = 2 × the game's pairs + 6 (tries, a try is two cards).</summary>
        public static int MovesTarget(int pairs) => 2 * pairs + 6;

        /// <summary>The end bonus: 50 × pairs − penalty × mistakes (at least 0), plus 3 per whole second left.</summary>
        public static int EndBonus(int pairs, int mistakes, int penalty, float secondsLeft)
        {
            int bonus = EndPairPoints * pairs - penalty * mistakes;
            if (bonus < 0)
                bonus = 0;
            int whole = secondsLeft > 0f ? (int)secondsLeft : 0;
            return bonus + SecondPoints * whole;
        }

        // ---------------- challenges (0-based) ----------------

        /// <summary>Challenge 1 « Petite grille »: 4 × 3, identical portraits (d1: a 2 s preview).</summary>
        public const int SmallGridChallenge = 0;

        /// <summary>Challenge 2 « Bonne mémoire »: 4 × 3, no preview.</summary>
        public const int GoodMemoryChallenge = 1;

        /// <summary>Challenge 3 « Grille moyenne »: 4 × 4 (d5).</summary>
        public const int MediumGridChallenge = 2;

        /// <summary>Challenge 4 « Empreintes »: 4 × 4, a pawprint and a portrait (d6).</summary>
        public const int PawprintChallenge = 3;

        /// <summary>Challenge 5 « Sans faute »: 4 × 3, each mistake takes 50 (d3: a 1 s preview).</summary>
        public const int FlawlessChallenge = 4;

        /// <summary>Challenge 6 « Cartes qui bougent »: 4 × 4, two face-down cards swap places every 4 pairs (d5).</summary>
        public const int MovingCardsChallenge = 5;

        /// <summary>Challenge 7 « Chrono serré »: 4 × 4 in 45 s (d5).</summary>
        public const int TightClockChallenge = 6;

        /// <summary>Challenge 8 « Grande grille »: 5 × 4, identical portraits (d8).</summary>
        public const int BigGridChallenge = 7;

        /// <summary>
        /// Challenge 9 « Même biome »: 5 × 4, every card from one biome (d8, identical portraits): the biome the player has the
        /// most variants of. Today only the Prairie exists, so every card is a Prairie variant either way; with more biomes
        /// the stage names each variant's biome (<see cref="MemoryController.SetBiomes"/>).
        /// </summary>
        public const int SameBiomeChallenge = 8;

        /// <summary>Challenge 10 « Grand Memory »: 5 × 4, a pawprint and a portrait, 75 s (d10).</summary>
        public const int GrandChallenge = 9;

        // ---------------- the items won (doc 01, S-ACT-004 « Objets gagnés ») ----------------

        /// <summary>« À 3 étoiles, 1 Biscuit aux graines »: a rewarded game at 3 stars gives 1 Seed Cookie (a treat).</summary>
        public static readonly ContentId ThreeStarTreat = ContentId.FromSource("treat.seed_cookie");

        public const int ThreeStarTreatCount = 1;
        public const int ThreeStarTreatStars = 3;

        /// <summary>
        /// « … et 1 graine (Jardin ouvert) »: Trèfle doux, the Prairie's seed (the plant of the seed lots, the only one known
        /// before the Garden, <see cref="DailyRewardTables.SweetClover"/>). Given only once the Garden is open
        /// (<see cref="ActivityConfig.GardenAvailable"/>, false until T43, which may pick the theme biome's seed instead).
        /// </summary>
        public static readonly ContentId ThreeStarSeed = DailyRewardTables.SweetClover;

        /// <summary>The seed rides on the reward service's counted items: count 0 is 1 per game played to its end.</summary>
        public const int SeedCount = 0;

        public const int SeedPer = 1;
        public const int SeedMax = 1;
        public const int SeedStars = 3;

        // ---------------- the free mode's thresholds (doc 01: « Seuils du mode libre ») ----------------

        /// <summary>
        /// 4 × 3: doc 500 / 700 / 850 → 500 / 700 / 980 (T41 balance, MemoryBalanceTests, six seeds). Every pair found already
        /// scores 900 before the time bonus (600 for the pairs, 300 for the end bonus), so a casual player finishing its d3 game
        /// scores 905 to 972 and took 3 stars at 850; 980 asks for no mistake and about 27 seconds left, which a strong player
        /// reaches (990 to 996 from d1 to d3). The 4 × 4 and 5 × 4 thresholds keep the doc's.
        /// </summary>
        public static readonly StarThresholds SmallFree = new StarThresholds(500, 700, 980);
        public static readonly StarThresholds MediumFree = new StarThresholds(700, 950, 1150);
        public static readonly StarThresholds LargeFree = new StarThresholds(900, 1250, 1500);

        /// <summary>The free mode's thresholds of a grid.</summary>
        public static StarThresholds FreeOf(int grid)
        {
            switch (ClampGrid(grid))
            {
                case SmallGrid: return SmallFree;
                case MediumGrid: return MediumFree;
                default: return LargeFree;
            }
        }

        private const double Keep = double.NaN;

        /// <summary>
        /// The rules: the stepped parameters by d; the free mode's thresholds and length per grid, by d; the 10 challenges of
        /// the doc table, each at a d whose grid is its own (challenges 1 and 2 at d1, 5 at d3, 3, 6 and 7 at d5, 4 at d6, 8
        /// and 9 at d8, 10 at d10), with their fixed values, thresholds and lengths. Each call builds a new object (boot and
        /// tests).
        /// </summary>
        public static ActivityRules Create()
        {
            var parameters = new[]
            {
                //                                  d1  d2  d3  d4  d5  d6  d7  d8  d9  d10
                DifficultyParameter.Steps("grid", GridByLevel),
                DifficultyParameter.Steps("pawprints", 0, 0, 0, 0, 0, 1, 1, 1, 1, 1),
                DifficultyParameter.Steps("preview", 2, 2, 1, 1, 0, 0, 0, 0, 0, 0),
                DifficultyParameter.Steps("double_pair", 0, 0, 0, 1, 1, 1, 1, 1, 1, 1),
                new DifficultyParameter("glow_card", 1, 1, 1),
                new DifficultyParameter("mistake_penalty", MistakePoints, MistakePoints, MistakePoints),
                new DifficultyParameter("swap_every", 0, 0, 0),
                new DifficultyParameter("same_biome", 0, 0, 0),
            };
            var challenges = new[]
            {
                // 01 « Petite grille »: 4 × 3, identical portraits (d1: a 2 s preview).
                Challenge(500, 700, 850, 1),
                // 02 « Bonne mémoire »: 4 × 3, no preview.
                Challenge(450, 650, 800, 1, preview: 0),
                // 03 « Grille moyenne »: 4 × 4 (d5: no preview, the double pair).
                Challenge(700, 950, 1150, 5),
                // 04 « Empreintes »: 4 × 4, a pawprint and a portrait.
                Challenge(650, 900, 1100, 6, pawprints: 1),
                // 05 « Sans faute »: 4 × 3, each mistake takes 50 instead of 20 (d3: a 1 s preview).
                Challenge(500, 700, 850, 3, penalty: StrictMistakePoints),
                // 06 « Cartes qui bougent »: 4 × 4, two face-down cards swap places every 4 pairs.
                Challenge(650, 900, 1100, 5, swapEvery: 4),
                // 07 « Chrono serré »: 4 × 4 in 45 s.
                Challenge(650, 900, 1100, 5, seconds: TightSeconds),
                // 08 « Grande grille »: 5 × 4, identical portraits.
                Challenge(900, 1250, 1500, 8, pawprints: 0),
                // 09 « Même biome »: 5 × 4, every card from one biome, identical portraits.
                Challenge(850, 1200, 1450, 8, pawprints: 0, sameBiome: 1),
                // 10 « Grand Memory »: 5 × 4, a pawprint and a portrait, 75 s.
                Challenge(900, 1250, 1500, 10, pawprints: 1, seconds: GrandSeconds),
            };
            var freeByLevel = new StarThresholds[AdaptiveDifficulty.Max];
            var secondsByLevel = new float[AdaptiveDifficulty.Max];
            for (int d = AdaptiveDifficulty.Min; d <= AdaptiveDifficulty.Max; d++)
            {
                int grid = (int)parameters[Grid].At(d);
                freeByLevel[d - 1] = FreeOf(grid);
                secondsByLevel[d - 1] = SecondsOf(grid);
            }
            int start = (int)parameters[Grid].At(AdaptiveDifficulty.Start);
            return new ActivityRules(ActivityId.PawMemory, FreeOf(start), parameters, challenges, freeByLevel, secondsByLevel);
        }

        private static ChallengeRule Challenge(int one, int two, int three, int level, double pawprints = Keep, double preview = Keep,
            double penalty = Keep, double swapEvery = Keep, double sameBiome = Keep, float seconds = 0f)
        {
            // The grid, the double pair and the glow card always follow the challenge's d.
            var overrides = new[] { Keep, pawprints, preview, Keep, Keep, penalty, swapEvery, sameBiome };
            return new ChallengeRule(new StarThresholds(one, two, three), level, overrides, seconds);
        }
    }
}
