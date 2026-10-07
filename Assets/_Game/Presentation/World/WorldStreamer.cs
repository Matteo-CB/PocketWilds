using System;
using PocketWilds.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    public enum ChunkStreamState : byte
    {
        Unloaded,
        Loading,
        /// <summary>Instanced, palettes bound, inactive: activation is a single SetActive.</summary>
        Staged,
        Active,
        /// <summary>The load failed: the chunk is left out of the world (and of the camera bounds).</summary>
        Failed,
    }

    public enum ChunkRequestStatus : byte
    {
        Pending,
        Ready,
        Failed,
    }

    /// <summary>One load of one chunk: asset load, then instantiation under an inactive parent.</summary>
    public interface IChunkRequest
    {
        ChunkRequestStatus Status { get; }
        /// <summary>The instance, once Ready.</summary>
        WorldChunk Chunk { get; }
        string Error { get; }
        /// <summary>Destroys the instance and releases the asset. Safe while pending.</summary>
        void Release();
    }

    /// <summary>Where chunks come from: Addressables in the game, a fake in tests.</summary>
    public interface IChunkSource
    {
        IChunkRequest Request(in WorldChunkSet.Entry entry, Transform stagingParent);
    }

    [Serializable]
    public struct WorldStreamerStats
    {
        public int loads;
        public int activations;
        public int deactivations;
        public int releases;
        public int failures;
        /// <summary>Activations of a chunk the camera already saw: a visible pop-in. Should stay 0.</summary>
        public int lateActivations;
    }

    /// <summary>
    /// ChunkStreamer of the architecture doc: keeps a small window of chunks around the camera's ground footprint.
    /// Preloads the next chunk asynchronously (instanced inactive, palettes bound), activates it within budget
    /// before it can enter the frame, releases it once out of the window for an anti-thrash delay. At most one
    /// load start, one activation and one release per tick; every transition has a ProfilerMarker.
    /// A plain class ticked by the scene entry; nothing allocates per tick.
    /// </summary>
    public sealed class WorldStreamer : IDisposable
    {
        private struct Slot
        {
            public WorldChunkSet.Entry Entry;
            public Rect Area;
            public ChunkStreamState State;
            public IChunkRequest Request;
            public WorldChunk Chunk;
            public float LastNear;

            /// <summary>T35A: an open zone. Loaded and never released (its ground and obstacles stay for the animals),
            /// shown or hidden by distance like any chunk.</summary>
            public bool Resident;
        }

        private readonly Slot[] _slots;
        private readonly IChunkSource _source;
        private readonly StreamingWindow _window;
        private readonly Transform _root;
        private readonly Transform _staging;
        private readonly PaletteTable _palettes;
        private QualityTier _tier;
        private WorldStreamerStats _stats;
        private bool _disposed;

        /// <summary>Raised on every state change (index, new state). Transitions are rare: never per frame.</summary>
        public event Action<int, ChunkStreamState> StateChanged;

        public WorldStreamer(WorldChunkSet set, IChunkSource source, PaletteTable palettes, QualityTier tier, Transform parent = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _palettes = palettes;
            _tier = tier;
            _window = set.Window;
            _slots = new Slot[set.Count];
            float half = set.HalfSize;
            for (int i = 0; i < _slots.Length; i++)
            {
                var entry = set.Get(i);
                _slots[i] = new Slot
                {
                    Entry = entry,
                    Area = new Rect(entry.origin.x - half, entry.origin.z - half, half * 2f, half * 2f),
                    State = ChunkStreamState.Unloaded,
                    LastNear = float.NegativeInfinity,
                };
            }
            _root = new GameObject("WorldChunks").transform;
            _root.SetParent(parent, false);
            _staging = new GameObject("Staging").transform;
            _staging.SetParent(_root, false);
            _staging.gameObject.SetActive(false);
        }

        public int Count => _slots.Length;
        public WorldStreamerStats Stats => _stats;
        public Transform Root => _root;
        public StreamingWindow Window => _window;
        public ChunkStreamState GetState(int index) => _slots[index].State;
        public WorldChunk GetChunk(int index) => _slots[index].Chunk;
        public WorldChunkSet.Entry GetEntry(int index) => _slots[index].Entry;
        public Rect GetArea(int index) => _slots[index].Area;

        /// <summary>
        /// T35A: keeps a chunk loaded whatever the camera does (an open zone: animals walk on it), or lets it stream
        /// again. Activation still follows the camera, so a resident chunk out of sight is staged, not drawn.
        /// </summary>
        public void SetResident(int index, bool resident)
        {
            if (index >= 0 && index < _slots.Length)
                _slots[index].Resident = resident;
        }

        public bool IsResident(int index) => index >= 0 && index < _slots.Length && (_slots[index].Resident || _slots[index].Entry.pinned);

        /// <summary>Loaded (staged or active): its data can be read.</summary>
        public bool IsLoaded(int index) =>
            index >= 0 && index < _slots.Length && (_slots[index].State == ChunkStreamState.Staged || _slots[index].State == ChunkStreamState.Active);

        public int IndexOf(string id)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (string.Equals(_slots[i].Entry.id, id, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        /// <summary>True once every pinned chunk is active (or failed): the scene can start.</summary>
        public bool PinnedSettled
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    var s = _slots[i];
                    if (s.Entry.pinned && s.State != ChunkStreamState.Active && s.State != ChunkStreamState.Failed)
                        return false;
                }
                return true;
            }
        }

        /// <summary>Union of the chunk squares the world can show (failed chunks excluded).</summary>
        public bool TryGetBounds(out Rect bounds)
        {
            bounds = default;
            bool any = false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State == ChunkStreamState.Failed)
                    continue;
                var area = _slots[i].Area;
                bounds = any ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, area.xMin), Mathf.Min(bounds.yMin, area.yMin),
                    Mathf.Max(bounds.xMax, area.xMax), Mathf.Max(bounds.yMax, area.yMax)) : area;
                any = true;
            }
            return any;
        }

        /// <summary>Shortest distance between two xz rectangles (0 when they overlap).</summary>
        public static float Distance(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dz = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Bounding rectangle (x, z) of what the camera sees of the ground plane y = 0. Rays above the horizon
        /// count up to maxDistance. No allocation.
        /// </summary>
        public static Rect GroundFootprint(Camera camera, float maxDistance)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int corner = 0; corner < 4; corner++)
            {
                var ray = camera.ViewportPointToRay(new Vector3(corner & 1, corner >> 1, 0f));
                float t = maxDistance;
                if (ray.direction.y < -1e-4f)
                    t = Mathf.Min(maxDistance, -ray.origin.y / ray.direction.y);
                var p = ray.origin + ray.direction * t;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        public void Tick(Rect footprint, float time)
        {
            if (_disposed)
                return;
            using (GameProfilerMarkers.WorldStreamerTick.Auto())
            {
                bool started = false, activated = false, released = false;
                for (int i = 0; i < _slots.Length; i++)
                {
                    ref var s = ref _slots[i];
                    bool pinned = s.Entry.pinned;
                    bool kept = pinned || s.Resident;
                    float distance = Distance(footprint, s.Area);
                    if (kept || distance <= _window.releaseDistance)
                        s.LastNear = time;

                    switch (s.State)
                    {
                        case ChunkStreamState.Unloaded:
                            if (!started && (kept || distance <= _window.preloadDistance))
                            {
                                started = true;
                                Load(i, ref s);
                            }
                            break;
                        case ChunkStreamState.Loading:
                            Poll(i, ref s);
                            break;
                        case ChunkStreamState.Staged:
                            if (!activated && (pinned || distance <= _window.activateDistance))
                            {
                                activated = true;
                                Activate(i, ref s, late: !pinned && distance <= 0f);
                            }
                            else if (!released && !kept && time - s.LastNear >= _window.releaseDelay)
                            {
                                released = true;
                                Release(i, ref s);
                            }
                            break;
                        case ChunkStreamState.Active:
                            if (!pinned && distance > _window.deactivateDistance)
                                Deactivate(i, ref s);
                            break;
                    }
                }
            }
        }

        public void SetQuality(QualityTier tier)
        {
            _tier = tier;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Chunk != null)
                    _slots[i].Chunk.ApplyQuality(tier);
            }
        }

        private void Load(int index, ref Slot s)
        {
            using (GameProfilerMarkers.WorldStreamerLoad.Auto())
            {
                s.Request = _source.Request(in s.Entry, _staging);
                _stats.loads++;
                SetState(index, ref s, ChunkStreamState.Loading);
            }
        }

        private void Poll(int index, ref Slot s)
        {
            var status = s.Request.Status;
            if (status == ChunkRequestStatus.Pending)
                return;
            if (status == ChunkRequestStatus.Failed || s.Request.Chunk == null)
            {
                GameLog.Warning(LogCategory.Presentation, "Chunk " + s.Entry.id + " failed to load: " + s.Request.Error);
                s.Request.Release();
                s.Request = null;
                _stats.failures++;
                SetState(index, ref s, ChunkStreamState.Failed);
                return;
            }
            using (GameProfilerMarkers.WorldStreamerLoad.Auto())
            {
                // Still under the inactive staging root: switch the instance off first, then move it into the world.
                var chunk = s.Request.Chunk;
                chunk.gameObject.SetActive(false);
                chunk.transform.SetParent(_root, false);
                chunk.transform.SetLocalPositionAndRotation(s.Entry.origin, Quaternion.identity);
                if (_palettes != null)
                {
                    int missing = chunk.Bind(_palettes);
                    if (missing > 0)
                        GameLog.Warning(LogCategory.Presentation, missing + " renderers of " + s.Entry.id + " reference unknown palettes.");
                }
                chunk.ApplyQuality(_tier);
                s.Chunk = chunk;
                SetState(index, ref s, ChunkStreamState.Staged);
            }
        }

        private void Activate(int index, ref Slot s, bool late)
        {
            using (GameProfilerMarkers.WorldChunkActivate.Auto())
            {
                s.Chunk.gameObject.SetActive(true);
                _stats.activations++;
                if (late)
                    _stats.lateActivations++;
                SetState(index, ref s, ChunkStreamState.Active);
            }
        }

        private void Deactivate(int index, ref Slot s)
        {
            using (GameProfilerMarkers.WorldStreamerDeactivate.Auto())
            {
                s.Chunk.gameObject.SetActive(false);
                _stats.deactivations++;
                SetState(index, ref s, ChunkStreamState.Staged);
            }
        }

        private void Release(int index, ref Slot s)
        {
            using (GameProfilerMarkers.WorldStreamerRelease.Auto())
            {
                // Only inactive chunks are released: nothing renders between the asset release and the deferred Destroy.
                s.Request.Release();
                s.Request = null;
                s.Chunk = null;
                _stats.releases++;
                SetState(index, ref s, ChunkStreamState.Unloaded);
            }
        }

        private void SetState(int index, ref Slot s, ChunkStreamState state)
        {
            s.State = state;
            StateChanged?.Invoke(index, state);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Request?.Release();
                _slots[i].Request = null;
                _slots[i].Chunk = null;
                _slots[i].State = ChunkStreamState.Unloaded;
            }
            if (_root != null)
                DestroySafe(_root.gameObject);
        }

        /// <summary>Destroy in play mode, DestroyImmediate in edit-mode tests and tools.</summary>
        public static void DestroySafe(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
