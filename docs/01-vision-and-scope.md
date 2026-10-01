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

## Production context
**V1 is built by a solo developer, with Claude Code as the coding partner.** Scope, engine and pipeline are chosen for one person. The full design in the faction docs stays as the **long-term vision**. Each faction doc marks the **Solo V1 core roster**, which is the part V1 actually ships.

## V1 scope (solo)
| IN — Solo V1 | LATER (V1.x / V2+, or when the team grows) |
|---|---|
| 3 factions (IDF, Hamas, Hezbollah), all matchups including mirror | Full rosters (about 15 units per faction) |
| About 10 units per faction (core roster) | Second doctrine and second hero per faction |
| 1v1 skirmish vs AI: Easy / Normal / Hard | Online multiplayer (1v1, 2v2), ranked |
| 2 maps (Border Ridge Outpost, Khan Sahil) | Maps 3–5 |
| Base building: HQ + 3 tech tiers + defenses per faction (T4 content folded into T3) | 4th tech tier |
| 1 doctrine + 1 hero per faction | Story campaign and **cinematics** (hooks only, see [06](06-audio-voice-cinematics.md)) |
| Veterancy: units Vet 0–3, heroes Lv 1–5 | Map editor, mod support |
| 2 special weapons per faction | Night maps, weather |
| Unit VO: AI text-to-speech (TTS) in native languages + English subtitles, about 1,000 lines | Recorded human VO, English dub |
| In-game tooltips + a "faction primer" screen | Full scripted tutorial |
| UI in English (Hebrew and Arabic UI as V1.1) | Achievements, cosmetics |

**Build order:** IDF vs Hamas first (fully playable), then Hezbollah. This gives a playable game at every stage.

**Never cut:** the 3 AI levels, veterancy, heroes, and the faction-unique systems (Intel, tunnels, Rocket Stockpile, Iron Dome, Trophy).

**Cut line, if needed:** Hezbollah moves to V1.1, and V1 ships as IDF vs Hamas on 2 maps.

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
| Release model (solo) | (A) Full release when all 3 factions are done · (B) Free itch.io demo (IDF vs Hamas) + Steam page, then Steam Early Access, with Hezbollah added during Early Access | **B.** Feedback, wishlists and a playable build attract future collaborators. |
| Engine | (A) Godot 4 .NET + pure C# simulation · (B) Unity 6 | **A** for a solo developer working with Claude Code (see [08](08-engine-and-technology.md)) |
| Code visibility | (A) Private repo · (B) Open-source code, with assets licensed separately | Either works with Godot. With B, paid asset packs must stay out of the public repo (see [08](08-engine-and-technology.md)). |
| Hard-AI cheats | (A) Fair only · (B) Add "Insane" with resource bonus | **A for V1.0**, B in V1.1 |
