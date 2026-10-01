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

# Screenshot (Forward+ via software Vulkan, as the player sees it). Use 1280x720; it renders at a few fps.
xvfb-run -a -s "-screen 0 1280x720x24" godot --path game --rendering-method forward_plus --audio-driver Dummy \
  --resolution 1280x720 -- --demo --focus-army --fast-forward 120 --screenshot /abs/path/shot.png --after-frames 30
```
Launch options after `--`:
- `--demo`: AI also plays the local side.
- `--fast-forward N`: simulate N ticks before the first frame.
- `--overview`: camera frames the whole map.
- `--focus-army`: camera centers on your squads.
- `--select-all`: start with all your squads selected.
- `--seed S`, `--p0`, `--p1`: seed and factions.
- `--screenshot PATH`, `--after-frames N`: save a frame and quit.
- `--smoke-test N`: run N ticks headless and exit.

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

## Where things are going next (docs/09 roadmap, M1)
Cover, accuracy and suppression → retreat and reinforce → sector capture and income → combat between squads. Each step: sim + tests first, then visuals, then a screenshot check.
