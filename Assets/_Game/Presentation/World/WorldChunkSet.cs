using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// Chunk registry of one biome (doc 11, M-DEF-BIOME chunk group): Addressable chunk prefabs, their place in
    /// the world and the streaming window. Authoring data only, never mutated at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Pocket Wilds/World/Chunk Set", fileName = "DEF_ChunkSet_")]
    public sealed class WorldChunkSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Stable chunk id, also its Addressables address (chunk.<biome>.<name>).")]
            public string id;
            public AssetReferenceGameObject prefab;
            [Tooltip("World position of the chunk root.")]
            public Vector3 origin;
            [Tooltip("Always loaded and active: the sanctuary lives there.")]
            public bool pinned;
        }

        [SerializeField] private Entry[] _chunks = Array.Empty<Entry>();
        [Tooltip("Half size of a chunk footprint (m). Grassland chunks are 14 m squares.")]
        [SerializeField] private float _halfSize = 7f;
        [SerializeField] private StreamingWindow _window = StreamingWindow.Default;

        public int Count => _chunks.Length;
        public Entry Get(int index) => _chunks[index];
        public float HalfSize => _halfSize;
        public StreamingWindow Window => _window;

        public void SetAuthoring(Entry[] chunks, float halfSize, StreamingWindow window)
        {
            _chunks = chunks ?? Array.Empty<Entry>();
            _halfSize = halfSize;
            _window = window;
        }
    }

    /// <summary>
    /// Distances (m) between the camera's ground footprint and a chunk square. Hysteresis on both pairs:
    /// a chunk does not flicker in and out when the camera rests on a threshold.
    /// </summary>
    [Serializable]
    public struct StreamingWindow
    {
        [Tooltip("Start loading and instancing (inactive) when the footprint comes this close.")]
        public float preloadDistance;
        [Tooltip("Activate when this close: before the chunk can enter the frame.")]
        public float activateDistance;
        [Tooltip("Deactivate when farther than this.")]
        public float deactivateDistance;
        [Tooltip("Release (destroy and unload) when farther than this for releaseDelay seconds.")]
        public float releaseDistance;
        [Tooltip("Anti-thrash delay before a release (s).")]
        public float releaseDelay;

        public static StreamingWindow Default => new StreamingWindow
        {
            preloadDistance = 4f, activateDistance = 1f, deactivateDistance = 2f, releaseDistance = 6f, releaseDelay = 4f,
        };
    }
}
