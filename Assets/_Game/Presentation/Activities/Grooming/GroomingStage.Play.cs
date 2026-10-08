using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PocketWilds.Presentation
{
    // The game as it plays: one finger, the game's events, the animal's reactions, the A15 tutorial, the bow picker.
    public sealed partial class GroomingStage
    {
        /// <summary>A touch shorter and stiller than this is a tap (a leaf, a burr, a bubble, the golden brush).</summary>
        public const float TapPixels = 18f;

        public const float TapSeconds = 0.35f;

        /// <summary>The finger counts as brushing for this long after its last pass (the content eyes, the lean).</summary>
        private const float BrushingSeconds = 0.7f;

        /// <summary>Happy hops spaced at least this much (a reward, never a fidget).</summary>
        private const float HappyCooldown = 2.2f;

        /// <summary>A1 5: the brush tip lasts 2 s; the others a little longer.</summary>
        private const float BrushTipSeconds = 2f;

        private const float TipSeconds = 2.5f;

        /// <summary>A burr tapped once and left alone this long gets its double-tap paw (once a game).</summary>
        private const float BurrHintDelay = 2.5f;

        private readonly GroomingStroke _stroke;
        private readonly int[] _passes = new int[16];
        private readonly ActivityPickerItem[] _pickerItems = new ActivityPickerItem[3];

        private float _time;
        private float _dt;
        private bool _pressing;
        private bool _ignorePress;
        private bool _moved;
        private int _touchId = -1;
        private Vector2 _pressPosition;
        private float _pressTime;
        private float _lastPassTime = -10f;
        private float _happyAt = -10f;
        private float _sighAt = -1f;
        private float _welcomeAt = -1f;
        private float _content;
        private bool _celebrated;
        private bool _pickerShown;

        // A15 tutorial: 1 brush (swipe), 2 leaf (tap), 3 burr (double tap), 4 done. 0: no tutorial this game.
        private int _tutorialStep;
        private int _tutorialTarget = -1;
        private bool _gesturePending;
        private Vector2 _gestureAt;
        private int _burrHint = -1;
        private float _burrHintAt = -1f;
        private bool _burrHintDone;

        /// <summary>The A15 brush tip waits for the first tick: the presenter shows the HUD (fresh, without tips) after Enter.</summary>
        private string _pendingTip;

        /// <summary>The stage reads the touchscreen and the mouse itself (tests turn it off and call the pointer methods).</summary>
        public bool PollInput { get; set; } = true;

        /// <summary>The tutorial step shown (0 none, 1 brush, 2 leaves, 3 burr, 4 done).</summary>
        public int TutorialStep => _tutorialStep;

        public bool PickerShown => _pickerShown;

        public float ContentEyes => _content;

        // ---------------- per frame ----------------

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!_entered || _set == null)
                return;
            _dt = _paused ? 0f : Mathf.Min(deltaTime, 0.1f);
            _time += _dt;
            if (_pendingTip != null)
            {
                ShowTip(_pendingTip, BrushTipSeconds);
                _pendingTip = null;
            }
            if (!_paused && !_ended && PollInput)
                PollPointer();
            DrainEvents();
            if (_paused)
                return;
            KeepLighting();
            SyncItems();
            UpdateReactions();
            if (!_ended)
                UpdateTutorial();
        }

        /// <summary>After the animation (GroomingSetView.LateUpdate): the animal's pose, its zones, the props, the dust.</summary>
        public void LateTick()
        {
            if (!_entered || _set == null || _animal == null)
                return;
            float dt = _dt;
            _dt = 0f;
            UpdateAnimalPose(dt);
            _animal.Update(_data.camera);
            UpdateDust(dt);
            UpdateProps(dt);
            UpdateBrush(dt);
            _animal.SetContent(_content);
            _animal.Upload();
            if (_gesturePending)
                ShowPendingGesture();
        }

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
                    Pointer(false, _stroke.Position, _touchId);
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
            if (pressed && !_pressing)
            {
                // A touch on the HUD (pause, the bows) is the HUD's.
                var events = EventSystem.current;
                _ignorePress = events != null && events.IsPointerOverGameObject(pointerId);
                if (_ignorePress)
                    _pressing = true;
                else
                    PointerDown(position);
                return;
            }
            if (pressed)
            {
                if (!_ignorePress)
                    PointerMove(position);
                return;
            }
            if (_pressing)
            {
                if (_ignorePress)
                {
                    _pressing = false;
                    _ignorePress = false;
                }
                else
                {
                    PointerUp();
                }
            }
        }

        /// <summary>The finger touches the screen (screen pixels, origin bottom left).</summary>
        public void PointerDown(Vector2 screen)
        {
            if (!_entered || _ended || _paused)
                return;
            _pressing = true;
            _moved = false;
            _pressPosition = screen;
            _pressTime = Time.unscaledTime;
            _stroke.Begin(screen);
            _brushTarget = screen;
        }

        /// <summary>The finger slides: passes over the zones it crosses.</summary>
        public void PointerMove(Vector2 screen)
        {
            if (!_pressing || !_entered || _ended || _paused)
                return;
            if (!_moved && (screen - _pressPosition).sqrMagnitude > TapPixels * TapPixels)
                _moved = true;
            _brushTarget = screen;
            if (_animal == null || !_moved)
                return;
            int count = _stroke.Move(screen, _animal.ZoneCount, _animal.Screen, _animal.ScreenRadii, _animal.Hittable, _passes);
            float speed = _stroke.LastDelta.magnitude / Mathf.Max(0.004f, Time.unscaledDeltaTime);
            for (int i = 0; i < count; i++)
                Pass(_passes[i], speed);
        }

        /// <summary>The finger leaves: a short still touch is a tap.</summary>
        public void PointerUp()
        {
            if (!_pressing)
                return;
            _pressing = false;
            bool tap = !_moved && Time.unscaledTime - _pressTime <= TapSeconds;
            _stroke.End();
            if (tap && _entered && !_ended && !_paused)
                Tap(_pressPosition);
        }

        /// <summary>A whole tap at a screen point (tests).</summary>
        public void TapAt(Vector2 screen)
        {
            PointerDown(screen);
            PointerUp();
        }

        private void CancelStroke()
        {
            _pressing = false;
            _ignorePress = false;
            _touchId = -1;
            _stroke.End();
        }

        public bool Brushing => _stroke.Down && _moved;

        /// <summary>
        /// One pass over a zone. A dirty zone always counts. A clean one (or one never dirty) counts only when the finger is
        /// on it alone: crossing the clean fur on the way to dust is never a rebrush (doc 01: no punishment feel).
        /// </summary>
        private void Pass(int zone, float pixelsPerSecond)
        {
            var phase = _game.Phase;
            if (phase == GroomingPhase.Done)
                return;
            if (!_game.IsDirty(zone) && FingerOnDust())
                return;
            _game.Brush(zone);
            _lastPassTime = _time;
            // J-SFX-ACT-001-01: the felted whoosh on every pass, brighter when quick.
            float speed01 = Mathf.Clamp01(pixelsPerSecond / (0.9f * Mathf.Max(1f, _data.camera != null ? _data.camera.pixelHeight : 2000f)));
            GroomingAudio.Brush(Audio, speed01);
        }

        private bool FingerOnDust()
        {
            var finger = _stroke.Position;
            for (int z = 0; z < _animal.ZoneCount; z++)
            {
                if (!_animal.Hittable[z] || !_game.IsDirty(z))
                    continue;
                float r = _animal.ScreenRadii[z];
                if ((_animal.Screen[z] - finger).sqrMagnitude < r * r)
                    return true;
            }
            return false;
        }

        /// <summary>A tap: the golden brush first, then a soap bubble, then the nearest leaf or burr under the finger.</summary>
        private void Tap(Vector2 screen)
        {
            if (_animal == null)
                return;
            float minRadius = 0.035f * (_data.camera != null ? _data.camera.pixelHeight : 2000f);
            if (_goldenShown && !_game.GoldenBrushActive && _game.GoldenBrushZone >= 0
                && (screen - _goldenScreen).sqrMagnitude < Square(Mathf.Max(minRadius * 1.4f, _goldenScreenRadius)))
            {
                _game.TakeGoldenBrush();
                return;
            }
            for (int b = 0; b < _bubbleState.Length; b++)
            {
                if (_bubbleState[b] != PropShown || !_game.BubbleActive(b))
                    continue;
                if ((screen - _bubbleScreen[b]).sqrMagnitude < Square(Mathf.Max(minRadius, _bubbleScreenRadius[b] * 1.25f)))
                {
                    _game.TapBubble(b);
                    return;
                }
            }
            int best = -1;
            float bestDistance = float.MaxValue;
            int count = Mathf.Min(_game.DebrisCount, GroomingLayout.MaxDebris);
            for (int i = 0; i < count; i++)
            {
                if (_debrisState[i] != PropShown || !_game.DebrisActive(i))
                    continue;
                float d = (screen - _debrisScreen[i]).sqrMagnitude;
                int zone = _game.DebrisZone(i);
                float r = Mathf.Max(minRadius, (uint)zone < (uint)_animal.ZoneCount ? _animal.ScreenRadii[zone] : 0f);
                if (d < r * r && d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            if (best >= 0)
                _game.TapDebris(best);
        }

        private static float Square(float x) => x * x;

        // ---------------- the game's events ----------------

        private IAudioService Audio => _context != null ? _context.Audio : NullAudioService.Instance;

        /// <summary>Where the set's sounds play: the refuge's listening point (the audio view stays the refuge's: full level, centred).</summary>
        private Vector3 AudioPoint => _activeHost != null && _activeHost.CameraRig != null ? _activeHost.CameraRig.Focus : Vector3.zero;

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

        private void OnEvent(in GroomingEvent e)
        {
            if (_set != null)
                GroomingAudio.Play(Audio, in e, _species, AudioPoint);
            var haptics = _context != null ? _context.Haptics : NullHapticService.Instance;
            switch (e.Kind)
            {
                case GroomingEventKind.ZonePass:
                    Foam(e.Index, GroomingLayout.FoamPerPass);
                    TutorialPassed();
                    break;
                case GroomingEventKind.ZoneCleaned:
                    Foam(e.Index, GroomingLayout.FoamPerPass);
                    Glints(e.Index);
                    haptics.Play(HapticEventId.TapLight);
                    PlayHappy();
                    TutorialPassed();
                    break;
                case GroomingEventKind.Rebrush:
                    // J-SFX-ACT-001-04: a giggle (its voice is GroomingAudio's), a little start, never a scolding.
                    if (_animal != null)
                        _animal.View.PlayState(AnimalStateHashes.React, 0.06f);
                    break;
                case GroomingEventKind.DebrisRemoved:
                case GroomingEventKind.BurrRemoved:
                    FlyDebris(e.Index);
                    haptics.Play(HapticEventId.TapLight);
                    TutorialItemRemoved(e.Kind == GroomingEventKind.BurrRemoved);
                    break;
                case GroomingEventKind.BurrLoosened:
                    WobbleBurr(e.Index);
                    haptics.Play(HapticEventId.TapLight);
                    if (!_burrHintDone)
                    {
                        _burrHint = e.Index;
                        _burrHintAt = _time + BurrHintDelay;
                    }
                    break;
                case GroomingEventKind.BubbleSpawned:
                    ShowBubble(e.Index);
                    break;
                case GroomingEventKind.BubblePopped:
                    PopBubble(e.Index);
                    haptics.Play(HapticEventId.TapLight);
                    break;
                case GroomingEventKind.GoldenBrushOffered:
                    ShowGolden();
                    break;
                case GroomingEventKind.GoldenBrushStart:
                    TakeGolden();
                    haptics.Play(HapticEventId.Confirm);
                    break;
                case GroomingEventKind.GoldenBrushEnd:
                    SetBrushGold(false);
                    _activeHost?.Hud?.HideBoost();
                    break;
                case GroomingEventKind.BowPhase:
                    ShowPicker();
                    break;
                case GroomingEventKind.BowPlaced:
                    HidePicker();
                    FlyBow(e.Index);
                    haptics.Play(HapticEventId.Confirm);
                    break;
                case GroomingEventKind.AllClean:
                    PlayHappy(true);
                    break;
                case GroomingEventKind.Finished:
                    Celebrate();
                    break;
            }
        }

        // ---------------- the animal's reactions ----------------

        private void PlayHappy(bool always = false)
        {
            if (_animal == null || (!always && _time - _happyAt < HappyCooldown))
                return;
            _happyAt = _time;
            _animal.View.PlayState(AnimalStateHashes.Happy, 0.15f);
        }

        private void UpdateReactions()
        {
            if (_welcomeAt >= 0f && _time >= _welcomeAt)
            {
                _welcomeAt = -1f;
                PlayHappy(true);
            }
            // « Il ferme les yeux »: half closed while brushed, relaxed between strokes, closed content at the end.
            float target = _ended ? 0.9f : _time - _lastPassTime < BrushingSeconds ? 1f : 0.12f;
            float rate = target > _content ? 2.4f : 0.9f;
            _content = Mathf.MoveTowards(_content, target, _dt * rate);
            if (_sighAt >= 0f && _time >= _sighAt)
            {
                _sighAt = -1f;
                GroomingAudio.Sigh(Audio, _species, AudioPoint);
            }
        }

        /// <summary>The end (doc 01: hearts H-VFX-012, a content sigh): once, whether by the bow or by the time.</summary>
        private void Celebrate()
        {
            if (_celebrated || _animal == null || _game.Elapsed < 0.5f)
                return;
            _celebrated = true;
            HideBrushNow();
            PlayHappy(true);
            var refuge = _activeHost != null ? _activeHost.Refuge : null;
            var head = _animal.HeadTop != null ? _animal.HeadTop.position : _animal.View.transform.position + Vector3.up * 0.3f;
            // The refuge's own heart burst (pooled there, world-space particles): played at the head, over the set.
            refuge?.Vfx?.Play(VfxId.FavoriteHeart, head + Vector3.up * (0.02f * _animal.Scale), refuge.SimulationTime);
            _sighAt = _time + 0.6f;
        }

        // ---------------- the bow picker (doc 01: rose, bleu ciel, crème) ----------------

        private void ShowPicker()
        {
            HideBrushNow();
            var hud = _activeHost != null ? _activeHost.Hud : null;
            if (hud == null || _pickerShown || _game.BowPlaced)
                return;
            for (int i = 0; i < _pickerItems.Length; i++)
            {
                var icon = hud.Icon(BowIconNames[i]);
                // White keeps a drawn icon's own colors; without its icon a soft disc of the bow's color.
                _pickerItems[i] = new ActivityPickerItem { Icon = icon, Tint = icon != null ? Color.white : BowTint(i) };
            }
            hud.ShowPicker(_pickerItems, _pickerItems.Length, this);
            _pickerShown = true;
            if (_tutorialStep > 0)
                ShowTip(ActivityText.GroomingTipBow, TipSeconds);
        }

        private void HidePicker()
        {
            if (!_pickerShown)
                return;
            _pickerShown = false;
            var hud = _activeHost != null ? _activeHost.Hud : null;
            if (hud != null && hud.PickerShown)
                hud.HidePicker();
        }

        /// <summary>A bow picked in the HUD (0 pink, 1 sky blue, 2 cream).</summary>
        public void Pick(int index)
        {
            _pickerShown = false;
            if (!_entered || _ended || _game.Phase != GroomingPhase.Bow || (uint)index > (uint)GroomingBow.Cream)
                return;
            _game.PlaceBow((GroomingBow)index);
            DrainEvents();
        }

        private static Color BowTint(int bow) =>
            bow == 0 ? new Color(0.96f, 0.65f, 0.75f) : bow == 1 ? new Color(0.61f, 0.8f, 0.94f) : new Color(0.97f, 0.91f, 0.81f);

        // ---------------- A15 tutorial (doc 01 Contenu, A15; doc Mini-jeux section 1: understood without text) ----------------

        private void StartTutorial(bool tutorial)
        {
            _tutorialStep = tutorial ? 1 : 0;
            _tutorialTarget = -1;
            _burrHint = -1;
            _burrHintAt = -1f;
            _burrHintDone = false;
            _gesturePending = tutorial;
            _pendingTip = tutorial ? ActivityText.GroomingTipBrush : null;
        }

        private void ShowTip(string key, float seconds)
        {
            var hud = _activeHost != null ? _activeHost.Hud : null;
            var text = _activeHost != null ? _activeHost.Text : null;
            if (hud == null || text == null)
                return;
            hud.ShowTip(text.Get(key), seconds);
        }

        /// <summary>The first pass made: the brush paw goes, the leaf comes (A15: « patte qui tape sur la première feuille »).</summary>
        private void TutorialPassed()
        {
            if (_tutorialStep != 1)
                return;
            _activeHost?.Hud?.HideGesture();
            _gesturePending = false;
            _tutorialStep = 2;
            _tutorialTarget = FirstItem(false);
            if (_tutorialTarget < 0)
            {
                NextTutorialStep();
                return;
            }
            ShowTip(ActivityText.GroomingTipLeaves, TipSeconds);
            _gesturePending = true;
        }

        private void TutorialItemRemoved(bool burr)
        {
            if (_tutorialStep == 2 && !burr)
                NextTutorialStep();
            else if (_tutorialStep == 3 && burr)
                NextTutorialStep();
            if (burr)
            {
                _burrHintDone = true;
                if (_burrHint >= 0)
                {
                    _burrHint = -1;
                    _activeHost?.Hud?.HideGesture();
                }
            }
        }

        private void NextTutorialStep()
        {
            _activeHost?.Hud?.HideGesture();
            _gesturePending = false;
            if (_tutorialStep == 2)
            {
                _tutorialStep = 3;
                _tutorialTarget = FirstItem(true);
                if (_tutorialTarget >= 0)
                {
                    ShowTip(ActivityText.GroomingTipBurr, TipSeconds);
                    _gesturePending = true;
                    return;
                }
            }
            _tutorialStep = 4;
            _tutorialTarget = -1;
        }

        private int FirstItem(bool burr)
        {
            int count = Mathf.Min(_game.DebrisCount, GroomingLayout.MaxDebris);
            int best = -1;
            float bestY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                if (!_game.DebrisActive(i) || _game.DebrisIsBurr(i) != burr || _debrisState[i] != PropShown)
                    continue;
                // The most visible one: the highest on screen (never hidden by the paw coming from below).
                if (_debrisScreen[i].y > bestY)
                {
                    bestY = _debrisScreen[i].y;
                    best = i;
                }
            }
            return best;
        }

        private void UpdateTutorial()
        {
            if ((_tutorialStep == 2 || _tutorialStep == 3) && _tutorialTarget >= 0 && !_game.DebrisActive(_tutorialTarget))
                NextTutorialStep();
            // Any game: a burr tapped once and left gets its double-tap paw, once (no text).
            if (_burrHint >= 0 && _burrHintAt >= 0f && _time >= _burrHintAt && _tutorialStep != 3)
            {
                _burrHintAt = -1f;
                _burrHintDone = true;
                if (_game.DebrisActive(_burrHint))
                {
                    _tutorialTarget = _burrHint;
                    _gesturePending = true;
                }
                else
                {
                    _burrHint = -1;
                }
            }
        }

        /// <summary>The paw, once the zones are on screen (after the first LateTick); moved only when its target moved.</summary>
        private void ShowPendingGesture()
        {
            var hud = _activeHost != null ? _activeHost.Hud : null;
            if (hud == null)
            {
                _gesturePending = false;
                return;
            }
            if (_tutorialStep == 1)
            {
                if (!SwipeAcrossDust(out var from, out var to))
                    return;
                hud.ShowGesture(ActivityGesture.Swipe, from, to);
                _gesturePending = false;
                return;
            }
            int target = _tutorialTarget;
            if (target < 0 || !_game.DebrisActive(target) || _debrisState[target] != PropShown)
            {
                _gesturePending = false;
                return;
            }
            var point = _debrisScreen[target];
            bool burr = _game.DebrisIsBurr(target);
            hud.ShowGesture(burr ? ActivityGesture.DoubleTap : ActivityGesture.Tap, point, point);
            _gestureAt = point;
            // The target may drift with a calm sway: the paw follows it when it moved a finger's width.
            _gesturePending = false;
            _followGesture = true;
        }

        private bool _followGesture;

        /// <summary>A swipe through the dustiest part of the coat: centred on the dirty zone nearest their middle, across.</summary>
        private bool SwipeAcrossDust(out Vector2 from, out Vector2 to)
        {
            from = to = default;
            if (_animal == null)
                return false;
            var sum = Vector2.zero;
            int n = 0;
            for (int z = 0; z < _animal.ZoneCount; z++)
            {
                if (!_animal.Hittable[z] || !_game.IsDirty(z))
                    continue;
                sum += _animal.Screen[z];
                n++;
            }
            if (n == 0)
                return false;
            var middle = sum / n;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int z = 0; z < _animal.ZoneCount; z++)
            {
                if (!_animal.Hittable[z] || !_game.IsDirty(z))
                    continue;
                float d = (_animal.Screen[z] - middle).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = z;
                }
            }
            var center = _animal.Screen[best];
            float half = 0.17f * (_data.camera != null ? _data.camera.pixelWidth : 1080f);
            from = center - new Vector2(half, 0f);
            to = center + new Vector2(half, 0f);
            return true;
        }

        private void FollowGesture()
        {
            if (!_followGesture || _tutorialTarget < 0 || _debrisState[_tutorialTarget] != PropShown)
                return;
            var point = _debrisScreen[_tutorialTarget];
            if ((point - _gestureAt).sqrMagnitude < 24f * 24f)
                return;
            _gestureAt = point;
            _activeHost?.Hud?.ShowGesture(_game.DebrisIsBurr(_tutorialTarget) ? ActivityGesture.DoubleTap : ActivityGesture.Tap, point, point);
        }

        private void HideAids()
        {
            var hud = _activeHost != null ? _activeHost.Hud : null;
            _gesturePending = false;
            _followGesture = false;
            if (hud == null)
            {
                _pickerShown = false;
                return;
            }
            hud.HideTip();
            hud.HideGesture();
            HidePicker();
            hud.HideBoost();
        }
    }
}
