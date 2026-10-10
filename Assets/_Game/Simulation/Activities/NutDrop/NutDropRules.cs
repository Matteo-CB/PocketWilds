#nullable enable
using PocketWilds.Core;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The rules of « Pluie de noisettes » as data (doc 01 Contenu, S-ACT-003 « Règles de score », « Adaptation de
    /// difficulté », « Objets » and « Défis »; doc « Mini-jeux &amp; activités », S-ACT-003): the free mode's thresholds, the
    /// difficulty parameters at d1 / d5 / d10, the playfield, the scoring numbers, the specials and the 10 challenges.
    /// <see cref="NutDropController"/> reads the parameters by the indexes below; a challenge fixes some of them (its
    /// <see cref="ChallengeRule.Overrides"/>) and its own length (challenge 10: 90 s).
    /// <para>T40 thresholds (balance tests: a strong scripted player at 3 stars in the free mode from d1 to d10 and in every
    /// challenge, a casual one at 1 to 2 stars in the free mode at d3 and at least 1 in every challenge, six seeds each): the
    /// doc's wherever its numbers allow them; lowered where the doc's own rates cap the points (see <see cref="Create"/>).</para>
    /// <para>T40 readings where the doc is silent: the regular nuts split evenly between hazelnut, acorn and berry; the
    /// multiplier of a catch counts that catch (the 5th in a row scores × 1.1); challenge 7's other nuts count in the streak
    /// and a pinecone still resets it; challenge 7 has no golden shower.</para>
    /// </summary>
    public static class NutDropRules
    {
        // ---------------- difficulty parameters, by index (ActivityRules.Parameters) ----------------

        /// <summary>Objects per second: 1.0 / 1.6 / 2.4 (challenge 5 « Averse »: 2.5), a straight line rounded to 0.01.</summary>
        public const int Rate = 0;

        /// <summary>Fall speed in playfield units per second: 2.5 / 3.5 / 5.0 (challenge 2: 4.0; challenge 9: 4.5).</summary>
        public const int FallSpeed = 1;

        /// <summary>Share of pinecones: 0.10 / 0.18 / 0.25 (challenge 2: 0; challenge 3: 0.30).</summary>
        public const int PineconeShare = 2;

        /// <summary>The basket's catch width: 1.20 / 1.00 / 0.85 (challenge 4 « Petit panier »: 0.70).</summary>
        public const int BasketWidth = 3;

        /// <summary>Share of golden hazelnuts: 0.03 / 0.04 / 0.05 (challenge 9: 0.10).</summary>
        public const int GoldenShare = 4;

        /// <summary>1: only berries score (challenge 7 « Baies seulement »); the other nuts are worth 0 and keep the streak going.</summary>
        public const int BerriesOnly = 5;

        /// <summary>The side wind's strength in units per second (challenge 8 « Vent de côté »: 0.6); 0: still air.</summary>
        public const int Wind = 6;

        /// <summary>1: the challenge measures the longest streak (challenge 6 « Série d'or »); 0: the score.</summary>
        public const int StreakGoal = 7;

        public const int ParameterCount = 8;

        // ---------------- challenges (0-based) ----------------

        /// <summary>Challenge 1 « Premières noisettes »: the d1 parameters.</summary>
        public const int FirstChallenge = 0;

        /// <summary>Challenge 2 « Sans pomme de pin »: no pinecone, falls at 4.0.</summary>
        public const int NoPineconeChallenge = 1;

        /// <summary>Challenge 3 « Gare aux pommes de pin »: 30 % pinecones.</summary>
        public const int PineconeChallenge = 2;

        /// <summary>Challenge 4 « Petit panier »: the basket 0.70 wide.</summary>
        public const int SmallBasketChallenge = 3;

        /// <summary>Challenge 5 « Averse »: 2.5 objects per second.</summary>
        public const int DownpourChallenge = 4;

        /// <summary>Challenge 6 « Série d'or »: the longest streak is measured (20 / 35 / 50 catches).</summary>
        public const int StreakChallenge = 5;

        /// <summary>Challenge 7 « Baies seulement »: only the berries score.</summary>
        public const int BerriesChallenge = 6;

        /// <summary>Challenge 8 « Vent de côté »: a gentle side wind.</summary>
        public const int WindChallenge = 7;

        /// <summary>Challenge 9 « Noisettes dorées »: 10 % golden hazelnuts, falls at 4.5.</summary>
        public const int GoldenChallenge = 8;

        /// <summary>Challenge 10 « Grande récolte »: 90 s at d10.</summary>
        public const int HarvestChallenge = 9;

        // ---------------- the playfield (main session's frozen T40 decision 3) ----------------

        /// <summary>Half the field's width: items and the basket stay within ± this.</summary>
        public const float HalfWidth = 2.4f;

        /// <summary>Items appear at this height and fall to the basket's rim at y = 0.</summary>
        public const float SpawnHeight = 6f;

        /// <summary>Missed items fall to here, beside the basket, then roll into the grass (the stage's part).</summary>
        public const float GroundY = -0.6f;

        /// <summary>An item's radius: a catch is |x − basket| ≤ width / 2 + this as it crosses the rim.</summary>
        public const float ItemRadius = 0.18f;

        /// <summary>The basket's top speed toward the finger, units per second (never slowed by « Activités plus lentes »: it is the player's).</summary>
        public const float BasketSpeed = 14f;

        /// <summary>The gentle ease near the target: the speed is at most this × the distance left (so the last 1 u eases).</summary>
        public const float BasketEase = 14f;

        /// <summary>The wide basket special: the width × this.</summary>
        public const float WideFactor = 1.5f;

        // ---------------- scoring (doc 01, S-ACT-003 « Règles de score ») ----------------

        public const int HazelnutPoints = 10;
        public const int AcornPoints = 10;
        public const int BerryPoints = 15;
        public const int GoldenPoints = 50;

        /// <summary>The multiplier: × 1.0, +0.1 every 5 catches in a row, × 3.0 at most (in tenths).</summary>
        public const int MultiplierBaseTenths = 10;
        public const int MultiplierStepCatches = 5;
        public const int MultiplierMaxTenths = 30;

        // ---------------- spawning (T40 defaults where the doc is silent) ----------------

        /// <summary>The first item appears at this second (the tutorial's paw shows at 1 s).</summary>
        public const float FirstSpawnSeconds = 0.6f;

        /// <summary>Each interval is 1 / rate × a seeded factor between 1 − this and 1 + this.</summary>
        public const float RateJitter = 0.25f;

        /// <summary>
        /// The reach a fair game asks for, units per second (the basket can do 14): two catchable items in a row never land
        /// farther apart than the basket's catch span plus this × the seconds between them. A pinecone is never one of them.
        /// </summary>
        public const float FairReachSpeed = 5f;

        /// <summary>The fair moves start after this reaction: the seconds between two landings count from here.</summary>
        public const float FairReactionSeconds = 0.12f;

        /// <summary>A pinecone landing within this many seconds of a catchable item keeps clear of it (the catch can avoid it).</summary>
        public const float PineconeClearSeconds = 0.6f;

        /// <summary>Extra room kept between a pinecone's catch zone and a catchable item's when they land close in time.</summary>
        public const float PineconeClearMargin = 0.12f;

        /// <summary>Free space between two items' edges at spawn (they then fall together, so they never touch).</summary>
        public const float ItemGap = 0.06f;

        // ---------------- specials (doc 01, S-ACT-003 « Objets ») ----------------

        /// <summary>Each special falls at most once a game, with this chance (seeded)…</summary>
        public const double SpecialChance = 0.5;

        /// <summary>… between these seconds of the game (× 1.25 with « Activités plus lentes »).</summary>
        public const float SpecialFromSeconds = 15f;
        public const float SpecialToSeconds = 45f;

        /// <summary>Two specials fall at least this far apart, so their effects never overlap (the longest lasts 8 s).</summary>
        public const float SpecialGapSeconds = 9f;

        public const float WideSeconds = 8f;
        public const float FeatherSeconds = 5f;
        public const float GoldenShowerSeconds = 5f;

        /// <summary>The feather: everything falls × this.</summary>
        public const float FeatherFactor = 0.6f;

        // ---------------- challenge 8's wind (T40 default: a gentle sine) ----------------

        /// <summary>The wind swings from one side to the other and back over this many seconds (its phase is seeded).</summary>
        public const float WindPeriodSeconds = 8f;

        // ---------------- the length ----------------

        /// <summary>The free mode's length (challenge 10: 90 s; × 1.25 with « Activités plus lentes »).</summary>
        public const float BaseSeconds = 60f;

        public const float HarvestSeconds = 90f;

        // ---------------- the items won (doc 01, S-ACT-003 « Objets gagnés ») ----------------

        /// <summary>« 1 Noisette grillée à 3 étoiles »: a rewarded game at 3 stars gives 1 Toasted Hazelnut (a treat).</summary>
        public static readonly ContentId ThreeStarTreat = ContentId.FromSource("treat.toasted_hazelnut");

        public const int ThreeStarTreatCount = 1;
        public const int ThreeStarTreatStars = 3;

        /// <summary>« 1 Noisette (ingrédient) par 10 noisettes attrapées, 3 au plus » (golden ones count).</summary>
        public static readonly ContentId HazelnutIngredient = ContentId.FromSource("ingredient.hazelnut");

        public const int HazelnutsPerIngredient = 10;
        public const int HazelnutIngredientMax = 3;

        /// <summary>« 1 Fraise par 10 baies, 2 au plus (Pâtisserie ouverte) ».</summary>
        public static readonly ContentId StrawberryIngredient = ContentId.FromSource("ingredient.strawberry");

        public const int BerriesPerStrawberry = 10;
        public const int StrawberryMax = 2;

        /// <summary>The reward counts the controller gives (<see cref="IActivityRewardCounts.RewardCount"/>).</summary>
        public const int HazelnutCount = 0;
        public const int BerryCount = 1;

        /// <summary>The free mode's thresholds (doc 500 / 1 000 / 1 600; see <see cref="Create"/>).</summary>
        public const int FreeOne = 500;
        public const int FreeTwo = 700;
        public const int FreeThree = 900;

        private const double Keep = double.NaN;

        /// <summary>
        /// The rules: the free mode's thresholds; the parameters at d1 / d5 / d10; the 10 challenges of the doc, each from the
        /// d its table names (d1 for the first, d10 for the last, d5 for the others), with their fixed values and thresholds,
        /// challenge 10 in 90 s. Each call builds a new object (boot and tests).
        /// <para>T40 thresholds (NutDropBalanceTests, six seeds; the doc's rates cap the points: 1 item a second at d1 is
        /// about 52 nuts, 1 000 points without the golden shower): free mode 500 / 1 000 / 1 600 → 500 / 700 / 900 (a strong
        /// player's d1 game, a casual player's d3 game under it); challenge 1: 3 stars 1 200 → 900; challenge 3: 500 / 1 000
        /// / 1 500 → 450 / 700 / 950; challenge 5: 3 stars 2 200 → 2 050; challenge 6: 1 star 20 → 10 catches; challenge 7:
        /// 300 / 600 / 900 → 140 / 300 / 450; challenge 8: 3 stars 1 500 → 1 400; challenge 9: 900 / 1 700 / 2 500 → 750 /
        /// 1 100 / 1 500; challenge 10: 1 500 / 2 800 / 4 000 → 1 150 / 2 800 / 3 000. Challenges 2 and 4 keep the doc's.</para>
        /// </summary>
        public static ActivityRules Create()
        {
            var parameters = new[]
            {
                new DifficultyParameter("rate", 1.0, 1.6, 2.4, 2),
                new DifficultyParameter("fall_speed", 2.5, 3.5, 5.0, 2),
                new DifficultyParameter("pinecone_share", 0.10, 0.18, 0.25, 3),
                new DifficultyParameter("basket_width", 1.20, 1.00, 0.85, 3),
                new DifficultyParameter("golden_share", 0.03, 0.04, 0.05, 3),
                new DifficultyParameter("berries_only", 0, 0, 0),
                new DifficultyParameter("wind", 0.0, 0.0, 0.0, 2),
                new DifficultyParameter("streak_goal", 0, 0, 0),
            };
            var challenges = new[]
            {
                // 01 « Premières noisettes »: the d1 parameters (3 stars: doc 1 200, the d1 rain holds about 1 000 points).
                Challenge(400, 800, 900, 1),
                // 02 « Sans pomme de pin »: no pinecone, falls at 4.0.
                Challenge(600, 1100, 1600, 5, pinecones: 0.0, fall: 4.0),
                // 03 « Gare aux pommes de pin »: 30 % pinecones (doc 500 / 1 000 / 1 500: fewer nuts fall).
                Challenge(450, 700, 950, 5, pinecones: 0.30),
                // 04 « Petit panier »: the basket 0.70.
                Challenge(400, 850, 1300, 5, width: 0.70),
                // 05 « Averse »: 2.5 objects per second (3 stars: doc 2 200).
                Challenge(800, 1500, 2050, 5, rate: 2.5),
                // 06 « Série d'or »: the longest streak (catches), d5 (1 star: doc 20).
                Challenge(10, 35, 50, 5, streakGoal: 1),
                // 07 « Baies seulement »: the berries' score; the other nuts are worth 0 and never break the streak (doc 300 /
                // 600 / 900: a berry is a third of the nuts, about 25 a game).
                Challenge(140, 300, 450, 5, berriesOnly: 1),
                // 08 « Vent de côté »: the items drift with a gentle side wind (3 stars: doc 1 500).
                Challenge(500, 1000, 1400, 5, wind: 0.6),
                // 09 « Noisettes dorées »: 10 % golden hazelnuts, falls at 4.5 (doc 900 / 1 700 / 2 500).
                Challenge(750, 1100, 1500, 5, golden: 0.10, fall: 4.5),
                // 10 « Grande récolte »: 90 s at d10 (doc 1 500 / 2 800 / 4 000).
                Challenge(1150, 2800, 3000, 10, seconds: HarvestSeconds),
            };
            return new ActivityRules(ActivityId.NutRain, new StarThresholds(FreeOne, FreeTwo, FreeThree), parameters, challenges);
        }

        /// <summary>The points of an item caught (0 for a pinecone and the specials).</summary>
        public static int Points(NutDropItemKind kind)
        {
            switch (kind)
            {
                case NutDropItemKind.Hazelnut: return HazelnutPoints;
                case NutDropItemKind.Acorn: return AcornPoints;
                case NutDropItemKind.Berry: return BerryPoints;
                case NutDropItemKind.GoldenNut: return GoldenPoints;
                default: return 0;
            }
        }

        public static bool IsSpecial(NutDropItemKind kind) =>
            kind == NutDropItemKind.WideBasket || kind == NutDropItemKind.Feather || kind == NutDropItemKind.GoldenShower;

        /// <summary>A special's effect in seconds (before « Activités plus lentes »).</summary>
        public static float SpecialSeconds(NutDropItemKind kind) =>
            kind == NutDropItemKind.WideBasket ? WideSeconds
            : kind == NutDropItemKind.Feather ? FeatherSeconds
            : kind == NutDropItemKind.GoldenShower ? GoldenShowerSeconds : 0f;

        /// <summary>The multiplier for a streak, in tenths: 10 + 1 per 5 catches in a row, 30 at most.</summary>
        public static int MultiplierTenths(int streak)
        {
            int tenths = MultiplierBaseTenths + (streak > 0 ? streak / MultiplierStepCatches : 0);
            return tenths > MultiplierMaxTenths ? MultiplierMaxTenths : tenths;
        }

        private static ChallengeRule Challenge(int one, int two, int three, int level, double rate = Keep, double fall = Keep,
            double pinecones = Keep, double width = Keep, double golden = Keep, double berriesOnly = Keep, double wind = Keep,
            double streakGoal = Keep, float seconds = 0f)
        {
            var overrides = new[] { rate, fall, pinecones, width, golden, berriesOnly, wind, streakGoal };
            return new ChallengeRule(new StarThresholds(one, two, three), level, overrides, seconds);
        }
    }
}
