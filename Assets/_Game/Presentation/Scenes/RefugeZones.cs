using System;
using System.Collections.Generic;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T35A: the open zones of the refuge's biome in the world (doc 02 Améliorations, section 4.6). It keeps every
    /// open zone loaded, lets the animals walk across all of them, closes the next zone with a hedge and a sign,
    /// bounds the camera to the open zones (with a glimpse past the hedge), and plays the opening: the hedge sinks,
    /// a few animals walk in, more animals may be seen. A touch on the hedge or the sign raises <see cref="SignTapped"/>
    /// (the progression presenter shows the zone's window). A plain class ticked by the scene entry; it allocates only
    /// when the zones change (never per frame).
    /// </summary>
    public sealed class RefugeZones : IDisposable
    {
        /// <summary>The walk area stops this far from the island's west edge and from either side (m).</summary>
        public const float WestReach = 5.4f;
        public const float HalfDepth = 5.4f;

        /// <summary>The walk area ends this far before the hedge's seam (m).</summary>
        public const float SeamMargin = 0.6f;

        /// <summary>The camera may look this far past the seam: the hedge, the sign and a glimpse of the closed zone.</summary>
        public const float CameraPastSeam = 2.2f;

        private const float GlideSeconds = 1.3f;
        private const int AnimalsWalkingIn = 3;

        /// <summary>
        /// After an opening, the walk-in is watched this long: a walker stopped on its way (another animal in the path)
        /// is replaced, so two or three animals really enter the new zone (doc 02, section 4.6).
        /// </summary>
        private const float WalkInSeconds = 20f;
        private const float WalkInCheck = 1.5f;

        /// <summary>At most this many walkers per opening (the first ones and their replacements).</summary>
        private const int WalkInMaxSent = 6;

        private readonly GameContext _context;
        private readonly ProgressionServices _progression;
        private readonly WorldStreamer _streamer;
        private readonly RefugeController _refuge;
        private readonly ScatterRenderer _scatter;
        private readonly AmbientLife _life;
        private readonly RefugeCameraRig _rig;
        private readonly ZoneBoundary _boundary;
        private readonly ContentId _biome;
        private readonly ZoneDefinition[] _zones;
        private readonly int[] _chunkIndex;
        private readonly float _half;
        private readonly Action<ZoneOpening> _onOpened;

        private int _walkZones = -1;
        private int _boundaryZone = -1;
        private int _keepOutZone = -1;
        private int _captureZone = -1;
        private AsyncOperationHandle<Sprite> _capture;
        private float _glideEnd = -1f;
        private int _pendingAnimals;
        private int _walkInZone = -1;
        private float _walkInEnd = -1f;
        private float _walkInNext;
        private int _walkInSent;
        private float _time;

        public RefugeZones(GameContext context, WorldStreamer streamer, WorldChunkSet set, ZoneKit kit, RefugeController refuge,
            ScatterRenderer scatter, AmbientLife life, RefugeCameraRig rig, Transform root)
        {
            _context = context;
            _progression = context.Progression ?? throw new ArgumentException("No progression services.", nameof(context));
            _streamer = streamer;
            _refuge = refuge;
            _scatter = scatter;
            _life = life;
            _rig = rig;
            _half = set != null ? set.HalfSize : 7f;
            _biome = _progression.FirstBiome;
            var zones = new List<ZoneDefinition>();
            foreach (var zone in _progression.Config.Zones)
                if (zone.Biome == _biome)
                    zones.Add(zone);
            zones.Sort((a, b) => a.Index.CompareTo(b.Index));
            _zones = zones.ToArray();
            _chunkIndex = new int[_zones.Length];
            for (int i = 0; i < _zones.Length; i++)
                _chunkIndex[i] = streamer != null ? streamer.IndexOf(_zones[i].ChunkAddress) : -1;
            _boundary = kit != null ? new ZoneBoundary(kit, context.Palettes, root) : null;
            _onOpened = OnZoneOpened;
            _progression.Zones.Opened += _onOpened;
            Refresh();
        }

        public ZoneBoundary Boundary => _boundary;

        /// <summary>A zone is opening: the hedge sinks, the camera glides, animals walk in (no card comes meanwhile).</summary>
        public bool Opening => (_boundary != null && _boundary.IsMoving) || _glideEnd >= 0f || _pendingAnimals > 0;

        /// <summary>After an opening, the walk-in is still watched (replacements may set off).</summary>
        public bool WalkingIn => _walkInEnd >= 0f;

        /// <summary>Open zones in a row from the arrival zone (the walkable part of the biome).</summary>
        public int OpenZones
        {
            get
            {
                int n = 0;
                while (n < _zones.Length && _progression.Zones.IsOpen(_zones[n]))
                    n++;
                return n;
            }
        }

        /// <summary>Zones the animals walk on now (open and loaded).</summary>
        public int WalkZones => Math.Max(0, _walkZones);

        /// <summary>x of the east edge of the last open zone (the hedge's seam).</summary>
        public float SeamX => ZoneOrigin(Math.Max(1, OpenZones) - 1).x + _half;

        /// <summary>Camera limits along x: from the arrival zone to a glimpse past the next zone's hedge.</summary>
        public void GetCameraRange(out float minX, out float maxX)
        {
            minX = ZoneOrigin(0).x - WestReach + 2f;
            maxX = OpenZones < _zones.Length ? SeamX + CameraPastSeam : ZoneOrigin(_zones.Length - 1).x + _half - 2f;
        }

        /// <summary>A chunk changed state: an open zone that finished loading joins the walk area.</summary>
        public void OnChunkState(int index, ChunkStreamState state)
        {
            if (state != ChunkStreamState.Staged && state != ChunkStreamState.Active)
                return;
            for (int i = 0; i < _chunkIndex.Length; i++)
                if (_chunkIndex[i] == index)
                {
                    Refresh();
                    return;
                }
        }

        public void Tick(float time)
        {
            _time = time;
            if (_boundary != null && _boundary.Tick(time))
            {
                // The hedge is in the ground: the zone is part of the refuge now (its volumes go with it); the next
                // hedge rises at the next seam.
                _boundary.Hide();
                Refresh(true);
                if (_boundary.IsPlaced)
                    _boundary.Rise(time, _context.State.Settings.ReduceMotion);
                SendAnimalsIn();
            }
            if (_glideEnd >= 0f && time >= _glideEnd)
            {
                _glideEnd = -1f;
                _rig?.ReturnToWorld();
            }
            if (_walkInEnd >= 0f && time >= _walkInNext)
                WatchWalkIn(time);
        }

        /// <summary>A world tap: on the hedge or the sign it asks for the zone's window.</summary>
        public bool TryTap(Ray ray)
        {
            if (_boundary == null || !_boundary.Hits(ray) || _boundary.IsMoving)
                return false;
            SignTapped?.Invoke();
            return true;
        }

        /// <summary>The hedge or the sign of the next zone was touched.</summary>
        public event Action SignTapped;

        /// <summary>Where the next zone's sign stands (false while no hedge is placed).</summary>
        public bool TryGetSignPosition(out Vector3 position)
        {
            if (_boundary != null && _boundary.IsPlaced && _boundary.HasSign)
            {
                position = _boundary.SignPosition;
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>The capture of a zone for its window, when loaded (null: the window shows the sign icon meanwhile).</summary>
        public Sprite CaptureFor(in ZoneDefinition zone) =>
            zone.Index == _captureZone && _capture.IsValid() && _capture.Status == AsyncOperationStatus.Succeeded ? _capture.Result : null;

        /// <summary>Address of a zone's capture (WorldCapture.CaptureZones writes them, one Addressable each).</summary>
        public static string CaptureAddress(in ZoneDefinition zone) => "zones.capture." + zone.ChunkAddress.Substring("chunk.".Length);

        /// <summary>Only the next zone's capture is in memory (one small texture), loaded when its hedge stands.</summary>
        private void LoadCapture(int zone)
        {
            if (zone == _captureZone)
                return;
            if (_capture.IsValid())
                Addressables.Release(_capture);
            _capture = default;
            _captureZone = zone;
            if (zone >= 0 && zone < _zones.Length && _zones[zone].Role == ZoneRole.Expansion)
                _capture = Addressables.LoadAssetAsync<Sprite>(CaptureAddress(_zones[zone]));
        }

        /// <summary>
        /// Keeps the open zones (and the next one, its props meet the hedge) loaded, rebuilds the walk area across the
        /// open zones already loaded, places the hedge at the seam once both sides are loaded, and updates the limits.
        /// </summary>
        public void Refresh() => Refresh(false);

        private void Refresh(bool force)
        {
            int open = OpenZones;
            for (int i = 0; i < _zones.Length; i++)
                _streamer?.SetResident(_chunkIndex[i], i <= open);

            int loaded = 0;
            while (loaded < open && Loaded(loaded))
                loaded++;
            bool boundaryReady = open < _zones.Length && loaded == open && Loaded(open);
            int boundaryZone = boundaryReady ? open : -1;
            if (!force && loaded == _walkZones && boundaryZone == _boundaryZone)
                return;

            var avoid = new List<Obstacle>();
            var interests = new List<InterestPoint>();
            var grounds = new GroundSurface[Math.Max(1, loaded)];
            for (int i = 0; i < loaded; i++)
            {
                var chunk = _streamer.GetChunk(_chunkIndex[i]);
                avoid.AddRange(chunk.GetWorldObstacles());
                interests.AddRange(chunk.GetWorldInterests());
                grounds[i] = chunk.Ground;
            }
            var ground = loaded > 0 ? GroundSurface.Strip(grounds, ZoneOrigin(0).x - _half, 2f * _half) : GroundSurface.Flat(0f);

            if (_boundary != null)
            {
                if (boundaryReady && !_boundary.IsMoving)
                {
                    // Both sides of the seam: the hedge never enters a prop of either zone.
                    var next = _streamer.GetChunk(_chunkIndex[open]);
                    var both = new List<Obstacle>(avoid);
                    both.AddRange(next.GetWorldObstacles());
                    var origin = ZoneOrigin(0);
                    _boundary.Place(SeamX, origin.z - HalfDepth, origin.z + HalfDepth, ground, both);
                }
                else if (!boundaryReady && !_boundary.IsMoving)
                {
                    _boundary.Hide();
                }
                var volumes = _boundary.Obstacles;
                avoid.AddRange(volumes);
                // Nothing grows in the hedge, no bird lands in it; where a hedge stood, the plants grow back.
                _scatter?.SetKeepOut(volumes);
                int seamZone = _boundary.IsPlaced ? open : -1;
                if (seamZone != _keepOutZone)
                {
                    RebuildScatter(_keepOutZone - 1);
                    RebuildScatter(_keepOutZone);
                    RebuildScatter(seamZone - 1);
                    RebuildScatter(seamZone);
                    _keepOutZone = seamZone;
                }
                _life?.SetExtraObstacles(volumes);
            }

            if (_refuge != null && loaded > 0)
            {
                var first = ZoneOrigin(0);
                float east = ZoneOrigin(loaded - 1).x + _half - (loaded < _zones.Length ? SeamMargin : 1.4f);
                var area = WanderArea.Rectangle(first.x - WestReach, first.z - HalfDepth, east, first.z + HalfDepth, first.y);
                _refuge.SetWalkData(new RefugeWalkData(area, ground, avoid.ToArray(), interests.ToArray()));
                int allowance = RefugeController.ArrivalVisibleAnimals + _progression.Zones.VisibleAnimalBonus(_biome);
                _refuge.SetVisibleAllowance(allowance, ZoneArea(loaded - 1));
            }
            _walkZones = loaded;
            _boundaryZone = boundaryZone;
            LoadCapture(boundaryZone);
            CameraRangeChanged?.Invoke();
        }

        /// <summary>The camera limits moved (zone opened, zone loaded).</summary>
        public event Action CameraRangeChanged;

        /// <summary>The walkable part of one zone (for spawns and the animals walking in).</summary>
        public WanderArea ZoneArea(int zone)
        {
            var origin = ZoneOrigin(Math.Max(0, zone));
            float west = zone <= 0 ? origin.x - WestReach : origin.x - _half + SeamMargin;
            return WanderArea.Rectangle(west, origin.z - HalfDepth, origin.x + _half - SeamMargin, origin.z + HalfDepth, origin.y);
        }

        public void Dispose()
        {
            _progression.Zones.Opened -= _onOpened;
            _boundary?.Dispose();
            if (_capture.IsValid())
                Addressables.Release(_capture);
            _capture = default;
        }

        // ---------------- The opening ----------------

        private void OnZoneOpened(ZoneOpening opening)
        {
            if (opening.Zone.Biome != _biome)
                return;
            bool reduce = _context.State.Settings.ReduceMotion;
            var at = _boundary != null && _boundary.HasSign ? _boundary.SignPosition : new Vector3(SeamX, 0f, ZoneOrigin(0).z);
            _context.Audio.Play(AudioEventId.Claim, at, default);
            _context.Haptics.Play(HapticEventId.Rare);
            _refuge?.Vfx?.Play(VfxId.MasteryLevelUp, at + new Vector3(0f, 0.4f, 0f), _time);
            _pendingAnimals = AnimalsWalkingIn;
            if (_boundary != null && _boundary.IsPlaced)
            {
                _boundary.Sink(_time, reduce);
            }
            else
            {
                Refresh();
                SendAnimalsIn();
            }
            // The camera glides to show the new zone, then comes back (none with Reduce Motion).
            if (_rig != null && !reduce)
            {
                var center = ZoneOrigin(opening.Zone.Index);
                _rig.FocusOn(new Vector3(center.x - _half * 0.45f, _rig.Focus.y, _rig.Focus.z), RefugeCameraState.FocusAnimal);
                _glideEnd = _time + GlideSeconds;
            }
        }

        private void SendAnimalsIn()
        {
            if (_pendingAnimals <= 0 || _refuge == null || _walkZones < 2)
            {
                GameLog.Info(LogCategory.Presentation, $"[Zones] no walk-in: pending {_pendingAnimals}, walk zones {_walkZones}");
                return;
            }
            int sent = _refuge.SendAnimalsInto(ZoneArea(_walkZones - 1), _pendingAnimals);
            GameLog.Info(LogCategory.Presentation, $"[Zones] {sent} of {_pendingAnimals} animals walk into zone {_walkZones - 1}");
            _walkInSent = sent;
            _pendingAnimals = 0;
            _walkInZone = _walkZones - 1;
            _walkInEnd = _time + WalkInSeconds;
            _walkInNext = _time + WalkInCheck;
        }

        /// <summary>
        /// Every second and a half after an opening, until three animals stand in the new zone: a walker stopped on its
        /// way (it met another animal, then sat or slept) no longer counts, and another one sets off.
        /// </summary>
        private void WatchWalkIn(float time)
        {
            _walkInNext = time + WalkInCheck;
            if (_refuge == null)
            {
                _walkInEnd = -1f;
                return;
            }
            var zone = ZoneArea(_walkInZone);
            if (_refuge.CountAnimalsIn(zone, false) >= AnimalsWalkingIn || time >= _walkInEnd || _walkInSent >= WalkInMaxSent)
            {
                _walkInEnd = -1f;
                return;
            }
            int there = _refuge.CountAnimalsIn(zone, true);
            if (there >= AnimalsWalkingIn)
                return;
            int sent = _refuge.SendAnimalsInto(zone, Math.Min(AnimalsWalkingIn - there, WalkInMaxSent - _walkInSent));
            _walkInSent += sent;
            if (sent > 0)
                GameLog.Info(LogCategory.Presentation, $"[Zones] {sent} more animals walk into zone {_walkInZone}");
        }

        // ---------------- Helpers ----------------

        private bool Loaded(int zone) => _streamer != null && zone < _chunkIndex.Length && _streamer.IsLoaded(_chunkIndex[zone]);

        private Vector3 ZoneOrigin(int zone)
        {
            if (_streamer == null || zone < 0 || zone >= _chunkIndex.Length || _chunkIndex[zone] < 0)
                return new Vector3(zone * 2f * _half, 0f, 0f);
            return _streamer.GetEntry(_chunkIndex[zone]).origin;
        }

        private void RebuildScatter(int zone)
        {
            if (_scatter == null || zone < 0 || zone >= _chunkIndex.Length || _streamer.GetState(_chunkIndex[zone]) != ChunkStreamState.Active)
                return;
            _scatter.Rebuild(_streamer.GetChunk(_chunkIndex[zone]));
        }
    }
}
