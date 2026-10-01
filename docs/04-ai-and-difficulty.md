# 04 — AI & Difficulty

The skirmish AI **is** the V1 product. It is the highest technical risk, so AI development starts in **month 1** and runs continuously.

## 1. Architecture (three layers)
```
┌──────────────────────────────────────────────────────────────┐
│ STRATEGIC (Commander) — utility AI, re-evaluated every 2–5 s │
│ Goals: Economy · Military · Tech · Defense · Special Weapons │
│ Build-order templates per faction × doctrine (branching)     │
└───────────────┬──────────────────────────────────────────────┘
                │ objectives + budgets
┌───────────────▼──────────────────────────────────────────────┐
│ OPERATIONAL — task forces + influence maps                   │
│ Maps: threat, territory value, VP pressure, known enemy      │
│ assets (tunnels, launch cells, depots, Iron Dome coverage)   │
└───────────────┬──────────────────────────────────────────────┘
                │ squad orders
┌───────────────▼──────────────────────────────────────────────┐
│ TACTICAL — per-squad behavior trees                          │
│ Cover seeking · retreat thresholds · flanking · garrisoning  │
│ ability use · AT kiting · dodging grenades and artillery     │
└──────────────────────────────────────────────────────────────┘
```

### Strategic layer
- **Utility scoring:** each goal is scored from the game state (income vs the enemy, army value, tech gap, threats seen, CP available, special-weapon readiness). The top goals receive a resource budget.
- **Build orders:** data-defined templates per faction × doctrine. Each has 3–5 **branch points**, for example "enemy armor scouted → go T2 AT now".
- **Counter-building:** compares the scouted enemy composition against the counter matrix in [02](02-core-gameplay.md) and shifts production weights. This is Hard only; Normal does it partially.
- **Special-weapon planner:** picks high-value targets from the influence maps (clusters, structures, detected launch cells) and times saturation against interceptor reload windows.

### Operational layer
- **Influence maps** are low-resolution grids (4 m cells) updated at 2 Hz:
  - threat (enemy DPS projected)
  - control (owned sectors)
  - value (VPs, fuel, munitions)
  - stealth knowledge (last-known positions of camo units, tunnel entrances)
  - AA coverage (to avoid flying air call-ins into SAMs)
- **Task forces** have one objective each: capture, defend, attack, harass, escort, or hunt (for example, an IDF K9 + D9R team hunting tunnels).
- **Multi-prong attacks:** Hard AI splits forces and feints. Normal AI attacks occasionally from two directions.

### Tactical layer (behavior trees)
- Seek the best cover within the move radius.
- Retreat at an HP or suppression threshold (by difficulty).
- Flank emplaced MGs instead of charging them frontally.
- Kite AT teams away from tanks. Keep tanks' front armor facing known AT.
- Leave artillery circles and grenade markers.
- Garrison buildings near objectives (Hamas prefers rubble ambushes).
- Use abilities on conditions: for example, smoke before crossing open ground, or Double-Tap when Trophy charges > 0.

### Faction-specific behaviors
| Faction | Signature AI behavior |
|---|---|
| IDF | Saves INT for strikes on launch cells and depots; escorts tanks with infantry; K9 + D9R tunnel hunts; places Iron Dome to cover production |
| Hamas | Builds a tunnel web early on sand maps; stationary rubble ambushes; times salvos for when Iron Dome is reloading; lays EFPs on predicted tank lanes; retreats through tunnels |
| Hezbollah | Fortifies high ground; layers AA before IDF T4; hides depots; Double-Taps Merkavas; shoot-and-scoot MLRS |

### Perception & fairness
- The AI **obeys fog of war on every difficulty**. There is no map hack. Last-known-position memory decays after 60 s.
- AI orders go through the **same command queue** as player input. This guarantees parity and makes AI matches replayable.

## 2. Difficulty levels
| Parameter | **Easy — Recruit** | **Normal — Veteran** | **Hard — Elite** |
|---|---|---|---|
| Reaction delay | 2.5 s | 1.0 s | 0.3 s |
| Effective APM cap | ~20 | ~50 | ~120 |
| Build order | Slow, generic, no branches | Faction-standard, some branches | Optimized, full branching + counter-builds |
| Cover usage | Poor (50% chance to pick cover) | Good | Precise, uses cover previews |
| Retreat threshold | 15% HP (often too late) | 30% HP | Dynamic: preserves veteran squads; considers suppression |
| Ability usage | ~40%, simple targets | ~75% | ~100%, combos (smoke → assault, Observer → salvo) |
| Multi-prong / feints | No | Occasional 2-prong | Yes, plus feints |
| Special weapons | Random targets | Valuable targets | Timed vs interceptor reloads, AA-aware flight paths |
| Hero use | Hero A mostly | Either | Either, with ability micro |
| Faction tricks | Basic | Tunnels / fortify / Intel | Full signature behaviors |
| Resource bonus | 0% | 0% | 0% (fair) |

**V1.1 option:** *Insane* difficulty = Hard + 30% resource bonus. It is clearly labeled as a cheating AI.

## 3. Targets & acceptance tests
| Test | Pass criteria |
|---|---|
| New-player test | First-time RTS players beat Easy within 1–3 attempts |
| CoH-veteran test | Experienced CoH players beat Normal about 70% of the time and Hard about 30–45% of the time |
| AI vs AI | Same-difficulty mirror matchups are 50% ± 3%; cross-faction matchups 47–53% |
| Performance | AI full update (all three layers) costs < 2 ms per frame on the minimum spec |
| No stalls | 0 matches in 1,000 where the AI stops producing or idles more than 60 s with resources available |

## 4. Tooling (built alongside the AI)
- **Headless batch runner:** runs AI-vs-AI matches faster than real time with no rendering. It runs nightly (thousands of matches) and outputs win rates, unit usage, resource curves and stall detection to CSV/JSON. Built on the deterministic simulation core (see [08](08-engine-and-technology.md)).
- **In-game AI debugger overlay:** influence maps, goal scores, task-force objectives and per-squad behavior-tree state.
- **Build-order editor:** data assets, editable by designers without code changes.
- **Replay of AI matches:** command-log replays for bug reproduction.
