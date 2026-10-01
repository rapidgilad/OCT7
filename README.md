# Iron Swords: Frontlines — V1 Game Design (codename OCT7)

A squad-based tactical RTS in the Company of Heroes tradition, set in modern Middle East warfare. It has three asymmetric factions:

| Faction | Identity | Signature systems |
|---|---|---|
| **IDF** | Precision & combined arms | Merkava + Trophy APS, Iron Dome / Iron Beam, Intel-driven air strikes, K9 & drones |
| **Hamas** | The Underground | Tunnel network, ambush & stealth, IED/EFP, rockets, quadcopters |
| **Hezbollah** | Defense in depth & firepower | Kornet/Almas ATGMs, fortified hills, Rocket Stockpile, drones, layered anti-air |

> **Working title** *Iron Swords: Frontlines* is a placeholder.

## V1 at a glance
- **Mode:** 1v1 skirmish vs AI. Three difficulty levels: **Easy (Recruit) / Normal (Veteran) / Hard (Elite)**.
- **Factions:** all three, every matchup including mirror matches.
- **Base building:** 4 tech tiers per faction, defensive structures, and faction-unique buildings (tunnels, Iron Dome, rocket depots).
- **Special weapons:** air strikes, bunker-busters, rocket salvos, tunnel bombs, heavy rockets, drone swarms.
- **Pre-match loadout:** 2 doctrines (commander trees) and 2 heroes per faction.
- **Progression:** unit veterancy (Vet 0–3) and hero levels (Lv 1–5).
- **Voices:** full unit voice-over (VO) in native languages (Hebrew, Gazan Arabic, Lebanese Arabic) with English subtitles.
- **Content:** 4 maps plus 1 stretch map, and a basic tutorial.
- **Deferred to V2+:** cinematics, campaign, multiplayer, map editor.

## Engine recommendation
**Unity 6 LTS (URP, C#)** with a deterministic simulation core. Full rationale, alternatives (Unreal 5, Godot), architecture and the complete "what we need" list are in [docs/08-engine-and-technology.md](docs/08-engine-and-technology.md).

## Design documents
| # | Document | Contents |
|---|---|---|
| 01 | [Vision & Scope](docs/01-vision-and-scope.md) | Pillars, V1 in/out scope, content & platform rules, open decisions |
| 02 | [Core Gameplay](docs/02-core-gameplay.md) | Economy, territory, combat model, cover, suppression, detection, destruction, win conditions |
| F1 | [Faction — IDF](docs/factions/idf.md) | Mechanics, tech tree, units, defenses, special weapons, doctrines, heroes |
| F2 | [Faction — Hamas](docs/factions/hamas.md) | Same structure |
| F3 | [Faction — Hezbollah](docs/factions/hezbollah.md) | Same structure |
| 03 | [Veterancy & Heroes](docs/03-veterancy-and-heroes.md) | XP model, vet bonuses, hero progression & abilities |
| 04 | [AI & Difficulty](docs/04-ai-and-difficulty.md) | AI architecture, difficulty parameters, tooling |
| 05 | [Maps](docs/05-maps.md) | V1 map pool, layout rules, ground types |
| 06 | [Audio, Voice & Cinematics](docs/06-audio-voice-cinematics.md) | VO plan, bark system, music, cinematics roadmap |
| 07 | [UI / UX](docs/07-ui-ux.md) | HUD, controls, overlays, localization (RTL), accessibility |
| 08 | [Engine & Technology](docs/08-engine-and-technology.md) | Engine choice, architecture, middleware, hardware, licenses |
| 09 | [Production Roadmap](docs/09-production-roadmap.md) | Milestones, team, budget bands, risks |

## Status
Pre-production. All numbers are **initial tuning values**. They will change through balance work and automated AI-vs-AI simulation.
