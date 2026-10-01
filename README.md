# Iron Swords: Frontlines — V1 Game Design (codename OCT7)

A squad-based tactical RTS in the Company of Heroes tradition, set in modern Middle East warfare. It has three asymmetric factions:

| Faction | Identity | Signature systems |
|---|---|---|
| **IDF** | Precision & combined arms | Merkava + Trophy APS, Iron Dome, Intel-driven air strikes, K9 & drones |
| **Hamas** | The Underground | Tunnel network, ambush & stealth, IED/EFP, rockets, quadcopters |
| **Hezbollah** | Defense in depth & firepower | Kornet ATGMs, fortified hills, Rocket Stockpile, layered anti-air |

> **Working title** *Iron Swords: Frontlines* is a placeholder.

## Production setup
**Solo developer + Claude Code.** V1 scope, engine and pipeline are sized for one person. The faction docs hold the full long-term vision and mark the **Solo V1 core roster** that actually ships.

## Solo V1 at a glance
- **Mode:** 1v1 skirmish vs AI. Three difficulty levels: **Easy (Recruit) / Normal (Veteran) / Hard (Elite)**.
- **Factions:** all three, with about 10 units each. IDF vs Hamas is built first, then Hezbollah.
- **Base building:** HQ + 3 tech tiers, defenses and faction-unique structures (tunnels, Iron Dome, rocket depots, bunkers).
- **Special weapons:** 2 per faction (precision strike, bunker-buster, rocket salvo, tunnel bomb, Burkan, Fadi).
- **Pre-match loadout:** 1 doctrine and 1 hero per faction.
- **Progression:** unit veterancy (Vet 0–3) and hero levels (Lv 1–5).
- **Content:** 2 maps.
- **Voices:** AI text-to-speech (TTS) in Hebrew, Gazan Arabic and Lebanese Arabic, with English subtitles. Scripts are reviewed by native speakers.
- **Later:** cinematics, campaign, multiplayer, full rosters, second doctrine and hero per faction.

## Engine recommendation
**Godot 4 (.NET / C#)**, with all game rules in a **pure C# simulation library** that runs and tests headless.

This fits a solo developer working through Claude Code:
- Scene files are short, readable text.
- Everything can be built and tested from the command line.
- The engine is free, with an MIT license.
- The game logic can later be ported to Unity or Unreal if a team joins.

Full rationale, project layout, tools, asset sources and licensing: [docs/08-engine-and-technology.md](docs/08-engine-and-technology.md).

## Design documents
| # | Document | Contents |
|---|---|---|
| 01 | [Vision & Scope](docs/01-vision-and-scope.md) | Pillars, solo V1 scope, content & platform rules, open decisions |
| 02 | [Core Gameplay](docs/02-core-gameplay.md) | Economy, territory, combat model, cover, suppression, detection, destruction, win conditions |
| F1 | [Faction — IDF](docs/factions/idf.md) | V1 core roster + full vision: mechanics, tech tree, units, specials, doctrines, heroes |
| F2 | [Faction — Hamas](docs/factions/hamas.md) | Same structure |
| F3 | [Faction — Hezbollah](docs/factions/hezbollah.md) | Same structure |
| 03 | [Veterancy & Heroes](docs/03-veterancy-and-heroes.md) | XP model, vet bonuses, hero progression & abilities |
| 04 | [AI & Difficulty](docs/04-ai-and-difficulty.md) | AI architecture, difficulty parameters, MatchRunner tooling |
| 05 | [Maps](docs/05-maps.md) | Map pool, layout rules, ground types |
| 06 | [Audio, Voice & Cinematics](docs/06-audio-voice-cinematics.md) | TTS voice plan, bark system, music, cinematics roadmap |
| 07 | [UI / UX](docs/07-ui-ux.md) | HUD, controls, overlays, localization (RTL), onboarding |
| 08 | [Engine & Technology](docs/08-engine-and-technology.md) | Godot vs Unity for solo, architecture, tools, assets, licensing |
| 09 | [Production Roadmap](docs/09-production-roadmap.md) | Solo milestones, budget, weekly rhythm, risks, next 30 days |

## Status
Pre-production (design complete). All numbers are **initial tuning values**. They will change through balance work and automated AI-vs-AI simulation (MatchRunner).
