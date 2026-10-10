using System;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T43: the plants' visuals (agent GP): for each plant id, its three stages as prefabs (stage 1 sprout, stage 2, stage 3
    /// ripe), each with its root at the soil and its own measured radius and height (nothing enters anything: the plant
    /// stays inside its bed).
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/Garden Plant Visuals")]
    public sealed class GardenPlantVisuals : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("The plant's runtime id (plant.sweet_clover).")]
            public string plantKey;

            public GameObject stage1;
            public GameObject stage2;
            public GameObject stage3;

            [Tooltip("The ripe plant's footprint radius and height (metres), measured by the pipeline.")]
            public float radius;

            public float height;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public bool TryGet(string plantKey, out Entry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].plantKey == plantKey)
                {
                    entry = entries[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
