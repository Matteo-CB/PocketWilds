using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PocketWilds.Presentation
{
    /// <summary>Receives taps (touch released quickly without dragging). Implemented by the scene controller.</summary>
    public interface ISanctuaryTapHandler
    {
        void OnTap(Vector2 screenPosition);
    }

    /// <summary>
    /// Touch and mouse input for the sanctuary camera: one-finger drag pans, pinch or wheel zooms, a short
    /// touch without movement is a tap (egg, animal). Polls devices directly: no callbacks, no allocation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SanctuaryCameraRig))]
    public sealed class SanctuaryCameraInput : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _tapMaxPixels = 14f;
        [SerializeField, Min(0.05f)] private float _tapMaxSeconds = 0.3f;
        [SerializeField, Min(0.0001f)] private float _wheelZoomPerUnit = 0.0012f;

        private SanctuaryCameraRig _rig;
        private ISanctuaryTapHandler _tapHandler;
        private bool _pressing;
        private bool _pressFromTouch;
        private bool _dragging;
        private float _pressTime;
        private Vector2 _pressPosition;
        private Vector2 _lastPosition;
        private float _lastPinchDistance;

        public void SetTapHandler(ISanctuaryTapHandler handler) => _tapHandler = handler;

        private void Awake() => _rig = GetComponent<SanctuaryCameraRig>();

        private void Update()
        {
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
            if (pressed && !_pressing)
            {
                _pressing = true;
                _dragging = false;
                _pressTime = Time.unscaledTime;
                _pressPosition = _lastPosition = position;
                return;
            }
            if (pressed)
            {
                if (!_dragging && (position - _pressPosition).sqrMagnitude > _tapMaxPixels * _tapMaxPixels)
                    _dragging = true;
                if (_dragging)
                    _rig.PanPixels(position - _lastPosition);
                _lastPosition = position;
                return;
            }
            if (_pressing)
            {
                _pressing = false;
                if (!_dragging && Time.unscaledTime - _pressTime <= _tapMaxSeconds)
                    _tapHandler?.OnTap(_pressPosition);
            }
        }
    }
}
