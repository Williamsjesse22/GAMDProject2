# Projects 2A & 2B — Unity Minimax + First-Person Maze

## Context
Individual project for ComS 437 (Iowa State, Spring 2026). Graded. Two assignments live in **one Unity project** because Project 2B explicitly incorporates 2A as the "lock" minigame in a different scene. This CLAUDE.md covers both phases.

AI-assisted development is explicitly permitted for these assignments. Hard requirements below override any conflicting suggestion in chat.

## Tech stack
- Unity 6 LTS (or 2022.3 LTS), 3D Universal Render Pipeline
- C# / .NET Standard 2.1
- Unity MCP (CoplayDev/unity-mcp) for AI-driven editor automation
- Standard Unity NavMesh for Phase 2

## Project phases

### Phase 1 — Project 2A (Minimax 3D Tic-Tac-Toe)
Standalone scene + a Unity-agnostic minimax library. Must be playable on its own and ready to be embedded as a minigame in Phase 2.

### Phase 2 — Project 2B (First-Person Maze)
First-person scene with terrain, NavMesh, two AI agents, hit-points, exit lock. The lock launches the Phase 1 scene as a minigame; winning unlocks the door.

---

## Phase 1 hard requirements (Project 2A)

1. **3D tic-tac-toe** (or another game with at least as many game-tree nodes). Stick with 3D TTT — easiest to implement and the rubric explicitly approves it.
2. **4×4×4 board**: four 4×4 layers stacked. Win = four in a row in any direction including verticals and 3D diagonals.
3. **Player vs AI**, AI uses **minimax**.
4. **5 difficulty levels** (depth-limited search: depth 1 / 2 / 3 / 4 / 5+).
5. **Alpha-beta pruning** required (worth 10% of the minimax points). Implement plain minimax first to lock in 90%, then add pruning.
6. **No third-party minimax assets**. Must be written by us.
7. **Unity 3D project** (so it's reusable in Phase 2). Display can be 2D — flat plane with cubes is fine and is what the assignment shows in its example.

## Phase 2 hard requirements (Project 2B)

1. **First-person play area** with obstacles (walls, terrain features, props). Camera moves with player; over-the-shoulder is acceptable.
2. **Unity NavMesh** baked on the terrain.
3. **At least 2 NavMesh agents** with **distinct personalities**, controlled by a **tactical AI method** discussed in class — we'll use **finite state machines** for one, **behavior trees** for the other (clearly demonstrates two methods).
4. **Player hit points**. If they hit zero, game over.
5. **Agent search behavior**: agents patrol; if they spot the player, chase. Within close range, an "aura" deducts player health.
6. **Exit goal**: player navigates from start to an exit. Exit has a **lock**.
7. **Lock minigame** = the Phase 1 tic-tac-toe game. **3 difficulty levels** (easy/medium/hard) map to different minimax depths.
8. **Winning the lock opens the door to a portal.** (Loss = play again at same difficulty, to avoid frustration.)

---

## Architecture

### Directory layout
```
Assets/
├── Scenes/
│   ├── TicTacToe.unity        — Phase 1 standalone scene
│   ├── Maze.unity             — Phase 2 main scene
│   └── MainMenu.unity         — scene picker (lets us run 2A or 2B from one build)
├── Scripts/
│   ├── Minimax/                       ← PURE C#, NO MonoBehaviour. Reusable.
│   │   ├── Board3D.cs                 — 4×4×4 cell state, move application, win detection
│   │   ├── Move.cs                    — struct: (x, y, z, player)
│   │   ├── Player.cs                  — enum: None / X / O
│   │   ├── BoardEvaluator.cs          — heuristic scoring for non-terminal nodes
│   │   ├── MinimaxAI.cs               — plain minimax + alpha-beta variant, depth-limited
│   │   └── DifficultyLevel.cs         — enum mapping levels → search depth
│   ├── TicTacToe/                     ← Unity-specific UI for Phase 1
│   │   ├── TicTacToeGameController.cs — orchestrates turns, win check, AI calls
│   │   ├── BoardVisualizer.cs         — instantiates the 64 cell cubes, places X/O markers
│   │   ├── CellInteractor.cs          — handles raycast clicks onto cells
│   │   ├── DifficultyMenu.cs          — picks the AI level
│   │   └── TicTacToeResult.cs         — result struct passed back to Phase 2 caller
│   ├── Maze/
│   │   ├── Player/
│   │   │   ├── FirstPersonController.cs
│   │   │   ├── HealthComponent.cs
│   │   │   └── PlayerHud.cs           — health bar, agent-detected indicator
│   │   ├── Agents/
│   │   │   ├── AgentBase.cs           — shared NavMeshAgent wrapper, vision check
│   │   │   ├── FsmAgent.cs            — patrol → chase → attack-aura FSM
│   │   │   ├── BehaviorTreeAgent.cs   — patrol/chase/search via BT nodes
│   │   │   └── BehaviorTree/          — minimal BT runtime (Sequence/Selector/Leaf)
│   │   ├── World/
│   │   │   ├── ExitLock.cs            — triggers the minigame
│   │   │   ├── Portal.cs              — opens after lock won
│   │   │   └── PowerUp.cs             — optional health pickups
│   │   └── MazeGameController.cs      — scene-level state, win/lose, scene transitions
│   └── Shared/
│       ├── SceneLoader.cs             — loads TicTacToe.unity additively from ExitLock
│       └── GameState.cs               — static holder so 2A scene can hand result back
├── Prefabs/
│   ├── BoardCell.prefab
│   ├── XPiece.prefab / OPiece.prefab
│   ├── FsmAgent.prefab / BehaviorTreeAgent.prefab
│   └── Player.prefab
├── Materials/
└── Tests/
    └── EditMode/
        ├── Board3DTests.cs            — win detection across all 76 lines
        ├── MinimaxAITests.cs          — known endgame positions, depth correctness
        └── AlphaBetaTests.cs          — same results as plain minimax, fewer nodes visited
```

### Why Minimax/ is pure C#
This is the key separation. `Minimax/` contains zero `using UnityEngine` statements. That gives us:
- Trivial unit tests (EditMode, no Play mode required)
- Zero coupling to scene state
- Drop-in reuse from Phase 2's lock minigame without modification

The Unity-specific layer in `TicTacToe/` is the only thing that talks to Unity APIs.

### Minimax notes
- `Board3D` stores state as `Player[,,]` (4×4×4). Pre-compute the **76 winning lines** once at static init: 48 axis-aligned (16 per axis × 3 axes), 24 face diagonals, 4 space diagonals.
- Win detection: iterate the 76 lines; if all 4 cells match a player, that player wins.
- Heuristic for non-terminal nodes (used at depth limit): for each line, score based on count of own pieces minus opponent pieces, weighted exponentially. (1 piece = 1, 2 = 10, 3 = 100; opponent pieces zero out the line.)
- Difficulty depth: Easy=1, Easy-Med=2, Med=3, Hard=4, Expert=5+. Branching factor of 64 means depth 5 with alpha-beta is ~100K-1M nodes — should be sub-second on modern hardware.
- Alpha-beta: identical signature, same return values, but with α/β cutoffs. **Tests must verify both versions return the same best move on the same position.**

### FSM vs Behavior Tree (for the two agents)
Use both methods on purpose — the rubric wants us demonstrating "tactical AI decision-making methods."

- **FsmAgent** — "the brute." States: `Patrol`, `Chase`, `AttackAura`, `LostSight`. Hard-coded transitions. Aggressive.
- **BehaviorTreeAgent** — "the cautious one." Selector at root: [seek health if low → flee if outnumbered → chase if visible → patrol]. Uses a tiny in-house BT runtime (Sequence/Selector/Leaf nodes).

The two should *feel* different. FSM agent moves faster and commits to chases; BT agent retreats when low on aggression points.

### Scene transition for the lock
- Player reaches `ExitLock` trigger → `MazeGameController.PauseAndOpenLock(difficulty)`
- Loads `TicTacToe.unity` additively, hides the maze
- `TicTacToeGameController` reads difficulty from `GameState.LockDifficulty`
- On game end, writes result to `GameState.LockResult`, unloads itself
- `MazeGameController` reads result on resume → win opens portal, loss returns player

---

## Coding conventions
- `PascalCase` types/public members, `_camelCase` private fields, `s_` prefix for static fields.
- No allocation in `MinimaxAI.Search` hot path — reuse `Move` lists from a pool, or use stackalloc'd spans for child-move enumeration.
- Public members in `Minimax/` and `Agents/` get XML doc comments.
- All Unity-specific `MonoBehaviour` dependencies injected via `[SerializeField]` private fields, not `GameObject.Find`.
- Use `UnityEngine.Random` only for visual flair; minimax must be deterministic given a seed (for reproducible tests).

## Build & run
- Open in Unity, hit play in `MainMenu` scene to choose Phase 1 or Phase 2.
- Run EditMode tests: `Window` → `General` → `Test Runner` → `EditMode` tab → `Run All`.

---

## Phase ordering (proposed slice plan)

### Phase 1 slices
1. **Pure-C# minimax core**: `Board3D`, `Move`, win detection, plain minimax, evaluator, all unit tests passing.
2. **Alpha-beta variant**: same tests, both strategies green.
3. **Minimal Unity scene**: 64 cubes laid out, click to place, no AI.
4. **Wire AI into the scene**: difficulty menu, AI plays after human, result UI.
5. **Polish**: highlight last move, win-line visualization, sounds.

### Phase 2 slices
6. **First-person controller + maze geometry + NavMesh bake**.
7. **HealthComponent + HUD**.
8. **FsmAgent**: patrol + chase + aura damage. Hand-test with one agent.
9. **BehaviorTreeAgent**: BT runtime + same behaviors with different parameters/priorities.
10. **ExitLock + scene transition** to TicTacToe scene with difficulty parameter.
11. **Portal** + win/lose flow.
12. **Polish**: vision cones, agent personality tuning, audio.

Commit after each slice. Don't combine slices.

---

## Rules for Claude Code working on this repo

1. **Do not add Unity Asset Store packages** for minimax, navmesh agents, or behavior trees. Hard requirement #6 of Phase 1 forbids minimax assets, and we want our agent AI to be visibly hand-written for Phase 2 grading.
2. **Maintain the `Minimax/` purity rule** — zero `using UnityEngine` in that folder. If a feature seems to need Unity types, the integration goes in `TicTacToe/` instead.
3. **Always write EditMode tests** for new code in `Minimax/`. This is the highest-stakes subsystem.
4. **Don't bake the NavMesh on every script change** — it's expensive. Bake explicitly when terrain changes.
5. **Prefer NavMeshAgent built-ins** for movement; don't reimplement pathfinding.
6. **Scene transitions use additive loading**, not single-scene swaps — we want maze state preserved while the minigame plays.
7. **Don't enter Play mode to test logic** that can be tested in EditMode. EditMode tests are 100x faster.
8. **Verify against the rubric before adding bonus features.** Hard requirements first. Bonus is bonus.
9. **When debugging agent behavior**, add visible Gizmos (vision cone, current state label above head) — you can't grade or debug what you can't see.
10. **MCP commands that touch the scene hierarchy** should be batched when possible — many small edits trigger many domain reloads.

## Bonus features (only after Phase 2 hard reqs are 100% complete)
- Ranged attacks for agents (projectile prefab + line of fire check)
- Power-ups (health pickups, temporary speed boost)
- Multiple maze levels with progressive difficulty
- Better lock minigame UX (preview moves, undo)
- Win-line animation in the TTT scene when a 4-in-a-row is detected