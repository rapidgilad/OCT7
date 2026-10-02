> **Status:** Done
>
> **Outcome:** Implemented in `78de00a`: Godot 4.5.1 .NET project, the engine-free `sim/` library, tests, MatchRunner, CI and CLAUDE.md.
>
> **Note:** The engine-free `sim/` is the reason a Unity port is a presentation-only job.
>
> The plan below is the approved text, unchanged. Its file paths and numbers describe the project as it was at the time.

# Plan — Godot 4 .NET project skeleton for OCT7 (cloud-first, solo dev + Claude Code)

## Context
The design docs (README + `docs/`) are on branch `claude/coh-rts-game-design-06vm8t` and already describe the Godot 4 .NET + pure C# simulation architecture (`docs/08`). The owner considered Unity, then chose **Godot** because Claude can do nearly everything in the cloud.

Checked in this container:

| Item | Status |
|---|---|
| Godot 4.5.1 .NET Linux build (GitHub releases, 93 MB) | Downloadable |
| Godot export templates | Downloadable |
| `dotnet-sdk-8.0`, `xvfb`, Mesa (apt) | Available |
| nuget.org | Reachable |
| Unity download hosts | Blocked |

Outcome: a skeleton where Claude can **build, test, run AI-vs-AI matches, and take screenshots in the cloud**. You open `game/project.godot` locally and press F5 to play. The sandbox has IDF and Hamas squads (team-colored boxes) on a map with obstacles:
- RTS camera, click/box selection, right-click to move along A* paths.
- 10 Hz deterministic simulation with interpolation.
- A debug HUD.

## Repo layout to create
```
OCT7/
├── CLAUDE.md                          # project rules + commands for future sessions
├── OCT7.sln                           # sim + tests + runner + game
├── .gitignore                         # .godot/, bin/, obj/, builds/, *.tmp, .mono/
├── .gitattributes                     # LFS for binary art/audio; text eol=lf for .tscn/.tres/.cs/.json
├── .claude/settings.json + scripts/cloud-setup.sh   # SessionStart hook (via session-start-hook skill):
│                                      #   remote sessions only → apt dotnet-sdk-8.0, xvfb, mesa; download Godot 4.5.1 .NET → /usr/local/bin/godot
├── .github/workflows/ci.yml           # dotnet test → MatchRunner determinism smoke → Godot headless import + smoke test
├── sim/OCT7.Sim.csproj                # netstandard2.1, LangVersion 9 (stays portable to Unity), System.Text.Json pkg
│   ├── Core/        Simulation, SimConfig (10 Hz), Command/MoveSquadsCommand, CommandQueue (tick→player→seq order),
│   │                DeterministicRandom (xorshift128+), StateHasher (FNV-1a)
│   ├── Math/        Vec2, GridPos
│   ├── World/       MapGrid (2 m cells, blocked[], GroundType[]), MapFactory.CreateSandbox (obstacle rects)
│   ├── Pathfinding/ GridPathfinder (8-dir A*, binary heap, no corner cutting, deterministic tie-break, LOS smoothing)
│   ├── Units/       Squad (id, owner, defId, Position/PrevPosition, path), MovementSystem
│   ├── Economy/     PlayerState (MP/MU/FU), EconomySystem (HQ income/tick)
│   ├── Data/        EconomyDef, FactionDef, UnitDef, GameDataSet, GameDataLoader (JSON strings → defs), GameDataValidator
│   ├── Match/       MatchSetup (players, HQ positions, starting squads per faction; any matchup)
│   └── AI/          IAiController, AdvanceAi (stub: idle squads advance toward the enemy HQ every 5 s)
├── tests/OCT7.Sim.Tests/              # xUnit net8.0 → ProjectReference sim; loads real game/data JSON
├── tools/MatchRunner/                 # console net8.0: --matches N --seed S --ticks T --p0 idf --p1 hamas --verify-determinism → JSON
└── game/                              # Godot project (presentation only)
    ├── project.godot                  # name, main scene, [dotnet] assembly_name="OCT7.Game", window 1600x900, Forward+
    ├── OCT7.Game.csproj               # Sdk="Godot.NET.Sdk/4.5.1", net8.0, ProjectReference ../sim
    ├── export_presets.cfg             # Windows Desktop + Linux, include_filter="data/*.json"
    ├── icon.svg
    ├── data/economy.json, factions.json, units.json   # solo-V1 core rosters from the faction docs (≈30 units + 3 heroes)
    ├── scenes/main.tscn               # Node3D root (Main.cs) + DirectionalLight3D + WorldEnvironment + CameraRig + HUD CanvasLayer
    └── scripts/
        ├── Main.cs                    # load data (FileAccess) → validate → MatchSetup → fixed-step accumulator → AI → Step() → views
        │                              # CLI: --smoke-test <ticks> (headless run, print hash, exit 0/1)
        ├── InputSetup.cs              # registers InputMap actions in code (WASD/arrows, Q/E, mouse) — no hand-written input map
        ├── Views/  ViewRegistry, SquadView (BoxMesh + team StandardMaterial3D + selection ring), MapView (ground + obstacles)
        ├── Input/  RtsCamera (pan/edge-pan/zoom/rotate), SelectionController (screen-projection box/click select; ground-plane ray for move)
        ├── UI/     DebugHud (tick, MP/MU/FU, selection count, drag rectangle)
        └── Tools/  ScreenshotTool (CLI --screenshot <path> --after-frames N → save viewport PNG, quit)
```

## Rules baked in (also written into CLAUDE.md)
- **The simulation (`sim/`) has no Godot references.** C# 9 / .NET Standard 2.1. All game rules live there; `game/` only renders and sends commands.
- **Determinism (same machine):**
  - Fixed tick; every mutation goes through a command.
  - All randomness goes through `DeterministicRandom`.
  - Entities live in a list sorted by id.
  - No Dictionary iteration, wall-clock time or `System.Random` in sim logic.
- **Data-driven:** stats only in `game/data/*.json` (camelCase). The validator runs in tests.
- **Every rule ships with tests.** Cloud commands:
  - `dotnet test`
  - `dotnet run --project tools/MatchRunner -- --matches 20 --verify-determinism`
  - `godot --headless --path game -- --smoke-test 600`
  - `xvfb-run godot --path game --rendering-method gl_compatibility -- --screenshot out.png`

## Doc touch-ups
- **`docs/factions/hamas.md`:** move Quadcopter Team from T2 to T1 in the solo roster table (it contradicts the unit table).
- **`docs/08`:** paths become `game/data/`, `tests/`, `tools/MatchRunner`. Add a "Cloud workflow (verified)" section: Godot pin 4.5.1, SessionStart hook, Xvfb screenshots.
- **`README.md`:**
  - Add a "Getting started" section. Local: install Godot 4.5.1 .NET + .NET 8 SDK → open `game/project.godot` → F5. Cloud: automatic.
  - Embed a sandbox screenshot.
- **`docs/09`:** adjust the "Next 30 days" paths. The skeleton completes step 2 and most of step 3.

## Tests (xUnit)
- `DeterministicRandom`: same seed → same sequence.
- **Command ordering:** commands execute on their scheduled tick in deterministic order.
- `GridPathfinder`:
  - Straight path.
  - Route around a wall.
  - Unreachable target → empty path.
  - No diagonal corner cutting.
  - The smoothed path stays walkable.
- **Movement:** a squad arrives within distance / speed ticks (± 1).
- **Economy:** 600 ticks → +220 MP.
- **Determinism:**
  - Identical runs (seed + commands + AdvanceAi) → identical hash.
  - A different command stream → a different hash.
- **Data:**
  - The real JSON loads.
  - The validator passes (unique ids, faction refs, costs ≥ 0, pop/squadSize > 0).
  - Each faction has 1 hero and ≥ 9 units.

## Execution steps
1. Load the session-start-hook skill. Create `scripts/cloud-setup.sh` + `.claude/settings.json`, then run the script once in this session.
2. Create `sim/`, `tests/`, `tools/` and `OCT7.sln`. Run `dotnet test` and MatchRunner until green.
3. Create `game/` (project, csproj, scene, scripts, data). Run:
   1. `dotnet build`
   2. `godot --headless --path game --import`
   3. the `--smoke-test` headless run
4. Take a sandbox screenshot with Xvfb + gl_compatibility. Inspect it, iterate on visuals, and save it to `docs/images/sandbox.png`.
5. Add CI, CLAUDE.md, .gitignore/.gitattributes and the doc touch-ups. Check links.
6. Commit and push to `claude/coh-rts-game-design-06vm8t`. Send you the screenshot.

## Verification
- **Cloud:**
  - `dotnet test` green.
  - MatchRunner reports `determinism: ok`.
  - Godot import has no errors.
  - The smoke test exits 0.
  - The screenshot shows the map, obstacles, and blue/red squads.
- **Your machine:** open `game/project.godot` in Godot 4.5.1 .NET → F5.
  - WASD/edge pan, scroll zoom and Q/E rotate move the camera.
  - Drag selects IDF squads; right-click moves them around obstacles.
  - Hamas squads advance on their own.
  - The HUD shows the tick and rising MP.
- **Deferred:** Windows export (the templates are about 1 GB) waits for the first playable milestone; the presets are already included.
