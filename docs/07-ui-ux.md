# 07 — UI / UX

## HUD layout (CoH convention)
```
┌──────────────────────────────────────────────────────────────────┐
│ [MP] [MU] [FU] [CP] [INT|RS]   Pop 42/100   Tickets 412 | 388    │  ← top bar
│                                                                  │
│                                                                  │
│                       (world view)                               │
│                                                                  │
│ ┌────────┐ ┌──────────────────────────────┐ ┌───────────────────┐│
│ │MINIMAP │ │ Unit card: portrait, squad   │ │ Command card      ││
│ │        │ │ HP, vet chevrons, weapons,   │ │ (abilities,       ││
│ │        │ │ upgrades, hero level ring    │ │ production, build)││
│ └────────┘ └──────────────────────────────┘ └───────────────────┘│
│ [Doctrine abilities]                        [Hero portrait]      │
└──────────────────────────────────────────────────────────────────┘
```
- **Faction resource slot:** INT gauge for the IDF, RS gauge for Hezbollah, and a tunnel-network icon (entrance count / capacity) for Hamas.
- **Unit card extras:** Trophy charges (Merkava), interceptor stock (Iron Dome), ERA status (T-72).

## Controls
- **Grid hotkeys:** the command card maps to QWER / ASDF / ZXCV regardless of language.
- **Standard RTS controls:** drag-select, double-click to select the same type, Ctrl+# control groups, Shift-queue, attack-move (A), reverse move for vehicles (hold Ctrl + right-click, so the front armor stays toward the enemy), retreat (R), reinforce (hotkey).
- **Tactical map (Tab):** full-screen map with sectors, VPs, known enemy assets and ability targeting.
- **Camera:** WASD/edge pan, rotate (middle mouse), zoom with min/max clamps, F-keys to jump to HQ, last alert and hero.

## Information overlays
| Overlay | Trigger |
|---|---|
| Cover preview (green / yellow / red markers) | While issuing a move order |
| Weapon range and firing arc | Hovering or selecting emplaced weapons and vehicles |
| Detection radius | Selecting detectors (K9, towers, drones) |
| **Tunnel network** (entrances + links) | Hamas player: always available; hold Alt for a full view |
| **Iron Dome coverage** | IDF player when selecting a battery; enemy sees it only after spotting the battery |
| AA coverage (known enemy) | When targeting air call-ins |
| Artillery / strike target circles | Visible to the targeted side as a warning (CoH convention, ~2 s before impact) |
| Sector supply connection | Tactical map: disconnected sectors are greyed out |

## Alerts
Minimap pings and a bark/radio line for these events:
- VP under attack
- Unit under attack while off screen
- Construction or production complete
- Hero down
- Incoming rocket warning (IDF only, via radar)
- Tunnel entrance discovered (Hamas)
- Rocket Depot discovered (Hezbollah)

## Pre-match flow
Main menu → Skirmish → choose map → choose faction, doctrine and hero (loadout screen with a doctrine-tree preview) → choose AI faction (or random), AI difficulty and the AI's doctrine/hero (or random) → choose win condition → launch.

## Localization
- **UI languages:** English in V1; Hebrew and Arabic UI in V1.1. Subtitles for the native-language VO are English from V1.
- **RTL support:** Hebrew and Arabic need right-to-left layout, and Arabic needs **letter shaping**. Godot 4 supports both natively (TextServer with HarfBuzz/ICU, BiDi and Control `layout_direction`), so no plugin is needed. With Unity, RTLTMPro would be required. Mirrored layouts apply to menus only; the HUD keeps its fixed positions so control muscle memory stays the same.
- All strings go through Godot's translation system (CSV/PO) from day 1, even before Hebrew/Arabic ship.
- **Fonts:**
  - Hebrew: Heebo / Assistant
  - Arabic: Noto Kufi / Cairo
  - Latin: Inter or a military-stencil display font for headers
- All in-game text, including faction names and emblem references, comes from localization keys (this supports regional variants; see [01](01-vision-and-scope.md)).

## Accessibility
- Colorblind-safe team colors (blue/orange default, plus presets)
- Subtitle size and background options
- Full key rebinding
- UI scale 80–150%
- Screen-shake toggle
- Bark volume separate from SFX volume

## Onboarding
**Solo V1:** rich tooltips (unit role, strengths, counters), a one-screen **faction primer** per faction, and "first match" hints (contextual pop-ups the first time you see suppression, tunnels and so on). The scripted tutorial below is post-V1.

### Tutorial — "Basic Training" (post-V1)
A scripted 10–15 minute map covering:
1. Select and move; cover
2. Suppression and retreat
3. Capture and economy
4. Build T1; reinforce
5. Vehicles and armor facing
6. Detection and camouflage
7. A doctrine ability
8. A skirmish vs Easy AI

Played as the IDF by default. One extra "faction primer" card per faction explains its unique mechanic.
