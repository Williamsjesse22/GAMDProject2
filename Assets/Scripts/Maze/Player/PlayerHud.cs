using Shared;
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

        [Header("Damage feedback")]
        [SerializeField] private float _damageFlashSeconds = 0.25f;
        [SerializeField] private Color _damageFlashColor = new Color(1f, 0.1f, 0.1f, 0.45f);
        [Tooltip("HP fraction below which the low-HP red vignette starts pulsing.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowHpThreshold = 0.3f;
        [SerializeField] private float _lowHpVignettePulseSpeed = 1.6f;

        private Texture2D _whitePixel;
        private Texture2D _vignetteEdge;
        private GUIStyle _hpLabelStyle;
        private GUIStyle _alertStyle;
        private GUIStyle _deathStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _levelLabelStyle;

        private float _damageFlashTimer;
        private int _prevHp;

        private void Awake()
        {
            if (_health == null) _health = GetComponent<HealthComponent>();
            if (_awareness == null) _awareness = GetComponent<PlayerAwareness>();
            if (_fpc == null) _fpc = GetComponent<FirstPersonController>();
            if (_health != null) _prevHp = _health.CurrentHp;
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDied += HandleDied;
                _health.OnHealthChanged += HandleHpChanged;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDied -= HandleDied;
                _health.OnHealthChanged -= HandleHpChanged;
            }
        }

        private void HandleHpChanged(int current, int max)
        {
            if (current < _prevHp) _damageFlashTimer = _damageFlashSeconds;
            _prevHp = current;
        }

        private void Update()
        {
            if (_damageFlashTimer > 0f) _damageFlashTimer -= Time.deltaTime;

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

            // Background feedback layers go first so HP/labels/etc. draw on top.
            if (_health != null && !_health.IsDead)
            {
                if (_health.HpFraction < _lowHpThreshold) DrawLowHpVignette();
                if (_damageFlashTimer > 0f) DrawDamageFlash();
            }

            if (_health != null)
            {
                DrawHpBar();
                if (_health.IsDead) DrawDeathOverlay();
            }
            if (_awareness != null && _awareness.IsBeingObserved && _health != null && !_health.IsDead)
                DrawDetectedIndicator();

            DrawLevelLabel();
        }

        private void DrawLevelLabel()
        {
            if (_levelLabelStyle == null)
                _levelLabelStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 18, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.95f, 0.85f, 0.4f) }
                };
            string label = $"Level {GameState.MazeLevel} / {GameState.MaxMazeLevels}";
            var rect = new Rect((Screen.width - 200f) * 0.5f, 12f, 200f, 28f);
            // Drop shadow for readability.
            Color prev = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.65f);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), label, _levelLabelStyle);
            GUI.color = prev;
            GUI.Label(rect, label, _levelLabelStyle);
        }

        private void DrawDamageFlash()
        {
            float t = Mathf.Clamp01(_damageFlashTimer / _damageFlashSeconds);
            Color c = _damageFlashColor;
            c.a *= t; // fade out over the timer
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whitePixel);
            GUI.color = prev;
        }

        private void DrawLowHpVignette()
        {
            // Pulse intensity using HpFraction → smaller fraction = stronger pulse.
            float severity = 1f - Mathf.Clamp01(_health.HpFraction / _lowHpThreshold);
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * _lowHpVignettePulseSpeed);
            float alpha = 0.18f + 0.32f * severity * pulse;

            // Four edge bands tinted red — cheap "vignette" without a custom shader.
            Color c = new Color(1f, 0.05f, 0.05f, alpha);
            Color prev = GUI.color;
            GUI.color = c;
            float w = Screen.width;
            float h = Screen.height;
            float band = Mathf.Max(40f, h * 0.12f);
            GUI.DrawTexture(new Rect(0, 0, w, band), _whitePixel);          // top
            GUI.DrawTexture(new Rect(0, h - band, w, band), _whitePixel);   // bottom
            GUI.DrawTexture(new Rect(0, band, band, h - band * 2f), _whitePixel);          // left
            GUI.DrawTexture(new Rect(w - band, band, band, h - band * 2f), _whitePixel);   // right
            GUI.color = prev;
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
