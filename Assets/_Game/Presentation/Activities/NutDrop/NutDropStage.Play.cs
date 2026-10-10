using PocketWilds.Core;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PocketWilds.Presentation
{
    // The game as it plays: one finger anywhere, the game's events, the animal's reactions, the tutorial, the boost chip.
    public sealed partial class NutDropStage
    {
        /// <summary>Doc 01 S-ACT-003 « Objets »: the HUD's boost chip icons (agent NA), borrowed from the HUD (never in this bundle).</summary>
        public const string WideBasketIconName = "ICO_WideBasket";

        public const string FeatherIconName = "ICO_Feather";
        public const string GoldenShowerIconName = "ICO_GoldenShower";

        /// <summary>The tutorial's paw shows after this long (doc: « après 1 s »).</summary>
        public const float TutorialDelay = 1f;

        /// <summary>The slide tip stays until the first catch (the HUD hides it sooner only with its own time).</summary>
        private const float SlideTipSeconds = 30f;

        private const float TipSeconds = 2.5f;

        /// <summary>A Happy or React plays this long before the walk or the idle comes back.</summary>
        private const float ReactionSeconds = 0.75f;

        private float _time;
        private float _dt;
        private bool _pressing;
        private bool _ignorePress;
        private int _touchId = -1;
        private float _fingerX;
        private bool _celebrated;
        private readonly Sprite[] _boostIcons = new Sprite[3];

        // Tutorial (first game): 0 none, 1 waiting for the paw, 2 paw shown until the first catch, 3 done.
        private int _tutorialStep;
        private bool _pineconeSeen;
        private bool _pineconeTipShown;
        private bool _slideTipShown;
        private float _resumeSlideAt = -1f;

        /// <summary>The stage reads the touchscreen and the mouse itself (tests turn it off and call the pointer methods).</summary>
        public bool PollInput { get; set; } = true;

        /// <summary>The tutorial step (0 none, 1 waiting, 2 the swipe paw and the slide tip, 3 done).</summary>
        public int TutorialStep => _tutorialStep;

        public bool PineconeTipShown => _pineconeTipShown;

        /// <summary>The finger's last playfield x (what the basket was asked to follow).</summary>
        public float FingerX => _fingerX;

        // ---------------- per frame ----------------

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!_entered || _set == null)
                return;
            _dt = _paused ? 0f : Mathf.Min(deltaTime, 0.1f);
            _time += _dt;
            if (!_paused && !_ended && PollInput)
                PollPointer();
            DrainEvents();
            if (_paused)
                return;
            KeepLighting();
            if (_data.camera != null && (_data.camera.pixelWidth != _framedWidth || _data.camera.pixelHeight != _framedHeight))
                FrameCamera();
            SyncItems();
            if (!_ended)
                UpdateTutorial();
            // The HUD's streak is the presenter's (runner.Streak is the game's, every frame).
        }

        /// <summary>After the animation (NutDropSetView.LateUpdate): the animal's pose, the basket on its head, the items, the effects.</summary>
        public void LateTick()
        {
            if (!_entered || _set == null)
                return;
            float dt = _dt;
            _dt = 0f;
            UpdateAnimal(dt);
            UpdateBasket(dt);
            UpdateItems(dt);
            UpdateEffects(dt);
        }

        // ---------------- the finger (anywhere on the screen; a touch that starts on the HUD is the HUD's) ----------------

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
                    Pointer(false, default, _touchId);
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
                _ignorePress = false;
                PointerUp();
            }
        }

        /// <summary>The finger touches the screen (screen pixels, origin bottom left): the basket goes under it.</summary>
        public void PointerDown(Vector2 screen)
        {
            if (!_entered || _ended || _paused)
                return;
            _pressing = true;
            Follow(screen.x);
        }

        /// <summary>The finger slides: the basket follows its x (the game moves it there at its own speed).</summary>
        public void PointerMove(Vector2 screen)
        {
            if (!_pressing || !_entered || _ended || _paused)
                return;
            Follow(screen.x);
        }

        public void PointerUp()
        {
            _pressing = false;
        }

        private void CancelPointer()
        {
            _pressing = false;
            _ignorePress = false;
            _touchId = -1;
        }

        private void Follow(float screenX)
        {
            _fingerX = FieldX(screenX);
            _game.SetBasketTarget(_fingerX);
        }

        /// <summary>The playfield x under a screen x: the full width less a small margin at each side, clamped to the field.</summary>
        public float FieldX(float screenX)
        {
            float width = _data != null && _data.camera != null ? _data.camera.pixelWidth : Screen.width;
            float u = (screenX / Mathf.Max(1f, width) - 0.5f) / (0.5f - NutDropLayout.InputMargin);
            return Mathf.Clamp(u, -1f, 1f) * _game.HalfWidth;
        }

        /// <summary>The screen x that asks for a playfield x (tests and captures).</summary>
        public float ScreenX(float fieldX)
        {
            float width = _data != null && _data.camera != null ? _data.camera.pixelWidth : Screen.width;
            float u = Mathf.Clamp(fieldX / Mathf.Max(0.01f, _game.HalfWidth), -1f, 1f);
            return (0.5f + u * (0.5f - NutDropLayout.InputMargin)) * width;
        }

        // ---------------- the game's events ----------------

        private IAudioService Audio => _context != null ? _context.Audio : NullAudioService.Instance;

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

        private void OnEvent(in NutDropEvent e)
        {
            if (_set != null)
                NutDropAudio.Play(Audio, in e, AudioPoint);
            var haptics = _context != null ? _context.Haptics : NullHapticService.Instance;
            switch (e.Kind)
            {
                case NutDropEventKind.Spawned:
                    OnSpawned(e.Item, e.ItemKind);
                    if (e.ItemKind == NutDropItemKind.Pinecone && _tutorialStep > 0)
                        _pineconeSeen = true;
                    break;
                case NutDropEventKind.Caught:
                    if (e.ItemKind == NutDropItemKind.Pinecone)
                    {
                        Bonk(e.Item);
                    }
                    else if (NutDropAudio.IsSpecial(e.ItemKind))
                    {
                        Pop(e.Item);
                    }
                    else
                    {
                        Catch(e.Item, e.ItemKind);
                        haptics.Play(HapticEventId.TapLight);
                        TutorialCaught();
                    }
                    break;
                case NutDropEventKind.Missed:
                    Miss(e.Item);
                    break;
                case NutDropEventKind.Pinecone:
                    Bonk(e.Item);
                    // A soft felted bonk, never a scolding.
                    haptics.Play(HapticEventId.TapLight);
                    PlayReaction(AnimalStateHashes.React);
                    break;
                case NutDropEventKind.MultiplierUp:
                    PlayReaction(AnimalStateHashes.Happy);
                    Heart();
                    break;
                case NutDropEventKind.SpecialStarted:
                    Pop(e.Item);
                    Ring();
                    haptics.Play(HapticEventId.Confirm);
                    ShowBoost(e.ItemKind, e.Seconds);
                    break;
                case NutDropEventKind.SpecialEnded:
                    if (_game.ActiveSpecial == NutDropItemKind.None)
                        _activeHost?.Hud?.HideBoost();
                    break;
                case NutDropEventKind.Finished:
                    Celebrate();
                    break;
            }
        }

        /// <summary>Where the set's sounds play: the refuge's listening point (the audio view stays the refuge's: full level, centred).</summary>
        private Vector3 AudioPoint => _activeHost != null && _activeHost.CameraRig != null ? _activeHost.CameraRig.Focus : Vector3.zero;

        private void ShowBoost(NutDropItemKind kind, float seconds)
        {
            var hud = _activeHost != null ? _activeHost.Hud : null;
            if (hud == null)
                return;
            int index = kind == NutDropItemKind.WideBasket ? 0 : kind == NutDropItemKind.Feather ? 1 : kind == NutDropItemKind.GoldenShower ? 2 : -1;
            if (index < 0)
                return;
            float left = seconds > 0f ? seconds : _game.SpecialLeft;
            // A missing icon keeps the chip's own fallback (the HUD's ring and soft disc).
            hud.ShowBoost(_boostIcons[index], left);
        }

        /// <summary>The HUD's boost icons, borrowed once a game (the icon names are agent NA's).</summary>
        private void BorrowIcons()
        {
            var hud = _activeHost != null ? _activeHost.Hud : null;
            _boostIcons[0] = hud?.Icon(WideBasketIconName);
            _boostIcons[1] = hud?.Icon(FeatherIconName);
            _boostIcons[2] = hud?.Icon(GoldenShowerIconName);
            if (hud != null && (_boostIcons[0] == null || _boostIcons[1] == null || _boostIcons[2] == null))
                GameLog.Warning(LogCategory.Presentation, "Nut Drop: a boost icon is missing in the activity HUD (ICO_WideBasket, ICO_Feather, ICO_GoldenShower): the chip shows its fallback.");
        }

        // ---------------- the animal's reactions ----------------

        private void PlayReaction(int state)
        {
            if (_view == null || _ended)
                return;
            _view.PlayState(state, 0.1f);
            _reactionUntil = _time + ReactionSeconds;
        }

        /// <summary>The end: a happy hop under a heart, once (whether the time ran out or the game was left).</summary>
        private void Celebrate()
        {
            if (_celebrated || _view == null || _game.Elapsed < 0.5f)
                return;
            _celebrated = true;
            _view.PlayState(AnimalStateHashes.Happy, 0.15f);
            _reactionUntil = _time + ReactionSeconds;
            Heart();
        }

        // ---------------- tutorial (doc 01 A15; doc Mini-jeux section 1: understood without text in 3 s) ----------------

        private void StartTutorial(bool tutorial)
        {
            _tutorialStep = tutorial ? 1 : 0;
            _pineconeSeen = false;
            _pineconeTipShown = false;
            _slideTipShown = false;
            _resumeSlideAt = -1f;
        }

        private void UpdateTutorial()
        {
            if (_tutorialStep == 0)
                return;
            if (_tutorialStep == 1 && _time >= TutorialDelay)
                ShowPaw();
            // « Évite les pommes de pin » when the first pinecone falls (after the paw's first words), then the slide tip again
            // until the first catch.
            if (_pineconeSeen && !_pineconeTipShown && _tutorialStep >= 2)
            {
                _pineconeTipShown = true;
                _slideTipShown = false;
                ShowTip(ActivityText.NutDropTipPinecone, TipSeconds);
                if (_tutorialStep == 2)
                    _resumeSlideAt = _time + TipSeconds;
            }
            if (_resumeSlideAt >= 0f && _time >= _resumeSlideAt)
            {
                _resumeSlideAt = -1f;
                if (_tutorialStep == 2)
                {
                    ShowTip(ActivityText.NutDropTipSlide, SlideTipSeconds);
                    _slideTipShown = true;
                }
            }
        }

        /// <summary>A paw sliding left and right under the basket, with « Glisse pour attraper », until the first catch.</summary>
        private void ShowPaw()
        {
            _tutorialStep = 2;
            var hud = _activeHost != null ? _activeHost.Hud : null;
            if (hud == null || _data.camera == null)
                return;
            float x = _game.BasketX * NutDropLayout.FieldScale;
            var origin = _data.origin.position;
            float reach = 0.55f * NutDropLayout.FieldScale * _game.HalfWidth;
            const float y = -0.02f;
            var from = (Vector2)_data.camera.WorldToScreenPoint(origin + new Vector3(Mathf.Clamp(x - reach, -HalfFieldWorld, HalfFieldWorld), y, 0f));
            var to = (Vector2)_data.camera.WorldToScreenPoint(origin + new Vector3(Mathf.Clamp(x + reach, -HalfFieldWorld, HalfFieldWorld), y, 0f));
            hud.ShowGesture(ActivityGesture.Swipe, from, to);
            ShowTip(ActivityText.NutDropTipSlide, SlideTipSeconds);
            _slideTipShown = true;
        }

        private float HalfFieldWorld => _game.HalfWidth * NutDropLayout.FieldScale;

        /// <summary>The first catch: the paw and the slide tip go (a pinecone tip still showing stays its time).</summary>
        private void TutorialCaught()
        {
            if (_tutorialStep == 0 || _tutorialStep == 3)
                return;
            bool pawShown = _tutorialStep == 2;
            _tutorialStep = 3;
            _resumeSlideAt = -1f;
            var hud = _activeHost != null ? _activeHost.Hud : null;
            if (hud == null || !pawShown)
                return;
            hud.HideGesture();
            if (_slideTipShown)
                hud.HideTip();
            _slideTipShown = false;
        }

        private void ShowTip(string key, float seconds)
        {
            var hud = _activeHost != null ? _activeHost.Hud : null;
            var text = _activeHost != null ? _activeHost.Text : null;
            if (hud == null || text == null)
                return;
            hud.ShowTip(text.Get(key), seconds);
        }

        private void HideAids()
        {
            var hud = _activeHost != null ? _activeHost.Hud : null;
            if (hud == null)
                return;
            hud.HideTip();
            hud.HideGesture();
            hud.HideBoost();
        }
    }
}
