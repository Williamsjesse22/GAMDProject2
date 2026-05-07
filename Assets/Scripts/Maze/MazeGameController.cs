using Maze.Agents;
using Maze.Player;
using Minimax;
using Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maze
{
    /// <summary>
    /// Scene-level state for the maze. In slice 10 it owns the lock-minigame
    /// transition: pause the maze, additively load TicTacToe.unity, and resume
    /// when TicTacToe unloads itself with a result. Slice 11 will use that
    /// result to unlock the portal.
    /// </summary>
    public sealed class MazeGameController : MonoBehaviour
    {
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private PlayerHud _playerHud;
        [SerializeField] private FsmAgent _fsmAgent;
        [SerializeField] private BehaviorTreeAgent _btAgent;

        public bool IsLockActive { get; private set; }
        public LockOutcome? LastOutcome => GameState.LastLockOutcome;

        public static MazeGameController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            // Auto-resolve unset references — convenient when added at runtime.
            if (_player == null) _player = FindAnyObjectByType<FirstPersonController>();
            if (_playerCamera == null && _player != null) _playerCamera = _player.GetComponentInChildren<Camera>();
            if (_playerHud == null && _player != null) _playerHud = _player.GetComponent<PlayerHud>();
            if (_fsmAgent == null) _fsmAgent = FindAnyObjectByType<FsmAgent>();
            if (_btAgent == null) _btAgent = FindAnyObjectByType<BehaviorTreeAgent>();
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

        /// <summary>
        /// Pauses the maze and additively loads the TicTacToe scene as a lock
        /// minigame at <paramref name="tier"/>. Idempotent — does nothing if a
        /// lock is already active.
        /// </summary>
        public void EnterLock(LockDifficultyTier tier)
        {
            if (IsLockActive) return;
            IsLockActive = true;
            PauseMaze();
            SceneLoader.LoadTicTacToeAdditive(tier);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name != SceneLoader.TicTacToeSceneName) return;
            // TTT wrote GameState.LastLockOutcome before unloading; resume the maze.
            IsLockActive = false;
            ResumeMaze();
            // Slice 11 will read GameState.LastLockOutcome here and open the portal.
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
    }
}
