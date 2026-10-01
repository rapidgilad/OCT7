# 09 — Production Roadmap (Solo)

## 1. Reality check
A Company-of-Heroes-style RTS is one of the most demanding genres for one person. Three things make it feasible:
1. **Reduced V1 scope:** about 10 units per faction, 1 doctrine, 1 hero and 2 maps. See [01](01-vision-and-scope.md).
2. **Claude Code writes most of the code**, verified by headless tests and AI-vs-AI batches.
3. **Bought or free art and TTS voices** instead of custom production.

The bottlenecks are **game feel, art integration and balance**, not lines of code.

## 2. Milestones
Durations assume about 30–40 hours per week. At 10–15 hours per week, multiply by about 2.5.

| # | Milestone | Duration (full-time) | Deliverables | Exit gate |
|---|---|---|---|---|
| M0 | **Foundations** | 1–1.5 months | Repo layout, `CLAUDE.md`, `sim/` skeleton (tick, commands, RNG), grid map, pathfinding, data loader, `dotnet test` in GitHub Actions, Godot project rendering the simulation with placeholder cubes | Squads move as groups on a grid map; tests are green in CI |
| M1 | **Core combat prototype** | 2 months | Cover, suppression, retreat, reinforce, capture, economy; Golani vs Qassam Fighters; placeholder art (CC0); camera, selection, command card | **Fun test:** 10 v 10 squad fights feel tactical (cover and suppression decide fights) |
| M2 | **IDF vs Hamas playable** | 3 months | IDF and Hamas core rosters; tiers HQ–T3; construction; Intel, tunnels, Iron Dome, Trophy; veterancy; Normal AI; MatchRunner; map Border Ridge Outpost (grey-box) | A full match vs Normal AI from start to victory without blockers |
| M3 | **Specials, heroes, difficulty** | 2 months | 2 special weapons and 1 doctrine per faction; heroes (Ronen, Al-Khuld); Easy and Hard AI; TTS voices v1; first art pass with purchased packs | **Public demo** on itch.io (free) + Steam "Coming Soon" page |
| M4 | **Hezbollah** | 2.5 months | Hezbollah core roster, Rocket Stockpile, fortifications, Al-Sayyad hero, Strategic Firepower doctrine; AI behaviors; balance with MatchRunner | All 9 faction matchups within 45–55% in AI-vs-AI |
| M5 | **Content & polish** | 2.5 months | Map 2 (Khan Sahil), destruction states, VFX, music, full voice set, UI polish, settings, faction primer screens, performance pass | Meets the performance budget on the min-spec PC |
| M6 | **Release** | 1 month | Steam Early Access (or full release), store assets, trailer (in-engine capture), rating questionnaire (IARC) | Ship V1 |

**Total:** about 14–16 months full-time, or about 3 years part-time.

**Do not move on until each exit gate passes.** The M1 fun test matters most. If combat isn't fun with cubes, art won't fix it.

## 3. Solo budget (rough)
| Item | Cost |
|---|---|
| Engine (Godot), Blender, Krita, Git/GitHub, .NET | $0 |
| Asset packs (characters, props, environment) | $300–1,500 |
| Vehicle models (purchased or commissioned) | $300–2,000 |
| SFX + music | $0–2,300 |
| TTS subscription during VO production | $50–400 |
| Native-speaker script review (Hebrew, Gazan Arabic, Lebanese Arabic) | $200–800 |
| Steam Direct fee | $100 |
| **Total** | **~$1,000–7,000** (excluding your time and the Claude subscription) |

## 4. Weekly rhythm (suggested)
| Day(s) | Focus |
|---|---|
| 1–3 | Feature work with Claude Code (one system per day, tests included) |
| 4 | Play the build; write down "feel" issues; integrate art |
| 5 | Balance: run MatchRunner overnight, adjust `data/`, fix AI issues |
| Every 2 weeks | Tag a build; play 3 full matches vs AI on different difficulties |

## 5. Risk register (solo)
| # | Risk | Mitigation |
|---|---|---|
| 1 | **Scope creep** (the full design is huge) | The core roster in each faction doc is the contract; everything else is "later" |
| 2 | **Burnout / motivation** | Playable milestones every 2–3 months; public demo at M3 for feedback and momentum |
| 3 | **AI quality** | AI starts in M2, not at the end; MatchRunner + debug overlay; 3 difficulties are just parameter sets of one AI |
| 4 | **Art consistency from mixed packs** | Pick one style family; unify with shared materials, color palette and lighting |
| 5 | **Pathfinding with destruction** | Grid-based simulation pathing (simple to update); tested headless in M0 |
| 6 | **TTS quality for dialects** | Native-speaker review; keep lines short (barks); swap in human actors later |
| 7 | **Store approval / regional rules** | Content rules in [01](01-vision-and-scope.md); data-driven names and emblems; submit the Steam page early (M3) |
| 8 | **Engine-specific dead ends** | Pure C# simulation means the presentation layer is replaceable |

## 6. Growing beyond solo
The public demo (M3) and an open repo, if you go that way, are the best recruitment tools. The first hires that change the most:
1. **3D artist / tech artist** (art consistency, vehicles, destruction)
2. **Sound designer** (feel, voices)
3. **Second programmer** focused on AI or multiplayer (V2)

The original team-scale plan (about 15 people, about 24 months, $2–3.5M) remains the reference for a funded expansion.

## 7. Post-V1 roadmap
| Version | Content |
|---|---|
| V1.1 | Hebrew/Arabic UI, Insane AI, maps 3–4, second hero per faction |
| V1.2 | Second doctrine per faction, more units from the full rosters |
| V2 | Online multiplayer (fixed-point determinism + lockstep), replays for players |
| V2.x | Campaign with cinematics, recorded VO |
| V3 | Map editor and mod support |

## 8. Next 30 days
1. Install Godot 4 .NET + .NET SDK locally; confirm a C# hello-world scene runs.
2. With Claude Code: create the repo layout, `CLAUDE.md`, the `sim/` skeleton with tick and command queue, and the first xUnit tests + GitHub Actions.
3. Grid map + pathfinding + squad movement (headless tests), then render it in Godot with cubes.
4. Implement cover + accuracy + suppression with tests based on [02](02-core-gameplay.md).
5. First playable: 3 Golani squads vs 3 Qassam squads on a flat test map.
