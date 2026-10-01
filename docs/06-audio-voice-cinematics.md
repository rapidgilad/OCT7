# 06 — Audio, Voice & Cinematics

## 1. Unit voices (V1)

> **Solo V1 voice plan**
> - **Volume:** about 25 lines per unit type (select 3, move 4, attack 3, ability 3, under fire 2, retreat 2, casualty 2, vet-up 1, callouts 5), about 60 per hero and about 40 per HQ announcer. That's **≈ 350 per faction, ≈ 1,000 total.**
> - **Production:**
>   1. Claude drafts each line in English plus Hebrew, Gazan Arabic or Lebanese Arabic, with a transliteration.
>   2. A **native speaker reviews** each faction's script (paid hourly).
>   3. Lines are generated with **AI text-to-speech (TTS)** voices, 3–4 distinct voices per faction. Pitch and radio filters add variety.
>   4. Use licensed TTS voices only. **No cloning of real people's voices.**
> - **Upgrade path:** the script database and line IDs stay the same, so human actors can replace TTS files one by one later.
> - The tables below describe the full-scale target.

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

> **Solo V1:** use game-licensed music packs or one commissioned 3-layer track per faction. SFX come from free GDC bundles (Sonniss) plus one paid weapons pack. Godot audio buses handle the layering.

- **Adaptive score:** three intensity layers (calm, tension, combat) crossfaded by the combat-intensity metric, plus a short motif per faction. The score is **original**: no real anthems or organizational songs.
- **Instrumentation:**
  - IDF: modern orchestral with percussion.
  - Hamas and Hezbollah: regional instruments (oud, ney, darbuka) blended with a cinematic orchestral base.
- **Weapon SFX:** authentic recordings or licensed libraries, with near/mid/far layers, an indoor/outdoor reverb split and tail occlusion.
- **Status (v0.1): placeholder SFX are synthesized in code.**
  - **Where:** `game/scripts/Audio/SoundSynth.cs`.
  - **Sounds:** rifle, machine gun, sniper, cannon (tank gun; the autocannon uses it pitched up), rocket launch and explosion. Each has 2–4 variants plus random pitch and volume per shot.
  - **Playback:** `AudioManager` plays them positionally from sim events, heard from the camera.
    - Rocket and shell impacts are delayed to match the VFX.
    - Vehicles and buildings explode when destroyed.
    - Only fights the local player can see make sound, and starts per frame are capped.
  - **Replacing them:** drop a recording into `game/assets/audio/` (see the README there) and it replaces the synthesized sound with no code change.
  - **Volume:** the slider in the main and pause menus is saved to `user://settings.cfg`.
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
| Camera rig hooks (named Camera3D markers + AnimationPlayer camera paths on maps) | Map intros and briefings can be added without rework |
| Character rig standard (shared skeleton + facial blendshape set) | The same models work in cutscenes |
| VO metadata includes speaker ID + emotion tags | Lip-sync and performance capture later |
| Hero model LOD0 built to "close-up" quality | Heroes are the likely cinematic stars |

### Recommended V2 approach: hybrid
| Option | Pros | Cons |
|---|---|---|
| **In-engine (Godot AnimationPlayer + camera paths)** | Consistent with gameplay visuals, cheap iteration, small download | Lower fidelity than offline rendering |
| **Pre-rendered (Unreal Engine 5 + MetaHuman, or Blender)** | Top-tier fidelity for trailers and key story beats | Large video files; visual mismatch with gameplay |
| **Hybrid (recommended)** | In-engine for briefings and map intros; pre-rendered for 3–5 key story scenes and trailers | Two pipelines to maintain |

**Needed for cinematics:** script and storyboard, body mocap (studio or Rokoko suits), facial capture (Live Link Face / Faceware), a cinematic lighting artist, an editor and a composer.
