using UnityEngine;
using UnityEngine.InputSystem;

namespace TicTacToe
{
    /// <summary>
    /// Hold Alt (Option on macOS) and move the mouse to orbit the camera around
    /// the tic-tac-toe board — useful for inspecting the 3D arrangement when
    /// pieces in the back layers are occluded by pieces in front. Scroll wheel
    /// zooms in/out. Doesn't fight piece placement: the controller suppresses
    /// click-to-place + the hover ghost while <see cref="IsOrbiting"/> is true.
    /// </summary>
    public sealed class TicTacToeCameraOrbit : MonoBehaviour
    {
        [Tooltip("Point the camera orbits around. Defaults to the Board GameObject if unset.")]
        [SerializeField] private Transform _target;

        [Header("Orbit (Alt + mouse move)")]
        [Tooltip("Degrees of rotation per pixel of mouse delta.")]
        [SerializeField] private float _orbitSpeed = 0.25f;
        [Tooltip("Pitch is clamped to ±this many degrees so the camera can't flip upside down.")]
        [Range(10f, 89f)]
        [SerializeField] private float _maxPitchDegrees = 80f;

        [Header("Zoom (scroll wheel)")]
        [Tooltip("Distance change per scroll-tick. Positive scrolling zooms in.")]
        [SerializeField] private float _zoomSpeed = 0.01f;
        [SerializeField] private float _minDistance = 4f;
        [SerializeField] private float _maxDistance = 25f;

        /// <summary>True on any frame Alt is held — used by the controller to skip click input.</summary>
        public bool IsOrbiting { get; private set; }

        private float _azimuthDegrees;
        private float _elevationDegrees;
        private float _distance;
        private bool _initialized;

        private void Awake()
        {
            if (_target == null)
            {
                GameObject board = GameObject.Find("Board");
                if (board != null) _target = board.transform;
            }
            CaptureCurrentAsInitial();
        }

        private void OnEnable()
        {
            // Re-capture on enable in case the camera was moved between sessions.
            if (_target != null) CaptureCurrentAsInitial();
        }

        private void CaptureCurrentAsInitial()
        {
            if (_target == null) return;
            Vector3 offset = transform.position - _target.position;
            _distance = Mathf.Max(0.01f, offset.magnitude);
            _elevationDegrees = Mathf.Asin(Mathf.Clamp(offset.y / _distance, -1f, 1f)) * Mathf.Rad2Deg;
            // atan2(x, z) gives 0 when looking down +Z, increases as we yaw to the right.
            _azimuthDegrees = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            _initialized = true;
        }

        private void Update()
        {
            if (_target == null) return;
            if (!_initialized) CaptureCurrentAsInitial();

            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            IsOrbiting = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);

            // Orbit while Alt held and the mouse is moving.
            if (IsOrbiting && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0.0001f)
                {
                    _azimuthDegrees += delta.x * _orbitSpeed;
                    _elevationDegrees = Mathf.Clamp(_elevationDegrees - delta.y * _orbitSpeed,
                                                   -_maxPitchDegrees, _maxPitchDegrees);
                    ApplyOrbit();
                }
            }

            // Scroll wheel zoom (always available, even without Alt).
            if (mouse != null)
            {
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    _distance = Mathf.Clamp(_distance - scrollY * _zoomSpeed,
                                            _minDistance, _maxDistance);
                    ApplyOrbit();
                }
            }
        }

        private void ApplyOrbit()
        {
            float az = _azimuthDegrees * Mathf.Deg2Rad;
            float el = _elevationDegrees * Mathf.Deg2Rad;
            float cosEl = Mathf.Cos(el);
            Vector3 offset = new Vector3(
                _distance * cosEl * Mathf.Sin(az),
                _distance * Mathf.Sin(el),
                _distance * cosEl * Mathf.Cos(az));
            transform.position = _target.position + offset;
            transform.LookAt(_target.position, Vector3.up);
        }
    }
}
