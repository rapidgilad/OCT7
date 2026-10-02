> **Status:** Not implemented (superseded)
>
> **Outcome:** Not built. Before approving it, the owner asked whether Godot would let the whole project be built in the cloud. That led to [`2026-10-01-03-godot-skeleton.md`](2026-10-01-03-godot-skeleton.md).
>
> **Note:** Useful again for the Unity port: its asmdef layout, StreamingAssets data and InputAdapter ideas are inputs to [`2026-10-02-08-unity-port.md`](2026-10-02-08-unity-port.md). Its folder layout is out of date: the sim now lives in `sim/`, not under `Unity/Assets`.
>
> The plan below is the approved text, unchanged. Its file paths and numbers describe the project as it was at the time.

# Plan — Unity 6 project skeleton for OCT7 (solo dev + Claude Code)

## Context
The design docs (README + `docs/`) are on branch `claude/coh-rts-game-design-06vm8t`. The last revision recommended Godot. The owner has now **chosen Unity 6** and wants a **code skeleton**.

Constraints:
- Solo developer.
- Claude Code works in a Linux cloud container that **cannot run the Unity Editor** (no install or license).
- The container **can** run .NET: `apt install dotnet-sdk-8.0` is available, and nuget.org is reachable.

Approach: keep all game rules in a **pure C# simulation assembly inside the Unity project**. Unity compiles it, and a .NET test project compiles the **same source files** headless. Claude can then build, test and run AI-vs-AI matches in the cloud. Unity is the thin presentation layer that you open locally.

Outcome: open `Unity/` in Unity Hub, run **OCT7 → Create Sandbox Scene**, press Play. Cubes = IDF and Hamas squads on a sandbox map with obstacles, an RTS camera, click/box select, and right-click to move along A* paths. The sim runs at 10 Hz with interpolation. `dotnet test` passes in the cloud and in GitHub Actions.

## Repo layout to create
```
OCT7/
├── CLAUDE.md                         # rules for future Claude sessions
├── .gitignore                        # Unity + .NET (ignore Unity/Library, Temp, Logs, UserSettings, Unity/*.csproj|*.sln, tools/**/bin|obj)
├── .gitattributes                    # LFS for binary art/audio; Unity YAML eol=lf + unityyamlmerge
├── .github/workflows/sim-tests.yml   # setup-dotnet 8 → dotnet test → MatchRunner smoke run
├── Unity/                            # ← open this folder in Unity Hub
│   ├── ProjectSettings/ProjectVersion.txt   # m_EditorVersion: 6000.0.23f1 (Unity 6 LTS; Hub lets you pick any installed 6.x)
│   └── Assets/
│       ├── StreamingAssets/data/{economy,factions,units}.json   # solo-V1 core rosters from the faction docs
│       └── _Project/
│           ├── Sim/                  # OCT7.Sim.asmdef (noEngineReferences: true, autoReferenced: true)
│           │   ├── Core/   Simulation, SimConfig (10 Hz), Command + MoveSquadsCommand, CommandQueue,
│           │   │           DeterministicRandom (xorshift, seeded), StateHasher (FNV-1a over state)
│           │   ├── Math/   Vec2, GridPos
│           │   ├── World/  MapGrid (2 m cells, blocked[], GroundType[]), MapFactory.CreateSandbox (obstacle rects)
│           │   ├── Pathfinding/ GridPathfinder (8-dir A*, binary heap, no corner cutting, deterministic tie-break, LOS smoothing)
│           │   ├── Units/  Squad (id, owner, defId, Position/PrevPosition, path, speed), MovementSystem
│           │   ├── Economy/ PlayerState (MP/MU/FU), EconomySystem (HQ income per tick)
│           │   ├── Data/   [Serializable] defs w/ public fields (JsonUtility + System.Text.Json compatible):
│           │   │           EconomyDef, FactionDef(+List), UnitDef(+List), GameDataSet, GameDataValidator
│           │   ├── Match/  MatchSetup (players, HQ positions, starting squads by faction; any matchup)
│           │   └── AI/     IAiController, AdvanceAi (stub: idle squads advance toward the enemy HQ every 5 s)
│           ├── Game/                 # presentation → Assembly-CSharp (no asmdef, so the Input System auto-references when installed)
│           │   ├── GameBootstrap.cs  # loads data, validates, MatchSetup, fixed-step accumulator, AI think, Step(), view sync with alpha
│           │   ├── UnityGameDataLoader.cs  # JsonUtility from StreamingAssets/data
│           │   ├── Views/  EntityViewRegistry, SquadView (team-colored cube + interpolation), MapView (ground + obstacle cubes)
│           │   ├── Input/  InputAdapter (#if ENABLE_INPUT_SYSTEM → Mouse/Keyboard.current, #elif ENABLE_LEGACY_INPUT_MANAGER → Input.*),
│           │   │           RtsCameraController (WASD/edge pan, scroll zoom, Q/E rotate), SelectionController (click/box select, right-click move)
│           │   └── UI/DebugHud.cs    # OnGUI: tick, resources, selection count, selection box
│           └── Editor/SandboxSceneCreator.cs   # menu "OCT7/Create Sandbox Scene": camera, light, Bootstrap → saves Scenes/Sandbox.unity + build settings
└── tools/
    ├── OCT7.Tools.sln
    ├── Sim/OCT7.Sim.csproj           # netstandard2.1, LangVersion 9 (Unity-compatible), <Compile Include="../../Unity/Assets/_Project/Sim/**/*.cs"/>
    ├── Shared/JsonGameDataLoader.cs  # System.Text.Json (IncludeFields) → same defs; linked into tests + runner
    ├── Sim.Tests/                    # xUnit, net8.0
    └── MatchRunner/                  # console: --matches N --seed S --ticks T --p0 idf --p1 hamas --verify-determinism → JSON summary
```

## Key rules baked into the skeleton (also written into CLAUDE.md)
- **Sim assembly:** no UnityEngine references; C# 9 / .NET Standard 2.1 only. LangVersion 9 in the csproj enforces this in the cloud.
- **Determinism (same-machine):**
  - Fixed tick; every mutation goes through a command.
  - All randomness goes through `DeterministicRandom`.
  - No `Dictionary` iteration, wall-clock time or `System.Random` in sim logic.
  - Entities are kept in a list sorted by id.
- **Data-driven:** stats only in `StreamingAssets/data/*.json`. Field names are camelCase public fields (JsonUtility is case-sensitive).
- **.meta files:** Unity generates them on first open. **Always commit .meta files.** Files Claude adds get their .meta files on your next Unity open.

## Doc updates (Godot → Unity, owner's decision)
- **`docs/08-engine-and-technology.md`:** rewrite for Unity 6 + pure C# sim. Covers:
  - Why the sim/presentation split matters (Claude can't run Unity in the cloud).
  - The layout above.
  - Tools: Unity Personal free under $200K, Rider, Blender.
  - Asset Store art packs.
  - Git LFS.
  - CI: `dotnet test` now; GameCI Unity builds later (needs license secrets).
  - RTL via RTLTMPro.
  - Licensing for an open repo.
- **`docs/09-production-roadmap.md`:** M0 and "Next 30 days" become Unity steps; budget line "Unity Personal $0".
- **`README.md`:** engine section → Unity 6; add a "Getting started" section (Unity Hub → Add `Unity/` → menu → Play; `dotnet test tools/OCT7.Tools.sln`).
- **`docs/01`:**
  - Engine decision row → "Unity 6 (chosen)".
  - Code-visibility row: drop the Godot mention.
- **`docs/04`:** AI path → `Unity/Assets/_Project/Sim/AI/`.
- **`docs/06`:** in-engine cinematics → Timeline + Cinemachine; music note → Unity Audio Mixer.
- **`docs/07`:** RTL → RTLTMPro + Unity Localization package.
- **Fix an inconsistency:** the Hamas solo roster lists Quadcopter Team under T2, but the unit table has it at T1. Move it to T1.

## Tests (xUnit, run in the cloud)
- `DeterministicRandom`: same seed → same sequence; different seeds → different sequences.
- `CommandQueue` / `Simulation`: commands execute on their scheduled tick in deterministic order (tick, player, sequence).
- `GridPathfinder`:
  - Straight path.
  - Route around a wall.
  - Unreachable target → empty path.
  - No diagonal corner cutting.
  - Smoothing keeps the path walkable.
- **Movement:** a squad reaches its target in the expected number of ticks (distance / speed).
- **Economy:** 600 ticks (60 s) → +220 MP from HQ income.
- **Determinism:**
  - Two identical runs (seed + commands + AdvanceAi) → identical `StateHasher` hash.
  - A different command stream → a different hash.
- **Data:**
  - The real JSON in `Unity/Assets/StreamingAssets/data` loads.
  - The validator passes (unique ids, valid faction refs, costs ≥ 0, pop/squadSize > 0).
  - Each faction has 1 hero and ≥ 9 units.

## Execution steps
1. `apt-get install -y dotnet-sdk-8.0`.
2. Create the files above. Unity scripts use only long-stable APIs: `GameObject.CreatePrimitive`, `Camera.ScreenPointToRay`, `Plane.Raycast`, `OnGUI`, `EditorSceneManager`, `EditorBuildSettings`.
3. `dotnet build` + `dotnet test tools/OCT7.Tools.sln`; run `MatchRunner --matches 20 --verify-determinism`.
4. Compile-check the Unity-side scripts in a scratchpad project against minimal stubs of the UnityEngine/UnityEditor APIs used. This catches syntax and type errors in our code; the real API check happens when you open the project.
5. Update the docs; check links.
6. Commit and push to `claude/coh-rts-game-design-06vm8t`.

## Verification
- **Cloud:**
  - `dotnet test` green.
  - MatchRunner prints a JSON summary with `determinism: ok` for all matches.
  - The stub compile of the Unity scripts succeeds.
  - Doc links resolve.
- **Your machine (first open):**
  1. Unity Hub → Add → `OCT7/Unity` → pick an installed Unity 6.x.
  2. Unity generates Packages/ProjectSettings/.meta files; no console errors.
  3. Run **OCT7 → Create Sandbox Scene** → Play.
  4. WASD/scroll/Q-E move the camera. Click or drag selects blue (IDF) cubes. Right-click moves them around obstacles. The red (Hamas) cubes advance on their own (AdvanceAi). The HUD shows the tick and rising MP.
  5. Commit the generated `Packages/`, `ProjectSettings/` and `.meta` files.
- **Optional after first open:** install URP + Input System via Package Manager (the InputAdapter handles both input backends).
