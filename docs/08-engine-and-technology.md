# 08 — Engine & Technology

## 1. Recommendation: Unity 6 LTS (URP), C#

### Why Unity for this project
| Reason | Detail |
|---|---|
| **Genre-proven** | Iron Harvest (a CoH-style squad RTS) and Broken Arrow (a large-scale modern-warfare RTS) are both built on Unity. The genre's hard problems (many units, cover, destruction, line of sight) have been solved on this engine before. |
| **Iteration speed** | C# with fast domain reload, ScriptableObject data assets, and designer-friendly inspectors (Odin). RTS balance needs thousands of small data tweaks. |
| **Scale** | DOTS / Jobs / Burst for projectiles, line-of-sight grids, influence maps and the AI. A hybrid approach (GameObjects for units, Jobs for heavy systems) keeps complexity manageable. |
| **Ecosystem** | A* Pathfinding Project Pro, FMOD/Wwise integrations, RTLTMPro (Hebrew/Arabic), Cinemachine/Timeline (future cinematics), GameCI for builds. |
| **Hiring** | A large C# talent pool, including in Israel. |
| **Cost** | Personal is free under $200K revenue; Pro is a per-seat annual subscription. No runtime fee. |
| **Hardware reach** | URP scales to mid-range PCs and laptops, which widens the RTS audience. |

**Trade-offs:**
- Top-end visuals take more art and tech-art effort than Unreal 5.
- Unity's corporate/pricing history is a business risk. Mitigation: keep the simulation core engine-agnostic (pure C#).

### Alternatives
| Engine | Choose it if | Trade-offs |
|---|---|---|
| **Unreal Engine 5** | Visual fidelity and cinematic marketing are the #1 priority and the team is strong in C++ | Lumen/Nanite are costly from a top-down camera with hundreds of units; Mass Entity is less proven for RTS; deterministic lockstep is harder; slower iteration (C++ compile); 5% royalty above $1M gross. Excellent for **pre-rendered cinematics** later. |
| **Godot 4** | Zero license cost, open source, very small team | Weaker large-scale 3D performance and tooling; few RTS-grade plugins (pathfinding, destruction); higher technical risk |
| **Custom engine** | Large, experienced engine team | Not viable for V1 time and budget |

**Hybrid option (recommended for later):** build the game in Unity and produce V2 pre-rendered cinematics in Unreal 5 (MetaHuman) or Blender, played back as video. This gives cinematic quality without engine lock-in.

## 2. Architecture

### Deterministic simulation core (decide now)
Even though V1 is single-player, the simulation is built **deterministic** from day 1:

```
 Player input ─┐                   ┌─> Presentation (Unity): interpolated
 AI decisions ─┼─> Command Queue ─>│   rendering, animation, VFX, audio, UI
 (same path)   │   (tick-stamped)  │
               └───────────────────┴─> Simulation Core (pure C#, fixed-point,
                                       fixed 8–10 Hz tick, seeded RNG)
```

- **Pure C# simulation** with fixed-point math and a seeded RNG. No Unity physics in gameplay logic. The presentation layer reads simulation state and interpolates.
- **Benefits:**
  - Internal **replays** (command logs) for bug reproduction.
  - The **headless AI-vs-AI batch runner** (runs faster than real time) for balance work.
  - **Lockstep multiplayer** in V2 without a rewrite.
- **Cost:** about 10–15% more upfront engineering. This is the single most important architectural decision.

### Core modules
| Module | Notes |
|---|---|
| Selection & commands | Command pattern; every action is serializable |
| Pathfinding | **A\* Pathfinding Project Pro**: recast graphs, dynamic graph updates when buildings collapse, RVO local avoidance; custom vehicle pathing with turning radius and reverse |
| Formations & squad movement | Squad leader + member slots; cover-aware slot assignment |
| Cover system | Cover nodes generated from tagged props at bake time; runtime updates on destruction |
| Combat resolver | Accuracy bands, received accuracy, cover, suppression, armor/penetration, criticals |
| Interceptor system | Shared component for Trophy, Iron Dome, Iron Beam and ERA |
| Line of sight / fog of war | Height-aware grid (1–2 m cells) computed in Burst jobs; GPU fog texture for rendering |
| Detection / camouflage | Camo vs detection levels per unit; reveal timers |
| Territory & economy | Sector graph with supply connectivity |
| Construction & tech | Placement validation, build progress, tier requirements |
| **Tunnel network** | Graph of entrances; travel-time model; capacity; reroute on destruction |
| Abilities framework | Data-defined: targeting type, delay, area, effects, costs, cooldowns |
| Air call-ins & AA | Flight-path entities; AA engagement; abort logic |
| Destruction | Pre-fractured damage states → rubble prefab → navmesh and cover update (no full physics) |
| Veterancy & heroes | XP ledger, modifiers pipeline |
| AI | See [04](04-ai-and-difficulty.md) |
| Bark manager | See [06](06-audio-voice-cinematics.md) |
| Save / settings | Settings, keybinds, profile; no mid-match saves in V1 |

### Data pipeline
- **Every stat lives in data.** Units, weapons, abilities, upgrades, costs and build orders are defined in **balance spreadsheets**.
- An editor importer converts them to ScriptableObjects, which are baked to runtime blobs.
- Designers change balance without touching code. Spreadsheets are diffable in Git as CSV.
- Validation tool: checks references, missing localization keys and out-of-range values.

## 3. What we need

### Software & licenses
| Item | Purpose | Notes |
|---|---|---|
| Unity 6 LTS (Pro seats) | Engine | Per-seat subscription; Personal is fine during prototyping if eligible |
| JetBrains Rider (or Visual Studio) | C# IDE | |
| Blender (free) or Autodesk Maya | 3D modeling and animation | |
| Substance 3D Painter / Designer | Texturing | |
| ZBrush | High-poly sculpting (characters, heroes) | |
| Houdini Indie (optional) | Destruction and fracture authoring, procedural rubble | |
| Photoshop / Krita | 2D, UI, concept art | |
| FMOD Studio **or** Wwise | Adaptive audio and VO implementation | Both have free indie tiers |
| Reaper / Pro Tools | Audio editing | |
| Figma | UI/UX design | |

### Middleware & plugins (Unity)
| Item | Purpose |
|---|---|
| A* Pathfinding Project Pro | Pathfinding, dynamic navmesh, RVO |
| Odin Inspector | Designer tooling and data editors |
| RTLTMPro | Hebrew / Arabic right-to-left text and Arabic shaping |
| MicroSplat | Terrain shading |
| DOTween | UI and presentation tweens |
| Cinemachine + Timeline (built-in) | RTS camera, future cinematics |
| Steamworks.NET (or Facepunch.Steamworks) | Steam integration |
| Sentry or Backtrace | Crash reporting |
| Superluminal / Unity Profiler | Performance profiling |

### Version control & CI
| Item | Purpose |
|---|---|
| **Perforce Helix Core** (free up to 5 users) **or Unity Version Control** | Large binary art assets, file locking |
| GitHub (this repo) | Design docs, and optionally code with Git LFS |
| **GameCI** on GitHub Actions, or Unity Build Automation | Automated nightly builds, tests, headless AI batch runs |

**Recommendation:** use Perforce or Unity Version Control for the game project, and keep this GitHub repo for design docs and balance spreadsheets.

### Production tools
- Jira or Linear (tasks), Confluence or Notion (wiki), Miro (boards), Discord (playtest community), Google Sheets (balance).

### Development hardware (per seat)
| Role | Spec |
|---|---|
| Programmers / designers | Ryzen 9 / Core i9, 64 GB RAM, RTX 4070 Ti or better, 2 TB NVMe, dual monitors |
| Artists / tech art | Same, with an RTX 4080-class GPU and 128 GB RAM for Houdini/ZBrush users |
| Build server | 16+ cores, 128 GB RAM, fast NVMe (for nightly builds and AI batch runs) |
| Mocap | Rokoko Smartsuit Pro + gloves, or an outsourced mocap studio; Asset Store military animation packs as placeholders |

### Player target specs (V1)
| | Minimum | Recommended |
|---|---|---|
| GPU | GTX 1660 / RX 5600 | RTX 3060 / RX 6600 XT |
| CPU | 4-core (i5-8400 class) | 6-core (Ryzen 5 5600 class) |
| RAM | 16 GB | 16 GB |
| Storage | 30 GB SSD | 30 GB SSD |
| Target | 1080p / 45+ fps at full pop | 1440p / 60 fps |

## 4. Performance budget (target at full pop, ~200 pop total)
| System | Budget per frame (minimum spec) |
|---|---|
| Simulation tick (amortized) | ≤ 4 ms |
| AI (all layers) | ≤ 2 ms |
| Line of sight / fog of war | ≤ 1.5 ms (Burst jobs) |
| Rendering | ≤ 12 ms |
| Draw calls | ≤ 3,000 (GPU instancing for infantry, LODs, impostors for distant props) |
