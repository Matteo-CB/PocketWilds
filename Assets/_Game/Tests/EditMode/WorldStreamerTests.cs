using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PocketWilds.Core;
using PocketWilds.Editor.Environment;
using PocketWilds.Editor.World;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.EditMode
{
    /// <summary>
    /// T27 WorldStreamer state machine with a fake chunk source: pinned start chunk, neighbour preload, activation
    /// before the frame, anti-thrash release, one transition of each kind per tick, failed chunks, no allocation.
    /// Plus the shipped data: chunk set, Addressables ownership and a ground seam that matches vertex for vertex.
    /// </summary>
    public class WorldStreamerTests
    {
        private sealed class FakeSource : IChunkSource
        {
            public int PollsBeforeReady;
            public readonly HashSet<string> Failing = new HashSet<string>();
            public readonly List<FakeRequest> Requests = new List<FakeRequest>();

            public IChunkRequest Request(in WorldChunkSet.Entry entry, Transform stagingParent)
            {
                var request = new FakeRequest(entry.id, stagingParent, PollsBeforeReady, Failing.Contains(entry.id));
                Requests.Add(request);
                return request;
            }
        }

        private sealed class FakeRequest : IChunkRequest
        {
            private readonly string _id;
            private readonly Transform _parent;
            private readonly bool _fail;
            private int _polls;
            private WorldChunk _chunk;

            public FakeRequest(string id, Transform parent, int polls, bool fail)
            {
                _id = id;
                _parent = parent;
                _polls = polls;
                _fail = fail;
            }

            public bool Released { get; private set; }
            public string Id => _id;

            public ChunkRequestStatus Status
            {
                get
                {
                    if (_fail)
                        return ChunkRequestStatus.Failed;
                    if (_polls-- > 0)
                        return ChunkRequestStatus.Pending;
                    if (_chunk == null)
                    {
                        var go = new GameObject(_id);
                        go.transform.SetParent(_parent, false);
                        _chunk = go.AddComponent<WorldChunk>();
                    }
                    return ChunkRequestStatus.Ready;
                }
            }

            public WorldChunk Chunk => _chunk;
            public string Error => _fail ? "missing" : null;

            public void Release()
            {
                Released = true;
                if (_chunk != null)
                    Object.DestroyImmediate(_chunk.gameObject);
                _chunk = null;
            }
        }

        private readonly List<Object> _cleanup = new List<Object>();
        private WorldStreamer _streamer;

        [TearDown]
        public void TearDown()
        {
            _streamer?.Dispose();
            _streamer = null;
            foreach (var o in _cleanup)
                if (o != null)
                    Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        /// <summary>Start pinned at the origin, meadow one chunk east, optionally a third one north.</summary>
        private WorldStreamer Create(FakeSource source, bool third = false)
        {
            var set = ScriptableObject.CreateInstance<WorldChunkSet>();
            _cleanup.Add(set);
            var entries = new List<WorldChunkSet.Entry>
            {
                new WorldChunkSet.Entry { id = "chunk.test.start", origin = Vector3.zero, pinned = true },
                new WorldChunkSet.Entry { id = "chunk.test.meadow", origin = new Vector3(14f, 0f, 0f) },
            };
            if (third)
                entries.Add(new WorldChunkSet.Entry { id = "chunk.test.north", origin = new Vector3(0f, 0f, 14f) });
            set.SetAuthoring(entries.ToArray(), 7f, StreamingWindow.Default);
            _streamer = new WorldStreamer(set, source, null, QualityTier.Medium);
            return _streamer;
        }

        /// <summary>A 4 x 6 m camera footprint whose east edge is at the given x.</summary>
        private static Rect FootprintEastEdgeAt(float x) => Rect.MinMaxRect(x - 4f, -3f, x, 3f);

        private static void TickN(WorldStreamer streamer, Rect footprint, ref float time, int ticks, float dt = 0.1f)
        {
            for (int i = 0; i < ticks; i++)
            {
                streamer.Tick(footprint, time);
                time += dt;
            }
        }

        [Test]
        public void PinnedStart_LoadsAndActivates_NeighbourStaysUnloaded()
        {
            var source = new FakeSource { PollsBeforeReady = 2 };
            var streamer = Create(source);
            float t = 0f;
            Assert.IsFalse(streamer.PinnedSettled);
            TickN(streamer, FootprintEastEdgeAt(0f), ref t, 6);
            Assert.IsTrue(streamer.PinnedSettled);
            Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(0));
            Assert.IsTrue(streamer.GetChunk(0).gameObject.activeInHierarchy);
            Assert.AreEqual(ChunkStreamState.Unloaded, streamer.GetState(1), "7 m away: beyond the 4 m preload distance.");
            Assert.AreEqual(1, source.Requests.Count);
        }

        [Test]
        public void Neighbour_IsStagedInactive_ThenActivatedBeforeItEntersTheFrame()
        {
            var source = new FakeSource { PollsBeforeReady = 1 };
            var streamer = Create(source);
            float t = 0f;
            TickN(streamer, FootprintEastEdgeAt(0f), ref t, 4);

            TickN(streamer, FootprintEastEdgeAt(4f), ref t, 4); // 3 m from the meadow: preload
            Assert.AreEqual(ChunkStreamState.Staged, streamer.GetState(1));
            var meadow = streamer.GetChunk(1);
            Assert.IsFalse(meadow.gameObject.activeSelf, "Staged chunks are instanced but inactive.");
            Assert.AreEqual(new Vector3(14f, 0f, 0f), meadow.transform.position);
            Assert.AreEqual(streamer.Root, meadow.transform.parent, "Moved out of the staging root.");

            TickN(streamer, FootprintEastEdgeAt(6.5f), ref t, 1); // 0.5 m: activate
            Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(1));
            Assert.IsTrue(meadow.gameObject.activeInHierarchy);
            Assert.AreEqual(0, streamer.Stats.lateActivations);
        }

        [Test]
        public void Release_WaitsForTheAntiThrashDelay_AndFreesTheInstance()
        {
            var source = new FakeSource();
            var streamer = Create(source);
            float t = 0f;
            TickN(streamer, FootprintEastEdgeAt(0f), ref t, 2);
            TickN(streamer, FootprintEastEdgeAt(6.5f), ref t, 4);
            Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(1));
            var request = source.Requests.Single(r => r.Id == "chunk.test.meadow");

            // Far west (7 + 3 = 10 m away): deactivated at once, released only after the delay.
            var far = FootprintEastEdgeAt(-3f);
            TickN(streamer, far, ref t, 1);
            Assert.AreEqual(ChunkStreamState.Staged, streamer.GetState(1));
            TickN(streamer, far, ref t, 30); // 3 s < 4 s
            Assert.AreEqual(ChunkStreamState.Staged, streamer.GetState(1));
            Assert.IsFalse(request.Released);
            TickN(streamer, far, ref t, 12);
            Assert.AreEqual(ChunkStreamState.Unloaded, streamer.GetState(1));
            Assert.IsTrue(request.Released);
            Assert.IsNull(streamer.GetChunk(1));
            Assert.AreEqual(1, streamer.Stats.releases);
            Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(0), "The pinned chunk is never released.");
        }

        [Test]
        public void ComingBackWithinTheDelay_KeepsTheChunk()
        {
            var source = new FakeSource();
            var streamer = Create(source);
            float t = 0f;
            TickN(streamer, FootprintEastEdgeAt(6.5f), ref t, 4);
            for (int cycle = 0; cycle < 5; cycle++)
            {
                TickN(streamer, FootprintEastEdgeAt(-3f), ref t, 20); // 2 s away
                TickN(streamer, FootprintEastEdgeAt(6.5f), ref t, 2);
            }
            Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(1));
            Assert.AreEqual(0, streamer.Stats.releases);
            Assert.AreEqual(2, source.Requests.Count, "Loaded once.");
        }

        [Test]
        public void OneLoadStartAndOneActivationPerTick()
        {
            var source = new FakeSource();
            var streamer = Create(source, third: true);
            float t = 0f;
            // A footprint touching the meadow and the north chunk at once: both want to load and activate.
            var both = Rect.MinMaxRect(3f, 3f, 7.5f, 7.5f);
            streamer.Tick(both, t);
            Assert.AreEqual(1, source.Requests.Count, "Only the pinned chunk starts on the first tick.");
            streamer.Tick(both, t += 0.1f);
            Assert.AreEqual(2, source.Requests.Count);
            streamer.Tick(both, t += 0.1f);
            Assert.AreEqual(3, source.Requests.Count);
            int activeBefore = Enumerable.Range(0, 3).Count(i => streamer.GetState(i) == ChunkStreamState.Active);
            streamer.Tick(both, t += 0.1f);
            int activeAfter = Enumerable.Range(0, 3).Count(i => streamer.GetState(i) == ChunkStreamState.Active);
            Assert.LessOrEqual(activeAfter - activeBefore, 1, "At most one activation per tick.");
            TickN(streamer, both, ref t, 4);
            Assert.IsTrue(Enumerable.Range(0, 3).All(i => streamer.GetState(i) == ChunkStreamState.Active));
        }

        [Test]
        public void FailedChunk_IsLeftOut_WithoutBreakingTheWorld()
        {
            var source = new FakeSource();
            source.Failing.Add("chunk.test.meadow");
            var streamer = Create(source);
            int failedEvents = 0;
            streamer.StateChanged += (i, s) => failedEvents += s == ChunkStreamState.Failed ? 1 : 0;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("chunk.test.meadow failed to load"));
            float t = 0f;
            TickN(streamer, FootprintEastEdgeAt(6.5f), ref t, 10);
            Assert.AreEqual(ChunkStreamState.Failed, streamer.GetState(1));
            Assert.AreEqual(ChunkStreamState.Active, streamer.GetState(0));
            Assert.AreEqual(1, failedEvents);
            Assert.AreEqual(1, streamer.Stats.failures);
            Assert.IsTrue(source.Requests.Single(r => r.Id == "chunk.test.meadow").Released);
            Assert.IsTrue(streamer.TryGetBounds(out var bounds));
            Assert.AreEqual(7f, bounds.xMax, 1e-4f, "The camera bounds shrink back to the chunks that exist.");
        }

        [Test]
        public void SteadyTicks_DoNotAllocate()
        {
            var source = new FakeSource();
            var streamer = Create(source, third: true);
            float t = 0f;
            var footprint = FootprintEastEdgeAt(6.5f);
            TickN(streamer, footprint, ref t, 20);
            long before = GC.GetAllocatedBytesForCurrentThread();
            TickN(streamer, footprint, ref t, 500);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void GroundFootprint_CoversTheFocusAndGrowsWithDistance()
        {
            var go = new GameObject("FootprintCamera");
            _cleanup.Add(go);
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 30f;
            camera.aspect = 540f / 1170f;
            var rotation = Quaternion.Euler(40f, 0f, 0f);
            var focus = new Vector3(3f, 0f, 1f);
            go.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 13f, rotation);
            var near = WorldStreamer.GroundFootprint(camera, 30f);
            Assert.IsTrue(near.Contains(new Vector2(focus.x, focus.z)));
            go.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 17f, rotation);
            var far = WorldStreamer.GroundFootprint(camera, 30f);
            Assert.Greater(far.width, near.width, "Zooming out widens the window.");
            Assert.AreEqual(0f, WorldStreamer.Distance(near, near));
            Assert.AreEqual(5f, WorldStreamer.Distance(new Rect(0f, 0f, 1f, 1f), new Rect(4f, 5f, 1f, 1f)), 1e-4f);
        }

        // ---------------- Shipped data ----------------

        [Test]
        public void GrasslandChunkSet_IsAddressable_WithOnePinnedStart()
        {
            var set = AssetDatabase.LoadAssetAtPath<WorldChunkSet>(WorldContentPipeline.ChunkSetPath);
            Assert.IsNotNull(set);
            // T33B: the strip runs east of the start: Meadow, StreamBend, Orchard, Hill, WindmillRise, PondEdge, ForestEdge.
            Assert.AreEqual(8, set.Count);
            for (int i = 1; i < set.Count; i++)
                Assert.AreEqual(set.Get(i - 1).origin + new Vector3(14f, 0f, 0f), set.Get(i).origin, set.Get(i).id);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.IsNotNull(settings);
            for (int i = 0; i < set.Count; i++)
            {
                var entry = set.Get(i);
                var asset = settings.FindAssetEntry(entry.prefab.AssetGUID);
                Assert.IsNotNull(asset, entry.id + " is not Addressable.");
                Assert.AreEqual(entry.id, asset.address);
                Assert.AreEqual(WorldContentPipeline.ChunksGroup, asset.parentGroup.Name);
                Assert.IsTrue(asset.labels.Contains(WorldContentPipeline.BiomeLabel));
            }
            Assert.IsTrue(set.Get(0).pinned && !set.Get(1).pinned);
            Assert.AreEqual(set.Get(0).origin + new Vector3(14f, 0f, 0f), set.Get(1).origin, "The meadow is the next chunk east.");

            // Shared kit dependencies have one owner: the kit group, the Bootstrap group for the world master shaders every biome
            // shares (T34C), or World_Shared for what the Forest uses too (T50).
            foreach (var path in WorldContentPipeline.SharedDependencies(WorldContentPipeline.GrasslandChunks.Select(c => c.path).ToArray()))
            {
                var asset = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                Assert.IsNotNull(asset, path + " is shared by both chunks but owned by no bundle.");
                string owner = path.EndsWith(".shader", StringComparison.Ordinal) ? CommonContentPipeline.BootstrapGroup : WorldContentPipeline.KitGroup;
                if (asset.parentGroup.Name != WorldContentPipeline.SharedGroup)
                    Assert.AreEqual(owner, asset.parentGroup.Name, path);
                Assert.IsFalse(path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase), "Kit FBX would drag the URP Lit material: " + path);
            }
        }

        [Test]
        public void GroundSeam_StartAndMeadowShareTheSameEdgeVertices()
        {
            Mesh Ground(string name) => AssetDatabase.LoadAllAssetsAtPath($"{GrasslandPipeline.ModelsFolder}/{name}.fbx").OfType<Mesh>().First();
            var a = Ground("GL_Ground_Core_A").vertices;
            var b = Ground("GL_Ground_Core_B").vertices;
            float offset = GrasslandPipeline.MeadowOffset.x;
            // Start's east edge (x = +7) against the meadow's west edge (x = -7), in world space.
            var east = new HashSet<Vector3Int>(a.Where(v => Mathf.Abs(v.x - offset * 0.5f) < 1e-3f && v.y > -0.01f).Select(Key));
            var west = new HashSet<Vector3Int>(b.Where(v => Mathf.Abs(v.x + offset * 0.5f) < 1e-3f && v.y > -0.01f)
                .Select(v => Key(v + new Vector3(offset, 0f, 0f))));
            Assert.GreaterOrEqual(east.Count, 15, "The joined side is a straight edge from corner to corner.");
            Assert.IsTrue(east.SetEquals(west), "Both grounds meet on the same vertices at the same height.");
        }

        private static Vector3Int Key(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x * 1000f), Mathf.RoundToInt(v.y * 1000f), Mathf.RoundToInt(v.z * 1000f));
    }
}
