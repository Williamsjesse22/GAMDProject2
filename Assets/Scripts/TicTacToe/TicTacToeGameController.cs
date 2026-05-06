using System.Collections;
using Minimax;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TicTacToe
{
    /// <summary>
    /// Slice 4 controller: human vs minimax AI. Difficulty menu picks search depth
    /// (Easy=1 … Expert=5). Human always goes first by default, AI takes the
    /// opposing side. <see cref="MinimaxAI"/> uses alpha-beta by default; AI moves
    /// run synchronously inside a coroutine that yields one frame so the
    /// "AI thinking…" status has a chance to render before the search blocks.
    /// </summary>
    [RequireComponent(typeof(BoardVisualizer))]
    public sealed class TicTacToeGameController : MonoBehaviour
    {
        private const int MaxSearchDepth = 5;

        [SerializeField] private BoardVisualizer _visualizer;
        [SerializeField] private Camera _camera;
        [Tooltip("Side controlled by the human. AI plays the opposite side.")]
        [SerializeField] private Player _humanPlayer = Player.X;
        [Tooltip("Cosmetic delay before the AI commits its move so the player can see their own move first.")]
        [SerializeField] private float _aiThinkPauseSeconds = 0.35f;
        [Tooltip("Disable to silence all procedural audio cues.")]
        [SerializeField] private bool _enableAudio = true;
        [Range(0f, 1f)]
        [SerializeField] private float _audioVolume = 0.7f;

        private enum GameState { Menu, Playing, Over }

        private Board3D _board;
        private MinimaxAI _ai;
        private Player _aiPlayer;
        private Player _currentPlayer;
        private DifficultyLevel _difficulty = DifficultyLevel.Medium;
        private GameState _state = GameState.Menu;
        private bool _aiThinking;

        private string _statusMessage = string.Empty;
        private string _resultBanner;
        private GUIStyle _statusStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _menuTitleStyle;
        private GUIStyle _buttonStyle;

        private AudioSource _audio;
        private AudioClip _humanPlaceClip;
        private AudioClip _aiPlaceClip;
        private AudioClip _winClip;
        private AudioClip _loseClip;
        private AudioClip _drawClip;

        private static readonly DifficultyLevel[] s_difficultyOrder =
        {
            DifficultyLevel.Easy,
            DifficultyLevel.EasyMedium,
            DifficultyLevel.Medium,
            DifficultyLevel.Hard,
            DifficultyLevel.Expert
        };

        private void Awake()
        {
            if (_visualizer == null) _visualizer = GetComponent<BoardVisualizer>();
            if (_camera == null) _camera = Camera.main;
            _ai = new MinimaxAI(maxDepth: MaxSearchDepth);
            _aiPlayer = _humanPlayer.Opponent();
            _board = new Board3D();
            _currentPlayer = Player.X;
            SetupAudio();
        }

        private void SetupAudio()
        {
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f; // 2D — same volume regardless of camera distance.
            _audio.volume = _audioVolume;

            // Distinct pitches so the player can hear who just moved.
            _humanPlaceClip = SoundSynth.Beep("ttt_place_human", 880f, 0.08f);
            _aiPlaceClip = SoundSynth.Beep("ttt_place_ai", 660f, 0.10f);
            // C major arpeggio for win, descending minor-ish for loss.
            _winClip = SoundSynth.Arp("ttt_win", new[] { 523f, 659f, 784f, 1047f }, 0.55f);
            _loseClip = SoundSynth.Arp("ttt_lose", new[] { 392f, 311f, 247f }, 0.5f);
            _drawClip = SoundSynth.Beep("ttt_draw", 440f, 0.25f);
        }

        private void PlayCue(AudioClip clip)
        {
            if (!_enableAudio || clip == null || _audio == null) return;
            _audio.PlayOneShot(clip, _audioVolume);
        }

        private void Start()
        {
            _visualizer.Build();
            _statusMessage = "Press 1-5 to choose difficulty (or click)";
        }

        private void Update()
        {
            // Global: R returns to the difficulty menu from any state.
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                BackToMenu();
                return;
            }

            switch (_state)
            {
                case GameState.Menu:
                    HandleMenuKeyboard();
                    return;
                case GameState.Over:
                    return;
                case GameState.Playing:
                    if (_aiThinking) return;
                    if (_currentPlayer != _humanPlayer) return;
                    HandleHumanClick();
                    return;
            }
        }

        private void HandleMenuKeyboard()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) StartGame(DifficultyLevel.Easy);
            else if (kb.digit2Key.wasPressedThisFrame) StartGame(DifficultyLevel.EasyMedium);
            else if (kb.digit3Key.wasPressedThisFrame) StartGame(DifficultyLevel.Medium);
            else if (kb.digit4Key.wasPressedThisFrame) StartGame(DifficultyLevel.Hard);
            else if (kb.digit5Key.wasPressedThisFrame) StartGame(DifficultyLevel.Expert);
        }

        private void HandleHumanClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (_camera == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;

            var interactor = hit.collider.GetComponent<CellInteractor>();
            if (interactor == null) return;

            if (TryPlay(interactor.X, interactor.Y, interactor.Z)
                && _state == GameState.Playing
                && _currentPlayer == _aiPlayer)
            {
                StartCoroutine(AiTurnCoroutine());
            }
        }

        private IEnumerator AiTurnCoroutine()
        {
            _aiThinking = true;
            _statusMessage = $"AI thinking…    [{_difficulty}, depth {_difficulty.ToDepth()}]";
            // Yield once so the "thinking" message paints before we block.
            yield return null;
            if (_aiThinkPauseSeconds > 0f) yield return new WaitForSeconds(_aiThinkPauseSeconds);

            Move aiMove = _ai.FindBestMove(_board, _aiPlayer, _difficulty.ToDepth());
            TryPlay(aiMove.X, aiMove.Y, aiMove.Z);
            _aiThinking = false;
        }

        /// <summary>
        /// Apply a move on behalf of <see cref="_currentPlayer"/>. Returns false if
        /// the cell was occupied (for human clicks) — the AI is expected to never
        /// produce illegal moves.
        /// </summary>
        private bool TryPlay(int x, int y, int z)
        {
            if (!_board.IsLegal(x, y, z)) return false;

            Player mover = _currentPlayer;
            var move = new Move(x, y, z, mover);
            _board.Apply(move);
            _visualizer.PlacePiece(x, y, z, mover);
            _visualizer.SetLastMove(x, y, z);
            PlayCue(mover == _humanPlayer ? _humanPlaceClip : _aiPlaceClip);

            Player winner = _board.CheckWinner();
            if (winner != Player.None)
            {
                int[] line = FindWinningLine(winner);
                if (line != null) _visualizer.HighlightLine(line);
                bool humanWon = winner == _humanPlayer;
                EndGame(humanWon ? "You win!"
                       : winner == _aiPlayer ? "AI wins."
                       : $"{winner} wins!",
                       humanWon ? _winClip : _loseClip);
                return true;
            }

            if (_board.IsFull)
            {
                EndGame("Draw.", _drawClip);
                return true;
            }

            _currentPlayer = _currentPlayer.Opponent();
            UpdatePlayingStatus();
            return true;
        }

        private void EndGame(string banner, AudioClip endCue)
        {
            _state = GameState.Over;
            _aiThinking = false;
            _resultBanner = banner;
            _statusMessage = $"{banner}    Press R for menu.";
            PlayCue(endCue);
        }

        private void UpdatePlayingStatus()
        {
            string who = _currentPlayer == _humanPlayer
                ? $"Your move ({_humanPlayer})"
                : "AI's move";
            _statusMessage = $"{who}    [{_difficulty}, depth {_difficulty.ToDepth()}]    R = menu";
        }

        private void StartGame(DifficultyLevel difficulty)
        {
            StopAllCoroutines();
            _difficulty = difficulty;
            _board.Reset();
            _visualizer.ClearPieces();
            _aiPlayer = _humanPlayer.Opponent();
            _currentPlayer = Player.X;
            _state = GameState.Playing;
            _aiThinking = false;
            _resultBanner = null;
            UpdatePlayingStatus();

            // Edge case: if the human is configured to play O, AI moves first.
            if (_currentPlayer == _aiPlayer)
            {
                StartCoroutine(AiTurnCoroutine());
            }
        }

        private void BackToMenu()
        {
            StopAllCoroutines();
            _board.Reset();
            _visualizer.ClearPieces();
            _state = GameState.Menu;
            _aiThinking = false;
            _resultBanner = null;
            _statusMessage = "Press 1-5 to choose difficulty (or click)";
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

        // ---- IMGUI: status text + difficulty menu ----

        private void OnGUI()
        {
            EnsureStyles();
            DrawStatusText();
            if (_state == GameState.Menu) DrawDifficultyMenu();
            if (_state == GameState.Over && !string.IsNullOrEmpty(_resultBanner))
                DrawResultBanner();
        }

        private void EnsureStyles()
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
            if (_bannerStyle == null)
            {
                _bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 36,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.85f, 0.2f) }
                };
            }
            if (_menuTitleStyle == null)
            {
                _menuTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 42
                };
            }
        }

        private void DrawStatusText()
        {
            // Drop shadow then text for legibility on any background.
            var shadowRect = new Rect(12, 12, 1000, 40);
            var rect = new Rect(10, 10, 1000, 40);
            Color prev = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.65f);
            GUI.Label(shadowRect, _statusMessage, _statusStyle);
            GUI.color = prev;
            GUI.Label(rect, _statusMessage, _statusStyle);
        }

        private void DrawDifficultyMenu()
        {
            const float panelWidth = 320f;
            const float panelHeight = 380f;
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            var panelRect = new Rect(centerX - panelWidth * 0.5f, centerY - panelHeight * 0.5f,
                                     panelWidth, panelHeight);

            // Dim background panel (uses default GUI box for portability).
            GUI.Box(panelRect, GUIContent.none);

            var titleRect = new Rect(panelRect.x, panelRect.y + 14, panelRect.width, 30);
            GUI.Label(titleRect, "Select Difficulty", _menuTitleStyle);

            float buttonY = panelRect.y + 60f;
            const float buttonHeight = 42f;
            const float buttonSpacing = 8f;
            for (int i = 0; i < s_difficultyOrder.Length; i++)
            {
                DifficultyLevel level = s_difficultyOrder[i];
                var btnRect = new Rect(panelRect.x + 20, buttonY,
                                       panelRect.width - 40, buttonHeight);
                string label = $"{i + 1}. {level} (depth {(int)level})";
                if (GUI.Button(btnRect, label, _buttonStyle))
                {
                    StartGame(level);
                }
                buttonY += buttonHeight + buttonSpacing;
            }
        }

        private void DrawResultBanner()
        {
            float w = 600f;
            float h = 70f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
            // Semi-transparent box backdrop.
            Color prev = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = prev;
            GUI.Label(rect, _resultBanner, _bannerStyle);
        }
    }
}
