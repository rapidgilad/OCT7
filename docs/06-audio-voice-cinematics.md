# 06 — Audio, Voice & Cinematics

## 1. Unit voices (V1)
### Languages
| Faction | VO language | Subtitles |
|---|---|---|
| IDF | Hebrew (military radio slang) | English (+ Hebrew / Arabic UI options) |
| Hamas | Gazan Arabic | English |
| Hezbollah | Lebanese Arabic | English |

An English dub is a V2 option. Native languages are the V1 default for authenticity.

### Line categories (per unit type)
| Category | Lines (approx.) | Examples (EN gloss) |
|---|---|---|
| Select | 6 | "Squad ready." |
| Move | 8 | "Moving." / "On the way." |
| Attack | 6 | "Engaging!" |
| Ability | 4–8 | "Smoke out!" / "Firing salvo!" |
| Under fire | 4 | "Taking fire!" |
| Suppressed / pinned | 3 | "Can't move, pinned!" |
| Retreat | 4 | "Fall back!" |
| Casualty | 3 | "Man down!" |
| Vet-up | 2 | "We're getting sharper." |
| Contextual callouts | 6 | "RPG!", "Tank!", "Sniper!", "Drone overhead!" / "Zanana!", "Tzeva Adom!" |

### Volume estimate
| Source | Lines |
|---|---|
| Standard unit types: ~16 per faction × ~45 lines | ~720 per faction |
| Heroes: 2 per faction × ~150 lines | ~300 per faction |
| HQ radio / announcer ("Sector captured", "Our VP is under attack") | ~120 per faction |
| **Total** | **~1,150 per faction, ~3,500 overall** |

**Cast:** 5–6 actors per faction (squad members, vehicle crews, heroes, announcer). Total about 16–18 actors.

### VO pipeline
1. **Script database** (spreadsheet → importer). Columns: line ID, unit, category, context/trigger, line (native script), transliteration, English subtitle, priority, cooldown.
2. **Placeholder TTS** voices during development (pre-production to Alpha). This keeps barks testable from day 1.
3. **Casting at Alpha.** Arabic voice talent availability is a schedule risk: start early and use multiple vendors.
4. **Recording at Beta:** 48 kHz / 24-bit, 3 takes per line, an actor's direction sheet per unit personality.
5. **Implementation** in FMOD or Wwise (random containers per category, vet-level switches).

### Bark manager (runtime system)
- **Priorities:** callouts > casualty > under fire > ability > move/select.
- **Cooldowns:** per line, per category and per unit type (anti-spam).
- **Proximity:** only units on screen or near the camera bark. Off-screen critical events go through the HQ radio.
- **Concurrency limits:** at most 2 squad barks and 1 radio line at a time.
- **Vet switching:** Vet 3 units use the "veteran" variant set.

## 2. Music & SFX
- **Adaptive score:** three intensity layers (calm, tension, combat) crossfaded by the combat-intensity metric, plus a short motif per faction. The score is **original**: no real anthems or organizational songs.
- **Instrumentation:**
  - IDF: modern orchestral with percussion.
  - Hamas and Hezbollah: regional instruments (oud, ney, darbuka) blended with a cinematic orchestral base.
- **Weapon SFX:** authentic recordings or licensed libraries, with near/mid/far layers, an indoor/outdoor reverb split and tail occlusion.
- **Gameplay-critical audio cues:**

  | Cue | Meaning |
  |---|---|
  | Tzeva Adom siren + interception pops | Incoming rockets (IDF) |
  | Drone buzz ("zanana") | Enemy drone nearby |
  | Trophy activation crack | APS intercepted a shot |
  | Tunnel-digging rumble | Heard by the Seismic Sensor |
  | Rocket-launch whoosh | Reveals a launch-cell direction |

## 3. Cinematics — deferred to V2 (campaign)
**V1 contains no cinematics.** Preparation work in V1 avoids rework later:

| V1 prep item | Why |
|---|---|
| Camera rig hooks (Cinemachine virtual cameras on maps) | Map intros and briefings can be added without rework |
| Character rig standard (shared skeleton + facial blendshape set) | The same models work in cutscenes |
| VO metadata includes speaker ID + emotion tags | Lip-sync and performance capture later |
| Hero model LOD0 built to "close-up" quality | Heroes are the likely cinematic stars |

### Recommended V2 approach: hybrid
| Option | Pros | Cons |
|---|---|---|
| **In-engine (Unity Timeline + Cinemachine)** | Consistent with gameplay visuals, cheap iteration, small download | Lower fidelity than offline rendering |
| **Pre-rendered (Unreal Engine 5 + MetaHuman, or Blender)** | Top-tier fidelity for trailers and key story beats | Large video files; visual mismatch with gameplay |
| **Hybrid (recommended)** | In-engine for briefings and map intros; pre-rendered for 3–5 key story scenes and trailers | Two pipelines to maintain |

**Needed for cinematics:** script and storyboard, body mocap (studio or Rokoko suits), facial capture (Live Link Face / Faceware), a cinematic lighting artist, an editor and a composer.
