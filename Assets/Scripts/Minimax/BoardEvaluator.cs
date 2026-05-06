namespace Minimax
{
    /// <summary>
    /// Heuristic scoring for non-terminal positions, evaluated from a fixed perspective.
    /// </summary>
    public static class BoardEvaluator
    {
        /// <summary>Score returned for a winning terminal position (negated for a loss).</summary>
        public const int WinScore = 100_000;

        /// <summary>
        /// Score the position from <paramref name="perspective"/>'s point of view.
        /// Positive favors perspective; negative favors opponent.
        /// </summary>
        public static int Evaluate(Board3D board, Player perspective)
        {
            Player winner = board.CheckWinner();
            if (winner == perspective) return WinScore;
            if (winner != Player.None) return -WinScore;

            Player opponent = perspective.Opponent();
            int score = 0;
            var lines = Board3D.WinningLines;
            for (int i = 0; i < lines.Count; i++)
            {
                int[] line = lines[i];
                int own = 0, opp = 0;
                for (int j = 0; j < line.Length; j++)
                {
                    Player p = board.Get(line[j]);
                    if (p == perspective) own++;
                    else if (p == opponent) opp++;
                }
                // Lines with both colors are dead — neither side can complete them.
                if (own > 0 && opp > 0) continue;
                if (own > 0) score += LineValue(own);
                else if (opp > 0) score -= LineValue(opp);
            }
            return score;
        }

        private static int LineValue(int count) => count switch
        {
            1 => 1,
            2 => 10,
            3 => 100,
            4 => WinScore,
            _ => 0
        };
    }
}
