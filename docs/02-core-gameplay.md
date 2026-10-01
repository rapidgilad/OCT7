# 02 — Core Gameplay

All numbers are **initial tuning values**. Balance changes are made through data tables (see [08](08-engine-and-technology.md)), never in code.

## 1. Economy

### Resources
| Resource | Source | Spent on |
|---|---|---|
| **Manpower (MP)** | HQ base income + standard sectors | Infantry, buildings, reinforcements, hero deployment |
| **Munitions (MU)** | Munitions sectors | Weapon upgrades, abilities, special weapons |
| **Fuel (FU)** | Fuel sectors | Vehicles, tech tiers, tunnels, fortifications |
| **Command Points (CP)** | Combat XP earned by all units (shared pool) | Unlocking doctrine abilities (ranks 1–5) |
| **Intel (INT)** — IDF only | Drones, camera towers, spotting, kills | Locking precision strikes, revealing tunnel segments |
| **Rocket Stockpile (RS)** — Hezbollah only | Rocket Depots over time | Rocket barrages and heavy rockets |
| *Tunnel Network* — Hamas only | Mechanic, not a currency | See [Hamas](factions/hamas.md) |

### Income (per minute)
| Source | MP | MU | FU |
|---|---|---|---|
| HQ base | 220 | 10 | 5 |
| Standard sector | +15 | — | — |
| Munitions sector | — | +25 | — |
| Fuel sector | — | — | +15 |
| Outpost on a sector | +30% of that sector's yield | | |

- **Starting resources:** 400 MP / 40 MU / 15 FU.
- **Pop cap:** 100 for all factions.
- **Upkeep:** each pop point above 20 reduces MP income by 1.5/min (about −120 MP/min at 100 pop). This punishes floating a huge army and rewards trading efficiently.
- **Pop philosophy:** IDF squads cost 7–8 pop, Hamas squads 4–6, Hezbollah squads 5–6. At max pop, Hamas fields roughly 1.5× the IDF's unit count.

## 2. Territory & Victory
- The map is divided into **sectors**. Each one is a capture point plus an area polygon.
- A sector yields income only when it is **connected** to your HQ through a chain of owned sectors. Cutting the chain starves the enemy.
- **Capture:** infantry captures (vehicles cannot). The base rate is 20 s for a neutral sector and 30 s for an enemy one. Motorcycle Raiders and elite units capture 25% faster.
- **Victory Points (VPs):** 3 per map. Each side starts with **500 tickets**. The side holding more VPs drains the other's tickets by 1 per 3 s for each VP of difference.
- **Win conditions:**
  - **Default:** tickets reach 0, or the enemy HQ is destroyed.
  - **Optional:** Annihilation (all enemy units and production destroyed).

## 3. Combat Model
CoH-style probabilistic combat, resolved by a deterministic simulation at a **fixed 8–10 Hz tick**.

### Hit resolution
```
HitChance = WeaponAccuracy(range band)
          × TargetReceivedAccuracy
          × CoverModifier
          × MovingModifier            (shooter moving: 0.5 for most rifles, 1.0 for LMG/Negev "assault" weapons)
          × VeterancyModifiers
```
- Weapons have **near / mid / far** accuracy bands and per-band damage and cooldown.
- **Area weapons** (grenades, mortars, rockets) roll a scatter ellipse, then apply radial damage falloff.

### Cover
| Cover type | Accuracy multiplier against target | Notes |
|---|---|---|
| Heavy (walls, rubble, sandbags, trenches) | 0.5 | Also −25% suppression received |
| Light (fences, hedges, light debris) | 0.75 | |
| Open | 1.0 | |
| Negative (craters, water, moving in the open) | 1.25 | |
| Garrisoned building | 0.6 | Worsens with damage; collapse kills or ejects occupants |
| **Rubble — Hamas trait** | 0.45 | Hamas squads also gain camo 2 while stationary in rubble |

Cover comes from **cover nodes** generated from tagged props. When a squad is given a move order, the destination shows a green/yellow/red preview.

### Suppression
- Each squad has a suppression meter (0–100). MGs, explosions and sustained fire fill it; it decays over time.
- **Suppressed (≥ 50):** −50% speed, −25% accuracy, cannot sprint.
- **Pinned (≥ 90):** cannot move except to retreat; −50% accuracy.
- Snipers and ATGMs do not suppress. MGs, mortars and rockets do.

### Retreat & reinforce
- Any squad can **retreat**: received accuracy ×0.5, +30% speed, and it ignores other orders until it arrives.
- **Retreat points:**

  | Faction | Retreat points |
  |---|---|
  | IDF | HQ, Field Aid Station, Namer APC |
  | Hamas | Command Bunker, or **any tunnel entrance** (the nearest one is chosen) |
  | Hezbollah | Command Post, bunkers |

- **Reinforce:** squads buy back lost members while near a retreat point or friendly structure in connected territory. Reinforcing does not lose veterancy.

### Vehicles
- **Directional armor:** front / side / rear values. Penetration chance = clamp(Penetration ÷ Armor, 5%, 100%).
- **Critical hits** (rolled on penetration when HP < 50%):

  | Critical | Effect |
  |---|---|
  | Engine damaged | −50% speed |
  | Immobilized | Cannot move until repaired |
  | Main gun damaged | Cannot fire main gun until repaired |
  | Crew stunned | Cannot act for 5 s |
  | Fire | Damage over time until extinguished or repaired |

- Engineers repair vehicles and structures.
- Abandoned vehicles: not in V1.

### Interceptor system (shared component)
| System | Owner | Intercepts | Chance | Ammo / limits | Counters |
|---|---|---|---|---|---|
| **Trophy APS** | IDF Merkava | RPGs, ATGMs | 85% (40% vs top-attack) | 3 charges, +1 per 45 s out of combat | Saturation (Double-Tap, multiple RPGs at once), top-attack (Almas, quadcopter drop), EFP/IEDs, mines, large blasts |
| **Iron Dome** | IDF structure | Rockets, mortars | 90% rockets / 50% mortars / 60% heavy rockets | 12 interceptors, restock 5 MU each | Saturation salvos, destroying the battery, drones |
| **Iron Beam** | Iron Dome upgrade | Drones, mortars, single rockets | 95% per engagement | No ammo; 6 s cycle per target | Saturation (one target per cycle) |
| **Kontakt-1 ERA** | Hezbollah T-72 | First ATGM/RPG hit | 100% negates the first hit | 1 use per tile set; re-armed for 30 MU | Follow-up shots |

### Detection & camouflage
- Each unit has **Camo (0–3)** and **Detection (0–3)** values.
- An enemy unit is revealed when a unit with Detection ≥ its Camo is within detection radius, or when it fires. Firing reveals the shooter for 3 s; some elite units are exempt on their first shot.

| Detector | Detection level | Radius |
|---|---|---|
| Standard infantry | 1 | 35 m |
| Hummer / Observer / Forward Observer | 1 | 45 m |
| Oketz K9 | 3 | 25 m |
| Drones (Skylark, Hermes, Mersad, quadcopter) | 2 | 30–50 m circle |
| Camera Observation Tower | 2 | 50 m |
| Hero: IronVision (IDF) | 3 | 30 m |

- **Camo sources:**
  - Stationary in a building or rubble (Hamas): 2
  - Camouflage nets (Hezbollah): +1
  - Elite units: 1–2 innate
  - Tunnel entrances: 3

### Air call-ins & anti-air
- Air call-ins fly a real path, with an entry vector chosen by the player.
- **AA units** (MANPADS, ZU-23, "358" SAM) can damage or abort call-ins in their radius:
  - Apache: can be destroyed or forced to abort.
  - Drones: destroyed.
  - Fast jets: only the "358" can force a 50% chance to abort.

### Destruction
- Buildings have 3–4 damage states. At 0 HP they collapse into **rubble**, which is heavy cover, partially blocks vehicles and triggers a navmesh update.
- The **D9R** bulldozer clears rubble and creates vehicle paths through it.
- Tunnels are not damaged by surface destruction, except by the GBU-28 Bunker-Buster and the Hamas Tunnel Collapse.

### Ground type
Each map zone is tagged with a ground type:

| Ground | Tunnels (Hamas) | Bunkers (Hezbollah) | Vehicles |
|---|---|---|---|
| Sand / urban | Cost ×1.0, build time ×1.0 | HP ×1.0 | Normal |
| Rock | Cost ×2.0, build time ×2.0 | **HP ×1.3** | −10% speed off-road |
| Mud / farmland | Cost ×1.2 | HP ×1.0 | −20% speed off-road |

## 4. Base Building
- Every faction builds with its **engineer unit**. Production structures, defenses and faction-unique structures may only be placed in owned territory, except mines and booby traps.
- **Tech progression:** HQ → T1 → T2 → T3 → T4. Each tier requires the previous one.
- **Building costs:** MP + FU, scaled by tier.
- **Upgrades:** global upgrades are bought at buildings (for example, IDF "Hard Training", Hamas "Rapid Excavation").
- Full trees are in the faction documents.

## 5. Counter Matrix
| Threat | Primary counters |
|---|---|
| IDF air power (Apache, drones, jets) | Hezbollah MANPADS / ZU-23 / "358" SAM; Hamas Mutabar MANPADS |
| IDF Merkava | Saturation (Kornet Double-Tap, multiple RPG teams), top-attack (Almas, quadcopter drop), Shuath EFP / IEDs, mines |
| Hamas tunnels & stealth | Oketz K9, drones, camera towers with seismic sensor, D9R, GBU-28, Intel reveals |
| Rocket and mortar barrages | Iron Dome / Iron Beam, counter-battery Intel strikes on launch cells and depots |
| Hezbollah fortifications | M109 artillery, MATADOR rockets, D9R, Pereh NLOS, precision strikes |
| Mass cheap infantry (Hamas) | MAG/PKM MGs, mortars, Eitan 30mm, Merkava APAM rounds |
| Snipers | Counter-snipers, drones, light vehicles, mortars |
| IDF Intel economy | Killing drones (MANPADS), denying vision, destroying camera towers |
| Hezbollah RS economy | Finding and destroying Rocket Depots with Intel + strikes |
