using PocketWilds.Core;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    // The set's pooled props and the animal's motion (doc 01 Contenu S-ACT-001 « Objets », « Sons et effets »; collision
    // rule: the debris lie on the fur, the bow sits on the head, the brush glides just above the fur, the animal stands on
    // its cushion and sways only over its flat top). Every prop is a renderer built with the set; a game only moves them.
    public sealed partial class GroomingStage
    {
        private const byte PropHidden = 0;
        private const byte PropShown = 1;
        private const byte PropLeaving = 2;

        private const float AppearSeconds = 0.28f;
        private const float FlySeconds = 0.75f;
        private const float FlyGravity = -3.2f;
        private const float WobbleSeconds = 0.45f;
        private const float FoamSeconds = 0.55f;
        private const float GlintPulseSeconds = 0.6f;
        private const float BowFlightSeconds = 0.5f;
        private const float HopSeconds = 0.42f;
        private const float HopHeight = 0.03f;
        private const float SwayPause = 1.6f;
        private const float MaxLeanDegrees = 3f;

        /// <summary>The soap bubble's radius on the coat (m, at the animal's scale 1), and the foam's.</summary>
        private const float BubbleWorldRadius = 0.032f;

        private const float FoamMinRadius = 0.0055f;
        private const float FoamMaxRadius = 0.0095f;

        /// <summary>Contact gap kept between a prop and the fur (collision rule: touching, never inside).</summary>
        private const float ContactGap = 0.002f;

        private readonly byte[] _debrisState = new byte[GroomingLayout.MaxDebris];
        private readonly float[] _debrisT = new float[GroomingLayout.MaxDebris];
        private readonly float[] _debrisSpin = new float[GroomingLayout.MaxDebris];
        private readonly float[] _debrisLift = new float[GroomingLayout.MaxDebris];
        private readonly float[] _debrisWobble = new float[GroomingLayout.MaxDebris];
        private readonly Vector3[] _debrisFrom = new Vector3[GroomingLayout.MaxDebris];
        private readonly Vector3[] _debrisVelocity = new Vector3[GroomingLayout.MaxDebris];
        private readonly Vector3[] _debrisAxis = new Vector3[GroomingLayout.MaxDebris];
        private readonly Quaternion[] _debrisRotation = new Quaternion[GroomingLayout.MaxDebris];
        private readonly Vector2[] _debrisScreen = new Vector2[GroomingLayout.MaxDebris];
        private Transform[] _debrisTransforms;
        private MeshFilter[] _debrisFilters;

        private readonly byte[] _bubbleState = new byte[GroomingLayout.MaxBubbles];
        private readonly float[] _bubbleT = new float[GroomingLayout.MaxBubbles];
        private readonly int[] _bubbleZone = new int[GroomingLayout.MaxBubbles];
        private readonly Vector2[] _bubbleScreen = new Vector2[GroomingLayout.MaxBubbles];
        private readonly float[] _bubbleScreenRadius = new float[GroomingLayout.MaxBubbles];

        private bool _goldenShown;
        private bool _goldenLeaving;
        private float _goldenT;
        private int _goldenZone = -1;
        private Vector2 _goldenScreen;
        private float _goldenScreenRadius;

        private byte _bowState;
        private float _bowT;
        private int _bowColor;
        private Vector3 _bowFrom;

        private float _brushShow;
        private Vector3 _brushPosition;
        private Vector3 _brushVelocity;
        private Vector3 _brushForward = Vector3.right;
        private Vector2 _brushTarget;
        private bool _brushGold;
        private Vector3 _brushScale = Vector3.one;

        private readonly int[] _foamZone = new int[GroomingLayout.FoamCount];
        private readonly float[] _foamT = new float[GroomingLayout.FoamCount];
        private readonly float[] _foamSize = new float[GroomingLayout.FoamCount];
        private readonly Vector3[] _foamOffset = new Vector3[GroomingLayout.FoamCount];
        private int _foamNext;
        private uint _foamSeed = 0x46304D21u;
        private int _glintNext;

        private readonly float[] _dustShown = new float[GroomingLayout.MaxZones];
        private readonly float[] _glintTime = new float[GroomingLayout.MaxZones];

        private float _swayX;
        private float _swayFrom;
        private float _swayTo;
        private float _hopT = -1f;
        private float _hopY;
        private float _swayTimer;
        private int _swaySide = 1;
        private int _hops;
        private float _lean;
        private float _frameDistance = 2f;
        private float _visibleWidth = 1f;

        private uint _brushValue, _goldValue, _bubbleValue, _burrValue;
        private readonly uint[] _bowValues = new uint[3];
        private uint[] _foamValues = System.Array.Empty<uint>();
        private uint[] _themeValues = System.Array.Empty<uint>();
        private GroomingTheme _valuesTheme;
        private bool _bubbleTip;

        // ---------------- read by tests and captures ----------------

        public bool DebrisShown(int debris) => (uint)debris < (uint)_debrisState.Length && _debrisState[debris] == PropShown;

        public Vector2 DebrisScreen(int debris) => _debrisScreen[debris];

        public Vector3 DebrisPosition(int debris) => _debrisTransforms != null ? _debrisTransforms[debris].position : default;

        public bool BubbleShown(int bubble) => _bubbleState[bubble] == PropShown;

        public Vector2 BubbleScreen(int bubble) => _bubbleScreen[bubble];

        public bool GoldenShown => _goldenShown && !_goldenLeaving;

        public Vector2 GoldenScreen => _goldenScreen;

        public bool BrushGold => _brushGold;

        public bool BrushVisible => _data != null && _data.brush != null && _data.brush.gameObject.activeSelf;

        public Vector3 BrushPosition => _brushPosition;

        /// <summary>The bow sits on the head socket (parented to it), its color.</summary>
        public bool BowOnHead => _bowState == 2 && _data != null && _data.bow != null && _animal != null && _animal.HeadTop != null
                                 && _data.bow.transform.parent == _animal.HeadTop;

        public int BowColor => _bowColor;

        public Transform BowTransform => _data != null && _data.bow != null ? _data.bow.transform : null;

        public float SwayOffset => _swayX;

        /// <summary>How far the animal may shift sideways with every paw still on the cushion's flat top (1 cm kept).</summary>
        public float SwayLimit => _swayLimit;

        private float _swayLimit;

        /// <summary>
        /// The sway's reach for this animal (its paws' outline turned to the grooming view, at its scale): for each paw point q,
        /// |q + s x| stays within the flat top less 1 cm. Once per game.
        /// </summary>
        private void MeasureSway()
        {
            _swayLimit = 0f;
            if (_animal == null || _data == null)
                return;
            float radius = _data.moundTopRadius - 0.01f;
            float scale = _animal.Scale;
            var body = _animal.Body;
            var yaw = GroomingLayout.AnimalRotation;
            float limit = float.MaxValue;
            var points = body.footPoints;
            if (points == null || points.Length == 0)
            {
                _swayLimit = Mathf.Max(0f, radius - body.footRadius * scale);
                return;
            }
            for (int i = 0; i < points.Length; i++)
            {
                var q = yaw * new Vector3(points[i].x * scale, 0f, points[i].y * scale);
                float room = radius * radius - q.z * q.z;
                if (room <= 0f)
                {
                    limit = 0f;
                    break;
                }
                float half = Mathf.Sqrt(room);
                limit = Mathf.Min(limit, Mathf.Min(half - q.x, half + q.x));
            }
            _swayLimit = Mathf.Max(0f, limit);
        }

        public int FoamAlive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _foamT.Length; i++)
                    if (_foamT[i] >= 0f && _foamZone[i] >= 0)
                        n++;
                return n;
            }
        }

        // ---------------- building (at load) ----------------

        private void BuildProps()
        {
            var debris = _data.debris ?? System.Array.Empty<MeshRenderer>();
            int count = Mathf.Min(debris.Length, GroomingLayout.MaxDebris);
            _debrisTransforms = new Transform[GroomingLayout.MaxDebris];
            _debrisFilters = new MeshFilter[GroomingLayout.MaxDebris];
            for (int i = 0; i < count; i++)
            {
                if (debris[i] == null)
                    continue;
                _debrisTransforms[i] = debris[i].transform;
                _debrisFilters[i] = debris[i].GetComponent<MeshFilter>();
            }
            var palettes = _context != null ? _context.Palettes : null;
            _brushValue = GroomingSetView.Packed(palettes, _data.brushPalette);
            _goldValue = GroomingSetView.Packed(palettes, _data.goldPalette);
            _bubbleValue = _data.bubbleTint;
            _burrValue = GroomingSetView.Packed(palettes, _data.burrPalette);
            for (int i = 0; i < _bowValues.Length; i++)
                _bowValues[i] = _data.bowPalettes != null && i < _data.bowPalettes.Length ? GroomingSetView.Packed(palettes, _data.bowPalettes[i]) : _brushValue;
            var foam = _data.foamTints ?? System.Array.Empty<uint>();
            _foamValues = new uint[Mathf.Max(1, foam.Length)];
            for (int i = 0; i < _foamValues.Length; i++)
                _foamValues[i] = i < foam.Length ? foam[i] : _bubbleValue;
            for (int i = 0; i < _foamZone.Length; i++)
                _foamZone[i] = -1;
            HideVisuals();
        }

        /// <summary>The theme's debris palettes, packed once per theme.</summary>
        private void PackTheme()
        {
            if (_theme == _valuesTheme)
                return;
            _valuesTheme = _theme;
            int kinds = _theme != null && _theme.debrisPalettes != null ? _theme.debrisPalettes.Length : 0;
            _themeValues = new uint[Mathf.Max(1, kinds)];
            var palettes = _context != null ? _context.Palettes : null;
            for (int i = 0; i < _themeValues.Length; i++)
                _themeValues[i] = i < kinds ? GroomingSetView.Packed(palettes, _theme.debrisPalettes[i]) : _burrValue;
        }

        // ---------------- a game starts / ends ----------------

        /// <summary>The coat as the game laid it: dust at once (no fade in), the debris popping on one after the other.</summary>
        private void BeginVisuals()
        {
            _time = 0f;
            _dt = 0f;
            _celebrated = false;
            _pickerShown = false;
            _sighAt = -1f;
            _welcomeAt = 0.35f;
            _happyAt = -10f;
            _lastPassTime = -10f;
            _content = 0.1f;
            _bubbleTip = false;
            _swayX = _swayFrom = _swayTo = 0f;
            _hopT = -1f;
            _hopY = 0f;
            _swayTimer = SwayPause * 0.6f;
            MeasureSway();
            _swaySide = 1;
            _hops = 0;
            _lean = 0f;
            HideVisuals();
            PackTheme();
            if (_animal != null)
            {
                for (int z = 0; z < _dustShown.Length; z++)
                {
                    _dustShown[z] = z < _animal.ZoneCount ? _game.Dust(z) : 0f;
                    _glintTime[z] = 0f;
                    _animal.SetZone(z, _dustShown[z], 0f);
                }
                _animal.SetContent(_content);
                _animal.Upload();
            }
            SetBrushGold(false);
            SyncItems();
            StartTutorial(_activeHost != null && _activeHost.Tutorial);
        }

        /// <summary>Every prop back in its pool (inactive), the bow off the head, the effects stopped.</summary>
        private void HideVisuals()
        {
            if (_data == null)
                return;
            for (int i = 0; i < _debrisState.Length; i++)
            {
                _debrisState[i] = PropHidden;
                if (_debrisTransforms != null && _debrisTransforms[i] != null)
                    _debrisTransforms[i].gameObject.SetActive(false);
            }
            for (int b = 0; b < _bubbleState.Length; b++)
            {
                _bubbleState[b] = PropHidden;
                if (_data.bubbles != null && b < _data.bubbles.Length && _data.bubbles[b] != null)
                    _data.bubbles[b].gameObject.SetActive(false);
            }
            for (int i = 0; i < _foamT.Length; i++)
            {
                _foamZone[i] = -1;
                if (_data.foam != null && i < _data.foam.Length && _data.foam[i] != null)
                    _data.foam[i].gameObject.SetActive(false);
            }
            _goldenShown = false;
            _goldenLeaving = false;
            if (_data.goldenBrush != null)
                _data.goldenBrush.gameObject.SetActive(false);
            _bowState = 0;
            if (_data.bow != null)
            {
                if (_data.props != null && _data.bow.transform.parent != _data.props)
                    _data.bow.transform.SetParent(_data.props, false);
                _data.bow.gameObject.SetActive(false);
            }
            HideBrushNow();
            if (_data.glints != null)
                for (int i = 0; i < _data.glints.Length; i++)
                    if (_data.glints[i] != null)
                        _data.glints[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void HideBrushNow()
        {
            _brushShow = 0f;
            if (_data != null && _data.brush != null && _data.brush.gameObject.activeSelf)
                _data.brush.gameObject.SetActive(false);
        }

        private void PauseEffects(bool paused)
        {
            if (_data == null || _data.glints == null)
                return;
            for (int i = 0; i < _data.glints.Length; i++)
            {
                var system = _data.glints[i];
                if (system == null)
                    continue;
                if (paused && system.isPlaying)
                    system.Pause(true);
                else if (!paused && system.isPaused)
                    system.Play(true);
            }
        }

        /// <summary>The warm-up frame: one of each prop in view of the camera (so its material is compiled), then hidden.</summary>
        private void WarmProps(bool on)
        {
            if (!on)
            {
                HideVisuals();
                return;
            }
            var camera = _data.camera;
            var basePoint = camera.transform.position + camera.transform.forward * (_frameDistance * 0.8f);
            var right = camera.transform.right * 0.06f;
            int slot = 0;
            if (_debrisTransforms[0] != null && _theme == null)
                _theme = _set.Theme(PrairieTheme);
            PackTheme();
            if (_theme != null && _theme.debrisMeshes != null)
            {
                for (int k = 0; k < _theme.debrisMeshes.Length && slot < GroomingLayout.MaxDebris; k++, slot++)
                    PlaceWarm(_debrisTransforms[slot], _debrisFilters[slot], _theme.debrisMeshes[k], _themeValues[k], basePoint + right * slot);
            }
            if (slot < GroomingLayout.MaxDebris)
                PlaceWarm(_debrisTransforms[slot], _debrisFilters[slot], _data.burrMesh, _burrValue, basePoint + right * slot);
            if (_data.bubbles != null && _data.bubbles.Length > 0 && _data.bubbles[0] != null)
                PlaceWarm(_data.bubbles[0].transform, null, null, _bubbleValue, basePoint - right);
            if (_data.foam != null && _data.foam.Length > 0 && _data.foam[0] != null)
                PlaceWarm(_data.foam[0].transform, null, null, _foamValues[0], basePoint - right * 2f);
            if (_data.brush != null)
                PlaceWarm(_data.brush.transform, null, null, _brushValue, basePoint - right * 3f);
            if (_data.goldenBrush != null)
            {
                _data.goldenBrush.sharedMaterial = _data.goldMaterial != null ? _data.goldMaterial : _data.goldenBrush.sharedMaterial;
                PlaceWarm(_data.goldenBrush.transform, null, null, _goldValue, basePoint - right * 4f);
            }
            if (_data.bow != null)
                PlaceWarm(_data.bow.transform, null, null, _bowValues[0], basePoint + Vector3.up * 0.06f);
            if (_data.glints != null && _data.glints.Length > 0 && _data.glints[0] != null)
            {
                _data.glints[0].transform.position = basePoint;
                _data.glints[0].Emit(1);
            }
        }

        private static void PlaceWarm(Transform target, MeshFilter filter, Mesh mesh, uint value, Vector3 position)
        {
            if (target == null)
                return;
            if (filter != null && mesh != null)
                filter.sharedMesh = mesh;
            target.position = position;
            target.gameObject.SetActive(true);
            if (target.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.SetShaderUserValue(value);
        }

        // ---------------- items of the game (debris, burrs, bubbles, golden brush) ----------------

        /// <summary>Shows what the game lays and hides what it took away (the events do the same, this catches any gap).</summary>
        private void SyncItems()
        {
            if (_animal == null || _debrisTransforms == null)
                return;
            int count = Mathf.Min(_game.DebrisCount, GroomingLayout.MaxDebris);
            for (int i = 0; i < count; i++)
            {
                bool active = _game.DebrisActive(i);
                if (active && _debrisState[i] == PropHidden)
                    ShowDebris(i);
                else if (!active && _debrisState[i] == PropShown)
                    FlyDebris(i);
            }
            for (int b = 0; b < _bubbleState.Length && b < _game.BubbleCount; b++)
            {
                bool active = _game.BubbleActive(b);
                if (active && _bubbleState[b] == PropHidden)
                    ShowBubble(b);
                else if (!active && _bubbleState[b] == PropShown)
                {
                    // Gone without a tap (the bow phase): it shrinks away softly.
                    _bubbleState[b] = PropLeaving;
                    _bubbleT[b] = -1f;
                }
            }
            int golden = _game.GoldenBrushZone;
            if (golden >= 0 && !_game.GoldenBrushActive && !_goldenShown)
                ShowGolden();
            else if (_goldenShown && !_goldenLeaving && (golden < 0 || _game.GoldenBrushActive))
            {
                _goldenLeaving = true;
                _goldenT = 0f;
            }
            if (_game.Phase == GroomingPhase.Bow && !_pickerShown && !_game.BowPlaced && !_ended)
                ShowPicker();
        }

        private void ShowDebris(int i)
        {
            var target = _debrisTransforms[i];
            if (target == null)
                return;
            bool burr = _game.DebrisIsBurr(i);
            int kinds = _theme != null && _theme.debrisMeshes != null ? _theme.debrisMeshes.Length : 0;
            int kind = kinds > 0 ? (int)(Hash(i, 0x44454231u) % (uint)kinds) : 0;
            var mesh = burr || kinds == 0 ? _data.burrMesh : _theme.debrisMeshes[kind];
            if (_debrisFilters[i] != null && mesh != null)
                _debrisFilters[i].sharedMesh = mesh;
            if (target.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.SetShaderUserValue(burr || kinds == 0 ? _burrValue : _themeValues[kind]);
            float lift = burr ? _data.burrLift : _theme != null && _theme.debrisLift != null && kind < _theme.debrisLift.Length ? _theme.debrisLift[kind] : 0.004f;
            _debrisLift[i] = lift + ContactGap;
            _debrisSpin[i] = Hash(i, 0x5350494Eu) % 360u;
            _debrisWobble[i] = 0f;
            // They pop on one after the other at the start (a game's debris all land in the first half second).
            _debrisT[i] = _time < 0.5f ? -0.04f * i : 0f;
            _debrisState[i] = PropShown;
            target.localScale = Vector3.zero;
            target.gameObject.SetActive(true);
        }

        /// <summary>H-VFX-ACT-001-02: the debris leaves the fur and flies off spinning (pooled mesh, tweened).</summary>
        private void FlyDebris(int i)
        {
            if ((uint)i >= (uint)GroomingLayout.MaxDebris || _debrisState[i] != PropShown || _debrisTransforms[i] == null)
                return;
            var target = _debrisTransforms[i];
            _debrisState[i] = PropLeaving;
            _debrisT[i] = 0f;
            _debrisFrom[i] = target.position;
            _debrisRotation[i] = target.rotation;
            int zone = _game.DebrisZone(i);
            var normal = (uint)zone < (uint)_animal.ZoneCount ? _animal.Normals[zone] : Vector3.up;
            var side = _data.camera != null ? _data.camera.transform.right : Vector3.right;
            float s = Mathf.Sqrt(_animal.Scale);
            _debrisVelocity[i] = (normal * 0.7f + Vector3.up * 1.0f + side * ((i & 1) == 0 ? 0.45f : -0.45f)) * s;
            uint h = Hash(i, 0x41584953u);
            _debrisAxis[i] = new Vector3((h & 255u) / 255f - 0.5f, ((h >> 8) & 255u) / 255f - 0.5f, ((h >> 16) & 255u) / 255f + 0.2f).normalized;
        }

        private void WobbleBurr(int i)
        {
            if ((uint)i < (uint)GroomingLayout.MaxDebris)
                _debrisWobble[i] = 1f;
        }

        private void ShowBubble(int b)
        {
            if ((uint)b >= (uint)_bubbleState.Length || _data.bubbles == null || b >= _data.bubbles.Length || _data.bubbles[b] == null)
                return;
            if (_bubbleState[b] == PropShown)
                return;
            _bubbleState[b] = PropShown;
            _bubbleT[b] = 0f;
            _bubbleZone[b] = _game.BubbleZone(b);
            var renderer = _data.bubbles[b];
            renderer.SetShaderUserValue(_bubbleValue);
            renderer.transform.localScale = Vector3.zero;
            renderer.gameObject.SetActive(true);
            if (_tutorialStep > 0 && !_bubbleTip)
            {
                _bubbleTip = true;
                ShowTip(ActivityText.GroomingTipBubble, TipSeconds);
            }
        }

        private void PopBubble(int b)
        {
            if ((uint)b >= (uint)_bubbleState.Length || _bubbleState[b] == PropHidden)
                return;
            _bubbleState[b] = PropLeaving;
            _bubbleT[b] = 0f;
            int zone = _bubbleZone[b];
            Foam(zone, 6);
            Glints(zone);
        }

        private void ShowGolden()
        {
            if (_data.goldenBrush == null || _game.GoldenBrushZone < 0)
                return;
            _goldenShown = true;
            _goldenLeaving = false;
            _goldenT = 0f;
            _goldenZone = _game.GoldenBrushZone;
            var renderer = _data.goldenBrush;
            if (_data.goldMaterial != null)
                renderer.sharedMaterial = _data.goldMaterial;
            renderer.SetShaderUserValue(_goldValue);
            renderer.transform.localScale = Vector3.zero;
            renderer.gameObject.SetActive(true);
        }

        /// <summary>The golden brush taken: the one on the coat goes, the player's brush turns gold, the HUD's boost chip runs.</summary>
        private void TakeGolden()
        {
            if (_goldenShown)
            {
                _goldenLeaving = true;
                _goldenT = 0f;
            }
            SetBrushGold(true);
            var hud = _activeHost != null ? _activeHost.Hud : null;
            float seconds = _game.GoldenBrushLeft > 0.1f ? _game.GoldenBrushLeft : 5f;
            hud?.ShowBoost(hud.Icon(GoldenBrushIconName), seconds);
        }

        private void SetBrushGold(bool gold)
        {
            _brushGold = gold;
            var brush = _data != null ? _data.brush : null;
            if (brush == null)
                return;
            var material = gold ? _data.goldMaterial : _data.brushMaterial;
            if (material != null && brush.sharedMaterial != material)
                brush.sharedMaterial = material;
            brush.SetShaderUserValue(gold ? _goldValue : _brushValue);
        }

        /// <summary>The picked bow flies up from the picker to the top of the head and settles there with a little bounce.</summary>
        private void FlyBow(int color)
        {
            var bow = _data.bow;
            if (bow == null || _animal == null || _animal.HeadTop == null)
                return;
            _bowColor = Mathf.Clamp(color, 0, 2);
            bow.SetShaderUserValue(_bowValues[_bowColor]);
            var camera = _data.camera;
            float depth = Vector3.Dot(_animal.HeadTop.position - camera.transform.position, camera.transform.forward);
            _bowFrom = camera.ScreenToWorldPoint(new Vector3(camera.pixelWidth * 0.5f, camera.pixelHeight * 0.12f, Mathf.Max(0.3f, depth)));
            bow.transform.SetParent(_data.props, true);
            bow.transform.position = _bowFrom;
            bow.transform.localScale = Vector3.zero;
            bow.gameObject.SetActive(true);
            _bowState = 1;
            _bowT = 0f;
        }

        // ---------------- foam and glints ----------------

        /// <summary>H-VFX-ACT-001-01: soft foam, a few small round bubbles that rise off the brushed zone and pop.</summary>
        private void Foam(int zone, int count)
        {
            if (_animal == null || _data.foam == null || _data.foam.Length == 0 || (uint)zone >= (uint)_animal.ZoneCount)
                return;
            float radius = (_animal.Body.zoneRadii.Length > zone ? _animal.Body.zoneRadii[zone] : 0.03f) * 0.55f;
            for (int k = 0; k < count; k++)
            {
                int slot = _foamNext;
                _foamNext = (_foamNext + 1) % Mathf.Min(_data.foam.Length, GroomingLayout.FoamCount);
                var renderer = _data.foam[slot];
                if (renderer == null)
                    continue;
                _foamZone[slot] = zone;
                _foamT[slot] = -0.05f * k;
                float a = Next01() * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Next01()) * radius;
                _foamOffset[slot] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.006f + 0.01f * Next01());
                _foamSize[slot] = Mathf.Lerp(FoamMinRadius, FoamMaxRadius, Next01());
                renderer.SetShaderUserValue(_foamValues[slot % _foamValues.Length]);
                renderer.transform.localScale = Vector3.zero;
                renderer.gameObject.SetActive(true);
            }
        }

        /// <summary>H-VFX-ACT-001-03: three round glints on a zone that came clean (round dots only), and the fur's warm sheen.</summary>
        private void Glints(int zone)
        {
            if (_animal == null || (uint)zone >= (uint)_animal.ZoneCount)
                return;
            _glintTime[zone] = 1f;
            var systems = _data.glints;
            if (systems == null || systems.Length == 0)
                return;
            var system = systems[_glintNext];
            _glintNext = (_glintNext + 1) % systems.Length;
            if (system == null)
                return;
            system.transform.position = _animal.Points[zone] + _animal.Normals[zone] * (0.012f * _animal.Scale);
            system.Play(true);
        }

        private float Next01()
        {
            _foamSeed ^= _foamSeed << 13;
            _foamSeed ^= _foamSeed >> 17;
            _foamSeed ^= _foamSeed << 5;
            return (_foamSeed & 0xFFFFFFu) / 16777216f;
        }

        private static uint Hash(int i, uint salt)
        {
            uint h = (uint)i * 2654435761u ^ salt;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            return h;
        }

        // ---------------- per frame (LateTick) ----------------

        /// <summary>
        /// The animal on its cushion: a calm sideways hop when the game asks for movement (doc 01: 0 % to 12 % of the
        /// screen's width), never past the cushion's flat top; a small lean toward the brush while brushed, pivoting on the
        /// paws of that side so no paw ever sinks into the cushion.
        /// </summary>
        private void UpdateAnimalPose(float dt)
        {
            var anchor = _data.animalAnchor.position;
            float scale = _animal.Scale;
            float limit = SwayLimit;
            float amplitude = _ended ? 0f : Mathf.Min(_game.Movement * _visibleWidth, limit);
            if (_hopT < 0f)
            {
                if (amplitude > 0.003f)
                {
                    _swayTimer -= dt;
                    if (_swayTimer <= 0f)
                    {
                        _swaySide = -_swaySide;
                        StartHop(_swaySide * amplitude * (0.6f + 0.4f * (Hash(_hops++, 0x484F5053u) & 255u) / 255f));
                        _swayTimer = SwayPause * (0.8f + 0.4f * ((Hash(_hops, 0x50415553u) & 255u) / 255f));
                    }
                }
                else if (Mathf.Abs(_swayX) > 0.002f)
                {
                    StartHop(0f);
                }
            }
            if (_hopT >= 0f)
            {
                _hopT += dt / HopSeconds;
                float t = Mathf.Min(1f, _hopT);
                _swayX = Mathf.Lerp(_swayFrom, _swayTo, t * t * (3f - 2f * t));
                _hopY = 4f * t * (1f - t) * HopHeight * scale;
                if (t >= 1f)
                {
                    _hopT = -1f;
                    _hopY = 0f;
                }
            }
            _swayX = Mathf.Clamp(_swayX, -limit, limit);

            float leanTarget = 0f;
            if (Brushing && !_ended && _data.camera != null)
            {
                float center = _data.camera.pixelWidth * 0.5f;
                leanTarget = Mathf.Clamp((_brushTarget.x - center) / (0.25f * _data.camera.pixelWidth), -1f, 1f) * MaxLeanDegrees;
            }
            _lean = Mathf.MoveTowards(_lean, leanTarget, dt * 10f);

            var position = anchor + Vector3.right * _swayX + Vector3.up * _hopY;
            var rotation = GroomingLayout.AnimalRotation;
            if (Mathf.Abs(_lean) > 0.01f)
            {
                // Pivot on the outer paws of the leaning side: the other paws lift a few millimetres, none goes down.
                var pivot = position + Vector3.right * (Mathf.Sign(_lean) * _animal.Body.footRadius * scale);
                var q = Quaternion.AngleAxis(-_lean, Vector3.forward);
                position = pivot + q * (position - pivot);
                rotation = q * rotation;
            }
            _animal.View.SetPose(position, rotation);
        }

        private void StartHop(float to)
        {
            _swayFrom = _swayX;
            _swayTo = to;
            _hopT = 0f;
        }

        private void UpdateDust(float dt)
        {
            int n = _animal.ZoneCount;
            for (int z = 0; z < n; z++)
            {
                float target = _game.Dust(z);
                // The dust thins out softly pass after pass, never in a jump.
                _dustShown[z] = Mathf.MoveTowards(_dustShown[z], target, dt * 3.2f);
                float glint = 0f;
                if (_glintTime[z] > 0f)
                {
                    _glintTime[z] = Mathf.Max(0f, _glintTime[z] - dt / GlintPulseSeconds);
                    float t = 1f - _glintTime[z];
                    glint = t < 0.18f ? t / 0.18f : 1f - (t - 0.18f) / 0.82f;
                }
                _animal.SetZone(z, _dustShown[z], glint);
            }
        }

        private void UpdateProps(float dt)
        {
            var camera = _data.camera;
            float scale = _animal.Scale;
            var cameraUp = camera != null ? camera.transform.up : Vector3.up;
            int count = Mathf.Min(_game.DebrisCount, GroomingLayout.MaxDebris);
            for (int i = 0; i < GroomingLayout.MaxDebris; i++)
            {
                var state = _debrisState[i];
                if (state == PropHidden)
                    continue;
                var target = _debrisTransforms[i];
                if (state == PropShown && i < count)
                {
                    int zone = _game.DebrisZone(i);
                    if ((uint)zone >= (uint)_animal.ZoneCount)
                        continue;
                    _debrisT[i] += dt;
                    var normal = _animal.Normals[zone];
                    var rotation = Quaternion.LookRotation(Tangent(normal, cameraUp), normal) * Quaternion.AngleAxis(_debrisSpin[i], Vector3.up);
                    float appear = Appear(_debrisT[i]);
                    if (_debrisWobble[i] > 0f)
                    {
                        // A loosened burr wobbles on its spot (it never sinks: the wobble turns it around its contact point).
                        _debrisWobble[i] = Mathf.Max(0f, _debrisWobble[i] - dt / WobbleSeconds);
                        float w = _debrisWobble[i];
                        rotation = rotation * Quaternion.Euler(Mathf.Sin(w * 40f) * 14f * w, 0f, Mathf.Cos(w * 33f) * 10f * w);
                        appear *= 1f + 0.12f * w * Mathf.Sin(w * 50f);
                    }
                    var position = _animal.Points[zone] + normal * (_debrisLift[i] * scale);
                    target.SetPositionAndRotation(position, rotation);
                    float s = scale * Mathf.Max(0f, appear);
                    target.localScale = new Vector3(s, s, s);
                    if (camera != null)
                        _debrisScreen[i] = ToScreen(camera, position);
                }
                else
                {
                    // Flying off, spinning, under a soft gravity; it shrinks away at the end of its arc.
                    _debrisT[i] += dt;
                    float t = _debrisT[i];
                    if (t >= FlySeconds || target == null)
                    {
                        _debrisState[i] = PropHidden;
                        if (target != null)
                            target.gameObject.SetActive(false);
                        continue;
                    }
                    var position = _debrisFrom[i] + _debrisVelocity[i] * t + Vector3.up * (0.5f * FlyGravity * t * t * Mathf.Sqrt(scale));
                    var rotation = Quaternion.AngleAxis(760f * t, _debrisAxis[i]) * _debrisRotation[i];
                    target.SetPositionAndRotation(position, rotation);
                    float s = scale * (1f - Smooth(0.5f * FlySeconds, FlySeconds, t)) * (1f + 0.15f * Mathf.Sin(Mathf.Min(1f, t / 0.12f) * Mathf.PI));
                    target.localScale = new Vector3(s, s, s);
                }
            }

            UpdateBubbles(dt, camera, cameraUp, scale);
            UpdateGolden(dt, camera, cameraUp, scale);
            UpdateBow(dt);
            UpdateFoam(dt, cameraUp, scale);
            FollowGesture();
        }

        private void UpdateBubbles(float dt, Camera camera, Vector3 cameraUp, float scale)
        {
            if (_data.bubbles == null)
                return;
            float meshRadius = Mathf.Max(0.0001f, _data.bubbleRadius);
            for (int b = 0; b < _bubbleState.Length && b < _data.bubbles.Length; b++)
            {
                if (_bubbleState[b] == PropHidden || _data.bubbles[b] == null)
                    continue;
                var target = _data.bubbles[b].transform;
                int zone = _bubbleZone[b];
                if ((uint)zone >= (uint)_animal.ZoneCount)
                    continue;
                float radius = BubbleWorldRadius * scale;
                float size;
                if (_bubbleState[b] == PropShown)
                {
                    _bubbleT[b] += dt;
                    float wobble = 1f + 0.035f * Mathf.Sin(_bubbleT[b] * 5.1f);
                    size = Appear(_bubbleT[b]) * wobble;
                }
                else if (_bubbleT[b] >= 0f)
                {
                    // Tapped: a quick swell, then it is gone (the puff and the foam take over).
                    _bubbleT[b] += dt;
                    size = 1f + 0.3f * (_bubbleT[b] / 0.09f);
                    if (_bubbleT[b] >= 0.09f)
                    {
                        _bubbleState[b] = PropHidden;
                        target.gameObject.SetActive(false);
                        continue;
                    }
                }
                else
                {
                    // Gone without a tap: a soft shrink over a quarter of a second.
                    _bubbleT[b] -= dt;
                    size = Mathf.Max(0f, 1f + (_bubbleT[b] + 1f) / 0.25f);
                    if (size <= 0f)
                    {
                        _bubbleState[b] = PropHidden;
                        target.gameObject.SetActive(false);
                        continue;
                    }
                }
                var normal = _animal.Normals[zone];
                float bob = 0.002f * Mathf.Sin(_time * 2.3f + b) * scale;
                // It rests on the fur: its center one (swollen) radius above the zone, plus the contact gap.
                var center = _animal.Points[zone] + normal * (radius * Mathf.Max(1f, size) + ContactGap + Mathf.Max(0f, bob));
                float s = radius / meshRadius * size;
                target.SetPositionAndRotation(center, Quaternion.LookRotation(Tangent(normal, cameraUp), normal));
                target.localScale = new Vector3(s, s * (1f + 0.04f * Mathf.Sin(_time * 6.2f)), s);
                if (camera != null)
                {
                    _bubbleScreen[b] = ToScreen(camera, center);
                    float depth = Mathf.Max(0.1f, Vector3.Dot(center - camera.transform.position, camera.transform.forward));
                    _bubbleScreenRadius[b] = radius * camera.pixelHeight / (2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
                }
            }
        }

        private void UpdateGolden(float dt, Camera camera, Vector3 cameraUp, float scale)
        {
            if (!_goldenShown || _data.goldenBrush == null)
                return;
            var target = _data.goldenBrush.transform;
            int zone = _goldenZone;
            if ((uint)zone >= (uint)_animal.ZoneCount)
                return;
            _goldenT += dt;
            float size;
            if (_goldenLeaving)
            {
                size = 1f - _goldenT / 0.18f;
                if (size <= 0f)
                {
                    _goldenShown = false;
                    _goldenLeaving = false;
                    target.gameObject.SetActive(false);
                    return;
                }
            }
            else
            {
                size = Appear(_goldenT) * (1f + 0.05f * Mathf.Sin(_goldenT * 4f));
            }
            var normal = _animal.Normals[zone];
            // It waits just above the fur with a slow bob and turn: a gift to take, never lying in the coat.
            float hover = (_data.brushHover + 0.012f + 0.004f * (1f + Mathf.Sin(_goldenT * 3f))) * scale;
            var position = _animal.Points[zone] + normal * hover;
            var rotation = Quaternion.LookRotation(Tangent(normal, cameraUp), normal) * Quaternion.AngleAxis(_goldenT * 35f, Vector3.up);
            target.SetPositionAndRotation(position, rotation);
            float s = scale * size;
            target.localScale = new Vector3(s, s, s);
            if (camera != null)
            {
                _goldenScreen = ToScreen(camera, position);
                float depth = Mathf.Max(0.1f, Vector3.Dot(position - camera.transform.position, camera.transform.forward));
                _goldenScreenRadius = 0.06f * scale * camera.pixelHeight / (2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            }
        }

        private void UpdateBow(float dt)
        {
            if (_bowState != 1 || _data.bow == null || _animal.HeadTop == null)
                return;
            var bow = _data.bow.transform;
            var head = _animal.HeadTop;
            float lift = _animal.Body.bowLift;
            float bowScale = _animal.Body.bowScale;
            _bowT += dt / BowFlightSeconds;
            float t = Mathf.Min(1f, _bowT);
            var end = head.position + head.up * (lift * _animal.Scale);
            // An arc over the animal: up from the picker, over, and down onto the head.
            var middle = (_bowFrom + end) * 0.5f + Vector3.up * (0.12f * _animal.Scale);
            float u = 1f - (1f - t) * (1f - t);
            var position = (1f - u) * (1f - u) * _bowFrom + 2f * (1f - u) * u * middle + u * u * end;
            var rotation = head.rotation * Quaternion.AngleAxis((1f - u) * 360f, Vector3.up);
            float s = bowScale * _animal.Scale * Mathf.Lerp(0.6f, 1f, u);
            bow.SetPositionAndRotation(position, rotation);
            bow.localScale = new Vector3(s, s, s);
            if (t < 1f)
                return;
            // Landed: it rides the head from now on (parented to SOCK_HeadTop, measured lift and size: on the head, never in it).
            bow.SetParent(head, false);
            bow.localPosition = new Vector3(0f, lift, 0f);
            bow.localRotation = Quaternion.identity;
            bow.localScale = new Vector3(bowScale, bowScale, bowScale);
            _bowState = 2;
            PlayHappy(true);
        }

        private void UpdateFoam(float dt, Vector3 cameraUp, float scale)
        {
            if (_data.foam == null)
                return;
            float meshRadius = Mathf.Max(0.0001f, _data.bubbleRadius);
            int n = Mathf.Min(_data.foam.Length, GroomingLayout.FoamCount);
            for (int i = 0; i < n; i++)
            {
                int zone = _foamZone[i];
                if (zone < 0 || _data.foam[i] == null)
                    continue;
                _foamT[i] += dt;
                float t = _foamT[i];
                var target = _data.foam[i].transform;
                if (t >= FoamSeconds || (uint)zone >= (uint)_animal.ZoneCount)
                {
                    _foamZone[i] = -1;
                    target.gameObject.SetActive(false);
                    continue;
                }
                float k = Mathf.Max(0f, t) / FoamSeconds;
                float size = t < 0f ? 0f : k < 0.2f ? k / 0.2f : k > 0.75f ? 1f - (k - 0.75f) / 0.25f * 0.9f : 1f;
                var normal = _animal.Normals[zone];
                var tangent = Tangent(normal, cameraUp);
                var bitangent = Vector3.Cross(normal, tangent);
                var offset = _foamOffset[i];
                float radius = _foamSize[i] * scale;
                var position = _animal.Points[zone] + (tangent * offset.x + bitangent * offset.y) * scale
                               + normal * (radius + ContactGap + offset.z * k * scale);
                float s = radius / meshRadius * size;
                target.position = position;
                target.localScale = new Vector3(s, s, s);
            }
        }

        /// <summary>
        /// The brush follows the finger just above the fur (the surface under the finger from the nearest zones, the
        /// bristles' height above it), its back toward the camera, tilting into the stroke.
        /// </summary>
        private void UpdateBrush(float dt)
        {
            var brush = _data.brush;
            if (brush == null || _data.camera == null)
                return;
            bool show = _stroke.Down && !_ended && !_paused && _game.Phase != GroomingPhase.Done && (_moved || Time.unscaledTime - _pressTime > 0.12f);
            _brushShow = Mathf.MoveTowards(_brushShow, show ? 1f : 0f, dt / (show ? 0.12f : 0.18f));
            if (_brushShow <= 0f)
            {
                if (brush.gameObject.activeSelf)
                    brush.gameObject.SetActive(false);
                return;
            }
            var camera = _data.camera;
            float scale = _animal.Scale;
            var finger = _brushTarget;
            Vector3 surface, normal;
            if (!SurfaceUnder(finger, out surface, out normal))
            {
                surface = camera.ScreenToWorldPoint(new Vector3(finger.x, finger.y, _frameDistance * 0.92f));
                normal = -camera.transform.forward;
            }
            var goal = surface + normal * ((_data.brushHover + 0.003f) * scale);
            bool appearing = !brush.gameObject.activeSelf;
            if (appearing)
            {
                _brushPosition = goal;
                _brushVelocity = Vector3.zero;
                brush.gameObject.SetActive(true);
            }
            else
            {
                _brushPosition = Vector3.SmoothDamp(_brushPosition, goal, ref _brushVelocity, 0.035f, Mathf.Infinity, Mathf.Max(dt, 0.0001f));
            }
            var delta = _stroke.LastDelta;
            var world = camera.transform.right * delta.x + camera.transform.up * delta.y;
            var along = world - normal * Vector3.Dot(world, normal);
            if (along.sqrMagnitude > 1e-6f)
                _brushForward = Vector3.Slerp(_brushForward, along.normalized, Mathf.Min(1f, dt * 14f));
            var forward = _brushForward - normal * Vector3.Dot(_brushForward, normal);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Tangent(normal, camera.transform.up);
            float speed = Mathf.Clamp01(delta.magnitude / Mathf.Max(1f, 0.03f * camera.pixelHeight));
            // The leading edge lifts a little with speed (bristles drag behind): a few degrees, never into the fur.
            var rotation = Quaternion.LookRotation(forward.normalized, normal) * Quaternion.AngleAxis(-12f * speed, Vector3.right);
            brush.transform.SetPositionAndRotation(_brushPosition, rotation);
            float s = scale * Mathf.Sin(_brushShow * Mathf.PI * 0.5f);
            brush.transform.localScale = new Vector3(s, s, s);
        }

        /// <summary>The coat under a screen point: the nearest zones' points and normals, weighted by their distance on screen.</summary>
        private bool SurfaceUnder(Vector2 screen, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            float bestRatio = float.MaxValue;
            var sumPoint = Vector3.zero;
            var sumNormal = Vector3.zero;
            float sumWeight = 0f;
            for (int z = 0; z < _animal.ZoneCount; z++)
            {
                if (!_animal.Hittable[z])
                    continue;
                float r = _animal.ScreenRadii[z];
                float d = (_animal.Screen[z] - screen).magnitude;
                bestRatio = Mathf.Min(bestRatio, d / Mathf.Max(1f, r));
                if (d > 2.2f * r)
                    continue;
                float w = 1f / (0.1f * r * r + d * d);
                sumPoint += _animal.Points[z] * w;
                sumNormal += _animal.Normals[z] * w;
                sumWeight += w;
            }
            if (sumWeight <= 0f || bestRatio > 1.6f)
                return false;
            point = sumPoint / sumWeight;
            normal = sumNormal.sqrMagnitude > 1e-8f ? sumNormal.normalized : Vector3.up;
            // Between zones the blended point may sit under a convex coat: lift it to the highest of the near zones' planes.
            float lift = 0f;
            for (int z = 0; z < _animal.ZoneCount; z++)
            {
                if (!_animal.Hittable[z] || (_animal.Screen[z] - screen).magnitude > 2.2f * _animal.ScreenRadii[z])
                    continue;
                lift = Mathf.Max(lift, Vector3.Dot(_animal.Points[z] - point, normal));
            }
            point += normal * lift;
            return true;
        }

        private static Vector3 Tangent(Vector3 normal, Vector3 up)
        {
            var t = Vector3.Cross(up, normal);
            if (t.sqrMagnitude < 1e-6f)
                t = Vector3.Cross(Vector3.forward, normal);
            return t.normalized;
        }

        private static Vector2 ToScreen(Camera camera, Vector3 world)
        {
            var p = camera.WorldToScreenPoint(world);
            return new Vector2(p.x, p.y);
        }

        /// <summary>0 to 1 with a soft overshoot (a pop), over <see cref="AppearSeconds"/>; negative time waits.</summary>
        private static float Appear(float t)
        {
            if (t <= 0f)
                return 0f;
            // Ease out with a small overshoot (about 10 %), back to 1 at the end.
            const float c1 = 1.3f;
            const float c3 = c1 + 1f;
            float k = Mathf.Min(1f, t / AppearSeconds) - 1f;
            return 1f + c3 * k * k * k + c1 * k * k;
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
