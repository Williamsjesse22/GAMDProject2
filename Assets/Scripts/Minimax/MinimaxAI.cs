using System;
using System.Collections.Generic;

namespace Minimax
{
    /// <summary>
    /// Depth-limited minimax search for <see cref="Board3D"/>. Deterministic: ties between
    /// equally-scored moves are broken by flat-index order, so the same position with the
    /// same depth always returns the same move.
    /// </summary>
    /// <remarks>
    /// Slice 1 ships plain minimax. Alpha-beta pruning is added in slice 2 with the same
    /// public surface so callers don't change.
    /// </remarks>
    public sealed class MinimaxAI
    {
        private readonly List<Move>[] _movePool;
        private readonly int _maxDepth;

        /// <summary>Number of search nodes evaluated by the most recent <see cref="FindBestMove"/> call.</summary>
        public long NodesEvaluated { get; private set; }

        /// <param name="maxDepth">
        /// Largest depth that will ever be passed to <see cref="FindBestMove"/>. Drives the
        /// size of the per-depth move-list pool — pre-allocated once to keep the search hot
        /// path allocation-free.
        /// </param>
        public MinimaxAI(int maxDepth = 6)
        {
            if (maxDepth < 1) throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth must be ≥ 1");
            _maxDepth = maxDepth;
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
            for (int i = 0; i < n; i++)
            {
                Move m = moves[i];
                board.Apply(m);
                int score = Search(board, depth - 1, maximizing: false, perspective: player);
                board.Undo(m);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = m;
                }
            }
            return best;
        }

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
    }
}
