using UnityEngine;

namespace TicTacToe
{
    /// <summary>
    /// Lightweight tag attached to each runtime-spawned cell cube. Carries the
    /// (x, y, z) board coordinates so the controller's raycast hit can map back
    /// to a board position without string parsing.
    /// </summary>
    public sealed class CellInteractor : MonoBehaviour
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }

        public void Init(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
