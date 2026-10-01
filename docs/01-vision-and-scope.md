# 01 — Vision & Scope

## High concept
A Company-of-Heroes-style tactical RTS in which three asymmetric modern forces fight over contested urban districts, farmland and rocky hills. Squads use cover, suppress, flank and retreat. Bases are built, teched and defended. Each faction has signature special weapons and counter-weapons, so every match is a duel of systems, not just of unit counts.

- **Genre:** squad-based tactical RTS
- **Platform:** PC (Windows), Steam
- **Input:** mouse and keyboard
- **Target rating:** ESRB M / PEGI 18
- **Match length:** 20–35 minutes

## Design pillars
| Pillar | Player-facing meaning | Design tests |
|---|---|---|
| **Asymmetry with parity** | Each faction feels like a different game, yet all are equally viable. | Win rates within 47–53% per matchup in AI-vs-AI and playtests. |
| **Cover, terrain & destruction matter** | Position beats numbers. Rubble, hills, buildings and tunnels decide fights. | A squad in heavy cover must beat an equal-cost squad in the open. |
| **Counter-play loops** | Every signature weapon has a readable, buildable counter. | No ability or unit without at least two counters (see counter matrix in [02](02-core-gameplay.md)). |
| **Units are characters** | Veterancy, heroes and native-language voices make squads memorable. | Players name and protect their veteran squads; retreat is used, not ignored. |

## Match phases
| Phase | Time | Typical play |
|---|---|---|
| Early | 0–6 min | Capture sectors, infantry skirmishes, first tier building, scouting |
| Mid | 6–15 min | Support weapons, light vehicles, tunnels/fortifications, first doctrine abilities, hero deployment |
| Late | 15+ min | Armor, T4 special weapons, heroes at Lv 4–5, ticket race over victory points (VPs) |

## V1 scope
| IN | OUT (V2+) |
|---|---|
| 3 factions (IDF, Hamas, Hezbollah), all matchups including mirror | Online multiplayer (1v1, 2v2), lobbies, ranked |
| 1v1 skirmish vs AI: Easy / Normal / Hard | Story campaign |
| 4 maps (+1 stretch map) | **Cinematics** (planned hooks only, see [06](06-audio-voice-cinematics.md)) |
| Base building: 4 tech tiers + defenses per faction | Map editor, mod support |
| 2 doctrines + 2 heroes per faction, chosen pre-match | Night maps, dynamic weather |
| Veterancy: units Vet 0–3, heroes Lv 1–5 | Additional factions or doctrines |
| Special weapons per faction | Player-facing replays (internal replay tool exists) |
| Full unit VO in native languages + English subtitles | English dub |
| Tutorial ("Basic Training") | Achievements, cosmetics |
| UI in English, Hebrew, Arabic | Other languages |

**Cut line, if schedule slips:** ship 1 doctrine per faction and 3 maps. Heroes, veterancy and the 3 AI levels are never cut.

## Content & platform rules
These are production constraints driven by store approval, age rating and regional law.

1. **No civilian NPCs and no civilian-harm mechanics in V1.** Maps are evacuated combat zones. There are no hostage or kidnapping mechanics.
2. **Heroes are fictional.** No real persons are depicted. Every hero name gets a name-clearance check before content lock.
3. **Names and insignia are data-driven.** Faction display names, emblems, flags and patches are localization keys plus swappable textures. A regional variant (for example, for Germany, where Hamas and Hezbollah symbols are prohibited) is then a configuration, not a content rebuild.
4. **Original music only.** No real anthems, organizational songs or propaganda audio.
5. **Protected sites:** religious buildings and hospitals on maps are non-destructible, non-garrisonable set dressing.
6. **VO scripts are tactical and military.** No slurs and no incitement. This keeps localization and rating review clean.

## Open decisions for the project owner
| Decision | Options | Recommendation |
|---|---|---|
| Faction display names | (A) Real names everywhere · (B) Real names + regional variants · (C) Fictionalized "inspired-by" names | **B.** The data-driven pipeline makes it cheap. |
| Release model | (A) Full release at month ~24 · (B) Early Access at month ~14 with IDF + Hamas, Hezbollah added during EA | **B** if funding or feedback is a priority. **A** if first impressions are a priority. |
| Hard-AI cheats | (A) Fair only · (B) Add "Insane" with resource bonus | **A for V1.0**, B in V1.1 |
