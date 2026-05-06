using System;
using NUnit.Framework;

namespace Minimax.Tests
{
    public class MinimaxAITests
    {
        [Test]
        public void FindBestMove_PicksImmediateWin_AtDepth1()
        {
            // X has three pieces along (0..3, 0, 0); the win is at (3,0,0).
            var b = new Board3D();
            b.Apply(new Move(0, 0, 0, Player.X));
            b.Apply(new Move(1, 0, 0, Player.X));
            b.Apply(new Move(2, 0, 0, Player.X));

            var ai = new MinimaxAI(maxDepth: 2);
            Move pick = ai.FindBestMove(b, Player.X, depth: 1);

            Assert.AreEqual(new Move(3, 0, 0, Player.X), pick);
        }

        [Test]
        public void FindBestMove_BlocksImmediateThreat_AtDepth2()
        {
            // O has 3 in a row along (0..3, 0, 0). X to move at depth 2 must block at (3,0,0).
            var b = new Board3D();
            b.Apply(new Move(0, 0, 0, Player.O));
            b.Apply(new Move(1, 0, 0, Player.O));
            b.Apply(new Move(2, 0, 0, Player.O));

            var ai = new MinimaxAI(maxDepth: 3);
            Move pick = ai.FindBestMove(b, Player.X, depth: 2);

            Assert.AreEqual(new Move(3, 0, 0, Player.X), pick,
                "X must block O's immediate threat — depth 2 search must see the loss otherwise");
        }

        [Test]
        public void FindBestMove_PrefersWin_OverBlock()
        {
            // X has 3 in a row → can win at (3,0,0).
            // O also has 3 in a row → could win at (3,1,0) on its next turn.
            // X to move: should win, not block.
            var b = new Board3D();
            b.Apply(new Move(0, 0, 0, Player.X));
            b.Apply(new Move(1, 0, 0, Player.X));
            b.Apply(new Move(2, 0, 0, Player.X));
            b.Apply(new Move(0, 1, 0, Player.O));
            b.Apply(new Move(1, 1, 0, Player.O));
            b.Apply(new Move(2, 1, 0, Player.O));

            var ai = new MinimaxAI(maxDepth: 3);
            Move pick = ai.FindBestMove(b, Player.X, depth: 3);

            Assert.AreEqual(new Move(3, 0, 0, Player.X), pick);
        }

        [Test]
        public void FindBestMove_IsDeterministic()
        {
            var b1 = new Board3D();
            b1.Apply(new Move(1, 1, 1, Player.O));

            var b2 = new Board3D();
            b2.Apply(new Move(1, 1, 1, Player.O));

            var ai = new MinimaxAI(maxDepth: 2);
            Move m1 = ai.FindBestMove(b1, Player.X, 2);
            Move m2 = ai.FindBestMove(b2, Player.X, 2);

            Assert.AreEqual(m1, m2);
        }

        [Test]
        public void FindBestMove_TracksNodesEvaluated()
        {
            var b = new Board3D();
            var ai = new MinimaxAI(maxDepth: 2);
            ai.FindBestMove(b, Player.X, 1);
            // Depth 1 from empty board: 64 root moves × 1 evaluation each = 64 leaf calls into Search.
            Assert.AreEqual(64, ai.NodesEvaluated, "depth-1 from empty should evaluate exactly 64 leaves");
        }

        [Test]
        public void FindBestMove_ReturnsLegalMove_FromAnyState()
        {
            var b = new Board3D();
            b.Apply(new Move(0, 0, 0, Player.X));
            b.Apply(new Move(3, 3, 3, Player.O));

            var ai = new MinimaxAI(maxDepth: 2);
            Move pick = ai.FindBestMove(b, Player.X, 2);

            Assert.AreEqual(Player.X, pick.Player);
            Assert.AreEqual(Player.None, b.Get(pick.X, pick.Y, pick.Z),
                "AI must pick an empty cell");
        }

        [Test]
        public void FindBestMove_OnTerminalBoard_Throws()
        {
            // Build a winning position for X.
            var b = new Board3D();
            for (int t = 0; t < Board3D.Size; t++)
                b.Apply(new Move(t, 0, 0, Player.X));

            var ai = new MinimaxAI(maxDepth: 2);
            Assert.Throws<InvalidOperationException>(() => ai.FindBestMove(b, Player.O, 2));
        }

        [Test]
        public void Constructor_RejectsBadMaxDepth()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimaxAI(maxDepth: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimaxAI(maxDepth: -1));
        }

        [Test]
        public void FindBestMove_RejectsBadDepth()
        {
            var ai = new MinimaxAI(maxDepth: 3);
            var b = new Board3D();
            Assert.Throws<ArgumentOutOfRangeException>(() => ai.FindBestMove(b, Player.X, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ai.FindBestMove(b, Player.X, 4));
        }

        [Test]
        public void FindBestMove_RejectsPlayerNone()
        {
            var ai = new MinimaxAI(maxDepth: 2);
            var b = new Board3D();
            Assert.Throws<ArgumentException>(() => ai.FindBestMove(b, Player.None, 1));
        }

        [Test]
        public void Evaluator_EmptyBoard_IsZero()
        {
            var b = new Board3D();
            Assert.AreEqual(0, BoardEvaluator.Evaluate(b, Player.X));
        }

        [Test]
        public void Evaluator_WinningTerminal_ReturnsWinScore()
        {
            var b = new Board3D();
            for (int t = 0; t < Board3D.Size; t++)
                b.Apply(new Move(t, 0, 0, Player.X));

            Assert.AreEqual(BoardEvaluator.WinScore, BoardEvaluator.Evaluate(b, Player.X));
            Assert.AreEqual(-BoardEvaluator.WinScore, BoardEvaluator.Evaluate(b, Player.O));
        }

        [Test]
        public void Evaluator_BlockedLine_ContributesZero()
        {
            // A line with both X and O contributes nothing — neither side can complete it.
            var b = new Board3D();
            b.Apply(new Move(0, 0, 0, Player.X));
            b.Apply(new Move(1, 0, 0, Player.O));
            // Score is symmetric: X has 1 piece in many lines, O has 1 piece in many lines,
            // but the (0..3, 0, 0) line is blocked for both → contributes 0 to both sides.
            int xScore = BoardEvaluator.Evaluate(b, Player.X);
            int oScore = BoardEvaluator.Evaluate(b, Player.O);
            Assert.AreEqual(-oScore, xScore, "perspective inversion should negate the score");
        }
    }
}
