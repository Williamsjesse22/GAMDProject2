using Minimax;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TicTacToe
{
    /// <summary>
    /// Slice 3 controller: human-vs-human. Owns the <see cref="Board3D"/> state,
    /// alternates turns, and drives the <see cref="BoardVisualizer"/>. The AI
    /// hookup lands in slice 4.
    /// </summary>
    [RequireComponent(typeof(BoardVisualizer))]
    public sealed class TicTacToeGameController : MonoBehaviour
    {
        [SerializeField] private BoardVisualizer _visualizer;
        [SerializeField] private Camera _camera;
        [SerializeField] private Player _startingPlayer = Player.X;

        private Board3D _board;
        private Player _currentPlayer;
        private string _statusMessage = string.Empty;
        private int[] _winningLine;
        private GUIStyle _statusStyle;

        private void Awake()
        {
            if (_visualizer == null) _visualizer = GetComponent<BoardVisualizer>();
            if (_camera == null) _camera = Camera.main;
            _board = new Board3D();
            _currentPlayer = _startingPlayer;
        }

        private void Start()
        {
            _visualizer.Build();
            UpdateStatus();
        }

        private void Update()
        {
            if (_board.IsTerminal()) return;

            // R resets at any time (handy for slice-3 manual testing).
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetGame();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (_camera == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;

            var interactor = hit.collider.GetComponent<CellInteractor>();
            if (interactor == null) return;

            TryPlay(interactor.X, interactor.Y, interactor.Z);
        }

        public void ResetGame()
        {
            _board.Reset();
            _visualizer.ClearPieces();
            _currentPlayer = _startingPlayer;
            _winningLine = null;
            UpdateStatus();
        }

        private void TryPlay(int x, int y, int z)
        {
            if (!_board.IsLegal(x, y, z)) return;

            var move = new Move(x, y, z, _currentPlayer);
            _board.Apply(move);
            _visualizer.PlacePiece(x, y, z, _currentPlayer);

            Player winner = _board.CheckWinner();
            if (winner != Player.None)
            {
                _winningLine = FindWinningLine(winner);
                if (_winningLine != null) _visualizer.HighlightLine(_winningLine);
                _statusMessage = $"{winner} wins! Press R to reset.";
                return;
            }

            if (_board.IsFull)
            {
                _statusMessage = "Draw. Press R to reset.";
                return;
            }

            _currentPlayer = _currentPlayer.Opponent();
            UpdateStatus();
        }

        private int[] FindWinningLine(Player winner)
        {
            var lines = Board3D.WinningLines;
            for (int i = 0; i < lines.Count; i++)
            {
                int[] line = lines[i];
                if (_board.Get(line[0]) == winner
                    && _board.Get(line[1]) == winner
                    && _board.Get(line[2]) == winner
                    && _board.Get(line[3]) == winner)
                    return line;
            }
            return null;
        }

        private void UpdateStatus()
        {
            _statusMessage = $"{_currentPlayer} to move    (R = reset)";
        }

        private void OnGUI()
        {
            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }
            // Drop shadow for readability against any background.
            var shadowRect = new Rect(12, 12, 600, 40);
            var rect = new Rect(10, 10, 600, 40);
            var prevColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.Label(shadowRect, _statusMessage, _statusStyle);
            GUI.color = prevColor;
            GUI.Label(rect, _statusMessage, _statusStyle);
        }
    }
}
