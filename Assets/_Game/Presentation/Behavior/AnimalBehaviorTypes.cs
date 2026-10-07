using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>Visible behavior of one animal. Presentation only, never saved.</summary>
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
        /// <summary>First at the meeting point: idles and turns toward the partner still on its way.</summary>
        SocialWait = 10,
    }

    /// <summary>
    /// T34B: a near animal (tier A) starts a state with a voice: a greeting, a moment of joy, falling asleep. The
    /// listener decides whether it is heard (the audio service caps and spaces animal voices for the whole refuge).
    /// </summary>
    public interface IAnimalVoiceListener
    {
        void AnimalVoice(AnimalView view, AnimalBehaviorState state, Vector3 position);
    }

    /// <summary>What an animal does at a point of interest.</summary>
    public enum InterestKind : byte
    {
        Inspect = 0,
        Eat = 1,
    }

    /// <summary>
    /// A rock, tree or flower patch animals walk to (T25). Authored as chunk data: no collider, no NavMesh.
    /// The animal stops standOff meters before the point, facing it.
    /// </summary>
    [Serializable]
    public struct InterestPoint
    {
        public Vector3 position;
        [Min(0f)] public float standOff;
        public InterestKind kind;

        public InterestPoint(Vector3 position, float standOff, InterestKind kind)
        {
            this.position = position;
            this.standOff = standOff;
            this.kind = kind;
        }
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

    /// <summary>
    /// Where animals may walk: a flat disk (a chunk's authored area), or a rectangle when <see cref="HalfSize"/> is set
    /// (T35A: the open zones of a biome side by side).
    /// </summary>
    [Serializable]
    public struct WanderArea
    {
        public Vector3 Center;
        [Min(0.5f)] public float Radius;

        [Tooltip("Half extents (x, z) of a rectangular area. Zero: the disk of Radius.")]
        public Vector2 HalfSize;

        public WanderArea(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
            HalfSize = Vector2.zero;
        }

        /// <summary>A rectangle from (minX, minZ) to (maxX, maxZ) at height y.</summary>
        public static WanderArea Rectangle(float minX, float minZ, float maxX, float maxZ, float y)
        {
            var area = new WanderArea(new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f),
                0.5f * Mathf.Max(maxX - minX, maxZ - minZ));
            area.HalfSize = new Vector2((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
            return area;
        }

        public bool IsRectangle => HalfSize.x > 0f && HalfSize.y > 0f;

        public Vector3 Clamp(Vector3 point)
        {
            if (IsRectangle)
                return new Vector3(Mathf.Clamp(point.x, Center.x - HalfSize.x, Center.x + HalfSize.x), Center.y,
                    Mathf.Clamp(point.z, Center.z - HalfSize.y, Center.z + HalfSize.y));
            var offset = point - Center;
            offset.y = 0f;
            float max = Radius;
            if (offset.sqrMagnitude > max * max)
                offset = offset.normalized * max;
            return new Vector3(Center.x + offset.x, Center.y, Center.z + offset.z);
        }

        public bool Contains(Vector3 point)
        {
            if (IsRectangle)
                return Mathf.Abs(point.x - Center.x) <= HalfSize.x && Mathf.Abs(point.z - Center.z) <= HalfSize.y;
            float dx = point.x - Center.x, dz = point.z - Center.z;
            return dx * dx + dz * dz <= Radius * Radius;
        }

        /// <summary>A point of the area from two uniform numbers in [0, 1) (uniform over a disk or a rectangle).</summary>
        public Vector3 PointAt(float u, float v)
        {
            if (IsRectangle)
                return new Vector3(Center.x + (u * 2f - 1f) * HalfSize.x, Center.y, Center.z + (v * 2f - 1f) * HalfSize.y);
            float angle = u * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(v) * Radius;
            return Center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
    }

    /// <summary>Scheduler budgets per quality tier (Performance Guide: 20-30 A on Low, more above).</summary>
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
