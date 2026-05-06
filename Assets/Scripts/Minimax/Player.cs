namespace Minimax
{
    /// <summary>
    /// Identifies the owner of a board cell or the side to move.
    /// </summary>
    public enum Player : byte
    {
        None = 0,
        X = 1,
        O = 2
    }

    public static class PlayerExtensions
    {
        /// <summary>Returns the opposing player. <see cref="Player.None"/> maps to itself.</summary>
        public static Player Opponent(this Player player) => player switch
        {
            Player.X => Player.O,
            Player.O => Player.X,
            _ => Player.None
        };
    }
}
