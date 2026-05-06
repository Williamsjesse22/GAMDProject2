using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Maze.Player
{
    /// <summary>
    /// IMGUI HUD for the maze: bottom-left HP bar, top-right pulsing DETECTED
    /// indicator while any agent has line-of-sight, and a death overlay with
    /// a "Press R to restart" prompt that reloads the active scene.
    /// </summary>
    public sealed class PlayerHud : MonoBehaviour
    {
        [SerializeField] private HealthComponent _health;
        [SerializeField] private PlayerAwareness _awareness;
        [SerializeField] private FirstPersonController _fpc;

        [Header("HP bar")]
        [SerializeField] private Vector2 _hpBarSize = new Vector2(260f, 22f);
        [SerializeField] private Vector2 _hpBarMargin = new Vector2(20f, 30f);

        [Header("Detected indicator")]
        [SerializeField] private float _alertPulseSpeed = 6f;

        private Texture2D _whitePixel;
        private GUIStyle _hpLabelStyle;
        private GUIStyle _alertStyle;
        private GUIStyle _deathStyle;
        private GUIStyle _hintStyle;

        private void Awake()
        {
            if (_health == null) _health = GetComponent<HealthComponent>();
            if (_awareness == null) _awareness = GetComponent<PlayerAwareness>();
            if (_fpc == null) _fpc = GetComponent<FirstPersonController>();
        }

        private void OnEnable()
        {
            if (_health != null) _health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnDied -= HandleDied;
        }

        private void Update()
        {
            if (_health != null && _health.IsDead)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb.rKey.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }
        }

        private void HandleDied()
        {
            // Disable player input + free the cursor so the death overlay is interactable.
            if (_fpc != null) _fpc.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_health != null)
            {
                DrawHpBar();
                if (_health.IsDead) DrawDeathOverlay();
            }
            if (_awareness != null && _awareness.IsBeingObserved && _health != null && !_health.IsDead)
                DrawDetectedIndicator();
        }

        private void EnsureStyles()
        {
            if (_whitePixel == null)
            {
                _whitePixel = new Texture2D(1, 1);
                _whitePixel.SetPixel(0, 0, Color.white);
                _whitePixel.Apply();
            }
            if (_hpLabelStyle == null)
                _hpLabelStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 16, fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            if (_alertStyle == null)
                _alertStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 28, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(1f, 0.2f, 0.2f) }
                };
            if (_deathStyle == null)
                _deathStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 56, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.3f, 0.3f) }
                };
            if (_hintStyle == null)
                _hintStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 22, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
        }

        private void DrawHpBar()
        {
            float frac = Mathf.Clamp01(_health.HpFraction);
            float x = _hpBarMargin.x;
            float y = Screen.height - _hpBarMargin.y - _hpBarSize.y;

            var bgRect = new Rect(x, y, _hpBarSize.x, _hpBarSize.y);
            var fillRect = new Rect(x + 2f, y + 2f,
                                    Mathf.Max(0f, (_hpBarSize.x - 4f) * frac),
                                    _hpBarSize.y - 4f);
            var labelRect = new Rect(x, y - 22f, _hpBarSize.x, 20f);

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(bgRect, _whitePixel);
            GUI.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f),
                                   new Color(0.3f, 0.85f, 0.3f), frac);
            GUI.DrawTexture(fillRect, _whitePixel);
            GUI.color = prev;
            GUI.Label(labelRect, $"HP: {_health.CurrentHp} / {_health.MaxHp}", _hpLabelStyle);
        }

        private void DrawDetectedIndicator()
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * _alertPulseSpeed);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, pulse);
            var rect = new Rect(Screen.width - 240f, 20f, 220f, 36f);
            GUI.Label(rect, "● DETECTED", _alertStyle);
            GUI.color = prev;
        }

        private void DrawDeathOverlay()
        {
            float w = Screen.width;
            float h = Screen.height;

            Color prev = GUI.color;
            // Dark backdrop covering the whole screen
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, w, h), _whitePixel);
            GUI.color = prev;

            GUI.Label(new Rect(0, h * 0.35f, w, 80f), "YOU DIED", _deathStyle);
            GUI.Label(new Rect(0, h * 0.5f, w, 40f), "Press R to restart", _hintStyle);
        }
    }
}
