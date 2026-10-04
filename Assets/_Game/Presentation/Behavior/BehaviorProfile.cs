using System;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Behavior timing and preferences for one species (serialized in DEF_AnimalVisualCatalog).
    /// Timing lives in data, not in clips: one-shot durations mirror the family clips so the
    /// scheduler hands over exactly when the Animator returns to Idle.
    /// Defaults match the SmallQuadruped library (T08).
    /// </summary>
    [Serializable]
    public sealed class BehaviorProfile
    {
        public const int ChoiceCount = 6;

        [Header("Choice weights after a state ends")]
        [Min(0f)] public float idleWeight = 0.20f;
        [Min(0f)] public float idleLookWeight = 0.18f;
        [Min(0f)] public float wanderWeight = 0.32f;
        [Min(0f)] public float eatWeight = 0.14f;
        [Min(0f)] public float inspectWeight = 0.10f;
        [Min(0f)] public float sleepWeight = 0.06f;

        [Header("Loop durations (seconds)")]
        public Vector2 idleSeconds = new Vector2(2.5f, 6f);
        public Vector2 eatSeconds = new Vector2(4f, 9f);
        public Vector2 sleepSeconds = new Vector2(10f, 24f);

        [Header("One-shot durations (clip length x exit time + fade)")]
        [Min(0.1f)] public float idleLookSeconds = 3.8f;
        [Min(0.1f)] public float inspectSeconds = 1.5f;
        [Min(0.1f)] public float reactSeconds = 0.67f;
        [Min(0.1f)] public float happySeconds = 1.04f;
        [Min(0.1f)] public float wakeSeconds = 0.67f;

        [Header("Movement")]
        [Tooltip("Ground speed matched to the hop clip so paws do not slide.")]
        [Min(0.05f)] public float moveSpeed = 0.38f;
        [Min(30f)] public float turnDegreesPerSecond = 300f;
        public Vector2 wanderDistance = new Vector2(0.5f, 1.6f);
        [Min(0f)] public float personalSpace = 0.45f;

        [Header("Tier B")]
        [Tooltip("Background animals stay calmer: longer states, shorter walks, no look/inspect one-shots.")]
        [Min(1f)] public float tierBDurationScale = 1.5f;
        [Range(0f, 1f)] public float tierBWanderScale = 0.5f;

        [Header("Transitions (seconds)")]
        [Min(0f)] public float fade = 0.2f;
        [Min(0f)] public float moveFade = 0.12f;
        [Min(0f)] public float reactFade = 0.06f;

        public float Weight(int choice)
        {
            switch (choice)
            {
                case 0: return idleWeight;
                case 1: return idleLookWeight;
                case 2: return wanderWeight;
                case 3: return eatWeight;
                case 4: return inspectWeight;
                default: return sleepWeight;
            }
        }

        /// <summary>Choice order used by <see cref="Weight"/> and the temperament table.</summary>
        public static AnimalBehaviorState ChoiceState(int choice)
        {
            switch (choice)
            {
                case 0: return AnimalBehaviorState.Idle;
                case 1: return AnimalBehaviorState.IdleLook;
                case 2: return AnimalBehaviorState.Wander;
                case 3: return AnimalBehaviorState.Eat;
                case 4: return AnimalBehaviorState.Inspect;
                default: return AnimalBehaviorState.Sleep;
            }
        }
    }

    /// <summary>How each temperament bends the same choice weights (doc 03: one state machine, other weights).</summary>
    public static class TemperamentTable
    {
        public const int Count = 5;

        // Columns: Idle, IdleLook, Wander, Eat, Inspect, Sleep.
        private static readonly float[] Multipliers =
        {
            0.8f, 1.0f, 1.5f, 1.0f, 1.1f, 0.5f, // Playful
            1.4f, 1.2f, 0.7f, 1.0f, 0.7f, 1.0f, // Shy
            0.9f, 1.5f, 1.1f, 0.9f, 2.0f, 0.7f, // Curious
            1.2f, 0.9f, 0.7f, 1.0f, 0.8f, 2.6f, // Sleepy
            0.9f, 0.9f, 1.3f, 1.0f, 1.3f, 0.8f, // Brave
        };

        private static readonly float[] HappyAfterTouch = { 0.6f, 0.15f, 0.4f, 0.3f, 0.55f };

        public static float Multiplier(Temperament temperament, int choice) =>
            Multipliers[(int)temperament * BehaviorProfile.ChoiceCount + choice];

        public static float HappyChance(Temperament temperament) => HappyAfterTouch[(int)temperament];
    }
}
