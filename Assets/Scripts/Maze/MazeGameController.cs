using Maze.Agents;
using Maze.Player;
using Maze.World;
using Minimax;
using Shared;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Maze
{
    /// <summary>
    /// Scene-level state for the maze. Owns the lock-minigame transition
    /// (pause → load TicTacToe additively → resume on its unload) and the
    /// portal-based win flow:
    ///   - Lock <see cref="LockOutcome.Win"/>  → activate the portal.
    ///   - Lock <see cref="LockOutcome.Loss"/> / <see cref="LockOutcome.Draw"/>
    ///     → re-arm the ExitLock so the player can re-enter and try again.
    ///   - Player walks into the active portal → win overlay + R restarts.
    /// </summary>
    public sealed class MazeGameController : MonoBehaviour
    {
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private PlayerHud _playerHud;
        [SerializeField] private FsmAgent _fsmAgent;
        [SerializeField] private BehaviorTreeAgent _btAgent;
        [SerializeField] private ExitLock _exitLock;
        [SerializeField] private Portal _portal;

        public bool IsLockActive { get; private set; }
        public bool IsWon { get; private set; }
        public LockOutcome? LastOutcome => GameState.LastLockOutcome;

        public static MazeGameController Instance { get; private set; }

        private string _bannerMessage;
        private GUIStyle _bannerStyle;
        private GUIStyle _hintStyle;
        private Texture2D _whitePixel;

        private void Awake()
        {
            Instance = this;
            // Auto-resolve unset references — convenient when added at runtime.
            if (_player == null) _player = FindAnyObjectByType<FirstPersonController>();
            if (_playerCamera == null && _player != null) _playerCamera = _player.GetComponentInChildren<Camera>();
            if (_playerHud == null && _player != null) _playerHud = _player.GetComponent<PlayerHud>();
            if (_fsmAgent == null) _fsmAgent = FindAnyObjectByType<FsmAgent>();
            if (_btAgent == null) _btAgent = FindAnyObjectByType<BehaviorTreeAgent>();
            if (_exitLock == null) _exitLock = FindAnyObjectByType<ExitLock>();
            if (_portal == null) _portal = FindAnyObjectByType<Portal>();
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Restart prompt while showing the win overlay.
            if (IsWon && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                GameState.Reset();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        /// <summary>
        /// Pauses the maze and additively loads the TicTacToe scene as a lock
        /// minigame at <paramref name="tier"/>. Idempotent — does nothing if a
        /// lock is already active.
        /// </summary>
        public void EnterLock(LockDifficultyTier tier)
        {
            if (IsLockActive || IsWon) return;
            IsLockActive = true;
            PauseMaze();
            SceneLoader.LoadTicTacToeAdditive(tier);
        }

        public void HandlePortalEntered()
        {
            if (IsWon) return;
            IsWon = true;
            _bannerMessage = "YOU ESCAPED";
            // Stop the player and free the cursor so the prompt is interactable.
            if (_player != null) _player.enabled = false;
            // Disable agents so they can't keep damaging on the win screen.
            if (_fsmAgent != null) _fsmAgent.enabled = false;
            if (_btAgent != null) _btAgent.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name != SceneLoader.TicTacToeSceneName) return;
            IsLockActive = false;
            ResumeMaze();

            // Route the lock outcome.
            LockOutcome? outcome = GameState.LastLockOutcome;
            if (outcome == LockOutcome.Win)
            {
                if (_portal != null) _portal.Activate();
            }
            else
            {
                // Loss or Draw → re-arm the lock so player can retry by walking
                // back into it. Per CLAUDE.md: "Loss = play again at same
                // difficulty, to avoid frustration".
                if (_exitLock != null) _exitLock.ResetTrigger();
            }
        }

        private void PauseMaze()
        {
            if (_fsmAgent != null) _fsmAgent.enabled = false;
            if (_btAgent != null) _btAgent.enabled = false;
            if (_player != null) _player.enabled = false;
            if (_playerCamera != null) _playerCamera.enabled = false;
            if (_playerHud != null) _playerHud.enabled = false;
            // Free the cursor so the menu/buttons inside TTT are interactable.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ResumeMaze()
        {
            if (_fsmAgent != null) _fsmAgent.enabled = true;
            if (_btAgent != null) _btAgent.enabled = true;
            if (_player != null) _player.enabled = true;
            if (_playerCamera != null) _playerCamera.enabled = true;
            if (_playerHud != null) _playerHud.enabled = true;
            // The FirstPersonController will re-lock the cursor on next click.
        }

        // ---- Win overlay (IMGUI) ----

        private void OnGUI()
        {
            if (!IsWon) return;
            EnsureStyles();

            float w = Screen.width;
            float h = Screen.height;

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0.05f, 0.1f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, w, h), _whitePixel);
            GUI.color = prev;

            GUI.Label(new Rect(0, h * 0.35f, w, 90f), _bannerMessage, _bannerStyle);
            GUI.Label(new Rect(0, h * 0.5f, w, 40f), "Press R to restart", _hintStyle);
        }

        private void EnsureStyles()
        {
            if (_whitePixel == null)
            {
                _whitePixel = new Texture2D(1, 1);
                _whitePixel.SetPixel(0, 0, Color.white);
                _whitePixel.Apply();
            }
            if (_bannerStyle == null)
                _bannerStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 64, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.4f, 1f, 0.85f) }
                };
            if (_hintStyle == null)
                _hintStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 22, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
        }
    }
}
