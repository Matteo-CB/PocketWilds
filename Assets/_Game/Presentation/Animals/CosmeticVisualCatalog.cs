using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T47 presentation data of the cosmetics, built by the editor pipelines (AccessoryVisualPipeline, CozyBurrowPlacementPipeline):
    /// each accessory's prefab (one mesh in neutral rig space on the shared accessory material) with the palette row of each of its
    /// palette names, and the Cozy Burrow's prefab with its measured spot in the arrival chunk and its measured volume. Reached at
    /// runtime through <see cref="AnimalVisualCatalog.cosmetics"/>: data only, no scene reference.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Cosmetic Visual Catalog", fileName = "DEF_CosmeticVisualCatalog")]
    public sealed class CosmeticVisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class AccessoryEntry
        {
            [Tooltip("Accessory id, the same as the owned item, e.g. cosmetic.flower_crown.")]
            public string accessoryId = string.Empty;

            [Tooltip("Visual key of the accessory definition (ACC_FlowerCrown).")]
            public string visualKey = string.Empty;

            public GameObject prefab;

            [Tooltip("Palette names (meadow, blossom, frost).")]
            public List<string> palettes = new List<string>();

            [Tooltip("Palette row source id of each palette name, same order (DEF_Palettes_Cosmetics).")]
            public List<string> paletteRows = new List<string>();

            [Tooltip("Pieces made at load (Gate 10: nothing instantiates in play while within this count).")]
            [Min(0)] public int warm = 2;
        }

        public List<AccessoryEntry> accessories = new List<AccessoryEntry>();

        [Header("Cozy Burrow (decor.cozy_burrow)")]
        public string burrowItem = "decor.cozy_burrow";

        public GameObject burrowPrefab;
        public string burrowPalette = "palette.grassland.cozy_burrow";

        [Tooltip("True once CozyBurrowPlacementPipeline found and validated a free spot in the arrival chunk.")]
        public bool burrowPlaced;

        [Tooltip("Pivot of the burrow on the ground, chunk space.")]
        public Vector3 burrowPosition;

        [Tooltip("Yaw in degrees; the entrance faces the burrow's local +Z.")]
        public float burrowYaw;

        [Tooltip("Radius of the footprint: the burrow appears once no animal stands within it.")]
        [Min(0.1f)] public float burrowRadius = 0.62f;

        [Tooltip("The burrow's measured volume (height bands), chunk space: animals walk around it.")]
        public Obstacle[] burrowObstacles = Array.Empty<Obstacle>();

        [Tooltip("In front of the entrance, chunk space: where the companion may rest.")]
        public Vector3 burrowRestSpot;

        public AccessoryEntry Find(string accessoryId)
        {
            for (int i = 0; i < accessories.Count; i++)
                if (accessories[i] != null && accessories[i].accessoryId == accessoryId)
                    return accessories[i];
            return null;
        }
    }
}
