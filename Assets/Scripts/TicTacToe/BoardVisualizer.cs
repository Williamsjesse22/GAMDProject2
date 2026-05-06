using Minimax;
using UnityEngine;

namespace TicTacToe
{
    /// <summary>
    /// Spawns the 4×4×4 cell grid at runtime and places X/O markers when cells
    /// are claimed. World layout: board.x → world.x, board.y → world.z (depth),
    /// board.z → world.y (vertical layer), so the four 4×4 layers stack upward.
    /// </summary>
    public sealed class BoardVisualizer : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float _cellSpacing = 1.5f;
        [SerializeField] private float _layerSpacing = 1.8f;
        // Cells render as small marker dots so pieces (rendered larger) sit visibly
        // around them. The collider is grown back up to ~unit world-space below so
        // the click target still covers the full cell volume.
        [SerializeField] private float _cellScale = 0.18f;
        [SerializeField] private float _pieceScale = 0.55f;

        [Header("Colors")]
        [SerializeField] private Color _cellColor = new Color(0.55f, 0.55f, 0.6f);
        [SerializeField] private Color _xColor = new Color(0.85f, 0.25f, 0.25f);
        [SerializeField] private Color _oColor = new Color(0.25f, 0.45f, 0.85f);
        [SerializeField] private Color _winningHighlightColor = new Color(1f, 0.85f, 0.2f);

        [Header("Last-move highlight")]
        [Tooltip("Multiplier applied to the most recently placed piece's scale.")]
        [SerializeField] private float _lastMoveScaleMultiplier = 1.25f;
        [Tooltip("How far the most recently placed piece's color is shifted toward white.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lastMoveTintAmount = 0.4f;

        private readonly GameObject[] _cells = new GameObject[Board3D.CellCount];
        private readonly GameObject[] _pieces = new GameObject[Board3D.CellCount];
        private readonly Player[] _piecePlayers = new Player[Board3D.CellCount];
        private int _lastMoveIdx = -1;
        private bool _built;

        /// <summary>
        /// Spawn all 64 cell cubes as children of this transform. Idempotent —
        /// subsequent calls are no-ops.
        /// </summary>
        public void Build()
        {
            if (_built) return;
            for (int z = 0; z < Board3D.Size; z++)
            {
                for (int y = 0; y < Board3D.Size; y++)
                {
                    for (int x = 0; x < Board3D.Size; x++)
                    {
                        SpawnCell(x, y, z);
                    }
                }
            }
            _built = true;
        }

        public void PlacePiece(int x, int y, int z, Player player)
        {
            int idx = Board3D.Index(x, y, z);
            if (_pieces[idx] != null) Destroy(_pieces[idx]);

            var piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // Don't let pieces intercept clicks meant for cells.
            Destroy(piece.GetComponent<Collider>());
            piece.transform.SetParent(transform, worldPositionStays: false);
            piece.transform.localPosition = LocalCellPos(x, y, z);
            piece.transform.localScale = Vector3.one * _pieceScale;
            piece.name = $"Piece_{player}_{x}_{y}_{z}";
            ApplyColor(piece, player == Player.X ? _xColor : _oColor);
            _pieces[idx] = piece;
            _piecePlayers[idx] = player;
        }

        /// <summary>
        /// Mark the move at (x, y, z) as the most recent. The previously highlighted
        /// piece (if any) is restored to its normal styling. Useful so the player can
        /// see at a glance where the AI just placed.
        /// </summary>
        public void SetLastMove(int x, int y, int z)
        {
            int newIdx = Board3D.Index(x, y, z);
            if (newIdx == _lastMoveIdx) return;

            if (_lastMoveIdx >= 0 && _pieces[_lastMoveIdx] != null)
                ApplyNormalStyle(_pieces[_lastMoveIdx], _piecePlayers[_lastMoveIdx]);

            _lastMoveIdx = newIdx;
            if (_pieces[newIdx] != null)
                ApplyHighlightStyle(_pieces[newIdx], _piecePlayers[newIdx]);
        }

        /// <summary>Remove every placed piece. Cells are kept.</summary>
        public void ClearPieces()
        {
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i] != null)
                {
                    Destroy(_pieces[i]);
                    _pieces[i] = null;
                }
                _piecePlayers[i] = Player.None;
            }
            _lastMoveIdx = -1;
        }

        /// <summary>Recolor the four pieces along a winning line so the win is visible.</summary>
        public void HighlightLine(int[] line)
        {
            if (line == null) return;
            for (int i = 0; i < line.Length; i++)
            {
                GameObject piece = _pieces[line[i]];
                if (piece != null) ApplyColor(piece, _winningHighlightColor);
            }
        }

        private void ApplyNormalStyle(GameObject piece, Player player)
        {
            piece.transform.localScale = Vector3.one * _pieceScale;
            ApplyColor(piece, player == Player.X ? _xColor : _oColor);
        }

        private void ApplyHighlightStyle(GameObject piece, Player player)
        {
            piece.transform.localScale = Vector3.one * (_pieceScale * _lastMoveScaleMultiplier);
            Color baseColor = player == Player.X ? _xColor : _oColor;
            ApplyColor(piece, Color.Lerp(baseColor, Color.white, _lastMoveTintAmount));
        }

        private void SpawnCell(int x, int y, int z)
        {
            var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.transform.SetParent(transform, worldPositionStays: false);
            cell.transform.localPosition = LocalCellPos(x, y, z);
            cell.transform.localScale = Vector3.one * _cellScale;
            cell.name = $"Cell_{x}_{y}_{z}";
            ApplyColor(cell, _cellColor);

            // Cube primitive already has BoxCollider; expand it slightly so clicks
            // anywhere in the visual cell volume register, not just the small core.
            var collider = cell.GetComponent<BoxCollider>();
            collider.size = Vector3.one * (1f / _cellScale);

            var interactor = cell.AddComponent<CellInteractor>();
            interactor.Init(x, y, z);

            _cells[Board3D.Index(x, y, z)] = cell;
        }

        private Vector3 LocalCellPos(int x, int y, int z)
        {
            const float center = (Board3D.Size - 1) * 0.5f;
            return new Vector3(
                (x - center) * _cellSpacing,
                (z - center) * _layerSpacing,
                (y - center) * _cellSpacing);
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            // Instance the material so each piece can have its own color without
            // affecting siblings sharing the primitive's default material.
            var material = new Material(renderer.sharedMaterial);
            material.color = color;
            renderer.sharedMaterial = material;
        }
    }
}
