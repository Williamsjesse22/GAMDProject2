namespace Minimax
{
    /// <summary>
    /// Five difficulty levels mapping directly to minimax search depth.
    /// </summary>
    public enum DifficultyLevel
    {
        Easy = 1,
        EasyMedium = 2,
        Medium = 3,
        Hard = 4,
        Expert = 5
    }

    public static class DifficultyLevelExtensions
    {
        /// <summary>The search depth a difficulty corresponds to.</summary>
        public static int ToDepth(this DifficultyLevel level) => (int)level;

        /// <summary>
        /// Phase-2 lock minigame uses 3 levels (Easy / Medium / Hard) which map to
        /// underlying depths 1 / 3 / 5 respectively.
        /// </summary>
        public static DifficultyLevel FromLockTier(LockDifficultyTier tier) => tier switch
        {
            LockDifficultyTier.Easy => DifficultyLevel.Easy,
            LockDifficultyTier.Medium => DifficultyLevel.Medium,
            LockDifficultyTier.Hard => DifficultyLevel.Expert,
            _ => DifficultyLevel.Medium
        };
    }

    /// <summary>Three-tier difficulty exposed by the maze exit-lock minigame.</summary>
    public enum LockDifficultyTier
    {
        Easy,
        Medium,
        Hard
    }
}
