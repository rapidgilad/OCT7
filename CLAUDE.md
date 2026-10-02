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
Playable builds (CI `package` job in `.github/workflows/ci.yml`):
- Every push that passes CI exports Windows and Linux (`game/export_presets.cfg`), then smoke-tests the exported Linux binary.
- `.github/scripts/package.sh` zips both, adding launcher `.bat` files and a README to the Windows zip.
- Pushes to the default branch update the rolling `dev-latest` release (`OCT7-windows.zip`).
- CI writes `game/build_info.json` (gitignored) and the main menu shows it.
- Godot's C# exporter needs `game/OCT7.Game.sln` (named after the assembly; it holds only the game and sim projects). Keep it.
- To export locally:
  1. Install the 4.5.1 mono export templates in `~/.local/share/godot/export_templates/4.5.1.stable.mono/`.
  2. Run `godot --headless --path game --export-release "Linux" ../builds/linux/OCT7.x86_64`.
  3. Run `builds/linux/OCT7.x86_64 --headless -- --smoke-test 600`.

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
  - `--look X,Z`, `--yaw DEG`: initial camera focus and rotation.
  - `--select all|one|hq|build` (`--select-all` = `all`): initial selection, to show the HUD's selection and command cards.
- Art review:
  - `--showcase idf|hamas|hezbollah|all`: a gallery instead of a match (`MatchSetup.CreateShowcase`). Each faction gets a band with every structure, every unit in front, and obstacle and vegetation samples. Bands are 76 m apart; for band i use `--look 54,<54+76*i> --distance 66`.
  - `--vfx-test`: loops sample explosions, fire, tracers and rockets in front of the camera. Effects advance at most 1/30 s per rendered frame, so take software-rendered shots after 25 or more frames.
- Screenshots:
  - `--menu`: show the main menu even with other options (menu screenshots).
  - `--screenshot PATH`, `--after-frames N`: save a frame and quit. The run also prints `[audio] played …` counts on exit.
- Audio:
  - `--export-sounds DIR`: write the synthesized SFX (`game/scripts/Audio/SoundSynth.cs`) as WAV files and quit. Works headless.
  - Recordings in `game/assets/audio/{rifle,machine_gun,sniper,cannon,rocket_launch,explosion}.wav|.ogg` override them. Audio files go through Git LFS.

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

## Plans and handoff
- **Reading order for a new session:**
  1. This file.
  2. [`docs/10-implementation-reference.md`](docs/10-implementation-reference.md): the game as built, with every mechanic, number and UI behavior.
  3. The newest plan in [`docs/plans/`](docs/plans/README.md) that isn't Done.
  4. The design docs (`docs/01`–`09`) for intent and future scope.
- **Plans:**
  - Every approved plan is committed to `docs/plans/YYYY-MM-DD-NN-name.md`, with a status header, **before** implementation.
  - Plan-mode files outside the repo are temporary and get lost.
- **Mechanic changes:** any change to a mechanic updates docs/10 in the same commit.
- **Engine direction (2026-10-02):** the presentation is moving to **Unity** on the owner's desktop ([`docs/plans/2026-10-02-08-unity-port.md`](docs/plans/2026-10-02-08-unity-port.md)).
  - `sim/` and `game/data/` are shared and must stay engine-free.
  - Unity must reproduce the reference state hashes (docs/10 §1.4) before any gameplay UI is built.
  - `game/` (Godot) stays buildable and keeps CI green until the port's milestone U4.
- **Where to work:**
  - **Desktop:** primary, and the place for Unity work.
  - **Cloud sessions:** engine-free work only (`sim/`, tests, data, MatchRunner), and only when asked.
  - Pull before starting; push when done.

## Where things are going next (docs/09 §8)
v0.1, the first playable skirmish, is done. Next:
1. **The Unity port**, milestones U0–U4 (see the plan).
2. Playtest feedback and a balance pass with MatchRunner. Engine-free, so it can run in parallel.
3. Signature systems: tunnels, Iron Dome, Trophy, Intel.
4. Veterancy, heroes and doctrines.
5. The deferred units.

Each step: sim + tests first, then visuals, then a screenshot check.

Visuals:
- Procedural models live in `game/scripts/Visual/`: `SoldierModels`, `VehicleModels`, `StructureModels` (one design per structure id), `NatureModels`, `MeshKit`, and `Textures` (procedural world-triplanar detail textures).
- A `res://assets/models/{id}.glb` overrides a vehicle or structure model.
