using UnityEngine;

namespace PocketWilds.Presentation
{
    public enum RefugeCameraState : byte
    {
        DefaultWorld = 0,
        FocusAnimal = 1,
        HatchReveal = 2,
    }

    /// <summary>
    /// PF_CameraRig_Refuge. Portrait diorama view: fixed pitch, yaw and FOV (no dynamic FOV, no rotation),
    /// pan clamped to the refuge bounds with a fling that glides on after release, pinch zoom between strict
    /// limits, short smoothed transitions between states. One LateUpdate for the whole camera; no per-frame allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RefugeCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Header("Framing")]
        [SerializeField, Range(20f, 70f)] private float _pitch = 40f;
        [SerializeField] private float _yaw;
        [SerializeField, Range(15f, 60f)] private float _fieldOfView = 30f;

        [Header("Zoom (distance to the focus point, m)")]
        [SerializeField] private float _defaultDistance = 13.8f;
        [SerializeField] private float _minDistance = 9.5f;
        [SerializeField] private float _maxDistance = 17f;
        [SerializeField] private float _focusAnimalDistance = 8.5f;
        [SerializeField] private float _hatchDistance = 9.5f;

        [Header("Pan bounds (focus point, world XZ)")]
        [SerializeField] private Vector3 _boundsCenter;
        [SerializeField] private Vector2 _boundsHalfSize = new Vector2(3.5f, 4f);

        [Header("Motion")]
        [SerializeField, Range(0.02f, 0.5f)] private float _smoothTime = 0.1f;
        [SerializeField, Range(0.1f, 0.8f)] private float _transitionTime = 0.35f;

        [Header("Fling (the view keeps the finger's speed after release, then slows down)")]
        [SerializeField, Range(0.1f, 1.5f)] private float _flingTimeConstant = 0.6f;
        [SerializeField, Min(1f)] private float _maxFlingSpeed = 45f;
        [SerializeField, Min(0.01f)] private float _flingStopSpeed = 0.08f;

        private Vector3 _focus;
        private Vector3 _targetFocus;
        private Vector3 _focusVelocity;
        private float _distance;
        private float _targetDistance;
        private float _distanceVelocity;
        private float _worldDistance;
        private Vector3 _worldFocus;
        private Vector3 _flingVelocity;
        private float _currentSmooth;
        private bool _initialized;

        public Camera Camera => _camera;
        public float DefaultDistance => _defaultDistance;
        public RefugeCameraState State { get; private set; }
        public Vector3 Focus => _focus;
        public float Distance => _distance;
        public float MinDistance => _minDistance;
        public float MaxDistance => _maxDistance;

        /// <summary>The view still glides after a fling (m/s on the ground; zero once stopped).</summary>
        public Vector3 FlingVelocity => _flingVelocity;

        /// <summary>Editor/prefab builder hook.</summary>
        public void SetCamera(Camera camera) => _camera = camera;

        public void SetBounds(Vector3 center, Vector2 halfSize)
        {
            _boundsCenter = center;
            _boundsHalfSize = halfSize;
            _targetFocus = Clamp(_targetFocus);
        }

        /// <summary>Places the camera at once (scene entry). Leaves any focus state.</summary>
        public void SnapTo(Vector3 focus, float distance = -1f)
        {
            Initialize();
            State = RefugeCameraState.DefaultWorld;
            _focus = _targetFocus = Clamp(focus);
            _distance = _targetDistance = Mathf.Clamp(distance > 0f ? distance : _defaultDistance, _minDistance, _maxDistance);
            _focusVelocity = Vector3.zero;
            _distanceVelocity = 0f;
            _flingVelocity = Vector3.zero;
            ApplyTransform();
        }

        /// <summary>Drag in pixels: the ground follows the finger.</summary>
        public void PanPixels(Vector2 pixelDelta)
        {
            if (State != RefugeCameraState.DefaultWorld || _camera == null)
                return;
            _targetFocus = Clamp(_targetFocus + PixelsToGround(pixelDelta));
            _currentSmooth = _smoothTime;
        }

        /// <summary>
        /// The finger left the screen while moving (pixels per second): the view keeps that speed and slows down
        /// smoothly, so a quick swipe crosses the refuge without dragging all the way.
        /// </summary>
        public void Fling(Vector2 pixelsPerSecond)
        {
            if (State != RefugeCameraState.DefaultWorld || _camera == null)
                return;
            _flingVelocity = Vector3.ClampMagnitude(PixelsToGround(pixelsPerSecond), _maxFlingSpeed);
            if (_flingVelocity.magnitude < _flingStopSpeed)
                _flingVelocity = Vector3.zero;
        }

        /// <summary>A new touch catches the view where it is.</summary>
        public void StopFling() => _flingVelocity = Vector3.zero;

        /// <summary>Ground motion (m) that keeps the ground under a finger moving by this many pixels.</summary>
        private Vector3 PixelsToGround(Vector2 pixels)
        {
            float worldPerPixel = 2f * _distance * Mathf.Tan(_fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, _camera.pixelHeight);
            var right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            var forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            // Vertical drag moves along the ground: compensate the pitch foreshortening.
            float depthScale = 1f / Mathf.Max(0.3f, Mathf.Sin(_pitch * Mathf.Deg2Rad));
            return -right * (pixels.x * worldPerPixel) - forward * (pixels.y * worldPerPixel * depthScale);
        }

        /// <summary>factor &lt; 1 zooms in, &gt; 1 zooms out (pinch ratio or scroll).</summary>
        public void Zoom(float factor)
        {
            if (State != RefugeCameraState.DefaultWorld || factor <= 0f)
                return;
            _targetDistance = Mathf.Clamp(_targetDistance * factor, _minDistance, _maxDistance);
            _currentSmooth = _smoothTime;
        }

        /// <summary>Short framing on an animal or the hatching egg. ReturnToWorld restores the previous view.</summary>
        public void FocusOn(Vector3 point, RefugeCameraState state) =>
            FocusOn(point, state, state == RefugeCameraState.HatchReveal ? _hatchDistance : _focusAnimalDistance);

        /// <summary>T35B: the same framing at a chosen distance (a group of ten eggs needs the wider view).</summary>
        public void FocusOn(Vector3 point, RefugeCameraState state, float distance)
        {
            if (State == RefugeCameraState.DefaultWorld)
            {
                _worldFocus = _targetFocus;
                _worldDistance = _targetDistance;
            }
            _flingVelocity = Vector3.zero;
            State = state;
            _targetFocus = point;
            _targetDistance = Mathf.Clamp(distance, _minDistance, _maxDistance);
            _currentSmooth = _transitionTime * 0.4f;
        }

        /// <summary>T35A: glides the world view to a point (the next zone's sign), within the bounds.</summary>
        public void GlideTo(Vector3 point)
        {
            if (State != RefugeCameraState.DefaultWorld)
                return;
            _flingVelocity = Vector3.zero;
            _targetFocus = Clamp(new Vector3(point.x, _targetFocus.y, point.z));
            _currentSmooth = _transitionTime * 1.2f;
        }

        public void ReturnToWorld()
        {
            if (State == RefugeCameraState.DefaultWorld)
                return;
            State = RefugeCameraState.DefaultWorld;
            _targetFocus = Clamp(_worldFocus);
            _targetDistance = _worldDistance;
            _currentSmooth = _transitionTime * 0.4f;
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            if (_camera == null)
                _camera = GetComponentInChildren<Camera>(true);
            if (_camera != null)
                _camera.fieldOfView = _fieldOfView;
            _focus = _targetFocus = Clamp(_boundsCenter);
            _distance = _targetDistance = _defaultDistance;
            _worldFocus = _focus;
            _worldDistance = _distance;
            _currentSmooth = _smoothTime;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (_flingVelocity != Vector3.zero)
                Glide(dt);
            _focus = Vector3.SmoothDamp(_focus, _targetFocus, ref _focusVelocity, _currentSmooth, Mathf.Infinity, dt);
            _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _distanceVelocity, _currentSmooth, Mathf.Infinity, dt);
            ApplyTransform();
        }

        /// <summary>One frame of a fling: exponential slow-down; an edge of the bounds stops the motion along it.</summary>
        private void Glide(float dt)
        {
            if (State != RefugeCameraState.DefaultWorld)
            {
                _flingVelocity = Vector3.zero;
                return;
            }
            var wanted = _targetFocus + _flingVelocity * dt;
            var next = Clamp(wanted);
            if (!Mathf.Approximately(next.x, wanted.x))
                _flingVelocity.x = 0f;
            if (!Mathf.Approximately(next.z, wanted.z))
                _flingVelocity.z = 0f;
            _targetFocus = next;
            _currentSmooth = _smoothTime;
            _flingVelocity *= Mathf.Exp(-dt / _flingTimeConstant);
            if (_flingVelocity.sqrMagnitude < _flingStopSpeed * _flingStopSpeed)
                _flingVelocity = Vector3.zero;
        }

        private void ApplyTransform()
        {
            if (_camera == null)
                return;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _distance, rotation);
        }

        private Vector3 Clamp(Vector3 point)
        {
            point.x = Mathf.Clamp(point.x, _boundsCenter.x - _boundsHalfSize.x, _boundsCenter.x + _boundsHalfSize.x);
            point.z = Mathf.Clamp(point.z, _boundsCenter.z - _boundsHalfSize.y, _boundsCenter.z + _boundsHalfSize.y);
            point.y = _boundsCenter.y;
            return point;
        }
    }
}
