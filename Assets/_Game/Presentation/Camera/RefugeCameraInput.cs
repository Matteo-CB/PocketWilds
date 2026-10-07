using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PocketWilds.Presentation
{
    /// <summary>Receives taps (touch released quickly without dragging). Implemented by the scene controller.</summary>
    public interface IRefugeTapHandler
    {
        void OnTap(Vector2 screenPosition);
    }

    /// <summary>
    /// Touch and mouse input for the refuge camera: one-finger drag pans (and flings on release), pinch or wheel
    /// zooms, a short touch without movement is a tap (egg, animal). Polls devices directly: no callbacks, no allocation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RefugeCameraRig))]
    public sealed class RefugeCameraInput : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _tapMaxPixels = 14f;
        [SerializeField, Min(0.05f)] private float _tapMaxSeconds = 0.3f;
        [SerializeField, Min(0.0001f)] private float _wheelZoomPerUnit = 0.0012f;
        // The drag speed is averaged over about this time; a finger held still this long before release does not fling.
        [SerializeField, Range(0.02f, 0.3f)] private float _flingWindow = 0.08f;

        private RefugeCameraRig _rig;
        private IRefugeTapHandler _tapHandler;
        private bool _pressing;
        private bool _pressFromTouch;
        private bool _dragging;
        private float _pressTime;
        private Vector2 _pressPosition;
        private Vector2 _lastPosition;
        private Vector2 _dragVelocity;
        private float _lastMoveTime;
        private float _lastPinchDistance;
        private bool _blocked;

        public void SetTapHandler(IRefugeTapHandler handler) => _tapHandler = handler;

        /// <summary>A modal screen is open: no pan, zoom or world tap. An ongoing press is dropped.</summary>
        public bool Blocked
        {
            get => _blocked;
            set
            {
                _blocked = value;
                if (value)
                {
                    _pressing = false;
                    _dragging = false;
                    _lastPinchDistance = 0f;
                    if (_rig != null)
                        _rig.StopFling();
                }
            }
        }

        private void Awake() => _rig = GetComponent<RefugeCameraRig>();

        private void Update()
        {
            if (_blocked)
                return;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && HandleTouches(touchscreen))
                return;
            var mouse = Mouse.current;
            if (mouse != null)
                HandleMouse(mouse);
        }

        private bool HandleTouches(Touchscreen touchscreen)
        {
            var touches = touchscreen.touches;
            int active = 0;
            TouchControl first = null, second = null;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (!touch.press.isPressed)
                    continue;
                if (active == 0) first = touch;
                else if (active == 1) second = touch;
                active++;
            }

            if (active >= 2)
            {
                float distance = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());
                if (_lastPinchDistance > 0f && distance > 1f)
                    _rig.Zoom(_lastPinchDistance / distance);
                _lastPinchDistance = distance;
                _dragging = true; // a pinch is never a tap
                _dragVelocity = Vector2.zero; // nor a fling
                _rig.StopFling();
                return true;
            }
            _lastPinchDistance = 0f;

            if (active == 1)
            {
                _pressFromTouch = true;
                Pointer(true, first.position.ReadValue());
                return true;
            }
            if (_pressing && _pressFromTouch)
            {
                Pointer(false, _lastPosition);
                _pressFromTouch = false;
                return true;
            }
            return false;
        }

        private void HandleMouse(Mouse mouse)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
                _rig.Zoom(1f - wheel * _wheelZoomPerUnit);
            Pointer(mouse.leftButton.isPressed, mouse.position.ReadValue());
        }

        private void Pointer(bool pressed, Vector2 position)
        {
            float now = Time.unscaledTime;
            if (pressed && !_pressing)
            {
                _pressing = true;
                _dragging = false;
                _pressTime = _lastMoveTime = now;
                _pressPosition = _lastPosition = position;
                _dragVelocity = Vector2.zero;
                // Touching a gliding view catches it.
                _rig.StopFling();
                return;
            }
            if (pressed)
            {
                if (!_dragging && (position - _pressPosition).sqrMagnitude > _tapMaxPixels * _tapMaxPixels)
                    _dragging = true;
                var delta = position - _lastPosition;
                if (_dragging)
                    _rig.PanPixels(delta);
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f)
                {
                    // Smoothed finger speed: the last few frames count, an old fast stroke fades out.
                    float k = 1f - Mathf.Exp(-dt / _flingWindow);
                    _dragVelocity = Vector2.Lerp(_dragVelocity, delta / dt, k);
                }
                if (delta.sqrMagnitude > 0.25f)
                    _lastMoveTime = now;
                _lastPosition = position;
                return;
            }
            if (_pressing)
            {
                _pressing = false;
                if (!_dragging && now - _pressTime <= _tapMaxSeconds)
                    _tapHandler?.OnTap(_pressPosition);
                else if (_dragging && now - _lastMoveTime <= _flingWindow)
                    _rig.Fling(_dragVelocity);
            }
        }
    }
}
