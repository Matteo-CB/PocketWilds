using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using PocketWilds.Editor.Environment;
using PocketWilds.Presentation;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PocketWilds.Editor.Activities
{
    /// <summary>
    /// The fast build (2026-10-12): the same measures as the one-by-one build, on every core but one, and two caches in
    /// Library/PocketWilds/HideSpots (one file per zone, never versioned):
    /// - the zone: its key holds everything a zone is measured from (this code, the species' posed meshes and bodies, the
    ///   chunk's triangles, decor, obstacles and ground, the strip's ground, the zone's focus and view); same key, same zone.
    /// - the measures: every share the zone's last build measured, keyed by what the meter reads (its triangles and camera,
    ///   the species' posed mesh, the ground point, the yaw, the frames). When a species or the zone code changes, the zone
    ///   is chosen again and only the shares that are new (the changed species, the new positions) are measured.
    /// The build profile (2026-10-12, 8 species, 8 zones, one thread: 724 s): 94 % in the meter, mostly rasterizing the
    /// animal's frames. A zone measured with the caches equals a full build bit for bit: <see cref="VerifyCache"/>.
    /// </summary>
    public static partial class HideSpotPipeline
    {
        /// <summary>Bumped when the cache files' layout changes (older files are then ignored).</summary>
        private const int CacheFormat = 2;
        private const int CacheSignature = 0x53485750; // "PWHS"

        /// <summary>Where the caches live (tests point it at a temporary folder).</summary>
        internal static string CacheFolder = "Library/PocketWilds/HideSpots";

        /// <summary>The code the zones are measured with: any change measures every zone again (choices, not shares).</summary>
        internal static readonly string[] ZoneSources =
        {
            "Assets/_Game/Editor/Activities/HideSpotPipeline.cs",
            "Assets/_Game/Editor/Activities/HideSpotPipeline.Zone.cs",
            "Assets/_Game/Editor/Activities/HideSpotPipeline.Meter.cs",
            "Assets/_Game/Editor/Activities/HideSpotPipeline.Cache.cs",
            "Assets/_Game/Presentation/World/WorldCollision.cs",
            "Assets/_Game/Presentation/World/HideSpotSet.cs",
        };

        /// <summary>The meter's own code: a change measures every share again.</summary>
        internal static readonly string[] MeasureSources = { "Assets/_Game/Editor/Activities/HideSpotPipeline.Meter.cs" };

        [MenuItem("Pocket Wilds/Activities/Verify Hide Spots Cache (T39)", priority = 242)]
        public static void VerifyMenu()
        {
            var problems = VerifyCache();
            if (problems.Any(p => p.StartsWith("cache:", StringComparison.Ordinal)))
                Debug.LogError("[HideSpotPipeline] The cache differs from a full build:\n" + string.Join("\n", problems));
            else
                Debug.Log("[HideSpotPipeline] Every cached zone equals a full build." + (problems.Count > 0 ? "\n" + string.Join("\n", problems) : ""));
        }

        // ---------------------------------------------------------------- keys

        /// <summary>A 128-bit digest (the first half of a SHA-256).</summary>
        internal readonly struct Key128 : IEquatable<Key128>
        {
            public readonly ulong A;
            public readonly ulong B;

            public Key128(ulong a, ulong b)
            {
                A = a;
                B = b;
            }

            public bool IsEmpty => A == 0 && B == 0;
            public bool Equals(Key128 other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is Key128 other && Equals(other);
            public override int GetHashCode() => unchecked((int)A ^ (int)(A >> 32) ^ (int)B * 397 ^ (int)(B >> 32));
            public override string ToString() => A.ToString("x16") + B.ToString("x16");
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public int Int;

            public static int Of(float value) => new FloatBits { Float = value }.Int;
        }

        /// <summary>Feeds values to a SHA-256, floats by their bits.</summary>
        internal sealed class Hasher : IDisposable
        {
            private readonly SHA256 _sha = SHA256.Create();
            private readonly byte[] _buffer = new byte[1 << 16];
            private int _used;

            public static Key128 Of(Action<Hasher> fill)
            {
                using (var hasher = new Hasher())
                {
                    fill(hasher);
                    return hasher.Finish();
                }
            }

            public Hasher Add(int value)
            {
                if (_used + 4 > _buffer.Length)
                {
                    _sha.TransformBlock(_buffer, 0, _used, null, 0);
                    _used = 0;
                }
                _buffer[_used++] = (byte)value;
                _buffer[_used++] = (byte)(value >> 8);
                _buffer[_used++] = (byte)(value >> 16);
                _buffer[_used++] = (byte)(value >> 24);
                return this;
            }

            public Hasher Add(ulong value) => Add((int)value).Add((int)(value >> 32));
            public Hasher Add(bool value) => Add(value ? 1 : 0);
            public Hasher Add(float value) => Add(FloatBits.Of(value));
            public Hasher Add(Vector2 v) => Add(v.x).Add(v.y);
            public Hasher Add(Vector3 v) => Add(v.x).Add(v.y).Add(v.z);
            public Hasher Add(Quaternion q) => Add(q.x).Add(q.y).Add(q.z).Add(q.w);
            public Hasher Add(Key128 key) => Add(key.A).Add(key.B);
            public Hasher Add(BodyShape b) => Add(b.front).Add(b.back).Add(b.halfWidth).Add(b.height);

            public Hasher Add(string text)
            {
                text = text ?? "";
                Add(text.Length);
                foreach (char c in text)
                    Add(c);
                return this;
            }

            public Hasher Add(List<Vector3> list)
            {
                Add(list.Count);
                foreach (var v in list)
                    Add(v);
                return this;
            }

            public Hasher Add(Vector3[] array)
            {
                Add(array.Length);
                foreach (var v in array)
                    Add(v);
                return this;
            }

            public Hasher Add(Vector2[] array)
            {
                Add(array.Length);
                foreach (var v in array)
                    Add(v);
                return this;
            }

            public Hasher Add(List<int> list)
            {
                Add(list.Count);
                foreach (int v in list)
                    Add(v);
                return this;
            }

            public Hasher Add(int[] array)
            {
                Add(array.Length);
                foreach (int v in array)
                    Add(v);
                return this;
            }

            public Hasher Add(float[] array)
            {
                Add(array.Length);
                foreach (float v in array)
                    Add(v);
                return this;
            }

            public Hasher Add(Color32[] array)
            {
                Add(array.Length);
                foreach (var c in array)
                    Add(c.r | c.g << 8 | c.b << 16 | c.a << 24);
                return this;
            }

            public Key128 Finish()
            {
                _sha.TransformFinalBlock(_buffer, 0, _used);
                var hash = _sha.Hash;
                return new Key128(BitConverter.ToUInt64(hash, 0), BitConverter.ToUInt64(hash, 8));
            }

            public void Dispose() => _sha.Dispose();
        }

        /// <summary>The sources' text (line ends ignored), so an edit to the measuring code measures again.</summary>
        private static void AddSources(Hasher hasher, string[] paths)
        {
            foreach (var path in paths)
            {
                hasher.Add(path);
                string full = Path.GetFullPath(path);
                hasher.Add(File.Exists(full) ? File.ReadAllText(full).Replace("\r\n", "\n") : "missing");
            }
        }

        /// <summary>What a cached share depends on besides its key: the meter's code and the engine's math.</summary>
        internal static Key128 MeasureVersion() =>
            Hasher.Of(h =>
            {
                h.Add("measures").Add(CacheFormat).Add(Application.unityVersion);
                AddSources(h, MeasureSources);
            });

        /// <summary>What a cached zone depends on besides its data: the code and the stage's frame rules.</summary>
        internal static Key128 CodeVersion() =>
            Hasher.Of(h =>
            {
                h.Add("zones").Add(CacheFormat).Add(Application.unityVersion);
                AddSources(h, ZoneSources);
                h.Add(HideSeekStage.WidestFittedAspect).Add(HideSeekLayout.NarrowestAspect).Add(HideSeekLayout.HudTopShare)
                    .Add(HideSeekLayout.HudBottomShare).Add(HideSeekLayout.SideShare).Add(HideSpotSet.ShareLevels);
                for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                    h.Add(HideSpotSet.ShareAt(level));
            });

        /// <summary>A species' keys: what the meter reads of it (its posed frames, triangles), then all a zone uses.</summary>
        internal static void KeySpecies(SpeciesModel s)
        {
            s.MeasureKey = Hasher.Of(h =>
            {
                h.Add(s.Frames.Length);
                foreach (var frame in s.Frames)
                    h.Add(frame);
                h.Add(s.SearchFrames.Length);
                foreach (var frame in s.SearchFrames)
                    h.Add(frame);
                h.Add(s.Triangles);
            });
            s.Key = Hasher.Of(h => h.Add(s.MeasureKey).Add((int)s.Id).Add(s.Name).Add(s.VisualKey).Add(s.Body).Add(s.HideBody).Add(s.Height).Add(s.Pose));
        }

        /// <summary>A chunk's geometry keys (its triangles, its ground).</summary>
        internal static void KeyWorld(ChunkWorld w)
        {
            w.GeometryKey = Hasher.Of(h => h.Add(w.Vertices).Add(w.Triangles));
            w.GroundKey = Hasher.Of(h => h.Add(w.GroundVertices).Add(w.GroundTriangles));
        }

        /// <summary>
        /// Everything one zone is measured from: the code, the species, the chunk as the pipeline sees it (triangles, ground and
        /// its paint, obstacles with the neighbours', decor and other props, egg, heart, height field), the strip's ground (the
        /// island's edge) and the zone's authored focus and view. Call <see cref="KeyWorld"/> on every chunk of the strip first.
        /// </summary>
        internal static Key128 ZoneKey(ChunkWorld w, Key128 code, List<SpeciesModel> species) =>
            Hasher.Of(h =>
            {
                h.Add(code).Add(species.Count);
                foreach (var s in species)
                    h.Add(s.Key);
                h.Add(w.Address).Add(w.Name).Add(w.Origin).Add(w.GeometryKey).Add(w.GroundKey).Add(w.GroundColors);
                h.Add(w.Obstacles.Count);
                foreach (var o in w.Obstacles)
                    h.Add(o.a).Add(o.b).Add(o.radius).Add(o.baseY).Add(o.bottom).Add(o.top).Add((int)o.kind);
                h.Add(w.Field != null);
                if (w.Field != null)
                    h.Add(w.Field.origin).Add(w.Field.cell).Add(w.Field.countX).Add(w.Field.countZ).Add(w.Field.heights ?? Array.Empty<float>());
                h.Add(w.Egg.HasValue).Add(w.Egg ?? Vector3.zero).Add(w.Heart);
                h.Add(w.Decor.Count);
                foreach (var d in w.Decor)
                {
                    h.Add(d.Prop).Add((int)d.Kind).Add(d.Center).Add(d.GroundReach).Add(d.Reach).Add(d.Height).Add(d.Corners).Add(d.GroundHull)
                        .Add(d.Vertices).Add(d.Triangles);
                }
                h.Add(w.OtherProps.Count);
                foreach (var (vertices, triangles) in w.OtherProps)
                    h.Add(vertices).Add(triangles);
                var strip = w.Strip ?? new List<ChunkWorld> { w };
                h.Add(strip.Count);
                foreach (var other in strip)
                    h.Add(other.Origin).Add(other.GroundKey);
                var focus = GrasslandPipeline.HideZoneFocusFor(w.Address);
                var view = GrasslandPipeline.HideZoneViewFor(w.Address);
                h.Add(focus.HasValue).Add(focus ?? Vector2.zero).Add(view.yaw).Add(view.elevation);
            });

        // ---------------------------------------------------------------- the measures cache

        internal readonly struct MemoKey : IEquatable<MemoKey>
        {
            public readonly Key128 Meter;
            public readonly Key128 Species;
            public readonly int X;
            public readonly int Y;
            public readonly int Z;
            public readonly int Yaw;
            public readonly bool All;

            public MemoKey(Key128 meter, Key128 species, Vector3 position, float yaw, bool all)
                : this(meter, species, FloatBits.Of(position.x), FloatBits.Of(position.y), FloatBits.Of(position.z), FloatBits.Of(yaw), all)
            {
            }

            public MemoKey(Key128 meter, Key128 species, int x, int y, int z, int yaw, bool all)
            {
                Meter = meter;
                Species = species;
                X = x;
                Y = y;
                Z = z;
                Yaw = yaw;
                All = all;
            }

            public bool Equals(MemoKey o) =>
                X == o.X && Y == o.Y && Z == o.Z && Yaw == o.Yaw && All == o.All && Meter.Equals(o.Meter) && Species.Equals(o.Species);

            public override bool Equals(object obj) => obj is MemoKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Meter.GetHashCode();
                    hash = hash * 31 + Species.GetHashCode();
                    hash = hash * 31 + X;
                    hash = hash * 31 + Y;
                    hash = hash * 31 + Z;
                    hash = hash * 31 + Yaw;
                    return hash * 2 + (All ? 1 : 0);
                }
            }
        }

        internal readonly struct MemoValue
        {
            public readonly float Share;
            public readonly Vector4 Box;

            public MemoValue(float share, Vector4 box)
            {
                Share = share;
                Box = box;
            }
        }

        /// <summary>
        /// The shares one zone measured (<see cref="ShareMeter.Measure"/>, exact bits), read from its last build and filled by
        /// this one, from any thread. Only what this build asked for is saved again.
        /// </summary>
        internal sealed class MeasureMemo
        {
            private readonly Dictionary<MemoKey, MemoValue> _old;
            private readonly ConcurrentDictionary<MemoKey, MemoValue> _now = new ConcurrentDictionary<MemoKey, MemoValue>();
            private int _hits;
            private int _misses;

            public MeasureMemo(Dictionary<MemoKey, MemoValue> old = null) => _old = old ?? new Dictionary<MemoKey, MemoValue>();

            public int Hits => _hits;
            public int Misses => _misses;
            public int Count => _now.Count;
            public IEnumerable<KeyValuePair<MemoKey, MemoValue>> Entries => _now;

            public bool TryGet(MemoKey key, out MemoValue value)
            {
                bool found = _now.TryGetValue(key, out value);
                if (!found && _old.TryGetValue(key, out value))
                {
                    _now.TryAdd(key, value);
                    found = true;
                }
                if (found)
                    Interlocked.Increment(ref _hits);
                else
                    Interlocked.Increment(ref _misses);
                return found;
            }

            public void Add(MemoKey key, MemoValue value) => _now.TryAdd(key, value);
        }

        // ---------------------------------------------------------------- the zone cache

        /// <summary>One zone as the asset stores it (its spots in order, their placements and shares), and its report.</summary>
        internal sealed class ZoneEntry
        {
            public Key128 Key;
            public Vector2 Focus;
            public HideSpotSet.Zone Zone;
            public HideSpotSet.Spot[] Spots;
            public Vector3[] Placements; // per spot, species, level (the asset's order)
            public float[] Measured;
            public int ReachableHits, Entries, Unreachable, FirstUnreachable, FirstEntries, Over40, Overlaps;
            public string Line;

            public static ZoneEntry From(ZoneResult zone, List<SpeciesModel> species, Key128 key, double seconds)
            {
                var placements = new List<Vector3>();
                var measured = new List<float>();
                foreach (var spot in zone.Spots)
                {
                    for (int s = 0; s < species.Count; s++)
                    {
                        for (int level = 0; level < HideSpotSet.ShareLevels; level++)
                        {
                            placements.Add(spot.Placements[s, level]);
                            measured.Add(spot.Measured[s, level]);
                        }
                    }
                }
                return new ZoneEntry
                {
                    Key = key, Focus = zone.Focus, Zone = zone.Zone, Spots = zone.Spots.Select(s => s.Spot).ToArray(), Placements = placements.ToArray(),
                    Measured = measured.ToArray(), ReachableHits = zone.ReachableHits, Entries = zone.Entries, Unreachable = zone.Unreachable,
                    FirstUnreachable = zone.FirstUnreachable, FirstEntries = zone.FirstEntries, Over40 = zone.Over40, Overlaps = zone.Overlaps,
                    Line = zone.Describe(species, seconds),
                };
            }

            public void Write(BinaryWriter w)
            {
                w.Write(Key.A);
                w.Write(Key.B);
                w.Write(Focus.x);
                w.Write(Focus.y);
                w.Write(Zone.chunkId ?? "");
                Write(w, Zone.cameraPosition);
                w.Write(Zone.cameraRotation.x);
                w.Write(Zone.cameraRotation.y);
                w.Write(Zone.cameraRotation.z);
                w.Write(Zone.cameraRotation.w);
                w.Write(Zone.fieldOfView);
                Write(w, Zone.gatherPoint);
                w.Write(Spots.Length);
                foreach (var s in Spots)
                {
                    Write(w, s.position);
                    w.Write(s.facing);
                    Write(w, s.landing);
                    Write(w, s.decorCenter);
                    w.Write(s.decorRadius);
                    w.Write(s.decorHeight);
                    w.Write((byte)s.decor);
                }
                w.Write(Placements.Length);
                foreach (var p in Placements)
                    Write(w, p);
                w.Write(Measured.Length);
                foreach (float m in Measured)
                    w.Write(m);
                foreach (int n in new[] { ReachableHits, Entries, Unreachable, FirstUnreachable, FirstEntries, Over40, Overlaps })
                    w.Write(n);
                w.Write(Line ?? "");
            }

            public static ZoneEntry Read(BinaryReader r)
            {
                var e = new ZoneEntry { Key = new Key128(r.ReadUInt64(), r.ReadUInt64()), Focus = new Vector2(r.ReadSingle(), r.ReadSingle()) };
                e.Zone.chunkId = r.ReadString();
                e.Zone.cameraPosition = ReadVector3(r);
                e.Zone.cameraRotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                e.Zone.fieldOfView = r.ReadSingle();
                e.Zone.gatherPoint = ReadVector3(r);
                e.Spots = new HideSpotSet.Spot[r.ReadInt32()];
                for (int i = 0; i < e.Spots.Length; i++)
                {
                    e.Spots[i] = new HideSpotSet.Spot
                    {
                        position = ReadVector3(r), facing = r.ReadSingle(), landing = ReadVector3(r), decorCenter = ReadVector3(r),
                        decorRadius = r.ReadSingle(), decorHeight = r.ReadSingle(), decor = (HideDecorKind)r.ReadByte(),
                    };
                }
                e.Placements = new Vector3[r.ReadInt32()];
                for (int i = 0; i < e.Placements.Length; i++)
                    e.Placements[i] = ReadVector3(r);
                e.Measured = new float[r.ReadInt32()];
                for (int i = 0; i < e.Measured.Length; i++)
                    e.Measured[i] = r.ReadSingle();
                e.ReachableHits = r.ReadInt32();
                e.Entries = r.ReadInt32();
                e.Unreachable = r.ReadInt32();
                e.FirstUnreachable = r.ReadInt32();
                e.FirstEntries = r.ReadInt32();
                e.Over40 = r.ReadInt32();
                e.Overlaps = r.ReadInt32();
                e.Line = r.ReadString();
                return e;
            }

            /// <summary>The differences with another entry, bit for bit (the report line and the key left out).</summary>
            public List<string> Differences(ZoneEntry other)
            {
                var list = new List<string>();
                string name = Zone.chunkId;
                if (Zone.chunkId != other.Zone.chunkId)
                    list.Add($"{name}: chunk {other.Zone.chunkId}");
                if (!Same(Focus.x, other.Focus.x) || !Same(Focus.y, other.Focus.y))
                    list.Add($"{name}: focus");
                if (!Same(Zone.cameraPosition, other.Zone.cameraPosition) || !Same(Zone.cameraRotation, other.Zone.cameraRotation)
                    || !Same(Zone.fieldOfView, other.Zone.fieldOfView) || !Same(Zone.gatherPoint, other.Zone.gatherPoint))
                    list.Add($"{name}: camera or gathering place");
                if (Spots.Length != other.Spots.Length)
                    list.Add($"{name}: {Spots.Length} spots against {other.Spots.Length}");
                else
                    for (int i = 0; i < Spots.Length; i++)
                        if (!Same(Spots[i], other.Spots[i]))
                            list.Add($"{name}: spot {i}");
                if (Placements.Length != other.Placements.Length || Enumerable.Range(0, Placements.Length).Any(i => !Same(Placements[i], other.Placements[i])))
                    list.Add($"{name}: placements");
                if (Measured.Length != other.Measured.Length || Enumerable.Range(0, Measured.Length).Any(i => !Same(Measured[i], other.Measured[i])))
                    list.Add($"{name}: measured shares");
                return list;
            }

            private static void Write(BinaryWriter w, Vector3 v)
            {
                w.Write(v.x);
                w.Write(v.y);
                w.Write(v.z);
            }

            private static Vector3 ReadVector3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        internal static bool Same(float a, float b) => FloatBits.Of(a) == FloatBits.Of(b);
        internal static bool Same(Vector3 a, Vector3 b) => Same(a.x, b.x) && Same(a.y, b.y) && Same(a.z, b.z);
        internal static bool Same(Quaternion a, Quaternion b) => Same(a.x, b.x) && Same(a.y, b.y) && Same(a.z, b.z) && Same(a.w, b.w);

        internal static bool Same(HideSpotSet.Spot a, HideSpotSet.Spot b) =>
            Same(a.position, b.position) && Same(a.facing, b.facing) && Same(a.landing, b.landing) && Same(a.decorCenter, b.decorCenter)
            && Same(a.decorRadius, b.decorRadius) && Same(a.decorHeight, b.decorHeight) && a.decor == b.decor;

        internal static string ZoneFile(string address) =>
            Path.Combine(CacheFolder, string.Concat(address.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' ? c : '_')) + ".bin");

        /// <summary>
        /// Reads a zone's cache file: the zone when its key is <paramref name="key"/>, else null; the shares it measured when
        /// the meter is unchanged (<paramref name="measureVersion"/>), else null. A missing or unreadable file reads as empty.
        /// </summary>
        internal static ZoneEntry ReadZoneFile(string address, Key128 key, Key128 measureVersion, out Dictionary<MemoKey, MemoValue> shares)
        {
            shares = null;
            string path = ZoneFile(address);
            if (!File.Exists(path))
                return null;
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (reader.ReadInt32() != CacheSignature || reader.ReadInt32() != CacheFormat)
                        return null;
                    int length = reader.ReadInt32();
                    long start = reader.BaseStream.Position;
                    var entry = ZoneEntry.Read(reader);
                    reader.BaseStream.Position = start + length;
                    if (entry.Key.Equals(key))
                        return entry;
                    if (!new Key128(reader.ReadUInt64(), reader.ReadUInt64()).Equals(measureVersion))
                        return null;
                    var meters = new Key128[reader.ReadInt32()];
                    for (int i = 0; i < meters.Length; i++)
                        meters[i] = new Key128(reader.ReadUInt64(), reader.ReadUInt64());
                    var species = new Key128[reader.ReadInt32()];
                    for (int i = 0; i < species.Length; i++)
                        species[i] = new Key128(reader.ReadUInt64(), reader.ReadUInt64());
                    int count = reader.ReadInt32();
                    shares = new Dictionary<MemoKey, MemoValue>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var memo = new MemoKey(meters[reader.ReadInt32()], species[reader.ReadInt32()], reader.ReadInt32(), reader.ReadInt32(),
                            reader.ReadInt32(), reader.ReadInt32(), reader.ReadBoolean());
                        shares[memo] = new MemoValue(reader.ReadSingle(), new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                    }
                    return null;
                }
            }
            catch (Exception e) when (e is IOException || e is EndOfStreamException || e is IndexOutOfRangeException || e is ArgumentException)
            {
                shares = null;
                return null;
            }
        }

        /// <summary>Writes a zone's cache file (the zone, then the shares its build measured), through a temporary file.</summary>
        internal static void WriteZoneFile(ZoneEntry entry, Key128 measureVersion, MeasureMemo memo)
        {
            string path = ZoneFile(entry.Zone.chunkId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                entry.Write(writer);
                writer.Flush();
                var zone = stream.ToArray();
                using (var file = new BinaryWriter(File.Create(temp)))
                {
                    file.Write(CacheSignature);
                    file.Write(CacheFormat);
                    file.Write(zone.Length);
                    file.Write(zone);
                    file.Write(measureVersion.A);
                    file.Write(measureVersion.B);
                    var entries = memo != null ? memo.Entries.ToList() : new List<KeyValuePair<MemoKey, MemoValue>>();
                    var meters = new Dictionary<Key128, int>();
                    var species = new Dictionary<Key128, int>();
                    foreach (var pair in entries)
                    {
                        if (!meters.ContainsKey(pair.Key.Meter))
                            meters[pair.Key.Meter] = meters.Count;
                        if (!species.ContainsKey(pair.Key.Species))
                            species[pair.Key.Species] = species.Count;
                    }
                    foreach (var table in new[] { meters, species })
                    {
                        file.Write(table.Count);
                        foreach (var key in table.OrderBy(p => p.Value).Select(p => p.Key))
                        {
                            file.Write(key.A);
                            file.Write(key.B);
                        }
                    }
                    file.Write(entries.Count);
                    foreach (var pair in entries)
                    {
                        var k = pair.Key;
                        file.Write(meters[k.Meter]);
                        file.Write(species[k.Species]);
                        file.Write(k.X);
                        file.Write(k.Y);
                        file.Write(k.Z);
                        file.Write(k.Yaw);
                        file.Write(k.All);
                        file.Write(pair.Value.Share);
                        file.Write(pair.Value.Box.x);
                        file.Write(pair.Value.Box.y);
                        file.Write(pair.Value.Box.z);
                        file.Write(pair.Value.Box.w);
                    }
                }
            }
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
        }

        // ---------------------------------------------------------------- measuring the zones

        /// <summary>How the zones were obtained (for the report and the checks).</summary>
        internal sealed class MeasureStats
        {
            public int Cached;
            public int Measured;
            public int ShareHits;
            public int ShareMisses;
            public double KeySeconds;
            public double MeasureSeconds;
        }

        /// <summary>
        /// The zones of these chunks (in order): read from the zone cache when <paramref name="readCache"/> and the key matches,
        /// else measured on every core (the shares of the zone's last build read back when <paramref name="readCache"/>, and
        /// each share measured once per build unless <paramref name="memo"/> is off); written back to the cache when
        /// <paramref name="writeCache"/>. Main thread.
        /// </summary>
        internal static ZoneEntry[] MeasureZones(List<ChunkWorld> worlds, List<SpeciesModel> species, bool readCache, bool writeCache, MeasureStats stats,
            bool memo = true)
        {
            var watch = Stopwatch.StartNew();
            var code = CodeVersion();
            var measureVersion = MeasureVersion();
            foreach (var s in species)
                KeySpecies(s);
            foreach (var w in worlds)
                foreach (var other in w.Strip ?? new List<ChunkWorld>())
                    if (other.GroundKey.IsEmpty)
                        KeyWorld(other);
            foreach (var w in worlds)
                if (w.GeometryKey.IsEmpty)
                    KeyWorld(w);
            var keys = worlds.Select(w => ZoneKey(w, code, species)).ToArray();
            stats.KeySeconds = watch.Elapsed.TotalSeconds;

            var entries = new ZoneEntry[worlds.Count];
            var todo = new List<int>();
            var memos = new List<MeasureMemo>();
            for (int w = 0; w < worlds.Count; w++)
            {
                Dictionary<MemoKey, MemoValue> shares = null;
                var cached = readCache ? ReadZoneFile(worlds[w].Address, keys[w], measureVersion, out shares) : null;
                if (cached != null)
                {
                    cached.Line += " (cached)";
                    entries[w] = cached;
                    stats.Cached++;
                    continue;
                }
                todo.Add(w);
                memos.Add(memo ? new MeasureMemo(readCache ? shares : null) : null);
            }
            if (todo.Count == 0)
                return entries;

            watch.Restart();
            var zones = BuildZones(todo.Select(i => worlds[i]).ToList(), species, memos, out var cpu, "Hide spots (T39)");
            stats.MeasureSeconds = watch.Elapsed.TotalSeconds;
            for (int k = 0; k < todo.Count; k++)
            {
                int w = todo[k];
                entries[w] = ZoneEntry.From(zones[k], species, keys[w], cpu[k]);
                stats.Measured++;
                stats.ShareHits += memos[k]?.Hits ?? 0;
                stats.ShareMisses += memos[k]?.Misses ?? 0;
                if (writeCache)
                    WriteZoneFile(entries[w], measureVersion, memos[k]);
            }
            return entries;
        }

        /// <summary>
        /// The zones of these chunks measured on every core but one, each equal to <see cref="BuildZone"/>'s: every focus's
        /// candidates (each decor, each side) in one queue, focus after focus; a focus's choice runs as soon as its last
        /// candidate is in (its meters are then let go); each zone keeps the focus <see cref="Pick"/> keeps.
        /// <paramref name="cpuSeconds"/>: the time its measures took, summed over the threads.
        /// </summary>
        internal static ZoneResult[] BuildZones(List<ChunkWorld> worlds, List<SpeciesModel> species, IList<MeasureMemo> memos, out double[] cpuSeconds,
            string progressTitle, Func<ChunkWorld, Vector2[]> focusesOf = null)
        {
            if (Trace != null)
                throw new InvalidOperationException("Trace is for the one-by-one diagnostics (Diagnose, CompareFocuses, Preview).");
            var attempts = new List<(int world, Vector2 focus)>();
            for (int w = 0; w < worlds.Count; w++)
                foreach (var focus in (focusesOf ?? FocusesOf)(worlds[w]))
                    attempts.Add((w, focus));
            var contexts = new Lazy<AttemptContext>[attempts.Count];
            var results = new ZoneResult[attempts.Count];
            var remaining = new int[attempts.Count];
            var ticks = new long[worlds.Count];
            var items = new List<(int attempt, int decor, int side)>();
            for (int a = 0; a < attempts.Count; a++)
            {
                var (w, focus) = attempts[a];
                var world = worlds[w];
                var memo = memos?[w];
                contexts[a] = new Lazy<AttemptContext>(() => BeginAttempt(world, species, focus, memo));
                remaining[a] = world.Decor.Count * 2;
                for (int d = 0; d < world.Decor.Count; d++)
                    for (int side = 0; side < 2; side++)
                        items.Add((a, d, side));
            }
            RunParallel(progressTitle, items.Count, i =>
            {
                long start = Stopwatch.GetTimestamp();
                var (a, d, side) = items[i];
                var context = contexts[a].Value;
                MeasureCandidate(context, d, side);
                if (Interlocked.Decrement(ref remaining[a]) == 0)
                {
                    context.Meter = null;
                    context.Occlusion = null;
                    results[a] = FinishAttempt(context);
                }
                Interlocked.Add(ref ticks[attempts[a].world], Stopwatch.GetTimestamp() - start);
            });
            for (int a = 0; a < attempts.Count; a++)
                if (results[a] == null)
                    results[a] = FinishAttempt(contexts[a].Value); // a chunk without decor
            var zones = new ZoneResult[worlds.Count];
            for (int w = 0; w < worlds.Count; w++)
                zones[w] = Pick(Enumerable.Range(0, attempts.Count).Where(a => attempts[a].world == w).Select(a => results[a]).ToList());
            cpuSeconds = ticks.Select(t => t / (double)Stopwatch.Frequency).ToArray();
            return zones;
        }

        /// <summary>
        /// Runs <paramref name="body"/> for 0 to count - 1 on every core but one (below normal priority), the main thread
        /// waiting with a progress bar that can cancel. The body never touches Unity's API but its thread-safe math.
        /// </summary>
        internal static void RunParallel(string progressTitle, int count, Action<int> body)
        {
            int next = -1, done = 0, cancel = 0;
            Exception error = null;
            int threads = Math.Max(1, Math.Min(System.Environment.ProcessorCount - 1, count));
            var workers = new Thread[threads];
            for (int t = 0; t < threads; t++)
            {
                workers[t] = new Thread(() =>
                {
                    try
                    {
                        while (Volatile.Read(ref cancel) == 0 && Volatile.Read(ref error) == null)
                        {
                            int i = Interlocked.Increment(ref next);
                            if (i >= count)
                                break;
                            body(i);
                            Interlocked.Increment(ref done);
                        }
                    }
                    catch (Exception e)
                    {
                        Interlocked.CompareExchange(ref error, e, null);
                    }
                })
                {
                    IsBackground = true, Name = "Hide spots " + t, Priority = System.Threading.ThreadPriority.BelowNormal,
                };
                workers[t].Start();
            }
            try
            {
                foreach (var worker in workers)
                {
                    while (!worker.Join(100))
                    {
                        if (progressTitle != null && EditorUtility.DisplayCancelableProgressBar(progressTitle,
                                $"{Volatile.Read(ref done)} of {count} candidates on {threads} threads", Volatile.Read(ref done) / (float)count))
                            Interlocked.Exchange(ref cancel, 1);
                    }
                }
            }
            finally
            {
                if (progressTitle != null)
                    EditorUtility.ClearProgressBar();
            }
            if (error != null)
                throw new InvalidOperationException("Hide spots: a measuring thread failed: " + error.Message, error);
            if (cancel != 0)
                throw new OperationCanceledException("Hide spots: cancelled.");
        }

        // ---------------------------------------------------------------- the quick check

        /// <summary>
        /// The listed zones (every zone when none) measured again from scratch (no cache), each compared with its cache entry
        /// when the cache holds one for today's inputs, and with the asset. One line per finding, prefixed by its kind:
        /// « cache: » the cached zone differs from the full build (a cache bug), « asset: » the asset differs (it is older than
        /// today's inputs: run the build), « note: » nothing cached to compare. Writes nothing.
        /// </summary>
        public static List<string> VerifyCache(params string[] addresses)
        {
            var findings = new List<string>();
            var report = new StringBuilder();
            var species = LoadSpecies(report);
            var worlds = LoadZoneWorlds(report);
            var chosen = worlds.Where(w => addresses.Length == 0 || addresses.Contains(w.Address)).ToList();
            if (species.Count == 0 || chosen.Count == 0)
            {
                findings.Add("note: nothing to verify. " + report);
                return findings;
            }
            var stats = new MeasureStats();
            var fresh = MeasureZones(chosen, species, false, false, stats, false);
            var measureVersion = MeasureVersion();
            var asset = AssetDatabase.LoadAssetAtPath<HideSpotSet>(AssetPath);
            var assetSpecies = asset != null ? Enumerable.Range(0, asset.SpeciesCount).Select(i => asset.GetSpecies(i).Value).ToArray() : null;
            bool sameSpecies = assetSpecies != null && assetSpecies.SequenceEqual(species.Select(s => s.Id));
            for (int k = 0; k < chosen.Count; k++)
            {
                var cached = ReadZoneFile(chosen[k].Address, fresh[k].Key, measureVersion, out _);
                if (cached == null)
                    findings.Add($"note: {chosen[k].Address}: the cache holds no zone for today's inputs (run Build Hide Spots).");
                else
                    findings.AddRange(fresh[k].Differences(cached).Select(d => "cache: " + d));
                var stored = asset != null && sameSpecies ? AssetZone(asset, chosen[k].Address) : null;
                if (stored == null)
                    findings.Add($"asset: {chosen[k].Address}: not in the asset with today's species.");
                else
                    findings.AddRange(fresh[k].Differences(stored).Select(d => "asset: " + d));
            }
            return findings;
        }

        /// <summary>
        /// The quick check (HideSpotCacheTests, a few seconds): the zone's cached entry for today's inputs against its focus
        /// measured again from scratch (no cache: the spots, placements and shares must be the same bits; the full check of
        /// every focus is <see cref="VerifyCache"/>), the keys (each changes with what it holds, never otherwise) and the
        /// files (what is written reads back, a damaged file reads as empty). Lines prefixed « cache: », « keys: »,
        /// « files: » are faults; « note: » lines say what could not be compared. Writes nothing in the project's cache.
        /// </summary>
        public static List<string> QuickCheck(string address = StartAddress)
        {
            var findings = new List<string>();
            var report = new StringBuilder();
            foreach (var path in ZoneSources.Concat(MeasureSources))
                if (!File.Exists(Path.GetFullPath(path)))
                    findings.Add($"keys: {path} is listed as code the cache depends on but does not exist.");
            var species = LoadSpecies(report);
            var worlds = LoadZoneWorlds(report);
            var world = worlds.FirstOrDefault(w => w.Address == address);
            if (species.Count == 0 || world == null)
            {
                findings.Add($"note: nothing to check ({address}). " + report);
                return findings;
            }
            var code = CodeVersion();
            var measureVersion = MeasureVersion();
            foreach (var s in species)
                KeySpecies(s);
            foreach (var w in worlds)
                KeyWorld(w);
            var key = ZoneKey(world, code, species);

            // The cached zone against its focus measured from scratch.
            var cached = ReadZoneFile(address, key, measureVersion, out _);
            if (cached == null)
            {
                findings.Add($"note: {address}: the cache holds no zone for today's inputs (run Build Hide Spots); its focus is measured without it.");
            }
            else
            {
                var fresh = BuildZones(new List<ChunkWorld> { world }, species, null, out var cpu, null, w => new[] { cached.Focus });
                var entry = ZoneEntry.From(fresh[0], species, key, cpu[0]);
                findings.AddRange(entry.Differences(cached).Select(d => "cache: " + d));
            }

            // The keys follow what they hold.
            void Expect(bool changed, string what)
            {
                if (!changed)
                    findings.Add("keys: " + what);
            }
            var s0 = species[0];
            var frame = s0.Frames[0];
            var vertex = frame[0];
            frame[0] = vertex + new Vector3(1e-4f, 0f, 0f);
            var measureKey = s0.MeasureKey;
            KeySpecies(s0);
            Expect(!s0.MeasureKey.Equals(measureKey) && !ZoneKey(world, code, species).Equals(key), $"{s0.Name}'s posed mesh moved but its keys did not change");
            frame[0] = vertex;
            KeySpecies(s0);
            Expect(s0.MeasureKey.Equals(measureKey) && ZoneKey(world, code, species).Equals(key), $"{s0.Name}'s keys changed with nothing changed");
            if (world.Decor.Count > 0 && world.Decor[0].Vertices.Count > 0)
            {
                var decor = world.Decor[0];
                var corner = decor.Vertices[0];
                decor.Vertices[0] = corner + Vector3.up * 1e-3f;
                Expect(!ZoneKey(world, code, species).Equals(key), $"{address}: a decor triangle moved but the zone key did not change");
                decor.Vertices[0] = corner;
            }
            if (world.Obstacles.Count > 0)
            {
                var obstacle = world.Obstacles[0];
                world.Obstacles[0] = obstacle.Translated(new Vector3(1e-3f, 0f, 0f));
                Expect(!ZoneKey(world, code, species).Equals(key), $"{address}: an obstacle moved but the zone key did not change");
                world.Obstacles[0] = obstacle;
            }
            Expect(ZoneKey(world, code, species).Equals(key), $"{address}: the zone key changed with nothing changed");
            Expect(!code.Equals(measureVersion), "the zone and measure versions are the same key");

            // The files read back what was written, and a damaged one reads as empty.
            string folder = CacheFolder;
            string temp = Path.Combine(Path.GetTempPath(), "PocketWildsHideSpots_" + Guid.NewGuid().ToString("N"));
            try
            {
                CacheFolder = temp;
                var sample = cached ?? new ZoneEntry
                {
                    Key = key, Focus = new Vector2(0.5f, -1.25f), Zone = new HideSpotSet.Zone { chunkId = address, fieldOfView = 40f },
                    Spots = new[] { new HideSpotSet.Spot { position = Vector3.one, facing = 12.5f, decor = HideDecorKind.Rock } },
                    Placements = new[] { Vector3.right }, Measured = new[] { 0.15f }, Line = "sample",
                };
                var memo = new MeasureMemo();
                var memoKey = new MemoKey(key, s0.MeasureKey, new Vector3(1.5f, 0f, -2.25f), 37.5f, true);
                memo.Add(memoKey, new MemoValue(0.3125f, new Vector4(-0.1f, -0.2f, 0.1f, 0.2f)));
                WriteZoneFile(sample, measureVersion, memo);
                var read = ReadZoneFile(address, sample.Key, measureVersion, out _);
                if (read == null)
                    findings.Add("files: a zone written was not read back.");
                else
                {
                    findings.AddRange(read.Differences(sample).Select(d => "files: " + d));
                    if (read.Line != sample.Line || read.ReachableHits != sample.ReachableHits || read.Entries != sample.Entries || read.Overlaps != sample.Overlaps)
                        findings.Add("files: the zone's report did not read back.");
                }
                var other = ReadZoneFile(address, new Key128(sample.Key.A ^ 1, sample.Key.B), measureVersion, out var shares);
                if (other != null)
                    findings.Add("files: a zone was read back under another key.");
                if (shares == null || shares.Count != 1 || !shares.TryGetValue(memoKey, out var share) || !Same(share.Share, 0.3125f) || !Same(share.Box.w, 0.2f))
                    findings.Add("files: the measured shares were not read back.");
                ReadZoneFile(address, new Key128(1, 2), new Key128(3, 4), out var stale);
                if (stale != null)
                    findings.Add("files: shares were read back for another meter code.");
                string file = ZoneFile(address);
                var bytes = File.ReadAllBytes(file);
                File.WriteAllBytes(file, bytes.Take(bytes.Length / 2).ToArray());
                if (ReadZoneFile(address, sample.Key, measureVersion, out var cut) != null || cut != null)
                    findings.Add("files: a damaged file was read.");
            }
            catch (Exception e)
            {
                findings.Add("files: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                CacheFolder = folder;
                if (Directory.Exists(temp))
                    Directory.Delete(temp, true);
            }
            return findings;
        }

        /// <summary>One zone of the asset as a cache entry (null: no such zone).</summary>
        internal static ZoneEntry AssetZone(HideSpotSet asset, string address)
        {
            int z = asset.FindZone(address);
            if (z < 0)
                return null;
            var zone = asset.GetZone(z);
            var placements = (Vector3[])typeof(HideSpotSet).GetField("_placements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(asset);
            var measured = (float[])typeof(HideSpotSet).GetField("_measured", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(asset);
            int stride = asset.SpeciesCount * HideSpotSet.ShareLevels;
            return new ZoneEntry
            {
                Zone = zone,
                Spots = Enumerable.Range(zone.firstSpot, zone.spotCount).Select(asset.GetSpot).ToArray(),
                Placements = placements.Skip(zone.firstSpot * stride).Take(zone.spotCount * stride).ToArray(),
                Measured = measured.Skip(zone.firstSpot * stride).Take(zone.spotCount * stride).ToArray(),
            };
        }
    }
}
