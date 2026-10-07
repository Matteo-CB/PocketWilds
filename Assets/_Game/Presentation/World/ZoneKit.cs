using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T35A: what closes a zone of a biome (doc 02 Améliorations, section 4.6). A low hedge of the biome's round bushes
    /// across the zone's entrance and a wooden sign with the price; before the passage toward the next biome, the
    /// biome's gate instead of the sign (section 5.2). Built by ZonePipeline (DEF_ZoneKit_&lt;Biome&gt;,
    /// Addressables kit group); the meshes and the material are the biome kit's own, so nothing is duplicated.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/World/Zone Kit", fileName = "DEF_ZoneKit")]
    public sealed class ZoneKit : ScriptableObject
    {
        [Tooltip("Round bushes of the hedge, used in turn (the biome's low bushes).")]
        public Mesh[] bushes;

        public Material material;

        [Tooltip("ContentId value of the bushes' palette (palette.<biome>.<name>).")]
        public uint paletteId;

        [Range(0, 15)] public byte brightness = WorldChunk.NeutralBrightness;

        [Tooltip("Footprint radius of one bush seen from above (m), measured from the meshes.")]
        public float bushRadius = 0.45f;

        [Tooltip("Height of the bushes (m): the hedge's volume top.")]
        public float bushHeight = 0.6f;

        [Tooltip("Distance between two bushes along the hedge (m).")]
        public float spacing = 0.78f;

        [Tooltip("The wooden sign with the price (optional until it is modeled).")]
        public Mesh signMesh;

        public uint signPaletteId;
        [Range(0, 15)] public byte signBrightness = WorldChunk.NeutralBrightness;

        [Tooltip("Footprint radius of the sign seen from above (m).")]
        public float signRadius = 0.25f;

        public float signHeight = 1.1f;

        [Header("T35A biome gate: where the open land meets the passage toward the next biome")]
        [Tooltip("The gate (an arch of branches for the Forest), closed until the next biome opens.")]
        public Mesh gateMesh;

        public Material gateMaterial;
        public uint gatePaletteId;
        [Range(0, 15)] public byte gateBrightness = WorldChunk.NeutralBrightness;

        [Tooltip("Half the distance between the gate's two trunks, along its span (the mesh's x).")]
        public float gateHalfSpan = 1.6f;

        [Tooltip("Radius of a trunk at its foot (m), roots included.")]
        public float gateTrunkRadius = 0.34f;

        public float gateHeight = 3.6f;
    }
}
