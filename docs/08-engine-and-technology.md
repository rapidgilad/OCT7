# 08 — Engine & Technology (Solo Developer + Claude Code)

## 1. Recommendation: Godot 4 (.NET / C#) + a pure C# simulation core

### Why the recommendation changed from Unity
The first draft assumed a team of about 15. For **one developer who builds mostly through Claude Code**, the deciding factors are different:

| Factor | Godot 4 .NET | Unity 6 |
|---|---|---|
| **Works with Claude Code** | Scenes (`.tscn`) and resources (`.tres`) are short, readable text files that Claude can write and edit directly. The editor is small and runs **headless on Linux**, so Claude can build and test in the cloud session. | Scenes and prefabs are long, noisy YAML that is hard to edit by text. Headless and CI use needs license activation. Many tasks need the GUI editor. |
| **Cost** | Free, MIT license, no seats, no royalties | Free under $200K revenue, then per-seat Pro |
| **Install / footprint** | About 100–200 MB, starts in seconds | Many GB, slow imports |
| **Code visibility** | MIT engine, ideal for public code | Possible, but contributors need Unity and Asset Store items can't be shared |
| **Asset ecosystem** | Smaller. Use engine-agnostic sources (FBX/glTF packs, Mixamo, CC0) | Huge Asset Store |
| **RTS precedent** | Fewer shipped 3D RTS games; you are closer to the frontier | Iron Harvest, Broken Arrow |
| **3D performance at RTS scale** | Adequate for the solo V1 scale (≈ 2 × 100 pop) with MultiMesh, LODs and a simulation in C# | Stronger (DOTS/Burst) |

**Verdict:** Godot 4 .NET is the better fit for a solo developer coding with Claude Code. Unity stays a valid choice if you prefer its editor, already know it, or want to rely heavily on Asset Store art.

**Hedge:** the simulation is **pure C# with no engine references**. If the project grows into a team, the presentation layer can be ported to Unity without rewriting the game rules.

## 2. Architecture

### Project layout (monorepo)
```
OCT7/
├── docs/                      # design docs (this folder)
├── data/                      # all balance data: units, weapons, abilities, build orders (JSON/CSV)
├── sim/                       # pure C# class library (.NET 8+) — THE GAME RULES
│   ├── Core/                  # tick loop, command queue, seeded RNG, entity store
│   ├── World/                 # map grid, sectors, ground types, cover nodes, LOS grid
│   ├── Combat/                # accuracy, cover, suppression, armor, criticals, interceptors
│   ├── Units/                 # squads, vehicles, veterancy, heroes, abilities
│   ├── Economy/               # resources, income, upkeep, tech tiers, construction
│   ├── Factions/              # Intel (IDF), TunnelNetwork (Hamas), RocketStockpile (Hezbollah)
│   ├── Pathfinding/           # grid A* + flow fields + steering
│   └── AI/                    # strategic utility AI, influence maps, squad behaviors
├── sim.tests/                 # xUnit tests — run with `dotnet test`, no engine needed
├── tools/
│   └── MatchRunner/           # headless CLI: AI vs AI × N matches → win-rate report
└── game/                      # Godot 4 .NET project — PRESENTATION ONLY
    ├── scenes/                # maps, units, UI (.tscn)
    ├── scripts/               # C# glue: reads sim state, renders, plays audio, sends commands
    └── assets/                # models, textures, audio (see licensing)
```

### Simulation rules (what makes this solo-friendly)
- **Fixed tick (10 Hz), command queue, seeded RNG.** Every player and AI action is a command.
- **Same-machine determinism** using plain floats is enough for V1. It gives replays and repeatable bug reproduction. Fixed-point math (cross-machine lockstep) is needed only for V2 multiplayer, so it is postponed.
- **The simulation knows nothing about Godot.** Godot reads simulation state each frame, interpolates positions, and plays animations, VFX and audio.
- **Everything is testable headless.** `dotnet test` and `MatchRunner` run in the Claude Code cloud container and in GitHub Actions. This is your QA team.

### Systems and solo-simplified approaches
| System | Solo V1 approach |
|---|---|
| Pathfinding | Grid (2 m cells) A* + flow fields for groups + simple steering. In-simulation, deterministic, testable. Vehicles use a clearance-aware grid. |
| Cover | Cover nodes placed by tagging props in the map; heavy/light/negative |
| Line of sight / fog of war | Height-aware grid in the simulation; Godot renders a fog texture from it |
| Destruction | 2–3 mesh states per building (intact → damaged → rubble); the grid updates cover and pathing |
| Combat | Probabilistic model as in [02](02-core-gameplay.md) |
| Interceptors | One shared component for Trophy, Iron Dome and ERA |
| Tunnels | Graph of entrances with a travel-time model |
| Abilities | Data-driven (JSON): target type, delay, area, effect list, costs, cooldown |
| AI | Utility strategic layer + influence map + squad finite-state machines (FSMs); see [04](04-ai-and-difficulty.md) |
| Rendering of crowds | MultiMeshInstance3D for props; skinned meshes with LOD for soldiers; impostors for far props |
| Audio | Godot audio buses + a simple bark manager (priority + cooldown) |

### Data pipeline
- Balance lives in `data/` as JSON (or CSV for spreadsheets). You or Claude can edit numbers in plain text. Diffs are reviewable in Git.
- A validator test checks references, ranges and missing localization keys on every `dotnet test`.

## 3. What you need (solo)

### Software (all free unless noted)
| Item | Purpose |
|---|---|
| **Godot 4.x .NET build** (latest stable) | Engine |
| **.NET 8+ SDK** | C# simulation, tests, MatchRunner |
| **Claude Code** | Main coding partner; can run tests and headless matches in the cloud |
| VS Code (C# Dev Kit) or JetBrains Rider (free for non-commercial use) | Local editing and debugging |
| **Blender** | Kitbashing, fixing and retopologizing purchased/CC0 models; simple animation |
| Krita / GIMP | Textures, UI icons |
| Audacity / Reaper (cheap license) | Audio editing |
| Git + GitHub (+ Git LFS for binaries) | Version control (Perforce is unnecessary solo) |
| GitHub Actions | Runs `dotnet test` and nightly AI-vs-AI batches automatically |

### Assets (where a solo developer gets art and sound)
| Need | Sources | Rough cost |
|---|---|---|
| Soldiers, weapons, props | Low-poly / stylized military packs (e.g., Synty POLYGON military packs), Quaternius (CC0), Kenney (CC0), Sketchfab (CC-BY or paid) | $0–800 |
| Animations | **Mixamo** (free), plus retargeted packs | $0–200 |
| Vehicles (Merkava, Namer, D9R, technicals, MLRS) | Sketchfab purchases, or commissioned freelance low-poly models | $300–2,000 |
| Environment (urban blocks, hills, greenhouses) | Modular packs + Blender kitbash | $100–500 |
| SFX | Sonniss GDC bundles (free), paid weapon packs | $0–300 |
| Music | Game-licensed music packs, or a commissioned composer | $0–2,000 |
| Voices | AI TTS with Hebrew and Arabic support, plus a native speaker to review scripts | $10–100/month while producing VO |

**Art direction for a solo developer:** stylized, readable at RTS distance (clean silhouettes, strong team colors, low-poly with good lighting). Realistic AAA art is a team-scale goal.

**Licensing rule:** read each pack's license. Most paid packs (Synty included) allow use in any engine and commercial games but **forbid redistributing the raw files**.

### If the code will be open source
| Item | Recommendation |
|---|---|
| Code license | MIT (maximum adoption) or GPL-3.0 (forks must stay open) |
| Paid assets | Keep them **out of the public repo**: a private Git LFS repo or private submodule, or a download step |
| Public repo assets | CC0 placeholders (Kenney/Quaternius) so anyone can build and run it |
| Your own original art and audio | A separate license, e.g., CC BY-NC 4.0 or all rights reserved |

### Your hardware
- Any modern PC works for Godot. Comfortable: 6–8-core CPU, 32 GB RAM, RTX 3060 or better, SSD.
- Test regularly on a weaker machine or laptop. The minimum target is GTX 1060-class, 8–16 GB RAM.

### Player target specs (solo V1)
| | Minimum | Recommended |
|---|---|---|
| GPU | GTX 1060 / RX 580 | RTX 3060 |
| CPU | 4-core | 6-core |
| RAM | 8 GB | 16 GB |
| Target | 1080p / 45+ fps at full pop | 1080p–1440p / 60 fps |

## 4. Performance budget (full pop, ~200 pop total)
| System | Budget per frame (minimum spec) |
|---|---|
| Simulation tick (amortized) | ≤ 4 ms |
| AI (all layers) | ≤ 2 ms |
| Line of sight / fog of war | ≤ 1.5 ms |
| Rendering | ≤ 14 ms |

## 5. Working effectively with Claude Code
1. **`CLAUDE.md` at the repo root.** Project overview, folder layout, conventions, how to run tests and MatchRunner. Future sessions start with full context.
2. **One feature = one small task.** Ask for one system at a time (e.g., "suppression meter + tests"), referencing the design doc section.
3. **Tests first for simulation rules.** Every rule in [02](02-core-gameplay.md) becomes a test (e.g., "heavy cover halves hit chance"). Claude runs them in the cloud.
4. **Use MatchRunner as an automated playtester.** After a balance change, run 200 AI-vs-AI matches and read the win-rate report.
5. **You own the feel.** Claude writes and tests code; you play the build in Godot locally and judge whether it's fun.
6. **Commit often to feature branches;** the docs in `docs/` remain the source of truth.

## 6. When the team grows
- The pure C# simulation, data folder and design docs scale without change.
- Possible additions: Perforce or Unity Version Control for large art, dedicated artists, recorded VO.
- Optionally port the presentation layer to Unity or Unreal if top-end visuals become the priority. The game rules stay in `sim/`.
