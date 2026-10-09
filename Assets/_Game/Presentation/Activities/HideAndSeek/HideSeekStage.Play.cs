using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PocketWilds.Presentation
{
    // The game as it plays: taps on the decor, the game's events, the items, the night, the tutorial (doc 01 Contenu
    // S-ACT-002; doc « Mini-jeux & activités » section 1: understood without text in 3 s).
    public sealed partial class HideSeekStage
    {
        /// <summary>The tips (agent HU's texts): the first game's find, its first hint, its items; a kind word while touches rest.</summary>
        public const string TipFind = ActivityText.HideSeekTipFind;

        public const string TipHint = ActivityText.HideSeekTipHint;
        public const string TipItems = ActivityText.HideSeekTipItems;
        public const string TipPaused = ActivityText.HideSeekTipPaused;

        /// <summary>The HUD icons of the items (agent HI), borrowed from the HUD: magnifier, whistle.</summary>
        private static readonly string[] ItemIconNames = { ActivityText.IconMagnifier, ActivityText.IconWhistle };

        public const int MagnifierItem = 0;
        public const int WhistleItem = 1;

        private readonly ActivityPickerItem[] _items = new ActivityPickerItem[2];

        private float _time;
        private float _dt;

        // The finger.
        private bool _pressing;
        private bool _ignorePress;
        private bool _moved;
        private int _touchId = -1;
        private Vector2 _pressPosition;
        private float _pressTime;
        private bool _fingerDown;
        private bool _fingerSeen;
        private Vector2 _finger;
        private bool _hasTap;
        private Vector2 _lastTap;

        // The last find (its tap and when): a happy double tap on it never counts as an empty touch.
        private Vector2 _foundTap;
        private float _foundTapUntil = -1f;

        // HUD parts shown on the first tick (the presenter shows the HUD, fresh, after Enter).
        private bool _hudPending;
        private bool _itemsShown;

        // Tutorial (first game of the activity): 1 waits 2 s, 2 the paw on an animal, 3 done; 0 none.
        private bool _tutorial;
        private int _tutorialStep;
        private int _gestureAnimal = -1;
        private Vector2 _gestureAt;
        private bool _hintTipShown;
        private bool _itemsTipShown;

        // Night (challenge 7): the HUD's spotlight over a moonlit refuge.
        private bool _night;
        private bool _nightWasOn;
        private WorldAtmosphere _atmosphere;
        private bool _dimmed;
        private Light _dimSun;
        private float _sunBase, _sunDim;
        private Color _skyBase, _equatorBase, _groundBase, _skyDim, _equatorDim, _groundDim;
        private Vector2 _spotlightAt = new Vector2(-1f, -1f);

        // Birds and insects keep off the spots and the landing points while the game shows (collision rule).
        private Obstacle[] _lifeObstacles = System.Array.Empty<Obstacle>();
        private bool _lifeSet;

        /// <summary>Captures without a refuge scene running: the lighting moods the night challenge switches (else the scene's).</summary>
        public WorldAtmosphere Atmosphere { get; set; }

        /// <summary>The stage reads the touchscreen and the mouse itself (tests turn it off and call <see cref="TapAt"/>).</summary>
        public bool PollInput { get; set; } = true;

        /// <summary>The tutorial step (0 none, 1 waiting, 2 the paw on an animal, 3 done).</summary>
        public int TutorialStep => _tutorialStep;

        public bool ItemsShown => _itemsShown;

        public bool NightOn => _night;

        /// <summary>A finger rests on the meadow (the night's light stays where it last touched once it lifts).</summary>
        public bool FingerDown => _fingerDown;

        // ---------------- a game starts ----------------

        private void BeginVisuals()
        {
            _time = 0f;
            _dt = 0f;
            HideVisuals();
            PlaceAnimals();
            _hudPending = true;
            _itemsShown = false;
            _hasTap = false;
            _fingerSeen = false;
            _foundTapUntil = -1f;
            _spotlightAt = new Vector2(-1f, -1f);
            _tutorial = _activeHost != null && _activeHost.Tutorial;
            _tutorialStep = _tutorial ? 1 : 0;
            _gestureAnimal = -1;
            _hintTipShown = false;
            _itemsTipShown = false;
            EnterNight();
            SetLifeObstacles();
        }

        // ---------------- per frame ----------------

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!_entered || _set == null || _failed)
                return;
            _dt = _paused ? 0f : Mathf.Min(deltaTime, 0.1f);
            _time += _dt;
            if (_chunk == null)
                LateBindChunk();
            if (_hudPending && !_paused)
                ShowHudParts();
            if (!_paused && !_ended && PollInput)
                PollPointer();
            DrainEvents();
            if (_paused)
                return;
            UpdateAnimals(_dt);
            UpdateGusts();
            UpdateRings(_dt);
            UpdateLeaves(_dt);
            UpdateNight();
            if (!_ended)
                UpdateTutorial();
        }

        /// <summary>The items, once the HUD shows (the presenter shows it fresh after Enter).</summary>
        private void ShowHudParts()
        {
            _hudPending = false;
            var hud = Hud;
            if (hud == null)
                return;
            if (_game.ItemsEnabled)
            {
                for (int i = 0; i < _items.Length; i++)
                {
                    var icon = hud.Icon(ItemIconNames[i]);
                    // Without its icon a soft round button of the item's color (glass blue, warm wood).
                    _items[i] = new ActivityPickerItem { Icon = icon, Tint = icon != null ? Color.white : i == 0 ? new Color(0.62f, 0.8f, 0.94f) : new Color(0.87f, 0.72f, 0.52f) };
                }
                hud.ShowItems(_items, _items.Length, this);
                hud.SetItemUsed(MagnifierItem, !_game.MagnifierAvailable);
                hud.SetItemUsed(WhistleItem, !_game.WhistleAvailable);
                _itemsShown = true;
            }
        }

        private IActivityHudView Hud => _activeHost != null ? _activeHost.Hud : null;

        // ---------------- the finger ----------------

        private void PollPointer()
        {
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
                    _touchId = id;
                    Pointer(true, touch.position.ReadValue(), id);
                    return;
                }
                if (_touchId >= 0)
                {
                    Pointer(false, _finger, _touchId);
                    _touchId = -1;
                    return;
                }
            }
            var mouse = Mouse.current;
            if (mouse != null)
                Pointer(mouse.leftButton.isPressed, mouse.position.ReadValue(), -1);
        }

        private void Pointer(bool pressed, Vector2 position, int pointerId)
        {
            _fingerDown = pressed;
            if (pressed)
            {
                _finger = position;
                _fingerSeen = true;
            }
            if (pressed && !_pressing)
            {
                // A touch on the HUD (pause, the items) is the HUD's.
                _ignorePress = OverHud(pointerId);
                _pressing = true;
                _moved = false;
                _pressPosition = position;
                _pressTime = Time.unscaledTime;
                return;
            }
            if (pressed)
            {
                if (!_moved && (position - _pressPosition).sqrMagnitude > HideSeekLayout.TapPixels * HideSeekLayout.TapPixels)
                    _moved = true;
                // The UI module may read a new touch after this stage on its first frame: asked again while the finger rests.
                if (!_ignorePress && !_moved && OverHud(pointerId))
                    _ignorePress = true;
                return;
            }
            if (!_pressing)
                return;
            _pressing = false;
            // A quick tap on an item button can begin and end between two UI updates: asked once more as it lifts.
            bool tap = !_ignorePress && !_moved && Time.unscaledTime - _pressTime <= HideSeekLayout.TapSeconds && !OverHud(pointerId);
            _ignorePress = false;
            if (tap)
                TapAt(_pressPosition);
        }

        /// <summary>The pointer is over the HUD (its score, pause and item buttons): its touches never reach the meadow.</summary>
        private static bool OverHud(int pointerId)
        {
            var events = EventSystem.current;
            return events != null && events.IsPointerOverGameObject(pointerId);
        }

        /// <summary>Tests and captures: where the finger is (the night's spotlight follows it), pressed or not.</summary>
        public void PointerAt(Vector2 screen, bool down)
        {
            _fingerDown = down;
            if (down)
            {
                _finger = screen;
                _fingerSeen = true;
            }
        }

        private void CancelPress()
        {
            _pressing = false;
            _ignorePress = false;
            _fingerDown = false;
            _touchId = -1;
        }

        /// <summary>
        /// A tap at a screen point (screen pixels, origin bottom left; tests call it directly): the spot it touches
        /// (<see cref="HitSpot"/>); bare ground does nothing; while touches rest, a soft cue only; a happy double tap on the
        /// animal just found is ignored (never an empty touch on the decor next to it).
        /// </summary>
        public void TapAt(Vector2 screen)
        {
            if (!_entered || _ended || _paused || _set == null || _failed)
                return;
            _hasTap = true;
            _lastTap = screen;
            _finger = screen;
            _fingerSeen = true;
            if (_game.TouchesPaused)
            {
                Cue(screen);
                return;
            }
            var camera = Camera;
            if (_time < _foundTapUntil && camera != null)
            {
                float guard = HideSeekLayout.FoundTapGuardShare * camera.pixelHeight;
                if ((screen - _foundTap).sqrMagnitude <= guard * guard)
                    return;
            }
            int spot = HitSpot(screen);
            if (spot < 0)
                return;
            int found = _game.FoundCount;
            _game.TapSpot(spot);
            if (_game.FoundCount > found)
            {
                _foundTap = screen;
                _foundTapUntil = _time + HideSeekLayout.FoundTapGuardSeconds;
            }
            DrainEvents();
        }

        /// <summary>
        /// The spot under a screen point: a hidden animal's own body first (its ear over a neighbour's decor still finds it;
        /// one slipping away in challenge 9 is found at its new spot), else the decor's (<see cref="HitDecor"/>). −1 for bare
        /// ground.
        /// </summary>
        public int HitSpot(Vector2 screen)
        {
            var camera = Camera;
            if (camera == null)
                return -1;
            int animal = AnimalUnder(camera, screen);
            if (animal >= 0)
                return _phase[animal] == Moving ? _nextSpot[animal] : _spot[animal];
            return HitDecor(screen);
        }

        /// <summary>
        /// The spot whose decor a screen point touches: each decor projected to the screen as a rectangle (its radius across,
        /// its height up, the hidden animal's own body added, a finger's width at least); among those holding the point, the
        /// one whose decor's middle is nearest (a decor's own place, never shifted by its animal). −1 for bare ground.
        /// </summary>
        public int HitDecor(Vector2 screen)
        {
            var camera = Camera;
            if (camera == null)
                return -1;
            float minHalf = HideSeekLayout.MinTargetShare * camera.pixelHeight;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int k = 0; k < _spotCount; k++)
            {
                if (!SpotRect(camera, k, minHalf, out var center, out var half, out var anchor))
                    continue;
                var d = screen - center;
                if (Mathf.Abs(d.x) > half.x || Mathf.Abs(d.y) > half.y)
                    continue;
                float distance = (screen - anchor).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = k;
                }
            }
            return best;
        }

        /// <summary>
        /// The hidden animal (or one slipping to another spot) whose body holds a screen point: its body's box on the screen
        /// (its capsule from the ground to its height, a finger's margin); the nearest box center wins. −1: none.
        /// </summary>
        public int AnimalUnder(Camera camera, Vector2 screen)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            float margin = HideSeekLayout.AnimalTapMarginShare * camera.pixelHeight;
            for (int i = 0; i < _hiddenCount; i++)
            {
                if ((_phase[i] != Hidden && _phase[i] != Moving) || _view[i] == null || (uint)_spot[i] >= (uint)_spotCount)
                    continue;
                if (!AnimalBox(camera, i, out var center, out var half))
                    continue;
                var d = screen - center;
                if (Mathf.Abs(d.x) > half.x + margin || Mathf.Abs(d.y) > half.y + margin)
                    continue;
                float distance = d.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>An animal's body on the screen (center and half size, pixels): its capsule's ends at the ground and at its height.</summary>
        private bool AnimalBox(Camera camera, int animal, out Vector2 center, out Vector2 half)
        {
            center = half = default;
            var body = AnimalBody(animal);
            float scale = _scale[animal];
            var p = _position[animal];
            body.Segment(p, _yaw[animal], scale, out var a0, out var a1);
            float top = p.y + body.height * scale;
            var s0 = camera.WorldToScreenPoint(new Vector3(a0.x, p.y, a0.y));
            var s1 = camera.WorldToScreenPoint(new Vector3(a1.x, p.y, a1.y));
            var s2 = camera.WorldToScreenPoint(new Vector3(a0.x, top, a0.y));
            var s3 = camera.WorldToScreenPoint(new Vector3(a1.x, top, a1.y));
            if (s0.z <= 0.05f || s1.z <= 0.05f || s2.z <= 0.05f || s3.z <= 0.05f)
                return false;
            float r = body.halfWidth * scale * PixelsPerMeter(camera, 0.25f * (s0.z + s1.z + s2.z + s3.z));
            float minX = Mathf.Min(Mathf.Min(s0.x, s1.x), Mathf.Min(s2.x, s3.x)) - r;
            float maxX = Mathf.Max(Mathf.Max(s0.x, s1.x), Mathf.Max(s2.x, s3.x)) + r;
            float minY = Mathf.Min(Mathf.Min(s0.y, s1.y), Mathf.Min(s2.y, s3.y)) - r;
            float maxY = Mathf.Max(Mathf.Max(s0.y, s1.y), Mathf.Max(s2.y, s3.y)) + r;
            center = new Vector2(0.5f * (minX + maxX), 0.5f * (minY + maxY));
            half = new Vector2(0.5f * (maxX - minX), 0.5f * (maxY - minY));
            return true;
        }

        private static float PixelsPerMeter(Camera camera, float depth) =>
            camera.pixelHeight / (2f * Mathf.Max(0.1f, depth) * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));

        /// <summary>A spot's tap rectangle on the screen (center and half size, pixels). False when behind the camera.</summary>
        public bool SpotRect(Camera camera, int spot, float minHalf, out Vector2 center, out Vector2 half) =>
            SpotRect(camera, spot, minHalf, out center, out half, out _);

        /// <summary>The same, and the middle of the decor alone (<paramref name="anchor"/>).</summary>
        private bool SpotRect(Camera camera, int spot, float minHalf, out Vector2 center, out Vector2 half, out Vector2 anchor)
        {
            center = half = anchor = default;
            var s = _set.GetSpot(_firstSpot + spot);
            var bottom = _origin + s.decorCenter;
            var top = bottom + Vector3.up * Mathf.Max(0.15f, s.decorHeight);
            var b = camera.WorldToScreenPoint(bottom);
            var t = camera.WorldToScreenPoint(top);
            if (b.z <= 0.05f || t.z <= 0.05f)
                return false;
            float pixelsPerMeter = PixelsPerMeter(camera, 0.5f * (b.z + t.z));
            float minX = Mathf.Min(b.x, t.x) - s.decorRadius * pixelsPerMeter;
            float maxX = Mathf.Max(b.x, t.x) + s.decorRadius * pixelsPerMeter;
            float minY = Mathf.Min(b.y, t.y) - 0.35f * s.decorRadius * pixelsPerMeter;
            float maxY = Mathf.Max(b.y, t.y);
            anchor = new Vector2(0.5f * (minX + maxX), 0.5f * (minY + maxY));
            // The animal hidden there counts too (an ear above the grass, a tail beside it).
            for (int i = 0; i < _hiddenCount; i++)
            {
                if (_spot[i] != spot || (_phase[i] != Hidden && _phase[i] != Moving) || _view[i] == null)
                    continue;
                var root = camera.WorldToScreenPoint(_position[i]);
                var head = camera.WorldToScreenPoint(_position[i] + Vector3.up * (AnimalBody(i).height * _scale[i]));
                if (root.z <= 0.05f || head.z <= 0.05f)
                    continue;
                float r = AnimalBody(i).Reach * _scale[i] * pixelsPerMeter;
                minX = Mathf.Min(minX, Mathf.Min(root.x, head.x) - r);
                maxX = Mathf.Max(maxX, Mathf.Max(root.x, head.x) + r);
                minY = Mathf.Min(minY, Mathf.Min(root.y, head.y));
                maxY = Mathf.Max(maxY, Mathf.Max(root.y, head.y));
            }
            center = new Vector2(0.5f * (minX + maxX), 0.5f * (minY + maxY));
            half = new Vector2(Mathf.Max(minHalf, 0.5f * (maxX - minX)), Mathf.Max(minHalf, 0.5f * (maxY - minY)));
            return true;
        }

        /// <summary>The screen point at the middle of a spot's decor (tests tap it).</summary>
        public Vector2 SpotScreen(int spot)
        {
            var camera = Camera;
            if (camera == null || (uint)spot >= (uint)_spotCount)
                return default;
            var s = _set.GetSpot(_firstSpot + spot);
            var p = camera.WorldToScreenPoint(_origin + s.decorCenter + Vector3.up * (0.45f * s.decorHeight));
            return new Vector2(p.x, p.y);
        }

        /// <summary>A soft pale ring where the finger touched (touches rest a moment): no sound, no point.</summary>
        private void Cue(Vector2 screen)
        {
            if (GroundPoint(screen, out var point))
                Ring(RingCue, point + Vector3.up * 0.02f, 0.3f);
        }

        /// <summary>Where a screen point meets the zone's ground (its height at the zone's spots).</summary>
        private bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            point = default;
            var camera = Camera;
            if (camera == null)
                return false;
            var ray = camera.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            float height = _spotCount > 0 ? _origin.y + _set.GetSpot(_firstSpot).decorCenter.y : _origin.y;
            if (ray.direction.y > -1e-4f)
                return false;
            float t = (height - ray.origin.y) / ray.direction.y;
            point = ray.origin + ray.direction * t;
            point.y = GroundY(point);
            return true;
        }

        // ---------------- items (doc 01: « Loupe », « Sifflet », one each per game) ----------------

        /// <summary>
        /// A HUD item button: 0 the magnifier (toward the last touch, else the middle of the screen), 1 the whistle. The HUD
        /// marks the button used at the tap; it is given back whenever the game did not take it (paused, ended, nothing hidden
        /// to ring).
        /// </summary>
        public void UseItem(int index)
        {
            if (_entered && !_ended && !_paused && !_failed && _set != null && _game.ItemsEnabled)
            {
                if (index == MagnifierItem && _game.MagnifierAvailable)
                {
                    var camera = Camera;
                    var screen = _hasTap ? _lastTap : camera != null ? new Vector2(camera.pixelWidth * 0.5f, camera.pixelHeight * 0.5f) : Vector2.zero;
                    var point = _spotCount > 0 ? _origin + _set.GetSpot(_firstSpot).position : _origin;
                    if (GroundPoint(screen, out var ground))
                        point = ground;
                    // The game's space is the set's (chunk space).
                    var local = point - _origin;
                    _game.UseMagnifier(local.x, local.y, local.z);
                }
                else if (index == WhistleItem && _game.WhistleAvailable)
                {
                    _game.UseWhistle();
                }
                DrainEvents();
            }
            var hud = Hud;
            if (hud != null && _itemsShown && (index == MagnifierItem || index == WhistleItem))
                hud.SetItemUsed(index, !(index == MagnifierItem ? _game.MagnifierAvailable : _game.WhistleAvailable));
        }

        // ---------------- the game's events ----------------

        private IAudioService Audio => _context != null ? _context.Audio : NullAudioService.Instance;

        private IHapticService Haptics => _context != null ? _context.Haptics : NullHapticService.Instance;

        /// <summary>Where the stage's sounds play: the refuge's listening point (full level, centred).</summary>
        private Vector3 AudioPoint => _host != null && _host.CameraRig != null ? _host.CameraRig.Focus : Vector3.zero;

        private void DrainEvents()
        {
            int count = _game.EventCount;
            if (count == 0)
                return;
            for (int i = 0; i < count; i++)
            {
                var e = _game.Event(i);
                OnEvent(in e);
            }
            _game.ClearEvents();
        }

        private void OnEvent(in HideSeekEvent e)
        {
            // J-SFX-ACT-002-01 to 06 (agent HA's HideSeekAudio): the « coucou » with the found animal's own voice.
            bool find = e.Kind == HideSeekEventKind.Found || e.Kind == HideSeekEventKind.FoundAfterHint;
            var species = find && _pickCount > 0 && (uint)e.Animal < (uint)_hiddenCount ? _picks[e.Animal % _pickCount].Species : ContentId.None;
            HideSeekAudio.Play(Audio, in e, species, AudioPoint);
            switch (e.Kind)
            {
                case HideSeekEventKind.Found:
                case HideSeekEventKind.FoundAfterHint:
                    Coucou(e.Animal);
                    break;
                case HideSeekEventKind.Empty:
                    Leaves(e.Spot);
                    Haptics.Play(HapticEventId.TapLight);
                    break;
                case HideSeekEventKind.TouchesPaused:
                    // A soft cue only (no sound, no point): a pale ring where the finger was and a kind word.
                    if (_hasTap)
                        Cue(_lastTap);
                    ShowTip(TipPaused, 1.4f);
                    break;
                case HideSeekEventKind.Hint:
                    if ((uint)e.Animal < (uint)_hiddenCount)
                    {
                        _hinting[e.Animal] = true;
                        _hintNext[e.Animal] = _time;
                    }
                    if (_tutorial && !_hintTipShown)
                    {
                        _hintTipShown = true;
                        ShowTip(TipHint, HideSeekLayout.TipSeconds);
                    }
                    break;
                case HideSeekEventKind.DecoyRustle:
                    // Wind only: the decoy ripples like a hint, silent (a real hint is heard).
                    Gust(e.Spot, HideSeekLayout.DecoyRippleSeconds, 0.85f);
                    break;
                case HideSeekEventKind.Magnifier:
                    Haptics.Play(HapticEventId.TapLight);
                    break;
                case HideSeekEventKind.Whistle:
                    int k = 0;
                    for (int i = 0; i < _hiddenCount; i++)
                        if (_phase[i] == Hidden || _phase[i] == Moving)
                            _wiggleAt[i] = _time + HideSeekLayout.WhistleStagger * k++;
                    break;
                case HideSeekEventKind.Moved:
                    StartMove(e.Animal, e.Spot);
                    break;
                case HideSeekEventKind.Mistake:
                    Haptics.Play(HapticEventId.TapLight);
                    break;
                case HideSeekEventKind.AllFound:
                    Haptics.Play(HapticEventId.Confirm);
                    _gatherAt = _time + HideSeekLayout.HopSeconds + HideSeekLayout.GatherDelay;
                    break;
                case HideSeekEventKind.Finished:
                    break;
            }
        }

        /// <summary>« Coucou » (its pop and voice played with the event): a heart above it, a ring where it lands, the hop.</summary>
        private void Coucou(int animal)
        {
            if ((uint)animal >= (uint)_hiddenCount)
                return;
            StartCoucou(animal, 0f);
            Haptics.Play(HapticEventId.Confirm);
            Heart(animal);
            if (_view[animal] != null)
                Ring(RingDiscovery, _landing[animal] + Vector3.up * 0.02f, 2.4f * AnimalBody(animal).Reach * _scale[animal] + 0.2f);
            if (_tutorialStep == 1 || _tutorialStep == 2)
                TutorialFound();
        }

        // ---------------- tutorial (doc Mini-jeux section 1; doc 01 A18) ----------------

        private void ShowTip(string key, float seconds)
        {
            var hud = Hud;
            var text = _activeHost != null ? _activeHost.Text : null;
            if (hud == null || text == null)
                return;
            hud.ShowTip(text.Get(key), seconds);
        }

        private void UpdateTutorial()
        {
            if (_tutorialStep == 1 && _time >= HideSeekLayout.TutorialDelay)
            {
                _tutorialStep = 2;
                ShowTip(TipFind, HideSeekLayout.TipSeconds + 0.6f);
                _gestureAnimal = -1;
            }
            if (_tutorialStep != 2)
                return;
            // The paw taps the most visible animal still hidden; it follows a move.
            if (_gestureAnimal < 0 || !IsHidden(_gestureAnimal))
                _gestureAnimal = MostVisible();
            if (_gestureAnimal < 0)
                return;
            var point = AnimalScreen(_gestureAnimal);
            var hud = Hud;
            if (hud == null)
                return;
            if (!hud.GestureShown || (point - _gestureAt).sqrMagnitude > 24f * 24f)
            {
                _gestureAt = point;
                hud.ShowGesture(ActivityGesture.Tap, point, point);
            }
        }

        private void TutorialFound()
        {
            if (_tutorialStep == 0)
                return;
            _tutorialStep = 3;
            Hud?.HideGesture();
            if (_tutorial && _itemsShown && !_itemsTipShown)
            {
                _itemsTipShown = true;
                ShowTip(TipItems, HideSeekLayout.TipSeconds);
            }
        }

        /// <summary>The hidden animal with the largest measured visible share (else the highest on the screen).</summary>
        public int MostVisible()
        {
            int best = -1;
            float bestShare = float.MinValue;
            for (int i = 0; i < _hiddenCount; i++)
            {
                if (_phase[i] != Hidden || _view[i] == null)
                    continue;
                float share = ExpectedShare(i);
                if (share < 0f)
                    share = 0.001f * AnimalScreen(i).y;
                if (share > bestShare)
                {
                    bestShare = share;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The part of a hidden animal that peeks out (its head), on the screen.</summary>
        public Vector2 AnimalScreen(int animal)
        {
            var camera = Camera;
            var view = (uint)animal < (uint)_hiddenCount ? _view[animal] : null;
            if (camera == null || view == null)
                return default;
            var head = view.SocketHeadTop != null ? view.SocketHeadTop.position : _position[animal] + Vector3.up * (0.8f * AnimalBody(animal).height * _scale[animal]);
            var p = camera.WorldToScreenPoint(head);
            return new Vector2(p.x, p.y);
        }

        private void HideAids(bool items)
        {
            var hud = Hud;
            _hudPending = false;
            if (hud == null)
            {
                _itemsShown = false;
                return;
            }
            hud.HideTip();
            hud.HideGesture();
            hud.SetSpotlight(0f, default);
            if (items && _itemsShown)
            {
                hud.HideItems();
                _itemsShown = false;
            }
        }

        // ---------------- night (challenge 7 « Dans le noir ») ----------------

        /// <summary>
        /// The night challenge: the refuge's own night mood (stars, moonlight) while the game shows, else its light dimmed to
        /// a soft moonlit look; the HUD keeps a lit circle around the finger (an interface mask, no real-time light).
        /// </summary>
        private void EnterNight()
        {
            _night = _game.NightSpotlight > 0f;
            if (!_night)
                return;
            _atmosphere = Atmosphere != null ? Atmosphere : _host != null && _host.World != null ? _host.World.Atmosphere : null;
            _nightWasOn = _atmosphere != null && _atmosphere.Night;
            _atmosphere?.SetNight(true);
            _dimmed = _atmosphere == null || !_atmosphere.HasNight;
            _dimSun = null;
        }

        private void UpdateNight()
        {
            if (!_night)
                return;
            var hud = Hud;
            var camera = Camera;
            if (hud != null && camera != null && !_ended)
            {
                // Around the finger, and where it last touched once lifted (the « coucou » it found plays in the light); the
                // middle of the screen before the first touch.
                var center = _fingerSeen ? _finger : new Vector2(camera.pixelWidth * 0.5f, camera.pixelHeight * 0.5f);
                if ((center - _spotlightAt).sqrMagnitude > 1f)
                {
                    _spotlightAt = center;
                    hud.SetSpotlight(_game.NightSpotlight, center);
                }
            }
            if (_dimmed)
                KeepDim();
        }

        /// <summary>No night mood in this refuge: its sun and ambient at 45 %, cooler, kept so while the clock rewrites them.</summary>
        private void KeepDim()
        {
            var sun = RenderSettings.sun;
            if (sun != null && (sun != _dimSun || !Mathf.Approximately(sun.intensity, _sunDim)))
            {
                _dimSun = sun;
                _sunBase = sun.intensity;
                _sunDim = _sunBase * 0.45f;
                sun.intensity = _sunDim;
            }
            if (RenderSettings.ambientSkyColor != _skyDim)
            {
                _skyBase = RenderSettings.ambientSkyColor;
                _skyDim = Moonlit(_skyBase);
                RenderSettings.ambientSkyColor = _skyDim;
            }
            if (RenderSettings.ambientEquatorColor != _equatorDim)
            {
                _equatorBase = RenderSettings.ambientEquatorColor;
                _equatorDim = Moonlit(_equatorBase);
                RenderSettings.ambientEquatorColor = _equatorDim;
            }
            if (RenderSettings.ambientGroundColor != _groundDim)
            {
                _groundBase = RenderSettings.ambientGroundColor;
                _groundDim = Moonlit(_groundBase);
                RenderSettings.ambientGroundColor = _groundDim;
            }
        }

        private static Color Moonlit(Color c) => new Color(c.r * 0.42f, c.g * 0.46f, c.b * 0.6f, c.a);

        private void ExitNight()
        {
            if (!_night)
                return;
            _night = false;
            if (_atmosphere != null)
                _atmosphere.SetNight(_nightWasOn);
            _atmosphere = null;
            if (_dimmed)
            {
                if (_dimSun != null && Mathf.Approximately(_dimSun.intensity, _sunDim))
                    _dimSun.intensity = _sunBase;
                if (RenderSettings.ambientSkyColor == _skyDim)
                    RenderSettings.ambientSkyColor = _skyBase;
                if (RenderSettings.ambientEquatorColor == _equatorDim)
                    RenderSettings.ambientEquatorColor = _equatorBase;
                if (RenderSettings.ambientGroundColor == _groundDim)
                    RenderSettings.ambientGroundColor = _groundBase;
            }
            _dimmed = false;
            _dimSun = null;
            Hud?.SetSpotlight(0f, default);
        }

        // ---------------- ambient life (collision rule) ----------------

        /// <summary>
        /// For the game, birds land and insects pass nowhere in the spots, the landing points and the gather row (added to
        /// the hedge's volumes the ambient life already avoids), each disc wide and tall enough for the biggest animal of the
        /// game at its own size, hop included; given back at the end. Twice a game, never per frame.
        /// </summary>
        private void SetLifeObstacles()
        {
            var world = _host != null ? _host.World : null;
            var life = world != null ? world.Life : null;
            if (life == null || _set == null || _spotCount == 0)
                return;
            var boundary = world.Zones != null && world.Zones.Boundary != null ? world.Zones.Boundary.Obstacles : null;
            int extra = boundary != null ? boundary.Length : 0;
            int count = extra + 2 * _spotCount + 1;
            if (_lifeObstacles.Length != count)
                _lifeObstacles = new Obstacle[count];
            // Each disc holds the biggest animal of this game at its own size (any of them may hide or land there).
            // The gather row is as long as the animals side by side (StartGather).
            float reach = 0.3f, top = 0.7f, row = 0f;
            for (int i = 0; i < _hiddenCount; i++)
            {
                if (_view[i] == null)
                    continue;
                var body = AnimalBody(i);
                reach = Mathf.Max(reach, body.Reach * _scale[i] + 0.05f);
                top = Mathf.Max(top, body.height * _scale[i] + HideSeekLayout.HopHeight * Mathf.Max(0.6f, _scale[i]) + 0.05f);
                row += 2f * body.halfWidth * _scale[i] + 0.06f;
            }
            float rowHalf = Mathf.Max(1.3f, 0.5f * row);
            int n = 0;
            for (int i = 0; i < extra; i++)
                _lifeObstacles[n++] = boundary[i];
            for (int k = 0; k < _spotCount; k++)
            {
                var s = _set.GetSpot(_firstSpot + k);
                var p = CollisionMath.Flat(_origin + s.position);
                var l = CollisionMath.Flat(_origin + s.landing);
                float y = _origin.y + s.position.y;
                _lifeObstacles[n++] = new Obstacle(p, p, reach, y, 0f, top, ObstacleKind.Solid);
                _lifeObstacles[n++] = new Obstacle(l, l, reach, y, 0f, top, ObstacleKind.Solid);
            }
            var zone = _set.GetZone(_zone);
            var right = CollisionMath.Flat(Vector3.Cross(Vector3.up, CameraForward()));
            var g = CollisionMath.Flat(_origin + zone.gatherPoint);
            _lifeObstacles[n++] = new Obstacle(g - right * rowHalf, g + right * rowHalf, Mathf.Max(0.35f, reach), _origin.y + zone.gatherPoint.y, 0f, top,
                ObstacleKind.Solid);
            life.SetExtraObstacles(_lifeObstacles);
            _lifeSet = true;
        }

        private void RestoreLifeObstacles()
        {
            if (!_lifeSet)
                return;
            _lifeSet = false;
            var world = _host != null ? _host.World : null;
            var life = world != null ? world.Life : null;
            if (life == null)
                return;
            var boundary = world.Zones != null && world.Zones.Boundary != null ? world.Zones.Boundary.Obstacles : null;
            life.SetExtraObstacles(boundary);
        }
    }
}
