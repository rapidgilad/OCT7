# 10 — Implementation Reference: the game as built (v0.1)

> **Purpose.** This document describes how v0.1 *actually works*, not how it was planned to work. Docs 01–09 describe intent and future scope. This one describes the shipped behavior that any port (see [`plans/2026-10-02-08-unity-port.md`](plans/2026-10-02-08-unity-port.md)) must reproduce.
>
> **Sources of truth, in order:**
> 1. `game/data/*.json` for every number.
> 2. `sim/` for every rule.
> 3. This document as a readable summary of both.
>
> If this document disagrees with the code or data, the code and data win, and this document gets fixed.
>
> Snapshot: commit `574a776` (2026-10-02), 94 passing tests, CI green.

---

## 1. Project at a glance

### 1.1 Layout

| Path | Contents | Engine dependency |
|---|---|---|
| `sim/` | All game rules. Pure C# library (`netstandard2.1`, C# 9, `System.Text.Json` 8.0.5) | None |
| `game/data/*.json` | All stats: rules, economy, factions, units, weapons, structures, AI, maps. camelCase keys, `//` comments allowed | — |
| `tests/OCT7.Sim.Tests/` | xUnit tests that load the real JSON | None |
| `tools/MatchRunner/` | Headless AI-vs-AI batch runner for balance and determinism | None |
| `tools/Shared/` | Repo data loader shared by the tests and tools | None |
| `game/` | Godot 4.5.1 .NET presentation: rendering, input, UI, audio | Godot |
| `docs/` | Design documents, this reference, and `plans/` | — |

The **sim/presentation split** is the most important architectural fact:
- The presentation reads sim state, interpolates between ticks, renders, and sends **commands**. It never writes sim state.
- Everything in §3–§7 lives in `sim/` and moves to another engine unchanged.
- Everything in §10 is presentation and must be rebuilt per engine.

### 1.2 Commands

```bash
dotnet build OCT7.sln
dotnet test tests/OCT7.Sim.Tests                      # 94 tests
dotnet run --project tools/MatchRunner -- --matches 20 --verify-determinism [--p0 idf --p1 hezbollah] [--verbose]
dotnet format whitespace OCT7.sln --verify-no-changes
godot --headless --path game -- --smoke-test 600      # prints the state hash at tick 600
godot --headless --path game -- --smoke-test 30000 --full-match
```

### 1.3 CI and playable builds

`.github/workflows/ci.yml` runs on every push and pull request. It has three jobs:

| Job | What it does |
|---|---|
| `sim` | Format check, unit tests, then MatchRunner: every faction matchup twice, with the determinism check |
| `godot` | Builds the game assembly, imports the project, runs the 600-tick smoke test, then a full match (Hezbollah vs IDF, hard) that must end in a victory |
| `package` | Pushes only, not pull requests. Exports Windows and Linux, smoke-tests the exported Linux binary, then zips both with `.github/scripts/package.sh` |

On the default branch, `package` also updates the rolling GitHub release `dev-latest` (asset `OCT7-windows.zip`).

### 1.4 Reference state hashes: the determinism parity target

`StateHasher` computes a 64-bit FNV hash over the full sim state. These values were produced by the Godot host and by the exported Linux binary:

| Setup | Ticks | Winner | Hash |
|---|---|---|---|
| IDF (p0) vs Hamas (p1), normal, seed 1, map `ridge_outpost`, 600 ticks (`--smoke-test 600`) | 600 | — | `abc254d66a095c02` |
| Same setup, full match (`--smoke-test 30000 --full-match`) | 10560 | 1 (Hamas) | `d2b762761181a436` |
| Hezbollah (p0) vs IDF (p1), hard, seed 1, full match (`--smoke-test 30000 --full-match --p0 hezbollah --p1 idf --difficulty hard`) | 11844 | 0 (Hezbollah) | `015b300ee8927c38` |

In these runs both sides are AI-controlled. Any new host is correct only once it reproduces these hashes. The hashes have not yet been confirmed on Windows.

---

## 2. Simulation architecture

### 2.1 Tick and loop
- Fixed 10 Hz tick (`SimConfig.TickSeconds` = 0.1).
- Each host calls `SimLoop.Step(sim, ais, buffer)` once per tick. It lets every AI think in player order, enqueues their commands, then calls `Simulation.Step()`. This keeps every host identical.
- `Simulation.Step()` runs in this exact order:
  1. Apply the commands due this tick.
  2. Update vision (only on ticks divisible by `visionUpdateTicks` = 2).
  3. Production and construction.
  4. Movement.
  5. Combat, including suppression decay and the stationary timer.
  6. Support: retreat, reinforce, heal.
  7. Territory.
  8. Economy.
  9. Victory check, then `Tick++`.
- Once the match is over, `Step()` only advances the tick.

### 2.2 Command queue
- Every change a player (human or AI) makes is a `Command` with `PlayerId` and a target `Tick`.
- Commands are ordered by **(tick, player, sequence)**. The queue assigns the sequence; past ticks are clamped to the current tick.
- Squad ids in a command are sorted before use, so selection order never changes results.
- Invalid commands are ignored. Where the UI should explain why, the sim emits `CommandRejected` with a `RejectReason`.

| Command | Effect |
|---|---|
| `MoveSquadsCommand` | Move squads in a grid formation (6 m spacing, roughly square). Infantry snap to the best cover within `coverSnapRadius` = 4 m of their slot. Cancels retreat, attack and build orders. |
| `AttackCommand` | Attack a specific squad or structure. Squads approach to 80% of their max weapon range if needed. |
| `RetreatCommand` | Retreat to HQ: faster, harder to hit, cannot fire, ignores pinning. |
| `ReinforceCommand` | Refill lost models near the HQ or a production building. |
| `StopCommand` | Stop moving; drop attack and build orders. |
| `ProduceCommand` | Queue a unit at a production structure. Cost and pop are reserved immediately. |
| `CancelProductionCommand` | Remove a queued unit and refund its full cost. |
| `ConstructCommand` | Engineers place a new structure (center cell + orientation 0/1), or assist an existing one (`ExistingStructureId`). |
| `SetRallyPointCommand` | Where newly produced units walk to. |
| `SurrenderCommand` | The player concedes; the match ends at the same tick's victory check. |

`RejectReason` values: `NotEnoughResources`, `PopCap`, `QueueFull`, `NotUnlocked`, `InvalidPlacement`, `NotOwner`, `NoEngineer`.

### 2.3 Events (output only)
`SimEventType` values: `ShotFired`, `ModelKilled`, `SquadDestroyed`, `Deflected`, `StructurePlaced`, `StructureCompleted`, `StructureDestroyed`, `UnitProduced`, `ModelReinforced`, `SectorCaptured`, `SectorNeutralized`, `CommandRejected`, `MatchEnded`.

Each event carries: `PlayerId`, `SourceId`, `TargetId`, `DefId`, `From`, `To`, and `Value` (hits in a volley, model index, reject reason, or winner id).

**Nothing in the sim reads events**, so they cannot affect determinism. Presentation uses them for tracers, sounds, messages and stats.

### 2.4 Determinism rules
- State changes only inside `Simulation.Step()` and `Command.Apply()`.
- Randomness only from `DeterministicRandom`: the sim's own instance, or a seeded instance owned by each AI. Never `System.Random`, wall-clock time or engine time.
- Never iterate a `Dictionary` or `HashSet` to make decisions. Entities live in id-ordered lists.
- `StateHasher` covers all state. Any new field must be added to it.

### 2.5 Source map

| Folder | Contents |
|---|---|
| `Core/` | `Simulation`, `CommandQueue`, `Commands`, `SimEvents`, `DeterministicRandom`, `StateHasher`, `SimConfig` |
| `Data/` | `Definitions`, `AiDefinitions`, `GameDataSet` (JSON loading), `GameDataValidator` |
| `World/` | `MapDefinition`, `MapFactory` (mirroring), `MapGrid`, `CoverGrid`, `VisibilityGrid` |
| `Units/` | `SimWorld` (entity lists), `Squad`, `MovementSystem` |
| `Pathfinding/` | `GridPathfinder` (A*) |
| `Combat/` | `CombatSystem`, `SupportSystem` (suppression, retreat, reinforce, heal) |
| `Territory/` | `TerritorySystem` |
| `Economy/` | `EconomySystem`, `PlayerState` |
| `Production/` | `ProductionSystem`, `Structure` |
| `AI/` | `SkirmishAi`, `AiControllers` (also holds `SimLoop`) |
| `Match/` | `MatchSetup`: skirmish setup and the art-review showcase |

---

## 3. Data model (`game/data`)

| File | Contents |
|---|---|
| `rules.json` | Combat, suppression, retreat, reinforce, heal, capture, construction and terrain constants (§4–§7) |
| `economy.json` | Starting resources, income, upkeep, pop cap, tickets (§6.4) |
| `factions.json` | Faction ids, localization keys, HQ structure, starting units (engineers plus one line squad each) |
| `units.json` | Unit definitions (§8) |
| `weapons.json` | Weapon definitions (§8.4) |
| `structures.json` | Structure definitions (§8) |
| `ai.json` | Difficulty presets and per-faction opening plus unit mix (§7) |
| `maps/maps.json`, `maps/ridge_outpost.json` | Map list and the v0.1 map (§9) |

`GameDataValidator` checks the loaded data on startup and in the tests:
- **Ids:** unique and non-empty for factions, weapons, units and structures. Every faction has a display-name key.
- **Weapons:** range, cooldown and damage > 0; accuracies within 0..1; `modelsHitVsInfantry` ≥ 1.
- **Units:**
  - A known faction; no negative costs; pop, squad size and speed > 0; tier 0–3.
  - At least one weapon, each a known weapon with count ≥ 1.
  - Vehicles need front armor > 0.
  - Every **enabled** unit must be produced by some structure.
- **Structures:**
  - Health > 0, footprint ≥ 1, build time > 0; sandbags need a length.
  - Produced units must exist and belong to the same faction. Requirements must exist.
- **Factions:** the HQ structure exists, is an HQ of the same faction, and produces an engineer. Starting units exist.
- **Maps:** valid size; at least 2 unblocked starts; unique, unblocked sector points; one HQ sector per player; at least one victory point.
- **AI:** all three difficulties exist; each faction has a plan; every opening step sets exactly one of build or produce, with known ids; mix units are valid.
- **Economy:** pop cap > 0; HQ income ≥ 0.

Any new field must be added in three places: `sim/Data/Definitions.cs`, the JSON, and the validator.

---

## 4. Movement, terrain, vision

### 4.1 Grid and pathfinding
- The map is a grid of 2 m cells. `ridge_outpost` is 150 × 150 cells (300 m × 300 m).
- `GridPathfinder` runs A* over walkable cells, without diagonal corner cutting, then smooths the path. A blocked target resolves to the nearest walkable cell.
- **Walkability:** buildings, walls, rocks and every structure except sandbags block movement. Fences and sandbags are walkable.
- Squads follow paths at their unit `speed`. Ground zones change vehicle speed only: vehicles move ×0.9 on rock (`rockVehicleSpeedMultiplier`) and ×0.8 on mud or farmland (`mudVehicleSpeedMultiplier`).
- Speed modifiers:
  - Suppressed: speed ×0.5 (`suppressedSpeedMultiplier`).
  - Pinned: speed 0, except while retreating.
  - Retreating: speed ×1.3 (`retreatSpeedMultiplier`).

### 4.2 Cover
- `CoverGrid` marks walkable cells next to cover sources. Each marked cell has a cover type and an outward normal.
- **Heavy cover:** next to buildings, walls, rocks, sandbags and structures.
- **Light cover:** next to fences.
- Placing or destroying a structure recomputes cover in its area.
- Cover protects only from the front. It applies when `dot(coverNormal, directionToAttacker) > coverDirectionThreshold` (0.2).
- Move orders snap infantry to cover. The snap weighs heavy cover twice as much as light cover.

### 4.3 Vision and fog of war
- `VisibilityGrid` keeps a per-player visible mask, updated every 2 ticks.
- Line of sight is blocked by map buildings and rocks at least `losBlockHeight` (2.5 m) tall, and by structures at least 2.5 m tall. Walls, fences and sandbags never block sight.
- A unit or structure sees within its `sight` radius (§8).
- `IsExplored` remembers every cell a player has ever seen. The presentation draws it as "remembered terrain" (dim, without enemy units).
- Combat target selection and the AI's enemy memory only use **visible** enemies.

---

## 5. Combat

### 5.1 Targeting
- Each squad looks for the **nearest visible enemy within max weapon range and with line of sight**.
- Priority by weapon kind:
  - `antiTank` and `tankGun` weapons prefer vehicles (vehicles first, then infantry).
  - All other weapons prefer infantry, and treat vehicles as lowest priority.
- If no squads are in range and the weapon's `vsStructure` ≥ 0.3, the squad targets the nearest enemy structure, but never sandbags.
- An explicit `AttackCommand` overrides automatic targeting. The squad approaches to 80% of max range.

### 5.2 Firing
- Weapons fire on a per-weapon cooldown, multiplied by a random factor of 0.9–1.1 each shot.
- **Per-model** weapons fire once per alive model. **Crew** weapons fire `count` times (count is shown as "crew ×N" in §8).
- **Setup weapons** need the squad to stand still for at least `setupTime` (`StationarySeconds ≥ SetupTime`). A weapon cannot fire while moving if it needs setup or if its `movingAccuracy` ≤ 0.
- A weapon does not fire at a vehicle if `penetration / rearArmor` < 0.05; it simply cannot hurt it.
- Retreating squads never fire.

### 5.3 Hit chance
Range bands: **near** is distance ≤ range/3, **mid** is ≤ 2/3 of range, **far** is beyond that.

```
hit = bandAccuracy(near|mid|far)
    × target.ReceivedAccuracy                      (unit stat, default 1)
    × (target is infantry ? vsInfantryAccuracy × coverMultiplier : 1)
    × (target retreating ? retreatReceivedAccuracy 0.5 : 1)
    × (shooter moving ? movingAccuracy (default 0.5) : 1)
    × (shooter pinned ? 0.5 : shooter suppressed ? 0.75 : 1)
clamped to [0, 0.98]
```

`coverMultiplier` is 0.5 in heavy cover and 0.75 in light cover. It applies only if the cover faces the attacker (§4.2).

### 5.4 Damage
- **Infantry hit:** deals `vsInfantryDamage` (if set) or `damage`. The damage lands on `modelsHitVsInfantry` random alive models; for example, the 120 mm gun hits 2 models.
- A model at 0 HP dies (`ModelKilled`). A squad with no models left is destroyed (`SquadDestroyed`).
- **Vehicle hit:**
  - Penetration chance = `penetration / armor`, capped at 1.
  - Front armor is used when `dot(vehicleFacing, directionToShooter) ≥ 0.3`; otherwise rear armor.
  - A penetrating hit deals `damage`. A failed roll emits `Deflected` and deals no damage.
- **Structure hit:** hit chance is 0.9 × the moving modifier. Damage is `damage × vsStructure` (default 0.05).

### 5.5 Suppression
- Each shot at infantry adds the weapon's `suppression` on a hit, or half of it on a miss (`missSuppressionFactor`). Suppression is capped at 1.
- Suppression decays by 0.12 per second (`suppressionDecayPerSecond`), starting 1.5 s (`suppressionDecayDelaySeconds`) after the last incoming fire.
- **Suppressed** at ≥ 0.5: speed ×0.5, own accuracy ×0.75.
- **Pinned** at ≥ 0.9: cannot move (unless retreating), own accuracy ×0.5.
- Vehicles are never suppressed: shots at vehicles add no suppression.

---

## 6. Support systems, territory, economy, production

### 6.1 Retreat
- The squad moves to the HQ center at ×1.3 speed.
- Incoming accuracy is ×0.5 and the squad cannot fire.
- Retreat ends within 14 m of the HQ (`retreatArriveDistance`) or when the path ends.

### 6.2 Reinforce
- Only within 30 m (`reinforceRadius`) of the HQ or a completed production building.
- Adds one model every 3 s (`reinforceSecondsPerModel`).
- Each model costs the unit's manpower cost divided by its squad size.
- Not available for vehicles or while retreating. Emits `ModelReinforced`.

### 6.3 Healing
Within 30 m of the HQ (`healRadius`), starting 5 s after the last damage (`healDelaySeconds`), each model heals 2% of its max HP per second (`healFractionPerSecond`).

### 6.4 Territory
- The map is divided into Voronoi **sectors**, each owned by the nearest sector point.
- A sector is captured by standing within 10 m (`captureRadius`) of its point. A base capture takes 20 s (`captureSeconds`).
- Each extra capturing squad adds +25% speed (`extraCapturerBonus`), up to ×2 (`maxCaptureMultiplier`).
- If squads from both sides are in range, the sector is **contested** and progress pauses.
- **Neutralize, then capture:** an enemy sector first drains from 1 to 0 (`SectorNeutralized`), then fills from 0 to 1 for the capturer (`SectorCaptured`).
- If nobody is in range, a partly drained owned sector regenerates at half rate.
- Vehicles and retreating squads cannot capture.
- **Supply:** a breadth-first search from the player's HQ sector through owned, adjacent sectors. Only connected sectors produce income.
- Sector types: `hq`, `victory` (VP), `fuel`, `munitions`, `standard`.

### 6.5 Economy and tickets (`economy.json`)

| Item | Value |
|---|---|
| Start | 400 MP / 40 MU / 15 FU, 500 tickets, pop cap 100 |
| HQ income per minute | 220 MP, 10 MU, 5 FU |
| Connected standard sector | +15 MP per minute |
| Connected munitions sector | +25 MU per minute |
| Connected fuel sector | +15 FU per minute |
| Upkeep | 1.5 MP per minute for each pop above 20 |
| `outpostBonus` (0.3) | Defined but **unused** in v0.1 |

- **Ticket drain** per second for each player = (best other player's VP count − own VP count) × 0.333 (`ticketDrainPerVpPerSecond`), applied only when positive.
- **Defeat:** tickets ≤ 0, or the player's HQ is destroyed (if they had one), or `SurrenderCommand`.
- **Victory:** the last player not defeated wins (`MatchEnded`, `Value` = winner).

### 6.6 Production
- Queue length is at most 5 (`maxQueueLength`).
- The full cost is deducted when a unit is queued. Cancelling refunds the full cost.
- Population counts queued units, so a full queue can hit the pop cap.
- Requirements: a structure is unlocked once a completed structure of the required id exists (`requires`, a tier chain HQ → T1 → T2 → T3).
- Units spawn at the walkable cell just outside the footprint on the side facing the map center, then walk to the rally point if one is set (`UnitProduced`).

### 6.7 Construction
- **Placement:**
  - On own territory. Sandbags may also go on neutral territory.
  - Every footprint cell must be walkable, contain no cover source and overlap no existing structure.
  - Blocking structures also need a 1-cell walkable margin and no squads inside the footprint.
- Orientation 0/1 swaps the footprint axes; in the UI, **Space** rotates.
- The cost is paid on placement. The structure starts at 10% HP (`StructurePlaced`).
- Each engineer squad within `buildRange + 0.5` (4.5 m) of the footprint adds `dt / buildTime` progress per tick, and these contributions stack. HP rises by 0.9 × MaxHP × each step. Completion emits `StructureCompleted`.
- Engineers (`engineer: true` units) are the only builders. Every faction starts with one engineer squad.
- **Sandbags:** 1 × 3 cells. They are walkable, don't block sight, and give heavy cover to the cells beside them.
- Other structures block movement in their footprint, and block sight if at least 2.5 m tall. A destroyed structure frees its cells.

---

## 7. Skirmish AI (`sim/AI/SkirmishAi.cs`, `game/data/ai.json`)

The AI sends ordinary commands through the same queue as the player. It owns a seeded `DeterministicRandom`.

### 7.1 Loop
- The AI thinks every `thinkInterval` seconds, with a per-player phase offset.
- **Enemy memory:** only enemies it has actually seen (fog respected). Each entry stores position, value and whether it is a vehicle, and expires after 60 s.
- **Unit value** = MP + MU + 2 × FU, scaled by remaining health.

### 7.2 Decisions in each think
1. **Retreat:**
   - When to retreat: the squad has health below `retreatHealthFraction`, or it is a squad of 3+ models down to 1 model.
   - Only if it took fire in the last 5 s and is not already at base.
   - Squads at base reinforce if the preset allows it.
2. **Engineers:**
   - Build the opening structures, and help build unfinished ones.
   - With `buildDefenses` and MP > 350, place sandbags at held VPs.
3. **Production:**
   - Follow the faction `opening` steps in order.
   - After the opening, idle producers pick from the weighted `mix`.
   - AT units get ×2.5 weight when known enemy armor is more than 25% of known enemy value.
   - Each production waits `productionDelay` seconds.
4. **Cappers** (up to `maxCappers`, the cheapest capable non-support squads):
   - Each picks the sector with the best score: type weight (fuel 3, munitions 2.5, VP 2, standard 1) × (adjacent to own territory ? 2 : 1) × (neutral ? 1.5 : 1) / (distance + 40).
   - Sectors with known enemies within 25 m are skipped.
5. **Army:**
   - **Attack** when army value ≥ the attack threshold AND army value ≥ `attackStrengthRatio` × known enemy value.
     - The threshold is `firstAttackArmyValue`, or ×0.6 of it after the first attack.
     - Attack objective: the nearest VP it doesn't own, otherwise the nearest enemy sector.
   - **Stop attacking** when the army falls below 0.6 × enemy value or below 0.35 × the first-attack threshold.
   - Otherwise **defend** the own VP nearest the enemy.
   - With `micro`, move orders snap to cover.

### 7.3 Presets

| Preset | Think s | Retreat HP | Attack ratio | First attack value | Max cappers | Prod. delay s | Reinforce | Micro | Defenses |
|---|---|---|---|---|---|---|---|---|---|
| Easy "Recruit" | 4.0 | 0.15 | 1.6 | 1400 | 1 | 10 | no | no | no |
| Normal "Veteran" | 2.5 | 0.25 | 1.35 | 1100 | 2 | 7 | yes | yes | yes |
| Hard "Elite" | 1.0 | 0.35 | 1.0 | 800 | 3 | 0 | yes | yes | yes |

No preset gets bonus resources or vision. Difficulty comes only from the parameters above.

### 7.4 Openings and mixes

| Faction | Opening (in order) | Mix weights after the opening |
|---|---|---|
| IDF | T1; Golani ×2; MAG; Engineers; T2; Golani; Spike; T3; Merkava | Golani 3, MAG 1.5, Sniper 1, Spike 1.5, Namer 1, Merkava 2 |
| Hamas | Fighters ×2; T1; PKM; RPG; T2; Fighters; Kornet; T3; Elite | Fighters 3, RPG 2, PKM 1.5, Al-Ghoul 1, Kornet 1.5, Elite 2 |
| Hezbollah | Fighters ×2; T1; PKM; RPG-29; T2; Kornet; ZU-23; T3; Radwan | Fighters 3, PKM 1.5, RPG-29 1.5, HS.50 1, Kornet 1.5, ZU-23 1, Radwan 2 |

---

## 8. Rosters (generated from `game/data`)

The tables below were generated from the JSON. Units marked *(deferred)* are defined but disabled, and no structure produces them.
- **Squad** is the number of models. HP is per model.
- **Pop** is population.
- **Armor F/R** is front and rear armor (vehicles only).
- In the weapons column, **per model** means one shot per alive model, and **crew ×N** means N shots per volley.

### 8.1 IDF

#### IDF units

| Unit | id | Category | Tier | Squad | HP/model | Cost MP/MU/FU | Pop | Build s | Speed m/s | Sight m | Armor F/R | Weapons | Built at |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Yahalom Combat Engineers | `idf_engineers` | infantry | 0 | 4 | 80 | 220/0/0 | 5 | 20 | 4.0 | 30 | — | Carbine (per model) | `idf_hq` |
| Oketz K9 Team *(deferred)* | `idf_k9` | support | 0 | 2 | 70 | 160/0/0 | 3 | 20 | 4.6 | 40 | — | Carbine (per model) | — |
| Golani Rifle Squad | `idf_golani` | infantry | 1 | 5 | 90 | 300/0/0 | 7 | 25 | 4.0 | 35 | — | Tavor X95 (per model) | `idf_t1` |
| MAG Machine Gun Team | `idf_mag_team` | support | 1 | 3 | 80 | 260/0/0 | 6 | 25 | 3.4 | 35 | — | FN MAG (crew x1) | `idf_t1` |
| Sniper Team | `idf_sniper` | infantry | 1 | 2 | 70 | 340/0/0 | 6 | 30 | 4.0 | 45 | — | Sniper rifle (crew x1) | `idf_t1` |
| Spike MR ATGM Team | `idf_spike_team` | support | 2 | 3 | 80 | 300/20/0 | 6 | 30 | 3.4 | 35 | — | Spike MR (crew x1) | `idf_t2` |
| Namer Heavy APC | `idf_namer` | vehicle | 2 | 1 | 1000 | 300/0/70 | 10 | 35 | 7.0 | 40 | 240/120 | .50 cal RWS (crew x1) | `idf_t2` |
| Merkava Mk.4M | `idf_merkava` | vehicle | 3 | 1 | 1300 | 480/0/180 | 16 | 50 | 6.0 | 40 | 320/150 | 120mm gun (crew x1); Coaxial MAG (crew x1) | `idf_t3` |
| D9R Armored Bulldozer *(deferred)* | `idf_d9r` | vehicle | 3 | 1 | 1400 | 300/0/70 | 8 | 20 | 4.0 | 30 | 300/200 |  | — |
| Maj. Ido "Ze'ev" Ronen *(deferred)* | `idf_hero_ronen` | hero | 1 | 4 | 120 | 380/40/0 | 9 | 20 | 4.4 | 40 | — | Tavor X95 (per model) | — |

#### IDF structures

| Structure | id | Kind | Tier | Cost MP/MU/FU | Build s | HP | Footprint (cells) | Height m | Sight m | Requires | Produces |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Brigade Forward HQ | `idf_hq` | hq | 0 | 0/0/0 | — | 3500 | 6 | 6 | 40 | — | `idf_engineers` |
| Infantry Company Post | `idf_t1` | production | 1 | 180/0/15 | 35 | 1500 | 5 | 4.5 | 30 | `idf_hq` | `idf_golani`, `idf_mag_team`, `idf_sniper` |
| Combat Support Base | `idf_t2` | production | 2 | 200/0/60 | 45 | 1800 | 5 | 5 | 30 | `idf_t1` | `idf_spike_team`, `idf_namer` |
| Armored Battalion Yard | `idf_t3` | production | 3 | 300/0/120 | 60 | 2200 | 6 | 6 | 30 | `idf_t2` | `idf_merkava` |

### 8.2 Hamas

#### Hamas units

| Unit | id | Category | Tier | Squad | HP/model | Cost MP/MU/FU | Pop | Build s | Speed m/s | Sight m | Armor F/R | Weapons | Built at |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Tunnel Diggers | `hamas_diggers` | infantry | 0 | 4 | 70 | 180/0/0 | 4 | 18 | 4.0 | 30 | — | Carbine (per model) | `hamas_hq` |
| Qassam Fighters | `hamas_fighters` | infantry | 0 | 6 | 70 | 220/0/0 | 5 | 20 | 4.2 | 35 | — | AK-47 (per model) | `hamas_hq` |
| Observer *(deferred)* | `hamas_observer` | support | 0 | 2 | 60 | 100/0/0 | 2 | 20 | 4.4 | 45 | — | Carbine (per model) | — |
| Yassin-105 RPG Team | `hamas_rpg_team` | support | 1 | 3 | 70 | 240/0/0 | 5 | 22 | 4.0 | 35 | — | Yassin-105 RPG (crew x1); AK-47 (per model) | `hamas_t1` |
| PKM MG Team | `hamas_pkm_team` | support | 1 | 3 | 70 | 220/0/0 | 5 | 22 | 3.6 | 35 | — | PKM (crew x1) | `hamas_t1` |
| Al-Ghoul Sniper | `hamas_ghoul_sniper` | infantry | 1 | 1 | 80 | 300/0/0 | 5 | 28 | 4.0 | 45 | — | Al-Ghoul 14.5mm (crew x1) | `hamas_t1` |
| Quadcopter Team *(deferred)* | `hamas_quadcopter` | support | 1 | 2 | 60 | 200/20/0 | 3 | 20 | 4.2 | 40 | — |  | — |
| Kornet ATGM Team | `hamas_kornet_team` | support | 2 | 3 | 70 | 320/30/0 | 6 | 30 | 3.4 | 35 | — | Kornet (crew x1) | `hamas_t2` |
| Mutabar MANPADS Team *(deferred)* | `hamas_mutabar_team` | support | 2 | 2 | 70 | 220/20/0 | 4 | 20 | 4.0 | 35 | — |  | — |
| Al-Qassam Elite Squad | `hamas_elite` | infantry | 3 | 5 | 95 | 400/0/0 | 8 | 35 | 4.4 | 38 | — | AK (elite) (per model); Yassin-105 RPG (crew x1) | `hamas_t3` |
| "Al-Khuld" (The Mole) *(deferred)* | `hamas_hero_khuld` | hero | 1 | 4 | 110 | 300/30/0 | 7 | 20 | 4.4 | 40 | — | AK (elite) (per model) | — |

#### Hamas structures

| Structure | id | Kind | Tier | Cost MP/MU/FU | Build s | HP | Footprint (cells) | Height m | Sight m | Requires | Produces |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Command Bunker | `hamas_hq` | hq | 0 | 0/0/0 | — | 3500 | 6 | 3 | 40 | — | `hamas_diggers`, `hamas_fighters` |
| Workshop | `hamas_t1` | production | 1 | 150/0/10 | 30 | 1300 | 5 | 4 | 30 | `hamas_hq` | `hamas_rpg_team`, `hamas_pkm_team`, `hamas_ghoul_sniper` |
| Arms Lab | `hamas_t2` | production | 2 | 200/0/40 | 40 | 1500 | 5 | 4 | 30 | `hamas_t1` | `hamas_kornet_team` |
| Elite Command | `hamas_t3` | production | 3 | 250/0/80 | 50 | 1800 | 5 | 3.5 | 30 | `hamas_t2` | `hamas_elite` |

### 8.3 Hezbollah

#### Hezbollah units

| Unit | id | Category | Tier | Squad | HP/model | Cost MP/MU/FU | Pop | Build s | Speed m/s | Sight m | Armor F/R | Weapons | Built at |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Combat Engineers | `hzb_engineers` | infantry | 0 | 4 | 75 | 200/0/0 | 4 | 20 | 4.0 | 30 | — | Carbine (per model) | `hzb_hq` |
| Resistance Fighters | `hzb_fighters` | infantry | 0 | 5 | 85 | 260/0/0 | 6 | 22 | 4.0 | 35 | — | AK-47 (per model) | `hzb_hq` |
| PKM MG Team | `hzb_pkm_team` | support | 1 | 3 | 80 | 240/0/0 | 5 | 24 | 3.4 | 35 | — | PKM (crew x1) | `hzb_t1` |
| RPG-29 Team | `hzb_rpg29_team` | support | 1 | 3 | 80 | 260/0/0 | 5 | 24 | 3.8 | 35 | — | RPG-29 (crew x1); AK-47 (per model) | `hzb_t1` |
| HS.50 Sniper | `hzb_hs50_sniper` | infantry | 1 | 2 | 70 | 320/0/0 | 6 | 28 | 4.0 | 45 | — | Steyr HS .50 (crew x1) | `hzb_t1` |
| Kornet ATGM Team | `hzb_kornet_team` | support | 2 | 3 | 80 | 320/25/0 | 6 | 30 | 3.4 | 35 | — | Kornet (crew x1) | `hzb_t2` |
| ZU-23-2 Technical | `hzb_zu23_technical` | vehicle | 2 | 1 | 320 | 260/0/25 | 6 | 30 | 9.0 | 35 | 15/12 | ZU-23-2 (crew x1) | `hzb_t2` |
| Radwan Commandos | `hzb_radwan` | infantry | 3 | 5 | 100 | 420/0/0 | 9 | 40 | 4.6 | 38 | — | AK (elite) (per model); RPG-29 (crew x1) | `hzb_t3` |
| Katyusha MLRS Truck *(deferred)* | `hzb_katyusha` | vehicle | 3 | 1 | 400 | 380/0/80 | 12 | 20 | 7.0 | 35 | 15/12 |  | — |
| "Al-Sayyad" (The Hunter) *(deferred)* | `hzb_hero_sayyad` | hero | 1 | 2 | 110 | 360/40/0 | 7 | 20 | 4.2 | 40 | — | Kornet (crew x1) | — |

#### Hezbollah structures

| Structure | id | Kind | Tier | Cost MP/MU/FU | Build s | HP | Footprint (cells) | Height m | Sight m | Requires | Produces |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Command Post | `hzb_hq` | hq | 0 | 0/0/0 | — | 3500 | 6 | 4.5 | 40 | — | `hzb_engineers`, `hzb_fighters` |
| Training Camp | `hzb_t1` | production | 1 | 180/0/15 | 35 | 1400 | 5 | 4 | 30 | `hzb_hq` | `hzb_pkm_team`, `hzb_rpg29_team`, `hzb_hs50_sniper` |
| Fortification Command | `hzb_t2` | production | 2 | 220/0/50 | 45 | 1800 | 5 | 4 | 30 | `hzb_t1` | `hzb_kornet_team`, `hzb_zu23_technical` |
| Strategic Unit HQ | `hzb_t3` | production | 3 | 300/0/100 | 55 | 2000 | 6 | 5 | 30 | `hzb_t2` | `hzb_radwan` |

#### Shared structures

| Structure | id | Kind | Cost MP/MU/FU | Build s | HP | Footprint x length (cells) | Height m | Blocks movement |
|---|---|---|---|---|---|---|---|---|
| Sandbags | `sandbags` | sandbags | 25/0/0 | 8 | 300 | 1 x 3 | 1 | No (walkable; heavy cover beside them) |

### 8.4 Weapons

| Weapon | id | Kind | Range m | Acc near/mid/far | Damage | vs-inf dmg | Cooldown s | Pen | Suppr. | Setup s | Moving acc | vs-inf acc | Models hit | vs struct |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Carbine | `carbine` | smallArms | 30 | 0.45/0.32/0.2 | 10 | — | 1.8 | 1 | 0.012 | 0 | 0.5 | 1 | 1 | 0.05 |
| Tavor X95 | `tavor` | smallArms | 35 | 0.55/0.42/0.28 | 12 | — | 1.6 | 1 | 0.015 | 0 | 0.5 | 1 | 1 | 0.05 |
| AK-47 | `ak47` | smallArms | 35 | 0.5/0.38/0.25 | 12 | — | 1.6 | 1 | 0.015 | 0 | 0.5 | 1 | 1 | 0.05 |
| AK (elite) | `ak47_elite` | smallArms | 35 | 0.58/0.45/0.32 | 13 | — | 1.5 | 1 | 0.018 | 0 | 0.5 | 1 | 1 | 0.05 |
| FN MAG | `mag` | machineGun | 40 | 0.45/0.35/0.25 | 8 | — | 0.35 | 3 | 0.15 | 2.5 | 0.0 | 1 | 1 | 0.05 |
| PKM | `pkm` | machineGun | 40 | 0.43/0.33/0.23 | 8 | — | 0.35 | 3 | 0.15 | 2.5 | 0.0 | 1 | 1 | 0.05 |
| Sniper rifle | `barrett` | sniper | 50 | 0.9/0.85/0.8 | 110 | — | 5.0 | 15 | 0.1 | 0 | 0.2 | 1 | 1 | 0.05 |
| Al-Ghoul 14.5mm | `ghoul` | sniper | 50 | 0.85/0.8/0.72 | 110 | — | 5.5 | 25 | 0.1 | 0 | 0.2 | 1 | 1 | 0.05 |
| Steyr HS .50 | `hs50` | sniper | 50 | 0.88/0.82/0.75 | 110 | — | 5.5 | 20 | 0.1 | 0 | 0.2 | 1 | 1 | 0.05 |
| Spike MR | `spike_mr` | antiTank | 50 | 0.8/0.75/0.7 | 280 | 60 | 9.0 | 330 | 0.15 | 3.0 | 0.0 | 0.3 | 1 | 1.0 |
| Kornet | `kornet` | antiTank | 55 | 0.8/0.75/0.7 | 280 | 60 | 9.0 | 340 | 0.15 | 3.0 | 0.0 | 0.3 | 1 | 1.0 |
| Yassin-105 RPG | `yassin105` | antiTank | 30 | 0.6/0.45/0.32 | 200 | 60 | 7.0 | 240 | 0.2 | 0 | 0.4 | 0.4 | 1 | 1.0 |
| RPG-29 | `rpg29` | antiTank | 30 | 0.62/0.47/0.34 | 210 | 60 | 7.0 | 260 | 0.2 | 0 | 0.4 | 0.4 | 1 | 1.0 |
| 120mm gun | `merkava_120` | tankGun | 45 | 0.7/0.6/0.5 | 260 | 85 | 5.0 | 360 | 0.3 | 0 | 0.6 | 0.8 | 2 | 1.0 |
| Coaxial MAG | `merkava_coax` | machineGun | 40 | 0.4/0.3/0.2 | 8 | — | 0.35 | 3 | 0.1 | 0 | 0.7 | 1 | 1 | 0.05 |
| .50 cal RWS | `namer_rws` | autocannon | 40 | 0.55/0.45/0.35 | 14 | — | 0.45 | 22 | 0.1 | 0 | 0.8 | 1 | 1 | 0.3 |
| ZU-23-2 | `zu23` | autocannon | 42 | 0.5/0.4/0.3 | 18 | — | 0.4 | 30 | 0.12 | 0 | 0.5 | 1 | 1 | 0.3 |

### 8.5 Faction identity in play (v0.1)
- **IDF:** expensive, durable squads with better rifles. Armor is the late-game power spike (Namer at T2, Merkava at T3). Combat engineers start on the field.
- **Hamas:** cheap, large infantry squads. AT spread across tiers (RPG at T1, Kornet at T2, Elite with RPG at T3). Fastest and cheapest tech. **No vehicles.**
- **Hezbollah:** sturdier infantry than Hamas. Long-range Kornet. A fast but fragile ZU-23 technical. Radwan commandos at T3.

---

## 9. Map: Border Ridge Outpost (`maps/ridge_outpost.json`)

- **Size:** 150 × 150 cells of 2 m (300 m × 300 m), for 2 players.
- **Mirroring:** the file defines one half. Every element without `"center": true` is duplicated by a 180° rotation (cell x → 149 − x, and the same for y), so both sides are identical.
- **Start:** player 0's HQ at world (30, 30) m; player 1's mirrored at (270, 270).
- **19 sectors:** 2 HQ, 3 VP, 2 fuel, 2 munitions, 10 standard. `vp_center` at (150, 150) is not duplicated. Per side:
  - `vp_flank` (74, 226)
  - `fuel` (60, 140)
  - `munitions` (140, 60)
  - five standard sectors: `s_west`, `s_south`, `s_mid`, `s_ridge_w`, `s_ridge_s`
- **Obstacles:** cell rectangles with a height and a kind (`building`, `wall`, `fence`, `rock`). Buildings and rocks block line of sight from 2.5 m upward. Walls and fences are low cover.
- **Ground zones:** `rock` and `farmland` rectangles. They are visual, and they change vehicle speed (§4.1).
- **Design reference:** `docs/05-maps.md`.

---

## 10. Player-facing spec (presentation; rebuild per engine)

### 10.1 Scenes and flow
1. **Main menu** (`UI/MainMenu.cs`):
   - Faction pick for you and the enemy; difficulty; seed; map.
   - Volume slider, saved to `user://settings.cfg`, plus a sound-test row.
   - A build stamp read from `build_info.json`.
2. **Match** (`Match/MatchController.cs`): hosts the sim and AI, and builds the world and UI in code.
3. **Pause menu** (Esc when nothing is selected): Resume, Surrender, Main menu, and a volume slider.
4. **End screen:**
   - VICTORY or DEFEAT, and how the match ended.
   - Stats: match time; squads fielded, lost and enemy squads destroyed; sectors held.
   - Buttons: "Play again" (new seed) and "Main menu".

### 10.2 Controls

| Input | Action |
|---|---|
| LMB click or drag | Select own squads; a box select replaces the selection |
| Shift + LMB | Add to the selection |
| Double-click | Select all on-screen squads of the same type |
| Click a structure | Select it (shows its production card) |
| RMB on ground | Move (infantry snap to cover) |
| RMB on an enemy squad or structure | Attack |
| RMB on an own unfinished structure with engineers selected | Assist construction |
| RMB with only a structure selected | Set rally point |
| Ctrl + A | Select all own squads |
| Ctrl + 1–9 / 1–9 | Set / recall a control group |
| R / T / H | Retreat / Reinforce / Stop (hold) |
| B | Build menu (engineers). Esc goes back |
| Command-card slots | Hotkeys Z X C V B N M G, left to right |
| Placement | Ghost follows the mouse. Space rotates, LMB places, Shift + LMB keeps placing, RMB or Esc cancels |
| Esc | Cancel placement, then clear the selection, then pause |
| WASD or arrows | Pan |
| Screen edge (8 px) | Pan |
| Q / E | Rotate the camera (90°/s) |
| Mouse wheel | Zoom (distance 20–260 m, default 58, ×0.88 or ×1.12 per notch, smoothed) |

The camera pitch is fixed at 55°.

### 10.3 HUD (compact corner panels; `UI/Hud.cs`)

| Position | Panel |
|---|---|
| Top-left | Resources: MP, MU and FU, each with an icon and income per minute; population and cap |
| Top-center | Two ticket bars, with a diamond for each VP colored by owner |
| Top-right | Match clock and the menu button |
| Bottom-left | 176 px minimap with fog, sectors, units and the camera frame. Left-click or drag moves the camera; right-click moves the selected squads |
| Bottom-center | Selection card, shown only when something is selected. For one squad: name, per-soldier health segments, and state (Retreating, PINNED, Suppressed). For several: chips (click one to select only that squad). For a structure: HP and queue chips (click a chip to cancel with a full refund) |
| Bottom-right | Command card: 62 px square buttons with an icon, hotkey, short name and cost, plus a tooltip. A button dims and disables when its action is unaffordable, locked or blocked by the pop cap |
| Bottom-center (when nothing is selected) | Hint line with the basic controls |
| Under the ticket bars | Message feed: up to 4 lines, each shown for 4 s. Red: rejected commands with the reason ("Not enough resources", "Requires …"), "Sector lost", "We are losing a victory point!", "… destroyed", "… lost". Green: "… ready", "… complete", "Sector captured", "Victory point captured" |

### 10.4 Overlays and world UI
- **On squads:** a team-colored ring decal, a health bar, and a **RETREAT**, **PINNED** or **SUPPRESSED** tag.
- **On structures:** construction progress.
- **Capture:** a progress ring at each sector point.
- **Territory:** borders drawn analytically at 4 texels per cell and tinted by owner.
- **Fog of war:** a blurred mask. Explored but not visible areas are dimmed; unexplored areas are dark. Enemy units are hidden outside vision.
- **Cover preview:** with infantry selected, the cursor shows where a move would snap. Labels: "heavy cover" in green, "light cover" in yellow, "open" in red.
- **Markers:** move and rally clicks leave a short-lived marker.
- **Team colors:** player 0 blue (0.25, 0.52, 0.95); player 1 red (0.90, 0.30, 0.22).

### 10.5 Audio and effects
- **Sound:** v0.1 synthesizes all sound effects at startup (`Audio/SoundSynth.cs`).
  - Sounds: rifle, machine gun, sniper, cannon, rocket launch, explosion, each with variants.
  - Recordings in `game/assets/audio/{name}.wav|.ogg` override the synthesized ones.
  - Playback uses a pool of 32 3D voices with per-frame caps and plays only for events visible to the player.
  - Impacts are delayed by flight time: 0.12 s for a tank shell, 0.3 s for a rocket.
- **Effects** (`Vfx/VfxManager.cs`):
  - Muzzle flashes and tracers on `ShotFired`.
  - Rocket and ATGM smoke trails.
  - Explosions with flash light, fire, smoke puffs, debris chunks, a thin shockwave ring, and a ground scorch decal.
  - Destroyed vehicle: two explosions, then fire and smoke for 30 s.
  - Destroyed structure: three staggered explosions, then fire and smoke for 18 s.
  - Ricochet sparks on `Deflected`.
  - Effects play only where the local player has vision.
  - Dead soldiers fall and fade (`Views/CorpseAnimator.cs`).

### 10.6 Visual identity (procedural low-poly; `game/scripts/Visual/`)
- **IDF:**
  - Structures:
    - HQ: containers, T-walls and an antenna.
    - T1: watchtower and tent.
    - T2: radome.
    - T3: Quonset hangar.
  - Soldiers wear covered helmets and carry Tavors.
  - Vehicles: Merkava and Namer.
- **Hamas:**
  - Structures:
    - HQ: concrete house with water tanks, rebar and a tunnel shaft.
    - T1: rusty workshop.
    - T2: damaged two-story building with a rocket rack.
    - T3: walled compound with a pergola.
  - Soldiers wear balaclavas or headbands and carry AKs. Diggers wear hard hats with headlamps.
  - Vehicles: none.
- **Hezbollah:**
  - Structures:
    - HQ: limestone farmhouse with red tiles, a bunker mound and an antenna.
    - T1: camo canopy, A-frame, tires and targets.
    - T2: bermed facade with firing slits and a trench.
    - T3: tunnel portal in a rock hill, with blast doors.
  - Soldiers wear camo and caps.
  - Vehicles: the ZU-23 technical.
- **World:**
  - Triplanar procedural detail textures, faceted rocks, and MultiMesh grass and pebbles.
  - Mediterranean plants: olive, cypress, palm, bush and dry shrub.
  - Map buildings in several styles, with balconies, damaged corners and rooftop clutter.
- **Content rules** (`docs/01`): no civilian NPCs, no religious-building shapes, fictional heroes, and faction names through localization keys.

### 10.7 Launch options
The options are listed in `CLAUDE.md` ("Launch options"). Three matter most for a port:
- `--smoke-test N` with optional `--full-match`: the determinism check.
- `--showcase`: the art gallery.
- `--demo --fast-forward N --screenshot PATH`: visual review.

A port should keep equivalents of all three.

---

## 11. Tests and tools

| Test file | Tests | Covers |
|---|---|---|
| `CoreTests` | 9 | RNG determinism and ranges; command ordering by tick, player and sequence; HQ income; upkeep; starting resources |
| `WorldTests` | 16 | Data loading and validation; map mirroring and reachability; walkability; heavy, light and directional cover; cover snap; vision radius; line-of-sight blockers; explored memory |
| `PathfindingTests` | 7 | Straight paths, routing around walls, unreachable goals, no corner cutting, deterministic and smoothed paths, blocked targets |
| `CombatTests` | 16 | Auto-engage; cover; retreat accuracy; MG suppression and pinning; setup weapons; line of sight; armor; rifles ignoring tanks; AT priority; wiped squads; attack orders; cover snap; retreat; reinforce; heal |
| `TerritoryProductionTests` | 18 | Starting ownership; capture time; neutralize-then-capture; contested sectors and vehicles; connected-only income; ticket drain; victory by tickets, HQ loss or surrender; production, rejects, pop cap, refunds; engineers and unlocks; placement rules; sandbags; destroyed structures; skirmish setup for each faction pair |
| `AiTests` | 5 | Full AI match to victory, AI determinism, few rejected commands, every difficulty present in the data |
| `SimulationTests` | 12 | Movement timing, foreign-command rejection, formation, interpolation data, identical and different hashes, opposing forces advancing, data validation, the showcase |

- **MatchRunner** plays AI-vs-AI batches. It reports win rates, match lengths and per-unit stats. `--verify-determinism` replays each match and compares hashes.
- **Determinism check for a new host:** run the setups in §1.4 and compare the printed hash.

---

## 12. Known issues and balance state (v0.1)

**Balance**
- Hamas is strong in AI-vs-AI play. Cheap six-model squads and early RPGs outtrade the IDF before armor arrives. This has not been tuned yet ("game first, balance later").
- The docs/04 target "Hard beats Easy more than 70% of the time" has not been measured.

**Unbuilt or unused**
- `outpostBonus` exists in `economy.json` but is unused.
- Deferred units are defined but disabled: IDF K9, D9R and the hero Ronen; Hamas Observer, Quadcopter, Mutabar and the hero Al-Khuld; Hezbollah Katyusha and the hero Al-Sayyad.
- Not built yet: veterancy, heroes, doctrines, and the signature systems (tunnels, Iron Dome, Trophy, Intel). See docs 02, 03 and 09.

**Not verified**
- Windows has not reproduced the reference hashes yet.
- Mouse-and-keyboard play on Windows has only been checked by the user's own test runs.

**Presentation**
- Software-rendered screenshots run at a few fps. Effect time is capped at 1/30 s per frame, so capture after 25 or more frames.
- Music and voices don't exist yet (docs/06).
