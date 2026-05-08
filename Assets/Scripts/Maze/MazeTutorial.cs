using Maze.Agents;
using Maze.Player;
using Shared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Maze
{
    /// <summary>
    /// Pre-game tutorial overlay shown on the first maze level. Pauses the
    /// world (disables player + agents, frees the cursor) and renders an
    /// IMGUI panel explaining the controls and what the colored objects do.
    /// Player presses Space or Enter to dismiss; the overlay then re-enables
    /// everything and the maze resumes normally.
    /// </summary>
    public sealed class MazeTutorial : MonoBehaviour
    {
        [Tooltip("If true, the tutorial only shows when GameState.MazeLevel == 1.")]
        [SerializeField] private bool _firstLevelOnly = true;

        [SerializeField] private FirstPersonController _player;
        [SerializeField] private FsmAgent _fsmAgent;
        [SerializeField] private BehaviorTreeAgent _btAgent;

        private bool _shown;
        private Texture2D _whitePixel;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;

        private void Awake()
        {
            if (_player == null) _player = FindAnyObjectByType<FirstPersonController>();
            if (_fsmAgent == null) _fsmAgent = FindAnyObjectByType<FsmAgent>();
            if (_btAgent == null) _btAgent = FindAnyObjectByType<BehaviorTreeAgent>();
        }

        private void Start()
        {
            if (_firstLevelOnly && GameState.MazeLevel != 1)
            {
                enabled = false;
                return;
            }
            Show();
        }

        private void Update()
        {
            if (!_shown) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                Hide();
        }

        private void Show()
        {
            _shown = true;
            if (_player != null) _player.enabled = false;
            if (_fsmAgent != null) _fsmAgent.enabled = false;
            if (_btAgent != null) _btAgent.enabled = false;
            // Free the cursor so the panel is visible/usable while paused.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Hide()
        {
            _shown = false;
            if (_player != null) _player.enabled = true;
            if (_fsmAgent != null) _fsmAgent.enabled = true;
            if (_btAgent != null) _btAgent.enabled = true;
            // FirstPersonController re-locks the cursor on its next mouse click.
        }

        private void OnGUI()
        {
            if (!_shown) return;
            EnsureStyles();

            float w = Screen.width;
            float h = Screen.height;

            // Dim the world so the panel pops.
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0.05f, 0.1f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, w, h), _whitePixel);
            GUI.color = prev;

            const float panelW = 580f;
            const float panelH = 480f;
            var panelRect = new Rect((w - panelW) * 0.5f, (h - panelH) * 0.5f, panelW, panelH);
            GUI.Box(panelRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(panelRect.x + 30, panelRect.y + 20,
                                          panelRect.width - 60, panelRect.height - 40));
            GUILayout.Label("HOW TO PLAY", _titleStyle);
            GUILayout.Space(14);

            GUILayout.Label("CONTROLS", _sectionStyle);
            GUILayout.Label("WASD / arrows  =  move", _bodyStyle);
            GUILayout.Label("Mouse  =  look", _bodyStyle);
            GUILayout.Label("Space  =  jump   ·   Esc  =  release cursor", _bodyStyle);
            GUILayout.Space(14);

            GUILayout.Label("WHAT YOU'LL SEE", _sectionStyle);
            GUILayout.Label("●  Green box   —  health pack  (+30 HP)", _bodyStyle);
            GUILayout.Label("●  Cyan box    —  speed boost  (1.8× for 6s)", _bodyStyle);
            GUILayout.Label("●  Gold cube   —  exit lock  (triggers tic-tac-toe minigame)", _bodyStyle);
            GUILayout.Label("●  Cyan sphere —  portal  (appears after you win the lock)", _bodyStyle);
            GUILayout.Label("●  Red capsule —  brute  (chases, melee aura, fires projectiles)", _bodyStyle);
            GUILayout.Label("●  Blue capsule —  sniper  (cautious; kites you at range)", _bodyStyle);
            GUILayout.Space(14);

            GUILayout.Label("GOAL", _sectionStyle);
            GUILayout.Label("Reach the gold lock, win the tic-tac-toe minigame,", _bodyStyle);
            GUILayout.Label("then step into the portal that appears.", _bodyStyle);
            GUILayout.Label($"Clear all {GameState.MaxMazeLevels} levels to escape for real.", _bodyStyle);
            GUILayout.FlexibleSpace();

            GUILayout.Label("Press  SPACE  or  ENTER  to begin", _hintStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_whitePixel == null)
            {
                _whitePixel = new Texture2D(1, 1);
                _whitePixel.SetPixel(0, 0, Color.white);
                _whitePixel.Apply();
            }
            if (_titleStyle == null)
                _titleStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 32, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.95f, 0.85f, 0.4f) }
                };
            if (_sectionStyle == null)
                _sectionStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 18, fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.6f, 0.95f, 1f) }
                };
            if (_bodyStyle == null)
                _bodyStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 15,
                    normal = { textColor = new Color(0.92f, 0.92f, 0.92f) }
                };
            if (_hintStyle == null)
                _hintStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 18, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.85f) }
                };
        }
    }
}
