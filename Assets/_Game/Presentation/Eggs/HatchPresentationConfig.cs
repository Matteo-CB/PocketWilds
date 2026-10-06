using System;
using PocketWilds.Content;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Hatch staging data (DEF_HatchPresentation): three phases, anticipation, crack, reveal (UI/UX doc).
    /// Timing lives here, not in code. Stronger rarities only gain a few hundred milliseconds.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Hatch Presentation", fileName = "DEF_HatchPresentation")]
    public sealed class HatchPresentationConfig : ScriptableObject
    {
        [Serializable]
        public struct FragmentFlight
        {
            [Tooltip("Initial velocity in egg space (m/s).")] public Vector3 velocity;
            [Tooltip("Spin in egg space (degrees/s).")] public Vector3 spin;
        }

        [Header("Prefabs")]
        public EggView eggPrefab;
        [Tooltip("Every feedback effect of the refuge (T34A): crack, hatch per rarity, egg tap, discoveries, mastery, hearts, rare sparkles.")]
        public VfxCatalog vfx;

        [Header("Waiting egg")]
        [Min(0f)] public float idlePulseScale = 0.03f;
        [Min(0.1f)] public float idlePulseHz = 1.1f;

        [Header("Timeline (seconds at speed 1)")]
        [Min(0.05f)] public float anticipation = 0.5f;
        [Min(0.05f)] public float afterCrack1 = 0.5f;
        [Min(0.05f)] public float afterCrack2 = 0.45f;
        [Min(0.05f)] public float reveal = 0.45f;
        [Min(0f)] public float celebrate = 0.55f;
        [Tooltip("Extra staging per rarity: Common, Uncommon, Rare, Epic, Legendary, Fabulous.")]
        public float[] rarityExtraSeconds = { 0f, 0f, 0.25f, 0.4f, 0.6f, 0.8f };
        [Tooltip("Repeated hatches play faster (UX: the player can speed up repetitions).")]
        [Min(1f)] public float repeatSpeed = 1.6f;

        [Header("Shell motion")]
        [Range(0f, 30f)] public float wobbleDegrees1 = 7f;
        [Range(0f, 30f)] public float wobbleDegrees2 = 11f;
        [Min(0.1f)] public float wobbleHz = 3.2f;
        [Range(0f, 0.4f)] public float crackSquash = 0.12f;
        [Range(0f, 0.3f)] public float swellBeforeBurst = 0.06f;
        [Min(0.05f)] public float swellSeconds = 0.25f;

        [Header("Fragments (no physics: preauthored ballistic flight)")]
        [Tooltip("Egg space, +Z is the egg front (toward the camera). Keep forward speeds small: shards must never cover the reveal.")]
        public FragmentFlight[] fragments = DefaultFragments();
        [Min(0.1f)] public float gravity = 5.5f;
        [Min(0.1f)] public float fragmentFlightSeconds = 0.55f;
        [Min(0.01f)] public float fragmentShrinkSeconds = 0.18f;

        [Header("Animal reveal")]
        [Min(1f)] public float revealOvershoot = 1.14f;
        [Min(0f)] public float happyDelay = 0.12f;

        /// <summary>A: top cap pops up and back. B: front half tips sideways to the left. C: back half falls right and back.</summary>
        public static FragmentFlight[] DefaultFragments() => new[]
        {
            new FragmentFlight { velocity = new Vector3(0.12f, 1.5f, -0.35f), spin = new Vector3(-150f, 40f, 80f) },
            new FragmentFlight { velocity = new Vector3(-0.75f, 0.55f, 0.08f), spin = new Vector3(120f, 0f, 200f) },
            new FragmentFlight { velocity = new Vector3(0.7f, 0.6f, -0.45f), spin = new Vector3(-110f, -30f, -190f) },
        };

        public float RarityExtra(RarityTier rarity)
        {
            int index = (int)rarity;
            return rarityExtraSeconds != null && index < rarityExtraSeconds.Length ? Mathf.Max(0f, rarityExtraSeconds[index]) : 0f;
        }
    }
}
