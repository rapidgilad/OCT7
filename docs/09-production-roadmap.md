# 09 — Production Roadmap

## 1. Milestones (~22–24 months)
| Phase | Months | Deliverables | Exit gate |
|---|---|---|---|
| **Pre-production** | 0–3 | Final GDD; engine set up; deterministic simulation skeleton; grey-box prototype: selection, movement, cover, suppression, capture, retreat; 1 IDF squad type vs 1 Hamas squad type; art style target (1 hero asset per faction, 1 map block-out); AI strategic-layer skeleton | The core loop of cover + suppression + retreat feels good with 10 squads |
| **Vertical slice** | 4–9 | IDF vs Hamas through T1–T2; base building; tunnels; Iron Dome; veterancy; 1 hero each; 1 map (Khan Sahil) at final art quality; Normal AI; placeholder TTS VO; first bark manager | **"Is it fun?" gate:** external playtest of 10–20 CoH players; ≥ 70% want to play again |
| **Alpha** | 10–16 | Hezbollah; all tiers T1–T4; all special weapons; both doctrines per faction; both heroes per faction; 4 maps grey-boxed (2 art-complete); Easy / Normal / Hard AI; headless batch runner live; VO casting | Feature complete; every unit and ability in game |
| **Beta** | 17–21 | Content lock; VO recorded and implemented; all maps art-complete; balance via nightly AI-vs-AI + closed playtests; optimization to min spec; Hebrew/Arabic localization; tutorial | Content complete; no crash bugs; meets performance budget |
| **Release** | 22–24 | Release candidate; Steam store page and review; age rating (IARC/PEGI/ESRB); launch | Ship |

**Optional Early Access path:** release to Steam Early Access around month 14 with IDF + Hamas, 2 maps and 3 AI levels. Hezbollah joins during Early Access. This brings earlier revenue and feedback, but the first impression comes from an incomplete game.

## 2. Team

### Standard team (~15 people)
| Role | Count | Key responsibilities |
|---|---|---|
| Game director / lead designer | 1 | Vision, faction design, final calls |
| Systems & balance designer | 1 | Data tables, economy, counter matrix, AI build orders |
| Lead programmer | 1 | Architecture, deterministic simulation core |
| Gameplay programmers | 3 | Combat, units, abilities, construction, tunnels, interceptors |
| AI programmer | 1 | Strategic, operational and tactical AI; batch runner |
| Tools / engine programmer | 1 | Data pipeline, editors, build pipeline, performance |
| Technical artist | 1 | Shaders, destruction pipeline, LODs, VFX support |
| Environment artists | 2 | Modular kits, maps |
| Unit / vehicle artist | 1–2 | Infantry, vehicles, heroes |
| Animator | 1 | Infantry and vehicle animation, mocap cleanup |
| VFX artist | 1 | Weapons, explosions, interceptions, destruction |
| UI/UX designer | 1 | HUD, menus, RTL layouts |
| Audio designer | 1 | SFX, music integration, VO implementation (VO recording outsourced) |
| QA | 1–2 | Test plans, regression, AI stall detection |
| Producer | 1 | Schedule, outsourcing, VO vendors |

**Outsourcing candidates:** VO recording (3 languages), music composition, vehicle modeling, mocap, localization QA.

### Lean team (6–8 people)
| Role | Count |
|---|---|
| Designer | 1 |
| Programmers (1 focused on AI) | 3 |
| Tech artist / generalist artist | 1–2 |
| Producer / designer | 1 |
| Outsourced audio | — |

Lean teams rely heavily on Asset Store art and animation and outsourcing, and need about **30+ months**.

## 3. Budget bands (rough, fully loaded)
| Option | Team | Duration | Rough range |
|---|---|---|---|
| Lean | 6–8 + outsourcing | ~30 months | **$0.8–1.5M** |
| Standard | ~15 | ~24 months | **$2–3.5M** |

The ranges include salaries, licenses, hardware, VO (about 3,500 lines across 3 languages), music, mocap and QA. They exclude marketing.

## 4. Risk register
| # | Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|---|
| 1 | **AI quality** (the AI is the whole V1 experience) | High | High | AI programmer from month 1; batch runner by Alpha; AI acceptance tests ([04](04-ai-and-difficulty.md)) |
| 2 | **Pathfinding with destruction and vehicles** | High | Medium | Prototype A* PP Pro + dynamic updates + vehicle turning in months 1–2 |
| 3 | **Scope: three asymmetric factions** | High | High | Vertical slice ships 2 factions; Hezbollah at Alpha; cut line = 1 doctrine per faction, 3 maps |
| 4 | **Store approval / regional law / rating** | High | Medium | Content rules ([01](01-vision-and-scope.md)); data-driven names and emblems; early Steam page submission; rating questionnaire early |
| 5 | **Arabic VO casting** | Medium | Medium | Start casting at Alpha; multiple vendors; TTS placeholders keep development unblocked |
| 6 | **Performance at full pop with destruction** | Medium | Medium | Performance budget from the vertical slice; Burst jobs for line of sight and projectiles; GPU instancing |
| 7 | **Balance of asymmetric counters** | Medium | High | Counter matrix as a design contract; nightly AI-vs-AI stats; telemetry in playtests |
| 8 | **Determinism bugs** | Medium | Medium | Fixed-point math, seeded RNG, desync checker (hash simulation state per tick in debug builds) |

## 5. Post-V1 roadmap
| Version | Content |
|---|---|
| V1.1 | Insane AI, +1 doctrine per faction, +1 map, balance patch |
| V2 | Online multiplayer (1v1, 2v2, lockstep), replays for players, ranked |
| V2.x | Campaign with cinematics (hybrid pipeline), English dub |
| V3 | Map editor and mod support, additional factions |

## 6. Immediate next steps (first 30 days)
1. Lock the open decisions in [01](01-vision-and-scope.md): faction naming approach, release model.
2. Hire or assign the lead programmer and AI programmer first.
3. Set up Unity 6 LTS, version control, GameCI and the data-pipeline skeleton.
4. Build the grey-box prototype: 1 IDF Golani squad vs 1 Hamas Qassam squad, cover, suppression, retreat, capture.
5. Art-style target: 1 Golani soldier, 1 Qassam fighter, 1 urban building with damage states.
6. Write the balance spreadsheet v0 from the faction docs.
