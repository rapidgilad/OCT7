# 05 — Maps

## Rules for all V1 maps
- **Format:** 1v1, about **400 × 400 m** playable area.
- **Fairness:** mirrored or rotationally symmetric layouts. Each map is checked by AI-vs-AI per side to be within 50% ± 3% (see [04](04-ai-and-difficulty.md)).
- **Sectors:**

  | Sector type | Count |
  |---|---|
  | Victory points | 3 |
  | Fuel sectors | 2 |
  | Munitions sectors | 2 |
  | Standard sectors | 8–12 |
  | Base sector per player | 1 |

- **Ground-type zones:** sand/urban, rock or mud/farmland. These change tunnel and bunker costs and vehicle speed (see [02](02-core-gameplay.md)).
- **Elevation:** high ground gives +10% weapon range and +20% sight radius. Line of sight is blocked by terrain and buildings.
- **Neutral buildings:** garrisonable and destructible into rubble. Religious buildings and hospitals are non-interactive set dressing (see [01](01-vision-and-scope.md)).
- **No civilian NPCs.** Ambient life is limited to animals and environmental effects (dust, smoke, wind on vegetation).

> **Solo V1:** ship **2 maps**. Build **Border Ridge Outpost** first (balanced, ideal for tuning), then **Khan Sahil** (urban showcase, tunnels, destruction). The others are post-V1. Map size can start at 300 × 300 m to reduce art load.

## V1 map pool
### 1. Khan Sahil — dense urban (Hamas "home turf")
- Dense 3–5-story concrete blocks, narrow alleys, one central boulevard that vehicles can use, and a market square (the central VP).
- **Ground:** sand/urban, so tunnels are cheap.
- **Flow:** short sightlines; garrison fights; rubble grows through the match and changes the map.
- **Design goal:** IDF must use D9R, MATADOR and K9 to crack the city. Hamas must avoid being cornered when its tunnels are revealed.

### 2. Greenhouse Belt — farmland edge
- Rows of greenhouses (destructible, light cover), orchards, irrigation canals, a small pump station (fuel) and a farm compound (VP).
- **Ground:** mud/farmland and sand.
- **Flow:** long sightlines across fields. ATGM duels, armor maneuvers and flanking through orchards.
- **Design goal:** favors IDF armor and Hezbollah ATGMs. Hamas must use canals and orchards for concealment.

### 3. Wadi Safra Ridge — southern Lebanon hills (Hezbollah "home turf")
- Terraced hills, olive groves, a ridge-top village (VP), a dry riverbed (wadi) cutting through the middle, and caves.
- **Ground:** rock, so tunnels are expensive and bunkers get +30% HP.
- **Flow:** fighting over elevation. Kornet nests on ridges; the wadi is a covered approach for flanks.
- **Design goal:** IDF must combine smoke, artillery and air (vs AA) to take the high ground.

### 4. Border Ridge Outpost — mixed
- A border fence line, two **neutral capturable outposts** (pre-built fortifications any faction can capture and garrison), forest and a road network.
- **Ground:** mixed rock and soil.
- **Flow:** fights over the capturable fortifications. Forest gives camouflage.
- **Design goal:** a balanced, readable "tournament" map for all matchups.

### 5. (Stretch) Litani Crossings — river map
- A river with 3 crossings (2 bridges and 1 ford). Bridges are destructible and repairable by engineers.
- **Flow:** chokepoints, bridge demolition, ford ambushes.

## Map production pipeline
1. **Paper layout** (sectors, lanes, cover density) → designer review.
2. **Grey-box** in engine → AI-vs-AI fairness pass + internal playtests.
3. **Art pass** with a modular kit (urban, farmland and hills kits shared across maps).
4. **Destruction and navmesh validation:** every building's rubble state is tested for pathing.
5. **Performance pass:** draw calls and shadow budget per map.

## Shared environment kits
| Kit | Contents |
|---|---|
| Urban concrete | Modular block buildings (3–5 floors) with damage states, rebar rubble, cars (destructible), walls |
| Farmland | Greenhouses, orchards, canals, farm compounds, pump stations |
| Lebanon hills | Terraces, stone houses, olive trees, rock outcrops, caves |
| Military | Outposts, border fence, sandbags, concrete T-walls, watchtowers |
