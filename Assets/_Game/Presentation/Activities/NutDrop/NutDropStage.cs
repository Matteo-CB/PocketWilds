using System;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T40 « Pluie de noisettes », the scene side (doc 01 Contenu S-ACT-003; doc « Mini-jeux &amp; activités » S-ACT-003,
    /// sections 1 and 6). A cozy Prairie corner under a big round hazel tree, in its own little set far below the refuge
    /// (PF_Activity_NutDrop_Set, Addressables group Activity_NutDrop: loaded on the first game, released when the player
    /// leaves the activities). The refuge stops rendering while the set shows; the set has its own portrait camera, a warm
    /// light and the Prairie's sky. The player's animal (the Squirrel if there is one, else a SmallQuadruped, else the
    /// favorite) faces the camera with a wicker basket on its head and hops left and right under the finger; hazelnuts,
    /// acorns, berries, golden hazelnuts, pinecones and the three specials fall from the leaves. Caught items drop into the
    /// basket and settle on its weave, missed ones bounce once on the meadow and fade, a pinecone bonks off the rim.
    /// Everything is built while the game loads and pooled: nothing is instantiated during a game, and nothing allocates
    /// per frame.
    /// </summary>
    public sealed partial class NutDropStage : IActivityStage, IActivityStagePreparation, IActivityPlayedAnimals, IDisposable
    {
        /// <summary>The Prairie, the only set of this build (the Forest's theme opens with its set: data only).</summary>
        public static readonly ContentId PrairieTheme = ContentId.FromSource("biome.grassland");

        private static readonly ContentId SquirrelSpecies = ContentId.FromSource("species.squirrel");

        private readonly ActivityStageHost _host;
        private readonly GameContext _context;
        private readonly IActivityController _controller;
        private readonly INutDropGame _game;
        private readonly Action _lateTick;

        // Loading (doc Mini-jeux, section 1: open in under a second).
        private AsyncOperationHandle<GameObject> _handle;
        private GameObject _setObject;
        private NutDropSetView _set;
        private NutDropSetData _data;
        private ActivityAnimalViews _views;
        private bool _failed;
        private bool _warmed;
        private bool _disposed;
        private bool _ownsHandle;
        private uint _pendingAnimal;
        private uint _preparedAnimal;
        private bool _hasPrepared;

        // The played animal.
        private AnimalInstance _animalData;
        private ContentId _species;
        private NutDropSeat _seat;
        private AnimalView _view;
        private Transform _socket;
        private float _animalScale = 1f;
        private float _halfWidth = 0.15f, _front = 0.2f, _back = -0.2f, _height = 0.4f;

        // The game shown.
        private ActivityStageHost _activeHost;
        private bool _entered;
        private bool _paused;
        private bool _ended;

        // Lighting while the set shows (saved and given back to the refuge).
        private bool _lightingApplied;
        private Light _savedSun;
        private bool _savedSunEnabled;
        private AmbientMode _savedAmbientMode;
        private Color _savedAmbientTop, _savedAmbientEquator, _savedAmbientBottom;
        private Color _savedSkyTop, _savedSkyHorizon, _savedSkyBottom, _savedSunTint, _savedCloudLight, _savedCloudShade;
        private Vector4 _savedSunDirection, _savedSkyParams;
        private float _savedSkyBlend;
        private int _framedWidth, _framedHeight;

        private static readonly int SkyBlendId = Shader.PropertyToID("_PW_SkyBlend");
        private static readonly int SkyTopId = Shader.PropertyToID("_PW_SkyTop");
        private static readonly int SkyHorizonId = Shader.PropertyToID("_PW_SkyHorizon");
        private static readonly int SkyBottomId = Shader.PropertyToID("_PW_SkyBottom");
        private static readonly int SunTintId = Shader.PropertyToID("_PW_SunTint");
        private static readonly int SunDirectionId = Shader.PropertyToID("_PW_SunDirection");
        private static readonly int CloudLightId = Shader.PropertyToID("_PW_CloudLight");
        private static readonly int CloudShadeId = Shader.PropertyToID("_PW_CloudShade");
        private static readonly int SkyParamsId = Shader.PropertyToID("_PW_SkyParams");

        /// <summary>The shipped stage: its set comes from Addressables.</summary>
        public NutDropStage(ActivityStageHost host) : this(host, null, null)
        {
        }

        /// <summary>
        /// Tests and captures: a given controller (else the nut drop game) and a given set prefab (else the Addressables set).
        /// With a prefab the set is built at once (edit mode captures).
        /// </summary>
        public NutDropStage(ActivityStageHost host, GameObject setPrefab, IActivityController controller)
        {
            _host = host;
            _context = host?.Context;
            _controller = controller ?? CreateController();
            _game = _controller as INutDropGame ?? throw new ArgumentException("The nut drop stage needs an INutDropGame.", nameof(controller));
            _lateTick = LateTick;
            AllocateItemState(_game.ItemCapacity);
            if (setPrefab != null)
                BuildSet(setPrefab);
            else
                StartLoading();
        }

        /// <summary>The one place the stage's game is made (NS's simulation).</summary>
        public static IActivityController CreateController() => new NutDropController();

        /// <summary>
        /// Registers the nut drop stage in the presenter's registry (the presenter calls it once): the factory, and the themes
        /// it ships (the Prairie; the hub shows only those).
        /// </summary>
        public static void Register(ActivityStageRegistry registry)
        {
            if (registry == null)
                return;
            registry.Register(ActivityId.NutRain, host => new NutDropStage(host));
            registry.SetThemes(ActivityId.NutRain, PrairieTheme);
        }

        // ---------------- IActivityStage ----------------

        public ActivityId Activity => ActivityId.NutRain;

        public IActivityController Controller => _controller;

        /// <summary>The nut drop game the stage plays (tests).</summary>
        public INutDropGame Game => _game;

        /// <summary>
        /// The set is loaded and built, the played animal's view warmed in its pool, the shaders warmed. Polled by the
        /// presenter each frame while it loads. A set that cannot load never blocks the game (it then plays on without its
        /// visuals, and says so in the log).
        /// </summary>
        public bool Ready => Advance();

        public NutDropSetView Set => _set;

        /// <summary>The set's Addressables handle is held (released with <see cref="Dispose"/>).</summary>
        public bool SetLoaded => _handle.IsValid();

        public bool Shown => _entered;

        /// <summary>The played animal's view (null before a game or without a visual).</summary>
        public AnimalView Animal => _view;

        public uint PlayedAnimal => _preparedAnimal;

        public Camera Camera => _data != null ? _data.camera : null;

        public void Prepare(in ActivityRequest request)
        {
            // Doc 01 S-ACT-003: the Squirrel if the player has one, else a SmallQuadruped, else the favorite.
            uint chosen = ChooseAnimal(_context);
            _pendingAnimal = chosen != 0 ? chosen : request.Animal0;
            _hasPrepared = true;
            Advance();
        }

        /// <summary>The animal that plays (its bond and species XP): the one the stage chose in <see cref="Prepare"/>.</summary>
        public int PlayedAnimals(out uint first, out uint second, out uint third)
        {
            second = third = 0;
            first = _pendingAnimal != 0 ? _pendingAnimal : _preparedAnimal;
            return first != 0 ? 1 : 0;
        }

        /// <summary>
        /// Doc 01 S-ACT-003: « l'Écureuil s'il en a un, sinon un SmallQuadruped, sinon l'animal favori ». A favorite of the
        /// kind first, then the first one owned; 0 when the collection is empty. Once per game start (never per frame).
        /// </summary>
        public static uint ChooseAnimal(GameContext context)
        {
            var collection = context != null && context.Save != null ? context.State.Collection : null;
            if (collection == null || collection.Count == 0)
                return 0;
            uint squirrel = 0, squirrelFavorite = 0, small = 0, smallFavorite = 0, favorite = 0;
            for (int i = 0; i < collection.Count; i++)
            {
                ref readonly var animal = ref collection[i];
                if (animal.Species == SquirrelSpecies)
                {
                    if (squirrel == 0)
                        squirrel = animal.InstanceId;
                    if (animal.IsFavorite && squirrelFavorite == 0)
                        squirrelFavorite = animal.InstanceId;
                }
                else if (context.Content != null && context.Content.Species.TryGet(animal.Species, out var species)
                         && species.RigFamily == RigFamily.SmallQuadruped)
                {
                    if (small == 0)
                        small = animal.InstanceId;
                    if (animal.IsFavorite && smallFavorite == 0)
                        smallFavorite = animal.InstanceId;
                }
                if (animal.IsFavorite && favorite == 0)
                    favorite = animal.InstanceId;
            }
            if (squirrel != 0)
                return squirrelFavorite != 0 ? squirrelFavorite : squirrel;
            if (small != 0)
                return smallFavorite != 0 ? smallFavorite : small;
            return favorite != 0 ? favorite : collection[0].InstanceId;
        }

        public void Enter(in ActivitySetup setup, ActivityStageHost host)
        {
            Advance();
            _activeHost = host ?? _host;
            _entered = true;
            _paused = false;
            _ended = false;
            if (_set == null)
            {
                GameLog.Warning(LogCategory.Presentation, "Nut Drop: no set, the game plays without its visuals.");
                return;
            }
            uint animal = _pendingAnimal != 0 ? _pendingAnimal : setup.Animal0;
            if (!_hasPrepared)
                GameLog.Warning(LogCategory.Presentation, "Nut Drop: the stage was not prepared before the game (call Prepare after Acquire).");
            if (animal == 0)
                animal = ChooseAnimal(_context);
            if (animal != _preparedAnimal || _species == ContentId.None)
                PrepareAnimal(animal);
            _setObject.SetActive(true);
            ShowAnimal();
            FrameCamera();
            ApplyLighting();
            if (_data.camera != null)
                _data.camera.enabled = true;
            _activeHost?.SetWorldVisible?.Invoke(false);
            BeginVisuals();
        }

        public void SetPaused(bool paused)
        {
            if (!_entered || _paused == paused)
                return;
            _paused = paused;
            if (paused)
                CancelPointer();
            if (_view != null)
                _view.SetPlaybackSpeed(paused ? 0f : 1f);
            PauseEffects(paused);
        }

        public void End()
        {
            if (!_entered || _ended)
                return;
            DrainEvents();
            _ended = true;
            CancelPointer();
            HideAids();
            Celebrate();
        }

        public void Exit()
        {
            if (!_entered)
                return;
            _entered = false;
            _ended = false;
            if (_paused && _view != null)
                _view.SetPlaybackSpeed(1f);
            _paused = false;
            CancelPointer();
            HideAids();
            _game.ClearEvents();
            _hasPrepared = false;
            if (_set == null)
                return;
            HideVisuals();
            HideAnimal();
            if (_data.camera != null)
                _data.camera.enabled = false;
            RestoreLighting();
            _setObject.SetActive(false);
            _activeHost?.SetWorldVisible?.Invoke(true);
        }

        /// <summary>Leaving the activities (the registry's ReleaseAll): the set and its animal views go, the bundle is released.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            Exit();
            _disposed = true;
            _view = null;
            _socket = null;
            _views?.Dispose();
            _views = null;
            if (_set != null)
                _set.LateTick = null;
            if (_setObject != null)
                DestroyObject(_setObject);
            _setObject = null;
            _set = null;
            _data = null;
            if (_ownsHandle && _handle.IsValid())
                Addressables.Release(_handle);
            _handle = default;
        }

        // ---------------- loading ----------------

        private void StartLoading()
        {
            if (!AddressExists(NutDropLayout.SetAddress))
            {
                _failed = true;
                GameLog.Error(LogCategory.Presentation,
                    $"Nut Drop: no Addressables entry {NutDropLayout.SetAddress} (run Pocket Wilds/Activities/Build Nut Drop (T40)).");
                return;
            }
            _handle = Addressables.LoadAssetAsync<GameObject>(NutDropLayout.SetAddress);
            _ownsHandle = true;
        }

        private static bool AddressExists(string address)
        {
            foreach (var locator in Addressables.ResourceLocators)
                if (locator.Locate(address, typeof(GameObject), out _))
                    return true;
            return false;
        }

        /// <summary>Moves the loading on: the set, then the pending animal, then the warm-up frame.</summary>
        private bool Advance()
        {
            if (_disposed)
                return false;
            if (_set == null && !_failed)
            {
                if (!_handle.IsValid() || !_handle.IsDone)
                    return false;
                if (_handle.Status != AsyncOperationStatus.Succeeded || _handle.Result == null)
                {
                    _failed = true;
                    GameLog.Error(LogCategory.Presentation, "Nut Drop: the set did not load (" + NutDropLayout.SetAddress + ").");
                }
                else
                {
                    BuildSet(_handle.Result);
                }
            }
            if (_set == null)
                return true;
            if (_pendingAnimal != 0 && _preparedAnimal != _pendingAnimal)
                PrepareAnimal(_pendingAnimal);
            if (!_warmed && _species != ContentId.None && !_entered)
                Prewarm();
            return _pendingAnimal == 0 || _preparedAnimal == _pendingAnimal;
        }

        private void BuildSet(GameObject prefab)
        {
            var parent = _host != null ? _host.Root : null;
            _setObject = Object.Instantiate(prefab, NutDropLayout.SetPosition, Quaternion.identity, parent);
            _setObject.name = NutDropLayout.SetPrefabName;
            _set = _setObject.GetComponent<NutDropSetView>();
            if (_set == null || _set.Data == null || _set.Data.origin == null)
            {
                GameLog.Error(LogCategory.Presentation, "Nut Drop: the set prefab has no NutDropSetView (rebuild it with NutDropPipeline).");
                DestroyObject(_setObject);
                _setObject = null;
                _set = null;
                _failed = true;
                return;
            }
            _data = _set.Data;
            _set.LateTick = _lateTick;
            if (_data.camera != null)
            {
                _data.camera.enabled = false;
                _data.camera.fieldOfView = NutDropLayout.CameraFov;
                _data.camera.nearClipPlane = NutDropLayout.CameraNear;
                _data.camera.farClipPlane = NutDropLayout.CameraFar;
            }
            if (_data.light != null)
                _data.light.enabled = false;
            int missing = _set.Bind(_context != null ? _context.Palettes : null);
            if (missing > 0)
                GameLog.Warning(LogCategory.Presentation, $"Nut Drop: {missing} set palettes unknown (DEF_Palettes_NutDrop in the content database?).");
            _views = new ActivityAnimalViews(_context, _data.origin, "Animal", 1);
            BuildProps();
            _setObject.SetActive(false);
        }

        /// <summary>The played animal: its species' view warmed in the pool (instantiated now, never during the game), its seat.</summary>
        private void PrepareAnimal(uint instanceId)
        {
            _preparedAnimal = instanceId;
            if (_context == null || _context.Save == null || _set == null)
                return;
            var collection = _context.State.Collection;
            if (!collection.TryGet(instanceId, out var animal))
            {
                if (collection.Count == 0)
                    return;
                animal = collection[0];
            }
            PrepareAnimal(in animal);
            _preparedAnimal = instanceId;
        }

        /// <summary>The given animal on the set (captures and tools pass one directly; the game goes through <see cref="Prepare"/>).</summary>
        public void PrepareAnimal(in AnimalInstance animal)
        {
            _preparedAnimal = animal.InstanceId;
            _hasPrepared = true;
            if (_set == null || _views == null)
                return;
            if (_view != null)
                HideAnimal();
            _animalData = animal;
            _species = animal.Species;
            if (_views.Warm(animal.Species, 1) == 0)
            {
                GameLog.Warning(LogCategory.Presentation, "Nut Drop: no visual for the played animal's species.");
                _seat = null;
                return;
            }
            _views.TryGetEntry(animal.Species, out var entry);
            _seat = entry != null ? _set.Seat(entry.visualKey) : null;
            if (_seat == null)
                GameLog.Warning(LogCategory.Presentation, "Nut Drop: " + (entry != null ? entry.visualKey : "the species")
                    + " has no measured seat (run Build Nut Drop): the basket sits at a default height.");
            // The body the items keep clear of: the visual catalog's measured shape, else the seat's.
            if (entry != null && entry.behavior != null)
            {
                var body = entry.behavior.body;
                _halfWidth = body.halfWidth;
                _front = body.front;
                _back = body.back;
                _height = body.height;
            }
            else if (_seat != null)
            {
                _halfWidth = _seat.halfWidth;
                _front = _seat.front;
                _back = _seat.back;
                _height = _seat.height;
            }
            if (_seat != null)
            {
                // The seat's body is measured on the mesh (ears and tail included): the larger of the two.
                _halfWidth = Mathf.Max(_halfWidth, _seat.halfWidth);
                _height = Mathf.Max(_height, _seat.height);
                _front = Mathf.Max(_front, _seat.front);
                _back = Mathf.Min(_back, _seat.back);
            }
        }

        /// <summary>
        /// One frame drawn offscreen while the game loads, the animal, the basket, every item kind and the effects in view:
        /// their materials and variants are compiled now, never on the game's first frame.
        /// </summary>
        private void Prewarm()
        {
            _warmed = true;
            var camera = _data.camera;
            if (camera == null)
                return;
            _setObject.SetActive(true);
            ShowAnimal();
            FrameCamera();
            WarmProps(true);
            ApplyLighting();
            var target = RenderTexture.GetTemporary(64, 128, 24);
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
            }
            catch (Exception e)
            {
                GameLog.Warning(LogCategory.Presentation, "Nut Drop: warm-up frame failed: " + e.Message);
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.ReleaseTemporary(target);
                RestoreLighting();
                WarmProps(false);
                HideAnimal();
                _setObject.SetActive(_entered);
            }
        }

        private void ShowAnimal()
        {
            if (_views == null || _species == ContentId.None)
                return;
            if (_view == null)
            {
                var origin = _data.origin.position;
                _view = _views.Show(in _animalData, origin, NutDropLayout.Yaw(0f), true);
                if (_view == null)
                    return;
                _animalScale = Mathf.Max(0.1f, _view.transform.localScale.x);
                _socket = _view.SocketHeadTop != null ? _view.SocketHeadTop : FindDeep(_view.transform, "SOCK_HeadTop");
            }
            _view.gameObject.SetActive(true);
        }

        private void HideAnimal()
        {
            if (_views != null)
                _views.HideAll();
            _view = null;
            _socket = null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null)
                return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;
                var found = FindDeep(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        // ---------------- the camera (portrait: the canopy at the top, the animal at the bottom) ----------------

        /// <summary>
        /// The camera keeps the whole field across (its edges and a margin) and, on a wide screen, the floor and the canopy's
        /// spawn line in height; the floor sits <see cref="NutDropLayout.FloorScreenShare"/> above the bottom of the screen.
        /// </summary>
        private void FrameCamera()
        {
            var camera = _data.camera;
            if (camera == null)
                return;
            camera.fieldOfView = NutDropLayout.CameraFov;
            _framedWidth = camera.pixelWidth;
            _framedHeight = camera.pixelHeight;
            float aspect = camera.aspect > 0.01f ? camera.aspect : 0.4615f;
            float tanV = Mathf.Tan(NutDropLayout.CameraFov * 0.5f * Mathf.Deg2Rad);
            float halfWidth = (_game.HalfWidth + NutDropLayout.FieldMargin) * NutDropLayout.FieldScale;
            // Height needed: the floor at its share of the screen, the spawn line under the canopy's share.
            float span = _data.spawnY / Mathf.Max(0.2f, 1f - NutDropLayout.FloorScreenShare - NutDropLayout.CanopyScreenShare);
            float halfHeight = Mathf.Max(halfWidth / aspect, span * 0.5f);
            float distance = halfHeight / tanV;
            var rotation = NutDropLayout.CameraRotation;
            var forward = rotation * Vector3.forward;
            var origin = _data.origin.position;
            // The point of the field plane at the screen's center: the floor sits FloorScreenShare above the bottom.
            var center = origin + Vector3.up * (halfHeight * (1f - 2f * NutDropLayout.FloorScreenShare));
            camera.transform.SetPositionAndRotation(center - forward * distance, rotation);
        }

        // ---------------- lighting ----------------

        /// <summary>
        /// The Prairie's warm day under the hazel: the set's sun becomes the main light (the refuge's is switched off, the
        /// refuge does not render anyway), the ambient and the sky globals take the set's values. Everything is given back on
        /// exit.
        /// </summary>
        private void ApplyLighting()
        {
            if (_lightingApplied || _data == null)
                return;
            _lightingApplied = true;
            _savedSun = RenderSettings.sun;
            _savedSunEnabled = _savedSun != null && _savedSun.enabled;
            _savedAmbientMode = RenderSettings.ambientMode;
            _savedAmbientTop = RenderSettings.ambientSkyColor;
            _savedAmbientEquator = RenderSettings.ambientEquatorColor;
            _savedAmbientBottom = RenderSettings.ambientGroundColor;
            _savedSkyBlend = Shader.GetGlobalFloat(SkyBlendId);
            _savedSkyTop = Shader.GetGlobalColor(SkyTopId);
            _savedSkyHorizon = Shader.GetGlobalColor(SkyHorizonId);
            _savedSkyBottom = Shader.GetGlobalColor(SkyBottomId);
            _savedSunTint = Shader.GetGlobalColor(SunTintId);
            _savedSunDirection = Shader.GetGlobalVector(SunDirectionId);
            _savedCloudLight = Shader.GetGlobalColor(CloudLightId);
            _savedCloudShade = Shader.GetGlobalColor(CloudShadeId);
            _savedSkyParams = Shader.GetGlobalVector(SkyParamsId);

            var light = _data.light;
            if (light != null)
            {
                light.shadows = _savedSun != null ? _savedSun.shadows : LightShadows.Soft;
                light.enabled = true;
                RenderSettings.sun = light;
            }
            if (_savedSun != null && _savedSun != light)
                _savedSun.enabled = false;
            var refugeCamera = _activeHost != null && _activeHost.CameraRig != null ? _activeHost.CameraRig.Camera : null;
            if (_data.camera != null && refugeCamera != null
                && refugeCamera.TryGetComponent<UniversalAdditionalCameraData>(out var from)
                && _data.camera.TryGetComponent<UniversalAdditionalCameraData>(out var to))
            {
                // The quality tier's choices (shadows, post) follow the refuge camera's.
                to.renderShadows = from.renderShadows;
                to.renderPostProcessing = from.renderPostProcessing;
                to.antialiasing = from.antialiasing;
            }
            if (_data.camera != null && refugeCamera != null)
                _data.camera.depth = refugeCamera.depth + 1f;
            KeepLighting();
        }

        /// <summary>
        /// Keeps the set's ambient and sky while it shows (the refuge's clock may rewrite them once a minute): a few compares
        /// a frame, the writes only on a difference.
        /// </summary>
        private void KeepLighting()
        {
            if (!_lightingApplied)
                return;
            if (RenderSettings.ambientMode != AmbientMode.Trilight)
                RenderSettings.ambientMode = AmbientMode.Trilight;
            if (RenderSettings.ambientSkyColor != _data.ambientTop)
                RenderSettings.ambientSkyColor = _data.ambientTop;
            if (RenderSettings.ambientEquatorColor != _data.ambientEquator)
                RenderSettings.ambientEquatorColor = _data.ambientEquator;
            if (RenderSettings.ambientGroundColor != _data.ambientBottom)
                RenderSettings.ambientGroundColor = _data.ambientBottom;
            var top = _data.skyTop.linear;
            if (Shader.GetGlobalColor(SkyTopId) == top && Shader.GetGlobalFloat(SkyBlendId) == 1f)
                return;
            Shader.SetGlobalFloat(SkyBlendId, 1f);
            Shader.SetGlobalColor(SkyTopId, top);
            Shader.SetGlobalColor(SkyHorizonId, _data.skyHorizon.linear);
            Shader.SetGlobalColor(SkyBottomId, _data.skyBottom.linear);
            Shader.SetGlobalColor(SunTintId, _data.sunGlowTint.linear);
            Shader.SetGlobalColor(CloudLightId, _data.cloudLight.linear);
            Shader.SetGlobalColor(CloudShadeId, _data.cloudShade.linear);
            Shader.SetGlobalVector(SkyParamsId, new Vector4(_data.cloudCoverage, _data.sunGlow, 0f, 0f));
            var toSun = _data.light != null ? -_data.light.transform.forward : new Vector3(0.3f, 0.7f, -0.6f);
            Shader.SetGlobalVector(SunDirectionId, new Vector4(toSun.x, toSun.y, toSun.z, 0f));
        }

        private void RestoreLighting()
        {
            if (!_lightingApplied)
                return;
            _lightingApplied = false;
            if (_data != null && _data.light != null)
                _data.light.enabled = false;
            if (_savedSun != null)
                _savedSun.enabled = _savedSunEnabled;
            RenderSettings.sun = _savedSun;
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientSkyColor = _savedAmbientTop;
            RenderSettings.ambientEquatorColor = _savedAmbientEquator;
            RenderSettings.ambientGroundColor = _savedAmbientBottom;
            Shader.SetGlobalFloat(SkyBlendId, _savedSkyBlend);
            Shader.SetGlobalColor(SkyTopId, _savedSkyTop);
            Shader.SetGlobalColor(SkyHorizonId, _savedSkyHorizon);
            Shader.SetGlobalColor(SkyBottomId, _savedSkyBottom);
            Shader.SetGlobalColor(SunTintId, _savedSunTint);
            Shader.SetGlobalVector(SunDirectionId, _savedSunDirection);
            Shader.SetGlobalColor(CloudLightId, _savedCloudLight);
            Shader.SetGlobalColor(CloudShadeId, _savedCloudShade);
            Shader.SetGlobalVector(SkyParamsId, _savedSkyParams);
            _savedSun = null;
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
