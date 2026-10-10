using System;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T39 « Cache-cache », the scene side (doc 01 Contenu S-ACT-002; doc « Mini-jeux &amp; activités » S-ACT-002 « chaque
    /// chunk a ses cachettes », sections 1 and 6). The game plays in the player's own refuge: the refuge camera glides to one
    /// open Prairie zone (a different corner each game), the refuge's own animals, bubbles and egg step aside, and pooled
    /// views of the player's animals hide in the zone's measured spots (DEF_HideSpots_Grassland, Addressables group
    /// Activity_HideAndSeek: loaded on the first game, released when the player leaves the activities), standing where the
    /// share of their silhouette the game asks for stays visible, never inside anything. A tap on a decor touches its spot;
    /// a found animal pops out « coucou » in an arc around its decor to free ground, happy, under a heart and a ring; an
    /// empty spot sends three leaves flying; hints and decoys ripple the foliage by position (FoliageGusts). The magnifier
    /// and the whistle are HUD buttons, the night challenge is the HUD's spotlight over a moonlit refuge. Everything is
    /// built while the game loads and pooled: nothing is instantiated during a game, nothing allocates per frame.
    /// </summary>
    public sealed partial class HideSeekStage : IActivityStage, IActivityStagePreparation, IActivityItemActions, IActivityPlayedAnimals, IDisposable
    {
        /// <summary>The Prairie, the only theme of this build (the other biomes' spots come with their chunks).</summary>
        public static readonly ContentId PrairieTheme = ContentId.FromSource("biome.grassland");

        /// <summary>The zone of the last game this session: the next game frames another one when two or more are open.</summary>
        private static string s_lastChunk;

        private readonly ActivityStageHost _host;
        private readonly GameContext _context;
        private readonly IActivityController _controller;
        private readonly IHideSeekGame _game;
        private readonly GameObject _root;
        private readonly ActivityAnimalViews _views;
        private readonly Camera _cameraOverride;

        // Loading (doc Mini-jeux, section 1: open in under a second).
        private AsyncOperationHandle<HideSpotSet> _setHandle;
        private AsyncOperationHandle<Material> _vfxHandle;
        private AsyncOperationHandle<Mesh> _leafHandle;
        private AsyncOperationHandle<Material> _leafMaterialHandle;
        private HideSpotSet _set;
        private bool _setFailed;
        private bool _fallbackSet;
        private bool _loadingShared;
        private Material _vfxMaterial;
        private Mesh _leafMesh;
        private Material _leafMaterial;
        private bool _propsBuilt;
        private bool _disposed;

        // The prepared game: its zone, its chunk, its spots.
        private bool _prepared;
        private int _zone = -1;
        private int _neededSpots;

        /// <summary>Each zone's last layout (its occupied spots, bit s for spot s): the next game there avoids repeating it.</summary>
        private static uint[] s_lastLayouts = new uint[16];
        private int _chunkIndex = -1;
        private WorldChunk _chunk;
        private WorldChunk _chunkOverride;
        private Vector3 _origin;
        private int _firstSpot;
        private int _spotCount;
        private bool _spotsGiven;
        private float _chunkWait;
        private bool _chunkBound;
        private bool _holding;
        private bool _actorsHidden;
        private GroundSurface _ground = GroundSurface.Flat(0f);
        private Obstacle[] _obstacles = Array.Empty<Obstacle>();
        private int _obstacleCount;
        private readonly float[] _spotX = new float[HideSeekLayout.MaxSpots];
        private readonly float[] _spotY = new float[HideSeekLayout.MaxSpots];
        private readonly float[] _spotZ = new float[HideSeekLayout.MaxSpots];
        private readonly int[] _zoneOrder = new int[64];

        // The animals the game may hide (picked while it loads, their views warmed then).
        private readonly AnimalInstance[] _picks = new AnimalInstance[HideSeekLayout.MaxHidden];
        private readonly BodyShape[] _pickBody = new BodyShape[HideSeekLayout.MaxHidden];
        private int _pickCount;
        private bool _picksSameSpecies;

        // The game shown.
        private ActivityStageHost _activeHost;
        private bool _entered;
        private bool _paused;
        private bool _ended;

        /// <summary>Preparing failed (logged): the game still starts, without its visuals (never a presenter stuck loading).</summary>
        private bool _failed;

        /// <summary>Enter is preparing a game the runner has already begun (the presenter skipped Prepare).</summary>
        private bool _lateSpots;

        /// <summary>The shipped stage: the spots and the shared assets come from Addressables.</summary>
        public HideSeekStage(ActivityStageHost host) : this(host, null, null)
        {
        }

        /// <summary>
        /// Tests and captures: given spots (else the Addressables set), a given controller (else the hide-and-seek game) and
        /// a given camera (else the refuge's). With given spots, the shared assets come from <see cref="SetSharedAssets"/>.
        /// </summary>
        public HideSeekStage(ActivityStageHost host, HideSpotSet spots, IActivityController controller, Camera camera = null)
        {
            _host = host;
            _context = host?.Context;
            _controller = controller ?? CreateController();
            _game = _controller as IHideSeekGame ?? throw new ArgumentException("The hide-and-seek stage needs an IHideSeekGame.", nameof(controller));
            _cameraOverride = camera;
            _root = new GameObject("HideSeek_Stage");
            if (host != null && host.Root != null)
                _root.transform.SetParent(host.Root, false);
            _views = new ActivityAnimalViews(_context, _root.transform, "Animals", HideSeekLayout.MaxHidden);
            for (int i = 0; i < _gustSpot.Length; i++)
                _gustSpot[i] = -1;
            if (spots != null)
                _set = spots;
            else
                StartLoading();
        }

        /// <summary>The one place the stage's game is made (agent HS's simulation).</summary>
        public static IActivityController CreateController() => new HideSeekController();

        /// <summary>
        /// Registers the hide-and-seek stage in the presenter's registry (the presenter calls it once): the factory, and the
        /// themes it ships (the Prairie; the hub shows only those).
        /// </summary>
        public static void Register(ActivityStageRegistry registry)
        {
            if (registry == null)
                return;
            registry.Register(ActivityId.HideAndSeek, host => new HideSeekStage(host));
            registry.SetThemes(ActivityId.HideAndSeek, PrairieTheme);
        }

        // ---------------- IActivityStage ----------------

        public ActivityId Activity => ActivityId.HideAndSeek;

        public IActivityController Controller => _controller;

        /// <summary>The hide-and-seek game the stage plays (tests).</summary>
        public IHideSeekGame Game => _game;

        /// <summary>
        /// The spots are loaded, the zone chosen and its spots given to the game, its chunk drawn, the camera arrived, the
        /// played animals' views warmed. Polled by the presenter each frame while it loads: each poll moves the loading on. A
        /// missing spot set or chunk never blocks the game (it then plays on approximate spots, and says so in the log), nor
        /// does a failure (logged once; the presenter's frame goes on).
        /// </summary>
        public bool Ready
        {
            get
            {
                if (_failed)
                    return true;
                try
                {
                    return Advance();
                }
                catch (Exception e)
                {
                    Fail("loading", e);
                    return true;
                }
            }
        }

        /// <summary>The spot set's Addressables handle is held (released with <see cref="Dispose"/>).</summary>
        public bool SpotsLoaded => _setHandle.IsValid();

        public HideSpotSet Spots => _set;

        /// <summary>The spots are measured ones (false: approximate spots built from the chunk, the set being missing).</summary>
        public bool SpotsMeasured => _set != null && !_fallbackSet;

        public bool Shown => _entered;

        /// <summary>The zone played (index in <see cref="Spots"/>), its chunk and the chunk's origin (world).</summary>
        public int Zone => _zone;

        public WorldChunk Chunk => _chunk;

        public Vector3 Origin => _origin;

        public int SpotCount => _spotCount;

        /// <summary>The camera the game is seen through (the refuge's, or the capture's).</summary>
        public Camera Camera => _cameraOverride != null ? _cameraOverride : _host != null && _host.CameraRig != null ? _host.CameraRig.Camera : null;

        /// <summary>The refuge's own animals and egg are hidden by this stage right now.</summary>
        public bool ActorsHidden => _actorsHidden;

        /// <summary>Captures: the shared assets given directly (the VFX material, the kit's leaf and its material).</summary>
        public void SetSharedAssets(Material vfx, Mesh leaf, Material leafMaterial)
        {
            _vfxMaterial = vfx;
            _leafMesh = leaf;
            _leafMaterial = leafMaterial;
            _loadingShared = false;
        }

        /// <summary>Captures: the zone to play and its chunk instance (no streamer in edit mode).</summary>
        public void UseZone(int zone, WorldChunk chunk)
        {
            _zone = Mathf.Clamp(zone, 0, _set != null ? Mathf.Max(0, _set.ZoneCount - 1) : 0);
            _chunkOverride = chunk;
            _chunk = null;
            _chunkBound = false;
            if (_set != null && _set.ZoneCount > 0)
                SetZone(_zone, -1, chunk != null ? chunk.transform.position : Vector3.zero);
        }

        /// <summary>
        /// Captures and tools: the animals to hide, given directly (instead of the player's collection); looks repeat when
        /// fewer than the game hides. Kept for the next games until called again with none.
        /// </summary>
        public void UseAnimals(AnimalInstance[] animals, int count)
        {
            _givenAnimals = animals != null && count > 0;
            _pickCount = 0;
            for (int i = 0; _givenAnimals && i < count && i < _picks.Length; i++)
                _picks[_pickCount++] = animals[i];
            CompletePicks();
        }

        private bool _givenAnimals;

        public void Prepare(in ActivityRequest request)
        {
            _prepared = true;
            _failed = false;
            _spotsGiven = false;
            _chunkWait = 0f;
            _chunkBound = false;
            if (_chunkOverride == null)
            {
                _zone = -1;
                _chunk = null;
            }
            try
            {
                _neededSpots = NeededSpots(in request);
                PickAnimals(in request);
                Advance();
            }
            catch (Exception e)
            {
                // Called from the hub's button: a failure here never leaves the presenter loading forever.
                Fail("preparing", e);
            }
        }

        public void Enter(in ActivitySetup setup, ActivityStageHost host)
        {
            _activeHost = host ?? _host;
            _entered = true;
            _paused = false;
            _ended = false;
            try
            {
                if (!_prepared)
                {
                    // The presenter did not call Prepare: the zone and the views now; the game the runner has just begun keeps
                    // the spots it holds (it may hold none of this zone).
                    GameLog.Warning(LogCategory.Presentation, "HideSeek: the stage was not prepared before the game (call Prepare after Acquire).");
                    var request = new ActivityRequest { Activity = setup.Activity, Mode = setup.Mode, Challenge = setup.Challenge, Theme = setup.Theme };
                    _lateSpots = true;
                    Prepare(in request);
                }
                if (!_failed)
                    Advance();
                if (_failed || _set == null || _zone < 0)
                {
                    GameLog.Warning(LogCategory.Presentation, "HideSeek: no spots, the game plays without its visuals.");
                    return;
                }
                if (!_holding)
                    StartHolding();
                if (!_chunkBound)
                    BindChunk();
                BeginVisuals();
            }
            catch (Exception e)
            {
                // The runner has begun the game: the presenter must go on to its HUD and its end (else the runner stays busy).
                Fail("showing the game", e);
            }
            finally
            {
                _lateSpots = false;
            }
        }

        public void SetPaused(bool paused)
        {
            if (!_entered || _paused == paused)
                return;
            _paused = paused;
            if (paused)
                CancelPress();
            for (int i = 0; i < _hiddenCount; i++)
                if (_view[i] != null)
                    _view[i].SetPlaybackSpeed(paused ? 0f : 1f);
        }

        public void End()
        {
            if (!_entered || _ended)
                return;
            // Called from the runner's end: whatever the last events do, the presenter moves on to its result.
            try
            {
                DrainEvents();
            }
            catch (Exception e)
            {
                Fail("ending the game", e);
            }
            _ended = true;
            try
            {
                CancelPress();
                HideAids(true);
                RevealRest();
            }
            catch (Exception e)
            {
                Fail("ending the game", e);
            }
        }

        /// <summary>
        /// Back to the hub or the refuge: the views and effects away, then the refuge as it was (gusts calm, its day, its
        /// ambient life, its camera, its animals and egg), each step even when one before it failed. Never throws: the
        /// presenter's own exit goes on.
        /// </summary>
        public void Exit()
        {
            if (!_entered && !_holding && !_prepared && !_lifeSet && !_night)
                return;
            bool entered = _entered;
            _entered = false;
            _ended = false;
            _paused = false;
            _prepared = false;
            _spotsGiven = false;
            _chunkBound = false;
            try
            {
                CancelPress();
                if (entered)
                    HideAids(true);
                _game.ClearEvents();
                HideVisuals();
            }
            catch (Exception e)
            {
                Fail("hiding the game", e);
            }
            FoliageGusts.ClearAll();
            try
            {
                ExitNight();
            }
            catch (Exception e)
            {
                Fail("ending the night", e);
            }
            try
            {
                RestoreLifeObstacles();
            }
            catch (Exception e)
            {
                Fail("giving the meadow back to the ambient life", e);
            }
            try
            {
                ReleaseRefuge();
            }
            catch (Exception e)
            {
                Fail("giving the camera back", e);
            }
            _failed = false;
            if (_chunkOverride == null)
            {
                _zone = -1;
                _chunk = null;
            }
        }

        /// <summary>A failure in a lifecycle step: logged (an error, so tests see it); the game goes on without its visuals.</summary>
        private void Fail(string step, Exception e)
        {
            _failed = true;
            GameLog.Error(LogCategory.Presentation, "HideSeek: " + step + " failed: " + e);
        }

        /// <summary>Leaving the activities (the registry's ReleaseAll): the views and effects go, the spots are released.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            Exit();
            _disposed = true;
            _views.Dispose();
            DestroyProps();
            DestroyObject(_root);
            if (_fallbackSet && _set != null)
                DestroyObject(_set);
            _set = null;
            Release(ref _setHandle);
            Release(ref _vfxHandle);
            Release(ref _leafHandle);
            Release(ref _leafMaterialHandle);
        }

        private static void Release<T>(ref AsyncOperationHandle<T> handle)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
            handle = default;
        }

        // ---------------- loading ----------------

        private void StartLoading()
        {
            if (!AddressExists(HideSeekLayout.SpotsAddress, typeof(HideSpotSet)))
            {
                _setFailed = true;
                GameLog.Error(LogCategory.Presentation,
                    $"HideSeek: no Addressables entry {HideSeekLayout.SpotsAddress} (run the hide-spot pipeline for DEF_HideSpots_Grassland): approximate spots.");
            }
            else
            {
                _setHandle = Addressables.LoadAssetAsync<HideSpotSet>(HideSeekLayout.SpotsAddress);
            }
            // Shared assets of the CommonAnimals and Prairie kit groups (already in memory with the refuge): instant.
            _loadingShared = true;
            if (AddressExists(HideSeekLayout.VfxMaterialAddress, typeof(Material)))
                _vfxHandle = Addressables.LoadAssetAsync<Material>(HideSeekLayout.VfxMaterialAddress);
            if (AddressExists(HideSeekLayout.LeafMeshAddress, typeof(Mesh)))
                _leafHandle = Addressables.LoadAssetAsync<Mesh>(HideSeekLayout.LeafMeshAddress);
            if (AddressExists(HideSeekLayout.LeafMaterialAddress, typeof(Material)))
                _leafMaterialHandle = Addressables.LoadAssetAsync<Material>(HideSeekLayout.LeafMaterialAddress);
        }

        private static bool AddressExists(string address, Type type)
        {
            foreach (var locator in Addressables.ResourceLocators)
                if (locator.Locate(address, type, out _))
                    return true;
            return false;
        }

        /// <summary>The shared assets' loads are over (each optional: a missing one only hides its effect).</summary>
        private bool SharedSettled()
        {
            if (!_loadingShared)
                return true;
            if (!Done(_vfxHandle) || !Done(_leafHandle) || !Done(_leafMaterialHandle))
                return false;
            _loadingShared = false;
            _vfxMaterial = Result(_vfxHandle);
            _leafMesh = Result(_leafHandle);
            _leafMaterial = Result(_leafMaterialHandle);
            if (_vfxMaterial == null || _leafMesh == null || _leafMaterial == null)
                GameLog.Warning(LogCategory.Presentation, "HideSeek: a shared asset did not load (rings or leaves hidden).");
            return true;
        }

        private static bool Done<T>(AsyncOperationHandle<T> handle) => !handle.IsValid() || handle.IsDone;

        private static T Result<T>(AsyncOperationHandle<T> handle) where T : Object =>
            handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;

        /// <summary>Moves the loading on: the spots, the shared assets, the zone, its chunk, the camera's glide.</summary>
        private bool Advance()
        {
            if (_disposed)
                return false;
            if (_set == null && !_setFailed)
            {
                if (!_setHandle.IsValid() || !_setHandle.IsDone)
                    return false;
                _set = Result(_setHandle);
                if (_set == null)
                {
                    _setFailed = true;
                    GameLog.Error(LogCategory.Presentation, "HideSeek: the spots did not load (" + HideSeekLayout.SpotsAddress + "): approximate spots.");
                }
            }
            if (!SharedSettled())
                return false;
            if (!_propsBuilt)
                BuildProps();
            if (!_prepared)
                return true;
            if (_zone < 0 && !ChooseZone())
                return true;
            if (!_holding)
                StartHolding();
            if (!_spotsGiven)
                GiveSpots();
            if (!_chunkBound)
            {
                if (!ChunkDrawn())
                {
                    // Frame by frame (the app coming back from the background never spends the whole wait in one frame).
                    _chunkWait += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    if (_chunkWait < HideSeekLayout.ChunkWaitSeconds)
                        return false;
                    // Once (bound below, its volumes bound again when it shows: LateBindChunk).
                    GameLog.Warning(LogCategory.Presentation, "HideSeek: the zone's chunk is still not drawn: the game starts anyway.");
                }
                BindChunk();
            }
            var refuge = _host != null ? _host.Refuge : null;
            return refuge == null || !refuge.CameraGliding;
        }

        // ---------------- the zone (doc 01: « La caméra cadre une zone du biome du thème ») ----------------

        private WorldStreamer Streamer => _host != null && _host.World != null ? _host.World.Streamer : null;

        /// <summary>
        /// The spots this game needs (its hidden animals and decoys at the player's d or the challenge's values, plus a free
        /// spot for challenge 9's moves): the zone is chosen among those that hold them all.
        /// </summary>
        private int NeededSpots(in ActivityRequest request)
        {
            var services = _context != null ? _context.Activities : null;
            if (services == null || !services.Rules.TryGet(ActivityId.HideAndSeek, out var rules))
                return 0;
            int d = services.Difficulty(ActivityId.HideAndSeek);
            int challenge = request.Mode == ActivityMode.Free ? -1 : request.Challenge;
            int needed = (int)rules.Value(HideSeekRules.Hidden, d, challenge) + (int)rules.Value(HideSeekRules.Decoys, d, challenge);
            if (rules.Value(HideSeekRules.MoveEvery, d, challenge) > 0.0)
                needed++;
            return needed;
        }

        /// <summary>
        /// The next open Prairie zone after the last one played (never the same twice in a row when two or more are open),
        /// its chunk in the streamer. Without a measured set: approximate spots on the start chunk.
        /// </summary>
        private bool ChooseZone()
        {
            if (_set == null && !BuildFallbackSet())
                return false;
            if (_chunkOverride != null)
            {
                SetZone(Mathf.Max(0, _zone), -1, _chunkOverride.transform.position);
                return true;
            }
            var streamer = Streamer;
            int open = 0;
            for (int z = 0; z < _set.ZoneCount && open < _zoneOrder.Length; z++)
            {
                // A zone without spots (a chunk the pipeline could not measure) is never framed: nothing to find there.
                if (_set.GetZone(z).spotCount <= 0)
                    continue;
                string id = _set.GetZone(z).chunkId;
                int index = streamer != null ? streamer.IndexOf(id) : -1;
                if (streamer != null && index < 0)
                    continue;
                if (ZoneOpen(id, streamer, index))
                    _zoneOrder[open++] = z;
            }
            if (open == 0)
            {
                // Nothing open known (headless): the first zone.
                _zoneOrder[open++] = 0;
            }
            // The zones that hold the whole game come first; when none does, the open zone with the most spots alone (the game
            // then hides fewer animals: the controller clamps to the spots it has).
            int roomy = 0;
            int largest = _zoneOrder[0];
            for (int i = 0; i < open; i++)
            {
                int z = _zoneOrder[i];
                if (_set.GetZone(z).spotCount > _set.GetZone(largest).spotCount)
                    largest = z;
                if (_set.GetZone(z).spotCount >= _neededSpots)
                    _zoneOrder[roomy++] = z;
            }
            if (roomy == 0)
                _zoneOrder[roomy++] = largest;
            open = roomy;
            int pick = 0;
            int last = -1;
            for (int i = 0; i < open && s_lastChunk != null; i++)
                if (string.Equals(_set.GetZone(_zoneOrder[i]).chunkId, s_lastChunk, StringComparison.Ordinal))
                    last = i;
            if (last >= 0)
                pick = (last + 1) % open;
            else if (_context != null && _context.Activities != null)
                pick = _context.Activities.Record(ActivityId.HideAndSeek).GamesPlayed % open;
            int zone = _zoneOrder[pick];
            var chunkId = _set.GetZone(zone).chunkId;
            s_lastChunk = chunkId;
            int chunkIndex = streamer != null ? streamer.IndexOf(chunkId) : -1;
            var origin = chunkIndex >= 0 ? streamer.GetEntry(chunkIndex).origin : Vector3.zero;
            SetZone(zone, chunkIndex, origin);
            return true;
        }

        /// <summary>A zone is open (doc 02: the zones bought in a row from the arrival); without progression, a resident chunk.</summary>
        private bool ZoneOpen(string chunkId, WorldStreamer streamer, int index)
        {
            var progression = _context != null ? _context.Progression : null;
            if (progression != null)
            {
                var zones = progression.Config.Zones;
                for (int i = 0; i < zones.Length; i++)
                    if (string.Equals(zones[i].ChunkAddress, chunkId, StringComparison.Ordinal))
                        return progression.Zones.IsOpen(zones[i]);
            }
            return streamer == null || streamer.IsResident(index);
        }

        private void SetZone(int zone, int chunkIndex, Vector3 origin)
        {
            _zone = zone;
            _chunkIndex = chunkIndex;
            _origin = origin;
            var z = _set.GetZone(zone);
            _firstSpot = Mathf.Clamp(z.firstSpot, 0, _set.SpotCount);
            _spotCount = Mathf.Clamp(z.spotCount, 0, Mathf.Min(HideSeekLayout.MaxSpots, _set.SpotCount - _firstSpot));
        }

        /// <summary>The zone's chunk is loaded and drawn (the camera's glide makes the streamer activate it).</summary>
        private bool ChunkDrawn()
        {
            if (_chunkOverride != null)
                return true;
            var streamer = Streamer;
            if (streamer == null || _chunkIndex < 0)
                return true;
            return streamer.GetState(_chunkIndex) == ChunkStreamState.Active;
        }

        /// <summary>The chunk's ground and its measured volumes (world space), copied once per game.</summary>
        private void BindChunk()
        {
            _chunkBound = true;
            WorldChunk chunk = _chunkOverride;
            var streamer = Streamer;
            if (chunk == null && streamer != null && _chunkIndex >= 0 && streamer.IsLoaded(_chunkIndex))
                chunk = streamer.GetChunk(_chunkIndex);
            if (chunk == null && _host != null && _host.World != null && _chunkIndex < 0)
                chunk = _host.World.StartChunk;
            _chunk = chunk;
            _obstacleCount = 0;
            if (chunk == null)
            {
                _ground = GroundSurface.Flat(_origin.y);
                return;
            }
            _origin = chunk.transform.position;
            _ground = chunk.Ground;
            int n = chunk.ObstacleCount;
            if (_obstacles.Length < n)
                _obstacles = new Obstacle[n];
            for (int i = 0; i < n; i++)
                _obstacles[i] = chunk.GetObstacle(i).Translated(_origin);
            _obstacleCount = n;
        }

        /// <summary>
        /// The zone's chunk was still not drawn when the game began (the loading's timeout): once it shows, its ground and
        /// volumes are bound and the animals still hidden stand on it again, free of its decor (collision rule). A state read
        /// per frame until then.
        /// </summary>
        private void LateBindChunk()
        {
            if (_chunk != null || _chunkOverride != null || _chunkIndex < 0 || !ChunkDrawn())
                return;
            BindChunk();
            if (_chunk == null)
                return;
            for (int i = 0; i < _hiddenCount; i++)
            {
                if (_phase[i] != Hidden || _view[i] == null)
                    continue;
                HidingPose(i, _spot[i], out var position, out float yaw);
                SetPose(i, position, yaw);
            }
        }

        /// <summary>The zone's spots to the game before it begins (chunk space: its distances, the magnifier, challenge 9).</summary>
        private void GiveSpots()
        {
            for (int k = 0; k < _spotCount; k++)
            {
                var p = _set.GetSpot(_firstSpot + k).position;
                _spotX[k] = p.x;
                _spotY[k] = p.y;
                _spotZ[k] = p.z;
            }
            // A game quit mid-way is still running for the controller: it ends here, before the next zone's spots (never the
            // game the runner has just begun, when Enter prepares late).
            if (!_lateSpots)
                (_controller as HideSeekController)?.EndGame();
            _game.SetSpots(_spotCount, _spotX, _spotY, _spotZ);
            // A replay in this zone hides its animals elsewhere when it can (the controller's soft rotation).
            if (_controller is HideSeekController spots && _zone >= 0 && _zone < s_lastLayouts.Length && s_lastLayouts[_zone] != 0)
                spots.LastLayout = s_lastLayouts[_zone];
            _spotsGiven = true;
        }

        // ---------------- the refuge for a game: camera, actors ----------------

        /// <summary>The camera glides to the zone (its measured pose, a field of view fitted to the device), the refuge's actors step aside.</summary>
        private void StartHolding()
        {
            if (_set == null || _zone < 0)
                return;
            _holding = true;
            var zone = _set.GetZone(_zone);
            var position = _origin + zone.cameraPosition;
            var rotation = zone.cameraRotation.normalized;
            var camera = Camera;
            float aspect = camera != null && camera.aspect > 0.05f ? camera.aspect : HideSeekLayout.NarrowestAspect;
            float fov = FitFieldOfView(position, rotation, zone.fieldOfView, aspect);
            var refuge = _host != null ? _host.Refuge : null;
            if (refuge != null)
            {
                // Marked first: whatever happens next, Exit gives the camera and the actors back.
                _actorsHidden = true;
                refuge.HoldCamera(position, rotation, fov, HideSeekLayout.CameraGlideSeconds);
                refuge.SetActorsVisible(false);
            }
            else if (camera != null)
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fov;
            }
        }

        /// <summary>The camera glides back to the player's view (the rig resumes there), the refuge's actors show again; both always.</summary>
        private void ReleaseRefuge()
        {
            if (!_holding)
                return;
            _holding = false;
            bool actors = _actorsHidden;
            _actorsHidden = false;
            var refuge = _host != null ? _host.Refuge : null;
            if (refuge == null)
                return;
            try
            {
                refuge.ReleaseCamera(HideSeekLayout.CameraGlideSeconds);
            }
            finally
            {
                if (actors)
                    refuge.SetActorsVisible(true);
            }
        }

        /// <summary>
        /// The vertical field of view that holds every spot (its decor, its landing, the gather point) between the HUD's top
        /// and bottom on this device: the zone's measured value at 9:21 adapted to any aspect. Only the camera's position sets
        /// what hides what, so the visible shares stay those measured whatever the field of view.
        /// </summary>
        private float FitFieldOfView(Vector3 position, Quaternion rotation, float measured, float aspect) =>
            FitFieldOfView(_set, _firstSpot, _spotCount, _origin, position, rotation, measured, aspect);

        /// <summary>The widest screen the frame is fitted to (phones); wider screens keep its horizontal extent.</summary>
        public const float WidestFittedAspect = 9f / 16f;

        /// <summary>
        /// The stage's field of view for a zone on a device of this aspect. Phones (9:21 to 9:16) fit the spots' decor and
        /// landings between the HUD's bands (never under 82 % of the measured value; wider than measured below 9:21). Wider
        /// screens (tablets) keep the 9:16 horizontal extent and zoom in (top and bottom cropped) instead of widening, so the
        /// frame never shows more of the island's sides than a phone does. HideSpotPipeline.StageFov holds the same rule for
        /// the zones' play-area check (HideSpotTests compares them).
        /// </summary>
        public static float FitFieldOfView(HideSpotSet set, int firstSpot, int spotCount, Vector3 origin, Vector3 position, Quaternion rotation,
            float measured, float aspect)
        {
            if (aspect > WidestFittedAspect)
            {
                float phone = FitFieldOfView(set, firstSpot, spotCount, origin, position, rotation, measured, WidestFittedAspect);
                float tan = Mathf.Tan(phone * 0.5f * Mathf.Deg2Rad) * WidestFittedAspect / aspect;
                return 2f * Mathf.Atan(tan) * Mathf.Rad2Deg;
            }
            float fov = measured > 1f ? measured : 34f;
            if (aspect < HideSeekLayout.NarrowestAspect)
            {
                float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * HideSeekLayout.NarrowestAspect / aspect;
                fov = 2f * Mathf.Atan(tan) * Mathf.Rad2Deg;
            }
            if (set == null || spotCount == 0)
                return fov;
            var inverse = Quaternion.Inverse(rotation);
            float up = 1f - 2f * HideSeekLayout.HudTopShare;
            float down = 1f - 2f * HideSeekLayout.HudBottomShare;
            float side = (1f - 2f * HideSeekLayout.SideShare) * aspect;
            float need = 0f;
            for (int k = 0; k < spotCount; k++)
            {
                var spot = set.GetSpot(firstSpot + k);
                var center = origin + spot.decorCenter;
                need = Mathf.Max(need, Need(inverse, position, center, up, down, side));
                need = Mathf.Max(need, Need(inverse, position, center + Vector3.up * Mathf.Max(0.2f, spot.decorHeight), up, down, side));
                need = Mathf.Max(need, Need(inverse, position, origin + spot.landing + Vector3.up * 0.35f, up, down, side));
            }
            if (need <= 0f)
                return fov;
            float fitted = 2f * Mathf.Atan(need) * Mathf.Rad2Deg;
            // Tighter on wider phones (bigger animals), wider when a spot would fall under the HUD; always reasonable.
            return Mathf.Clamp(Mathf.Max(fitted, fov * 0.82f), 18f, 70f);
        }

        private static float Need(Quaternion inverse, Vector3 eye, Vector3 point, float up, float down, float side)
        {
            var local = inverse * (point - eye);
            if (local.z < 0.1f)
                return 0f;
            float x = Mathf.Abs(local.x / local.z) / side;
            float y = local.y / local.z;
            y = y >= 0f ? y / up : -y / down;
            return Mathf.Max(x, y);
        }

        // ---------------- the animals the game hides ----------------

        /// <summary>
        /// Up to 8 of the player's animals, all different when possible: visible favorites first, then the other animals in
        /// the refuge, then the rest of the collection, one per species before any second of a kind; challenge 8 « Tous
        /// pareils »: the species the player has most of, copies of its look when there are fewer than needed. Their views
        /// are warmed now (never instantiated during the game).
        /// </summary>
        private void PickAnimals(in ActivityRequest request)
        {
            _picksSameSpecies = request.Challenge == HideSeekRules.SameSpeciesChallenge;
            if (_givenAnimals)
                return;
            _pickCount = 0;
            var collection = _context != null && _context.Save != null ? _context.State.Collection : null;
            if (collection == null || collection.Count == 0)
                return;
            if (_picksSameSpecies)
            {
                PickSameSpecies(collection);
            }
            else
            {
                // The animals the presenter named for the game (its rewards' bond goes to them) hide first.
                for (int i = 0; i < 3 && _pickCount < _picks.Length; i++)
                {
                    uint named = i == 0 ? request.Animal0 : i == 1 ? request.Animal1 : request.Animal2;
                    if (named != 0 && !Picked(named) && collection.TryGet(named, out var animal) && HasVisual(animal.Species))
                        _picks[_pickCount++] = animal;
                }
                PickVaried(collection);
            }
            CompletePicks();
        }

        /// <summary>
        /// The first three different animals this game hides (instance ids, 0 for none; every game hides at least four), known
        /// once prepared: the presenter may name them in its request before the runner begins, so the bond and species rewards
        /// go to animals that really played (challenge 8 hides one species only). Returns how many.
        /// </summary>
        public int PlayedAnimals(out uint first, out uint second, out uint third)
        {
            first = second = third = 0;
            int n = 0;
            for (int i = 0; i < _pickCount && n < 3; i++)
            {
                uint id = _picks[i].InstanceId;
                if (id == 0 || id == first || id == second)
                    continue;
                if (n == 0)
                    first = id;
                else if (n == 1)
                    second = id;
                else
                    third = id;
                n++;
            }
            return n;
        }

        /// <summary>Fewer animals than the game hides (a profile below 8, six of a kind): their looks repeat; bodies read, views warmed.</summary>
        private void CompletePicks()
        {
            int owned = _pickCount;
            for (int i = owned; i < _picks.Length && owned > 0; i++)
                _picks[_pickCount++] = _picks[i % owned];
            for (int i = 0; i < _pickCount; i++)
                _pickBody[i] = BodyOf(_picks[i].Species);
            WarmViews();
        }

        private readonly uint[] _visibleIds = new uint[64];

        private void PickVaried(CollectionState collection)
        {
            var refuge = _host != null ? _host.Refuge : null;
            int visible = refuge != null ? refuge.CopyVisibleAnimals(_visibleIds) : 0;
            int count = collection.Count;
            int start = _context.Activities != null ? (int)((uint)_context.Activities.Activities.GamesStarted % (uint)count) : 0;
            // Passes: 0 visible favorites, 1 visible, 2 favorites, 3 any; species not yet picked first, then any species.
            for (int repeat = 0; repeat < 2 && _pickCount < _picks.Length; repeat++)
            {
                for (int pass = 0; pass < 4 && _pickCount < _picks.Length; pass++)
                {
                    for (int n = 0; n < count && _pickCount < _picks.Length; n++)
                    {
                        ref readonly var animal = ref collection[(start + n) % count];
                        bool isVisible = Contains(_visibleIds, visible, animal.InstanceId);
                        bool wanted = pass == 0 ? isVisible && animal.IsFavorite : pass == 1 ? isVisible : pass != 2 || animal.IsFavorite;
                        if (!wanted || Picked(animal.InstanceId) || (repeat == 0 && PickedSpecies(animal.Species)) || !HasVisual(animal.Species)
                            || IsAway(animal.InstanceId))
                            continue;
                        _picks[_pickCount++] = animal;
                    }
                }
            }
        }

        private void PickSameSpecies(CollectionState collection)
        {
            int count = collection.Count;
            ContentId best = ContentId.None;
            int bestCount = 0;
            for (int i = 0; i < count; i++)
            {
                var species = collection[i].Species;
                if (species == best || !HasVisual(species) || IsAway(collection[i].InstanceId))
                    continue;
                int n = 0;
                for (int j = 0; j < count; j++)
                    if (collection[j].Species == species && !IsAway(collection[j].InstanceId))
                        n++;
                if (n > bestCount)
                {
                    best = species;
                    bestCount = n;
                }
            }
            for (int i = 0; i < count && _pickCount < _picks.Length; i++)
                if (collection[i].Species == best && !IsAway(collection[i].InstanceId))
                    _picks[_pickCount++] = collection[i];
        }

        /// <summary>T42: an animal away on an expedition is not in the refuge, so it never hides.</summary>
        private bool IsAway(uint instanceId) => _context != null && _context.Expeditions != null && _context.Expeditions.IsAway(instanceId);

        private static bool Contains(uint[] ids, int count, uint id)
        {
            for (int i = 0; i < count; i++)
                if (ids[i] == id)
                    return true;
            return false;
        }

        private bool Picked(uint instance)
        {
            for (int i = 0; i < _pickCount; i++)
                if (_picks[i].InstanceId == instance)
                    return true;
            return false;
        }

        private bool PickedSpecies(ContentId species)
        {
            for (int i = 0; i < _pickCount; i++)
                if (_picks[i].Species == species)
                    return true;
            return false;
        }

        private bool HasVisual(ContentId species) => _views.TryGetEntry(species, out _);

        private BodyShape BodyOf(ContentId species) =>
            _views.TryGetEntry(species, out var entry) && entry.behavior != null ? entry.behavior.body : new BodyShape(0.12f, -0.12f, 0.08f, 0.25f);

        private void WarmViews()
        {
            for (int i = 0; i < _pickCount; i++)
            {
                var species = _picks[i].Species;
                bool first = true;
                for (int j = 0; j < i && first; j++)
                    first = _picks[j].Species != species;
                if (!first)
                    continue;
                int n = 0;
                for (int j = 0; j < _pickCount; j++)
                    if (_picks[j].Species == species)
                        n++;
                _views.Warm(species, n);
            }
        }

        /// <summary>
        /// The temperament the refuge gives an animal (AnimalScheduler: its seed mixed with the temperament salt, five
        /// kinds). « Shy » is the doc's Timid: it waits 2 s more before its hint.
        /// </summary>
        public static Temperament TemperamentOf(uint seed) => AnimalScheduler.TemperamentOf(seed);

        // ---------------- approximate spots (no measured set) ----------------

        /// <summary>
        /// The measured set is missing: approximate spots on the start chunk from its own volumes (a bush, a rock, a stump:
        /// low and not too big), each animal standing just beyond it away from the camera, its landing in front. The game
        /// stays playable; the visible share is not measured (the spot's point for every species).
        /// </summary>
        private bool BuildFallbackSet()
        {
            var world = _host != null ? _host.World : null;
            var chunk = _chunkOverride != null ? _chunkOverride : world != null ? world.StartChunk : null;
            var camera = Camera;
            if (chunk == null || camera == null)
                return false;
            var origin = chunk.transform.position;
            var forward = camera.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            var spots = new HideSpotSet.Spot[Mathf.Min(14, HideSeekLayout.MaxSpots)];
            int count = 0;
            for (int i = 0; i < chunk.ObstacleCount && count < spots.Length; i++)
            {
                var o = chunk.GetObstacle(i);
                if (o.kind != ObstacleKind.Solid || o.bottom > 0.1f || o.radius < 0.15f || o.radius > 0.8f || o.top > 1.6f)
                    continue;
                var center = new Vector3(o.Center.x, o.baseY, o.Center.y);
                bool near = false;
                for (int k = 0; k < count && !near; k++)
                    near = (spots[k].decorCenter - center).sqrMagnitude < 1.2f * 1.2f;
                if (near)
                    continue;
                float reach = o.Reach;
                spots[count++] = new HideSpotSet.Spot
                {
                    position = center + forward * (reach + 0.22f),
                    facing = Mathf.Atan2(-forward.x, -forward.z) * Mathf.Rad2Deg,
                    landing = center - forward * (reach + 0.3f),
                    decorCenter = center,
                    decorRadius = reach,
                    decorHeight = Mathf.Max(0.3f, o.top),
                    decor = HideDecorKind.Other,
                };
            }
            if (count == 0)
                return false;
            Array.Resize(ref spots, count);
            var zone = new HideSpotSet.Zone
            {
                chunkId = string.Empty,
                cameraPosition = camera.transform.position - origin,
                cameraRotation = camera.transform.rotation,
                fieldOfView = camera.fieldOfView,
                gatherPoint = spots[0].landing,
                firstSpot = 0,
                spotCount = count,
            };
            _set = ScriptableObject.CreateInstance<HideSpotSet>();
            _set.name = "HideSpots_Approximate";
            _set.SetAuthoring(new[] { zone }, spots, null, null, null);
            _fallbackSet = true;
            _chunkOverride = _chunkOverride != null ? _chunkOverride : chunk;
            GameLog.Warning(LogCategory.Presentation, $"HideSeek: {count} approximate spots on {chunk.name} (no measured DEF_HideSpots_Grassland).");
            return true;
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
