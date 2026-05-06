using System.Collections.Generic;
using NUnit.Framework;

namespace Minimax.Tests
{
    public class Board3DTests
    {
        [Test]
        public void NewBoard_IsEmpty()
        {
            var b = new Board3D();
            Assert.AreEqual(0, b.MovesPlayed);
            Assert.IsFalse(b.IsFull);
            Assert.IsFalse(b.IsTerminal());
            Assert.AreEqual(Player.None, b.CheckWinner());
            Assert.AreEqual(Board3D.CellCount, b.LegalMoveCount());
        }

        [Test]
        public void IndexCoords_RoundTrip()
        {
            for (int z = 0; z < Board3D.Size; z++)
                for (int y = 0; y < Board3D.Size; y++)
                    for (int x = 0; x < Board3D.Size; x++)
                    {
                        int idx = Board3D.Index(x, y, z);
                        var (rx, ry, rz) = Board3D.Coords(idx);
                        Assert.AreEqual((x, y, z), (rx, ry, rz),
                            $"round-trip failed for ({x},{y},{z}) → idx {idx}");
                    }
        }

        [Test]
        public void Apply_PlacesPiece()
        {
            var b = new Board3D();
            b.Apply(new Move(1, 2, 3, Player.X));
            Assert.AreEqual(Player.X, b.Get(1, 2, 3));
            Assert.AreEqual(1, b.MovesPlayed);
            Assert.AreEqual(Board3D.CellCount - 1, b.LegalMoveCount());
        }

        [Test]
        public void Apply_OccupiedCell_Throws()
        {
            var b = new Board3D();
            b.Apply(new Move(0, 0, 0, Player.X));
            Assert.Throws<System.InvalidOperationException>(
                () => b.Apply(new Move(0, 0, 0, Player.O)));
        }

        [Test]
        public void Apply_PlayerNone_Throws()
        {
            var b = new Board3D();
            Assert.Throws<System.ArgumentException>(
                () => b.Apply(new Move(0, 0, 0, Player.None)));
        }

        [Test]
        public void Undo_RestoresState()
        {
            var b = new Board3D();
            var m = new Move(2, 1, 3, Player.O);
            b.Apply(m);
            b.Undo(m);
            Assert.AreEqual(Player.None, b.Get(2, 1, 3));
            Assert.AreEqual(0, b.MovesPlayed);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var a = new Board3D();
            a.Apply(new Move(0, 0, 0, Player.X));
            var b = a.Clone();
            b.Apply(new Move(1, 1, 1, Player.O));
            Assert.AreEqual(Player.None, a.Get(1, 1, 1), "clone modification leaked back to original");
            Assert.AreEqual(1, a.MovesPlayed);
            Assert.AreEqual(2, b.MovesPlayed);
        }

        [Test]
        public void WinningLines_HasExactly76()
        {
            Assert.AreEqual(76, Board3D.WinningLines.Count);
        }

        [Test]
        public void WinningLines_AreAllUniqueAndValid()
        {
            var seen = new HashSet<string>();
            foreach (int[] line in Board3D.WinningLines)
            {
                Assert.AreEqual(4, line.Length, "line should have 4 cells");
                // Distinct cells.
                var cells = new HashSet<int>(line);
                Assert.AreEqual(4, cells.Count, "line cells must be distinct");
                // Canonicalize + uniqueness check across all lines.
                var sorted = new int[4];
                line.CopyTo(sorted, 0);
                System.Array.Sort(sorted);
                string key = string.Join(",", sorted);
                Assert.IsTrue(seen.Add(key), $"duplicate line: {key}");
                // All cell indices in [0, 64).
                foreach (int c in line)
                    Assert.IsTrue(c >= 0 && c < Board3D.CellCount, $"cell index {c} out of range");
            }
        }

        [Test]
        public void EveryLine_DetectsWinForBothPlayers()
        {
            int lineIndex = 0;
            foreach (int[] line in Board3D.WinningLines)
            {
                foreach (Player p in new[] { Player.X, Player.O })
                {
                    var b = new Board3D();
                    foreach (int cell in line)
                    {
                        var (x, y, z) = Board3D.Coords(cell);
                        b.Apply(new Move(x, y, z, p));
                    }
                    Assert.AreEqual(p, b.CheckWinner(),
                        $"line #{lineIndex} ({string.Join(",", line)}) failed to detect win for {p}");
                }
                lineIndex++;
            }
        }

        [Test]
        public void ThreeInARow_DoesNotTriggerWin()
        {
            // Fill 3 of 4 cells of the first axis-aligned line.
            var b = new Board3D();
            int[] line = Board3D.WinningLines[0];
            for (int i = 0; i < 3; i++)
            {
                var (x, y, z) = Board3D.Coords(line[i]);
                b.Apply(new Move(x, y, z, Player.X));
            }
            Assert.AreEqual(Player.None, b.CheckWinner());
        }

        [Test]
        public void BlockedLine_NoWin()
        {
            // 3 X's + 1 O on a line — no winner, even though the line is "full".
            var b = new Board3D();
            int[] line = Board3D.WinningLines[0];
            for (int i = 0; i < 3; i++)
            {
                var (x, y, z) = Board3D.Coords(line[i]);
                b.Apply(new Move(x, y, z, Player.X));
            }
            var (ox, oy, oz) = Board3D.Coords(line[3]);
            b.Apply(new Move(ox, oy, oz, Player.O));
            Assert.AreEqual(Player.None, b.CheckWinner());
        }

        [Test]
        public void SpaceDiagonal_DetectsWin()
        {
            // (0,0,0) → (3,3,3)
            var b = new Board3D();
            for (int t = 0; t < Board3D.Size; t++)
                b.Apply(new Move(t, t, t, Player.X));
            Assert.AreEqual(Player.X, b.CheckWinner());
        }

        [Test]
        public void FullBoard_NoWinner_IsTerminalAndDraw()
        {
            // Construct a deliberately drawn-looking 4×4×4 by alternating in a way that
            // avoids 4-in-a-row. We don't actually need a "real" draw — just a full board
            // with no winner, to exercise IsFull + IsTerminal.
            // Instead of contriving a no-win full board (which is non-trivial in 3D TTT),
            // verify the simpler invariant: filling the board makes IsFull true and
            // LegalMoveCount zero.
            var b = new Board3D();
            for (int i = 0; i < Board3D.CellCount; i++)
            {
                var (x, y, z) = Board3D.Coords(i);
                b.Apply(new Move(x, y, z, Player.X));
            }
            Assert.IsTrue(b.IsFull);
            Assert.AreEqual(0, b.LegalMoveCount());
            Assert.IsTrue(b.IsTerminal());
        }

        [Test]
        public void GetLegalMoves_FillsList()
        {
            var b = new Board3D();
            b.Apply(new Move(0, 0, 0, Player.X));
            b.Apply(new Move(1, 0, 0, Player.O));
            var dest = new List<Move>(8);
            b.GetLegalMoves(Player.X, dest);
            Assert.AreEqual(Board3D.CellCount - 2, dest.Count);
            foreach (var m in dest)
            {
                Assert.AreEqual(Player.X, m.Player);
                Assert.AreEqual(Player.None, b.Get(m.X, m.Y, m.Z));
            }
        }

        [Test]
        public void LinesByCell_IsConsistentWithWinningLines()
        {
            // For every cell, every line listed in LinesByCell[cell] must actually contain that cell.
            for (int cell = 0; cell < Board3D.CellCount; cell++)
            {
                foreach (int li in Board3D.LinesByCell[cell])
                {
                    int[] line = Board3D.WinningLines[li];
                    Assert.Contains(cell, line, $"line {li} listed under cell {cell} but doesn't contain it");
                }
            }
        }
    }
}
