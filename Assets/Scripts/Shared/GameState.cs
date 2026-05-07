using Minimax;

namespace Shared
{
    /// <summary>The result of a single lock-minigame attempt.</summary>
    public enum LockOutcome
    {
        Win,
        Loss,
        Draw
    }

    /// <summary>
    /// Static, scene-independent communication channel between the maze (Phase 2)
    /// and the tic-tac-toe lock (Phase 1). The maze sets <see cref="IsLockMode"/>
    /// and <see cref="LockDifficulty"/> before additively loading TicTacToe.unity;
    /// the TTT controller writes <see cref="LastLockOutcome"/> on game end and
    /// asks for its own scene to be unloaded.
    /// </summary>
    public static class GameState
    {
        /// <summary>True while the TTT scene is running as a lock minigame inside the maze.</summary>
        public static bool IsLockMode { get; set; }

        /// <summary>Difficulty tier the lock asked for (Easy/Medium/Hard → depths 1/3/5).</summary>
        public static LockDifficultyTier LockDifficulty { get; set; } = LockDifficultyTier.Medium;

        /// <summary>Result of the most recent lock attempt. Null until TTT writes it.</summary>
        public static LockOutcome? LastLockOutcome { get; set; }

        /// <summary>Reset all fields — useful when leaving the maze entirely.</summary>
        public static void Reset()
        {
            IsLockMode = false;
            LockDifficulty = LockDifficultyTier.Medium;
            LastLockOutcome = null;
        }
    }
}
