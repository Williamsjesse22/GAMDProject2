using System.Collections.Generic;
using Maze.Agents;
using Maze.Player;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Maze.World
{
    /// <summary>
    /// Spawns a procedurally-generated maze of corridors and walls, then
    /// repositions the player, exit lock, portal, agents, and pickups onto
    /// walkable cells. NavMesh is rebaked at the end so agents can pathfind.
    /// Calling <see cref="Build"/> again destroys the previous wall set and
    /// regenerates from a new seed — used by the regenerate-on-death flow.
    /// </summary>
    public sealed class MazeBuilder : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("Number of cells along each axis (square grid).")]
        [SerializeField] private int _gridSize = 7;
        [Tooltip("World-space size of one cell.")]
        [SerializeField] private float _cellSize = 4f;

        [Header("Walls")]
        [SerializeField] private float _wallHeight = 3f;
        [SerializeField] private float _wallThickness = 0.4f;
        [SerializeField] private Color _wallColor = new Color(0.5f, 0.5f, 0.55f);
        [Tooltip("Parent transform for spawned walls. Cleared on every Build().")]
        [SerializeField] private Transform _wallParent;

        [Header("References")]
        [SerializeField] private NavMeshSurface _navMeshSurface;
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private ExitLock _exitLock;
        [SerializeField] private Portal _portal;
        [SerializeField] private FsmAgent _fsmAgent;
        [SerializeField] private BehaviorTreeAgent _btAgent;

        public int GridSize => _gridSize;
        public float CellSize => _cellSize;

        // Most-recently-generated grid. Useful for the regenerate-on-death flow.
        public MazeGenerator.Cell[,] CurrentGrid { get; private set; }
        public Vector2Int PlayerCell { get; private set; }
        public Vector2Int LockCell { get; private set; }

        /// <summary>
        /// Generate a maze with <paramref name="seed"/>. <paramref name="playerCell"/>
        /// is where the player goes; if null, defaults to (0, 0). <paramref name="lockCell"/>
        /// is where the gold lock goes; if null, defaults to the cell furthest from
        /// the player (opposite corner).
        /// </summary>
        public void Build(int seed, Vector2Int? playerCell = null, Vector2Int? lockCell = null)
        {
            ClearWalls();

            CurrentGrid = MazeGenerator.Generate(_gridSize, _gridSize, seed);
            PlayerCell = playerCell ?? new Vector2Int(0, 0);
            LockCell = lockCell ?? new Vector2Int(_gridSize - 1, _gridSize - 1);

            SpawnWalls(CurrentGrid);
            RepositionEntities();
            BakeNavMesh();
        }

        private void ClearWalls()
        {
            if (_wallParent == null) return;
            // Use DestroyImmediate even in play mode — Destroy is end-of-frame
            // async, but we need the old walls gone *before* SpawnWalls + the
            // NavMesh rebake see them as obstacles. Plain wall primitives have
            // no destruction-time logic so this is safe.
            for (int i = _wallParent.childCount - 1; i >= 0; i--)
            {
                GameObject child = _wallParent.GetChild(i).gameObject;
                DestroyImmediate(child);
            }
        }

        private void SpawnWalls(MazeGenerator.Cell[,] grid)
        {
            // For interior walls we only spawn the N and E walls of every cell so
            // shared walls aren't double-spawned. Perimeter walls (S of bottom row,
            // W of left column) are spawned explicitly to close the maze.
            for (int x = 0; x < _gridSize; x++)
            {
                for (int y = 0; y < _gridSize; y++)
                {
                    Vector3 center = CellWorldPos(x, y);
                    var c = grid[x, y];

                    if (c.WallN)
                        SpawnWall(center + new Vector3(0f, _wallHeight * 0.5f, _cellSize * 0.5f),
                                  new Vector3(_cellSize + _wallThickness, _wallHeight, _wallThickness));
                    if (c.WallE)
                        SpawnWall(center + new Vector3(_cellSize * 0.5f, _wallHeight * 0.5f, 0f),
                                  new Vector3(_wallThickness, _wallHeight, _cellSize + _wallThickness));
                    if (y == 0 && c.WallS)
                        SpawnWall(center + new Vector3(0f, _wallHeight * 0.5f, -_cellSize * 0.5f),
                                  new Vector3(_cellSize + _wallThickness, _wallHeight, _wallThickness));
                    if (x == 0 && c.WallW)
                        SpawnWall(center + new Vector3(-_cellSize * 0.5f, _wallHeight * 0.5f, 0f),
                                  new Vector3(_wallThickness, _wallHeight, _cellSize + _wallThickness));
                }
            }
        }

        private void SpawnWall(Vector3 worldCenter, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "MazeWall";
            if (_wallParent != null) wall.transform.SetParent(_wallParent, worldPositionStays: true);
            wall.transform.position = worldCenter;
            wall.transform.localScale = size;
            wall.isStatic = true;

            var renderer = wall.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(renderer.sharedMaterial);
                mat.color = _wallColor;
                renderer.sharedMaterial = mat;
            }
        }

        private void RepositionEntities()
        {
            // Player → start cell
            if (_player != null)
            {
                Vector3 worldPos = CellWorldPos(PlayerCell.x, PlayerCell.y) + Vector3.up * 0.05f;
                MovePlayerTo(worldPos);
            }

            // Lock → end cell, with the portal placed just south of it inside the
            // same cell (so the player walks past the lock into the portal once
            // the minigame is won).
            Vector3 lockWorld = CellWorldPos(LockCell.x, LockCell.y);
            if (_exitLock != null) _exitLock.transform.position = lockWorld + new Vector3(0f, 1.5f, 0.6f);
            if (_portal != null) _portal.transform.position = lockWorld + new Vector3(0f, 1.5f, -0.8f);

            // Agents → random cells in the middle of the grid, far from the
            // player so the start isn't an instant ambush.
            var agentCells = PickAgentCells();
            if (_fsmAgent != null && agentCells.Count > 0)
                MoveAgentTo(_fsmAgent.GetComponent<NavMeshAgent>(), CellWorldPos(agentCells[0].x, agentCells[0].y));
            if (_btAgent != null && agentCells.Count > 1)
                MoveAgentTo(_btAgent.GetComponent<NavMeshAgent>(), CellWorldPos(agentCells[1].x, agentCells[1].y));

            // Pickups → random walkable cells (excluding player + lock + agent cells).
            ScatterPickups(agentCells);
        }

        private List<Vector2Int> PickAgentCells()
        {
            // Rough rule: any cell with chebyshev distance ≥ 3 from the player and
            // not the lock cell is a valid agent spawn. Pick up to two distinct ones.
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < _gridSize; x++)
            {
                for (int y = 0; y < _gridSize; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (cell == PlayerCell || cell == LockCell) continue;
                    int dx = Mathf.Abs(cell.x - PlayerCell.x);
                    int dy = Mathf.Abs(cell.y - PlayerCell.y);
                    if (Mathf.Max(dx, dy) < 3) continue;
                    candidates.Add(cell);
                }
            }
            // Shuffle so successive Builds give different agent cells.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            return candidates;
        }

        private void ScatterPickups(List<Vector2Int> agentCells)
        {
            PowerUp[] pickups = FindObjectsByType<PowerUp>(FindObjectsInactive.Exclude);
            if (pickups.Length == 0) return;

            var blocked = new HashSet<Vector2Int> { PlayerCell, LockCell };
            foreach (var c in agentCells) blocked.Add(c);

            var candidates = new List<Vector2Int>();
            for (int x = 0; x < _gridSize; x++)
                for (int y = 0; y < _gridSize; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!blocked.Contains(cell)) candidates.Add(cell);
                }

            // Spread pickups across as many distinct cells as we have candidates.
            for (int i = 0; i < pickups.Length && candidates.Count > 0; i++)
            {
                int idx = Random.Range(0, candidates.Count);
                Vector2Int cell = candidates[idx];
                candidates.RemoveAt(idx);
                pickups[i].transform.position = CellWorldPos(cell.x, cell.y) + new Vector3(0f, 0.6f, 0f);
            }
        }

        private void MovePlayerTo(Vector3 worldPos)
        {
            var cc = _player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _player.transform.position = worldPos;
            if (cc != null) cc.enabled = true;
        }

        private void MoveAgentTo(NavMeshAgent agent, Vector3 worldPos)
        {
            if (agent == null) return;
            // Warp lifts the y so the agent sits on the NavMesh — but the NavMesh
            // hasn't been baked yet at this point, so set the transform directly
            // and rely on Warp after the bake to settle it correctly.
            agent.transform.position = worldPos + Vector3.up * 0.05f;
        }

        private void BakeNavMesh()
        {
            if (_navMeshSurface == null) return;
            _navMeshSurface.BuildNavMesh();

            // Now snap agents onto the freshly-baked NavMesh.
            foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude))
            {
                if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
            }
        }

        public Vector3 CellWorldPos(int x, int y)
        {
            float boardSize = _gridSize * _cellSize;
            float halfBoard = boardSize * 0.5f;
            return new Vector3(
                -halfBoard + (x + 0.5f) * _cellSize,
                0f,
                -halfBoard + (y + 0.5f) * _cellSize);
        }

        /// <summary>Map a world position back to the closest grid cell coordinate.</summary>
        public Vector2Int CellAtWorld(Vector3 worldPos)
        {
            float boardSize = _gridSize * _cellSize;
            float halfBoard = boardSize * 0.5f;
            int x = Mathf.Clamp(Mathf.FloorToInt((worldPos.x + halfBoard) / _cellSize), 0, _gridSize - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((worldPos.z + halfBoard) / _cellSize), 0, _gridSize - 1);
            return new Vector2Int(x, y);
        }
    }
}
