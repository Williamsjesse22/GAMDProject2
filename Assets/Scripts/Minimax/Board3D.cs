using System;
using System.Collections.Generic;

namespace Minimax
{
    /// <summary>
    /// Mutable 4×4×4 tic-tac-toe board with O(76) win detection.
    /// Cells are flat-indexed as <c>x + y*4 + z*16</c>.
    /// </summary>
    public sealed class Board3D
    {
        public const int Size = 4;
        public const int CellCount = Size * Size * Size;
        public const int LineCount = 76;

        private static readonly int[][] s_winningLines = ComputeWinningLines();
        private static readonly int[][] s_linesByCell = ComputeLinesByCell();

        /// <summary>Pre-computed winning lines as arrays of 4 cell indices.</summary>
        public static IReadOnlyList<int[]> WinningLines => s_winningLines;

        /// <summary>For each cell index, the indices of winning lines that include it.</summary>
        public static IReadOnlyList<int[]> LinesByCell => s_linesByCell;

        private readonly Player[] _cells = new Player[CellCount];
        private int _movesPlayed;

        public int MovesPlayed => _movesPlayed;
        public bool IsFull => _movesPlayed >= CellCount;

        /// <summary>Convert (x, y, z) to a flat cell index.</summary>
        public static int Index(int x, int y, int z) => x + y * Size + z * Size * Size;

        /// <summary>Decompose a flat cell index back into (x, y, z).</summary>
        public static (int x, int y, int z) Coords(int index) =>
            (index % Size, (index / Size) % Size, index / (Size * Size));

        public Player Get(int x, int y, int z) => _cells[Index(x, y, z)];
        public Player Get(int index) => _cells[index];

        public bool IsLegal(int x, int y, int z) => Get(x, y, z) == Player.None;
        public bool IsLegal(Move m) => IsLegal(m.X, m.Y, m.Z);

        /// <summary>Place <paramref name="m"/> on the board. Throws if the cell is occupied.</summary>
        public void Apply(Move m)
        {
            int idx = Index(m.X, m.Y, m.Z);
            if (_cells[idx] != Player.None)
                throw new InvalidOperationException($"Cell ({m.X},{m.Y},{m.Z}) already occupied by {_cells[idx]}");
            if (m.Player == Player.None)
                throw new ArgumentException("Cannot apply a move with Player.None", nameof(m));
            _cells[idx] = m.Player;
            _movesPlayed++;
        }

        /// <summary>Reverse a previously-applied move. No validation — caller's responsibility.</summary>
        public void Undo(Move m)
        {
            _cells[Index(m.X, m.Y, m.Z)] = Player.None;
            _movesPlayed--;
        }

        /// <summary>Returns the winning <see cref="Player"/> or <see cref="Player.None"/> if no one has won.</summary>
        public Player CheckWinner()
        {
            for (int i = 0; i < s_winningLines.Length; i++)
            {
                int[] line = s_winningLines[i];
                Player first = _cells[line[0]];
                if (first == Player.None) continue;
                if (_cells[line[1]] == first && _cells[line[2]] == first && _cells[line[3]] == first)
                    return first;
            }
            return Player.None;
        }

        /// <summary>True iff someone has won or the board is full.</summary>
        public bool IsTerminal() => CheckWinner() != Player.None || IsFull;

        /// <summary>Enumerate all legal moves for <paramref name="player"/> in flat-index order.</summary>
        public IEnumerable<Move> LegalMoves(Player player)
        {
            for (int i = 0; i < CellCount; i++)
            {
                if (_cells[i] == Player.None)
                {
                    var (x, y, z) = Coords(i);
                    yield return new Move(x, y, z, player);
                }
            }
        }

        /// <summary>
        /// Non-allocating variant: clear <paramref name="dest"/> and refill it with all legal
        /// moves for <paramref name="player"/> in flat-index order. Used by the minimax hot path.
        /// </summary>
        public void GetLegalMoves(Player player, List<Move> dest)
        {
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            dest.Clear();
            for (int i = 0; i < CellCount; i++)
            {
                if (_cells[i] == Player.None)
                {
                    var (x, y, z) = Coords(i);
                    dest.Add(new Move(x, y, z, player));
                }
            }
        }

        /// <summary>Count of empty cells.</summary>
        public int LegalMoveCount() => CellCount - _movesPlayed;

        public Board3D Clone()
        {
            var b = new Board3D();
            Array.Copy(_cells, b._cells, CellCount);
            b._movesPlayed = _movesPlayed;
            return b;
        }

        public void Reset()
        {
            Array.Clear(_cells, 0, CellCount);
            _movesPlayed = 0;
        }

        private static int[][] ComputeWinningLines()
        {
            var lines = new List<int[]>(LineCount);

            // Axis-aligned lines: 16 per axis × 3 axes = 48.
            for (int a = 0; a < Size; a++)
            {
                for (int b = 0; b < Size; b++)
                {
                    int[] xLine = new int[Size];
                    int[] yLine = new int[Size];
                    int[] zLine = new int[Size];
                    for (int t = 0; t < Size; t++)
                    {
                        xLine[t] = Index(t, a, b);
                        yLine[t] = Index(a, t, b);
                        zLine[t] = Index(a, b, t);
                    }
                    lines.Add(xLine);
                    lines.Add(yLine);
                    lines.Add(zLine);
                }
            }

            // Face diagonals: 2 diagonals per plane × 4 planes per axis × 3 axes = 24.
            for (int fixedCoord = 0; fixedCoord < Size; fixedCoord++)
            {
                int[] xyDiag1 = new int[Size];
                int[] xyDiag2 = new int[Size];
                int[] xzDiag1 = new int[Size];
                int[] xzDiag2 = new int[Size];
                int[] yzDiag1 = new int[Size];
                int[] yzDiag2 = new int[Size];
                for (int t = 0; t < Size; t++)
                {
                    xyDiag1[t] = Index(t, t, fixedCoord);
                    xyDiag2[t] = Index(t, Size - 1 - t, fixedCoord);
                    xzDiag1[t] = Index(t, fixedCoord, t);
                    xzDiag2[t] = Index(t, fixedCoord, Size - 1 - t);
                    yzDiag1[t] = Index(fixedCoord, t, t);
                    yzDiag2[t] = Index(fixedCoord, t, Size - 1 - t);
                }
                lines.Add(xyDiag1);
                lines.Add(xyDiag2);
                lines.Add(xzDiag1);
                lines.Add(xzDiag2);
                lines.Add(yzDiag1);
                lines.Add(yzDiag2);
            }

            // Space diagonals: 4.
            int[] sd1 = new int[Size];
            int[] sd2 = new int[Size];
            int[] sd3 = new int[Size];
            int[] sd4 = new int[Size];
            for (int t = 0; t < Size; t++)
            {
                sd1[t] = Index(t, t, t);
                sd2[t] = Index(Size - 1 - t, t, t);
                sd3[t] = Index(t, Size - 1 - t, t);
                sd4[t] = Index(t, t, Size - 1 - t);
            }
            lines.Add(sd1);
            lines.Add(sd2);
            lines.Add(sd3);
            lines.Add(sd4);

            if (lines.Count != LineCount)
                throw new InvalidOperationException($"Expected {LineCount} winning lines, generated {lines.Count}");
            return lines.ToArray();
        }

        private static int[][] ComputeLinesByCell()
        {
            var perCell = new List<int>[CellCount];
            for (int i = 0; i < CellCount; i++) perCell[i] = new List<int>();
            for (int li = 0; li < s_winningLines.Length; li++)
            {
                int[] line = s_winningLines[li];
                for (int j = 0; j < line.Length; j++)
                    perCell[line[j]].Add(li);
            }
            var arr = new int[CellCount][];
            for (int i = 0; i < CellCount; i++) arr[i] = perCell[i].ToArray();
            return arr;
        }
    }
}
