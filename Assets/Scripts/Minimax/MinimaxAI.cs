using System;
using System.Collections.Generic;

namespace Minimax
{
    /// <summary>Which search algorithm <see cref="MinimaxAI"/> uses.</summary>
    public enum SearchStrategy
    {
        /// <summary>Plain depth-limited minimax. Visits every node up to the depth limit.</summary>
        PlainMinimax,
        /// <summary>Alpha-beta pruning. Same answer as plain minimax, never visits more nodes.</summary>
        AlphaBeta
    }

    /// <summary>
    /// Depth-limited minimax search for <see cref="Board3D"/>. Deterministic: ties between
    /// equally-scored moves are broken by flat-index order, so the same position with the
    /// same depth and strategy always returns the same move.
    /// </summary>
    /// <remarks>
    /// Both plain minimax and alpha-beta pruning live in this class — same public surface,
    /// chosen via <see cref="SearchStrategy"/>. Alpha-beta is the default because it's
    /// strictly cheaper and provably returns the same best score.
    /// </remarks>
    public sealed class MinimaxAI
    {
        private readonly List<Move>[] _movePool;
        private readonly int _maxDepth;

        /// <summary>The configured search strategy for this instance.</summary>
        public SearchStrategy Strategy { get; }

        /// <summary>Number of search nodes evaluated by the most recent <see cref="FindBestMove"/> call.</summary>
        public long NodesEvaluated { get; private set; }

        /// <param name="maxDepth">
        /// Largest depth that will ever be passed to <see cref="FindBestMove"/>. Drives the
        /// size of the per-depth move-list pool — pre-allocated once to keep the search hot
        /// path allocation-free.
        /// </param>
        /// <param name="strategy">Plain minimax or alpha-beta pruning. Defaults to alpha-beta.</param>
        public MinimaxAI(int maxDepth = 6, SearchStrategy strategy = SearchStrategy.AlphaBeta)
        {
            if (maxDepth < 1) throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth must be ≥ 1");
            _maxDepth = maxDepth;
            Strategy = strategy;
            _movePool = new List<Move>[maxDepth + 1];
            for (int i = 0; i <= maxDepth; i++)
                _movePool[i] = new List<Move>(Board3D.CellCount);
        }

        /// <summary>
        /// Pick the move that maximizes the minimax score for <paramref name="player"/> at
        /// the given <paramref name="depth"/>.
        /// </summary>
        public Move FindBestMove(Board3D board, Player player, int depth)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (depth < 1) throw new ArgumentOutOfRangeException(nameof(depth), "depth must be ≥ 1");
            if (depth > _maxDepth)
                throw new ArgumentOutOfRangeException(nameof(depth),
                    $"depth {depth} exceeds the MinimaxAI's configured maxDepth {_maxDepth}");
            if (player == Player.None) throw new ArgumentException("Player.None has no moves", nameof(player));
            if (board.IsTerminal()) throw new InvalidOperationException("Board is already terminal");

            NodesEvaluated = 0;

            List<Move> moves = _movePool[depth];
            board.GetLegalMoves(player, moves);
            int n = moves.Count;
            if (n == 0) throw new InvalidOperationException("No legal moves available on this board");

            Move best = moves[0];
            int bestScore = int.MinValue;
            // For alpha-beta, alpha rises as we find better moves at the root; beta stays
            // at +∞ at this level since nothing above us is minimizing.
            int alpha = int.MinValue;
            const int beta = int.MaxValue;

            for (int i = 0; i < n; i++)
            {
                Move m = moves[i];
                board.Apply(m);
                int score = Strategy == SearchStrategy.AlphaBeta
                    ? SearchAlphaBeta(board, depth - 1, alpha, beta, maximizing: false, perspective: player)
                    : Search(board, depth - 1, maximizing: false, perspective: player);
                board.Undo(m);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = m;
                    if (bestScore > alpha) alpha = bestScore;
                }
            }
            return best;
        }

        // ---- Plain minimax ----

        private int Search(Board3D board, int depth, bool maximizing, Player perspective)
        {
            NodesEvaluated++;
            if (depth == 0 || board.IsTerminal())
                return BoardEvaluator.Evaluate(board, perspective);

            Player toMove = maximizing ? perspective : perspective.Opponent();
            List<Move> moves = _movePool[depth];
            board.GetLegalMoves(toMove, moves);
            int n = moves.Count;

            if (maximizing)
            {
                int best = int.MinValue;
                for (int i = 0; i < n; i++)
                {
                    Move m = moves[i];
                    board.Apply(m);
                    int score = Search(board, depth - 1, false, perspective);
                    board.Undo(m);
                    if (score > best) best = score;
                }
                return best;
            }
            else
            {
                int best = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    Move m = moves[i];
                    board.Apply(m);
                    int score = Search(board, depth - 1, true, perspective);
                    board.Undo(m);
                    if (score < best) best = score;
                }
                return best;
            }
        }

        // ---- Alpha-beta pruning ----

        private int SearchAlphaBeta(Board3D board, int depth, int alpha, int beta, bool maximizing, Player perspective)
        {
            NodesEvaluated++;
            if (depth == 0 || board.IsTerminal())
                return BoardEvaluator.Evaluate(board, perspective);

            Player toMove = maximizing ? perspective : perspective.Opponent();
            List<Move> moves = _movePool[depth];
            board.GetLegalMoves(toMove, moves);
            int n = moves.Count;

            if (maximizing)
            {
                int best = int.MinValue;
                for (int i = 0; i < n; i++)
                {
                    Move m = moves[i];
                    board.Apply(m);
                    int score = SearchAlphaBeta(board, depth - 1, alpha, beta, false, perspective);
                    board.Undo(m);
                    if (score > best) best = score;
                    if (best > alpha) alpha = best;
                    if (beta <= alpha) break; // β cutoff: minimizer above us already has a better option
                }
                return best;
            }
            else
            {
                int best = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    Move m = moves[i];
                    board.Apply(m);
                    int score = SearchAlphaBeta(board, depth - 1, alpha, beta, true, perspective);
                    board.Undo(m);
                    if (score < best) best = score;
                    if (best < beta) beta = best;
                    if (beta <= alpha) break; // α cutoff: maximizer above us already has a better option
                }
                return best;
            }
        }
    }
}
