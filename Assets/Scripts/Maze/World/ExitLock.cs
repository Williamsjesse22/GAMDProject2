using Minimax;
using UnityEngine;

namespace Maze.World
{
    /// <summary>
    /// Trigger volume placed in front of the maze exit. When the player
    /// enters, asks <see cref="MazeGameController"/> to launch the
    /// tic-tac-toe lock minigame at the configured difficulty tier.
    /// One-shot until the lock resolves to avoid double-firing on re-entry.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ExitLock : MonoBehaviour
    {
        [SerializeField] private LockDifficultyTier _difficulty = LockDifficultyTier.Medium;
        [Tooltip("Tag the trigger uses to identify the player.")]
        [SerializeField] private string _playerTag = "Player";

        private bool _triggered;

        private void Reset()
        {
            // Make sure the collider is configured as a trigger when this
            // component is first added in the editor.
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (!other.CompareTag(_playerTag)) return;
            if (MazeGameController.Instance == null) return;
            if (MazeGameController.Instance.IsLockActive) return;

            _triggered = true;
            MazeGameController.Instance.EnterLock(_difficulty);
        }

        // Expose a way for slice 11 to re-arm the lock after a loss/draw so the
        // player can try again by re-entering.
        public void ResetTrigger() => _triggered = false;
    }
}
