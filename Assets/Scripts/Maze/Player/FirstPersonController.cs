using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Maze.Player
{
    /// <summary>
    /// CharacterController-based first-person controller using the new Input
    /// System (Mouse.current / Keyboard.current). WASD = move, mouse = look,
    /// Space = jump, Esc = release cursor (so the player can interact with the
    /// editor again during testing).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _jumpSpeed = 6.5f;
        [SerializeField] private float _gravity = -20f;

        [Header("Look")]
        [SerializeField] private Camera _camera;
        [Tooltip("Mouse delta multiplier. Lower = slower turn.")]
        [SerializeField] private float _lookSensitivity = 0.12f;
        [SerializeField] private float _maxPitchDegrees = 85f;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursorOnStart = true;

        private CharacterController _cc;
        private float _verticalVelocity;
        private float _pitch;
        private bool _cursorLocked;

        private float _baseMoveSpeed;
        private Coroutine _speedBoostCoroutine;
        private float _speedBoostExpiresAt = -1f;

        /// <summary>True while a speed-boost pickup is currently active.</summary>
        public bool HasActiveSpeedBoost => _speedBoostExpiresAt > Time.time;

        /// <summary>Seconds remaining on the current speed boost; 0 when none.</summary>
        public float SpeedBoostSecondsRemaining =>
            HasActiveSpeedBoost ? Mathf.Max(0f, _speedBoostExpiresAt - Time.time) : 0f;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_camera == null) _camera = GetComponentInChildren<Camera>();
            _baseMoveSpeed = _moveSpeed;
        }

        /// <summary>
        /// Apply a temporary speed multiplier for <paramref name="duration"/> seconds.
        /// Replaces any existing boost rather than stacking, so picking up two boosts
        /// in quick succession refreshes the timer instead of compounding.
        /// </summary>
        public void ApplySpeedBoost(float multiplier, float duration)
        {
            if (_speedBoostCoroutine != null) StopCoroutine(_speedBoostCoroutine);
            _speedBoostCoroutine = StartCoroutine(SpeedBoostCoroutine(multiplier, duration));
        }

        private IEnumerator SpeedBoostCoroutine(float multiplier, float duration)
        {
            _moveSpeed = _baseMoveSpeed * multiplier;
            _speedBoostExpiresAt = Time.time + duration;
            yield return new WaitForSeconds(duration);
            _moveSpeed = _baseMoveSpeed;
            _speedBoostExpiresAt = -1f;
            _speedBoostCoroutine = null;
        }

        private void Start()
        {
            if (_lockCursorOnStart) LockCursor();
        }

        private void Update()
        {
            HandleCursorToggle();
            HandleLook();
            HandleMovement();
        }

        private void HandleCursorToggle()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) ReleaseCursor();
            // Click anywhere in the game view re-locks (handy after Esc).
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !_cursorLocked)
                LockCursor();
        }

        private void HandleLook()
        {
            // Don't rotate while the cursor is free — otherwise the camera spins
            // every time the player clicks back into the game view.
            if (!_cursorLocked) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 delta = mouse.delta.ReadValue() * _lookSensitivity;
            transform.Rotate(0f, delta.x, 0f);

            _pitch = Mathf.Clamp(_pitch - delta.y, -_maxPitchDegrees, _maxPitchDegrees);
            if (_camera != null)
                _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            Vector3 input = ReadMoveInput();
            Vector3 worldMove = transform.TransformDirection(input) * _moveSpeed;

            if (_cc.isGrounded)
            {
                // Small downward push so isGrounded stays true on flat ground;
                // jump if pressed this frame.
                _verticalVelocity = -2f;
                Keyboard kb = Keyboard.current;
                if (kb != null && kb.spaceKey.wasPressedThisFrame)
                    _verticalVelocity = _jumpSpeed;
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }

            worldMove.y = _verticalVelocity;
            _cc.Move(worldMove * Time.deltaTime);
        }

        private static Vector3 ReadMoveInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return Vector3.zero;

            Vector3 v = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.z += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.z -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _cursorLocked = true;
        }

        private void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _cursorLocked = false;
        }

        private void OnApplicationFocus(bool focus)
        {
            // Releasing focus shouldn't keep the cursor trapped.
            if (!focus) ReleaseCursor();
        }
    }
}
