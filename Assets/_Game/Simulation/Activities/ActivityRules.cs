#nullable enable
using System;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// The scores for 1, 2 and 3 stars (doc 01 Contenu, section 12.0). Higher is better: an activity whose doc counts moves
    /// or time turns them into points first. The free mode's thresholds never change with d.
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
    /// straight lines between, rounded to <see cref="Decimals"/> digits (0: whole numbers).
    /// </summary>
    public readonly struct DifficultyParameter
    {
        /// <summary>A short name for tests and tools (« zones », « fall_speed »); never shown.</summary>
        public readonly string Name;

        public readonly double At1;
        public readonly double At5;
        public readonly double At10;
        public readonly int Decimals;

        public DifficultyParameter(string name, double at1, double at5, double at10, int decimals = 0)
        {
            Name = name;
            At1 = at1;
            At5 = at5;
            At10 = at10;
            Decimals = Math.Max(0, Math.Min(6, decimals));
        }

        public double At(int d) => AdaptiveDifficulty.Param(At1, At5, At10, d, Decimals);
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
    /// </summary>
    public sealed class ActivityRules
    {
        public readonly ActivityId Activity;
        public readonly StarThresholds Free;
        public readonly DifficultyParameter[] Parameters;
        public readonly ChallengeRule[] Challenges;

        public ActivityRules(ActivityId activity, StarThresholds free, DifficultyParameter[] parameters, ChallengeRule[] challenges)
        {
            if (ActivityCatalog.SlotOf(activity) < 0)
                throw new ArgumentException("Unknown activity " + activity + ".");
            if (challenges.Length != ActivityCatalog.ChallengesPerActivity)
                throw new ArgumentException("An activity has exactly " + ActivityCatalog.ChallengesPerActivity + " challenges.");
            Activity = activity;
            Free = free;
            Parameters = parameters;
            Challenges = challenges;
        }

        public int ParameterCount => Parameters.Length;

        /// <summary>Thresholds of a challenge (0 to 9, rewarded or practiced), else of the free mode (−1).</summary>
        public StarThresholds Thresholds(int challenge) =>
            (uint)challenge < (uint)Challenges.Length ? Challenges[challenge].Stars : Free;

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

        /// <summary>A challenge's duration, else the given default.</summary>
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
