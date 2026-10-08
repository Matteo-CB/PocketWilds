#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The rules of « Toilettage » as data (doc 01 Contenu, S-ACT-001 « Règles de score », « Adaptation de difficulté »,
    /// « Objets » and « Défis »; doc « Mini-jeux &amp; activités », S-ACT-001): the free mode's thresholds 200 / 380 / 520,
    /// the difficulty parameters at d1 / d5 / d10, the scoring numbers, the item times and the 10 challenges.
    /// <see cref="GroomingController"/> reads the parameters through <see cref="ActivitySetup.Param"/> by the indexes below;
    /// a challenge fixes some of them (its <see cref="ChallengeRule.Overrides"/>), never the free mode's d.
    /// </summary>
    public static class GroomingRules
    {
        // ---------------- difficulty parameters, by index (ActivityRules.Parameters) ----------------

        /// <summary>Dirty zones Z: 16 / 24 / 32 (clamped to the zones the animal's body has).</summary>
        public const int Zones = 0;

        /// <summary>Debris D (one tap, +15): 4 / 8 / 12.</summary>
        public const int Debris = 1;

        /// <summary>Burrs B (two taps, +25): 0 / 2 / 4.</summary>
        public const int Burrs = 2;

        /// <summary>Passes P to clean a zone: 2 / 2 / 3, a step (2 up to d7, 3 from d8: the rounded line gives it).</summary>
        public const int Passes = 3;

        /// <summary>How far the animal moves, a share of the screen's width: 0 / 0.05 / 0.12 (challenge 5: 0.15).</summary>
        public const int Movement = 4;

        /// <summary>Points taken by each rebrush (challenge 2 « Tout doux »: 20; otherwise 0, nothing is ever lost).</summary>
        public const int RebrushPenalty = 5;

        /// <summary>
        /// A bow placed after this second of a 45 s game gives no bow points (challenge 8 « Finition rapide »: 35 s; 0: none).
        /// Scaled like every time of the game when the game is longer or shorter.
        /// </summary>
        public const int BowDeadline = 6;

        public const int ParameterCount = 7;

        // ---------------- challenges ----------------

        /// <summary>Challenge 7 « Grand gabarit » (0-based 6): the presenter features a HeavyQuadruped, or the biggest animal owned.</summary>
        public const int HeavyAnimalChallenge = 6;

        /// <summary>Challenge 2 « Tout doux » (0-based 1): each rebrush costs 20 points.</summary>
        public const int GentleChallenge = 1;

        /// <summary>Challenge 4 « Vite fait, bien fait » (0-based 3): 30 s.</summary>
        public const int QuickChallenge = 3;

        /// <summary>Challenge 8 « Finition rapide » (0-based 7): no bow points after 35 s.</summary>
        public const int LateBowChallenge = 7;

        // ---------------- scoring (doc 01, S-ACT-001 « Règles de score ») ----------------

        /// <summary>A zone cleaned: 10 × the streak multiplier (× 1.0 for the first zone in a row, +0.1 each, × 2.0 at most).</summary>
        public const int ZonePoints = 10;

        /// <summary>The multiplier's step in points of a zone (0.1 × 10), and its cap above × 1.0 (× 2.0 = +10).</summary>
        public const int StreakStepPoints = 1;
        public const int StreakMaxBonus = 10;

        public const int DebrisPoints = 15;
        public const int BurrPoints = 25;
        public const int BowPoints = 50;

        /// <summary>Each whole second left when every zone is clean and the bow placed.</summary>
        public const int SecondPoints = 5;

        /// <summary>The golden brush waits for a streak of × 1.5 (5 zones in a row).</summary>
        public const int GoldenBrushStreak = 5;

        /// <summary>A soap bubble cleans the 3 dirty zones nearest to it.</summary>
        public const int BubbleZones = 3;

        public const int BubbleCount = 2;

        // ---------------- times of a 45 s game (scaled with the game's length) ----------------

        public const float BaseSeconds = 45f;
        public const float FirstBubbleSecond = 15f;
        public const float SecondBubbleSecond = 30f;
        public const float GoldenBrushSecond = 20f;

        /// <summary>The bows come 5 s before the end (40 s of 45; 25 s of challenge 4's 30 s).</summary>
        public const float BowLeadSeconds = 5f;

        /// <summary>The golden brush's double passes (× 1.25 with « Activités plus lentes »).</summary>
        public const float GoldenBrushSeconds = 5f;

        // ---------------- kindness (no doc number; the T38 defaults) ----------------

        /// <summary>One giggle at most per stroke: a rebrush within this time of the last one is not counted again.</summary>
        public const float RebrushMergeSeconds = 0.6f;

        /// <summary>A zone just cleaned can be passed again this long without a rebrush (the finger is still on it).</summary>
        public const float CleanGraceSeconds = 0.5f;

        // ---------------- the item won (doc 01, S-ACT-001 « Objet gagné ») ----------------

        /// <summary>« Objet gagné : 1 Trèfle croquant à 3 étoiles »: a rewarded game at 3 stars gives 1 Clover Crunch.</summary>
        public static readonly ContentId ThreeStarTreat = ContentId.FromSource("treat.clover_crunch");

        public const int ThreeStarTreatCount = 1;
        public const int ThreeStarTreatStars = 3;

        private const double Keep = double.NaN;

        /// <summary>
        /// The rules: free mode 200 / 380 / 520; the parameters at d1 / d5 / d10; the 10 challenges of the doc with the level
        /// their other parameters come from (d1 for the first, d3 for the « 20 zones » ones, d5 for 24 and 32 zones, d8 for the
        /// last), their fixed values and thresholds, challenge 4 in 30 s. Each call builds a new object (boot and tests).
        /// </summary>
        public static ActivityRules Create()
        {
            var parameters = new[]
            {
                new DifficultyParameter("zones", 16, 24, 32),
                new DifficultyParameter("debris", 4, 8, 12),
                new DifficultyParameter("burrs", 0, 2, 4),
                new DifficultyParameter("passes", 2, 2, 3),
                new DifficultyParameter("movement", 0.0, 0.05, 0.12, 3),
                new DifficultyParameter("rebrush_penalty", 0, 0, 0),
                new DifficultyParameter("bow_deadline", 0, 0, 0),
            };
            var challenges = new[]
            {
                // 01 « Premier coup de brosse »: clean the animal; 16 zones, 4 debris, 2 passes (d1: no burr, still).
                Challenge(150, 260, 360, 1, zones: 16, debris: 4, passes: 2),
                // 02 « Tout doux »: clean without rebrushing; 20 zones; each rebrush takes 20 points.
                Challenge(200, 320, 420, 3, zones: 20, penalty: 20),
                // 03 « Pluie de feuilles »: many debris; 20 zones, 14 debris (leaves only: no burr).
                Challenge(280, 420, 540, 3, zones: 20, debris: 14, burrs: 0),
                // 04 « Vite fait, bien fait »: finish in 30 s; 20 zones, 6 debris.
                Challenge(200, 300, 400, 3, zones: 20, debris: 6, seconds: 30f),
                // 05 « Petit agité »: an animal that moves; 24 zones, movement 15 %.
                Challenge(220, 360, 480, 5, zones: 24, movement: 0.15),
                // 06 « Pelage épais »: 3 passes per zone; 20 zones, 6 debris.
                Challenge(200, 330, 450, 3, zones: 20, debris: 6, passes: 3),
                // 07 « Grand gabarit »: a heavy animal; 32 zones, 8 debris (the presenter picks the animal).
                Challenge(300, 480, 620, 5, zones: 32, debris: 8),
                // 08 « Finition rapide »: the bow before 35 s; 20 zones; a bow after 35 s gives no bow points.
                Challenge(260, 380, 500, 3, zones: 20, deadline: 35),
                // 09 « Bardanes têtues »: take the burrs off; 20 zones, 8 burrs.
                Challenge(300, 440, 580, 3, zones: 20, burrs: 8),
                // 10 « Grand toilettage »: everything at once; 36 zones, 12 debris, 3 passes, movement 10 %.
                Challenge(400, 580, 720, 8, zones: 36, debris: 12, passes: 3, movement: 0.10),
            };
            return new ActivityRules(ActivityId.Grooming, new StarThresholds(200, 380, 520), parameters, challenges);
        }

        private static ChallengeRule Challenge(int one, int two, int three, int level, double zones = Keep, double debris = Keep,
            double burrs = Keep, double passes = Keep, double movement = Keep, double penalty = Keep, double deadline = Keep,
            float seconds = 0f)
        {
            var overrides = new[] { zones, debris, burrs, passes, movement, penalty, deadline };
            return new ChallengeRule(new StarThresholds(one, two, three), level, overrides, seconds);
        }
    }
}
