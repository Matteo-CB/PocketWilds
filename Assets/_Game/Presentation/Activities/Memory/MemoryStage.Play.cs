using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PocketWilds.Presentation
{
    // The game as it plays: a tap turns a card, the cards follow the game's state, the game's events play their effects and
    // sounds, the first game's tapping paw.
    public sealed partial class MemoryStage
    {
        /// <summary>The tutorial's paw shows this long after the preview turned the cards back (or after the start without one).</summary>
        public const float TutorialDelay = 0.4f;

        /// <summary>The paw taps one card, then its pair, one tap each (the HUD's tap paw: 1.1 s a tap).</summary>
        public const float TutorialTapPeriod = 1.1f;

        /// <summary>The pairs tip stays until the first match (the HUD hides it sooner only with its own time).</summary>
        private const float PairsTipSeconds = 30f;

        private float _time;
        private float _dt;
        private bool _pressing;
        private int _touchId = -1;

        // Tutorial (first game): 0 none, 1 waiting for the preview, 2 the paw on a card and its pair, 3 done.
        private int _tutorialStep;
        private float _tutorialAt;
        private int _tutorialCard = -1;
        private int _tutorialPair = -1;
        private int _tutorialTarget = -1;
        private float _tutorialSwitchAt;
        private bool _tipShown;
        private bool _wasPreviewing;

        /// <summary>The stage reads the touchscreen and the mouse itself (tests turn it off and call <see cref="PointerDown"/>).</summary>
        public bool PollInput { get; set; } = true;

        /// <summary>The tutorial step (0 none, 1 waiting for the preview, 2 the tapping paw and the pairs tip, 3 done).</summary>
        public int TutorialStep => _tutorialStep;

        /// <summary>The card the tutorial's paw taps now (−1: none).</summary>
        public int TutorialTarget => _tutorialStep == 2 ? _tutorialTarget : -1;

        // Counts of what the stage played this game (tests and captures).
        public int Matches { get; private set; }
        public int Mismatches { get; private set; }
        public int MistakesSeen { get; private set; }
        public int Glows { get; private set; }
        public int Swaps { get; private set; }
        public int Taps { get; private set; }

        // ---------------- per frame ----------------

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!_entered)
                return;
            _dt = _paused ? 0f : Mathf.Min(deltaTime, 0.1f);
            _time += _dt;
            if (!_paused && !_ended && PollInput && _view != null)
                PollPointer();
            // Drained even without a screen (the game's ring stays clean).
            DrainEvents();
            if (_paused || _view == null)
                return;
            SyncCards(false);
            if (!_ended)
                UpdateTutorial();
            _view.Tick(_dt);
        }

        private void ResetPlay()
        {
            _time = 0f;
            _dt = 0f;
            _wasPreviewing = false;
            Matches = Mismatches = MistakesSeen = Glows = Swaps = Taps = 0;
        }

        /// <summary>
        /// Each card shows the side the game says: face up while previewing, turned over or matched. A change flips it (the
        /// preview's flips run along the diagonals). A few reads per card, no allocation.
        /// </summary>
        private void SyncCards(bool instant)
        {
            bool previewing = _game.Previewing;
            bool previewChange = previewing != _wasPreviewing;
            _wasPreviewing = previewing;
            int columns = Mathf.Max(1, _game.Columns);
            int count = Mathf.Min(_game.CardCount, _view.Capacity);
            for (int card = 0; card < count; card++)
            {
                bool matched = _game.Matched(card);
                bool up = previewing || matched || _game.FaceUp(card);
                float delay = previewChange && !instant ? ((card % columns) + card / columns) * MemoryLayout.PreviewStagger : 0f;
                _view.SetUp(card, up, instant, delay);
                _view.SetMatched(card, matched, instant);
            }
        }

        // ---------------- touches (a tap on a card; a touch that starts on the HUD is the HUD's) ----------------

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

        /// <summary>A finger touches the screen (pixels, origin bottom left): the card under it turns, if the game lets it.</summary>
        public void PointerDown(Vector2 screen)
        {
            if (!_entered || _ended || _paused || _view == null)
                return;
            int card = _view.HitTest(screen);
            if (card < 0 || card >= _game.CardCount)
                return;
            Taps++;
            _game.Flip(card);
        }

        /// <summary>A tap on a card through its screen point (tests and captures: the same hit test as a finger).</summary>
        public void Tap(int card)
        {
            if (_view != null && card >= 0 && card < _game.CardCount)
                PointerDown(_view.ScreenPoint(card));
        }

        private void CancelPointer()
        {
            _pressing = false;
            _touchId = -1;
        }

        // ---------------- the game's events ----------------

        private IAudioService Audio => _context != null && _context.Audio != null ? _context.Audio : NullAudioService.Instance;

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

        private void OnEvent(in MemoryEvent e)
        {
            MemoryAudio.Play(Audio, in e, _game.PairsFound, _game.PairCount);
            var haptics = _context != null && _context.Haptics != null ? _context.Haptics : NullHapticService.Instance;
            switch (e.Kind)
            {
                case MemoryEventKind.Matched:
                    Matches++;
                    if (Valid(e.Card) && Valid(e.Other))
                        _view.Match(e.Card, e.Other);
                    haptics.Play(HapticEventId.TapLight);
                    TutorialMatched();
                    break;
                case MemoryEventKind.Mismatched:
                    Mismatches++;
                    if (e.Mistake)
                        MistakesSeen++;
                    // A gentle shake, never a scolding; the cards turn back on FlippedBack.
                    if (Valid(e.Card))
                        _view.Shake(e.Card);
                    if (Valid(e.Other))
                        _view.Shake(e.Other);
                    break;
                case MemoryEventKind.Glow:
                    Glows++;
                    if (Valid(e.Other))
                        _view.Pulse(e.Other, MemoryLayout.GlowSeconds);
                    break;
                case MemoryEventKind.Swapped:
                    Swaps++;
                    if (Valid(e.Card) && Valid(e.Other))
                    {
                        // Each place now holds the other's card: dealt again, then glides in from where it was.
                        Deal(e.Card);
                        Deal(e.Other);
                        _view.GlideFrom(e.Card, e.Other, MemoryLayout.SwapSeconds);
                        _view.GlideFrom(e.Other, e.Card, MemoryLayout.SwapSeconds);
                    }
                    break;
                case MemoryEventKind.AllMatched:
                    haptics.Play(HapticEventId.Confirm);
                    break;
            }
        }

        private bool Valid(int card) => _view != null && card >= 0 && card < _game.CardCount && card < _view.Capacity;

        // ---------------- tutorial (doc 01 A15; doc Mini-jeux section 1: understood without text in 3 s) ----------------

        private void StartTutorial(bool tutorial)
        {
            _tutorialStep = tutorial ? 1 : 0;
            _tutorialAt = -1f;
            _tutorialCard = _tutorialPair = _tutorialTarget = -1;
            _tipShown = false;
        }

        private void UpdateTutorial()
        {
            if (_tutorialStep == 1)
            {
                // After the preview turned the cards back (or at once without one), a short breath, then the paw.
                if (_game.Previewing)
                {
                    _tutorialAt = -1f;
                    return;
                }
                if (_tutorialAt < 0f)
                    _tutorialAt = _time + TutorialDelay;
                if (_time >= _tutorialAt)
                    ShowPaw();
                return;
            }
            if (_tutorialStep != 2)
                return;
            // The paw taps the card, then its pair, and again; once the card is up it stays on the pair.
            if (_tutorialCard < 0 || _game.Matched(_tutorialCard) || _game.Matched(_tutorialPair))
            {
                PickTutorialPair();
                if (_tutorialCard < 0)
                    return;
            }
            int target = _tutorialTarget;
            if (_game.FaceUp(_tutorialCard) && !_game.FaceUp(_tutorialPair))
                target = _tutorialPair;
            else if (_time >= _tutorialSwitchAt)
            {
                target = _tutorialTarget == _tutorialCard ? _tutorialPair : _tutorialCard;
                _tutorialSwitchAt = _time + TutorialTapPeriod;
            }
            if (target != _tutorialTarget)
                PointPaw(target);
        }

        /// <summary>The tapping paw on a card and then its pair, with « Trouve les paires », until the first match.</summary>
        private void ShowPaw()
        {
            _tutorialStep = 2;
            PickTutorialPair();
            if (_tutorialCard < 0)
                return;
            _tutorialSwitchAt = _time + TutorialTapPeriod;
            PointPaw(_tutorialCard);
            ShowTip(ActivityText.MemoryTipPairs, PairsTipSeconds);
            _tipShown = true;
        }

        /// <summary>The first face-down card in reading order and its pair (none when every card is matched).</summary>
        private void PickTutorialPair()
        {
            _tutorialCard = _tutorialPair = -1;
            int count = _game.CardCount;
            for (int card = 0; card < count && _tutorialCard < 0; card++)
            {
                if (_game.Matched(card))
                    continue;
                int pair = _game.PairOf(card);
                for (int other = card + 1; other < count; other++)
                {
                    if (_game.PairOf(other) == pair && !_game.Matched(other))
                    {
                        _tutorialCard = card;
                        _tutorialPair = other;
                        break;
                    }
                }
            }
        }

        private void PointPaw(int card)
        {
            _tutorialTarget = card;
            var hud = _activeHost != null ? _activeHost.Hud : null;
            if (hud == null || _view == null || card < 0)
                return;
            var point = _view.ScreenPoint(card);
            hud.ShowGesture(ActivityGesture.Tap, point, point);
        }

        /// <summary>The first match: the paw and the pairs tip go.</summary>
        private void TutorialMatched()
        {
            if (_tutorialStep == 0 || _tutorialStep == 3)
                return;
            bool pawShown = _tutorialStep == 2;
            _tutorialStep = 3;
            var hud = _activeHost != null ? _activeHost.Hud : null;
            if (hud == null || !pawShown)
                return;
            hud.HideGesture();
            if (_tipShown)
                hud.HideTip();
            _tipShown = false;
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
        }
    }
}
