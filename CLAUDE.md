# CLAUDE.md — OCT7 / Iron Swords: Frontlines

Company-of-Heroes-style squad RTS (IDF vs Hamas vs Hezbollah). Solo developer + Claude Code.
**The design docs in `docs/` are the source of truth.** Read the relevant doc section before implementing a feature, and update the doc if the design changes.

## Layout
| Path | What | Engine refs? |
|---|---|---|
| `sim/` | **All game rules.** Pure C# library (`netstandard2.1`, C# 9) | **None.** Never reference Godot here |
| `tests/OCT7.Sim.Tests/` | xUnit tests for the sim (load the real `game/data` JSON) | No |
| `tools/MatchRunner/` | Headless AI-vs-AI batch runner (balance + determinism) | No |
| `tools/Shared/` | Helpers linked into tests and tools (repo data loader) | No |
| `game/` | Godot 4.5.1 .NET project: rendering, input, UI, audio only | Yes |
| `game/data/*.json` | **All stats** (economy, factions, units). camelCase keys, comments allowed | — |
| `docs/` | Game design documents | — |

## Commands (all run in the cloud; the session-start hook installs .NET 8 + Godot)
```bash
dotnet build OCT7.sln                                   # everything, including the Godot game assembly
dotnet test tests/OCT7.Sim.Tests                        # unit tests
dotnet run --project tools/MatchRunner -- --matches 20 --verify-determinism [--p0 idf --p1 hezbollah] [--verbose]
dotnet format whitespace OCT7.sln --verify-no-changes   # formatting (CI enforces)

godot --headless --path game --import                   # (re)import assets after adding files
godot --headless --path game -- --smoke-test 600        # run the real game headless, print state hash, exit 0/1
godot --headless --path game -- --smoke-test 30000 --full-match   # full AI-vs-AI match; fails unless a side wins

# Screenshot (Forward+ via software Vulkan, as the player sees it). Use 1280x720; it renders at a few fps.
xvfb-run -a -s "-screen 0 1280x720x24" godot --path game --rendering-method forward_plus --audio-driver Dummy \
  --resolution 1280x720 -- --demo --fast-forward 7200 --focus-army --distance 40 --screenshot /abs/path/shot.png --after-frames 6
```
Scenes: `scenes/menu.tscn` (`UI/MainMenu.cs`, the main scene) → `scenes/match.tscn` (`Match/MatchController.cs`, which hosts the sim and AI and builds the world and UI in code).

Launch options after `--` (any match option skips the menu; the menu reads them once):
- Match setup:
  - `--quick`: start a skirmish with the defaults.
  - `--p0`, `--p1`: player and enemy factions.
  - `--difficulty easy|normal|hard`, `--seed S`, `--map ID`.
- AI control:
  - `--demo`: AI also plays the local side (no fog unless `--fog`).
  - `--fog`: keep fog of war in demo mode.
- Running ahead:
  - `--fast-forward N`: simulate N ticks before the first frame. With `--focus-army`, it keeps going until someone is shooting.
  - `--full-match`: with `--smoke-test N`, run until victory (N = tick cap, fails without a winner); otherwise fast-forward to the end screen.
  - `--smoke-test N`: run N ticks headless and exit.
- Camera:
  - `--overview`: frame the whole map.
  - `--focus-army`: center on the nearest fight.
  - `--distance M`: initial camera distance.
  - `--select-all`: start with all your squads selected.
- Screenshots:
  - `--menu`: show the main menu even with other options (menu screenshots).
  - `--screenshot PATH`, `--after-frames N`: save a frame and quit.

## Golden rules
1. **Sim/presentation split.** Gameplay logic goes in `sim/`. `game/` reads sim state, interpolates, renders, and sends **commands**. It never mutates sim state directly.
2. **Determinism** (replays, MatchRunner and future lockstep multiplayer depend on it):
   - All state changes happen inside `Simulation.Step()` or `Command.Apply()`. Fixed 10 Hz tick (`SimConfig`).
   - Randomness only from `DeterministicRandom` (the sim's own, or a seeded one owned by an AI). **Never** `System.Random`, `DateTime`, `Stopwatch` or Godot time in sim logic.
   - Never iterate a `Dictionary`/`HashSet` to make sim decisions. Entities live in id-ordered lists.
   - Host loops call `SimLoop.Step(sim, ais, buffer)` so every host runs AI and sim in the same order.
   - `StateHasher` must cover any new state you add. Add it there when you add fields.
3. **Data-driven.** No gameplay numbers in code. Add fields to `sim/Data/Definitions.cs` + `game/data/*.json`, and extend `GameDataValidator`.
4. **Tests with every rule.** Each mechanic from `docs/02-core-gameplay.md` gets xUnit tests. Run MatchRunner after balance or AI changes.
5. **C# 9 in `sim/`.** No file-scoped namespaces, records, `required`, collection expressions or global usings. This keeps it portable to Unity.
6. **Godot scripts:** `partial` classes deriving from Godot nodes; file name = class name. Scenes stay minimal; build nodes in code where practical. Commit `*.uid` and `*.import` files; never commit `.godot/`.
7. **Content rules** (`docs/01-vision-and-scope.md`): no civilian NPCs or civilian-harm mechanics, fictional heroes, faction names and emblems via localization keys.

## Where things are going next (docs/09 §8)
v0.1, the first playable skirmish, is done. Next:
1. Playtest feedback and a balance pass with MatchRunner.
2. Signature systems: tunnels, Iron Dome, Trophy, Intel.
3. Veterancy, heroes and doctrines.
4. The deferred units.

Each step: sim + tests first, then visuals, then a screenshot check. Procedural models live in `game/scripts/Visual/`; a `res://assets/models/{id}.glb` overrides a vehicle or structure model.
