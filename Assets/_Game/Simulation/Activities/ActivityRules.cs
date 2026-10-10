#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The scores for 1, 2 and 3 stars (doc 01 Contenu, section 12.0). Higher is better: an activity whose doc counts moves
    /// or time turns them into points first. The free mode's thresholds never change with d, except for an activity whose
    /// doc gives them per grid (T41 Pawprint Memory: <see cref="ActivityRules.FreeAt"/>).
    /// </summary>
    public readonly struct StarThresholds
    {
        public readonly int One;
        public readonly int Two;
        public readonly int Three;

        public StarThresholds(int one, int two, int three)
        {
            if (one <= 0 || two < one || three < two)
                throw new ArgumentException("Star thresholds must be positive and growing.");
            One = one;
            Two = two;
            Three = three;
        }

        public bool IsValid => One > 0;

        /// <summary>0 to 3 stars for a score.</summary>
        public int Stars(int score) => !IsValid ? 0 : score >= Three ? 3 : score >= Two ? 2 : score >= One ? 1 : 0;
    }

    /// <summary>
    /// One parameter of an activity given at d = 1, 5 and 10 (doc 01 Contenu, section 12.0 « Difficulté qui s'adapte »):
    /// straight lines between, rounded to <see cref="Decimals"/> digits (0: whole numbers). T41: a doc that gives a parameter
    /// in steps (« 4 × 3 (d1 à d3), 4 × 4 (d4 à d7), 5 × 4 (d8 à d10) ») gives one value per d instead (<see cref="Steps"/>).
    /// </summary>
    public readonly struct DifficultyParameter
    {
        /// <summary>A short name for tests and tools (« zones », « fall_speed »); never shown.</summary>
        public readonly string Name;

        public readonly double At1;
        public readonly double At5;
        public readonly double At10;
        public readonly int Decimals;

        /// <summary>T41: one value per d (index d − 1, 10 values), or null for the straight lines.</summary>
        public readonly double[]? ByLevel;

        public DifficultyParameter(string name, double at1, double at5, double at10, int decimals = 0)
        {
            Name = name;
            At1 = at1;
            At5 = at5;
            At10 = at10;
            Decimals = Math.Max(0, Math.Min(6, decimals));
            ByLevel = null;
        }

        private DifficultyParameter(string name, double[] byLevel)
        {
            Name = name;
            ByLevel = byLevel;
            At1 = byLevel[0];
            At5 = byLevel[4];
            At10 = byLevel[AdaptiveDifficulty.Max - 1];
            Decimals = 6;
        }

        /// <summary>T41: a parameter in steps, one value for each d from 1 to 10 (copied).</summary>
        public static DifficultyParameter Steps(string name, params double[] byLevel)
        {
            if (byLevel == null || byLevel.Length != AdaptiveDifficulty.Max)
                throw new ArgumentException("A stepped parameter has one value per d (" + AdaptiveDifficulty.Max + ").");
            return new DifficultyParameter(name, (double[])byLevel.Clone());
        }

        public double At(int d) =>
            ByLevel != null ? ByLevel[AdaptiveDifficulty.Clamp(d) - 1] : AdaptiveDifficulty.Param(At1, At5, At10, d, Decimals);
    }

    /// <summary>
    /// One of the 10 fixed challenges (doc 01 Contenu, section 12.0 « Défis »): its thresholds, the d its parameters start
    /// from, the parameters it fixes (NaN keeps the value at <see cref="Level"/>) and its own duration (0: the activity's).
    /// The goal text is « challenge.&lt;activity&gt;.&lt;nn&gt;.goal ». Challenges never adapt.
    /// </summary>
    public sealed class ChallengeRule
    {
        public readonly StarThresholds Stars;

        /// <summary>The d whose parameters the challenge starts from (« paramètres d1 », « d5 », « d10 »).</summary>
        public readonly int Level;

        /// <summary>Fixed values, by difficulty parameter index; NaN or missing keeps the value at <see cref="Level"/>.</summary>
        public readonly double[] Overrides;

        public readonly float Seconds;

        public ChallengeRule(StarThresholds stars, int level = 5, double[]? overrides = null, float seconds = 0f)
        {
            Stars = stars;
            Level = AdaptiveDifficulty.Clamp(level);
            Overrides = overrides ?? Array.Empty<double>();
            Seconds = Math.Max(0f, seconds);
        }
    }

    /// <summary>
    /// An activity's rules as data (doc « Mini-jeux &amp; activités », section 2 « Données »: ActivityDefinition's score
    /// rules and challenges): the free mode's star thresholds, the difficulty parameters at d = 1, 5 and 10, and the 10
    /// challenges. Each activity task builds its own and registers it in <see cref="ActivityRulesRegistry"/>; a theme
    /// never changes them.
    /// <para>T41: an activity whose doc gives the free mode's thresholds and length per grid (Pawprint Memory: « 4 × 3 : 500,
    /// 700, 850 ; 4 × 4 : 700, 950, 1 150 ; 5 × 4 : 900, 1 250, 1 500 », « 45 s en 4 × 3, 60 s en 4 × 4, 90 s en 5 × 4 »)
    /// gives them per d (<see cref="FreeByLevel"/>, <see cref="SecondsByLevel"/>); the runner reads them at the game's d
    /// (<see cref="Thresholds(int, int)"/>, <see cref="SecondsAt"/>), so the HUD and the result card follow.</para>
    /// </summary>
    public sealed class ActivityRules
    {
        public readonly ActivityId Activity;

        /// <summary>The free mode's thresholds (with <see cref="FreeByLevel"/>: those of the starting d, for tools and captures).</summary>
        public readonly StarThresholds Free;

        public readonly DifficultyParameter[] Parameters;
        public readonly ChallengeRule[] Challenges;

        /// <summary>T41: the free mode's thresholds for each d (index d − 1, 10 values), or null: <see cref="Free"/> at every d.</summary>
        public readonly StarThresholds[]? FreeByLevel;

        /// <summary>T41: the free mode's length for each d (index d − 1, 10 values), or null: the activity's own length.</summary>
        public readonly float[]? SecondsByLevel;

        public ActivityRules(ActivityId activity, StarThresholds free, DifficultyParameter[] parameters, ChallengeRule[] challenges,
            StarThresholds[]? freeByLevel = null, float[]? secondsByLevel = null)
        {
            if (ActivityCatalog.SlotOf(activity) < 0)
                throw new ArgumentException("Unknown activity " + activity + ".");
            if (challenges.Length != ActivityCatalog.ChallengesPerActivity)
                throw new ArgumentException("An activity has exactly " + ActivityCatalog.ChallengesPerActivity + " challenges.");
            if (freeByLevel != null && freeByLevel.Length != AdaptiveDifficulty.Max)
                throw new ArgumentException("Free thresholds per d need one entry per d (" + AdaptiveDifficulty.Max + ").");
            if (secondsByLevel != null && secondsByLevel.Length != AdaptiveDifficulty.Max)
                throw new ArgumentException("Seconds per d need one entry per d (" + AdaptiveDifficulty.Max + ").");
            Activity = activity;
            Free = free;
            Parameters = parameters;
            Challenges = challenges;
            FreeByLevel = freeByLevel != null ? (StarThresholds[])freeByLevel.Clone() : null;
            SecondsByLevel = secondsByLevel != null ? (float[])secondsByLevel.Clone() : null;
        }

        public int ParameterCount => Parameters.Length;

        /// <summary>Thresholds of a challenge (0 to 9, rewarded or practiced), else of the free mode (−1) at its starting d.</summary>
        public StarThresholds Thresholds(int challenge) =>
            (uint)challenge < (uint)Challenges.Length ? Challenges[challenge].Stars : Free;

        /// <summary>T41: thresholds of a challenge, else of the free mode at d (the runner's reading).</summary>
        public StarThresholds Thresholds(int challenge, int d) =>
            (uint)challenge < (uint)Challenges.Length ? Challenges[challenge].Stars : FreeAt(d);

        /// <summary>T41: the free mode's thresholds at d (<see cref="Free"/> when they do not change with d).</summary>
        public StarThresholds FreeAt(int d)
        {
            if (FreeByLevel == null)
                return Free;
            var thresholds = FreeByLevel[AdaptiveDifficulty.Clamp(d) - 1];
            return thresholds.IsValid ? thresholds : Free;
        }

        /// <summary>T41: the length of a game at d (its grid's), else <paramref name="fallback"/> (the activity's own length).</summary>
        public float SecondsAt(int d, float fallback)
        {
            if (SecondsByLevel == null)
                return fallback;
            float seconds = SecondsByLevel[AdaptiveDifficulty.Clamp(d) - 1];
            return seconds > 0f ? seconds : fallback;
        }

        /// <summary>
        /// A parameter of a game: the challenge's fixed value (or its level's), else the value at d. NaN for an unknown index.
        /// </summary>
        public double Value(int parameter, int d, int challenge = -1)
        {
            if ((uint)parameter >= (uint)Parameters.Length)
                return double.NaN;
            if ((uint)challenge < (uint)Challenges.Length)
            {
                var rule = Challenges[challenge];
                if (parameter < rule.Overrides.Length && !double.IsNaN(rule.Overrides[parameter]))
                    return rule.Overrides[parameter];
                return Parameters[parameter].At(rule.Level);
            }
            return Parameters[parameter].At(d);
        }

        /// <summary>A challenge's duration, else the given default (the runner gives the length at the challenge's d).</summary>
        public float ChallengeSeconds(int challenge, float fallback) =>
            (uint)challenge < (uint)Challenges.Length && Challenges[challenge].Seconds > 0f ? Challenges[challenge].Seconds : fallback;
    }

    /// <summary>
    /// The rules of the activities this build plays, by slot. The bootstrap registers each shipped activity's rules once;
    /// tests register the test activity's. Lookups never allocate.
    /// </summary>
    public sealed class ActivityRulesRegistry
    {
        private readonly ActivityRules?[] _rules = new ActivityRules?[ActivityCatalog.SlotCount];

        public void Register(ActivityRules rules) => _rules[ActivityCatalog.SlotOf(rules.Activity)] = rules;

        public bool TryGet(ActivityId activity, out ActivityRules rules)
        {
            int slot = ActivityCatalog.SlotOf(activity);
            rules = slot >= 0 ? _rules[slot]! : null!;
            return rules != null;
        }

        public bool Has(ActivityId activity)
        {
            int slot = ActivityCatalog.SlotOf(activity);
            return slot >= 0 && _rules[slot] != null;
        }
    }
}
