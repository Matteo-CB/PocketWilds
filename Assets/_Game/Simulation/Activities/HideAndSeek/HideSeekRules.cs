#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The rules of « Cache-cache » as data (doc 01 Contenu, S-ACT-002 « Règles de score », « Adaptation de difficulté »,
    /// « Objets » and « Défis »; doc « Mini-jeux &amp; activités », S-ACT-002): the free mode's thresholds, the difficulty
    /// parameters at d1 / d5 / d10, the scoring numbers, the item and kindness times and the 10 challenges.
    /// <see cref="HideSeekController"/> reads the parameters by the indexes below; a challenge fixes some of them (its
    /// <see cref="ChallengeRule.Overrides"/>) and its own length (challenge 4: 40 s, challenge 10: 45 s).
    /// <para>T39 deviation (Docs/Design/T39_Cache_cache.md): the game plays in the player's own refuge, in a framed zone of a
    /// Prairie chunk that holds 3 to 6 good hiding spots, not on a set of 12. The counts shrink: hidden animals 3 / 4 / 5
    /// instead of 4 / 6 / 8, decoys 0 / 1 / 2 instead of 0 / 2 / 4, the challenges' counts with them; visibility and hint
    /// delays stay the doc's. The star thresholds follow the new counts where the doc's no longer fit (balance tests).</para>
    /// </summary>
    public static class HideSeekRules
    {
        // ---------------- difficulty parameters, by index (ActivityRules.Parameters) ----------------

        /// <summary>Animals hidden N: 3 / 4 / 5 (T39 deviation from the doc's 4 / 6 / 8; clamped to the zone's spots).</summary>
        public const int Hidden = 0;

        /// <summary>Share of the silhouette left visible: 0.30 / 0.15 / 0.08, a straight line rounded to 0.01.</summary>
        public const int Visibility = 1;

        /// <summary>Seconds before an animal not found gives its hint: 6 / 10 / 15 (a Timid animal waits 2 s more).</summary>
        public const int HintDelay = 2;

        /// <summary>Decoy spots (empty, moving in the wind): 0 / 1 / 2 (T39 deviation from 0 / 2 / 4; clamped to the spots left free).</summary>
        public const int Decoys = 3;

        /// <summary>1: no hint this game (challenges 2 « Sans indice » and 10 « Le grand cache-cache »); otherwise 0.</summary>
        public const int NoHints = 4;

        /// <summary>1: no magnifier nor whistle (challenge 2 « Sans indice »); otherwise 0. Challenge 10 keeps its items.</summary>
        public const int NoItems = 5;

        /// <summary>The mistake that ends the game (challenge 6 « Un seul regard »: the 4th); 0: none, nothing is ever lost.</summary>
        public const int MistakeLimit = 6;

        /// <summary>Night: the share of the screen lit around the finger (challenge 7 « Dans le noir »: 0.25); 0: day.</summary>
        public const int Night = 7;

        /// <summary>1: the hidden animals are all of one species (challenge 8 « Tous pareils »); the stage picks it.</summary>
        public const int SameSpecies = 8;

        /// <summary>Seconds between two moves of an animal not found yet (challenge 9 « Cachettes mouvantes »: 15); 0: never.</summary>
        public const int MoveEvery = 9;

        public const int ParameterCount = 10;

        // ---------------- challenges (0-based) ----------------

        /// <summary>Challenge 2 « Sans indice »: no hint, no magnifier, no whistle.</summary>
        public const int NoHintChallenge = 1;

        /// <summary>Challenge 4 « Course contre la montre »: 40 s.</summary>
        public const int QuickChallenge = 3;

        /// <summary>Challenge 6 « Un seul regard »: the 4th mistake ends the game.</summary>
        public const int OneLookChallenge = 5;

        /// <summary>Challenge 7 « Dans le noir »: night, a lit circle of 25 % of the screen around the finger (HUD mask).</summary>
        public const int NightChallenge = 6;

        /// <summary>Challenge 8 « Tous pareils »: five animals of one species (the presenter picks the species).</summary>
        public const int SameSpeciesChallenge = 7;

        /// <summary>Challenge 9 « Cachettes mouvantes »: an animal not found yet changes spot every 15 s (no decoy: a free spot stays).</summary>
        public const int MovingChallenge = 8;

        /// <summary>Challenge 10 « Le grand cache-cache »: 5 animals, 45 s, 8 %, no hint (the items stay, main session's decision).</summary>
        public const int GrandChallenge = 9;

        // ---------------- scoring (doc 01, S-ACT-002 « Règles de score ») ----------------

        /// <summary>An animal found before its hint: +100 and +2 per whole second left.</summary>
        public const int FoundPoints = 100;
        public const int FoundSecondPoints = 2;

        /// <summary>An animal found after its hint (or after the magnifier or the whistle): +50 and +1 per whole second left.</summary>
        public const int HintFoundPoints = 50;
        public const int HintFoundSecondPoints = 1;

        /// <summary>Every animal found: +5 per whole second left, the game ends.</summary>
        public const int AllFoundSecondPoints = 5;

        // ---------------- times (doc 01; the T39 kindness defaults where the doc is silent) ----------------

        /// <summary>The free mode's length (challenges 4 and 10 have their own; × 1.25 with « Activités plus lentes »).</summary>
        public const float BaseSeconds = 60f;

        /// <summary>A Timid animal waits this much more before its hint (doc 01, doc 02 Renouveau).</summary>
        public const float TimidExtraSeconds = 2f;

        /// <summary>
        /// The hints are staggered over this span (T39 default): each hidden animal gets its own offset, evenly spread in a
        /// seeded order and slightly jittered, so hints never pop together.
        /// </summary>
        public const float HintStaggerSeconds = 1.5f;

        /// <summary>The magnifier's soft ring (× 1.25 with « Activités plus lentes »).</summary>
        public const float MagnifierSeconds = 2f;

        /// <summary>3 empty touches within 2 s: touches are ignored for 1 s.</summary>
        public const int EmptyBurstTouches = 3;
        public const float EmptyBurstSeconds = 2f;
        public const float TouchPauseSeconds = 1f;

        /// <summary>A decoy rustles every 3.5 to 5.5 s (its own period, seeded), first between 1.2 s and about 6 s (T39 default).</summary>
        public const float DecoyPeriodMinSeconds = 3.5f;
        public const float DecoyPeriodSpanSeconds = 2f;
        public const float DecoyFirstSeconds = 1.2f;

        /// <summary>
        /// A decoy never rustles within this time of a real hint's start, nor within <see cref="DecoyGapSeconds"/> of another
        /// decoy: the player can always tell which spot moved (T39 default).
        /// </summary>
        public const float HintGapSeconds = 0.6f;
        public const float DecoyGapSeconds = 0.35f;

        // ---------------- the item won (doc 01, S-ACT-002 « Objet gagné ») ----------------

        /// <summary>« Objet gagné : 1 Biscuit champignon à 3 étoiles »: a rewarded game at 3 stars gives 1 Mushroom Biscuit.</summary>
        public static readonly ContentId ThreeStarTreat = ContentId.FromSource("treat.mushroom_biscuit");

        public const int ThreeStarTreatCount = 1;
        public const int ThreeStarTreatStars = 3;

        private const double Keep = double.NaN;

        /// <summary>
        /// The rules: free mode 300 / 600 / 750; the parameters at d1 / d5 / d10; the 10 challenges of the doc with the level
        /// their other parameters come from (d1 for the first, d3 for the « 4 animaux » ones: 4 animals, 8 s hints, 1 decoy;
        /// d5 for « Grande famille », « Tous pareils » and « Cachettes mouvantes »; d10 for the last), their fixed values and
        /// thresholds, challenge 4 in 40 s and challenge 10 in 45 s. Each call builds a new object (boot and tests).
        /// <para>T39 thresholds (balance tests: a strong player 3 stars on zones of 7, 6, 5 and 4 spots, a casual one 1 to 2
        /// at d3 and at least 1 in every challenge): the doc's where they still fit; lowered for the fewer animals in the free
        /// mode (3 stars 900 → 750), challenges 3 (850 → 800), 4 (750 → 700), 5 (1 300 → 1 000), 8 (1 000 → 900: a 4-spot
        /// zone holds 4 of its 5), 9 (1 000 → 850) and 10 (400 / 800 / 1 200 → 350 / 600 / 750).</para>
        /// </summary>
        public static ActivityRules Create()
        {
            var parameters = new[]
            {
                new DifficultyParameter("hidden", 3, 4, 5),
                new DifficultyParameter("visibility", 0.30, 0.15, 0.08, 2),
                new DifficultyParameter("hint_delay", 6, 10, 15),
                new DifficultyParameter("decoys", 0, 1, 2),
                new DifficultyParameter("no_hints", 0, 0, 0),
                new DifficultyParameter("no_items", 0, 0, 0),
                new DifficultyParameter("mistake_limit", 0, 0, 0),
                new DifficultyParameter("night", 0.0, 0.0, 0.0, 2),
                new DifficultyParameter("same_species", 0, 0, 0),
                new DifficultyParameter("move_every", 0, 0, 0),
            };
            var challenges = new[]
            {
                // 01 « Premiers cachés »: find 3 animals (doc: 4); hint at 6 s (d1: 30 %, no decoy).
                Challenge(200, 450, 650, 1, hidden: 3, hintDelay: 6),
                // 02 « Sans indice »: find 4 animals (doc: 5); no hint, no magnifier, no whistle.
                Challenge(300, 600, 850, 3, hidden: 4, noHints: 1, noItems: 1),
                // 03 « Bien cachés »: find 4 animals (doc: 5); visibility 10 %.
                Challenge(300, 600, 800, 3, hidden: 4, visibility: 0.10),
                // 04 « Course contre la montre »: find 4 animals (doc: 5) in 40 s.
                Challenge(250, 500, 700, 3, hidden: 4, seconds: 40f),
                // 05 « Grande famille »: find 5 animals (doc: 8); visibility 20 %; no decoy (the zone's spots go to the family).
                Challenge(450, 900, 1000, 5, hidden: 5, visibility: 0.20, decoys: 0),
                // 06 « Un seul regard »: find 4 animals (doc: 5); the 4th mistake ends the game.
                Challenge(300, 600, 900, 3, hidden: 4, mistakes: 4),
                // 07 « Dans le noir »: find 4 animals (doc: 5); night, a lit circle of 25 % of the screen around the finger.
                Challenge(250, 550, 800, 3, hidden: 4, night: 0.25),
                // 08 « Tous pareils »: find 5 animals of one species (doc: 6); visibility 15 %.
                Challenge(350, 700, 900, 5, hidden: 5, visibility: 0.15, sameSpecies: 1),
                // 09 « Cachettes mouvantes »: find 4 animals (doc: 6); one not found yet changes spot every 15 s; no decoy, so a
                // free spot stays to move to.
                Challenge(350, 700, 850, 5, hidden: 4, decoys: 0, moveEvery: 15),
                // 10 « Le grand cache-cache »: find 5 animals (doc: 8); 45 s, visibility 8 %, no hint, no decoy (the items stay:
                // only challenge 2 takes them away, « ni loupe ni sifflet »).
                Challenge(350, 600, 750, 10, hidden: 5, visibility: 0.08, decoys: 0, noHints: 1, seconds: 45f),
            };
            return new ActivityRules(ActivityId.HideAndSeek, new StarThresholds(300, 600, 750), parameters, challenges);
        }

        private static ChallengeRule Challenge(int one, int two, int three, int level, double hidden = Keep, double visibility = Keep,
            double hintDelay = Keep, double decoys = Keep, double noHints = Keep, double noItems = Keep, double mistakes = Keep,
            double night = Keep, double sameSpecies = Keep, double moveEvery = Keep, float seconds = 0f)
        {
            var overrides = new[] { hidden, visibility, hintDelay, decoys, noHints, noItems, mistakes, night, sameSpecies, moveEvery };
            return new ChallengeRule(new StarThresholds(one, two, three), level, overrides, seconds);
        }
    }
}
