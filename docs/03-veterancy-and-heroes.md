# 03 — Veterancy & Heroes

## 1. Unit veterancy (Vet 0 → Vet 3)

### XP sources
| Source | XP |
|---|---|
| Damage dealt | Damage × (target cost ÷ target max HP) × 0.1 |
| Kill bonus | 10% of the killed unit's resource value |
| Sector capture | 10 per capture |
| Support actions (spotting for a kill, healing, detecting camo units or IEDs, repairs) | 5–15 per action |

Each unit has XP thresholds by cost class:

| Cost class | Example | Vet 1 | Vet 2 | Vet 3 |
|---|---|---|---|---|
| Light (≤ 250 MP) | Qassam Fighters, Engineers | 120 | 300 | 600 |
| Medium (251–400 MP) | Golani, Kornet team, Sniper | 160 | 400 | 800 |
| Heavy vehicle / elite | Merkava, Radwan, T-72 | 240 | 600 | 1200 |

### Bonuses by unit category
| Level | Infantry | Support weapons (MG, mortar, ATGM) | Vehicles |
|---|---|---|---|
| **Vet 1** | Unlocks the unit's Vet ability (for example, Golani *Fire & Maneuver*), +10% accuracy | −25% setup time | Unlocks the vehicle's Vet ability, +15% rotation speed |
| **Vet 2** | −10% received accuracy, +20% suppression resistance | +10% accuracy, +5% range | −15% reload time |
| **Vet 3** | +15% accuracy, −20% reload time, +10% speed | −15% received accuracy | +10% armor; self-repair to 50% HP when out of combat for 20 s |

### Retention rules
- Reinforcing a squad **keeps** its veterancy.
- A squad wipe or a destroyed vehicle **loses** its veterancy.
- **IDF:** CASEVAC recovery means no XP loss for the recovered model.
- **Hamas:** retreating through a tunnel preserves XP.
- **Hezbollah:** squads bought with *Veteran Cadres* or *Syria Veterans* start at Vet 1.

### Presentation
- Chevrons on the unit card and over the unit.
- A promotion bark ("Unit is getting sharp!") and a distinct sound.
- Vet 3 squads use a "veteran" voice set: calmer and more confident.

## 2. Heroes

> **Solo V1:** each faction ships **one hero**: IDF Ronen, Hamas Al-Khuld, Hezbollah Al-Sayyad. The hero picker exists from the start, so the second heroes slot in later without UI changes.

### Rules
- Choose **1 of 2** heroes in the pre-match loadout. The hero deploys from the HQ after **T1** is built.
- Only **one hero** may be on the field at a time.
- Heroes level from **Lv 1 to Lv 5**. XP thresholds: 0 / 400 / 1000 / 2000 / 3500.
- Progression pattern:

  | Level | Unlock |
  |---|---|
  | Lv 1 | Ability A |
  | Lv 2 | Aura: friendly units within 20 m get +10% accuracy and −10% received accuracy |
  | Lv 3 | Ability B |
  | Lv 4 | Aura upgrade: +15% accuracy, −15% received accuracy, +suppression resistance |
  | Lv 5 | Ultimate |

- **Death:** redeploy after 120 s, keeping 50% of the hero's XP. The enemy gains +1 CP and a "Hero down!" bark.
- **Voice:** about 150 unique lines per hero, with personality and reactions to enemy heroes.
- **Names:** all hero names are **fictional**. Run a name-clearance check before content lock.

### IDF heroes
| | **Maj. Ido "Ze'ev" Ronen** — Sayeret commander | **Lt. Col. Tamar "Lahav" Adler** — Armored battalion commander |
|---|---|---|
| Unit | Hero squad: commander + 3 operators. 380 MP / 40 MU, pop 9 | Merkava Mk.5 "Barak" command tank. 520 MP / 200 FU, pop 18 |
| Lv 1 | *Target Designation*: free Intel lock on a target for 20 s | *Command Aura (armor)*: tanks within 25 m −15% reload |
| Lv 2 | Aura | Aura (applies to vehicles and infantry) |
| Lv 3 | *Breach & Clear*: flashbangs; the next garrison assault forces enemy occupants out and pins them | *IronVision*: detection 3 at 30 m around the tank |
| Lv 4 | Aura upgrade | Aura upgrade + *Trophy* charges 3 → 5 |
| Lv 5 (Ult) | *Deep Strike*: squad becomes camo 3 for 30 s and can plant demo charges on structures | *Fire Weaver Network*: every friendly mortar, M109 and Pereh fires on her target once |

### Hamas heroes
| | **"Al-Khuld" (The Mole)** — tunnel commander | **"Al-Shabah" (The Ghost)** — marksman |
|---|---|---|
| Unit | Hero squad: commander + 3 bodyguards. 300 MP / 30 MU, pop 7 | Single sniper with a modified Al-Ghoul rifle. 340 MP / 30 MU, pop 6 |
| Lv 1 | *Rapid Dig*: creates a temporary tunnel exit (60 s) anywhere within 40 m | *Anti-Materiel Shot*: destroys the optics on a vehicle (−50% sight and accuracy for 30 s) or disables a light vehicle's engine |
| Lv 2 | Aura | Aura (to units within 30 m) |
| Lv 3 | *Ambush Command*: all friendly infantry within 25 m become camo 2 for 20 s | *Ghost Walk*: uses any tunnel entrance as an instant relocation (cooldown 60 s) |
| Lv 4 | Aura upgrade | Aura upgrade + *Decoy* (fake muzzle flashes reveal a false position) |
| Lv 5 (Ult) | *Underground Assault*: one Qassam squad per tunnel entrance (up to 3) emerges instantly, free | *Headhunter*: the next shot kills a squad leader (the squad is pinned for 6 s) or a vehicle's commander (the vehicle is stunned for 6 s) |

### Hezbollah heroes
| | **"Al-Jabal" (The Mountain)** — Radwan field commander | **"Al-Sayyad" (The Hunter)** — Kornet ace |
|---|---|---|
| Unit | Hero squad: commander + 4 Radwan. 420 MP / 40 MU, pop 10 | 2-man Kornet crew. 360 MP / 40 MU, pop 7 |
| Lv 1 | *Radwan Assault*: squad sprints and is immune to suppression for 10 s | *Tandem Volley*: +50% penetration for the next shot |
| Lv 2 | Aura | Aura |
| Lv 3 | *Instant Fortify*: builds sandbags and a trench around the squad in 3 s | *Hidden Position*: camo 3 while emplaced and not firing |
| Lv 4 | Aura upgrade | Aura upgrade + *Double-Tap ×2* (four missiles in two pairs) |
| Lv 5 (Ult) | *Strike Group*: calls in 2 Radwan squads at Vet 1 at his position | *Tank Hunter*: the next 3 shots ignore APS and ERA and always penetrate |

## 3. Balance guardrails
- A Lv 5 hero should be worth about 1.5 equivalent-cost units, not more.
- Aura bonuses do not stack between heroes (moot in V1, since only one hero is allowed) or with doctrine auras. The highest bonus applies.
- Ultimates have a cooldown of at least 180 s.
- Easy AI uses Hero B less often (it is more micro-intensive). Hard AI uses either hero.
