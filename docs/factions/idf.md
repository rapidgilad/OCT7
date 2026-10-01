# Faction — IDF: "Precision & Combined Arms"

## Identity
Expensive, elite and technological. The IDF has the best armor, the best detection and dominant air support. Every unit counts, and losses hurt.

| Strengths | Weaknesses |
|---|---|
| Merkava + Trophy, heavy APCs | Small army, high cost per unit |
| Best detection: K9, drones, towers | Precision weapons depend on Intel |
| Air power and precision strikes | Air call-ins vulnerable to AA |
| Iron Dome / Iron Beam vs rockets | Iron Dome can be saturated |

**Visual identity:** olive and sand-tan, digital displays, clean angular silhouettes.
**Voice:** Hebrew, calm and professional radio discipline, military slang.

## Solo V1 core roster
This is what ships in V1. Everything else in this document is the long-term vision.

| Tier | V1 content |
|---|---|
| HQ | Yahalom Combat Engineers, Oketz K9 Team, *Skylark Drone* ability |
| T1 | Golani Rifle Squad, MAG Machine Gun Team, Sniper Team |
| T2 | Spike MR ATGM Team, Namer Heavy APC (with Ambulance Kit = CASEVAC), Iron Dome Battery |
| T3 | Merkava Mk.4M, D9R Armored Bulldozer; **T4 content folded in:** special weapons below |
| Special weapons | F-16I Precision Strike, GBU-28 Bunker-Buster |
| Doctrine | **Air Force & Intelligence** (in V1, rank 4 becomes *Apache Strafing Run*, since the T4 Apache isn't in V1) |
| Hero | **Maj. Ido "Ze'ev" Ronen** |
| Later | Mortar, Hummer, Sayeret, Eitan, M109, Pereh, Field Aid Station, Iron Beam, Armored Corps doctrine, Lt. Col. Adler |

## Unique mechanics
1. **Intel (INT, cap 100)**

   | Source | INT |
   |---|---|
   | Each active drone or camera tower | +1 per 10 s |
   | First sighting of an enemy structure | +5 |
   | Enemy squad or vehicle destroyed | +3 |
   | K9 finds an IED, tunnel entrance or camo unit | +2 |

   INT is spent on precision-strike locks (F-16I, GBU-28) and **Tunnel Reveal** (30 INT: reveals the tunnel entrances in a 40 m radius for 60 s).
2. **CASEVAC.** When an IDF soldier falls, there is a 50% chance a **wounded marker** stays for 25 s. A Namer with the ambulance kit, or the Field Aid Station's medics, can recover it. This refunds 50% of the model's cost to the squad's next reinforcement and loses no XP.
3. **Combined arms.** Tanks within 15 m of friendly infantry detect camo-1 AT teams.
4. **"Tzeva Adom" alert.** Radar coverage gives a 3–5 s warning, with a siren, a voice line and a ground marker, before enemy rockets land inside IDF territory.

## Tech tree
| Building | Cost (MP/FU) | Requires | Unlocks |
|---|---|---|---|
| Brigade Forward HQ | start | — | Engineers, K9, Skylark drone, global upgrades |
| T1 Infantry Company Post | 180 / 15 | HQ | Golani, MAG, Sniper, Mortar |
| Field Aid Station | 150 / 0 | T1 | Healing aura, retreat point, CASEVAC recovery |
| T2 Combat Support Base | 200 / 60 | T1 | Spike MR, Namer, Hummer, Sayeret |
| Iron Dome Battery (max 2) | 250 / 60 | T2 | Interception |
| T3 Armored Battalion Yard | 300 / 120 | T2 | Merkava, Eitan, D9R, M109 |
| T4 Air Force Liaison Cell | 250 / 150 | T3 | Pereh, air special weapons, Iron Beam upgrade |

## Units
| Tier | Unit | MP / MU / FU | Pop | Role & abilities |
|---|---|---|---|---|
| HQ | **Yahalom Combat Engineers** (4) | 220 / 0 / 0 | 5 | Build, repair, mines, demolition charge (40 MU); mine-sweeper upgrade (35 MU); tunnel demolition (requires T2): destroys a revealed tunnel entrance in 10 s |
| HQ | **Oketz K9 Team** (handler + dog) | 160 / 0 / 0 | 3 | Detection 3, 25 m: IEDs, tunnel entrances, camo units; *Attack* (dog pins a target squad for 4 s). Max 2 |
| HQ | *Skylark Drone* (ability) | 0 / 30 / 0 | — | 60 s recon circle, detection 2, +INT; can be shot down |
| T1 | **Golani Rifle Squad** (5) | 300 / 0 / 0 | 7 | Tavor X95; Negev LMG (60 MU); MATADOR anti-structure rocket (50 MU); frag grenade (25 MU); smoke grenade (15 MU) |
| T1 | **MAG Machine Gun Team** (3) | 260 / 0 / 0 | 6 | Heavy suppression, 90° firing arc, setup time 2 s |
| T1 | **Sniper Team** (2) | 340 / 0 / 0 | 6 | Long range, camo 1 when stationary, one-shot infantry |
| T1 | **60mm Commando Mortar** (3) | 240 / 0 / 0 | 5 | Indirect fire; smoke barrage (20 MU) |
| T2 | **Spike MR ATGM Team** (3) | 300 / 20 / 0 | 6 | Fire-and-forget AT, long range, 4 s setup |
| T2 | **Namer Heavy APC** | 300 / 0 / 70 | 10 | Heavy armor, .50 cal remote weapon station (RWS), carries 1 squad, reinforce point; *Ambulance Kit* (40 MU) adds CASEVAC recovery and slow healing |
| T2 | **Hummer Patrol** | 220 / 0 / 15 | 5 | Fast, MAG, detection 1 at 45 m, transports 1 team |
| T2 | **Sayeret Recon Team** (4) | 400 / 0 / 0 | 8 | Elite; camo 1; *Laser Designator*: −50% INT cost on strikes for 20 s; breaching charges. Max 2 |
| T3 | **Merkava Mk.4M** | 480 / 0 / 180 | 16 | Main battle tank (MBT), 120mm; **Trophy APS**; *Kalanit APAM round* (35 MU, anti-infantry airburst); *Smoke launchers* |
| T3 | **Eitan AFV (8x8)** | 340 / 0 / 90 | 10 | 30mm autocannon, carries 1 squad, fast wheeled chassis |
| T3 | **D9R Armored Bulldozer** | 300 / 0 / 70 | 8 | Very high armor, no main weapon; clears rubble, obstacles and IEDs; crushes revealed tunnel entrances and light structures |
| T3 | **M109 "Doher" SPH** | 380 / 0 / 100 | 12 | 155mm barrage (needs vision); counter-battery mode |
| T4 | **Pereh NLOS Carrier** | 400 / 0 / 150 | 14 | Disguised tank; fires Spike NLOS at any **spotted** target in a very long radius; 25 s reload |

## Defenses
| Structure | Built by | Cost | Notes |
|---|---|---|---|
| Sandbags | Engineers | 15 MP | Heavy cover |
| Concertina wire | Engineers | 10 MP | Blocks infantry |
| Tank traps | Engineers | 25 MP | Blocks vehicles |
| AT / AP mines | Engineers | 40 MU / 30 MU | Hidden, camo 2 |
| MAG Emplacement | Engineers | 220 MP | Fortified MG |
| Camera Observation Tower | Engineers | 150 MP / 15 FU | Detection 2 at 50 m, +INT; *Seismic Sensor* upgrade (40 MU) warns of tunnel bombs and digging |
| Iron Dome Battery | Engineers | 250 MP / 60 FU | 60 m radius; 12 interceptors |
| Iron Beam (upgrade) | T4 | 100 MU / 100 FU | Adds laser interception vs drones, mortars and single rockets |

## Special weapons (T4 Air Force Liaison Cell)
| Weapon | Cost | Cooldown | Effect | Counters |
|---|---|---|---|---|
| **F-16I "Sufa" Precision Strike** | 150 MU + 40 INT | 120 s | One JDAM on a target lock, 3 s delay; destroys most structures; heavy damage in 8 m | "358" SAM (abort chance), denying vision |
| **AH-64 "Saraf" Strafing Run** | 200 MU | 150 s | 30mm and Hellfire run along a line | MANPADS, ZU-23 |
| **GBU-28 Bunker-Buster** | 250 MU + 60 INT | 240 s | Collapses all tunnel entrances in 20 m and damages the underground network segment; full damage to the Hamas Command Bunker | Denying vision, decoy entrances |

## Doctrines (choose one before the match)
| CP rank | **Armored Corps** | **Air Force & Intelligence** |
|---|---|---|
| 1 | *Smoke Barrage* (30 MU) | *Hermes 450 Recon* (50 MU, 120 s loiter, +INT) |
| 2 | *Fire Weaver Link* (passive: artillery and mortars +25% accuracy, −2 s call delay) | *Unit 8200 SIGINT Sweep* (60 MU: all enemy units shown on the minimap for 20 s) |
| 3 | *Veteran Crews* (passive: tanks and APCs start at Vet 1) | *Spike Firefly* (40 MU, infantry-launched loitering munition) |
| 4 | *Reserve Armor* (Merkava Mk.3 call-in, 400 MP / 120 FU) | *Close Air Support* (Apache −30% cost) |
| 5 | *Divisional 155mm Barrage* (200 MU, 12 rounds) | *F-35I "Adir" Strategic Strike* (250 MU + 50 INT, ignores the "358" abort) |

## Heroes
See [03 — Veterancy & Heroes](../03-veterancy-and-heroes.md).
- **Maj. Ido "Ze'ev" Ronen:** Sayeret commander squad.
- **Lt. Col. Tamar "Lahav" Adler:** Merkava Mk.5 "Barak" commander tank.

## Veterancy flavor
- CASEVAC preserves XP.
- *Hard Training* upgrade (HQ, 100 MU): new squads start with 10% of the XP needed for Vet 1.

## AI play-style notes
Builds Intel early (Skylark, towers), protects tanks with infantry, saves INT for strikes on launch cells and depots, and counters tunnels with K9 + D9R.
