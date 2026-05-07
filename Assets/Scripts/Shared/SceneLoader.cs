using Minimax;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shared
{
    /// <summary>
    /// Thin wrapper around additive scene loading for the maze ↔ lock-minigame
    /// transition. Keeps <see cref="GameState"/> in sync as a side effect.
    /// </summary>
    public static class SceneLoader
    {
        public const string TicTacToeSceneName = "TicTacToe";
        public const string MazeSceneName = "Maze";

        /// <summary>
        /// Load the TicTacToe scene additively as a lock minigame at the given
        /// difficulty tier. Sets <see cref="GameState.IsLockMode"/> + clears
        /// <see cref="GameState.LastLockOutcome"/> before the load fires.
        /// </summary>
        public static AsyncOperation LoadTicTacToeAdditive(LockDifficultyTier tier)
        {
            GameState.IsLockMode = true;
            GameState.LockDifficulty = tier;
            GameState.LastLockOutcome = null;
            return SceneManager.LoadSceneAsync(TicTacToeSceneName, LoadSceneMode.Additive);
        }

        /// <summary>
        /// Unload the additively-loaded TicTacToe scene. The caller (TicTacToe
        /// game controller) writes the outcome to <see cref="GameState"/> first.
        /// Returns the AsyncOperation so callers can chain on completion.
        /// </summary>
        public static AsyncOperation UnloadTicTacToe()
        {
            GameState.IsLockMode = false;
            return SceneManager.UnloadSceneAsync(TicTacToeSceneName);
        }
    }
}
