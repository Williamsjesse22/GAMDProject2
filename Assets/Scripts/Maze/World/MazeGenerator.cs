using System;

namespace Maze.World
{
    /// <summary>
    /// Recursive-backtracker maze generator. Produces a "perfect" maze — exactly one
    /// path between any two cells, no loops, fully connected. Seed-driven so the
    /// same seed always produces the same maze (useful for debugging and replay).
    /// </summary>
    public static class MazeGenerator
    {
        public struct Cell
        {
            public bool WallN;
            public bool WallE;
            public bool WallS;
            public bool WallW;
        }

        /// <summary>Generate a <paramref name="width"/>×<paramref name="height"/> grid.</summary>
        public static Cell[,] Generate(int width, int height, int seed)
        {
            if (width < 1 || height < 1)
                throw new ArgumentException("Grid dimensions must be ≥ 1");

            var grid = new Cell[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    grid[x, y] = new Cell { WallN = true, WallE = true, WallS = true, WallW = true };
                }
            }

            var visited = new bool[width, height];
            var rng = new Random(seed);
            Carve(grid, visited, 0, 0, width, height, rng);
            return grid;
        }

        // Direction encoding: 0=N (+y), 1=E (+x), 2=S (-y), 3=W (-x).
        private static void Carve(Cell[,] grid, bool[,] visited,
                                  int x, int y, int w, int h, Random rng)
        {
            visited[x, y] = true;
            int[] dirs = { 0, 1, 2, 3 };
            Shuffle(dirs, rng);

            for (int i = 0; i < dirs.Length; i++)
            {
                int d = dirs[i];
                int nx = x + (d == 1 ? 1 : d == 3 ? -1 : 0);
                int ny = y + (d == 0 ? 1 : d == 2 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                if (visited[nx, ny]) continue;

                switch (d)
                {
                    case 0:
                        grid[x, y].WallN = false;
                        grid[nx, ny].WallS = false;
                        break;
                    case 1:
                        grid[x, y].WallE = false;
                        grid[nx, ny].WallW = false;
                        break;
                    case 2:
                        grid[x, y].WallS = false;
                        grid[nx, ny].WallN = false;
                        break;
                    case 3:
                        grid[x, y].WallW = false;
                        grid[nx, ny].WallE = false;
                        break;
                }
                Carve(grid, visited, nx, ny, w, h, rng);
            }
        }

        private static void Shuffle<T>(T[] arr, Random rng)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }
    }
}
