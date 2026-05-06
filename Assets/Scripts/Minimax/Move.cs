using System;

namespace Minimax
{
    /// <summary>
    /// A single placement on the 4×4×4 board. Coordinates are zero-indexed in [0, 3].
    /// </summary>
    public readonly struct Move : IEquatable<Move>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;
        public readonly Player Player;

        public Move(int x, int y, int z, Player player)
        {
            X = x;
            Y = y;
            Z = z;
            Player = player;
        }

        public bool Equals(Move other) =>
            X == other.X && Y == other.Y && Z == other.Z && Player == other.Player;

        public override bool Equals(object obj) => obj is Move m && Equals(m);

        public override int GetHashCode() =>
            ((X * 397) ^ Y) * 397 ^ (Z * 397) ^ (int)Player;

        public override string ToString() => $"{Player}@({X},{Y},{Z})";

        public static bool operator ==(Move a, Move b) => a.Equals(b);
        public static bool operator !=(Move a, Move b) => !a.Equals(b);
    }
}
