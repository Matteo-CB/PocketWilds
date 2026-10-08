using System;
using System.Collections.Generic;
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
    /// T38 « Toilettage », the scene side (doc 01 Contenu S-ACT-001 and A15; doc « Mini-jeux &amp; activités » S-ACT-001,
    /// sections 1 and 6). A close-up on the player's own animal standing on a soft grass mound, in its own little set far
    /// below the refuge (PF_Activity_Grooming_Set, Addressables group Activity_Grooming: loaded on the first game, released
    /// when the player leaves the activities). The refuge camera stops while the set shows; the set has its own camera, the
    /// Prairie's warm light and sky. The finger brushes the coat's measured zones (a dust that fades pass after pass, soft
    /// foam, round glints when a zone comes clean), taps the leaves and burrs off, pops the soap bubbles, takes the golden
    /// brush, then picks a bow in the HUD that flies onto the head. The animal leans into the brush and half closes its
    /// eyes, giggles when brushed where it is already clean, sighs happily at the end under hearts.
    /// Everything is built while the game loads (under a second) and pooled: nothing is instantiated during a game, and
    /// nothing allocates per frame (no LINQ, no closures, labels and arrays only written on a change).
    /// </summary>
    public sealed partial class GroomingStage : IActivityStage, IActivityStagePreparation, IActivityPickerActions, IDisposable
    {
        /// <summary>The Prairie, the only set of this build (the other biomes' themes open with their sets: data only).</summary>
        public static readonly ContentId PrairieTheme = ContentId.FromSource("biome.grassland");

        private readonly ActivityStageHost _host;
        private readonly GameContext _context;
        private readonly IActivityController _controller;
        private readonly IGroomingGame _game;
        private readonly Dictionary<string, GroomingAnimal> _animals = new Dictionary<string, GroomingAnimal>(4);
        private readonly float[] _bodyX = new float[GroomingLayout.MaxZones];
        private readonly float[] _bodyY = new float[GroomingLayout.MaxZones];
        private readonly float[] _bodyZ = new float[GroomingLayout.MaxZones];
        private readonly Action _lateTick;

        // Loading (doc Mini-jeux, section 1: open in under a second).
        private AsyncOperationHandle<GameObject> _handle;
        private GameObject _setObject;
        private GroomingSetView _set;
        private GroomingSetData _data;
        private bool _failed;
        private bool _warmed;
        private bool _disposed;
        private bool _ownsHandle;
        private uint _pendingAnimal;
        private ContentId _pendingTheme;
        private uint _preparedAnimal;
        private Material _dustMaterial;

        // The game shown.
        private GroomingAnimal _animal;
        private AnimalInstance _animalData;
        private ContentId _species;
        private GroomingTheme _theme;
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
        public GroomingStage(ActivityStageHost host) : this(host, null, null)
        {
        }

        /// <summary>
        /// Tests and captures: a given controller (else the grooming game) and a given set prefab (else the Addressables
        /// set). With a prefab the set is built at once (edit mode captures).
        /// </summary>
        public GroomingStage(ActivityStageHost host, GameObject setPrefab, IActivityController controller)
        {
            _host = host;
            _context = host?.Context;
            _controller = controller ?? CreateController();
            _game = _controller as IGroomingGame ?? throw new ArgumentException("The grooming stage needs an IGroomingGame.", nameof(controller));
            _stroke = new GroomingStroke(GroomingLayout.MaxZones);
            _lateTick = LateTick;
            if (setPrefab != null)
                BuildSet(setPrefab);
            else
                StartLoading();
        }

        /// <summary>The one place the stage's game is made (GS's simulation).</summary>
        /// <summary>The HUD icons of the bows (in <see cref="GroomingBow"/> order) and of the golden brush.</summary>
        private static readonly string[] BowIconNames = { "ICO_Bow_Pink", "ICO_Bow_SkyBlue", "ICO_Bow_Cream" };

        private const string GoldenBrushIconName = "ICO_GoldenBrush";

        public static IActivityController CreateController() => new GroomingController();

        /// <summary>
        /// Registers the grooming stage in the presenter's registry (the presenter calls it once): the factory, and the
        /// themes it ships (the Prairie; the hub shows only those).
        /// </summary>
        public static void Register(ActivityStageRegistry registry)
        {
            if (registry == null)
                return;
            registry.Register(ActivityId.Grooming, host => new GroomingStage(host));
            registry.SetThemes(ActivityId.Grooming, PrairieTheme);
        }

        // ---------------- IActivityStage ----------------

        public ActivityId Activity => ActivityId.Grooming;

        public IActivityController Controller => _controller;

        /// <summary>The grooming game the stage plays (tests).</summary>
        public IGroomingGame Game => _game;

        /// <summary>
        /// The set is loaded and built, the played animal measured and given to the game, its shaders warmed. Polled by the
        /// presenter each frame while it loads: each poll moves the loading on. A set that cannot load never blocks the game
        /// (it then plays on without its visuals, and says so in the log).
        /// </summary>
        public bool Ready => Advance();

        public GroomingSetView Set => _set;

        /// <summary>The set's Addressables handle is held (released with <see cref="Dispose"/>).</summary>
        public bool SetLoaded => _handle.IsValid();

        public bool Shown => _entered;

        public GroomingAnimal Animal => _animal;

        public Camera Camera => _data != null ? _data.camera : null;

        public void Prepare(in ActivityRequest request)
        {
            _pendingAnimal = request.Animal0 != 0 ? request.Animal0 : FirstAnimal();
            _pendingTheme = request.Theme;
            Advance();
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
                GameLog.Warning(LogCategory.Presentation, "Grooming: no set, the game plays without its visuals.");
                return;
            }
            uint animal = setup.Animal0 != 0 ? setup.Animal0 : _pendingAnimal;
            if (_animal == null || (animal != 0 && animal != _preparedAnimal))
            {
                // The presenter did not call Prepare: measured now, after the game began (its zones may be the game's own).
                GameLog.Warning(LogCategory.Presentation, "Grooming: the stage was not prepared before the game (call Prepare after Acquire).");
                PrepareAnimal(animal != 0 ? animal : FirstAnimal());
            }
            _theme = _set.Theme(setup.Theme.IsValid ? setup.Theme : PrairieTheme);
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
                CancelStroke();
            if (_animal != null)
                _animal.View.SetPlaybackSpeed(paused ? 0f : 1f);
            PauseEffects(paused);
        }

        public void End()
        {
            if (!_entered || _ended)
                return;
            DrainEvents();
            _ended = true;
            CancelStroke();
            HideAids();
            Celebrate();
        }

        public void Exit()
        {
            if (!_entered)
                return;
            _entered = false;
            _ended = false;
            if (_paused && _animal != null)
                _animal.View.SetPlaybackSpeed(1f);
            _paused = false;
            CancelStroke();
            HideAids();
            _game.ClearEvents();
            if (_set == null)
                return;
            HideVisuals();
            if (_animal != null)
            {
                _animal.ClearDust();
                _animal.View.gameObject.SetActive(false);
            }
            if (_data.camera != null)
                _data.camera.enabled = false;
            RestoreLighting();
            _setObject.SetActive(false);
            _activeHost?.SetWorldVisible?.Invoke(true);
        }

        /// <summary>Leaving the activities (the registry's ReleaseAll): the set and its animals go, the bundle is released.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            Exit();
            _disposed = true;
            foreach (var pair in _animals)
                pair.Value?.Destroy();
            _animals.Clear();
            _animal = null;
            if (_set != null)
                _set.LateTick = null;
            if (_setObject != null)
                DestroyObject(_setObject);
            _setObject = null;
            _set = null;
            _data = null;
            if (_dustMaterial != null)
                DestroyObject(_dustMaterial);
            _dustMaterial = null;
            if (_ownsHandle && _handle.IsValid())
                Addressables.Release(_handle);
            _handle = default;
        }

        // ---------------- loading ----------------

        private void StartLoading()
        {
            if (!AddressExists(GroomingLayout.SetAddress))
            {
                _failed = true;
                GameLog.Error(LogCategory.Presentation,
                    $"Grooming: no Addressables entry {GroomingLayout.SetAddress} (run Pocket Wilds/Activities/Build Grooming (T38)).");
                return;
            }
            _handle = Addressables.LoadAssetAsync<GameObject>(GroomingLayout.SetAddress);
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
                    GameLog.Error(LogCategory.Presentation, "Grooming: the set did not load (" + GroomingLayout.SetAddress + ").");
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
            if (!_warmed && _animal != null && !_entered)
                Prewarm();
            return _pendingAnimal == 0 || _preparedAnimal == _pendingAnimal;
        }

        private void BuildSet(GameObject prefab)
        {
            var parent = _host != null ? _host.Root : null;
            _setObject = Object.Instantiate(prefab, GroomingLayout.SetPosition, Quaternion.identity, parent);
            _setObject.name = GroomingLayout.SetPrefabName;
            _set = _setObject.GetComponent<GroomingSetView>();
            if (_set == null || _set.Data == null || _set.Data.animalAnchor == null)
            {
                GameLog.Error(LogCategory.Presentation, "Grooming: the set prefab has no GroomingSetView (rebuild it with GroomingPipeline).");
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
                _data.camera.fieldOfView = GroomingLayout.CameraFov;
                _data.camera.nearClipPlane = GroomingLayout.CameraNear;
                _data.camera.farClipPlane = GroomingLayout.CameraFar;
            }
            if (_data.light != null)
                _data.light.enabled = false;
            int missing = _set.Bind(_context != null ? _context.Palettes : null);
            if (missing > 0)
                GameLog.Warning(LogCategory.Presentation, $"Grooming: {missing} set palettes unknown (DEF_Palettes_Grooming in the content database?).");
            BuildProps();
            _setObject.SetActive(false);
        }

        private uint FirstAnimal()
        {
            var collection = _context != null && _context.Save != null ? _context.State.Collection : null;
            return collection != null && collection.Count > 0 ? collection[0].InstanceId : 0u;
        }

        /// <summary>The played animal's view (built once per species), its measured zones given to the game.</summary>
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

        /// <summary>
        /// The given animal on the set (captures and tools pass one directly; the game goes through <see cref="Prepare"/>):
        /// its species' view, built once, and its measured zones given to the game.
        /// </summary>
        public void PrepareAnimal(in AnimalInstance animal)
        {
            _preparedAnimal = animal.InstanceId;
            if (_context == null || _set == null || _context.Content == null)
                return;
            if (!_context.Content.Species.TryGet(animal.Species, out var species))
                return;
            if (!_animals.TryGetValue(species.VisualKey, out var groom))
            {
                groom = BuildAnimal(species.VisualKey, species.RigFamily);
                if (groom == null)
                    return;
                _animals.Add(species.VisualKey, groom);
            }
            if (_animal != null && _animal != groom)
                _animal.View.gameObject.SetActive(false);
            _animal = groom;
            _animalData = animal;
            _species = animal.Species;
            var body = groom.Body;
            int n = Mathf.Min(groom.ZoneCount, GroomingLayout.MaxZones);
            for (int z = 0; z < n; z++)
            {
                var p = body.zonePoints[z];
                _bodyX[z] = p.x;
                _bodyY[z] = p.y;
                _bodyZ[z] = p.z;
            }
            // Doc 01: the zones measured on the model, given to the game before it begins (it keeps a copy).
            // A game quit mid-way is still running for the controller: it ends here, before the next animal's zones.
            (_controller as GroomingController)?.EndGame();
            _game.SetBody(n, _bodyX, _bodyY, _bodyZ);
            // Ears and tail tips carry no debris, burr, bubble or golden brush (measured thin by the pipeline).
            if (_controller is GroomingController grooming && body.zoneThin != null && body.zoneThin.Length > 0)
                grooming.SetPropZones(body.zoneThin, n);
        }

        private GroomingAnimal BuildAnimal(string visualKey, RigFamily family)
        {
            var catalog = _context.VisualCatalog;
            if (catalog == null || !catalog.TryGet(visualKey, out var entry) || entry.prefab == null)
            {
                GameLog.Warning(LogCategory.Presentation, "Grooming: no visual for " + visualKey + ".");
                return null;
            }
            bool active = _setObject.activeSelf;
            _setObject.SetActive(true);
            var view = Object.Instantiate(entry.prefab, _data.animalAnchor);
            view.name = "Groomed_" + visualKey;
            GroomingBody body = null;
            if (_data.bodies == null || !_data.bodies.TryGet(visualKey, out body) || body == null || body.zoneCount <= 0)
            {
                GameLog.Warning(LogCategory.Presentation, $"Grooming: {visualKey} has no measured coat (run Build Grooming): an approximate one, no dust shown.");
                body = ApproximateBody(view, visualKey, GroomingLayout.ZoneCount(family));
            }
            EnsureDustMaterial(view);
            var groom = new GroomingAnimal(view, body, _dustMaterial);
            view.gameObject.SetActive(false);
            _setObject.SetActive(active);
            return groom;
        }

        /// <summary>One instance of the animal master with _PW_DUST on, worn only by the groomed animal.</summary>
        private void EnsureDustMaterial(AnimalView view)
        {
            if (_dustMaterial != null)
                return;
            var renderer = view.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var shared = renderer != null ? renderer.sharedMaterial : null;
            if (shared == null)
                return;
            _dustMaterial = new Material(shared) { name = shared.name + " (Groom dust)" };
            _dustMaterial.EnableKeyword("_PW_DUST");
        }

        /// <summary>
        /// A species without a measured coat (the pipeline was not run since it arrived): its zones on the camera's half of
        /// an ellipsoid fitted to its bounds, riding its root. The game stays fair; the fur shows no dust.
        /// </summary>
        private static GroomingBody ApproximateBody(AnimalView view, string visualKey, int zones)
        {
            var root = view.transform;
            var bounds = new Bounds(root.position + Vector3.up * 0.25f, new Vector3(0.4f, 0.5f, 0.5f));
            var renderer = view.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer != null)
                bounds = renderer.bounds;
            var center = root.InverseTransformPoint(bounds.center);
            var extents = bounds.extents * 0.8f;
            var toCamera = GroomingLayout.ToCameraLocal;
            var body = new GroomingBody { visualKey = visualKey, zoneCount = zones };
            body.zonePoints = new Vector3[zones];
            body.zoneNormals = new Vector3[zones];
            body.zoneRadii = new float[zones];
            body.bounds = new Bounds(center, bounds.size);
            body.footRadius = Mathf.Max(extents.x, extents.z);
            const float golden = 2.39996323f;
            float spacing = Mathf.Sqrt(2f * Mathf.PI * extents.x * extents.y / zones);
            for (int i = 0; i < zones; i++)
            {
                // A Fibonacci lattice on the hemisphere that faces the camera.
                float h = 1f - (i + 0.5f) / zones;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - h * h));
                float a = i * golden;
                var local = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, h);
                var direction = Quaternion.FromToRotation(Vector3.forward, toCamera) * local;
                body.zoneNormals[i] = direction.normalized;
                body.zonePoints[i] = center + Vector3.Scale(direction, extents);
                body.zoneRadii[i] = spacing * 0.6f;
            }
            body.bowScale = 1f;
            return body;
        }

        /// <summary>
        /// One frame drawn offscreen while the game loads, the animal and every prop in view: the dust variant, the set's
        /// materials and the effects are compiled now, never on the game's first frame.
        /// </summary>
        private void Prewarm()
        {
            _warmed = true;
            var camera = _data.camera;
            if (camera == null || _animal == null)
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
                GameLog.Warning(LogCategory.Presentation, "Grooming: warm-up frame failed: " + e.Message);
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.ReleaseTemporary(target);
                RestoreLighting();
                WarmProps(false);
                _animal.View.gameObject.SetActive(false);
                _setObject.SetActive(_entered);
            }
        }

        private void ShowAnimal()
        {
            if (_animal == null)
                return;
            var view = _animal.View;
            view.gameObject.SetActive(true);
            var anchor = _data.animalAnchor.position;
            if (_context != null && _context.Visuals != null && _context.Visuals.TryResolve(_animalData, anchor, GroomingLayout.AnimalYaw, out var snapshot))
                view.Configure(snapshot);
            else
                view.SetPose(anchor, GroomingLayout.AnimalRotation);
            if (view.LodGroup != null)
                view.LodGroup.ForceLOD(0);
            _animal.Update(_data.camera);
        }

        /// <summary>
        /// The camera frames the animal (its measured bounds at its scale) so it fills the width the HUD leaves, a calm sway
        /// included: a big animal pulls the camera back, a small one brings it close.
        /// </summary>
        private void FrameCamera()
        {
            var camera = _data.camera;
            if (camera == null || _animal == null)
                return;
            camera.fieldOfView = GroomingLayout.CameraFov;
            var rotation = GroomingLayout.CameraRotation;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;
            var body = _animal.Body;
            float scale = _animal.Scale;
            var anchor = _data.animalAnchor.position;
            var yaw = GroomingLayout.AnimalRotation;
            var center = anchor + yaw * (body.bounds.center * scale);
            float tanV = Mathf.Tan(GroomingLayout.CameraFov * 0.5f * Mathf.Deg2Rad);
            float aspect = camera.aspect > 0.01f ? camera.aspect : 0.5f;
            float tanH = tanV * aspect;
            float movement = Mathf.Clamp(_game.Movement, 0f, 0.2f);
            float widthShare = Mathf.Max(0.42f, GroomingLayout.FrameWidthShare - 2f * movement);
            var extents = body.bounds.extents * scale;
            float distance = 0.6f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -extents.x : extents.x, (i & 2) == 0 ? -extents.y : extents.y, (i & 4) == 0 ? -extents.z : extents.z);
                var offset = yaw * corner;
                float x = Mathf.Abs(Vector3.Dot(offset, right));
                float y = Mathf.Abs(Vector3.Dot(offset, up));
                float z = Vector3.Dot(offset, forward);
                distance = Mathf.Max(distance, x / (widthShare * tanH) - z, y / (GroomingLayout.FrameHeightShare * tanV) - z);
            }
            // The animal sits a touch low: the score and the time take the top of the screen.
            var target = center + up * (0.05f * 2f * distance * tanV);
            camera.transform.SetPositionAndRotation(target - forward * distance, rotation);
            _frameDistance = distance;
            _visibleWidth = 2f * distance * tanH;
        }

        // ---------------- lighting ----------------

        /// <summary>
        /// The Prairie's warm day on the set: its own sun becomes the main light (the refuge's is switched off, the refuge does
        /// not render anyway), the ambient and the sky globals take the set's values. Everything is given back on exit.
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
