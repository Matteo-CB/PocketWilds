using System;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// One species' coat for grooming (doc 01 Contenu, S-ACT-001 « zones mesurées sur le modèle »), measured by
    /// GroomingPipeline on the species' LOD0 at bind pose, in the 3/4 view of <see cref="GroomingLayout"/>: the zones are
    /// points spread over the part of the coat the camera sees (farthest-point sampling; eyes, nose, paws and inner ears
    /// left out through the vertex-color regions; points hidden behind the body left out), each skinned to its bones like
    /// the mesh itself, so it follows the animation exactly. The groomed mesh is a copy of LOD0 carrying each vertex's zone
    /// (uv2.x) and bind position (uv3) for SH_AnimalMaster's _PW_DUST. All lengths at the animal's scale 1, in its root space.
    /// </summary>
    [Serializable]
    public sealed class GroomingBody
    {
        public const int Influences = 4;

        public string visualKey;

        /// <summary>LOD0 copy with the zone channel (uv2) and the bind positions (uv3).</summary>
        public Mesh groomMesh;

        public int zoneCount;

        /// <summary>Zone centers and normals at bind pose (root space): the game's distances, the framing, the fallbacks.</summary>
        public Vector3[] zonePoints = Array.Empty<Vector3>();

        public Vector3[] zoneNormals = Array.Empty<Vector3>();

        /// <summary>Touch radius of each zone (m): about 0.6 of its distance to the nearest other zone.</summary>
        public float[] zoneRadii = Array.Empty<float>();

        /// <summary>
        /// Zones where a prop could seem to float (an ear, a paw, a zone seen edge-on at the silhouette, a coat thinner than
        /// <c>GroomingPipeline.ThinCoat</c>): no debris, burr, bubble or golden brush lies there (collision rule: a burr on an
        /// ear's tip would hang in the air). They are still brushed.
        /// </summary>
        public bool[] zoneThin = Array.Empty<bool>();

        /// <summary>Per zone, <see cref="Influences"/> bones (index in the LOD0 renderer's bones, -1 unused) with their weights.</summary>
        public int[] zoneBones = Array.Empty<int>();

        public float[] zoneWeights = Array.Empty<float>();

        /// <summary>Per zone and influence: the zone point and normal in that bone's space (bindpose × vertex).</summary>
        public Vector3[] zoneLocalPoints = Array.Empty<Vector3>();

        public Vector3[] zoneLocalNormals = Array.Empty<Vector3>();

        /// <summary>The LOD0 renderer's bone names, in its order: the runtime finds its bones by name if needed.</summary>
        public string[] boneNames = Array.Empty<string>();

        /// <summary>Bind-pose bounds of the whole body (root space): the camera framing.</summary>
        public Bounds bounds;

        /// <summary>Footprint radius on the ground (the farthest paw from the root, horizontally).</summary>
        public float footRadius;

        /// <summary>
        /// The paws' outline on the ground (convex hull of the vertices under 4 cm, root space x and z): the sway is held so
        /// that every one of them stays on the cushion's flat top (collision rule).
        /// </summary>
        public Vector2[] footPoints = Array.Empty<Vector2>();

        /// <summary>
        /// The bow on SOCK_HeadTop (collision rule: on the head, never inside the head or the ears): its lift above the
        /// socket along the socket's up and its scale, measured against the head and ear vertices under its footprint.
        /// </summary>
        public float bowLift;

        public float bowScale = 1f;

        public bool HasSkin => zoneBones != null && zoneBones.Length >= zoneCount * Influences && boneNames != null && boneNames.Length > 0;
    }

    /// <summary>DEF_GroomingBodies: the measured coats of every species with a visual (built by GroomingPipeline).</summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Activities/Grooming Bodies", fileName = "DEF_GroomingBodies")]
    public sealed class GroomingBodyCatalog : ScriptableObject
    {
        public GroomingBody[] bodies = Array.Empty<GroomingBody>();

        public bool TryGet(string visualKey, out GroomingBody body)
        {
            if (bodies != null)
            {
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i] != null && bodies[i].visualKey == visualKey)
                    {
                        body = bodies[i];
                        return true;
                    }
                }
            }
            body = null;
            return false;
        }
    }
}
