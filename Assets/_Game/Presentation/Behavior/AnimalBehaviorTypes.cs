using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>Visible behavior of one animal. Social arrives with T25.</summary>
    public enum AnimalBehaviorState : byte
    {
        Idle = 0,
        IdleLook = 1,
        Wander = 2,
        Eat = 3,
        Sleep = 4,
        Wake = 5,
        Inspect = 6,
        React = 7,
        Happy = 8,
        Social = 9,
    }

    /// <summary>A: near and interactive, frequent ticks. B: background, rare ticks and a smaller state set.</summary>
    public enum SimulationTier : byte
    {
        A = 0,
        B = 1,
    }

    /// <summary>Light personality derived from the seed. Changes weights only, never the logic.</summary>
    public enum Temperament : byte
    {
        Playful = 0,
        Shy = 1,
        Curious = 2,
        Sleepy = 3,
        Brave = 4,
    }

    /// <summary>Animator state hashes shared by every family controller.</summary>
    public static class AnimalStateHashes
    {
        public static readonly int Idle = Animator.StringToHash("Idle");
        public static readonly int IdleLook = Animator.StringToHash("IdleLook");
        public static readonly int Move = Animator.StringToHash("Move");
        public static readonly int Happy = Animator.StringToHash("Happy");
        public static readonly int React = Animator.StringToHash("React");
        public static readonly int SleepEnter = Animator.StringToHash("SleepEnter");
        public static readonly int Wake = Animator.StringToHash("Wake");
        public static readonly int Eat = Animator.StringToHash("Eat");
        public static readonly int Social = Animator.StringToHash("Social");
        public static readonly int Inspect = Animator.StringToHash("Inspect");
    }

    /// <summary>Flat disk where animals may wander. One per chunk once streaming lands (T27).</summary>
    [Serializable]
    public struct WanderArea
    {
        public Vector3 Center;
        [Min(0.5f)] public float Radius;

        public WanderArea(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public Vector3 Clamp(Vector3 point)
        {
            var offset = point - Center;
            offset.y = 0f;
            float max = Radius;
            if (offset.sqrMagnitude > max * max)
                offset = offset.normalized * max;
            return new Vector3(Center.x + offset.x, Center.y, Center.z + offset.z);
        }
    }

    /// <summary>Scheduler budgets per quality tier (Performance Bible: 20-30 A on Low, more above).</summary>
    [Serializable]
    public struct SchedulerSettings
    {
        [Min(0)] public int MaxTierA;
        [Min(0.1f)] public float TickRateA;
        [Min(0.05f)] public float TickRateB;
        [Min(0.1f)] public float TierUpdateInterval;

        public static SchedulerSettings ForQuality(QualityTier tier, bool batterySaver)
        {
            int maxA = tier == QualityTier.Low ? 20 : tier == QualityTier.High ? 40 : 30;
            return new SchedulerSettings
            {
                MaxTierA = maxA,
                TickRateA = 5f,
                // Battery saver lowers the secondary tick rate (Performance Test Matrix).
                TickRateB = batterySaver ? 0.5f : 1f,
                TierUpdateInterval = 0.5f,
            };
        }
    }
}
