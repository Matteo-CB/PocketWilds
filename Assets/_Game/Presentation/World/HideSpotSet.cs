using System;
using PocketWilds.Core;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>What a hiding spot stands behind (doc 01 Contenu, S-ACT-002 « Thèmes par biome », Prairie row and the kit).</summary>
    public enum HideDecorKind : byte
    {
        TallGrass = 0,
        FlowerBush = 1,
        Bush = 2,
        SmallTree = 3,
        Tree = 4,
        Rock = 5,
        Log = 6,
        Stump = 7,
        HayBale = 8,
        Fence = 9,
        Other = 10,
    }

    /// <summary>
    /// T39 « Cache-cache »: the hiding spots of one biome's chunks (doc Mini-jeux S-ACT-002 « chaque chunk a ses cachettes »,
    /// asset « marqueurs de cachettes dans les chunks (HideSpots) »; queue T39 « cachettes dans tous les chunks Prairie »),
    /// measured by the hide-spot pipeline on the chunks themselves. A game plays in the refuge: the camera frames one zone
    /// (one per chunk) and the player's animals stand in its spots. Each spot is a point on the ground beside a decor, the
    /// body never inside anything (Docs/Design/Collision_Rule.md); for every species the pipeline stores where the animal
    /// stands so that 8 %, 15 %, 20 % or 30 % of its silhouette stays visible from the zone's camera (its LOD0 in the Eat
    /// pose, the largest share over the loop, against the chunk's triangles), and the stage interpolates between those
    /// levels. A zone's spots are stored best first (the decor that hides every species most and meets the most levels
    /// leads; spots whose slide cannot reach a level come later): the stage hides its animals in the first spots and puts
    /// the decoys after them. Everything is in chunk space: chunks are only ever translated (<see cref="WorldChunk.Ground"/>), so a world point is
    /// the chunk's origin plus the stored point. Authoring data, never mutated at run time; frozen contract for T39.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Activities/Hide Spot Set", fileName = "DEF_HideSpots_")]
    public sealed class HideSpotSet : ScriptableObject
    {
        /// <summary>The measured levels of visible silhouette (doc 01: 30 % at d1, 15 % at d5, 8 % at d10; challenges 10 % and 20 %).</summary>
        public const int ShareLevels = 4;

        public static float ShareAt(int level) => level <= 0 ? 0.08f : level == 1 ? 0.15f : level == 2 ? 0.20f : 0.30f;

        /// <summary>One framed corner of a chunk: its camera and its spots (5 to 14, best first; the start chunk at least 4).</summary>
        [Serializable]
        public struct Zone
        {
            [Tooltip("The WorldChunkSet id of the chunk (chunk.grassland.meadow).")]
            public string chunkId;

            [Tooltip("The game's camera, chunk space (portrait, 3/4 high view).")]
            public Vector3 cameraPosition;

            public Quaternion cameraRotation;

            [Tooltip("Vertical field of view that holds every spot at the narrowest supported portrait aspect (9:21).")]
            public float fieldOfView;

            [Tooltip("Free ground where the found animals gather when every one is found, chunk space.")]
            public Vector3 gatherPoint;

            public int firstSpot;
            public int spotCount;
        }

        /// <summary>One hiding spot.</summary>
        [Serializable]
        public struct Spot
        {
            [Tooltip("Reference placement on the ground (a rabbit at 15 %), chunk space.")]
            public Vector3 position;

            [Tooltip("The animal's yaw (degrees) while hidden, chunk space.")]
            public float facing;

            [Tooltip("Free ground in front of the decor where the animal lands at its « coucou », chunk space.")]
            public Vector3 landing;

            [Tooltip("The decor it hides behind: the ground point under its center, chunk space.")]
            public Vector3 decorCenter;

            [Tooltip("Plan radius of the decor (m): the tap target and the hint's local wind.")]
            public float decorRadius;

            [Tooltip("Height of the decor above the ground (m): the tap target.")]
            public float decorHeight;

            public HideDecorKind decor;
        }

        [SerializeField] private Zone[] _zones = Array.Empty<Zone>();
        [SerializeField] private Spot[] _spots = Array.Empty<Spot>();
        [Tooltip("ContentId values of the species measured, in table order.")]
        [SerializeField] private uint[] _species = Array.Empty<uint>();
        [Tooltip("(spot × species + species index) × ShareLevels + level: the animal's ground point, chunk space.")]
        [SerializeField] private Vector3[] _placements = Array.Empty<Vector3>();
        [Tooltip("Same indexing: the share actually reached (when a level cannot be reached without overlap, the closest).")]
        [SerializeField] private float[] _measured = Array.Empty<float>();

        public int ZoneCount => _zones.Length;
        public Zone GetZone(int index) => _zones[index];
        public int SpotCount => _spots.Length;
        public Spot GetSpot(int index) => _spots[index];
        public int SpeciesCount => _species.Length;
        public ContentId GetSpecies(int index) => new ContentId(_species[index]);

        /// <summary>The zone of a chunk (−1: none). Linear: call when a game starts, not per frame.</summary>
        public int FindZone(string chunkId)
        {
            for (int i = 0; i < _zones.Length; i++)
                if (string.Equals(_zones[i].chunkId, chunkId, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        /// <summary>The species' row in the tables (−1: not measured, the stage then uses the spot's reference placement).</summary>
        public int SpeciesIndex(ContentId species)
        {
            for (int i = 0; i < _species.Length; i++)
                if (_species[i] == species.Value)
                    return i;
            return -1;
        }

        /// <summary>
        /// Where an animal of this species stands in this spot to leave <paramref name="share"/> of its silhouette visible
        /// (chunk space): the measured levels, interpolated, clamped to the first and the last.
        /// </summary>
        public Vector3 Placement(int spot, int species, float share)
        {
            if (species < 0 || species >= _species.Length || _placements.Length == 0)
                return _spots[spot].position;
            int baseIndex = (spot * _species.Length + species) * ShareLevels;
            if (share <= ShareAt(0))
                return _placements[baseIndex];
            for (int level = 1; level < ShareLevels; level++)
            {
                float hi = ShareAt(level);
                if (share <= hi)
                {
                    float lo = ShareAt(level - 1);
                    return Vector3.Lerp(_placements[baseIndex + level - 1], _placements[baseIndex + level], (share - lo) / (hi - lo));
                }
            }
            return _placements[baseIndex + ShareLevels - 1];
        }

        /// <summary>The share the pipeline measured at a level (tests and the validator).</summary>
        public float MeasuredShare(int spot, int species, int level) =>
            _measured.Length == 0 ? ShareAt(level) : _measured[(spot * _species.Length + species) * ShareLevels + level];

        /// <summary>Editor hook (the hide-spot pipeline). Run-time code never rewrites the set.</summary>
        public void SetAuthoring(Zone[] zones, Spot[] spots, uint[] species, Vector3[] placements, float[] measured)
        {
            _zones = zones ?? Array.Empty<Zone>();
            _spots = spots ?? Array.Empty<Spot>();
            _species = species ?? Array.Empty<uint>();
            _placements = placements ?? Array.Empty<Vector3>();
            _measured = measured ?? Array.Empty<float>();
        }
    }
}
