using System.Collections;
using System.Collections.Generic;
using Minimax;
using Shared;
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

        // Local PlayState enum (named distinctly to avoid colliding with
        // Shared.GameState which we reference for lock-mode interop).
        private enum PlayState { Rules, Menu, Playing, Over }

        // Static so the rules screen is shown once per editor session in
        // standalone mode and skipped on subsequent plays.
        private static bool s_rulesShown;

        private Board3D _board;
        private MinimaxAI _ai;
        private Player _aiPlayer;
        private Player _currentPlayer;
        private DifficultyLevel _difficulty = DifficultyLevel.Medium;
        private PlayState _state = PlayState.Menu;
        private bool _aiThinking;

        // Bonus: move history stack for undo, in placement order.
        private readonly Stack<Move> _moveHistory = new Stack<Move>();

        private string _statusMessage = string.Empty;
        private string _resultBanner;
        private GUIStyle _statusStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _menuTitleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _rulesTitleStyle;
        private GUIStyle _rulesSectionStyle;
        private GUIStyle _rulesBodyStyle;
        private GUIStyle _rulesHintStyle;
        private Texture2D _whitePixel;

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

            if (GameState.IsLockMode)
            {
                // Embedded as a maze lock minigame: skip the menu, start
                // immediately at the depth the lock asked for.
                DifficultyLevel level = DifficultyLevelExtensions.FromLockTier(GameState.LockDifficulty);
                StartGame(level);
            }
            else if (!s_rulesShown)
            {
                // Standalone first launch: explain the 4-in-a-row rules
                // before showing the difficulty menu.
                _state = PlayState.Rules;
                _statusMessage = string.Empty;
            }
            else
            {
                _state = PlayState.Menu;
                _statusMessage = "Press 1-5 to choose difficulty (or click)";
            }
        }

        private void Update()
        {
            // Global: R returns to the difficulty menu — only in standalone
            // mode. In lock mode, R would unhelpfully reset back to a menu
            // that we deliberately skipped.
            if (!GameState.IsLockMode
                && Keyboard.current != null
                && Keyboard.current.rKey.wasPressedThisFrame)
            {
                BackToMenu();
                return;
            }

            // Standalone-only: U undoes the last human+AI move pair.
            if (!GameState.IsLockMode
                && _state == PlayState.Playing
                && Keyboard.current != null
                && Keyboard.current.uKey.wasPressedThisFrame)
            {
                UndoLastMovePair();
                return;
            }

            switch (_state)
            {
                case PlayState.Rules:
                    HandleRulesDismiss();
                    return;
                case PlayState.Menu:
                    HandleMenuKeyboard();
                    return;
                case PlayState.Over:
                    return;
                case PlayState.Playing:
                    if (_aiThinking) return;
                    if (_currentPlayer != _humanPlayer)
                    {
                        // Not human's turn — make sure no stale ghost preview lingers.
                        _visualizer.HideGhost();
                        return;
                    }
                    UpdateHoverPreview();
                    HandleHumanClick();
                    return;
            }
        }

        private void HandleRulesDismiss()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            {
                s_rulesShown = true;
                _state = PlayState.Menu;
                _statusMessage = "Press 1-5 to choose difficulty (or click)";
            }
        }

        private void UpdateHoverPreview()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _camera == null) { _visualizer.HideGhost(); return; }
            // Hide ghost while orbiting so it doesn't trail the cursor through dead air.
            if (IsAltHeld()) { _visualizer.HideGhost(); return; }

            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                _visualizer.HideGhost();
                return;
            }

            var interactor = hit.collider.GetComponent<CellInteractor>();
            if (interactor == null || !_board.IsLegal(interactor.X, interactor.Y, interactor.Z))
            {
                _visualizer.HideGhost();
                return;
            }

            _visualizer.SetGhostMove(interactor.X, interactor.Y, interactor.Z, _humanPlayer);
        }

        private static bool IsAltHeld()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
        }

        private void UndoLastMovePair()
        {
            if (_moveHistory.Count == 0) return;

            // Cancel any in-flight AI move and stop coroutines so a delayed
            // AI play doesn't land mid-undo.
            StopAllCoroutines();
            _aiThinking = false;

            // Pop up to 2 moves so we revert to the state before the human's
            // last move (one human + one AI response). If only one is on the
            // stack, just revert that.
            int popsNeeded = Mathf.Min(2, _moveHistory.Count);
            for (int i = 0; i < popsNeeded; i++)
            {
                Move m = _moveHistory.Pop();
                _board.Undo(m);
                _visualizer.RemovePiece(m.X, m.Y, m.Z);
            }

            // After undo it should be the human's turn again.
            _currentPlayer = _humanPlayer;
            _visualizer.HideGhost();
            UpdatePlayingStatus();
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
            // Don't place a piece while the user is orbiting the camera.
            if (IsAltHeld()) return;

            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;

            var interactor = hit.collider.GetComponent<CellInteractor>();
            if (interactor == null) return;

            if (TryPlay(interactor.X, interactor.Y, interactor.Z)
                && _state == PlayState.Playing
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
            _moveHistory.Push(move);
            _visualizer.PlacePiece(x, y, z, mover);
            _visualizer.SetLastMove(x, y, z);
            // Hide the ghost the moment we commit so it doesn't briefly overlap
            // a real piece on the just-clicked cell.
            _visualizer.HideGhost();
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
                       humanWon ? _winClip : _loseClip,
                       humanWon ? LockOutcome.Win : LockOutcome.Loss);
                return true;
            }

            if (_board.IsFull)
            {
                EndGame("Draw.", _drawClip, LockOutcome.Draw);
                return true;
            }

            _currentPlayer = _currentPlayer.Opponent();
            UpdatePlayingStatus();
            return true;
        }

        private void EndGame(string banner, AudioClip endCue, LockOutcome outcome)
        {
            _state = PlayState.Over;
            _aiThinking = false;
            _resultBanner = banner;
            PlayCue(endCue);

            if (Shared.GameState.IsLockMode)
            {
                // Hand the result back to the maze and unload ourselves after a
                // short delay so the player can see the result banner first.
                Shared.GameState.LastLockOutcome = outcome;
                _statusMessage = banner;
                StartCoroutine(UnloadAfterDelay(1.5f));
            }
            else
            {
                _statusMessage = $"{banner}    Press R for menu.";
            }
        }

        private IEnumerator UnloadAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            SceneLoader.UnloadTicTacToe();
        }

        private void UpdatePlayingStatus()
        {
            string who = _currentPlayer == _humanPlayer
                ? $"Your move ({_humanPlayer})"
                : "AI's move";
            string controls = GameState.IsLockMode
                ? string.Empty
                : "    R = menu    U = undo";
            _statusMessage = $"{who}    [{_difficulty}, depth {_difficulty.ToDepth()}]{controls}";
        }

        private void StartGame(DifficultyLevel difficulty)
        {
            StopAllCoroutines();
            _difficulty = difficulty;
            _board.Reset();
            _moveHistory.Clear();
            _visualizer.ClearPieces();
            _aiPlayer = _humanPlayer.Opponent();
            _currentPlayer = Player.X;
            _state = PlayState.Playing;
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
            _moveHistory.Clear();
            _visualizer.ClearPieces();
            _state = PlayState.Menu;
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
            if (_state == PlayState.Rules)
            {
                DrawRulesPanel();
                return;
            }
            DrawStatusText();
            if (_state == PlayState.Menu) DrawDifficultyMenu();
            if (_state == PlayState.Over && !string.IsNullOrEmpty(_resultBanner))
                DrawResultBanner();
        }

        private void DrawRulesPanel()
        {
            float w = Screen.width;
            float h = Screen.height;

            // Dim backdrop.
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0.05f, 0.1f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, w, h), _whitePixel);
            GUI.color = prev;

            const float panelW = 620f;
            const float panelH = 540f;
            var panelRect = new Rect((w - panelW) * 0.5f, (h - panelH) * 0.5f, panelW, panelH);
            GUI.Box(panelRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(panelRect.x + 30, panelRect.y + 22,
                                          panelRect.width - 60, panelRect.height - 44));
            GUILayout.Label("3D TIC-TAC-TOE — RULES", _rulesTitleStyle);
            GUILayout.Space(14);

            GUILayout.Label("THE BOARD", _rulesSectionStyle);
            GUILayout.Label("64 cells in a 4×4×4 cube — four 4×4 layers stacked vertically.", _rulesBodyStyle);
            GUILayout.Space(10);

            GUILayout.Label("HOW TO WIN", _rulesSectionStyle);
            GUILayout.Label("Get FOUR of your pieces in a row — not three.", _rulesBodyStyle);
            GUILayout.Label("A row can run in any of these 76 ways:", _rulesBodyStyle);
            GUILayout.Label("●  Along an edge of the cube  (left-right, front-back, up-down)", _rulesBodyStyle);
            GUILayout.Label("●  Diagonally across one layer  (e.g. top-left to bottom-right)", _rulesBodyStyle);
            GUILayout.Label("●  Diagonally through all four layers  (3D space diagonals)", _rulesBodyStyle);
            GUILayout.Space(10);

            GUILayout.Label("TURNS", _rulesSectionStyle);
            GUILayout.Label("You play X; the AI plays O.  X always moves first.", _rulesBodyStyle);
            GUILayout.Label("After every click, the AI takes one turn — strict 1-for-1.", _rulesBodyStyle);
            GUILayout.Label("If a click does nothing, you hit an occupied cell or missed.", _rulesBodyStyle);
            GUILayout.Space(10);

            GUILayout.Label("TIPS", _rulesSectionStyle);
            GUILayout.Label("●  The dim translucent piece is a hover preview, not a placement.", _rulesBodyStyle);
            GUILayout.Label("●  Winning lines highlight gold + animate when complete.", _rulesBodyStyle);
            GUILayout.Label("●  R = back to menu   ·   U = undo your last move.", _rulesBodyStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Press  SPACE  or  ENTER  to continue", _rulesHintStyle);
            GUILayout.EndArea();
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
            if (_whitePixel == null)
            {
                _whitePixel = new Texture2D(1, 1);
                _whitePixel.SetPixel(0, 0, Color.white);
                _whitePixel.Apply();
            }
            if (_rulesTitleStyle == null)
                _rulesTitleStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 28, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.95f, 0.85f, 0.4f) }
                };
            if (_rulesSectionStyle == null)
                _rulesSectionStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 18, fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.6f, 0.95f, 1f) }
                };
            if (_rulesBodyStyle == null)
                _rulesBodyStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 15,
                    normal = { textColor = new Color(0.92f, 0.92f, 0.92f) }
                };
            if (_rulesHintStyle == null)
                _rulesHintStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 18, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
                };
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
