using System.Collections.Generic;
using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T43, the set side of the garden: the set placed at <see cref="GardenLayout.SetOrigin"/> with its camera and light (the
    /// refuge's light and ambient given back on leaving), a pooled plant view per bed (its plant's stage prefab, swapped when
    /// the stage changes, with a soft green ring and a pop), ripe plants swaying (4°, 1.5 Hz), the harvest's hop and flight
    /// into the basket, the beds picked from the set camera against their measured rectangles (no physics raycast), and each
    /// bed label's screen point. Nothing allocates per frame.
    /// </summary>
    public sealed partial class GardenPresenter
    {
        /// <summary>A harvested plant on its way to the basket (or fading in place with Reduce Motion).</summary>
        private struct Flight
        {
            public bool Active;
            public GameObject Object;
            public GameObject Prefab;
            public Vector3 From;
            public Quaternion Rotation;
            public float Height;
            public float T;
        }

        /// <summary>The soil puff when a seed goes in (the arrival dust, in the beds' soil colour).</summary>
        private static readonly Color Soil = new Color(0.55f, 0.42f, 0.30f, 1f);

        private static readonly Color GrowGreen = new Color(0.62f, 0.85f, 0.50f, 1f);

        private GameObject _setObject;
        private GardenSetView _set;
        private GardenPlantVisuals _visuals;
        private Transform _poolRoot;

        // A plant view per bed.
        private readonly GameObject[] _bedObject = new GameObject[GardenLayout.PlotCount];
        private readonly GameObject[] _bedPrefab = new GameObject[GardenLayout.PlotCount];
        private readonly ContentId[] _bedPlant = new ContentId[GardenLayout.PlotCount];
        private readonly int[] _bedStage = new int[GardenLayout.PlotCount];
        private readonly float[] _bedHeight = new float[GardenLayout.PlotCount];
        private readonly float[] _popT = new float[GardenLayout.PlotCount];
        private readonly float[] _swayPhase = new float[GardenLayout.PlotCount];
        private readonly Flight[] _flights = new Flight[GardenLayout.PlotCount * 2];
        private readonly Dictionary<GameObject, Stack<GameObject>> _pool = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly List<string> _missingVisuals = new List<string>();

        // The labels' framing.
        private int _placedWidth = -1, _placedHeight = -1;
        private float _referenceTan;

        // The set's light and ambient while it shows (the refuge's saved and given back).
        private bool _lightingApplied;
        private Light _savedSun;
        private bool _savedSunEnabled;
        private AmbientMode _savedAmbientMode;
        private Color _savedAmbientTop, _savedAmbientEquator, _savedAmbientBottom;

        // Touches.
        private int _touchId = -1;
        private bool _pressing;

        /// <summary>The set (tests and captures: its camera, beds and basket).</summary>
        public GardenSetView Set => _set;

        public Camera SetCamera => _set != null ? _set.setCamera : null;

        /// <summary>The plant view a bed shows (null when bare or without a model).</summary>
        public GameObject PlantViewOf(int plot) => (uint)plot < (uint)_bedObject.Length ? _bedObject[plot] : null;

        /// <summary>The stage the bed's plant view shows (0: none).</summary>
        public int StageShown(int plot) => (uint)plot < (uint)_bedStage.Length ? _bedStage[plot] : 0;

        /// <summary>Harvested plants still flying to the basket.</summary>
        public int FlightsActive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _flights.Length; i++)
                    if (_flights[i].Active)
                        n++;
                return n;
            }
        }

        /// <summary>Plant views instantiated so far (tests: none after the first harvest of a visit).</summary>
        public int PlantViewsMade { get; private set; }

        private void InitStage()
        {
            for (int i = 0; i < _swayPhase.Length; i++)
                _swayPhase[i] = i * 1.37f;
        }

        /// <summary>
        /// The set and each plant carry their palette bindings on a WorldChunk (GardenSetPipeline, like the other sets): their
        /// colours come from the palette table once, when they are made.
        /// </summary>
        private void BindPalettes(GameObject root)
        {
            var chunk = root != null ? root.GetComponent<WorldChunk>() : null;
            var palettes = _context != null ? _context.Palettes : null;
            if (chunk == null || palettes == null)
                return;
            int missing = chunk.Bind(palettes);
            if (missing > 0)
                GameLog.Warning(LogCategory.Presentation, $"Garden: {missing} palettes unknown on {root.name} (DEF_Palettes_Garden in the content database?).");
        }

        // ---------------- the set ----------------

        private void BuildSet(GameObject prefab)
        {
            var parent = _activities?.Host?.Root;
            _setObject = Object.Instantiate(prefab, GardenLayout.SetOrigin, Quaternion.identity, parent);
            _setObject.name = GardenLayout.SetPrefabName;
            BindPalettes(_setObject);
            _set = _setObject.GetComponent<GardenSetView>();
            if (_set == null || _set.beds == null || _set.beds.Length < GardenLayout.PlotCount || _set.plantAnchors == null
                || _set.plantAnchors.Length < GardenLayout.PlotCount || _set.setCamera == null)
            {
                GameLog.Error(LogCategory.Presentation, "Garden: the set prefab has no complete GardenSetView (rebuild it with GardenSetPipeline).");
                DestroyObject(_setObject);
                _setObject = null;
                _set = null;
                _failed = true;
                return;
            }
            var pool = new GameObject("PlantPool");
            pool.transform.SetParent(_setObject.transform, false);
            pool.SetActive(false);
            _poolRoot = pool.transform;
            var camera = _set.setCamera;
            camera.enabled = false;
            _referenceTan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * (1080f / 2340f);
            if (_set.sun != null)
                _set.sun.enabled = false;
            _setObject.SetActive(false);
        }

        /// <summary>The set shows: its camera over the refuge's, its light and ambient, the plants of the beds as they are.</summary>
        private void EnterSet()
        {
            if (_set == null)
                return;
            _setObject.SetActive(true);
            var camera = _set.setCamera;
            FrameCamera();
            ApplyLighting();
            camera.enabled = true;
            _placedWidth = _placedHeight = -1;
            for (int plot = 0; plot < GardenLayout.PlotCount; plot++)
            {
                _bedStage[plot] = -1;
                _popT[plot] = -1f;
            }
        }

        private void ExitSet()
        {
            for (int i = 0; i < _flights.Length; i++)
                EndFlight(i);
            if (_set == null)
                return;
            if (_set.setCamera != null)
                _set.setCamera.enabled = false;
            RestoreLighting();
            _setObject.SetActive(false);
        }

        /// <summary>The set, its plant views and their pool go (the visit is over).</summary>
        private void ReleaseSet()
        {
            RestoreLighting();
            for (int plot = 0; plot < GardenLayout.PlotCount; plot++)
            {
                _bedObject[plot] = null;
                _bedPrefab[plot] = null;
                _bedPlant[plot] = ContentId.None;
                _bedStage[plot] = 0;
            }
            for (int i = 0; i < _flights.Length; i++)
                _flights[i] = default;
            _pool.Clear();
            if (_setObject != null)
                DestroyObject(_setObject);
            _setObject = null;
            _set = null;
            _poolRoot = null;
        }

        private void DisposeStage() => ReleaseSet();

        /// <summary>A narrower screen than the reference widens the vertical view so the whole row stays in the frame.</summary>
        private void FrameCamera()
        {
            var camera = _set.setCamera;
            if (camera == null || _referenceTan <= 0f)
                return;
            float aspect = camera.aspect > 0.01f ? camera.aspect : 1080f / 2340f;
            float tanV = Mathf.Max(_referenceTan / aspect, _referenceTan / (1080f / 2340f));
            camera.fieldOfView = 2f * Mathf.Atan(tanV) * Mathf.Rad2Deg;
        }

        private void SetCover(int plot, bool locked)
        {
            if (_set == null || _set.lockedCovers == null || plot >= _set.lockedCovers.Length)
                return;
            var cover = _set.lockedCovers[plot];
            if (cover != null && cover.activeSelf != locked)
                cover.SetActive(locked);
        }

        // ---------------- lighting ----------------

        private void ApplyLighting()
        {
            if (_lightingApplied || _set == null)
                return;
            _lightingApplied = true;
            _savedSun = RenderSettings.sun;
            _savedSunEnabled = _savedSun != null && _savedSun.enabled;
            _savedAmbientMode = RenderSettings.ambientMode;
            _savedAmbientTop = RenderSettings.ambientSkyColor;
            _savedAmbientEquator = RenderSettings.ambientEquatorColor;
            _savedAmbientBottom = RenderSettings.ambientGroundColor;
            var light = _set.sun;
            if (light != null)
            {
                light.shadows = _savedSun != null ? _savedSun.shadows : LightShadows.Soft;
                light.enabled = true;
                RenderSettings.sun = light;
            }
            if (_savedSun != null && _savedSun != light)
                _savedSun.enabled = false;
            var camera = _set.setCamera;
            var refugeCamera = _activities?.Host?.CameraRig != null ? _activities.Host.CameraRig.Camera : null;
            if (camera != null && refugeCamera != null)
            {
                if (refugeCamera.TryGetComponent<UniversalAdditionalCameraData>(out var from) && camera.TryGetComponent<UniversalAdditionalCameraData>(out var to))
                {
                    // The quality tier's choices (shadows, post) follow the refuge camera's.
                    to.renderShadows = from.renderShadows;
                    to.renderPostProcessing = from.renderPostProcessing;
                    to.antialiasing = from.antialiasing;
                }
                camera.depth = refugeCamera.depth + 1f;
            }
            KeepLighting();
        }

        /// <summary>The set's ambient while it shows (the refuge's clock may rewrite it once a minute): compares, writes on a difference.</summary>
        private void KeepLighting()
        {
            if (!_lightingApplied || _set == null)
                return;
            if (RenderSettings.ambientMode != AmbientMode.Trilight)
                RenderSettings.ambientMode = AmbientMode.Trilight;
            if (RenderSettings.ambientSkyColor != _set.ambientSky)
                RenderSettings.ambientSkyColor = _set.ambientSky;
            if (RenderSettings.ambientEquatorColor != _set.ambientEquator)
                RenderSettings.ambientEquatorColor = _set.ambientEquator;
            if (RenderSettings.ambientGroundColor != _set.ambientGround)
                RenderSettings.ambientGroundColor = _set.ambientGround;
            if (_set.sun != null && RenderSettings.sun != _set.sun)
                RenderSettings.sun = _set.sun;
        }

        private void RestoreLighting()
        {
            if (!_lightingApplied)
                return;
            _lightingApplied = false;
            if (_set != null && _set.sun != null)
                _set.sun.enabled = false;
            if (_savedSun != null)
                _savedSun.enabled = _savedSunEnabled;
            RenderSettings.sun = _savedSun;
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientSkyColor = _savedAmbientTop;
            RenderSettings.ambientEquatorColor = _savedAmbientEquator;
            RenderSettings.ambientGroundColor = _savedAmbientBottom;
            _savedSun = null;
        }

        // ---------------- the plant views ----------------

        /// <summary>
        /// The bed's plant view follows its plant and stage: a new stage swaps the prefab (from the pool), a soft green ring and
        /// a small pop when it happens in sight (<paramref name="live"/>). Allocates only when a stage prefab is first made.
        /// </summary>
        private void SyncPlant(int plot, GardenPlotState state, bool live)
        {
            if (_set == null)
                return;
            int stage = state == GardenPlotState.Growing || state == GardenPlotState.Ripe ? Mathf.Clamp(_garden.StageOf(plot), 1, 3) : 0;
            var plant = stage > 0 ? _garden.PlantOf(plot) : ContentId.None;
            if (stage == _bedStage[plot] && plant == _bedPlant[plot])
                return;
            bool grew = live && stage > 0 && plant == _bedPlant[plot] && stage > _bedStage[plot] && _bedStage[plot] > 0;
            _bedStage[plot] = stage;
            _bedPlant[plot] = plant;
            ReturnView(plot);
            if (stage == 0)
                return;
            if (!_garden.TryGetPlant(plant, out var info))
                return;
            GameObject prefab = null;
            float height = 0.3f;
            if (_visuals != null && _visuals.TryGet(info.Key, out var entry))
            {
                prefab = stage == 1 ? entry.stage1 : stage == 2 ? entry.stage2 : entry.stage3;
                height = entry.height > 0.01f ? entry.height : height;
            }
            if (prefab == null)
            {
                if (!_missingVisuals.Contains(info.Key))
                {
                    _missingVisuals.Add(info.Key);
                    GameLog.Warning(LogCategory.Presentation, "Garden: no stage model for " + info.Key + " (DEF_GardenPlantVisuals): the bed shows its soil.");
                }
                return;
            }
            var anchor = _set.plantAnchors[plot];
            var view = Take(prefab, anchor);
            _bedObject[plot] = view;
            _bedPrefab[plot] = prefab;
            _bedHeight[plot] = height * (stage == 3 ? 1f : stage == 2 ? 0.7f : 0.4f);
            _popT[plot] = grew && !ReduceMotionShown ? 0f : -1f;
            ApplyPose(plot, 0f);
            if (grew)
            {
                PlayVfx(VfxId.GardenGrow, anchor.position + anchor.up * 0.04f, GrowGreen);
                if (_context?.Audio != null)
                    GardenAudio.Stage(_context.Audio);
            }
        }

        private GameObject Take(GameObject prefab, Transform anchor)
        {
            GameObject view = null;
            if (_pool.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0 && view == null)
                    view = stack.Pop();
            }
            if (view == null)
            {
                view = Object.Instantiate(prefab, anchor, false);
                // A new plant view takes its colours once (the pool keeps them).
                BindPalettes(view);
                PlantViewsMade++;
            }
            else
            {
                view.transform.SetParent(anchor, false);
            }
            var t = view.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            if (!view.activeSelf)
                view.SetActive(true);
            return view;
        }

        private void Return(GameObject prefab, GameObject view)
        {
            if (view == null)
                return;
            if (prefab == null || _poolRoot == null)
            {
                DestroyObject(view);
                return;
            }
            view.SetActive(false);
            view.transform.SetParent(_poolRoot, false);
            if (!_pool.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<GameObject>(2);
                _pool.Add(prefab, stack);
            }
            stack.Push(view);
        }

        private void ReturnView(int plot)
        {
            if (_bedObject[plot] != null)
                Return(_bedPrefab[plot], _bedObject[plot]);
            _bedObject[plot] = null;
            _bedPrefab[plot] = null;
            _popT[plot] = -1f;
        }

        private bool ReduceMotionShown => _view != null ? _view.ReduceMotion : ReduceMotion;

        /// <summary>Ripe plants sway, a new stage pops, harvested plants fly. No allocation.</summary>
        private void TickPlants(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            for (int plot = 0; plot < GardenLayout.PlotCount; plot++)
            {
                if (_bedObject[plot] == null)
                    continue;
                if (_popT[plot] >= 0f)
                {
                    _popT[plot] += dt;
                    if (_popT[plot] >= GardenScreenLayout.StagePopSeconds)
                        _popT[plot] = -1f;
                }
                ApplyPose(plot, _clock);
            }
            for (int i = 0; i < _flights.Length; i++)
                if (_flights[i].Active)
                    TickFlight(i, dt);
        }

        private void ApplyPose(int plot, float time)
        {
            var t = _bedObject[plot].transform;
            float scale = 1f;
            if (_popT[plot] >= 0f)
            {
                float k = Mathf.Clamp01(_popT[plot] / GardenScreenLayout.StagePopSeconds);
                // Ease out with a small overshoot (back): 0.82 → 1.04 → 1.
                float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
                scale = Mathf.LerpUnclamped(GardenScreenLayout.StagePop, 1f, back);
            }
            float angle = 0f;
            if (_bedStage[plot] == 3 && !ReduceMotionShown)
                angle = GardenScreenLayout.SwayDegrees * Mathf.Sin(time * GardenScreenLayout.SwayHertz * 2f * Mathf.PI + _swayPhase[plot]);
            t.localRotation = Quaternion.Euler(0f, 0f, angle);
            t.localScale = new Vector3(scale, scale, scale);
        }

        // ---------------- effects of the touches ----------------

        private void PlayPlanted(int plot)
        {
            if (_set == null)
                return;
            var anchor = _set.plantAnchors[plot];
            PlayVfx(VfxId.ArrivalDust, anchor.position + anchor.up * 0.02f, Soil);
        }

        private void PlayWatered(int plot)
        {
            if (_set == null)
                return;
            var anchor = _set.plantAnchors[plot];
            PlayVfx(VfxId.GardenDrops, anchor.position + anchor.up * Mathf.Max(0.15f, _bedHeight[plot] + 0.08f), Color.white);
        }

        /// <summary>« Les voisines s'entraident »: two small green hearts between the bed and its neighbour of another species, once.</summary>
        private void PlayHelp(int plot)
        {
            if (_set == null)
                return;
            int neighbour = -1;
            var plant = _garden.PlantOf(plot);
            for (int side = -1; side <= 1; side += 2)
            {
                int other = plot + side;
                if ((uint)other >= (uint)Plots)
                    continue;
                var state = _garden.StateOf(other);
                if ((state == GardenPlotState.Growing || state == GardenPlotState.Ripe) && _garden.PlantOf(other) != plant)
                {
                    neighbour = other;
                    break;
                }
            }
            var a = _set.plantAnchors[plot].position;
            var b = neighbour >= 0 ? _set.plantAnchors[neighbour].position : a;
            PlayVfx(VfxId.GardenHelp, (a + b) * 0.5f + Vector3.up * 0.25f, Color.white);
        }

        /// <summary>The harvest: the effect in the plant's dye colour, the plant view hops and flies to the basket (fades with Reduce Motion).</summary>
        private void PlayHarvest(int plot, ContentId plant)
        {
            if (_set == null)
                return;
            var anchor = _set.plantAnchors[plot];
            PlayVfx(VfxId.GardenHarvest, anchor.position + anchor.up * Mathf.Max(0.1f, _bedHeight[plot] * 0.6f), TintOf(plant));
            var view = _bedObject[plot];
            if (view == null)
                return;
            int slot = -1;
            for (int i = 0; i < _flights.Length; i++)
                if (!_flights[i].Active)
                {
                    slot = i;
                    break;
                }
            if (slot < 0)
            {
                ReturnView(plot);
                return;
            }
            var t = view.transform;
            var from = t.position;
            var rotation = t.rotation;
            t.SetParent(_setObject.transform, true);
            _flights[slot] = new Flight
            {
                Active = true, Object = view, Prefab = _bedPrefab[plot], From = from, Rotation = rotation, Height = Mathf.Max(0.1f, _bedHeight[plot]), T = 0f,
            };
            _bedObject[plot] = null;
            _bedPrefab[plot] = null;
            _bedStage[plot] = 0;
            _bedPlant[plot] = ContentId.None;
        }

        private void TickFlight(int i, float dt)
        {
            ref var f = ref _flights[i];
            f.T += dt;
            var t = f.Object != null ? f.Object.transform : null;
            if (t == null)
            {
                f = default;
                return;
            }
            if (ReduceMotionShown)
            {
                // Doc 05 sheet 24, Reduce Motion: the plant fades in place, no flight.
                float k = Mathf.Clamp01(f.T / GardenScreenLayout.FadeSeconds);
                float s = 1f - k;
                t.localScale = new Vector3(s, s, s);
                if (k >= 1f)
                    EndFlight(i);
                return;
            }
            float squash = GardenScreenLayout.SquashSeconds, hop = GardenScreenLayout.HopSeconds, flight = GardenScreenLayout.FlightSeconds;
            if (f.T < squash)
            {
                // Anticipation: settles to 0.90 in height (a touch wider).
                float k = f.T / squash;
                float y = Mathf.Lerp(1f, GardenScreenLayout.Squash, k);
                float xz = Mathf.Lerp(1f, 1.05f, k);
                t.localScale = new Vector3(xz, y, xz);
                return;
            }
            if (f.T < squash + hop)
            {
                // The hop: up a little and to Bond 1.25, back toward 1.
                float k = (f.T - squash) / hop;
                float s = 1f + (GardenScreenLayout.Bond - 1f) * Mathf.Sin(k * Mathf.PI);
                t.position = f.From + Vector3.up * (f.Height * 0.35f * Mathf.Sin(k * Mathf.PI * 0.5f));
                t.localScale = new Vector3(s, s, s);
                return;
            }
            float u = Mathf.Clamp01((f.T - squash - hop) / flight);
            var start = f.From + Vector3.up * (f.Height * 0.35f);
            var end = _set != null && _set.basket != null ? _set.basket.position + Vector3.up * 0.1f : start;
            float eased = u * u * (3f - 2f * u);
            var p = Vector3.Lerp(start, end, eased) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * Mathf.Max(0.25f, f.Height));
            t.position = p;
            float scale = Mathf.Lerp(1f, 0.3f, eased);
            t.localScale = new Vector3(scale, scale, scale);
            if (u >= 1f)
                EndFlight(i);
        }

        private void EndFlight(int i)
        {
            ref var f = ref _flights[i];
            if (!f.Active)
                return;
            Return(f.Prefab, f.Object);
            f = default;
        }

        /// <summary>The plant's dye colour (doc 01 table 9.2), a soft green without one.</summary>
        private Color TintOf(ContentId plant)
        {
            if (!_garden.TryGetPlant(plant, out var info) || info.Dye == ContentId.None)
                return GrowGreen;
            for (int i = 0; i < GardenText.Dyes.Length && i < GardenText.DyeColors.Length; i++)
            {
                if (DyeIds[i] != info.Dye)
                    continue;
                uint hex = GardenText.DyeColors[i];
                return new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);
            }
            return GrowGreen;
        }

        private static ContentId[] s_dyeIds;

        private static ContentId[] DyeIds
        {
            get
            {
                if (s_dyeIds != null)
                    return s_dyeIds;
                var ids = new ContentId[GardenText.Dyes.Length];
                for (int i = 0; i < ids.Length; i++)
                    ids[i] = ContentId.FromSource(GardenText.Dyes[i]);
                return s_dyeIds = ids;
            }
        }

        private void PlayVfx(VfxId id, Vector3 position, Color tint)
        {
            var refuge = _activities?.Host?.Refuge;
            var vfx = VfxOverride ?? (refuge != null ? refuge.Vfx : null);
            if (vfx == null)
                return;
            float time = refuge != null ? refuge.SimulationTime : _clock;
            vfx.Play(id, position, time, tint);
        }

        // ---------------- the labels and the paw (screen points from the set camera) ----------------

        /// <summary>
        /// The bed labels follow the framing: placed under each bed (its near edge on screen), again when the screen changes; a
        /// locked bed's sign sits on its cover, so it never reads as the label of the bed in front of it.
        /// </summary>
        private void PlaceLabels(bool force)
        {
            var camera = SetCamera;
            if (camera == null || _view == null)
                return;
            int width = camera.pixelWidth, height = camera.pixelHeight;
            if (!force && width == _placedWidth && height == _placedHeight)
                return;
            if (width != _placedWidth || height != _placedHeight)
                FrameCamera();
            _placedWidth = width;
            _placedHeight = height;
            int plots = Plots;
            for (int plot = 0; plot < GardenLayout.PlotCount; plot++)
            {
                if (plot >= plots)
                {
                    _view.PlaceBed(plot, default, false);
                    continue;
                }
                var bed = _set.beds[plot];
                var size = plot < _set.bedSizes.Length ? _set.bedSizes[plot] : new Vector2(0.8f, 0.6f);
                var center = bed.position;
                var a = camera.WorldToScreenPoint(center + bed.forward * (size.y * 0.5f));
                var b = camera.WorldToScreenPoint(center - bed.forward * (size.y * 0.5f));
                var near = a.y < b.y ? a : b;
                var middle = camera.WorldToScreenPoint(center);
                bool locked = _garden != null && _garden.StateOf(plot) == GardenPlotState.Locked;
                _view.PlaceBed(plot, new Vector2(middle.x, locked ? middle.y : near.y), middle.z > 0f);
            }
            UpdateTutorial();
        }

        /// <summary>A bed's plant point on screen (the paw, tests and captures tap it).</summary>
        public Vector2 BedScreenPoint(int plot)
        {
            var camera = SetCamera;
            if (camera == null || (uint)plot >= (uint)GardenLayout.PlotCount)
                return Vector2.zero;
            var anchor = _set.plantAnchors[plot];
            var point = camera.WorldToScreenPoint(anchor.position + anchor.up * 0.05f);
            return new Vector2(point.x, point.y);
        }

        /// <summary>
        /// The bed under a screen point: the set camera's ray against each bed's top rectangle (its measured size), and against
        /// the same rectangle raised to half its plant's height (a tall plant is touched above its soil). −1: none.
        /// </summary>
        public int PickBed(Vector2 screen)
        {
            var camera = SetCamera;
            if (camera == null)
                return -1;
            var ray = camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            int best = -1;
            float bestDistance = float.MaxValue;
            int plots = Plots;
            for (int plot = 0; plot < plots; plot++)
            {
                var bed = _set.beds[plot];
                if (bed == null)
                    continue;
                var size = plot < _set.bedSizes.Length ? _set.bedSizes[plot] : new Vector2(0.8f, 0.6f);
                for (int level = 0; level < 2; level++)
                {
                    float lift = level == 0 ? 0f : _bedHeight[plot] * 0.5f;
                    if (level == 1 && lift <= 0.01f)
                        continue;
                    var normal = bed.up;
                    var origin = bed.position + normal * lift;
                    float facing = Vector3.Dot(ray.direction, normal);
                    if (Mathf.Abs(facing) < 1e-4f)
                        continue;
                    float distance = Vector3.Dot(origin - ray.origin, normal) / facing;
                    if (distance <= 0f || distance >= bestDistance)
                        continue;
                    var local = Quaternion.Inverse(bed.rotation) * (ray.origin + ray.direction * distance - origin);
                    if (Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f)
                    {
                        best = plot;
                        bestDistance = distance;
                    }
                }
            }
            return best;
        }

        // ---------------- touches (a tap on a bed; a touch that starts on the overlay is the overlay's) ----------------

        private void PollPointer()
        {
            if (!Application.isPlaying)
                return;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (!touch.press.isPressed)
                        continue;
                    int id = touch.touchId.ReadValue();
                    if (_touchId >= 0 && id != _touchId)
                        continue;
                    if (_touchId < 0)
                    {
                        _touchId = id;
                        Press(touch.position.ReadValue(), id);
                    }
                    return;
                }
                if (_touchId >= 0)
                {
                    _touchId = -1;
                    _pressing = false;
                    return;
                }
            }
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            if (mouse.leftButton.isPressed)
            {
                if (!_pressing)
                    Press(mouse.position.ReadValue(), -1);
            }
            else
            {
                _pressing = false;
            }
        }

        private void Press(Vector2 position, int pointerId)
        {
            _pressing = true;
            var events = EventSystem.current;
            if (events != null && events.IsPointerOverGameObject(pointerId))
                return;
            PointerDown(position);
        }

        /// <summary>A finger touches the set (pixels, origin bottom left): the bed under it, else the seed sheet closes.</summary>
        public void PointerDown(Vector2 screen)
        {
            if (_phase != Phase.Browsing || _view == null)
                return;
            int plot = PickBed(screen);
            if (plot < 0)
            {
                if (_view.SeedsShown)
                    CloseSeeds();
                return;
            }
            TapBed(plot);
        }
    }
}
