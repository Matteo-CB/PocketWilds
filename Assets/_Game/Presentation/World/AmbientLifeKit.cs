using System;
using UnityEngine;

namespace PocketWilds.Presentation
{
    public enum AmbientLifeBehaviour
    {
        /// <summary>Slow lazy loops around the anchor, soft bobbing.</summary>
        Butterfly,
        /// <summary>Quick zigzags close to the flowers.</summary>
        Bee,
        /// <summary>Hops and pecks on the grass; flies off in an arc when startled, lands back nearby.</summary>
        Bird,
        /// <summary>T48B slow drifting round glows of Soleil evenings (SeasonLookSet.fireflies), steered around every volume.</summary>
        Firefly,
    }

    /// <summary>
    /// The small life of a biome (DEF_AmbientLife_&lt;Biome&gt;): butterflies, bees, birds. Every species shares the
    /// biome's life material (SH_AmbientLife); species differ by mesh, palette, behaviour and wing flap.
    /// Chunks only provide anchors (VFXAnchors named after <see cref="Species.anchorPrefix"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/World/Ambient Life Kit", fileName = "DEF_AmbientLife_Biome")]
    public sealed class AmbientLifeKit : ScriptableObject
    {
        [Serializable]
        public struct Species
        {
            [Tooltip("Stable id, e.g. life.butterfly_a.")]
            public string id;
            public Mesh mesh;
            [Tooltip("ContentId value of palette.<biome>.life_<name>. Column 6 is the second variant color.")]
            public uint paletteId;
            public AmbientLifeBehaviour behaviour;
            [Tooltip("Chunk anchors that host this species: VFX anchors whose name starts with this prefix.")]
            public string anchorPrefix;
            [Tooltip("Individuals per anchor on Magnifique; tiers keep a share (WorldQualityProfile.ambientLifeDensity).")]
            [Range(1, 8)] public int perAnchor;
            [Tooltip("Share of the anchors that host this species (stable per anchor position): 1 everywhere, 0.3 for a rare one.")]
            [Range(0f, 1f)] public float spawnChance;
            [Tooltip("Uniform scale: tiny creatures are enlarged a little so they read from the gameplay camera.")]
            [Range(0.5f, 3f)] public float scale;
            [Tooltip("Wing flap in flight: speed (rad/s), amplitude (rad), rest angle (rad).")]
            public Vector3 flap;
            [Tooltip("Birds on the ground: wings folded (rest angle, no amplitude).")]
            public Vector3 flapGrounded;
            [Tooltip("Wing hinge in mesh space: x to the side, y up.")]
            public Vector2 shoulder;
        }

        [Tooltip("MAT_AmbientLife_<Biome> (PocketWilds/AmbientLife), GPU instancing on.")]
        public Material material;
        public Species[] species = Array.Empty<Species>();
    }
}
